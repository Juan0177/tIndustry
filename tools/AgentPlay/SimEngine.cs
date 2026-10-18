using System.Text.Json;
using TIndustry.Shared;

namespace TIndustry.AgentPlay;

/// <summary>One game an agent plays: the simulation here, the real game (Task 4) or co-op (SP5) behind the same calls.</summary>
public interface IAgentEngine
{
    /// <summary>One method of the channel (<c>stato</c>, <c>piazza</c>…); ok:false is an <see cref="AgentException"/>.</summary>
    JsonElement Call(string method, JsonElement p);

    void Close();

    string Kind { get; }
}

/// <summary>Content, campaign and map files the server opens games with (<c>data/</c> copied next to the server).</summary>
public sealed record GameData(FactoryContent Content, CampaignCatalog Campaign, string MapsDirectory)
{
    public static GameData Load(string dataDirectory) => new(
        FactoryContent.Load(Path.Combine(dataDirectory, "content.json")),
        CampaignCatalog.Load(Path.Combine(dataDirectory, "campaign.json")),
        Path.Combine(dataDirectory, "maps"));
}

/// <summary>
/// The simulation engine: an <see cref="AgentGame"/> on a <see cref="SimHost"/> in this process, paused (time moves
/// only with <c>avanza</c> and with each command's tick). Its journals go into <see cref="Folder"/>, a temporary folder
/// deleted on <see cref="Close"/> unless kept.
/// </summary>
public sealed class SimEngine : IAgentEngine
{
    public const string SimulationKind = AgentGameSetup.SimulationEngine;

    /// <summary>Temporary root of every session folder of this server.</summary>
    public static readonly string TempRoot = Path.Combine(Path.GetTempPath(), "tindustry-agentplay");

    // Methods that need the real game's window, input or clock.
    private static readonly HashSet<string> GameOnly = new(StringComparer.Ordinal)
    {
        "esegui", "riprendi", "cattura", "telecamera", "input", "ui", "log"
    };

    private readonly AgentGame game;
    private readonly bool keep;
    private readonly object gate = new();

    private SimEngine(AgentGame game, string folder, bool keep)
    {
        this.game = game;
        Folder = folder;
        this.keep = keep;
    }

    public string Kind => SimulationKind;

    public string Folder { get; }

    /// <summary>Makes the game of <paramref name="setup"/> (always through <see cref="AgentGameSetup.CreateHost"/>, so its journal replays).</summary>
    public static SimEngine Open(AgentGameSetup setup, GameData data, bool keep)
    {
        var host = setup.CreateHost(data.Content, data.Campaign, data.MapsDirectory);
        var folder = NewFolder();
        return new SimEngine(
            new AgentGame(host, data.Content, setup, data.Campaign, folder, tickPerComando: true), folder, keep);
    }

    /// <summary>Makes <paramref name="journal"/>'s game again and replays it: a new session with the same state.</summary>
    public static SimEngine Replay(AgentJournal journal, GameData data, bool keep)
    {
        var folder = NewFolder();
        try
        {
            return new SimEngine(AgentGame.Replay(journal, data.Content, data.Campaign, data.MapsDirectory, folder), folder, keep);
        }
        catch
        {
            Directory.Delete(folder, recursive: true);
            throw;
        }
    }

    public ulong Checksum()
    {
        lock (gate)
        {
            return game.Host.Checksum();
        }
    }

    public JsonElement Call(string method, JsonElement p)
    {
        if (GameOnly.Contains(method))
        {
            throw new AgentException(AgentCodes.MotoreNonDisponibile, $"'{method}' funziona solo nel gioco vero (apri_gioco).");
        }

        // MCP calls can arrive in parallel; one game runs one call at a time.
        lock (gate)
        {
            return method == "pausa"
                ? JsonSerializer.SerializeToElement(new { tick = game.Host.State.CurrentTick, inPausa = true })
                : game.Call(method, p);
        }
    }

    public void Close()
    {
        lock (gate)
        {
            game.Dispose();
            if (keep)
            {
                return;
            }

            try
            {
                Directory.Delete(Folder, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Cartella della sessione non cancellata: {Folder}: {ex.Message}");
            }
        }
    }

    private static string NewFolder() => Directory.CreateDirectory(Path.Combine(TempRoot, Path.GetRandomFileName())).FullName;
}
