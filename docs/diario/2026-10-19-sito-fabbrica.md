# Sito «fabbrica»

- **Specifica**: [2026-10-19-sito-fabbrica-design](../superpowers/specs/2026-10-19-sito-fabbrica-design.md)
- **Piano**: [2026-10-19-sito-fabbrica](../superpowers/plans/2026-10-19-sito-fabbrica.md)
- **Periodo**: 2026-10-19
- **Esito**: in `main` con la PR [#PR](https://github.com/Juan0177/tIndustry/pull/PR), pubblicato su [juan0177.github.io/tIndustry](https://juan0177.github.io/tIndustry/) col workflow Pages lanciato a mano.

> Scritto a fine lavoro dalla sessione che ha fatto tutto (brainstorming, specifica, piano, implementazione, revisione), dai risultati dei due workflow di agenti (moduli in parallelo e revisione a più lenti) e dalla storia git. Dove le fonti non dicono qualcosa qui non è inventato.

## Obiettivo

Rifare da zero il sito di GitHub Pages come **una fabbrica di tIndustry** per attirare chi gioca ai factory game e agli RTS: più catene di produzione vere che scorrono sui nastri del gioco fino al Core col bottone di download, una pagina sullo sviluppo e la pagina degli asset nel guscio nuovo. Tutto generato dal repository, fedele al gioco cella per cella, senza caricamenti esterni. Punto di partenza: il brief di ripartenza del 19 ottobre e la specifica del 18, riconfermata punto per punto con l'utente.

## Come è andata

1. **Correzione preliminare** (indicata dal brief): `composti.py` dava per scontati 8 fotogrammi per cella, con le strisce a 32 i nastri della pagina asset andavano a un quarto della velocità. Ora il numero di fotogrammi si legge dallo sprite, con un test. Commit `cf5d10a`.
2. **Brainstorming con l'utente** su una bozza visiva pubblicata come artifact (sprite veri, nastri animati, blocchi, Core), tre giri: più catene e non tutte verso il Core (mix «ogni sezione la sua» e «catene che si intrecciano», una nel Core); via le monete, al loro posto lo schermo del Mercato col grafico in loop; fedeltà (nastro che esce dal minatore con la curva, minatore alimentato con la linea, generatore col carbone, nodi collegati a una fonte); scroll «stile Apple» (hero e striscia agganciate, cornici CRT che si accendono, parallasse); sezione teaser delle unità con avvisi neon, bordo rosso e vetro sparato. Poi carta bianca sui miglioramenti.
3. **Specifica e piano** scritti dalla sessione (approvazione conversazionale: l'utente ha chiesto di procedere senza altri passaggi).
4. **Implementazione** in una sessione sola: `fabbrica.py` e `costruisci.py` dalla sessione (modello a celle con controllo di connettività, che ha subito trovato una curva specchiata nella bozza); `documenti.py` con la pagina sviluppo e `sito.js` col drone affidati a due agenti in parallelo (workflow), con contratto nella specifica. Commit `1de20f9`, `941992e`.
5. **Revisione** con un workflow a otto lenti (visiva desktop, visiva telefono/senza script/movimento ridotto, fedeltà al gioco, codice Python, codice JS/CSS, testi per il pubblico, completezza rispetto alla specifica, peso e pubblicazione), verifica avversaria di ogni segnalazione con due giudici, correzioni per gruppo con i test, secondo giro. Vedi sotto.
6. **Documenti e pubblicazione**: README, CHANGELOG, ARCHITETTURA, mappa Obsidian (`695a8ab`), questo diario; PR, merge in `main`, workflow Pages.

## Task, commit e PR

Un solo ramo (`claude/exciting-ride-dmceyv`) e una sola PR con un commit per pezzo di lavoro: deroga, chiesta dall'utente («fallo e pubblicalo»), alla regola «una PR per task».

| Task | Contenuto | Commit |
|---|---|---|
| 0 | Nastri della pagina asset a un fotogramma per pixel; `.gitignore` per `__pycache__` | `cf5d10a`, `af123f9` |
| 1 | `fabbrica.py` (modello, scene, immagini in `fabbrica/`, connettività), `test_fabbrica.py` | `1de20f9` |
| 2 | `documenti.py` e `test_documenti.py` (agente) | `1de20f9` |
| 3 | `sito.js`, `drone.css`, `test_sito.mjs` (agente), adattamento dello zoom | `1de20f9` |
| 4 | Guscio, `stile.css`, vetrina, sviluppo, asset, `test_costruisci.py`, `pages.yml` | `1de20f9`, `941992e` |
| 5 | Revisione a più lenti e correzioni | REVISIONE |
| 6 | Documenti e diario | `695a8ab`, DIARIO |

## Giri di revisione

REVISIONE_DETTAGLIO

## Decisioni prese in autonomia

- **Rendering CSS invece del canvas** — il modello della fabbrica è a griglia come previsto (approccio 1), ma la bozza ha mostrato che i nastri si disegnano bene con lo sfondo di un fotogramma fatto scorrere in CSS (un fotogramma per pixel: scorrere di una cella in 1/velocità secondi *è* la striscia) e gli oggetti con animazioni CSS; funziona anche senza script — costo se sbagliata: con molti più pezzi animati il CSS pesa; si può tornare al canvas tenendo il modello.
- **Immagini delle scene come file (`fabbrica/`)** invece di data URI — le pagine restano piccole (vetrina 55 KB) e il browser le mette in cache — costo: più richieste HTTP, tutte dello stesso dominio.
- **Pavimento di 1152 px ridotto in scala** (`zoom`) sotto i 1184 px e contenuti in colonna sotto gli 864 px — è la soluzione più robusta per un pavimento disegnato in pixel — costo: fra 864 e 1184 px il testo si riduce fino al 75 %.
- **Hero e sfondo**: cattura `gioco.png` ritagliata senza HUD, al posto del banner del README che portava scritte sotto il logo.
- **Schede dei contenuti accorciate** (fino al primo «;» e alla virgola che ci sta in 110 caratteri) invece del testo intero del README, che non stava nelle schede.
- **Riga dei numeri del gioco** in «Blocchi» (blocchi, tier, ricette, minerali, biomi, livelli), presi da `data/`, per riempire la parte bassa della sezione che l'aggancio della striscia lascia vuota.
- **Pagina sviluppo**: il paragrafo «Obiettivo» del diario «Agenti che giocano» cita un assistente AI; viene dal documento sorgente, non dal generatore, e resta com'è.
- **`fetch-depth: 0` nel workflow Pages** perché `git describe` trovi il tag per il bottone di download; senza tag il bottone dice «Scarica l'ultima release».

## Problemi rinviati

- Versione inglese del sito, feed, changelog completo, pagina delle release: fuori ambito per scelta.
- Il nastro rinforzato (`conveyor-armored`, piano «nastri come Mindustry» Task 10) comparirà nella pagina asset da solo quando arriverà (i tier si leggono da `content.json`); la vetrina usa T1 e T2 e andrà ritoccata a mano per mostrarlo.
- Catture nuove per il sito (basi grandi, Core, oggetti) non fatte: nel cloud non c'è Godot; le cornici usano le catture del README.

## Verifiche finali

VERIFICHE
