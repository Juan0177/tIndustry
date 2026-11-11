namespace TIndustry.Shared;

/// <summary>
/// 2×2 generator that burns fuel from belts; while burning it gives its network
/// <see cref="BuildingDefinition.PowerOutput"/> (spec «Rete di corrente», balance in <see cref="PowerSystem"/>). Its
/// definition (<see cref="DefinitionId"/>: <see cref="BuildingId"/> or <see cref="CombustionId"/>) gives what it burns
/// (<see cref="BuildingDefinition.Fuels"/>, each piece for its own seconds) and its output. It holds at most
/// <see cref="FuelBufferCapacity"/> pieces of all its fuels together and burns the fuel listed first first.
/// </summary>
public sealed class Generator
{
    public const int Size = 2;
    public const string BuildingId = "generator";
    public const string CombustionId = "combustion-generator";
    public const string FuelItemId = "coal";
    public const int FuelBufferCapacity = 8;
    public const float SecondsPerFuel = 8f;

    /// <summary>The spec's output in units per second, used when the content (built in code) does not give one.</summary>
    public const int DefaultPowerOutput = 30;

    /// <summary>The spec's output of the combustion generator, used when the content (built in code) does not give one.</summary>
    public const int DefaultCombustionOutput = 90;

    /// <summary>True for the ids of the generators that burn fuel.</summary>
    public static bool IsGeneratorId(string? id) => id is BuildingId or CombustionId;

    /// <summary>
    /// Output of a burning generator <paramref name="definitionId"/> in thousandths of a unit per second (content, or the
    /// spec's when the content built in code has none).
    /// </summary>
    public static long OutputRateOf(FactoryContent content, string definitionId = BuildingId) =>
        content.FindBuilding(definitionId)?.PowerOutputRate is > 0 and var rate
            ? rate
            : definitionId switch
            {
                BuildingId => DefaultPowerOutput * 1000L,
                CombustionId => DefaultCombustionOutput * 1000L,
                _ => 0
            };

    /// <summary>
    /// What a generator <paramref name="definitionId"/> burns: the content's fuels, or the spec's when the content built
    /// in code has none (the generator 1 coal / 8 s; the combustion generator 1 coal / 2 s or 1 graphite / 4 s).
    /// </summary>
    public static IReadOnlyList<FuelDefinition> FuelsOf(FactoryContent? content, string definitionId = BuildingId) =>
        content?.FindBuilding(definitionId)?.Fuels is { Count: > 0 } fuels ? fuels : DefaultFuels(definitionId);

    private static readonly FuelDefinition[] GeneratorFuels = [new(FuelItemId, SecondsPerFuel)];
    private static readonly FuelDefinition[] CombustionFuels = [new(FuelItemId, 2f), new("graphite", 4f)];

    private static IReadOnlyList<FuelDefinition> DefaultFuels(string definitionId) =>
        definitionId == CombustionId ? CombustionFuels : GeneratorFuels;

    public Generator(
        GridPosition position,
        Direction direction = Direction.East,
        long id = 0,
        string definitionId = BuildingId,
        IReadOnlyList<FuelDefinition>? fuels = null)
    {
        Id = id;
        Position = position;
        Direction = direction;
        DefinitionId = definitionId;
        Fuels = fuels is { Count: > 0 } ? fuels : DefaultFuels(definitionId);
        fuelCounts = new int[Fuels.Count];
    }

    /// <summary>Stable entity id assigned by <see cref="Factory"/> at creation (saved).</summary>
    public long Id { get; }

    /// <summary><see cref="BuildingId"/> or <see cref="CombustionId"/> (saved).</summary>
    public string DefinitionId { get; }

    /// <summary>What it burns, in content order: the first fuel with stock burns first.</summary>
    public IReadOnlyList<FuelDefinition> Fuels { get; }

    // Pieces held of each fuel of Fuels (same order); their sum is at most FuelBufferCapacity.
    private readonly int[] fuelCounts;

    public GridPosition Position { get; private set; }
    public Direction Direction { get; private set; }

    /// <summary>Pieces held, of all its fuels together (at most <see cref="FuelBufferCapacity"/>).</summary>
    public int FuelBuffer
    {
        get
        {
            var total = 0;
            foreach (var count in fuelCounts)
            {
                total += count;
            }

            return total;
        }
    }

    public float BurnRemaining { get; private set; }
    public bool IsGenerating => BurnRemaining > 0f;
    public long FuelConsumed { get; private set; }

    /// <summary>Pieces of <paramref name="itemId"/> held (0 for an item it does not burn).</summary>
    public int FuelCount(string itemId)
    {
        var index = FuelIndex(itemId);
        return index < 0 ? 0 : fuelCounts[index];
    }

    private int FuelIndex(string itemId)
    {
        for (var i = 0; i < Fuels.Count; i++)
        {
            if (string.Equals(Fuels[i].ItemId, itemId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    public void Relocate(GridPosition position, Direction direction)
    {
        Position = position;
        Direction = direction;
    }

    /// <summary>Restores <paramref name="fuelBuffer"/> pieces of its first fuel (none of the others) and the burn.</summary>
    public void RestoreFuel(int fuelBuffer, float burnRemaining, long fuelConsumed = 0)
    {
        Array.Clear(fuelCounts);
        fuelCounts[0] = Math.Clamp(fuelBuffer, 0, FuelBufferCapacity);
        BurnRemaining = Math.Max(0f, burnRemaining);
        FuelConsumed = Math.Max(0, fuelConsumed);
    }

    /// <summary>
    /// Restores the pieces of each fuel (<paramref name="counts"/>: item id and pieces; an item it does not burn is
    /// dropped, and the total stops at <see cref="FuelBufferCapacity"/> in content order) and the burn.
    /// </summary>
    public void RestoreFuels(IEnumerable<(string ItemId, int Count)> counts, float burnRemaining, long fuelConsumed = 0)
    {
        Array.Clear(fuelCounts);
        foreach (var (itemId, count) in counts)
        {
            if (FuelIndex(itemId) is var index and >= 0)
            {
                fuelCounts[index] = Math.Max(0, count);
            }
        }

        var room = FuelBufferCapacity;
        for (var i = 0; i < fuelCounts.Length; i++)
        {
            fuelCounts[i] = Math.Min(fuelCounts[i], room);
            room -= fuelCounts[i];
        }

        BurnRemaining = Math.Max(0f, burnRemaining);
        FuelConsumed = Math.Max(0, fuelConsumed);
    }

    /// <summary>Pieces held of each fuel with stock, in content order (for the save).</summary>
    public IEnumerable<(string ItemId, int Count)> FuelCounts()
    {
        for (var i = 0; i < Fuels.Count; i++)
        {
            if (fuelCounts[i] > 0)
            {
                yield return (Fuels[i].ItemId, fuelCounts[i]);
            }
        }
    }

    /// <summary>True when every piece held is of its first fuel (the save then needs no list per fuel).</summary>
    public bool HoldsOnlyFirstFuel => FuelBuffer == fuelCounts[0];

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

    /// <summary>Whether <see cref="TryAcceptFuel"/> would take one <paramref name="itemId"/> (changes nothing).</summary>
    public bool CanAcceptFuel(string itemId) => FuelIndex(itemId) >= 0 && FuelBuffer < FuelBufferCapacity;

    public bool TryAcceptFuel(string itemId)
    {
        var index = FuelIndex(itemId);
        if (index < 0 || FuelBuffer >= FuelBufferCapacity)
        {
            return false;
        }

        fuelCounts[index]++;
        return true;
    }

    public int AcceptFromBelts(BeltGrid belts)
    {
        var accepted = 0;
        foreach (var cell in belts.Cells.Values)
        {
            // A belt hands over at its output tile, a junction at the far side of each queue.
            for (var index = 0; index < cell.OutputTargetCount; index++)
            {
                var target = cell.OutputTarget(index);
                if (!Occupies(target))
                {
                    continue;
                }

                while (cell.PeekOutputToward(target) is { } item)
                {
                    if (!TryAcceptFuel(item.ItemId))
                    {
                        break;
                    }

                    cell.RemoveOutputToward(target);
                    accepted++;
                }
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
            // The first fuel (content order) with stock burns, for its own time.
            var index = Array.FindIndex(fuelCounts, count => count > 0);
            if (index < 0)
            {
                return false;
            }

            fuelCounts[index]--;
            FuelConsumed++;
            BurnRemaining = Fuels[index].Seconds;
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
