namespace TIndustry.Shared;

/// <summary>Fornos then assemblers, each with the satisfaction of its network when it asked for power this tick.</summary>
public sealed class CraftingSystem : ISimulationSystem
{
    public string Name => "Crafting";

    public void Run(GameState state, SystemContext ctx)
    {
        foreach (var craft in state.CraftMachines())
        {
            var exclude = craft;
            craft.Tick(
                ctx.Dt,
                state.Belts,
                ref state.NextItemId,
                powerBp: craft.PowerRequested ? state.SatisfactionOf(craft.Id) : 0,
                tryDeliverAdjacent: itemId =>
                    state.TryDeliverAdjacent(craft.Position, Crafter.Size, itemId, exclude));
        }
    }
}
