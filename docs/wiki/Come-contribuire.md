# Come contribuire

Il flusso di lavoro del progetto, dalle idee alle pull request, e le convenzioni da rispettare.

## Il flusso di lavoro

1. **Specifica.** Un lavoro nuovo parte da una specifica in `docs/superpowers/specs/` (`AAAA-MM-GG-<nome>-design.md`): cosa si vuole, le opzioni, le scelte fatte con chi decide.
2. **Piano.** La specifica diventa un piano di implementazione in `docs/superpowers/plans/`, diviso in task numerati, ciascuno con i suoi passi e le sue verifiche.
3. **Una PR per task.** Ogni task è una pull request unita in `main` prima del task seguente; `main` resta verde dopo ogni task.
4. **Diario.** A fine piano il registro di lavoro (tenuto in `.superpowers/sdd/<piano>/progress.md`) si copia in `docs/diario/AAAA-MM-GG-<nome>.md` *prima* di cancellare la cartella `.superpowers/sdd/`. Il diario racconta task e PR, giri di revisione, decisioni prese in autonomia, problemi rinviati e verifiche finali (formato in [docs/diario/README.md](https://github.com/Juan0177/tIndustry/blob/main/docs/diario/README.md)).
5. **Decisioni.** Una scelta architetturale diventa un ADR in `docs/decisioni/` e una riga dell'indice [docs/decisioni.md](https://github.com/Juan0177/tIndustry/blob/main/docs/decisioni.md); le novità visibili vanno in [CHANGELOG.md](https://github.com/Juan0177/tIndustry/blob/main/CHANGELOG.md).

## Lingua

- **Italiano** per i testi di gioco, i documenti, i messaggi di commit e le pull request.
- **Inglese** per i commenti nel codice.
- I testi visibili nel gioco passano sempre da chiavi di `godot/lingue/testi.csv`, in italiano e in inglese (vedi [Interfaccia e lingue](Interfaccia-e-lingue)).

## Convenzioni del codice

- Namespace: `TIndustry.Shared`, `TIndustry.Godot`, `TIndustry.Shared.Tests`, senza sotto-namespace per cartella.
- `src/TIndustry.Shared` non dipende da Godot né dal sistema operativo e rispetta le regole di determinismo (vedi [Architettura](Architettura)).
- Dati solo in `data/`; mappe solo con `tools/MapTool`; sprite solo da `tools/art/genera.py`; stili solo dal tema `UiTheme`.
- Spostare uno script Godot vuol dire spostarlo con `git mv` insieme al suo file `.uid`.
- Cambiare il formato dei salvataggi vuol dire alzare la versione (vedi [Formato dei salvataggi](Salvataggi-formato)).
- Prima di proporre una modifica: build senza errori né avvisi, test verdi, playtest se tocchi il client (vedi [Test e playtest](Test-e-playtest)), sempre con un profilo temporaneo.

## Commit e pull request

- Messaggi in italiano, nella forma `tipo(area): descrizione` (per esempio `feat(sim): …`, `fix(save): …`, `docs: …`).
- Le **catture** da mostrare in una PR si caricano sul ramo orfano `pr-media` e si linkano con il loro URL raw, così non finiscono nella storia di `main`.

## GitHub Actions

I workflow partono **solo a mano o su un tag di release**, mai su push o pull request:

| Workflow | Quando | Cosa fa |
|---|---|---|
| `build-publish.yml` | A mano | Build e test della simulazione e del server degli agenti, test e controllo degli sprite, build del client C# |
| `release.yml` | Tag `v*` (o a mano) | Test, export Godot per Windows e Linux, zip nella Release |
| `pages.yml` | A mano da `main` o tag `v*` | Test e pubblicazione del sito (vedi [Sito web](Sito-web)) |
| `wiki.yml` | A mano | Copia `docs/wiki/` nel Wiki di GitHub: le pagine del Wiki si modificano nel repository, non sul web |

Dopo un push si lancia la build sul proprio ramo e se ne segue l'esito:

```bash
gh workflow run build-publish.yml --ref <ramo>
gh run watch <id> --exit-status
```

## Issue

Bug, richieste e lavori rinviati si tracciano nelle [issue di GitHub](https://github.com/Juan0177/tIndustry/issues). Una segnalazione utile dice cosa si è fatto, cosa ci si aspettava e cosa è successo; se il bug è stato trovato da un agente, allega il diario che lo riproduce (vedi [Agenti che giocano](Agenti-che-giocano)).

## Per approfondire

- [docs/diario/README.md](https://github.com/Juan0177/tIndustry/blob/main/docs/diario/README.md) · [docs/superpowers/](https://github.com/Juan0177/tIndustry/tree/main/docs/superpowers)
- [CLAUDE.md](https://github.com/Juan0177/tIndustry/blob/main/CLAUDE.md): le stesse regole, scritte per gli agenti AI
- [Panoramica](Sviluppo-Panoramica) · [Compilare e avviare](Compilare-e-avviare) · [Test e playtest](Test-e-playtest)

<!-- navigazione: generata da tools/dev/wiki_navigazione.py -->

---

[← Pagina precedente: Sito web](Sito-web) · [Torna alla Home](Home)

<!-- /navigazione -->
