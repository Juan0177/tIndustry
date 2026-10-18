"""Tests of the factory pieces of the website: python -m unittest tools/sito/test_fabbrica.py"""
import os
import sys
import unittest

from PIL import Image

SITO = os.path.dirname(os.path.abspath(__file__))
ASSETS = os.path.join(SITO, "..", "..", "godot", "assets")
sys.path.insert(0, SITO)
import composti  # noqa: E402
import fabbrica  # noqa: E402


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
        self.assertEqual(est[0].rotate(180).tobytes(),
                         fabbrica.fotogrammi_nastro(self.im, "conveyor-basic", "W")[0].tobytes())

    def test_curve(self):
        base = fabbrica.fotogrammi_curva(self.im, "conveyor-basic", "E", "S")  # clockwise from East: raw art
        grezzo = composti._ingrandisci(self.im.fotogrammi("conveyor-basic-corner-anim")[0])
        self.assertEqual(grezzo.tobytes(), base[0].tobytes())
        ws = fabbrica.fotogrammi_curva(self.im, "conveyor-basic", "W", "S")  # counter-clockwise from West: mirrored
        self.assertEqual(grezzo.transpose(Image.Transpose.FLIP_LEFT_RIGHT).tobytes(), ws[0].tobytes())
        self.assertEqual(grezzo.rotate(-90).tobytes(),
                         fabbrica.fotogrammi_curva(self.im, "conveyor-basic", "S", "W")[0].tobytes())
        with self.assertRaises(ValueError):
            fabbrica.fotogrammi_curva(self.im, "conveyor-basic", "E", "W")  # no turn

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
        self.assertNotEqual(fabbrica.sfondo_bioma(self.im, "neve").tobytes(),
                            fabbrica.sfondo_bioma(self.im, "prati").tobytes())

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

    def test_html_pezzi(self):
        f = fabbrica.Fabbrica(self.im)
        h = f.html_spina(fabbrica.Spina([fabbrica.Tratto("S", "coal", "conveyor-express", celle=2),
                                         fabbrica.Pezzo("sorter"), fabbrica.Pezzo("junction"),
                                         fabbrica.Ponte("S"), fabbrica.Nodo("power-node", "b"), fabbrica.Spazio(1)],
                                        lato="dx"))
        self.assertTrue(h.startswith('<div class="spina spina-dx">'), h[:40])
        self.assertIn('<span class="pezzo" data-pezzo="sorter">', h)
        self.assertEqual(1, h.count('class="filtro"'))
        self.assertIn('data-pezzo="junction"', h)
        self.assertRegex(h, r'<img class="ponte" src="data:image/gif;base64,[^"]+" width="64" height="256"')
        self.assertIn('class="nodo" data-rete="b"', h)
        self.assertIn('<i class="spazio" style="--celle:1"></i>', h)
        self.assertIn("--v-conveyor-express:128", f.stile())
        with self.assertRaises(ValueError):
            f.html_linea(fabbrica.Linea(["nastro"]))

    def test_css_e_js_dei_nastri(self):
        with open(os.path.join(SITO, "stile.css"), encoding="utf-8") as fh:
            css = fh.read()
        for k in ("@keyframes scorri-e", "@keyframes scorri-s", "@media (max-width: 719px)", ".spina{"):
            self.assertIn(k, css)
        with open(os.path.join(SITO, "sito.js"), encoding="utf-8") as fh:
            js = fh.read()
        self.assertIn("--durata", js)


if __name__ == "__main__":
    unittest.main()
