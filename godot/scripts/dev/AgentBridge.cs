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
    private const double MaxRunSeconds = 120;
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

    private World _world = null!;
    private AgentGame? _game;
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
                    Serve(connection, reader, line);
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
                case "esci":
                    // The launcher's chiudi: answer first, the tree quits at the end of this frame.
                    Reply(connection, request.Id, new JsonObject { ["esce"] = true });
                    GetTree().Quit();
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
    /// The launcher's first request (tools/AgentPlay): leaves Home for a sandbox of <c>misura</c> and <c>seme</c>
    /// (biome generator) or for the campaign level <c>livello</c>.
    /// </summary>
    private JsonObject NewGame(JsonElement p)
    {
        var size = OptInt(p, "misura") ?? (int)MapSize.Media;
        if (size is < 12 or > Factory.MaxMapEdge)
        {
            throw Bad($"misura tra 12 e {Factory.MaxMapEdge}.");
        }

        var level = OptStr(p, "livello") is { } id
            ? _world.AgentCampaign?.Find(id) ?? throw Bad($"livello sconosciuto: {id}.")
            : null;
        if (_world.AgentStartGame(size, OptInt(p, "seme") ?? 1, level) is { } error)
        {
            throw Bad(error);
        }

        return new JsonObject { ["tick"] = CurrentTick() };
    }

    // --- Shared methods (AgentGame) -------------------------------------------------------------------------------

    /// <summary>The AgentGame of the running game, made again when the world attached another host.</summary>
    private AgentGame Game()
    {
        var host = _world.AgentHost ?? throw new AgentException(AgentCodes.GiocoTerminato, "Nessuna partita in corso.");
        if (_game?.Host != host)
        {
            _game?.Dispose();
            var diaries = Path.Combine(Profile(), "diari");
            Directory.CreateDirectory(diaries);
            _game = new AgentGame(
                host, host.State.Content, campaign: _world.AgentCampaign, journalDirectory: diaries, tickPerComando: false);
        }

        return _game;
    }

    private void CallGame(int connection, AgentRequest request)
    {
        var game = Game();
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
        var (connection, id, deadline) = _pending!.Value;
        if (_game is null || _game.Host != _world.AgentHost)
        {
            _pending = null;
            Send(connection, AgentResponse.Failure(
                id, new AgentException(AgentCodes.GiocoTerminato, "La partita è cambiata prima dell'esito del comando.")));
            return true;
        }

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
                    _game.Dispose();
                    _game = null; // drops the stuck call, so the next command is accepted
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
            throw Bad($"'secondi' tra 0 e {MaxRunSeconds}.");
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
    /// <c>ui {}</c>: the visible controls of the HUD, the open panels and the open popup windows, with a stable name
    /// (their path, unnamed nodes as type + index), type, shown text and rectangle in window pixels, and why a click
    /// would miss (<c>nascosto</c>); <c>ui {clic: nome}</c> clicks its centre, or refuses with <see cref="NonCliccabile"/>.
    /// </summary>
    private void Ui(int connection, long id, JsonElement p)
    {
        var controls = new List<(string Name, Control Control)>();
        Collect(_world.UiRoot, "", controls);
        foreach (var node in GetTree().Root.FindChildren("*", nameof(Window), recursive: true, owned: false))
        {
            if (node is Window { Visible: true } window)
            {
                Collect(window, window.Name.ToString(), controls);
            }
        }

        if (OptStr(p, "clic") is { } name)
        {
            var target = FindControl(controls, name);
            var at = WindowRect(target).GetCenter();
            if (ClickProblem(target, at) is { } problem)
            {
                throw new AgentException(NonCliccabile, $"'{name}': {problem}.");
            }

            Play(connection, id, _world.PlaytestClick(at), new JsonObject { ["clic"] = name, ["schermo"] = ScreenJson(at) });
            return;
        }

        var list = new JsonArray();
        foreach (var (path, control) in controls.Take(AgentGame.MaxListItems))
        {
            var rect = WindowRect(control);
            var entry = new JsonObject
            {
                ["nome"] = path,
                ["tipo"] = control.GetClass(),
                ["testo"] = ShownText(control),
                ["rett"] = new JsonObject
                {
                    ["x"] = Mathf.RoundToInt(rect.Position.X), ["y"] = Mathf.RoundToInt(rect.Position.Y),
                    ["larghezza"] = Mathf.RoundToInt(rect.Size.X), ["altezza"] = Mathf.RoundToInt(rect.Size.Y)
                }
            };
            if (!string.IsNullOrEmpty(control.TooltipText))
            {
                entry["suggerimento"] = Testi.T(control.TooltipText);
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

    private static void Collect(Node parent, string prefix, List<(string Name, Control Control)> into)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is not Control control || !control.IsVisibleInTree())
            {
                continue;
            }

            var name = control.Name.ToString();
            // Nodes added without a name get "@Button@123", which changes run to run.
            var segment = name.StartsWith('@') ? $"{control.GetClass()}{control.GetIndex()}" : name;
            var path = prefix.Length == 0 ? segment : $"{prefix}/{segment}";
            if (control is BaseButton or Label or RichTextLabel or LineEdit or TextEdit or global::Godot.Range
                || (control is PanelContainer or Panel && !name.StartsWith('@')))
            {
                into.Add((path, control));
            }

            Collect(control, path, into);
        }
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

    /// <summary>By full name, or by its last segment when only one visible control has it.</summary>
    private static Control FindControl(List<(string Name, Control Control)> controls, string name)
    {
        if (controls.FirstOrDefault(c => c.Name == name).Control is { } exact)
        {
            return exact;
        }

        var bySegment = controls.Where(c => c.Name.EndsWith("/" + name, StringComparison.Ordinal)).ToList();
        return bySegment.Count switch
        {
            1 => bySegment[0].Control,
            0 => throw Bad($"Nessun controllo visibile chiamato '{name}' (vedi 'ui')."),
            _ => throw Bad($"'{name}' indica {bySegment.Count} controlli: usa il nome intero.")
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

    private Vector2 ToWindow(Control control, Vector2 local)
    {
        var at = control.GetGlobalTransformWithCanvas() * local;
        var root = GetTree().Root;
        // An embedded popup draws at its position inside the viewport that holds it.
        for (var viewport = control.GetViewport(); viewport is Window window && window != root; viewport = window.GetParent()?.GetViewport())
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
