# Architettura di tIndustry

Gioco in Godot 4.4 .NET. La simulazione è C# puro in `src/TIndustry.Shared/` (nessuna dipendenza da Godot); il client in `godot/` la disegna e raccoglie l'input.

## Mappa delle cartelle

```
tIndustry/
├─ CLAUDE.md · README.md · CHANGELOG.md · tIndustry.sln
├─ data/                        unica fonte dei contenuti: content.json, campaign.json
├─ docs/
│  ├─ ARCHITETTURA.md           questo file
│  ├─ decisioni/                ADR (0001-server-csharp.md)
│  └─ superpowers/              specifiche e piani
├─ src/TIndustry.Shared/        simulazione (futuro codice comune a client e server)
│  ├─ Factory.cs                stato e Tick della fabbrica
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

**Nomi storici.** Alcuni nomi interni conservano ancora la dicitura dei prototipi "spike/slice" e non sono stati rinominati: `Factory.CreateSpikeDemo`, `Factory.BuildSpikeLPath`, `Factory.CreateSandboxSlice`, `Factory.CreateCampaignSlice`, il campo `World._slice` e i metodi `SaveSlice`/`LoadSlice`. Indicano comunque la `Factory` corrente.

## Ciclo di un frame

1. Godot chiama `World._Process` (`godot/scripts/world/World.cs`); l'input è gestito in `World.Input.cs` e diventa piazzamenti/rimozioni (`World.Placement.cs`) sulla `Factory`.
2. `World` chiama `Factory.Tick(dt)` (`src/TIndustry.Shared/Factory.cs`): nastri, minatori, forni/assemblatori, estrattori, potenza, consegna al Core, economia.
3. Se la griglia o gli edifici sono cambiati, `World` ricostruisce i visual dei nastri e degli edifici; poi sincronizza sprite degli oggetti e animazioni (`World.Visuals.cs`, `godot/scripts/visuals/`).
4. `World.UpdateHud` chiama `Hud.UpdateStock`, che inoltra a `CorePanel` (fondi, energia, risorse); `BuildPalette` e `BlockInfoPanel` seguono selezione e sblocchi.
5. Controllo di completamento della campagna e `QueueRedraw` (`World.Drawing.cs` disegna terreno e griglia).

Con la Home aperta o senza `Factory` il frame salta i passi 2–5.

## Dati

`data/content.json` (strutture, oggetti, ricette, ricerca, mercato) e `data/campaign.json` (livelli e obiettivi) sono l'unica fonte. In sviluppo il gioco legge `data/` direttamente (`World.ResolveContentPath` prova `res://../data/` per primo). All'export `tools/ci/export-godot.sh` copia i due file in `godot/data/` perché finiscano nel pacchetto; quella copia non va committata.

`ContentValidationTests` controlla id unici, prerequisiti esistenti e senza cicli, icone presenti per strutture e oggetti, obiettivi di campagna su id noti.

## Salvataggi e impostazioni

`GameDataPaths.Root` (`src/TIndustry.Shared/Save/GameDataPaths.cs`) è la radice dei dati del giocatore: `%LOCALAPPDATA%/tIndustry` (Linux `~/.local/share/tIndustry`), oppure la cartella indicata da `TINDUSTRY_DATA_DIR`. Sotto: `godot-saves/` (salvataggi), `campaignProgress.json`, `settings.json` (`ClientSettings`).

Il salvataggio è `FactorySaveData` / `FactorySaveStore` (`src/TIndustry.Shared/Save/FactorySave.cs`), **versione 9**. Un file di versione precedente è rifiutato con "Salvataggio di una versione precedente: non compatibile."; un file illeggibile con "Salvataggio non leggibile."; in entrambi i casi la partita corrente non cambia. Cambiare il formato richiede di alzare `CurrentVersion`.

## Come aggiungere un blocco

1. **`data/content.json`**: aggiungi la struttura (id, costi, prerequisiti per la ricerca, ricetta/parametri). Se serve un nuovo oggetto, aggiungilo anche lì (e al mercato se vendibile).
2. **Icona** in `godot/assets/` (PNG 64×64, nome = id); aggiungi la riga in `godot/assets/ATTRIBUTION.md` con autore e licenza.
3. **Palette**: aggiungi la voce in `BuildPalette` (`godot/scripts/ui/hud/BuildPalette.cs`) nella categoria giusta.
4. **Visual** in `godot/scripts/visuals/buildings/` (o riusa uno esistente) e collegalo in `World.Visuals.cs`.
5. **Simulazione**: se il comportamento è nuovo, aggiungilo in `src/TIndustry.Shared/Buildings/` o `Power/` e richiamalo da `Factory`.
6. **Test**: `ContentValidationTests` deve restare verde (icona presente, prerequisiti validi); aggiungi uno scenario in `tests/TIndustry.Shared.Tests/` se c'è logica nuova.

## Test e playtest

```bash
dotnet build tIndustry.sln -c Release -nologo -v q
dotnet test tests/TIndustry.Shared.Tests -c Release --no-build
tools/dev/playtest.sh ui /tmp/shots-ui          # anche: tutorial
python tools/dev/compare_shots.py <baseline> <nuovi>
```

`tools/dev/playtest.sh` lancia Godot con `TINDUSTRY_PLAYTEST` e un profilo dati temporaneo (`TINDUSTRY_DATA_DIR`), salva gli screenshot e `log.txt` nella cartella indicata ed esce con 0 solo se il log contiene `PLAYTEST OK` e nessun `ERROR`. Il codice dei playtest è `godot/scripts/dev/World.Playtest.cs`; durante la prova gli input reali sono ignorati. `compare_shots.py` confronta due cartelle di screenshot ignorando la zona del contatore FPS.
