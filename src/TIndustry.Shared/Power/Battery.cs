namespace TIndustry.Shared;

/// <summary>
/// Battery (<see cref="BuildingId"/> 1×1, <see cref="LargeId"/> 3×3): a power block that stores the surplus of its
/// network and gives it back when the generators do not cover the demand (spec «Rete di corrente»). Its capacity is
/// the <see cref="BuildingDefinition.PowerCapacity"/> of its definition (<see cref="CapacityOf"/>).
/// </summary>
public sealed class Battery
{
    public const string BuildingId = "battery";
    public const string LargeId = "battery-large";

    public const int SmallSize = 1;
    public const int LargeSize = 3;

    public Battery(GridPosition position, long id = 0, string definitionId = BuildingId)
    {
        Id = id;
        Position = position;
        DefinitionId = definitionId;
        Size = SizeFor(definitionId);
    }

    /// <summary>Stable entity id assigned by <see cref="Factory"/> at creation (saved).</summary>
    public long Id { get; }

    /// <summary>Top-left cell.</summary>
    public GridPosition Position { get; }

    /// <summary><see cref="BuildingId"/> or <see cref="LargeId"/> (saved).</summary>
    public string DefinitionId { get; }

    /// <summary>Edge of the square: 1 for the battery, 3 for the large battery.</summary>
    public int Size { get; }

    /// <summary>
    /// Stored energy in sixty-thousandths of a unit (<see cref="BuildingDefinition.ChargePerUnit"/>), 0 to
    /// <see cref="CapacityOf"/>. Set by the balance of its network (<see cref="PowerSystem"/>) and by a restore.
    /// </summary>
    public long Charge { get; internal set; }

    /// <summary>Edge of the square of a battery <paramref name="definitionId"/>.</summary>
    public static int SizeFor(string definitionId) => definitionId == LargeId ? LargeSize : SmallSize;

    /// <summary>True for the ids of the batteries.</summary>
    public static bool IsBatteryId(string? id) => id is BuildingId or LargeId;

    /// <summary>
    /// Charge of a full battery <paramref name="definitionId"/>, from the content (the spec's 1 000 or 20 000 units for
    /// code-built content without one).
    /// </summary>
    public static long CapacityOf(FactoryContent content, string definitionId = BuildingId) =>
        (content.FindBuilding(definitionId)?.PowerCapacity is > 0 and var units
            ? units
            : definitionId == LargeId ? DefaultLargeCapacity : DefaultCapacity)
        * BuildingDefinition.ChargePerUnit;

    /// <summary>The spec's capacity in units, used when the content (built in code) does not give one.</summary>
    public const int DefaultCapacity = 1000;

    /// <summary>The spec's capacity of the large battery in units, used when the content (built in code) does not give one.</summary>
    public const int DefaultLargeCapacity = 20_000;

    public bool Occupies(GridPosition tile) =>
        tile.X >= Position.X && tile.X < Position.X + Size && tile.Y >= Position.Y && tile.Y < Position.Y + Size;

    public IEnumerable<GridPosition> OccupiedTiles()
    {
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                yield return new GridPosition(Position.X + x, Position.Y + y);
            }
        }
    }
}
