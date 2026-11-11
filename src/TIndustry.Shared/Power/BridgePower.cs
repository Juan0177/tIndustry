namespace TIndustry.Shared;

/// <summary>
/// The power side of a powered bridge (<see cref="ConveyorDefinition.IsPoweredBridge"/>, the long bridge): a 1×1 power
/// consumer on the bridge's entry cell, joined to its network by contact and by node lasers through its
/// <see cref="Id"/> like a lamp. A logistics cell has no entity id, so the bridge gets this one when it is placed and
/// loses it when it is taken away. It asks for its <see cref="ConveyorDefinition.PowerUse"/> every tick (an idle
/// bridge still draws); the entry lets items cross only while <see cref="SatisfactionBp"/> is above 0
/// (<see cref="BeltGrid.BridgePowered"/>): partial power is enough, with no slower crossing.
/// </summary>
public sealed class BridgePower
{
    public const int Size = 1;

    public BridgePower(GridPosition entry, long id = 0)
    {
        Id = id;
        Entry = entry;
    }

    /// <summary>Stable entity id assigned by <see cref="Factory"/> at creation (saved with the entry's cell).</summary>
    public long Id { get; }

    /// <summary>The bridge's entry cell: the power block's square.</summary>
    public GridPosition Entry { get; }

    /// <summary>True when this bridge's power use is in its network's demand this tick (set by the power systems).</summary>
    public bool PowerRequested { get; internal set; }

    /// <summary>
    /// Share of its power use the bridge got in the last balance, 0–10 000 basis points (not saved: the next tick sets it;
    /// a loaded game waits one balance, the same for every copy).
    /// </summary>
    public int SatisfactionBp { get; internal set; }

    /// <summary>True when items may cross now: some power in the last balance.</summary>
    public bool IsPowered => SatisfactionBp > 0;
}
