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
            ["Builder", "Construct", "Belt", "Power", "Mining", "Crafting", "Extraction", "Belt", "CoreDelivery"],
            Simulation.Systems.Select(s => s.Name).ToArray());
        Assert.Equal(SimulationClock.TickRate, Simulation.TickRate);
        Assert.Equal(SimulationClock.TickSeconds, Simulation.TickSeconds);
    }

    [Fact]
    public void StepRunsFullSequenceWithDt()
    {
        var content = FactoryContent.Load(ContentPath);
        var state = NewGame.Demo(content).State;
        var trace = new List<(string Name, float Dt)>();

        Simulation.Step(state, (name, dt) => trace.Add((name, dt)));

        var tick = SimulationClock.TickSeconds;
        Assert.Equal(
            [("Builder", tick), ("Construct", tick), ("Belt", tick), ("Power", tick), ("Mining", tick), ("Crafting", tick),
             ("Extraction", tick), ("Belt", 0f), ("CoreDelivery", tick)],
            trace);
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
