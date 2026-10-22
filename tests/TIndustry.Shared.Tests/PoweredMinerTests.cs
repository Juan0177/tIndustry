using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>The Miner T2's +20% when powered is paid for: it draws its power use from the shared buffer.</summary>
public sealed class PoweredMinerTests
{
    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    /// <summary>A Miner T2 at (4,4) and a burning generator at <paramref name="generator"/>.</summary>
    private static Factory WithGenerator(GridPosition generator)
    {
        var factory = new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(new GridPosition(18, 18), size: 2));
        factory.UnlockAllForDemo();
        factory.Research.ForceUnlock(Miner.AdvancedId);
        Assert.True(factory.TryPlaceMiner(new GridPosition(4, 4), Direction.East, definitionId: Miner.AdvancedId));
        Assert.True(factory.TryPlaceGenerator(generator, Direction.East));
        Assert.True(factory.Generators[0].TryAcceptFuel("coal") && factory.Generators[0].TryAcceptFuel("coal"));
        for (var i = 0; i < SimulationClock.TickRate * 2; i++)
        {
            factory.Step();
        }

        return factory;
    }

    [Fact]
    public void AdvancedMinerHasAPowerUse()
    {
        Assert.True(Content.FindBuilding(Miner.AdvancedId)!.PowerUse > 0f);
        Assert.Equal(0f, Content.FindBuilding(Miner.BasicId)!.PowerUse);
    }

    [Fact]
    public void PoweredAdvancedMinerDrawsItsPowerUse()
    {
        var powered = WithGenerator(new GridPosition(4, 2)); // touches the miner
        var unpowered = WithGenerator(new GridPosition(12, 2)); // same generator, away from the miner
        Assert.True(powered.Miners[0].IsPowered);
        Assert.False(unpowered.Miners[0].IsPowered);

        // From an empty buffer, one tick: the generator refills both the same, the powered miner takes its share.
        powered.State.PowerBuffer = 0f;
        unpowered.State.PowerBuffer = 0f;
        powered.Step();
        unpowered.Step();

        var draw = Content.FindBuilding(Miner.AdvancedId)!.PowerUse / SimulationClock.TickRate;
        Assert.Equal(unpowered.PowerBuffer - draw, powered.PowerBuffer, 4);
        Assert.True(powered.Miners[0].IsPowered);
    }
}
