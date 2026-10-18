"""Readers of the repository documents for the website: README tables, diary, decisions, release version.

Everything is read at build time, so the site follows the documents without editing. Markdown support is
the minimum the README and the diary use inline: code, links, bold, italic.
"""
import glob
import html
import os
import posixpath
import re
import subprocess

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
REPO_URL = "https://github.com/Juan0177/tIndustry"

_CODICE = re.compile(r"`([^`]+)`")
_LINK = re.compile(r"\[([^\]]+)\]\(([^)\s]+)\)")
_GRASSETTO = re.compile(r"\*\*(.+?)\*\*")
_CORSIVO = re.compile(r"\*([^*]+)\*")
_SEGNAPOSTO = re.compile("\x00(\\d+)\x00")


def _url(destinazione, base=""):
    if destinazione.startswith(("http://", "https://")):
        return destinazione
    if destinazione.startswith("#"):
        return REPO_URL + destinazione
    return f"{REPO_URL}/blob/main/{posixpath.normpath(posixpath.join(base, destinazione))}"


def inline(markdown, base=""):
    """HTML of one line of Markdown: escape, code, links, bold, italic. Relative links resolve from base
    (a repository folder, "" for the root) to the file on GitHub."""
    # Code spans and link openings become placeholders, so no asterisk inside them turns into emphasis.
    pezzi = []

    def tieni(testo):
        pezzi.append(testo)
        return f"\x00{len(pezzi) - 1}\x00"

    testo = html.escape(markdown, quote=False)
    testo = _CODICE.sub(lambda m: tieni(f"<code>{m.group(1)}</code>"), testo)
    testo = _LINK.sub(lambda m: tieni(f'<a href="{html.escape(_url(html.unescape(m.group(2)), base))}">')
                      + m.group(1) + tieni("</a>"), testo)
    testo = _GRASSETTO.sub(r"<b>\1</b>", testo)
    testo = _CORSIVO.sub(r"<i>\1</i>", testo)
    while _SEGNAPOSTO.search(testo):  # a code span can sit inside a link text
        testo = _SEGNAPOSTO.sub(lambda m: pezzi[int(m.group(1))], testo)
    return testo


def _leggi(rel):
    with open(os.path.join(REPO, rel), encoding="utf-8") as fh:
        return fh.read()


def tabella_dopo(markdown, titolo):
    """Raw cells of the first table under the heading `## titolo` (or `### titolo`), without the `| --- |`
    row; [] when the section has none."""
    livello = None
    righe = []
    for riga in markdown.splitlines():
        intestazione = re.match(r"(#+) (.*)", riga)
        if livello is None:
            if intestazione and len(intestazione.group(1)) in (2, 3) and intestazione.group(2).strip() == titolo:
                livello = len(intestazione.group(1))
            continue
        if riga.startswith("|"):
            celle = [c.strip() for c in riga.strip().strip("|").split("|")]
            if not all(re.fullmatch(r":?-+:?", c) for c in celle):
                righe.append(celle)
        elif righe or (intestazione and len(intestazione.group(1)) <= livello):
            break
    return righe


def _senza_grassetto(cella):
    return cella[2:-2] if cella.startswith("**") and cella.endswith("**") else cella


def contenuti():
    """(area, contenuto) of the README table "Cosa c'è nel gioco", as HTML."""
    righe = tabella_dopo(_leggi("README.md"), "Cosa c'è nel gioco")[1:]
    return [(inline(_senza_grassetto(area)), inline(cosa)) for area, cosa in righe]


def roadmap():
    """(stato, nome, cosa) of the README table "Stato e roadmap", as HTML."""
    righe = tabella_dopo(_leggi("README.md"), "Stato e roadmap")[1:]
    return [(inline(stato), inline(_senza_grassetto(nome)), inline(cosa)) for stato, nome, cosa in righe]


def requisiti():
    """(sistema, file, eseguibile) of the table under "## Gioca", as HTML."""
    righe = tabella_dopo(_leggi("README.md"), "Gioca")[1:]
    return [tuple(inline(c) for c in r) for r in righe]


def _obiettivo(rel):
    """First paragraph after `## Obiettivo` of a diary file, as HTML with links resolved from its folder."""
    paragrafo = []
    dentro = False
    for riga in _leggi(rel).splitlines():
        if not dentro:
            dentro = riga.strip() == "## Obiettivo"
        elif riga.strip():
            if riga.startswith("#"):
                break
            paragrafo.append(riga.strip())
        elif paragrafo:
            break
    return inline(" ".join(paragrafo), base=posixpath.dirname(rel))


def diario():
    """Entries of the diary index (docs/diario/README.md), in index order."""
    voci = []
    for m in re.finditer(r"^- \[(\d{4}-\d{2}-\d{2}) — (.+)\]\(([^)\s]+)\)\s*$", _leggi("docs/diario/README.md"), re.M):
        rel = f"docs/diario/{m.group(3)}"
        voci.append({"data": m.group(1), "titolo": m.group(2), "file": m.group(3),
                     "url": f"{REPO_URL}/blob/main/{rel}", "obiettivo": _obiettivo(rel)})
    return voci


def decisioni():
    """Architecture decisions of docs/decisioni/NNNN-*.md, by number."""
    adr = []
    for path in glob.glob(os.path.join(REPO, "docs", "decisioni", "[0-9][0-9][0-9][0-9]-*.md")):
        nome = os.path.basename(path)
        with open(path, encoding="utf-8") as fh:
            prima = fh.readline().strip()
        titolo = re.sub(r"^#\s*(\d+\s*[—-]\s*)?", "", prima)
        adr.append({"numero": int(nome[:4]), "titolo": titolo, "url": f"{REPO_URL}/blob/main/docs/decisioni/{nome}"})
    return sorted(adr, key=lambda a: a["numero"])


def versione_release(repo=REPO):
    """Latest release tag reachable from HEAD (git describe), or None without git or tags."""
    try:
        esito = subprocess.run(["git", "describe", "--tags", "--abbrev=0"], cwd=repo, capture_output=True, text=True)
    except OSError:
        return None
    if esito.returncode != 0:
        return None
    return esito.stdout.strip() or None
