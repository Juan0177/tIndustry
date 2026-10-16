# Simulazione a comandi e sistemi — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rendere la simulazione di tIndustry deterministica, a tick fisso, divisa in sistemi, guidata da comandi tramite un server locale (`SimHost`) e serializzabile in un'istantanea con checksum — senza cambiare il gioco per il giocatore.

**Architecture:** Lavoro incrementale in 7 task raggruppati nelle 5 PR della spec. Prima il determinismo (tick fisso, ordine canonico, id), poi `GameState` + sistemi dietro la facciata `Factory`, poi comandi e `SimHost` (prima in Shared, poi nel client), poi istantanea/checksum e salvataggi v10, infine la documentazione.

**Tech Stack:** C# / .NET 8, xUnit, Godot 4.4.1 .NET (client), `tools/dev/dev.sh` e `tools/dev/playtest.sh`.

**Spec:** `docs/superpowers/specs/2026-10-15-simulazione-comandi-sistemi-design.md`

## Global Constraints

- Tick fisso: `TickRate = 60`, `TickSeconds = 1f / 60f`, `MaxTicksPerFrame = 5` (valori della spec).
- Ordine dei sistemi in `Step()`: Belt (avanzamento) → Power → Mining → Crafting → Extraction → Belt (passo zero) → CoreDelivery.
- Regole di determinismo (spec): niente tempo in `Step()`; niente dipendenza dall'ordine di `Dictionary`/`HashSet`; niente orologio né `Random` non seminato; niente parallelismo; formati numerici invarianti; nessun riferimento a Godot/sistema operativo in `TIndustry.Shared`.
- Comandi validati all'applicazione; esito come evento `CommandApplied` / `CommandRejected(motivo)` con i testi italiani di oggi.
- Checksum: FNV-1a 64 bit sui byte dell'istantanea canonica. Salvataggi versione **10**; v9 → messaggio esistente "Salvataggio di una versione precedente: non compatibile.".
- Comportamento di gioco invariato; soglie temporali dei test adattate solo dove il tick fisso le sposta, con commento che spiega il nuovo valore.
- Regole di progetto (`CLAUDE.md`): italiano con l'utente; CI solo manuale (`gh workflow run build-publish.yml --ref <branch>`); dati reali del giocatore mai toccati (usa `--profilo-temp` / `TINDUSTRY_DATA_DIR`); dopo `--import` ripristina i `.import` che differiscono solo per fine riga; `git mv` degli script Godot insieme al `.uid`.
- Commit con `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; PR con `🤖 Generated with [Claude Code](https://claude.com/claude-code)`. PR impilate: ogni PR ha come base il branch della precedente; nessun merge senza richiesta dell'utente.

**Verifica standard** (VERIFICA, tutte le voci obbligatorie a fine di ogni PR):
1. `dotnet build tIndustry.sln -c Release -nologo -v q` → `Errori: 0`, `Avvisi: 0`
2. `dotnet test tests/TIndustry.Shared.Tests -c Release --no-build` → `Non superati: 0`
3. `tools/dev/dev.sh --playtest ui` e `--playtest tutorial` → `OK`
4. `python tools/dev/compare_shots.py "$TEMP/baseline-sp2/<ui|tutorial>" .dev/playtest-<ui|tutorial>` → exit 0
5. `diff -rq "$TEMP/tIndustry-backup-riordino" "$LOCALAPPDATA/tIndustry"` identico; `settings.json` reale con data invariata
6. Push, PR, `gh workflow run build-publish.yml --ref <branch>` → `success`

## Review Focus

- **Ritmo di gioco cambiato dal tick fisso** (produzione, velocità dei nastri, energia più lente o più veloci): il giocatore non deve accorgersene → test `GameplayRatesMatchWallClock` (task 1) e confronto screenshot.
- **Gioco che rallenta** (frame da 200 ms): niente spirale, al massimo 5 tick per frame → `LongFrameIsCappedAtMaxTicks` (task 1).
- **Nastri trascinati** (decine di comandi nello stesso frame): tutti applicati, in ordine, nello stesso tick → `ManyCommandsInOneFrameApplyInOrder` (task 3).
- **Comando reso invalido da un evento precedente nello stesso tick** (risorse spese dal comando prima): rifiutato con motivo, non applicato a metà → `SecondCommandRejectedWhenFirstSpentTheMoney` (task 3).
- **Salvataggio fatto a metà di un tick o con comandi in coda**: il salvataggio contiene solo stato applicato, i comandi in coda non si perdono né si duplicano → `SaveWithPendingCommandsKeepsThemForNextTick` (task 6).

---

### Task 0: Baseline del sotto-progetto

**Files:** nessuno nel repo.

- [ ] **Step 1:** `git switch -c feat/sim-1-determinismo main` (dopo il merge di `docs/sim-comandi-spec`, oppure da `docs/sim-comandi-spec` se non ancora unito: la spec e questo piano viaggiano con la prima PR).
- [ ] **Step 2:** baseline: `tools/dev/playtest.sh ui "$TEMP/baseline-sp2/ui"` e `tools/dev/playtest.sh tutorial "$TEMP/baseline-sp2/tutorial"` → entrambi `OK`; auto-confronto `python tools/dev/compare_shots.py "$TEMP/baseline-sp2/ui" "$TEMP/baseline-sp2/ui"` → exit 0.
- [ ] **Step 3:** `dotnet test` → annota il numero di test verdi (atteso 62).

---

### Task 1 (PR 1): Fondamenta del determinismo

**Files:**
- Create: `src/TIndustry.Shared/Simulation/SimulationClock.cs`
- Create: `tests/TIndustry.Shared.Tests/DeterminismTests.cs`, `tests/TIndustry.Shared.Tests/FactoryTestExtensions.cs`
- Modify: `src/TIndustry.Shared/Factory.cs` (nuovi `Step()`, `CurrentTick`, id degli edifici; `Tick(float)` diventa `private`), `src/TIndustry.Shared/Grid/BeltGrid.cs` (iterazione canonica), `src/TIndustry.Shared/Buildings/{Miner,Crafter,Extractor}.cs`, `src/TIndustry.Shared/Power/{Generator,PowerNode}.cs` (proprietà `Id`), `src/TIndustry.Shared/Save/FactorySave.cs` (DTO con `Id` e `CurrentTick`; celle nastro salvate in ordine canonico), `tests/TIndustry.Shared.Tests/FactoryScenarios.cs` (38 chiamate a `Tick(dt)` → `RunSeconds`/`Step`), `godot/scripts/world/World.cs` (`_Process` usa `SimulationClock`)

**Interfaces:**
- Produces:
  - `public sealed class SimulationClock` con `public const int TickRate = 60`, `public const float TickSeconds = 1f / 60f`, `public const int MaxTicksPerFrame = 5`, `public int Accumulate(double frameSeconds)` → numero di tick da eseguire (accumulatore interno; mai più di `MaxTicksPerFrame`; l'eccedenza oltre il tetto viene scartata).
  - `Factory.Step()` — avanza un tick (`TickSeconds`); `Factory.CurrentTick : long`.
  - `long Id { get; }` su `Miner`, `Crafter`, `Generator`, `Extractor`, `PowerNode`, assegnato alla creazione da un contatore `nextEntityId` di `Factory` (salvato). Salvataggi senza id (versione 9 caricata in sviluppo) → id riassegnati in ordine di lista.
  - Ordine canonico dei nastri: `BeltGrid` itera le celle ordinate per `(Y, X)`. Struttura a scelta dell'implementatore (es. `SortedDictionary<GridPosition, BeltGridCell>` con comparer dedicato, o lista ordinata mantenuta), purché `Cells` resti `IReadOnlyDictionary<GridPosition, BeltGridCell>`.
  - Test helper: `static void RunSeconds(this Factory f, float seconds)` → esegue `ceil(seconds / TickSeconds)` volte `Step()`.

- [ ] **Step 1: test rossi** in `DeterminismTests`:

```csharp
[Fact] public void StepAdvancesExactlyOneTick()            // CurrentTick 0 → 1 → 2
[Fact] public void LongFrameIsCappedAtMaxTicks()           // new SimulationClock().Accumulate(0.2) == 5; poi Accumulate(0) == 0
[Fact] public void FrameRateDoesNotChangeTheOutcome()
{
    // stessa demo (Factory.CreateSpikeDemo), A: frame costanti 1/60, B: frame casuali in [0.004, 0.05] con Random(42)
    // entrambe guidate da SimulationClock finché CurrentTick == 1200 (B: se supera, si ferma esattamente a 1200 eseguendo i tick uno a uno)
    // Assert.Equal(Serialize(A.Capture()), Serialize(B.Capture()))
}
[Fact] public void PlacementOrderDoesNotChangeOutcome()   // stessa linea minatore→nastri→CORE costruita in ordine diretto e inverso prima del primo tick; 1200 tick; Capture uguali
[Fact] public void SaveRestoreMidGameKeepsSimulationIdentical()
{
    // 300 tick; copy = Factory.Restore(content, original.Capture()); entrambe +600 tick; Capture uguali (JSON con JsonSerializer e le opzioni di FactorySaveStore)
}
[Fact] public void GameplayRatesMatchWallClock()          // un minatore T1 su ferro in 10 s di gioco consegna lo stesso numero di oggetti al CORE (±1) di prima del cambio: valore misurato con Tick(1/30f) prima di modificare il codice e scritto come costante nel test
```
- [ ] **Step 2:** `dotnet test --filter DeterminismTests` → rossi per assenza di `Step`/`SimulationClock`/`CurrentTick`; registra l'output.
- [ ] **Step 3:** implementa `SimulationClock`, `Factory.Step()`/`CurrentTick`, id degli edifici (+ DTO), ordine canonico in `BeltGrid` e nella cattura dei nastri; `Tick(float)` diventa `private` e viene chiamato solo da `Step()`.
- [ ] **Step 4:** migra le 38 chiamate di `FactoryScenarios` a `RunSeconds`/`Step` (stessa durata simulata). Dove un'asserzione temporale cambia, aggiorna la soglia e scrivi nel commento il valore vecchio, quello nuovo e il perché.
- [ ] **Step 5:** `World._Process`: `var ticks = _clock.Accumulate(delta); for (var i = 0; i < ticks; i++) _slice.Step();` al posto di `_slice.Tick((float)delta)`.
- [ ] **Step 6:** `dotnet test` → tutti verdi (atteso: 62 + 6).
- [ ] **Step 7:** VERIFICA 1–5; voce CHANGELOG "### Sviluppo"; commit; VERIFICA 6 — PR "feat(sim): tick fisso 60/s, ordine canonico, id stabili (1/5)" con base `main` (porta con sé spec e piano).

---

### Task 2 (PR 2): `GameState` e sistemi

**Files:**
- Create: `src/TIndustry.Shared/Simulation/GameState.cs`, `src/TIndustry.Shared/Simulation/Simulation.cs`, `src/TIndustry.Shared/Simulation/Systems/{BeltSystem,PowerSystem,MiningSystem,CraftingSystem,ExtractionSystem,CoreDeliverySystem}.cs`, `tests/TIndustry.Shared.Tests/SystemOrderTests.cs`
- Modify: `src/TIndustry.Shared/Factory.cs` (diventa facciata: possiede un `GameState`, le operazioni di piazzamento/economia restano metodi di `Factory` ma leggono/scrivono `State`; `Step()` delega a `Simulation.Step(State)`)

**Interfaces:**
- Consumes: `SimulationClock`, `Factory.Step/CurrentTick`, id degli edifici (task 1).
- Produces:
  - `public sealed class GameState` — campi oggi privati in `Factory`: liste di edifici, `BeltGrid Belts`, `EconomyWallet Wallet`, `ResearchState Research`, `EconomySession Session`, `MarketCatalog Market`, `IReadOnlySet<GridPosition> CoreTiles`, `TerrainMap? Terrain`, `float PowerBuffer/PowerCapacity/PowerGenerationPerSecond`, `bool AutoSellAtCore`, `int CoreUpgradeLevel/CoreSaleBonusPercent`, `string? ActiveCampaignLevelId`, `long CoreDeliveredItems`, `long NextItemId`, `long NextEntityId`, `long CurrentTick`, `FactoryContent Content`.
  - `public interface ISimulationSystem { string Name { get; } void Run(GameState state, SystemContext ctx); }` — `SystemContext` porta solo `float Dt` (= `TickSeconds`, o `0` per il secondo passaggio nastri) e la lista dei generatori attivi calcolata da `PowerSystem`.
  - `public static class Simulation` con `public const int TickRate = SimulationClock.TickRate`, `public const float TickSeconds = SimulationClock.TickSeconds`, `public static IReadOnlyList<ISimulationSystem> Systems` (ordine della spec) e `public static void Step(GameState state)`.
  - `Factory.State : GameState` (accesso pubblico in sola lettura alla proprietà; il tipo resta mutabile fino al task 4).

- [ ] **Step 1: test rosso** `SystemOrderTests.SystemsRunInSpecOrder`: `Simulation.Systems.Select(s => s.Name)` == `["Belt", "Power", "Mining", "Crafting", "Extraction", "Belt", "CoreDelivery"]`. Più `StepIsPureOverState`: due `GameState` uguali (via `Restore` dello stesso `Capture`) dopo 600 `Simulation.Step` → `Capture` uguali.
- [ ] **Step 2:** `dotnet test --filter SystemOrderTests` → rosso (tipi assenti).
- [ ] **Step 3:** estrai `GameState`; sposta il corpo di `Factory.Tick` nei sistemi senza cambiarne la logica (stesso ordine, stessi calcoli, stesse chiamate a `TryDeliverAdjacent`/`TrySpendPower`, ora funzioni che ricevono `GameState`).
- [ ] **Step 4:** `dotnet test` → tutti verdi, inclusi `DeterminismTests` e i 22 scenari senza toccare le soglie (spostamento puro).
- [ ] **Step 5:** controllo: `Factory.cs` non contiene più logica di tick (`grep -n "foreach (var miner" src/TIndustry.Shared/Factory.cs` → nessun risultato).
- [ ] **Step 6:** VERIFICA 1–5; CHANGELOG; commit; VERIFICA 6 — PR "refactor(sim): GameState e sette sistemi in ordine fisso (2/5)" con base il branch del task 1.

---

### Task 3 (PR 3, parte 1): Comandi, `SimHost` ed eventi (solo Shared)

**Files:**
- Create: `src/TIndustry.Shared/Simulation/Commands.cs`, `src/TIndustry.Shared/Simulation/SimHost.cs`, `src/TIndustry.Shared/Simulation/IGameView.cs`, `tests/TIndustry.Shared.Tests/SimHostTests.cs`
- Modify: `src/TIndustry.Shared/Factory.cs` (le `Try…` restituiscono il motivo del rifiuto invece di `bool`, o una variante `…WithReason` usata dall'host)

**Interfaces:**
- Consumes: `Factory` facciata e `GameState` (task 2).
- Produces:
  - `public abstract record SimCommand(int PlayerId) { public long Tick { get; init; } }` e i record della spec: `PlaceBelt(int PlayerId, GridPosition Position, Direction Direction, string ConveyorId)`, `PlaceBuilding(int PlayerId, BuildingKind Kind, GridPosition Origin, Direction Direction, string? RecipeId = null, string? FilterItemId = null)`, `Remove(int PlayerId, GridPosition Tile)`, `UnlockStructure(int PlayerId, string StructureId)`, `Sell(int PlayerId, string ItemId, int Amount)`, `SetAutoSell(int PlayerId, bool Enabled)`, `UpgradeCore(int PlayerId)`. `BuildingKind`: `Junction, Splitter, Sorter, Bridge, Miner, MinerAdvanced, Smelter, Assembler, Generator, Extractor, PowerNode, PowerNodeT2`.
  - `public sealed class SimHost` con `SimHost(Factory factory)`, `void Submit(SimCommand cmd)` (assegna `Tick = CurrentTick + 1` se 0), `int Advance(double frameSeconds)` (usa un `SimulationClock`; per ogni tick: applica i comandi di quel tick in ordine di arrivo, poi `Factory.Step()`; ritorna i tick eseguiti), `IGameView State`, `event Action<SimCommand>? CommandApplied`, `event Action<SimCommand, string>? CommandRejected`.
  - `public interface IGameView` — sola lettura di ciò che il client legge oggi: `CurrentTick`, `Content`, `Belts` (`IReadOnlyDictionary`), le liste di edifici (`IReadOnlyList`), `IWalletView Wallet` (`int Money`, `int MaterialCount(string)`, `IReadOnlyDictionary<string,int> MaterialsSnapshot()`; implementata da `EconomyWallet`), `IResearchView Research` (`IsUnlocked`, `GetNodeState`, `CanUnlock(StructureDefinition, IWalletView)`, `MeetsPrerequisites`, `UnlockedIds`; implementata da `ResearchState`), `Market`, `Session`, `CoreTiles`, `Terrain`, potenza, `AutoSellAtCore`, upgrade del CORE, `ActiveCampaignLevelId`, `CoreDeliveredItems`, e i metodi di sola lettura usati dalla UI (`CanAffordStructure`, `FormatNeedMessage`, `PreviewSellPrice`, `FormatCoreUpgradeNeedMessage`, `IsStructureUnlocked`, `CanOccupyFootprint`, `IsBuildingTile`). `Factory` implementa `IGameView`.
  - Motivi di rifiuto: i testi italiani che oggi il client mostra nei toast (es. il risultato di `FormatNeedMessage`, "… bloccato: sbloccalo in Ricerca (T).", "Spazio occupato.").

- [ ] **Step 1: test rossi** in `SimHostTests`:

```csharp
[Fact] public void SubmittedCommandAppliesOnNextTick()               // Submit(PlaceBelt) → stato invariato; Advance(TickSeconds) → nastro presente; CommandApplied ricevuto
[Fact] public void ManyCommandsInOneFrameApplyInOrder()              // 30 PlaceBelt in fila nello stesso frame → tutti presenti dopo 1 tick, eventi nell'ordine di Submit
[Fact] public void RejectedCommandLeavesStateUnchanged()             // PlaceBuilding Smelter non sbloccato → CommandRejected con motivo non vuoto; Capture prima == dopo
[Fact] public void SecondCommandRejectedWhenFirstSpentTheMoney()     // Wallet con fondi per 1 sblocco; due UnlockStructure diversi nello stesso tick → primo applicato, secondo rifiutato con motivo
[Fact] public void SellAndAutoSellAndUpgradeGoThroughCommands()      // Sell/SetAutoSell/UpgradeCore: effetti identici ai metodi diretti di oggi
[Fact] public void HostAdvanceRespectsTickCap()                      // Advance(1.0) == 5
```
- [ ] **Step 2:** `dotnet test --filter SimHostTests` → rossi.
- [ ] **Step 3:** implementa comandi, `SimHost`, `IGameView`, motivi di rifiuto.
- [ ] **Step 4:** `dotnet test` → tutti verdi.
- [ ] **Step 5:** commit sul branch `feat/sim-3-comandi` (nessuna PR ancora: la PR 3 si apre col task 4).

---

### Task 4 (PR 3, parte 2): Client su `SimHost`

**Files:**
- Modify: `godot/scripts/world/World.cs`, `World.Placement.cs`, `World.Session.cs`, `World.Input.cs`, `World.Visuals.cs`, `World.Drawing.cs`, `World.Panels.cs`, `godot/scripts/dev/World.Playtest.cs`, `godot/scripts/ui/panels/{MercatoPanel,ResearchPanel}.cs`, `godot/scripts/ui/hud/*` dove ricevono `Factory`

**Interfaces:**
- Consumes: `SimHost`, `SimCommand` e record, `IGameView`, eventi (task 3).
- Produces: `World` possiede `SimHost _host` (nessun campo `Factory` accessibile alla UI); i pannelli ricevono `IGameView` per leggere e un `Action<SimCommand>` per inviare.

- [ ] **Step 1:** `World`: `_host = new SimHost(factory)` dove oggi si crea/carica `_slice`; `_Process` → `_host.Advance(delta)`; tutte le letture passano da `_host.State`.
- [ ] **Step 2:** `World.Placement`: ogni `_slice.TryPlace…`/`TryRemove…` → `_host.Submit(new …)`. Il toast di rifiuto e l'aggiornamento grafico (`_visualDirty`/`_buildingsDirty`) avvengono negli handler di `CommandApplied`/`CommandRejected`.
- [ ] **Step 3:** Mercato (`Sell`, `SetAutoSell`), Ricerca (`UnlockStructure`), upgrade del CORE (`UpgradeCore`): inviano comandi; il pannello si aggiorna su `CommandApplied`.
- [ ] **Step 4:** controllo a compilazione: `grep -rnE "\.(TryPlace|TryRemove|TryUnlock|TrySell|TryUpgrade)|AutoSellAtCore\s*=|Wallet\.(Add|TrySpend|TryRemove)" godot/scripts` → nessun risultato (tranne la creazione dello stato iniziale nei costruttori di partita).
- [ ] **Step 5:** playtest `ui` e `tutorial` → `OK` (il tutorial deve ancora avanzare: minatore piazzato, nastro fino al CORE, Mercato, Ricerca); confronto screenshot con `baseline-sp2`.
- [ ] **Step 6:** VERIFICA 1–5; CHANGELOG; commit; VERIFICA 6 — PR "feat(sim): comandi, SimHost e client in sola lettura (3/5)" con base il branch del task 2.

---

### Task 5 (PR 4, parte 1): Istantanea canonica e checksum

**Files:**
- Create: `src/TIndustry.Shared/Simulation/Snapshot.cs`, `tests/TIndustry.Shared.Tests/SnapshotTests.cs`
- Modify: `src/TIndustry.Shared/Simulation/SimHost.cs` (`Checksum()`, `CreateSnapshot()`, `static SimHost FromSnapshot(FactoryContent, byte[])`)

**Interfaces:**
- Produces: `public static class Snapshot` con `byte[] Write(GameState state)` (JSON UTF-8 canonico: collezioni in ordine di id / posizione `(Y, X)`, `JsonSerializerOptions` con cultura invariante, nessun campo di metadati), `GameState Read(FactoryContent content, byte[] data)`, `ulong Checksum(byte[] snapshot)` (FNV-1a 64: offset `14695981039346656037`, primo `1099511628211`).

- [ ] **Step 1: test rossi** in `SnapshotTests`:

```csharp
[Fact] public void ReplayGivesSameChecksum()                 // stessa sequenza di comandi (a tick fissati) + 10_000 tick, eseguita due volte → Checksum uguale
[Fact] public void SnapshotRoundTripMidGame()                // 3000 tick con comandi; host2 = FromSnapshot(CreateSnapshot()); entrambi +3000 tick → checksum uguale
[Fact] public void SnapshotIsCanonical()                     // stessa fabbrica costruita in due ordini diversi prima del primo tick → byte dell'istantanea identici
[Fact] public void ChecksumKnownVector()                     // Checksum(Encoding.UTF8.GetBytes("tIndustry")) == valore FNV-1a 64 calcolato a parte e scritto come costante
```
- [ ] **Step 2:** `dotnet test --filter SnapshotTests` → rossi.
- [ ] **Step 3:** implementa `Snapshot` e i metodi di `SimHost`.
- [ ] **Step 4:** misura: tempo di `CreateSnapshot()+Checksum()` sulla mappa 1000×1000 con la demo più grande disponibile, annotato nel report (riferimento per il sotto-progetto 5; nessuna soglia vincolante).
- [ ] **Step 5:** `dotnet test` → tutti verdi; commit sul branch `feat/sim-4-istantanea` (la PR 4 si apre col task 6).

---

### Task 6 (PR 4, parte 2): Salvataggi v10, costruttori di partita, nomi storici

**Files:**
- Modify: `src/TIndustry.Shared/Save/FactorySave.cs` (formato = metadati + istantanea; `CurrentVersion = 10`), `src/TIndustry.Shared/Factory.cs` (costruttori: `CreateSandboxSlice` → `NewGame.Sandbox(…)`, `CreateCampaignSlice` → `NewGame.Campaign(…)`, `CreateSpikeDemo` → `NewGame.Demo(…)`, `BuildSpikeLPath` → `DemoLayouts.LPath()`, `UnlockGodotSliceDemo` → `UnlockAllForDemo`), `godot/scripts/world/*` (`_slice` → `_host`/`_game`, `SaveSlice`/`LoadSlice` → `SaveGame`/`LoadGame`), test che usano i vecchi nomi
- Create: `src/TIndustry.Shared/Simulation/NewGame.cs`, `tests/TIndustry.Shared.Tests/SaveV10Tests.cs`

**Interfaces:**
- Consumes: `Snapshot`, `SimHost.CreateSnapshot/FromSnapshot` (task 5).
- Produces: `FactorySaveStore.Save(string slotId, SimHost host)` e `bool TryLoad(FactoryContent content, string slotId, out SimHost? host, out string? error)`; messaggi d'errore invariati ("Salvataggio di una versione precedente: non compatibile.", "Salvataggio non leggibile."); `ContinueAutoSave` (#145) invariato.

- [ ] **Step 1: test rossi** in `SaveV10Tests`:

```csharp
[Fact] public void NewGameSaveLoadKeepsChecksum()                    // NewGame.Sandbox → +600 tick → Save → TryLoad → Checksum uguale
[Fact] public void Version9SaveIsRejectedWithClearMessage()          // file con "version": 9 → error == "Salvataggio di una versione precedente: non compatibile."
[Fact] public void SaveWithPendingCommandsKeepsThemForNextTick()     // Submit(PlaceBelt) senza Advance → Save → TryLoad: il comando NON è nel salvataggio; sull'host originale si applica al tick successivo come previsto (salvataggio = solo stato applicato)
```
- [ ] **Step 2:** `dotnet test --filter SaveV10Tests` → rossi.
- [ ] **Step 3:** implementa salvataggi v10 e `NewGame`; rinomina i nomi storici (rinomina per simbolo, aggiorna i test).
- [ ] **Step 4:** controllo residui: `grep -rnE "Slice|Spike" src godot/scripts tests --include=*.cs` → nessun risultato.
- [ ] **Step 5:** `dotnet test` → tutti verdi; playtest e confronto screenshot.
- [ ] **Step 6:** VERIFICA 1–5; CHANGELOG ("### Cambiato": salvataggi v10, i v9 non si caricano); commit; VERIFICA 6 — PR "feat(sim): istantanea, checksum e salvataggi v10 (4/5)" con base il branch del task 4.

---

### Task 7 (PR 5): Documentazione

**Files:**
- Create: `docs/decisioni/0002-sincronizzazione-ibrida.md`
- Modify: `docs/ARCHITETTURA.md`, `CLAUDE.md`, `README.md` (solo se cita nomi cambiati)

- [ ] **Step 1:** ADR 0002: Contesto (co-op ospitato da un giocatore), Opzioni (istantanee / lockstep / ibrido), Decisione (ibrido: host autoritativo che ordina i comandi, simulazione deterministica, checksum ogni 60 tick, istantanea di recupero e per entrare a partita in corso), Conseguenze (regole di determinismo; `float` accettato con recupero via istantanea), Stato: accettata 2026-10-15.
- [ ] **Step 2:** `ARCHITETTURA.md`: sezione "Simulazione" (tick fisso, `GameState`, sistemi e ordine, `SimHost`, comandi ed eventi, istantanea/checksum); "Regole di determinismo" (le cinque della spec); ciclo di frame aggiornato (`_Process` → `SimHost.Advance` → grafica); "Come aggiungere un blocco" aggiornato (nuovo `BuildingKind` + applicazione del comando); "Nomi storici" aggiornata (rimossi quelli eliminati nel task 6).
- [ ] **Step 3:** `CLAUDE.md`: una riga sulle regole di determinismo e il rimando ad ADR 0002.
- [ ] **Step 4:** controllo link: ogni percorso e simbolo citato esiste (`grep`/`test -e`).
- [ ] **Step 5:** VERIFICA 1–2 e 6; CHANGELOG; commit — PR "docs(sim): architettura della simulazione e ADR 0002 (5/5)" con base il branch del task 6.
