"""Tests of the story map and its timeline (racconto.py): python -m unittest discover -s tools/sito."""
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import costruisci  # noqa: E402,F401  (puts tools/art on the path)
import composti  # noqa: E402
import fabbrica  # noqa: E402
import racconto  # noqa: E402

ASSETS = os.path.join(fabbrica.REPO, "godot", "assets")


class TestRacconto(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.dati = racconto.linea_del_tempo()

    def test_mappa_come_il_gioco(self):
        # The story map follows the same rules as the landing page: no overlaps, every chain ends in a consumer.
        self.assertEqual([], fabbrica.controlla(racconto.SCENA))
        self.assertEqual(["blocco", "smelter"], self.dati["fine"])
        # The ore leaves the miner through a corner, never straight out of it.
        primo = self.dati["costruzioni"][1]
        self.assertEqual("curva", primo["sprite"])
        celle = [c for c in self.dati["costruzioni"] if c["nastro"]]
        catena = next(c for c in fabbrica.catene(racconto.SCENA) if c["sorgente"] == ("blocco", "miner"))
        self.assertEqual(catena["celle"], [(c["x"], c["y"]) for c in celle])

    def test_linea_del_tempo(self):
        costruzioni = self.dati["costruzioni"]
        self.assertEqual("minatore", costruzioni[0]["id"])
        for prima, dopo in zip(costruzioni, costruzioni[1:]):
            self.assertLess(prima["t0"], dopo["t0"])   # built one after the other, in chain order
            self.assertLessEqual(prima["t1"], dopo["t0"])
        for c in costruzioni:
            self.assertTrue(0 <= c["piano"] <= c["t0"] < c["t1"] <= 1, c)
        tempi = [k["t"] for k in self.dati["camera"]]
        self.assertEqual(sorted(tempi), tempi)
        self.assertEqual((0, 1), (tempi[0], tempi[-1]))
        # The item path is made of axis-aligned steps of half a cell or one cell, from the miner to the smelter.
        percorso = self.dati["percorso"]
        self.assertEqual(len(costruzioni), len(percorso) - 1)
        for a, b in zip(percorso, percorso[1:]):
            self.assertIn(abs(b[0] - a[0]) + abs(b[1] - a[1]), (0.5, 1.0))
            self.assertTrue(a[0] == b[0] or a[1] == b[1])

    def test_fantasma_come_lo_shader(self):
        immagini = composti.Immagini(ASSETS)
        piano = racconto.fantasma(immagini("miner"))
        self.assertEqual(racconto.AMBRA + (255,), piano.getpixel((0, piano.height // 2)))   # amber outline
        r, g, b, a = piano.getpixel((piano.width // 2, piano.height // 2))
        self.assertLess(a, 70)   # see-through dark inside

    def test_esportazione_deterministica(self):
        immagini = composti.Immagini(ASSETS)
        letti = []
        for _ in range(2):
            with tempfile.TemporaryDirectory() as out:
                dati = racconto.esporta(out, immagini)
                cartella = os.path.join(out, "racconto")
                letti.append({n: open(os.path.join(cartella, n), "rb").read() for n in sorted(os.listdir(cartella))})
        self.assertEqual(letti[0], letti[1])
        self.assertEqual(racconto.FETTE, dati["sprite"]["drone"]["fotogrammi"])
        self.assertEqual(24 * racconto.INGRANDIMENTO, dati["sprite"]["drone"]["w"])


if __name__ == "__main__":
    unittest.main()
