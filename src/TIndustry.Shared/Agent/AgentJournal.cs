using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Shared;

/// <summary>One journal line: the method an agent called at <paramref name="Tick"/> (game tick before the call).</summary>
public sealed record AgentJournalEntry(
    long Tick,
    [property: JsonPropertyName("metodo")] string Method,
    [property: JsonPropertyName("parametri")] JsonElement Params);

/// <summary>
/// The calls of a session that change the game or its time (see <see cref="AgentGame.JournaledMethods"/>), with
/// their tick: <see cref="AgentGame.Replay"/> runs them again on a new game to reproduce what an agent found.
/// </summary>
public sealed class AgentJournal
{
    private readonly List<AgentJournalEntry> entries = [];

    public IReadOnlyList<AgentJournalEntry> Entries => entries;

    public void Add(long tick, string method, JsonElement @params) =>
        entries.Add(new AgentJournalEntry(tick, method, @params.Clone()));

    public void Save(string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(entries, AgentProtocol.JsonOptions));

    public static AgentJournal Load(string path)
    {
        var journal = new AgentJournal();
        var read = JsonSerializer.Deserialize<List<AgentJournalEntry>>(File.ReadAllText(path), AgentProtocol.JsonOptions)
            ?? throw new AgentException(AgentCodes.ParametriNonValidi, $"Diario vuoto: {path}");
        foreach (var entry in read)
        {
            journal.Add(entry.Tick, entry.Method, entry.Params);
        }

        return journal;
    }
}
