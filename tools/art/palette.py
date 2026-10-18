"""Every colour used by the sprite generator.

Ramps go from dark to light (4-6 tones). Light always comes from the top-left:
lit edges take the high end of a ramp, shaded edges the low end. Structure ramps
come from the approved samples (.dev/pixelart-campioni); item ramps are built
around the tints in src/TIndustry.Shared/Content/ItemVisualColors.cs.
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
ENERGIA = [(84, 70, 20), (150, 128, 34), (220, 196, 60), (250, 230, 110), (255, 248, 200)]

# Materials (items and material-coloured parts).
RAME = [(96, 48, 34), (146, 78, 46), (200, 116, 66), (232, 156, 98), (250, 204, 152)]
FERRO = [(58, 66, 84), (90, 102, 124), (128, 142, 166), (172, 186, 208), (220, 228, 240)]
PIOMBO = [(52, 52, 74), (80, 80, 108), (114, 114, 148), (156, 156, 190), (196, 200, 222)]
TITANIO = [(20, 48, 56), (52, 112, 124), (120, 200, 210), (170, 230, 236), (222, 248, 250)]
CARBONE = [(8, 8, 10), (30, 28, 28), (58, 54, 50), (86, 80, 74)]
GRAFITE = [(20, 22, 26), (50, 54, 60), (88, 92, 98), (128, 134, 142), (168, 174, 182)]
SILICIO = [(60, 56, 20), (128, 118, 60), (170, 160, 90), (210, 200, 120), (240, 232, 170)]

RAMPE = {
    "METALLO": METALLO, "FULIGGINE": FULIGGINE, "BAGLIORE": BAGLIORE, "ACCENTO": ACCENTO,
    "NASTRO": NASTRO, "ENERGIA": ENERGIA, "ROSSO": ROSSO, "RAME": RAME, "FERRO": FERRO, "PIOMBO": PIOMBO, "TITANIO": TITANIO,
    "CARBONE": CARBONE, "GRAFITE": GRAFITE, "SILICIO": SILICIO,
}
