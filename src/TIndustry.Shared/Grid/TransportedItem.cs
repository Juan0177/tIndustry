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
    public float Progress { get; set; }
    /// <summary>Exit direction through a junction (incoming axis). Null on normal belts.</summary>
    public Direction? Travel { get; set; }

    /// <summary>
    /// Movement past the end of its cell in the current tick (set by <see cref="BeltCell.Advance"/>, spent by the next
    /// cell's <see cref="BeltCell.TryInsert"/>), so a handoff never stalls the item; null for an item that has not
    /// moved on a belt since it was last put on one. Lives within one tick: not saved.
    /// </summary>
    internal float? Carry { get; set; }
}
