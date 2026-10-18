namespace TIndustry.Shared;

/// <summary>
/// Builders for the starting <see cref="Factory"/> of a new game (sandbox, campaign level, demo).
/// Each takes optional <see cref="GameRules"/>: the refund share comes from the mode (sandbox 100,
/// campaign = the level's), while the caller's <see cref="GameRules.InstantBuild"/> wins over the
/// mode's default (false, demo true). Every new game has player 0's drone at the centre of the Core.
/// </summary>
public static class NewGame
{
    /// <summary>
    /// Empty sandbox on seeded terrain: the classic generator (default seed 42, 1000×1000), or the biome
    /// generator when <paramref name="map"/> is given (its size and seed then replace the other arguments).
    /// </summary>
    public static Factory Sandbox(
        FactoryContent content,
        GridPosition coreOrigin,
        int coreSize = 2,
        int mapWidth = 1000,
        int mapHeight = 1000,
        int seed = 42,
        int startingMoney = 180,
        GameRules? rules = null,
        MapGenSettings? map = null)
    {
        if (map is not null)
        {
            mapWidth = map.Width;
            mapHeight = map.Height;
        }

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
        var terrain = map is null
            ? TerrainMap.Generate(mapWidth, mapHeight, seed, core, starter)
            : MapGenerator.Generate(map, core, starter);
        var factory = new Factory(content, new BeltGrid(), core, wallet, research, session)
        {
            Terrain = terrain
        };
        factory.State.Rules = new GameRules(RefundPercent: 100, InstantBuild: rules?.InstantBuild ?? false);
        factory.AddBuilder(0);
        return factory;
    }

    /// <summary>Empty factory around fixed core with campaign wallet/session/research + seeded terrain.</summary>
    public static Factory Campaign(
        FactoryContent content,
        CampaignCatalog catalog,
        CampaignLevelDefinition level,
        GridPosition? coreOrigin = null,
        int coreSize = 2,
        int? mapWidth = null,
        int? mapHeight = null,
        GameRules? rules = null)
    {
        var (w, h) = Factory.ResolveCampaignMapSize(level);
        if (mapWidth is int mw)
        {
            w = Math.Clamp(Math.Max(12, mw), 12, Factory.MaxMapEdge);
        }

        if (mapHeight is int mh)
        {
            h = Math.Clamp(Math.Max(8, mh), 8, Factory.MaxMapEdge);
        }

        var origin = coreOrigin ?? Factory.CenteredCoreOrigin(w, h, coreSize);
        var core = CoreStockSink.MakeCoreTiles(origin, Math.Max(1, coreSize));
        var wallet = catalog.CreateWallet(level);
        var session = new EconomySession(level.StartingMoney);
        var research = ResearchState.CreateNew(content);
        var starter = Factory.ResolveStarterDeposit(origin, coreSize, w, h);
        var terrain = TerrainMap.Generate(w, h, level.Seed, core, starter);
        // L01 teach: seed a bit of ore so Mercato sell objectives are reachable quickly.
        if (level.Id.Contains("primi-passi", StringComparison.Ordinal))
        {
            wallet.AddMaterial("iron-ore", 8);
        }

        var factory = new Factory(content, new BeltGrid(), core, wallet, research, session)
        {
            ActiveCampaignLevelId = level.Id,
            Terrain = terrain
        };
        factory.State.Rules = new GameRules(
            RefundPercent: Math.Clamp(level.RefundPercent, 0, 100),
            InstantBuild: rules?.InstantBuild ?? false);
        factory.AddBuilder(0);
        return factory;
    }

    /// <summary>L-belt demo without forno (ore → core), everything unlocked.</summary>
    public static Factory Demo(FactoryContent content, string beltId = "conveyor-basic", GameRules? rules = null)
    {
        var beltDef = content.RequireConveyor(beltId);
        var grid = new BeltGrid();
        grid.PlacePath(DemoLayouts.LPath(), beltDef);
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var factory = new Factory(content, grid, core);
        factory.State.Rules = new GameRules(RefundPercent: 100, InstantBuild: rules?.InstantBuild ?? true);
        factory.UnlockAllForDemo();
        factory.TryPlaceMiner(new GridPosition(2, 7), Direction.East);
        // After the miner, so the demo's building ids stay as before.
        factory.AddBuilder(0);
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
