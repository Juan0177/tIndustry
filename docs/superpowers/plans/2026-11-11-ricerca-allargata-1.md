# Ricerca allargata, piano 1: oggetti, ricette e macchine — piano di implementazione

> **Per gli agenti:** SOTTO-SKILL RICHIESTA: superpowers:subagent-driven-development (consigliata) o superpowers:executing-plans, task per task. I passi usano le caselle (`- [ ]`).

**Obiettivo:** sabbia, vetro, plastica e circuiti; la fonderia avanzata, la pressa grande, il separatore, il trapano a impatto e il plastificatore, costruibili, sbloccabili in Ricerca, salvati, giocabili dagli agenti e disegnati nel client.

**Architettura:** le macchine 2×2 nuove sono `Crafter` con un **profilo** letto da `content.json` (prefisso delle ricette, modo della corrente, velocità), in una lista nuova `Machines` accanto a forni e assemblatori; il trapano a impatto è un `Miner` con la misura per istanza (3×3). La sabbia viene dal pavimento nel piazzamento del minatore. Il caso del separatore è un hash del seme della partita, dell'id della macchina e del numero di pezzi fatti: niente stato nuovo da salvare. La ricetta dei circuiti è un nodo della Ricerca senza blocco (`kind: "recipe"`).

**Tecnologie:** C# .NET 8 (`src/TIndustry.Shared`, xUnit), `data/content.json`, Python 3 (`tools/dev/genera_finanza.py`, `tools/art/genera.py`), client Godot 4.4 .NET.

**Specifica:** [`docs/superpowers/specs/2026-11-09-ricerca-allargata-design.md`](../specs/2026-11-09-ricerca-allargata-design.md) (sezioni «Oggetti e ricette», «Macchine», più la parte di Ricerca, salvataggi, agenti e client che le riguarda).

**Punti da toccare per un blocco nuovo:** `.superpowers/sdd/2026-11-11-ricerca-allargata-1/touchpoints.md` (elenco ricavato da batteria, Minatore T2 e assemblatore). Leggerlo prima di ogni task.

## Vincoli globali

- Oggetti nuovi in `market` (valore B): `sand` «Sabbia» 2, `glass` «Vetro» 9, `plastic` «Plastica» 20, `circuit` «Circuito» 90.
- Ricette: `smelt-glass` 2 sabbia → 1 vetro in 2 s (forno e fonderia avanzata); `press-graphite` 3 carbone → 2 grafite in 1,5 s; `separate-sand` 2 sabbia → 1 pezzo a caso in 2 s (ferro, rame, piombo grezzo 30% ciascuno, titanio grezzo 10%); `plasticize-plastic` 2 carbone → 1 plastica in 2 s; `craft-circuit` 1 filo di rame + 1 silicio + 1 lastra di piombo → 1 circuito in 3 s (assemblatore, solo con il nodo `circuit` sbloccato).
- Macchine (misura, corrente): `smelter-advanced` 2×2, 20/s, ricette del forno a velocità 2, ferma senza corrente, non brucia carbone; `graphite-press` 2×2, senza corrente; `separator` 2×2, 8/s; `plasticizer` 2×2, 6/s; `impact-drill` 3×3, 30/s, tier 3 (scava anche il titanio), velocità 4 volte il Minatore T2.
- Le macchine con corrente obbligatoria lavorano alla velocità (velocità del profilo × soddisfazione della rete); a 0 sono ferme. Il trapano idem: (8 × efficienza × soddisfazione).
- **Interpretazione** di «scava ogni giacimento sotto di sé»: il trapano scava qualunque giacimento (tier 3) e, come gli altri minatori, il prevalente sotto le sue 9 celle; efficienza = celle coperte / 9, senza il ×1,25 del T2.
- Sabbia dal pavimento: un minatore (T1, T2 o trapano) senza un giacimento scavabile sotto, con celle di pavimento `Sand` o `DarkSand`, scava `sand` con efficienza = celle di sabbia / celle dell'impronta. Un giacimento scavabile vince sempre sulla sabbia. Generatori di mappa (`classico`, `biomi-1`) **intatti**; senza terreno (test) resta il ferro di oggi. Il drone non scava sabbia a mano.
- Caso del separatore: `SplitMix64(seed ^ (id × 0x9E3779B97F4A7C15) ^ itemsCrafted)` modulo la somma dei pesi; `seed` = `Terrain.Seed` (0 senza terreno). Nessun `Random`, nessun float nel sorteggio.
- Costi di costruzione (a mano, materiali della fase): `graphite-press` 20 lastre di ferro + 10 lastre di piombo; `smelter-advanced` 30 lastre di ferro + 15 lastre di titanio + 10 fili di rame; `separator` 20 lastre di ferro + 10 lastre di titanio + 10 fili di rame; `plasticizer` 20 lastre di titanio + 20 lastre di piombo + 10 silicio; `impact-drill` 40 lastre di titanio + 20 silicio + 20 grafite. `refundPercent` 100.
- Ricerca (primo prerequisito = genitore nell'albero, fase del foglio): `graphite-press` ← `assembler` (fase 1, Metà); `smelter-advanced` ← `smelter` (fase 2); `separator` ← `smelter-advanced` (fase 2); `plasticizer` ← `graphite-press` (fase 2); `circuit` (kind `recipe`, «Ricetta dei circuiti») ← `assembler` (fase 2); `impact-drill` ← `miner-advanced` (fase 3). Costi **solo** dallo script (`--scrivi-costi`, `--controlla`); il tempo per sbloccare tutto senza jackpot resta fra 5,5 e 6,5 ore (si scelgono i minuti dei nodi nuovi per starci; il riequilibrio vero è del piano 3).
- Salvataggi **v21** (`CurrentVersion = MinReadableVersion = 21`); la v20 non si apre.
- Regole di sempre (CLAUDE.md): determinismo (interi dove si decide, nessun ordine di `Dictionary`/`HashSet`), testi solo da `testi.csv` it + en, stili dal tema, sprite solo da `genera.py`, enum `BuildingKind` solo in coda, commenti in inglese, commit in italiano con `Co-Authored-By:`.

## Punti da sorvegliare in revisione

1. **Forno e assemblatore invariati**: il profilo generico non deve cambiare velocità, carbone e corrente di forno e assemblatore (test esistenti verdi, checksum dei test di determinismo invariati salvo i salvataggi) — Task 1.
2. **Minatore su sabbia e giacimento insieme**: un'impronta metà ferro e metà sabbia scava ferro (2 celle, efficienza 2/4), mai sabbia; un T1 su titanio e sabbia scava sabbia (il titanio non è scavabile dal T1) senza il rifiuto `NeedsAdvancedMiner` — Task 2.
3. **Ricetta dei circuiti prima dello sblocco**: un assemblatore piazzato prima del nodo `circuit` non prende silicio per i circuiti e non li fa; dopo lo sblocco li fa senza essere ripiazzato; dopo un salvataggio e un caricamento resta coerente — Task 3.
4. **Trapano 3×3 ai bordi**: impronta che esce dalla mappa rifiutata; uscite sui 12 lati; consegna diretta al CORE se lo tocca — Task 4.
5. **Separatore e determinismo**: due partite con lo stesso seme danno la stessa sequenza; semi diversi sequenze diverse; dopo salvataggio e caricamento la sequenza prosegue identica — Task 3 e Task 6.

---

### Task 1: Oggetti, ricette e profilo delle macchine

**File:**
- Modifica: `data/content.json` (4 voci `market`; ricette `smelt-glass`, `press-graphite`, `separate-sand`, `plasticize-plastic`, `craft-circuit`; campi `recipes`, `powerMode`, `craftSpeed` sui `buildings` di `smelter` e `assembler`)
- Modifica: `src/TIndustry.Shared/Content/ContentTypes.cs`, `Content/FactoryContent.cs`, `Content/ItemVisualColors.cs`, `Buildings/Crafter.cs`, `Simulation/Systems/PowerSystem.cs`, `Factory.cs` (dove si sceglie l'elenco delle ricette)
- Modifica: `godot/lingue/testi.csv` (`oggetto.<id>.nome`, `ricetta.<id>.nome` per i nuovi)
- Test: `tests/TIndustry.Shared.Tests/CrafterProfileTests.cs` (nuovo)

**Interfacce:**
- Produce: `enum CraftPowerMode { CoalOrPower, Boost, Required, None }`; in `BuildingDefinition` i campi `string? RecipePrefix`, `CraftPowerMode PowerMode`, `int CraftSpeedPercent` (100 = 1×; JSON `craftSpeed` float → percento); `RecipeDefinition.RandomOutput` (bool, JSON `randomOutput`: le quantità delle uscite sono pesi, esce un pezzo); `FactoryContent.RecipesFor(string buildingId) -> IReadOnlyList<RecipeDefinition>` (per prefisso; sostituisce le chiamate a `SmelterRecipes()`/`AssemblerRecipes()`, che restano come scorciatoie); `Crafter` costruito con la `BuildingDefinition` (o il profilo) e con `PowerMode`/`CraftSpeedPercent` al posto di `IsAssembler` nei rami di velocità e carbone (`IsAssembler` può restare come proprietà di comodo); `PowerSystem` usa `RateOf(content, craft.DefinitionId)`.
- Valori in `content.json`: `smelter` → `recipes: "smelt-"`, `powerMode: "coal-or-power"`; `assembler` → `recipes: "craft-"`, `powerMode: "boost"`. Validazione: un building con `recipes` ha almeno una ricetta con quel prefisso; `powerMode` sconosciuto rifiutato.

- [ ] **Passo 1: test che falliscono** in `CrafterProfileTests`: `ContentHasTheNewItemsAndRecipes` (B 2/9/20/90, ricette con i numeri dei vincoli, `separate-sand.RandomOutput` vero con pesi 30/30/30/10); `RecipesForSmelterIncludeGlass` (`smelt-iron`, `smelt-lead`, `smelt-titanium`, `smelt-glass`); `SmelterMakesGlassFromSand` (forno con carbone, 2 sabbia → 1 vetro dopo 2 s ±1 tick); `SmelterAndAssemblerSpeedsUnchanged` (forno a corrente piena 1,2×, assemblatore senza corrente 1× e con 1,2×: stessi tick di oggi per `smelt-iron` e `craft-copper-wire`); `UnknownPowerModeIsRefused`.
- [ ] **Passo 2:** `dotnet test tests/TIndustry.Shared.Tests -c Release --filter CrafterProfileTests` → FAIL (tipi e dati mancanti).
- [ ] **Passo 3:** implementazione (profilo, `RecipesFor`, colori degli oggetti nuovi in `ItemVisualColors`, testi it + en).
- [ ] **Passo 4:** `dotnet build tIndustry.sln -c Release -nologo -v q` 0 errori 0 avvisi; `dotnet test tests/TIndustry.Shared.Tests -c Release --no-build` tutto verde (anche `TestiTests`, `DeterminismTests`).
- [ ] **Passo 5: commit** `feat(produzione): sabbia, vetro, plastica e circuiti, ricette e profilo delle macchine`.

### Task 2: Sabbia dal pavimento

**File:**
- Modifica: `src/TIndustry.Shared/Factory.cs` (`PlaceMiner`, `MinerTooWeak`), eventualmente `Grid/TerrainMap.cs` (`CountFloorTiles(origin, size, Func<FloorKind,bool>)`)
- Test: `tests/TIndustry.Shared.Tests/SandMiningTests.cs` (nuovo)

**Interfacce:**
- Consuma: `Miner` com'è; Task 4 renderà la misura per istanza (qui si usa la misura del minatore piazzato).
- Produce: `TerrainMap.IsSandFloor(FloorKind)` (vero per `Sand`, `DarkSand`); `TerrainMap.CountSandTiles(GridPosition origin, int size)` (solo celle senza giacimento).

- [ ] **Passo 1: test che falliscono** (terreno costruito a mano nel test, come in `MinerTierTests`): `MinerOnSandMinesSand` (4 celle di sabbia → `OutputItemId == "sand"`, `CoveredDepositTiles == 4`, produce sabbia al ritmo del T1); `DepositBeatsSand` (2 ferro + 2 sabbia → ferro, coperte 2); `HalfSandHalfStone` (2 sabbia → efficienza 0,5); `T1OnTitaniumAndSandDigsSand` (nessun rifiuto, sabbia); `NoSandOnClassicGenerator` (nessuna cella `Sand`/`DarkSand` in una mappa `classico` 200×200 di un seme fisso).
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi, build 0/0.
- [ ] **Passo 5: commit** `feat(produzione): i minatori scavano sabbia dal pavimento`.

### Task 3: Fonderia avanzata, pressa, separatore, plastificatore e ricetta dei circuiti

**File:**
- Modifica: `data/content.json` (`buildings` e `structures` delle quattro macchine e del nodo `circuit`, costi di Ricerca provvisori uguali per tutti — li riscrive il Task 5)
- Modifica: `Simulation/Commands.cs` (in coda: `SmelterAdvanced`, `GraphitePress`, `Separator`, `Plasticizer`), `Construction/BuildSpec.cs`, `Simulation/GameState.cs` (lista `Machines`, in `CraftMachines()`), `Factory.cs` (liste, piazzamento, rimozione, `TryGetSpecAt`, `FootprintSize` 2, `IsBuildingTile`, `BeltIncoming`, `IsValidSpec`/`FindRecipeFor` per ogni crafter), `Power/PowerGrid.cs` (le macchine nuove sono consumatori, la pressa no), `Buildings/Crafter.cs` (uscita a caso), `Simulation/IGameView.cs` (`Machines`), `Research/ResearchState.cs` se serve per il kind `recipe`
- Modifica: `godot/lingue/testi.csv` (`struttura.<id>.nome/suggerimento/descrizione` delle quattro macchine e di `circuit`)
- Test: `tests/TIndustry.Shared.Tests/AdvancedMachinesTests.cs`, `SeparatorTests.cs` (nuovi); aggiornare `TerrainRulesTests.EverySpec`, `PowerGridTests`

**Interfacce:**
- Consuma: profilo e `RecipesFor` del Task 1.
- Produce: `Crafter.SmelterAdvancedId = "smelter-advanced"`, `GraphitePressId = "graphite-press"`, `SeparatorId = "separator"`, `PlasticizerId = "plasticizer"`; `GameState.Machines` / `Factory.Machines` / `IGameView.Machines` (`IReadOnlyList<Crafter>`); `Crafter.PickRandomOutput(RecipeDefinition r, long seed) -> string` (statico o d'istanza, usa `Id` e `ItemsCrafted`); `FactoryContent.ResearchGatedRecipes` o campo `RecipeDefinition.Research` (JSON `research: "circuit"`); `Factory.RecipesAvailableTo(string buildingId)` = `RecipesFor` meno le ricette con un nodo non sbloccato; `Crafter.SetAvailableRecipes(IReadOnlyList<RecipeDefinition>)` chiamato su tutte le macchine dopo ogni sblocco e dopo il ripristino.
- Valori in `content.json`: `smelter-advanced` → `recipes: "smelt-"`, `powerMode: "required"`, `craftSpeed: 2`, `powerUse: 20`; `graphite-press` → `recipes: "press-"`, `powerMode: "none"`; `separator` → `recipes: "separate-"`, `powerMode: "required"`, `powerUse: 8`; `plasticizer` → `recipes: "plasticize-"`, `powerMode: "required"`, `powerUse: 6`; nodo `circuit`: `kind: "recipe"`, `displayName: "Ricetta dei circuiti"`.

- [ ] **Passo 1: test che falliscono** (`NewGame.Sandbox(..., rules: new GameRules(InstantBuild: true))`, sblocchi via comando): `AdvancedSmelterTwiceAsFastWithFullPower` (`smelt-iron` in 1 s ±1 tick a soddisfazione 10 000); `AdvancedSmelterStopsWithoutPower` (nessun progresso in 600 tick senza rete, anche con carbone sul nastro: non lo accetta); `AdvancedSmelterHalfPowerHalfSpeed`; `PressMakesTwoGraphiteFromThreeCoalWithoutPower` (1,5 s); `PlasticizerMakesPlasticWithPower` e fermo senza; `SeparatorFrequencies` (20 000 sorteggi di `PickRandomOutput` con un seme: ogni minerale fra 28% e 32%, titanio fra 8,5% e 11,5%); `SeparatorSameSeedSameSequence` e `DifferentSeedDifferentSequence` (prime 50 uscite); `CircuitNeedsResearch` (assemblatore con filo, silicio e lastra di piombo: nessun circuito prima di `circuit`; sbloccato il nodo, circuito in 3 s senza ripiazzarlo); `NewMachinesArePowerConsumers` (rete con un generatore: richiesta = somma dei `powerUse`; la pressa non chiede); `RemoveRefundsBuildCost` per ognuna.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi, build 0/0.
- [ ] **Passo 5: commit** `feat(produzione): fonderia avanzata, pressa, separatore, plastificatore e ricetta dei circuiti`.

### Task 4: Trapano a impatto

**File:**
- Modifica: `Buildings/Miner.cs` (misura per istanza: `Size`, `FootprintArea`, `OutputTileCount` da costanti a proprietà; costanti `T1Size = 2`, `ImpactDrillId = "impact-drill"`, `ImpactDrillSize = 3`; `MiningSpeed`, `Efficiency`, `CanReceivePower`, modo della corrente per definizione), `Factory.cs` (`PlaceMiner`, `MinerTooWeak`, `FootprintSize` 3, `BeltIncoming`, `TryGetSpecAt`, rimozione), `Simulation/Systems/MiningSystem.cs`, `PowerSystem.cs` (`RateOf(content, miner.DefinitionId)`), `Power/PowerGrid.cs` (consumatori: T2 e trapano), `Content/FactoryContent.cs` (`CheckMinerTiers` con il trapano), `Commands.cs` (in coda: `ImpactDrill`), `BuildSpec.cs`, `data/content.json` (building `impact-drill`: `tier: 3`, `powerUse: 30`, costo dei vincoli; structure provvisoria), `godot/lingue/testi.csv`
- Test: `tests/TIndustry.Shared.Tests/ImpactDrillTests.cs` (nuovo); aggiornare `MinerTierTests` se usano `Miner.Size`

**Interfacce:**
- Produce: `Miner.SizeFor(string definitionId) -> int` (2, o 3 per il trapano); `Miner.Size` d'istanza; il T2 lavora anche senza corrente (1 + 0,2 × s, come oggi), il trapano solo con corrente (8 × efficienza × s).

- [ ] **Passo 1: test che falliscono**: `DrillOccupiesThreeByThree` (9 celle occupate, un nastro dentro rifiutato); `DrillDigsTitanium` (9 celle di titanio → `titanium-ore`); `DrillIsFourTimesT2` (a corrente piena e 9 celle coperte, pezzi in 60 s = 4 × quelli di un T2 a corrente piena su 4 celle, ±1); `DrillStopsWithoutPower`; `DrillOnSand` (9 celle di sabbia → sabbia); `DrillPartlyOutsideMapRefused`; `DrillEjectsOnAllTwelveSides` (un nastro uscente su ognuno dei 12 lati riceve pezzi); `DrillTouchingCoreDeliversDirectly`.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi (tutti: il cambio di `Miner.Size` tocca molto).
- [ ] **Passo 5: commit** `feat(produzione): trapano a impatto 3×3`.

### Task 5: Ricerca e bilanciamento dei nodi nuovi

**File:**
- Modifica: `tools/dev/genera_finanza.py` (`NODES`: i sei nodi con fase e minuti; materiali di Ricerca per nodo, prima del fattore di fase: `graphite-press` 20 lastre di ferro + 10 lastre di piombo; `smelter-advanced` 40 lastre di titanio + 20 fili di rame; `separator` 40 lastre di titanio + 20 grafite; `plasticizer` 30 silicio + 30 grafite; `circuit` 40 silicio + 40 lastre di piombo; `impact-drill` 60 lastre di titanio + 40 silicio), `tools/dev/test_genera_finanza.py` se elenca nodi, `data/content.json` (costi riscritti con `python tools/dev/genera_finanza.py --scrivi-costi data/content.json`)
- Modifica: `src/TIndustry.Shared/Research/TechTreeLayout.cs` solo se il kind `recipe` va disegnato a parte; `tests/TIndustry.Shared.Tests/ResearchTreeContentTests.cs` (`ExpectedParent` dei sei nodi), `ResearchCostTests.cs` (forbice dei soldi: la nuova somma ±5%, se la vecchia 22–25 milioni non regge più)
- Modifica (se serve): `data/campaign.json` e i test della campagna, perché restino completabili

- [ ] **Passo 1: test che falliscono**: Python `test_genera_finanza` (nodi nuovi presenti, ore senza jackpot fra 5,5 e 6,5, `--controlla` pulito dopo `--scrivi-costi`); C# `ResearchTreeContentTests` coi genitori dei vincoli.
- [ ] **Passo 2:** FAIL. **Passo 3:** minuti dei nodi nuovi scelti perché le ore senza jackpot restino fra 5,5 e 6,4; poi `--scrivi-costi`.
- [ ] **Passo 4:** `python -m unittest tools/dev/test_genera_finanza.py`, `python tools/dev/genera_finanza.py --controlla data/content.json`, test C# verdi, `dotnet run --project tools/MapTool -- valida` OK.
- [ ] **Passo 5: commit** `feat(ricerca): nodi delle macchine nuove e della ricetta dei circuiti`.

### Task 6: Salvataggi v21 e agenti

**File:**
- Modifica: `Save/FactorySave.cs` (v21: lista `Machines` di `CraftSaveDto`, `null` se vuota; il trapano viaggia nei `Miners` con il suo `DefinitionId`; commento in testa coi formati), `Factory.cs` (`Capture`, ripristino, `maxSavedId`, `SortById`, ricette disponibili ricalcolate dopo il ripristino), `Agent/AgentGame.cs` (`Legend`, `KindSymbol`, `Buildings()`, contatori di `stato`), `tools/AgentPlay/Tools.cs` e `tools/AgentPlay/README.md` (blocchi nuovi), `CLAUDE.md` (riga dei salvataggi: «versione 21»)
- Test: `SaveRoundTripTests`, `SaveVersionTests`, `AgentGameTests`, `DeterminismTests`

- [ ] **Passo 1: test che falliscono**: v21 avanti e indietro con una macchina per tipo, un trapano, un minatore su sabbia, un separatore a metà ricetta e un assemblatore coi circuiti sbloccati (stesso checksum, stessa sequenza del separatore dopo il caricamento); v20 rifiutato; `piazza` di ognuno dei cinque blocchi nuovi dagli agenti; simbolo di legenda per ogni `BuildingKind`; determinismo 10 minuti con tutte le macchine nuove.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi (Shared e `tests/TIndustry.AgentPlay.Tests`).
- [ ] **Passo 5: commit** `feat(salvataggi): v21 con le macchine nuove, e i blocchi nuovi per gli agenti`.

### Task 7: Client e arte

**File:**
- Modifica: `tools/art/blocchi/produzione.py` (sprite `smelter-advanced`, `graphite-press`, `separator`, `plasticizer`: icona e base 64×64 più un'animazione di lavoro `<id>-anim` di 4 fotogrammi; `impact-drill`: icona e base 96×96, `impact-drill-drill-anim`), `tools/art/blocchi/oggetti.py` (icone e sprite da nastro di `sand`, `glass`, `plastic`, `circuit`), `tools/art/test_genera.py` (`CELLE` e test di copertura), PNG rigenerati con `python tools/art/genera.py`
- Modifica: `godot/scripts/ui/hud/Hud.cs`, `BuildPalette.cs`, `world/World.cs`, `World.Input.cs`, `World.Session.cs`, `World.Drawing.cs`, `World.Commands.cs`, `World.Visuals.cs`, `visuals/buildings/MinerVisual.cs` (misura 3), visuale nuova `visuals/buildings/MachineVisual.cs` (base + animazione quando `IsWorking`), `ui/hud/BlockInfoPanel.cs`, `ui/panels/ResearchPanel.cs` (righe di ingresso/uscita delle macchine nuove e del nodo `circuit`), `visuals/construction/PlacementPreview.cs` se serve, `godot/lingue/testi.csv`
- Test: `tools/art/test_genera.py`, `TestiTests`, `ContentValidationTests.EveryStructureHasAnIcon`, `GodotProjectReferencesTests`

- [ ] **Passo 1:** sprite e test dell'arte (`python -m unittest tools/art/test_genera.py` verde, `python tools/art/genera.py --controlla --pixel` pulito).
- [ ] **Passo 2:** client: palette (Produzione: fonderia avanzata, pressa, separatore, plastificatore, trapano), fantasma e visuali; build 0/0 della soluzione (compila anche `godot/`).
- [ ] **Passo 3:** test verdi (Shared, AgentPlay, arte).
- [ ] **Passo 4: commit** `feat(client): macchine nuove e oggetti nuovi nella palette e nel mondo`.

### Task 8: Documenti, stato e diario

**File:** `docs/wiki/Produzione.md`, `Economia-e-Ricerca.md`, `Salvataggi-formato.md`, `Contenuti-e-dati.md`, `Mercato-come-una-borsa.md` se elenca gli oggetti, `godot/README.md`, `CHANGELOG.md`, `docs/STATO.md` (casella «Ricerca allargata, piano 1» fatta, «Cosa manca, in piccolo» col controllo del client sul PC di casa), `docs/mappa-progetto.canvas` (`python tools/dev/mappa_canvas.py`), barre (`python tools/sito/stato.py`), diario `docs/diario/2026-11-11-ricerca-allargata-1.md` dal registro `.superpowers/sdd/2026-11-11-ricerca-allargata-1/progress.md`

- [ ] **Passo 1:** documenti aggiornati; `python tools/dev/mappa_canvas.py`; `python tools/sito/stato.py`.
- [ ] **Passo 2:** `python -m unittest discover -s tools/sito -p "test_*.py"` e `node --test tools/sito/test_sito.mjs tools/sito/test_racconto.mjs` verdi.
- [ ] **Passo 3: commit** `docs: Ricerca allargata, piano 1 fatto`.
