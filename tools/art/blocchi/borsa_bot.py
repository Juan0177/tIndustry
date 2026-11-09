"""The stock-exchange screen's bot, its briefcase and the confetti of the jackpot sequence (spec 2b).

The bot is a small flying screen in 2.5D (seen slightly from above: a lit top face, the front face with the dark
glass and two yellow LED eyes, a soft drop shadow on the ground). It wears the screen's colours: the steel frame
(`FERRO`), the dark glass (`NASTRO[1]`) and a tiny yellow and black LED strip like the one under the screen
(`ENERGIA`). It faces the viewer; when it flies it leans to the East (speed lines on the West side), so the client
mirrors it to fly West. Strips of square frames, 32 px (one cell, the side of `drone-3`), read with
`Sprites.Frames("<id>")` from `<id>-anim.png`:
- `market-bot-idle` (2): hovering, a gentle 1 px bob over a still shadow;
- `market-bot-fly` (2): higher, smaller shadow, longer flickering flame and speed lines;
- `market-bot-surprised` (1): a hop with wide eyes, an open mouth, raised arms and a pixel «!» above the head;
- `market-bot-carry` (2): flying with the briefcase held in front in both arms;
- `market-bot-throw` (2): the briefcase above the head, then a hop with the arms up and the hands empty.
`market-briefcase` (12x12) is the briefcase alone, drawn by the client as it flies into the screen.
`market-confetti` (6 frames of 8 px) are six confetti pieces, one per frame, in the LED colours of the Normale
variant (blue, purple, yellow) and white; the white one (frame 3) can be tinted with any `UiColors` LED colour.
"""
import forme
from blocchi import sprite
from palette import ACCENTO, BAGLIORE, CARTA, ENERGIA, FERRO, LED_BLU, LED_GIALLO, LED_VIOLA, NASTRO, OUT

LATO = 32  # bot frame side
VALIGETTA = 12  # briefcase side
CORIANDOLO = 8  # confetti frame side
OMBRA_ALFA = 80


def _ombra(im, rx, alfa):
    # Soft drop shadow on the ground: an ellipse with a fainter rim.
    cx, cy = 15.5, 29
    for y in range(26, 32):
        for x in range(LATO):
            d = ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / 1.8) ** 2
            if d <= 1:
                forme.pixel(im, x, y, (0, 0, 0, alfa if d <= 0.55 else alfa // 2))


def _braccio(im, x, y0, y1):
    # A stubby arm: a 3 px wide steel bar from row y0 to y1 (outlined, lit on the left).
    forme.pannello(im, x, min(y0, y1), x + 2, max(y0, y1), FERRO)


def _corpo(im, dy=0, occhi="normali", fiamma=1, braccia="giu", antenna=True):
    """The bot with its body moved by dy rows. occhi: normali | sorpresi; fiamma: flame length in rows;
    braccia: giu (at the sides) | su (raised) | alto (holding something above the head) | avanti (holding
    something in front); antenna: False when something above the head hides it."""
    # Arms first, so the body covers their roots.
    if braccia == "giu":
        _braccio(im, 5, 16 + dy, 21 + dy)
        _braccio(im, 24, 16 + dy, 21 + dy)
    elif braccia == "su":
        _braccio(im, 5, 9 + dy, 17 + dy)
        _braccio(im, 24, 9 + dy, 17 + dy)
    elif braccia == "alto":
        _braccio(im, 8, 6 + dy, 14 + dy)
        _braccio(im, 21, 6 + dy, 14 + dy)
    elif braccia == "avanti":
        _braccio(im, 7, 17 + dy, 24 + dy)
        _braccio(im, 22, 17 + dy, 24 + dy)
    # Flame under the nozzle (drawn before the nozzle so the outline stays on top).
    for k in range(fiamma):
        y = 26 + dy + k
        if k == 0:
            forme.rett(im, 14, y, 17, y, BAGLIORE[3])
            forme.rett(im, 15, y, 16, y, BAGLIORE[4])
        else:
            forme.rett(im, 15, y, 16, y, BAGLIORE[2] if k == 1 else BAGLIORE[1])
    # Nozzle.
    forme.rett(im, 13, 25 + dy, 18, 25 + dy, OUT)
    forme.rett(im, 14, 25 + dy, 17, 25 + dy, NASTRO[2])
    # Top face (2.5D): a trapezoid, narrower at the back, in the light tones of the frame.
    forme.rett(im, 10, 10 + dy, 21, 10 + dy, OUT)
    forme.rett(im, 9, 11 + dy, 22, 11 + dy, OUT)
    forme.rett(im, 10, 11 + dy, 21, 11 + dy, FERRO[4])
    forme.rett(im, 8, 12 + dy, 23, 12 + dy, OUT)
    forme.rett(im, 9, 12 + dy, 22, 12 + dy, FERRO[3])
    # Front face: the screen's frame, lit on the left and shaded on the right and bottom.
    forme.pannello(im, 8, 13 + dy, 23, 24 + dy, FERRO)
    forme.rett(im, 9, 13 + dy, 22, 13 + dy, FERRO[3])  # crease between top and front faces
    # Glass with the reflection of the screen in its top-left corner.
    forme.rett(im, 11, 15 + dy, 20, 20 + dy, OUT)
    forme.rett(im, 12, 16 + dy, 19, 19 + dy, NASTRO[1])
    forme.pixel(im, 12, 16 + dy, NASTRO[3])
    if occhi == "sorpresi":
        for ex in (13, 17):
            forme.rett(im, ex, 16 + dy, ex + 1, 18 + dy, ENERGIA[4])
        forme.rett(im, 15, 19 + dy, 16, 19 + dy, ENERGIA[2])  # open mouth
    else:
        for ex in (13, 17):
            forme.rett(im, ex, 17 + dy, ex + 1, 18 + dy, ENERGIA[3])
            forme.pixel(im, ex, 17 + dy, ENERGIA[4])
    # The LED strip under the glass: yellow and black bands as under the screen.
    for x in range(10, 22):
        forme.pixel(im, x, 22 + dy, ENERGIA[2] if x // 2 % 2 == 0 else NASTRO[0])
    if not antenna:
        return
    # Antenna on the left of the top face, with a yellow lamp.
    forme.rett(im, 11, 8 + dy, 11, 9 + dy, OUT)
    forme.rett(im, 9, 4 + dy, 12, 7 + dy, OUT)
    forme.rett(im, 10, 5 + dy, 11, 6 + dy, ENERGIA[3])
    forme.pixel(im, 10, 5 + dy, ENERGIA[4])


def _scie(im, dy, lunghe):
    # Speed lines on the West side (the bot flies East).
    for y, n in ((14 + dy, 4 if lunghe else 3), (19 + dy, 3 if lunghe else 4)):
        forme.rett(im, 4 - n, y, 3, y, FERRO[4] + (160,))


def _valigetta(im, x0, y0):
    # The briefcase, 12x10 from (x0, y0): a handle on top, a leather body with a seam and two gold clasps.
    forme.rett(im, x0 + 4, y0, x0 + 7, y0, OUT)
    forme.pixel(im, x0 + 3, y0 + 1, OUT)
    forme.pixel(im, x0 + 8, y0 + 1, OUT)
    forme.pannello(im, x0, y0 + 2, x0 + 11, y0 + 10, ACCENTO)
    forme.rett(im, x0 + 1, y0 + 5, x0 + 10, y0 + 5, ACCENTO[0])
    for cx in (x0 + 3, x0 + 7):
        forme.rett(im, cx, y0 + 4, cx + 1, y0 + 5, ENERGIA[3])
        forme.pixel(im, cx, y0 + 4, ENERGIA[4])


def _esclamativo(im):
    # A pixel «!» above the head: a bar and a dot, white with a dark outline.
    forme.rett(im, 14, 0, 17, 5, OUT)
    forme.rett(im, 15, 1, 16, 4, CARTA[4])
    forme.rett(im, 14, 5, 17, 8, OUT)
    forme.rett(im, 15, 6, 16, 7, CARTA[4])
    forme.rett(im, 15, 5, 16, 5, OUT)


def _fotogramma(ombra_rx=6, ombra_alfa=OMBRA_ALFA):
    im = forme.nuova(LATO, LATO)
    _ombra(im, ombra_rx, ombra_alfa)
    return im


@sprite("market-bot-idle-anim")
def market_bot_idle():
    fotogrammi = []
    for dy in (0, 1):
        im = _fotogramma()
        _corpo(im, dy, fiamma=1)
        fotogrammi.append(im)
    return forme.striscia(fotogrammi)


@sprite("market-bot-fly-anim")
def market_bot_fly():
    fotogrammi = []
    for k in range(2):
        im = _fotogramma(4, OMBRA_ALFA * 2 // 3)
        _corpo(im, -2, fiamma=3 if k == 0 else 2)
        _scie(im, -2, k == 0)
        fotogrammi.append(im)
    return forme.striscia(fotogrammi)


@sprite("market-bot-surprised-anim")
def market_bot_surprised():
    im = _fotogramma(5)
    _corpo(im, 0, occhi="sorpresi", fiamma=2, braccia="su")
    _esclamativo(im)
    return forme.striscia([im])


@sprite("market-bot-carry-anim")
def market_bot_carry():
    fotogrammi = []
    for k in range(2):
        dy = -2 - k
        im = _fotogramma(4, OMBRA_ALFA * 2 // 3)
        _corpo(im, dy, fiamma=2 if k == 0 else 3, braccia="avanti")
        _valigetta(im, 10, 20 + dy)
        _scie(im, dy, k == 0)
        fotogrammi.append(im)
    return forme.striscia(fotogrammi)


@sprite("market-bot-throw-anim")
def market_bot_throw():
    # Wind-up: the briefcase above the head in raised arms; then the throw: a hop with the arms up, hands empty.
    alto = _fotogramma(5)
    _corpo(alto, 2, fiamma=1, braccia="alto", antenna=False)
    _valigetta(alto, 10, 0)
    lancio = _fotogramma(5)
    _corpo(lancio, -1, fiamma=2, braccia="su")
    return forme.striscia([alto, lancio])


@sprite("market-briefcase")
def market_briefcase():
    im = forme.nuova(VALIGETTA, VALIGETTA)
    _valigetta(im, 0, 0)
    return im


# Confetti pieces, one per frame: (ramp, lit pixels, shaded pixels) in an 8x8 cell; few pixels each.
PEZZI = (
    (LED_BLU, ((2, 3), (3, 3), (4, 3)), ((2, 4), (3, 4), (4, 4))),  # flat strip
    (LED_VIOLA, ((2, 2), (3, 3), (4, 4)), ((3, 2), (4, 3), (5, 4))),  # diagonal ribbon
    (LED_GIALLO, ((3, 2), (3, 3), (3, 4)), ((4, 2), (4, 3), (4, 4))),  # standing strip
    (CARTA, ((3, 3), (4, 3)), ((3, 4), (4, 4))),  # white square
    (LED_GIALLO, ((5, 2), (4, 3), (3, 4)), ((5, 3), (4, 4), (3, 5))),  # diagonal the other way
    (LED_BLU, ((3, 2), (4, 3)), ((3, 3), (4, 4), (5, 4))),  # twisted curl
)


@sprite("market-confetti-anim")
def market_confetti():
    fotogrammi = []
    for rampa, chiari, scuri in PEZZI:
        im = forme.nuova(CORIANDOLO, CORIANDOLO)
        for x, y in chiari:
            forme.pixel(im, x, y, rampa[2])
        for x, y in scuri:
            forme.pixel(im, x, y, rampa[1])
        fotogrammi.append(im)
    return forme.striscia(fotogrammi)
