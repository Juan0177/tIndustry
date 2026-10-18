using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Control channel for AI agents (dev builds only, see <see cref="ShouldStart"/>): JSON lines on TCP 127.0.0.1. A
/// client first sends <c>{"token":"..."}</c> with the <see cref="TokenEnv"/> secret; a wrong token or an HTTP-looking
/// line closes the connection. A background thread reads the requests; they run here on the main thread in
/// <see cref="_Process"/>, one at a time. The shared methods go to <see cref="AgentGame"/> on the world's
/// <see cref="SimHost"/> (a command is answered once the frame loop's next tick resolves it); the game-only ones
/// (cattura, telecamera, input, ui, log, esegui, pausa, riprendi) act on the client, simulated input going through
/// <see cref="Input.ParseInputEvent"/> like a playtest.
/// </summary>
public partial class AgentBridge : Node
{
    /// <summary>Too many queued requests: the client is answered with this code and disconnected.</summary>
    public const string Occupato = nameof(Occupato);

    /// <summary><c>ui {clic}</c> on a control outside the window or clipped by its container.</summary>
    public const string NonCliccabile = nameof(NonCliccabile);

    private const int MaxRequestsPerFrame = 64;
    private const int MaxQueuedRequests = 1024;
    private const ulong CommandTimeoutMsec = 10_000;
    private const double MaxRunSeconds = AgentProtocol.MaxRunSeconds;
    private const int DefaultLogLines = 50;
    private const int MaxLogLines = 500;
    private const int KeptLogLines = 20_000;

    private readonly ConcurrentQueue<(int Connection, AgentRequest? Request, AgentException? Error)> _inbox = new();
    private readonly object _writeLock = new();
    private TcpListener? _listener;
    private TcpClient? _client;
    private StreamWriter? _writer;
    private int _connections;
    // The connected client's number, 0 when none: requests of any other number are dropped.
    private volatile int _connection;
    private Thread? _thread;
    private volatile bool _closing;
    private const int HandshakeTimeoutMs = 5000;

    private string _token = "";
    // Started by tools/AgentPlay: the game lives as long as the launcher's authenticated connection.
    private bool _quitWithoutClient;
    private volatile bool _clientLost;

    private World _world = null!;
    private AgentGame? _game;
    // The header nuova_partita gives the game it starts (null: a loaded slot, whose snapshot is taken).
    private AgentGameSetup? _launcherSetup;
    private bool _launcherStart;
    private bool _realInputNoted;
    private bool _realInputWarned;
    // The command call waiting for its tick, and a running `esegui` (other requests go on meanwhile).
    private (int Connection, long Id, ulong Deadline)? _pending;
    private (int Connection, long Id, ulong Deadline, long TickBefore, bool HeldAfter, double Seconds)? _run;
    // A multi-frame input (click, drag, key, wheel) is being played.
    private bool _busy;
    private int _shots;
    // This run's log, read incrementally: only the bytes written since the last `log`.
    private StreamReader? _logReader;
    private string _logPartial = "";
    private readonly List<string> _logLines = [];

    public override void _Ready()
    {
        _world = GetParent<World>();
        _token = OS.GetEnvironment(TokenEnv);
        _quitWithoutClient = OS.GetEnvironment(QuitWithoutClientEnv) == "1";
        TryParsePort(OS.GetEnvironment(PortEnv), out var port);
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
        }
        catch (SocketException ex)
        {
            GD.PushError($"AgentBridge: porta {port} non disponibile ({ex.Message}).");
            _listener = null;
            SetProcess(false);
            return;
        }

        _thread = new Thread(ReadLoop) { IsBackground = true, Name = "AgentBridge" };
        _thread.Start();
        GD.Print($"AgentBridge: in ascolto su 127.0.0.1:{port}");
    }

    /// <summary>Closes the socket, the reading thread and the log reader, so nothing is left for the engine teardown.</summary>
    public override void _ExitTree()
    {
        _closing = true;
        _listener?.Stop();
        _listener = null;
        lock (_writeLock)
        {
            _client?.Close();
            _client = null;
            _writer = null;
            _connection = 0;
        }

        _thread?.Join(2000);
        _thread = null;
        _game?.Dispose();
        _game = null;
        _logReader?.Dispose();
        _logReader = null;
    }

    /// <summary>Network thread: one client at a time; after the token, each line becomes a parsed request in the inbox.</summary>
    private void ReadLoop()
    {
        var line = new StringBuilder();
        while (!_closing)
        {
            TcpClient client;
            try
            {
                client = _listener!.AcceptTcpClient();
            }
            catch (Exception)
            {
                // Stopped by _ExitTree, or a failed accept: never let an exception end the process from this thread.
                if (_closing)
                {
                    return;
                }

                Thread.Sleep(100);
                continue;
            }

            client.NoDelay = true;
            // A client that stops reading must not stall the game's frame for long.
            client.SendTimeout = 2000;
            // A silent connection (port scan, browser preconnect) must not hold the only slot: the token comes fast.
            client.ReceiveTimeout = HandshakeTimeoutMs;
            var stream = client.GetStream();
            int connection;
            lock (_writeLock)
            {
                if (_closing)
                {
                    client.Close();
                    return;
                }

                _client = client;
                _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                connection = ++_connections;
                _connection = connection;
            }

            try
            {
                using var reader = new StreamReader(stream, new UTF8Encoding(false));
                if (ReadBoundedLine(reader, line, out _) is { } first && !LooksLikeHttp(first) && TokenMatches(first, _token))
                {
                    client.ReceiveTimeout = 0; // authenticated: an agent may stay quiet as long as it likes
                    Send(connection, AgentResponse.Success(0, JsonSerializer.SerializeToElement(new JsonObject { ["canale"] = "tindustry" })));
                    try
                    {
                        Serve(connection, reader, line);
                    }
                    finally
                    {
                        // The launcher (or the server behind it) is gone, however it died: so is the game.
                        _clientLost = _quitWithoutClient;
                    }
                }
            }
            catch (Exception)
            {
                // Client gone or bridge closing: the next client may connect.
            }

            lock (_writeLock)
            {
                if (_connection == connection)
                {
                    _client?.Close();
                    _client = null;
                    _writer = null;
                    _connection = 0; // its queued requests are dropped
                }
            }
        }
    }

    private void Serve(int connection, StreamReader reader, StringBuilder buffer)
    {
        while (!_closing)
        {
            var line = ReadBoundedLine(reader, buffer, out var tooLong);
            if (tooLong)
            {
                Send(connection, AgentResponse.Failure(
                    0, new AgentException(AgentCodes.ParametriNonValidi, $"Riga oltre {MaxLineChars / 1024} KiB: connessione chiusa.")));
                return;
            }

            if (line is null || LooksLikeHttp(line))
            {
                return; // end of stream, or a browser's cross-protocol request
            }

            if (line.Length == 0)
            {
                continue;
            }

            if (_inbox.Count >= MaxQueuedRequests)
            {
                Send(connection, AgentResponse.Failure(
                    0, new AgentException(Occupato, $"Più di {MaxQueuedRequests} richieste in coda: connessione chiusa.")));
                return;
            }

            try
            {
                _inbox.Enqueue((connection, AgentProtocol.ReadLine(line), null));
            }
            catch (AgentException ex)
            {
                _inbox.Enqueue((connection, null, ex));
            }
        }
    }

    /// <summary>One line without its end (null at end of stream), at most <see cref="MaxLineChars"/> characters.</summary>
    private static string? ReadBoundedLine(StreamReader reader, StringBuilder buffer, out bool tooLong)
    {
        buffer.Clear();
        tooLong = false;
        while (true)
        {
            var c = reader.Read();
            if (c < 0)
            {
                return buffer.Length > 0 ? buffer.ToString() : null;
            }

            if (c == '\n')
            {
                return buffer.ToString().TrimEnd('\r');
            }

            if (buffer.Length >= MaxLineChars)
            {
                tooLong = true;
                return null;
            }

            buffer.Append((char)c);
        }
    }

    public override void _Process(double delta)
    {
        if (_clientLost && !_closing)
        {
            GD.Print("AgentBridge: il lanciatore si è scollegato, il gioco esce.");
            _closing = true;
            GetTree().Quit();
            return;
        }

        if (_run is { } run && Time.GetTicksMsec() >= run.Deadline)
        {
            _run = null;
            World.PlaytestHeld = run.HeldAfter;
            Reply(run.Connection, run.Id, new JsonObject
            {
                ["eseguito"] = true, ["tickPrima"] = run.TickBefore, ["tick"] = CurrentTick(), ["secondi"] = run.Seconds,
                ["inPausa"] = run.HeldAfter
            });
        }

        SyncGame();
        if (_busy || (_pending is not null && !FinishPending()))
        {
            return;
        }

        for (var i = 0; i < MaxRequestsPerFrame && !_busy && _pending is null && _inbox.TryDequeue(out var item); i++)
        {
            if (item.Connection != _connection)
            {
                continue; // that client has gone: its requests no longer act on the game
            }

            if (item.Error is { } error)
            {
                Send(item.Connection, AgentResponse.Failure(0, error));
                continue;
            }

            Execute(item.Connection, item.Request!);
        }
    }

    private void Execute(int connection, AgentRequest request)
    {
        try
        {
            var p = request.Params;
            if (p.ValueKind != JsonValueKind.Object)
            {
                throw Bad("I parametri devono essere un oggetto.");
            }

            switch (request.Method)
            {
                case "pausa":
                case "riprendi":
                    SetHeld(request.Method == "pausa");
                    Reply(connection, request.Id, new JsonObject { ["tick"] = CurrentTick(), ["inPausa"] = World.PlaytestHeld });
                    break;
                case "esegui":
                    StartRun(connection, request.Id, p);
                    break;
                case "cattura":
                    Reply(connection, request.Id, Capture(p));
                    break;
                case "telecamera":
                    Reply(connection, request.Id, Camera(p));
                    break;
                case "input":
                    StartInput(connection, request.Id, p);
                    break;
                case "ui":
                    Ui(connection, request.Id, p);
                    break;
                case "log":
                    Reply(connection, request.Id, Log(p));
                    break;
                case "nuova_partita":
                    Reply(connection, request.Id, NewGame(p));
                    break;
                default:
                    CallGame(connection, request);
                    break;
            }
        }
        catch (AgentException ex)
        {
            Send(connection, AgentResponse.Failure(request.Id, ex));
        }
        catch (Exception ex)
        {
            GD.PushError($"AgentBridge {request.Method}: {ex}");
            Send(connection, AgentResponse.Failure(
                request.Id, new AgentException(AgentCodes.ErroreInterno, $"{ex.GetType().Name}: {ex.Message}")));
        }
    }

    /// <summary>
    /// The launcher's first request (tools/AgentPlay): leaves Home for a sandbox of <c>misura</c>, <c>seme</c> and
    /// <c>generatore</c> (made like <see cref="AgentGameSetup.CreateHost"/>, so a journal replays on the same game), for
    /// the campaign level <c>livello</c>, or for the save <c>slot</c> the launcher copied into the profile.
    /// </summary>
    private JsonObject NewGame(JsonElement p)
    {
        var level = OptStr(p, "livello") is { } id
            ? _world.AgentCampaign?.Find(id) ?? throw Bad($"livello sconosciuto: {id}.")
            : null;
        var sandbox = AgentGameSetup.Sandbox(
            OptInt(p, "misura") ?? (int)MapSize.Media, OptInt(p, "seme") ?? 1, OptStr(p, "generatore") ?? MapGenerator.Name,
            new GameRules()) with { Engine = AgentGameSetup.GameEngine };
        var slot = OptStr(p, "slot");
        // The journal's header: how the game is made (a loaded slot: its snapshot once loaded).
        _launcherSetup = slot is not null ? null : level is not null ? sandbox with { Level = level.Id } : sandbox;
        _launcherStart = true;
        try
        {
            if (_world.AgentStartGame(sandbox, level, slot) is { } error)
            {
                throw Bad(error);
            }

            SyncGame();
        }
        finally
        {
            _launcherSetup = null;
            _launcherStart = false;
        }
        return new JsonObject { ["tick"] = CurrentTick() };
    }

    // --- Shared methods (AgentGame) -------------------------------------------------------------------------------

    /// <summary>The AgentGame of the running game (<see cref="SyncGame"/>).</summary>
    private AgentGame Game()
    {
        SyncGame();
        return _game ?? throw new AgentException(AgentCodes.MotoreNonDisponibile, "Nessuna partita in corso.");
    }

    /// <summary>
    /// Every frame and before each call: when the world attached another host (new game, level, a loaded save, also
    /// from a person at the window) the journal starts again with that game's header, the launcher's setup for its own
    /// <c>nuova_partita</c>, else the snapshot of the game as it is now; <c>eventi</c> gets <c>partitaCaricata</c>
    /// and a command still waiting is answered <see cref="AgentCodes.PartitaCambiata"/>. When the world drops its game
    /// the AgentGame goes too.
    /// </summary>
    private void SyncGame()
    {
        var host = _world.AgentHost;
        if (_game?.Host == host)
        {
            return;
        }

        if (_pending is { } pending)
        {
            _pending = null;
            Send(pending.Connection, AgentResponse.Failure(pending.Id, new AgentException(
                AgentCodes.PartitaCambiata, "La partita è cambiata prima dell'esito del comando: il comando è perso.")));
        }

        if (host is null)
        {
            // The world dropped its game (a failed first load): commands answer "no game" instead of reaching it.
            _game?.Dispose();
            _game = null;
            return;
        }

        var previous = _game;
        StartJournal(host, _launcherSetup, warning: null);
        _launcherSetup = null;
        // The launcher's own first game is no news; any later change is (the agent's journal started again).
        if (previous is not null && !_launcherStart)
        {
            var before = previous.Host.State.CurrentTick;
            var now = host.State.CurrentTick;
            _game!.Note(new JsonObject
            {
                ["tick"] = now, ["tipo"] = "partitaCaricata", ["tickPrima"] = before, ["indietro"] = now < before
            });
        }
    }

    /// <summary>A new AgentGame (and journal) on <paramref name="host"/>, keeping the unread events of the old one.</summary>
    private void StartJournal(SimHost host, AgentGameSetup? setup, string? warning)
    {
        string? failure = null;
        if (setup is null)
        {
            try
            {
                setup = AgentGameSetup.Snapshot(AgentGameSetup.GameEngine, host);
            }
            catch (Exception ex)
            {
                // No header: diario says the journal cannot be replayed, rigioca refuses it.
                GD.PushError($"AgentBridge: istantanea della partita non riuscita: {ex}");
                failure = $"Istantanea della partita non riuscita ({ex.GetType().Name}): diario non rigiocabile.";
            }
        }

        var diaries = Path.Combine(Profile(), "diari");
        Directory.CreateDirectory(diaries);
        var game = new AgentGame(
            host, host.State.Content, setup, _world.AgentCampaign, diaries, tickPerComando: false)
        {
            ReadBrush = () => _world.AgentDirection,
            RotateBrush = _world.AgentRotate
        };
        foreach (var text in new[] { failure, warning })
        {
            if (text is not null)
            {
                game.Journal.Warnings.Add(text);
            }
        }

        if (_game is { } previous)
        {
            game.CarryEventsFrom(previous);
            previous.Dispose();
        }

        _game = game;
        _realInputWarned = false;
    }

    /// <summary>
    /// A person at the window (input that is not the bridge's or a playtest's): <c>eventi</c> says so once until the
    /// next <c>eventi</c>, and the journal warns once that the replay may differ.
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (@event is not (InputEventKey { Pressed: true } or InputEventMouseButton { Pressed: true })
            || World.IsSyntheticInput(@event) || _game is not { } game)
        {
            return;
        }

        var tick = game.Host.State.CurrentTick;
        if (!_realInputNoted)
        {
            _realInputNoted = true;
            game.Note(new JsonObject
            {
                ["tick"] = tick, ["tipo"] = "inputReale", ["input"] = @event is InputEventKey ? "tasto" : "mouse"
            });
        }

        if (!_realInputWarned)
        {
            _realInputWarned = true;
            game.Journal.Warnings.Add($"Tick {tick}: una persona ha usato la finestra del gioco; i suoi comandi non sono nel diario.");
        }
    }

    /// <summary><c>input</c> and <c>ui</c> clicks act on the game outside AgentGame: journaled, but not replayable.</summary>
    private void JournalOnly(string method, JsonElement p)
    {
        SyncGame();
        _game?.Journal.Add(CurrentTick(), method, p, replayable: false);
    }

    private void CallGame(int connection, AgentRequest request)
    {
        var game = Game();
        if (request.Method == "eventi")
        {
            _realInputNoted = false;
        }

        var response = game.Handle(request);
        if (!response.Ok && response.Code == AgentCodes.ErroreInterno)
        {
            GD.PushError($"AgentBridge {request.Method}: {game.LastInternalError}");
        }

        if (response.Ok && response.Result is { } result
            && result.TryGetProperty("inAttesa", out var waiting) && waiting.ValueKind == JsonValueKind.True)
        {
            _pending = (connection, request.Id, Time.GetTicksMsec() + CommandTimeoutMsec);
            return;
        }

        if (response.Ok && request.Method == "avanza")
        {
            // Exact ticks freeze real time, like pausa: the state stays put for the captures.
            SetHeld(true);
            var node = JsonNode.Parse(response.Result!.Value.GetRawText())!.AsObject();
            node["inPausa"] = true;
            response = response with { Result = JsonSerializer.SerializeToElement(node) };
        }

        Send(connection, response);
    }

    /// <summary>
    /// Answers the pending command once its tick has run (true); false while it waits. The frame loop runs the tick;
    /// when it does not (paused, behind Home) the bridge runs that one tick itself.
    /// </summary>
    private bool FinishPending()
    {
        SyncGame(); // a changed game answers the pending command PartitaCambiata
        if (_pending is not { } waiting || _game is null)
        {
            return true;
        }

        var (connection, id, deadline) = waiting;

        try
        {
            if (!_game.TryFinishPending(out var result))
            {
                if (_world.SimAdvances)
                {
                    if (Time.GetTicksMsec() < deadline)
                    {
                        return false;
                    }

                    _pending = null;
                    // Drops the stuck call, so the next command is accepted; the journal starts again from here.
                    var tick = _game.Host.State.CurrentTick;
                    StartJournal(_game.Host, null,
                        $"Tick {tick}: un comando non ha avuto esito entro 10 secondi (se si applica dopo, la riproduzione può differire); il diario riparte dall'istantanea di questo tick.");
                    Send(connection, AgentResponse.Failure(
                        id, new AgentException(AgentCodes.Timeout, "Il comando non ha avuto esito entro 10 secondi.")));
                    return true;
                }

                RunOneTick(_game.Host);
                if (!_game.TryFinishPending(out result))
                {
                    return false;
                }
            }

            _pending = null;
            Send(connection, AgentResponse.Success(id, result));
        }
        catch (AgentException ex)
        {
            _pending = null;
            Send(connection, AgentResponse.Failure(id, ex));
        }

        return true;
    }

    private static void RunOneTick(SimHost host)
    {
        try
        {
            host.AdvanceTicks(1);
        }
        catch (AggregateException ex)
        {
            foreach (var inner in ex.InnerExceptions)
            {
                GD.PushError($"SimHost handler: {inner}");
            }
        }
    }

    private long CurrentTick() => _world.AgentHost?.State.CurrentTick ?? 0;

    /// <summary>The session profile (always set: <see cref="ShouldStart"/> requires it).</summary>
    private static string Profile() =>
        GameDataPaths.Override ?? throw new AgentException(AgentCodes.MotoreNonDisponibile, "Nessun profilo di sessione.");

    // --- Time ----------------------------------------------------------------------------------------------------

    /// <summary>pausa, riprendi and avanza: at once, and the last explicit choice also holds after a running esegui.</summary>
    private void SetHeld(bool held)
    {
        World.PlaytestHeld = held;
        if (_run is { } run)
        {
            _run = run with { HeldAfter = held };
        }
    }

    /// <summary>
    /// <c>esegui {secondi}</c>: real time runs (pause lifted), the answer comes when the time has passed; with no game in
    /// play (Home, Nuova partita) it answers at once with <c>eseguito:false</c>.
    /// </summary>
    private void StartRun(int connection, long id, JsonElement p)
    {
        if (_run is not null)
        {
            throw Bad("'esegui' è già in corso.");
        }

        var seconds = Number(p, "secondi");
        if (!(seconds > 0 && seconds <= MaxRunSeconds))
        {
            throw Bad($"'secondi' più di 0 e al massimo {MaxRunSeconds:0}.");
        }

        if (!_world.GameInPlay)
        {
            Reply(connection, id, new JsonObject
            {
                ["eseguito"] = false, ["motivo"] = "Nessuna partita in gioco: è aperta la Home o Nuova partita.",
                ["tick"] = CurrentTick()
            });
            return;
        }

        _run = (connection, id, Time.GetTicksMsec() + (ulong)(seconds * 1000), CurrentTick(), World.PlaytestHeld, seconds);
        World.PlaytestHeld = false;
    }

    // --- Capture, camera, log ------------------------------------------------------------------------------------

    /// <summary><c>cattura {percorso?}</c>: PNG of the window, only under <c>&lt;profile&gt;/catture</c>.</summary>
    private JsonObject Capture(JsonElement p)
    {
        var path = CapturePath(Profile(), OptStr(p, "percorso"), _shots + 1)
            ?? throw Bad("'percorso' è un nome relativo .png dentro la cartella catture del profilo, senza '..'.");
        _shots++;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = _world.CaptureFrame();
        if (image.SavePng(path) is var error and not Error.Ok)
        {
            throw new AgentException(AgentCodes.ErroreInterno, $"Cattura non salvata ({error}).");
        }

        return new JsonObject { ["percorso"] = path, ["larghezza"] = image.GetWidth(), ["altezza"] = image.GetHeight() };
    }

    /// <summary><c>telecamera {cella?, zoom?}</c>: moves the camera there, answers where it is.</summary>
    private JsonObject Camera(JsonElement p)
    {
        var cam = _world.GetNodeOrNull<WorldCamera>("Camera")
            ?? throw new AgentException(AgentCodes.MotoreNonDisponibile, "Nessuna telecamera.");
        if (p.TryGetProperty("cella", out _))
        {
            cam.Position = World.CellCenter(Cell(p, "cella"));
        }

        if (p.TryGetProperty("zoom", out _))
        {
            var zoom = Number(p, "zoom");
            if (!(zoom > 0))
            {
                throw Bad("'zoom' deve essere maggiore di 0.");
            }

            var z = Mathf.Clamp((float)zoom, cam.MinZoom, cam.MaxZoom);
            cam.Zoom = new Vector2(z, z);
        }

        cam.ClampToMap();
        return new JsonObject
        {
            ["cella"] = new JsonObject
            {
                ["x"] = Mathf.FloorToInt(cam.Position.X / World.TileSize),
                ["y"] = Mathf.FloorToInt(cam.Position.Y / World.TileSize)
            },
            ["zoom"] = cam.Zoom.X
        };
    }

    /// <summary>
    /// <c>log {righe?, tutte?}</c>: the last lines of <c>&lt;profile&gt;/godot.log</c> (where the scripts point
    /// <c>--log-file</c>), warnings and errors only by default. The file stays open and only new bytes are read.
    /// </summary>
    private JsonObject Log(JsonElement p)
    {
        var count = OptInt(p, "righe") ?? DefaultLogLines;
        if (count is < 1 or > MaxLogLines)
        {
            throw Bad($"'righe' tra 1 e {MaxLogLines}.");
        }

        var path = Path.Combine(Profile(), "godot.log");
        try
        {
            if (_logReader is null)
            {
                if (!File.Exists(path))
                {
                    throw new AgentException(AgentCodes.MotoreNonDisponibile, $"Nessun log in {path} (serve --log-file).");
                }

                _logReader = new StreamReader(
                    new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite | FileShare.Delete));
            }

            var parts = (_logPartial + _logReader.ReadToEnd()).Split('\n');
            _logPartial = parts[^1];
            foreach (var part in parts[..^1])
            {
                _logLines.Add(part.TrimEnd('\r'));
            }
        }
        catch (IOException ex)
        {
            throw new AgentException(AgentCodes.MotoreNonDisponibile, $"Log non leggibile: {ex.Message}");
        }

        if (_logLines.Count > KeptLogLines)
        {
            _logLines.RemoveRange(0, _logLines.Count - KeptLogLines);
        }

        var all = p.TryGetProperty("tutte", out var t) && t.ValueKind == JsonValueKind.True;
        var lines = _logLines.Where(l => all ? l.Length > 0 : l.Contains("ERROR") || l.Contains("WARNING")).ToList();
        var lastLines = new JsonArray();
        foreach (var line in lines.Skip(Math.Max(0, lines.Count - count)))
        {
            lastLines.Add(line);
        }

        var result = new JsonObject { ["percorso"] = path, ["righe"] = lastLines };
        if (lines.Count > count)
        {
            result["troncato"] = lines.Count - count;
        }

        return result;
    }

    // --- Simulated input and UI ----------------------------------------------------------------------------------

    /// <summary><c>input {tipo, schermo | cella, verso?, tasto?, pulsante?}</c>: the real input path, over a few frames.</summary>
    private void StartInput(int connection, long id, JsonElement p)
    {
        var tipo = Str(p, "tipo");
        var button = OptStr(p, "pulsante") switch
        {
            null or "sinistro" => MouseButton.Left,
            "destro" => MouseButton.Right,
            _ => throw Bad("'pulsante' è sinistro o destro.")
        };
        var result = new JsonObject { ["tipo"] = tipo };
        Task action;
        switch (tipo)
        {
            case "clic":
            {
                var at = Point(p, result);
                action = _world.PlaytestClick(at, button);
                break;
            }
            case "trascina":
            {
                var from = Point(p, result);
                var to = DragEnd(p);
                result["fine"] = ScreenJson(to);
                action = _world.PlaytestDrag(button, from, to);
                break;
            }
            case "tasto":
            {
                var (key, modifiers, unicode) = ParseKey(Str(p, "tasto"));
                result["tasto"] = OS.GetKeycodeString(key | (Key)(long)modifiers);
                action = _world.PlaytestKey(key, modifiers, unicode);
                break;
            }
            case "rotella":
            {
                var wheel = OptStr(p, "verso") switch
                {
                    null or "su" => MouseButton.WheelUp,
                    "giu" or "giù" => MouseButton.WheelDown,
                    _ => throw Bad("'verso' della rotella è su o giu.")
                };
                var at = p.TryGetProperty("schermo", out _) || p.TryGetProperty("cella", out _)
                    ? Point(p, result)
                    : WindowCentre(result);
                action = Wheel(at, wheel);
                break;
            }
            default:
                throw Bad("'tipo' è clic, trascina, tasto o rotella.");
        }

        JournalOnly("input", p);
        Play(connection, id, action, result);
    }

    /// <summary>
    /// "Ctrl+Shift+S", "Escape", "a": the key, its modifiers as flags, and the character it types (printable keys
    /// without Ctrl, Alt or Meta; letters lower case unless Shift).
    /// </summary>
    private static (Key Key, KeyModifierMask Modifiers, long Unicode) ParseKey(string name)
    {
        var full = (long)OS.FindKeycodeFromString(name);
        var key = (Key)(full & (long)KeyModifierMask.CodeMask);
        var modifiers = (KeyModifierMask)(full & (long)KeyModifierMask.ModifierMask);
        if (modifiers.HasFlag(KeyModifierMask.MaskCmdOrCtrl) && !OS.HasFeature("macos"))
        {
            modifiers = (modifiers & ~KeyModifierMask.MaskCmdOrCtrl) | KeyModifierMask.MaskCtrl;
        }

        if (key == Key.None)
        {
            throw Bad($"Tasto sconosciuto: '{name}'.");
        }

        long unicode = 0;
        var code = (long)key;
        if (code is >= 0x20 and <= 0x7E
            && (modifiers & (KeyModifierMask.MaskCtrl | KeyModifierMask.MaskAlt | KeyModifierMask.MaskMeta)) == 0)
        {
            var c = (char)code;
            unicode = char.IsLetter(c) && !modifiers.HasFlag(KeyModifierMask.MaskShift) ? char.ToLowerInvariant(c) : c;
        }

        return (key, modifiers, unicode);
    }

    private void Play(int connection, long id, Task action, JsonObject result)
    {
        _busy = true;
        _ = PlayAsync(connection, id, action, result);
    }

    private async Task PlayAsync(int connection, long id, Task action, JsonObject result)
    {
        try
        {
            await action;
            Reply(connection, id, result);
        }
        catch (Exception ex)
        {
            GD.PushError($"AgentBridge input: {ex}");
            Send(connection, AgentResponse.Failure(
                id, new AgentException(AgentCodes.ErroreInterno, $"{ex.GetType().Name}: {ex.Message}")));
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task Wheel(Vector2 at, MouseButton wheel)
    {
        World.PlaytestMove(at, 0);
        foreach (var pressed in new[] { true, false })
        {
            Input.ParseInputEvent(new InputEventMouseButton
            {
                ButtonIndex = wheel, Pressed = pressed, Factor = 1f, Position = at, GlobalPosition = at,
                Device = World.PlaytestDevice
            });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    /// <summary>The window point of <c>schermo {x,y}</c> (window pixels) or <c>cella {x,y}</c> (its centre on screen).</summary>
    private Vector2 Point(JsonElement p, JsonObject result)
    {
        var hasScreen = p.TryGetProperty("schermo", out _);
        if (hasScreen == p.TryGetProperty("cella", out _))
        {
            throw Bad("Serve 'schermo' oppure 'cella' (uno dei due).");
        }

        Vector2 at;
        if (hasScreen)
        {
            var (x, y) = Pair(p, "schermo");
            at = new Vector2(x, y);
        }
        else
        {
            var cell = Cell(p, "cella");
            at = _world.CellToScreen(cell);
            result["cella"] = new JsonObject { ["x"] = cell.X, ["y"] = cell.Y };
        }

        if (!InWindow(at))
        {
            throw Bad($"Il punto ({at.X:0},{at.Y:0}) è fuori dalla finestra {DisplayServer.WindowGetSize()}.");
        }

        result["schermo"] = ScreenJson(at);
        return at;
    }

    private static bool InWindow(Vector2 at)
    {
        var size = DisplayServer.WindowGetSize();
        return at.X >= 0 && at.Y >= 0 && at.X < size.X && at.Y < size.Y;
    }

    /// <summary>End of a drag: <c>verso</c> as <c>{x,y}</c> or "x,y", in the same space as the start (screen or cell).</summary>
    private Vector2 DragEnd(JsonElement p)
    {
        if (!p.TryGetProperty("verso", out var end))
        {
            throw Bad("'trascina' vuole 'verso': la fine, {x,y} o \"x,y\" come il punto di partenza.");
        }

        int x, y;
        if (end.ValueKind == JsonValueKind.Object)
        {
            (x, y) = Pair(p, "verso");
        }
        else if (end.ValueKind == JsonValueKind.String && end.GetString()!.Split(',') is [var a, var b]
                 && int.TryParse(a.Trim(), out x) && int.TryParse(b.Trim(), out y))
        {
        }
        else
        {
            throw Bad("'verso' è {x,y} o \"x,y\".");
        }

        if (p.TryGetProperty("schermo", out _))
        {
            return new Vector2(x, y);
        }

        if (_world.AgentHost?.State.Terrain is { } map && (x < 0 || y < 0 || x >= map.Width || y >= map.Height))
        {
            throw Bad($"La cella ({x},{y}) è fuori dalla mappa.");
        }

        return _world.CellToScreen(new GridPosition(x, y));
    }

    private static Vector2 WindowCentre(JsonObject result)
    {
        var at = (Vector2)DisplayServer.WindowGetSize() / 2;
        result["schermo"] = ScreenJson(at);
        return at;
    }

    private static JsonObject ScreenJson(Vector2 at) => new() { ["x"] = Mathf.RoundToInt(at.X), ["y"] = Mathf.RoundToInt(at.Y) };

    /// <summary>
    /// <c>ui {}</c>: the controls of the HUD, the open panels and the open popup windows a click can reach, with a
    /// stable name (their path, unnamed nodes as type + index), type, shown text, tooltip and rectangle in window pixels.
    /// Left out: transparent controls (a faded toast), controls under another one that takes the mouse (the Home's
    /// shade over the HUD) or under an open popup window, and copies of one already listed (same type, text and
    /// rectangle). A control clipped by its container says why a click would miss (<c>nascosto</c>). <c>ui {clic}</c>
    /// takes the name, the last part of it, the shown text or the tooltip, and clicks its centre; more than one match is
    /// refused as ambiguous.
    /// </summary>
    private void Ui(int connection, long id, JsonElement p)
    {
        var controls = new List<UiControl>();
        var all = new List<Control>();
        var windows = new List<(Window Window, int Start)>();
        Collect(_world.UiRoot, "", 1f, controls, all);
        foreach (var node in GetTree().Root.FindChildren("*", nameof(Window), recursive: true, owned: false))
        {
            if (node is Window { Visible: true } window)
            {
                // A tooltip lets the mouse through: it hides nothing.
                if (!window.GetFlag(Window.Flags.MousePassthrough))
                {
                    windows.Add((window, all.Count));
                }

                Collect(window, window.Name.ToString(), 1f, controls, all);
            }
        }

        controls = Reachable(controls, all, windows);
        if (OptStr(p, "clic") is { } name)
        {
            var target = FindControl(controls, name);
            var at = WindowRect(target.Control).GetCenter();
            if (ClickProblem(target.Control, at) is { } problem)
            {
                throw new AgentException(NonCliccabile, $"'{name}': {problem}.");
            }

            JournalOnly("ui", p);
            Play(connection, id, _world.PlaytestClick(at),
                new JsonObject { ["clic"] = target.Name, ["schermo"] = ScreenJson(at) });
            return;
        }

        var list = new JsonArray();
        foreach (var item in controls.Take(AgentGame.MaxListItems))
        {
            var control = item.Control;
            var rect = item.Rect;
            var entry = new JsonObject
            {
                ["nome"] = item.Name,
                ["tipo"] = control.GetClass(),
                ["testo"] = item.Text,
                ["rett"] = new JsonObject
                {
                    ["x"] = Mathf.RoundToInt(rect.Position.X), ["y"] = Mathf.RoundToInt(rect.Position.Y),
                    ["larghezza"] = Mathf.RoundToInt(rect.Size.X), ["altezza"] = Mathf.RoundToInt(rect.Size.Y)
                }
            };
            if (item.Tooltip.Length > 0)
            {
                entry["suggerimento"] = item.Tooltip;
            }

            if (control is BaseButton { Disabled: true })
            {
                entry["disattivo"] = true;
            }

            if (ClickProblem(control, rect.GetCenter()) is { } problem)
            {
                entry["nascosto"] = problem;
            }

            list.Add(entry);
        }

        var result = new JsonObject { ["controlli"] = list };
        if (controls.Count > AgentGame.MaxListItems)
        {
            result["troncato"] = controls.Count - AgentGame.MaxListItems;
        }

        Reply(connection, id, result);
    }

    /// <summary>A listed control: its stable name, rectangle in window pixels, shown text and translated tooltip.</summary>
    private sealed record UiControl(string Name, Control Control, Rect2 Rect, string Text, string Tooltip);

    /// <summary>Below this opacity (own and inherited modulate) a control counts as not shown.</summary>
    private const float MinShownAlpha = 0.05f;

    /// <summary>
    /// Every visible control under <paramref name="parent"/> in drawing order (<paramref name="all"/>), and the ones worth
    /// listing (<paramref name="into"/>): buttons, texts, inputs, named panels and the clickable slots with a tooltip
    /// (the build palette), when not transparent.
    /// </summary>
    private void Collect(Node parent, string prefix, float alpha, List<UiControl> into, List<Control> all)
    {
        foreach (var child in parent.GetChildren())
        {
            // A node queued for deletion is still in the tree until the frame ends (a rebuilt panel's old row).
            if (child is not Control control || !control.IsVisibleInTree() || control.IsQueuedForDeletion())
            {
                continue;
            }

            all.Add(control);
            var inherited = alpha * control.Modulate.A;
            var name = control.Name.ToString();
            // Nodes added without a name get "@Button@123", which changes run to run.
            var segment = name.StartsWith('@') ? $"{control.GetClass()}{control.GetIndex()}" : name;
            var path = prefix.Length == 0 ? segment : $"{prefix}/{segment}";
            var clickableSlot = control.MouseFilter == Control.MouseFilterEnum.Stop && control.TooltipText.Length > 0;
            if ((control is BaseButton or Label or RichTextLabel or LineEdit or TextEdit or global::Godot.Range
                    || (control is PanelContainer or Panel && !name.StartsWith('@')) || clickableSlot)
                && inherited * control.SelfModulate.A >= MinShownAlpha)
            {
                var tooltip = control.TooltipText.Length > 0 ? Testi.T(control.TooltipText) : "";
                into.Add(new UiControl(path, control, WindowRect(control), ShownText(control), tooltip));
            }

            Collect(control, path, inherited, into, all);
        }
    }

    /// <summary>
    /// Drops the copies (same type, text and rectangle) and the controls whose centre lies under a later control
    /// (drawn above) that takes the mouse and is not inside them, as Godot's own picking does, and the ones under a popup
    /// window opened after them (<paramref name="windows"/>: each window and where its controls start in
    /// <paramref name="all"/>) that does not hold them.
    /// </summary>
    private List<UiControl> Reachable(List<UiControl> controls, List<Control> all, List<(Window Window, int Start)> windows)
    {
        var order = new Dictionary<Control, int>();
        for (var i = 0; i < all.Count; i++)
        {
            order[all[i]] = i;
        }

        var blockers = all.Where(c => c.MouseFilter != Control.MouseFilterEnum.Ignore)
            .Select(c => (Control: c, Rect: WindowRect(c), Order: order[c])).ToList();
        var popups = windows.Select(w => (w.Window, Rect: WindowRect(w.Window), w.Start)).ToList();
        var seen = new HashSet<(string, string, Rect2I)>();
        var kept = new List<UiControl>();
        foreach (var item in controls)
        {
            var centre = item.Rect.GetCenter();
            var mine = order[item.Control];
            var covered = blockers.Any(b => b.Order > mine && b.Rect.HasPoint(centre)
                && !item.Control.IsAncestorOf(b.Control) && b.Control.GetViewport() == item.Control.GetViewport())
                || popups.Any(w => mine < w.Start && w.Rect.HasPoint(centre) && !w.Window.IsAncestorOf(item.Control));
            var rounded = new Rect2I((Vector2I)item.Rect.Position.Round(), (Vector2I)item.Rect.Size.Round());
            if (!covered && seen.Add((item.Control.GetClass(), item.Text, rounded)))
            {
                kept.Add(item);
            }
        }

        return kept;
    }

    /// <summary>Why a click at <paramref name="at"/> would not reach <paramref name="control"/>, or null.</summary>
    private string? ClickProblem(Control control, Vector2 at)
    {
        if (!InWindow(at))
        {
            return "il centro è fuori dalla finestra";
        }

        for (var node = control.GetParent(); node is not null and not Window; node = node.GetParent())
        {
            if (node is Control { ClipContents: true } clip && !WindowRect(clip).HasPoint(at))
            {
                return $"il centro è tagliato da {clip.GetClass()} '{clip.Name}' (scorri prima)";
            }
        }

        return null;
    }

    /// <summary>
    /// By full name; else by its last segment, its shown text or its tooltip (without the "[1]" or "(T)" hotkey),
    /// ignoring case, preferring the clickable ones (buttons and slots); more than one match is ambiguous.
    /// </summary>
    private static UiControl FindControl(List<UiControl> controls, string name)
    {
        if (controls.FirstOrDefault(c => c.Name == name) is { } exact)
        {
            return exact;
        }

        static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        static string WithoutHotkey(string tooltip) =>
            tooltip.LastIndexOfAny(['[', '(']) is var cut and > 0 ? tooltip[..cut] : tooltip;
        var matches = controls.Where(c => c.Name.EndsWith("/" + name, StringComparison.Ordinal)
            || Same(c.Text, name) || (c.Tooltip.Length > 0 && (Same(c.Tooltip, name) || Same(WithoutHotkey(c.Tooltip), name))))
            .ToList();
        var clickable = matches.Where(c => c.Control is BaseButton || c.Control.MouseFilter == Control.MouseFilterEnum.Stop).ToList();
        var found = clickable.Count > 0 ? clickable : matches;
        return found.Count switch
        {
            1 => found[0],
            0 => throw Bad($"Nessun controllo visibile chiamato '{name}' (nome, testo o suggerimento: vedi 'ui')."),
            _ => throw Bad($"'{name}' è ambiguo: indica {found.Count} controlli ({string.Join(", ", found.Take(5).Select(c => c.Name))}); usa il nome intero.")
        };
    }

    private static string ShownText(Control control)
    {
        var text = control switch
        {
            Button b => b.Text,
            LinkButton l => l.Text,
            Label l => l.Text,
            RichTextLabel r => r.GetParsedText(),
            LineEdit e => e.Text,
            TextEdit e => e.Text,
            global::Godot.Range r => r.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => ""
        };
        // Texts are set as keys of testi.csv and shown translated (auto-translate).
        return text.Length == 0 ? text : Testi.T(text);
    }

    /// <summary>The control's rectangle in window pixels (what a real mouse event carries), also inside a popup window.</summary>
    private Rect2 WindowRect(Control control)
    {
        var a = ToWindow(control, Vector2.Zero);
        var b = ToWindow(control, control.Size);
        return new Rect2(a, b - a).Abs();
    }

    /// <summary>An embedded window's rectangle in window pixels.</summary>
    private Rect2 WindowRect(Window window)
    {
        var holder = window.GetParent()?.GetViewport();
        var a = ToRoot(holder, window.Position);
        var b = ToRoot(holder, window.Position + window.Size);
        return new Rect2(a, b - a).Abs();
    }

    private Vector2 ToWindow(Control control, Vector2 local) =>
        ToRoot(control.GetViewport(), control.GetGlobalTransformWithCanvas() * local);

    /// <summary>A point of <paramref name="viewport"/>'s canvas in window pixels.</summary>
    private Vector2 ToRoot(Viewport? viewport, Vector2 at)
    {
        var root = GetTree().Root;
        // An embedded popup draws at its position inside the viewport that holds it.
        for (; viewport is Window window && window != root; viewport = window.GetParent()?.GetViewport())
        {
            at = window.GetFinalTransform() * at + (Vector2)window.Position;
        }

        return root.GetFinalTransform() * at;
    }

    // --- Replies and parameters ----------------------------------------------------------------------------------

    private void Reply(int connection, long id, JsonObject result) =>
        Send(connection, AgentResponse.Success(id, JsonSerializer.SerializeToElement(result)));

    /// <summary>Writes one answer line to <paramref name="connection"/> if it is still the connected client (any thread).</summary>
    private void Send(int connection, AgentResponse response)
    {
        var line = AgentProtocol.WriteLine(response) + "\n";
        lock (_writeLock)
        {
            if (connection != _connection || _writer is null)
            {
                return;
            }

            try
            {
                _writer.Write(line);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                _client?.Close();
            }
        }
    }

    private static AgentException Bad(string message) => new(AgentCodes.ParametriNonValidi, message);

    private static string Str(JsonElement p, string name) =>
        OptStr(p, name) ?? throw Bad($"manca '{name}' (testo).");

    private static string? OptStr(JsonElement p, string name)
    {
        if (!p.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return v.ValueKind == JsonValueKind.String ? v.GetString() : throw Bad($"'{name}' deve essere un testo.");
    }

    private static int? OptInt(JsonElement p, string name)
    {
        if (!p.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : throw Bad($"'{name}' deve essere un numero intero.");
    }

    private static double Number(JsonElement p, string name) =>
        p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDouble()
            : throw Bad($"manca '{name}' (numero).");

    private static (int X, int Y) Pair(JsonElement p, string name)
    {
        if (!p.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Object)
        {
            throw Bad($"manca '{name}' {{x, y}}.");
        }

        return (OptInt(v, "x") ?? throw Bad($"manca '{name}.x'."), OptInt(v, "y") ?? throw Bad($"manca '{name}.y'."));
    }

    private GridPosition Cell(JsonElement p, string name)
    {
        var (x, y) = Pair(p, name);
        if (_world.AgentHost?.State.Terrain is { } map && (x < 0 || y < 0 || x >= map.Width || y >= map.Height))
        {
            throw Bad($"La cella ({x},{y}) è fuori dalla mappa.");
        }

        return new GridPosition(x, y);
    }
}
