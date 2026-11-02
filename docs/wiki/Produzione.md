# Produzione

I blocchi che producono: minatori, forno, assemblatore ed estrattore, con misure, costi, ritmi e consumi.

![Blocchi di produzione e potenza al lavoro](https://raw.githubusercontent.com/Juan0177/tIndustry/main/docs/img/readme/blocchi.png)

## In breve

| Blocco | Misura | Costo di costruzione | Sblocco | Cosa fa |
|---|---|---|---|---|
| Minatore T1 (`miner`) | 2×2 | 4 Ferro grezzo | iniziale | Estrae ferro, rame, carbone, piombo |
| Minatore T2 (`miner-advanced`) | 2×2 | 10 Lastre di ferro, 4 Fili di rame | Ricerca | Estrae tutto, titanio compreso, a velocità doppia |
| Forno (`smelter`) | 2×2 | 6 Ferro grezzo | Ricerca | Fonde i minerali in lastre, a carbone o a corrente |
| Assemblatore (`assembler`) | 2×2 | 8 Lastre di ferro, 2 Rame grezzo | Ricerca | Filo di rame, grafite, silicio |
| Estrattore (`extractor`) | 1×1 | 2 Lastre di ferro | Ricerca | Tira oggetti fuori dal CORE su un nastro |

I costi di sblocco sono in [Economia e Ricerca](Economia-e-Ricerca). Le costruzioni non costano soldi, solo materiali presi dal CORE.

## Come entrano ed escono gli oggetti

- **Entrata**: un nastro che punta dentro il blocco gli consegna gli oggetti. Il blocco accetta solo quello che usa (ingredienti delle sue ricette, carbone come combustibile): un oggetto che non gli serve resta fermo sul nastro e lo blocca.
- **Uscita**: minatori, forno e assemblatore mettono quello che producono su un nastro accanto a loro che **si allontana** dal blocco, su qualunque lato, a turno fra i nastri disponibili. Un nastro che punta verso il blocco non riceve nulla.
- **Contatto diretto**: se un minatore, un forno o un assemblatore tocca con un lato un forno, un assemblatore o il CORE, gli passa gli oggetti direttamente, senza nastro.

## Minatore T1

Si piazza su un giacimento. Con tutte e 4 le celle sul minerale produce **1 oggetto ogni 2 secondi**; con meno celle va più piano in proporzione (2 celle su 4: 1 oggetto ogni 4 secondi). Se copre più minerali scava quello presente nel maggior numero di celle. Non scava il titanio. Non usa corrente.

## Minatore T2

Come il T1 ma **velocità doppia** e ogni cella sul minerale conta il 25% in più, fino al massimo: con tutte e 4 le celle fa 1 oggetto al secondo. Scava anche il **titanio**. Se è nella rete di un generatore acceso (a contatto o con un laser di un nodo, vedi [Energia](Energia)) lavora fino al **20% più veloce** e consuma 0,5 unità di energia al secondo; con poca corrente il vantaggio cala in proporzione.

| Minatore | 4 celle su 4 | Con corrente |
|---|---|---|
| Minatore T1 | 1 ogni 2 s (0,5/s) | non la usa |
| Minatore T2 | 1 ogni 1 s (1/s) | 1,2/s |

## Forno

Fonde i minerali in lastre (ricette in [Risorse e oggetti](Risorse-e-oggetti)): Lastra di ferro, di piombo, di titanio. Per lavorare serve **carbone oppure corrente**:

| Modo | Come funziona |
|---|---|
| A carbone | Il carbone che arriva va in una scorta di 8; ogni Carbone dà 6 secondi di lavoro |
| A corrente | Se è nella rete di un generatore consuma 8 unità di energia al secondo quando ha qualcosa da fondere e va fino al **20% più veloce**; se la corrente non basta e ha carbone, lo brucia e va a velocità normale |

Di ogni ingrediente tiene da parte al massimo 4 volte quanto ne chiede una ricetta (8 Ferro grezzo, 8 Piombo grezzo, 12 Titanio grezzo); oltre, il nastro aspetta. Vale anche per l'Assemblatore. Si accende (fuoco, fumo, luce di notte) solo quando lavora davvero.

## Assemblatore

Fa Filo di rame, Grafite e Silicio. **Lavora anche senza corrente**; se è nella rete di un generatore consuma 10 unità di energia al secondo e va fino al 20% più veloce (meno, se la corrente non basta per tutta la rete). Avvia la prima ricetta per cui ha tutti gli ingredienti, nell'ordine Filo di rame, Grafite, Silicio.

## Estrattore

Un blocco 1×1 da mettere **a contatto con il CORE** con un nastro davanti (la direzione si sceglie con `R`) che si allontana. Ogni 0,4 secondi prende 1 oggetto dal magazzino del CORE (o, se tocca un container o un vault che non è collegato al CORE, dal suo contenuto) e lo mette sul nastro: così il magazzino rifornisce le macchine. Oggi preleva Lastre di ferro (il suo filtro predefinito): nel gioco non c'è ancora un comando per cambiarlo.

## Vedi anche

- [Risorse e oggetti](Risorse-e-oggetti)
- [Energia](Energia)
- [Logistica](Logistica)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Risorse e oggetti](Risorse-e-oggetti) · [Torna alla Home](Home) · [Pagina successiva: Logistica →](Logistica)

<!-- /navigazione -->
