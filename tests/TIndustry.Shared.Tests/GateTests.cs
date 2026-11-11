using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Mindustry's overflow and underflow gates: no direction, no queue (an item enters only if it leaves in the same
/// call). The overflow gate sends an item straight on (the way it travels) when the block ahead takes it, else to the
/// left or the right alternating on a turn bit per direction of travel (as the sorter); the underflow gate tries the
/// sides first and goes straight only when neither side takes it. With nothing free the item waits on its belt. A gate
/// never takes from, nor hands to, a gate or a sorter.
/// </summary>
public sealed class GateTests
{
    private const string Basic = "conveyor-basic";
    private const string Iron = "iron-ore";
    private const string Copper = "copper-ore";
    private const int Player = 0;

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly ConveyorDefinition Belt = Content.RequireConveyor(Basic);
    private static readonly ConveyorDefinition Overflow = Content.RequireConveyor("overflow-gate");
    private static readonly ConveyorDefinition Underflow = Content.RequireConveyor("underflow-gate");
    private static readonly ConveyorDefinition Sorter = Content.RequireConveyor("sorter");

    private static readonly GridPosition At = new(5, 5);
    private static readonly Direction Left = DirectionMath.Left(Direction.East);
    private static readonly Direction Right = DirectionMath.Right(Direction.East);

    private long nextId = 1;

    private TransportedItem NewItem(string itemId = Iron) => new(nextId++, itemId);

    private static void Tick(BeltGrid grid) => grid.Tick(SimulationClock.TickSeconds);

    private static int ItemsOnGrid(BeltGrid grid) => grid.Cells.Values.Sum(c => c.Items.Count);

    /// <summary>A gate on <see cref="At"/> with a belt leaving on each of <paramref name="exits"/>.</summary>
    private static BeltGrid GateWithExits(ConveyorDefinition gate, params Direction[] exits)
    {
        var grid = new BeltGrid();
        Assert.True(grid.TryPlaceFree(At, Direction.North, gate));
        foreach (var dir in exits)
        {
            Assert.True(grid.TryPlaceFree(At.Step(dir), dir, Belt));
        }

        return grid;
    }

    /// <summary>Fills the belt leaving the gate on <paramref name="exit"/> (three items packed at its end, no room left).</summary>
    private void Fill(BeltGrid grid, Direction exit)
    {
        var cell = grid.Cells[At.Step(exit)];
        cell.RestoreState(new[] { 1f, 0.6f, 0.2f }.Select(p => new TransportedItem(nextId++, Iron, p)));
        Assert.False(cell.CanInsert(exit));
    }

    /// <summary>
    /// Inserts an item travelling East (from the West) and says which exit got it (null: refused). The exits in
    /// <paramref name="keep"/> keep what they hold (a full exit stays full); the others are emptied after the pass.
    /// </summary>
    private Direction? PassEast(BeltGrid grid, string itemId = Iron, params Direction[] keep)
    {
        var item = NewItem(itemId);
        if (!grid.TryInsert(At, item, Direction.East))
        {
            Assert.DoesNotContain(grid.Cells.Values, c => c.Items.Contains(item));
            return null;
        }

        Assert.Empty(grid.Cells[At].Items); // no queue: the item is already past the gate
        var exit = DirectionMath.All.Single(d => grid.TryGet(At.Step(d), out var c) && c.Items.Contains(item));
        foreach (var dir in DirectionMath.All.Where(d => !keep.Contains(d)))
        {
            if (grid.TryGet(At.Step(dir), out var cell))
            {
                foreach (var held in cell.Items.ToList())
                {
                    cell.TryRemoveItem(held);
                }
            }
        }

        return exit;
    }

    /// <summary>No terrain (iron everywhere, no walls); Core at (60, 60); both gates researched.</summary>
    private static Factory SandboxFactory() => Unlocked(
        new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(new GridPosition(60, 60))));

    /// <summary>A sandbox game with its Core on (30..32, 30..32); both gates researched.</summary>
    private static Factory CoreFactory() => Unlocked(NewGame.Sandbox(
        Content, new GridPosition(30, 30), mapWidth: 60, mapHeight: 60, rules: new GameRules(InstantBuild: true)));

    private static Factory Unlocked(Factory factory)
    {
        factory.State.Rules = new GameRules(InstantBuild: true);
        factory.UnlockAllForDemo();
        factory.Research.ForceUnlock("overflow-gate");
        factory.Research.ForceUnlock("underflow-gate");
        return factory;
    }

    [Fact]
    public void TheContentHasBothGates()
    {
        Assert.Equal(LogisticsKind.Gate, Overflow.Kind);
        Assert.Equal(LogisticsKind.Gate, Underflow.Kind);
        Assert.False(Overflow.Inverted);
        Assert.True(Underflow.Inverted);
        foreach (var gate in new[] { Overflow, Underflow })
        {
            Assert.Equal(
                new Dictionary<string, int> { ["lead-plate"] = 4, ["copper-wire"] = 2 },
                gate.EffectiveBuildCost.ToDictionary(a => a.ItemId, a => a.Amount));
            Assert.Equal(Sorter.RateItemsPerSecond, gate.RateItemsPerSecond);
            Assert.Equal(Sorter.Capacity, gate.Capacity);
            Assert.Equal(Sorter.ItemSpacing, gate.ItemSpacing);
        }

        Assert.Equal("Overflow gate", Content.RequireStructure("overflow-gate").DisplayName);
        Assert.Equal("Underflow gate", Content.RequireStructure("underflow-gate").DisplayName);
        Assert.Equal(["router"], Content.RequireStructure("overflow-gate").Prerequisites);
        Assert.Equal(["overflow-gate"], Content.RequireStructure("underflow-gate").Prerequisites);
        Assert.Equal(BuildingKind.OverflowGate, BuildSpec.KindFor("overflow-gate"));
        Assert.Equal(BuildingKind.UnderflowGate, BuildSpec.KindFor("underflow-gate"));
    }

    [Fact]
    public void OverflowGoesStraightWhenFrontAccepts()
    {
        var grid = GateWithExits(Overflow, Direction.North, Direction.East, Direction.South);
        for (var n = 0; n < 4; n++)
        {
            Assert.Equal(Direction.East, PassEast(grid));
        }

        Assert.Equal(0, grid.Cells[At].SorterTurns);
    }

    [Fact]
    public void OverflowAlternatesSidesWhenFrontBlocked()
    {
        var grid = GateWithExits(Overflow, Direction.North, Direction.East, Direction.South);
        Fill(grid, Direction.East);
        Assert.Equal(
            new Direction?[] { Left, Right, Left, Right },
            Enumerable.Range(0, 4).Select(_ => PassEast(grid, Copper, Direction.East)).ToArray());

        // Without a belt ahead at all the same: the sides alternate.
        var open = GateWithExits(Overflow, Direction.North, Direction.South);
        Assert.Equal(
            new Direction?[] { Left, Right, Left, Right },
            Enumerable.Range(0, 4).Select(_ => PassEast(open)).ToArray());
    }

    [Fact]
    public void OverflowKeepsTheTurnWhenOnlyOneSideTakes()
    {
        // Only the right side: every item goes there and the bit stays (it flips only when both sides take the item).
        var grid = GateWithExits(Overflow, Right);
        Assert.Equal(Right, PassEast(grid));
        Assert.Equal(Right, PassEast(grid));
        Assert.Equal(0, grid.Cells[At].SorterTurns);
    }

    [Fact]
    public void OverflowWaitsWhenNothingAccepts()
    {
        // Belts x 1..4 heading East into the gate; nothing beyond it.
        var grid = new BeltGrid();
        Assert.True(grid.TryPlaceFree(At, Direction.North, Overflow));
        for (var x = 1; x <= 4; x++)
        {
            Assert.True(grid.TryPlaceFree(new GridPosition(x, 5), Direction.East, Belt));
        }

        var item = NewItem();
        Assert.True(grid.TryInsert(new GridPosition(1, 5), item, Direction.East));
        for (var t = 0; t < 300; t++)
        {
            Tick(grid);
        }

        // The item waits at the end of the feeding belt; nothing lost, nothing on the gate.
        var feeder = grid.Cells[new GridPosition(4, 5)];
        Assert.Equal(1, ItemsOnGrid(grid));
        Assert.Contains(item, feeder.Items);
        Assert.Equal(1f, item.Progress);
        Assert.Empty(grid.Cells[At].Items);

        // Full exits all round: still waiting.
        foreach (var dir in new[] { Direction.North, Direction.East, Direction.South })
        {
            Assert.True(grid.TryPlaceFree(At.Step(dir), dir, Belt));
            Fill(grid, dir);
        }

        var full = ItemsOnGrid(grid);
        Assert.False(grid.TryInsert(At, NewItem(), Direction.East));
        Tick(grid);
        Assert.Contains(item, feeder.Items);
        Assert.Equal(full, ItemsOnGrid(grid));

        // Room ahead: it passes, straight on.
        var ahead = grid.Cells[At.Step(Direction.East)];
        foreach (var held in ahead.Items.ToList())
        {
            ahead.TryRemoveItem(held);
        }

        Tick(grid);
        Assert.DoesNotContain(item, feeder.Items);
        Assert.Contains(item, ahead.Items);
    }

    [Fact]
    public void UnderflowPrefersSides()
    {
        var grid = GateWithExits(Underflow, Direction.North, Direction.East, Direction.South);
        Assert.Equal(
            new Direction?[] { Left, Right, Left, Right, Left, Right },
            Enumerable.Range(0, 6).Select(_ => PassEast(grid)).ToArray());

        // One side full: everything to the other one, never straight.
        Fill(grid, Left);
        for (var n = 0; n < 3; n++)
        {
            Assert.Equal(Right, PassEast(grid, Copper, Left));
        }
    }

    [Fact]
    public void UnderflowGoesStraightWhenSidesBlocked()
    {
        var grid = GateWithExits(Underflow, Direction.North, Direction.East, Direction.South);
        Fill(grid, Left);
        Fill(grid, Right);
        for (var n = 0; n < 3; n++)
        {
            Assert.Equal(Direction.East, PassEast(grid, Iron, Left, Right));
        }

        // No sides at all: straight; nothing free: refused.
        var straightOnly = GateWithExits(Underflow, Direction.East);
        Assert.Equal(Direction.East, PassEast(straightOnly));
        Fill(straightOnly, Direction.East);
        Assert.Null(PassEast(straightOnly, Iron, Direction.East));
    }

    [Fact]
    public void GateDoesNotTakeFromGateOrSorter()
    {
        // Gate (5, 5) → gate (6, 5) → belt (7, 5): the overflow gate's front is a gate, so it never hands on straight.
        var grid = GateWithExits(Overflow);
        var second = At.Step(Direction.East);
        Assert.True(grid.TryPlaceFree(second, Direction.North, Underflow));
        Assert.True(grid.TryPlaceFree(second.Step(Direction.East), Direction.East, Belt));
        Assert.Null(PassEast(grid));

        // With a side free the item goes there instead of into the gate ahead.
        Assert.True(grid.TryPlaceFree(At.Step(Left), Left, Belt));
        Assert.Equal(Left, PassEast(grid));

        // Directly: a gate refuses an item from a gate's or a sorter's tile, takes it from a belt's.
        var sorterAt = second.Step(Direction.South);
        Assert.True(grid.TryPlaceFree(sorterAt, Direction.North, Sorter));
        Assert.True(grid.TryPlaceFree(second.Step(Direction.North), Direction.South, Belt));
        Assert.False(grid.TryInsert(second, NewItem(), source: At));
        Assert.False(grid.TryInsert(second, NewItem(), source: sorterAt));
        Assert.True(grid.TryInsert(second, NewItem(), source: second.Step(Direction.North)));

        // A sorter refuses an item from a gate too.
        Assert.False(grid.TryInsert(sorterAt, NewItem(), source: second));
    }

    [Fact]
    public void ItNeedsToKnowWhereTheItemComesFrom()
    {
        var grid = GateWithExits(Overflow, Direction.North, Direction.East, Direction.South);
        Assert.False(grid.TryInsert(At, NewItem()));
        Assert.False(grid.TryInsert(At, NewItem(), source: new GridPosition(9, 9))); // not beside it
    }

    [Fact]
    public void GateDeliversIntoBuilding()
    {
        // A forno (40..41, 40..41) ahead of an overflow gate (39, 40); the item travels East.
        var factory = CoreFactory();
        Assert.True(factory.TryPlaceSmelter(new GridPosition(40, 40), Direction.East));
        var gate = new GridPosition(39, 40);
        Assert.True(factory.PlaceInstant(
            new BuildSpec("overflow-gate", BuildingKind.OverflowGate, gate, Direction.East), charge: false));
        var smelter = Assert.Single(factory.State.Smelters);
        Assert.True(factory.Belts.TryInsert(gate, NewItem(Iron), Direction.East));
        Assert.Equal(1, smelter.InputBuffer.GetValueOrDefault(Iron));
        Assert.Equal(0, ItemsOnGrid(factory.Belts));

        // An underflow gate beside the Core (30..32, 30..32) with nothing on its sides hands into the Core straight on.
        var under = new GridPosition(29, 31);
        Assert.True(factory.PlaceInstant(
            new BuildSpec("underflow-gate", BuildingKind.UnderflowGate, under, Direction.East), charge: false));
        var stock = factory.Wallet.MaterialCount(Copper);
        Assert.True(factory.State.Storage.CanAccept(Copper));
        Assert.True(factory.Belts.TryInsert(under, NewItem(Copper), Direction.East));
        Assert.Equal(stock + 1, factory.Wallet.MaterialCount(Copper));
        Assert.Equal(0, ItemsOnGrid(factory.Belts));
    }

    [Fact]
    public void ItHasNoDirectionAndIsDraggedLikeTheRouter()
    {
        var factory = SandboxFactory();
        var at = new GridPosition(20, 21);
        Assert.True(factory.PlaceInstant(
            new BuildSpec("overflow-gate", BuildingKind.OverflowGate, at, Direction.West), charge: false));
        Assert.Equal(Direction.North, factory.Belts.Cells[at].Direction);
        Assert.False(factory.Belts.Cells[at].HasDirection);
        Assert.True(factory.Belts.TryOrient(at, Direction.East));
        Assert.Equal(Direction.North, factory.Belts.Cells[at].Direction);

        var line = PlacementLine.Plan(factory, new LineRequest(
            Player, "underflow-gate", new GridPosition(30, 40), new GridPosition(34, 40), Direction.East, false));
        Assert.Equal(5, line.Count);
        Assert.All(line, c => Assert.Equal(Direction.North, c.Direction));
        Assert.All(line, c => Assert.Equal(BuildingKind.UnderflowGate, ((PlaceBuilding)c.Command!).Kind));
    }

    [Theory]
    [InlineData(BuildingKind.OverflowGate, "overflow-gate", false)]
    [InlineData(BuildingKind.UnderflowGate, "underflow-gate", true)]
    public void PlaceBuildingChargesAndRemoveRefunds(BuildingKind kind, string id, bool inverted)
    {
        var factory = SandboxFactory();
        factory.Wallet.AddMaterial("lead-plate", 50);
        factory.Wallet.AddMaterial("copper-wire", 50);
        var lead = factory.Wallet.MaterialCount("lead-plate");
        var wire = factory.Wallet.MaterialCount("copper-wire");
        var host = new SimHost(factory);
        RefusalCode? refused = null;
        host.CommandRejected += (_, r) => refused = r.Code;
        var at = new GridPosition(20, 20);

        host.Submit(new PlaceBuilding(Player, kind, at, Direction.East));
        host.AdvanceTicks(1);
        Assert.Null(refused);
        var cell = factory.Belts.Cells[at];
        Assert.Equal(LogisticsKind.Gate, cell.Kind);
        Assert.Equal(id, cell.Definition.Id);
        Assert.Equal(inverted, cell.Definition.Inverted);
        Assert.Equal(lead - 4, factory.Wallet.MaterialCount("lead-plate"));
        Assert.Equal(wire - 2, factory.Wallet.MaterialCount("copper-wire"));
        Assert.True(factory.TryGetSpecAt(at, out var spec, out _));
        Assert.Equal(kind, spec.Kind);
        Assert.Equal(id, spec.StructureId);

        host.Submit(new Remove(Player, at));
        host.AdvanceTicks(1);
        Assert.Null(refused);
        Assert.False(factory.Belts.Contains(at));
        Assert.Equal(lead, factory.Wallet.MaterialCount("lead-plate"));
        Assert.Equal(wire, factory.Wallet.MaterialCount("copper-wire"));
    }

    [Fact]
    public void GateAlternationSurvivesSave()
    {
        var factory = SandboxFactory();
        var at = new GridPosition(20, 21);
        Assert.True(factory.PlaceInstant(
            new BuildSpec("overflow-gate", BuildingKind.OverflowGate, at, Direction.North), charge: false));
        foreach (var dir in new[] { Direction.North, Direction.South })
        {
            Assert.True(factory.TryPlaceBelt(at.Step(dir), dir));
        }

        // No belt ahead: one item to the left, so the next travelling East goes right.
        Assert.True(factory.Belts.TryInsert(at, NewItem(Copper), Direction.East));
        Assert.Contains(factory.Belts.Cells[at.Step(Left)].Items, i => i.ItemId == Copper);
        Assert.Equal(1 << (int)Direction.East, factory.Belts.Cells[at].SorterTurns);

        var restored = new Factory(Snapshot.Read(Content, Snapshot.Write(factory.State)));
        Assert.Equal(1 << (int)Direction.East, restored.Belts.Cells[at].SorterTurns);
        Assert.Equal("overflow-gate", restored.Belts.Cells[at].Definition.Id);
        Assert.Equal(Snapshot.Write(factory.State), Snapshot.Write(restored.State));
        var loaded = Factory.Restore(Content, factory.Capture());
        Assert.Equal(1 << (int)Direction.East, loaded.Belts.Cells[at].SorterTurns);

        for (var tick = 0; tick < 20; tick++)
        {
            factory.Step();
            restored.Step();
        }

        Assert.Equal(Snapshot.Write(factory.State), Snapshot.Write(restored.State));
        Assert.True(restored.Belts.TryInsert(at, NewItem(Copper), Direction.East));
        Assert.Contains(restored.Belts.Cells[at.Step(Right)].Items, i => i.ItemId == Copper);
    }

    [Fact]
    public void AnOverflowLineFeedsTheSidesOnlyWhenTheFrontBacksUp()
    {
        // Belts x 1..4 East into an overflow gate (5, 5); exits North, East and South (belts going away, 3 cells each).
        // While the East exit is drained every tick everything goes straight; once it is left to fill, the sides run.
        var grid = new BeltGrid();
        Assert.True(grid.TryPlaceFree(At, Direction.North, Overflow));
        for (var i = 1; i <= 4; i++)
        {
            Assert.True(grid.TryPlaceFree(new GridPosition(i, 5), Direction.East, Belt));
        }

        foreach (var dir in new[] { Direction.North, Direction.East, Direction.South })
        {
            for (var k = 1; k <= 3; k++)
            {
                Assert.True(grid.TryPlaceFree(At.Step(dir, k), dir, Belt));
            }
        }

        long fed = 0;
        var straight = 0;
        var sidesWhileFlowing = 0;
        var sidesOnceBacked = 0;
        for (var tick = 0; tick < 1200; tick++)
        {
            fed += grid.TryInsert(new GridPosition(1, 5), NewItem(), Direction.East) ? 1 : 0;
            Tick(grid);
            var drainFront = tick < 600;
            foreach (var dir in new[] { Direction.North, Direction.East, Direction.South })
            {
                var end = grid.Cells[At.Step(dir, 3)];
                if ((dir != Direction.East || drainFront) && end.PeekOutput() is not null)
                {
                    end.RemoveOutput();
                    if (dir == Direction.East)
                    {
                        straight++;
                    }
                    else if (drainFront)
                    {
                        sidesWhileFlowing++;
                    }
                    else
                    {
                        sidesOnceBacked++;
                    }
                }
            }
        }

        Assert.True(straight > 30, $"straight {straight}");
        Assert.Equal(0, sidesWhileFlowing);
        Assert.True(sidesOnceBacked > 30, $"sides {sidesOnceBacked}");
        Assert.Equal(fed, straight + sidesOnceBacked + ItemsOnGrid(grid));
    }
}
