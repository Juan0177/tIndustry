namespace TIndustry.Shared;

/// <summary>Compat wrapper — prefer <see cref="FactoryContent"/> for new code.</summary>
public static class ConveyorContent
{
    public static IReadOnlyList<ConveyorDefinition> LoadConveyors(string contentJsonPath) =>
        FactoryContent.Load(contentJsonPath).Conveyors;

    public static ConveyorDefinition RequireBelt(string contentJsonPath, string id = "conveyor-basic") =>
        FactoryContent.Load(contentJsonPath).RequireConveyor(id);
}
