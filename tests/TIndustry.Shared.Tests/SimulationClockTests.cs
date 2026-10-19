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
}
