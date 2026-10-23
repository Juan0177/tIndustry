# tIndustry — istruzioni per gli agenti

Gioco di logistica industriale come Mindustry, con un mercato che si muove come una borsa («Dal giacimento al mercato»), in Godot 4.4 .NET. Simulazione in C# (`src/TIndustry.Shared`), client in `godot/`.

## Regole fisse

- **Con l'utente si parla in italiano.** Testi di gioco, documenti e messaggi di commit/PR in italiano; commenti nel codice in inglese.
- **Ogni lavoro sul progetto aggiorna in main `docs/STATO.md` e la mappa del progetto** (`python tools/dev/mappa_canvas.py` rigenera `docs/mappa-progetto.canvas`). Vale per ogni PR, issue o cambio della lista delle cose da fare, nella stessa PR. Poi `python tools/sito/stato.py` riscrive la barra di avanzamento nel README e nella Home della Wiki; il sito la ridisegna nella pagina Sviluppo. La CI controlla che le barre corrispondano a STATO.md.
- **GitHub Actions solo manuali o su tag di release.** Mai aggiungere trigger `push`/`pull_request`. Dopo un push: `gh workflow run build-publish.yml --ref <branch>` poi `gh run watch <id> --exit-status`.
- **Mai toccare i dati reali del giocatore**: `%LOCALAPPDATA%\tIndustry` e `%APPDATA%\Godot\app_userdata\tIndustry`. Prove, playtest e catture sempre con `TINDUSTRY_DATA_DIR` su una cartella temporanea.
- `gh` locale: `"/c/Program Files/GitHub CLI/gh.exe"`. Godot: 4.4.1 mono, comando `godot4`.
- Commit con la riga `Co-Authored-By:` del modello; PR con `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.

## Comandi

Avvio all-in-one: su Windows `.\play.ps1 -Utente` o `.\play.ps1 -Dev [-ProfiloTemp | -Profilo DIR] [-Nuova] [-Editor] [-Test] [-Playtest ui|tutorial|agenti] [-SoloBuild] [-Import]` (senza `-Utente`/`-Dev` chiede «Entrare come utente o come dev? [u/d]»; controlla e, se mancano, installa .NET 8, Git, Python 3 e Godot 4.4.1 .NET; `-Dev` imposta `TINDUSTRY_DEV=1` e abilita le opzioni di sviluppo; `-Si` accetta le installazioni); su Linux e per gli agenti cloud `tools/dev/dev.sh [--profilo-temp | --profilo DIR] [--nuova] [--editor] [--test] [--playtest ui|tutorial|agenti] [--solo-build] [--import]`. `--profilo DIR` (`-Profilo DIR`) usa un profilo dati persistente in `DIR`, `--import` forza la reimportazione degli asset. Per prove usa sempre `--profilo-temp`.

```bash
dotnet build tIndustry.sln -c Release -nologo -v q       # atteso: 0 errori, 0 avvisi
dotnet test tests/TIndustry.Shared.Tests -c Release --no-build
python -m unittest tools/art/test_genera.py                # test del generatore degli sprite
python tools/sito/costruisci.py [--out DIR]                # sito (GitHub Pages) in _site/; si pubblica col workflow Pages, a mano
python -m unittest discover -s tools/sito -p "test_*.py"   # test del sito (fabbrica, documenti, pagine); li esegue anche il workflow Pages
node --test tools/sito/test_sito.mjs tools/sito/test_racconto.mjs   # test degli script del sito
APPDATA="$(mktemp -d)" godot4 --headless --path godot --import   # poi ripristina i .import (sotto)
APPDATA="$(mktemp -d)" godot4 --headless --path godot --build-solutions --quit
godot4 --path godot                                      # avvia il gioco (legge data/)
tools/dev/playtest.sh ui /tmp/shots-ui                   # idem: tutorial, agenti; PLAYTEST OK, 0 ERROR
python tools/dev/compare_shots.py <baseline> <nuovi>     # confronto screenshot
dotnet run --project tools/MapTool -- valida             # mappe della campagna; anche: genera <id> …, anteprima
dotnet test tests/TIndustry.AgentPlay.Tests -c Release --no-build   # server MCP degli agenti
```

`TINDUSTRY_PLAYTEST=ui|tutorial|agenti` richiede `TINDUSTRY_DATA_DIR` (lo script lo imposta da solo). I playtest sono sempre in finestra 1280×720 con la Scala UI al 100%; `TINDUSTRY_PLAYTEST_LINGUA=en` li gioca in inglese.
Con un profilo temporaneo/esplicito gli script passano `--log-file <profilo>/godot.log` e, per playtest e partite (non per l'editor), puntano `APPDATA` al profilo: su Windows Godot costruisce `user://` da `%APPDATA%`, quindi log, `shader_cache`, `vulkan` e `.recovery_mode_lock` restano nel profilo e `%APPDATA%\Godot\app_userdata\tIndustry` non viene toccato. Godot 4.4 non ha un'opzione CLI per `user://`; `use_custom_user_dir` cambia solo il nome sotto `%APPDATA%`. L'editor (`--editor`) usa ancora il `%APPDATA%` reale; per `--import` e `--build-solutions` da riga di comando metti `APPDATA` su una cartella temporanea (come sopra; `dev.sh` lo fa da solo per l'importazione).

Dopo `godot4 --import`, ripristina i `.import` che differiscono solo per fine riga:

```bash
for f in $(git ls-files -m godot/assets); do git diff --ignore-cr-at-eol --quiet -- "$f" && git checkout -q -- "$f"; done
```

`.gitattributes` mantiene gli `*.sh` in LF: non convertirli.

## Agenti che giocano

Il server MCP `tindustry` (`.mcp.json`, codice in `tools/AgentPlay`) fa giocare gli agenti: simulazione senza grafica (`apri_simulazione`) o gioco vero in una finestra (`apri_gioco`), comandi, input, catture, `diario` e `rigioca`. Guida: `tools/AgentPlay/README.md`; ricette per cercare bug e fare catture: skill `gioca-e-testa` (`.claude/skills/gioca-e-testa/SKILL.md`); decisione: `docs/decisioni/0008-agenti-che-giocano.md`.

- Il gioco vero gira sempre in un profilo temporaneo creato dal server: **non aggirarlo mai** (niente `TINDUSTRY_AGENT_PORT` a mano su un profilo vero, niente salvataggi presi da `%LOCALAPPDATA%\tIndustry`: copiali prima in una cartella temporanea).
- Serve il progetto importato e compilato (`--import`, `--build-solutions`, sopra). Se Claude Code non trova `godot4` nel PATH di Windows, imposta la variabile d'ambiente utente `TINDUSTRY_GODOT` sull'eseguibile di Godot: `.mcp.json` la passa al server. Non scrivere percorsi della tua macchina in `.mcp.json`.

## Convenzioni

- Namespace: `TIndustry.Shared`, `TIndustry.Godot`, `TIndustry.Shared.Tests` (nessun sotto-namespace per cartella).
- `src/TIndustry.Shared` non dipende da Godot né dal sistema operativo (vedi `docs/decisioni/0001-server-csharp.md`).
- Dati di gioco solo in `data/` (`content.json`, `campaign.json`, `maps/`); `godot/data/` è generata dall'export ed è ignorata da git. Le mappe della campagna (`data/maps/<id>.png`, `<id>.giacimenti.png`, `<id>.json`) si creano e si controllano con `tools/MapTool` (`genera`, `valida`, `anteprima`), mai a occhio: dopo ogni ritocco `valida` deve dire OK. Terreno, generatori e regole: `docs/decisioni/0005-mappe-seme-e-file.md`.
- Il logo (`docs/img/logo.png`, il logo generato ×8) si usa solo dove il titolo è soltanto «tIndustry»: il banner del README, la barra in alto e la vetrina del sito. Mai dentro una frase: in «Wiki di tIndustry» o «Architettura di tIndustry» resta la parola. Il logo e il banner (`docs/img/readme/banner.png`) si rigenerano con `python tools/art/immagini_documenti.py` e la CI li controlla.
- Sprite di edifici, nastri, Core, drone e oggetti solo da `python tools/art/genera.py`; non modificare i PNG a mano (si cambia lo script e si rigenera; `--controlla` verifica, vedi `tools/art/README.md` e `docs/decisioni/0004-arte-generata-da-script.md`). Tutto in `godot/assets/` (piatto); icone e carattere dell'interfaccia, generati anch'essi, in `godot/assets/ui/`. Un asset libero di terzi va citato in `godot/assets/ATTRIBUTION.md`.
- Spostare uno script Godot = `git mv` insieme al suo `.uid`.
- **Testi visibili solo tramite chiavi di `godot/lingue/testi.csv`** (colonne `keys,it,en`, italiano e inglese sempre entrambi): nel client nessuna frase scritta nel codice; la simulazione dà codici (`RefusalCode`, `SaveLoadError`) che il client traduce. `TestiTests` lo controlla; come aggiungere un testo o una lingua in `docs/ARCHITETTURA.md`, decisione in `docs/decisioni/0006-interfaccia-lingue-carattere.md`.
- **Stili solo dal tema** (`UiTheme`, colori da `UiColors`): niente `StyleBoxFlat` né `AddTheme*Override` nei pannelli; un aspetto nuovo è una variante (`ThemeTypeVariation`) in `UiTheme`, con i colori nelle tre varianti (Normale, Daltonici, AltoContrasto).
- Salvataggi alla versione 16 (`FactorySaveData.CurrentVersion`); le versioni precedenti non si aprono più (il gioco non è ancora uscito); cambiare il formato = alzare la versione.
- Costruzione come Mindustry (piani, drone, cantieri: `docs/decisioni/0003-costruzione-come-mindustry.md`). I test che non provano la costruzione usano `GameRules(InstantBuild: true)` (per esempio `NewGame.Sandbox(..., rules: new GameRules(InstantBuild: true))`), così i piazzamenti tramite `SimHost` sono istantanei; i `Factory.TryPlace…` piazzano sempre subito.
- Terreno nei test: `NewGame.Sandbox(...)` e `NewGame.Campaign(...)` usano per default il generatore **classico** (congelato, coordinate fisse); il generatore a biomi si chiede con `map: MapGenSettings.Normal(misura, seme)`, la mappa di un livello con `mapsDirectory:`. Un generatore pubblicato (`classico`, `biomi-1`) non si modifica mai: una generazione diversa è un generatore nuovo con un nome nuovo (i salvataggi rigenerano il terreno per nome e ne controllano l'impronta).
- **Diario di lavoro**: a fine di ogni piano di implementazione copia il registro (`.superpowers/sdd/<piano>/progress.md`) in `docs/diario/AAAA-MM-GG-<nome>.md` (formato in `docs/diario/README.md`) *prima* di cancellare `.superpowers/sdd/`.
- La simulazione è a tick fisso (60 Hz), guidata da comandi (`SimHost`) e deterministica: in `Step()` niente tempo, niente ordine di `Dictionary`/`HashSet`, niente orologio né `Random` non seminato, niente parallelismo né formati dipendenti dalla cultura. Regole in `docs/ARCHITETTURA.md`, modello di sincronizzazione in `docs/decisioni/0002-sincronizzazione-ibrida.md`.

## Dove guardare

- `docs/STATO.md` — a che punto siamo e cosa manca: i blocchi di lavoro con le loro caselle, fonte della barra di avanzamento (regola in «Regole fisse»).
- `docs/ARCHITETTURA.md` — mappa delle cartelle, ciclo di frame, dati, salvataggi, interfaccia, come aggiungere un blocco, un testo o una lingua.
- `docs/decisioni/` — decisioni architetturali (ADR).
- `docs/superpowers/` — specifiche e piani di lavoro.
- `docs/diario/` — diario di lavoro per piano completato (task, PR, decisioni, rinvii).
- `godot/README.md` — controlli e funzioni del gioco; `CHANGELOG.md` — storia.
