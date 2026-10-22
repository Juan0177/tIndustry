# Stato del progetto

A che punto siamo e cosa manca. Si aggiorna a ogni PR unita e alla fine di ogni blocco di lavoro; il quadro generale (fatto, prossimi, aperti) è nella [mappa del progetto](mappa-progetto.canvas), la storia nel [diario](diario/README.md) e nel [CHANGELOG](../CHANGELOG.md).

**Aggiornato:** 2026-10-23, dopo la chiusura del Mercato come una borsa (PR #282).

## Ordine di lavoro in corso

Dato dall'utente il 2026-10-22: alla fine di ogni blocco ci si ferma e si chiede conferma.

| Blocco | Cosa | Stato |
| --- | --- | --- |
| A | Controlli veloci: playtest dopo la #271, pulizia di rami e modifiche vecchie, test intermittenti | Fatto, tranne la pulizia (vedi sotto) |
| B | Mercato come una borsa, specifica 1 ([#268](https://github.com/Juan0177/tIndustry/issues/268)) | Fatto: PR #274–#282, [diario](diario/2026-10-22-mercato-borsa.md) |
| C | Nuove specifiche, col brainstorming prima del codice: schermo di borsa e bot della finanza (specifica 2), magazzino del CORE e livelli (specifica 3), campagna rifatta coi valori nuovi | **Prossimo**, in attesa del via dell'utente |
| D | Logistica come Mindustry in un piano unico: nastri stretti variante C ([#252](https://github.com/Juan0177/tIndustry/issues/252)), Junction ([#253](https://github.com/Juan0177/tIndustry/issues/253)), Sorter ([#254](https://github.com/Juan0177/tIndustry/issues/254)), Router ([#255](https://github.com/Juan0177/tIndustry/issues/255)), Estrattore come l'Unloader ([#270](https://github.com/Juan0177/tIndustry/issues/270)) | Da fare, dopo B |
| E | Rete di energia come Mindustry ([#269](https://github.com/Juan0177/tIndustry/issues/269), serve una specifica), telemetria ([#234](https://github.com/Juan0177/tIndustry/issues/234)) | Più avanti |

## Cosa manca, in piccolo

- **Pulizia del blocco A**, approvata dall'utente ma da lanciare a mano, perché la modalità automatica blocca le cancellazioni:
  - i worktree vecchi in `.claude/worktrees/`;
  - i rami locali già uniti;
  - il ramo `sito/fabbrica`, in locale e su GitHub;
  - le cartelle vecchie di `.superpowers/` (`sdd/2026-10-18-sito-fabbrica`, `sdd-backup`, `brainstorm`).
- **`godot/assets/drone-1-elmetto.png`**: è diverso da quello che produce `tools/art/genera.py` (`--controlla` lo segnala) e il suo `.import` manca dal repository. Va sistemato con una piccola PR.
- **Sonda `oggetti-fluidi`** del playtest `ui`: fallisce ogni tanto per un frame lento (1 volta su 4 il 22/10). Rilanciando passa.
- **Server MCP `tindustry` nel gioco vero**: non trova `godot4`. Serve la variabile d'ambiente `TINDUSTRY_GODOT` sull'eseguibile di Godot e un riavvio di Claude Code.

## Cosa serve dall'utente

- Il via per il blocco C.
- Lanciare la pulizia del blocco A, oppure darle il permesso.
