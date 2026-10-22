"""Tests of the progress bar read from docs/STATO.md: python -m unittest tools/sito/test_stato.py"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stato  # noqa: E402

ESEMPIO = """# Stato
## Aspetti
### 1 · [Primo](wiki/Logistica.md)
- [x] uno
- [ ] due
- [X] tre
### 2 · Secondo
- [ ] quattro
## Altro
- [x] fuori dalla barra
"""


class TestStato(unittest.TestCase):
    def test_blocchi_e_totale(self):
        bs = stato.blocchi(ESEMPIO)
        self.assertEqual([("1", "Primo", "wiki/Logistica.md", 2, 3), ("2", "Secondo", None, 0, 1)], bs)
        self.assertEqual((2, 4), stato.totale(bs))
        self.assertEqual(67, stato.percento(2, 3))
        self.assertEqual(0, stato.percento(0, 0))

    def test_link_per_ogni_posto(self):
        self.assertEqual("Logistica", stato.link_per("wiki/Logistica.md", "wiki"))
        self.assertEqual(stato.WIKI + "Logistica", stato.link_per("wiki/Logistica.md", "readme"))
        self.assertEqual("docs/ARCHITETTURA.md", stato.link_per("ARCHITETTURA.md", "readme"))
        self.assertEqual(stato.BLOB + "docs/ARCHITETTURA.md", stato.link_per("ARCHITETTURA.md", "wiki"))
        self.assertEqual("https://x.y/z", stato.link_per("https://x.y/z", "sito"))

    def test_barra_in_markdown(self):
        testo = stato.markdown_barra(stato.blocchi(ESEMPIO), "readme")
        self.assertIn('<img src="docs/img/stato/totale.svg" alt="Avanzamento del gioco: 50%, 2 passi fatti su 4"', testo)
        self.assertIn(f'| 1 | [Primo]({stato.WIKI}Logistica) | <img src="docs/img/stato/1.svg" alt="67%" width="160"> '
                      "| 67% (2/3) |", testo)
        self.assertIn("| 2 | Secondo |", testo)
        self.assertIn(f'src="{stato.RAW}docs/img/stato/2.svg"', stato.markdown_barra(stato.blocchi(ESEMPIO), "wiki"))

    def test_immagini(self):
        svg = stato.immagini(stato.blocchi(ESEMPIO))
        self.assertEqual({"docs/img/stato/totale.svg", "docs/img/stato/1.svg", "docs/img/stato/2.svg"}, set(svg))
        self.assertIn('width="132"', svg["docs/img/stato/1.svg"])  # 2/3 of the 198 px inside the track
        self.assertNotIn("url(#strisce)\"/></svg>", svg["docs/img/stato/2.svg"])  # 0%: no fill
        self.assertIn(">50%</text>", svg["docs/img/stato/totale.svg"])

    def test_barra_html(self):
        pagina = stato.html_barra(stato.blocchi(ESEMPIO))
        self.assertIn('style="width:67%"', pagina)
        self.assertIn("<b>50%</b>", pagina)
        self.assertIn(f'<a href="{stato.WIKI}Logistica">Primo</a>', pagina)

    def test_stato_vero(self):
        bs = stato.blocchi()
        self.assertGreaterEqual(len(bs), 10)
        self.assertTrue(all(n > 0 for *_, n in bs), "ogni aspetto ha almeno una casella")
        self.assertTrue(all(l for _, _, l, _, _ in bs), "ogni aspetto porta alla sua documentazione")
        for _, _, l, _, _ in bs:
            if not l.startswith("http"):
                self.assertTrue(os.path.exists(os.path.join(stato.REPO, "docs", l.partition("#")[0])), l)

    def test_copie_allineate(self):
        # The README and the Wiki home must carry the bar of the current docs/STATO.md.
        self.assertEqual(0, stato.main(["--controlla"]), "lancia python tools/sito/stato.py")


if __name__ == "__main__":
    unittest.main()
