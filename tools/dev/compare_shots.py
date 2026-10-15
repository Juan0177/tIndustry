#!/usr/bin/env python3
"""Usage: compare_shots.py <dirA> <dirB>
Compares same-named PNGs; exit 0 if identical outside the masked rectangles.
Reports name + bounding box of the difference otherwise (exit 1)."""
import sys
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw

# (left, top, right, bottom) rectangles painted black on both images before diffing.
MASKS = [
    (1180, 0, 1280, 30),  # FPS counter
    # Known animated / timing-dependent zones (found by diffing two identical runs):
    (470, 325, 745, 455),  # miner gear + belt items next to the CORE at default zoom
    (200, 300, 720, 540),  # same machines in the zoomed ui-belt-into-core shot
    (50, 108, 95, 135),    # HUD first resource counter (value depends on tick timing)
]


def masked(img):
    img = img.convert("RGB")
    d = ImageDraw.Draw(img)
    for m in MASKS:
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
        box = ImageChops.difference(masked(ia), masked(ib)).getbbox()
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
