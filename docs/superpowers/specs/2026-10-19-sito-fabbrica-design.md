# Sito «fabbrica» — vetrina, sviluppo e asset su GitHub Pages

- **Stato**: **superata** (2026-10-20): il lavoro sul sito è andato avanti col [racconto guidato dallo scroll](2026-10-20-sito-scroll-narrativo.md) (PR #249–#251), che fa da riferimento; questa specifica resta come storico. Approvata il 2026-10-19 (brainstorming con l'utente su una bozza visiva pubblicata come artifact, tre giri di correzioni; l'utente ha dato carta bianca sui miglioramenti).
- **Sostituisce**: il sito di oggi (`tools/sito/costruisci.py`: ingresso con le catture del README e pagina asset) e la specifica del 18 ottobre (rimasta sul ramo `claude/ripresa-task-notturni-5ht4tf`, mai unita: le sue decisioni sono state riconfermate una per una e riportate qui).
- **Pubblicazione**: invariata, `.github/workflows/pages.yml` a mano da `main` o sui tag `v*`.

## Obiettivo e pubblico

Una **landing page che attiri chi gioca ai factory game e agli RTS** (Mindustry, Factorio, Shapez), non una pagina di vendita: il gioco è gratuito e open source. La vetrina *è* una fabbrica di tIndustry: più catene di produzione vere scorrono sui nastri veri del gioco, con curve, unioni, incroci, ponti, selezionatore e sdoppiatore, fino al Core in fondo dove sta il bottone di download. La fabbrica deve essere impeccabile: ogni pezzo come in partita (minatore alimentato → lampada accesa → linea di corrente; forno a carbone o corrente; generatore col combustibile; nodi che risalgono a una fonte; nastro che esce dal minatore con la curva).

Tre pagine: la vetrina (`index.html`), lo sviluppo (`sviluppo/`: com'è fatto, roadmap, diario, decisioni) e gli asset (`asset/`, i contenuti di oggi nel guscio nuovo). Tutto generato dal repository, mai scritto a mano, senza caricamenti esterni.

## Decisioni prese con l'utente

| Tema | Decisione |
|---|---|
| Struttura | Pagina lunga a sezioni: Hero, Come funziona, Catture, Blocchi come in gioco, **Sotto attacco** (teaser), Ambienti, Interfaccia e accessibilità, Scarica, piè di pagina. |
| Catene | **Più catene, non una sola**: ogni sezione ha un ramo corto che finisce in un blocco della sezione; **due catene lunghe** attraversano la pagina ai due lati e **convergono sul Core** in Scarica (mix delle opzioni «ogni catena finisce nella sua sezione» e «le catene si intrecciano»). Niente nastro delle monete: al suo posto **uno schermo del Mercato** collegato al Core, col grafico dei prezzi che sale e scende in loop. |
| Fedeltà | Ricette vere di `data/content.json`. Un minatore con la lampada accesa è alimentato e ha la sua linea; il nastro esce dal minatore con una curva; il forno riceve il carbone; il generatore riceve il carbone da uno sdoppiatore; i nodi sono collegati in catena a una fonte (linea elettrica lungo il margine destro, dal generatore in «Come funziona» fino allo schermo del Mercato). |
| Veste | Industriale scura; effetto **CRT** sulle catture (scanline, vignetta); **strisce gialle e nere da cantiere** su barra, divisori e bottone; logo `ui-logo` come titolo; **carattere pixel del gioco** per i titoli (immagini generate da `carattere.scrivi`); etichette monospazio numerate. |
| Hero | **Alta quanto lo schermo e agganciata**: scorrendo lo sfondo si avvicina e scurisce, il logo si ritira, frase e bottoni salgono. Minatore T1 su un giacimento di ferro in basso a sinistra avvia la dorsale. |
| Scroll | Effetti guidati dallo scorrimento (stile Apple): hero agganciata; griglia dei blocchi alla scala del gioco che entra riga per riga (la prima striscia agganciata che scorreva di lato è stata tolta il 20 ottobre); titoli che entrano, schede che salgono, **cornici CRT che si accendono** entrando in vista; **parallasse** del terreno; **barra di avanzamento** a strisce in cima. Con script spenti la pagina è ferma ma completa (i nastri girano lo stesso: sono GIF). Le animazioni sono accese per tutti: il pulsante «Animazioni» nella barra le ferma e le riavvia (scelta ricordata in `localStorage`); `prefers-reduced-motion` da solo spegne soltanto lampi, scossa, sfarfallio e vetro rotto di «Sotto attacco» e tiene il drone a terra. |
| Sotto attacco | Sezione teaser delle **unità di attacco e difesa in arrivo**: fondo nero con disturbo video, titolo pixel con sbavatura cromatica che sfarfalla, sagome nere che si scoprono per un istante, **bordo rosso neon** pulsante su tutto il perimetro, **avvisi neon pixellati** («ATTENZIONE», «PERICOLO», «UNITÀ OSTILI IN AVVICINAMENTO», «DIFESE NON DISPONIBILI», «INTRUSIONE RILEVATA») che compaiono e spariscono in punti diversi, e ogni 6–8 s un **impatto da vetro sparato** (lampo, scossa, ragnatela di crepe in SVG che resta e svanisce). Tutto CSS, deterministico. |
| Drone | `drone-1` vaga per la vetrina; al clic su un link con `data-costruisci` vola sul bottone, lo **costruisce col raggio** (riempimento da cantiere, 0,5 s) e poi si va alla pagina. Non si intercettano tasto centrale, modificatori, script spenti, link senza l'attributo. Il drone vola solo con le animazioni al completo: con `prefers-reduced-motion` resta a terra, a meno che il visitatore non prema «Animazioni»; lo stesso pulsante ferma tutto, drone compreso. |
| Potenza | Generatore, nodi e lampade sparsi, linee di corrente del colore del client (`UiColors.Energia`, `#FBE36B`), decorazione fissa. |
| Biomi | Scorrendo lo sfondo passa da rocce a sabbia, neve e prati: fasce di terreno composte dal generatore (suoli con varianti), scurite, che sfumano ai bordi. |
| Larghezza | La fabbrica è un **pavimento di 18 celle (1152 px)** disegnato alla scala del gioco (64 px per cella). Su schermi più stretti si **riduce in scala** (`zoom`) per stare nella larghezza; il testo (titoli, schede, catture, bottoni) sta sopra il pavimento su desktop e **sotto, in colonna** su telefono, sempre leggibile. |
| Script | Il sito ha un suo script (`sito.js`, scroll, zoom, drone) e un suo foglio di stile (`stile.css`), entrambi file del sito. Nessun caricamento esterno. |
| Fuori ambito | Versione inglese del sito, feed, changelog completo, ricerca, pagina delle release. |

## Pagine

### `index.html` — vetrina

Pavimento di 18×N celle per sezione; coordinate in celle. Dorsale (nastro T1) sul bordo sinistro (colonna 0), seconda catena lunga (nastro T2) sul bordo destro (colonna 17, poi 13 da «Blocchi» in giù), linea elettrica a x = 17,5.

| # | Sezione | Fabbrica | Contenuto |
|---|---|---|---|
| 01 | **Hero** (alta quanto lo schermo, agganciata) | Minatore T1 su giacimento di ferro (1,6); il nastro esce con la curva in (0,6) e scende. Sfondo: cattura `gioco.png` sfumata. | Logo `ui-logo` ×8, frase, bottoni **Scarica** (`releases/latest`, `data-costruisci`) e **Codice su GitHub**, «scorri ▼». |
| 02 | **Come funziona** (rocce, 9 righe) | Dorsale: `iron-ore` → forno (0,3) → `iron-plate`. Minatore su carbone (5,3) → sdoppiatore (3,3): carbone al forno e, in giù, al generatore (3,6). Nodo (6,7) → lampada (7,7) e nodo del margine (17,8). | Titolo, nota di una riga sulla catena, nove schede dei contenuti dal README («Cosa c'è nel gioco»). |
| 03 | **Catture** (sabbia, 10 righe) | Dorsale col **ponte** (0,3)–(0,6) sopra la cattura grande. A destra: minatore T2 (alimentato, con la linea) sul piombo (13,1) → curva (15,1) → forno (15,4) → `lead-plate` → assemblatore (15,8) col carbone da un minatore (11,8) → `silicon` → curva (17,8) → giù. | Titolo, quattro catture in cornice CRT con didascalia. |
| 04 | **Blocchi come in gioco** (sabbia, 14 righe) | Dorsale: assemblatore (0,2) → `copper-wire`. Seconda catena: curva (17,0) → ovest → curva (13,0) → giù, con **incrocio** in (13,2). Rame: minatore (15,2) → ovest → incrocio → **selezionatore** (2,2) → assemblatore. | Titolo, nota, **striscia agganciata** di undici blocchi animati (GIF di `composti`) che scorre di lato con lo scroll, rimando ad `asset/`. |
| ?? | **Sotto attacco** (nero, 6 righe) | Le due catene lunghe e la linea elettrica passano. | Etichetta «// ?? · SEGNALE DISTURBATO», titolo «Unità: attacco e difesa», sottotitolo, sagome dei droni, avvisi neon, bordo rosso, vetro sparato. |
| 05 | **Ambienti** (neve, 8 righe) | Titanio: minatore (15,0) → curva (14,0) → forno (14,4), che scarica la lastra sulla seconda catena con il pezzo di **unione con l'edificio** (13,4). | Titolo, quattro finestre dei biomi (`tavola._finestra`). |
| 06 | **Interfaccia e accessibilità** (prati, 8 righe) | Seconda catena col **ponte** (13,2)–(13,5) sopra la cattura della palette; lampade accese dal nodo del margine. | Titolo, cinque catture CRT (HUD, daltonici, palette, alto contrasto, Mercato). |
| 07 | **Scarica** (prati, 11 righe) | Le due catene curvano ed entrano nel **Core T3** (7,2) da sinistra e da destra. Schermo del Mercato (12,6) collegato al nodo del margine. Drone che costruisce il bottone. | Titolo, bottone **Scarica vX.Y.Z** (versione dall'ultimo tag, altrimenti «Scarica l'ultima release»), **Sorgenti e release su GitHub**, requisiti dal README («Gioca»). |
| — | Piè di pagina | — | Asset, Sviluppo, GitHub, «generato da tools/sito». |

Catene e oggetti (ricette di `content.json`): dorsale `iron-ore` → `smelt-iron` → `iron-plate` → `craft-copper-wire` (+`copper-ore`) → `copper-wire` → Core; seconda `lead-ore` → `smelt-lead` → `lead-plate` → `craft-silicon` (+`coal`) → `silicon`, più `titanium-ore` → `smelt-titanium` → `titanium-plate` unita in Ambienti → Core. Gli oggetti sono gli sprite `*-belt` (10 px → 20 px).

### `sviluppo/index.html` — com'è fatto e diario

Stesso guscio, senza drone, con un tratto di nastro in testa come divisorio. Sezioni: **Com'è fatto** (pannelli dalle ADR: titolo, prima frase, link a GitHub), **Roadmap** (tabella «Stato e roadmap» del README con lo stato come etichetta), **Diario** (linea del tempo dall'indice di `docs/diario/README.md` con il paragrafo «Obiettivo» di ogni voce), **Decisioni** (elenco di `docs/decisioni/*.md`, numeri non consecutivi), link al `CHANGELOG.md`.

### `asset/index.html`

I contenuti di oggi (blocchi composti con stati e pezzi, sprite, ambienti, carattere) nel guscio nuovo, senza drone. I test di oggi restano validi.

## Architettura del generatore

`tools/sito/`:

- `costruisci.py` — orchestratore: guscio (`_pagina`), pagine, copia dei file statici (`stile.css`, `sito.js`), `catture/`, `fabbrica/` (immagini delle scene), `.nojekyll`; CLI `--out`.
- `composti.py` — invariato (blocchi come in gioco).
- **`fabbrica.py`** — il modello della fabbrica e il disegno delle scene. Una **scena** è una lista di elementi in coordinate di cella: `Tratto` (nastro dritto: cella d'inizio, lunghezza, verso E/S/W/N, tier, oggetti trasportati), `Pezzo` (curva, T, unione laterale, unione con edificio, incrocio, selezionatore, sdoppiatore, ponte: sprite, rotazione/specchio con le regole del client `BeltCornerTile.ResolveTransform`), `Blocco` (id e stato di `composti`), `Giacimento`, `Nodo`, `Linea` (corrente), `Terreno` (bioma). Produce HTML con le immagini scritte in `fabbrica/` (nomi deterministici) e un **controllo di connettività** (test): ogni oggetto parte da un minatore/forno/assemblatore e arriva a un blocco che lo consuma o al Core; nessuna cella con due elementi solidi.
- **`documenti.py`** — README (tabelle «Cosa c'è nel gioco», «Stato e roadmap», requisiti di «Gioca»), indice e paragrafi «Obiettivo» del diario, ADR, CHANGELOG (link), versione (`git describe --tags --abbrev=0`, `None` senza tag), Markdown minimo → HTML (`html.escape` poi codice, grassetto, corsivo, link relativi assoluti su GitHub `blob/main/`). Espone anche `sezioni_sviluppo(radice)`, il corpo della pagina sviluppo.
- `stile.css`, `sito.js` — sorgenti statici copiati nel sito.
- Test: `test_costruisci.py` (pagine, autonomia, dimensione, catture, asset invariati), `test_fabbrica.py` (connettività, trasformazioni delle curve, immagini scritte, determinismo), `test_documenti.py` (tabelle vere del README, diario, ADR, Markdown senza residui), `test_sito.mjs` (`node --test`, funzioni pure dello script: `intercetta`, avanzamento).

### Guscio e classi

```html
<!doctype html><html lang="it"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>…</title><link rel="stylesheet" href="{radice}stile.css"><style>{solo la pagina asset: _STILE_STATI}</style></head>
<body class="vetrina|sviluppo|asset">
<div id="avanza" aria-hidden="true"></div>                      <!-- solo vetrina -->
<header class="barra"><a class="marca" href="{radice}">tINDUSTRY</a><nav>…</nav></header>
<main>…</main>
<footer>…</footer>
<div id="drone" aria-hidden="true" hidden><img src="{radice}fabbrica/drone-1.png" alt=""><svg class="raggio">…</svg></div>  <!-- solo vetrina -->
<script src="{radice}sito.js" defer></script></body></html>
```

Sezione della vetrina: `<section id="…" class="sez" data-bioma="rocce" style="--righe:9">` con dentro `<div class="scena">` (pavimento 1152 px, elementi assoluti in px, `data-rivela` sulle cornici) e `<div class="contenuto">` (figli con `style="--x:…px;--y:…px;--w:…px"`: assoluti su desktop, in colonna sotto la scena su telefono, `max-width: 900px`). Sezioni agganciate: `<div class="pin">` attorno alla sezione (hero) o `.finestra-striscia` sticky (blocchi).

Script (`sito.js`): `adatta()` (zoom delle scene: `min(1, larghezza/1152)`), `scorri()` (variabile `--p` 0..1 su `.pin`, `.sez`, `[data-rivela]`; barra `#avanza`), finestra delle catture (`#visore`, funzione pura `vicino()`), drone (`vaga()`, `costruisci(link)`, `intercetta(evento, link)` pura), politica delle animazioni (`politica(preferenza, riduceSistema)` pura: `statico`, `ridotto` o `pieno`; classi `statico` e `animato` su `<html>`, lette da `stile.css`; pulsante `#animazioni` della barra nella vetrina e in sviluppo; non nella pagina asset, dove i nastri sono solo GIF che nulla può fermare). Senza script: scene a zoom 1 con `overflow-x: hidden`, contenuti visibili, animazioni CSS dei nastri attive.

### Fonti dei dati

`godot/assets/` (sprite), `tools/art` (registro, carattere, tavola dei biomi), `tools/sito/composti.py`, `data/content.json` (ricette, velocità dei nastri), `README.md`, `docs/diario/`, `docs/decisioni/`, `docs/img/readme/` (catture), i tag git.

## Scostamenti decisi durante il lavoro

- **Rendering CSS, non canvas**: il modello a celle è quello previsto, ma le scene sono HTML: i nastri sono le strisce vere del gioco come GIF (binari fermi, battistrada che scorre), gli oggetti sprite animati in CSS, i ponti la GIF composta da `composti` (frecce in sequenza). Funziona anche senza script; il canvas resta una strada se i pezzi animati diventeranno molti di più.
- **Sdoppiatore** come nel gioco: scarica solo ai lati, mai dritto; la catena del carbone di «Come funziona» entra dall'alto e si divide fra forno e generatore con le curve.
- **Pezzo a T** sopra il CORE in «Scarica»: le due catene lunghe arrivano lungo la prima riga da sinistra e da destra e si uniscono in un nastro a T che scende nel CORE. Le unioni laterali («blocco col punto») stanno accanto ai minatori, l'unione con l'edificio in «Ambienti».
- **Titanio col Minatore T2** (durezza 2), con la sua linea elettrica; il forno di «Catture» è alimentato dal nodo del margine.
- **Immagini delle scene a 1×** scalate dalla pagina con `image-rendering: pixelated`; catture ricompresse senza perdita.
- **Striscia dei blocchi** agganciata dentro la sua sezione (sticky), con i numeri del gioco sopra; la hero è agganciata con un `.pin` alto `100vh + 320px`.
- **Telefono**: la hero mostra il pavimento ridotto e il testo in colonna; niente drone sotto gli 864 px; avvisi di «Sotto attacco» statici, due soli.
- Le linee di corrente sono decorazione: nodi più lontani di quanto il gioco ammetta e un Core T3 che in partita non c'è ancora (gli sprite sì).
- **Animazioni sempre accese** (20 ottobre): la prima versione fermava tutto (nastri, oggetti, drone, aggancio di hero e striscia) quando il sistema chiedeva `prefers-reduced-motion`; su Windows basta avere «Effetti di animazione» spento e Chrome lo riferisce al sito, così l'utente vedeva una pagina ferma con la barra di scorrimento sotto la striscia e un vuoto sotto la hero (la regola `height:auto` non batteva `.js .pin`). La fabbrica in movimento è il contenuto: ora gira per tutti, con un pulsante «Animazioni» nella barra che ferma e riavvia tutto (WCAG 2.2.2, scelta in `localStorage`); la preferenza di sistema da sola spegne solo gli effetti a rischio (lampi, scossa, sfarfallio, vetro rotto) e tiene il drone a terra. Via il bottone «Ferma il drone» del piè di pagina.
- **Dopo il controllo dell'utente** (20 ottobre): le catture si aprono in una finestra sopra la pagina (`#visore`: «Chiudi», Esc, clic fuori, frecce per sfogliarle) invece che come pagina a sé, da cui si usciva solo col tasto Indietro; la striscia dei blocchi agganciata (scroll verticale che la spostava di lato, un «doppio scroll» strano da vedere) diventa una griglia ferma su tre righe alla scala del gioco; l'unione a T sopra il CORE mostra gli oggetti di entrambe le catene (filo di rame da ovest, silicio da est, a turno, con animazioni che curvano verso il basso) invece del solo filo di rame; il terreno di ogni sezione copre tutta la larghezza dello schermo, non solo i 1152 px del pavimento. Il racconto guidato dallo scroll chiesto insieme è pianificato a parte: `2026-10-20-sito-scroll-narrativo.md`.

## Verifica

- Test di cui sopra, tutti in `python -m unittest discover -s tools/sito -p "test_*.py"` (il workflow Pages li esegue).
- Vetrina: tre pagine scritte; nessun `<script>`/`<link>`/`<img>` esterno; ogni cattura con didascalia; ogni tratto di nastro con gli oggetti giusti per il suo punto della catena; ponte, incrocio, selezionatore, sdoppiatore, T/unioni presenti; il Core porta `releases/latest`; drone e `data-costruisci` solo nella vetrina; contrasto testo/fondo ≥ 4,5:1.
- Controllo nel browser (Playwright, Chromium del contenitore): 1920, 1280 e 390 px, con script spenti e con `prefers-reduced-motion`.

## Documenti

`tools/sito/costruisci.py` (docstring), `docs/ARCHITETTURA.md` (riga di `tools/sito`), README (roadmap: sito), CHANGELOG, diario a fine piano, `docs/mappa-progetto.canvas` (nodo del sito).
