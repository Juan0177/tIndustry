"""The scroll-driven story of the site: «dal giacimento al CORE», eight chapters on fourteen panels.

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
import math
import os
import random

from PIL import Image

import composti
import fabbrica
from fabbrica import T1, Blocco, Giacimento, Nodo, Pezzo, Scena, Tratto

CELLA = composti.T  # art px per cell (32); the canvas scales the art, like the game draws it x2
LARGHEZZA, RIGHE = 64, 40  # the whole world of the story: the factory in the middle, biomes around
AMBRA = (0xFF, 0xD3, 0x7F)  # UiColors.Costruzione = Accento (Mindustry Pal.accent)
FETTE = 6  # slices of the drone stack, one art px apart (modello_drone gives each part its own heights)
INGRANDIMENTO = 4  # DroneVisual.SharpFactor: enlarged without filtering, drawn smaller with filtering

# The story map, built chapter by chapter. Iron: a miner on its deposit, the ore leaving through a corner (never
# straight out of a miner), down and east into the smelter. Coal: a second miner on the deposit next to it feeds
# the smelter from the east and, from its top side, the generator. The plates leave the smelter eastwards, run
# down to the Core (there from the start, as in every game) and enter it through the T merge above it; in the last
# chapter a copper line joins the same T from the west. Power: generator, three nodes, four lamps.
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
    Tratto(27, 15, 6, "E", T1, ("iron-plate",)),
    Pezzo(33, 15, "curva", T1, da="E", a="S"),
    Tratto(33, 16, 13, "S", T1, ("iron-plate",)),
    Pezzo(33, 29, "curva", T1, da="S", a="W"),
    Tratto(32, 29, 2, "W", T1, ("iron-plate",)),
    Pezzo(30, 29, "t", T1, verso="S"),
    Tratto(30, 30, 4, "S", T1, ("iron-plate",)),
    Blocco(29, 34, "core-2", "Luce del nucleo"),
    Pezzo(26, 8, "curva", T1, da="N", a="E"),
    Tratto(27, 8, 4, "E", T1, ("coal",)),
    Blocco(31, 7, "generator", "In funzione"),
    Nodo(28, 11), Nodo(19, 12), Nodo(36, 17),
    Blocco(23, 12, "lamp", "Accesa"), Blocco(14, 12, "lamp", "Accesa"),
    Blocco(38, 13, "lamp", "Accesa"), Blocco(38, 22, "lamp", "Accesa"),
    Giacimento(17, 26, 4, 4, "copper-ore", 9),
    Blocco(18, 27, "miner", "Al lavoro"),
    Pezzo(20, 28, "curva", T1, da="E", a="S"),
    Pezzo(20, 29, "curva", T1, da="S", a="E"),
    Tratto(21, 29, 9, "E", T1, ("copper-ore",)),
    # Lines already there before the story starts, only partly seen: there is more factory around.
    Giacimento(1, 30, 4, 4, "lead-ore", 13),                     # lead from the rocks, into the Core from the west
    Blocco(2, 31, "miner", "Al lavoro"),
    Pezzo(4, 32, "curva", T1, da="E", a="S"),
    Tratto(4, 33, 2, "S", T1, ("lead-ore",)),
    Pezzo(4, 35, "curva", T1, da="S", a="E"),
    Tratto(5, 35, 24, "E", T1, ("lead-ore",)),
    Tratto(16, 0, 5, "S", T1, ("iron-ore",)),                    # ore from the north into a smelter of its own
    Blocco(15, 5, "smelter", "Al lavoro"),
    Tratto(58, 0, RIGHE, "S", T1, ("titanium-ore",)),             # titanium crossing the east, beyond the river
], seme=7, larghezza=LARGHEZZA)
# The pieces of the lines above: built before the story, the drone never visits them.
GIA_FATTE = {(2, 31), (15, 5)}   # their blocks (top-left cells); their belts are found by their chains

# Biomes of the world around the factory (chapter «il mondo»): rocks to the north-west, snow to the north-east, sand
# to the south-east, a river between the factory and the east, where the enemies come from.
def _scarto(x, y, sale):
    """A fixed pseudo-random offset in -1..1 per cell, so biome borders are ragged instead of stair-stepped."""
    h = (x * 73856093) ^ (y * 19349663) ^ (sale * 83492791)
    return ((h & 0xFFFF) / 0xFFFF) * 2 - 1


def bioma_della_cella(x, y):
    fiume = 46 + 3.2 * math.sin(y / 5.0) + (y - 20) * 0.12 + 0.35 * _scarto(x, y, 1)
    if fiume - 1.4 <= x <= fiume + 1.4:
        return "acqua-profonda" if abs(x - fiume) < 0.6 else "acqua"
    if x > fiume:
        return "neve" if y < 18 + 2 * math.sin(x / 3.0) + 1.3 * _scarto(x, y, 2) else "sabbia"
    if x < 7 + 2 * math.sin(y / 4.0) + 1.2 * _scarto(x, y, 3) or y < 4 + 1.5 * math.sin(x / 3.5) + 1.2 * _scarto(x, y, 4):
        return "rocce"
    return "prati"


# Pictures of each block: (working, stopped, plan) sprite names of sprite_del_racconto().
STATI = {"miner": ("minatore", "minatore-fermo", "minatore-piano"),
         "smelter": ("forno", "forno-fermo", "forno-piano"),
         "generator": ("generatore", "generatore-fermo", "generatore-piano"),
         "lamp": ("lampada", "lampada-fermo", "lampada-piano"),
         "power-node": ("nodo", "nodo", "nodo-piano")}

# Scroll progress (0..1) of the story: eleven panels, one screen each, at 0, 0.1, ... 1. The drone builds in this
# order; each phase spreads its pieces evenly over its window.
INGRESSO_DRONE = 0.025
FASI = [  # (what, from, to)
    ("minatore-ferro", 0.04, 0.09),
    ("nastri-ferro", 0.11, 0.23),
    ("forno", 0.25, 0.29),
    ("minatore-carbone", 0.30, 0.33),
    ("nastri-carbone", 0.335, 0.37),
    ("nastri-lastre", 0.385, 0.47),
    ("generatore", 0.475, 0.50),
    ("nastri-generatore", 0.50, 0.53),
    ("nodi", 0.54, 0.57),
    ("lampade", 0.57, 0.60),
    ("minatore-rame", 0.905, 0.925),
    ("nastri-rame", 0.928, 0.975),
]
# Night as NightLayer.Darkness, the world multiplied by it: it falls in chapter 6 and lifts at dawn (chapter 8).
NOTTE = {"t0": 0.52, "t1": 0.62, "alba0": 0.76, "alba1": 0.84, "colore": [0.34, 0.38, 0.54]}
LUCE_LAMPADA = {"raggio": 6.5, "colore": [1.0, 0.9, 0.68], "energia": 1.4}  # NightLayer.LampLight
LUCE_FORNO = {"raggio": 3.0, "colore": [1.0, 0.55, 0.25], "energia": 1.2}   # NightLayer.SmelterLight
LUCE_CORE = {"raggio": 5.0, "colore": [0.55, 0.88, 1.0], "energia": 1.3}    # NightLayer.CoreLight
# The attack (roadmap «Attacco e difesa»: enemies come only at night, at daybreak the survivors flee). They come over
# the river from the east, stop outside the light of the lamps, and run back east when the day returns.
ALLARME = {"t0": 0.63, "t1": 0.80}   # red glow at the screen's edges while they are there
ELMETTO = {"cade": 0.672, "posato": 0.69, "toglie": 0.84, "via": 0.86}
# The close-up of the attack: cinema bars come in, the drone flies to the river bank (GUARDIA) and faces the
# silhouettes coming from the east; it sees them (a «!»), trembles and sweats, the helmet drops on it. Then the
# ice breaks (the panel says they leave by themselves at dawn): it calms down, the bars go and the camera pulls back.
ATTACCO = {"barre": [0.63, 0.65, 0.715, 0.735], "vede": 0.66, "trema": [0.66, 0.715]}
GUARDIA = [{"t": 0.642, "x": 38.15, "y": 21.6},
           {"t": 0.66, "x": 40.3, "y": 21.4, "guarda": [47.0, 21.8]}, {"t": 0.80, "x": 40.3, "y": 21.4, "guarda": [47.0, 21.8]}]
NEMICI = [{"sprite": "nemico-2", "y": 18.8, "chiavi": [[0.64, 50.5], [0.72, 45.4], [0.80, 46.4], [0.87, 70.0]]},
          {"sprite": "nemico-3", "y": 21.2, "chiavi": [[0.645, 51.5], [0.725, 44.6], [0.80, 45.6], [0.86, 71.0]]},
          {"sprite": "nemico-2", "y": 23.5, "chiavi": [[0.65, 51.0], [0.73, 45.2], [0.80, 46.2], [0.87, 70.0]]},
          {"sprite": "nemico-3", "y": 25.8, "chiavi": [[0.655, 52.0], [0.735, 46.0], [0.80, 47.0], [0.88, 71.0]]},
          {"sprite": "nemico-2", "y": 13.5, "chiavi": [[0.66, 56.0], [0.75, 47.0], [0.80, 48.0], [0.87, 70.0]]},
          {"sprite": "nemico-3", "y": 30.5, "chiavi": [[0.665, 57.0], [0.755, 46.5], [0.80, 47.5], [0.88, 71.0]]}]
CAMERA = [  # centre in cells, `vista`: cells across a 16:9 screen; xv, yv, vv: centre and vista on portrait screens
    {"t": 0.00, "x": 10.6, "y": 10.6, "vista": 9},
    {"t": 0.07, "x": 11.0, "y": 10.8, "vista": 10},
    {"t": 0.13, "x": 11.8, "y": 11.8, "vista": 12},
    {"t": 0.21, "x": 17.0, "y": 13.6, "vista": 15},
    {"t": 0.27, "x": 23.0, "y": 13.6, "vista": 13},
    {"t": 0.33, "x": 25.6, "y": 11.4, "vista": 13},
    {"t": 0.39, "x": 28.0, "y": 14.6, "vista": 15},
    {"t": 0.46, "x": 31.0, "y": 22.0, "vista": 18},
    {"t": 0.50, "x": 30.0, "y": 10.0, "vista": 15},
    {"t": 0.58, "x": 27.0, "y": 15.0, "vista": 26},
    {"t": 0.625, "x": 34.0, "y": 18.0, "vista": 24},
    # the close-up of the attack: the drone left of the middle (the panel stands on the left), the enemies right
    {"t": 0.66, "x": 41.2, "y": 21.6, "vista": 14, "xv": 43.6, "yv": 24.5, "vv": 16},
    {"t": 0.715, "x": 41.3, "y": 21.6, "vista": 13.5, "xv": 43.6, "yv": 24.5, "vv": 15.5},
    {"t": 0.76, "x": 40.0, "y": 19.0, "vista": 30},
    {"t": 0.82, "x": 40.0, "y": 18.0, "vista": 36},
    {"t": 0.88, "x": 32.0, "y": 20.0, "vista": 64},
    {"t": 0.93, "x": 32.0, "y": 20.0, "vista": 64},
    {"t": 1.00, "x": 28.0, "y": 32.0, "vista": 20, "xv": 30.5, "yv": 34.5, "vv": 12},  # xv, yv, vv: portrait screens
]


# The chapters above run on their own scale (0..1); the market chapter takes the last three of fourteen panels, so
# they are squeezed into 0..10/13 and the market fills 10/13..1 (times below already on the final scale).
SCALA_STORIA = 10 / 13
# The market: a big screen next to the Core. The price of iron plates soars, the drone fills a briefcase (its
# counter climbs to 9999), throws it in an arc into the screen through two hatches on top, the price crashes and
# its falling line becomes a rocket-bomb that goes off as a mushroom cloud when it hits the bottom (the usual
# selling price), the screen shows a parody of the «stonks» meme with the drone in a suit, then a money bonus;
# last, the drone changes channel with a remote and the screen shows the game's channel, with the real download and
# GitHub buttons (HTML laid over the canvas by racconto.js).
MERCATO = {"x": 34, "y": 26, "w": 8, "h": 6,
           "interno": [14, 22, 228, 132],     # the screen inside the frame, art px
           "bocca": [108, 8, 40],             # slot on top of the frame (x, y, width): the hatches hinge on its sides
           "prezzo": {"base": 30, "alto": 2500, "basso": 30},   # the bottom of the crash: the usual selling price
           "sale": [0.785, 0.835], "porte": [0.868, 0.91],
           # the crash: the head of the falling price line is a rocket-bomb, the line its trail; the camera follows it
           # down and, when it hits the bottom (the usual selling price), it goes off as the mushroom cloud
           "crollo": [0.904, 0.926], "boom": [0.926, 0.944],
           "caduta": [96, 156],                  # x where the fall starts and where it hits, screen art px
           "meme": [0.944, 0.954], "bonus": [0.954, 0.964],
           # the remote: a click (tasto), then the old channel folds into a line, static, the new one rolls in
           "canale": {"tasto": 0.968, "cambio": [0.97, 0.982], "sensore": [6, 149]}}
BONUS = 777   # after stonks: «bonus dei soldi», a made-up percentage (the user's joke)
VALIGETTA = {"appare": 0.80, "conta": [0.806, 0.858], "quanti": 9999, "lancio": [0.875, 0.895], "dentro": 0.902}
TAPPE_MERCATO = [  # the drone after the last build: above the Core, then where it throws from, facing the screen;
    # last, above the screen's top left corner with the remote
    {"t": 0.775, "x": 30.15, "y": 32.4}, {"t": 0.86, "x": 30.15, "y": 32.4},
    {"t": 0.872, "x": 32.4, "y": 30.4, "guarda": [38.0, 26.3]}, {"t": 0.956, "x": 32.4, "y": 30.4, "guarda": [38.0, 26.3]},
    {"t": 0.966, "x": 34.6, "y": 26.0, "guarda": [37.0, 28.6]}, {"t": 1.0, "x": 34.6, "y": 26.0, "guarda": [37.0, 28.6]}]
def y_prezzo(v):
    """Height of a price on the market screen's log scale, screen art px (racconto.js yPrezzo does the same)."""
    P, ih = MERCATO["prezzo"], MERCATO["interno"][3]
    lmin, lmax = math.log(P["basso"] * 0.5), math.log(P["alto"] * 1.3)
    return ih - 8 - (math.log(v) - lmin) / (lmax - lmin) * (ih - 34)


def _bomba_nel_mondo(u):
    """Where the falling rocket-bomb is at fraction u of the crash, in cells (for the camera that follows it)."""
    x0, x1 = MERCATO["caduta"]
    y0, y1 = y_prezzo(MERCATO["prezzo"]["alto"]), y_prezzo(MERCATO["prezzo"]["basso"])
    x, y = x0 + (x1 - x0) * u, y0 + (y1 - y0) * u * u
    ix, iy = MERCATO["interno"][:2]
    return (MERCATO["x"] * CELLA + ix + x) / CELLA, (MERCATO["y"] * CELLA + iy + y) / CELLA


def _camera_mercato():
    """The chapter's panels stand on the left, so the screen is kept in the right part of the view; during the
    crash the camera closes in and follows the rocket-bomb down to the bottom, then pulls back for the blast."""
    c0, c1 = MERCATO["crollo"]
    seguito = []
    for u in (0.0, 0.5, 0.8, 1.0):
        x, y = _bomba_nel_mondo(u)
        seguito.append({"t": round(c0 + (c1 - c0) * u, 4), "x": round(x, 2), "y": round(y, 2), "vista": 6,
                        "xv": round(x, 2), "yv": round(y + 0.3, 2), "vv": 7})   # portrait: high, the next panel rises
    # Desktop: the drone a little right of the panel's edge (about 40 % of the width), the screen on the right.
    # Portrait: the panels cross the whole screen from the bottom up, so the action goes where they are not: low
    # while the panel of the soaring price leaves at the top, high from the crash on (the next ones rise from
    # below) until the blast, in the middle while that panel leaves at the top and the last one rises (stonks and
    # the bonus), and at the end the whole screen in the top two fifths, above the last, long panel.
    alto = {"xv": 38.0, "yv": 31.2, "vv": 13.4}   # the screen's 8 cells across a phone, at its top
    mezzo = {"xv": 38.0, "yv": 27.0, "vv": 13.4}  # ...and in its middle
    return ([{"t": 0.79, "x": 32.3, "y": 31.0, "vista": 22, "xv": 31.5, "yv": 31.0, "vv": 12},
             {"t": 0.86, "x": 32.5, "y": 30.8, "vista": 21, "xv": 31.8, "yv": 30.8, "vv": 12},
             {"t": 0.872, "x": 34.2, "y": 29.6, "vista": 18, "xv": 35.5, "yv": 24.5, "vv": 15},
             {"t": 0.898, "x": 35.6, "y": 29.4, "vista": 17, "xv": 38.0, "yv": 24.5, "vv": 14}]
            + seguito
            + [dict({"t": 0.94, "x": 37.2, "y": 29.2, "vista": 10}, **alto),
               dict({"t": 0.952, "x": 36.4, "y": 28.9, "vista": 12.5}, **mezzo),
               dict({"t": 0.966, "x": 36.1, "y": 28.6, "vista": 14.5}, **mezzo),
               dict({"t": 0.982, "x": 36.1, "y": 28.6, "vista": 14.5}, **alto),
               dict({"t": 1.0, "x": 36.1, "y": 28.6, "vista": 14.5}, **alto)])




def _scala(dati, k):
    """The chapters' times multiplied by k (see SCALA_STORIA); nothing else changes."""
    r = lambda v: round(v * k, 4) if v >= 0 else v
    for c in dati["costruzioni"]:
        for chiave in ("t0", "t1", "piano", "attivo"):
            if chiave in c:
                c[chiave] = r(c[chiave])
    for f in dati["flussi"]:
        f["avvio"] = r(f["avvio"])
    for l in dati["linee"]:
        l["t0"], l["t1"] = r(l["t0"]), r(l["t1"])
    for l in dati["luci"]:
        l["da"] = r(l["da"])
    dati["notte"] = {c: (r(v) if c != "colore" else v) for c, v in dati["notte"].items()}
    dati["allarme"] = {c: r(v) for c, v in dati["allarme"].items()}
    dati["elmetto"] = {c: r(v) for c, v in dati["elmetto"].items()}
    dati["attacco"] = {c: ([r(x) for x in v] if isinstance(v, list) else r(v)) for c, v in dati["attacco"].items()}
    dati["drone"]["tappe"] = [dict(p, t=r(p["t"])) for p in dati["drone"]["tappe"]]
    dati["nemici"] = [dict(n, chiavi=[[r(t), x] for t, x in n["chiavi"]]) for n in dati["nemici"]]
    dati["camera"] = [dict(c, t=r(c["t"])) for c in dati["camera"]]
    dati["drone"]["ingresso"] = r(dati["drone"]["ingresso"])
    return dati


def _blocchi(bid):
    return [e for e in SCENA.elementi if isinstance(e, Blocco) and e.id == bid]


def _catena(sorgente_xy, fine):
    """The belt cells of the chain leaving the block at sorgente_xy and ending in `fine`, in flow order."""
    occupato = fabbrica._occupazione(SCENA)
    for c in fabbrica.catene(SCENA):
        primo = c["celle"][0]
        v = fabbrica._ingresso(occupato[primo][0], primo)
        dietro = occupato.get((primo[0] - fabbrica.VERSI[v][0], primo[1] - fabbrica.VERSI[v][1]))
        if dietro and isinstance(dietro[0], Blocco) and (dietro[0].x, dietro[0].y) == sorgente_xy \
                and c["fine"][0] == fine[0] and (fine[0] == "sotto" or c["fine"][1] == fine[1]):
            return [(cella, occupato[cella][0]) for cella in c["celle"]], c["fine"]
    raise KeyError((sorgente_xy, fine))


def _verso_della_cella(e):
    return e.verso if isinstance(e, Tratto) or e.tipo == "t" else e.a


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
    site and working times), the Core, item chains, power lines, lights, night and dawn, the attack."""
    ferro, carbone, rame = (next(b for b in _blocchi("miner") if b.y == y) for y in (10, 9, 27))
    forno = next(b for b in _blocchi("smelter") if (b.x, b.y) not in GIA_FATTE)
    generatore, core = _blocchi("generator")[0], _blocchi("core-2")[0]
    nodi = [e for e in SCENA.elementi if isinstance(e, Nodo)]
    nodo = {(n.x, n.y): n for n in nodi}
    lampade = {(b.x, b.y): b for b in _blocchi("lamp")}
    catene = {
        "ferro": _catena((ferro.x, ferro.y), ("blocco", "smelter")),
        "carbone": _catena((carbone.x, carbone.y), ("blocco", "smelter")),
        "lastre": _catena((forno.x, forno.y), ("blocco", "core-2")),
        "generatore": _catena((carbone.x, carbone.y), ("blocco", "generator")),
        "rame": _catena((rame.x, rame.y), ("blocco", "core-2")),
    }
    ordine = {
        "minatore-ferro": [ferro], "nastri-ferro": catene["ferro"][0], "forno": [forno],
        "minatore-carbone": [carbone], "nastri-carbone": catene["carbone"][0], "nastri-lastre": catene["lastre"][0],
        "generatore": [generatore], "nastri-generatore": catene["generatore"][0],
        "nodi": [nodo[(28, 11)], nodo[(19, 12)], nodo[(36, 17)]],
        "lampade": [lampade[(23, 12)], lampade[(14, 12)], lampade[(38, 13)], lampade[(38, 22)]],
        "minatore-rame": [rame],
        "nastri-rame": [cella for cella in catene["rame"][0] if cella[0] not in {c for c, _ in catene["lastre"][0]}],
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
                if isinstance(e, Pezzo) and e.tipo == "curva":
                    c["specchio"], c["rot"] = fabbrica.trasformazione_curva(e.da, e.a)
                    nome = "curva"
                elif isinstance(e, Pezzo):   # the T merge: both side inputs, out ahead
                    c["rot"] = fabbrica.ROT[e.verso]
                    nome = "t"
                else:
                    c["rot"] = fabbrica.ROT[e.verso]
                    nome = "nastro"
                c.update(x=x, y=y, w=1, h=1, nastro=True, lavoro=nome, fermo=nome, piano_sprite=nome + "-piano")
                indice[(x, y)] = len(costruzioni)
            else:
                bid = "power-node" if isinstance(pezzo, Nodo) else pezzo.id
                w, h = (1, 1) if bid == "power-node" else fabbrica._blocco(bid).celle
                lavoro, fermo, piano = STATI[bid]
                c.update(x=pezzo.x, y=pezzo.y, w=w, h=h, nastro=False, lavoro=lavoro, fermo=fermo, piano_sprite=piano)
                indice[(pezzo.x, pezzo.y, bid)] = len(costruzioni)
            costruzioni.append(c)
            precedente = inizio

    # The lines that were there before the story: built and working from the first frame, never visited.
    gia_flussi = []
    occupato = fabbrica._occupazione(SCENA)
    for cat in fabbrica.catene(SCENA):
        primo = cat["celle"][0]
        v = fabbrica._ingresso(occupato[primo][0], primo)
        dietro = occupato.get((primo[0] - fabbrica.VERSI[v][0], primo[1] - fabbrica.VERSI[v][1]))
        da_blocco = dietro and isinstance(dietro[0], Blocco) and (dietro[0].x, dietro[0].y) in GIA_FATTE
        if not (cat["sorgente"][0] == "sopra" or da_blocco):
            continue
        celle = [(cella, occupato[cella][0]) for cella in cat["celle"]]
        for (x, y), e in celle:
            c = {"id": f"gia-{x}-{y}", "t0": -1, "t1": -0.5, "piano": -1, "rot": fabbrica.ROT[_verso_della_cella(e)],
                 "specchio": False, "fisso": True, "x": x, "y": y, "w": 1, "h": 1, "nastro": True}
            nome = "nastro"
            if isinstance(e, Pezzo):
                c["specchio"], c["rot"] = fabbrica.trasformazione_curva(e.da, e.a)
                nome = "curva"
            c.update(lavoro=nome, fermo=nome, piano_sprite=nome + "-piano")
            indice[(x, y)] = len(costruzioni)
            costruzioni.append(c)
        oggetto = next(e.oggetti[0] for _, e in celle if isinstance(e, Tratto))
        gia_flussi.append({"oggetto": "oggetto-" + oggetto, "percorso": _percorso(celle, cat["fine"]),
                           "celle": [indice[cella] for cella, _ in celle], "avvio": -1})
    for b in SCENA.elementi:
        if isinstance(b, Blocco) and (b.x, b.y) in GIA_FATTE:
            lavoro, fermo, piano = STATI[b.id]
            w, h = fabbrica._blocco(b.id).celle
            costruzioni.append({"id": f"gia-{b.id}-{b.x}-{b.y}", "t0": -1, "t1": -0.5, "piano": -1, "rot": 0,
                                "specchio": False, "fisso": True, "x": b.x, "y": b.y, "w": w, "h": h, "nastro": False,
                                "lavoro": lavoro, "fermo": fermo, "piano_sprite": piano, "attivo": -1})

    def fatto(celle):   # the scroll progress at which a whole chain is built
        return max(costruzioni[indice[cella]]["t1"] for cella, _ in celle)

    def blocco(b, bid=None):
        return costruzioni[indice[(b.x, b.y, bid or getattr(b, "id", "power-node"))]]

    # When each block starts working: miners once built; the smelter once both its inputs arrive; the generator
    # once its coal arrives; lamps once their line is drawn (below).
    for m in (ferro, carbone, rame):
        blocco(m)["attivo"] = blocco(m)["t1"]
    blocco(forno)["attivo"] = round(max(blocco(forno)["t1"], fatto(catene["ferro"][0]), fatto(catene["carbone"][0])), 4)
    blocco(generatore)["attivo"] = round(max(blocco(generatore)["t1"], fatto(catene["generatore"][0])), 4)
    for n in nodi:
        blocco(n, "power-node")["attivo"] = blocco(n, "power-node")["t1"]
    # Power lines: from the generator out, each drawn once both its ends stand.
    centro = lambda b, w=1, h=1: [b.x + w / 2, b.y + h / 2]
    reti = [(centro(generatore, 2, 2), generatore, "generator", nodo[(28, 11)], "power-node"),
            (centro(nodo[(28, 11)]), nodo[(28, 11)], "power-node", nodo[(19, 12)], "power-node"),
            (centro(nodo[(28, 11)]), nodo[(28, 11)], "power-node", nodo[(36, 17)], "power-node"),
            (centro(nodo[(19, 12)]), nodo[(19, 12)], "power-node", lampade[(23, 12)], "lamp"),
            (centro(nodo[(19, 12)]), nodo[(19, 12)], "power-node", lampade[(14, 12)], "lamp"),
            (centro(nodo[(36, 17)]), nodo[(36, 17)], "power-node", lampade[(38, 13)], "lamp"),
            (centro(nodo[(36, 17)]), nodo[(36, 17)], "power-node", lampade[(38, 22)], "lamp")]
    linee = []
    for da, b1, id1, b2, id2 in reti:
        a = centro(b2)
        t0 = max(blocco(b1, id1)["t1"], blocco(b2, id2)["t1"], blocco(generatore)["attivo"])
        lungo = ((a[0] - da[0]) ** 2 + (a[1] - da[1]) ** 2) ** 0.5
        linee.append({"da": da, "a": a, "t0": round(t0, 4), "t1": round(t0 + lungo / 600, 4)})
        if id2 == "lamp":
            blocco(b2)["attivo"] = linee[-1]["t1"]
    oggetti = {"ferro": "iron-ore", "carbone": "coal", "lastre": "iron-plate", "generatore": "coal", "rame": "copper-ore"}
    sorgenti = {"ferro": blocco(ferro), "carbone": blocco(carbone), "lastre": blocco(forno),
                "generatore": blocco(carbone), "rame": blocco(rame)}
    flussi = [{"oggetto": "oggetto-" + oggetti[n], "percorso": _percorso(*catene[n]),
               "celle": [indice[cella] for cella, _ in catene[n][0]], "avvio": sorgenti[n]["attivo"]}
              for n in ("ferro", "carbone", "lastre", "generatore", "rame")] + gia_flussi
    luci = [dict(LUCE_LAMPADA, x=b.x + 0.5, y=b.y + 0.5, da=blocco(b)["attivo"]) for b in lampade.values()]
    luci.append(dict(LUCE_FORNO, x=forno.x + 1, y=forno.y + 1, da=blocco(forno)["attivo"]))
    luci.append(dict(LUCE_CORE, x=core.x + 1.5, y=core.y + 1.5, da=0))
    dati = {
        "cella": CELLA, "larghezza": LARGHEZZA, "righe": RIGHE,
        "camera": CAMERA,
        "costruzioni": costruzioni,
        "fissi": [{"sprite": "nucleo", "x": core.x, "y": core.y, "w": 3, "h": 3},
                  {"sprite": "schermo", "x": MERCATO["x"], "y": MERCATO["y"], "w": MERCATO["w"], "h": MERCATO["h"]}],
        "flussi": flussi,
        "linee": linee,
        "luci": luci,
        "notte": NOTTE,
        "allarme": ALLARME,
        "elmetto": ELMETTO,
        "attacco": ATTACCO,
        "nemici": NEMICI,
        "velocita": fabbrica.RATE[T1],  # cells per second on a T1 belt
        "drone": {"ingresso": INGRESSO_DRONE, "partenza": [26.0, 2.0], "fette": FETTE,
                  "ingrandimento": INGRANDIMENTO, "tappe": GUARDIA},
    }
    _scala(dati, SCALA_STORIA)
    dati["camera"] += _camera_mercato()
    dati["mercato"] = MERCATO
    dati["valigetta"] = VALIGETTA
    dati["drone"]["tappe"] += TAPPE_MERCATO
    return dati


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


FETTE_ELMETTO = 4  # brim, then the dome narrowing towards the top


def fette_elmetto(elmetto):
    """The helmet of tools/art (drone-1-elmetto) in 2.5D like the drone: the brim on the bottom layer, the dome on
    the layers above, each one the part of the shell at least that high (the shades of the sprite say how high:
    brim, edge, middle, top); lower layers darker, enlarged x INGRANDIMENTO."""
    from palette import MUSCHIO
    elmetto = elmetto.convert("RGBA")
    px = elmetto.load()
    w, h = elmetto.size
    quota = {}
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if not a:
                continue
            c = (r, g, b)
            if c in MUSCHIO:
                quota[(x, y)] = {0: 0, 1: 1, 2: 2}.get(MUSCHIO.index(c), 3)
            else:   # outline: the brim's outer edge, or the step from brim to dome
                vicini = [quota.get(n) for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))]
                fuori = any(not (0 <= n[0] < w and 0 <= n[1] < h) or not px[n][3]
                            for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
                quota[(x, y)] = 0 if fuori else 1
    livelli = []
    for k in range(FETTE_ELMETTO):
        livello = Image.new("RGBA", elmetto.size)
        dst = livello.load()
        for (x, y), q in quota.items():
            if q < k:
                continue
            r, g, b, a = px[x, y]
            if q > k:
                f = 0.55 + 0.15 * k
                r, g, b = int(r * f), int(g * f), int(b * f)
            dst[x, y] = (r, g, b, a)
        livelli.append(livello.resize((w * INGRANDIMENTO, h * INGRANDIMENTO), Image.NEAREST))
    return striscia(livelli)


def ombra_drone(drone):
    """DroneVisual's shadow: the same sprite in black at 35 %."""
    drone = drone.convert("RGBA")
    a = drone.getchannel("A").point(lambda v: int(v * 0.35))
    out = Image.new("RGBA", drone.size, (0, 0, 0, 0))
    out.putalpha(a)
    return out.resize((drone.width * INGRANDIMENTO, drone.height * INGRANDIMENTO), Image.NEAREST)


def terreno(immagini):
    """The ground of the whole map at art size: each cell's biome (bioma_della_cella) drawn with its floors as
    fabbrica's terrain windows (mostly the first floor, a few others), the river in shallow and deep water, and the
    deposits on top."""
    from blocchi import terreno as tr
    suoli = {}
    for f in tr.TERRENO["floors"]:
        suoli.setdefault(f["biome"], []).append(f["id"])
    rnd = random.Random(SCENA.seme)
    im = Image.new("RGBA", (LARGHEZZA * CELLA, RIGHE * CELLA))
    for j in range(RIGHE):
        for i in range(LARGHEZZA):
            bioma = bioma_della_cella(i, j)
            if bioma.startswith("acqua"):
                s = "deep-water" if bioma == "acqua-profonda" else "shallow-water"
                rnd.random()
            else:
                elenco = suoli[bioma]
                s = elenco[0] if rnd.random() < 0.88 else rnd.choice(elenco)  # calmer than the landing windows
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


# ---------------------------------------------------------------- the market's pictures (drawn here: props of the
# site's story, not game sprites; colours from the game's palette)

def schermo_mercato():
    """The market screen, 8x6 cells: a metal frame on two legs, the dark screen inside (MERCATO["interno"], drawn
    by racconto.js) and the slot on top where the hatches open."""
    from PIL import ImageDraw
    from palette import ACCENTO, METALLO, NASTRO, OUT
    w, h = MERCATO["w"] * CELLA, MERCATO["h"] * CELLA
    im = Image.new("RGBA", (w, h))
    d = ImageDraw.Draw(im)
    for x0 in (44, w - 56):   # legs and feet
        d.rectangle((x0, 160, x0 + 11, h - 5), fill=METALLO[1], outline=OUT)
        d.rectangle((x0 - 8, h - 6, x0 + 19, h - 1), fill=METALLO[2], outline=OUT)
    d.rectangle((0, 8, w - 1, 167), fill=METALLO[2], outline=OUT)
    d.line((1, 9, w - 2, 9), fill=METALLO[4]); d.line((1, 9, 1, 166), fill=METALLO[4])
    d.line((1, 166, w - 2, 166), fill=METALLO[0]); d.line((w - 2, 9, w - 2, 166), fill=METALLO[0])
    for x, y in ((5, 13), (w - 7, 13), (5, 161), (w - 7, 161)):   # rivets
        d.rectangle((x, y, x + 1, y + 1), fill=METALLO[5])
    ix, iy, iw, ih = MERCATO["interno"]
    d.rectangle((ix - 2, iy - 2, ix + iw + 1, iy + ih + 1), fill=NASTRO[1], outline=OUT)
    d.rectangle((ix, iy, ix + iw - 1, iy + ih - 1), fill=(5, 7, 10))
    bx, by, bw = MERCATO["bocca"]   # the slot on top, with its two hinge pins
    d.rectangle((bx, by - 1, bx + bw - 1, by + 6), fill=(0, 0, 0), outline=OUT)
    for x in (bx - 2, bx + bw):
        d.rectangle((x, by, x + 1, by + 2), fill=ACCENTO[3])
    sx, sy = MERCATO["canale"]["sensore"]   # the remote's sensor, a small red window on the frame
    d.rectangle((sx - 2, sy - 2, sx + 2, sy + 2), fill=(60, 8, 14), outline=OUT)
    d.rectangle((sx - 1, sy - 1, sx, sy), fill=(255, 74, 90))
    for k in range(0, w - 40, 12):   # hazard stripes on the bottom band
        d.polygon([(20 + k, 157), (26 + k, 157), (22 + k, 163), (16 + k, 163)], fill=(250, 204, 21))
    return im


def valigetta():
    """The briefcase (24x18): leather, handle, two clasps and a small display (LCD_VALIGETTA) racconto.js writes
    the count on."""
    from PIL import ImageDraw
    from palette import METALLO, OUT, TERRA
    im = Image.new("RGBA", (24, 18))
    d = ImageDraw.Draw(im)
    d.rectangle((8, 0, 15, 4), outline=OUT); d.rectangle((9, 1, 14, 3), outline=TERRA[1])
    d.rectangle((0, 4, 23, 17), fill=TERRA[2], outline=OUT)
    d.line((1, 5, 22, 5), fill=TERRA[4]); d.line((1, 16, 22, 16), fill=TERRA[0])
    for x in (3, 18):
        d.rectangle((x, 5, x + 2, 7), fill=METALLO[4], outline=OUT)
    lx, ly, lw, lh = LCD_VALIGETTA
    d.rectangle((lx - 1, ly - 1, lx + lw, ly + lh), fill=(10, 24, 14), outline=OUT)
    return im


LCD_VALIGETTA = [4, 9, 16, 5]   # display of the briefcase, art px (four 3x5 digits with 1 px gaps)


def titolo_mercato():
    import carattere
    return carattere.scrivi("MERCATO · LASTRE DI FERRO", k=1, colore=(0xFF, 0xD3, 0x7F))


def stonks(drone):
    """The ending on the market screen: a parody of the «stonks» meme drawn here (nothing taken from the original
    picture): a blue stock board full of numbers, the drone in suit and tie with crossed arms where the man stands,
    and the glowing arrow that plunges instead of rising."""
    from PIL import ImageDraw
    import carattere
    from palette import METALLO, OUT
    iw, ih = MERCATO["interno"][2:]
    im = Image.new("RGBA", (iw, ih))
    d = ImageDraw.Draw(im)
    for y in range(ih):   # the board: deep blue, lighter at the top
        u = y / ih
        d.line((0, y, iw, y), fill=(int(22 - 10 * u), int(70 - 34 * u), int(190 - 80 * u), 255))
    numeri = ["0.168", "1.4563", "2.286", "0.12%", "1.0287", "0.9%", "0.1204", "N/A", "1.286", "0.234", "0.1902",
              "560", "2.404", "0.156", "1.902", "0.12"]
    for k, n in enumerate(numeri):   # cells of the board, each with a number and an up or down mark
        cx, cy = 4 + (k % 4) * 57, 4 + (k // 4) * 33
        d.rectangle((cx, cy, cx + 53, cy + 29), outline=(70, 120, 235))
        t = carattere.scrivi(n, k=1, colore=(200, 220, 255))
        im.alpha_composite(t, (cx + 4, cy + 4))
        su = (k * 7) % 3 != 0
        x0, y0 = cx + 42, cy + 18
        d.polygon([(x0, y0 + 6), (x0 + 6, y0 + 6), (x0 + 3, y0)] if su else [(x0, y0), (x0 + 6, y0), (x0 + 3, y0 + 6)],
                  fill=(225, 235, 255))
    # the arrow, plunging: a glow, an orange body, a hot core, and the head at the bottom right
    for largo, colore in ((9, (255, 120, 20, 90)), (5, (255, 120, 20, 255)), (2, (255, 220, 120, 255))):
        d.line([(92, 18), (128, 46), (146, 40), (196, 100)], fill=colore, width=largo, joint="curve")
    d.polygon([(206, 112), (184, 104), (200, 88)], fill=(255, 150, 40, 255), outline=(255, 230, 150, 255))
    # the drone in a suit: shoulders, lapels, white shirt, blue tie, crossed arms; its head is drone-1, nose up
    d.polygon([(8, ih), (18, 96), (40, 88), (64, 88), (86, 96), (96, ih)], fill=(26, 27, 33), outline=OUT)
    d.polygon([(40, 88), (52, 112), (64, 88)], fill=(236, 238, 242))
    d.polygon([(40, 88), (34, 104), (50, 110)], fill=(44, 46, 56)); d.polygon([(64, 88), (70, 104), (54, 110)], fill=(44, 46, 56))
    d.polygon([(49, 90), (55, 90), (54, 95), (50, 95)], fill=(40, 96, 214))
    d.polygon([(50, 95), (54, 95), (57, 114), (52, 120), (47, 114)], fill=(48, 110, 230), outline=(24, 64, 160))
    d.rectangle((14, 112, 90, 124), fill=(34, 35, 42), outline=OUT)   # crossed arms
    d.line((20, 112, 60, 124), fill=OUT); d.line((84, 112, 44, 124), fill=OUT)
    for x in (24, 74):
        d.rectangle((x, 114, x + 6, 120), fill=METALLO[3], outline=OUT)
    testa = drone.convert("RGBA").rotate(90, expand=True).resize((drone.width * 2, drone.height * 2), Image.NEAREST)
    im.alpha_composite(testa, (52 - testa.width // 2, 86 - testa.height + 6))
    scritta = carattere.scrivi("stonks", k=3, colore=(255, 255, 255))
    ombra = carattere.scrivi("stonks", k=3, colore=(10, 20, 60))
    for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1), (2, 2)):
        im.alpha_composite(ombra, (120 + dx, 96 + dy))
    im.alpha_composite(scritta, (120, 96))
    return im


def bonus_soldi():
    """The bonus after stonks: a gold-framed box with a coin, «BONUS DEI SOLDI» and +BONUS%, like a game reward."""
    from PIL import ImageDraw
    import carattere
    from palette import ACCENTO, OUT
    im = Image.new("RGBA", (176, 56))
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, 175, 55), fill=ACCENTO[3], outline=OUT)
    d.rectangle((3, 3, 172, 52), fill=(20, 22, 28), outline=ACCENTO[1])
    d.ellipse((10, 12, 41, 43), fill=(250, 204, 21), outline=OUT)   # the coin, with its $
    d.ellipse((14, 16, 37, 39), outline=(196, 142, 30))
    moneta = carattere.scrivi("$", k=2, colore=(120, 80, 10))
    im.alpha_composite(moneta, (26 - moneta.width // 2, 28 - moneta.height // 2))
    im.alpha_composite(carattere.scrivi("BONUS DEI SOLDI", k=1, colore=(0xFF, 0xD3, 0x7F)), (50, 9))
    im.alpha_composite(carattere.scrivi(f"+{BONUS}%", k=3, colore=(88, 214, 141)), (50, 22))
    return im


def canale_del_gioco(logo):
    """The channel the drone switches to: the game's own, its logo and below it room for the real buttons
    (racconto.js lays the HTML links over the lower part, BOTTONI_TV)."""
    from PIL import ImageDraw
    iw, ih = MERCATO["interno"][2:]
    im = Image.new("RGBA", (iw, ih))
    d = ImageDraw.Draw(im)
    for y in range(ih):   # night blue, darker towards the bottom, with faint scan lines
        u = y / ih
        c = (int(16 - 8 * u), int(24 - 12 * u), int(44 - 22 * u))
        d.line((0, y, iw, y), fill=(c[0] + 6, c[1] + 6, c[2] + 8, 255) if y % 3 == 0 else c + (255,))
    grande = logo.convert("RGBA").resize((logo.width * 3, logo.height * 3), Image.NEAREST)
    im.alpha_composite(grande, ((iw - grande.width) // 2, 26))   # under the channel number (osd_canale, top left)
    x0, y0, x1, y1 = BOTTONI_TV
    d.rectangle((x0, y0, x1, y1), outline=(38, 52, 84))
    return im


BOTTONI_TV = [16, 86, 211, 123]   # where the HTML buttons stand on the new channel, screen art px


def osd_canale():
    import carattere
    return carattere.scrivi("CANALE 2", k=2, colore=(124, 252, 138))


def sprite_del_racconto(immagini):
    """{name: (image, meta)}: every picture racconto.js draws, at art size (the drone x INGRANDIMENTO)."""
    nastro = immagini.fotogrammi(T1 + "-anim")
    curva = immagini.fotogrammi(T1 + "-corner-anim")
    t = immagini.fotogrammi(T1 + "-t-anim")
    drone = immagini("drone-1")
    fps = fabbrica.RATE[T1] * len(nastro)  # one cell of tread per 1 / rate seconds, as in the game
    out = {
        "nastro": (striscia(nastro), {"fotogrammi": len(nastro), "fps": fps}),
        "curva": (striscia(curva), {"fotogrammi": len(curva), "fps": fps}),
        "nastro-piano": (fantasma(nastro[0]), {"fotogrammi": 1}),
        "t": (striscia(t), {"fotogrammi": len(t), "fps": fps}),
        "t-piano": (fantasma(t[0]), {"fotogrammi": 1}),
        "curva-piano": (fantasma(curva[0]), {"fotogrammi": 1}),
        "nodo": (immagini("power-node"), {"fotogrammi": 1}),
        "nodo-piano": (fantasma(immagini("power-node")), {"fotogrammi": 1}),
        "drone": (fette_drone(drone), {"fotogrammi": FETTE, "lato": drone.width}),
        "drone-ombra": (ombra_drone(drone), {"fotogrammi": 1, "lato": drone.width}),
        "elmetto": (fette_elmetto(immagini("drone-1-elmetto")), {"fotogrammi": FETTE_ELMETTO, "lato": drone.width}),
        "nemico-2": (_nero(immagini("drone-2")), {"fotogrammi": 1}),
        "nemico-3": (_nero(immagini("drone-3")), {"fotogrammi": 1}),
    }
    out["schermo"] = (schermo_mercato(), {"fotogrammi": 1})
    out["valigetta"] = (valigetta(), {"fotogrammi": 1, "lcd": LCD_VALIGETTA})
    out["mercato-titolo"] = (titolo_mercato(), {"fotogrammi": 1})
    out["stonks"] = (stonks(drone), {"fotogrammi": 1})
    out["bonus"] = (bonus_soldi(), {"fotogrammi": 1})
    out["canale"] = (canale_del_gioco(immagini("ui/ui-logo")), {"fotogrammi": 1, "bottoni": BOTTONI_TV})
    out["canale-osd"] = (osd_canale(), {"fotogrammi": 1})
    nucleo = _stato("core-2", "Luce del nucleo", immagini)
    out["nucleo"] = (striscia([f for f, _ in nucleo]), {"fotogrammi": len(nucleo), "durate": [d for _, d in nucleo]})
    for oggetto in ("iron-ore", "coal", "iron-plate", "copper-ore", "lead-ore", "titanium-ore"):
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

    for f in dati["fissi"]:
        metti(f["sprite"], f["x"], f["y"])
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
    ritaglio = im.crop((6 * CELLA, 5 * CELLA, 42 * CELLA, 34 * CELLA))
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
