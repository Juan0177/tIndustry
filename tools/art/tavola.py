"""Review sheet: every sprite old next to new, at 1x and 2x, on a light and a dark background.

One row per sprite:  name | light: old 1x, old 2x, new 1x, new 2x | dark: same four.
A missing old sprite (a brand new name) leaves an empty, crossed slot.

The terrain sheet (tavola_terreno) composes a 12x12-cell window per land biome from the terrain
sprites the way the client draws the map: variants, edges, walls, water, deposits, decorations.
"""
import os

from PIL import Image, ImageDraw, ImageFont

CHIARO = (214, 210, 200, 255)
SCURO = (36, 38, 42, 255)
FONDO = (60, 60, 64, 255)
TESTO = (235, 235, 235, 255)
MARGINE = 6
ETICHETTA = 170


def _apri(cartella, nome):
    path = os.path.join(cartella, nome + ".png")
    if not os.path.exists(path):
        return None
    with Image.open(path) as im:
        return im.convert("RGBA")


def _riquadro(im, fondo, k):
    """Sprite scaled by k (nearest) on a solid background; a crossed box when missing."""
    if im is None:
        box = Image.new("RGBA", (32 * k, 32 * k), fondo)
        d = ImageDraw.Draw(box)
        d.line((0, 0, box.width - 1, box.height - 1), fill=(200, 60, 60, 255))
        d.line((0, box.height - 1, box.width - 1, 0), fill=(200, 60, 60, 255))
        return box
    box = Image.new("RGBA", (im.width * k, im.height * k), fondo)
    box.alpha_composite(im.resize(box.size, Image.NEAREST))
    return box


def tavola(nomi, vecchi_dir, nuovi_dir):
    """Old/new comparison sheet of the given sprites."""
    righe = []
    for nome in nomi:
        vecchio, nuovo = _apri(vecchi_dir, nome), _apri(nuovi_dir, nome)
        riquadri = [_riquadro(im, fondo, k)
                    for fondo in (CHIARO, SCURO)
                    for im in (vecchio, nuovo)
                    for k in (1, 2)]
        righe.append((nome, riquadri))

    larghezza = max(ETICHETTA + sum(r.width + MARGINE for r in rq) for _, rq in righe) + MARGINE
    altezza = sum(max(r.height for r in rq) + MARGINE for _, rq in righe) + MARGINE
    out = Image.new("RGBA", (larghezza, altezza), FONDO)
    disegno = ImageDraw.Draw(out)
    font = ImageFont.load_default()
    y = MARGINE
    for nome, riquadri in righe:
        disegno.text((MARGINE, y), nome, fill=TESTO, font=font)
        x = ETICHETTA
        for r in riquadri:
            out.alpha_composite(r, (x, y))
            x += r.width + MARGINE
        y += max(r.height for r in riquadri) + MARGINE
    return out


# --- terrain sheet (G2b) ----------------------------------------------------------------------

# 12x12 window drawn for every land biome. A-D = the biome's floors in content order (cycled when
# the biome has fewer), w/W = shallow/deep water, # = the biome's wall (on the floor of the nearest
# cell to its left), o/i = deposits of the biome's two ores (on floor A).
FINESTRA = (
    "##AAAABBBBBB",
    "#AAAAABBBBBB",
    "AAAoAAABBBCC",
    "AAoooABB##CC",
    "AAAoAAB###CC",
    "AAAAAAAB#CCC",
    "wwAAAAAABCCC",
    "WwwAAA#AACCD",
    "WWwwAAAAAADD",
    "WWWwwAAiiiDD",
    "WWWWwwAAiiDD",
    "WWWWWwwAADDD",
)
VICINI = {"N": (0, -1), "E": (1, 0), "S": (0, 1), "O": (-1, 0),
          "NE": (1, -1), "SE": (1, 1), "SO": (-1, 1), "NO": (-1, -1)}


def _finestra(b, bioma, sprite):
    """One biome window composed like the client will: floor variant, edges of higher-priority
    neighbours (lowest first), deposit, decoration, then the dual-grid walls half a cell off."""
    from blocchi import terreno as tr

    T = tr.T
    suoli = [f["id"] for f in tr.TERRENO["floors"] if f["biome"] == bioma]
    parete = next(w["id"] for w in tr.TERRENO["walls"] if w["biome"] == bioma)
    acque = list(tr.ACQUA)  # shallow, deep
    minerali = [tr.MINERALI[(2 * b + k) % len(tr.MINERALI)] for k in range(2)]
    n = len(FINESTRA)
    ox = 100 * b  # each window samples its own map cells

    def cella(x, y):
        x, y = min(max(x, 0), n - 1), min(max(y, 0), n - 1)
        c = FINESTRA[y][x]
        if c in "ABCD":
            return suoli["ABCD".index(c) % len(suoli)], False, None
        if c in "wW":
            return acque["wW".index(c)], False, None
        if c == "#":
            vicino = FINESTRA[y][:x].rstrip("#")[-1:] or FINESTRA[y][x:].lstrip("#")[:1]
            return cella(FINESTRA[y].index(vicino), y)[0], True, None
        return suoli[0], False, minerali["oi".index(c)]

    strisce = {}

    def pezzo(nome, i):
        if nome not in strisce:
            strisce[nome] = sprite(nome)
        return strisce[nome].crop((i * T, 0, (i + 1) * T, T))

    im = Image.new("RGBA", ((n + 1) * T, (n + 1) * T), (0, 0, 0, 255))
    m = T // 2  # margin: wall pieces start half a cell before the first cell
    for y in range(n):
        for x in range(n):
            suolo, muro, minerale = cella(x, y)
            px, py = m + x * T, m + y * T
            if suolo in tr.ACQUA:
                base = pezzo(tr.ACQUA[suolo], 0)
            else:
                base = sprite(f"terreno-{suolo}-{tr.hash_cella(x + ox, y, tr.SALE_VARIANTE) % tr.VARIANTI}")
            im.alpha_composite(base, (px, py))
            sopra = sorted((tr.PRIORITA[v], i, v) for i, (dx, dy) in enumerate(VICINI.values())
                           if tr.PRIORITA[v := cella(x + dx, y + dy)[0]] > tr.PRIORITA[suolo])
            for _, i, v in sopra:
                im.alpha_composite(pezzo(f"terreno-{v}-bordo", i), (px, py))
            if minerale:
                variante = tr.hash_cella(x + ox, y, tr.SALE_GIACIMENTO) % tr.GIACIMENTI
                im.alpha_composite(sprite(f"giacimento-{minerale}-{variante}"), (px, py))
            elif not muro and suolo not in tr.ACQUA and \
                    tr.hash_cella(x + ox, y, tr.SALE_DECO) % 100 < tr.DENSITA_DECO.get(bioma, 10):
                k = tr.hash_cella(x + ox, y, tr.SALE_DECO_TIPO) % tr.DECORAZIONI
                im.alpha_composite(sprite(f"deco-{bioma}-{k}"), (px, py))
    for y in range(n + 1):
        for x in range(n + 1):
            angoli = sum(bit for bit, (cx, cy) in ((tr.NO, (x - 1, y - 1)), (tr.NE, (x, y - 1)),
                                                     (tr.SE, (x, y)), (tr.SO, (x - 1, y)))
                         if 0 <= cx < n and 0 <= cy < n and FINESTRA[cy][cx] == "#")
            if angoli:
                im.alpha_composite(pezzo(f"parete-{parete}", angoli), (x * T, y * T))
    return im.crop((m, m, m + n * T, m + n * T))


def tavola_terreno(sprite, scala=2):
    """One 12x12-cell window per land biome, at the in-game scale (x2 Nearest), two per row."""
    from blocchi import terreno as tr

    finestre = [(bioma, _finestra(b, bioma, sprite)) for b, bioma in enumerate(tr.BIOMI)]
    lato = finestre[0][1].width * scala
    colonne = 2
    righe = (len(finestre) + colonne - 1) // colonne
    titolo = 14
    out = Image.new("RGBA", (MARGINE + colonne * (lato + MARGINE), MARGINE + righe * (titolo + lato + MARGINE)), FONDO)
    disegno = ImageDraw.Draw(out)
    font = ImageFont.load_default()
    for k, (bioma, im) in enumerate(finestre):
        x = MARGINE + (k % colonne) * (lato + MARGINE)
        y = MARGINE + (k // colonne) * (titolo + lato + MARGINE)
        disegno.text((x, y), bioma, fill=TESTO, font=font)
        out.alpha_composite(im.resize((lato, lato), Image.NEAREST), (x, y + titolo))
    return out
