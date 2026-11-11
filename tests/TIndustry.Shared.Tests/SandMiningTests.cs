using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Miners dig sand from sand floor cells when no diggable deposit lies under them.</summary>
public sealed class SandMiningTests
{
    private const int Size = 40;

    private static readonly string ContentPath = Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    private static readonly FactoryContent Content = FactoryContent.Load(ContentPath);

    private static Factory Sandbox(
        (int X, int Y, FloorKind Floor, DepositKind Deposit)[] cells)
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(30, 30), mapWidth: Size, mapHeight: Size,
            rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        var tiles = new TerrainTile[Size, Size];
        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                tiles[x, y] = new TerrainTile(FloorKind.Stone, WallKind.None, DepositKind.None);
            }
        }

        foreach (var (x, y, floor, deposit) in cells)
        {
            tiles[x, y] = new TerrainTile(floor, WallKind.None, deposit);
        }

        factory.Terrain = TerrainMap.FromTiles(tiles);
        return factory;
    }

    private static (int, int, FloorKind, DepositKind)[] Block(
        int x, int y, FloorKind floor, DepositKind deposit, int count = 4)
    {
        (int, int)[] offsets = [(0, 0), (1, 0), (0, 1), (1, 1)];
        return offsets.Take(count).Select(o => (x + o.Item1, y + o.Item2, floor, deposit)).ToArray();
    }

    [Fact]
    public void MinerOnSandMinesSand()
    {
        var factory = Sandbox(Block(10, 10, FloorKind.Sand, DepositKind.None));

        Assert.True(factory.TryPlaceMiner(new GridPosition(10, 10), Direction.East));
        var miner = Assert.Single(factory.Miners);
        Assert.Equal("sand", miner.OutputItemId);
        Assert.Equal(4, miner.CoveredDepositTiles);
        Assert.Equal(1f, miner.Efficiency, 3);
    }

    [Fact]
    public void DarkSandCountsAsSand()
    {
        var factory = Sandbox(Block(10, 10, FloorKind.DarkSand, DepositKind.None));

        Assert.True(factory.TryPlaceMiner(new GridPosition(10, 10), Direction.East));
        Assert.Equal("sand", Assert.Single(factory.Miners).OutputItemId);
    }

    [Fact]
    public void DepositBeatsSand()
    {
        var factory = Sandbox([
            (10, 10, FloorKind.Sand, DepositKind.Iron), (11, 10, FloorKind.Sand, DepositKind.Iron),
            (10, 11, FloorKind.Sand, DepositKind.None), (11, 11, FloorKind.Sand, DepositKind.None)]);

        Assert.True(factory.TryPlaceMiner(new GridPosition(10, 10), Direction.East));
        var miner = Assert.Single(factory.Miners);
        Assert.Equal("iron-ore", miner.OutputItemId);
        Assert.Equal(2, miner.CoveredDepositTiles);
    }

    [Fact]
    public void HalfSandHalfStone()
    {
        var factory = Sandbox(Block(10, 10, FloorKind.Sand, DepositKind.None, count: 2));

        Assert.True(factory.TryPlaceMiner(new GridPosition(10, 10), Direction.East));
        var miner = Assert.Single(factory.Miners);
        Assert.Equal("sand", miner.OutputItemId);
        Assert.Equal(2, miner.CoveredDepositTiles);
        Assert.Equal(0.5f, miner.Efficiency, 3);
    }

    [Fact]
    public void T1OnTitaniumAndSandDigsSand()
    {
        var factory = Sandbox([
            (10, 10, FloorKind.Stone, DepositKind.Titanium), (11, 10, FloorKind.Stone, DepositKind.Titanium),
            (10, 11, FloorKind.Sand, DepositKind.None), (11, 11, FloorKind.Sand, DepositKind.None)]);

        Assert.True(factory.CanPlace(
            new BuildSpec(Miner.BasicId, BuildingKind.Miner, new GridPosition(10, 10), Direction.East), out _));
        Assert.True(factory.TryPlaceMiner(new GridPosition(10, 10), Direction.East));
        var miner = Assert.Single(factory.Miners);
        Assert.Equal("sand", miner.OutputItemId);
        Assert.Equal(2, miner.CoveredDepositTiles);
    }

    [Fact]
    public void SandOnTitaniumCellsOnlyStillRefusedForT1()
    {
        // Titanium deposit on a sand floor: the deposit wins and the cell is not sand.
        var factory = Sandbox(Block(10, 10, FloorKind.Sand, DepositKind.Titanium));

        Assert.False(factory.CanPlace(
            new BuildSpec(Miner.BasicId, BuildingKind.Miner, new GridPosition(10, 10), Direction.East), out var reason));
        Assert.Equal(RefusalCode.NeedsAdvancedMiner, reason);
    }

    [Fact]
    public void NoSandOnClassicGenerator()
    {
        HashSet<GridPosition> core = [new(100, 100), new(101, 100), new(100, 101), new(101, 101)];
        var map = ClassicTerrainGenerator.Generate(200, 200, 42, core, new GridPosition(96, 100));

        for (var x = 0; x < map.Width; x++)
        {
            for (var y = 0; y < map.Height; y++)
            {
                Assert.False(TerrainMap.IsSandFloor(map[x, y].Floor), $"sand floor at {x},{y}");
            }
        }
    }
}
