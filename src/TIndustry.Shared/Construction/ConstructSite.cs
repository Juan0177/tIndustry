namespace TIndustry.Shared;

/// <summary>
/// A block being built or taken apart (Mindustry's <c>ConstructBuild</c>). It occupies the cells of
/// the final block and does no work. <see cref="Construction.Advance"/> moves <see cref="Progress"/>
/// (0–1), takes the cost from the Core while building and gives refunds back while deconstructing.
/// </summary>
public sealed class ConstructSite
{
    public ConstructSite(
        long id,
        BuildSpec spec,
        IReadOnlyList<GridPosition> tiles,
        IReadOnlyList<CostEntry> cost,
        int ownerPlayerId)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentNullException.ThrowIfNull(cost);
        Id = id;
        Spec = spec;
        Tiles = tiles.ToArray();
        Cost = cost.ToArray();
        SpentLines = new int[Cost.Count];
        RefundedLines = new int[Cost.Count];
        Spent = Array.AsReadOnly(SpentLines);
        Refunded = Array.AsReadOnly(RefundedLines);
        OwnerPlayerId = ownerPlayerId;
    }

    public long Id { get; }
    public BuildSpec Spec { get; }

    /// <summary>Cells reserved for the block (a bridge has its two ends).</summary>
    public IReadOnlyList<GridPosition> Tiles { get; }

    /// <summary>0 = nothing built, 1 = complete.</summary>
    public float Progress { get; internal set; }

    /// <summary>Taken from the Core so far, per <see cref="Cost"/> line (read-only).</summary>
    public IReadOnlyList<int> Spent { get; }

    /// <summary>Given back so far while deconstructing, per <see cref="Cost"/> line (read-only).</summary>
    public IReadOnlyList<int> Refunded { get; }

    /// <summary>The live values behind <see cref="Spent"/>; only construction and restore change them.</summary>
    internal int[] SpentLines { get; }

    /// <summary>The live values behind <see cref="Refunded"/>.</summary>
    internal int[] RefundedLines { get; }

    /// <summary>Whole cost of the block: money first (if any), then materials by ordinal id.</summary>
    public IReadOnlyList<CostEntry> Cost { get; }

    public bool Deconstructing { get; internal set; }

    /// <summary>
    /// Share of <see cref="Spent"/> given back by the deconstruction in progress, in basis points
    /// (0–10000, 10000 = 100%): game rule % × building refund % for a finished building, 10000 for a
    /// cancelled site. Basis points keep <c>ceil(cost × rule% × building%)</c> a single exact rounding (<see cref="Construction.RefundShare"/>).
    /// </summary>
    public int RefundBasisPoints { get; internal set; } = Construction.FullRefundBasisPoints;

    public int OwnerPlayerId { get; }

    /// <summary>
    /// The cost line that stopped the last tick, with the amount still short of finishing
    /// (cost − spent − available); null when the last tick was not stalled.
    /// </summary>
    public CostEntry? Missing { get; internal set; }

    /// <summary>
    /// Ticks in a row the site has been stalled while its drone worked on it (capped at
    /// <see cref="Construction.StuckTicks"/>). At the cap, with other plans waiting, the drone moves the site's
    /// plan to the back of its queue (Mindustry's <c>plan.stuck</c>); the site stays where it stopped.
    /// </summary>
    public int StalledTicks { get; internal set; }

    /// <summary>
    /// The blocks this site replaces (Mindustry's replace: another 1×1 logistics block on the same cell), each with its
    /// items and state; null for an ordinary site, never empty otherwise. Their cost covers the new one's common part
    /// (<see cref="Construction.ReplacementCredit"/>, counted in <see cref="Spent"/> while building and taken out when
    /// cancelled: a deconstructing site with <see cref="Replaced"/> has it out already). Cancelled, it puts them back.
    /// </summary>
    public IReadOnlyList<ReplacedBlock>? Replaced { get; internal set; }

    public bool Occupies(GridPosition tile)
    {
        foreach (var t in Tiles)
        {
            if (t.Equals(tile))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// A block taken away by a replacement site: its spec (a sorter's filter included), its items (front first, with their
/// positions, junction queues and pack counts) and the state it needs to come back as it was (<see cref="State"/>, null
/// when it has none). New kinds of replaceable blocks add their own <see cref="ReplacedState"/>.
/// </summary>
public sealed record ReplacedBlock(BuildSpec Spec, IReadOnlyList<TransportedItem> Items, ReplacedState? State = null);

/// <summary>The state a replaced block keeps beyond its spec and items, one kind per family of blocks.</summary>
public abstract record ReplacedState;

/// <summary>
/// A replaced logistics cell's state: a sorter's or gate's turn bits (<see cref="BeltGridCell.SorterTurns"/>), a router's
/// turn, wait and side of arrival (<see cref="BeltGridCell.RouterTurn"/>, <see cref="BeltGridCell.RouterWaitTicks"/>,
/// <see cref="BeltGridCell.RouterFrom"/>), a stack conveyor's wait (<see cref="BeltGridCell.StackWaitTicks"/>). The
/// fields of other kinds are 0 (null).
/// </summary>
public sealed record ReplacedLogisticsState(
    int SorterTurns = 0,
    int RouterTurn = 0,
    int RouterWaitTicks = 0,
    Direction? RouterFrom = null,
    int StackWaitTicks = 0) : ReplacedState;
