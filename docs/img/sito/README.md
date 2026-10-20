# Catture per il sito

Catture di gioco 1280x720 (Scala UI 100%, italiano) prodotte il 2026-10-18 dai playtest automatici, con profilo dati temporaneo. Servono alla pagina vetrina del sito (`tools/sito/costruisci.py`). Nei cinque scatti dei blocchi (non nelle basi) compare al centro il messaggio in sovrimpressione "Impostazioni · UI 100% · VSync ON", residuo del playtest.

## Catture usate

| File | Playtest e cattura originale | Cosa mostra |
| --- | --- | --- |
| `fabbrica-accesa.png` | `ui` con `TINDUSTRY_PLAYTEST_BLOCCHI=1`, `ui-blocchi-accesi.png` | Tutti i blocchi di produzione e potenza al lavoro: forni e assemblatori accesi, minatori, generatori, energia 64/64. |
| `fabbrica-spenta.png` | `ui` con `TINDUSTRY_PLAYTEST_BLOCCHI=1`, `ui-blocchi-spenti.png` | Gli stessi blocchi spenti, a zoom più vicino (energia 48/64), per il confronto con la fabbrica accesa. |
| `panoramica.png` | `ui` con `TINDUSTRY_PLAYTEST_BLOCCHI=1`, `ui-blocchi-zoom04.png` | La fabbrica a zoom basso (0,4) nel paesaggio: biomi, giacimenti e roccia. |
| `core.png` | `ui` con `TINDUSTRY_PLAYTEST_BLOCCHI=1`, `ui-blocchi-core.png` | Il Core da vicino con un minatore, un tratto di nastro e il drone in volo. |
| `oggetti.png` | `ui` con `TINDUSTRY_PLAYTEST_BLOCCHI=1`, `ui-blocchi-oggetti.png` | Tutti gli oggetti di gioco sui tre tipi di nastro. |
| `campagna.png` | `ui`, `ui-scala-100-tutorial.png` | Livello 1 della campagna ("Primi passi") con il pannello Obiettivo e il pannello del tutorial (passo 3/8). Il playtest `tutorial` gioca in sandbox, quindi la campagna con il tutorial viene dal playtest `ui`. |
| `partita.png` | `tutorial`, `tutorial-06-step7-forno.png` | Partita in corso: minatore, nastro con ferro verso il Core e pannello del tutorial (passo 7/8). Alternativa più leggibile e senza testo: `ui-belt-into-core.png` del playtest `ui` (stessa scena, zoom ravvicinato). |
| `base-piccola.png` | `ui` con `TINDUSTRY_PLAYTEST_BASI=1`, `ui-basi-piccola.png` | Base piccola con HUD nascosto: due minatori, un forno, un generatore con nodo e nastri con curve fino al Core, oggetti in viaggio. |
| `base-grande.png` | `ui` con `TINDUSTRY_PLAYTEST_BASI=1`, `ui-basi-grande.png` | Base grande intera: minatori T1 e T2, nastri dei tre tier, ponte, selezionatore, due forni, assemblatore, generatori, nodi e lampade, linee che convergono sul Core. |
| `base-grande-vicino.png` | `ui` con `TINDUSTRY_PLAYTEST_BASI=1`, `ui-basi-grande-vicino.png` | Zoom 1 sulla parte più fitta della base grande, con il Core e i nastri carichi. |

## Altre catture prodotte

Le descrizioni sono ricavate dai nomi dei file; solo alcune sono state riviste a vista. Chi ne vuole altre può chiederle indicando il nome originale.

### Playtest `tutorial`

- `tutorial-00-home.png`: schermata iniziale.
- `tutorial-01-step1-camera.png`: tutorial, passo 1 (telecamera).
- `tutorial-02-step2-palette.png`: passo 2 (palette dei blocchi), mappa classica con il Core.
- `tutorial-03-step3-miner-placed.png`: passo 3, minatore pianificato dal drone accanto al Core.
- `tutorial-04-step5-mercato.png`: passo 5 (Mercato).
- `tutorial-05-mercato-open.png`: pannello del Mercato aperto.
- `tutorial-06-step7-forno.png`: passo 7 (forno), con minatore e nastro.
- `tutorial-07-step8-impostazioni.png`: passo 8 (impostazioni).
- `tutorial-08-done.png`: tutorial completato.

### Playtest `ui`

- Blocchi (con `TINDUSTRY_PLAYTEST_BLOCCHI=1`): solo le cinque catture della tabella.
- Basi (con `TINDUSTRY_PLAYTEST_BASI=1`): solo le tre catture della tabella (`ui-basi-piccola.png`, `ui-basi-grande.png`, `ui-basi-grande-vicino.png`).
- `ui-belt-into-core.png`: nastro che porta ferro nel Core, zoom ravvicinato.
- `ui-home.png`, `ui-nuova-partita.png`, `ui-impostazioni.png`, `ui-impostazioni-en.png`, `ui-impostazioni-scala-50.png`, `ui-mercato.png`, `ui-ricerca.png`: schermate di menu e pannelli (impostazioni anche in inglese e con Scala UI al 50%).
- `ui-hud.png`: interfaccia di gioco con Core, minatore e nastro.
- `ui-campagna.png`: campagna a zoom molto basso (mappa 200x200 con giacimenti); `ui-campagna-mappa-mancante.png`: caso di mappa mancante.
- `ui-palette-inizio.png`, `ui-palette-tutto.png`, `ui-palette-info.png`: palette dei blocchi a inizio partita, con tutto sbloccato e con le informazioni.
- `ui-fantasma-valido.png`, `ui-fantasma-non-valido.png`, `ui-fantasma-titanio.png`, `ui-rifiuto.png`: anteprima di piazzamento valida e non valida, titanio e rifiuto.
- `ui-colori-{normale,daltonici,altocontrasto}-{impostazioni,valido,non-valido,smontaggio}.png`: le tre varianti di colore per impostazioni, piazzamento valido, non valido e smontaggio.
- `ui-terreno-biomi.png`, `ui-terreno-minerali.png`, `ui-terreno-partenza.png`, `ui-terreno-campagna.png`: terreno a biomi, minerali, zona di partenza e terreno della campagna.
- `ui-scala-{75,100,125,150,200}-{home,hud,campagna,mercato,impostazioni,nuova-partita,salvataggi,ricerca,ricerca-intera,tutorial}.png`: le schermate principali alle cinque Scale UI (la 200 ha anche `ui-scala-200-clic-cella.png` e `ui-scala-200-suggerimento.png`).
