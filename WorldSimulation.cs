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

public sealed class MinerCell
{
    public MinerCell(GridPosition position, Direction direction)
    {
        Position = position;
        Direction = direction;
    }

    public GridPosition Position { get; }
    public Direction Direction { get; }
    public float Progress { get; internal set; }
    public GridPosition OutputPosition => Position.Step(Direction);
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

    private readonly Dictionary<GridPosition, MinerCell> miners = [];

    public FactoryWorld(int width, int height, int seed)
    {
        CoreTiles = new HashSet<GridPosition>
        {
            new(width - 4, height / 2 - 1),
            new(width - 3, height / 2 - 1),
            new(width - 4, height / 2),
            new(width - 3, height / 2)
        };
        Terrain = TerrainMap.Generate(width, height, seed, CoreTiles);
    }

    public TerrainMap Terrain { get; }
    public IReadOnlyDictionary<GridPosition, MinerCell> Miners => miners;
    public IReadOnlySet<GridPosition> CoreTiles { get; }
    public int SoldItems { get; private set; }

    public bool CanPlaceConveyor(GridPosition position) =>
        Terrain[position].IsBuildable && !CoreTiles.Contains(position) && !miners.ContainsKey(position);

    public bool CanPlaceMiner(GridPosition position, ConveyorGrid conveyors) =>
        Terrain[position].Deposit == DepositKind.Iron
        && !CoreTiles.Contains(position)
        && !miners.ContainsKey(position)
        && !conveyors.Cells.ContainsKey(position);

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

        miners.Add(position, new MinerCell(position, direction));
        return true;
    }

    public bool TryRemoveMiner(GridPosition position, EconomyWallet wallet)
    {
        if (!miners.Remove(position))
        {
            return false;
        }

        wallet.AddMoney(MinerMoneyCost);
        wallet.AddMaterial("iron-plate", MinerPlateCost);
        return true;
    }

    public void Update(float deltaSeconds, ConveyorGrid conveyors, EconomyWallet wallet, ref long nextItemId)
    {
        foreach (var miner in miners.Values)
        {
            miner.Progress = Math.Min(miner.Progress + deltaSeconds / MiningDurationSeconds, 1f);
            if (miner.Progress >= 1f
                && conveyors.Cells.TryGetValue(miner.OutputPosition, out var output)
                && output.Items.Count < output.Definition.Capacity
                && output.TryInsert(new TransportedItem(nextItemId, "iron-ore")))
            {
                nextItemId++;
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
}