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
        Spent = new int[Cost.Count];
        Refunded = new int[Cost.Count];
        OwnerPlayerId = ownerPlayerId;
    }

    public long Id { get; }
    public BuildSpec Spec { get; }

    /// <summary>Cells reserved for the block (a bridge has its two ends).</summary>
    public IReadOnlyList<GridPosition> Tiles { get; }

    /// <summary>0 = nothing built, 1 = complete.</summary>
    public float Progress { get; internal set; }

    /// <summary>Taken from the Core so far, per <see cref="Cost"/> line.</summary>
    public int[] Spent { get; }

    /// <summary>Given back so far while deconstructing, per <see cref="Cost"/> line.</summary>
    public int[] Refunded { get; }

    /// <summary>Whole cost of the block: money first (if any), then materials by ordinal id.</summary>
    public IReadOnlyList<CostEntry> Cost { get; }

    public bool Deconstructing { get; internal set; }

    /// <summary>
    /// Share of <see cref="Spent"/> given back by the deconstruction in progress, in basis points
    /// (0–10000, 10000 = 100%): game rule % × building refund % for a finished building, 10000 for a
    /// cancelled site. Basis points keep <c>floor(cost × rule% × building%)</c> a single exact floor.
    /// </summary>
    public int RefundBasisPoints { get; internal set; } = Construction.FullRefundBasisPoints;

    public int OwnerPlayerId { get; }

    /// <summary>
    /// The cost line that stopped the last tick, with the amount still short of finishing
    /// (cost − spent − available); null when the last tick was not stalled.
    /// </summary>
    public CostEntry? Missing { get; internal set; }

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
