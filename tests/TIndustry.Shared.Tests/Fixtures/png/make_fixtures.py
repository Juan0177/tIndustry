"""Writes the PNG fixtures of PngReaderTests (run from this folder: python make_fixtures.py).

Each image is the pattern of PngReaderTests.Expected, encoded with an explicit filter per row so every
filter type (0-4) is covered; Pillow then decodes each file to check it matches the pattern.
"""
import struct
import zlib

from PIL import Image

W, H = 7, 5


def pixel(x, y):
    return ((x * 37 + y * 11) & 255, (x * 5 + y * 71) & 255, ((x ^ y) * 13) & 255, (x * y * 9 + 40) & 255)


def paeth(a, b, c):
    p = a + b - c
    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    if pa <= pb and pa <= pc:
        return a
    return b if pb <= pc else c


def encode(rows, bpp, filters):
    out = bytearray()
    prev = bytes(len(rows[0]))
    for row, f in zip(rows, filters):
        out.append(f)
        for i, v in enumerate(row):
            a = row[i - bpp] if i >= bpp else 0
            b = prev[i]
            c = prev[i - bpp] if i >= bpp else 0
            pred = [0, a, b, (a + b) // 2, paeth(a, b, c)][f]
            out.append((v - pred) & 255)
        prev = row
    return bytes(out)


def chunk(kind, data):
    body = kind + data
    return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)


def write(name, alpha, filters, interlace=0, split=False):
    bpp = 4 if alpha else 3
    rows = [bytes(v for x in range(W) for v in pixel(x, y)[:bpp]) for y in range(H)]
    data = zlib.compress(encode(rows, bpp, filters), 9)
    ihdr = struct.pack(">IIBBBBB", W, H, 8, 6 if alpha else 2, 0, 0, interlace)
    idat = chunk(b"IDAT", data[:5]) + chunk(b"IDAT", data[5:]) if split else chunk(b"IDAT", data)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"tEXt", b"Comment\x00prova") + idat + chunk(b"IEND", b"")
    with open(name, "wb") as f:
        f.write(png)
    if not interlace:
        img = Image.open(name)
        img.load()
        for y in range(H):
            for x in range(W):
                assert img.getpixel((x, y)) == pixel(x, y)[:bpp], (name, x, y)


for f in range(5):
    write(f"rgb-filtro{f}.png", False, [f] * H)
    write(f"rgba-filtro{f}.png", True, [f] * H)
write("rgba-misto.png", True, [4, 3, 2, 1, 0], split=True)
write("interlacciato.png", False, [0] * H, interlace=1)
print("ok")
