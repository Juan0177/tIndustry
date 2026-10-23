namespace TIndustry.Shared;

/// <summary>Sells from the Core stock at the Core tier's rate for every item with auto-sell on (runs after the Core delivery).</summary>
public sealed class AutoSellSystem : ISimulationSystem
{
    public string Name => "AutoSell";

    public void Run(GameState state, SystemContext ctx) => MarketSales.AutoSellTick(state);
}
