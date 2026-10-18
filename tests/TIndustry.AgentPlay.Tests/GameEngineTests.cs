using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.AgentPlay.Tests;

/// <summary>A test that starts the real game: skipped, with the reason, where Godot is not installed (the CI).</summary>
public sealed class GodotFactAttribute : FactAttribute
{
    public GodotFactAttribute()
    {
        if (!GodotAvailable())
        {
            Skip = "godot4 non è nel PATH (né TINDUSTRY_GODOT): i test del gioco vero si saltano.";
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

        var core = (await server.Ok("stato", new { sessione = id })).GetProperty("core").GetProperty("cella");
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

        // The game journal carries its header, so the simulation can replay its commands.
        var journalPath = (await server.Ok("diario", new { sessione = id, nome = "prova" })).GetProperty("percorso").GetString()!;
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
    }

    [GodotFact]
    public async Task KilledGameIsReported()
    {
        var open = await server.Ok("apri_gioco", new { misura = 64 });
        var id = open.GetProperty("sessione").GetString()!;
        var folder = open.GetProperty("cartella").GetString()!;

        Process.GetProcessById(open.GetProperty("processo").GetInt32()).Kill(entireProcessTree: true);
        var clock = Stopwatch.StartNew();
        var answer = await server.Call("stato", new { sessione = id });
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), $"risposta dopo {clock.Elapsed}");
        Assert.Equal(AgentCodes.GiocoTerminato, answer.GetProperty("codice").GetString());
        // The message ends with the game's last log lines (the bridge prints where it listens).
        Assert.Contains("AgentBridge", answer.GetProperty("messaggio").GetString());

        await server.Ok("chiudi", new { sessione = id });
        Assert.False(Directory.Exists(folder));
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
        // The engine log and the client's settings are in the profile.
        Assert.True(File.Exists(Path.Combine(folder, "godot.log")));
        Assert.Contains("\"en\"", await File.ReadAllTextAsync(Path.Combine(folder, "tIndustry", "settings.json")));
        await server.Ok("chiudi", new { sessione = id });
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
            Assert.False(Directory.Exists(root) && Directory.EnumerateDirectories(root, "gioco-*").Any(), "profilo rimasto");
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [GodotFact]
    public async Task KeepLeavesTheProfile()
    {
        var open = await server.Ok("apri_gioco", new { misura = 64, conserva = true });
        var folder = open.GetProperty("cartella").GetString()!;

        await server.Ok("chiudi", new { sessione = open.GetProperty("sessione").GetString() });
        Assert.True(File.Exists(Path.Combine(folder, "godot.log")));
        Directory.Delete(folder, recursive: true);
    }
}
