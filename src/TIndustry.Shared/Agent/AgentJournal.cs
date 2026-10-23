using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Shared;

/// <summary>
/// One journal line: the method an agent called at <paramref name="Tick"/> (the game tick when the call arrived; a
/// command applies in the tick after it). <paramref name="Replayable"/> false (<c>rigiocabile</c>): done in the real
/// game only (<c>input</c>, <c>ui</c> clicks), <c>rigioca</c> skips it with a warning.
/// </summary>
public sealed record AgentJournalEntry(
    long Tick,
    [property: JsonPropertyName("metodo")] string Method,
    [property: JsonPropertyName("parametri")] JsonElement Params,
    [property: JsonPropertyName("rigiocabile")] bool? Replayable = null);

/// <summary>
/// Rules of the journaled game (<see cref="GameRules"/> with the journal's Italian keys). <paramref name="DayNight"/> is
/// the world's time by its campaign name (<see cref="DayNightName"/>): <c>ciclo</c>, <c>giorno</c> or <c>notte</c>.
/// </summary>
public sealed record AgentRulesSetup(
    [property: JsonPropertyName("rimborso")] int RefundPercent = 100,
    [property: JsonPropertyName("costruzioneIstantanea")] bool InstantBuild = false,
    [property: JsonPropertyName("giornoNotte")] string DayNight = AgentRulesSetup.Cycle)
{
    public const string Cycle = "ciclo";
    public const string AlwaysDay = "giorno";
    public const string AlwaysNight = "notte";

    public static AgentRulesSetup From(GameRules rules) =>
        new(rules.RefundPercent, rules.InstantBuild, DayNightName(rules.DayNight));

    /// <summary>The game's rules; an unknown <see cref="DayNight"/> name (a journal written by hand) is <c>ParametriNonValidi</c>.</summary>
    public GameRules ToGameRules() => new(RefundPercent, InstantBuild, ParseDayNight(DayNight));

    /// <summary>The journal's (and the campaign's) name of <paramref name="mode"/>.</summary>
    public static string DayNightName(DayNightMode mode) => mode switch
    {
        DayNightMode.AlwaysDay => AlwaysDay,
        DayNightMode.AlwaysNight => AlwaysNight,
        _ => Cycle
    };

    private static DayNightMode ParseDayNight(string? name) => name switch
    {
        null or Cycle => DayNightMode.Cycle,
        AlwaysDay => DayNightMode.AlwaysDay,
        AlwaysNight => DayNightMode.AlwaysNight,
        _ => throw new AgentException(
            AgentCodes.ParametriNonValidi, $"Tempo del mondo sconosciuto nel diario: \"{name}\" (ciclo, giorno o notte).")
    };
}

/// <summary>
/// How the journaled game was made, so <see cref="AgentGame.Replay(AgentJournal, FactoryContent, CampaignCatalog?, string?, string?)"/>
/// can make it again: a sandbox (<paramref name="Size"/>, <paramref name="Seed"/>, <paramref name="Generator"/>
/// <c>classico</c> or <c>biomi-1</c>, Core at the centre), a campaign <paramref name="Level"/>, or a starting snapshot
/// (<paramref name="StartSnapshot"/>, base64 of <see cref="SimHost.CreateSnapshot"/>, e.g. a loaded save).
/// </summary>
public sealed record AgentGameSetup(
    [property: JsonPropertyName("motore")] string Engine,
    [property: JsonPropertyName("misura")] int Size,
    [property: JsonPropertyName("seme")] int Seed,
    [property: JsonPropertyName("generatore")] string Generator,
    [property: JsonPropertyName("regole")] AgentRulesSetup Rules,
    [property: JsonPropertyName("livello")] string? Level = null,
    [property: JsonPropertyName("partenza")] string? StartSnapshot = null,
    [property: JsonPropertyName("tickPerComando")] bool TickPerCommand = true)
{
    public const string SimulationEngine = "simulazione";

    /// <summary>The terrain generators <see cref="CreateHost"/> knows (a new generator gets a new name here).</summary>
    public static readonly IReadOnlyList<string> Generators = [ClassicTerrainGenerator.Name, MapGenerator.Name];

    /// <summary>The real game's journals (written by the client's channel).</summary>
    public const string GameEngine = "gioco";

    public static AgentGameSetup Sandbox(int size, int seed, string generator, GameRules rules) =>
        new(SimulationEngine, size, seed, generator, AgentRulesSetup.From(rules));

    /// <summary>The game of <paramref name="host"/> as it is now (a loaded save, a game seen half-way): its snapshot.</summary>
    public static AgentGameSetup Snapshot(string engine, SimHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        return new(engine, host.State.Terrain?.Width ?? 0, 0, "", AgentRulesSetup.From(host.State.Rules),
            StartSnapshot: Convert.ToBase64String(host.CreateSnapshot()));
    }

    /// <summary>A new host for this setup; <see cref="AgentException"/> (<c>ParametriNonValidi</c>) when it cannot be made.</summary>
    public SimHost CreateHost(FactoryContent content, CampaignCatalog? campaign = null, string? mapsDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        var rules = (Rules ?? new AgentRulesSetup()).ToGameRules();
        try
        {
            if (StartSnapshot is not null)
            {
                return SimHost.FromSnapshot(content, Convert.FromBase64String(StartSnapshot), mapsDirectory);
            }

            if (Level is not null)
            {
                var level = campaign?.Find(Level)
                    ?? throw new AgentException(AgentCodes.ParametriNonValidi, $"Livello di campagna sconosciuto: {Level}.");
                return new SimHost(NewGame.Campaign(content, campaign, level, rules: rules, mapsDirectory: mapsDirectory));
            }
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or IOException)
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, $"Partita non ricreabile: {ex.Message}");
        }

        if (Size is < 12 or > Factory.MaxMapEdge)
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, $"Misura tra 12 e {Factory.MaxMapEdge}.");
        }

        var core = Factory.CenteredCoreOrigin(Size, Size);
        return Generator switch
        {
            ClassicTerrainGenerator.Name => new SimHost(
                NewGame.Sandbox(content, core, mapWidth: Size, mapHeight: Size, seed: Seed, rules: rules)),
            MapGenerator.Name => new SimHost(NewGame.Sandbox(
                content, core, rules: rules, map: new MapGenSettings(Size, Size, Seed, MapGenSettings.AllBiomes))),
            _ => throw new AgentException(AgentCodes.ParametriNonValidi, $"Generatore sconosciuto: {Generator}.")
        };
    }
}

/// <summary>
/// The calls of a session that change the game or its time (see <see cref="AgentGame.JournaledMethods"/>), with
/// their tick, after the <see cref="Setup"/> of the game: replaying them on the same setup reproduces what an agent found.
/// File: <c>{versione, partita, voci, fine, avvisi}</c>: <c>fine</c> is the tick when the journal was saved (the time
/// after the last call replays too), <c>avvisi</c> what may make the replay differ from the game.
/// </summary>
public sealed class AgentJournal
{
    /// <summary>
    /// Version of the journal file: 3 since the rules say the world's time (<c>giornoNotte</c>, before a <c>notte</c>
    /// flag); 2 was the 3×3 shard, 1 the 2×2 Core on another map. Older journals are refused rather than replayed.
    /// </summary>
    public const int CurrentVersion = 3;

    private readonly List<AgentJournalEntry> entries = [];

    public AgentGameSetup? Setup { get; init; }

    public IReadOnlyList<AgentJournalEntry> Entries => entries;

    /// <summary>The game tick when the journal was last saved or listed: <c>rigioca</c> runs up to it.</summary>
    public long? EndTick { get; set; }

    /// <summary>What may make a replay differ from the game (a command without outcome, an unreadable start…).</summary>
    public List<string> Warnings { get; } = [];

    public void Add(long tick, string method, JsonElement @params, bool replayable = true) =>
        entries.Add(new AgentJournalEntry(tick, method, @params.Clone(), replayable ? null : false));

    /// <summary>Writes the journal atomically; I/O failures become <c>ParametriNonValidi</c>.</summary>
    public void Save(string path)
    {
        var file = new JournalFile(CurrentVersion, Setup, entries, EndTick, Warnings.Count > 0 ? Warnings : null);
        var json = JsonSerializer.Serialize(file, AgentProtocol.JsonOptions);
        Guard(path, () => AtomicFile.WriteAllText(path, json));
    }

    /// <summary>Reads a journal; a missing, unreadable or newer file is <c>ParametriNonValidi</c>.</summary>
    public static AgentJournal Load(string path)
    {
        JournalFile? file = null;
        Guard(path, () => file = JsonSerializer.Deserialize<JournalFile>(File.ReadAllText(path), AgentProtocol.JsonOptions));
        if (file is null || file.Version != CurrentVersion || file.Entries is null)
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, $"Diario non valido: {path}.");
        }

        var journal = new AgentJournal { Setup = file.Setup, EndTick = file.EndTick };
        journal.Warnings.AddRange(file.Warnings ?? []);
        foreach (var entry in file.Entries)
        {
            journal.Add(entry.Tick, entry.Method, entry.Params, entry.Replayable != false);
        }

        return journal;
    }

    private static void Guard(string path, Action io)
    {
        try
        {
            io();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
            or NotSupportedException or ArgumentException)
        {
            throw new AgentException(AgentCodes.ParametriNonValidi, $"Diario {path}: {ex.Message}");
        }
    }

    private sealed record JournalFile(
        [property: JsonPropertyName("versione")] int Version,
        [property: JsonPropertyName("partita")] AgentGameSetup? Setup,
        [property: JsonPropertyName("voci")] List<AgentJournalEntry>? Entries,
        [property: JsonPropertyName("fine")] long? EndTick = null,
        [property: JsonPropertyName("avvisi")] List<string>? Warnings = null);
}
