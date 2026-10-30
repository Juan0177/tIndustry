using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>The Miner T2's bonus when powered is paid for: its power use is in the demand of its network.</summary>
public sealed class PoweredMinerTests
{
    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    /// <summary>A Miner T2 at (4,4) and a burning generator at <paramref name="generator"/>.</summary>
    private static Factory WithGenerator(GridPosition generator)
    {
        var factory = new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(new GridPosition(18, 18)));
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

        // The powered miner is in the demand of the generator's network with its power use; the far one is not.
        var network = powered.PowerGrid.NetworkOf(powered.Miners[0].Id)!;
        Assert.Same(network, powered.PowerGrid.NetworkOf(powered.Generators[0].Id));
        Assert.Equal(Content.FindBuilding(Miner.AdvancedId)!.PowerUseRate, network.RequestedRate);
        Assert.Equal(10_000, network.SatisfactionBp);
        Assert.Equal(0, unpowered.PowerGrid.NetworkOf(unpowered.Generators[0].Id)!.RequestedRate);
        Assert.Equal(0, unpowered.State.SatisfactionOf(unpowered.Miners[0].Id));
    }
}
