using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Playtest <c>agenti</c>: the game runs with its control channel (<see cref="AgentBridge"/>, TINDUSTRY_AGENT_PORT set
/// by playtest.sh) and a TCP client inside the playtest plays through it like an agent: stato, piazza of a miner,
/// avanza 60 (frozen afterwards), a HUD button found with ui and clicked by input and by name, keys, wheel, camera,
/// cattura of the window, log, and a burst of 50 stato while esegui 2 runs.
/// </summary>
public partial class World
{
    private const ulong AgentReplyTimeoutMsec = 15_000;

    private async Task<bool> PlaytestAgentiAsync(string dir)
    {
        if (GetNodeOrNull<AgentBridge>("AgentBridge") is null
            || !AgentBridge.TryParsePort(OS.GetEnvironment(AgentBridge.PortEnv), out var port))
        {
            return Fail($"canale degli agenti assente: serve {AgentBridge.PortEnv} in una build di sviluppo");
        }

        var token = OS.GetEnvironment(AgentBridge.TokenEnv);
        var handshake = JsonSerializer.Serialize(new { token });

        // Without the secret, or with a browser's request (before or after it), the bridge just hangs up.
        foreach (var (lines, what) in new[]
                 {
                     (new[] { JsonSerializer.Serialize(new { token = token + "x" }) }, "token sbagliato"),
                     (new[] { "POST / HTTP/1.1", "Host: 127.0.0.1" }, "richiesta HTTP al posto del token"),
                     (new[] { handshake, "POST / HTTP/1.1", "{\"id\":1,\"metodo\":\"vendi\",\"parametri\":{}}" }, "richiesta HTTP dopo il token")
                 })
        {
            if (!await PlaytestAgentHangsUp(port, lines))
            {
                return Fail($"agenti: il canale resta aperto con {what}");
            }
        }

        using var client = new TcpClient();
        var connect = client.ConnectAsync("127.0.0.1", port);
        if (!await PlaytestTaskDone(connect) || !client.Connected)
        {
            return Fail($"connessione a 127.0.0.1:{port} non riuscita");
        }

        var stream = client.GetStream();
        using var reader = new StreamReader(stream, new UTF8Encoding(false));
        using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        long nextId = 0;

        long Send(string method, object? parameters = null)
        {
            var id = ++nextId;
            writer.Write(JsonSerializer.Serialize(new { id, metodo = method, parametri = parameters ?? new { } }) + "\n");
            return id;
        }

        async Task<JsonElement?> Receive()
        {
            var read = reader.ReadLineAsync();
            if (!await PlaytestTaskDone(read) || read.Result is not { } line)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(line);
            return doc.RootElement.Clone();
        }

        // One call and its answer: the result, or null after logging why it failed.
        async Task<JsonElement?> Call(string method, object? parameters = null, string? expectCode = null)
        {
            var id = Send(method, parameters);
            if (await Receive() is not { } reply || reply.GetProperty("id").GetInt64() != id)
            {
                Fail($"agenti {method}: nessuna risposta con id {id}");
                return null;
            }

            var ok = reply.GetProperty("ok").GetBoolean();
            if (expectCode is not null)
            {
                if (!ok && reply.GetProperty("codice").GetString() == expectCode)
                {
                    return reply;
                }

                Fail($"agenti {method}: atteso {expectCode}, risposta {reply}");
                return null;
            }

            if (!ok)
            {
                Fail($"agenti {method}: {reply}");
                return null;
            }

            return reply.GetProperty("risultato");
        }

        writer.Write(handshake + "\n");
        if (await Receive() is not { } welcome || !welcome.GetProperty("ok").GetBoolean())
        {
            return Fail("agenti: nessuna risposta al token giusto");
        }

        // Behind the Home no game is in play: esegui says so at once.
        if (await Call("esegui", new { secondi = 5 }) is not { } idle || idle.GetProperty("eseguito").GetBoolean())
        {
            return Fail("agenti esegui dietro la Home: atteso eseguito:false");
        }

        // Typing into a real LineEdit (the seed of Nuova partita): click it, Ctrl+A selects all, "7" replaces it.
        _settings.TutorialCompleted = true;
        OpenNewGamePanel(PlaytestSeed);
        await PlaytestWait(0.3);
        if (await Call("ui") is not { } form)
        {
            return false;
        }

        var field = form.GetProperty("controlli").EnumerateArray()
            .FirstOrDefault(c => c.GetProperty("tipo").GetString() == "LineEdit");
        if (field.ValueKind != JsonValueKind.Object)
        {
            return Fail("agenti ui: nessun campo di testo in Nuova partita");
        }

        var fieldName = field.GetProperty("nome").GetString();
        if (await Call("ui", new { clic = fieldName }) is null
            || await Call("input", new { tipo = "tasto", tasto = "Ctrl+A" }) is null
            || await Call("input", new { tipo = "tasto", tasto = "7" }) is null)
        {
            return false;
        }

        await PlaytestWait(0.1);
        var typed = await Call("ui");
        var after = typed?.GetProperty("controlli").EnumerateArray()
            .FirstOrDefault(c => c.GetProperty("nome").GetString() == fieldName);
        if (after is not { ValueKind: JsonValueKind.Object } edited || edited.GetProperty("testo").GetString() != "7")
        {
            return Fail($"agenti input tasto: Ctrl+A e 7 non scrivono '7' nel campo del seme ({after})");
        }

        OnHomeNewGame();
        _tutorial?.Close();
        FocusCameraOnCore();
        await PlaytestTerrainReady();

        // A malformed line is answered and the channel stays open.
        writer.Write("questa riga non è JSON\n");
        if (await Receive() is not { } bad || bad.GetProperty("ok").GetBoolean()
            || bad.GetProperty("codice").GetString() != AgentCodes.ParametriNonValidi)
        {
            return Fail("agenti: una riga non valida non dà ParametriNonValidi");
        }

        if (await Call("stato") is not { } stato)
        {
            return false;
        }

        var coreCell = stato.GetProperty("core").GetProperty("cella");
        var core = new { x = coreCell.GetProperty("x").GetInt32(), y = coreCell.GetProperty("y").GetInt32() };

        // A miner on the nearest iron the game accepts, through SimHost like the player's own click.
        if (await Call("trova", new { cosa = "Iron", cella = core, max = 10 }) is not { } found)
        {
            return false;
        }

        var placed = false;
        foreach (var cell in found.GetProperty("risultati").EnumerateArray())
        {
            var id = Send("piazza", new { tipo = "miner", cella = new { x = cell.GetProperty("x").GetInt32(), y = cell.GetProperty("y").GetInt32() } });
            if (await Receive() is not { } reply || reply.GetProperty("id").GetInt64() != id)
            {
                return Fail("agenti piazza: nessuna risposta");
            }

            if (reply.GetProperty("ok").GetBoolean())
            {
                placed = reply.GetProperty("risultato").GetProperty("celle").GetArrayLength() == 1;
                break;
            }
        }

        if (!placed || _view!.PlansOf(LocalPlayer).Count + _view.Sites.Count + _view.Miners.Count == 0)
        {
            return Fail("agenti: nessun minatore piazzato vicino al Core");
        }

        // avanza: exact ticks, then the game stays frozen (pausa) until riprendi.
        if (await Call("avanza", new { tick = 60 }) is not { } advanced)
        {
            return false;
        }

        var tickAfter = advanced.GetProperty("tick").GetInt64();
        if (tickAfter != advanced.GetProperty("tickPrima").GetInt64() + 60 || !PlaytestHeld)
        {
            return Fail($"agenti avanza 60: {advanced}, pausa {PlaytestHeld}");
        }

        await PlaytestWait(0.3);
        if (_view.CurrentTick != tickAfter)
        {
            return Fail($"agenti: il gioco è andato avanti in pausa ({tickAfter} → {_view.CurrentTick})");
        }

        if (await Call("riprendi") is null || PlaytestHeld)
        {
            return Fail("agenti riprendi: il gioco resta fermo");
        }

        // A HUD button found by its text with ui, clicked by input at its centre, then by name.
        if (await Call("ui") is not { } ui)
        {
            return false;
        }

        var mercatoText = Testi.T("menu.mercato");
        var button = ui.GetProperty("controlli").EnumerateArray()
            .FirstOrDefault(c => c.GetProperty("tipo").GetString() == "Button" && c.GetProperty("testo").GetString() == mercatoText);
        if (button.ValueKind != JsonValueKind.Object)
        {
            return Fail($"agenti ui: nessun pulsante '{mercatoText}' tra {ui.GetProperty("controlli").GetArrayLength()} controlli");
        }

        var rect = button.GetProperty("rett");
        var centre = new
        {
            x = rect.GetProperty("x").GetInt32() + rect.GetProperty("larghezza").GetInt32() / 2,
            y = rect.GetProperty("y").GetInt32() + rect.GetProperty("altezza").GetInt32() / 2
        };
        foreach (var how in new[] { "input", "ui" })
        {
            var clicked = how == "input"
                ? await Call("input", new { tipo = "clic", schermo = centre })
                : await Call("ui", new { clic = button.GetProperty("nome").GetString() });
            await PlaytestWait(0.3);
            if (clicked is null || _mercato?.IsOpen != true)
            {
                return Fail($"agenti {how}: il clic su '{mercatoText}' non apre il Mercato");
            }

            await Call("input", new { tipo = "tasto", tasto = "M" });
            await PlaytestWait(0.3);
            if (_mercato.IsOpen)
            {
                return Fail("agenti input tasto M: il Mercato resta aperto");
            }
        }

        // Camera on the Core, then the wheel zooms out through the real input path.
        if (await Call("telecamera", new { cella = core, zoom = 1.0 }) is not { } camera
            || GetNodeOrNull<WorldCamera>("Camera") is not { } cam || !Mathf.IsEqualApprox(cam.Zoom.X, 1f))
        {
            return Fail("agenti telecamera: zoom non applicato");
        }

        await PlaytestWait(0.1);
        if (await Call("input", new { tipo = "rotella", verso = "giu" }) is null || cam.Zoom.X >= 1f)
        {
            return Fail($"agenti rotella giu: zoom {cam.Zoom.X} (telecamera {camera})");
        }

        // cattura: a PNG of the window inside the session profile, at the window's size.
        await PlaytestWait(0.3);
        if (await Call("cattura") is not { } shot)
        {
            return false;
        }

        var shotPath = shot.GetProperty("percorso").GetString()!;
        var window = DisplayServer.WindowGetSize();
        using (var image = File.Exists(shotPath) ? Image.LoadFromFile(shotPath) : null)
        {
            if (image is null || image.GetWidth() != window.X || image.GetHeight() != window.Y
                || GameDataPaths.Override is not { } profile || !shotPath.StartsWith(profile, StringComparison.OrdinalIgnoreCase))
            {
                return Fail($"agenti cattura: {shotPath} mancante, fuori dal profilo o non {window.X}×{window.Y}");
            }

            GD.Print($"PLAYTEST agenti cattura {image.GetWidth()}×{image.GetHeight()}");
        }

        File.Copy(shotPath, Path.Combine(dir, "agenti-cattura.png"), overwrite: true);

        // The fast sRGB table gives the same bytes as the per-pixel conversion, over every 16-bit pattern.
        var halves = new byte[256 * 256 * 6];
        for (var h = 0; h < 65536; h++)
        {
            for (var c = 0; c < 3; c++)
            {
                halves[h * 6 + c * 2] = (byte)(h + c * 7);
                halves[h * 6 + c * 2 + 1] = (byte)((h + c * 7) >> 8);
            }
        }

        using (var linear = Image.CreateFromData(256, 256, false, Image.Format.Rgbh, halves))
        using (var fast = LinearToSrgb(linear))
        using (var slow = LinearToSrgbPerPixel(linear))
        {
            if (!fast.GetData().AsSpan().SequenceEqual(slow.GetData()))
            {
                return Fail("agenti cattura: la conversione sRGB a tabella non dà gli stessi byte di quella pixel per pixel");
            }
        }

        // cattura {immagine}: the PNG in the answer, fitted inside 640×360; with alta a real 1920×1080 frame.
        if (await Call("cattura", new { immagine = true }) is not { } inline || !inline.TryGetProperty("png", out var png)
            || inline.TryGetProperty("percorso", out _))
        {
            return Fail("agenti cattura immagine: nessun png nella risposta, o un file salvato");
        }

        using (var small = new Image())
        {
            if (small.LoadPngFromBuffer(Convert.FromBase64String(png.GetString()!)) != Error.Ok
                || small.GetWidth() > 640 || small.GetHeight() > 360 || Math.Max(small.GetWidth() / 640f, small.GetHeight() / 360f) < 0.99f)
            {
                return Fail($"agenti cattura immagine: {small.GetWidth()}×{small.GetHeight()}, atteso entro 640×360");
            }
        }

        if (await Call("cattura", new { alta = true, percorso = "alta.png" }) is not { } high
            || high.GetProperty("larghezza").GetInt32() != 1920 || high.GetProperty("altezza").GetInt32() != 1080
            || GetTree().Root.ContentScaleMode != Window.ContentScaleModeEnum.Disabled)
        {
            return Fail("agenti cattura alta: non 1920×1080, o la finestra non è tornata come prima");
        }

        // Frames that never come (a minimized window) cannot be made on demand: a wait of 0 ms takes the same path.
        // alta gives up with Timeout, puts the window back and frees the bridge.
        AgentBridge.HighCaptureTimeoutMs = 0;
        var stalled = await Call("cattura", new { alta = true, immagine = true }, AgentCodes.Timeout);
        AgentBridge.HighCaptureTimeoutMs = 3000;
        if (stalled is null || GetTree().Root.ContentScaleMode != Window.ContentScaleModeEnum.Disabled
            || await Call("pausa") is null)
        {
            return Fail("agenti cattura alta senza frame: niente Timeout, finestra non ripristinata o ponte bloccato");
        }

        GD.Print("PLAYTEST agenti cattura immagine e alta ok");
        foreach (var outside in new[] { "..\\..\\x.png", Path.Combine(Path.GetTempPath(), "fuori.png"), ProjectSettings.GlobalizePath("res://fuori.png") })
        {
            if (await Call("cattura", new { percorso = outside }, AgentCodes.ParametriNonValidi) is null)
            {
                return false;
            }
        }

        if (await Call("log", new { righe = 5 }) is not { } log || log.GetProperty("righe").ValueKind != JsonValueKind.Array)
        {
            return Fail("agenti log: nessuna riga");
        }

        // A burst of 50 stato while esegui 2 runs: all answered at once, esegui last, after its 2 s of real time.
        var runId = Send("esegui", new { secondi = 2 });
        for (var i = 0; i < 50; i++)
        {
            Send("stato");
        }

        var started = Time.GetTicksMsec();
        long lastTick = -1;
        for (var i = 0; i < 50; i++)
        {
            if (await Receive() is not { } reply || !reply.GetProperty("ok").GetBoolean()
                || reply.GetProperty("id").GetInt64() == runId
                || reply.GetProperty("risultato").GetProperty("tick").GetInt64() < lastTick)
            {
                return Fail($"agenti raffica: risposta {i} mancante, con errore o fuori ordine");
            }

            lastTick = reply.GetProperty("risultato").GetProperty("tick").GetInt64();
        }

        // pausa during esegui acts at once and still holds when esegui ends (the last explicit choice wins).
        await PlaytestWait(0.5);
        if (await Call("pausa") is not { } paused || !paused.GetProperty("inPausa").GetBoolean() || !PlaytestHeld)
        {
            return Fail("agenti pausa durante esegui");
        }

        if (await Receive() is not { } run || run.GetProperty("id").GetInt64() != runId || !run.GetProperty("ok").GetBoolean()
            || run.GetProperty("risultato") is var ran && (!ran.GetProperty("eseguito").GetBoolean()
                || ran.GetProperty("tick").GetInt64() <= ran.GetProperty("tickPrima").GetInt64()
                || !ran.GetProperty("inPausa").GetBoolean())
            || Time.GetTicksMsec() - started < 1500 || !PlaytestHeld)
        {
            return Fail("agenti esegui 2: risposta mancante, troppo presto o pausa persa");
        }

        if (await Call("riprendi") is null || PlaytestHeld)
        {
            return Fail("agenti riprendi");
        }

        GD.Print("PLAYTEST agenti: canale ok");
        return true;
    }

    /// <summary>
    /// A throwaway client sends <paramref name="lines"/>: true when the bridge closes the connection, after at most its
    /// welcome to a right token and without answering anything else.
    /// </summary>
    private async Task<bool> PlaytestAgentHangsUp(int port, string[] lines)
    {
        using var client = new TcpClient();
        if (!await PlaytestTaskDone(client.ConnectAsync("127.0.0.1", port)))
        {
            return false;
        }

        var stream = client.GetStream();
        using var reader = new StreamReader(stream, new UTF8Encoding(false));
        using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        writer.Write(string.Join("\n", lines) + "\n");
        for (var answers = 0; answers < 3; answers++)
        {
            var read = reader.ReadLineAsync();
            await PlaytestTaskDone(read);
            if (!read.IsCompleted)
            {
                return false; // still open after the timeout
            }

            if (read.IsFaulted || read.Result is null)
            {
                return true; // closed (or reset) by the bridge
            }

            // Only the welcome to a right token may come before the hang-up.
            if (!read.Result.Contains("\"canale\"", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Waits frames until <paramref name="task"/> ends (false after the timeout): never blocks the main thread.</summary>
    private async Task<bool> PlaytestTaskDone(Task task)
    {
        var deadline = Time.GetTicksMsec() + AgentReplyTimeoutMsec;
        while (!task.IsCompleted)
        {
            if (Time.GetTicksMsec() > deadline)
            {
                return false;
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        return task.IsCompletedSuccessfully;
    }
}
