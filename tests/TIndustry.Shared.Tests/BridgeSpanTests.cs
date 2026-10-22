using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The bridge reaches like Mindustry's bridge conveyor: its two ends stand 2 to 4 cells apart, so it crosses 1 to 3
/// cells. A save with a longer bridge (older rule, 2–5) loads without it: neither end is placed.
/// </summary>
public sealed class BridgeSpanTests
{
    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly ConveyorDefinition Belt = Content.RequireConveyor("conveyor-basic");
    private static readonly ConveyorDefinition Bridge = Content.RequireConveyor("conveyor-bridge");

    [Fact]
    public void EndsStandTwoToFourCellsApart()
    {
        Assert.Equal(2, BeltGridCell.MinBridgeSpan);
        Assert.Equal(4, BeltGridCell.MaxBridgeSpan);

        // Cells 2 and 3 ahead taken: the exit lands 4 ahead.
        var four = new BeltGrid();
        four.TryPlaceFree(new GridPosition(2, 0), Direction.South, Belt);
        four.TryPlaceFree(new GridPosition(3, 0), Direction.South, Belt);
        Assert.True(four.TryPlaceBridge(new GridPosition(0, 0), Direction.East, Bridge));
        Assert.Equal(new GridPosition(4, 0), four.Cells[new GridPosition(0, 0)].BridgePartner);

        // Cells 2, 3 and 4 ahead taken: no exit within reach.
        var five = new BeltGrid();
        for (var x = 2; x <= 4; x++)
        {
            five.TryPlaceFree(new GridPosition(x, 0), Direction.South, Belt);
        }

        Assert.False(five.TryPlaceBridge(new GridPosition(0, 0), Direction.East, Bridge));
        Assert.False(five.Contains(new GridPosition(0, 0)));
        Assert.False(five.Contains(new GridPosition(5, 0)));
    }

    [Fact]
    public void ABridgeTooLongInASaveIsNotPlaced()
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(30, 30), mapWidth: 40, mapHeight: 40,
            startingMoney: 100_000, rules: new GameRules(InstantBuild: true));
        var entry = new GridPosition(10, 10);
        Assert.True(factory.Belts.TryPlaceBridge(entry, Direction.East, Bridge));
        Assert.True(factory.Belts.TryPlaceFree(new GridPosition(10, 12), Direction.East, Belt));

        // Rewrite the save as the old rule allowed: exit 5 cells ahead.
        var data = factory.Capture();
        var far = new GridPosition(15, 10);
        foreach (var cell in data.Belts.Where(c => c.DefinitionId == Bridge.Id))
        {
            if (cell.X == entry.X)
            {
                (cell.BridgePartnerX, cell.BridgePartnerY) = (far.X, far.Y);
            }
            else
            {
                (cell.X, cell.Y) = (far.X, far.Y);
            }
        }

        var restored = Factory.Restore(Content, data);
        Assert.False(restored.Belts.Contains(entry));
        Assert.False(restored.Belts.Contains(far));
        Assert.True(restored.Belts.Contains(new GridPosition(10, 12)));
    }
}
