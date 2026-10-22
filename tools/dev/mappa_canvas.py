# Generates docs/mappa-progetto.canvas (Obsidian).
# Edit the cards below and run: python tools/dev/mappa_canvas.py
# Cards are stacked per column and sized to their text, so nothing needs scrolling.
import json
import math
import os
import sys
import textwrap

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "sito"))
import stato  # noqa: E402  (progress of docs/STATO.md, shown on the "In corso" card)

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

title = "# tIndustry — mappa del progetto\nDal giacimento al mercato: logistica come Mindustry e mercato come una borsa, Godot 4.4 .NET. Aggiornata il 2026-10-23, dopo il Mercato come una borsa."
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
fatto.card("g3", "**G3 · Effetti e luci**\nParticelle di lavoro, costruzione e ambiente (pool di CPUParticles2D, una riga di dati per effetto), ombre, bagliore, notte opzionale con la Lampada, impostazioni grafiche per PC deboli (effetti, limite FPS, scala di rendering).\nPR #186–#189 · [[0004-arte-generata-da-script|ADR 0004]] · [[2026-10-18-g3-effetti-luci-design|spec]]")
fatto.card("g4", "**G4 · Interfaccia**\nTema unico in stile Mindustry, carattere pixel e icone generati, Scala UI solo dell'interfaccia, schermo intero all'avvio, italiano e inglese con i rifiuti come codici, colori per daltonici e alto contrasto, logo e mappa che scorre nella Home.\nPR #194–#201 · [[0006-interfaccia-lingue-carattere|ADR 0006]] · [[2026-10-18-g4-interfaccia-design|spec]]")
fatto.card("agenti", "**Agenti che giocano**\nServer MCP tindustry (tools/AgentPlay): simulazione, gioco vero comandato da AgentBridge (solo sviluppo, profilo temporaneo, segreto), co-op segnaposto; catture, input, diario e rigioca; skill gioca-e-testa.\nPR #210–#214, #217 · [[2026-10-19-agenti-giocano|diario]] · [[0008-agenti-che-giocano|ADR 0008]] · [[2026-10-18-agenti-giocano-design|spec]]")
fatto.card("web", "**Sito «fabbrica» su GitHub Pages**\nVetrina che è una fabbrica del gioco (catene vere sui nastri, due lunghe nel Core col download, teaser delle unità, drone che costruisce i bottoni), pagina sviluppo (ADR, roadmap, diario) e asset, tutto da tools/sito/costruisci.py; workflow Pages a mano.\nPR #233, #235 · [[2026-10-19-sito-fabbrica|diario]] · [[2026-10-19-sito-fabbrica-design|spec]]")
fatto.card("nastri-mindustry", "**Nastri come Mindustry**\nGiro sul posto e cambio di tier, costi in minerale grezzo, portata e spaziatura di Mindustry (3 oggetti per cella), oggetti fluidi, linea di piazzamento in Shared con ponte automatico e Ctrl, trascinamento con anteprima, rettangolo di smontaggio, tasto centrale e rotellina, nastro rinforzato; salvataggi v13.\nPR #224, #227–#232, #240, #242, #245, #246 · [[docs/diario/2026-10-19-nastri-mindustry|diario]] · [[docs/superpowers/plans/2026-10-19-nastri-mindustry|piano]] · [[2026-10-18-nastri-mindustry-design|spec]]")
fatto.card("mercato-borsa", "**Mercato come una borsa (spec 1 di 3)**\nPrezzi per oggetto con picchi e crolli, vendite al prezzo del clic, vendita automatica per oggetto, jackpot e rete di sicurezza nella simulazione, salvataggi v14, panoramica del Mercato. Il jackpot in partita aspetta lo schermo di borsa.\nPR #274–#282 · [[2026-10-22-mercato-borsa|diario]] · [[2026-10-22-mercato-borsa-design|spec]]")
fatto.card("extra", "**Rifiniture**\n- salvataggio atomico, doppio input, MajorityDeposit (#155)\n- log dei playtest fuori dal profilo (#156)\n- diario e .md in LF (#157)\n- playtest isolati e backup di Continua (#162)")
fatto.card("docs", "**Documenti**\n- [[ARCHITETTURA]]\n- [[decisioni]] (indice degli ADR, fino a [[0008-agenti-che-giocano|0008]])\n- [[docs/diario/README|diario dei lavori]]")
fatto.close()

# --- In corso --------------------------------------------------------------
corso = Column("g-corso", "In corso", X[1], "3")
_blocchi = stato.blocchi()
_fatti, _tutti = stato.totale(_blocchi)
corso.card("stato", f"**Stato di oggi → [[STATO]] · {stato.percento(_fatti, _tutti)}%**\n"
           + "\n".join(f"- {k} · {t}: {stato.percento(f, n)}% ({f}/{n})" for k, t, _, f, n in _blocchi)
           + "\nLe cose piccole che mancano e cosa serve dall'utente sono in docs/STATO.md.")
corso.close()

# --- Prossimi --------------------------------------------------------------
nxt = Column("g-next", "Prossimi (in ordine)", X[2], "5")
roadmap = [
    nxt.card("borsa2", "**Mercato 2 · Schermo di borsa**\nBlocco accanto al CORE, sequenza del Vendi tutto col drone, LED, moduli e ramo Finanza della Ricerca; il jackpot scatta in partita."),
    nxt.card("borsa3", "**Mercato 3 · Magazzino del CORE e livelli**\nMassimo per oggetto, linee che si fermano a CORE pieno, drone più veloce per livello."),
    nxt.card("giacimenti", "**Bilanciamento dei giacimenti (generatore biomi-2)**\nRarità per minerale, titanio più lontano dal Core, partenza più ricca; il titanio si scava già solo col Minatore T2."),
    nxt.card("tier", "**SP4 · Tier e drone comandato**\nGameplay dei 4 Core e dei droni alpha / beta / gamma; il drone lo muovi tu."),
    nxt.card("liquidi", "**Liquidi**\nPompe, tubi, serbatoi e ricette con l'acqua."),
    nxt.card("attacco", "**Attacco e difesa**\nUnità nemiche e difese. I nemici arrivano solo di notte: quando torna il giorno i superstiti scappano, se il CORE ha resistito; se lo distruggono è Game Over. Il drone si mette l'elmetto."),
    nxt.card("sp5", "**SP5 · Server C# e co-op**\nSincronizzazione ibrida ([[0002-sincronizzazione-ibrida|ADR 0002]])."),
]
nxt.close()
chain(roadmap)

# --- Aperti ----------------------------------------------------------------
aperti = Column("g-aperti", "Aperti", X[3], "1")
aperti.card("a1", "**Salvataggio reale sovrascritto**\nChiuso: le partite erano avviate a mano. Protezione e backup in #162.", "4")
aperti.card("a2", "**Progetti di Claude Code**\nIndagati: restiamo con le sessioni locali.", "4")
aperti.card("a3", "**Grafica G1–G4**\nSpec approvate il 2026-10-18 (#167). G1, G2a, G2b, G3 e G4 finite; basi degli screenshot candidate in .dev/baseline-g1-candidata, .dev/baseline-g2a-candidata, .dev/baseline-g2b-candidata, .dev/baseline-g3-candidata e .dev/baseline-g4-candidata (it e en), da approvare.")
aperti.card("nastri-stretti", "**Nastri stretti: scelta la C «Mindustry»**\nScelta dall'utente il 2026-10-20 fra le tre proposte della PR #246; da portare negli sprite con un piano a parte (#252). Insieme: incrocio come la Junction (#253), selezionatore come il Sorter (#254), sdoppiatore che diventa il Router di Mindustry con id router (#255).")
aperti.close()

chain(["g2a", "g2b", "g3", "g4"])
link("g4", "web")
link("agenti", "nastri-mindustry")


def obsidian_json(items):
    """One compact object per line, tab-indented: the layout Obsidian writes when it saves a canvas."""
    return ",\n".join("\t\t" + json.dumps(i, ensure_ascii=False, separators=(",", ":")) for i in items)


with open("docs/mappa-progetto.canvas", "w", encoding="utf-8", newline="\n") as fh:
    fh.write('{\n\t"nodes":[\n' + obsidian_json(nodes) + '\n\t],\n\t"edges":[\n'
             + obsidian_json(edges) + '\n\t]\n}')
print(f"{len(nodes)} nodi, {len(edges)} collegamenti")
