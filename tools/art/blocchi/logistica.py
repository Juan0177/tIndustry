"""Logistics sprites: narrow belts in four tiers (basic, fast, express, armored; icon, straight strip,
corner strip, T merge strip, side join strips: variant C "Mindustry" of the proposal sheet), the stack
conveyor (icon and straight strip: its cells are always drawn straight), junction,
router, sorter, inverted sorter, overflow and underflow gates, bridge end and bridge span, long bridge end and span.

Every directional sprite faces East (flow towards +x); the client rotates it. The belt is as wide as
Mindustry's at 32 px: a 12 px tread (rows 10-21) between side plates of 4 px (outline, three rail tones
and an inner edge: rows 5-9 above, 22-25 below); the rest of the cell is transparent. Belt strips have
32 frames that move the tread 1 px along the flow each, so the loop has no seam and the tread scrolls
smoothly next to items moving every tick. The plates stay still; on the tread a small chevron repeats
every 16 px and a rib every 8 px, so each step shows a different phase and the direction reads.
Junction, router, sorter and extractor carry no arrows (they are not directional on screen); the
sorters and the extractor keep a 14x14 slot in the centre for the filter icon the client draws.
"""
import math

import forme
from blocchi import sprite
from palette import ACCENTO, CARTA, ENERGIA, FERRO, METALLO, NASTRO, OUT, PLASTICA, RAME, ROSSO, TITANIO

T = 32
PASSO = 1  # px the tread moves per frame: 32 frames per cell, smooth next to items moving every tick
FOTOGRAMMI = T // PASSO

# Rows across a straight belt (0 = left of the flow, North for an East belt): "o" outline, "r<k>" plate
# tone k of the tier's rail ramp, "e" inner edge (the tier's accent, else rail tone 1), "t" tread.
RIGHE = {5: "o", 6: "r4", 7: "r3", 8: "r2", 9: "e", **{v: "t" for v in range(10, 22)},
         22: "e", 23: "r2", 24: "r1", 25: "o"}
TREAD_RIGHE = range(10, 22)  # the 12 px tread
PIASTRE_SU = range(5, 10)  # top side plate
PIASTRE_GIU = range(22, 26)  # bottom side plate
CHEVRON = 18  # chevron column on the axis in frame 0 (2 px colour then outline in front)
PERIODO_FRECCIA = 16
PERIODO_COSTA = 8
ARCO = (T - 1) / (math.pi / 2)  # corner: flow position per radian (0..31 over the quarter turn)

# Tier look: rail ramp, chevron ramp, inner plate edge (None = the plain rail tone 1).
TIER = {
    "conveyor-basic": (METALLO, METALLO, None),
    "conveyor-fast": (RAME, RAME, None),
    "conveyor-express": (TITANIO[:4], TITANIO, ACCENTO[2]),
    # Armored (Mindustry's armored conveyor): riveted steel plates, red armour edge and chevrons.
    "conveyor-armored": (FERRO, ROSSO, ROSSO[2]),
}
RINFORZATI = {"conveyor-armored"}  # tiers whose plates carry rivets
# Stack conveyor (Mindustry's plastanium conveyor): plastic plates, white chevrons. Only straight: the grid never
# draws it as a corner, T or join (BeltGrid.GetShape), so it has the icon and the straight strip alone.
PACCHI = {"conveyor-stack": (PLASTICA, CARTA, PLASTICA[3])}
ASPETTO = {**TIER, **PACCHI}


def _colore(tier, u, v, spostamento=0, passo_u=1.0, frecce=True):
    """(layer, colour) at flow position u (float) and row v across the belt of an East belt whose tread is
    moved `spostamento` px; None outside the belt. Layer 0 = outline and plates (still), layer 1 = tread: a
    shape draws every layer 0 before any layer 1, so a tread crossing another piece's plate opens it.

    A pixel covers [u, u + passo_u) along the flow: 1 on a straight belt, more on the inner radii of a
    corner, where the chevron still gets every pixel it touches.
    """
    tipo = RIGHE.get(v)
    if tipo is None:
        return None
    binario, freccia, bordo = ASPETTO[tier]
    if tipo == "o":
        return 0, OUT
    if tipo == "e":
        return 0, bordo if bordo is not None else binario[1]
    if tipo[0] == "r":
        k = min(int(tipo[1:]), len(binario) - 1)
        i = math.floor(u)
        if tier in RINFORZATI and v in (7, 24) and i % 8 in (3, 4):
            return 0, FERRO[4] if i % 8 == 3 else FERRO[0]  # rivet on the armour plate, lit from the left
        return 0, binario[max(0, k - 2) if i % 8 == 7 else k]  # roller joint every 8 px
    m = u - spostamento
    dv = int(abs(v + 0.5 - T / 2))
    if frecce and dv <= 2:
        d = (m + dv - CHEVRON) % PERIODO_FRECCIA  # 0..1 inside the chevron, 1 = its outlined front
        if d < 2 or d > PERIODO_FRECCIA - passo_u:
            return 1, (freccia[3], OUT)[int(d) if d < 2 else 0]
    nervatura = math.floor(m) % PERIODO_COSTA
    if nervatura == PERIODO_COSTA - 1:
        return 1, NASTRO[0]
    if nervatura == 0:
        return 1, NASTRO[2]
    return 1, NASTRO[1]


def _riga_curva(a, b):
    """Row across the belt (v) of the corner pixel at (a, b) from the bend's pivot; -1 beyond the bend."""
    r = math.hypot(a, b)
    return -1 if r > T - 0.5 else T - 1 - int(round(r))


# Geometries: (x, y) -> (flow position, row across, span of a pixel along the flow) or None. A piece is a
# list of (geometry, chevrons[, clip]) drawn layer by layer in that order; `clip(x, y, layer)` says where the
# geometry may draw (default: everywhere).

def _dritto(x, y):
    return x, y, 1.0


def _curva_g(x, y):
    """Corner in from the West edge, out from the South edge (clockwise turn), pivot on the bottom-left
    pixel: the distance from it (rounded) gives the row across the belt and the angle the flow position
    0..31, so the West column is exactly column 0 of the straight frame and the South row its column 31."""
    return _polare(x, T - 1 - y)


def _polare(a, b):
    v = _riga_curva(a, b)
    if v < 0:
        return None
    r = math.hypot(a, b)
    u = round(math.atan2(a, b) * ARCO, 6)  # exact 0 and 31 on the edges
    # Edge pixels sit next to a straight cell: same 1 px span as there.
    return u, v, 1.0 if a == 0 or b == 0 else max(1.0, ARCO / r)


def _da_sud_g(x, y):
    """Corner in from the South edge, out from the East edge (pivot on the South-East corner)."""
    return _polare(T - 1 - y, T - 1 - x)


def _da_nord_g(x, y):
    """Mirror of `_da_sud_g`: in from the North edge, out from the East edge."""
    return _da_sud_g(x, T - 1 - y)


def _lato_nord_g(x, y):
    """Side belt coming in from the North edge, down to the straight belt's last tread row."""
    return (y, T - 1 - x, 1.0) if y <= TREAD_RIGHE[-1] else None


def _lato_sud_g(x, y):
    return _lato_nord_g(x, T - 1 - y)


def _componi(tier, fotogramma, pezzi):
    im = forme.nuova(T, T)
    for strato in (0, 1):
        for geometria, frecce, *taglio in pezzi:
            for y in range(T):
                for x in range(T):
                    g = geometria(x, y) if not taglio or taglio[0](x, y, strato) else None
                    c = g and _colore(tier, g[0], g[1], fotogramma * PASSO, g[2], frecce)
                    if c and c[0] == strato:
                        forme.pixel(im, x, y, c[1])
    return im


def nastro(tier, fotogramma):
    """Straight East belt; frame k has the tread of frame 0 moved k px East."""
    return _componi(tier, fotogramma, [(_dritto, True)])


def curva(tier, fotogramma):
    """Corner entering from the West edge and leaving from the South edge (clockwise turn)."""
    return _componi(tier, fotogramma, [(_curva_g, True)])


def unione_t(tier, fotogramma):
    """T merge (Mindustry's merging conveyor), composed from two corners: in from the North and South
    edges, out from the East edge. Nothing behind: the West side is open ground. The treads of the two
    corners overlap and merge; their plates stay each in its own half, so the East edge is the straight belt's."""
    return _componi(tier, fotogramma, [(_da_sud_g, True, lambda x, y, strato: strato == 1 or y >= T // 2),
                                       (_da_nord_g, True, lambda x, y, strato: strato == 1 or y < T // 2)])


def unione_lati(tier, fotogramma, nord, sud, frecce=True):
    """Straight East belt fed from behind and from the North (`nord`, left of the flow) and / or South
    (`sud`) edge by a side belt (`frecce`) or, without chevrons, by a building that pushes items onto it."""
    lati = ([(_lato_nord_g, frecce)] if nord else []) + ([(_lato_sud_g, frecce)] if sud else [])
    return _componi(tier, fotogramma, [*lati, (_dritto, True)])


def _registra_nastro(tier):
    sprite(tier)(lambda: nastro(tier, 0))
    sprite(tier + "-anim")(lambda: forme.striscia([nastro(tier, k) for k in range(FOTOGRAMMI)]))
    sprite(tier + "-corner-anim")(lambda: forme.striscia([curva(tier, k) for k in range(FOTOGRAMMI)]))
    sprite(tier + "-t-anim")(lambda: forme.striscia([unione_t(tier, k) for k in range(FOTOGRAMMI)]))
    # Side joins (BeltShape.JoinLeft / JoinRight / JoinBoth / JoinBuildingBoth), East-facing art: left of the
    # flow is North. The building join has plain-tread stubs: a building, not a belt, feeds it.
    unioni = [("join-left", True, False, True), ("join-right", False, True, True), ("join-both", True, True, True)]
    if tier not in RINFORZATI:  # an armored belt never blends with a building (BeltGrid.BuildingFeeders)
        unioni.append(("join-building-both", True, True, False))
    for nome, nord, sud, frecce in unioni:
        sprite(f"{tier}-{nome}-anim")(lambda nord=nord, sud=sud, frecce=frecce: forme.striscia(
            [unione_lati(tier, k, nord, sud, frecce) for k in range(FOTOGRAMMI)]))


for _tier in TIER:
    _registra_nastro(_tier)

for _tier in PACCHI:
    sprite(_tier)(lambda tier=_tier: nastro(tier, 0))
    sprite(_tier + "-anim")(lambda tier=_tier: forme.striscia([nastro(tier, k) for k in range(FOTOGRAMMI)]))


# ---------------------------------------------------------------- belt furniture

def _piastra():
    """1x1 bevelled base plate with corner bolts."""
    im = forme.nuova(T, T)
    forme.pannello(im, 0, 0, T - 1, T - 1, METALLO)
    for bx, by in ((2, 2), (T - 4, 2), (2, T - 4), (T - 4, T - 4)):
        forme.bullone(im, bx, by, METALLO)
    return im


def _canale(im, x0, y0, x1, y1):
    """Recessed belt channel: dark rubber with an outline and a lit lower/right lip."""
    forme.rett(im, x0, y0, x1, y1, OUT)
    forme.rett(im, x0 + 1, y0 + 1, x1 - 1, y1 - 1, NASTRO[1])
    forme.rett(im, x0 + 1, y0 + 1, x1 - 1, y0 + 1, NASTRO[0])
    forme.rett(im, x0 + 1, y0 + 1, x0 + 1, y1 - 1, NASTRO[0])


def _freccia(im, cx, cy, verso, rampa, lato=3):
    """Small solid arrow with its tip on (cx, cy) pointing `verso` (E, W, N or S): rows
    widen by one px on each side going back from the tip, the back row is shaded."""
    for t in range(lato + 1):
        for s in range(-t, t + 1):
            bx, by = {"E": (-t, s), "W": (t, s), "S": (s, -t), "N": (s, t)}[verso]
            forme.pixel(im, cx + bx, cy + by, rampa[2] if t == lato else rampa[3])


def _battistrada(u):
    """Still tread without chevrons at flow position u: a rib every 8 px, as on the belts."""
    return {7: NASTRO[0], 0: NASTRO[2]}.get(u % 8, NASTRO[1])


def _croce(im):
    """The 12 px treads (rows / columns 10-21) of both belts reaching every edge, each between two outline
    rows, with the ribbed tread of the belts and no arrows; the vertical one lies over the horizontal one."""
    forme.rett(im, 0, 9, T - 1, 22, OUT)
    forme.rett(im, 9, 0, 22, T - 1, OUT)
    for y in range(10, 22):
        for x in range(T):
            forme.pixel(im, x, y, _battistrada(x))
    for x in range(10, 22):
        for y in range(T):
            forme.pixel(im, x, y, _battistrada(y))


def _incrocio():
    """Two belts crossing, as in Mindustry: a plate with the 12 px treads of both belts through the middle
    and a small lighter diamond hub (8 px with its outline) where they cross; no arrows (a junction has no
    direction). All four sides are open: the client closes each side without an input or output with
    `junction-bordo`."""
    im = _piastra()
    _croce(im)
    c = (T - 1) / 2
    for y in range(T):
        for x in range(T):
            d = abs(x - c) + abs(y - c)
            if d <= 4:
                forme.pixel(im, x, y, OUT if d > 3 else METALLO[4 if x + y < T - 1 else 3])
    forme.pixel(im, 15, 15, METALLO[5])
    forme.pixel(im, 16, 16, METALLO[5])
    return im


# The icon is the fully open crossing (every side connected): the base itself.
sprite("junction-base")(_incrocio)
sprite("junction")(_incrocio)


@sprite("junction-bordo")
def junction_bordo():
    """Cap closing the North side of a junction (no input or output there): a plate over the mouth of the
    channel, transparent elsewhere. The client rotates it."""
    im = forme.nuova(T, T)
    forme.rett(im, 9, 0, 22, 3, OUT)
    forme.rett(im, 10, 0, 21, 2, METALLO[2])
    forme.rett(im, 10, 0, 21, 0, METALLO[3])
    forme.rett(im, 10, 1, 21, 1, METALLO[3])
    forme.pixel(im, 12, 1, METALLO[5])
    forme.pixel(im, 19, 1, METALLO[5])
    return im


@sprite("router")
def router():
    """Accepts from any side and hands items to the others: the four treads meet in a round chamber under a
    raised cap; no arrows."""
    im = _piastra()
    _croce(im)
    forme.cerchio(im, 16, 16, 9, METALLO)
    forme.cerchio(im, 16, 16, 5, NASTRO)
    forme.pixel(im, 14, 14, METALLO[5])
    return im


def _selettore(cornice):
    """Plate with the straight tread through and the side treads, and a square frame in the centre around
    an empty 14x14 slot (rows / columns 9-22) holding the filter icon the client draws."""
    im = _piastra()
    _croce(im)
    forme.rett(im, 7, 7, 24, 24, OUT)
    forme.rett(im, 8, 8, 23, 23, cornice[1])
    forme.rett(im, 8, 8, 23, 8, cornice[3])
    forme.rett(im, 8, 8, 8, 23, cornice[3])
    forme.rett(im, 23, 8, 23, 23, cornice[0])
    forme.rett(im, 8, 23, 23, 23, cornice[0])
    forme.rett(im, 9, 9, 22, 22, OUT)
    forme.rett(im, 10, 10, 21, 21, NASTRO[0])
    return im


@sprite("sorter")
def sorter():
    """Filtered items go on straight, the others to the sides; the amber frame's empty centre holds the
    filter icon."""
    return _selettore(ACCENTO)


@sprite("sorter-inverted")
def sorter_inverted():
    """The opposite: filtered items go to the sides, the others straight on. A red frame, broken at the
    middle of each side, tells it from the sorter at a glance."""
    im = _selettore(ROSSO)
    for x, y in ((15, 8), (16, 8), (15, 23), (16, 23), (8, 15), (8, 16), (23, 15), (23, 16)):
        forme.pixel(im, x, y, OUT)
    return im


def _gate(cornice, verso):
    """Overflow / underflow gate: the crossing treads under a square cap framed in `cornice`, with a small arrow on
    each tread (no direction on screen, so all four alike). `verso` "fuori": arrows point out of the cap, toward the
    edges (the item leaves straight ahead first); "lati": they point back toward the cap (the sides come first)."""
    im = _piastra()
    _croce(im)
    forme.rett(im, 9, 9, 22, 22, OUT)
    forme.pannello(im, 10, 10, 21, 21, METALLO)
    forme.rett(im, 12, 12, 19, 19, OUT)
    forme.rett(im, 13, 13, 18, 18, cornice[1])
    forme.rett(im, 13, 13, 18, 13, cornice[3])
    forme.rett(im, 13, 13, 13, 18, cornice[3])
    forme.rett(im, 15, 15, 16, 16, cornice[4])
    if verso == "fuori":
        frecce = ((29, 16, "E"), (2, 15, "W"), (15, 2, "N"), (16, 29, "S"))
    else:
        frecce = ((23, 15, "W"), (8, 16, "E"), (16, 8, "S"), (15, 23, "N"))
    for cx, cy, punta in frecce:
        _freccia(im, cx, cy, punta, cornice, lato=2)
    return im


@sprite("overflow-gate")
def overflow_gate():
    """Overflow gate: items go straight on, to the sides only when the way ahead is full. Amber cap, arrows leaving
    toward every edge."""
    return _gate(ACCENTO, "fuori")


@sprite("underflow-gate")
def underflow_gate():
    """Underflow gate: the overflow the other way round (sides first). Red cap, arrows turned back toward the cap."""
    return _gate(ROSSO, "lati")


@sprite("conveyor-bridge")
def conveyor_bridge():
    """Bridge end: a raised housing over the belt with the span leaving East."""
    im = _piastra()
    _canale(im, -1, 9, T, 22)
    forme.pannello(im, 6, 6, 25, 25, FERRO)
    forme.rett(im, 10, 10, 21, 21, OUT)
    forme.rett(im, 11, 11, 20, 20, NASTRO[0])
    forme.rett(im, 11, 11, 20, 11, NASTRO[2])
    _freccia(im, 19, 16, "E", ACCENTO)
    return im


def _campata(rotaia):
    """16 px of a bridge deck (East) with side rails of the ramp `rotaia`, tiled along the span; 22 px thick (~0.70 cell,
    BridgeSpanVisual.DeckPixels in the client)."""
    w, h = 16, 22
    im = forme.nuova(w, h)
    for x in range(w):
        for y, c in ((0, OUT), (1, rotaia[4]), (2, rotaia[3]), (3, OUT),
                     (18, OUT), (19, rotaia[3]), (20, rotaia[2]), (21, OUT)):
            forme.pixel(im, x, y, c)
        for y in range(4, 18):
            k = x % 8
            forme.pixel(im, x, y, NASTRO[3] if k == 0 else NASTRO[0] if k == 1 else NASTRO[1])
    return im


@sprite("conveyor-bridge-span")
def conveyor_bridge_span():
    """16 px of the bridge deck (East), tiled along the span; 22 px thick (~0.70 cell,
    BridgeSpanVisual.DeckPixels in the client)."""
    im = _campata(METALLO)
    for x in (4, 12):
        forme.pixel(im, x, 1, METALLO[5])
        forme.pixel(im, x, 19, METALLO[4])
    return im


@sprite("bridge-long")
def bridge_long():
    """Long bridge end (Mindustry's phase conveyor, without the phase): the bridge housing in titanium with a power
    lamp (it runs on power) and the span leaving East."""
    im = _piastra()
    _canale(im, -1, 9, T, 22)
    forme.pannello(im, 5, 5, 26, 26, TITANIO)
    forme.rett(im, 10, 10, 21, 21, OUT)
    forme.rett(im, 11, 11, 20, 20, NASTRO[0])
    forme.rett(im, 11, 11, 20, 11, NASTRO[2])
    _freccia(im, 19, 16, "E", ACCENTO)
    # Power lamp in the top-left corner of the housing.
    forme.rett(im, 7, 7, 9, 9, OUT)
    forme.pixel(im, 8, 8, ENERGIA[3])
    return im


@sprite("bridge-long-span")
def bridge_long_span():
    """16 px of the long bridge's deck (East), titanium rails with a yellow power stripe every 8 px; same size as the
    plain bridge's deck (BridgeSpanVisual.DeckPixels)."""
    im = _campata(TITANIO)
    for x in (4, 12):
        forme.pixel(im, x, 1, ENERGIA[3])
        forme.pixel(im, x, 19, ENERGIA[2])
    return im
