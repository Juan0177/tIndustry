namespace TIndustry.Shared;

/// <summary>
/// 1×1 extractor, Mindustry's Unloader: no direction; <see cref="ItemsPerSecond"/> items a second (an integer
/// accumulator, +<see cref="ItemsPerSecond"/> a tick, one item every <see cref="Threshold"/>). It takes the filter's
/// item (without a filter, the next item by ordinal id among those available, after <see cref="LastItemId"/>) from a
/// touching block that has it — the Core (also through a linked storage block), a standalone storage block, a forno's
/// or an assembler's output — and puts it into the next touching block that takes it (a logistics cell or a building
/// that receives from logistics), never the block it took it from, and never from one store (the Core stock or a
/// storage block) into another (no bouncing between stores). Sources and destinations go in turn by side (North,
/// East, South, West). With no taker it takes nothing; it never takes from belts or from other extractors.
/// </summary>
public sealed class Extractor
{
    public const int Size = 1;
    public const string BuildingId = "extractor";

    /// <summary>Mindustry's Unloader speed: 11 items a second.</summary>
    public const int ItemsPerSecond = 11;

    /// <summary>Accumulator value worth one item (one second of ticks).</summary>
    public const int Threshold = SimulationClock.TickRate;

    public Extractor(GridPosition position, string? filterItemId = null, long id = 0)
    {
        Id = id;
        Position = position;
        FilterItemId = string.IsNullOrWhiteSpace(filterItemId) ? null : filterItemId;
    }

    /// <summary>Stable entity id assigned by <see cref="Factory"/> at creation (saved).</summary>
    public long Id { get; }

    public GridPosition Position { get; }

    /// <summary>The extractor has no direction: always North (a saved direction is ignored).</summary>
    public Direction Direction => Direction.North;

    /// <summary>The item it moves; null = any item, in turn by id.</summary>
    public string? FilterItemId { get; private set; }

    /// <summary>+<see cref="ItemsPerSecond"/> a tick; an item moves at <see cref="Threshold"/>, which it never passes while blocked.</summary>
    public int Accumulator { get; private set; }

    /// <summary>Index in <see cref="DirectionMath.All"/> of the first side tried as a source.</summary>
    public int SourceTurn { get; private set; }

    /// <summary>Index in <see cref="DirectionMath.All"/> of the first side tried as a destination.</summary>
    public int DestinationTurn { get; private set; }

    /// <summary>The last item moved (without a filter the next one is the first available after it by ordinal id).</summary>
    public string? LastItemId { get; private set; }

    public long ItemsExtracted { get; private set; }

    /// <summary>The sources on the four sides, refilled at each try (not state).</summary>
    private readonly ExtractorSource[] sourceBuffer = new ExtractorSource[DirectionMath.All.Length];

    public void SetFilterItem(string? itemId) =>
        FilterItemId = string.IsNullOrWhiteSpace(itemId) ? null : itemId;

    /// <summary>Restores the saved state; out-of-range values are brought back into range.</summary>
    public void RestoreState(int accumulator, int sourceTurn, int destinationTurn, string? lastItemId, long itemsExtracted = 0)
    {
        var sides = DirectionMath.All.Length;
        Accumulator = Math.Clamp(accumulator, 0, Threshold);
        SourceTurn = ((sourceTurn % sides) + sides) % sides;
        DestinationTurn = ((destinationTurn % sides) + sides) % sides;
        LastItemId = string.IsNullOrWhiteSpace(lastItemId) ? null : lastItemId;
        ItemsExtracted = Math.Max(0, itemsExtracted);
    }

    public IEnumerable<GridPosition> OccupiedTiles()
    {
        yield return Position;
    }

    public bool Occupies(GridPosition tile) => tile.Equals(Position);

    /// <summary>One tick: true when an item moved.</summary>
    public bool Tick(GameState state)
    {
        Accumulator += ItemsPerSecond;
        if (Accumulator < Threshold)
        {
            return false;
        }

        if (!TryMoveOne(state))
        {
            // Blocked: ready for the next tick, without banking time.
            Accumulator = Threshold;
            return false;
        }

        Accumulator -= Threshold;
        ItemsExtracted++;
        return true;
    }

    private bool TryMoveOne(GameState state)
    {
        var sides = DirectionMath.All;
        var sources = sourceBuffer;
        var anySource = false;
        for (var i = 0; i < sides.Length; i++)
        {
            sources[i] = state.ExtractorSourceAt(Position.Step(sides[i]));
            anySource |= sources[i].IsSome;
        }

        if (!anySource)
        {
            return false;
        }

        if (FilterItemId is { } filter)
        {
            return TryMoveItem(state, sources, filter);
        }

        // No filter: the item ids in ordinal order, starting after the last one moved.
        var ids = state.Content.ItemIds;
        var start = 0;
        if (LastItemId is { } last)
        {
            while (start < ids.Count && string.CompareOrdinal(ids[start], last) <= 0)
            {
                start++;
            }
        }

        for (var step = 0; step < ids.Count; step++)
        {
            if (TryMoveItem(state, sources, ids[(start + step) % ids.Count]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Sources in turn, and for each one that has <paramref name="itemId"/> the destinations in turn (never the same
    /// block): the destination takes the item first, then the source gives it up, so nothing is lost or duplicated.
    /// </summary>
    private bool TryMoveItem(GameState state, ReadOnlySpan<ExtractorSource> sources, string itemId)
    {
        var sides = DirectionMath.All;
        for (var s = 0; s < sides.Length; s++)
        {
            var si = (SourceTurn + s) % sides.Length;
            var source = sources[si];
            if (!source.IsSome || !source.Has(state, itemId))
            {
                continue;
            }

            for (var d = 0; d < sides.Length; d++)
            {
                var di = (DestinationTurn + d) % sides.Length;
                var target = Position.Step(sides[di]);
                if (state.LogisticsBlockAt(target) is not { } block || ReferenceEquals(block, source.Block)
                    || (source.IsStore && block is CoreStorage or StorageBlock)
                    || !Deliver(state, target, sides[di], itemId))
                {
                    continue;
                }

                // The destination only ever adds items, so the source still has the one it was asked for.
                if (!source.Take(state, itemId))
                {
                    throw new InvalidOperationException($"Extractor {Id}: the source lost {itemId} while delivering it.");
                }

                SourceTurn = (si + 1) % sides.Length;
                DestinationTurn = (di + 1) % sides.Length;
                LastItemId = itemId;
                return true;
            }
        }

        return false;
    }

    /// <summary>Hands one item to the logistics cell or the building on <paramref name="target"/>, travelling <paramref name="side"/>.</summary>
    private bool Deliver(GameState state, GridPosition target, Direction side, string itemId)
    {
        if (!state.Belts.TryGet(target, out _))
        {
            return state.ReceiveFromLogistics(target, itemId);
        }

        if (!state.Belts.TryInsert(target, new TransportedItem(state.NextItemId, itemId), side, Position))
        {
            return false;
        }

        state.NextItemId++;
        return true;
    }
}

/// <summary>
/// A block an extractor can take from: the Core stock (the Core or a linked storage block), a standalone storage
/// block, or a crafter's output. <see cref="Block"/> is the block's identity (<see cref="GameState.LogisticsBlockAt"/>).
/// </summary>
public readonly struct ExtractorSource
{
    private readonly StorageBlock? storage;
    private readonly Crafter? crafter;
    private readonly bool core;

    private ExtractorSource(object block, StorageBlock? storage, Crafter? crafter, bool core)
    {
        Block = block;
        this.storage = storage;
        this.crafter = crafter;
        this.core = core;
    }

    public object? Block { get; }

    public bool IsSome => Block is not null;

    /// <summary>The Core stock or a standalone storage block: it never hands items to another store.</summary>
    public bool IsStore => core || storage is not null;

    public static ExtractorSource Core(CoreStorage stock) => new(stock, null, null, true);

    public static ExtractorSource Of(StorageBlock storage) => new(storage, storage, null, false);

    public static ExtractorSource Of(Crafter crafter) => new(crafter, null, crafter, false);

    public bool Has(GameState state, string itemId) =>
        core ? state.Wallet.MaterialCount(itemId) > 0
        : storage is not null ? storage.Count(itemId) > 0
        : crafter is not null && crafter.HasOutput(itemId);

    public bool Take(GameState state, string itemId) =>
        core ? state.Storage.TryTake(itemId)
        : storage is not null ? storage.TryTake(itemId)
        : crafter is not null && crafter.TryTakeOutput(itemId);
}
