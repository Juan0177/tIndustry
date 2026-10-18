namespace TIndustry.Shared;

/// <summary>
/// 2×2 generator that burns coal from belts and
/// feeds the factory power buffer (capacity/generation) while marking
/// adjacent craft as network-connected for TrySpendPower.
/// </summary>
public sealed class Generator
{
    public const int Size = 2;
    public const string BuildingId = "generator";
    public const string FuelItemId = "coal";
    public const int FuelBufferCapacity = 8;
    public const float SecondsPerFuel = 8f;
    public const float PoweredCraftSpeedMultiplier = 1.20f;

    /// <summary>Raylib parity: capacity added per live (burning) generator.</summary>
    public const float CapacityBonus = 40f;
    /// <summary>Raylib parity: watts added to the shared buffer while burning.</summary>
    public const float GenerationPerSecond = 28f;

    public Generator(GridPosition position, Direction direction = Direction.East, long id = 0)
    {
        Id = id;
        Position = position;
        Direction = direction;
    }

    /// <summary>Stable entity id assigned by <see cref="Factory"/> at creation (saved).</summary>
    public long Id { get; }

    public GridPosition Position { get; private set; }
    public Direction Direction { get; private set; }
    public int FuelBuffer { get; private set; }
    public float BurnRemaining { get; private set; }
    public bool IsGenerating => BurnRemaining > 0f;
    public long FuelConsumed { get; private set; }

    public void Relocate(GridPosition position, Direction direction)
    {
        Position = position;
        Direction = direction;
    }

    public void RestoreFuel(int fuelBuffer, float burnRemaining, long fuelConsumed = 0)
    {
        FuelBuffer = Math.Clamp(fuelBuffer, 0, FuelBufferCapacity);
        BurnRemaining = Math.Max(0f, burnRemaining);
        FuelConsumed = Math.Max(0, fuelConsumed);
    }

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

    public bool Occupies(GridPosition tile)
    {
        foreach (var t in OccupiedTiles())
        {
            if (t.Equals(tile))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryAcceptFuel(string itemId)
    {
        if (itemId != FuelItemId || FuelBuffer >= FuelBufferCapacity)
        {
            return false;
        }

        FuelBuffer++;
        return true;
    }

    public int AcceptFromBelts(BeltGrid belts)
    {
        var accepted = 0;
        foreach (var cell in belts.Cells.Values)
        {
            if (!Occupies(cell.OutputPosition))
            {
                continue;
            }

            while (cell.PeekOutput() is { } item)
            {
                if (!TryAcceptFuel(item.ItemId))
                {
                    break;
                }

                cell.RemoveOutput();
                accepted++;
            }
        }

        return accepted;
    }

    /// <summary>Burns fuel; true while generating this tick.</summary>
    public bool Tick(float deltaSeconds, BeltGrid belts)
    {
        AcceptFromBelts(belts);
        if (BurnRemaining <= 0f)
        {
            if (FuelBuffer <= 0)
            {
                return false;
            }

            FuelBuffer--;
            FuelConsumed++;
            BurnRemaining = SecondsPerFuel;
        }

        BurnRemaining = Math.Max(0f, BurnRemaining - deltaSeconds);
        return true;
    }

    /// <summary>4-connected adjacency between two 2×2 footprints.</summary>
    public static bool FootprintsAdjacent(GridPosition a, int aSize, GridPosition b, int bSize)
    {
        for (var ay = 0; ay < aSize; ay++)
        {
            for (var ax = 0; ax < aSize; ax++)
            {
                var at = new GridPosition(a.X + ax, a.Y + ay);
                foreach (var dir in DirectionMath.All)
                {
                    var n = at.Step(dir);
                    if (n.X >= b.X && n.X < b.X + bSize && n.Y >= b.Y && n.Y < b.Y + bSize)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// <see cref="FootprintsAdjacent"/> with a bounding-box test first: same answer, but cheap for the many far-away
    /// pairs (every lamp and craft machine checks every live generator each tick).
    /// </summary>
    public bool IsAdjacentTo(GridPosition origin, int size) =>
        origin.X <= Position.X + Size && origin.X + size >= Position.X
        && origin.Y <= Position.Y + Size && origin.Y + size >= Position.Y
        && FootprintsAdjacent(Position, Size, origin, size);
}
