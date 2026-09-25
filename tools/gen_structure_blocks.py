#!/usr/bin/env python3
"""Generate Mindustry-style opaque 64×64 block sprites for Godot placeables.

Logistics use Blu1–4 (#191E24 / #24292F / #323C46 / #86A7B8).
Production = warm umber/amber; Power = gold; Core = steel cyan.
"""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw

OUT = Path("/workspace/godot/assets")
SIZE = 64

# Blu logistics (match belt_corner_v2.gdshader)
BLU1 = (0x19, 0x1E, 0x24, 255)
BLU2 = (0x24, 0x29, 0x2F, 255)
BLU3 = (0x32, 0x3C, 0x46, 255)
BLU4 = (0x86, 0xA7, 0xB8, 255)
BLU4_DIM = (0x5A, 0x78, 0x8A, 255)
WHITE = (0xE8, 0xEE, 0xF2, 255)
BLACK = (0x0A, 0x0C, 0x0E, 255)

# Production
PROD_DARK = (0x1A, 0x16, 0x12, 255)
PROD_BODY = (0x3A, 0x2E, 0x24, 255)
PROD_MID = (0x5C, 0x48, 0x36, 255)
PROD_ACCENT = (0xC4, 0x7A, 0x3A, 255)
PROD_HOT = (0xE8, 0xA0, 0x4A, 255)
PROD_EDGE = (0x8A, 0x6A, 0x48, 255)

# Power
POW_DARK = (0x1C, 0x18, 0x10, 255)
POW_BODY = (0x3A, 0x32, 0x1C, 255)
POW_MID = (0x6A, 0x58, 0x28, 255)
POW_GOLD = (0xD4, 0xB4, 0x4A, 255)
POW_BRIGHT = (0xF0, 0xDC, 0x7A, 255)

# Core
CORE_DARK = (0x12, 0x1C, 0x28, 255)
CORE_BODY = (0x2A, 0x4A, 0x6A, 255)
CORE_MID = (0x3A, 0x68, 0x8A, 255)
CORE_CYAN = (0x5A, 0xC8, 0xE8, 255)


def new_img(fill=BLU2) -> Image.Image:
    return Image.new("RGBA", (SIZE, SIZE), fill)


def border(draw: ImageDraw.ImageDraw, color=BLU1, w=2) -> None:
    for i in range(w):
        draw.rectangle([i, i, SIZE - 1 - i, SIZE - 1 - i], outline=color)


def fill_rect(draw, xy, color) -> None:
    draw.rectangle(xy, fill=color)


def chevron_down(draw, cx, cy, w=10, h=8, color=BLU4) -> None:
    draw.polygon([(cx, cy + h // 2), (cx - w // 2, cy - h // 2), (cx + w // 2, cy - h // 2)], fill=color)


def chevron_right(draw, cx, cy, w=8, h=10, color=BLU4) -> None:
    draw.polygon([(cx + w // 2, cy), (cx - w // 2, cy - h // 2), (cx - w // 2, cy + h // 2)], fill=color)


def save(img: Image.Image, name: str) -> None:
    path = OUT / name
    img.save(path)
    print(f"wrote {path}")


def draw_belt_t1() -> None:
    img = new_img(BLU3)
    d = ImageDraw.Draw(img)
    # rails
    fill_rect(d, [0, 0, SIZE - 1, 7], BLU1)
    fill_rect(d, [0, SIZE - 8, SIZE - 1, SIZE - 1], BLU1)
    fill_rect(d, [0, 8, SIZE - 1, SIZE - 9], BLU2)
    # center track
    fill_rect(d, [4, 18, SIZE - 5, SIZE - 19], BLU3)
    for x in (16, 32, 48):
        chevron_right(d, x, 32, w=10, h=14, color=BLU4)
    border(d, BLU1, 2)
    save(img, "conveyor-basic.png")


def draw_belt_t2() -> None:
    img = new_img(BLU3)
    d = ImageDraw.Draw(img)
    fill_rect(d, [0, 0, SIZE - 1, 7], BLU1)
    fill_rect(d, [0, SIZE - 8, SIZE - 1, SIZE - 1], BLU1)
    fill_rect(d, [0, 8, SIZE - 1, SIZE - 9], BLU2)
    fill_rect(d, [4, 18, SIZE - 5, SIZE - 19], BLU3)
    # Same single chevron row as T1 — border accent is the T2 tell.
    for x in (16, 32, 48):
        chevron_right(d, x, 32, w=10, h=14, color=BLU4)
    # T2 accent stripe (highlighted bordino)
    fill_rect(d, [2, 10, SIZE - 3, 13], BLU4)
    fill_rect(d, [2, SIZE - 14, SIZE - 3, SIZE - 11], BLU4)
    border(d, BLU1, 2)
    save(img, "conveyor-fast.png")


def draw_junction() -> None:
    img = new_img(BLU2)
    d = ImageDraw.Draw(img)
    fill_rect(d, [4, 4, SIZE - 5, SIZE - 5], BLU3)
    # cross channels
    fill_rect(d, [26, 6, 37, SIZE - 7], BLU4_DIM)
    fill_rect(d, [6, 26, SIZE - 7, 37], BLU4_DIM)
    fill_rect(d, [28, 8, 35, SIZE - 9], BLU4)
    fill_rect(d, [8, 28, SIZE - 9, 35], BLU4)
    # hub
    d.ellipse([24, 24, 39, 39], fill=BLU1, outline=BLU4)
    d.ellipse([28, 28, 35, 35], fill=BLU4)
    border(d, BLU1, 2)
    save(img, "junction.png")


def draw_splitter() -> None:
    img = new_img(BLU2)
    d = ImageDraw.Draw(img)
    fill_rect(d, [4, 4, SIZE - 5, SIZE - 5], BLU3)
    # T shape: in from top, out L/R
    fill_rect(d, [26, 6, 37, 34], BLU4_DIM)
    fill_rect(d, [28, 8, 35, 32], BLU4)
    fill_rect(d, [8, 26, SIZE - 9, 37], BLU4_DIM)
    fill_rect(d, [10, 28, SIZE - 11, 35], BLU4)
    chevron_down(d, 32, 18, w=12, h=8, color=WHITE)
    chevron_right(d, 48, 32, w=8, h=10, color=WHITE)
    d.polygon([(16, 32), (24, 26), (24, 38)], fill=WHITE)  # left
    border(d, BLU1, 2)
    save(img, "splitter.png")


def draw_sorter() -> None:
    img = new_img(BLU2)
    d = ImageDraw.Draw(img)
    fill_rect(d, [4, 4, SIZE - 5, SIZE - 5], BLU3)
    fill_rect(d, [26, 6, 37, SIZE - 7], BLU4_DIM)
    fill_rect(d, [28, 8, 35, SIZE - 9], BLU4)
    # filter plate
    fill_rect(d, [14, 22, 49, 41], BLU1)
    fill_rect(d, [16, 24, 47, 39], PROD_ACCENT)
    fill_rect(d, [20, 28, 43, 35], PROD_HOT)
    # side reject notch
    fill_rect(d, [6, 28, 14, 35], BLU4_DIM)
    border(d, BLU1, 2)
    save(img, "sorter.png")


def draw_bridge() -> None:
    img = new_img(BLU2)
    d = ImageDraw.Draw(img)
    fill_rect(d, [4, 4, SIZE - 5, SIZE - 5], BLU3)
    # elevated thin span
    fill_rect(d, [6, 24, SIZE - 7, 39], BLU1)
    fill_rect(d, [8, 26, SIZE - 9, 37], BLU4_DIM)
    fill_rect(d, [10, 28, SIZE - 11, 35], BLU4)
    # end pylons
    fill_rect(d, [8, 16, 18, 47], BLU1)
    fill_rect(d, [45, 16, 55, 47], BLU1)
    fill_rect(d, [10, 18, 16, 45], BLU3)
    fill_rect(d, [47, 18, 53, 45], BLU3)
    for x in (22, 32, 42):
        chevron_right(d, x, 32, w=7, h=8, color=WHITE)
    border(d, BLU1, 2)
    save(img, "bridge.png")


def draw_miner(advanced: bool = False) -> None:
    img = new_img(PROD_DARK)
    d = ImageDraw.Draw(img)
    fill_rect(d, [3, 3, SIZE - 4, SIZE - 4], PROD_BODY)
    fill_rect(d, [6, 6, SIZE - 7, SIZE - 7], PROD_MID)
    # drill tower
    fill_rect(d, [22, 8, 41, 28], PROD_EDGE)
    fill_rect(d, [25, 10, 38, 26], PROD_ACCENT if not advanced else POW_GOLD)
    # bit
    d.polygon([(32, 48), (22, 28), (42, 28)], fill=BLU1)
    d.polygon([(32, 44), (26, 30), (38, 30)], fill=BLU4 if not advanced else POW_BRIGHT)
    # base ring
    d.ellipse([14, 36, 49, 55], outline=PROD_EDGE, width=3)
    d.ellipse([20, 40, 43, 52], fill=PROD_DARK, outline=PROD_ACCENT)
    if advanced:
        fill_rect(d, [8, 8, 18, 14], POW_GOLD)
        fill_rect(d, [45, 8, 55, 14], POW_GOLD)
    border(d, PROD_DARK, 2)
    fill_rect(d, [0, 0, SIZE - 1, 1], PROD_ACCENT)
    save(img, "miner-advanced.png" if advanced else "miner.png")


def draw_extractor() -> None:
    img = new_img(PROD_DARK)
    d = ImageDraw.Draw(img)
    fill_rect(d, [3, 3, SIZE - 4, SIZE - 4], PROD_BODY)
    fill_rect(d, [8, 8, SIZE - 9, SIZE - 9], BLU3)
    # intake funnel from core side (bottom)
    d.polygon([(12, 52), (32, 28), (52, 52)], fill=BLU2)
    d.polygon([(18, 50), (32, 34), (46, 50)], fill=BLU4)
    # outlet pipe top
    fill_rect(d, [26, 8, 37, 30], BLU1)
    fill_rect(d, [28, 10, 35, 28], BLU4)
    chevron_down(d, 32, 18, w=10, h=7, color=WHITE)
    # filter badge
    fill_rect(d, [40, 10, 54, 22], PROD_ACCENT)
    border(d, BLU1, 2)
    save(img, "extractor.png")


def draw_smelter() -> None:
    img = new_img(PROD_DARK)
    d = ImageDraw.Draw(img)
    fill_rect(d, [3, 3, SIZE - 4, SIZE - 4], PROD_BODY)
    fill_rect(d, [8, 14, SIZE - 9, SIZE - 7], PROD_MID)
    # chimney
    fill_rect(d, [26, 4, 37, 18], PROD_EDGE)
    fill_rect(d, [28, 6, 35, 16], PROD_ACCENT)
    # door / flame window
    fill_rect(d, [18, 24, 45, 48], PROD_DARK)
    fill_rect(d, [22, 28, 41, 44], PROD_HOT)
    d.polygon([(32, 30), (26, 40), (30, 40), (28, 44), (36, 44), (34, 40), (38, 40)], fill=WHITE)
    # vents
    for x in (14, 20, 44, 50):
        fill_rect(d, [x, 52, x + 3, 58], PROD_DARK)
    border(d, PROD_DARK, 2)
    fill_rect(d, [0, 0, SIZE - 1, 1], PROD_HOT)
    save(img, "smelter.png")


def draw_assembler() -> None:
    img = new_img(PROD_DARK)
    d = ImageDraw.Draw(img)
    fill_rect(d, [3, 3, SIZE - 4, SIZE - 4], BLU2)
    fill_rect(d, [8, 8, SIZE - 9, SIZE - 9], BLU3)
    # gear-ish plate
    d.ellipse([16, 16, 47, 47], fill=BLU1, outline=BLU4, width=3)
    d.ellipse([24, 24, 39, 39], fill=BLU4_DIM, outline=BLU4)
    d.ellipse([28, 28, 35, 35], fill=PROD_ACCENT)
    # corner mounts
    for xy in [(8, 8), (48, 8), (8, 48), (48, 48)]:
        fill_rect(d, [xy[0], xy[1], xy[0] + 7, xy[1] + 7], PROD_EDGE)
    # I/O notches
    fill_rect(d, [28, 3, 35, 10], BLU4)
    fill_rect(d, [28, 53, 35, 60], BLU4)
    border(d, BLU1, 2)
    save(img, "assembler.png")


def draw_generator() -> None:
    img = new_img(POW_DARK)
    d = ImageDraw.Draw(img)
    fill_rect(d, [3, 3, SIZE - 4, SIZE - 4], POW_BODY)
    fill_rect(d, [8, 10, SIZE - 9, SIZE - 9], POW_MID)
    # turbine housing
    d.ellipse([14, 14, 49, 49], fill=POW_DARK, outline=POW_GOLD, width=3)
    d.ellipse([22, 22, 41, 41], fill=POW_BODY, outline=POW_BRIGHT)
    # blades
    fill_rect(d, [30, 16, 33, 47], POW_GOLD)
    fill_rect(d, [16, 30, 47, 33], POW_GOLD)
    d.ellipse([28, 28, 35, 35], fill=POW_BRIGHT)
    # fuel port
    fill_rect(d, [10, 50, 22, 58], BLU1)
    fill_rect(d, [12, 52, 20, 56], BLU4)
    border(d, POW_DARK, 2)
    fill_rect(d, [0, 0, SIZE - 1, 1], POW_GOLD)
    save(img, "generator.png")


def draw_power_node(t2: bool = False) -> None:
    img = new_img(POW_DARK)
    d = ImageDraw.Draw(img)
    fill_rect(d, [3, 3, SIZE - 4, SIZE - 4], POW_BODY)
    # node disc
    r = 22 if not t2 else 26
    c = 32
    d.ellipse([c - r, c - r, c + r, c + r], fill=POW_MID, outline=POW_GOLD, width=3)
    d.ellipse([c - 10, c - 10, c + 10, c + 10], fill=POW_DARK, outline=POW_BRIGHT)
    d.ellipse([c - 4, c - 4, c + 4, c + 4], fill=POW_BRIGHT)
    # link stubs
    for a, b in [((32, 4), (32, 12)), ((32, 52), (32, 60)), ((4, 32), (12, 32)), ((52, 32), (60, 32))]:
        fill_rect(d, [min(a[0], b[0]), min(a[1], b[1]), max(a[0], b[0]), max(a[1], b[1])], POW_GOLD)
    if t2:
        fill_rect(d, [8, 8, 16, 16], POW_BRIGHT)
        fill_rect(d, [47, 8, 55, 16], POW_BRIGHT)
    border(d, POW_DARK, 2)
    save(img, "power-node-t2.png" if t2 else "power-node.png")


def draw_core() -> None:
    img = new_img(CORE_DARK)
    d = ImageDraw.Draw(img)
    fill_rect(d, [3, 3, SIZE - 4, SIZE - 4], CORE_BODY)
    fill_rect(d, [10, 10, SIZE - 11, SIZE - 11], CORE_MID)
    # diamond core
    d.polygon([(32, 14), (50, 32), (32, 50), (14, 32)], fill=CORE_DARK, outline=CORE_CYAN)
    d.polygon([(32, 22), (42, 32), (32, 42), (22, 32)], fill=CORE_CYAN)
    d.ellipse([28, 28, 35, 35], fill=WHITE)
    border(d, CORE_DARK, 2)
    fill_rect(d, [0, 0, SIZE - 1, 1], CORE_CYAN)
    save(img, "core.png")


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    draw_belt_t1()
    draw_belt_t2()
    draw_junction()
    draw_splitter()
    draw_sorter()
    draw_bridge()
    draw_miner(False)
    draw_miner(True)
    draw_extractor()
    draw_smelter()
    draw_assembler()
    draw_generator()
    draw_power_node(False)
    draw_power_node(True)
    draw_core()
    print("done")


if __name__ == "__main__":
    main()
