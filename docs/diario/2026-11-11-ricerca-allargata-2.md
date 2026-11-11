# Ricerca allargata, piano 2: logistica ed energia

- **Specifica**: [2026-11-09-ricerca-allargata-design](../superpowers/specs/2026-11-09-ricerca-allargata-design.md)
- **Piano**: [2026-11-11-ricerca-allargata-2](../superpowers/plans/2026-11-11-ricerca-allargata-2.md)
- **Periodo**: 2026-11-11
- **Esito**: tutto sul ramo `claude/loving-planck-vjf9sv`, senza PR al momento della scrittura. Simulazione, dati, salvataggi, agenti e test verdi; il client è compilato ma non provato a schermo (manca Godot in cloud, vedi «Per il PC di casa»).

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-11-11-ricerca-allargata-2/progress.md`) e dai rapporti dei task. Dove il registro non dice qualcosa qui non è inventato.

## Obiettivo

Il secondo dei tre piani della Ricerca allargata: otto blocchi nuovi, costruibili, sbloccabili in Ricerca, salvati, giocabili dagli agenti e disegnati nel client. Logistica: overflow e underflow gate, nastro a pacchi, ponte lungo con corrente. Energia: pannello solare piccolo e grande, generatore a combustione, batteria grande. Potenziamenti, premi e bilanciamento sono il piano 3.

## Task e commit

Base del piano: `ca20ccf`.

| Task | Contenuto | Commit |
|---|---|---|
| 1 | Overflow e underflow gate: `LogisticsKind.Gate`, passaggio in `BeltGrid.TryInsert` come il selezionatore (`PassGate`, lati con `PassAside`), alternanza in `SorterTurns` (nessun cambio del formato), costo 4 lastre di piombo + 2 fili di rame; nodi della Ricerca dopo il router, simboli `O` e `U`, sprite e testi | `73f6794` |
| 2 | Nastro a pacchi: `LogisticsKind.Stack`, `TransportedItem.Count`, cella di carico e di scarico ricalcolate a ogni cambio della griglia, pacco che parte a 10 pezzi o dopo 60 tick, scarico solo in edifici e CORE; posato con `PlaceBelt`; salvataggi v22 (`Count`, `StackWaitTicks`); diario degli agenti alla versione 8, simboli `8 6 2 4`; Nastro T3 accorciato da 25 a 22 minuti | `58cc32e` |
| 3 | Ponte lungo: `ConveyorDefinition.MaxSpan` (`maxSpan`, 4 per il ponte di prima, 8 per il lungo) e `PowerUse` (`powerUse`), entità `BridgePower` consumatrice della rete, oggetti fermi senza corrente; salvataggio di `PowerId` e `PowerSatisfactionBp`; simbolo `Z` | `4032679` |
| 4 | Pannelli solari 1×1 (3/s) e 3×3 (36/s): `SolarPanel`, produzione `powerOutput × luce / 1000` in interi calcolata in `PowerSystem`, `GameState.SolarPanels` salvati; simboli `D` e `J` | `0c0613a` |
| 5 | Generatore a combustione (90/s, carbone 2 s, grafite 4 s, prima il carbone, scorta di 8) e batteria grande (3×3, 20.000): combustibili per definizione (`fuels`; il Generatore diventa `fuels: [{coal, 8}]`), produzione e capacità per definizione, `DefinitionId` e `Fuels` nei salvataggi; simboli `%` e `&`; Deposito accorciato da 24 a 23 minuti. Prima, in un commit a sé, la correzione del test del sito rotto dal Task 2 | `e65032b`, `13413e5` |
| 6 | Salvataggi, agenti e determinismo: scenario con tutti gli otto blocchi (`PlanTwoScenario`), andata e ritorno al tick 808 con la stessa impronta dopo 600 tick, v21 rifiutata, `piazza` degli otto blocchi con i simboli in `area`, determinismo per 10 minuti fra giorno e notte; commento di `BridgePower` corretto | `619c41e` |
| 7 | Client: segni del pacco (`SyncPackMark`, da 0 a 3 copie più scure), conto dell'HUD in pezzi, `SolarVisual` (pannello che si scurisce con la produzione), fuoco del generatore a combustione (`combustion-generator-fire-anim`, solo mentre brucia) e la sua luce di notte, righe I/O del pannello del blocco e della Ricerca prese dai dati (`BlockIoLine`), chiave `belt-stack` mancante in `Factory.StructureIdForTool` | `db06c84` |
| 8 | Documenti, stato e diario | commit `docs: Ricerca allargata, piano 2 fatto` |

Fra il Task 5 e il Task 6 sul ramo c'è anche `0afd5a3` (`docs: piano 3 della Ricerca allargata`), che non fa parte di questo piano.

## Giri di revisione

- **Prima dei task**: un controllo incrociato del piano non ha trovato conflitti fra i task (ordine dei valori nuovi di `LogisticsKind`, versione 22 dei salvataggi, client che deve compilare dopo ogni blocco nuovo, `PowerGrid` toccato in fila dai Task 3, 4 e 5); tre decisioni lo hanno fissato prima di partire (sotto).
- **Task 1, 2, 3, 4, 5, 6, 7**: una revisione ciascuno, senza rilievi da correggere; solo minori, rinviati (sotto).
- **Task 4**: l'implementatore ha trovato rotto un test del sito (`test_costruisci.test_nastri_un_fotogramma_per_pixel`), già dal Task 2: la pagina degli asset non aveva il nastro a pacchi fra i nastri. Corretto nel Task 5 (`e65032b`, `tools/sito/composti.py`: il nastro a pacchi come nastro solo dritto, come nel client).
- **Task 6**: la revisione (solo test) è andata in parallelo con l'implementatore del Task 7, su file diversi.
- **Riporti fra task**: dal Task 2 al Task 7 il conto degli oggetti sui nastri dell'HUD, che contava un pacco come 1 (fatto); dal Task 3 al Task 6 il commento di `BridgePower` che diceva la quota non salvata (fatto); dal Task 5 al Task 8 il paragrafo dei totali in Economia e Ricerca (fatto) e le schede del sito per i blocchi nuovi (non fatte, vedi le decisioni).
- **Task 8**: la sua revisione si fonde nella revisione finale (sotto).

## Decisioni prese in autonomia

Decisione: il nastro a pacchi si piazza come un nastro (`PlaceBelt` col suo id, trascinabile in linea come il Nastro T3), non come un `BuildingKind` nuovo — è così che si posa ogni nastro con direzione e `PlacementLine` continua a funzionare — costo se sbagliata: agenti e palette avrebbero bisogno di un'altra strada, un cambio piccolo.
Decisione: i gate tengono i loro bit di alternanza in `BeltSaveDto.SorterTurns`, quindi il Task 1 non cambia il formato e il primo a portare i salvataggi alla v22 è il Task 2 — costo se sbagliata: nessuno.
Decisione: ogni task tiene il client compilato con i casi minimi per i suoi blocchi; il Task 7 fa la grafica vera — costo se sbagliata: nessuno.
Decisione (dopo il Task 2): per il resto del piano, quando un nodo nuovo porta le ore senza jackpot sopra 6,5, il task può accorciare i minuti di nodi esistenti della stessa fase, tenendo i costi crescenti lungo le catene — la specifica dice «si accorciano i minuti dei nodi, non la regola» e il piano 3 riequilibra tutto — costo se sbagliata: qualche costo esistente si sposta un poco, sempre rigenerato dallo script.
Decisione (Task 2): il Nastro T3 passa da 25 a 22 minuti ($1.500.000 → $1.300.000) — il nastro a pacchi (fase Fine) deve costare più del suo genitore, e con il Nastro T3 a 1,5 milioni le ore sarebbero salite a 6,54 — costo se sbagliata: il Nastro T3 costa 200.000 $ in meno.
Decisione (Task 2): diario degli agenti alla versione 8 — come per ogni salto dei salvataggi, un diario può portare un'istantanea di partenza che la v22 non legge più — costo se sbagliata: i diari vecchi degli agenti non si rigiocano più.
Decisione (Task 2, 3, 4): l'intervallo dei soldi della Ricerca nei test C# e Python sale a 26,7, poi 26,72, poi 26,74 milioni, sempre di quanto basta — costo se sbagliata: un tetto più alto di 1,24 milioni rispetto al piano 1.
Decisione (Task 3): la quota di corrente del ponte lungo **si salva** (`BeltSaveDto.PowerSatisfactionBp`), contro il brief che diceva di no — in un tick i nastri si muovono prima del bilancio della corrente, quindi senza quel campo una partita caricata (o una copia dell'istantanea, che usa il co-op) trattiene gli oggetti al primo tick mentre l'originale li fa passare, e le impronte divergono (provato rompendo il ripristino) — costo se sbagliata: un campo facoltativo in più.
Decisione (Task 4): simboli degli agenti `D` e `J` per i pannelli solari, le maiuscole libere rimaste (non mnemoniche) — costo se sbagliata: nessuno, ma restano poche lettere (liberi cifre dispari, 0 e punteggiatura).
Decisione (Task 5): il Deposito passa da 24 a 23 minuti ($1.500.000 → $1.400.000), stessa fase della batteria grande — con la batteria grande le ore sarebbero passate di poco 6,5 — costo se sbagliata: il Deposito costa 100.000 $ in meno.
Decisione (Task 5): il test del sito rotto dal Task 2 si corregge dentro il Task 5, in un commit a sé — blocca il ramo, costa poco, non serve un task — costo se sbagliata: nessuno.
Decisione (Task 5): i combustibili si salvano solo quando un generatore tiene un combustibile diverso dal primo della sua lista; quale combustibile sta bruciando non si salva — non cambia né la produzione né il determinismo (`BurnRemaining` è salvato e la produzione dipende solo dalla definizione) — costo se sbagliata: nessuno.
Decisione (Task 6): la revisione del Task 6 (solo test) va in parallelo con l'implementatore del Task 7 — revisore in sola lettura, file diversi — costo se sbagliata: una correzione dei test dopo il Task 7.
Decisione (Task 6): il ponte lungo degli agenti (`piazza` con `tipo: bridge-long`) trova l'uscita da solo fino a 8 celle, senza un parametro per sceglierla — come il ponte di prima — costo se sbagliata: un parametro da aggiungere.
Decisione (Task 7): `SolarVisual.cs.uid` scritto a mano (`uid://cq3n8vwe5ty2d`, solo l'alfabeto dei uid di Godot, unico nel repository) — in cloud Godot non c'è — costo se sbagliata: Godot lo rigenera al primo import.
Decisione (Task 7): anche Generatore e Batteria, che non sono blocchi nuovi, hanno ora le righe I/O prese dai dati (combustibili, produzione, capacità) nel pannello del blocco e in Ricerca, e la chiave `blocco.io.combustione` sparisce — per coerenza con i blocchi nuovi — costo se sbagliata: due catture dei playtest da rifare.
Decisione (Task 7 e 8): nessuna scheda composta nel sito per i blocchi nuovi (`tools/sito/composti.py`) — le strisce nuove compaiono da sole fra le schede della loro famiglia, come per le macchine del piano 1, e i test del sito restano verdi — costo se sbagliata: una scheda da aggiungere.
Decisione (Task 8): la wiki dice le due regole che i Task 2 avevano lasciato per i documenti, smontare una cella a pacchi perde il suo pacco (come un nastro, ma fino a 10 pezzi) e due celle a pacchi testa a testa si fermano — costo se sbagliata: nessuno.

## Problemi rinviati

- Task 1: il messaggio d'errore del ripristino «Turni del selezionatore» vale anche per i gate; gli aiuti con il nome del selezionatore (`SorterTurns`, `FlipSorter`, `RestoreSorter`, `SorterHandsTo`) servono anche i gate; `AnOverflowLineFeedsTheSidesOnlyWhenTheFrontBacksUp` dipende dalla spaziatura dei nastri; i gate non hanno un tasto rapido.
- Task 2: due celle a pacchi testa a testa si fermano come due nastri (scritto nella wiki); `BeltGrid.Version` sale di due quando si piazza o si smonta un ponte.
- Task 3: un salvataggio rovinato che perde un ponte lungo coi suoi laser diventa illeggibile invece di perdere il ponte in silenzio; `BridgePowerAt` cerca in modo lineare; `TryGetSpecAt` dà l'id della corrente per entrambe le estremità del ponte lungo; nessuna grafica per «ponte lungo senza corrente».
- Task 4: `UnlockAllForDemo` (`ResearchState.DemoStructureIds`) non comprende i blocchi nuovi.
- Task 5: il bruciare resta in float, quindi un carbone da 2 s dura 121 tick (lo stesso difetto del generatore di prima; il piano 3 può passare a tick interi); `RestoreFuels` taglia o scarta in silenzio; nessun controllo che `FuelBuffer` e `Fuels` dicano la stessa cosa; `Generator.Size` è una costante comune ai due generatori.
- Task 6 (per le correzioni finali del piano): il commento del test di determinismo dice che il solare ha lavorato di giorno senza controllarlo; il tick 808 dell'andata e ritorno ha bisogno di un commento su come è stato trovato; il test della v21 rifiutata ripete quello della v20.
- Task 7 (per le correzioni finali del piano): `World.Visuals.cs` somma i pezzi sui nastri con un enumeratore a ogni frame (meglio un ciclo `for`); `SolarVisual` resta scuro dopo un caricamento in pausa fino al primo tick; `SyncPackMark` si basa sul numero dei figli del nodo.
- I PNG nuovi non hanno il loro `.import` (li crea Godot a casa, da committare dopo il primo import).

## Per il PC di casa

- Importare il progetto (`--import`) per i `.import` dei 15 PNG nuovi: `overflow-gate`, `underflow-gate`, `conveyor-stack`, `conveyor-stack-anim`, `bridge-long`, `bridge-long-span`, `solar`, `solar-large`, `combustion-generator` con `-base`, `-rotor-anim` e `-fire-anim`, `battery-large` con `-base` e `-charge-anim`. Controllare che Godot accetti `SolarVisual.cs.uid` (`uid://cq3n8vwe5ty2d`, scritto a mano). Poi `--build-solutions`, ripristino dei `.import` che cambiano solo per fine riga e commit dei `.import` nuovi.
- `python tools/art/genera.py --controlla` byte per byte: in cloud vale solo `--pixel` (Pillow diverso). Se segnala i PNG di questo piano, rigenerarli con `python tools/art/genera.py --solo` seguito dai nomi.
- Playtest `ui`, `tutorial` e `agenti` (PLAYTEST OK, 0 ERROR). Le catture cambieranno: la palette ha otto blocchi in più in Logistica e Potenza e l'icona nuova del generatore a combustione (col focolare); la Ricerca e il pannello del blocco hanno righe I/O nuove, anche per Generatore e Batteria. Confrontarle con `compare_shots.py` e rifare i riferimenti.
- A mano:
  1. i due gate: dritto e ai lati alternando, l'oggetto che aspetta sul nastro quando nessuno accetta, il gate accanto al CORE o a un forno, il cappuccio ambra (overflow) e rosso (underflow), nessuna rotazione con `R`, trascinamento in fila;
  2. il nastro a pacchi: carico solo sulla prima cella, i segni del pacco (da 0 a 3 copie più scure dietro l'oggetto: che stiano dietro e si leggano allo zoom solito), lo scarico solo in un edificio o nel CORE, la linea ferma davanti a un nastro, il conto dei pezzi nell'HUD;
  3. il ponte lungo: il fantasma fino a 8 celle, i laser dei nodi su una delle due estremità, gli oggetti fermi senza corrente;
  4. i pannelli solari che si scuriscono al tramonto e sono spenti di notte (il loro scuro si somma al buio della notte: guardare il tono), con «Solo giorno» e «Solo notte»;
  5. il generatore a combustione: il fuoco solo mentre brucia (8 fotogrammi al secondo, col Bagliore), la luce arancione di notte, carbone e grafite dallo stesso nastro;
  6. la batteria grande 3×3 con la sua barra di carica, accanto a una piccola;
  7. le righe I/O nuove del pannello del blocco e della Ricerca in italiano e in inglese, alla Scala UI al 100% e al 150%.

## Verifiche dei task

Ultimo stato riportato dai task: build Release 0 errori e 0 avvisi (progetto Godot compreso); `TIndustry.Shared.Tests` 1389 superati e 2 saltati al Task 7; `TIndustry.AgentPlay.Tests` 29 superati e 10 saltati (le prove del gioco vero, senza Godot); `test_genera.py` 51 OK; `genera.py --controlla --pixel` 305 aggiornati, 0 da rigenerare; `test_genera_finanza.py` 15 OK e `genera_finanza.py --controlla data/content.json` uguale al foglio; test del sito 71 OK e `node --test` 25 superati dal Task 5. Ore senza jackpot 6,4956 (6,03 con uno, 5,69 con due), Ricerca 26.737.500 $. Task 8: build 0 errori e 0 avvisi, `TIndustry.Shared.Tests` 1389 superati e 2 saltati (`TestiTests` compresi), test del sito 71 OK e `node --test` 25 superati, `controlla_wiki.py` OK, mappa del progetto e barre rigenerate (avanzamento 71%, 61 passi su 86). Nessun playtest (manca Godot in cloud).

## Revisione finale

Revisione finale dell'intero ramo: in corso.
