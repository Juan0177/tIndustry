# Il drone sei tu: comandi come Mindustry

- **Stato**: approvata dall'utente il 3 novembre 2026. Aspetto 11 «Drone e tier» di `docs/STATO.md` (CORE e droni a livelli ci sono già: [magazzino del CORE](2026-10-23-magazzino-core-design.md)).
- **Sostituisce**: il drone che vola da solo verso i cantieri (`BuilderSystem`), la visuale mossa con WASD, gli strumenti della palette (`BuildTool`: cursore, martello, fabbrica) e il trascinamento col clic sinistro che sposta la visuale.

## Obiettivo

Si gioca come in Mindustry su PC: il giocatore è il suo drone. Lo muove con WASD, la visuale lo segue, il drone costruisce e smonta solo entro il suo raggio, scava a mano i giacimenti e porta i pezzi al CORE. Mouse e tastiera non hanno più modalità: il clic seleziona o configura, il blocco scelto in palette si piazza, il clic destro smonta.

Successo: una partita nuova si gioca dall'inizio alla fine senza strumenti; i piani fuori raggio aspettano finché non ci si avvicina; lo scavo a mano porta minerali nel CORE; la catena di nodi, il filtro di selezionatori ed estrattori e i laser si configurano col clic.

## Decisioni prese con l'utente

| Tema | Decisione |
|---|---|
| Portata | **Come Mindustry, senza combattimento**: movimento, costruzione nel raggio, selezione e configurazione col clic, scavo a mano. Il drone non può essere distrutto (arriverà con l'aspetto 13). |
| Visuale | **Segue il drone**; tenendo premuto il tasto centrale si trascina lontano e al rilascio torna sul drone; rotella zoom, M mappa grande. |
| Scavo | alpha: ferro, rame, piombo, carbone, **0,7/s**, stiva **30**; beta: stessi, **0,9/s**, stiva **50**; gamma: anche titanio, **1,1/s**, stiva **70**. Scarico automatico entro **6 celle** dal CORE, rispettando il massimo per oggetto. |
| Costruzione | **Come Mindustry**: il drone non si muove da solo; costruisce e smonta solo i piani entro il raggio (27,5 celle); gli altri aspettano. |

## Regole

### Movimento

- Il drone ha una direzione d'ingresso (−1, 0, 1 per asse) impostata da un comando e tenuta finché non cambia; a ogni tick si muove alla velocità del suo livello (alpha 22,5, beta 24,75, gamma 26,6 celle/s), con la diagonale normalizzata. Resta dentro la mappa; vola sopra tutto (pareti, acqua, edifici).
- Nessun movimento automatico verso i cantieri.

### Costruzione

- La coda dei piani resta per giocatore come oggi. A ogni tick il drone lavora sul primo piano della coda **entro il raggio** (distanza dal centro del drone al cantiere ≤ 27,5 celle); i piani fuori raggio restano in coda e si vedono semitrasparenti, con un contorno che dice «fuori raggio».
- Ordine: il primo in coda fra quelli nel raggio; un piano bloccato per materiali va in fondo come oggi.

### Scavo a mano

- Comando «scava questa cella» (o «smetti»). Vale se la cella ha un giacimento del livello del drone, è entro il raggio e la stiva ha posto per quell'oggetto. Un solo tipo di oggetto alla volta nella stiva; cambiare oggetto con la stiva piena non scava.
- Ritmo per livello come in tabella, con accumulatore intero (nessun float nei conti della stiva).
- A ogni tick, se il drone è entro 6 celle dal CORE, la stiva passa nel CORE fino al massimo per oggetto; quello che non entra resta nella stiva.
- Il giacimento non si consuma (come i minatori).
- Le 6 celle dello scarico si misurano dal centro del drone al bordo più vicino del CORE (0 sopra il CORE).
- Come Mindustry: lo scavo si annulla se la cella esce dal raggio o se sul giacimento c'è il CORE, un edificio, un nastro o un cantiere; mentre il drone costruisce lo scavo si ferma e poi riprende; con la stiva piena il comando si rifiuta, salvo che il drone sia entro 6 celle da un CORE con posto per quell'oggetto.

### Mouse e tastiera

| Azione | Effetto |
|---|---|
| WASD | muove il drone |
| tasto centrale tenuto | trascina la visuale; al rilascio torna sul drone |
| rotella | zoom |
| clic sinistro, nessun blocco scelto | seleziona il blocco sotto il cursore: informazioni e, se ha configurazione, la apre (filtro di selezionatori ed estrattori, laser dei nodi) |
| clic sinistro tenuto su un giacimento, nessun blocco scelto | scava |
| blocco scelto in palette | clic o trascinamento piazza i piani; R ruota |
| clic destro o Esc con un blocco scelto | deseleziona il blocco |
| clic destro trascinato | rettangolo di smontaggio |
| clic destro su un piano | lo annulla |
| Q | sceglie in palette il blocco sotto il cursore |
| Spazio | pausa |
| M | mappa grande |

La palette tiene categorie e blocchi; spariscono i pulsanti degli strumenti.

## Simulazione

- Comandi nuovi: `SetDroneInput(PlayerId, int Dx, int Dy)`, `SetMining(PlayerId, GridPosition? Tile)`; rifiuti per scavo fuori raggio, giacimento non scavabile dal livello, stiva piena.
- `BuilderUnit`: direzione d'ingresso, stiva (oggetto, quantità), cella di scavo, accumulatore. `BuilderSystem`: movimento dall'ingresso, nessun inseguimento dei cantieri; `ConstructSystem`: solo cantieri nel raggio; sistema nuovo per scavo e scarico.
- `IGameView`: stato del drone (posizione, stiva, scavo, piani fuori raggio).
- Agenti: comandi `muovi` (dx, dy o una cella da raggiungere, risolta dal server con più comandi) e `scava`; stato del drone in `stato`.
- Salvataggi: versione nuova (dopo la v18 della logistica) con ingresso, stiva, scavo e accumulatore.
- Determinismo: tutto in tick, interi per la stiva, posizione come oggi.

## Client

- Input: WASD al drone, tasto centrale per trascinare la visuale, niente strumenti; clic sinistro seleziona, configura o scava; Q sceglie il blocco.
- Visuale che segue il drone con un leggero inseguimento; ritorno sul drone al rilascio del tasto centrale.
- Piani fuori raggio semitrasparenti; raggio del drone visibile mentre si piazza.
- Raggio di scavo (linea dal drone al giacimento come in Mindustry), stiva nell'HUD.
- Pannelli di configurazione aperti dal clic: filtro (dalla specifica della logistica) e laser dei nodi (già fatti col cursore).
- Testi in `testi.csv`; Wiki Comandi riscritta; playtest e agenti adattati (il drone si guida con i comandi).

## Test

- Movimento: velocità per livello, diagonale, bordi della mappa, nessun movimento senza ingresso.
- Costruzione: piano fuori raggio che aspetta, avvicinamento che lo fa partire, ordine della coda.
- Scavo: ritmo per livello, titanio solo col gamma, stiva piena, un oggetto alla volta, scarico entro 6 celle col massimo del CORE.
- Rifiuti dei comandi; salvataggio avanti e indietro; determinismo con movimento e scavo.

## Fuori ambito

Combattimento, distruzione e rinascita del drone, più droni, co-op (aspetti 13 e 14).
