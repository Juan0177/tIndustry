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
