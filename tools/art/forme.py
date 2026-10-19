"""Shared pixel-art building blocks. Light always comes from the top-left.

Coordinates are pixels, rectangles are inclusive (x0, y0, x1, y1). Ramps are
palette lists from dark to light with 4-6 tones; every shape picks its tones
relative to the ramp length, so any ramp works with any shape. Drawing outside
the image is silently clipped. No randomness: same inputs, same pixels.
"""
import math

from PIL import Image

from palette import OUT

TRASPARENTE = (0, 0, 0, 0)


def _toni(rampa):
    """(shadow, mid, light, highlight) tones of a ramp."""
    n = len(rampa)
    m = (n - 1) // 2
    return rampa[max(0, m - 1)], rampa[m], rampa[min(n - 1, m + 1)], rampa[min(n - 1, m + 2)]


def _rgba(c):
    return tuple(c) if len(c) == 4 else (c[0], c[1], c[2], 255)


def _dentro_cerchio(x, y, cx, cy, r):
    dx, dy = x + 0.5 - cx, y + 0.5 - cy
    return dx * dx + dy * dy <= r * r


def _luce(dx, dy):
    """-1 (facing bottom-right) .. 1 (facing the top-left light)."""
    d = math.hypot(dx, dy)
    return 0.0 if d == 0 else (-dx - dy) / (d * math.sqrt(2))


def dentro_poligono(px, py, poli):
    """Even-odd test of a point against a polygon [(x, y), ...]."""
    dentro = False
    j = len(poli) - 1
    for i in range(len(poli)):
        xi, yi = poli[i]
        xj, yj = poli[j]
        if (yi > py) != (yj > py) and px < (xj - xi) * (py - yi) / (yj - yi) + xi:
            dentro = not dentro
        j = i
    return dentro


def poligono(poli):
    """Pixels whose centre is inside the polygon [(x, y), ...]."""
    xs = [p[0] for p in poli]
    ys = [p[1] for p in poli]
    return {(x, y) for y in range(int(min(ys)), int(max(ys)) + 1)
            for x in range(int(min(xs)), int(max(xs)) + 1) if dentro_poligono(x + 0.5, y + 0.5, poli)}


def nuova(w, h):
    """Transparent RGBA canvas."""
    return Image.new("RGBA", (w, h), TRASPARENTE)


def pixel(im, x, y, c):
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), _rgba(c))


def rett(im, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            pixel(im, x, y, c)


def pannello(im, x0, y0, x1, y1, rampa, contorno=True):
    """Raised bevelled panel: lit top/left edges, shaded bottom/right edges."""
    if contorno:
        rett(im, x0, y0, x1, y1, OUT)
        x0, y0, x1, y1 = x0 + 1, y0 + 1, x1 - 1, y1 - 1
    if x1 < x0 or y1 < y0:
        return
    ombra_, medio, chiaro, luce = _toni(rampa)
    rett(im, x0, y0, x1, y1, medio)
    for x in range(x0, x1 + 1):
        pixel(im, x, y0, chiaro)
        pixel(im, x, y1, ombra_)
    for y in range(y0, y1 + 1):
        pixel(im, x0, y, chiaro)
        pixel(im, x1, y, ombra_)
    pixel(im, x0, y0, luce)
    pixel(im, x1, y0, medio)
    pixel(im, x0, y1, medio)


def sagoma(im, pieni, rampa):
    """Raised shape from a set of (x, y) pixels: outline on its border, lit top/left inner
    edge, shaded bottom/right inner edge (any outline, not only rectangles)."""
    ombra_, medio, chiaro, luce = _toni(rampa)
    bordo = {(x, y) for x, y in pieni
             if any((x + ax, y + ay) not in pieni for ax, ay in ((1, 0), (-1, 0), (0, 1), (0, -1)))}
    for x, y in sorted(pieni):
        if (x, y) in bordo:
            c = OUT
        elif (x - 1, y) in bordo or (x, y - 1) in bordo:
            c = luce if (x - 1, y - 1) in bordo and (x - 1, y) in bordo and (x, y - 1) in bordo else chiaro
        elif (x + 1, y) in bordo or (x, y + 1) in bordo:
            c = ombra_
        else:
            c = medio
        pixel(im, x, y, c)


def smussato(x0, y0, x1, y1, smusso):
    """Pixels of a rectangle with its four corners cut at 45 degrees by `smusso` px."""
    return {(x, y) for y in range(y0, y1 + 1) for x in range(x0, x1 + 1)
            if min(x - x0, x1 - x) + min(y - y0, y1 - y) >= smusso}


def arrotondato(x0, y0, x1, y1, r):
    """Pixels of a rectangle with rounded corners of radius r."""
    out = set()
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            cx = min(max(x + 0.5, x0 + r), x1 + 1 - r)
            cy = min(max(y + 0.5, y0 + r), y1 + 1 - r)
            if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= r * r:
                out.add((x, y))
    return out


def bullone(im, x, y, rampa):
    """2x2 bolt: highlight top-left, outline bottom-right."""
    _, _, chiaro, _ = _toni(rampa)
    pixel(im, x, y, rampa[-1])
    pixel(im, x + 1, y, chiaro)
    pixel(im, x, y + 1, chiaro)
    pixel(im, x + 1, y + 1, OUT)


def tubo(im, x0, y0, x1, y1, rampa):
    """Pipe along the longer side of the rectangle, shaded across its width."""
    orizzontale = (x1 - x0) >= (y1 - y0)
    a0, a1 = (y0, y1) if orizzontale else (x0, x1)
    spessore = a1 - a0
    for a in range(a0, a1 + 1):
        if a in (a0, a1):
            c = OUT
        else:
            t = (a - a0) / spessore  # 0 = lit side (top or left)
            k = len(rampa) - 1 - int(t * (len(rampa) - 1) + 0.5)
            c = rampa[max(0, min(len(rampa) - 1, k))]
        if orizzontale:
            rett(im, x0, a, x1, a, c)
        else:
            rett(im, a, y0, a, y1, c)
    # end caps
    if orizzontale:
        rett(im, x0, y0, x0, y1, OUT)
        rett(im, x1, y0, x1, y1, OUT)
    else:
        rett(im, x0, y0, x1, y0, OUT)
        rett(im, x0, y1, x1, y1, OUT)


def griglia(im, x0, y0, x1, y1, rampa, passo):
    """Vent grille: horizontal slots every `passo` pixels, lit lip above each slot."""
    ombra_, medio, chiaro, _ = _toni(rampa)
    rett(im, x0, y0, x1, y1, medio)
    for y in range(y0, y1 + 1):
        fase = (y - y0) % passo
        if fase == 0:
            c = OUT
        elif fase == 1:
            c = rampa[0]
        elif fase == passo - 1:
            c = chiaro
        else:
            continue
        rett(im, x0 + 1, y, x1 - 1, y, c)
    rett(im, x0, y0, x0, y1, ombra_)
    rett(im, x1, y0, x1, y1, chiaro)


def ingranaggio(im, cx, cy, r, denti, rampa, angolo):
    """Gear of outer radius r with `denti` teeth, rotated by `angolo` radians."""
    ombra_, medio, chiaro, luce = _toni(rampa)
    corpo = r - max(2, r // 4)
    foro = max(1.5, r / 4)
    passo = 2 * math.pi / denti
    pieni = set()
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            fase = ((math.atan2(dy, dx) - angolo) % passo) / passo
            raggio = r if 0.25 <= fase < 0.75 else corpo
            if foro <= d <= raggio:
                pieni.add((x, y))
    for x, y in sorted(pieni):
        if any((x + ax, y + ay) not in pieni for ax, ay in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            c = OUT  # outline on every edge: rim, tooth flanks and hub hole
        else:
            lit = _luce(x + 0.5 - cx, y + 0.5 - cy)
            c = luce if lit > 0.6 else chiaro if lit > 0.2 else ombra_ if lit < -0.35 else medio
        pixel(im, x, y, c)


def cerchio(im, cx, cy, r, rampa, pieno=True):
    """Shaded disc (or a 2 px ring when pieno=False) with a lit top-left rim."""
    ombra_, medio, chiaro, _ = _toni(rampa)
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            if not _dentro_cerchio(x, y, cx, cy, r):
                continue
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            bordo = d > r - 1.6
            if not pieno and not bordo:
                continue
            lit = _luce(dx, dy)
            c = (chiaro if lit > 0.35 else ombra_ if lit < -0.35 else medio) if bordo else medio
            pixel(im, x, y, c)


def bagliore(im, cx, cy, r, rampa):
    """Soft radial glow blended over the image: bright core fading to transparent."""
    strato = nuova(im.width, im.height)
    n = len(rampa)
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            if not (0 <= x < im.width and 0 <= y < im.height):
                continue
            t = math.hypot(x + 0.5 - cx, y + 0.5 - cy) / r
            if t >= 1:
                continue
            c = rampa[n - 1 - min(n - 1, int(t * n))]
            alfa = int(255 * (1 - t * t) + 0.5)
            strato.putpixel((x, y), (c[0], c[1], c[2], alfa))
    im.alpha_composite(strato)


def ombra(im, alfa):
    """Drop shadow of the opaque pixels, shifted 2 px away from the light."""
    sh = nuova(im.width, im.height)
    for y in range(im.height):
        for x in range(im.width):
            if im.getpixel((x, y))[3]:
                pixel(sh, x + 2, y + 2, (0, 0, 0, alfa))
    return sh


def striscia(fotogrammi):
    """Horizontal strip of same-size frames (an `<id>-anim` sprite)."""
    w, h = fotogrammi[0].size
    if any(f.size != (w, h) for f in fotogrammi):
        raise ValueError("i fotogrammi di una striscia devono avere la stessa misura")
    out = nuova(w * len(fotogrammi), h)
    for i, f in enumerate(fotogrammi):
        out.paste(f, (i * w, 0))
    return out


def basamento(im, rampa):
    """`im` on a rectangular plate filling its whole footprint: outlined, in the shadow tone of the
    block's own ramp (darker than its body), so a chamfered or rounded block shows no ground in its
    cut corners next to the belts it feeds, and its silhouette still reads."""
    w, h = im.size
    out = nuova(w, h)
    rett(out, 0, 0, w - 1, h - 1, OUT)
    rett(out, 1, 1, w - 2, h - 2, _toni(rampa)[0])
    out.alpha_composite(im)
    return out


def componi(*strati):
    """Alpha-composite same-size layers, first at the bottom."""
    out = nuova(*strati[0].size)
    for s in strati:
        out.alpha_composite(s)
    return out
