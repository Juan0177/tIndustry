namespace TIndustry.Shared;

/// <summary>
/// Static miner produce loop (shared math with Logistics MinerBuilding).
/// Miner T1 and T2 are 2×2, the impact drill 3×3 (<see cref="SizeFor"/>); ejects onto adjacent belt cells facing
/// outward from the footprint.
/// </summary>
public sealed class Miner
{
    /// <summary>Edge of the Miner T1 and T2 (the starter deposits are cut to it).</summary>
    public const int T1Size = 2;

    /// <summary>Edge of the impact drill.</summary>
    public const int ImpactDrillSize = 3;

    public const float MiningDurationSeconds = 2f;
    public const string BasicId = "miner";
    public const string AdvancedId = "miner-advanced";
    public const string ImpactDrillId = "impact-drill";
    public const string DefaultOutputItemId = "iron-ore";

    /// <summary>Footprint edge of the miner <paramref name="definitionId"/>: 3 for the impact drill, 2 otherwise.</summary>
    public static int SizeFor(string? definitionId) => definitionId == ImpactDrillId ? ImpactDrillSize : T1Size;

    /// <summary>
    /// Own mining speed of <paramref name="definitionId"/> (pieces per <see cref="MiningDurationSeconds"/> at full
    /// efficiency, before power): T1 1, T2 2, impact drill 8 (four times the T2).
    /// </summary>
    public static float SpeedOf(string? definitionId) => definitionId switch
    {
        AdvancedId => 2f,
        ImpactDrillId => 8f,
        _ => 1f
    };

    public Miner(
        GridPosition position,
        Direction direction,
        int? coveredDepositTiles = null,
        string outputItemId = DefaultOutputItemId,
        string definitionId = BasicId,
        long id = 0)
    {
        Id = id;
        Position = position;
        Direction = direction;
        OutputItemId = string.IsNullOrWhiteSpace(outputItemId) ? DefaultOutputItemId : outputItemId;
        DefinitionId = string.IsNullOrWhiteSpace(definitionId) ? BasicId : definitionId;
        Size = SizeFor(DefinitionId);
        CoveredDepositTiles = coveredDepositTiles ?? FootprintArea;
    }

    /// <summary>Stable entity id assigned by <see cref="Factory"/> at creation (saved).</summary>
    public long Id { get; }

    public GridPosition Position { get; private set; }
    public Direction Direction { get; private set; }
    public int CoveredDepositTiles { get; }
    public string OutputItemId { get; }
    public string DefinitionId { get; }

    /// <summary>Footprint edge (<see cref="SizeFor"/>).</summary>
    public int Size { get; }

    public int FootprintArea => Size * Size;

    /// <summary>Outer edge cells it ejects on: 8 for a 2×2, 12 for the drill.</summary>
    public int OutputTileCount => Size * 4;

    public void Relocate(GridPosition position, Direction direction)
    {
        Position = position;
        Direction = direction;
    }

    public void RestoreProgress(float progress, int ejectIndex)
    {
        Progress = Math.Clamp(progress, 0f, 1f);
        EjectIndex = ((ejectIndex % OutputTileCount) + OutputTileCount) % OutputTileCount;
    }

    public float Progress { get; private set; }
    public int EjectIndex { get; private set; }
    public long ItemsProduced { get; private set; }

    /// <summary>True while the T2 or the drill gets some power from its network this tick.</summary>
    public bool IsPowered => PowerBp > 0;

    /// <summary>Share of its power use the T2 or the drill got this tick, 0–10 000 basis points (always 0 for T1; never saved).</summary>
    public int PowerBp { get; private set; }

    /// <summary>True when the T2 or the drill works this tick and so asks its network for its power use.</summary>
    public bool WantsPower => CanReceivePower && IsWorking;

    /// <summary>True when this miner's power use is in its network's demand this tick (set by the power systems).</summary>
    public bool PowerRequested { get; internal set; }

    private void SetPower(int powerBp) => PowerBp = CanReceivePower ? Math.Clamp(powerBp, 0, 10_000) : 0;

    /// <summary>On a deposit (gears should spin).</summary>
    public bool IsWorking => Efficiency > 0f;

    public float MiningSpeed => SpeedOf(DefinitionId);

    /// <summary>The T2 and the drill are power blocks; the T1 is not.</summary>
    public bool CanReceivePower => DefinitionId is AdvancedId or ImpactDrillId;

    /// <summary>The drill works only with power (speed × satisfaction); the T2 also without (power is a bonus).</summary>
    public bool NeedsPower => DefinitionId == ImpactDrillId;

    public float Efficiency
    {
        get
        {
            if (CoveredDepositTiles <= 0)
            {
                return 0f;
            }

            // covered / footprint; the T2 alone gets ×1.25 (capped at 1), the drill does not.
            var raw = CoveredDepositTiles / (float)FootprintArea;
            return DefinitionId == AdvancedId ? Math.Min(1f, raw * 1.25f) : raw;
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
    public bool Tick(float deltaSeconds, BeltLane belt, ref long nextItemId, int powerBp = 0)
    {
        SetPower(powerBp);
        if (!AdvanceToReady(deltaSeconds))
        {
            return false;
        }

        var start = EjectIndex;
        for (var step = 0; step < OutputTileCount; step++)
        {
            var slot = (start + step) % OutputTileCount;
            var outputPosition = OutputTileAt(slot);
            if (!IsOutwardBeltCell(belt, outputPosition))
            {
                continue;
            }

            if (!belt.TryInsertAt(outputPosition, new TransportedItem(nextItemId, OutputItemId)))
            {
                continue;
            }

            nextItemId++;
            ItemsProduced++;
            EjectIndex = (slot + 1) % OutputTileCount;
            Progress = 0f;
            return true;
        }

        return false;
    }

    public bool Tick(float deltaSeconds, BeltGrid grid, ref long nextItemId, int powerBp = 0,
        Func<string, bool>? tryDeliverAdjacent = null)
    {
        SetPower(powerBp);
        if (!AdvanceToReady(deltaSeconds))
        {
            return false;
        }

        // Prefer footprint-touch transfer (Raylib parity) before belt eject.
        if (tryDeliverAdjacent?.Invoke(OutputItemId) == true)
        {
            ItemsProduced++;
            Progress = 0f;
            return true;
        }

        var start = EjectIndex;
        for (var step = 0; step < OutputTileCount; step++)
        {
            var slot = (start + step) % OutputTileCount;
            var outputPosition = OutputTileAt(slot);
            if (!IsOutwardBeltCell(grid, outputPosition))
            {
                continue;
            }

            // Source: the footprint tile beside the output tile (an armored belt checks it).
            var source = outputPosition.Step(DirectionMath.Opposite(DirectionMath.All[slot / Size]));
            if (!grid.TryInsert(outputPosition, new TransportedItem(nextItemId, OutputItemId), source: source))
            {
                continue;
            }

            nextItemId++;
            ItemsProduced++;
            EjectIndex = (slot + 1) % OutputTileCount;
            Progress = 0f;
            return true;
        }

        return false;
    }

    private bool AdvanceToReady(float deltaSeconds)
    {
        if (Efficiency <= 0f)
        {
            return false;
        }

        // Drill: its speed × s (stalled at 0). T2: 1 + 0.2 × s on top of its own speed. T1: PowerBp is always 0.
        var powerMul = NeedsPower ? PowerBp / 10_000f : 1f + PowerBp / 50_000f;
        if (powerMul <= 0f)
        {
            return false;
        }

        Progress = Math.Min(
            1f,
            Progress + deltaSeconds * Efficiency * MiningSpeed * powerMul / MiningDurationSeconds);
        return Progress >= 1f;
    }

    private bool IsOutwardBeltCell(BeltLane belt, GridPosition outputPosition)
    {
        for (var i = 0; i < belt.Cells.Count; i++)
        {
            if (!belt.Cells[i].Position.Equals(outputPosition))
            {
                continue;
            }

            return IsOutwardDirection(belt.DirectionAt(i), outputPosition);
        }

        return false;
    }

    private bool IsOutwardBeltCell(BeltGrid grid, GridPosition outputPosition) =>
        grid.TryGet(outputPosition, out var cell)
        && (cell.Kind is LogisticsKind.Junction or LogisticsKind.Router or LogisticsKind.Sorter
            || IsOutwardDirection(cell.Direction, outputPosition));

    private bool IsOutwardDirection(Direction dir, GridPosition outputPosition)
    {
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
}
