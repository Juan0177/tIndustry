# 0001 — Il server multiplayer sarà in C#

**Stato:** accettata, 2026-10-15

## Contesto

L'obiettivo di lungo periodo è il multiplayer: più giocatori nella stessa fabbrica, ciascuno con un drone che costruisce a distanza, con costruzione in corso (piani di costruzione che avanzano e consumano risorse). Serve un server autoritativo e un client che resti reattivo anche con latenza.

Oggi la simulazione vive in `src/TIndustry.Shared/` (.NET 8, nessuna dipendenza da Godot) e il client Godot la esegue in-process.

## Opzioni

1. **C#, riusando `TIndustry.Shared`.** Client e server eseguono lo stesso codice di simulazione; il client può prevedere il proprio drone e riconciliarsi con le istantanee del server (l'approccio di Mindustry: un solo linguaggio per client e server). Un solo toolchain.
2. **Rust.** Imporrebbe di scrivere la simulazione due volte oppure di avere un client "muto" e laggoso, più un secondo toolchain, per un guadagno di prestazioni non misurato.

## Decisione

Il server sarà un'app **.NET headless** (senza Godot) che riusa `TIndustry.Shared`. Rust è scartato.

## Conseguenze

- `TIndustry.Shared` non deve dipendere da Godot né dal sistema operativo (per questo `SystemMonitor` è stato spostato nel client, `godot/scripts/dev/`).
- La simulazione dovrà essere **deterministica**: stesso stato e stessi comandi, stesso risultato su client e server. Come garantirlo (ordine di tick, generatori casuali, aritmetica in virgola mobile) si progetta nel sotto-progetto 2 (simulazione a comandi e sistemi), insieme alla suddivisione di `Factory` in sistemi.
- Il single player diventerà un "server locale" nello stesso processo; il trasporto di rete arriva nel sotto-progetto 5.
