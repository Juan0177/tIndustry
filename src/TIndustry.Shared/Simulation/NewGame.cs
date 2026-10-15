namespace TIndustry.Shared;

/// <summary>Builders for the starting <see cref="Factory"/> of a new game (sandbox, campaign level, demo).</summary>
public static class NewGame
{
    /// <summary>Empty sandbox with optional seed-driven terrain (default seed 42, 1000×1000).</summary>
    public static Factory Sandbox(
        FactoryContent content,
        GridPosition coreOrigin,
        int coreSize = 2,
        int mapWidth = 1000,
        int mapHeight = 1000,
        int seed = 42,
        int startingMoney = 180)
    {
        var core = CoreStockSink.MakeCoreTiles(coreOrigin, Math.Max(1, coreSize));
        // Parity with Raylib CreateStartingWallet: plates/wire so first place isn't free-or-impossible.
        var wallet = new EconomyWallet(
            startingMoney,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["iron-plate"] = 48,
                ["copper-wire"] = 10
            });
        var session = new EconomySession(startingMoney);
        var research = ResearchState.CreateNew(content);
        var starter = Factory.ResolveStarterDeposit(coreOrigin, coreSize, mapWidth, mapHeight);
        var terrain = TerrainMap.Generate(mapWidth, mapHeight, seed, core, starter);
        return new Factory(content, new BeltGrid(), core, wallet, research, session)
        {
            Terrain = terrain
        };
    }

    /// <summary>Empty factory around fixed core with campaign wallet/session/research + seeded terrain.</summary>
    public static Factory Campaign(
        FactoryContent content,
        CampaignCatalog catalog,
        CampaignLevelDefinition level,
        GridPosition? coreOrigin = null,
        int coreSize = 2,
        int? mapWidth = null,
        int? mapHeight = null)
    {
        var (w, h) = Factory.ResolveCampaignMapSize(level);
        if (mapWidth is int mw)
        {
            w = Math.Clamp(Math.Max(12, mw), 12, Factory.MaxGodotCampaignMapEdge);
        }

        if (mapHeight is int mh)
        {
            h = Math.Clamp(Math.Max(8, mh), 8, Factory.MaxGodotCampaignMapEdge);
        }

        var origin = coreOrigin ?? Factory.CenteredCoreOrigin(w, h, coreSize);
        var core = CoreStockSink.MakeCoreTiles(origin, Math.Max(1, coreSize));
        var wallet = catalog.CreateWallet(level);
        var session = new EconomySession(level.StartingMoney);
        var research = ResearchState.CreateNew(content);
        var starter = Factory.ResolveStarterDeposit(origin, coreSize, w, h);
        var terrain = TerrainMap.Generate(w, h, level.Seed, core, starter);
        return new Factory(content, new BeltGrid(), core, wallet, research, session)
        {
            ActiveCampaignLevelId = level.Id,
            Terrain = terrain
        };
    }

    /// <summary>L-belt demo without forno (ore → core), everything unlocked.</summary>
    public static Factory Demo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var beltDef = content.RequireConveyor(beltId);
        var grid = new BeltGrid();
        grid.PlacePath(DemoLayouts.LPath(), beltDef);
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var factory = new Factory(content, grid, core);
        factory.UnlockAllForDemo();
        factory.TryPlaceMiner(new GridPosition(2, 7), Direction.East);
        return factory;
    }
}

/// <summary>Fixed belt layouts used by the demo games and tests.</summary>
public static class DemoLayouts
{
    /// <summary>Belt path of the demo: east along y=8, then south along x=10 into the core.</summary>
    public static List<GridPosition> LPath()
    {
        var path = new List<GridPosition>();
        for (var x = 4; x <= 10; x++)
        {
            path.Add(new GridPosition(x, 8));
        }

        for (var y = 9; y <= 13; y++)
        {
            path.Add(new GridPosition(10, y));
        }

        return path;
    }
}
