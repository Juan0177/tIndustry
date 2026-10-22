"""Images of the documents, made from the generated sprites (never edited by hand):

- docs/img/logo.png: the logo (godot/assets/ui/ui-logo.png) scaled x8, for a title that is just "tIndustry";
- docs/img/readme/banner.png: the README banner, with the logo, the pixel font and a miner feeding the CORE.

    python tools/art/immagini_documenti.py              # rewrite both
    python tools/art/immagini_documenti.py --controlla  # exit 1 if one differs from a fresh build (pixels)
"""
import os
import sys

from PIL import Image, ImageChops

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import carattere  # noqa: E402

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ASSETS = os.path.join(REPO, "godot", "assets")
FONDO = (11, 13, 16, 255)        # --bg of the site
AMBRA = (255, 211, 127, 255)     # --ambra
TESTO = (227, 230, 234, 255)     # --testo
TENUE = (154, 163, 178, 255)     # --tenue
RIGA_1 = "Dal giacimento al mercato"
RIGA_2 = "Logistica come Mindustry · mercato come una borsa"


def _sprite(nome, k=1):
    im = Image.open(os.path.join(ASSETS, nome + ".png")).convert("RGBA")
    return im.resize((im.width * k, im.height * k), Image.NEAREST) if k != 1 else im


def logo():
    return _sprite("ui/ui-logo", 8)


def banner():
    out = Image.new("RGBA", (1280, 320), FONDO)
    out.alpha_composite(_sprite("ui/ui-logo", 6), (64, 72))
    out.alpha_composite(carattere.scrivi(RIGA_1, 3, TESTO), (64, 190))
    out.alpha_composite(carattere.scrivi(RIGA_2, 2, TENUE), (64, 240))
    # A miner feeding the CORE through a belt with iron ore on it, at x2 like the game at its default zoom.
    k, cella = 2, 64
    y = 160 - cella
    nastro = _sprite("conveyor-basic", k)
    minerale = _sprite("iron-ore-belt", k)
    core = _sprite("core-3", k)
    x = 1280 - 48 - core.width - 2 * cella - 2 * cella
    out.alpha_composite(_sprite("miner", k), (x, y))
    for i in range(2):
        cx = x + 2 * cella + i * cella
        out.alpha_composite(nastro, (cx, y + cella // 2))
        out.alpha_composite(minerale, (cx + (cella - minerale.width) // 2, y + cella // 2 + (cella - minerale.height) // 2))
    out.alpha_composite(core, (x + 4 * cella, y + cella - core.height // 2))
    out.paste(AMBRA, (0, 316, 1280, 320))
    return out


IMMAGINI = (("docs/img/logo.png", logo), ("docs/img/readme/banner.png", banner))


def _uguali(percorso, atteso):
    if not os.path.exists(percorso):
        return False
    attuale = Image.open(percorso).convert("RGBA")
    return attuale.size == atteso.size and ImageChops.difference(attuale, atteso).getbbox() is None


def main(argv):
    diverse = []
    for rel, fai in IMMAGINI:
        percorso, im = os.path.join(REPO, rel), fai()
        if "--controlla" in argv:
            if not _uguali(percorso, im):
                diverse.append(rel)
        else:
            im.save(percorso, optimize=True)
            print(f"scritto {rel}")
    if diverse:
        print("diverse dal generatore: " + ", ".join(diverse) + " (lancia python tools/art/immagini_documenti.py)")
        return 1
    if "--controlla" in argv:
        print("immagini dei documenti: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
