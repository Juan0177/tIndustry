# Sito "fabbrica" — vetrina, sviluppo e asset su GitHub Pages

- **Stato**: approvata il 2026-10-18 (design discusso con l'utente nel companion visivo, tre direzioni grafiche e due hero a confronto)
- **Sostituisce**: il sito di oggi (`tools/sito/costruisci.py`: ingresso con le catture del README e pagina asset)
- **Pubblicazione**: invariata, `.github/workflows/pages.yml` a mano da `main` o sui tag di release

## Obiettivo

Un sito che *è* una fabbrica di tIndustry: la vetrina si legge scorrendo una pagina lunga in cui il minerale nasce in alto, passa per forno e assemblatore e finisce nel Core in fondo, dove sta il bottone di download. Una seconda pagina racconta come è fatto il gioco e il diario di sviluppo; la pagina degli asset resta con i contenuti di oggi nel guscio nuovo. Tutto generato dal repository, mai scritto a mano.

## Decisioni prese con l'utente

| Tema | Decisione |
|---|---|
| Scroll | **Pagina lunga** a sezioni, nessun feed con caricamento progressivo. "Infinito" è il nastro, non la pagina. |
| Direzione grafica | **Industriale**: fondo scuro, effetto **CRT** sulle catture (scanline, vignetta, riga di scansione), **strisce gialle e nere da cantiere** come bordi e divisori, etichette monospazio numerate, **logo del gioco** (`ui-logo`) come titolo della hero, carattere pixel del gioco per i titoli di sezione. Scartate la "scheda tecnica" chiara e l'"arcade" a colori pieni (di quest'ultima resta la striscia da cantiere). |
| Hero | **A tutto schermo**: la cattura riempie la hero e sfuma nel fondo, il logo ci sta sopra. Se non regge (leggibilità, mobile) si ripiega sulla hero "monitor" (cornice CRT accanto al titolo) e lo si dice all'utente. |
| Larghezza | Su desktop la pagina **usa tutto lo schermo**: sezioni da bordo a bordo, griglie che aggiungono colonne con la larghezza. Solo il testo corrente ha una larghezza massima. |
| Nastri | Non solo orizzontali da sinistra a destra: **curve, tratti verticali, ponte, incrocio, selezionatore** con gli sprite veri, così il nastro accompagna anche i contenuti che non possono prendere tutta la larghezza. |
| Oggetti | I nastri **non sono vuoti**: portano gli oggetti della catena vera del gioco, che si trasforma da materia prima a prodotto fino al Core. |
| Drone | Un **drone** gira per la vetrina; al clic su una voce del menu o un bottone arriva, **costruisce il bottone col raggio** (veloce) e poi si va alla pagina. Non passa alle altre pagine. |
| Potenza | **Generatore, nodi di corrente con le linee, lampade** sparsi per la pagina: il sito è una fabbrica vera. |
| Biomi | Scorrendo, lo sfondo **sfuma tra i quattro biomi** (rocce → sabbia → neve → prati). |
| Seconda pagina | `sviluppo/`: **portafoglio tecnico e diario**, presi dai documenti del repository. |
| Script | Il sito **smette di essere senza script** (drone, durata delle animazioni dei nastri), ma resta **senza caricamenti esterni** e funziona anche con gli script spenti. |
| Fuori ambito | Versione inglese del sito, feed, pagina del changelog completo, ricerca. |

## Pagine

### `index.html` — vetrina

Sezioni in ordine, ognuna con numero ed etichetta monospazio ("// 02 · Come funziona") e titolo col carattere pixel (`carattere.scrivi`):

1. **Hero**: logo `ui-logo` ingrandito senza filtro, frase ("Estrai minerali, portali al CORE sui nastri, fondili in lastre e fili, vendili al Mercato e reinvesti in Ricerca."), bottoni **Scarica** (`releases/latest`) e **Codice su GitHub**, cattura a tutto schermo con effetto CRT che sfuma nel fondo. In basso a sinistra un **minatore T1** al lavoro su un giacimento di ferro (celle di `giacimento-iron-ore-*` sul suolo del bioma) avvia il nastro.
2. **Come funziona**: il nastro porta `iron-ore` a un **forno** acceso; da un nastrino laterale arriva `coal`; dal forno esce `iron-plate`. Sotto, le schede dei contenuti (Mondo, Produzione, Logistica, Energia, Economia, Ricerca, Campagna, Interfaccia, Salvataggi) dalla tabella "Cosa c'è nel gioco" del README.
3. **Catture**: le catture del README più quelle nuove di `docs/img/sito/` (basi piccola e grande, Core, oggetti), in griglia a 2–4 colonne, cornice CRT, didascalia. Il nastro con le lastre **piega con le curve** e scende in **verticale** lungo il bordo, con un **ponte** dove scavalca un pannello.
4. **Blocchi come in gioco**: striscia di blocchi animati (minatore, forno, assemblatore, generatore, lampada, Core) con rimando ad `asset/`. Un secondo minatore porta `copper-ore` da destra; l'**assemblatore** riceve lastre e rame e produce `copper-wire` (ricetta `craft-copper-wire` di `data/content.json`).
5. **Ambienti**: le quattro finestre dei biomi (`tavola._finestra`, come nella pagina asset di oggi). Il nastro col filo di rame continua in verticale con un **selezionatore** e un **incrocio** dove lo attraversa il nastro del carbone.
6. **Interfaccia e accessibilità**: catture HUD, palette, inglese, daltonici, alto contrasto.
7. **Scarica**: il nastro entra in un **Core T3** grande; gli oggetti spariscono dentro; **sul Core** il bottone "Scarica" con la versione dell'ultima release (da `git describe --tags --abbrev=0` al momento della build; senza tag, solo "Scarica l'ultima release") e accanto "Codice su GitHub". Sotto, i requisiti (Windows, .NET incluso nell'export) presi dal README "Gioca".
8. **Piè di pagina**: Asset, Sviluppo, GitHub, "generato da tools/sito".

**Potenza**: un generatore acceso vicino al forno, nodi T1 e T2 nei margini collegati da linee di corrente (colore e spessore del client), lampade con `lamp-glow` accanto ai pannelli delle sezioni. Decorazione fissa, nessuna logica.

**Biomi**: ogni sezione ha come sfondo una fascia di terreno composta dal generatore (suoli con varianti e decorazioni del bioma, senza pareti, scurita per la leggibilità); le fasce sono rocce, sabbia, neve, prati in quest'ordine e sfumano l'una nell'altra sul confine (maschera a gradiente). Il terreno è un PNG ripetuto, non un data URI gigante.

### `sviluppo/index.html` — com'è fatto e diario

1. **Com'è fatto**: quattro pannelli: simulazione deterministica a 60 Hz guidata da comandi (`docs/decisioni/0002`), costruzione come Mindustry (`0003`), arte generata da script (`0004`), mappe a seme (`0005`), interfaccia e lingue (`0006`); ogni pannello cita l'ADR con link a GitHub.
2. **Roadmap**: la tabella "Stato e roadmap" del README, con lo stato come etichetta colorata (Fatto / In corso / Previsto).
3. **Diario**: linea del tempo dall'indice di `docs/diario/README.md`: data, titolo, il paragrafo "Obiettivo" di ogni voce, link al file su GitHub.
4. **Decisioni**: elenco di `docs/decisioni/*.md` con titolo (prima riga `#`) e link.
5. Link al `CHANGELOG.md`.

Stesso guscio della vetrina (strisce, CRT, etichette, nastro in testa come divisorio), **senza drone**.

### `asset/index.html`

I contenuti di oggi (blocchi composti con stati e pezzi, sprite, ambienti, carattere) nel guscio nuovo. Senza drone.

## Architettura del generatore

`tools/sito/`:

- `costruisci.py`: orchestratore, pagine, CLI (`--out`), copia dei file (`catture/`, `terreno/`, `sito.js`, `stile.css`).
- `composti.py`: invariato (blocchi come in gioco).
- **`fabbrica.py`** (nuovo): il modello della fabbrica della vetrina: tratti di nastro, curve, ponte, incrocio, selezionatore, oggetti trasportati, blocchi, nodi e linee di corrente, come dati (una tabella per sezione) e il loro HTML/CSS. Le immagini dei tratti (orizzontale fluido con sprite ripetuto, **verticale con fotogrammi ruotati** dal generatore, curve nei quattro orientamenti) nascono da `composti.fotogrammi_stato` e dagli sprite; velocità del nastro T1 (`VELOCITA_NASTRO`).
- **`documenti.py`** (nuovo): lettura di README (tabelle Markdown → righe), `docs/diario/README.md` (indice) e dei file del diario (paragrafo Obiettivo), `docs/decisioni/*.md` (titoli); convertitore Markdown minimo (grassetto, corsivo, codice, link; i link relativi diventano assoluti su GitHub `blob/main/`). Niente dipendenze nuove: solo `re`.
- `stile.css` e `sito.js` scritti dal generatore come file accanto alle pagine (una volta, condivisi): le pagine restano leggibili e il CSS è testabile.

### Nastri e oggetti

- Un **tratto** è un elemento con lo sprite del nastro come sfondo ripetuto (orizzontale: `repeat-x`, larghezza fluida; verticale: GIF dei fotogrammi ruotati, `repeat-y`, altezza fissa). Curve, ponte, incrocio e selezionatore sono celle fisse da 64 px (scala del gioco, 2 px per pixel d'arte).
- Gli **oggetti** sono gli sprite `*-belt` dentro il tratto, con un'animazione CSS da un capo all'altro (`left`/`top` da 0 a 100%), sfalsati con `animation-delay`; `sito.js` misura la lunghezza di ogni tratto al caricamento e al ridimensionamento e imposta la durata così la velocità resta quella del nastro T1 del gioco. Senza script la durata è quella predefinita del CSS (velocità approssimata).
- Ogni tratto porta l'oggetto della catena in quel punto: `iron-ore` → forno → `iron-plate` → assemblatore (+ `copper-ore`) → `copper-wire` → Core; `coal` sul nastrino del forno. Gli oggetti spariscono entrando nel blocco o nel Core (`overflow: hidden` del tratto).
- Su schermi stretti (< 720 px) i tratti orizzontali diventano una **spina verticale** sul bordo sinistro e i blocchi stanno sopra le sezioni; il ponte e l'incrocio restano.

### Drone

- `drone-1` (sprite del gioco, ombra sotto come `DroneVisual`) in `position: fixed`, solo nella vetrina. **Vaga**: `sito.js` sceglie ogni 3–6 s un punto vicino a un blocco o a un nastro visibile e ci vola con una transizione (velocità simile al gioco), con l'oscillazione del volo (`HoverTiles` di `DroneVisual`) in CSS.
- **Clic su un link con `data-costruisci`** (menu, bottoni Scarica/Codice/Asset/Sviluppo): si ferma la navigazione, il drone vola sul bottone, appare il **raggio** (SVG: linea 2 px e punto luminoso dal muso del drone al bottone, colore `UiColors.Costruzione`), il bottone si copre con lo stile dei **cantieri** (riempimento progressivo) per 0,5 s, poi `location.href` (o `window.open` se il link ha `target`). Durante la costruzione un secondo clic va subito alla pagina.
- **Non si intercetta**: tasto centrale, Ctrl/Cmd/Shift/Alt, `prefers-reduced-motion: reduce`, script spenti, link esterni al menu (nel corpo del testo): in tutti questi casi il link è normale. Da tastiera (Invio) il drone costruisce come al clic.
- Il drone non supera i bordi, non copre il testo mentre si legge (vaga solo vicino ai nastri e ai blocchi) e si può nascondere con un bottone nel piè di pagina ("Ferma il drone", ricordato in `localStorage`).

### Effetto CRT e veste

- `.crt`: cornice scura, scanline (`repeating-linear-gradient`), vignetta radiale, riga di scansione che passa ogni 6 s (ferma con `prefers-reduced-motion`). Le scanline stanno solo sulle immagini, mai sul testo.
- Strisce da cantiere (`repeating-linear-gradient(135deg, giallo, nero)`) come bordo superiore della barra fissa, divisori di sezione, bordo del bottone Scarica.
- Tavolozza: fondo `#0b0d10`, pannelli `#14171c`, linee `#262b33`, ambra `#ffd37f` (titoli, link), giallo `#f2c230` (strisce, bottone), testo `#e3e6ea`, tenue `#9aa3b2` (contrasto ≥ 4,5:1 sul fondo, verificato nel test).
- Menu fisso in alto con le sezioni; su mobile un menu a una riga scorrevole.

### Fonti dei dati

Tutto dal repository: `godot/assets/` (sprite), `tools/art` (registro, carattere, tavola dei biomi), `tools/sito/composti.py`, `data/content.json` (ricette, velocità dei nastri), `README.md` (tabelle Contenuti, Roadmap, requisiti), `docs/diario/`, `docs/decisioni/`, `docs/img/readme/` e `docs/img/sito/` (catture, con `README.md` che le descrive). Le catture nuove vengono dai playtest (`TINDUSTRY_PLAYTEST_BLOCCHI`, `TINDUSTRY_PLAYTEST_BASI`), non da partite a mano.

## Verifica

`tools/sito/test_costruisci.py` (già nel workflow Pages):

- tre pagine scritte, `.nojekyll`, `stile.css`, `sito.js`, `catture/`, `terreno/`;
- vetrina: ogni tratto di nastro ha almeno un oggetto e l'oggetto giusto per il suo punto della catena; le curve e i pezzi speciali ci sono (ponte, incrocio, selezionatore); il Core porta il link a `releases/latest`; ogni cattura di `CATTURE` e di `docs/img/sito/` compare con la didascalia; il drone e i link `data-costruisci` ci sono solo nella vetrina;
- sviluppo: ogni voce dell'indice del diario, ogni ADR e ogni riga della roadmap compaiono; nel testo convertito non restano `**`, `[`…`](`, né backtick;
- asset: i test di oggi invariati;
- contrasto dei colori della tavolozza (calcolo WCAG nel test);
- controllo a occhio nel browser (desktop largo, 1280, mobile) con catture per la PR: hero, nastri con oggetti, drone che costruisce un bottone, pagina sviluppo.

## Documenti

`tools/sito/costruisci.py` (docstring: pagine, script, fonti), `docs/ARCHITETTURA.md` (riga di `tools/sito`, playtest `TINDUSTRY_PLAYTEST_BASI`), README (roadmap: sito), CHANGELOG, diario a fine piano, mappa Obsidian.
