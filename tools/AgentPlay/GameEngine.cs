using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TIndustry.Shared;

namespace TIndustry.AgentPlay;

/// <summary>
/// The real game: Godot started by <see cref="GameLauncher"/>, driven through its agents' channel (AgentBridge, JSON
/// lines on 127.0.0.1). Requests run in parallel like the bridge allows (a <c>pausa</c> during <c>esegui</c>); one
/// reader thread hands each answer to its request by id. A game that exits or drops the socket ends the session
/// (<see cref="Ended"/>: <c>GiocoTerminato</c> with the last lines of its log). Journals saved with <c>diario {nome}</c>
/// get a header (motore gioco) and go to <see cref="JournalFolder"/>, so <c>rigioca</c> replays their commands on the
/// simulation.
/// </summary>
public sealed class GameEngine : IAgentEngine
{
    public const string GameKind = "gioco";

    public static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan QuitWait = TimeSpan.FromSeconds(5);
    private const int LogLinesOnExit = 30;
    private const string PortRefused = "AgentBridge: porta";
    private static readonly JsonElement Empty = JsonDocument.Parse("{}").RootElement.Clone();

    private readonly GameProcess game;
    private readonly GameOptions options;
    private readonly string journals;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<AgentResponse>> waiting = new();
    private readonly object writeLock = new();
    // The profile's and the journals' lock files (SimEngine.LockFile): another server's sweep leaves them alone.
    private readonly List<FileStream> folderLocks = [];
    private TcpClient? client;
    private StreamReader? reader;
    private StreamWriter? writer;
    private long nextId;
    private int closed;
    private volatile string? ended;

    private GameEngine(GameProcess game, GameOptions options, string journals)
    {
        this.game = game;
        this.options = options;
        this.journals = journals;
    }

    public string Kind => GameKind;

    /// <summary>The game's temporary profile (log, captures, settings, the copied save).</summary>
    public string Folder => game.ProfileDir;

    /// <summary>Where <c>diario {nome}</c> saves (deleted by <c>chiudi</c> unless kept, like a simulation session's folder).</summary>
    public string JournalFolder => journals;

    public int ProcessId => game.Process.Id;

    /// <summary>The game is gone (exited, crashed, channel closed): the session should be closed.</summary>
    public bool Ended => ended is not null;

    /// <summary>Starts the game, waits for its channel (<see cref="StartTimeout"/>) and starts the chosen game.</summary>
    public static GameEngine Open(GameOptions options)
    {
        // The journals folder is created only after Start has refused a profile inside the player's real data.
        var journals = Path.GetFullPath(Path.Combine(SimEngine.TempRoot, Path.GetRandomFileName()));
        var engine = new GameEngine(GameLauncher.Start(options), options, journals);
        try
        {
            engine.folderLocks.Add(SimEngine.Lock(engine.Folder));
            Directory.CreateDirectory(journals);
            engine.folderLocks.Add(SimEngine.Lock(journals));
            if (options.Conserva)
            {
                File.WriteAllText(Path.Combine(journals, SimEngine.KeepMarker), "");
            }

            var deadline = DateTime.UtcNow + StartTimeout;
            engine.Connect(deadline);
            engine.Request("nuova_partita", JsonSerializer.SerializeToElement(new
            {
                misura = options.Misura,
                seme = options.Seme,
                generatore = options.Generatore,
                livello = options.Livello,
                slot = options.Salvataggio is null ? null : GameLauncher.SaveSlot
            }, AgentProtocol.JsonOptions), Remaining(deadline));
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
        var timeout = RequestTimeout;
        if (method == "esegui")
        {
            // The bridge refuses longer runs too: checked here so the wait never outlives the run.
            if (!(p.TryGetProperty("secondi", out var s) && s.TryGetDouble(out var seconds)
                    && seconds > 0 && seconds <= AgentProtocol.MaxRunSeconds))
            {
                throw new AgentException(AgentCodes.ParametriNonValidi, $"'secondi' tra 0 e {AgentProtocol.MaxRunSeconds:0}.");
            }

            timeout += TimeSpan.FromSeconds(seconds);
        }

        var result = Request(method, p, timeout);
        return method == "diario" && result.TryGetProperty("percorso", out var path) ? MoveJournal(result, path.GetString()!) : result;
    }

    /// <summary>
    /// Drops the channel first (a request still waiting fails at once, and the bridge quits the game when its
    /// launcher goes), waits <see cref="QuitWait"/> for the exit, then closes the job (kills what is left) and deletes
    /// the profile and the journals unless kept.
    /// </summary>
    public void Close()
    {
        if (Interlocked.Exchange(ref closed, 1) == 1)
        {
            return;
        }

        client?.Dispose();
        var process = game.Process;
        if (client is not null)
        {
            process.WaitForExit(QuitWait); // the bridge quits when its launcher's connection drops; unconnected, no one told it
        }

        game.Job.Dispose();
        if (!process.HasExited)
        {
            Kill(process); // no job object (not Windows) or a child outside it
        }

        process.Dispose();
        foreach (var folderLock in folderLocks)
        {
            folderLock.Dispose();
        }

        if (!options.Conserva)
        {
            GameLauncher.DeleteFolder(game.ProfileDir);
            GameLauncher.DeleteFolder(journals);
        }
    }

    // Connects (retrying while Godot starts), sends the secret and waits for the welcome; fails early when the game
    // exits or logs that the port is taken.
    private void Connect(DateTime deadline)
    {
        while (true)
        {
            ThrowIfStartFailed();
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
        writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        Write(JsonSerializer.Serialize(new { token = game.Token }));
        var welcome = reader.ReadLineAsync();
        while (!welcome.Wait(TimeSpan.FromMilliseconds(250)))
        {
            ThrowIfStartFailed();
            if (DateTime.UtcNow >= deadline)
            {
                throw Timeout("Il canale non ha risposto al segreto.");
            }
        }

        if (welcome.IsFaulted || welcome.Result is not { } line
            || Parse(line) is not { Ok: true, Result: { } result }
            || !result.TryGetProperty("canale", out var canale) || canale.GetString() != "tindustry")
        {
            // Another program on the port, or the game hung up.
            ThrowIfStartFailed();
            throw new AgentException(AgentCodes.MotoreNonDisponibile, "Sulla porta del gioco risponde qualcos'altro: canale rifiutato.");
        }

        new Thread(ReadLoop) { IsBackground = true, Name = "GameEngine " + game.Port }.Start();
    }

    // One reader for all requests: an answer goes to the request with its id; lines that do not parse, or whose
    // request already gave up (Timeout), are skipped. The end of the stream fails every request still waiting.
    private void ReadLoop()
    {
        try
        {
            while (reader!.ReadLine() is { } line)
            {
                if (Parse(line) is { } response && waiting.TryRemove(response.Id, out var request))
                {
                    request.TrySetResult(response);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
        {
        }

        if (closed == 0)
        {
            game.Process.WaitForExit(TimeSpan.FromSeconds(2)); // for the exit code
            MarkEnded("il canale si è chiuso");
        }

        foreach (var id in waiting.Keys)
        {
            if (waiting.TryRemove(id, out var request))
            {
                request.TrySetException(Gone());
            }
        }
    }

    private JsonElement Request(string method, JsonElement p, TimeSpan timeout)
    {
        if (closed == 1 || ended is not null)
        {
            throw Gone();
        }

        var id = Interlocked.Increment(ref nextId);
        var line = AgentProtocol.WriteLine(new AgentRequest(id, method, p));
        // The bridge hangs up on a longer line, and the game quits without its launcher: a bad parameter must not.
        if (line.Length > AgentProtocol.MaxLineChars)
        {
            throw new AgentException(
                AgentCodes.ParametriNonValidi, $"Parametri troppo lunghi: la richiesta supera {AgentProtocol.MaxLineChars / 1024} KiB.");
        }

        var answer = new TaskCompletionSource<AgentResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        waiting[id] = answer;
        try
        {
            Write(line);
        }
        catch (AgentException)
        {
            waiting.TryRemove(id, out _);
            throw;
        }

        if (Task.WaitAny([answer.Task], timeout) < 0)
        {
            waiting.TryRemove(id, out _);
            if (game.Process.HasExited)
            {
                MarkEnded("il processo è uscito");
                throw Gone();
            }

            throw Timeout($"Il gioco non ha risposto entro {timeout.TotalSeconds:0} s.");
        }

        var response = answer.Task.GetAwaiter().GetResult();
        return response.Ok
            ? response.Result ?? Empty
            : throw new AgentException(response.Code ?? AgentCodes.ErroreInterno, response.Message ?? "")
            {
                Missing = response.Missing is { ValueKind: JsonValueKind.Object } missing ? JsonNode.Parse(missing.GetRawText())!.AsObject() : null
            };
    }

    private void Write(string line)
    {
        try
        {
            lock (writeLock)
            {
                writer!.Write(line + "\n");
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException or NullReferenceException)
        {
            if (closed == 0)
            {
                MarkEnded("il canale si è chiuso");
            }

            throw Gone();
        }
    }

    // The bridge writes the journal with its header (the game it was made for: the launcher's setup, or the snapshot
    // of a game loaded later); move it to the server's journal folder (not the profile) and answer with that path. The
    // path comes from the socket: only a file inside this game's profile is read and deleted.
    private JsonElement MoveJournal(JsonElement result, string bridgePath)
    {
        if (!IsInside(game.ProfileDir, bridgePath))
        {
            throw new AgentException(AgentCodes.ErroreInterno, $"Il gioco ha indicato un diario fuori dal suo profilo: {bridgePath}.");
        }

        var journal = AgentJournal.Load(bridgePath);
        var path = Path.Combine(journals, Path.GetFileName(bridgePath));
        journal.Save(path);
        File.Delete(bridgePath);
        var moved = JsonNode.Parse(result.GetRawText())!.AsObject();
        moved["percorso"] = path;
        return JsonSerializer.SerializeToElement(moved);
    }

    /// <summary><paramref name="path"/> is a file inside <paramref name="folder"/> (not the folder itself, no "..").</summary>
    internal static bool IsInside(string folder, string path)
    {
        try
        {
            var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar;
            return Path.IsPathFullyQualified(path) && Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static AgentResponse? Parse(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<AgentResponse>(line, AgentProtocol.JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void ThrowIfStartFailed()
    {
        if (game.Process.HasExited)
        {
            MarkEnded("il processo è uscito");
            throw Gone();
        }

        if (GameLauncher.LogLines(game.ProfileDir).Any(l => l.Contains(PortRefused, StringComparison.Ordinal)))
        {
            throw new AgentException(
                AgentCodes.MotoreNonDisponibile,
                $"Il gioco non ha potuto aprire il canale. Ultime righe del log:\n{GameLauncher.LogTail(game.ProfileDir, LogLinesOnExit)}");
        }
    }

    private void MarkEnded(string why) =>
        ended ??= game.Process.HasExited ? $"{why} (codice di uscita {game.Process.ExitCode})" : why;

    // Closed by chiudi: the session is gone; ended on its own: the game's last log lines.
    private AgentException Gone() => ended is null
        ? new AgentException(AgentCodes.SessioneSconosciuta, "La sessione è stata chiusa.")
        : new AgentException(
            AgentCodes.GiocoTerminato,
            $"Il gioco è terminato: {ended}. La sessione è chiusa. Ultime righe del log:\n{GameLauncher.LogTail(game.ProfileDir, LogLinesOnExit)}");

    private AgentException Timeout(string message) =>
        new(AgentCodes.Timeout, $"{message} Ultime righe del log:\n{GameLauncher.LogTail(game.ProfileDir, LogLinesOnExit)}");

    private static TimeSpan Remaining(DateTime deadline)
    {
        var left = deadline - DateTime.UtcNow;
        return left > TimeSpan.FromMilliseconds(1) ? left : TimeSpan.FromMilliseconds(1);
    }

    internal static void Kill(Process process)
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
