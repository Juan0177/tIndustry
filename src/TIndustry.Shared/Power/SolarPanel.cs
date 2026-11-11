namespace TIndustry.Shared;

/// <summary>
/// Solar panel (<see cref="SmallId"/> 1×1, <see cref="LargeId"/> 3×3): a generator of its power network that burns
/// nothing. Each tick it produces its <see cref="BuildingDefinition.PowerOutput"/> × the world's light
/// (<see cref="DayNightClock.LightAt"/>) / <see cref="DayNightClock.FullLight"/>, in integer thousandths of a unit per
/// second (<see cref="RateAt"/>): all of it by day, nothing by night, linearly in between at dusk and dawn. Joined to its
/// network by contact and by node lasers through its <see cref="Id"/>, like the other power blocks.
/// </summary>
public sealed class SolarPanel
{
    public const string SmallId = "solar";
    public const string LargeId = "solar-large";

    public const int SmallSize = 1;
    public const int LargeSize = 3;

    public SolarPanel(GridPosition position, string definitionId, long id = 0)
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

    /// <summary><see cref="SmallId"/> or <see cref="LargeId"/> (saved).</summary>
    public string DefinitionId { get; }

    /// <summary>Edge of the square: 1 for the panel, 3 for the large panel.</summary>
    public int Size { get; }

    /// <summary>
    /// What the panel produced in the last power balance, thousandths of a unit per second (not saved: every tick's
    /// <see cref="PowerSystem"/> sets it before anything reads it; for the client and the agents).
    /// </summary>
    public long CurrentRate { get; internal set; }

    /// <summary>Edge of the square of a solar panel <paramref name="definitionId"/>.</summary>
    public static int SizeFor(string definitionId) => definitionId == LargeId ? LargeSize : SmallSize;

    /// <summary>True for the ids of the solar panels.</summary>
    public static bool IsSolarId(string? id) => id is SmallId or LargeId;

    /// <summary>
    /// Output of a panel with <paramref name="outputRate"/> (thousandths per second at full light) under
    /// <paramref name="light"/> (0–<see cref="DayNightClock.FullLight"/>): floored to the thousandth.
    /// </summary>
    public static long RateAt(long outputRate, int light) =>
        outputRate * Math.Clamp(light, 0, DayNightClock.FullLight) / DayNightClock.FullLight;

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
