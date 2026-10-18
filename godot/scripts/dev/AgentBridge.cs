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
/// background thread reads the requests; they run here on the main thread in <see cref="_Process"/>, one at a time.
/// The shared methods go to <see cref="AgentGame"/> on the world's <see cref="SimHost"/> (a command is answered once the
/// frame loop's next tick resolves it); the game-only ones (cattura, telecamera, input, ui, log, esegui, pausa,
/// riprendi) act on the client, simulated input going through <see cref="Input.ParseInputEvent"/> like a playtest.
/// </summary>
public partial class AgentBridge : Node
{
    private const int MaxRequestsPerFrame = 64;
    private const ulong CommandTimeoutMsec = 10_000;
    private const double MaxRunSeconds = 120;
    private const int DefaultLogLines = 50;
    private const int MaxLogLines = 500;

    private readonly ConcurrentQueue<(int Connection, AgentRequest? Request, AgentException? Error)> _inbox = new();
    private readonly object _writeLock = new();
    private TcpListener? _listener;
    private TcpClient? _client;
    private StreamWriter? _writer;
    private int _connection;
    private Thread? _thread;
    private volatile bool _closing;

    private World _world = null!;
    private AgentGame? _game;
    // The command call waiting for its tick, and a running `esegui` (other requests go on meanwhile).
    private (int Connection, long Id, ulong Deadline)? _pending;
    private (int Connection, long Id, ulong Deadline, long TickBefore, bool WasHeld, double Seconds)? _run;
    // A multi-frame input (click, drag, key, wheel) is being played.
    private bool _busy;
    private int _shots;

    public override void _Ready()
    {
        _world = GetParent<World>();
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

    /// <summary>Closes the socket and the reading thread, so nothing is left for the engine teardown.</summary>
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
        }

        _thread?.Join(2000);
        _thread = null;
        _game?.Dispose();
        _game = null;
    }

    /// <summary>Network thread: one client at a time; each line becomes a parsed request in the inbox.</summary>
    private void ReadLoop()
    {
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
            var stream = client.GetStream();
            int connection;
            lock (_writeLock)
            {
                _client = client;
                _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                connection = ++_connection;
            }

            try
            {
                using var reader = new StreamReader(stream, new UTF8Encoding(false));
                while (!_closing && reader.ReadLine() is { } line)
                {
                    if (line.Length == 0)
                    {
                        continue;
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
                }
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_run is { } run && Time.GetTicksMsec() >= run.Deadline)
        {
            _run = null;
            World.PlaytestHeld = run.WasHeld;
            Reply(run.Connection, run.Id, new JsonObject
            {
                ["tickPrima"] = run.TickBefore, ["tick"] = CurrentTick(), ["secondi"] = run.Seconds
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
                    World.PlaytestHeld = request.Method == "pausa";
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

    // --- Shared methods (AgentGame) -------------------------------------------------------------------------------

    /// <summary>The AgentGame of the running game, made again when the world attached another host.</summary>
    private AgentGame Game()
    {
        var host = _world.AgentHost ?? throw new AgentException(AgentCodes.GiocoTerminato, "Nessuna partita in corso.");
        if (_game?.Host != host)
        {
            _game?.Dispose();
            var diaries = GameDataPaths.Override is { } profile ? Path.Combine(profile, "diari") : null;
            if (diaries is not null)
            {
                Directory.CreateDirectory(diaries);
            }

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
            World.PlaytestHeld = true;
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
                if (_world.AgentSimRunning)
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

    // --- Time ----------------------------------------------------------------------------------------------------

    /// <summary><c>esegui {secondi}</c>: real time runs (pause lifted), the answer comes when the time has passed.</summary>
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

        _run = (connection, id, Time.GetTicksMsec() + (ulong)(seconds * 1000), CurrentTick(), World.PlaytestHeld, seconds);
        World.PlaytestHeld = false;
    }

    // --- Capture, camera, log ------------------------------------------------------------------------------------

    /// <summary><c>cattura {percorso?}</c>: PNG of the window inside the session profile (or the temp folder).</summary>
    private JsonObject Capture(JsonElement p)
    {
        var profile = GameDataPaths.Override;
        var temp = Path.GetFullPath(Path.GetTempPath());
        var folder = Path.Combine(profile ?? Path.Combine(temp, "tindustry-catture"), "catture");
        var path = OptStr(p, "percorso") is { } wanted
            ? Path.GetFullPath(Path.Combine(folder, wanted))
            : Path.Combine(folder, $"cattura-{++_shots:000}.png");
        if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            throw Bad("'percorso' deve finire con .png.");
        }

        if (!Inside(path, profile) && !Inside(path, temp))
        {
            throw Bad("'percorso' deve stare nel profilo della sessione o nella cartella temporanea.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var image = _world.CaptureFrame();
        if (image.SavePng(path) is var error and not Error.Ok)
        {
            throw new AgentException(AgentCodes.ErroreInterno, $"Cattura non salvata ({error}).");
        }

        return new JsonObject { ["percorso"] = path, ["larghezza"] = image.GetWidth(), ["altezza"] = image.GetHeight() };
    }

    private static bool Inside(string path, string? folder) =>
        folder is not null && path.StartsWith(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

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

    /// <summary><c>log {righe?, tutte?}</c>: the last lines of this run's Godot log, warnings and errors only by default.</summary>
    private static JsonObject Log(JsonElement p)
    {
        var count = OptInt(p, "righe") ?? DefaultLogLines;
        if (count is < 1 or > MaxLogLines)
        {
            throw Bad($"'righe' tra 1 e {MaxLogLines}.");
        }

        var path = LogPath()
            ?? throw new AgentException(AgentCodes.MotoreNonDisponibile, "Questa partita non scrive un file di log.");
        string text;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            text = reader.ReadToEnd();
        }
        catch (IOException ex)
        {
            throw new AgentException(AgentCodes.MotoreNonDisponibile, $"Log non leggibile: {ex.Message}");
        }

        var all = p.TryGetProperty("tutte", out var t) && t.ValueKind == JsonValueKind.True;
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r'))
            .Where(l => all ? l.Length > 0 : l.Contains("ERROR") || l.Contains("WARNING")).ToList();
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

    /// <summary>
    /// This run's log: <c>&lt;profile&gt;/godot.log</c>, where the scripts point <c>--log-file</c> (Godot keeps its own
    /// arguments out of <see cref="OS.GetCmdlineArgs"/>), else Godot's own file log when enabled.
    /// </summary>
    private static string? LogPath()
    {
        if (GameDataPaths.Override is { } profile && File.Exists(Path.Combine(profile, "godot.log")))
        {
            return Path.Combine(profile, "godot.log");
        }

        return ProjectSettings.GetSetting("debug/file_logging/enable_file_logging").AsBool()
            ? ProjectSettings.GlobalizePath(ProjectSettings.GetSetting("debug/file_logging/log_path").AsString())
            : null;
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
                var name = Str(p, "tasto");
                var key = OS.FindKeycodeFromString(name);
                if (key == Key.None)
                {
                    throw Bad($"Tasto sconosciuto: '{name}'.");
                }

                result["tasto"] = OS.GetKeycodeString(key);
                action = _world.PlaytestKey(key);
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

        var size = DisplayServer.WindowGetSize();
        if (at.X < 0 || at.Y < 0 || at.X >= size.X || at.Y >= size.Y)
        {
            throw Bad($"Il punto ({at.X:0},{at.Y:0}) è fuori dalla finestra {size.X}×{size.Y}.");
        }

        result["schermo"] = ScreenJson(at);
        return at;
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
    /// <c>ui {}</c>: the visible controls of the HUD and the open panels with a stable name (their path, unnamed nodes
    /// as type + index), type, shown text and rectangle in window pixels; <c>ui {clic: nome}</c> clicks its centre.
    /// </summary>
    private void Ui(int connection, long id, JsonElement p)
    {
        var controls = new List<(string Name, Control Control)>();
        Collect(_world.UiRoot, "", controls);
        if (OptStr(p, "clic") is { } name)
        {
            var target = FindControl(controls, name);
            var rect = WindowRect(target);
            var at = rect.GetCenter();
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

    /// <summary>The control's rectangle in window pixels (what a real mouse event carries).</summary>
    private Rect2 WindowRect(Control control)
    {
        var xf = GetViewport().GetFinalTransform() * control.GetGlobalTransformWithCanvas();
        var a = xf * Vector2.Zero;
        var b = xf * control.Size;
        return new Rect2(a, b - a).Abs();
    }

    // --- Replies and parameters ----------------------------------------------------------------------------------

    private void Reply(int connection, long id, JsonObject result) =>
        Send(connection, AgentResponse.Success(id, JsonSerializer.SerializeToElement(result)));

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
