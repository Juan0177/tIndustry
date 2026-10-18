using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Belt corners as in Mindustry: a belt with nothing feeding it from behind and exactly one feeder on a
/// side (a belt or a building that ejects into it) is drawn as a curve from that side.
/// </summary>
public sealed class BeltCornerTests
{
    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    // Belt cell under test; its left/right neighbours are (19,21) and (21,21).
    private static readonly GridPosition P = new(20, 21);

    private static Factory Sandbox()
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7,
            startingMoney: 100_000, rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        return factory;
    }

    /// <summary>P heads North and continues to (20,20): a line leaving northwards.</summary>
    private static Factory WithNorthLine()
    {
        var factory = Sandbox();
        Assert.True(factory.TryPlaceBelt(P, Direction.North));
        Assert.True(factory.TryPlaceBelt(new GridPosition(20, 20), Direction.North));
        return factory;
    }

    private static void AssertCorner(Factory factory, Direction expectedIncoming)
    {
        Assert.True(factory.IsBeltCorner(P));
        Assert.True(factory.TryGetBeltIncomingDirection(P, out var incoming));
        Assert.Equal(expectedIncoming, incoming);
    }

    [Fact]
    public void MinerOnTheSideMakesACorner()
    {
        var factory = WithNorthLine();
        Assert.True(factory.TryPlaceMiner(new GridPosition(18, 20), Direction.East)); // x 18-19, west of P
        AssertCorner(factory, Direction.East);
    }

    [Fact]
    public void SmelterOnTheSideMakesACorner()
    {
        var factory = WithNorthLine();
        Assert.True(factory.TryPlaceSmelter(new GridPosition(21, 21), Direction.East)); // x 21-22, east of P
        AssertCorner(factory, Direction.West);
    }

    [Fact]
    public void AssemblerOnTheSideMakesACorner()
    {
        var factory = WithNorthLine();
        Assert.True(factory.TryPlaceAssembler(new GridPosition(18, 21), Direction.East));
        AssertCorner(factory, Direction.East);
    }

    [Fact]
    public void ExtractorFacingTheBeltMakesACorner()
    {
        var factory = WithNorthLine();
        Assert.True(factory.TryPlaceExtractor(new GridPosition(19, 21), Direction.East));
        AssertCorner(factory, Direction.East);

        var other = WithNorthLine();
        Assert.True(other.TryPlaceExtractor(new GridPosition(19, 21), Direction.West)); // ejects away from P
        Assert.False(other.IsBeltCorner(P));
    }

    [Fact]
    public void FeederBehindWinsOverABuildingOnTheSide()
    {
        var factory = WithNorthLine();
        Assert.True(factory.TryPlaceBelt(new GridPosition(20, 22), Direction.North));
        Assert.True(factory.TryPlaceMiner(new GridPosition(18, 20), Direction.East));
        Assert.False(factory.IsBeltCorner(P));
    }

    [Fact]
    public void FeederBehindWinsOverABeltOnTheSide()
    {
        // P heads South: the side feeder from the west (flowing East) used to win by enum order.
        var factory = Sandbox();
        Assert.True(factory.TryPlaceBelt(P, Direction.South));
        Assert.True(factory.TryPlaceBelt(new GridPosition(20, 20), Direction.South));
        Assert.True(factory.TryPlaceBelt(new GridPosition(19, 21), Direction.East));
        Assert.False(factory.IsBeltCorner(P));
    }

    [Fact]
    public void BuildingsOnBothSidesAndNothingBehindStayStraight()
    {
        var factory = WithNorthLine();
        Assert.True(factory.TryPlaceMiner(new GridPosition(18, 20), Direction.East));
        Assert.True(factory.TryPlaceSmelter(new GridPosition(21, 21), Direction.East));
        Assert.False(factory.IsBeltCorner(P));
    }

    [Fact]
    public void PowerNodeOnTheSideStaysStraight()
    {
        var factory = WithNorthLine();
        Assert.True(factory.TryPlacePowerNode(new GridPosition(19, 21)));
        Assert.False(factory.IsBeltCorner(P));
    }

    [Fact]
    public void CoreOnTheSideStaysStraight()
    {
        // Core covers (60..61, 60..61): a belt along its east edge heading North.
        var factory = Sandbox();
        var beside = new GridPosition(62, 61);
        Assert.True(factory.TryPlaceBelt(beside, Direction.North));
        Assert.True(factory.TryPlaceBelt(new GridPosition(62, 60), Direction.North));
        Assert.False(factory.IsBeltCorner(beside));
    }

    [Fact]
    public void BeltToBeltCornerIsUnchanged()
    {
        var factory = WithNorthLine();
        Assert.True(factory.TryPlaceBelt(new GridPosition(19, 21), Direction.East));
        AssertCorner(factory, Direction.East);
        Assert.False(factory.IsBeltCorner(new GridPosition(20, 20)));
    }

    [Fact]
    public void SplitterSideExitMakesACorner()
    {
        var factory = WithNorthLine();
        Assert.True(factory.TryPlaceSplitter(new GridPosition(19, 21), Direction.North)); // exits West and East
        AssertCorner(factory, Direction.East);
    }

    [Fact]
    public void JunctionCountsOnlyWithAFeederBehindIt()
    {
        var factory = WithNorthLine();
        Assert.True(factory.TryPlaceJunction(new GridPosition(19, 21), Direction.East));
        Assert.False(factory.IsBeltCorner(P));
        Assert.True(factory.TryPlaceBelt(new GridPosition(18, 21), Direction.East));
        AssertCorner(factory, Direction.East);
    }

    [Fact]
    public void RemovingTheBuildingStraightensTheBelt()
    {
        var factory = WithNorthLine();
        Assert.True(factory.TryPlaceMiner(new GridPosition(18, 20), Direction.East));
        Assert.True(factory.IsBeltCorner(P));
        Assert.True(factory.TryRemoveBuildingAt(new GridPosition(19, 21)));
        Assert.False(factory.IsBeltCorner(P));
    }
}
