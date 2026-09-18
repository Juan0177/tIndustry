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
    Iron
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

    public static TerrainMap Generate(int width, int height, int seed, IReadOnlySet<GridPosition> coreTiles)
    {
        var map = new TerrainMap(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var position = new GridPosition(x, y);
                var elevation = SmoothNoise(x, y, seed);
                var terrain = elevation switch
                {
                    < 0.13f => TerrainKind.Water,
                    < 0.45f => TerrainKind.Grass,
                    < 0.78f => TerrainKind.Soil,
                    _ => TerrainKind.Stone
                };
                var oreNoise = SmoothNoise(x + 31, y - 17, seed * 3 + 11);
                var deposit = terrain != TerrainKind.Water && oreNoise > 0.63f
                    ? DepositKind.Iron
                    : DepositKind.None;
                map.tiles[x, y] = new TerrainTile(terrain, deposit);
            }
        }

        foreach (var position in coreTiles)
        {
            map.tiles[position.X, position.Y] = new TerrainTile(TerrainKind.Stone, DepositKind.None);
        }

        var guaranteedDeposit = new[]
        {
            new GridPosition(2, 2),
            new GridPosition(3, 2),
            new GridPosition(2, 3),
            new GridPosition(3, 3)
        };
        foreach (var position in guaranteedDeposit)
        {
            map.tiles[position.X, position.Y] = new TerrainTile(TerrainKind.Stone, DepositKind.Iron);
        }

        var partialDepositOrigin = new GridPosition(0, 0);
        for (var y = 0; y < MinerBuilding.Size; y++)
        {
            for (var x = 0; x < MinerBuilding.Size; x++)
            {
                var position = new GridPosition(partialDepositOrigin.X + x, partialDepositOrigin.Y + y);
                map.tiles[position.X, position.Y] = new TerrainTile(
                    TerrainKind.Stone,
                    position == partialDepositOrigin ? DepositKind.Iron : DepositKind.None);
            }
        }

        return map;
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

    public MinerBuilding(GridPosition position, int coveredDepositTiles)
    {
        Position = position;
        CoveredDepositTiles = coveredDepositTiles;
    }

    public GridPosition Position { get; }
    public int CoveredDepositTiles { get; }
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
            yield return new GridPosition(Position.X + offset, Position.Y - 1);
            yield return new GridPosition(Position.X + offset, Position.Y + Size);
            yield return new GridPosition(Position.X - 1, Position.Y + offset);
            yield return new GridPosition(Position.X + Size, Position.Y + offset);
        }
    }
}

public sealed class FactoryWorld
{
    public const int MinerMoneyCost = 25;
    public const int MinerPlateCost = 4;
    public const float MiningDurationSeconds = 2f;
    public const int IronOreSalePrice = 8;

    private static readonly ResourceAmount[] MinerBuildCost =
    [
        new ResourceAmount("iron-plate", MinerPlateCost)
    ];

    private readonly Dictionary<GridPosition, MinerBuilding> miners = [];
    private readonly Dictionary<GridPosition, MinerBuilding> minerByTile = [];

    public FactoryWorld(int width, int height, int seed)
    {
        var coreLeft = width - 6;
        var coreTop = height / 2 - 2;
        var coreTiles = new HashSet<GridPosition>();
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
            {
                coreTiles.Add(new GridPosition(coreLeft + x, coreTop + y));
            }
        }
        CoreTiles = coreTiles;
        Terrain = TerrainMap.Generate(width, height, seed, CoreTiles);
    }

    public TerrainMap Terrain { get; }
    public IReadOnlyDictionary<GridPosition, MinerBuilding> Miners => miners;
    public IReadOnlySet<GridPosition> CoreTiles { get; }
    public int SoldItems { get; private set; }

    public bool CanPlaceConveyor(GridPosition position) =>
        Terrain[position].IsBuildable && !CoreTiles.Contains(position) && !minerByTile.ContainsKey(position);

    public bool CanPlaceMiner(GridPosition position, ConveyorGrid conveyors) =>
        Footprint(position).All(tile =>
            IsInside(tile)
            && Terrain[tile].IsBuildable
            && !CoreTiles.Contains(tile)
            && !minerByTile.ContainsKey(tile)
            && !conveyors.Cells.ContainsKey(tile))
        && CountCoveredDepositTiles(position) > 0;

    public int CountCoveredDepositTiles(GridPosition position) =>
        Footprint(position).Count(tile =>
            IsInside(tile) && Terrain[tile].Deposit == DepositKind.Iron);

    public bool TryPlaceMiner(
        GridPosition position,
        Direction direction,
        ConveyorGrid conveyors,
        EconomyWallet wallet)
    {
        if (!CanPlaceMiner(position, conveyors)
            || !wallet.TrySpend(MinerMoneyCost, MinerBuildCost))
        {
            return false;
        }

        var miner = new MinerBuilding(position, CountCoveredDepositTiles(position));
        miners.Add(position, miner);
        foreach (var tile in miner.OccupiedTiles())
        {
            minerByTile.Add(tile, miner);
        }
        return true;
    }

    public bool TryRemoveMiner(GridPosition position, EconomyWallet wallet)
    {
        if (!minerByTile.TryGetValue(position, out var miner))
        {
            return false;
        }

        miners.Remove(miner.Position);
        foreach (var tile in miner.OccupiedTiles())
        {
            minerByTile.Remove(tile);
        }

        wallet.AddMoney(MinerMoneyCost);
        wallet.AddMaterial("iron-plate", MinerPlateCost);
        return true;
    }

    public void Update(float deltaSeconds, ConveyorGrid conveyors, EconomyWallet wallet, ref long nextItemId)
    {
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
                    && output.Items.Count < output.Definition.Capacity
                    && output.TryInsert(new TransportedItem(nextItemId, "iron-ore")))
                {
                    nextItemId++;
                    produced = true;
                }
            }

            if (produced)
            {
                miner.Progress = 0f;
            }
        }

        conveyors.Update(deltaSeconds);
        foreach (var conveyor in conveyors.Cells.Values)
        {
            while (CoreTiles.Contains(conveyor.OutputPosition) && conveyor.PeekOutput() is not null)
            {
                conveyor.RemoveOutput();
                wallet.AddMoney(IronOreSalePrice);
                SoldItems++;
            }
        }
    }

    public bool IsMinerTile(GridPosition position) => minerByTile.ContainsKey(position);

    public bool TryGetMinerAt(GridPosition position, out MinerBuilding miner) =>
        minerByTile.TryGetValue(position, out miner!);

    private bool IsInside(GridPosition position) =>
        position.X >= 0 && position.X < Terrain.Width
        && position.Y >= 0 && position.Y < Terrain.Height;

    private static IEnumerable<GridPosition> Footprint(GridPosition origin)
    {
        for (var y = 0; y < MinerBuilding.Size; y++)
        {
            for (var x = 0; x < MinerBuilding.Size; x++)
            {
                yield return new GridPosition(origin.X + x, origin.Y + y);
            }
        }
    }
}