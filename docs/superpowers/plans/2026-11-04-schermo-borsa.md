# Schermo di borsa, moduli della Finanza e bilanciamento — piano di implementazione

> **Per gli agenti:** SOTTO-SKILL RICHIESTA: superpowers:subagent-driven-development (consigliata) o superpowers:executing-plans, task per task. I passi usano le caselle (`- [ ]`).

**Obiettivo:** uno schermo di borsa attaccato al CORE accende il jackpot in partita e mostra il mercato di un oggetto; tre moduli della Ricerca (previsione, percentuale, probabilità) aiutano a vendere; i costi della Ricerca si riscalano perché sbloccare tutto richieda circa 6 ore.

**Architettura:** lo schermo è un edificio nuovo (`MarketScreen`) con oggetto seguito e ultimo esito; `GameState.HasMarketScreen` si ricalcola dagli edifici e dal CORE. Le informazioni dei moduli sono letture pure dello stato del mercato (nessun numero casuale nuovo). I costi vengono da un foglio «Bilanciamento» in `tools/dev/genera_finanza.py`, che li scrive e li controlla in `data/content.json`.

**Tecnologie:** C# .NET 8 (`src/TIndustry.Shared`, xUnit), Python 3 (`genera_finanza.py`, openpyxl), `data/content.json`, `data/campaign.json`, client Godot 4.4 .NET, sprite da `tools/art/genera.py`.

**Specifica:** [`docs/superpowers/specs/2026-11-04-schermo-borsa-design.md`](../specs/2026-11-04-schermo-borsa-design.md)

## Vincoli globali

- Schermo `market-screen`: 2×2, senza direzione, senza corrente, **uno solo** (finito o in cantiere), piazzabile solo con una cella che tocca lato contro lato il CORE.
- **Attivo** = finito e a contatto col CORE; `HasMarketScreen` vero solo con uno schermo attivo.
- LED: giallo da `ledYellowPieces` = 10.000 pezzi o col jackpot; blu sotto i 10.000, senza crollo, calo ≤ `ledMildDrop` = 10% di B; viola tutti gli altri.
- Esito mostrato per **6 secondi** (360 tick) dal tick del `SaleResolved`.
- Ramo Finanza in fila: `market-screen` (prerequisito `assembler`) → `finance-forecast` → `finance-percent` → `finance-odds`. Costi di partenza della specifica, poi riscalati dal bilanciamento.
- Moduli visibili solo se sbloccati **e** con uno schermo attivo.
- Bilanciamento: circa 6 ore (5,5–6,5) per sbloccare tutto senza jackpot vendendo il prodotto migliore di ogni fase; riga «Difesa (aspetto 13)» a zero; costi solo dallo script (`--scrivi-costi`, `--controlla`), mai a mano.
- Salvataggi **v20** (`CurrentVersion = MinReadableVersion = 20`); diario degli agenti **v6**.
- Regole di sempre (CLAUDE.md): determinismo, interi, testi da `testi.csv` it+en, stili dal tema, sprite solo da `genera.py`, commenti in inglese, commit in italiano con `Co-Authored-By:`, STATO + mappa + barre.

## Punti da sorvegliare in revisione

1. **Schermo e CORE che cresce**: lo schermo sulle celle nuove blocca il potenziamento come ogni edificio (decisione dell'utente del 4/11); uno schermo staccato si spegne; riattaccarlo (smontare e ripiazzare) lo riaccende — test in Task 1 e 2.
2. **Due «Vendi tutto» nello stesso tick**: l'ultimo esito è quello del secondo, nessun esito per una vendita senza schermo attivo — test in Task 2.
3. **Probabilità mostrata ≠ probabilità usata**: la previsione deve dare esattamente il numero che `Sell` userebbe in quel tick (pity, attesa, fuori picco, sotto 400 pezzi) — test in Task 3.
4. **Costi enormi e somme**: costi in milioni e incassi con jackpot non devono traboccare (`long` o somme sature come in `MarketSales`) — test in Task 5.
5. **Campagna e tutorial ancora giocabili** coi costi nuovi: i livelli che sbloccano Ricerca restano completabili — test in Task 5.

---

### Task 1: Schermo di borsa nella simulazione

**File:** `data/content.json` (struttura `market-screen`, kind `building`, prerequisito `assembler`, costi di partenza; blocco 2×2 con `buildCost` della specifica), `Content/ContentTypes.cs`, `Content/FactoryContent.cs`, `Simulation/Commands.cs` (`BuildingKind.MarketScreen`, `RefusalCode.MarketScreenNotTouchingCore`, `RefusalCode.MarketScreenAlreadyPresent`), `Construction/BuildSpec.cs`, edificio nuovo `Buildings/MarketScreen.cs`, `Factory.cs` (piazzamento, ricalcolo), `Simulation/GameState.cs` (`HasMarketScreen` calcolato), `SimHost.cs`, `godot/lingue/testi.csv` (rifiuti, nome); test nuovi `MarketScreenTests.cs`

**Interfacce prodotte:** `MarketScreen` (`Id`, `Position`, `string? WatchedItemId`, `bool Active`); `Factory.MarketScreen` (lo schermo o null); `Factory.RefreshMarketScreen()` chiamato dopo ogni cambio di edifici o CORE.
- [ ] **Passo 1: test che falliscono**: piazzato a contatto col CORE → cantiere; staccato → `MarketScreenNotTouchingCore`; secondo schermo con il primo in cantiere o finito → `MarketScreenAlreadyPresent`; `HasMarketScreen` falso in cantiere e vero finito; smontato → falso; CORE che cresce sopra lo schermo → rifiutato (`CoreUpgradeBlocked`, dal Task 2); CORE che cresce e lo lascia staccato → `Active` falso; un «Vendi tutto» nel picco da 400 pezzi con lo schermo attivo è un tentativo di jackpot (`SaleResolved.Attempt`), senza no.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** build 0/0, test verdi (Shared, AgentPlay, `TestiTests`).
- [ ] **Passo 5: commit** `feat(mercato): schermo di borsa attaccato al CORE che accende il jackpot`.

### Task 2: Oggetto seguito, ultimo esito e LED

**File:** `Buildings/MarketScreen.cs` (ultimo esito), `Economy/MarketSales.cs` (esito al `SaleResolved` di un «Vendi tutto» con schermo attivo), `data/content.json` (`marketRules.ledMildDrop: 0.10`, `marketRules.ledYellowPieces: 10000`), `Content/ContentTypes.cs` (`MarketRules`), `SimHost.cs` (`SetBlockFilter` accetta lo schermo; rifiuto per oggetto non del Mercato), `Simulation/IGameView.cs`; test nuovi `MarketScreenLedTests.cs`

**Interfacce prodotte:** `enum ScreenLed { Blue, Purple, Yellow }`; `record ScreenSaleResult(string ItemId, long Income, ScreenLed Led, bool Jackpot, long Tick)`; `MarketScreen.LastResult`; `MarketSales.LedFor(MarketRules r, int pieces, bool crash, bool jackpot, int dropMilliOfB) -> ScreenLed`; costante `MarketScreen.ResultTicks = 360`.
- [ ] **Passo 1: test che falliscono**: i casi del foglio «Esempi di vendita» (blu con calo ≤ 10% di B, viola oltre, viola col crollo sotto 10.000, giallo da 10.000 vinto o perso, giallo col jackpot); `SetBlockFilter` sullo schermo imposta e toglie l'oggetto; oggetto sconosciuto rifiutato; «Vendi 100» non lascia esito; «Vendi tutto» senza schermo attivo non lascia esito; due «Vendi tutto» nello stesso tick → esito del secondo.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi.
- [ ] **Passo 5: commit** `feat(mercato): oggetto seguito, esito del «Vendi tutto» e LED dello schermo`.

### Task 3: Moduli della Finanza

**File:** `data/content.json` (nodi `finance-forecast` ← `market-screen`, `finance-percent` ← `finance-forecast`, `finance-odds` ← `finance-percent`, kind nuovo `module` senza blocco, costi di partenza), `Content/ContentTypes.cs`, `Economy/MarketSales.cs` (`JackpotChance` usata anche dalla lettura), lettura nuova `Economy/MarketInsight.cs`, `Simulation/IGameView.cs`, client della Ricerca solo se serve per mostrare un nodo senza blocco (`TechTreeCanvas`); test nuovi `MarketInsightTests.cs`

**Interfacce prodotte:** `record MarketInsight(int? SecondsToSpike, int? SecondsToCrashEnd, int? SecondsSellAllBlocked, bool SellNow, int? PercentOverB, int? JackpotChancePercent)`; `IGameView.MarketInsight(string itemId)` (campi a null se il modulo non è sbloccato o se non c'è uno schermo attivo); `IGameView.IsResearched(string nodeId)` se non esiste già.
- [ ] **Passo 1: test che falliscono**: senza Ricerca tutto null; con la Ricerca ma senza schermo attivo tutto null; previsione: secondi al picco = (`NextSpikeTick` − tick) / 60 per eccesso, `SellNow` vero solo nel picco (prezzo ≥ soglia), fine del crollo, attesa globale; percentuale per difetto (prezzo 2,85×B → 185); probabilità uguale a quella che `Sell` usa in quel tick con tutti i pezzi del CORE (pity a 1 e 2 mancati, 0 fuori dal picco, 0 sotto 400 pezzi, 0 durante l'attesa); la lettura non cambia il checksum.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi.
- [ ] **Passo 5: commit** `feat(mercato): moduli previsione, percentuale e probabilità nella Ricerca`.

### Task 4: Salvataggi v20 e agenti

**File:** `Save/FactorySave.cs` (v20: oggetto seguito e ultimo esito dello schermo), `Factory.cs` (snapshot/restore), `Agent/AgentGame.cs` (`stato.schermo`, moduli per oggetto `previsione`/`percentuale`/`probabilita_jackpot`, `piazza` e `filtro` sullo schermo), `Agent/AgentJournal.cs` (v6), `tools/AgentPlay/Tools.cs` e `README.md`, `CLAUDE.md` (riga dei salvataggi: «versione 20»); test `SaveRoundTripTests`, `SaveVersionTests`, `AgentGameTests`, `DeterminismTests`
- [ ] **Passo 1: test che falliscono**: v20 avanti e indietro con schermo, oggetto seguito ed esito (stesso checksum); v19 rifiutato; `stato` senza moduli finché non sbloccati; `piazza market-screen` e `filtro` sullo schermo; determinismo 10 minuti con schermo e «Vendi tutto».
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi.
- [ ] **Passo 5: commit** `feat(mercato): salvataggi v20 e schermo di borsa per gli agenti`.

### Task 5: Bilanciamento

**File:** `tools/dev/genera_finanza.py` (foglio «Bilanciamento»: fasi della specifica con prodotto, pezzi/s, incasso; per nodo fase, minuti di lavoro, costo in soldi e materiali; somme a 6 ore; tempo senza jackpot e con 1 o 2 jackpot; riga «Difesa (aspetto 13)» a zero; opzioni `--scrivi-costi CONTENT` e `--controlla CONTENT`), test nuovo `tools/dev/test_genera_finanza.py`, `data/content.json` (costi scritti dallo script), `data/campaign.json` (soldi iniziali e obiettivi solo dove serve), test della campagna e del tutorial (`CampaignTests` e simili), test C# nuovo `ResearchCostTests.cs`
- [ ] **Passo 1: test che falliscono**: Python: il foglio dà fra 5,5 e 6,5 ore senza jackpot; `--controlla` fallisce se un costo in `content.json` differisce dal foglio e passa dopo `--scrivi-costi`; costi crescenti lungo ogni catena di prerequisiti. C#: somma dei soldi della Ricerca fra 22 e 25 milioni; nessun trabocco nello sblocco con soldi vicini a `long`/`int` massimo (portafoglio e costi nel tipo che usa `EconomyWallet`).
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione; poi `python tools/dev/genera_finanza.py --scrivi-costi data/content.json` e i ritocchi di `campaign.json` fino a test della campagna verdi e `dotnet run --project tools/MapTool -- valida` OK.
- [ ] **Passo 4:** test verdi (Python, Shared, AgentPlay).
- [ ] **Passo 5: commit** `feat(economia): costi della Ricerca riscalati per circa 6 ore di partita`.

### Task 6: Client

**File:** `tools/art/blocchi/` (sprite `market-screen` e animazione dei LED) e PNG rigenerati, `tools/art/test_genera.py`, visuale nuova `godot/scripts/world/MarketScreenVisual.cs` (grafico, prezzo, righe dei moduli, LED e «JACKPOT» per 6 s, «Staccato dal CORE», «Scegli un oggetto»), `World.Input.cs`/`World.Panels.cs` (clic sullo schermo apre il pannello del filtro), pannello panoramica del Mercato (colonne dei moduli), palette, `UiColors.cs` (LED blu, viola, giallo nelle tre varianti), `godot/lingue/testi.csv`
- [ ] **Passo 1:** client e testi; build 0/0, `TestiTests` verdi, `python tools/art/genera.py --controlla` pulito.
- [ ] **Passo 2 (PC di casa):** playtest `ui`, `tutorial`, `agenti`; a mano: schermo attaccato e staccato, oggetto seguito, «Vendi tutto» con i tre LED, moduli nella panoramica.
- [ ] **Passo 3: commit** `feat(client): schermo di borsa, LED e moduli della Finanza`.

### Task 7: Documenti, stato e diario

**File:** `docs/wiki/Mercato-come-una-borsa.md` (lo schermo, il jackpot ora in partita), `docs/wiki/Economia-e-Ricerca.md` (ramo Finanza, costi nuovi), `docs/wiki/Salvataggi-formato.md` (v20), `godot/README.md`, `README.md`, `docs/ARCHITETTURA.md`, `docs/finanza/mercato-in-breve.md`, `CHANGELOG.md`, `docs/STATO.md` (aspetto 6: specifica 2a approvata e attuata; specifica 2b da fare), mappa, barre, diario `docs/diario/2026-11-04-schermo-borsa.md`, specifica «attuata», commento di chiusura della 2a in #268.
- [ ] **Passo 1:** testi coi numeri dei vincoli e dei costi scritti dallo script.
- [ ] **Passo 2:** `python tools/sito/stato.py`, `python tools/dev/mappa_canvas.py`, `python tools/dev/controlla_wiki.py`, test del sito.
- [ ] **Passo 3: commit** `docs: schermo di borsa e bilanciamento`.
