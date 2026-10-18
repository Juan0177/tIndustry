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

    /// <summary>Marker file of a folder kept with <c>conserva</c>: the startup sweep leaves it alone.</summary>
    public const string KeepMarker = ".conserva";

    /// <summary>Folders left behind by a killed server are swept after this long.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(24);

    private readonly AgentGame game;
    private readonly bool keep;
    private readonly object gate = new();
    private bool closed;

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
        return InNewFolder(keep, folder => new AgentGame(host, data.Content, setup, data.Campaign, folder, tickPerComando: true));
    }

    /// <summary>Makes <paramref name="journal"/>'s game again and replays it: a new session with the same state.</summary>
    public static SimEngine Replay(AgentJournal journal, GameData data, bool keep) =>
        InNewFolder(keep, folder => AgentGame.Replay(journal, data.Content, data.Campaign, data.MapsDirectory, folder));

    /// <summary>
    /// Deletes session folders older than <see cref="StaleAfter"/> without <see cref="KeepMarker"/> (a server killed
    /// before it could close its sessions); folders still in use or locked are skipped.
    /// </summary>
    public static void SweepStale(DateTime nowUtc)
    {
        if (!Directory.Exists(TempRoot))
        {
            return;
        }

        foreach (var dir in new DirectoryInfo(TempRoot).EnumerateDirectories())
        {
            if (nowUtc - dir.LastWriteTimeUtc < StaleAfter || File.Exists(Path.Combine(dir.FullName, KeepMarker)))
            {
                continue;
            }

            try
            {
                dir.Delete(recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Cartella vecchia non cancellata: {dir.FullName}: {ex.Message}");
            }
        }
    }

    public ulong Checksum()
    {
        lock (gate)
        {
            ThrowIfClosed();
            return game.Host.Checksum();
        }
    }

    public JsonElement Call(string method, JsonElement p)
    {
        // MCP calls can arrive in parallel; one game runs one call at a time.
        lock (gate)
        {
            ThrowIfClosed();
            if (GameOnly.Contains(method))
            {
                throw new AgentException(AgentCodes.MotoreNonDisponibile, $"'{method}' funziona solo nel gioco vero (apri_gioco).");
            }

            return method == "pausa"
                ? JsonSerializer.SerializeToElement(new { tick = game.Host.State.CurrentTick, inPausa = true })
                : game.Call(method, p);
        }
    }

    public void Close()
    {
        lock (gate)
        {
            if (closed)
            {
                return;
            }

            closed = true;
            game.Dispose();
            if (!keep)
            {
                DeleteFolder(Folder);
            }
        }
    }

    // A call that got the engine just before chiudi must not run on the closed game.
    private void ThrowIfClosed()
    {
        if (closed)
        {
            throw new AgentException(AgentCodes.SessioneSconosciuta, "La sessione è stata chiusa.");
        }
    }

    private static SimEngine InNewFolder(bool keep, Func<string, AgentGame> create)
    {
        var folder = Directory.CreateDirectory(Path.Combine(TempRoot, Path.GetRandomFileName())).FullName;
        try
        {
            if (keep)
            {
                File.WriteAllText(Path.Combine(folder, KeepMarker), "");
            }

            return new SimEngine(create(folder), folder, keep);
        }
        catch
        {
            DeleteFolder(folder);
            throw;
        }
    }

    private static void DeleteFolder(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Cartella della sessione non cancellata: {folder}: {ex.Message}");
        }
    }
}
