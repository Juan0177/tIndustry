# Mappe e terreno

Come si generano le mappe delle partite libere, come si creano e controllano quelle della campagna e perché i generatori pubblicati non si toccano.

## Due tipi di mappa

- **Partite libere**: generate dal seme col generatore a biomi, in tre misure (Media 1000, Grande 1500, Enorme 2000 celle per lato). Il generatore usa solo interi, così il risultato è identico su Windows e Linux, e garantisce una partenza giocabile attorno al Core.
- **Campagna**: mappe prestabilite in file, in `data/maps/`, create dallo stesso generatore e poi validate (o ritoccate) con `tools/MapTool`.

Il terreno non entra mai nell'istantanea né nei salvataggi: si rifà dal generatore e dal seme, o dal file della mappa. La decisione è l'[ADR 0005](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0005-mappe-seme-e-file.md).

## Celle e tabella del terreno

Ogni cella è `TerrainTile(Floor, Wall, Deposit)`: suolo, parete e giacimento, un byte ciascuno. La tabella `terrain` di `data/content.json` dà per ogni suolo e parete id, bioma, colore e se si può costruire; per i giacimenti la durezza. Pareti e acqua profonda bloccano la costruzione, l'acqua bassa no; la regola sta in un solo punto, `Factory.TerrainBlocks`.

I colori della tabella sono anche le chiavi dei PNG delle mappe della campagna: cambiare quello di un tipo esistente rompe le mappe finché non si rigenerano.

## Generatori congelati

| Nome | Classe | Usato da |
|---|---|---|
| `classico` | `ClassicTerrainGenerator` | Salvataggi v11 e, per default, i test (`NewGame.Sandbox`, `NewGame.Campaign`) |
| `biomi-1` | `MapGenerator` | Partite libere del client (`MapGenSettings.Normal(misura, seme)`); i primi salvataggi v12 lo chiamano `biomi` |

**Un generatore pubblicato non si modifica mai.** I salvataggi rigenerano il terreno dal nome del generatore e ne controllano l'impronta: cambiare `classico` o `biomi-1` sposterebbe pareti, acqua e giacimenti sotto le fabbriche salvate. Una generazione diversa è un generatore nuovo con un nome nuovo (per esempio `biomi-2`), con il suo caso in `Factory.RestoreTerrain`. Le impronte fissate nei test (`ClassicGeneratorTests`, `MapGeneratorTests`) non si aggiornano: se cambiano, un generatore pubblicato è stato toccato.

## MapTool

Le mappe della campagna si creano e si controllano solo con `tools/MapTool`, mai a occhio.

```bash
dotnet run --project tools/MapTool -- genera <id> --misura N --seme S --biomi a,b [--pareti P] [--acqua P] [--giacimenti P] [--escludi titanio] [--nota "testo"]
dotnet run --project tools/MapTool -- valida [id…]       # senza id: tutte le mappe dei livelli di campaign.json
dotnet run --project tools/MapTool -- anteprima [id…]    # scrive .dev/mappe/<id>.png
```

- `genera` scrive `data/maps/<id>.png`, `<id>.giacimenti.png` e `<id>.json`, con i parametri usati nella nota. Pareti, acqua e giacimenti sono percentuali (100 = normale).
- `valida` controlla colori noti, Core e area di partenza liberi, almeno metà dell'area vicina raggiungibile a terra, nessun giacimento vicino chiuso, minerali degli obiettivi entro 60 celle dal Core. **Dopo ogni ritocco deve dire OK.**
- `anteprima` scrive un'immagine ingrandita per guardare la mappa.

Il test `EveryCampaignLevelHasAValidMap` carica e valida ogni livello anche in `dotnet test`.

## Aggiungere un livello o un suolo

- **Livello**: aggiungilo in `data/campaign.json` con `"map": "<id>"`, generane la mappa con `genera`, controllala con `valida` e `anteprima`. Cambiare la misura di una mappa già pubblicata rende illeggibili i salvataggi di quel livello.
- **Suolo o parete**: un valore **in fondo** all'enum (i byte esistenti non cambiano), una riga nella tabella `terrain` e, perché compaia nelle mappe a seme, un generatore nuovo. Servono anche gli sprite (vedi [Arte generata](Arte-generata)).

Le ricette passo per passo sono in [ARCHITETTURA.md](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md#come-aggiungere-un-suolo-una-parete-o-un-livello).

## Nel client

`TerrainChunks` disegna il terreno a pezzi, solo attorno alla vista, a due livelli di dettaglio: da lontano un pixel di colore per cella, da vicino pezzi d'arte composti con le scelte di `TerrainLook` (la stessa regola di `tools/art/tavola.py`, tenuta allineata da una fixture).

## Per approfondire

- [ADR 0005 — Mappe: generate da seme e prestabilite in file](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni/0005-mappe-seme-e-file.md)
- [docs/ARCHITETTURA.md, Terreno e mappe](https://github.com/Juan0177/tIndustry/blob/main/docs/ARCHITETTURA.md#terreno-e-mappe)
- [tools/MapTool/Program.cs](https://github.com/Juan0177/tIndustry/blob/main/tools/MapTool/Program.cs)
- [Contenuti e dati](Contenuti-e-dati) · [Formato dei salvataggi](Salvataggi-formato) · [Arte generata](Arte-generata)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Arte generata](Arte-generata) · [Torna alla Home](Home) · [Pagina successiva: Interfaccia e lingue →](Interfaccia-e-lingue)

<!-- /navigazione -->
