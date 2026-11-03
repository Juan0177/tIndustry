using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The router as Mindustry's Router: it holds one item, and <see cref="BeltGridCell.RouterTicks"/> ticks after it
/// arrived tries the sides in turn (North, East, South, West, from the side after the last one used), never the side it
/// came from; belts and buildings feed it from any side and it hands items to belts, logistics cells and buildings.
/// </summary>
public sealed class RouterTests
{
    private const string Basic = "conveyor-basic";
    private const string Iron = "iron-ore";
    private const int Player = 0;

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly ConveyorDefinition Belt = Content.RequireConveyor(Basic);
    private static readonly ConveyorDefinition Router = Content.RequireConveyor("router");

    private long nextId = 1;

    private TransportedItem NewItem(string itemId = Iron) => new(nextId++, itemId);

    private static void Tick(BeltGrid grid) => grid.Tick(SimulationClock.TickSeconds);

    private static void Line(BeltGrid grid, IEnumerable<GridPosition> cells, Direction dir, ConveyorDefinition? belt = null)
    {
        foreach (var cell in cells)
        {
            Assert.True(grid.TryPlaceFree(cell, dir, belt ?? Belt));
        }
    }

    private static IEnumerable<GridPosition> Row(int y, int x0, int x1) =>
        Enumerable.Range(x0, x1 - x0 + 1).Select(x => new GridPosition(x, y));

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

    /// <summary>No terrain (iron everywhere, no walls); Core at (60, 60).</summary>
    private static Factory SandboxFactory()
    {
        var factory = new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(new GridPosition(60, 60)));
        factory.State.Rules = new GameRules(InstantBuild: true);
        factory.UnlockAllForDemo();
        return factory;
    }

    [Fact]
    public void NumbersAreMindustrys()
    {
        Assert.Equal(8, BeltGridCell.RouterTicks);
        Assert.Equal(1, Router.Capacity);
        Assert.Equal(LogisticsKind.Router, Router.Kind);
    }

    [Fact]
    public void TheSplitterIsGoneFromTheContent()
    {
        Assert.Null(Content.FindConveyor("splitter"));
        Assert.DoesNotContain(Content.Structures, s => s.Id == "splitter");
        Assert.Contains(Content.Structures, s => s.Id == "router");
        Assert.DoesNotContain("splitter", ResearchState.DemoStructureIds);
        Assert.Contains("router", ResearchState.DemoStructureIds);
        Assert.Equal(BuildingKind.Router, BuildSpec.KindFor("router"));
        Assert.Null(BuildSpec.KindFor("splitter"));
        Assert.Equal("router", Factory.StructureIdForTool("router"));
    }

    [Fact]
    public void ItTakesTurnsAmongThreeExits()
    {
        // Router (5, 5) fed from the West; belts leave North, East and South.
        var grid = new BeltGrid();
        var at = new GridPosition(5, 5);
        Assert.True(grid.TryPlaceFree(at, Direction.North, Router));
        var exits = new Dictionary<Direction, GridPosition>
        {
            [Direction.North] = at.Step(Direction.North),
            [Direction.East] = at.Step(Direction.East),
            [Direction.South] = at.Step(Direction.South)
        };
        foreach (var (dir, cell) in exits)
        {
            Assert.True(grid.TryPlaceFree(cell, dir, Belt));
        }

        var order = new List<Direction>();
        for (var n = 0; n < 6; n++)
        {
            var item = NewItem();
            Assert.True(grid.TryInsert(at, item, Direction.East));
            Assert.False(grid.TryInsert(at, NewItem(), Direction.East)); // one item at a time
            for (var tick = 0; tick < BeltGridCell.RouterTicks - 1; tick++)
            {
                Tick(grid);
                Assert.Contains(item, grid.Cells[at].Items);
            }

            Tick(grid);
            Assert.Empty(grid.Cells[at].Items);
            order.Add(exits.Single(e => grid.Cells[e.Value].Items.Contains(item)).Key);
            foreach (var cell in exits.Values)
            {
                foreach (var held in grid.Cells[cell].Items.ToList())
                {
                    grid.Cells[cell].TryRemoveItem(held);
                }
            }
        }

        Assert.Equal(
            [Direction.North, Direction.East, Direction.South, Direction.North, Direction.East, Direction.South],
            order);
    }

    [Fact]
    public void ItNeverHandsAnItemBack()
    {
        // Two routers side by side: the item enters the West one, goes East, and the East one has nowhere else to go.
        var grid = new BeltGrid();
        var west = new GridPosition(5, 5);
        var east = new GridPosition(6, 5);
        Assert.True(grid.TryPlaceFree(west, Direction.North, Router));
        Assert.True(grid.TryPlaceFree(east, Direction.North, Router));
        var item = NewItem();
        Assert.True(grid.TryInsert(west, item, Direction.East));
        for (var tick = 0; tick < 200; tick++)
        {
            Tick(grid);
        }

        Assert.Empty(grid.Cells[west].Items);
        Assert.Equal([item], grid.Cells[east].Items);
        Assert.Equal(Direction.West, grid.Cells[east].RouterFrom);
    }

    [Fact]
    public void ABeltFacingIntoTheRouterGetsNothingBack()
    {
        // Router (5, 5) fed by a belt from the West; a belt North of it also faces into it (empty); the ways out are a
        // belt East and a belt South, both leading away.
        var grid = new BeltGrid();
        var at = new GridPosition(5, 5);
        Assert.True(grid.TryPlaceFree(at, Direction.North, Router));
        Line(grid, Row(5, 2, 4), Direction.East);
        Assert.True(grid.TryPlaceFree(at.Step(Direction.North), Direction.South, Belt));
        Assert.True(grid.TryPlaceFree(at.Step(Direction.East), Direction.East, Belt));
        Assert.True(grid.TryPlaceFree(at.Step(Direction.South), Direction.South, Belt));
        var south = at.Step(Direction.South);

        long fed = 0, east = 0, toSouth = 0;
        for (var tick = 0; tick < 1200; tick++)
        {
            fed += grid.TryInsert(new GridPosition(2, 5), NewItem(), Direction.East) ? 1 : 0;
            Tick(grid);
            Assert.Empty(grid.Cells[at.Step(Direction.North)].Items);
            east += Drain(grid, at.Step(Direction.East));
            toSouth += Drain(grid, south);
        }

        Assert.True(east > 20, $"east {east}");
        Assert.True(toSouth > 20, $"south {toSouth}");
        Assert.Equal(fed, east + toSouth + ItemsOnGrid(grid));
    }

    [Fact]
    public void ItHoldsTheItemWhileEveryExitIsFull()
    {
        // Router (5, 5) fed from the West, one dead-end belt East.
        var grid = new BeltGrid();
        var at = new GridPosition(5, 5);
        var exit = at.Step(Direction.East);
        Assert.True(grid.TryPlaceFree(at, Direction.North, Router));
        Assert.True(grid.TryPlaceFree(exit, Direction.East, Belt));
        long fed = 0;
        for (var tick = 0; tick < 600; tick++)
        {
            fed += grid.TryInsert(at, NewItem(), Direction.East) ? 1 : 0;
            Tick(grid);
        }

        var held = Assert.Single(grid.Cells[at].Items);
        Assert.False(grid.TryInsert(at, NewItem(), Direction.East));
        Assert.Equal(fed, ItemsOnGrid(grid));
        Assert.Equal(Belt.Capacity, grid.Cells[exit].Items.Count);

        // The exit frees one place: the held item goes on the next tick it fits.
        Assert.Equal(1, Drain(grid, exit));
        for (var tick = 0; tick < 60 && grid.Cells[at].Items.Count > 0; tick++)
        {
            Tick(grid);
        }

        Assert.Contains(held, grid.Cells[exit].Items);
    }

    [Theory]
    [InlineData(Direction.East)]
    [InlineData(Direction.West)]
    public void AChainOfRoutersPassesOneItemEveryEightTicks(Direction way)
    {
        // Express belt (3 cells) → 5 routers → express belt (3 cells) along y 5, running East or West, drained at its
        // end; the express belt (18 items a second) is faster than a router. Counted for a minute after a warm-up.
        var express = Content.RequireConveyor("conveyor-express");
        var grid = new BeltGrid();
        GridPosition At(int k) => new(way == Direction.East ? k : 10 - k, 5);
        Line(grid, Enumerable.Range(0, 3).Select(At), way, express);
        foreach (var k in Enumerable.Range(3, 5))
        {
            Assert.True(grid.TryPlaceFree(At(k), Direction.North, Router));
        }

        Line(grid, Enumerable.Range(8, 3).Select(At), way, express);
        long fed = 0, drained = 0, counted = 0;
        const int minute = 60 * SimulationClock.TickRate;
        for (var tick = 0; tick < 600 + minute; tick++)
        {
            fed += grid.TryInsert(At(0), NewItem(), way) ? 1 : 0;
            Tick(grid);
            var out1 = Drain(grid, At(10));
            drained += out1;
            counted += tick >= 600 ? out1 : 0;
        }

        Assert.InRange(counted, minute / BeltGridCell.RouterTicks - 1, minute / BeltGridCell.RouterTicks + 1);
        Assert.Equal(fed, drained + ItemsOnGrid(grid));
    }

    [Fact]
    public void ARingOfFourRoutersNeitherBlocksNorMakesItems()
    {
        // Ring (5..6, 5..6): fed from the West into (5, 5), drained East of (6, 6).
        var grid = new BeltGrid();
        foreach (var cell in new[] { new GridPosition(5, 5), new GridPosition(6, 5), new GridPosition(6, 6), new GridPosition(5, 6) })
        {
            Assert.True(grid.TryPlaceFree(cell, Direction.North, Router));
        }

        Line(grid, Row(5, 2, 4), Direction.East);
        Line(grid, Row(6, 7, 9), Direction.East);
        long fed = 0, drained = 0, lastMinute = 0;
        var seen = new HashSet<long>();
        var ticks = 5 * 60 * SimulationClock.TickRate;
        for (var tick = 0; tick < ticks; tick++)
        {
            fed += grid.TryInsert(new GridPosition(2, 5), NewItem(), Direction.East) ? 1 : 0;
            Tick(grid);
            var out1 = Drain(grid, new GridPosition(9, 6));
            drained += out1;
            lastMinute += tick >= ticks - 60 * SimulationClock.TickRate ? out1 : 0;
            if (tick % 600 == 0)
            {
                seen.Clear();
                Assert.All(grid.Cells.Values.SelectMany(c => c.Items), item => Assert.True(seen.Add(item.Id)));
                Assert.Equal(fed, drained + ItemsOnGrid(grid));
                Assert.All(grid.Cells.Values.Where(c => c.Kind == LogisticsKind.Router), c => Assert.True(c.Items.Count <= 1));
            }
        }

        // The exit router hands an item on every 8 ticks at best (450 a minute) and, by turns, sends at least half of
        // them out (the rest go round the ring); the basic belt out takes at most 315 a minute.
        Assert.InRange(lastMinute, 60 * SimulationClock.TickRate / BeltGridCell.RouterTicks / 2, 315);
        Assert.Equal(fed, drained + ItemsOnGrid(grid));
    }

    [Fact]
    public void ADraggedLineOfRoutersHasNoDirection()
    {
        var factory = SandboxFactory();
        factory.AddBuilder(Player);
        var host = new SimHost(factory);
        var refusals = new List<Refusal>();
        host.CommandRejected += (_, refusal) => refusals.Add(refusal);
        var from = new GridPosition(40, 33);
        var to = new GridPosition(44, 33);
        foreach (var (brush, fixedWay) in new[] { (Direction.East, false), (Direction.South, true) })
        {
            var line = PlacementLine.Plan(host.State, new LineRequest(Player, "router", from, to, brush, fixedWay));
            Assert.Equal(5, line.Count);
            Assert.All(line, c => Assert.Equal(Direction.North, c.Direction));
            Assert.All(line, c => Assert.IsType<PlaceBuilding>(c.Command));
            if (fixedWay)
            {
                continue;
            }

            foreach (var command in PlacementLine.Commands(line))
            {
                host.Submit(command);
            }

            host.AdvanceTicks(1);
        }

        Assert.Empty(refusals);
        foreach (var cell in Row(33, 40, 44))
        {
            Assert.Equal(LogisticsKind.Router, factory.Belts.Cells[cell].Kind);
            Assert.Equal(Direction.North, factory.Belts.Cells[cell].Direction);
        }
    }

    [Fact]
    public void ADraggedLineOfRoutersOverAPerpendicularBeltMakesNoCrossing()
    {
        // A belt column x = 40, y 30..36 heading South; a router line dragged East along y 33 across it.
        var factory = SandboxFactory();
        factory.AddBuilder(Player);
        var host = new SimHost(factory);
        var refusals = new List<Refusal>();
        host.CommandRejected += (_, refusal) => refusals.Add(refusal);
        for (var y = 30; y <= 36; y++)
        {
            Assert.True(factory.TryPlaceBelt(new GridPosition(40, y), Direction.South));
        }

        var crossing = new GridPosition(40, 33);
        var line = PlacementLine.Plan(
            host.State, new LineRequest(Player, "router", new GridPosition(37, 33), new GridPosition(44, 33), Direction.East));
        Assert.All(line, c => Assert.IsType<PlaceBuilding>(c.Command));
        Assert.Contains(line, c => c.Cell == crossing && c.Refusal == RefusalCode.Occupied);
        foreach (var command in PlacementLine.Commands(line))
        {
            host.Submit(command);
        }

        host.AdvanceTicks(1);

        Assert.Empty(refusals);
        Assert.Equal(LogisticsKind.Belt, factory.Belts.Cells[crossing].Kind);
        Assert.Equal(Direction.South, factory.Belts.Cells[crossing].Direction);
        Assert.DoesNotContain(factory.Belts.Cells.Values, c => c.Kind == LogisticsKind.Junction);
        Assert.Equal(7, factory.Belts.Cells.Values.Count(c => c.Kind == LogisticsKind.Router));
    }

    [Fact]
    public void DeconstructingAFullRouterLosesItsItemButNeverDuplicates()
    {
        // Belts (15..19, 21) East into a router (20, 21) with a dead-end belt past it: everything fills, then it goes.
        var factory = SandboxFactory();
        var at = new GridPosition(20, 21);
        foreach (var x in Enumerable.Range(15, 5))
        {
            Assert.True(factory.TryPlaceBelt(new GridPosition(x, 21), Direction.East));
        }

        Assert.True(factory.TryPlaceRouter(at, Direction.North));
        Assert.True(factory.TryPlaceBelt(new GridPosition(21, 21), Direction.East));
        long fed = 0;
        for (var tick = 0; tick < 1200; tick++)
        {
            fed += factory.Belts.TryInsert(new GridPosition(15, 21), NewItem(), Direction.East) ? 1 : 0;
            factory.Step();
        }

        var inside = Assert.Single(factory.Belts.Cells[at].Items).Id;
        Assert.Equal(fed, ItemsOnGrid(factory.Belts));

        Assert.True(factory.TryRemoveBelt(at));
        for (var tick = 0; tick < 600; tick++)
        {
            fed += factory.Belts.TryInsert(new GridPosition(15, 21), NewItem(), Direction.East) ? 1 : 0;
            factory.Step();
        }

        var ids = factory.Belts.Cells.Values.SelectMany(c => c.Items).Select(item => item.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.DoesNotContain(inside, ids);
        Assert.Equal(fed, ids.Count + 1);
    }

    [Theory]
    [InlineData("Up")]
    [InlineData("north")]
    [InlineData("2")]
    [InlineData("")]
    public void ABadSideOfArrivalMakesTheSaveUnreadable(string from)
    {
        var factory = SandboxFactory();
        var at = new GridPosition(20, 21);
        Assert.True(factory.TryPlaceRouter(at, Direction.North));
        Assert.True(factory.Belts.TryInsert(at, NewItem(), Direction.East));
        var data = factory.Capture();
        var cell = Assert.Single(data.Belts, b => b.X == at.X && b.Y == at.Y);
        Assert.Equal("West", cell.RouterFrom);

        cell.RouterFrom = from;

        Assert.Throws<InvalidDataException>(() => Factory.Restore(Content, data));
    }

    [Fact]
    public void ARouterPlacedFacingAnyWayFacesNorth()
    {
        var factory = SandboxFactory();
        Assert.True(factory.TryPlaceRouter(new GridPosition(20, 21), Direction.West));
        Assert.Equal(Direction.North, factory.Belts.Cells[new GridPosition(20, 21)].Direction);
        Assert.True(factory.TryGetSpecAt(new GridPosition(20, 21), out var spec, out _));
        Assert.Equal(BuildingKind.Router, spec.Kind);
        Assert.Equal("router", spec.StructureId);
    }

    [Fact]
    public void ItHandsItemsToTheCore()
    {
        // Core (30..32, 30..32); a line East on y 31 into a router at (29, 31) beside it.
        var factory = NewGame.Sandbox(
            Content, new GridPosition(30, 30), mapWidth: 60, mapHeight: 60, rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        Assert.True(factory.TryPlaceRouter(new GridPosition(29, 31), Direction.North));
        foreach (var x in Enumerable.Range(24, 5))
        {
            Assert.True(factory.TryPlaceBelt(new GridPosition(x, 31), Direction.East, Basic));
        }

        Assert.True(factory.Wallet.TryRemoveMaterial(Iron, factory.Wallet.MaterialCount(Iron)));
        var before = factory.CoreDeliveredItems;
        long fed = 0;
        for (var tick = 0; tick < 600; tick++)
        {
            fed += factory.Belts.TryInsert(new GridPosition(24, 31), NewItem(), Direction.East) ? 1 : 0;
            factory.Step();
        }

        var delivered = factory.CoreDeliveredItems - before;
        Assert.True(delivered > 30, $"delivered {delivered}");
        Assert.Equal(fed, delivered + ItemsOnGrid(factory.Belts));
    }

    [Fact]
    public void ASmelterFeedsTheRouterAndTheRouterFeedsASmelter()
    {
        var factory = SandboxFactory();
        // Forno A (18..19, 20..21) with plates to give; router (18, 22) right below it, whose North is the forno; belts
        // (18, 23..24) South take the plates away.
        Assert.True(factory.TryPlaceSmelter(new GridPosition(18, 20), Direction.East));
        Assert.True(factory.TryPlaceRouter(new GridPosition(18, 22), Direction.North));
        Assert.True(factory.TryPlaceBelt(new GridPosition(18, 23), Direction.South));
        Assert.True(factory.TryPlaceBelt(new GridPosition(18, 24), Direction.South));
        var smelterA = Assert.Single(factory.State.Smelters);
        smelterA.RestoreCraftState(0f, false, new Dictionary<string, int>(), ["iron-plate", "iron-plate"], 0);

        // Forno B (30..31, 20..21); belt (33, 21) West into a router (32, 21) beside it.
        Assert.True(factory.TryPlaceSmelter(new GridPosition(30, 20), Direction.East));
        Assert.True(factory.TryPlaceRouter(new GridPosition(32, 21), Direction.North));
        Assert.True(factory.TryPlaceBelt(new GridPosition(33, 21), Direction.West));
        var smelterB = Assert.Single(factory.State.Smelters, s => s != smelterA);

        for (var tick = 0; tick < 180; tick++)
        {
            if (tick % 20 == 0 && tick < 60)
            {
                Assert.True(factory.Belts.TryInsert(new GridPosition(33, 21), NewItem(), Direction.West));
            }

            factory.Step();
        }

        Assert.Empty(smelterA.OutputQueue);
        Assert.Equal(2, new[] { 23, 24 }.Sum(y => factory.Belts.Cells[new GridPosition(18, y)].Items.Count));
        Assert.True(smelterB.InputBuffer.GetValueOrDefault(Iron) > 0 || smelterB.IsCrafting);
        Assert.Empty(factory.Belts.Cells[new GridPosition(32, 21)].Items);
        Assert.Empty(factory.Belts.Cells[new GridPosition(33, 21)].Items);
    }

    [Fact]
    public void AMinerFeedsARouterBelowIt()
    {
        // No terrain: full iron everywhere. Miner (7..8, 14..15); router (7, 16) below it; belts (7, 17..18) South.
        var factory = new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(new GridPosition(30, 30)));
        factory.UnlockAllForDemo();
        Assert.True(factory.TryPlaceMiner(new GridPosition(7, 14), Direction.East));
        Assert.True(factory.TryPlaceRouter(new GridPosition(7, 16), Direction.North));
        Assert.True(factory.TryPlaceBelt(new GridPosition(7, 17), Direction.South));
        Assert.True(factory.TryPlaceBelt(new GridPosition(7, 18), Direction.South));
        var miner = Assert.Single(factory.State.Miners);

        for (var tick = 0; tick < 30 * SimulationClock.TickRate; tick++)
        {
            factory.Step();
        }

        Assert.True(miner.ItemsProduced > 0);
        Assert.Equal(miner.ItemsProduced, ItemsOnGrid(factory.Belts));
        Assert.True(factory.Belts.Cells[new GridPosition(7, 18)].Items.Count > 0);
    }

    [Fact]
    public void AHeldItemSurvivesSaveAndLoad()
    {
        var factory = SandboxFactory();
        var at = new GridPosition(20, 21);
        Assert.True(factory.TryPlaceRouter(at, Direction.North));
        foreach (var dir in new[] { Direction.North, Direction.East, Direction.South })
        {
            Assert.True(factory.TryPlaceBelt(at.Step(dir), dir));
        }

        // A first item goes North (turn 0), so the turn is East; a second waits 3 ticks.
        Assert.True(factory.Belts.TryInsert(at, NewItem(), Direction.East));
        for (var tick = 0; tick < BeltGridCell.RouterTicks; tick++)
        {
            factory.Step();
        }

        Assert.True(factory.Belts.TryInsert(at, NewItem(), Direction.South));
        factory.Step();
        factory.Step();
        factory.Step();

        var restored = Factory.Restore(Content, factory.Capture());
        var cell = restored.Belts.Cells[at];
        Assert.Single(cell.Items);
        Assert.Equal(3, cell.RouterWaitTicks);
        Assert.Equal(Direction.North, cell.RouterFrom);
        Assert.Equal(1, cell.RouterTurn);
        Assert.Equal(Snapshot.Write(factory.State), Snapshot.Write(restored.State));
        for (var tick = 0; tick < 20; tick++)
        {
            factory.Step();
            restored.Step();
        }

        Assert.Equal(Snapshot.Write(factory.State), Snapshot.Write(restored.State));
    }
}
