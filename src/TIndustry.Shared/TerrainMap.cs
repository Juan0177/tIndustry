namespace TIndustry.Shared;

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
    Coal,
    Lead,
    Titanium
}

public readonly record struct TerrainTile(TerrainKind Terrain, DepositKind Deposit)
{
    public bool IsBuildable => Terrain != TerrainKind.Water;
}

/// <summary>
/// Seeded procedural terrain + deposits (ported from Raylib TerrainMap.Generate).
/// Godot spike clamps width/height; campaign seed drives patch layout.
/// </summary>
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
    public int Seed { get; private set; }
    public TerrainTile this[GridPosition position] => tiles[position.X, position.Y];
    public TerrainTile this[int x, int y] => tiles[x, y];

    public bool InBounds(GridPosition position) =>
        position.X >= 0 && position.Y >= 0 && position.X < Width && position.Y < Height;

    public static TerrainMap Generate(
        int width,
        int height,
        int seed,
        IReadOnlySet<GridPosition> coreTiles,
        GridPosition starterDepositOrigin)
    {
        width = Math.Max(8, width);
        height = Math.Max(8, height);
        var map = new TerrainMap(width, height) { Seed = seed };
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
                var leadNoise = SmoothNoise(x - 11, y - 29, seed * 11 + 41);
                var titaniumNoise = SmoothNoise(x + 53, y + 13, seed * 13 + 7);
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
                    else if (leadNoise > oreThreshold + 0.05f)
                    {
                        deposit = DepositKind.Lead;
                    }
                    else if (titaniumNoise > oreThreshold + 0.10f)
                    {
                        deposit = DepositKind.Titanium;
                    }
                }

                map.tiles[x, y] = new TerrainTile(terrain, deposit);
            }
        }

        foreach (var position in coreTiles)
        {
            if (!map.InBounds(position))
            {
                continue;
            }

            map.tiles[position.X, position.Y] = new TerrainTile(TerrainKind.Stone, DepositKind.None);
        }

        PlaceDepositPatch(map, starterDepositOrigin, MinerProducer.Size, DepositKind.Iron);

        var copperStarter = new GridPosition(
            starterDepositOrigin.X, starterDepositOrigin.Y + MinerProducer.Size + 1);
        if (copperStarter.Y + MinerProducer.Size <= height)
        {
            PlaceDepositPatch(map, copperStarter, MinerProducer.Size, DepositKind.Copper);
        }

        var coalStarter = new GridPosition(
            starterDepositOrigin.X + MinerProducer.Size + 1, starterDepositOrigin.Y);
        if (coalStarter.X + MinerProducer.Size <= width && !coreTiles.Contains(coalStarter))
        {
            PlaceDepositPatch(map, coalStarter, MinerProducer.Size, DepositKind.Coal);
        }

        var leadStarter = new GridPosition(
            starterDepositOrigin.X - MinerProducer.Size - 1, starterDepositOrigin.Y);
        if (leadStarter.X >= 0 && !coreTiles.Contains(leadStarter))
        {
            PlaceDepositPatch(map, leadStarter, MinerProducer.Size, DepositKind.Lead);
        }

        var legacyOrigin = new GridPosition(2, 2);
        if (!coreTiles.Contains(legacyOrigin)
            && !coreTiles.Contains(new GridPosition(3, 3)))
        {
            PlaceDepositPatch(map, legacyOrigin, MinerProducer.Size, DepositKind.Iron);
        }

        return map;
    }

    private static void PlaceDepositPatch(
        TerrainMap map, GridPosition origin, int size, DepositKind deposit)
    {
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var position = new GridPosition(origin.X + x, origin.Y + y);
                if (!map.InBounds(position))
                {
                    continue;
                }

                map.tiles[position.X, position.Y] = new TerrainTile(TerrainKind.Stone, deposit);
            }
        }
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

    public DepositKind MajorityDeposit(GridPosition origin, int size)
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
                if (d == DepositKind.None)
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

        return counts.OrderByDescending(kv => kv.Value).First().Key;
    }

    public static string ItemIdForDeposit(DepositKind deposit) => deposit switch
    {
        DepositKind.Iron => "iron-ore",
        DepositKind.Copper => "copper-ore",
        DepositKind.Coal => "coal",
        DepositKind.Lead => "lead-ore",
        DepositKind.Titanium => "titanium-ore",
        _ => MinerProducer.DefaultOutputItemId
    };

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
