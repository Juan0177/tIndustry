"""The scroll-driven story of the site (prototype: chapter 1, «dal giacimento al CORE»).

One factory on a map of its own, drawn on a fixed full-screen canvas by racconto.js: scrolling moves the camera
and plays the construction forwards and backwards (scrub). This module holds the data and writes the assets:

- SCENA: the story map as fabbrica elements (cells), checked by the same connectivity walker as the landing page;
- linea_del_tempo(): when each block is built (scroll progress 0..1, in chain order), where the drone hovers
  while it builds, the camera key frames and the path the items follow;
- esporta(): into <out>/racconto/ the terrain (with the deposits), the sprite strips at art size, the ghosts of the
  plans (the construct shader of the client: dark silhouette, amber outline), the drone in 2.5D (stacked slices
  enlarged x4, as DroneVisual draws its sprite, and its shadow) and a still poster for pages without the script.

Everything is derived from the game's sprites (godot/assets, via composti) and is deterministic: two builds write
the same bytes.
"""
import io
import json
import os
import random

from PIL import Image

import composti
import fabbrica
from fabbrica import T1, Blocco, Giacimento, Pezzo, Scena, Tratto

CELLA = composti.T  # art px per cell (32); the canvas scales the art, like the game draws it x2
LARGHEZZA, RIGHE = 40, 24
AMBRA = (0xFF, 0xD3, 0x7F)  # UiColors.Costruzione = Accento (Mindustry Pal.accent)
FETTE = 7  # slices of the drone stack: 6 extruded walls and the sprite on top
INGRANDIMENTO = 4  # DroneVisual.SharpFactor: enlarged without filtering, drawn smaller with filtering

# The miner sits on an iron deposit; its ore leaves from the east side through a corner (never straight out of a
# miner), runs down, turns east and heads for the smelter of chapter 2, still only a plan.
SCENA = Scena("racconto-1", RIGHE, "prati", [
    Giacimento(9, 9, 3, 4, "iron-ore", 11),
    Blocco(10, 10, "miner", "Al lavoro"),
    Pezzo(12, 11, "curva", T1, da="E", a="S"),
    Tratto(12, 12, 3, "S", T1, ("iron-ore",)),
    Pezzo(12, 15, "curva", T1, da="S", a="E"),
    Tratto(13, 15, 12, "E", T1, ("iron-ore",)),
    Blocco(25, 14, "smelter", "Fermo"),
], seme=7, larghezza=LARGHEZZA)

# Scroll progress of each moment of chapter 1 (five panels, one screen each: 0, .25, .5, .75, 1).
INGRESSO_DRONE = 0.08   # the drone flies in from the top right
MINATORE = (0.20, 0.34)  # the miner site rises
NASTRI = (0.42, 0.86)    # one belt cell after the other
PIANO_FORNO = 0.80       # the plan of the smelter shows up (chapter 2 teaser)
CAMERA = [  # centre in cells, `vista`: cells across a 16:9 screen
    {"t": 0.00, "x": 10.6, "y": 10.6, "vista": 9},
    {"t": 0.16, "x": 11.0, "y": 10.8, "vista": 10},
    {"t": 0.38, "x": 11.8, "y": 11.8, "vista": 12},
    {"t": 0.58, "x": 14.0, "y": 13.4, "vista": 14},
    {"t": 0.86, "x": 19.0, "y": 14.0, "vista": 18},
    {"t": 1.00, "x": 19.5, "y": 13.6, "vista": 22},
]


def _celle_della_catena():
    """The belt cells from the miner to the smelter, in flow order, with their element."""
    catena = next(c for c in fabbrica.catene(SCENA) if c["sorgente"] == ("blocco", "miner"))
    occupato = fabbrica._occupazione(SCENA)
    return [(cella, occupato[cella][0]) for cella in catena["celle"]], catena["fine"]


def _verso_della_cella(e):
    return e.verso if isinstance(e, Tratto) else e.a


def linea_del_tempo():
    """The data racconto.js plays: map size, camera key frames, constructions in order, the item path."""
    celle, fine = _celle_della_catena()
    costruzioni = []
    miner = next(e for e in SCENA.elementi if isinstance(e, Blocco) and e.id == "miner")
    costruzioni.append({"id": "minatore", "sprite": "minatore", "x": miner.x, "y": miner.y, "w": 2, "h": 2,
                        "rot": 0, "specchio": False, "t0": MINATORE[0], "t1": MINATORE[1],
                        "piano": INGRESSO_DRONE, "nastro": False})
    passo = (NASTRI[1] - NASTRI[0]) / len(celle)
    precedente = MINATORE[1]
    for k, (cella, e) in enumerate(celle):
        t0 = NASTRI[0] + k * passo
        if isinstance(e, Pezzo):  # a corner
            specchio, rot = fabbrica.trasformazione_curva(e.da, e.a)
            sprite = "curva"
        else:
            specchio, rot = False, fabbrica.ROT[e.verso]
            sprite = "nastro"
        costruzioni.append({"id": f"nastro-{k}", "sprite": sprite, "x": cella[0], "y": cella[1], "w": 1, "h": 1,
                            "rot": rot, "specchio": specchio, "t0": round(t0, 4), "t1": round(t0 + passo * 0.85, 4),
                            "piano": round(precedente, 4), "nastro": True})
        precedente = t0
    forno = next(e for e in SCENA.elementi if isinstance(e, Blocco) and e.id == "smelter")
    # The item path: from the miner's edge through every belt cell centre to the smelter's edge, in cells.
    primo, ultimo = celle[0][0], celle[-1][0]
    v0 = celle[0][1].da
    dx, dy = fabbrica.VERSI[v0]
    percorso = [[primo[0] + 0.5 - dx * 0.5, primo[1] + 0.5 - dy * 0.5]]
    percorso += [[c[0] + 0.5, c[1] + 0.5] for c, _ in celle]
    dx, dy = fabbrica.VERSI[_verso_della_cella(celle[-1][1])]
    percorso.append([ultimo[0] + 0.5 + dx * 0.5, ultimo[1] + 0.5 + dy * 0.5])
    return {
        "cella": CELLA, "larghezza": LARGHEZZA, "righe": RIGHE,
        "camera": CAMERA,
        "costruzioni": costruzioni,
        "piano": {"sprite": "forno", "x": forno.x, "y": forno.y, "w": 2, "h": 2, "t": PIANO_FORNO},
        "fine": list(fine),
        "percorso": percorso,
        "velocita": fabbrica.RATE[T1],  # cells per second on a T1 belt
        "drone": {"ingresso": INGRESSO_DRONE, "partenza": [LARGHEZZA * 0.62, 2.0], "fette": FETTE,
                  "ingrandimento": INGRANDIMENTO},
    }


# ---------------------------------------------------------------- images

def _png(im):
    buf = io.BytesIO()
    im.save(buf, "PNG", optimize=True)
    return buf.getvalue()


def fantasma(im):
    """The plan look of the client's construct shader at progress 0: every opaque pixel becomes a dark see-through
    silhouette, except the outline (an opaque pixel next to a transparent one), drawn in amber."""
    im = im.convert("RGBA")
    src = im.load()
    out = Image.new("RGBA", im.size)
    dst = out.load()
    w, h = im.size

    def vuoto(x, y):
        return not (0 <= x < w and 0 <= y < h) or src[x, y][3] < 26

    for y in range(h):
        for x in range(w):
            r, g, b, a = src[x, y]
            if a < 26:
                continue
            if vuoto(x + 1, y) or vuoto(x - 1, y) or vuoto(x, y + 1) or vuoto(x, y - 1):
                dst[x, y] = AMBRA + (255,)
            else:
                dst[x, y] = (int(r * 0.15), int(g * 0.15), int(b * 0.15), int(a * 0.25))
    return out


def striscia(fotogrammi):
    """Frames side by side, left to right."""
    w, h = fotogrammi[0].size
    out = Image.new("RGBA", (w * len(fotogrammi), h))
    for k, f in enumerate(fotogrammi):
        out.paste(f.convert("RGBA"), (k * w, 0))
    return out


def fette_drone(drone):
    """The drone in 2.5D: FETTE layers of the same footprint, the lower ones the sprite darkened step by step (its
    walls), the top one the sprite itself, enlarged x INGRANDIMENTO without filtering."""
    drone = drone.convert("RGBA")
    livelli = []
    for k in range(FETTE):
        if k == FETTE - 1:
            livello = drone.copy()
        else:
            f = 0.32 + 0.38 * k / (FETTE - 2)
            px = drone.load()
            livello = Image.new("RGBA", drone.size)
            dst = livello.load()
            for y in range(drone.height):
                for x in range(drone.width):
                    r, g, b, a = px[x, y]
                    if a:
                        dst[x, y] = (int(r * f), int(g * f), int(b * f), a)
        livelli.append(livello.resize((drone.width * INGRANDIMENTO, drone.height * INGRANDIMENTO), Image.NEAREST))
    return striscia(livelli)


def ombra_drone(drone):
    """DroneVisual's shadow: the same sprite in black at 35 %."""
    drone = drone.convert("RGBA")
    a = drone.getchannel("A").point(lambda v: int(v * 0.35))
    out = Image.new("RGBA", drone.size, (0, 0, 0, 0))
    out.putalpha(a)
    return out.resize((drone.width * INGRANDIMENTO, drone.height * INGRANDIMENTO), Image.NEAREST)


def terreno(immagini):
    """The ground of the whole map at art size: the biome's floors (as fabbrica's terrain windows) and the deposits."""
    from blocchi import terreno as tr
    suoli = [f["id"] for f in tr.TERRENO["floors"] if f["biome"] == SCENA.bioma]
    rnd = random.Random(SCENA.seme)
    im = Image.new("RGBA", (LARGHEZZA * CELLA, RIGHE * CELLA))
    for j in range(RIGHE):
        for i in range(LARGHEZZA):
            s = suoli[0] if rnd.random() < 0.88 else rnd.choice(suoli)  # calmer than the landing windows
            im.paste(immagini(f"terreno-{s}-{rnd.randrange(4)}"), (i * CELLA, j * CELLA))
    for e in SCENA.elementi:
        if isinstance(e, Giacimento):
            r = random.Random(e.seme)
            for j in range(e.h):
                for i in range(e.w):
                    tessera = immagini(f"giacimento-{e.minerale}-{r.randrange(2)}").convert("RGBA")
                    im.alpha_composite(tessera, ((e.x + i) * CELLA, (e.y + j) * CELLA))
    return im


def _stato(bid, nome, immagini):
    b = fabbrica._blocco(bid)
    s = next(x for x in b.stati if x.nome == nome)
    return [(fabbrica._meta(f), d) for f, d in composti.fotogrammi_stato(b, s, immagini)]


def sprite_del_racconto(immagini):
    """{name: (image, meta)}: every picture racconto.js draws, at art size (the drone x INGRANDIMENTO)."""
    nastro = immagini.fotogrammi(T1 + "-anim")
    curva = immagini.fotogrammi(T1 + "-corner-anim")
    lavoro = _stato("miner", "Al lavoro", immagini)
    fermo = _stato("miner", "Fermo", immagini)[0][0]
    forno = _stato("smelter", "Fermo", immagini)[0][0]
    drone = immagini("drone-1")
    fps = fabbrica.RATE[T1] * len(nastro)  # one cell of tread per 1 / rate seconds, as in the game
    return {
        "nastro": (striscia(nastro), {"fotogrammi": len(nastro), "fps": fps}),
        "curva": (striscia(curva), {"fotogrammi": len(curva), "fps": fps}),
        "minatore": (striscia([f for f, _ in lavoro]), {"fotogrammi": len(lavoro), "durate": [d for _, d in lavoro]}),
        "minatore-fermo": (fermo, {"fotogrammi": 1}),
        "minatore-piano": (fantasma(fermo), {"fotogrammi": 1}),
        "nastro-piano": (fantasma(nastro[0]), {"fotogrammi": 1}),
        "curva-piano": (fantasma(curva[0]), {"fotogrammi": 1}),
        "forno-piano": (fantasma(forno), {"fotogrammi": 1}),
        "oggetto": (immagini("iron-ore-belt"), {"fotogrammi": 1}),
        "drone": (fette_drone(drone), {"fotogrammi": FETTE, "lato": drone.width}),
        "drone-ombra": (ombra_drone(drone), {"fotogrammi": 1, "lato": drone.width}),
    }


def poster(immagini, sprite, dati):
    """The last moment of the chapter as one still picture (pages without the script, link previews): the built
    chain, the working miner, the smelter's plan and the drone, x2 as in the game, cropped around the chain."""
    im = terreno(immagini)

    def metti(nome, x, y, rot=0, specchio=False, fotogramma=0):
        foglio, meta = sprite[nome]
        w = foglio.width // meta["fotogrammi"]
        f = foglio.crop((fotogramma * w, 0, (fotogramma + 1) * w, foglio.height))
        if specchio:
            f = f.transpose(Image.FLIP_LEFT_RIGHT)
        if rot:
            f = f.rotate(-rot, expand=True)
        im.alpha_composite(f, (int(x * CELLA), int(y * CELLA)))

    for c in dati["costruzioni"]:
        metti("minatore" if c["sprite"] == "minatore" else c["sprite"], c["x"], c["y"], c["rot"], c["specchio"])
    p = dati["piano"]
    metti("forno-piano", p["x"], p["y"])
    ombra, meta = sprite["drone-ombra"]
    fette, _ = sprite["drone"]
    lato = meta["lato"]
    dx, dy = 23.4 * CELLA, 13.6 * CELLA
    piccola = ombra.resize((lato, lato), Image.LANCZOS)
    im.alpha_composite(piccola, (int(dx - lato / 2 + 4), int(dy - lato / 2 + 6)))
    passo = fette.width // FETTE
    for k in range(FETTE):
        f = fette.crop((k * passo, 0, (k + 1) * passo, fette.height)).resize((lato, lato), Image.LANCZOS)
        im.alpha_composite(f, (int(dx - lato / 2), int(dy - lato / 2 - k - 4)))
    x0, y0, x1, y1 = 6 * CELLA, 7 * CELLA, 31 * CELLA, 19 * CELLA
    ritaglio = im.crop((x0, y0, x1, y1))
    return ritaglio.resize((ritaglio.width * 2, ritaglio.height * 2), Image.NEAREST)


def esporta(out_dir, immagini):
    """Write <out>/racconto/{terreno,poster,<sprite>}.png; returns the data for the page (timeline + sprite list)."""
    cartella = os.path.join(out_dir, "racconto")
    os.makedirs(cartella, exist_ok=True)
    dati = linea_del_tempo()
    sprite = sprite_del_racconto(immagini)

    def scrivi(nome, im):
        with open(os.path.join(cartella, nome + ".png"), "wb") as fh:
            fh.write(_png(im))
        return nome + ".png"

    dati["terreno"] = scrivi("terreno", terreno(immagini))
    dati["sprite"] = {}
    for nome, (im, meta) in sorted(sprite.items()):
        dati["sprite"][nome] = dict(meta, file=scrivi(nome, im), w=im.width // meta["fotogrammi"], h=im.height)
    dati["poster"] = scrivi("poster", poster(immagini, sprite, dati))
    return dati


def json_della_pagina(dati):
    """The timeline as JSON safe inside <script type="application/json">."""
    return json.dumps(dati, ensure_ascii=False, separators=(",", ":"), sort_keys=True).replace("</", "<\\/")
