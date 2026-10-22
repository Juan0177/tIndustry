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
        self.assertEqual(sorted(["('blocco', 'smelter')", "('blocco', 'smelter')", "('blocco', 'smelter')",
                                 "('blocco', 'generator')", "('blocco', 'core-2')", "('blocco', 'core-2')",
                                 "('blocco', 'core-2')", "('sotto', 58)"]), fini)
        # Plates and copper meet in the T merge and run down to the Core: the Core is not right under the T.
        nel_core = [c for c in fabbrica.catene(racconto.SCENA) if c["fine"] == ("blocco", "core-2")]
        dalla_t = [c for c in nel_core if (30, 29) in c["celle"]]
        self.assertEqual(2, len(dalla_t))
        self.assertTrue(all(c["celle"].index((30, 29)) <= len(c["celle"]) - 4 for c in dalla_t))
        # Ore leaves a miner through a corner, never straight out of it.
        occupato = fabbrica._occupazione(racconto.SCENA)
        for c in fabbrica.catene(racconto.SCENA):
            if c["sorgente"] == ("blocco", "miner"):
                self.assertEqual("curva", occupato[c["celle"][0]][0].tipo)

    def test_linee_gia_piazzate(self):
        # More factory around: lines already there before the story, working from the first frame, never visited
        # by the drone; at least one only crosses the map (in from the top, out from the bottom).
        gia = [c for c in self.dati["costruzioni"] if c.get("fisso")]
        self.assertGreaterEqual(len(gia), 30)
        self.assertTrue(all(c["t1"] < 0 and c["piano"] < 0 for c in gia))
        flussi = [f for f in self.dati["flussi"] if f["avvio"] < 0]
        self.assertEqual(3, len(flussi))
        self.assertEqual({"oggetto-lead-ore", "oggetto-iron-ore", "oggetto-titanium-ore"}, {f["oggetto"] for f in flussi})
        self.assertTrue(any(f["percorso"][-1][1] > racconto.RIGHE for f in flussi))   # leaves past the bottom
        self.assertTrue(all(i < len(self.dati["costruzioni"]) and self.dati["costruzioni"][i].get("fisso")
                            for f in flussi for i in f["celle"]))

    def test_mercato(self):
        M, V = self.dati["mercato"], self.dati["valigetta"]
        ultimo = max(c["t1"] for c in self.dati["costruzioni"])
        # The story's chapters fit in their part of the scroll, the market takes the rest, in order:
        # soaring, the briefcase filling up, the throw through the open hatches, the blast, the crash, stonks.
        self.assertLess(ultimo, M["sale"][0])
        self.assertTrue(M["sale"][0] < V["conta"][0] < V["conta"][1] < V["lancio"][0])   # it fills while the price soars
        self.assertLess(M["sale"][1], V["lancio"][0])
        self.assertTrue(M["porte"][0] <= V["lancio"][0] < V["lancio"][1] < V["dentro"] <= M["porte"][1])
        # The blast comes from the crash: the price falls as a rocket-bomb and goes off when it hits the bottom,
        # which is the floor B; the camera follows it down.
        self.assertTrue(V["dentro"] <= M["crollo"][0] < M["crollo"][1] <= M["boom"][0] < M["boom"][1] <= M["meme"][0])
        self.assertTrue(M["prezzo"]["basso"] < M["prezzo"]["base"] < M["prezzo"]["alto"])   # floor B, usual, spike
        x0, x1 = M["caduta"]
        self.assertTrue(0 < x0 < x1 < M["interno"][2])
        self.assertTrue(0 < racconto.y_prezzo(M["prezzo"]["alto"]) < racconto.y_prezzo(M["prezzo"]["basso"]) < M["interno"][3])
        seguito = [c for c in self.dati["camera"] if M["crollo"][0] <= c["t"] <= M["crollo"][1]]
        self.assertGreaterEqual(len(seguito), 3)
        self.assertTrue(all(c["vista"] <= 8 for c in seguito))
        self.assertEqual(sorted(c["y"] for c in seguito), [c["y"] for c in seguito])   # going down with it
        self.assertTrue(M["meme"][0] < M["meme"][1] <= M["bonus"][0] < M["bonus"][1] <= 1)   # the bonus after stonks
        self.assertIn("bonus", self.nomi)
        self.assertEqual((23, 30, 69), (M["prezzo"]["basso"], M["prezzo"]["base"], M["prezzo"]["alto"]))   # B, 1.3 B, 3 B
        self.assertEqual(777, racconto.BONUS)
        # Then the drone changes channel: the bonus first, the click, the switch, the game's channel before the end
        # of the scroll (its buttons are HTML over the screen, racconto.js places them on BOTTONI_TV).
        C = M["canale"]
        self.assertTrue(M["bonus"][1] <= C["tasto"] < C["cambio"][0] < C["cambio"][1] < 1)
        self.assertIn("canale", self.nomi)
        self.assertIn("canale-osd", self.nomi)
        x0, y0, x1, y1 = racconto.BOTTONI_TV
        self.assertTrue(0 <= x0 < x1 <= M["interno"][2] and 0 <= y0 < y1 <= M["interno"][3])
        sx, sy = C["sensore"]   # the remote's sensor sits on the frame, outside the screen
        self.assertFalse(M["interno"][0] <= sx < M["interno"][0] + M["interno"][2] and M["interno"][1] <= sy < M["interno"][1] + M["interno"][3])
        tappe = self.dati["drone"]["tappe"]
        self.assertEqual(sorted(t["t"] for t in tappe), [t["t"] for t in tappe])
        mercato = [p for p in tappe if p["t"] > ultimo]
        self.assertEqual(racconto.TAPPE_MERCATO, mercato)
        canale = mercato[-1]   # with the remote, over the top left corner of the frame (8 art px below the cell)
        self.assertLessEqual(canale["y"], M["y"] + 8 / racconto.CELLA)
        self.assertTrue(M["x"] - 1 <= canale["x"] <= M["x"] + 1)
        for nome in ("schermo", "valigetta", "stonks", "mercato-titolo"):
            self.assertIn(nome, self.nomi)

    def test_unione_a_t_su_nastro_veloce(self):
        # Plates and copper meet in the T and share the belt down to the Core: that part is T2 (two full T1 lines
        # carry more than a T1 holds), the items speed up there and the copper is half a spacing behind the plates,
        # so on the merged belt they alternate instead of covering each other.
        veloci = [c for c in self.dati["costruzioni"] if c["lavoro"] in ("nastro-veloce", "t-veloce")]
        self.assertEqual({"t-veloce"}, {c["lavoro"] for c in veloci if (c["x"], c["y"]) == (30, 29)})
        self.assertEqual(5, len(veloci))
        uniti = [f for f in self.dati["flussi"] if "unione" in f]
        self.assertEqual(["oggetto-iron-plate", "oggetto-copper-ore"], [f["oggetto"] for f in uniti])
        k = fabbrica.RATE[fabbrica.T2] / fabbrica.RATE[fabbrica.T1]
        for f in uniti:
            self.assertAlmostEqual(k, f["unione"]["k"], places=3)
            s, punto = 0.0, None
            for a, b in zip(f["percorso"], f["percorso"][1:]):   # the merge starts at the T's centre
                if s == f["unione"]["s"]:
                    punto = a
                s += abs(b[0] - a[0]) + abs(b[1] - a[1])
            self.assertEqual([30.5, 29.5], punto)
        # where each flow's items cross the T's centre, as a fraction of a spacing: half apart
        passaggio = [(f["unione"]["s"] - f["sfasa"]) % 1 for f in uniti]
        self.assertAlmostEqual(0.5, abs(passaggio[0] - passaggio[1]), places=3)
        for nome in ("nastro-veloce", "t-veloce", "nastro-veloce-piano", "t-veloce-piano"):
            self.assertIn(nome, self.nomi)

    def test_primo_piano_dell_attacco(self):
        # The close-up: cinema bars, the drone at its guard post on the river bank facing east, it sees the
        # silhouettes, trembles, the helmet drops; the ice breaks (it calms down, the bars go) before dawn.
        A, E, N = self.dati["attacco"], self.dati["elmetto"], self.dati["notte"]
        b = A["barre"]
        self.assertTrue(N["t1"] <= b[0] < b[1] < A["vede"] <= A["trema"][0] < E["cade"] < E["posato"] < A["trema"][1])
        self.assertTrue(A["trema"][1] <= b[2] < b[3] < N["alba0"])
        ultimo = max(c["t1"] for c in self.dati["costruzioni"])
        guardia = [p for p in self.dati["drone"]["tappe"] if p["t"] < ultimo]
        self.assertEqual(3, len(guardia))
        posto = guardia[-1]
        self.assertTrue(guardia[1]["t"] <= A["vede"] and posto["t"] >= N["alba0"] - 0.05)   # there all along
        self.assertGreater(posto["guarda"][0], posto["x"])   # facing east, where they come from
        vicini = [n for n in self.dati["nemici"]
                  if abs(n["y"] - posto["y"]) < 5 and min(k[1] for k in n["chiavi"]) - posto["x"] < 7]
        self.assertGreaterEqual(len(vicini), 3)   # in the same shot as the drone
        primo_piano = [k for k in self.dati["camera"] if b[1] <= k["t"] <= b[2]]
        self.assertTrue(primo_piano and all(k["vista"] <= 14 for k in primo_piano))
        for k in primo_piano:   # the drone left of the middle, the enemies' stops right of it, inside the frame
            self.assertLess(posto["x"], k["x"])
            for n in vicini:
                self.assertTrue(k["x"] < n["chiavi"][1][1] < k["x"] + k["vista"] / 2)

    def test_linea_del_tempo(self):
        costruzioni = [c for c in self.dati["costruzioni"] if not c.get("fisso")]
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
        # Night, attack and dawn (roadmap «Attacco e difesa»: enemies only at night, the survivors flee at dawn).
        N, A, E = self.dati["notte"], self.dati["allarme"], self.dati["elmetto"]
        self.assertTrue(N["t0"] < N["t1"] <= A["t0"] < N["alba0"] < N["alba1"])
        self.assertTrue(A["t0"] <= E["cade"] < E["posato"] < E["toglie"] < E["via"])
        self.assertGreaterEqual(E["toglie"], N["alba0"])   # the helmet stays on until the day comes back
        lampade_accese = max(c["attivo"] for c in costruzioni if c["lavoro"] == "lampada")
        self.assertLess(lampade_accese, N["t1"])
        for n in self.dati["nemici"]:
            tempi_n = [k[0] for k in n["chiavi"]]
            self.assertEqual(sorted(tempi_n), tempi_n)
            self.assertGreaterEqual(tempi_n[0], N["t1"])            # they come only once it is dark
            self.assertGreater(tempi_n[-2], N["alba0"] - 0.05)       # still there until dawn...
            self.assertGreaterEqual(n["chiavi"][-1][1], racconto.LARGHEZZA)   # ...then they leave the map
            x_vicino = min(k[1] for k in n["chiavi"])
            self.assertGreater(x_vicino, max(c["x"] for c in costruzioni))   # never inside the factory
            self.assertGreater(x_vicino, self.dati["mercato"]["x"] + self.dati["mercato"]["w"])
        self.assertEqual("nucleo", self.dati["fissi"][0]["sprite"])   # the Core is there from the start
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
        self.assertEqual(racconto.FETTE_ELMETTO, dati["sprite"]["elmetto"]["fotogrammi"])
        self.assertEqual(24 * racconto.INGRANDIMENTO, dati["sprite"]["drone"]["w"])


if __name__ == "__main__":
    unittest.main()
