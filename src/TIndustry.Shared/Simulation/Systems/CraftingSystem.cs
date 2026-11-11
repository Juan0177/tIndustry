namespace TIndustry.Shared;

/// <summary>Fornos, assemblers then the other machines (<see cref="GameState.CraftMachines"/>), each with the satisfaction of its network when it asked for power this tick.</summary>
public sealed class CraftingSystem : ISimulationSystem
{
    public string Name => "Crafting";

    public void Run(GameState state, SystemContext ctx)
    {
        // The game's seed draws the pieces of the random recipes (the separator); 0 without terrain.
        long seed = state.Terrain?.Seed ?? 0;
        foreach (var craft in state.CraftMachines())
        {
            var exclude = craft;
            craft.Tick(
                ctx.Dt,
                state.Belts,
                ref state.NextItemId,
                powerBp: craft.PowerRequested ? state.SatisfactionOf(craft.Id) : 0,
                tryDeliverAdjacent: itemId =>
                    state.TryDeliverAdjacent(craft.Position, Crafter.Size, itemId, exclude),
                randomSeed: seed);
        }
    }
}
