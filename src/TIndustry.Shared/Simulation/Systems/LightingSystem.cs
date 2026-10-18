namespace TIndustry.Shared;

/// <summary>Lamps, after the craft machines: each one draws its power use from the shared buffer when connected.</summary>
public sealed class LightingSystem : ISimulationSystem
{
    public string Name => "Lighting";

    public void Run(GameState state, SystemContext ctx)
    {
        if (state.Lamps.Count == 0)
        {
            return;
        }

        var liveGens = ctx.LiveGenerators;
        // FactoryContent.Load rejects a lamp without a positive power use; a hand-built content without one stays dark
        // instead of lighting for free.
        var powerUse = state.Content.FindBuilding(Lamp.BuildingId)?.PowerUse ?? 0f;
        var draw = powerUse * ctx.Dt;
        foreach (var lamp in state.Lamps)
        {
            var connected = powerUse > 0f
                && (liveGens.Any(g => g.IsAdjacentTo(lamp.Position, Lamp.Size))
                    || state.IsPoweredViaNode(lamp.Position, Lamp.Size, liveGens));
            lamp.Tick(connected, state.TrySpendPower, draw);
        }
    }
}
