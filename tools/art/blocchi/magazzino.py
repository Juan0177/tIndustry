"""Storage sprites: container (2x2) and vault (3x3), each standalone and linked to the Core.

Both storage blocks are seen from above: an armoured steel lid with corner bolts and a cross of ribs; the
vault is bigger, heavier and has a round lock plate in the middle. The `-linked` variant (drawn while the
block touches the Core and adds to its storage) turns the accent from amber to the Core's cyan.
The Core tiers (`core-shard`, `core-foundation`, `core-nucleus`) are drawn in core.py.
"""
import forme
from blocchi import SPRITE, sprite
from palette import ACCENTO, FERRO, METALLO, NUCLEO, OUT, TITANIO

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


def _container(accento):
    im = _coperchio(2 * T, FERRO, 0)
    forme.pannello(im, 24, 24, 39, 39, accento)
    return forme.basamento(im, FERRO[:5])


def _vault(accento):
    im = _coperchio(3 * T, TITANIO, 0)
    forme.cerchio(im, 48, 48, 14, [OUT, OUT, OUT, OUT])
    forme.cerchio(im, 48, 48, 12, METALLO[1:])
    forme.cerchio(im, 48, 48, 5, accento)
    return forme.basamento(im, TITANIO[:5])


sprite("container")(lambda: _container(ACCENTO))
sprite("container-linked")(lambda: _container(NUCLEO))
sprite("vault")(lambda: _vault(ACCENTO))
sprite("vault-linked")(lambda: _vault(NUCLEO))
