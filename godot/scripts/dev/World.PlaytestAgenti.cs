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

        _settings.TutorialCompleted = true;
        OnHomeNewGame();
        _tutorial?.Close();
        FocusCameraOnCore();
        await PlaytestTerrainReady();

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
        if (await Call("cattura", new { percorso = ProjectSettings.GlobalizePath("res://fuori.png") }, AgentCodes.ParametriNonValidi) is null)
        {
            return false;
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
        for (var i = 0; i < 51; i++)
        {
            if (await Receive() is not { } reply || !reply.GetProperty("ok").GetBoolean())
            {
                return Fail($"agenti raffica: risposta {i} mancante o con errore");
            }

            var id = reply.GetProperty("id").GetInt64();
            var result = reply.GetProperty("risultato");
            if (i < 50)
            {
                var tick = result.GetProperty("tick").GetInt64();
                if (id == runId || tick < lastTick)
                {
                    return Fail($"agenti raffica: risposta {i} fuori ordine ({reply})");
                }

                lastTick = tick;
            }
            else if (id != runId || result.GetProperty("tick").GetInt64() <= result.GetProperty("tickPrima").GetInt64()
                     || Time.GetTicksMsec() - started < 1500)
            {
                return Fail($"agenti esegui 2: {reply}");
            }
        }

        if (await Call("pausa") is not { } paused || !paused.GetProperty("inPausa").GetBoolean() || !PlaytestHeld
            || await Call("riprendi") is null || PlaytestHeld)
        {
            return Fail("agenti pausa/riprendi");
        }

        GD.Print("PLAYTEST agenti: canale ok");
        return true;
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
