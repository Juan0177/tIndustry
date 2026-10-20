"""The scroll-driven story of the site (prototype: chapters 1-4 of «dal giacimento al CORE», up to the attack).

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
from fabbrica import T1, Blocco, Giacimento, Nodo, Pezzo, Scena, Tratto

CELLA = composti.T  # art px per cell (32); the canvas scales the art, like the game draws it x2
LARGHEZZA, RIGHE = 40, 24
AMBRA = (0xFF, 0xD3, 0x7F)  # UiColors.Costruzione = Accento (Mindustry Pal.accent)
FETTE = 6  # slices of the drone stack, one art px apart (modello_drone gives each part its own heights)
INGRANDIMENTO = 4  # DroneVisual.SharpFactor: enlarged without filtering, drawn smaller with filtering

# The story map, built chapter by chapter. Iron: a miner on its deposit, the ore leaving through a corner (never
# straight out of a miner), down and east into the smelter. Coal: a second miner on the deposit next to it feeds
# the smelter from the east and, from its top side, the generator. The plates leave the smelter eastwards and run
# down out of the map, towards the chapters to come. Power: generator, three nodes, four lamps.
SCENA = Scena("racconto", RIGHE, "prati", [
    Giacimento(9, 9, 3, 4, "iron-ore", 11),
    Blocco(10, 10, "miner", "Al lavoro"),
    Pezzo(12, 11, "curva", T1, da="E", a="S"),
    Tratto(12, 12, 3, "S", T1, ("iron-ore",)),
    Pezzo(12, 15, "curva", T1, da="S", a="E"),
    Tratto(13, 15, 12, "E", T1, ("iron-ore",)),
    Blocco(25, 14, "smelter", "Al lavoro"),
    Giacimento(24, 8, 3, 4, "coal", 5),
    Blocco(25, 9, "miner", "Al lavoro"),
    Pezzo(27, 10, "curva", T1, da="E", a="S"),
    Tratto(27, 11, 3, "S", T1, ("coal",)),
    Pezzo(27, 14, "curva", T1, da="S", a="W"),
    Tratto(27, 15, 3, "E", T1, ("iron-plate",)),
    Pezzo(30, 15, "curva", T1, da="E", a="S"),
    Tratto(30, 16, 8, "S", T1, ("iron-plate",)),
    Pezzo(26, 8, "curva", T1, da="N", a="E"),
    Tratto(27, 8, 4, "E", T1, ("coal",)),
    Blocco(31, 7, "generator", "In funzione"),
    Nodo(28, 11), Nodo(19, 12), Nodo(33, 15),
    Blocco(23, 12, "lamp", "Accesa"), Blocco(14, 12, "lamp", "Accesa"),
    Blocco(35, 12, "lamp", "Accesa"), Blocco(33, 19, "lamp", "Accesa"),
], seme=7, larghezza=LARGHEZZA)

# Pictures of each block: (working, stopped, plan) sprite names of sprite_del_racconto().
STATI = {"miner": ("minatore", "minatore-fermo", "minatore-piano"),
         "smelter": ("forno", "forno-fermo", "forno-piano"),
         "generator": ("generatore", "generatore-fermo", "generatore-piano"),
         "lamp": ("lampada", "lampada-fermo", "lampada-piano"),
         "power-node": ("nodo", "nodo", "nodo-piano")}

# Scroll progress (0..1) of the story: eight panels, one screen each, at 0, 1/7, ... 1. The drone builds in this
# order; each phase spreads its pieces evenly over its window.
INGRESSO_DRONE = 0.04
FASI = [  # (what, from, to)
    ("minatore-ferro", 0.06, 0.13),
    ("nastri-ferro", 0.16, 0.33),
    ("forno", 0.36, 0.41),
    ("minatore-carbone", 0.43, 0.47),
    ("nastri-carbone", 0.48, 0.53),
    ("nastri-lastre", 0.55, 0.65),
    ("generatore", 0.67, 0.71),
    ("nastri-generatore", 0.72, 0.76),
    ("nodi", 0.77, 0.81),
    ("lampade", 0.81, 0.86),
]
NOTTE = {"t0": 0.74, "t1": 0.88, "colore": [0.34, 0.38, 0.54]}  # NightLayer.Darkness, the world multiplied by it
LUCE_LAMPADA = {"raggio": 6.5, "colore": [1.0, 0.9, 0.68], "energia": 1.4}  # NightLayer.LampLight
LUCE_FORNO = {"raggio": 3.0, "colore": [1.0, 0.55, 0.25], "energia": 1.2}   # NightLayer.SmelterLight
ALLARME = 0.9  # chapter 5 teaser: enemy silhouettes at the edge of the map, a red glow at the screen's edges
NEMICI = [{"sprite": "nemico-2", "x": 39.0, "y": 7.0}, {"sprite": "nemico-3", "x": 39.6, "y": 13.0},
          {"sprite": "nemico-2", "x": 38.6, "y": 19.0}]
CAMERA = [  # centre in cells, `vista`: cells across a 16:9 screen
    {"t": 0.00, "x": 10.6, "y": 10.6, "vista": 9},
    {"t": 0.10, "x": 11.0, "y": 10.8, "vista": 10},
    {"t": 0.18, "x": 11.8, "y": 11.8, "vista": 12},
    {"t": 0.30, "x": 17.0, "y": 13.6, "vista": 15},
    {"t": 0.38, "x": 23.0, "y": 13.6, "vista": 13},
    {"t": 0.47, "x": 25.6, "y": 11.4, "vista": 13},
    {"t": 0.55, "x": 26.8, "y": 14.2, "vista": 14},
    {"t": 0.64, "x": 28.6, "y": 17.4, "vista": 16},
    {"t": 0.71, "x": 30.0, "y": 10.0, "vista": 15},
    {"t": 0.82, "x": 25.0, "y": 12.6, "vista": 24},
    {"t": 0.92, "x": 25.0, "y": 13.0, "vista": 29},
    {"t": 1.00, "x": 26.0, "y": 13.0, "vista": 30},
]


def _blocchi(bid):
    return [e for e in SCENA.elementi if isinstance(e, Blocco) and e.id == bid]


def _catena(sorgente_xy, fine):
    """The belt cells of the chain leaving the block at sorgente_xy and ending in `fine`, in flow order."""
    occupato = fabbrica._occupazione(SCENA)
    for c in fabbrica.catene(SCENA):
        primo = c["celle"][0]
        dietro = occupato.get((primo[0] - fabbrica.VERSI[fabbrica._ingresso(occupato[primo][0], primo)][0],
                               primo[1] - fabbrica.VERSI[fabbrica._ingresso(occupato[primo][0], primo)][1]))
        if dietro and isinstance(dietro[0], Blocco) and (dietro[0].x, dietro[0].y) == sorgente_xy \
                and c["fine"][0] == fine[0] and (fine[0] == "sotto" or c["fine"][1] == fine[1]):
            return [(cella, occupato[cella][0]) for cella in c["celle"]], c["fine"]
    raise KeyError((sorgente_xy, fine))


def _verso_della_cella(e):
    return e.verso if isinstance(e, Tratto) else e.a


def _percorso(celle, fine):
    """Item path in cells: from the source's edge through every belt cell centre to the end (a block's edge, or past
    the bottom of the map)."""
    primo, ultimo = celle[0][0], celle[-1][0]
    dx, dy = fabbrica.VERSI[fabbrica._ingresso(celle[0][1], primo)]
    out = [[primo[0] + 0.5 - dx * 0.5, primo[1] + 0.5 - dy * 0.5]]
    out += [[c[0] + 0.5, c[1] + 0.5] for c, _ in celle]
    dx, dy = fabbrica.VERSI[_verso_della_cella(celle[-1][1])]
    coda = 0.5 if fine[0] == "blocco" else 1.5
    out.append([ultimo[0] + 0.5 + dx * coda, ultimo[1] + 0.5 + dy * coda])
    return out


def linea_del_tempo():
    """The data racconto.js plays: map size, camera key frames, constructions in building order (each with its plan,
    site and working times), item chains, power lines, lights, night and the chapter 5 teaser."""
    ferro, carbone = (next(b for b in _blocchi("miner") if b.y == y) for y in (10, 9))
    forno, generatore = _blocchi("smelter")[0], _blocchi("generator")[0]
    nodi = [e for e in SCENA.elementi if isinstance(e, fabbrica.Nodo)]
    nodo = {(n.x, n.y): n for n in nodi}
    lampade = {(b.x, b.y): b for b in _blocchi("lamp")}
    catene = {
        "ferro": _catena((ferro.x, ferro.y), ("blocco", "smelter")),
        "carbone": _catena((carbone.x, carbone.y), ("blocco", "smelter")),
        "lastre": _catena((forno.x, forno.y), ("sotto", None)),
        "generatore": _catena((carbone.x, carbone.y), ("blocco", "generator")),
    }
    ordine = {
        "minatore-ferro": [ferro], "nastri-ferro": catene["ferro"][0], "forno": [forno],
        "minatore-carbone": [carbone], "nastri-carbone": catene["carbone"][0], "nastri-lastre": catene["lastre"][0],
        "generatore": [generatore], "nastri-generatore": catene["generatore"][0],
        "nodi": [nodo[(28, 11)], nodo[(19, 12)], nodo[(33, 15)]],
        "lampade": [lampade[(23, 12)], lampade[(14, 12)], lampade[(35, 12)], lampade[(33, 19)]],
    }
    costruzioni, indice, precedente = [], {}, INGRESSO_DRONE
    for fase, t0, t1 in FASI:
        pezzi = ordine[fase]
        passo = (t1 - t0) / len(pezzi)
        for k, pezzo in enumerate(pezzi):
            inizio = t0 + k * passo
            c = {"id": f"{fase}-{k}", "t0": round(inizio, 4), "t1": round(inizio + passo * 0.85, 4),
                 "piano": round(precedente, 4), "rot": 0, "specchio": False}
            if isinstance(pezzo, tuple):   # a belt cell: (cell, element)
                (x, y), e = pezzo
                if isinstance(e, Pezzo):
                    c["specchio"], c["rot"] = fabbrica.trasformazione_curva(e.da, e.a)
                    nome = "curva"
                else:
                    c["rot"] = fabbrica.ROT[e.verso]
                    nome = "nastro"
                c.update(x=x, y=y, w=1, h=1, nastro=True, lavoro=nome, fermo=nome, piano_sprite=nome + "-piano")
                indice[(x, y)] = len(costruzioni)
            else:
                bid = "power-node" if isinstance(pezzo, fabbrica.Nodo) else pezzo.id
                w, h = (1, 1) if bid == "power-node" else fabbrica._blocco(bid).celle
                lavoro, fermo, piano = STATI[bid]
                c.update(x=pezzo.x, y=pezzo.y, w=w, h=h, nastro=False, lavoro=lavoro, fermo=fermo, piano_sprite=piano)
                indice[(pezzo.x, pezzo.y, bid)] = len(costruzioni)
            costruzioni.append(c)
            precedente = inizio

    def fatto(celle):   # the scroll progress at which a whole chain is built
        return max(costruzioni[indice[cella]]["t1"] for cella, _ in celle)

    def blocco(b, bid=None):
        return costruzioni[indice[(b.x, b.y, bid or getattr(b, "id", "power-node"))]]

    # When each block starts working: miners once built; the smelter once both its inputs arrive; the generator
    # once its coal arrives; lamps once their line is drawn (below).
    blocco(ferro)["attivo"] = blocco(ferro)["t1"]
    blocco(carbone)["attivo"] = blocco(carbone)["t1"]
    blocco(forno)["attivo"] = round(max(blocco(forno)["t1"], fatto(catene["ferro"][0]), fatto(catene["carbone"][0])), 4)
    blocco(generatore)["attivo"] = round(max(blocco(generatore)["t1"], fatto(catene["generatore"][0])), 4)
    for n in nodi:
        blocco(n, "power-node")["attivo"] = blocco(n, "power-node")["t1"]
    # Power lines: from the generator out, each drawn once both its ends stand, at 25 cells per unit of progress.
    centro = lambda b, w=1, h=1: [b.x + w / 2, b.y + h / 2]
    reti = [(centro(generatore, 2, 2), generatore, "generator", nodo[(28, 11)], "power-node"),
            (centro(nodo[(28, 11)]), nodo[(28, 11)], "power-node", nodo[(19, 12)], "power-node"),
            (centro(nodo[(28, 11)]), nodo[(28, 11)], "power-node", nodo[(33, 15)], "power-node"),
            (centro(nodo[(19, 12)]), nodo[(19, 12)], "power-node", lampade[(23, 12)], "lamp"),
            (centro(nodo[(19, 12)]), nodo[(19, 12)], "power-node", lampade[(14, 12)], "lamp"),
            (centro(nodo[(33, 15)]), nodo[(33, 15)], "power-node", lampade[(35, 12)], "lamp"),
            (centro(nodo[(33, 15)]), nodo[(33, 15)], "power-node", lampade[(33, 19)], "lamp")]
    linee = []
    for da, b1, id1, b2, id2 in reti:
        a = centro(b2)
        t0 = max(blocco(b1, id1)["t1"], blocco(b2, id2)["t1"], blocco(generatore)["attivo"])
        lungo = ((a[0] - da[0]) ** 2 + (a[1] - da[1]) ** 2) ** 0.5
        linee.append({"da": da, "a": a, "t0": round(t0, 4), "t1": round(t0 + lungo / 400, 4)})
        if id2 == "lamp":
            blocco(b2)["attivo"] = linee[-1]["t1"]
    oggetti = {"ferro": "iron-ore", "carbone": "coal", "lastre": "iron-plate", "generatore": "coal"}
    sorgenti = {"ferro": blocco(ferro), "carbone": blocco(carbone), "lastre": blocco(forno), "generatore": blocco(carbone)}
    flussi = [{"oggetto": "oggetto-" + oggetti[n], "percorso": _percorso(*catene[n]),
               "celle": [indice[cella] for cella, _ in catene[n][0]], "avvio": sorgenti[n]["attivo"]}
              for n in ("ferro", "carbone", "lastre", "generatore")]
    luci = [dict(LUCE_LAMPADA, x=b.x + 0.5, y=b.y + 0.5, da=blocco(b)["attivo"]) for b in lampade.values()]
    luci.append(dict(LUCE_FORNO, x=forno.x + 1, y=forno.y + 1, da=blocco(forno)["attivo"]))
    return {
        "cella": CELLA, "larghezza": LARGHEZZA, "righe": RIGHE,
        "camera": CAMERA,
        "costruzioni": costruzioni,
        "flussi": flussi,
        "linee": linee,
        "luci": luci,
        "notte": NOTTE,
        "allarme": ALLARME,
        "nemici": NEMICI,
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


# The parts of drone-1, told apart by colour (tools/art draws it with these exact shades).
_SCAFO = {(0xA4, 0xA6, 0xB2), (0xD6, 0xD8, 0xE2), (0x84, 0x86, 0x92)}   # the light hull and its arms
_GONDOLA = {(0x60, 0x62, 0x6E), (0x44, 0x46, 0x52)}                     # rotor ducts (and the tail stub)
_SENSORE = {(0xD6, 0x84, 0x3C), (0xFF, 0xBE, 0x64), (0xFF, 0xF0, 0xC8)}  # the orange eye
_CONTORNO = (0x2B, 0x2B, 0x2B)


def modello_drone(drone):
    """{(x, y): (part, bottom, top)} of drone-1 as a small voxel model (heights in art px, 0 = belly).

    Sprite stacking only looks solid when the layers differ: here the hull is a dome (taller towards its
    middle), the eye a sensor raised above it, the two ducts float higher than the belly as stepped rings, the tail
    stub stays low. Outline pixels join the part most of their neighbours belong to."""
    drone = drone.convert("RGBA")
    px = drone.load()
    w, h = drone.size
    parte = {}
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if not a:
                continue
            c = (r, g, b)
            if c in _SENSORE:
                parte[(x, y)] = "sensore"
            elif c in _SCAFO:
                parte[(x, y)] = "scafo"
            elif c in _GONDOLA:
                parte[(x, y)] = "coda" if x <= 3 else "gondola"
            else:
                parte[(x, y)] = None   # outline: decided below
    for _ in range(3):   # outlines take the part of their neighbours (the eye's ring belongs to the hull)
        for (x, y), v in list(parte.items()):
            if v is None:
                vicini = [parte.get(q) for q in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))]
                vicini = ["scafo" if n == "sensore" else n for n in vicini if n]
                if vicini:
                    parte[(x, y)] = max(sorted(set(vicini)), key=vicini.count)
    for k, v in parte.items():
        if v is None:
            parte[k] = "scafo"
    # Depth of every hull pixel from the hull's edge: the dome rises towards the middle.
    profondita = {}
    bordo = [q for q, v in parte.items() if v in ("scafo", "sensore")
             and any(parte.get(n) not in ("scafo", "sensore") for n in ((q[0] + 1, q[1]), (q[0] - 1, q[1]),
                                                                        (q[0], q[1] + 1), (q[0], q[1] - 1)))]
    for q in bordo:
        profondita[q] = 1
    fronte = bordo
    while fronte:
        nuovo = []
        for x, y in fronte:
            for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if parte.get(n) in ("scafo", "sensore") and n not in profondita:
                    profondita[n] = profondita[(x, y)] + 1
                    nuovo.append(n)
        fronte = nuovo
    modello = {}
    for q, v in parte.items():
        if v == "gondola":   # a stepped duct: outer rim high, inner band lower, the hub at the bottom
            c = px[q][:3]
            esterno = any(not (0 <= n[0] < w and 0 <= n[1] < h) or not px[n][3]
                          for n in ((q[0] + 1, q[1]), (q[0] - 1, q[1]), (q[0], q[1] + 1), (q[0], q[1] - 1)))
            alto = 3 if c == (0x60, 0x62, 0x6E) or (c == _CONTORNO and esterno) else 2 if c == (0x44, 0x46, 0x52) else 1
            modello[q] = (v, 1, alto)
        elif v == "coda":
            modello[q] = (v, 0, 2)
        elif v == "sensore":
            modello[q] = (v, 0, 5)
        else:
            modello[q] = (v, 0, 1 + min(profondita.get(q, 1), 3))
    return modello


def fette_drone(drone):
    """The drone in 2.5D: FETTE layers of modello_drone, bottom first. A pixel shows its own colour on the layer of
    its top (the face seen from above) and a darker shade on the layers below (its walls, which the offset of the
    stack lets peek out); enlarged x INGRANDIMENTO without filtering."""
    drone = drone.convert("RGBA")
    px = drone.load()
    modello = modello_drone(drone)
    livelli = []
    for k in range(FETTE):
        livello = Image.new("RGBA", drone.size)
        dst = livello.load()
        for (x, y), (parte, basso, alto) in modello.items():
            if not basso <= k <= alto:
                continue
            r, g, b, a = px[x, y]
            if k < alto:
                f = 0.42 + 0.3 * k / max(1, alto)
                r, g, b = int(r * f), int(g * f), int(b * f)
            dst[x, y] = (r, g, b, a)
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


def _nero(im):
    """A silhouette: the sprite's shape in black (the enemies of the teaser, as on the landing page)."""
    im = im.convert("RGBA")
    out = Image.new("RGBA", im.size, (0, 0, 0, 0))
    out.putalpha(im.getchannel("A"))
    return out


def sprite_del_racconto(immagini):
    """{name: (image, meta)}: every picture racconto.js draws, at art size (the drone x INGRANDIMENTO)."""
    nastro = immagini.fotogrammi(T1 + "-anim")
    curva = immagini.fotogrammi(T1 + "-corner-anim")
    drone = immagini("drone-1")
    fps = fabbrica.RATE[T1] * len(nastro)  # one cell of tread per 1 / rate seconds, as in the game
    out = {
        "nastro": (striscia(nastro), {"fotogrammi": len(nastro), "fps": fps}),
        "curva": (striscia(curva), {"fotogrammi": len(curva), "fps": fps}),
        "nastro-piano": (fantasma(nastro[0]), {"fotogrammi": 1}),
        "curva-piano": (fantasma(curva[0]), {"fotogrammi": 1}),
        "nodo": (immagini("power-node"), {"fotogrammi": 1}),
        "nodo-piano": (fantasma(immagini("power-node")), {"fotogrammi": 1}),
        "drone": (fette_drone(drone), {"fotogrammi": FETTE, "lato": drone.width}),
        "drone-ombra": (ombra_drone(drone), {"fotogrammi": 1, "lato": drone.width}),
        "nemico-2": (_nero(immagini("drone-2")), {"fotogrammi": 1}),
        "nemico-3": (_nero(immagini("drone-3")), {"fotogrammi": 1}),
    }
    for oggetto in ("iron-ore", "coal", "iron-plate"):
        out["oggetto-" + oggetto] = (immagini(oggetto + "-belt"), {"fotogrammi": 1})
    for bid, (nome, lavoro, fermo) in (("miner", ("minatore", "Al lavoro", "Fermo")),
                                       ("smelter", ("forno", "Al lavoro", "Fermo")),
                                       ("generator", ("generatore", "In funzione", "Fermo")),
                                       ("lamp", ("lampada", "Accesa", "Spenta"))):
        fotogrammi = _stato(bid, lavoro, immagini)
        meta = {"fotogrammi": len(fotogrammi)}
        if len(fotogrammi) > 1:
            meta["durate"] = [d for _, d in fotogrammi]
        out[nome] = (striscia([f for f, _ in fotogrammi]), meta)
        spento = _stato(bid, fermo, immagini)[0][0]
        out[nome + "-fermo"] = (spento, {"fotogrammi": 1})
        out[nome + "-piano"] = (fantasma(spento), {"fotogrammi": 1})
    return out


def poster(immagini, sprite, dati):
    """The whole factory as one still picture (pages without the script, link previews): every block working,
    every belt, the power lines and the drone, x2 as in the game, cropped around the factory."""
    from PIL import ImageDraw
    im = terreno(immagini)

    def metti(nome, x, y, rot=0, specchio=False):
        foglio, meta = sprite[nome]
        w = foglio.width // meta["fotogrammi"]
        f = foglio.crop((0, 0, w, foglio.height))
        if specchio:
            f = f.transpose(Image.FLIP_LEFT_RIGHT)
        if rot:
            f = f.rotate(-rot, expand=True)
        im.alpha_composite(f, (int(x * CELLA), int(y * CELLA)))

    for c in dati["costruzioni"]:
        metti(c["lavoro"], c["x"], c["y"], c["rot"], c["specchio"])
    disegno = ImageDraw.Draw(im)
    for linea in dati["linee"]:
        (x0, y0), (x1, y1) = linea["da"], linea["a"]
        disegno.line((x0 * CELLA, y0 * CELLA, x1 * CELLA, y1 * CELLA), fill=(0xFB, 0xE3, 0x6B, 255), width=1)
    ombra, meta = sprite["drone-ombra"]
    fette, _ = sprite["drone"]
    lato = meta["lato"]
    dx, dy = 21.4 * CELLA, 11.0 * CELLA
    im.alpha_composite(ombra.resize((lato, lato), Image.LANCZOS), (int(dx - lato / 2 + 4), int(dy - lato / 2 + 6)))
    passo = fette.width // FETTE
    for k in range(FETTE):
        f = fette.crop((k * passo, 0, (k + 1) * passo, fette.height)).resize((lato, lato), Image.LANCZOS)
        im.alpha_composite(f, (int(dx - lato / 2), int(dy - lato / 2 - k - 4)))
    ritaglio = im.crop((7 * CELLA, 5 * CELLA, 38 * CELLA, 22 * CELLA))
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
