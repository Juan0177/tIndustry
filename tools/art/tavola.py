"""Review sheet: every sprite old next to new, at 1x and 2x, on a light and a dark background.

One row per sprite:  name | light: old 1x, old 2x, new 1x, new 2x | dark: same four.
A missing old sprite (a brand new name) leaves an empty, crossed slot.
"""
import os

from PIL import Image, ImageDraw, ImageFont

CHIARO = (214, 210, 200, 255)
SCURO = (36, 38, 42, 255)
FONDO = (60, 60, 64, 255)
TESTO = (235, 235, 235, 255)
MARGINE = 6
ETICHETTA = 170


def _apri(cartella, nome):
    path = os.path.join(cartella, nome + ".png")
    if not os.path.exists(path):
        return None
    with Image.open(path) as im:
        return im.convert("RGBA")


def _riquadro(im, fondo, k):
    """Sprite scaled by k (nearest) on a solid background; a crossed box when missing."""
    if im is None:
        box = Image.new("RGBA", (32 * k, 32 * k), fondo)
        d = ImageDraw.Draw(box)
        d.line((0, 0, box.width - 1, box.height - 1), fill=(200, 60, 60, 255))
        d.line((0, box.height - 1, box.width - 1, 0), fill=(200, 60, 60, 255))
        return box
    box = Image.new("RGBA", (im.width * k, im.height * k), fondo)
    box.alpha_composite(im.resize(box.size, Image.NEAREST))
    return box


def tavola(nomi, vecchi_dir, nuovi_dir):
    righe = []
    for nome in nomi:
        vecchio, nuovo = _apri(vecchi_dir, nome), _apri(nuovi_dir, nome)
        riquadri = [_riquadro(im, fondo, k)
                    for fondo in (CHIARO, SCURO)
                    for im in (vecchio, nuovo)
                    for k in (1, 2)]
        righe.append((nome, riquadri))

    larghezza = max(ETICHETTA + sum(r.width + MARGINE for r in rq) for _, rq in righe) + MARGINE
    altezza = sum(max(r.height for r in rq) + MARGINE for _, rq in righe) + MARGINE
    out = Image.new("RGBA", (larghezza, altezza), FONDO)
    disegno = ImageDraw.Draw(out)
    font = ImageFont.load_default()
    y = MARGINE
    for nome, riquadri in righe:
        disegno.text((MARGINE, y), nome, fill=TESTO, font=font)
        x = ETICHETTA
        for r in riquadri:
            out.alpha_composite(r, (x, y))
            x += r.width + MARGINE
        y += max(r.height for r in riquadri) + MARGINE
    return out
