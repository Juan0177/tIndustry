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
    Copper
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

public sealed class MinerBuilding
{
    public const int Size = 2;
    public const int FootprintArea = Size * Size;

    public MinerBuilding(
        GridPosition position,
        Direction direction,
        int coveredDepositTiles,
        string outputItemId = "iron-ore")
    {
        Position = position;
        Direction = direction;
        CoveredDepositTiles = coveredDepositTiles;
        OutputItemId = outputItemId;
    }

    public GridPosition Position { get; }
    public Direction Direction { get; }
    public int CoveredDepositTiles { get; }
    public string OutputItemId { get; }
    public float Efficiency => CoveredDepositTiles / (float)FootprintArea;
    public float Progress { get; internal set; }

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

    public IEnumerable<GridPosition> OutputTiles()
    {
        for (var offset = 0; offset < Size; offset++)
        {
            yield return Direction switch
            {
                Direction.North => new GridPosition(Position.X + offset, Position.Y - 1),
                Direction.East => new GridPosition(Position.X + Size, Position.Y + offset),
                Direction.South => new GridPosition(Position.X + offset, Position.Y + Size),
                Direction.West => new GridPosition(Position.X - 1, Position.Y + offset),
                _ => Position
            };
        }
    }
}

public sealed class SmelterBuilding
{
    public const int Size = 2;
    public const int MoneyCost = 40;
    public const int PlateCost = 6;

    private readonly Dictionary<string, int> inputBuffer = new(StringComparer.Ordinal);
    private readonly Queue<string> outputQueue = new();

    public SmelterBuilding(GridPosition position, Direction direction, RecipeDefinition recipe)
    {
        Position = position;
        Direction = direction;
        Recipe = recipe;
    }

    public GridPosition Position { get; }
    public Direction Direction { get; private set; }
    public RecipeDefinition Recipe { get; }
    public float Progress { get; internal set; }
    public bool IsCrafting { get; private set; }
    public IReadOnlyDictionary<string, int> InputBuffer => inputBuffer;
    public IReadOnlyCollection<string> OutputQueue => outputQueue;

    public void SetDirection(Direction direction) => Direction = direction;

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

    public IEnumerable<GridPosition> InputTiles() => EdgeTiles(Opposite(Direction));

    public IEnumerable<GridPosition> OutputTiles() => EdgeTiles(Direction);

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
        IEnumerable<string>? outputs)
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
    }

    internal void Update(
        float deltaSeconds,
        ConveyorGrid conveyors,
        ref long nextItemId,
        Func<float, bool>? trySpendPower = null,
        float powerDrawPerSecond = 0f)
    {
        AcceptFromBelts(conveyors);
        TryStartCraft();
        if (IsCrafting
            && trySpendPower is not null
            && powerDrawPerSecond > 0f
            && !trySpendPower(powerDrawPerSecond * deltaSeconds))
        {
            // Brownout: craft stalls without losing progress.
        }
        else
        {
            AdvanceCraft(deltaSeconds);
        }

        EmitOutputs(conveyors, ref nextItemId);
    }

    private void AcceptFromBelts(ConveyorGrid conveyors)
    {
        foreach (var conveyor in conveyors.Cells.Values)
        {
            if (!OccupiedTiles().Contains(conveyor.OutputPosition))
            {
                continue;
            }

            while (conveyor.PeekOutput() is { } item && TryAccept(item.ItemId))
            {
                conveyor.RemoveOutput();
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

    private void EmitOutputs(ConveyorGrid conveyors, ref long nextItemId)
    {
        while (outputQueue.Count > 0)
        {
            var itemId = outputQueue.Peek();
            var delivered = false;
            foreach (var outputPosition in OutputTiles())
            {
                if (conveyors.Cells.TryGetValue(outputPosition, out var output)
                    && output.TryInsert(new TransportedItem(nextItemId, itemId)))
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

    public GeneratorBuilding(GridPosition position)
    {
        Position = position;
    }

    public GridPosition Position { get; }

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

    public FactoryWorld(int width, int height, int seed)
    {
        if (width < 12 || height < 8)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Mappa troppo piccola per core e giacimenti.");
        }

        Seed = seed;
        var coreLeft = width - 6;
        var coreTop = Math.Max(0, height / 2 - 2);
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
        StarterDepositOrigin = new GridPosition(coreLeft - 4, coreTop);
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
    public IReadOnlySet<GridPosition> CoreTiles { get; }
    public int SoldItems { get; private set; }
    public int SaleRevenue { get; private set; }
    public int CoreUpgradeLevel { get; private set; }
    public int CoreSaleBonusPercent { get; private set; }
    public float PowerBuffer { get; private set; }
    public float PowerCapacity { get; private set; } = CorePowerCapacity;

    public static int SalePrice(string itemId) => MarketCatalog.CreateDefault().GetSellPrice(itemId);

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

    public void SetCoreUpgrade(int level, int saleBonusPercent)
    {
        CoreUpgradeLevel = Math.Max(0, level);
        CoreSaleBonusPercent = Math.Max(0, saleBonusPercent);
    }

    public void SetPowerBuffer(float buffer) =>
        PowerBuffer = Math.Clamp(buffer, 0f, Math.Max(PowerCapacity, CorePowerCapacity));

    public void RecalculatePowerCapacity()
    {
        PowerCapacity = CorePowerCapacity + generators.Count * GeneratorBuilding.CapacityBonus;
        PowerBuffer = Math.Min(PowerBuffer, PowerCapacity);
    }

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
        string? outputItemId = null)
    {
        if (!IsInside(position)
            || Footprint(position, MinerBuilding.Size).Any(tile =>
                !IsInside(tile)
                || minerByTile.ContainsKey(tile)
                || smelterByTile.ContainsKey(tile)
                || assemblerByTile.ContainsKey(tile)
                || generatorByTile.ContainsKey(tile)
                || CoreTiles.Contains(tile)))
        {
            return false;
        }

        var miner = new MinerBuilding(
            position,
            direction,
            CountCoveredDepositTiles(position),
            outputItemId ?? ResolveMinerOutput(position))
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
        IEnumerable<string>? outputs)
    {
        if (!CanOccupyBuilding(position, SmelterBuilding.Size, null))
        {
            return false;
        }

        var smelter = new SmelterBuilding(position, direction, recipe);
        smelter.RestoreState(progress, isCrafting, buffer, outputs);
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
        && !generatorByTile.ContainsKey(position);

    public bool CanPlaceMiner(GridPosition position, ConveyorGrid conveyors) =>
        Footprint(position, MinerBuilding.Size).All(tile =>
            IsInside(tile)
            && Terrain[tile].IsBuildable
            && !CoreTiles.Contains(tile)
            && !minerByTile.ContainsKey(tile)
            && !smelterByTile.ContainsKey(tile)
            && !assemblerByTile.ContainsKey(tile)
            && !generatorByTile.ContainsKey(tile)
            && !conveyors.Cells.ContainsKey(tile))
        && CountCoveredDepositTiles(position) > 0;

    public bool CanPlaceSmelter(GridPosition position, ConveyorGrid conveyors) =>
        CanOccupyBuilding(position, SmelterBuilding.Size, conveyors);

    public bool CanPlaceAssembler(GridPosition position, ConveyorGrid conveyors) =>
        CanOccupyBuilding(position, SmelterBuilding.Size, conveyors);

    public int CountCoveredDepositTiles(GridPosition position) =>
        Footprint(position, MinerBuilding.Size).Count(tile =>
            IsInside(tile)
            && Terrain[tile].Deposit is DepositKind.Iron or DepositKind.Copper);

    public string ResolveMinerOutput(GridPosition position)
    {
        var iron = 0;
        var copper = 0;
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
            }
        }

        return copper > iron ? "copper-ore" : "iron-ore";
    }

    public bool TryPlaceMiner(
        GridPosition position,
        Direction direction,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        BuildingDefinition? cost = null,
        EconomySession? session = null)
    {
        cost ??= new BuildingDefinition("miner", MinerMoneyCost, MinerBuildCost, 100);
        if (!CanPlaceMiner(position, conveyors)
            || !wallet.TrySpend(cost.MoneyCost, cost.BuildCost))
        {
            return false;
        }

        var miner = new MinerBuilding(
            position,
            direction,
            CountCoveredDepositTiles(position),
            ResolveMinerOutput(position));
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

        cost ??= new BuildingDefinition("miner", MinerMoneyCost, MinerBuildCost, 100);
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
        smelters.Remove(smelter.Position);
        foreach (var tile in smelter.OccupiedTiles())
        {
            smelterByTile.Remove(tile);
        }

        ApplyRefund(wallet, cost, session);
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
        assemblers.Remove(assembler.Position);
        foreach (var tile in assembler.OccupiedTiles())
        {
            assemblerByTile.Remove(tile);
        }

        ApplyRefund(wallet, cost, session);
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
        generators.Remove(generator.Position);
        foreach (var tile in generator.OccupiedTiles())
        {
            generatorByTile.Remove(tile);
        }

        RecalculatePowerCapacity();
        ApplyRefund(wallet, cost, session);
        return true;
    }

    public bool TryRestoreGenerator(GridPosition position)
    {
        if (!CanOccupyBuilding(position, GeneratorBuilding.Size, null))
        {
            return false;
        }

        RegisterGenerator(new GeneratorBuilding(position));
        return true;
    }

    public void Update(
        float deltaSeconds,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        ref long nextItemId,
        MarketCatalog? market = null,
        EconomySession? session = null)
    {
        market ??= MarketCatalog.CreateDefault();
        RecalculatePowerCapacity();
        var generation = CorePowerGeneration + generators.Count * GeneratorBuilding.GenerationPerSecond;
        PowerBuffer = Math.Min(PowerCapacity, PowerBuffer + generation * deltaSeconds);

        foreach (var miner in miners.Values)
        {
            miner.Progress = Math.Min(
                miner.Progress + deltaSeconds * miner.Efficiency / MiningDurationSeconds,
                1f);
            if (miner.Progress < 1f)
            {
                continue;
            }

            var produced = false;
            foreach (var outputPosition in miner.OutputTiles())
            {
                if (conveyors.Cells.TryGetValue(outputPosition, out var output)
                    && output.TryInsert(new TransportedItem(nextItemId, miner.OutputItemId)))
                {
                    nextItemId++;
                    produced = true;
                    break;
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
            smelter.Update(deltaSeconds, conveyors, ref nextItemId, TrySpendPower, SmelterPowerDraw);
        }

        foreach (var assembler in assemblers.Values)
        {
            assembler.Update(deltaSeconds, conveyors, ref nextItemId, TrySpendPower, AssemblerPowerDraw);
        }

        foreach (var conveyor in conveyors.Cells.Values)
        {
            while (CoreTiles.Contains(conveyor.OutputPosition) && conveyor.PeekOutput() is { } item)
            {
                conveyor.RemoveOutput();
                var price = EffectiveSalePrice(item.ItemId, market);
                wallet.AddMoney(price);
                SaleRevenue += price;
                SoldItems++;
                session?.RecordSale(item.ItemId, price);
            }
        }
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
