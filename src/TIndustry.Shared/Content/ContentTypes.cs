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
    LogisticsKind Kind = LogisticsKind.Belt,
    float BuildCostMultiplier = 1f)
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
    int RefundPercent = 100,
    float BuildCostMultiplier = 1f);

/// <summary>Mobile unit stats (Mindustry's UnitType): build speed, build range in tiles, speed in tiles/s.</summary>
public sealed record UnitDefinition(float BuildSpeed, float BuildRange, float Speed);

public sealed record CoreUpgradeDefinition(
    int MoneyCost,
    IReadOnlyList<ResourceAmount> BuildCost,
    int SaleBonusPercent);

public sealed record MarketItemDefinition(
    string ItemId,
    string DisplayName,
    int SellPrice,
    int? MinSellPrice = null,
    int? MaxSellPrice = null,
    int? SoftStock = null);

public sealed record StructureDefinition(
    string Id,
    string DisplayName,
    string Kind,
    bool UnlockedByDefault,
    UnlockRequirement? Unlock,
    bool IsStub,
    IReadOnlyList<string> Prerequisites);

/// <summary>
/// A floor of <c>terrain.floors</c>: <paramref name="Biome"/> is one of <see cref="TerrainTable.Biomes"/>,
/// <paramref name="Color"/> is <c>#RRGGBB</c>, <paramref name="Buildable"/> is false only for deep water.
/// </summary>
public sealed record FloorDef(string Id, FloorKind Kind, string Biome, string Color, bool Buildable);

/// <summary>A wall of <c>terrain.walls</c> (walls always block building).</summary>
public sealed record WallDef(string Id, WallKind Kind, string Biome, string Color);

/// <summary>The <c>terrain</c> section of content.json: one row per <see cref="FloorKind"/> and per wall.</summary>
public sealed record TerrainTable(IReadOnlyList<FloorDef> Floors, IReadOnlyList<WallDef> Walls)
{
    /// <summary>The four biomes, plus <c>acqua</c> for the water floors shared by all of them.</summary>
    public static readonly IReadOnlyList<string> Biomes = ["rocce", "sabbia", "neve", "prati", "acqua"];

    public static readonly TerrainTable Empty = new([], []);

    public FloorDef? Floor(FloorKind kind) => Floors.FirstOrDefault(f => f.Kind == kind);

    public WallDef? Wall(WallKind kind) => Walls.FirstOrDefault(w => w.Kind == kind);
}
