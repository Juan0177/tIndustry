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
        else if (!ProjectImported())
        {
            Skip = "progetto Godot mai importato e compilato (godot4 --import e --build-solutions, vedi CLAUDE.md): i test del gioco vero si saltano.";
        }
    }

    /// <summary>The same files GameLauncher.RequireImported wants: the imported texts and the built client.</summary>
    public static bool ProjectImported() =>
        File.Exists(Path.Combine(ServerFixture.RepoRoot, "godot", "lingue", "testi.it.translation"))
        && File.Exists(Path.Combine(ServerFixture.RepoRoot, "godot", ".godot", "mono", "temp", "bin", "Debug", "TIndustry.Godot.dll"));

    // The same lookup as GameLauncher.FindGodot.
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
        Assert.Equal(1, ServerFixture.Count(replayed.GetProperty("edifici"), "minatori")
            + ServerFixture.Count(replayed.GetProperty("costruzione"), "cantieri"));
        await server.Ok("chiudi", new { sessione = replay.GetProperty("sessione").GetString() });

        // With a path the capture is a file of the whole window (1280×720, narrower on a screen that cannot fit it).
        var file = await server.Ok("cattura", new { sessione = id, percorso = "prova.png" });
        var png = await File.ReadAllBytesAsync(file.GetProperty("percorso").GetString()!);
        Assert.Equal((file.GetProperty("larghezza").GetInt32(), file.GetProperty("altezza").GetInt32()), PngSize(png));
        Assert.True(PngSize(png) is (<= 1280, 720), $"{PngSize(png)}");

        await server.Ok("chiudi", new { sessione = id });
        Assert.False(Directory.Exists(folder), $"profilo rimasto: {folder}");
        Assert.False(Directory.Exists(journals), $"diari rimasti: {journals}");
    }

    /// <summary>
    /// The first real use's bugs, in one game: verso as an object, piazza's crossing, the player's direction, ui by text,
    /// over-long parameters, the journal's time tail, Esc on the Home and a game changed from the window.
    /// </summary>
    [GodotFact]
    public async Task FirstUseBugsStayFixed()
    {
        var open = await server.Ok("apri_gioco", new { misura = 64, seme = 1 });
        var id = open.GetProperty("sessione").GetString()!;
        await server.Ok("pausa", new { sessione = id });
        var core = (await server.Ok("stato", new { sessione = id })).GetProperty("core").GetProperty("cella");
        var (cx, cy) = (core.GetProperty("x").GetInt32(), core.GetProperty("y").GetInt32());

        // Bug 2: a line across a built perpendicular belt is a crossing (here refused: the junction is not researched).
        await server.Ok("piazza", new { sessione = id, tier = 1, da = new { x = cx + 6, y = cy - 5 }, a = new { x = cx + 6, y = cy + 5 }, direzione = "nord" });
        await server.Ok("avanza", new { sessione = id, tick = 6000 });
        var line = await server.Ok("piazza", new { sessione = id, tier = 1, da = new { x = cx + 3, y = cy }, a = new { x = cx + 9, y = cy }, direzione = "est" });
        Assert.Contains(line.GetProperty("rifiuti").EnumerateArray(), r => r.GetProperty("x").GetInt32() == cx + 6
            && r.GetProperty("codice").GetString() == nameof(RefusalCode.CrossingNeedsJunction));

        // Bug 5: stato and ruota use the player's own direction.
        await server.Ok("input", new { sessione = id, tipo = "tasto", tasto = "R" });
        Assert.Equal("sud", (await server.Ok("stato", new { sessione = id })).GetProperty("direzione").GetString());
        Assert.Equal("ovest", (await server.Ok("ruota", new { sessione = id })).GetProperty("direzione").GetString());

        // Bug 1: verso as an object, as documented.
        await server.Ok("input", new { sessione = id, tipo = "trascina", cella = new { x = cx - 3, y = cy - 3 }, verso = new { x = cx - 1, y = cy - 3 } });

        // Bug 6: the palette is listed, ui clicks by shown text.
        var controls = (await server.Ok("ui", new { sessione = id })).GetProperty("controlli").EnumerateArray().ToList();
        Assert.Contains(controls, c => c.TryGetProperty("suggerimento", out var s) && s.GetString()!.StartsWith("Nastro T1", StringComparison.Ordinal));
        await server.Ok("ui", new { sessione = id, clic = "Ricerca" });
        await server.Ok("input", new { sessione = id, tipo = "tasto", tasto = "Escape" });

        // Compact ui: each name is the shortest end that clic still finds alone, rett is [x, y, w, h]; dettaglio gives
        // the whole paths and rectangles.
        var compact = (await server.Ok("ui", new { sessione = id })).GetProperty("controlli").EnumerateArray().ToList();
        var full = (await server.Ok("ui", new { sessione = id, dettaglio = true })).GetProperty("controlli").EnumerateArray().ToList();
        Assert.Equal(full.Count, compact.Count);
        Assert.All(compact.Zip(full), pair =>
        {
            var (shortName, longName) = (pair.First.GetProperty("nome").GetString()!, pair.Second.GetProperty("nome").GetString()!);
            Assert.True(longName == shortName || longName.EndsWith("/" + shortName, StringComparison.Ordinal), $"{shortName} / {longName}");
            Assert.Equal(4, pair.First.GetProperty("rett").GetArrayLength());
            Assert.Equal(pair.Second.GetProperty("rett").GetProperty("larghezza").GetInt32(), pair.First.GetProperty("rett")[2].GetInt32());
        });
        Assert.Contains(full, c => c.GetProperty("nome").GetString()!.StartsWith("FactoryHudRoot/", StringComparison.Ordinal));
        var shortName = compact.Select(c => c.GetProperty("nome").GetString()!)
            .First(n => n.Contains('/') && full.Any(f => f.GetProperty("nome").GetString()!.EndsWith("/" + n) && f.GetProperty("tipo").GetString() == "Label"));
        var clicked = (await server.Ok("ui", new { sessione = id, clic = shortName })).GetProperty("clic").GetString()!;
        Assert.EndsWith("/" + shortName, "/" + clicked);
        await server.Ok("avanza", new { sessione = id, tick = 2 });

        // Review M2 and M5: over-long or out-of-range parameters are refused, the game goes on.
        Assert.Equal(AgentCodes.ParametriNonValidi, await server.Refused("ui", new { sessione = id, clic = new string('x', 70_000) }));
        Assert.Equal(AgentCodes.ParametriNonValidi, await server.Refused("esegui", new { sessione = id, secondi = 500 }));

        // Review I1: the time after the last command replays too.
        await server.Ok("esegui", new { sessione = id, secondi = 2 });
        var played = await server.Ok("stato", new { sessione = id });
        var tail = (await server.Ok("diario", new { sessione = id, nome = "coda" })).GetProperty("percorso").GetString()!;
        var replay = await server.Ok("rigioca", new { percorso = tail.Replace('\\', '/') });
        Assert.Equal(played.GetProperty("impronta").GetString(), replay.GetProperty("impronta").GetString());
        Assert.Contains(replay.GetProperty("avvisi").EnumerateArray(), a => a.GetString()!.Contains("'input'", StringComparison.Ordinal));
        await server.Ok("chiudi", new { sessione = replay.GetProperty("sessione").GetString() });

        // Bug 3: Esc opens the Home over the game and Esc again goes back to it; Riprendi is there, the HUD under it is not.
        await server.Ok("input", new { sessione = id, tipo = "tasto", tasto = "Escape" });
        var home = (await server.Ok("ui", new { sessione = id })).GetProperty("controlli").EnumerateArray()
            .Select(c => c.GetProperty("nome").GetString()!).ToList();
        Assert.Contains(home, n => n == "Riprendi" || n.EndsWith("/Riprendi", StringComparison.Ordinal));
        Assert.DoesNotContain(home, n => n == "Ricerca" || n.EndsWith("/Ricerca", StringComparison.Ordinal));
        await server.Ok("input", new { sessione = id, tipo = "tasto", tasto = "Escape" });
        Assert.True((await server.Ok("esegui", new { sessione = id, secondi = 0.5 })).GetProperty("eseguito").GetBoolean());

        // Bug 4 and review I2: a new game started from the window restarts the journal from its snapshot.
        await server.Ok("input", new { sessione = id, tipo = "tasto", tasto = "Escape" });
        await server.Ok("ui", new { sessione = id, clic = "NuovaPartita" });
        await server.Ok("ui", new { sessione = id, clic = "Inizia" });
        await server.Ok("pausa", new { sessione = id });
        Assert.Contains((await server.Ok("eventi", new { sessione = id })).GetProperty("eventi").EnumerateArray(),
            e => e.GetProperty("tipo").GetString() == "partitaCaricata");
        var fresh = await server.Ok("stato", new { sessione = id });
        var after = (await server.Ok("diario", new { sessione = id, nome = "dopo" })).GetProperty("percorso").GetString()!;
        Assert.True(JsonDocument.Parse(await File.ReadAllTextAsync(after)).RootElement.GetProperty("partita").TryGetProperty("partenza", out _));
        var again = await server.Ok("rigioca", new { percorso = after });
        Assert.Equal(fresh.GetProperty("impronta").GetString(), again.GetProperty("impronta").GetString());
        await server.Ok("chiudi", new { sessione = again.GetProperty("sessione").GetString() });
        await server.Ok("chiudi", new { sessione = id });
    }

    /// <summary>
    /// Every compact ui name of real screens (HUD, an open panel, the Home) names its own control alone under the
    /// bridge's rule, and the bridge's clic lands on that control (all the labels are clicked: harmless).
    /// </summary>
    [GodotFact]
    public async Task EveryCompactUiNameFindsItsControl()
    {
        var id = (await server.Ok("apri_gioco", new { misura = 64, seme = 1 })).GetProperty("sessione").GetString()!;
        await server.Ok("pausa", new { sessione = id });
        foreach (var scene in new[] { "hud", "ricerca", "home" })
        {
            if (scene == "ricerca")
            {
                await server.Ok("ui", new { sessione = id, clic = "Ricerca" });
                await server.Ok("avanza", new { sessione = id, tick = 2 });
            }
            else if (scene == "home")
            {
                await server.Ok("input", new { sessione = id, tipo = "tasto", tasto = "Escape" });
                await server.Ok("input", new { sessione = id, tipo = "tasto", tasto = "Escape" });
                await server.Ok("avanza", new { sessione = id, tick = 2 });
            }

            var compact = await server.Ok("ui", new { sessione = id });
            var full = await server.Ok("ui", new { sessione = id, dettaglio = true });
            Assert.False(compact.TryGetProperty("troncato", out _), scene);
            var entries = full.GetProperty("controlli").EnumerateArray().Select(c => (
                Name: c.GetProperty("nome").GetString()!,
                Text: c.TryGetProperty("testo", out var t) ? t.GetString()! : "",
                Tooltip: c.TryGetProperty("suggerimento", out var s) ? s.GetString()! : "",
                Type: c.GetProperty("tipo").GetString()!)).ToList();
            var names = compact.GetProperty("controlli").EnumerateArray().Select(c => c.GetProperty("nome").GetString()!).ToList();
            Assert.Equal(entries.Count, names.Count);
            for (var i = 0; i < names.Count; i++)
            {
                var own = i;
                var hits = entries.Select((e, at) => (e, at)).Where(x => x.e.Name == names[own]
                    || AgentUi.Matches(x.e.Name, x.e.Text, x.e.Tooltip, names[own])).Select(x => x.at).ToList();
                Assert.True(hits.SequenceEqual([own]), $"{scene}: '{names[own]}' indica {hits.Count} controlli");
            }

            if (scene == "hud")
            {
                for (var i = 0; i < names.Count; i++)
                {
                    if (entries[i].Type == "Label")
                    {
                        var clicked = (await server.Ok("ui", new { sessione = id, clic = names[i] })).GetProperty("clic").GetString();
                        Assert.Equal(entries[i].Name, clicked);
                    }
                }
            }
        }

        await server.Ok("chiudi", new { sessione = id });
    }

    /// <summary>cattura answers with the image itself: compact by default, a real 1080p render with alta, cut to cells.</summary>
    [GodotFact]
    public async Task ShotComesInTheAnswer()
    {
        var id = (await server.Ok("apri_gioco", new { misura = 64, seme = 1 })).GetProperty("sessione").GetString()!;
        await server.Ok("pausa", new { sessione = id });

        // Default: the text and a PNG fitted inside 640×360, and no file.
        var (text, png) = await Shot(new { sessione = id });
        var size = PngSize(png);
        Assert.True(size is (<= 640, <= 360) && (size.Width == 640 || size.Height == 360), $"{size}");
        Assert.Equal(size, (text.GetProperty("immagine")[0].GetInt32(), text.GetProperty("immagine")[1].GetInt32()));
        Assert.False(text.TryGetProperty("percorso", out _));

        // alta: a real 1920×1080 frame, not an enlarged window.
        var (high, highPng) = await Shot(new { sessione = id, alta = true });
        Assert.Equal((1920, 1080), PngSize(highPng));
        Assert.Equal(1920, high.GetProperty("larghezza").GetInt32());

        // Cells around the Core at zoom 1: 4×4 cells of 64 pixels, cut from the frame.
        var core = (await server.Ok("stato", new { sessione = id })).GetProperty("core").GetProperty("cella");
        var (cx, cy) = (core.GetProperty("x").GetInt32(), core.GetProperty("y").GetInt32());
        await server.Ok("telecamera", new { sessione = id, cella = core, zoom = 1 });
        await server.Ok("avanza", new { sessione = id, tick = 2 });
        var (cut, cutPng) = await Shot(new { sessione = id, da = new { x = cx - 1, y = cy - 1 }, a = new { x = cx + 2, y = cy + 2 } });
        Assert.True(PngSize(cutPng) is (>= 255 and <= 257, >= 255 and <= 257), $"{PngSize(cutPng)}");
        Assert.Equal(4, cut.GetProperty("regione").GetArrayLength());

        // A file stays an option, only inside the profile's catture.
        var file = await server.Ok("cattura", new { sessione = id, percorso = "scena.png", regione = new { x = 0, y = 0, larghezza = 100, altezza = 50 } });
        Assert.Equal((100, 50), PngSize(await File.ReadAllBytesAsync(file.GetProperty("percorso").GetString()!)));
        Assert.Equal(AgentCodes.ParametriNonValidi, await server.Refused("cattura", new { sessione = id, percorso = "../fuori.png" }));
        await server.Ok("chiudi", new { sessione = id });
    }

    private async Task<(JsonElement Text, byte[] Png)> Shot(object args)
    {
        var result = await server.CallResult("cattura", args);
        Assert.Equal(2, result.Content.Count);
        var text = JsonDocument.Parse(Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(result.Content[0]).Text).RootElement.Clone();
        Assert.True(text.GetProperty("ok").GetBoolean(), text.ToString());
        var image = Assert.IsType<ModelContextProtocol.Protocol.ImageContentBlock>(result.Content[1]);
        Assert.Equal("image/png", image.MimeType);
        return (text, image.DecodedData.ToArray());
    }

    private static (int Width, int Height) PngSize(byte[] png)
    {
        Assert.True(png.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }), "non è un PNG");
        return (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));
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
                var answer = JsonDocument.Parse(text).RootElement;
                if (GodotFactAttribute.ProjectImported())
                {
                    Assert.Equal(AgentCodes.GiocoTerminato, answer.GetProperty("codice").GetString());
                }
                else
                {
                    // CI tests before importing the project: the launcher refuses before starting anything.
                    Assert.Equal(AgentCodes.MotoreNonDisponibile, answer.GetProperty("codice").GetString());
                    Assert.Contains("--import", answer.GetProperty("messaggio").GetString());
                }
            }

            var root = Path.Combine(temp, "tindustry-agentplay");
            Assert.False(Directory.Exists(root) && Directory.EnumerateDirectories(root).Any(), "profilo o diari rimasti");
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    /// <summary>Without Godot the answer says exactly what to put in .mcp.json, and nothing is left behind.</summary>
    [Fact]
    public async Task MissingGodotSaysWhatToSet()
    {
        var temp = Directory.CreateTempSubdirectory("tindustry-agentplay-senza-godot-").FullName;
        try
        {
            // Only dotnet's folder on the PATH: the server still starts, godot4 is not found.
            var dotnet = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
                .First(dir => File.Exists(Path.Combine(dir.Trim('"'), OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet")));
            // An unexpanded .mcp.json placeholder counts as unset.
            await using (var client = await ServerFixture.Start(temp, new() { ["TINDUSTRY_GODOT"] = "${TINDUSTRY_GODOT:-}", ["PATH"] = dotnet }))
            {
                var result = await client.CallToolAsync("apri_gioco", new Dictionary<string, object?> { ["misura"] = 64 });
                var text = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(result.Content)).Text;
                var answer = JsonDocument.Parse(text).RootElement;
                Assert.Equal(AgentCodes.MotoreNonDisponibile, answer.GetProperty("codice").GetString());
                var message = answer.GetProperty("messaggio").GetString()!;
                Assert.Contains(".mcp.json", message);
                Assert.Contains("\"TINDUSTRY_GODOT\": \"${TINDUSTRY_GODOT:-}\"", message);
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
