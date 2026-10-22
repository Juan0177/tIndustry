"""Progress bar of the work order, read from the check boxes of docs/STATO.md.

The total is done steps over all steps; the bar is split in one segment per block (A, B, ...), each filled
with that block's own percentage. The README and the Wiki home carry a text copy between markers, written
by this script; the "sviluppo" page of the site draws it as HTML (`html_barra`).

    python tools/sito/stato.py              # rewrite the bars in README.md and docs/wiki/Home.md
    python tools/sito/stato.py --controlla  # exit 1 if a bar is out of date (CI)
"""
import html
import os
import re
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
STATO = "docs/STATO.md"
STATO_URL = "https://github.com/Juan0177/tIndustry/blob/main/docs/STATO.md"
INIZIO, FINE = "<!-- barra-stato:inizio -->", "<!-- barra-stato:fine -->"
# Files with a text copy of the bar, and the link to docs/STATO.md as seen from each of them.
COPIE = (("README.md", "docs/STATO.md"), ("docs/wiki/Home.md", STATO_URL))
LARGHEZZA = 10  # characters per block in the text bar

_RE_BLOCCO = re.compile(r"^###\s+([A-Z])\s+·\s+(.+?)\s*$")
_RE_CASELLA = re.compile(r"^-\s+\[([ xX])\]\s")


def percento(fatti, totale):
    return int(fatti * 100 / totale + 0.5) if totale else 0


def blocchi(testo=None):
    """[(letter, title, done, total)] in file order, from the `### X · Title` headings and their check boxes."""
    if testo is None:
        with open(os.path.join(REPO, STATO), encoding="utf-8") as fh:
            testo = fh.read()
    risultato = []
    for riga in testo.splitlines():
        if m := _RE_BLOCCO.match(riga):
            risultato.append([m.group(1), m.group(2), 0, 0])
        elif risultato and riga.startswith("## "):
            break  # past the work order
        elif risultato and (m := _RE_CASELLA.match(riga)):
            risultato[-1][3] += 1
            risultato[-1][2] += m.group(1) != " "
    return [tuple(b) for b in risultato]


def totale(bs):
    return sum(b[2] for b in bs), sum(b[3] for b in bs)


def markdown_barra(bs, link):
    """The text bar (a code block, so it lines up on GitHub and in the Wiki) with its legend."""
    fatti, tutti = totale(bs)
    cella = LARGHEZZA + 2
    etichette = "".join(f"{f'{l} {percento(f, t)}%':<{cella}}" for l, _, f, t in bs).rstrip()
    barre = "".join(
        f"{'█' * round(LARGHEZZA * f / t if t else 0) + '░' * (LARGHEZZA - round(LARGHEZZA * f / t if t else 0)):<{cella}}"
        for _, _, f, t in bs).rstrip()
    legenda = " · ".join(f"{l} {t_} ({f}/{t})" for l, t_, f, t in bs)
    return (f"**Avanzamento: {percento(fatti, tutti)}%** ({fatti} passi fatti su {tutti}, [dettaglio]({link}))\n\n"
            f"```text\n{etichette}\n{barre}\n```\n\n{legenda}\n")


def html_barra(bs):
    """The bar for the site: one segment per block, each filled with its own percentage."""
    fatti, tutti = totale(bs)
    segmenti = "".join(
        f'<div class="blocco-avanzamento" title="{html.escape(t_)}: {f} su {t}">'
        f'<span class="etichetta-avanzamento">{l} · {percento(f, t)}%</span>'
        f'<span class="traccia-avanzamento"><span class="pieno-avanzamento" style="width:{percento(f, t)}%"></span></span>'
        f'<span class="nome-avanzamento">{html.escape(t_)}</span></div>'
        for l, t_, f, t in bs)
    return (f'<p class="totale-avanzamento"><b>{percento(fatti, tutti)}%</b> · {fatti} passi fatti su {tutti}</p>'
            f'<div class="barra-avanzamento">{segmenti}</div>')


def _con_barra(testo, barra):
    prima, sep, resto = testo.partition(INIZIO)
    _, sep2, dopo = resto.partition(FINE)
    if not (sep and sep2):
        raise ValueError(f"marcatori {INIZIO} … {FINE} mancanti")
    return f"{prima}{INIZIO}\n{barra}{FINE}{dopo}"


def main(argv):
    bs = blocchi()
    fuori = []
    for percorso, link in COPIE:
        pieno = os.path.join(REPO, percorso)
        with open(pieno, encoding="utf-8", newline="") as fh:
            vecchio = fh.read()
        nuovo = _con_barra(vecchio, markdown_barra(bs, link))
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
