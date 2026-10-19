"""Core sprites: four tiers of one family, 2x2 (`core-1`, the starting Core) up to 5x5 (`core-4`).

Every tier is a steel body inside a dark armoured frame with item ports on each side, a blue
deck and, in the middle, the nucleus: a cyan crystal in a recessed pit ringed by raised rings.
Each tier up is grander: a heavier frame, one more ring, more lamps, then corner modules,
conduits and a ring of lights round the nucleus.

`core-<n>-light-anim` (4 frames) is the slow light of the nucleus: every frame is an opaque
disc over the pit, so the client stacks it on `core-<n>.png` and it replaces the nucleus
(never adds a second glow). `core-<n>.png` is the body with frame 1; `core-1` is also the HUD
and research icon.
"""
import math

import forme
from blocchi import sprite
from palette import FERRO, FULIGGINE, METALLO, NASTRO, NUCLEO, OUT

T = 32
TIER = 4
# Light levels of the nucleus frames: bright, half, dim, half (a slow pulse when looped).
LIVELLI = (2, 1, 0, 1)


def _misure(n):
    """Geometry of tier n (1-4): side, centre, frame width, deck inset, pit radius."""
    lato = T * (n + 1)
    telaio = 4 + n  # frame band width
    ponte = 2 + telaio + 1  # deck inset from the edge
    pozzo = 7 + 2.5 * n  # pit radius = light disc radius
    return lato, lato / 2, telaio, ponte, pozzo


def _lampada(im, cx, cy, r):
    """Round cyan lamp with a dark rim and a white-cyan glint (light from the top-left)."""
    forme.cerchio(im, cx, cy, r + 1, [OUT, OUT, OUT, OUT])
    forme.cerchio(im, cx, cy, r, NUCLEO[1:])
    forme.pixel(im, int(cx - r / 2), int(cy - r / 2), NUCLEO[-1])


def _anello(im, cx, cy, r0, r1, rampa):
    """Raised ring between r0 and r1: outlined, lit on its top-left half."""
    ombra_, medio, chiaro, luce = forme._toni(rampa)
    for y in range(int(cy - r1 - 1), int(cy + r1 + 2)):
        for x in range(int(cx - r1 - 1), int(cx + r1 + 2)):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            if not r0 <= d <= r1:
                continue
            if d > r1 - 1 or d < r0 + 1:
                c = OUT
            else:
                lit = forme._luce(dx, dy)
                c = luce if lit > 0.75 else chiaro if lit > 0.25 else ombra_ if lit < -0.35 else medio
            forme.pixel(im, x, y, c)


def _porta(im, x0, y0, x1, y1):
    """Item port cut through the frame: dark slot with roller lines across the flow."""
    forme.rett(im, x0, y0, x1, y1, OUT)
    forme.rett(im, x0 + 1, y0 + 1, x1 - 1, y1 - 1, NASTRO[0])
    orizzontale = (x1 - x0) > (y1 - y0)  # slot runs along x on the West/East sides
    if orizzontale:
        for x in range(x0 + 2, x1, 2):
            forme.rett(im, x, y0 + 1, x, y1 - 1, NASTRO[2])
    else:
        for y in range(y0 + 2, y1, 2):
            forme.rett(im, x0 + 1, y, x1 - 1, y, NASTRO[2])


def _modulo(im, cx, cy, mezzo, n):
    """Corner module on the deck (tier 2+): raised steel block with a vent and a lamp on top."""
    x0, y0, x1, y1 = int(cx - mezzo), int(cy - mezzo), int(cx + mezzo) - 1, int(cy + mezzo) - 1
    forme.sagoma(im, forme.smussato(x0, y0, x1, y1, 2), METALLO[:5])
    forme.griglia(im, x0 + 3, y1 - 2 - mezzo // 2, x1 - 3, y1 - 3, METALLO, 3)
    forme.bullone(im, x0 + 2, y0 + 2, METALLO)
    forme.bullone(im, x1 - 3, y0 + 2, METALLO)
    _lampada(im, cx - 0.5, y0 + mezzo * 0.75, 1.5 + n * 0.5)


def _corpo(n):
    """Tier n body without the nucleus light (the pit is left dark)."""
    L, C, telaio, ponte, pozzo = _misure(n)
    im = forme.nuova(L, L)
    smusso = 4 + n
    forme.sagoma(im, forme.smussato(0, 0, L - 1, L - 1, smusso), METALLO[:5])
    # Heavy dark frame, armour plates split at every cell.
    esterno = forme.smussato(2, 2, L - 3, L - 3, smusso - 1)
    interno = forme.smussato(2 + telaio, 2 + telaio, L - 3 - telaio, L - 3 - telaio, max(1, smusso - telaio))
    forme.sagoma(im, esterno - interno, FULIGGINE)
    for j in range(1, n + 1):
        a = j * T
        forme.rett(im, a, 3, a, 2 + telaio - 1, OUT)
        forme.rett(im, a, L - 2 - telaio, a, L - 4, OUT)
        forme.rett(im, 3, a, 2 + telaio - 1, a, OUT)
        forme.rett(im, L - 2 - telaio, a, L - 4, a, OUT)
    # Blue steel deck with seams at the cell borders.
    forme.sagoma(im, forme.smussato(ponte, ponte, L - 1 - ponte, L - 1 - ponte, 2), FERRO)
    for j in range(1, n + 1):
        a = j * T
        forme.rett(im, a, ponte + 2, a, L - 3 - ponte, FERRO[1])
        forme.rett(im, ponte + 2, a, L - 3 - ponte, a, FERRO[1])
    # A recessed floor plate in every cell (shaded top/left, lit bottom/right).
    for cy in range(n + 1):
        for cx in range(n + 1):
            x0, y0 = max(cx * T + 4, ponte + 3), max(cy * T + 4, ponte + 3)
            x1, y1 = min(cx * T + T - 4, L - 4 - ponte), min(cy * T + T - 4, L - 4 - ponte)
            forme.rett(im, x0, y0, x1, y0, FERRO[1])
            forme.rett(im, x0, y0, x0, y1, FERRO[1])
            forme.rett(im, x0 + 1, y1, x1, y1, FERRO[3])
            forme.rett(im, x1, y0 + 1, x1, y1, FERRO[3])
    # Item ports: one per cell on every side.
    for j in range(n + 1):
        a = j * T + T // 2
        forme.rett(im, a - 4, 0, a + 3, 0, OUT)
        _porta(im, a - 4, 1, a + 3, ponte - 1)
        _porta(im, a - 4, L - ponte, a + 3, L - 2)
        _porta(im, 1, a - 4, ponte - 1, a + 3)
        _porta(im, L - ponte, a - 4, L - 2, a + 3)
    # Conduits from the nucleus to the frame along the axes (tier 2+), lamps where they meet it.
    if n >= 2:
        for x0, y0, x1, y1 in ((C - 2, ponte, C + 1, C), (C - 2, C, C + 1, L - 1 - ponte),
                               (ponte, C - 2, C, C + 1), (C, C - 2, L - 1 - ponte, C + 1)):
            forme.tubo(im, int(x0), int(y0), int(x1), int(y1), NUCLEO[:4])
    # Corner lamps (tier 1) or corner modules with lamps (tier 2+).
    if n == 1:
        for cx in (ponte + 5, L - ponte - 5):
            for cy in (ponte + 5, L - ponte - 5):
                _lampada(im, cx, cy, 2.5)
    else:
        mezzo = 4 + 3 * n
        for cx in (ponte + 3 + mezzo, L - ponte - 3 - mezzo):
            for cy in (ponte + 3 + mezzo, L - ponte - 3 - mezzo):
                _modulo(im, cx, cy, mezzo, n)
    # Lamps on the frame at the cell borders (tier 2+), where the armour plates meet.
    if n >= 2:
        m = 2 + telaio / 2
        for j in range(1, n + 1):
            a = j * T
            if abs(a - C) < 1:
                continue  # the conduit lamp sits there
            for cx, cy in ((a, m), (a, L - m), (m, a), (L - m, a)):
                _lampada(im, cx, cy, 1.5)
        for cx, cy in ((C, m), (C, L - m), (m, C), (L - m, C)):
            _lampada(im, cx, cy, 2.5)
    # Nucleus: n raised rings round the pit, bolts on the outer one.
    r = pozzo
    for j in range(n):
        _anello(im, C, C, r, r + 3.5, METALLO if j % 2 == 0 else FERRO)
        r += 4.5
    esterno_r = r - 1
    bulloni = 4 * n
    for b in range(bulloni):
        a = math.pi / bulloni + b * 2 * math.pi / bulloni
        forme.bullone(im, int(C + (esterno_r - 2.75) * math.cos(a)) - 1,
                      int(C + (esterno_r - 2.75) * math.sin(a)) - 1, METALLO)
    # Tier 4: a crown of lights round the nucleus.
    if n >= 4:
        for b in range(8):
            a = b * math.pi / 4 + math.pi / 8
            _lampada(im, C + (esterno_r + 5) * math.cos(a), C + (esterno_r + 5) * math.sin(a), 2)
    return forme.basamento(im, METALLO[:5])


def luce(n, livello):
    """Light frame of tier n at level 0 (dim) - 2 (bright): an opaque disc over the pit with the
    recessed wall, the faceted crystal and a halo whose reach follows the level."""
    L, C, _, _, pozzo = _misure(n)
    im = forme.nuova(L, L)
    cristallo = pozzo * 0.6
    alone = 1 + livello * (1 + n // 2)  # halo reach in px beyond the crystal
    b = 1 + livello  # base tone of the crystal facets
    for y in range(int(C - pozzo - 1), int(C + pozzo + 2)):
        for x in range(int(C - pozzo - 1), int(C + pozzo + 2)):
            dx, dy = x + 0.5 - C, y + 0.5 - C
            d = math.hypot(dx, dy)
            if d > pozzo:
                continue
            m = abs(dx) + abs(dy)
            if d > pozzo - 1.5:
                # Recessed wall: the far (bottom-right) side catches the light.
                c = NASTRO[2] if forme._luce(dx, dy) < -0.3 else OUT
            elif m <= cristallo - 1:
                if dx < 0 and dy < 0:
                    c = NUCLEO[min(4, b + 1)]
                elif dx >= 0 and dy >= 0:
                    c = NUCLEO[b - 1]
                else:
                    c = NUCLEO[b]
            elif m <= cristallo:
                c = OUT
            elif m <= cristallo + alone:
                fuori = m - cristallo
                c = NUCLEO[1] if fuori <= alone / 2 else (NUCLEO[0] if (x + y) % 2 == 0 else NASTRO[0])
            else:
                c = NASTRO[0]
            forme.pixel(im, x, y, c)
    # Glint in the middle of the crystal: bigger when bright.
    lato = 2 if livello == 2 else 1
    forme.rett(im, int(C) - lato, int(C) - lato, int(C) - 1, int(C) - 1, NUCLEO[4] if livello else NUCLEO[3])
    return im


def _registra(n):
    sprite(f"core-{n}-light-anim")(lambda: forme.striscia([luce(n, k) for k in LIVELLI]))
    sprite(f"core-{n}")(lambda: forme.componi(_corpo(n), luce(n, LIVELLI[0])))


for _n in range(1, TIER + 1):
    _registra(_n)
