# Architettura di tIndustry

Gioco in Godot 4.4 .NET. La simulazione è C# puro in `src/TIndustry.Shared/` (nessuna dipendenza da Godot); il client in `godot/` la disegna e raccoglie l'input.

## Mappa delle cartelle

```
tIndustry/
├─ CLAUDE.md · README.md · CHANGELOG.md · tIndustry.sln
├─ data/                        unica fonte dei contenuti: content.json, campaign.json
├─ docs/
│  ├─ ARCHITETTURA.md           questo file
│  ├─ diario/                   diario di lavoro: un file per piano completato
│  ├─ decisioni/                ADR (0001-server-csharp.md, 0002-sincronizzazione-ibrida.md, 0003-costruzione-come-mindustry.md)
│  └─ superpowers/              specifiche e piani
├─ src/TIndustry.Shared/        simulazione (futuro codice comune a client e server)
│  ├─ Factory.cs                facciata: piazzamento, economia, Capture/Restore; Step() → Simulation.Step
│  ├─ Simulation/               SimulationClock, GameState, Simulation, Systems/ (otto sistemi), Commands, SimEvents, SimHost, IGameView, Snapshot, NewGame
│  ├─ Construction/             GameRules, BuildSpec, BuilderUnit (drone e BuildPlan), ConstructSite, Construction (formula, prelievo, rimborso)
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
│  ├─ shaders/                  construct (cantieri)
│  ├─ assets/                   icone (cartella piatta; ui/ per quelle dell'interfaccia) + ATTRIBUTION.md
│  └─ scripts/
│     ├─ world/                 World + partial (Input, Placement, Session, Drawing, Visuals, Panels), WorldCamera
│     ├─ visuals/belts/         MindustryBeltVisual, ScrollingBeltStrip, BeltCornerTile, BridgeSpanVisual
│     ├─ visuals/buildings/     MinerVisual, SmelterVisual, AssemblerVisual, GeneratorVisual, ExtractorVisual, BuildingPad
│     ├─ visuals/items/         ItemPalette, ItemSilhouette
│     ├─ visuals/construction/  ConstructionLayer (piani, cantieri, droni), DroneVisual
│     ├─ ui/hud/                Hud, CorePanel, BuildPalette, BlockInfoPanel, BuildQueuePanel, HudStyle
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

La simulazione è guidata da comandi, a tick fisso, ed è deterministica (modello di sincronizzazione: [ADR 0002](decisioni/0002-sincronizzazione-ibrida.md)).

- **Tick fisso.** `SimulationClock` (`src/TIndustry.Shared/Simulation/SimulationClock.cs`) trasforma il tempo di frame in tick da 1/60 s: `TickRate = 60`, `TickSeconds = 1f / 60f`, `MaxTicksPerFrame = 5` (oltre il tetto il tempo in eccesso è scartato, niente spirale di recupero). La simulazione non vede mai il tempo del frame.
- **`GameState`** (`Simulation/GameState.cs`) contiene tutto lo stato mutabile: griglia e nastri, liste di edifici, portafoglio, ricerca, mercato, sessione economica, potenza, livello di campagna, auto-vendita, upgrade del CORE, contatori di id (`NextEntityId`, `NextItemId`) e `CurrentTick`. `Factory` ne è la facciata (piazzamento, economia, `Capture`/`Restore`) e implementa `IGameView`.
- **Sistemi.** `Simulation.Step(state)` (`Simulation/Simulation.cs`) avanza esattamente un tick eseguendo `Simulation.Systems` (cartella `Simulation/Systems/`) in quest'ordine: `BuilderSystem` → `ConstructSystem` → `BeltSystem` (avanzamento) → `PowerSystem` → `MiningSystem` → `CraftingSystem` (forni e assemblatori) → `ExtractionSystem` → `BeltSystem` (secondo passaggio, passo zero) → `CoreDeliverySystem`; poi incrementa `CurrentTick`. `SystemOrderTests` blocca l'ordine.
- **Comandi.** Ogni modifica al gioco è un record `SimCommand` (`Simulation/Commands.cs`) con `PlayerId` e `Tick`: `PlaceBelt`, `PlaceBuilding` (con `BuildingKind`), `Remove`, `UnlockStructure`, `Sell`, `SetAutoSell`, `UpgradeCore`, `SetSorterFilter`. Sono **validati all'applicazione**, non all'invio. Con la costruzione vera `PlaceBelt` e `PlaceBuilding` accodano un piano e `Remove` cancella o smonta (sezione [Costruzione](#costruzione)).
- **`SimHost`** (`Simulation/SimHost.cs`) è il "server locale": `Submit(cmd)` mette in coda il comando (con `Tick == 0` vale "il prossimo tick"); `Advance(frameSeconds)` esegue i tick dovuti e, all'inizio di ciascuno, applica i comandi di quel tick in ordine di arrivo, poi chiama `Factory.Step()`. L'esito è un evento: `CommandApplied` oppure `CommandRejected` con il motivo in italiano (`CommandReasons`), a stato invariato. Gli eventi che nascono durante il tick (`SimEvent`, in `Simulation/SimEvents.cs`) arrivano dopo `Step()` con `EventRaised`. Un gestore che lancia non interrompe la simulazione (eccezioni raccolte in un `AggregateException` a fine `Advance`). `Pending` espone in sola lettura i comandi in coda (il client la usa per calcolare il valore "effettivo" di un interruttore premuto due volte nello stesso tick), `LastCommandMoneyDelta` il guadagno del solo comando che `CommandApplied` sta riportando (ogni vendita mostra il proprio) e `AdvanceTicks(n)` esegue esattamente `n` tick, senza orologio.
- **`IGameView`** (`Simulation/IGameView.cs`) è la vista in sola lettura che il client usa per disegnare e per l'HUD (`SimHost.State`): il client non può mutare il gioco, può solo inviare comandi. Oggi questo vale per convenzione: `IGameView` espone ancora entità concrete (`BeltGridCell.SetFilterItem`, `Miner`, `Crafter`, `MarketCatalog`); il confine di sola lettura a livello di tipo arriva col sotto-progetto 5.
- **Istantanea e checksum.** `SimHost.CreateSnapshot()` produce l'istantanea canonica (`Snapshot.Write`: JSON compatto, edifici in ordine di id, celle dei nastri per (Y, X), collezioni con chiave in ordine ordinale); `SimHost.FromSnapshot` riparte da essa (coda comandi vuota, orologio nuovo). `SimHost.Checksum()` è FNV-1a a 64 bit sui byte dell'istantanea (`Snapshot.Checksum`). L'istantanea contiene solo lo stato applicato (niente coda dei comandi né resto dell'orologio) ed è valida ai confini di tick. La cadenza "ogni 60 tick" è decisa ma **non ancora implementata**: oggi il checksum si calcola a richiesta, il confronto periodico arriva con il sotto-progetto 5.
- **Nuove partite.** `NewGame.Sandbox` / `NewGame.Campaign` / `NewGame.Demo` (`Simulation/NewGame.cs`) costruiscono lo stato iniziale; le risorse iniziali fanno parte della creazione, non sono comandi.

## Regole di determinismo

Il codice di `src/TIndustry.Shared/` che gira in `Step()` le rispetta; i test `DeterminismTests`, `SnapshotTests` e `SystemOrderTests` le fanno valere:

1. `Step()` non riceve tempo: ogni durata usa `TickSeconds` o conteggi di tick.
2. Mai dipendere dall'ordine di `Dictionary`/`HashSet`: si itera per id crescente o con ordinamento esplicito.
3. Niente orologio di sistema e niente `Random` non seminato (un eventuale generatore vive nello stato con il seme salvato).
4. Niente parallelismo nella simulazione; niente conversioni testo↔numero dipendenti dalla cultura (formati invarianti).
5. Nessun riferimento a Godot o al sistema operativo in `TIndustry.Shared` ([ADR 0001](decisioni/0001-server-csharp.md)).

I calcoli restano in `float`: le differenze residue tra architetture CPU si rilevano col checksum e si riparano con l'istantanea ([ADR 0002](decisioni/0002-sincronizzazione-ibrida.md)).

## Costruzione

Come in Mindustry, piazzare un blocco non lo crea subito: diventa un piano che il drone del giocatore costruisce nel tempo, prelevando dal Core man mano. Decisione e scelte di attuazione: [ADR 0003](decisioni/0003-costruzione-come-mindustry.md). Il codice sta in `src/TIndustry.Shared/Construction/` e nei sistemi `BuilderSystem` e `ConstructSystem`.

- **Regole della partita.** `GameRules` (`Construction/GameRules.cs`, in `GameState.Rules`) ha `RefundPercent` (0–100) e `InstantBuild`. La partita normale (`NewGame.Sandbox`) mette 100, `NewGame.Campaign` il `refundPercent` del livello (`campaign.json`: livelli 1–3 al 100%, dal 4 al 50%). `InstantBuild` è falso nel gioco e vero in `NewGame.Demo`. Con `InstantBuild` i comandi di piazzamento e `Remove` funzionano come prima: blocco posato e pagato nel tick del comando, rimozione istantanea con rimborso.
- **Spec e piani.** `BuildSpec` (`Construction/BuildSpec.cs`) dice cosa costruire e dove: id della struttura, `BuildingKind` (null per un nastro), origine, direzione, ricetta e filtro. Un `BuildPlan` (`Construction/BuilderUnit.cs`) è uno spec più `Breaking`; un piano di smontaggio indica la cella cliccata. `PlaceBuilding` e `PlaceBelt` passano da `SimHost.QueuePlan`, che controlla sblocco e celle libere e accoda il piano. Le celle non devono avere blocchi, nastri, cantieri o piani di costruzione di qualunque giocatore (`Factory.CanPlace`, `Factory.IsPlannedTile`). Le risorse non si controllano.
- **Drone.** `BuilderUnit` ha posizione `float` in tile (il centro), rotazione, la coda `Plans` (in sola lettura; la lista viva è interna) e `CurrentSiteId`. Ogni partita nuova ha il drone del giocatore 0 al centro del Core (`Factory.AddBuilder`). I valori vengono da `content.json` → `units.drone` (`FactoryContent.Drone`): `buildSpeed` 0,5, `buildRange` 27,5 tile, `speed` 22,5 tile/s.
- **`BuilderSystem`.** Per ogni drone, in ordine di id. Senza cantiere e con piani, vola in linea retta verso il centro del primo piano (`Factory.PlanCentre`); entro `buildRange − 1` rivalida sblocco e celle e chiama `Construction.BeginBuild`, oppure scarta il piano con `PlanDropped` ("Cella occupata o non valida" o "Bloccato dalla ricerca"). Un piano di smontaggio chiama `Construction.BeginDeconstruct`, o sparisce in silenzio se il blocco non c'è più. Con la coda vuota il drone torna al punto di riposo (`Factory.BuilderRestPoint`: una tile a ovest del Core, a metà del lato). Con un cantiere resta entro raggio dal suo centro (`Factory.SiteCentre`).
- **Cantieri.** `ConstructSite` (`Construction/ConstructSite.cs`) occupa le celle del blocco finale (`Tiles`; per un ponte i due estremi) e non lavora: niente produzione, niente oggetti dai nastri, niente energia. Ha `Cost` (prima il denaro, poi i materiali in ordine di id), `Progress` 0–1, `Spent` e `Refunded` per voce (in sola lettura; i valori vivi sono interni), `Deconstructing`, `RefundBasisPoints`, `Missing` (la voce che ha fermato l'ultimo tick, la più limitante, e quanto manca per finire) e `StalledTicks` (tick di fila fermo mentre il drone ci lavora).
- **Formula.** `Construction.BuildCost` vale `(20 + Σ quantità × buildWeight) × buildCostMultiplier`. I pesi sono in `content.json` → `buildWeights` (`iron-plate` 0,7, `copper-wire` 0,5, `copper-ore` 0,5, gli altri 1,0); `buildCostMultiplier` è per struttura, nelle voci di `buildings[]` e `conveyors[]` di `content.json` (1 se manca). Il denaro non entra nel tempo. Ogni tick `Δprogress = buildSpeed / buildCost`: un `conveyor-basic` richiede circa 42 tick.
- **Prelievo e rimborso (`Construction.Advance`).** Costruendo, il progresso del tick è limitato da ciò che il Core può ancora pagare per ogni voce. Per ogni voce si preleva fino a `ceil(progress × quantità)`: ogni unità si paga prima di costruire la sua fetta. Senza risorse il cantiere resta fermo (`Missing` impostato) e riparte da solo. Se resta fermo per `Construction.StuckTicks` (60 tick, un secondo) e in coda ci sono altri piani, il suo piano va in fondo alla coda e il drone passa al successivo (il `plan.stuck` di Mindustry); il cantiere resta sulla mappa con progresso e speso, e quando il piano torna in testa `BuilderSystem` lo riprende (`Factory.SiteOfPlan`). Un piano fermo da solo aspetta. A progresso 1 il cantiere sparisce e `Factory.PlaceInstant` posa il blocco vero, con un id nuovo e senza pagare di nuovo. Smontando, il progresso scende alla stessa velocità e per ogni voce rientra `floor(Spent × RefundBasisPoints / 10000) − ceil(progress × quantità × RefundBasisPoints / 10000)`; a 0 il cantiere sparisce. Un edificio finito rimborsa la regola della partita × il `refundPercent` dell'edificio (`Factory.RefundBasisPointsFor`). Un cantiere annullato (`Construction.Cancel`) rimborsa il 100% di quanto speso, man mano da dove era arrivato. Gli oggetti di un blocco smontato si perdono, come in Mindustry.
- **`ConstructSystem`.** Per ogni cantiere, in ordine di id, il drone che ci lavora lo fa avanzare se è entro `buildRange` dal suo centro. Un cantiere finito toglie il primo piano dalla coda e alza `ConstructFinished(siteId, entityId)` (entityId 0 per i nastri) o `DeconstructFinished(siteId)`.
- **`Remove`** (`SimHost.ApplyRemove`), in quest'ordine: cancella il primo piano del giocatore non ancora avviato che copre la cella (un piano di smontaggio copre tutto il blocco, quindi si toglie da qualunque sua cella; un piano con un cantiere già in piedi passa al punto dopo); annulla un cantiere in costruzione (anche uno lasciato fermo in fondo alla coda: il drone lo smonta quando il suo piano torna in testa); su un cantiere in smontaggio non fa nulla; accoda un piano di smontaggio del blocco o nastro (una volta sola per blocco); su una cella vuota rifiuta in silenzio. Con `Remove.OnlyMark` (le celle di un trascinamento) i piani di smontaggio non si cancellano mai: due trascinamenti sovrapposti tengono tutti i segni.
- **Nastri accanto al Core.** `PlaceBelt` accoda il piano già con la direzione verso il Core (`CoreStockSink.PreferDirectionIntoCore`), la stessa che il nastro finito prenderebbe: piano, sagoma, cantiere e blocco finito coincidono.
- **Eventi.** `PlanDropped`, `ConstructFinished` e `DeconstructFinished` (`Simulation/SimEvents.cs`) si raccolgono in `GameState.Events` durante il tick. `SimHost` li consegna con `EventRaised` dopo `Step()` e svuota la lista. Non fanno parte dell'istantanea.
- **Vista.** `IGameView` espone `Builders`, `Sites`, `TryGetSiteAt`, `PlansOf`, `PlanTiles` e `IsPlannedTile`.
- **Client.** `ConstructionLayer` (`godot/scripts/visuals/construction/`) disegna in un solo nodo, sopra edifici e oggetti, tre cose. I piani del giocatore sono sagome semitrasparenti col contorno giallo (`Accent`), rosse (`Remove`) se di smontaggio. I cantieri usano lo shader `godot/shaders/construct.gdshader`: lo sprite si rivela dal basso con una linea luminosa, in rosso e al contrario nello smontaggio, con la linea più lenta se il cantiere è fermo. I droni sono `DroneVisual`, con il raggio verso il cantiere. `BuildQueuePanel` mostra la coda nella colonna in alto a sinistra dell'HUD. I piani di smontaggio hanno un riempimento rosso traslucido, come in Mindustry; i piani con un cantiere già in piedi sono mostrati dal cantiere. Un clic destro senza movimento invia, al rilascio, un `Remove` che alterna il segno (`World.TryRemoveAt`); trascinando, `World.RemoveOnceInDrag` invia `Remove` con `OnlyMark` una sola volta per blocco, a partire dalla prima cella. Fra due movimenti del mouse i trascinamenti (piazzamento e smontaggio) riempiono le celle saltate con una linea di Bresenham (`World.DragCells`). Il tooltip di un cantiere fermo dice "Mancano: N oggetto" (o "Mancano: $N"); la cella sotto il mouse si ricalcola a ogni frame dalla telecamera (`World.SyncHover`) e il tooltip si nasconde sopra l'HUD. `BuildQueuePanel` colora d'ambra la barra di un cantiere fermo e scrive "Manca: <materiale>".

## Ciclo di un frame

1. Godot chiama `World._Process` (`godot/scripts/world/World.cs`). L'input non passa da `_Process`: arriva dai callback di Godot (`World._UnhandledInput` in `World.Input.cs`) e diventa **comandi** (`World.Placement.cs` → `World.SubmitCommand` in `World.Commands.cs` → `SimHost.Submit`).
2. `_Process` chiama `World.AdvanceHost(delta)` (`World.Commands.cs`), che chiama `SimHost.Advance(delta)`: l'orologio restituisce quanti tick fissi eseguire (al massimo 5); a ogni tick l'host applica i comandi in coda e chiama `Factory.Step()` → `Simulation.Step`. Gli eventi `CommandApplied` / `CommandRejected` e quelli della simulazione (`EventRaised`: `PlanDropped`, `ConstructFinished`, `DeconstructFinished`) arrivano a `World` (toast, aggiornamento di visual e pannelli).
3. `World.SyncConstructionVisuals` aggiorna il livello della costruzione (`ConstructionLayer.Sync`: piani, cantieri, droni) e segnala quali visual dei blocchi rifare. Se la griglia o gli edifici sono cambiati, `World` ricostruisce i visual dei nastri e degli edifici leggendo `IGameView`; poi sincronizza sprite degli oggetti, tooltip del cantiere fermo (`World.SyncSiteTooltip`) e animazioni (`World.Visuals.cs`, `godot/scripts/visuals/`).
4. `World.UpdateHud` chiama `Hud.UpdateStock`, che inoltra a `CorePanel` (fondi, energia, risorse); `BuildPalette` e `BlockInfoPanel` seguono selezione e sblocchi; `Hud.SyncBuildQueue` aggiorna la coda del drone (`BuildQueuePanel`).
5. Controllo di completamento della campagna e `QueueRedraw` (`World.Drawing.cs` disegna terreno e griglia).

Con la Home aperta o senza partita il frame salta i passi 2–5.

## Dati

`data/content.json` (strutture, oggetti, ricette, ricerca, mercato, pesi di costruzione `buildWeights`, drone `units.drone`) e `data/campaign.json` (livelli, obiettivi e `refundPercent` per livello) sono l'unica fonte. In sviluppo il gioco legge `data/` direttamente (`World.ResolveContentPath` prova `res://../data/` per primo). All'export `tools/ci/export-godot.sh` copia i due file in `godot/data/` perché finiscano nel pacchetto; quella copia non va committata.

`ContentValidationTests` controlla id unici, prerequisiti esistenti e senza cicli, icone presenti per strutture e oggetti, obiettivi di campagna su id noti.

## Salvataggi e impostazioni

`GameDataPaths.Root` (`src/TIndustry.Shared/Save/GameDataPaths.cs`) è la radice dei dati del giocatore: `%LOCALAPPDATA%/tIndustry` (Linux `~/.local/share/tIndustry`), oppure la cartella indicata da `TINDUSTRY_DATA_DIR`. Sotto: `godot-saves/` (salvataggi) e `campaignProgress.json`.

Le impostazioni (`settings.json`, `ClientSettings.SettingsDirectory`) seguono un'altra regola: con `TINDUSTRY_DATA_DIR` stanno in `<TINDUSTRY_DATA_DIR>/tIndustry/settings.json`, altrimenti nella cartella utente di Godot (`%APPDATA%\Godot\app_userdata\tIndustry\tIndustry\settings.json`).

Il salvataggio è `SaveFileData` / `FactorySaveStore` (`src/TIndustry.Shared/Save/FactorySave.cs`), **versione 11**: un file JSON con i metadati dell'elenco (fondi, seme, mappa, livello) e l'istantanea canonica (`Snapshot`, in base64; la struttura è `FactorySaveData`). Si scrive con `FactorySaveStore.Save(slot, SimHost)` e si legge con `TryLoad(content, slot, out SimHost?, out error)`; comandi in coda e orologio non sono salvati (il salvataggio è lo stato applicato). Dalla v11 l'istantanea porta anche le regole di partita (`GameRules`), i droni con la coda dei piani e i cantieri (celle, progresso, speso e rimborsato, quota di rimborso, tick da fermo). Speso e rimborsato sono salvati per oggetto (`SpentByItem` / `RefundedByItem`, righe `{itemId, amount}` in ordine ordinale) e al caricamento si agganciano al costo ricalcolato per id, così un cambio di costo nei contenuti non li sposta; i v11 scritti prima di queste righe usano ancora le liste per indice (`Spent` / `Refunded`). Il caricamento valida i cantieri (`Factory.RestoreSite`): progresso in [0, 1], quota in [0, 10000], speso e rimborsato in [0, costo], celle ricalcolate se mancano; un cantiere con uno spec non più costruibile sparisce col suo piano e rende al Core quanto aveva preso; un cantiere corrente inesistente si azzera. La scrittura è atomica: prima `<slot>.json.tmp` nella stessa cartella, poi rinomina sullo slot; i `.tmp` rimasti non compaiono nell'elenco. Un file di versione precedente è rifiutato con "Salvataggio di una versione precedente: non compatibile."; un file illeggibile con "Salvataggio non leggibile."; in entrambi i casi la partita corrente non cambia. Cambiare il formato richiede di alzare `CurrentVersion`.

## Come aggiungere un blocco

1. **`data/content.json`**: aggiungi la struttura (id, costi, prerequisiti per la ricerca, ricetta/parametri). Se serve un nuovo oggetto, aggiungilo anche lì (e al mercato se vendibile). Il tempo di costruzione viene dal costo: un oggetto nuovo pesa 1,0 se non lo metti in `buildWeights`, e il blocco può rallentare o accelerare con `buildCostMultiplier` nella sua voce di `buildings[]` o `conveyors[]` (1 se manca). `refundPercent` della stessa voce è la sua quota di rimborso allo smontaggio.
2. **Icona** in `godot/assets/` (PNG, di norma 64×64; nome = id della struttura); aggiungi la riga in `godot/assets/ATTRIBUTION.md` con autore e licenza. La stessa icona è lo sprite di piani e cantieri (`ConstructionLayer.TextureFor`). Eccezioni: `conveyor-express` usa `conveyor-fast.png` e `conveyor-bridge` usa `bridge.png` (fallback in `TechTreeCanvas.ResolveIcon`, `ConstructionLayer.TextureFor` e `ContentValidationTests.IconFileFor`; le righe della palette puntano direttamente al file).
3. **Strumento di costruzione** (se il blocco è piazzabile dalla palette), da collegare in questi punti:
   - `Hud.ToolKind` e `Hud.StructureIdFor` (`godot/scripts/ui/hud/Hud.cs`): nuova voce dell'enum e mappatura sull'id della struttura;
   - `BuildPalette.cs` (`godot/scripts/ui/hud/`): riga nella tabella della categoria (`ToolsEntries`, `LogisticsEntries`, `ProductionEntries` o `PowerEntries`), `CategoryFor` e nome mostrato (`ToolDisplayFallback`);
   - enum `World.BuildTool` (`godot/scripts/world/World.cs`);
   - `World.Input.cs`: `ToHudTool`, `FromHudTool`, il nome mostrato nel toast e l'eventuale tasto rapido in `_UnhandledInput`;
   - `World.Placement.cs`: ramo nello `switch` del piazzamento, che costruisce il comando (`PlaceBuilding` con il nuovo `BuildingKind`) e lo invia con `SubmitCommand`; grafica e toast di rifiuto arrivano dagli eventi dell'host (`World.Commands.cs`);
   - `World.Session.cs`: aggiungi il tool all'elenco di `SyncResearchLocks`, altrimenti non viene bloccato dalla ricerca;
   - `World.Drawing.cs`: dimensione dell'anteprima (ghost) e, per i blocchi orientabili, l'elenco che mostra la freccia di direzione.
4. **Visual** in `godot/scripts/visuals/buildings/` (o riusa uno esistente): creazione e aggiornamento in `World.Visuals.cs`. I nastri e simili sono disegnati da `MindustryBeltVisual`.
5. **Simulazione** (se il blocco è piazzabile con `PlaceBuilding`; se manca uno di questi punti il piazzamento fallisce in modo fuorviante). Piani, cantieri e piazzamento istantaneo passano tutti da `BuildSpec`, quindi il blocco va descritto lì:
   - `BuildingKind` (`Simulation/Commands.cs`): nuovo valore;
   - `CommandReasons.ToolName(BuildingKind)` (`Simulation/Commands.cs`): il nome mostrato, altrimenti il toast di blocco dice "? bloccato…";
   - `BuildSpec.StructureIdFor` (`Construction/BuildSpec.cs`): mappatura sull'id della struttura. Con `null` il comando è "Comando non valido." e `Factory.IsValidSpec` rifiuta lo spec;
   - `Factory.FootprintSize` (`Factory.cs`): il lato del blocco, se non è 1×1. `Footprint`, `CanPlace` e il centro del piano lo usano;
   - `Factory.PlaceInstant` (`Factory.cs`): un ramo nello `switch` che posa il blocco (pagando o no). Lo chiamano il comando con `InstantBuild` e il cantiere finito; senza ramo cade su `default: return false` e un cantiere completato lancia un'eccezione;
   - `Factory.TryGetSpecAt` (`Factory.cs`): riconosce il blocco su una cella e ne ricostruisce lo `BuildSpec`. Serve a `Remove` (piano di smontaggio), a `Construction.BeginDeconstruct` e a `ConstructFinished`;
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
