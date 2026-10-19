using System.Numerics;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Drawing-only interpolation of belt items between ticks (client side, no simulation state).</summary>
public sealed class ItemMotionTests
{
    private static readonly GridPosition Cell = new(5, 5);

    /// <summary>An item moving East at 1 px per tick, drawn at every frame of <paramref name="ticksPerFrame"/> ticks.</summary>
    private static List<float> DrawnPositions(double ticksPerFrame, int frames)
    {
        // Position at time t ticks: x = t (1 px per tick), cell = floor(x / 10); cells are 10 px wide.
        var drawn = new List<float>();
        ItemMotion? motion = null;
        var time = 0.0;
        long tick = 0;
        for (var f = 0; f < frames; f++)
        {
            time += ticksPerFrame;
            var newTick = (long)Math.Floor(time + 1e-9);
            var cell = new GridPosition((int)(newTick / 10), 0);
            motion = ItemMotion.Next(motion, new Vector2(newTick, 0), cell, Direction.East, newTick - tick, restart: false);
            tick = newTick;
            drawn.Add(motion.Value.At(time - newTick).X);
        }

        return drawn;
    }

    [Theory]
    [InlineData(1.0 / 2.4)] // 144 fps
    [InlineData(1.0)] // 60 fps
    [InlineData(2.0)] // 30 fps: two ticks per frame
    [InlineData(4.6)] // a slow frame of 4–5 ticks
    public void DrawnPositionMovesEvenlyAcrossTicksAndCells(double ticksPerFrame)
    {
        var drawn = DrawnPositions(ticksPerFrame, 200);
        // After the first tick the item is drawn exactly one tick behind: x = time - 1, whatever the ticks per frame.
        for (var f = 10; f < drawn.Count; f++)
        {
            Assert.Equal((f + 1) * ticksPerFrame - 1, drawn[f], 3);
        }
    }

    [Fact]
    public void SameCellAndNextCellAreContinuousOtherCellsStartOver()
    {
        var start = ItemMotion.Start(new Vector2(0, 0), Cell, Direction.East);
        var same = ItemMotion.Next(start, new Vector2(4, 0), Cell, Direction.East, 2, restart: false);
        Assert.Equal(new Vector2(2, 0), same.Before); // one tick behind on the path
        Assert.Equal(new Vector2(4, 0), same.After);

        // Handoff to the next cell along the belt (it may turn): still continuous.
        var next = ItemMotion.Next(same, new Vector2(5, 0), Cell.Step(Direction.East), Direction.North, 1, restart: false);
        Assert.Equal(new Vector2(4, 0), next.Before);

        // A cell that is not the next one along its old belt: starts where it is.
        var jumped = ItemMotion.Next(next, new Vector2(50, 0), new GridPosition(9, 9), Direction.North, 1, restart: false);
        Assert.Equal(jumped.After, jumped.Before);
    }

    [Fact]
    public void NoTickKeepsTheMotionRestartOrNegativeTicksStartOver()
    {
        var motion = new ItemMotion(new Vector2(1, 0), new Vector2(2, 0), Cell, Direction.East);
        Assert.Equal(motion, ItemMotion.Next(motion, new Vector2(2, 0), Cell, Direction.East, 0, restart: false));
        Assert.Equal(new Vector2(3, 0), ItemMotion.Next(motion, new Vector2(3, 0), Cell, Direction.East, 1, restart: true).Before);
        Assert.Equal(new Vector2(3, 0), ItemMotion.Next(motion, new Vector2(3, 0), Cell, Direction.East, -4, restart: false).Before);
        Assert.Equal(new Vector2(3, 0), ItemMotion.Next(null, new Vector2(3, 0), Cell, Direction.East, 1, restart: false).Before);
    }
}
