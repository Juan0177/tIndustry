"""Pixel font of the interface (G4), drawn here glyph by glyph and exported as BMFont for Godot.

Every glyph is hand-placed, one character per pixel ('#' full, '.' empty), on a 9-row grid:
rows 0-6 sit above the baseline (capitals and digits are 7 px tall, lowercase 5 px from row 2),
rows 7-8 are the descender. Accented letters are their base letter plus a 2x2 accent: above a
capital it takes the two extra rows of the cell, above a lowercase letter rows 0-1 of the grid.
The cell is therefore 11 rows (2 accent rows + 9), with the baseline after row 8 of the cell.

Widths are proportional (most letters 5 px, digits all 5 px so numbers line up); the advance is
always width + 1 px. Glyphs are pure white on transparency: the client tints them with the theme.

Two sizes are exported to godot/assets/ui/ (text and titles), the glyphs scaled x2 and x3 with
no smoothing: `carattere-testo.fnt` + `.png` and `carattere-titoli.fnt` + `.png` (BMFont text
format, one page each). `file_carattere()` returns the bytes; genera.py writes and checks them.
"""
import io

from PIL import Image

BIANCO = (255, 255, 255, 255)
RIGHE = 9  # glyph grid rows: 7 above the baseline + 2 descender
ACCENTO_RIGHE = 2  # extra rows above the grid for the accents of capitals
CELLA = ACCENTO_RIGHE + RIGHE  # 11 rows per glyph in the atlas
INTERLINEA = 1  # empty row between lines
SPAZIATURA = 1  # px after every glyph
MISURE = {"testo": 2, "titoli": 3}  # exported size name -> scale of the 1x glyphs
NOME = "tIndustry"
ATLANTE_LARGHEZZA = 256  # atlas page width in px at every scale
MARGINE_ATLANTE = 2  # px between glyphs in the atlas

# Required set (G4 global constraints): printable ASCII plus the Italian accents and signs used.
EXTRA = "àèéìíòóùúÀÈÉÌÒÙ«»…·€×"
INSIEME = "".join(chr(c) for c in range(32, 127)) + EXTRA

# Base glyphs; rows beyond the given ones are empty (a 7-row glyph has no descender).
GLIFI = {
    " ": ["...", "...", "..."],
    "!": ["#", "#", "#", "#", "#", ".", "#"],
    '"': ["#.#", "#.#"],
    "#": [".#.#.", ".#.#.", "#####", ".#.#.", "#####", ".#.#.", ".#.#."],
    "$": ["..#..", ".####", "#.#..", ".###.", "..#.#", "####.", "..#.."],
    "%": ["##..#", "##..#", "...#.", "..#..", ".#...", "#..##", "#..##"],
    "&": [".##..", "#..#.", "#.#..", ".#...", "#.#.#", "#..#.", ".##.#"],
    "'": ["#", "#"],
    "(": [".#", "#.", "#.", "#.", "#.", "#.", ".#"],
    ")": ["#.", ".#", ".#", ".#", ".#", ".#", "#."],
    "*": [".....", "..#..", "#.#.#", ".###.", "#.#.#", "..#..", "....."],
    "+": [".....", "..#..", "..#..", "#####", "..#..", "..#..", "....."],
    ",": ["..", "..", "..", "..", "..", ".#", ".#", "#."],
    "-": ["....", "....", "....", "####"],
    ".": [".", ".", ".", ".", ".", ".", "#"],
    "/": ["....#", "....#", "...#.", "..#..", ".#...", "#....", "#...."],
    "0": [".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###."],
    "1": ["..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###."],
    "2": [".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"],
    "3": [".###.", "#...#", "....#", "..##.", "....#", "#...#", ".###."],
    "4": ["...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#."],
    "5": ["#####", "#....", "####.", "....#", "....#", "#...#", ".###."],
    "6": ["..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###."],
    "7": ["#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..."],
    "8": [".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###."],
    "9": [".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.."],
    ":": [".", ".", ".", "#", ".", ".", "#"],
    ";": ["..", "..", "..", ".#", "..", "..", ".#", ".#", "#."],
    "<": ["...#", "..#.", ".#..", "#...", ".#..", "..#.", "...#"],
    "=": [".....", ".....", "#####", ".....", "#####"],
    ">": ["#...", ".#..", "..#.", "...#", "..#.", ".#..", "#..."],
    "?": [".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.."],
    "@": [".###.", "#...#", "#.###", "#.#.#", "#.###", "#....", ".###."],
    "A": [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "B": ["####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."],
    "C": [".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."],
    "D": ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"],
    "F": ["#####", "#....", "#....", "####.", "#....", "#....", "#...."],
    "G": [".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####"],
    "H": ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"],
    "I": ["###", ".#.", ".#.", ".#.", ".#.", ".#.", "###"],
    "J": ["..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.."],
    "K": ["#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#"],
    "L": ["#....", "#....", "#....", "#....", "#....", "#....", "#####"],
    "M": ["#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"],
    "N": ["#...#", "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#"],
    "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "P": ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."],
    "Q": [".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#"],
    "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "S": [".###.", "#...#", "#....", ".###.", "....#", "#...#", ".###."],
    "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "U": ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "V": ["#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
    "W": ["#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#"],
    "X": ["#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#"],
    "Y": ["#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.."],
    "Z": ["#####", "....#", "...#.", "..#..", ".#...", "#....", "#####"],
    "[": ["##", "#.", "#.", "#.", "#.", "#.", "##"],
    "\\": ["#....", "#....", ".#...", "..#..", "...#.", "....#", "....#"],
    "]": ["##", ".#", ".#", ".#", ".#", ".#", "##"],
    "^": ["..#..", ".#.#.", "#...#"],
    "_": [".....", ".....", ".....", ".....", ".....", ".....", ".....", "#####"],
    "`": ["#.", ".#"],
    "a": [".....", ".....", ".###.", "....#", ".####", "#...#", ".####"],
    "b": ["#....", "#....", "####.", "#...#", "#...#", "#...#", "####."],
    "c": [".....", ".....", ".###.", "#....", "#....", "#...#", ".###."],
    "d": ["....#", "....#", ".####", "#...#", "#...#", "#...#", ".####"],
    "e": [".....", ".....", ".###.", "#...#", "#####", "#....", ".###."],
    "f": ["..##", ".#..", "####", ".#..", ".#..", ".#..", ".#.."],
    "g": [".....", ".....", ".####", "#...#", "#...#", "#...#", ".####", "....#", ".###."],
    "h": ["#....", "#....", "####.", "#...#", "#...#", "#...#", "#...#"],
    "i": [".#.", "...", "##.", ".#.", ".#.", ".#.", "###"],
    "j": ["...#", "....", "..##", "...#", "...#", "...#", "...#", "#..#", ".##."],
    "k": ["#...", "#...", "#..#", "#.#.", "##..", "#.#.", "#..#"],
    "l": ["##.", ".#.", ".#.", ".#.", ".#.", ".#.", "###"],
    "m": [".....", ".....", "##.#.", "#.#.#", "#.#.#", "#.#.#", "#...#"],
    "n": [".....", ".....", "####.", "#...#", "#...#", "#...#", "#...#"],
    "o": [".....", ".....", ".###.", "#...#", "#...#", "#...#", ".###."],
    "p": [".....", ".....", "####.", "#...#", "#...#", "#...#", "####.", "#....", "#...."],
    "q": [".....", ".....", ".####", "#...#", "#...#", "#...#", ".####", "....#", "....#"],
    "r": [".....", ".....", "#.##.", "##..#", "#....", "#....", "#...."],
    "s": [".....", ".....", ".####", "#....", ".###.", "....#", "####."],
    "t": [".#..", ".#..", "####", ".#..", ".#..", ".#..", "..##"],
    "u": [".....", ".....", "#...#", "#...#", "#...#", "#...#", ".####"],
    "v": [".....", ".....", "#...#", "#...#", "#...#", ".#.#.", "..#.."],
    "w": [".....", ".....", "#...#", "#...#", "#.#.#", "#.#.#", ".#.#."],
    "x": [".....", ".....", "#...#", ".#.#.", "..#..", ".#.#.", "#...#"],
    "y": [".....", ".....", "#...#", "#...#", "#...#", "#...#", ".####", "....#", ".###."],
    "z": [".....", ".....", "#####", "...#.", "..#..", ".#...", "#####"],
    "{": ["..#", ".#.", ".#.", "#..", ".#.", ".#.", "..#"],
    "|": ["#", "#", "#", "#", "#", "#", "#", "#"],
    "}": ["#..", ".#.", ".#.", "..#", ".#.", ".#.", "#.."],
    "~": [".....", ".....", ".#...", "#.#.#", "...#."],
    "«": [".....", ".....", "..#.#", ".#.#.", "#.#..", ".#.#.", "..#.#"],
    "»": [".....", ".....", "#.#..", ".#.#.", "..#.#", ".#.#.", "#.#.."],
    "…": [".....", ".....", ".....", ".....", ".....", ".....", "#.#.#"],
    "·": [".", ".", ".", "#"],
    "€": ["..###", ".#...", "####.", ".#...", "####.", ".#...", "..###"],
}
# The multiplication sign must not look like a lowercase x: smaller, centred on the middle row.
GLIFI["×"] = ["...", "...", "#.#", ".#.", "#.#"]

GRAVE = ["#.", ".#"]
ACUTO = [".#", "#."]
# Accented letter -> (base letter, accent). The lowercase i loses its dot under the accent.
ACCENTATE = {
    "à": ("a", GRAVE), "è": ("e", GRAVE), "é": ("e", ACUTO), "ì": ("i", GRAVE), "í": ("i", ACUTO),
    "ò": ("o", GRAVE), "ó": ("o", ACUTO), "ù": ("u", GRAVE), "ú": ("u", ACUTO),
    "À": ("A", GRAVE), "È": ("E", GRAVE), "É": ("E", ACUTO), "Ì": ("I", GRAVE),
    "Ò": ("O", GRAVE), "Ù": ("U", GRAVE),
}


def larghezza(ch):
    """Drawn width of a glyph in 1x pixels (accented letters: their base letter)."""
    base = ACCENTATE[ch][0] if ch in ACCENTATE else ch
    return len(GLIFI[base][0])


def griglia(ch):
    """Glyph as CELLA rows of '#'/'.' strings: 2 accent rows, then the 9-row grid."""
    base, accento = ACCENTATE.get(ch, (ch, None))
    righe = GLIFI[base]
    w = len(righe[0])
    if any(len(r) != w for r in righe) or len(righe) > RIGHE:
        raise ValueError(f"glifo {ch!r} di misura sbagliata")
    cella = [["."] * w for _ in range(CELLA)]
    for y, riga in enumerate(righe):
        for x, c in enumerate(riga):
            if c == "#":
                cella[ACCENTO_RIGHE + y][x] = "#"
    if accento is not None:
        if base == "i":
            for x in range(w):  # dotless i under the accent
                cella[ACCENTO_RIGHE][x] = "."
        y0 = 0 if base.isupper() else ACCENTO_RIGHE
        x0 = (w - 2) // 2 if accento is GRAVE else (w - 1) // 2
        for y, riga in enumerate(accento):
            for x, c in enumerate(riga):
                if c == "#":
                    cella[y0 + y][x0 + x] = "#"
    return ["".join(r) for r in cella]


def glifo(ch, k=1):
    """White glyph image (width x CELLA rows), scaled by k without smoothing."""
    righe = griglia(ch)
    im = Image.new("RGBA", (len(righe[0]), CELLA), (0, 0, 0, 0))
    for y, riga in enumerate(righe):
        for x, c in enumerate(riga):
            if c == "#":
                im.putpixel((x, y), BIANCO)
    return im.resize((im.width * k, im.height * k), Image.NEAREST) if k != 1 else im


def misura_testo(testo, k=1):
    """(width, height) in px of one line of text."""
    w = sum((larghezza(ch) + SPAZIATURA) * k for ch in testo)
    return max(0, w - SPAZIATURA * k), CELLA * k


def scrivi(testo, k=1, colore=BIANCO):
    """One line of text as an RGBA image, glyphs tinted with `colore` (review sheets, tests)."""
    w, h = misura_testo(testo, k)
    im = Image.new("RGBA", (max(1, w), h), (0, 0, 0, 0))
    x = 0
    for ch in testo:
        g = glifo(ch, k)
        if colore != BIANCO:
            tinta = Image.new("RGBA", g.size, colore)
            tinta.putalpha(g.getchannel("A"))
            g = tinta
        im.alpha_composite(g, (x, 0))
        x += (larghezza(ch) + SPAZIATURA) * k
    return im


def _atlante(k):
    """Atlas page and glyph rectangles {ch: (x, y, w, h)} at scale k, glyphs in INSIEME order."""
    passo = CELLA * k + MARGINE_ATLANTE
    posti, x, y = {}, MARGINE_ATLANTE, MARGINE_ATLANTE
    for ch in INSIEME:
        w = larghezza(ch) * k
        if x + w + MARGINE_ATLANTE > ATLANTE_LARGHEZZA:
            x, y = MARGINE_ATLANTE, y + passo
        posti[ch] = (x, y, w, CELLA * k)
        x += w + MARGINE_ATLANTE
    im = Image.new("RGBA", (ATLANTE_LARGHEZZA, y + passo), (0, 0, 0, 0))
    for ch, (gx, gy, _, _) in posti.items():
        im.alpha_composite(glifo(ch, k), (gx, gy))
    return im, posti


def fnt(nome_file, k):
    """(BMFont text descriptor, atlas image) of the font at scale k; the page is `<nome_file>.png`."""
    im, posti = _atlante(k)
    righe = [
        f'info face="{NOME}" size={8 * k} bold=0 italic=0 charset="" unicode=1 stretchH=100 smooth=0 aa=1 '
        f"padding=0,0,0,0 spacing={MARGINE_ATLANTE},{MARGINE_ATLANTE} outline=0",
        f"common lineHeight={(CELLA + INTERLINEA) * k} base={(ACCENTO_RIGHE + 7) * k} scaleW={im.width} "
        f"scaleH={im.height} pages=1 packed=0 alphaChnl=0 redChnl=4 greenChnl=4 blueChnl=4",
        f'page id=0 file="{nome_file}.png"',
        f"chars count={len(posti)}",
    ]
    for ch in INSIEME:
        x, y, w, h = posti[ch]
        righe.append(f"char id={ord(ch)} x={x} y={y} width={w} height={h} xoffset=0 yoffset=0 "
                     f"xadvance={(larghezza(ch) + SPAZIATURA) * k} page=0 chnl=15")
    righe.append("kernings count=0")
    return "\n".join(righe) + "\n", im


def file_carattere():
    """Every exported font file: {path relative to godot/assets: bytes}."""
    out = {}
    for misura, k in MISURE.items():
        nome = f"carattere-{misura}"
        testo, im = fnt(nome, k)
        buf = io.BytesIO()
        im.save(buf, format="PNG", optimize=False)  # as genera.salva: no metadata, deterministic
        out[f"ui/{nome}.png"] = buf.getvalue()
        out[f"ui/{nome}.fnt"] = testo.encode("utf-8")
    return out
