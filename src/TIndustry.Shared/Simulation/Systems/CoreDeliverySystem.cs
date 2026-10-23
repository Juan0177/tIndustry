namespace TIndustry.Shared;

/// <summary>Drains items that reached the core tiles into the Core storage (or cash when auto-sell is on); a full item stays on its belt.</summary>
public sealed class CoreDeliverySystem : ISimulationSystem
{
    public string Name => "CoreDelivery";

    public void Run(GameState state, SystemContext ctx) =>
        state.CoreDeliveredItems += CoreStockSink.Drain(state);
}
