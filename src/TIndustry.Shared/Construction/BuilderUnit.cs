namespace TIndustry.Shared;

/// <summary>
/// One entry of a drone's build queue (Mindustry's <c>BuildPlan</c>): build <paramref name="Spec"/>,
/// or, with <paramref name="Breaking"/>, take apart what stands on <c>Spec.Origin</c> (the clicked
/// cell; the other fields only describe what was there when the plan was made).
/// </summary>
public sealed record BuildPlan(BuildSpec Spec, bool Breaking);

/// <summary>
/// A player's construction drone (Mindustry's alpha). It flies to the first plan of its queue and works
/// on one site at a time. Position is the drone's centre in tiles: cell (x, y) spans [x, x+1) × [y, y+1).
/// </summary>
public sealed class BuilderUnit
{
    public BuilderUnit(long id, int ownerPlayerId, float x, float y)
    {
        Id = id;
        OwnerPlayerId = ownerPlayerId;
        X = x;
        Y = y;
    }

    public long Id { get; }

    public int OwnerPlayerId { get; }

    /// <summary>Centre, in tiles.</summary>
    public float X { get; internal set; }

    /// <summary>Centre, in tiles.</summary>
    public float Y { get; internal set; }

    /// <summary>Direction of the last movement, in degrees (atan2 of the step, 0 = +X).</summary>
    public float Rotation { get; internal set; }

    /// <summary>Build queue: the first plan is the one being reached or worked on.</summary>
    public List<BuildPlan> Plans { get; } = [];

    /// <summary>Site of the first plan once work has begun; null while flying or idle.</summary>
    public long? CurrentSiteId { get; internal set; }
}
