# Architettura di tIndustry

Gioco in Godot 4.4 .NET. La simulazione è C# puro in `src/TIndustry.Shared/` (nessuna dipendenza da Godot); il client in `godot/` la disegna e raccoglie l'input.

## Mappa delle cartelle

```
tIndustry/
├─ CLAUDE.md · README.md · CHANGELOG.md · tIndustry.sln
├─ data/                        unica fonte dei contenuti: content.json, campaign.json
├─ docs/
│  ├─ ARCHITETTURA.md           questo file
│  ├─ decisioni/                ADR (0001-server-csharp.md, 0002-sincronizzazione-ibrida.md)
│  └─ superpowers/              specifiche e piani
├─ src/TIndustry.Shared/        simulazione (futuro codice comune a client e server)
│  ├─ Factory.cs                facciata: piazzamento, economia, Capture/Restore; Step() → Simulation.Step
│  ├─ Simulation/               SimulationClock, GameState, Simulation, Systems/ (sette sistemi), Commands, SimHost, IGameView, Snapshot, NewGame
│  ├─ Content/                  ContentTypes, FactoryContent, ConveyorContent, MarketCatalog, ItemVisualColors
│  ├─ Grid/                     GridTypes, BeltGrid, BeltLane, TransportedItem, TerrainMap
│  ├─ Buildings/                Miner, Crafter (forno e assemblatore), Extractor, BuildingIo, CoreStockSink
│  ├─ Power/                    Generator, PowerNode
│  ├─ Economy/                  EconomyWallet, EconomySession
│  ├─ Research/                 ResearchState, TechTreeLayout
│  ├─ Campaign/                 CampaignCatalog, CampaignProgress
│  └─ Save/                     FactorySave (FactorySaveData, FactorySaveStore), GameDataPaths
├─ godot/
│  ├─ project.godot · export_presets.cfg · TIndustry.Godot.csproj
│  ├─ scenes/Main.tscn          scena principale (nodo radice Main, script World)
│  ├─ shaders/                  belt_scroll, belt_corner_v2
│  ├─ assets/                   icone (cartella piatta; ui/ per quelle dell'interfaccia) + ATTRIBUTION.md
│  └─ scripts/
│     ├─ world/                 World + partial (Input, Placement, Session, Drawing, Visuals, Panels), WorldCamera
│     ├─ visuals/belts/         MindustryBeltVisual, ScrollingBeltStrip, BeltCornerTile, BridgeSpanVisual
│     ├─ visuals/buildings/     MinerVisual, SmelterVisual, AssemblerVisual, GeneratorVisual, ExtractorVisual, BuildingPad
│     ├─ visuals/items/         ItemPalette, ItemSilhouette
│     ├─ ui/hud/                Hud, CorePanel, BuildPalette, BlockInfoPanel, HudStyle
│     ├─ ui/panels/             Home, Saves, Settings, Tutorial, Mercato, Research, TechTreeCanvas, CampaignSelect
│     ├─ settings/              ClientSettings
│     └─ dev/                   World.Playtest.cs, SystemMonitor
├─ tests/TIndustry.Shared.Tests/  test xUnit (scenari della sim, validazione dei contenuti, salvataggi)
└─ tools/
   ├─ ci/                       setup-godot.sh, export-godot.sh
   ├─ dev/                      playtest.sh, compare_shots.py
   └─ gen_structure_blocks.py   generatore delle icone dei blocchi
```

`godot/data/` esiste solo dopo l'export (è una copia di `data/`) ed è ignorata da git.

**Nomi storici.** I nomi dei prototipi "spike/slice" non esistono più: le partite nuove si costruiscono con `NewGame.Sandbox` / `NewGame.Campaign` / `NewGame.Demo` (`src/TIndustry.Shared/Simulation/NewGame.cs`), il percorso a L della demo è `DemoLayouts.LPath()`, il client usa `SaveGame` / `LoadGame` e tiene un `SimHost` (`World._host`). Resta il nodo HUD: `World.Panels.cs` lo crea come "FactoryHud" e `Hud._Ready` lo rinomina in "FactoryHudRoot".

## Simulazione

La simulazione è guidata da comandi, a tick fisso, ed è deterministica (modello di sincronizzazione: `docs/decisioni/0002-sincronizzazione-ibrida.md`).

- **Tick fisso.** `SimulationClock` (`src/TIndustry.Shared/Simulation/SimulationClock.cs`) trasforma il tempo di frame in tick da 1/60 s: `TickRate = 60`, `TickSeconds = 1f / 60f`, `MaxTicksPerFrame = 5` (oltre il tetto il tempo in eccesso è scartato, niente spirale di recupero). La simulazione non vede mai il tempo del frame.
- **`GameState`** (`Simulation/GameState.cs`) contiene tutto lo stato mutabile: griglia e nastri, liste di edifici, portafoglio, ricerca, mercato, sessione economica, potenza, livello di campagna, auto-vendita, upgrade del CORE, contatori di id (`NextEntityId`, `NextItemId`) e `CurrentTick`. `Factory` ne è la facciata (piazzamento, economia, `Capture`/`Restore`) e implementa `IGameView`.
- **Sistemi.** `Simulation.Step(state)` (`Simulation/Simulation.cs`) avanza esattamente un tick eseguendo `Simulation.Systems` (cartella `Simulation/Systems/`) in quest'ordine: `BeltSystem` (avanzamento) → `PowerSystem` → `MiningSystem` → `CraftingSystem` (forni e assemblatori) → `ExtractionSystem` → `BeltSystem` (secondo passaggio, passo zero) → `CoreDeliverySystem`; poi incrementa `CurrentTick`. `SystemOrderTests` blocca l'ordine.
- **Comandi.** Ogni modifica al gioco è un record `SimCommand` (`Simulation/Commands.cs`) con `PlayerId` e `Tick`: `PlaceBelt`, `PlaceBuilding` (con `BuildingKind`), `Remove`, `UnlockStructure`, `Sell`, `SetAutoSell`, `UpgradeCore`, `SetSorterFilter`. Sono **validati all'applicazione**, non all'invio.
- **`SimHost`** (`Simulation/SimHost.cs`) è il "server locale": `Submit(cmd)` mette in coda il comando (con `Tick == 0` vale "il prossimo tick"); `Advance(frameSeconds)` esegue i tick dovuti e, all'inizio di ciascuno, applica i comandi di quel tick in ordine di arrivo, poi chiama `Factory.Step()`. L'esito è un evento: `CommandApplied` oppure `CommandRejected` con il motivo in italiano (`CommandReasons`), a stato invariato. Un gestore che lancia non interrompe la simulazione (eccezioni raccolte in un `AggregateException` a fine `Advance`).
- **`IGameView`** (`Simulation/IGameView.cs`) è la vista in sola lettura che il client usa per disegnare e per l'HUD (`SimHost.State`): il client non può mutare il gioco, può solo inviare comandi. Oggi questo vale per convenzione: `IGameView` espone ancora entità concrete (`BeltGridCell.SetFilterItem`, `Miner`, `Crafter`, `MarketCatalog`); il confine di sola lettura a livello di tipo arriva col sotto-progetto 5.
- **Istantanea e checksum.** `SimHost.CreateSnapshot()` produce l'istantanea canonica (`Snapshot.Write`: JSON compatto, edifici in ordine di id, celle dei nastri per (Y, X), collezioni con chiave in ordine ordinale); `SimHost.FromSnapshot` riparte da essa (coda comandi vuota, orologio nuovo). `SimHost.Checksum()` è FNV-1a a 64 bit sui byte dell'istantanea (`Snapshot.Checksum`). L'istantanea contiene solo lo stato applicato (niente coda dei comandi né resto dell'orologio) ed è valida ai confini di tick. La cadenza "ogni 60 tick" è decisa ma **non ancora implementata**: oggi il checksum si calcola a richiesta, il confronto periodico arriva con il sotto-progetto 5.
- **Nuove partite.** `NewGame.Sandbox` / `NewGame.Campaign` / `NewGame.Demo` (`Simulation/NewGame.cs`) costruiscono lo stato iniziale; le risorse iniziali fanno parte della creazione, non sono comandi.

## Regole di determinismo

Il codice di `src/TIndustry.Shared/` che gira in `Step()` le rispetta; i test `DeterminismTests`, `SnapshotTests` e `SystemOrderTests` le fanno valere:

1. `Step()` non riceve tempo: ogni durata usa `TickSeconds` o conteggi di tick.
2. Mai dipendere dall'ordine di `Dictionary`/`HashSet`: si itera per id crescente o con ordinamento esplicito.
3. Niente orologio di sistema e niente `Random` non seminato (un eventuale generatore vive nello stato con il seme salvato).
4. Niente parallelismo nella simulazione; niente conversioni testo↔numero dipendenti dalla cultura (formati invarianti).
5. Nessun riferimento a Godot o al sistema operativo in `TIndustry.Shared` (ADR 0001).

I calcoli restano in `float`: le differenze residue tra architetture CPU si rilevano col checksum e si riparano con l'istantanea (ADR 0002).

## Ciclo di un frame

1. Godot chiama `World._Process` (`godot/scripts/world/World.cs`). L'input non passa da `_Process`: arriva dai callback di Godot (`World._UnhandledInput` in `World.Input.cs`) e diventa **comandi** (`World.Placement.cs` → `World.SubmitCommand` in `World.Commands.cs` → `SimHost.Submit`).
2. `_Process` chiama `World.AdvanceHost(delta)` (`World.Commands.cs`), che chiama `SimHost.Advance(delta)`: l'orologio restituisce quanti tick fissi eseguire (al massimo 5); a ogni tick l'host applica i comandi in coda e chiama `Factory.Step()` → `Simulation.Step`. Gli eventi `CommandApplied` / `CommandRejected` arrivano a `World` (toast, aggiornamento di visual e pannelli).
3. Se la griglia o gli edifici sono cambiati, `World` ricostruisce i visual dei nastri e degli edifici leggendo `IGameView`; poi sincronizza sprite degli oggetti e animazioni (`World.Visuals.cs`, `godot/scripts/visuals/`).
4. `World.UpdateHud` chiama `Hud.UpdateStock`, che inoltra a `CorePanel` (fondi, energia, risorse); `BuildPalette` e `BlockInfoPanel` seguono selezione e sblocchi.
5. Controllo di completamento della campagna e `QueueRedraw` (`World.Drawing.cs` disegna terreno e griglia).

Con la Home aperta o senza partita il frame salta i passi 2–5.

## Dati

`data/content.json` (strutture, oggetti, ricette, ricerca, mercato) e `data/campaign.json` (livelli e obiettivi) sono l'unica fonte. In sviluppo il gioco legge `data/` direttamente (`World.ResolveContentPath` prova `res://../data/` per primo). All'export `tools/ci/export-godot.sh` copia i due file in `godot/data/` perché finiscano nel pacchetto; quella copia non va committata.

`ContentValidationTests` controlla id unici, prerequisiti esistenti e senza cicli, icone presenti per strutture e oggetti, obiettivi di campagna su id noti.

## Salvataggi e impostazioni

`GameDataPaths.Root` (`src/TIndustry.Shared/Save/GameDataPaths.cs`) è la radice dei dati del giocatore: `%LOCALAPPDATA%/tIndustry` (Linux `~/.local/share/tIndustry`), oppure la cartella indicata da `TINDUSTRY_DATA_DIR`. Sotto: `godot-saves/` (salvataggi) e `campaignProgress.json`.

Le impostazioni (`settings.json`, `ClientSettings.SettingsDirectory`) seguono un'altra regola: con `TINDUSTRY_DATA_DIR` stanno in `<TINDUSTRY_DATA_DIR>/tIndustry/settings.json`, altrimenti nella cartella utente di Godot (`%APPDATA%\Godot\app_userdata\tIndustry\tIndustry\settings.json`).

Il salvataggio è `SaveFileData` / `FactorySaveStore` (`src/TIndustry.Shared/Save/FactorySave.cs`), **versione 10**: un file JSON con i metadati dell'elenco (fondi, seme, mappa, livello) e l'istantanea canonica (`Snapshot`, in base64; la struttura è `FactorySaveData`). Si scrive con `FactorySaveStore.Save(slot, SimHost)` e si legge con `TryLoad(content, slot, out SimHost?, out error)`; comandi in coda e orologio non sono salvati (il salvataggio è lo stato applicato). Un file di versione precedente è rifiutato con "Salvataggio di una versione precedente: non compatibile."; un file illeggibile con "Salvataggio non leggibile."; in entrambi i casi la partita corrente non cambia. Cambiare il formato richiede di alzare `CurrentVersion`.

## Come aggiungere un blocco

1. **`data/content.json`**: aggiungi la struttura (id, costi, prerequisiti per la ricerca, ricetta/parametri). Se serve un nuovo oggetto, aggiungilo anche lì (e al mercato se vendibile).
2. **Icona** in `godot/assets/` (PNG, di norma 64×64; nome = id della struttura); aggiungi la riga in `godot/assets/ATTRIBUTION.md` con autore e licenza. Eccezioni: `conveyor-express` usa `conveyor-fast.png` e `conveyor-bridge` usa `bridge.png` (fallback in `TechTreeCanvas.ResolveIcon` e `ContentValidationTests.IconFileFor`; le righe della palette puntano direttamente al file).
3. **Strumento di costruzione** (se il blocco è piazzabile dalla palette), da collegare in questi punti:
   - `Hud.ToolKind` e `Hud.StructureIdFor` (`godot/scripts/ui/hud/Hud.cs`): nuova voce dell'enum e mappatura sull'id della struttura;
   - `BuildPalette.cs` (`godot/scripts/ui/hud/`): riga nella tabella della categoria (`ToolsEntries`, `LogisticsEntries`, `ProductionEntries` o `PowerEntries`), `CategoryFor` e nome mostrato (`ToolDisplayFallback`);
   - enum `World.BuildTool` (`godot/scripts/world/World.cs`);
   - `World.Input.cs`: `ToHudTool`, `FromHudTool`, il nome mostrato nel toast e l'eventuale tasto rapido in `_UnhandledInput`;
   - `World.Placement.cs`: ramo nello `switch` del piazzamento, che costruisce il comando (`PlaceBuilding` con il nuovo `BuildingKind`) e lo invia con `SubmitCommand`; grafica e toast di rifiuto arrivano dagli eventi dell'host (`World.Commands.cs`);
   - `World.Session.cs`: aggiungi il tool all'elenco di `SyncResearchLocks`, altrimenti non viene bloccato dalla ricerca;
   - `World.Drawing.cs`: dimensione dell'anteprima (ghost) e, per i blocchi orientabili, l'elenco che mostra la freccia di direzione.
4. **Visual** in `godot/scripts/visuals/buildings/` (o riusa uno esistente): creazione e aggiornamento in `World.Visuals.cs`. I nastri e simili sono disegnati da `MindustryBeltVisual`.
5. **Simulazione** (se il blocco è piazzabile con `PlaceBuilding`; tutti i punti in `src/TIndustry.Shared/Simulation/`; se ne manca uno il piazzamento fallisce in modo fuorviante):
   - `BuildingKind` (`Commands.cs`): nuovo valore;
   - `CommandReasons.ToolName(BuildingKind)` (`Commands.cs`): il nome mostrato, altrimenti il toast di blocco dice "? bloccato…";
   - `SimHost.StructureId` (`SimHost.cs`): mappatura sull'id della struttura (con `null` il comando è "Comando non valido.");
   - lo `switch` di piazzamento in `SimHost.ApplyPlaceBuilding`: un ramo che chiama il `Factory.TryPlace…` del blocco; senza ramo cade su `_ => false` e il rifiuto è "occupata". Il metodo `Factory.TryPlace…` deve esistere (`StructureId` e lo `switch` lo presuppongono);
   - comportamento nuovo: scrivilo in `src/TIndustry.Shared/Buildings/` o `Power/` e richiamalo dal sistema giusto in `Simulation/Systems/`, rispettando le regole di determinismo;
   - se l'edificio ha stato, includilo in `Factory.Capture`/`Restore` (istantanea e salvataggio) e, cambiando il formato, alza `FactorySaveData.CurrentVersion`.
6. **Test**: `ContentValidationTests` deve restare verde (icona presente, prerequisiti validi); aggiungi uno scenario in `tests/TIndustry.Shared.Tests/` se c'è logica nuova.

## Test e playtest

`tools/dev/dev.sh` (Windows: `dev.cmd`) fa tutto in un comando: controllo strumenti, build, test (`--test`), import asset se manca la cache, avvio del gioco o del playtest (`--playtest ui|tutorial`). Opzioni in `--help`.

```bash
dotnet build tIndustry.sln -c Release -nologo -v q
dotnet test tests/TIndustry.Shared.Tests -c Release --no-build
tools/dev/playtest.sh ui /tmp/shots-ui          # anche: tutorial
python tools/dev/compare_shots.py <baseline> <nuovi>
```

`tools/dev/playtest.sh` lancia Godot con `TINDUSTRY_PLAYTEST` e un profilo dati temporaneo (`TINDUSTRY_DATA_DIR`), salva gli screenshot e `log.txt` nella cartella indicata ed esce con 0 solo se il log contiene `PLAYTEST OK` e nessun `ERROR`. Il codice dei playtest è `godot/scripts/dev/World.Playtest.cs`; durante la prova gli input reali sono ignorati. `compare_shots.py` confronta due cartelle di screenshot ignorando la zona del contatore FPS.
