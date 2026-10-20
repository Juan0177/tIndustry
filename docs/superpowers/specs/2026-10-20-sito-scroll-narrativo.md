# Sito: racconto guidato dallo scroll — piano e requisiti

Stato: **proposta**, da approvare prima di scrivere codice. Data: 20 ottobre 2026.

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
| 5. Sotto attacco | La camera si allarga di colpo, bordo rosso, sagome nemiche ai margini (il teaser di oggi, integrato). | Unità in arrivo. |
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

## Decisioni da prendere con l'utente

- Una mappa scritta a mano per il racconto (consigliato: controllo totale delle inquadrature) o una mappa vera della campagna.
- Se il racconto sostituisce le sezioni di oggi in alto o si aggiunge sopra di esse.
- Se provare Lenis (scorrimento morbido) o restare con lo scroll del browser (consigliato).
