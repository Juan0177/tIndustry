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

title = "# tIndustry — mappa del progetto\nMindustry × Tiny Industry, Godot 4.4 .NET. Aggiornata il 2026-10-18, dopo la G2b."
nodes.append({"id": "titolo", "type": "text", "text": title, "x": 0, "y": -170,
              "width": X[3] + COL_W + 2 * PAD, "height": 130, "color": "6"})

# --- Fatto -----------------------------------------------------------------
fatto = Column("g-fatto", "Fatto", X[0], "4")
fatto.card("sp1", "**SP1 · Riordino**\nCartelle, CLAUDE.md, ARCHITETTURA, dev.cmd, ADR 0001.\nPR #139–#146")
fatto.card("sp2", "**SP2 · Simulazione a comandi**\nTick 60/s, GameState e sistemi, SimHost, IGameView, istantanea e checksum, salvataggi v10.\nPR #147–#151 (+#154) · [[2026-10-16-simulazione-comandi-sistemi|diario]]")
fatto.card("sp3", "**SP3 · Costruzione come Mindustry**\nPiani, drone e cantieri con consegna dal Core, smontaggio con rimborso graduale, coda nell'HUD, salvataggi v11.\nPR #159–#166 · [[2026-10-17-costruzione-in-corso|diario]] · [[0003-costruzione-come-mindustry|ADR 0003]]")
fatto.card("g1", "**G1 · Arte dei blocchi**\nPixel-art nostra generata da tools/art/ (32 px per cella): edifici a strati, nastri a 3 tier, oggetti, Core in 4 tier, drone alpha / beta / gamma, animazioni di base.\nPR #168–#173 · [[0004-arte-generata-da-script|ADR 0004]] · [[2026-10-18-g1-arte-blocchi|piano]]")
fatto.card("g2a", "**G2a · Mappe e terreno**\nMappe a seme in 3 misure (fino a 2000²), 4 biomi, pareti, acqua bassa e profonda, mappe della campagna in file con tools/MapTool, terreno a pezzi nel client, Nuova partita, salvataggi v12.\nPR #175–#180 · [[0005-mappe-seme-e-file|ADR 0005]] · [[2026-10-18-g2a-mappe-terreno-design|spec]]")
fatto.card("g2b", "**G2b · Arte del terreno**\nSuoli con varianti e bordi, pareti in rilievo a doppia griglia, acqua con increspature, giacimenti a puntini, decorazioni, due livelli di dettaglio (Enorme a 240 FPS).\nPR #182–#184 · [[0004-arte-generata-da-script|ADR 0004]] · [[2026-10-18-g2b-arte-terreno-design|spec]]")
fatto.card("extra", "**Rifiniture**\n- salvataggio atomico, doppio input, MajorityDeposit (#155)\n- log dei playtest fuori dal profilo (#156)\n- diario e .md in LF (#157)\n- playtest isolati e backup di Continua (#162)")
fatto.card("docs", "**Documenti**\n- [[ARCHITETTURA]]\n- [[decisioni]] (indice degli ADR, fino a [[0005-mappe-seme-e-file|0005]])\n- [[docs/diario/README|diario dei lavori]]")
fatto.close()

# --- In corso --------------------------------------------------------------
corso = Column("g-corso", "In corso", X[1], "3")
corso.card("g3", "**G3 · Effetti e luci**\nParticelle, ombre, bagliori, notte opzionale, impostazioni grafiche per PC deboli.\n[[2026-10-18-g3-effetti-luci-design|spec]]")
corso.close()

# --- Prossimi --------------------------------------------------------------
nxt = Column("g-next", "Prossimi (in ordine)", X[2], "5")
roadmap = [
    "g3",
    nxt.card("g4", "**G4 · Interfaccia**\nStile Mindustry, pixel font nostro, lingue, scala e accessibilità.\n[[2026-10-18-g4-interfaccia-design|spec]]"),
    nxt.card("tier", "**SP4 · Tier e drone comandato**\nGameplay dei 4 Core e dei droni alpha / beta / gamma; il drone lo muovi tu."),
    nxt.card("liquidi", "**Liquidi**\nPompe, tubi, serbatoi e ricette con l'acqua."),
    nxt.card("sp5", "**SP5 · Server C# e co-op**\nSincronizzazione ibrida ([[0002-sincronizzazione-ibrida|ADR 0002]])."),
]
nxt.close()
chain(roadmap)

# --- Aperti ----------------------------------------------------------------
aperti = Column("g-aperti", "Aperti", X[3], "1")
aperti.card("a1", "**Salvataggio reale sovrascritto**\nChiuso: le partite erano avviate a mano. Protezione e backup in #162.", "4")
aperti.card("a2", "**Progetti di Claude Code**\nIndagati: restiamo con le sessioni locali.", "4")
aperti.card("a3", "**Grafica G1–G4**\nSpec approvate il 2026-10-18 (#167). G1, G2a e G2b finite; basi degli screenshot candidate in .dev/baseline-g1-candidata, .dev/baseline-g2a-candidata e .dev/baseline-g2b-candidata, da approvare.")
aperti.close()

chain(["g2a", "g2b"])
link("g2b", "g3")


def obsidian_json(items):
    """One compact object per line, tab-indented: the layout Obsidian writes when it saves a canvas."""
    return ",\n".join("\t\t" + json.dumps(i, ensure_ascii=False, separators=(",", ":")) for i in items)


with open("docs/mappa-progetto.canvas", "w", encoding="utf-8", newline="\n") as fh:
    fh.write('{\n\t"nodes":[\n' + obsidian_json(nodes) + '\n\t],\n\t"edges":[\n'
             + obsidian_json(edges) + '\n\t]\n}')
print(f"{len(nodes)} nodi, {len(edges)} collegamenti")
