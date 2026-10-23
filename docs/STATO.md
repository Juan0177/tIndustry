# Stato del progetto

A che punto siamo con **tutti gli aspetti del gioco** e cosa manca. **Ogni modifica al progetto aggiorna, in main, questo file e la [mappa del progetto](mappa-progetto.canvas)**; la storia sta nel [diario](diario/README.md) e nel [CHANGELOG](../CHANGELOG.md).

Le caselle della prossima sezione sono la fonte della barra di avanzamento che compare nel README, nella Wiki e nella pagina Sviluppo del sito: ogni aspetto è un pezzo della barra, con la sua percentuale, e il titolo porta alla sua documentazione. Dopo ogni modifica si lancia `python tools/sito/stato.py`, che riscrive le barre; la CI controlla che siano allineate. Un aspetto ancora da progettare ha quattro passi: specifica approvata, piano, implementazione, documenti e diario.

**Aggiornato:** 2026-10-23, giorno e notte approvato; pausa, play.ps1, HUD nascosto nella Home e schermata di caricamento.

## Aspetti del gioco

### 1 · [Fondamenta tecniche](ARCHITETTURA.md)

- [x] Riordino del progetto (sotto-progetto 1, PR #139–#146)
- [x] Simulazione a comandi, tick fisso e checksum (sotto-progetto 2, PR #147–#154)
- [x] Salvataggi con versione, fino alla v14 (PR #279)
- [x] Agenti che giocano col server MCP (PR #210–#217)
- [x] Avvio unico play.ps1 (utente o dev) con installazione delle dipendenze
- [ ] Pulizia di rami, worktree e cartelle vecchie (approvata, da lanciare a mano)
- [ ] Sonda `oggetti-fluidi` del playtest `ui` stabile

### 2 · [Costruzione](wiki/Costruzione.md)

- [x] Costruzione come Mindustry: piani, drone e cantieri (sotto-progetto 3, PR #159–#166)
- [x] Trascinamento, linea con ponte automatico, rettangolo di smontaggio (PR #229–#232)

### 3 · [Logistica](wiki/Logistica.md)

- [x] Nastri come Mindustry: portata, oggetti fluidi, giro sul posto, tier (PR #224–#246)
- [x] Nastro rinforzato (PR #240)
- [ ] Nastri stretti, variante C ([#252](https://github.com/Juan0177/tIndustry/issues/252))
- [ ] Junction ([#253](https://github.com/Juan0177/tIndustry/issues/253))
- [ ] Sorter con l'alternanza sinistra/destra ([#254](https://github.com/Juan0177/tIndustry/issues/254))
- [ ] Router ([#255](https://github.com/Juan0177/tIndustry/issues/255))
- [x] Container e vault (magazzini collegati al CORE o depositi a sé)
- [ ] Estrattore come l'Unloader, configurato col clic ([#270](https://github.com/Juan0177/tIndustry/issues/270))

### 4 · [Produzione](wiki/Produzione.md)

- [x] Minatori, forni, assemblatori ed estrattori
- [x] Titanio e Minatore T2

### 5 · [Energia](wiki/Energia.md)

- [x] Generatori, nodi di potenza e Lampada
- [ ] Rete di nodi come Mindustry: specifica approvata ([#269](https://github.com/Juan0177/tIndustry/issues/269))
- [ ] Rete di nodi come Mindustry: piano
- [ ] Rete di nodi come Mindustry: implementazione
- [ ] Rete di nodi come Mindustry: documenti e diario

### 6 · [Mercato ed economia](wiki/Mercato-come-una-borsa.md)

- [x] Mercato come una borsa, specifica 1 (PR #274–#282, [diario](diario/2026-10-22-mercato-borsa.md))
- [ ] Schermo di borsa e bot della finanza, specifica 2: specifica approvata
- [ ] Schermo di borsa e bot della finanza: implementazione
- [x] Magazzino del CORE e livelli, specifica 3: specifica approvata ([spec](superpowers/specs/2026-10-23-magazzino-core-design.md))
- [x] Magazzino del CORE e livelli: implementazione ([piano](superpowers/plans/2026-10-23-magazzino-core.md), [diario](diario/2026-10-23-magazzino-core.md))

### 7 · [Mappe e terreno](wiki/Mappe-e-terreno.md)

- [x] Mappe a seme, biomi, pareti e acqua (G2a, PR #175–#180)
- [x] Arte del terreno (G2b, PR #182–#184)
- [ ] Giacimenti per bioma, generatore biomi-2: specifica approvata ([spec](superpowers/specs/2026-10-18-biomi-2-giacimenti-design.md), [#204](https://github.com/Juan0177/tIndustry/pull/204))
- [ ] Giacimenti per bioma: piano
- [ ] Giacimenti per bioma: implementazione
- [ ] Giacimenti per bioma: documenti e diario

### 8 · [Campagna](wiki/Campagna-e-mappe.md)

- [x] Livelli della campagna con le loro mappe
- [ ] Campagna rifatta coi valori del Mercato nuovo

### 9 · [Grafica ed effetti](wiki/Arte-generata.md)

- [x] Arte dei blocchi generata da script (G1, PR #168–#173)
- [x] Effetti, ombre e luci (G3, PR #186–#189)
- [ ] Effetti e luci approvati dall'utente

### 10 · [Interfaccia](wiki/Interfaccia-e-lingue.md)

- [x] Tema, carattere, lingue, Scala UI e colori accessibili (G4, PR #194–#201)
- [x] Panoramica del Mercato (PR #281)
- [x] Pausa con Spazio e velocità ×1/×5/×10/×20 in modalità dev
- [x] HUD nascosto nella Home e schermata di caricamento all'apertura di una partita
- [ ] Telemetria, overlay delle risorse ([#234](https://github.com/Juan0177/tIndustry/issues/234))

### 11 · [Drone e tier](https://github.com/Juan0177/tIndustry#stato-e-roadmap)

Sotto-progetto 4: il drone lo comandi tu. I CORE shard → foundation → nucleus e i droni alpha → beta → gamma sono passati alla [specifica 3 del Mercato](superpowers/specs/2026-10-23-magazzino-core-design.md) (aspetto 6).

- [ ] Specifica approvata
- [ ] Piano
- [ ] Implementazione
- [ ] Documenti e diario

### 12 · [Liquidi](https://github.com/Juan0177/tIndustry#stato-e-roadmap)

- [ ] Specifica approvata
- [ ] Piano
- [ ] Implementazione
- [ ] Documenti e diario

### 13 · [Attacco e difesa](https://github.com/Juan0177/tIndustry#stato-e-roadmap)

Nemici solo di notte, Game Over se cade il CORE.

- [x] Giorno e notte: specifica approvata ([spec](superpowers/specs/2026-10-23-giorno-notte-design.md))
- [ ] Giorno e notte: ciclo, solo giorno, solo notte col contatore del primo attacco ([piano](superpowers/plans/2026-10-23-giorno-notte.md))
- [ ] Specifica approvata
- [ ] Piano
- [ ] Implementazione
- [ ] Documenti e diario

### 14 · [Co-op](decisioni/0002-sincronizzazione-ibrida.md)

Sotto-progetto 5: server C# e sincronizzazione ibrida.

- [ ] Specifica approvata
- [ ] Piano
- [ ] Implementazione
- [ ] Documenti e diario

### 15 · [Sito web](wiki/Sito-web.md)

- [x] Vetrina «fabbrica», pagine Sviluppo e Asset (PR #233, #235)
- [x] Racconto guidato dallo scroll pubblicato in `racconto/`
- [x] Mercato del sito allineato alla borsa (PR #276)
- [x] Barra di avanzamento nella pagina Sviluppo (PR #284)
- [x] Logo nella barra in alto
- [x] Catena della vetrina intera su schermo stretto: i nastri proseguono dietro il testo fino alla sezione dopo ([#287](https://github.com/Juan0177/tIndustry/issues/287))
- [ ] Racconto approvato dall'utente e messo nella barra del sito ([spec](superpowers/specs/2026-10-20-sito-scroll-narrativo.md))

### 16 · [Wiki e README](wiki/Home.md)

- [x] Pagine per chi gioca
- [x] Pagine per chi sviluppa
- [x] Mercato come una borsa e barra di avanzamento
- [ ] Pagine dello schermo di borsa e del magazzino del CORE, quando arrivano
- [x] Banner del README col logo pixel, generato da `tools/art/immagini_documenti.py`
- [ ] README rifatto da capo

## Ordine di lavoro in corso

Dato dall'utente il 2026-10-22: alla fine di ogni blocco ci si ferma e si chiede conferma. Fra parentesi l'aspetto della barra a cui appartiene.

| Blocco | Cosa | Stato |
| --- | --- | --- |
| A | Controlli veloci, pulizia, test intermittenti (1) | Fatto, tranne la pulizia |
| B | Mercato come una borsa, specifica 1 (6) | Fatto: PR #274–#282 |
| C | Specifiche 2 e 3 del Mercato (6), campagna rifatta (8): prima il brainstorming | **In corso**: specifica 3 attuata (2026-10-23, [diario](diario/2026-10-23-magazzino-core.md)); prossime la specifica 2 (schermo di borsa) e la campagna rifatta |
| D | Logistica come Mindustry: #252–#255, #270 (3) | Da fare, dopo B |
| E | Rete di energia #269 (5), telemetria #234 (10) | Più avanti |

## Cosa manca, in piccolo

- **Pulizia del blocco A**, approvata dall'utente ma da lanciare a mano, perché la modalità automatica blocca le cancellazioni:
  - i worktree vecchi in `.claude/worktrees/`;
  - i rami locali già uniti;
  - il ramo `sito/fabbrica`, in locale e su GitHub;
  - le cartelle vecchie di `.superpowers/` (`sdd/2026-10-18-sito-fabbrica`, `sdd-backup`, `brainstorm`).
- **Controllo del client sul PC di casa** (il lavoro del magazzino del CORE è stato fatto in cloud, senza Godot): importare il progetto (`--import`) e generare i `.import` degli 8 PNG nuovi, lanciare i playtest `ui` e `agenti`, il `PlaytestScala` (una categoria «Magazzino» nuova nella palette), rifare le catture (la riga `core.livello` dell'HUD non va a capo e cambia le immagini) e controllare le celle di prova del playtest grafico (`World.PlaytestGrafica.cs`), che ora toccano il CORE 3×3.
- **Sonda `oggetti-fluidi`** del playtest `ui`: fallisce ogni tanto per un frame lento (1 volta su 4 il 22/10). Rilanciando passa.
- **Server MCP `tindustry` nel gioco vero**: non trova `godot4`. Serve la variabile d'ambiente `TINDUSTRY_GODOT` sull'eseguibile di Godot e un riavvio di Claude Code.

## Cosa serve dall'utente

- Lanciare la pulizia del blocco A, oppure darle il permesso.
- Il tuo parere su un aspetto distinto per gli sprite di foundation e nucleus: oggi hanno gli stessi pixel dei vecchi CORE a 2 livelli, quindi i tre CORE si distinguono solo per misura.
- Provare il client del magazzino del CORE sul PC di casa (vedi «Cosa manca, in piccolo»).
- L'ok su effetti e luci (G3) e, quando vuoi, sul racconto del sito.
