using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>The agents' game methods on a simulation-only <see cref="SimHost"/>, and their journal.</summary>
public sealed class AgentGameTests
{
    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly GridPosition CoreOrigin = new(60, 60);

    private static SimHost NewHost() =>
        new(NewGame.Sandbox(
            Content, CoreOrigin, mapWidth: 120, mapHeight: 120, rules: new GameRules(InstantBuild: true)));

    private static JsonElement Call(AgentGame game, string method, string json = "{}") =>
        game.Call(method, JsonDocument.Parse(json).RootElement);

    private static AgentException Refused(AgentGame game, string method, string json) =>
        Assert.Throws<AgentException>(() => Call(game, method, json));

    /// <summary>First wall cell (row by row) whose 2x2 miner block fits inside the map.</summary>
    private static GridPosition WallBlock(IGameView view)
    {
        var t = view.Terrain!;
        for (var y = 0; y < t.Height - 1; y++)
        {
            for (var x = 0; x < t.Width - 1; x++)
            {
                if (t[x, y].Wall != WallKind.None)
                {
                    return new GridPosition(x, y);
                }
            }
        }

        throw new InvalidOperationException("Nessuna parete sulla mappa di prova.");
    }

    [Fact]
    public void StatoReportsCoreAndMoney()
    {
        var host = NewHost();
        var game = new AgentGame(host, Content);

        var stato = Call(game, "stato");

        Assert.Equal(0, stato.GetProperty("tick").GetInt64());
        Assert.Equal(180, stato.GetProperty("soldi").GetInt32());
        Assert.Equal(48, stato.GetProperty("risorse").GetProperty("iron-plate").GetInt32());
        Assert.Equal(10, stato.GetProperty("risorse").GetProperty("copper-wire").GetInt32());
        var energia = stato.GetProperty("energia");
        Assert.Equal(host.State.PowerCapacity, energia.GetProperty("capacita").GetSingle());
        Assert.True(energia.TryGetProperty("generazionePerSecondo", out _));
        Assert.Equal(0, stato.GetProperty("edifici").GetProperty("nastri").GetInt32());

        Call(game, "piazza", """{"tipo":"conveyor-basic","cella":{"x":50,"y":62}}""");
        stato = Call(game, "stato");
        Assert.Equal(1, stato.GetProperty("tick").GetInt64());
        Assert.Equal(host.State.Wallet.Money, stato.GetProperty("soldi").GetInt32());
        Assert.Equal(47, stato.GetProperty("risorse").GetProperty("iron-plate").GetInt32());
        Assert.Equal(1, stato.GetProperty("edifici").GetProperty("nastri").GetInt32());
        Assert.Equal(0, stato.GetProperty("edifici").GetProperty("minatori").GetInt32());
        Assert.Contains("miner", stato.GetProperty("ricerca").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void PiazzaUsesSimHostAndReportsRefusalCode()
    {
        // The classic generator has no walls: the biome one does.
        var host = new SimHost(NewGame.Sandbox(
            Content,
            new GridPosition(500, 500),
            rules: new GameRules(InstantBuild: true),
            map: MapGenSettings.Normal(MapSize.Media, 1)));
        var game = new AgentGame(host, Content);
        var applied = new List<SimCommand>();
        var rejected = new List<SimCommand>();
        host.CommandApplied += applied.Add;
        host.CommandRejected += (c, _) => rejected.Add(c);
        var wall = WallBlock(host.State);

        var error = Refused(game, "piazza", $$$"""{"tipo":"miner","cella":{"x":{{{wall.X}}},"y":{{{wall.Y}}}}}""");

        Assert.Equal("Wall", error.Code);
        var command = Assert.IsType<PlaceBuilding>(Assert.Single(rejected));
        Assert.Equal(new PlaceBuilding(0, BuildingKind.Miner, wall, Direction.East) { Tick = 1 }, command);
        Assert.Empty(applied);
        Assert.Empty(host.State.Miners);
    }

    [Fact]
    public void PiazzaLineDragsLikeThePlayer()
    {
        var host = NewHost();
        var game = new AgentGame(host, Content);
        // Client drag (40,50) -> (43,51): the pressed cell, then World.DragCells' 4-connected steps.
        GridPosition[] expected = [new(40, 50), new(41, 50), new(42, 50), new(42, 51), new(43, 51)];

        var result = Call(game, "piazza", """{"tipo":"conveyor-basic","da":{"x":40,"y":50},"a":{"x":43,"y":51}}""");

        var placed = result.GetProperty("celle").EnumerateArray()
            .Select(c => new GridPosition(c.GetProperty("x").GetInt32(), c.GetProperty("y").GetInt32()))
            .ToArray();
        Assert.Equal(expected, placed);
        foreach (var cell in expected)
        {
            // Every cell takes the brush direction, as the client does today.
            Assert.Equal(Direction.East, host.State.Belts[cell].Direction);
        }

        Assert.Equal(expected.Skip(1), DragLine.Cells(expected[0], expected[^1]));

        // `ruota` turns the brush like the player's R; `direzione` overrides it for one call.
        Assert.Equal("South", Call(game, "ruota").GetProperty("direzione").GetString());
        Call(game, "piazza", """{"tier":1,"da":{"x":40,"y":53},"a":{"x":40,"y":55}}""");
        Call(game, "piazza", """{"tier":1,"da":{"x":44,"y":53},"a":{"x":46,"y":53},"direzione":"West"}""");
        Assert.Equal("conveyor-basic", host.State.Belts[new GridPosition(40, 55)].Definition.Id);
        Assert.Equal(Direction.South, host.State.Belts[new GridPosition(40, 54)].Direction);
        Assert.Equal(Direction.West, host.State.Belts[new GridPosition(46, 53)].Direction);
    }

    [Fact]
    public void AreaIsCompactAndBounded()
    {
        var host = NewHost();
        var game = new AgentGame(host, Content);
        Call(game, "piazza", """{"tipo":"conveyor-basic","cella":{"x":58,"y":60}}""");

        var area = Call(game, "area", """{"da":{"x":56,"y":58},"a":{"x":63,"y":63}}""");

        var rows = area.GetProperty("righe").EnumerateArray().Select(r => r.GetString()!).ToArray();
        Assert.Equal(6, rows.Length);
        Assert.All(rows, r => Assert.Equal(8, r.Length));
        Assert.Equal('>', rows[2][2]);
        Assert.Equal('C', rows[2][4]);
        var legenda = area.GetProperty("legenda");
        Assert.True(legenda.TryGetProperty(">", out _));
        Assert.True(legenda.TryGetProperty("C", out _));

        Assert.Equal(AgentCodes.ParametriNonValidi,
            Refused(game, "area", """{"da":{"x":0,"y":0},"a":{"x":64,"y":64}}""").Code);
        Assert.Equal(AgentCodes.ParametriNonValidi,
            Refused(game, "area", """{"da":{"x":-1,"y":0},"a":{"x":5,"y":5}}""").Code);
        Assert.Equal(AgentCodes.ParametriNonValidi,
            Refused(game, "area", """{"da":{"x":110,"y":110},"a":{"x":125,"y":115}}""").Code);
        Assert.Equal(AgentCodes.ParametriNonValidi,
            Refused(game, "area", """{"da":{"x":0,"y":0},"a":{"x":999,"y":999}}""").Code);
        Assert.Equal(AgentCodes.ParametriNonValidi, Refused(game, "area", """{"da":"no"}""").Code);
    }

    [Fact]
    public void TrovaReturnsNearestDeposits()
    {
        var host = NewHost();
        var game = new AgentGame(host, Content);
        var terrain = host.State.Terrain!;
        var from = new GridPosition(30, 30);
        var nearest = int.MaxValue;
        for (var y = 0; y < terrain.Height; y++)
        {
            for (var x = 0; x < terrain.Width; x++)
            {
                if (terrain[x, y].Deposit == DepositKind.Iron)
                {
                    nearest = Math.Min(nearest, Math.Abs(x - from.X) + Math.Abs(y - from.Y));
                }
            }
        }

        var found = Call(game, "trova", """{"cosa":"iron-ore","cella":{"x":30,"y":30},"max":5}""");

        var cells = found.GetProperty("risultati").EnumerateArray().ToArray();
        Assert.Equal(5, cells.Length);
        Assert.Equal(nearest, cells[0].GetProperty("distanza").GetInt32());
        var distances = cells.Select(c => c.GetProperty("distanza").GetInt32()).ToArray();
        Assert.Equal(distances.Order(), distances);
        Assert.All(cells, c => Assert.Equal(
            DepositKind.Iron,
            terrain[c.GetProperty("x").GetInt32(), c.GetProperty("y").GetInt32()].Deposit));

        Call(game, "piazza", """{"tipo":"conveyor-basic","cella":{"x":50,"y":62}}""");
        var belts = Call(game, "trova", """{"cosa":"conveyor-basic","cella":{"x":0,"y":0}}""");
        Assert.Equal(50, Assert.Single(belts.GetProperty("risultati").EnumerateArray()).GetProperty("x").GetInt32());
        Assert.Equal(AgentCodes.ParametriNonValidi, Refused(game, "trova", """{"cosa":"pizza","cella":{"x":0,"y":0}}""").Code);
    }

    [Fact]
    public void EventiReturnsOnlyNewEvents()
    {
        var host = NewHost();
        var game = new AgentGame(host, Content);
        Refused(game, "vendi", """{"oggetto":"iron-ore","quantita":1000}""");

        var first = Call(game, "eventi").GetProperty("eventi").EnumerateArray().ToArray();
        Assert.Equal("SellFailed", Assert.Single(first).GetProperty("codice").GetString());
        Assert.Empty(Call(game, "eventi").GetProperty("eventi").EnumerateArray());

        Refused(game, "sblocca", """{"struttura":"miner"}""");
        var second = Call(game, "eventi").GetProperty("eventi").EnumerateArray().ToArray();
        Assert.Equal("AlreadyUnlocked", Assert.Single(second).GetProperty("codice").GetString());
        Assert.Equal("AlreadyUnlocked",
            Call(game, "stato").GetProperty("rifiuti").EnumerateArray().Last().GetProperty("codice").GetString());
    }

    [Fact]
    public void AvanzaRunsExactTicks()
    {
        var host = NewHost();
        var game = new AgentGame(host, Content);

        var result = Call(game, "avanza", """{"tick":37}""");

        Assert.Equal(0, result.GetProperty("tickPrima").GetInt64());
        Assert.Equal(37, result.GetProperty("tick").GetInt64());
        Assert.Equal(37, host.State.CurrentTick);
        Assert.Equal(AgentCodes.ParametriNonValidi, Refused(game, "avanza", """{"tick":-1}""").Code);
        Assert.Equal(AgentCodes.ParametriNonValidi, Refused(game, "avanza", "{}").Code);
        Assert.Equal(AgentCodes.ParametriNonValidi, Refused(game, "metodo-inventato", "{}").Code);
    }

    [Fact]
    public void ReplayGivesSameChecksum()
    {
        var host = NewHost();
        var game = new AgentGame(host, Content);
        var actions = new List<(string Method, string Json)>
        {
            ("piazza", """{"tipo":"miner","cella":{"x":52,"y":60}}"""),
            ("piazza", """{"tipo":"conveyor-basic","da":{"x":54,"y":60},"a":{"x":59,"y":60}}"""),
            ("avanza", """{"tick":120}"""),
            ("ruota", "{}"),
            ("piazza", """{"tipo":"conveyor-basic","da":{"x":40,"y":40},"a":{"x":40,"y":44}}"""),
            ("smonta", """{"cella":{"x":40,"y":42}}"""),
            ("vendi", """{"oggetto":"iron-plate","quantita":2}"""),
            ("vendi", """{"oggetto":"iron-ore","quantita":999}"""),
            ("sblocca", """{"struttura":"smelter"}"""),
            ("avanza", """{"tick":300}"""),
            ("potenzia_core", "{}"),
            ("ruota", "{}"),
            ("piazza", """{"tipo":"junction","cella":{"x":45,"y":45}}"""),
            ("smonta", """{"cella":{"x":45,"y":45}}"""),
            ("smonta", """{"cella":{"x":45,"y":45}}"""),
            ("avanza", """{"tick":60}"""),
            ("piazza", """{"tipo":"conveyor-basic","cella":{"x":-5,"y":3}}"""),
            ("piazza", """{"tier":3,"da":{"x":30,"y":50},"a":{"x":33,"y":53}}"""),
            ("vendi", """{"oggetto":"copper-wire","quantita":1}"""),
            ("avanza", """{"tick":240}""")
        };
        foreach (var (method, json) in actions)
        {
            try
            {
                Call(game, method, json);
            }
            catch (AgentException)
            {
                // Refusals are part of the run: they are journaled and replayed too.
            }
        }

        Assert.Equal(20, game.Journal.Entries.Count);
        Assert.NotEqual(NewHost().Checksum(), host.Checksum());
        Assert.Contains(Call(game, "diario").GetProperty("voci").EnumerateArray(), v => v.GetProperty("metodo").GetString() == "smonta");
        var dir = Path.Combine(Path.GetTempPath(), "tindustry-agenti-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "diario.json");
            Call(game, "diario", JsonSerializer.Serialize(new { percorso = path }));

            var replayHost = NewHost();
            AgentGame.Replay(AgentJournal.Load(path), replayHost);

            Assert.Equal(host.State.CurrentTick, replayHost.State.CurrentTick);
            Assert.Equal(host.Checksum(), replayHost.Checksum());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ProtocolLinesRoundTrip()
    {
        var request = AgentProtocol.ReadLine("""{"id":7,"metodo":"stato"}""");
        Assert.Equal(7, request.Id);
        Assert.Equal("stato", request.Method);
        Assert.Equal(JsonValueKind.Object, request.Params.ValueKind);

        var ok = AgentProtocol.WriteLine(AgentResponse.Success(7, JsonDocument.Parse("""{"tick":3}""").RootElement));
        Assert.Equal("""{"id":7,"ok":true,"risultato":{"tick":3}}""", ok);
        var error = AgentProtocol.WriteLine(
            AgentResponse.Failure(8, new AgentException(AgentCodes.ParametriNonValidi, "x")));
        Assert.Equal("""{"id":8,"ok":false,"codice":"ParametriNonValidi","messaggio":"x"}""", error);
        Assert.Equal(AgentCodes.ParametriNonValidi, Assert.Throws<AgentException>(() => AgentProtocol.ReadLine("{non json")).Code);
    }
}
