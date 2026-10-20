"""Checks docs/wiki/ before it is published to the GitHub Wiki (.github/workflows/wiki.yml).

    python tools/dev/controlla_wiki.py

Every page link [text](Page-Name) must point to a page of docs/wiki/, every page except the
special ones (_Sidebar, _Footer) must be reachable from Home or the sidebar, and links to files
of the repository must be absolute (relative paths do not resolve inside the Wiki).
The navigation bar of every page must be up to date (tools/dev/wiki_navigazione.py).
Exits with 1 and lists the problems otherwise.
"""
import os
import re
import sys

import wiki_navigazione

WIKI = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "docs", "wiki")
LINK = re.compile(r"(?<!!)\[[^\]]*\]\(([^)\s]+)\)")
SPECIALI = {"_Sidebar", "_Footer"}


def controlla(cartella=WIKI):
    pagine = {f[:-3]: os.path.join(cartella, f) for f in sorted(os.listdir(cartella)) if f.endswith(".md")}
    problemi, raggiunte = [], set()
    for nome, percorso in pagine.items():
        with open(percorso, encoding="utf-8") as fh:
            testo = re.sub(r"```.*?```", "", fh.read(), flags=re.S)
        for destinazione in LINK.findall(testo):
            if destinazione.startswith(("http://", "https://", "mailto:", "#")):
                continue
            pagina = destinazione.split("#", 1)[0]
            if "/" in pagina or pagina.endswith(".md"):
                problemi.append(f"{nome}: link relativo a un file ({destinazione}), usa l'URL assoluto su GitHub")
            elif pagina not in pagine:
                problemi.append(f"{nome}: pagina inesistente ({destinazione})")
            elif nome in ("Home", "_Sidebar"):
                raggiunte.add(pagina)
    for nome in pagine:
        if nome not in SPECIALI and nome != "Home" and nome not in raggiunte:
            problemi.append(f"{nome}: non raggiungibile da Home né dalla barra laterale")
    for pagina in wiki_navigazione.da_aggiornare(cartella):
        problemi.append(f"{pagina}: barra di navigazione mancante o vecchia (python tools/dev/wiki_navigazione.py)")
    if "Home" not in pagine:
        problemi.append("manca Home.md")
    return problemi


if __name__ == "__main__":
    trovati = controlla()
    for p in trovati:
        print(p)
    print(f"wiki: {len(trovati)} problemi" if trovati else "wiki: OK")
    sys.exit(1 if trovati else 0)
