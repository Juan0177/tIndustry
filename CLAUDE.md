# tIndustry — istruzioni per gli agenti

Gioco di logistica industriale (Mindustry × Tiny Industry) in Godot 4.4 .NET. Simulazione in C# (`src/TIndustry.Shared`), client in `godot/`.

## Regole fisse

- **Con l'utente si parla in italiano.** Testi di gioco, documenti e messaggi di commit/PR in italiano; commenti nel codice in inglese.
- **GitHub Actions solo manuali o su tag di release.** Mai aggiungere trigger `push`/`pull_request`. Dopo un push: `gh workflow run build-publish.yml --ref <branch>` poi `gh run watch <id> --exit-status`.
- **Mai toccare i dati reali del giocatore**: `%LOCALAPPDATA%\tIndustry` e `%APPDATA%\Godot\app_userdata\tIndustry`. Prove, playtest e catture sempre con `TINDUSTRY_DATA_DIR` su una cartella temporanea.
- `gh` locale: `"/c/Program Files/GitHub CLI/gh.exe"`. Godot: 4.4.1 mono, comando `godot4`.
- Commit con la riga `Co-Authored-By:` del modello; PR con `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.

## Comandi

Avvio all-in-one: `tools/dev/dev.sh [--profilo-temp] [--nuova] [--dev] [--editor] [--test] [--playtest ui|tutorial] [--solo-build]` (Windows: `dev.cmd`, stesse opzioni). Per prove usa sempre `--profilo-temp`.

```bash
dotnet build tIndustry.sln -c Release -nologo -v q       # atteso: 0 errori, 0 avvisi
dotnet test tests/TIndustry.Shared.Tests -c Release --no-build
godot4 --headless --path godot --import                  # poi ripristina i .import (sotto)
godot4 --headless --path godot --build-solutions --quit
godot4 --path godot                                      # avvia il gioco (legge data/)
tools/dev/playtest.sh ui /tmp/shots-ui                   # idem: tutorial; PLAYTEST OK, 0 ERROR
python tools/dev/compare_shots.py <baseline> <nuovi>     # confronto screenshot
```

`TINDUSTRY_PLAYTEST=ui|tutorial` richiede `TINDUSTRY_DATA_DIR` (lo script lo imposta da solo).
`TINDUSTRY_DEV=1` (solo build di debug) attiva console F1, velocità F3/F4 e trucchi; `TINDUSTRY_DEV_AVVIO` esegue comandi a ogni partita (vedi `godot/README.md`).

Dopo `godot4 --import`, ripristina i `.import` che differiscono solo per fine riga:

```bash
for f in $(git ls-files -m godot/assets); do git diff --ignore-cr-at-eol --quiet -- "$f" && git checkout -q -- "$f"; done
```

`.gitattributes` mantiene gli `*.sh` in LF: non convertirli.

## Convenzioni

- Namespace: `TIndustry.Shared`, `TIndustry.Godot`, `TIndustry.Shared.Tests` (nessun sotto-namespace per cartella).
- `src/TIndustry.Shared` non dipende da Godot né dal sistema operativo (vedi `docs/decisioni/0001-server-csharp.md`).
- Dati di gioco solo in `data/` (`content.json`, `campaign.json`); `godot/data/` è generata dall'export ed è ignorata da git.
- Icone solo in `godot/assets/` (piatto), con attribuzione in `godot/assets/ATTRIBUTION.md`.
- Spostare uno script Godot = `git mv` insieme al suo `.uid`.
- Salvataggi alla versione 9 (`FactorySaveData.CurrentVersion`); cambiare il formato = alzare la versione.
- La suddivisione di `Factory` in sistemi non è ancora fatta: è il sotto-progetto 2.

## Dove guardare

- `docs/ARCHITETTURA.md` — mappa delle cartelle, ciclo di frame, dati, salvataggi, come aggiungere un blocco.
- `docs/decisioni/` — decisioni architetturali (ADR).
- `docs/superpowers/` — specifiche e piani di lavoro.
- `godot/README.md` — controlli e funzioni del gioco; `CHANGELOG.md` — storia.
