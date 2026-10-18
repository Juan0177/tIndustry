"""Tests of the website builder: python -m unittest tools/sito/test_costruisci.py"""
import os
import re
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import costruisci  # noqa: E402


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

    @classmethod
    def tearDownClass(cls):
        cls._tmp.cleanup()

    def test_pagine(self):
        self.assertEqual(["asset/index.html", "index.html"], self.scritti)
        self.assertTrue(os.path.exists(os.path.join(self.out, ".nojekyll")))
        self.assertIn('href="asset/"', self.ingresso)

    def test_ogni_sprite_compare(self):
        # Every registered sprite with a PNG has its figure, under its family.
        attesi = [n for nomi in costruisci.famiglie(os.path.join(costruisci.REPO, "godot", "assets")).values()
                  for n in nomi]
        self.assertGreater(len(attesi), 100)
        for nome in attesi:
            self.assertIn(f"<b>{nome.rsplit('/', 1)[-1]}</b>", self.asset, nome)
        self.assertEqual(len(attesi), self.asset.count("<figure>"))

    def test_animazioni_gif(self):
        anim = sum(1 for n in costruisci.SPRITE if n.endswith("-anim"))
        self.assertEqual(anim, self.asset.count("data:image/gif;base64,"))

    def test_autonome(self):
        # Self-contained: no external scripts, stylesheets or images.
        for pagina in (self.asset, self.ingresso):
            self.assertNotIn("<script", pagina)
            self.assertEqual([], re.findall(r'<img src="(?!data:)', pagina))
            self.assertNotIn("<link", pagina)


if __name__ == "__main__":
    unittest.main()
