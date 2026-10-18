using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Shared;

/// <summary>One journal line: the method an agent called at <paramref name="Tick"/> (game tick before the call).</summary>
public sealed record AgentJournalEntry(
    long Tick,
    [property: JsonPropertyName("metodo")] string Method,
    [property: JsonPropertyName("parametri")] JsonElement Params);

/// <summary>Rules of the journaled game (<see cref="GameRules"/> with the journal's Italian keys).</summary>
public sealed record AgentRulesSetup(
    [property: JsonPropertyName("rimborso")] int RefundPercent = 100,
    [property: JsonPropertyName("costruzioneIstantanea")] bool InstantBuild = false,
    [property: JsonPropertyName("notte")] bool Night = false)
{
    public static AgentRulesSetup From(GameRules rules) => new(rules.RefundPercent, rules.InstantBuild, rules.Night);

    public GameRules ToGameRules() => new(RefundPercent, InstantBuild, Night);
}

/// <summary>
/// How the journaled game was made, so <see cref="AgentGame.Replay(AgentJournal, FactoryContent, CampaignCatalog?, string?)"/>
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

    public static AgentGameSetup Sandbox(int size, int seed, string generator, GameRules rules) =>
        new(SimulationEngine, size, seed, generator, AgentRulesSetup.From(rules));

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
/// File: <c>{versione, partita, voci}</c>.
/// </summary>
public sealed class AgentJournal
{
    public const int CurrentVersion = 1;

    private readonly List<AgentJournalEntry> entries = [];

    public AgentGameSetup? Setup { get; init; }

    public IReadOnlyList<AgentJournalEntry> Entries => entries;

    public void Add(long tick, string method, JsonElement @params) =>
        entries.Add(new AgentJournalEntry(tick, method, @params.Clone()));

    /// <summary>Writes the journal atomically; I/O failures become <c>ParametriNonValidi</c>.</summary>
    public void Save(string path)
    {
        var json = JsonSerializer.Serialize(new JournalFile(CurrentVersion, Setup, entries), AgentProtocol.JsonOptions);
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

        var journal = new AgentJournal { Setup = file.Setup };
        foreach (var entry in file.Entries)
        {
            journal.Add(entry.Tick, entry.Method, entry.Params);
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
        [property: JsonPropertyName("voci")] List<AgentJournalEntry>? Entries);
}
