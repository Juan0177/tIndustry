# Interazione con i nastri come in Mindustry

- **Stato**: da approvare (punti scelti dall'utente il 2026-10-18)
- **Ricerca**: [come Mindustry piazza e modifica i nastri, e cosa manca](2026-10-18-nastri-mindustry-ricerca.md)
- **Decisioni collegate**: [ADR 0003 — Costruzione come Mindustry](../../decisioni/0003-costruzione-come-mindustry.md)
- **Dopo**: la PR dell'unione a T e dell'incrocio automatico (`feat/nastri-t-incroci`), che tocca gli stessi file

## Obiettivo

Piazzare, girare, potenziare e smontare i nastri con gli stessi gesti di Mindustry, così chi viene da Mindustry si trova a casa e le linee lunghe si fanno con un solo trascinamento.

## Decisioni prese con l'utente

Tutti gli otto gruppi proposti si fanno: (1) direzione che segue la linea, (2) R durante il trascinamento, (3) linea dritta con anteprima e invio al rilascio, (4) nastro sopra nastro (giro sul posto, cambio di tier, Ctrl per la catena), (5) ponte automatico sugli ostacoli, (6) smontaggio a rettangolo, (7) tasto centrale per prendere il blocco, (8) Ctrl per la diagonale e rotellina che ruota.

## Comportamento

### Trascinamento col sinistro

- **Ancora e cursore**: alla pressione si fissa la cella d'ancora; a ogni movimento la linea si ricalcola dall'ancora al cursore.
- **Linea normale**: retta sull'asse dominante (vince il maggiore fra |dx| e |dy|; l'altra coordinata resta quella dell'ancora).
- **Con Ctrl**: percorso a scalinata senza passi diagonali, che aggira gli ostacoli con una ricerca A* a 4 direzioni (cambio di direzione penalizzato per evitare zig-zag, celle non piazzabili costose, limite di nodi; se fallisce, la retta).
- **Direzione per cella**: ogni nastro guarda la cella successiva della linea; l'ultimo prende la direzione del nastro su cui finisce, se c'è, altrimenti quella dal penultimo. Il clic singolo usa la direzione scelta con R.
- **R durante il trascinamento**: fissa la direzione scelta per tutta la linea fino al rilascio (per posare una fila "di lato").
- **Anteprima e invio al rilascio**: durante il trascinamento si vede solo l'anteprima (le sagome del fantasma di piazzamento, valide o rifiutate); al rilascio tutti i comandi partono insieme, in ordine dall'ancora. Esc o il destro durante il trascinamento annullano.
- La regola del nastro accanto al Core che si gira verso il Core vale solo per l'ultima cella senza successiva.

### Nastro sopra nastro (simulazione)

- **Stesso tier, direzione diversa**: il nastro gira sul posto, subito e gratis, tenendo gli oggetti (giro rapido come Mindustry).
- **Tier diverso**: sostituzione con un cantiere che ricorda il nastro precedente; al termine il nastro nuovo eredita gli oggetti. Si paga la differenza secondo le regole dei cantieri di ADR 0003. Se il cantiere deve salvare il blocco precedente e il formato non lo permette, i salvataggi passano alla v13 (con lettura dei v12).
- **Ctrl da un capo all'altro di una catena**: il trascinamento segue la catena esistente invece della retta e la potenzia tutta al tier in mano.
- Stesso nastro, stessa direzione: nessun effetto, in silenzio.
- **Attraversare un nastro perpendicolare** (decisione dell'utente del 2026-10-19, come Mindustry): l'incrocio automatico si crea solo se la giunzione è sbloccata (e si può pagare). Senza giunzione il trascinamento non si rifiuta più (`CrossingNeedsJunction` sparisce dalla linea): il nastro nuovo sostituisce la cella attraversata con le regole qui sopra (giro sul posto o cambio di tier), così la linea attraversata diventa un'unione 2→1 o 3→1 nella linea nuova. Due linee che si incrociano senza mescolarsi esistono solo con la giunzione.

### Ponte automatico

Su una linea dritta, quando una o più celle non si possono piazzare (parete, edificio, acqua profonda), la linea mette un ponte che le scavalca entro la portata del ponte (campata 2–4, regole di `FindBridgeExit`). Sopra un nastro perpendicolare resta l'incrocio automatico. Se l'ostacolo è più lungo della portata, la cella resta rifiutata e il fantasma lo mostra.

### Smontaggio a rettangolo

Trascinando col destro si disegna un rettangolo dall'ancora al cursore, con contorno rosso. Al rilascio si segnano per lo smontaggio tutti i blocchi e si annullano tutti i piani dentro. Il clic singolo destro resta come oggi.

### Tasto centrale e rotellina

- Clic centrale senza movimento: prende in mano il blocco sotto il cursore con la sua direzione (e il filtro del selezionatore). Trascinato resta lo spostamento della vista.
- Rotellina con un blocco orientabile in mano: lo ruota; con Ctrl, o senza blocco in mano, zooma come oggi.

## Dove sta la logica

- Linea, direzione per cella, anteprima, rettangolo, tasto centrale, rotellina: client (`godot/scripts/world/World.Input.cs`, `World.Placement.cs`).
- Ponte automatico e scelta incrocio/ponte lungo la linea: una funzione pura in `TIndustry.Shared` che riceve la linea e restituisce i piazzamenti, così un futuro server (e gli agenti) usano la stessa regola.
- Giro sul posto e cambio di tier: `SimHost` / `Factory` / costruzione, deterministici.

## Verifica

- xUnit: retta sull'asse dominante; direzioni per cella (ultima cella con e senza catena); A* con ostacoli e ripiego; ponte automatico su parete, edificio, acqua profonda e ostacolo troppo lungo; giro sul posto con oggetti tenuti; cambio di tier con cantiere, costo e oggetti ereditati (con `InstantBuild` e senza); catena potenziata con Ctrl; salvataggio e caricamento di un cantiere di sostituzione.
- Playtest: trascinamento verso Nord con direzione Est che dà nastri verso Nord; R durante il trascinamento; anteprima senza piani prima del rilascio; rettangolo di smontaggio; clic centrale che prende il blocco; catture per ciascuno.
- Documenti: `godot/README.md` (comandi), `docs/ARCHITETTURA.md`, ADR 0003 (sostituzione e giro sul posto), `CHANGELOG.md`.

## Fuori ambito

Schemi copia-incolla, spostare un piano trascinandolo, Shift per invertire la coda, linee di blocchi multi-cella.
