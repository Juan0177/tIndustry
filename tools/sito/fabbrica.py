"""Factory pieces of the website: belts in four directions, corners, items, blocks and biome backgrounds.

Images come from the game sprites (godot/assets/) at the in-game scale (CELLA = 64 px per cell), turned
like the client turns them: straight belts are drawn heading East and rotated clockwise (South +90°);
corners follow BeltCornerTile.ResolveTransform (mirror first for counter-clockwise turns, then rotate).
"""
import base64
import io
import os
import random
import sys

from PIL import Image

SITO_DIR = os.path.dirname(os.path.abspath(__file__))
ART_DIR = os.path.join(SITO_DIR, "..", "art")
for _d in (ART_DIR, SITO_DIR):
    if _d not in sys.path:
        sys.path.insert(0, _d)

import composti  # noqa: E402
from blocchi import terreno as tr  # noqa: E402

CELLA = composti.T * composti.SCALA
VERSI = ("E", "S", "W", "N")
ROTAZIONE = {"E": 0, "S": 90, "W": 180, "N": -90}  # clockwise degrees from the East-facing art
# Corner art enters from the West and leaves South: the clockwise turns rotate it, the others mirror it first.
_CURVE_ORARIE = {("E", "S"), ("S", "W"), ("W", "N"), ("N", "E")}
_ROTAZIONE_SPECCHIATA = {"W": 0, "S": -90, "E": 180, "N": 90}
SCURO = 0.38  # biome backgrounds: RGB multiplier, so text stays readable on them


def data_url(data, mime):
    return f"data:{mime};base64," + base64.b64encode(data).decode("ascii")


def png(im):
    buf = io.BytesIO()
    im.save(buf, "PNG")
    return buf.getvalue()


def gif(frames, ms=110):
    """Animated GIF of RGBA frames; alpha below 128 becomes the transparent index (GIF has 1-bit alpha).
    ms: one duration for every frame or a list, one per frame; equal consecutive frames are merged."""
    durate = list(ms) if isinstance(ms, (list, tuple)) else [ms] * len(frames)
    uniti = []
    for f, d in zip(frames, durate):
        if uniti and uniti[-1][0].tobytes() == f.tobytes():
            uniti[-1][1] += d
        else:
            uniti.append([f, d])
    conv = []
    for f, _ in uniti:
        trasparente = f.getchannel("A").point(lambda v: 255 if v < 128 else 0)
        p = f.convert("RGB").convert("P", palette=Image.Palette.ADAPTIVE, colors=255)
        p.paste(255, mask=trasparente)
        conv.append(p)
    buf = io.BytesIO()
    conv[0].save(buf, "GIF", save_all=True, append_images=conv[1:], duration=[d for _, d in uniti], loop=0,
                 disposal=2, transparency=255)
    return buf.getvalue()


def ruota(im, gradi_orari):
    return im.rotate(-gradi_orari, expand=True)  # PIL turns counter-clockwise


def fotogrammi_nastro(immagini, tier, verso):
    return [ruota(composti._ingrandisci(f), ROTAZIONE[verso]) for f in immagini.fotogrammi(f"{tier}-anim")]


def fotogrammi_curva(immagini, tier, da, a):
    """Frames of a corner entered heading `da` and left heading `a` (BeltCornerTile.ResolveTransform)."""
    fotogrammi = [composti._ingrandisci(f) for f in immagini.fotogrammi(f"{tier}-corner-anim")]
    if (da, a) in _CURVE_ORARIE:
        return [ruota(f, ROTAZIONE[da]) for f in fotogrammi]
    if (a, da) in _CURVE_ORARIE:  # the same turn walked the other way round is counter-clockwise
        return [ruota(f.transpose(Image.Transpose.FLIP_LEFT_RIGHT), _ROTAZIONE_SPECCHIATA[da]) for f in fotogrammi]
    raise ValueError(f"nessuna curva da {da} a {a}")


def _durate_nastro(tier, n):
    """Centisecond delays rounded on the running total, so the loop keeps 1000 / (speed * 8) ms per frame."""
    fps = composti.VELOCITA_NASTRO[tier] * 8
    return [int(composti._arrotonda((k + 1) * 100 / fps) - composti._arrotonda(k * 100 / fps)) * 10
            for k in range(n)]


def gif_nastro(immagini, tier, verso):
    fotogrammi = fotogrammi_nastro(immagini, tier, verso)
    return data_url(gif(fotogrammi, _durate_nastro(tier, len(fotogrammi))), "image/gif")


def gif_curva(immagini, tier, da, a):
    fotogrammi = fotogrammi_curva(immagini, tier, da, a)
    return data_url(gif(fotogrammi, _durate_nastro(tier, len(fotogrammi))), "image/gif")


def png_sprite(immagini, nome, scala=composti.SCALA):
    im = immagini(nome)
    return data_url(png(im.resize((im.width * scala, im.height * scala), Image.NEAREST)), "image/png")


def immagine_stato(blocco, stato, immagini):
    """(bytes, mime) of one state: an animated GIF when it moves, a PNG when it is still."""
    fotogrammi = composti.fotogrammi_stato(blocco, stato, immagini)
    if len(fotogrammi) == 1:
        return png(fotogrammi[0][0]), "image/png"
    return gif([f for f, _ in fotogrammi], [d for _, d in fotogrammi]), "image/gif"


def gif_blocco(immagini, bid, stato):
    """(data URI, width px, height px) of the named state of a block of composti.BLOCCHI."""
    blocco = next(b for b in composti.BLOCCHI if b.id == bid)
    s = next(x for x in blocco.stati if x.nome == stato)
    dati, mime = immagine_stato(blocco, s, immagini)
    return data_url(dati, mime), blocco.celle[0] * CELLA, blocco.celle[1] * CELLA


def sfondo_bioma(immagini, bioma, lato_celle=8):
    """Darkened square of the biome's floors with a few decorations, at the in-game scale. It tiles: every cell
    is a seamless floor variant picked from its own seed, so the edges match like the inside."""
    suoli = [f["id"] for f in tr.TERRENO["floors"] if f["biome"] == bioma]
    t = composti.T
    out = Image.new("RGBA", (lato_celle * t, lato_celle * t))
    for y in range(lato_celle):
        for x in range(lato_celle):
            caso = random.Random(f"{bioma}:{x}:{y}")
            suolo = suoli[0] if caso.random() < 0.7 or len(suoli) == 1 else caso.choice(suoli[1:])
            out.alpha_composite(immagini(f"terreno-{suolo}-{caso.randrange(4)}"), (x * t, y * t))
            if caso.random() < 1 / 9:
                out.alpha_composite(immagini(f"deco-{bioma}-{caso.randrange(3)}"), (x * t, y * t))
    scuro = composti._ingrandisci(out).convert("RGB").point(lambda v: int(v * SCURO))
    return scuro
