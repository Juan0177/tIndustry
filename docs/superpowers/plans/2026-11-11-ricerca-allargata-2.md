# Ricerca allargata, piano 2: logistica ed energia — piano di implementazione

> **Per gli agenti:** SOTTO-SKILL RICHIESTA: superpowers:subagent-driven-development (consigliata) o superpowers:executing-plans, task per task. I passi usano le caselle (`- [ ]`).

**Obiettivo:** overflow e underflow gate, nastro a pacchi e ponte lungo; pannello solare piccolo e grande, generatore a combustione e batteria grande. Costruibili, sbloccabili in Ricerca, salvati, giocabili dagli agenti e disegnati nel client.

**Architettura:** i gate sono un `LogisticsKind` nuovo che passa l'oggetto dentro `TryInsert` come il selezionatore (nessun oggetto tenuto), con un bit di alternanza per direzione di viaggio. Il nastro a pacchi è un `LogisticsKind` nuovo i cui oggetti portano un numero di pezzi (`Count`), con la cella di carico che raccoglie e la cella finale che scarica solo in edifici e CORE. Il ponte lungo è un `ConveyorDefinition` con la sua portata (`MaxSpan`) e un'entità di corrente che lo mette nella rete. Generatori e batterie diventano per definizione (misura, carburanti, potenza, capacità); il solare è un generatore senza carburante che segue la luce del giorno.

**Tecnologie:** C# .NET 8 (`src/TIndustry.Shared`, xUnit), `data/content.json`, Python 3 (`genera_finanza.py`, `genera.py`), client Godot 4.4 .NET.

**Specifica:** [`docs/superpowers/specs/2026-11-09-ricerca-allargata-design.md`](../specs/2026-11-09-ricerca-allargata-design.md) (sezioni «Logistica» ed «Energia», e la parte di Ricerca, salvataggi, agenti e client).

**Briefing tecnico:** `.superpowers/sdd/2026-11-11-ricerca-allargata-2/briefing.md` (griglia dei nastri, selezionatore, ponte, corrente, client) e i punti da toccare del piano 1 (`.superpowers/sdd/2026-11-11-ricerca-allargata-2/touchpoints.md`).

## Vincoli globali

- **Overflow gate** (`overflow-gate`, «Overflow gate»), 1×1, senza direzione: l'oggetto prosegue dritto (nella direzione in cui viaggia); se davanti non accetta, va a sinistra o a destra alternando; se nessuno accetta, il gate rifiuta e l'oggetto aspetta sul nastro che lo porta.
- **Underflow gate** (`underflow-gate`, «Underflow gate»): al contrario, prima ai lati alternando, dritto solo se i lati non accettano.
- I gate non tengono oggetti, non ricevono da un altro gate o selezionatore (come il selezionatore oggi), consegnano anche a edifici e CORE (via `BuildingReceives`), alternanza in un bit per direzione di viaggio come `SorterTurns` (si salva lì).
- **Nastro a pacchi** (`conveyor-stack`, «Nastro a pacchi»), con direzione, velocità 21,6 celle/s (3 × 7,2 del nastro espresso), un pacco per cella (`capacity` 1, `itemSpacing` 1). Un pacco = fino a **10** pezzi dello stesso oggetto (`TransportedItem.Count`, 1 per gli oggetti normali).
- **Carico**: solo la prima cella di una linea (nessuna cella a pacchi le consegna) accetta oggetti, da nastri, edifici e logistica, come un nastro normale; li aggiunge al pacco in formazione se è lo stesso oggetto e sotto 10, altrimenti rifiuta. Il pacco parte quando ha 10 pezzi oppure 60 tick dopo l'ultimo pezzo entrato (contatore salvato). Le celle a pacchi successive accettano solo pacchi da celle a pacchi dietro di loro.
- **Scarico**: solo l'ultima cella della linea (davanti non c'è una cella a pacchi) scarica, solo in un edificio o nel CORE davanti (mai in un nastro o in un altro blocco logistico); consegna i pezzi uno alla volta finché l'edificio li prende, anche più pezzi per tick; il pacco resta finché non è vuoto.
- **Ponte lungo** (`bridge-long`, «Ponte lungo»): come il ponte di oggi ma portata fino a **8** celle (`maxSpan` del `ConveyorDefinition`, 4 per il ponte di oggi), corrente **1/s**; senza corrente (soddisfazione 0) non passa niente, con corrente parziale passa (nessuna velocità ridotta). L'entrata è un consumatore della rete (entità con id, misura 1, collegabile per contatto e coi laser dei nodi).
- **Pannello solare** (`solar`, «Pannello solare») 1×1, **3/s**; **Pannello solare grande** (`solar-large`, «Pannello solare grande») 3×3, **36/s**; produzione = nominale × `LightAt` / 1000 in interi (millesimi); 0 di notte.
- **Generatore a combustione** (`combustion-generator`, «Generatore a combustione») 2×2, **90/s**; brucia 1 carbone ogni **2 s** oppure 1 grafite ogni **4 s** (carburanti per definizione in `content.json`: `fuels: [{itemId, seconds}]`; il generatore di oggi diventa `fuels: [{coal, 8}]`); buffer di 8 pezzi in tutto; brucia prima il carburante che compare prima nell'elenco.
- **Batteria grande** (`battery-large`, «Batteria grande») 3×3, capacità **20 000**.
- Costi di costruzione (a mano): gate 4 lastre di piombo + 2 fili di rame ciascuno; nastro a pacchi 1 plastica + 1 silicio + 1 grafite per cella; ponte lungo 6 lastre di titanio + 6 lastre di piombo + 2 silicio (per estremità, ×2); solare 6 fili di rame + 4 silicio; solare grande 30 lastre di piombo + 25 silicio + 10 lastre di titanio; generatore a combustione 20 lastre di ferro + 10 lastre di piombo + 6 fili di rame; batteria grande 40 lastre di piombo + 20 lastre di titanio + 10 silicio. `refundPercent` 100.
- Ricerca (primo prerequisito = genitore; fase del foglio): `overflow-gate` ← `router` (fase 1); `underflow-gate` ← `overflow-gate` (fase 1); `bridge-long` ← `conveyor-bridge` (fase 2); `conveyor-stack` ← `conveyor-express` (fase 3); `solar` ← `power-node` (fase 1); `solar-large` ← `solar` (fase 2); `combustion-generator` ← `generator` (fase 1); `battery-large` ← `battery` (fase 2). Costi solo dallo script; costi crescenti lungo ogni catena; ore senza jackpot ≤ 6,5 (minuti piccoli; il riequilibrio è del piano 3).
- Salvataggi **v22**: il primo task che cambia il formato porta `CurrentVersion = MinReadableVersion = 22`; i task dopo aggiungono campi alla v22 senza alzarla ancora (nessuna versione intermedia esce).
- Regole di sempre (CLAUDE.md): determinismo, interi dove si decide, testi solo da `testi.csv` it + en, stili dal tema, sprite solo da `genera.py`, `BuildingKind` e `LogisticsKind` solo in coda, ogni commit verde (sprite, testi, nodi di Ricerca e simboli degli agenti nel task che introduce il blocco).

## Punti da sorvegliare in revisione

1. **Gate in fila e gate davanti a un selezionatore**: un gate non riceve da un gate o selezionatore (nessuna ricorsione); un nastro che porta a un gate bloccato si ferma senza perdere oggetti — Task 1.
2. **Pacco misto**: un oggetto diverso alla cella di carico è rifiutato finché il pacco non parte; un pacco non entra mai in un nastro normale, nemmeno da un router o da un estrattore — Task 2.
3. **Ponte lungo senza corrente al salvataggio**: si salva e si ricarica con gli oggetti fermi sull'entrata; la portata 8 sopravvive al caricamento — Task 3 e Task 6.
4. **Solare all'alba**: produzione intera e monotona durante i 1200 tick di alba e tramonto, mai sopra il nominale; in «solo giorno» sempre piena, in «solo notte» sempre 0 — Task 4.
5. **Generatore a combustione con due carburanti**: con carbone e grafite insieme brucia prima il carbone; il buffer non supera 8 pezzi in tutto; il generatore di oggi resta identico (8 s per carbone, 30/s) — Task 5.

---

### Task 1: Overflow e underflow gate

**File:** `data/content.json` (`conveyors` e `structures`), `Content/ContentTypes.cs` (`LogisticsKind.Gate` in coda; `Inverted` = underflow), `Content/FactoryContent.cs` (`ParseKind`), `Grid/BeltGrid.cs` (`TryInsert`, `WouldAccept`, `FeedsToward`, `HeadOn`, `HasDirection`, passaggio del gate sul modello di `PassSorter`), `Factory.cs` (definizioni, piazzamento via `PlaceLogistics`, cattura/ripristino con `SorterTurns`, rimozione, `TryGetSpecAt`), `Simulation/Commands.cs` (`OverflowGate`, `UnderflowGate` in coda), `Construction/BuildSpec.cs`, `Simulation/PlacementLine.cs` se elenca i blocchi logistici, `Agent/AgentGame.cs` (simboli), sprite in `tools/art/blocchi/logistica.py`, `godot/lingue/testi.csv`, `tools/dev/genera_finanza.py` (nodi), test nuovi `GateTests.cs`

**Interfacce:** `LogisticsKind.Gate`; `BeltGrid.PassGate(cell, travel, item) -> bool`; il client vede il kind nella cella (`MindustryBeltVisual`).

- [ ] **Passo 1: test che falliscono** (`BeltGrid` nudo come in `RouterTests`): `OverflowGoesStraightWhenFrontAccepts`; `OverflowAlternatesSidesWhenFrontBlocked` (4 oggetti: sinistra, destra, sinistra, destra); `OverflowWaitsWhenNothingAccepts` (il nastro che porta resta col suo oggetto, nessuna perdita); `UnderflowPrefersSides`, `UnderflowGoesStraightWhenSidesBlocked`; `GateDoesNotTakeFromGateOrSorter`; `GateDeliversIntoBuilding` (un forno davanti riceve); `GateAlternationSurvivesSave` (bit in `SorterTurns`, round trip di `LogisticsSaveTests`); piazzamento e rimborso via `PlaceBuilding`.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione (con sprite, testi, nodi di Ricerca e `--scrivi-costi`, `ExpectedParent`, simboli degli agenti). **Passo 4:** build 0/0; Shared, AgentPlay, arte, `test_genera_finanza` e `--controlla` verdi.
- [ ] **Passo 5: commit** `feat(logistica): overflow e underflow gate`.

### Task 2: Nastro a pacchi

**File:** `data/content.json`, `Content/ContentTypes.cs` (`LogisticsKind.Stack` in coda), `Content/FactoryContent.cs`, `Grid/TransportedItem.cs` (`Count`, default 1), `Grid/BeltLane.cs` (pacco in formazione e contatore di attesa nella cella di carico), `Grid/BeltGrid.cs` (accettazione solo nella cella di carico, passaggio solo fra celle a pacchi, scarico solo nell'ultima cella verso un edificio o il CORE via `BuildingReceives`, esclusione da `OutputTarget*` verso i nastri e da `WouldAccept` dei blocchi logistici), `Factory.cs`, `Save/FactorySave.cs` (**v22**: `ItemSaveDto.Count` nullo = 1, `BeltSaveDto.StackWaitTicks` nullo = 0), `Commands.cs` (`StackConveyor` in coda — o `PlaceBelt` col suo id se il nastro a pacchi si piazza come un nastro: scegliere il percorso del nastro espresso, che si trascina in linea), `Agent/AgentGame.cs`, sprite (`conveyor-stack` e il pacco sul nastro), testi, nodo di Ricerca; test nuovi `StackConveyorTests.cs`

**Interfacce:** `TransportedItem.Count` (int ≥ 1); `BeltCell.StackWaitTicks`; `BeltGridCell.IsStackLoader` / `IsStackUnloader` (calcolati dalla linea); costante `StackPackSize = 10`, `StackWaitTicks = 60`.

- [ ] **Passo 1: test che falliscono**: `LoaderPacksTenIdenticalItems` (10 ferri → un pacco da 10 parte subito); `PartialPackLeavesAfter60Ticks` (3 ferri → pacco da 3 al tick 60 dall'ultimo); `DifferentItemRefusedWhilePacking`; `MiddleCellsRefuseSideInput` (un nastro laterale non entra in una cella di mezzo); `PackSpeedIsTripleExpress` (un pacco percorre 21,6 celle in 60 tick ±1); `UnloaderDeliversIntoCore` (10 pezzi nel CORE); `UnloaderNeverFeedsABelt` (nastro davanti all'ultima cella: il pacco resta); `PackSurvivesSave` (v22, `Count` e attesa); `V21Refused`.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** tutti i test verdi.
- [ ] **Passo 5: commit** `feat(logistica): nastro a pacchi`.

### Task 3: Ponte lungo

**File:** `data/content.json` (conveyor `bridge-long` con `maxSpan: 8`, `powerUse: 1`; il ponte di oggi `maxSpan: 4`), `Content/ContentTypes.cs` (`ConveyorDefinition.MaxSpan`, `PowerUse`), `FactoryContent.cs`, `Grid/BeltGrid.cs` (portata per definizione al posto di `MaxBridgeSpan`; il passaggio dell'entrata chiede `BridgePowered(position)`), `Factory.cs` (`FindBridgeExit`, refusals, piazzamento, rimozione, cattura e ripristino), entità nuova `Power/BridgePower.cs` (Id, entrata, `PowerRequested`, `SatisfactionBp`), `Simulation/GameState.cs` (lista), `Power/PowerGrid.cs` (consumatore misura 1 all'entrata), `PowerDemandSystem`/`PowerSystem`, `Save/FactorySave.cs` (id dell'entità nel `BeltSaveDto` dell'entrata), `Commands.cs` (`BridgeLong` in coda), `PlacementLine.cs` (ponte automatico solo col ponte di oggi), Godot `MindustryBeltVisual`/`BridgeSpanVisual` (portata per definizione), sprite, testi, nodo; test nuovi `LongBridgeTests.cs`

- [ ] **Passo 1: test che falliscono**: `LongBridgeSpansEight` (uscita a 8 celle accettata, a 9 rifiutata); `OldBridgeStillFour`; `LongBridgeNeedsPower` (senza rete l'oggetto resta sull'entrata; con un generatore passa); `LongBridgeIsAPowerConsumer` (richiesta della rete +1/s); `NodeLaserLinksLongBridge`; `LongBridgeSurvivesSave` (portata, partner, id della corrente, oggetti fermi).
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi.
- [ ] **Passo 5: commit** `feat(logistica): ponte lungo con corrente`.

### Task 4: Pannelli solari

**File:** `data/content.json` (`solar` `powerOutput: 3`, `solar-large` `powerOutput: 36`, campo `solar: true`), entità nuova `Power/SolarPanel.cs` (Id, Position, DefinitionId, Size 1 o 3), `GameState.cs`, `Factory.cs` (piazzamento, rimozione, `FootprintSize`, `TryGetSpecAt`, cattura e ripristino), `Power/PowerGrid.cs` (blocco Generator con misura per istanza), `PowerSystem.cs` (produzione = `PowerOutputRate × LightAt / FullLight`, interi), `Save/FactorySave.cs` (lista `SolarPanels`, nulla se vuota), `Commands.cs` (`Solar`, `SolarLarge` in coda), `BuildSpec.cs`, `AgentGame.cs`, `IGameView.cs`, sprite, testi, nodi; test nuovi `SolarTests.cs`

- [ ] **Passo 1: test che falliscono**: `SolarFullAtNoon` (3000 millesimi/s di giorno), `SolarZeroAtNight`, `SolarHalfAtMidDusk` (a metà tramonto 1500 ±3), `SolarMonotoneAtDusk`, `AlwaysDayAlwaysFull`, `AlwaysNightAlwaysZero`, `LargeSolarIs36AndThreeByThree`, `SolarChargesBattery`, `SolarSurvivesSave`.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi.
- [ ] **Passo 5: commit** `feat(energia): pannelli solari`.

### Task 5: Generatore a combustione e batteria grande

**File:** `data/content.json` (`fuels` sui generatori; `combustion-generator` 90/s; `battery-large` `powerCapacity: 20000`), `Content/ContentTypes.cs` (`FuelDefinition(ItemId, Seconds)`), `FactoryContent.cs` (validazione), `Power/Generator.cs` (definizione per istanza, buffer per carburante in ordine di contenuto, totale ≤ 8, `OutputRateOf(content, id)`), `Power/Battery.cs` (definizione e misura per istanza, `CapacityOf(content, id)`), `PowerGrid.cs` (capacità per batteria, misure per istanza), `PowerSystem.cs`, `GameState.cs` (`ReceiveFromLogistics` coi carburanti), `Factory.cs`, `Save/FactorySave.cs` (v22: carburanti per tipo nel `GeneratorSaveDto`, `DefinitionId` di generatori e batterie), `Commands.cs` (`CombustionGenerator`, `BatteryLarge` in coda), `BuildSpec.cs`, `AgentGame.cs`, sprite, testi, nodi; test nuovi `CombustionGeneratorTests.cs`, `LargeBatteryTests.cs`

- [ ] **Passo 1: test che falliscono**: `OldGeneratorUnchanged` (8 s per carbone, 30/s, stessi tick di oggi); `CombustionBurnsCoalEveryTwoSeconds` e `GraphiteEveryFourSeconds` (pezzi consumati in 60 s: 30 carbone o 15 grafite); `CoalBurnsBeforeGraphite`; `BufferHoldsEightInAll`; `CombustionGives90`; `LargeBatteryHolds20000AndIsThreeByThree`; `MixedBatteriesShareByCapacity` (una piccola e una grande nella stessa rete: carica divisa 1000:20000); `GeneratorsAndBatteriesSurviveSave`.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi.
- [ ] **Passo 5: commit** `feat(energia): generatore a combustione e batteria grande`.

### Task 6: Salvataggi, agenti e determinismo

**File:** `Save/FactorySave.cs` (commento in testa coi formati della v22), `CLAUDE.md` (riga dei salvataggi «versione 22»), `Agent/AgentGame.cs` e `Agent/AgentJournal.cs` (versione del diario se il formato lo richiede, come nel piano 1), `tools/AgentPlay/Tools.cs` e `README.md` (blocchi nuovi), test `SaveRoundTripTests`, `SaveVersionTests`, `AgentGameTests`, `DeterminismTests`

- [ ] **Passo 1: test che falliscono**: v22 avanti e indietro con tutti i blocchi del piano (stesso checksum); v21 rifiutato; `piazza` di ognuno degli otto blocchi; determinismo 10 minuti con gate, pacchi, ponte lungo, solari (ciclo giorno e notte), combustione e batteria grande.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi (Shared, AgentPlay).
- [ ] **Passo 5: commit** `feat(salvataggi): v22 con logistica ed energia nuove, e i blocchi per gli agenti`.

### Task 7: Client

**File:** `godot/scripts/...` come nel piano 1 (palette: Logistica coi gate, il nastro a pacchi e il ponte lungo; Energia coi solari, il generatore a combustione e la batteria grande), `visuals/belts/MindustryBeltVisual.cs` (gate, nastro a pacchi e ponte lungo; portata per definizione), `World.Visuals.cs` (`SyncItemSprites`: il pacco col suo oggetto e un segno del numero di pezzi), visuali di solare (spento di notte), generatore a combustione (fiamma quando brucia), batteria grande (`BatteryVisual` con la capacità per definizione), `NightLayer` (fiamma del generatore), `BlockInfoPanel`/`ResearchPanel`, testi; animazioni negli script dell'arte se servono

- [ ] **Passo 1:** client e arte; build 0/0, `TestiTests`, `ContentValidationTests`, `GodotProjectReferencesTests`, test dell'arte verdi, `genera.py --controlla --pixel` pulito.
- [ ] **Passo 2: commit** `feat(client): logistica ed energia nuove nella palette e nel mondo`.

### Task 8: Documenti, stato e diario

**File:** `docs/wiki/Logistica.md`, `Energia.md`, `Economia-e-Ricerca.md`, `Salvataggi-formato.md`, `Salvataggi.md`, `docs/ARCHITETTURA.md`, `README.md`/`godot/README.md` (versione dei salvataggi, blocchi), `CHANGELOG.md`, `docs/STATO.md` (casella del piano 2, controllo del client sul PC di casa), mappa (`python tools/dev/mappa_canvas.py`), barre (`python tools/sito/stato.py`), diario `docs/diario/2026-11-11-ricerca-allargata-2.md`

- [ ] **Passo 1:** documenti; mappa e barre rigenerate; test del sito verdi.
- [ ] **Passo 2: commit** `docs: Ricerca allargata, piano 2 fatto`.
