"""docs/img/logo.png: the generated logo (godot/assets/ui/ui-logo.png) scaled x8 for the documents' titles.

    python tools/art/logo_documenti.py              # rewrite docs/img/logo.png
    python tools/art/logo_documenti.py --controlla  # exit 1 if it is not the x8 of the current logo
"""
import os
import sys

from PIL import Image, ImageChops

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SORGENTE = os.path.join(REPO, "godot", "assets", "ui", "ui-logo.png")
DESTINAZIONE = os.path.join(REPO, "docs", "img", "logo.png")
SCALA = 8  # same scale as the site's hero logo


def logo():
    im = Image.open(SORGENTE).convert("RGBA")
    return im.resize((im.width * SCALA, im.height * SCALA), Image.NEAREST)


def aggiornato():
    if not os.path.exists(DESTINAZIONE):
        return False
    attuale = Image.open(DESTINAZIONE).convert("RGBA")
    atteso = logo()
    return attuale.size == atteso.size and ImageChops.difference(attuale, atteso).getbbox() is None


def main(argv):
    if "--controlla" in argv:
        if aggiornato():
            print("logo dei documenti: OK")
            return 0
        print("docs/img/logo.png diverso dal logo generato: lancia python tools/art/logo_documenti.py")
        return 1
    logo().save(DESTINAZIONE, optimize=True)
    print(f"scritto {os.path.relpath(DESTINAZIONE, REPO)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
