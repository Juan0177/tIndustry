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

# Extra masks per screenshot, only where animation/timing noise was observed by diffing
# identical runs. A real change inside these rectangles would be MISSED.
GEAR = (470, 325, 745, 455)  # miner gear + belt items beside the CORE (animated);
#                              a sprite/layout change of those machines goes unseen
HUD_COUNT = (30, 100, 95, 135)  # HUD first resource counter value (tick timing);
#                                a change to that counter's look/position goes unseen
# HUD power digit in the very first tutorial frame: it shows the power buffer after however many
# ticks the first frame ran (frame length varies), so it is timing noise. A change to that digit's
# look/position in this one screenshot goes unseen.
POWER_DIGIT = (258, 77, 272, 95)
# Construction drone resting beside the CORE (west side), drawn since sp3 task 4: it hovers on a
# timer (graphics only), so its pixels vary between runs. Each box is the drone's measured footprint
# in that screenshot plus 2 px for the hover; a change to the drone or to the ground under it goes
# unseen there. In the ui screenshots the drone already lies inside GEAR.
DRONE_TUTORIAL_01 = (574, 331, 608, 364)
DRONE_TUTORIAL_02 = (313, 189, 348, 223)
FILE_MASKS = {
    "tutorial-01-step1-camera.png": [POWER_DIGIT, DRONE_TUTORIAL_01],
    "tutorial-02-step2-palette.png": [DRONE_TUTORIAL_02],
    "ui-hud.png": [GEAR],
    "ui-palette-info.png": [GEAR],
    "ui-mercato.png": [GEAR],  # only the belt strip under the modal edge varies
    # the running miner and belt items show through the campaign list card; a change to the card there goes unseen
    "ui-campagna-mappa-mancante.png": [(320, 316, 670, 539)],
    # zoomed view: the machines fill this area; a change there goes unseen
    "ui-belt-into-core.png": [(200, 300, 720, 540)],
    "tutorial-03-step3-miner-placed.png": [GEAR],
    "tutorial-04-step5-mercato.png": [GEAR],
    "tutorial-05-mercato-open.png": [GEAR, HUD_COUNT],
    "tutorial-06-step7-forno.png": [GEAR],
    "tutorial-07-step8-impostazioni.png": [GEAR],
    "tutorial-08-done.png": [GEAR],
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
