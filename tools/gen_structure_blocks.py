#!/usr/bin/env python3
"""Generate Mindustry-style opaque 64×64 block sprites for Godot placeables.

Logistics use Blu1–4 (#191E24 / #24292F / #323C46 / #86A7B8).
Production = warm umber/amber; Power = gold; Core = steel cyan.
"""
from __future__ import annotations

import math
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
CORE_DARK = (0x12, 0x1A, 0x22, 255)
CORE_BODY = (0x2A, 0x3A, 0x48, 255)
CORE_MID = (0x3A, 0x52, 0x64, 255)
CORE_CYAN = (0x5A, 0xC8, 0xD8, 255)

# Gear colors — T1 bronze/copper (reference), T2 red
RUST = (0x9A, 0x6A, 0x38, 255)
RUST_MID = (0xC4, 0x8A, 0x48, 255)
RUST_DARK = (0x5C, 0x38, 0x1C, 255)
RUST_HUB = (0xE0, 0xA8, 0x5C, 255)
GEAR_RED = (0xC4, 0x32, 0x28, 255)
GEAR_RED_MID = (0xE0, 0x48, 0x38, 255)
GEAR_RED_DARK = (0x7A, 0x18, 0x14, 255)
GEAR_RED_HUB = (0xF0, 0x70, 0x58, 255)
PERNO = (0x2A, 0x28, 0x24, 255)
PERNO_LIT = (0xF0, 0xDC, 0x7A, 255)
ACCENT_DOT = (0xF0, 0xC8, 0x3A, 255)


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
    """Thin pad; azzurro corner circle; SE border/pad arcs with the circle (raccordo)."""
    img = new_img(BLU2)
    pix = img.load()
    # Body fill
    for y in range(SIZE):
        for x in range(SIZE):
            pix[x, y] = BLU3

    cx, cy, r = 49, 49, 11
    # Concentric raccordo: pad edge follows circle (outer ring = thin border tone)
    r_border = r + 6
    r_pad = r + 3

    def dist(x, y):
        return math.hypot(x - cx, y - cy)

    for y in range(SIZE):
        for x in range(SIZE):
            d = dist(x, y)
            # SE quadrant relative to circle center (toward tile corner)
            in_se = x >= cx - 1 and y >= cy - 1
            if in_se and d <= r:
                continue  # circle drawn later
            if in_se and d <= r_pad:
                pix[x, y] = BLU3  # gap between circle and pad edge
            elif in_se and d <= r_border:
                pix[x, y] = BLU1  # border arc following circle
            elif in_se and d > r_border and (x > SIZE - 3 or y > SIZE - 3):
                # Outside the arc toward the absolute SE corner — keep dark void / border stub
                pix[x, y] = BLU1 if (x >= SIZE - 2 or y >= SIZE - 2) else BLU2
            else:
                # Main pad body (slightly inset)
                if 3 <= x <= SIZE - 4 and 3 <= y <= SIZE - 4:
                    pix[x, y] = BLU2
                elif 1 <= x <= SIZE - 2 and 1 <= y <= SIZE - 2:
                    pix[x, y] = BLU3

    # Thin border on N / W / E-above-arc / S-left-of-arc
    for i in range(2):
        for x in range(SIZE):
            # top always
            pix[x, i] = BLU1
            # bottom only west of raccordo
            if x <= cx - r_border:
                pix[x, SIZE - 1 - i] = BLU1
        for y in range(SIZE):
            pix[i, y] = BLU1
            if y <= cy - r_border:
                pix[SIZE - 1 - i, y] = BLU1

    # Azzurro circle (primary accent)
    d = ImageDraw.Draw(img)
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=BLU4, outline=BLU1)
    d.ellipse([cx - 5, cy - 5, cx + 5, cy + 5], fill=BLU4_DIM)
    d.ellipse([cx - 2, cy - 2, cx + 2, cy + 2], fill=WHITE)

    save(img, "splitter.png")


def draw_sorter() -> None:
    """Thin-border square frame; filter icon is overlaid dynamically in Godot (~70%)."""
    img = new_img(BLU2)
    d = ImageDraw.Draw(img)
    fill_rect(d, [2, 2, SIZE - 3, SIZE - 3], BLU3)
    # Soft inner well for the filter glyph
    fill_rect(d, [10, 10, SIZE - 11, SIZE - 11], BLU2)
    # Neutral empty mark (shown when no overlay / as underlay)
    d.ellipse([26, 26, 37, 37], outline=BLU4_DIM, width=2)
    border(d, BLU1, 2)
    save(img, "sorter.png")


def draw_bridge() -> None:
    """Anchor pad: thin border + 4 corner circles joined as X (cut by 4 smaller body circles)."""
    img = new_img(BLU2)
    d = ImageDraw.Draw(img)
    fill_rect(d, [2, 2, SIZE - 3, SIZE - 3], BLU3)
    # 4 large corner circles (Blu4) — X arms
    r = 14
    corners = [(10, 10), (SIZE - 11, 10), (10, SIZE - 11), (SIZE - 11, SIZE - 11)]
    for cx, cy in corners:
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=BLU4)
    # 4 smaller body-color circles to carve the X look (mid-edge cuts)
    cut = 11
    cuts = [(32, 8), (32, SIZE - 9), (8, 32), (SIZE - 9, 32)]
    for cx, cy in cuts:
        d.ellipse([cx - cut, cy - cut, cx + cut, cy + cut], fill=BLU3)
    # Center hub
    d.ellipse([26, 26, 37, 37], fill=BLU1, outline=BLU4)
    d.ellipse([29, 29, 34, 34], fill=BLU4)
    border(d, BLU1, 2)
    save(img, "bridge.png")


def draw_gear(size: int, rust: bool, teeth: int = 6) -> Image.Image:
    """Full gear with transparent background; perno at center. Default 6 teeth."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx = cy = size // 2
    # Leave 1px margin so teeth stay inside the texture bounds.
    outer = size // 2 - 4
    tooth_out = outer + 3
    body = RUST_MID if rust else GEAR_RED_MID
    dark = RUST_DARK if rust else GEAR_RED_DARK
    mid = RUST if rust else GEAR_RED
    hub = RUST_HUB if rust else GEAR_RED_HUB
    half_w = 0.28  # tooth angular half-width
    for i in range(teeth):
        ang = i * (2 * math.pi / teeth) - math.pi / 2
        tip = (cx + int(math.cos(ang) * tooth_out), cy + int(math.sin(ang) * tooth_out))
        b1 = (
            cx + int(math.cos(ang - half_w) * (outer - 1)),
            cy + int(math.sin(ang - half_w) * (outer - 1)),
        )
        b2 = (
            cx + int(math.cos(ang + half_w) * (outer - 1)),
            cy + int(math.sin(ang + half_w) * (outer - 1)),
        )
        d.polygon([tip, b1, b2], fill=body)
        d.line([b1, tip, b2], fill=dark, width=1)
    # Rim
    d.ellipse([cx - outer, cy - outer, cx + outer, cy + outer], fill=mid, outline=dark)
    d.ellipse(
        [cx - outer + 3, cy - outer + 3, cx + outer - 3, cy + outer - 3],
        fill=body,
    )
    # Spokes toward teeth
    for i in range(teeth):
        ang = i * (2 * math.pi / teeth) - math.pi / 2
        x2 = cx + int(math.cos(ang) * (outer - 6))
        y2 = cy + int(math.sin(ang) * (outer - 6))
        d.line([(cx, cy), (x2, y2)], fill=dark, width=2)
    hub_r = max(5, size // 9)
    perno_r = max(2, size // 18)
    d.ellipse([cx - hub_r, cy - hub_r, cx + hub_r, cy + hub_r], fill=hub, outline=dark)
    d.ellipse([cx - perno_r, cy - perno_r, cx + perno_r, cy + perno_r], fill=PERNO)
    return img


def draw_miner_body(advanced: bool = False) -> Image.Image:
    """Square pad: dark outer rim, light blue-grey frame, dark interior + border accents."""
    img = new_img(BLU1)
    d = ImageDraw.Draw(img)
    # Light blue-grey frame (reference style)
    fill_rect(d, [2, 2, SIZE - 3, SIZE - 3], BLU4)
    # Dark interior pad (gear sits here; keep clear of frame)
    fill_rect(d, [6, 6, SIZE - 7, SIZE - 7], BLU2)
    fill_rect(d, [8, 8, SIZE - 9, SIZE - 9], BLU1)
    # Mid-side dark pin accents on the BLU4 frame (belt-tile language)
    mid = SIZE // 2
    pin = 3
    for x0, y0, x1, y1 in [
        (mid - pin, 2, mid + pin, 5),  # N
        (mid - pin, SIZE - 6, mid + pin, SIZE - 3),  # S
        (2, mid - pin, 5, mid + pin),  # W
        (SIZE - 6, mid - pin, SIZE - 3, mid + pin),  # E
    ]:
        fill_rect(d, [x0, y0, x1, y1], BLU1)
    # Corner accents (small dots on frame)
    for cx, cy in [(4, 4), (SIZE - 5, 4), (4, SIZE - 5), (SIZE - 5, SIZE - 5)]:
        d.ellipse([cx - 1, cy - 1, cx + 1, cy + 1], fill=BLU4_DIM if not advanced else ACCENT_DOT)
    border(d, BLACK, 2)
    return img


def draw_miner(advanced: bool = False) -> None:
    """Bordered pad + one large centered 6-tooth gear that fits inside the frame."""
    img = draw_miner_body(advanced)
    # Inner pad is ~48px; gear ~44 so teeth clear the BLU4 frame.
    gear = draw_gear(44, rust=not advanced, teeth=6)
    ox = (SIZE - gear.width) // 2
    oy = (SIZE - gear.height) // 2
    img.alpha_composite(gear, (ox, oy))
    save(img, "miner-advanced.png" if advanced else "miner.png")


def draw_miner_gear_assets() -> None:
    """Full gear sprites for world animation (centered in Godot)."""
    draw_gear(64, rust=True, teeth=6).save(OUT / "gear-rust.png")
    print(f"wrote {OUT / 'gear-rust.png'}")
    draw_gear(64, rust=False, teeth=6).save(OUT / "gear-red.png")
    print(f"wrote {OUT / 'gear-red.png'}")
    draw_miner_body(False).save(OUT / "miner-pad.png")
    print(f"wrote {OUT / 'miner-pad.png'}")
    draw_miner_body(True).save(OUT / "miner-pad-advanced.png")
    print(f"wrote {OUT / 'miner-pad-advanced.png'}")
    lit = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(lit)
    d.ellipse([1, 1, 14, 14], fill=PERNO_LIT)
    d.ellipse([4, 4, 11, 11], fill=WHITE)
    lit.save(OUT / "gear-perno-lit.png")
    print(f"wrote {OUT / 'gear-perno-lit.png'}")


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
    draw_miner_gear_assets()
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
