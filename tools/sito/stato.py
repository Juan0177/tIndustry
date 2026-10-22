"""Progress bar of every aspect of the game, read from the check boxes of docs/STATO.md.

The total is done steps over all steps; the bar is split in one segment per aspect (`### N · [Title](doc)`),
each filled with that aspect's own percentage and linked to its documentation. The README and the Wiki home
carry a text copy between markers, written by this script; the "sviluppo" page of the site draws it as HTML.

    python tools/sito/stato.py              # rewrite the bars in README.md and docs/wiki/Home.md
    python tools/sito/stato.py --controlla  # exit 1 if a bar is out of date (CI)
"""
import html
import os
import posixpath
import re
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
REPO_URL = "https://github.com/Juan0177/tIndustry"
BLOB = REPO_URL + "/blob/main/"
WIKI = REPO_URL + "/wiki/"
STATO = "docs/STATO.md"
STATO_URL = BLOB + STATO
INIZIO, FINE = "<!-- barra-stato:inizio -->", "<!-- barra-stato:fine -->"
# Files with a text copy of the bar and where they live (links are written for that place).
COPIE = (("README.md", "readme"), ("docs/wiki/Home.md", "wiki"))
LARGHEZZA = 10  # characters of an aspect's own bar
SEGMENTO = 4    # characters of an aspect in the total bar

_RE_BLOCCO = re.compile(r"^###\s+([A-Z0-9]+)\s+·\s+(.+?)\s*$")
_RE_TITOLO_LINK = re.compile(r"^\[(.+?)\]\(([^)\s]+)\)$")
_RE_CASELLA = re.compile(r"^-\s+\[([ xX])\]\s")


def percento(fatti, totale):
    return int(fatti * 100 / totale + 0.5) if totale else 0


def blocchi(testo=None):
    """[(key, title, doc link or None, done, total)] in file order: the `### N · Title` headings of the first
    section that has them, with their check boxes."""
    if testo is None:
        with open(os.path.join(REPO, STATO), encoding="utf-8") as fh:
            testo = fh.read()
    risultato = []
    for riga in testo.splitlines():
        if m := _RE_BLOCCO.match(riga):
            titolo, link = m.group(2), None
            if l := _RE_TITOLO_LINK.match(titolo):
                titolo, link = l.group(1), l.group(2)
            risultato.append([m.group(1), titolo, link, 0, 0])
        elif risultato and riga.startswith("## "):
            break  # past the aspects
        elif risultato and (m := _RE_CASELLA.match(riga)):
            risultato[-1][4] += 1
            risultato[-1][3] += m.group(1) != " "
    return [tuple(b) for b in risultato]


def totale(bs):
    return sum(b[3] for b in bs), sum(b[4] for b in bs)


def link_per(link, dove):
    """The doc link of docs/STATO.md (relative to docs/) as written in the README, the Wiki or the site."""
    if link is None or link.startswith("http"):
        return link
    percorso, _, ancora = link.partition("#")
    percorso = posixpath.normpath(posixpath.join("docs", percorso))
    if percorso.startswith("docs/wiki/") and percorso.endswith(".md"):
        pagina = posixpath.basename(percorso)[:-3]
        pagina = "" if pagina == "Home" else pagina
        return pagina if dove == "wiki" else WIKI + pagina
    if dove == "readme":
        return percorso + ("#" + ancora if ancora else "")
    return BLOB + percorso + ("#" + ancora if ancora else "")


def _barra(fatti, tutti, larghezza):
    pieno = round(larghezza * fatti / tutti) if tutti else 0
    return "█" * pieno + "░" * (larghezza - pieno)


def markdown_barra(bs, dove):
    """The total bar split by aspect (a code block, so it lines up) and a table with each aspect's own bar."""
    fatti, tutti = totale(bs)
    dettaglio = "docs/STATO.md" if dove == "readme" else STATO_URL
    righe = "\n".join(
        f"| {k} | {f'[{t}]({link_per(l, dove)})' if l else t} | `{_barra(f, n, LARGHEZZA)}` | {percento(f, n)}% ({f}/{n}) |"
        for k, t, l, f, n in bs)
    return (f"**Avanzamento del gioco: {percento(fatti, tutti)}%** ({fatti} passi fatti su {tutti}, "
            f"[dettaglio]({dettaglio}))\n\n"
            f"```text\n{'│'.join(_barra(f, n, SEGMENTO) for *_, f, n in bs)}\n```\n\n"
            f"| # | Aspetto | Avanzamento | % |\n| --- | --- | --- | --- |\n{righe}\n")


def html_barra(bs):
    """The bar for the site: one segment per aspect, each filled with its own percentage and linked to its doc."""
    fatti, tutti = totale(bs)
    segmenti = []
    for k, t, l, f, n in bs:
        nome = html.escape(t)
        if l:
            nome = f'<a href="{html.escape(link_per(l, "sito"))}">{nome}</a>'
        segmenti.append(
            f'<div class="blocco-avanzamento" title="{html.escape(t)}: {f} su {n}">'
            f'<span class="etichetta-avanzamento">{k} · {percento(f, n)}%</span>'
            f'<span class="traccia-avanzamento"><span class="pieno-avanzamento" style="width:{percento(f, n)}%"></span></span>'
            f'<span class="nome-avanzamento">{nome}</span></div>')
    return (f'<p class="totale-avanzamento"><b>{percento(fatti, tutti)}%</b> · {fatti} passi fatti su {tutti}</p>'
            f'<div class="barra-avanzamento">{"".join(segmenti)}</div>')


def _con_barra(testo, barra):
    prima, sep, resto = testo.partition(INIZIO)
    _, sep2, dopo = resto.partition(FINE)
    if not (sep and sep2):
        raise ValueError(f"marcatori {INIZIO} … {FINE} mancanti")
    return f"{prima}{INIZIO}\n{barra}{FINE}{dopo}"


def main(argv):
    bs = blocchi()
    fuori = []
    for percorso, dove in COPIE:
        pieno = os.path.join(REPO, percorso)
        with open(pieno, encoding="utf-8", newline="") as fh:
            vecchio = fh.read()
        nuovo = _con_barra(vecchio, markdown_barra(bs, dove))
        if nuovo != vecchio:
            fuori.append(percorso)
            if "--controlla" not in argv:
                with open(pieno, "w", encoding="utf-8", newline="") as fh:
                    fh.write(nuovo)
    fatti, tutti = totale(bs)
    if "--controlla" in argv and fuori:
        print("barra non aggiornata in: " + ", ".join(fuori) + " (lancia python tools/sito/stato.py)")
        return 1
    print(f"avanzamento {percento(fatti, tutti)}% ({fatti}/{tutti})" + (f", riscritti: {', '.join(fuori)}" if fuori else ""))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
