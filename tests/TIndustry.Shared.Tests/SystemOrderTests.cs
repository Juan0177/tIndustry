using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>The simulation is a fixed list of systems run in spec order over a <see cref="GameState"/>.</summary>
public sealed class SystemOrderTests
{
    private static readonly string ContentPath =
        Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    private static string Serialize(Factory factory) =>
        JsonSerializer.Serialize(factory.Capture(), FactorySaveStore.JsonOptions);

    [Fact]
    public void SystemsRunInSpecOrder()
    {
        Assert.Equal(
            ["Belt", "Power", "Mining", "Crafting", "Extraction", "Belt", "CoreDelivery"],
            Simulation.Systems.Select(s => s.Name).ToArray());
        Assert.Equal(SimulationClock.TickRate, Simulation.TickRate);
        Assert.Equal(SimulationClock.TickSeconds, Simulation.TickSeconds);
    }

    [Fact]
    public void StepIsPureOverState()
    {
        var content = FactoryContent.Load(ContentPath);
        var seed = NewGame.Demo(content);
        seed.RunSeconds(2f);
        var snapshot = seed.Capture();

        var a = Factory.Restore(content, snapshot);
        var b = Factory.Restore(content, snapshot);
        for (var i = 0; i < 600; i++)
        {
            Simulation.Step(a.State);
        }

        b.RunTicks(600);

        Assert.Equal(Serialize(b), Serialize(a));
        Assert.Equal(snapshot.CurrentTick + 600, a.State.CurrentTick);
    }
}
