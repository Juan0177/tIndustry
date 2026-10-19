# tIndustry — Godot 4 .NET (**path ufficiale**)

Client di gioco primario. Sim in `src/TIndustry.Shared/`.

## Download

Release zip ufficiali = questo client:

- Windows: `tIndustry-win-x64.zip` → `tIndustry.exe`
- Linux: `tIndustry-linux-x64.zip` → `tIndustry.x86_64`

## Run (da sorgente)

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

Il seme è già compilato con un numero casuale: puoi cambiarlo (un intero tra -2147483648 e 2147483647), sceglierne un altro con **Nuovo seme** o copiarlo con **Copia**. Lo stesso seme con la stessa misura dà sempre la stessa mappa. **Inizia** (o Invio) avvia la partita, **Indietro** (o Esc) torna alla Home. Per Grande ed Enorme compare "Generazione della mappa…" mentre la mappa si prepara (qualche secondo); in quel momento tasti e clic sono ignorati.

La mappa ha quattro biomi (rocce e basalto, sabbia e deserto, neve e ghiaccio, prati e paludi), laghi e fiumi. Il CORE parte al centro, in un'area libera con ferro e rame vicini e una strada a terra verso ferro, rame e carbone.

- **Pareti** (colori scuri con un bordo più chiaro verso le celle libere): non ci si costruisce sopra, né blocchi né nastri.
- **Acqua bassa** (vicino alle rive): si costruisce normalmente.
- **Acqua profonda** (blu scuro, al centro di laghi e fiumi): non ci si costruisce e i nastri non la attraversano; il **ponte** la scavalca, purché le due estremità stiano su celle costruibili.
- Il drone vola sopra tutto.

Sopra una parete o l'acqua profonda il fantasma del blocco diventa rosso e un suggerimento in basso dice il motivo ("C'è una parete", "Acqua profonda").

Giacimenti: ferro, rame, carbone, piombo e titanio. Il **titanio** si scava solo col **Minatore T2** (da sbloccare in Ricerca): come in Mindustry serve una trivella di tier più alto. Un Minatore T1 dove sotto c'è solo titanio viene rifiutato ("Serve il Minatore T2 per il titanio"); su celle miste scava gli altri minerali e ignora il titanio. Il Minatore T2 scava tutto.

I livelli della **Campagna** hanno mappe prestabilite, più piccole all'inizio (200×200 nel primo livello) e più grandi verso la fine (1000×1000 nel decimo).

### Notte e Lampada

Nella schermata Nuova partita la casella **Notte** fa una mappa al buio: contano le luci dei blocchi (Core, forni al lavoro, generatori accesi, droni) e le **Lampade**. Anche i livelli 6 (*Corrente*) e 10 (*Espansione T2*) della Campagna sono di notte. La **Lampada** (1×1, categoria Potenza, si sblocca nella Ricerca dopo il Nodo T1; costa 2 lastre di ferro e 1 filo di rame) si accende solo se è collegata come forni e assemblatori (generatore vivo adiacente o nodo in raggio) e consuma 1 unità di corrente al secondo; spenta non fa luce. Di notte sui prati compaiono anche le lucciole (solo con Effetti Alti).

## Controlli build

**Cursore** (default, categoria Strumenti) · 1–9 tools · **Esc** / riesci tool → cursore · **R** ruota · **C** cicla filtro · click pianifica · trascina col sinistro una linea (anteprima, piani al rilascio; **Ctrl** percorso, **R** a metà fissa la direzione, **Esc** o destro annullano) · **destro** cancella o smonta, trascinato un rettangolo · **centrale** prende il blocco sotto il cursore · pan: **WASD**, rotella premuta, o trascina col sinistro in modalità cursore · rotella ruota il blocco orientabile in mano, altrimenti (o con **Ctrl**) zoom · **H** torna al CORE

### Costruzione (come Mindustry)

Piazzare non crea il blocco subito: diventa un **piano**, una sagoma semitrasparente col contorno giallo. Trascinando compare l'anteprima della linea, una sagoma per cella; i piani nascono al rilascio. Il **drone** parte dal CORE, vola verso il primo piano della coda e lo costruisce: lo sprite si disegna dal basso mentre il raggio parte dal drone. Le risorse escono dal CORE man mano e non bloccano il piazzamento: se mancano, il cantiere si ferma e, passandoci sopra col mouse, dice cosa manca ("Mancano: 3 Lastra di ferro", oppure "Mancano: $20"). Riparte da solo quando le risorse arrivano. Se un cantiere resta fermo per un secondo e in coda ci sono altri piani, il drone lo lascia dov'è (con quello che ha già speso) e passa ai successivi, come in Mindustry: il piano fermo va in fondo alla coda e il cantiere riprende quando ritocca a lui. L'ultimo nastro di una linea (o di un clic) accanto al CORE è pianificato già rivolto verso il CORE. Con la coda vuota il drone torna accanto al CORE.

Tasto **destro**:

- su un **piano** non ancora iniziato lo cancella; su un blocco già segnato per lo smontaggio toglie il segno, da qualunque cella del blocco (anche dall'altro capo di un ponte);
- su un **cantiere** in costruzione lo annulla: il drone lo smonta e restituisce man mano tutto quello già speso (un cantiere lasciato fermo viene smontato quando ritocca a lui);
- su un **edificio** o un **nastro** lo segna in rosso (riempimento rosso traslucido): il drone lo smonta e rimborsa man mano (100% nella partita normale e nei livelli 1–3 della campagna, 50% dal livello 4); gli oggetti dentro vanno persi;
- su un cantiere già in smontaggio o su una cella vuota non fa nulla.

Tenendo premuto il destro e trascinando si disegna un **rettangolo** dall'ancora al cursore, col contorno rosso (al massimo 64 celle per lato); al rilascio si **segnano** tutti i blocchi che lo toccano, si cancellano i piani e si annullano i cantieri dentro, una volta per blocco. Il rettangolo non toglie mai un segno di smontaggio, anche sopra blocchi già segnati o con due rettangoli sovrapposti; solo un clic destro singolo, senza trascinare, toglie un segno. Come per la linea, **Esc**, un pannello che si apre, la finestra che perde il fuoco o un rilascio fuori dalla finestra annullano il rettangolo senza smontare nulla.

Tasto **centrale**: un clic senza muovere il mouse **prende in mano** il blocco (o il cantiere) sotto il cursore, con la sua direzione e, per il selezionatore, il suo filtro; un blocco non ancora sbloccato dà un toast e lo strumento non cambia. Trascinato col centrale si sposta la vista. **Rotellina**: con in mano un blocco orientabile (nastri, sdoppiatore, selezionatore, ponte, estrattore, assemblatore) lo ruota come **R** (su in senso orario, giù al contrario; durante un trascinamento fissa la direzione come R); con **Ctrl**, col cursore o con un blocco senza direzione zooma.

**Trascinamento come Mindustry.** Col sinistro premuto, con qualunque strumento, si fissa l'ancora e si vede solo l'anteprima dall'ancora al cursore: nulla parte finché il tasto è giù. Nastri, incroci, sdoppiatori e selezionatori fanno una **retta sull'asse dominante** (vince il più lungo fra orizzontale e verticale); ogni altro blocco è uno solo, sull'ancora. La **direzione viene dal trascinamento** per ogni blocco: ogni nastro guarda il successivo, l'ultimo prende quella del nastro su cui finisce (se c'è) e, accanto al CORE, guarda il CORE; un blocco solo guarda verso il cursore (il pennello resta quello scelto con R). Un clic usa la direzione di R. **R a metà** trascinamento gira il pennello e fissa quella direzione per tutta la linea fino al rilascio (una fila "di lato"). Con **Ctrl** i nastri seguono la catena esistente fra ancora e cursore (e la portano al tier in mano) oppure un percorso a scalinata che aggira gli ostacoli; senza percorso torna la retta. Le sagome rosse sono le celle che la linea già rifiuta: non si mandano, e se non parte nulla (un clic sul CORE, per esempio) un toast dice perché. Al **rilascio** la linea si ricalcola e tutti i piani partono insieme, in ordine dall'ancora; un rifiuto come le risorse insufficienti dà un solo toast per linea. **Esc** o il **destro** durante il trascinamento annullano la linea (lo strumento resta); anche un pannello che si apre (T, M, G, I), la finestra che perde il fuoco o un rilascio fuori dalla finestra la annullano senza piazzare nulla. **Ponte automatico**: col ponte sbloccato, una retta di nastri che incontra pareti, acqua profonda o edifici li scavalca con un ponte (fino a 4 celle); un ostacolo più lungo resta rosso. Nastro sopra nastro e incrocio automatico valgono anche dentro la linea (qui sotto).

**Incrocio automatico.** Trascinando una linea di nastri che attraversa un nastro perpendicolare e prosegue oltre, in quella cella va un **incrocio** al posto del nastro (si paga l'incrocio e il nastro viene rimborsato come uno smontaggio; il drone smonta il nastro e poi costruisce l'incrocio, e le risorse dell'incrocio devono esserci già), come in Mindustry: le due linee si attraversano senza mescolarsi. Succede solo dove l'incrocio fa lo stesso lavoro del nastro: un nastro dritto che riceve da un altro pezzo di logistica dietro di sé e passa a un pezzo di logistica davanti (non accanto al CORE, a un edificio o a un minatore che lo alimenta, non su curve o T, non su un nastro già segnato da smontare). Sopra sdoppiatori, selezionatori, ponti e incroci non cambia nulla; sopra nastri paralleli, con un clic singolo o sull'ultima cella del trascinamento non mette incroci. Annullare uno dei due piani (destro) li annulla entrambi. L'incrocio è veloce (4 oggetti/s per direzione), così non rallenta neanche i nastri espressi. Se l'incrocio non è ancora sbloccato in Ricerca, il nastro attraversato gira nella direzione della linea e le sue merci entrano di lato: la linea attraversata diventa un'unione (2→1, o 3→1 con un ingresso per lato), come in Mindustry. **Nastro sopra nastro**: un nastro dello stesso tipo piazzato sopra un nastro con un'altra direzione lo gira subito e gratis, tenendo gli oggetti; con la stessa direzione non succede nulla. Un nastro con un ingresso su entrambi i lati e nulla dietro si disegna a **T**.

**Coda nell'HUD**: nella colonna in alto a sinistra, sotto le risorse del CORE, un riquadro mostra il blocco in lavorazione con la sua barra (rossa se si smonta, ambra con "Manca: <materiale>" se il cantiere è fermo per le risorse) e "+N" piani in attesa. Un clic sposta la visuale sul cantiere. Il riquadro sparisce quando la coda è vuota. Se un piano non si può più costruire quando il drone arriva (cella occupata, blocco non sbloccato) un avviso dice il motivo.

Palette Mindustry (angolo basso-destra): griglia fissa di 4 colonne con icone da 32 px, senza testo né numeri, della stessa misura in ogni categoria (le caselle libere restano vuote); mostra solo i blocchi già sbloccati in Ricerca; rail categorie a icone (Strumenti / Logistica / Produzione / Potenza); hover/selezione apre il pannello info (nome, tasto rapido `[2]`, costi con icona barrata se mancano); `?` apre dettaglio.

HUD in alto a sinistra (stile Mindustry): pulsanti **Menu · Ricerca · Mercato · Campagna**, poi pannello CORE con **Fondi**, energia e solo le risorse che hai (icona + numero, nome al passaggio del mouse), **Potenzia CORE** con i costi, e gli obiettivi di campagna.

| Hotkey | Azione |
| --- | --- |
| Esc / \` | Cursore (nessun tool); con il cursore Esc salva la Continua e apre la Home |
| 1–9 | Nastro … Ponte (2 = Minatore) |
| M | Mercato (vendi stock) |
| G | Campagna (seleziona livello) |
| T | Ricerca (grafo icon-node) |
| R | Ruota direzione piazzamento |
| C | Cicla filtro selezionatore |
| RMB | Cancella un piano, annulla un cantiere o segna un blocco da smontare; clic singolo toglie il segno, trascinare segna soltanto (non è uno slot palette) |

**Home durante una partita** (Esc col cursore, o **Menu**): il primo pulsante è **Riprendi**, che torna alla partita così com'è (Continua, che ricaricherebbe l'ultimo salvataggio automatico, non c'è); anche **Esc** riprende. Gli altri pulsanti sono quelli di sempre. Nella Home iniziale, all'avvio, Esc (come **Esci**) chiede conferma con «Uscire dal gioco?»: **Invio** o **Esci** esce, **Esc** o **Annulla** torna alla Home.

Ponte: estremi 1×1, campata larga circa il 70% della cella, distanza 2–4, celle in mezzo libere (un nastro può passarci sotto).

**Nastro rinforzato** (categoria Logistica, si sblocca in Ricerca dopo il Nastro T2): veloce come il T2, ma come l'*Armored Conveyor* di Mindustry prende oggetti dagli altri nastri da ogni lato e dagli edifici (e da sdoppiatori, selezionatori, ponti e incroci) solo da dietro. Una linea rinforzata passa accanto a una fila di trivelle o macchine senza raccogliere niente e resta dritta, senza unirsi agli edifici ai lati; con i nastri si unisce come gli altri. Binari d'acciaio rivettati con il bordo e le frecce rossi.

## Grafica

Edifici, nastri, Core, drone e oggetti sono pixel-art nostra generata da `python tools/art/genera.py` (32 px per cella, mostrati ×2 senza filtro; vedi `tools/art/README.md` e l'ADR 0004). Gli edifici hanno parti animate: la trivella del minatore gira mentre estrae, il forno si accende, i bracci dell'assemblatore premono, il rotore del generatore gira finché c'è combustibile; il nucleo del Core pulsa piano. I nastri scorrono alla velocità del loro tier e gli oggetti sopra hanno il loro sprite a colori, senza tinta. Anche le icone dell'interfaccia (16 e 32 px), il logo, le caselle di spunta e il carattere pixel in `assets/ui/` sono generati da `tools/art`: nel gioco non ci sono asset di terzi (`assets/ATTRIBUTION.md`).

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

**Avvio.** Il gioco esportato parte a schermo intero alla risoluzione dello schermo; il mondo resta in scala 1:1, quindi uno schermo più grande mostra più mappa. Al primo avvio (nessun file di impostazioni) la Scala UI segue l'altezza dello schermo: 100% sotto i 1400 pixel, 150% sotto i 2000, 200% oltre. In sviluppo e nei test (editor, `dev.sh`, build di debug) parte in una finestra 1280×720 con l'interfaccia al 100%; una scelta salvata vale comunque. I playtest sono sempre in finestra 1280×720 al 100%, qualunque cosa dicano le impostazioni, così le catture restano confrontabili.

Nella sezione **Interfaccia** la **Scala UI** (cursore da 75% a 200%, a passi di 5; predefinita 100%) ingrandisce solo HUD, pannelli e suggerimenti: il mondo e la visuale restano come sono, e il clic cade sempre sulla cella sotto il mouse. Il testo pixel è più nitido a 100%, 150% e 200% (ogni pixel del carattere è un numero intero di pixel dello schermo); alle altre scale qualche riga di pixel è un po' più larga. Su una finestra 1280×720 tutto sta nella finestra fino al 150%; sopra, alcuni pannelli scorrono o escono un poco dai bordi. I vecchi valori (100, 125, 150, 200) restano validi.

**Colori** (Normali / Daltonici / Alto contrasto) cambia subito i colori che hanno un significato: anteprima di piazzamento valida o rifiutata, costruzione e smontaggio, energia e denaro, rifiuti e materiali mancanti, linee della Ricerca. Daltonici usa tinte sicure per deuteranopia e protanopia (azzurro e vermiglio, giallo e porpora); Alto contrasto ha pannelli neri, testo bianco, bordi chiari e segni nel mondo giallo puro e rosso magenta con un bordo nero, leggibili su ogni terreno (lo smontaggio usa lo stesso rosso del rifiuto: lo distingue il tratteggio dalla X). Il significato non sta mai solo nel colore: l'anteprima rifiutata ha una X (e in basso il motivo, per esempio "Serve il Minatore T2 per il titanio"), i blocchi da smontare e quelli che il drone sta smontando sono tratteggiati, un materiale o il denaro mancante è barrato, l'energia bassa ha un "!" e un prezzo in calo una freccia.

**Lingua** (Italiano / English) cambia subito tutti i testi, anche nei pannelli aperti: nomi dei blocchi e degli oggetti, livelli e obiettivi della campagna, rifiuti ed errori dei salvataggi. Predefinito italiano.

**Home.** Il logo pixel è generato da `tools/art` (`ui/ui-logo`); dietro la Home la mappa scorre piano da sola e non risponde a mouse e tastiera (nei playtest resta ferma).

## Prove automatiche

```bash
tools/dev/playtest.sh ui /tmp/shots-ui          # oppure: tutorial, agenti
python tools/dev/compare_shots.py <baseline> <nuovi>
```

Il playtest `agenti` prova il canale degli agenti (`AgentBridge`, `godot/scripts/dev/`): `playtest.sh` sceglie una porta libera e un segreto, e un client dentro il playtest gioca come un agente (stato, piazza, avanza, `ui` e clic, tasti, rotella, telecamera, cattura su file, nella risposta e a 1920×1080, log, richieste in raffica durante `esegui`), controllando che senza segreto o con una richiesta da browser il canale chiuda la connessione. Il canale esiste solo nelle build di sviluppo, con `TINDUSTRY_AGENT_PORT`, `TINDUSTRY_AGENT_TOKEN` e un profilo temporaneo (`TINDUSTRY_DATA_DIR` fuori dai dati veri), e ascolta solo su `127.0.0.1`: nel gioco esportato non si apre mai. Gli agenti lo usano attraverso il server MCP di `tools/AgentPlay` (vedi il suo README).

`tools/dev/playtest.sh` lancia il gioco con `TINDUSTRY_PLAYTEST=ui|tutorial`, input simulati e un profilo dati temporaneo (`TINDUSTRY_DATA_DIR`): non tocca impostazioni e salvataggi reali. Salva un PNG per passo e `log.txt` nella cartella indicata; esce con 0 solo se il log contiene `PLAYTEST OK` e nessun `ERROR`. Con `TINDUSTRY_PLAYTEST_PERF=1` il playtest `ui` posa anche 200 nastri e 100 edifici a strati al lavoro (forni, assemblatori, generatori, minatori) e stampa FPS medio e minimo e numero di nodi (`PLAYTEST perf: …`), senza esito. Con `TINDUSTRY_PLAYTEST_BLOCCHI=1` costruisce ogni blocco di produzione e potenza spento e acceso e salva `ui-blocchi-spenti.png`, `ui-blocchi-accesi.png`, `ui-blocchi-zoom04.png`, `ui-blocchi-core.png` (Core e drone da vicino) e `ui-blocchi-oggetti.png` (ogni oggetto su ogni nastro), senza esito. Nei playtest gli effetti, le ombre, il bagliore e la notte sono spenti e gli orologi fermi, così le catture sono stabili; con `TINDUSTRY_PLAYTEST_EFFETTI=1` il playtest `ui` li accende e aggiunge il probe di G3 (60 secondi con effetti Alti, FPS, nodi e particelle, ombre, bagliore, notte con Lampade, scala di rendering) con le sue catture `fx-*` e `luci-*`, senza esito sulle immagini. Il playtest `ui` prova anche la Scala UI (catture `ui-scala-<percento>-*`): a 100, 125, 150 e 200% apre ogni pannello e controlla che nessun testo esca dal suo rettangolo e che i pannelli stiano nella finestra (obbligatorio fino al 150%), che il mondo non cambi e che a 200% un clic col mouse pianifichi il nastro proprio sulla cella sotto il cursore; con `TINDUSTRY_PLAYTEST_SCALA=<percento>` un playtest intero gira a quella scala. Con `TINDUSTRY_PLAYTEST_LINGUA=en` un playtest intero gira in inglese (il controllo dei rettangoli misura i testi tradotti); il playtest `ui` prova anche il cambio di lingua, le tre varianti di colore e la Home. I playtest sono sempre in una finestra 1280×720 al 100%. `compare_shots.py` confronta due cartelle di screenshot ignorando la zona FPS.

Lanciando Godot a mano, `TINDUSTRY_PLAYTEST` richiede `TINDUSTRY_DATA_DIR`; `TINDUSTRY_CAPTURE_DIR` cambia dove vanno gli screenshot (default `godot/artifacts/`).

## Save / load

| Azione | UI | Hotkey | Slot file |
| --- | --- | --- | --- |
| Salva continua | chip **F5** | F5 | `continua.json` |
| Carica continua | chip **F9** | F9 | `continua.json` |
| Salva slot | chip **F6** | F6 | `slot-1.json` |
| Carica slot | chip **F7** | F7 | `slot-1.json` |

Path: `%LocalAppData%/tIndustry/godot-saves/` (Linux: `~/.local/share/tIndustry/godot-saves/`), oppure `godot-saves/` dentro `TINDUSTRY_DATA_DIR` se impostata. Formato alla versione 13 (metadati + istantanea, con il generatore della mappa e i cantieri di sostituzione dei nastri): i salvataggi v12 e v11 si caricano ancora, i v11 sul terreno di prima; i più vecchi non sono compatibili. **Gestione salvataggi** mostra per ogni slot il tipo di mappa ("Biomi · 1500 · seme N", "Mappa <livello> · W×H" o "Classico · W×H · seme N").  
Persists belts (+items, bridge partner, sorter filter), buildings, wallet, nextItemId, core, regole di partita, drone con la coda dei piani e cantieri a metà.

## Projects

| Path | Role |
| --- | --- |
| `godot/` | **Official** playable client |
| `src/TIndustry.Shared/` | Factory sim + content + save (shared) |
| `data/` | Contenuti (`content.json`, `campaign.json`), unica fonte |

Mappa completa: [`docs/ARCHITETTURA.md`](../docs/ARCHITETTURA.md).

## Ricerca

**T** apre la Ricerca, un albero stile Mindustry: **CORE in cima**, ogni blocco sotto il suo prerequisito (linee a gomito; prerequisiti aggiuntivi tratteggiati). Bordo dorato = puoi sbloccarlo ora, lucchetto = prima sblocca il precedente; passa col mouse per costo e mancanze, click per il pannello di sblocco. Trascina per muoverti, rotella zoom, H vista intera. Sblocca strutture spendendo denaro/materiali; place tools 3–9 restano gated. Salvataggio v3 include `unlockedStructures` + vendite Mercato.

## Mercato

**M** apre il Mercato: vendi stock del Core (1 / tutti) a prezzo dinamico (scende con stock alto). Il pannello Core mostra `$`. Necessario per guadagnare denaro e sbloccare in Ricerca.

## Campagna

**G** apre la selezione livelli. Obiettivi in alto a sinistra (`OBIETTIVO`). Completando un livello si sblocca il successivo. Continua salva `activeCampaignLevelId` (save v4).
