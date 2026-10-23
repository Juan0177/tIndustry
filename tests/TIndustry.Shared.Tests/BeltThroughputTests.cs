using System.Text;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Belt throughput like Mindustry (Task 3 of "nastri come Mindustry"): three items per cell 0.4 apart, a saturated
/// line flows at speed / spacing whatever its direction, an item keeps its movement across a cell handoff, and side
/// inputs never lose or duplicate items.
/// </summary>
public sealed class BeltThroughputTests
{
    private const string Basic = "conveyor-basic";
    private const int Length = 12;

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    // Core covers (30..32, 30..32).
    private static Factory Sandbox() =>
        NewGame.Sandbox(Content, new GridPosition(30, 30), mapWidth: 60, mapHeight: 60,
            rules: new GameRules(InstantBuild: true));

    /// <summary>The <see cref="Length"/> cells of a straight basic line heading <paramref name="dir"/> into the Core, upstream first.</summary>
    private static GridPosition[] LineIntoCore(Direction dir) => dir switch
    {
        Direction.East => Cells(i => new GridPosition(30 - Length + i, 30)),
        Direction.West => Cells(i => new GridPosition(33 + Length - 1 - i, 30)),
        Direction.South => Cells(i => new GridPosition(30, 30 - Length + i)),
        _ => Cells(i => new GridPosition(30, 33 + Length - 1 - i))
    };

    private static GridPosition[] Cells(Func<int, GridPosition> at) => Enumerable.Range(0, Length).Select(at).ToArray();

    private static void Place(Factory factory, IEnumerable<GridPosition> cells, Direction dir)
    {
        foreach (var cell in cells)
        {
            Assert.True(factory.TryPlaceBelt(cell, dir, Basic), $"belt at {cell}");
        }
    }

    private static long nextId = 1_000_000;

    /// <summary>Puts a new item on <paramref name="cell"/> (from behind, flowing <paramref name="dir"/>) if it takes one.</summary>
    private static bool Feed(Factory factory, GridPosition cell, Direction dir) =>
        factory.Belts.TryInsert(cell, new TransportedItem(Interlocked.Increment(ref nextId), "iron-ore"), dir);

    private static int ItemsOnGrid(Factory factory) => factory.Belts.Cells.Values.Sum(c => c.Items.Count);

    [Theory]
    [InlineData(Direction.East)]
    [InlineData(Direction.West)]
    [InlineData(Direction.North)]
    [InlineData(Direction.South)]
    public void SaturatedLineFlowsTheSameInEveryDirection(Direction dir)
    {
        var belt = Content.RequireConveyor(Basic);
        var delivered = DeliveredBySaturatedLine(dir);

        var expected = 600 / SimulationClock.TickRate * belt.RateItemsPerSecond / belt.ItemSpacing;
        Assert.InRange(delivered, expected - 2, expected + 2);
        Assert.InRange(delivered - DeliveredBySaturatedLine(Direction.East), -1, 1);
    }

    /// <summary>Items a line heading <paramref name="dir"/>, fed every tick, delivers to the Core in 600 ticks once full.</summary>
    private static long DeliveredBySaturatedLine(Direction dir)
    {
        var factory = Sandbox();
        var line = LineIntoCore(dir);
        Place(factory, line, dir);
        var warmUp = (int)(Length / Content.RequireConveyor(Basic).RateItemsPerSecond * SimulationClock.TickRate) + 120;
        long before = 0;
        for (var i = 0; i < warmUp + 600; i++)
        {
            if (i == warmUp)
            {
                before = factory.CoreDeliveredItems;
            }

            Feed(factory, line[0], dir);
            factory.Step();
        }

        return factory.CoreDeliveredItems - before;
    }

    [Fact]
    public void AFullCellHoldsThreeItems()
    {
        var factory = Sandbox();
        // East line on y 20, x 10..19, against a miner at (20..21, 20..21) that takes no items.
        var line = Enumerable.Range(10, 10).Select(x => new GridPosition(x, 20)).ToArray();
        Place(factory, line, Direction.East);
        Assert.True(factory.TryPlaceMiner(new GridPosition(20, 20), Direction.East));
        for (var i = 0; i < 1200; i++)
        {
            Feed(factory, line[0], Direction.East);
            factory.Step();
        }

        var belt = Content.RequireConveyor(Basic);
        var items = factory.Belts.Cells[line[^2]].Items;
        Assert.Equal(3, belt.Capacity);
        Assert.Equal(3, items.Count);
        for (var i = 1; i < items.Count; i++)
        {
            Assert.Equal(belt.ItemSpacing, items[i - 1].Progress - items[i].Progress, 3);
        }
    }

    [Fact]
    public void SideInputsNeverLoseOrDuplicateItems()
    {
        // Main line East on y 30 into the Core, x 18..29: a side line into (19,30), a junction at 21 crossed by a line
        // heading North, a bridge 23 → 25 (24 stays empty) and a sorter at 27 that sends lead to dead ends on its sides.
        var factory = Sandbox();
        factory.UnlockAllForDemo();
        foreach (var material in new[] { "iron-ore", "lead-ore", "titanium-ore", "graphite", "silicon" })
        {
            factory.Wallet.AddMaterial(material, 1_000);
        }

        Assert.True(factory.TryPlaceBridge(new GridPosition(23, 30), Direction.East));
        Assert.Equal(new GridPosition(25, 30), factory.Belts.Cells[new GridPosition(23, 30)].BridgePartner);
        Assert.True(factory.TryPlaceJunction(new GridPosition(21, 30), Direction.East));
        Assert.True(factory.TryPlaceSorter(new GridPosition(27, 30), Direction.East, "iron-ore"));
        var main = new[] { 18, 19, 20, 22, 26, 28, 29 }.Select(x => new GridPosition(x, 30)).ToArray();
        Place(factory, main, Direction.East);
        var side = Enumerable.Range(31, 6).Reverse().Select(y => new GridPosition(19, y)).ToArray();
        Place(factory, side, Direction.North);
        var cross = new[] { 33, 32, 31, 29, 28 }.Select(y => new GridPosition(21, y)).ToArray();
        Place(factory, cross, Direction.North);
        Place(factory, [new GridPosition(27, 29), new GridPosition(27, 28)], Direction.North);
        Place(factory, [new GridPosition(27, 31), new GridPosition(27, 32)], Direction.South);
        Assert.All(main.Concat(side), cell => Assert.Equal(3, factory.Belts.Cells[cell].Definition.Capacity));
        // Built: no iron or lead left in the Core, so its maximum never stops the line here.
        foreach (var material in new[] { "iron-ore", "lead-ore" })
        {
            Assert.True(factory.Wallet.TryRemoveMaterial(material, factory.Wallet.MaterialCount(material)));
        }

        long fed = 0;
        for (var i = 0; i < 1800; i++)
        {
            var item = i % 4 == 0 ? "lead-ore" : "iron-ore";
            fed += factory.Belts.TryInsert(main[0], new TransportedItem(Interlocked.Increment(ref nextId), item), Direction.East) ? 1 : 0;
            fed += Feed(factory, side[0], Direction.North) ? 1 : 0;
            fed += Feed(factory, cross[0], Direction.North) ? 1 : 0;
            factory.Step();
        }

        Assert.True(factory.CoreDeliveredItems > 0);
        Assert.NotEmpty(factory.Belts.Cells[new GridPosition(21, 28)].Items); // the crossing line went through
        Assert.NotEmpty(factory.Belts.Cells[new GridPosition(27, 28)].Items); // lead went aside
        Assert.Equal(fed, factory.CoreDeliveredItems + ItemsOnGrid(factory));
    }

    [Theory]
    [InlineData("conveyor-express")]
    [InlineData("junction")]
    public void AHandoffKeepsMovingAtTheReceivingSpeed(string fromId)
    {
        // An item 0.95 along a fast cell crosses into a basic belt within the tick: it goes on for the rest of the tick
        // at the basic belt's speed, never jumping ahead by the faster cell's leftover.
        var from = Content.RequireConveyor(fromId);
        var basic = Content.RequireConveyor(Basic);
        var grid = new BeltGrid();
        grid.TryPlaceFree(new GridPosition(0, 0), Direction.East, from);
        grid.TryPlaceFree(new GridPosition(1, 0), Direction.East, basic);
        var item = new TransportedItem(1, "iron-ore");
        Assert.True(grid.TryInsert(new GridPosition(0, 0), item, Direction.East));
        item.Progress = 0.95f;

        grid.Tick(SimulationClock.TickSeconds);

        var fromStep = from.RateItemsPerSecond * SimulationClock.TickSeconds;
        var basicStep = basic.RateItemsPerSecond * SimulationClock.TickSeconds;
        Assert.Same(item, Assert.Single(grid.Cells[new GridPosition(1, 0)].Items));
        Assert.Equal((0.95f + fromStep - 1f) / fromStep * basicStep, item.Progress, 4);
        Assert.True(item.Progress < basicStep);
    }

    [Fact]
    public void ABeltHeadOnIsRefused()
    {
        // Two plain belts facing each other: an item never bounces between them.
        var factory = Sandbox();
        Place(factory, [new GridPosition(10, 10)], Direction.East);
        Place(factory, [new GridPosition(11, 10)], Direction.West);
        var item = new TransportedItem(9_000, "iron-ore");
        Assert.True(factory.Belts.TryInsert(new GridPosition(10, 10), item, Direction.East));
        for (var tick = 0; tick < 300; tick++)
        {
            factory.Step();
            Assert.Empty(factory.Belts.Cells[new GridPosition(11, 10)].Items);
        }

        Assert.Same(item, Assert.Single(factory.Belts.Cells[new GridPosition(10, 10)].Items));
    }

    [Theory]
    [InlineData(Basic)]
    [InlineData("conveyor-fast")]
    [InlineData("conveyor-express")]
    [InlineData("conveyor-armored")]
    public void AnItemCrossesNCellsInNOverSpeedSeconds(string conveyorId)
    {
        const int n = 20;
        var belt = Content.RequireConveyor(conveyorId);
        var grid = new BeltGrid();
        for (var x = 0; x < n; x++)
        {
            grid.TryPlaceFree(new GridPosition(x, 0), Direction.East, belt);
        }

        Assert.True(grid.TryInsert(new GridPosition(0, 0), new TransportedItem(1, "iron-ore"), Direction.East));
        var ticks = 0;
        while (grid.Cells[new GridPosition(n - 1, 0)].PeekOutput() is null && ticks < 100_000)
        {
            grid.Tick(SimulationClock.TickSeconds);
            ticks++;
        }

        // Float steps can land an ulp short of a cell end, which costs at most the one tick allowed.
        var expected = Math.Round(n / (double)belt.RateItemsPerSecond * SimulationClock.TickRate, 3);
        Assert.InRange(ticks, expected - 1, expected + 1);
    }

    [Fact]
    public void SavedBeltItemsKeepFlowing()
    {
        var factory = Sandbox();
        var line = LineIntoCore(Direction.East);
        Place(factory, line, Direction.East);
        for (var i = 0; i < line.Length; i++)
        {
            var item = new TransportedItem(5_000 + i, "iron-ore");
            Assert.True(factory.Belts.TryInsert(line[i], item, Direction.East));
            item.Progress = i * 0.37f % 1f; // one item per cell (the old capacity), anywhere on it
        }

        var host = SimHost.FromSnapshot(Content, new SimHost(factory).CreateSnapshot());
        var before = host.State.CoreDeliveredItems;

        host.AdvanceTicks((int)(Length / Content.RequireConveyor(Basic).RateItemsPerSecond * SimulationClock.TickRate) + 60);

        Assert.Equal(line.Length, host.State.CoreDeliveredItems - before);
        Assert.All(host.State.Belts.Values, c => Assert.Empty(c.Items));
    }
}
