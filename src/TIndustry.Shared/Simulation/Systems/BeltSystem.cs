namespace TIndustry.Shared;

/// <summary>
/// Belts advance first so fuel reaches generators and handoffs reach craft/core.
/// The second run (<c>advance: false</c>, Dt 0) lets freshly emitted items move the same tick.
/// </summary>
public sealed class BeltSystem(bool advance) : ISimulationSystem
{
    public string Name => "Belt";

    public void Run(GameState state, SystemContext ctx) =>
        state.Belts.Tick(advance ? ctx.Dt : 0f);
}
