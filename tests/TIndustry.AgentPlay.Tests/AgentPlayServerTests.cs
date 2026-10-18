using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.AgentPlay.Tests;

public sealed class AgentPlayServerTests(ServerFixture server) : IClassFixture<ServerFixture>
{
    private static readonly string[] ToolNames =
    [
        "apri_simulazione", "apri_gioco", "collega_coop", "sessioni", "chiudi", "stato", "area", "trova", "eventi",
        "piazza", "smonta", "ruota", "sblocca", "vendi", "potenzia_core", "avanza", "esegui", "pausa", "riprendi",
        "cattura", "telecamera", "input", "ui", "log", "diario", "rigioca"
    ];

    [Fact]
    public async Task ServerListsToolsAndPlaysASimulation()
    {
        var tools = await server.Client.ListToolsAsync();
        var names = tools.Select(t => t.Name).ToList();
        Assert.Equal(ToolNames.Order(StringComparer.Ordinal), names.Order(StringComparer.Ordinal));
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
        var built = after.GetProperty("edifici").GetProperty("minatori").GetInt32()
            + after.GetProperty("costruzione").GetProperty("cantieri").GetInt32();
        Assert.Equal(1, built);
        Assert.True(after.GetProperty("tick").GetInt64() > before.GetProperty("tick").GetInt64());

        await server.Ok("chiudi", new { sessione = id });
        Assert.False(Directory.Exists(folder));
        Assert.Equal(AgentCodes.SessioneSconosciuta, await server.Refused("stato", new { sessione = id }));
        Assert.Equal(AgentCodes.SessioneSconosciuta, await server.Refused("chiudi", new { sessione = id }));
    }

    [Fact]
    public async Task CoopIsAPlaceholder()
    {
        Assert.Equal(AgentCodes.MotoreNonDisponibile, await server.Refused("collega_coop"));
        Assert.Equal(AgentCodes.MotoreNonDisponibile, await server.Refused("apri_gioco"));
    }

    [Fact]
    public async Task GameOnlyToolsNeedTheRealGame()
    {
        var id = await Open(new { misura = "64", generatore = "classico" });
        foreach (var (tool, args) in new (string, object)[]
        {
            ("esegui", new { sessione = id, secondi = 1.0 }), ("riprendi", new { sessione = id }),
            ("cattura", new { sessione = id }), ("telecamera", new { sessione = id }),
            ("input", new { sessione = id, tipo = "clic" }), ("ui", new { sessione = id }), ("log", new { sessione = id })
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

        await server.Ok("chiudi", new { sessione = id });
        Assert.False(File.Exists(path));
        await server.Ok("chiudi", new { sessione = copy });
    }

    [Fact]
    public async Task KeptSessionsLeaveTheirFolder()
    {
        var opened = await server.Ok("apri_simulazione", new { misura = "64", generatore = "classico", conserva = true });
        var folder = opened.GetProperty("cartella").GetString()!;
        await server.Ok("chiudi", new { sessione = opened.GetProperty("sessione").GetString() });
        Assert.True(Directory.Exists(folder));
    }

    private async Task<string> Open(object args) =>
        (await server.Ok("apri_simulazione", args)).GetProperty("sessione").GetString()!;
}
