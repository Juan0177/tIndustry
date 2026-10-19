using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using TIndustry.Shared;

namespace TIndustry.AgentPlay;

/// <summary>A map cell.</summary>
public sealed record Cella(
    [property: JsonRequired, Description("Colonna (cella).")] int X,
    [property: JsonRequired, Description("Riga (cella).")] int Y);

/// <summary>A rectangle of a capture, in pixels.</summary>
public sealed record RegioneSchermo(
    [property: JsonRequired, Description("Pixel da sinistra.")] int X,
    [property: JsonRequired, Description("Pixel dall'alto.")] int Y,
    [property: JsonRequired, Description("Larghezza in pixel.")] int Larghezza,
    [property: JsonRequired, Description("Altezza in pixel.")] int Altezza);

/// <summary>A screen point in pixels.</summary>
public sealed record PuntoSchermo(
    [property: JsonRequired, Description("Pixel da sinistra.")] int X,
    [property: JsonRequired, Description("Pixel dall'alto.")] int Y);

/// <summary>
/// The MCP tools. Each one answers a JSON text: <c>{ok:true, ...}</c> or <c>{ok:false, codice, messaggio}</c>.
/// Game tools pass their arguments unchanged (nulls left out) to the session's <see cref="IAgentEngine"/>.
/// </summary>
[McpServerToolType]
public sealed class Tools(Sessions sessions, GameData data)
{
    private const string Sessione = "Id della sessione (da apri_simulazione o apri_gioco).";
    private const string Dettaglio = "Risposta completa: senza, la risposta è compatta (niente valori vuoti o predefiniti, vedi README).";

    /// <summary>The tools' names in the order this class declares them (the order of <c>tools/list</c>).</summary>
    public static readonly List<string> Order = typeof(Tools).GetMethods()
        .Select(m => (m.MetadataToken, Tool: m.GetCustomAttributes(typeof(McpServerToolAttribute), false).OfType<McpServerToolAttribute>().FirstOrDefault()))
        .Where(m => m.Tool?.Name is not null)
        .OrderBy(m => m.MetadataToken)
        .Select(m => m.Tool!.Name!)
        .ToList();

    // The channel's options, but accents and quotes stay readable: the answers go to an agent, never into HTML.
    private static readonly JsonSerializerOptions Json = new(AgentProtocol.JsonOptions)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // ---- Sessions

    [McpServerTool(Name = "apri_simulazione")]
    [Description("Apre una partita nella sola simulazione (senza grafica, ferma finché non avanzi): la scelta veloce per provare regole e comandi.")]
    public string ApriSimulazione(
        [Description("Misura della mappa: il nome Media (1000), Grande (1500) o Enorme (2000), oppure un numero di celle per lato tra 12 e 2000 (64 o \"64\").")] JsonElement? misura = null,
        [Description("Seme del terreno.")] int seme = 1,
        [Description("Generatore del terreno: biomi-1 o classico.")] string generatore = MapGenerator.Name,
        [Description("Id di un livello della campagna (ignora misura e generatore).")] string? livello = null,
        [Description("Percorso di un file di salvataggio da caricare (copialo prima in una cartella temporanea: i salvataggi veri del giocatore sono rifiutati).")] string? salvataggio = null,
        [Description("Costruzione istantanea, senza drone e cantieri (non con salvataggio: valgono le regole salvate).")] bool costruzioneIstantanea = false,
        [Description("Tiene la cartella della sessione (diari) dopo chiudi.")] bool conserva = false) =>
        Run(() =>
        {
            var setup = Setup(misura, seme, generatore, livello, salvataggio, costruzioneIstantanea);
            SimEngine engine;
            try
            {
                engine = SimEngine.Open(setup, data, conserva);
            }
            catch (SaveMapException ex)
            {
                throw new AgentException(ex.Code.ToString(), ex.Message);
            }

            return Opened(sessions.Open(engine), engine.Kind, engine.Folder);
        });

    [McpServerTool(Name = "apri_gioco")]
    [Description("Apre il gioco vero (Godot, finestra 1280×720, profilo temporaneo) per catture e prove dell'interfaccia: più lento della simulazione, il tempo scorre da solo (pausa per fermarlo).")]
    public string ApriGioco(
        [Description("Misura della mappa: il nome Media (1000), Grande (1500) o Enorme (2000), oppure un numero di celle per lato tra 12 e 2000 (64 o \"64\").")] JsonElement? misura = null,
        [Description("Seme del terreno.")] int seme = 1,
        [Description("Generatore del terreno: biomi-1 o classico.")] string generatore = MapGenerator.Name,
        [Description("Id di un livello della campagna (ignora misura, seme e generatore).")] string? livello = null,
        [Description("Percorso di un file di salvataggio: il gioco ne carica una copia nel profilo (i salvataggi veri del giocatore sono rifiutati).")] string? salvataggio = null,
        [Description("Lingua dei testi: it (predefinita) o en.")] string? lingua = null,
        [Description("Scala dell'interfaccia da 0.75 a 2 a passi di 0.05 (predefinita 1).")] double? scala = null,
        [Description("Colori: Normale (predefinito), Daltonici o AltoContrasto.")] string? colori = null,
        [Description("Tiene il profilo temporaneo (log, catture) e i diari dopo chiudi.")] bool conserva = false) =>
        Run(() =>
        {
            var options = Options(misura, seme, generatore, livello, salvataggio, lingua, scala, colori, conserva);
            var engine = GameEngine.Open(options);
            var result = Opened(sessions.Open(engine), engine.Kind, engine.Folder);
            result["diari"] = engine.JournalFolder;
            result["processo"] = engine.ProcessId;
            return result;
        });

    [McpServerTool(Name = "collega_coop")]
    [Description("Si collega a una partita co-op: non ancora disponibile (arriva con il co-op).")]
    public string CollegaCoop([Description("Indirizzo del server co-op.")] string? indirizzo = null) =>
        Run(() => throw new AgentException(AgentCodes.MotoreNonDisponibile, "Il co-op non è ancora disponibile."));

    [McpServerTool(Name = "sessioni")]
    [Description("Elenca le sessioni aperte con il loro motore.")]
    public string Sessioni() => Run(() => JsonSerializer.SerializeToElement(
        new { sessioni = sessions.List().Select(s => new { sessione = s.Id, motore = s.Engine.Kind }) }, Json));

    [McpServerTool(Name = "chiudi")]
    [Description("Chiude una sessione e cancella la sua cartella temporanea (salvo conserva all'apertura).")]
    public string Chiudi([Description(Sessione)] string sessione) => Run(() =>
    {
        sessions.Close(sessione);
        return JsonSerializer.SerializeToElement(new { sessione }, Json);
    });

    // ---- Look

    [McpServerTool(Name = "stato")]
    [Description("Riassunto della partita: tick, soldi, risorse, energia, ricerca (le strutture già sbloccate), edifici, costruzione, Core, direzione dei piazzamenti (nel gioco vero quella del giocatore), ultimi rifiuti (tick = quello del diario: il tick in cui è arrivato il comando) e impronta (da confrontare con quella di rigioca). obiettivi e livelloCompletato ci sono solo nei livelli della campagna.")]
    public string Stato([Description(Sessione)] string sessione, [Description(Dettaglio)] bool dettaglio = false) => Game(sessione, "stato", dettaglio: dettaglio);

    [McpServerTool(Name = "area")]
    [Description("Le celle di un rettangolo (al massimo 64×64) in righe di simboli, con legenda e forme dei nastri: solo curve e T (il nastro dritto è il caso normale e non è elencato; gli incroci hanno il loro simbolo); ingresso è il verso di marcia dell'oggetto che entra. Con oggetti: quanti oggetti ci sono su ogni nastro e di che tipo.")]
    public string Area(
        [Description(Sessione)] string sessione,
        [Description("Un angolo del rettangolo.")] Cella da,
        [Description("L'angolo opposto (compreso).")] Cella a,
        [Description("Elenca anche gli oggetti sui nastri (cella, n, tipi).")] bool? oggetti = null) =>
        Game(sessione, "area", new { da, a, oggetti });

    [McpServerTool(Name = "trova")]
    [Description("Trova i giacimenti (Iron, iron-ore…) o le strutture (per id) più vicini a una cella.")]
    public string Trova(
        [Description(Sessione)] string sessione,
        [Description("Giacimento (Iron, Copper… o id del minerale) o id di una struttura.")] string cosa,
        [Description("Cella da cui misurare la distanza.")] Cella cella,
        [Description("Quanti risultati (1-200, predefinito 10).")] int? max = null) =>
        Game(sessione, "trova", new { cosa, cella, max });

    [McpServerTool(Name = "eventi")]
    [Description("Cosa è successo dall'ultima chiamata: rifiuti (con mancano, se si sa), piani scartati, costruzioni e smontaggi finiti (cella e struttura); nel gioco vero anche partitaCaricata (nuova partita, livello o salvataggio caricato, con tickPrima e indietro se il tick è tornato indietro: il diario riparte da lì) e inputReale (una persona ha usato la finestra).")]
    public string Eventi([Description(Sessione)] string sessione, [Description(Dettaglio)] bool dettaglio = false) => Game(sessione, "eventi", dettaglio: dettaglio);

    [McpServerTool(Name = "catalogo")]
    [Description("Le strutture del gioco: id, nome, tipo, costo di costruzione, costo di sblocco, prerequisiti e se è già sbloccata.")]
    public string Catalogo([Description(Sessione)] string sessione) => Game(sessione, "catalogo");

    // ---- Act (the player's commands through SimHost)

    [McpServerTool(Name = "piazza")]
    [Description("Piazza un blocco in una cella, o trascina da 'da' ad 'a' come il giocatore (la stessa linea, PlacementLine): nastri, incroci, sdoppiatori e selezionatori in retta sull'asse più lungo (al massimo 200 celle), ogni altro blocco uno solo in 'da'. Senza 'direzione' segue il trascinamento (ogni cella guarda la seguente, un blocco solo guarda verso 'a'); incrocio automatico compreso (incrocio: true sulla cella diventata un incrocio) e ponte automatico su pareti, acqua profonda o edifici in una retta di nastri, col ponte sbloccato (ponte: true sulla cella del ponte; le celle scavalcate non sono elencate). 'diagonale' è Ctrl. Risponde le celle piazzate e i rifiuti (codice, mancano), anche quelli che la linea prevede e non manda.")]
    public string Piazza(
        [Description(Sessione)] string sessione,
        [Description("Id della struttura (per esempio miner, conveyor-basic).")] string? tipo = null,
        [Description("Livello del nastro (al posto di tipo).")] int? tier = null,
        [Description("Cella del blocco (un clic).")] Cella? cella = null,
        [Description("Inizio del trascinamento.")] Cella? da = null,
        [Description("Fine del trascinamento.")] Cella? a = null,
        [Description("nord, est, sud, ovest: fissa la direzione di ogni cella, come R durante il trascinamento (senza: con 'cella' quella di ruota, con 'da'/'a' quella del trascinamento).")] string? direzione = null,
        [Description("true = Ctrl, solo nastri: segue la catena di nastri fra 'da' e 'a' (cambio di tier) o un percorso A* sui quattro lati che gira attorno agli ostacoli (mai ponti), con le sue direzioni anche con 'direzione'; senza percorso, la retta (lì 'direzione' vale).")] bool? diagonale = null,
        [Description("Ricetta di forno o assemblatore.")] string? ricetta = null,
        [Description("Oggetto del filtro del selezionatore.")] string? filtro = null,
        [Description(Dettaglio)] bool dettaglio = false) =>
        Game(sessione, "piazza", new { tipo, tier, cella, da, a, direzione, diagonale, ricetta, filtro }, dettaglio);

    [McpServerTool(Name = "smonta")]
    [Description("Smonta come il tasto destro (rimborso secondo le regole). 'cella' è il clic: segna, o toglie il segno a un blocco già segnato, cancella un piano o annulla un cantiere. 'da' e 'a' sono il rettangolo del destro trascinato (RemovalArea, al massimo 64×64 celle): segna ogni blocco, cancella ogni piano e annulla ogni cantiere che lo tocca, senza mai togliere un segno; risponde le celle mandate (la prima di ogni blocco) e i rifiuti (codice).")]
    public string Smonta(
        [Description(Sessione)] string sessione,
        [Description("Cella da smontare (un clic).")] Cella? cella = null,
        [Description("Angolo del rettangolo dove si preme il destro.")] Cella? da = null,
        [Description("Angolo opposto, dove si rilascia.")] Cella? a = null) =>
        Game(sessione, "smonta", new { cella, da, a });

    [McpServerTool(Name = "ruota")]
    [Description("Gira in senso orario la direzione dei prossimi piazzamenti, come il tasto R (nel gioco vero è proprio la direzione del giocatore).")]
    public string Ruota([Description(Sessione)] string sessione) => Game(sessione, "ruota");

    [McpServerTool(Name = "sblocca")]
    [Description("Sblocca una struttura nella ricerca, pagando il suo costo.")]
    public string Sblocca([Description(Sessione)] string sessione, [Description("Id della struttura.")] string struttura) =>
        Game(sessione, "sblocca", new { struttura });

    [McpServerTool(Name = "vendi")]
    [Description("Vende oggetti del Core al mercato.")]
    public string Vendi(
        [Description(Sessione)] string sessione,
        [Description("Id dell'oggetto.")] string oggetto,
        [Description("Quante unità.")] int quantita) =>
        Game(sessione, "vendi", new { oggetto, quantita });

    [McpServerTool(Name = "potenzia_core")]
    [Description("Potenzia il Core al livello successivo, se si può pagare.")]
    public string PotenziaCore([Description(Sessione)] string sessione) => Game(sessione, "potenzia_core");

    // ---- Time

    [McpServerTool(Name = "avanza")]
    [Description("Fa avanzare la partita di un numero esatto di tick (60 al secondo), poi la ferma.")]
    public string Avanza([Description(Sessione)] string sessione, [Description("Tick da eseguire (0-36000).")] int tick) =>
        Game(sessione, "avanza", new { tick });

    [McpServerTool(Name = "esegui")]
    [Description("Lascia scorrere il gioco vero per alcuni secondi reali, al massimo 120 (solo gioco vero).")]
    public string Esegui([Description(Sessione)] string sessione, [Description("Secondi reali, più di 0 e al massimo 120.")] double secondi) =>
        Game(sessione, "esegui", new { secondi });

    [McpServerTool(Name = "pausa")]
    [Description("Ferma il tempo della partita (la simulazione è sempre ferma tra un avanza e l'altro).")]
    public string Pausa([Description(Sessione)] string sessione) => Game(sessione, "pausa");

    [McpServerTool(Name = "riprendi")]
    [Description("Fa ripartire il tempo del gioco vero dopo pausa (solo gioco vero).")]
    public string Riprendi([Description(Sessione)] string sessione) => Game(sessione, "riprendi");

    // ---- Real game only

    [McpServerTool(Name = "cattura")]
    [Description("Cattura del gioco vero. Senza percorso l'immagine arriva nella risposta: PNG ridotto entro 640×360 (pochi token), o un vero rendering 1920×1080 con alta. Con percorso salva il PNG intero sotto <profilo>/catture e ne dà il percorso (immagine: true per averlo anche nella risposta). regione (pixel della cattura) o da e a (celle) la ritagliano.")]
    public CallToolResult Cattura(
        [Description(Sessione)] string sessione,
        [Description("Nome relativo .png sotto <profilo>/catture: salva il file.")] string? percorso = null,
        [Description("Immagine nella risposta (predefinito: sì senza percorso, no con percorso).")] bool? immagine = null,
        [Description("Vero rendering 1920×1080, come su uno schermo 1080p con l'interfaccia al 100% (più mappa, HUD disposto per quello schermo), senza riduzione.")] bool? alta = null,
        [Description("Rettangolo da ritagliare, in pixel della cattura (1280×720, o 1920×1080 con alta).")] RegioneSchermo? regione = null,
        [Description("Un angolo dell'area di celle da ritagliare (con a).")] Cella? da = null,
        [Description("L'angolo opposto dell'area di celle (compreso).")] Cella? a = null)
    {
        var text = Game(sessione, "cattura", new { percorso, immagine = immagine ?? percorso is null, alta, regione, da, a });
        var answer = JsonNode.Parse(text)!.AsObject();
        if (answer["png"]?.GetValue<string>() is not { } png)
        {
            return Text(text);
        }

        // The image is data from the game: only a well-formed PNG reaches the agent, as image content.
        answer.Remove("png");
        var buffer = new byte[png.Length];
        if (!Convert.TryFromBase64String(png, buffer, out var length) || !buffer.AsSpan(0, length).StartsWith(PngSignature))
        {
            return Text(Fail(new AgentException(AgentCodes.ErroreInterno, "Il gioco non ha dato un PNG valido.")));
        }

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = answer.ToJsonString(Json) }, ImageContentBlock.FromBytes(buffer.AsMemory(0, length), "image/png")]
        };
    }

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static CallToolResult Text(string text) => new() { Content = [new TextContentBlock { Text = text }] };

    [McpServerTool(Name = "telecamera")]
    [Description("Sposta la telecamera su una cella e cambia lo zoom, o dice dove si trova (solo gioco vero).")]
    public string Telecamera(
        [Description(Sessione)] string sessione,
        [Description("Cella da centrare.")] Cella? cella = null,
        [Description("Zoom.")] double? zoom = null) =>
        Game(sessione, "telecamera", new { cella, zoom });

    [McpServerTool(Name = "input")]
    [Description("Simula un clic, un trascinamento, un tasto o la rotellina su un punto dello schermo o su una cella (solo gioco vero).")]
    public string Input(
        [Description(Sessione)] string sessione,
        [Description("clic, trascina, tasto o rotella.")] string tipo,
        [Description("Punto dello schermo in pixel.")] PuntoSchermo? schermo = null,
        [Description("Cella del mondo.")] Cella? cella = null,
        [Description("Con rotella: su o giu. Con trascina: il punto finale, nello stesso spazio dell'inizio (schermo o cella), come {\"x\":10,\"y\":5} o \"10,5\".")] JsonElement? verso = null,
        [Description("Tasto (per esempio R, Escape).")] string? tasto = null,
        [Description("Pulsante del mouse: sinistro o destro.")] string? pulsante = null) =>
        Game(sessione, "input", new { tipo, schermo, cella, verso, tasto, pulsante });

    [McpServerTool(Name = "ui")]
    [Description("Elenca i controlli dell'interfaccia che un clic raggiunge (nome, tipo, testo, suggerimento, rettangolo; non quelli trasparenti, coperti da un pannello o doppi) o clicca quello dato (solo gioco vero).")]
    public string Ui(
        [Description(Sessione)] string sessione,
        [Description("Controllo da cliccare: il nome (anche solo la sua parte finale, come lo dà ui), il testo visibile o il suggerimento, per esempio Ricerca o Nastro T1; se ne indica più di uno la risposta dice che è ambiguo.")] string? clic = null,
        [Description(Dettaglio)] bool dettaglio = false) =>
        Game(sessione, "ui", new { clic }, dettaglio);

    [McpServerTool(Name = "log")]
    [Description("Le ultime righe di avviso ed errore del log di Godot (solo gioco vero).")]
    public string Log(
        [Description(Sessione)] string sessione,
        [Description("Quante righe (1-500, predefinite 50).")] int? righe = null,
        [Description("Tutte le righe, non solo avvisi ed errori.")] bool? tutte = null) =>
        Game(sessione, "log", new { righe, tutte });

    // ---- Replay

    [McpServerTool(Name = "diario")]
    [Description("Le azioni della sessione con il loro tick (il tick in cui è arrivata la chiamata; un comando si applica nel tick dopo), fine (il tick di adesso, fin dove arriva rigioca), rigiocabile e avvisi; con nome le salva in un file e ne dà il percorso, per rigioca. Nel gioco vero il diario riparte quando la partita cambia (vedi eventi partitaCaricata).")]
    public string Diario(
        [Description(Sessione)] string sessione,
        [Description("Nome semplice del file (lettere, cifre, - e _).")] string? nome = null,
        [Description(Dettaglio)] bool dettaglio = false) =>
        Game(sessione, "diario", new { nome }, dettaglio);

    [McpServerTool(Name = "rigioca")]
    [Description("Riesegue un diario salvato (anche del gioco vero: i comandi, non input e clic dell'interfaccia) su una sessione nuova di simulazione fino al tick fine del diario, per riprodurre un bug. Risponde l'impronta (confrontala con quella di stato) e gli avvisi: cosa non si è potuto rigiocare uguale.")]
    public string Rigioca(
        [Description("Percorso del file del diario (da diario con nome); vanno bene anche le barre / al posto di \\.")] string percorso,
        [Description("Tiene la cartella della nuova sessione dopo chiudi.")] bool conserva = false) =>
        Run(() =>
        {
            var engine = SimEngine.Replay(AgentJournal.Load(percorso), data, conserva);
            var result = Opened(sessions.Open(engine), engine.Kind, engine.Folder);
            result["impronta"] = engine.Checksum().ToString("x16", CultureInfo.InvariantCulture);
            if (engine.ReplayWarnings.Count > 0)
            {
                result["avvisi"] = new JsonArray(engine.ReplayWarnings.Select(w => (JsonNode)w).ToArray());
            }

            return result;
        });

    /// <summary>Arguments that do not fit the tool (see <see cref="Arguments"/>): the same <c>ParametriNonValidi</c> as the engine's.</summary>
    public static string BadArguments(string message) => Fail(new AgentException(AgentCodes.ParametriNonValidi, message));

    private string Game(string sessione, string method, object? args = null, bool dettaglio = false) =>
        Run(() =>
        {
            var engine = sessions.Get(sessione);
            try
            {
                return engine.Call(method, JsonSerializer.SerializeToElement(args ?? new { }, Json));
            }
            catch (AgentException) when (engine is GameEngine { Ended: true })
            {
                // The game crashed or exited: this answer (GiocoTerminato) is the last; then the session is unknown.
                try
                {
                    sessions.Close(sessione);
                }
                catch (AgentException)
                {
                    // A parallel call closed it first.
                }

                throw;
            }
        }, dettaglio ? null : method);

    private static JsonObject Opened(string id, string kind, string folder) => new()
    {
        ["sessione"] = id,
        ["motore"] = kind,
        ["cartella"] = folder
    };

    // apri_gioco's options, checked before Godot is started.
    private GameOptions Options(
        JsonElement? misura, int seme, string generatore, string? livello, string? salvataggio, string? lingua, double? scala,
        string? colori, bool conserva)
    {
        var project = GameLauncher.RequireProject();
        var size = Size(misura);
        if (size is < 12 or > Factory.MaxMapEdge)
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, $"'misura' tra 12 e {Factory.MaxMapEdge}.");
        }

        if (!AgentGameSetup.Generators.Contains(generatore))
        {
            throw new AgentException(
                AgentCodes.ParametriNonValidi, $"'generatore' è uno di: {string.Join(", ", AgentGameSetup.Generators)}.");
        }

        if (livello is not null && salvataggio is not null)
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, "Dai livello oppure salvataggio, non tutti e due.");
        }

        if (livello is not null && data.Campaign.Find(livello) is null)
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, $"Livello di campagna sconosciuto: {livello}.");
        }

        if (salvataggio is not null)
        {
            salvataggio = Path.GetFullPath(salvataggio);
            RefuseRealSave(salvataggio);
            if (!File.Exists(salvataggio))
            {
                throw new AgentException(AgentCodes.ParametriNonValidi, $"Salvataggio non trovato: {salvataggio}.");
            }
        }

        var languages = GameLauncher.Languages(project);
        lingua ??= "it";
        if (!languages.Contains(lingua))
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, $"'lingua' è una di: {string.Join(", ", languages)}.");
        }

        var percent = (int)Math.Round((scala ?? 1.0) * 100);
        if (percent < GameLauncher.ScaleMin || percent > GameLauncher.ScaleMax || percent % GameLauncher.ScaleStep != 0)
        {
            static string Ratio(int percent) => (percent / 100.0).ToString(CultureInfo.InvariantCulture);
            throw new AgentException(
                AgentCodes.ParametriNonValidi,
                $"'scala' da {Ratio(GameLauncher.ScaleMin)} a {Ratio(GameLauncher.ScaleMax)} a passi di {Ratio(GameLauncher.ScaleStep)}.");
        }

        colori ??= GameLauncher.Colours[0];
        if (!GameLauncher.Colours.Contains(colori))
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, $"'colori' è uno di: {string.Join(", ", GameLauncher.Colours)}.");
        }

        return new GameOptions(lingua, percent, colori, livello, seme, size, generatore, salvataggio, conserva);
    }

    private static AgentGameSetup Setup(
        JsonElement? misura, int seme, string generatore, string? livello, string? salvataggio, bool instant)
    {
        if (livello is not null && salvataggio is not null)
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, "Dai livello oppure salvataggio, non tutti e due.");
        }

        if (instant && salvataggio is not null)
        {
            throw new AgentException(
                AgentCodes.ParametriNonValidi, "costruzioneIstantanea non vale con salvataggio: la partita tiene le regole salvate.");
        }

        var rules = new AgentRulesSetup(InstantBuild: instant);
        var sandbox = new AgentGameSetup(AgentGameSetup.SimulationEngine, Size(misura), seme, generatore, rules);
        return salvataggio is not null ? sandbox with { StartSnapshot = SaveSnapshot(salvataggio) }
            : livello is not null ? sandbox with { Level = livello }
            : sandbox;
    }

    private static int Size(JsonElement? misura) => misura switch
    {
        null or { ValueKind: JsonValueKind.Null } => (int)MapSize.Media,
        { ValueKind: JsonValueKind.Number } m when m.TryGetInt32(out var cells) => cells,
        { ValueKind: JsonValueKind.String } m when int.TryParse(
            m.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var cells) => cells,
        { ValueKind: JsonValueKind.String } m when Enum.TryParse<MapSize>(m.GetString(), ignoreCase: true, out var size)
            && Enum.IsDefined(size) => (int)size,
        _ => throw new AgentException(
            AgentCodes.ParametriNonValidi, "'misura' è Media, Grande, Enorme o un numero di celle per lato (12-2000).")
    };

    /// <summary>
    /// The snapshot of a save file given by path, read with the game's own reader (<see cref="FactorySaveStore.TryReadSaveFile"/>);
    /// never one under the player's real data folders.
    /// </summary>
    private static void RefuseRealSave(string full)
    {
        if (RealDataRoots().Any(root => full.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AgentException(
                AgentCodes.ParametriNonValidi,
                "Questo è un salvataggio vero del giocatore: copialo prima in una cartella temporanea e dai quel percorso.");
        }
    }

    internal static string SaveSnapshot(string path)
    {
        var full = Path.GetFullPath(path);
        RefuseRealSave(full);
        if (!FactorySaveStore.TryReadSaveFile(full, out var file, out var failure))
        {
            throw failure is null
                ? new AgentException(AgentCodes.ParametriNonValidi, $"Salvataggio non trovato: {full}.")
                : new AgentException(failure.Code.ToString(), failure.Detail);
        }

        // A damaged snapshot is the game's "unreadable", not a bad parameter.
        return Convert.TryFromBase64String(file!.Snapshot!, new byte[file.Snapshot!.Length], out _)
            ? file.Snapshot
            : throw new AgentException(nameof(SaveLoadError.Unreadable), FactorySaveStore.UnreadableMessage);
    }

    /// <summary>The player's real data folders (never the TINDUSTRY_DATA_DIR override), each ending with a separator.</summary>
    internal static IEnumerable<string> RealDataRoots() =>
        new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "tIndustry"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Godot", "app_userdata", "tIndustry")
        }.Select(root => Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar);

    /// <summary>
    /// <paramref name="call"/>'s object (a JsonElement or JsonObject) after <c>ok:true</c>, made <see cref="Compact"/>
    /// for the game tool <paramref name="compact"/> names (null: as it is), or its error.
    /// </summary>
    private static string Run(Func<object> call, string? compact = null)
    {
        try
        {
            var result = new JsonObject { ["ok"] = true };
            foreach (var (key, value) in JsonSerializer.SerializeToNode(call(), Json)!.AsObject().ToList())
            {
                result[key] = value?.DeepClone();
            }

            if (compact is not null)
            {
                Compact.Apply(compact, result);
            }

            return result.ToJsonString(Json);
        }
        catch (AgentException ex)
        {
            if (ex.Detail is not null)
            {
                Console.Error.WriteLine(ex.Detail);
            }

            return Fail(ex);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return Fail(new AgentException(AgentCodes.ErroreInterno, $"{ex.GetType().Name}: {ex.Message}"));
        }
    }

    private static string Fail(AgentException ex)
    {
        var answer = new JsonObject { ["ok"] = false, ["codice"] = ex.Code, ["messaggio"] = ex.Message };
        if (ex.Missing is not null)
        {
            answer["mancano"] = ex.Missing.DeepClone();
        }

        return answer.ToJsonString(Json);
    }
}
