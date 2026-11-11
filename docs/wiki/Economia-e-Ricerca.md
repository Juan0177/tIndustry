# Economia e Ricerca

Il CORE e il suo magazzino, il Mercato con i suoi prezzi e l'albero della Ricerca con tutti gli sblocchi e i loro costi.

## Il CORE

Il CORE è il cuore della base: tutto quello che vi arriva sui nastri, o da un edificio che lo tocca, entra nel suo **magazzino**. Dal magazzino si pagano costruzioni e sblocchi. Il CORE non vende da solo: per guadagnare si vende al Mercato (o si accende la vendita automatica).

Il pannello in alto a sinistra mostra i **Fondi** (`$`), l'energia e gli oggetti che hai, ognuno con il massimo (`120/1.000`; passa col mouse per il nome). Un oggetto al massimo si vede pieno, col colore di avviso.

### Il magazzino ha un massimo

Ogni oggetto ha un **massimo**, che dipende dal livello del CORE. I soldi non hanno limite. Quando un oggetto è al massimo il CORE non lo accetta più: il nastro che lo porta si ferma e si riempie dietro, un minatore attaccato smette di consegnarlo, e riparte appena il magazzino scende sotto il massimo. Gli altri oggetti arrivano normalmente. I rimborsi (smontaggio, cantiere annullato) entrano fino al massimo: il resto va perso.

### I tre livelli

Si parte sempre dallo **shard**. Gli altri due si sbloccano nella Ricerca (ramo **CORE**, sotto la radice) e si piazzano sopra il CORE vecchio.

| CORE | Misura | Massimo per oggetto | Vendita automatica | Drone | Ricerca | Costruzione |
|---|---|---|---|---|---|---|
| shard | 3×3 | 1.000 | 10 pezzi/s | alpha | di partenza | — |
| foundation (`core-foundation`) | 4×4 | 4.000 | 25 pezzi/s | beta | $1.500.000, 800 Lastre di ferro, 400 Fili di rame | 800 Lastre di ferro, 500 Fili di rame, 400 Lastre di piombo |
| nucleus (`core-nucleus`) | 5×5 | 12.000 | 60 pezzi/s | gamma | $7.000.000, 2.400 Lastre di titanio, 1.800 Silicio | 3.000 Lastre di ferro, 2.000 Lastre di piombo, 1.500 Silicio, 1.000 Lastre di titanio |

Il nucleus si sblocca dopo il foundation. Non c'è più il pulsante «Potenzia CORE»: sbloccato il livello, il blocco compare nella palette (categoria **Magazzino**).

**Come si potenzia.** Il blocco si piazza solo sopra il CORE attuale: il fantasma si aggancia lì e non va altrove. Lo shard 3×3 cresce di una fila a destra e in basso (diventa foundation 4×4 con lo stesso angolo in alto a sinistra); il foundation cresce di una fila a sinistra e in alto (diventa nucleus 5×5, centrato come lo shard di partenza). Le celle nuove devono essere **libere e costruibili**: niente edifici, nastri, cantieri, pareti, acqua profonda, né il bordo della mappa; se no il piazzamento è rifiutato col motivo. Un magazzino collegato o lo [schermo di borsa](#lo-schermo-di-borsa) sulla fila di crescita vanno spostati prima, come ogni altro edificio (in Mindustry è lo stesso): conviene attaccarli dove il CORE non cresce. Lo shard cresce a destra e in basso e il foundation a sinistra e in alto, quindi nessun lato resta libero da tutti e due i potenziamenti: chi li fa entrambi sposta qualcosa una volta. È un cantiere del drone come gli altri: i materiali escono dal magazzino a poco a poco, intanto il CORE vecchio lavora normalmente. Annullarlo rende tutto lo speso. Un solo potenziamento alla volta, e il CORE non scende di livello.

**Il drone.** A potenziamento finito il drone passa al tipo successivo: più veloce a costruire e a volare, stesso raggio (vedi [Costruzione](Costruzione)). I [potenziamenti del drone](#i-potenziamenti) valgono per tutti e tre i tipi.

### Container e vault

| Blocco | Misura | Spazio per oggetto | Ricerca | Costruzione |
|---|---|---|---|---|
| Contenitore (`container`) | 2×2 | 300 | $110.000, 60 Lastre di ferro, 30 Lastre di piombo (dopo l'Incrocio) | 40 Lastre di ferro, 20 Lastre di piombo |
| Deposito (`vault`) | 3×3 | 1.000 | $880.000, 80 Lastre di titanio (dopo il Contenitore) | 60 Lastre di titanio, 40 Lastre di ferro |

Un magazzino che **tocca il CORE** (lato contro lato) è *collegato*: il suo spazio si somma al massimo di ogni oggetto e fa parte del magazzino del CORE (vendite, «Vendi tutto» e costruzioni vedono un magazzino solo). Uno che non lo tocca è un **deposito a sé**, con un contenuto suo per oggetto: accetta da nastri ed edifici qualsiasi oggetto finché c'è posto, e l'estrattore attaccato lo svuota. Smontarlo perde il contenuto (il rimborso dei materiali è quello normale). Se il CORE cresce fino a toccare un deposito a sé, questo versa il suo contenuto nel CORE fino al massimo. Se smonti un magazzino collegato e il massimo scende sotto quanto c'è, niente sparisce: l'oggetto resta «pieno» finché non scende sotto il nuovo massimo. I potenziamenti **Magazzino I** e **Magazzino II** alzano del 25% e del 50% il massimo del CORE (livello più magazzini collegati) e lo spazio dei depositi a sé (vedi [I potenziamenti](#i-potenziamenti)).

## Il Mercato

![Mercato](https://raw.githubusercontent.com/Juan0177/tIndustry/main/docs/img/readme/mercato.png)

`M` apre il Mercato, una panoramica: una riga per ogni oggetto che hai nel CORE, con quantità, prezzo del momento con la freccia (su o giù), un mini grafico degli ultimi 2 minuti, i pulsanti **Vendi 1**, **Vendi 100** e **Vendi tutto**, l'interruttore della vendita automatica di quell'oggetto e la spunta «solo a CORE pieno». La quantità si legge `x/massimo`. Non c'è un pulsante che vende tutto di tutti. Se un oggetto è in crollo la riga lo dice. Una spiegazione senza numeri è in [Mercato come una borsa](Mercato-come-una-borsa).

Ogni oggetto ha un mercato suo, come un titolo in borsa, con un **valore base** B che è anche il prezzo minimo: sotto B il prezzo non scende (salvo il crollo, sotto). Tutti i pezzi di una vendita valgono il prezzo del momento del clic.

### Come si muove il prezzo

- **Normale.** Fra B e 1,6×B, con una media di 1,3×B: un passo nuovo ogni 4 secondi, un richiamo verso la media e un piccolo scarto casuale.
- **Picco.** Per ogni oggetto, ogni 10-20 minuti: sale in 10 secondi fino a un massimo fra 2,8×B e 3×B, resta in cima 10 secondi e scende in 30. Da 2,8×B si dice che la vendita è «nel picco».
- **Vendite.** Ogni pezzo venduto abbassa il prezzo dello 0,04% di B, mai sotto B. In 2 minuti il prezzo torna al suo andamento.
- **Crollo.** Se in 2 minuti vendi 5.000 pezzi o più di un oggetto, quell'oggetto crolla per 20 minuti: prezzo vicino a B, nei primi 5 minuti a volte 0,6-0,7×B per 10 secondi, e niente picchi. Gli altri oggetti non cambiano.
- **Vendita automatica.** Un interruttore per oggetto. Vende **dal magazzino** al **prezzo minimo** (B, o il prezzo del momento se è sotto B in un crollo), con il ritmo del CORE: 10, 25 o 60 pezzi al secondo per oggetto (shard, foundation, nucleus). Non tocca il mercato: niente calo, niente crollo, niente picchi. Se la produzione supera il ritmo il magazzino sale fino al massimo e la linea si ferma. Accanto all'interruttore c'è la spunta **«solo a CORE pieno»**: l'oggetto si vende solo mentre è al massimo o a meno di un secondo di vendita dal massimo (massimo meno il ritmo), così il magazzino resta pieno, pronto per un picco, e la linea scorre fino al ritmo.
- **Jackpot.** Con **Vendi tutto**, nel picco, con almeno 400 pezzi e con lo [schermo di borsa](#lo-schermo-di-borsa) attivo, una vendita può valere circa dieci volte (probabilità dal 2% al 50% secondo i pezzi, con una «rete di sicurezza» che garantisce la vittoria dopo tre tentativi grossi a vuoto). Senza schermo attivo il «Vendi tutto» non tenta il jackpot. Vedi anche [Mercato come una borsa](Mercato-come-una-borsa).

### Valori base

| Oggetto | B | Normale (B - 1,6×B) | Picco massimo (3×B) |
|---|---|---|---|
| Ferro grezzo (`iron-ore`) | $6 | $6 - $9,6 | $18 |
| Lastra di ferro (`iron-plate`) | $23 | $23 - $36,8 | $69 |
| Rame grezzo (`copper-ore`) | $5 | $5 - $8 | $15 |
| Filo di rame (`copper-wire`) | $12 | $12 - $19,2 | $36 |
| Carbone (`coal`) | $3 | $3 - $4,8 | $9 |
| Piombo grezzo (`lead-ore`) | $4 | $4 - $6,4 | $12 |
| Lastra di piombo (`lead-plate`) | $14 | $14 - $22,4 | $42 |
| Titanio grezzo (`titanium-ore`) | $14 | $14 - $22,4 | $42 |
| Lastra di titanio (`titanium-plate`) | $52 | $52 - $83,2 | $156 |
| Grafite (`graphite`) | $12 | $12 - $19,2 | $36 |
| Silicio (`silicon`) | $32 | $32 - $51,2 | $96 |
| Sabbia (`sand`) | $2 | $2 - $3,2 | $6 |
| Vetro (`glass`) | $9 | $9 - $14,4 | $27 |
| Plastica (`plastic`) | $20 | $20 - $32 | $60 |
| Circuito (`circuit`) | $90 | $90 - $144 | $270 |

Il prezzo è sempre un numero intero di dollari quando paga: gli oggetti da pochi dollari cambiano prezzo in dollari di rado (il carbone solo una decina di volte in un quarto d'ora), quelli cari di continuo. Trasformare conviene: una Lastra di ferro vale $23, i due Ferro grezzo che servono per farla $12.

### Lo schermo di borsa

| Blocco | Misura | Ricerca | Costruzione |
|---|---|---|---|
| Schermo di borsa (`market-screen`) | 2×2 | $150.000, 240 Lastre di ferro, 120 Fili di rame (dopo l'Assemblatore) | 60 Lastre di ferro, 25 Fili di rame, 10 Lastre di piombo |

Uno solo per partita (finito, in cantiere o in piano), senza direzione e senza corrente, nella categoria **Magazzino** della palette. Si piazza solo se una sua cella tocca **lato contro lato** il CORE (se no: «Lo schermo di borsa va messo a contatto col CORE»; un secondo: «C'è già uno schermo di borsa: se ne può avere uno solo»). È **attivo** quando è finito e tocca il CORE: solo allora il «Vendi tutto» può tentare il jackpot e si vedono i moduli. «Staccato dal CORE» è solo uno stato di sicurezza (il CORE non può allargarsi sullo schermo); smontato e ripiazzato a contatto torna attivo. Sulle celle dove il CORE si allarga **blocca il potenziamento** come ogni edificio (vedi [Come si potenzia](#i-tre-livelli)).

- **Oggetto seguito.** Clic sullo schermo senza blocco scelto: la scheda «Oggetto seguito · Schermo di borsa» offre gli oggetti del Mercato e **Nessuno**. Sul vetro: icona, prezzo con ▲▼, grafico degli ultimi 2 minuti (gli stessi dati della panoramica) e «Crollo» se l'oggetto è crollato; senza oggetto «Scegli un oggetto». Il suggerimento al passaggio del mouse ripete tutto per esteso.
- **Esito del «Vendi tutto».** Per 6 secondi (360 tick) dopo ogni «Vendi tutto» fatto con lo schermo attivo: oggetto venduto, incasso «+$N» e i LED della cornice. «Vendi 1», «Vendi 100» e la vendita automatica non accendono niente.

| LED | Quando (`marketRules`) |
|---|---|
| Giallo | «Vendi tutto» da `ledYellowPieces` = 10.000 pezzi in su (il 50/50), vinto o perso; sempre col jackpot, che aggiunge «JACKPOT» e le luci lampeggianti negli angoli |
| Blu | sotto i 10.000 pezzi, senza crollo, e il prezzo è sceso al massimo di `ledMildDrop` = 10% di B |
| Viola | tutti gli altri: calo oltre il 10% di B, oppure l'oggetto crolla o era già in crollo |

I colori dei LED cambiano con la variante **Colori** (Normali, Daltonici, Alto contrasto).

### I moduli della Finanza

Tre nodi della Ricerca in fila dopo lo schermo. Non si piazzano: sbloccati, si vedono sul vetro (per l'oggetto seguito) e nella panoramica del Mercato (riga «Moduli» di ogni oggetto), **solo con lo schermo attivo**.

| Modulo | Cosa mostra |
|---|---|
| Previsione (`finance-forecast`) | Nello stato normale «picco fra circa N min» (minuti per eccesso, «meno di 1 min» sotto i 60 s) e «aspetta»; all'inizio della salita «picco in arrivo»; dalla soglia del picco (2,8×B) «vendi ora»; nel crollo «fine del crollo fra N min»; dopo un tentativo a vuoto «Vendi tutto bloccato per N min». Non prevede mai il jackpot |
| Percentuale (`finance-percent`) | Il prezzo del momento come «+N% su B», intero per difetto (negativo nel tuffo del crollo) |
| Probabilità (`finance-odds`) | La probabilità del jackpot se ora si facesse «Vendi tutto» di tutti i pezzi di quell'oggetto, rete di sicurezza compresa, calcolata con la stessa regola della vendita: 0% fuori dal picco, sotto i 400 pezzi o durante l'attesa |

## La Ricerca

![Albero della Ricerca](https://raw.githubusercontent.com/Juan0177/tIndustry/main/docs/img/readme/ricerca.png)

`T` apre la Ricerca, un albero come in Mindustry con il CORE in cima e ogni blocco sotto il suo prerequisito. Bordo dorato: puoi sbloccarlo ora. Lucchetto: prima sblocca il precedente. Passa col mouse per costo e mancanze, fai clic per il pannello di sblocco e **Conferma sblocco**. Trascina per muoverti, rotellina per lo zoom, `H` per la vista intera. Un nodo con più prerequisiti ha il primo come genitore nell'albero e gli altri collegati da linee tratteggiate.

Sbloccare costa **soldi e materiali**, presi dal CORE; poi il blocco compare nella palette e si costruisce pagando il suo costo di costruzione (vedi [Produzione](Produzione) e [Logistica](Logistica)). Alcuni nodi non danno un blocco: la ricetta dei circuiti, i moduli della Finanza e i [potenziamenti](#i-potenziamenti). I quattro nodi segnati come **premio** sono i [premi della campagna](#i-premi-della-campagna).

| Nodo | Ramo | Prerequisito | Costo di sblocco |
|---|---|---|---|
| Nastro T1 (`conveyor-basic`) | Logistica | — | iniziale |
| Incrocio (`junction`) | Logistica | Nastro T1 | $6.900, 26 Ferro grezzo |
| Router (`router`) | Logistica | Incrocio | $9.700, 26 Ferro grezzo |
| Overflow gate (`overflow-gate`) | Logistica | Router | $18.000, 12 Lastre di piombo, 6 Fili di rame |
| Underflow gate (`underflow-gate`) | Logistica | Overflow gate | $22.000, 12 Lastre di piombo, 6 Fili di rame |
| Selezionatore (`sorter`) | Logistica | Incrocio | $73.000, 30 Ferro grezzo, 30 Piombo grezzo |
| Selezionatore invertito (`sorter-inverted`) | Logistica | Selezionatore | $73.000, 30 Ferro grezzo, 30 Piombo grezzo |
| Nastro ponte (`conveyor-bridge`) | Logistica | Incrocio | $91.000, 63 Ferro grezzo, 63 Piombo grezzo |
| Ponte lungo (`bridge-long`) | Logistica | Nastro ponte | $140.000, 24 Lastre di titanio, 24 Lastre di piombo, 8 Silicio |
| Nastro T2 (`conveyor-fast`) | Logistica | Nastro T1 | $73.000, 24 Ferro grezzo, 24 Piombo grezzo, 24 Titanio grezzo |
| Nastro rinforzato (`conveyor-armored`) | Logistica | Nastro T2 | $740.000, 32 Ferro grezzo, 32 Piombo grezzo, 40 Titanio grezzo, 32 Grafite |
| Nastro T3 (`conveyor-express`) | Logistica | Nastro T2 | $740.000, 32 Titanio grezzo, 32 Silicio, 32 Grafite |
| Nastro a pacchi (`conveyor-stack`) | Logistica | Nastro T3 | $2.010.000, 144 Plastica, 144 Silicio, 144 Grafite · **premio del capitolo 2** (prezzo ×3) |
| Contenitore (`container`) | Logistica | Incrocio | $110.000, 60 Lastre di ferro, 30 Lastre di piombo |
| Deposito (`vault`) | Logistica | Contenitore | $880.000, 80 Lastre di titanio |
| Magazzino I (`upgrade-storage-1`) | Logistica | Contenitore | $270.000, 120 Lastre di piombo, 40 Lastre di titanio |
| Magazzino II (`upgrade-storage-2`) | Logistica | Magazzino I e Deposito | $1.300.000, 360 Lastre di titanio, 180 Grafite |
| CORE foundation (`core-foundation`) | CORE | CORE | $1.500.000, 800 Lastre di ferro, 400 Fili di rame |
| CORE nucleus (`core-nucleus`) | CORE | CORE foundation | $7.000.000, 2.400 Lastre di titanio, 1.800 Silicio |
| Drone: costruzione (`upgrade-drone-build`) | CORE | CORE | $36.000, 30 Lastre di ferro, 15 Fili di rame |
| Drone: stiva (`upgrade-drone-hold`) | CORE | CORE | $36.000, 30 Lastre di ferro, 15 Fili di rame |
| Drone: raggio (`upgrade-drone-range`) | CORE | CORE | $36.000, 30 Lastre di ferro, 15 Fili di rame |
| Drone: scavo (`upgrade-drone-mine`) | CORE | CORE | $36.000, 30 Lastre di ferro, 15 Fili di rame |
| Drone potenziato (`drone-boost`) | CORE | i quattro del drone | $1.680.000, 720 Lastre di titanio, 720 Silicio · **premio del capitolo 4** (prezzo ×3) |
| Minatore T1 (`miner`) | Produzione | — | iniziale |
| Estrattore (`extractor`) | Produzione | Minatore T1 | $2.800, 12 Lastre di ferro |
| Forno (`smelter`) | Produzione | Minatore T1 | $1.500, 30 Lastre di ferro |
| Assemblatore (`assembler`) | Produzione | Forno | $55.000, 66 Lastre di ferro, 12 Rame grezzo |
| Minatore T2 (`miner-advanced`) | Produzione | Minatore T1 | $150.000, 120 Lastre di ferro, 24 Fili di rame |
| Minatori I (`upgrade-miners-1`) | Produzione | Minatore T2 | $180.000, 120 Lastre di ferro, 30 Fili di rame |
| Minatori II (`upgrade-miners-2`) | Produzione | Minatori I | $340.000, 120 Lastre di titanio, 80 Fili di rame |
| Trapano a impatto (`impact-drill`) | Produzione | Minatore T2 | $670.000, 360 Lastre di titanio, 240 Silicio |
| Pressa grande (`graphite-press`) | Produzione | Assemblatore | $64.000, 60 Lastre di ferro, 30 Lastre di piombo |
| Plastificatore (`plasticizer`) | Produzione | Pressa grande | $410.000, 120 Silicio, 120 Grafite |
| Ricetta dei circuiti (`circuit`) | Produzione | Assemblatore | $1.020.000, 480 Silicio, 480 Lastre di piombo · **premio del capitolo 3** (prezzo ×3) |
| Macchine I (`upgrade-machines-1`) | Produzione | Assemblatore | $270.000, 120 Lastre di piombo, 80 Fili di rame |
| Macchine II (`upgrade-machines-2`) | Produzione | Macchine I e Fonderia avanzata | $890.000, 360 Lastre di titanio, 180 Silicio |
| Fonderia avanzata (`smelter-advanced`) | Produzione | Forno | $470.000, 160 Lastre di titanio, 80 Fili di rame |
| Separatore (`separator`) | Produzione | Fonderia avanzata | $540.000, 160 Lastre di titanio, 80 Grafite |
| Generatore (`generator`) | Energia | — | $5.500, 24 Lastre di ferro |
| Generatore a combustione (`combustion-generator`) | Energia | Generatore | $108.000, 180 Lastre di ferro, 90 Lastre di piombo, 54 Fili di rame · **premio del capitolo 1** (prezzo ×3) |
| Nodo T1 (`power-node`) | Energia | Generatore | $8.300, 8 Lastre di ferro, 4 Fili di rame |
| Lampada (`lamp`) | Energia | Nodo T1 | $11.000, 8 Lastre di ferro, 4 Fili di rame |
| Batteria (`battery`) | Energia | Nodo T1 | $91.000, 30 Lastre di piombo, 15 Fili di rame |
| Batteria grande (`battery-large`) | Energia | Batteria | $200.000, 160 Lastre di piombo, 80 Lastre di titanio, 40 Silicio |
| Pannello solare (`solar`) | Energia | Nodo T1 | $36.000, 18 Fili di rame, 12 Silicio |
| Pannello solare grande (`solar-large`) | Energia | Pannello solare | $200.000, 120 Lastre di piombo, 100 Silicio, 40 Lastre di titanio |
| Nodo T2 (`power-node-t2`) | Energia | Nodo T1 | $340.000, 48 Lastre di ferro, 24 Fili di rame |
| Schermo di borsa (`market-screen`) | Finanza | Assemblatore | $150.000, 240 Lastre di ferro, 120 Fili di rame |
| Modulo previsione (`finance-forecast`) | Finanza | Schermo di borsa | $1.400.000, 240 Lastre di piombo, 120 Fili di rame |
| Modulo percentuale (`finance-percent`) | Finanza | Modulo previsione | $1.700.000, 320 Lastre di titanio |
| Modulo probabilità (`finance-odds`) | Finanza | Modulo percentuale | $4.000.000, 720 Silicio, 480 Grafite |

La **Ricetta dei circuiti** è un nodo di tipo ricetta: non dà un blocco da piazzare ma insegna all'Assemblatore il Circuito (anche a quelli già costruiti). Gli sblocchi valgono per la partita in corso e si salvano con lei. Ogni livello della campagna riparte con i soli Nastro T1 e Minatore T1.

### I potenziamenti

Undici nodi senza blocco da piazzare (tipo `upgrade`, come i moduli della Finanza): sbloccati, cambiano un numero della partita dal tick dopo. Ogni gruppo ha due livelli e il **livello 2 sostituisce il livello 1**, non si somma: con Minatori I e Minatori II i minatori vanno al +20%, non al +30%.

| Gruppo | Livello 1 | Livello 2 | Cosa cambia |
|---|---|---|---|
| Drone: costruzione | `upgrade-drone-build` +25% | `drone-boost` +50% | velocità di costruzione e smontaggio del drone (alpha 0,5 → 0,625 → 0,75) |
| Drone: stiva | `upgrade-drone-hold` +50% | `drone-boost` +100% | pezzi nella stiva dello scavo a mano (alpha 30 → 45 → 60, beta 50 → 75 → 100, gamma 70 → 105 → 140) |
| Drone: raggio | `upgrade-drone-range` +5 celle | `drone-boost` +10 celle | raggio di costruzione e di scavo (27,5 → 32,5 → 37,5 celle) |
| Drone: scavo | `upgrade-drone-mine` +25% | `drone-boost` +50% | ritmo dello scavo a mano (alpha 0,7 → 0,875 → 1,05 pezzi/s) |
| Minatori | `upgrade-miners-1` +10% | `upgrade-miners-2` +20% | velocità di tutti i minatori: T1, T2 e trapano a impatto (il +20% del T2 con la corrente resta in più) |
| Macchine | `upgrade-machines-1` +15% | `upgrade-machines-2` +30% | velocità di forno, fonderia avanzata e assemblatore (non pressa, separatore e plastificatore): un forno passa da 120 a circa 104 e 92 tick per lastra |
| Magazzino | `upgrade-storage-1` +25% | `upgrade-storage-2` +50% | massimo per oggetto del CORE (livello più magazzini collegati: shard 1.000 → 1.250 → 1.500) e dei depositi a sé (contenitore 300 → 375 → 450, deposito 1.000 → 1.250 → 1.500) |

- **Drone potenziato** (`drone-boost`) è il livello 2 dei quattro gruppi del drone insieme, in un nodo solo: si sblocca dopo i quattro livelli 1 ed è il premio del capitolo 4. I potenziamenti del drone valgono per alpha, beta e gamma.
- **Arrotondamenti.** Capienze e massimi sono interi, per difetto (`n × (100 + p) / 100`); il ritmo dello scavo a mano è in millesimi interi. Senza potenziamenti tutti i numeri restano esattamente quelli di prima.
- **Niente si perde.** Quando un massimo cresce gli oggetti restano e c'è posto per altri; lo scavo a mano in corso continua col ritmo e la stiva nuovi. Anche la vendita automatica «solo a CORE pieno» segue il massimo nuovo. I potenziamenti non hanno un campo nel salvataggio: si rileggono dagli sblocchi.
- **In Ricerca** il pannello di un potenziamento ha la riga **Effetto**, con il numero di adesso e quello dopo lo sblocco (per esempio «Stiva del drone: 30 → 45»). Il pannello del blocco mostra i numeri potenziati: pezzi al secondo dei minatori, tempi delle ricette delle tre macchine, massimo del CORE e dei depositi. HUD e cerchio del raggio del drone usano già stiva e raggio potenziati.

### I premi della campagna

Quattro nodi sono il **premio di un capitolo** della campagna (`campaignReward` in `data/content.json`):

| Capitolo | Premio | Prezzo nella partita libera |
|---|---|---|
| 1 | Generatore a combustione (`combustion-generator`) | $108.000, 180 Lastre di ferro, 90 Lastre di piombo, 54 Fili di rame |
| 2 | Nastro a pacchi (`conveyor-stack`) | $2.010.000, 144 Plastica, 144 Silicio, 144 Grafite |
| 3 | Ricetta dei circuiti (`circuit`) | $1.020.000, 480 Silicio, 480 Lastre di piombo |
| 4 | Drone potenziato (`drone-boost`) | $1.680.000, 720 Lastre di titanio, 720 Silicio |

- **In campagna non si comprano**: lo sblocco è rifiutato con «Si ottiene chiudendo il capitolo N della campagna» (`CampaignRewardOnly`) e non si spende niente. Nell'albero il nodo ha un bordo di un colore suo (nelle tre varianti di **Colori**) e il numero del capitolo nell'angolo; il pannello dice «Premio del capitolo N» al posto del prezzo.
- **Si ottengono chiudendo il capitolo**, gratis e senza prerequisiti (`Factory.GrantCampaignReward`). A darli sarà il lancio della [campagna rifatta](Campagna-e-mappe#premi-dei-capitoli), il prossimo lavoro: la campagna di oggi, a dieci livelli, non ha ancora capitoli, quindi lì i quattro nodi restano bloccati.
- **Nella partita libera si comprano** al prezzo che il foglio darebbe al nodo **per 3**, soldi e materiali (scritto dallo script, come ogni costo).

### Quanto costa tutto: circa 6 ore

I costi di sblocco non si scelgono a mano: li calcola il foglio «Bilanciamento» di `tools/dev/genera_finanza.py`, che li scrive in `data/content.json` (`--scrivi-costi`) e controlla che siano ancora quelli (`--controlla`). L'idea è che sbloccare **tutta la Ricerca** (50 nodi, in tutto **$30.193.700**, premi compresi al prezzo ×3) richieda **circa 6 ore** di partita libera ben giocata senza jackpot, vendendo il prodotto migliore di ogni fase e non il ferro grezzo. Il foglio dà **6,00 ore** senza jackpot, **5,63** con un jackpot e **5,26** con due. La regola è 5,5–6,5 ore; i test vogliono 6,0 ± 0,2 senza jackpot, jackpot che accorciano davvero e un totale della Ricerca entro il 5% da quello del foglio (fra 28,68 e 31,7 milioni).

Storia: prima della Ricerca allargata il foglio dava 5,86 ore. I sei nodi del piano 1 (macchine e ricetta dei circuiti) lo hanno portato a 6,38 e gli otto del piano 2 (logistica ed energia) a 6,50, accorciando per restarci qualche nodo. Il piano 3 ha aggiunto gli undici potenziamenti e il prezzo ×3 dei premi, ha messo la plastica nella fase Avanzata e i circuiti nella fase Fine e ha rifatto i minuti di tutti i nodi: 6,00 ore.

| Fase | Orario | Prodotti venduti (al secondo) | Incasso a B |
|---|---|---|---|
| Inizio | 0–30 min | 2 Lastre di ferro | $46/s |
| Metà | 30 min – 2 h | 10 Fili di rame, 8 Lastre di piombo, 6 Grafite | $304/s |
| Avanzata | 2–4 h | 12 Lastre di titanio, 12 Silicio, 6 Plastica | $1.128/s |
| Fine | 4–6 h | 36 Lastre di titanio, 35 Silicio, 8 Circuiti | $3.712/s |

Il costo in soldi di un nodo è l'incasso della sua fase per i minuti di lavoro che gli dà il foglio, arrotondato a due cifre (per 3 se è un premio); i materiali sono quelli di prima moltiplicati per la fase (×2, ×3, ×4, ×6, e per 3 se è un premio), così una fase non si salta vendendo. Lungo ogni catena dell'albero i costi crescono. I nodi:

- **Inizio**: forno, estrattore, generatore, incrocio, Nodo T1, router, lampada.
- **Metà**: assemblatore, pressa grande, Nastro T2, selezionatori, overflow e underflow gate, ponte, batteria, pannello solare, generatore a combustione, i quattro potenziamenti del drone di livello 1, contenitore, Minatore T2, Minatori I, schermo di borsa.
- **Avanzata**: ponte lungo, pannello solare grande, batteria grande, Macchine I, Magazzino I, Minatori II, Nodo T2, fonderia avanzata, separatore, plastificatore, ricetta dei circuiti, nastri rinforzato e T3, deposito, CORE foundation, previsione, percentuale.
- **Fine**: trapano a impatto, nastro a pacchi, Drone potenziato, Macchine II, Magazzino II, probabilità, CORE nucleus.

Due conti a parte:

- Il **jackpot** sul testa o croce di 10.000 Lastre di titanio alla soglia del picco vale **$5.500.000** (la stessa vendita a B ne darebbe 520.000): quasi un CORE nucleus.
- Le fasi non partono sempre all'orario: una fase comincia solo quando la Ricerca di quelle prima è tutta comprata. Senza jackpot la Ricerca dell'Avanzata finisce verso **4 h 41 min**, e lì parte la fase Fine. Con le fasi all'orario in 6 ore si incasserebbero $36.572.400; nella partita del foglio si incassa un po' meno.

Costi di costruzione dei blocchi: invariati. Il foglio ha già una riga «Difesa (aspetto 13)», oggi a zero, per la spesa di torrette e difese quando arriveranno.

## Vedi anche

- [Risorse e oggetti](Risorse-e-oggetti)
- [Campagna e mappe](Campagna-e-mappe)
- [Costruzione](Costruzione)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Costruzione](Costruzione) · [Torna alla Home](Home) · [Pagina successiva: Campagna e mappe →](Campagna-e-mappe)

<!-- /navigazione -->
