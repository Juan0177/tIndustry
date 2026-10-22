# Stato del progetto

A che punto siamo e cosa manca. **Ogni modifica al progetto aggiorna, in main, questo file e la [mappa del progetto](mappa-progetto.canvas)**; la storia sta nel [diario](diario/README.md) e nel [CHANGELOG](../CHANGELOG.md).

Le caselle qui sotto sono la fonte della barra di avanzamento che compare nel README, nella Wiki e nella pagina Sviluppo del sito: dopo ogni modifica si lancia `python tools/sito/stato.py`, che riscrive le barre (la CI controlla che siano allineate).

**Aggiornato:** 2026-10-23, barra di avanzamento e logo nei titoli dei documenti.

## Ordine di lavoro in corso

Dato dall'utente il 2026-10-22: alla fine di ogni blocco ci si ferma e si chiede conferma.

### A · Controlli veloci

- [x] A1 · Playtest `ui` e `tutorial` dopo la #271, testi nei riquadri anche in inglese
- [ ] A2 · Pulizia di rami, worktree e cartelle vecchie (approvata, da lanciare a mano)
- [x] A3 · Test di Shared intermittenti (nessuno trovato in tre giri)

### B · Mercato come una borsa

Specifica 1 ([#268](https://github.com/Juan0177/tIndustry/issues/268)), [diario](diario/2026-10-22-mercato-borsa.md).

- [x] Task 1 · Valori base e regole nei dati (#274)
- [x] Task 2 · Andamento dei prezzi e picchi (#275)
- [x] Task 3 · Vendite, calo e crollo; sito allineato (#276, #273)
- [x] Task 4 · Jackpot, pity e attesa (#277)
- [x] Task 5 · Vendita automatica per oggetto (#278)
- [x] Task 6 · Salvataggi v14 (#279)
- [x] Task 7 · Vista per il client e per gli agenti (#280)
- [x] Task 8 · Schermata panoramica (#281)
- [x] Prova di 15 minuti e parere dell'utente
- [x] Task 9 · Documenti, diario e revisione finale (#282)

### C · Nuove specifiche del Mercato

Prima il brainstorming e la specifica approvata, poi il codice. In attesa del via dell'utente.

- [ ] C1 · Specifica 2: schermo di borsa e bot della finanza
- [ ] C2 · Specifica 3: magazzino del CORE e livelli
- [ ] C3 · Campagna rifatta coi valori del Mercato nuovo

### D · Logistica come Mindustry

Un piano unico, dopo il blocco B.

- [ ] D1 · Nastri stretti, variante C ([#252](https://github.com/Juan0177/tIndustry/issues/252))
- [ ] D2 · Junction ([#253](https://github.com/Juan0177/tIndustry/issues/253))
- [ ] D3 · Sorter con l'alternanza sinistra/destra ([#254](https://github.com/Juan0177/tIndustry/issues/254))
- [ ] D4 · Router ([#255](https://github.com/Juan0177/tIndustry/issues/255))
- [ ] D5 · Estrattore come l'Unloader ([#270](https://github.com/Juan0177/tIndustry/issues/270))

### E · Più avanti

- [ ] E1 · Rete di energia come Mindustry, con la sua specifica ([#269](https://github.com/Juan0177/tIndustry/issues/269))
- [ ] E2 · Telemetria, overlay delle risorse ([#234](https://github.com/Juan0177/tIndustry/issues/234))

## Cosa manca, in piccolo

Fuori dalla barra: cose piccole da non perdere.

- **Pulizia del blocco A**, approvata dall'utente ma da lanciare a mano, perché la modalità automatica blocca le cancellazioni:
  - i worktree vecchi in `.claude/worktrees/`;
  - i rami locali già uniti;
  - il ramo `sito/fabbrica`, in locale e su GitHub;
  - le cartelle vecchie di `.superpowers/` (`sdd/2026-10-18-sito-fabbrica`, `sdd-backup`, `brainstorm`).
- **Sonda `oggetti-fluidi`** del playtest `ui`: fallisce ogni tanto per un frame lento (1 volta su 4 il 22/10). Rilanciando passa.
- **Server MCP `tindustry` nel gioco vero**: non trova `godot4`. Serve la variabile d'ambiente `TINDUSTRY_GODOT` sull'eseguibile di Godot e un riavvio di Claude Code.

## Cosa serve dall'utente

- Il via per il blocco C.
- Lanciare la pulizia del blocco A, oppure darle il permesso.
