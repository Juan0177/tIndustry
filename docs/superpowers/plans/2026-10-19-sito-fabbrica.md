# Sito «fabbrica»: piano di implementazione

**Stato:** completato e pubblicato il 2026-10-19 (PR #233 e #235), vedi il [diario](../../diario/2026-10-19-sito-fabbrica.md).

**Goal:** riscrivere il sito di GitHub Pages come una fabbrica di tIndustry secondo la [specifica](../specs/2026-10-19-sito-fabbrica-design.md) e pubblicarlo.

**Architecture:** `costruisci.py` orchestra tre pagine e i file statici; `fabbrica.py` disegna le scene dal modello a celle; `documenti.py` legge README, diario e ADR; `stile.css` e `sito.js` sono sorgenti copiati. Immagini delle scene in `fabbrica/`, catture in `catture/`.

**Tech Stack:** Python 3.12 + Pillow (workflow Pages), HTML/CSS/JS senza dipendenze, `unittest`, `node --test` in locale.

**Esecuzione:** un'unica sessione con carta bianca dell'utente; i task 2 e 3 sono affidati ad agenti in parallelo (workflow), gli altri alla sessione; la revisione finale è un workflow a più lenti con verifica avversaria. Un solo ramo (`claude/exciting-ride-dmceyv`) e una sola PR, con un commit per task (deroga, chiesta dall'utente, alla regola «una PR per task»).

### Task 1: `fabbrica.py` e scene della vetrina
- [x] Modello (`Tratto`, `Pezzo`, `Blocco`, `Giacimento`, `Nodo`, `Linea`, `Terreno`, `Scena`), trasformazioni delle curve come il client, immagini in `fabbrica/`, HTML delle scene; sezioni della vetrina come dati.
- [x] `test_fabbrica.py`: connettività delle catene, curve, nessuna sovrapposizione, determinismo, immagini scritte.

### Task 2: `documenti.py` e pagina sviluppo (agente)
- [x] Tabelle del README, diario, ADR, versione, Markdown minimo, `sezioni_sviluppo(radice)`; `test_documenti.py` sulle fonti vere.

### Task 3: `sito.js` (drone affidato a un agente)
- [x] `adatta()`, `scorri()`, barra; drone (vaga, costruisce, naviga; `intercetta` pura); `test_sito.mjs`.

### Task 4: guscio, `stile.css`, pagine in `costruisci.py`
- [x] Guscio e classi della specifica; vetrina (testi rivisti, Sotto attacco con avvisi neon, bordo rosso e vetro sparato); sviluppo; asset nel guscio nuovo; `test_costruisci.py` aggiornato; `pages.yml` esegue tutti i test.

### Task 5: revisione esaustiva (workflow)
- [x] Catture Playwright a 1920/1280/390, script spenti, reduced motion; revisori (codice, test, testi per il pubblico, accessibilità, convenzioni di CLAUDE.md, fedeltà al gioco); verifica avversaria; correzioni fino a esaurimento.

### Task 6: documenti e pubblicazione
- [x] ARCHITETTURA, README (roadmap), CHANGELOG, canvas, diario; commit, push, PR, merge, `pages.yml` da `main`, controllo del sito online.
