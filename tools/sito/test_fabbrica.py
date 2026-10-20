"""Tests of the factory floor of the landing page: python -m unittest tools/sito/test_fabbrica.py"""
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import composti  # noqa: E402
import fabbrica  # noqa: E402

ASSETS = os.path.join(fabbrica.REPO, "godot", "assets")


class TestFabbrica(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls._tmp = tempfile.TemporaryDirectory()
        cls.out = cls._tmp.name
        cls.archivio = fabbrica.Archivio(composti.Immagini(ASSETS), cls.out, radice="")
        cls.html = {s.id: fabbrica.html_scena(s, cls.archivio) for s in fabbrica.VETRINA}

    @classmethod
    def tearDownClass(cls):
        cls._tmp.cleanup()

    def test_curve_come_il_client(self):
        # BeltCornerTile.ResolveTransform: the base art enters from West and exits South (a clockwise turn seen
        # from the East heading). Clockwise turns rotate from the heading; counter-clockwise ones mirror first.
        self.assertEqual((False, 0), fabbrica.trasformazione_curva("E", "S"))
        self.assertEqual((False, 90), fabbrica.trasformazione_curva("S", "W"))
        self.assertEqual((False, 180), fabbrica.trasformazione_curva("W", "N"))
        self.assertEqual((False, -90), fabbrica.trasformazione_curva("N", "E"))
        self.assertEqual((True, 0), fabbrica.trasformazione_curva("W", "S"))
        self.assertEqual((True, -90), fabbrica.trasformazione_curva("S", "E"))
        self.assertEqual((True, 180), fabbrica.trasformazione_curva("E", "N"))
        self.assertEqual((True, 90), fabbrica.trasformazione_curva("N", "W"))
        with self.assertRaises(ValueError):
            fabbrica.trasformazione_curva("E", "W")

    def test_scene_senza_sovrapposizioni_e_catene_complete(self):
        # Every chain starts at a block that produces (or at the top edge, coming from the scene above) and ends
        # in a block that consumes, in the Core, or at the bottom edge; no cell holds two solid elements.
        for scena in fabbrica.VETRINA:
            problemi = fabbrica.controlla(scena)
            self.assertEqual([], problemi, scena.id)
            for catena in fabbrica.catene(scena):
                self.assertIn(catena["fine"][0], ("blocco", "sotto"), f"{scena.id}: {catena}")

    def test_catene_continuano_fra_le_scene(self):
        # What leaves a scene from the bottom edge enters the next one from the top edge, same column, heading S.
        uscite = None
        for scena in fabbrica.VETRINA:
            entrate = fabbrica.entrate_dall_alto(scena)
            if uscite is not None:
                self.assertEqual(uscite, entrate, scena.id)
            uscite = fabbrica.uscite_dal_basso(scena)
        self.assertEqual(set(), uscite)  # the last scene (Scarica) keeps everything in the Core

    def test_ogni_oggetto_e_un_oggetto_del_gioco(self):
        # Items on the belts are real items with their belt sprite; every belt run carries something.
        for scena in fabbrica.VETRINA:
            for e in scena.elementi:
                if isinstance(e, fabbrica.Tratto):
                    self.assertTrue(e.oggetti, f"{scena.id}: tratto {e.x},{e.y}")
                    for o in e.oggetti:
                        self.assertTrue(os.path.exists(os.path.join(ASSETS, o + "-belt.png")), o)

    def test_oggetti_secondo_le_ricette(self):
        # What leaves a block is what the game makes there: the ore of the deposit under a miner, a plate out of
        # a smelter (smelt-* recipes), a craft-* output out of an assembler (data/content.json).
        ricette = composti._CONTENT["recipes"]
        fusi = {o["itemId"] for r in ricette if r["id"].startswith("smelt-") for o in r["outputs"]}
        assemblati = {o["itemId"] for r in ricette if r["id"].startswith("craft-") for o in r["outputs"]}
        controllate = 0
        for scena in fabbrica.VETRINA:
            occupato = fabbrica._occupazione(scena)
            for catena in fabbrica.catene(scena):
                if catena["sorgente"][0] != "blocco":
                    continue
                tratti = [occupato[c][0] for c in catena["celle"] if isinstance(occupato[c][0], fabbrica.Tratto)]
                if not tratti:
                    continue
                oggetti, bid = set(tratti[0].oggetti), catena["sorgente"][1]
                if bid.startswith("miner"):
                    primo = catena["celle"][0]
                    h = fabbrica._ingresso(occupato[primo][0], primo)
                    dietro = (primo[0] - fabbrica.VERSI[h][0], primo[1] - fabbrica.VERSI[h][1])
                    minatore = occupato[dietro][0]
                    giacimenti = {g.minerale for g in scena.elementi if isinstance(g, fabbrica.Giacimento)
                                  and g.x <= minatore.x and minatore.x + 1 < g.x + g.w
                                  and g.y <= minatore.y and minatore.y + 1 < g.y + g.h}
                    self.assertEqual(giacimenti, oggetti, f"{scena.id}: minatore in {minatore.x},{minatore.y}")
                elif bid == "smelter":
                    self.assertLessEqual(oggetti, fusi, f"{scena.id}: forno")
                elif bid == "assembler":
                    self.assertLessEqual(oggetti, assemblati, f"{scena.id}: assemblatore")
                controllate += 1
        self.assertGreaterEqual(controllate, 8)

    def test_titanio_solo_col_minatore_t2(self):
        # DepositDef hardness: titanium (2) needs a tier-2 miner; every miner sits on ore it can dig.
        durezza = {d["id"]: d.get("hardness", 0) for d in composti._CONTENT["terrain"]["deposits"]}
        tier = {b["id"]: b.get("tier", 0) for b in composti._CONTENT["buildings"]}
        for scena in fabbrica.VETRINA:
            for b in scena.elementi:
                if isinstance(b, fabbrica.Blocco) and b.id.startswith("miner"):
                    for g in scena.elementi:
                        if isinstance(g, fabbrica.Giacimento) and g.x <= b.x < g.x + g.w and g.y <= b.y < g.y + g.h:
                            self.assertGreaterEqual(tier[b.id], durezza[g.minerale.replace("-ore", "")], f"{scena.id}: {b.id} su {g.minerale}")

    def test_pezzi_della_vetrina(self):
        # The landing page shows every logistics piece of the game at least once.
        tipi = {e.tipo for s in fabbrica.VETRINA for e in s.elementi if isinstance(e, fabbrica.Pezzo)}
        self.assertLessEqual({"curva", "t", "incrocio", "selezionatore", "sdoppiatore", "ponte", "unione_edificio", "unione"}, tipi)
        # The T merge above the Core takes both long chains: two chains end in the Core through the same cell.
        scarica = next(sc for sc in fabbrica.VETRINA if sc.id == "scarica")
        nel_core = [c for c in fabbrica.catene(scarica) if c["fine"] == ("blocco", "core-3")]
        self.assertEqual(2, len(nel_core))
        self.assertTrue(all((8, 1) in c["celle"] for c in nel_core))
        # ...and shows what both belts carry: items in from the west and from the east, taking turns.
        t = next(e for e in scarica.elementi if isinstance(e, fabbrica.Pezzo) and e.tipo == "t")
        da_ovest = next(e for e in scarica.elementi if isinstance(e, fabbrica.Tratto) and e.verso == "E" and e.y == 1)
        da_est = next(e for e in scarica.elementi if isinstance(e, fabbrica.Tratto) and e.verso == "W" and e.y == 1)
        for verso, nastro in (("WS", da_ovest), ("ES", da_est)):
            portati = {o for v, o, _ in t.flussi if v == verso}
            self.assertTrue(portati)
            self.assertLessEqual(portati, set(nastro.oggetti))
        html = fabbrica.html_scena(scarica, self.archivio)
        self.assertIn("animation-name:va-ws", html)
        self.assertIn("animation-name:va-es", html)
        tier = {e.tier for s in fabbrica.VETRINA for e in s.elementi if isinstance(e, fabbrica.Tratto)}
        self.assertEqual({"conveyor-basic", "conveyor-fast"}, tier)

    def test_corrente_e_fedelta(self):
        # A powered miner (lamp on) has a power line reaching it; every scene with a node is wired to the margin.
        for scena in fabbrica.VETRINA:
            alimentati = [e for e in scena.elementi if isinstance(e, fabbrica.Blocco) and "con corrente" in e.stato]
            punti = {p for e in scena.elementi if isinstance(e, fabbrica.Linea) for p in e.punti}
            for b in alimentati:
                vicino = any(b.x - 1 <= px <= b.x + 3 and b.y - 1 <= py <= b.y + 3 for px, py in punti)
                self.assertTrue(vicino, f"{scena.id}: {b.id} in {b.x},{b.y} senza linea")

    def test_immagini_scritte_e_deterministiche(self):
        # Scene images are files of the site (fabbrica/…), each written once, same bytes on a second build.
        nomi = sorted(os.listdir(os.path.join(self.out, "fabbrica")))
        self.assertGreater(len(nomi), 40)
        for s in fabbrica.VETRINA:
            self.assertIn('class="scena', self.html[s.id])
            self.assertNotIn("data:image", self.html[s.id])
        with tempfile.TemporaryDirectory() as altro:
            archivio = fabbrica.Archivio(composti.Immagini(ASSETS), altro, radice="")
            for s in fabbrica.VETRINA:
                self.assertEqual(self.html[s.id], fabbrica.html_scena(s, archivio))
            for nome in nomi:
                with open(os.path.join(self.out, "fabbrica", nome), "rb") as a, \
                        open(os.path.join(altro, "fabbrica", nome), "rb") as b:
                    self.assertEqual(a.read(), b.read(), nome)

    def test_velocita_dei_nastri(self):
        # One loop of the tread is one cell: 1 / rate seconds (basic 2 s), and items cross n cells in n / rate s.
        html = self.html["come-funziona"]
        self.assertIn("--giro:2s", html)
        self.assertIn("--dur:6.000s", html)  # the three-cell run of iron ore on the dorsale
        self.assertIn("nastro-conveyor-basic-s.gif", html)  # the real strip: rails still, tread moving
        self.assertIn("nastro-conveyor-basic-s-fermo.png", html)  # the still frame for reduced motion


if __name__ == "__main__":
    unittest.main()
