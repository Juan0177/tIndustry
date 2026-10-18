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

ASSETS = os.path.join(costruisci.REPO, "godot", "assets")


class TestSito(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls._tmp = tempfile.TemporaryDirectory()
        cls.out = cls._tmp.name
        cls.scritti = costruisci.costruisci(cls.out)
        with open(os.path.join(cls.out, "asset", "index.html"), encoding="utf-8") as fh:
            cls.asset = fh.read()
        with open(os.path.join(cls.out, "index.html"), encoding="utf-8") as fh:
            cls.ingresso = fh.read()
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

    def test_pagine(self):
        self.assertEqual(["asset/index.html", "index.html"], self.scritti)
        self.assertTrue(os.path.exists(os.path.join(self.out, ".nojekyll")))
        self.assertIn('href="asset/"', self.ingresso)

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

    def test_ambienti(self):
        # One composed window per land biome of content.json.
        with open(os.path.join(costruisci.REPO, "data", "content.json"), encoding="utf-8") as fh:
            biomi = {w["biome"] for w in json.load(fh)["terrain"]["walls"]}
        self.assertEqual(biomi, set(costruisci.biomi()))
        for bioma in biomi:
            self.assertRegex(self.asset, rf'data-bioma="{bioma}"><img src="data:image/png;base64,')
        self.assertIn('href="#ambienti"', self.asset)

    def test_deterministico(self):
        with tempfile.TemporaryDirectory() as altro:
            costruisci.costruisci(altro)
            with open(os.path.join(altro, "asset", "index.html"), encoding="utf-8") as fh:
                self.assertEqual(self.asset, fh.read())

    def test_autonome(self):
        # Self-contained and script-free: no scripts, external stylesheets or images (the landing page
        # captures are files of the site itself, in catture/).
        for pagina in (self.asset, self.ingresso):
            self.assertNotIn("<script", pagina)
            self.assertEqual([], re.findall(r'<img src="(?!data:|catture/[a-z0-9-]+\.png")', pagina))
            self.assertNotIn("<link", pagina)
            self.assertNotIn("url(http", pagina)

    def test_catture(self):
        # Every README capture listed is on the landing page, embedded, with its caption, under the section.
        inizio = self.ingresso.index('<section id="catture">')
        sezione = self.ingresso[inizio:self.ingresso.index("</section>", inizio)]
        self.assertIn('href="#catture"', self.ingresso)
        self.assertGreaterEqual(len(costruisci.CATTURE), 6)
        for nome, didascalia in costruisci.CATTURE:
            self.assertTrue(os.path.exists(os.path.join(costruisci.CATTURE_DIR, nome)), nome)
            self.assertIn(f'data-cattura="{nome}"><img src="catture/{nome}"', sezione)
            with open(os.path.join(self.out, "catture", nome), "rb") as copia:
                with open(os.path.join(costruisci.CATTURE_DIR, nome), "rb") as originale:
                    self.assertEqual(originale.read(), copia.read(), nome)
            self.assertIn(f"<figcaption>{didascalia}</figcaption>", sezione)
        self.assertEqual(len(costruisci.CATTURE), sezione.count("<figure"))
        self.assertLess(len(self.ingresso.encode("utf-8")), 200_000)

    def test_dimensione(self):
        self.assertLess(len(self.asset.encode("utf-8")), 3_000_000)


if __name__ == "__main__":
    unittest.main()
