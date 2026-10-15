# Riordino del progetto tIndustry — design

- **Data:** 2026-10-15
- **Stato:** proposta, da approvare
- **Sotto-progetto:** 1 di 5 (vedi [Roadmap](#roadmap-dei-sotto-progetti))

## Perché

Il progetto è cresciuto per strati (prototipo Raylib → spike Godot → client ufficiale) e porta i segni di ogni fase: nomi che non dicono più la verità, codice e asset morti, dati duplicati, 35 script in una sola cartella. Obiettivi concordati:

1. **Capirlo a colpo d'occhio** — nomi veri, una cartella per area.
2. **Togliere zavorra** — codice morto, avanzi Cursor/Raylib, duplicati.
3. **Pronto per crescere** — base pulita per costruzione in corso, drone del giocatore e multiplayer.
4. **Lavorare bene con agenti AI** — architettura e regole scritte nel repo.

## Vincoli e decisioni prese

- **Comportamento di gioco invariato.** Il riordino non cambia meccaniche, bilanciamento o grafica. Ogni passo è verificato con build, test e playtest.
- **Salvataggi vecchi: non vincolanti.** Il formato può cambiare; la versione del salvataggio sale e un file vecchio produce un messaggio chiaro invece di un errore muto.
- **Modalità di cattura `TINDUSTRY_CAPTURE`: eliminate tutte.** Lo strumento per screenshot e verifica visiva sono i playtest scriptati (`TINDUSTRY_PLAYTEST`).
- **Server multiplayer in C#, riusando `TIndustry.Shared`; Rust scartato.** Motivo: client e server eseguono lo stesso codice di simulazione, quindi il client può prevedere il proprio drone e riconciliarsi con le istantanee del server (approccio Mindustry: un solo linguaggio per client e server). Rust imporrebbe di scrivere la simulazione due volte o un client "muto" e laggoso, più un secondo toolchain, per un guadagno di prestazioni non misurato. Il server sarà un'app .NET headless senza Godot. Va registrato come ADR (vedi [Documentazione](#documentazione)).
- **La suddivisione di `Factory` in sistemi NON fa parte di questo riordino**: va progettata insieme a comandi e istantanee (sotto-progetto 2). Qui si rinomina soltanto.

## Struttura di destinazione

```
tIndustry/
├─ CLAUDE.md
├─ README.md · CHANGELOG.md · tIndustry.sln
├─ docs/
│  ├─ ARCHITETTURA.md
│  ├─ decisioni/0001-server-csharp.md
│  └─ superpowers/specs/…
├─ data/                       unica fonte: content.json, campaign.json
├─ src/TIndustry.Shared/       simulazione (comune a client e futuro server)
│  ├─ Content/     ContentTypes, FactoryContent, ConveyorContent, MarketCatalog, ItemVisualColors
│  ├─ Grid/        GridTypes, BeltGrid, BeltLane, TransportedItem, TerrainMap
│  ├─ Buildings/   Miner, Crafter, Extractor, BuildingIo, CoreStockSink
│  ├─ Power/       Generator, PowerNode
│  ├─ Economy/     EconomyWallet, EconomySession
│  ├─ Research/    ResearchState, TechTreeLayout
│  ├─ Campaign/    CampaignCatalog, CampaignProgress
│  ├─ Save/        FactorySave (ex FactorySliceSave), GameDataPaths
│  └─ Factory.cs   (ex FactorySlice)
├─ godot/
│  ├─ project.godot · export_presets.cfg · TIndustry.Godot.csproj
│  ├─ scenes/Main.tscn         (ex Spike.tscn)
│  ├─ shaders/                 belt_scroll, belt_corner_v2
│  ├─ assets/                  unica copia delle icone + ATTRIBUTION.md
│  └─ scripts/
│     ├─ world/     World.cs + World.{Input,Placement,Session,Drawing,Visuals,Panels}.cs, WorldCamera.cs
│     ├─ visuals/   belts/ (MindustryBeltVisual, ScrollingBeltStrip, BeltCornerTile, BridgeSpanVisual)
│     │             buildings/ (MinerVisual, CrafterVisual*, GeneratorVisual, ExtractorVisual, BuildingPad)
│     │             items/ (ItemPalette, ItemSilhouette)
│     ├─ ui/        hud/ (Hud, CorePanel, BuildPalette, BlockInfoPanel)
│     │             panels/ (Home, Saves, Settings, Tutorial, Mercato, Research, TechTreeCanvas, CampaignSelect)
│     ├─ settings/  ClientSettings
│     └─ dev/       World.Playtest.cs, SystemMonitor
├─ tests/TIndustry.Shared.Tests/
└─ tools/          ci/, gen_structure_blocks.py
```

\* `SmelterVisual` e `AssemblerVisual` restano due classi distinte se la grafica differisce; il nome segue l'edificio mostrato, non la classe di simulazione.

Namespace: `TIndustry.Shared` e `TIndustry.Godot` restano; non si introducono sotto-namespace per cartella (le cartelle bastano a orientarsi e si evita di toccare ogni `using`).

## Rinomine

| Prima | Dopo | Note |
|---|---|---|
| `SpikeWorld` (+ partial) | `World` | file `World*.cs` |
| `SpikeCamera` | `WorldCamera` | |
| `scenes/Spike.tscn` | `scenes/Main.tscn` | aggiornare `run/main_scene` in `project.godot` |
| `FactorySlice` | `Factory` | anche `FactorySliceSave*` → `FactorySave*`, `FactorySliceScenarios` → `FactoryScenarios` |
| `SmelterStub` | `Crafter` | usato per forno e assemblatore |
| `GeneratorStub` · `ExtractorStub` · `PowerNodeStub` | `Generator` · `Extractor` · `PowerNode` | |
| `MinerProducer` | `Miner` | |
| `StructureDefinition.Requires` | `Prerequisites` | stesso nome del campo JSON `prerequisites` |
| `FactoryHud` | `Hud` + componenti | vedi [Divisione dell'HUD](#divisione-dellhud) |

Le rinomine si fanno con il refactoring del compilatore (rinomina simbolo + `git mv`), non con sostituzioni di testo, per non toccare stringhe e commenti per sbaglio. I commenti storici che citano "Raylib parity" restano.

## Eliminazioni

| Cosa | Perché | Verifica che sia morto |
|---|---|---|
| `SpikeWorld.Capture.cs` (~1.800 righe) | Catture Cursor-era e controlli pixel dei prototipi dei nastri; sostituite dai playtest | `CaptureDirectory()` si sposta nel playtest; nessun altro riferimento |
| Rami `TINDUSTRY_CAPTURE` in `World._Ready` e `BeginTutorialIfNeeded` | Servivano solo alle catture | build |
| `StaticMiner`, `StaticAssembler`, `StaticSmelter` + nodo `Miner` in `Spike.tscn` | Non usati (il nodo veniva cancellato a runtime) | grep: solo `Spike.tscn` |
| `assets/` in radice (icone, font DejaVu, `thumbnail.png`) | Copie delle icone già in `godot/assets`; font e miniatura erano di Raylib | grep: nessun riferimento |
| `.cursor/` | Configurazione degli agenti Cursor, non più usati | — |
| `shaders/belt_corner.gdshader` | Solo ripiego di `belt_corner_v2` | `BeltCornerTile` carica v2 |
| `godot/data/` da git | Copia di `data/`; l'export la rigenera già (`tools/ci/export-godot.sh`) | aggiunto a `.gitignore` |

`godot/assets/ATTRIBUTION.md` eredita il contenuto di `assets/ATTRIBUTION.md`, con i percorsi aggiornati.

**Da spostare:** `SystemMonitor` (CPU/RAM/GPU per l'overlay risorse) esce da `TIndustry.Shared` e va in `godot/scripts/dev/`: è codice del client, e la simulazione (che diventerà comune al server) non deve dipendere da contatori del sistema operativo.

**Da correggere, non eliminare:** `tools/gen_structure_blocks.py` usa il percorso fisso `/workspace/godot/assets`: diventa relativo alla posizione dello script.

## Divisione dell'HUD

`FactoryHud` (~1.640 righe) diventa:

- `Hud` — radice: crea i componenti, inoltra eventi a `World`, toast.
- `CorePanel` — colonna in alto a sinistra: pulsanti Menu/Ricerca/Mercato/Campagna, Fondi, energia, risorse, Potenzia CORE, obiettivi.
- `BuildPalette` — categorie, griglia dei blocchi, stato bloccato/selezionato.
- `BlockInfoPanel` — nome, tasto rapido, costi, dettaglio `?`.

API pubblica verso `World` invariata (stessi metodi ed eventi su `Hud`), così il cambio resta locale all'HUD.

## Test aggiunti

- **`ContentValidationTests`** su `data/content.json` e `data/campaign.json`: id unici; prerequisiti esistenti e senza cicli; ogni struttura e ogni oggetto ha un'icona in `godot/assets`; obiettivi della campagna riferiti a oggetti/strutture esistenti.
- **Salvataggio da zero:** crea partita → salva → ricarica → stato equivalente; un salvataggio con versione precedente viene rifiutato con messaggio chiaro.

I 44 test esistenti restano verdi a ogni passo.

## Documentazione

- **`docs/ARCHITETTURA.md`** — mappa delle cartelle; ciclo di un frame (input → `Factory.Tick` → grafica → HUD); dove stanno contenuti, salvataggi, impostazioni (`GameDataPaths`, `TINDUSTRY_DATA_DIR`); come aggiungere un blocco nuovo passo per passo; come lanciare test e playtest.
- **`docs/decisioni/0001-server-csharp.md`** — la decisione sul server (contesto, opzioni C#/Rust, scelta, conseguenze).
- **`CLAUDE.md`** — per le sessioni con agenti: comunicazione in italiano; CI solo manuale o in release; comandi di build/test/playtest con profilo dati isolato; mai toccare i dati reali del giocatore; convenzioni di nomi e cartelle; puntatori ad `ARCHITETTURA.md` e alle decisioni.
- README e `godot/README.md` aggiornati ai nuovi nomi e percorsi.

## Sequenza di PR

Ogni PR è indipendente, parte da `main` aggiornato e viene mergiata prima della successiva.

1. **Eliminazioni** — catture, classi morte, `assets/` in radice, `.cursor/`, shader di ripiego.
2. **Rinomine** — classi, scena, `Prerequisites`, file di test.
3. **Cartelle** — script Godot in `world/ visuals/ ui/ settings/ dev/`; Shared in sottocartelle.
4. **HUD diviso** in `Hud`, `CorePanel`, `BuildPalette`, `BlockInfoPanel`.
5. **Dati unici + validazione** — `godot/data/` fuori da git, `ContentValidationTests`, test del salvataggio, versione del salvataggio aumentata.
6. **Documentazione** — `ARCHITETTURA.md`, ADR 0001, `CLAUDE.md`, README.

**Verifica di ogni PR** (nessuna PR senza tutte le voci):
- `dotnet build tIndustry.sln -c Release` → 0 errori, 0 avvisi
- `dotnet test` → tutti verdi
- `godot4 --headless --path godot --import` senza errori di riferimenti mancanti
- playtest `ui` e `tutorial` → `PLAYTEST OK`, 0 errori nel log
- screenshot dei playtest confrontati con quelli prima del cambio (nessuna differenza visiva attesa, salvo PR 4 dove l'HUD deve risultare identico)
- dati reali del giocatore non toccati
- CI `build-publish` lanciata a mano → verde

## Rischi

- **Spostare script in Godot rompe i riferimenti delle scene.** Godot 4.4 usa gli UID (`*.cs.uid`): spostare file e `.uid` insieme con `git mv` e verificare con `--import`. `Main.tscn` riferisce `World.cs` per percorso: va aggiornato.
- **Rinomine via testo che toccano stringhe JSON o messaggi.** Mitigato con rinomina simbolica e revisione del diff.
- **`godot/data` assente in sviluppo locale.** `ResolveContentPath` legge già `res://../data/` per primo; il test di avvio lo copre.
- **Rumore di fine riga sui `.import`** dopo `--import`: ripristinare i file che differiscono solo per fine riga prima del commit.

## Fuori portata

Suddivisione di `Factory` in sistemi, comandi, istantanee, server, drone, costruzione in corso, combat: sotto-progetti successivi.

## Roadmap dei sotto-progetti

1. **Riordino** (questo documento).
2. **Simulazione a comandi e sistemi** — `Factory` diviso in sistemi; azioni del giocatore come comandi applicati al tick; stato come istantanea serializzabile; single player come "server locale" nello stesso processo.
3. **Costruzione in corso** — piani di costruzione, blocchi con avanzamento e consumo graduale di risorse, decostruzione con rimborso.
4. **Drone del giocatore** — unità volante controllata dal giocatore, raggio di costruzione; costruisce i piani.
5. **Server e rete** — server .NET headless autoritativo, trasporto, sincronizzazione, più droni, predizione lato client.
