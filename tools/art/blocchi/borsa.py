"""Stock-exchange screen (2x2): a wide screen with no legs, seen from above.

A dark glass panel in a steel frame, with the yellow and black LED strip along the bottom edge of the
frame (spec 2a «Schermo di borsa»). The client draws the chart, the price and the lit LEDs over it.
"""
import forme
from blocchi import sprite
from palette import ENERGIA, FERRO, METALLO, NASTRO, OUT

T = 32
LATO = 2 * T


@sprite("market-screen")
def market_screen():
    im = forme.nuova(LATO, LATO)
    # Steel frame and the dark glass of the screen.
    forme.pannello(im, 1, 6, LATO - 2, LATO - 7, FERRO)
    forme.rett(im, 5, 10, LATO - 6, LATO - 19, OUT)
    forme.rett(im, 6, 11, LATO - 7, LATO - 20, NASTRO[1])
    # A faint grid line and a flat price line on the glass, so the block reads as a screen when it is off.
    for x in range(8, LATO - 8, 2):
        forme.pixel(im, x, 26, NASTRO[3])
    forme.rett(im, 8, 30, LATO - 9, 30, METALLO[2])
    # LED strip: yellow and black blocks along the bottom of the frame.
    for i, x in enumerate(range(6, LATO - 6, 4)):
        colore = ENERGIA[3] if i % 2 == 0 else NASTRO[0]
        forme.rett(im, x, LATO - 15, min(x + 3, LATO - 7), LATO - 12, colore)
    forme.rett(im, 5, LATO - 16, LATO - 6, LATO - 16, OUT)
    forme.rett(im, 5, LATO - 11, LATO - 6, LATO - 11, OUT)
    return forme.basamento(im, FERRO[:5])


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
