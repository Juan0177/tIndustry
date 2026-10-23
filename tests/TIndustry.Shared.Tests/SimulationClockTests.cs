using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Sub-tick fraction of the fixed-step clock: drawing only, never the simulation state.</summary>
public sealed class SimulationClockTests
{
    [Fact]
    public void AlphaStaysInRange()
    {
        var clock = new SimulationClock();
        foreach (var frame in new[] { 1.0 / 144, 1.0 / 60, 1.0 / 30, 0.5 })
        {
            for (var i = 0; i < 200; i++)
            {
                clock.Accumulate(frame);
                Assert.InRange(clock.Alpha, 0.0, 1.0);
                Assert.True(clock.Alpha < 1.0, $"Alpha {clock.Alpha} after frames of {frame} s");
            }
        }

        var fresh = new SimulationClock();
        Assert.Equal(1, fresh.Accumulate(1.5 / 60));
        Assert.Equal(0.5, fresh.Alpha, 1e-9);
    }

    [Fact]
    public void DroppedTicksOnlyAfterAHitchOverTheCap()
    {
        var clock = new SimulationClock();
        Assert.Equal(SimulationClock.MaxTicksPerFrame, clock.Accumulate(SimulationClock.MaxTicksPerFrame / 60.0));
        Assert.False(clock.DroppedTicks); // a legitimate 5-tick frame drops nothing
        Assert.Equal(SimulationClock.MaxTicksPerFrame, clock.Accumulate(0.5));
        Assert.True(clock.DroppedTicks);
        clock.Accumulate(1.0 / 60);
        Assert.False(clock.DroppedTicks);
    }

    [Fact]
    public void AlphaNeverTouchesTheChecksum()
    {
        var content = FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));
        SimHost NewHost()
        {
            var host = new SimHost(NewGame.Sandbox(
                content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120,
                rules: new GameRules(InstantBuild: true)));
            for (var x = 0; x < 6; x++)
            {
                host.Submit(new PlaceBelt(1, new GridPosition(50 + x, 40), Direction.East, "conveyor-basic"));
            }

            return host;
        }

        var fast = NewHost();
        var slow = NewHost();
        while (fast.State.CurrentTick < 120)
        {
            fast.Advance(0.007); // 0.42 ticks per frame
        }

        while (slow.State.CurrentTick < 120)
        {
            slow.Advance(0.03); // 1.8 ticks per frame
        }

        Assert.Equal(120, fast.State.CurrentTick);
        Assert.Equal(120, slow.State.CurrentTick);
        Assert.True(Math.Abs(fast.TickAlpha - slow.TickAlpha) > 0.1, $"{fast.TickAlpha} vs {slow.TickAlpha}");
        Assert.Equal(fast.Checksum(), slow.Checksum());
    }

    [Theory]
    [InlineData(1, 60)]
    [InlineData(5, 300)]
    [InlineData(10, 600)]
    [InlineData(20, 1200)]
    public void OneSecondOfFramesRunsSpeedTimesTheTicks(int speed, int expected)
    {
        var clock = new SimulationClock { Speed = speed };
        var ticks = 0;
        for (var i = 0; i < 60; i++)
        {
            ticks += clock.Accumulate(1.0 / 60);
            Assert.False(clock.DroppedTicks, $"frame {i} at x{speed} dropped ticks");
        }

        Assert.Equal(expected, ticks);
    }

    [Fact]
    public void TickCapScalesWithSpeed()
    {
        var clock = new SimulationClock();
        Assert.Equal(SimulationClock.MaxTicksPerFrame, clock.MaxTicksThisFrame);
        clock.Speed = 20;
        Assert.Equal(SimulationClock.MaxTicksPerFrame * 20, clock.MaxTicksThisFrame);
        // A hitch at x20 runs the scaled cap, not the x1 one, and drops the rest.
        Assert.Equal(SimulationClock.MaxTicksPerFrame * 20, clock.Accumulate(0.5));
        Assert.True(clock.DroppedTicks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(21)]
    [InlineData(100)]
    public void InvalidSpeedIsRefused(int speed)
    {
        var clock = new SimulationClock();
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Speed = speed);
        Assert.Equal(1, clock.Speed);
        Assert.False(SimulationClock.IsValidSpeed(speed));
    }

    [Fact]
    public void ValidSpeedsAreTheDevOnes()
    {
        Assert.Equal(new[] { 1, 5, 10, 20 }, SimulationClock.Speeds);
        foreach (var speed in SimulationClock.Speeds)
        {
            Assert.True(SimulationClock.IsValidSpeed(speed));
        }
    }

    [Fact]
    public void PausedFramesRunNothing()
    {
        var content = FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));
        var host = new SimHost(NewGame.Sandbox(
            content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120,
            rules: new GameRules(InstantBuild: true)));
        host.Speed = 20;
        Assert.Equal(20, host.Speed);
        host.Submit(new PlaceBelt(1, new GridPosition(50, 40), Direction.East, "conveyor-basic"));
        for (var i = 0; i < 60; i++)
        {
            Assert.Equal(0, host.Advance(0));
        }

        Assert.Equal(0, host.State.CurrentTick);
        Assert.Single(host.Pending); // the command waits for the resume
        Assert.Equal(20, host.Advance(1.0 / 60));
        Assert.Empty(host.Pending);
        Assert.Throws<ArgumentOutOfRangeException>(() => host.Speed = 7);
    }

    [Fact]
    public void SpeedNeverChangesTheResult()
    {
        var content = FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));
        SimHost NewHost(int speed)
        {
            var host = new SimHost(NewGame.Sandbox(
                content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120,
                rules: new GameRules(InstantBuild: true))) { Speed = speed };
            for (var x = 0; x < 6; x++)
            {
                host.Submit(new PlaceBelt(1, new GridPosition(50 + x, 40), Direction.East, "conveyor-basic"));
            }

            return host;
        }

        var normal = NewHost(1);
        var fast = NewHost(20);
        var changing = NewHost(5);
        normal.AdvanceTicks(600);
        while (fast.State.CurrentTick < 600)
        {
            fast.Advance(1.0 / 60);
        }

        // Speed changed mid-run (×5 → ×20 → ×1 → ×10): only the ticks per frame change.
        var speeds = new[] { (Until: 100L, Speed: 20), (Until: 300L, Speed: 1), (Until: 330L, Speed: 10), (Until: 600L, Speed: 10) };
        foreach (var (until, speed) in speeds)
        {
            while (changing.State.CurrentTick < until)
            {
                changing.Advance(1.0 / 60);
            }

            changing.Speed = speed;
        }

        Assert.Equal(600, fast.State.CurrentTick);
        Assert.Equal(600, changing.State.CurrentTick);
        Assert.Equal(normal.Checksum(), fast.Checksum());
        Assert.Equal(normal.Checksum(), changing.Checksum());
    }
}
