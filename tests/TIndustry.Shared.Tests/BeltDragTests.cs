using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The drag's automatic crossing (<see cref="BeltDrag"/>), shared by the client's mouse drag and the agents' <c>piazza</c>
/// line: both send the same cells with the same crossing flag, so they end in the same game.
/// </summary>
public sealed class BeltDragTests
{
    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly GridPosition Crossing = new(40, 33);

    /// <summary>A sandbox with a belt line x = 40, y 30..36 heading south (Core far away at 60,60).</summary>
    private static SimHost NewHost(bool junctionUnlocked)
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, startingMoney: 1000,
            rules: new GameRules(InstantBuild: true));
        if (junctionUnlocked)
        {
            factory.Research.ForceUnlock("junction");
        }
        else
        {
            factory.Research.ForceLock("junction");
        }

        for (var y = 30; y <= 36; y++)
        {
            Assert.True(factory.PlaceInstant(
                new BuildSpec(BuildSpec.DefaultConveyorId, null, new GridPosition(40, y), Direction.South), charge: false));
        }

        return new SimHost(factory);
    }

    private static JsonElement Piazza(AgentGame game, string json) => game.Call("piazza", JsonDocument.Parse(json).RootElement);

    private const string EastLine = """{"tier":1,"da":{"x":37,"y":33},"a":{"x":43,"y":33},"direzione":"est"}""";

    [Fact]
    public void PiazzaCrossesAnUnlockedBeltLikeTheClientsDrag()
    {
        var agent = NewHost(junctionUnlocked: true);
        using var game = new AgentGame(agent, Content);

        var result = Piazza(game, EastLine);

        Assert.Empty(result.GetProperty("rifiuti").EnumerateArray());
        var crossing = result.GetProperty("celle").EnumerateArray().Single(c => c.GetProperty("x").GetInt32() == Crossing.X);
        Assert.True(crossing.GetProperty("incrocio").GetBoolean());
        Assert.Equal(LogisticsKind.Junction, agent.State.Belts[Crossing].Kind);

        // The client: a mouse drag over the same cells, fed cell by cell to the same BeltDrag.
        var client = NewHost(junctionUnlocked: true);
        var drag = new BeltDrag();
        var from = new GridPosition(37, 33);
        drag.Begin(from);
        client.Submit(new PlaceBelt(0, from, Direction.East, BuildSpec.DefaultConveyorId));
        foreach (var cell in DragLine.Cells(from, new GridPosition(43, 33)))
        {
            foreach (var (place, isCrossing) in drag.Next(client.State, cell, Direction.East, belts: true))
            {
                client.Submit(new PlaceBelt(0, place, Direction.East, BuildSpec.DefaultConveyorId, isCrossing));
            }
        }

        client.AdvanceTicks(1);
        Assert.Equal(client.Checksum(), agent.Checksum());
    }

    [Fact]
    public void ALockedJunctionIsRefusedAsBefore()
    {
        var host = NewHost(junctionUnlocked: false);
        using var game = new AgentGame(host, Content);

        var result = Piazza(game, EastLine);

        var refused = Assert.Single(result.GetProperty("rifiuti").EnumerateArray());
        Assert.Equal(Crossing.X, refused.GetProperty("x").GetInt32());
        Assert.Equal(nameof(RefusalCode.CrossingNeedsJunction), refused.GetProperty("codice").GetString());
        Assert.Equal(6, result.GetProperty("celle").GetArrayLength());
        Assert.Equal(LogisticsKind.Belt, host.State.Belts[Crossing].Kind);
        Assert.Equal(Direction.South, host.State.Belts[Crossing].Direction);
    }

    [Fact]
    public void BuildingsInALineNeverCross()
    {
        var host = NewHost(junctionUnlocked: true);

        var (place, dropped) = BeltDrag.Line(host.State, new GridPosition(37, 33), new GridPosition(43, 33), Direction.East, belts: false);

        Assert.Equal(Enumerable.Range(37, 7).Select(x => (new GridPosition(x, 33), false)), place);
        Assert.Null(dropped);
    }

    [Fact]
    public void ALineEndingOnABeltDropsThatCell()
    {
        var host = NewHost(junctionUnlocked: true);

        var (place, dropped) = BeltDrag.Line(host.State, new GridPosition(37, 33), Crossing, Direction.East, belts: true);

        Assert.Equal(new[] { 37, 38, 39 }, place.Select(p => p.Cell.X));
        Assert.Equal(Crossing, dropped);
    }
}
