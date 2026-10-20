# Risorse e oggetti

I minerali che trovi sulla mappa, gli oggetti che produci e tutte le ricette, con quantità e tempi.

![Giacimenti di ferro, rame, piombo, carbone e titanio](https://raw.githubusercontent.com/Juan0177/tIndustry/main/docs/img/readme/terreno-minerali.png)

## Giacimenti

I giacimenti sono macchie di puntini colorati sul terreno. Un minatore piazzato sopra estrae il minerale del giacimento.

| Giacimento | Dà | Chi lo scava |
|---|---|---|
| ferro | Ferro grezzo | Minatore T1 e T2 |
| rame | Rame grezzo | Minatore T1 e T2 |
| carbone | Carbone | Minatore T1 e T2 |
| piombo | Piombo grezzo | Minatore T1 e T2 |
| titanio | Titanio grezzo | solo **Minatore T2** |

Il titanio è più duro: come in Mindustry serve una trivella di livello più alto. Un Minatore T1 su un giacimento di solo titanio viene rifiutato ("Serve il Minatore T2 per il titanio"); su celle miste scava gli altri minerali e ignora il titanio. Quando un minatore copre più minerali, scava quello presente nel maggior numero di celle.

Nelle partite libere il CORE parte con ferro e rame vicini e una strada a terra verso ferro, rame e carbone. Nella campagna il titanio compare solo dal livello 9.

## Oggetti

| Oggetto | Id | Come si ottiene | A cosa serve |
|---|---|---|---|
| Ferro grezzo | `iron-ore` | Minatore su ferro | Lastre di ferro; costo di nastri, minatore, forno e logistica |
| Rame grezzo | `copper-ore` | Minatore su rame | Filo di rame; costo dell'assemblatore |
| Carbone | `coal` | Minatore su carbone | Combustibile di forno e generatore; grafite e silicio |
| Piombo grezzo | `lead-ore` | Minatore su piombo | Lastre di piombo; costo di nastri T2, selezionatore, ponte |
| Titanio grezzo | `titanium-ore` | Minatore T2 su titanio | Lastre di titanio; costo dei nastri T2, T3 e rinforzato |
| Lastra di ferro | `iron-plate` | Forno | Filo di rame; costo di edifici, sblocchi e potenziamento del CORE |
| Lastra di piombo | `lead-plate` | Forno | Silicio |
| Lastra di titanio | `titanium-plate` | Forno | Si vende (prezzo di listino più alto del gioco) |
| Filo di rame | `copper-wire` | Assemblatore | Costo di nodi, Lampada, Minatore T2 e sblocchi di energia |
| Grafite | `graphite` | Assemblatore | Costo di Nastro T3 e Nastro rinforzato |
| Silicio | `silicon` | Assemblatore | Costo del Nastro T3 |

Tutto quello che arriva al CORE va nel suo magazzino (o si vende subito, con la vendita automatica): da lì si pagano costruzioni e sblocchi. I prezzi di vendita sono in [Economia e Ricerca](Economia-e-Ricerca).

## Ricette

Il Forno ha le ricette `smelt-…`, l'Assemblatore quelle `craft-…`. Non si sceglie la ricetta: la macchina avvia la prima, nell'ordine della tabella, per cui ha già tutti gli ingredienti. Con la corrente il Forno e l'Assemblatore lavorano il 20% più veloci.

| Ricetta | Macchina | Ingredienti | Prodotto | Tempo | Con corrente |
|---|---|---|---|---|---|
| Lastra di ferro (`smelt-iron`) | Forno | 2 Ferro grezzo | 1 Lastra di ferro | 2 s | 1,67 s |
| Lastra di piombo (`smelt-lead`) | Forno | 2 Piombo grezzo | 1 Lastra di piombo | 1,8 s | 1,5 s |
| Lastra di titanio (`smelt-titanium`) | Forno | 3 Titanio grezzo | 1 Lastra di titanio | 3,2 s | 2,67 s |
| Filo di rame (`craft-copper-wire`) | Assemblatore | 1 Rame grezzo + 1 Lastra di ferro | 2 Fili di rame | 1,5 s | 1,25 s |
| Grafite (`craft-graphite`) | Assemblatore | 2 Carbone | 1 Grafite | 1,2 s | 1 s |
| Silicio (`craft-silicon`) | Assemblatore | 1 Lastra di piombo + 1 Carbone | 1 Silicio | 2,4 s | 2 s |

Il Forno brucia anche carbone come combustibile se non ha corrente: 1 Carbone ogni 6 secondi di lavoro (vedi [Produzione](Produzione)).

## Catene tipiche

```text
ferro ──▶ Forno ──▶ Lastra di ferro ──┐
rame ─────────────────────────────────┴──▶ Assemblatore ──▶ Filo di rame
piombo ──▶ Forno ──▶ Lastra di piombo ──┐
carbone ────────────────────────────────┴──▶ Assemblatore ──▶ Silicio
carbone ──▶ Assemblatore ──▶ Grafite
titanio ──▶ (Minatore T2) ──▶ Forno ──▶ Lastra di titanio
```

## Vedi anche

- [Produzione](Produzione)
- [Economia e Ricerca](Economia-e-Ricerca)
- [Campagna e mappe](Campagna-e-mappe)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Comandi](Comandi) · [Torna alla Home](Home) · [Pagina successiva: Produzione →](Produzione)

<!-- /navigazione -->
