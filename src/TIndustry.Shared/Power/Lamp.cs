namespace TIndustry.Shared;

/// <summary>
/// 1×1 lamp: a plain power consumer that only lights the map at night (looks only). It is a block of its power
/// network and asks for its <see cref="BuildingDefinition.PowerUse"/> only in the ticks whose world light
/// (<see cref="DayNightClock.LightAt"/>) is below <see cref="DarkBelow"/>; then its light is the share it gets. By day it
/// asks for nothing and stays dark, so it never drains the batteries.
/// </summary>
public sealed class Lamp
{
    public const int Size = 1;
    public const string BuildingId = "lamp";

    /// <summary>World light (0–<see cref="DayNightClock.FullLight"/>) below which a lamp wants to work: half the day's light.</summary>
    public const int DarkBelow = DayNightClock.FullLight / 2;

    public Lamp(GridPosition position, long id = 0)
    {
        Id = id;
        Position = position;
    }

    /// <summary>Stable entity id assigned by <see cref="Factory"/> at creation (saved).</summary>
    public long Id { get; }

    public GridPosition Position { get; }

    /// <summary>Share of its power use the lamp got in the last tick, 0–10 000 basis points (not saved: the next tick sets it).</summary>
    public int LightBp { get; private set; }

    /// <summary>Light of the lamp, 0 (dark) to 1: the satisfaction of its network.</summary>
    public float Light => LightBp / 10_000f;

    /// <summary>True when the lamp got some power in the last tick.</summary>
    public bool IsLit => LightBp > 0;

    /// <summary>True when this lamp's power use is in its network's demand this tick (set by the power systems).</summary>
    public bool PowerRequested { get; internal set; }

    public bool Occupies(GridPosition tile) => tile.Equals(Position);

    public IEnumerable<GridPosition> OccupiedTiles()
    {
        yield return Position;
    }

    /// <summary>One tick with <paramref name="powerBp"/> (0–10 000) of its power use: that is its light.</summary>
    public void Tick(int powerBp) => LightBp = Math.Clamp(powerBp, 0, 10_000);
}
