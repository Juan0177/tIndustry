namespace TIndustry.Shared;

/// <summary>
/// 1×1 extractor: pulls a filtered item from Core wallet or adjacent craft output
/// onto an outward belt (Raylib ExtractorBuilding parity, Shared stub).
/// </summary>
public sealed class ExtractorStub
{
    public const int Size = 1;
    public const float IntervalSeconds = 0.4f;
    public const string BuildingId = "extractor";
    public const string DefaultFilterItemId = "iron-plate";

    public ExtractorStub(
        GridPosition position,
        Direction direction,
        string filterItemId = DefaultFilterItemId)
    {
        Position = position;
        Direction = direction;
        FilterItemId = string.IsNullOrWhiteSpace(filterItemId) ? DefaultFilterItemId : filterItemId;
    }

    public GridPosition Position { get; private set; }
    public Direction Direction { get; private set; }
    public string FilterItemId { get; private set; }
    public float Progress { get; private set; }
    public long ItemsExtracted { get; private set; }

    public void Relocate(GridPosition position, Direction direction)
    {
        Position = position;
        Direction = direction;
    }

    public void RestoreProgress(float progress) => Progress = Math.Max(0f, progress);

    public void SetFilterItem(string itemId)
    {
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            FilterItemId = itemId;
        }
    }

    public void CycleFilterItem(IReadOnlyList<string> itemIds)
    {
        if (itemIds.Count == 0)
        {
            return;
        }

        var index = 0;
        for (var i = 0; i < itemIds.Count; i++)
        {
            if (string.Equals(itemIds[i], FilterItemId, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        FilterItemId = itemIds[(index + 1) % itemIds.Count];
    }

    public IEnumerable<GridPosition> OccupiedTiles()
    {
        yield return Position;
    }

    public bool Occupies(GridPosition tile) => tile.Equals(Position);

    /// <summary>Advance and try to eject one filtered item from Core stock onto an outward belt.</summary>
    public bool Tick(
        float deltaSeconds,
        BeltGrid belts,
        EconomyWallet wallet,
        IReadOnlySet<GridPosition> coreTiles,
        ref long nextItemId)
    {
        Progress += deltaSeconds;
        if (Progress < IntervalSeconds)
        {
            return false;
        }

        var outPos = Position.Step(Direction);
        if (!belts.TryGet(outPos, out var belt)
            || !IsOutwardBelt(belt.Direction, outPos))
        {
            return false;
        }

        if (!TryPullFromCore(wallet, coreTiles))
        {
            Progress = 0f;
            return false;
        }

        if (!belts.TryInsert(outPos, new TransportedItem(nextItemId, FilterItemId)))
        {
            wallet.AddMaterial(FilterItemId, 1);
            return false;
        }

        nextItemId++;
        ItemsExtracted++;
        Progress = 0f;
        return true;
    }

    private bool IsOutwardBelt(Direction beltDir, GridPosition beltPos)
    {
        var next = beltPos.Step(beltDir);
        return !next.Equals(Position);
    }

    private bool TryPullFromCore(EconomyWallet wallet, IReadOnlySet<GridPosition> coreTiles)
    {
        foreach (var (neighbor, _) in PerimeterSlots(Position, Size))
        {
            if (coreTiles.Contains(neighbor) && wallet.TryRemoveMaterial(FilterItemId, 1))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<(GridPosition Tile, Direction Edge)> PerimeterSlots(
        GridPosition origin, int size)
    {
        for (var i = 0; i < size; i++)
        {
            yield return (new GridPosition(origin.X + i, origin.Y - 1), Direction.North);
            yield return (new GridPosition(origin.X + size, origin.Y + i), Direction.East);
            yield return (new GridPosition(origin.X + i, origin.Y + size), Direction.South);
            yield return (new GridPosition(origin.X - 1, origin.Y + i), Direction.West);
        }
    }
}
