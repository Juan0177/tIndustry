using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using TIndustry.Shared;

namespace TIndustry.AgentPlay;

/// <summary>
/// The real game: Godot started by <see cref="GameLauncher"/>, driven through its agents' channel (AgentBridge, JSON
/// lines on 127.0.0.1). One request at a time; a game that exits or drops the socket is <c>GiocoTerminato</c> with the
/// last lines of its log. Journals saved with <c>diario {nome}</c> get a header (motore gioco), so <c>rigioca</c> can
/// replay their commands on the simulation.
/// </summary>
public sealed class GameEngine : IAgentEngine
{
    public const string GameKind = "gioco";

    public static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan QuitWait = TimeSpan.FromSeconds(5);
    private const int LogLinesOnExit = 30;
    private static readonly JsonElement Empty = JsonDocument.Parse("{}").RootElement.Clone();

    private readonly GameProcess game;
    private readonly GameOptions options;
    private readonly object gate = new();
    private TcpClient? client;
    private StreamReader? reader;
    private StreamWriter? writer;
    private long nextId;
    private bool closed;
    private string? ended;

    private GameEngine(GameProcess game, GameOptions options)
    {
        this.game = game;
        this.options = options;
    }

    public string Kind => GameKind;

    /// <summary>The game's temporary profile (log, captures, journals, settings).</summary>
    public string Folder => game.ProfileDir;

    public int ProcessId => game.Process.Id;

    /// <summary>Starts the game, waits for its channel (<see cref="StartTimeout"/>) and starts the chosen game.</summary>
    public static GameEngine Open(GameOptions options)
    {
        var engine = new GameEngine(GameLauncher.Start(options), options);
        try
        {
            var deadline = DateTime.UtcNow + StartTimeout;
            engine.Connect(deadline);
            engine.Request("nuova_partita", JsonSerializer.SerializeToElement(
                new { misura = options.Misura, seme = options.Seme, livello = options.Livello }, AgentProtocol.JsonOptions),
                Remaining(deadline));
            return engine;
        }
        catch
        {
            engine.Close();
            throw;
        }
    }

    public JsonElement Call(string method, JsonElement p)
    {
        lock (gate)
        {
            if (closed)
            {
                throw new AgentException(AgentCodes.SessioneSconosciuta, "La sessione è stata chiusa.");
            }

            var timeout = method == "esegui" && p.TryGetProperty("secondi", out var s) && s.TryGetDouble(out var seconds)
                ? RequestTimeout + TimeSpan.FromSeconds(Math.Clamp(seconds, 0, 3600))
                : RequestTimeout;
            var result = Request(method, p, timeout);
            if (method == "diario" && result.TryGetProperty("percorso", out var path))
            {
                AddHeader(path.GetString()!);
            }

            return result;
        }
    }

    /// <summary>Asks the game to quit (<c>esci</c>), waits <see cref="QuitWait"/>, then kills it; deletes the profile unless kept.</summary>
    public void Close()
    {
        lock (gate)
        {
            if (closed)
            {
                return;
            }

            closed = true;
            var process = game.Process;
            if (ended is null && writer is not null && !process.HasExited)
            {
                try
                {
                    Request("esci", Empty, QuitWait);
                }
                catch (AgentException)
                {
                    // Not answering is fine: it is killed below.
                }
            }

            client?.Dispose();
            if (!process.WaitForExit(QuitWait))
            {
                Kill(process);
            }

            // Wrappers (godot4.cmd, the console exe) and the game itself: nothing may outlive the session.
            Kill(process);
            process.Dispose();
            if (!options.Conserva)
            {
                GameLauncher.DeleteProfile(game.ProfileDir);
            }
        }
    }

    // Connects (retrying while Godot starts), sends the secret and waits for the welcome.
    private void Connect(DateTime deadline)
    {
        while (true)
        {
            ThrowIfExited();
            var attempt = new TcpClient();
            try
            {
                if (attempt.ConnectAsync(IPAddress.Loopback, game.Port).Wait(TimeSpan.FromSeconds(1)) && attempt.Connected)
                {
                    client = attempt;
                    break;
                }
            }
            catch (AggregateException)
            {
                // Not listening yet.
            }

            attempt.Dispose();
            if (DateTime.UtcNow >= deadline)
            {
                throw Timeout($"Il gioco non ha aperto il canale entro {StartTimeout.TotalSeconds:0} s.");
            }

            Thread.Sleep(250);
        }

        var stream = client.GetStream();
        reader = new StreamReader(stream, new UTF8Encoding(false));
        writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
        Write(JsonSerializer.Serialize(new { token = game.Token }));
        var welcome = ReadResponse(0, Remaining(deadline));
        if (!welcome.Ok)
        {
            throw new AgentException(welcome.Code ?? AgentCodes.ErroreInterno, welcome.Message ?? "Canale rifiutato.");
        }
    }

    private JsonElement Request(string method, JsonElement p, TimeSpan timeout)
    {
        ThrowIfExited();
        var id = ++nextId;
        Write(AgentProtocol.WriteLine(new AgentRequest(id, method, p)));
        var response = ReadResponse(id, timeout);
        return response.Ok
            ? response.Result ?? Empty
            : throw new AgentException(response.Code ?? AgentCodes.ErroreInterno, response.Message ?? "");
    }

    private void Write(string line)
    {
        try
        {
            writer!.Write(line + "\n");
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
        {
            throw Ended("il canale si è chiuso");
        }
    }

    // The answer with this id (answers of earlier, timed-out requests are skipped).
    private AgentResponse ReadResponse(long id, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            string? line;
            using (var cancel = new CancellationTokenSource(Remaining(deadline)))
            {
                try
                {
                    line = reader!.ReadLineAsync(cancel.Token).AsTask().GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    ThrowIfExited();
                    throw Timeout($"Il gioco non ha risposto entro {timeout.TotalSeconds:0} s.");
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
                {
                    line = null;
                }
            }

            if (line is null)
            {
                // The socket closed: the game is gone or going; give it a moment to exit for its exit code.
                game.Process.WaitForExit(TimeSpan.FromSeconds(2));
                throw Ended("il canale si è chiuso");
            }

            var response = JsonSerializer.Deserialize<AgentResponse>(line, AgentProtocol.JsonOptions);
            if (response is not null && response.Id == id)
            {
                return response;
            }
        }
    }

    // A game journal as written by the bridge has no header: add how this game was made (sandbox or level).
    private void AddHeader(string path)
    {
        var bare = AgentJournal.Load(path);
        var journal = new AgentJournal
        {
            Setup = new AgentGameSetup(GameKind, options.Misura, options.Seme, MapGenerator.Name, new AgentRulesSetup(), options.Livello)
        };
        foreach (var entry in bare.Entries)
        {
            journal.Add(entry.Tick, entry.Method, entry.Params);
        }

        journal.Save(path);
    }

    private void ThrowIfExited()
    {
        if (ended is not null || game.Process.HasExited)
        {
            throw Ended("il processo è uscito");
        }
    }

    private AgentException Ended(string why)
    {
        ended ??= game.Process.HasExited ? $"{why} (codice di uscita {game.Process.ExitCode})" : why;
        return new AgentException(
            AgentCodes.GiocoTerminato,
            $"Il gioco è terminato: {ended}. Chiudi la sessione. Ultime righe del log:\n{GameLauncher.LogTail(game.ProfileDir, LogLinesOnExit)}");
    }

    private AgentException Timeout(string message) =>
        new(AgentCodes.Timeout, $"{message} Ultime righe del log:\n{GameLauncher.LogTail(game.ProfileDir, LogLinesOnExit)}");

    private static TimeSpan Remaining(DateTime deadline)
    {
        var left = deadline - DateTime.UtcNow;
        return left > TimeSpan.FromMilliseconds(1) ? left : TimeSpan.FromMilliseconds(1);
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // Already gone.
        }
    }
}
