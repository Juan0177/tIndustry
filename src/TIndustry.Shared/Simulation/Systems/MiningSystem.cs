namespace TIndustry.Shared;

/// <summary>
/// Miners after gens so T2 can apply adjacency/node power the same tick. A connected, working T2 draws its power use
/// from the shared buffer; without enough in it the +20% does not apply that tick.
/// </summary>
public sealed class MiningSystem : ISimulationSystem
{
    public string Name => "Mining";

    public void Run(GameState state, SystemContext ctx)
    {
        var liveGens = ctx.LiveGenerators;
        foreach (var miner in state.Miners)
        {
            var connected = miner.CanReceivePower
                && miner.IsWorking
                && (liveGens.Any(g => g.IsAdjacentTo(miner.Position, Miner.Size))
                    || state.IsPoweredViaNode(miner.Position, Miner.Size, liveGens))
                && state.TrySpendPower((state.Content.FindBuilding(miner.DefinitionId)?.PowerUse ?? 0f) * ctx.Dt);
            miner.Tick(
                ctx.Dt,
                state.Belts,
                ref state.NextItemId,
                connected,
                itemId => state.TryDeliverAdjacent(miner.Position, Miner.Size, itemId));
        }
    }
}
