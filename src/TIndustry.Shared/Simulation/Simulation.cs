namespace TIndustry.Shared;

/// <summary>Per-tick data handed to every system.</summary>
public sealed class SystemContext
{
    /// <summary>Tick length in seconds (<see cref="Simulation.TickSeconds"/>).</summary>
    public float Dt { get; init; } = Simulation.TickSeconds;

    /// <summary>Live generators, filled by the Power system for the systems after it.</summary>
    public List<Generator> LiveGenerators { get; } = [];
}

public interface ISimulationSystem
{
    string Name { get; }

    void Run(GameState state, SystemContext ctx);
}

/// <summary>Fixed-tick simulation: runs the systems in spec order over a <see cref="GameState"/>.</summary>
public static class Simulation
{
    public const int TickRate = SimulationClock.TickRate;
    public const float TickSeconds = SimulationClock.TickSeconds;

    /// <summary>Belt, Power, Mining, Crafting, Extraction, Belt (settle, Dt 0), CoreDelivery.</summary>
    public static IReadOnlyList<ISimulationSystem> Systems { get; } =
    [
        new BeltSystem(advance: true),
        new PowerSystem(),
        new MiningSystem(),
        new CraftingSystem(),
        new ExtractionSystem(),
        new BeltSystem(advance: false),
        new CoreDeliverySystem()
    ];

    /// <summary>Advances the state by exactly one fixed tick.</summary>
    public static void Step(GameState state)
    {
        var ctx = new SystemContext();
        foreach (var system in Systems)
        {
            system.Run(state, ctx);
        }

        state.CurrentTick++;
    }
}
