"""Stock-exchange screen (2x2): a wide screen with no legs, seen from above, and the Finance module icons.

`market-screen-base` is the steel frame with the dark empty glass, the yellow and black LED strip under it
(diagonal hazard bands) and four dark lamps in the corners of the frame (spec 2a «Schermo di borsa»). The client
draws the item, the price, the chart and the module lines on the glass (`VETRO`), and lights the strip with
`market-screen-led` (the yellow bands in white, tinted with the LED colour) and the corners with
`market-screen-corners` (white, tinted yellow for a jackpot). `market-screen` = the base with a price line that
climbs to a spike on the glass: the icon of the palette, of the Research and of the site.
"""
import forme
from blocchi import sprite
from palette import ENERGIA, FERRO, METALLO, NASTRO, OUT

T = 32
LATO = 2 * T

# Inclusive pixel rectangles of the 64x64 sprite; MarketScreenVisual.cs (godot/scripts/world) mirrors VETRO.
VETRO = (5, 7, 58, 42)  # the dark glass the client draws on
STRISCIA = (4, 47, 59, 52)  # the LED strip
ANGOLI = ((2, 2), (59, 2), (2, 58), (59, 58))  # top-left pixel of the 3x3 corner lamps
LAMPADA = 3


def _gialla(x, y):
    """True on the yellow bands of the strip (diagonal hazard stripes 4 px wide)."""
    return (x + y) // 4 % 2 == 0


@sprite("market-screen-base")
def market_screen_base():
    im = forme.nuova(LATO, LATO)
    forme.pannello(im, 1, 1, LATO - 2, LATO - 2, FERRO)
    # Glass in its dark outline, a faint grid and a soft reflection in the top-left corner.
    x0, y0, x1, y1 = VETRO
    forme.rett(im, x0 - 1, y0 - 1, x1 + 1, y1 + 1, OUT)
    forme.rett(im, x0, y0, x1, y1, NASTRO[1])
    for y in (y0 + 12, y0 + 24):
        for x in range(x0 + 1, x1, 3):
            forme.pixel(im, x, y, NASTRO[2])
    for k in range(6):
        forme.pixel(im, x0 + 1 + k, y0 + 6 - k, NASTRO[3])
    # LED strip: yellow and black diagonal bands in a dark outline.
    sx0, sy0, sx1, sy1 = STRISCIA
    forme.rett(im, sx0 - 1, sy0 - 1, sx1 + 1, sy1 + 1, OUT)
    for y in range(sy0, sy1 + 1):
        for x in range(sx0, sx1 + 1):
            forme.pixel(im, x, y, (ENERGIA[3] if y == sy0 else ENERGIA[2]) if _gialla(x, y) else NASTRO[0])
    # Corner lamps, off: dark amber glass in a dark rim.
    for ax, ay in ANGOLI:
        forme.rett(im, ax, ay, ax + LAMPADA - 1, ay + LAMPADA - 1, NASTRO[2])
        forme.pixel(im, ax + 1, ay + 1, ENERGIA[0])
    # Bolts on the bottom of the frame.
    for bx in (10, LATO - 12):
        forme.bullone(im, bx, LATO - 8, FERRO)
    return forme.basamento(im, FERRO[:5])


@sprite("market-screen-led")
def market_screen_led():
    # The yellow bands lit, in white (the client tints them): a brighter top row like the base's highlight.
    im = forme.nuova(LATO, LATO)
    sx0, sy0, sx1, sy1 = STRISCIA
    for y in range(sy0, sy1 + 1):
        for x in range(sx0, sx1 + 1):
            if _gialla(x, y):
                forme.pixel(im, x, y, (255, 255, 255) if y == sy0 else (226, 226, 226))
    return im


@sprite("market-screen-corners")
def market_screen_corners():
    # The four corner lamps lit, in white with a brighter centre (the client tints them yellow for a jackpot).
    im = forme.nuova(LATO, LATO)
    for ax, ay in ANGOLI:
        forme.rett(im, ax, ay, ax + LAMPADA - 1, ay + LAMPADA - 1, (214, 214, 214))
        forme.pixel(im, ax + 1, ay + 1, (255, 255, 255))
    return im


@sprite("market-screen")
def market_screen():
    # The icon: the base with a price line that climbs to a spike, its tip yellow.
    im = market_screen_base()
    x0, y0, x1, y1 = VETRO
    linea = [(x0 + 3, y1 - 8), (x0 + 12, y1 - 10), (x0 + 20, y1 - 7), (x0 + 30, y1 - 12), (x0 + 38, y1 - 22),
             (x0 + 42, y0 + 5), (x0 + 46, y1 - 16), (x1 - 3, y1 - 13)]
    for (ax, ay), (bx, by) in zip(linea, linea[1:]):
        passi = max(abs(bx - ax), abs(by - ay))
        for k in range(passi + 1):
            forme.pixel(im, ax + (bx - ax) * k // passi, ay + (by - ay) * k // passi, METALLO[4])
    forme.rett(im, x0 + 41, y0 + 4, x0 + 43, y0 + 5, ENERGIA[3])
    return im


# Finance modules (spec 2a «Ramo Finanza della Ricerca»): research nodes with nothing to place, so their sprite is
# only the Research icon (one cell): a small dark screen in a steel frame with the mark of what the module shows.
def _modulo():
    im = forme.nuova(T, T)
    forme.pannello(im, 2, 4, T - 3, T - 5, FERRO)
    forme.rett(im, 5, 7, T - 6, T - 8, OUT)
    forme.rett(im, 6, 8, T - 7, T - 9, NASTRO[1])
    # The yellow LED in the corner of the frame, as on the screen's strip.
    forme.rett(im, T - 7, T - 7, T - 6, T - 6, ENERGIA[3])
    return im


@sprite("finance-forecast")
def finance_forecast():
    # A price line that climbs to a spike, with a yellow tip.
    im = _modulo()
    linea = [(7, 20), (9, 19), (11, 20), (13, 18), (15, 19), (17, 16), (19, 11), (21, 16), (23, 18)]
    for (x0, y0), (x1, y1) in zip(linea, linea[1:]):
        passi = max(abs(x1 - x0), abs(y1 - y0))
        for k in range(passi + 1):
            forme.pixel(im, x0 + (x1 - x0) * k // passi, y0 + (y1 - y0) * k // passi, METALLO[4])
    forme.rett(im, 18, 10, 20, 10, ENERGIA[3])
    return im


@sprite("finance-percent")
def finance_percent():
    # A percent sign: two small rings and the slash.
    im = _modulo()
    for cx, cy in ((11, 12), (20, 19)):
        forme.rett(im, cx - 1, cy - 1, cx + 1, cy + 1, METALLO[4])
        forme.pixel(im, cx, cy, NASTRO[1])
    for k in range(10):
        forme.pixel(im, 20 - k, 11 + k, ENERGIA[3])
        forme.pixel(im, 21 - k, 11 + k, ENERGIA[3])
    return im


@sprite("finance-odds")
def finance_odds():
    # A die showing five: the odds of the jackpot.
    im = _modulo()
    forme.rett(im, 10, 10, 21, 20, METALLO[4])
    forme.rett(im, 11, 11, 20, 19, METALLO[5])
    for x, y in ((12, 12), (18, 12), (15, 15), (12, 18), (18, 18)):
        forme.rett(im, x, y, x + 1, y + 1, OUT)
    return im
