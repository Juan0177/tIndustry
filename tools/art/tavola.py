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
def pezzi_mappa(celle, ox=0, oy=0):
    """Sprite pieces of a grid of cells in drawing order (rule in tools/art/README.md), as
    [(sprite name, piece index in its strip, px, py)]; (px, py) is the top-left pixel of the piece
    with celle[0][0] at (0, 0) (wall pieces start half a cell up and left of their cell).
    celle[y][x] = (floor id, wall id or None, ore item id or None); (ox, oy) is the map position
    of celle[0][0], used by hash_cella. A neighbour outside the grid counts as the cell itself.
    The client (TerrainLook.Plan in C#) must give the same list: see fixture_terreno.py."""
    from blocchi import terreno as tr

    T = tr.T
    h, w = len(celle), len(celle[0])
    pezzi = []
    for y in range(h):
        for x in range(w):
            suolo, parete, minerale = celle[y][x]
            bioma = tr.BIOMA[suolo]
            mx, my = ox + x, oy + y
            px, py = x * T, y * T
            # Water too: its still variant is the bed; the client animates ripples over it.
            pezzi.append((f"terreno-{suolo}-{tr.hash_cella(mx, my, tr.SALE_VARIANTE) % tr.VARIANTI}", 0, px, py))
            vicini = {lato: celle[y + dy][x + dx][0] if 0 <= x + dx < w and 0 <= y + dy < h else suolo
                      for lato, (dx, dy) in tr.VICINI.items()}
            for altro, i in tr.pezzi_bordo(suolo, vicini):
                pezzi.append((f"terreno-{altro}-bordo", i, px, py))
            if minerale:
                variante = tr.hash_cella(mx, my, tr.SALE_GIACIMENTO) % tr.GIACIMENTI
                pezzi.append((f"giacimento-{minerale}-{variante}", 0, px, py))
            elif (parete is None and suolo not in tr.ACQUA and bioma in tr.DENSITA_DECO
                  and tr.hash_cella(mx, my, tr.SALE_DECO) % 100 < tr.DENSITA_DECO[bioma]):
                k = tr.hash_cella(mx, my, tr.SALE_DECO_TIPO) % tr.DECORAZIONI
                pezzi.append((f"deco-{bioma}-{k}", 0, px, py))
    pareti = sorted({c[1] for riga in celle for c in riga if c[1]})
    for parete in pareti:
        for y in range(h + 1):
            for x in range(w + 1):
                angoli = sum(bit for bit, (cx, cy) in ((tr.NO, (x - 1, y - 1)), (tr.NE, (x, y - 1)),
                                                         (tr.SE, (x, y)), (tr.SO, (x - 1, y)))
                             if 0 <= cx < w and 0 <= cy < h and celle[cy][cx][1] == parete)
                if angoli:
                    pezzi.append((f"parete-{parete}", angoli, x * T - T // 2, y * T - T // 2))
    return pezzi


def componi(celle, sprite, ox=0, oy=0):
    """Compose a grid of cells like the client: the pieces of pezzi_mappa, blitted in order."""
    from blocchi import terreno as tr

    T = tr.T
    h, w = len(celle), len(celle[0])
    strisce = {}

    def pezzo(nome, i):
        if nome not in strisce:
            strisce[nome] = sprite(nome)
        return strisce[nome].crop((i * T, 0, (i + 1) * T, T))

    m = T // 2  # margin: wall pieces start half a cell before the first cell
    im = Image.new("RGBA", ((w + 1) * T, (h + 1) * T), (0, 0, 0, 255))
    for nome, i, px, py in pezzi_mappa(celle, ox, oy):
        im.alpha_composite(pezzo(nome, i), (m + px, m + py))
    return im.crop((m, m, m + w * T, m + h * T))


def _finestra(b, bioma, sprite):
    """One biome window of FINESTRA, composed like the client."""
    from blocchi import terreno as tr

    suoli = [f["id"] for f in tr.TERRENO["floors"] if f["biome"] == bioma]
    parete = next(w["id"] for w in tr.TERRENO["walls"] if w["biome"] == bioma)
    acque = sorted(tr.ACQUA, key=lambda s: tr.ACQUA[s])  # shallow (bassa), deep (profonda)
    minerali = [tr.MINERALI[(2 * b + k) % len(tr.MINERALI)] for k in range(2)]

    def cella(x, y):
        c = FINESTRA[y][x]
        if c in "ABCD":
            return suoli["ABCD".index(c) % len(suoli)], None, None
        if c in "wW":
            return acque["wW".index(c)], None, None
        if c == "#":  # on the floor of the nearest cell to its left (or right)
            vicino = FINESTRA[y][:x].rstrip("#")[-1:] or FINESTRA[y][x:].lstrip("#")[:1]
            return cella(FINESTRA[y].index(vicino), y)[0], parete, None
        return suoli[0], None, minerali["oi".index(c)]

    celle = [[cella(x, y) for x in range(len(FINESTRA[0]))] for y in range(len(FINESTRA))]
    return componi(celle, sprite, ox=100 * b)


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


# --- interface sheet (G4) ---------------------------------------------------------------------

PANNELLO = (20, 22, 26, 255)  # #14161A, the panel colour of the interface theme
BORDO = (255, 211, 127, 255)  # #FFD37F, the accent of the project (palette.ACCENTO[3])
FRASI = (
    "Il nastro porta 12 lingotti di rame al Core: è già più veloce!",
    "Perché la città è così lontana? Più energia, più fumo, più caffè…",
    "ÈCCO À PIÙ ÉPOCHE: «Ricerca» · Mercato · Campagna · 3×4 = 12 €",
    "The quick brown fox jumps over the lazy dog: 0123456789 (100%)",
)


def _pannello(w, h):
    """Interface panel: near-black fill, square corners, 2 px accent border."""
    im = Image.new("RGBA", (w, h), BORDO)
    im.paste(PANNELLO, (2, 2, w - 2, h - 2))
    return im


def tavola_ui(sprite):
    """Interface review sheet: the pixel font at x2 and x3 (Italian and English sample sentences, the
    whole glyph set), a reference line in Pillow's bundled OFL font (Aileron) at the same height, and
    every `ui-*` icon at 16 px, 32 px and 32 px shown x2, all on a panel of the interface theme."""
    import carattere
    from blocchi.interfaccia import ICONE

    insieme = carattere.INSIEME
    meta = len(insieme) // 2
    righe = [carattere.scrivi("Carattere pixel e icone dell'interfaccia (G4)", 3, BORDO)]
    righe += [carattere.scrivi(f, 2) for f in FRASI]
    righe += [carattere.scrivi(insieme[:meta], 2), carattere.scrivi(insieme[meta:], 2)]
    righe += [carattere.scrivi(f, 3) for f in FRASI[:2] + FRASI[3:]]
    riferimento = Image.new("RGBA", (1400, 40), (0, 0, 0, 0))
    ImageDraw.Draw(riferimento).text((0, 0), "Aileron (OFL, riferimento): " + FRASI[0],
                                     fill=(170, 170, 170, 255), font=ImageFont.load_default(size=18))
    righe.append(riferimento.crop(riferimento.getbbox()))

    icone = []
    for nome in ICONE:
        piccola, grande = sprite(f"ui/ui-{nome}-16"), sprite(f"ui/ui-{nome}")
        doppia = grande.resize((64, 64), Image.NEAREST)
        etichetta = carattere.scrivi(nome, 2)
        w = max(16 + 32 + 64 + 2 * MARGINE, etichetta.width)
        cella = Image.new("RGBA", (w, 64 + MARGINE + etichetta.height), (0, 0, 0, 0))
        cella.alpha_composite(piccola, (0, 64 - 16))
        cella.alpha_composite(grande, (16 + MARGINE, 64 - 32))
        cella.alpha_composite(doppia, (16 + 32 + 2 * MARGINE, 0))
        cella.alpha_composite(etichetta, (0, 64 + MARGINE))
        icone.append(cella)
    per_riga = 5
    file_icone = [icone[i:i + per_riga] for i in range(0, len(icone), per_riga)]

    bordo = 4 * MARGINE
    larghezza = max([r.width for r in righe] + [sum(c.width + bordo for c in f) for f in file_icone]) + 2 * bordo
    altezza = (sum(r.height + 2 * MARGINE for r in righe)
               + sum(max(c.height for c in f) + bordo for f in file_icone) + 3 * bordo)
    out = _pannello(larghezza, altezza)
    y = bordo
    for r in righe:
        out.alpha_composite(r, (bordo, y))
        y += r.height + 2 * MARGINE
    y += bordo
    for f in file_icone:
        x = bordo
        for c in f:
            out.alpha_composite(c, (x, y))
            x += c.width + bordo
        y += max(c.height for c in f) + bordo
    return out
