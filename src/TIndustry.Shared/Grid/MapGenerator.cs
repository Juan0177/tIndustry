namespace TIndustry.Shared;

/// <summary>
/// The biome generator (<see cref="TerrainMap.Generator"/> = <c>"biomi"</c>). Integer-only and seeded, so the
/// same settings give the same map everywhere.
/// </summary>
/// <remarks>
/// Steps, all from <see cref="Noise"/>:
/// <list type="number">
/// <item>Climate: temperature and moisture are low-frequency noise sampled at a domain-warped position (a
/// medium warp bends the borders, a fine one frays them), then equalised over the map so every biome keeps its
/// share. The biome is the allowed one whose (temperature, moisture) centre is nearest; the gap to the
/// second nearest measures how close the cell is to a biome border.</item>
/// <item>Elevation (equalised) below a moisture-driven water level makes lakes; the zero band of a second
/// noise makes rivers. Wet cells beside them get the biome's shore floor.</item>
/// <item>Walls: ridged noise (high along the zero contour of a noise) above a per-biome threshold, lowered near
/// biome borders and gated by a coarse mask so ridges break into chains; boulder clusters in the rocks.</item>
/// <item>Deposits: one clustered noise per ore, with per-biome bonuses; never on walls or water.</item>
/// <item>Water depth: city-block distance to land; cells farther than a 1–3 cell ring are deep.</item>
/// <item>Start: a free buildable disc around the Core, the iron and copper starter patches, and a land path
/// (4-neighbour flood fill) to iron, copper and coal, dug through walls and deep water when missing.</item>
/// </list>
/// </remarks>
public static class MapGenerator
{
    /// <summary>
    /// Value of <see cref="TerrainMap.Generator"/> for maps made here. Published and frozen: saves regenerate
    /// their terrain from it, so a change to the generation must go under a new name ("biomi-2") while this
    /// one stays as it is for the saves that use it (ADR 0005).
    /// </summary>
    public const string Name = "biomi-1";

    /// <summary>Name saved before the generator had a version: the same generator as <see cref="Name"/>.</summary>
    public const string UnversionedName = "biomi";

    /// <summary>True when a save's <paramref name="generator"/> means this generator.</summary>
    public static bool IsName(string? generator) => generator is Name or UnversionedName;

    /// <summary>Free buildable radius around the Core footprint.</summary>
    public const int StartRadius = 10;

    private const int Rocce = 0;
    private const int Sabbia = 1;
    private const int Neve = 2;
    private const int Prati = 3;

    // Climate centre (temperature, moisture) of each biome, equalised scale 0..65535.
    private static readonly int[] CentreT = [33000, 60000, 5000, 44000];
    private static readonly int[] CentreM = [24000, 8000, 34000, 58000];

    private static readonly WallKind[] Walls = [WallKind.StoneWall, WallKind.SandWall, WallKind.IceWall, WallKind.ShrubWall];

    private static readonly FloorKind[] BaseFloor = [FloorKind.Stone, FloorKind.Sand, FloorKind.Snow, FloorKind.Grass];

    private static readonly FloorKind[] ShoreFloor = [FloorKind.Gravel, FloorKind.DarkSand, FloorKind.Ice, FloorKind.Mud];

    // Ridge value above which a wall stands, and the coarse mask value it also needs.
    private static readonly int[] RidgeThreshold = [57500, 60500, 59500, 62000];
    private static readonly int[] MaskThreshold = [24000, 30000, 28000, 36000];

    // Deposit threshold bonus per biome, per ore (Iron, Copper, Coal, Lead, Titanium).
    private static readonly int[][] DepositBonus =
    [
        [7000, 0, 6000, 1500, 0],
        [0, 7000, 0, 6000, 0],
        [1500, 0, 0, 0, 7000],
        [3500, 3500, 3500, 3000, 1500]
    ];

    private const int DepositBase = 55000;

    public static TerrainMap Generate(MapGenSettings s, IReadOnlySet<GridPosition> core, GridPosition starter)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(core);
        if (s.Width < 8 || s.Height < 8 || s.Width > Factory.MaxMapEdge || s.Height > Factory.MaxMapEdge)
        {
            throw new ArgumentOutOfRangeException(
                nameof(s), $"Mappa {s.Width}×{s.Height}: il lato va da 8 a {Factory.MaxMapEdge}.");
        }

        if (s.WallDensityPercent < 0 || s.WaterPercent < 0 || s.DepositPercent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(s), "Le percentuali non possono essere negative.");
        }

        var allowed = AllowedBiomes(s.Biomes);
        var w = s.Width;
        var h = s.Height;
        var map = new TerrainMap(w, h, s.Seed, Name);
        var biomes = new byte[w * h];
        var seeds = new Seeds(s.Seed);
        var biomeCell = Math.Clamp(Math.Max(w, h) / 3, 96, 400);
        var step = Math.Max(4, Math.Max(w, h) / 128);
        var tempCdf = Cdf(w, h, step, (x, y) => Noise.Fractal(x, y, seeds.Temperature, biomeCell, 2));
        var moistCdf = Cdf(w, h, step, (x, y) => Noise.Fractal(x, y, seeds.Moisture, biomeCell, 2));
        var elevCdf = Cdf(w, h, step, (x, y) => Noise.Fractal(x, y, seeds.Elevation, 128, 3));

        var ridgeLimit = new int[4];
        var depositLimit = new int[4][];
        for (var b = 0; b < 4; b++)
        {
            ridgeLimit[b] = Scale(RidgeThreshold[b], s.WallDensityPercent);
            depositLimit[b] = new int[5];
            for (var k = 0; k < 5; k++)
            {
                depositLimit[b][k] = Scale(DepositBase - DepositBonus[b][k], s.DepositPercent);
            }
        }

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                // 1. Climate at a warped position.
                var wx = x + Offset(Noise.Fractal(x, y, seeds.WarpX, 96, 2), 44) + Offset(Noise.Value2D(x, y, seeds.FrayX, 6), 7);
                var wy = y + Offset(Noise.Fractal(x, y, seeds.WarpY, 96, 2), 44) + Offset(Noise.Value2D(x, y, seeds.FrayY, 6), 7);
                var t = Equalise(tempCdf, Noise.Fractal(wx, wy, seeds.Temperature, biomeCell, 2));
                var m = Equalise(moistCdf, Noise.Fractal(wx, wy, seeds.Moisture, biomeCell, 2));
                var (biome, border) = Classify(t, m, allowed);
                biomes[y * w + x] = (byte)biome;

                // 2. Water: lakes where the ground is low, rivers along a noise contour.
                var elevation = Equalise(elevCdf, Noise.Fractal(x, y, seeds.Elevation, 128, 3));
                var waterLevel = (int)((1200L + m * 8500L / Noise.Max + (Noise.Max - t) * 2500L / Noise.Max) * s.WaterPercent / 100);
                var river = Math.Abs(Noise.Fractal(x, y, seeds.River, 320, 2) - 32768);
                var riverWidth = Noise.Value2D(x, y, seeds.RiverMask, 400) < 30000
                    ? 0
                    : (int)((220L + m * 700L / Noise.Max) * s.WaterPercent / 100);
                if (elevation < waterLevel || river < riverWidth)
                {
                    map.Set(x, y, new TerrainTile(FloorKind.ShallowWater, WallKind.None, DepositKind.None));
                    continue;
                }

                var shore = elevation < waterLevel + 1800 || river < riverWidth * 2;
                var floor = shore ? ShoreFloor[biome] : FloorFor(biome, Noise.Fractal(x, y, seeds.Floor, 24, 2));

                // 3. Walls: ridges (denser near borders), boulder clusters in the rocks.
                var ridge = Math.Max(0, Noise.Max - 2 * Math.Abs(Noise.Fractal(x, y, seeds.Ridge, 64, 3) - 32768));
                var limit = ridgeLimit[biome] - (border < 5000 ? (5000 - border) * 3 / 5 : 0);
                var wall = (ridge > limit && Noise.Fractal(x, y, seeds.Mask, 80, 2) > MaskThreshold[biome])
                    || (biome == Rocce && Noise.Fractal(x, y, seeds.Boulder, 10, 2) > Scale(52000, s.WallDensityPercent));
                if (wall && !shore)
                {
                    map.Set(x, y, new TerrainTile(floor, Walls[biome], DepositKind.None));
                    continue;
                }

                // 4. Deposits, only inside coarse ore fields so veins come in groups: the ore whose noise
                // clears its biome threshold by the most.
                var deposit = DepositKind.None;
                var best = 0;
                var oreField = Noise.Value2D(x, y, seeds.OreField, 90) > 30000;
                for (var k = 0; oreField && k < 5; k++)
                {
                    var excess = Noise.Fractal(x, y, seeds.Deposits[k], 14, 2) - depositLimit[biome][k];
                    if (excess > best)
                    {
                        best = excess;
                        deposit = (DepositKind)(k + 1);
                    }
                }

                map.Set(x, y, new TerrainTile(floor, WallKind.None, deposit));
            }
        }

        // Clear the start first, so the shallow ring also lines the water the disc cuts.
        ClearStart(map, biomes, core, starter);
        SetWaterDepth(map, seeds.Ring);
        EnsureLandPaths(map, core, starter);
        return map;
    }

    private static int[] AllowedBiomes(IReadOnlyList<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var allowed = new List<int>();
        foreach (var id in ids)
        {
            var index = -1;
            for (var b = 0; b < MapGenSettings.AllBiomes.Count; b++)
            {
                if (string.Equals(MapGenSettings.AllBiomes[b], id, StringComparison.Ordinal))
                {
                    index = b;
                }
            }

            if (index < 0)
            {
                throw new ArgumentException(
                    $"Bioma '{id}' sconosciuto ({string.Join(", ", MapGenSettings.AllBiomes)}).", nameof(ids));
            }

            if (!allowed.Contains(index))
            {
                allowed.Add(index);
            }
        }

        if (allowed.Count == 0)
        {
            throw new ArgumentException("Serve almeno un bioma.", nameof(ids));
        }

        allowed.Sort();
        return [.. allowed];
    }

    /// <summary>Nearest allowed biome centre and the distance (climate units) to the border with the next one.</summary>
    private static (int Biome, int Border) Classify(int t, int m, int[] allowed)
    {
        var first = -1;
        var second = -1;
        long d1 = long.MaxValue;
        long d2 = long.MaxValue;
        foreach (var b in allowed)
        {
            long dt = t - CentreT[b];
            long dm = m - CentreM[b];
            var d = dt * dt + dm * dm;
            if (d < d1)
            {
                second = first;
                d2 = d1;
                first = b;
                d1 = d;
            }
            else if (d < d2)
            {
                second = b;
                d2 = d;
            }
        }

        if (second < 0)
        {
            return (first, int.MaxValue);
        }

        // d2 - d1 = 2 · |c1 - c2| · (distance to the bisector), so no square root of the cell is needed.
        return (first, (int)Math.Min(int.MaxValue, (d2 - d1) / (2 * CentreGap(first, second))));
    }

    private static long CentreGap(int a, int b)
    {
        long dt = CentreT[a] - CentreT[b];
        long dm = CentreM[a] - CentreM[b];
        return Math.Max(1, ISqrt(dt * dt + dm * dm));
    }

    private static long ISqrt(long n)
    {
        long r = 0;
        long bit = 1L << 62;
        while (bit > n)
        {
            bit >>= 2;
        }

        while (bit != 0)
        {
            if (n >= r + bit)
            {
                n -= r + bit;
                r = (r >> 1) + bit;
            }
            else
            {
                r >>= 1;
            }

            bit >>= 2;
        }

        return r;
    }

    private static FloorKind FloorFor(int biome, int n) => biome switch
    {
        Rocce => n < 29000 ? FloorKind.Stone : n < 36500 ? FloorKind.Gravel : FloorKind.Basalt,
        Sabbia => n < 37000 ? FloorKind.Sand : FloorKind.DarkSand,
        Neve => n < 38500 ? FloorKind.Snow : FloorKind.Ice,
        _ => n < 25000 ? FloorKind.Soil : n < 37500 ? FloorKind.Grass : FloorKind.Moss
    };

    // Raises a threshold towards the top so only (percent/100) of the band above it remains.
    private static int Scale(int threshold, int percent) =>
        (int)Math.Max(-1, Noise.Max - (Noise.Max - (long)threshold) * percent / 100);

    private static int Offset(int noise, int amplitude) => (int)(((long)noise - 32768) * amplitude >> 15);

    /// <summary>Cumulative histogram (257 points, 0..65535) of a noise sampled every <paramref name="step"/> cells.</summary>
    private static int[] Cdf(int w, int h, int step, Func<int, int, int> sample)
    {
        var counts = new long[256];
        long total = 0;
        for (var y = step / 2; y < h; y += step)
        {
            for (var x = step / 2; x < w; x += step)
            {
                counts[sample(x, y) >> 8]++;
                total++;
            }
        }

        var cdf = new int[257];
        long running = 0;
        for (var i = 0; i < 256; i++)
        {
            cdf[i] = (int)(running * Noise.Max / Math.Max(1, total));
            running += counts[i];
        }

        cdf[256] = Noise.Max;
        return cdf;
    }

    private static int Equalise(int[] cdf, int v)
    {
        var bin = v >> 8;
        return cdf[bin] + (cdf[bin + 1] - cdf[bin]) * (v & 255) / 256;
    }

    /// <summary>Water farther from land than a 1–3 cell ring becomes deep.</summary>
    private static void SetWaterDepth(TerrainMap map, int ringSeed)
    {
        var w = map.Width;
        var h = map.Height;
        var dist = new byte[w * h];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = y * w + x;
                if (map[x, y].Floor != FloorKind.ShallowWater)
                {
                    continue;
                }

                var d = 255;
                if (x > 0)
                {
                    d = Math.Min(d, dist[i - 1] + 1);
                }

                if (y > 0)
                {
                    d = Math.Min(d, dist[i - w] + 1);
                }

                dist[i] = (byte)Math.Min(255, d);
            }
        }

        for (var y = h - 1; y >= 0; y--)
        {
            for (var x = w - 1; x >= 0; x--)
            {
                var i = y * w + x;
                if (dist[i] == 0)
                {
                    continue;
                }

                var d = (int)dist[i];
                if (x < w - 1)
                {
                    d = Math.Min(d, dist[i + 1] + 1);
                }

                if (y < h - 1)
                {
                    d = Math.Min(d, dist[i + w] + 1);
                }

                dist[i] = (byte)d;
                var ring = 1 + Noise.Value2D(x, y, ringSeed, 20) * 3 / 65536;
                if (d > ring)
                {
                    map.Set(x, y, new TerrainTile(FloorKind.DeepWater, WallKind.None, DepositKind.None));
                }
            }
        }
    }

    /// <summary>Clears a disc of <see cref="StartRadius"/> around the Core and lays the starter patches.</summary>
    private static void ClearStart(TerrainMap map, byte[] biomes, IReadOnlySet<GridPosition> core, GridPosition starter)
    {
        if (core.Count == 0)
        {
            return;
        }

        var (minX, minY, maxX, maxY) = Bounds(core);
        for (var y = Math.Max(0, minY - StartRadius); y <= Math.Min(map.Height - 1, maxY + StartRadius); y++)
        {
            for (var x = Math.Max(0, minX - StartRadius); x <= Math.Min(map.Width - 1, maxX + StartRadius); x++)
            {
                var dx = x < minX ? minX - x : x > maxX ? x - maxX : 0;
                var dy = y < minY ? minY - y : y > maxY ? y - maxY : 0;
                if (dx * dx + dy * dy > StartRadius * StartRadius)
                {
                    continue;
                }

                var floor = map[x, y].Floor;
                if (floor is FloorKind.ShallowWater or FloorKind.DeepWater)
                {
                    floor = BaseFloor[biomes[y * map.Width + x]];
                }

                map.Set(x, y, new TerrainTile(floor, WallKind.None, DepositKind.None));
            }
        }

        Patch(map, starter, DepositKind.Iron);
        var copper = new GridPosition(starter.X, starter.Y + Miner.Size + 1);
        if (copper.Y + Miner.Size > map.Height)
        {
            copper = new GridPosition(starter.X, starter.Y - Miner.Size - 1);
        }

        Patch(map, copper, DepositKind.Copper);
    }

    private static void Patch(TerrainMap map, GridPosition origin, DepositKind deposit)
    {
        for (var y = origin.Y; y < origin.Y + Miner.Size; y++)
        {
            for (var x = origin.X; x < origin.X + Miner.Size; x++)
            {
                if (map.InBounds(new GridPosition(x, y)))
                {
                    var floor = map[x, y].Floor;
                    map.Set(x, y, new TerrainTile(
                        floor == FloorKind.DeepWater ? FloorKind.ShallowWater : floor, WallKind.None, deposit));
                }
            }
        }
    }

    private static (int MinX, int MinY, int MaxX, int MaxY) Bounds(IReadOnlySet<GridPosition> cells)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var p in cells)
        {
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X);
            maxY = Math.Max(maxY, p.Y);
        }

        return (minX, minY, maxX, maxY);
    }

    private static readonly DepositKind[] RequiredOres = [DepositKind.Iron, DepositKind.Copper, DepositKind.Coal];

    /// <summary>
    /// Flood-fills the land reachable from the Core; for each of iron, copper and coal not reached, digs an
    /// L-shaped passage to the nearest deposit of that ore (or lays a patch near the start, on cells holding
    /// no other deposit, if the map has none). Repeats until all three are reached, so a patch can never
    /// hide an earlier one.
    /// </summary>
    internal static void EnsureLandPaths(TerrainMap map, IReadOnlySet<GridPosition> core, GridPosition starter)
    {
        if (core.Count == 0)
        {
            return;
        }

        var (minX, minY, _, _) = Bounds(core);
        var origin = new GridPosition(Math.Clamp(minX, 0, map.Width - 1), Math.Clamp(minY, 0, map.Height - 1));
        for (var pass = 0; pass < RequiredOres.Length + 1; pass++)
        {
            var changed = false;
            foreach (var ore in RequiredOres)
            {
                if (Reaches(map, origin, ore))
                {
                    continue;
                }

                changed = true;
                if (Nearest(map, origin, ore) is { } target)
                {
                    Dig(map, origin, target);
                }
                else if (FreePatchSpot(map, core, starter) is { } spot)
                {
                    Patch(map, spot, ore);
                }
            }

            if (!changed)
            {
                return;
            }
        }
    }

    /// <summary>First spot near the starter whose 2×2 is in the map, off the Core and free of deposits.</summary>
    private static GridPosition? FreePatchSpot(TerrainMap map, IReadOnlySet<GridPosition> core, GridPosition starter)
    {
        const int gap = Miner.Size + 1;
        GridPosition[] candidates =
        [
            new(starter.X, starter.Y - gap), new(starter.X, starter.Y + gap),
            new(starter.X - gap, starter.Y), new(starter.X - gap, starter.Y - gap),
            new(starter.X - gap, starter.Y + gap), new(starter.X, starter.Y - 2 * gap),
            new(starter.X, starter.Y + 2 * gap), new(starter.X - 2 * gap, starter.Y)
        ];
        foreach (var spot in candidates)
        {
            var free = true;
            for (var y = spot.Y; y < spot.Y + Miner.Size; y++)
            {
                for (var x = spot.X; x < spot.X + Miner.Size; x++)
                {
                    var p = new GridPosition(x, y);
                    free &= map.InBounds(p) && !core.Contains(p) && map[p].Deposit == DepositKind.None;
                }
            }

            if (free)
            {
                return spot;
            }
        }

        return null;
    }

    /// <summary>Is a cell of <paramref name="ore"/> reachable from <paramref name="origin"/> over buildable cells (4 neighbours)?</summary>
    public static bool Reaches(TerrainMap map, GridPosition origin, DepositKind ore)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (!map.InBounds(origin))
        {
            return false;
        }

        var w = map.Width;
        var seen = new bool[w * map.Height];
        var queue = new Queue<int>();
        var start = origin.Y * w + origin.X;
        seen[start] = true;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var i = queue.Dequeue();
            var x = i % w;
            var y = i / w;
            if (map[x, y].Deposit == ore)
            {
                return true;
            }

            Visit(x - 1, y);
            Visit(x + 1, y);
            Visit(x, y - 1);
            Visit(x, y + 1);
        }

        return false;

        void Visit(int nx, int ny)
        {
            if (nx < 0 || ny < 0 || nx >= w || ny >= map.Height)
            {
                return;
            }

            var j = ny * w + nx;
            if (!seen[j] && !map[nx, ny].BlocksBuilding)
            {
                seen[j] = true;
                queue.Enqueue(j);
            }
        }
    }

    private static GridPosition? Nearest(TerrainMap map, GridPosition origin, DepositKind ore)
    {
        GridPosition? best = null;
        var bestDistance = int.MaxValue;
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var d = Math.Abs(x - origin.X) + Math.Abs(y - origin.Y);
                if (d < bestDistance && map[x, y].Deposit == ore)
                {
                    bestDistance = d;
                    best = new GridPosition(x, y);
                }
            }
        }

        return best;
    }

    // Along y = from.Y to target.X, then along x = target.X: walls go, deep water turns shallow.
    private static void Dig(TerrainMap map, GridPosition from, GridPosition target)
    {
        var dx = Math.Sign(target.X - from.X);
        for (var x = from.X; x != target.X; x += dx)
        {
            Clear(map, x, from.Y);
        }

        var dy = Math.Sign(target.Y - from.Y);
        for (var y = from.Y; y != target.Y; y += dy)
        {
            Clear(map, target.X, y);
        }

        Clear(map, target.X, target.Y);
    }

    private static void Clear(TerrainMap map, int x, int y)
    {
        var tile = map[x, y];
        if (tile.BlocksBuilding)
        {
            map.Set(x, y, new TerrainTile(
                tile.Floor == FloorKind.DeepWater ? FloorKind.ShallowWater : tile.Floor, WallKind.None, tile.Deposit));
        }
    }

    /// <summary>One seed per noise layer, all derived from the map seed.</summary>
    private sealed class Seeds(int seed)
    {
        public readonly int Temperature = Mix(seed, 1);
        public readonly int Moisture = Mix(seed, 2);
        public readonly int Elevation = Mix(seed, 3);
        public readonly int River = Mix(seed, 4);
        public readonly int RiverMask = Mix(seed, 5);
        public readonly int WarpX = Mix(seed, 6);
        public readonly int WarpY = Mix(seed, 7);
        public readonly int FrayX = Mix(seed, 8);
        public readonly int FrayY = Mix(seed, 9);
        public readonly int Floor = Mix(seed, 10);
        public readonly int Ridge = Mix(seed, 11);
        public readonly int Mask = Mix(seed, 12);
        public readonly int Boulder = Mix(seed, 13);
        public readonly int Ring = Mix(seed, 14);
        public readonly int OreField = Mix(seed, 15);
        public readonly int[] Deposits = [Mix(seed, 20), Mix(seed, 21), Mix(seed, 22), Mix(seed, 23), Mix(seed, 24)];

        private static int Mix(int seed, int layer) =>
            unchecked(Noise.Hash(seed, layer, 0x5EED) * 65536 + Noise.Hash(layer, seed, 0x1A7E));
    }
}
