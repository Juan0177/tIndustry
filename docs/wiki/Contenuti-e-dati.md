# Contenuti e dati

Dove stanno blocchi, oggetti, ricette, livelli e mappe, e quali file toccare per aggiungere un blocco nuovo.

## Un'unica fonte: data/

Tutti i contenuti di gioco stanno in `data/` e solo lì. Il codice li legge, non li ripete.

| File | Cosa contiene |
|---|---|
| `data/content.json` | Strutture (`structures` con sblocco e prerequisiti, `buildings` e `conveyors` con i parametri), ricette (`recipes`), mercato (`market`), economia (`economy`), pesi di costruzione (`buildWeights`), drone (`units.drone`), tabella del terreno (`terrain`: suoli, pareti, giacimenti con la loro durezza) |
| `data/campaign.json` | I livelli della campagna (`levels`): mappa, soldi e materiali di partenza, quota di rimborso (`refundPercent`), obiettivi, livello successivo |
| `data/maps/` | Le mappe della campagna: per ogni livello `<id>.png` (terreno), `<id>.giacimenti.png` (giacimenti) e `<id>.json` (Core, biomi, nota con i parametri) |

In sviluppo il gioco legge `data/` direttamente. All'export `tools/ci/export-godot.sh` copia i file in `godot/data/`, che è ignorata da git e non va mai modificata.

Il test `ContentValidationTests` controlla i dati a ogni `dotnet test`: id unici, prerequisiti esistenti e senza cicli, icone presenti per strutture e oggetti, obiettivi della campagna su id noti.

## Regole da ricordare

- Le mappe della campagna si creano e si controllano solo con `tools/MapTool`, mai a occhio (vedi [Mappe e terreno](Mappe-e-terreno)).
- I colori della tabella `terrain` sono anche le chiavi dei PNG delle mappe: cambiare il colore di un suolo o di una parete esistente rompe le mappe in `data/maps/`.
- I nomi visibili di strutture, oggetti e livelli vengono tradotti per id da `godot/lingue/testi.csv`; l'italiano di `data/` resta il valore di riserva (vedi [Interfaccia e lingue](Interfaccia-e-lingue)).
- Il tempo di costruzione di un blocco viene dal suo costo: `(20 + Σ quantità × buildWeight) × buildCostMultiplier`.

## Come aggiungere un blocco (riassunto)

La ricetta completa, con ogni metodo da toccare, è in [ARCHITETTURA.md, Come aggiungere un blocco](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md#come-aggiungere-un-blocco). In sintesi:

1. **Dati.** In `data/content.json` la struttura (id, costi, prerequisiti per la Ricerca, ricetta o parametri), gli oggetti nuovi ed eventualmente `buildCostMultiplier` e `refundPercent`.
2. **Sprite.** Una funzione nel generatore `tools/art`, mai un PNG a mano (vedi [Arte generata](Arte-generata)). L'icona `<id>.png` serve a palette, Ricerca, coda, piani e cantieri.
3. **Strumento di costruzione** nel client, se il blocco si piazza dalla palette: `Hud`, `BuildPalette`, `World.BuildTool` e i file parziali di `World` (input, piazzamento, blocchi della Ricerca, anteprima).
4. **Visual** in `godot/scripts/visuals/buildings/`, o uno esistente.
5. **Simulazione.** Il nuovo `BuildingKind`, la mappatura in `BuildSpec.StructureIdFor`, ingombro, posa e riconoscimento in `Factory`; il comportamento in `Buildings/` o `Power/`, chiamato dal sistema giusto nel rispetto delle regole di determinismo. Se l'edificio ha stato va nell'istantanea e, cambiando il formato, si alza la versione dei salvataggi.
6. **Testi.** `struttura.<id>.nome`, `.suggerimento` e `.descrizione` in `godot/lingue/testi.csv`, in italiano e in inglese.
7. **Test.** `ContentValidationTests`, `TestiTests` e i test del generatore devono restare verdi; uno scenario nuovo in `tests/TIndustry.Shared.Tests/` se c'è logica nuova.

Se manca uno dei punti di simulazione il piazzamento fallisce in modo fuorviante (per esempio «Comando non valido»): conviene seguire la lista dell'architettura voce per voce.

Nello stesso documento ci sono anche le ricette per aggiungere [un suolo, una parete o un livello](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md#come-aggiungere-un-suolo-una-parete-o-un-livello) e [un effetto](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md#come-aggiungere-un-effetto).

## Per approfondire

- [docs/ARCHITETTURA.md, Dati](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md#dati)
- [data/content.json](https://github.com/Juan0177/tIndustry/blob/main/data/content.json) · [data/campaign.json](https://github.com/Juan0177/tIndustry/blob/main/data/campaign.json)
- [Arte generata](Arte-generata) · [Mappe e terreno](Mappe-e-terreno) · [Interfaccia e lingue](Interfaccia-e-lingue)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Architettura](Architettura) · [Torna alla Home](Home) · [Pagina successiva: Formato dei salvataggi →](Salvataggi-formato)

<!-- /navigazione -->
