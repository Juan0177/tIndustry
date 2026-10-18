"""Drone sprites seen from above, nose to the East (the client rotates them to the heading).

Three tiers of the builder drone in the accent yellow (it must read against any ground):
- `drone-1` alpha (24 px): a dart, one engine;
- `drone-2` beta (28 px): swept wings and two engine pods;
- `drone-3` gamma (32 px): the biggest, four wings (forward pair and tail pair), twin engines.
Every shape is mirror-symmetric across the flight axis; the shadow is drawn by the client.
"""
import forme
from blocchi import sprite
from palette import ACCENTO, BAGLIORE, METALLO, NUCLEO, OUT


def _dentro(px, py, poli):
    """Even-odd test of a point against a polygon."""
    dentro = False
    j = len(poli) - 1
    for i in range(len(poli)):
        xi, yi = poli[i]
        xj, yj = poli[j]
        if (yi > py) != (yj > py) and px < (xj - xi) * (py - yi) / (yj - yi) + xi:
            dentro = not dentro
        j = i
    return dentro


def _pieni(lato, poli):
    """Pixels whose centre is inside the polygon, mirrored across the flight axis so the
    shape is exactly symmetric whatever the edge rounding."""
    out = {(x, y) for y in range(lato) for x in range(lato) if _dentro(x + 0.5, y + 0.5, poli)}
    out |= {(x, lato - 1 - y) for x, y in out}
    # Drop pixel slivers (fewer than two side neighbours): they would be lone outline dots.
    out = {(x, y) for x, y in out
           if sum((x + ax, y + ay) in out for ax, ay in ((1, 0), (-1, 0), (0, 1), (0, -1))) >= 2}
    # Nose: a 2 px tip on the flight axis that reaches the East edge.
    for y in (lato // 2 - 1, lato // 2):
        x = max((px for px, py in out if py == y), default=None)
        if x is not None and x >= lato - 3:
            out |= {(px, y) for px in range(x, lato)}
    return out


def _speculare(poli, lato):
    """The polygon mirrored across the flight axis (y -> lato - y)."""
    return [(x, lato - y) for x, y in poli]


def _fusoliera(im, x0, x1, y0, y1):
    """Steel spine along the flight axis with the cyan canopy near the nose."""
    lato = im.height
    forme.tubo(im, x0, y0, x1, y1, METALLO)
    forme.rett(im, x1 - 4, y0 + 1, x1 - 2, y1 - 1, NUCLEO[2])
    forme.pixel(im, x1 - 4, y0 + 1, NUCLEO[4])
    assert y0 + y1 == lato - 1  # centred on the axis


def _motore(im, x, y0, y1):
    """Engine nozzle glowing at the tail (column x)."""
    forme.rett(im, x, y0, x, y1, BAGLIORE[3])
    forme.rett(im, x + 1, y0, x + 1, y1, BAGLIORE[2])


@sprite("drone-1")
def alpha():
    s = 24
    im = forme.nuova(s, s)
    forme.sagoma(im, _pieni(s, [(23.5, 12), (2, 2), (5, 2), (9, 12)]), ACCENTO)
    _fusoliera(im, 6, 21, 10, 13)
    _motore(im, 4, 11, 12)
    return im


@sprite("drone-2")
def beta():
    s = 28
    im = forme.nuova(s, s)
    ali = [(21, 12), (11, 0.5), (6, 0.5), (7, 12)]
    forme.sagoma(im, _pieni(s, ali), ACCENTO)
    # Two engine pods on the wings.
    for y0 in (5, s - 10):
        forme.tubo(im, 4, y0, 15, y0 + 4, METALLO)
        forme.rett(im, 12, y0 + 1, 14, y0 + 3, ACCENTO[3])
        _motore(im, 2, y0 + 1, y0 + 3)
    forme.sagoma(im, _pieni(s, [(27.5, 14), (19, 9.5), (6, 10.5), (6, 14)]), ACCENTO)
    _fusoliera(im, 7, 25, 12, 15)
    return im


@sprite("drone-3")
def gamma():
    s = 32
    im = forme.nuova(s, s)
    # Tail wings, then the big forward wings over them.
    forme.sagoma(im, _pieni(s, [(11, 13), (4, 4.5), (1.5, 4.5), (3, 13)]), ACCENTO)
    forme.sagoma(im, _pieni(s, [(24, 13), (15, 1.5), (10, 1.5), (13, 13)]), ACCENTO)
    # Wing-tip lights.
    for y in (3, s - 4):
        forme.rett(im, 11, y, 12, y, NUCLEO[3])
    forme.sagoma(im, _pieni(s, [(31.5, 16), (23, 11.5), (5, 11.5), (3, 16)]), ACCENTO)
    _fusoliera(im, 8, 29, 14, 17)
    # Twin engines either side of the spine.
    for y0 in (11, s - 14):
        forme.rett(im, 3, y0, 8, y0 + 2, OUT)
        forme.rett(im, 4, y0 + 1, 8, y0 + 1, METALLO[2])
        _motore(im, 2, y0, y0 + 2)
    return im
