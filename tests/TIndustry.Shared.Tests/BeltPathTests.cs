using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>The run of belts a double click selects: belts only, from the first cell to the last.</summary>
public sealed class BeltPathTests
{
    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static GridPosition P(int x, int y) => new(x, y);

    private static void Put(BeltGrid grid, GridPosition at, Direction dir, string id) =>
        Assert.True(grid.TryPlaceFree(at, dir, Content.RequireConveyor(id)));

    private static void Belts(BeltGrid grid, Direction dir, string id, params GridPosition[] cells)
    {
        foreach (var c in cells)
        {
            Put(grid, c, dir, id);
        }
    }

    private static IReadOnlyList<GridPosition> Walk(BeltGrid grid, GridPosition at) => BeltPath.Walk(grid.Cells, at);

    [Fact]
    public void Straight_run_is_listed_first_to_last_from_any_cell()
    {
        var grid = new BeltGrid();
        Belts(grid, Direction.East, "conveyor-basic", P(0, 0), P(1, 0), P(2, 0), P(3, 0));
        var expected = new[] { P(0, 0), P(1, 0), P(2, 0), P(3, 0) };
        foreach (var start in expected)
        {
            Assert.Equal(expected, Walk(grid, start));
        }
    }

    [Fact]
    public void Run_with_corners_follows_the_flow_and_mixes_tiers()
    {
        var grid = new BeltGrid();
        Put(grid, P(0, 0), Direction.East, "conveyor-basic");
        Put(grid, P(1, 0), Direction.South, "conveyor-fast");
        Put(grid, P(1, 1), Direction.South, "conveyor-basic");
        Put(grid, P(1, 2), Direction.West, "conveyor-basic");
        Put(grid, P(0, 2), Direction.West, "conveyor-basic");
        var expected = new[] { P(0, 0), P(1, 0), P(1, 1), P(1, 2), P(0, 2) };
        Assert.Equal(expected, Walk(grid, P(1, 1)));
    }

    [Theory]
    [InlineData("junction")]
    [InlineData("router")]
    [InlineData("sorter")]
    [InlineData("overflow-gate")]
    [InlineData("underflow-gate")]
    [InlineData("conveyor-bridge")]
    public void Stops_at_non_belt_logistics_in_both_directions(string id)
    {
        var grid = new BeltGrid();
        Belts(grid, Direction.East, "conveyor-basic", P(0, 0), P(1, 0), P(3, 0), P(4, 0));
        Put(grid, P(2, 0), Direction.East, id);
        Assert.Equal(new[] { P(0, 0), P(1, 0) }, Walk(grid, P(0, 0)));
        Assert.Equal(new[] { P(3, 0), P(4, 0) }, Walk(grid, P(4, 0)));
        Assert.Empty(Walk(grid, P(2, 0)));
    }

    [Fact]
    public void Loop_terminates_and_lists_each_cell_once()
    {
        var grid = new BeltGrid();
        Put(grid, P(0, 0), Direction.East, "conveyor-basic");
        Put(grid, P(1, 0), Direction.South, "conveyor-basic");
        Put(grid, P(1, 1), Direction.West, "conveyor-basic");
        Put(grid, P(0, 1), Direction.North, "conveyor-basic");
        var path = Walk(grid, P(1, 0));
        Assert.Equal(4, path.Count);
        Assert.Equal(4, path.Distinct().Count());
        Assert.Contains(P(0, 0), path);
    }

    [Fact]
    public void Two_belts_feeding_the_same_cell_stop_the_walk_back_there()
    {
        var grid = new BeltGrid();
        Belts(grid, Direction.East, "conveyor-basic", P(0, 1), P(1, 1), P(2, 1));
        Put(grid, P(2, 0), Direction.South, "conveyor-basic"); // joins P(2,1) from the north
        Put(grid, P(2, 1), Direction.East, "conveyor-basic");
        Put(grid, P(3, 1), Direction.East, "conveyor-basic");
        Assert.Equal(new[] { P(2, 1), P(3, 1) }, Walk(grid, P(3, 1)));
        Assert.Equal(new[] { P(2, 1), P(3, 1) }, Walk(grid, P(2, 1)));
        Assert.Equal(new[] { P(0, 1), P(1, 1), P(2, 1), P(3, 1) }, Walk(grid, P(1, 1)));
        Assert.Equal(new[] { P(2, 0), P(2, 1), P(3, 1) }, Walk(grid, P(2, 0)));
    }

    [Fact]
    public void Start_that_is_not_a_belt_gives_an_empty_path()
    {
        var grid = new BeltGrid();
        Belts(grid, Direction.East, "conveyor-basic", P(0, 0));
        Assert.Empty(Walk(grid, P(5, 5)));
    }

    [Fact]
    public void Stack_cells_are_included()
    {
        var grid = new BeltGrid();
        Belts(grid, Direction.East, "conveyor-stack", P(0, 0), P(1, 0), P(2, 0));
        Put(grid, P(3, 0), Direction.East, "conveyor-basic");
        Assert.Equal(new[] { P(0, 0), P(1, 0), P(2, 0), P(3, 0) }, Walk(grid, P(1, 0)));
    }

    [Fact]
    public void Order_is_the_same_on_every_call()
    {
        var grid = new BeltGrid();
        Belts(grid, Direction.East, "conveyor-basic", P(0, 0), P(1, 0), P(2, 0));
        Assert.Equal(Walk(grid, P(1, 0)), Walk(grid, P(1, 0)));
    }
}
