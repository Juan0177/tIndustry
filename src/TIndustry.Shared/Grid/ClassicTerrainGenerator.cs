namespace TIndustry.Shared;

/// <summary>
/// The pre-G2a generator, frozen: saves from v11 and the tests rely on it producing the very same cells
/// (classic water becomes <see cref="FloorKind.ShallowWater"/>, there are no walls). Do not change it.
/// </summary>
public static class ClassicTerrainGenerator
{
    /// <summary>Value of <see cref="TerrainMap.Generator"/> for maps made here.</summary>
    public const string Name = "classico";

    // The four terrain kinds of the classic generator.
    private enum ClassicKind
    {
        Grass,
        Soil,
        Stone,
        Water
    }

    private static FloorKind FloorFor(ClassicKind kind) => kind switch
    {
        ClassicKind.Grass => FloorKind.Grass,
        ClassicKind.Soil => FloorKind.Soil,
        ClassicKind.Stone => FloorKind.Stone,
        _ => FloorKind.ShallowWater
    };

    public static TerrainMap Generate(
        int width,
        int height,
        int seed,
        IReadOnlySet<GridPosition> coreTiles,
        GridPosition starterDepositOrigin)
    {
        width = Math.Max(8, width);
        height = Math.Max(8, height);
        var map = new TerrainMap(width, height, seed, Name);
        var oreThreshold = width >= 256 ? 0.72f : 0.63f;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var elevation = SmoothNoise(x, y, seed);
                var terrain = elevation switch
                {
                    < 0.13f => ClassicKind.Water,
                    < 0.45f => ClassicKind.Grass,
                    < 0.78f => ClassicKind.Soil,
                    _ => ClassicKind.Stone
                };
                var oreNoise = SmoothNoise(x + 31, y - 17, seed * 3 + 11);
                var copperNoise = SmoothNoise(x - 19, y + 41, seed * 5 + 29);
                var coalNoise = SmoothNoise(x + 7, y + 53, seed * 7 + 17);
                var leadNoise = SmoothNoise(x - 11, y - 29, seed * 11 + 41);
                var titaniumNoise = SmoothNoise(x + 53, y + 13, seed * 13 + 7);
                var deposit = DepositKind.None;
                if (terrain != ClassicKind.Water)
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

                map.Set(x, y, new TerrainTile(FloorFor(terrain), WallKind.None, deposit));
            }
        }

        foreach (var position in coreTiles)
        {
            if (!map.InBounds(position))
            {
                continue;
            }

            map.Set(position.X, position.Y, new TerrainTile(FloorKind.Stone, WallKind.None, DepositKind.None));
        }

        PlaceDepositPatch(map, starterDepositOrigin, Miner.Size, DepositKind.Iron);

        var copperStarter = new GridPosition(
            starterDepositOrigin.X, starterDepositOrigin.Y + Miner.Size + 1);
        if (copperStarter.Y + Miner.Size <= height)
        {
            PlaceDepositPatch(map, copperStarter, Miner.Size, DepositKind.Copper);
        }

        var coalStarter = new GridPosition(
            starterDepositOrigin.X + Miner.Size + 1, starterDepositOrigin.Y);
        if (coalStarter.X + Miner.Size <= width && !coreTiles.Contains(coalStarter))
        {
            PlaceDepositPatch(map, coalStarter, Miner.Size, DepositKind.Coal);
        }

        var leadStarter = new GridPosition(
            starterDepositOrigin.X - Miner.Size - 1, starterDepositOrigin.Y);
        if (leadStarter.X >= 0 && !coreTiles.Contains(leadStarter))
        {
            PlaceDepositPatch(map, leadStarter, Miner.Size, DepositKind.Lead);
        }

        var legacyOrigin = new GridPosition(2, 2);
        if (!coreTiles.Contains(legacyOrigin)
            && !coreTiles.Contains(new GridPosition(3, 3)))
        {
            PlaceDepositPatch(map, legacyOrigin, Miner.Size, DepositKind.Iron);
        }

        // Raylib scout patches on large maps — visible veins near the starter cluster.
        if (width >= 256)
        {
            PlaceDepositPatch(map, new GridPosition(
                Math.Clamp(starterDepositOrigin.X - 48, 2, width - 4),
                Math.Clamp(starterDepositOrigin.Y - 20, 2, height - 4)), 3, DepositKind.Iron);
            PlaceDepositPatch(map, new GridPosition(
                Math.Clamp(starterDepositOrigin.X + 36, 2, width - 4),
                Math.Clamp(starterDepositOrigin.Y + 40, 2, height - 4)), 3, DepositKind.Iron);
            PlaceDepositPatch(map, new GridPosition(
                Math.Clamp(starterDepositOrigin.X - 20, 2, width - 4),
                Math.Clamp(starterDepositOrigin.Y + 28, 2, height - 4)), 3, DepositKind.Copper);
            PlaceDepositPatch(map, new GridPosition(
                Math.Clamp(starterDepositOrigin.X + 24, 2, width - 4),
                Math.Clamp(starterDepositOrigin.Y - 18, 2, height - 4)), 3, DepositKind.Coal);
            PlaceDepositPatch(map, new GridPosition(
                Math.Clamp(starterDepositOrigin.X + 52, 2, width - 4),
                Math.Clamp(starterDepositOrigin.Y + 12, 2, height - 4)), 3, DepositKind.Lead);
            PlaceDepositPatch(map, new GridPosition(
                Math.Clamp(starterDepositOrigin.X - 36, 2, width - 4),
                Math.Clamp(starterDepositOrigin.Y + 52, 2, height - 4)), 2, DepositKind.Titanium);
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

                map.Set(position.X, position.Y, new TerrainTile(FloorKind.Stone, WallKind.None, deposit));
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
