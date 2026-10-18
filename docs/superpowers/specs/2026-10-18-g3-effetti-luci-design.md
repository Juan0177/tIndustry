# G3 — Effetti, ombre e luci

- **Stato**: approvata il 2026-10-18
- **Roadmap grafica**: [G1](2026-10-18-g1-arte-blocchi-design.md) → [G2a](2026-10-18-g2a-mappe-terreno-design.md) → [G2b](2026-10-18-g2b-arte-terreno-design.md) → **G3** → [G4](2026-10-18-g4-interfaccia-design.md)

## Obiettivo

Gli effetti di Mindustry: particelle del lavoro dei blocchi, della costruzione e dell'ambiente, ombre sotto gli edifici, bagliori veri e notte opzionale, con impostazioni grafiche per i PC meno potenti. Solo aspetto: la simulazione non cambia e gli effetti non influenzano il determinismo.

## Decisioni prese con l'utente

| Tema | Decisione |
|---|---|
| Effetti | Tutti, come Mindustry: lavoro dei blocchi, costruzione, ambiente, ombre. |
| Luci | Bagliori veri; **notte opzionale** per mappa o livello (le luci dei blocchi contano al buio). Nessun ciclo giorno/notte automatico. |
| Prestazioni | Impostazione **Effetti: Alti / Bassi / Spenti** (predefinito Alti) più altre impostazioni grafiche per dispositivi deboli. |

## Contenuto

- **Lavoro dei blocchi**: fumo dal forno, scintille dalla trivella, vapore dal generatore, polvere quando un oggetto entra nel Core.
- **Costruzione**: raggio del drone più ricco, scintille sul cantiere, lampo e polvere a lavoro finito, sbriciolamento allo smontaggio.
- **Ambiente** (per bioma): granelli di sabbia, fiocchi di neve, lucciole o foglie sui prati, increspature dell'acqua.
- **Ombre**: ombra morbida sotto edifici, Core e pareti (offset fisso in basso a destra, come Mindustry).
- **Luci**: bagliore (glow 2D di Godot con HDR 2D) su forni, generatori, Core, lampade e luci del drone; con la notte, uno strato scuro sulla mappa e luci dei blocchi che lo "bucano". Un blocco **Lampada** semplice (costo e ricerca nel piano) per illuminare al buio.
- La notte si attiva per livello (`data/campaign.json`) o come opzione della partita normale.

## Architettura (client)

- Un **gestore degli effetti** unico (pool di particelle riusate, limite massimo per schermata) a cui i visual chiedono un effetto per nome e posizione. Gli effetti sono definiti come dati (`godot/` o `data/`, scelto nel piano) per aggiungerne senza codice.
- Gli eventi della simulazione già esistenti (`ConstructFinished`, `DeconstructFinished`, consegne al Core) e lo stato dei blocchi già letto dal client fanno partire gli effetti. Nessun dato nuovo nella simulazione.
- Gli effetti partono solo nel visibile (più un margine), così le mappe enormi non costano.

## Impostazioni grafiche

- **Effetti**: Alti (tutto), Bassi (meno particelle, niente effetti d'ambiente), Spenti (nessuna particella, acqua ferma).
- **Ombre**: sì/no. **Bagliore**: sì/no. **Animazioni dei blocchi**: sì/no.
- **Limite FPS** e **V-Sync**; **scala di rendering** (es. 75% sui PC deboli).
- Salvate con le altre impostazioni (fuori dal salvataggio della partita), applicate subito.

## Verifica

- Playtest con Effetti Alti e Spenti: nessun errore, frequenza misurata con 200 blocchi attivi e mappa enorme; numero di nodi stabile (le particelle tornano nel pool).
- Il determinismo della simulazione non cambia (test esistenti verdi; nessun riferimento agli effetti in Shared).
- Catture e un breve video o sequenza di catture per la revisione dell'utente; nuova base delle catture dopo il suo ok (con Effetti Spenti per catture stabili nei playtest).

## Documenti

ADR 0004 aggiornato (effetti e luci), ARCHITETTURA (come aggiungere un effetto), godot/README (impostazioni grafiche), diario e mappa Obsidian.
