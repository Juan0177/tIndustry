# Riordino del progetto — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Riorganizzare tIndustry (nomi veri, cartelle per area, niente codice/asset morti, dati in un solo posto, HUD diviso, documentazione per persone e agenti) senza cambiare il comportamento di gioco.

**Architecture:** Sei PR piccole e indipendenti su `main`, una per task 1–6, ciascuna verificata con build, test, playtest e confronto di screenshot con una baseline catturata nel task 0. Rinomine fatte per simbolo (`git mv` + rinomina dei tipi), non per sostituzione di testo libera.

**Tech Stack:** C# / .NET 8, Godot 4.4.1 .NET, xUnit, GitHub CLI (`gh`).

**Spec:** [2026-10-15-riordino-progetto-design](../specs/2026-10-15-riordino-progetto-design.md)

## Global Constraints

- Comportamento di gioco, bilanciamento e grafica invariati (unica eccezione: messaggio chiaro per salvataggi di versione precedente, task 5).
- Comunicazione e testi in italiano; commenti nel codice in inglese come il resto del repo.
- CI solo manuale: dopo ogni push `gh workflow run build-publish.yml --ref <branch>`; mai aggiungere trigger push/PR.
- Playtest e catture solo con `TINDUSTRY_DATA_DIR` su una cartella temporanea: i dati reali in `%LOCALAPPDATA%\tIndustry` e `%APPDATA%\Godot\app_userdata\tIndustry` non vanno mai toccati.
- Namespace invariati: `TIndustry.Shared`, `TIndustry.Godot`, `TIndustry.Shared.Tests`.
- Commit con `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; PR con `🤖 Generated with [Claude Code](https://claude.com/claude-code)`.
- Dopo `godot4 --import`, ripristinare i `.import` che differiscono solo per fine riga: `for f in $(git ls-files -m godot/assets); do git diff --ignore-cr-at-eol --quiet -- "$f" && git checkout -q -- "$f"; done`.

**Verifica standard** (citata come *VERIFICA* nei task; tutte le voci obbligatorie):
1. `dotnet build tIndustry.sln -c Release -nologo -v q` → `Errori: 0`, `Avvisi: 0`
2. `dotnet test tests/TIndustry.Shared.Tests -c Release --no-build` → `Non superati: 0`
3. `godot4 --headless --path godot --import` poi `godot4 --headless --path godot --build-solutions --quit` → nessuna riga `ERROR` su risorse mancanti
4. Playtest `ui` e `tutorial` (comando nel task 0) → `PLAYTEST OK` e `grep -c ERROR` = 0
5. Screenshot dei playtest confrontati con la baseline: `tools/dev/compare_shots.py <baseline> <nuovi>` → nessuna differenza fuori dalla zona FPS
6. `diff -rq <backup> "$LOCALAPPDATA/tIndustry"` identico; `settings.json` reale con data invariata
7. Push, PR, `gh workflow run build-publish.yml --ref <branch>` → `success`

## Review Focus

- **Riferimenti Godot rotti dopo spostamenti/rinomine** (script in `.tscn`, `run/main_scene`, `res://` di asset): il gioco deve avviarsi e passare i playtest → coperto da VERIFICA 3–4 in task 2 e 3, più il test `MainSceneAndItsScriptsExist` (task 2), che resta valido anche dopo il task 3.
- **Export di release dopo l'uscita di `godot/data` da git e il cambio di scena**: lo zip deve contenere i dati e avviarsi → task 5, step di `release.yml` lanciato a mano senza tag.
- **Salvataggio di una versione precedente**: deve dare il messaggio "Salvataggio di una versione precedente: non compatibile", non un crash → test `OldSaveVersionIsRejectedWithClearMessage` (task 5).
- **Percorsi dei dati del giocatore cambiati per errore dalle rinomine**: impostazioni e salvataggi devono restare dove sono → test `PlayerDataPathsAreUnchanged` (task 2).
- **HUD diverso dopo la divisione**: stesso aspetto e stessi comportamenti → confronto pixel in VERIFICA 5 (task 4) e playtest `ui` che apre Mercato/Ricerca dai pulsanti.

---

### Task 0: Baseline e strumenti di confronto

**Files:**
- Create: `tools/dev/playtest.sh`
- Create: `tools/dev/compare_shots.py`

**Interfaces:**
- Produces: `tools/dev/playtest.sh <ui|tutorial> <outdir>` → exit 0 solo se il log contiene `PLAYTEST OK` e nessun `ERROR`; crea un profilo dati temporaneo per ogni esecuzione. `tools/dev/compare_shots.py <dirA> <dirB>` → exit 0 se per ogni PNG con lo stesso nome la differenza è vuota fuori dal rettangolo FPS `(1180, 0, 1280, 30)`, altrimenti stampa nome + bounding box ed esce 1.

- [ ] **Step 1:** Scrivi `playtest.sh`: imposta `TINDUSTRY_DATA_DIR` su `$(mktemp -d)`, `TINDUSTRY_CAPTURE_DIR` su `<outdir>` (convertiti con `cygpath -w` se disponibile), lancia `godot4 --path godot --resolution 1280x720` con `TINDUSTRY_PLAYTEST=<nome>` e `timeout 150`, salva il log in `<outdir>/log.txt`, applica le due condizioni di uscita sopra.
- [ ] **Step 2:** Scrivi `compare_shots.py` con Pillow (`ImageChops.difference`, maschera nera sul rettangolo FPS).
- [ ] **Step 3:** Backup dei dati reali: `cp -r "$LOCALAPPDATA/tIndustry" "$TEMP/tIndustry-backup-riordino"` e annota la data di `settings.json`.
- [ ] **Step 4:** Baseline da `main`: `tools/dev/playtest.sh ui "$TEMP/baseline/ui"` e `tools/dev/playtest.sh tutorial "$TEMP/baseline/tutorial"` → entrambi exit 0.
- [ ] **Step 5:** Auto-verifica del confronto: `python tools/dev/compare_shots.py "$TEMP/baseline/ui" "$TEMP/baseline/ui"` → exit 0. Rilancia il playtest `ui` in una seconda cartella e confronta: se ci sono differenze dovute ad animazioni (nastri, ingranaggi), aggiungi a `compare_shots.py` le zone mobili note come maschere e annotale nel file; il confronto baseline-vs-baseline deve tornare a 0.
- [ ] **Step 6:** `git switch -c chore/riordino-1-eliminazioni main` e commit dei due script (viaggiano con la prima PR).

---

### Task 1 (PR 1): Eliminazioni

**Files:**
- Delete: `godot/scripts/SpikeWorld.Capture.cs` (+ `.uid`), `godot/scripts/StaticMiner.cs`, `StaticAssembler.cs`, `StaticSmelter.cs` (+ `.uid`), `godot/shaders/belt_corner.gdshader` (+ `.uid`), cartella `assets/` in radice, cartella `.cursor/`
- Create: `godot/assets/ATTRIBUTION.md` (da `assets/ATTRIBUTION.md`, percorsi `assets/icons/…` → `godot/assets/…`, sezione "Fonts" e miniatura Raylib rimosse)
- Modify: `godot/scripts/SpikeWorld.cs` (rami `TINDUSTRY_CAPTURE` righe ~140–236), `SpikeWorld.Panels.cs:376` (guard capture in `BeginTutorialIfNeeded`), `SpikeWorld.Playtest.cs` (ospita `CaptureDirectory()`), `FactoryHud.cs:1466-1475` (rimuovi `OpenBlockDetailForCapture`/`CloseBlockDetailForCapture`), `godot/scenes/Spike.tscn` (rimuovi nodo `Miner`, il suo `Sprite` e l'`ext_resource` di `StaticMiner.cs`), `godot/scripts/SpikeWorld.Visuals.cs` (`EnsureBuildingsLayer`: togli la rimozione del nodo `Miner`), `godot/scripts/BeltCornerTile.cs:75-76` (solo `belt_corner_v2`), `tools/gen_structure_blocks.py` (output relativo), `README.md` e `godot/README.md` (togli `TINDUSTRY_CAPTURE` e link ad `assets/ATTRIBUTION.md`), `.gitignore`

**Interfaces:**
- Produces: `private static string? CaptureDirectory()` resta con firma invariata, ora in `SpikeWorld.Playtest.cs`. Dopo questo task `TINDUSTRY_CAPTURE` e `TINDUSTRY_CAPTURE_MODE` non esistono più; `TINDUSTRY_CAPTURE_DIR` resta (usata dai playtest).

- [ ] **Step 1:** Sul branch `chore/riordino-1-eliminazioni` (creato nel task 0) sposta `CaptureDirectory()` in `SpikeWorld.Playtest.cs`, poi elimina `SpikeWorld.Capture.cs`.
- [ ] **Step 2:** In `SpikeWorld._Ready` rimuovi i quattro rami `TINDUSTRY_CAPTURE*` e il blocco `capture`/`captureMode`: il flusso finale è `TINDUSTRY_FRESH && !PlaytestRequested` → `_home.Close()`, altrimenti `_home.Open()`. Rimuovi il guard capture in `BeginTutorialIfNeeded`.
- [ ] **Step 3:** Rimuovi i metodi `*ForCapture` dell'HUD, le classi `Static*`, il nodo `Miner` dalla scena e la sua pulizia in `EnsureBuildingsLayer`, lo shader di ripiego.
- [ ] **Step 4:** `git mv assets/ATTRIBUTION.md godot/assets/ATTRIBUTION.md`, aggiornane i percorsi; `git rm -r assets .cursor`. Aggiorna i link in `README.md` (sezione Stack: "Icone: `godot/assets/ATTRIBUTION.md`").
- [ ] **Step 5:** `gen_structure_blocks.py`: `OUT = Path(__file__).resolve().parent.parent / "godot" / "assets"`. Verifica con `python -c "import ast,sys;ast.parse(open('tools/gen_structure_blocks.py').read())"`.
- [ ] **Step 6:** Controllo residui: `grep -rn "TINDUSTRY_CAPTURE\b\|TINDUSTRY_CAPTURE_MODE\|StaticMiner\|StaticAssembler\|StaticSmelter\|belt_corner.gdshader\|\.cursor\|assets/icons\|assets/fonts" --include=*.cs --include=*.tscn --include=*.md --include=*.py --include=*.sh --include=*.yml .` → nessun risultato (esclusi `CHANGELOG.md` e `docs/superpowers/`).
- [ ] **Step 7:** VERIFICA 1–6 con `compare_shots.py "$TEMP/baseline/<pt>" <nuovi>` per `ui` e `tutorial`.
- [ ] **Step 8:** Voce CHANGELOG sotto `[Unreleased]` → "### Pulizia"; commit; VERIFICA 7 (PR "chore: riordino 1/6 — eliminazioni").

---

### Task 2 (PR 2): Rinomine

**Files:**
- Rename (con `git mv`, inclusi i `.uid`): `godot/scripts/SpikeWorld*.cs` → `World*.cs`; `SpikeCamera.cs` → `WorldCamera.cs`; `godot/scenes/Spike.tscn` → `godot/scenes/Main.tscn`; `src/TIndustry.Shared/FactorySlice.cs` → `Factory.cs`; `FactorySliceSave.cs` → `FactorySave.cs`; `SmelterStub.cs` → `Crafter.cs`; `GeneratorStub.cs` → `Generator.cs`; `ExtractorStub.cs` → `Extractor.cs`; `PowerNodeStub.cs` → `PowerNode.cs`; `MinerProducer.cs` → `Miner.cs`; `tests/…/FactorySliceScenarios.cs` → `FactoryScenarios.cs`; `FactorySliceSelfTests.cs` → `FactorySelfTests.cs`
- Modify: `godot/project.godot` (`run/main_scene="res://scenes/Main.tscn"`), `Main.tscn` (nome nodo radice `Main`, script `res://scripts/World.cs`, camera `res://scripts/WorldCamera.cs`), `ContentTypes.cs` (`Requires` → `Prerequisites`), tutti i chiamanti
- Create: `tests/TIndustry.Shared.Tests/PlayerDataPathsTests.cs`, `tests/TIndustry.Shared.Tests/GodotProjectReferencesTests.cs`

**Interfaces:**
- Produces (nuovi nomi usati dai task successivi): `World`, `WorldCamera`, `Factory`, `FactorySaveData`, `FactorySaveStore`, `Crafter`, `Generator`, `Extractor`, `PowerNode`, `Miner`, `StructureDefinition.Prerequisites : IReadOnlyList<string>` (nessuna proprietà `Requires`), `FactoryScenarios`.
- Nota: la proprietà record `StructureDefinition.Prerequisites` esiste già come parametro posizionale nullable; diventa `IReadOnlyList<string> Prerequisites` non nullable (default `[]`) e `Requires` sparisce.

- [ ] **Step 1: test che fissa i percorsi dei dati del giocatore** (devono restare uguali dopo le rinomine). In `PlayerDataPathsTests`:

```csharp
[Fact]
public void PlayerDataPathsAreUnchanged()
{
    var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    Assert.Equal(Path.Combine(local, "tIndustry"), GameDataPaths.Root);
    Assert.Equal(Path.Combine(local, "tIndustry", "godot-saves"), FactorySliceSaveStore.SavesDirectory);
    Assert.Equal("continua", FactorySliceSaveStore.ContinueSlotId);
}
```
Il test vive fuori dalla collection `SaveStore` ma deve girare senza `SavesDirectoryOverride`: assicurati che `TempSaveStoreFixture` non sia attiva (metti il test in una collection propria, `DisableParallelization = true`).

- [ ] **Step 2: test che fissa i riferimenti del progetto Godot.** In `GodotProjectReferencesTests`, con la radice del repo trovata risalendo da `AppContext.BaseDirectory` fino a `tIndustry.sln`:

```csharp
[Fact]
public void MainSceneAndItsScriptsExist()
{
    // run/main_scene="res://…" in godot/project.godot → il file esiste
    // ogni [ext_resource type="Script" path="res://…"] della scena principale → il file esiste
}
```
`res://` corrisponde a `<repo>/godot/`.
- [ ] **Step 3:** `dotnet test` → entrambi PASS sul codice attuale (fissano il comportamento esistente prima di rinominare).
- [ ] **Step 4:** Rinomina i tipi e i file Shared + test (tipo per tipo: `git mv` del file, rinomina del tipo e dei riferimenti C#, build dopo ognuno). Aggiorna il test dello step 1 a `FactorySaveStore`.
- [ ] **Step 5:** `Requires` → `Prerequisites` (record non nullable, mapping in `FactoryContent.cs:137` invariato sul JSON).
- [ ] **Step 6:** Rinomina gli script Godot e la scena; aggiorna `project.godot` e i percorsi in `Main.tscn`. Il nodo radice si chiama `Main`; `World` resta lo script sul nodo radice.
- [ ] **Step 7:** Controllo residui: `grep -rnE "Spike(World|Camera)?\b|Spike\.tscn|FactorySlice|SmelterStub|GeneratorStub|ExtractorStub|PowerNodeStub|MinerProducer|\.Requires\b" --include=*.cs --include=*.tscn --include=*.godot --include=*.md godot src tests README.md` → nessun risultato (`CHANGELOG.md` e `docs/superpowers/` esclusi).
- [ ] **Step 8:** VERIFICA 1–6 (confronto con baseline del task 0); `MainSceneAndItsScriptsExist` verde con `Main.tscn`.
- [ ] **Step 9:** CHANGELOG "### Pulizia" (rinomine); commit; VERIFICA 7 (PR "refactor: riordino 2/6 — nomi veri").

---

### Task 3 (PR 3): Cartelle

**Files:**
- Move (con `git mv`, `.uid` inclusi) in `godot/scripts/`:
  - `world/`: `World*.cs` tranne `World.Playtest.cs`, `WorldCamera.cs`
  - `visuals/belts/`: `MindustryBeltVisual.cs`, `ScrollingBeltStrip.cs`, `BeltCornerTile.cs`, `BridgeSpanVisual.cs`
  - `visuals/buildings/`: `MinerVisual.cs`, `SmelterVisual.cs`, `AssemblerVisual.cs`, `GeneratorVisual.cs`, `ExtractorVisual.cs`, `BuildingPad.cs`
  - `visuals/items/`: `ItemPalette.cs`, `ItemSilhouette.cs`
  - `ui/hud/`: `FactoryHud.cs`
  - `ui/panels/`: `HomePanel.cs`, `SavesPanel.cs`, `SettingsPanel.cs`, `TutorialPanel.cs`, `MercatoPanel.cs`, `ResearchPanel.cs`, `TechTreeCanvas.cs`, `CampaignSelectPanel.cs`
  - `settings/`: `ClientSettings.cs`
  - `dev/`: `World.Playtest.cs`
- Move `src/TIndustry.Shared/SystemMonitor.cs` → `godot/scripts/dev/SystemMonitor.cs` (namespace `TIndustry.Godot`)
- Move in `src/TIndustry.Shared/` sottocartelle come da spec: `Content/` (ContentTypes, FactoryContent, ConveyorContent, MarketCatalog, ItemVisualColors), `Grid/` (GridTypes, BeltGrid, BeltLane, TransportedItem, TerrainMap), `Buildings/` (Miner, Crafter, Extractor, BuildingIo, CoreStockSink), `Power/` (Generator, PowerNode), `Economy/` (EconomyWallet, EconomySession), `Research/` (ResearchState, TechTreeLayout), `Campaign/` (CampaignCatalog, CampaignProgress), `Save/` (FactorySave, GameDataPaths); `Factory.cs` resta in radice
- Modify: `Main.tscn` (percorsi `res://scripts/world/World.cs`, `res://scripts/world/WorldCamera.cs`)

**Interfaces:**
- Consumes: nomi del task 2. Produces: nessuna API nuova; solo percorsi. `SystemMonitor` ora è `TIndustry.Godot.SystemMonitor` (stesse membri statici: `Update(float)`, `CpuPercent`, `FormatRam()`, `GpuLabel`).

- [ ] **Step 1:** Sposta i file Shared nelle sottocartelle (il `.csproj` include tutto per glob: nessuna modifica). Build + test.
- [ ] **Step 2:** Sposta `SystemMonitor` nel progetto Godot e cambia il namespace; build: l'unico chiamante (`World.cs`) compila senza `using` nuovi perché è nello stesso namespace.
- [ ] **Step 3:** Sposta gli script Godot e aggiorna i percorsi nella scena.
- [ ] **Step 4:** Controllo: `ls godot/scripts/*.cs` → nessun file in radice; `ls src/TIndustry.Shared/*.cs` → solo `Factory.cs`.
- [ ] **Step 5:** VERIFICA 1–6.
- [ ] **Step 6:** CHANGELOG; commit; VERIFICA 7 (PR "refactor: riordino 3/6 — cartelle per area").

---

### Task 4 (PR 4): HUD diviso

**Files:**
- Create: `godot/scripts/ui/hud/CorePanel.cs`, `BuildPalette.cs`, `BlockInfoPanel.cs`, `HudStyle.cs`
- Rename: `godot/scripts/ui/hud/FactoryHud.cs` → `Hud.cs` (classe `Hud`)
- Modify: tutti i riferimenti a `FactoryHud` (`World*.cs`, `TechTreeCanvas`/pannelli che usano `FactoryHud.ItemTexture` o `FactoryHud.StructureIdFor`)

**Interfaces:**
- `HudStyle` (static): colori (`PanelBg`, `PanelEdge`, `SlotBg`, `SlotIdle`, `SlotSelected`, `TextPrimary`, `TextMuted`, `Insufficient`, `MoneyColor`, `PowerColor`, `Cat*`) e helper `MakePanel(string)`, `MakeSlotStyle(Color,int)`, `MakeIcon(Texture2D?,float,Color?)`, `ItemTexture(string)`, `FormatCount(int)`, `MakeCostChip(...)`, `MakeMoneyChip(int,bool)`.
- `CorePanel : VBoxContainer` — contiene l'attuale colonna in alto a sinistra (oggi `BuildTopLeftColumn`, `AddMenuButton`, `BuildCorePanel`, `AddStockCell`, `BuildObjectivesPanel`, `SetStock`, `UpdateStock`, `SyncCoreUpgrade`, `ClearObjectives`, `UpdateObjectives`); eventi `MenuRequested`, `ResearchRequested`, `MercatoRequested`, `CampaignRequested`, `CoreUpgradeRequested`.
- `BuildPalette : PanelContainer` — categorie e griglia (oggi `BuildCornerDock` parte palette, `AddCategoryButton`, `ShowCategory`, `RebuildGrid`, `AddPaletteCell`, `OnPaletteClicked`, chrome selezione/blocco, `EntriesFor`, `CategoryFor`, `FindEntry`); eventi `ToolChosen(ToolKind)`, `Hovered(ToolKind?)`.
- `BlockInfoPanel : Control` — slot riservato e dettaglio `?` (oggi `SetInfoSlotFilled`, `RefreshBlockInfo`, `ResolveCost`, `ResolveIoLine`, `BuildDetailOverlay`, `OpenDetailOverlay`); metodo `Show(ToolKind? focus, bool locked)`.
- `Hud` — invariata verso `World`: stessi metodi pubblici (`BindEconomy`, `SetSelectedTool`, `SetSorterFilterBrush`, `SetToolLocked`, `SetToolsLocked`, `IsToolLocked`, `ClearToolSelection`, `SetDirectionLabel`, `UpdateStock`, `SyncCoreUpgrade`, `ShowToast`, `ClearObjectives`, `UpdateObjectives`), stessi eventi, stessi tipi annidati `ToolKind`/`BuildCategory`, static `StructureIdFor`; `ItemTexture` passa a `HudStyle.ItemTexture`.

- [ ] **Step 1:** Estrai `HudStyle` (spostamento puro). Build + playtest `ui` → OK.
- [ ] **Step 2:** Estrai `CorePanel`; `Hud` lo crea e inoltra eventi/aggiornamenti. Build + playtest `ui`.
- [ ] **Step 3:** Estrai `BuildPalette` e `BlockInfoPanel`; `Hud` collega hover/selezione tra i due. Build + playtest `ui`.
- [ ] **Step 4:** Rinomina `FactoryHud` → `Hud`.
- [ ] **Step 5:** Misura: `wc -l godot/scripts/ui/hud/*.cs` → nessun file oltre 600 righe.
- [ ] **Step 6:** VERIFICA 1–6 — il confronto pixel del playtest `ui` (HUD, palette-info, mercato, ricerca) con la baseline deve essere identico fuori dalle zone mascherate.
- [ ] **Step 7:** CHANGELOG; commit; VERIFICA 7 (PR "refactor: riordino 4/6 — HUD in componenti").

---

### Task 5 (PR 5): Dati unici, validazione, versione salvataggi

**Files:**
- Delete from git: `godot/data/` (+ `.gitignore`: `godot/data/`)
- Create: `tests/TIndustry.Shared.Tests/ContentValidationTests.cs`, `tests/TIndustry.Shared.Tests/SaveRoundTripTests.cs`
- Modify: `src/TIndustry.Shared/Save/FactorySave.cs` (`CurrentVersion = 9`, messaggio per versioni vecchie), `godot/scripts/world/World.Session.cs` (`LoadSlice` mostra il messaggio nel toast), `tests/TIndustry.Shared.Tests/TIndustry.Shared.Tests.csproj` (include anche le icone per il test: `<None Include="../../godot/assets/*.png" Link="assets/%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />`)

**Interfaces:**
- Produces: `FactorySaveData.CurrentVersion == 9`; `FactorySaveStore.Load(string slotId)` lancia `InvalidDataException` con messaggio esatto `"Salvataggio di una versione precedente: non compatibile."` se `Version < CurrentVersion`, e `"Versione save non supportata: {n}"` se `Version > CurrentVersion` o `< 1`.

- [ ] **Step 1: test di validazione (rossi dove serve).** In `ContentValidationTests`:
  - `StructureIdsAreUnique`, `ItemIdsAreUnique` (ids di `market`)
  - `PrerequisitesExistAndHaveNoCycles` (DFS su `Prerequisites`)
  - `EveryStructureHasAnIcon` → esiste `assets/<id>.png` nell'output dei test per ogni struttura, con le eccezioni attuali mappate come in `TechTreeCanvas.ResolveIcon` (`conveyor-express` → `conveyor-fast`, `conveyor-bridge` → `bridge`)
  - `EveryMarketItemHasAnIcon`
  - `CampaignObjectivesReferenceKnownIds`
- [ ] **Step 2:** `dotnet test` → registra quali falliscono (atteso: tutti verdi; se uno è rosso è un difetto reale nei dati: correggilo in `data/` e annotalo nel CHANGELOG).
- [ ] **Step 3: test dei salvataggi.** In `SaveRoundTripTests` (collection `SaveStore`):

```csharp
[Fact] public void NewGameSavesAndReloads()      // CreateSandboxSlice → Save → Load → stesse Miners/Belts/Money
[Fact] public void OldSaveVersionIsRejectedWithClearMessage()
{
    // save valido con Version = FactorySaveData.CurrentVersion - 1 scritto su disco
    var ex = Assert.Throws<InvalidDataException>(() => FactorySaveStore.Load("vecchio"));
    Assert.Equal("Salvataggio di una versione precedente: non compatibile.", ex.Message);
}
```
- [ ] **Step 4:** `dotnet test` → `OldSaveVersionIsRejectedWithClearMessage` FAIL (oggi le versioni precedenti sono accettate).
- [ ] **Step 5:** `CurrentVersion = 9` e controllo di versione in `Restore`/`Load` con i due messaggi; `World.LoadSlice` cattura `InvalidDataException` e mostra `ex.Message` nel toast (resta sulla partita corrente).
- [ ] **Step 6:** `dotnet test` → tutti verdi.
- [ ] **Step 7:** `git rm -r --cached godot/data`, aggiungi a `.gitignore`. In sviluppo il gioco legge `res://../data/` (`ResolveContentPath`, primo candidato): avvia il playtest `ui` con `godot/data` rimossa dal disco → OK.
- [ ] **Step 8:** Export: `gh workflow run release.yml --ref <branch> -f tag=""` → job `export-godot` `success` (crea gli zip senza pubblicare release); scarica l'artefatto `tIndustry-godot-zips` con `gh run download` e verifica che lo zip Windows contenga `data/content.json`.
- [ ] **Step 9:** VERIFICA 1–6; CHANGELOG ("### Cambiato": salvataggi precedenti non compatibili, con messaggio); commit; VERIFICA 7 (PR "chore: riordino 5/6 — dati unici e validazione").

---

### Task 6 (PR 6): Documentazione

**Files:**
- Create: `docs/ARCHITETTURA.md`, `docs/decisioni/0001-server-csharp.md`, `CLAUDE.md`
- Modify: `README.md`, `godot/README.md`

**Interfaces:**
- Consumes: struttura e nomi finali dei task 1–5.

- [ ] **Step 1:** `ARCHITETTURA.md` con queste sezioni: Mappa delle cartelle (albero finale reale, generato con `git ls-files` e verificato); Ciclo di un frame (`World._Process` → `Factory.Tick` → visuals → `Hud.UpdateStock`); Dati (`data/` unica fonte, come arrivano nell'export); Salvataggi e impostazioni (`GameDataPaths`, `TINDUSTRY_DATA_DIR`, versione 9); Come aggiungere un blocco (content.json → icona in `godot/assets` → entry palette in `BuildPalette` → visual in `visuals/buildings` → test di validazione); Test e playtest (comandi di `tools/dev/`).
- [ ] **Step 2:** ADR `0001-server-csharp.md`: Contesto (multiplayer obiettivo, drone, costruzione in corso), Opzioni (C# riusando Shared; Rust), Decisione (C#, server .NET headless), Conseguenze (Shared senza dipendenze da Godot né dal sistema operativo; simulazione deterministica da progettare nel sotto-progetto 2), Stato: accettata 2026-10-15.
- [ ] **Step 3:** `CLAUDE.md` (≤ 80 righe): lingua italiana con l'utente; CI solo manuale/release; comandi build/test/playtest (`tools/dev/playtest.sh`); regola dati reali del giocatore; ripristino `.import` dopo `--import`; convenzioni di nomi/cartelle; puntatori ad `ARCHITETTURA.md`, `docs/decisioni/`, `docs/superpowers/`.
- [ ] **Step 4:** README: tabella Path aggiornata (`godot/`, `src/TIndustry.Shared/`, `data/`, `tests/`, `docs/`, `tools/`), comandi di sviluppo via `tools/dev/`; `godot/README.md`: sezione "Prove automatiche" su `tools/dev/playtest.sh`.
- [ ] **Step 5:** Controllo link: ogni percorso citato in `ARCHITETTURA.md`, `CLAUDE.md`, README esiste (`grep -o '\`[^\`]*/[^\`]*\`'` sui tre file e `test -e` per ciascuno).
- [ ] **Step 6:** VERIFICA 1–2 e 7; CHANGELOG; commit (PR "docs: riordino 6/6 — architettura, decisione server, CLAUDE.md").
