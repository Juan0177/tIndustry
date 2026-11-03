namespace TIndustry.Shared;

public sealed class ExtractionSystem : ISimulationSystem
{
    public string Name => "Extraction";

    public void Run(GameState state, SystemContext ctx)
    {
        foreach (var ex in state.Extractors)
        {
            ex.Tick(state);
        }
    }
}
