# Generates docs/mappa-progetto.canvas (Obsidian).
# Edit the cards below and run: python tools/dev/mappa_canvas.py
# Cards are stacked per column and sized to their text, so nothing needs scrolling.
import json
import math
import textwrap

COL_W = 440      # card width
GAP = 24         # vertical gap between cards
PAD = 20         # group padding
COL_GAP = 60     # horizontal gap between columns

nodes, edges = [], []


def card_height(text, width):
    """Rough Obsidian text-card height: ~8.6 px per char at 16 px font, 26 px per line."""
    chars = max(10, int((width - 56) / 8.6))
    lines = 0
    for raw in text.split("\n"):
        plain = raw.replace("**", "").replace("[[", "").replace("]]", "")
        heading = plain.startswith("#")
        n = max(1, math.ceil(len(plain) / chars)) if plain.strip() else 1
        lines += n * (1.5 if heading else 1)
    return int(44 + 26 * lines)


class Column:
    def __init__(self, gid, label, x, color):
        self.gid, self.label, self.x, self.color = gid, label, x, color
        self.y0 = 0
        self.y = self.y0 + PAD + 30
        self.ids = []

    def card(self, cid, text, color=None):
        h = card_height(text, COL_W)
        n = {"id": cid, "type": "text", "text": text, "x": self.x + PAD, "y": self.y,
             "width": COL_W, "height": h}
        if color or self.color:
            n["color"] = color or self.color
        nodes.append(n)
        self.ids.append(cid)
        self.y += h + GAP
        return cid

    def close(self):
        nodes.insert(0, {"id": self.gid, "type": "group", "label": self.label, "x": self.x,
                         "y": self.y0, "width": COL_W + 2 * PAD,
                         "height": self.y - self.y0 + PAD - GAP, "color": self.color})


def chain(ids):
    for a, b in zip(ids, ids[1:]):
        edges.append({"id": f"e-{a}-{b}", "fromNode": a, "fromSide": "bottom",
                      "toNode": b, "toSide": "top"})


def link(a, b):
    edges.append({"id": f"e-{a}-{b}", "fromNode": a, "fromSide": "right",
                  "toNode": b, "toSide": "left"})


X = [0, COL_W + 2 * PAD + COL_GAP]
X += [X[1] * 2, X[1] * 3]

title = "# tIndustry — mappa del progetto\nMindustry × Tiny Industry, Godot 4.4 .NET. Aggiornata il 2026-10-17, sera."
nodes.append({"id": "titolo", "type": "text", "text": title, "x": 0, "y": -170,
              "width": X[3] + COL_W + 2 * PAD, "height": 130, "color": "6"})

# --- Fatto -----------------------------------------------------------------
fatto = Column("g-fatto", "✅ Fatto", X[0], "4")
fatto.card("sp1", "**SP1 · Riordino**\nCartelle, CLAUDE.md, ARCHITETTURA, dev.cmd, ADR 0001.\nPR #139–#146")
fatto.card("sp2", "**SP2 · Simulazione a comandi**\nTick 60/s, GameState e sistemi, SimHost, IGameView, istantanea e checksum, salvataggi v10.\nPR #147–#151 (+#154) · [[2026-10-16-simulazione-comandi-sistemi|diario]]")
fatto.card("extra", "**Rifiniture**\n- salvataggio atomico, doppio input, MajorityDeposit (#155)\n- log dei playtest fuori dal profilo (#156)\n- diario e .md in LF (#157)\n- playtest isolati e backup di Continua (#162)")
fatto.card("docs", "**Documenti**\n- [[ARCHITETTURA]]\n- [[decisioni]] (indice degli ADR)\n- [[docs/diario/README|diario dei lavori]]")
fatto.close()

# --- In corso --------------------------------------------------------------
sp3 = Column("g-sp3", "🔨 In corso · SP3 Costruzione come Mindustry", X[1], "3")
tasks = [
    sp3.card("t0", "**Task 0** · base degli screenshot ✅", "4"),
    sp3.card("t1", "**Task 1** · regole, dati, cantiere ✅\nin main (#159, correzioni in #160)", "4"),
    sp3.card("t2", "**Task 2** · drone, piani, comandi ✅\nin main (#160)", "4"),
    sp3.card("t3", "**Task 3** · salvataggi v11 ✅\nin main (#161)", "4"),
    sp3.card("t4", "**Task 4** · grafica di piani, cantieri e drone ✅\narriva con la PR del Task 5", "4"),
    sp3.card("t5", "**Task 5** · costruzione vera nel gioco, coda nell'HUD, tutorial ⏳\nPR 4"),
    sp3.card("t6", "**Task 6** · documentazione e ADR 0003 ⏳\nPR 5", "0"),
    sp3.card("tf", "**Revisione finale e diario** ⏳", "0"),
]
sp3.card("sp3docs", "**Documenti**\n- [[2026-10-17-costruzione-in-corso-design|spec]]\n- [[2026-10-17-costruzione-in-corso|piano]]")
sp3.close()
chain(tasks)

# --- Prossimi --------------------------------------------------------------
nxt = Column("g-next", "🗺️ Prossimi (ordine proposto)", X[2], "5")
roadmap = [
    nxt.card("g1", "**G1 · Arte dei blocchi**\nPixel-art nostra (generatore in tools/art/): edifici, nastri, oggetti, drone e Core nei 3 tier."),
    nxt.card("tier", "**T + SP4 · Tier e drone comandato**\nCore shard / foundation / nucleus, drone alpha / beta / gamma; il drone lo muovi tu."),
    nxt.card("g2", "**G2 · Terreno**\nPavimenti, bordi, giacimenti, rocce."),
    nxt.card("g3", "**G3 · Effetti e luci**\nParticelle, bloom, ombre, effetti di costruzione."),
    nxt.card("g4", "**G4 · Interfaccia**\nHUD, pannelli e menu con la nuova arte."),
    nxt.card("sp5", "**SP5 · Server C# e co-op**\nSincronizzazione ibrida ([[0002-sincronizzazione-ibrida|ADR 0002]])."),
]
nxt.close()
chain(roadmap)

# --- Aperti ----------------------------------------------------------------
aperti = Column("g-aperti", "⚠️ Aperti", X[3], "1")
aperti.card("a1", "**Salvataggio reale sovrascritto**\nChiuso: le partite erano avviate a mano. Protezione e backup in #162.", "4")
aperti.card("a2", "**Progetti di Claude Code**\nDa indagare dopo il sotto-progetto 3.")
aperti.card("a3", "**Grafica (G1–G4) e tier**\nIn attesa del via dopo il sotto-progetto 3.")
aperti.close()

link("sp2", "t0")
link("tf", "g1")

with open("docs/mappa-progetto.canvas", "w", encoding="utf-8") as fh:
    json.dump({"nodes": nodes, "edges": edges}, fh, ensure_ascii=False, indent=1)
print(f"{len(nodes)} nodi, {len(edges)} collegamenti")
