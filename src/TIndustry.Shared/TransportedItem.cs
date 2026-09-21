namespace TIndustry.Shared;

/// <summary>Item on a belt cell; Progress 0..1 along the cell (same convention as Logistics).</summary>
public sealed class TransportedItem
{
    public TransportedItem(long id, string itemId, float progress = 0f)
    {
        Id = id;
        ItemId = itemId;
        Progress = progress;
    }

    public long Id { get; }
    public string ItemId { get; }
    public float Progress { get; set; }
}
