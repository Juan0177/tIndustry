# Rete di corrente come Mindustry (aspetto 5)

- **Specifica**: [2026-10-30-rete-corrente-design](../superpowers/specs/2026-10-30-rete-corrente-design.md)
- **Piano**: [2026-10-30-rete-corrente](../superpowers/plans/2026-10-30-rete-corrente.md)
- **Periodo**: 2026-10-30
- **Esito**: tutto sul ramo `claude/ripresa-task-notturni-5ht4tf` (una PR sola alla fine, che chiude l'issue #269). Costruzione e test verdi; il client non è stato provato a schermo (vedi «Per il PC di casa»).

> Riscritto dal registro dell'agente (`.superpowers/sdd/2026-10-30-rete-corrente/progress.md`). Dove il registro non dice qualcosa qui non è inventato.

## Obiettivo

Far viaggiare la corrente come in Mindustry: i nodi si collegano con dei laser, si passano la corrente fra loro e formano una rete con generatori, batterie e macchine. Una catena di nodi dal generatore al forno funziona; con poca corrente le macchine della rete rallentano insieme; una batteria tiene accesa la rete quando il carbone finisce. Il CORE non produce più corrente e la riserva unica sparisce.

## Task e commit

Base del piano: `156d70c`.

| Task | Contenuto | Commit |
|---|---|---|
| 1 | Reti e laser: `PowerGrid`, collegamenti automatici, comando `TogglePowerLink`, Batteria | `c5718cf`, correzione `d91cfca` |
| 2 | Bilancio per rete, soddisfazione in proporzione, effetti sulle macchine, batterie | `765c22e` |
| 3 | Salvataggi v17 e agenti (comando `collega`, reti nello stato) | `c28436c` |
| 4 | Client: laser, nodo selezionato, batteria, suggerimento della rete, testi | `98117b0` |
| 5 | Documenti, STATO, mappa, barre, CHANGELOG e questo diario | `docs: rete di corrente` |

Numeri: Nodo T1 raggio 6 e 6 collegamenti; Nodo T2 raggio 10 e 10 collegamenti; generatore 30/s; forno 8/s, assemblatore 10/s, Minatore T2 0,5/s, lampada 1/s; batteria 1.000. Conti interi: ritmi in millesimi di unità, carica delle batterie in sessantamillesimi, soddisfazione in decimillesimi.

## Giri di revisione

- Task 1: un giro di correzione. Rilievo importante: il ritorno anticipato di `AutoLinkNode` saltava il ricalcolo delle reti. Corretto (`d91cfca`).
- Task 2, 3 e 4: revisione senza rilievi bloccanti, solo piccole cose rinviate (sotto).

## Decisioni prese in autonomia

1. **Decisione**: il Task 2 adatta il salvataggio col minimo (campi vecchi letti e scritti come zero o tolti) per tenere verdi i round-trip, e il Task 3 porta la v17 — costo: codice ponte per un task.
2. **Decisione**: ogni task tiene la build della soluzione (client compreso) a 0/0 adattando i chiamanti col minimo; il client vero è il Task 4 — costo: HUD dell'energia provvisorio fra il Task 2 e il 4.
3. **Decisione**: il playtest del client sul PC di casa — costo: il client non è stato visto a schermo.
4. **Decisione**: il raggio si misura come distanza intera fra i bordi dei due blocchi (la specifica dice «fra i bordi», mentre `IsWithinRange` misura dal centro) — costo: raggio effettivo un po' più lungo di prima.
5. **Decisione**: il collegamento automatico include anche generatori e batterie (altrimenti una catena non partirebbe mai) e salta i consumatori già alimentati e i blocchi già nella rete del nodo — costo: nessuno.
6. **Decisione**: le Lampade chiedono corrente solo quando la luce del mondo è sotto metà (`Light < 500`), cioè quando «vogliono lavorare» — costo: di giorno non consumano.
7. **Decisione**: una macchina che avvia la ricetta a metà tick lavora con soddisfazione 0 per un tick — costo: 1/60 s, deterministico.
8. **Decisione**: con un nodo selezionato, il clic su un blocco che non è di corrente toglie la selezione senza messaggio (come Mindustry) — costo: il rifiuto «non è un blocco di corrente» non si vede mai nel client.

## Problemi rinviati

- Commento vecchio in `FactoryContent.cs` (`TrySpendPower`) e `ShareCharge` che presuppone capacità uguali (commento e controllo): piccolezze del Task 2.
- `PowerNetworks` allocata due volte a frame dall'HUD (Task 2); strato dei laser ridisegnato a ogni frame senza culling, testi del nodo e del suggerimento non aggiornati al cambio lingua, sincronizzazione delle batterie O(n²), `LevelOf` senza test, `_leftDown` della camera (Task 4): per la revisione finale.
- Il diario degli agenti resta v3: i diari con partenza v16 falliscono con `ParametriNonValidi` leggibile; quelli senza istantanea si rigiocano con lampade e `collega` diversi.
- I laser caricati dal salvataggio non controllano raggio e collegamenti massimi; il test delle lampade modifica il tick a mano e scrive 500 invece di `Lamp.DarkBelow`.
- `MapGeneratorTests.GenerationTimeBudget` fallisce a volte nella suite completa sotto carico (da solo 712 ms contro 1500): test a tempo instabile.
- Le Batterie e i laser fuori dal checksum: ripresi con la v17 (Task 3).

## Per il PC di casa

Da fare dove c'è Godot (anche in [STATO](../STATO.md)):

- `--import` per i `.import` dei PNG della batteria (`battery`, `battery-base`, `battery-charge-anim`) e il `BatteryVisual.cs.uid`;
- playtest `ui`, `tutorial` e `agenti` con le catture di riferimento nuove;
- a mano, con una catena di nodi: selezione del nodo, raggio, clic per collegare e scollegare, rifiuti, Esc, barre di carica delle batterie, suggerimento della rete, le tre varianti di colore (Normale, Daltonici, Alto contrasto), la notte con le Lampade;
- larghezza di `SelectionInfo` (640 px accanto alla palette a 1280×720).

## Verifiche finali

Build della soluzione a 0 errori e 0 avvisi, test della simulazione verdi, test del sito e controllo della Wiki (risultati nel report del Task 5). Il client non è stato lanciato.

## Revisione finale

Revisione di tutto il ramo (156d70c..8829b59): pronto al merge, nessun problema grave. Correzioni nel commit `94f0905`, riviste:

- schema della Wiki Energia coerente coi 5 nodi T1 per un forno a 30 celle;
- testi del nodo selezionato e del suggerimento della rete aggiornati al cambio di lingua;
- strato dei laser ridisegnato solo quando cambia qualcosa di visibile (prima a ogni frame).

Decisione presa in autonomia: la revisione del Task 5 (documenti) è stata fusa nella revisione finale. Costo se sbagliato: un controllo in meno sui documenti.

Rimandati: ricerca dei blocchi di corrente per id (oggi costo quadratico quando si seleziona un nodo in una fabbrica grande), `TryPlaceBattery`, sincronizzazione delle batterie O(n²), laser caricati senza controllo di raggio e collegamenti massimi, diario degli agenti ancora alla versione 3, il ridisegno dei laser che può saltare un blocco sostituito sul posto con lo stesso numero di collegamenti, il test a tempo `GenerationTimeBudget` instabile sotto carico.
