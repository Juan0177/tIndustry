namespace TIndustry.Shared;

/// <summary>Miners after gens so T2 can apply adjacency/node power the same tick.</summary>
public sealed class MiningSystem : ISimulationSystem
{
    public string Name => "Mining";

    public void Run(GameState state, SystemContext ctx)
    {
        var liveGens = ctx.LiveGenerators;
        foreach (var miner in state.Miners)
        {
            var connected = miner.CanReceivePower
                && (liveGens.Any(g => g.IsAdjacentTo(miner.Position, Miner.Size))
                    || state.IsPoweredViaNode(miner.Position, Miner.Size, liveGens));
            miner.Tick(
                ctx.Dt,
                state.Belts,
                ref state.NextItemId,
                connected,
                itemId => state.TryDeliverAdjacent(miner.Position, Miner.Size, itemId));
        }
    }
}
