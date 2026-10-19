"""Tests of the document readers of the website, on the real sources of the repository:
python -m unittest tools/sito/test_documenti.py"""
import html
import os
import re
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import documenti  # noqa: E402

RESIDUI = ("**", "`", "](")


def _senza_tag(testo):
    return re.sub(r"<[^>]+>", "", testo)


class TestInline(unittest.TestCase):
    def test_grassetto_e_codice(self):
        self.assertEqual("<b>Mondo</b> con <code>a&lt;b</code>", documenti.inline("**Mondo** con `a<b`"))

    def test_link_relativo_con_backtick(self):
        self.assertEqual(f'<a href="{documenti.REPO_URL}/blob/main/docs/x.md">spec <code>y</code></a>',
                         documenti.inline("[spec `y`](docs/x.md)"))

    def test_link_ancora(self):
        self.assertEqual(f'<a href="{documenti.REPO_URL}#gioca">Gioca</a>', documenti.inline("[Gioca](#gioca)"))

    def test_link_http(self):
        self.assertEqual('<a href="https://e.x/">e</a>', documenti.inline("[e](https://e.x/)"))

    def test_link_relativo_risolto_dalla_base(self):
        self.assertEqual(
            f'<a href="{documenti.REPO_URL}/blob/main/docs/decisioni/0002-s.md#x">ADR</a>',
            documenti.inline("[ADR](../decisioni/0002-s.md#x)", base="docs/diario"))

    def test_corsivo(self):
        self.assertEqual("da <i>Primi passi</i> a <b>T2</b>", documenti.inline("da *Primi passi* a **T2**"))


class TestTabella(unittest.TestCase):
    MD = "# T\n\n## Una\n\ntesto\n\n| A | B |\n| --- | --- |\n| 1 | `x\\|y` |\n| 2 | **b** |\n\naltro\n\n## Due\n\nniente\n"

    def test_celle_senza_separatore(self):
        self.assertEqual([["A", "B"], ["1", "`x|y`"], ["2", "**b**"]], documenti.tabella_dopo(self.MD, "Una"))

    def test_manca(self):
        self.assertEqual([], documenti.tabella_dopo(self.MD, "Due"))
        self.assertEqual([], documenti.tabella_dopo(self.MD, "Tre"))


class TestReadme(unittest.TestCase):
    def test_contenuti(self):
        self.assertEqual(["Mondo", "Produzione", "Logistica", "Energia", "Economia", "Ricerca", "Campagna",
                          "Interfaccia", "Salvataggi"], [a for a, _ in documenti.contenuti()])
        for area, cosa in documenti.contenuti():
            self.assertTrue(cosa, area)
            for residuo in RESIDUI:
                self.assertNotIn(residuo, area)
                self.assertNotIn(residuo, cosa)

    def test_roadmap(self):
        righe = documenti.roadmap()
        self.assertTrue({"Fatto", "In corso", "Previsto"} <= {_senza_tag(s) for s, _, _ in righe}, righe)
        for riga in righe:
            self.assertEqual(3, len(riga))
            for cella in riga:
                self.assertTrue(cella)
                for residuo in RESIDUI:
                    self.assertNotIn(residuo, cella, cella)
            self.assertNotIn("<b>", riga[1])

    def test_requisiti(self):
        requisiti = documenti.requisiti()
        self.assertEqual("Windows", requisiti[0][0])
        for sistema, file, eseguibile in requisiti:
            self.assertTrue(file.endswith(".zip"), file)
            for cella in (sistema, file, eseguibile):
                self.assertNotIn("`", cella)
                self.assertNotIn("<", cella)

    def test_primi_passi(self):
        passi = documenti.primi_passi()
        self.assertGreaterEqual(len(passi), 3)
        for passo in passi:
            self.assertTrue(passo)
            for residuo in RESIDUI:
                self.assertNotIn(residuo, passo, passo)


class TestDiario(unittest.TestCase):
    def test_voci(self):
        voci = documenti.diario()
        self.assertGreaterEqual(len(voci), 1)
        with open(os.path.join(documenti.REPO, "docs", "diario", "README.md"), encoding="utf-8") as fh:
            indice = re.findall(r"^- \[(\d{4}-\d{2}-\d{2}) — ", fh.read(), re.M)
        self.assertEqual(indice[0], voci[0]["data"])
        self.assertEqual(indice, [v["data"] for v in voci])
        for v in voci:
            self.assertTrue(v["obiettivo"], v["file"])
            self.assertTrue(v["titolo"], v["file"])
            self.assertTrue(v["url"].startswith(documenti.REPO_URL + "/blob/main/docs/diario/"), v["url"])
            self.assertEqual(v["url"], documenti.REPO_URL + "/blob/main/docs/diario/" + v["file"])
            self.assertTrue(os.path.isfile(os.path.join(documenti.REPO, "docs", "diario", v["file"])), v["file"])
            for residuo in RESIDUI:
                self.assertNotIn(residuo, v["obiettivo"], v["file"])
            self.assertNotIn('href="../', v["obiettivo"], v["file"])


class TestDecisioni(unittest.TestCase):
    def test_adr(self):
        adr = documenti.decisioni()
        numeri = [a["numero"] for a in adr]
        self.assertEqual(sorted(numeri), numeri)
        self.assertEqual(len(set(numeri)), len(numeri))
        file = sorted(f for f in os.listdir(os.path.join(documenti.REPO, "docs", "decisioni"))
                      if re.match(r"^\d{4}-.+\.md$", f))
        self.assertEqual([int(f[:4]) for f in file], numeri)
        self.assertEqual(1, adr[0]["numero"])
        self.assertEqual("Il server multiplayer sarà in C#", adr[0]["titolo"])
        for a in adr:
            self.assertNotRegex(a["titolo"], r"^\d{4}")
            self.assertTrue(a["url"].startswith(documenti.REPO_URL + "/blob/main/docs/decisioni/"), a["url"])
            for residuo in RESIDUI:
                self.assertNotIn(residuo, a["riassunto"], a["url"])

    def test_riassunti_dalla_tabella(self):
        with open(os.path.join(documenti.REPO, "docs", "decisioni.md"), encoding="utf-8") as fh:
            in_tabella = set(re.findall(r"\]\(decisioni/(\d{4}-[^)]+\.md)\)", fh.read()))
        for a in documenti.decisioni():
            file = a["url"].rsplit("/", 1)[-1]
            if file in in_tabella:
                self.assertTrue(a["riassunto"], file)
                self.assertNotIn(". ", _senza_tag(a["riassunto"])[:-1], file)


class TestVersione(unittest.TestCase):
    def test_repo(self):
        self.assertRegex(documenti.versione_release() or "v0.0.0", r"^v\d+\.\d+")

    def test_senza_git(self):
        with tempfile.TemporaryDirectory() as vuota:
            self.assertIsNone(documenti.versione_release(vuota))


class TestSezioniSviluppo(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.pagina = documenti.sezioni_sviluppo()

    def test_ordine_e_classi(self):
        posizioni = [self.pagina.index(f'<section id="{i}" class="sez-doc">')
                     for i in ("come-e-fatto", "roadmap", "diario", "decisioni", "changelog")]
        self.assertEqual(sorted(posizioni), posizioni)
        for classe in ("contenuto-doc", "etichetta", "nota", "pannelli", "pannello-doc", "tabella",
                       "linea-tempo", "elenco-adr"):
            self.assertIn(f'class="{classe}"', self.pagina)
        self.assertEqual(5, self.pagina.count("<h2>"))
        self.assertIn("<thead><tr><th>Stato</th><th>Sotto-progetto</th><th>Cosa porta</th></tr></thead>", self.pagina)

    def test_decisioni(self):
        for a in documenti.decisioni():
            self.assertIn(f'<h3><a href="{a["url"]}">{html.escape(a["titolo"])}</a></h3>', self.pagina)
            self.assertIn(f'<li><a href="{a["url"]}">{a["numero"]:04d} — {html.escape(a["titolo"])}</a></li>',
                          self.pagina)

    def test_diario(self):
        for v in documenti.diario():
            self.assertIn(f'<time>{v["data"]}</time><h3><a href="{v["url"]}">{html.escape(v["titolo"])}</a></h3>',
                          self.pagina)
            self.assertIn(f"<p>{v['obiettivo']}</p>", self.pagina)

    def test_roadmap(self):
        for stato, nome, cosa in documenti.roadmap():
            self.assertIn(f'<span class="stato {documenti.classe_stato(stato)}">{stato}</span></td><td>{nome}</td>',
                          self.pagina)
            self.assertIn(f"<td>{cosa}</td>", self.pagina)
        self.assertIn('class="stato stato-fatto"', self.pagina)
        self.assertEqual("stato-fatto", documenti.classe_stato("Fatto, in attesa dell'ok"))
        self.assertEqual("stato-in-corso", documenti.classe_stato("In corso"))
        self.assertEqual("stato-previsto", documenti.classe_stato("Previsto"))
        self.assertEqual("stato-altro", documenti.classe_stato("Rinviato"))

    def test_changelog_e_link(self):
        self.assertIn(f'href="{documenti.REPO_URL}/blob/main/CHANGELOG.md"', self.pagina)
        self.assertIn(f'href="{documenti.REPO_URL}/releases"', self.pagina)
        self.assertIn('href="../asset/"', self.pagina)
        self.assertIn('href="radice/#scarica"', documenti.sezioni_sviluppo("radice/"))

    def test_nessun_residuo(self):
        self.assertNotIn("<script", self.pagina)
        self.assertNotIn("<img", self.pagina)
        for residuo in RESIDUI:
            self.assertNotIn(residuo, self.pagina)
        self.assertNotRegex(self.pagina, r'href="(?!https://|\.\./)')


if __name__ == "__main__":
    unittest.main()
