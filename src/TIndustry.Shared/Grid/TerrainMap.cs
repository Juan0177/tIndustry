namespace TIndustry.Shared;

/// <summary>Ground of a cell. The biome and colour of each kind live in <c>content.json</c> (<c>terrain.floors</c>).</summary>
public enum FloorKind : byte
{
    Stone,
    Basalt,
    Gravel,
    Sand,
    DarkSand,
    Snow,
    Ice,
    Grass,
    Moss,
    Mud,
    Soil,
    ShallowWater,
    DeepWater
}

/// <summary>Rock standing on a cell; anything but <see cref="None"/> blocks building (<c>terrain.walls</c>).</summary>
public enum WallKind : byte
{
    None,
    StoneWall,
    SandWall,
    IceWall,
    ShrubWall
}

public enum DepositKind : byte
{
    None,
    Iron,
    Copper,
    Coal,
    Lead,
    Titanium
}

/// <summary>One map cell: floor, wall and deposit, one byte each.</summary>
public readonly record struct TerrainTile(FloorKind Floor, WallKind Wall, DepositKind Deposit)
{
    /// <summary>Walls and deep water take no block, belt or bridge end (the drone flies over them).</summary>
    public bool BlocksBuilding => Wall != WallKind.None || Floor == FloorKind.DeepWater;
}

/// <summary>
/// Seeded terrain + deposits, 3 bytes per cell. <see cref="Generate"/> is the classic generator
/// (<see cref="ClassicTerrainGenerator"/>), <see cref="MapGenerator"/> the biome one; <see cref="Generator"/>
/// names the one that made the map.
/// </summary>
public sealed class TerrainMap
{
    private readonly TerrainTile[] tiles;

    internal TerrainMap(int width, int height, int seed, string generator)
    {
        Width = width;
        Height = height;
        Seed = seed;
        Generator = generator;
        tiles = new TerrainTile[width * height];
    }

    public int Width { get; }
    public int Height { get; }
    public int Seed { get; }

    /// <summary>Generator that made the map: <c>"classico"</c>, <c>"biomi-1"</c> or <c>"campagna:&lt;id&gt;"</c>.</summary>
    public string Generator { get; }

    public TerrainTile this[GridPosition position] => this[position.X, position.Y];

    public TerrainTile this[int x, int y]
    {
        get
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"Cella ({x}, {y}) fuori dalla mappa.");
            }

            return tiles[y * Width + x];
        }
    }

    public bool InBounds(GridPosition position) =>
        position.X >= 0 && position.Y >= 0 && position.X < Width && position.Y < Height;

    internal void Set(int x, int y, TerrainTile tile)
    {
        tiles[y * Width + x] = tile;
        hash = null;
    }

    private ulong? hash;

    /// <summary><see cref="TerrainHash"/> of this map, computed once (the map does not change during a game).</summary>
    public ulong Hash => hash ??= TerrainHash(this);

    /// <summary>Builds a map from explicit tiles (<c>tiles[x, y]</c>); used by tests and tools.</summary>
    public static TerrainMap FromTiles(
        TerrainTile[,] tiles, int seed = 0, string generator = ClassicTerrainGenerator.Name)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        var map = new TerrainMap(tiles.GetLength(0), tiles.GetLength(1), seed, generator);
        for (var x = 0; x < map.Width; x++)
        {
            for (var y = 0; y < map.Height; y++)
            {
                map.Set(x, y, tiles[x, y]);
            }
        }

        return map;
    }

    /// <summary>The classic generator (<see cref="ClassicTerrainGenerator.Generate"/>).</summary>
    public static TerrainMap Generate(
        int width,
        int height,
        int seed,
        IReadOnlySet<GridPosition> coreTiles,
        GridPosition starterDepositOrigin) =>
        ClassicTerrainGenerator.Generate(width, height, seed, coreTiles, starterDepositOrigin);

    /// <summary>FNV-1a 64 over the cells (floor, wall, deposit) in row order: the map's fingerprint.</summary>
    public static ulong TerrainHash(TerrainMap m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var hash = 14695981039346656037UL;
        foreach (var tile in m.tiles)
        {
            hash = (hash ^ (byte)tile.Floor) * 1099511628211UL;
            hash = (hash ^ (byte)tile.Wall) * 1099511628211UL;
            hash = (hash ^ (byte)tile.Deposit) * 1099511628211UL;
        }

        return hash;
    }

    public int CountDepositTiles(GridPosition origin, int size, DepositKind deposit)
    {
        var count = 0;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var p = new GridPosition(origin.X + x, origin.Y + y);
                if (InBounds(p) && this[p].Deposit == deposit)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// The deposit covering most cells of the footprint, among those <paramref name="canDig"/> accepts (all when
    /// null); <see cref="DepositKind.None"/> when there is none.
    /// </summary>
    public DepositKind MajorityDeposit(GridPosition origin, int size, Func<DepositKind, bool>? canDig = null)
    {
        var counts = new Dictionary<DepositKind, int>();
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var p = new GridPosition(origin.X + x, origin.Y + y);
                if (!InBounds(p))
                {
                    continue;
                }

                var d = this[p].Deposit;
                if (d == DepositKind.None || (canDig is not null && !canDig(d)))
                {
                    continue;
                }

                counts[d] = counts.GetValueOrDefault(d) + 1;
            }
        }

        if (counts.Count == 0)
        {
            return DepositKind.None;
        }

        // Most tiles wins; on a tie the lowest deposit id (ordinal string order) wins, never the
        // Dictionary enumeration order.
        return counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => ItemIdForDeposit(kv.Key), StringComparer.Ordinal)
            .First().Key;
    }

    public static string ItemIdForDeposit(DepositKind deposit) => deposit switch
    {
        DepositKind.Iron => "iron-ore",
        DepositKind.Copper => "copper-ore",
        DepositKind.Coal => "coal",
        DepositKind.Lead => "lead-ore",
        DepositKind.Titanium => "titanium-ore",
        _ => Miner.DefaultOutputItemId
    };
}
