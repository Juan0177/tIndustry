# Sito: racconto guidato dallo scroll — piano e requisiti

Stato: **prototipo dei capitoli 1-4 pubblicato** in `racconto/` (giacimento, nastri, forno, lastre, corrente, sera), con l'anticipo del capitolo 5; mancano attacco, mondo e CORE. Data: 20 ottobre 2026.

## Cosa chiede l'utente

Effetti di scroll «della branca» di [endurosat.com/space-service](https://www.endurosat.com/space-service/): lo scorrimento non sposta solo la pagina, **fa avanzare una storia**. Oggi il sito ha un solo momento così (la hero agganciata); il resto sono sezioni che scorrono con qualche comparsa.

## Cosa fa la pagina di esempio (misurato sul suo codice)

- Una **scena 3D fissa a tutto schermo** (un `<canvas>` WebGL con Three.js: modelli GLTF compressi con Draco, texture KTX2) resta dietro a tutta la pagina.
- Sopra scorrono **«fotogrammi» alti quanto lo schermo** (`section.fps-frame`, una ventina): ciascuno è un capitolo con un titolo e poche righe.
- **GSAP ScrollTrigger** lega ogni capitolo a una posizione di scroll: *scrub* (l'animazione segue lo scroll, avanti e indietro, invece di partire da sola) e *pin* (un pezzo resta fermo mentre lo scroll consuma l'animazione). La camera si sposta, il satellite ruota e si compone, i testi entrano ed escono.
- **Lenis** rende lo scorrimento morbido (interpola la rotellina).

La branca è quella dello *scrollytelling* o delle *scroll-driven animations*: **la posizione di scroll è la linea del tempo**. Gli strumenti sono sempre gli stessi: scrub, pin, parallasse, comparse, sequenze di fotogrammi, una scena persistente che cambia inquadratura.

## Il concetto per tIndustry: «dal giacimento al CORE»

Una **sola fabbrica a tutto schermo**, fissa dietro alla pagina, disegnata con gli sprite veri. Scorrendo, **la camera segue il minerale** dal giacimento al CORE e la fabbrica **si costruisce sotto gli occhi** del visitatore. I testi sono pannelli che entrano a sinistra o a destra, come i capitoli dell'esempio.

| Capitolo | Cosa succede scorrendo (scrub, reversibile) | Testo |
|---|---|---|
| 1. Il giacimento | Camera stretta su un giacimento di ferro; il drone arriva e costruisce il minatore; la lampada si accende. | Logo, una frase, «Scarica». |
| 2. I nastri | Il drone posa i nastri cella per cella, curva compresa; gli oggetti partono quando il tratto è chiuso. | Come funziona la logistica. |
| 3. Il forno | La camera scivola al forno: carbone da un lato, ferro dall'altro, lastre in uscita. | Ricette, forno multi-ricetta. |
| 4. La corrente | Generatore, nodi e lampade: le linee si disegnano una dopo l'altra (tratteggio che si riempie) e gli edifici si illuminano. | Energia. |
| 5. Sotto attacco | La camera si allarga di colpo, bordo rosso, sagome nemiche ai margini (il teaser di oggi, integrato); al drone cade in testa un elmetto. | Unità in arrivo. |
| 6. Il mondo | Zoom fuori su tutta la mappa: si vedono i biomi, i fiumi, le altre catene. | Mappe generate dal seme, campagna. |
| 7. Il CORE | La camera torna sul CORE, le due catene convergono nell'unione a T, il grafico del Mercato sale. | Scarica, requisiti. |

Sotto il racconto restano, come pagina normale, catture, blocchi e interfaccia (con la finestra delle catture già fatta).

## Come lo farei

### Motore: nostro, piccolo, senza dipendenze (consigliato)

Il sito ha già la base: `sito.js` scrive una variabile di avanzamento `--p` per sezione a ogni fotogramma. La estenderei con:

- **Linea del tempo**: una lista di capitoli con inizio e fine in altezze di schermo; per ciascuno, fotogrammi chiave della camera (x, y, zoom) e degli eventi (blocco costruito al 30 %, linea disegnata al 60 %). Funzioni pure `capitolo(scrollY)` e `interpola(chiavi, t)`, testabili con `node --test`.
- **Disegno su `<canvas>` 2D**: uno strato di terreno pre-renderizzato una volta, sopra blocchi, nastri e oggetti disegnati a ogni fotogramma da un unico atlante di sprite. Zoom a passi interi (1×, 2×, 3×) per tenere i pixel netti, `imageSmoothingEnabled = false`.
- **Dati dalla stessa fonte di oggi**: `fabbrica.py` esporta la scena (celle, blocchi, nastri, ricette) e la linea del tempo in JSON; uno script Pillow compone l'atlante dagli sprite di `godot/assets/` (regola dell'arte generata rispettata).
- **Scroll nativo**: niente dirottamento della rotellina. Lenis (licenza MIT, circa 10 KB) solo come prova facoltativa su desktop, mai su touch.

### Alternative valutate

| Opzione | Pro | Contro |
|---|---|---|
| GSAP + ScrollTrigger | Standard del settore, scrub e pin già pronti, molto rodato. | Licenza gratuita ma non open source (la «Standard No Charge»), altri 100 KB da includere nel repository. |
| Animazioni CSS legate allo scroll (`animation-timeline: scroll()/view()`) | Zero JavaScript, fluide. | Firefox non le aveva ancora attive di serie (da ricontrollare quando si parte); non bastano per una camera su canvas. Utili per comparse e testi. |
| PixiJS (WebGL) | Migliaia di sprite senza fatica. | Circa 450 KB; il canvas 2D basta per una fabbrica di qualche migliaio di celle. |
| 3D come l'esempio (Three.js) | Effetto «wow» immediato. | Il gioco è pixel-art 2D: un 3D tradirebbe lo stile e richiederebbe modelli da fare. |

## Requisiti

### Funzionali

- Canvas fisso a tutto schermo dietro ai capitoli; ogni capitolo alto almeno uno schermo; scrub reversibile (tornando su la fabbrica si «smonta»).
- Il testo resta nel DOM (non dentro il canvas): leggibile, selezionabile, tradotto, indicizzabile.
- Le ancore della barra portano all'inizio di ogni capitolo con l'inquadratura giusta.
- La fabbrica rispetta il gioco come oggi: curve, ricette, durezze e velocità vere; i test di connettività valgono anche per la scena grande.

### Movimento e accessibilità

- Pulsante «Animazioni»: in pausa il canvas mostra un'immagine ferma per capitolo (fotogrammi chiave pre-renderizzati) e niente camera in movimento.
- Preferenza di sistema `prefers-reduced-motion`: come deciso il 20 ottobre, la fabbrica si muove lo stesso; spariscono lampi, scossa e zoom bruschi (il capitolo 5 usa uno stacco invece di uno zoom).
- Nessun lampo sopra le tre volte al secondo; testo sempre sopra un fondo che dà contrasto (velo scuro sotto i pannelli).
- Senza JavaScript: la pagina di oggi, ferma ma completa.

### Prestazioni

- 60 fotogrammi al secondo su un portatile medio; meno di 4 ms di script per fotogramma; nessun lavoro quando la scheda è nascosta o il canvas è fuori vista.
- Contenuto visibile entro 2 secondi; peso totale sotto i 3 MB (atlante in PNG ottimizzato, un solo file).
- Densità di pixel dello schermo gestita (canvas alla risoluzione reale, sprite netti).

### Telefono

- Stessa storia in verticale: inquadrature per proporzione dello schermo (la camera stringe di più), pannelli di testo in basso.
- Scroll touch naturale, nessuna interpolazione aggiunta.

### Verifica

- Test Node per linea del tempo, interpolazione e limiti della camera.
- Playwright: catture a posizioni di scroll fisse (deterministiche: tempo bloccato con un parametro di prova) a 1920, 1280 e 390 px, confrontate col riferimento (`tools/dev/compare_shots.py`).
- Controllo di peso e di errori di console nel workflow Pages.

## Fasi

1. **Prototipo** (un capitolo: giacimento, minatore e primo nastro con camera e scrub) da far provare all'utente per approvare la sensazione. È il passo che decide tutto il resto.
2. Motore, esportazione dei dati e atlante, con i test.
3. I sette capitoli e i testi.
4. Telefono, stato fermo, senza script.
5. Rifinitura, misure di prestazioni, catture di riferimento, documenti e diario.

## Decisioni

Prese con l'utente il 20 ottobre (il lavoro non è ancora partito):

- **Mappa dedicata al racconto**, scritta a mano per le inquadrature, non una mappa della campagna. Va comunque controllata dai test di connettività, ricette e durezze come le scene di oggi.
- **Scroll morbido**: si usa Lenis (licenza MIT) su desktop. Su touch resta lo scroll del telefono, che è già morbido di suo. La tastiera (frecce, Pagina giù, Spazio) e le ancore della barra devono continuare a funzionare; con «Animazioni» in pausa lo scroll morbido si spegne.

- **Strada A, sostituire**: hero, Come funziona, Sotto attacco e Scarica diventano capitoli del racconto; sotto restano come pagina normale Catture, Blocchi, Ambienti e Interfaccia.
- **Il drone è il filo del racconto, in 2.5D, con l'elmetto nell'attacco.** Il drone segue il visitatore e costruisce la fabbrica capitolo dopo capitolo; nel capitolo «Sotto attacco» gli compare sopra un elmetto, e se ne va quando l'attacco finisce. Per portare l'elmetto in modo credibile il drone del racconto diventa 2.5D.

### Il drone 2.5D (proposta tecnica)

- **Sprite impilati** (*sprite stacking*, la tecnica classica del pixel-art in 2.5D): il drone è una pila di 8-12 fette orizzontali di pixel-art disegnate una sopra l'altra, ciascuna spostata di un pixel verso l'alto. Ruotando le fette insieme il drone gira su se stesso con volume vero, inclinandosi in volo e nelle curve, senza modelli 3D e restando pixel-art.
- **Ombra a terra** separata, che si allontana quando il drone sale e si stringe quando scende: dà la quota.
- **Elmetto** come altre 3-4 fette in cima alla pila: quando compare cade dall'alto e si posa con un piccolo rimbalzo; finito l'attacco si solleva e sparisce.
- Fette, ombra ed elmetto si generano con `tools/art/genera.py` come ogni altro sprite (regola dell'arte generata da script), a partire dal `drone-1` del gioco, così il drone del sito resta riconoscibile.
- Con «Animazioni» in pausa il drone è un'immagine ferma (una vista di tre quarti, con l'elmetto nel capitolo dell'attacco).
- Il drone del gioco resta com'è: il 2.5D riguarda solo il sito, salvo decisione diversa.
Nessuna domanda aperta: si parte quando l'utente lo dice, dal prototipo di un capitolo (giacimento, minatore, primo nastro, con il drone 2.5D).

## Prototipo del capitolo 1 (20 ottobre)

Pagina `racconto/` del sito, fuori dalla barra della vetrina e con `noindex`: cinque pannelli alti uno schermo sopra un canvas fisso.

- **Dati**: `tools/sito/racconto.py`. Mappa di 40×24 celle (prati): giacimento di ferro, minatore, curva in uscita, 3 nastri giù, curva, 12 nastri verso il piano del forno del capitolo 2; la controlla `fabbrica.controlla` come la vetrina (`Scena` ha ora una `larghezza`). Linea del tempo: piano del minatore a 0,08, cantiere da 0,20 a 0,34, nastri uno dopo l'altro da 0,42 a 0,86, piano del forno da 0,80; sei fotogrammi chiave della camera.
- **Immagini** in `racconto/`: terreno con i giacimenti, strisce dei nastri e delle curve (32 fotogrammi), minatore al lavoro, piani come lo shader `construct` del gioco (sagoma scura, contorno ambra), oggetto, drone a fette con l'ombra, poster per chi non ha lo script. Tutto derivato dagli sprite del gioco e deterministico.
- **Motore**: `tools/sito/racconto.js`, canvas 2D senza dipendenze. Lo scroll è la linea del tempo (scrub: tornando su la fabbrica si smonta); il tempo muove solo battistrada, oggetti, minatore, volo del drone e raggio. Cantiere che sale dal basso con la linea di scansione ambra, oggetti solo sulle celle finite, drone che vola alla prossima costruzione e la costruisce col raggio.
- **Drone 2.5D**: la prima versione estrudeva la sagoma (7 fette uguali scurite) e sembrava solo «più spessa», come ha notato l'utente. Ora `racconto.modello_drone` fa del `drone-1` un piccolo modello a volumi, con le parti riconosciute dai colori dello sprite: scafo a cupola (più alto verso il centro), sensore arancione rialzato, prese d'aria a gradini (bordo, fascia interna, mozzo) staccate dalla pancia, moncone di coda basso; 6 strati ingranditi ×4 e disegnati più piccoli con il filtro, come `DroneVisual`. In volo il drone si inclina in avanti (gli strati scivolano lungo la rotta); ombra al 35 %, galleggiamento di ±0,05 celle, bagliore del motore in coda dietro lo scafo. Niente eliche, per scelta dell'utente.
- **Scroll morbido**: scritto nel motore (approccio esponenziale alla meta della rotellina, la stessa tecnica di Lenis) invece di includere Lenis: niente file di terzi da tenere. Solo con la politica di movimento piena (`html.animato`); tastiera, barra di scorrimento, touch e ancore restano del browser.
- **Telefono**: in verticale la camera mostra il 60 % delle celle e mette il punto d'interesse al 36 % dell'altezza, sopra i pannelli.
- **Pausa** («Animazioni»): il tempo si ferma (canvas immobile), lo scroll muove ancora il racconto ma senza scorrimento morbido. Con `prefers-reduced-motion` resta tutto acceso tranne lo scroll morbido.
- **Scostamento**: le fette del drone sono derivate da `drone-1` al momento di costruire il sito (`racconto.fette_drone`), come le composizioni di `composti.py`, invece di stare in `tools/art/genera.py`. L'elmetto del capitolo 5, che è disegno nuovo, andrà in `genera.py`.
- **Verifica**: `test_racconto.py` (mappa, linea del tempo, piani, esportazione deterministica), `test_racconto.mjs` (funzioni pure del motore), `test_costruisci.test_racconto` (pagina); Chromium a 1576×900 e 390×844 su nove posizioni di scroll, rotellina, tastiera, pausa, riduzione del movimento, senza script; nessun errore di console.

Da decidere dopo la prova: ritmo dei capitoli (quanto scroll per capitolo), dimensione del drone (oggi quella del gioco), quanto si vede il piano del forno.

## Capitoli 2-4 (20 ottobre, «fai anche la seconda e terza sezione fino all'attacco»)

- **Mappa** (`racconto.SCENA`, 40×24, controllata da `fabbrica.controlla`): al minatore del ferro si aggiungono un giacimento di carbone con il suo minatore, che dal lato est porta il carbone al forno (curva, tre nastri, curva) e dal lato nord al generatore (curva, quattro nastri); le lastre escono dal forno verso est e scendono fuori dalla mappa, verso i capitoli che verranno; tre nodi e quattro lampade.
- **Linea del tempo** a fasi (`FASI`), otto pannelli: minatore del ferro, nastri del ferro, forno, minatore del carbone, nastri del carbone, nastri delle lastre, generatore, nastri del generatore, nodi, lampade. Ogni blocco ha un istante in cui si accende: i minatori appena costruiti, il forno quando gli arrivano ferro e carbone, il generatore quando gli arriva il carbone, le lampade quando la loro linea è tesa. Gli oggetti corrono su quattro catene, ciascuna solo dove è costruita e solo da quando la sua sorgente lavora.
- **Corrente**: sette linee del colore `UiColors.Energia` che si stendono da un estremo all'altro dopo che entrambi sono in piedi.
- **Sera** come il `NightLayer` del client: il mondo moltiplicato per il suo blu scuro, poi luce calda sommata attorno alle lampade (6,5 celle) e alla bocca del forno (3 celle).
- **Anticipo dell'attacco**: tre sagome nere di unità nemiche, bordate di rosso, entrano dal margine destro e un bagliore rosso lento (un respiro ogni 2,5 s, niente lampi) chiude il racconto.
- **Camera**: dodici fotogrammi chiave; la vista resta sempre dentro la mappa (`limitaCamera`) e non scende mai sotto la mappa intera, così il telefono non mostra fasce nere.
