using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The automatic bridge of a straight belt line (<see cref="PlacementLine"/>): a run of walls, deep water or buildings
/// the bridge can span is jumped over from the free cell before it to the first cell after it.
/// </summary>
public sealed class AutoBridgeTests
{
    private const string Belt = BuildSpec.DefaultConveyorId;
    private const int Size = 40;

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly string BridgeId = BuildSpec.StructureIdFor(BuildingKind.Bridge)!;

    /// <summary>40×40 grass, the Core at (30,30), the bridge researched unless <paramref name="bridge"/> is false.</summary>
    private static Factory NewFactory(
        IEnumerable<(int X, int Y)>? walls = null, IEnumerable<(int X, int Y)>? deepWater = null, bool bridge = true)
    {
        var factory = NewGame.Sandbox(Content, new GridPosition(30, 30), mapWidth: Size, mapHeight: Size,
            startingMoney: 1000, rules: new GameRules(InstantBuild: true));
        var tiles = new TerrainTile[Size, Size];
        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                tiles[x, y] = new TerrainTile(FloorKind.Grass, WallKind.None, DepositKind.None);
            }
        }

        foreach (var (x, y) in walls ?? [])
        {
            tiles[x, y] = new TerrainTile(FloorKind.Stone, WallKind.StoneWall, DepositKind.None);
        }

        foreach (var (x, y) in deepWater ?? [])
        {
            tiles[x, y] = new TerrainTile(FloorKind.DeepWater, WallKind.None, DepositKind.None);
        }

        factory.Terrain = TerrainMap.FromTiles(tiles);
        if (bridge)
        {
            factory.Research.ForceUnlock(BridgeId);
        }

        return factory;
    }

    private static IReadOnlyList<LinePlacement> EastLine(IGameView view, int fromX, int toX, bool fixedNorth = false) =>
        PlacementLine.Plan(view, new LineRequest(
            0, Belt, new GridPosition(fromX, 10), new GridPosition(toX, 10),
            fixedNorth ? Direction.North : Direction.East, FixedDirection: fixedNorth));

    private static LinePlacement At(IReadOnlyList<LinePlacement> line, int x) => line.Single(c => c.Cell == new GridPosition(x, 10));

    /// <summary>(entry) a bridge East, then the spanned cells, then belts East up to <paramref name="toX"/>.</summary>
    private static void AssertBridged(IReadOnlyList<LinePlacement> line, int entry, int exit, int toX)
    {
        Assert.Equal(new PlaceBuilding(0, BuildingKind.Bridge, new GridPosition(entry, 10), Direction.East), At(line, entry).Command);
        Assert.Null(At(line, entry).Refusal);
        for (var x = entry + 1; x <= exit; x++)
        {
            Assert.True(At(line, x).Spanned);
            Assert.Null(At(line, x).Command);
            Assert.Null(At(line, x).Refusal);
        }

        for (var x = exit + 1; x <= toX; x++)
        {
            Assert.Equal(new PlaceBelt(0, new GridPosition(x, 10), Direction.East, Belt), At(line, x).Command);
            Assert.Null(At(line, x).Refusal);
        }
    }

    [Fact]
    public void BridgeOverAWall()
    {
        var factory = NewFactory(walls: [(8, 10)]);

        var line = EastLine(factory, 5, 12);

        Assert.Equal(Enumerable.Range(5, 8).Select(x => new GridPosition(x, 10)), line.Select(c => c.Cell));
        AssertBridged(line, 7, 9, 12);
        Assert.All(line.Take(2), c => Assert.IsType<PlaceBelt>(c.Command));

        var host = new SimHost(factory);
        var refused = new List<Refusal>();
        host.CommandRejected += (_, r) => refused.Add(r);
        foreach (var command in PlacementLine.Commands(line))
        {
            host.Submit(command);
        }

        host.AdvanceTicks(1);
        Assert.Empty(refused);
        Assert.Equal(new GridPosition(9, 10), factory.Belts.Cells[new GridPosition(7, 10)].BridgePartner);

        Assert.True(factory.Belts.TryInsert(new GridPosition(5, 10), new TransportedItem(1, "iron-ore")));
        factory.RunSeconds(20);
        Assert.Contains(factory.Belts.Cells[new GridPosition(12, 10)].Items, i => i.Id == 1);
    }

    [Fact]
    public void BridgeCostComesFromCostOf()
    {
        var factory = NewFactory(walls: [(8, 10)]);
        var commands = PlacementLine.Commands(EastLine(factory, 5, 12));
        var expected = commands
            .Select(c => c is PlaceBelt b ? BuildSpec.FromCommand(b) : BuildSpec.FromCommand((PlaceBuilding)c))
            .SelectMany(factory.CostOf)
            .GroupBy(e => e.ItemId ?? "")
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));
        var money = factory.Wallet.Money;
        var materials = factory.Wallet.MaterialsSnapshot().ToDictionary(kv => kv.Key, kv => kv.Value);

        var host = new SimHost(factory);
        foreach (var command in commands)
        {
            host.Submit(command);
        }

        host.AdvanceTicks(1);

        Assert.Equal(expected.GetValueOrDefault(""), money - factory.Wallet.Money);
        foreach (var (item, amount) in expected.Where(kv => kv.Key != ""))
        {
            Assert.Equal(amount, materials[item] - factory.Wallet.MaterialCount(item));
        }
    }

    [Fact]
    public void BridgeOverABuilding()
    {
        var factory = NewFactory();
        Assert.True(factory.PlaceInstant(new BuildSpec("miner", BuildingKind.Miner, new GridPosition(8, 10), Direction.East), charge: false));

        AssertBridged(EastLine(factory, 5, 12), 7, 10, 12);
    }

    [Fact]
    public void BridgeOverDeepWater()
    {
        var factory = NewFactory(deepWater: [(8, 10), (9, 10)]);

        AssertBridged(EastLine(factory, 5, 12), 7, 10, 12);
    }

    [Fact]
    public void ObstacleLongerThanTheSpanStaysRefused()
    {
        var factory = NewFactory(walls: Enumerable.Range(8, 5).Select(x => (x, 10)));

        var line = EastLine(factory, 5, 15);

        Assert.All(Enumerable.Range(8, 5), x => Assert.Equal(RefusalCode.Wall, At(line, x).Refusal));
        Assert.DoesNotContain(line, c => c.Spanned || c.Command is PlaceBuilding);
    }

    [Fact]
    public void NoBridgeWhenLockedOrSideways()
    {
        var locked = EastLine(NewFactory(walls: [(8, 10)], bridge: false), 5, 12);
        var sideways = EastLine(NewFactory(walls: [(8, 10)]), 5, 12, fixedNorth: true);

        foreach (var line in new[] { locked, sideways })
        {
            Assert.Equal(RefusalCode.Wall, At(line, 8).Refusal);
            Assert.DoesNotContain(line, c => c.Spanned || c.Command is PlaceBuilding);
        }
    }

    [Fact]
    public void NoBridgeOverTheCoreOrOffTheMap()
    {
        // The Core (30..31, 30..31) is no obstacle a bridge spans; nor is the map's edge.
        var factory = NewFactory();
        var overCore = PlacementLine.Plan(factory, new LineRequest(0, Belt, new GridPosition(27, 30), new GridPosition(35, 30), Direction.East));
        var offMap = PlacementLine.Plan(factory, new LineRequest(0, Belt, new GridPosition(35, 10), new GridPosition(42, 10), Direction.East));

        Assert.DoesNotContain(overCore, c => c.Spanned || c.Command is PlaceBuilding);
        Assert.DoesNotContain(offMap, c => c.Spanned || c.Command is PlaceBuilding);
    }

    [Fact]
    public void PerpendicularBeltStillGetsTheJunction()
    {
        var factory = NewFactory();
        factory.Research.ForceUnlock("junction");
        for (var y = 7; y <= 13; y++)
        {
            Assert.True(factory.PlaceInstant(new BuildSpec(Belt, null, new GridPosition(8, y), Direction.South), charge: false));
        }

        var line = EastLine(factory, 5, 12);

        Assert.True(((PlaceBelt)At(line, 8).Command!).Crossing);
        Assert.DoesNotContain(line, c => c.Spanned || c.Command is PlaceBuilding);
    }
}
