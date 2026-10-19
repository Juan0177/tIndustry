namespace TIndustry.Shared;

/// <summary>
/// A player's request to change the game. Queued by <see cref="SimHost.Submit"/>, validated and
/// applied at the start of tick <see cref="Tick"/>; the outcome comes back as an event.
/// </summary>
public abstract record SimCommand(int PlayerId)
{
    /// <summary>Tick at whose start the command is applied (0 = "next tick", set on submit).</summary>
    public long Tick { get; init; }
}

/// <summary>Buildings and logistics pieces placed by <see cref="PlaceBuilding"/> (belts use <see cref="PlaceBelt"/>).</summary>
public enum BuildingKind
{
    Junction,
    Splitter,
    Sorter,
    Bridge,
    Miner,
    MinerAdvanced,
    Smelter,
    Assembler,
    Generator,
    Extractor,
    PowerNode,
    PowerNodeT2,
    Lamp
}

/// <summary>
/// Replaces <see cref="Factory.TryPlaceBelt"/>. <paramref name="Crossing"/>: the belt is part of a dragged line, so
/// over a plain belt perpendicular to it a junction takes the belt's place (<see cref="Factory.BeltCrossingAt"/>).
/// </summary>
public sealed record PlaceBelt(
    int PlayerId,
    GridPosition Position,
    Direction Direction,
    string ConveyorId,
    bool Crossing = false) : SimCommand(PlayerId);

/// <summary>
/// Replaces the <c>TryPlace…</c> methods for junction, splitter, sorter, bridge, miners, forno,
/// assembler, generator, extractor and power nodes. <paramref name="RecipeId"/> applies to forno /
/// assembler (null = today's default recipe); <paramref name="FilterItemId"/> to sorter / extractor.
/// </summary>
public sealed record PlaceBuilding(
    int PlayerId,
    BuildingKind Kind,
    GridPosition Origin,
    Direction Direction,
    string? RecipeId = null,
    string? FilterItemId = null) : SimCommand(PlayerId);

/// <summary>
/// Takes away what stands on <paramref name="Tile"/> (see <see cref="SimHost"/> for the order). A single
/// right-click toggles: a second Remove on a block marked for dismantling takes the mark off. With
/// <paramref name="OnlyMark"/> (the removal rectangle, <see cref="RemovalArea"/>) it only marks or cancels build plans and sites and
/// never takes a dismantling mark off, so overlapping rectangles keep all their marks.
/// </summary>
public sealed record Remove(int PlayerId, GridPosition Tile, bool OnlyMark = false) : SimCommand(PlayerId);

/// <summary>Replaces <see cref="Factory.TryUnlockStructure"/>.</summary>
public sealed record UnlockStructure(int PlayerId, string StructureId) : SimCommand(PlayerId);

/// <summary>Replaces <see cref="Factory.TrySellFromWallet"/>.</summary>
public sealed record Sell(int PlayerId, string ItemId, int Amount) : SimCommand(PlayerId);

/// <summary>Replaces assigning <see cref="Factory.AutoSellAtCore"/>.</summary>
public sealed record SetAutoSell(int PlayerId, bool Enabled) : SimCommand(PlayerId);

/// <summary>Replaces <see cref="Factory.TryUpgradeCore"/>.</summary>
public sealed record UpgradeCore(int PlayerId) : SimCommand(PlayerId);

/// <summary>
/// Sets the filter of the sorter on <paramref name="Tile"/> (replaces the client's direct
/// <c>BeltGridCell.CycleFilterItem</c>). <paramref name="ItemId"/> must be one of <see cref="SorterFilters.Ids"/>.
/// </summary>
public sealed record SetSorterFilter(int PlayerId, GridPosition Tile, string ItemId) : SimCommand(PlayerId);

/// <summary>
/// Why <see cref="SimHost"/> refused a command (<see cref="SimHost.CommandRejected"/>) or the drone dropped a plan
/// (<see cref="PlanDropped"/>). The simulation has no language: the client turns each code into its own text.
/// </summary>
public enum RefusalCode
{
    /// <summary>
    /// Malformed command (unknown id, non-positive amount, sorter filter on a non-sorter tile or with an item outside
    /// <see cref="SorterFilters.Ids"/>): the client never sends one.
    /// </summary>
    InvalidCommand,

    /// <summary>Not enough money or materials; <see cref="Refusal.MissingMoney"/> and <see cref="Refusal.MissingItems"/> say how much.</summary>
    InsufficientResources,

    /// <summary>Placement on a CORE tile.</summary>
    OnCore,

    /// <summary>Placement on an occupied cell, outside the map or on another player's plan.</summary>
    Occupied,

    /// <summary>Placement on a cell with a wall (<see cref="TerrainTile.Wall"/>).</summary>
    Wall,

    /// <summary>Placement on deep water (<see cref="FloorKind.DeepWater"/>); bridges span it.</summary>
    DeepWater,

    /// <summary>
    /// A T1 miner on cells whose only ore is harder than its tier (titanium): Mindustry refuses a drill that cannot dig
    /// what lies under it.
    /// </summary>
    NeedsAdvancedMiner,

    /// <summary>The structure (<see cref="Refusal.StructureId"/>) is not researched yet.</summary>
    Locked,

    /// <summary><see cref="PlanDropped"/>: the structure was locked when the drone reached the plan.</summary>
    LockedByResearch,

    /// <summary>Research: already unlocked.</summary>
    AlreadyUnlocked,

    /// <summary>Research: prerequisites missing.</summary>
    MissingPrerequisites,

    /// <summary>Research: cannot pay the unlock.</summary>
    UnlockUnaffordable,

    /// <summary>Market: sale failed (not enough stock).</summary>
    SellFailed,

    /// <summary>Remove on an empty tile (the client shows nothing).</summary>
    NothingToRemove,

    /// <summary>The Core is already upgraded (<see cref="IGameView.CoreUpgradeLevel"/>).</summary>
    CoreAlreadyUpgraded
}

/// <summary>Shape of a dragged belt line, for the auto-junction (<see cref="Factory.BeltCrossingAt"/>).</summary>
public static class BeltLine
{
    /// <summary>
    /// True when <paramref name="cell"/> lies inside a straight dragged line facing <paramref name="direction"/>: the
    /// line came from the cell behind it and goes on to the cell ahead (Mindustry plans on both sides of a crossing).
    /// The press cell (no <paramref name="previous"/>) and the last cell of a drag (no <paramref name="next"/>) never are.
    /// </summary>
    public static bool IsInterior(GridPosition? previous, GridPosition cell, GridPosition? next, Direction direction) =>
        previous == cell.Step(DirectionMath.Opposite(direction)) && next == cell.Step(direction);

    /// <summary>
    /// True when a dragged belt facing <paramref name="direction"/> on <paramref name="cell"/> would cross a plain belt
    /// perpendicular to it: the client holds such a cell back until it knows whether the line goes on past it.
    /// </summary>
    public static bool MayCross(IGameView view, GridPosition cell, Direction direction) =>
        view.Belts.TryGetValue(cell, out var belt)
        && belt.Kind == LogisticsKind.Belt
        && belt.Direction != direction
        && belt.Direction != DirectionMath.Opposite(direction);
}

/// <summary>What a dragged belt does on a cell (<see cref="Factory.BeltCrossingAt"/>).</summary>
public enum BeltCrossing
{
    /// <summary>No crossing: the belt is placed (or refused) as usual.</summary>
    None,

    /// <summary>A junction replaces the perpendicular belt.</summary>
    Junction,

    /// <summary>A junction would replace it, but it is not researched: the belt goes on as a belt over a belt.</summary>
    JunctionLocked
}

/// <summary>What a plain belt placed over a plain belt does (<see cref="IGameView.BeltOverBeltAt"/>); tier = conveyor id.</summary>
public enum BeltOverBelt
{
    /// <summary>Not a belt over a free plain belt: placed (or refused) as usual.</summary>
    None,

    /// <summary>Same belt, same direction: nothing to do, silently.</summary>
    Same,

    /// <summary>Same belt, another direction: it turns in place, at once and free, keeping its items.</summary>
    Rotate,

    /// <summary>Another tier: the belt is replaced (not produced yet).</summary>
    Replace
}

/// <summary>
/// A refusal: its <paramref name="Code"/> and the arguments the client needs to explain it. <paramref name="StructureId"/>
/// names the structure of <see cref="RefusalCode.Locked"/> and <see cref="RefusalCode.InsufficientResources"/>;
/// <paramref name="MissingMoney"/> and <paramref name="MissingItems"/> are the shortfall of
/// <see cref="RefusalCode.InsufficientResources"/> (both empty when it is unknown).
/// </summary>
public sealed record Refusal(
    RefusalCode Code,
    string? StructureId = null,
    int MissingMoney = 0,
    IReadOnlyList<ResourceAmount>? MissingItems = null)
{
    public static implicit operator Refusal(RefusalCode code) => new(code);

    // Value equality on the missing items too (a record compares the list by reference).
    public bool Equals(Refusal? other) =>
        other is not null
        && Code == other.Code
        && StructureId == other.StructureId
        && MissingMoney == other.MissingMoney
        && (MissingItems ?? []).SequenceEqual(other.MissingItems ?? []);

    public override int GetHashCode() => HashCode.Combine(Code, StructureId, MissingMoney);
}
