using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// A belt placed over a belt of the same tier, as in Mindustry: another direction turns it in place (at once, free,
/// items kept); the same direction does nothing, silently. Busy cells (marked, planned, specials) stay refused.
/// </summary>
public sealed class BeltOverBeltTests
{
    private const int Player = 0;
    private const string Basic = BuildSpec.DefaultConveyorId;

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly GridPosition Cell = new(10, 10);

    /// <summary>Sandbox with the Core far away (60, 60).</summary>
    private static (Factory Factory, SimHost Host, List<Refusal> Refusals) NewHost(bool instantBuild)
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, startingMoney: 1000,
            rules: new GameRules(InstantBuild: instantBuild));
        foreach (var id in ResearchState.DemoStructureIds)
        {
            factory.Research.ForceUnlock(id);
        }

        var host = new SimHost(factory);
        var refusals = new List<Refusal>();
        host.CommandRejected += (_, refusal) => refusals.Add(refusal);
        return (factory, host, refusals);
    }

    private static void Belt(Factory factory, GridPosition at, Direction dir) =>
        Assert.True(factory.PlaceInstant(new BuildSpec(Basic, null, at, dir), charge: false));

    private static void Place(SimHost host, Direction dir)
    {
        host.Submit(new PlaceBelt(Player, Cell, dir, Basic));
        host.AdvanceTicks(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SameTierOtherDirectionTurnsInPlaceKeepingItems(bool instantBuild)
    {
        var (factory, host, refusals) = NewHost(instantBuild);
        Belt(factory, Cell, Direction.East);
        Assert.True(factory.Belts.TryInsert(Cell, new TransportedItem(7, "iron-ore")));
        var money = factory.Wallet.Money;
        var plates = factory.Wallet.MaterialCount("iron-plate");

        Place(host, Direction.North);

        Assert.Empty(refusals);
        Assert.Equal(Direction.North, factory.Belts.Cells[Cell].Direction);
        Assert.Contains(factory.Belts.Cells[Cell].Items, i => i.Id == 7);
        Assert.Equal(money, factory.Wallet.Money);
        Assert.Equal(plates, factory.Wallet.MaterialCount("iron-plate"));
        Assert.Empty(factory.PlansOf(Player));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SameTierSameDirectionIsASilentNoOp(bool instantBuild)
    {
        var (factory, host, refusals) = NewHost(instantBuild);
        var (control, controlHost, _) = NewHost(instantBuild);
        Belt(factory, Cell, Direction.East);
        Belt(control, Cell, Direction.East);

        Place(host, Direction.East);
        controlHost.AdvanceTicks(1);

        Assert.Empty(refusals);
        Assert.Equal(controlHost.Checksum(), host.Checksum());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TurningInPlaceRefreshesTheShape(bool instantBuild)
    {
        // (10, 9) heads South into the side of (10, 10) East: a corner; turned South, it is fed from behind.
        var (factory, host, _) = NewHost(instantBuild);
        Belt(factory, new GridPosition(10, 9), Direction.South);
        Belt(factory, Cell, Direction.East);
        Assert.Equal(BeltShape.Corner, factory.GetBeltShape(Cell));

        Place(host, Direction.South);

        Assert.Equal(BeltShape.Straight, factory.GetBeltShape(Cell));
    }

    [Theory]
    [InlineData(true, "marked")]
    [InlineData(false, "marked")]
    [InlineData(true, "site")]
    [InlineData(false, "site")]
    [InlineData(true, "otherPlayersPlan")]
    [InlineData(false, "otherPlayersPlan")]
    [InlineData(true, "junction")]
    [InlineData(false, "junction")]
    [InlineData(true, "splitter")]
    [InlineData(false, "splitter")]
    [InlineData(true, "sorter")]
    [InlineData(false, "sorter")]
    [InlineData(true, "bridge")]
    [InlineData(false, "bridge")]
    public void BusyCellsAreNeverTurnedOrReplaced(bool instantBuild, string busy)
    {
        var (factory, host, refusals) = NewHost(instantBuild);
        var belt = new BuildSpec(Basic, null, Cell, Direction.East);
        switch (busy)
        {
            case "marked":
                Belt(factory, Cell, Direction.East);
                factory.BuilderOf(Player)!.PlanQueue.Add(new BuildPlan(belt, Breaking: true));
                break;
            case "site":
                Construction.BeginBuild(factory.State, belt, Player);
                break;
            case "otherPlayersPlan":
                Belt(factory, Cell, Direction.East);
                factory.AddBuilder(1);
                factory.BuilderOf(1)!.PlanQueue.Add(new BuildPlan(belt with { Direction = Direction.South }, Breaking: false));
                break;
            case "junction":
                Assert.True(factory.PlaceInstant(new BuildSpec("junction", BuildingKind.Junction, Cell, Direction.East), charge: false));
                break;
            case "splitter":
                Assert.True(factory.PlaceInstant(new BuildSpec("splitter", BuildingKind.Splitter, Cell, Direction.East), charge: false));
                break;
            case "sorter":
                Assert.True(factory.PlaceInstant(new BuildSpec("sorter", BuildingKind.Sorter, Cell, Direction.East), charge: false));
                break;
            case "bridge":
                Assert.True(factory.PlaceInstant(new BuildSpec("conveyor-bridge", BuildingKind.Bridge, Cell, Direction.East), charge: false));
                break;
        }

        factory.Belts.Cells.TryGetValue(Cell, out var before);
        Assert.Equal(BeltOverBelt.None, factory.BeltOverBeltAt(Cell, Direction.North, Basic));

        Place(host, Direction.North);

        Assert.Equal(RefusalCode.Occupied, Assert.Single(refusals).Code);
        factory.Belts.Cells.TryGetValue(Cell, out var after);
        Assert.Equal(before?.Kind, after?.Kind);
        Assert.Equal(before?.Direction, after?.Direction);
    }
}
