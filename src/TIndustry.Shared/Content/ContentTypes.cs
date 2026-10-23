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

/// <summary>
/// A belt or a logistics block. <see cref="RateItemsPerSecond"/> is the speed in cells per second (the name is older
/// than the spacing); a full line carries speed / <see cref="ItemSpacing"/> items per second, at most
/// <see cref="Capacity"/> items per cell.
/// </summary>
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
    float BuildCostMultiplier = 1f,
    bool Armored = false)
{
    /// <summary>Armored belt (Mindustry's): takes items from non-belts only from behind, never blends with buildings.</summary>
    public bool IsArmoredBelt => Armored && Kind == LogisticsKind.Belt;

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
    float BuildCostMultiplier = 1f,
    float PowerUse = 0f,
    int Tier = 0);

/// <summary>Mobile unit stats (Mindustry's UnitType): build speed, build range in tiles, speed in tiles/s.</summary>
public sealed record UnitDefinition(float BuildSpeed, float BuildRange, float Speed);

public sealed record MarketItemDefinition(string ItemId, string DisplayName, int BaseValue);

/// <summary>
/// The <c>marketRules</c> block of content.json, all integers: prices and multipliers in thousandths of B
/// (milli), probabilities in basis points (10,000 = 100%), sale impact in millionths of B per piece.
/// </summary>
public sealed record MarketRules(
    int NormalMinMilli, int NormalMaxMilli, int NormalAverageMilli, int NormalStepSeconds,
    int NormalPullPermille, int NormalNoiseMilli,
    int SpikeThresholdMilli, int SpikeMaxMilli, int SpikeRiseSeconds, int SpikeHoldSeconds, int SpikeFallSeconds,
    int SpikeIntervalMinMinutes, int SpikeIntervalMaxMinutes,
    int SaleImpactMicro, int SaleRecoverySeconds,
    int CrashPieces, int CrashWindowSeconds, int CrashMinutes, int CrashDipMinutes,
    int CrashDipMinMilli, int CrashDipMaxMilli, int CrashDipEverySeconds, int CrashDipChanceBp,
    int CrashJitterMilli,
    int JackpotMinPieces, int JackpotBaseBp, int JackpotStepPieces, int JackpotStepBp, int JackpotCapBp,
    int JackpotBonusPermille,
    int PityPieces, int PityStepBp, int PityGuaranteeMisses, int MissCooldownSeconds, int MissSpikeDelaySeconds,
    int LostBoostBp, int LostBoostMinutes, int LostPieces, int HistorySeconds);

public sealed record StructureDefinition(
    string Id,
    string DisplayName,
    string Kind,
    bool UnlockedByDefault,
    UnlockRequirement? Unlock,
    bool IsStub,
    IReadOnlyList<string> Prerequisites,
    int ItemCapacity = 0);

/// <summary>
/// One Core tier of <c>cores</c> (index = tier): footprint <paramref name="Size"/>, pieces per item, pieces/s sold
/// automatically, the drone it spawns and the build cost of the site (the Research cost lives on the structure).
/// </summary>
public sealed record CoreTierDefinition(
    string Id,
    int Size,
    int ItemCapacity,
    int AutoSellPerSecond,
    string UnitId,
    IReadOnlyList<ResourceAmount> BuildCost);

/// <summary>
/// A floor of <c>terrain.floors</c>: <paramref name="Biome"/> is one of <see cref="TerrainTable.Biomes"/>,
/// <paramref name="Color"/> is <c>#RRGGBB</c>, <paramref name="Buildable"/> is false only for deep water.
/// <paramref name="EdgePriority"/> orders the terrain art: the floor with the higher value draws its jagged
/// edge over the lower neighbour; one value per floor, water below every land floor.
/// </summary>
public sealed record FloorDef(string Id, FloorKind Kind, string Biome, string Color, bool Buildable, int EdgePriority);

/// <summary>A wall of <c>terrain.walls</c> (walls always block building).</summary>
public sealed record WallDef(string Id, WallKind Kind, string Biome, string Color);

/// <summary>
/// An ore of <c>terrain.deposits</c>: a miner digs it only when its <see cref="BuildingDefinition.Tier"/> is at
/// least <paramref name="Hardness"/> (Mindustry's drill tier vs item hardness).
/// </summary>
public sealed record DepositDef(string Id, DepositKind Kind, int Hardness);

/// <summary>
/// The <c>terrain</c> section of content.json: one row per <see cref="FloorKind"/>, per wall and per ore
/// (<see cref="Deposits"/>).
/// </summary>
public sealed record TerrainTable(IReadOnlyList<FloorDef> Floors, IReadOnlyList<WallDef> Walls)
{
    /// <summary>One row per <see cref="DepositKind"/> but <see cref="DepositKind.None"/>; empty in code-built content.</summary>
    public IReadOnlyList<DepositDef> Deposits { get; init; } = [];

    /// <summary>The four biomes, plus <c>acqua</c> for the water floors shared by all of them.</summary>
    public static readonly IReadOnlyList<string> Biomes = ["rocce", "sabbia", "neve", "prati", "acqua"];

    public static readonly TerrainTable Empty = new([], []);

    public FloorDef? Floor(FloorKind kind) => Floors.FirstOrDefault(f => f.Kind == kind);

    public WallDef? Wall(WallKind kind) => Walls.FirstOrDefault(w => w.Kind == kind);

    /// <summary>Hardness of <paramref name="kind"/>; 0 (any miner digs it) when the table has no row for it.</summary>
    public int Hardness(DepositKind kind) => Deposits.FirstOrDefault(d => d.Kind == kind)?.Hardness ?? 0;
}
