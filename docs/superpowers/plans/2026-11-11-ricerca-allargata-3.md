# Ricerca allargata, piano 3: potenziamenti, premi della campagna e bilanciamento — piano di implementazione

> **Per gli agenti:** SOTTO-SKILL RICHIESTA: superpowers:subagent-driven-development (consigliata) o superpowers:executing-plans, task per task. I passi usano le caselle (`- [ ]`).

**Obiettivo:** potenziamenti del drone, della produzione e del magazzino come nodi della Ricerca; quattro nodi premio dei capitoli della campagna (non comprabili in campagna, a prezzo ×3 nella partita libera); bilanciamento di tutta la Ricerca a circa 6 ore.

**Architettura:** i potenziamenti sono nodi senza blocco (`kind: "upgrade"`) letti con `Research.IsUnlocked`, come i moduli della Finanza; un solo punto per gruppo applica il bonus (unità effettiva del drone, percentuale dei minatori, percentuale delle macchine, percentuale del magazzino), in interi o con un ×1 esatto al livello 0. I premi hanno un campo `campaignReward` (numero del capitolo): in campagna il comando di sblocco li rifiuta e `Factory.GrantCampaignReward` li dà; il prezzo ×3 lo scrive lo script nel contenuto. Il bilanciamento aggiunge plastica e circuiti ai prodotti delle fasi e ritocca i minuti.

**Tecnologie:** C# .NET 8 (`src/TIndustry.Shared`, xUnit), `data/content.json`, Python 3 (`genera_finanza.py`, `genera.py`), client Godot 4.4 .NET.

**Specifica:** [`docs/superpowers/specs/2026-11-09-ricerca-allargata-design.md`](../specs/2026-11-09-ricerca-allargata-design.md) (sezioni «Potenziamenti», «Premi della campagna», «Ricerca e bilanciamento»).

**Briefing tecnico:** `.superpowers/sdd/2026-11-11-ricerca-allargata-3/briefing.md`; punti da toccare: `.superpowers/sdd/2026-11-11-ricerca-allargata-3/touchpoints.md`.

## Vincoli globali

- Nodi dei potenziamenti (`kind: "upgrade"`, nessun blocco), con il bonus **sostituito** (non sommato) dal livello 2:
  - `upgrade-drone-build` «Drone: costruzione» +25% velocità di costruzione; `upgrade-drone-hold` «Drone: stiva» +50% capienza; `upgrade-drone-range` «Drone: raggio» +5 celle; `upgrade-drone-mine` «Drone: scavo» +25% ritmo dello scavo a mano. Livello 2 dei quattro insieme: un nodo solo, `drone-boost` «Drone potenziato» (+50%, +100%, +10 celle, +50%), prerequisiti i quattro livelli 1.
  - `upgrade-miners-1` «Minatori I» +10%, `upgrade-miners-2` «Minatori II» +20% (tutti i minatori: T1, T2, trapano).
  - `upgrade-machines-1` «Macchine I» +15%, `upgrade-machines-2` «Macchine II» +30% (forno, fonderia avanzata, assemblatore).
  - `upgrade-storage-1` «Magazzino I» +25%, `upgrade-storage-2` «Magazzino II» +50% al massimo per oggetto del CORE (tier più depositi collegati) e dei depositi a sé (contenitore, deposito).
- Arrotondamenti: capienze e massimi per difetto (`n × (100 + p) / 100` in interi); ritmo dello scavo a mano in millesimi interi; velocità di costruzione, raggio, minatori e macchine moltiplicano i float di oggi per `(100 + p) / 100f`, che vale esattamente 1 senza potenziamenti. Effetto dal tick dopo lo sblocco; nessun oggetto perso quando un massimo cresce.
- Albero (primo prerequisito = genitore; fase del foglio): i quattro `upgrade-drone-*` senza prerequisiti (fase 1); `drone-boost` ← i quattro (fase 3); `upgrade-miners-1` ← `miner-advanced` (fase 1); `upgrade-miners-2` ← `upgrade-miners-1` (fase 2); `upgrade-machines-1` ← `assembler` (fase 2); `upgrade-machines-2` ← `upgrade-machines-1`, `smelter-advanced` (fase 3); `upgrade-storage-1` ← `container` (fase 2); `upgrade-storage-2` ← `upgrade-storage-1`, `vault` (fase 3).
- **Premi della campagna** (`campaignReward`: numero del capitolo): 1 `combustion-generator`, 2 `conveyor-stack`, 3 `circuit`, 4 `drone-boost`. In campagna (`ActiveCampaignLevelId` non nullo) lo sblocco è rifiutato con `RefusalCode.CampaignRewardOnly`; `Factory.GrantCampaignReward(string nodeId)` lo sblocca senza costo (lo userà il lancio della campagna rifatta). Nella partita libera si comprano al prezzo del foglio **×3** (soldi e materiali), scritto dallo script.
- Bilanciamento: costi solo dallo script; prodotti nuovi nelle fasi: `plastic` nella fase Avanzata, `circuit` nella fase Fine (pezzi al secondo scelti dal piano: Avanzata titanio 12, silicio 12, plastica 6; Fine titanio 36, silicio 35, circuiti 8); ore senza jackpot **fra 5,5 e 6,5** con obiettivo 6,0 ± 0,2, premi compresi al prezzo ×3; costi crescenti lungo ogni catena; la forbice dei soldi dei test segue il totale nuovo (±5% attorno al valore del foglio).
- Salvataggi: nessun campo nuovo (gli sblocchi si salvano già) → resta **v22**.
- Regole di sempre (CLAUDE.md); ogni commit verde (icone dei nodi da `genera.py`, testi it + en, nodi in `NODES`, `ExpectedParent`).

## Punti da sorvegliare in revisione

1. **Stiva che cresce e cala**: sbloccare la stiva a metà scavo non perde pezzi; un salvataggio caricato con la stiva piena e il potenziamento sbloccato non viene tagliato al massimo senza potenziamento (il ripristino legge la Ricerca prima di tagliare) — Task 1.
2. **Massimo del CORE e vendita automatica**: la vendita automatica usa il massimo con il potenziamento — Task 1.
3. **Premio in un salvataggio della campagna**: un nodo dato con `GrantCampaignReward` resta dopo il caricamento; in campagna l'agente che prova `sblocca` riceve il rifiuto giusto — Task 2.
4. **Tutto l'albero sbloccabile nella partita libera** (`ResearchCostTests`) coi prezzi ×3 senza trabocchi — Task 2 e Task 3.
5. **Campagna di oggi ancora completabile** coi costi nuovi e coi premi non comprabili (nessun livello di oggi chiede un nodo premio) — Task 3.

---

### Task 1: Potenziamenti nella simulazione

**File:** `data/content.json` (dieci strutture `kind: "upgrade"` con `unlock` segnaposto), `Content/FactoryContent.cs` (validazione del kind), classe nuova `Research/Upgrades.cs` (lettura dei bonus dalla Ricerca: `DroneBuildPercent`, `DroneHoldPercent`, `DroneRangeCells`, `DroneMinePercent`, `MinerPercent`, `MachinePercent`, `StoragePercent`), `Simulation/GameState.cs` (`EffectiveUnit`), `ConstructSystem`, `BuilderSystem`, `HandMiningSystem`, `Factory.cs` (`IsPlanInRange`, `IsMiningNow`, ripristino della stiva, massimo del CORE), `Buildings/Miner.cs` + `MiningSystem` (`RatePercent`), `Buildings/Crafter.cs` + `CraftingSystem` (`BonusPercent`), `Economy/CoreStorage.cs` e `Buildings/StorageBlock.cs` (percentuale del magazzino), `Agent/AgentGame.cs` (`stato.potenziamenti`, raggio e capienza effettivi), `tools/dev/genera_finanza.py` (`NODES`), sprite delle icone dei nodi (`tools/art/blocchi/interfaccia.py` o accanto ai moduli della Finanza), `godot/lingue/testi.csv`, client solo per compilare e leggere l'unità effettiva (cerchio del raggio, stiva nell'HUD); test nuovi `UpgradeTests.cs`

- [ ] **Passo 1: test che falliscono**: per ogni gruppo l'effetto esatto dei due livelli (costruzione: tick per finire un cantiere ×1/1,25/1,5 ±1; stiva 30/45/60 per alpha; raggio 27,5/32,5/37,5; scavo 700/875/1050 millesimi per tick; minatore T1: pezzi in 600 tick a +0/+10/+20% ±1; forno, fonderia avanzata, assemblatore +15/+30%, pressa e separatore invariati; massimo del CORE 1000/1250/1500 e contenitore a sé 300/375/450); nessun cambiamento di checksum senza potenziamenti (`DeterminismTests` invariati); sblocco a metà scavo senza perdite; stiva piena salvata col potenziamento e ricaricata senza tagli; vendita automatica col massimo nuovo; `stato.potenziamenti` dell'agente.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione (icone, testi, nodi con minuti provvisori e `--scrivi-costi`, `ExpectedParent`). **Passo 4:** build 0/0; Shared, AgentPlay, arte, `test_genera_finanza`, `--controlla` verdi (le ore possono superare 6,5 solo se il test lo permette: altrimenti minuti piccoli, il riequilibrio è del Task 3).
- [ ] **Passo 5: commit** `feat(ricerca): potenziamenti del drone, della produzione e del magazzino`.

### Task 2: Premi della campagna

**File:** `data/content.json` (`campaignReward` sui quattro nodi), `Content/ContentTypes.cs` (`StructureDefinition.CampaignReward`, int?), `FactoryContent.cs` (validazione: 1–4, uno per capitolo), `Simulation/Commands.cs` (`RefusalCode.CampaignRewardOnly` in coda), `SimHost.cs` (`ApplyUnlock`), `Factory.cs` (`GrantCampaignReward`, rifiuto in `TryUnlockStructure` se in campagna), `tools/dev/genera_finanza.py` (premi ×3 su soldi e materiali; `REWARDS`), `Agent/AgentGame.cs` (rifiuto tradotto, `ricerca` mostra il premio), `godot/lingue/testi.csv` (rifiuto, «Premio del capitolo {0}»), client Ricerca (etichetta del premio al posto del prezzo in campagna); test nuovi `CampaignRewardTests.cs`, `test_genera_finanza.py`

- [ ] **Passo 1: test che falliscono**: in campagna `UnlockStructure` di un premio → `CampaignRewardOnly`, soldi intatti; `GrantCampaignReward` lo sblocca senza costo, anche coi prerequisiti mancanti, e sopravvive al salvataggio; nella partita libera il prezzo è il triplo del foglio (Python) e lo sblocco funziona; `content.json` ha esattamente i quattro premi dei capitoli 1–4; `ResearchCostTests` sblocca tutto l'albero nella partita libera senza trabocchi.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi.
- [ ] **Passo 5: commit** `feat(ricerca): premi dei capitoli della campagna`.

### Task 3: Bilanciamento della Ricerca

**File:** `tools/dev/genera_finanza.py` (`PHASES` coi prodotti nuovi, minuti di tutti i nodi rivisti), `tools/dev/test_genera_finanza.py` (forbici dei soldi e delle ore), `tests/TIndustry.Shared.Tests/ResearchCostTests.cs`, `data/content.json` (costi riscritti), `data/campaign.json` e test della campagna solo se servono a tenerla completabile

- [ ] **Passo 1: test che falliscono**: ore senza jackpot fra 5,8 e 6,2 (obiettivo del piano dentro la regola 5,5–6,5); con 1 e 2 jackpot più corte; forbice dei soldi ±5% attorno al totale nuovo; costi crescenti lungo ogni catena (premi compresi).
- [ ] **Passo 2:** FAIL. **Passo 3:** `PHASES` e minuti; `--scrivi-costi`.
- [ ] **Passo 4:** `python3 -m unittest tools/dev/test_genera_finanza.py`, `--controlla`, test C# (campagna e tutorial compresi), `dotnet run --project tools/MapTool -- valida` OK.
- [ ] **Passo 5: commit** `feat(economia): Ricerca allargata bilanciata a circa 6 ore`.

### Task 4: Client

**File:** `godot/scripts/ui/panels/ResearchPanel.cs`, `TechTreeCanvas.cs` (nodi `upgrade`, etichetta del premio in campagna, prezzo ×3 nella partita libera già nel contenuto), HUD (stiva e raggio effettivi già dal Task 1: controllare), `BlockInfoPanel` se mostra capienze, testi

- [ ] **Passo 1:** client; build 0/0; `TestiTests`, `ContentValidationTests`, test dell'arte verdi.
- [ ] **Passo 2: commit** `feat(client): potenziamenti e premi nella Ricerca`.

### Task 5: Documenti, stato e diario

**File:** `docs/wiki/Economia-e-Ricerca.md` (albero, potenziamenti, premi, ore), `Campagna-e-mappe.md` (premi), `Contenuti-e-dati.md` (`kind: upgrade`, `campaignReward`), `CHANGELOG.md`, `docs/STATO.md` (casella del piano 3; aspetto 8: la campagna rifatta è il prossimo passo), mappa e barre, diario `docs/diario/2026-11-11-ricerca-allargata-3.md`

- [ ] **Passo 1:** documenti; mappa e barre rigenerate; test del sito verdi.
- [ ] **Passo 2: commit** `docs: Ricerca allargata, piano 3 fatto`.
