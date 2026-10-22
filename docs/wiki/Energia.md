# Energia

Come si produce e si porta la corrente: generatore, carbone, nodi T1 e T2, Lampada, e cosa cambia nei blocchi alimentati.

## A cosa serve

La corrente non è obbligatoria: il Forno può andare a carbone e l'Assemblatore lavora anche senza. Con la corrente però:

| Blocco | Con la corrente | Consumo |
|---|---|---|
| Forno | Non brucia carbone e lavora il **20% più veloce** | 8 unità al secondo mentre lavora |
| Assemblatore | Lavora il **20% più veloce** | 10 unità al secondo mentre lavora |
| Minatore T2 | Estrae il **20% più veloce** | 0,5 unità al secondo mentre scava |
| Lampada | Si accende e illumina la notte | 1 unità al secondo |

Il Minatore T1 e l'Estrattore non usano corrente.

## Generatore

| Generatore (`generator`) | |
|---|---|
| Misura | 2×2 |
| Costo | 8 Lastre di ferro |
| Sblocco | $280, 12 Lastre di ferro (nessun prerequisito) |
| Combustibile | Carbone portato da un nastro; scorta di 8 |
| Consumo | 1 Carbone ogni 8 secondi |
| Produzione | +28 unità al secondo mentre brucia |
| Accumulo | +40 di capacità mentre brucia |

Un generatore è **acceso** finché ha carbone da bruciare: il rotore gira e, di notte, fa luce. Accetta solo carbone.

## L'energia accumulata

L'energia sta in un'unica riserva della partita, mostrata nel pannello del CORE come *energia accumulata / capacità* con il numero di generatori attivi. Il CORE da solo dà 24 di capacità e 12 unità al secondo; ogni generatore acceso aggiunge 40 di capacità e 28 al secondo. Quando la riserva è bassa compare un "!".

| Generatori accesi | Capacità | Produzione al secondo |
|---|---|---|
| 0 | 24 | 12 |
| 1 | 64 | 40 |
| 2 | 104 | 68 |

La riserva però si può usare solo da un blocco **collegato a un generatore acceso**: senza generatori accesi nessun blocco è alimentato.

## Come si collega un blocco

Un blocco è alimentato se:

1. **tocca** con un lato un generatore acceso, oppure
2. **tocca** con un lato un **nodo** che ha un generatore acceso entro il suo raggio.

| Nodo | Misura | Raggio | Costo | Sblocco |
|---|---|---|---|---|
| Nodo T1 (`power-node`) | 1×1 | 6 celle | 2 Fili di rame | $140, 4 Lastre di ferro, 2 Fili di rame (dopo il Generatore) |
| Nodo T2 (`power-node-t2`) | 2×2 | 10 celle | 6 Lastre di ferro, 4 Fili di rame | $320, 12 Lastre di ferro, 6 Fili di rame (dopo il Nodo T1) |

Il raggio si misura dal centro del nodo al centro del generatore. I nodi si collegano da soli, senza fili da tracciare. Un nodo non passa la corrente a un altro nodo: ogni nodo deve avere un generatore acceso nel suo raggio. Il CORE non fa parte della rete.

## Lampada

| Lampada (`lamp`) | |
|---|---|
| Misura | 1×1 |
| Costo | 2 Lastre di ferro, 1 Filo di rame |
| Sblocco | $90, 4 Lastre di ferro, 2 Fili di rame (dopo il Nodo T1) |
| Consumo | 1 unità di energia al secondo |

Si accende solo se è collegata come gli altri blocchi (generatore acceso accanto o nodo nel raggio) e l'energia basta; spenta non fa luce. Serve nelle partite di **notte** (casella *Notte* in Nuova partita, livelli 6 e 10 della campagna), dove contano le luci del CORE, dei forni al lavoro, dei generatori accesi, dei droni e delle Lampade.

## Uno schema semplice

```text
nastro di carbone ──▶ [Generatore] ·· entro 6 celle ·· [Nodo T1][Forno]
```

Il nodo tocca il forno e ha il generatore nel raggio: il forno va a corrente. Un forno messo a contatto diretto con il generatore non ha bisogno del nodo.

## Vedi anche

- [Produzione](Produzione)
- [Economia e Ricerca](Economia-e-Ricerca)
- [Domande frequenti](Domande-frequenti)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Logistica](Logistica) · [Torna alla Home](Home) · [Pagina successiva: Costruzione →](Costruzione)

<!-- /navigazione -->
