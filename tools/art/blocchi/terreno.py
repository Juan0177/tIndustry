"""Terrain sprites (G2b): floors, edges, dual-grid walls, water, deposits and decorations.

Floor, wall and biome ids come from the `terrain` table of data/content.json, ore ids from the
item table of blocchi/oggetti.py, colours from palette.SUOLI and palette.PARETI.

- `terreno-<floor>-<n>` (n = 0..3): opaque 32x32 variants. Fine noise, periodic on the cell, in
  tones 1-3 of the floor ramp with the same share of each tone in every variant (so variants
  tile with themselves and with each other), plus a few features (pebbles, blades, cracks, glints)
  kept away from the cell edges. Low contrast: belts, items and buildings are drawn on top.
- `terreno-<floor>-bordo`: strip of 8 pieces N, E, S, O, NE, SE, SO, NO, drawn on a cell whose
  floor has a lower edgePriority: N when the northern neighbour is this floor, NE when the
  north-eastern one is, and so on (composition rule in tools/art/README.md). A jagged fringe of
  this floor's texture (variant 0, at the same pixel positions, so it continues the neighbour),
  without an outline: overlapping pieces of one floor merge without marks. Sides and corners are
  quarter turns of N and NE; the corner radii match the ends of the side fringes.
- `parete-<wall>`: dual grid. A piece is drawn on a cell corner (half a cell off the grid) and
  its four quadrants belong to the four cells around that corner; bit NO=1, NE=2, SE=4, SO=8 is
  set when that cell is this wall. Raised angular rock: light faceted top with cracks, dark
  cliff on the bottom side, darker right side, drop shadow; neighbours merge into ridges.
- `acqua-bassa-anim`, `acqua-profonda-anim` (the buildable and the deep floor of biome "acqua"): 4 frames of slow ripples, each frame tileable; the
  water floors' variants are still water (calm noise, a few glints).
- `giacimento-<item>-<n>` (n = 0..2): transparent, a few ore pebbles in the item colours.
- `deco-<biome>-<n>`: transparent, a small rock, shrub, ice shard or dry grass tuft.

`hash_cella(x, y, sale)` picks variant, deposit look and decoration of a map cell (the SALE_*
constants); the client uses the same function, so a cell looks the same at every load.
"""
import functools
import json
import os
import zlib

import forme
from blocchi import sprite
from blocchi.oggetti import OGGETTI
from palette import (ERBA, GHIACCIO, MUSCHIO, NEVE, OUT, PARETE_ARBUSTI, PARETE_GHIACCIO,
                     PARETE_PIETRA, PARETE_SABBIA, PARETI, SABBIA, SABBIA_SCURA, SUOLI)

T = 32
VARIANTI = 4
GIACIMENTI = 3
BORDI = ("N", "E", "S", "O", "NE", "SE", "SO", "NO")
NO, NE, SE, SO = 1, 2, 4, 8
FOTOGRAMMI_ACQUA = 4

# Salts of hash_cella for the per-cell choices, and decoration density per biome (% of cells).
SALE_VARIANTE, SALE_DECO, SALE_DECO_TIPO, SALE_GIACIMENTO = 1, 2, 3, 4
DENSITA_DECO = {"rocce": 10, "sabbia": 8, "neve": 7, "prati": 14}

_CONTENT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "data", "content.json")
with open(_CONTENT, encoding="utf-8") as _f:
    TERRENO = json.load(_f)["terrain"]
SUOLO_IDS = [f["id"] for f in TERRENO["floors"]]
PARETE_IDS = [w["id"] for w in TERRENO["walls"]]
PRIORITA = {f["id"]: f["edgePriority"] for f in TERRENO["floors"]}
BIOMA = {f["id"]: f["biome"] for f in TERRENO["floors"]}
BIOMI = sorted({w["biome"] for w in TERRENO["walls"]})  # land biomes: water is shared by all
# Water floor id -> animation: the buildable one is shallow, the other deep.
ACQUA = {f["id"]: "acqua-bassa-anim" if f["buildable"] else "acqua-profonda-anim"
         for f in TERRENO["floors"] if f["biome"] == "acqua"}
MINERALI = sorted(i for i, (tipo, _, _) in OGGETTI.items() if tipo == "minerale")

_M = 0xFFFFFFFF


def hash_cella(x, y, sale=0):
    """32-bit hash of a cell (any integers): same inputs, same value, on every platform."""
    h = ((x * 0x27D4EB2D) ^ (y * 0x165667B1) ^ (sale * 0x9E3779B1)) & _M
    h ^= h >> 15
    h = (h * 0x2C1B3C6D) & _M
    h ^= h >> 12
    h = (h * 0x297A2D39) & _M
    h ^= h >> 15
    return h


def _seme(*parti):
    return zlib.crc32("/".join(str(p) for p in parti).encode("utf-8"))


class _Dadi:
    """Deterministic dice from a seed."""

    def __init__(self, seme):
        self.seme, self.n = seme, 0

    def tira(self, k):
        self.n += 1
        return hash_cella(self.n, 0, self.seme) % k


def _rumore(seme, passo, bordo_medio=False):
    """Value noise periodic on the 32x32 cell: lattice every `passo` px, values 0..255. With
    `bordo_medio` the lattice points on the cell edges are all 128: every such noise has the same
    calm edges, so the variants of a floor tile with each other without a seam or a grid."""
    n = T // passo
    lat = [[128 if bordo_medio and not (i and j) else hash_cella(i, j, seme) & 255
            for i in range(n)] for j in range(n)]
    out = [[0] * T for _ in range(T)]
    for y in range(T):
        j, fy = divmod(y, passo)
        j1 = (j + 1) % n
        for x in range(T):
            i, fx = divmod(x, passo)
            i1 = (i + 1) % n
            a = lat[j][i] * (passo - fx) + lat[j][i1] * fx
            b = lat[j1][i] * (passo - fx) + lat[j1][i1] * fx
            out[y][x] = (a * (passo - fy) + b * fy) // (passo * passo)
    return out


def _quote(valori, seme, quote):
    """Tone grid from a value grid by rank: `quote` is [(share %, tone), ...] from the lowest
    values up; the remaining pixels get tone 2. Same share of each tone in every variant."""
    ordine = sorted((valori[y][x], hash_cella(x, y, seme), y, x) for y in range(T) for x in range(T))
    toni = [[2] * T for _ in range(T)]
    i = 0
    for quota, tono in quote:
        n = T * T * quota // 100
        for _, _, y, x in ordine[i:i + n]:
            toni[y][x] = tono
        i += n
    return toni


# --- floors -----------------------------------------------------------------------------------

# Floor look: weight of the coarse noise (0..10, the rest is per-pixel), share of the dark (1)
# and light (3) tones, and the features on top. Every floor of content.json
# needs its entry (test_stili_terreno).
STILI = {
    "stone": dict(grosso=5, scuro=20, chiaro=20, sassi=3, crepe=1),
    "basalt": dict(grosso=4, scuro=18, chiaro=16, crepe=3, sassi=1),
    "gravel": dict(grosso=2, scuro=26, chiaro=24, ghiaia=16),
    "sand": dict(grosso=5, scuro=18, chiaro=20, sassi=1, puntini=5),
    "dark-sand": dict(grosso=5, scuro=20, chiaro=18, sassi=2, puntini=4),
    "snow": dict(grosso=4, scuro=14, chiaro=16, puntini=6),
    "ice": dict(grosso=5, scuro=16, chiaro=16, riflessi=2, crepe=1),
    "grass": dict(grosso=3, scuro=22, chiaro=18, fili=14),
    "moss": dict(grosso=6, scuro=22, chiaro=22, puntini=6),
    "mud": dict(grosso=6, scuro=20, chiaro=14, pozze=2),
    "soil": dict(grosso=4, scuro=20, chiaro=20, zolle=5),
    # Still water (the animated frames are acqua-*-anim): calm noise with a few glints.
    "shallow-water": dict(grosso=6, scuro=14, chiaro=12, riflessi=2),
    "deep-water": dict(grosso=6, scuro=16, chiaro=8, riflessi=1),
}
MARGINE = 2  # features stay this far from the cell edges (variants must tile)


def _libero(x, y, lato=1):
    return MARGINE <= x and x + lato - 1 < T - MARGINE and MARGINE <= y and y + lato - 1 < T - MARGINE


def _sassi(toni, dadi, n, lato):
    """Pebbles: lit top-left pixel, mid body, dark shadow below-right."""
    for _ in range(n):
        x, y = MARGINE + dadi.tira(T - 2 * MARGINE - lato), MARGINE + dadi.tira(T - 2 * MARGINE - lato)
        for dy in range(lato):
            for dx in range(lato):
                toni[y + dy][x + dx] = 4 if dx + dy == 0 else 3 if dx == 0 or dy == 0 else 2
        for d in range(1, lato + 1):
            toni[y + lato][x + d] = 0
            toni[y + d][x + lato] = 0


def _crepe(toni, dadi, n, tono, lunghezza=5):
    """Short hairline cracks (or glints) that wander one pixel at a time."""
    for _ in range(n):
        x, y = MARGINE + dadi.tira(T - 2 * MARGINE), MARGINE + dadi.tira(T - 2 * MARGINE)
        dx = 1 if dadi.tira(2) else -1
        for _ in range(lunghezza):
            if _libero(x, y):
                toni[y][x] = tono
            if dadi.tira(3):
                x += dx
            else:
                y += 1


def _segni(toni, dadi, n, motivo):
    """Small marks from a pattern of (dx, dy, tone) at random spots."""
    for _ in range(n):
        x, y = MARGINE + dadi.tira(T - 2 * MARGINE - 2), MARGINE + dadi.tira(T - 2 * MARGINE - 2)
        for dx, dy, tono in motivo:
            if _libero(x + dx, y + dy):
                toni[y + dy][x + dx] = tono


def _valori_suolo(suolo, variante):
    stile = STILI[suolo]
    seme = _seme("suolo", suolo, variante)
    grosso = _rumore(seme, 8, bordo_medio=True)
    g = stile["grosso"]
    return [[grosso[y][x] * g + (hash_cella(x, y, seme + 1) & 255) * (10 - g) for x in range(T)] for y in range(T)]


def _toni_suolo(suolo, variante):
    """Noise tones by fixed cut values (those that give variant 0 its tone shares), so the
    shared edges of every variant get the same tones."""
    stile = STILI[suolo]
    seme = _seme("suolo", suolo, variante)
    riferimento = sorted(v for riga in _valori_suolo(suolo, 0) for v in riga)
    basso = riferimento[T * T * stile["scuro"] // 100]
    alto = riferimento[T * T * (100 - stile["chiaro"]) // 100]
    toni = [[1 if v < basso else 3 if v >= alto else 2 for v in riga] for riga in _valori_suolo(suolo, variante)]
    dadi = _Dadi(seme + 2)
    _crepe(toni, dadi, stile.get("crepe", 0), 0, 4)
    _crepe(toni, dadi, stile.get("riflessi", 0), 4, 6)
    _sassi(toni, dadi, stile.get("sassi", 0), 2)
    _sassi(toni, dadi, stile.get("ghiaia", 0) // 4, 2)
    _segni(toni, dadi, stile.get("ghiaia", 0), ((0, 0, 3), (1, 0, 2), (1, 1, 0)))
    _segni(toni, dadi, stile.get("fili", 0), ((0, 0, 4 if dadi.tira(4) == 0 else 3), (0, 1, 1)))
    _segni(toni, dadi, stile.get("puntini", 0), ((0, 0, 4),))
    _segni(toni, dadi, stile.get("zolle", 0), ((0, 0, 3), (1, 0, 1), (0, 1, 1), (1, 1, 0)))
    _segni(toni, dadi, stile.get("pozze", 0), ((1, 0, 0), (2, 0, 0), (0, 1, 0), (1, 1, 0), (2, 1, 0), (3, 1, 0),
                                               (1, 2, 0), (2, 2, 0), (1, 1, 3)))
    return toni


def _da_toni(toni, rampa):
    im = forme.nuova(T, T)
    im.putdata([tuple(rampa[toni[y][x]]) + (255,) for y in range(T) for x in range(T)])
    return im


@functools.lru_cache(maxsize=None)
def _suolo(suolo, variante):
    return _da_toni(_toni_suolo(suolo, variante), SUOLI[suolo])


# --- water ------------------------------------------------------------------------------------

@functools.lru_cache(maxsize=None)
def _acqua(suolo, fotogramma):
    """Ripples: contour lines of two noise layers, one scrolling 8 px per frame East, the other
    North (4 frames make a loop), with a few troughs a tone darker. Deep water: fewer, dimmer
    ripples."""
    profonda = ACQUA[suolo] == "acqua-profonda-anim"
    a, b = _rumore(_seme("acqua", suolo, "a"), 16), _rumore(_seme("acqua", suolo, "b"), 8)
    s = fotogramma * T // FOTOGRAMMI_ACQUA
    v = [[a[y][(x + s) % T] + b[(y - s) % T][x] for x in range(T)] for y in range(T)]
    media = sorted(v[y][x] for y in range(T) for x in range(T))[T * T // 2]
    seme = _seme("acqua", suolo)
    linee = [[abs(v[y][x] - media) for x in range(T)] for y in range(T)]
    toni = _quote(v, seme, [(6, 1)])
    ordine = sorted((linee[y][x], hash_cella(x, y, seme), y, x) for y in range(T) for x in range(T))
    n = T * T * (5 if profonda else 7) // 100
    for i, (_, _, y, x) in enumerate(ordine[:n]):
        toni[y][x] = 3 if profonda or i % 5 else 4
    return _da_toni(toni, SUOLI[suolo])


# --- edges ------------------------------------------------------------------------------------

def _profilo(seme):
    """Jagged fringe depth per column (periodic): 3..8 px."""
    lat = [hash_cella(i, 0, seme) % 5 for i in range(4)]
    out = []
    for x in range(T):
        i, f = divmod(x, 8)
        grosso = (lat[i] * (8 - f) + lat[(i + 1) % 4] * f) // 8  # 0..4
        out.append(3 + grosso + (hash_cella(x, 1, seme) & 1))
    return out


# Neighbour offsets in BORDI order (N, E, S, O, NE, SE, SO, NO); y grows southwards.
VICINI = {"N": (0, -1), "E": (1, 0), "S": (0, 1), "O": (-1, 0),
          "NE": (1, -1), "SE": (1, 1), "SO": (-1, 1), "NO": (-1, -1)}


def pezzi_bordo(suolo, vicini):
    """Edge pieces drawn on a cell of floor `suolo`, as [(floor, piece index in the bordo strip)]
    in drawing order. `vicini` maps each BORDI side to the neighbour's floor. A side piece for
    every side neighbour with a higher edgePriority; a corner piece for a diagonal neighbour with
    a higher edgePriority only when neither of the two sides next to that corner is the same
    floor (otherwise the side fringes already cover the corner). Lowest priority first, then
    BORDI order, so the highest floor ends on top."""
    pezzi = []
    for i, lato in enumerate(BORDI):
        altro = vicini[lato]
        if PRIORITA[altro] <= PRIORITA[suolo]:
            continue
        if len(lato) == 2 and altro in (vicini[lato[0]], vicini[lato[1:]]):
            continue
        pezzi.append((PRIORITA[altro], i, altro))
    return [(altro, i) for _, i, altro in sorted(pezzi)]


def _ruota(maschera):
    """Pixel set turned a quarter clockwise: (x, y) -> (31 - y, x)."""
    return {(T - 1 - y, x) for x, y in maschera}


@functools.lru_cache(maxsize=None)
def _bordo(suolo):
    prof = _profilo(_seme("bordo", suolo))
    nord = {(x, y) for x in range(T) for y in range(prof[x])}
    rx, ry = prof[T - 1], prof[0]  # corner radii: ends of the side fringes it joins
    ne = {(x, y) for x in range(T) for y in range(T)
          if (2 * (T - x) - 1) ** 2 * ry * ry + (2 * y + 1) ** 2 * rx * rx <= 4 * rx * rx * ry * ry}
    lati, angoli = [nord], [ne]
    for _ in range(3):
        lati.append(_ruota(lati[-1]))
        angoli.append(_ruota(angoli[-1]))
    trama = _suolo(suolo, 0)
    pezzi = []
    for maschera in lati + angoli:
        im = forme.nuova(T, T)
        for x, y in maschera:
            im.putpixel((x, y), trama.getpixel((x, y)))
        pezzi.append(im)
    return forme.striscia(pezzi)


# --- walls ------------------------------------------------------------------------------------

# Raised rock seen from above and a little from the front. A wall cell fills its cell, inset by
# INSETTO (+ a jag of 0..FRASTAGLIO) on each side that faces a cell without this wall, with a
# SMUSSO cut on outer corners and a small notch on inner ones: angular blocks that merge into
# ridges. Inside the footprint: outline, a light top face (facets, cracks, lit top-left rim), a
# dark cliff ALTEZZA px tall on the bottom side and a LATO px side face on the right, then a soft
# drop shadow on the floor. The look only depends on the footprint within MARGINE_PARETE px, and
# a pixel that close to the piece belongs to one of its four cells and never feels a cell beyond
# them (test_parete_margine_esatto): neighbouring pieces join exactly at their shared corners.
INSETTO = 2
FRASTAGLIO = 2
SMUSSO = 6
ALTEZZA = 5
LATO = 3
MARGINE_PARETE = ALTEZZA + 1
OMBRA_PARETE = 80  # alpha of the drop shadow on the floor
_BIT = {(0, 0): NO, (1, 0): NE, (1, 1): SE, (0, 1): SO}

# Wall look: tones of the top-face facets, facet border tone, texture extra.
STILI_PARETI = {
    "stone-wall": dict(facce=(2, 3, 3, 2, 3), solchi=1, crepe=3),
    "sand-wall": dict(facce=(3, 2, 3, 3, 2), solchi=2, strati=True),
    "ice-wall": dict(facce=(3, 4, 3, 4, 3), solchi=2, riflessi=True),
    "shrub-wall": dict(facce=(2, 3, 2, 3, 3), solchi=1, foglie=True),
}


def _frastaglio(t):
    """Inset jag (0..FRASTAGLIO) along a cell side at position t: periodic on 32 px and symmetric
    (t and 31 - t match), so it survives the quarter turns and continues across pieces."""
    u = min(t % T, T - 1 - t % T)
    return hash_cella(u // 3, 7, 0x51DE) % (FRASTAGLIO + 1)


def _pieno(angoli, x, y, esterni=False):
    """Is pixel (x, y) of a piece (-16 <= x, y < 48) inside the wall footprint? `esterni` is
    the wall state assumed for the cells beyond the four corners (it never changes the result
    within MARGINE_PARETE px of the piece)."""
    i, j = (x + 16) // T, (y + 16) // T

    def muro(a, b):
        return bool(angoli & _BIT[(a, b)]) if (a, b) in _BIT else esterni

    if not muro(i, j):
        return False
    dx, dy = 2 * x + 1 - 2 * T * i, 2 * y + 1 - 2 * T * j  # from the cell centre, half pixels
    sx, sy = (1 if dx > 0 else -1), (1 if dy > 0 else -1)
    gx, gy = T - abs(dx), T - abs(dy)  # to the nearer vertical / horizontal side, half pixels
    ix, iy = 2 * (INSETTO + _frastaglio(y)), 2 * (INSETTO + _frastaglio(x))
    vx, vy = muro(i + sx, j), muro(i, j + sy)
    if (not vx and gx < ix) or (not vy and gy < iy):
        return False
    if not vx and not vy:
        return (gx - ix) + (gy - iy) >= 2 * SMUSSO
    if vx and vy and not muro(i + sx, j + sy):
        return gx >= 2 * INSETTO or gy >= 2 * INSETTO
    return True


def _facce(seme, n=5):
    """Facet index per pixel: nearest of n seed points on the 32 px torus (periodic)."""
    punti = [(hash_cella(k, 1, seme) % T, hash_cella(k, 2, seme) % T) for k in range(n)]
    out = [[0] * T for _ in range(T)]
    for y in range(T):
        for x in range(T):
            out[y][x] = min(range(n), key=lambda k: (min(abs(x - punti[k][0]), T - abs(x - punti[k][0])) ** 2
                                                     + min(abs(y - punti[k][1]), T - abs(y - punti[k][1])) ** 2, k))
    return out


@functools.lru_cache(maxsize=None)
def _trama_parete(parete):
    """Tone grid of the wall top face, periodic on the piece so pieces join."""
    stile = STILI_PARETI[parete]
    seme = _seme("parete", parete)
    facce = _facce(seme)
    toni = [[stile["facce"][facce[y][x]] for x in range(T)] for y in range(T)]
    for y in range(T):
        for x in range(T):
            if facce[y][x] != facce[(y + 1) % T][x] or facce[y][x] != facce[y][(x + 1) % T]:
                toni[y][x] = stile["solchi"]  # facet border
    if stile.get("foglie"):  # leafy: fine light and dark speckles over the facets
        for y in range(T):
            for x in range(T):
                h = hash_cella(x, y, seme) % 10
                toni[y][x] = 1 if h < 2 else 4 if h == 9 else toni[y][x]
    if stile.get("strati"):  # sandstone: broken, wavy strata every 16 rows
        onda = _rumore(seme + 1, 16)
        for y in range(T):
            for x in range(T):
                if (y + onda[y][x] // 64) % 16 == 0 and hash_cella(x // 3, y, seme) % 4:
                    toni[y][x] = 1
    if stile.get("riflessi"):  # ice: diagonal shine streaks
        for y in range(T):
            for x in range(T):
                if (x - y) % 16 == 0 and hash_cella((x + y) // 6, 0, seme) % 3 == 0:
                    toni[y][x] = 4
    dadi = _Dadi(seme + 3)
    for _ in range(stile.get("crepe", 0)):  # a few thin diagonal cracks, inside the piece
        x, y = 4 + dadi.tira(T - 12), 4 + dadi.tira(T - 12)
        verso = 1 if dadi.tira(2) else -1
        for _ in range(7):
            if 2 <= x < T - 2 and y < T - 2:
                toni[y][x] = 0
            x += verso if dadi.tira(4) else 0
            y += 1
    return toni


def _pezzo_parete(parete, angoli):
    rampa = PARETI[parete]
    m = MARGINE_PARETE
    pieni = {(x, y) for y in range(-m, T + m) for x in range(-m, T + m) if _pieno(angoli, x, y)}
    trama = _trama_parete(parete)
    im = forme.nuova(T, T)
    for y in range(T):
        for x in range(T):
            def fuori(ax, ay):
                return (x + ax, y + ay) not in pieni

            if (x, y) in pieni:
                if fuori(1, 0) or fuori(-1, 0) or fuori(0, 1) or fuori(0, -1):
                    c = rampa[0]  # outline
                elif any(fuori(0, k) for k in range(2, ALTEZZA + 1)):
                    c = rampa[0] if (x + hash_cella(x, 3, 0xC1FF)) % 5 == 0 else rampa[1]  # cliff, streaked
                elif fuori(0, ALTEZZA + 1):
                    c = rampa[3]  # rim where the top face meets the cliff
                elif any(fuori(k, 0) for k in range(2, LATO + 1)):
                    c = rampa[1]  # right side face
                elif fuori(-1, -1) or fuori(-2, 0) or fuori(0, -2):
                    c = rampa[4]  # lit top-left rim
                else:
                    c = rampa[trama[y][x]]
                im.putpixel((x, y), tuple(c) + (255,))
            elif any((x - ax, y - ay) in pieni for ax, ay in ((1, 1), (2, 2), (1, 2), (2, 1))):
                im.putpixel((x, y), (0, 0, 0, OMBRA_PARETE))
    return im


@functools.lru_cache(maxsize=None)
def _parete(parete):
    return forme.striscia([_pezzo_parete(parete, i) for i in range(16)])


# --- deposits and decorations -----------------------------------------------------------------

def _sasso(im, pieni, rampa):
    """Shaded stone: outline, lit top-left, shaded bottom-right (forme.sagoma)."""
    forme.sagoma(im, pieni, rampa)


def _ellisse(cx, cy, rx, ry):
    return {(x, y) for y in range(int(cy - ry) - 1, int(cy + ry) + 2) for x in range(int(cx - rx) - 1, int(cx + rx) + 2)
            if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1}


@functools.lru_cache(maxsize=None)
def _giacimento(minerale, variante):
    """5-7 ore pebbles scattered over the cell, never touching: the floor stays visible."""
    _, rampa, _ = OGGETTI[minerale]
    dadi = _Dadi(_seme("giacimento", minerale, variante))
    im = forme.nuova(T, T)
    presi = []
    n = 5 + dadi.tira(3)
    tentativi = 0
    while len(presi) < n and tentativi < 200:
        tentativi += 1
        lato = 4 + dadi.tira(2)
        x, y = 2 + dadi.tira(T - 4 - lato), 2 + dadi.tira(T - 4 - lato)
        if any(abs(x - px) < lato + 2 and abs(y - py) < lato + 2 for px, py in presi):
            continue
        presi.append((x, y))
        _sasso(im, forme.smussato(x, y, x + lato - 1, y + lato - 1, 1), rampa)
    return forme.componi(forme.ombra(im, 60), im)


def _ciuffo(im, base_x, base_y, steli, rampa):
    """Grass tuft: one-pixel blades (x shift per step, length) from a dark base to a light tip."""
    for passo_x, lunghezza in steli:
        for k in range(lunghezza):
            tono = min(len(rampa) - 1, k * len(rampa) // lunghezza)
            forme.pixel(im, base_x + int(round(passo_x * k)), base_y - k, rampa[tono])


def _cristallo(im, cx, base, altezza, larghezza, rampa):
    _sasso(im, forme.poligono([(cx - larghezza, base), (cx - larghezza / 2, base - altezza * 0.7), (cx, base - altezza),
                          (cx + larghezza / 2, base - altezza * 0.6), (cx + larghezza, base)]), rampa)


# Angular boulder (faceted outline, taller on the left) for the capped decorations.
_MASSO = [(9, 24), (9, 18), (12, 12), (15, 10), (18, 13), (21, 13), (24, 17), (24, 22), (21, 25), (12, 25)]


def _cappello(im, colori, fino, discesa):
    """Snow or moss over the top of a stone: rows above `fino`, plus columns that run up to
    `discesa` px further down (a ragged edge), inside the outline only."""
    for x in range(T):
        limite = fino + hash_cella(x // 2, 5, fino) % (discesa + 1)
        for y in range(limite):
            if im.getpixel((x, y))[3] and im.getpixel((x, y))[:3] != OUT:
                forme.pixel(im, x, y, colori[(x * 3 + y) % len(colori)])


def _deco(bioma, n):
    im = forme.nuova(T, T)
    roccia = PARETE_PIETRA[1:]
    if bioma == "rocce":
        if n == 0:
            _sasso(im, _ellisse(16, 17, 6.5, 5), roccia)
        elif n == 1:
            _sasso(im, _ellisse(12, 19, 4.5, 3.5), roccia)
            _sasso(im, _ellisse(20, 14, 3.5, 3), roccia)
        else:
            for cx, cy in ((10, 14), (17, 20), (22, 12)):
                _sasso(im, _ellisse(cx, cy, 3, 2.5), roccia)
    elif bioma == "sabbia":
        secco = [SABBIA_SCURA[0], SABBIA_SCURA[2], SABBIA[3], SABBIA[4]]
        if n == 0:
            _ciuffo(im, 16, 22, ((-0.6, 7), (-0.3, 9), (0.0, 10), (0.25, 9), (0.55, 7), (0.9, 5)), secco)
        elif n == 1:
            _sasso(im, _ellisse(16, 18, 6, 4), PARETE_SABBIA[1:])
        else:
            _sasso(im, _ellisse(12, 17, 4, 3), PARETE_SABBIA[1:])
            _sasso(im, _ellisse(20, 21, 3, 2.5), PARETE_SABBIA[1:])
    elif bioma == "neve":
        if n == 0:
            _cristallo(im, 16, 23, 13, 4, PARETE_GHIACCIO[1:])
        elif n == 1:  # angular rock under a snow cap with a ragged lower edge
            _sasso(im, forme.poligono(_MASSO), roccia)
            _cappello(im, (NEVE[3], NEVE[4], NEVE[2]), 14, 3)
        else:
            _cristallo(im, 12, 22, 9, 3, PARETE_GHIACCIO[1:])
            _cristallo(im, 19, 21, 7, 2.5, GHIACCIO)
    else:  # prati
        if n == 0:
            arbusto = _ellisse(13, 18, 5, 4.5) | _ellisse(19, 17, 5, 5) | _ellisse(16, 13, 4.5, 4)
            _sasso(im, arbusto, PARETE_ARBUSTI[1:])
            for x, y in ((14, 12), (19, 14), (12, 17), (17, 17)):
                forme.pixel(im, x, y, PARETE_ARBUSTI[4])
        elif n == 1:
            _ciuffo(im, 16, 22, ((-0.6, 6), (-0.3, 8), (0.0, 9), (0.3, 8), (0.6, 6)), [ERBA[0], ERBA[2], MUSCHIO[3], MUSCHIO[4]])
        else:  # angular rock with moss growing down its top
            _sasso(im, forme.poligono(_MASSO), roccia)
            _cappello(im, (MUSCHIO[2], MUSCHIO[3], MUSCHIO[2], MUSCHIO[4]), 13, 5)
    return forme.componi(forme.ombra(im, 60), im)


DECORAZIONI = 3  # per biome


# --- registry ---------------------------------------------------------------------------------

def _registra(nome, fn, *args):
    sprite(nome)(lambda: fn(*args).copy())
    NOMI.append(nome)


NOMI = []  # every terrain sprite name, in registration order
for _s in SUOLO_IDS:
    for _n in range(VARIANTI):
        _registra(f"terreno-{_s}-{_n}", _suolo, _s, _n)
    _registra(f"terreno-{_s}-bordo", _bordo, _s)
for _s, _nome in ACQUA.items():
    _registra(_nome, lambda s: forme.striscia([_acqua(s, k) for k in range(FOTOGRAMMI_ACQUA)]), _s)
for _p in PARETE_IDS:
    _registra(f"parete-{_p}", _parete, _p)
for _m in MINERALI:
    for _n in range(GIACIMENTI):
        _registra(f"giacimento-{_m}-{_n}", _giacimento, _m, _n)
for _b in BIOMI:
    for _n in range(DECORAZIONI):
        _registra(f"deco-{_b}-{_n}", _deco, _b, _n)
