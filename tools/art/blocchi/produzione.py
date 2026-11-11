"""Production sprites: miner (basic and advanced), impact drill, smelter, assembler, extractor and the machines of
the «Ricerca allargata» (advanced smelter, graphite press, separator, plasticizer).

Every block is a still base plus its moving parts, each part a full-size layer so the client
stacks them centred on the block:
- miner: `-base`, `-drill-anim` (8 frames of the drill bit turning), advanced `-lamp` (lit with power);
- impact drill (3x3, 96 px): `-base` and `-drill-anim`, like the miner, with a bigger bit;
- smelter: `-base`, `-glow-anim` (4 frames of the glowing mouth);
- assembler: `-base`, `-arm` (left arm, mirrored by the client for the right one, moved by code);
- advanced smelter, graphite press, separator, plasticizer: `-base` and the icon (still, for now).
`<id>.png` is the composed icon (palette, research, plans, construction sites).
The bases differ on purpose: chamfered grey plate (miner), red-trimmed plate with a lamp
(advanced miner), graphite deck with four titanium hammers around a wide pit (impact drill), sooty brick-red furnace with round rivets (smelter), blue steel bed on rails
(assembler), lead-coloured octagon (extractor), titanium furnace with two glowing mouths (advanced
smelter), graphite ram between two steel pillars (graphite press), sand drum with four ore chutes
(separator), magenta vat with a coal hopper (plasticizer).
"""
import math

from PIL import Image

import forme
from blocchi import sprite
from palette import (ACCENTO, BAGLIORE, CARBONE, FERRO, FULIGGINE, GRAFITE, METALLO, NASTRO, OUT, PIOMBO, PLASTICA,
                     RAME, ROSSO, SABBIA_GRANI, TITANIO)

T = 32
L = 2 * T  # side of a 2x2 block
C = L / 2  # centre of a 2x2 block (between pixels 31 and 32)


def _anello(im, cx, cy, r0, r1, rampa, contorno=True):
    """Flat ring between radii r0 and r1, shaded by the light, with an outline on both edges."""
    ombra_, medio, chiaro, luce = forme._toni(rampa)
    for y in range(int(cy - r1 - 1), int(cy + r1 + 2)):
        for x in range(int(cx - r1 - 1), int(cx + r1 + 2)):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            if not r0 <= d <= r1:
                continue
            if contorno and (d > r1 - 1 or d < r0 + 1):
                c = OUT
            else:
                lit = forme._luce(dx, dy)
                c = luce if lit > 0.75 else chiaro if lit > 0.25 else ombra_ if lit < -0.35 else medio
            forme.pixel(im, x, y, c)


def _pozzo(im, cx, cy, r, rampa):
    """Recessed round pit: the inner wall facing the light is in shadow, the far one is lit."""
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            if d > r:
                continue
            if d > r - 2.5:
                lit = -forme._luce(dx, dy)  # recessed: lit wall is the bottom-right one
                c = rampa[2] if lit > 0.4 else rampa[0] if lit < -0.2 else rampa[1]
            else:
                c = rampa[0]
            forme.pixel(im, x, y, c)


# ---------------------------------------------------------------- miner

PUNTA_R = 10.5  # drill cone radius (teeth reach PUNTA_R + 3): ~27 px wide in a 64 px frame
SOLCHI = 4  # helical flutes on the cone
DENTI = 8  # cutting teeth around the cone
TRIVELLA_FOTOGRAMMI = 8
# The bit has 4-fold symmetry, so 8 frames of a sixteenth of a turn close the loop.
TRIVELLA_PASSO = (2 * math.pi / SOLCHI) / TRIVELLA_FOTOGRAMMI


def trivella(angolo, cono, denti, collare, lato=L, scala=1):
    """Conical drill bit seen from above, turned by `angolo`: a cone with helical flutes that
    wind into the tip, a ring of square cutting teeth and a collar at the base of the cone.
    `lato` is the frame side and `scala` the size of the bit (1 in a 2x2 miner)."""
    im = forme.nuova(lato, lato)
    centro = lato / 2
    punta_r = PUNTA_R * scala
    pieni = {}
    r_denti = punta_r + 3 * scala
    for y in range(lato):
        for x in range(lato):
            dx, dy = x + 0.5 - centro, y + 0.5 - centro
            d = math.hypot(dx, dy)
            a = math.atan2(dy, dx) - angolo
            if d <= punta_r:
                if d < 1.5 * scala:
                    pieni[(x, y)] = cono[-1]  # the tip
                    continue
                # Flute: an Archimedean spiral; the edge after it catches the light.
                fase = ((a + d / scala * 0.32) % (2 * math.pi / SOLCHI)) / (2 * math.pi / SOLCHI)
                lit = forme._luce(dx, dy)
                k = 2 + (1 if lit > 0.3 else -1 if lit < -0.3 else 0)
                if d < 4 * scala:
                    k += 1  # the tip is lighter
                if fase < 0.24:
                    c = OUT if d > 3 * scala else cono[0]
                elif fase < 0.38:
                    c = cono[min(len(cono) - 1, k + 1)]
                else:
                    c = cono[max(0, min(len(cono) - 1, k))]
                if punta_r - 1.4 * scala < d:
                    c = collare[2] if lit > 0 else collare[1]  # collar ring at the base
                pieni[(x, y)] = c
            elif d <= r_denti:
                fase = (a % (2 * math.pi / DENTI)) / (2 * math.pi / DENTI)
                if fase < 0.5:
                    lit = forme._luce(dx, dy)
                    pieni[(x, y)] = denti[3] if lit > 0.2 else denti[2] if lit > -0.4 else denti[1]
    for (x, y), c in pieni.items():
        bordo = any((x + ax, y + ay) not in pieni for ax, ay in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        forme.pixel(im, x, y, OUT if bordo else c)
    return im


def _base_minatore(rampa_corpo, accento, avanzato):
    im = forme.nuova(L, L)
    forme.sagoma(im, forme.smussato(0, 0, L - 1, L - 1, 9), rampa_corpo)
    # Raised deck around the pit, chamfered like the body.
    forme.sagoma(im, forme.smussato(6, 6, L - 7, L - 7, 8), rampa_corpo)
    # Side vents.
    for x0 in (2, L - 6):
        forme.griglia(im, x0, 22, x0 + 3, 41, rampa_corpo, 3)
    # Pit with a collar ring (where the drill turns).
    _anello(im, C, C, 15.5, 20.5, accento)
    _pozzo(im, C, C, 15.5, NASTRO)
    # Ore chips at the bottom of the pit.
    for x, y in ((24, 37), (38, 25), (40, 38), (23, 26)):
        forme.pixel(im, x, y, NASTRO[3])
    # Bolts on the diagonals of the deck.
    for bx, by in ((12, 12), (L - 14, 12), (12, L - 14), (L - 14, L - 14)):
        forme.bullone(im, bx, by, rampa_corpo)
    if avanzato:
        # Red bands across the cut corners and a lamp housing on the North edge.
        for y in range(L):
            for x in range(L):
                dx, dy = min(x, L - 1 - x), min(y, L - 1 - y)
                if dx <= 12 and dy <= 12 and dx + dy in (12, 13, 14):
                    forme.pixel(im, x, y, ROSSO[3] if dx + dy == 12 else ROSSO[2])
        forme.pannello(im, 26, 0, 37, 6, METALLO)
        forme.rett(im, 29, 2, 34, 4, ROSSO[0])  # lamp glass, unlit
        forme.pixel(im, 29, 2, ROSSO[1])
    else:
        # Rust hazard marks on the chamfers.
        for bx, by in ((5, 5), (L - 7, 5), (5, L - 7), (L - 7, L - 7)):
            forme.rett(im, bx, by, bx + 1, by + 1, RAME[2])
    return forme.basamento(im, rampa_corpo)


def _lampada():
    im = forme.nuova(L, L)
    forme.bagliore(im, C, 3.5, 7, BAGLIORE)
    forme.rett(im, 29, 2, 34, 4, ROSSO[4])
    forme.rett(im, 30, 2, 33, 3, (255, 236, 220))
    return im


_MINATORI = {
    # id: (body ramp, collar/accent ramp, cone ramp, teeth ramp, advanced)
    "miner": (METALLO, RAME, METALLO, ACCENTO, False),
    "miner-advanced": (METALLO, ROSSO, FERRO, TITANIO, True),
}


def _registra_minatore(bid):
    corpo, accento, cono, denti, avanzato = _MINATORI[bid]

    def fotogramma(k):
        return trivella(k * TRIVELLA_PASSO, cono, denti, accento)

    sprite(bid + "-base")(lambda: _base_minatore(corpo, accento, avanzato))
    sprite(bid + "-drill-anim")(lambda: forme.striscia([fotogramma(k) for k in range(TRIVELLA_FOTOGRAMMI)]))
    sprite(bid)(lambda: forme.componi(_base_minatore(corpo, accento, avanzato), fotogramma(0)))


for _bid in _MINATORI:
    _registra_minatore(_bid)

sprite("miner-advanced-lamp")(_lampada)


# ---------------------------------------------------------------- impact drill

L3 = 3 * T  # side of a 3x3 block
C3 = L3 / 2
TRAPANO_SCALA = 1.5  # the bit of the 3x3 drill, 1.5 times the miner's (about 40 px across)


def _base_trapano():
    """Graphite deck, chamfered, with a titanium-collared pit in the middle and four hammer housings
    (titanium heads on graphite arms) pointing at the pit from the diagonals: the "impact" part."""
    im = forme.nuova(L3, L3)
    forme.sagoma(im, forme.smussato(0, 0, L3 - 1, L3 - 1, 13), GRAFITE)
    forme.sagoma(im, forme.smussato(8, 8, L3 - 9, L3 - 9, 12), GRAFITE)
    # Side vents, two per edge.
    for a in (30, 54):
        forme.griglia(im, 2, a, 6, a + 11, GRAFITE, 3)
        forme.griglia(im, L3 - 7, a, L3 - 3, a + 11, GRAFITE, 3)
        forme.griglia(im, a, 2, a + 11, 6, GRAFITE, 3)
        forme.griglia(im, a, L3 - 7, a + 11, L3 - 3, GRAFITE, 3)
    # Hammer housings on the diagonals: a graphite arm and a titanium head facing the pit.
    for sx, sy in ((1, 1), (-1, 1), (1, -1), (-1, -1)):
        ax = C3 + sx * 26
        ay = C3 + sy * 26
        forme.pannello(im, int(ax - 7), int(ay - 7), int(ax + 6), int(ay + 6), METALLO)
        forme.pannello(im, int(ax - 4), int(ay - 4), int(ax + 3), int(ay + 3), TITANIO)
        forme.bullone(im, int(ax - sx * 1), int(ay - sy * 1), TITANIO)
    # Wide pit with a titanium collar (where the bit turns).
    _anello(im, C3, C3, 22.5, 28.5, TITANIO)
    _pozzo(im, C3, C3, 22.5, NASTRO)
    # Ore chips at the bottom of the pit.
    for x, y in ((36, 56), (58, 37), (60, 57), (35, 39), (48, 64), (47, 31)):
        forme.pixel(im, x, y, NASTRO[3])
    # Hazard marks on the chamfers.
    for bx, by in ((6, 6), (L3 - 8, 6), (6, L3 - 8), (L3 - 8, L3 - 8)):
        forme.rett(im, bx, by, bx + 1, by + 1, ACCENTO[2])
    return forme.basamento(im, GRAFITE)


def _trivella_trapano(k):
    return trivella(k * TRIVELLA_PASSO, TITANIO, METALLO, ACCENTO, lato=L3, scala=TRAPANO_SCALA)


sprite("impact-drill-base")(_base_trapano)
sprite("impact-drill-drill-anim")(lambda: forme.striscia([_trivella_trapano(k) for k in range(TRIVELLA_FOTOGRAMMI)]))
sprite("impact-drill")(lambda: forme.componi(_base_trapano(), _trivella_trapano(0)))


# ---------------------------------------------------------------- smelter

BOCCA = (19, 21, 44, 44)  # furnace mouth (inclusive), drawn by the base and the glow frames
FORNO_FOTOGRAMMI = 4


def _sbarre(im):
    """Grate over the mouth: vertical bars every 5 px and a lintel."""
    x0, y0, x1, y1 = BOCCA
    for x in range(x0 + 3, x1, 5):
        forme.rett(im, x, y0 + 1, x + 1, y1 - 1, OUT)
        forme.rett(im, x, y0 + 1, x, y1 - 1, METALLO[2])
    forme.rett(im, x0, y0, x1, y0 + 1, OUT)


def _rivetto(im, cx, cy):
    forme.cerchio(im, cx, cy, 3.5, METALLO)
    forme.pixel(im, int(cx) - 1, int(cy) - 1, OUT)
    forme.pixel(im, int(cx), int(cy) - 1, OUT)
    forme.pixel(im, int(cx) - 1, int(cy), OUT)
    forme.pixel(im, int(cx), int(cy), OUT)


def _base_forno():
    im = forme.nuova(L, L)
    forme.pannello(im, 0, 0, L - 1, L - 1, FULIGGINE)
    # Firebrick band around the chamber.
    forme.pannello(im, 11, 11, L - 12, L - 12, RAME[:4])
    for y in range(13, L - 13, 4):
        for x in range(13 + (2 if (y // 4) % 2 else 0), L - 13, 6):
            forme.pixel(im, x, y, RAME[0])  # brick joints
    # Mouth: soot-black chamber behind the grate.
    x0, y0, x1, y1 = BOCCA
    forme.rett(im, x0 - 1, y0 - 1, x1 + 1, y1 + 1, OUT)
    forme.rett(im, x0, y0, x1, y1, FULIGGINE[0])
    forme.rett(im, x0, y1 - 2, x1, y1, FULIGGINE[1])  # ash on the floor
    _sbarre(im)
    # Round rivets in the corners and dark vents along the sides.
    for cx, cy in ((6, 6), (L - 6, 6), (6, L - 6), (L - 6, L - 6)):
        _rivetto(im, cx, cy)
    for x0v in (3, L - 6):
        for y in range(20, 45, 4):
            forme.rett(im, x0v, y, x0v + 2, y + 1, OUT)
    # Hopper slot on the North edge.
    forme.pannello(im, 24, 2, 39, 8, METALLO)
    forme.rett(im, 26, 4, 37, 6, NASTRO[0])
    return im


def _bocca_accesa(k):
    """Glow frame k: flames in the mouth and the side vents lit (transparent elsewhere)."""
    im = forme.nuova(L, L)
    x0, y0, x1, y1 = BOCCA
    for x in range(x0, x1 + 1):
        # Flame tongues rise from the floor; their heights shift every frame.
        h = 9 + 4 * math.sin(x * 0.85 + k * math.pi / 2) + 3 * math.sin(x * 0.37 - k * math.pi / 2 * 3)
        for y in range(y0, y1 + 1):
            alto = y1 - y  # height above the floor
            if alto > h + 4:
                c = BAGLIORE[0]
            elif alto > h:
                c = BAGLIORE[1]
            elif alto > h * 0.55:
                c = BAGLIORE[2]
            elif alto > h * 0.2:
                c = BAGLIORE[3]
            else:
                c = BAGLIORE[4]
            forme.pixel(im, x, y, c)
    _sbarre(im)
    for x0v in (3, L - 6):
        for i, y in enumerate(range(20, 45, 4)):
            c = BAGLIORE[2] if (i + k) % 2 else BAGLIORE[3]
            forme.rett(im, x0v, y, x0v + 2, y + 1, c)
    return im


sprite("smelter-base")(_base_forno)
sprite("smelter-glow-anim")(lambda: forme.striscia([_bocca_accesa(k) for k in range(FORNO_FOTOGRAMMI)]))
sprite("smelter")(lambda: forme.componi(_base_forno(), _bocca_accesa(0)))


# ---------------------------------------------------------------- assembler

BRACCIO_APERTO = 0.28  # AssemblerVisual: arm offset from the centre, fraction of the block side


def _base_assemblatore():
    im = forme.nuova(L, L)
    forme.sagoma(im, forme.smussato(0, 0, L - 1, L - 1, 3), FERRO)
    # Rails the two carriages run on, North and South.
    for y0 in (8, L - 13):
        forme.rett(im, 3, y0, L - 4, y0 + 4, OUT)
        forme.rett(im, 4, y0 + 1, L - 5, y0 + 1, METALLO[4])
        forme.rett(im, 4, y0 + 2, L - 5, y0 + 2, METALLO[2])
        forme.rett(im, 4, y0 + 3, L - 5, y0 + 3, METALLO[1])
        for x in range(8, L - 4, 8):
            forme.pixel(im, x, y0 + 2, OUT)  # rail joints
    # Work bed between the rails.
    forme.rett(im, 6, 15, L - 7, L - 16, OUT)
    forme.rett(im, 7, 16, L - 8, L - 17, NASTRO[1])
    forme.rett(im, 7, 16, L - 8, 16, NASTRO[0])
    forme.rett(im, 7, 16, 7, L - 17, NASTRO[0])
    for x in range(10, L - 8, 4):
        forme.rett(im, x, 18, x, L - 19, NASTRO[2])  # bed slats
    # Target marks where the arms meet.
    for sx, sy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
        cx, cy = int(C) + (sx * 6 if sx > 0 else sx * 6 - 1), int(C) + (sy * 6 if sy > 0 else sy * 6 - 1)
        forme.pixel(im, cx, cy, ACCENTO[3])
        forme.pixel(im, cx - sx, cy, ACCENTO[2])
        forme.pixel(im, cx, cy - sy, ACCENTO[2])
    # Corner brackets with a bolt.
    for bx, by in ((2, 2), (L - 5, 2), (2, L - 5), (L - 5, L - 5)):
        forme.pannello(im, bx - 1, by - 1, bx + 2, by + 2, FERRO)
        forme.bullone(im, bx, by, METALLO)
    return forme.basamento(im, FERRO)


def _braccio():
    """Left arm, drawn around the centre of the frame: a carriage across both rails (x 29-34)
    with a copper press head facing the centre (x 35-37)."""
    im = forme.nuova(L, L)
    forme.pannello(im, 29, 7, 34, L - 8, METALLO)
    for y0 in (8, L - 13):
        forme.rett(im, 30, y0 + 1, 33, y0 + 3, METALLO[1])  # wheels on the rail
        forme.pixel(im, 30, y0 + 1, METALLO[3])
    forme.rett(im, 31, 18, 32, L - 19, METALLO[2])  # spine
    forme.pannello(im, 34, 25, 38, 38, RAME)
    forme.rett(im, 38, 27, 38, 36, ACCENTO[3])  # press face
    return im


def _assemblatore():
    im = _base_assemblatore()
    off = round(BRACCIO_APERTO * L)
    sinistro = forme.nuova(L, L)
    sinistro.alpha_composite(_braccio(), (-off, 0))
    destro = forme.nuova(L, L)
    destro.alpha_composite(_braccio().transpose(Image.Transpose.FLIP_LEFT_RIGHT), (off, 0))
    return forme.componi(im, sinistro, destro)


sprite("assembler-base")(_base_assemblatore)
sprite("assembler-arm")(_braccio)
sprite("assembler")(_assemblatore)


# ---------------------------------------------------------------- extractor

@sprite("extractor")
def extractor():
    """1x1 extractor: lead-coloured octagon with a dark intake slot on every edge (no arrows) and a square
    frame in the centre around an empty 14x14 slot (rows / columns 9-22) for the filter icon the client draws."""
    im = forme.nuova(T, T)
    forme.sagoma(im, forme.smussato(0, 0, T - 1, T - 1, 7), PIOMBO)
    for (x0, y0, x1, y1) in ((12, 1, 19, 4), (12, T - 5, 19, T - 2), (1, 12, 4, 19), (T - 5, 12, T - 2, 19)):
        forme.rett(im, x0, y0, x1, y1, OUT)
        forme.rett(im, x0 + 1, y0 + 1, x1 - 1, y1 - 1, NASTRO[1])
    forme.rett(im, 7, 7, 24, 24, OUT)
    forme.rett(im, 8, 8, 23, 23, METALLO[3])
    forme.rett(im, 8, 8, 23, 8, METALLO[5])
    forme.rett(im, 8, 8, 8, 23, METALLO[5])
    forme.rett(im, 23, 8, 23, 23, METALLO[1])
    forme.rett(im, 8, 23, 23, 23, METALLO[1])
    forme.rett(im, 9, 9, 22, 22, OUT)
    forme.rett(im, 10, 10, 21, 21, NASTRO[0])
    return forme.basamento(im, PIOMBO)


# ---------------------------------------------------------------- machines of the «Ricerca allargata»

def _angoli(im, rampa):
    """Corner brackets with a bolt, as on the assembler."""
    for bx, by in ((2, 2), (L - 5, 2), (2, L - 5), (L - 5, L - 5)):
        forme.pannello(im, bx - 1, by - 1, bx + 2, by + 2, rampa)
        forme.bullone(im, bx, by, METALLO)


BOCCHE_AVANZATE = ((12, 22, 27, 41), (36, 22, 51, 41))  # the two mouths of the advanced smelter (inclusive)


def _base_fonderia():
    """Advanced smelter: a titanium casing with a red band and two dark mouths behind grates."""
    im = forme.nuova(L, L)
    forme.sagoma(im, forme.smussato(0, 0, L - 1, L - 1, 4), TITANIO)
    forme.pannello(im, 7, 15, L - 8, L - 16, ROSSO[:4])
    for x0, y0, x1, y1 in BOCCHE_AVANZATE:
        forme.rett(im, x0 - 1, y0 - 1, x1 + 1, y1 + 1, OUT)
        forme.rett(im, x0, y0, x1, y1, FULIGGINE[0])
        forme.rett(im, x0, y1 - 2, x1, y1, FULIGGINE[1])
    # Power socket on the North edge instead of the forno's coal hopper.
    forme.pannello(im, 26, 3, 37, 10, METALLO)
    forme.rett(im, 29, 5, 30, 8, OUT)
    forme.rett(im, 33, 5, 34, 8, OUT)
    _angoli(im, TITANIO)
    return forme.basamento(im, TITANIO)


def _bocche_accese():
    """Both mouths lit, grates over them (transparent elsewhere)."""
    im = forme.nuova(L, L)
    for x0, y0, x1, y1 in BOCCHE_AVANZATE:
        for y in range(y0, y1 + 1):
            alto = y1 - y
            c = BAGLIORE[1] if alto > 13 else BAGLIORE[2] if alto > 8 else BAGLIORE[3] if alto > 3 else BAGLIORE[4]
            for x in range(x0, x1 + 1):
                forme.pixel(im, x, y, c)
        for x in range(x0 + 3, x1, 5):
            forme.rett(im, x, y0, x, y1, OUT)
    return im


sprite("smelter-advanced-base")(_base_fonderia)
sprite("smelter-advanced")(lambda: forme.componi(_base_fonderia(), _bocche_accese()))


def _base_pressa():
    """Graphite press: two steel pillars and a crosshead over a graphite ram pressing a coal bed."""
    im = forme.nuova(L, L)
    forme.sagoma(im, forme.smussato(0, 0, L - 1, L - 1, 3), FERRO)
    # Coal bed in the middle.
    forme.rett(im, 14, 36, L - 15, L - 10, OUT)
    forme.rett(im, 15, 37, L - 16, L - 11, CARBONE[1])
    for x in range(17, L - 16, 4):
        forme.pixel(im, x, 39, CARBONE[3])
        forme.pixel(im, x + 2, 42, CARBONE[2])
    # Pillars and crosshead.
    for x0 in (6, L - 13):
        forme.pannello(im, x0, 6, x0 + 6, L - 7, METALLO)
    forme.pannello(im, 6, 6, L - 7, 14, METALLO)
    # The ram: a graphite block under the crosshead.
    forme.pannello(im, 18, 15, L - 19, 33, GRAFITE)
    forme.rett(im, 21, 18, L - 22, 18, GRAFITE[4])
    forme.rett(im, 30, 15, 33, 33, GRAFITE[1])
    _angoli(im, FERRO)
    return forme.basamento(im, FERRO)


sprite("graphite-press-base")(_base_pressa)
sprite("graphite-press")(_base_pressa)


def _base_separatore():
    """Separator: a sand drum on a lead plate, with four ore chutes in the colours of what comes out."""
    im = forme.nuova(L, L)
    forme.sagoma(im, forme.smussato(0, 0, L - 1, L - 1, 3), PIOMBO)
    scivoli = ((4, 4, FERRO), (L - 13, 4, RAME), (4, L - 13, PIOMBO), (L - 13, L - 13, TITANIO))
    for x0, y0, rampa in scivoli:
        forme.pannello(im, x0, y0, x0 + 8, y0 + 8, METALLO)
        forme.rett(im, x0 + 2, y0 + 2, x0 + 6, y0 + 6, rampa[2])
        forme.rett(im, x0 + 2, y0 + 2, x0 + 6, y0 + 2, rampa[3])
    forme.cerchio(im, C, C, 18, METALLO)
    forme.cerchio(im, C, C, 14, SABBIA_GRANI)
    # Sieve slots across the drum.
    for y in range(22, 43, 5):
        forme.rett(im, 24, y, 39, y, SABBIA_GRANI[0])
    forme.cerchio(im, C, C, 4, METALLO)
    return forme.basamento(im, PIOMBO)


sprite("separator-base")(_base_separatore)
sprite("separator")(_base_separatore)


def _base_plastificatore():
    """Plasticizer: a magenta vat in a steel frame, a coal hopper on the North edge and two pipes."""
    im = forme.nuova(L, L)
    forme.sagoma(im, forme.smussato(0, 0, L - 1, L - 1, 3), METALLO)
    forme.pannello(im, 23, 2, 40, 11, METALLO)
    forme.rett(im, 25, 4, 38, 9, CARBONE[1])
    forme.cerchio(im, C, C + 4, 17, PLASTICA)
    forme.cerchio(im, C, C + 4, 11, PLASTICA, pieno=False)
    forme.bagliore(im, C - 4, C, 5, PLASTICA)
    forme.tubo(im, 4, 40, 12, 46, METALLO)
    forme.tubo(im, L - 13, 40, L - 5, 46, METALLO)
    _angoli(im, METALLO)
    return forme.basamento(im, METALLO)


sprite("plasticizer-base")(_base_plastificatore)
sprite("plasticizer")(_base_plastificatore)
