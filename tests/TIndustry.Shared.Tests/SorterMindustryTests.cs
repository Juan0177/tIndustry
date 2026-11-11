using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The sorter and the inverted sorter as Mindustry's: no direction, instantaneous with no queue (an item enters only if
/// it leaves in the same tick); the item equal to the filter goes straight on (the side opposite its entry), the others
/// to the left or the right alternating (the inverted sorter the other way round); without a filter the sorter sends
/// everything aside and the inverted one everything straight; a sorter never takes from a sorter.
/// </summary>
public sealed class SorterMindustryTests
{
    private const string Basic = "conveyor-basic";
    private const string Iron = "iron-ore";
    private const string Copper = "copper-ore";
    private const int Player = 0;

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly ConveyorDefinition Belt = Content.RequireConveyor(Basic);
    private static readonly ConveyorDefinition Sorter = Content.RequireConveyor("sorter");
    private static readonly ConveyorDefinition Inverted = Content.RequireConveyor("sorter-inverted");

    private static readonly GridPosition At = new(5, 5);

    private long nextId = 1;

    private TransportedItem NewItem(string itemId = Iron) => new(nextId++, itemId);

    private static void Tick(BeltGrid grid) => grid.Tick(SimulationClock.TickSeconds);

    private static int ItemsOnGrid(BeltGrid grid) => grid.Cells.Values.Sum(c => c.Items.Count);

    /// <summary>A sorter on <see cref="At"/> with a belt leaving on each of <paramref name="exits"/>.</summary>
    private static BeltGrid SorterWithExits(ConveyorDefinition sorter, string? filter, params Direction[] exits)
    {
        var grid = new BeltGrid();
        Assert.True(grid.TryPlaceFree(At, Direction.North, sorter));
        grid.Cells[At].SetFilterItem(filter);
        foreach (var dir in exits)
        {
            Assert.True(grid.TryPlaceFree(At.Step(dir), dir, Belt));
        }

        return grid;
    }

    /// <summary>Inserts an item travelling East (from the West) and says which exit got it (null: refused).</summary>
    private Direction? PassEast(BeltGrid grid, string itemId)
    {
        var item = NewItem(itemId);
        if (!grid.TryInsert(At, item, Direction.East))
        {
            Assert.DoesNotContain(grid.Cells.Values, c => c.Items.Contains(item));
            return null;
        }

        Assert.Empty(grid.Cells[At].Items); // no queue: the item is already past the sorter
        var exit = DirectionMath.All.Single(d => grid.TryGet(At.Step(d), out var c) && c.Items.Contains(item));
        foreach (var dir in DirectionMath.All)
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

    private static Factory SandboxFactory()
    {
        var factory = new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(new GridPosition(60, 60)));
        factory.State.Rules = new GameRules(InstantBuild: true);
        factory.UnlockAllForDemo();
        return factory;
    }

    /// <summary>Runs <paramref name="command"/> for one tick; the refusal or null.</summary>
    private static Refusal? Run(SimHost host, SimCommand command)
    {
        Refusal? refused = null;
        void OnRejected(SimCommand _, Refusal refusal) => refused = refusal;
        host.CommandRejected += OnRejected;
        host.Submit(command);
        host.AdvanceTicks(1);
        host.CommandRejected -= OnRejected;
        return refused;
    }

    [Fact]
    public void TheMatchGoesStraightOn()
    {
        var grid = SorterWithExits(Sorter, Iron, Direction.North, Direction.East, Direction.South);
        for (var n = 0; n < 4; n++)
        {
            Assert.Equal(Direction.East, PassEast(grid, Iron));
        }
    }

    [Fact]
    public void TheOthersAlternateLeftAndRight()
    {
        var grid = SorterWithExits(Sorter, Iron, Direction.North, Direction.East, Direction.South);
        var left = DirectionMath.Left(Direction.East);
        var right = DirectionMath.Right(Direction.East);
        Assert.Equal(
            new Direction?[] { left, right, left, right, left, right },
            Enumerable.Range(0, 6).Select(_ => PassEast(grid, Copper)).ToArray());
    }

    [Fact]
    public void ABlockedSideKeepsTheTurnAsMindustry()
    {
        // No exit on the right: the items go left without flipping the turn; once the right opens, the turn still says
        // left, then they alternate (Mindustry flips only when both sides take the item).
        var left = DirectionMath.Left(Direction.East);
        var right = DirectionMath.Right(Direction.East);
        var grid = SorterWithExits(Sorter, Iron, Direction.East, left);
        Assert.Equal(left, PassEast(grid, Copper));
        Assert.Equal(left, PassEast(grid, Copper));
        Assert.Equal(0, grid.Cells[At].SorterTurns);

        Assert.True(grid.TryPlaceFree(At.Step(right), right, Belt));
        Assert.Equal(
            new Direction?[] { left, right, left, right },
            Enumerable.Range(0, 4).Select(_ => PassEast(grid, Copper)).ToArray());
    }

    [Fact]
    public void ABuildingThatWouldNotTakeTheItemDoesNotFlipTheTurn()
    {
        // Sorter (31, 29) above the Core (30..32, 30..32): travelling West its left is the Core (South), its right a
        // belt (North). The Core is full of copper.
        var factory = NewGame.Sandbox(
            Content, new GridPosition(30, 30), mapWidth: 60, mapHeight: 60, rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        var at = new GridPosition(31, 29);
        var north = at.Step(Direction.North);
        Assert.True(factory.TryPlaceSorter(at, Direction.North, Iron));
        Assert.True(factory.TryPlaceBelt(north, Direction.North));
        Assert.Equal(Direction.South, DirectionMath.Left(Direction.West));
        var storage = factory.State.Storage;
        factory.Wallet.AddMaterial(Copper, storage.Capacity(Copper) - factory.Wallet.MaterialCount(Copper));
        Assert.False(storage.CanAccept(Copper));

        void Clear()
        {
            foreach (var held in factory.Belts.Cells[north].Items.ToList())
            {
                factory.Belts.Cells[north].TryRemoveItem(held);
            }
        }

        // Only the belt would take it: it goes there and the turn stays on the left (the Core).
        Assert.True(factory.Belts.TryInsert(at, NewItem(Copper), Direction.West));
        Assert.Single(factory.Belts.Cells[north].Items);
        Assert.Equal(0, factory.Belts.Cells[at].SorterTurns);
        Clear();

        // Room in the Core: both would, so it goes left into the Core and the turn flips; the next goes to the belt.
        Assert.True(factory.Wallet.TryRemoveMaterial(Copper, 1));
        var stock = factory.Wallet.MaterialCount(Copper);
        Assert.True(factory.Belts.TryInsert(at, NewItem(Copper), Direction.West));
        Assert.Equal(stock + 1, factory.Wallet.MaterialCount(Copper));
        Assert.Empty(factory.Belts.Cells[north].Items);
        Assert.True(factory.Belts.Cells[at].SorterTurnsRight(Direction.West));
        Assert.True(factory.Wallet.TryRemoveMaterial(Copper, 1));
        Assert.True(factory.Belts.TryInsert(at, NewItem(Copper), Direction.West));
        Assert.Single(factory.Belts.Cells[north].Items);
        Assert.Equal(0, factory.Belts.Cells[at].SorterTurns);
    }

    [Fact]
    public void TwoOppositeInputsEachAlternateTheirOwnItems()
    {
        // Inputs from the West and the East, one item each every 30 ticks (far below a belt's rate); no filter, so all
        // aside to the belts North and South. Each input's items alternate between the two sides on their own turn bit.
        var grid = new BeltGrid();
        Assert.True(grid.TryPlaceFree(At, Direction.North, Sorter));
        for (var i = 1; i <= 4; i++)
        {
            Assert.True(grid.TryPlaceFree(new GridPosition(i, 5), Direction.East, Belt));
            Assert.True(grid.TryPlaceFree(new GridPosition(10 - i, 5), Direction.West, Belt));
            Assert.True(grid.TryPlaceFree(new GridPosition(5, i), Direction.North, Belt));
            Assert.True(grid.TryPlaceFree(new GridPosition(5, 10 - i), Direction.South, Belt));
        }

        var fromWest = new HashSet<long>();
        var west = new List<Direction>();
        var east = new List<Direction>();
        for (var tick = 0; tick < 1200; tick++)
        {
            if (tick % 30 == 0)
            {
                var a = NewItem(Iron);
                Assert.True(grid.TryInsert(new GridPosition(1, 5), a, Direction.East));
                fromWest.Add(a.Id);
                Assert.True(grid.TryInsert(new GridPosition(9, 5), NewItem(Copper), Direction.West));
            }

            Tick(grid);
            foreach (var end in new[] { new GridPosition(5, 1), new GridPosition(5, 9) })
            {
                if (grid.Cells[end].PeekOutput() is not null)
                {
                    grid.Cells[end].RemoveOutput();
                }
            }

            foreach (var (exit, side) in new[] { (new GridPosition(5, 4), Direction.North), (new GridPosition(5, 6), Direction.South) })
            {
                foreach (var item in grid.Cells[exit].Items.Where(i => !seen.Contains(i.Id)).ToList())
                {
                    seen.Add(item.Id);
                    (fromWest.Contains(item.Id) ? west : east).Add(side);
                }
            }
        }

        Assert.True(west.Count > 30 && east.Count > 30, $"{west.Count} / {east.Count}");
        for (var n = 1; n < west.Count; n++)
        {
            Assert.NotEqual(west[n - 1], west[n]);
        }

        for (var n = 1; n < east.Count; n++)
        {
            Assert.NotEqual(east[n - 1], east[n]);
        }
    }

    private readonly HashSet<long> seen = [];

    [Fact]
    public void ABlockedSideSendsToTheOther()
    {
        // No exit on the left: every other item goes right; with the right full too, nothing enters.
        var right = DirectionMath.Right(Direction.East);
        var grid = SorterWithExits(Sorter, Iron, Direction.East, right);
        for (var n = 0; n < 4; n++)
        {
            Assert.Equal(right, PassEast(grid, Copper));
        }

        // A belt on the left facing into the sorter takes nothing from it (Mindustry's conveyor).
        var left = DirectionMath.Left(Direction.East);
        Assert.True(grid.TryPlaceFree(At.Step(left), DirectionMath.Opposite(left), Belt));
        Assert.Equal(right, PassEast(grid, Copper));
        Assert.Equal(right, PassEast(grid, Copper));
    }

    [Fact]
    public void WithNoSideFreeTheItemDoesNotEnter()
    {
        var grid = SorterWithExits(Sorter, Iron, Direction.East);
        Assert.Null(PassEast(grid, Copper));

        // Both sides full: the item stays out.
        var full = SorterWithExits(Sorter, Iron, Direction.North, Direction.East, Direction.South);
        Assert.True(full.TryInsert(At.Step(Direction.North), NewItem(), Direction.North));
        Assert.True(full.TryInsert(At.Step(Direction.South), NewItem(), Direction.South));
        var item = NewItem(Copper);
        Assert.False(full.TryInsert(At, item, Direction.East));
        Assert.Equal(2, ItemsOnGrid(full));

        // A match with the straight exit missing does not enter either (never aside).
        var noStraight = SorterWithExits(Sorter, Iron, Direction.North, Direction.South);
        Assert.Null(PassEast(noStraight, Iron));
    }

    [Fact]
    public void WithoutAFilterTheSorterSendsAllAsideAndTheInvertedAllStraight()
    {
        var sorter = SorterWithExits(Sorter, null, Direction.North, Direction.East, Direction.South);
        Assert.Null(sorter.Cells[At].FilterItemId);
        var aside = Enumerable.Range(0, 4).Select(_ => PassEast(sorter, Iron)).ToList();
        Assert.DoesNotContain(Direction.East, aside.Cast<Direction>());
        Assert.Equal(2, aside.Count(d => d == Direction.North));

        var inverted = SorterWithExits(Inverted, null, Direction.North, Direction.East, Direction.South);
        for (var n = 0; n < 4; n++)
        {
            Assert.Equal(Direction.East, PassEast(inverted, n % 2 == 0 ? Iron : Copper));
        }

        var invertedNoStraight = SorterWithExits(Inverted, null, Direction.North, Direction.South);
        Assert.Null(PassEast(invertedNoStraight, Iron));
    }

    [Fact]
    public void TheInvertedSorterSendsTheMatchAsideAndTheOthersStraight()
    {
        var grid = SorterWithExits(Inverted, Iron, Direction.North, Direction.East, Direction.South);
        Assert.Equal(Direction.East, PassEast(grid, Copper));
        Assert.Equal(DirectionMath.Left(Direction.East), PassEast(grid, Iron));
        Assert.Equal(Direction.East, PassEast(grid, Copper));
        Assert.Equal(DirectionMath.Right(Direction.East), PassEast(grid, Iron));
    }

    [Fact]
    public void ASorterTakesNothingFromASorter()
    {
        // Sorter (5, 5) → sorter (6, 5) → belt (7, 5): the match would go straight into the second sorter.
        var grid = SorterWithExits(Sorter, Iron);
        Assert.True(grid.TryPlaceFree(At.Step(Direction.East), Direction.North, Inverted));
        Assert.True(grid.TryPlaceFree(At.Step(Direction.East, 2), Direction.East, Belt));
        Assert.Null(PassEast(grid, Iron));

        // Directly, from the first sorter's tile, the second refuses too; from the North (a belt there) it takes, straight
        // on South (no filter on the inverted one).
        var second = At.Step(Direction.East);
        Assert.True(grid.TryPlaceFree(second.Step(Direction.North), Direction.South, Belt));
        Assert.True(grid.TryPlaceFree(second.Step(Direction.South), Direction.South, Belt));
        Assert.False(grid.TryInsert(second, NewItem(Copper), source: At));
        Assert.True(grid.TryInsert(second, NewItem(Copper), source: second.Step(Direction.North)));
        Assert.Single(grid.Cells[second.Step(Direction.South)].Items);
    }

    [Fact]
    public void ItNeedsToKnowWhereTheItemComesFrom()
    {
        var grid = SorterWithExits(Sorter, Iron, Direction.North, Direction.East, Direction.South);
        Assert.False(grid.TryInsert(At, NewItem()));
        Assert.False(grid.TryInsert(At, NewItem(), source: new GridPosition(9, 9))); // not beside it
    }

    [Fact]
    public void TwoInputsInTheSameTickPassOneItemEachInAFixedOrder()
    {
        // Belts from the West (x 1..4, East) and the East (x 6..9, West) into the sorter; no filter: all aside,
        // to the belts North (y 1..4) and South (y 6..9) going away. Each input feeds every tick.
        static (List<long> North, List<long> South, long Fed, int Left) Run()
        {
            var grid = new BeltGrid();
            Assert.True(grid.TryPlaceFree(At, Direction.North, Sorter));
            for (var i = 1; i <= 4; i++)
            {
                Assert.True(grid.TryPlaceFree(new GridPosition(i, 5), Direction.East, Belt));
                Assert.True(grid.TryPlaceFree(new GridPosition(10 - i, 5), Direction.West, Belt));
                Assert.True(grid.TryPlaceFree(new GridPosition(5, i), Direction.North, Belt));
                Assert.True(grid.TryPlaceFree(new GridPosition(5, 10 - i), Direction.South, Belt));
            }

            var north = new List<long>();
            var south = new List<long>();
            long id = 1;
            long fed = 0;
            for (var tick = 0; tick < 1200; tick++)
            {
                fed += grid.TryInsert(new GridPosition(1, 5), new TransportedItem(id++, Iron), Direction.East) ? 1 : 0;
                fed += grid.TryInsert(new GridPosition(9, 5), new TransportedItem(id++, Copper), Direction.West) ? 1 : 0;
                Tick(grid);
                Assert.Empty(grid.Cells[At].Items);
                if (grid.Cells[new GridPosition(5, 1)].PeekOutput() is { } n)
                {
                    north.Add(n.Id);
                    grid.Cells[new GridPosition(5, 1)].RemoveOutput();
                }

                if (grid.Cells[new GridPosition(5, 9)].PeekOutput() is { } s)
                {
                    south.Add(s.Id);
                    grid.Cells[new GridPosition(5, 9)].RemoveOutput();
                }
            }

            return (north, south, fed, ItemsOnGrid(grid));
        }

        var first = Run();
        var second = Run();
        Assert.Equal(first.North, second.North);
        Assert.Equal(first.South, second.South);

        // Nothing lost or made; both inputs flow and both exits run.
        Assert.Equal(first.Fed, first.North.Count + first.South.Count + first.Left);
        // A basic belt carries 2.1 / 0.4 = 5.25 items/s: both exits run full, about 85 items in 20 s after the ~4 s the
        // lines take to fill.
        Assert.True(first.North.Count >= 80 && first.South.Count >= 80, $"{first.North.Count} / {first.South.Count}");
        var all = first.North.Concat(first.South).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.Contains(all, i => i % 2 == 1); // West input (odd ids first in each tick)
        Assert.Contains(all, i => i % 2 == 0); // East input
    }

    [Fact]
    public void ItHasNoDirection()
    {
        var factory = SandboxFactory();
        Assert.True(factory.TryPlaceSorter(new GridPosition(20, 21), Direction.West));
        Assert.Equal(Direction.North, factory.Belts.Cells[new GridPosition(20, 21)].Direction);
        Assert.True(factory.Belts.TryOrient(new GridPosition(20, 21), Direction.East));
        Assert.Equal(Direction.North, factory.Belts.Cells[new GridPosition(20, 21)].Direction);
        Assert.False(factory.Belts.Cells[new GridPosition(20, 21)].HasDirection);

        var line = PlacementLine.Plan(factory, new LineRequest(
            Player, "sorter-inverted", new GridPosition(30, 30), new GridPosition(34, 30), Direction.East, false));
        Assert.Equal(5, line.Count);
        Assert.All(line, c => Assert.Equal(Direction.North, c.Direction));
        Assert.All(line, c => Assert.Equal(BuildingKind.SorterInverted, ((PlaceBuilding)c.Command!).Kind));
    }

    [Theory]
    [InlineData("sorter")]
    [InlineData("sorter-inverted")]
    public void ADraggedLineOfSortersOverAPerpendicularBeltReplacesItWithoutACrossing(string id)
    {
        // A belt column x = 40, y 30..36 heading South; a sorter line dragged East along y 33 across it.
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
            host.State, new LineRequest(Player, id, new GridPosition(37, 33), new GridPosition(44, 33), Direction.East));
        Assert.All(line, c => Assert.IsType<PlaceBuilding>(c.Command));
        // A sorter over a belt replaces it (Mindustry's replace, LogisticsReplaceTests).
        Assert.DoesNotContain(line, c => c.Refusal is not null);
        foreach (var command in PlacementLine.Commands(line))
        {
            host.Submit(command);
        }

        host.AdvanceTicks(1);

        Assert.Empty(refusals);
        Assert.Equal(LogisticsKind.Sorter, factory.Belts.Cells[crossing].Kind);
        Assert.DoesNotContain(factory.Belts.Cells.Values, c => c.Kind == LogisticsKind.Junction);
        var sorters = factory.Belts.Cells.Values.Where(c => c.Kind == LogisticsKind.Sorter).ToList();
        Assert.Equal(8, sorters.Count);
        Assert.All(sorters, c => Assert.Equal(id, c.Definition.Id));
        Assert.All(sorters, c => Assert.Equal(Direction.North, c.Direction));
    }

    [Fact]
    public void ItIsPlacedWithoutAFilter()
    {
        var factory = SandboxFactory();
        factory.AddBuilder(Player);
        var host = new SimHost(factory);
        Assert.Null(Run(host, new PlaceBuilding(Player, BuildingKind.Sorter, new GridPosition(20, 20), Direction.East)));
        Assert.Null(Run(host, new PlaceBuilding(Player, BuildingKind.SorterInverted, new GridPosition(22, 20), Direction.East)));
        Assert.Null(factory.Belts.Cells[new GridPosition(20, 20)].FilterItemId);
        Assert.Null(factory.Belts.Cells[new GridPosition(22, 20)].FilterItemId);
        Assert.True(factory.Belts.Cells[new GridPosition(22, 20)].Definition.Inverted);
        Assert.True(factory.TryGetSpecAt(new GridPosition(22, 20), out var spec, out _));
        Assert.Equal(BuildingKind.SorterInverted, spec.Kind);
        Assert.Equal("sorter-inverted", spec.StructureId);

        // With a filter it keeps it; an unknown item is refused.
        Assert.Null(Run(host, new PlaceBuilding(Player, BuildingKind.Sorter, new GridPosition(24, 20), Direction.East, FilterItemId: "coal")));
        Assert.Equal("coal", factory.Belts.Cells[new GridPosition(24, 20)].FilterItemId);
        Assert.Equal(
            RefusalCode.InvalidCommand,
            Run(host, new PlaceBuilding(Player, BuildingKind.Sorter, new GridPosition(26, 20), Direction.East, FilterItemId: "non-esiste"))?.Code);
        Assert.False(factory.Belts.Contains(new GridPosition(26, 20)));
    }

    [Fact]
    public void SetBlockFilterSetsAndClearsTheFilterOfBothSorters()
    {
        var factory = SandboxFactory();
        var host = new SimHost(factory);
        var sorter = new GridPosition(20, 20);
        var inverted = new GridPosition(22, 20);
        Assert.True(factory.TryPlaceSorter(sorter, Direction.East));
        Assert.True(factory.TryPlaceSorterInverted(inverted, Direction.East));

        Assert.Null(Run(host, new SetBlockFilter(Player, sorter, "coal")));
        Assert.Equal("coal", factory.Belts.Cells[sorter].FilterItemId);
        Assert.Null(Run(host, new SetBlockFilter(Player, inverted, "titanium-ore")));
        Assert.Equal("titanium-ore", factory.Belts.Cells[inverted].FilterItemId);
        Assert.Null(Run(host, new SetBlockFilter(Player, sorter, null)));
        Assert.Null(factory.Belts.Cells[sorter].FilterItemId);
    }

    [Fact]
    public void SetBlockFilterIsRefusedOnABlockWithoutAFilterOrWithAnUnknownItem()
    {
        var factory = SandboxFactory();
        var host = new SimHost(factory);
        var sorter = new GridPosition(20, 20);
        Assert.True(factory.TryPlaceSorter(sorter, Direction.East, "coal"));
        Assert.True(factory.TryPlaceBelt(new GridPosition(25, 25), Direction.East));
        Assert.True(factory.TryPlaceJunction(new GridPosition(27, 25), Direction.East));
        Assert.True(factory.TryPlaceRouter(new GridPosition(29, 25), Direction.East));
        foreach (var bad in new SimCommand[]
                 {
                     new SetBlockFilter(Player, new GridPosition(25, 25), "coal"), // plain belt
                     new SetBlockFilter(Player, new GridPosition(27, 25), "coal"), // junction
                     new SetBlockFilter(Player, new GridPosition(29, 25), null),   // router
                     new SetBlockFilter(Player, new GridPosition(40, 40), "coal"), // empty tile
                     new SetBlockFilter(Player, sorter, "non-esiste"),             // unknown item
                     new SetBlockFilter(Player, sorter, "")                        // blank is not "none"
                 })
        {
            Assert.Equal(RefusalCode.InvalidCommand, Run(host, bad)?.Code);
        }

        Assert.Equal("coal", factory.Belts.Cells[sorter].FilterItemId);
    }

    [Fact]
    public void TheInvertedSorterIsResearchedAfterTheSorterAndCostsTheSame()
    {
        Assert.Equal(LogisticsKind.Sorter, Inverted.Kind);
        Assert.True(Inverted.Inverted);
        Assert.False(Sorter.Inverted);
        Assert.Equal(Sorter.EffectiveBuildCost, Inverted.EffectiveBuildCost);
        Assert.Equal(Sorter.MoneyCost, Inverted.MoneyCost);
        var structure = Content.RequireStructure("sorter-inverted");
        var sorter = Content.RequireStructure("sorter");
        Assert.Equal(["sorter"], structure.Prerequisites);
        Assert.Equal(sorter.Unlock!.Money, structure.Unlock!.Money);
        Assert.Equal(sorter.Unlock.Materials, structure.Unlock.Materials);
        Assert.Equal(BuildingKind.SorterInverted, BuildSpec.KindFor("sorter-inverted"));
        Assert.Equal("sorter-inverted", Factory.StructureIdForTool("sorter-inverted"));
        Assert.Contains("sorter-inverted", ResearchState.DemoStructureIds);
    }

    [Fact]
    public void ItHandsTheMatchToTheCore()
    {
        // Core (30..32, 30..32); a line East on y 31 into a sorter at (29, 31) beside it, filter iron.
        var factory = NewGame.Sandbox(
            Content, new GridPosition(30, 30), mapWidth: 60, mapHeight: 60, rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        Assert.True(factory.TryPlaceSorter(new GridPosition(29, 31), Direction.North, Iron));
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
    public void AMinerFeedsAnInvertedSorterBelowIt()
    {
        // No terrain: full iron everywhere. Miner (7..8, 14..15); inverted sorter without filter (7, 16) below it, so
        // all straight on: belts (7, 17..18) South.
        var factory = new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(new GridPosition(30, 30)));
        factory.UnlockAllForDemo();
        Assert.True(factory.TryPlaceMiner(new GridPosition(7, 14), Direction.East));
        Assert.True(factory.TryPlaceSorterInverted(new GridPosition(7, 16), Direction.North));
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
        Assert.Empty(factory.Belts.Cells[new GridPosition(7, 16)].Items);
    }

    [Fact]
    public void TheFilterAndTheTurnSurviveSaveAndLoad()
    {
        var factory = SandboxFactory();
        var at = new GridPosition(20, 21);
        var bare = new GridPosition(24, 21);
        Assert.True(factory.TryPlaceSorter(at, Direction.North, Iron));
        Assert.True(factory.TryPlaceSorterInverted(bare, Direction.North));
        foreach (var dir in new[] { Direction.North, Direction.East, Direction.South })
        {
            Assert.True(factory.TryPlaceBelt(at.Step(dir), dir));
        }

        // One copper to the left: the next travelling East goes right.
        Assert.True(factory.Belts.TryInsert(at, NewItem(Copper), Direction.East));
        Assert.True(factory.Belts.Cells[at].SorterTurnsRight(Direction.East));
        Assert.Equal(1 << (int)Direction.East, factory.Belts.Cells[at].SorterTurns);

        var restored = Factory.Restore(Content, factory.Capture());
        Assert.Equal(Iron, restored.Belts.Cells[at].FilterItemId);
        Assert.Equal(1 << (int)Direction.East, restored.Belts.Cells[at].SorterTurns);
        Assert.Null(restored.Belts.Cells[bare].FilterItemId);
        Assert.True(restored.Belts.Cells[bare].Definition.Inverted);
        Assert.Equal(Snapshot.Write(factory.State), Snapshot.Write(restored.State));
        for (var tick = 0; tick < 20; tick++)
        {
            factory.Step();
            restored.Step();
        }

        Assert.True(restored.Belts.TryInsert(at, NewItem(Copper), Direction.East));
        Assert.Contains(restored.Belts.Cells[at.Step(DirectionMath.Right(Direction.East))].Items, i => i.ItemId == Copper);
    }
}
