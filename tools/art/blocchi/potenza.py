"""Power sprites: generator (base + turning rotor), power node (1x1) and power node T2 (2x2).

The generator base has rounded corners, yellow-and-black hazard bands and cooling fins, so it
reads apart from the production blocks; the rotor strip (4 frames) turns in its round housing.
`<id>.png` is the composed icon. Power nodes are seen from above: insulators on a plate,
copper windings and a yellow cap.
"""
import math

import forme
from blocchi import sprite
from palette import ENERGIA, FERRO, FULIGGINE, METALLO, NASTRO, OUT, RAME

T = 32
L = 2 * T
C = L / 2

ROTORE_R = 16.5  # rotor radius inside its housing (ring 17-21)
PALE = 6
ROTORE_FOTOGRAMMI = 4
# 6 blades: 4 frames of a quarter of the blade pitch close the loop.
ROTORE_PASSO = (2 * math.pi / PALE) / ROTORE_FOTOGRAMMI


def _strisce_pericolo(im, x0, y0, x1, y1):
    """Diagonal yellow-and-black hazard band (inclusive rectangle) with an outline."""
    forme.rett(im, x0, y0, x1, y1, OUT)
    for y in range(y0 + 1, y1):
        for x in range(x0 + 1, x1):
            forme.pixel(im, x, y, ENERGIA[2] if (x + y) // 3 % 2 == 0 else FULIGGINE[1])


def _base_generatore():
    im = forme.nuova(L, L)
    forme.sagoma(im, forme.arrotondato(0, 0, L - 1, L - 1, 9), FULIGGINE)
    _strisce_pericolo(im, 10, 2, L - 11, 7)
    _strisce_pericolo(im, 10, L - 8, L - 11, L - 3)
    # Cooling fins on the sides.
    for x0 in (2, L - 9):
        forme.rett(im, x0, 12, x0 + 6, L - 13, OUT)
        for y in range(13, L - 13):
            c = METALLO[3] if y % 3 == 0 else METALLO[1] if y % 3 == 1 else OUT
            forme.rett(im, x0 + 1, y, x0 + 5, y, c)
    # Rotor housing: a raised steel ring around a dark well.
    for y in range(L):
        for x in range(L):
            dx, dy = x + 0.5 - C, y + 0.5 - C
            d = math.hypot(dx, dy)
            if d <= 17:
                forme.pixel(im, x, y, NASTRO[0] if d < 15.5 else NASTRO[1])
            elif d <= 22:
                lit = forme._luce(dx, dy)
                if d > 21 or d < 18:
                    c = OUT
                else:
                    c = METALLO[4] if lit > 0.5 else METALLO[3] if lit > 0 else METALLO[2] if lit > -0.5 else METALLO[1]
                forme.pixel(im, x, y, c)
    # Housing bolts at 45 degrees.
    for a in range(4):
        ang = math.pi / 4 + a * math.pi / 2
        forme.bullone(im, int(C + 19.5 * math.cos(ang)) - 1, int(C + 19.5 * math.sin(ang)) - 1, METALLO)
    # Corner terminals (yellow caps where the power leaves).
    for cx, cy in ((7, 10), (L - 8, 10), (7, L - 11), (L - 8, L - 11)):
        forme.cerchio(im, cx, cy, 2.5, ENERGIA)
        forme.pixel(im, int(cx), int(cy), OUT)
    return im


def rotore(angolo):
    """Turbine rotor seen from above: 6 swept blades on a hub with a yellow cap."""
    im = forme.nuova(L, L)
    pieni = {}
    for y in range(L):
        for x in range(L):
            dx, dy = x + 0.5 - C, y + 0.5 - C
            d = math.hypot(dx, dy)
            if d > ROTORE_R:
                continue
            if d <= 5:
                pieni[(x, y)] = ENERGIA[3] if forme._luce(dx, dy) > 0.2 or d < 2 else ENERGIA[2]
                continue
            a = math.atan2(dy, dx) - angolo - d * 0.045  # blades swept back
            fase = (a % (2 * math.pi / PALE)) / (2 * math.pi / PALE)
            if fase < 0.5:
                # Each blade is lit on its leading edge and darker towards the trailing one.
                c = FERRO[4] if fase < 0.12 else FERRO[3] if fase < 0.3 else FERRO[2]
                pieni[(x, y)] = c
    for (x, y), c in pieni.items():
        bordo = any((x + ax, y + ay) not in pieni for ax, ay in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        forme.pixel(im, x, y, OUT if bordo else c)
    forme.pixel(im, int(C) - 1, int(C) - 1, ENERGIA[4])
    return im


sprite("generator-base")(_base_generatore)
sprite("generator-rotor-anim")(lambda: forme.striscia([rotore(k * ROTORE_PASSO) for k in range(ROTORE_FOTOGRAMMI)]))
sprite("generator")(lambda: forme.componi(_base_generatore(), rotore(0)))


# ---------------------------------------------------------------- power nodes

def _isolatore(im, cx, cy, r):
    """Insulator from above: copper winding ring, ceramic discs and a yellow cap."""
    forme.cerchio(im, cx, cy, r, RAME)
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            d = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
            if r - 1 < d <= r:
                forme.pixel(im, x, y, OUT)
            elif r - 2.2 < d <= r - 1 and (x + y) % 2 == 0:
                forme.pixel(im, x, y, RAME[1])  # winding turns
    forme.cerchio(im, cx, cy, r - 2.5, METALLO[2:])
    forme.cerchio(im, cx, cy, max(1.5, r - 4.5), ENERGIA)
    forme.pixel(im, int(cx) - 1, int(cy) - 1, ENERGIA[4])


@sprite("power-node")
def power_node():
    im = forme.nuova(T, T)
    forme.sagoma(im, forme.smussato(0, 0, T - 1, T - 1, 6), METALLO[:5])
    # Yellow-and-black ticks across the cut corners, a bolt on each side.
    for y in range(T):
        for x in range(T):
            dx, dy = min(x, T - 1 - x), min(y, T - 1 - y)
            if dx + dy in (8, 9) and dx >= 1 and dy >= 1:
                forme.pixel(im, x, y, ENERGIA[2] if dx % 2 else FULIGGINE[1])
    for bx, by in ((15, 2), (15, T - 4), (2, 15), (T - 4, 15)):
        forme.bullone(im, bx, by, METALLO)
    _isolatore(im, T / 2, T / 2, 8.5)
    return im


@sprite("power-node-t2")
def power_node_t2():
    im = forme.nuova(L, L)
    forme.sagoma(im, forme.smussato(0, 0, L - 1, L - 1, 4), METALLO[:5])
    # Hazard corners.
    for y in range(L):
        for x in range(L):
            dx, dy = min(x, L - 1 - x), min(y, L - 1 - y)
            if 6 <= dx + dy <= 11 and dx >= 1 and dy >= 1:
                forme.pixel(im, x, y, ENERGIA[2] if (dx + dy) % 3 else FULIGGINE[1])
    # Transformer in the middle, insulators on the four quadrants, copper bus between them.
    pos = (17, L - 17)
    for a in pos:
        forme.tubo(im, pos[0], a - 1, pos[1], a + 1, RAME)
        forme.tubo(im, a - 1, pos[0], a + 1, pos[1], RAME)
    forme.pannello(im, 24, 24, L - 25, L - 25, FERRO)
    forme.griglia(im, 27, 27, L - 28, L - 28, FERRO, 3)
    for cx in pos:
        for cy in pos:
            _isolatore(im, cx, cy, 8.5)
    return im
