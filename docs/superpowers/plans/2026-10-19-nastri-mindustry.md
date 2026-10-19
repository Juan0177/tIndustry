# Nastri come in Mindustry — piano di implementazione

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** piazzare, girare, potenziare e smontare i nastri con i gesti di Mindustry (linea ancora→cursore con anteprima, direzione per cella, nastro sopra nastro, ponte automatico, Ctrl, rettangolo, tasto centrale, rotellina), più il nastro rinforzato e i costi della logistica ribilanciati sui rapporti di Mindustry, con le stesse regole per il giocatore e per gli agenti.

**Architecture:** la linea è una funzione pura di Shared (`PlacementLine.Plan`) che riceve ancora, cursore e modo e restituisce i piazzamenti (direzione, incrocio, ponte, rifiuto previsto); il client la ricalcola per l'anteprima e la invia al rilascio, `piazza` degli agenti la chiama uguale. Nastro sopra nastro (giro sul posto, cambio di tier con cantiere di sostituzione) e nastro rinforzato stanno nella simulazione (`SimHost`, `Factory`, `Construction`, `BeltGrid`), deterministici. Rettangolo, tasto centrale e rotellina sono client; il rettangolo ha la sua funzione di Shared (`RemovalArea`) per `smonta {da, a}`.

**Tech Stack:** .NET 8, `TIndustry.Shared`, xUnit, Godot 4.4.1 .NET (`godot4`), Python 3 (`tools/art`, `tools/dev`, `tools/sito`), `tools/MapTool`.

**Spec:** [docs/superpowers/specs/2026-10-18-nastri-mindustry-design.md](../specs/2026-10-18-nastri-mindustry-design.md) e la ricerca [2026-10-18-nastri-mindustry-ricerca.md](../specs/2026-10-18-nastri-mindustry-ricerca.md).

## Global Constraints

- Simulazione a 60 Hz, deterministica: in `Step()` e in `PlacementLine`/`RemovalArea` niente tempo, niente ordine di `Dictionary`/`HashSet`, niente `Random` non seminato, niente parallelismo, niente formati dipendenti dalla cultura; le code di priorità rompono i pari con un contatore di inserimento.
- `src/TIndustry.Shared` non dipende da Godot né dal sistema operativo. Namespace `TIndustry.Shared`, `TIndustry.Godot`, `TIndustry.Shared.Tests`.
- Testi visibili solo da chiavi di `godot/lingue/testi.csv`, sempre `it` ed `en`; la simulazione dà codici (`RefusalCode`), il client li traduce; `TestiTests` verde.
- Stili solo dal tema (`UiTheme`, colori da `UiColors` nelle tre varianti Normale, Daltonici, AltoContrasto); niente `StyleBoxFlat` né `AddTheme*Override` nei pannelli.
- Sprite solo da `python tools/art/genera.py`, mai PNG a mano; `--controlla` verde. Uno script Godot nuovo si committa col suo `.uid`.
- Dati solo in `data/` (`content.json`, `campaign.json`, `maps/`). I generatori pubblicati non si toccano.
- Salvataggi: `FactorySaveData.CurrentVersion = 13` dal Task 3, `MinReadableVersion = 11` (v11 e v12 si leggono ancora).
- Test che non provano la costruzione: `GameRules(InstantBuild: true)`; quelli che la provano girano anche senza (`[Theory]` su `InstantBuild`).
- Nomi dalla specifica: blocco `conveyor-armored`, "Nastro rinforzato" / "Armored belt"; stessa velocità di `conveyor-fast` (`rateItemsPerSecond` 1.0); si sblocca dopo `conveyor-fast`.
- Agenti con le regole del giocatore: dal Task 4 `piazza` usa solo `PlacementLine`, dal Task 7 `smonta {da, a}` solo `RemovalArea`; nessuna logica di linea propria in `AgentGame` o nel client.
- Mai i dati reali del giocatore (`%LOCALAPPDATA%\tIndustry`, `%APPDATA%\Godot\app_userdata\tIndustry`): playtest solo con `tools/dev/playtest.sh` (profilo temporaneo), `--import` e `--build-solutions` con `APPDATA` temporaneo, poi si ripristinano i `.import` cambiati solo per fine riga (CLAUDE.md).
- Una PR per task, unita in `main` prima del task seguente; `main` verde dopo ogni task. Commit e PR in italiano, commenti nel codice in inglese, commit con `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`, corpo della PR che finisce con `🤖 Generated with [Claude Code](https://claude.com/claude-code)`. Catture nelle PR dal ramo orfano `pr-media` con URL raw.
- GitHub Actions solo a mano: dopo il push `"/c/Program Files/GitHub CLI/gh.exe" workflow run build-publish.yml --ref <ramo>` e `gh run watch <id> --exit-status`.

**Verifica completa (V)**, l'ultimo passo di ogni task prima del commit:

```bash
dotnet build tIndustry.sln -c Release -nologo -v q              # 0 errori, 0 avvisi
dotnet test tests/TIndustry.Shared.Tests -c Release --no-build   # tutti verdi
dotnet test tests/TIndustry.AgentPlay.Tests -c Release --no-build
python -m unittest tools/art/test_genera.py
python tools/art/genera.py --controlla                           # nessuna differenza
APPDATA="$(mktemp -d)" godot4 --headless --path godot --build-solutions --quit
tools/dev/playtest.sh ui "$(mktemp -d)"                          # PLAYTEST OK, 0 ERROR
tools/dev/playtest.sh tutorial "$(mktemp -d)"                    # PLAYTEST OK, 0 ERROR
```

## Review Focus

1. **Trascinamento interrotto** (Esc, destro, un pannello aperto con T/M/G/I, la finestra che perde il fuoco, il rilascio fuori dalla finestra): l'anteprima sparisce e non parte nessun comando; un rilascio normale ovunque invia la linea. Test nel Task 6 (playtest `nastri-interrotto`).
2. **Linee lunghe su mappe grandi** (2000², trascinamento da un capo all'altro, Ctrl in un labirinto di pareti): l'anteprima resta leggera, la linea si ferma a `PlacementLine.MaxCells` = 200 celle e l'A* a `MaxSearchNodes` = 1000 nodi, poi la retta. Test nei Task 4 e 5 (`LineIsCappedAtMaxCells`, `CtrlFallsBackToTheStraightLine`).
3. **Oggetti e risorse durante una sostituzione** (annullata a metà, salvata e ricaricata a metà, al 50% di rimborso): nessun oggetto o materiale creato o perso; l'annullamento rimette il nastro vecchio coi suoi oggetti e rende solo quanto pagato davvero. Test nel Task 3 (`CancellingAReplacementGivesBackTheOldBeltAndOnlyWhatWasPaid`, `ReplacementSiteSurvivesSaveAndLoad`).
4. **Nastro sopra una cella occupata** (nastro segnato da smontare, cantiere, piano di un altro giocatore, incrocio, sdoppiatore, selezionatore, capo di un ponte): rifiuto `Occupied`, niente cambia. Test nel Task 1 (`BusyCellsAreNeverTurnedOrReplaced`, poi esteso nel Task 3).
5. **Nastro rinforzato accanto a logistica che non è un nastro** (sdoppiatore, selezionatore, uscita di un ponte, incrocio sul fianco): non accetta di lato, accetta da dietro, e la forma resta dritta. Test nel Task 8 (`ArmoredRefusesNonBeltLogisticsFromTheSide`).

## Struttura dei file

- `src/TIndustry.Shared/Simulation/PlacementLine.cs` (nuovo): linea pura (retta, Ctrl, catena, direzioni, incroci, ponti, rifiuti previsti) e comandi da inviare.
- `src/TIndustry.Shared/Simulation/RemovalArea.cs` (nuovo): rettangolo di smontaggio e i suoi `Remove`.
- `src/TIndustry.Shared/Simulation/SimHost.cs`, `Factory.cs`, `Construction/*.cs`, `Simulation/Systems/BuilderSystem.cs`: nastro sopra nastro, cantiere di sostituzione.
- `src/TIndustry.Shared/Grid/BeltGrid.cs`, `Buildings/{Miner,Crafter,Extractor}.cs`: regola del rinforzato.
- `src/TIndustry.Shared/Save/FactorySave.cs`: v13.
- Si cancellano: `Simulation/BeltDrag.cs` (Task 6), `Grid/DragLine.cs` (Task 7) e i loro test.
- `godot/scripts/world/World.{Input,Placement,Drawing,Commands}.cs`, `WorldCamera.cs`: gesti del client.
- `godot/scripts/dev/World.PlaytestNastri.cs` (nuovo, + `.uid`): sonde del playtest `ui` per i gesti e il rinforzato.

---

### Task 1: nastro sopra nastro dello stesso tier — giro sul posto e unione senza incrocio

**Files:**
- Modify: `src/TIndustry.Shared/Simulation/Commands.cs` (enum `BeltOverBelt`; tolto `RefusalCode.CrossingNeedsJunction`), `Simulation/IGameView.cs`, `Factory.cs` (`BeltOverBeltAt`, `TryRotateBelt`), `Simulation/SimHost.cs` (`ApplyPlaceBelt`), `Simulation/BeltDrag.cs` (solo il commento)
- Modify: `godot/scripts/world/World.Commands.cs` (via `_crossingToastShown`), `World.Placement.cs`, `World.Drawing.cs` (`GhostAt`: nastro sopra nastro valido), `godot/lingue/testi.csv` (via `rifiuto.CrossingNeedsJunction`), `godot/scripts/dev/World.PlaytestIncrocio.cs`
- Modify: `docs/decisioni/0003-costruzione-come-mindustry.md`, `godot/README.md` (paragrafo "Incrocio automatico"), `tools/AgentPlay/README.md` (codici, linea), `CHANGELOG.md`
- Test: `tests/TIndustry.Shared.Tests/BeltOverBeltTests.cs` (nuovo), `AutoJunctionTests.cs`, `BeltDragTests.cs`

**Interfaces:**
- Produces: `public enum BeltOverBelt { None, Same, Rotate, Replace }` accanto a `BeltCrossing`; `BeltOverBelt IGameView.BeltOverBeltAt(GridPosition cell, Direction direction, string conveyorId)`: `None` se la cella non ha un nastro semplice (`LogisticsKind.Belt`), se `conveyorId` non è un nastro semplice dei contenuti, se un piano copre la cella (`IsPlannedTile`) o c'è un cantiere; poi `Same` (stesso id e direzione), `Rotate` (stesso id, altra direzione); un altro id dà `None` fino al Task 3, che lo fa diventare `Replace`. "Tier" = id del nastro.
- Produces: `internal bool Factory.TryRotateBelt(GridPosition cell, Direction direction)` con `BeltGrid.TryOrient` (tiene gli oggetti, alza `Belts.Version`, quindi la cache delle forme si rifà).
- `SimHost.ApplyPlaceBelt`: spec valido → `Crossing` con `BeltCrossing.Junction` → incrocio come oggi; `JunctionLocked` → prosegue → `BeltOverBeltAt`: `Same` → applicato senza effetto (nessun rifiuto), `Rotate` → `TryRotateBelt`, subito e gratis con e senza `InstantBuild`; altrimenti come oggi. Un incrocio sbloccato ma che non si può pagare resta un rifiuto `InsufficientResources`.

- [ ] **Step 1: test che falliscono.** `BeltOverBeltTests`, ognuno `[Theory]` su `InstantBuild` vero e falso, Sandbox con il Core lontano: `SameTierOtherDirectionTurnsInPlaceKeepingItems` (nastro base Est in (10,10) con un oggetto inserito; `PlaceBelt` base Nord → `Belts[(10,10)].Direction == North`, stesso id d'oggetto sulla cella, portafoglio uguale, `PlansOf(0)` vuoto dopo un tick); `SameTierSameDirectionIsASilentNoOp` (nessun `CommandRejected`, `Checksum()` uguale a prima); `TurningInPlaceRefreshesTheShape` (curva che torna dritta: `GetBeltShape` cambia nel tick stesso); `BusyCellsAreNeverTurnedOrReplaced` (Review Focus 4: nastro segnato con `Remove`, cantiere sulla cella, piano del giocatore 1, incrocio, sdoppiatore, selezionatore, capo di ponte → `Occupied`, cella invariata). In `AutoJunctionTests`: `LockedJunctionLeavesTheBeltAndSaysWhy` diventa `LockedJunctionTurnsTheCrossedBeltIntoAMerge` (incrocio bloccato, linea Est attraverso la colonna Sud in (40,33): nessun rifiuto, `Belts[(40,33)].Direction == East`, `GetBeltShape((40,33)) == BeltShape.JoinLeft`); `ASingleClickOnABeltKeepsTodaysBehaviour` diventa `ASingleClickOnAPerpendicularBeltTurnsIt`. In `BeltDragTests`: `ALockedJunctionIsRefusedAsBefore` diventa `ALockedJunctionMergesLikeTheClientsDrag` (stesso checksum fra agente e client).
- [ ] **Step 2:** `dotnet test tests/TIndustry.Shared.Tests -c Release --filter "FullyQualifiedName~BeltOverBelt|FullyQualifiedName~AutoJunction|FullyQualifiedName~BeltDrag"` → FAIL (`BeltOverBeltAt` non esiste).
- [ ] **Step 3: implementazione** come nelle Interfaces; tolto `CrossingNeedsJunction` (l'ultimo valore dell'enum: gli altri non cambiano numero) e la sua riga di `testi.csv`; nel client via `_crossingToastShown`; `GhostAt` dà il fantasma valido quando `BeltOverBeltAt` non è `None`.
- [ ] **Step 4: playtest.** In `PlaytestAutoJunctionAsync` la parte "bloccato" ora attende `GetBeltShape(lockedCell) == JoinLeft` e la direzione Est, nessun toast di rifiuto, e cattura `unione-senza-incrocio`.
- [ ] **Step 5: documenti.** ADR 0003, "Scelte prese durante l'attuazione": giro sul posto (stesso nastro, altra direzione: subito, gratis, oggetti tenuti) e unione senza incrocio; `godot/README.md` "Incrocio automatico" senza l'avviso, con l'unione 2→1/3→1; README di AgentPlay senza `CrossingNeedsJunction`; CHANGELOG.
- [ ] **Step 6:** V; commit `feat(nastri): giro sul posto e unione senza incrocio (1/9)`; push, workflow, PR.

### Task 2: costi della logistica come Mindustry — **serve l'OK dell'utente sulla tabella prima di eseguirlo**

Valori di Mindustry verificati il 2026-10-19 su `core/src/mindustry/content/Blocks.java`, `Items.java` e `world/Block.java` di Anuken/Mindustry (master): `conveyor` rame 1; `titaniumConveyor` rame 1, piombo 1, titanio 1; `plastaniumConveyor` plastanio 1, silicio 1, grafite 1; `armoredConveyor` plastanio 1, torio 1, vetro metallico 1; `junction` rame 3; `router` rame 3; `sorter` piombo 2, rame 2; `itemBridge` piombo 6, rame 6 (per estremo); `cost` degli oggetti rame 0,5, piombo 0,7, grafite 1, titanio 1, silicio 0,8; costo di ricerca `round(60 + quantità^1.11 × 20, 10)` per voce (`researchCostMultiplier` 1). Non verificati: l'arrotondamento `UI.roundAmount` successivo (con queste quantità non cambia nulla) e i moltiplicatori di ricerca della campagna di Mindustry.

Corrispondenze: il rame di Mindustry è il nostro materiale base, `iron-plate` (il nostro `copper-wire` è un pezzo lavorato all'assemblatore e i livelli 1–3 partono senza produrlo); piombo → `lead-plate`; titanio → `titanium-plate`; i materiali alti (plastanio, torio, vetro metallico) → `graphite` e `silicon`. Sblocco: soldi come oggi; materiali con la formula di ricerca di Mindustry applicata al nostro costo nuovo e divisa per 10 (1 → 8, 2 → 10, 3 → 13, 6 → 21).

| Blocco | Costo di oggi | Costo nuovo | Sblocco di oggi | Sblocco nuovo | Perché |
|---|---|---|---|---|---|
| `conveyor-basic` | ferro 1 | ferro 1 | sbloccato | sbloccato | `conveyor`: rame 1 |
| `conveyor-fast` (T2) | ferro 2, filo 1 | ferro 1, piombo 1, titanio 1 | $450, ferro 50 | $450, ferro 8, piombo 8, titanio 8 | `titaniumConveyor` |
| `conveyor-express` (T3) | ferro 3, filo 2 | titanio 1, silicio 1, grafite 1 | $850, ferro 80, filo 20 | $850, titanio 8, silicio 8, grafite 8 | `plastaniumConveyor`, il tier sopra il titanio |
| `conveyor-armored` (nuovo, Task 8) | — | ferro 1, piombo 1, titanio 2, grafite 1 | — | $600, ferro 8, piombo 8, titanio 10, grafite 8 | specifica: il veloce più titanio; `armoredConveyor` usa materiali alti → grafite |
| `junction` | ferro 2 | ferro 3 | $200, ferro 10 | $200, ferro 13 | `junction`: rame 3 |
| `splitter` (sdoppiatore) | ferro 3 | ferro 3 | $280, ferro 15 | $280, ferro 13 | `router`: rame 3 |
| `sorter` | ferro 3, filo 1 | ferro 2, piombo 2 | $320, ferro 18 | $320, ferro 10, piombo 10 | `sorter`: piombo 2, rame 2 |
| `conveyor-bridge` (per estremo; il ponte ne paga 2) | ferro 3, filo 1 | ferro 6, piombo 6 | $340, ferro 20 | $340, ferro 21, piombo 21 | `itemBridge`: piombo 6, rame 6 |

Pesi di costruzione nuovi in `buildWeights` (il `cost` di Mindustry): `lead-plate` 0,7, `titanium-plate` 1,0, `graphite` 1,0, `silicon` 0,8. Le ricette delle macchine (forno, assemblatore, …) non cambiano.

Conseguenze da controllare: il T2 arriva solo col titanio (livello 9 in poi); il livello 8 deve fare lastre di piombo per sbloccare il selezionatore; il livello 10 deve produrre piombo e titanio per sbloccare il Nastro T2 (ha già l'obiettivo del Minatore T2). Il tutorial usa solo nastri base e minatori.

**Files:**
- Modify: `data/content.json` (`conveyors`, `structures`, `buildWeights`), `data/campaign.json` solo se la verifica del passo 3 lo chiede (materiali di partenza di un livello), `CHANGELOG.md`
- Test: `tests/TIndustry.Shared.Tests/LogisticsCostsTests.cs` (nuovo); i test che fissano i vecchi numeri (`AutoJunctionTests` "6 + 2", `BuilderTests`, `ConstructionTests`, `SaveMigrationTests`…) prendono i costi da `factory.CostOf(spec)` invece di numeri scritti

**Interfaces:**
- Produces: solo dati. Da qui i test dei task seguenti leggono i costi da `CostOf`, così l'ordine fra questo task e gli altri non conta.

- [ ] **Step 1: tabella e numeri.** Riporta nella PR la tabella sopra con l'OK dell'utente (o quella che l'utente ha corretto) e scrivi i numeri in `data/content.json`.
- [ ] **Step 2: test che falliscono.** `LogisticsCostsTests`: `BuildCostsMatchTheTable` (`[Theory]` una riga per blocco: `Content.FindConveyor(id).EffectiveBuildCost` uguale alla colonna "Costo nuovo"); `UnlockCostsMatchTheTable` (soldi e materiali di `structures`); `BuildWeightsMatchMindustry`; `CampaignUnlockObjectivesAreReachable`: per ogni obiettivo `unlockResearch` di `campaign.json`, ogni materiale dello sblocco è nei `startingMaterials` del livello in quantità sufficiente oppure si produce sulla sua mappa (`NewGame.Campaign(..., mapsDirectory:)`, giacimenti del terreno: `lead-plate` ← piombo, `titanium-plate` ← titanio, `graphite` ← carbone, `silicon` ← piombo e carbone, `copper-wire` ← rame e ferro, `iron-plate` ← ferro). Lancia prima di cambiare i dati: FAIL.
- [ ] **Step 3: campagna e tutorial.** `dotnet run --project tools/MapTool -- valida` → OK; se `CampaignUnlockObjectivesAreReachable` fallisce per un livello, si alzano i suoi `startingMaterials` in `campaign.json` (mai le mappe) e lo si scrive nella PR.
- [ ] **Step 4:** i test che fissavano i vecchi costi passano a `CostOf`; V (il playtest `tutorial` deve finire); commit `feat(dati): costi della logistica come Mindustry (2/9)`; push, workflow, PR con la tabella.

### Task 3: cambio di tier con cantiere di sostituzione e salvataggi v13

**Files:**
- Modify: `src/TIndustry.Shared/Construction/ConstructSite.cs` (`Replaced`), `Construction/Construction.cs` (`BeginReplace`, credito, fine, annullamento), `Simulation/Systems/BuilderSystem.cs` (`BeginWork`), `Simulation/SimHost.cs` (`ApplyPlaceBelt`, `QueuePlan`), `Factory.cs` (`BeltOverBeltAt` dà `Replace`; `TryReplaceBeltInstant`; `Capture`, `RestoreSite`), `Save/FactorySave.cs`
- Modify: `docs/decisioni/0003-costruzione-come-mindustry.md`, `docs/ARCHITETTURA.md` (Salvataggi), `CLAUDE.md` ("Salvataggi alla versione 13; i v11 e i v12 si leggono ancora"), `CHANGELOG.md`
- Test: `tests/TIndustry.Shared.Tests/BeltReplaceTests.cs` (nuovo), `ConstructionSaveTests.cs`, `SaveMigrationTests.cs`

**Interfaces:**
- Consumes: `BeltOverBelt`, `BeltOverBeltAt` (Task 1).
- Produces: `public sealed record ReplacedBelt(BuildSpec Spec, IReadOnlyList<TransportedItem> Items)`; `ConstructSite.Replaced { get; internal set; }` (null per i cantieri normali).
- Produces: `public static ConstructSite Construction.BeginReplace(GameState s, BuildSpec spec, int ownerPlayerId)`: toglie il nastro vecchio senza rimborso, ne tiene spec e oggetti in `Replaced`, e mette in `SpentLines[i]` il credito `min(nuovo_i, vecchio_i)` per id d'oggetto (i soldi sono una riga a sé). `internal static int[] Construction.ReplacementCredit(FactoryContent c, ConstructSite site)` lo ricalcola dai contenuti (non si salva). Il tempo di costruzione è quello intero del nastro nuovo.
- Fine del cantiere: `PlaceInstant(spec, charge: false)`, il nastro nuovo eredita i primi `Capacity` oggetti di `Replaced.Items` (`BeltGridCell.RestoreState`), e la parte del costo vecchio oltre il credito si rende con `RefundBasisPointsFor(Replaced.Spec)` (un solo arrotondamento per difetto per voce).
- Annullamento (`Remove` sul cantiere → `Construction.Cancel`): su un cantiere di sostituzione toglie prima il credito da `SpentLines` (non sotto 0); a progresso 0 rimette `Replaced.Spec` con `PlaceInstant(..., charge: false)` e i suoi oggetti. `Deconstructing` con `Replaced` non nullo vuol dire "credito già tolto": non serve un campo in più.
- Produces: `internal Refusal? Factory.TryReplaceBeltInstant(BuildSpec spec)` per `InstantBuild`: tutto o niente; paga nuovo − credito (se manca: `InsufficientResources` con `MissingItems` della sola differenza, nulla cambia), rende l'eccedenza come sopra, tiene gli oggetti.
- Con i piani: `QueuePlan` accetta un nastro su una cella `Replace` (sblocco e celle pianificate come oggi); `BuilderSystem.BeginWork` avvia `BeginReplace` se la cella è ancora `Replace`, altrimenti la via di oggi (cella vuota → cantiere normale; cella occupata → `PlanDropped` `Occupied`).
- Produces: `SiteSaveDto.Replaced` (`ReplacedSaveDto { BuildSpecSaveDto Spec; List<ItemSaveDto> Items }`, scritto solo se non nullo); `FactorySaveData.CurrentVersion = 13`, `MinReadableVersion = 11`.

- [ ] **Step 1: test che falliscono** (`BeltReplaceTests`; i costi attesi da `factory.CostOf`): `UpgradeWithInstantBuildPaysTheDifferenceAndKeepsItems` (base Est con un oggetto → `PlaceBelt` `conveyor-fast` Est: cella `conveyor-fast`, stesso oggetto, il portafoglio scende di costo(fast) − credito per voce); `UpgradeNeedsOnlyTheDifference` (in magazzino solo la differenza: riesce); `UpgradeShortOfTheDifferenceChangesNothing` (`InsufficientResources`, `MissingItems` = la differenza mancante, `Checksum()` uguale); `UpgradeWithPlansBuildsAReplacementSite` (`InstantBuild` falso: un piano; il cantiere ha `Replaced.Spec.StructureId == "conveyor-basic"`; alla fine nastro veloce con l'oggetto; speso = differenza); `DowngradeRefundsTheExcessAtTheRuleShare` (`GameRules(RefundPercent: 50)`, `conveyor-express` → `conveyor-basic`: alla fine torna `floor(eccedenza × 50%)` per voce); `CancellingAReplacementGivesBackTheOldBeltAndOnlyWhatWasPaid` (Review Focus 3: `Remove` a metà; alla fine nastro base con il suo oggetto, portafoglio identico a prima del `PlaceBelt`, numero di oggetti sulla griglia invariato); `ABeltRemovedBeforeTheDroneArrivesIsBuiltAsNew` (costo intero, nessun `Replaced`); `LockedJunctionOverAnotherTierReplacesTheCrossedBelt`; `BusyCellsAreNeverTurnedOrReplaced` (Task 1) esteso a un id diverso. In `ConstructionSaveTests`: `ReplacementSiteSurvivesSaveAndLoad` (Capture → Restore a metà: stesso checksum; finito dopo il caricamento, eredita l'oggetto; il file dice `Version` 13); `Version12SaveStillLoads` (istantanea scritta con `Version = 12` e senza `Replaced` → si carica, stesso stato).
- [ ] **Step 2:** `dotnet test ... --filter "FullyQualifiedName~BeltReplace|FullyQualifiedName~ConstructionSave"` → FAIL.
- [ ] **Step 3: implementazione** come nelle Interfaces; il fantasma del client è già valido (`BeltOverBeltAt`).
- [ ] **Step 4: documenti.** ADR 0003: sostituzione (cantiere che ricorda il nastro, credito = parte comune dei costi, eccedenza resa alla fine con la regola, annullamento che rimette il nastro vecchio), salvataggi v13 nelle Conseguenze; ARCHITETTURA e CLAUDE.md alla v13; CHANGELOG.
- [ ] **Step 5:** V; commit `feat(nastri): cambio di tier con cantiere di sostituzione, salvataggi v13 (3/9)`; push, workflow, PR.

### Task 4: la linea di Shared — retta, direzione per cella, incroci; `piazza` degli agenti

**Files:**
- Create: `src/TIndustry.Shared/Simulation/PlacementLine.cs`
- Modify: `Construction/BuildSpec.cs` (`KindFor`, spostato da `AgentGame.KindOf`), `Factory.cs` (`BeltCrossingAt` senza il controllo dei vicini della linea), `Agent/AgentGame.cs` (`Piazza`), `tools/AgentPlay/Tools.cs` (descrizione e parametri di `piazza`), `tools/AgentPlay/README.md`, `.claude/skills/gioca-e-testa/SKILL.md`, `docs/decisioni/0008-agenti-che-giocano.md`, `CHANGELOG.md`
- Test: `tests/TIndustry.Shared.Tests/PlacementLineTests.cs` (nuovo), `AgentGameTests.cs`, `AutoJunctionTests.cs`, `tests/TIndustry.AgentPlay.Tests/AgentPlayServerTests.cs`

**Interfaces:**
- Consumes: `BeltOverBeltAt` (Task 1), `BeltLine.IsInterior`, `BeltLine.MayCross`, `IGameView.CanPlace`, `IGameView.Footprint`, `IsPlannedTile`, `CoreStockSink.PreferDirectionIntoCore`.
- Produces:
  ```csharp
  public enum LineShape { Straight, Diagonal }
  public sealed record LineRequest(int PlayerId, string StructureId, GridPosition Anchor, GridPosition Cursor,
      Direction Brush, bool FixedDirection = false, LineShape Shape = LineShape.Straight,
      string? RecipeId = null, string? FilterItemId = null);
  public sealed record LinePlacement(GridPosition Cell, Direction Direction, SimCommand? Command,
      RefusalCode? Refusal, bool Spanned = false);
  public static class PlacementLine
  {
      public const int MaxCells = 200;            // = AgentGame.MaxListItems
      public static IReadOnlyList<LinePlacement> Plan(IGameView view, LineRequest request);
      public static IReadOnlyList<SimCommand> Commands(IReadOnlyList<LinePlacement> line); // accepted, in line order
      public static IReadOnlyList<GridPosition> Straight(GridPosition anchor, GridPosition cursor);
  }
  public static BuildingKind? BuildSpec.KindFor(string structureId);
  ```
- Regole: `Straight` sull'asse dominante (|dx| ≥ |dy| → asse x, il pari va a x come in `DragLine`), l'altra coordinata dell'ancora, al massimo `MaxCells` celle dall'ancora. Linee solo per nastri semplici, `junction`, `splitter`, `sorter`; ogni altro blocco è una cella sola all'ancora. Direzione: `FixedDirection` → `Brush` ovunque; cella sola → `Brush`; altrimenti verso la cella seguente, e l'ultima prende la direzione del nastro semplice su cui finisce, se c'è, altrimenti quella dalla penultima. Regola del Core solo sull'ultima cella (cella sola compresa), senza `FixedDirection` e non sopra un nastro: `PreferDirectionIntoCore`. Incrocio (solo nastri): cella interna (`IsInterior`) sopra un nastro perpendicolare (`MayCross`) → `PlaceBelt` con `Crossing: true`. Rifiuto previsto: nastro → nullo se incrocio o `BeltOverBeltAt != None`, altrimenti il motivo di `CanPlace` o `Occupied` se una cella dell'ingombro è pianificata; gli altri blocchi lo stesso senza il caso nastro. `Commands` salta le celle con rifiuto o `Spanned`: le celle rifiutate si vedono in rosso e non si inviano.
- `Factory.BeltCrossingAt` tiene le condizioni sul nastro attraversato ma non chiede più i nastri della linea ai due lati (lo decide la linea): i comandi partono nell'ordine dall'ancora, e due incroci di fila diventano entrambi incroci, come in Mindustry.
- `piazza {da, a}` degli agenti: senza `direzione` la linea segue il trascinamento; `direzione` vale come R durante il trascinamento (`FixedDirection`); `piazza {cella}` usa `direzione` o il pennello. I rifiuti previsti dalla linea finiscono in `rifiuti` con il loro codice, insieme a quelli della simulazione.

- [ ] **Step 1: test che falliscono** (`PlacementLineTests`, Sandbox `InstantBuild`): `StraightTakesTheDominantAxis` (`Straight((5,5),(9,7))` = x 5..9 a y 5; `Straight((5,5),(6,9))` = y 5..9 a x 5; `Straight((5,5),(8,8))` sull'asse x); `EveryBeltFacesTheNextCell` (pennello Est, (10,10) → (10,6): 5 nastri tutti Nord); `LastCellTakesTheBeltItEndsOn` (nastro Ovest in (10,6): ultima cella Ovest, `Same`); `SingleClickUsesTheBrush`; `FixedDirectionWinsEverywhere` (pennello Est, linea verso Nord: tutti Est); `CoreRuleOnlyOnTheLastCell` (linea lungo il lato ovest del Core verso Nord: tutti Nord tranne l'ultima, che guarda il Core); `InteriorCrossingIsMarked` e `ConsecutiveCrossingsAllBecomeJunctions` (colonne Sud in x 40 e 41, linea Est, incrocio sbloccato: tramite `SimHost` entrambe `LogisticsKind.Junction`); `RefusedCellsAreShownAndNotSent` (parete: `Refusal == Wall`, assente da `Commands`); `LineIsCappedAtMaxCells` (Review Focus 2: `Straight((0,0),(500,0)).Count == 200`); `SpecialsFaceTheNextCellToo` (linea di selezionatori verso Sud: tutti Sud). In `AgentGameTests`: `PiazzaLineDragsLikeThePlayer` aggiornato (da (10,10) a (10,6) senza `direzione` → `celle` tutte `nord`); `PiazzaDirezioneIsFixed`; `PiazzaReportsLineRefusals` (parete → `rifiuti[0].codice == "Wall"`); `ClientAndAgentSendTheSameCommands` (gli stessi `PlacementLine.Commands(Plan(...))` inviati a mano su un secondo host → stesso `Checksum()` di `piazza`).
- [ ] **Step 2:** `dotnet test ... --filter "FullyQualifiedName~PlacementLine|FullyQualifiedName~AgentGame|FullyQualifiedName~AutoJunction"` → FAIL.
- [ ] **Step 3: implementazione** di `PlacementLine` (solo `LineShape.Straight` qui; `Diagonal` per ora come `Straight`), `BuildSpec.KindFor`, `BeltCrossingAt`, `AgentGame.Piazza`. Il client resta su `BeltDrag` fino al Task 6: `main` resta coerente perché `BeltDrag` manda incroci solo per celle interne.
- [ ] **Step 4: documenti degli agenti.** README di AgentPlay e skill `gioca-e-testa`: `piazza {da, a}` segue la linea, `direzione` la fissa, rifiuti della linea; ADR 0008: "la linea è una funzione di Shared (`PlacementLine`)" al posto di `BeltDrag`; descrizione di `piazza` in `Tools.cs`; CHANGELOG.
- [ ] **Step 5:** V; commit `feat(nastri): linea di Shared con direzione per cella, piazza degli agenti (4/9)`; push, workflow, PR.

### Task 5: ponte automatico e Ctrl (A* e catena) nella linea

**Files:**
- Modify: `src/TIndustry.Shared/Simulation/PlacementLine.cs`, `Agent/AgentGame.cs` (`diagonale`), `tools/AgentPlay/Tools.cs`, `tools/AgentPlay/README.md`, `.claude/skills/gioca-e-testa/SKILL.md`, `CHANGELOG.md`
- Test: `tests/TIndustry.Shared.Tests/AutoBridgeTests.cs`, `DiagonalLineTests.cs` (nuovi), `AgentGameTests.cs`

**Interfaces:**
- Consumes: `PlacementLine`, `LineRequest`, `LinePlacement` (Task 4); `IGameView.Footprint` di un ponte (l'uscita di `FindBridgeExit`).
- Produces: `public const int PlacementLine.MaxSearchNodes = 1000;` e le regole:
  - **Ponte** solo con `LineShape.Straight`, nastri, ogni cella con la direzione della linea (niente R di lato), `conveyor-bridge` sbloccato. Una serie di celle bloccate (rifiuto `Wall` o `DeepWater`, oppure `IsBuildingTile`: edificio o cantiere) preceduta da una cella libera della linea su cui il ponte si può piazzare: l'uscita è la seconda cella di `view.Footprint(new BuildSpec("conveyor-bridge", BuildingKind.Bridge, entrata, dir))`; vale solo se è la prima cella dopo la serie e sta dentro la linea. Allora l'entrata diventa `PlaceBuilding(Bridge, entrata, dir)`, le celle bloccate e l'uscita `Spanned` senza comando, e la linea riprende dopo l'uscita. Altrimenti le celle restano rifiutate. Campata quella di `FindBridgeExit` (2–5 passi, cioè 1–4 celle scavalcate). Mai sopra Core, pezzi di logistica o fuori mappa; sopra un nastro perpendicolare resta l'incrocio.
  - **Ctrl** (`LineShape.Diagonal`): se ancora e cursore sono nastri semplici della stessa catena (scendendo da uno con `cell.Direction` si arriva all'altro entro `MaxCells`, in un verso o nell'altro) la linea è la catena, ogni cella con la sua direzione e lo `StructureId` in mano (tier uguale → `Same`, diverso → `Replace`). Altrimenti A* a 4 direzioni: passo 1, +8 a ogni cambio di direzione, +20 entrando in una cella dove il nastro sarebbe rifiutato (le celle nastro-sopra-nastro non pagano), al massimo `MaxSearchNodes` nodi espansi, pari rotti da (f, g, ordine d'inserimento); niente percorso o limite superato → `Straight`. Il percorso ha le direzioni per cella e gli incroci sui tratti dritti, mai ponti.
- `piazza {da, a, diagonale: true}` = Ctrl.

- [ ] **Step 1: test che falliscono.** `AutoBridgeTests`: `BridgeOverAWall` (linea Est y 10 da x 5 a 12, parete in (8,10): (7,10) ponte Est, (8,10) e (9,10) `Spanned`, (10,10)–(12,10) nastri; tramite `SimHost` `BridgePartner` di (7,10) è (9,10) e un oggetto messo in (5,10) arriva in (12,10)); `BridgeOverABuilding` (minatore 2×2 sulla linea); `BridgeOverDeepWater` (acqua profonda sul terreno di prova); `ObstacleLongerThanTheSpanStaysRefused` (5 pareti di fila: tutte `Wall`, nessun ponte); `NoBridgeWhenLockedOrSideways` (ponte bloccato; `FixedDirection` Nord su una linea Est); `PerpendicularBeltStillGetsTheJunction`; ogni test calcola il costo del ponte da `CostOf`. `DiagonalLineTests`: `CtrlPathIsFourConnected` (terreno libero (5,5) → (9,8): 8 celle, ogni passo su un lato, una sola svolta); `CtrlGoesAroundAWall`; `CtrlFallsBackToTheStraightLine` (cursore chiuso da pareti: risultato uguale a `Straight(ancora, cursore)`; Review Focus 2); `CtrlFollowsAChainToUpgradeIt` (catena base (5,5)E (6,5)E (7,5)N (7,4)N (7,3)N, Ctrl da (5,5) a (7,3) con `conveyor-fast`: 5 `PlaceBelt` fast con le direzioni della catena; con `SimHost` `InstantBuild` tutti fast, direzioni e oggetti tenuti); `CtrlChainWorksFromTheDownstreamEnd`. In `AgentGameTests`: `PiazzaDiagonaleUsesTheSamePath`.
- [ ] **Step 2:** `dotnet test ... --filter "FullyQualifiedName~AutoBridge|FullyQualifiedName~DiagonalLine"` → FAIL.
- [ ] **Step 3: implementazione** come nelle Interfaces.
- [ ] **Step 4: documenti degli agenti.** `diagonale` e ponte automatico nel README di AgentPlay, nella skill e in `Tools.cs`; CHANGELOG.
- [ ] **Step 5:** V; commit `feat(nastri): ponte automatico e Ctrl nella linea (5/9)`; push, workflow, PR.

### Task 6: trascinamento del client con anteprima e invio al rilascio

**Files:**
- Modify: `godot/scripts/world/World.Input.cs`, `World.Placement.cs`, `World.Drawing.cs`, `World.Commands.cs`, `World.cs`, `godot/scripts/ui/hud/Hud.cs` (`ToastsShown`, contatore in sola lettura per i playtest)
- Modify: `src/TIndustry.Shared/Simulation/SimHost.cs` e `Factory.cs` (via la regola del Core da `ApplyPlaceBelt` e da `PlaceBelt`: ora è della linea)
- Delete: `src/TIndustry.Shared/Simulation/BeltDrag.cs`, `tests/TIndustry.Shared.Tests/BeltDragTests.cs` (il confronto agente/client vive in `PlacementLineTests.ClientAndAgentSendTheSameCommands`)
- Create: `godot/scripts/dev/World.PlaytestNastri.cs` (+ `.uid`), chiamato da `PlaytestUiAsync`
- Modify: `godot/README.md` (Controlli build, Costruzione), `docs/decisioni/0003-costruzione-come-mindustry.md` (regola del Core sull'ultima cella; piani al rilascio), `docs/ARCHITETTURA.md` (Interfaccia: la linea viene da Shared), `CHANGELOG.md`
- Test: `tests/TIndustry.Shared.Tests/PlacementLineTests.cs`, `SimHostTests.cs`

**Interfaces:**
- Consumes: `PlacementLine.Plan`, `PlacementLine.Commands`, `LineRequest`, `LineShape` (Task 4–5).
- Produces (per il Task 7 e 8): `private static bool IsBelt(BuildTool tool)` (unico elenco dei nastri nel client), `private void CancelLine()`, `internal bool LineDragActive`.
- Regole: sinistro premuto con uno strumento che non è il cursore → ancora e anteprima, nessun comando; a ogni cambio di cella o di Ctrl (`InputEventWithModifiers.CtrlPressed` dell'evento) la linea si ricalcola; al rilascio si ricalcola sullo stato di quel momento e si inviano `Commands` in ordine, con `quietWhenBlocked: true`. R (o la rotellina, Task 7) durante il trascinamento gira il pennello e fissa la direzione (`FixedDirection`) fino al rilascio. Esc o destro durante il trascinamento annullano solo la linea (lo strumento resta); anche un pannello che si apre e la perdita del fuoco (`NotificationApplicationFocusOut`) la annullano. Fantasmi: `PlacementPreview.Draw` per ogni `LinePlacement` (rosso con rifiuto), la campata del ponte per le `Spanned`.
- Toast: i codici di posizione restano zitti come oggi; ogni altro codice (`InsufficientResources`, `Locked`, …) al massimo una volta per rilascio (`HashSet<RefusalCode>` azzerato a ogni invio, al posto di `_crossingToastShown`).

- [ ] **Step 1: test Shared che falliscono.** `SimHostTests.ABeltBesideTheCoreKeepsItsDirection` (`[Theory]` su `InstantBuild`: `PlaceBelt` Nord accanto al Core resta Nord); `PlacementLineTests.ClientAndAgentSendTheSameCommands` già c'è. Lancia: FAIL il primo.
- [ ] **Step 2: implementazione Shared** (regola del Core tolta da `SimHost` e da `Factory.PlaceBelt`; i test che contavano sul giro automatico passano la direzione verso il Core); via `BeltDrag` e i suoi test.
- [ ] **Step 3: playtest che fallisce.** `World.PlaytestNastri.cs`, sonde con cattura (aiuti nuovi `PlaytestPress`, `PlaytestMove`, `PlaytestRelease` accanto a `PlaytestDrag`, con `ctrl` facoltativo): `nastri-verso-nord` (pennello Est, trascinamento verso Nord di 5 celle → 5 nastri Nord costruiti); `nastri-anteprima` (premuto e mosso: `PendingCommands` vuoto e `PlansOf(LocalPlayer)` invariato finché il tasto è giù; Esc → nessun comando e lo strumento resta Nastro); `nastri-r` (R a metà trascinamento: tutti i nastri con la direzione nuova del pennello); `nastri-interrotto` (Review Focus 1: T a metà trascinamento → `LineDragActive` falso, nessun comando; chiusa la Ricerca, nulla è partito); `nastri-un-toast` (incrocio sbloccato, lastre a 0, linea attraverso tre nastri perpendicolari → `ToastsShown` sale di 1). Il passo 4 del tutorial e `PlaytestAutoJunctionAsync` restano verdi.
- [ ] **Step 4: implementazione client** come nelle Interfaces; build-solutions e `tools/dev/playtest.sh ui "$(mktemp -d)"` → PLAYTEST OK.
- [ ] **Step 5: documenti.** `godot/README.md`: trascinamento come Mindustry (retta sull'asse dominante, direzione per cella, R a metà, Ctrl, anteprima, invio al rilascio, Esc/destro annullano, ponte automatico, nastro sopra nastro); ADR 0003; ARCHITETTURA; CHANGELOG.
- [ ] **Step 6:** V; commit `feat(nastri): trascinamento con anteprima e invio al rilascio (6/9)`; push, workflow, PR con le catture.

### Task 7: rettangolo di smontaggio, tasto centrale e rotellina; `smonta {da, a}`

**Files:**
- Create: `src/TIndustry.Shared/Simulation/RemovalArea.cs`
- Modify: `godot/scripts/world/World.Input.cs`, `World.Placement.cs` (via `RemoveOnceInDrag`, `DragCells`, `_removedThisDrag`; nuovo `PickAt`), `World.Drawing.cs` (contorno rosso del rettangolo), `WorldCamera.cs`, `godot/scripts/ui/UiColors.cs` (colore del rettangolo nelle tre varianti)
- Delete: `src/TIndustry.Shared/Grid/DragLine.cs` (nessun uso rimasto) e i suoi test
- Modify: `Agent/AgentGame.cs` (`smonta {da, a}`), `tools/AgentPlay/Tools.cs`, `tools/AgentPlay/README.md`, `.claude/skills/gioca-e-testa/SKILL.md`
- Modify: `godot/scripts/dev/World.Playtest.cs` (`PlaytestRemoveDragOutAndBackAsync` diventa `PlaytestRemoveRectangleAsync`), `World.PlaytestNastri.cs`
- Modify: `godot/README.md` (destro, centrale, rotellina), `docs/decisioni/0003-costruzione-come-mindustry.md` ("Smontaggio trascinato" → rettangolo), `CHANGELOG.md`
- Test: `tests/TIndustry.Shared.Tests/RemovalAreaTests.cs` (nuovo), `AgentGameTests.cs`

**Interfaces:**
- Consumes: `IsBelt`, `CancelLine` (Task 6).
- Produces:
  ```csharp
  public static class RemovalArea
  {
      public const int MaxEdge = 64;   // = AgentGame.MaxAreaEdge
      public static (GridPosition Min, GridPosition Max) Rect(GridPosition anchor, GridPosition cursor);
      public static IReadOnlyList<Remove> Commands(IGameView view, int playerId, GridPosition anchor, GridPosition cursor);
  }
  ```
  `Rect` è limitato a `MaxEdge` per lato dall'ancora; `Commands` va per righe e colonne (Y poi X) e dà un `Remove(playerId, cella, OnlyMark: true)` per ogni blocco, cantiere o piano del giocatore che tocca il rettangolo (la prima sua cella incontrata), saltando le celle vuote.
- `WorldCamera`: `public Func<bool> WheelZooms { get; set; } = () => true;` (con `false` e senza Ctrl la rotellina non zooma e passa al mondo); `public event Action<Vector2>? MiddleClicked;` al rilascio del centrale con meno di 4 px di movimento dalla pressione; trascinato resta lo spostamento della vista.
- Client: destro premuto → ancora; mosso → rettangolo con contorno rosso; al rilascio `RemovalArea.Commands`; clic destro senza movimento alterna il segno come oggi. Centrale senza movimento → `PickAt(cell)`: blocco in piedi (o cantiere) sotto il cursore → strumento corrispondente, pennello alla sua direzione, filtro del selezionatore; struttura bloccata → toast `rifiuto.Locked` e strumento invariato; cella vuota → niente. Rotellina con uno strumento orientabile (nastri e `PlacementPreview.HasDirection`) e senza Ctrl → ruota come R (durante un trascinamento fissa la direzione); con Ctrl o col cursore → zoom.

- [ ] **Step 1: test che falliscono.** `RemovalAreaTests`: `OneRemovePerBlockInsideTheRectangle` (minatore 2×2, 3 nastri e un piano nel rettangolo → 5 comandi); `PlansInsideAreCancelledAndBlocksMarked` (`InstantBuild` falso, via `SimHost`); `EmptyCellsSendNothing`; `RectangleIsClampedTo64` (`Rect((0,0),(100,100))` = (0,0)–(63,63)); `ARectangleNeverUnmarks` (rettangolo su blocchi già segnati: restano segnati). `AgentGameTests.SmontaAreaMarksLikeTheRectangle` (stesso checksum di `RemovalArea.Commands` inviati a mano).
- [ ] **Step 2:** `dotnet test ... --filter "FullyQualifiedName~RemovalArea|FullyQualifiedName~AgentGame"` → FAIL.
- [ ] **Step 3: implementazione** Shared e agenti, poi client e `WorldCamera`.
- [ ] **Step 4: playtest.** `PlaytestRemoveRectangleAsync`: rettangolo sopra la linea del seme → tutti segnati, cattura `smontaggio-rettangolo` col tasto ancora giù (contorno visibile); un secondo rettangolo non toglie segni; un clic destro singolo toglie un segno. In `World.PlaytestNastri.cs`: `nastri-prendi` (clic centrale su un nastro veloce Nord → strumento Nastro T2, pennello Nord; centrale trascinato → vista spostata, strumento uguale); `nastri-rotellina` (strumento Nastro: rotellina su → pennello da Est a Sud, zoom invariato; Ctrl+rotellina → zoom cambiato; col cursore → zoom).
- [ ] **Step 5: documenti** (README del gioco, ADR 0003, README di AgentPlay e skill per `smonta {da, a}`, CHANGELOG).
- [ ] **Step 6:** V; commit `feat(nastri): rettangolo di smontaggio, tasto centrale e rotellina (7/9)`; push, workflow, PR con le catture.

### Task 8: nastro rinforzato `conveyor-armored`

**Files:**
- Modify: `data/content.json` (nastro e struttura con i numeri del Task 2), `src/TIndustry.Shared/Content/ContentTypes.cs` (`ConveyorDefinition.Armored`), `Content/FactoryContent.cs` (campo `armored` del DTO), `Grid/BeltGrid.cs`, `Buildings/Miner.cs`, `Buildings/Crafter.cs`, `Buildings/Extractor.cs`, `Factory.cs` (`StructureIdForTool`: `"belt-armored"`)
- Modify: `tools/art/blocchi/logistica.py` (`TIER["conveyor-armored"]` con bordo e rinforzi), `tools/art/test_genera.py` (`NASTRI` e conteggi), `tools/art/README.md`, `tools/sito/composti.py`; sprite rigenerati `godot/assets/conveyor-armored*.png` (+ `.import` da `--import`, poi ripristino dei `.import` cambiati solo per fine riga)
- Modify: `godot/scripts/world/World.cs` (`BuildTool.BeltArmored`), `World.Input.cs` (`ToHudTool`, `FromHudTool`, `IsBelt`, `PickAt`), `godot/scripts/ui/hud/Hud.cs` (`ToolKind.BeltArmored`, `StructureIdFor`), `BuildPalette.cs`, `World.Session.cs` (`SyncResearchLocks`), `godot/lingue/testi.csv`
- Modify: `godot/scripts/dev/World.PlaytestNastri.cs`, `World.Playtest.cs` (elenchi dei tier), `godot/README.md`, `CHANGELOG.md`
- Test: `tests/TIndustry.Shared.Tests/ArmoredBeltTests.cs` (nuovo), `ResearchTreeContentTests.cs` (`{ "conveyor-armored", "conveyor-fast" }`), `LogisticsCostsTests.cs` (riga del rinforzato)

**Interfaces:**
- Consumes: `IsBelt`, `PickAt` (Task 6–7), `BeltOverBelt.Replace` (Task 3: fast ↔ rinforzato è un cambio di tier).
- Produces: `ConveyorDefinition.Armored` (`bool`, default `false`; in `content.json` `"armored": true`). Voce: `{"id":"conveyor-armored","tier":2,"rateItemsPerSecond":1.0,"capacity":1,"itemSpacing":1.0,"moneyCost":0,"kind":"belt","armored":true}` con `buildCost` e `unlock` della tabella del Task 2; struttura `displayName` "Nastro rinforzato", `kind` `conveyor`, `prerequisites` `["conveyor-fast"]`. Con `tier` 2 `piazza {tier: 2}` resta il veloce (primo nell'elenco); il rinforzato si chiede con `tipo`.
- Produces: `public bool BeltGrid.TryInsert(GridPosition position, TransportedItem item, Direction? fromDirection = null, GridPosition? source = null)`. Una cella rinforzata accetta solo se la sorgente (`source`, o `position.Step(Opposite(fromDirection))` per i passaggi fra celle) è la cella dietro di lei oppure un nastro semplice (`LogisticsKind.Belt`, rinforzato compreso); un inserimento senza direzione e senza sorgente è rifiutato. Il passaggio dell'incrocio (`next.TryInsert` in `TryHandoff`) passa dallo stesso controllo. Minatore, forno, assemblatore ed estrattore passano come `source` la cella del loro ingombro accanto alla cella d'uscita.
- `BeltGrid.Feeders` e `GetShape` di una cella rinforzata: un fianco conta solo se è un nastro semplice; `BuildingFeeders` e le fusioni con edifici, Core e generatori sono sempre `(false, false)`.
- Testi: `struttura.conveyor-armored.nome` "Nastro rinforzato" / "Armored belt"; `.suggerimento` "Nastro rinforzato · prende dagli edifici solo da dietro · sblocca in Ricerca" / "Armored belt · takes from buildings only from behind · unlock in Research"; `.descrizione` "Nastro rinforzato · come il T2, non raccoglie di lato · R/rotella" / "Armored belt · like T2, never picks up from the side · R/wheel".

- [ ] **Step 1: test che falliscono** (`ArmoredBeltTests`, `InstantBuild`): `ABuildingOnTheSideIsRefused` (minatore sul fianco di una linea rinforzata: dopo 600 tick nessun oggetto sulla linea); `ABuildingBehindIsAccepted`; `ABeltOnTheSideIsAccepted` (e la forma è `JoinLeft`/`JoinRight` come gli altri nastri); `ShapeStaysStraightBesideADrill` (`GetBeltShape == Straight` accanto a una trivella, a un generatore e al Core); `ArmoredRefusesNonBeltLogisticsFromTheSide` (Review Focus 5, `[Theory]` su sdoppiatore, selezionatore, uscita di ponte, incrocio: di lato rifiutati, da dietro accettati); `ArmoredSavesAndLoads` (Capture → Restore: stesso checksum, id `conveyor-armored`); `FastToArmoredIsATierChange` (`BeltOverBeltAt` dà `Replace`). In `test_genera.py`: `conveyor-armored` in `NASTRI` (strisce cicliche, curve, T, unioni).
- [ ] **Step 2:** `dotnet test ... --filter "FullyQualifiedName~Armored|FullyQualifiedName~ResearchTree|FullyQualifiedName~LogisticsCosts"` e `python -m unittest tools/art/test_genera.py` → FAIL.
- [ ] **Step 3: implementazione** Shared e dati; arte (`python tools/art/genera.py`, poi `--controlla`); client (strumento, palette, blocchi in Ricerca, testi); `godot4 --import` con `APPDATA` temporaneo e ripristino dei `.import`.
- [ ] **Step 4: playtest.** Sonda `nastro-rinforzato` in `World.PlaytestNastri.cs`: una fila di 3 minatori su un giacimento, una linea rinforzata lungo il loro fianco e una linea base parallela dall'altro lato; dopo 10 s la base ha oggetti e la rinforzata no; cattura `nastro-rinforzato`.
- [ ] **Step 5: documenti** (`godot/README.md` blocchi, `tools/art/README.md`, CHANGELOG); `python tools/sito/costruisci.py` per vedere il nastro nella sezione asset.
- [ ] **Step 6:** V; commit `feat(nastri): nastro rinforzato (8/9)`; push, workflow, PR con la cattura.

### Task 9: chiusura — mappa del progetto, diario, stato della specifica

**Files:**
- Modify: `tools/dev/mappa_canvas.py` (carta "Nastri alla Mindustry" da Prossimi a Fatto con le PR, titolo con la data), `docs/mappa-progetto.canvas` (rigenerata), `docs/superpowers/specs/2026-10-18-nastri-mindustry-design.md` (Stato: attuata, con il link a questo piano), `CHANGELOG.md` (una voce d'insieme in Aggiunto sopra quelle per PR)
- Create: `docs/diario/2026-10-19-nastri-mindustry.md` (dal registro `.superpowers/sdd/<piano>/progress.md`, formato di `docs/diario/README.md`), riga nell'indice di `docs/diario/README.md`

- [ ] **Step 1:** aggiorna la mappa e lancia `python tools/dev/mappa_canvas.py` → stampa il numero di nodi e collegamenti; il canvas si apre in Obsidian.
- [ ] **Step 2:** diario (task, PR, giri di revisione, decisioni nella forma "Decisione: cosa — perché — costo se sbagliata", rinvii, verifiche finali), poi si cancella `.superpowers/sdd/<piano>/`.
- [ ] **Step 3:** V; commit `docs: chiusura dei nastri come Mindustry (9/9)`; push, workflow, PR.
