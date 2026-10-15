using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Determinism foundations: fixed tick, frame-rate independence, canonical iteration order,
/// stable building ids across save/restore.
/// </summary>
public sealed class DeterminismTests
{
    private static readonly string ContentPath =
        Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    /// <summary>
    /// Iron ore delivered to the CORE in 10 s of play by one T1 miner touching the CORE,
    /// measured on the variable-step code before the fixed tick: 300 × <c>Tick(1f / 30f)</c> → 4.
    /// </summary>
    private const long DeliveredInTenSecondsBeforeFixedTick = 4;

    private static FactoryContent LoadContent() => FactoryContent.Load(ContentPath);

    private static string Serialize(Factory factory) =>
        JsonSerializer.Serialize(factory.Capture(), FactorySaveStore.JsonOptions);

    [Fact]
    public void StepAdvancesExactlyOneTick()
    {
        var factory = NewGame.Demo(LoadContent());
        Assert.Equal(0, factory.CurrentTick);
        factory.Step();
        Assert.Equal(1, factory.CurrentTick);
        factory.Step();
        Assert.Equal(2, factory.CurrentTick);
    }

    [Fact]
    public void LongFrameIsCappedAtMaxTicks()
    {
        var clock = new SimulationClock();
        Assert.Equal(SimulationClock.MaxTicksPerFrame, clock.Accumulate(0.2));
        Assert.Equal(5, SimulationClock.MaxTicksPerFrame);
        Assert.Equal(0, clock.Accumulate(0));
    }

    [Fact]
    public void FrameRateDoesNotChangeTheOutcome()
    {
        const long target = 1200;
        var content = LoadContent();

        var a = NewGame.Demo(content);
        var clockA = new SimulationClock();
        while (a.CurrentTick < target)
        {
            var ticks = clockA.Accumulate(1.0 / 60.0);
            for (var i = 0; i < ticks && a.CurrentTick < target; i++)
            {
                a.Step();
            }
        }

        var b = NewGame.Demo(content);
        var clockB = new SimulationClock();
        var random = new Random(42);
        while (b.CurrentTick < target)
        {
            var frame = 0.004 + random.NextDouble() * (0.05 - 0.004);
            var ticks = clockB.Accumulate(frame);
            for (var i = 0; i < ticks && b.CurrentTick < target; i++)
            {
                b.Step();
            }
        }

        Assert.Equal(target, a.CurrentTick);
        Assert.Equal(target, b.CurrentTick);
        // 20 s is not enough for ore to reach the CORE on the long L path, but items must be in flight.
        Assert.True(a.Miners[0].ItemsProduced > 0, "the demo must actually produce something");
        Assert.Equal(Serialize(a), Serialize(b));
    }

    [Fact]
    public void PlacementOrderDoesNotChangeOutcome()
    {
        var content = LoadContent();
        var forward = BuildMinerLine(content, reverse: false);
        var backward = BuildMinerLine(content, reverse: true);

        forward.RunTicks(1200);
        backward.RunTicks(1200);

        Assert.True(forward.CoreDeliveredItems > 0, "the line must actually deliver something");
        Assert.Equal(Serialize(forward), Serialize(backward));
    }

    [Fact]
    public void SaveRestoreMidGameKeepsSimulationIdentical()
    {
        var content = LoadContent();
        var original = Factory.CreateSorterBridgeDemo(content);
        original.RunTicks(300);

        var copy = Factory.Restore(content, original.Capture());
        Assert.Equal(Serialize(original), Serialize(copy));

        original.RunTicks(600);
        copy.RunTicks(600);

        Assert.Equal(900, copy.CurrentTick);
        Assert.Equal(Serialize(original), Serialize(copy));
    }

    [Fact]
    public void GameplayRatesMatchWallClock()
    {
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var factory = new Factory(LoadContent(), new BeltGrid(), core);
        factory.UnlockAllForDemo();
        // T1 miner on iron (no terrain = full deposit) touching the CORE west edge.
        Assert.True(factory.TryPlaceMiner(new GridPosition(7, 14), Direction.East));

        factory.RunSeconds(10f);

        Assert.InRange(
            factory.CoreDeliveredItems,
            DeliveredInTenSecondsBeforeFixedTick - 1,
            DeliveredInTenSecondsBeforeFixedTick + 1);
    }

    /// <summary>Miner (2,13) → belts (4..8, 14) east → CORE (9,14), placed in either order.</summary>
    private static Factory BuildMinerLine(FactoryContent content, bool reverse)
    {
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var factory = new Factory(content, new BeltGrid(), core);
        factory.UnlockAllForDemo();

        var belts = Enumerable.Range(4, 5).Select(x => new GridPosition(x, 14)).ToList();
        if (reverse)
        {
            belts.Reverse();
            foreach (var pos in belts)
            {
                Assert.True(factory.TryPlaceBelt(pos, Direction.East));
            }

            Assert.True(factory.TryPlaceMiner(new GridPosition(2, 13), Direction.East));
        }
        else
        {
            Assert.True(factory.TryPlaceMiner(new GridPosition(2, 13), Direction.East));
            foreach (var pos in belts)
            {
                Assert.True(factory.TryPlaceBelt(pos, Direction.East));
            }
        }

        return factory;
    }
}
