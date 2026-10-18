using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Auto-junction as in Mindustry: a dragged belt line (<see cref="PlaceBelt.Crossing"/>) that crosses a plain belt
/// perpendicular to it puts a junction on that cell, paid as a junction; parallel belts and specials are left alone.
/// </summary>
public sealed class AutoJunctionTests
{
    private const int Player = 0;

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly GridPosition Crossing = new(40, 33);

    private static (Factory Factory, SimHost Host, List<Refusal> Refusals) NewGame(
        bool instantBuild = true, bool junctionUnlocked = true)
    {
        var wallet = new EconomyWallet(
            0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["iron-plate"] = 1000, ["copper-wire"] = 100 });
        var factory = new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(new GridPosition(20, 20), 2), wallet);
        factory.State.Rules = new GameRules(InstantBuild: instantBuild);
        foreach (var id in ResearchState.DemoStructureIds)
        {
            if (junctionUnlocked || id != "junction")
            {
                factory.Research.ForceUnlock(id);
            }
        }

        factory.AddBuilder(Player);
        var host = new SimHost(factory);
        var refusals = new List<Refusal>();
        host.CommandRejected += (_, refusal) => refusals.Add(refusal);
        return (factory, host, refusals);
    }

    /// <summary>The existing line: x = 40, y 30..36, heading South.</summary>
    private static void SouthLine(SimHost host)
    {
        for (var y = 30; y <= 36; y++)
        {
            host.Submit(new PlaceBelt(Player, new GridPosition(40, y), Direction.South, BuildSpec.DefaultConveyorId));
        }

        host.AdvanceTicks(1);
    }

    /// <summary>Drags an East line along y = 33 from x 37 to 43 (the first cell is the click, the rest the drag).</summary>
    private static void DragEast(SimHost host, Direction direction = Direction.East)
    {
        for (var x = 37; x <= 43; x++)
        {
            host.Submit(new PlaceBelt(Player, new GridPosition(x, 33), direction, BuildSpec.DefaultConveyorId, Crossing: x > 37));
        }

        host.AdvanceTicks(1);
    }

    private static GridPosition? WhereIs(Factory factory, long itemId) =>
        factory.Belts.Cells.Values.FirstOrDefault(c => c.Items.Any(i => i.Id == itemId))?.Position;

    [Fact]
    public void DragAcrossAPerpendicularBeltMakesAJunctionAndBothLinesFlow()
    {
        var (factory, host, refusals) = NewGame();
        SouthLine(host);
        var ironBefore = factory.Wallet.MaterialCount("iron-plate");
        DragEast(host);

        Assert.Empty(refusals);
        Assert.Equal(LogisticsKind.Junction, factory.Belts.Cells[Crossing].Kind);
        // Six new belts (1 plate each) and a junction (2) paid, the replaced belt refunded (its plate or less).
        Assert.InRange(ironBefore - factory.Wallet.MaterialCount("iron-plate"), 7, 8);

        Assert.True(factory.Belts.TryInsert(new GridPosition(37, 33), new TransportedItem(1, "iron-ore")));
        Assert.True(factory.Belts.TryInsert(new GridPosition(40, 30), new TransportedItem(2, "copper-ore")));
        host.AdvanceTicks(60 * 30);
        Assert.Equal(new GridPosition(43, 33), WhereIs(factory, 1));
        Assert.Equal(new GridPosition(40, 36), WhereIs(factory, 2));
    }

    [Theory]
    [InlineData(Direction.South)]
    [InlineData(Direction.North)]
    public void DragAlongAParallelBeltKeepsTodaysBehaviour(Direction existing)
    {
        var (factory, host, refusals) = NewGame();
        host.Submit(new PlaceBelt(Player, Crossing, existing, BuildSpec.DefaultConveyorId));
        host.AdvanceTicks(1);
        host.Submit(new PlaceBelt(Player, Crossing, Direction.South, BuildSpec.DefaultConveyorId, Crossing: true));
        host.AdvanceTicks(1);

        Assert.Equal(RefusalCode.Occupied, Assert.Single(refusals).Code);
        Assert.Equal(LogisticsKind.Belt, factory.Belts.Cells[Crossing].Kind);
        Assert.Equal(existing, factory.Belts.Cells[Crossing].Direction);
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

        host.Submit(new PlaceBelt(Player, Crossing, Direction.East, BuildSpec.DefaultConveyorId, Crossing: true));
        host.AdvanceTicks(1);

        Assert.Equal(RefusalCode.Occupied, Assert.Single(refusals).Code);
        Assert.Equal(before, factory.Belts.Cells[Crossing].Kind);
        Assert.Equal(BeltCrossing.None, factory.BeltCrossingAt(Crossing, Direction.East));
    }

    [Fact]
    public void ASingleClickOnABeltKeepsTodaysBehaviour()
    {
        var (factory, host, refusals) = NewGame();
        SouthLine(host);
        host.Submit(new PlaceBelt(Player, Crossing, Direction.East, BuildSpec.DefaultConveyorId));
        host.AdvanceTicks(1);

        Assert.Equal(RefusalCode.Occupied, Assert.Single(refusals).Code);
        Assert.Equal(LogisticsKind.Belt, factory.Belts.Cells[Crossing].Kind);
        Assert.Equal(Direction.South, factory.Belts.Cells[Crossing].Direction);
    }

    [Fact]
    public void LockedJunctionLeavesTheBeltAndSaysWhy()
    {
        var (factory, host, refusals) = NewGame(junctionUnlocked: false);
        SouthLine(host);
        Assert.Equal(BeltCrossing.JunctionLocked, factory.BeltCrossingAt(Crossing, Direction.East));
        DragEast(host);

        Assert.Equal(RefusalCode.CrossingNeedsJunction, Assert.Single(refusals).Code);
        Assert.Equal(LogisticsKind.Belt, factory.Belts.Cells[Crossing].Kind);
        Assert.Equal(Direction.South, factory.Belts.Cells[Crossing].Direction);
        Assert.Equal(LogisticsKind.Belt, factory.Belts.Cells[new GridPosition(41, 33)].Kind);
    }

    [Fact]
    public void WithPlansTheDroneTakesTheBeltApartAndBuildsTheJunction()
    {
        var (factory, host, refusals) = NewGame(instantBuild: false);
        for (var y = 30; y <= 36; y++)
        {
            factory.PlaceInstant(new BuildSpec(BuildSpec.DefaultConveyorId, null, new GridPosition(40, y), Direction.South), charge: false);
        }

        host.Submit(new PlaceBelt(Player, Crossing, Direction.East, BuildSpec.DefaultConveyorId, Crossing: true));
        host.AdvanceTicks(1);
        Assert.Empty(refusals);
        var plans = factory.State.Builders[0].Plans;
        Assert.Equal(2, plans.Count);
        Assert.True(plans[0].Breaking);
        Assert.Equal(BuildingKind.Junction, plans[1].Spec.Kind);

        for (var tick = 0; tick < 20_000 && !(factory.Belts.TryGet(Crossing, out var cell) && cell.Kind == LogisticsKind.Junction); tick++)
        {
            host.AdvanceTicks(1);
        }

        Assert.Equal(LogisticsKind.Junction, factory.Belts.Cells[Crossing].Kind);
        Assert.Empty(factory.State.Builders[0].Plans);
    }
}
