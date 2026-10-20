# Compilare e avviare

Cosa installare, come compilare, provare e avviare tIndustry dai sorgenti senza toccare i dati di chi ci gioca.

## Requisiti

| Strumento | Perché |
|---|---|
| [.NET 8 SDK](https://dotnet.microsoft.com/download) | Simulazione, client C#, test, `MapTool`, server degli agenti |
| [Godot 4.4.1 .NET (mono)](https://godotengine.org/download), raggiungibile come comando `godot4` | Client di gioco, importazione degli asset, playtest |
| Python 3 con [Pillow](https://pypi.org/project/pillow/) | Generatore degli sprite (`tools/art`), confronto screenshot, sito, lanciatore del server degli agenti |
| Node.js | Solo per i test degli script del sito (`node --test`) |
| Git for Windows (Git Bash) | Solo su Windows, per `dev.cmd` |

Gli sprite del repository sono scritti con **Pillow 12.3** (la versione installata dalla CI): con un'altra versione il controllo byte per byte degli sprite può segnalare differenze a pixel uguali (vedi [Arte generata](Arte-generata)).

## Il modo più semplice: dev.sh

Un solo comando controlla gli strumenti, compila, importa gli asset se serve e avvia il gioco:

```bash
tools/dev/dev.sh --profilo-temp      # Linux/macOS
dev.cmd --profilo-temp               # Windows, dalla radice del repository (usa Git Bash)
```

| Opzione | Effetto |
|---|---|
| `--profilo-temp` | profilo dati temporaneo, cancellato all'uscita: impostazioni e salvataggi reali restano intatti (**da usare per ogni prova**) |
| `--profilo DIR` | usa `DIR` come profilo dati, che resta fra un avvio e l'altro |
| `--nuova` | salta la Home e parte da una nuova partita (`TINDUSTRY_FRESH=1`) |
| `--editor` | apre l'editor Godot invece del gioco |
| `--test` | esegue anche i test xUnit prima di avviare |
| `--playtest NOME` | esegue il playtest `ui`, `tutorial` o `agenti` (screenshot in `.dev/playtest-NOME`) |
| `--solo-build` | compila (e importa se serve) senza avviare |
| `--import` | forza la reimportazione degli asset Godot |
| `-h`, `--help` | mostra l'aiuto |

La variabile `GODOT` indica un comando di Godot diverso da `godot4`.

## Passo per passo

```bash
dotnet build tIndustry.sln -c Release -nologo -v q        # atteso: 0 errori, 0 avvisi
dotnet test tests/TIndustry.Shared.Tests -c Release --no-build
dotnet test tests/TIndustry.AgentPlay.Tests -c Release --no-build
python -m unittest tools/art/test_genera.py
godot4 --path godot                                        # avvia il gioco, legge i contenuti da data/
```

In sviluppo il gioco legge i contenuti direttamente da `data/`; la copia in `godot/data/` nasce solo con l'export ed è ignorata da git.

## Importare gli asset di Godot

Prima del primo avvio (e dopo ogni sprite nuovo) Godot deve importare gli asset e compilare la soluzione C#. Da riga di comando conviene dare a Godot una cartella `APPDATA` temporanea, così non scrive nella cartella utente reale:

```bash
APPDATA="$(mktemp -d)" godot4 --headless --path godot --import
APPDATA="$(mktemp -d)" godot4 --headless --path godot --build-solutions --quit
```

L'importazione riscrive le fine riga di alcuni file `.import` già nel repository. Le modifiche che riguardano solo le fine riga vanno annullate:

```bash
for f in $(git ls-files -m godot/assets); do git diff --ignore-cr-at-eol --quiet -- "$f" && git checkout -q -- "$f"; done
```

`dev.sh` fa entrambe le cose da solo quando importa. Gli script `*.sh` restano con fine riga LF (`.gitattributes`): non convertirli.

L'importazione headless a volte esce con il codice 139 (crash alla chiusura) **dopo** aver finito: l'importazione è comunque valida, lo conferma la presenza di `godot/.godot/imported/`.

## Non toccare i dati del giocatore

Prove, playtest e catture non devono mai leggere o scrivere i dati veri:

- `%LOCALAPPDATA%\tIndustry` (Linux `~/.local/share/tIndustry`): salvataggi e progressi della campagna;
- `%APPDATA%\Godot\app_userdata\tIndustry`: impostazioni, log e cache di Godot.

Per questo:

- imposta `TINDUSTRY_DATA_DIR` su una cartella temporanea, oppure usa `--profilo-temp` (gli script dei playtest lo fanno da soli);
- con un profilo temporaneo o esplicito gli script passano a Godot `--log-file <profilo>/godot.log` e, per partite e playtest, puntano `APPDATA` al profilo: su Windows Godot costruisce `user://` da `%APPDATA%`, quindi log, cache degli shader e file di blocco restano nel profilo;
- l'editor (`--editor`) usa ancora l'`%APPDATA%` reale: Godot 4.4 non ha un'opzione da riga di comando per spostare `user://`.

Dove finiscono salvataggi e impostazioni con e senza `TINDUSTRY_DATA_DIR` è spiegato in [ARCHITETTURA.md, Salvataggi e impostazioni](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md#salvataggi-e-impostazioni).

## Export

In locale: `tools/ci/setup-godot.sh` una volta, poi `tools/ci/export-godot.sh`, che produce `dist/tIndustry-win-x64.zip` e `dist/tIndustry-linux-x64.zip`. Le release ufficiali le costruisce il workflow `release.yml` su un tag `v*`.

## Per approfondire

- [tools/dev/dev.sh](https://github.com/Juan0177/tIndustry/blob/main/tools/dev/dev.sh) (`--help` per tutte le opzioni)
- [README.md, sezione Sviluppo](https://github.com/Juan0177/tIndustry/blob/main/README.md#sviluppo)
- [CLAUDE.md](https://github.com/Juan0177/tIndustry/blob/main/CLAUDE.md), sezione Comandi
- [Test e playtest](Test-e-playtest) · [Panoramica](Sviluppo-Panoramica) · [Come contribuire](Come-contribuire)
