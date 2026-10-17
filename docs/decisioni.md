# Decisioni

Indice delle decisioni del progetto. Le decisioni architetturali (ADR) stanno in `docs/decisioni/`. Le scelte prese durante i lavori si trovano nel [diario](diario/README.md), una voce per piano.

## Decisioni architetturali (ADR)

| ADR | Decisione | Stato |
|---|---|---|
| [0001 — Il server multiplayer sarà in C#](decisioni/0001-server-csharp.md) | Il server per il co-op riusa la simulazione `TIndustry.Shared` in C#, senza un secondo linguaggio. | accettata, 2026-10-15 |
| [0002 — Sincronizzazione multiplayer ibrida](decisioni/0002-sincronizzazione-ibrida.md) | Un giocatore ospita la partita e ordina i comandi. Tutti simulano in modo deterministico. Checksum per accorgersi delle divergenze, istantanea per recuperare o per entrare a partita in corso. | accettata, 2026-10-15 |
| 0003 — Costruzione come Mindustry | Piani, drone automatico, cantieri, consumo e rimborso graduali. Arriva con la documentazione del sotto-progetto 3. | in scrittura |

## Scelte di prodotto (dalle conversazioni)

- **Co-op**: cooperativo, ospitato da un giocatore, con il giocatore come piccolo drone, come in Mindustry.
- **Costruzione**: il più simile possibile a Mindustry. Rimborso allo smontaggio al 100% in partita base e sandbox; in campagna è fissato per livello (100% nei livelli iniziali, 50% dopo).
- **Grafica**: pixel-art originale nostra, generata da script, in stile Mindustry. Nessun file di Mindustry e nessun cambio di licenza.
- **Tier**: il Core ha tre livelli (shard, foundation, nucleus) e il drone tre tier (alpha, beta, gamma), con effetti sul gameplay come in Mindustry. Saranno pianificati con il sotto-progetto 4.
- **CI**: GitHub Actions solo manuali o su tag di release.
- **Dati del giocatore**: mai toccati da sviluppo e playtest, che usano sempre un profilo temporaneo.
