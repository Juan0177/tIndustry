using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Junction sides as the client draws them: a side keeps its rail unless something feeds the junction from there
/// or items cross toward it and a logistics cell there takes them.
/// </summary>
public sealed class JunctionSideTests
{
    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly GridPosition J = new(20, 21);

    private static Factory WithJunction()
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7,
            startingMoney: 100_000, rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        Assert.True(factory.TryPlaceJunction(J, Direction.East));
        return factory;
    }

    private static Direction[] Connected(Factory factory) =>
        new[] { Direction.North, Direction.East, Direction.South, Direction.West }
            .Where(side => factory.IsJunctionSideConnected(J, side))
            .ToArray();

    [Fact]
    public void TwoWayLineOpensOnlyItsAxis()
    {
        var factory = WithJunction();
        Assert.Empty(Connected(factory)); // fills the cache
        Assert.True(factory.TryPlaceBelt(new GridPosition(19, 21), Direction.East)); // feeds from the West
        Assert.True(factory.TryPlaceBelt(new GridPosition(21, 21), Direction.East)); // takes the output
        Assert.Equal([Direction.East, Direction.West], Connected(factory));
    }

    [Fact]
    public void ThreeAndFourWays()
    {
        var factory = WithJunction();
        Assert.True(factory.TryPlaceBelt(new GridPosition(19, 21), Direction.East));
        Assert.True(factory.TryPlaceBelt(new GridPosition(21, 21), Direction.East));
        Assert.True(factory.TryPlaceBelt(new GridPosition(20, 20), Direction.South)); // feeds from the North
        Assert.Equal([Direction.North, Direction.East, Direction.West], Connected(factory));

        Assert.True(factory.TryPlaceBelt(new GridPosition(20, 22), Direction.South)); // takes the North stream
        Assert.Equal([Direction.North, Direction.East, Direction.South, Direction.West], Connected(factory));

        Assert.True(factory.TryRemoveBelt(new GridPosition(19, 21)));
        Assert.Equal([Direction.North, Direction.South], Connected(factory));
    }

    [Fact]
    public void ABeltWithNothingComingTowardItStaysClosed()
    {
        var factory = WithJunction();
        Assert.True(factory.TryPlaceBelt(new GridPosition(21, 21), Direction.East));
        Assert.Empty(Connected(factory));
    }

    [Fact]
    public void ABuildingFeedingASideDoesNotOpenIt()
    {
        // The junction refuses a building's directionless insert, so a miner beside it is no input.
        var factory = WithJunction();
        Assert.True(factory.TryPlaceMiner(new GridPosition(18, 20), Direction.East)); // x 18-19, west of J
        Assert.True(factory.TryPlaceBelt(new GridPosition(21, 21), Direction.East));
        Assert.Empty(Connected(factory));
    }

    [Fact]
    public void ABeltPointingIntoTheSideOpensIt()
    {
        var factory = WithJunction();
        Assert.True(factory.TryPlaceBelt(new GridPosition(20, 22), Direction.North)); // feeds from the South
        Assert.Equal([Direction.South], Connected(factory));
        Assert.False(factory.IsJunctionSideConnected(new GridPosition(20, 22), Direction.North)); // not a junction
    }
}
