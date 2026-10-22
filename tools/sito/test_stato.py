"""Tests of the progress bar read from docs/STATO.md: python -m unittest tools/sito/test_stato.py"""
import os
import sys
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import stato  # noqa: E402

ESEMPIO = """# Stato
### A · Primo
- [x] uno
- [ ] due
- [X] tre
### B · Secondo
- [ ] quattro
## Altro
- [x] fuori dalla barra
"""


class TestStato(unittest.TestCase):
    def test_blocchi_e_totale(self):
        bs = stato.blocchi(ESEMPIO)
        self.assertEqual([("A", "Primo", 2, 3), ("B", "Secondo", 0, 1)], bs)
        self.assertEqual((2, 4), stato.totale(bs))
        self.assertEqual(67, stato.percento(2, 3))
        self.assertEqual(0, stato.percento(0, 0))

    def test_barra_di_testo(self):
        testo = stato.markdown_barra(stato.blocchi(ESEMPIO), "x.md")
        self.assertIn("**Avanzamento: 50%** (2 passi fatti su 4, [dettaglio](x.md))", testo)
        self.assertIn("A 67%       B 0%", testo)
        self.assertIn("███████░░░  ░░░░░░░░░░", testo)

    def test_barra_html(self):
        pagina = stato.html_barra(stato.blocchi(ESEMPIO))
        self.assertIn('style="width:67%"', pagina)
        self.assertIn("<b>50%</b>", pagina)

    def test_stato_vero_ha_blocchi(self):
        bs = stato.blocchi()
        self.assertEqual("ABCDE", "".join(b[0] for b in bs))
        self.assertTrue(all(t > 0 for *_, t in bs))

    def test_copie_allineate(self):
        # The README and the Wiki home must carry the bar of the current docs/STATO.md.
        self.assertEqual(0, stato.main(["--controlla"]), "lancia python tools/sito/stato.py")


if __name__ == "__main__":
    unittest.main()
