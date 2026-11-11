"""Research upgrades (plan 3 of the «Ricerca allargata», kind "upgrade"): research nodes with nothing to place, like the
Finance modules, so their sprite is only the one-cell Research icon. A steel plate with the mark of the group (drone
build, hold, range and mining, the boosted drone, miners, machines, storage) and its level as yellow chevrons in the
bottom-right corner: one for level 1, two for level 2.
"""
import forme
from blocchi import sprite
from palette import ACCENTO, ENERGIA, FERRO, METALLO, NUCLEO, OUT, UNITA_SOPRA

T = 32
CHEVRON_X = 25  # tip column of the level chevrons


def _piastra():
    im = forme.nuova(T, T)
    forme.pannello(im, 2, 2, T - 3, T - 3, METALLO)
    return im


def _livello(im, livello):
    """`livello` stacked yellow chevrons pointing up in the bottom-right corner, each in a dark outline."""
    for k in range(livello):
        y = T - 10 - 5 * k
        punti = {(CHEVRON_X + d * s, y + d + t) for d in range(4) for s in (-1, 1) for t in (0, 1)}
        bordo = {(x + ax, y_ + ay) for x, y_ in punti for ax in (-1, 0, 1) for ay in (-1, 0, 1)} - punti
        for x, y_ in sorted(bordo):
            forme.pixel(im, x, y_, OUT)
        for x, y_ in sorted(punti):
            forme.pixel(im, x, y_, ENERGIA[3])
    return im


def _linea(im, a, b, colore, spessore=2):
    (x0, y0), (x1, y1) = a, b
    passi = max(abs(x1 - x0), abs(y1 - y0))
    for k in range(passi + 1):
        x = x0 + (x1 - x0) * k // passi
        y = y0 + (y1 - y0) * k // passi
        forme.rett(im, x, y, x + spessore - 1, y + spessore - 1, colore)


def _martello(im):
    # A hammer: a wooden handle across the plate and a steel head on top.
    _linea(im, (7, 23), (15, 13), OUT, 4)
    _linea(im, (8, 22), (15, 14), ACCENTO[2])
    forme.pannello(im, 9, 6, 21, 12, FERRO)


def _cassa(im, x0, y0, x1, y1):
    # A wooden crate with a dark cross plank.
    forme.pannello(im, x0, y0, x1, y1, ACCENTO)
    for k in range(1, x1 - x0):
        forme.pixel(im, x0 + k, y0 + (y1 - y0) * k // (x1 - x0), ACCENTO[0])


def _bersaglio(im):
    # Range: two rings and a dot around the drone's centre.
    forme.cerchio(im, 14, 15, 9, NUCLEO, pieno=False)
    forme.cerchio(im, 14, 15, 5, NUCLEO, pieno=False)
    forme.cerchio(im, 14, 15, 2, NUCLEO)


def _piccone(im):
    # Hand mining: a pickaxe, its curved steel head over a wooden handle.
    _linea(im, (13, 24), (15, 10), OUT, 4)
    _linea(im, (14, 23), (15, 11), ACCENTO[2])
    testa = [(5, 12), (8, 8), (13, 6), (18, 6), (23, 8), (24, 11), (19, 9), (13, 9), (8, 11)]
    forme.sagoma(im, forme.poligono(testa), FERRO)


def _drone(im):
    # The boosted drone: an arrow-shaped hull seen from above with its cyan cell.
    scafo = forme.poligono([(14, 4), (23, 24), (14, 19), (5, 24)])
    forme.sagoma(im, scafo, UNITA_SOPRA)
    forme.rett(im, 13, 12, 15, 14, NUCLEO[3])


def _punta(im):
    # Miners: a drill bit pointing down with its spiral grooves.
    punta = forme.poligono([(6, 6), (22, 6), (14, 25)])
    forme.sagoma(im, punta, FERRO)
    for y in (10, 14, 18):
        _linea(im, (9 + (y - 6) // 3, y), (19 - (y - 6) // 3, y + 2), FERRO[0], 1)


def _ingranaggio(im):
    # Machines: a gear.
    forme.ingranaggio(im, 14, 15, 10, 8, FERRO, 0.0)


def _casse(im):
    # Storage: three crates stacked in a pyramid.
    _cassa(im, 4, 15, 13, 24)
    _cassa(im, 13, 15, 22, 24)
    _cassa(im, 8, 6, 17, 15)


@sprite("upgrade-drone-build")
def upgrade_drone_build():
    im = _piastra()
    _martello(im)
    return _livello(im, 1)


@sprite("upgrade-drone-hold")
def upgrade_drone_hold():
    im = _piastra()
    _cassa(im, 6, 8, 20, 22)
    return _livello(im, 1)


@sprite("upgrade-drone-range")
def upgrade_drone_range():
    im = _piastra()
    _bersaglio(im)
    return _livello(im, 1)


@sprite("upgrade-drone-mine")
def upgrade_drone_mine():
    im = _piastra()
    _piccone(im)
    return _livello(im, 1)


@sprite("drone-boost")
def drone_boost():
    im = _piastra()
    _drone(im)
    return _livello(im, 2)


@sprite("upgrade-miners-1")
def upgrade_miners_1():
    im = _piastra()
    _punta(im)
    return _livello(im, 1)


@sprite("upgrade-miners-2")
def upgrade_miners_2():
    im = _piastra()
    _punta(im)
    return _livello(im, 2)


@sprite("upgrade-machines-1")
def upgrade_machines_1():
    im = _piastra()
    _ingranaggio(im)
    return _livello(im, 1)


@sprite("upgrade-machines-2")
def upgrade_machines_2():
    im = _piastra()
    _ingranaggio(im)
    return _livello(im, 2)


@sprite("upgrade-storage-1")
def upgrade_storage_1():
    im = _piastra()
    _casse(im)
    return _livello(im, 1)


@sprite("upgrade-storage-2")
def upgrade_storage_2():
    im = _piastra()
    _casse(im)
    return _livello(im, 2)
