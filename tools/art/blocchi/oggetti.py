"""Item sprites: `<id>.png` 32x32 (HUD, panels) and `<id>-belt.png` 10x10 (belts, drawn x2).

Each item is drawn in its own colours (no runtime tint), ramps built around the tints in
src/TIndustry.Shared/Content/ItemVisualColors.cs so current players still recognise them.
One shape per kind, the same at both sizes:
- ores: a heap of faceted stones in the ore colour with bright glints;
- plates: two stacked slabs in oblique view (light top, shaded front and side);
- copper wire: a wound coil with a loose end;
- graphite: three stacked bars;
- silicon: a cluster of pointed crystals;
- money: two stacks of coins.
Outlines are dark (OUT); coal is near-black, so its HUD outline is a light rim. On belts
(BORDO_CHIARO) coal and titanium get the dark outline plus a light rim just inside it, so they
stand out on the dark rubber and on the cyan express chevrons. The 10x10 sprites are drawn by
hand (pixel grids): scaling the 32 px ones down would blur them.
"""
import math

import forme
from blocchi import sprite
from palette import (ACCENTO, CARBONE, FERRO, FULIGGINE, GRAFITE, OUT, PIOMBO, RAME,
                     SILICIO, TITANIO)

T = 32  # HUD icon side
B = 10  # belt sprite side

# Coal: near-black body with a light rim instead of the dark outline.
CARBONE_BORDO = FULIGGINE[3]
# Copper wire: copper shadows, warm brass highlights (more yellow than the ore).
FILO = [RAME[1], RAME[2], ACCENTO[1], ACCENTO[2], ACCENTO[3]]

# Belt sprites that must stand out on their belts (coal on the dark rubber, titanium on the cyan
# express chevrons): dark outline plus a 1 px light rim just inside it, in this colour.
BORDO_CHIARO = {"coal": CARBONE_BORDO, "titanium-ore": TITANIO[4], "titanium-plate": TITANIO[4]}

# id -> (kind, ramp, outline)
OGGETTI = {
    "iron-ore": ("minerale", FERRO, OUT),
    "copper-ore": ("minerale", RAME, OUT),
    "coal": ("minerale", CARBONE, CARBONE_BORDO),
    "lead-ore": ("minerale", PIOMBO, OUT),
    "titanium-ore": ("minerale", TITANIO, OUT),
    "iron-plate": ("lastra", FERRO[1:], OUT),
    "lead-plate": ("lastra", PIOMBO, OUT),
    "titanium-plate": ("lastra", TITANIO[1:], OUT),
    "copper-wire": ("filo", FILO, OUT),
    "graphite": ("barre", GRAFITE, OUT),
    "silicon": ("cristalli", SILICIO, OUT),
    "money": ("monete", ACCENTO, OUT),
}


def _toni(rampa):
    """'0' darkest, '1' shadow, '2' mid, '3' light, '4' highlight of a ramp."""
    n = len(rampa)
    m = (n - 1) // 2
    return {"0": rampa[0], "1": rampa[max(0, m - 1)], "2": rampa[m],
            "3": rampa[min(n - 1, m + 1)], "4": rampa[n - 1]}


def _bordo(pieni):
    return {(x, y) for x, y in pieni
            if any((x + ax, y + ay) not in pieni for ax, ay in ((1, 0), (-1, 0), (0, 1), (0, -1)))}


def _riempi(im, pieni, tono, contorno):
    """Outline on the border of `pieni`, tono(x, y) inside."""
    bordo = _bordo(pieni)
    for x, y in sorted(pieni):
        forme.pixel(im, x, y, contorno if (x, y) in bordo else tono(x, y))


def _griglia(righe, rampa, contorno, bordo=None):
    """10x10 sprite from a pixel grid: '.' empty, 'o' outline, '0'-'4' ramp tones; with `bordo`,
    every body pixel touching the outline (4-neighbour) takes that light rim colour."""
    assert len(righe) == B and all(len(r) == B for r in righe), righe
    toni = _toni(rampa)
    im = forme.nuova(B, B)

    def contorno_in(x, y):
        return 0 <= x < B and 0 <= y < B and righe[y][x] == "o"

    for y, riga in enumerate(righe):
        for x, c in enumerate(riga):
            if c == "o":
                forme.pixel(im, x, y, contorno)
            elif c != ".":
                vicino = any(contorno_in(x + ax, y + ay) for ax, ay in ((1, 0), (-1, 0), (0, 1), (0, -1)))
                forme.pixel(im, x, y, bordo if bordo and vicino else toni[c])
    return im


# --- 32 px shapes -------------------------------------------------------------------------

def _pietra(im, poli, rampa, contorno, scintille=(), crepe=()):
    """Faceted stone: lit top-left facet, shaded bottom-right, glints and cracks."""
    toni = _toni(rampa)
    pieni = forme.poligono(poli)
    cx = sum(p[0] for p in poli) / len(poli)
    cy = sum(p[1] for p in poli) / len(poli)
    r = max(max(p[0] for p in poli) - min(p[0] for p in poli),
            max(p[1] for p in poli) - min(p[1] for p in poli)) / 2
    bordo = _bordo(pieni)

    def tono(x, y):
        # Inner rim: lit on the top/left edge, dark on the bottom/right one.
        if (x - 1, y) in bordo or (x, y - 1) in bordo:
            return toni["4"] if (x - 1, y - 1) in bordo else toni["3"]
        if (x + 1, y) in bordo or (x, y + 1) in bordo:
            return toni["0"] if (x + 1, y + 1) in bordo else toni["1"]
        t = ((x + 0.5 - cx) + (y + 0.5 - cy)) / (2 * r)
        return toni["3"] if t < -0.2 else toni["2"] if t < 0.22 else toni["1"]

    _riempi(im, pieni, tono, contorno)
    for x, y in scintille:
        forme.pixel(im, x, y, toni["4"])
    for x, y in crepe:
        forme.pixel(im, x, y, toni["0"])


def minerale(rampa, contorno):
    im = forme.nuova(T, T)
    _pietra(im, [(3, 15), (8, 7), (16, 4), (22, 8), (22, 18), (14, 23), (4, 22)], rampa, contorno,
            scintille=[(10, 9), (11, 9), (10, 10), (7, 14)], crepe=[(15, 14), (16, 15), (16, 16)])
    _pietra(im, [(17, 13), (24, 9), (29, 13), (29, 22), (23, 25), (17, 21)], rampa, contorno,
            scintille=[(22, 13), (23, 13)], crepe=[(25, 19)])
    _pietra(im, [(7, 21), (14, 18), (20, 22), (19, 28), (11, 29), (6, 26)], rampa, contorno,
            scintille=[(11, 21), (12, 21), (9, 23)], crepe=[(16, 25)])
    return im


def _lastra(im, x0, y0, l, p, s, rampa, contorno, dettaglio):
    """Oblique slab: top face l x p (depth p goes up-right), thickness s below it."""
    toni = _toni(rampa)
    cima = [(x0, y0), (x0 + p, y0 - p), (x0 + p + l, y0 - p), (x0 + l, y0)]
    fronte = [(x0, y0), (x0 + l, y0), (x0 + l, y0 + s), (x0, y0 + s)]
    lato = [(x0 + l, y0), (x0 + l + p, y0 - p), (x0 + l + p, y0 - p + s), (x0 + l, y0 + s)]
    pc, pf, pl = forme.poligono(cima), forme.poligono(fronte), forme.poligono(lato)
    pieni = pc | pf | pl

    def tono(x, y):
        if (x, y) in pf:
            return toni["1"] if y == y0 + s - 1 else toni["2"]
        if (x, y) in pl:
            return toni["0"] if (x, y) in pc else toni["1"]
        # Top face: highlight along the back edge, light elsewhere.
        return toni["4"] if (x, y - 1) not in pieni else toni["3"]

    _riempi(im, pieni, tono, contorno)
    dettaglio(im, toni)


def lastra(rampa, contorno, dettaglio):
    im = forme.nuova(T, T)
    _lastra(im, 2, 23, 20, 7, 5, rampa, contorno, lambda im, t: None)
    _lastra(im, 2, 15, 20, 7, 5, rampa, contorno, dettaglio)
    return im


def _rivetti(im, toni):
    for x, y in ((6, 13), (19, 13), (11, 9), (24, 9)):
        forme.pixel(im, x, y, toni["4"])
        forme.pixel(im, x + 1, y + 1, toni["1"])


def _scanalatura(im, toni):
    """Stamped groove across the top face."""
    for x in range(7, 21):
        forme.pixel(im, x, 12, toni["1"])
        forme.pixel(im, x, 13, toni["4"])


def _riflesso(im, toni):
    """Two diagonal glints on the top face."""
    for i in range(5):
        forme.pixel(im, 9 + i, 14 - i, toni["4"])
        forme.pixel(im, 14 + i, 14 - i, toni["4"])


DETTAGLI = {"iron-plate": _rivetti, "lead-plate": _scanalatura, "titanium-plate": _riflesso}


def filo(rampa, contorno):
    """Coil seen from above: a thick ring with radial winding stripes and a loose end."""
    toni = _toni(rampa)
    im = forme.nuova(T, T)
    cx, cy, r_out, r_in = 15, 15, 13.2, 5.2
    coda = forme.poligono([(22, 24), (29, 28), (30, 30), (27, 30), (21, 26)])
    anello = {(x, y) for y in range(T) for x in range(T)
              if r_in <= math.hypot(x + 0.5 - cx, y + 0.5 - cy) <= r_out}

    def tono(x, y):
        dx, dy = x + 0.5 - cx, y + 0.5 - cy
        d = math.hypot(dx, dy)
        if (x, y) in coda and d > r_out - 1:
            return toni["3"] if (x + y) % 3 else toni["2"]
        # Ring cross-section: lit towards the light on the outer half, the inner half shaded.
        luce = (-dx - dy) / (d * math.sqrt(2)) if d else 0
        ext = (d - r_in) / (r_out - r_in)  # 0 inner edge .. 1 outer edge
        k = 2 + (1 if luce > 0.3 else -1 if luce < -0.3 else 0)
        if ext < 0.25:
            k -= 1
        elif 0.45 < ext < 0.7 and luce > -0.3:
            k += 1
        # Winding: a darker groove every 18 degrees.
        if int((math.degrees(math.atan2(dy, dx)) + 360) / 18) % 2 == 0 and 0.15 < ext < 0.9:
            k -= 1
        return toni[str(max(0, min(4, k)))]

    _riempi(im, anello | coda, tono, contorno)
    return im


def _barra(im, x0, y0, l, h, p, rampa, contorno):
    """Bar lying along x: front face l x h, top face and end face p px deep (up-right)."""
    toni = _toni(rampa)
    fronte = forme.poligono([(x0, y0), (x0 + l, y0), (x0 + l, y0 + h), (x0, y0 + h)])
    cima = forme.poligono([(x0, y0), (x0 + p, y0 - p), (x0 + l + p, y0 - p), (x0 + l, y0)])
    testa = forme.poligono([(x0 + l, y0), (x0 + l + p, y0 - p), (x0 + l + p, y0 - p + h), (x0 + l, y0 + h)])

    def tono(x, y):
        if (x, y) in fronte:
            return toni["3"] if y == y0 else toni["1"] if y == y0 + h - 1 else toni["2"]
        if (x, y) in testa and (x, y) not in cima:
            return toni["1"]
        return toni["4"] if x < x0 + l // 2 else toni["3"]

    _riempi(im, fronte | cima | testa, tono, contorno)


def barre(rampa, contorno):
    im = forme.nuova(T, T)
    _barra(im, 2, 23, 21, 6, 4, rampa, contorno)
    _barra(im, 6, 16, 21, 6, 4, rampa, contorno)
    _barra(im, 3, 9, 21, 6, 4, rampa, contorno)
    return im


def _cristallo(im, cx, cima, base, w, rampa, contorno):
    """Pointed prism: lit left face, shaded right face, bright ridge."""
    toni = _toni(rampa)
    pieni = forme.poligono([(cx - w, base), (cx - w, cima + w), (cx, cima), (cx + w, cima + w), (cx + w, base)])

    def tono(x, y):
        if x == int(cx) - 1 or x == int(cx):
            return toni["4"] if x == int(cx) - 1 else toni["3"]
        if x < cx:
            return toni["3"] if y < cima + w + 2 else toni["2"]
        return toni["1"] if y > base - 3 else toni["2"] if y < cima + w + 2 else toni["1"]

    _riempi(im, pieni, tono, contorno)


def cristalli(rampa, contorno):
    im = forme.nuova(T, T)
    _cristallo(im, 8, 11, 29, 4, rampa, contorno)
    _cristallo(im, 24, 8, 29, 4, rampa, contorno)
    _cristallo(im, 16, 2, 30, 5, rampa, contorno)
    return im


def _moneta(im, cx, cy, rx, ry, s, rampa, contorno):
    """Coin seen from above at an angle: top ellipse at cy, rim s px thick below it."""
    toni = _toni(rampa)

    def ell(x, y, oy, kx=0.0, ky=0.0):
        dx, dy = (x + 0.5 - cx) / (rx - kx), (y + 0.5 - (cy + oy)) / (ry - ky)
        return dx * dx + dy * dy <= 1

    pieni = {(x, y) for y in range(int(cy - ry - 1), int(cy + ry + s + 2))
             for x in range(int(cx - rx - 1), int(cx + rx + 2))
             if any(ell(x, y, o) for o in range(s + 1))}

    def tono(x, y):
        if not ell(x, y, 0):
            return toni["1"] if x % 2 else toni["2"]  # reeded edge
        if not ell(x, y, 0, 1.5, 1):
            return toni["4"] if x + 0.5 < cx else toni["3"]  # raised rim
        return toni["2"] if (x - cx) + 2 * (y - cy) > 0 else toni["3"]  # face

    _riempi(im, pieni, tono, contorno)


def monete(rampa, contorno):
    im = forme.nuova(T, T)
    for k in range(3):
        _moneta(im, 21, 20 - 4 * k, 8.5, 3.5, 3, rampa, contorno)
    for k in range(4):
        _moneta(im, 11, 26 - 4 * k, 8.5, 3.5, 3, rampa, contorno)
    return im


# --- 10 px belt shapes (hand drawn) ----------------------------------------------------

GRIGLIE = {
    "minerale": [
        "...ooo....",
        "..o443oo..",
        ".o443432o.",
        "o44332221o",
        "o43322211o",
        "oo3222111o",
        "o32221110o",
        ".o2211100o",
        "..o1110oo.",
        "...oooo...",
    ],
    "lastra": [
        "..........",
        "..........",
        "...ooooooo",
        "..o444433o",
        ".o3333331o",
        "o33333311o",
        "o22222211o",
        "o1111110o.",
        "oooooooo..",
        "..........",
    ],
    "filo": [
        "..oooooo..",
        ".o334432o.",
        "o34oooo32o",
        "o3o....o2o",
        "o4o....o1o",
        "o3o....o1o",
        "o32oooo21o",
        ".o221111oo",
        "..oooooo3o",
        "........oo",
    ],
    "barre": [
        "..oooooo..",
        ".o444333o.",
        "o22222221o",
        "oooooooooo",
        "o44433331o",
        "o22222221o",
        "oooooooooo",
        "o44433331o",
        "o22222221o",
        "oooooooooo",
    ],
    "cristalli": [
        "....oo....",
        "...o43o...",
        ".oo4321oo.",
        "o4o4321o3o",
        "o3o4322o2o",
        "o3o3321o1o",
        "o32o321o1o",
        "o32o221o1o",
        "o21o211o1o",
        "oooooooooo",
    ],
    "monete": [
        "..........",
        "...ooooo..",
        "..o44433o.",
        "..o12121o.",
        "ooooooooo.",
        "o4443332o.",
        "o2121212o.",
        "ooooooooo.",
        "o2121212o.",
        "ooooooooo.",
    ],
}


def _grande(item_id):
    tipo, rampa, contorno = OGGETTI[item_id]
    if tipo == "lastra":
        return lastra(rampa, contorno, DETTAGLI[item_id])
    return {"minerale": minerale, "filo": filo, "barre": barre,
            "cristalli": cristalli, "monete": monete}[tipo](rampa, contorno)


def _registra(item_id):
    tipo, rampa, contorno = OGGETTI[item_id]
    sprite(item_id)(lambda: _grande(item_id))
    if item_id in BORDO_CHIARO:
        sprite(item_id + "-belt")(lambda: _griglia(GRIGLIE[tipo], rampa, OUT, BORDO_CHIARO[item_id]))
    else:
        sprite(item_id + "-belt")(lambda: _griglia(GRIGLIE[tipo], rampa, contorno))


for _id in OGGETTI:
    _registra(_id)
