"""Particle sprites of the client effect manager (G3): `fx-<nome>`, drawn x2 in the world like every sprite.

Each sprite is a single square or a strip of square variants (side 8-16 px); the manager picks one variant
per particle at random (CanvasItemMaterial particle frames), it does not animate them. Unlike blocks,
particles have no dark outline: they are light, small and blend over everything (as in Mindustry).
Colours are the natural ones of each particle; godot/effetti/effetti.json only tints and fades them.
"""
import forme
from blocchi import sprite
from palette import ACCENTO, BAGLIORE, ENERGIA, FULIGGINE, LUCCIOLA, NEVE, PARETE_SABBIA, RAME, TERRA


def _sbuffo(lato, dischi, rampa):
    """Puff of overlapping shaded discs [(cx, cy, r), ...] (lit top-left, like every shape)."""
    im = forme.nuova(lato, lato)
    for cx, cy, r in dischi:
        forme.cerchio(im, cx, cy, r, rampa)
    return im


@sprite("fx-fumo")
def fumo():
    # Three 12 px soot puffs: one round, one wide, one in two lobes.
    return forme.striscia([
        _sbuffo(12, [(6, 6, 4.6)], FULIGGINE),
        _sbuffo(12, [(4.5, 6.5, 3.4), (7.5, 5.5, 3.8)], FULIGGINE),
        _sbuffo(12, [(5, 7, 3.2), (7, 4.5, 2.8), (8, 7.5, 2.6)], FULIGGINE),
    ])


@sprite("fx-vapore")
def vapore():
    # Lighter, looser puffs than smoke (generator steam).
    return forme.striscia([
        _sbuffo(12, [(6, 6, 4.2)], NEVE),
        _sbuffo(12, [(4.5, 6, 3.0), (7.5, 6.5, 3.4)], NEVE),
        _sbuffo(12, [(6, 5, 3.0), (4.5, 7.5, 2.4), (8, 8, 2.2)], NEVE),
    ])


def _scintilla(lungo):
    """8 px spark: a hot streak along the flight axis (East) with a white tip."""
    im = forme.nuova(8, 8)
    forme.rett(im, 7 - lungo, 3, 7, 4, BAGLIORE[2])
    forme.rett(im, 8 - lungo // 2, 3, 7, 4, BAGLIORE[3])
    forme.rett(im, 6, 3, 7, 4, BAGLIORE[4])
    return im


@sprite("fx-scintilla")
def scintilla():
    return forme.striscia([_scintilla(6), _scintilla(4)])


def _grumi(lato, punti, rampa):
    """Small clumps: each (x, y, size) is a lit square of 1-3 px."""
    im = forme.nuova(lato, lato)
    for x, y, s in punti:
        forme.rett(im, x, y, x + s - 1, y + s - 1, rampa[1])
        forme.rett(im, x, y, x + max(0, s - 2), y + max(0, s - 2), rampa[3])
    return im


@sprite("fx-polvere")
def polvere():
    # Dust when an item enters the Core and when a block crumbles.
    return forme.striscia([
        _grumi(8, [(2, 2, 3), (5, 5, 2)], TERRA),
        _grumi(8, [(3, 3, 3)], TERRA),
        _grumi(8, [(1, 4, 2), (4, 1, 2), (5, 5, 2)], TERRA),
    ])


@sprite("fx-granello")
def granello():
    # Sand grains blown over the desert: darker and warmer than the sand floor, so they read on it.
    return forme.striscia([
        _grumi(8, [(3, 3, 2)], PARETE_SABBIA),
        _grumi(8, [(2, 4, 2), (5, 2, 1)], PARETE_SABBIA),
        _grumi(8, [(4, 4, 1), (2, 2, 1)], PARETE_SABBIA),
    ])


def _fiocco(grande):
    """Snowflake: a plus (and a dot ring when big) in the snow ramp."""
    im = forme.nuova(8, 8)
    if grande:
        forme.rett(im, 3, 1, 4, 6, NEVE[3])
        forme.rett(im, 1, 3, 6, 4, NEVE[3])
        forme.rett(im, 3, 3, 4, 4, NEVE[4])
    else:
        forme.rett(im, 3, 2, 4, 5, NEVE[3])
        forme.rett(im, 2, 3, 5, 4, NEVE[3])
        forme.pixel(im, 3, 3, NEVE[4])
    return im


@sprite("fx-fiocco")
def fiocco():
    return forme.striscia([_fiocco(True), _fiocco(False)])


def _foglia(rampa, specchio):
    """8 px leaf on a diagonal, light upper half and a dark vein."""
    im = forme.nuova(8, 8)
    pieni = forme.poligono([(1, 6.5), (3, 2.5), (6.5, 1), (5, 5), (1.5, 7)])
    for x, y in pieni:
        lx = 7 - x if specchio else x
        c = rampa[3] if x + y < 7 else rampa[2]
        forme.pixel(im, lx, y, c)
    for i in range(1, 6):
        forme.pixel(im, (7 - i) if specchio else i, 7 - i, rampa[1])
    return im


@sprite("fx-foglia")
def foglia():
    # Autumn leaves (gold and copper): green ones vanished on the meadows they drift over.
    return forme.striscia([_foglia(ACCENTO, False), _foglia(RAME, True)])


@sprite("fx-lampo")
def lampo():
    # 16 px four-point star flash for a finished construction (drawn big and short-lived).
    im = forme.nuova(16, 16)
    forme.bagliore(im, 8, 8, 7.5, ACCENTO)
    forme.rett(im, 7, 1, 8, 14, ENERGIA[3])
    forme.rett(im, 1, 7, 14, 8, ENERGIA[3])
    forme.rett(im, 7, 4, 8, 11, ENERGIA[4])
    forme.rett(im, 4, 7, 11, 8, ENERGIA[4])
    return im


def _lucciola(r):
    """8 px firefly: a soft yellow-green halo with a bright core (glows over the night)."""
    im = forme.nuova(8, 8)
    forme.bagliore(im, 4, 4, r, LUCCIOLA)
    forme.rett(im, 3, 3, 4, 4, LUCCIOLA[4])
    return im


@sprite("fx-lucciola")
def lucciola():
    # Fireflies over the meadows at night (G3 3/4).
    return forme.striscia([_lucciola(3.8), _lucciola(3.0)])
