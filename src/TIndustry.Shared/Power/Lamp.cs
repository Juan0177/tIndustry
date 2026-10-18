namespace TIndustry.Shared;

/// <summary>
/// 1×1 lamp: a plain power consumer that only lights the map at night (looks only). It takes power like a craft
/// machine (next to a burning generator, or next to a power node in range of one) and draws its
/// <see cref="BuildingDefinition.PowerUse"/> from the shared buffer every tick; <see cref="IsLit"/> says whether it
/// got it this tick.
/// </summary>
public sealed class Lamp
{
    public const int Size = 1;
    public const string BuildingId = "lamp";

    public Lamp(GridPosition position, long id = 0)
    {
        Id = id;
        Position = position;
    }

    /// <summary>Stable entity id assigned by <see cref="Factory"/> at creation (saved).</summary>
    public long Id { get; }

    public GridPosition Position { get; }

    /// <summary>True when the lamp got its power in the last tick (not saved: the next tick sets it again, like <see cref="Crafter.IsPowered"/>).</summary>
    public bool IsLit { get; private set; }

    public bool Occupies(GridPosition tile) => tile.Equals(Position);

    public IEnumerable<GridPosition> OccupiedTiles()
    {
        yield return Position;
    }

    /// <summary>One tick: lit when connected to a live generator and the buffer covers <paramref name="draw"/>.</summary>
    public void Tick(bool networkConnected, Func<float, bool> trySpendPower, float draw) =>
        IsLit = networkConnected && trySpendPower(draw);
}
