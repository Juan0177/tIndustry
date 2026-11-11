namespace TIndustry.Shared;

/// <summary>Item on a belt cell; Progress 0..1 along the cell (same convention as Logistics).</summary>
public sealed class TransportedItem
{
    public TransportedItem(long id, string itemId, float progress = 0f, Direction? travel = null)
    {
        Id = id;
        ItemId = itemId;
        Progress = progress;
        Travel = travel;
    }

    public long Id { get; }
    public string ItemId { get; }

    /// <summary>
    /// Pieces of <see cref="ItemId"/> it stands for: 1 for an item, 1 to <see cref="BeltGrid.StackPackSize"/> for a pack on
    /// a stack conveyor (<see cref="LogisticsKind.Stack"/>). Only stack cells ever hold more than one.
    /// </summary>
    public int Count { get; set; } = 1;

    public float Progress { get; set; }
    /// <summary>Direction of travel through a junction: the queue the item is in, and the side it leaves by. Null on belts.</summary>
    public Direction? Travel { get; set; }

    /// <summary>
    /// Ticks spent inside a junction queue (0 on entry, up to <see cref="BeltGridCell.JunctionCrossTicks"/>); null
    /// outside a junction. Saved: an item leaves a restored junction on the same tick it would have.
    /// </summary>
    public int? JunctionTicks { get; set; }

    /// <summary>
    /// The part of the current tick (0–1) the item could not move for, for example past the end of its cell (set by
    /// <see cref="BeltCell.Advance"/>, spent by the next cell's <see cref="BeltCell.TryInsert"/> at that cell's speed),
    /// so a handoff never stalls the item nor makes it jump; null for an item that has not moved on a belt since it was
    /// last put on one. Lives within one tick: not saved.
    /// </summary>
    internal float? Carry { get; set; }
}
