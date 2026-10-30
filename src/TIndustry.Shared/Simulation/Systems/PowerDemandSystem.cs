namespace TIndustry.Shared;

/// <summary>
/// First half of the power tick: the craft machines take what the belts bring and start a recipe, then every
/// consumer says whether it works this tick (<c>PowerRequested</c>). <see cref="PowerSystem"/> balances the networks
/// on these requests; the consuming systems then work with the satisfaction of their network.
/// </summary>
public sealed class PowerDemandSystem : ISimulationSystem
{
    public string Name => "PowerDemand";

    public void Run(GameState state, SystemContext ctx)
    {
        foreach (var craft in state.CraftMachines())
        {
            craft.TakeInputs(state.Belts);
            craft.PowerRequested = craft.WantsPower;
        }

        foreach (var miner in state.Miners)
        {
            miner.PowerRequested = miner.WantsPower;
        }

        foreach (var lamp in state.Lamps)
        {
            lamp.PowerRequested = true;
        }
    }
}
