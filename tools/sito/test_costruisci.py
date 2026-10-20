"""Tests of the website builder: python -m unittest tools/sito/test_costruisci.py"""
import json
import os
import re
import sys
import tempfile
import unittest

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import composti  # noqa: E402
import costruisci  # noqa: E402
import fabbrica  # noqa: E402

ASSETS = os.path.join(costruisci.REPO, "godot", "assets")


def _luminanza(colore):
    def canale(v):
        v /= 255
        return v / 12.92 if v <= 0.03928 else ((v + 0.055) / 1.055) ** 2.4
    r, g, b = (int(colore[i:i + 2], 16) for i in (1, 3, 5))
    return 0.2126 * canale(r) + 0.7152 * canale(g) + 0.0722 * canale(b)


def contrasto(a, b):
    la, lb = sorted((_luminanza(a), _luminanza(b)), reverse=True)
    return (la + 0.05) / (lb + 0.05)


class TestSito(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls._tmp = tempfile.TemporaryDirectory()
        cls.out = cls._tmp.name
        cls.scritti = costruisci.costruisci(cls.out)
        cls.pagine = {}
        for rel in ("index.html", "sviluppo/index.html", "asset/index.html"):
            with open(os.path.join(cls.out, rel), encoding="utf-8") as fh:
                cls.pagine[rel] = fh.read()
        cls.ingresso = cls.pagine["index.html"]
        cls.sviluppo = cls.pagine["sviluppo/index.html"]
        cls.asset = cls.pagine["asset/index.html"]
        cls.immagini = composti.Immagini(ASSETS)

    @classmethod
    def tearDownClass(cls):
        cls._tmp.cleanup()

    def _figure(self, nome):
        """Every figure (card or piece) of sprite `nome`, up to its caption."""
        return re.findall(rf'<figure[^>]*data-sprite="{re.escape(nome)}">(.*?)</figcaption>', self.asset, re.S)

    def _scheda(self, bid):
        inizio = self.asset.index(f'id="blocco-{bid}"')
        return self.asset[inizio:self.asset.index('class="lente"', inizio)]

    # ---- the three pages and their files

    def test_pagine(self):
        self.assertEqual(["asset/index.html", "index.html", "racconto/index.html", "sviluppo/index.html"], self.scritti)
        for nome in (".nojekyll", "stile.css", "sito.js", "favicon.png", "fabbrica", "catture"):
            self.assertTrue(os.path.exists(os.path.join(self.out, nome)), nome)
        for rel, radice in (("index.html", ""), ("sviluppo/index.html", "../"), ("asset/index.html", "../")):
            pagina = self.pagine[rel]
            self.assertIn(f'<link rel="stylesheet" href="{radice}stile.css">', pagina)
            self.assertIn(f'<script src="{radice}sito.js" defer></script>', pagina)
            self.assertIn(f'<link rel="icon" href="{radice}favicon.png"', pagina)
            self.assertIn('<html lang="it">', pagina)
            self.assertIn(f'href="{radice}sviluppo/"', pagina)
            self.assertIn(f'href="{radice}asset/"', pagina)
        with open(os.path.join(self.out, "stile.css"), encoding="utf-8") as fh:
            stile = fh.read()
        self.assertIn("#drone", stile)  # drone.css is part of the stylesheet
        self.assertIn(".scena", stile)

    def test_autonome(self):
        # Self-contained: one local script, local stylesheet and icon, images only data: or files of the site.
        for rel, pagina in self.pagine.items():
            radice = "" if rel == "index.html" else "../"
            self.assertEqual([f'<script src="{radice}sito.js" defer></script>'], re.findall(r"<script[^>]*></script>", pagina), rel)
            self.assertEqual(3, len(re.findall(r"<link\b", pagina)), rel)  # stylesheet, canonical, icon
            esterne = re.findall(rf'<img[^>]*src="(?!data:|{re.escape(radice)}(?:catture|fabbrica)/[a-z0-9./-]+")([^"]*)"', pagina)
            self.assertEqual([], esterne, rel)
            self.assertNotIn("url(http", pagina)
            self.assertNotIn("<iframe", pagina)

    def test_immagini_riferite_esistono(self):
        # Every fabbrica/ and catture/ file the pages use is on disk, and no file is a data: URI twin.
        for rel, pagina in self.pagine.items():
            radice = "" if rel == "index.html" else "../"
            for percorso in set(re.findall(rf'(?:src|url\()\(?["\']?{re.escape(radice)}((?:catture|fabbrica)/[a-z0-9./-]+)', pagina)):
                self.assertTrue(os.path.exists(os.path.join(self.out, percorso)), f"{rel}: {percorso}")

    def test_deterministico(self):
        with tempfile.TemporaryDirectory() as altro:
            costruisci.costruisci(altro)
            for rel, pagina in self.pagine.items():
                with open(os.path.join(altro, rel), encoding="utf-8") as fh:
                    self.assertEqual(pagina, fh.read(), rel)

    def test_dimensione(self):
        self.assertLess(len(self.ingresso.encode("utf-8")), 300_000)
        self.assertLess(len(self.sviluppo.encode("utf-8")), 300_000)
        self.assertLess(len(self.asset.encode("utf-8")), 3_000_000)

    def test_contrasto(self):
        # The palette of stile.css keeps text readable on the background (WCAG AA, 4.5:1).
        with open(os.path.join(self.out, "stile.css"), encoding="utf-8") as fh:
            radice = re.search(r":root\{([^}]*)\}", fh.read()).group(1)
        colori = dict(re.findall(r"--([a-z]+):(#[0-9a-f]{6})", radice))
        for nome in ("testo", "tenue", "ambra", "giallo"):
            self.assertGreaterEqual(contrasto(colori[nome], colori["bg"]), 4.5, nome)
            self.assertGreaterEqual(contrasto(colori[nome], colori["pan"]), 4.5, nome)

    # ---- the landing page

    def test_vetrina(self):
        pagina = self.ingresso
        ordine = [s.id for s in fabbrica.VETRINA]
        posizioni = [pagina.index(f'<section id="{sid}"') for sid in ordine]
        self.assertEqual(sorted(posizioni), posizioni)
        for sid in ordine:
            self.assertIn(f'<section id="{sid}"', pagina)
        self.assertIn('href="https://github.com/Juan0177/tIndustry/releases/latest"', pagina)
        self.assertGreaterEqual(pagina.count("data-costruisci"), 4)
        self.assertIn('<div id="drone-cantiere"', pagina)
        self.assertIn('<div id="avanza"', pagina)
        self.assertIn('class="pin"', pagina)  # the pinned hero
        self.assertIn('class="vetrina-blocchi"', pagina)
        self.assertNotIn("finestra-striscia", pagina)   # no sideways strip pinned to the scroll any more
        # The captures open over the page (#visore) and close again; the terrain spans the whole width of every biome section.
        self.assertEqual(1, pagina.count('<dialog id="visore"'))
        self.assertIn('<button type="button" class="visore-chiudi" autofocus>Chiudi</button>', pagina)
        self.assertEqual(sum(1 for s in costruisci.fabbrica.VETRINA if s.bioma), pagina.count('class="terra-larga"'))
        self.assertIn('property="og:image"', pagina)
        self.assertIn('name="description"', pagina)
        # Sotto attacco: five neon warnings, four impacts, the red-border section class.
        self.assertEqual(len(costruisci.AVVISI), pagina.count('<div class="avviso'))
        self.assertEqual(4, pagina.count('<div class="crepa"'))
        self.assertIn('class="sez sez-attacco"', pagina)
        # The numbers of the game come from data/.
        with open(os.path.join(costruisci.REPO, "data", "content.json"), encoding="utf-8") as fh:
            content = json.load(fh)
        self.assertIn(f"<b>{len(content['recipes'])}</b><span>ricette</span>", pagina)
        # Every capture listed is framed on the landing page with its caption, and copied.
        for nome, didascalia in costruisci.CATTURE:
            self.assertIn(f'<img src="catture/{nome}" alt=""', pagina)
            self.assertIn(f"<figcaption>{didascalia}</figcaption>", pagina.replace("&#x27;", "'"))
            self.assertTrue(os.path.exists(os.path.join(self.out, "catture", nome)), nome)
            with Image.open(os.path.join(self.out, "catture", nome)) as copia, \
                    Image.open(os.path.join(costruisci.CATTURE_DIR, nome)) as originale:
                self.assertEqual(originale.convert("RGBA").tobytes(), copia.convert("RGBA").tobytes(), nome)
        self.assertIn(f'<meta property="og:url" content="{costruisci.SITO_URL}">', pagina)
        self.assertIn(f'<meta property="og:url" content="{costruisci.SITO_URL}sviluppo/">', self.sviluppo)
        self.assertIn(f'<link rel="canonical" href="{costruisci.SITO_URL}asset/">', self.asset)
        # The download button carries the latest tag when the checkout has one.
        versione = costruisci.documenti.versione_release()
        self.assertIn(f"Scarica {versione}" if versione else "Scarica l'ultima release", pagina.replace("&#x27;", "'"))
        # Only the landing page has the drone and the construction links.
        for rel in ("sviluppo/index.html", "asset/index.html"):
            self.assertNotIn('<div id="drone-cantiere"', self.pagine[rel])
            self.assertNotIn("data-costruisci", self.pagine[rel])

    def test_racconto(self):
        # The story prototype: canvas, data, the poster for pages without the script, both scripts, noindex.
        with open(os.path.join(self.out, "racconto", "index.html"), encoding="utf-8") as fh:
            pagina = fh.read()
        self.assertIn('<canvas id="racconto-tela" aria-hidden="true"></canvas>', pagina)
        self.assertIn('<meta name="robots" content="noindex">', pagina)
        self.assertEqual(['<script src="../sito.js" defer></script>', '<script src="../racconto.js" defer></script>'],
                         re.findall(r"<script src[^>]*></script>", pagina))
        self.assertEqual(5, pagina.count('<section class="capitolo"'))
        self.assertIn('id="animazioni"', pagina)
        dati = json.loads(re.search(r'<script type="application/json" id="racconto-dati">(.*?)</script>', pagina, re.S).group(1))
        for nome in [dati["terreno"], dati["poster"]] + [s["file"] for s in dati["sprite"].values()]:
            self.assertTrue(os.path.isfile(os.path.join(self.out, "racconto", nome)), nome)
        for src in re.findall(r'src="\.\./([^"]+)"', pagina):
            self.assertTrue(os.path.isfile(os.path.join(self.out, src)), src)
        self.assertTrue(os.path.isfile(os.path.join(self.out, "racconto.js")))

    def test_prezzo_del_mercato(self):
        # The market price follows the line: 48 two-digit values, two digit wheels starting on the last one.
        m = re.search(r'class="prezzo" data-prezzi="([0-9,]+)"', self.ingresso)
        prezzi = [int(v) for v in m.group(1).split(",")]
        self.assertEqual(48, len(prezzi))
        self.assertTrue(all(1 <= v <= 99 for v in prezzi))
        self.assertGreater(len(set(prezzi)), 10)   # it really rises and falls
        cifre = re.findall(r'<span class="cifre" style="--c:(\d)">', self.ingresso)
        self.assertEqual(f"{prezzi[-1]:02d}", "".join(cifre))
        with open(os.path.join(self.out, "stile.css"), encoding="utf-8") as fh:
            self.assertIn("@keyframes scorre{to{transform:translateX(-480px)}}", fh.read())

    def test_animazioni(self):
        # The landing and sviluppo pages have the pause/play button in the bar, hidden until the script shows
        # it; the asset page has only GIFs, which nothing can stop, so no button.
        for rel, pagina in self.pagine.items():
            self.assertEqual(0 if rel.startswith("asset") else 1, pagina.count('id="animazioni"'), rel)
            self.assertNotIn("ferma-drone", pagina, rel)
        self.assertIn('class="animazioni" data-ferma="Ferma le animazioni" data-avvia="Avvia le animazioni" '
                      'aria-label="Ferma le animazioni" title="Ferma le animazioni" hidden>', self.ingresso)
        # The policy in the stylesheet: the belts and the scroll effects never stop for the system's
        # prefers-reduced-motion alone (only the flashes, the shake and the glass go); html.statico, set by
        # the button, stops everything and releases the pinned hero and the strip.
        with open(os.path.join(self.out, "stile.css"), encoding="utf-8") as fh:
            stile = fh.read()
        inizio = stile.index("@media (prefers-reduced-motion:reduce){\n html:not(.animato)")
        ridotto = stile[inizio:stile.index("\n}\n", inizio)]
        for vietato in ("animation-play-state", ".fermo", ".pin", "--p:", ".nastro"):
            self.assertNotIn(vietato, ridotto)
        for atteso in (".lampo{display:none}", ".crepa,", ".sez-attacco .pavimento{animation:none}", ".avviso{opacity:1;animation:none}"):
            self.assertIn(atteso, ridotto)
        self.assertIn("html.statico *,html.statico *::before,html.statico *::after{animation-play-state:paused!important", stile)
        self.assertIn("html.statico .nastro .fermo{display:block}", stile)
        self.assertIn("html.statico .pin{height:auto!important}", stile)   # beats .js .pin
        self.assertIn("html.statico .barra .animazioni .icona{", stile)   # the play icon
        self.assertIn(".barra .animazioni[hidden]{display:none}", stile)   # no button without the script
        # The capture inside its link fills the frame: without height:100% the height="720" attribute wins
        # and the frame shows a full-size corner of the capture.
        self.assertIn(".crt>img,.crt>a>img{flex:1;min-height:0;width:100%;height:100%;", stile)

    def test_schede_brevi(self):
        # README cells become one clause each: plain text, no entity cut in half, whole words at the end.
        self.assertEqual("Mappe generate dal seme con quattro biomi", costruisci._breve("Mappe generate dal seme (tre misure) con quattro biomi; pareti"))
        self.assertEqual("Scala UI solo dell&#x27;interfaccia",
                         costruisci._breve("Scala UI solo dell&#x27;interfaccia, colori per daltonici e alto contrasto", massimo=40))
        self.assertEqual("Tema unico, carattere pixel", costruisci._breve("<b>Tema</b> unico, carattere pixel, italiano e inglese", massimo=30))
        for _, testo in costruisci.documenti.contenuti():
            breve = costruisci._breve(testo)
            self.assertLessEqual(len(breve), 120, breve)
            self.assertNotIn("&#x27", breve.replace("&#x27;", ""))
            self.assertNotIn("<", breve)

    def test_sviluppo(self):
        for sid in ("come-e-fatto", "roadmap", "diario", "decisioni", "changelog"):
            self.assertIn(f'<section id="{sid}"', self.sviluppo)
        self.assertIn('<div class="tabella-scorri"><table class="tabella">', self.sviluppo)
        self.assertEqual(1, self.sviluppo.count("<h1"))
        self.assertIn('href="#changelog"', self.sviluppo)
        self.assertIn('class="nastro-testa"', self.sviluppo)
        self.assertNotIn("**", re.sub(r"<[^>]+>", "", self.sviluppo))

    # ---- the asset page (unchanged content)

    def test_ogni_sprite_compare(self):
        # Every registered sprite with a PNG is on the page: as a card, or as a piece of a block.
        attesi = [n for nomi in costruisci.famiglie(ASSETS).values() for n in nomi]
        self.assertGreater(len(attesi), 100)
        for nome in attesi:
            self.assertTrue(self._figure(nome), nome)

    def test_pezzi_solo_nella_modale(self):
        # A block's pieces of its own family are no separate cards: the grid has one card per block.
        for b in composti.BLOCCHI:
            for nome in costruisci.assorbiti(b):
                self.assertEqual([], re.findall(rf'<figure data-sprite="{re.escape(nome)}">', self.asset), nome)
            self.assertEqual(1, self.asset.count(f'id="blocco-{b.id}"'), b.id)
            self.assertIn(f'popovertarget="p-{b.id}"', self.asset)
            self.assertIn(f'<div popover id="p-{b.id}" class="modale" role="dialog" aria-labelledby="p-{b.id}-t">',
                          self.asset)
            self.assertIn(f'<h3 id="p-{b.id}-t">', self.asset)

    def test_animazioni_gif(self):
        # Every strip plays as a GIF wherever it is shown.
        for nome in costruisci.SPRITE:
            if nome.endswith("-anim"):
                for fig in self._figure(nome):
                    self.assertIn("data:image/gif;base64,", fig, nome)

    def test_blocchi_nel_registro(self):
        # Every block of the table exists in content.json (the Core tiers as the Core) and every layer and
        # piece is a registered sprite with its PNG.
        with open(os.path.join(costruisci.REPO, "data", "content.json"), encoding="utf-8") as fh:
            content = json.load(fh)
        ids = {s["id"] for s in content["structures"]}
        for b in composti.BLOCCHI:
            if not b.id.startswith("core-"):
                self.assertIn(b.id, ids)
            strati = {s.sprite for s in b.strati if s.sprite}
            for nome in strati | {n for n, _ in b.parti}:
                self.assertIn(nome, costruisci.SPRITE, f"{b.id}: {nome}")
                self.assertTrue(os.path.exists(os.path.join(ASSETS, nome + ".png")), nome)
            self.assertLessEqual(strati, {n for n, _ in b.parti}, b.id)
            self.assertTrue(os.path.exists(os.path.join(costruisci.REPO, b.fonte)), b.fonte)

    def test_ogni_stato_ha_immagine(self):
        for b in composti.BLOCCHI:
            scheda = self._scheda(b.id)
            for s in b.stati:
                img = re.findall(rf'<img[^>]*src="data:image/(\w+);base64,[^"]+"[^>]*'
                                 rf'data-stato="{re.escape(s.nome)}"', scheda)
                self.assertEqual(["gif" if s.durata else "png"], img, f"{b.id}: {s.nome}")
                fotogrammi = composti.fotogrammi_stato(b, s, self.immagini)
                if s.durata:
                    self.assertGreater(len(fotogrammi), 1, f"{b.id}: {s.nome}")
                    self.assertEqual(round(s.durata * 100), sum(d for _, d in fotogrammi) // 10, f"{b.id}: {s.nome}")
                    self.assertTrue(all(d >= 20 for _, d in fotogrammi), f"{b.id}: {s.nome}")
            if len(b.stati) > 1:
                self.assertEqual(len(b.stati), scheda.count('type="radio"'), b.id)

    def test_primo_fotogramma_uguale_icona(self):
        # tools/art builds `<id>.png` from the same layers: the first frame of the icon state, without what the
        # client draws in code, is the icon at the in-game scale.
        controllati = 0
        for b in composti.BLOCCHI:
            if not b.stato_icona:
                continue
            stato = next(s for s in b.stati if s.nome == b.stato_icona)
            im = composti.componi(b, stato, 0.0, self.immagini, grezzo=True)
            icona = self.immagini(b.icona)
            icona = icona.resize((icona.width * composti.SCALA, icona.height * composti.SCALA), Image.NEAREST)
            self.assertEqual(icona.tobytes(), im.tobytes(), b.id)
            controllati += 1
        self.assertGreaterEqual(controllati, 10)

    def test_braccia_assemblatore(self):
        # AssemblerVisual: open 0.28 of the span, closed 0.11, in whole art pixels; the cycle closes and reopens.
        b = next(x for x in composti.BLOCCHI if x.id == "assembler")
        sinistro = b.strati[1].sposta
        self.assertEqual((-36, 0), sinistro(0.0, frozenset()))
        self.assertEqual((-14, 0), sinistro(0.0, frozenset({"chiuso"})))
        ciclo = [sinistro(k * composti.TICK, frozenset({"ciclo"}))[0] for k in range(120)]
        self.assertEqual(-36, ciclo[0])
        self.assertEqual(-14, max(ciclo))
        self.assertEqual(-36, ciclo[-1])

    def test_nastri_un_fotogramma_per_pixel(self):
        # MindustryBeltVisual: the tier clock advances `rate` cells per second and shows frame
        # (int)(scroll * count) % count, count being the strip's frame count (one per pixel of tread). So the strip
        # plays at rate * count frames per second, read from the sprite, and one loop (one cell) lasts 1 / rate s.
        nastri = [b for b in composti.BLOCCHI if b.id in composti.VELOCITA_NASTRO and b.fps_parti]
        tier = [c for c in composti.VELOCITA_NASTRO if os.path.exists(os.path.join(ASSETS, c + "-anim.png"))]
        self.assertEqual(sorted(tier), sorted(b.id for b in nastri))
        for b in nastri:
            rate = composti.VELOCITA_NASTRO[b.id]
            for s in b.strati:
                n = len(self.immagini.fotogrammi(s.sprite))
                self.assertGreater(n, 8, s.sprite)
                self.assertEqual(rate * n, s.fps, s.sprite)
                self.assertEqual(rate * n, b.fps_parti[s.sprite], s.sprite)
            self.assertIn(f"{rate * n:g} fotogrammi al secondo", b.nota, b.id)
            self.assertNotIn(".", b.nota.split(":")[0], b.nota)  # decimal comma, as the interface
            for st in b.stati:
                if st.durata:
                    self.assertAlmostEqual(1 / rate, st.durata, msg=f"{b.id}: {st.nome}")

    def test_ambienti(self):
        # One composed window per land biome of content.json, on the asset page and on the landing page.
        with open(os.path.join(costruisci.REPO, "data", "content.json"), encoding="utf-8") as fh:
            biomi = {w["biome"] for w in json.load(fh)["terrain"]["walls"]}
        self.assertEqual(biomi, set(costruisci.biomi()))
        for bioma in biomi:
            self.assertRegex(self.asset, rf'data-bioma="{bioma}"><img src="data:image/png;base64,')
            self.assertIn(f'src="fabbrica/bioma-{bioma}.png"', self.ingresso)
        self.assertIn('href="#ambienti"', self.asset)


if __name__ == "__main__":
    unittest.main()
