"""Writes the navigation bar at the bottom of every page of docs/wiki/.

    python tools/dev/wiki_navigazione.py            # rewrites the bars
    python tools/dev/wiki_navigazione.py --controlla  # exits with 1 if a bar is missing or stale

The order comes from _Sidebar.md: each bold heading (**Giocare**, **Sviluppare**) opens a part and
its list items are the pages in reading order. Previous and next stay inside a part; every bar links
back to Home. The bar sits between the two marker comments, so editing a page never touches it.
"""
import os
import re
import sys

WIKI = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "docs", "wiki")
INIZIO = "<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->"
FINE = "<!-- /navigazione -->"
BLOCCO = re.compile(r"\n*" + re.escape(INIZIO) + r".*?" + re.escape(FINE) + r"\n*", re.S)


def parti(cartella=WIKI):
    """Pages of each part, in sidebar order: [[(page, label), ...], ...]."""
    with open(os.path.join(cartella, "_Sidebar.md"), encoding="utf-8") as fh:
        righe = fh.read().splitlines()
    risultato = []
    for riga in righe:
        if re.fullmatch(r"\*\*[^*\[\]]+\*\*", riga.strip()):
            risultato.append([])
            continue
        voce = re.match(r"\s*-\s*\[([^\]]+)\]\(([^)\s]+)\)", riga)
        if voce and risultato:
            risultato[-1].append((voce.group(2), voce.group(1)))
    return [p for p in risultato if p]


def barra(precedente, successiva):
    pezzi = []
    if precedente:
        pezzi.append(f"[← Pagina precedente: {precedente[1]}]({precedente[0]})")
    pezzi.append("[Torna alla Home](Home)")
    if successiva:
        pezzi.append(f"[Pagina successiva: {successiva[1]} →]({successiva[0]})")
    return f"{INIZIO}\n\n---\n\n{' · '.join(pezzi)}\n\n{FINE}\n"


def attese(cartella=WIKI):
    """Expected content of every page that gets a bar: {page: text}."""
    risultato = {}
    for parte in parti(cartella):
        for i, (pagina, _) in enumerate(parte):
            percorso = os.path.join(cartella, pagina + ".md")
            if not os.path.exists(percorso):
                continue
            with open(percorso, encoding="utf-8") as fh:
                testo = BLOCCO.sub("\n", fh.read()).rstrip("\n") + "\n"
            precedente = parte[i - 1] if i > 0 else None
            successiva = parte[i + 1] if i + 1 < len(parte) else None
            risultato[pagina] = testo + "\n" + barra(precedente, successiva)
    return risultato


def da_aggiornare(cartella=WIKI):
    stantie = []
    for pagina, testo in attese(cartella).items():
        with open(os.path.join(cartella, pagina + ".md"), encoding="utf-8") as fh:
            if fh.read() != testo:
                stantie.append(pagina)
    return stantie


def scrivi(cartella=WIKI):
    for pagina, testo in attese(cartella).items():
        with open(os.path.join(cartella, pagina + ".md"), "w", encoding="utf-8", newline="\n") as fh:
            fh.write(testo)


if __name__ == "__main__":
    if "--controlla" in sys.argv[1:]:
        stantie = da_aggiornare()
        for pagina in stantie:
            print(f"{pagina}: barra di navigazione mancante o vecchia (python tools/dev/wiki_navigazione.py)")
        sys.exit(1 if stantie else 0)
    scrivi()
    print(f"navigazione: {sum(len(p) for p in parti())} pagine in {len(parti())} parti")
