# Magazzino del CORE e livelli — piano di implementazione

> **Per gli agenti:** SOTTO-SKILL RICHIESTA: superpowers:subagent-driven-development (consigliata) o superpowers:executing-plans, task per task. I passi usano le caselle (`- [ ]`).

**Obiettivo:** CORE a tre livelli (shard 3×3, foundation 4×4, nucleus 5×5) con un massimo per oggetto, potenziamento piazzato sopra il CORE dal ramo CORE della Ricerca, drone alpha/beta/gamma, vendita automatica a ritmo con la spunta «solo a CORE pieno», container e vault, salvataggi v15.

**Architettura:** tutto ciò che entra nel CORE passa da un solo punto (`CoreStorage`), che conosce livello, massimo e magazzini collegati; chi consegna tiene il pezzo quando il CORE dice no, ed è così che le linee si fermano. Il potenziamento è un cantiere normale le cui celle sono solo quelle nuove; a fine lavori cambia `CoreTier` e ricalcola le celle del CORE. Tutto intero e deterministico.

**Tecnologie:** C# .NET 8 (`src/TIndustry.Shared`, xUnit in `tests/TIndustry.Shared.Tests`), dati in `data/content.json`, client Godot 4.4 .NET, sprite da `tools/art/genera.py`, mappe con `tools/MapTool`.

**Specifica:** [`docs/superpowers/specs/2026-10-23-magazzino-core-design.md`](../specs/2026-10-23-magazzino-core-design.md)

## Vincoli globali

- Livelli: shard 3×3 / 1.000 per oggetto / 10 pezzi/s / alpha; foundation 4×4 / 4.000 / 25/s / beta; nucleus 5×5 / 12.000 / 60/s / gamma.
- Droni: alpha costruzione 0,5 velocità 22,5; beta 0,75 / 24,75; gamma 1,0 / 26,6; raggio 27,5 per tutti.
- Foundation: Ricerca 5.000 $ + 200 `iron-plate`, 100 `copper-wire`; costruzione 800 `iron-plate`, 500 `copper-wire`, 400 `lead-plate`.
- Nucleus: Ricerca 25.000 $ + 400 `titanium-plate`, 300 `silicon`; costruzione 3.000 `iron-plate`, 2.000 `lead-plate`, 1.500 `silicon`, 1.000 `titanium-plate`.
- Container 2×2, 300 per oggetto; Ricerca 400 $ + 20 `iron-plate`, 10 `lead-plate`, dopo `junction`; costruzione 40 `iron-plate`, 20 `lead-plate`.
- Vault 3×3, 1.000 per oggetto; Ricerca 900 $ + 20 `titanium-plate`, dopo `container`; costruzione 60 `titanium-plate`, 40 `iron-plate`.
- Crescita: shard (x, y) → foundation (x, y); foundation (x, y) → nucleus (x−1, y−1).
- Salvataggi: `CurrentVersion = 15`, `MinReadableVersion = 15`.
- Regole di sempre (CLAUDE.md): niente `Dictionary`/`HashSet` in ordine d'iterazione dentro `Step()`, niente float per i conti del magazzino, testi solo da `godot/lingue/testi.csv`, stili solo dal tema, commenti in inglese, commit in italiano con `Co-Authored-By:`, una PR per task, `docs/STATO.md` e mappa aggiornati in ogni PR, nessun trigger `push`/`pull_request` nei workflow.
- Build `dotnet build tIndustry.sln -c Release -nologo -v q`: 0 errori, 0 avvisi; `dotnet test tests/TIndustry.Shared.Tests -c Release --no-build` verde a fine di ogni task.

## Punti da sorvegliare in revisione

1. **Edificio attaccato al CORE con l'uscita piena**: un forno che produce lastre col CORE pieno di lastre deve fermarsi (buffer d'uscita pieno) e non perdere pezzi né duplicarli — test in Task 2.
2. **Estrattore col CORE pieno**: se il pezzo estratto non entra nel nastro torna nel CORE; non deve andare perso nemmeno col CORE al massimo (rientro senza limite) — test in Task 2.
3. **Nastro che punta al CORE con due oggetti diversi in coda**: col ferro pieno il pezzo di ferro in testa blocca anche il rame dietro (come Mindustry); non si salta la coda — test in Task 2.
4. **Potenziamento mentre un cantiere o un piano del giocatore copre una cella della fila nuova**: rifiutato con area occupata, anche se il cantiere è dell'altro drone o in smontaggio — test in Task 4.
5. **Salvataggio durante il cantiere del potenziamento**: ricaricato, il cantiere riprende e a fine lavori il CORE cresce una volta sola — test in Task 6.

---

### Task 1: Dati dei livelli, dei droni e dei magazzini

**File:**
- Modifica: `data/content.json` (`cores`, `units`, `structures`; via `economy.coreUpgrade` e la frase sul CORE in `refundPolicyNote` resta com'è)
- Modifica: `src/TIndustry.Shared/Content/ContentTypes.cs`, `src/TIndustry.Shared/Content/FactoryContent.cs`
- Test: `tests/TIndustry.Shared.Tests/ContentValidationTests.cs`

**Interfacce:**
- Produce: `sealed record CoreTierDefinition(string Id, int Size, int ItemCapacity, int AutoSellPerSecond, string UnitId, int UnlockMoney, IReadOnlyList<ResourceAmount> UnlockCost, IReadOnlyList<ResourceAmount> BuildCost)`; `FactoryContent.CoreTiers : IReadOnlyList<CoreTierDefinition>` (indice = tier 0..2); `FactoryContent.Units : IReadOnlyDictionary<string, UnitDefinition>` con `alpha`, `beta`, `gamma`; `FactoryContent.UnitForTier(int tier) : UnitDefinition`; `StructureDefinition` con `Kind` `"storage"` (campo nuovo `ItemCapacity : int`, 0 altrove) e `"core-upgrade"`.
- Rimuove: `FactoryContent.CoreUpgrade`, `CoreUpgradeDefinition`, `FactoryContent.Drone` (i due chiamanti in `ConstructSystem`/`BuilderSystem` passano a `UnitForTier(state.CoreTier)` nel Task 2; qui usano `UnitForTier(0)`).

- [ ] **Passo 1: test che falliscono** in `ContentValidationTests`:
  - `CoreTiers_HaveSpecValues`: tre livelli con (Size, ItemCapacity, AutoSellPerSecond, UnitId) = (3, 1000, 10, "alpha"), (4, 4000, 25, "beta"), (5, 12000, 60, "gamma"); `BuildCost` del foundation = 800 `iron-plate`, 500 `copper-wire`, 400 `lead-plate`; del nucleus = 3000/2000/1500/1000 come nei vincoli.
  - `CoreBuildCost_FitsPreviousTier`: per ogni livello ≥ 1 ogni riga di `BuildCost` < `ItemCapacity` del livello prima.
  - `Units_MatchMindustry`: alpha (0.5, 27.5, 22.5), beta (0.75, 27.5, 24.75), gamma (1.0, 27.5, 26.6).
  - `StorageStructures`: `container` storage capacità 300 prerequisito `junction`; `vault` storage 1000 prerequisito `container`; `core-foundation` e `core-nucleus` di tipo `core-upgrade`, il secondo con prerequisito `core-foundation`; unlock coi valori dei vincoli.
- [ ] **Passo 2:** `dotnet test … --filter ContentValidationTests` → FAIL (proprietà mancanti).
- [ ] **Passo 3:** dati in `content.json` e mappatura in `FactoryContent` (DTO nuovi `CoreTierDto`, `units` come dizionario). Gli unlock di `core-foundation`/`core-nucleus` stanno nella struttura (Ricerca, come gli altri nodi); `cores[i].buildCost` è il cantiere. La validazione esistente dei prerequisiti accetta la radice `core` come prerequisito.
- [ ] **Passo 4:** build 0 avvisi, test verdi (anche `ResearchTreeContentTests`, `TechTreeLayoutTests`: aggiornare i conteggi dei nodi se li fissano).
- [ ] **Passo 5: commit** `feat(contenuti): livelli del CORE, droni alpha/beta/gamma, container e vault`.

### Task 2: CORE a livelli col massimo e linee che si fermano

**File:**
- Crea: `src/TIndustry.Shared/Economy/CoreStorage.cs`
- Modifica: `Simulation/GameState.cs` (`CoreTiles` diventa sostituibile, `CoreTier`, `CoreOrigin`), `Buildings/CoreStockSink.cs`, `Buildings/Extractor.cs`, `Construction/Construction.cs` (rimborsi), `Factory.cs` (righe con `Wallet.AddMaterial`, nuove partite 3×3, `CoreUpgradeLevel` via), `Simulation/NewGame.cs`, `Simulation/IGameView.cs`, `Simulation/Systems/{ConstructSystem,BuilderSystem}.cs`, `Grid/CampaignMapFile.cs` (`CoreSize = 3`), `data/maps/*.json`/`*.png` solo se `MapTool valida` lo chiede
- Test: crea `tests/TIndustry.Shared.Tests/CoreStorageTests.cs`; aggiorna i test che fissano un CORE 2×2 (`MakeCoreTiles(..., size: 2)`, scenari in `FactoryScenarios.cs`)

**Interfacce:**
- Produce in `GameState`: `int CoreTier { get; internal set; }`, `GridPosition CoreOrigin { get; }`, `IReadOnlySet<GridPosition> CoreTiles { get; }` ricalcolato da `SetCore(GridPosition origin, int tier)`; `CoreStorage Storage { get; }`.
- Produce `CoreStorage` (costruito su `GameState`): `int Capacity(string itemId)`, `bool IsFull(string itemId)`, `bool TryAccept(string itemId)` (aggiunge 1 se sotto il massimo), `int AddClamped(string itemId, int amount)` (rimborsi: ritorna quanto è entrato), `void Return(string itemId, int amount)` (rientro senza limite: pezzo preso e non consegnato), `int LinkedCapacity { get; }` (0 fino al Task 5), `void RecomputeLinks()` (vuoto fino al Task 5).
- Produce in `IGameView`: `int CoreTier`, `int CoreCapacity(string itemId)`.
- `Factory.CenteredCoreOrigin(w, h, coreSize: 3)` di default.

- [ ] **Passo 1: test che falliscono** in `CoreStorageTests` (partite con `NewGame.Sandbox(..., rules: new GameRules(InstantBuild: true))`):
  - `NewGame_StartsWithShard3x3`: `CoreTier == 0`, 9 celle, `CoreCapacity("iron-ore") == 1000`.
  - `BeltIntoFullCore_Stops`: CORE con 1000 `iron-ore`, nastro che porta ferro: dopo 10 s il CORE ha ancora 1000, il nastro è pieno; tolti 5 pezzi (vendita), dopo 1 s ne ha di nuovo 1000.
  - `BeltQueue_HeadBlocksTail`: nastro con ferro in testa e rame dietro, ferro pieno: il rame non entra.
  - `OtherItemsStillEnter`: ferro pieno, rame che arriva su un altro nastro entra.
  - `AdjacentSmelter_StopsWhenPlatesFull`: forno attaccato, `iron-plate` a 1000: il forno smette di consegnare e il suo buffer d'uscita resta pieno, nessun pezzo perso (somma CORE + buffer costante).
  - `Extractor_ReturnsWhenBeltBlocked_EvenIfFull`: CORE pieno di `iron-plate`, estrattore col nastro d'uscita bloccato: il CORE resta a 1000, nessun pezzo perso.
  - `Refund_OverCapacity_IsLost`: CORE a 995 `iron-plate`, smontaggio che rende 20: il CORE va a 1000.
  - `ExistingStockOverCapacity_IsKept`: portafoglio iniziale con 1500 ferro: resta 1500 e non entra niente.
- [ ] **Passo 2:** FAIL.
- [ ] **Passo 3:** `CoreStorage`; ogni `Wallet.AddMaterial` dei consegnatori passa da `TryAccept` (nastri: `CoreStockSink.Drain` lascia il pezzo in `cell.PeekOutput()` e passa alla cella dopo; edifici: `GameState.TryDeliverAdjacent` ritorna false), i rimborsi da `AddClamped`, l'estrattore e i rientri da `Return`. In questo task la vendita automatica all'arrivo resta com'è (cambia nel Task 3). Statistiche del drone da `Content.UnitForTier(state.CoreTier)`.
- [ ] **Passo 4:** `CampaignMapFile.CoreSize = 3`; `dotnet run --project tools/MapTool -- valida` → OK su tutte (se una mappa non passa: `MapTool genera` con gli stessi parametri della sua nota, poi `valida`). `CampaignMapTests` verdi.
- [ ] **Passo 5:** build, tutti i test verdi (aggiornare i test che contavano 4 celle di CORE o un CORE a (9, 14) 2×2).
- [ ] **Passo 6: commit** `feat(core): magazzino con massimo per oggetto, CORE shard 3×3, linee che si fermano`.

### Task 3: Vendita automatica a ritmo e spunta «solo a CORE pieno»

**File:**
- Modifica: `Economy/MarketState.cs` (`MarketItemState`), `Economy/MarketSales.cs`, `Simulation/Systems/MarketSystem.cs` (o un `AutoSellSystem` nuovo dopo `CoreDelivery`, registrato in `Simulation.cs` e in `SystemOrderTests`), `Simulation/Commands.cs`, `Simulation/SimHost.cs`, `Simulation/MarketItemView.cs`, `Buildings/CoreStockSink.cs`, `Simulation/GameState.cs` (niente più `TryAutoSell` all'arrivo)
- Test: `tests/TIndustry.Shared.Tests/AutoSellTests.cs` (riscritto)

**Interfacce:**
- Consuma: `CoreStorage.Capacity`, `CoreTiers[tier].AutoSellPerSecond`.
- Produce: `MarketItemState.AutoSellWhenFull : bool`, `MarketItemState.AutoSellCarryMilli : int`; comando `SetAutoSellWhenFull(int PlayerId, string ItemId, bool Enabled)`; `MarketItemView.AutoSellWhenFull`; `MarketSales.AutoSellTick(GameState s)`.
- Regola: per ogni oggetto in ordine di `Content.Market`, se `AutoSell`: `carry += ritmo × 1000 / 60`, `n = carry / 1000` pezzi interi, `carry %= 1000`; con la spunta `n = min(n, max(0, quantità − (massimo − ritmo)))`, senza `n = min(n, quantità)`; vende `n` al prezzo minimo senza toccare il mercato (stessa funzione di oggi per il prezzo). Senza `AutoSell` il carry torna a 0.

- [ ] **Passo 1: test che falliscono**:
  - `AutoSell_Shard_Sells10PerSecond`: 1000 `iron-plate`, nessuna produzione, 10 s → 900 venduti, soldi = 100 × prezzo minimo, prezzo del mercato invariato.
  - `AutoSell_RateFollowsTier`: con `CoreTier` 1 e 2 (impostato dal test) 25 e 60 al secondo.
  - `AutoSell_WhenFull_WaitsUntilNearCapacity`: 500 pezzi con la spunta → 10 s, niente venduto; 1000 pezzi → ne vende, mai sotto 990.
  - `AutoSell_KeepsLineFlowing`: shard, linea che porta 8 pezzi/s di ferro, spunta accesa, CORE pieno: il nastro non si ferma per 60 s.
  - `AutoSell_LineFasterThanRate_Stops`: linea da 15/s, shard: dopo che il CORE è pieno il nastro si riempie.
  - `SetAutoSellWhenFull_UnknownItem_Refused` (`RefusalCode` dei comandi malformati).
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi, `DeterminismTests` verdi.
- [ ] **Passo 5: commit** `feat(mercato): vendita automatica a ritmo del CORE e spunta «solo a CORE pieno»`.

### Task 4: Potenziamento del CORE, ramo CORE della Ricerca, droni per livello

**File:**
- Modifica: `Simulation/Commands.cs` (`BuildingKind.CoreFoundation`, `BuildingKind.CoreNucleus`; via `UpgradeCore`; `RefusalCode` nuovi), `Construction/BuildSpec.cs` (`StructureIdFor`), `Simulation/SimHost.cs`, `Factory.cs` (via `TryUpgradeCore`/`CoreUpgradeRefusal`; `CoreUpgradeSpec`, convalida, `PlaceInstant` per i due kind), `Construction/Construction.cs`, `Research/TechTreeLayout.cs`, `Research/ResearchState.cs` se serve, `Agent/AgentGame.cs` (`potenzia_core` piazza il blocco del livello dopo)
- Test: crea `tests/TIndustry.Shared.Tests/CoreUpgradeTests.cs`; aggiorna `TechTreeLayoutTests`, `SimHostTests`, `AgentGameTests`

**Interfacce:**
- Produce: `RefusalCode.CoreUpgradeLocked`, `RefusalCode.CoreUpgradeInProgress`, `RefusalCode.CoreUpgradeBlocked` (cella occupata o non costruibile), `RefusalCode.CoreUpgradeOutOfMap`, `RefusalCode.CoreMaxTier`; `Factory.CoreUpgradeSpec() : BuildSpec?` (kind, origine e direzione del livello dopo, null al nucleus); `Factory.CoreUpgradeRefusal() : Refusal?` (riscritto coi codici nuovi); `Factory.CoreUpgradeNewTiles() : IReadOnlyList<GridPosition>` in ordine riga per riga.
- Il comando è il normale `PlaceBuilding(PlayerId, BuildingKind.CoreFoundation | CoreNucleus, origin, direction)`: `SimHost` lo accetta solo se `origin == CoreUpgradeSpec().Origin`, altrimenti `CoreUpgradeBlocked`.
- Cantiere: `ConstructSite.Tiles` = solo `CoreUpgradeNewTiles()`; costo = `CoreTiers[tier+1].BuildCost`. `PlaceInstant` per i due kind chiama `GameState.SetCore(nuovaOrigine, tier + 1)`, poi `Storage.RecomputeLinks()`.
- `TechTreeLayout`: i nodi `core-upgrade` in un ramo sotto la radice, primo a sinistra.

- [ ] **Passo 1: test che falliscono**:
  - `Upgrade_LockedUntilResearched` → `CoreUpgradeLocked`.
  - `Upgrade_Foundation_GrowsRightAndDown`: sbloccato, materiali nel CORE, `InstantBuild`: celle (x..x+3, y..y+3), `CoreTier == 1`, capacità 4000, drone con `BuildSpeed 0.75`.
  - `Upgrade_Nucleus_GrowsLeftAndUp_CenteredOnShard`: celle (x−1..x+3, y−1..y+3).
  - `Upgrade_BlockedByBelt` / `ByBuilding` / `ByPlannedSite` / `ByDeconstructingSite` / `ByWall` / `ByDeepWater` → `CoreUpgradeBlocked`; `Upgrade_AtMapEdge` → `CoreUpgradeOutOfMap`.
  - `Upgrade_WrongOrigin_Refused` → `CoreUpgradeBlocked`.
  - `Upgrade_SecondWhileInProgress` → `CoreUpgradeInProgress`; `Upgrade_AfterNucleus` → `CoreMaxTier`.
  - `Upgrade_Site_UsesMaterials_OldCoreWorks` (costruzione vera, niente `InstantBuild`): durante il cantiere i nastri consegnano e il CORE resta 3×3; a fine lavori 800/500/400 spesi.
  - `Upgrade_CancelRefundsAll`.
  - `Upgrade_BeltTouchingNewCells_Delivers`: un nastro che punta a una cella della fila nuova, prima fermo, consegna dopo il potenziamento.
  - `TechTree_CoreBranch`: `core-foundation` figlio della radice, `core-nucleus` figlio di `core-foundation`.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi.
- [ ] **Passo 5: commit** `feat(core): potenziamento piazzato sopra il CORE, ramo CORE della Ricerca, droni beta e gamma`.

### Task 5: Container e vault

**File:**
- Crea: `src/TIndustry.Shared/Buildings/StorageBlock.cs`
- Modifica: `Simulation/Commands.cs` (`BuildingKind.Container`, `BuildingKind.Vault`), `Construction/BuildSpec.cs`, `Factory.cs` (piazzamento, misura, smontaggio, `IsBuildingTile`), `Simulation/GameState.cs` (lista `Storages` in ordine di id, consegna a un magazzino), `Economy/CoreStorage.cs` (`RecomputeLinks`, `LinkedCapacity`), `Buildings/CoreStockSink.cs` (un nastro che punta a un magazzino collegato consegna al CORE; a un magazzino a sé, al magazzino), `Buildings/Extractor.cs` (svuota anche un magazzino a sé che tocca)
- Test: crea `tests/TIndustry.Shared.Tests/StorageBlockTests.cs`

**Interfacce:**
- Produce: `StorageBlock(long id, BuildingKind kind, GridPosition origin, int size, int capacity)` con `bool IsLinked`, `int Count(string itemId)`, `bool TryAccept(string itemId)` (solo a sé), `bool TryTake(string itemId)`, `IReadOnlyList<(string ItemId, int Count)> ContentsInOrder()` (ordinato per id oggetto, ordinale).
- `CoreStorage.RecomputeLinks()`: collegato = almeno una cella del perimetro del magazzino è CORE; un magazzino che diventa collegato versa il contenuto nel CORE con `AddClamped` (resto perso) e si svuota; `LinkedCapacity` = somma delle capacità dei collegati. Chiamato dopo ogni piazzamento/smontaggio completato e dopo `SetCore`.

- [ ] **Passo 1: test che falliscono**:
  - `LinkedVault_AddsCapacity`: vault attaccato allo shard → `CoreCapacity == 2000`; container in più → 2300.
  - `LinkedStorage_BeltDeliversToCore`.
  - `RemovingLinked_LowersCap_KeepsItems`: 1800 ferro, smonto il vault → capacità 1000, ferro resta 1800, nastri fermi.
  - `StandaloneContainer_FillsTo300PerItem_ThenStopsBelt`.
  - `StandaloneContainer_ExtractorEmptiesIt`.
  - `StandaloneDeconstructed_ContentsLost_MaterialsRefunded`.
  - `StandaloneBecomesLinked_OnCoreUpgrade_PoursIn`: container a sé con 300 ferro toccato dal foundation: ferro nel CORE +300 (fino al massimo), container vuoto e collegato.
  - `Upgrade_BlockedByLinkedStorageOnGrowthRow` → `CoreUpgradeBlocked`.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi, `DeterminismTests` verdi.
- [ ] **Passo 5: commit** `feat(logistica): container e vault, collegati al CORE o depositi a sé`.

### Task 6: Salvataggi v15 e determinismo

**File:**
- Modifica: `Save/FactorySave.cs` (`CurrentVersion = 15`, `MinReadableVersion = 15`, campi nuovi, via `CoreUpgradeLevel`/`AutoSellAtCore` e la conversione v11–v14), `Factory.cs` (snapshot/restore), `Simulation/GameState.cs` (checksum: `CoreTier`, origine, magazzini, carry)
- Test: `SaveRoundTripTests`, `SaveVersionTests`, `ConstructionSaveTests`, `DeterminismTests`; via `SaveMigrationTests` e `Fixtures/save-v11.json` (al loro posto un test di rifiuto)

**Interfacce:**
- Campi in `FactorySaveData`: `CoreTier : int`, `CoreX`, `CoreY` (se non già presenti), `Storages : List<StorageSaveData>` (`Id`, `Kind`, `X`, `Y`, `Items : Dictionary<string,int>` scritto in ordine di chiave), per oggetto del mercato `AutoSellWhenFull`, `AutoSellCarryMilli`. `CoreSize` al caricamento deve valere `CoreTiers[CoreTier].Size`, se no `SaveLoadError.Unreadable`.

- [ ] **Passo 1: test che falliscono**:
  - `RoundTrip_FoundationWithStoragesAndAutoSell`: partita con foundation, vault collegato, container a sé con 120 rame, spunta accesa e carry a metà → salva, carica, stesso checksum e stesse viste.
  - `RoundTrip_DuringCoreUpgradeSite`: cantiere del nucleus al 40% → salva, carica, finisce: `CoreTier == 2` una volta sola, materiali spesi una volta.
  - `Version14_Refused_OlderVersion`: file con `"Version": 14` → `SaveLoadError.OlderVersion`.
  - `Determinism_UpgradeStoragesAutoSell`: due corse da 5 minuti con gli stessi comandi → checksum uguali a ogni minuto.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** tutti i test verdi; `dotnet test tests/TIndustry.AgentPlay.Tests -c Release --no-build` verde.
- [ ] **Passo 5: commit** `feat(salvataggi): versione 15 con livello del CORE, magazzini e vendita automatica; le versioni vecchie non si aprono più`.

### Task 7: Client — sprite, palette, panoramica, HUD, testi, agenti

**File:**
- Modifica: `tools/art/genera.py` (+ `tools/art/test_genera.py`): `core-shard` 3×3, `core-foundation` 4×4, `core-nucleus` 5×5, `container` 2×2 e `vault` 3×3 nelle varianti normale e collegata, droni `beta` e `gamma`; rigenerare con `python tools/art/genera.py` e `--controlla`
- Modifica in `godot/`: disegno del CORE dal livello (sprite e misura da `IGameView`), palette (blocchi del CORE sbloccati; fantasma agganciato a `CoreUpgradeSpec().Origin`, celle di `CoreUpgradeNewTiles()` occupate in rosso), panoramica del Mercato (`x/massimo`, spunta accanto all'interruttore, variante di tema per «pieno» con i colori nelle tre varianti in `UiTheme`/`UiColors`), HUD (massimo; via il pulsante «Potenzia CORE»), traduzione dei `RefusalCode` nuovi e di `SaveLoadError.OlderVersion` se il testo cambia
- Modifica: `godot/lingue/testi.csv` (it ed en: nomi di shard/foundation/nucleus/container/vault/beta/gamma, rifiuti nuovi, «solo a CORE pieno», «pieno»), `tools/AgentPlay` (parametro `solo_a_pieno`, `potenzia_core`, `tools/AgentPlay/README.md`)
- Test: `TestiTests`, `GodotProjectReferencesTests`, `tests/TIndustry.AgentPlay.Tests`

- [ ] **Passo 1:** sprite: test in `test_genera.py` per misure e varianti; `python -m unittest tools/art/test_genera.py` FAIL → implementa → PASS; `python tools/art/genera.py --controlla` OK.
- [ ] **Passo 2:** client e testi; `dotnet build tIndustry.sln` 0 avvisi, `TestiTests` verdi.
- [ ] **Passo 3 (solo sul PC di casa, con Godot):** `--import`, `--build-solutions`, `tools/dev/playtest.sh ui` e `agenti` → `PLAYTEST OK, 0 ERROR`; catture del CORE nei tre livelli, del fantasma rifiutato, della panoramica con un oggetto pieno, di container e vault collegati e a sé.
- [ ] **Passo 4: commit** `feat(client): CORE a livelli, magazzini, vendita automatica a CORE pieno`.

### Task 8: Documenti, Wiki, stato e diario

**File:** `docs/wiki/Economia-e-Ricerca.md`, `docs/wiki/Logistica.md`, `docs/wiki/Costruzione.md`, `docs/wiki/Salvataggi.md`, `docs/wiki/Salvataggi-formato.md`, `docs/wiki/Mercato-come-una-borsa.md`, `godot/README.md`, `docs/ARCHITETTURA.md` (ingresso unico `CoreStorage`), `CLAUDE.md` (salvataggi alla v15, nessuna versione vecchia leggibile), `CHANGELOG.md`, `docs/STATO.md` (caselle dell'aspetto 6 e una per container e vault nell'aspetto 3), `docs/mappa-progetto.canvas`, diario `docs/diario/2026-10-2X-magazzino-core.md`, stato della specifica «attuata».

- [ ] **Passo 1:** testi e tabelle coi numeri dei vincoli globali.
- [ ] **Passo 2:** `python tools/sito/stato.py`, `python tools/dev/mappa_canvas.py`, `python tools/dev/controlla_wiki.py`, test del sito verdi.
- [ ] **Passo 3: commit** `docs: magazzino del CORE e livelli`.
