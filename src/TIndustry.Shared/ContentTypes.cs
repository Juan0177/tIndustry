namespace TIndustry.Shared;

public sealed record ResourceAmount(string ItemId, int Amount);

public sealed record UnlockRequirement(
    int Money,
    IReadOnlyList<ResourceAmount> Materials);

public enum LogisticsKind
{
    Belt,
    Junction,
    Splitter,
    Bridge,
    Sorter
}

public sealed record ConveyorDefinition(
    string Id,
    int Tier,
    float RateItemsPerSecond,
    int Capacity,
    float ItemSpacing,
    int MoneyCost = 0,
    IReadOnlyList<ResourceAmount>? BuildCost = null,
    UnlockRequirement? Unlock = null,
    LogisticsKind Kind = LogisticsKind.Belt)
{
    public IReadOnlyList<ResourceAmount> EffectiveBuildCost => BuildCost ?? [];
}

public sealed record RecipeDefinition(
    string Id,
    float DurationSeconds,
    IReadOnlyList<ResourceAmount> Inputs,
    IReadOnlyList<ResourceAmount> Outputs);

public sealed record BuildingDefinition(
    string Id,
    int MoneyCost,
    IReadOnlyList<ResourceAmount> BuildCost,
    int RefundPercent = 100);

public sealed record MarketItemDefinition(
    string ItemId,
    string DisplayName,
    int SellPrice);

public sealed record StructureDefinition(
    string Id,
    string DisplayName,
    string Kind,
    bool UnlockedByDefault,
    UnlockRequirement? Unlock,
    bool IsStub);
