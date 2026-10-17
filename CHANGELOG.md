# Changelog

Tutte le note di versione rilevanti per [tIndustry](https://github.com/Juan0177/tIndustry).
Il formato è ispirato a [Keep a Changelog](https://keepachangelog.com/); il versioning segue SemVer dove ha senso per un prototipo.

## [Unreleased]

### Documentazione

- **Diario di lavoro** in `docs/diario/`: un file per piano di implementazione completato (task, PR, revisioni, decisioni autonome, rinvii, verifiche), con il diario del sotto-progetto 2; `.gitattributes` forza LF sui `.md` per non vederli modificati dopo l'apertura in Obsidian

### Sviluppo

- **Dati del giocatore protetti nei playtest**: con `TINDUSTRY_PLAYTEST` ogni scrittura di salvataggi, impostazioni e progressi passa da `GameDataPaths.EnsureWritable` e viene rifiutata se cade fuori da `TINDUSTRY_DATA_DIR`; senza profilo, o con un percorso relativo, il gioco esce subito con errore (codice 3). Prima della prima scrittura automatica di una sessione la vecchia **Continua** viene copiata in `continua.prev.json` (stessa cartella, visibile e caricabile da Gestione salvataggi)
- **`user://` isolato nei playtest e nelle partite con profilo**: `playtest.sh` e `dev.sh` (senza `--editor`) puntano `APPDATA` al profilo, così `shader_cache`, `vulkan`, log e `.recovery_mode_lock` non finiscono più in `%APPDATA%\Godot\app_userdata\tIndustry`
- **Costruzione in corso (sotto-progetto 3, parte 1)**: base della simulazione per i cantieri come Mindustry. Nuove regole di partita `GameRules` (`RefundPercent`, `InstantBuild`), `BuildSpec` (cosa costruire e dove) e cantieri `ConstructSite` con `Construction`: tempo `(20 + Σ quantità × peso) × moltiplicatore` diviso per la velocità del drone, risorse prelevate dal Core man mano, cantiere fermo quando mancano e ripartenza da solo, smontaggio con rimborso graduale (edificio finito: regola di partita × rimborso dell'edificio; cantiere annullato: il 100% di quanto speso). I cantieri occupano le loro celle. Dati: `buildWeights` e `units.drone` in `content.json`, `refundPercent` per livello in `campaign.json` (livelli 1–3 al 100%, dal 4 al 50%). I comandi di piazzamento passano da `Factory.PlaceInstant`; il gioco non cambia ancora: drone e piani arrivano nella parte 2
- Tolto il "riposizionamento gratuito": piazzare un secondo minatore, forno, assemblatore o generatore su un'area occupata dall'unico esistente non lo sposta più gratis

### Tutorial / UI

- **Ricerca a rami per tipologia** (come Mindustry): sotto il CORE un ramo **Logistica** (Nastro T1 → T2 → T3; Incrocio → Sdoppiatore / Selezionatore / Ponte), uno **Produzione** (Minatore → Estrattore, Forno → Assemblatore, Minatore T2) e uno **Energia** (Generatore → Nodo T1 → Nodo T2). Un solo genitore per blocco: Nastro T2, Generatore e Minatore T2 non richiedono più il Forno (restano vincolati dal costo in lastre); costi invariati
- **HUD stile Mindustry**: colonna in alto a sinistra con pulsanti etichettati **Menu · Ricerca · Mercato · Campagna** (al posto di 7 icone senza nome); pannello CORE con **Fondi** (non più "Magazzino $"), energia e **tutte** le risorse possedute come icona + numero; obiettivi sotto; tolti i contatori tecnici "consegnati / nastro"
- **Palette**: categorie a icone al posto di St/Lo/Pr/Po; niente numeri tagliati sugli slot, il tasto rapido è nel pannello info (`[2]`) e nel tooltip
- **Ricerca stile Mindustry**: albero dall'alto con **CORE** come radice e ogni blocco sotto il suo prerequisito (layout ad albero ordinato, linee a gomito, prerequisiti extra tratteggiati); nodi quadrati con il **nome sotto**; bordo dorato = sbloccabile ora, **lucchetto** finché non sblocchi il precedente; tooltip con costo e cosa manca; selezione iniziale sul primo blocco sbloccabile; trascina col sinistro, rotella = zoom, H = vista intera
- **Mercato**: tabella compatta con icona, quantità, prezzo (arancione se sotto listino), valore, **Vendi 1 / Vendi tutto**; pannello alto quanto il contenuto; interruttore ON/OFF ben visibile
- **Nastri**: gli oggetti sono a mezza casella con sagoma piena e contorno scuro (non si vede più la belt attraverso), passano **sotto** il CORE all'ingresso invece di sovrapporsi; frecce della belt più discrete
- **Tutorial non bloccante**: scheda in basso a sinistra, il gioco resta giocabile; ogni passo avanza **da solo** quando lo completi (visuale, palette, minatore, nastro→CORE, Mercato, Ricerca, forno) con "✓ fatto"; testi corretti sui comandi reali
- **Comandi**: trascina col tasto sinistro per muovere la visuale in modalità cursore (prima solo rotella premuta, ma il tutorial diceva "trascina"); tolti gli alias in conflitto **A** (assemblatore, ma A = pan WASD) e **G** (generatore, ma G = Campagna)
- **Impostazioni**: overlay risorse (FPS/CPU/RAM/GPU) **spento di default** per i nuovi giocatori

### Pulizia

- Simulazione: lo stato di gioco passa in `GameState` e la logica per tick in sette sistemi (`Belt`, `Power`, `Mining`, `Crafting`, `Extraction`, `Belt` a passo zero, `CoreDelivery`) eseguiti in ordine fisso da `Simulation.Step`; `Factory` resta la facciata per piazzamento ed economia. Solo spostamento, nessun cambio di comportamento
- Comandi e `SimHost`: ogni modifica al gioco (piazzare, rimuovere, sbloccare, vendere, vendita automatica, potenziare il CORE, filtro del selezionatore) è un comando validato all'applicazione, con esito come evento `CommandApplied` / `CommandRejected` con i testi italiani di prima. Nuovo comando `SetSorterFilter` e lista condivisa `SorterFilters`. Il client Godot tiene solo un `SimHost`, legge lo stato tramite `IGameView` e invia comandi; pannelli Ricerca e Mercato, toast e grafica reagiscono agli eventi. Nessun cambio di gioco: i rifiuti mostrano gli stessi toast (rimuovere su cella vuota resta silenzioso, trascinando non compare "occupata"); il tick parte da 0 a ogni nuova partita, livello o caricamento
- Documentazione della simulazione: `docs/ARCHITETTURA.md` descrive tick fisso, `GameState`, sistemi, comandi, `SimHost`, istantanea e checksum, con le cinque regole di determinismo e il ciclo di frame aggiornato; nuovo ADR `docs/decisioni/0002-sincronizzazione-ibrida.md` (host autoritativo che ordina i comandi, simulazione deterministica, checksum ogni 60 tick, recupero e ingresso a partita in corso via istantanea; il checksum periodico non è ancora implementato, arriva col multiplayer)
- Rimosse le modalità di cattura `TINDUSTRY_CAPTURE` / `TINDUSTRY_CAPTURE_MODE` (ereditate da Cursor); restano `TINDUSTRY_PLAYTEST` e `TINDUSTRY_CAPTURE_DIR`
- Rimossi `StaticMiner`/`StaticAssembler`/`StaticSmelter`, il nodo `Miner` della scena, lo shader `belt_corner.gdshader`, le cartelle `.cursor/` e `assets/` in radice (attribuzioni spostate in `godot/assets/ATTRIBUTION.md`)
- Rinomine con i nomi veri: `SpikeWorld`→`World`, `SpikeCamera`→`WorldCamera`, `Spike.tscn`→`Main.tscn` (nodo radice `Main`), `FactorySlice`→`Factory`, `FactorySliceSaveData/Store`→`FactorySaveData/Store`, `SmelterStub`→`Crafter`, `GeneratorStub`→`Generator`, `ExtractorStub`→`Extractor`, `PowerNodeStub`→`PowerNode`, `MinerProducer`→`Miner`, `StructureDefinition.Requires`→`Prerequisites`; test su percorsi dei dati e riferimenti del progetto Godot. Nessun cambio di comportamento né di formato dei salvataggi
- Cartelle per area: `godot/scripts/` in `world/`, `visuals/{belts,buildings,items}/`, `ui/{hud,panels}/`, `settings/`, `dev/`; `src/TIndustry.Shared/` in `Content/`, `Grid/`, `Buildings/`, `Power/`, `Economy/`, `Research/`, `Campaign/`, `Save/` (resta `Factory.cs` in radice); `SystemMonitor` passa al progetto Godot (`TIndustry.Godot`). Solo spostamenti, nessun cambio di comportamento
- HUD in componenti: `FactoryHud` (1.626 righe) diventa `Hud` che compone `CorePanel` (colonna in alto a sinistra), `BuildPalette` (categorie e griglia), `BlockInfoPanel` (striscia info e dettaglio `?`) e `HudStyle` (colori e widget condivisi). Aspetto e comportamento invariati
- Dati in un solo posto: `godot/data/` esce da git (è una copia di `data/` che l'export rigenera da sé; in sviluppo il gioco legge `data/`). Nuovi test di validazione dei contenuti (id unici, prerequisiti esistenti e senza cicli, icone presenti per strutture e oggetti del mercato, obiettivi di campagna su id noti) e test di salvataggio/ricarica

### Correzioni

- **Salvataggio atomico**: lo slot si scrive su un file temporaneo e poi si rinomina, quindi un crash durante il salvataggio non lascia più un salvataggio troncato
- **Doppio input nello stesso tick**: due pressioni di **C** su un selezionatore, due clic sull'interruttore di vendita automatica o due vendite ravvicinate non si annullano più e ogni vendita mostra il proprio guadagno
- **Caricamento fallito**: se il caricamento di un salvataggio fallisce, la partita precedente torna completa (ricerca, livello di campagna, visuale, oggetti sui nastri); al primo caricamento si resta senza partita
- **Potenziamento del CORE**: il rifiuto dell'host dice il motivo preciso (già al livello massimo, fondi o materiali mancanti)
- **Energia accumulata non più persa**: caricando un salvataggio (o un'istantanea) la riserva di energia sopra il minimo del CORE tornava a 24 perché veniva limitata prima di ripristinare la capacità; ora capacità e riserva tornano identiche
- **Caricamento più robusto**: un salvataggio di una versione futura o con contenuti illeggibili mostra "Salvataggio non leggibile." invece di testo tecnico, e un caricamento fallito lascia intatta la partita in corso
- Livello 1 della campagna: le 8 unità di minerale di ferro iniziali sono ora parte della creazione del livello (`NewGame.Campaign`), non del solo client

### Documentazione

- Nuovi `docs/ARCHITETTURA.md` (mappa delle cartelle, ciclo di un frame, dati, salvataggi, come aggiungere un blocco, test), `docs/decisioni/0001-server-csharp.md` (server multiplayer in C#) e `CLAUDE.md` (istruzioni per gli agenti); README aggiornati ai nuovi nomi e percorsi

### Cambiato

- **Salvataggi versione 10**: il file contiene i metadati dello slot più l'istantanea canonica dello stato (`FactorySaveStore.Save(slot, SimHost)` / `TryLoad(content, slot, …)`); i salvataggi v9 e precedenti non si caricano più. I salvataggi di versioni precedenti non sono più compatibili: caricandoli compare il messaggio "Salvataggio di una versione precedente: non compatibile." e la partita corrente resta com'è. Le partite nuove si costruiscono con `NewGame.Sandbox/Campaign/Demo`; spariti i nomi `Slice`/`Spike` (`CreateSandboxSlice`, `CreateSpikeDemo`, `UnlockGodotSliceDemo`, `SaveSlice`/`LoadSlice`…)
- **Continua non più sovrascritto a vuoto**: l'autosave alla chiusura (Esci, chiusura finestra, Menu/Esc → Home) scrive su Continua solo se in questa sessione hai giocato (nuova partita, livello di campagna o caricamento riuscito). Prima, aprire il gioco e uscire subito sostituiva Continua con la sandbox di avvio, anche quando il salvataggio era solo di una versione non compatibile

### Sviluppo

- **Simulazione a passo fisso (1/5 del sotto-progetto 2)**: la fabbrica avanza a tick fissi di 1/60 s (`Factory.Step()`, contatore `CurrentTick`); `SimulationClock` converte il tempo di frame in tick con un tetto di 5 per frame. I nastri sono percorsi in ordine canonico (riga, poi colonna) e gli edifici hanno un id stabile salvato (`Id`, `NextEntityId`, `CurrentTick` nel salvataggio; i file senza id li ricevono al caricamento). Nuovi test di determinismo (FPS diversi, ordine di piazzamento, salvataggio a metà partita, ritmo di gioco). Ritmo di gioco invariato per il giocatore
- **Avvio in dev all-in-one**: `dev.cmd` (Windows) / `tools/dev/dev.sh` controlla .NET 8 e Godot 4.4 mono, compila, importa gli asset se serve e avvia il gioco; opzioni `--profilo-temp`, `--nuova`, `--editor`, `--test`, `--playtest ui|tutorial`, `--solo-build`, `--import`
- **Testo e icone nitidi**: la UI usa il filtro lineare con mipmap (la mappa resta Nearest per la pixel art); prima icone 64px ridotte a 16–26px e testo alla scala 125% erano sgranati
- **Playtest isolati**: con `TINDUSTRY_PLAYTEST` gli input reali (mouse/tastiera sulla finestra) vengono ignorati; un click o Esc durante la prova la faceva fallire a caso. Il playtest `ui` lo verifica
- Rimossi 4 eventi HUD inutilizzati (0 avvisi di build)
- `SpikeWorld.cs` diviso in partial per responsabilità; i self-test di `FactorySlice` spostati nei test (−1.300 righe dal codice di gioco)
- `TINDUSTRY_DATA_DIR`: profilo dati isolato (impostazioni, salvataggi, campagna) per prove senza toccare i dati del giocatore
- `TINDUSTRY_PLAYTEST=tutorial`: playtest scriptato con input simulati (richiede `TINDUSTRY_DATA_DIR`), un'immagine per passo
- Le modalità `TINDUSTRY_CAPTURE` sono state rimosse in favore dei playtest scriptati (`TINDUSTRY_PLAYTEST`); gli screenshot dei playtest vanno in `TINDUSTRY_CAPTURE_DIR` o `godot/artifacts/` (ignorata da git)

### Rimosso

- **Client Raylib legacy** (`TIndustry.Logistics`, `src/App|Simulation|UI|Content`): Godot 4 .NET è l'unico client. Ultimo codice Raylib nel tag `v0.3.4`; niente più zip `tIndustry-raylib-legacy-*` nelle Release.

### Aggiunto / migliorato

- **Test**: progetto xUnit `tests/TIndustry.Shared.Tests` che esegue i 22 scenari self-test della sim Shared (prima mai lanciati) — sostituisce il `--self-test` Raylib come gate CI
- **Shared**: `FactorySliceSaveStore.SavesDirectoryOverride` — i test non toccano più i salvataggi reali del giocatore
- **CI**: workflow non più automatici su push/PR — `build-publish.yml` solo manuale; `release.yml` (tag `v*` o manuale) esegue i test Shared prima dell'export
- **Repo**: `tIndustry.sln` alla radice (Shared + test + Godot); CI e release solo .NET 8 / Godot

## [0.3.4] — 2026-09-26

Parity mondo Raylib: minerali, CORE centrato, H→core, bordi mappa, terrain, **tinte item distinte**. Zip ufficiali restano **export Godot**.

### Aggiunto / migliorato

- **Godot/Shared**: parity mondo Raylib — minerali icone/scout patches, CORE centrato + camera H/Home, bordi mappa + clamp, terrain color Raylib, no tutorial spam in campagna ([#129](https://github.com/Juan0177/tIndustry/pull/129))
- **Godot/Shared**: tinte item/minerali distinte (Raylib `ItemColor`) — silhouettes bianche modulate via `ItemVisualColors` su depositi, cargo, HUD stock, sorter filter ([#130](https://github.com/Juan0177/tIndustry/pull/130))

### Come giocare (download)

1. Scarica [`tIndustry-win-x64.zip`](https://github.com/Juan0177/tIndustry/releases/latest) (o linux)
2. Estrai → avvia `tIndustry.exe` (Windows) / `tIndustry.x86_64` (Linux)

Da sorgente: `godot4 --path godot` (vedi README).

## [0.3.3] — 2026-09-26

Godot QoL + mondo grande: gestione salvataggi completa, overlay risorse sistema, mappe **1000×1000** seed-based. Zip ufficiali restano **export Godot**.

### Aggiunto / migliorato

- **Godot**: Gestione salvataggi full — autosave Continua (Esc/Home/Esci), Salva come, seed in lista, ↑↓, Conferma elimina ([#125](https://github.com/Juan0177/tIndustry/pull/125))
- **Godot/Shared**: overlay **risorse sistema** (FPS · CPU · RAM · GPU · tempo processo) da Impostazioni ([#126](https://github.com/Juan0177/tIndustry/pull/126))
- **Godot/Shared**: mondo **1000×1000** seed-based (campagna + sandbox), clamp 1000, draw con **viewport culling** ([#127](https://github.com/Juan0177/tIndustry/pull/127))

### Come giocare (download)

1. Scarica [`tIndustry-win-x64.zip`](https://github.com/Juan0177/tIndustry/releases/latest) (o linux)
2. Estrai → avvia `tIndustry.exe` (Windows) / `tIndustry.x86_64` (Linux)

Da sorgente: `godot4 --path godot` (vedi README).

## [0.3.2] — 2026-09-26

Parity patch: Godot recupera i gap Raylib (P0–P1) — costi, multi-ricetta, power, tutorial, crumbs mid-game. Zip ufficiali restano **export Godot** (come 0.3.1).

### Aggiunto / migliorato

- **Godot/Shared**: costi di piazzamento + rimborso come Raylib (`FactorySlice` spende `BuildCost` / rimborsa su remove); sandbox Nuova partita parte con lastre/fili; toast IT se stock insufficiente ([#114](https://github.com/Juan0177/tIndustry/pull/114))
- **Godot/Shared**: forno/assemblatore **multi-ricetta** (auto-pick `smelt-*` / `craft-*` come Raylib) — piombo, titanio, grafite, silicio craftabili ([#115](https://github.com/Juan0177/tIndustry/pull/115))
- **Godot**: palette/place **Nastro T3** (`conveyor-express`) + **Nodo potenza T2** (`power-node-t2`) ([#116](https://github.com/Juan0177/tIndustry/pull/116))
- **Godot/Shared**: **CORE upgrade** (+25% vendite) da `economy.coreUpgrade`, chip HUD, save v5 ([#117](https://github.com/Juan0177/tIndustry/pull/117))
- **Godot/Shared**: forno **carbone OR corrente** (Raylib): buffer fuel + burn, craft bloccato senza; **+20%** se alimentato; save v6 ([#118](https://github.com/Juan0177/tIndustry/pull/118))
- **Godot/Shared**: **power buffer/capacity** (Raylib thin): core+gen ricarica, spend on craft, soft brownout → forno torna al carbone; HUD `pot. buf/cap`; save v7 ([#119](https://github.com/Juan0177/tIndustry/pull/119))
- **Godot**: **tutorial first-run** (8 step IT, Avanti/Salta, persist) + **Impostazioni** (VSync, scala UI, FPS overlay, rivedi tutorial) da Home / tasto I ([#120](https://github.com/Juan0177/tIndustry/pull/120))
- **Shared**: **I/O adiacente** edificio↔edificio/core (Raylib): miner/forno transferiscono senza nastro se i footprint si toccano ([#121](https://github.com/Juan0177/tIndustry/pull/121))
- **Godot/Shared**: **map size da campagna** (`level.MapWidth/Height`, core centrato, clamp 128× per draw Godot); save v8 seed/WxH ([#122](https://github.com/Juan0177/tIndustry/pull/122))
- **Godot**: **Gestione salvataggi** (lista slot, Carica/Elimina/Duplica Continua) da Home ([#123](https://github.com/Juan0177/tIndustry/pull/123))

### Come giocare (download)

1. Scarica [`tIndustry-win-x64.zip`](https://github.com/Juan0177/tIndustry/releases/latest) (o linux)
2. Estrai → avvia `tIndustry.exe` (Windows) / `tIndustry.x86_64` (Linux)

Da sorgente: `godot4 --path godot` (vedi README).

## [0.3.1] — 2026-09-26

Patch release: i download ufficiali della Release sono **export Godot**, non più Raylib.

### Cambiato

- **Release assets**: `tIndustry-win-x64.zip` / `tIndustry-linux-x64.zip` = client **Godot** (`tIndustry.exe` / `tIndustry.x86_64`)
- Alias `tIndustry-godot-*-x64.zip` (stesso contenuto)
- Raylib solo come `tIndustry-raylib-legacy-*-x64.zip`
- CI `release.yml`: install Godot 4.4.1 mono + templates → export Windows/Linux; Raylib in job separato
- `godot/export_presets.cfg`, `godot/TIndustry.Godot.sln`, seed in `godot/data/`, script `tools/ci/setup-godot.sh` + `export-godot.sh`

### Come giocare (download)

1. Scarica [`tIndustry-win-x64.zip`](https://github.com/Juan0177/tIndustry/releases/latest) (o linux)
2. Estrai → avvia `tIndustry.exe` (Windows) / `tIndustry.x86_64` (Linux)

Da sorgente: `godot4 --path godot` (vedi README).

## [0.3.0] — 2026-09-25

**Godot 4 .NET diventa il client ufficiale.** Raylib resta legacy (zip release ancora disponibili). Port slice completo: fabbrica, UI Mindustry, campagna, art, logistica.

### Cambiato

- **Godot 4 .NET = path ufficiale** per build & play ([#111](https://github.com/Juan0177/tIndustry/pull/111))
- Client Raylib (`TIndustry.Logistics`) demoted a **legacy** (manutenzione opzionale)
- CI: job **Godot + Shared**; publish zip Raylib invariato (etichettato legacy)

### Godot — gameplay & sistemi

- Factory slice: nastri (corner gallery), craft (forno/assy), junction/splitter, power stubs ([#66](https://github.com/Juan0177/tIndustry/pull/66)–[#87](https://github.com/Juan0177/tIndustry/pull/87))
- **Save/load** continua + slot ([#88](https://github.com/Juan0177/tIndustry/pull/88))
- **Sorter + ponte** (decision 14 thin span) ([#89](https://github.com/Juan0177/tIndustry/pull/89))
- **Ricerca** unlock + tech-tree grafo ([#91](https://github.com/Juan0177/tIndustry/pull/91)–[#92](https://github.com/Juan0177/tIndustry/pull/92))
- **Mercato** sell + `$` HUD ([#93](https://github.com/Juan0177/tIndustry/pull/93)); **auto-sell** al Core ([#99](https://github.com/Juan0177/tIndustry/pull/99))
- **Campagna** select + obiettivi HUD ([#94](https://github.com/Juan0177/tIndustry/pull/94))
- **Home splash** Continua / Campagna / Nuova partita ([#98](https://github.com/Juan0177/tIndustry/pull/98))
- **T2** nastro/miner, estrattore, power-node ([#100](https://github.com/Juan0177/tIndustry/pull/100))
- **Map seed** terrain da campagna/sandbox ([#101](https://github.com/Juan0177/tIndustry/pull/101))

### Godot — UI Mindustry

- Palette angolo + categorie St/Lo/Pr/Po · info strip · tech tree icon-only ([#95](https://github.com/Juan0177/tIndustry/pull/95)–[#97](https://github.com/Juan0177/tIndustry/pull/97))
- Cursor-default toolbar ([#90](https://github.com/Juan0177/tIndustry/pull/90))

### Godot — art

- Block sprites 64×64 coerenti mappa + palette ([#102](https://github.com/Juan0177/tIndustry/pull/102))
- Nastro T2 single chevron ([#103](https://github.com/Juan0177/tIndustry/pull/103))
- Logistics redesign splitter/sorter/bridge ([#104](https://github.com/Juan0177/tIndustry/pull/104)–[#105](https://github.com/Juan0177/tIndustry/pull/105))
- Miner gear animato ([#106](https://github.com/Juan0177/tIndustry/pull/106)–[#109](https://github.com/Juan0177/tIndustry/pull/109))
- Estrattore / forno / assemblatore / generatore / core redesign ([#110](https://github.com/Juan0177/tIndustry/pull/110))

### Limitazioni

- Combat / unità ancora deferred
- Zip GitHub Release = client **Raylib legacy**; per Godot: `godot4 --path godot` (vedi README)
- Art polish opzionale in corso

### Come giocare (ufficiale — Godot)

```bash
dotnet build src/TIndustry.Shared/TIndustry.Shared.csproj
dotnet build godot/TIndustry.Godot.csproj
godot4 --path godot
```

Richiede Godot **4.4+** .NET. Dettagli: [`godot/README.md`](godot/README.md).

### Legacy Raylib

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

O scarica gli zip dalla Release (`tIndustry-win-x64.zip` / `tIndustry-linux-x64.zip`).

## [0.2.11] — 2026-09-21

Polish completo sopra **0.2.10**: UX/copy IT, feedback, HUD, ghost placement — niente feature P2.

### Aggiunto / migliorato

- **Copy IT**: tutorial Potenza, dock Sdop/Selez/Pot., Ingresso/Uscita, magazzino (no “stock”), toast potenza/raggio, Menu principale, Ricerca, segnaposto
- **Font HUD**: atlas con — → … ’ ✓ Δ (niente più `?` ripetuti); atlas 160×scale + Bilinear per testo meno granuloso; spacing più stretto
- **Overflow**: truncate toast/home/ricerca/campagne/save/header; CORE upgrade sempre testo compatto (niente icone sovrapposte); Mercato righe dinamiche
- **Short labels**: Pb/Ti lastre → `Pb Ls` / `Ti Ls` nella strip
- **Feedback**: toast errore in ambra; refuse place nastri/logistica; salvataggio Esc con “Partita salvata.”; elimina slot a doppia conferma
- **HUD**: strip risorse overflow (+N, priorità stock); Fabbrica Pot. + tooltip legenda M/F/A/N/G/P; Home centrata; save manager responsive
- **Ghost**: silhouette anche se invalido; cerchio raggio nodi T1/T2
- **Campagna**: obiettivo hit-test (no click-through); label Magazzino; vittoria “Menu principale”
- **Docs**: README allineato a 0.2.11

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.10] — 2026-09-21

P1 gap close sopra **0.2.9**: campagna estesa, atlas icone, polish tech-tree UX.

### Aggiunto / migliorato

- **Campagna 10 livelli**: dopo Economia Foundry → Corrente (gen+nodo) → Piombo → Logistica (sorter) → Titanio/silicio → Espansione T2; bilanciamento starter/obiettivi mid-game
- **AppData campagna**: merge id seed mancanti + sync name/obiettivi/`unlocksNext` (come `content.json`)
- **Tech-tree UX**: Ctrl+rotella zoom (verso cursore), path highlight su prerequisiti/dipendenti, H/Home reset pan/zoom
- **Atlas icone**: `GameIcons` packa ~40 PNG in una sola texture (UV draw); API `Draw`/`TryDraw` invariata
- **Docs**: README allineato a 0.2.10

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.9] — 2026-09-21

P0 gap close sopra **0.2.8**: mercato dinamico, più minerali/ricette, polish animazioni/lighting.

### Aggiunto / migliorato

- **Mercato dinamico**: prezzi reagiscono allo stock (curva supply / softStock); bonus CORE applicato sul prezzo dinamico; Mercato mostra fino a 6 voci (stockate in alto)
- **Minerali**: depositi **piombo** + **titanio** (noise + starter/scout); item grezzi + lastre
- **Ricette**: forno multi-ricetta (`smelt-iron` / `smelt-lead` / `smelt-titanium`); assemblatore (`craft-copper-wire` / `craft-graphite` / `craft-silicon`)
- **Grafica**: glow forno gated su fuel/power + bloom; trivella idle rock a 0%; ombre soft a 2 layer
- **Docs**: README allineato a 0.2.9

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.8] — 2026-09-21

Release di contenuto sopra **0.2.7**: mid-game Phase 6, I/O, tech tree, sorter, potenza a nodi, forno carbone-o-corrente, icona minatore **trivella**.

### Aggiunto / migliorato

- **I/O belt-uscente** + trasferimento **edificio adiacente** (miner|forno, forno|assy, →CORE); overlay amber/ciano; tutorial **14** passi ([#38](https://github.com/Juan0177/tIndustry/pull/38))
- **Tech tree a grafo**: nodi/archi da `prerequisites`; hit-test unlock + sync prereq AppData ([#40](https://github.com/Juan0177/tIndustry/pull/40), [#47](https://github.com/Juan0177/tIndustry/pull/47))
- **Selezionatore (sorter)**: filtro item (F), routing Mindustry ([#39](https://github.com/Juan0177/tIndustry/pull/39))
- **Grafica mondo (Raylib)**: silhouettes TI-inspired, terrain micro-variazione ([#42](https://github.com/Juan0177/tIndustry/pull/42))
- **Mid-game T2/T3**: Minatore T2, Nastro T3, fuel generatore (carbone) ([#41](https://github.com/Juan0177/tIndustry/pull/41))
- **Icone**: ore rock+tint ([#44](https://github.com/Juan0177/tIndustry/pull/44)); minatore = **trivella / auger** Lorc `drill` (non trapano a pistola); T2 riusa lo stesso PNG
- **AppData**: merge id seed mancanti (no crash) ([#45](https://github.com/Juan0177/tIndustry/pull/45)); sync `displayName` tier ([#46](https://github.com/Juan0177/tIndustry/pull/46))
- **Overlay versione** semi-trasparente in play HUD ([#48](https://github.com/Juan0177/tIndustry/pull/48))
- **Nodi potenza** T1/T2 (linee dirette + auto-link); cavi tile superseduti; grafo esclude CORE ([#49](https://github.com/Juan0177/tIndustry/pull/49)–[#51](https://github.com/Juan0177/tIndustry/pull/51))
- **Forno = carbone OR corrente** (+20% craft se alimentato da rete) ([#52](https://github.com/Juan0177/tIndustry/pull/52))
- Naming UI a **codici tier** (Minatore T1/T2, Nastro T1/T2/T3)
- **Docs**: README **revamp** per 0.2.8 (pitch mid-game, download first, controlli/power aggiornati)

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.7] — 2026-09-19

Patch UX sopra **0.2.6**: bottone upgrade **CORE** mostra costo completo (×N lastre + $), come il footer dock. ([#35](https://github.com/Juan0177/tIndustry/pull/35))

### Aggiunto / migliorato

- **Fabbrica / CORE**: label Peak-style `CORE · ×N [icona lastre] · +$cost`; toast di afford cita `×N lastre` (non più "+ lastre" vago) ([#35](https://github.com/Juan0177/tIndustry/pull/35))
- **Docs**: README allineato a 0.2.7

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.6] — 2026-09-19

Patch UX sopra **0.2.5** (Campagna già inclusa): polish Mercato/dock, ricette Input/Output nel footer, fix conteggi Fabbrica e upgrade CORE. ([#31](https://github.com/Juan0177/tIndustry/pull/31), [#32](https://github.com/Juan0177/tIndustry/pull/32), [#33](https://github.com/Juan0177/tIndustry/pull/33))

### Aggiunto / migliorato

- **Mercato / dock**: padding e accent polish; seeding capture per still HUD ([#31](https://github.com/Juan0177/tIndustry/pull/31))
- **Dock footer**: riga **Input / Output** (icone ×N + durata ricetta) quando tieni Forno/Assemblatore; sotto resta il costo build ([#32](https://github.com/Juan0177/tIndustry/pull/32))
- **Fabbrica HUD**: conteggi `M/F/A/N/G` restano sopra **CORE** a scala UI di default; click CORE con feedback afford / già potenziato ([#33](https://github.com/Juan0177/tIndustry/pull/33))
- **Docs**: README allineato a 0.2.6

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.5] — 2026-09-19

Patch sopra **0.2.4**: modalità **Campagna** a livelli + fix clip Mercato e costi build a icone nel dock. ([#28](https://github.com/Juan0177/tIndustry/pull/28), [#29](https://github.com/Juan0177/tIndustry/pull/29))

### Aggiunto / migliorato

- **Campagna**: Home → **Campagna** → selezione livelli data-driven (`data/campaign.json`); obiettivi (earn/stock/unlock/sell); HUD **OBIETTIVO**; progressione in AppData `campaignProgress.json`; 5 livelli starter ([#28](https://github.com/Juan0177/tIndustry/pull/28))
- **Mercato**: bottoni `1` / `tutti` inset e scale-safe — etichette non più tagliate dal bordo giallo ([#29](https://github.com/Juan0177/tIndustry/pull/29))
- **Dock footer**: costi build Peak-style (`Nome · ×N [icona] · +$cost`); **Rimuovi** con label chiaro senza costi finti ([#29](https://github.com/Juan0177/tIndustry/pull/29))
- **Docs**: README allineato a 0.2.5

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.4] — 2026-09-19

Patch UX sopra **0.2.3**: pannello Mercato dedicato, icone più chiare, tutorial stock-first a 6 step, HUD scale-safe. ([#26](https://github.com/Juan0177/tIndustry/pull/26))

### Aggiunto / migliorato

- **Mercato**: pannello dedicato (stock + vendi 1/tutti + prezzi + auto-sell), separato dalla strip **Fabbrica** (tips / CORE)
- **Icone**: item/dock più leggibili con etichette IT brevi; icona vendita UI
- **Tutorial**: **6 step** produce → stock → spendi/vendi → ricerca (prima 5)
- **UI scale**: Mercato soft-scale + scroll Impostazioni — layout valido a **100–200%** senza overlap HUD
- **Docs**: README allineato a 0.2.4

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.3] — 2026-09-19

Patch economia sopra **0.2.2**: core stock-first, vendita esplicita Mercato, auto-sell opzionale. ([#24](https://github.com/Juan0177/tIndustry/pull/24))

### Changed

- **Economia stock-first**: item al core → magazzino (wallet); vendita esplicita dal Mercato (`1` / `tutti`) o toggle **Vendita automatica** (OFF di default, persistito in `settings.json`) ([#24](https://github.com/Juan0177/tIndustry/pull/24))
- **Docs**: README allineato a 0.2.3

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.2] — 2026-09-19

Patch logistica + tutorial sopra **0.2.1**: sdoppiatore T-fork, miner round-robin, tutorial ripartibile. ([#21](https://github.com/Juan0177/tIndustry/pull/21), [#22](https://github.com/Juan0177/tIndustry/pull/22))

### Aggiunto / migliorato

- **Sdoppiatore**: flusso lungo il facing + alternanza sinistra/destra al handoff; drag-connect non ruota più lo splitter (come gli incroci) ([#21](https://github.com/Juan0177/tIndustry/pull/21))
- **Miner**: eject **round-robin** sui nastri adiacenti che accettano — più uscite ricevono ore nel tempo ([#21](https://github.com/Juan0177/tIndustry/pull/21))
- **Tutorial**: **Nuova partita → Conferma** riparte sempre da Tutorial 1/5; **Impostazioni → Rivedi tutorial** azzera `tutorialCompleted`; Salta/Fine restano per la run corrente ([#22](https://github.com/Juan0177/tIndustry/pull/22))
- **Docs**: README allineato a 0.2.2

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.1] — 2026-09-19

Patch QoL / playability sopra **0.2.0**: nastri leggibili, UI scale, tutorial, miner multi-lato, toast, layout `src/`. ([#15](https://github.com/Juan0177/tIndustry/pull/15), [#16](https://github.com/Juan0177/tIndustry/pull/16), [#17](https://github.com/Juan0177/tIndustry/pull/17), [#18](https://github.com/Juan0177/tIndustry/pull/18), [#19](https://github.com/Juan0177/tIndustry/pull/19); splash ~8s già in 0.2.0 via [#13](https://github.com/Juan0177/tIndustry/pull/13))

### Aggiunto / migliorato

- **Nastri**: chip item saturi + icona sopra gli edifici; viaggio lungo `RoutedExit` — i minerali in transito si vedono di nuovo ([#16](https://github.com/Juan0177/tIndustry/pull/16))
- **UI scale**: Impostazioni **100% / 125% / 150% / 200%** (default **125%**), atlas DejaVu riscalato; layout Impostazioni senza overlap ON/OFF ([#16](https://github.com/Juan0177/tIndustry/pull/16), [#19](https://github.com/Juan0177/tIndustry/pull/19))
- **Tutorial**: banner IT a 5 step alla prima Nuova partita; **Salta** / Backspace; `tutorialCompleted` nel save ([#16](https://github.com/Juan0177/tIndustry/pull/16))
- **Piazzamento**: edifici su qualsiasi terra libera (non acqua); miner off-deposito a **0%**; click su Forno bloccato non ruba più lo strumento ([#16](https://github.com/Juan0177/tIndustry/pull/16))
- **Risorse sistema**: overlay **CPU · GPU · RAM** (FPS/CPU/RAM; GPU via `nvidia-smi` se c’è); strip inventario sempre a parte ([#16](https://github.com/Juan0177/tIndustry/pull/16))
- **Miner**: eject su **tutti e quattro** i lati adiacenti (qualsiasi nastro che accetta); freccia facing rimossa ([#17](https://github.com/Juan0177/tIndustry/pull/17))
- **Direzione nastro**: chevron unidirezionali allineati al facing (niente hash bidirezionali) ([#17](https://github.com/Juan0177/tIndustry/pull/17))
- **HUD**: FPS unico (angolo XOR overlay sistema); overlay sistema solo in play; font più nitido a scale alte ([#17](https://github.com/Juan0177/tIndustry/pull/17))
- **Status toast**: auto-clear ~3.5s con fade; **Esc** lo chiude ([#18](https://github.com/Juan0177/tIndustry/pull/18))
- **Repo**: sorgenti sotto `src/{App,Simulation,Content,UI}/`; `.csproj` in root ([#19](https://github.com/Juan0177/tIndustry/pull/19))
- **Docs**: README allineato a 0.2.1 ([#15](https://github.com/Juan0177/tIndustry/pull/15) + refresh release)

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.2.0] — 2026-09-19

Minor sopra **0.1.1**: first-launch AppData, icon pack HUD, splash + ottimizzazioni hot-path, tooling MCP. ([#9](https://github.com/Juan0177/tIndustry/pull/9), [#10](https://github.com/Juan0177/tIndustry/pull/10), [#11](https://github.com/Juan0177/tIndustry/pull/11), [#12](https://github.com/Juan0177/tIndustry/pull/12), [#13](https://github.com/Juan0177/tIndustry/pull/13); polish UI [#8](https://github.com/Juan0177/tIndustry/pull/8))

### Aggiunto / migliorato

- **Contenuti first-launch**: niente Excel nel publish; seed `content.json` → AppData al primo avvio (`~/.local/share/tIndustry/content/`) ([#9](https://github.com/Juan0177/tIndustry/pull/9))
- **Home**: **Continua** nascosto se non c’è autosave valido ([#9](https://github.com/Juan0177/tIndustry/pull/9))
- **Dock**: rimossa categoria **Strumenti**; **Rimuovi** in Produzione; tooltip hover IT (nome + hint) ([#9](https://github.com/Juan0177/tIndustry/pull/9))
- **Grafica**: icon pack HUD/dock/nastri (game-icons.net CC BY 3.0 + Kenney CC0); pannelli più nitidi; label **Δ sessione** ([#10](https://github.com/Juan0177/tIndustry/pull/10))
- **Splash**: schermata avvio con `assets/thumbnail.png`, brand tINDUSTRY, dismiss click/tasto o auto (~8s) ([#12](https://github.com/Juan0177/tIndustry/pull/12), [#13](https://github.com/Juan0177/tIndustry/pull/13))
- **Performance**: culling terreno, cache dock/home, intake nastro O(footprint), bridge O(1), meno alloc per frame ([#12](https://github.com/Juan0177/tIndustry/pull/12))
- **Tooling**: `.cursor/mcp.json`, raccomandazioni C# VS Code, bootstrap `dnx` in `install.sh` ([#11](https://github.com/Juan0177/tIndustry/pull/11))
- **UI polish**: loading, icone dock, fix Tools/miner su rotazione ([#8](https://github.com/Juan0177/tIndustry/pull/8))

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.1.1] — 2026-09-19

Patch UI/QoL dopo 0.1.0: dock Mindustry, controlli camera, core centrato, Impostazioni display estese. ([#7](https://github.com/Juan0177/tIndustry/pull/7))

### Aggiunto / migliorato

- **Dock Mindustry** (basso-destra): colonna categorie (Produzione / Logistica / Potenza / Strumenti / Inventario) + griglia piazzabili/item con bordo di selezione giallo/arancio
- **Header snello** + strip risorse (se overlay attivo); pannello Mercato compatto invece della barra piena a destra
- **Camera**: zoom solo con **Ctrl+rotella**; **rotella** sola ruota la direzione di piazzamento; **niente edge pan** (restano WASD / drag centrale / Shift+drag)
- **Core al centro** della mappa 1000×1000; la camera parte sul core
- **Impostazioni**: preset **2K** (2560×1440) e **4K** (3840×2160), **Auto risoluzione**, **VSync** on/off, **limite FPS** (30→600) + **Illimitato** — persistiti in `settings.json`

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

## [0.1.0] — 2026-09-19

Prima release pubblica-ish: fasi **0–6** sul prototipo C# / Raylib (`.NET 10`), più Impostazioni e CI publish.

### Aggiunto

- **Mondo**: mappa **1000×1000**, camera pan/zoom, culling viewport, depositi ferro e rame
- **Home / sessione**: Continua, Nuova partita, Gestione salvataggi, Impostazioni, Esci
- **Nuova partita**: selezione scenario (Classico / Espanso / Arcipelago) + seed numerico
- **Produzione**: minatore, **forno** (`smelt-iron`), **assemblatore** (rame + lastra → fili)
- **Logistica**: nastro base / **veloce**, **incrocio**, **sdoppiatore**, **ponte** (span 2–4)
- **Economia**: wallet (denaro + lastre + fili), prezzi mercato content-driven, rimborso rimozione 100%, **potenziamento core** (+25% vendite)
- **Ricerca**: sblocchi strutture data-driven, a pagamento, persistenti nel save
- **Power (stub)**: pool globale (baseline core + **Generatore**); forni/assemblatori consumano potenza (brownout = craft in stall)
- **QoL**: overlay FPS/risorse (Impostazioni), onboarding tip contestuali, UI in italiano, `--self-test` esteso
- **Persistenza**: save JSON versionato (**v6**: edifici, ponti, unlock, economia, generatori, PowerBuffer)
- **CI**: publish self-contained `win-x64` / `linux-x64` su push/PR; release job su tag `v*`

### Limitazioni note

- Niente **fuel / coal grid** né cavi di potenza (solo pool globale)
- Niente **combat**, unità, fog of war
- `miner-advanced` resta **stub** (nel content/ricerca, non giocabile)
- Bilanciamento ancora grezzo; poche ricette mid-game
- Slot nominati solo via **Duplica Continua** (niente “Salva come…” in-game)
- Grafica procedurale (niente sprite atlas)

### Verifica

```bash
dotnet run --project TIndustry.Logistics.csproj -- --self-test
dotnet run --project TIndustry.Logistics.csproj
```

[0.3.4]: https://github.com/Juan0177/tIndustry/releases/tag/v0.3.4
[0.3.3]: https://github.com/Juan0177/tIndustry/releases/tag/v0.3.3
[0.3.2]: https://github.com/Juan0177/tIndustry/releases/tag/v0.3.2
[0.3.1]: https://github.com/Juan0177/tIndustry/releases/tag/v0.3.1
[0.3.0]: https://github.com/Juan0177/tIndustry/releases/tag/v0.3.0
[0.2.2]: https://github.com/Juan0177/tIndustry/releases/tag/v0.2.2
[0.2.1]: https://github.com/Juan0177/tIndustry/releases/tag/v0.2.1
[0.2.0]: https://github.com/Juan0177/tIndustry/releases/tag/v0.2.0
[0.1.1]: https://github.com/Juan0177/tIndustry/releases/tag/v0.1.1
[0.1.0]: https://github.com/Juan0177/tIndustry/releases/tag/v0.1.0
