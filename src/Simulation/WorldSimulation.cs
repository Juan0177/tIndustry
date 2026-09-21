namespace TIndustry.Logistics;

public enum TerrainKind
{
    Grass,
    Soil,
    Stone,
    Water
}

public enum DepositKind
{
    None,
    Iron,
    Copper,
    Coal
}

public readonly record struct TerrainTile(TerrainKind Terrain, DepositKind Deposit)
{
    public bool IsBuildable => Terrain != TerrainKind.Water;
}

public sealed class TerrainMap
{
    private readonly TerrainTile[,] tiles;

    private TerrainMap(int width, int height)
    {
        Width = width;
        Height = height;
        tiles = new TerrainTile[width, height];
    }

    public int Width { get; }
    public int Height { get; }
    public TerrainTile this[GridPosition position] => tiles[position.X, position.Y];
    public TerrainTile this[int x, int y] => tiles[x, y];

    public static TerrainMap Generate(
        int width,
        int height,
        int seed,
        IReadOnlySet<GridPosition> coreTiles,
        GridPosition starterDepositOrigin)
    {
        var map = new TerrainMap(width, height);
        // Slightly rarer ore on huge maps keeps scout interesting without flooding every biome.
        var oreThreshold = width >= 256 ? 0.72f : 0.63f;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var elevation = SmoothNoise(x, y, seed);
                var terrain = elevation switch
                {
                    < 0.13f => TerrainKind.Water,
                    < 0.45f => TerrainKind.Grass,
                    < 0.78f => TerrainKind.Soil,
                    _ => TerrainKind.Stone
                };
                var oreNoise = SmoothNoise(x + 31, y - 17, seed * 3 + 11);
                var copperNoise = SmoothNoise(x - 19, y + 41, seed * 5 + 29);
                var coalNoise = SmoothNoise(x + 7, y + 53, seed * 7 + 17);
                var deposit = DepositKind.None;
                if (terrain != TerrainKind.Water)
                {
                    if (oreNoise > oreThreshold)
                    {
                        deposit = DepositKind.Iron;
                    }
                    else if (copperNoise > oreThreshold + 0.04f)
                    {
                        deposit = DepositKind.Copper;
                    }
                    else if (coalNoise > oreThreshold + 0.06f)
                    {
                        deposit = DepositKind.Coal;
                    }
                }

                map.tiles[x, y] = new TerrainTile(terrain, deposit);
            }
        }

        foreach (var position in coreTiles)
        {
            map.tiles[position.X, position.Y] = new TerrainTile(TerrainKind.Stone, DepositKind.None);
        }

        PlaceIronPatch(map, starterDepositOrigin, MinerBuilding.Size);

        // Copper starter patch sits south of the iron starter so both are near the core.
        var copperStarter = new GridPosition(starterDepositOrigin.X, starterDepositOrigin.Y + MinerBuilding.Size + 1);
        if (copperStarter.Y + MinerBuilding.Size <= height)
        {
            PlaceDepositPatch(map, copperStarter, MinerBuilding.Size, DepositKind.Copper);
        }

        // Coal starter east of iron — fuel for generators (Phase 6 mid-game).
        var coalStarter = new GridPosition(starterDepositOrigin.X + MinerBuilding.Size + 1, starterDepositOrigin.Y);
        if (coalStarter.X + MinerBuilding.Size <= width
            && !coreTiles.Contains(coalStarter))
        {
            PlaceDepositPatch(map, coalStarter, MinerBuilding.Size, DepositKind.Coal);
        }

        // Legacy / self-test patch at (2,2) when it does not collide with the core.
        var legacyOrigin = new GridPosition(2, 2);
        if (!coreTiles.Contains(legacyOrigin)
            && !coreTiles.Contains(new GridPosition(3, 3)))
        {
            PlaceIronPatch(map, legacyOrigin, MinerBuilding.Size);
        }

        // Extra scout patches on large maps, offset from the core so pan/zoom has a job.
        if (width >= 256)
        {
            PlaceIronPatch(map, new GridPosition(
                Math.Clamp(starterDepositOrigin.X - 48, 2, width - 4),
                Math.Clamp(starterDepositOrigin.Y - 20, 2, height - 4)), 3);
            PlaceIronPatch(map, new GridPosition(
                Math.Clamp(starterDepositOrigin.X + 36, 2, width - 4),
                Math.Clamp(starterDepositOrigin.Y + 40, 2, height - 4)), 3);
            PlaceDepositPatch(map, new GridPosition(
                Math.Clamp(starterDepositOrigin.X - 20, 2, width - 4),
                Math.Clamp(starterDepositOrigin.Y + 28, 2, height - 4)), 3, DepositKind.Copper);
            PlaceDepositPatch(map, new GridPosition(
                Math.Clamp(starterDepositOrigin.X + 24, 2, width - 4),
                Math.Clamp(starterDepositOrigin.Y - 18, 2, height - 4)), 3, DepositKind.Coal);
        }

        var partialDepositOrigin = new GridPosition(0, 0);
        for (var y = 0; y < MinerBuilding.Size; y++)
        {
            for (var x = 0; x < MinerBuilding.Size; x++)
            {
                var position = new GridPosition(partialDepositOrigin.X + x, partialDepositOrigin.Y + y);
                if (position.X >= width || position.Y >= height || coreTiles.Contains(position))
                {
                    continue;
                }

                map.tiles[position.X, position.Y] = new TerrainTile(
                    TerrainKind.Stone,
                    position == partialDepositOrigin ? DepositKind.Iron : DepositKind.None);
            }
        }

        return map;
    }

    private static void PlaceIronPatch(TerrainMap map, GridPosition origin, int size) =>
        PlaceDepositPatch(map, origin, size, DepositKind.Iron);

    private static void PlaceDepositPatch(TerrainMap map, GridPosition origin, int size, DepositKind deposit)
    {
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var position = new GridPosition(origin.X + x, origin.Y + y);
                if (position.X < 0 || position.Y < 0
                    || position.X >= map.Width || position.Y >= map.Height)
                {
                    continue;
                }

                map.tiles[position.X, position.Y] = new TerrainTile(TerrainKind.Stone, deposit);
            }
        }
    }

    private static float SmoothNoise(int x, int y, int seed)
    {
        var total = 0f;
        var weight = 0f;
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                var sampleWeight = offsetX == 0 && offsetY == 0 ? 4f : 1f;
                total += HashNoise(x + offsetX, y + offsetY, seed) * sampleWeight;
                weight += sampleWeight;
            }
        }

        return total / weight;
    }

    private static float HashNoise(int x, int y, int seed)
    {
        var value = unchecked((uint)(x * 374761393 + y * 668265263 + seed * 1442695041));
        value = (value ^ (value >> 13)) * 1274126177u;
        return (value ^ (value >> 16)) / (float)uint.MaxValue;
    }
}

/// <summary>
/// Mindustry-style building I/O: belts adjacent to a footprint are outputs when facing
/// away, inputs when their exit lands on the footprint. Also drives perimeter enumeration
/// for adjacent building→building transfer.
/// </summary>
public static class BuildingIo
{
    public static bool Occupies(GridPosition origin, int size, GridPosition tile) =>
        tile.X >= origin.X && tile.X < origin.X + size
        && tile.Y >= origin.Y && tile.Y < origin.Y + size;

    /// <summary>Travel direction from the footprint edge into an adjacent neighbor tile.</summary>
    public static bool TryTravelOut(GridPosition neighbor, GridPosition origin, int size, out Direction travel)
    {
        var edgeTile = new GridPosition(
            Math.Clamp(neighbor.X, origin.X, origin.X + size - 1),
            Math.Clamp(neighbor.Y, origin.Y, origin.Y + size - 1));
        return ConveyorGrid.TryDirectionBetween(edgeTile, neighbor, out travel);
    }

    /// <summary>Belt on the perimeter whose facing points away from the building = output.</summary>
    public static bool IsOutwardBelt(ConveyorCell belt, GridPosition origin, int size) =>
        TryTravelOut(belt.Position, origin, size, out var travel) && belt.Direction == travel;

    /// <summary>Belt whose routed exit lands on the footprint = input.</summary>
    public static bool IsInwardBelt(ConveyorCell belt, GridPosition origin, int size) =>
        Occupies(origin, size, belt.OutputPosition);

    /// <summary>
    /// Perimeter neighbor tiles (N/E/S/W × size) with the outward travel direction.
    /// </summary>
    public static IEnumerable<(GridPosition Position, Direction TravelOut)> PerimeterSlots(
        GridPosition origin,
        int size)
    {
        var count = size * 4;
        for (var index = 0; index < count; index++)
        {
            var edge = DirectionMath.All[index / size];
            var offset = index % size;
            var position = edge switch
            {
                Direction.North => new GridPosition(origin.X + offset, origin.Y - 1),
                Direction.East => new GridPosition(origin.X + size, origin.Y + offset),
                Direction.South => new GridPosition(origin.X + offset, origin.Y + size),
                Direction.West => new GridPosition(origin.X - 1, origin.Y + offset),
                _ => origin
            };
            yield return (position, edge);
        }
    }
}

public sealed class MinerBuilding
{
    public const int Size = 2;
    public const int FootprintArea = Size * Size;
    public const int OutputTileCount = Size * 4;
    public const string BasicId = "miner";
    public const string AdvancedId = "miner-advanced";

    public MinerBuilding(
        GridPosition position,
        Direction direction,
        int coveredDepositTiles,
        string outputItemId = "iron-ore",
        string definitionId = BasicId)
    {
        Position = position;
        Direction = direction;
        CoveredDepositTiles = coveredDepositTiles;
        OutputItemId = outputItemId;
        DefinitionId = string.IsNullOrWhiteSpace(definitionId) ? BasicId : definitionId;
    }

    public GridPosition Position { get; }
    public Direction Direction { get; }
    public int CoveredDepositTiles { get; }
    public string OutputItemId { get; }
    public string DefinitionId { get; }
    public bool IsAdvanced => DefinitionId == AdvancedId;

    /// <summary>Advanced miners run at 2× extraction rate.</summary>
    public float MiningSpeed => IsAdvanced ? 2f : 1f;

    /// <summary>
    /// Deposit coverage 0–100%. Advanced miners get +25% effective efficiency (capped),
    /// still zero when completely off-deposit.
    /// </summary>
    public float Efficiency
    {
        get
        {
            if (CoveredDepositTiles <= 0)
            {
                return 0f;
            }

            var raw = CoveredDepositTiles / (float)FootprintArea;
            return IsAdvanced ? Math.Min(1f, raw * 1.25f) : raw;
        }
    }

    public float Progress { get; internal set; }

    /// <summary>Round-robin cursor over the 8 adjacent perimeter tiles (N/E/S/W × 2).</summary>
    public int EjectIndex { get; internal set; }

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

    /// <summary>
    /// Every tile adjacent to the 2×2 footprint (N/E/S/W). Runtime eject only uses
    /// outward-facing belts and adjacent accepting buildings.
    /// </summary>
    public IEnumerable<GridPosition> OutputTiles()
    {
        for (var index = 0; index < OutputTileCount; index++)
        {
            yield return OutputTileAt(index);
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

    /// <summary>Travel direction from the miner footprint into an adjacent output tile.</summary>
    public static bool TryTravelInto(GridPosition output, GridPosition minerOrigin, out Direction travel) =>
        BuildingIo.TryTravelOut(output, minerOrigin, Size, out travel);
}

public sealed class SmelterBuilding
{
    public const int Size = 2;
    public const int MoneyCost = 40;
    public const int PlateCost = 6;

    /// <summary>Coal fuel item accepted into the forno stock buffer (belt insert), like the generator.</summary>
    public const string FuelItemId = "coal";
    public const int FuelBufferCapacity = 8;
    /// <summary>Seconds of coal-only craft time provided by one fuel unit.</summary>
    public const float SecondsPerFuel = 6f;
    /// <summary>
    /// Craft progress multiplier when the forno is powered (corrente).
    /// Coal-only is baseline 1.0; powered is ~20% faster even if coal is also buffered.
    /// </summary>
    public const float PoweredCraftSpeedMultiplier = 1.20f;

    private readonly Dictionary<string, int> inputBuffer = new(StringComparer.Ordinal);
    private readonly Queue<string> outputQueue = new();

    public SmelterBuilding(
        GridPosition position,
        Direction direction,
        RecipeDefinition recipe,
        int fuelBuffer = 0,
        float burnRemaining = 0f)
    {
        Position = position;
        Direction = direction;
        Recipe = recipe;
        FuelBuffer = Math.Clamp(fuelBuffer, 0, FuelBufferCapacity);
        BurnRemaining = Math.Max(0f, burnRemaining);
    }

    public GridPosition Position { get; }
    public Direction Direction { get; private set; }
    public RecipeDefinition Recipe { get; }
    public float Progress { get; internal set; }
    public bool IsCrafting { get; private set; }
    public int FuelBuffer { get; private set; }
    public float BurnRemaining { get; private set; }
    public bool IsBurningFuel => BurnRemaining > 0f;
    public IReadOnlyDictionary<string, int> InputBuffer => inputBuffer;
    public IReadOnlyCollection<string> OutputQueue => outputQueue;

    public void SetDirection(Direction direction) => Direction = direction;

    public bool TryAcceptFuel(string itemId)
    {
        if (itemId != FuelItemId || FuelBuffer >= FuelBufferCapacity)
        {
            return false;
        }

        FuelBuffer++;
        return true;
    }

    public void RestoreFuel(int fuelBuffer, float burnRemaining)
    {
        FuelBuffer = Math.Clamp(fuelBuffer, 0, FuelBufferCapacity);
        BurnRemaining = Math.Max(0f, burnRemaining);
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

    /// <summary>Legacy facing-opposite edge (preview only). Runtime I/O uses belt direction.</summary>
    public IEnumerable<GridPosition> InputTiles() => EdgeTiles(Opposite(Direction));

    /// <summary>Legacy facing edge (preview only). Runtime eject uses outward belts + adjacency.</summary>
    public IEnumerable<GridPosition> OutputTiles() => EdgeTiles(Direction);

    /// <summary>All perimeter neighbor tiles (belt-uscente / building-transfer candidates).</summary>
    public IEnumerable<GridPosition> PerimeterTiles() =>
        BuildingIo.PerimeterSlots(Position, Size).Select(slot => slot.Position);

    public int Buffered(string itemId) => inputBuffer.GetValueOrDefault(itemId);

    public bool TryAccept(string itemId)
    {
        var needed = Recipe.Inputs.FirstOrDefault(entry => entry.ItemId == itemId);
        if (needed is null)
        {
            return false;
        }

        var have = Buffered(itemId);
        if (have >= needed.Amount)
        {
            return false;
        }

        inputBuffer[itemId] = have + 1;
        return true;
    }

    public void RestoreState(
        float progress,
        bool isCrafting,
        IReadOnlyDictionary<string, int>? buffer,
        IEnumerable<string>? outputs,
        int fuelBuffer = 0,
        float burnRemaining = 0f)
    {
        Progress = Math.Clamp(progress, 0f, 1f);
        IsCrafting = isCrafting;
        inputBuffer.Clear();
        if (buffer is not null)
        {
            foreach (var pair in buffer)
            {
                inputBuffer[pair.Key] = pair.Value;
            }
        }

        outputQueue.Clear();
        if (outputs is not null)
        {
            foreach (var itemId in outputs)
            {
                outputQueue.Enqueue(itemId);
            }
        }

        RestoreFuel(fuelBuffer, burnRemaining);
    }

    internal void Update(
        float deltaSeconds,
        ConveyorGrid conveyors,
        ref long nextItemId,
        Func<float, bool>? trySpendPower = null,
        float powerDrawPerSecond = 0f,
        Func<string, bool>? tryDeliverAdjacent = null,
        bool allowCoalOrPower = false)
    {
        AcceptFromBelts(conveyors, allowCoalOrPower);
        TryStartCraft();
        if (IsCrafting)
        {
            var speed = 1f;
            var canAdvance = false;
            if (trySpendPower is not null
                && powerDrawPerSecond > 0f
                && trySpendPower(powerDrawPerSecond * deltaSeconds))
            {
                // Corrente: craft advances; powered bonus applies even if coal is buffered.
                canAdvance = true;
                speed = allowCoalOrPower ? PoweredCraftSpeedMultiplier : 1f;
            }
            else if (allowCoalOrPower && TickFuel(deltaSeconds))
            {
                // Carbone alone: baseline speed (no power bonus).
                canAdvance = true;
                speed = 1f;
            }

            if (canAdvance)
            {
                AdvanceCraft(deltaSeconds * speed);
            }
            // Else brown-out / no fuel: craft stalls without losing progress.
        }

        EmitOutputs(conveyors, ref nextItemId, tryDeliverAdjacent);
    }

    /// <summary>Burns coal fuel; true while the forno can craft on carbone this tick.</summary>
    internal bool TickFuel(float deltaSeconds)
    {
        if (BurnRemaining <= 0f)
        {
            if (FuelBuffer <= 0)
            {
                return false;
            }

            FuelBuffer--;
            BurnRemaining = SecondsPerFuel;
        }

        BurnRemaining = Math.Max(0f, BurnRemaining - deltaSeconds);
        return true;
    }

    private void AcceptFromBelts(ConveyorGrid conveyors, bool acceptCoalFuel)
    {
        // Only inspect neighbors of occupied tiles (O(footprint)) instead of every belt.
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var tileX = Position.X + x;
                var tileY = Position.Y + y;
                for (var d = 0; d < DirectionMath.All.Length; d++)
                {
                    var dir = DirectionMath.All[d];
                    var from = new GridPosition(tileX, tileY).Step(DirectionMath.Opposite(dir));
                    if (!conveyors.Cells.TryGetValue(from, out var conveyor))
                    {
                        continue;
                    }

                    var output = conveyor.OutputPosition;
                    if (output.X != tileX || output.Y != tileY)
                    {
                        continue;
                    }

                    while (conveyor.PeekOutput() is { } item)
                    {
                        var took = acceptCoalFuel && item.ItemId == FuelItemId
                            ? TryAcceptFuel(item.ItemId)
                            : TryAccept(item.ItemId);
                        if (!took)
                        {
                            break;
                        }

                        conveyor.RemoveOutput();
                    }
                }
            }
        }
    }

    private void TryStartCraft()
    {
        if (IsCrafting || outputQueue.Count > 0)
        {
            return;
        }

        if (!Recipe.Inputs.All(entry => Buffered(entry.ItemId) >= entry.Amount))
        {
            return;
        }

        foreach (var entry in Recipe.Inputs)
        {
            inputBuffer[entry.ItemId] = Buffered(entry.ItemId) - entry.Amount;
            if (inputBuffer[entry.ItemId] <= 0)
            {
                inputBuffer.Remove(entry.ItemId);
            }
        }

        IsCrafting = true;
        Progress = 0f;
    }

    private void AdvanceCraft(float deltaSeconds)
    {
        if (!IsCrafting)
        {
            return;
        }

        Progress = Math.Min(Progress + deltaSeconds / Recipe.DurationSeconds, 1f);
        if (Progress < 1f)
        {
            return;
        }

        foreach (var entry in Recipe.Outputs)
        {
            for (var count = 0; count < entry.Amount; count++)
            {
                outputQueue.Enqueue(entry.ItemId);
            }
        }

        IsCrafting = false;
        Progress = 0f;
    }

    private void EmitOutputs(
        ConveyorGrid conveyors,
        ref long nextItemId,
        Func<string, bool>? tryDeliverAdjacent)
    {
        while (outputQueue.Count > 0)
        {
            var itemId = outputQueue.Peek();
            if (tryDeliverAdjacent?.Invoke(itemId) == true)
            {
                outputQueue.Dequeue();
                continue;
            }

            var delivered = false;
            foreach (var (outputPosition, travel) in BuildingIo.PerimeterSlots(Position, Size))
            {
                if (!conveyors.Cells.TryGetValue(outputPosition, out var output)
                    || !BuildingIo.IsOutwardBelt(output, Position, Size))
                {
                    continue;
                }

                if (output.TryInsert(new TransportedItem(nextItemId, itemId), travel))
                {
                    nextItemId++;
                    outputQueue.Dequeue();
                    delivered = true;
                    break;
                }
            }

            if (!delivered)
            {
                return;
            }
        }
    }

    private IEnumerable<GridPosition> EdgeTiles(Direction edge)
    {
        for (var offset = 0; offset < Size; offset++)
        {
            yield return edge switch
            {
                Direction.North => new GridPosition(Position.X + offset, Position.Y - 1),
                Direction.East => new GridPosition(Position.X + Size, Position.Y + offset),
                Direction.South => new GridPosition(Position.X + offset, Position.Y + Size),
                Direction.West => new GridPosition(Position.X - 1, Position.Y + offset),
                _ => Position
            };
        }
    }

    private static Direction Opposite(Direction direction) => direction switch
    {
        Direction.North => Direction.South,
        Direction.East => Direction.West,
        Direction.South => Direction.North,
        Direction.West => Direction.East,
        _ => direction
    };
}

public sealed class GeneratorBuilding
{
    public const int Size = 2;
    public const float CapacityBonus = 40f;
    public const float GenerationPerSecond = 28f;
    public const string FuelItemId = "coal";
    public const int FuelBufferCapacity = 8;
    /// <summary>Seconds of generation provided by one fuel unit.</summary>
    public const float SecondsPerFuel = 8f;

    public GeneratorBuilding(GridPosition position, int fuelBuffer = 0, float burnRemaining = 0f)
    {
        Position = position;
        FuelBuffer = Math.Clamp(fuelBuffer, 0, FuelBufferCapacity);
        BurnRemaining = Math.Max(0f, burnRemaining);
    }

    public GridPosition Position { get; }
    public int FuelBuffer { get; private set; }
    public float BurnRemaining { get; private set; }
    public bool IsGenerating => BurnRemaining > 0f;

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

    public bool TryAcceptFuel(string itemId)
    {
        if (itemId != FuelItemId || FuelBuffer >= FuelBufferCapacity)
        {
            return false;
        }

        FuelBuffer++;
        return true;
    }

    public void RestoreFuel(int fuelBuffer, float burnRemaining)
    {
        FuelBuffer = Math.Clamp(fuelBuffer, 0, FuelBufferCapacity);
        BurnRemaining = Math.Max(0f, burnRemaining);
    }

    internal void AcceptFromBelts(ConveyorGrid conveyors)
    {
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var tileX = Position.X + x;
                var tileY = Position.Y + y;
                for (var d = 0; d < DirectionMath.All.Length; d++)
                {
                    var dir = DirectionMath.All[d];
                    var from = new GridPosition(tileX, tileY).Step(DirectionMath.Opposite(dir));
                    if (!conveyors.Cells.TryGetValue(from, out var conveyor))
                    {
                        continue;
                    }

                    var output = conveyor.OutputPosition;
                    if (output.X != tileX || output.Y != tileY)
                    {
                        continue;
                    }

                    while (conveyor.PeekOutput() is { } item && TryAcceptFuel(item.ItemId))
                    {
                        conveyor.RemoveOutput();
                    }
                }
            }
        }
    }

    /// <summary>Burns fuel and returns true while the generator should emit power this tick.</summary>
    internal bool TickGeneration(float deltaSeconds)
    {
        if (BurnRemaining <= 0f)
        {
            if (FuelBuffer <= 0)
            {
                return false;
            }

            FuelBuffer--;
            BurnRemaining = SecondsPerFuel;
        }

        BurnRemaining = Math.Max(0f, BurnRemaining - deltaSeconds);
        return true;
    }
}

public sealed class FactoryWorld
{
    public const int MinerMoneyCost = 25;
    public const int MinerPlateCost = 4;
    public const float MiningDurationSeconds = 2f;
    public const int IronOreSalePrice = 8;
    public const int IronPlateSalePrice = 30;
    public const int CoreSize = 4;
    public const float CorePowerCapacity = 24f;
    public const float CorePowerGeneration = 12f;
    public const float SmelterPowerDraw = 8f;
    public const float AssemblerPowerDraw = 10f;

    private static readonly ResourceAmount[] MinerBuildCost =
    [
        new ResourceAmount("iron-plate", MinerPlateCost)
    ];

    private static readonly ResourceAmount[] SmelterBuildCost =
    [
        new ResourceAmount("iron-plate", SmelterBuilding.PlateCost)
    ];

    private readonly Dictionary<GridPosition, MinerBuilding> miners = [];
    private readonly Dictionary<GridPosition, MinerBuilding> minerByTile = [];
    private readonly Dictionary<GridPosition, SmelterBuilding> smelters = [];
    private readonly Dictionary<GridPosition, SmelterBuilding> smelterByTile = [];
    private readonly Dictionary<GridPosition, SmelterBuilding> assemblers = [];
    private readonly Dictionary<GridPosition, SmelterBuilding> assemblerByTile = [];
    private readonly Dictionary<GridPosition, GeneratorBuilding> generators = [];
    private readonly Dictionary<GridPosition, GeneratorBuilding> generatorByTile = [];
    private readonly Dictionary<GridPosition, PowerNodeBuilding> powerNodes = [];
    private readonly Dictionary<GridPosition, PowerNodeBuilding> powerNodeByTile = [];
    private readonly List<PowerLink> powerLinks = [];
    private PowerNetworkState powerNetworks = PowerNetworkState.Empty;

    public FactoryWorld(int width, int height, int seed)
    {
        if (width < 12 || height < 8)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Mappa troppo piccola per core e giacimenti.");
        }

        Seed = seed;
        // Core always sits at the geometric center of the map (4×4 footprint).
        var coreLeft = Math.Max(0, (width - CoreSize) / 2);
        var coreTop = Math.Max(0, (height - CoreSize) / 2);
        var coreTiles = new HashSet<GridPosition>();
        for (var y = 0; y < CoreSize; y++)
        {
            for (var x = 0; x < CoreSize; x++)
            {
                coreTiles.Add(new GridPosition(coreLeft + x, coreTop + y));
            }
        }

        CoreOrigin = new GridPosition(coreLeft, coreTop);
        // Starter iron sits immediately west of the core so small self-test maps and 1000² stay playable.
        StarterDepositOrigin = new GridPosition(Math.Max(0, coreLeft - 4), coreTop);
        CoreTiles = coreTiles;
        Terrain = TerrainMap.Generate(width, height, seed, CoreTiles, StarterDepositOrigin);
    }

    public int Seed { get; }
    public GridPosition CoreOrigin { get; }
    public GridPosition StarterDepositOrigin { get; }
    public TerrainMap Terrain { get; }
    public IReadOnlyDictionary<GridPosition, MinerBuilding> Miners => miners;
    public IReadOnlyDictionary<GridPosition, SmelterBuilding> Smelters => smelters;
    public IReadOnlyDictionary<GridPosition, SmelterBuilding> Assemblers => assemblers;
    public IReadOnlyDictionary<GridPosition, GeneratorBuilding> Generators => generators;
    public IReadOnlyDictionary<GridPosition, PowerNodeBuilding> PowerNodes => powerNodes;
    public IReadOnlyList<PowerLink> PowerLinks => powerLinks;
    public PowerNetworkState PowerNetworks => powerNetworks;
    public IReadOnlySet<GridPosition> CoreTiles { get; }
    public int SoldItems { get; private set; }
    public int SaleRevenue { get; private set; }
    /// <summary>Items that reached the core (stocked or auto-sold).</summary>
    public int CoreDeliveredItems { get; private set; }
    public int CoreUpgradeLevel { get; private set; }
    public int CoreSaleBonusPercent { get; private set; }
    public float PowerBuffer { get; private set; }
    public float PowerCapacity { get; private set; } = CorePowerCapacity;

    public static int SalePrice(string itemId) => MarketCatalog.Default.GetSellPrice(itemId);

    public int EffectiveSalePrice(string itemId, MarketCatalog market)
    {
        var price = market.GetSellPrice(itemId);
        if (CoreUpgradeLevel <= 0 || CoreSaleBonusPercent <= 0)
        {
            return price;
        }

        return price + price * CoreSaleBonusPercent / 100;
    }

    public void SetSoldItems(int soldItems, int saleRevenue = -1)
    {
        SoldItems = soldItems;
        SaleRevenue = saleRevenue >= 0 ? saleRevenue : soldItems * IronOreSalePrice;
    }

    public void SetCoreDeliveredItems(int delivered) =>
        CoreDeliveredItems = Math.Max(0, delivered);

    public void SetCoreUpgrade(int level, int saleBonusPercent)
    {
        CoreUpgradeLevel = Math.Max(0, level);
        CoreSaleBonusPercent = Math.Max(0, saleBonusPercent);
    }

    public void SetPowerBuffer(float buffer) =>
        PowerBuffer = Math.Clamp(buffer, 0f, Math.Max(PowerCapacity, CorePowerCapacity));

    public void RecalculatePowerCapacity()
    {
        // Capacity still scales with placed generators; local networks gate who can spend.
        PowerCapacity = CorePowerCapacity + generators.Count * GeneratorBuilding.CapacityBonus;
        PowerBuffer = Math.Min(PowerBuffer, PowerCapacity);
    }

    public bool IsBuildingPowered(GridPosition origin, int size) =>
        PowerNetworking.IsBuildingPowered(this, powerNetworks, origin, size);

    public bool TrySpendPower(float amount)
    {
        if (amount <= 0f)
        {
            return true;
        }

        if (PowerBuffer < amount)
        {
            return false;
        }

        PowerBuffer -= amount;
        return true;
    }

    /// <summary>Spend only if the building is on a live gen network or adjacency-powered cluster.</summary>
    public bool TrySpendPowerForBuilding(GridPosition origin, int size, float amount)
    {
        if (amount <= 0f)
        {
            return true;
        }

        if (!IsBuildingPowered(origin, size))
        {
            return false;
        }

        return TrySpendPower(amount);
    }

    public bool TryUpgradeCore(
        EconomyWallet wallet,
        CoreUpgradeDefinition upgrade,
        EconomySession? session = null)
    {
        if (CoreUpgradeLevel > 0
            || !wallet.TrySpend(upgrade.MoneyCost, upgrade.BuildCost))
        {
            return false;
        }

        CoreUpgradeLevel = 1;
        CoreSaleBonusPercent = upgrade.SaleBonusPercent;
        session?.RecordUpgradeSpend(upgrade.MoneyCost);
        return true;
    }

    public bool TryRestoreMiner(
        GridPosition position,
        Direction direction,
        float progress,
        string? outputItemId = null,
        string? definitionId = null)
    {
        if (!IsInside(position)
            || Footprint(position, MinerBuilding.Size).Any(tile =>
                !IsInside(tile)
                || minerByTile.ContainsKey(tile)
                || smelterByTile.ContainsKey(tile)
                || assemblerByTile.ContainsKey(tile)
                || generatorByTile.ContainsKey(tile)
                || powerNodeByTile.ContainsKey(tile)
                || CoreTiles.Contains(tile)))
        {
            return false;
        }

        var miner = new MinerBuilding(
            position,
            direction,
            CountCoveredDepositTiles(position),
            outputItemId ?? ResolveMinerOutput(position),
            definitionId ?? MinerBuilding.BasicId)
        {
            Progress = Math.Clamp(progress, 0f, 1f)
        };
        miners.Add(position, miner);
        foreach (var tile in miner.OccupiedTiles())
        {
            minerByTile.Add(tile, miner);
        }

        return true;
    }

    public bool TryRestoreSmelter(
        GridPosition position,
        Direction direction,
        RecipeDefinition recipe,
        float progress,
        bool isCrafting,
        IReadOnlyDictionary<string, int>? buffer,
        IEnumerable<string>? outputs,
        int fuelBuffer = 0,
        float burnRemaining = 0f)
    {
        if (!CanOccupyBuilding(position, SmelterBuilding.Size, null))
        {
            return false;
        }

        var smelter = new SmelterBuilding(position, direction, recipe);
        smelter.RestoreState(progress, isCrafting, buffer, outputs, fuelBuffer, burnRemaining);
        RegisterSmelter(smelter);
        return true;
    }

    public bool CanPlaceConveyor(GridPosition position) =>
        IsInside(position)
        && Terrain[position].IsBuildable
        && !CoreTiles.Contains(position)
        && !minerByTile.ContainsKey(position)
        && !smelterByTile.ContainsKey(position)
        && !assemblerByTile.ContainsKey(position)
        && !generatorByTile.ContainsKey(position)
        && !powerNodeByTile.ContainsKey(position);

    public bool CanPlacePowerNode(GridPosition position, int size, ConveyorGrid conveyors) =>
        CanOccupyBuilding(position, size, conveyors);

    public bool TryPlacePowerNode(
        GridPosition position,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        string definitionId = PowerNodeBuilding.Tier1Id,
        BuildingDefinition? cost = null,
        EconomySession? session = null,
        bool autoLink = true)
    {
        definitionId = definitionId == PowerNodeBuilding.Tier2Id
            ? PowerNodeBuilding.Tier2Id
            : PowerNodeBuilding.Tier1Id;
        var size = PowerNodeBuilding.SizeFor(definitionId);
        cost ??= definitionId == PowerNodeBuilding.Tier2Id
            ? new BuildingDefinition(
                PowerNodeBuilding.Tier2Id, 55,
                [new ResourceAmount("iron-plate", 6), new ResourceAmount("copper-wire", 4)], 100,
                Footprint: PowerNodeBuilding.Tier2Size,
                MaxPowerLinks: PowerNodeBuilding.Tier2MaxLinks,
                PowerLinkRange: PowerNodeBuilding.Tier2Range)
            : new BuildingDefinition(
                PowerNodeBuilding.Tier1Id, 20, [new ResourceAmount("copper-wire", 2)], 100,
                Footprint: PowerNodeBuilding.Tier1Size,
                MaxPowerLinks: PowerNodeBuilding.Tier1MaxLinks,
                PowerLinkRange: PowerNodeBuilding.Tier1Range);

        if (!CanPlacePowerNode(position, size, conveyors)
            || !wallet.TrySpend(cost.MoneyCost, cost.BuildCost))
        {
            return false;
        }

        var node = new PowerNodeBuilding(position, definitionId);
        RegisterPowerNode(node);
        session?.RecordBuildSpend(cost.MoneyCost);
        if (autoLink)
        {
            PowerNetworking.AutoLinkNode(this, node, powerLinks);
        }

        RefreshPowerNetworks();
        return true;
    }

    public bool TryRemovePowerNode(
        GridPosition position,
        EconomyWallet wallet,
        BuildingDefinition? cost = null,
        EconomySession? session = null)
    {
        if (!powerNodeByTile.TryGetValue(position, out var node))
        {
            return false;
        }

        cost ??= node.DefinitionId == PowerNodeBuilding.Tier2Id
            ? new BuildingDefinition(
                PowerNodeBuilding.Tier2Id, 55,
                [new ResourceAmount("iron-plate", 6), new ResourceAmount("copper-wire", 4)], 100,
                Footprint: PowerNodeBuilding.Tier2Size,
                MaxPowerLinks: PowerNodeBuilding.Tier2MaxLinks,
                PowerLinkRange: PowerNodeBuilding.Tier2Range)
            : new BuildingDefinition(
                PowerNodeBuilding.Tier1Id, 20, [new ResourceAmount("copper-wire", 2)], 100,
                Footprint: PowerNodeBuilding.Tier1Size,
                MaxPowerLinks: PowerNodeBuilding.Tier1MaxLinks,
                PowerLinkRange: PowerNodeBuilding.Tier1Range);

        var nodeId = new PowerEndpointId(PowerEndpointKind.Node, node.Position);
        PowerNetworking.RemoveEndpointLinks(nodeId, powerLinks);
        powerNodes.Remove(node.Position);
        foreach (var tile in node.OccupiedTiles())
        {
            powerNodeByTile.Remove(tile);
        }

        ApplyRefund(wallet, cost, session);
        RefreshPowerNetworks();
        return true;
    }

    public bool TryRestorePowerNode(GridPosition position, string definitionId = PowerNodeBuilding.Tier1Id)
    {
        definitionId = definitionId == PowerNodeBuilding.Tier2Id
            ? PowerNodeBuilding.Tier2Id
            : PowerNodeBuilding.Tier1Id;
        var size = PowerNodeBuilding.SizeFor(definitionId);
        if (!CanOccupyBuilding(position, size, null))
        {
            return false;
        }

        RegisterPowerNode(new PowerNodeBuilding(position, definitionId));
        return true;
    }

    public bool TryRestorePowerLink(PowerEndpointId a, PowerEndpointId b)
    {
        var link = PowerLink.Create(a, b);
        if (powerLinks.Contains(link))
        {
            return true;
        }

        powerLinks.Add(link);
        return true;
    }

    public void RefreshPowerNetworks() =>
        powerNetworks = PowerNetworking.Build(this, powerNodes, powerLinks, generators.Values);

    /// <summary>
    /// Ensures a consumer is powered: prefer adjacency to a fueled generator, else place T1
    /// node(s) that auto-link toward the nearest generator (never CORE).
    /// Used by self-tests and capture scenes.
    /// </summary>
    public bool TryEnsurePowerLinkToGenerator(
        GridPosition origin,
        int size,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        BuildingDefinition? cost = null,
        EconomySession? session = null)
    {
        RefreshPowerNetworks();
        if (IsBuildingPowered(origin, size))
        {
            return true;
        }

        cost ??= new BuildingDefinition(
            PowerNodeBuilding.Tier1Id, 20, [new ResourceAmount("copper-wire", 2)], 100,
            Footprint: PowerNodeBuilding.Tier1Size,
            MaxPowerLinks: PowerNodeBuilding.Tier1MaxLinks,
            PowerLinkRange: PowerNodeBuilding.Tier1Range);

        // Prefer a free tile adjacent to the building, scored toward nearest generator.
        var candidates = new List<(GridPosition Pos, float Score)>();
        foreach (var tile in Footprint(origin, size))
        {
            for (var d = 0; d < DirectionMath.All.Length; d++)
            {
                var n = tile.Step(DirectionMath.All[d]);
                if (!CanPlacePowerNode(n, PowerNodeBuilding.Tier1Size, conveyors))
                {
                    continue;
                }

                var toGen = NearestGeneratorDistance(n, PowerNodeBuilding.Tier1Size);
                var toBuilding = PowerNetworking.DistanceCenters(n, PowerNodeBuilding.Tier1Size, origin, size);
                candidates.Add((n, toGen + toBuilding * 0.1f));
            }
        }

        // If nothing adjacent, search a small ring around the building.
        if (candidates.Count == 0)
        {
            for (var dy = -3; dy <= size + 2; dy++)
            {
                for (var dx = -3; dx <= size + 2; dx++)
                {
                    var n = new GridPosition(origin.X + dx, origin.Y + dy);
                    if (!CanPlacePowerNode(n, PowerNodeBuilding.Tier1Size, conveyors))
                    {
                        continue;
                    }

                    var toBuilding = PowerNetworking.DistanceCenters(n, PowerNodeBuilding.Tier1Size, origin, size);
                    if (toBuilding > PowerNodeBuilding.Tier1Range)
                    {
                        continue;
                    }

                    var toGen = NearestGeneratorDistance(n, PowerNodeBuilding.Tier1Size);
                    candidates.Add((n, toGen + toBuilding * 0.1f));
                }
            }
        }

        foreach (var (pos, _) in candidates.OrderBy(c => c.Score))
        {
            if (!TryPlacePowerNode(pos, conveyors, wallet, PowerNodeBuilding.Tier1Id, cost, session))
            {
                continue;
            }

            if (IsBuildingPowered(origin, size))
            {
                return true;
            }

            // Bridge toward nearest generator with a second node if needed.
            if (TryPlaceBridgeNodeTowardGenerator(pos, conveyors, wallet, cost, session)
                && IsBuildingPowered(origin, size))
            {
                return true;
            }
        }

        RefreshPowerNetworks();
        return IsBuildingPowered(origin, size);
    }

    /// <summary>Legacy alias — CORE is not on the power graph; routes to generator ensure.</summary>
    public bool TryEnsurePowerLinkToCore(
        GridPosition origin,
        int size,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        BuildingDefinition? cost = null,
        EconomySession? session = null) =>
        TryEnsurePowerLinkToGenerator(origin, size, conveyors, wallet, cost, session);

    private float NearestGeneratorDistance(GridPosition nodeOrigin, int nodeSize)
    {
        var best = float.MaxValue;
        foreach (var gen in generators.Values)
        {
            var dist = PowerNetworking.DistanceCenters(
                nodeOrigin, nodeSize, gen.Position, GeneratorBuilding.Size);
            if (dist < best)
            {
                best = dist;
            }
        }

        return best;
    }

    private bool TryPlaceBridgeNodeTowardGenerator(
        GridPosition fromNode,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        BuildingDefinition cost,
        EconomySession? session)
    {
        if (generators.Count == 0)
        {
            return false;
        }

        GridPosition? bestPos = null;
        var best = float.MaxValue;
        foreach (var gen in generators.Values)
        {
            foreach (var genTile in gen.OccupiedTiles())
            {
                for (var d = 0; d < DirectionMath.All.Length; d++)
                {
                    var n = genTile.Step(DirectionMath.All[d]);
                    if (!CanPlacePowerNode(n, PowerNodeBuilding.Tier1Size, conveyors))
                    {
                        continue;
                    }

                    var dist = PowerNetworking.DistanceCenters(
                        fromNode, PowerNodeBuilding.Tier1Size, n, PowerNodeBuilding.Tier1Size);
                    if (dist < best && dist <= PowerNodeBuilding.Tier1Range)
                    {
                        best = dist;
                        bestPos = n;
                    }
                }
            }
        }

        return bestPos is { } pos
            && TryPlacePowerNode(pos, conveyors, wallet, PowerNodeBuilding.Tier1Id, cost, session);
    }

    public bool TryGetPowerNodeAt(GridPosition position, out PowerNodeBuilding node) =>
        powerNodeByTile.TryGetValue(position, out node!);

    private void RegisterPowerNode(PowerNodeBuilding node)
    {
        powerNodes.Add(node.Position, node);
        foreach (var tile in node.OccupiedTiles())
        {
            powerNodeByTile.Add(tile, node);
        }
    }

    public bool CanPlaceMiner(GridPosition position, ConveyorGrid conveyors) =>
        Footprint(position, MinerBuilding.Size).All(tile =>
            IsInside(tile)
            && Terrain[tile].IsBuildable
            && !CoreTiles.Contains(tile)
            && !minerByTile.ContainsKey(tile)
            && !smelterByTile.ContainsKey(tile)
            && !assemblerByTile.ContainsKey(tile)
            && !generatorByTile.ContainsKey(tile)
            && !conveyors.Cells.ContainsKey(tile)
            && !powerNodeByTile.ContainsKey(tile));
    // Deposit coverage optional: 0 covered tiles → 0% efficiency, no ore output.

    public bool CanPlaceSmelter(GridPosition position, ConveyorGrid conveyors) =>
        CanOccupyBuilding(position, SmelterBuilding.Size, conveyors);

    public bool CanPlaceAssembler(GridPosition position, ConveyorGrid conveyors) =>
        CanOccupyBuilding(position, SmelterBuilding.Size, conveyors);

    public int CountCoveredDepositTiles(GridPosition position) =>
        Footprint(position, MinerBuilding.Size).Count(tile =>
            IsInside(tile)
            && Terrain[tile].Deposit is DepositKind.Iron or DepositKind.Copper or DepositKind.Coal);

    public string ResolveMinerOutput(GridPosition position)
    {
        var iron = 0;
        var copper = 0;
        var coal = 0;
        foreach (var tile in Footprint(position, MinerBuilding.Size))
        {
            if (!IsInside(tile))
            {
                continue;
            }

            switch (Terrain[tile].Deposit)
            {
                case DepositKind.Iron:
                    iron++;
                    break;
                case DepositKind.Copper:
                    copper++;
                    break;
                case DepositKind.Coal:
                    coal++;
                    break;
            }
        }

        if (coal >= iron && coal >= copper && coal > 0)
        {
            return "coal";
        }

        return copper > iron ? "copper-ore" : "iron-ore";
    }

    public bool TryPlaceMiner(
        GridPosition position,
        Direction direction,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        BuildingDefinition? cost = null,
        EconomySession? session = null,
        string definitionId = MinerBuilding.BasicId)
    {
        cost ??= definitionId == MinerBuilding.AdvancedId
            ? new BuildingDefinition("miner-advanced", 70,
                [new ResourceAmount("iron-plate", 10), new ResourceAmount("copper-wire", 4)], 100)
            : new BuildingDefinition("miner", MinerMoneyCost, MinerBuildCost, 100);
        if (!CanPlaceMiner(position, conveyors)
            || !wallet.TrySpend(cost.MoneyCost, cost.BuildCost))
        {
            return false;
        }

        var miner = new MinerBuilding(
            position,
            direction,
            CountCoveredDepositTiles(position),
            ResolveMinerOutput(position),
            definitionId);
        miners.Add(position, miner);
        foreach (var tile in miner.OccupiedTiles())
        {
            minerByTile.Add(tile, miner);
        }

        session?.RecordBuildSpend(cost.MoneyCost);
        return true;
    }

    public bool TryPlaceSmelter(
        GridPosition position,
        Direction direction,
        RecipeDefinition recipe,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        BuildingDefinition? cost = null,
        EconomySession? session = null)
    {
        cost ??= new BuildingDefinition("smelter", SmelterBuilding.MoneyCost, SmelterBuildCost, 100);
        if (!CanPlaceSmelter(position, conveyors)
            || !wallet.TrySpend(cost.MoneyCost, cost.BuildCost))
        {
            return false;
        }

        RegisterSmelter(new SmelterBuilding(position, direction, recipe));
        session?.RecordBuildSpend(cost.MoneyCost);
        PowerNetworking.AutoLinkEndpoint(
            this,
            new PowerEndpointId(PowerEndpointKind.Consumer, position),
            position,
            SmelterBuilding.Size,
            powerLinks);
        RefreshPowerNetworks();
        return true;
    }

    public bool TryPlaceAssembler(
        GridPosition position,
        Direction direction,
        RecipeDefinition recipe,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        BuildingDefinition? cost = null,
        EconomySession? session = null)
    {
        cost ??= new BuildingDefinition("assembler", 60,
            [new ResourceAmount("iron-plate", 8), new ResourceAmount("copper-wire", 2)], 100);
        if (!CanPlaceAssembler(position, conveyors)
            || !wallet.TrySpend(cost.MoneyCost, cost.BuildCost))
        {
            return false;
        }

        RegisterAssembler(new SmelterBuilding(position, direction, recipe));
        session?.RecordBuildSpend(cost.MoneyCost);
        PowerNetworking.AutoLinkEndpoint(
            this,
            new PowerEndpointId(PowerEndpointKind.Consumer, position),
            position,
            SmelterBuilding.Size,
            powerLinks);
        RefreshPowerNetworks();
        return true;
    }

    public bool TryRemoveMiner(
        GridPosition position,
        EconomyWallet wallet,
        BuildingDefinition? cost = null,
        EconomySession? session = null)
    {
        if (!minerByTile.TryGetValue(position, out var miner))
        {
            return false;
        }

        cost ??= miner.IsAdvanced
            ? new BuildingDefinition("miner-advanced", 70,
                [new ResourceAmount("iron-plate", 10), new ResourceAmount("copper-wire", 4)], 100)
            : new BuildingDefinition("miner", MinerMoneyCost, MinerBuildCost, 100);
        miners.Remove(miner.Position);
        foreach (var tile in miner.OccupiedTiles())
        {
            minerByTile.Remove(tile);
        }

        ApplyRefund(wallet, cost, session);
        return true;
    }

    public bool TryRemoveSmelter(
        GridPosition position,
        EconomyWallet wallet,
        BuildingDefinition? cost = null,
        EconomySession? session = null)
    {
        if (!smelterByTile.TryGetValue(position, out var smelter))
        {
            return false;
        }

        cost ??= new BuildingDefinition("smelter", SmelterBuilding.MoneyCost, SmelterBuildCost, 100);
        PowerNetworking.RemoveEndpointLinks(
            new PowerEndpointId(PowerEndpointKind.Consumer, smelter.Position), powerLinks);
        smelters.Remove(smelter.Position);
        foreach (var tile in smelter.OccupiedTiles())
        {
            smelterByTile.Remove(tile);
        }

        ApplyRefund(wallet, cost, session);
        RefreshPowerNetworks();
        return true;
    }

    public bool TryRemoveAssembler(
        GridPosition position,
        EconomyWallet wallet,
        BuildingDefinition? cost = null,
        EconomySession? session = null)
    {
        if (!assemblerByTile.TryGetValue(position, out var assembler))
        {
            return false;
        }

        cost ??= new BuildingDefinition("assembler", 60,
            [new ResourceAmount("iron-plate", 8), new ResourceAmount("copper-wire", 2)], 100);
        PowerNetworking.RemoveEndpointLinks(
            new PowerEndpointId(PowerEndpointKind.Consumer, assembler.Position), powerLinks);
        assemblers.Remove(assembler.Position);
        foreach (var tile in assembler.OccupiedTiles())
        {
            assemblerByTile.Remove(tile);
        }

        ApplyRefund(wallet, cost, session);
        RefreshPowerNetworks();
        return true;
    }

    public bool CanPlaceGenerator(GridPosition position, ConveyorGrid conveyors) =>
        CanOccupyBuilding(position, GeneratorBuilding.Size, conveyors);

    public bool TryPlaceGenerator(
        GridPosition position,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        BuildingDefinition? cost = null,
        EconomySession? session = null)
    {
        cost ??= new BuildingDefinition("generator", 55, [new ResourceAmount("iron-plate", 8)], 100);
        if (!CanPlaceGenerator(position, conveyors)
            || !wallet.TrySpend(cost.MoneyCost, cost.BuildCost))
        {
            return false;
        }

        RegisterGenerator(new GeneratorBuilding(position));
        session?.RecordBuildSpend(cost.MoneyCost);
        PowerNetworking.AutoLinkEndpoint(
            this,
            new PowerEndpointId(PowerEndpointKind.Generator, position),
            position,
            GeneratorBuilding.Size,
            powerLinks);
        RefreshPowerNetworks();
        return true;
    }

    public bool TryRemoveGenerator(
        GridPosition position,
        EconomyWallet wallet,
        BuildingDefinition? cost = null,
        EconomySession? session = null)
    {
        if (!generatorByTile.TryGetValue(position, out var generator))
        {
            return false;
        }

        cost ??= new BuildingDefinition("generator", 55, [new ResourceAmount("iron-plate", 8)], 100);
        PowerNetworking.RemoveEndpointLinks(
            new PowerEndpointId(PowerEndpointKind.Generator, generator.Position), powerLinks);
        generators.Remove(generator.Position);
        foreach (var tile in generator.OccupiedTiles())
        {
            generatorByTile.Remove(tile);
        }

        RecalculatePowerCapacity();
        ApplyRefund(wallet, cost, session);
        RefreshPowerNetworks();
        return true;
    }

    public bool TryRestoreGenerator(GridPosition position, int fuelBuffer = 0, float burnRemaining = 0f)
    {
        if (!CanOccupyBuilding(position, GeneratorBuilding.Size, null))
        {
            return false;
        }

        RegisterGenerator(new GeneratorBuilding(position, fuelBuffer, burnRemaining));
        return true;
    }

    public void Update(
        float deltaSeconds,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        ref long nextItemId,
        MarketCatalog? market = null,
        EconomySession? session = null,
        bool autoSellAtCore = false)
    {
        market ??= MarketCatalog.Default;
        var generation = CorePowerGeneration;
        foreach (var generator in generators.Values)
        {
            generator.AcceptFromBelts(conveyors);
            if (generator.TickGeneration(deltaSeconds))
            {
                generation += GeneratorBuilding.GenerationPerSecond;
            }
        }

        // Rebuild local node networks after generator burn state updates.
        powerNetworks = PowerNetworking.Build(this, powerNodes, powerLinks, generators.Values);
        PowerCapacity = powerNetworks.Capacity;
        PowerBuffer = Math.Min(PowerCapacity, PowerBuffer + generation * deltaSeconds);

        foreach (var miner in miners.Values)
        {
            miner.Progress = Math.Min(
                miner.Progress + deltaSeconds * miner.Efficiency * miner.MiningSpeed / MiningDurationSeconds,
                1f);
            if (miner.Progress < 1f)
            {
                continue;
            }

            var produced = false;
            // Prefer Mindustry-style building→building when footprints touch.
            if (TryDeliverAdjacent(
                    miner.Position,
                    MinerBuilding.Size,
                    miner.OutputItemId,
                    wallet,
                    market,
                    session,
                    autoSellAtCore))
            {
                produced = true;
            }
            else
            {
                var start = miner.EjectIndex;
                for (var step = 0; step < MinerBuilding.OutputTileCount; step++)
                {
                    var slot = (start + step) % MinerBuilding.OutputTileCount;
                    var outputPosition = miner.OutputTileAt(slot);
                    if (!conveyors.Cells.TryGetValue(outputPosition, out var output)
                        || !BuildingIo.IsOutwardBelt(output, miner.Position, MinerBuilding.Size))
                    {
                        continue;
                    }

                    Direction? travel = MinerBuilding.TryTravelInto(outputPosition, miner.Position, out var into)
                        ? into
                        : null;
                    if (output.TryInsert(new TransportedItem(nextItemId, miner.OutputItemId), travel))
                    {
                        nextItemId++;
                        produced = true;
                        // Next eject starts on the following neighbor so two belts share ore.
                        miner.EjectIndex = (slot + 1) % MinerBuilding.OutputTileCount;
                        break;
                    }
                }
            }

            if (produced)
            {
                miner.Progress = 0f;
            }
        }

        conveyors.Update(deltaSeconds);

        foreach (var smelter in smelters.Values)
        {
            smelter.Update(
                deltaSeconds,
                conveyors,
                ref nextItemId,
                amount => TrySpendPowerForBuilding(smelter.Position, SmelterBuilding.Size, amount),
                SmelterPowerDraw,
                itemId => TryDeliverAdjacent(
                    smelter.Position,
                    SmelterBuilding.Size,
                    itemId,
                    wallet,
                    market,
                    session,
                    autoSellAtCore,
                    smelter),
                allowCoalOrPower: true);
        }

        foreach (var assembler in assemblers.Values)
        {
            assembler.Update(
                deltaSeconds,
                conveyors,
                ref nextItemId,
                amount => TrySpendPowerForBuilding(assembler.Position, SmelterBuilding.Size, amount),
                AssemblerPowerDraw,
                itemId => TryDeliverAdjacent(
                    assembler.Position,
                    SmelterBuilding.Size,
                    itemId,
                    wallet,
                    market,
                    session,
                    autoSellAtCore,
                    assembler));
        }

        foreach (var conveyor in conveyors.Cells.Values)
        {
            while (CoreTiles.Contains(conveyor.OutputPosition) && conveyor.PeekOutput() is { } item)
            {
                conveyor.RemoveOutput();
                CoreDeliveredItems++;
                if (autoSellAtCore)
                {
                    ApplyCoreSale(wallet, item.ItemId, 1, market, session);
                }
                else
                {
                    // Stock-first: materials accumulate for builds/unlocks; sell via Mercato.
                    wallet.AddMaterial(item.ItemId, 1);
                }
            }
        }
    }

    /// <summary>Sell stocked materials from the wallet at the current core market price.</summary>
    public bool TrySellFromWallet(
        EconomyWallet wallet,
        string itemId,
        int amount,
        MarketCatalog? market = null,
        EconomySession? session = null)
    {
        market ??= MarketCatalog.Default;
        if (amount <= 0 || !wallet.TryRemoveMaterial(itemId, amount))
        {
            return false;
        }

        ApplyCoreSale(wallet, itemId, amount, market, session);
        return true;
    }

    private void ApplyCoreSale(
        EconomyWallet wallet,
        string itemId,
        int amount,
        MarketCatalog market,
        EconomySession? session)
    {
        var unitPrice = EffectiveSalePrice(itemId, market);
        var total = unitPrice * amount;
        wallet.AddMoney(total);
        SaleRevenue += total;
        SoldItems += amount;
        for (var i = 0; i < amount; i++)
        {
            session?.RecordSale(itemId, unitPrice);
        }
    }

    /// <summary>
    /// Direct building→building (or building→core) transfer when footprints touch.
    /// Prefer over belts so compact Mindustry layouts work without a belt between.
    /// </summary>
    private bool TryDeliverAdjacent(
        GridPosition origin,
        int size,
        string itemId,
        EconomyWallet wallet,
        MarketCatalog market,
        EconomySession? session,
        bool autoSellAtCore,
        SmelterBuilding? excludeCrafter = null)
    {
        foreach (var (neighbor, _) in BuildingIo.PerimeterSlots(origin, size))
        {
            if (TryGetSmelterAt(neighbor, out var smelter)
                && !ReferenceEquals(smelter, excludeCrafter)
                && smelter.TryAccept(itemId))
            {
                return true;
            }

            if (TryGetAssemblerAt(neighbor, out var assembler)
                && !ReferenceEquals(assembler, excludeCrafter)
                && assembler.TryAccept(itemId))
            {
                return true;
            }

            if (CoreTiles.Contains(neighbor))
            {
                CoreDeliveredItems++;
                if (autoSellAtCore)
                {
                    ApplyCoreSale(wallet, itemId, 1, market, session);
                }
                else
                {
                    wallet.AddMaterial(itemId, 1);
                }

                return true;
            }
        }

        return false;
    }

    private static void ApplyRefund(EconomyWallet wallet, BuildingDefinition cost, EconomySession? session)
    {
        var refundMoney = cost.MoneyCost * Math.Clamp(cost.RefundPercent, 0, 100) / 100;
        wallet.AddMoney(refundMoney);
        foreach (var entry in cost.BuildCost)
        {
            var amount = entry.Amount * Math.Clamp(cost.RefundPercent, 0, 100) / 100;
            if (amount > 0)
            {
                wallet.AddMaterial(entry.ItemId, amount);
            }
        }

        session?.RecordRefund(refundMoney);
    }

    public bool IsMinerTile(GridPosition position) => minerByTile.ContainsKey(position);

    public bool IsSmelterTile(GridPosition position) => smelterByTile.ContainsKey(position);

    public bool IsAssemblerTile(GridPosition position) => assemblerByTile.ContainsKey(position);

    public bool IsGeneratorTile(GridPosition position) => generatorByTile.ContainsKey(position);

    public bool TryGetMinerAt(GridPosition position, out MinerBuilding miner) =>
        minerByTile.TryGetValue(position, out miner!);

    public bool TryGetSmelterAt(GridPosition position, out SmelterBuilding smelter) =>
        smelterByTile.TryGetValue(position, out smelter!);

    public bool TryGetAssemblerAt(GridPosition position, out SmelterBuilding assembler) =>
        assemblerByTile.TryGetValue(position, out assembler!);

    public bool TryGetGeneratorAt(GridPosition position, out GeneratorBuilding generator) =>
        generatorByTile.TryGetValue(position, out generator!);

    public bool TryRestoreAssembler(
        GridPosition position,
        Direction direction,
        RecipeDefinition recipe,
        float progress,
        bool isCrafting,
        IReadOnlyDictionary<string, int>? buffer,
        IEnumerable<string>? outputs)
    {
        if (!CanOccupyBuilding(position, SmelterBuilding.Size, null))
        {
            return false;
        }

        var assembler = new SmelterBuilding(position, direction, recipe);
        assembler.RestoreState(progress, isCrafting, buffer, outputs);
        RegisterAssembler(assembler);
        return true;
    }

    private void RegisterSmelter(SmelterBuilding smelter)
    {
        smelters.Add(smelter.Position, smelter);
        foreach (var tile in smelter.OccupiedTiles())
        {
            smelterByTile.Add(tile, smelter);
        }
    }

    private void RegisterAssembler(SmelterBuilding assembler)
    {
        assemblers.Add(assembler.Position, assembler);
        foreach (var tile in assembler.OccupiedTiles())
        {
            assemblerByTile.Add(tile, assembler);
        }
    }

    private void RegisterGenerator(GeneratorBuilding generator)
    {
        generators.Add(generator.Position, generator);
        foreach (var tile in generator.OccupiedTiles())
        {
            generatorByTile.Add(tile, generator);
        }

        RecalculatePowerCapacity();
    }

    private bool CanOccupyBuilding(GridPosition position, int size, ConveyorGrid? conveyors) =>
        Footprint(position, size).All(tile =>
            IsInside(tile)
            && Terrain[tile].IsBuildable
            && !CoreTiles.Contains(tile)
            && !minerByTile.ContainsKey(tile)
            && !smelterByTile.ContainsKey(tile)
            && !assemblerByTile.ContainsKey(tile)
            && !generatorByTile.ContainsKey(tile)
            && !powerNodeByTile.ContainsKey(tile)
            && (conveyors is null || !conveyors.Cells.ContainsKey(tile)));

    private bool IsInside(GridPosition position) =>
        position.X >= 0 && position.X < Terrain.Width
        && position.Y >= 0 && position.Y < Terrain.Height;

    private static IEnumerable<GridPosition> Footprint(GridPosition origin, int size)
    {
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                yield return new GridPosition(origin.X + x, origin.Y + y);
            }
        }
    }
}
