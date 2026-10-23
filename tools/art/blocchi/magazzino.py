"""Storage sprites: container (2x2), vault (3x3) and the icons of the Core upgrades.

Both storage blocks are seen from above: an armoured steel lid with corner bolts and a cross of ribs; the
vault is bigger, heavier and has a round lock plate in the middle. `core-foundation` and `core-nucleus` are the
research icons of the Core upgrades: they reuse the sprites of the matching Core tiers (4x4 and 5x5 cells).
"""
import forme
from blocchi import SPRITE, sprite
from palette import ACCENTO, FERRO, METALLO, OUT, TITANIO

T = 32


def _coperchio(lato, rampa, nervature):
    im = forme.nuova(lato, lato)
    forme.pannello(im, 0, 0, lato - 1, lato - 1, rampa)
    for b in (3, lato - 5):
        for c in (3, lato - 5):
            forme.bullone(im, b, c, METALLO)
    mezzo = lato // 2
    for x in range(6, lato - 6):
        forme.rett(im, x, mezzo - 1, x, mezzo, rampa[1])
        forme.rett(im, mezzo - 1, x, mezzo, x, rampa[1])
    for k in range(1, nervature + 1):
        passo = (lato - 12) * k // (nervature + 1) + 6
        forme.rett(im, 6, passo, lato - 7, passo, OUT)
    return im


@sprite("container")
def container():
    im = _coperchio(2 * T, FERRO, 0)
    forme.pannello(im, 24, 24, 39, 39, ACCENTO)
    return forme.basamento(im, FERRO[:5])


@sprite("vault")
def vault():
    im = _coperchio(3 * T, TITANIO, 0)
    forme.cerchio(im, 48, 48, 14, [OUT, OUT, OUT, OUT])
    forme.cerchio(im, 48, 48, 12, METALLO[1:])
    forme.cerchio(im, 48, 48, 5, ACCENTO)
    return forme.basamento(im, TITANIO[:5])


sprite("core-foundation")(lambda: SPRITE["core-3"]())
sprite("core-nucleus")(lambda: SPRITE["core-4"]())
