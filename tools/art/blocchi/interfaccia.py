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


# --- check boxes (theme of CheckButton / CheckBox) ---------------------------------------------

SPUNTA_LATO = 20  # side of the check box drawn by the client theme, UI px


def spunta(acceso, spento):
    """Square check box: 2 px edge, dark inside; when on, an accent (or disabled grey) square in the middle."""
    from palette import ACCENTO, UI_BORDO, UI_BORDO_BLOCCATO, UI_DISATTIVO, UI_FONDO_BLOCCATO, UI_FONDO_SCELTO
    n = SPUNTA_LATO
    bordo = UI_BORDO_BLOCCATO if spento else ACCENTO[3] if acceso else UI_BORDO
    im = forme.nuova(n, n)
    forme.rett(im, 0, 0, n - 1, n - 1, bordo)
    forme.rett(im, 2, 2, n - 3, n - 3, UI_FONDO_SCELTO if acceso else UI_FONDO_BLOCCATO)
    if acceso:
        forme.rett(im, 6, 6, n - 7, n - 7, UI_DISATTIVO if spento else ACCENTO[3])
    return im


SPUNTE = {
    "ui/ui-spunta-si": (True, False),
    "ui/ui-spunta-no": (False, False),
    "ui/ui-spunta-si-spenta": (True, True),
    "ui/ui-spunta-no-spenta": (False, True),
}
for _nome, (_acceso, _spento) in SPUNTE.items():
    sprite(_nome)(lambda a=_acceso, s=_spento: spunta(a, s))


# --- logo (Home) -------------------------------------------------------------------------------

LOGO_TESTO = "tINDUSTRY"


@sprite("ui/ui-logo")
def logo():
    """The game's name in the interface font at 1x, lit like the blocks (light rows on top, a 1 px extrusion below
    in the accent's dark tone, a dark outline), standing on a conveyor belt with accent chevrons. The client shows it
    at a whole multiple (Nearest)."""
    import carattere
    from palette import ACCENTO, NASTRO, OUT

    testo = carattere.scrivi(LOGO_TESTO, 1)
    alfa = testo.getchannel("A").load()
    righe = [y for y in range(testo.height) if any(alfa[x, y] for x in range(testo.width))]
    alto, basso = righe[0], righe[-1]
    pieni = {(x, y - alto) for y in range(alto, basso + 1) for x in range(testo.width) if alfa[x, y]}
    h_testo = basso - alto + 1
    estrusione = {(x, y + 1) for x, y in pieni} - pieni
    nastro_y = h_testo + 2  # one row of air under the extrusion
    w = testo.width + 2
    nastro = {(x, nastro_y + y) for x in range(-1, w - 1) for y in range(5)}
    m = 1  # outline margin
    im = forme.nuova(w + 2 * m, nastro_y + 5 + 2 * m)

    def metti(px, c):
        for x, y in px:
            forme.pixel(im, x + m + 1, y + m, c)

    metti(nastro, NASTRO[1])
    metti({(x, y) for x, y in nastro if y == nastro_y}, NASTRO[3])
    metti({(x, y) for x, y in nastro if y == nastro_y + 4}, NASTRO[0])
    # Chevrons ">" every 5 px on the belt, pointing east like a running conveyor.
    metti({(x0 + dx, nastro_y + dy) for x0 in range(1, w - 3, 5) for dx, dy in ((0, 1), (1, 2), (0, 3))}, ACCENTO[2])
    metti(estrusione, ACCENTO[0])
    for x, y in pieni:
        tono = ACCENTO[4] if y <= 1 else ACCENTO[3] if y < h_testo - 2 else ACCENTO[2]
        metti({(x, y)}, tono)
    # Dark outline around everything drawn.
    px = im.load()
    pieni_im = {(x, y) for y in range(im.height) for x in range(im.width) if px[x, y][3]}
    for y in range(im.height):
        for x in range(im.width):
            if (x, y) not in pieni_im and any((x + dx, y + dy) in pieni_im for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                forme.pixel(im, x, y, OUT)
    return im
