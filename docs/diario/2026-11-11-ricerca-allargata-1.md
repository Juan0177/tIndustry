# Ricerca allargata, piano 1: oggetti, ricette e macchine

- **Specifica**: [2026-11-09-ricerca-allargata-design](../superpowers/specs/2026-11-09-ricerca-allargata-design.md)
- **Piano**: [2026-11-11-ricerca-allargata-1](../superpowers/plans/2026-11-11-ricerca-allargata-1.md)
- **Periodo**: 2026-11-11
- **Esito**: tutto sul ramo `claude/loving-planck-vjf9sv`, senza PR al momento della scrittura. Simulazione, dati, salvataggi, agenti e test verdi; il client è compilato ma non provato a schermo (manca Godot in cloud, vedi «Per il PC di casa»).

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-11-11-ricerca-allargata-1/progress.md`) e dai rapporti dei task. Dove il registro non dice qualcosa qui non è inventato.

## Obiettivo

Il primo dei tre piani della Ricerca allargata: quattro oggetti nuovi (sabbia, vetro, plastica, circuito), la sabbia scavata dal pavimento, cinque blocchi nuovi (fonderia avanzata, pressa grande, separatore, plastificatore, trapano a impatto 3×3) e la ricetta dei circuiti come nodo della Ricerca senza blocco; costruibili, sbloccabili, salvati, giocabili dagli agenti e disegnati nel client. Logistica ed energia sono il piano 2; potenziamenti, premi e bilanciamento il piano 3.

## Task e commit

Base del piano: `bae4198`.

| Task | Contenuto | Commit |
|---|---|---|
| 1 | Oggetti `sand`, `glass`, `plastic`, `circuit` nel Mercato; ricette `smelt-glass`, `press-graphite`, `separate-sand` (`randomOutput`), `plasticize-plastic`; profilo delle macchine nei dati (`recipes`, `powerMode`, `craftSpeed`) con `FactoryContent.RecipesFor` e `Crafter.Create`; sprite e testi degli oggetti | `9ded889` |
| 2 | I minatori senza giacimento scavabile scavano sabbia dal pavimento `Sand`/`DarkSand` (`TerrainMap.CountSandTiles`) | `afd4a08` |
| 3 | Fonderia avanzata, pressa grande, separatore, plastificatore (`GameState.Machines`), caso del separatore con SplitMix64, ricetta `craft-circuit` col nodo `circuit` di tipo `recipe` (`Factory.RefreshAvailableRecipes`), pressa fuori dalla rete di corrente, nodi e costi della Ricerca, simboli degli agenti, sprite e testi | `45da7b5` |
| 4 | Trapano a impatto 3×3: misura del minatore per istanza (`Miner.SizeFor`), tier 3, velocità 8 per efficienza per soddisfazione, fermo senza corrente; nodo della Ricerca, simbolo `T`, sprite | `50c8f49` |
| 5 | Taratura dei minuti dei nodi: niente da fare, già fatta dai Task 3 e 4 (vedi le decisioni) | nessun commit |
| 6 | Salvataggi v21 (`Machines`, `CoveredDepositTiles`), diario degli agenti alla versione 7, descrizioni di `piazza`, `ricetta` e `stato` | `0d6fe77` |
| 7 | Client: palette Produzione coi cinque blocchi, `MachineVisual` con le strisce `-anim` delle quattro macchine, luce notturna della fonderia avanzata, fantasmi 2×2 e 3×3, righe I/O del pannello del blocco (probabilità del separatore, tempi della fonderia avanzata) | `99ad38e` |
| 8 | Documenti, stato e diario; testi di trapano, minatori e assemblatore | commit `docs: Ricerca allargata, piano 1 fatto` |

Fra il Task 6 e il Task 7 sul ramo c'è anche `09f1e93` (`docs: piano 2 della Ricerca allargata`), che non fa parte di questo piano.

## Giri di revisione

- **Prima dei task**: un controllo incrociato del piano ha trovato i punti in cui un task avrebbe rotto i test fino a uno successivo (la ricetta dei circuiti senza il suo cancello fra il Task 1 e il 3; sprite, nodi della Ricerca e simboli degli agenti dei blocchi nuovi che arrivavano solo nei Task 5–7). Le decisioni qui sotto spostano quei pezzi nel task che introduce il blocco, così ogni commit resta verde.
- **Task 1, 2, 3, 4, 6, 7**: una revisione ciascuno, senza rilievi da correggere; solo minori, rinviati (sotto).
- **Task 2**: il revisore non ha lanciato `NoSandOnClassicGenerator`; risolto con la suite completa dell'implementatore (1235 superati), che lo comprende.
- **Task 6**: il revisore non ha letto le prove di andata e ritorno del separatore e del circuito; risolto con la suite completa dell'implementatore (1284 superati), che le comprende.
- **Task 7**: tre lacune nei testi (minatori che non parlano della sabbia, assemblatore che non parla dei circuiti, «4 pezzi al secondo» senza condizione) passate al Task 8, che le ha corrette.
- **Task 8**: la sua revisione si fonde nella revisione finale (sotto).

## Decisioni prese in autonomia

Decisione: la ricetta `craft-circuit` arriva nel Task 3 insieme al suo cancello della Ricerca, non nel Task 1 — così il Task 1 non cambia il comportamento e nessun assemblatore fa circuiti senza sblocco fra i due task — costo se sbagliata: nessuno.
Decisione: ogni task aggiunge gli sprite (`genera.py`, PNG rigenerati, `CELLE` di `test_genera.py`) e le chiavi di `testi.csv` di quello che introduce (oggetti nel Task 1, le quattro macchine nel Task 3, il trapano nel Task 4); il Task 7 tiene il codice del client e le animazioni — così ogni commit resta verde — costo se sbagliata: un Task 7 più piccolo del previsto.
Decisione: i Task 3 e 4 aggiungono i loro nodi a `NODES` di `genera_finanza.py` con minuti provvisori, lanciano `--scrivi-costi`, aggiungono i genitori in `ResearchTreeContentTests` e allargano l'intervallo dei soldi di `ResearchCostTests` solo se serve; il Task 5 avrebbe poi tarato i minuti — così ogni commit resta verde — costo se sbagliata: nessuno.
Decisione: i Task 3 e 4 aggiungono i simboli della legenda degli agenti, `KindSymbol` e `Buildings()` per i loro blocchi; il Task 6 fa salvataggi, prove di `piazza` e documenti degli strumenti — perché senza simbolo `EveryBuildingKindHasALegendSymbol` fallisce — costo se sbagliata: nessuno.
Decisione: la velocità del trapano resta 8 (4 volte la velocità base 2 del Minatore T2, senza il suo +20% con la corrente) — la specifica dice «4 volte il Minatore T2» e la formula del piano fissa 8 — costo se sbagliata: il trapano è circa il 17% più lento di 4 Minatori T2 alimentati, una costante da cambiare.
Decisione: il Task 5 non si fa partire — i Task 3 e 4 avevano già messo tutti e sei i nodi, i costi con `--scrivi-costi`, i genitori dell'albero e l'intervallo dei soldi a 22–25,5 milioni; il controllore ha verificato `--controlla` OK, `test_genera_finanza` OK, `MapTool valida` OK, 6,38 ore senza jackpot (≤ 6,4) e 25.395.700 $ — costo se sbagliata: nessuno, il piano 3 riequilibra comunque.
Decisione (Task 3): minuti della pressa grande 3,5 (non 3) e del separatore 6,5 (non 6) — `test_costs_increase_along_every_prerequisite_chain` vuole un figlio che costi strettamente più del genitore (con 3 minuti la pressa costava quanto l'assemblatore, con 6 il separatore quanto la fonderia avanzata); il ritocco più piccolo che passa — costo se sbagliata: due nodi un po' più cari.
Decisione (Task 3): la pressa grande (`powerMode` `none`) non è un blocco di corrente: nessuna rete, nessun laser, nessun contatto — il Task 1 aveva segnalato che `PowerGrid` avrebbe contato ogni macchina come consumatore — costo se sbagliata: nessuno.
Decisione (Task 3): il nodo `circuit` ha lo stesso id dell'oggetto `circuit` e ne usa l'icona — chiavi di testo separate (`struttura.circuit.*`, `oggetto.circuit.nome`) — costo se sbagliata: uno strumento che desse per scontati spazi di id separati li confonderebbe.
Decisione (Task 4): l'intervallo dei soldi della Ricerca passa a 22–25,5 milioni nei test C# e Python — con il trapano il totale è 25.395.700 $ — costo se sbagliata: un tetto più alto di mezzo milione.
Decisione (Task 6): diario degli agenti alla versione 7 — come per le v18–v20, i minatori su sabbia e i blocchi nuovi farebbero rigiocare diversamente un diario v6 — costo se sbagliata: i diari vecchi degli agenti non si rigiocano più.
Decisione (Task 6): una macchina salvata con una definizione che non è una delle quattro nuove rende il file illeggibile, le celle coperte si limitano all'impronta e senza campo valgono l'impronta intera — meglio un errore che un forno finito nella lista sbagliata — costo se sbagliata: un salvataggio rifiutato invece di riparato.
Decisione (Task 7): `MachineVisual.cs.uid` scritto a mano (`uid://dm4qk7xa2wrvb`, solo l'alfabeto dei uid di Godot, unico nel repository), come per `JackpotSequenceView` — in cloud Godot non c'è — costo se sbagliata: Godot lo rigenera al primo import.
Decisione (Task 7): le strisce `-anim` delle macchine si vedono solo mentre la macchina lavora e, se mancano, non si disegna niente al loro posto (niente segnaposto magenta) — costo se sbagliata: una macchina senza animazione passa inosservata.
Decisione (Task 8): i suggerimenti del trapano dicono «fino a 4 pezzi al secondo» e la descrizione «a piena efficienza»; minatori e trapano dicono che senza giacimento si scava sabbia dal pavimento di sabbia; l'assemblatore dice che fa i circuiti dopo la Ricerca — costo se sbagliata: suggerimenti un po' più lunghi, da guardare a schermo.

## Problemi rinviati

- Task 1: `powerMode` letto con casi scritti a mano (può staccarsi dall'enum); `Crafter.Create` ripiega in silenzio sui valori di prima se il blocco manca dai contenuti; `PowerSystem` cerca il blocco in modo lineare per ogni macchina a ogni tick; il foglio `docs/finanza/mercato-e-finanza.xlsx` non rigenerato (ignorato da git, senza test).
- Task 2: `MinerOnSandMinesSand` non fa girare i tick per controllare il ritmo; `MinerTooWeak` scorre l'impronta due volte; la costante `SandItemId` a metà della classe.
- Task 3: `ResearchState.ForceUnlock`/`ForceLock` non chiamano `RefreshAvailableRecipes` (li usano i test e le sonde dev del client); `IsValidSpec`/`FindRecipeFor` accettano in un piano una ricetta ancora chiusa dalla Ricerca e la macchina ripiega in silenzio sulla prima disponibile; il ripiego `smelt-iron` di `PlaceCrafter` se tutte le ricette fossero chiuse (oggi irraggiungibile); `graphite-press` in italiano «Pressa grande» e in inglese "Graphite press".
- Task 4: l'arresto del minatore senza corrente confronta un float (equivalente all'intero `PowerBp`); la chiave `footprint` dei contenuti è ignorata dal caricamento (solo documentazione).
- Task 6: ripiego finale `smelt-iron` in `RestoreCraft`; la ricerca dell'impronta in `PiazzaPlacesEachNewBlock` è fragile; una modifica ridondante in `MinerTierTests`.
- Task 7: i PNG nuovi non hanno il loro `.import` (li crea Godot a casa, da committare dopo il primo import); `ToolKeysNameTheNewBlocks` sta in `ImpactDrillTests.cs`; il Minatore T2 non ha ancora la riga I/O nel pannello del blocco (solo il T1 e il trapano).

## Per il PC di casa

- Importare il progetto (`--import`) per i `.import` dei 23 PNG nuovi: `sand`, `glass`, `plastic`, `circuit` e i loro `-belt`; `smelter-advanced`, `graphite-press`, `separator`, `plasticizer` con `-base` e `-anim`; `impact-drill`, `impact-drill-base`, `impact-drill-drill-anim`. Controllare che Godot accetti `MachineVisual.cs.uid` (`uid://dm4qk7xa2wrvb`, scritto a mano). Poi `--build-solutions`, ripristino dei `.import` che cambiano solo per fine riga e commit dei `.import` nuovi.
- `python tools/art/genera.py --controlla` byte per byte: in cloud vale solo `--pixel` (Pillow diverso). Se segnala i PNG di questo piano, rigenerarli con `python tools/art/genera.py --solo` seguito dai nomi.
- Playtest `ui`, `tutorial` e `agenti` (PLAYTEST OK, 0 ERROR). Le catture di prima non dovrebbero cambiare: la categoria Produzione passa a 10 blocchi ma resta di 3 righe, come Logistica.
- A mano:
  1. i cinque blocchi nella palette Produzione: bloccati finché non si sbloccano, suggerimenti e dettagli (il trapano con i minerali che scava), `Q` sui blocchi piazzati;
  2. i blocchi nel mondo: fantasmi 2×2 e 3×3, il trapano parzialmente fuori mappa o su un nastro rifiutato, nessuna direzione per le macchine;
  3. le animazioni solo mentre lavorano: fiamme nelle due bocche della fonderia avanzata (con fumo e luce di notte), pistone della pressa, tamburo del separatore, bolle del plastificatore, punta del trapano; scala, fase e bagliore;
  4. le righe I/O del pannello del blocco e della Ricerca: separatore con le percentuali dei minerali, fonderia avanzata coi tempi dimezzati, Ricetta dei circuiti con la sua ricetta;
  5. il separatore che dà un minerale alla volta; un minatore e un trapano su sabbia; l'assemblatore che fa circuiti solo dopo lo sblocco;
  6. la grafica degli edifici rifatta quando una macchina si costruisce o si smonta;
  7. i suggerimenti più lunghi di minatori, trapano e assemblatore alla Scala UI al 100% e al 150%, in italiano e in inglese.

## Verifiche dei task

Ultimo stato riportato dai task: build Release 0 errori e 0 avvisi (progetto Godot compreso); `TIndustry.Shared.Tests` 1285 superati e 2 saltati al Task 7; `TIndustry.AgentPlay.Tests` 29 superati e 10 saltati (le prove del gioco vero, senza Godot); `test_genera.py` 51 OK; `genera.py --controlla --pixel` 290 aggiornati, 0 da rigenerare; `test_genera_finanza.py` OK e `genera_finanza.py --controlla data/content.json` uguale al foglio; `MapTool valida` OK. Task 8: build 0 errori e 0 avvisi, `TIndustry.Shared.Tests` 1285 superati e 2 saltati (`TestiTests` verdi coi testi nuovi), test del sito 71 OK e `node --test` 25 superati. Nessun playtest (manca Godot in cloud).

## Revisione finale

Revisione finale: in corso.
