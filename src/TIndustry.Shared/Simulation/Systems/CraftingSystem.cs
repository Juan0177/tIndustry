namespace TIndustry.Shared;

/// <summary>Smelters then assemblers, drawing from the shared power buffer.</summary>
public sealed class CraftingSystem : ISimulationSystem
{
    public const float SmelterPowerDraw = 8f;
    public const float AssemblerPowerDraw = 10f;

    public string Name => "Crafting";

    public void Run(GameState state, SystemContext ctx)
    {
        var liveGens = ctx.LiveGenerators;
        foreach (var craft in state.CraftMachines())
        {
            var connected = liveGens.Any(g => g.IsAdjacentTo(craft.Position, Crafter.Size))
                || state.IsPoweredViaNode(craft.Position, Crafter.Size, liveGens);
            var draw = craft.IsAssembler ? AssemblerPowerDraw : SmelterPowerDraw;
            var exclude = craft;
            craft.Tick(
                ctx.Dt,
                state.Belts,
                ref state.NextItemId,
                networkConnected: connected,
                trySpendPower: state.TrySpendPower,
                powerDrawPerSecond: draw,
                tryDeliverAdjacent: itemId =>
                    state.TryDeliverAdjacent(craft.Position, Crafter.Size, itemId, exclude));
        }
    }
}
