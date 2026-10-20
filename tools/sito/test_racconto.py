"""Tests of the story map and its timeline (racconto.py): python -m unittest discover -s tools/sito."""
import os
import pathlib
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
        cls.nomi = sorted(racconto.sprite_del_racconto(composti.Immagini(ASSETS)))

    def test_mappa_come_il_gioco(self):
        # The story map follows the same rules as the landing page: no overlaps, every chain ends in a consumer or
        # leaves from the bottom edge towards the chapters to come.
        self.assertEqual([], fabbrica.controlla(racconto.SCENA))
        fini = sorted(str(c["fine"]) for c in fabbrica.catene(racconto.SCENA))
        self.assertEqual(sorted(["('blocco', 'smelter')", "('blocco', 'smelter')", "('blocco', 'generator')",
                                 "('sotto', 30)"]), fini)
        # Ore leaves a miner through a corner, never straight out of it.
        occupato = fabbrica._occupazione(racconto.SCENA)
        for c in fabbrica.catene(racconto.SCENA):
            if c["sorgente"] == ("blocco", "miner"):
                self.assertEqual("curva", occupato[c["celle"][0]][0].tipo)

    def test_linea_del_tempo(self):
        costruzioni = self.dati["costruzioni"]
        self.assertEqual("minatore-ferro-0", costruzioni[0]["id"])
        for prima, dopo in zip(costruzioni, costruzioni[1:]):
            self.assertLess(prima["t0"], dopo["t0"])   # built one after the other, in the drone's order
            self.assertLessEqual(prima["t1"], dopo["t0"])
        for c in costruzioni:
            self.assertTrue(0 <= c["piano"] <= c["t0"] < c["t1"] <= 1, c)
            for nome in (c["lavoro"], c["fermo"], c["piano_sprite"]):
                self.assertIn(nome, self.nomi)   # every picture the engine asks for is exported
            if "attivo" in c:
                self.assertGreaterEqual(c["attivo"], c["t1"])   # nothing works before it stands
        per_id = {c["id"]: c for c in costruzioni}
        # The smelter lights up only once both iron and coal arrive; the lamps only once their line is drawn.
        flussi = {f["oggetto"]: f for f in self.dati["flussi"]}
        forno = per_id["forno-0"]
        for f in self.dati["flussi"][:2]:
            self.assertGreaterEqual(forno["attivo"], max(costruzioni[i]["t1"] for i in f["celle"]))
        self.assertEqual(forno["attivo"], flussi["oggetto-iron-plate"]["avvio"])
        lampade = [c for c in costruzioni if c["lavoro"] == "lampada"]
        self.assertEqual(4, len(lampade))
        fine_linee = {(l["a"][0] - 0.5, l["a"][1] - 0.5): l["t1"] for l in self.dati["linee"]}
        for c in lampade:
            self.assertEqual(fine_linee[(c["x"], c["y"])], c["attivo"])
        tempi = [k["t"] for k in self.dati["camera"]]
        self.assertEqual(sorted(tempi), tempi)
        self.assertEqual((0, 1), (tempi[0], tempi[-1]))
        self.assertLess(self.dati["notte"]["t0"], self.dati["notte"]["t1"])
        self.assertLess(max(c["t1"] for c in costruzioni), self.dati["allarme"])
        # Item paths: axis-aligned steps of half a cell, one cell or (past the border) a cell and a half.
        for f in self.dati["flussi"]:
            self.assertEqual(len(f["celle"]), len(f["percorso"]) - 2)
            for a, b in zip(f["percorso"], f["percorso"][1:]):
                self.assertIn(abs(b[0] - a[0]) + abs(b[1] - a[1]), (0.5, 1.0, 1.5))
                self.assertTrue(a[0] == b[0] or a[1] == b[1])

    def test_fantasma_come_lo_shader(self):
        immagini = composti.Immagini(ASSETS)
        piano = racconto.fantasma(immagini("miner"))
        self.assertEqual(racconto.AMBRA + (255,), piano.getpixel((0, piano.height // 2)))   # amber outline
        r, g, b, a = piano.getpixel((piano.width // 2, piano.height // 2))
        self.assertLess(a, 70)   # see-through dark inside

    def test_drone_a_strati_diversi(self):
        # Real 2.5D, not a thicker coin: the hull is a dome, the eye stands above it, the ducts are stepped rings.
        modello = racconto.modello_drone(composti.Immagini(ASSETS)("drone-1"))
        alti = lambda parte: {q: a for q, (p, _, a) in modello.items() if p == parte}
        scafo, sensore, gondola = alti("scafo"), alti("sensore"), alti("gondola")
        self.assertGreater(max(scafo.values()), min(scafo.values()))      # taller towards the middle
        self.assertGreater(min(sensore.values()), max(scafo.values()))    # the sensor above the hull
        self.assertEqual({1, 2, 3}, set(gondola.values()))                # rim, inner band, hub
        self.assertTrue(all(b >= 1 for q, (p, b, _) in modello.items() if p == "gondola"))   # off the belly
        self.assertEqual(racconto.FETTE, max(a for _, _, a in modello.values()) + 1)

    def test_esportazione_deterministica(self):
        immagini = composti.Immagini(ASSETS)
        letti = []
        for _ in range(2):
            with tempfile.TemporaryDirectory() as out:
                dati = racconto.esporta(out, immagini)
                cartella = os.path.join(out, "racconto")
                letti.append({n: pathlib.Path(cartella, n).read_bytes() for n in sorted(os.listdir(cartella))})
        self.assertEqual(letti[0], letti[1])
        self.assertEqual(racconto.FETTE, dati["sprite"]["drone"]["fotogrammi"])
        self.assertEqual(24 * racconto.INGRANDIMENTO, dati["sprite"]["drone"]["w"])


if __name__ == "__main__":
    unittest.main()
