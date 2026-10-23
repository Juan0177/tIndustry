using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Upgrading the Core: the next tier's block is placed over the Core with the normal <see cref="PlaceBuilding"/>
/// (foundation grows right and down from the shard, nucleus left and up from the foundation, centred on the shard).
/// Its new cells must be free and buildable; it is a site like any other (materials taken little by little, the old
/// Core keeps working, cancel gives everything back); once finished the Core has the new size, capacity and drone.
/// </summary>
public sealed class CoreUpgradeTests
{
    private const int Size = 40;
    private const int Player = 0;
    private const string Plate = "iron-plate";
    private const string Wire = "copper-wire";
    private const string Lead = "lead-plate";
    private const string Ore = "iron-ore";

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    /// <summary>The shard's top-left cell in the 40×40 test map.</summary>
    private static readonly GridPosition Origin = new(30, 30);

    private static readonly TerrainTile Grass = new(FloorKind.Grass, WallKind.None, DepositKind.None);
    private static readonly TerrainTile Deep = new(FloorKind.DeepWater, WallKind.None, DepositKind.None);
    private static readonly TerrainTile Wall = new(FloorKind.Stone, WallKind.StoneWall, DepositKind.None);

    private static long nextItemId = 7_000_000;

    /// <summary>40×40 grass, shard at <paramref name="origin"/> (default (30, 30)), the given cells changed.</summary>
    private static Factory NewFactory(
        bool instant = true, GridPosition? origin = null, params (int X, int Y, TerrainTile Tile)[] cells)
    {
        var factory = NewGame.Sandbox(
            Content, origin ?? Origin, mapWidth: Size, mapHeight: Size, rules: new GameRules(InstantBuild: instant));
        var tiles = new TerrainTile[Size, Size];
        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                tiles[x, y] = Grass;
            }
        }

        foreach (var (x, y, tile) in cells)
        {
            tiles[x, y] = tile;
        }

        factory.Terrain = TerrainMap.FromTiles(tiles);
        return factory;
    }

    /// <summary>Researches the tier's structure and puts its build cost in the Core.</summary>
    private static void Prepare(Factory factory, int tier)
    {
        var definition = Content.CoreTiers[tier];
        factory.Research.ForceUnlock(definition.Id);
        foreach (var entry in definition.BuildCost)
        {
            factory.Wallet.AddMaterial(entry.ItemId, entry.Amount);
        }
    }

    private static PlaceBuilding Upgrade(BuildingKind kind, GridPosition origin) =>
        new(Player, kind, origin, Direction.East);

    /// <summary>Submits <paramref name="command"/>, runs one tick and returns the refusal (null when applied).</summary>
    private static Refusal? Submit(SimHost host, SimCommand command)
    {
        Refusal? reason = null;
        void OnRejected(SimCommand c, Refusal r) => reason = r;
        host.CommandRejected += OnRejected;
        host.Submit(command);
        host.AdvanceTicks(1);
        host.CommandRejected -= OnRejected;
        return reason;
    }

    private static Refusal? Submit(Factory factory, SimCommand command) => Submit(new SimHost(factory), command);

    private static HashSet<GridPosition> Square(int x0, int y0, int x1, int y1)
    {
        var tiles = new HashSet<GridPosition>();
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                tiles.Add(new GridPosition(x, y));
            }
        }

        return tiles;
    }

    private static int RunUntil(SimHost host, Func<bool> done, int maxTicks = 20_000, Action? everyTick = null)
    {
        for (var tick = 1; tick <= maxTicks; tick++)
        {
            everyTick?.Invoke();
            host.AdvanceTicks(1);
            if (done())
            {
                return tick;
            }
        }

        throw new InvalidOperationException("condizione mai raggiunta");
    }

    [Fact]
    public void Upgrade_LockedUntilResearched()
    {
        var f = NewFactory();
        foreach (var entry in Content.CoreTiers[1].BuildCost)
        {
            f.Wallet.AddMaterial(entry.ItemId, entry.Amount);
        }

        Assert.Equal(RefusalCode.CoreUpgradeLocked, f.CoreUpgradeRefusal()?.Code);
        Assert.Equal(RefusalCode.CoreUpgradeLocked, Submit(f, Upgrade(BuildingKind.CoreFoundation, Origin))?.Code);
        Assert.Equal(0, f.CoreTier);
    }

    [Fact]
    public void Upgrade_Foundation_GrowsRightAndDown()
    {
        var f = NewFactory();
        Prepare(f, 1);
        var plates = f.Wallet.MaterialCount(Plate);
        var spec = f.CoreUpgradeSpec();
        Assert.Equal(new BuildSpec("core-foundation", BuildingKind.CoreFoundation, Origin, Direction.East), spec);
        Assert.Null(f.CoreUpgradeRefusal());

        Assert.Null(Submit(f, Upgrade(BuildingKind.CoreFoundation, Origin)));

        Assert.Equal(1, f.CoreTier);
        Assert.Equal(Origin, f.State.CoreOrigin);
        Assert.True(Square(30, 30, 33, 33).SetEquals(f.CoreTiles));
        Assert.Equal(4000, f.CoreCapacity(Plate));
        Assert.Equal(0.75f, f.Content.UnitForTier(f.CoreTier).BuildSpeed);
        Assert.Equal(plates - 800, f.Wallet.MaterialCount(Plate));
        Assert.Empty(f.Sites);
    }

    [Fact]
    public void Upgrade_Nucleus_GrowsLeftAndUp_CenteredOnShard()
    {
        var f = NewFactory();
        Prepare(f, 1);
        Assert.Null(Submit(f, Upgrade(BuildingKind.CoreFoundation, Origin)));
        Prepare(f, 2);
        var nucleusOrigin = new GridPosition(29, 29);
        Assert.Equal(new BuildSpec("core-nucleus", BuildingKind.CoreNucleus, nucleusOrigin, Direction.East), f.CoreUpgradeSpec());
        // Row by row: the new top row, then the new left cell of each row below.
        GridPosition[] expectedNew =
        [
            new(29, 29), new(30, 29), new(31, 29), new(32, 29), new(33, 29),
            new(29, 30), new(29, 31), new(29, 32), new(29, 33)
        ];
        Assert.Equal(expectedNew, f.CoreUpgradeNewTiles());

        Assert.Null(Submit(f, Upgrade(BuildingKind.CoreNucleus, nucleusOrigin)));

        Assert.Equal(2, f.CoreTier);
        Assert.Equal(nucleusOrigin, f.State.CoreOrigin);
        Assert.True(Square(29, 29, 33, 33).SetEquals(f.CoreTiles));
        Assert.Equal(12000, f.CoreCapacity(Plate));
        Assert.Equal(1.0f, f.Content.UnitForTier(f.CoreTier).BuildSpeed);
        // Centred on the starting shard (30..32): one cell all around.
        var (cx, cy) = f.CoreCentre();
        Assert.Equal((31.5f, 31.5f), (cx, cy));
    }

    [Fact]
    public void Upgrade_Foundation_NewTilesAreTheNewColumnAndRow()
    {
        var f = NewFactory();
        GridPosition[] expectedNew =
        [
            new(33, 30), new(33, 31), new(33, 32),
            new(30, 33), new(31, 33), new(32, 33), new(33, 33)
        ];
        Assert.Equal(expectedNew, f.CoreUpgradeNewTiles());
    }

    [Fact]
    public void Upgrade_BlockedByBelt()
    {
        var f = NewFactory();
        Prepare(f, 1);
        Assert.True(f.TryPlaceBelt(new GridPosition(33, 31), Direction.East));

        Assert.Equal(RefusalCode.CoreUpgradeBlocked, f.CoreUpgradeRefusal()?.Code);
        Assert.Equal(RefusalCode.CoreUpgradeBlocked, Submit(f, Upgrade(BuildingKind.CoreFoundation, Origin))?.Code);
        Assert.Equal(0, f.CoreTier);
    }

    [Fact]
    public void Upgrade_BlockedByBuilding()
    {
        var f = NewFactory();
        Prepare(f, 1);
        Assert.True(f.PlaceInstant(
            new BuildSpec(PowerNode.Tier1Id, BuildingKind.PowerNode, new GridPosition(31, 33), Direction.East), charge: false));

        Assert.Equal(RefusalCode.CoreUpgradeBlocked, Submit(f, Upgrade(BuildingKind.CoreFoundation, Origin))?.Code);
        Assert.Equal(0, f.CoreTier);
    }

    [Fact]
    public void Upgrade_BlockedByPlannedSite()
    {
        var f = NewFactory(instant: false);
        Prepare(f, 1);
        f.Research.ForceUnlock(PowerNode.Tier1Id);
        var host = new SimHost(f);
        Assert.Null(Submit(host, new PlaceBuilding(Player, BuildingKind.PowerNode, new GridPosition(33, 32), Direction.East)));

        Assert.Equal(RefusalCode.CoreUpgradeBlocked, Submit(host, Upgrade(BuildingKind.CoreFoundation, Origin))?.Code);
    }

    [Fact]
    public void Upgrade_BlockedByDeconstructingSite()
    {
        var f = NewFactory(instant: false);
        Prepare(f, 1);
        var node = new GridPosition(33, 30);
        Assert.True(f.PlaceInstant(new BuildSpec(PowerNode.Tier1Id, BuildingKind.PowerNode, node, Direction.East), charge: false));
        var site = Construction.BeginDeconstruct(f.State, node, Player);
        Assert.True(site.Deconstructing);

        Assert.Equal(RefusalCode.CoreUpgradeBlocked, Submit(f, Upgrade(BuildingKind.CoreFoundation, Origin))?.Code);
    }

    [Fact]
    public void Upgrade_BlockedByWall()
    {
        var f = NewFactory(cells: (33, 30, Wall));
        Prepare(f, 1);

        Assert.Equal(RefusalCode.CoreUpgradeBlocked, Submit(f, Upgrade(BuildingKind.CoreFoundation, Origin))?.Code);
    }

    [Fact]
    public void Upgrade_BlockedByDeepWater()
    {
        var f = NewFactory(cells: (32, 33, Deep));
        Prepare(f, 1);

        Assert.Equal(RefusalCode.CoreUpgradeBlocked, Submit(f, Upgrade(BuildingKind.CoreFoundation, Origin))?.Code);
    }

    [Fact]
    public void Upgrade_AtMapEdge()
    {
        // Shard on x 37..39: the foundation's new column (x 40) is outside the 40-wide map.
        var edge = new GridPosition(37, 30);
        var f = NewFactory(origin: edge);
        Prepare(f, 1);

        Assert.Equal(RefusalCode.CoreUpgradeOutOfMap, f.CoreUpgradeRefusal()?.Code);
        Assert.Equal(RefusalCode.CoreUpgradeOutOfMap, Submit(f, Upgrade(BuildingKind.CoreFoundation, edge))?.Code);
        Assert.Equal(0, f.CoreTier);
    }

    [Fact]
    public void Upgrade_WrongOrigin_Refused()
    {
        var f = NewFactory();
        Prepare(f, 1);

        Assert.Equal(RefusalCode.CoreUpgradeBlocked, Submit(f, Upgrade(BuildingKind.CoreFoundation, new GridPosition(31, 30)))?.Code);
        Assert.Equal(RefusalCode.CoreUpgradeBlocked, Submit(f, Upgrade(BuildingKind.CoreFoundation, new GridPosition(5, 5)))?.Code);
        Assert.Equal(0, f.CoreTier);
    }

    [Fact]
    public void Upgrade_InstantWithoutMaterials_NamesTheShortfall()
    {
        var f = NewFactory();
        f.Research.ForceUnlock("core-foundation");
        var have = f.Wallet.MaterialCount(Plate);

        var refusal = Submit(f, Upgrade(BuildingKind.CoreFoundation, Origin));

        Assert.Equal(RefusalCode.InsufficientResources, refusal?.Code);
        Assert.Equal("core-foundation", refusal?.StructureId);
        Assert.Contains(new ResourceAmount(Plate, 800 - have), refusal!.MissingItems!);
        Assert.Equal(0, f.CoreTier);
    }

    [Fact]
    public void Upgrade_SecondWhileInProgress()
    {
        var f = NewFactory(instant: false);
        Prepare(f, 1);
        var host = new SimHost(f);
        Assert.Null(Submit(host, Upgrade(BuildingKind.CoreFoundation, Origin)));

        Assert.Equal(RefusalCode.CoreUpgradeInProgress, f.CoreUpgradeRefusal()?.Code);
        Assert.Equal(RefusalCode.CoreUpgradeInProgress, Submit(host, Upgrade(BuildingKind.CoreFoundation, Origin))?.Code);
        RunUntil(host, () => f.Sites.Count > 0);
        Assert.Equal(RefusalCode.CoreUpgradeInProgress, Submit(host, Upgrade(BuildingKind.CoreFoundation, Origin))?.Code);
        Assert.Single(f.Sites);
    }

    [Fact]
    public void Upgrade_AfterNucleus()
    {
        var f = NewFactory();
        Prepare(f, 1);
        Assert.Null(Submit(f, Upgrade(BuildingKind.CoreFoundation, Origin)));
        Prepare(f, 2);
        Assert.Null(Submit(f, Upgrade(BuildingKind.CoreNucleus, new GridPosition(29, 29))));

        Assert.Null(f.CoreUpgradeSpec());
        Assert.Empty(f.CoreUpgradeNewTiles());
        Assert.Equal(RefusalCode.CoreMaxTier, f.CoreUpgradeRefusal()?.Code);
        Assert.Equal(RefusalCode.CoreMaxTier, Submit(f, Upgrade(BuildingKind.CoreNucleus, new GridPosition(29, 29)))?.Code);
        Assert.Equal(RefusalCode.CoreMaxTier, Submit(f, Upgrade(BuildingKind.CoreFoundation, new GridPosition(29, 29)))?.Code);
        Assert.Equal(2, f.CoreTier);
    }

    [Fact]
    public void Upgrade_Site_UsesMaterials_OldCoreWorks()
    {
        var f = NewFactory(instant: false);
        Prepare(f, 1);
        var before = (f.Wallet.MaterialCount(Plate), f.Wallet.MaterialCount(Wire), f.Wallet.MaterialCount(Lead));
        // A belt line into the shard's west side, fed with ore all along the build.
        var line = Enumerable.Range(24, 6).Select(x => new GridPosition(x, 31)).ToArray();
        foreach (var cell in line)
        {
            Assert.True(f.TryPlaceBelt(cell, Direction.East));
        }

        void Feed() => f.Belts.TryInsert(line[0], new TransportedItem(Interlocked.Increment(ref nextItemId), Ore), Direction.East);

        var host = new SimHost(f);
        Assert.Null(Submit(host, Upgrade(BuildingKind.CoreFoundation, Origin)));
        RunUntil(host, () => f.Sites.Count > 0 && f.Sites[0].Progress > 0.2f, everyTick: Feed);

        var site = Assert.Single(f.Sites);
        Assert.Equal("core-foundation", site.Spec.StructureId);
        Assert.Equal(f.CoreUpgradeNewTiles(), site.Tiles);
        Assert.Equal(0, f.CoreTier);
        Assert.Equal(9, f.CoreTiles.Count);
        var delivered = f.CoreDeliveredItems;
        var ore = f.Wallet.MaterialCount(Ore);
        host.AdvanceTicks(120);
        Assert.True(f.CoreDeliveredItems > delivered, "the old Core still takes belts");
        Assert.True(f.Wallet.MaterialCount(Ore) > ore);
        Assert.True(f.Wallet.MaterialCount(Plate) < before.Item1, "materials go little by little");
        Assert.True(f.Wallet.MaterialCount(Plate) > before.Item1 - 800);

        RunUntil(host, () => f.CoreTier == 1, everyTick: Feed);

        Assert.Empty(f.Sites);
        Assert.Equal(16, f.CoreTiles.Count);
        Assert.Equal((before.Item1 - 800, before.Item2 - 500, before.Item3 - 400),
            (f.Wallet.MaterialCount(Plate), f.Wallet.MaterialCount(Wire), f.Wallet.MaterialCount(Lead)));
        Assert.Empty(f.State.Builders[0].Plans);
    }

    [Fact]
    public void Upgrade_CancelRefundsAll()
    {
        var f = NewFactory(instant: false);
        Prepare(f, 1);
        var before = (f.Wallet.MaterialCount(Plate), f.Wallet.MaterialCount(Wire), f.Wallet.MaterialCount(Lead));
        var host = new SimHost(f);
        Assert.Null(Submit(host, Upgrade(BuildingKind.CoreFoundation, Origin)));
        RunUntil(host, () => f.Sites.Count > 0 && f.Sites[0].Progress > 0.3f);
        Assert.True(f.Wallet.MaterialCount(Plate) < before.Item1);

        Assert.Null(Submit(host, new Remove(Player, new GridPosition(33, 33))));
        RunUntil(host, () => f.Sites.Count == 0);

        Assert.Equal(before, (f.Wallet.MaterialCount(Plate), f.Wallet.MaterialCount(Wire), f.Wallet.MaterialCount(Lead)));
        Assert.Equal(0, f.CoreTier);
        Assert.Equal(9, f.CoreTiles.Count);
        Assert.Null(f.CoreUpgradeRefusal());
    }

    [Fact]
    public void Upgrade_SiteSurvivesSaveAndLoad()
    {
        // A generated map (a hand-made terrain cannot be regenerated on load).
        var origin = Factory.CenteredCoreOrigin(64, 64);
        var f = NewGame.Sandbox(Content, origin, map: MapGenSettings.Normal((MapSize)64, 7));
        Prepare(f, 1);
        var host = new SimHost(f);
        Assert.Null(Submit(host, Upgrade(BuildingKind.CoreFoundation, origin)));
        RunUntil(host, () => f.Sites.Count > 0 && f.Sites[0].Progress > 0.1f);

        var restored = new SimHost(Factory.Restore(Content, f.Capture()));
        Assert.Equal(host.Checksum(), restored.Checksum());
        var site = Assert.Single(restored.State.Sites);
        Assert.Equal(BuildingKind.CoreFoundation, site.Spec.Kind);
        Assert.Equal(f.CoreUpgradeNewTiles(), site.Tiles);
        host.AdvanceTicks(300);
        restored.AdvanceTicks(300);
        Assert.Equal(host.Checksum(), restored.Checksum());
    }

    [Fact]
    public void Upgrade_BeltTouchingNewCells_Delivers()
    {
        var f = NewFactory();
        Prepare(f, 1);
        // Points west at (33, 31), the foundation's new column: a dead end beside nothing while the Core is a shard.
        var belt = new GridPosition(34, 31);
        Assert.True(f.TryPlaceBelt(belt, Direction.West));
        Assert.True(f.Belts.TryInsert(belt, new TransportedItem(Interlocked.Increment(ref nextItemId), Ore), Direction.West));
        var ore = f.Wallet.MaterialCount(Ore);
        f.RunTicks(120);
        Assert.Equal(ore, f.Wallet.MaterialCount(Ore));
        Assert.Single(f.Belts.Cells[belt].Items);

        Assert.Null(Submit(f, Upgrade(BuildingKind.CoreFoundation, Origin)));
        f.RunTicks(120);

        Assert.Equal(ore + 1, f.Wallet.MaterialCount(Ore));
        Assert.Empty(f.Belts.Cells[belt].Items);
    }

    [Theory]
    [InlineData(MapGenerator.Name, 1)]
    [InlineData(MapGenerator.Name, 2)]
    [InlineData(ClassicTerrainGenerator.Name, 1)]
    [InlineData(ClassicTerrainGenerator.Name, 2)]
    public void UpgradedCore_SaveLoad_RegeneratesTheSameTerrain(string generator, int tier)
    {
        // The terrain is regenerated from the starting shard's cells (saved as CoreStartX/Y), not from the grown Core's.
        var origin = Factory.CenteredCoreOrigin(64, 64);
        var f = generator == MapGenerator.Name
            ? NewGame.Sandbox(Content, origin, rules: new GameRules(InstantBuild: true), map: MapGenSettings.Normal((MapSize)64, 7))
            : NewGame.Sandbox(Content, origin, mapWidth: 64, mapHeight: 64, seed: 7, rules: new GameRules(InstantBuild: true));
        var host = new SimHost(f);
        Prepare(f, 1);
        Assert.Null(Submit(host, Upgrade(BuildingKind.CoreFoundation, origin)));
        if (tier == 2)
        {
            Prepare(f, 2);
            var nucleus = f.CoreUpgradeSpec()!;
            Assert.Null(Submit(host, Upgrade(BuildingKind.CoreNucleus, nucleus.Origin)));
        }

        Assert.Equal(tier, f.CoreTier);
        var data = f.Capture();
        Assert.Equal((origin.X, origin.Y), (data.CoreStartX, data.CoreStartY));
        Assert.Equal(Content.CoreTiers[tier].Size, data.CoreSize);

        var restored = SimHost.FromSnapshot(Content, host.CreateSnapshot());

        Assert.Equal(host.Checksum(), restored.Checksum());
        Assert.Equal(f.Terrain!.Hash, restored.State.Terrain!.Hash);
        Assert.Equal(tier, restored.State.CoreTier);
        Assert.True(f.CoreTiles.SetEquals(restored.State.CoreTiles));
        Assert.Equal(origin, ((Factory)restored.State).State.CoreStart);
    }

    [Fact]
    public void SavedCoreStart_IsWhatTheTerrainIsRegeneratedFrom()
    {
        // The start is read from the save, never derived from the Core: another start makes another terrain.
        var origin = Factory.CenteredCoreOrigin(64, 64);
        var f = NewGame.Sandbox(Content, origin, mapWidth: 64, mapHeight: 64, seed: 7, rules: new GameRules(InstantBuild: true));
        Prepare(f, 1);
        Assert.Null(Submit(f, Upgrade(BuildingKind.CoreFoundation, origin)));
        var data = f.Capture();
        data.CoreStartX += 5;

        var ex = Assert.Throws<SaveMapException>(() => Factory.Restore(Content, data));
        Assert.Equal(SaveLoadError.TerrainChanged, ex.Code);
    }
}
