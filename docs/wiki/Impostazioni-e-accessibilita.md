# Impostazioni e accessibilità

Tutte le voci delle Impostazioni: lingua, Scala UI, colori per daltonici e ad alto contrasto, effetti grafici e animazioni.

Le Impostazioni si aprono con `I` in partita o dalla Home. **Applica** salva su disco e vale subito.

## Interfaccia

| Voce | Scelte | Effetto |
|---|---|---|
| **Lingua** | Italiano, English | Cambia subito tutti i testi, anche nei pannelli aperti: blocchi, oggetti, livelli, obiettivi, avvisi. Predefinito italiano |
| **Scala UI** | Da 75% a 200%, a passi di 5 | Ingrandisce solo HUD, pannelli e suggerimenti; il mondo resta com'è e il clic cade sempre sulla cella sotto il mouse |
| **Colori** | Normali, Daltonici, Alto contrasto | Cambia i colori che hanno un significato (vedi sotto) |
| **Mostra FPS** | sì / no | Mostra i fotogrammi al secondo |
| **Mostra risorse sistema** | sì / no | Mostra uso di CPU, GPU e RAM |
| **Rivedi tutorial** | — | Fa ripartire il tutorial |

La Scala UI predefinita è 100%; al primo avvio del gioco esportato si sceglie dall'altezza dello schermo (100% sotto i 1400 pixel, 150% sotto i 2000, 200% oltre). Il testo pixel è più nitido a 100%, 150% e 200%. In una finestra 1280×720 tutto sta nella finestra fino al 150%; più su alcuni pannelli scorrono.

## Colori e segni

| Variante | Come appare |
|---|---|
| Normali | I colori di sempre |
| Daltonici | Tinte sicure per deuteranopia e protanopia (azzurro e vermiglio, giallo e porpora) |
| Alto contrasto | Pannelli neri, testo bianco, bordi chiari; segni nel mondo giallo puro e rosso magenta con un bordo nero, leggibili su ogni terreno |

La variante cambia l'anteprima di piazzamento valida o rifiutata, costruzione e smontaggio, energia e denaro, rifiuti e materiali mancanti, linee della Ricerca. In ogni variante il significato non sta mai solo nel colore:

| Segno | Significato |
|---|---|
| X sull'anteprima (e il motivo in basso) | Piazzamento rifiutato |
| Tratteggio | Blocco da smontare o in smontaggio |
| Barra sopra un materiale o sul denaro | Manca |
| "!" sull'energia | Energia bassa |
| Freccia sul prezzo | Prezzo in calo |

<table>
  <tr>
    <td width="50%"><img src="https://raw.githubusercontent.com/Juan0177/tIndustry/main/docs/img/readme/interfaccia-daltonici.png" alt="Colori per daltonici"><br><sub><b>Daltonici</b> · anteprima rifiutata con la X, smontaggio tratteggiato</sub></td>
    <td width="50%"><img src="https://raw.githubusercontent.com/Juan0177/tIndustry/main/docs/img/readme/interfaccia-altocontrasto.png" alt="Alto contrasto"><br><sub><b>Alto contrasto</b> · pannelli neri, testo bianco, segni col bordo nero</sub></td>
  </tr>
</table>

## Grafica

| Voce | Scelte | Effetto |
|---|---|---|
| **Effetti** | Alti, Bassi, Spenti | Particelle (fumo, scintille, vapore, polvere). Alti: tutte, anche neve, sabbia e foglie d'ambiente; Bassi: la metà e niente ambiente; Spenti: nessuna e acqua ferma |
| **Ombre sotto gli edifici** | sì / no | Ombra sotto ogni blocco |
| **Bagliore** | sì / no | Alone luminoso su forni, CORE, Lampade e scintille |
| **Animazioni dei blocchi** | sì / no | Trivelle, rotori, bocche dei forni e luce del CORE; spente restano ferme (le particelle seguono la voce Effetti) |
| **Limite FPS** | Nessuno, 30, 60, 120, 144 | Tetto ai fotogrammi al secondo |
| **Scala di rendering** | 100%, 75%, 50% | Il mondo si disegna a risoluzione ridotta e si ingrandisce; l'interfaccia resta nitida. Per i PC meno potenti |
| **VSync** | sì / no | Sincronizzazione verticale |
| **Schermo intero** | sì / no | Schermo intero alla risoluzione dello schermo, oppure finestra 1280×720 centrata |

Valori predefiniti: Effetti Alti, Ombre e Bagliore accesi, nessun limite di FPS, scala di rendering 100%. Un file di impostazioni di una versione precedente resta valido: le voci mancanti prendono il valore predefinito.

Le lucciole sui prati di notte compaiono solo con Effetti Alti.

## Vedi anche

- [Comandi](Comandi)
- [Primi passi](Primi-passi)
- [Salvataggi](Salvataggi)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Campagna e mappe](Campagna-e-mappe) · [Torna alla Home](Home) · [Pagina successiva: Salvataggi →](Salvataggi)

<!-- /navigazione -->
