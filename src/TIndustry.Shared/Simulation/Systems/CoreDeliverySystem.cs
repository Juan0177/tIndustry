namespace TIndustry.Shared;

/// <summary>Drains items that reached the core tiles into stock (or cash when auto-sell is on).</summary>
public sealed class CoreDeliverySystem : ISimulationSystem
{
    public string Name => "CoreDelivery";

    public void Run(GameState state, SystemContext ctx) =>
        state.CoreDeliveredItems += CoreStockSink.Drain(
            state.Belts, state.CoreTiles, state.Wallet, state.Market, state.Session, state.AutoSellAtCore);
}
