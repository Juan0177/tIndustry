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

    /// <summary>
    /// Stored energy in sixty-thousandths of a unit (<see cref="BuildingDefinition.ChargePerUnit"/>), 0 to
    /// <see cref="CapacityOf"/>. Set by the balance of its network (<see cref="PowerSystem"/>) and by a restore.
    /// </summary>
    public long Charge { get; internal set; }

    /// <summary>Charge of a full battery, from the content (the spec's 1 000 units for code-built content without one).</summary>
    public static long CapacityOf(FactoryContent content) =>
        (content.FindBuilding(BuildingId)?.PowerCapacity is > 0 and var units ? units : DefaultCapacity)
        * BuildingDefinition.ChargePerUnit;

    /// <summary>The spec's capacity in units, used when the content (built in code) does not give one.</summary>
    public const int DefaultCapacity = 1000;

    public bool Occupies(GridPosition tile) => tile.Equals(Position);

    public IEnumerable<GridPosition> OccupiedTiles()
    {
        yield return Position;
    }
}
