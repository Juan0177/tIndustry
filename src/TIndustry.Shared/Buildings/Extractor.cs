namespace TIndustry.Shared;

/// <summary>
/// 1×1 extractor: pulls a filtered item from the Core stock (touching the Core or a storage block linked to it) or,
/// failing that, from a touching standalone storage block (lowest id first) onto an outward belt (Raylib
/// ExtractorBuilding parity). A piece the belt does not take goes back where it came from.
/// </summary>
public sealed class Extractor
{
    public const int Size = 1;
    public const float IntervalSeconds = 0.4f;
    public const string BuildingId = "extractor";
    public const string DefaultFilterItemId = "iron-plate";

    public Extractor(
        GridPosition position,
        Direction direction,
        string filterItemId = DefaultFilterItemId,
        long id = 0)
    {
        Id = id;
        Position = position;
        Direction = direction;
        FilterItemId = string.IsNullOrWhiteSpace(filterItemId) ? DefaultFilterItemId : filterItemId;
    }

    /// <summary>Stable entity id assigned by <see cref="Factory"/> at creation (saved).</summary>
    public long Id { get; }

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

    /// <summary>Advance and try to eject one filtered item from Core stock or a standalone storage block onto an outward belt.</summary>
    public bool Tick(
        float deltaSeconds,
        BeltGrid belts,
        CoreStorage core,
        IReadOnlySet<GridPosition> coreTiles,
        ref long nextItemId,
        IReadOnlyList<StorageBlock>? storages = null)
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

        StorageBlock? source = null;
        if (!TryPullFromCore(core, coreTiles, storages) && (source = TryPullFromStorage(storages)) is null)
        {
            Progress = 0f;
            return false;
        }

        if (!belts.TryInsert(outPos, new TransportedItem(nextItemId, FilterItemId), source: Position))
        {
            // Taken and not delivered: back where it came from, even at the maximum (nothing lost).
            if (source is not null)
            {
                source.Return(FilterItemId, 1);
            }
            else
            {
                core.Return(FilterItemId, 1);
            }

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

    /// <summary>Takes one piece from the Core stock when the extractor touches the Core or a linked storage block.</summary>
    private bool TryPullFromCore(
        CoreStorage core, IReadOnlySet<GridPosition> coreTiles, IReadOnlyList<StorageBlock>? storages)
    {
        foreach (var (neighbor, _) in PerimeterSlots(Position, Size))
        {
            if (coreTiles.Contains(neighbor) || TouchedStorage(storages, neighbor) is { IsLinked: true })
            {
                return core.TryTake(FilterItemId);
            }
        }

        return false;
    }

    /// <summary>Takes one piece from the touching standalone storage block with the lowest id that has one.</summary>
    private StorageBlock? TryPullFromStorage(IReadOnlyList<StorageBlock>? storages)
    {
        if (storages is null || storages.Count == 0)
        {
            return null;
        }

        foreach (var storage in storages)
        {
            if (storage.IsLinked || !BuildingIo.FootprintsAdjacent(Position, Size, storage.Origin, storage.Size))
            {
                continue;
            }

            if (storage.TryTake(FilterItemId))
            {
                return storage;
            }
        }

        return null;
    }

    private static StorageBlock? TouchedStorage(IReadOnlyList<StorageBlock>? storages, GridPosition tile)
    {
        if (storages is null)
        {
            return null;
        }

        foreach (var storage in storages)
        {
            if (storage.Occupies(tile))
            {
                return storage;
            }
        }

        return null;
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
