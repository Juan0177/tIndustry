"""Tests for the sprite generator (stdlib unittest only).

Run from the repo root:  python -m unittest tools/art/test_genera.py
"""
import json
import os
import sys
import tempfile
import unittest

from PIL import Image, ImageChops

ART_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ART_DIR, "..", ".."))
if ART_DIR not in sys.path:
    sys.path.insert(0, ART_DIR)

import forme  # noqa: E402
import genera  # noqa: E402
from blocchi import SPRITE, sprite  # noqa: E402
from palette import ACCENTO, BAGLIORE, METALLO, NASTRO, NUCLEO, RAME, RAMPE  # noqa: E402

T = 32  # pixels per block cell
BELT_ITEM = 10  # side of the small item sprite drawn on belts
NASTRI = ("conveyor-basic", "conveyor-fast", "conveyor-express")  # belt tiers with scrolling strips

# Block footprints in cells, kept by hand: update it together with Factory.FootprintSize
# (src/TIndustry.Shared/Factory.cs) when a block changes size. The bridge is two 1x1 ends.
CELLE = {
    "miner": 2, "miner-advanced": 2, "smelter": 2, "assembler": 2, "generator": 2,
    "power-node-t2": 2,
    "extractor": 1, "power-node": 1, "conveyor-basic": 1, "conveyor-fast": 1,
    "conveyor-express": 1, "junction": 1, "splitter": 1, "sorter": 1, "conveyor-bridge": 1,
    "core-1": 2, "core-2": 3, "core-3": 4, "core-4": 5,
}
CORE_TIER = 4
CORE_LUCE_FOTOGRAMMI = 4
# Drone tiers seen from above, nose to the East: square sprites of this side (alpha, beta, gamma).
DRONI = {"drone-1": 24, "drone-2": 28, "drone-3": 32}
# Terrain (G2b): floor variants, deposit variants, dual-grid wall pieces (corner bits NO=1, NE=2,
# SE=4, SO=8), edge pieces in strip order, and the colour step allowed across a variant seam.
VARIANTI = 4
GIACIMENTI = 3
NO, NE, SE, SO = 1, 2, 4, 8
BORDI = ("N", "E", "S", "O", "NE", "SE", "SO", "NO")
SOGLIA_CUCITURA = 8.0


def _content():
    with open(os.path.join(REPO, "data", "content.json"), encoding="utf-8") as f:
        return json.load(f)


def _item_ids():
    return {m["itemId"] for m in _content()["market"]} | {"money"}


def fotogrammi_di(striscia):
    """Square frames of an "-anim" strip (side = strip height)."""
    h = striscia.height
    return [striscia.crop((i * h, 0, (i + 1) * h, h)) for i in range(striscia.width // h)]


def _righe(im, y0, y1):
    """Pixels of rows y0..y1 (inclusive)."""
    return im.crop((0, y0, im.width, y1 + 1)).tobytes()


def _minerali():
    """Ore item ids (one deposit kind each), from the item sprite table."""
    from blocchi.oggetti import OGGETTI
    return sorted(i for i, (tipo, _, _) in OGGETTI.items() if tipo == "minerale")


def _biomi_terra():
    """Land biomes: those with a wall (water floors are shared by every biome)."""
    return sorted({w["biome"] for w in _content()["terrain"]["walls"]})


def _misure_terreno():
    """Terrain sprite name -> expected size (G2b global constraints)."""
    terrain = _content()["terrain"]
    attese = {"acqua-bassa-anim": (4 * T, T), "acqua-profonda-anim": (4 * T, T)}
    for f in terrain["floors"]:
        attese |= {f"terreno-{f['id']}-{n}": (T, T) for n in range(VARIANTI)}
        attese[f"terreno-{f['id']}-bordo"] = (8 * T, T)
    attese |= {f"parete-{w['id']}": (16 * T, T) for w in terrain["walls"]}
    attese |= {f"giacimento-{m}-{n}": (T, T) for m in _minerali() for n in range(GIACIMENTI)}
    return attese


def errore_misura(nome, im):
    """Size problem of one generated sprite, or None when it is fine.

    Every name must be an item, a drone tier, a block (or block part) of CELLE or a terrain
    sprite: an unknown name is an error, never a silent pass."""
    w, h = im.size
    terreno = _misure_terreno()
    if nome in terreno:
        return None if (w, h) == terreno[nome] else f"{nome}: {w}x{h}, atteso {terreno[nome][0]}x{terreno[nome][1]}"
    if nome.startswith("deco-") and nome[len("deco-"):].rsplit("-", 1)[0] in _biomi_terra():
        return None if (w, h) == (T, T) else f"{nome}: {w}x{h}, atteso {T}x{T}"
    anim = nome.endswith("-anim")
    base = nome[: -len("-anim")] if anim else nome
    if anim and (h <= 0 or w % h != 0):
        # Sprites.Frames cuts strips into squares: frame side = strip height.
        return f"{nome}: larghezza {w} non multipla dell'altezza {h} (fotogrammi quadrati)"
    if base.endswith("-belt") and base[: -len("-belt")] in _item_ids():
        return None if (w, h) == (BELT_ITEM, BELT_ITEM) else f"{nome}: {w}x{h}, atteso {BELT_ITEM}x{BELT_ITEM}"
    if base in _item_ids():
        return None if (w, h) == (T, T) else f"{nome}: {w}x{h}, atteso {T}x{T}"
    if base in DRONI:
        lato = DRONI[base]
        return None if (w, h) == (lato, lato) else f"{nome}: {w}x{h}, atteso {lato}x{lato}"
    # Block or block part: "<id>" or "<id>-<parte>" with a known footprint.
    block = next((b for b in sorted(CELLE, key=len, reverse=True)
                  if base == b or base.startswith(b + "-")), None)
    if block is None:
        return f"{nome}: nome sconosciuto (né oggetto, né drone, né blocco di CELLE)"
    side = T * CELLE[block]
    if base.endswith("-span"):
        # Repeatable span texture (bridge): tiles a cell evenly, never taller than the block.
        ok = w > 0 and side % w == 0 and 0 < h <= side
        return None if ok else f"{nome}: {w}x{h}, attesa larghezza divisore di {side} e altezza <= {side}"
    frame_w = h if anim else w
    if h != side or frame_w != side:
        return f"{nome}: fotogramma {frame_w}x{h}, atteso {side}x{side}"
    return None


# Test-only sprites: exercise every shape and an animation strip.
def _prova():
    im = forme.nuova(2 * T, 2 * T)
    forme.pannello(im, 0, 0, 2 * T - 1, 2 * T - 1, METALLO)
    forme.griglia(im, 6, 6, 25, 25, METALLO, 3)
    forme.tubo(im, 30, 4, 60, 9, RAME)
    forme.ingranaggio(im, 44, 44, 12, 8, METALLO, 0.3)
    forme.cerchio(im, 16, 46, 7, RAME)
    forme.cerchio(im, 16, 46, 3, ACCENTO, pieno=False)
    forme.bagliore(im, 44, 20, 6, BAGLIORE)
    forme.bullone(im, 3, 3, METALLO)
    forme.rett(im, 30, 30, 33, 33, ACCENTO[2])
    forme.pixel(im, 1, 62, BAGLIORE[4])
    return forme.componi(forme.ombra(im, 90), im)


def _prova_anim():
    fotogrammi = []
    for k in range(4):
        im = forme.nuova(T, T)
        forme.ingranaggio(im, 16, 16, 12, 6, METALLO, k * 0.26)
        fotogrammi.append(im)
    return forme.striscia(fotogrammi)


TEST_SPRITES = {"miner-prova": _prova, "conveyor-basic-prova-anim": _prova_anim}


class TestGenera(unittest.TestCase):
    def setUp(self):
        self._saved = dict(SPRITE)
        for nome, fn in TEST_SPRITES.items():
            sprite(nome)(fn)

    def tearDown(self):
        SPRITE.clear()
        SPRITE.update(self._saved)

    def _genera(self, cartella):
        out = os.path.join(cartella, "assets")
        os.makedirs(out)
        nomi = genera.main(out_dir=out, tavola_dir=os.path.join(cartella, "arte"))
        return out, nomi

    def test_deterministico(self):
        with tempfile.TemporaryDirectory() as a, tempfile.TemporaryDirectory() as b:
            out_a, nomi_a = self._genera(a)
            out_b, nomi_b = self._genera(b)
            self.assertEqual(nomi_a, nomi_b)
            self.assertEqual(nomi_a, sorted(SPRITE))
            for nome in nomi_a:
                with open(os.path.join(out_a, nome + ".png"), "rb") as fa, \
                        open(os.path.join(out_b, nome + ".png"), "rb") as fb:
                    self.assertEqual(fa.read(), fb.read(), nome)

    def test_misure(self):
        errori = [e for nome in sorted(SPRITE) if (e := errore_misura(nome, SPRITE[nome]()))]
        self.assertEqual([], errori)

    def test_misure_rileva_errori(self):
        self.assertIsNotNone(errore_misura("miner", forme.nuova(T, T)))
        self.assertIsNotNone(errore_misura("iron-ore-belt", forme.nuova(T, T)))
        self.assertIsNotNone(errore_misura("smelter-anim", forme.nuova(5 * T, 2 * T + 1)))
        self.assertIsNone(errore_misura("smelter-anim", forme.nuova(6 * T, 2 * T)))
        self.assertIsNotNone(errore_misura("core-3", forme.nuova(3 * T, 3 * T)))
        self.assertIsNotNone(errore_misura("core-4-light-anim", forme.nuova(4 * 4 * T, 4 * T)))
        self.assertIsNone(errore_misura("core-4-light-anim", forme.nuova(4 * 5 * T, 5 * T)))
        self.assertIsNotNone(errore_misura("drone-2", forme.nuova(24, 24)))
        self.assertIsNone(errore_misura("drone-2", forme.nuova(28, 28)))
        self.assertIsNotNone(errore_misura("drone-2-anim", forme.nuova(3 * 28 + 1, 28)))
        self.assertIsNotNone(errore_misura("core-9", forme.nuova(T, T)))
        self.assertIsNotNone(errore_misura("sconosciuto", forme.nuova(T, T)))
        self.assertIsNotNone(errore_misura("parete-stone-wall", forme.nuova(T, T)))
        self.assertIsNone(errore_misura("parete-stone-wall", forme.nuova(16 * T, T)))
        self.assertIsNotNone(errore_misura("terreno-sand-bordo", forme.nuova(4 * T, T)))
        self.assertIsNotNone(errore_misura("terreno-lava-0", forme.nuova(T, T)))
        self.assertIsNotNone(errore_misura("deco-vulcano-0", forme.nuova(T, T)))

    def test_copertura_tier(self):
        # The four Core tiers with their light strip and the three drones.
        attesi = {f"core-{n}" for n in range(1, CORE_TIER + 1)}
        attesi |= {f"core-{n}-light-anim" for n in range(1, CORE_TIER + 1)} | set(DRONI)
        self.assertEqual([], sorted(attesi - set(SPRITE)))

    def test_core(self):
        lampade = []
        for n in range(1, CORE_TIER + 1):
            with self.subTest(tier=n):
                core = SPRITE[f"core-{n}"]()
                fotogrammi = fotogrammi_di(SPRITE[f"core-{n}-light-anim"]())
                self.assertEqual(CORE_LUCE_FOTOGRAMMI, len(fotogrammi))
                # The core body is opaque all over its footprint except the cut corners.
                self.assertEqual(255, core.getpixel((core.width // 2, 2))[3])
                for f in fotogrammi:
                    # Light frames are opaque discs over the nucleus: stacked on the icon they replace
                    # it, they never add to it (no double glow).
                    self.assertEqual({0, 255}, set(f.getchannel("A").tobytes()))
                    sopra = forme.componi(core, f)
                    self.assertEqual(sopra.getbbox(), core.getbbox())
                self.assertEqual(core.tobytes(), forme.componi(core, fotogrammi[0]).tobytes())
                self.assertNotEqual(fotogrammi[0].tobytes(), fotogrammi[2].tobytes(), "la luce non cambia")
                px = core.tobytes()
                lampade.append(sum(1 for i in range(0, len(px), 4) if tuple(px[i:i + 3]) == NUCLEO[-1]))
        # Each tier is grander: more lit cyan highlights than the one below.
        self.assertEqual(sorted(lampade), lampade)
        self.assertEqual(len(set(lampade)), len(lampade))

    def test_droni(self):
        sagome = []
        for nome, lato in DRONI.items():
            with self.subTest(drone=nome):
                im = SPRITE[nome]()
                alfa = im.getchannel("A")
                # Mirror-symmetric across the flight axis (West-East), nose on the East edge.
                self.assertEqual(alfa.tobytes(), alfa.transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes())
                x0, y0, x1, y1 = alfa.getbbox()
                self.assertEqual(lato, x1, f"{nome}: il muso non arriva al bordo Est")
                naso = [y for y in range(lato) if alfa.getpixel((x1 - 1, y))]
                self.assertTrue(naso and max(naso) - min(naso) <= 3, f"{nome}: muso non a punta")
                self.assertEqual((0, 0, 0, 0), im.getpixel((0, 0)))
                sagome.append(alfa.resize((32, 32)).tobytes())
        self.assertEqual(len(set(sagome)), len(sagome))

    def test_tavolozza(self):
        for nome, rampa in RAMPE.items():
            self.assertTrue(4 <= len(rampa) <= 6, nome)
            luma = [0.299 * r + 0.587 * g + 0.114 * b for r, g, b in rampa]
            self.assertEqual(sorted(luma), luma, f"{nome}: non va da scuro a chiaro")

    def test_strisce_cicliche(self):
        # Straight belt strips: 8 frames, the tread (rows 5-26) of each one is the previous one
        # moved 4 px along the flow (East), so frame 8 moved by 4 px is frame 1 again and the
        # loop has no seam; the rails (rows 0-4 and 27-31) stay still.
        for nastro in NASTRI:
            with self.subTest(nastro=nastro):
                self.assertIn(nastro + "-anim", SPRITE)
                fotogrammi = fotogrammi_di(SPRITE[nastro + "-anim"]())
                self.assertEqual(8, len(fotogrammi))
                for i, f in enumerate(fotogrammi):
                    dopo = fotogrammi[(i + 1) % len(fotogrammi)]
                    self.assertEqual(_righe(ImageChops.offset(f, 4, 0), 5, 26), _righe(dopo, 5, 26),
                                     f"{nastro}: fotogramma {i + 1} scorso di 4 px != fotogramma {(i + 1) % 8 + 1}")
                    for y0, y1 in ((0, 4), (27, 31)):
                        self.assertEqual(_righe(f, y0, y1), _righe(fotogrammi[0], y0, y1), f"{nastro}: binari mossi")

    def test_curve_raccordate(self):
        # Corner (in from the West edge, out from the South edge): its West column is column 0
        # of the straight frame and its South row is column 31 of it (row across the belt =
        # 31 - x there), frame by frame, so corner and straights join without a seam.
        for nastro in NASTRI:
            with self.subTest(nastro=nastro):
                dritti = fotogrammi_di(SPRITE[nastro + "-anim"]())
                curve = fotogrammi_di(SPRITE[nastro + "-corner-anim"]())
                self.assertEqual(len(dritti), len(curve))
                for k, (d, c) in enumerate(zip(dritti, curve)):
                    ovest = [c.getpixel((0, y)) for y in range(T)]
                    self.assertEqual([d.getpixel((0, y)) for y in range(T)], ovest, f"{nastro} {k + 1}: lato Ovest")
                    sud = [c.getpixel((x, T - 1)) for x in range(T)]
                    self.assertEqual([d.getpixel((T - 1, T - 1 - x)) for x in range(T)], sud, f"{nastro} {k + 1}: lato Sud")

    def test_parti_produzione_e_potenza(self):
        # Layered blocks: base, animated part (frame count), composed icon = base + frame 1.
        strati = {
            "miner": ("miner-drill-anim", 8), "miner-advanced": ("miner-advanced-drill-anim", 8),
            "smelter": ("smelter-glow-anim", 4), "generator": ("generator-rotor-anim", 4),
        }
        for blocco, (anim, n) in strati.items():
            with self.subTest(blocco=blocco):
                fotogrammi = fotogrammi_di(SPRITE[anim]())
                self.assertEqual(n, len(fotogrammi))
                icona = forme.componi(SPRITE[blocco + "-base"](), fotogrammi[0])
                self.assertEqual(icona.tobytes(), SPRITE[blocco]().tobytes())
        for nome in ("miner-advanced-lamp", "assembler-base", "assembler-arm", "assembler",
                     "extractor", "power-node", "power-node-t2"):
            self.assertIn(nome, SPRITE)
        # The drill bit is clearly smaller than its frame (not a fan filling the block).
        for anim in ("miner-drill-anim", "miner-advanced-drill-anim"):
            x0, y0, x1, y1 = fotogrammi_di(SPRITE[anim]())[0].getbbox()
            self.assertLessEqual(max(x1 - x0, y1 - y0), T, anim)
        # Extractor: empty dark well, 8 px across, in the centre for the filter dot: one colour
        # over the whole disc and exactly 8 px of it across the middle row and column.
        ex = SPRITE["extractor"]()
        pozzo = NASTRO[0] + (255,)
        disco = {ex.getpixel((x, y)) for y in range(T) for x in range(T)
                 if (x + 0.5 - T / 2) ** 2 + (y + 0.5 - T / 2) ** 2 <= 16}
        self.assertEqual({pozzo}, disco)
        self.assertEqual(8, sum(1 for x in range(T) if ex.getpixel((x, T // 2)) == pozzo))
        self.assertEqual(8, sum(1 for y in range(T) if ex.getpixel((T // 2, y)) == pozzo))

    def test_oggetti(self):
        # Every market item and money: HUD icon and belt sprite, opaque pixels only (no tint at
        # runtime, so no half-transparent wash), a closed outline and a look of its own.
        from blocchi.oggetti import BORDO_CHIARO, OGGETTI
        from palette import OUT
        self.assertEqual(_item_ids(), set(OGGETTI))
        for misura in ("", "-belt"):
            viste = []
            for item_id, (_, _, contorno) in OGGETTI.items():
                if misura and item_id in BORDO_CHIARO:
                    contorno = OUT  # dark outline, light rim inside (checked below)
                with self.subTest(sprite=item_id + misura):
                    im = SPRITE[item_id + misura]()
                    self.assertEqual({0, 255}, set(im.getchannel("A").tobytes()))
                    for y in range(im.height):
                        for x in range(im.width):
                            px = im.getpixel((x, y))
                            vicini = [im.getpixel((x + ax, y + ay))[3]
                                      if 0 <= x + ax < im.width and 0 <= y + ay < im.height else 0
                                      for ax, ay in ((1, 0), (-1, 0), (0, 1), (0, -1))]
                            if px[3] and 0 in vicini:
                                self.assertEqual(contorno, px[:3], f"{item_id}{misura}: ({x},{y}) fuori dal contorno")
                    viste.append(im.tobytes())
            self.assertEqual(len(set(viste)), len(viste))
        # Contrast on belts: a light rim right inside the dark outline.
        for item_id, colore in BORDO_CHIARO.items():
            with self.subTest(bordo=item_id):
                im = SPRITE[item_id + "-belt"]()
                bordo = [im.getpixel((x, y))[:3] for y in range(1, im.height - 1) for x in range(1, im.width - 1)
                         if im.getpixel((x, y))[3] and im.getpixel((x, y))[:3] != OUT
                         and OUT in [im.getpixel((x + ax, y + ay))[:3] for ax, ay in ((1, 0), (-1, 0), (0, 1), (0, -1))]]
                self.assertTrue(bordo)
                self.assertEqual({colore}, set(bordo))

    def test_copertura(self):
        # Every structure and item of content.json, every Core tier and every drone has a sprite.
        c = _content()
        attesi = {s["id"] for s in c["structures"]} | _item_ids()
        attesi |= {f"core-{n}" for n in range(1, CORE_TIER + 1)} | set(DRONI)
        reali = set(SPRITE) - set(TEST_SPRITES)
        self.assertEqual([], sorted(attesi - reali))

    def test_terreno_copertura(self):
        # Every floor has its variants and its edge strip, every wall its 16 dual-grid pieces,
        # every ore its deposit variants, every land biome at least 2 decorations, both waters
        # their animation; every floor and wall has its own ramp in palette.py.
        from palette import PARETI, SUOLI
        terrain = _content()["terrain"]
        attesi = set(_misure_terreno())
        self.assertEqual([], sorted(attesi - set(SPRITE)))
        for bioma in _biomi_terra():
            with self.subTest(bioma=bioma):
                self.assertGreaterEqual(sum(1 for n in SPRITE if n.startswith(f"deco-{bioma}-")), 2)
        self.assertEqual(sorted(f["id"] for f in terrain["floors"]), sorted(SUOLI))
        self.assertEqual(sorted(w["id"] for w in terrain["walls"]), sorted(PARETI))
        # Deposits and decorations are drawn over the floor: transparent around, the floor shows.
        for nome in sorted(n for n in SPRITE if n.startswith(("giacimento-", "deco-"))):
            with self.subTest(sprite=nome):
                alfa = SPRITE[nome]().getchannel("A").tobytes()
                pieni = sum(1 for a in alfa if a)
                self.assertGreater(pieni, 0)
                self.assertLessEqual(pieni, len(alfa) // 4, f"{nome}: copre troppo il suolo")

    def test_varianti_ripetibili(self):
        # A variant tiles with itself and with every other variant of its floor. Across each seam
        # (right edge next to left edge, bottom next to top): the two outer rings hold only the
        # noise tones 1-3 (no pebble, blade or glint cut by the edge), facing pixels differ at most
        # by the noise span, and the mean colour stays within a step.
        from palette import SUOLI

        def media(px):
            return [sum(p[i] for p in px) / len(px) for i in range(3)]

        def lati(im):
            rgb = im.convert("RGB")
            return {"destra": [rgb.getpixel((T - 1, y)) for y in range(T)],
                    "sinistra": [rgb.getpixel((0, y)) for y in range(T)],
                    "sotto": [rgb.getpixel((x, T - 1)) for x in range(T)],
                    "sopra": [rgb.getpixel((x, 0)) for x in range(T)]}

        def controlla_cucitura(nome, a, b, ampiezza):
            for lato_a, lato_b in (("destra", "sinistra"), ("sotto", "sopra")):
                pa, pb = lati(a)[lato_a], lati(b)[lato_b]
                scarto = max(abs(x - y) for x, y in zip(media(pa), media(pb)))
                self.assertLessEqual(scarto, SOGLIA_CUCITURA, f"{nome}: cucitura visibile ({scarto:.1f})")
                salto = max(abs(u - v) for p, q in zip(pa, pb) for u, v in zip(p, q))
                self.assertLessEqual(salto, ampiezza, f"{nome}: salto di colore sulla cucitura ({salto})")

        for f in _content()["terrain"]["floors"]:
            rampa = SUOLI[f["id"]]
            rumore = {tuple(rampa[k]) for k in (1, 2, 3)}
            ampiezza = max(abs(u - v) for u, v in zip(rampa[1], rampa[3]))
            varianti = [SPRITE[f"terreno-{f['id']}-{n}"]() for n in range(VARIANTI)]
            for n, a in enumerate(varianti):
                self.assertEqual({255}, set(a.getchannel("A").tobytes()), f"{f['id']}: suolo non opaco")
                anello = {a.getpixel((x, y))[:3] for y in range(T) for x in range(T)
                          if min(x, y, T - 1 - x, T - 1 - y) < 2}
                self.assertEqual(set(), anello - rumore, f"{f['id']}-{n}: dettagli tagliati dal bordo")
            self.assertEqual(VARIANTI, len({v.tobytes() for v in varianti}), f"{f['id']}: varianti uguali")
            for i, a in enumerate(varianti):
                for j, b in enumerate(varianti):
                    controlla_cucitura(f"{f['id']} {i}|{j}", a, b, ampiezza)
        # Water frames tile the same way and loop (frame 4 is followed by frame 1).
        from blocchi.terreno import ACQUA
        for suolo, nome in ACQUA.items():
            fotogrammi = fotogrammi_di(SPRITE[nome]())
            self.assertEqual(4, len(fotogrammi))
            self.assertEqual(4, len({fr.tobytes() for fr in fotogrammi}), f"{nome}: fotogrammi uguali")
            rampa = SUOLI[suolo]
            for fr in fotogrammi:
                controlla_cucitura(nome, fr, fr, max(abs(u - v) for u, v in zip(rampa[1], rampa[-1])))

    def test_parete_doppia_griglia(self):
        # 16 pieces indexed by the wall corners (NO=1, NE=2, SE=4, SO=8): 15 is full, 0 is empty,
        # a piece rotated a quarter turn clockwise is the piece of the rotated corners (opaque
        # pixels: the light stays top-left), and each edge strip (colours included) only depends
        # on its two corners, so neighbouring pieces always join.
        def ruota(i):  # corners after a clockwise quarter turn: NO->NE->SE->SO->NO
            return ((i << 1) | (i >> 3)) & 15

        def opachi(im):
            return Image.eval(im.getchannel("A"), lambda a: 255 if a == 255 else 0).tobytes()

        lati = {  # edge -> (box, its two corners)
            "N": ((0, 0, T, 1), NO | NE), "E": ((T - 1, 0, T, T), NE | SE),
            "S": ((0, T - 1, T, T), SO | SE), "O": ((0, 0, 1, T), NO | SO),
        }
        for w in _content()["terrain"]["walls"]:
            with self.subTest(parete=w["id"]):
                pezzi = fotogrammi_di(SPRITE[f"parete-{w['id']}"]())
                self.assertEqual(16, len(pezzi))
                self.assertEqual({255}, set(pezzi[15].getchannel("A").tobytes()))
                self.assertEqual({0}, set(pezzi[0].getchannel("A").tobytes()))
                for i in range(1, 15):
                    girato = pezzi[i].transpose(Image.Transpose.ROTATE_270)
                    self.assertEqual(opachi(girato), opachi(pezzi[ruota(i)]), f"pezzo {i} ruotato != pezzo {ruota(i)}")
                for lato, (box, angoli) in lati.items():
                    for k in (0, angoli & -angoli, angoli & (angoli - 1), angoli):
                        viste = {pezzi[i].crop(box).tobytes() for i in range(16) if i & angoli == k}
                        self.assertEqual(1, len(viste), f"lato {lato}: dipende da angoli lontani")

    def test_parete_margine_esatto(self):
        # The wall look reads the footprint up to MARGINE_PARETE px around the piece: there it must
        # not depend on the cells beyond the four corners (unknown to the piece).
        from blocchi.terreno import MARGINE_PARETE, _pieno
        m = MARGINE_PARETE
        for angoli in range(16):
            diversi = [(x, y) for y in range(-m, T + m) for x in range(-m, T + m)
                       if _pieno(angoli, x, y, esterni=False) != _pieno(angoli, x, y, esterni=True)]
            self.assertEqual([], diversi, f"pezzo {angoli}: dipende da celle fuori dal pezzo")

    def test_regola_dei_bordi(self):
        # Corner pieces only where no side neighbour already brings the same floor; lowest
        # priority first; a lower or equal neighbour draws nothing.
        from blocchi.terreno import PRIORITA, pezzi_bordo
        ordine = sorted(PRIORITA, key=PRIORITA.get)
        basso, altro, alto = ordine[2], ordine[-2], ordine[-1]
        vicini = dict.fromkeys(BORDI, basso)
        self.assertEqual([], pezzi_bordo(basso, vicini))
        self.assertEqual([(alto, BORDI.index("NE"))], pezzi_bordo(basso, vicini | {"NE": alto}))
        self.assertEqual([(alto, BORDI.index("N"))], pezzi_bordo(basso, vicini | {"N": alto, "NE": alto}))
        self.assertEqual([(alto, BORDI.index("N")), (alto, BORDI.index("E"))],
                         pezzi_bordo(basso, vicini | {"N": alto, "E": alto, "NE": alto}))
        self.assertEqual([(altro, BORDI.index("N")), (alto, BORDI.index("NE"))],
                         pezzi_bordo(basso, vicini | {"N": altro, "NE": alto}))
        self.assertEqual([], pezzi_bordo(alto, vicini | {"N": altro}))

    def test_finestra_composta_senza_segni(self):
        # Composed window: a low cell under a higher floor on N, E and NE (two side fringes meeting
        # in a corner). Every pixel of the cell is its own variant or the higher floor's texture
        # at that spot: no dark lip or seam where the fringes meet.
        from blocchi import terreno as tr
        from tavola import componi
        ordine = sorted(tr.PRIORITA, key=tr.PRIORITA.get)
        basso, alto = ordine[2], ordine[-1]
        b, a = (basso, None, None), (alto, None, None)
        im = componi([[b, a, a], [b, b, a], [b, b, b]], lambda nome: SPRITE[nome]())
        cella = im.crop((T, T, 2 * T, 2 * T))
        proprio = SPRITE[f"terreno-{basso}-{tr.hash_cella(1, 1, tr.SALE_VARIANTE) % VARIANTI}"]()
        trama = SPRITE[f"terreno-{alto}-0"]()
        estranei = [(x, y) for y in range(T) for x in range(T)
                    if cella.getpixel((x, y)) not in (proprio.getpixel((x, y)), trama.getpixel((x, y)))]
        self.assertEqual([], estranei)
        coperti = sum(1 for y in range(T) for x in range(T)
                      if cella.getpixel((x, y)) == trama.getpixel((x, y)) != proprio.getpixel((x, y)))
        self.assertGreater(coperti, 2 * T)  # the fringes are there

    def test_stili_terreno(self):
        # Every terrain id of content.json has its art parameters, and the ore deposits are
        # exactly the items of TerrainMap.ItemIdForDeposit (DepositKind) in the C# simulation.
        import re
        from blocchi import terreno as tr
        c = _content()["terrain"]
        self.assertEqual(sorted(f["id"] for f in c["floors"]), sorted(tr.STILI))
        self.assertEqual(sorted(w["id"] for w in c["walls"]), sorted(tr.STILI_PARETI))
        self.assertEqual(_biomi_terra(), sorted(tr.DENSITA_DECO))
        self.assertEqual(["acqua-bassa-anim", "acqua-profonda-anim"], sorted(tr.ACQUA.values()))
        with open(os.path.join(REPO, "src", "TIndustry.Shared", "Grid", "TerrainMap.cs"), encoding="utf-8") as f:
            sorgente = f.read()
        corpo = re.search(r"enum DepositKind\s*:\s*byte\s*\{([^}]*)\}", sorgente).group(1)
        tipi = [t.strip() for t in corpo.split(",") if t.strip() and t.strip() != "None"]
        mappa = dict(re.findall(r'DepositKind\.(\w+) => "([^"]+)"', sorgente.split("ItemIdForDeposit", 1)[1]))
        self.assertEqual(sorted(tipi), sorted(mappa))
        self.assertEqual(sorted(mappa.values()), tr.MINERALI)

    def test_fixture_terreno_aggiornata(self):
        # The C# test TerrainLookTests compares the client's composition plan with this fixture:
        # it must be what pezzi_mappa gives today (python tools/art/fixture_terreno.py rewrites it).
        import fixture_terreno
        with open(fixture_terreno.FIXTURE, encoding="utf-8") as f:
            self.assertEqual(fixture_terreno.contenuto(), f.read())

    def test_densita_deco_come_in_csharp(self):
        # TerrainLook.DecorationDensity (C#) is a copy of DENSITA_DECO: same biomes, same values.
        import re
        from blocchi import terreno as tr
        with open(os.path.join(REPO, "src", "TIndustry.Shared", "Grid", "TerrainLook.cs"), encoding="utf-8") as f:
            sorgente = f.read()
        corpo = re.search(r"DecorationDensity\s*=.*?\{(.*?)\};", sorgente, re.S).group(1)
        self.assertEqual(tr.DENSITA_DECO, {b: int(v) for b, v in re.findall(r'\["(\w+)"\]\s*=\s*(\d+)', corpo)})

    def test_carbone_si_legge(self):
        # Coal pebbles are lumps, not holes: on basalt (dark) and gravel (light) every pebble has a
        # lit pixel clearly lighter than both floors, and a body darker than gravel.
        from blocchi import terreno as tr
        lum = lambda c: (c[0] * 299 + c[1] * 587 + c[2] * 114) // 1000  # noqa: E731
        pavimenti = [SPRITE[f"terreno-{s}-0"]().convert("RGB") for s in ("basalt", "gravel")]
        chiaro_suolo = max(lum(c) for p in pavimenti for _, c in p.getcolors(T * T))
        for v in range(tr.GIACIMENTI):
            with self.subTest(variante=v):
                im = SPRITE[f"giacimento-coal-{v}"]()
                opachi = [(x, y) for y in range(T) for x in range(T) if im.getpixel((x, y))[3] == 255]
                luci = [p for p in opachi if lum(im.getpixel(p)) >= 130]
                self.assertGreaterEqual(len(luci), 5)  # one glint per pebble (5-7 pebbles)
                self.assertGreater(max(lum(im.getpixel(p)) for p in luci), chiaro_suolo - 40)
                self.assertLess(sum(lum(im.getpixel(p)) for p in opachi) / len(opachi), 100)  # still coal-dark

    def test_bordi(self):
        # Edge strip: 8 pieces N, E, S, O, NE, SE, SO, NO; sides and corners are quarter turns
        # of each other; a side piece touches its side only, a corner piece its corner only.
        for f in _content()["terrain"]["floors"]:
            with self.subTest(suolo=f["id"]):
                pezzi = dict(zip(BORDI, fotogrammi_di(SPRITE[f"terreno-{f['id']}-bordo"]())))
                alfa = {k: p.getchannel("A") for k, p in pezzi.items()}
                for a, b in (("N", "E"), ("E", "S"), ("S", "O"), ("O", "N"),
                             ("NE", "SE"), ("SE", "SO"), ("SO", "NO"), ("NO", "NE")):
                    self.assertEqual(alfa[a].transpose(Image.Transpose.ROTATE_270).tobytes(), alfa[b].tobytes(), f"{a}->{b}")
                self.assertEqual((0, 0, T, alfa["N"].getbbox()[3]), alfa["N"].getbbox())
                self.assertLessEqual(alfa["N"].getbbox()[3], T // 3)
                x0, y0, x1, y1 = alfa["NE"].getbbox()
                self.assertEqual((T, 0), (x1, y0))
                self.assertLessEqual(max(x1 - x0, y1 - y0), T // 3)

    def test_solo_sprite_generati(self):
        # Every PNG in the root of godot/assets comes from the registry: no hand-made or third-party
        # sprite can sneak back in (third-party interface icons live in godot/assets/ui/).
        cartella = os.path.join(REPO, "godot", "assets")
        png = {f[: -len(".png")] for f in os.listdir(cartella)
               if f.endswith(".png") and os.path.isfile(os.path.join(cartella, f))}
        self.assertEqual([], sorted(png - (set(SPRITE) - set(TEST_SPRITES))))


if __name__ == "__main__":
    unittest.main()
