using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Ctrl during a belt drag (<see cref="LineShape.Diagonal"/>): the existing chain between anchor and cursor, else an A*
/// path on the four sides, else the straight line.
/// </summary>
public sealed class DiagonalLineTests
{
    private const string Belt = BuildSpec.DefaultConveyorId;
    private const string Fast = "conveyor-fast";
    private const int Size = 40;

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    /// <summary>40×40 grass with <paramref name="walls"/>, the Core at (30,30).</summary>
    private static Factory NewFactory(params (int X, int Y)[] walls)
    {
        var factory = NewGame.Sandbox(Content, new GridPosition(30, 30), mapWidth: Size, mapHeight: Size,
            startingMoney: 1000, rules: new GameRules(InstantBuild: true));
        factory.EnsureDemoBuildStock();
        var tiles = new TerrainTile[Size, Size];
        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                tiles[x, y] = new TerrainTile(FloorKind.Grass, WallKind.None, DepositKind.None);
            }
        }

        foreach (var (x, y) in walls)
        {
            tiles[x, y] = new TerrainTile(FloorKind.Stone, WallKind.StoneWall, DepositKind.None);
        }

        factory.Terrain = TerrainMap.FromTiles(tiles);
        return factory;
    }

    private static IReadOnlyList<LinePlacement> Ctrl(
        IGameView view, (int X, int Y) anchor, (int X, int Y) cursor, string id = Belt, Direction? fixedDirection = null) =>
        PlacementLine.Plan(view, new LineRequest(
            0, id, new GridPosition(anchor.X, anchor.Y), new GridPosition(cursor.X, cursor.Y), fixedDirection ?? Direction.East,
            FixedDirection: fixedDirection is not null, Shape: LineShape.Diagonal));

    /// <summary>Each cell is beside the one before and faces the next; the path ends on the cursor.</summary>
    private static void AssertConnected(IReadOnlyList<LinePlacement> line, (int X, int Y) cursor)
    {
        Assert.Equal(new GridPosition(cursor.X, cursor.Y), line[^1].Cell);
        for (var i = 0; i < line.Count - 1; i++)
        {
            Assert.Equal(line[i + 1].Cell, line[i].Cell.Step(line[i].Direction));
        }
    }

    private static int Turns(IReadOnlyList<LinePlacement> line) =>
        Enumerable.Range(1, line.Count - 2).Count(i => line[i].Direction != line[i - 1].Direction);

    [Fact]
    public void CtrlPathIsFourConnected()
    {
        var line = Ctrl(NewFactory(), (5, 5), (9, 8));

        Assert.Equal(new GridPosition(5, 5), line[0].Cell);
        Assert.Equal(8, line.Count);
        AssertConnected(line, (9, 8));
        Assert.Equal(1, Turns(line));
        Assert.All(line, c => Assert.Null(c.Refusal));
        Assert.All(line, c => Assert.IsType<PlaceBelt>(c.Command));
    }

    [Fact]
    public void CtrlGoesAroundAWall()
    {
        (int, int)[] walls = [.. Enumerable.Range(2, 7).Select(y => (8, y))];
        var factory = NewFactory(walls);

        var line = Ctrl(factory, (5, 5), (11, 5));

        AssertConnected(line, (11, 5));
        Assert.All(line, c => Assert.Null(c.Refusal));
        Assert.DoesNotContain(line, c => walls.Contains((c.Cell.X, c.Cell.Y)));
        Assert.DoesNotContain(line, c => c.Spanned || c.Command is PlaceBuilding);
    }

    [Fact]
    public void CtrlFallsBackToTheStraightLine()
    {
        var factory = NewFactory((9, 7), (11, 7), (10, 6), (10, 8));

        var line = Ctrl(factory, (5, 5), (10, 7));

        Assert.Equal(PlacementLine.Straight(new GridPosition(5, 5), new GridPosition(10, 7)), line.Select(c => c.Cell));
        Assert.All(line, c => Assert.Equal(Direction.East, c.Direction));
    }

    [Fact]
    public void CtrlPathCrossesBeltsOnStraightStretches()
    {
        var factory = NewFactory();
        for (var y = 2; y <= 8; y++)
        {
            Assert.True(factory.PlaceInstant(new BuildSpec(Belt, null, new GridPosition(7, y), Direction.South), charge: false));
        }

        var line = Ctrl(factory, (5, 5), (10, 5));

        Assert.Equal(Enumerable.Range(5, 6).Select(x => new GridPosition(x, 5)), line.Select(c => c.Cell));
        Assert.True(((PlaceBelt)line[2].Command!).Crossing);
    }

    /// <summary>Chain (5,5)E (6,5)E (7,5)N (7,4)N (7,3)N of plain belts, fast belts researched.</summary>
    private static Factory WithChain()
    {
        var factory = NewFactory();
        factory.Research.ForceUnlock(Fast);
        foreach (var (x, y, direction) in Chain)
        {
            Assert.True(factory.PlaceInstant(new BuildSpec(Belt, null, new GridPosition(x, y), direction), charge: false));
        }

        return factory;
    }

    private static readonly (int X, int Y, Direction Direction)[] Chain =
    [
        (5, 5, Direction.East), (6, 5, Direction.East), (7, 5, Direction.North), (7, 4, Direction.North), (7, 3, Direction.North)
    ];

    private static IEnumerable<PlaceBelt> FastChain() =>
        Chain.Select(c => new PlaceBelt(0, new GridPosition(c.X, c.Y), c.Direction, Fast));

    [Fact]
    public void CtrlFollowsAChainToUpgradeIt()
    {
        var factory = WithChain();
        Assert.True(factory.Belts.TryInsert(new GridPosition(6, 5), new TransportedItem(1, "iron-ore")));
        var host = new SimHost(factory);
        var refused = new List<Refusal>();
        host.CommandRejected += (_, r) => refused.Add(r);

        var line = Ctrl(factory, (5, 5), (7, 3), Fast);

        Assert.Equal(FastChain(), line.Select(c => c.Command));
        Assert.All(line, c => Assert.Null(c.Refusal));
        foreach (var command in PlacementLine.Commands(line))
        {
            host.Submit(command);
        }

        host.AdvanceTicks(1);
        Assert.Empty(refused);
        foreach (var (x, y, direction) in Chain)
        {
            var cell = factory.Belts.Cells[new GridPosition(x, y)];
            Assert.Equal(Fast, cell.Definition.Id);
            Assert.Equal(direction, cell.Direction);
        }

        Assert.Contains(factory.Belts.Cells.Values, c => c.Items.Any(i => i.Id == 1));
    }

    [Fact]
    public void CtrlChainWorksFromTheDownstreamEnd()
    {
        var line = Ctrl(WithChain(), (7, 3), (5, 5), Fast);

        Assert.Equal(FastChain().Reverse(), line.Select(c => c.Command));
    }

    [Fact]
    public void CtrlPathIgnoresTheFixedDirection()
    {
        // R during a Ctrl drag does not turn the path's belts all one way; it still fixes the straight fallback.
        var factory = NewFactory((9, 7), (11, 7), (10, 6), (10, 8));

        Assert.Equal(Ctrl(factory, (5, 5), (9, 8)), Ctrl(factory, (5, 5), (9, 8), fixedDirection: Direction.North));
        Assert.All(Ctrl(factory, (5, 5), (10, 7), fixedDirection: Direction.North), c => Assert.Equal(Direction.North, c.Direction));
    }

    [Fact]
    public void CtrlFallsBackWhenTheSearchIsTooLong()
    {
        // A wall on x 20 open only at y 39: the way round exists (under 200 cells) but costs more than MaxSearchNodes.
        var factory = NewFactory([.. Enumerable.Range(0, Size - 1).Select(y => (20, y))]);

        var line = Ctrl(factory, (10, 5), (30, 5));

        Assert.Equal(PlacementLine.Straight(new GridPosition(10, 5), new GridPosition(30, 5)), line.Select(c => c.Cell));
        Assert.Equal(RefusalCode.Wall, line.Single(c => c.Cell == new GridPosition(20, 5)).Refusal);
        Assert.Equal(1000, PlacementLine.MaxSearchNodes);
    }

    [Fact]
    public void CtrlTiesAlwaysPickTheSameSide()
    {
        // One wall on (8,5) between anchor and cursor: going round above or below costs the same.
        var factory = NewFactory((8, 5));

        var first = Ctrl(factory, (5, 5), (11, 5));
        var again = Ctrl(NewFactory((8, 5)), (5, 5), (11, 5));

        Assert.Equal(first, again);
        AssertConnected(first, (11, 5));
        Assert.All(first, c => Assert.True(c.Cell.Y <= 5));
    }

    [Fact]
    public void CtrlOnABeltLoopEnds()
    {
        var factory = NewFactory();
        foreach (var (x, y, direction) in new[] { (5, 5, Direction.East), (6, 5, Direction.South), (6, 6, Direction.West), (5, 6, Direction.North) })
        {
            Assert.True(factory.PlaceInstant(new BuildSpec(Belt, null, new GridPosition(x, y), direction), charge: false));
        }

        // Off the loop: the walk round it stops (MaxCells) and the A* path is used; on it: the chain.
        AssertConnected(Ctrl(factory, (5, 5), (9, 9)), (9, 9));
        Assert.Equal([new GridPosition(5, 5), new GridPosition(6, 5), new GridPosition(6, 6)], Ctrl(factory, (5, 5), (6, 6)).Select(c => c.Cell));
    }
}
