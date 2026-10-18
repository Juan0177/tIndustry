"""Drone sprites seen from above, nose to the East (the client rotates them to the heading).

Three tiers of the builder drone in the style of Mindustry's units (own drawing, nothing copied):
layered grey armour plates with 45-degree chamfers, darker under-plates, a glowing team cell in
the middle; compact and as wide as long.
- `drone-1` alpha (24 px): wide hull with two side plates and a tail stub;
- `drone-2` beta (28 px): bigger hull, four lobed plates, forward fins;
- `drone-3` gamma (32 px): X-shaped hull with swept arms and twin forward claws.

The art is hand-placed pixel by pixel: each drone is the left half drawn nose-up (one character
per pixel, see `TONI`), mirrored, centred on a square canvas and turned nose-East. The shadow and
the pulsing engine are drawn by the client.
"""
from PIL import Image

import forme
from blocchi import sprite
from palette import CELLA, OUT, UNITA_SOPRA, UNITA_SOTTO

# One character per pixel: outline, under-plate (dark, light), top plate (shadow, mid, light),
# team cell (rim, glow, hot centre). '.' is transparent.
TONI = {
    "O": OUT,
    "1": UNITA_SOTTO[1], "2": UNITA_SOTTO[3],
    "3": UNITA_SOPRA[1], "4": UNITA_SOPRA[2], "5": UNITA_SOPRA[4],
    "c": CELLA[1], "C": CELLA[3], "W": CELLA[4],
}


def disegna(lato, mezzo):
    """Square sprite from the left half drawn nose-up: mirrored, centred, turned nose-East."""
    righe = mezzo.split()
    w = len(righe[0])
    if w * 2 != lato or len(righe) > lato or any(len(r) != w for r in righe):
        raise ValueError("metà del drone di misura sbagliata")
    im = forme.nuova(lato, lato)
    y0 = (lato - len(righe)) // 2
    for y, riga in enumerate(righe):
        for x, ch in enumerate(riga):
            if ch != ".":
                forme.pixel(im, x, y0 + y, TONI[ch])
                forme.pixel(im, lato - 1 - x, y0 + y, TONI[ch])
    return im.transpose(Image.Transpose.ROTATE_270)  # nose up -> nose East


@sprite("drone-1")
def alpha():
    return disegna(24, """
        ............
        ............
        ..........OO
        .........O55
        ....OO..O554
        ...O22OO5544
        ..O2221O5444
        ..O2211O5443
        .O22211O443O
        .O2211O5443O
        O2211O5544Oc
        O211O55443OC
        O211O5443OcW
        O211O5443OcW
        O2211O443OcC
        .O211O4433Oc
        .O2211O4333O
        ..O221O43333
        ..O2221OO333
        ...O222O.OOO
        ....OOO..O21
        .........O21
        ..........OO
        ............
    """)


@sprite("drone-2")
def beta():
    return disegna(28, """
        ..............
        ..OOO.........
        .O222O......OO
        .O2221O....O55
        .O22211O..O554
        ..O2211O.O5544
        ...O2211O55444
        ...O211O554443
        ..O2211O54443O
        .O22211O4443Oc
        O222211O443OcC
        O22211O5443OCW
        O2211O55443OCW
        O211O554443OcC
        O211O5444443Oc
        O2211O4444433O
        .O2211O4444333
        ..O2211O443333
        ...O2211OO3333
        ...O22221OOOOO
        ..O222221O.O21
        .O2222221O.O21
        .O222OOOO...OO
        ..OOO.........
    """)


@sprite("drone-3")
def gamma():
    return disegna(32, """
        ................
        ..OO............
        .O22OO.......OOO
        .O2221O.....O555
        ..O2221O...O5544
        ..O22221O.O55443
        ...O2221OO55443O
        ....O221O5544OOc
        ....O21O5544OcCC
        ...O2211O543OcCW
        ..O22211O543OcCW
        .O222211O5433OcC
        O2222211O54433Oc
        O222211O554443OO
        O22211O55444433O
        O2211O5544444333
        .O211O5443OO4333
        .O211O543O11O333
        ..O21O543O1O.O33
        ..O2O543O.O..OOO
        ...OO543O....O21
        ....O543O....O21
        .....OOO......OO
        ................
    """)
