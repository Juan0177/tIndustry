using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The placement line shared by the client's drag and the agents' <c>piazza</c> (<see cref="PlacementLine"/>): a straight
/// line on the dominant axis, each cell facing the next, crossings inside the line, refusals foreseen and not sent.
/// </summary>
public sealed class PlacementLineTests
{
    private const string Belt = BuildSpec.DefaultConveyorId;

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    /// <summary>Core 2×2 on (60..61, 60..61).</summary>
    private static Factory NewFactory() =>
        NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, startingMoney: 1000,
            rules: new GameRules(InstantBuild: true));

    private static void PlaceBelt(Factory factory, int x, int y, Direction direction) =>
        Assert.True(factory.PlaceInstant(new BuildSpec(Belt, null, new GridPosition(x, y), direction), charge: false));

    private static IReadOnlyList<LinePlacement> Plan(
        IGameView view, (int X, int Y) anchor, (int X, int Y) cursor, Direction brush, string structureId = Belt,
        bool fixedDirection = false) =>
        PlacementLine.Plan(view, new LineRequest(
            0, structureId, new GridPosition(anchor.X, anchor.Y), new GridPosition(cursor.X, cursor.Y), brush, fixedDirection));

    [Fact]
    public void StraightTakesTheDominantAxis()
    {
        Assert.Equal(Enumerable.Range(5, 5).Select(x => new GridPosition(x, 5)),
            PlacementLine.Straight(new GridPosition(5, 5), new GridPosition(9, 7)));
        Assert.Equal(Enumerable.Range(5, 5).Select(y => new GridPosition(5, y)),
            PlacementLine.Straight(new GridPosition(5, 5), new GridPosition(6, 9)));
        Assert.All(PlacementLine.Straight(new GridPosition(5, 5), new GridPosition(8, 8)), c => Assert.Equal(5, c.Y));
        Assert.Equal([new GridPosition(5, 5)], PlacementLine.Straight(new GridPosition(5, 5), new GridPosition(5, 5)));
    }

    [Fact]
    public void LineIsCappedAtMaxCells()
    {
        Assert.Equal(PlacementLine.MaxCells, PlacementLine.Straight(new GridPosition(0, 0), new GridPosition(500, 0)).Count);
        Assert.Equal(200, PlacementLine.MaxCells);
        Assert.Equal(AgentGame.MaxListItems, PlacementLine.MaxCells);
    }

    [Fact]
    public void EveryBeltFacesTheNextCell()
    {
        var line = Plan(NewFactory(), (10, 10), (10, 6), Direction.East);

        Assert.Equal(Enumerable.Range(6, 5).Reverse().Select(y => new GridPosition(10, y)), line.Select(c => c.Cell));
        Assert.All(line, c => Assert.Equal(Direction.North, c.Direction));
        Assert.All(line, c => Assert.Null(c.Refusal));
        Assert.Equal(5, PlacementLine.Commands(line).Count);
    }

    [Fact]
    public void LastCellTakesTheBeltItEndsOn()
    {
        var factory = NewFactory();
        PlaceBelt(factory, 10, 6, Direction.West);

        var line = Plan(factory, (10, 10), (10, 6), Direction.East);

        Assert.All(line.Take(4), c => Assert.Equal(Direction.North, c.Direction));
        Assert.Equal(Direction.West, line[^1].Direction);
        Assert.Equal(BeltOverBelt.Same, factory.BeltOverBeltAt(new GridPosition(10, 6), Direction.West, Belt));
        // Not dropped nor reported as occupied (the old drag did): it is sent, and changes nothing.
        Assert.Null(line[^1].Refusal);
        Assert.Equal(5, PlacementLine.Commands(line).Count);
    }

    [Fact]
    public void SingleClickUsesTheBrush()
    {
        var line = Plan(NewFactory(), (10, 10), (10, 10), Direction.South);

        var cell = Assert.Single(line);
        Assert.Equal(Direction.South, cell.Direction);
        Assert.Equal(new PlaceBelt(0, new GridPosition(10, 10), Direction.South, Belt), cell.Command);
    }

    [Fact]
    public void FixedDirectionWinsEverywhere()
    {
        var factory = NewFactory();
        PlaceBelt(factory, 10, 6, Direction.West);

        var line = Plan(factory, (10, 10), (10, 6), Direction.East, fixedDirection: true);

        Assert.Equal(5, line.Count);
        Assert.All(line, c => Assert.Equal(Direction.East, c.Direction));
        Assert.All(line, c => Assert.False(((PlaceBelt)c.Command!).Crossing));
    }

    [Theory]
    [InlineData(BuildingKind.Bridge)]
    [InlineData(BuildingKind.Assembler)]
    [InlineData(BuildingKind.Extractor)]
    [InlineData(BuildingKind.Miner)]
    public void NonLineBlocksFaceTheDragDirection(BuildingKind kind)
    {
        var factory = NewFactory();
        var id = BuildSpec.StructureIdFor(kind)!;

        var dragged = Assert.Single(Plan(factory, (10, 10), (10, 4), Direction.East, id));
        var clicked = Assert.Single(Plan(factory, (10, 10), (10, 10), Direction.East, id));

        Assert.Equal(new PlaceBuilding(0, kind, new GridPosition(10, 10), Direction.North), dragged.Command);
        Assert.Equal(new PlaceBuilding(0, kind, new GridPosition(10, 10), Direction.East), clicked.Command);
    }

    [Fact]
    public void SpecialsFaceTheNextCellToo()
    {
        var line = Plan(NewFactory(), (10, 10), (10, 14), Direction.East, BuildSpec.StructureIdFor(BuildingKind.Sorter)!);

        Assert.Equal(5, line.Count);
        Assert.All(line, c => Assert.Equal(Direction.South, c.Direction));
        Assert.All(line, c => Assert.IsType<PlaceBuilding>(c.Command));
    }

    [Fact]
    public void CoreRuleOnlyOnTheLastCell()
    {
        // Up the Core's west side (x 59): (59,61) touches the Core but goes on North; the last cell faces the Core.
        var line = Plan(NewFactory(), (59, 64), (59, 60), Direction.South);

        Assert.All(line.Take(4), c => Assert.Equal(Direction.North, c.Direction));
        Assert.Equal(Direction.East, line[^1].Direction);
        Assert.Equal(Direction.East, Assert.Single(Plan(NewFactory(), (59, 60), (59, 60), Direction.West)).Direction);
        Assert.Equal(Direction.North, Plan(NewFactory(), (59, 64), (59, 60), Direction.North, fixedDirection: true)[^1].Direction);
    }

    [Fact]
    public void ClickOnABeltBesideTheCoreTurnsItLikeAnyBelt()
    {
        // Over a belt the Core rule does not apply: it turns in place as asked (Mindustry), in the plan and in the game.
        var factory = NewFactory();
        PlaceBelt(factory, 59, 60, Direction.East);
        var host = new SimHost(factory);

        var cell = Assert.Single(Plan(factory, (59, 60), (59, 60), Direction.North));
        Assert.Equal(Direction.North, cell.Direction);
        Assert.Null(cell.Refusal);
        host.Submit(cell.Command!);
        host.AdvanceTicks(1);

        Assert.Equal(Direction.North, factory.Belts.Cells[new GridPosition(59, 60)].Direction);
    }

    /// <summary>South columns on x 40 and 41 (y 30..36), the junction researched, an East line across them on y 33.</summary>
    private static (Factory Factory, IReadOnlyList<LinePlacement> Line) CrossTwoColumns()
    {
        var factory = NewFactory();
        factory.Research.ForceUnlock("junction");
        for (var y = 30; y <= 36; y++)
        {
            PlaceBelt(factory, 40, y, Direction.South);
            PlaceBelt(factory, 41, y, Direction.South);
        }

        return (factory, Plan(factory, (37, 33), (44, 33), Direction.North));
    }

    [Fact]
    public void InteriorCrossingIsMarked()
    {
        var (_, line) = CrossTwoColumns();

        Assert.Equal([40, 41], line.Where(c => ((PlaceBelt)c.Command!).Crossing).Select(c => c.Cell.X));
        Assert.All(line, c => Assert.Equal(Direction.East, c.Direction));
        Assert.All(line, c => Assert.Null(c.Refusal));

        // A line ending on a column: that last cell is no crossing.
        var factory = NewFactory();
        PlaceBelt(factory, 40, 33, Direction.South);
        Assert.DoesNotContain(Plan(factory, (37, 33), (40, 33), Direction.East), c => ((PlaceBelt)c.Command!).Crossing);
    }

    [Fact]
    public void ConsecutiveCrossingsAllBecomeJunctions()
    {
        var (factory, line) = CrossTwoColumns();
        var host = new SimHost(factory);
        var refused = new List<Refusal>();
        host.CommandRejected += (_, r) => refused.Add(r);

        foreach (var command in PlacementLine.Commands(line))
        {
            host.Submit(command);
        }

        host.AdvanceTicks(1);

        Assert.Empty(refused);
        Assert.Equal(LogisticsKind.Junction, factory.Belts.Cells[new GridPosition(40, 33)].Kind);
        Assert.Equal(LogisticsKind.Junction, factory.Belts.Cells[new GridPosition(41, 33)].Kind);
    }

    [Fact]
    public void NoCrossingBesideARefusedCell()
    {
        // A splitter on (41,33) right after the column: that cell is refused, so (40,33) would leave a junction in a
        // broken line. It is a plain belt over the column instead (it turns it, a merge).
        var factory = NewFactory();
        factory.Research.ForceUnlock("junction");
        for (var y = 30; y <= 36; y++)
        {
            PlaceBelt(factory, 40, y, Direction.South);
        }

        Assert.True(factory.PlaceInstant(new BuildSpec("splitter", BuildingKind.Splitter, new GridPosition(41, 33), Direction.East), charge: false));

        var line = Plan(factory, (37, 33), (44, 33), Direction.East);

        Assert.Equal(RefusalCode.Occupied, line[4].Refusal);
        Assert.False(((PlaceBelt)line[3].Command!).Crossing);
        Assert.Null(line[3].Refusal);
        Assert.Equal(BeltOverBelt.Rotate, factory.BeltOverBeltAt(new GridPosition(40, 33), Direction.East, Belt));
    }

    [Fact]
    public void RefusedCellsAreShownAndNotSent()
    {
        // The classic generator has no walls: the biome one does.
        var factory = NewGame.Sandbox(Content, new GridPosition(500, 500), rules: new GameRules(InstantBuild: true),
            map: MapGenSettings.Normal(MapSize.Media, 1));
        var wall = FirstWall(factory);

        var line = Plan(factory, (wall.X, wall.Y), (wall.X + 3, wall.Y), Direction.East);

        Assert.Equal(RefusalCode.Wall, line[0].Refusal);
        Assert.NotNull(line[0].Command);
        var commands = PlacementLine.Commands(line);
        Assert.DoesNotContain(commands, c => ((PlaceBelt)c).Position == wall);
        Assert.Equal(line.Count(c => c.Refusal is null), commands.Count);
    }

    [Fact]
    public void APlannedCellIsOccupied()
    {
        var factory = NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120);
        var host = new SimHost(factory);
        host.Submit(new PlaceBelt(0, new GridPosition(12, 10), Direction.East, Belt));
        host.AdvanceTicks(1);
        Assert.True(factory.IsPlannedTile(new GridPosition(12, 10)));

        var line = Plan(factory, (10, 10), (14, 10), Direction.East, BuildSpec.StructureIdFor(BuildingKind.Junction)!);

        Assert.Equal([12], line.Where(c => c.Refusal == RefusalCode.Occupied).Select(c => c.Cell.X));
    }

    /// <summary>A crossing over a belt with a plan on it (here a tier change) is refused like any planned cell.</summary>
    [Fact]
    public void APlannedCrossingIsOccupied()
    {
        var factory = NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120);
        factory.Research.ForceUnlock("conveyor-fast");
        for (var y = 9; y <= 11; y++)
        {
            PlaceBelt(factory, 12, y, Direction.South);
        }

        var host = new SimHost(factory);
        host.Submit(new PlaceBelt(0, new GridPosition(12, 10), Direction.South, "conveyor-fast"));
        host.AdvanceTicks(1);
        Assert.True(factory.IsPlannedTile(new GridPosition(12, 10)));

        var line = Plan(factory, (10, 10), (14, 10), Direction.East);

        var crossing = Assert.Single(line, c => c.Cell == new GridPosition(12, 10));
        Assert.Equal(RefusalCode.Occupied, crossing.Refusal);
        Assert.DoesNotContain(PlacementLine.Commands(line), c => ((PlaceBelt)c).Position == crossing.Cell);
    }

    private static GridPosition FirstWall(IGameView view)
    {
        var t = view.Terrain!;
        for (var y = 0; y < t.Height; y++)
        {
            for (var x = 0; x < t.Width - 4; x++)
            {
                if (t[x, y].Wall != WallKind.None)
                {
                    return new GridPosition(x, y);
                }
            }
        }

        throw new InvalidOperationException("Nessuna parete sulla mappa di prova.");
    }
}
