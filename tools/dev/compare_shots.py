#!/usr/bin/env python3
"""Usage: compare_shots.py <dirA> <dirB>
Compares same-named PNGs; exit 0 if identical outside the masked rectangles.
Reports name + bounding box of the difference otherwise (exit 1)."""
import sys
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw

# Global mask, painted black on both images before diffing.
MASKS = [
    (1180, 0, 1280, 30),  # FPS counter
]

# Extra masks per screenshot, only where timing noise was observed by diffing identical runs. A real change
# inside these rectangles would be MISSED. Playtests stop every visual clock (drills, belts, bridge arrows,
# the construction shader, the drone's hover and beam) and the colour captures hold the simulation, so what is
# left is simulation state that depends on how many ticks ran before a capture. Measured on three ui and
# tutorial runs in Italian and three in English (G4 final wave): union of the differing pixels + 3 px.
#
# Items on the belt into the CORE (where the seed line's items happen to be); the belt and the CORE edge
# under these few pixels go unseen.
ITEMS_CORE = (567, 339, 616, 354)
# HUD power buffer value ("N/24") at UI 100%: its digits go unseen.
POWER_HUD = (221, 63, 255, 83)
# End of the build queue bar's fill (site progress when the simulation was held): the bar end goes unseen.
QUEUE_BAR = (126, 203, 135, 217)
# Colour captures (simulation held): one item entering the CORE and the scan line of the site being
# taken apart (its progress); the marks themselves stay checked.
ITEM_GHOSTS = (650, 451, 659, 465)
SCAN_GHOSTS = (447, 473, 577, 485)
ITEM_REMOVAL = (709, 354, 720, 372)
SCAN_REMOVAL = (384, 389, 589, 404)
FILE_MASKS = {
    "tutorial-04-step5-mercato.png": [(584, 340, 596, 353)],
    "tutorial-06-step7-forno.png": [(581, 339, 596, 353)],
    "tutorial-08-done.png": [(573, 339, 613, 354)],
    "ui-hud.png": [ITEMS_CORE],
    "ui-palette-info.png": [ITEMS_CORE],
    "ui-fantasma-valido.png": [ITEMS_CORE],
    "ui-fantasma-non-valido.png": [ITEMS_CORE],
    "ui-impostazioni-en.png": [ITEMS_CORE],
    "ui-impostazioni-it.png": [ITEMS_CORE],
    "ui-rifiuto.png": [ITEMS_CORE],
    # zoomed view: the two items on the belt
    "ui-belt-into-core.png": [(550, 341, 598, 379), (661, 341, 693, 379)],
    "ui-nuova-partita.png": [POWER_HUD],
    "ui-palette-inizio.png": [POWER_HUD],
    "ui-palette-tutto.png": [POWER_HUD],
    "ui-scala-100-hud.png": [POWER_HUD],
    "ui-fantasma-titanio.png": [QUEUE_BAR],
    "ui-colori-normale-impostazioni.png": [QUEUE_BAR, SCAN_REMOVAL, ITEM_REMOVAL],
    "ui-colori-daltonici-impostazioni.png": [QUEUE_BAR, (503, 352, 561, 360)],
    "ui-colori-altocontrasto-impostazioni.png": [QUEUE_BAR],
    "ui-colori-normale-smontaggio.png": [QUEUE_BAR, SCAN_REMOVAL, ITEM_REMOVAL],
    "ui-colori-daltonici-smontaggio.png": [QUEUE_BAR, SCAN_REMOVAL, ITEM_REMOVAL],
    "ui-colori-altocontrasto-smontaggio.png": [QUEUE_BAR, SCAN_REMOVAL, ITEM_REMOVAL],
    "ui-colori-normale-valido.png": [QUEUE_BAR, SCAN_GHOSTS, ITEM_GHOSTS],
    "ui-colori-daltonici-valido.png": [QUEUE_BAR, SCAN_GHOSTS, ITEM_GHOSTS],
    "ui-colori-altocontrasto-valido.png": [QUEUE_BAR, SCAN_GHOSTS, ITEM_GHOSTS],
    "ui-colori-normale-non-valido.png": [QUEUE_BAR, SCAN_GHOSTS, ITEM_GHOSTS],
    "ui-colori-daltonici-non-valido.png": [QUEUE_BAR, SCAN_GHOSTS, ITEM_GHOSTS],
    "ui-colori-altocontrasto-non-valido.png": [QUEUE_BAR, SCAN_GHOSTS, ITEM_GHOSTS],
    # save date and time of the autosave (whole "AAAA-MM-GG hh:mm", Italian and English layouts): any digit
    # can change between runs
    "ui-scala-75-salvataggi.png": [(565, 267, 705, 284)],
    "ui-scala-100-salvataggi.png": [(527, 237, 727, 257)],
    "ui-scala-125-salvataggi.png": [(522, 207, 745, 231)],
    "ui-scala-150-salvataggi.png": [(500, 240, 766, 267)],
    "ui-scala-200-salvataggi.png": [(595, 285, 951, 319)],
}


def masked(img, name):
    img = img.convert("RGB")
    d = ImageDraw.Draw(img)
    for m in MASKS + FILE_MASKS.get(name, []):
        d.rectangle((m[0], m[1], m[2] - 1, m[3] - 1), fill=(0, 0, 0))
    return img


def main(a, b):
    da, db = Path(a), Path(b)
    names = sorted(p.name for p in da.glob("*.png"))
    if not names:
        print(f"no PNGs in {da}")
        return 1
    bad = 0
    for n in names:
        pb = db / n
        if not pb.exists():
            print(f"{n}: missing in {db}")
            bad += 1
            continue
        ia, ib = Image.open(da / n), Image.open(pb)
        if ia.size != ib.size:
            print(f"{n}: size {ia.size} vs {ib.size}")
            bad += 1
            continue
        box = ImageChops.difference(masked(ia, n), masked(ib, n)).getbbox()
        if box:
            print(f"{n}: differs, bbox={box}")
            bad += 1
    for p in db.glob("*.png"):
        if not (da / p.name).exists():
            print(f"{p.name}: missing in {da}")
            bad += 1
    print(f"{len(names)} compared, {bad} differing")
    return 1 if bad else 0


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit("usage: compare_shots.py <dirA> <dirB>")
    sys.exit(main(sys.argv[1], sys.argv[2]))
