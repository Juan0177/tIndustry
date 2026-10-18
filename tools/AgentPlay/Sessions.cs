using System.Collections.Concurrent;
using System.Security.Cryptography;
using TIndustry.Shared;

namespace TIndustry.AgentPlay;

/// <summary>
/// The open games of this server by id (<c>s3f9a-1</c>, <c>s3f9a-2</c>…: a prefix per server process, so an id kept
/// from a previous server is unknown instead of naming another game); closes them all when the server stops.
/// </summary>
public sealed class Sessions : IDisposable
{
    private readonly ConcurrentDictionary<string, IAgentEngine> open = new(StringComparer.Ordinal);
    private readonly string prefix = "s" + Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToLowerInvariant() + "-";
    private int lastId;

    public string Open(IAgentEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        var id = prefix + Interlocked.Increment(ref lastId);
        open[id] = engine;
        return id;
    }

    /// <summary>The engine of <paramref name="id"/>; <c>SessioneSconosciuta</c> when it is not open.</summary>
    public IAgentEngine Get(string id) =>
        open.TryGetValue(id, out var engine)
            ? engine
            : throw new AgentException(AgentCodes.SessioneSconosciuta, $"Nessuna sessione aperta con id '{id}'.");

    public void Close(string id)
    {
        if (!open.TryRemove(id, out var engine))
        {
            throw new AgentException(AgentCodes.SessioneSconosciuta, $"Nessuna sessione aperta con id '{id}'.");
        }

        engine.Close();
    }

    /// <summary>Open sessions, oldest first.</summary>
    public IReadOnlyList<(string Id, IAgentEngine Engine)> List() =>
        open.Select(kv => (kv.Key, kv.Value)).OrderBy(s => int.Parse(s.Key[prefix.Length..])).ToList();

    public void Dispose()
    {
        foreach (var id in open.Keys.ToList())
        {
            if (open.TryRemove(id, out var engine))
            {
                engine.Close();
            }
        }
    }
}
