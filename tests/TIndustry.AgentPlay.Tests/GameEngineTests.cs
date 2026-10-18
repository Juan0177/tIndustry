using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.AgentPlay.Tests;

/// <summary>
/// A test that starts the real game: skipped, with the reason, where Godot is not installed (the CI) or the project
/// was never imported and built.
/// </summary>
public sealed class GodotFactAttribute : FactAttribute
{
    public GodotFactAttribute()
    {
        if (!GodotAvailable())
        {
            Skip = "godot4 non è nel PATH (né TINDUSTRY_GODOT): i test del gioco vero si saltano.";
        }
        else if (!File.Exists(Path.Combine(ServerFixture.RepoRoot, "godot", ".godot", "mono", "temp", "bin", "Debug", "TIndustry.Godot.dll")))
        {
            Skip = "progetto Godot mai importato e compilato (godot4 --import e --build-solutions, vedi CLAUDE.md): i test del gioco vero si saltano.";
        }
    }

    // The same lookup as GameLauncher.FindGodot (the tests do not reference the server's assembly).
    private static bool GodotAvailable()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TINDUSTRY_GODOT")))
        {
            return true;
        }

        var names = OperatingSystem.IsWindows() ? new[] { "godot4.exe", "godot4.cmd", "godot4.bat" } : new[] { "godot4" };
        return (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(dir => names.Any(name => File.Exists(Path.Combine(dir.Trim('"'), name))));
    }
}

/// <summary>The real game through the MCP tools: Godot in a temporary profile, one window per test.</summary>
public sealed class GameEngineTests(ServerFixture server) : IClassFixture<ServerFixture>
{
    [GodotFact]
    public async Task GameSessionTakesAShot()
    {
        var open = await server.Ok("apri_gioco", new { seme = 1 });
        Assert.Equal("gioco", open.GetProperty("motore").GetString());
        var id = open.GetProperty("sessione").GetString()!;
        var folder = open.GetProperty("cartella").GetString()!;
        var journals = open.GetProperty("diari").GetString()!;

        // A journal saved at once replays to the game's own start: same money, resources, Core and map.
        var start = await server.Ok("stato", new { sessione = id });
        var empty = (await server.Ok("diario", new { sessione = id, nome = "inizio" })).GetProperty("percorso").GetString()!;
        var startReplay = await server.Ok("rigioca", new { percorso = empty });
        var replayedStart = await server.Ok("stato", new { sessione = startReplay.GetProperty("sessione").GetString() });
        foreach (var key in new[] { "soldi", "risorse", "core", "mappa" })
        {
            Assert.Equal(start.GetProperty(key).GetRawText(), replayedStart.GetProperty(key).GetRawText());
        }

        await server.Ok("chiudi", new { sessione = startReplay.GetProperty("sessione").GetString() });

        var core = start.GetProperty("core").GetProperty("cella");
        var ores = await server.Ok("trova", new { sessione = id, cosa = "Iron", cella = core, max = 20 });
        var placed = false;
        foreach (var hit in ores.GetProperty("risultati").EnumerateArray())
        {
            var answer = await server.Call("piazza", new { sessione = id, tipo = "miner", cella = new { x = hit.GetProperty("x").GetInt32(), y = hit.GetProperty("y").GetInt32() } });
            if (answer.GetProperty("ok").GetBoolean())
            {
                placed = true;
                break;
            }
        }

        Assert.True(placed, "nessun minatore accettato sul ferro vicino al Core");
        await server.Ok("avanza", new { sessione = id, tick = 120 });

        // The game journal carries its header and lives in the server's journal folder, not in the game's profile.
        var journalPath = (await server.Ok("diario", new { sessione = id, nome = "prova" })).GetProperty("percorso").GetString()!;
        Assert.StartsWith(journals, journalPath, StringComparison.OrdinalIgnoreCase);
        var journal = JsonDocument.Parse(await File.ReadAllTextAsync(journalPath)).RootElement;
        Assert.Equal("gioco", journal.GetProperty("partita").GetProperty("motore").GetString());
        Assert.Contains(journal.GetProperty("voci").EnumerateArray(), v => v.GetProperty("metodo").GetString() == "piazza");
        var replay = await server.Ok("rigioca", new { percorso = journalPath });
        Assert.Equal("simulazione", replay.GetProperty("motore").GetString());
        var replayed = await server.Ok("stato", new { sessione = replay.GetProperty("sessione").GetString() });
        Assert.Equal(1, replayed.GetProperty("edifici").GetProperty("minatori").GetInt32()
            + replayed.GetProperty("costruzione").GetProperty("cantieri").GetInt32());
        await server.Ok("chiudi", new { sessione = replay.GetProperty("sessione").GetString() });

        var shot = (await server.Ok("cattura", new { sessione = id })).GetProperty("percorso").GetString()!;
        var png = await File.ReadAllBytesAsync(shot);
        Assert.Equal((1280, 720), (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20))));

        await server.Ok("chiudi", new { sessione = id });
        Assert.False(Directory.Exists(folder), $"profilo rimasto: {folder}");
        Assert.False(Directory.Exists(journals), $"diari rimasti: {journals}");
    }

    [GodotFact]
    public async Task KilledGameIsReported()
    {
        // Kept, so the full log can be read after the session closed itself.
        var open = await server.Ok("apri_gioco", new { misura = 64, conserva = true });
        var id = open.GetProperty("sessione").GetString()!;
        var folder = open.GetProperty("cartella").GetString()!;

        Process.GetProcessById(open.GetProperty("processo").GetInt32()).Kill(entireProcessTree: true);
        var clock = Stopwatch.StartNew();
        var answer = await server.Call("stato", new { sessione = id });
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), $"risposta dopo {clock.Elapsed}");
        Assert.Equal(AgentCodes.GiocoTerminato, answer.GetProperty("codice").GetString());
        // The session closed itself: GiocoTerminato once, then unknown.
        Assert.Equal(AgentCodes.SessioneSconosciuta, await server.Refused("stato", new { sessione = id }));

        // The message ends with the log's last lines; the full log shows the channel was open.
        var log = File.ReadAllLines(Path.Combine(folder, "godot.log")).Where(l => l.Trim().Length > 0).ToList();
        Assert.Contains(log, l => l.Contains("AgentBridge: in ascolto"));
        Assert.EndsWith(log[^1].TrimEnd(), answer.GetProperty("messaggio").GetString()!.TrimEnd());
        Directory.Delete(folder, recursive: true);
        Directory.Delete(open.GetProperty("diari").GetString()!, recursive: true);
    }

    [GodotFact]
    public async Task ProfileNeverUnderRealData()
    {
        var open = await server.Ok("apri_gioco", new { livello = "level-01-primi-passi", lingua = "en" });
        var id = open.GetProperty("sessione").GetString()!;
        var folder = Path.GetFullPath(open.GetProperty("cartella").GetString()!);

        Assert.StartsWith(Path.GetFullPath(server.TempDirectory), folder, StringComparison.OrdinalIgnoreCase);
        foreach (var real in new[]
                 {
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "tIndustry"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Godot", "app_userdata", "tIndustry")
                 })
        {
            Assert.False(folder.StartsWith(Path.GetFullPath(real), StringComparison.OrdinalIgnoreCase), folder);
        }

        // The level is the game being played (its objectives), not the sandbox behind Home.
        Assert.True((await server.Ok("stato", new { sessione = id })).TryGetProperty("obiettivi", out _));
        // Godot itself made user:// inside the profile (APPDATA honoured); the engine log is there too.
        if (OperatingSystem.IsWindows())
        {
            Assert.True(Directory.Exists(Path.Combine(folder, "Godot", "app_userdata", "tIndustry")), "user:// non è nel profilo");
        }

        Assert.True(File.Exists(Path.Combine(folder, "godot.log")));
        // The interface is in English: the HUD's buttons are translated.
        var texts = (await server.Ok("ui", new { sessione = id })).GetProperty("controlli").EnumerateArray()
            .Select(c => c.TryGetProperty("testo", out var t) ? t.GetString() : null).ToList();
        Assert.Contains("Research", texts);
        await server.Ok("chiudi", new { sessione = id });
    }

    [GodotFact]
    public async Task KeepLeavesTheProfile()
    {
        var open = await server.Ok("apri_gioco", new { misura = 64, conserva = true });
        var folder = open.GetProperty("cartella").GetString()!;
        var journals = open.GetProperty("diari").GetString()!;

        await server.Ok("chiudi", new { sessione = open.GetProperty("sessione").GetString() });
        Assert.True(File.Exists(Path.Combine(folder, "godot.log")));
        Assert.True(Directory.Exists(journals));
        Directory.Delete(folder, recursive: true);
        Directory.Delete(journals, recursive: true);
    }

    /// <summary>pausa answers during a long esegui, and chiudi does not wait for it.</summary>
    [GodotFact]
    public async Task ChiudiDoesNotWaitForEsegui()
    {
        var open = await server.Ok("apri_gioco", new { misura = 64 });
        var id = open.GetProperty("sessione").GetString()!;
        var run = server.Call("esegui", new { sessione = id, secondi = 60 });
        await Task.Delay(1000);

        var clock = Stopwatch.StartNew();
        Assert.True((await server.Ok("pausa", new { sessione = id })).GetProperty("inPausa").GetBoolean());
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5), $"pausa dopo {clock.Elapsed}");
        await server.Ok("chiudi", new { sessione = id });
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(15), $"chiudi dopo {clock.Elapsed}");
        Assert.False((await run).GetProperty("ok").GetBoolean());
        Assert.False(Directory.Exists(open.GetProperty("cartella").GetString()));
    }

    /// <summary>A save is loaded from a copy in the profile; the file given is only read to be copied.</summary>
    [GodotFact]
    public async Task SaveFileIsLoadedFromACopy()
    {
        var content = FactoryContent.Load(Path.Combine(ServerFixture.RepoRoot, "data", "content.json"));
        var host = AgentGameSetup.Sandbox(64, 3, "classico", new GameRules()).CreateHost(content);
        var save = Path.Combine(server.TempDirectory, "da-caricare.json");
        File.WriteAllText(save, JsonSerializer.Serialize(
            new SaveFileData { Snapshot = Convert.ToBase64String(host.CreateSnapshot()) }, FactorySaveStore.JsonOptions));
        var before = await File.ReadAllBytesAsync(save);

        var open = await server.Ok("apri_gioco", new { salvataggio = save });
        var id = open.GetProperty("sessione").GetString()!;
        Assert.Equal(64, (await server.Ok("stato", new { sessione = id })).GetProperty("mappa").GetProperty("larghezza").GetInt32());
        Assert.True(File.Exists(Path.Combine(open.GetProperty("cartella").GetString()!, "godot-saves", "agente.json")));
        await server.Ok("chiudi", new { sessione = id });
        Assert.Equal(before, await File.ReadAllBytesAsync(save));
    }

    /// <summary>A server killed without any cleanup takes its games with it (job object, and the bridge's quit-without-client).</summary>
    [GodotFact]
    public async Task KilledServerTakesTheGameWithIt()
    {
        var temp = Directory.CreateTempSubdirectory("tindustry-agentplay-ucciso-").FullName;
        var start = new ProcessStartInfo("dotnet", [ServerFixture.ServerDll()])
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            WorkingDirectory = ServerFixture.RepoRoot
        };
        foreach (var (key, value) in ServerFixture.TempEnvironment(temp))
        {
            start.Environment[key] = value;
        }

        var before = GodotProcesses();
        using var serverProcess = Process.Start(start)!;
        _ = serverProcess.StandardError.ReadToEndAsync();
        foreach (var request in new object[]
                 {
                     new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "prova", version = "1" } } },
                     new { jsonrpc = "2.0", method = "notifications/initialized" },
                     new { jsonrpc = "2.0", id = 2, method = "tools/call", @params = new { name = "apri_gioco", arguments = new { misura = 64 } } }
                 })
        {
            await serverProcess.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request));
        }

        await serverProcess.StandardInput.FlushAsync();
        while (await serverProcess.StandardOutput.ReadLineAsync() is { } line && !line.Contains("\"id\":2"))
        {
        }

        var games = GodotProcesses().Except(before).ToList();
        Assert.NotEmpty(games);
        serverProcess.Kill(); // the server only, as a client or the OS would end it: no dispose runs

        var clock = Stopwatch.StartNew();
        while (games.Any(Alive) && clock.Elapsed < TimeSpan.FromSeconds(5))
        {
            await Task.Delay(100);
        }

        Assert.False(games.Any(Alive), "Godot è rimasto aperto dopo la morte del server");
        try
        {
            Directory.Delete(temp, recursive: true);
        }
        catch (IOException)
        {
            // The profile of the killed server is left to the startup sweep.
        }
    }

    /// <summary>A "Godot" that exits at once (dotnet with Godot's arguments): reported, nothing left behind. Runs on the CI too.</summary>
    [Fact]
    public async Task GameThatDoesNotStartIsReported()
    {
        var temp = Directory.CreateTempSubdirectory("tindustry-agentplay-avvio-").FullName;
        try
        {
            await using (var client = await ServerFixture.Start(temp, new() { ["TINDUSTRY_GODOT"] = "dotnet" }))
            {
                var result = await client.CallToolAsync("apri_gioco", new Dictionary<string, object?> { ["misura"] = 64 });
                var text = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(result.Content)).Text;
                Assert.Equal(AgentCodes.GiocoTerminato, JsonDocument.Parse(text).RootElement.GetProperty("codice").GetString());
            }

            var root = Path.Combine(temp, "tindustry-agentplay");
            Assert.False(Directory.Exists(root) && Directory.EnumerateDirectories(root).Any(), "profilo o diari rimasti");
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    /// <summary>The options apri_gioco accepts are the client's: colour variants, languages and UI scale limits.</summary>
    [Fact]
    public async Task GameOptionsMatchTheClient()
    {
        var godot = Path.Combine(ServerFixture.RepoRoot, "godot", "scripts");
        var colours = Regex.Match(File.ReadAllText(Path.Combine(godot, "ui", "UiColors.cs")), @"enum VarianteColori\s*\{(?<body>[^}]*)\}").Groups["body"].Value;
        var variants = Regex.Matches(Regex.Replace(colours, @"//.*", ""), @"\b([A-Z]\w*)\s*(?:,|$)").Select(m => m.Groups[1].Value);
        Assert.Equal(string.Join(", ", variants), ListIn(await Message(new { colori = "x" })));

        var languages = Regex.Match(File.ReadAllText(Path.Combine(godot, "ui", "Testi.cs")), @"Lingue\s*=\s*\[(?<list>[^\]]*)\]").Groups["list"].Value;
        Assert.Equal(string.Join(", ", Regex.Matches(languages, "\"(\\w+)\"").Select(m => m.Groups[1].Value)), ListIn(await Message(new { lingua = "x" })));

        var settings = File.ReadAllText(Path.Combine(godot, "settings", "ClientSettings.cs"));
        string Ratio(string name) =>
            (int.Parse(Regex.Match(settings, $@"{name}\s*=\s*(\d+)").Groups[1].Value) / 100.0).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(
            $"'scala' da {Ratio("UiScaleMin")} a {Ratio("UiScaleMax")} a passi di {Ratio("UiScaleStep")}.",
            await Message(new { scala = 9.0 }));
    }

    private async Task<string> Message(object args)
    {
        var answer = await server.Call("apri_gioco", args);
        Assert.Equal(AgentCodes.ParametriNonValidi, answer.GetProperty("codice").GetString());
        return answer.GetProperty("messaggio").GetString()!;
    }

    // "'colori' è uno di: a, b." -> "a, b"
    private static string ListIn(string message) => message[(message.IndexOf(": ", StringComparison.Ordinal) + 2)..].TrimEnd('.');

    private static HashSet<int> GodotProcesses() =>
        Process.GetProcesses().Where(p => p.ProcessName.Contains("godot", StringComparison.OrdinalIgnoreCase)).Select(p => p.Id).ToHashSet();

    private static bool Alive(int pid)
    {
        try
        {
            return !Process.GetProcessById(pid).HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
