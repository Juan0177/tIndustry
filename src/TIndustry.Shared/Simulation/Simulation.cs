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

    /// <summary>Time step this system actually uses for <paramref name="ctx"/> (the settle Belt pass uses 0).</summary>
    float DtFor(SystemContext ctx) => ctx.Dt;

    void Run(GameState state, SystemContext ctx);
}

/// <summary>Fixed-tick simulation: runs the systems in spec order over a <see cref="GameState"/>.</summary>
public static class Simulation
{
    public const int TickRate = SimulationClock.TickRate;
    public const float TickSeconds = SimulationClock.TickSeconds;

    /// <summary>
    /// Builder, Construct, Belt, Power, Mining, Crafting, Lighting, Extraction, Belt (settle, Dt 0), CoreDelivery, Market.
    /// </summary>
    public static IReadOnlyList<ISimulationSystem> Systems { get; } =
    [
        new BuilderSystem(),
        new ConstructSystem(),
        new BeltSystem(advance: true),
        new PowerSystem(),
        new MiningSystem(),
        new CraftingSystem(),
        new LightingSystem(),
        new ExtractionSystem(),
        new BeltSystem(advance: false),
        new CoreDeliverySystem(),
        new AutoSellSystem(),
        new MarketSystem()
    ];

    /// <summary>Advances the state by exactly one fixed tick.</summary>
    /// <param name="state">State to advance.</param>
    /// <param name="trace">Optional hook called before each system with its name and the dt it uses (tests).</param>
    public static void Step(GameState state, Action<string, float>? trace = null)
    {
        // Events describe one tick: whoever wanted the previous tick's has drained them (SimHost).
        state.Events.Clear();
        var ctx = new SystemContext();
        foreach (var system in Systems)
        {
            trace?.Invoke(system.Name, system.DtFor(ctx));
            system.Run(state, ctx);
        }

        state.CurrentTick++;
    }
}
