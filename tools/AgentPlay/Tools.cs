using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using TIndustry.Shared;

namespace TIndustry.AgentPlay;

/// <summary>A map cell.</summary>
public sealed record Cella(
    [property: JsonRequired, Description("Colonna (cella).")] int X,
    [property: JsonRequired, Description("Riga (cella).")] int Y);

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
        [Description("Seme del terreno (generatore biomi-1).")] int seme = 1,
        [Description("Id di un livello della campagna (ignora misura e seme).")] string? livello = null,
        [Description("Lingua dei testi: it (predefinita) o en.")] string? lingua = null,
        [Description("Scala dell'interfaccia da 0.75 a 2.0 a passi di 0.05 (predefinita 1.0).")] double? scala = null,
        [Description("Colori: Normale (predefinito), Daltonici o AltoContrasto.")] string? colori = null,
        [Description("Tiene il profilo temporaneo (log, catture, diari) dopo chiudi.")] bool conserva = false) =>
        Run(() =>
        {
            var options = Options(misura, seme, livello, lingua, scala, colori, conserva);
            var engine = GameEngine.Open(options);
            var result = Opened(sessions.Open(engine), engine.Kind, engine.Folder);
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
    [Description("Riassunto della partita: tick, soldi, risorse, energia, ricerca, obiettivi, edifici, costruzione, Core, ultimi rifiuti.")]
    public string Stato([Description(Sessione)] string sessione) => Game(sessione, "stato");

    [McpServerTool(Name = "area")]
    [Description("Le celle di un rettangolo (al massimo 64×64) in righe di simboli, con legenda e forme dei nastri.")]
    public string Area(
        [Description(Sessione)] string sessione,
        [Description("Un angolo del rettangolo.")] Cella da,
        [Description("L'angolo opposto (compreso).")] Cella a) =>
        Game(sessione, "area", new { da, a });

    [McpServerTool(Name = "trova")]
    [Description("Trova i giacimenti (Iron, iron-ore…) o le strutture (per id) più vicini a una cella.")]
    public string Trova(
        [Description(Sessione)] string sessione,
        [Description("Giacimento (Iron, Copper… o id del minerale) o id di una struttura.")] string cosa,
        [Description("Cella da cui misurare la distanza.")] Cella cella,
        [Description("Quanti risultati (1-200, predefinito 10).")] int? max = null) =>
        Game(sessione, "trova", new { cosa, cella, max });

    [McpServerTool(Name = "eventi")]
    [Description("Cosa è successo dall'ultima chiamata: rifiuti, piani scartati, costruzioni e smontaggi finiti.")]
    public string Eventi([Description(Sessione)] string sessione) => Game(sessione, "eventi");

    // ---- Act (the player's commands through SimHost)

    [McpServerTool(Name = "piazza")]
    [Description("Piazza un blocco in una cella, o una linea di nastri da 'da' ad 'a' come il trascinamento del giocatore.")]
    public string Piazza(
        [Description(Sessione)] string sessione,
        [Description("Id della struttura (per esempio miner, conveyor-basic).")] string? tipo = null,
        [Description("Livello del nastro (al posto di tipo).")] int? tier = null,
        [Description("Cella del blocco.")] Cella? cella = null,
        [Description("Inizio della linea.")] Cella? da = null,
        [Description("Fine della linea.")] Cella? a = null,
        [Description("nord, est, sud, ovest (predefinita: quella di ruota).")] string? direzione = null,
        [Description("Ricetta di forno o assemblatore.")] string? ricetta = null,
        [Description("Oggetto del filtro del selezionatore.")] string? filtro = null) =>
        Game(sessione, "piazza", new { tipo, tier, cella, da, a, direzione, ricetta, filtro });

    [McpServerTool(Name = "smonta")]
    [Description("Smonta la struttura in una cella (rimborso secondo le regole).")]
    public string Smonta([Description(Sessione)] string sessione, [Description("Cella da smontare.")] Cella cella) =>
        Game(sessione, "smonta", new { cella });

    [McpServerTool(Name = "ruota")]
    [Description("Gira in senso orario la direzione dei prossimi piazzamenti, come il tasto R.")]
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
    [Description("Lascia scorrere il gioco vero per alcuni secondi reali (solo gioco vero).")]
    public string Esegui([Description(Sessione)] string sessione, [Description("Secondi reali.")] double secondi) =>
        Game(sessione, "esegui", new { secondi });

    [McpServerTool(Name = "pausa")]
    [Description("Ferma il tempo della partita (la simulazione è sempre ferma tra un avanza e l'altro).")]
    public string Pausa([Description(Sessione)] string sessione) => Game(sessione, "pausa");

    [McpServerTool(Name = "riprendi")]
    [Description("Fa ripartire il tempo del gioco vero dopo pausa (solo gioco vero).")]
    public string Riprendi([Description(Sessione)] string sessione) => Game(sessione, "riprendi");

    // ---- Real game only

    [McpServerTool(Name = "cattura")]
    [Description("Salva una cattura PNG della finestra e restituisce il percorso del file (solo gioco vero).")]
    public string Cattura([Description(Sessione)] string sessione, [Description("Percorso del PNG (facoltativo).")] string? percorso = null) =>
        Game(sessione, "cattura", new { percorso });

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
    [Description("Elenca i controlli visibili dell'interfaccia (nome, tipo, testo, rettangolo) o clicca quello col nome dato (solo gioco vero).")]
    public string Ui([Description(Sessione)] string sessione, [Description("Nome del controllo da cliccare.")] string? clic = null) =>
        Game(sessione, "ui", new { clic });

    [McpServerTool(Name = "log")]
    [Description("Le ultime righe di avviso ed errore del log di Godot (solo gioco vero).")]
    public string Log(
        [Description(Sessione)] string sessione,
        [Description("Quante righe (1-500, predefinite 50).")] int? righe = null,
        [Description("Tutte le righe, non solo avvisi ed errori.")] bool? tutte = null) =>
        Game(sessione, "log", new { righe, tutte });

    // ---- Replay

    [McpServerTool(Name = "diario")]
    [Description("Le azioni della sessione con il loro tick; con nome le salva in un file e ne dà il percorso, per rigioca.")]
    public string Diario(
        [Description(Sessione)] string sessione,
        [Description("Nome semplice del file (lettere, cifre, - e _).")] string? nome = null) =>
        Game(sessione, "diario", new { nome });

    [McpServerTool(Name = "rigioca")]
    [Description("Riesegue un diario salvato (anche del gioco vero: solo i comandi, non gli input) su una sessione nuova di simulazione, per riprodurre un bug.")]
    public string Rigioca(
        [Description("Percorso del file del diario (da diario con nome).")] string percorso,
        [Description("Tiene la cartella della nuova sessione dopo chiudi.")] bool conserva = false) =>
        Run(() =>
        {
            var engine = SimEngine.Replay(AgentJournal.Load(percorso), data, conserva);
            var result = Opened(sessions.Open(engine), engine.Kind, engine.Folder);
            result["impronta"] = engine.Checksum().ToString("x16", CultureInfo.InvariantCulture);
            return result;
        });

    /// <summary>Arguments that do not fit the tool (see <see cref="Arguments"/>): the same <c>ParametriNonValidi</c> as the engine's.</summary>
    public static string BadArguments(string message) => Fail(new AgentException(AgentCodes.ParametriNonValidi, message));

    private string Game(string sessione, string method, object? args = null) =>
        Run(() => sessions.Get(sessione).Call(method, JsonSerializer.SerializeToElement(args ?? new { }, Json)));

    private static JsonObject Opened(string id, string kind, string folder) => new()
    {
        ["sessione"] = id,
        ["motore"] = kind,
        ["cartella"] = folder
    };

    // apri_gioco's options, checked before Godot is started.
    private GameOptions Options(
        JsonElement? misura, int seme, string? livello, string? lingua, double? scala, string? colori, bool conserva)
    {
        var size = Size(misura);
        if (size is < 12 or > Factory.MaxMapEdge)
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, $"'misura' tra 12 e {Factory.MaxMapEdge}.");
        }

        if (livello is not null && data.Campaign.Find(livello) is null)
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, $"Livello di campagna sconosciuto: {livello}.");
        }

        var languages = GameLauncher.FindProject() is { } project ? GameLauncher.Languages(project) : ["it"];
        lingua ??= "it";
        if (!languages.Contains(lingua))
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, $"'lingua' è una di: {string.Join(", ", languages)}.");
        }

        var percent = (int)Math.Round((scala ?? 1.0) * 100);
        if (percent is < 75 or > 200 || percent % 5 != 0)
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, "'scala' da 0.75 a 2.0 a passi di 0.05.");
        }

        colori ??= GameLauncher.Colours[0];
        if (!GameLauncher.Colours.Contains(colori))
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, $"'colori' è uno di: {string.Join(", ", GameLauncher.Colours)}.");
        }

        return new GameOptions(lingua, percent, colori, livello, seme, size, conserva);
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
    private static string SaveSnapshot(string path)
    {
        var full = Path.GetFullPath(path);
        if (RealDataRoots().Any(root => full.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AgentException(
                AgentCodes.ParametriNonValidi,
                "Questo è un salvataggio vero del giocatore: copialo prima in una cartella temporanea e dai quel percorso.");
        }

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

    /// <summary><paramref name="call"/>'s object (a JsonElement or JsonObject) after <c>ok:true</c>, or its error.</summary>
    private static string Run(Func<object> call)
    {
        try
        {
            var result = new JsonObject { ["ok"] = true };
            foreach (var (key, value) in JsonSerializer.SerializeToNode(call(), Json)!.AsObject().ToList())
            {
                result[key] = value?.DeepClone();
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

    private static string Fail(AgentException ex) =>
        new JsonObject { ["ok"] = false, ["codice"] = ex.Code, ["messaggio"] = ex.Message }.ToJsonString(Json);
}
