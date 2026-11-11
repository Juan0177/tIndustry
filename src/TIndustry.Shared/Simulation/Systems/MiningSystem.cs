namespace TIndustry.Shared;

/// <summary>
/// Miners after the power balance: a working T2 or impact drill that asked for power gets the satisfaction of its
/// network (T2: speed 1 + 0.2 × s on top of its own; drill: its speed × s, stalled without power); T1 is not a power
/// block.
/// </summary>
public sealed class MiningSystem : ISimulationSystem
{
    public string Name => "Mining";

    public void Run(GameState state, SystemContext ctx)
    {
        foreach (var miner in state.Miners)
        {
            miner.Tick(
                ctx.Dt,
                state.Belts,
                ref state.NextItemId,
                miner.PowerRequested ? state.SatisfactionOf(miner.Id) : 0,
                itemId => state.TryDeliverAdjacent(miner.Position, miner.Size, itemId));
        }
    }
}
