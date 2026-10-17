# Costruzione in corso (come Mindustry) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Con questo piano, piazzare o rimuovere un blocco crea un piano che il drone del giocatore esegue nel tempo, come in Mindustry: cantiere, consumo e rimborso graduali, raggio di costruzione e shader `build`.

**Architecture:**
- I primitivi di piazzamento esistenti (`Factory.TryPlace…`) restano il modo "istantaneo" di creare un blocco. Li riusano il completamento dei cantieri, `InstantBuild`, i test e le demo.
- Sopra di essi arrivano `BuildSpec` (cosa costruire), `ConstructSite` (cantiere), `BuilderUnit` (drone con coda di `BuildPlan`) e due sistemi nuovi in testa all'ordine fisso.
- Fino al task 5 il client crea le partite con `InstantBuild = true`: il gioco resta identico per il giocatore mentre la simulazione cambia. Il task 5 attiva la costruzione vera insieme alla grafica.

**Tech Stack:** C# / .NET 8, xUnit, Godot 4.4.1 .NET (client, shader `canvas_item`), `tools/dev/dev.sh`, `tools/dev/playtest.sh`, `tools/dev/compare_shots.py`.

**Spec:** `docs/superpowers/specs/2026-10-17-costruzione-in-corso-design.md`

## Global Constraints

- **Regola guida:** il più simile possibile a Mindustry. Dove spec e piano tacciono, si fa come Mindustry.
- **Formula:** `buildCost = (20 + Σ quantità × buildWeight) × buildCostMultiplier`; ogni tick `Δprogress = buildSpeed / buildCost`. Lo smontaggio va alla stessa velocità.
- **Pesi (`buildWeights`):** `iron-plate` 0.7, `copper-wire` 0.5, `copper-ore` 0.5; ogni altro oggetto 1.0. `buildCostMultiplier` vale 1 se assente.
- **Drone (`units.drone`):** `buildSpeed` 0.5, `buildRange` 27.5 tile, `speed` 22.5 tile/s. Lavora quando la distanza dal centro del piano è ≤ `buildRange − 1`. Nasce al centro del Core e torna a riposo nella cella al centro del lato ovest del Core, una tile fuori.
- **Rimborso:** `GameRules.RefundPercent` vale 100 nella partita base e nella sandbox; in campagna è il campo `refundPercent` del livello (livelli 1–3 → 100, livelli 4–10 → 50). Un edificio finito rimborsa `floor(costo × RefundPercent/100 × refundPercent(edificio)/100)` per ogni voce. Un cantiere annullato rimborsa il 100% di quanto speso.
- **Ordine dei sistemi:** Builder → Construct → Belt (avanzamento) → Power → Mining → Crafting → Extraction → Belt (passo zero) → CoreDelivery.
- **Determinismo:** valgono le regole del sotto-progetto 2. Droni, cantieri e piani si iterano in ordine di id e di coda. Le posizioni del drone sono `float` (ADR 0002).
- **Testi italiani:** si riusano quelli esistenti di `CommandReasons` ("Cella occupata o non valida", "Non puoi costruire sul Core", "X bloccato…"). Nuovi testi: "Mancano: {n} {oggetto}" (tooltip del cantiere fermo) e i motivi di `PlanDropped`, cioè "Cella occupata o non valida" e "Bloccato dalla ricerca".
- **Piazzamento:** non controlla più le risorse. Sparisce il rifiuto "Risorse insufficienti" su `PlaceBelt`/`PlaceBuilding` quando `InstantBuild = false`; con `InstantBuild = true` resta il comportamento di oggi.
- **Salvataggi v11:** i v10 vengono rifiutati con "Salvataggio di una versione precedente: non compatibile.".
- **Regole di progetto (`CLAUDE.md`):** italiano con l'utente; CI solo manuale (`gh workflow run build-publish.yml --ref <branch>`); dati reali del giocatore mai toccati; dopo `--import` si ripristinano i `.import` che differiscono solo per fine riga; `git mv` degli script Godot insieme al `.uid`; nuovi script Godot con il loro `.uid` committato.
- **Commit e PR:** commit con `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`; PR con `🤖 Generated with [Claude Code](https://claude.com/claude-code)`. Le PR sono impilate e nessun merge avviene senza richiesta dell'utente. Al merge si riallinea la base di ogni PR a `main` prima di unirla, e dopo si verifica `git merge-base --is-ancestor <branch> origin/main`.
- **Diario:** a fine piano il registro SDD si copia in `docs/diario/2026-10-17-costruzione-in-corso.md`, prima di cancellare `.superpowers/sdd/`.

**Verifica standard** (VERIFICA, obbligatoria a fine di ogni PR):
1. `dotnet build tIndustry.sln -c Release -nologo -v q` → `Errori: 0`, `Avvisi: 0`.
2. `dotnet test tests/TIndustry.Shared.Tests -c Release --no-build` → `Non superati: 0`.
3. `tools/dev/dev.sh --playtest ui` e `--playtest tutorial` → `OK`.
4. Task 1–4: `python tools/dev/compare_shots.py "$TEMP/baseline-sp3/<ui|tutorial>" .dev/playtest-<ui|tutorial>` → exit 0. Task 5: le catture nuove vanno inviate all'utente e diventano la base solo dopo il suo ok.
5. Dati reali: mtime dei file in `%LOCALAPPDATA%\tIndustry` e `%APPDATA%\Godot\app_userdata\tIndustry` invariati, annotati prima e dopo.
6. Push, PR, `gh workflow run build-publish.yml --ref <branch>` → `success`.

## Review Focus

- **Nastro che entra in un cantiere di nastro:** gli oggetti del nastro a monte si fermano davanti alla cella, non spariscono e non si duplicano. Test `ItemsWaitBeforeBeltSite` (task 2).
- **Rimozione di ciò che il drone sta costruendo:** si annulla con rimborso del 100% di quanto speso, senza doppio rimborso e senza blocco rimasto a metà. Test `RemoveOnSiteUnderConstructionRefundsSpent` (task 2).
- **Secondo trascinamento sulle stesse celle già pianificate:** rifiutato con "Cella occupata o non valida", nessun piano doppio. Test `PlanOnPlannedCellsIsRejected` (task 2).
- **Salvataggio con drone in volo, coda piena e cantiere a metà:** il caricamento riprende identico. Test `SaveMidConstructionKeepsChecksum` (task 3).
- **Denaro o materiali spesi altrove (vendita) mentre un cantiere li sta consumando:** il cantiere si ferma, non va in negativo e riparte. Test `SiteStallsWhenMaterialsSoldMidBuild` (task 1).

---

### Task 0: Base di partenza (controller)

- [ ] **Step 1:** dal `main` aggiornato, `tools/dev/dev.sh --playtest ui` e `--playtest tutorial` → OK. Copia `.dev/playtest-ui` e `.dev/playtest-tutorial` in `$TEMP/baseline-sp3/{ui,tutorial}`. Un secondo giro confrontato con `compare_shots.py` deve dare 0 differenze.
- [ ] **Step 2:** annota gli mtime dei file nelle due cartelle dei dati reali. `dotnet test` deve essere verde (108 superati, 1 ignorato).
- [ ] **Step 3:** branch `feat/sp3-1-cantieri` da `docs/sp3-spec`, così spec e piano viaggiano con la PR 1.

### Task 1 (PR 1): Regole, dati, `BuildSpec` e avanzamento del cantiere

**Files:**
- Create:
  - `src/TIndustry.Shared/Construction/GameRules.cs`
  - `src/TIndustry.Shared/Construction/BuildSpec.cs`
  - `src/TIndustry.Shared/Construction/ConstructSite.cs`
  - `src/TIndustry.Shared/Construction/Construction.cs`
  - `tests/TIndustry.Shared.Tests/ConstructionTests.cs`
- Modify:
  - `data/content.json` (`buildWeights`, `units.drone`)
  - `data/campaign.json` (`refundPercent` per livello)
  - `src/TIndustry.Shared/Content/ContentTypes.cs`, `Content/FactoryContent.cs` (DTO e accesso)
  - `src/TIndustry.Shared/Campaign/CampaignCatalog.cs` (`CampaignLevelDefinition.RefundPercent`)
  - `src/TIndustry.Shared/Simulation/GameState.cs` (`Rules`, `Sites`)
  - `src/TIndustry.Shared/Factory.cs` (primitivi per spec, occupazione)
  - `src/TIndustry.Shared/Simulation/NewGame.cs` (parametro `rules`)
  - `src/TIndustry.Shared/Simulation/SimHost.cs` (lo `switch` di `ApplyPlaceBuilding` passa a `BuildSpec`)

**Interfaces (Produces):**
- `public sealed record GameRules(int RefundPercent = 100, bool InstantBuild = false)`; `GameState.Rules { get; set; }`.
- `public sealed record BuildSpec(string StructureId, BuildingKind? Kind, GridPosition Origin, Direction Direction, string? RecipeId = null, string? FilterItemId = null)`. `Kind == null` indica un nastro e allora `StructureId` è il `ConveyorId`. Factory: `static BuildSpec BuildSpec.FromCommand(PlaceBuilding)` e `BuildSpec.FromCommand(PlaceBelt)`.
- `public readonly record struct CostEntry(string? ItemId, int Amount)`, dove `ItemId == null` indica il denaro.
- `Factory`:
  - `IReadOnlyList<GridPosition> Footprint(BuildSpec spec)`: il ponte copre le sue due celle.
  - `IReadOnlyList<CostEntry> CostOf(BuildSpec spec)`: costo intero; per il ponte la molteplicità 2 è inclusa; il denaro è la prima voce se `> 0`; poi i materiali in ordine ordinale per id.
  - `bool CanPlace(BuildSpec spec, out string? reason)`: solo celle (Core, occupazione da edifici, nastri e cantieri). Non controlla sblocco né risorse.
  - `bool PlaceInstant(BuildSpec spec, bool charge)`: con `charge: true` equivale al `TryPlace…` di oggi; con `false` non paga. Restituisce false senza modificare nulla se le celle non sono valide.
  - `bool TryGetSpecAt(GridPosition tile, out BuildSpec spec, out long entityId)`: lo spec dell'edificio o nastro che occupa la cella.
  - `bool RemoveInstant(GridPosition tile, bool refund)`.
- `FactoryContent`:
  - `float BuildWeight(string itemId)` (default 1.0);
  - `UnitDefinition Drone`, con `public sealed record UnitDefinition(float BuildSpeed, float BuildRange, float Speed)`;
  - `float BuildCostMultiplier(string structureId)` (default 1).
- `ConstructSite`:
  - campi: `long Id`; `BuildSpec Spec`; `float Progress`; `int[] Spent`; `int[] Refunded`; `IReadOnlyList<CostEntry> Cost`; `bool Deconstructing`; `int RefundPercent` (la percentuale effettiva per lo smontaggio in corso); `int OwnerPlayerId`; `CostEntry? Missing` (la voce che ha fermato l'ultimo tick, altrimenti null);
  - `GameState.Sites : List<ConstructSite>` in ordine di id.
- `Construction` (statica):
  - `float BuildCost(FactoryContent c, BuildSpec spec)`;
  - `ConstructStep Advance(GameState s, ConstructSite site, float buildSpeed)`, con `enum ConstructStep { Progressed, Stalled, Finished }`;
  - `ConstructSite BeginBuild(GameState s, BuildSpec spec, int ownerPlayerId)` (assegna l'id da `NextEntityId`);
  - `ConstructSite BeginDeconstruct(GameState s, GridPosition tile, int ownerPlayerId)`: toglie l'entità senza rimborso; il sito ha `Progress = 1`, `Spent = costo intero` e `RefundPercent = Rules.RefundPercent × refundPercent(edificio) / 100`;
  - `void Cancel(ConstructSite site)`: `Deconstructing = true`, `RefundPercent = 100`.
- **Occupazione:** `CanOccupyFootprint`, `IsBuildingTile` e i controlli dei nastri considerano occupate anche le celle di `Sites`. `BeltGrid` non contiene i nastri in costruzione, quindi gli oggetti a monte si fermano (verifica nel task 2).

- [ ] **Step 1: test rossi** in `ConstructionTests` (tutti con `GameRules(InstantBuild: false)` e `Construction.Advance` chiamato a mano con velocità 0.5):

```csharp
[Fact] void MinerBuildTicksMatchFormula()            // miner: ⌈(20 + 4×0.7)/0.5⌉ = 46 tick (±1) fino a Finished; poi esiste 1 Miner e 0 Sites
[Fact] void ConveyorBuildTicksMatchFormula()         // conveyor-basic: ⌈(20+0.7)/0.5⌉ = 42 (±1)
[Fact] void ConsumptionIsGradual()                   // miner: a metà tick Spent[iron-plate] ∈ [1,3]; a Finished esattamente 4 prelevati dal Wallet
[Fact] void SiteStallsWithoutMaterialsAndResumes()   // wallet 2 iron-plate: si ferma (Stalled, Missing = iron-plate), Progress ≤ 0.5; aggiunti 2 → Finished
[Fact] void SiteStallsWhenMaterialsSoldMidBuild()    // a metà costruzione il Wallet perde i materiali → Stalled, materiali mai < 0; riaggiunti → Finished
[Fact] void FinishedBuildingRefundFollowsRule()      // RefundPercent 100 → +4 iron-plate a fine smontaggio; RefundPercent 50 → +2
[Fact] void CancelledSiteRefundsAllSpent()           // costruzione fino a Spent=2, Cancel → al termine +2, nessun Miner
[Fact] void BridgeCostCountsBothCells()              // CostOf(ponte) = 2 × costo di conveyor-bridge
[Fact] void SiteCellsAreOccupied()                   // CanPlace su una cella del cantiere → false, "Cella occupata o non valida"
[Fact] void PlaceInstantWithChargeMatchesTryPlace()  // stessa fabbrica: PlaceInstant(spec, true) e TryPlaceMiner danno lo stesso Wallet e lo stesso Capture
[Fact] void CampaignRefundPercentFromLevel()         // NewGame.Campaign: livello 1 → 100, livello 4 → 50
```

- [ ] **Step 2:** `dotnet test --filter ConstructionTests` → rossi (tipi mancanti).
- [ ] **Step 3: dati.** In `content.json` aggiungi `"buildWeights": {"iron-plate": 0.7, "copper-wire": 0.5, "copper-ore": 0.5}` e `"units": {"drone": {"buildSpeed": 0.5, "buildRange": 27.5, "speed": 22.5}}`. In `campaign.json` aggiungi `"refundPercent"` a ogni livello: 100 per i livelli 01–03, 50 per i livelli 04–10. Aggiungi DTO, validazione e un controllo in `ContentValidationTests` (pesi > 0, drone presente).
- [ ] **Step 4: implementa** `GameRules`, `BuildSpec`, `CostEntry`, i primitivi di `Factory` (lo `switch` per tipo oggi in `SimHost.ApplyPlaceBuilding` passa in `Factory.PlaceInstant`, e `SimHost` lo chiama) e `ConstructSite`/`Construction`.
  - `Advance` segue questo algoritmo, valido per costruzione e smontaggio:

```
// costruzione
target = min(1, Progress + buildSpeed / BuildCost)
per ogni voce i con A_i > 0: target = min(target, (Spent_i + disponibile_i) / A_i)
if target <= Progress: Missing = prima voce che limita; return Stalled
per ogni voce: take = clamp(ceil(target × A_i) − Spent_i, 0, disponibile_i)   // a target == 1: A_i − Spent_i
               preleva take dal Wallet (denaro con TrySpend, materiali con TryRemoveMaterial); Spent_i += take
Progress = target; Missing = null
if Progress >= 1: rimuovi il sito, PlaceInstant(Spec, charge: false), return Finished
// smontaggio
Progress = max(0, Progress − buildSpeed / BuildCost)
R_i = floor(Spent_i × RefundPercent / 100)
per ogni voce: give = floor((1 − Progress) × R_i) − Refunded_i (a Progress == 0: R_i − Refunded_i); Wallet += give; Refunded_i += give
if Progress <= 0: rimuovi il sito, return Finished
```

  - `NewGame.Sandbox/Campaign/Demo` ricevono `GameRules? rules = null`:
    - `Sandbox` usa `RefundPercent 100`;
    - `Campaign` usa il `RefundPercent` del livello;
    - `Demo` usa `InstantBuild: true`;
    - l'`InstantBuild` passato dal chiamante vince.
  - `Factory` creata direttamente nei test: `Rules = new GameRules()`, cioè `InstantBuild false`. I test esistenti però usano `TryPlace…`, che resta istantaneo, quindi non cambiano.
  - Elimina il "riposizionamento gratuito" (miner, forno, assembler, generatore unici spostati senza costo, `Factory.cs` ~1295/1329/1362/1390). Se qualche test esistente lo verifica, aggiornalo e scrivilo nel report.
- [ ] **Step 5:** `dotnet test` → tutti verdi.
- [ ] **Step 6:** VERIFICA 1–6 (i playtest restano identici: nessun cambiamento nel client), CHANGELOG `[Unreleased]` "### Sviluppo" e commit. Apri la PR 1 "feat(sim): cantieri e regole di costruzione (3-1/5)" con base `main`; porta con sé spec e piano.

### Task 2 (PR 2): Drone, piani, sistemi e comandi

**Files:**
- Create:
  - `src/TIndustry.Shared/Construction/BuilderUnit.cs` (con `BuildPlan`)
  - `src/TIndustry.Shared/Simulation/Systems/BuilderSystem.cs`
  - `src/TIndustry.Shared/Simulation/Systems/ConstructSystem.cs`
  - `src/TIndustry.Shared/Simulation/SimEvents.cs`
  - `tests/TIndustry.Shared.Tests/BuilderTests.cs`
- Modify:
  - `Simulation/Simulation.cs` (ordine)
  - `Simulation/GameState.cs` (`Builders`, `Events`)
  - `Simulation/SimHost.cs` (semantica dei comandi, eventi)
  - `Simulation/Commands.cs` (`CommandReasons`)
  - `Simulation/IGameView.cs`
  - `Simulation/NewGame.cs` (drone)
  - `tests/TIndustry.Shared.Tests/SystemOrderTests.cs`, `SimHostTests.cs` e gli altri test con `SimHost` che si aspettano piazzamento istantaneo: usano `GameRules(InstantBuild: true)`, con un commento.
  - `godot/scripts/world/*.cs`: dove si crea la partita, passa `GameRules(InstantBuild: true)` con il commento `// TODO(sp3-task5): costruzione vera`.

**Interfaces (Produces):**
- `public sealed record BuildPlan(BuildSpec Spec, bool Breaking)`. Per `Breaking` conta solo `Spec.Origin` (la cella cliccata).
- `public sealed class BuilderUnit`:
  - `long Id`, `int OwnerPlayerId`, `float X`, `float Y` (centro, in tile), `float Rotation` (gradi);
  - `List<BuildPlan> Plans`, `long? CurrentSiteId`.
- `GameState.Builders : List<BuilderUnit>` in ordine di id.
- `NewGame.*` crea un drone per il giocatore 0 al centro del Core.
- `public abstract record SimEvent(int PlayerId)`:
  - `PlanDropped(int PlayerId, BuildPlan Plan, string Reason)`;
  - `ConstructFinished(int PlayerId, long SiteId, long EntityId)`;
  - `DeconstructFinished(int PlayerId, long SiteId)`.
- I sistemi accodano gli eventi in `GameState.Events : List<SimEvent>`. `SimHost` li svuota dopo ogni tick e li solleva con `event Action<SimEvent>? EventRaised`, con lo stesso isolamento dei gestori di `CommandApplied`. Gli eventi non entrano nell'istantanea.
- `IGameView`:
  - `IReadOnlyList<BuilderUnit> Builders`;
  - `IReadOnlyList<ConstructSite> Sites`;
  - `bool TryGetSiteAt(GridPosition tile, out ConstructSite site)`;
  - `IReadOnlyList<BuildPlan> PlansOf(int playerId)`.
- `Simulation.Systems` = `[BuilderSystem, ConstructSystem, Belt(adv), Power, Mining, Crafting, Extraction, Belt(settle), CoreDelivery]`.

**Comportamento:**
- `ApplyPlaceBuilding`/`ApplyPlaceBelt` con `InstantBuild false` controllano:
  - sblocco ("X bloccato…");
  - `CanPlace`;
  - che le celle non siano già in un piano, di qualunque giocatore ("Cella occupata o non valida").
  - Superati i controlli, accodano `BuildPlan(spec, false)` al drone del giocatore. Con `InstantBuild true` resta il comportamento di oggi, cioè `PlaceInstant(spec, charge: true)` con il controllo delle risorse.
- `Remove(tile)` con `InstantBuild false`, in quest'ordine:
  1. cella in un piano non avviato del giocatore → lo toglie dalla coda;
  2. cella in un cantiere in costruzione → `Construction.Cancel`;
  3. cantiere in smontaggio → nulla;
  4. edificio o nastro → accoda `BuildPlan(spec, Breaking: true)`, se non è già pianificato;
  5. niente → rifiuto silenzioso `NothingToRemove` (come oggi).
- **`BuilderSystem`**, per drone in ordine di id:
  - Senza `CurrentSiteId` e con piani: il bersaglio è il centro del primo piano. Il drone si muove verso il bersaglio di `speed × dt`, senza superarlo, e `Rotation` = direzione del moto.
  - Se la distanza è ≤ `buildRange − 1`, inizia i lavori:
    - piano `Breaking`: se nella cella c'è un edificio o nastro → `BeginDeconstruct`; altrimenti toglie il piano in silenzio;
    - altrimenti: sblocco e `CanPlace`. Se falliscono, toglie il piano ed emette `PlanDropped` con il motivo. Se passano → `BeginBuild` e `CurrentSiteId = sito`.
  - Senza piani torna al punto di riposo.
- **`ConstructSystem`**: per sito in ordine di id, con il suo drone nel raggio → `Advance(drone.BuildSpeed)`.
  - Con `Finished`: toglie il piano in testa, azzera `CurrentSiteId` ed emette `ConstructFinished` o `DeconstructFinished`.
  - Il nuovo edificio prende ricetta e filtro dallo spec.

- [ ] **Step 1: test rossi** in `BuilderTests` (`SimHost` con `GameRules(InstantBuild: false)`):

```csharp
[Fact] void PlaceBuildingQueuesPlanNotBuilding()          // dopo 1 tick: 0 Miner, 1 piano; dopo il volo + 46 tick: 1 Miner
[Fact] void DroneFliesToFarPlanBeforeBuilding()           // piano a 40 tile: nessun avanzamento finché distanza > 26.5; tempo di volo ≈ (40−26.5)/22.5 s
[Fact] void FiftyDraggedBeltsWaitForMaterialsThenFinishInOrder() // 0 materiali: 0 nastri dopo 600 tick; aggiunti i materiali: completati tutti, ordine di ConstructFinished = ordine del trascinamento
[Fact] void PlanOnPlannedCellsIsRejected()                // secondo PlaceBelt sulla stessa cella → CommandRejected "Cella occupata o non valida"
[Fact] void PlanDroppedWhenCellsTakenBeforeArrival()      // piano lontano, cella occupata con PlaceInstant prima dell'arrivo → PlanDropped, il drone passa al piano successivo
[Fact] void RemoveOnSiteUnderConstructionRefundsSpent()   // a metà miner: Remove → alla fine Wallet = iniziale, nessun Miner, nessun sito
[Fact] void RemoveOnQueuedPlanCancelsIt()                 // Remove su piano non avviato → coda vuota, nessun costo
[Fact] void BreakingPlanDeconstructsWithRuleRefund()      // edificio con oggetti dentro, RefundPercent 50 → +metà costo, oggetti persi, DeconstructFinished
[Fact] void ItemsWaitBeforeBeltSite()                     // linea miner → nastri con un nastro in costruzione in mezzo: oggetti a monte fermi davanti, conteggio totale invariato
[Fact] void PlacementDoesNotCheckResources()              // Wallet vuoto: PlaceBuilding → CommandApplied (piano accodato)
[Fact] void InstantBuildKeepsTodayBehaviour()             // InstantBuild true: PlaceBuilding → edificio al tick successivo, costo pagato, "Risorse insufficienti" se mancano
[Fact] void SameCommandsSameChecksum()                    // stessa sequenza con costruzione vera, 10_000 tick, due volte → Checksum uguale
```

- [ ] **Step 2:** `dotnet test --filter BuilderTests` → rossi.
- [ ] **Step 3: implementa** sistemi, comandi, eventi e vista. Aggiorna `SystemOrderTests` alla sequenza nuova, con la dt di ogni passaggio.
- [ ] **Step 4:** `dotnet test` → tutti verdi. `grep -rn "InstantBuild" godot/scripts` mostra solo i punti con `TODO(sp3-task5)`.
- [ ] **Step 5:** VERIFICA 1–6 (screenshot identici alla base, perché il client usa ancora `InstantBuild`), CHANGELOG e commit. PR 2 "feat(sim): drone e piani di costruzione (3-2/5)" con base il branch del task 1.

### Task 3 (PR 3): Istantanea e salvataggi v11

**Files:**
- Modify:
  - `src/TIndustry.Shared/Save/FactorySave.cs` (`CurrentVersion = 11`, DTO)
  - `src/TIndustry.Shared/Factory.cs` (`Capture`/`Restore`)
  - `src/TIndustry.Shared/Simulation/Snapshot.cs`
  - `tests/TIndustry.Shared.Tests/SaveV10Tests.cs` → rinominalo `SaveVersionTests.cs` con `git mv`
- Create: `tests/TIndustry.Shared.Tests/ConstructionSaveTests.cs`

**Interfaces:**
- DTO:
  - `GameRulesSaveDto(int RefundPercent, bool InstantBuild)`;
  - `BuildSpecDto(string StructureId, string? Kind, int X, int Y, string Direction, string? RecipeId, string? FilterItemId)`;
  - `BuildPlanDto(BuildSpecDto Spec, bool Breaking)`;
  - `BuilderSaveDto(long Id, int Owner, float X, float Y, float Rotation, long? CurrentSiteId, List<BuildPlanDto> Plans)`;
  - `SiteSaveDto(long Id, BuildSpecDto Spec, float Progress, List<int> Spent, List<int> Refunded, bool Deconstructing, int RefundPercent, int Owner)`.
- Il costo del sito non si salva: lo si ricalcola con `CostOf(spec)`, nello stesso ordine.
- Gli elenchi sono ordinati per id.

- [ ] **Step 1: test rossi:**

```csharp
[Fact] void SaveMidConstructionKeepsChecksum()     // drone in volo verso il 2° piano, 1° cantiere a metà, 10 piani in coda → Save/TryLoad: Checksum uguale subito e dopo 600 tick
[Fact] void SnapshotRoundTripWithSites()           // Write→Read→Write byte identici con siti in costruzione e in smontaggio
[Fact] void Version10SaveIsRejectedWithClearMessage() // "version": 10 → "Salvataggio di una versione precedente: non compatibile."
```

- [ ] **Step 2:** rossi. **Step 3:** implementa. **Step 4:** `dotnet test` verde.
- [ ] **Step 5:** VERIFICA 1–6, CHANGELOG "### Cambiato" ("salvataggi v11, i v10 non si caricano") e commit. PR 3 "feat(sim): salvataggi v11 con cantieri e drone (3-3/5)" con base il branch del task 2.

### Task 4 (PR 4, parte 1): Grafica dei piani, dei cantieri e del drone

**Files:**
- Create:
  - `godot/shaders/construct.gdshader`
  - `godot/scripts/visuals/construction/ConstructionLayer.cs`, che disegna piani, cantieri, drone e raggio in un unico nodo
  - `godot/scripts/visuals/construction/DroneVisual.cs`
- Modify:
  - `godot/scripts/world/World.Visuals.cs` (crea e aggiorna il livello)
  - `godot/scripts/world/World.Drawing.cs` (anteprima: celle di piani e cantieri occupate)
- Committa i `.uid` generati.

**Comportamento** (valori grafici da Mindustry):
- **Colori:**
  - "accent" `#FFD37F` per piani, raggio e contorno del cantiere;
  - "remove" `#E55454` per smontaggio e piani `Breaking`.
- **Piano:** sprite del blocco (o della freccia del nastro) con alfa 0.35, contorno di 1 px nel colore. Per `Breaking`: contorno rosso e una croce.
- **Shader `construct.gdshader`** (uniform `progress`, `color`, `time`):
  - pixel con `uv.y` rovesciata > `progress`: sagoma scura (alfa 0.25) con il solo contorno;
  - pixel sotto: sprite pieno;
  - linea di scansione larga 1–2 px al fronte, con il colore e un'oscillazione `sin(time)`.
  - Lo smontaggio usa lo stesso shader con il colore rosso e il `progress` attuale.
  - Un sito con `Missing != null` fa pulsare la linea a metà frequenza.
- **Drone:** triangolo con ali disegnato in codice (circa 0.9 tile), colore accent con bordo scuro e scia di 3 punti che svaniscono. Ruota secondo `Rotation`; quando è fermo fluttua di ±0.05 tile su un `sin` del tempo, solo grafico.
  - Il drone si interpola tra due tick con la posizione precedente, così il volo resta fluido a 60+ fps.
- **Raggio:** 4 linee dalla punta del drone agli angoli del cantiere, larghezza 1 px, e un poligono con alfa 0.15. Il colore è quello del sito.
- **Ordine di disegno:** sopra edifici e oggetti, sotto l'HUD.

- [ ] **Step 1:** implementa. Tutto legge solo `IGameView` (`Builders`, `Sites`, `PlansOf(0)`).
- [ ] **Step 2: verifica visiva.** Con `InstantBuild` ancora `true` nel client il drone è visibile fermo accanto al Core. Prova manuale con `dev.sh --profilo-temp --nuova` e una variabile d'ambiente temporanea `TINDUSTRY_REAL_BUILD=1`, che il task 5 rimuoverà: piazza un miner e guarda volo, raggio e shader. Salva 3 catture e allegale al report.
- [ ] **Step 3:** VERIFICA 1–3 e 5. La VERIFICA 4 può mostrare differenze solo dove compare il drone a riposo. Aggiungi in `compare_shots.py` una maschera per quella zona, con un commento. Commit, senza PR: la PR 4 si apre con il task 5.

### Task 5 (PR 4, parte 2): Costruzione vera nel gioco, HUD, tutorial e playtest

**Files:**
- Create:
  - `godot/scripts/ui/hud/BuildQueuePanel.cs`, il riquadro della coda
- Modify:
  - `godot/scripts/world/*.cs` (via `InstantBuild` dal client e via `TINDUSTRY_REAL_BUILD`; gestore di `EventRaised`; tasto destro)
  - `godot/scripts/ui/panels/TutorialPanel.cs` e i testi del tutorial
  - `godot/scripts/dev/World.Playtest.cs`
  - `godot/scripts/world/World.Placement.cs` (`SeedCoreDeliveryLine` con `InstantBuild` locale al seed: crea la linea con `PlaceInstant` diretto, come strumento di sviluppo)

**Comportamento:**
- Il client crea le partite con `InstantBuild = false`.
- I toast:
  - non c'è più "Risorse insufficienti" al piazzamento;
  - `PlanDropped` mostra il motivo;
  - gli altri restano come oggi.
- Tasto destro:
  - su un piano → `Remove`, che lo cancella;
  - su un cantiere → `Remove`, che lo annulla.
- Il trascinamento con il tasto destro su più edifici crea piani `Breaking`, come oggi rimuove più celle.
- **Tooltip in hover su un cantiere fermo:** "Mancano: {n} {nome oggetto}", dove `n` è la quantità che manca per la prossima unità della voce `Missing`.
- **`BuildQueuePanel`:**
  - icona del blocco del cantiere corrente, barra `Progress` (rossa in smontaggio) e "+N" piani rimasti;
  - un clic centra la telecamera sul cantiere;
  - nascosto con la coda vuota;
  - stile Mindustry dell'HUD attuale.
- **Tutorial:** i passi Miner e Forno scattano quando `Miners.Count > 0` o `Smelters.Count > 0`, cioè a edificio finito, come oggi. Il primo passo di piazzamento aggiunge la frase: "Il tuo drone costruisce ciò che pianifichi: guardalo volare e disegnare il blocco."
- **Playtest:** dopo ogni piazzamento il playtest del tutorial aspetta i `ConstructFinished` attesi, con un timeout di 10 s. Il trascinamento dei nastri aspetta la fine della coda prima del controllo `BeltToCore`, con un timeout di 30 s.

- [ ] **Step 1:** implementa. `grep -rn "InstantBuild\|TINDUSTRY_REAL_BUILD" godot/scripts` mostra solo il seed di sviluppo.
- [ ] **Step 2:** `tools/dev/dev.sh --playtest ui` e `--playtest tutorial` → OK.
- [ ] **Step 3:** confronta con `$TEMP/baseline-sp3` e documenta le differenze attese nel report. Il controller invia le catture all'utente. Solo dopo il suo ok le catture diventano `$TEMP/baseline-sp4` per i lavori futuri e le maschere temporanee del task 4 si tolgono.
- [ ] **Step 4:** VERIFICA 1–3, 5 e 6; CHANGELOG "### Aggiunto" ("costruzione come Mindustry: piani, drone, cantieri, smontaggio con rimborso"); commit. PR 4 "feat: costruzione in corso come Mindustry nel gioco (3-4/5)" con base il branch del task 3.

### Task 6 (PR 5): Documentazione

**Files:**
- Create: `docs/decisioni/0003-costruzione-come-mindustry.md`
- Modify: `docs/ARCHITETTURA.md`, `CLAUDE.md`, `godot/README.md`

- [ ] **Step 1: ADR 0003.**
  - Contesto: drone previsto nel sotto-progetto 4 e regola "come Mindustry".
  - Opzioni: cantiere separato / flag sugli edifici / solo grafica.
  - Decisione: cantiere separato e drone automatico.
  - Conseguenze: `InstantBuild` per test e demo; rimborso come regola della partita; salvataggi v11.
  - Stato: accettata 2026-10-17.
- [ ] **Step 2: `ARCHITETTURA.md`.**
  - sezione "Costruzione": piani, drone, cantieri, formula, accumulatore, eventi, ordine dei sistemi aggiornato;
  - "Come aggiungere un blocco": `buildWeights`/`buildCostMultiplier` e `BuildSpec`/`PlaceInstant` al posto dello `switch`.
- [ ] **Step 3:** `CLAUDE.md` passa a salvataggi v11 e aggiunge una riga su `InstantBuild` nei test. `godot/README.md` spiega i controlli: tasto destro su piano e cantiere, coda nell'HUD.
- [ ] **Step 4:** controllo dei link: ogni percorso e simbolo citato esiste.
- [ ] **Step 5:** VERIFICA 1, 2 e 6; CHANGELOG; commit. PR 5 "docs: costruzione come Mindustry e ADR 0003 (3-5/5)" con base il branch del task 5.
