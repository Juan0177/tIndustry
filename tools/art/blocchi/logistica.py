"""Logistics sprites: belts in three tiers (icon, straight strip, corner strip), junction,
splitter, sorter, bridge end and bridge span.

Every directional sprite faces East (flow towards +x); the client rotates it. Belt strips
have 4 frames that move the tread 8 px along the flow each, so 4 frames cover one cell and
the loop has no seam; everything else on the belt repeats every 8 px and looks still.
"""
import math

import forme
from blocchi import sprite
from palette import ACCENTO, FERRO, METALLO, NASTRO, OUT, RAME, TITANIO

T = 32
PASSO = 8  # px the tread moves per frame
FOTOGRAMMI = T // PASSO

# Rows across a straight belt (0 = left of the flow, North for an East belt).
BINARIO_SU = range(0, 5)  # top rail, outline on rows 0 and 4
BINARIO_GIU = range(27, 32)  # bottom rail, outline on rows 27 and 31
ASSE = 16  # belt axis, between rows 15 and 16

# Tier look: rail ramp, chevron ramp, inner rail edge (None = plain rail).
TIER = {
    "conveyor-basic": (METALLO, METALLO, None),
    "conveyor-fast": (RAME, RAME, None),
    "conveyor-express": (TITANIO[:4], TITANIO, ACCENTO[2]),
}


def _colore_nastro(u, v, tier, tacche=True):
    """Colour at (u along the flow, v across) of the straight tread, before scrolling.

    Period 32 along u (one chevron per cell); rails, rollers and ribs repeat every 8 px.
    """
    binario, freccia, bordo = TIER[tier]
    if v in (0, 4, 27, 31):
        return OUT
    if v in BINARIO_SU or v in BINARIO_GIU:
        su = v in BINARIO_SU
        riga = v - 1 if su else v - 28  # 0..2 inside the rail
        if bordo is not None and riga == (2 if su else 0):
            return bordo  # accent edge on the belt side of each rail
        k = (3, 2, 1)[riga] - (0 if su else 1)  # top rail catches the light
        if tacche and u % PASSO == PASSO - 1:
            k = max(0, k - 2)  # roller joint
        elif tacche and u % PASSO == 0 and riga == 1:
            k = min(len(binario) - 1, k + 2)  # roller highlight
        return binario[k]
    if v in (5, 26):
        return NASTRO[0]  # groove along the rails
    # Chevron pointing +u: the arms trail back one px per row away from the axis.
    dv = int(abs(v + 0.5 - ASSE))
    if dv <= 7:
        x = (u + dv) % T
        if 18 <= x <= 21:
            return (freccia[2], freccia[3], freccia[4], OUT)[x - 18]
    nervatura = u % PASSO
    if nervatura == 3:
        return NASTRO[0]
    if nervatura == 4:
        return NASTRO[2]
    return NASTRO[1]


def nastro(tier, fotogramma):
    """Straight East belt; frame k is frame 0 moved k * 8 px East."""
    im = forme.nuova(T, T)
    spostamento = fotogramma * PASSO
    for y in range(T):
        for x in range(T):
            forme.pixel(im, x, y, _colore_nastro(x - spostamento, y, tier))
    return im


def curva(tier, fotogramma):
    """Corner entering from the West edge and leaving from the South edge (clockwise turn).

    The straight tread is bent around the South-West corner: distance from that corner is
    the row across the belt, the angle is the position along the flow (0..32 over the
    quarter turn), so the corner joins the straight strips of the same frame seamlessly.
    """
    im = forme.nuova(T, T)
    spostamento = fotogramma * PASSO
    for y in range(T):
        for x in range(T):
            dx, dy = x + 0.5, T - (y + 0.5)
            r = math.hypot(dx, dy)
            if r > T:
                forme.pixel(im, x, y, METALLO[1])  # outer corner plate beyond the bend
                continue
            u = int(math.atan2(dx, dy) / (math.pi / 2) * T)
            v = min(T - 1, int(T - r))
            forme.pixel(im, x, y, _colore_nastro(u - spostamento, v, tier, tacche=False))
    forme.bullone(im, 27, 2, METALLO)
    return im


def _registra_nastro(tier):
    sprite(tier)(lambda: nastro(tier, 0))
    sprite(tier + "-anim")(lambda: forme.striscia([nastro(tier, k) for k in range(FOTOGRAMMI)]))
    sprite(tier + "-corner-anim")(lambda: forme.striscia([curva(tier, k) for k in range(FOTOGRAMMI)]))


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


@sprite("junction")
def junction():
    im = _piastra()
    # Two crossing channels, one per axis, reaching every edge.
    _canale(im, 10, -1, 21, T)
    _canale(im, -1, 10, T, 21)
    forme.rett(im, 11, 11, 20, 20, NASTRO[1])
    forme.pannello(im, 13, 13, 18, 18, METALLO)
    for cx, cy, v in ((15, 4, "N"), (15, 27, "S"), (4, 15, "W"), (27, 15, "E")):
        _freccia(im, cx + (1 if v in "NS" else 0), cy + (1 if v in "EW" else 0), v, METALLO, 2)
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
    """16 px of the bridge deck (East), tiled along the span; 22 px thick (~0.70 cell)."""
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
