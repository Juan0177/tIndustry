# Mercato come una borsa — piano di implementazione

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ogni oggetto ha un mercato suo con andamento normale, picchi, crolli, vendite al prezzo del clic, jackpot e pity (attivi solo con uno schermo di borsa, che arriva con la specifica 2), vendita automatica per oggetto e una schermata panoramica.

**Architecture:** lo stato del mercato vive in `GameState` (`MarketState`, un `MarketItemState` per oggetto in ordine fisso) e avanza in un sistema nuovo, `MarketSystem`, dopo `CoreDeliverySystem`. Prezzi interi in millesimi di B, probabilità in punti base, caso da un generatore PCG32 scritto nel progetto e salvato. I numeri vengono da `marketRules` in `data/content.json`. Le vendite passano dai comandi `Sell`, `SellAll` e `SetAutoSell` di `SimHost`; l'esito di ogni vendita è un evento `SaleResolved`. Il client legge una vista (`MarketItemView`) da `IGameView`.

**Tech Stack:** .NET 8, `TIndustry.Shared`, xUnit, Godot 4.4.1 .NET, `tools/AgentPlay` (MCP), Python 3 per `tools/dev/genera_finanza.py`.

**Spec:** [docs/superpowers/specs/2026-10-22-mercato-borsa-design.md](../specs/2026-10-22-mercato-borsa-design.md). Numeri ed esempi: `docs/finanza/mercato-e-finanza.xlsx` (si rigenera con `python tools/dev/genera_finanza.py . docs/finanza/mercato-e-finanza.xlsx`).

## Global Constraints

- Simulazione a 60 Hz deterministica: in `Step()` niente tempo reale, niente ordine di `Dictionary`/`HashSet`, niente `System.Random`, niente `float` nello stato del mercato (prezzi in **millesimi di B, interi**; probabilità in **punti base**, 10.000 = 100%), niente formati dipendenti dalla cultura.
- `src/TIndustry.Shared` non dipende da Godot. Namespace `TIndustry.Shared`, `TIndustry.Godot`, `TIndustry.Shared.Tests`.
- Testi visibili solo da `godot/lingue/testi.csv` (`it` ed `en`); la simulazione dà codici (`RefusalCode`). Stili solo dal tema (`UiTheme`, `UiColors`, tre varianti).
- Dati solo in `data/`. Nessun numero del mercato nel codice: tutto da `marketRules`.
- Salvataggi: `FactorySaveData.CurrentVersion = 14` dal Task 6, `MinReadableVersion = 11`.
- Incasso per pezzo `round(B × prezzo)` = `(B × priceMilli + 500) / 1000`; jackpot per pezzo `B + ⌊B × 7,77⌋ + (incasso per pezzo − B)`.
- Una PR per task, unita in `main` prima del task seguente; `main` verde dopo ogni task. Commit e PR in italiano, commenti nel codice in inglese, commit con la riga `Co-Authored-By:` del modello, PR che finisce con `🤖 Generated with [Claude Code](https://claude.com/claude-code)`. GitHub Actions solo a mano (`build-publish.yml` sul ramo).
- Mai i dati reali del giocatore: playtest solo con `tools/dev/playtest.sh` (profilo temporaneo).

**Verifica completa (V)**, ultimo passo di ogni task:

```bash
dotnet build tIndustry.sln -c Release -nologo -v q              # 0 errori, 0 avvisi
dotnet test tests/TIndustry.Shared.Tests -c Release --no-build
dotnet test tests/TIndustry.AgentPlay.Tests -c Release --no-build
dotnet run --project tools/MapTool -- valida
APPDATA="$(mktemp -d)" godot4 --headless --path godot --build-solutions --quit   # dal Task 7; serve Godot
tools/dev/playtest.sh ui "$(mktemp -d)"                         # dal Task 8; PLAYTEST OK, 0 ERROR
```

## Review Focus

1. **Salvataggio a metà picco, a metà crollo o durante l'attesa**: ricaricato, il mercato prosegue identico (stesso checksum dopo 10 minuti). Test nel Task 6 (`SaveMidSpikeCrashAndCooldownContinuesIdentically`).
2. **Due «Vendi tutto» nello stesso tick** (doppio clic, agente): il secondo trova 0 pezzi ed è rifiutato con `SellFailed`, nessuna estrazione e nessuna attesa in più. Test nel Task 4 (`SecondSellAllInSameTickIsRefused`).
3. **«Vendi tutto» senza scorte, su un oggetto fuori mercato o durante l'attesa**: rifiuto, stato del mercato e generatore invariati (stesso checksum). Test nel Task 4 (`RefusedSellAllLeavesMarketUntouched`).
4. **Vecchio salvataggio con la vendita automatica unica accesa**: dopo il caricamento tutti gli oggetti hanno la vendita automatica accesa e i nastri vendono al prezzo minimo. Test nel Task 6 (`OldSaveAutoSellTurnsOnEveryItem`).
5. **Partita lunga con incassi grandi**: 12.000 lastre di titanio con jackpot (oltre 6 milioni) non vanno in overflow di `Money` né di `SaleIncome`. Test nel Task 4 (`BiggestJackpotFitsInMoney`).

---

### Task 1: dati del mercato (`baseValue`, `marketRules`)

**Files:**
- Modify: `data/content.json` (`market[].baseValue`, nuovo blocco `marketRules`), `src/TIndustry.Shared/Content/ContentTypes.cs`, `Content/FactoryContent.cs` (DTO, `MapMarket`, validazione), `Content/MarketCatalog.cs`
- Test: `tests/TIndustry.Shared.Tests/MarketRulesTests.cs` (nuovo)

**Interfaces:**
- Produces: `MarketItemDefinition` con `int BaseValue` (i vecchi `SellPrice`, `MinSellPrice`, `MaxSellPrice`, `SoftStock` restano fino al Task 3).
- Produces: `public sealed record MarketRules(...)` in `ContentTypes.cs`, in `FactoryContent.MarketRules`, coi campi (nome JSON camelCase → C#, valore): `normalMin` 1.0 → `int NormalMinMilli` 1000; `normalMax` 1.6 → 1600; `normalAverage` 1.3 → 1300; `normalStepSeconds` 4; `normalPull` 0.08 → `NormalPullPermille` 80; `normalNoise` 0.06 → `NormalNoiseMilli` 60; `spikeThreshold` 2.8 → 2800; `spikeMax` 3.0 → 3000; `spikeRiseSeconds` 10; `spikeHoldSeconds` 10; `spikeFallSeconds` 30; `spikeIntervalMinMinutes` 10; `spikeIntervalMaxMinutes` 20; `saleImpact` 0.0004 → `SaleImpactMicro` 400 (milionesimi di B per pezzo); `saleRecoverySeconds` 120; `crashPieces` 5000; `crashWindowSeconds` 120; `crashMinutes` 20; `crashDipMinutes` 5; `crashDipMin` 0.6 → 600; `crashDipMax` 0.7 → 700; `crashDipEverySeconds` 10; `crashDipChance` 0.25 → 2500 pb; `jackpotMinPieces` 400; `jackpotBase` 0.02 → 200 pb; `jackpotStepPieces` 200; `jackpotStep` 0.01 → 100 pb; `jackpotCap` 0.5 → 5000 pb; `jackpotBonus` 7.77 → `JackpotBonusPermille` 7770; `pityPieces` 3000; `pityStep` 0.25 → 2500 pb; `pityGuaranteeMisses` 3; `missCooldownSeconds` 300; `missSpikeDelaySeconds` 300; `lostBoost` 0.2 → 2000 pb; `lostBoostMinutes` 5; `lostPieces` 10000; `historySeconds` 120.
- Produces: `int MarketCatalog.BaseValueOf(string itemId)` (1 per un oggetto sconosciuto).

- [ ] **Step 1: test che falliscono** (`MarketRulesTests`): `ShippedRulesMatchTheSpec` (ogni valore sopra, letto da `data/content.json`); `BaseValuesMatchTheSpec` (`iron-ore` 6, `iron-plate` 23, `copper-ore` 5, `copper-wire` 12, `coal` 3, `lead-ore` 4, `lead-plate` 14, `titanium-ore` 14, `titanium-plate` 52, `graphite` 12, `silicon` 32); `MissingBaseValueIsAnItalianError` e `IncoherentRulesAreAnItalianError` (`spikeThreshold` > `spikeMax`, `normalMin` > `normalMax`, `jackpotCap` > 1, `crashDipMin` > `crashDipMax`: `InvalidDataException` col messaggio che nomina il campo).
- [ ] **Step 2:** `dotnet test ... --filter FullyQualifiedName~MarketRules` → FAIL.
- [ ] **Step 3:** dati, record, lettura (conversione a interi con `Math.Round`, `MidpointRounding.AwayFromZero`) e validazione.
- [ ] **Step 4:** V; commit `feat(mercato): valori base e regole del mercato nei dati (1/9)`; PR.

### Task 2: andamento del prezzo e picchi (`MarketRandom`, `MarketState`, `MarketSystem`)

**Files:**
- Create: `src/TIndustry.Shared/Economy/MarketRandom.cs`, `Economy/MarketState.cs` (`MarketState`, `MarketItemState`, `MarketPhase`), `Simulation/Systems/MarketSystem.cs`
- Modify: `Simulation/GameState.cs` (`public MarketState Market { get; }` creato dal contenuto e dal seme), `Simulation/Simulation.cs` (sistema dopo `CoreDeliverySystem`, commento dell'ordine), `docs/ARCHITETTURA.md` (ordine dei sistemi)
- Test: `MarketRandomTests.cs`, `MarketPriceTests.cs` (nuovi), `SystemOrderTests.cs`

**Interfaces:**
- Produces: `public sealed class MarketRandom { MarketRandom(ulong seed); ulong State { get; } ; static MarketRandom FromState(ulong state, ulong inc); uint NextUInt(); int NextInt(int exclusiveMax); int NextRange(int min, int maxInclusive); }` — PCG32 (XSH-RR, moltiplicatore `6364136223846793005`).
- Produces: `public enum MarketPhase { Normal, SpikeRise, SpikeHold, SpikeFall, Crash }`.
- Produces: `public sealed class MarketItemState` con `string ItemId`, `int BaseValue`, `int TrendMilli` (andamento normale), `int PriceMilli` (prezzo del momento), `MarketPhase Phase`, `long PhaseStartTick`, `int SpikeTopMilli`, `long NextSpikeTick`, `long CrashEndTick`, `long BoostEndTick`, `int SaleDropMilli`, `int[] WindowPieces` (anello di `crashWindowSeconds` secondi), `int PityMisses`, `bool AutoSell`, `IReadOnlyList<int> HistoryMilli` (un punto al secondo, `historySeconds` punti).
- Produces: `public sealed class MarketState { IReadOnlyList<MarketItemState> Items; MarketItemState? Find(string itemId); long SellAllBlockedUntilTick; MarketRandom Random; }` — `Items` nell'ordine di `content.market`; `Find` per indice, non per dizionario iterato.
- Regole di `MarketSystem` (per ogni oggetto in ordine, a ogni tick):
  - ogni `normalStepSeconds` un nuovo bersaglio = `clamp(prec + (media − prec) × pull + U(−noise, +noise), normalMin, normalMax)`; `TrendMilli` interpola linearmente verso il bersaglio;
  - allo scadere di `NextSpikeTick` (solo fuori dal crollo): `SpikeTopMilli = U(spikeThreshold, spikeMax)`, salita lineare da `TrendMilli` in `spikeRiseSeconds`, cima `spikeHoldSeconds`, discesa lineare in `spikeFallSeconds`; poi `NextSpikeTick = ora + U(spikeIntervalMin, spikeIntervalMax)` minuti;
  - `PriceMilli = max(normalMin, fase − SaleDropMilli)` fuori dal crollo.

- [ ] **Step 1: test che falliscono.** `MarketRandomTests.KnownSequence` (seme 42: i primi 5 `NextUInt` fissati nel test, calcolati una volta con l'implementazione di riferimento PCG32); `SameSeedSameSequence`. `MarketPriceTests`: `NormalPriceStaysInBand` (10 ore senza vendite: fuori dai picchi sempre 1000–1600 millesimi); `SpikesArriveEveryTenToTwentyMinutes` (intervalli fra inizi di picco ∈ [10, 20] minuti, prima del primo picco fra 10 e 20 dall'inizio); `SpikeShapeIsTenTenThirty` (salita 600 tick, cima 600, discesa 1800; cima ∈ [2800, 3000]); `EverySpikeHasASellingWindow` (almeno un tick ≥ 2800 per picco); `ItemsMoveIndependently` (due oggetti con lo stesso seme non hanno gli stessi picchi); `MarketIsDeterministic` (due `GameState` uguali, stessi prezzi per 1 ora).
- [ ] **Step 2:** `dotnet test ... --filter "FullyQualifiedName~MarketRandom|FullyQualifiedName~MarketPrice"` → FAIL.
- [ ] **Step 3:** implementazione; seme del mercato = seme della partita (`Terrain?.Seed`, 0 senza terreno) mescolato con una costante fissa; `SystemOrderTests` aggiornato con `Market` in fondo.
- [ ] **Step 4:** V; commit `feat(mercato): andamento dei prezzi e picchi per oggetto (2/9)`; PR.

### Task 3: vendite al prezzo del clic, calo e crollo

**Files:**
- Modify: `Simulation/Commands.cs` (`SellAll`), `Simulation/SimHost.cs` (`ApplySell`, `ApplySellAll`), `Factory.cs` (`TrySellFromWallet` passa dal mercato; `TryUpgradeCore` non imposta più il bonus di vendita), `Simulation/GameState.cs` (`EffectiveSalePrice` tolto), `Economy/MarketState.cs`, `Simulation/Systems/MarketSystem.cs`, `Content/MarketCatalog.cs` (tolti `GetDynamicSellPrice`, `SellPrice`, `MinSellPrice`, `MaxSellPrice`, `SoftStock`), `data/content.json` (tolti i campi vecchi e `saleBonusPercent`), `data/campaign.json` (obiettivi `earnMoney` solo se un test lo richiede)
- Test: `MarketSaleTests.cs` (nuovo); aggiornati `FactoryScenarios.SelfTestMercato`, `SelfTestCoreUpgrade`, `SimHostTests` (vendite), `SnapshotTests.SubmitScript`

**Interfaces:**
- Consumes: `MarketState`, `MarketItemState`, `MarketRules` (Task 1–2).
- Produces: `public sealed record SellAll(int PlayerId, string ItemId) : SimCommand(PlayerId);`
- Produces: `public sealed record SaleResolved(int PlayerId, string ItemId, int Pieces, int PriceMilli, int Income, bool Crash, bool Attempt, bool Jackpot, int ProbabilityBasisPoints) : SimEvent(PlayerId);` (in questo task `Attempt` e `Jackpot` sempre `false`, probabilità 0).
- Produces: `internal static int MarketMath.UnitIncome(int baseValue, int priceMilli)` e `internal static SaleOutcome MarketSales.Sell(GameState s, MarketItemState item, int pieces, bool sellAll)` (unico punto da cui passano `Sell` e `SellAll`).
- Regole: incasso = pezzi × `UnitIncome(B, PriceMilli)` al comando; poi `SaleDropMilli += pezzi × 400 / 1000` (millesimi), limitato perché `PriceMilli` non scenda sotto 1000; il calo rientra linearmente in `saleRecoverySeconds`; `WindowPieces` somma i pezzi del secondo in corso; somma della finestra ≥ `crashPieces` → crollo: `Phase = Crash`, `CrashEndTick = ora + crashMinutes`, picco in corso interrotto, `NextSpikeTick` fermo (riprende da dov'era, contato da fine crollo); nel crollo `PriceMilli = 1000 + U(0, 40)`, e nei primi `crashDipMinutes` ogni `crashDipEverySeconds` con probabilità `crashDipChance` scende a `U(600, 700)` per quei secondi. «Potenzia CORE» resta ma non dà più bonus. `EconomySession.RecordSale` si chiama una volta per pezzo con l'incasso vero di quel pezzo (anche del jackpot, dal Task 4), così `SaleIncome` e `SoldByItem` restano il riferimento degli obiettivi della campagna.

- [ ] **Step 1: test che falliscono** (`MarketSaleTests`, con `GameRules(InstantBuild: true)` e prezzo forzato via `MarketItemState` interno): `AllPiecesPaidAtClickPrice` (lastra a 2900: 10 pezzi = 10 × 67); `EachPieceLowersPriceByFourHundredthsOfAPercent` (200 pezzi a 2900 → 2820); `SmallSalesNeverGoBelowB` (2.000 pezzi a 1400 → 1000, nessun crollo); `PriceRecoversInTwoMinutes`; `FiveThousandInTwoMinutesCrash` (3 vendite da 2.000 in 90 s → crollo; le stesse a 3 minuti di distanza → niente crollo); `CrashLastsTwentyMinutesWithoutSpikes` (nessun picco per 72.000 tick, poi il conto alla rovescia riprende da dove era); `CrashDipsOnlyInFirstFiveMinutes` (prezzi < 1000 solo nei primi 18.000 tick, tutti in [600, 700]); `SellAllSellsTheWholeStock`; `SaleResolvedReportsTheSale`; `UpgradeCoreGivesNoSaleBonus`.
- [ ] **Step 2:** → FAIL.
- [ ] **Step 3:** implementazione; `Sell`/`SellAll` rifiutati con `SellFailed` se l'oggetto non è nel mercato o mancano le scorte, `InvalidCommand` se `Amount <= 0`.
- [ ] **Step 4:** V (i test della campagna devono restare verdi: ritocca `earnMoney` in `campaign.json` solo se servono più vendite di quante il livello permetta, e scrivilo nella PR); commit `feat(mercato): vendite al prezzo del clic, calo e crollo (3/9)`; PR.

### Task 4: jackpot, pity e attesa

**Files:**
- Modify: `Economy/MarketState.cs`, `Simulation/Systems/MarketSystem.cs`, la vendita del Task 3, `Simulation/Commands.cs` (`RefusalCode.SellAllCooldown`), `Simulation/GameState.cs` (`HasMarketScreen`)
- Test: `JackpotTests.cs` (nuovo)

**Interfaces:**
- Consumes: `MarketSales.Sell`, `SaleResolved` (Task 3).
- Produces: `public bool GameState.HasMarketScreen { get; internal set; }` (sempre `false` in partita finché la specifica 2 non lo calcola dai blocchi).
- Produces: `internal static int MarketMath.JackpotChance(MarketRules r, int pieces, int pityMisses)` in punti base (`< 400` → 0; altrimenti `min(5000, 200 + ⌊(pezzi − 400) / 200⌋ × 100)`; se pezzi ≥ 3000 si aggiungono `pityMisses × 2500` e con `pityMisses ≥ 3` vale 10.000; tetto 10.000).
- Produces: `internal Func<int, bool>? GameState.JackpotRollForTests` (riceve la probabilità, dice l'esito; `null` = estrazione dal generatore con `NextInt(10000) < probabilità`).
- Regole: tentativo = `SellAll` con pezzi ≥ 400, `PriceMilli` ≥ 2800 e `HasMarketScreen`. Jackpot: incasso per pezzo `B + B × 7770 / 1000 + (UnitIncome − B)`, crollo, `PityMisses = 0`. A vuoto: incasso normale; pezzi ≥ 3000 → `PityMisses++`; con crollo (≥ 5000 nella finestra) e pezzi ≥ 10.000 → `BoostEndTick = ora + lostBoostMinutes` (nella finestra il calo da vendite rientra il 20% più in fretta e la probabilità dei cali sotto B è il 20% più bassa); senza crollo → `SellAllBlockedUntilTick = ora + 300 s` e `NextSpikeTick = ora + U(10, 20) min + 300 s`. Durante l'attesa ogni `SellAll` è rifiutato con `SellAllCooldown`.

- [ ] **Step 1: test che falliscono** (`JackpotTests`): `ChanceTable` (399 → 0, 400 → 200, 600 → 300, 3.000 → 1.500, 10.000 → 5.000, 12.000 → 5.000); `NoAttemptOutsideSpikeBelowFourHundredOrWithoutScreen`; `PityScenarioA` … `PityScenarioE` (i cinque scenari del foglio «Pity», esiti forzati con `JackpotRollForTests`, probabilità attese 2.500/5.000/7.500/10.000, 1.500/4.000/1.500/4.000, 500/1.700/400/4.200/6.700/10.000, 5.000/7.500/10.000, 200/200); `SmallAttemptDoesNotTouchThePitySeries`; `JackpotPaysBasePlusBonusPlusRise` (B 10 forzato, prezzo 3000 → 107 a pezzo); `JackpotCrashesAndResetsPity`; `MissWithoutCrashBlocksSellAllEverywhereAndDelaysThatSpike`; `LostFiftyFiftyGetsTheRecoveryBoost`; e dalla Review Focus `SecondSellAllInSameTickIsRefused`, `RefusedSellAllLeavesMarketUntouched`, `BiggestJackpotFitsInMoney`.
- [ ] **Step 2:** → FAIL.
- [ ] **Step 3:** implementazione.
- [ ] **Step 4:** V; commit `feat(mercato): jackpot, pity e attesa dopo un tentativo a vuoto (4/9)`; PR.

### Task 5: vendita automatica per oggetto

**Files:**
- Modify: `Simulation/Commands.cs` (`SetAutoSell`), `Simulation/SimHost.cs`, `Buildings/CoreStockSink.cs` (`AcceptAtCore`), `Simulation/GameState.cs` (`TryDeliverAdjacent`, tolto `AutoSellAtCore`)
- Test: `AutoSellTests.cs` (nuovo); aggiornati `FactoryScenarios.SelfTestAutoSell`, `SimHostTests` (`SetAutoSell`)

**Interfaces:**
- Produces: `public sealed record SetAutoSell(int PlayerId, string ItemId, bool Enabled) : SimCommand(PlayerId);` (`InvalidCommand` per un oggetto fuori mercato).
- Produces: `internal static bool MarketSales.TryAutoSell(GameState s, string itemId)` — usata dai due percorsi: con `AutoSell` acceso incassa `min(B, UnitIncome(B, PriceMilli))`, registra la vendita in `EconomySession` e non tocca `SaleDropMilli`, la finestra né il generatore.

- [ ] **Step 1: test che falliscono:** `AutoSellIsPerItem` (carbone acceso, ferro spento: il carbone si vende, il ferro va nel CORE); `AutoSellPaysTheMinimumPrice` (prezzo 1.300 → B; crollo a 650 → 0,65×B arrotondato); `AutoSellDoesNotMoveTheMarket` (stesso `PriceMilli` e stessa sequenza del generatore con e senza vendita automatica); `BeltsAndBuildingsSellTheSameWay`.
- [ ] **Step 2:** → FAIL.
- [ ] **Step 3:** implementazione.
- [ ] **Step 4:** V; commit `feat(mercato): vendita automatica per oggetto al prezzo minimo (5/9)`; PR.

### Task 6: salvataggi v14

**Files:**
- Modify: `Save/FactorySave.cs` (`CurrentVersion = 14`, `MarketSaveDto`, `MarketItemSaveDto`), `Factory.cs` (`Capture`, `Restore`)
- Test: `MarketSaveTests.cs` (nuovo), `SaveMigrationTests.cs`

**Interfaces:**
- Produces: `public sealed class MarketSaveDto { ulong RandomState; ulong RandomInc; long SellAllBlockedUntilTick; List<MarketItemSaveDto> Items; }` e `MarketItemSaveDto` con tutti i campi di `MarketItemState` (storia compresa); `FactorySaveData.Market` (`null` nei salvataggi fino al 13). `AutoSellAtCore` e `CoreSaleBonusPercent` si leggono ma non si scrivono più.

- [ ] **Step 1: test che falliscono:** `MarketSurvivesSaveAndLoad` (stato uguale campo per campo); `SaveMidSpikeCrashAndCooldownContinuesIdentically` (Review Focus: salvato a metà picco, a metà crollo e durante l'attesa; dopo 10 minuti stesso checksum della partita mai salvata); `OldSaveStartsAFreshMarket` (v13: prezzi alla media, primo picco fra 10 e 20 minuti); `OldSaveAutoSellTurnsOnEveryItem` (Review Focus).
- [ ] **Step 2:** → FAIL.
- [ ] **Step 3:** implementazione; `SnapshotTests` e `DeterminismTests` verdi col mercato nel checksum.
- [ ] **Step 4:** V; commit `feat(mercato): salvataggi v14 con lo stato del mercato (6/9)`; PR.

### Task 7: vista per il client e agenti

**Files:**
- Modify: `Simulation/IGameView.cs` (tolti `AutoSellAtCore`, `CoreSaleBonusPercent`, `PreviewSellPrice`), `Factory.cs`, `Agent/AgentGame.cs`, `tools/AgentPlay/Tools.cs`, `tools/AgentPlay/Compact.cs`, `tools/AgentPlay/README.md`, `.claude/skills/gioca-e-testa/SKILL.md`; nel client i punti che usavano i membri tolti (`MercatoPanel.cs` resta com'è salvo le chiamate)
- Test: `AgentGameTests.cs`, `tests/TIndustry.AgentPlay.Tests`

**Interfaces:**
- Produces: `public sealed record MarketItemView(string ItemId, int BaseValue, int PriceMilli, int UnitIncome, MarketPhase Phase, int CrashSecondsLeft, bool AutoSell, IReadOnlyList<int> HistoryMilli);` con `MarketItemView? IGameView.MarketItem(string itemId)`, `IReadOnlyList<MarketItemView> IGameView.MarketItems` e `int IGameView.SellAllCooldownSeconds`. Nessun dato dei moduli della specifica 2 (percentuale su B, previsione, probabilità).
- Produces (agenti): in `stato.mercato` per oggetto posseduto `{oggetto, prezzo, prezzoB, fase, crolloSecondi?, automatica}` e `attesaVendiTutto?`; `vendi {oggetto, quantita}` come oggi e `vendi {oggetto, tutto: true}` → `SellAll`, risposta `{incasso, soldi, crollo, tentativo, jackpot}`; nuovo `vendita_automatica {oggetto, accesa}`.

- [ ] **Step 1: test che falliscono:** `StatoReportsTheMarket`; `VendiTuttoUsesSellAll`; `VenditaAutomaticaIsPerItem`; `ReplayGivesSameChecksum` resta verde con i comandi nuovi; in AgentPlay il nuovo strumento compare nell'elenco.
- [ ] **Step 2:** → FAIL.
- [ ] **Step 3:** implementazione; il client compila (`--build-solutions`) con le chiamate aggiornate.
- [ ] **Step 4:** V; commit `feat(mercato): vista del mercato per il client e per gli agenti (7/9)`; PR.

### Task 8: schermata panoramica

**Files:**
- Modify: `godot/scripts/ui/panels/MercatoPanel.cs` (riscritto), `godot/scripts/world/World.Panels.cs`, `World.Commands.cs` (esito da `SaleResolved`, rifiuto `SellAllCooldown`), `godot/scripts/ui/theme/UiTheme.cs` e `UiColors.cs` (varianti per ▲▼, «Crollo» e mini grafico nelle tre varianti), `godot/lingue/testi.csv`, `godot/scripts/dev/World.Playtest.cs` (catture)
- Create: `godot/scripts/ui/panels/MarketSparkline.cs` (+ `.uid`)

**Interfaces:**
- Consumes: `MarketItemView`, `SellAllCooldownSeconds`, `SellAll`, `SetAutoSell`, `SaleResolved` (Task 3–7).
- Produces: per ogni oggetto posseduto una riga: icona, nome, quantità, prezzo in $ con ▲/▼ (icona, non solo colore), `MarketSparkline` sugli ultimi 120 punti, «Crollo · mm:ss» durante il crollo, «Vendi 1», «Vendi 100», «Vendi tutto» (disabilitato con suggerimento `mercato.attesa` e i secondi mancanti durante l'attesa), interruttore `mercato.auto` per oggetto. Nessun pulsante che vende tutto di tutti. Un jackpot (raggiungibile solo nei test finché manca lo schermo) dà un avviso `mercato.jackpot` con l'incasso.
- Testi nuovi (`it` / `en`): `mercato.vendi.cento` «Vendi 100» / «Sell 100»; `mercato.crollo` «Crollo · {0}» / «Crash · {0}»; `mercato.attesa` «Vendi tutto bloccato ancora {0} s» / «Sell all locked for {0} s»; `mercato.auto.oggetto` «Automatica» / «Auto»; `mercato.jackpot` «JACKPOT! +${0}» / «JACKPOT! +${0}»; `rifiuto.SellAllCooldown` «Vendi tutto è bloccato: aspetta {0} s» / «Sell all is locked: wait {0} s». Tolte le chiavi della vendita automatica unica e del consiglio sul listino.

- [ ] **Step 1:** playtest `ui`: sonda `mercato-panoramica` che apre M con tre oggetti (uno normale, uno nel picco, uno in crollo, forzati come nei test) e controlla righe, pulsanti e attesa; catture `ui-mercato`, `ui-mercato-crollo`, `ui-mercato-attesa`, anche in `TINDUSTRY_PLAYTEST_LINGUA=en` e nelle varianti Daltonici e AltoContrasto. Prima della riscrittura la sonda fallisce.
- [ ] **Step 2:** riscrittura del pannello e del grafico; `TestiTests` verde; controllo dei rettangoli della Scala UI verde fino al 150%.
- [ ] **Step 3:** V completa con playtest `ui` e `tutorial`; commit `feat(mercato): schermata panoramica del mercato (8/9)`; PR con le catture (ramo `pr-media`).

### Task 9: documenti e chiusura

**Files:**
- Modify: `docs/ARCHITETTURA.md` (mercato, generatore seminato, comandi), `godot/README.md` (Mercato), `docs/wiki/Economia-e-Ricerca.md`, `docs/wiki/Domande-frequenti.md` (poi `python tools/dev/wiki_navigazione.py` e `controlla_wiki.py` → `wiki: OK`), `README.md` (tabella dei contenuti, roadmap), `CHANGELOG.md`, `docs/idee.md` (lo «stonks» passa alla roadmap), `docs/superpowers/specs/2026-10-22-mercato-borsa-design.md` (Stato: attuata), `tools/dev/mappa_canvas.py` e `docs/mappa-progetto.canvas`
- Create: `docs/diario/2026-10-22-mercato-borsa.md` (dal registro `.superpowers/sdd/<piano>/progress.md`) e la riga nell'indice di `docs/diario/README.md`

- [ ] **Step 1:** documenti; `python tools/dev/genera_finanza.py . docs/finanza/mercato-e-finanza.xlsx` rigenera la cartella di lavoro con gli stessi numeri dei test (controllo a occhio dei fogli «Parametri» e «Valori B» contro `content.json`).
- [ ] **Step 2:** V; commit `docs(mercato): documenti, diario e chiusura del mercato come una borsa (9/9)`; PR; commento sull'issue #268 con ciò che resta alle specifiche 2 e 3.
