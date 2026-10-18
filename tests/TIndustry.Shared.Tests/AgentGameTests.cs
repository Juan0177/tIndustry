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

    /// <summary>
    /// A journal's sandbox (and the agents' real game, made the same way) is the client's Nuova partita: the
    /// arguments below are World.NewSandboxFactory's (Core 2x2 at the centre, 180 money, biome map, day).
    /// </summary>
    [Fact]
    public void SandboxSetupIsTheClientsNewGame()
    {
        var setup = AgentGameSetup.Sandbox(64, 7, MapGenerator.Name, new GameRules()).CreateHost(Content);
        var client = new SimHost(NewGame.Sandbox(
            Content, Factory.CenteredCoreOrigin(64, 64), coreSize: 2, startingMoney: 180,
            rules: new GameRules(Night: false), map: MapGenSettings.Normal((MapSize)64, 7)));

        Assert.Equal(180, setup.State.Wallet.Money);
        Assert.Equal(client.Checksum(), setup.Checksum());
    }

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
        var corner = host.State.CoreTiles.OrderBy(t => t.Y).ThenBy(t => t.X).First();
        Assert.Equal(corner.X, stato.GetProperty("core").GetProperty("cella").GetProperty("x").GetInt32());
        Assert.Equal(corner.Y, stato.GetProperty("core").GetProperty("cella").GetProperty("y").GetInt32());
        Assert.Equal(host.State.Terrain!.Width, stato.GetProperty("mappa").GetProperty("larghezza").GetInt32());
        Assert.Equal(host.State.Terrain.Height, stato.GetProperty("mappa").GetProperty("altezza").GetInt32());

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
        Assert.Equal("sud", Call(game, "ruota").GetProperty("direzione").GetString());
        Call(game, "piazza", """{"tier":1,"da":{"x":40,"y":53},"a":{"x":40,"y":55}}""");
        Call(game, "piazza", """{"tier":1,"da":{"x":44,"y":53},"a":{"x":46,"y":53},"direzione":"ovest"}""");
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
        var setup = AgentGameSetup.Sandbox(120, 42, ClassicTerrainGenerator.Name, new GameRules(InstantBuild: true));
        var host = setup.CreateHost(Content);
        var dir = Path.Combine(Path.GetTempPath(), "tindustry-agenti-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        using var game = new AgentGame(host, Content, setup, journalDirectory: dir);
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
            ("piazza", """{"tipo":"conveyor-basic","cella":{"x":59,"y":59}}"""),
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

        // Bad parameters change nothing and stay out of the journal.
        Refused(game, "piazza", """{"tipo":"conveyor-basic","cella":{"x":-5,"y":3}}""");
        Assert.Equal(20, game.Journal.Entries.Count);
        Assert.NotEqual(setup.CreateHost(Content).Checksum(), host.Checksum());
        Assert.Contains(Call(game, "diario").GetProperty("voci").EnumerateArray(), v => v.GetProperty("metodo").GetString() == "smonta");
        try
        {
            var path = Call(game, "diario", """{"nome":"partita-1"}""").GetProperty("percorso").GetString()!;
            Assert.Equal(Path.Combine(dir, "partita-1.json"), path);

            using var replay = AgentGame.Replay(AgentJournal.Load(path), Content);

            Assert.Equal(setup, replay.Journal.Setup);
            Assert.Equal(host.State.CurrentTick, replay.Host.State.CurrentTick);
            Assert.Equal(host.Checksum(), replay.Host.Checksum());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void StatoReportsCampaignObjectives()
    {
        var catalog = CampaignCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "campaign.json"));
        var level = catalog.FirstLevel!;
        var host = new SimHost(NewGame.Campaign(
            Content, catalog, level, mapWidth: 120, mapHeight: 120, rules: new GameRules(InstantBuild: true)));
        using var game = new AgentGame(host, Content, campaign: catalog);

        var before = Call(game, "stato");
        Assert.Equal(level.Id, before.GetProperty("livelloCampagna").GetString());
        var sell = before.GetProperty("obiettivi").EnumerateArray().First(o => o.GetProperty("tipo").GetString() == "vendi");
        Assert.Equal(0, sell.GetProperty("attuale").GetInt32());
        Assert.False(sell.GetProperty("fatto").GetBoolean());
        Assert.False(before.GetProperty("livelloCompletato").GetBoolean());

        Call(game, "vendi", $$"""{"oggetto":"{{sell.GetProperty("oggetto").GetString()}}","quantita":1}""");

        var after = Call(game, "stato").GetProperty("obiettivi").EnumerateArray()
            .First(o => o.GetProperty("tipo").GetString() == "vendi");
        Assert.Equal(1, after.GetProperty("attuale").GetInt32());
        Assert.Equal(sell.GetProperty("richiesto").GetInt32(), after.GetProperty("richiesto").GetInt32());
        using var sandbox = new AgentGame(NewHost(), Content, campaign: catalog);
        Assert.False(Call(sandbox, "stato").TryGetProperty("obiettivi", out _));
    }

    [Fact]
    public void PiazzaLineReportsPartialRefusals()
    {
        using var game = new AgentGame(NewHost(), Content);

        // The Core stands on x 60..61: those two cells are refused, the rest is built.
        var result = Call(game, "piazza", """{"tipo":"conveyor-basic","da":{"x":56,"y":60},"a":{"x":63,"y":60}}""");

        Assert.Equal(6, result.GetProperty("celle").GetArrayLength());
        var refused = result.GetProperty("rifiuti").EnumerateArray().ToArray();
        Assert.Equal([60, 61], refused.Select(r => r.GetProperty("x").GetInt32()));
        Assert.All(refused, r => Assert.Equal("OnCore", r.GetProperty("codice").GetString()));
        Assert.Equal("est", result.GetProperty("celle")[0].GetProperty("direzione").GetString());
    }

    [Fact]
    public void FarOrHugeCoordinatesAreBadParameters()
    {
        using var game = new AgentGame(NewHost(), Content);

        Assert.Equal(AgentCodes.ParametriNonValidi, Refused(game, "piazza",
            """{"tipo":"conveyor-basic","da":{"x":0,"y":0},"a":{"x":2000000000,"y":0}}""").Code);
        Assert.Equal(AgentCodes.ParametriNonValidi, Refused(game, "piazza",
            """{"tipo":"conveyor-basic","da":{"x":0,"y":0},"a":{"x":-2147483648,"y":0}}""").Code);
        Assert.Equal(AgentCodes.ParametriNonValidi, Refused(game, "piazza",
            """{"tipo":"conveyor-basic","da":{"x":0,"y":0},"a":{"x":119,"y":119}}""").Code);
        Assert.Equal(AgentCodes.ParametriNonValidi, Refused(game, "trova",
            """{"cosa":"iron-ore","cella":{"x":-2147483648,"y":0}}""").Code);
        Assert.Equal(AgentCodes.ParametriNonValidi, Refused(game, "smonta", """{"cella":{"x":500,"y":5}}""").Code);
        Assert.Equal(0, game.Host.State.CurrentTick);
        Assert.Empty(game.Journal.Entries);
    }

    [Fact]
    public void UnexpectedExceptionsBecomeErroreInterno()
    {
        var host = NewHost();
        using var game = new AgentGame(host, Content);
        host.CommandApplied += _ => throw new InvalidOperationException("gestore rotto");

        var response = game.Handle(AgentProtocol.ReadLine(
            """{"id":3,"metodo":"piazza","parametri":{"tipo":"conveyor-basic","cella":{"x":50,"y":50}}}"""));

        Assert.False(response.Ok);
        Assert.Equal(3, response.Id);
        Assert.Equal(AgentCodes.ErroreInterno, response.Code);
        Assert.Contains("gestore rotto", response.Message);
        Assert.DoesNotContain(" at ", response.Message);
        Assert.Contains("gestore rotto", game.LastInternalError);
        Assert.Contains(" at ", game.LastInternalError);
        // The command was applied and took its tick, so it is journaled.
        Assert.Single(game.Journal.Entries);
        Assert.True(game.Handle(AgentProtocol.ReadLine("""{"id":4,"metodo":"stato"}""")).Ok);
    }

    [Fact]
    public void DiarioWritesOnlyInsideItsFolder()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tindustry-agenti-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var noFolder = new AgentGame(NewHost(), Content);
            Assert.Equal(AgentCodes.ParametriNonValidi, Refused(noFolder, "diario", """{"nome":"a"}""").Code);

            using var game = new AgentGame(NewHost(), Content, journalDirectory: dir);
            foreach (var name in new[] { "../fuori", "..", "a/b", "a\\b", "C:\\x", "/x", ".nascosto", "", "partita\n", "CON", "nul.json", "Com1", "lpt9.json", "aux" })
            {
                Assert.Equal(AgentCodes.ParametriNonValidi,
                    Refused(game, "diario", JsonSerializer.Serialize(new { nome = name })).Code);
            }

            Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(dir)!, "fuori*"));
            Assert.Empty(Directory.GetFiles(dir));

            var broken = Path.Combine(dir, "rotto.json");
            File.WriteAllText(broken, "{non json");
            Assert.Equal(AgentCodes.ParametriNonValidi, Assert.Throws<AgentException>(() => AgentJournal.Load(broken)).Code);
            File.WriteAllText(broken, """{"versione":99,"voci":[]}""");
            Assert.Equal(AgentCodes.ParametriNonValidi, Assert.Throws<AgentException>(() => AgentJournal.Load(broken)).Code);
            Assert.Equal(AgentCodes.ParametriNonValidi,
                Assert.Throws<AgentException>(() => AgentJournal.Load(Path.Combine(dir, "manca.json"))).Code);
            // A journal without its game cannot be replayed from the header.
            Assert.Equal(AgentCodes.ParametriNonValidi,
                Assert.Throws<AgentException>(() => AgentGame.Replay(new AgentJournal(), Content)).Code);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void EventiKeepsTheNewestAndCountsTheRest()
    {
        using var game = new AgentGame(NewHost(), Content);
        for (var i = 0; i < 600; i++)
        {
            Refused(game, "vendi", """{"oggetto":"iron-ore","quantita":1000}""");
        }

        var result = Call(game, "eventi");

        var events = result.GetProperty("eventi").EnumerateArray().ToArray();
        Assert.Equal(AgentGame.MaxListItems, events.Length);
        Assert.Equal(400, result.GetProperty("troncato").GetInt32());
        // The journal's convention: the tick when the command was sent (it applied in tick 600).
        Assert.Equal(599, events[^1].GetProperty("tick").GetInt64());
        Assert.Equal("vendi", events[0].GetProperty("comando").GetString());
        Assert.False(Call(game, "eventi").TryGetProperty("troncato", out _));
    }

    [Fact]
    public void AreaShowsTerrainAndBeltShapes()
    {
        var host = new SimHost(NewGame.Sandbox(
            Content, new GridPosition(500, 500), rules: new GameRules(InstantBuild: true),
            map: MapGenSettings.Normal(MapSize.Media, 1)));
        using var game = new AgentGame(host, Content);
        var t = host.State.Terrain!;
        char At(GridPosition c) =>
            Call(game, "area", $$$"""{"da":{"x":{{{c.X}}},"y":{{{c.Y}}}},"a":{"x":{{{c.X}}},"y":{{{c.Y}}}}}""")
                .GetProperty("righe")[0].GetString()![0];
        GridPosition First(Func<TerrainTile, bool> match)
        {
            for (var y = 0; y < t.Height; y++)
            {
                for (var x = 0; x < t.Width; x++)
                {
                    if (match(t[x, y]))
                    {
                        return new GridPosition(x, y);
                    }
                }
            }

            throw new InvalidOperationException("Cella non trovata.");
        }

        Assert.Equal('#', At(First(c => c.Wall != WallKind.None)));
        Assert.Equal('~', At(First(c => c.Wall == WallKind.None && c.Floor == FloorKind.DeepWater)));
        Assert.Equal('i', At(First(c => c.Wall == WallKind.None && c.Floor != FloorKind.DeepWater && c.Deposit == DepositKind.Iron)));
        var byKind = Call(game, "trova", """{"cosa":"Iron","cella":{"x":500,"y":500},"max":1}""").GetProperty("risultati")[0];
        Assert.Equal(DepositKind.Iron, t[byKind.GetProperty("x").GetInt32(), byKind.GetProperty("y").GetInt32()].Deposit);

        // An L of belts on the cleared starting ground by the Core: east along y 496, then south.
        Call(game, "piazza", """{"tipo":"conveyor-basic","da":{"x":494,"y":496},"a":{"x":496,"y":496}}""");
        Call(game, "piazza", """{"tipo":"conveyor-basic","da":{"x":497,"y":496},"a":{"x":497,"y":498},"direzione":"sud"}""");
        var area = Call(game, "area", """{"da":{"x":494,"y":496},"a":{"x":497,"y":498}}""");
        var shapes = area.GetProperty("forme").EnumerateArray()
            .ToDictionary(s => (s.GetProperty("x").GetInt32(), s.GetProperty("y").GetInt32()));
        // Straight belts are the default and are left out: only the corner is listed.
        var corner = Assert.Single(shapes).Value;
        Assert.Equal((497, 496), (corner.GetProperty("x").GetInt32(), corner.GetProperty("y").GetInt32()));
        Assert.Equal("curva", corner.GetProperty("forma").GetString());
        Assert.Equal("est", corner.GetProperty("ingresso").GetString());
        Assert.Equal(">>>v", area.GetProperty("righe")[0].GetString());
        Assert.False(area.TryGetProperty("oggetti", out _));
    }

    [Fact]
    public void AreaCountsTheItemsOnBelts()
    {
        var factory = NewGame.Sandbox(Content, CoreOrigin, mapWidth: 120, mapHeight: 120, rules: new GameRules(InstantBuild: true));
        for (var x = 40; x <= 42; x++)
        {
            Assert.True(factory.PlaceInstant(new BuildSpec(BuildSpec.DefaultConveyorId, null, new GridPosition(x, 50), Direction.East), charge: false));
        }

        Assert.True(factory.Belts.TryInsert(new GridPosition(41, 50), new TransportedItem(1, "iron-ore")));
        using var game = new AgentGame(new SimHost(factory), Content);

        var area = Call(game, "area", """{"da":{"x":40,"y":50},"a":{"x":42,"y":50},"oggetti":true}""");

        var item = Assert.Single(area.GetProperty("oggetti").EnumerateArray());
        Assert.Equal((41, 1, "iron-ore"), (item.GetProperty("x").GetInt32(), item.GetProperty("n").GetInt32(), item.GetProperty("tipi")[0].GetString()));
    }

    [Fact]
    public void EveryBuildingKindHasALegendSymbol()
    {
        var symbols = AgentGame.Legend.Select(l => l.Symbol).ToHashSet();
        foreach (var kind in Enum.GetValues<BuildingKind>())
        {
            var symbol = AgentGame.KindSymbol(kind);
            Assert.NotEqual('!', symbol);
            Assert.Contains(symbol, symbols);
        }
    }

    [Fact]
    public void WithoutTickPerCommandTheOutcomeComesAfterTheNextTick()
    {
        var host = NewHost();
        using var game = new AgentGame(host, Content, tickPerComando: false);

        var waiting = Call(game, "piazza", """{"tipo":"conveyor-basic","cella":{"x":50,"y":50}}""");

        Assert.True(waiting.GetProperty("inAttesa").GetBoolean());
        Assert.Equal(1, waiting.GetProperty("tick").GetInt64());
        Assert.Equal(0, host.State.CurrentTick);
        Assert.False(game.TryFinishPending(out _));
        host.AdvanceTicks(1);
        Assert.True(game.TryFinishPending(out var done));
        Assert.Equal(1, done.GetProperty("celle").GetArrayLength());

        Call(game, "piazza", """{"tipo":"conveyor-basic","cella":{"x":50,"y":50}}""");
        // A second command while the first waits is refused, not swapped in.
        Assert.Equal(AgentCodes.ParametriNonValidi,
            Refused(game, "piazza", """{"tipo":"conveyor-basic","cella":{"x":51,"y":50}}""").Code);
        host.AdvanceTicks(1);
        Assert.Equal("Occupied", Assert.Throws<AgentException>(() => game.TryFinishPending(out _)).Code);
        Assert.False(host.State.Belts.ContainsKey(new GridPosition(51, 50)));
    }

    [Fact]
    public void ReplayKeepsTheRecordedTickPerCommand()
    {
        var setup = AgentGameSetup.Sandbox(120, 42, ClassicTerrainGenerator.Name, new GameRules(InstantBuild: true));
        var host = setup.CreateHost(Content);
        using var game = new AgentGame(host, Content, setup, tickPerComando: false);
        Call(game, "piazza", """{"tipo":"conveyor-basic","da":{"x":40,"y":50},"a":{"x":44,"y":50}}""");
        Call(game, "avanza", """{"tick":10}""");
        Assert.True(game.TryFinishPending(out _));
        Call(game, "ruota");
        Call(game, "piazza", """{"tipo":"conveyor-basic","da":{"x":45,"y":50},"a":{"x":45,"y":53}}""");
        Call(game, "avanza", """{"tick":5}""");
        Assert.False(game.Journal.Setup!.TickPerCommand);

        using var replay = AgentGame.Replay(game.Journal, Content);

        Assert.Equal(15, replay.Host.State.CurrentTick);
        Assert.Equal(host.State.CurrentTick, replay.Host.State.CurrentTick);
        Assert.Equal(host.Checksum(), replay.Host.Checksum());
        Assert.Equal(Direction.South, replay.Host.State.Belts[new GridPosition(45, 53)].Direction);
    }

    /// <summary>
    /// The real game's time goes on after the last command (esegui, real time): the journal keeps the tick it was saved
    /// at, and rigioca runs up to it, so tick and fingerprint match.
    /// </summary>
    [Fact]
    public void ReplayRunsUpToTheJournalsEnd()
    {
        var setup = AgentGameSetup.Sandbox(120, 42, ClassicTerrainGenerator.Name, new GameRules(InstantBuild: true));
        var host = setup.CreateHost(Content);
        using var game = new AgentGame(host, Content, setup, tickPerComando: false, journalDirectory: TempFolder());
        Call(game, "piazza", """{"tipo":"conveyor-basic","da":{"x":40,"y":50},"a":{"x":44,"y":50}}""");
        host.AdvanceTicks(1);
        Assert.True(game.TryFinishPending(out _));
        host.AdvanceTicks(600); // esegui 10: not a journal entry
        var path = Call(game, "diario", """{"nome":"coda"}""").GetProperty("percorso").GetString()!;

        using var replay = AgentGame.Replay(AgentJournal.Load(path), Content);

        Assert.Equal(601, replay.Host.State.CurrentTick);
        Assert.Equal(host.Checksum(), replay.Host.Checksum());
        Assert.Equal(Call(game, "stato").GetProperty("impronta").GetString(), Call(replay, "stato").GetProperty("impronta").GetString());
        Assert.Empty(replay.ReplayWarnings);
    }

    [Fact]
    public void ReplaySkipsWhatOnlyTheRealGameDidAndSaysSo()
    {
        var setup = AgentGameSetup.Sandbox(120, 42, ClassicTerrainGenerator.Name, new GameRules(InstantBuild: true));
        using var game = new AgentGame(setup.CreateHost(Content), Content, setup);
        game.Journal.Add(0, "input", JsonDocument.Parse("""{"tipo":"tasto","tasto":"R"}""").RootElement, replayable: false);
        game.Journal.Warnings.Add("prova");
        Call(game, "avanza", """{"tick":3}""");
        var listed = Call(game, "diario");
        Assert.True(listed.GetProperty("rigiocabile").GetBoolean());
        Assert.Equal(3, listed.GetProperty("fine").GetInt64());

        using var replay = AgentGame.Replay(game.Journal, Content);

        Assert.Equal(3, replay.Host.State.CurrentTick);
        Assert.Equal(2, replay.ReplayWarnings.Count);
        Assert.Contains(replay.ReplayWarnings, w => w.Contains("'input'", StringComparison.Ordinal));
    }

    /// <summary>The real game's brush is the player's: stato shows it, ruota turns it, a line journals the direction it used.</summary>
    [Fact]
    public void TheClientsBrushIsReadAndTurned()
    {
        var direction = Direction.North;
        using var game = new AgentGame(NewHost(), Content)
        {
            ReadBrush = () => direction,
            RotateBrush = () => direction = DirectionMath.Right(direction)
        };

        Assert.Equal("nord", Call(game, "stato").GetProperty("direzione").GetString());
        Assert.Equal("est", Call(game, "ruota").GetProperty("direzione").GetString());
        Assert.Equal(Direction.East, direction);
        Call(game, "piazza", """{"tipo":"conveyor-basic","cella":{"x":50,"y":50}}""");

        Assert.Equal("est", game.Journal.Entries[^1].Params.GetProperty("direzione").GetString());
    }

    [Fact]
    public void CatalogoListsStructuresAndRefusalsSayWhatIsMissing()
    {
        using var game = new AgentGame(NewHost(), Content);

        var junction = Call(game, "catalogo").GetProperty("strutture").EnumerateArray()
            .Single(s => s.GetProperty("id").GetString() == "junction");
        Assert.False(junction.GetProperty("sbloccato").GetBoolean());
        Assert.Equal(200, junction.GetProperty("sblocco").GetProperty("soldi").GetInt32());
        Assert.Equal("conveyor-basic", junction.GetProperty("prerequisiti")[0].GetString());

        // 180 money of the 200 the junction's unlock costs.
        var unaffordable = Refused(game, "sblocca", """{"struttura":"junction"}""");
        Assert.Equal(nameof(RefusalCode.UnlockUnaffordable), unaffordable.Code);
        Assert.Equal(20, (int)unaffordable.Missing!["soldi"]!);
        var locked = Refused(game, "piazza", """{"tipo":"junction","cella":{"x":50,"y":50}}""");
        Assert.Equal(nameof(RefusalCode.Locked), locked.Code);
        Assert.Equal(20, (int)locked.Missing!["soldi"]!);
        Assert.Contains("\"mancano\":{\"soldi\":20", AgentProtocol.WriteLine(AgentResponse.Failure(1, locked)));
    }

    [Fact]
    public void EventiSaysWhatWasBuilt()
    {
        var host = new SimHost(NewGame.Sandbox(Content, CoreOrigin, mapWidth: 120, mapHeight: 120));
        using var game = new AgentGame(host, Content);
        Call(game, "piazza", """{"tipo":"conveyor-basic","cella":{"x":58,"y":61}}""");
        Call(game, "avanza", """{"tick":3000}""");

        var built = Call(game, "eventi").GetProperty("eventi").EnumerateArray().First(e => e.GetProperty("tipo").GetString() == "costruito");

        Assert.Equal("conveyor-basic", built.GetProperty("struttura").GetString());
        Assert.Equal(58, built.GetProperty("cella").GetProperty("x").GetInt32());
    }

    private static string TempFolder() => Directory.CreateTempSubdirectory("tindustry-agentgame-").FullName;

    [Fact]
    public void DisposeStopsListening()
    {
        var host = NewHost();
        var game = new AgentGame(host, Content);
        game.Dispose();

        host.Submit(new Sell(0, "iron-ore", 1000));
        host.AdvanceTicks(1);

        Assert.Empty(Call(game, "eventi").GetProperty("eventi").EnumerateArray());
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
