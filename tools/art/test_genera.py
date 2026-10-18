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
from blocchi import FOTOGRAMMA, SPRITE, sprite  # noqa: E402
from palette import ACCENTO, BAGLIORE, METALLO, NUCLEO, RAME, RAMPE  # noqa: E402

T = 32  # pixels per block cell
BELT_ITEM = 10  # side of the small item sprite drawn on belts
NASTRI = ("conveyor-basic", "conveyor-fast", "conveyor-express")  # belt tiers with scrolling strips

# Block footprints in cells (Factory.FootprintSize; the bridge is two 1x1 ends).
CELLE = {
    "miner": 2, "miner-advanced": 2, "smelter": 2, "assembler": 2, "generator": 2,
    "power-node-t2": 2,
    "extractor": 1, "power-node": 1, "conveyor-basic": 1, "conveyor-fast": 1,
    "conveyor-express": 1, "junction": 1, "splitter": 1, "sorter": 1, "conveyor-bridge": 1,
    "core": 2, "core-1": 2, "core-2": 3, "core-3": 4, "core-4": 5,
}
CORE_TIER = 4
CORE_LUCE_FOTOGRAMMI = 4
# Drone tiers seen from above, nose to the East: square sprites of this side (alpha, beta, gamma).
DRONI = {"drone-1": 24, "drone-2": 28, "drone-3": 32}


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


def errore_misura(nome, im):
    """Size problem of one generated sprite, or None when it is fine."""
    w, h = im.size
    anim = nome.endswith("-anim")
    base = nome[: -len("-anim")] if anim else nome
    if anim:
        frame = FOTOGRAMMA.get(nome, h)
        if frame <= 0 or w % frame != 0:
            return f"{nome}: larghezza {w} non multipla del fotogramma {frame}"
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
        return None
    side = T * CELLE[block]
    if base.endswith("-span"):
        # Repeatable span texture (bridge): tiles a cell evenly, never taller than the block.
        ok = w > 0 and side % w == 0 and 0 < h <= side
        return None if ok else f"{nome}: {w}x{h}, attesa larghezza divisore di {side} e altezza <= {side}"
    frame_w = FOTOGRAMMA.get(nome, h) if anim else w
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
        self._saved = (dict(SPRITE), dict(FOTOGRAMMA))
        for nome, fn in TEST_SPRITES.items():
            sprite(nome)(fn)

    def tearDown(self):
        SPRITE.clear()
        SPRITE.update(self._saved[0])
        FOTOGRAMMA.clear()
        FOTOGRAMMA.update(self._saved[1])

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

    def test_copertura_tier(self):
        # The four Core tiers with their light strip, the HUD/research icon and the three drones.
        attesi = {"core"} | {f"core-{n}" for n in range(1, CORE_TIER + 1)}
        attesi |= {f"core-{n}-light-anim" for n in range(1, CORE_TIER + 1)} | set(DRONI)
        self.assertEqual([], sorted(attesi - set(SPRITE)))

    def test_core(self):
        self.assertEqual(SPRITE["core-1"]().tobytes(), SPRITE["core"]().tobytes())
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
            self.assertLessEqual(max(x1 - x0, y1 - y0), 2 * T // 2, anim)
        # Extractor: empty dark well (8 px across) in the centre for the filter dot.
        ex = SPRITE["extractor"]()
        pozzo = {ex.getpixel((x, y)) for y in range(13, 19) for x in range(13, 19)}
        self.assertEqual(1, len(pozzo))

    @unittest.skipUnless(os.environ.get("TINDUSTRY_ARTE_COMPLETA") == "1",
                         "gli sprite arrivano famiglia per famiglia (task 2-5 della G1)")
    def test_copertura(self):
        c = _content()
        attesi = {s["id"] for s in c["structures"]} | _item_ids()
        attesi |= {f"core-{n}" for n in range(1, 5)} | {f"drone-{n}" for n in range(1, 4)}
        reali = set(SPRITE) - set(TEST_SPRITES)
        self.assertEqual([], sorted(attesi - reali))


if __name__ == "__main__":
    unittest.main()
