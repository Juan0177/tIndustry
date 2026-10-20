"""Proposal sheet of narrow belts (as wide as the items on them, Mindustry style); sheet only.

    python tools/art/proposte_nastri_stretti.py            # writes .dev/arte/nastri-stretti.png
    python tools/art/proposte_nastri_stretti.py <file.png> # elsewhere

Nothing here is registered in genera.py and nothing is written to godot/assets/: the game keeps
today's belts until the user picks a variant (then that variant becomes its own plan).

Three variants, each with the tread as wide as the largest `*-belt` item sprite (10 px):
  A "binari sottili": 10 px tread between 2 px rails;
  B "canale": 10 px tread sunk between dark edges, no rails;
  C "Mindustry": Mindustry's proportions brought to 32 px (12 px tread, 4 px side plates), small arrows.
For the four belt tiers, every variant shows a straight run (two cells, full of items at the 0.4 cell
spacing), a corner, a T merge and a side join, at 1x and 2x (the in-game scale) on a light and a dark
background, next to today's belt with the same items.
"""
import math
import os
import sys

ART_DIR = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(ART_DIR, "..", ".."))
if ART_DIR not in sys.path:
    sys.path.insert(0, ART_DIR)

from PIL import Image  # noqa: E402

import carattere  # noqa: E402
import forme  # noqa: E402
from blocchi.logistica import ARCO, TIER, _riga_curva  # noqa: E402
from palette import NASTRO, OUT  # noqa: E402
from tavola import CHIARO, FONDO, MARGINE, SCURO, _riquadro  # noqa: E402

T = 32
ASSETS = os.path.join(REPO, "godot", "assets")
USCITA = os.path.join(REPO, ".dev", "arte", "nastri-stretti.png")
TIER_NASTRI = tuple(TIER)
NOMI_TIER = {"conveyor-basic": "base", "conveyor-fast": "veloce", "conveyor-express": "espresso",
             "conveyor-armored": "rinforzato"}
FORME = ("dritto", "curva", "t", "unione")
CELLE_FORMA = {"dritto": 2, "curva": 1, "t": 1, "unione": 1}
OGGETTI = ("iron-ore", "copper-ore", "coal", "titanium-plate", "silicon")
SPAZIATURA = 0.4 * T  # Mindustry's item spacing on plain belts (Task 3)

# Rows across an East belt: "o" outline, "r<k>" rail tone k of the tier's rail ramp, "e" inner edge
# (the tier's accent, else rail tone 1), "t" tread, "s" tread in the shadow of the top edge.
# Chevron: (half height in rows from the axis, thickness, period in px); rib period (None = smooth).
VARIANTI = {
    "A": dict(nome="binari sottili", righe={8: "o", 9: "r3", 10: "e", **{v: "t" for v in range(11, 21)},
                                            21: "e", 22: "r1", 23: "o"},
              freccia=(3, 3, 32), coste=8),
    "B": dict(nome="canale", righe={9: "o", 10: "r0", 11: "s", **{v: "t" for v in range(12, 21)},
                                    21: "r0", 22: "o"},
              freccia=(3, 2, 16), coste=None),
    "C": dict(nome="Mindustry", righe={5: "o", 6: "r4", 7: "r3", 8: "r2", 9: "e",
                                       **{v: "t" for v in range(10, 22)},
                                       22: "e", 23: "r2", 24: "r1", 25: "o"},
              freccia=(2, 2, 16), coste=8),
}

ALTEZZA_TITOLO = 132
ETICHETTA = 296


def _colore(variante, tier, u, v):
    """(layer, colour) at flow position u and row v of a variant's East belt; None outside it.
    Layer 0 = outline and rails, 1 = tread: a shape draws every layer 0 before any layer 1, so a
    tread crossing another piece's rail opens it."""
    var = VARIANTI[variante]
    tipo = var["righe"].get(v)
    if tipo is None:
        return None
    binario, freccia, bordo = TIER[tier]
    if tipo == "o":
        return 0, OUT
    if tipo == "e":
        return 0, bordo if bordo is not None else binario[1]
    if tipo[0] == "r":
        k = min(int(tipo[1:]), len(binario) - 1)
        return 0, binario[max(0, k - 2) if math.floor(u) % 8 == 7 else k]  # roller joint every 8 px
    alto, spessore, periodo = var["freccia"]
    dv = int(abs(v + 0.5 - T / 2))
    if dv <= alto:
        d = (math.floor(u) + dv - 18) % periodo
        if d < spessore:
            toni = {3: (freccia[2], freccia[3], OUT), 2: (freccia[3], OUT)}[spessore]
            return 1, toni[d]
    if tipo == "s":
        return 1, NASTRO[0]
    coste = var["coste"]
    if coste is not None:
        k = math.floor(u) % coste
        if k == coste - 1:
            return 1, NASTRO[0]
        if k == 0:
            return 1, NASTRO[2]
    return 1, NASTRO[1]


def _dritto(x, y):
    return x, y


def _curva(x, y):
    """Corner in from the West edge, out from the South edge: the bend of blocchi/logistica.curva."""
    a, b = x, T - 1 - y
    v = _riga_curva(a, b)
    return None if v < 0 else (math.atan2(a, b) * ARCO, v)


def _curva_da_sud(x, y):
    """Corner in from the South edge, out from the East edge (pivot on the South-East corner)."""
    a, b = T - 1 - y, T - 1 - x
    v = _riga_curva(a, b)
    return None if v < 0 else (math.atan2(a, b) * ARCO, v)


def _curva_da_nord(x, y):
    return _curva_da_sud(x, T - 1 - y)


def _da_nord(fine):
    """Side belt coming in from the North edge, down to row `fine`."""
    return lambda x, y: (y, T - 1 - x) if y <= fine else None


def _geometria(variante, forma):
    if forma == "dritto":
        return [_dritto]
    if forma == "curva":
        return [_curva]
    if forma == "t":
        return [_curva_da_sud, _curva_da_nord]
    fine = max(v for v, t in VARIANTI[variante]["righe"].items() if t in "ts")
    return [_da_nord(fine), _dritto]


def _oggetto(nome):
    with Image.open(os.path.join(ASSETS, nome + "-belt.png")) as im:
        return im.convert("RGBA")


def _centri(forma):
    """Item centres (pixels) on a shape; the straight run is full at the 0.4 cell spacing."""
    if forma == "dritto":
        return [(SPAZIATURA / 2 + k * SPAZIATURA, T / 2) for k in range(5)]
    if forma == "curva":
        r = T / 2 - 0.5
        return [(r * math.sin(t), T - r * math.cos(t)) for t in (math.pi / 8, 3 * math.pi / 8)]
    if forma == "t":
        return [(T / 2, T - 5), (T / 2, 5), (T - 5, T / 2)]
    return [(T / 2, 5), (5, T / 2), (T - 5, T / 2)]


def _con_oggetti(im, forma):
    for i, (cx, cy) in enumerate(_centri(forma)):
        ogg = _oggetto(OGGETTI[i % len(OGGETTI)])
        im.alpha_composite(ogg, (round(cx - ogg.width / 2), round(cy - ogg.height / 2)))
    return im


def pezzo(variante, tier, forma, oggetti=True):
    """One shape of a variant (East-facing like the game's art), with items on it."""
    im = forme.nuova(T * CELLE_FORMA[forma], T)
    geometria = _geometria(variante, forma)
    for strato in (0, 1):
        for g in geometria:
            for y in range(im.height):
                for x in range(im.width):
                    uv = g(x, y)
                    c = uv and _colore(variante, tier, *uv)
                    if c and c[0] == strato:
                        forme.pixel(im, x, y, c[1])
    return _con_oggetti(im, forma) if oggetti else im


def pezzo_di_oggi(tier, forma):
    """The same shape from today's belt sprites in godot/assets/ (frame 0), with the same items."""
    nome = {"dritto": tier, "curva": tier + "-corner-anim", "t": tier + "-t-anim",
            "unione": tier + "-join-left-anim"}[forma]
    with Image.open(os.path.join(ASSETS, nome + ".png")) as src:
        cella = src.convert("RGBA").crop((0, 0, T, T))
    im = forme.nuova(T * CELLE_FORMA[forma], T)
    for k in range(CELLE_FORMA[forma]):
        im.alpha_composite(cella, (k * T, 0))
    return _con_oggetti(im, forma)


def _larghezza_gruppo(k):
    return sum(CELLE_FORMA[f] * T * k + MARGINE for f in FORME)


def misura_tavola():
    blocco = _larghezza_gruppo(1) + _larghezza_gruppo(2) + 2 * MARGINE  # one background
    righe = len(TIER_NASTRI) * (1 + len(VARIANTI))
    return ETICHETTA + 2 * blocco + MARGINE, ALTEZZA_TITOLO + righe * (2 * T + MARGINE) + MARGINE


def _titolo():
    righe = [carattere.scrivi("Nastri stretti: proposte (solo tavola, il gioco non cambia)", 2),
             carattere.scrivi("A binari sottili: battistrada largo come l'oggetto (10 px), binari di 2 px", 2),
             carattere.scrivi("B canale: battistrada di 10 px incassato fra bordi scuri, niente binari", 2),
             carattere.scrivi("C Mindustry: proporzioni di Mindustry a 32 px (12 px), frecce piccole", 2),
             carattere.scrivi("dritto (pieno, 0,4) curva T unione, 1x e 2x (scala del gioco), fondo chiaro e scuro", 2)]
    im = Image.new("RGBA", (max(r.width for r in righe), sum(r.height + 2 for r in righe)), (0, 0, 0, 0))
    y = 0
    for r in righe:
        im.alpha_composite(r, (0, y))
        y += r.height + 2
    return im


def tavola():
    larghezza, altezza = misura_tavola()
    out = Image.new("RGBA", (larghezza, altezza), FONDO)
    titolo = _titolo()
    assert titolo.height + MARGINE <= ALTEZZA_TITOLO and titolo.width + MARGINE <= larghezza
    out.alpha_composite(titolo, (MARGINE, MARGINE))
    y = ALTEZZA_TITOLO
    for tier in TIER_NASTRI:
        for aspetto in ("oggi", *VARIANTI):
            if aspetto == "oggi":
                pezzi, testo = [pezzo_di_oggi(tier, f) for f in FORME], f"{NOMI_TIER[tier]}: oggi"
            else:
                pezzi = [pezzo(aspetto, tier, f) for f in FORME]
                testo = f"{NOMI_TIER[tier]}: {aspetto} {VARIANTI[aspetto]['nome']}"
            etichetta = carattere.scrivi(testo, 2)
            assert etichetta.width <= ETICHETTA - MARGINE, testo
            out.alpha_composite(etichetta, (MARGINE, y + T - etichetta.height // 2))
            x = ETICHETTA
            for fondo in (CHIARO, SCURO):
                for k in (1, 2):
                    for im in pezzi:
                        out.alpha_composite(_riquadro(im, fondo, k), (x, y))
                        x += im.width * k + MARGINE
                x += 2 * MARGINE
            y += 2 * T + MARGINE
    return out


def main(argv=None):
    argv = sys.argv[1:] if argv is None else argv
    uscita = argv[0] if argv else USCITA
    os.makedirs(os.path.dirname(os.path.abspath(uscita)), exist_ok=True)
    im = tavola()
    im.info.clear()
    im.save(uscita, format="PNG", optimize=False)
    print(uscita)


if __name__ == "__main__":
    main()
