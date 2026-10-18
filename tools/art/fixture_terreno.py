"""Writes the terrain composition fixture of the C# tests (TerrainLookTests.MatchesPythonReference).

    python tools/art/fixture_terreno.py            # rewrite the fixture
    python tools/art/fixture_terreno.py --controlla  # exit 1 if it is out of date

A fixed 24x24 map that uses every floor, wall and ore (edges, corners, walls of different biomes side by
side, deposits on water and under walls, the map border), and the sprite pieces tavola.pezzi_mappa draws
for it: the client's TerrainLook.Plan must give exactly the same list.
"""
import json
import os
import sys

ART_DIR = os.path.dirname(os.path.abspath(__file__))
if ART_DIR not in sys.path:
    sys.path.insert(0, ART_DIR)

from blocchi import terreno as tr  # noqa: E402
from tavola import pezzi_mappa  # noqa: E402

LATO = 24
FIXTURE = os.path.join(ART_DIR, "..", "..", "tests", "TIndustry.Shared.Tests", "Fixtures", "terrain-look-24.json")


def mappa():
    """celle[y][x] = (floor, wall or None, ore or None), from hash_cella with salts of its own."""
    h = tr.hash_cella
    suoli, pareti, minerali = tr.SUOLO_IDS, tr.PARETE_IDS, tr.MINERALI
    celle = []
    for y in range(LATO):
        riga = []
        for x in range(LATO):
            suolo = suoli[h(x // 3, y // 3, 101) % len(suoli)]
            if h(x, y, 102) % 5 == 0:
                suolo = suoli[h(x, y, 103) % len(suoli)]
            parete = pareti[h(x // 6, y // 6, 105) % len(pareti)] if h(x // 2, y // 2, 104) % 4 == 0 else None
            minerale = minerali[h(x, y, 107) % len(minerali)] if h(x, y, 106) % 7 == 0 else None
            riga.append((suolo, parete, minerale))
        celle.append(riga)
    return celle


def contenuto():
    celle = mappa()
    dati = {
        "size": LATO,
        "cells": [[list(c) for c in riga] for riga in celle],
        "pieces": [list(p) for p in pezzi_mappa(celle)],
    }
    return json.dumps(dati, separators=(",", ":")) + "\n"


def main():
    testo = contenuto()
    if "--controlla" in sys.argv[1:]:
        with open(FIXTURE, encoding="utf-8") as f:
            if f.read() != testo:
                print(f"{FIXTURE}: da rigenerare (python tools/art/fixture_terreno.py)")
                return 1
        print("fixture del terreno aggiornata")
        return 0
    with open(FIXTURE, "w", encoding="utf-8", newline="\n") as f:
        f.write(testo)
    print(f"scritto {FIXTURE}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
