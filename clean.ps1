<#
.SYNOPSIS
  Pulisce i file generati di tIndustry che possono causare problemi di compilazione o importazione vecchie.
.DESCRIPTION
  Elenca quello che cancellerebbe (con le dimensioni), chiede conferma e cancella.
  Non tocca i file nel repository, ne' i dati del giocatore (%LOCALAPPDATA%\tIndustry, %APPDATA%\Godot\app_userdata\tIndustry).
  Il prossimo play.ps1 ricompila e reimporta tutto.
#>
[CmdletBinding()]
param(
  [switch]$Prova,
  [switch]$Si,
  [switch]$Tutto,
  [switch]$Help
)

$script:Root = $PSScriptRoot

function Show-Help {
  @"
Uso: .\clean.ps1 [-Prova] [-Si] [-Tutto]

Cancella i file generati che possono lasciare compilazioni o importazioni vecchie:
  godot\.godot\ (cache di importazione, mono, editor), bin\ e obj\ di ogni progetto,
  godot\lingue\*.translation, _site\, TestResults\, __pycache__\, .dev\playtest-*\

  -Prova   elenca soltanto, non cancella
  -Si      cancella senza chiedere «Cancellare? (y/n)» (senza terminale interattivo la risposta e' «no»)
  -Tutto   cancella anche .dev\godot\ (il Godot scaricato da play.ps1)

Non tocca i file del repository ne' i dati del giocatore.
"@ | Write-Host
}

function Fail($m) { throw [System.InvalidOperationException]::new($m) }

function Get-Size($path) {
  $sum = 0L
  if ([IO.File]::Exists($path)) { return ([IO.FileInfo]$path).Length }
  foreach ($f in [IO.Directory]::EnumerateFiles($path, "*", [IO.SearchOption]::AllDirectories)) {
    try { $sum += ([IO.FileInfo]$f).Length } catch { }
  }
  return $sum
}

function Format-Size($b) {
  if ($b -ge 1GB) { return ("{0:N1} GB" -f ($b / 1GB)) }
  if ($b -ge 1MB) { return ("{0:N1} MB" -f ($b / 1MB)) }
  return ("{0:N0} KB" -f ($b / 1KB))
}

function Main {
  if ($Help) { Show-Help; return }
  $root = $script:Root
  if (-not (Test-Path (Join-Path $root "tIndustry.sln")) -or -not (Test-Path (Join-Path $root "godot/project.godot"))) {
    Fail "Questa non sembra la cartella di tIndustry (manca tIndustry.sln accanto a clean.ps1): non cancello nulla."
  }
  $rootFull = [IO.Path]::GetFullPath($root).TrimEnd('\', '/')
  $tracked = @()
  if (Get-Command git -ErrorAction SilentlyContinue) {
    Push-Location $root
    try { $tracked = @(& git ls-files 2>$null) } finally { Pop-Location }
  }

  $cand = New-Object System.Collections.Generic.List[string]
  function Add-IfExists($p) { if (Test-Path -LiteralPath $p) { $cand.Add([IO.Path]::GetFullPath($p)) } }

  Add-IfExists (Join-Path $root "godot/.godot")
  Add-IfExists (Join-Path $root "_site")
  Add-IfExists (Join-Path $root "TestResults")
  $lingue = Join-Path $root "godot/lingue"
  if (Test-Path $lingue) { foreach ($f in [IO.Directory]::GetFiles($lingue, "*.translation")) { $cand.Add($f) } }
  $dev = Join-Path $root ".dev"
  if (Test-Path $dev) {
    foreach ($d in [IO.Directory]::GetDirectories($dev, "playtest-*")) { $cand.Add($d) }
    if ($Tutto) { Add-IfExists (Join-Path $dev "godot") }
  }
  # bin/obj only next to a project file; __pycache__ anywhere; never inside .git, .dev, .godot or node_modules.
  $skip = @(".git", ".dev", ".godot", "node_modules")
  $stack = New-Object System.Collections.Generic.Stack[string]
  $stack.Push($rootFull)
  while ($stack.Count -gt 0) {
    $dir = $stack.Pop()
    foreach ($d in [IO.Directory]::GetDirectories($dir)) {
      $n = [IO.Path]::GetFileName($d)
      if ($skip -contains $n) { continue }
      if ($n -eq "__pycache__") { $cand.Add($d); continue }
      if ($n -eq "bin" -or $n -eq "obj") {
        if ([IO.Directory]::GetFiles($dir, "*.csproj").Count -gt 0) { $cand.Add($d); continue }
      }
      $stack.Push($d)
    }
  }

  $list = @()
  foreach ($p in ($cand | Sort-Object -Unique)) {
    if (-not $p.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar)) { continue }
    $rel = $p.Substring($rootFull.Length + 1).Replace('\', '/')
    $isTracked = $false
    foreach ($t in $tracked) { if ($t -eq $rel -or $t.StartsWith($rel + "/")) { $isTracked = $true; break } }
    if ($isTracked) { continue }
    $list += [pscustomobject]@{ Path = $p; Rel = $rel; Size = (Get-Size $p) }
  }
  if ($list.Count -eq 0) { Write-Host "Niente da pulire."; return }

  Write-Host "Da cancellare:" -ForegroundColor Yellow
  $tot = 0L
  foreach ($e in $list) { Write-Host ("  {0,10}  {1}" -f (Format-Size $e.Size), $e.Rel); $tot += $e.Size }
  Write-Host ("Totale: {0}" -f (Format-Size $tot))
  if ($Prova) { Write-Host "Solo prova (-Prova): non cancello nulla."; return }

  if ($Si) { Write-Host "Cancellare? (y/n) y  [-Si]" }
  elseif ([Console]::IsInputRedirected) { Write-Host "Cancellare? (y/n) n  [terminale non interattivo]"; return }
  else {
    while ($true) {
      $a = (Read-Host "Cancellare? (y/n)").Trim().ToLowerInvariant()
      if ($a -in @("y", "yes", "s", "si", "sì")) { break }
      if ($a -in @("n", "no")) { Write-Host "Annullato."; return }
    }
  }
  foreach ($e in $list) {
    try { Remove-Item -LiteralPath $e.Path -Recurse -Force -ErrorAction Stop }
    catch { Write-Host "Non riesco a cancellare $($e.Rel): $($_.Exception.Message)" -ForegroundColor DarkYellow }
  }
  Write-Host ""
  Write-Host "Fatto. Il prossimo play.ps1 ricompila e reimporta tutto (la prima volta ci mette un po')." -ForegroundColor Green
}

try { Main } catch { Write-Host $_.Exception.Message -ForegroundColor Red; exit 1 }
