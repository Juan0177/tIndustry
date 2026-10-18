# Nastri: come li piazza e modifica Mindustry, e cosa manca a tIndustry

Ricerca in sola lettura, 2026-10-18. Fonti Mindustry (master): `input/InputHandler.java`, `input/DesktopInput.java`, `input/Placement.java`, `input/Binding.java`, `world/Build.java`, `world/Block.java`, `world/blocks/distribution/Conveyor.java`. Riassunto con parole mie, nessun codice copiato. Fonti tIndustry: `godot/scripts/world/World.Input.cs`, `World.Placement.cs`, `src/TIndustry.Shared/Simulation/SimHost.cs` (`ApplyPlaceBelt`, `QueuePlan`), `Factory.CanPlace`, `godot/README.md`, ADR 0003.

## 1. Il modello di Mindustry (desktop)

Tasti di default (`Binding.java`): sinistro = piazza, destro = smonta/annulla, **centrale = prendi il blocco sotto il cursore (pick)**, **rotella = ruota** il blocco in mano (zooma solo se in mano non c'è un blocco ruotabile), R = ruota un blocco già piazzato sotto il cursore, **Ctrl sinistro = piazzamento "diagonale"**, Shift sinistro = boost (al rilascio accoda la linea al contrario), F = seleziona schema, Z/X = specchia schema, Q = svuota la coda, E = pausa costruzione.

Regole del trascinamento col sinistro:

1. **Ancora e cursore, non scia.** Alla pressione si fissa la cella d'ancora; a ogni cambio di cella la linea si ricalcola da zero dall'ancora al cursore. Durante il trascinamento si vede solo l'anteprima; i piani entrano nella coda del drone **al rilascio**, tutti insieme.
2. **Linea normale = retta sull'asse dominante** (`normalizeLine`): si confrontano |dx| e |dy|, vince il maggiore, l'altra coordinata resta quella dell'ancora. Una "L" non si fa con un trascinamento: si fanno due linee.
3. **Con Ctrl (diagonale)**: per i nastri, se l'impostazione `conveyorpathfinding` è attiva, un A* a 4 direzioni dall'ancora al cursore (passo 1, cambio direzione +8 per evitare zig-zag, celle non sostituibili costo 20, limite ~1000 nodi, poi ripiega sulla retta); senza A*, una scalinata Bresenham senza passi diagonali. Se entrambi gli estremi sono nastri di una catena esistente, Ctrl invece **segue la catena** (`upgradeLine`) per aggiornarla al tier in mano.
4. **Rotazione per cella**: ogni piano guarda la cella successiva del percorso. L'ultimo piano prende la rotazione del blocco di catena su cui finisce, se c'è; altrimenti, per i nastri, la direzione dal penultimo; per gli altri blocchi la rotazione scelta dal giocatore. Se il giocatore ruota **durante** il trascinamento, si accende `overrideLineRotation` e la linea smette di imporre la direzione del trascinamento (vale la rotazione scelta) fino al rilascio. Senza trascinamento (clic singolo) vale la rotazione del giocatore.
5. **Le linee valgono per tutti i blocchi**, anche multi-cella (spaziati della loro misura); i punti che cadono dentro un edificio multi-cella già piazzato si saltano.
6. **Sostituzione automatica in linea** (impostazione `blockreplace`): `getReplacement` per ogni piano; per i nastri diventa **incrocio** se la cella ha già un nastro perpendicolare e i due vicini del piano lungo la linea sono nastri o incroci.
7. **Ponti automatici** (`handlePlacementLine` → `Placement.calculateBridges`), solo su linee dritte e non "laterali": una programmazione dinamica sceglie per ogni cella nastro (costo 3), incrocio (30) o estremo di ponte (200, più una penalità crescente per ogni cella costruibile scavalcata). Le celle non piazzabili diventano incrocio se occupate da un blocco "da evitare" (nastri), altrimenti vanno scavalcate da un ponte entro la sua portata. Risultato: trascinando dritto contro un muro o un edificio stretto, la linea passa sopra con un ponte; sopra un nastro esistente preferisce l'incrocio.

Piazzare sopra qualcosa che esiste (`Build.validPlace`, `Block.canReplace`):

8. **Stesso blocco, stessa rotazione**: rifiutato (no-op). **Stesso blocco, rotazione diversa**: i blocchi `quickRotate` (i nastri) ruotano sul posto, subito, senza cantiere né costo.
9. **Blocco dello stesso gruppo** (trasporto: nastri di altro tier, incroci, ecc.) e misura compatibile: sostituzione con cantiere che ricorda il blocco precedente; un nastro che ne sostituisce un altro **eredita gli oggetti** (`overwrote`). Così si aggiorna il tier tracciando sopra la linea vecchia.
10. Fusione grafica: ogni nastro sceglie dritto/curva/T dai vicini che ci versano dentro; gli ingressi laterali entrano a metà nastro.

Smontaggio e altri comandi:

11. **Destro trascinato = rettangolo** ancora→cursore (limitato a una lunghezza massima): al rilascio smonta tutti gli edifici e annulla tutti i piani dentro. Sinistro premuto durante il destro annulla.
12. **Centrale = pick**: prende in mano il blocco sotto il cursore con la sua rotazione (e la configurazione).
13. **Schemi**: F trascinato copia un'area; i piani seguono il mouse, si ruotano e specchiano, clic per accodarli.
14. Trascinare un piano non ancora costruito lo sposta.

## 2. tIndustry oggi

- `World.Input.cs`: sinistro premuto con nastro/incrocio/splitter/selezionatore accende `_draggingPlace`; a ogni movimento `DragCells` (Bresenham a 4 vicini dall'ultima cella del mouse alla nuova) e `TryPlaceAt` per ogni cella: **è una scia che segue il mouse**, non una linea ancora→cursore. Ogni cella manda subito un `PlaceBelt`/`PlaceBuilding`: il piano entra in coda mentre si trascina, il drone parte già. Celle rifiutate in silenzio (`quietWhenBlocked`).
- **Direzione**: `PlaceCommandAt` usa sempre `_placeDir`, cambiato solo da R (`OnHudRotate`). Ogni cella della scia ha la stessa direzione, qualunque sia il verso del trascinamento. Unica eccezione: un nastro accanto al Core viene girato verso il Core (`CoreStockSink.PreferDirectionIntoCore`, non c'è in Mindustry).
- Sopra un nastro o un blocco esistente: `Factory.CanPlace` rifiuta (`!Belts.Contains`), `QueuePlan` rifiuta se c'è già un piano. Niente rotazione sul posto, niente cambio di tier, niente sostituzione.
- Destro: clic singolo alterna il segno di smontaggio; trascinato segna una volta per blocco lungo la **scia** (`RemoveOnceInDrag`), non un rettangolo.
- Rotella = zoom; rotella premuta = pan (`WorldCamera`); R = ruota il pennello; nessun pick, nessuno schema, nessun Ctrl/Shift.
- Ponte: piazzato a mano, un estremo; l'uscita la trova la simulazione (`FindBridgeExit`, campata 2–4).
- Curve dei nastri in grafica: già fatte (PR #205).
- **In corso** su `feat/nastri-t-incroci` (altro agente): incrocio automatico trascinando attraverso un nastro perpendicolare e grafica dei T di immissione. Non li conto come lacune.

## 3. Lacune

| # | Comportamento Mindustry | tIndustry | Cosa sente il giocatore | Consiglio | Sforzo |
|---|---|---|---|---|---|
| 1 | Ogni nastro della linea guarda la cella successiva; l'ultimo segue il trascinamento o la catena | **Diverso** (tutti `_placeDir`) | Con direzione Est trascina verso Nord: esce una colonna di nastri tutti verso Est, che non portano niente. Deve premere R prima di ogni tratto. È la differenza più grave | **Fare** (direzione dal passo della scia o della linea; clic singolo resta `_placeDir`) | S |
| 2 | Linea ancora→cursore sull'asse dominante, anteprima, accodamento al rilascio | **Diverso** (scia libera, accodata cella per cella) | In Mindustry una mano tremolante dà comunque una retta e si vede tutta prima di confermare; qui la scia riproduce ogni tremolio e i piani partono subito, senza poter "ripensarci" muovendo il mouse indietro | **Fare** (anteprima con le sagome già esistenti, invio dei comandi al rilascio; Esc/destro annulla) | M |
| 3 | Rotazione durante il trascinamento = `overrideLineRotation` | **Mancante** | Senza, non c'è modo di posare una fila di nastri "di lato" (es. una fila che versa tutta in un edificio accanto) | Fare insieme al #1 | S |
| 4 | Ctrl: scalinata o A* che aggira gli ostacoli | **Mancante** (la scia fa già scalinate, ma senza direzioni giuste) | Per collegare minatore e forno lontani e sfalsati servono più linee; con A* basta un trascinamento | Fare dopo #1–#2 (prima la scalinata, l'A* dopo) | M |
| 5 | Nastro sopra nastro con altra rotazione: ruota sul posto, gratis | **Mancante** (rifiutato) | Un nastro posato al contrario va smontato (destro, attesa drone, rimborso) e ripiazzato; in Mindustry ci si passa sopra e basta | **Fare** (comando o caso di `PlaceBelt` che ruota il nastro esistente subito, oggetti tenuti; deterministico, nessun cambio di salvataggio) | S–M |
| 6 | Nastro di altro tier sopra un nastro: sostituzione con cantiere, oggetti tenuti | **Mancante** (rifiutato) | Passare da nastro base a veloce = smontare tutta la linea e rifarla; in Mindustry la si ripassa | Fare (cantiere che ricorda il blocco precedente; probabile alzata della versione dei salvataggi se il cantiere salva il blocco vecchio) | M |
| 7 | Ctrl da un capo all'altro di una catena: la segue per aggiornarla | **Mancante** | Aggiornare una linea con curve in un solo gesto | Fare dopo #6 | S (dato #6) |
| 8 | Ponte automatico su linea dritta bloccata da muro/edificio/acqua profonda | **Mancante** | La linea si interrompe davanti all'ostacolo in silenzio; bisogna scegliere il ponte, orientarlo, tornare al nastro | Fare dopo l'incrocio automatico in corso (stessa sede: post-elaborazione della linea nel client prima dell'invio, con le regole di `FindBridgeExit`) | M |
| 9 | Incrocio automatico sopra nastro perpendicolare | **In corso** (`feat/nastri-t-incroci`) | — | — | — |
| 10 | Fusione dritto/curva/T | Curve: **uguale**; T: **in corso** | — | — | — |
| 11 | Destro trascinato = rettangolo | **Diverso** (scia) | Per sgomberare un'area bisogna "ripassarla" a righe; in Mindustry un rettangolo | Fare (rettangolo con contorno rosso, `RemoveOnceInDrag` su ogni cella al rilascio) | S |
| 12 | Rotella = ruota il blocco in mano (zoom solo senza blocco ruotabile o con Ctrl) | **Diverso** (rotella sempre zoom) | Chi viene da Mindustry gira la rotella per ruotare e invece zooma | Facoltativo: fare con Ctrl+rotella = zoom, oppure impostazione. Conflitto con l'abitudine attuale | S |
| 13 | Centrale = prendi il blocco sotto il cursore con la sua rotazione | **Mancante** (centrale = pan) | Per continuare una linea esistente bisogna cercare il tier nella palette e ruotare a mano | Fare: clic centrale senza movimento = pick, trascinato resta pan | S |
| 14 | Linee per tutti i blocchi (anche multi-cella, spaziati) | **Diverso** (solo nastri, incrocio, splitter, selezionatore) | Una fila di 5 minatori su un giacimento = 5 clic | Fare più tardi | S–M |
| 15 | Shift al rilascio: coda al contrario | **Mancante** | Minore: il drone costruisce dal capo sbagliato una linea lunga | Saltare (o dopo #2, quasi gratis) | S |
| 16 | Stesso nastro, stessa rotazione: no-op | **Uguale** in effetto (rifiuto silenzioso in trascinamento) | — | — | — |
| 17 | Celle dentro edifici saltate | **Uguale** in effetto (rifiuto silenzioso, buco nella linea) | — | Coperto da #8 | — |
| 18 | Schemi (F, copia, ruota, specchia) | **Mancante** | Niente copia di moduli ripetuti | Saltare per ora | L |
| 19 | Spostare un piano trascinandolo | **Mancante** | Minore | Saltare | M |
| 20 | Nastro accanto al Core girato verso il Core | **Solo tIndustry** | — | Tenere, ma con #1 va verificato che non contraddica la direzione del trascinamento (la regola deve valere solo se il nastro non ha un successivo nella linea) | S |

## 4. Ordine suggerito

1. #1 + #3 (direzione per cella e rotazione durante il trascinamento): piccolo, cambia tutto.
2. #2 (anteprima ancora→cursore, invio al rilascio), con #11 (rettangolo di smontaggio) che riusa lo stesso schema ancora/anteprima.
3. #5 (rotazione sul posto), poi #6 + #7 (cambio di tier).
4. #8 (ponte automatico), dopo che l'incrocio automatico in corso è unito.
5. #13, #4, #12, #14 secondo il tempo.

Note: #1–#4, #8, #11–#14 sono solo client (`godot/scripts/world/`); #5–#6 toccano la simulazione (`SimHost`, `Factory.CanPlace`, costruzione) e vanno coperti da test in `tests/TIndustry.Shared.Tests` con `InstantBuild` e senza.
