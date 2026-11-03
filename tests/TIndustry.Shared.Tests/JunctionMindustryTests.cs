using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The junction as Mindustry's Junction: one queue of <see cref="BeltGridCell.JunctionQueueLength"/> items per
/// direction of travel, each item crosses in <see cref="BeltGridCell.JunctionCrossTicks"/> ticks and leaves as soon as
/// the block on the far side takes it; the queues never block each other; buildings feed it like belts do.
/// </summary>
public sealed class JunctionMindustryTests
{
    private const string Basic = "conveyor-basic";
    private const string Iron = "iron-ore";

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly ConveyorDefinition Belt = Content.RequireConveyor(Basic);
    private static readonly ConveyorDefinition Junction = Content.RequireConveyor("junction");

    private long nextId = 1;

    private TransportedItem NewItem() => new(nextId++, Iron);

    private static void Tick(BeltGrid grid) => grid.Tick(SimulationClock.TickSeconds);

    private static void Line(BeltGrid grid, IEnumerable<GridPosition> cells, Direction dir)
    {
        foreach (var cell in cells)
        {
            Assert.True(grid.TryPlaceFree(cell, dir, Belt));
        }
    }

    private static IEnumerable<GridPosition> Row(int y, int x0, int x1) =>
        Enumerable.Range(x0, x1 - x0 + 1).Select(x => new GridPosition(x, y));

    private static IEnumerable<GridPosition> Column(int x, int y0, int y1) =>
        Enumerable.Range(y0, y1 - y0 + 1).Select(y => new GridPosition(x, y));

    /// <summary>Takes the ready head of <paramref name="cell"/> away (a sink past the line's end); 1 when it did.</summary>
    private static int Drain(BeltGrid grid, GridPosition cell)
    {
        var belt = grid.Cells[cell];
        if (belt.PeekOutput() is null)
        {
            return 0;
        }

        belt.RemoveOutput();
        return 1;
    }

    private static int ItemsOnGrid(BeltGrid grid) => grid.Cells.Values.Sum(c => c.Items.Count);

    [Fact]
    public void NumbersAreMindustrys()
    {
        Assert.Equal(6, BeltGridCell.JunctionQueueLength);
        Assert.Equal(26, BeltGridCell.JunctionCrossTicks);
        Assert.Equal(6, Junction.Capacity);
    }

    [Fact]
    public void TwoCrossedFlowsRunAtFullRateWithoutBlockingEachOther()
    {
        // West → East on y 10 (x 0..10) and North → South on x 5 (y 5..15), the junction at (5, 10); both saturated.
        var crossed = new BeltGrid();
        Line(crossed, Row(10, 0, 10).Where(p => p.X != 5), Direction.East);
        Line(crossed, Column(5, 5, 15).Where(p => p.Y != 10), Direction.South);
        Assert.True(crossed.TryPlaceFree(new GridPosition(5, 10), Direction.East, Junction));
        var plain = new BeltGrid();
        Line(plain, Row(0, 0, 10), Direction.East);

        int east = 0, south = 0, reference = 0;
        for (var tick = 0; tick < 1800; tick++)
        {
            crossed.TryInsert(new GridPosition(0, 10), NewItem(), Direction.East);
            crossed.TryInsert(new GridPosition(5, 5), NewItem(), Direction.South);
            plain.TryInsert(new GridPosition(0, 0), NewItem(), Direction.East);
            Tick(crossed);
            Tick(plain);
            var counting = tick >= 600;
            east += Drain(crossed, new GridPosition(10, 10)) * (counting ? 1 : 0);
            south += Drain(crossed, new GridPosition(5, 15)) * (counting ? 1 : 0);
            reference += Drain(plain, new GridPosition(10, 0)) * (counting ? 1 : 0);
        }

        Assert.True(reference > 100, $"reference line delivered {reference}");
        Assert.InRange(east, reference - 1, reference + 1);
        Assert.InRange(south, reference - 1, reference + 1);
    }

    [Fact]
    public void EachDirectionQueuesSixItems()
    {
        // A belt on every side leading away, so every direction has a block on its far side.
        var grid = new BeltGrid();
        var at = new GridPosition(5, 5);
        Assert.True(grid.TryPlaceFree(at, Direction.North, Junction));
        foreach (var dir in DirectionMath.All)
        {
            Assert.True(grid.TryPlaceFree(at.Step(dir), dir, Belt));
        }

        foreach (var dir in new[] { Direction.East, Direction.North, Direction.West, Direction.South })
        {
            for (var i = 0; i < 6; i++)
            {
                Assert.True(grid.TryInsert(at, NewItem(), dir), $"{dir} item {i}");
            }

            Assert.False(grid.TryInsert(at, NewItem(), dir), $"{dir}: a seventh item");
        }

        Assert.Equal(24, grid.Cells[at].Items.Count);
    }

    [Fact]
    public void AnItemWithoutADirectionIsRefused()
    {
        var grid = new BeltGrid();
        Assert.True(grid.TryPlaceFree(new GridPosition(5, 5), Direction.North, Junction));
        Assert.False(grid.TryInsert(new GridPosition(5, 5), NewItem()));
        Assert.False(grid.TryInsert(new GridPosition(5, 5), NewItem(), source: new GridPosition(7, 5))); // not beside it
    }

    [Theory]
    [InlineData(Direction.North)]
    [InlineData(Direction.East)]
    [InlineData(Direction.South)]
    [InlineData(Direction.West)]
    public void AnItemCrossesInTwentySixTicks(Direction travel)
    {
        // The junction's own (saved) direction plays no part: it faces North here.
        var grid = new BeltGrid();
        var at = new GridPosition(5, 5);
        var exit = at.Step(travel);
        Assert.True(grid.TryPlaceFree(at, Direction.North, Junction));
        Assert.True(grid.TryPlaceFree(exit, travel, Belt));
        var item = NewItem();
        Assert.True(grid.TryInsert(at, item, travel));

        for (var tick = 1; tick < 26; tick++)
        {
            Tick(grid);
            Assert.Same(item, Assert.Single(grid.Cells[at].Items));
        }

        Tick(grid);
        Assert.Empty(grid.Cells[at].Items);
        Assert.Same(item, Assert.Single(grid.Cells[exit].Items));
    }

    [Fact]
    public void AHandoffIntoTheJunctionLeavesTwentySixTicksLater()
    {
        // A belt hands the item over during tick T: it reaches the belt past the junction during tick T + 26.
        var grid = new BeltGrid();
        Assert.True(grid.TryPlaceFree(new GridPosition(0, 0), Direction.East, Belt));
        Assert.True(grid.TryPlaceFree(new GridPosition(1, 0), Direction.South, Junction));
        Assert.True(grid.TryPlaceFree(new GridPosition(2, 0), Direction.East, Belt));
        var item = NewItem();
        Assert.True(grid.TryInsert(new GridPosition(0, 0), item, Direction.East));
        var entered = -1;
        for (var tick = 1; tick <= 200; tick++)
        {
            Tick(grid);
            if (entered < 0 && grid.Cells[new GridPosition(1, 0)].Items.Contains(item))
            {
                entered = tick;
            }

            if (grid.Cells[new GridPosition(2, 0)].Items.Contains(item))
            {
                Assert.True(entered > 0);
                Assert.Equal(entered + 26, tick);
                return;
            }
        }

        Assert.Fail("the item never crossed");
    }

    [Fact]
    public void ABlockedExitFillsOnlyItsOwnQueue()
    {
        // Eastward items reach a dead-end belt past the junction; the North → South flow goes on at the rate of a plain line.
        var grid = new BeltGrid();
        var at = new GridPosition(5, 10);
        Line(grid, Row(10, 0, 4), Direction.East);
        Line(grid, Row(10, 6, 6), Direction.East);
        Line(grid, Column(5, 5, 15).Where(p => p.Y != 10), Direction.South);
        Assert.True(grid.TryPlaceFree(at, Direction.East, Junction));
        var plain = new BeltGrid();
        Line(plain, Column(0, 5, 15), Direction.South);

        int south = 0, reference = 0;
        for (var tick = 0; tick < 1800; tick++)
        {
            grid.TryInsert(new GridPosition(0, 10), NewItem(), Direction.East);
            grid.TryInsert(new GridPosition(5, 5), NewItem(), Direction.South);
            plain.TryInsert(new GridPosition(0, 5), NewItem(), Direction.South);
            Tick(grid);
            Tick(plain);
            south += tick >= 600 ? Drain(grid, new GridPosition(5, 15)) : 0;
            reference += tick >= 600 ? Drain(plain, new GridPosition(0, 15)) : 0;
            if (tick < 600)
            {
                Drain(grid, new GridPosition(5, 15));
                Drain(plain, new GridPosition(0, 15));
            }
        }

        var items = grid.Cells[at].Items;
        Assert.Equal(6, items.Count(item => item.Travel == Direction.East));
        Assert.True(items.Count(item => item.Travel == Direction.South) < 6);
        Assert.InRange(south, reference - 1, reference + 1);
    }

    [Fact]
    public void AnAttachedSmelterFeedsTheJunction()
    {
        // Forno (18..19, 20..21); the junction (20, 21) beside it faces back into it, which must not matter; a belt
        // past the junction takes the plates East.
        var factory = NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7,
            startingMoney: 100_000, rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        Assert.True(factory.TryPlaceSmelter(new GridPosition(18, 20), Direction.East));
        Assert.True(factory.TryPlaceJunction(new GridPosition(20, 21), Direction.West));
        Assert.True(factory.TryPlaceBelt(new GridPosition(21, 21), Direction.East));
        Assert.True(factory.TryPlaceBelt(new GridPosition(22, 21), Direction.East));
        var smelter = Assert.Single(factory.State.Smelters);
        smelter.RestoreCraftState(0f, false, new Dictionary<string, int>(), ["iron-plate", "iron-plate"], 0);

        for (var tick = 0; tick < 120; tick++)
        {
            factory.Step();
        }

        Assert.Empty(smelter.OutputQueue);
        Assert.Equal(2, new[] { 21, 22 }.Sum(x => factory.Belts.Cells[new GridPosition(x, 21)].Items.Count));
        Assert.True(factory.IsJunctionSideConnected(new GridPosition(20, 21), Direction.West)); // the forno is an input
        Assert.True(factory.IsJunctionSideConnected(new GridPosition(20, 21), Direction.East));
    }

    [Fact]
    public void TheJunctionHandsItemsToTheCore()
    {
        // Core (30..32, 30..32); a line East on y 31 ending in a junction at (29, 31) that faces North.
        var factory = NewGame.Sandbox(
            Content, new GridPosition(30, 30), mapWidth: 60, mapHeight: 60, rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        Assert.True(factory.TryPlaceJunction(new GridPosition(29, 31), Direction.North));
        foreach (var x in Enumerable.Range(24, 5))
        {
            Assert.True(factory.TryPlaceBelt(new GridPosition(x, 31), Direction.East, Basic));
        }

        foreach (var material in new[] { Iron })
        {
            Assert.True(factory.Wallet.TryRemoveMaterial(material, factory.Wallet.MaterialCount(material)));
        }

        var before = factory.CoreDeliveredItems;
        for (var tick = 0; tick < 600; tick++)
        {
            factory.Belts.TryInsert(new GridPosition(24, 31), NewItem(), Direction.East);
            factory.Step();
        }

        Assert.True(factory.CoreDeliveredItems - before > 30, $"delivered {factory.CoreDeliveredItems - before}");
        Assert.True(factory.IsJunctionSideConnected(new GridPosition(29, 31), Direction.East)); // the Core takes them
    }

    [Fact]
    public void NothingIsLostOrDuplicatedInFiveMinutes()
    {
        // Two junctions in a row on y 10 crossed by two lines heading North and South; the East exit of the row is a
        // dead end (its queue fills), a third line enters the second junction from the East heading West.
        var grid = new BeltGrid();
        var first = new GridPosition(5, 10);
        var second = new GridPosition(6, 10);
        Line(grid, Row(10, 0, 4), Direction.East);
        Line(grid, Row(10, 8, 12).Reverse(), Direction.West);
        Line(grid, Column(5, 5, 15).Where(p => p.Y != 10), Direction.South);
        Line(grid, Column(6, 5, 15).Where(p => p.Y != 10), Direction.North);
        Assert.True(grid.TryPlaceFree(first, Direction.North, Junction));
        Assert.True(grid.TryPlaceFree(second, Direction.East, Junction));
        Assert.True(grid.TryPlaceFree(new GridPosition(7, 10), Direction.East, Junction));

        long fed = 0, drained = 0;
        var seen = new HashSet<long>();
        for (var tick = 0; tick < 5 * 60 * SimulationClock.TickRate; tick++)
        {
            fed += grid.TryInsert(new GridPosition(0, 10), NewItem(), Direction.East) ? 1 : 0;
            fed += grid.TryInsert(new GridPosition(12, 10), NewItem(), Direction.West) ? 1 : 0;
            fed += grid.TryInsert(new GridPosition(5, 5), NewItem(), Direction.South) ? 1 : 0;
            fed += grid.TryInsert(new GridPosition(6, 15), NewItem(), Direction.North) ? 1 : 0;
            Tick(grid);
            foreach (var end in new[] { new GridPosition(5, 15), new GridPosition(6, 5), new GridPosition(0, 10) })
            {
                drained += Drain(grid, end);
            }

            if (tick % 600 == 0)
            {
                seen.Clear();
                Assert.All(grid.Cells.Values.SelectMany(c => c.Items), item => Assert.True(seen.Add(item.Id)));
                Assert.Equal(fed, drained + ItemsOnGrid(grid));
            }
        }

        Assert.True(drained > 1000, $"drained {drained}");
        Assert.Equal(fed, drained + ItemsOnGrid(grid));
    }

    [Fact]
    public void AQueueSurvivesSaveAndLoad()
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7,
            startingMoney: 100_000, rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        var at = new GridPosition(20, 21);
        Assert.True(factory.TryPlaceJunction(at, Direction.North));
        Assert.True(factory.TryPlaceBelt(new GridPosition(21, 21), Direction.East));
        Assert.True(factory.TryPlaceBelt(new GridPosition(20, 22), Direction.South));
        Assert.True(factory.Belts.TryInsert(at, NewItem(), Direction.East));
        factory.Step();
        factory.Step();
        Assert.True(factory.Belts.TryInsert(at, NewItem(), Direction.South));
        factory.Step();

        var restored = Factory.Restore(Content, factory.Capture());
        var items = restored.Belts.Cells[at].Items;
        Assert.Equal([Direction.East, Direction.South], items.Select(i => i.Travel!.Value));
        Assert.Equal([3, 1], items.Select(i => i.JunctionTicks!.Value));
        for (var tick = 0; tick < 22; tick++)
        {
            restored.Step();
        }

        Assert.Equal(2, restored.Belts.Cells[at].Items.Count);
        restored.Step(); // the eastward item has been inside for 26 ticks
        Assert.Single(restored.Belts.Cells[new GridPosition(21, 21)].Items);
        Assert.Single(restored.Belts.Cells[at].Items);
    }

    private static Factory SandboxFactory() =>
        NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7,
            startingMoney: 100_000, rules: new GameRules(InstantBuild: true));

    [Fact]
    public void TheFarSideMustTakeItems()
    {
        // Mindustry's Junction.acceptItem: only toward a block on the far side. An empty cell refuses, a belt takes.
        var grid = new BeltGrid();
        var at = new GridPosition(5, 5);
        Assert.True(grid.TryPlaceFree(at, Direction.North, Junction));
        Assert.False(grid.TryInsert(at, NewItem(), Direction.East));
        Assert.True(grid.TryPlaceFree(new GridPosition(6, 5), Direction.East, Belt));
        Assert.True(grid.TryInsert(at, NewItem(), Direction.East));
        Assert.False(grid.TryInsert(at, NewItem(), Direction.North)); // (5, 4) is still empty

        // In a game: a miner (it takes nothing) past the junction refuses, a forno takes.
        var factory = SandboxFactory();
        factory.UnlockAllForDemo();
        Assert.True(factory.TryPlaceJunction(new GridPosition(20, 21), Direction.North));
        Assert.True(factory.TryPlaceMiner(new GridPosition(21, 20), Direction.East)); // x 21-22
        Assert.False(factory.Belts.TryInsert(new GridPosition(20, 21), NewItem(), Direction.East));
        Assert.True(factory.TryPlaceJunction(new GridPosition(30, 21), Direction.North));
        Assert.True(factory.TryPlaceSmelter(new GridPosition(31, 20), Direction.East)); // x 31-32
        Assert.True(factory.Belts.TryInsert(new GridPosition(30, 21), NewItem(), Direction.East));
    }

    [Fact]
    public void TwoBeltsFacingIntoAJunctionNeverBounceItems()
    {
        // (4, 5) East → junction (5, 5) ← (6, 5) West: each belt refuses what crosses toward it from its front.
        var grid = new BeltGrid();
        var at = new GridPosition(5, 5);
        Assert.True(grid.TryPlaceFree(new GridPosition(4, 5), Direction.East, Belt));
        Assert.True(grid.TryPlaceFree(at, Direction.North, Junction));
        Assert.True(grid.TryPlaceFree(new GridPosition(6, 5), Direction.West, Belt));
        var fromWest = new HashSet<long>();
        var fromEast = new HashSet<long>();
        for (var tick = 0; tick < 600; tick++)
        {
            var west = NewItem();
            if (grid.TryInsert(new GridPosition(4, 5), west, Direction.East))
            {
                fromWest.Add(west.Id);
            }

            var east = NewItem();
            if (grid.TryInsert(new GridPosition(6, 5), east, Direction.West))
            {
                fromEast.Add(east.Id);
            }

            Tick(grid);
            Assert.DoesNotContain(grid.Cells[new GridPosition(6, 5)].Items, item => fromWest.Contains(item.Id));
            Assert.DoesNotContain(grid.Cells[new GridPosition(4, 5)].Items, item => fromEast.Contains(item.Id));
        }

        var queued = grid.Cells[at].Items;
        Assert.Equal(6, queued.Count(item => item.Travel == Direction.East));
        Assert.Equal(6, queued.Count(item => item.Travel == Direction.West));
        Assert.Equal(fromWest.Count + fromEast.Count, ItemsOnGrid(grid));
    }

    [Fact]
    public void DeconstructingAFullJunctionLosesItsItemsButNeverDuplicates()
    {
        // A line East into a junction (20, 21) and a dead-end belt past it: the queue fills, then the junction goes.
        var factory = SandboxFactory();
        factory.UnlockAllForDemo();
        var at = new GridPosition(20, 21);
        foreach (var x in Enumerable.Range(15, 5))
        {
            Assert.True(factory.TryPlaceBelt(new GridPosition(x, 21), Direction.East));
        }

        Assert.True(factory.TryPlaceJunction(at, Direction.North));
        Assert.True(factory.TryPlaceBelt(new GridPosition(21, 21), Direction.East));
        long fed = 0;
        for (var tick = 0; tick < 1200; tick++)
        {
            fed += factory.Belts.TryInsert(new GridPosition(15, 21), NewItem(), Direction.East) ? 1 : 0;
            factory.Step();
        }

        var inside = factory.Belts.Cells[at].Items.Select(item => item.Id).ToList();
        Assert.Equal(6, inside.Count);
        Assert.Equal(fed, ItemsOnGrid(factory.Belts));

        Assert.True(factory.TryRemoveBelt(at));
        long lost = inside.Count;
        for (var tick = 0; tick < 600; tick++)
        {
            fed += factory.Belts.TryInsert(new GridPosition(15, 21), NewItem(), Direction.East) ? 1 : 0;
            factory.Step();
        }

        var ids = factory.Belts.Cells.Values.SelectMany(c => c.Items).Select(item => item.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.DoesNotContain(ids, inside.Contains);
        Assert.Equal(fed, ids.Count + lost);
    }

    [Fact]
    public void AMinerDeliversThroughAJunction()
    {
        // No terrain: full iron everywhere. Miner (7..8, 14..15), junction (9, 14), belts (10..11, 14) into the Core
        // (12..14, 13..15); the miner does not touch the Core.
        var factory = new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(new GridPosition(12, 13)));
        factory.UnlockAllForDemo();
        Assert.True(factory.TryPlaceMiner(new GridPosition(7, 14), Direction.East));
        Assert.True(factory.TryPlaceJunction(new GridPosition(9, 14), Direction.North));
        Assert.True(factory.TryPlaceBelt(new GridPosition(10, 14), Direction.East));
        Assert.True(factory.TryPlaceBelt(new GridPosition(11, 14), Direction.East));
        var miner = Assert.Single(factory.State.Miners);
        var before = factory.CoreDeliveredItems;

        for (var tick = 0; tick < 30 * SimulationClock.TickRate; tick++)
        {
            factory.Step();
        }

        Assert.True(miner.ItemsProduced > 0);
        Assert.True(factory.CoreDeliveredItems - before > 0);
        Assert.Equal(miner.ItemsProduced, factory.CoreDeliveredItems - before + ItemsOnGrid(factory.Belts));
    }

    [Fact]
    public void AJunctionFeedsAGeneratorAndAStandaloneStorageBlock()
    {
        var factory = SandboxFactory();
        factory.UnlockAllForDemo();
        // Coal: belt (19, 21) East → junction (20, 21) → generator (21..22, 20..21).
        Assert.True(factory.PlaceInstant(
            new BuildSpec(Generator.BuildingId, BuildingKind.Generator, new GridPosition(21, 20), Direction.East), charge: false));
        Assert.True(factory.TryPlaceJunction(new GridPosition(20, 21), Direction.North));
        Assert.True(factory.TryPlaceBelt(new GridPosition(19, 21), Direction.East));
        // Iron: belt (38, 40) East → junction (39, 40) → container (40..41, 40..41), far from the Core.
        Assert.True(factory.PlaceInstant(
            new BuildSpec("container", BuildingKind.Container, new GridPosition(40, 40), Direction.East), charge: false));
        Assert.True(factory.TryPlaceJunction(new GridPosition(39, 40), Direction.North));
        Assert.True(factory.TryPlaceBelt(new GridPosition(38, 40), Direction.East));
        var generator = Assert.Single(factory.State.Generators);
        var storage = Assert.Single(factory.State.Storages, s => s.Kind == BuildingKind.Container);
        Assert.False(storage.IsLinked);

        for (var tick = 0; tick < 300; tick++)
        {
            factory.Belts.TryInsert(new GridPosition(19, 21), new TransportedItem(nextId++, Generator.FuelItemId), Direction.East);
            factory.Belts.TryInsert(new GridPosition(38, 40), NewItem(), Direction.East);
            factory.Step();
        }

        Assert.True(generator.FuelBuffer > 0 || generator.FuelConsumed > 0);
        Assert.True(storage.Count(Iron) > 0);
        Assert.True(factory.IsJunctionSideConnected(new GridPosition(20, 21), Direction.East));
        Assert.True(factory.IsJunctionSideConnected(new GridPosition(39, 40), Direction.East));
    }
}
