using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Auto-junction as in Mindustry: a dragged belt line (<see cref="PlaceBelt.Crossing"/>) that goes on past a straight
/// plain belt perpendicular to it puts a junction on that cell, paid as a junction, only where the junction behaves
/// like the belt it replaces. Parallel belts, specials, corners, belts touching buildings and marked belts stay.
/// </summary>
public sealed class AutoJunctionTests
{
    private const int Player = 0;
    private const string Iron = "iron-plate";

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    /// <summary>Core 2×2 at (20, 20).</summary>
    private static readonly GridPosition CoreOrigin = new(20, 20);

    private static readonly GridPosition Crossing = new(40, 33);

    private static (Factory Factory, SimHost Host, List<Refusal> Refusals) NewGame(
        bool instantBuild = true, bool junctionUnlocked = true, int iron = 1000)
    {
        var wallet = new EconomyWallet(
            0, new Dictionary<string, int>(StringComparer.Ordinal) { [Iron] = iron, ["copper-wire"] = 100 });
        var factory = new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(CoreOrigin, 2), wallet);
        factory.State.Rules = new GameRules(InstantBuild: instantBuild);
        foreach (var id in ResearchState.DemoStructureIds)
        {
            factory.Research.ForceUnlock(id);
        }

        if (!junctionUnlocked)
        {
            factory.Research.ForceLock("junction");
        }

        factory.AddBuilder(Player);
        var host = new SimHost(factory);
        var refusals = new List<Refusal>();
        host.CommandRejected += (_, refusal) => refusals.Add(refusal);
        return (factory, host, refusals);
    }

    private static void Belt(Factory factory, int x, int y, Direction dir, string id = BuildSpec.DefaultConveyorId) =>
        Assert.True(factory.PlaceInstant(new BuildSpec(id, null, new GridPosition(x, y), dir), charge: false));

    /// <summary>The existing line: x = 40, y 30..36, heading South (free of buildings at both ends).</summary>
    private static void SouthLine(Factory factory)
    {
        for (var y = 30; y <= 36; y++)
        {
            Belt(factory, 40, y, Direction.South);
        }
    }

    /// <summary>
    /// What the client sends on releasing a drag (World.SendLine): <see cref="PlacementLine.Commands"/> of the line from
    /// the first to the last of <paramref name="cells"/>, all in one tick; <paramref name="dir"/> is fixed on every cell
    /// (R during the drag) only when it is not the line's own way. Returns the line.
    /// </summary>
    private static IReadOnlyList<LinePlacement> Drag(SimHost host, IReadOnlyList<GridPosition> cells, Direction dir)
    {
        var way = BeltLane.DirectionBetween(cells[0], cells[1]);
        var line = PlacementLine.Plan(host.State, new LineRequest(
            Player, BuildSpec.DefaultConveyorId, cells[0], cells[^1], dir, FixedDirection: dir != way));
        foreach (var command in PlacementLine.Commands(line))
        {
            host.Submit(command);
        }

        host.AdvanceTicks(1);
        return line;
    }

    private static GridPosition[] Row(int y, int x0, int x1) =>
        Enumerable.Range(x0, x1 - x0 + 1).Select(x => new GridPosition(x, y)).ToArray();

    private static GridPosition? WhereIs(Factory factory, long itemId) =>
        factory.Belts.Cells.Values.FirstOrDefault(c => c.Items.Any(i => i.Id == itemId))?.Position;

    private static int RefundOfBelt(Factory factory) =>
        Content.Conveyors.First(c => c.Id == BuildSpec.DefaultConveyorId).EffectiveBuildCost.Single().Amount
        * factory.RefundBasisPointsFor(new BuildSpec(BuildSpec.DefaultConveyorId, null, Crossing, Direction.South))
        / Construction.FullRefundBasisPoints;

    [Fact]
    public void DragAcrossAPerpendicularBeltMakesAJunctionPaidAsOneAndBothLinesFlow()
    {
        var (factory, host, refusals) = NewGame();
        SouthLine(factory);
        var before = factory.Wallet.MaterialCount(Iron);
        Drag(host, Row(33, 37, 43), Direction.East);

        Assert.Empty(refusals);
        Assert.Equal(LogisticsKind.Junction, factory.Belts.Cells[Crossing].Kind);
        // Six new belts (1 plate each), the junction (2), the replaced belt refunded.
        Assert.Equal(6 + 2 - RefundOfBelt(factory), before - factory.Wallet.MaterialCount(Iron));

        Assert.True(factory.Belts.TryInsert(new GridPosition(37, 33), new TransportedItem(1, "iron-ore")));
        Assert.True(factory.Belts.TryInsert(new GridPosition(40, 30), new TransportedItem(2, "copper-ore")));
        host.AdvanceTicks(60 * 30);
        Assert.Equal(new GridPosition(43, 33), WhereIs(factory, 1));
        Assert.Equal(new GridPosition(40, 36), WhereIs(factory, 2));
    }

    [Fact]
    public void WithPlansTheDroneTakesTheBeltApartAndBuildsTheJunctionPaidAsOne()
    {
        var (factory, host, refusals) = NewGame(instantBuild: false);
        SouthLine(factory);
        var before = factory.Wallet.MaterialCount(Iron);
        Drag(host, Row(33, 37, 43), Direction.East);
        Assert.Empty(refusals);
        var plans = factory.State.Builders[0].Plans;
        Assert.Equal(8, plans.Count); // six belts, the breaking plan and right after it the junction plan
        var breaking = plans.ToList().FindIndex(p => p.Breaking);
        Assert.Equal(Crossing, plans[breaking].Spec.Origin);
        Assert.Equal(BuildingKind.Junction, plans[breaking + 1].Spec.Kind);
        Assert.Equal(Crossing, plans[breaking + 1].Spec.Origin);

        for (var tick = 0; tick < 40_000 && factory.State.Builders[0].Plans.Count > 0; tick++)
        {
            host.AdvanceTicks(1);
        }

        Assert.Empty(factory.State.Builders[0].Plans);
        Assert.Empty(factory.Sites);
        Assert.Equal(LogisticsKind.Junction, factory.Belts.Cells[Crossing].Kind);
        Assert.Equal(6 + 2 - RefundOfBelt(factory), before - factory.Wallet.MaterialCount(Iron));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellingEitherPlanOfACrossingCancelsBoth(bool onlyMark)
    {
        var (factory, host, _) = NewGame(instantBuild: false);
        SouthLine(factory);
        Drag(host, Row(33, 37, 43), Direction.East);
        Assert.Equal(8, factory.State.Builders[0].Plans.Count);

        host.Submit(new Remove(Player, Crossing, onlyMark));
        host.AdvanceTicks(1);

        var plans = factory.State.Builders[0].Plans;
        Assert.Equal(6, plans.Count);
        Assert.DoesNotContain(plans, p => p.Spec.Origin == Crossing);
        Assert.Equal(LogisticsKind.Belt, factory.Belts.Cells[Crossing].Kind);
    }

    [Fact]
    public void NotEnoughPlatesForTheJunctionLeavesTheBeltWithPlans()
    {
        var (factory, host, refusals) = NewGame(instantBuild: false, iron: 1);
        SouthLine(factory);
        Drag(host, Row(33, 37, 43), Direction.East);

        Assert.Equal(RefusalCode.InsufficientResources, Assert.Single(refusals).Code);
        Assert.DoesNotContain(factory.State.Builders[0].Plans, p => p.Spec.Origin == Crossing);
        Assert.Equal(LogisticsKind.Belt, factory.Belts.Cells[Crossing].Kind);
    }

    [Theory]
    [InlineData(Direction.South)]
    [InlineData(Direction.North)]
    public void DragAlongAParallelBeltTurnsItWithoutJunctions(Direction existing)
    {
        var (factory, host, refusals) = NewGame();
        for (var y = 30; y <= 36; y++)
        {
            Belt(factory, 40, y, existing);
        }

        Drag(host, Enumerable.Range(28, 11).Select(y => new GridPosition(40, y)).ToArray(), Direction.South);

        Assert.Empty(refusals);
        Assert.All(Enumerable.Range(30, 7), y => Assert.Equal(Direction.South, factory.Belts.Cells[new GridPosition(40, y)].Direction));
        Assert.DoesNotContain(factory.Belts.Cells.Values, c => c.Kind == LogisticsKind.Junction);
    }

    [Fact]
    public void FixedDirectionDragAlongAPerpendicularLineConvertsNothing()
    {
        // Brush East, mouse moving down the South line: no cell is inside an East line.
        var (factory, host, refusals) = NewGame();
        SouthLine(factory);
        Drag(host, Enumerable.Range(29, 9).Select(y => new GridPosition(40, y)).ToArray(), Direction.East);

        Assert.DoesNotContain(factory.Belts.Cells.Values, c => c.Kind == LogisticsKind.Junction);
        Assert.All(refusals, r => Assert.Equal(RefusalCode.Occupied, r.Code));
    }

    [Fact]
    public void ADragEndingOnTheBeltDoesNotConvertIt()
    {
        var (factory, host, refusals) = NewGame();
        SouthLine(factory);
        Drag(host, Row(33, 37, 40), Direction.East);

        Assert.Empty(refusals);
        Assert.Equal(LogisticsKind.Belt, factory.Belts.Cells[Crossing].Kind);
        Assert.Equal(Direction.South, factory.Belts.Cells[Crossing].Direction);
    }

    [Fact]
    public void WhereTheLineCrossesIsTheLinesCall()
    {
        // No belt of the line on either side: whether the cell lies inside the line is PlacementLine's to say, so two
        // crossings in a row both become junctions.
        var (factory, _, _) = NewGame();
        SouthLine(factory);

        Assert.Equal(BeltCrossing.Junction, factory.BeltCrossingAt(Crossing, Direction.East));
    }

    [Theory]
    [InlineData(BuildingKind.Splitter)]
    [InlineData(BuildingKind.Sorter)]
    [InlineData(BuildingKind.Junction)]
    [InlineData(BuildingKind.Bridge)]
    public void DragOverASpecialKeepsTodaysBehaviour(BuildingKind kind)
    {
        var (factory, host, refusals) = NewGame();
        host.Submit(new PlaceBuilding(Player, kind, Crossing, Direction.South));
        host.AdvanceTicks(1);
        Assert.Empty(refusals);
        var before = factory.Belts.Cells[Crossing].Kind;

        var line = Drag(host, Row(33, 37, 43), Direction.East);

        // The line foresees the refusal: the cell is shown red and not sent.
        Assert.Contains(line, c => c.Cell == Crossing && c.Refusal == RefusalCode.Occupied);
        Assert.Empty(refusals);
        Assert.Equal(before, factory.Belts.Cells[Crossing].Kind);
        Assert.Equal(BeltCrossing.None, factory.BeltCrossingAt(Crossing, Direction.East));
    }

    [Fact]
    public void ASingleClickOnAPerpendicularBeltTurnsIt()
    {
        var (factory, host, refusals) = NewGame();
        SouthLine(factory);
        host.Submit(new PlaceBelt(Player, Crossing, Direction.East, BuildSpec.DefaultConveyorId));
        host.AdvanceTicks(1);

        Assert.Empty(refusals);
        Assert.Equal(LogisticsKind.Belt, factory.Belts.Cells[Crossing].Kind);
        Assert.Equal(Direction.East, factory.Belts.Cells[Crossing].Direction);
    }

    [Fact]
    public void LockedJunctionTurnsTheCrossedBeltIntoAMerge()
    {
        var (factory, host, refusals) = NewGame(junctionUnlocked: false);
        SouthLine(factory);
        Drag(host, Row(33, 37, 43), Direction.East);

        Assert.Empty(refusals);
        Assert.Equal(LogisticsKind.Belt, factory.Belts.Cells[Crossing].Kind);
        Assert.Equal(Direction.East, factory.Belts.Cells[Crossing].Direction);
        Assert.Equal(BeltShape.JoinLeft, factory.GetBeltShape(Crossing));
        Assert.Equal(LogisticsKind.Belt, factory.Belts.Cells[new GridPosition(41, 33)].Kind);
    }

    [Fact]
    public void ABeltAlreadyMarkedForRemovalIsNeverConverted()
    {
        var (factory, host, _) = NewGame(instantBuild: false);
        SouthLine(factory);
        host.Submit(new Remove(Player, Crossing));
        host.AdvanceTicks(1);
        Drag(host, Row(33, 37, 43), Direction.East);

        Assert.DoesNotContain(factory.State.Builders[0].Plans, p => p.Spec.Kind == BuildingKind.Junction);
        Assert.Single(factory.State.Builders[0].Plans, p => p.Breaking);
    }

    [Fact]
    public void ACornerIsNeverConverted()
    {
        // The crossed belt turns at the crossing (fed only from its side): a junction would not turn.
        var (factory, host, _) = NewGame();
        Belt(factory, 39, 33, Direction.East);
        Belt(factory, 40, 33, Direction.South);
        Belt(factory, 40, 34, Direction.South);
        Assert.Equal(BeltShape.Corner, factory.GetBeltShape(Crossing));
        Assert.Equal(BeltCrossing.None, factory.BeltCrossingAt(Crossing, Direction.East));
    }

    [Fact]
    public void ABeltIntoTheCoreIsNeverConvertedOnlyTurned()
    {
        // The Core covers (20..21, 20..21): the crossed belt (22, 21) heads West into it, the drag goes North along x = 22.
        var (factory, host, refusals) = NewGame();
        Belt(factory, 23, 21, Direction.West);
        Belt(factory, 22, 21, Direction.West); // feeds the Core
        Drag(host, Enumerable.Range(19, 5).Select(y => new GridPosition(22, y)).Reverse().ToArray(), Direction.North);

        Assert.Equal(LogisticsKind.Belt, factory.Belts.Cells[new GridPosition(22, 21)].Kind);
        Assert.Equal(Direction.North, factory.Belts.Cells[new GridPosition(22, 21)].Direction);
        Assert.Empty(refusals);
    }

    [Fact]
    public void ABeltIntoASmelterOrFedByAMinerIsNeverConverted()
    {
        var (factory, host, _) = NewGame();
        // Into a smelter at (41..42, 33..34): the crossed belt (40, 33) heads East into it.
        Belt(factory, 39, 33, Direction.East);
        Belt(factory, 40, 33, Direction.East);
        Assert.True(factory.PlaceInstant(new BuildSpec(Crafter.SmelterBuildingId, BuildingKind.Smelter, new GridPosition(41, 33), Direction.East), charge: false));
        Assert.Equal(BeltCrossing.None, factory.BeltCrossingAt(Crossing, Direction.North));

        // Fed by a miner behind it: (50, 33) heads East, the miner covers (48..49, 32..33).
        var fed = new GridPosition(50, 33);
        Assert.True(factory.PlaceInstant(new BuildSpec(Miner.BasicId, BuildingKind.Miner, new GridPosition(48, 32), Direction.East), charge: false));
        Belt(factory, 50, 33, Direction.East);
        Belt(factory, 51, 33, Direction.East);
        Belt(factory, 50, 34, Direction.North);
        Belt(factory, 50, 32, Direction.North);
        Assert.Equal(BeltCrossing.None, factory.BeltCrossingAt(fed, Direction.North));
    }

    [Theory]
    [InlineData("conveyor-fast")]
    [InlineData("conveyor-express")]
    public void AJunctionNeverCapsTheLineItCrosses(string conveyorId)
    {
        double Rate(bool withJunction)
        {
            var (factory, host, _) = NewGame();
            // A West line from x = 45 into the Core's east side (22, 20).
            for (var x = 22; x <= 45; x++)
            {
                if (withJunction && x == 33)
                {
                    Assert.True(factory.PlaceInstant(new BuildSpec("junction", BuildingKind.Junction, new GridPosition(x, 20), Direction.West), charge: false));
                    continue;
                }

                Belt(factory, x, 20, Direction.West, conveyorId);
            }

            long next = 1;
            long delivered = 0;
            for (var tick = 0; tick < 60 * 80; tick++)
            {
                factory.Belts.TryInsert(new GridPosition(45, 20), new TransportedItem(next, "iron-ore"));
                if (factory.Belts.Cells[new GridPosition(45, 20)].Items.Any(i => i.Id == next))
                {
                    next++;
                }

                if (tick == 60 * 20)
                {
                    delivered = factory.CoreDeliveredItems;
                }

                host.AdvanceTicks(1);
            }

            return (factory.CoreDeliveredItems - delivered) / 60.0;
        }

        var plain = Rate(withJunction: false);
        var crossed = Rate(withJunction: true);
        Assert.True(plain > 0.5, $"{conveyorId}: {plain} oggetti/s");
        Assert.InRange(crossed, plain * 0.95, plain * 1.05);
    }
}
