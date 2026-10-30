# Rete di corrente come Mindustry — piano di implementazione

> **Per gli agenti:** SOTTO-SKILL RICHIESTA: superpowers:subagent-driven-development (consigliata) o superpowers:executing-plans, task per task. I passi usano le caselle (`- [ ]`).

**Obiettivo:** nodi collegati da laser (automatici e a mano), reti con bilancio proprio, soddisfazione in proporzione, batterie; via la riserva unica e la corrente del CORE; salvataggi v17.

**Architettura:** un `PowerGrid` in `GameState` tiene i laser (coppie di id) e ricostruisce le reti (unione per contatto e per laser) quando cambiano blocchi o laser, in ordine di id. `PowerSystem`, dopo aver acceso i generatori, raccoglie le richieste del tick, fa il bilancio per rete in interi e scrive la soddisfazione per rete; i sistemi che consumano la leggono. I numeri vengono da `content.json`.

**Tecnologie:** C# .NET 8 (`src/TIndustry.Shared`, xUnit), `data/content.json`, client Godot 4.4 .NET, sprite da `tools/art/genera.py`.

**Specifica:** [`docs/superpowers/specs/2026-10-30-rete-corrente-design.md`](../specs/2026-10-30-rete-corrente-design.md)

## Vincoli globali

- Nodo T1 (`power-node`): raggio 6, 6 collegamenti. Nodo T2 (`power-node-t2`, 2×2): raggio 10, 10 collegamenti.
- Generatore 30/s; consumi forno 8/s, assemblatore 10/s, Minatore T2 0,5/s, lampada 1/s; batteria (`battery`, 1×1) capacità 1.000; Ricerca 300 $ + 10 `lead-plate` + 5 `copper-wire` dopo `power-node`; costruzione 10 `lead-plate` + 4 `copper-wire`.
- Unità interne: il bilancio confronta **ritmi** in millesimi di unità al secondo (generatore 30.000, forno 8.000, assemblatore 10.000, Minatore T2 500, lampada 1.000), così ogni tick è esatto; la carica delle batterie è in sessantamillesimi di unità (a ogni tick si aggiunge o toglie il ritmo in eccesso o in difetto; 1.000 unità = 60.000.000). Soddisfazione in decimillesimi (0–10.000). Niente float nel bilancio.
- Bonus: assemblatore e Minatore T2 `1 + 0,2 × s`; lampada luce `s`; forno `1,2 × s` con corrente, carbone a 1 se `1,2 × s < 1` e ha carbone (in quel tick non chiede corrente).
- Il CORE non produce corrente; via `PowerBuffer`, `PowerCapacity`, `CorePowerCapacity`, `CorePowerGeneration`, `Generator.CapacityBonus`, `IsPoweredViaNode`.
- Salvataggi **v17** (`CurrentVersion = MinReadableVersion = 17`).
- Regole di sempre (CLAUDE.md): determinismo, testi da `testi.csv` it+en, stili dal tema, commenti in inglese, commit in italiano con `Co-Authored-By:`, STATO + mappa + barre.

## Punti da sorvegliare in revisione

1. **Macchina che tocca due reti diverse** (per esempio un forno fra due generatori non collegati): per contatto unisce le due reti in una sola — test in Task 2.
2. **Nodo smontato in mezzo a una catena**: la rete si spezza e ogni metà fa il suo bilancio subito, nello stesso tick — test in Task 2.
3. **Richiesta zero** (nessuna macchina lavora): soddisfazione 100% e niente divisione per zero; il surplus va alle batterie — test in Task 2.
4. **Laser verso un blocco in cantiere o appena smontato**: rifiutato o tolto; nessun laser verso un id che non esiste dopo un caricamento — test in Task 1 e Task 4.
5. **Ordine dei collegamenti automatici a parità di distanza**: id più basso, sempre uguale fra due corse — test in Task 1.

---

### Task 1: Dati, laser e reti

**File:**
- Modifica: `data/content.json` (`powerOutput`, `powerUse`, nodi, `battery` struttura e building), `Content/ContentTypes.cs`, `Content/FactoryContent.cs`
- Crea: `src/TIndustry.Shared/Power/PowerGrid.cs`, `src/TIndustry.Shared/Power/Battery.cs`
- Modifica: `Simulation/GameState.cs` (lista `Batteries`, `PowerGrid`), `Factory.cs` (piazzamento/smontaggio della batteria, collegamenti automatici a fine cantiere del nodo, laser tolti allo smontaggio), `Simulation/Commands.cs` (`BuildingKind.Battery`, `TogglePowerLink`, `RefusalCode` nuovi), `Construction/BuildSpec.cs`, `Simulation/SimHost.cs`
- Test: crea `tests/TIndustry.Shared.Tests/PowerGridTests.cs`; `ContentValidationTests`

**Interfacce:**
- `public sealed class PowerGrid`: `IReadOnlyList<(long A, long B)> Links` (ordinati, `A < B`); `bool HasLink(long a, long b)`; `int LinksOf(long nodeId)`; `IReadOnlyList<PowerNetwork> Networks`; `PowerNetwork? NetworkOf(long entityId)`; `void Rebuild(GameState s)` (contatto + laser, union-find su id ordinati); `internal void AddLink/RemoveLink`.
- `public sealed record PowerNetwork(int Index, IReadOnlyList<long> Members)` più i campi del bilancio del Task 2.
- `RefusalCode.PowerLinkOutOfRange`, `PowerLinkNoFreeSlots`, `PowerLinkNotPowerBlock`, `PowerLinkSameBlock`.
- `TogglePowerLink(int PlayerId, long FromNodeId, long ToEntityId)`.
- Collegamenti automatici: `Factory.AutoLinkNode(PowerNode node)` chiamato quando il cantiere del nodo finisce (e da `PlaceInstant`), con l'ordine della specifica.

- [ ] **Passo 1: test che falliscono** (con `GameRules(InstantBuild: true)`): contenuti coi numeri dei vincoli; catena di 5 nodi T1 da un generatore a un forno a 30 celle → stessa rete; contatto generatore–forno → stessa rete; due generatori separati da un forno che li tocca entrambi → una rete; collegamenti automatici dal più vicino, limite 6, laser fra nodi contato su entrambi, parità di distanza → id più basso; toggle aggiunge/toglie e i quattro rifiuti; smontare il nodo di mezzo spezza la rete in due; un nodo in cantiere non ha laser.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** build 0/0, test verdi (i test di corrente esistenti possono restare sulla vecchia regola finché il Task 2 non la sostituisce: niente cancellazioni qui).
- [ ] **Passo 5: commit** `feat(corrente): laser fra i nodi, batteria e reti`.

### Task 2: Bilancio per rete e consumatori

**File:**
- Modifica: `Simulation/Systems/PowerSystem.cs`, `CraftingSystem.cs`, `MiningSystem.cs`, `LightingSystem.cs`, `Buildings/Crafter.cs`, `Buildings/Miner.cs`, `Power/Lamp.cs`, `Power/Generator.cs`, `Simulation/GameState.cs` (via riserva unica e `IsPoweredViaNode`), `Simulation/IGameView.cs`, `Factory.cs`
- Test: `PowerGridTests` (bilancio), aggiorna `PoweredMinerTests`, `LampAndNightTests`, `CrafterWorkingTests` e gli altri test sulla corrente

**Interfacce:**
- `PowerNetwork`: `long ProducedRate`, `long RequestedRate` (millesimi/s), `int SatisfactionBp` (0–10.000), `long Stored`, `long Capacity` (sessantamillesimi).
- `GameState.SatisfactionOf(long entityId) : int` (0 se fuori rete).
- `IGameView`: `IReadOnlyList<(long A, long B)> PowerLinks`, `PowerNetworkView? PowerNetworkOf(GridPosition tile)`.
- Due fasi per tick: le macchine dichiarano la richiesta (`WantsPower`), `PowerSystem` fa il bilancio, poi lavorano con `s`; ordine dei sistemi aggiornato in `SystemOrderTests`.

- [ ] **Passo 1: test che falliscono**: 1 generatore + 3 forni = 10.000 bp; 1 generatore + 5 forni = 7.500 bp; richiesta zero = 10.000 bp e surplus in batteria; batteria piena copre il buco quando il generatore resta senza carbone, poi si svuota; due reti separate non si aiutano; carica divisa per capacità e conservata spezzando/unendo; assemblatore a 50% → velocità 1,1; Minatore T2 a 50% → +10%; lampada a 50% → luce 0,5; forno a 50% con carbone → brucia carbone a 1 e non chiede corrente; forno senza carbone a 50% → 0,6; CORE senza corrente.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione; via la riserva unica. **Passo 4:** test verdi, `DeterminismTests` verdi.
- [ ] **Passo 5: commit** `feat(corrente): bilancio per rete, soddisfazione e batterie`.

### Task 3: Salvataggi v17 e agenti

**File:** `Save/FactorySave.cs` (v17, laser, carica per batteria; via `PowerBuffer`/`PowerCapacity`), `Factory.cs` (snapshot/restore, laser verso id inesistenti → salvataggio illeggibile), `Agent/AgentGame.cs` (comando `collega` con `nodo` e `blocco`, reti nello stato), `Agent/AgentJournal.cs` (versione 4 se il setup cambia), `tools/AgentPlay/README.md`, `CLAUDE.md` (riga dei salvataggi: «versione 17»)
- Test: `SaveRoundTripTests`, `SaveVersionTests`, `AgentGameTests`, `DeterminismTests`

- [ ] **Passo 1: test che falliscono**: v17 avanti e indietro con laser e carica (stesso checksum); laser verso un id che non c'è → `Unreadable`; v16 → `OlderVersion`; comando agenti `collega` e i rifiuti; determinismo con reti che si dividono e si uniscono in 5 minuti.
- [ ] **Passo 2:** FAIL. **Passo 3:** implementazione. **Passo 4:** test verdi (anche AgentPlay).
- [ ] **Passo 5: commit** `feat(corrente): salvataggi v17 e agenti`.

### Task 4: Client

**File:** `tools/art/genera.py` (+ test) per lo sprite della batteria con la carica; `godot/scripts/world/World.Drawing.cs`, `World.Visuals.cs`, `World.Commands.cs` (laser al posto delle linee di oggi; nodo selezionato: raggio e collegamenti usati/massimi; clic su un blocco nel raggio = `TogglePowerLink`; suggerimento con produzione, consumo, percentuale e batterie); palette e Ricerca per la batteria; `godot/lingue/testi.csv` (rifiuti nuovi, suggerimento, batteria)
- Test: `test_genera.py`, `TestiTests`; playtest sul PC di casa

- [ ] **Passo 1:** sprite e testi; `python -m unittest tools/art/test_genera.py`, `genera.py --controlla`.
- [ ] **Passo 2:** client; build 0/0, `TestiTests` verdi.
- [ ] **Passo 3 (PC di casa):** playtest `ui`, `tutorial`, `agenti`; a mano la catena di nodi della cattura dell'utente.
- [ ] **Passo 4: commit** `feat(client): laser della corrente, batteria e reti`.

### Task 5: Documenti, stato e diario

**File:** `docs/wiki/Energia.md` (riscritta: rete, laser, batterie, numeri), `godot/README.md`, `README.md`, `docs/ARCHITETTURA.md`, `docs/wiki/Salvataggi-formato.md`, `CHANGELOG.md`, `docs/STATO.md` (aspetto 5: implementazione e documenti), mappa, barre, diario `docs/diario/2026-10-30-rete-corrente.md`, specifica «attuata», chiusura di #269 nel testo della PR.

- [ ] **Passo 1:** testi coi numeri dei vincoli.
- [ ] **Passo 2:** `python tools/sito/stato.py`, `python tools/dev/mappa_canvas.py`, `python tools/dev/controlla_wiki.py`, test del sito.
- [ ] **Passo 3: commit** `docs: rete di corrente`.
