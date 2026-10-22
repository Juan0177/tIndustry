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
IMMAGINI = "docs/img/stato"  # generated SVG bars: totale.svg and one <key>.svg per aspect
RAW = "https://raw.githubusercontent.com/Juan0177/tIndustry/main/"
# Colours of the site (tools/sito/stile.css), so the bars look the same everywhere.
FONDO, TRACCIA, LINEA, GIALLO, AMBRA, TENUE = "#0b0d10", "#14171c", "#262b33", "#f2c230", "#ffd37f", "#9aa3b2"

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


_STRISCE = (f'<defs><pattern id="strisce" width="16" height="16" patternUnits="userSpaceOnUse" '
            f'patternTransform="rotate(45)"><rect width="8" height="16" fill="{GIALLO}"/>'
            f'<rect x="8" width="8" height="16" fill="{AMBRA}"/></pattern></defs>')


def _traccia(x, y, w, h, fatti, tutti):
    """A track with its striped fill, as on the site's "sviluppo" page."""
    pieno = round((w - 2) * fatti / tutti) if tutti else 0
    return (f'<rect x="{x + .5}" y="{y + .5}" width="{w - 1}" height="{h - 1}" fill="{TRACCIA}" stroke="{LINEA}"/>'
            + (f'<rect x="{x + 1}" y="{y + 1}" width="{pieno}" height="{h - 2}" fill="url(#strisce)"/>' if pieno else ""))


def svg_aspetto(fatti, tutti):
    """The bar of one aspect, for its row of the table."""
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="200" height="16" viewBox="0 0 200 16">'
            f'{_STRISCE}{_traccia(0, 0, 200, 16, fatti, tutti)}</svg>\n')


def svg_totale(bs):
    """The total: the percentage and one segment per aspect, each filled with its own percentage."""
    fatti, tutti = totale(bs)
    w, margine, spazio, y = 960, 16, 6, 72
    largo = (w - 2 * margine - spazio * (len(bs) - 1)) / len(bs)
    pezzi = []
    for i, (k, _, _, f, n) in enumerate(bs):
        x = round(margine + i * (largo + spazio), 1)
        pezzi.append(f'<text x="{x + largo / 2:.1f}" y="{y - 6}" text-anchor="middle" fill="{TENUE}" '
                     f'font-size="14">{html.escape(k)}</text>{_traccia(x, y, round(largo, 1), 26, f, n)}')
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="114" viewBox="0 0 {w} 114" '
            f'font-family="ui-monospace,Cascadia Mono,Consolas,DejaVu Sans Mono,monospace">{_STRISCE}'
            f'<rect width="{w}" height="114" fill="{FONDO}"/>'
            f'<rect width="{w}" height="4" fill="url(#strisce)"/>'
            f'<text x="{margine}" y="44" fill="{AMBRA}" font-size="34" font-weight="700">{percento(fatti, tutti)}%</text>'
            f'<text x="{margine + 100}" y="42" fill="{TENUE}" font-size="18">avanzamento del gioco · '
            f'{fatti} passi fatti su {tutti}</text>{"".join(pezzi)}</svg>\n')


def immagini(bs):
    """{repo path: SVG text} of every bar image."""
    return {f"{IMMAGINI}/totale.svg": svg_totale(bs),
            **{f"{IMMAGINI}/{k}.svg": svg_aspetto(f, n) for k, _, _, f, n in bs}}


def markdown_barra(bs, dove):
    """The total bar (an image split by aspect) and a table with each aspect's link and own bar."""
    fatti, tutti = totale(bs)
    base = IMMAGINI if dove == "readme" else RAW + IMMAGINI
    dettaglio = "docs/STATO.md" if dove == "readme" else STATO_URL
    righe = "\n".join(
        f"| {k} | {f'[{t}]({link_per(l, dove)})' if l else t} | "
        f'<img src="{base}/{k}.svg" alt="{percento(f, n)}%" width="160"> | {percento(f, n)}% ({f}/{n}) |'
        for k, t, l, f, n in bs)
    return (f'<img src="{base}/totale.svg" alt="Avanzamento del gioco: {percento(fatti, tutti)}%, '
            f'{fatti} passi fatti su {tutti}" width="100%">\n\n'
            f"| # | Aspetto | Avanzamento | % |\n| --- | --- | --- | --- |\n{righe}\n\n"
            f"Passi di ogni aspetto in [docs/STATO.md]({dettaglio}).\n")


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
    attesi = immagini(bs)
    cartella = os.path.join(REPO, IMMAGINI)
    vecchie = {f"{IMMAGINI}/{n}" for n in os.listdir(cartella)} if os.path.isdir(cartella) else set()
    for percorso in sorted(vecchie - set(attesi)):  # an aspect that was removed
        fuori.append(percorso)
        if "--controlla" not in argv:
            os.remove(os.path.join(REPO, percorso))
    for percorso, svg in attesi.items():
        pieno = os.path.join(REPO, percorso)
        vecchio = open(pieno, encoding="utf-8", newline="").read() if os.path.exists(pieno) else None
        if vecchio != svg:
            fuori.append(percorso)
            if "--controlla" not in argv:
                os.makedirs(cartella, exist_ok=True)
                with open(pieno, "w", encoding="utf-8", newline="\n") as fh:
                    fh.write(svg)
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
