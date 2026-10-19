using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.AgentPlay.Tests;

public sealed class AgentPlayServerTests(ServerFixture server) : IClassFixture<ServerFixture>
{
    private static readonly string[] ToolNames =
    [
        "apri_simulazione", "apri_gioco", "collega_coop", "sessioni", "chiudi", "stato", "area", "trova", "eventi", "catalogo",
        "piazza", "smonta", "ruota", "sblocca", "vendi", "potenzia_core", "avanza", "esegui", "pausa", "riprendi",
        "cattura", "telecamera", "input", "ui", "log", "diario", "rigioca"
    ];

    [Fact]
    public async Task ServerListsToolsAndPlaysASimulation()
    {
        var tools = await server.Client.ListToolsAsync();
        var names = tools.Select(t => t.Name).ToList();
        // Always in the order Tools declares them, and the free parameters say what they take.
        Assert.Equal(ToolNames, names);
        var misura = tools.Single(t => t.Name == "apri_simulazione").JsonSchema.GetProperty("properties").GetProperty("misura");
        Assert.Contains("integer", misura.GetProperty("type").EnumerateArray().Select(t => t.GetString()));
        var verso = tools.Single(t => t.Name == "input").JsonSchema.GetProperty("properties").GetProperty("verso");
        Assert.Contains("object", verso.GetProperty("type").EnumerateArray().Select(t => t.GetString()));
        Assert.All(tools, t => Assert.False(string.IsNullOrWhiteSpace(t.Description), t.Name));

        var opened = await server.Ok("apri_simulazione", new { misura = "Media", seme = 1 });
        var id = opened.GetProperty("sessione").GetString()!;
        var folder = opened.GetProperty("cartella").GetString()!;
        Assert.Equal("simulazione", opened.GetProperty("motore").GetString());
        Assert.StartsWith(server.TempDirectory, folder, StringComparison.OrdinalIgnoreCase);
        Assert.True(Directory.Exists(folder));
        Assert.Contains(id, (await server.Ok("sessioni")).GetProperty("sessioni").EnumerateArray()
            .Select(s => s.GetProperty("sessione").GetString()));

        var before = await server.Ok("stato", new { sessione = id });
        Assert.Equal(1000, before.GetProperty("mappa").GetProperty("larghezza").GetInt32());
        var core = before.GetProperty("core").GetProperty("cella");
        var iron = (await server.Ok("trova", new { sessione = id, cosa = "Iron", cella = core, max = 1 }))
            .GetProperty("risultati")[0];

        var placed = await server.Ok("piazza", new
        {
            sessione = id, tipo = "miner", cella = new { x = iron.GetProperty("x").GetInt32(), y = iron.GetProperty("y").GetInt32() }
        });
        Assert.Single(placed.GetProperty("celle").EnumerateArray());
        var advanced = await server.Ok("avanza", new { sessione = id, tick = 120 });
        Assert.Equal(advanced.GetProperty("tickPrima").GetInt64() + 120, advanced.GetProperty("tick").GetInt64());

        // The drone has started (or finished) the miner: it is a site or a building, no longer only a plan.
        var after = await server.Ok("stato", new { sessione = id });
        // Compact stato: a building count of 0 is left out.
        var built = ServerFixture.Count(after.GetProperty("edifici"), "minatori")
            + ServerFixture.Count(after.GetProperty("costruzione"), "cantieri");
        Assert.Equal(1, built);
        Assert.True(after.GetProperty("tick").GetInt64() > before.GetProperty("tick").GetInt64());

        await server.Ok("chiudi", new { sessione = id });
        Assert.False(Directory.Exists(folder));
        Assert.Equal(AgentCodes.SessioneSconosciuta, await server.Refused("stato", new { sessione = id }));
        Assert.Equal(AgentCodes.SessioneSconosciuta, await server.Refused("chiudi", new { sessione = id }));
    }

    [Fact]
    public async Task AnswersAreCompactUnlessDettaglio()
    {
        var id = await Open(new { misura = 64, seme = 1 });
        var stato = await server.Ok("stato", new { sessione = id });
        var full = await server.Ok("stato", new { sessione = id, dettaglio = true });
        Assert.Equal(0, full.GetProperty("edifici").GetProperty("forni").GetInt32());
        Assert.False(stato.TryGetProperty("edifici", out var buildings) && buildings.TryGetProperty("forni", out _));
        Assert.Equal(full.GetProperty("impronta").GetString(), stato.GetProperty("impronta").GetString());
        Assert.True(stato.GetRawText().Length < full.GetRawText().Length);

        // A line says its direction once, and its cells built one by one come back as one event.
        var core = stato.GetProperty("core").GetProperty("cella");
        var (x, y) = (core.GetProperty("x").GetInt32() + 4, core.GetProperty("y").GetInt32() - 6);
        var line = await server.Ok("piazza", new { sessione = id, tier = 1, da = new { x, y }, a = new { x, y = y + 3 }, direzione = "sud" });
        Assert.True(line.TryGetProperty("direzione", out var lineDirection), line.ToString());
        Assert.Equal("sud", lineDirection.GetString());
        Assert.All(line.GetProperty("celle").EnumerateArray(), c => Assert.False(c.TryGetProperty("direzione", out _)));
        await server.Ok("avanza", new { sessione = id, tick = 600 });
        var built = Assert.Single((await server.Ok("eventi", new { sessione = id })).GetProperty("eventi").EnumerateArray(),
            e => e.GetProperty("tipo").GetString() == "costruito");
        Assert.Equal(4, built.GetProperty("celle").GetArrayLength());
        Assert.True(built.GetProperty("fino").GetInt64() > built.GetProperty("tick").GetInt64());
        Assert.False(built.TryGetProperty("cantiere", out _));

        // dettaglio: the full form, one event per cell, as before.
        await server.Ok("piazza", new { sessione = id, tier = 1, da = new { x = x + 2, y }, a = new { x = x + 2, y = y + 1 }, direzione = "sud" });
        await server.Ok("avanza", new { sessione = id, tick = 600 });
        var each = (await server.Ok("eventi", new { sessione = id, dettaglio = true })).GetProperty("eventi").EnumerateArray()
            .Where(e => e.GetProperty("tipo").GetString() == "costruito").ToList();
        Assert.Equal(2, each.Count);
        Assert.All(each, e => Assert.True(e.TryGetProperty("cantiere", out _) && e.TryGetProperty("cella", out _)));

        var saved = await server.Ok("diario", new { sessione = id, nome = "compatto" });
        Assert.Equal(4, saved.GetProperty("numeroVoci").GetInt32());
        Assert.False(saved.TryGetProperty("voci", out _));
        Assert.Equal(4, (await server.Ok("diario", new { sessione = id, dettaglio = true })).GetProperty("voci").GetArrayLength());
        await server.Ok("chiudi", new { sessione = id });
    }

    [Fact]
    public async Task CoopIsAPlaceholder()
    {
        Assert.Equal(AgentCodes.MotoreNonDisponibile, await server.Refused("collega_coop"));
    }

    [Fact]
    public async Task GameOptionsAreCheckedBeforeGodotStarts()
    {
        foreach (var args in new object[]
                 {
                     new { lingua = "xx" }, new { scala = 0.5 }, new { scala = 1.03 }, new { colori = "Viola" },
                     new { livello = "nessuno" }, new { misura = 5 }, new { generatore = "biomi-9" },
                     new { salvataggio = Path.Combine(server.TempDirectory, "non-esiste.json") },
                     new { salvataggio = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "tIndustry", "godot-saves", "slot-1.json") },
                     new { livello = "level-01-primi-passi", salvataggio = Path.Combine(server.TempDirectory, "x.json") }
                 })
        {
            Assert.Equal(AgentCodes.ParametriNonValidi, await server.Refused("apri_gioco", args));
        }
    }

    [Fact]
    public async Task GameOnlyToolsNeedTheRealGame()
    {
        var id = await Open(new { misura = "64", generatore = "classico" });
        foreach (var (tool, args) in new (string, object)[]
        {
            ("esegui", new { sessione = id, secondi = 1.0 }), ("riprendi", new { sessione = id }),
            ("cattura", new { sessione = id }), ("telecamera", new { sessione = id }),
            ("input", new { sessione = id, tipo = "clic" }), ("ui", new { sessione = id }), ("log", new { sessione = id }),
            // verso as an object, as documented: the parameter check lets it through (bug 1), the engine has no window.
            ("input", new { sessione = id, tipo = "trascina", cella = new { x = 1, y = 1 }, verso = new { x = 3, y = 1 } })
        })
        {
            Assert.Equal(AgentCodes.MotoreNonDisponibile, await server.Refused(tool, args));
        }

        Assert.True((await server.Ok("pausa", new { sessione = id })).GetProperty("inPausa").GetBoolean());
        await server.Ok("chiudi", new { sessione = id });
    }

    [Fact]
    public async Task BadParamsNeverKillTheServer()
    {
        var id = await Open(new { misura = "64", generatore = "classico" });
        var bad = new (string Tool, object Args)[]
        {
            ("apri_simulazione", new { misura = "Gigante" }),
            ("apri_simulazione", new { seme = "uno" }),
            ("apri_simulazione", new { generatore = "nessuno" }),
            ("apri_simulazione", new { livello = "non-esiste" }),
            ("apri_simulazione", new { salvataggio = Path.Combine(server.TempDirectory, "manca.json") }),
            ("apri_gioco", new { seme = "uno" }),
            ("collega_coop", new { indirizzo = 5 }),
            ("chiudi", new { }),
            ("stato", new { sessione = new { x = 1 } }),
            ("area", new { sessione = id, da = new { x = 0, y = 0 }, a = new { x = 100, y = 100 } }),
            ("area", new { sessione = id, da = new { x = 0 } }),
            ("trova", new { sessione = id, cosa = "pizza", cella = new { x = 0, y = 0 } }),
            ("eventi", new { }),
            ("catalogo", new { sessione = 5 }),
            ("piazza", new { sessione = id }),
            ("piazza", new { sessione = id, tipo = "miner", cella = new { x = -1, y = 0 } }),
            ("smonta", new { sessione = id, cella = "qui" }),
            ("ruota", new { sessione = 7 }),
            ("sblocca", new { sessione = id, struttura = 3 }),
            ("vendi", new { sessione = id, oggetto = "iron-ore", quantita = "tante" }),
            ("potenzia_core", new { }),
            ("avanza", new { sessione = id, tick = -1 }),
            ("avanza", new { sessione = id, tick = "molti" }),
            ("esegui", new { sessione = id, secondi = "due" }),
            ("pausa", new { }),
            ("riprendi", new { sessione = false }),
            ("cattura", new { sessione = id, percorso = 1 }),
            ("telecamera", new { sessione = id, zoom = "vicino" }),
            ("input", new { sessione = id }),
            ("ui", new { sessione = id, clic = 1 }),
            ("log", new { sessione = id, righe = "tutte" }),
            ("diario", new { sessione = id, nome = "../fuori" }),
            ("rigioca", new { }),
            ("rigioca", new { percorso = Path.Combine(server.TempDirectory, "manca.json") })
        };

        // sessioni takes no parameters, so it has no wrong ones.
        Assert.Equal(ToolNames.Where(t => t != "sessioni").Order(), bad.Select(b => b.Tool).Distinct().Order());
        foreach (var (tool, args) in bad)
        {
            Assert.True(await server.Refused(tool, args) == AgentCodes.ParametriNonValidi, $"{tool} {JsonSerializer.Serialize(args)}");
        }

        await server.Ok("stato", new { sessione = id });
        await server.Ok("chiudi", new { sessione = id });
    }

    [Fact]
    public async Task RigiocaReproducesTheGame()
    {
        var id = await Open(new { misura = "64", generatore = "classico", costruzioneIstantanea = true });
        var core = (await server.Ok("stato", new { sessione = id })).GetProperty("core").GetProperty("cella");
        var start = new { x = core.GetProperty("x").GetInt32() + 4, y = core.GetProperty("y").GetInt32() };
        await server.Ok("piazza", new { sessione = id, tipo = "conveyor-basic", da = start, a = new { start.x, y = start.y + 5 } });
        await server.Ok("ruota", new { sessione = id });
        await server.Ok("avanza", new { sessione = id, tick = 90 });
        await server.Refused("vendi", new { sessione = id, oggetto = "iron-plate", quantita = 100000 });
        var saved = await server.Ok("diario", new { sessione = id, nome = "partita" });
        var path = saved.GetProperty("percorso").GetString()!;
        Assert.True(Path.IsPathRooted(path) && File.Exists(path));
        var original = await server.Ok("stato", new { sessione = id });

        var replay = await server.Ok("rigioca", new { percorso = path });
        var copy = replay.GetProperty("sessione").GetString()!;
        Assert.NotEqual(id, copy);
        Assert.Matches("^[0-9a-f]{16}$", replay.GetProperty("impronta").GetString());
        var replayed = await server.Ok("stato", new { sessione = copy });
        Assert.Equal(original.GetRawText(), replayed.GetRawText());
        Assert.Equal(original.GetProperty("impronta").GetString(), replay.GetProperty("impronta").GetString());

        await server.Ok("chiudi", new { sessione = id });
        Assert.False(File.Exists(path));
        await server.Ok("chiudi", new { sessione = copy });
    }

    [Fact]
    public async Task CatalogoAndRefusalsSayWhatIsMissing()
    {
        var id = await Open(new { misura = "64", generatore = "classico" });
        var junction = (await server.Ok("catalogo", new { sessione = id })).GetProperty("strutture").EnumerateArray()
            .Single(s => s.GetProperty("id").GetString() == "junction");
        Assert.False(junction.GetProperty("sbloccato").GetBoolean());

        var refused = await server.Call("sblocca", new { sessione = id, struttura = "junction" });
        Assert.Equal(nameof(RefusalCode.UnlockUnaffordable), refused.GetProperty("codice").GetString());
        Assert.Equal(20, refused.GetProperty("mancano").GetProperty("soldi").GetInt32());
        await server.Ok("chiudi", new { sessione = id });
    }

    [Fact]
    public async Task KeptSessionsLeaveTheirFolder()
    {
        var opened = await server.Ok("apri_simulazione", new { misura = "64", generatore = "classico", conserva = true });
        var folder = opened.GetProperty("cartella").GetString()!;
        await server.Ok("chiudi", new { sessione = opened.GetProperty("sessione").GetString() });
        Assert.True(Directory.Exists(folder));
        Assert.True(File.Exists(Path.Combine(folder, ".conserva")));
    }

    [Fact]
    public async Task ParameterErrorsNameTheParameter()
    {
        var id = await Open(new { misura = 64, generatore = "classico" });
        Assert.Matches("^s[0-9a-f]{4}-[0-9]+$", id);

        async Task<string> Message(string tool, object args)
        {
            var answer = await server.Call(tool, args);
            Assert.Equal(AgentCodes.ParametriNonValidi, answer.GetProperty("codice").GetString());
            return answer.GetProperty("messaggio").GetString()!;
        }

        Assert.Equal("'tick' ha il tipo sbagliato: atteso numero intero.", await Message("avanza", new { sessione = id, tick = "120" }));
        Assert.Equal("'tick' ha il tipo sbagliato: atteso numero intero.", await Message("avanza", new { sessione = id, tick = 1e12 }));
        Assert.Equal("manca 'sessione' (testo).", await Message("chiudi", new { }));
        Assert.Equal("manca 'da.y' (numero intero).", await Message("area", new { sessione = id, da = new { x = 0 }, a = new { x = 1, y = 1 } }));
        var typo = await Message("piazza", new { sessione = id, tipo = "miner", cella = new { x = 1, y = 1 }, direzzione = "est" });
        Assert.StartsWith("'direzzione' non è un parametro: quelli validi sono ", typo);
        Assert.Contains("direzione", typo);
        Assert.Contains("questo strumento non ne ha", await Message("sessioni", new { tutte = true }));
        Assert.Contains("Misura", await Message("apri_simulazione", new { misura = 5000 }));
        Assert.DoesNotContain("JSON", await Message("vendi", new { sessione = id, oggetto = 1, quantita = 1 }));
        await server.Ok("chiudi", new { sessione = id });
    }

    [Fact]
    public async Task ParallelCallsOnOneSessionRunOneAtATime()
    {
        var id = await Open(new { misura = "64", generatore = "classico" });
        var start = (await server.Ok("stato", new { sessione = id })).GetProperty("tick").GetInt64();
        var calls = Enumerable.Range(0, 30).Select(i => i % 3 == 0
            ? server.Ok("stato", new { sessione = id })
            : server.Ok("avanza", new { sessione = id, tick = 1 }));
        await Task.WhenAll(calls);
        Assert.Equal(start + 20, (await server.Ok("stato", new { sessione = id })).GetProperty("tick").GetInt64());
        await server.Ok("chiudi", new { sessione = id });
    }

    [Fact]
    public async Task SaveFilesOpenButNeverThePlayersOwn()
    {
        var content = FactoryContent.Load(Path.Combine(ServerFixture.RepoRoot, "data", "content.json"));
        var host = AgentGameSetup.Sandbox(64, 3, "classico", new GameRules()).CreateHost(content);
        var save = Path.Combine(server.TempDirectory, "partita.json");
        File.WriteAllText(save, JsonSerializer.Serialize(
            new SaveFileData { Snapshot = Convert.ToBase64String(host.CreateSnapshot()) }, FactorySaveStore.JsonOptions));

        var id = await Open(new { salvataggio = save });
        Assert.Equal(64, (await server.Ok("stato", new { sessione = id })).GetProperty("mappa").GetProperty("larghezza").GetInt32());
        await server.Ok("chiudi", new { sessione = id });

        Assert.Equal(AgentCodes.ParametriNonValidi, await server.Refused("apri_simulazione", new { salvataggio = save, costruzioneIstantanea = true }));
        var real = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "tIndustry", "godot-saves", "slot-1.json");
        Assert.Equal(AgentCodes.ParametriNonValidi, await server.Refused("apri_simulazione", new { salvataggio = real }));

        var broken = Path.Combine(server.TempDirectory, "rotto.json");
        File.WriteAllText(broken, "{\"version\": 12, \"snapshot\": \"non base64!\"}");
        Assert.Equal(nameof(SaveLoadError.Unreadable), await server.Refused("apri_simulazione", new { salvataggio = broken }));
        File.WriteAllText(broken, "{\"version\": 1, \"snapshot\": \"AAAA\"}");
        Assert.Equal(nameof(SaveLoadError.OlderVersion), await server.Refused("apri_simulazione", new { salvataggio = broken }));
    }

    [Fact]
    public async Task StartupSweepsOldFoldersButNotKeptOnes()
    {
        var temp = Directory.CreateTempSubdirectory("tindustry-agentplay-sweep-").FullName;
        try
        {
            var root = Path.Combine(temp, "tindustry-agentplay");
            var old = Directory.CreateDirectory(Path.Combine(root, "vecchia")).FullName;
            var kept = Directory.CreateDirectory(Path.Combine(root, "tenuta")).FullName;
            var fresh = Directory.CreateDirectory(Path.Combine(root, "nuova")).FullName;
            var live = Directory.CreateDirectory(Path.Combine(root, "viva")).FullName;
            File.WriteAllText(Path.Combine(kept, ".conserva"), "");
            // A live session of another server: its lock file is held open, however old the folder looks.
            using var liveLock = new FileStream(Path.Combine(live, ".in-uso"), FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            foreach (var dir in new[] { old, kept, live })
            {
                Directory.SetLastWriteTimeUtc(dir, DateTime.UtcNow.AddDays(-2));
            }

            await using (var client = await ServerFixture.Start(temp))
            {
                await client.ListToolsAsync();
            }

            Assert.False(Directory.Exists(old));
            Assert.True(Directory.Exists(kept));
            Assert.True(Directory.Exists(fresh));
            Assert.True(Directory.Exists(live));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    /// <summary>Raw stdio, without the SDK client (which skips stray lines): every stdout line is a JSON-RPC message.</summary>
    [Fact]
    public async Task StdoutCarriesOnlyJsonRpc()
    {
        var start = new System.Diagnostics.ProcessStartInfo("dotnet", [ServerFixture.ServerDll()])
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var (key, value) in ServerFixture.TempEnvironment(server.TempDirectory))
        {
            start.Environment[key] = value;
        }

        using var process = System.Diagnostics.Process.Start(start)!;
        var stderr = process.StandardError.ReadToEndAsync();
        var requests = new object[]
        {
            new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "prova", version = "1" } } },
            new { jsonrpc = "2.0", method = "notifications/initialized" },
            new { jsonrpc = "2.0", id = 2, method = "tools/list" },
            new { jsonrpc = "2.0", id = 3, method = "tools/call", @params = new { name = "apri_simulazione", arguments = new { misura = "64", generatore = "classico" } } },
            new { jsonrpc = "2.0", id = 4, method = "tools/call", @params = new { name = "avanza", arguments = new { sessione = "nessuna", tick = "x" } } },
            new { jsonrpc = "2.0", id = 5, method = "tools/call", @params = new { name = "non_esiste", arguments = new { } } }
        };
        foreach (var request in requests)
        {
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request));
        }

        await process.StandardInput.FlushAsync();
        var lines = new List<string>();
        while (lines.Count(l => l.Contains("\"id\"")) < 5 && await process.StandardOutput.ReadLineAsync() is { } line)
        {
            lines.Add(line);
        }

        process.StandardInput.Close();
        lines.AddRange((await process.StandardOutput.ReadToEndAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.True(process.WaitForExit(30_000));
        await stderr;

        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.Equal("2.0", JsonDocument.Parse(l).RootElement.GetProperty("jsonrpc").GetString()));
        Assert.Equal(0, process.ExitCode);
    }

    private async Task<string> Open(object args) =>
        (await server.Ok("apri_simulazione", args)).GetProperty("sessione").GetString()!;
}
