namespace TIndustry.Shared;

/// <summary>Generators burn fuel; capacity + recharge (Raylib soft power network, no full link graph yet).</summary>
public sealed class PowerSystem : ISimulationSystem
{
    public string Name => "Power";

    public void Run(GameState state, SystemContext ctx)
    {
        var liveGens = ctx.LiveGenerators;
        liveGens.Clear();
        foreach (var gen in state.Generators)
        {
            if (gen.Tick(ctx.Dt, state.Belts))
            {
                liveGens.Add(gen);
            }
        }

        state.RecalculatePowerCapacity(liveGens.Count);
        var generation = GameState.CorePowerGeneration
            + liveGens.Count * Generator.GenerationPerSecond;
        state.PowerGenerationPerSecond = generation;
        state.PowerBuffer = Math.Min(state.PowerCapacity, state.PowerBuffer + generation * ctx.Dt);
    }
}
