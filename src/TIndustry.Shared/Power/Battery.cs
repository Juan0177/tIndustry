namespace TIndustry.Shared;

/// <summary>
/// 1×1 battery: a power block that stores the surplus of its network and gives it back when the generators do not
/// cover the demand (spec «Rete di corrente»). Its capacity is <see cref="BuildingDefinition.PowerCapacity"/>.
/// </summary>
public sealed class Battery
{
    public const int Size = 1;
    public const string BuildingId = "battery";

    public Battery(GridPosition position, long id = 0)
    {
        Id = id;
        Position = position;
    }

    /// <summary>Stable entity id assigned by <see cref="Factory"/> at creation (saved).</summary>
    public long Id { get; }

    public GridPosition Position { get; }

    public bool Occupies(GridPosition tile) => tile.Equals(Position);

    public IEnumerable<GridPosition> OccupiedTiles()
    {
        yield return Position;
    }
}
