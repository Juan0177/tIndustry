"""Every colour used by the sprite generator.

Ramps go from dark to light (4-6 tones). Light always comes from the top-left:
lit edges take the high end of a ramp, shaded edges the low end. Structure ramps
come from the approved samples (.dev/pixelart-campioni); item ramps are built
around the tints in src/TIndustry.Shared/Content/ItemVisualColors.cs. Terrain ramps start
from the colours of the `terrain` table in data/content.json (the keys of the campaign map
files, never changed here) and are tuned by eye: floors keep a low contrast between their
middle tones (belts and buildings sit on them), walls are brighter than their table colour
so their volume reads.
"""

# Dark outline around every shape.
OUT = (43, 43, 43)

# Structures.
METALLO = [(52, 54, 60), (78, 81, 90), (106, 110, 121), (138, 143, 155), (176, 181, 192), (212, 216, 224)]
FULIGGINE = [(40, 38, 40), (60, 57, 58), (84, 79, 78), (112, 105, 100), (146, 138, 130)]
BAGLIORE = [(110, 34, 20), (196, 70, 26), (244, 128, 40), (255, 186, 80), (255, 236, 160)]
ACCENTO = [(140, 92, 52), (196, 142, 80), (236, 186, 106), (255, 211, 127), (255, 236, 190)]
NASTRO = [(30, 30, 34), (44, 44, 50), (58, 58, 66), (82, 82, 92)]  # belt rubber, from the samples
ROSSO = [(72, 22, 24), (122, 34, 34), (176, 52, 46), (220, 88, 70), (246, 150, 126)]  # advanced-tier accents
NUCLEO = [(14, 46, 62), (26, 96, 124), (54, 160, 196), (112, 214, 236), (200, 246, 255)]  # Core energy (cyan)
# Drones (Mindustry-like units): grey armour plates and the glowing team cell.
UNITA_SOTTO = [(46, 47, 56), (68, 70, 82), (82, 84, 96), (96, 98, 110), (116, 118, 130)]  # under-plates
UNITA_SOPRA = [(96, 98, 110), (132, 134, 146), (164, 166, 178), (190, 192, 202), (214, 216, 226)]  # top plates
CELLA = [(150, 84, 40), (214, 132, 60), (240, 166, 84), (255, 190, 100), (255, 240, 200)]  # team cell
ENERGIA = [(84, 70, 20), (150, 128, 34), (220, 196, 60), (250, 230, 110), (255, 248, 200)]
LUCCIOLA = [(70, 96, 20), (128, 170, 40), (188, 226, 70), (226, 250, 120), (250, 255, 200)]  # fireflies at night (G3)

# Interface (G4), for the generated check boxes: the same values as UiColors (variant Normale) FondoScelto,
# FondoBloccato, BordoBloccato and TestoDisattivo (test_spunte_come_ui_colors), plus the box edge.
UI_FONDO_SCELTO = (59, 52, 36)  # #3B3424, selected slot
UI_FONDO_BLOCCATO = (16, 17, 20)  # #101114, locked slot / empty field
UI_BORDO = (140, 148, 153)  # #8C9499, edge of an unchecked box: lighter than UiColors.Bordo, so it reads in every variant
UI_BORDO_BLOCCATO = (44, 46, 51)  # #2C2E33, edge of a disabled box
UI_DISATTIVO = (106, 110, 117)  # #6A6E75, disabled text and marks

# Materials (items and material-coloured parts).
RAME = [(96, 48, 34), (146, 78, 46), (200, 116, 66), (232, 156, 98), (250, 204, 152)]
FERRO = [(58, 66, 84), (90, 102, 124), (128, 142, 166), (172, 186, 208), (220, 228, 240)]
PIOMBO = [(46, 46, 82), (72, 72, 120), (104, 104, 162), (146, 146, 204), (192, 194, 234)]  # blue-violet, apart from FERRO
TITANIO = [(20, 48, 56), (52, 112, 124), (120, 200, 210), (170, 230, 236), (222, 248, 250)]
CARBONE = [(8, 8, 10), (30, 28, 28), (58, 54, 50), (86, 80, 74)]
GRAFITE = [(20, 22, 26), (50, 54, 60), (88, 92, 98), (128, 134, 142), (168, 174, 182)]
SILICIO = [(60, 56, 20), (128, 118, 60), (170, 160, 90), (210, 200, 120), (240, 232, 170)]

# Floors (terrain.floors). Tones 1-3 make the fine noise, 0 and 4 the pebbles and glints.
PIETRA = [(60, 64, 63), (80, 85, 83), (92, 98, 96), (106, 112, 110), (132, 138, 135)]
BASALTO = [(36, 37, 41), (50, 52, 56), (60, 62, 66), (74, 76, 81), (100, 102, 108)]
GHIAIA = [(78, 75, 69), (102, 98, 91), (118, 114, 106), (134, 130, 122), (160, 156, 148)]
SABBIA = [(122, 102, 64), (160, 136, 88), (180, 154, 100), (194, 170, 118), (218, 198, 152)]
SABBIA_SCURA = [(84, 70, 47), (110, 93, 63), (126, 106, 72), (142, 121, 86), (168, 148, 112)]
NEVE = [(150, 160, 174), (192, 199, 208), (210, 216, 222), (224, 229, 234), (244, 247, 250)]
GHIACCIO = [(98, 128, 148), (138, 170, 190), (156, 192, 212), (174, 206, 224), (214, 234, 244)]
ERBA = [(28, 48, 33), (39, 67, 47), (46, 78, 54), (58, 94, 64), (86, 124, 84)]
MUSCHIO = [(40, 60, 32), (54, 82, 43), (62, 94, 50), (76, 110, 60), (110, 140, 80)]
FANGO = [(50, 42, 32), (68, 57, 43), (78, 66, 50), (92, 80, 64), (120, 108, 90)]
TERRA = [(72, 58, 38), (94, 77, 50), (108, 88, 58), (124, 103, 72), (152, 132, 100)]
ACQUA_BASSA = [(30, 56, 72), (37, 70, 88), (42, 78, 98), (52, 90, 110), (110, 156, 176)]
ACQUA_PROFONDA = [(18, 32, 46), (24, 45, 63), (28, 52, 72), (36, 62, 84), (78, 112, 136)]
# Walls (terrain.walls). Tone 0 is the outline, close to the table colour.
PARETE_PIETRA = [(22, 24, 26), (48, 51, 54), (72, 76, 80), (98, 103, 108), (132, 138, 142)]
PARETE_SABBIA = [(70, 44, 22), (110, 74, 42), (146, 102, 62), (178, 132, 84), (212, 170, 118)]
PARETE_GHIACCIO = [(22, 46, 84), (40, 88, 148), (122, 172, 212), (180, 216, 238), (228, 244, 252)]  # pale top, deep blue cliff
PARETE_ARBUSTI = [(14, 36, 20), (28, 64, 34), (44, 92, 46), (68, 122, 60), (110, 158, 82)]

# content.json terrain id -> ramp: a new floor or wall needs its ramp here (test_terreno_copertura).
SUOLI = {
    "stone": PIETRA, "basalt": BASALTO, "gravel": GHIAIA, "sand": SABBIA, "dark-sand": SABBIA_SCURA,
    "snow": NEVE, "ice": GHIACCIO, "grass": ERBA, "moss": MUSCHIO, "mud": FANGO, "soil": TERRA,
    "shallow-water": ACQUA_BASSA, "deep-water": ACQUA_PROFONDA,
}
PARETI = {
    "stone-wall": PARETE_PIETRA, "sand-wall": PARETE_SABBIA, "ice-wall": PARETE_GHIACCIO,
    "shrub-wall": PARETE_ARBUSTI,
}

RAMPE = {
    "METALLO": METALLO, "FULIGGINE": FULIGGINE, "BAGLIORE": BAGLIORE, "ACCENTO": ACCENTO,
    "NASTRO": NASTRO, "ENERGIA": ENERGIA, "LUCCIOLA": LUCCIOLA, "NUCLEO": NUCLEO, "ROSSO": ROSSO, "RAME": RAME, "FERRO": FERRO, "PIOMBO": PIOMBO, "TITANIO": TITANIO,
    "CARBONE": CARBONE, "GRAFITE": GRAFITE, "SILICIO": SILICIO,
    "UNITA_SOTTO": UNITA_SOTTO, "UNITA_SOPRA": UNITA_SOPRA, "CELLA": CELLA,
    **{f"suolo {k}": v for k, v in SUOLI.items()}, **{f"parete {k}": v for k, v in PARETI.items()},
}
