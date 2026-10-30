namespace TIndustry.Shared;

/// <summary>Lamps after the power balance: the light of each one is the satisfaction of its network.</summary>
public sealed class LightingSystem : ISimulationSystem
{
    public string Name => "Lighting";

    public void Run(GameState state, SystemContext ctx)
    {
        foreach (var lamp in state.Lamps)
        {
            lamp.Tick(lamp.PowerRequested ? state.SatisfactionOf(lamp.Id) : 0);
        }
    }
}
