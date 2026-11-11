# tIndustry — Godot 4 .NET (**path ufficiale**)

Client di gioco primario. Sim in `src/TIndustry.Shared/`.

## Download

Release zip ufficiali = questo client:

- Windows: `tIndustry-win-x64.zip` → `tIndustry.exe`
- Linux: `tIndustry-linux-x64.zip` → `tIndustry.x86_64`

## Run (da sorgente)

Tutto in un comando (controlla le dipendenze, compila, importa e avvia): `.\play.ps1 -Utente` o `.\play.ps1 -Dev` su Windows, `tools/dev/dev.sh` su Linux. Con `-Dev` (`TINDUSTRY_DEV=1`) si abilitano F1–F4 per la velocità. Se compilazione o importazione restano vecchie, `.\clean.ps1` (`-Prova` elenca soltanto) cancella i file generati e il prossimo avvio ricostruisce tutto. A mano:

```bash
godot4 --path godot
TINDUSTRY_FRESH=1 godot4 --path godot          # ignore continua
```

Richiede Godot **4.4+** .NET e restore C# (`dotnet build godot/TIndustry.Godot.csproj`).

## Export locale (CI)

```bash
./tools/ci/setup-godot.sh   # editor + templates (una tantum)
./tools/ci/export-godot.sh  # → dist/tIndustry-*-x64.zip
```

## Nuova partita e mappe

Dalla Home, **Nuova partita** apre una schermata con la **misura** della mappa e il **seme**:

| Misura | Lato |
| --- | --- |
| Media | 1000×1000 |
| Grande | 1500×1500 |
| Enorme | 2000×2000 |

Il seme è già compilato con un numero casuale: puoi cambiarlo (un intero tra -2147483648 e 2147483647), sceglierne un altro con **Nuovo seme** o copiarlo con **Copia**. Lo stesso seme con la stessa misura dà sempre la stessa mappa. Due caselle che si escludono, **Solo giorno (Pacifica)** e **Solo notte**, cambiano il tempo del mondo (nessuna casella = ciclo di giorno e notte; vedi «Giorno, notte e Lampada»). **Inizia** (o Invio) avvia la partita, **Indietro** (o Esc) torna alla Home. Mentre una partita si apre (nuova, livello della campagna, salvataggio, Continua) compare la schermata di caricamento, con la fase in corso; in quel momento tasti e clic sono ignorati.

La mappa ha quattro biomi (rocce e basalto, sabbia e deserto, neve e ghiaccio, prati e paludi), laghi e fiumi. Il CORE parte al centro, in un'area libera con ferro e rame vicini e una strada a terra verso ferro, rame e carbone.

- **Pareti** (colori scuri con un bordo più chiaro verso le celle libere): non ci si costruisce sopra, né blocchi né nastri.
- **Acqua bassa** (vicino alle rive): si costruisce normalmente.
- **Acqua profonda** (blu scuro, al centro di laghi e fiumi): non ci si costruisce e i nastri non la attraversano; il **ponte** la scavalca, purché le due estremità stiano su celle costruibili.
- Il drone vola sopra tutto.

Sopra una parete o l'acqua profonda il fantasma del blocco diventa rosso e un suggerimento in basso dice il motivo ("C'è una parete", "Acqua profonda").

Giacimenti: ferro, rame, carbone, piombo e titanio. Il **titanio** si scava solo col **Minatore T2** (da sbloccare in Ricerca): come in Mindustry serve una trivella di tier più alto. Un Minatore T1 dove sotto c'è solo titanio viene rifiutato ("Serve il Minatore T2 per il titanio"); su celle miste scava gli altri minerali e ignora il titanio. Il Minatore T2 scava tutto, come il **Trapano a impatto**. Un minatore senza un giacimento che sa scavare, ma su pavimento di sabbia o sabbia scura, scava **Sabbia** (ritmo in proporzione alle celle di sabbia); un giacimento vince sempre sulla sabbia.

I livelli della **Campagna** hanno mappe prestabilite, più piccole all'inizio (200×200 nel primo livello) e più grandi verso la fine (1000×1000 nel decimo).

### Giorno, notte e Lampada

Il mondo ha un ciclo di **giorno (10 minuti) e notte (4 minuti)**, con alba e tramonto sfumati di 20 secondi; il tempo parte dal primo blocco piazzato. Una riga sotto le risorse del CORE dice quanto manca alla notte o al giorno (si ferma con la pausa). Nella schermata Nuova partita due caselle che si escludono cambiano il tempo: **Solo giorno (Pacifica)** (sempre giorno, nessun contatore) e **Solo notte** (sempre buio; la riga conta 10 minuti fino al primo attacco, poi «Attacco!» per 5 secondi; gli attacchi veri arriveranno con l'aspetto 13). Al buio contano le luci dei blocchi (Core, forni al lavoro, generatori accesi, droni) e le **Lampade**, che consumano corrente anche di giorno ma la loro luce si vede quando la luce del mondo scende sotto la metà. I livelli 1–3 della Campagna sono solo giorno, il 6 (*Corrente*) e il 10 (*Espansione T2*) solo notte, gli altri hanno il ciclo. La **Lampada** (1×1, categoria Potenza, si sblocca nella Ricerca dopo il Nodo T1; costa 2 lastre di ferro e 1 filo di rame) si accende solo se è collegata come forni e assemblatori (generatore vivo adiacente o nodo in raggio) e consuma 1 unità di corrente al secondo; spenta non fa luce. Di notte sui prati compaiono anche le lucciole (solo con Effetti Alti).

### Rete di corrente

Generatori, nodi, Batterie e le macchine che consumano corrente formano una **rete**: i blocchi che si toccano lato contro lato sono collegati, gli altri si collegano con i **laser dei nodi** (Nodo T1: raggio 6, 6 collegamenti; Nodo T2: raggio 10, 10 collegamenti; il raggio si misura fra i bordi dei blocchi). Un nodo nuovo si collega da solo ai nodi e ai blocchi vicini. Per cambiare: **clic sul nodo** (si vedono il raggio e i collegamenti usati, per esempio 3/6), poi **clic su un blocco di corrente** nel raggio per collegarlo o scollegarlo; **Esc** deseleziona. Un collegamento rifiutato dice il motivo (fuori raggio, nodo senza collegamenti liberi, stesso blocco). Il suggerimento su un blocco di corrente riassume la rete: blocchi, produzione, consumo, soddisfazione e batterie. La **Batteria** (1×1, capacità 1.000, in Ricerca) mostra la carica con una barra. Se la corrente non basta, tutte le macchine della rete rallentano della stessa percentuale. Regole e numeri: Wiki, pagina Energia.

### Macchine della Ricerca allargata

Cinque blocchi della categoria **Produzione**, tutti da sbloccare in Ricerca e senza direzione: **Fonderia avanzata** (2×2: le ricette del forno, vetro compreso, a velocità doppia; solo con corrente, 20; niente carbone), **Pressa grande** (2×2: 3 carbone → 2 grafite in 1,5 s, senza corrente), **Separatore** (2×2: 2 sabbia → un minerale a caso, ferro, rame o piombo grezzo al 30% e titanio grezzo al 10%; corrente 8), **Plastificatore** (2×2: 2 carbone → 1 plastica in 2 s; corrente 6) e **Trapano a impatto** (3×3: scava ogni giacimento, fino a 4 pezzi al secondo con 9 celle su 9 e la rete piena; corrente 30, fermo senza; esce sui 12 lati). Le macchine con la corrente obbligatoria vanno alla loro velocità per la soddisfazione della rete. Il nodo **Ricetta dei circuiti** (niente da piazzare) insegna all'Assemblatore il Circuito: filo di rame + silicio + lastra di piombo in 3 s. Il pannello del blocco mostra le ricette coi tempi (la fonderia avanzata coi tempi dimezzati) e, per il separatore, la probabilità di ogni minerale. Oggetti nuovi nel Mercato: Sabbia ($2), Vetro ($9), Plastica ($20), Circuito ($90). Dettagli nella Wiki, pagina Produzione.

### Logistica ed energia della Ricerca allargata

Otto blocchi da sbloccare in Ricerca. In **Logistica**: **Overflow gate** (1×1, senza direzione: l'oggetto va dritto e, solo se davanti non accetta, ai lati alternando) e **Underflow gate** (prima ai lati, dritto solo se i lati non accettano), 4 lastre di piombo e 2 fili di rame, cappuccio ambra e rosso; **Nastro a pacchi** (21,6 celle/s, si trascina come un nastro: la prima cella della linea raccoglie fino a 10 pezzi dello stesso oggetto e li manda in un pacco, pieno o dopo un secondo; solo l'ultima cella scarica, e solo in un edificio o nel CORE; il pacco si vede con da 0 a 3 copie più scure dietro l'oggetto); **Ponte lungo** (come il ponte ma fino a 8 di distanza; l'entrata consuma 1 di corrente al secondo e senza corrente gli oggetti aspettano). In **Potenza**: **Pannello solare** (1×1, 3 al secondo) e **Pannello solare grande** (3×3, 36), che producono con la luce del giorno, niente di notte, e si scuriscono quando producono meno; **Generatore a combustione** (2×2, 90 al secondo, 1 carbone ogni 2 s o 1 grafite ogni 4 s, prima il carbone, scorta di 8; il focolare si accende mentre brucia e di notte fa luce); **Batteria grande** (3×3, 20.000). Il pannello del blocco e la Ricerca mostrano per ognuno la sua riga (portata, pacchi, combustibili, produzione, capacità). Dettagli nella Wiki, pagine Logistica ed Energia.

## Controlli build

**Il drone sei tu** (come Mindustry, niente strumenti) · **WASD** muove il drone e la visuale lo segue con un leggero inseguimento · **centrale** tenuto trascina la visuale, al rilascio torna sul drone · **rotella** zoom (con un blocco orientabile scelto lo ruota, con **Ctrl** zooma sempre) · **H** visuale subito sul drone · senza blocco scelto il **clic** seleziona o configura il blocco sotto il cursore (filtro di selezionatori ed estrattori, oggetto seguito dallo schermo di borsa, laser dei nodi; le informazioni al passaggio del mouse) e il **clic tenuto su un giacimento** lo scava a mano finché il tasto è giù · **1–9** o un clic in palette sceglie un blocco: clic o trascinamento piazzano i piani (anteprima, piani al rilascio; **Ctrl** percorso, **R** a metà fissa la direzione, **Esc** o destro annullano), **R** ruota · **destro** o **Esc** con un blocco scelto lo deselezionano · **destro** senza blocco cancella un piano o smonta, trascinato un rettangolo · **Q** sceglie il blocco sotto il cursore

Mentre un pannello copre la partita (Ricerca, Mercato, Campagna, Impostazioni) il drone si ferma; il tasto tenuto vale di nuovo appena il pannello si chiude. Il drone manda il suo ingresso solo quando i tasti cambiano, così un `muovi` degli agenti non viene sovrascritto a ogni frame. **Scavo a mano**: il drone alpha scava ferro, rame, piombo e carbone a 0,7 pezzi al secondo con una stiva da 30 (beta 0,9 e 50; gamma 1,1 e 70, e anche il titanio). Entro 6 celle dal bordo del CORE la stiva si scarica da sola; lo scavo si annulla fuori dal raggio di 27,5 celle o sotto un edificio, e si ferma mentre il drone costruisce (se il cantiere è fermo per mancanza di materiali un avviso lo dice: annulla il piano o allontanati). Un clic breve su un giacimento fuori raggio non dice nulla; tenuto, avvisa che è fuori raggio. Nell'HUD la stiva è accanto alle risorse. I piani fuori raggio restano in coda, semitrasparenti, e partono quando ti avvicini. Un clic sulla coda dei lavori sposta la visuale sul cantiere finché il drone non si muove o non si usa il centrale.

### Costruzione (come Mindustry)

Piazzare non crea il blocco subito: diventa un **piano**, una sagoma semitrasparente col contorno giallo. Trascinando compare l'anteprima della linea, una sagoma per cella; i piani nascono al rilascio. Il **drone** (lo guidi tu con WASD) costruisce il primo piano della coda entro il suo raggio (27,5 celle): lo sprite si disegna dal basso mentre il raggio parte dal drone. Le risorse escono dal CORE man mano e non bloccano il piazzamento: se mancano, il cantiere si ferma e, passandoci sopra col mouse, dice cosa manca ("Mancano: 3 Ferro grezzo", oppure "Mancano: $20"). Riparte da solo quando le risorse arrivano. Se un cantiere resta fermo per un secondo e c'è un altro piano nel raggio, il drone lo lascia dov'è (con quello che ha già speso) e passa ai successivi, come in Mindustry: il piano fermo va in fondo alla coda e il cantiere riprende quando ritocca a lui. L'ultimo nastro di una linea (o di un clic) accanto al CORE è pianificato già rivolto verso il CORE. I piani fuori raggio aspettano finché il drone non si avvicina: si vedono più tenui, con un contorno tratteggiato, e la coda dice «Fuori raggio». Con un blocco scelto si vede il raggio del drone, un cerchio tratteggiato. Mentre scava a mano il drone tira un laser fino al giacimento; la **stiva** (oggetto e quantità/massimo, per esempio 12/30) compare nell'HUD sotto la scorta del CORE e si svuota da sola entro 6 celle dal CORE.

Tasto **destro**:

- su un **piano** non ancora iniziato lo cancella; su un blocco già segnato per lo smontaggio toglie il segno, da qualunque cella del blocco (anche dall'altro capo di un ponte);
- su un **cantiere** in costruzione lo annulla: il drone lo smonta e restituisce man mano tutto quello già speso (un cantiere lasciato fermo viene smontato quando ritocca a lui);
- su un **edificio** o un **nastro** lo segna in rosso (riempimento rosso traslucido): il drone lo smonta e rimborsa man mano (100% nella partita normale e nei livelli 1–3 della campagna, 50% dal livello 4); gli oggetti dentro vanno persi;
- su un cantiere già in smontaggio o su una cella vuota non fa nulla.

Tenendo premuto il destro e trascinando si disegna un **rettangolo** dall'ancora al cursore, col contorno rosso (al massimo 64 celle per lato); al rilascio si **segnano** tutti i blocchi che lo toccano, si cancellano i piani e si annullano i cantieri dentro, una volta per blocco (un cambio di tier si annulla e il nastro vecchio si segna). Il rettangolo non toglie mai un segno di smontaggio, anche sopra blocchi già segnati o con due rettangoli sovrapposti; solo un clic destro singolo, senza trascinare, toglie un segno. Come per la linea, **Esc**, un pannello che si apre, la finestra che perde il fuoco o un rilascio fuori dalla finestra annullano il rettangolo senza smontare nulla.

Tasto **Q**: **sceglie** il blocco (o il cantiere) sotto il cursore, con la sua direzione (il filtro di selezionatori ed estrattori si sceglie poi col clic sul blocco, senza blocco scelto); un blocco non ancora sbloccato dà un toast e la scelta non cambia. Tasto **centrale** tenuto: trascina la visuale lontano dal drone; al rilascio torna sul drone. **Rotellina**: con un blocco orientabile scelto (nastri, ponte, assemblatore) lo ruota come **R** (su in senso orario, giù al contrario; durante un trascinamento fissa la direzione come R); con **Ctrl**, senza blocco scelto o con un blocco senza direzione zooma.

**Trascinamento come Mindustry.** Col sinistro premuto, con un blocco scelto, si fissa l'ancora e si vede solo l'anteprima dall'ancora al cursore: nulla parte finché il tasto è giù. Nastri (anche a pacchi), incroci, router, selezionatori e gate fanno una **retta sull'asse dominante** (vince il più lungo fra orizzontale e verticale); ogni altro blocco è uno solo, sull'ancora. La **direzione viene dal trascinamento** per ogni blocco: ogni nastro guarda il successivo, l'ultimo prende quella del nastro su cui finisce (se c'è) e, accanto al CORE, guarda il CORE; un blocco solo guarda verso il cursore (il pennello resta quello scelto con R). Un clic usa la direzione di R. **R a metà** trascinamento gira il pennello e fissa quella direzione per tutta la linea fino al rilascio (una fila "di lato"). Con **Ctrl** i nastri seguono la catena esistente fra ancora e cursore (e la portano al tier in mano) oppure un percorso a scalinata che aggira gli ostacoli; senza percorso torna la retta. Le sagome rosse sono le celle che la linea già rifiuta: non si mandano, e se non parte nulla (un clic sul CORE, per esempio) un toast dice perché. Al **rilascio** la linea si ricalcola e tutti i piani partono insieme, in ordine dall'ancora; un rifiuto come le risorse insufficienti dà un solo toast per linea. **Esc** o il **destro** durante il trascinamento annullano la linea (il blocco resta scelto); anche un pannello che si apre (T, M, G, I), la finestra che perde il fuoco o un rilascio fuori dalla finestra la annullano senza piazzare nulla. **Ponte automatico**: col ponte sbloccato, una retta di nastri che incontra pareti, acqua profonda o edifici li scavalca con un ponte (fino a 3 celle); un ostacolo più lungo resta rosso. Nastro sopra nastro e incrocio automatico valgono anche dentro la linea (qui sotto).

**Incrocio automatico.** Trascinando una linea di nastri che attraversa un nastro perpendicolare e prosegue oltre, in quella cella va un **incrocio** al posto del nastro (si paga l'incrocio e il nastro viene rimborsato come uno smontaggio; il drone smonta il nastro e poi costruisce l'incrocio, e le risorse dell'incrocio devono esserci già), come in Mindustry: le due linee si attraversano senza mescolarsi. Succede solo dove l'incrocio fa lo stesso lavoro del nastro: un nastro dritto che riceve da un altro pezzo di logistica dietro di sé e passa a un pezzo di logistica davanti (non accanto al CORE, a un edificio o a un minatore che lo alimenta, non su curve o T, non su un nastro già segnato da smontare). Sopra router, selezionatori, ponti e incroci non cambia nulla; sopra nastri paralleli, con un clic singolo o sull'ultima cella del trascinamento non mette incroci. Annullare uno dei due piani (destro) li annulla entrambi. L'incrocio funziona come la *Junction* di Mindustry: una coda di 6 oggetti per ciascuna delle quattro direzioni, ognuno lo attraversa in 26 tick ed esce appena il blocco dall'altra parte lo accetta (circa 13,8 oggetti/s per direzione); le code non si bloccano fra loro e anche gli edifici accanto ci mettono dentro i loro oggetti. Non rallenta i nastri base, T2 e rinforzati; un nastro espresso pieno passa a quel ritmo. Se l'incrocio non è ancora sbloccato in Ricerca, il nastro attraversato gira nella direzione della linea e le sue merci entrano di lato: la linea attraversata diventa un'unione (2→1, o 3→1 con un ingresso per lato), come in Mindustry. **Nastro sopra nastro**: un nastro dello stesso tipo piazzato sopra un nastro con un'altra direzione lo gira subito e gratis, tenendo gli oggetti; con la stessa direzione non succede nulla. Un nastro di un **altro tipo** (un altro tier, o il rinforzato) sopra un nastro semplice lo **sostituisce**, come in Mindustry: il drone apre un cantiere di sostituzione al suo posto, si paga solo la **differenza** fra i due costi (la parte comune è già pagata dal nastro vecchio; quella oltre torna al CORE con la quota di rimborso) e il nastro nuovo **tiene gli oggetti** del vecchio; annullare il cantiere (destro) rimette il nastro vecchio con i suoi oggetti. Per potenziare un'intera catena si trascina con **Ctrl** dal primo all'ultimo nastro col tier in mano: la linea segue la catena e ogni nastro tiene la sua direzione. Un rettangolo del destro sopra un cambio di tier, in attesa o già iniziato, lo annulla e segna da smontare il nastro vecchio. Un nastro con un ingresso su entrambi i lati e nulla dietro si disegna a **T**.

**Coda nell'HUD**: nella colonna in alto a sinistra, sotto le risorse del CORE, un riquadro mostra il blocco in lavorazione con la sua barra (rossa se si smonta, ambra con "Manca: <materiale>" se il cantiere è fermo per le risorse) e "+N" piani in attesa. Un clic sposta la visuale sul cantiere. Il riquadro sparisce quando la coda è vuota. Se un piano non si può più costruire quando il drone arriva (cella occupata, blocco non sbloccato) un avviso dice il motivo.

Palette Mindustry (angolo basso-destra): griglia fissa di 4 colonne con icone da 32 px, senza testo né numeri, della stessa misura in ogni categoria (le caselle libere restano vuote); mostra solo i blocchi già sbloccati in Ricerca; rail categorie a icone (Logistica / Produzione / Potenza / Magazzino; niente strumenti: senza blocco scelto il clic seleziona nel mondo; un clic sul blocco già scelto lo deseleziona); hover/selezione apre il pannello info (nome, tasto rapido `[2]`, costi con icona barrata se mancano); `?` apre dettaglio.

HUD in alto a sinistra (stile Mindustry): pulsanti **Menu · Ricerca · Mercato · Campagna**, poi pannello CORE con **Fondi**, energia e solo le risorse che hai (icona + numero, nome al passaggio del mouse), e gli obiettivi di campagna. Ogni risorsa si legge `quantità/massimo` (col colore di avviso quando è piena). Non c'è più il pulsante «Potenzia CORE»: i livelli del CORE (shard 3×3, foundation 4×4, nucleus 5×5) si sbloccano in Ricerca e si piazzano dalla categoria **Magazzino** della palette, sopra il CORE (il fantasma si aggancia lì e mostra in rosso le celle occupate); lì ci sono anche **Contenitore**, **Deposito** e lo **Schermo di borsa**.

| Hotkey | Azione |
| --- | --- |
| WASD | Muove il drone (la visuale lo segue) |
| Q | Sceglie il blocco sotto il cursore |
| H | Visuale subito sul drone |
| Esc / \` | Deseleziona il blocco; senza blocco scelto (e senza filtro o nodo aperti) Esc salva la Continua e apre la Home |
| 1–9 | Nastro … Ponte (2 = Minatore) |
| M | Mercato (vendi stock) |
| G | Campagna (seleziona livello) |
| T | Ricerca (grafo icon-node) |
| R | Ruota direzione piazzamento |
| Spazio | Pausa / riprende la partita (scritta «In pausa» al centro); i comandi dati in pausa partono alla ripresa. Non nei menu, non con un campo di testo o un pulsante col fuoco, non nei playtest né nelle partite degli agenti (lì il tempo lo comandano `pausa`, `riprendi`, `esegui`, `avanza`) |
| Clic sinistro / Esc | Durante la **sequenza del tentativo di jackpot**: saltano al risultato, un altro chiude (non nei primi 0,2 s e mai se il jackpot è vinto). Spazio mette in pausa la sequenza; gli altri comandi del mondo sono bloccati finché dura |
| F1–F4 | Solo con `TINDUSTRY_DEV=1` (mai nei playtest): velocità ×1 / ×5 / ×10 / ×20; in alto a destra una barra con velocità, tick e tempo di gioco (mm:ss) |
| RMB | Con un blocco scelto lo deseleziona; senza, cancella un piano, annulla un cantiere o segna un blocco da smontare; clic singolo toglie il segno, trascinare segna soltanto (non è uno slot palette) |

**Home durante una partita** (Esc senza blocco scelto, o **Menu**): il primo pulsante è **Riprendi**, che torna alla partita così com'è (Continua, che ricaricherebbe l'ultimo salvataggio automatico, non c'è); anche **Esc** riprende. Gli altri pulsanti sono quelli di sempre. Nella Home iniziale, all'avvio, Esc (come **Esci**) chiede conferma con «Uscire dal gioco?»: **Invio** o **Esci** esce, **Esc** o **Annulla** torna alla Home.

Ponte: estremi 1×1, campata larga circa il 70% della cella, distanza 2–4 (ponte lungo 2–8, con corrente), celle in mezzo libere (un nastro può passarci sotto).

**Filtri col clic** (come Mindustry): router, selezionatori, gate ed estrattore non hanno direzione (R e la rotellina non li girano). Senza blocco scelto, un clic su un selezionatore, un selezionatore invertito o un estrattore apre sopra il blocco la griglia delle icone degli oggetti e il pulsante **Nessuno**: la scelta manda il filtro e chiude; l'oggetto già scelto, cliccato di nuovo, toglie il filtro. Un altro clic sul blocco, un clic altrove, **Esc** o un blocco scelto in palette chiudono il pannello. L'icona del filtro si vede al centro del blocco; i blocchi si piazzano senza filtro. Sullo **schermo di borsa** (clic su una qualunque delle sue 4 celle) la stessa scheda, «Oggetto seguito · Schermo di borsa», offre solo gli oggetti del Mercato e sceglie l'oggetto mostrato sul vetro.

**Portata dei nastri** (come Mindustry): in una cella stanno fino a 3 oggetti, a 0,4 di cella l'uno dall'altro, e una linea piena porta velocità / 0,4 oggetti al secondo, uguale in tutte le direzioni. Nastro base 2,1 celle/s (5,25 oggetti/s), T2 e rinforzato 4,8 (12), espresso 7,2 (18); il nastro a pacchi tiene un pacco per cella a 21,6 celle/s (fino a 216 pezzi/s con pacchi pieni). Selezionatore istantaneo (passa al ritmo del nastro), router 7,5 (un oggetto ogni 8 tick), ponte 5,25, incrocio circa 13,8 per direzione (6 oggetti ogni 26 tick); ogni oggetto passa da una cella all'altra senza fermarsi.

**Nastro rinforzato** (categoria Logistica, si sblocca in Ricerca dopo il Nastro T2): veloce come il T2, ma come l'*Armored Conveyor* di Mindustry prende oggetti dagli altri nastri da ogni lato e dagli edifici (e da router, selezionatori, ponti e incroci) solo da dietro. Una linea rinforzata passa accanto a una fila di trivelle o macchine senza raccogliere niente e resta dritta, senza unirsi agli edifici ai lati; con i nastri si unisce come gli altri. Binari d'acciaio rivettati con il bordo e le frecce rossi.

## Grafica

Edifici, nastri, Core, drone e oggetti sono pixel-art nostra generata da `python tools/art/genera.py` (32 px per cella, mostrati ×2 senza filtro; vedi `tools/art/README.md` e l'ADR 0004). Gli edifici hanno parti animate: la trivella del minatore gira mentre estrae, il forno si accende, i bracci dell'assemblatore premono, il rotore del generatore gira finché c'è combustibile e il focolare del generatore a combustione arde mentre brucia; la fonderia avanzata ha le fiamme nelle due bocche (con fumo e luce di notte), il pistone della pressa scende, il tamburo del separatore gira, nel plastificatore salgono bolle e la punta del trapano a impatto gira, solo mentre lavorano; il nucleo del Core pulsa piano; i pannelli solari si scuriscono quando la luce cala. I nastri scorrono alla velocità del loro tier e gli oggetti sopra hanno il loro sprite a colori, senza tinta. Anche le icone dell'interfaccia (16 e 32 px), il logo, le caselle di spunta e il carattere pixel in `assets/ui/` sono generati da `tools/art`: nel gioco non ci sono asset di terzi (`assets/ATTRIBUTION.md`).

### Impostazioni grafiche

Il pannello **Impostazioni** ha una sezione **Grafica**; **Applica** salva su disco e vale subito:

| Voce | Effetto |
| --- | --- |
| **Effetti** (Alti / Bassi / Spenti) | Particelle: Alti = tutte, anche neve, sabbia e foglie d'ambiente; Bassi = la metà e niente ambiente; Spenti = nessuna e acqua ferma |
| **Ombre** | Ombra sotto ogni blocco |
| **Bagliore** | Alone luminoso su forni, Core, Lampade e scintille |
| **Animazioni dei blocchi** | Trivelle, rotori, bocche dei forni e luce del Core; spente restano ferme (le particelle non dipendono da questa voce) |
| **Limite FPS** | Nessuno, 30, 60, 120, 144 |
| **Scala rendering** | 100%, 75%, 50%: il mondo si disegna a risoluzione ridotta e si ingrandisce, l'interfaccia resta nitida (per i PC deboli) |
| **VSync** | Sincronizzazione verticale |
| **Schermo intero** | Schermo intero alla risoluzione dello schermo (sì) o finestra 1280×720 centrata (no) |

Un file di impostazioni di una versione precedente resta valido: le voci mancanti valgono il predefinito (Effetti Alti, Ombre e Bagliore accesi, nessun limite, scala 100%).

**Avvio.** Il gioco esportato parte a schermo intero alla risoluzione dello schermo; il mondo resta in scala 1:1, quindi uno schermo più grande mostra più mappa. Al primo avvio (nessun file di impostazioni) la Scala UI segue l'altezza dello schermo: 100% sotto i 1400 pixel, 150% sotto i 2000, 200% oltre. In sviluppo e nei test (editor, `dev.sh`, `play.ps1`, build di debug) parte in una finestra 1280×720 con l'interfaccia al 100%; una scelta salvata vale comunque. I playtest sono sempre in finestra 1280×720 al 100%, qualunque cosa dicano le impostazioni, così le catture restano confrontabili.

Nella sezione **Interfaccia** la **Scala UI** (cursore da 75% a 200%, a passi di 5; predefinita 100%) ingrandisce solo HUD, pannelli e suggerimenti: il mondo e la visuale restano come sono, e il clic cade sempre sulla cella sotto il mouse. Il testo pixel è più nitido a 100%, 150% e 200% (ogni pixel del carattere è un numero intero di pixel dello schermo); alle altre scale qualche riga di pixel è un po' più larga. Su una finestra 1280×720 tutto sta nella finestra fino al 150%; sopra, alcuni pannelli scorrono o escono un poco dai bordi. I vecchi valori (100, 125, 150, 200) restano validi.

**Colori** (Normali / Daltonici / Alto contrasto) cambia subito i colori che hanno un significato: anteprima di piazzamento valida o rifiutata, costruzione e smontaggio, energia e denaro, rifiuti e materiali mancanti, linee della Ricerca. Daltonici usa tinte sicure per deuteranopia e protanopia (azzurro e vermiglio, giallo e porpora); Alto contrasto ha pannelli neri, testo bianco, bordi chiari e segni nel mondo giallo puro e rosso magenta con un bordo nero, leggibili su ogni terreno (lo smontaggio usa lo stesso rosso del rifiuto: lo distingue il tratteggio dalla X). Il significato non sta mai solo nel colore: l'anteprima rifiutata ha una X (e in basso il motivo, per esempio "Serve il Minatore T2 per il titanio"), i blocchi da smontare e quelli che il drone sta smontando sono tratteggiati, un materiale o il denaro mancante è barrato, l'energia bassa ha un "!" e un prezzo in calo una freccia.

**Lingua** (Italiano / English) cambia subito tutti i testi, anche nei pannelli aperti: nomi dei blocchi e degli oggetti, livelli e obiettivi della campagna, rifiuti ed errori dei salvataggi. Predefinito italiano.

**Home.** Il logo pixel è generato da `tools/art` (`ui/ui-logo`); dietro la Home la mappa scorre piano da sola e non risponde a mouse e tastiera (nei playtest resta ferma). Nella Home e nelle schermate aperte da lì l'HUD della partita è nascosto.

## Prove automatiche

```bash
tools/dev/playtest.sh ui /tmp/shots-ui          # oppure: tutorial, agenti
python tools/dev/compare_shots.py <baseline> <nuovi>
```

Il playtest `agenti` prova il canale degli agenti (`AgentBridge`, `godot/scripts/dev/`): `playtest.sh` sceglie una porta libera e un segreto, e un client dentro il playtest gioca come un agente (stato, piazza, avanza, `ui` e clic, tasti, rotella, telecamera, cattura su file, nella risposta e a 1920×1080, log, richieste in raffica durante `esegui`), controllando che senza segreto o con una richiesta da browser il canale chiuda la connessione. Il canale esiste solo nelle build di sviluppo, con `TINDUSTRY_AGENT_PORT`, `TINDUSTRY_AGENT_TOKEN` e un profilo temporaneo (`TINDUSTRY_DATA_DIR` fuori dai dati veri), e ascolta solo su `127.0.0.1`: nel gioco esportato non si apre mai. Gli agenti lo usano attraverso il server MCP di `tools/AgentPlay` (vedi il suo README).

`tools/dev/playtest.sh` lancia il gioco con `TINDUSTRY_PLAYTEST=ui|tutorial|agenti`, input simulati e un profilo dati temporaneo (`TINDUSTRY_DATA_DIR`): non tocca impostazioni e salvataggi reali. Salva un PNG per passo e `log.txt` nella cartella indicata; esce con 0 solo se il log contiene `PLAYTEST OK` e nessun `ERROR`. Con `TINDUSTRY_PLAYTEST_PERF=1` il playtest `ui` posa anche 200 nastri e 100 edifici a strati al lavoro (forni, assemblatori, generatori, minatori) e stampa FPS medio e minimo e numero di nodi (`PLAYTEST perf: …`), senza esito. Con `TINDUSTRY_PLAYTEST_BLOCCHI=1` costruisce ogni blocco di produzione e potenza spento e acceso e salva `ui-blocchi-spenti.png`, `ui-blocchi-accesi.png`, `ui-blocchi-zoom04.png`, `ui-blocchi-core.png` (Core e drone da vicino) e `ui-blocchi-oggetti.png` (ogni oggetto su ogni nastro), senza esito. Nei playtest gli effetti, le ombre, il bagliore e la notte sono spenti e gli orologi fermi, così le catture sono stabili; con `TINDUSTRY_PLAYTEST_EFFETTI=1` il playtest `ui` li accende e aggiunge il probe di G3 (60 secondi con effetti Alti, FPS, nodi e particelle, ombre, bagliore, notte con Lampade, scala di rendering) con le sue catture `fx-*` e `luci-*`, senza esito sulle immagini. Il playtest `ui` prova anche la Scala UI (catture `ui-scala-<percento>-*`): a 100, 125, 150 e 200% apre ogni pannello e controlla che nessun testo esca dal suo rettangolo e che i pannelli stiano nella finestra (obbligatorio fino al 150%), che il mondo non cambi e che a 200% un clic col mouse pianifichi il nastro proprio sulla cella sotto il cursore; con `TINDUSTRY_PLAYTEST_SCALA=<percento>` un playtest intero gira a quella scala. Con `TINDUSTRY_PLAYTEST_LINGUA=en` un playtest intero gira in inglese (il controllo dei rettangoli misura i testi tradotti); il playtest `ui` prova anche il cambio di lingua, le tre varianti di colore e la Home. I playtest sono sempre in una finestra 1280×720 al 100%. `compare_shots.py` confronta due cartelle di screenshot ignorando la zona FPS.

Lanciando Godot a mano, `TINDUSTRY_PLAYTEST` richiede `TINDUSTRY_DATA_DIR`; `TINDUSTRY_CAPTURE_DIR` cambia dove vanno gli screenshot (default `godot/artifacts/`).

## Save / load

| Azione | UI | Hotkey | Slot file |
| --- | --- | --- | --- |
| Salva continua | chip **F5** | F5 | `continua.json` |
| Carica continua | chip **F9** | F9 | `continua.json` |
| Salva slot | chip **F6** | F6 | `slot-1.json` |
| Carica slot | chip **F7** | F7 | `slot-1.json` |

Path: `%LocalAppData%/tIndustry/godot-saves/` (Linux: `~/.local/share/tIndustry/godot-saves/`), oppure `godot-saves/` dentro `TINDUSTRY_DATA_DIR` se impostata. Formato alla versione 22 (metadati + istantanea, con le macchine della Ricerca allargata e le celle coperte dei minatori, i pacchi del nastro a pacchi, la corrente del ponte lungo, i pannelli solari, i combustibili dei generatori e il tipo delle batterie, lo schermo di borsa (oggetto seguito e ultimo esito), il drone (ingresso, scavo e stiva), il generatore della mappa, il livello del CORE, container e vault e la vendita automatica): i salvataggi delle versioni precedenti non si aprono più. **Gestione salvataggi** mostra per ogni slot il tipo di mappa ("Biomi · 1500 · seme N", "Mappa <livello> · W×H" o "Classico · W×H · seme N").  
Salva nastri (con oggetti e pacchi, partner del ponte, filtro del selezionatore, turni di selezionatori e gate), edifici, denaro, `nextItemId`, Core, regole di partita, drone con la coda dei piani e cantieri a metà.

## Progetti

| Percorso | Ruolo |
| --- | --- |
| `godot/` | Client di gioco **ufficiale** |
| `src/TIndustry.Shared/` | Simulazione della fabbrica, contenuti e salvataggi (condivisa) |
| `data/` | Contenuti (`content.json`, `campaign.json`), unica fonte |

Mappa completa: [`docs/ARCHITETTURA.md`](../docs/ARCHITETTURA.md).

## Ricerca

**T** apre la Ricerca, un albero stile Mindustry: **CORE in cima**, ogni blocco sotto il suo prerequisito (linee a gomito; prerequisiti aggiuntivi tratteggiati). Bordo dorato = puoi sbloccarlo ora, lucchetto = prima sblocca il precedente; passa col mouse per costo e mancanze, click per il pannello di sblocco. Trascina per muoverti, rotella zoom, H vista intera. Sblocca strutture spendendo denaro/materiali; i blocchi non ancora sbloccati non si piazzano. Undici nodi sono **potenziamenti** senza blocco (drone: costruzione, stiva, raggio, scavo e Drone potenziato; Minatori I e II; Macchine I e II; Magazzino I e II): il pannello mostra la riga **Effetto** col numero di adesso e quello dopo. Quattro nodi sono i **premi dei capitoli** della campagna (generatore a combustione, nastro a pacchi, ricetta dei circuiti, Drone potenziato): in campagna hanno il numero del capitolo nell'angolo e non si comprano, nella partita libera costano il triplo. Il salvataggio (versione 22) include `unlockedStructures` e lo stato del Mercato.

## Mercato

**M** apre il Mercato, una panoramica con una riga per ogni oggetto che hai nel CORE: icona, nome, quantità, prezzo del momento con la freccia (su o giù), mini grafico degli ultimi 2 minuti, **Vendi 1**, **Vendi 100**, **Vendi tutto**, l'interruttore della **vendita automatica** di quell'oggetto e la spunta **«solo a CORE pieno»**; la quantità è `x/massimo`. Non c'è un pulsante che vende tutto di tutti. Il pannello Core mostra `$`. Necessario per guadagnare denaro e sbloccare in Ricerca.

Ogni oggetto ha un prezzo suo, come un titolo in borsa:

- il prezzo sta fra il **valore base** B (il minimo) e 1,6×B e cambia di continuo, piano;
- ogni 10-20 minuti, per oggetto, c'è un **picco**: sale in 10 secondi fino a circa 3×B, resta in cima 10 secondi e scende in 30. Vendere lì rende fino al triplo;
- tutti i pezzi di una vendita valgono il prezzo del momento del clic; dopo, il prezzo scende un po' per ogni pezzo venduto e in 2 minuti torna al suo andamento;
- vendere **5.000 pezzi o più** in 2 minuti dello stesso oggetto lo fa **crollare** per 20 minuti: prezzo vicino a B, qualche volta sotto, e niente picchi. Gli altri oggetti non se ne accorgono (la riga mostra «Crollo»);
- un **tentativo di jackpot** (Vendi tutto nel picco con almeno 400 pezzi) richiede lo **schermo di borsa** attivo (sotto); un tentativo a vuoto senza crollo blocca per 5 minuti ogni «Vendi tutto» (la riga mostra l'attesa);
- la **vendita automatica** (un interruttore per oggetto) vende dal magazzino al prezzo minimo, 10, 25 o 60 pezzi al secondo secondo il CORE: non muove il mercato, ma neppure coglie i picchi; con la spunta «solo a CORE pieno» vende solo quando l'oggetto è al massimo;
- il **magazzino ha un massimo** per oggetto (1.000 / 4.000 / 12.000 secondo il CORE, più container e vault collegati): a oggetto pieno la linea si ferma.

Con lo schermo attivo e i moduli della Finanza sbloccati, ogni riga ha una terza riga **Moduli** (previsione e consiglio, percentuale su B, probabilità del jackpot).

**Schermo di borsa** (2×2, categoria Magazzino, si sblocca in Ricerca dopo l'Assemblatore; costa 60 lastre di ferro, 25 fili di rame e 10 lastre di piombo): uno solo, senza direzione né corrente, piazzabile solo a contatto lato contro lato col CORE (fantasma rosso e motivo se no). Finito e a contatto è **attivo**: accende il jackpot del «Vendi tutto» e mostra sul vetro l'oggetto seguito (icona, prezzo con ▲▼, grafico di 2 minuti, «Crollo») e le righe brevi dei moduli; senza oggetto «Scegli un oggetto»; «Staccato dal CORE» (jackpot spento) è solo uno stato di sicurezza, perché lo schermo blocca il potenziamento del CORE. Dopo un «Vendi tutto», per 6 secondi: oggetto, incasso «+$N» e i LED della cornice, **blu** (calo fino al 10% di B), **viola** (calo più forte o crollo), **gialli** (10.000 pezzi o più, il 50/50); col jackpot «JACKPOT» e le luci negli angoli che lampeggiano. **Sequenza del tentativo di jackpot** (12 s, solo per un «Vendi tutto» che può vincere, con lo schermo attivo): la visuale si avvicina allo schermo senza HUD, il bot esce, vola al CORE con la valigetta, torna e la lancia, lo schermo calcola, i LED lampeggiano e l'incasso gira su rulli; col jackpot coriandoli e «JACKPOT». **Clic sinistro** o **Esc** saltano al risultato (un altro chiude); non nei primi 0,2 s e mai col jackpot vinto. **Spazio** mette in pausa, come la perdita del fuoco della finestra. Home, caricamento, partita nuova, un pannello che si apre o lo schermo smontato la chiudono subito; l'esito resta 6 secondi sul vetro dopo la fine. Il suggerimento al passaggio del mouse dice tutto per esteso, anche il significato dei LED; i colori seguono la variante Colori. Sulle celle dove il CORE cresce lo schermo blocca il potenziamento come ogni edificio: va spostato prima. Moduli (Ricerca, niente da piazzare): **previsione** («picco fra circa N min» · «aspetta», «vendi ora» nel picco, fine del crollo, attesa del «Vendi tutto»), **percentuale** («+N% su B»), **probabilità** del jackpot di un «Vendi tutto» adesso.

Il salvataggio (versione 22) include lo stato di tutti i mercati e lo schermo (oggetto seguito e ultimo esito).

## Campagna

**G** apre la selezione livelli. Obiettivi in alto a sinistra (`OBIETTIVO`). Completando un livello si sblocca il successivo. Continua salva `activeCampaignLevelId` nel salvataggio (versione 22).
