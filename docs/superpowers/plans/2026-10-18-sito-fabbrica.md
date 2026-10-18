# Sito "fabbrica": piano di implementazione

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** riscrivere il sito di GitHub Pages come una fabbrica di tIndustry: vetrina a pagina lunga con nastri, oggetti, blocchi, drone e Core col download; pagina sviluppo (com'è fatto, roadmap, diario, ADR); pagina asset nel guscio nuovo.

**Architecture:** `tools/sito/costruisci.py` resta l'orchestratore e scrive tre pagine più i file statici (`stile.css`, `sito.js`, `catture/`, `terreno/`). Due moduli nuovi: `fabbrica.py` (nastri nei quattro versi, curve, pezzi, oggetti, blocchi, nodi, sfondi dei biomi e il loro HTML, a partire da `composti.py` e dagli sprite) e `documenti.py` (tabelle del README, indice del diario, ADR, Markdown minimo, versione della release). Il CSS e lo script sono file sorgente in `tools/sito/`, copiati nel sito: le animazioni dei nastri sono CSS, lo script misura i tratti per la velocità, muove il drone, disegna le linee di corrente e costruisce i bottoni al clic; senza script tutto resta leggibile e i link funzionano.

**Tech Stack:** Python 3.12 + Pillow (già nel workflow Pages), HTML/CSS/JS senza dipendenze, `unittest`, `node --test` (Node 26 in locale) per le funzioni pure dello script.

**Spec:** [docs/superpowers/specs/2026-10-18-sito-fabbrica-design.md](../specs/2026-10-18-sito-fabbrica-design.md)

## Global Constraints

- Tutto generato dal repository, mai scritto a mano; `python tools/sito/costruisci.py` deterministico (stesso output a due esecuzioni).
- Nessun caricamento esterno: immagini solo `data:`, `catture/…`, `terreno/…`; un solo `<link>` (`stile.css`) e un solo `<script>` (`sito.js`, `defer`), entrambi del sito.
- Scala del gioco: 1 cella = 32 px d'arte = **64 px** nel sito (`composti.SCALA = 2`), `image-rendering: pixelated`.
- Velocità dei nastri da `composti.VELOCITA_NASTRO` (celle al secondo: basic 0,5, fast 1, express 2 → 32, 64, 128 px/s); fotogrammi a `VELOCITA_NASTRO[tier] * 8` al secondo.
- Orientamenti come il client: sprite dritto disegnato verso **Est**; Sud = +90° orario, Ovest = 180°, Nord = −90°. Curva: arte base entra da Ovest ed esce a Sud (svolta **oraria** vista da Est: `(E→S, S→W, W→N, N→E)`); orarie: rotazione da `from` (E 0°, S 90°, W 180°, N −90°) senza specchio; antiorarie: specchio orizzontale prima, poi rotazione da `from` (W 0°, S −90°, E 180°, N 90°) (`BeltCornerTile.ResolveTransform`).
- Catena degli oggetti: `iron-ore` (minatore) → forno (+ `coal`) → `iron-plate` → assemblatore (+ `copper-ore`) → `copper-wire` → Core (`craft-copper-wire`, `smelt-iron` di `data/content.json`). Oggetti con gli sprite `*-belt` (10 px → 20 px).
- Tavolozza: fondo `#0b0d10`, pannelli `#14171c`, linee `#262b33`, ambra `#ffd37f`, giallo `#f2c230`, testo `#e3e6ea`, tenue `#9aa3b2`; corrente `#FBE36B` (`UiColors.Energia`); costruzione `#FFD37F` (`UiColors.Costruzione`). Contrasto testo/fondo ≥ 4,5:1.
- Drone: sprite `drone-1` (24 px → 48 px), oscillazione ±0,05 celle (3,2 px) a 2,6 rad/s (`DroneVisual.HoverTiles`, `HoverRadPerSec`), velocità di volo 6 celle/s (384 px/s), costruzione del bottone 0,5 s; solo nella vetrina. Non si intercettano tasto centrale, Ctrl/Cmd/Shift/Alt, `prefers-reduced-motion: reduce`, link senza `data-costruisci`.
- Biomi sulle sezioni, in ordine: hero e Come funziona `rocce`, Catture e Blocchi `sabbia`, Ambienti e Interfaccia `neve`, Scarica `prati` (`tr.BIOMI` = neve, prati, rocce, sabbia; suoli per bioma in `tr.TERRENO["floors"]`).
- Testi del sito in italiano senza emoji; commenti nel codice in inglese; commit con `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`; Actions solo a mano; mai dati reali del giocatore.
- Branch `sito/fabbrica` (da `main`, già creato con la specifica). I file che la sessione delle catture lascia nel checkout (`docs/img/sito/`, `godot/scripts/dev/World.Playtest.cs`, riga in `docs/ARCHITETTURA.md`) si committano nel Task 5, non prima.

## Review Focus

1. **Schermo stretto (< 720 px)**: nessuno scorrimento orizzontale, i tratti orizzontali diventano la spina verticale a sinistra, le griglie a una colonna. Test nel Task 4 (regola `@media (max-width: 719px)` presente e `.linea` in colonna) e controllo nel browser a 375 px nel Task 8.
2. **Script spenti o `prefers-reduced-motion`**: ogni elemento `data-costruisci` è un `<a href>` vero; gli oggetti si muovono con la durata predefinita del CSS; la riga di scansione e il drone stanno fermi. Test nel Task 5 (link veri) e nel Task 7 (blocco `prefers-reduced-motion` nel CSS, `intercetta()` che restituisce `false`).
3. **Clic col tasto centrale o con modificatori** su un link del menu: il browser deve aprire la nuova scheda, il drone non deve partire. Test `node --test` di `intercetta()` nel Task 7.
4. **Checkout senza tag** (CI con `fetch-depth: 1`, fork): `versione_release()` restituisce `None` e il bottone dice "Scarica l'ultima release" senza versione; il workflow prende i tag. Test nel Task 2 e modifica del workflow nel Task 8.
5. **Markdown del README che cambia** (link annidati, backtick dentro un link, riga della roadmap con ADR e ancora): nessun `**`, `` ` ``, `](` residuo e tutti i link relativi diventano assoluti su GitHub. Test nel Task 2 sulle righe vere del README.

---

### Task 1: Guscio nuovo, file statici e pagina asset nel guscio

**Files:**
- Create: `tools/sito/stile.css`, `tools/sito/sito.js` (scheletro: solo la durata dei tratti, vedi Task 4 e 7)
- Modify: `tools/sito/costruisci.py` (`STILE`, `_pagina`, `pagina_asset`, `pagina_ingresso`, `costruisci`)
- Test: `tools/sito/test_costruisci.py`

**Interfaces:**
- Produces: `_pagina(titolo, corpo, nav, radice="", drone=False, stile_extra="") -> str` (HTML completo: `<link rel="stylesheet" href="{radice}stile.css">`, `<script src="{radice}sito.js" defer>`, barra fissa `<header>` con striscia da cantiere in cima, `<nav>` dai link dati, `<main>`, `<footer>`; `radice` è `""` per la vetrina e `"../"` per le sottopagine; con `drone=True` aggiunge `<div id="drone" aria-hidden="true">`); `STATICI = ("stile.css", "sito.js")` copiati da `tools/sito/` in `out_dir`; `costruisci(out_dir, assets_dir) -> list[str]` restituisce le pagine scritte ordinate (`["asset/index.html", "index.html"]` in questo task, tre pagine dal Task 6).
- Il CSS di oggi (`STILE` + `_STILE_STATI`) va in `stile.css` (la parte degli stati resta generata: `_STILE_STATI` entra in un `<style>` della sola pagina asset tramite `stile_extra`); tavolozza nelle variabili `:root`; classi nuove: `.cantiere-striscia` (10 px, `repeating-linear-gradient(135deg,var(--giallo) 0 14px,#111 14px 28px)`), `.crt` (cornice, scanline, vignetta, `::before` riga di scansione che passa ogni 6 s), `.etichetta` (monospazio, `letter-spacing:.14em`, `//` davanti via `::before`), `.pannello`, `.griglia-catture` (`repeat(auto-fill,minmax(min(100%,420px),1fr))`), `main` senza `max-width`.

- [ ] **Step 1: Aggiornare i test del guscio**

In `test_costruisci.py` sostituisci `test_autonome` e aggiorna `test_pagine`:

```python
def test_pagine(self):
    self.assertEqual(["asset/index.html", "index.html"], self.scritti)
    for nome in ("stile.css", "sito.js", ".nojekyll"):
        self.assertTrue(os.path.exists(os.path.join(self.out, nome)), nome)
    self.assertIn('<link rel="stylesheet" href="../stile.css">', self.asset)
    self.assertIn('<script src="../sito.js" defer></script>', self.asset)
    self.assertIn('<link rel="stylesheet" href="stile.css">', self.ingresso)

def test_autonome(self):
    # Self-contained: one local stylesheet, one local script, images only data: or files of the site.
    for pagina in (self.asset, self.ingresso):
        self.assertEqual(1, len(re.findall(r"<script\b", pagina)))
        self.assertEqual(1, len(re.findall(r"<link\b", pagina)))
        self.assertEqual([], re.findall(r'<img src="(?!data:|(?:\.\./)?(?:catture|terreno)/[a-z0-9-]+\.png")', pagina))
        self.assertNotIn("url(http", pagina)
```

- [ ] **Step 2: Eseguire i test e vederli fallire**

Run: `python -m unittest tools/sito/test_costruisci.py -k test_pagine -k test_autonome`
Expected: FAIL (`stile.css` assente, `<script` assente).

- [ ] **Step 3: Creare `stile.css` e `sito.js`, riscrivere `_pagina` e `costruisci`**

`stile.css`: il CSS attuale riorganizzato con le variabili e le classi dell'Interfaces; `sito.js`: `"use strict";` e una funzione `durataTratti()` vuota per ora (il Task 4 la riempie), chiamata su `DOMContentLoaded` e `resize`. `pagina_asset` passa `nav` come lista di `(testo, href)` e `stile_extra=_STILE_STATI`; `pagina_ingresso` resta quella di oggi tradotta nel guscio nuovo (il Task 5 la sostituisce). `costruisci` copia `STATICI` con `shutil.copyfile` e scrive `.nojekyll`.

- [ ] **Step 4: Eseguire tutti i test del sito**

Run: `python -m unittest tools/sito/test_costruisci.py`
Expected: OK (i test degli asset passano invariati).

- [ ] **Step 5: Commit**

```bash
git add tools/sito/stile.css tools/sito/sito.js tools/sito/costruisci.py tools/sito/test_costruisci.py
git commit -m "feat(sito): guscio nuovo con foglio di stile e script del sito"
```

### Task 2: `documenti.py`: README, diario, decisioni, Markdown minimo, versione

**Files:**
- Create: `tools/sito/documenti.py`, `tools/sito/test_documenti.py`

**Interfaces:**
- Produces:
  - `tabella_dopo(markdown: str, titolo: str) -> list[list[str]]`: celle grezze (senza la riga `| --- |`) della prima tabella dopo la riga `## {titolo}` (o `### `), `[]` se manca.
  - `inline(markdown: str) -> str`: HTML con `html.escape` prima, poi `` `x` `` → `<code>x</code>`, `**x**` → `<b>x</b>`, `*x*` → `<i>x</i>`, `[t](u)` → `<a href="U">t</a>` con `U` = `u` se inizia per `http`, `REPO_URL + "#" + …` se inizia per `#`, altrimenti `REPO_URL + "/blob/main/" + u`; i backtick dentro il testo di un link restano `<code>` dentro `<a>`.
  - `contenuti() -> list[tuple[str, str]]`: `(area, contenuto)` dalla tabella "Cosa c'è nel gioco" del README, già passati da `inline` (l'area senza `<b>`).
  - `roadmap() -> list[tuple[str, str, str]]`: `(stato, nome, cosa)` dalla tabella "Stato e roadmap".
  - `requisiti() -> list[tuple[str, str, str]]`: `(sistema, file, eseguibile)` dalla tabella sotto "## Gioca".
  - `diario() -> list[dict]`: `{"data", "titolo", "file", "url", "obiettivo"}` dalle righe `- [DATA — TITOLO](file.md)` dell'indice di `docs/diario/README.md`, con `obiettivo` = primo paragrafo dopo `## Obiettivo` del file (passato da `inline`, link relativi risolti da `docs/diario/`), `url` su GitHub.
  - `decisioni() -> list[dict]`: `{"numero", "titolo", "url"}` da `docs/decisioni/NNNN-*.md` (titolo = prima riga `# ` senza il prefisso `NNNN — `, per esempio `# 0001 — Il server multiplayer sarà in C#` → `Il server multiplayer sarà in C#`), ordinate per numero.
  - `versione_release(repo: str = REPO) -> str | None`: `git describe --tags --abbrev=0` in `repo`; `None` se il comando fallisce o non ci sono tag.
  - `REPO_URL = "https://github.com/Juan0177/tIndustry"` (spostato qui da `costruisci.py`, che lo importa).

- [ ] **Step 1: Scrivere i test**

```python
class TestDocumenti(unittest.TestCase):
    def test_inline(self):
        self.assertEqual('<b>Mondo</b> con <code>a&lt;b</code>', documenti.inline("**Mondo** con `a<b`"))
        self.assertEqual(f'<a href="{documenti.REPO_URL}/blob/main/docs/x.md">spec <code>y</code></a>',
                         documenti.inline("[spec `y`](docs/x.md)"))
        self.assertEqual(f'<a href="{documenti.REPO_URL}#gioca">Gioca</a>', documenti.inline("[Gioca](#gioca)"))
        self.assertEqual('<a href="https://e.x/">e</a>', documenti.inline("[e](https://e.x/)"))

    def test_tabelle_readme(self):
        aree = [a for a, _ in documenti.contenuti()]
        self.assertEqual(["Mondo", "Produzione", "Logistica", "Energia", "Economia", "Ricerca", "Campagna", "Interfaccia", "Salvataggi"], aree)
        stati = {s for s, _, _ in documenti.roadmap()}
        self.assertTrue({"Fatto", "In corso", "Previsto"} <= stati, stati)
        self.assertEqual("Windows", documenti.requisiti()[0][0])
        for riga in documenti.contenuti() + [(n, c) for _, n, c in documenti.roadmap()]:
            for cella in riga:
                for residuo in ("**", "`", "]("):
                    self.assertNotIn(residuo, cella, cella)

    def test_diario_e_decisioni(self):
        voci = documenti.diario()
        self.assertGreaterEqual(len(voci), 7)
        self.assertEqual("2026-10-16", voci[0]["data"])
        for v in voci:
            self.assertTrue(v["obiettivo"], v["file"])
            self.assertTrue(v["url"].startswith(documenti.REPO_URL + "/blob/main/docs/diario/"))
        adr = documenti.decisioni()
        self.assertEqual([1, 2, 3, 4, 5, 6], [a["numero"] for a in adr][:6])
        self.assertEqual("Il server multiplayer sarà in C#", adr[0]["titolo"])

    def test_versione(self):
        self.assertRegex(documenti.versione_release() or "v0.0.0", r"^v\d+\.\d+")
        with tempfile.TemporaryDirectory() as vuota:
            self.assertIsNone(documenti.versione_release(vuota))
```

- [ ] **Step 2: Eseguirli e vederli fallire**

Run: `python -m unittest tools/sito/test_documenti.py`
Expected: FAIL (`ModuleNotFoundError: documenti`).

- [ ] **Step 3: Scrivere `documenti.py`** con le firme dell'Interfaces, solo `re`, `os`, `html`, `subprocess`, `glob`.

Per `inline` l'ordine è: escape, codice, link (regex `\[([^\]]+)\]\(([^)\s]+)\)` applicata dopo il codice, così i backtick nel testo del link sono già `<code>`), grassetto, corsivo. `subprocess.run(["git", "describe", "--tags", "--abbrev=0"], cwd=repo, capture_output=True, text=True)` in `try/except OSError`.

- [ ] **Step 4: Eseguire i test**

Run: `python -m unittest tools/sito/test_documenti.py`
Expected: OK.

- [ ] **Step 5: Commit**

```bash
git add tools/sito/documenti.py tools/sito/test_documenti.py tools/sito/costruisci.py
git commit -m "feat(sito): lettura di README, diario e decisioni per il sito"
```

### Task 3: `fabbrica.py`: immagini dei nastri, delle curve, degli oggetti e degli sfondi dei biomi

**Files:**
- Create: `tools/sito/fabbrica.py`, `tools/sito/test_fabbrica.py`

**Interfaces:**
- Consumes: `composti.Immagini`, `composti.VELOCITA_NASTRO`, `composti.SCALA`, `composti.T`, `composti.BLOCCHI`, `composti.fotogrammi_stato`, `costruisci._gif`/`_png`/`_data_url` (spostati in un modulo `immagini.py`? No: `fabbrica` importa da `costruisci` creerebbe un ciclo; **sposta** `_data_url`, `_png`, `_gif` in `fabbrica.py` come `data_url`, `png`, `gif` e fai importare `costruisci` da lì).
- Produces:
  - `CELLA = composti.T * composti.SCALA` (64).
  - `VERSI = ("E", "S", "W", "N")`; `ROTAZIONE = {"E": 0, "S": 90, "W": 180, "N": -90}` (gradi orari).
  - `ruota(im, gradi_orari: int) -> Image` (PIL ruota in senso antiorario: `im.rotate(-gradi, expand=True)`).
  - `fotogrammi_nastro(immagini, tier: str, verso: str) -> list[Image]`: i fotogrammi di `{tier}-anim` ruotati per `verso`, a scala 2.
  - `fotogrammi_curva(immagini, tier, da: str, a: str) -> list[Image]`: regola di `BeltCornerTile.ResolveTransform` (vedi Global Constraints), a scala 2.
  - `gif_nastro(immagini, tier, verso) -> str` e `gif_curva(immagini, tier, da, a) -> str`: data URI GIF con durata `1000 / (VELOCITA_NASTRO[tier] * 8)` ms per fotogramma.
  - `png_sprite(immagini, nome, scala=2) -> str`: data URI PNG dello sprite ingrandito (per `*-belt`, `power-node`, `power-node-t2`, `junction`, `sorter`, `lamp-glow`, `ui/ui-logo`, `drone-1`).
  - `gif_blocco(immagini, id: str, stato: str) -> tuple[str, int, int]`: `(data URI, larghezza px, altezza px)` dello stato di un blocco di `composti.BLOCCHI` (GIF se animato, PNG se fermo; stesso codice di `costruisci.immagine_stato`, che da qui in poi chiama questa).
  - `sfondo_bioma(immagini, bioma: str, lato_celle: int = 8) -> Image`: quadrato `lato_celle × lato_celle` celle a scala 2 di suoli del bioma (primo suolo di `tr.TERRENO["floors"]` per il bioma nel 70 % delle celle, gli altri nel resto; variante `0–3` scelta con `random.Random(f"{bioma}:{x}:{y}")`), una decorazione `deco-{bioma}-{k}` ogni 9 celle circa, poi scurito moltiplicando RGB per 0,38. Ripetibile (tile seamless perché i suoli lo sono).

- [ ] **Step 1: Scrivere i test**

```python
class TestFabbrica(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.im = composti.Immagini(ASSETS)

    def test_nastro_ruotato(self):
        est = fabbrica.fotogrammi_nastro(self.im, "conveyor-basic", "E")
        sud = fabbrica.fotogrammi_nastro(self.im, "conveyor-basic", "S")
        self.assertEqual(8, len(est))
        self.assertEqual((64, 64), est[0].size)
        self.assertEqual(est[0].rotate(-90).tobytes(), sud[0].tobytes())
        self.assertEqual(est[0].rotate(180).tobytes(), fabbrica.fotogrammi_nastro(self.im, "conveyor-basic", "W")[0].tobytes())

    def test_curve(self):
        base = fabbrica.fotogrammi_curva(self.im, "conveyor-basic", "E", "S")  # clockwise from East: raw art
        grezzo = composti._ingrandisci(self.im.fotogrammi("conveyor-basic-corner-anim")[0])
        self.assertEqual(grezzo.tobytes(), base[0].tobytes())
        ws = fabbrica.fotogrammi_curva(self.im, "conveyor-basic", "W", "S")  # counter-clockwise from West: mirrored
        self.assertEqual(grezzo.transpose(Image.Transpose.FLIP_LEFT_RIGHT).tobytes(), ws[0].tobytes())
        self.assertEqual(grezzo.rotate(-90).tobytes(), fabbrica.fotogrammi_curva(self.im, "conveyor-basic", "S", "W")[0].tobytes())

    def test_gif_e_png(self):
        self.assertTrue(fabbrica.gif_nastro(self.im, "conveyor-fast", "N").startswith("data:image/gif;base64,"))
        self.assertTrue(fabbrica.png_sprite(self.im, "iron-ore-belt").startswith("data:image/png;base64,"))
        uri, w, h = fabbrica.gif_blocco(self.im, "smelter", "Al lavoro")
        self.assertEqual((128, 128), (w, h))
        self.assertTrue(uri.startswith("data:image/gif;base64,"))

    def test_sfondo_bioma(self):
        for bioma in ("rocce", "sabbia", "neve", "prati"):
            a = fabbrica.sfondo_bioma(self.im, bioma)
            self.assertEqual((512, 512), a.size)
            self.assertEqual(a.tobytes(), fabbrica.sfondo_bioma(self.im, bioma).tobytes())
            self.assertLess(max(a.convert("L").getextrema()), 120, bioma)  # darkened
        self.assertNotEqual(fabbrica.sfondo_bioma(self.im, "neve").tobytes(), fabbrica.sfondo_bioma(self.im, "prati").tobytes())
```

- [ ] **Step 2: Eseguirli e vederli fallire**

Run: `python -m unittest tools/sito/test_fabbrica.py`
Expected: FAIL (`ModuleNotFoundError: fabbrica`).

- [ ] **Step 3: Scrivere la parte immagini di `fabbrica.py`** e spostare `data_url`, `png`, `gif` da `costruisci.py` (che li importa con i vecchi nomi `_data_url = fabbrica.data_url` ecc. per non toccare il resto).

- [ ] **Step 4: Eseguire i test di `fabbrica` e del sito**

Run: `python -m unittest tools/sito/test_fabbrica.py tools/sito/test_costruisci.py`
Expected: OK.

- [ ] **Step 5: Commit**

```bash
git add tools/sito/fabbrica.py tools/sito/test_fabbrica.py tools/sito/costruisci.py
git commit -m "feat(sito): nastri nei quattro versi, curve, oggetti e sfondi dei biomi"
```

### Task 4: `fabbrica.py`: modello e HTML della fabbrica (tratti, linee, spina, pezzi, nodi) e CSS/JS dei nastri

**Files:**
- Modify: `tools/sito/fabbrica.py`, `tools/sito/stile.css`, `tools/sito/sito.js`
- Test: `tools/sito/test_fabbrica.py`

**Interfaces:**
- Produces (dati):
  - `Tratto(verso: str, oggetto: str, tier: str = "conveyor-basic", celle: int | None = None)`: `celle=None` = orizzontale fluido (`flex: 1`), altrimenti lunghezza fissa.
  - `Curva(da: str, a: str, tier="conveyor-basic")`, `Pezzo(nome: str, verso: str = "E")` per `junction`, `sorter` (con `iron-ore-belt` centrato sopra come in `composti`), `Ponte(verso: str, tier="conveyor-basic")` (blocco `conveyor-bridge` stato "In funzione", ruotato per `verso`), `Blocco(id: str, stato: str)`, `Nodo(nome: str, rete: str)` (`power-node` / `power-node-t2`, `rete` = nome della rete per le linee), `Spazio(celle: int)`.
  - `Linea(elementi: list, allinea: str = "fondo")`: una fila flessibile; `Spina(elementi: list, lato: str = "sx")`: colonna verticale di 64 px sul bordo della sezione.
- Produces (HTML):
  - `Fabbrica(immagini)` con `html_linea(linea) -> str`, `html_spina(spina) -> str`, `stile() -> str` (le regole con i data URI: `.nastro-{tier}-{verso}{background-image:url(…)}` per i versi usati, `.curva-…`, più `--v-{tier}: {px/s}` sulle `:root`), che accumula solo le immagini effettivamente usate.
  - HTML di un tratto: `<div class="tratto tratto-{verso} nastro-{tier}-{verso}" data-oggetto="{oggetto}" data-tier="{tier}" style="--celle:{celle}">` con dentro **8** `<img class="oggetto" src="{png *-belt}" alt="" style="--k:{k}">`; i tratti orizzontali senza `celle` non hanno `--celle`.
  - HTML di una linea: `<div class="linea linea-{allinea}">…</div>`; blocco: `<img class="blocco" src=… width height alt="{id}: {stato}" data-blocco="{id}">`; nodo: `<img class="nodo" data-rete="{rete}" …>`; curva: `<img class="curva" src=… width="64" height="64" alt="">`; ponte: `<img class="ponte" …>`; pezzo: `<span class="pezzo" data-pezzo="{nome}"><img …>(<img class="filtro" …> per il selezionatore)</span>`; spazio: `<i class="spazio" style="--celle:n"></i>`. Le velocità in `stile()` si scrivono con `f"{v:g}"` (`--v-conveyor-basic:32`).
  - `test_fabbrica.py` definisce in testa `SITO = os.path.dirname(os.path.abspath(__file__))` e `ASSETS = os.path.join(SITO, "..", "..", "godot", "assets")`; `test_costruisci.py` importa anche `html` e `documenti`.
- CSS (`stile.css`): `.tratto` `position:relative; overflow:hidden; background-repeat` lungo il verso (`repeat-x` per E/W, `repeat-y` per S/N), misura `64px` per lato corto, `--celle` × 64 px per quello lungo se data; `.oggetto` assoluto, centrato sul lato corto, `animation: scorri-{verso} var(--durata, 8s) linear infinite; animation-delay: calc(var(--k) * var(--durata, 8s) / -8)`; `@keyframes scorri-e {from{left:-20px} to{left:100%}}` e simili per W (da destra), S (`top`), N; `.linea{display:flex;align-items:flex-end}` (`.linea-centro` → `center`); `.spina{position:absolute;top:0;bottom:0;width:64px;display:flex;flex-direction:column}`; `@media (max-width: 719px)`: `.linea{flex-direction:column;align-items:flex-start}` e i tratti orizzontali fluidi diventano verticali (`.tratto-e,.tratto-w{width:64px;height:calc(var(--celle,4)*64px)}` con l'animazione `scorri-s`).
- JS (`sito.js`): `durataTratti()` per ogni `.tratto`: lunghezza = `offsetWidth` (E/W) o `offsetHeight` (S/N), `--durata = lunghezza / v` secondi con `v` = `getComputedStyle(root).getPropertyValue("--v-" + tier)` (px/s); chiamata al caricamento e al `resize` (debounce 150 ms).

- [ ] **Step 1: Scrivere i test**

```python
def test_html_tratto(self):
    f = fabbrica.Fabbrica(self.im)
    h = f.html_linea(fabbrica.Linea([fabbrica.Blocco("miner", "Al lavoro"), fabbrica.Tratto("E", "iron-ore"),
                                     fabbrica.Curva("E", "S"), fabbrica.Tratto("S", "iron-ore", celle=3)]))
    self.assertEqual(2, h.count('class="tratto'))
    self.assertEqual(16, h.count('class="oggetto"'))
    self.assertIn('data-oggetto="iron-ore" data-tier="conveyor-basic"', h)
    self.assertIn('style="--celle:3"', h)
    self.assertIn('data-blocco="miner"', h)
    self.assertIn(".nastro-conveyor-basic-E{background-image:url(data:image/gif;base64,", f.stile())
    self.assertIn(".nastro-conveyor-basic-S{", f.stile())
    self.assertNotIn(".nastro-conveyor-basic-W{", f.stile())
    self.assertIn("--v-conveyor-basic:32", f.stile())

def test_css_e_js_dei_nastri(self):
    with open(os.path.join(SITO, "stile.css"), encoding="utf-8") as fh:
        css = fh.read()
    for k in ("@keyframes scorri-e", "@keyframes scorri-s", "@media (max-width: 719px)", ".spina{"):
        self.assertIn(k, css)
    with open(os.path.join(SITO, "sito.js"), encoding="utf-8") as fh:
        js = fh.read()
    self.assertIn("--durata", js)
```

- [ ] **Step 2: Eseguirli e vederli fallire**

Run: `python -m unittest tools/sito/test_fabbrica.py -k html -k css`
Expected: FAIL.

- [ ] **Step 3: Implementare dataclass, `Fabbrica`, CSS e `durataTratti()`**

- [ ] **Step 4: Eseguire i test**

Run: `python -m unittest tools/sito/test_fabbrica.py tools/sito/test_costruisci.py`
Expected: OK.

- [ ] **Step 5: Commit**

```bash
git add tools/sito/fabbrica.py tools/sito/test_fabbrica.py tools/sito/stile.css tools/sito/sito.js
git commit -m "feat(sito): modello della fabbrica, tratti con oggetti, spina verticale"
```

### Task 5: La vetrina (`index.html`)

**Files:**
- Modify: `tools/sito/costruisci.py` (`pagina_ingresso`, `CATTURE`, nuovo `CATTURE_SITO`, `costruisci`), `tools/sito/stile.css`
- Commit anche: `docs/img/sito/*.png`, `docs/img/sito/README.md`, `godot/scripts/dev/World.Playtest.cs`, la riga di `docs/ARCHITETTURA.md` lasciati dalla sessione delle catture (verifica con `git status` che siano solo quelli; se mancano `base-*.png`, usa `core.png` per la hero e scrivilo nel registro).
- Test: `tools/sito/test_costruisci.py`

**Interfaces:**
- Consumes: `fabbrica.Fabbrica` e le dataclass del Task 4, `fabbrica.gif_blocco`, `png_sprite`, `sfondo_bioma`; `documenti.contenuti`, `requisiti`, `versione_release`, `REPO_URL`; `tavola._finestra` e `biomi()` già in `costruisci`.
- Produces: `CATTURE_SITO_DIR = docs/img/sito`, `CATTURE_SITO = [("base-grande.png", "Base grande: quattro linee convergono sul Core"), ("base-piccola.png", "Base piccola: due minatori, forno e generatore"), ("base-grande-vicino.png", "Nastri pieni visti da vicino"), ("core.png", "Il Core da vicino, col drone"), ("oggetti.png", "Tutti gli oggetti sui tre nastri"), ("panoramica.png", "La fabbrica nel paesaggio a zoom basso")]` (tieni solo le voci il cui file esiste in `docs/img/sito/`; le didascalie si possono ritoccare dal `README.md` della cartella); `HERO = "base-grande-vicino.png"`; `SEZIONI = [("gioco", "Il gioco", "rocce"), ("come-funziona", "Come funziona", "rocce"), ("catture", "Catture", "sabbia"), ("blocchi", "Blocchi come in gioco", "sabbia"), ("ambienti", "Ambienti", "neve"), ("interfaccia", "Interfaccia e accessibilità", "neve"), ("scarica", "Scarica", "prati")]`; `costruisci` scrive anche `terreno/{bioma}.png` per i quattro biomi e copia le catture di entrambe le cartelle in `catture/`.
- Struttura della pagina (`<main>` senza larghezza massima; ogni `<section id class="sezione bioma-{bioma}">` con `::before` di sfondo `url(terreno/{bioma}.png)` a `opacity:.35` e `mask-image:linear-gradient(transparent,#000 160px,#000 calc(100% - 160px),transparent)`; dentro, `<span class="etichetta">0N · Titolo</span>` e il titolo come immagine `carattere.scrivi(titolo, 2, (227,230,234,255))`):
  1. `#gioco` (hero, `class="hero"`): `<div class="crt crt-pieno"><img src="catture/{HERO}" …></div>` a tutta larghezza con `mask-image:linear-gradient(#000 40%,transparent)`; sopra, in basso a sinistra: `<img class="logo" src="{png_sprite ui/ui-logo, scala 6}">` (324×96), `<p class="frase">` con la frase della specifica, bottoni `<a class="bottone bottone-giallo" href="{REPO_URL}/releases/latest" data-costruisci>Scarica{ " " + versione}</a>` e `<a class="bottone" href="{REPO_URL}" data-costruisci>Codice su GitHub</a>`. Sotto la hero una `Linea([Blocco("miner","Al lavoro"), Tratto("E","iron-ore"), Curva("E","S")])` su sfondo con 2×2 celle `giacimento-iron-ore-0` dietro il minatore, e la `Spina([Tratto("S","iron-ore",celle=2)])` che porta alla sezione 2.
  2. `#come-funziona`: `Linea([Spazio(1), Curva("S","E"), Tratto("E","iron-ore"), Blocco("smelter","Al lavoro"), Tratto("E","iron-plate","conveyor-fast"), Curva("E","S")])`; sopra il forno una linea corta `Linea([Spazio(6), Blocco("generator","In funzione"), Nodo("power-node","a"), Spazio(2), Tratto("W","coal",celle=3)], allinea="fondo")` (il carbone arriva al forno da destra); schede `<div class="schede">` una per `documenti.contenuti()` (`<article><b>{area}</b>{contenuto}</article>`).
  3. `#catture`: la `Spina` a destra `Spina([Tratto("S","iron-plate","conveyor-fast",celle=3), Ponte("S","conveyor-fast"), Tratto("S","iron-plate","conveyor-fast",celle=3)], lato="dx")`; griglia `.griglia-catture` con `CATTURE_SITO + CATTURE` (`<figure data-cattura="{nome}"><div class="crt"><img src="catture/{nome}" loading="lazy" width="1280" height="720" alt="{didascalia}"></div><figcaption>{didascalia}</figcaption></figure>`); in testa due `Nodo("power-node-t2","a")` ai lati e una `Blocco("lamp","Accesa")`.
  4. `#blocchi`: `Linea([Spazio(1), Curva("S","E"), Tratto("E","iron-plate","conveyor-fast"), Blocco("assembler","Ciclo di lavoro"), Tratto("W","copper-ore",celle=4), Blocco("miner-advanced","Al lavoro, con corrente"), Nodo("power-node","b")])` e sotto `Linea([Spazio(1), Tratto("E","copper-wire","conveyor-express"), Curva("E","S")])` che parte dall'assemblatore; poi la striscia `<div class="striscia-blocchi">` con sei `Blocco` (miner, smelter, assembler, generator, lamp, core-2) e la didascalia in monospazio, e il bottone `<a class="bottone" href="asset/" data-costruisci>Tutti gli asset</a>`.
  5. `#ambienti`: `Spina([Tratto("S","copper-wire","conveyor-express",celle=2), Pezzo("sorter"), Tratto("S","copper-wire","conveyor-express",celle=2), Pezzo("junction"), Tratto("S","copper-wire","conveyor-express",celle=2)], lato="sx")` con una `Linea([Spazio(1), Tratto("E","coal",celle=3)])` che attraversa l'incrocio; le quattro finestre dei biomi come nella pagina asset (`_sezione_ambienti` resa riusabile: estrai `_schede_ambienti(assets_dir, immagini) -> str`).
  6. `#interfaccia`: griglia `.griglia-catture` con `interfaccia-hud.png`, `interfaccia-palette.png`, `interfaccia-en.png`, `interfaccia-daltonici.png`, `interfaccia-altocontrasto.png` (didascalie da `CATTURE`), una `Blocco("lamp","Accesa")` per angolo.
  7. `#scarica`: `Linea([Spazio(1), Curva("S","E"), Tratto("E","copper-wire","conveyor-express"), <div class="core">])` dove `.core` contiene `Blocco("core-3","Luce del nucleo")` e, sovrapposti al centro, `<a class="bottone bottone-giallo bottone-core" href="{REPO_URL}/releases/latest" data-costruisci>Scarica {versione or "l'ultima release"}</a>` e sotto `<a class="bottone" href="{REPO_URL}" data-costruisci>Codice su GitHub</a>`; tabella dei `documenti.requisiti()` e il link a `godot/README.md` su GitHub.
  8. `<footer>`: link Asset (`asset/`), Sviluppo (`sviluppo/`), GitHub, `<button type="button" id="ferma-drone">Ferma il drone</button>`, "generato da tools/sito/costruisci.py".
- `<nav>` della barra: Gioco, Come funziona, Catture, Blocchi, Ambienti, Interfaccia, Asset, Sviluppo, Scarica (ancore e pagine), tutti con `data-costruisci`.

- [ ] **Step 1: Aggiornare i test della vetrina**

```python
def test_vetrina_fabbrica(self):
    for sid, _, bioma in costruisci.SEZIONI:
        self.assertIn(f'<section id="{sid}" class="sezione bioma-{bioma}">', self.ingresso)
        self.assertTrue(os.path.exists(os.path.join(self.out, "terreno", f"{bioma}.png")), bioma)
    tratti = re.findall(r'<div class="tratto[^"]*" data-oggetto="([a-z-]+)"', self.ingresso)
    self.assertGreaterEqual(len(tratti), 10)
    for oggetto in ("iron-ore", "coal", "iron-plate", "copper-ore", "copper-wire"):
        self.assertIn(oggetto, tratti)
    ordine = [o for o in tratti if o in ("iron-ore", "iron-plate", "copper-wire")]
    self.assertEqual(sorted(ordine, key=["iron-ore", "iron-plate", "copper-wire"].index), ordine)
    for pezzo in ('class="curva"', 'class="ponte"', 'data-pezzo="sorter"', 'data-pezzo="junction"', 'data-rete="a"', 'data-blocco="lamp"'):
        self.assertIn(pezzo, self.ingresso)
    self.assertIn('data-blocco="core-3"', self.ingresso)
    self.assertRegex(self.ingresso, r'<a class="bottone bottone-giallo bottone-core" href="https://github.com/Juan0177/tIndustry/releases/latest" data-costruisci>Scarica ')
    self.assertIn('src="data:image/png;base64,', self.ingresso[self.ingresso.index('class="logo"') - 200:self.ingresso.index('class="logo"') + 400])
    self.assertIn('<div id="drone" aria-hidden="true">', self.ingresso)
    self.assertNotIn('id="drone"', self.asset)
    for m in re.finditer(r"<a ([^>]*)data-costruisci", self.ingresso):
        self.assertIn("href=", m.group(1))

def test_catture(self):
    sezione = lambda sid: self.ingresso[self.ingresso.index(f'<section id="{sid}"'):]
    for nome, didascalia in costruisci.CATTURE_SITO + costruisci.CATTURE:
        self.assertIn(f'data-cattura="{nome}"><div class="crt"><img src="catture/{nome}"', self.ingresso, nome)
        self.assertIn(f"<figcaption>{html.escape(didascalia)}</figcaption>", self.ingresso)
        self.assertTrue(os.path.exists(os.path.join(self.out, "catture", nome)), nome)
    self.assertIn(f'<img src="catture/{costruisci.HERO}"', sezione("gioco"))
    self.assertLess(len(self.ingresso.encode("utf-8")), 2_500_000)
```

- [ ] **Step 2: Eseguirli e vederli fallire**

Run: `python -m unittest tools/sito/test_costruisci.py -k vetrina -k catture`
Expected: FAIL.

- [ ] **Step 3: Scrivere `pagina_ingresso` con la struttura sopra**, le classi CSS nuove in `stile.css` (`.hero`, `.crt-pieno`, `.logo`, `.bottone`, `.bottone-giallo` con bordo a striscia, `.bottone-core` sovrapposto, `.schede`, `.striscia-blocchi`, `.core`, `.sezione::before`, `.bioma-*`), e in `costruisci()` la scrittura di `terreno/*.png` e la copia di `CATTURE_SITO`.

- [ ] **Step 4: Eseguire i test e guardare la pagina**

Run: `python -m unittest tools/sito/test_costruisci.py tools/sito/test_fabbrica.py tools/sito/test_documenti.py && python tools/sito/costruisci.py`
Expected: OK. Apri `_site/index.html` nel browser (desktop largo e 1280): nastri con oggetti in movimento, curve giuste (gli oggetti non escono dal nastro), hero leggibile; se il logo o la frase non si leggono sulla cattura, aumenta la sfumatura (`mask-image` a `#000 25%`) e, se ancora no, passa alla hero "monitor" (`.crt` accanto al testo) e scrivilo nel registro.

- [ ] **Step 5: Commit**

```bash
git add tools/sito docs/img/sito godot/scripts/dev/World.Playtest.cs docs/ARCHITETTURA.md
git commit -m "feat(sito): la vetrina è una fabbrica: nastri, oggetti, blocchi, Core col download, catture nuove"
```

### Task 6: La pagina sviluppo (`sviluppo/index.html`)

**Files:**
- Modify: `tools/sito/costruisci.py` (`pagina_sviluppo`, `costruisci`), `tools/sito/stile.css`
- Test: `tools/sito/test_costruisci.py`

**Interfaces:**
- Consumes: `documenti.roadmap`, `diario`, `decisioni`, `inline`, `REPO_URL`; `_pagina(..., radice="../")`.
- Produces: `pagina_sviluppo() -> str`; `costruisci` scrive `sviluppo/index.html` (`["asset/index.html", "index.html", "sviluppo/index.html"]`).
- Sezioni: `#come-e-fatto` con cinque `<article class="pannello">` fissi in `COME_E_FATTO = [("Simulazione deterministica", "Tick fisso a 60 Hz guidato da comandi, stesso risultato su ogni macchina: la base del co-op.", 2), ("Costruzione come Mindustry", "Piani in coda, drone che vola e costruisce, cantieri che consumano e rimborsano.", 3), ("Arte generata da script", "Ogni sprite, nastro, icona e carattere nasce da tools/art: si cambia lo script e si rigenera.", 4), ("Mappe a seme", "Tre misure, quattro biomi, pareti e acqua da un numero; mappe della campagna in file.", 5), ("Interfaccia e lingue", "Tema unico, carattere pixel, italiano e inglese, scala dell'interfaccia e colori accessibili.", 6)]` (titolo, frase, numero dell'ADR → link dall'elenco `decisioni()`); `#roadmap` tabella `<table class="roadmap">` con `<td><span class="stato stato-{fatto|in-corso|previsto|altro}">{stato}</span></td>` (classe da `stato.lower()` prima della virgola, spazi → `-`); `#diario` `<ol class="diario">` una `<li>` per voce con `<time>{data}</time> <a href="{url}">{titolo}</a><p>{obiettivo}</p>`; `#decisioni` `<ul>` con `ADR {numero:04d} — <a>{titolo}</a>`; link al `CHANGELOG.md`.

- [ ] **Step 1: Scrivere il test**

```python
def test_sviluppo(self):
    self.assertEqual(["asset/index.html", "index.html", "sviluppo/index.html"], self.scritti)
    with open(os.path.join(self.out, "sviluppo", "index.html"), encoding="utf-8") as fh:
        pagina = fh.read()
    self.assertIn('<link rel="stylesheet" href="../stile.css">', pagina)
    for v in documenti.diario():
        self.assertIn(f'<a href="{v["url"]}">{html.escape(v["titolo"])}</a>', pagina)
    for a in documenti.decisioni():
        self.assertIn(a["url"], pagina)
    for stato, nome, _ in documenti.roadmap():
        self.assertIn(nome, pagina)
    self.assertIn('class="stato stato-fatto"', pagina)
    self.assertIn(f'{documenti.REPO_URL}/blob/main/CHANGELOG.md', pagina)
    self.assertNotIn('id="drone"', pagina)
    for residuo in ("**", "](", "<p>`"):
        self.assertNotIn(residuo, pagina)
```

- [ ] **Step 2: Eseguirlo e vederlo fallire**

Run: `python -m unittest tools/sito/test_costruisci.py -k sviluppo`
Expected: FAIL.

- [ ] **Step 3: Scrivere `pagina_sviluppo`** e aggiungere al `test_autonome` la terza pagina.

- [ ] **Step 4: Eseguire i test**

Run: `python -m unittest tools/sito/test_costruisci.py`
Expected: OK.

- [ ] **Step 5: Commit**

```bash
git add tools/sito/costruisci.py tools/sito/stile.css tools/sito/test_costruisci.py
git commit -m "feat(sito): pagina sviluppo con com'è fatto, roadmap, diario e decisioni"
```

### Task 7: Drone, raggio di costruzione, linee di corrente, riduzione del movimento

**Files:**
- Modify: `tools/sito/sito.js`, `tools/sito/stile.css`, `tools/sito/costruisci.py` (immagine del drone nel `#drone`: `png_sprite(immagini, "drone-1")` come `<img>` più `<i class="ombra">`)
- Create: `tools/sito/test_sito.js` (`node --test`)
- Test: `tools/sito/test_fabbrica.py` (CSS), `tools/sito/test_sito.js`

**Interfaces:**
- `sito.js` espone, in fondo, `if (typeof module !== "undefined") module.exports = { intercetta, prossimaMeta, durataDa };` dove:
  - `intercetta(evento, riduciMovimento: bool) -> bool`: `true` solo se `evento.button === 0`, nessun modificatore (`ctrlKey`, `metaKey`, `shiftKey`, `altKey`), `!riduciMovimento` e l'elemento ha `data-costruisci`.
  - `prossimaMeta(rettangoli: {x,y,w,h}[], finestra: {w,h}, caso: () => number) -> {x,y}`: un punto a 24–48 px da un lato di un rettangolo visibile scelto a caso (i rettangoli sono quelli di `.blocco`, `.tratto`, `.nodo` dentro la finestra); se nessuno è visibile, un punto casuale nella fascia bassa della finestra.
  - `durataDa(lunghezzaPx: number, velocitaPx: number) -> number` (secondi, ≥ 1).
- Comportamento nella pagina: `#drone` in `position:fixed`, trasformato con `translate(x,y)` in transizione (`transition: transform` con durata = distanza / 384 px/s, `ease-in-out`), dentro una `<img>` con l'animazione CSS `oscilla` (`translateY(±3.2px)`, periodo 2,42 s) e la `.ombra` (ellisse scura sfocata 6 px sotto); ogni 3–6 s una nuova meta da `prossimaMeta`. Al clic su `[data-costruisci]` con `intercetta` vero: `preventDefault`, meta = centro del bottone spostato di 40 px verso l'alto, all'arrivo (evento `transitionend` o 1,2 s al massimo) si mostra `<svg id="raggio">` (`line` 2 px `#FFD37F` dal muso del drone al centro del bottone, `rect` 6×6 e 3×3 bianco sul punto), il bottone riceve `.in-costruzione` (`::after` che riempie da sinistra in 0,5 s con `rgba(255,211,127,.55)`) e dopo 0,5 s `location.href = href` (o `window.open` se `target="_blank"`); un secondo clic sullo stesso bottone durante la costruzione naviga subito. `#ferma-drone` nasconde il drone e salva `localStorage.drone = "fermo"` (riletto al caricamento). Linee di corrente: `<svg id="corrente">` in `position:absolute` su `main`, una `line` (`#FBE36B`, 2 px, opacità .8) tra i centri di `[data-rete]` consecutivi della stessa rete, ricalcolate al `resize`.
- CSS: `@media (prefers-reduced-motion: reduce) { .oggetto, .crt::before, #drone img { animation: none } #drone { display: none } }`.

- [ ] **Step 1: Scrivere i test**

`tools/sito/test_sito.js`:

```js
const test = require("node:test");
const assert = require("node:assert");
const { intercetta, prossimaMeta, durataDa } = require("./sito.js");
const ev = (extra) => ({ button: 0, ctrlKey: false, metaKey: false, shiftKey: false, altKey: false,
  currentTarget: { dataset: { costruisci: "" } }, ...extra });
test("intercetta solo il clic semplice su un link da costruire", () => {
  assert.equal(intercetta(ev(), false), true);
  assert.equal(intercetta(ev({ button: 1 }), false), false);
  assert.equal(intercetta(ev({ ctrlKey: true }), false), false);
  assert.equal(intercetta(ev({ metaKey: true }), false), false);
  assert.equal(intercetta(ev({ shiftKey: true }), false), false);
  assert.equal(intercetta(ev(), true), false);
  assert.equal(intercetta(ev({ currentTarget: { dataset: {} } }), false), false);
});
test("la meta sta vicino a un rettangolo visibile", () => {
  const r = { x: 100, y: 100, w: 64, h: 64 };
  const m = prossimaMeta([r], { w: 800, h: 600 }, () => 0.5);
  assert.ok(m.x >= 52 && m.x <= 212 && m.y >= 52 && m.y <= 212, JSON.stringify(m));
  const vuota = prossimaMeta([], { w: 800, h: 600 }, () => 0.5);
  assert.ok(vuota.y > 300);
});
test("durata minima di un secondo", () => {
  assert.equal(durataDa(640, 64), 10);
  assert.equal(durataDa(10, 128), 1);
});
```

In `test_fabbrica.py`:

```python
def test_movimento_ridotto(self):
    with open(os.path.join(SITO, "stile.css"), encoding="utf-8") as fh:
        css = fh.read()
    self.assertIn("@media (prefers-reduced-motion: reduce)", css)
    self.assertIn("@keyframes oscilla", css)
```

- [ ] **Step 2: Eseguirli e vederli fallire**

Run: `node --test tools/sito/test_sito.js; python -m unittest tools/sito/test_fabbrica.py -k ridotto`
Expected: FAIL (funzioni non esportate / regola assente).

- [ ] **Step 3: Implementare drone, raggio, costruzione, linee di corrente, `#ferma-drone`, CSS**

`sito.js` fa tutto dentro `document.addEventListener("DOMContentLoaded", …)` e non tocca `document` al caricamento del modulo (così `require` in Node funziona senza DOM: proteggi con `if (typeof document !== "undefined")`).

- [ ] **Step 4: Eseguire i test e provare nel browser**

Run: `node --test tools/sito/test_sito.js && python -m unittest tools/sito/test_fabbrica.py tools/sito/test_costruisci.py && python tools/sito/costruisci.py`
Expected: OK. Nel browser: il drone vaga vicino ai nastri, un clic su "Catture" nel menu lo fa arrivare, raggio e riempimento in mezzo secondo, poi la pagina scorre all'ancora; Ctrl+clic su "Asset" apre la scheda nuova senza drone; con la riduzione del movimento (DevTools → Rendering) il drone sparisce e i link vanno subito.

- [ ] **Step 5: Commit**

```bash
git add tools/sito/sito.js tools/sito/test_sito.js tools/sito/stile.css tools/sito/costruisci.py tools/sito/test_fabbrica.py
git commit -m "feat(sito): drone che costruisce i bottoni, linee di corrente, riduzione del movimento"
```

### Task 8: Documenti, workflow, controllo finale e PR

**Files:**
- Modify: `.github/workflows/pages.yml` (checkout con `fetch-depth: 0` per `git describe`; `python -m unittest tools/sito/test_costruisci.py tools/sito/test_fabbrica.py tools/sito/test_documenti.py`; passo `node --test tools/sito/test_sito.js` con `actions/setup-node@v4`, `node-version: 22`), `docs/ARCHITETTURA.md` (riga di `tools/sito/`: tre pagine, `fabbrica.py`, `documenti.py`, `stile.css`, `sito.js`), `README.md` (riga della roadmap "Sito web su GitHub Pages": da "In corso" a "Fatto" con "vetrina-fabbrica, sviluppo, asset"), `CHANGELOG.md` (voce in testa), docstring di `tools/sito/costruisci.py` (pagine, fonti, script), `docs/mappa-progetto.canvas` (nodo del sito, come nelle tappe precedenti).
- Test: tutti.

- [ ] **Step 1: Test del contrasto della tavolozza**

In `test_fabbrica.py`:

```python
def test_contrasto(self):
    def lum(c):
        r, g, b = (int(c[i:i + 2], 16) / 255 for i in (1, 3, 5))
        f = lambda v: v / 12.92 if v <= 0.03928 else ((v + 0.055) / 1.055) ** 2.4
        return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b)
    def contrasto(a, b):
        la, lb = sorted((lum(a), lum(b)), reverse=True)
        return (la + 0.05) / (lb + 0.05)
    with open(os.path.join(SITO, "stile.css"), encoding="utf-8") as fh:
        css = fh.read()
    colori = dict(re.findall(r"--(bg|pan|testo|tenue|ambra|giallo):\s*(#[0-9a-fA-F]{6})", css))
    for sfondo in ("bg", "pan"):
        for testo in ("testo", "tenue", "ambra"):
            self.assertGreaterEqual(contrasto(colori[sfondo], colori[testo]), 4.5, (sfondo, testo))
    self.assertGreaterEqual(contrasto("#111111", colori["giallo"]), 4.5)
```

Run: `python -m unittest tools/sito/test_fabbrica.py -k contrasto` → PASS (altrimenti schiarisci `--tenue` finché passa).

- [ ] **Step 2: Aggiornare workflow e documenti** come nell'elenco dei file.

- [ ] **Step 3: Controllo completo**

Run: `python -m unittest tools/sito/test_costruisci.py tools/sito/test_fabbrica.py tools/sito/test_documenti.py && node --test tools/sito/test_sito.js && python tools/sito/costruisci.py && python -m unittest tools/art/test_genera.py`
Expected: tutto OK; `git status` pulito a parte `_site/` (ignorata).

- [ ] **Step 4: Catture per la PR** (browser a 1920, 1280 e 375 px: hero, Come funziona con gli oggetti sui nastri, drone che costruisce un bottone, Scarica col Core, pagina sviluppo, pagina asset) salvate nello scratchpad; caricate sul branch `pr-media` come nelle PR precedenti (memoria "Immagini nelle PR") e linkate nella descrizione.

- [ ] **Step 5: Commit, push, CI a mano, PR**

```bash
git add .github/workflows/pages.yml docs/ARCHITETTURA.md README.md CHANGELOG.md tools/sito/costruisci.py tools/sito/test_fabbrica.py docs/mappa-progetto.canvas
git commit -m "docs(sito): workflow Pages con tag e Node, architettura, README, changelog"
git push -u origin sito/fabbrica
"/c/Program Files/GitHub CLI/gh.exe" workflow run build-publish.yml --ref sito/fabbrica
```

Poi `gh run watch <id> --exit-status`, PR verso `main` in italiano con le catture e la riga `🤖 Generated with [Claude Code](https://claude.com/claude-code)`; dopo la fusione `gh workflow run pages.yml --ref main` per pubblicare. A fine piano: diario in `docs/diario/2026-10-18-sito-fabbrica.md` dal registro `.superpowers/sdd/…/progress.md` (regola di `CLAUDE.md`).
