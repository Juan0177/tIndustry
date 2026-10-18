"""Production sprites: miner (basic and advanced), smelter, assembler, extractor.

Every block is a still base plus its moving parts, each part a full-size layer so the client
stacks them centred on the block:
- miner: `-base`, `-drill-anim` (8 frames of the drill bit turning), advanced `-lamp` (lit with power);
- smelter: `-base`, `-glow-anim` (4 frames of the glowing mouth);
- assembler: `-base`, `-arm` (left arm, mirrored by the client for the right one, moved by code).
`<id>.png` is the composed icon (palette, research, plans, construction sites).
The bases differ on purpose: chamfered grey plate (miner), red-trimmed plate with a lamp
(advanced miner), sooty brick-red furnace with round rivets (smelter), blue steel bed on rails
(assembler), lead-coloured octagon (extractor).
"""
import math

from PIL import Image

import forme
from blocchi import sprite
from palette import ACCENTO, BAGLIORE, FERRO, FULIGGINE, METALLO, NASTRO, OUT, PIOMBO, RAME, ROSSO, TITANIO

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


def trivella(angolo, cono, denti, collare):
    """Conical drill bit seen from above, turned by `angolo`: a cone with helical flutes that
    wind into the tip, a ring of square cutting teeth and a collar at the base of the cone."""
    im = forme.nuova(L, L)
    pieni = {}
    r_denti = PUNTA_R + 3
    for y in range(L):
        for x in range(L):
            dx, dy = x + 0.5 - C, y + 0.5 - C
            d = math.hypot(dx, dy)
            a = math.atan2(dy, dx) - angolo
            if d <= PUNTA_R:
                if d < 1.5:
                    pieni[(x, y)] = cono[-1]  # the tip
                    continue
                # Flute: an Archimedean spiral; the edge after it catches the light.
                fase = ((a + d * 0.32) % (2 * math.pi / SOLCHI)) / (2 * math.pi / SOLCHI)
                lit = forme._luce(dx, dy)
                k = 2 + (1 if lit > 0.3 else -1 if lit < -0.3 else 0)
                if d < 4:
                    k += 1  # the tip is lighter
                if fase < 0.24:
                    c = OUT if d > 3 else cono[0]
                elif fase < 0.38:
                    c = cono[min(len(cono) - 1, k + 1)]
                else:
                    c = cono[max(0, min(len(cono) - 1, k))]
                if PUNTA_R - 1.4 < d:
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
    return im


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
    return im


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
    """1x1 extractor: lead-coloured octagon with an intake on every edge; the empty dark
    well in the centre (8 px across) holds the filter dot drawn by the client."""
    im = forme.nuova(T, T)
    forme.sagoma(im, forme.smussato(0, 0, T - 1, T - 1, 7), PIOMBO)
    # Intakes: a dark slot with an inward chevron on each edge.
    for (x0, y0, x1, y1) in ((12, 1, 19, 4), (12, T - 5, 19, T - 2), (1, 12, 4, 19), (T - 5, 12, T - 2, 19)):
        forme.rett(im, x0, y0, x1, y1, OUT)
        forme.rett(im, x0 + 1, y0 + 1, x1 - 1, y1 - 1, NASTRO[1])
    for t in range(3):
        for c in (ACCENTO[3],):
            forme.pixel(im, 15 - t, 2 + t, c); forme.pixel(im, 16 + t, 2 + t, c)
            forme.pixel(im, 15 - t, T - 3 - t, c); forme.pixel(im, 16 + t, T - 3 - t, c)
            forme.pixel(im, 2 + t, 15 - t, c); forme.pixel(im, 2 + t, 16 + t, c)
            forme.pixel(im, T - 3 - t, 15 - t, c); forme.pixel(im, T - 3 - t, 16 + t, c)
    # Ring around the well, then the well itself (radius 4).
    _anello(im, T / 2, T / 2, 4, 8, METALLO)
    for y in range(T):
        for x in range(T):
            if math.hypot(x + 0.5 - T / 2, y + 0.5 - T / 2) <= 4:
                forme.pixel(im, x, y, NASTRO[0])
    return im
