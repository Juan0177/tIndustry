namespace TIndustry.Shared;

/// <summary>
/// Static miner produce loop (shared math with Logistics MinerBuilding).
/// Size 2×2; ejects onto adjacent belt cells facing outward from the footprint.
/// </summary>
public sealed class MinerProducer
{
    public const int Size = 2;
    public const int FootprintArea = Size * Size;
    public const int OutputTileCount = Size * 4;
    public const float MiningDurationSeconds = 2f;
    public const string BasicId = "miner";
    public const string DefaultOutputItemId = "iron-ore";

    public MinerProducer(
        GridPosition position,
        Direction direction,
        int coveredDepositTiles = FootprintArea,
        string outputItemId = DefaultOutputItemId,
        string definitionId = BasicId)
    {
        Position = position;
        Direction = direction;
        CoveredDepositTiles = coveredDepositTiles;
        OutputItemId = string.IsNullOrWhiteSpace(outputItemId) ? DefaultOutputItemId : outputItemId;
        DefinitionId = string.IsNullOrWhiteSpace(definitionId) ? BasicId : definitionId;
    }

    public GridPosition Position { get; }
    public Direction Direction { get; }
    public int CoveredDepositTiles { get; }
    public string OutputItemId { get; }
    public string DefinitionId { get; }
    public float Progress { get; private set; }
    public int EjectIndex { get; private set; }
    public long ItemsProduced { get; private set; }

    public float MiningSpeed => DefinitionId == "miner-advanced" ? 2f : 1f;

    public float Efficiency
    {
        get
        {
            if (CoveredDepositTiles <= 0)
            {
                return 0f;
            }

            var raw = CoveredDepositTiles / (float)FootprintArea;
            return DefinitionId == "miner-advanced" ? Math.Min(1f, raw * 1.25f) : raw;
        }
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

    public GridPosition OutputTileAt(int index)
    {
        var i = ((index % OutputTileCount) + OutputTileCount) % OutputTileCount;
        var edge = DirectionMath.All[i / Size];
        var offset = i % Size;
        return edge switch
        {
            Direction.North => new GridPosition(Position.X + offset, Position.Y - 1),
            Direction.East => new GridPosition(Position.X + Size, Position.Y + offset),
            Direction.South => new GridPosition(Position.X + offset, Position.Y + Size),
            Direction.West => new GridPosition(Position.X - 1, Position.Y + offset),
            _ => Position
        };
    }

    /// <summary>
    /// Advances mining progress and tries to insert one ore onto an outward belt cell.
    /// </summary>
    public bool Tick(float deltaSeconds, BeltLane belt, ref long nextItemId)
    {
        if (Efficiency <= 0f)
        {
            return false;
        }

        Progress = Math.Min(
            1f,
            Progress + deltaSeconds * Efficiency * MiningSpeed / MiningDurationSeconds);

        if (Progress < 1f)
        {
            return false;
        }

        var start = EjectIndex;
        for (var step = 0; step < OutputTileCount; step++)
        {
            var slot = (start + step) % OutputTileCount;
            var outputPosition = OutputTileAt(slot);
            if (!TryInsertOutward(belt, outputPosition, ref nextItemId))
            {
                continue;
            }

            ItemsProduced++;
            EjectIndex = (slot + 1) % OutputTileCount;
            Progress = 0f;
            return true;
        }

        return false;
    }

    private bool TryInsertOutward(BeltLane belt, GridPosition outputPosition, ref long nextItemId)
    {
        if (!IsOutwardBeltCell(belt, outputPosition))
        {
            return false;
        }

        if (!belt.TryInsertAt(outputPosition, new TransportedItem(nextItemId, OutputItemId)))
        {
            return false;
        }

        nextItemId++;
        return true;
    }

    private bool IsOutwardBeltCell(BeltLane belt, GridPosition outputPosition)
    {
        for (var i = 0; i < belt.Cells.Count; i++)
        {
            if (!belt.Cells[i].Position.Equals(outputPosition))
            {
                continue;
            }

            // Outward: belt direction should leave the miner footprint (not face into it).
            var dir = belt.DirectionAt(i);
            var next = outputPosition.Step(dir);
            foreach (var tile in OccupiedTiles())
            {
                if (next.Equals(tile))
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }
}
