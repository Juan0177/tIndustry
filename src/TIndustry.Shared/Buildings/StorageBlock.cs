namespace TIndustry.Shared;

/// <summary>
/// Container or vault: a square storage block. Linked (touching the Core edge to edge) it has no contents of its own
/// and adds <see cref="Capacity"/> to every item's maximum in the Core (<see cref="CoreStorage.RecomputeLinks"/>);
/// standalone it keeps up to <see cref="Capacity"/> pieces of each item. Contents are kept by ordinal item id, so
/// every pass over them is deterministic; counts are integers.
/// </summary>
public sealed class StorageBlock
{
    public const int ContainerSize = 2;
    public const int VaultSize = 3;

    private readonly SortedDictionary<string, int> contents = new(StringComparer.Ordinal);

    /// <summary>Footprint edge of a storage kind (container 2, vault 3); 0 for any other kind.</summary>
    public static int SizeOf(BuildingKind? kind) => kind switch
    {
        BuildingKind.Container => ContainerSize,
        BuildingKind.Vault => VaultSize,
        _ => 0
    };

    public StorageBlock(long id, BuildingKind kind, GridPosition origin, int size, int capacity)
    {
        if (size < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "Misura del magazzino non valida.");
        }

        Id = id;
        Kind = kind;
        Origin = origin;
        Size = size;
        Capacity = Math.Max(0, capacity);
    }

    /// <summary>Stable entity id assigned by <see cref="Factory"/> at creation (saved).</summary>
    public long Id { get; }

    /// <summary><see cref="BuildingKind.Container"/> or <see cref="BuildingKind.Vault"/>.</summary>
    public BuildingKind Kind { get; }

    /// <summary>Top-left cell.</summary>
    public GridPosition Origin { get; }

    /// <summary>Edge of the square footprint.</summary>
    public int Size { get; }

    /// <summary>Pieces per item: its own space when standalone, the space it adds to the Core when linked.</summary>
    public int Capacity { get; }

    /// <summary>True when the block touches the Core (set by <see cref="CoreStorage.RecomputeLinks"/>).</summary>
    public bool IsLinked { get; internal set; }

    /// <summary>Structure id of the block (<c>container</c> or <c>vault</c>).</summary>
    public string StructureId => BuildSpec.StructureIdFor(Kind) ?? "";

    public bool Occupies(GridPosition tile) => BuildingIo.Occupies(Origin, Size, tile);

    public IEnumerable<GridPosition> OccupiedTiles()
    {
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                yield return new GridPosition(Origin.X + x, Origin.Y + y);
            }
        }
    }

    /// <summary>Pieces of <paramref name="itemId"/> held (always 0 when linked).</summary>
    public int Count(string itemId) => contents.TryGetValue(itemId, out var count) ? count : 0;

    /// <summary>Stores one <paramref name="itemId"/> when standalone and below <see cref="Capacity"/>; false otherwise (the caller keeps it).</summary>
    public bool TryAccept(string itemId)
    {
        if (IsLinked || string.IsNullOrWhiteSpace(itemId) || Count(itemId) >= Capacity)
        {
            return false;
        }

        contents[itemId] = Count(itemId) + 1;
        return true;
    }

    /// <summary>Takes one <paramref name="itemId"/> out; false when there is none.</summary>
    public bool TryTake(string itemId)
    {
        var have = Count(itemId);
        if (have <= 0)
        {
            return false;
        }

        if (have == 1)
        {
            contents.Remove(itemId);
        }
        else
        {
            contents[itemId] = have - 1;
        }

        return true;
    }

    /// <summary>Puts back a piece taken and not delivered, even at <see cref="Capacity"/> (nothing lost or duplicated).</summary>
    internal void Return(string itemId, int amount)
    {
        if (amount > 0 && !string.IsNullOrWhiteSpace(itemId))
        {
            contents[itemId] = Count(itemId) + amount;
        }
    }

    /// <summary>Contents by ordinal item id (items with none left out).</summary>
    public IReadOnlyList<(string ItemId, int Count)> ContentsInOrder()
    {
        var list = new List<(string ItemId, int Count)>(contents.Count);
        foreach (var (itemId, count) in contents)
        {
            list.Add((itemId, count));
        }

        return list;
    }

    /// <summary>Empties the block (it became linked, or a restore resets it).</summary>
    internal void Clear() => contents.Clear();
}
