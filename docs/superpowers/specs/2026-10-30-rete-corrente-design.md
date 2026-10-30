# Rete di corrente come Mindustry

- **Stato**: da approvare. Brainstorming con l'utente del 30 ottobre 2026, issue [#269](https://github.com/Juan0177/tIndustry/issues/269).
- **Sostituisce**: la regola di oggi (un blocco è alimentato se tocca un generatore acceso, o un nodo che ha un generatore acceso nel raggio; i nodi non si passano la corrente; `GameState.IsPoweredViaNode`), la riserva unica di corrente (`PowerBuffer`, `PowerCapacity`, `CorePowerCapacity`, `CorePowerGeneration`, `Generator.CapacityBonus`) e i consumi scritti nel codice (`CraftingSystem.SmelterPowerDraw`, `AssemblerPowerDraw`, `Generator.GenerationPerSecond`).

## Obiettivo

La corrente viaggia come in Mindustry: i nodi si collegano con dei laser, si passano la corrente fra loro e formano una rete con generatori, batterie e macchine. Una catena di nodi dal generatore al forno funziona, come il giocatore si aspetta.

Successo: una catena di nodi T1 porta la corrente da un generatore a un forno a 30 celle; con poca corrente tutte le macchine della rete rallentano insieme; una batteria tiene accesa la rete quando il carbone finisce.

## Decisioni prese con l'utente

| Tema | Decisione |
|---|---|
| Collegamenti | **Come Mindustry**: automatici al piazzamento del nodo, poi modificabili cliccando il nodo e un blocco. |
| Poca corrente | **In proporzione**: tutte le macchine della rete ricevono la stessa percentuale. |
| Batterie | **Sì**, blocco Batteria 1×1. Il CORE non produce più corrente e la riserva unica sparisce. |
| Nodi | T1: raggio **6**, **6** collegamenti. T2 (2×2): raggio **10**, **10** collegamenti. |
| Numeri | Generatore **30**/s; forno 8/s, assemblatore 10/s, Minatore T2 0,5/s, lampada 1/s; batteria **1.000**. Tutti in `content.json`. |

## Regole

### Blocchi di corrente

Sono blocchi di corrente: generatore, nodo T1, nodo T2, batteria e ogni blocco con un consumo (forno, assemblatore, Minatore T2, lampada). Gli altri (nastri, minatore T1, estrattore, magazzini, CORE) non fanno parte di nessuna rete.

### Collegamenti

- **Per contatto**: due blocchi di corrente che si toccano lato contro lato sono collegati, senza laser e senza consumare collegamenti.
- **Laser**: un nodo si collega a un altro blocco di corrente se la distanza fra i bordi dei due blocchi è al massimo il raggio del nodo (la distanza si misura come per il raggio di oggi, `PowerNode.IsWithinRange`). Un laser conta un collegamento sul nodo; un laser fra due nodi conta su entrambi e vale se almeno uno dei due raggi basta.
- **Automatici**: quando un nodo viene costruito (a cantiere finito), si collega:
  1. ai nodi nel suo raggio, dal più vicino (a parità, id più basso), finché entrambi hanno collegamenti liberi;
  2. poi ai blocchi di corrente nel raggio che non sono su nessuna rete con un generatore o una batteria, dal più vicino, finché ha collegamenti liberi.
- **A mano**: comando «collega/scollega» da un nodo a un blocco: se il laser c'è lo toglie, altrimenti lo aggiunge. Rifiuti con un motivo: fuori raggio, nodo senza collegamenti liberi, blocco che non è di corrente, stesso blocco.
- Smontare un blocco toglie i suoi laser. I laser non cambiano mentre un blocco è in cantiere: un nodo in costruzione non ha laser.

### La rete

- Una rete è l'insieme dei blocchi di corrente collegati, per laser o per contatto. Le reti si ricalcolano quando cambiano i blocchi o i laser, in ordine fisso (id).
- A ogni tick, per ogni rete:
  - **produzione** = somma dei generatori accesi (30/s ciascuno);
  - **richiesta** = somma dei consumi delle macchine che in quel tick vogliono lavorare (un forno che non ha niente da cuocere non chiede corrente);
  - se la produzione basta: tutte le macchine ricevono il 100%, il resto va nelle batterie della rete fino a riempirle;
  - se non basta: le batterie danno quello che manca finché hanno carica;
  - se ancora non basta: **soddisfazione** = (produzione + batterie) ÷ richiesta, la stessa per tutte le macchine; le batterie restano vuote.
- La carica di una rete si divide fra le sue batterie in proporzione alla capacità; spezzando o unendo reti, ogni batteria tiene la sua carica.
- Conti interi: corrente in millesimi di unità per tick (30/s = 500 millesimi a tick), soddisfazione in decimillesimi; nessun float nei conti della rete.

### Effetto sulle macchine

Con soddisfazione `s` (da 0 a 1):

- **Assemblatore**: lavora sempre; velocità `1 + 0,2 × s`.
- **Minatore T2**: velocità `1 + 0,2 × s` (oltre al suo 2×).
- **Lampada**: luce `s` (spenta a 0).
- **Forno**: con la corrente va a `1,2 × s`; se ha carbone e `1,2 × s < 1` brucia il carbone e va a 1, senza chiedere corrente in quel tick. Senza carbone e senza corrente è fermo.

### CORE

Il CORE non produce corrente e non è un blocco di corrente.

## Contenuti (`data/content.json`)

- `generator.powerOutput: 30`; consumi `powerUse`: forno 8, assemblatore 10, Minatore T2 0,5, lampada 1 (unità al secondo).
- Nodi: `power-node` raggio 6, collegamenti 6; `power-node-t2` raggio 10, collegamenti 10.
- Struttura nuova `battery` (1×1, `powerCapacity: 1000`): Ricerca 300 $ + 10 `lead-plate` + 5 `copper-wire`, prerequisito `power-node`; costruzione 10 `lead-plate` + 4 `copper-wire`.
- Via la nota della `refundPolicyNote` sulla corrente; le regole stanno nella Wiki.

## Simulazione

- `PowerGrid` (nuovo, in `Power/`): laser (coppie di id ordinate), reti ricalcolate in ordine fisso, bilancio per rete, soddisfazione e carica per batteria.
- `PowerSystem` fa il bilancio prima dei sistemi che consumano; `CraftingSystem`, `MiningSystem`, `LightingSystem` leggono la soddisfazione della rete della macchina.
- Comando `TogglePowerLink(PlayerId, FromNodeId, ToEntityId)` e `RefusalCode` nuovi (fuori raggio, collegamenti esauriti, non di corrente).
- `IGameView`: laser, rete di un blocco (produzione, richiesta, soddisfazione, carica e capacità).
- Agenti: comando `collega` (nodo, blocco) e reti nello stato.
- Salvataggi **v17**: laser e carica delle batterie; via `PowerBuffer`/`PowerCapacity`. I v16 non si aprono.

## Client

- Laser disegnati fra i blocchi (come le linee di corrente di oggi).
- Nodo selezionato: raggio, collegamenti usati/massimi; clic su un blocco nel raggio = collega/scollega.
- Suggerimento su un blocco di corrente: produzione, consumo, percentuale, batterie.
- Batteria: sprite da `tools/art/genera.py` con la carica visibile; testi nuovi in `testi.csv`.

## Test

- Catena di 5 nodi T1 da un generatore a un forno a 30 celle: il forno riceve il 100%.
- Contatto: generatore attaccato a un forno, senza nodi.
- Collegamenti automatici: ordine dal più vicino, limite dei collegamenti, laser fra nodi contato su entrambi.
- Toggle a mano e i tre rifiuti; smontaggio che toglie i laser e spezza la rete.
- Bilancio: 1 generatore e 3 forni = 100% (30 ≥ 24); 1 generatore e 5 forni = 75%; batterie che si caricano col surplus e coprono il buco; due reti separate non si aiutano.
- Effetti: velocità dell'assemblatore a 50%, forno che passa al carbone sotto `1,2 × s < 1`, lampada a metà luce.
- Salvataggio v17 avanti e indietro con laser e carica; v16 rifiutato.
- Determinismo con reti che si dividono e si uniscono.

## Fuori ambito

Diodi, interruttori e altri blocchi di corrente di Mindustry; generatori nuovi (solare, a vapore); linee di corrente aeree oltre i laser dei nodi.
