"""Tests of the repository readers for the website: python -m unittest tools/sito/test_documenti.py"""
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import documenti  # noqa: E402


class TestDocumenti(unittest.TestCase):
    def test_inline(self):
        self.assertEqual('<b>Mondo</b> con <code>a&lt;b</code>', documenti.inline("**Mondo** con `a<b`"))
        self.assertEqual(f'<a href="{documenti.REPO_URL}/blob/main/docs/x.md">spec <code>y</code></a>',
                         documenti.inline("[spec `y`](docs/x.md)"))
        self.assertEqual(f'<a href="{documenti.REPO_URL}#gioca">Gioca</a>', documenti.inline("[Gioca](#gioca)"))
        self.assertEqual('<a href="https://e.x/">e</a>', documenti.inline("[e](https://e.x/)"))

    def test_inline_codice_protetto(self):
        # Asterisks inside code and link targets are no emphasis; relative links resolve from a base folder.
        self.assertEqual("<code>*.cs</code> e <code>*.py</code>", documenti.inline("`*.cs` e `*.py`"))
        self.assertEqual(f'<a href="{documenti.REPO_URL}/blob/main/docs/decisioni/0002-x.md">ADR <i>2</i></a>',
                         documenti.inline("[ADR *2*](../decisioni/0002-x.md)", base="docs/diario"))

    def test_tabelle_readme(self):
        aree = [a for a, _ in documenti.contenuti()]
        self.assertEqual(["Mondo", "Produzione", "Logistica", "Energia", "Economia", "Ricerca", "Campagna",
                          "Interfaccia", "Salvataggi"], aree)
        stati = {s for s, _, _ in documenti.roadmap()}
        self.assertTrue({"Fatto", "In corso", "Previsto"} <= stati, stati)
        self.assertEqual("Windows", documenti.requisiti()[0][0])
        for riga in documenti.contenuti() + [(n, c) for _, n, c in documenti.roadmap()]:
            for cella in riga:
                for residuo in ("**", "`", "]("):
                    self.assertNotIn(residuo, cella, cella)

    def test_tabella_mancante(self):
        self.assertEqual([], documenti.tabella_dopo("## Altro\n\n| a |\n| --- |\n| b |\n", "Assente"))
        self.assertEqual([["b"]], documenti.tabella_dopo("### Qui\n\n| a |\n| --- |\n| b |\n", "Qui")[1:])

    def test_diario_e_decisioni(self):
        voci = documenti.diario()
        self.assertGreaterEqual(len(voci), 7)
        self.assertEqual("2026-10-16", voci[0]["data"])
        for v in voci:
            self.assertTrue(v["obiettivo"], v["file"])
            self.assertTrue(v["url"].startswith(documenti.REPO_URL + "/blob/main/docs/diario/"))
            self.assertNotIn("../", v["obiettivo"], v["file"])
        adr = documenti.decisioni()
        self.assertEqual([1, 2, 3, 4, 5, 6], [a["numero"] for a in adr][:6])
        self.assertEqual("Il server multiplayer sarà in C#", adr[0]["titolo"])

    def test_versione(self):
        self.assertRegex(documenti.versione_release() or "v0.0.0", r"^v\d+\.\d+")
        with tempfile.TemporaryDirectory() as vuota:
            self.assertIsNone(documenti.versione_release(vuota))


if __name__ == "__main__":
    unittest.main()
