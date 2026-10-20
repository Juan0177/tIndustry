"""Tests for the narrow-belt proposal sheet (stdlib unittest only).

Run from the repo root:  python -m unittest tools/art/test_proposte_nastri_stretti.py
"""
import contextlib
import io
import os
import sys
import tempfile
import unittest

from PIL import Image

ART_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ART_DIR, "..", ".."))
if ART_DIR not in sys.path:
    sys.path.insert(0, ART_DIR)

import proposte_nastri_stretti as p  # noqa: E402
from palette import NASTRO  # noqa: E402

ASSETS = os.path.join(REPO, "godot", "assets")


def _stato(cartella):
    return {n: os.path.getmtime(os.path.join(cartella, n)) for n in sorted(os.listdir(cartella))}


class TavolaTests(unittest.TestCase):
    def test_tavola_completa(self):
        self.assertEqual(sorted(p.VARIANTI), ["A", "B", "C"])
        self.assertEqual(p.TIER_NASTRI, ("conveyor-basic", "conveyor-fast", "conveyor-express", "conveyor-armored"))
        self.assertEqual(p.FORME, ("dritto", "curva", "t", "unione"))
        for variante in p.VARIANTI:
            for tier in p.TIER_NASTRI:
                for forma in p.FORME:
                    im = p.pezzo(variante, tier, forma)
                    self.assertEqual(im.size, (p.T * p.CELLE_FORMA[forma], p.T), (variante, tier, forma))
                    self.assertIsNotNone(im.getchannel("A").getbbox(), (variante, tier, forma))
        # One row per tier and look (today's belt + the three variants), a header on top.
        tav = p.tavola()
        self.assertEqual(tav.size, p.misura_tavola())
        larghezza, altezza = p.misura_tavola()
        self.assertEqual(altezza, p.ALTEZZA_TITOLO + len(p.TIER_NASTRI) * 4 * (2 * p.T + p.MARGINE) + p.MARGINE)
        self.assertGreater(larghezza, 2 * 3 * sum(p.CELLE_FORMA.values()) * p.T)

        # The default output lives in the git-ignored .dev/; a run writes only the sheet and never godot/assets.
        self.assertEqual(os.path.commonpath([p.USCITA, os.path.join(REPO, ".dev")]), os.path.join(REPO, ".dev"))
        prima = _stato(ASSETS)
        with tempfile.TemporaryDirectory() as tmp:
            uscita = os.path.join(tmp, "arte", "nastri-stretti.png")
            with contextlib.redirect_stdout(io.StringIO()):
                p.main([uscita])
            self.assertEqual(os.listdir(tmp), ["arte"])
            self.assertEqual(os.listdir(os.path.join(tmp, "arte")), ["nastri-stretti.png"])
            with Image.open(uscita) as scritta:
                self.assertEqual(scritta.size, tav.size)
        self.assertEqual(_stato(ASSETS), prima)

    def test_larghezza_come_gli_oggetti(self):
        oggetto = 0
        for nome in os.listdir(ASSETS):
            if nome.endswith("-belt.png"):
                with Image.open(os.path.join(ASSETS, nome)) as im:
                    x0, y0, x1, y1 = im.convert("RGBA").getchannel("A").getbbox()
                    oggetto = max(oggetto, x1 - x0, y1 - y0)
        self.assertGreater(oggetto, 0)
        gomma = {c + (255,) for c in NASTRO}
        for variante in p.VARIANTI:
            for tier in p.TIER_NASTRI:
                im = p.pezzo(variante, tier, "dritto", oggetti=False)
                righe = [y for y in range(p.T) if any(im.getpixel((x, y)) in gomma for x in range(im.width))]
                larghezza = righe[-1] - righe[0] + 1
                self.assertEqual(len(righe), larghezza, (variante, tier, "tread rows not contiguous"))
                self.assertLessEqual(abs(larghezza - oggetto), 2, (variante, tier, larghezza, oggetto))


if __name__ == "__main__":
    unittest.main()
