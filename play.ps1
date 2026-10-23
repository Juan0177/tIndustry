<#
.SYNOPSIS
  Avvio unico di tIndustry per Windows (e pwsh su Linux): controlla o installa le dipendenze, compila, importa gli asset e avvia il gioco.
.DESCRIPTION
  Senza -Utente o -Dev chiede come entrare. Vedi -Help per tutte le opzioni.
#>
[CmdletBinding()]
param(
  [switch]$Utente,
  [switch]$Dev,
  [switch]$ProfiloTemp,
  [string]$Profilo = "",
  [switch]$Nuova,
  [switch]$Editor,
  [switch]$Test,
  [string]$Playtest = "",
  [switch]$SoloBuild,
  [switch]$Import,
  [switch]$Si,
  [switch]$Help
)

# Windows PowerShell 5.1 has no $IsWindows: treat it as Windows.
$script:OnWin = ($PSVersionTable.PSVersion.Major -lt 6) -or [bool]$IsWindows
$script:Root = $PSScriptRoot
$script:GodotVersionUrl = "https://github.com/godotengine/godot/releases/download/4.4.1-stable"

function Show-Help {
  @"
Uso: .\play.ps1 [-Utente | -Dev] [opzioni]

Controlla (e se serve installa) le dipendenze, compila, importa gli asset Godot se servono e avvia il gioco.
Senza -Utente o -Dev chiede: «Entrare come utente o come dev? [u/d]».

Modi:
  -Utente           gioca con il profilo reale del giocatore
  -Dev              come utente, in piu' i comandi di sviluppo (TINDUSTRY_DEV=1: F1-F4 = velocita' x1/x5/x10/x20)

Opzioni (utente e dev):
  -ProfiloTemp      profilo dati temporaneo: impostazioni e salvataggi reali non vengono toccati
  -Nuova            salta la Home e parte da una nuova partita (TINDUSTRY_FRESH=1)
  -Si               risponde «si'» a ogni domanda di installazione (senza terminale interattivo le risposte sono «no»)
  -Help             mostra questo aiuto

Opzioni solo dev:
  -Profilo DIR      usa DIR come profilo dati (persistente tra un avvio e l'altro)
  -Editor           apre l'editor Godot invece del gioco
  -Test             esegue anche i test xUnit prima di avviare
  -Playtest NOME    esegue il playtest NOME (ui | tutorial | agenti) invece di avviare il gioco
  -SoloBuild        compila (e importa se serve) senza avviare
  -Import           forza la reimportazione degli asset Godot

Variabili: GODOT o TINDUSTRY_GODOT = eseguibile di Godot 4.4 .NET (mono). In mancanza si cerca godot4 nel PATH,
poi in .dev\godot\; se non c'e' si offre di scaricarlo li'.
Per le prove usa sempre -ProfiloTemp.
"@ | Write-Host
}

function Write-Step($t) { Write-Host ""; Write-Host "==> $t" -ForegroundColor Yellow }
function Fail($m) { throw [System.InvalidOperationException]::new($m) }

# Ask a yes/no question; false when input is not interactive (unless -Si).
function Ask-YesNo($q) {
  if ($Si) { Write-Host "$q (y/n) y  [-Si]"; return $true }
  if ([Console]::IsInputRedirected) { Write-Host "$q (y/n) n  [terminale non interattivo]"; return $false }
  while ($true) {
    $a = (Read-Host "$q (y/n)").Trim().ToLowerInvariant()
    if ($a -in @("y", "yes", "s", "si", "sì")) { return $true }
    if ($a -in @("n", "no")) { return $false }
  }
}

# Re-read PATH from the machine and user environment so freshly installed tools are found.
function Update-SessionPath {
  if (-not $script:OnWin) { return }
  $m = [Environment]::GetEnvironmentVariable("Path", "Machine")
  $u = [Environment]::GetEnvironmentVariable("Path", "User")
  $env:Path = (@($m, $u) | Where-Object { $_ }) -join ";"
}

function Install-WithWinget($nome, $id, $urlManuale) {
  if (-not $script:OnWin -or -not (Get-Command winget -ErrorAction SilentlyContinue)) {
    Fail "$nome manca e winget non e' disponibile: installalo a mano ($urlManuale) e rilancia."
  }
  Write-Host "winget install --id $id -e"
  & winget install --id $id -e --accept-package-agreements --accept-source-agreements
  if ($LASTEXITCODE -ne 0) { Fail "Installazione di $nome non riuscita (winget ha risposto $LASTEXITCODE)." }
  Update-SessionPath
}

function Test-Dotnet8 {
  if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { return $false }
  $sdks = @(& dotnet --list-sdks 2>$null)
  return [bool]($sdks | Where-Object { $_ -match '^8\.' })
}

function Test-Python3 {
  $py = Get-Command python -ErrorAction SilentlyContinue
  if (-not $py) { return $false }
  try { $v = (& python --version 2>&1 | Out-String) } catch { return $false }
  return $v -match 'Python 3'
}

function Get-GodotVersion($exe) {
  try { return ((& $exe --version 2>$null | Select-Object -Last 1) | Out-String).Trim() } catch { return "" }
}

function Find-LocalGodot {
  $dir = Join-Path $script:Root ".dev/godot"
  if (-not (Test-Path $dir)) { return $null }
  $pat = if ($script:OnWin) { "Godot_v*_mono_win64_console.exe" } else { "Godot_v*_mono_linux.x86_64" }
  $f = Get-ChildItem -Path $dir -Recurse -File -Filter $pat -ErrorAction SilentlyContinue | Select-Object -First 1
  if ($f) { return $f.FullName }
  return $null
}

# Resolution order: $env:GODOT, $env:TINDUSTRY_GODOT, godot4 in PATH, .dev/godot/.
function Resolve-Godot {
  $cands = @()
  if ($env:GODOT) { $cands += $env:GODOT }
  if ($env:TINDUSTRY_GODOT) { $cands += $env:TINDUSTRY_GODOT }
  $cands += "godot4"
  $local = Find-LocalGodot
  if ($local) { $cands += $local }
  foreach ($c in $cands) {
    $cmd = Get-Command $c -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $cmd) { continue }
    $exe = if ($cmd.Source) { $cmd.Source } else { $cmd.Path }
    $v = Get-GodotVersion $exe
    if ($v -match '^4\.4\..*mono') { return @{ Exe = $exe; Version = $v } }
    Write-Host "Salto '$c': non e' Godot 4.4 .NET (mono) (risponde '$v')." -ForegroundColor DarkYellow
  }
  return $null
}

function Install-Godot {
  $nome = if ($script:OnWin) { "Godot_v4.4.1-stable_mono_win64" } else { "Godot_v4.4.1-stable_mono_linux_x86_64" }
  $url = "$($script:GodotVersionUrl)/$nome.zip"
  $dest = Join-Path $script:Root ".dev/godot"
  New-Item -ItemType Directory -Force -Path $dest | Out-Null
  $zip = Join-Path $dest "$nome.zip"
  Write-Host "Scarico $url"
  try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
  } catch { }
  $old = $ProgressPreference
  $ProgressPreference = "SilentlyContinue"
  try { Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing } catch { Fail "Download di Godot non riuscito: $($_.Exception.Message)" } finally { $ProgressPreference = $old }
  Write-Host "Estraggo in $dest"
  Expand-Archive -Path $zip -DestinationPath $dest -Force
  Remove-Item $zip -Force -ErrorAction SilentlyContinue
  $exe = Find-LocalGodot
  if (-not $exe) { Fail "Godot estratto in $dest ma l'eseguibile non e' stato trovato." }
  if (-not $script:OnWin) { & chmod +x $exe }
}

function Ensure-Tools($modoDev, $serveGit) {
  Write-Step "Strumenti"
  if (-not (Test-Dotnet8)) {
    if (-not (Ask-YesNo "Installare .NET 8 SDK?")) { Fail ".NET 8 SDK mancante: serve per compilare. Installalo (https://dotnet.microsoft.com/download) e rilancia." }
    Install-WithWinget ".NET 8 SDK" "Microsoft.DotNet.SDK.8" "https://dotnet.microsoft.com/download"
    if (-not (Test-Dotnet8)) { Fail ".NET 8 SDK installato ma non ancora visibile: chiudi e riapri il terminale e rilancia." }
  }
  if ($modoDev -and -not (Test-Python3)) {
    if (-not (Ask-YesNo "Installare Python 3?")) { Fail "Python 3 mancante: in modalita' dev serve per gli strumenti (arte, sito). Installalo (https://www.python.org/downloads/) e rilancia." }
    Install-WithWinget "Python 3" "Python.Python.3.12" "https://www.python.org/downloads/"
    if (-not (Test-Python3)) { Fail "Python installato ma non ancora visibile: chiudi e riapri il terminale e rilancia." }
  }
  if ($serveGit -and -not (Get-Command git -ErrorAction SilentlyContinue)) {
    if (Ask-YesNo "Installare Git?") {
      try { Install-WithWinget "Git" "Git.Git" "https://git-scm.com/downloads" } catch { Write-Host $_.Exception.Message -ForegroundColor DarkYellow }
    }
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
      Write-Host "Git non c'e': salto il ripristino dei file .import dopo l'importazione." -ForegroundColor DarkYellow
    }
  }
  $g = Resolve-Godot
  if (-not $g) {
    if (-not (Ask-YesNo "Installare Godot 4.4.1 .NET (download in .dev/godot/)?")) {
      Fail "Godot 4.4 .NET (mono) mancante. Installalo e mettilo nel PATH come godot4, oppure imposta GODOT o TINDUSTRY_GODOT sul suo eseguibile."
    }
    Install-Godot
    $g = Resolve-Godot
    if (-not $g) { Fail "Godot scaricato ma non utilizzabile." }
  }
  Write-Host "dotnet $(& dotnet --version) - Godot $($g.Version)"
  return $g.Exe
}

function Restore-ImportFiles {
  if (-not (Get-Command git -ErrorAction SilentlyContinue)) { return }
  # Godot rewrites line endings of tracked .import files: drop changes that are only that.
  $mod = @(& git ls-files -m godot/assets 2>$null)
  foreach ($f in $mod) {
    & git diff --ignore-cr-at-eol --quiet -- $f 2>$null
    if ($LASTEXITCODE -eq 0) { & git checkout -q -- $f }
  }
}

function Invoke-Import($godot) {
  Write-Step "Importazione asset Godot"
  # The importer runs the editor, which builds user:// from %APPDATA%: give it a throwaway one so the
  # player's real app_userdata (.recovery_mode_lock, caches) is never touched.
  $tmp = Join-Path ([IO.Path]::GetTempPath()) ("tindustry-import-" + [guid]::NewGuid().ToString("N"))
  New-Item -ItemType Directory -Force -Path $tmp | Out-Null
  $oldAppData = $env:APPDATA
  try {
    $env:APPDATA = $tmp
    & $godot --headless --path godot --import *> $null
    $code = $LASTEXITCODE
  } finally {
    $env:APPDATA = $oldAppData
    Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
  }
  # The headless import sometimes crashes at exit (139) after finishing: the cache is what counts.
  if ($code -ne 0 -and -not (Test-Path (Join-Path $script:Root "godot/.godot/imported"))) {
    Fail "Importazione fallita (riprova con: $godot --headless --path godot --import)."
  }
  Restore-ImportFiles
}

function Invoke-Playtest($godot, $nome) {
  $out = Join-Path $script:Root ".dev/playtest-$nome"
  Write-Step "Playtest $nome (screenshot in $out)"
  New-Item -ItemType Directory -Force -Path $out | Out-Null
  Get-ChildItem $out -Filter *.png -ErrorAction SilentlyContinue | Remove-Item -Force
  foreach ($f in "log.txt", "log.err.txt", "godot.log") { Remove-Item (Join-Path $out $f) -Force -ErrorAction SilentlyContinue }
  $data = Join-Path ([IO.Path]::GetTempPath()) ("tindustry-playtest-" + [guid]::NewGuid().ToString("N"))
  New-Item -ItemType Directory -Force -Path $data | Out-Null
  $limit = 150
  if ($env:TINDUSTRY_PLAYTEST_EFFETTI -eq "1") { $limit = 300 }
  $saved = @{}
  foreach ($k in "APPDATA", "TINDUSTRY_DATA_DIR", "TINDUSTRY_CAPTURE_DIR", "TINDUSTRY_PLAYTEST", "TINDUSTRY_DEV", "TINDUSTRY_FRESH", "TINDUSTRY_AGENT_PORT", "TINDUSTRY_AGENT_TOKEN") {
    $saved[$k] = [Environment]::GetEnvironmentVariable($k, "Process")
  }
  try {
    if ($nome -eq "agenti") {
      # Free port bound at 0 by the OS, plus a fresh secret for the handshake.
      $l = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
      $l.Start(); $port = $l.LocalEndpoint.Port; $l.Stop()
      $bytes = New-Object byte[] 24
      $rng = [Security.Cryptography.RandomNumberGenerator]::Create(); $rng.GetBytes($bytes); $rng.Dispose()
      $env:TINDUSTRY_AGENT_PORT = "$port"
      $env:TINDUSTRY_AGENT_TOKEN = (($bytes | ForEach-Object { $_.ToString("x2") }) -join "")
    }
    # user:// (shader_cache, vulkan, .recovery_mode_lock) goes under %APPDATA% on Windows: point it at the temp profile.
    $env:APPDATA = $data
    $env:TINDUSTRY_DATA_DIR = $data
    $env:TINDUSTRY_CAPTURE_DIR = $out
    $env:TINDUSTRY_PLAYTEST = $nome
    Remove-Item Env:TINDUSTRY_DEV -ErrorAction SilentlyContinue   # dev controls never in playtests
    Remove-Item Env:TINDUSTRY_FRESH -ErrorAction SilentlyContinue
    $log = Join-Path $out "log.txt"
    $logErr = Join-Path $out "log.err.txt"
    $argStr = '--path godot --resolution 1280x720 --log-file "' + (Join-Path $data "godot.log") + '"'
    $p = Start-Process -FilePath $godot -ArgumentList $argStr -WorkingDirectory $script:Root -NoNewWindow -PassThru `
      -RedirectStandardOutput $log -RedirectStandardError $logErr
    if (-not $p.WaitForExit($limit * 1000)) {
      Write-Host "Tempo scaduto ($limit s): termino Godot." -ForegroundColor DarkYellow
      try { $p.Kill() } catch { }
      $p.WaitForExit()
    }
  } finally {
    foreach ($k in $saved.Keys) { [Environment]::SetEnvironmentVariable($k, $saved[$k], "Process") }
  }
  # Godot's own exit code is ignored: a rare shutdown crash can happen after PLAYTEST OK.
  $gl = Join-Path $data "godot.log"
  if (Test-Path $gl) { Copy-Item $gl (Join-Path $out "godot.log") -Force }
  Remove-Item -Recurse -Force $data -ErrorAction SilentlyContinue
  $parts = @()
  foreach ($f in "log.txt", "log.err.txt") { $fp = Join-Path $out $f; if (Test-Path $fp) { $parts += Get-Content $fp } }
  Remove-Item (Join-Path $out "log.err.txt") -Force -ErrorAction SilentlyContinue
  Set-Content -Path (Join-Path $out "log.txt") -Value $parts
  if (-not ($parts | Select-String -SimpleMatch "PLAYTEST OK" -Quiet)) { Fail "playtest ${nome}: nessun 'PLAYTEST OK' in $out\log.txt" }
  $errs = @($parts | Where-Object { $_ -cmatch "ERROR" })
  if ($errs.Count -gt 0) { Fail ("playtest ${nome}: righe ERROR in log.txt`n" + (($errs | Select-Object -First 5) -join "`n")) }
  $all = @($parts)
  if (Test-Path (Join-Path $out "godot.log")) { $all += Get-Content (Join-Path $out "godot.log") }
  if ($all | Where-Object { $_ -match "Leaked unsafe reference|rc_owner" }) { Fail "playtest ${nome}: chiusura non pulita (Leaked unsafe reference / rc_owner)" }
  Write-Host "playtest ${nome}: OK" -ForegroundColor Green
}

function Main {
  if ($Help) { Show-Help; return }

  if ($Utente -and $Dev) { Fail "Usa -Utente oppure -Dev, non entrambi." }
  $modoDev = $false
  if ($Dev) { $modoDev = $true }
  elseif (-not $Utente) {
    if ([Console]::IsInputRedirected) { Fail "Terminale non interattivo: indica -Utente oppure -Dev." }
    while ($true) {
      $a = (Read-Host "Entrare come utente o come dev? [u/d]").Trim().ToLowerInvariant()
      if ($a -eq "u") { break }
      if ($a -eq "d") { $modoDev = $true; break }
    }
  }

  $soloDev = @()
  if ($Profilo) { $soloDev += "-Profilo" }
  if ($Editor) { $soloDev += "-Editor" }
  if ($Test) { $soloDev += "-Test" }
  if ($Playtest) { $soloDev += "-Playtest" }
  if ($SoloBuild) { $soloDev += "-SoloBuild" }
  if ($Import) { $soloDev += "-Import" }
  if (-not $modoDev -and $soloDev.Count -gt 0) {
    Fail ("Opzioni solo per la modalita' dev: " + ($soloDev -join ", ") + ". Usa -Dev (o rimuovile).")
  }
  if ($Playtest -and $Playtest -notin @("ui", "tutorial", "agenti")) { Fail "-Playtest accetta solo ui, tutorial o agenti (non '$Playtest')." }
  if ($ProfiloTemp -and $Profilo) { Fail "Usa -ProfiloTemp oppure -Profilo DIR, non entrambi." }

  Set-Location $script:Root
  $needImport = $Import -or -not (Test-Path (Join-Path $script:Root "godot/.godot/imported"))
  $godot = Ensure-Tools $modoDev $needImport

  Write-Step "Compilazione (Shared, test, client Godot)"
  & dotnet build tIndustry.sln -nologo -v q "-clp:ErrorsOnly;NoSummary"
  if ($LASTEXITCODE -ne 0) { Fail "Compilazione fallita." }

  if ($Test) {
    Write-Step "Test"
    & dotnet test tests/TIndustry.Shared.Tests --no-build -nologo -v q
    if ($LASTEXITCODE -ne 0) { Fail "Test falliti." }
  }

  if ($needImport) { Invoke-Import $godot }

  if ($SoloBuild) { Write-Step "Pronto (solo build)"; return }

  if ($Playtest) { Invoke-Playtest $godot $Playtest; return }

  $tempDir = $null
  try {
    $profiloDir = ""
    if ($ProfiloTemp) {
      $tempDir = Join-Path ([IO.Path]::GetTempPath()) ("tindustry-" + [guid]::NewGuid().ToString("N"))
      $profiloDir = $tempDir
    } elseif ($Profilo) {
      $profiloDir = $Profilo
    }
    $logArgs = @()
    if ($profiloDir) {
      New-Item -ItemType Directory -Force -Path $profiloDir | Out-Null
      $profiloDir = (Resolve-Path $profiloDir).Path
      $env:TINDUSTRY_DATA_DIR = $profiloDir
      Write-Host "Profilo dati: $profiloDir"
      # With a custom profile the engine log goes there too, never into the player's real folder.
      $logArgs = @("--log-file", (Join-Path $profiloDir "godot.log"))
    } else {
      Write-Host "Profilo dati: quello reale del giocatore (usa -ProfiloTemp per non toccarlo)"
    }
    if ($Nuova) { $env:TINDUSTRY_FRESH = "1" }
    if ($modoDev) { $env:TINDUSTRY_DEV = "1" }

    if ($Editor) {
      Write-Step "Editor Godot"
      & $godot --path godot @logArgs -e
    } else {
      Write-Step "Avvio del gioco"
      # With a custom profile, user:// goes there too (Godot builds it from %APPDATA% on Windows).
      # Not for the editor, which keeps its own settings under the real %APPDATA%.
      $oldAppData = $env:APPDATA
      try {
        if ($profiloDir) { $env:APPDATA = $profiloDir }
        & $godot --path godot @logArgs
      } finally { $env:APPDATA = $oldAppData }
    }
  } finally {
    if ($tempDir) { Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue }
  }
}

try {
  Main
} catch [System.InvalidOperationException] {
  Write-Host $_.Exception.Message -ForegroundColor Red
  exit 1
}
