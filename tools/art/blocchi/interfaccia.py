"""Interface icons (G4): white pixel shapes with a dark outline, in the style of Mindustry's UI.

Every icon is hand-placed on a 14x14 grid (one character per pixel, '#' full, '.' empty) and
exported in two sizes into godot/assets/ui/:
- `ui-<nome>-16.png` (16x16): the shape at 1x, 1 px from the edge, ringed by a 1 px outline;
- `ui-<nome>.png` (32x32): the same shape at 2x with a 1 px outline, so the larger icon stays crisp.
The outline (`OUT`) covers every empty pixel touching the shape, holes included: a 1 px gap inside
a shape reads as a dark line. The shapes are pure white, so the client can tint them (disabled,
selected) with `Modulate`.
"""
import forme
from blocchi import sprite
from palette import OUT

BIANCO = (255, 255, 255)
GRIGLIA = 14

ICONE = {
    # Build palette categories.
    "cat-strumenti": """
        .......##.....
        ......####....
        .....######...
        ....########..
        .....########.
        ......######..
        .....##.####..
        ....###..##...
        ...###........
        ..###.........
        .###..........
        ###...........
        .#............
        ..............
    """,
    "cat-logistica": """
        ..............
        .......#......
        .......##.....
        .......###....
        .......####...
        ############..
        #############.
        #############.
        ############..
        .......####...
        .......###....
        .......##.....
        .......#......
        ..............
    """,
    "cat-produzione": """
        ..........###.
        ..........###.
        ..........###.
        ..........###.
        #...#...#.###.
        ##..##..##.##.
        ###.###.######
        ##############
        ##############
        ##.##.##.##.##
        ##.##.##.##.##
        ##############
        ##############
        ##############
    """,
    "cat-potenza": """
        ...##....##...
        ...##....##...
        ...##....##...
        .############.
        .############.
        .############.
        .############.
        ..##########..
        ...########...
        ....######....
        .....####.....
        ......##......
        ......##......
        ......##......
    """,
    # Core panel and menus.
    "menu": """
        ..............
        ##############
        ##############
        ##############
        ..............
        ..............
        ##############
        ##############
        ##############
        ..............
        ..............
        ##############
        ##############
        ##############
    """,
    "ricerca": """
        .....####.....
        .....####.....
        .....####.....
        .....####.....
        .......#......
        ..##########..
        ..#........#..
        ..#........#..
        ####......####
        ####......####
        ####......####
        ####......####
        ..............
        ..............
    """,
    "mercato": """
        .....####.....
        ...########...
        ..###....###..
        .##..####..##.
        .##.##.....##.
        ##..##......##
        ##...####...##
        ##......##..##
        ##......##..##
        .##.####...##.
        .##..##....##.
        ..###....###..
        ...########...
        .....####.....
    """,
    "campagna": """
        ##............
        #########.....
        ##########....
        ##########....
        ##############
        ##############
        ##..##########
        ##.....#######
        ##........####
        ##............
        ##............
        ##............
        ##............
        ##............
    """,
    "lucchetto": """
        ....######....
        ...########...
        ..###....###..
        ..##......##..
        ..##......##..
        ..##......##..
        ##############
        ##############
        ######..######
        #####....#####
        ######..######
        ######..######
        ##############
        ##############
    """,
    "energia": """
        ......#######.
        .....#######..
        ....#######...
        ...#######....
        ..#######.....
        .##########...
        ###########...
        ....#######...
        ....######....
        ...######.....
        ...#####......
        ..#####.......
        ..####........
        .###..........
    """,
    "impostazioni": """
        .....####.....
        ..#..####..#..
        .############.
        ..##########..
        ..####..####..
        #####....#####
        ####......####
        ####......####
        #####....#####
        ..####..####..
        ..##########..
        .############.
        ..#..####..#..
        .....####.....
    """,
    "salvataggi": """
        ############..
        ##........###.
        ##........####
        ##........####
        ##........####
        ##############
        ##############
        ##############
        ###........###
        ###........###
        ###........###
        ###........###
        ###........###
        ##############
    """,
    "lingua": """
        ....######....
        ..##..##..##..
        .##..####..##.
        .#..##..##..#.
        ##############
        #...##..##...#
        #...##..##...#
        #...##..##...#
        #...##..##...#
        ##############
        .#..##..##..#.
        .##..####..##.
        ..##..##..##..
        ....######....
    """,
}


def forma(nome):
    """Full cells {(x, y)} of an icon on the 14x14 grid."""
    righe = ICONE[nome].split()
    if len(righe) != GRIGLIA or any(len(r) != GRIGLIA for r in righe):
        raise ValueError(f"icona {nome} di misura sbagliata")
    return {(x, y) for y, r in enumerate(righe) for x, c in enumerate(r) if c == "#"}


def disegna(nome, lato):
    """Icon of side 16 (shape 1x) or 32 (shape 2x), centred, with a 1 px dark outline."""
    k = lato // 16
    margine = (lato - GRIGLIA * k) // 2
    pieni = {(margine + x * k + i, margine + y * k + j) for x, y in forma(nome) for i in range(k) for j in range(k)}
    im = forme.nuova(lato, lato)
    for x, y in pieni:
        forme.pixel(im, x, y, BIANCO)
    for y in range(lato):
        for x in range(lato):
            if (x, y) not in pieni and any((x + dx, y + dy) in pieni for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                forme.pixel(im, x, y, OUT)
    return im


def _registra(nome):
    sprite(f"ui/ui-{nome}")(lambda: disegna(nome, 32))
    sprite(f"ui/ui-{nome}-16")(lambda: disegna(nome, 16))


for _nome in ICONE:
    _registra(_nome)
