# G2b — Arte del terreno

- **Stato**: approvata il 2026-10-18
- **Roadmap grafica**: [G1](2026-10-18-g1-arte-blocchi-design.md) → [G2a](2026-10-18-g2a-mappe-terreno-design.md) → **G2b** → [G3](2026-10-18-g3-effetti-luci-design.md) → [G4](2026-10-18-g4-interfaccia-design.md)

## Obiettivo

Dare al terreno della G2a l'aspetto di Mindustry con la stessa pixel-art generata da script della G1: suoli dei 4 biomi, bordi tra suoli diversi, pareti che si raccordano, acqua bassa e profonda, giacimenti a puntini.

## Decisioni prese con l'utente

- Stile, risoluzione (32 px per cella, ×2 senza filtro) e strumento come la G1 (`tools/art/`, approccio A).
- Biomi: rocce e basalto, sabbia e deserto, neve e ghiaccio, prati e paludi; più biomi nella stessa mappa.
- Giacimenti: **puntini** del colore del minerale sparsi sulla cella, il suolo resta visibile.
- Pareti per bioma, con bordi raccordati alle celle vicine.

## Contenuto

- **Suoli**: per ogni suolo 3–4 varianti scelte dalla posizione della cella (deterministico, senza ripetizioni evidenti).
- **Bordi**: dove due suoli si toccano, un bordo frastagliato del suolo "più alto" sopra quello "più basso" (ordine fisso per suolo), come Mindustry.
- **Pareti**: forme con autotiling a 47 casi o a "doppia griglia" (dual grid), scelto nel piano: le pareti si uniscono in massi e creste continue, con ombra verso il basso a destra.
- **Acqua**: bassa e profonda con bordo verso la riva; animazione lenta a 4 fotogrammi (increspature), ferma con l'impostazione Effetti spenti (G3).
- **Giacimenti**: 5 minerali, 3 varianti di puntini ciascuno.
- **Decorazioni**: sassi e cespugli sparsi solo grafici (non bloccano), con densità per bioma.

## Integrazione nel client

- Terreno disegnato a chunk (dalla G2a): ogni chunk compone una volta suolo, bordi, pareti, giacimenti e decorazioni in una texture; si ricompone solo se cambia (oggi il terreno non cambia in partita).
- Cache dei chunk vicini alla telecamera, liberati quando lontani: memoria limitata anche sulla mappa enorme.
- La minimappa (se presente) e la schermata Nuova partita mostrano un'anteprima a colori medi per suolo.

## Verifica

- Generatore: uscite identiche a ogni esecuzione; copertura (ogni suolo, parete e giacimento del contenuto ha i suoi sprite).
- Prestazioni: frequenza stabile scorrendo la mappa enorme a zoom minimo; memoria del client sotto una soglia fissata nel piano.
- Tavola in `.dev/arte/` (suoli, bordi, pareti, acqua, giacimenti per bioma) e catture del gioco per la revisione dell'utente. Nuova base delle catture dopo il suo ok.

## Documenti

ADR 0004 aggiornato (terreno), ARCHITETTURA (come aggiungere un suolo con il suo sprite), diario e mappa Obsidian.
