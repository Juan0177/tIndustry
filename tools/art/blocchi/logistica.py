"""Logistics sprites: belts in three tiers (icon, straight strip, corner strip, T merge strip,
side join strips),
junction, splitter, sorter, bridge end and bridge span.

Every directional sprite faces East (flow towards +x); the client rotates it. Belt strips
have 8 frames that move the tread 4 px along the flow each, so 8 frames cover one cell and
the loop has no seam. The rails stay still; on the tread the chevron repeats every 32 px and
the ribs every 16 px, so each step shows a different phase and the direction reads.
"""
import math

import forme
from blocchi import sprite
from palette import ACCENTO, FERRO, METALLO, NASTRO, OUT, RAME, TITANIO

T = 32
PASSO = 4  # px the tread moves per frame
FOTOGRAMMI = T // PASSO

# Rows across a straight belt (0 = left of the flow, North for an East belt).
BINARIO_SU = range(0, 5)  # top rail, outline on rows 0 and 4
BINARIO_GIU = range(27, 32)  # bottom rail, outline on rows 27 and 31
NASTRO_RIGHE = range(5, 27)  # the moving tread between the rails
ASSE = 16  # belt axis, between rows 15 and 16
FRECCIA = 18  # chevron back column on the axis in frame 0 (18..20 colour, 21 outline)
ARCO = (T - 1) / (math.pi / 2)  # corner: flow position per radian (0..31 over the quarter turn)

# Tier look: rail ramp, chevron ramp, inner rail edge (None = plain rail).
TIER = {
    "conveyor-basic": (METALLO, METALLO, None),
    "conveyor-fast": (RAME, RAME, None),
    "conveyor-express": (TITANIO[:4], TITANIO, ACCENTO[2]),
}


def _binario(u, v, tier):
    """Rail colour at column u (still: roller joints every 8 px) and rail row v."""
    binario, _, bordo = TIER[tier]
    if v in (0, 4, 27, 31):
        return OUT
    su = v in BINARIO_SU
    riga = v - 1 if su else v - 28  # 0..2 inside the rail
    if bordo is not None and riga == (2 if su else 0):
        return bordo  # accent edge on the belt side of each rail
    k = (3, 2, 1)[riga] - (0 if su else 1)  # top rail catches the light
    if u % 8 == 7:
        k = max(0, k - 2)  # roller joint
    elif u % 8 == 0 and riga == 1:
        k = min(len(binario) - 1, k + 2)  # roller highlight
    return binario[k]


def _colore_nastro(u, v, tier, spostamento, passo_u=1.0):
    """Colour at flow position u (float) and row v; the tread is moved `spostamento` px.

    A pixel covers [u, u + passo_u) along the flow: 1 on a straight belt, more on the inner
    radii of a corner, where the chevron still gets every pixel it touches (never thinner
    than its 4 px on the straight).
    """
    if v in BINARIO_SU or v in BINARIO_GIU:
        return _binario(math.floor(u), v, tier)
    if v in (5, 26):
        return NASTRO[0]  # groove along the rails
    freccia = TIER[tier][1]
    m = u - spostamento
    # Chevron pointing +u: the arms trail back one px per row away from the axis.
    dv = int(abs(v + 0.5 - ASSE))
    if dv <= 7:
        d = (m + dv - FRECCIA) % T  # 0..3 inside the chevron, 3 = its outlined front
        if d < 4 or d > T - passo_u:  # the pixel's span reaches the chevron
            return (freccia[2], freccia[3], freccia[4], OUT)[0 if d >= 4 else int(d)]
    nervatura = math.floor(m) % 16
    if nervatura == 7:
        return NASTRO[0]
    if nervatura == 8:
        return NASTRO[2]
    return NASTRO[1]


def nastro(tier, fotogramma):
    """Straight East belt; frame k has the tread of frame 0 moved k * 4 px East."""
    im = forme.nuova(T, T)
    for y in range(T):
        for x in range(T):
            forme.pixel(im, x, y, _colore_nastro(x, y, tier, fotogramma * PASSO))
    return im


def curva(tier, fotogramma):
    """Corner entering from the West edge and leaving from the South edge (clockwise turn).

    The straight belt is bent around the bottom-left pixel: the distance from it (rounded)
    gives the row across the belt and the angle the flow position 0..31. The West column is
    then exactly column 0 of the straight frame and the South row is its column 31, so the
    corner joins the straight cells of the same frame; only the chevron is widened on the
    inner radii.
    """
    im = forme.nuova(T, T)
    for y in range(T):
        for x in range(T):
            forme.pixel(im, x, y, _colore_curva(tier, fotogramma, x, T - 1 - y))
    forme.bullone(im, 27, 2, METALLO)
    return im


def _riga_curva(a, b):
    """Row across the belt (v) of the corner pixel at (a, b) from the bend's pivot; -1 beyond the bend."""
    r = math.hypot(a, b)
    return -1 if r > T - 0.5 else T - 1 - int(round(r))


def _colore_curva(tier, fotogramma, a, b, v=None):
    """Corner colour at (a, b) from the pivot (a along the input edge, b along the output edge);
    `v` forces the row across the belt (the T uses it to extend the tread)."""
    r = math.hypot(a, b)
    if v is None:
        if r > T - 0.5:
            return METALLO[1]  # outer corner plate beyond the bend
        v = _riga_curva(a, b)
    u = round(math.atan2(a, b) * ARCO, 6)  # exact 0 and 31 on the edges
    # Edge pixels sit next to a straight cell: same 1 px span as there.
    passo_u = 1.0 if a == 0 or b == 0 else max(1.0, ARCO / r)
    return _colore_nastro(u, v, tier, fotogramma * PASSO, passo_u)


def unione_t(tier, fotogramma, nord=True, sud=True, dietro=False):
    """T merge (Mindustry's merging conveyor), composed from two corners: in from the North and
    South edges, out from the East edge.

    The bottom half is the corner turned to come in from the South (pivot on the South-East
    corner), the top half its mirror coming in from the North (pivot on the North-East corner),
    same frame: each pixel takes the corner whose pivot is nearer, so the two inner curved rails
    meet the output belt's rails and both treads bend into the output like a corner. Where both
    corners would draw their outer groove and rail (they would cross the other input) the tread goes on; the
    West side, with no input, is closed by a straight rail that continues the side belts' outer
    rails.

    Side joins (a belt fed from behind and from a side) reuse it: `dietro` opens the West side
    (the straight frame's columns 0-4, so the belt behind runs in), and a half without an input
    (`nord` / `sud` False) is the straight frame, so that side stays closed exactly as a straight.
    """
    im = forme.nuova(T, T)
    for y in range(T):
        for x in range(T):
            if not (nord if y < T // 2 else sud):
                forme.pixel(im, x, y, _colore_nastro(x, y, tier, fotogramma * PASSO))
                continue
            if x < 5:
                # Back rail, mirrored on the axis so both input edges see the corner's joint.
                forme.pixel(im, x, y, _colore_nastro(x, y, tier, fotogramma * PASSO) if dietro
                            else _binario(min(y, T - 1 - y), x, tier))
                continue
            a, b = (T - 1 - y, T - 1 - x) if y >= T // 2 else (y, T - 1 - x)
            v = _riga_curva(a, b)
            forme.pixel(im, x, y, _colore_curva(tier, fotogramma, a, b) if v >= 6
                        else _colore_curva(tier, fotogramma, a, b, v=6))
    return im


# Side joins (fed from behind and from the sides with an input), East-facing art: left of the flow is North.
UNIONI = {"join-left": (True, False), "join-right": (False, True), "join-both": (True, True)}


def _registra_nastro(tier):
    sprite(tier)(lambda: nastro(tier, 0))
    sprite(tier + "-anim")(lambda: forme.striscia([nastro(tier, k) for k in range(FOTOGRAMMI)]))
    sprite(tier + "-corner-anim")(lambda: forme.striscia([curva(tier, k) for k in range(FOTOGRAMMI)]))
    sprite(tier + "-t-anim")(lambda: forme.striscia([unione_t(tier, k) for k in range(FOTOGRAMMI)]))
    for nome, (nord, sud) in UNIONI.items():
        sprite(f"{tier}-{nome}-anim")(lambda nord=nord, sud=sud: forme.striscia(
            [unione_t(tier, k, nord, sud, dietro=True) for k in range(FOTOGRAMMI)]))


for _tier in TIER:
    _registra_nastro(_tier)


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


def _battistrada(u, v):
    """Still tread without chevrons at flow position u and row v across the belt: a rib every 8 px."""
    return {7: NASTRO[0], 0: NASTRO[2]}.get(u % 8, NASTRO[1])


def _incrocio():
    """Two belts crossing, as in Mindustry: the vertical piece lies over the horizontal one. Each
    keeps the straight belt's rails (rows / columns 0-4 and 27-31; in a corner the two rails meet
    on a dark mitre) and its ribbed tread, without chevrons: a junction has no direction. The
    vertical tread runs through the middle, the horizontal one shows where it comes in at the West
    and East edges; a small lighter diamond hub (8 px with its outline) marks the centre. All four
    sides are open: the client closes each side without an input or output with `junction-bordo`."""
    im = forme.nuova(T, T)
    c = (T - 1) / 2
    for y in range(T):
        for x in range(T):
            treno_x, treno_y = 5 <= y <= 26, 5 <= x <= 26  # inside the E-W tread rows / N-S tread columns
            if not treno_x and not treno_y:
                # Corner: the E-W rail nearer the North / South edge, the N-S rail nearer the West /
                # East edge, a dark mitre on the diagonal.
                dx, dy = abs(x - c), abs(y - c)
                colore = _binario(x, y, "conveyor-basic") if dy > dx else _binario(y, x, "conveyor-basic") if dx > dy else OUT
            elif treno_y:
                # The N-S piece lies over the E-W one: its tread runs through the middle.
                colore = NASTRO[0] if x in (5, 26) else _battistrada(y, x)
            else:
                colore = NASTRO[0] if y in (5, 26) else _battistrada(x, y)
            forme.pixel(im, x, y, colore)
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
    """Rail closing the North side of a junction (no input or output there): the straight belt's
    rail along the edge with the groove under it, transparent below. The client rotates it."""
    im = forme.nuova(T, T)
    for x in range(T):
        for y in range(5):
            forme.pixel(im, x, y, _binario(x, y, "conveyor-basic"))
        forme.pixel(im, x, 5, NASTRO[0])
    return im


@sprite("splitter")
def splitter():
    """Input from the West, items alternate between the North and South exits."""
    im = _piastra()
    _canale(im, -1, 10, 21, 21)
    _canale(im, 10, -1, 21, T)
    forme.rett(im, 11, 11, 20, 20, NASTRO[1])
    forme.cerchio(im, 16, 16, 4.5, METALLO)
    forme.pixel(im, 14, 14, METALLO[5])
    _freccia(im, 16, 3, "N", ACCENTO)
    _freccia(im, 16, 28, "S", ACCENTO)
    return im


@sprite("sorter")
def sorter():
    """Filtered items go East, the others to North or South; the empty 14x14 centre holds
    the filter glyph drawn by the client."""
    im = _piastra()
    _canale(im, -1, 10, T, 21)
    _canale(im, 10, -1, 21, T)
    forme.rett(im, 8, 8, 23, 23, ACCENTO[1])
    forme.rett(im, 8, 8, 23, 8, ACCENTO[3])
    forme.rett(im, 8, 8, 8, 23, ACCENTO[3])
    forme.rett(im, 9, 9, 22, 22, NASTRO[0])  # glyph slot
    _freccia(im, 28, 16, "E", ACCENTO)
    return im


@sprite("conveyor-bridge")
def conveyor_bridge():
    """Bridge end: a raised housing over the belt with the span leaving East."""
    im = _piastra()
    _canale(im, -1, 10, T, 21)
    forme.pannello(im, 6, 6, 25, 25, FERRO)
    forme.rett(im, 10, 10, 21, 21, OUT)
    forme.rett(im, 11, 11, 20, 20, NASTRO[0])
    forme.rett(im, 11, 11, 20, 11, NASTRO[2])
    _freccia(im, 19, 16, "E", ACCENTO)
    return im


@sprite("conveyor-bridge-span")
def conveyor_bridge_span():
    """16 px of the bridge deck (East), tiled along the span; 22 px thick (~0.70 cell,
    BridgeSpanVisual.DeckPixels in the client)."""
    w, h = 16, 22
    im = forme.nuova(w, h)
    for x in range(w):
        for y, c in ((0, OUT), (1, METALLO[4]), (2, METALLO[3]), (3, OUT),
                     (18, OUT), (19, METALLO[3]), (20, METALLO[2]), (21, OUT)):
            forme.pixel(im, x, y, c)
        for y in range(4, 18):
            k = x % 8
            forme.pixel(im, x, y, NASTRO[3] if k == 0 else NASTRO[0] if k == 1 else NASTRO[1])
    for x in (4, 12):
        forme.pixel(im, x, 1, METALLO[5])
        forme.pixel(im, x, 19, METALLO[4])
    return im
