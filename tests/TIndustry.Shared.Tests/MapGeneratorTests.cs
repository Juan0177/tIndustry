using System.Diagnostics;
using TIndustry.Shared;
using Xunit;
using Xunit.Abstractions;

namespace TIndustry.Shared.Tests;

/// <summary>A timing test: runs only in Release and off CI (shared runners make timings noisy).</summary>
public sealed class ReleaseFactAttribute : FactAttribute
{
    public ReleaseFactAttribute()
    {
#if DEBUG
        Skip = "misura di prestazioni: solo in Release";
#else
        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Skip = "misura di prestazioni: saltata in CI";
        }
#endif
    }
}

/// <summary>A test that runs only when <c>TINDUSTRY_MAP_PREVIEW</c> is set (it writes PNG files).</summary>
public sealed class PreviewFactAttribute : FactAttribute
{
    public PreviewFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TINDUSTRY_MAP_PREVIEW")))
        {
            Skip = "anteprime: imposta TINDUSTRY_MAP_PREVIEW=1 (o una cartella)";
        }
    }
}

/// <summary>The biome generator: determinism, biomes, the guaranteed start, deposits and timings.</summary>
public sealed class MapGeneratorTests(ITestOutputHelper output)
{
    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    /// <summary>Generates through <see cref="NewGame.Sandbox"/> with the Core centred, as the client will.</summary>
    private static (TerrainMap Map, GridPosition Core) Generate(MapGenSettings settings)
    {
        var core = Factory.CenteredCoreOrigin(settings.Width, settings.Height);
        var factory = NewGame.Sandbox(Content, core, map: settings);
        return (factory.Terrain!, core);
    }

    private static MapGenSettings Small(int seed, int edge = 256) =>
        new(edge, edge, seed, MapGenSettings.AllBiomes);

    private static string Label(MapGenSettings s) =>
        $"{s.Width}x{s.Height} seme {s.Seed} biomi {string.Join(",", s.Biomes)} " +
        $"pareti {s.WallDensityPercent}% acqua {s.WaterPercent}% giacimenti {s.DepositPercent}%";

    [Fact]
    public void SameSeedSameMap()
    {
        var a = Generate(Small(7)).Map;
        var b = Generate(Small(7)).Map;
        Assert.Equal("biomi-1", a.Generator);
        Assert.Equal(TerrainMap.TerrainHash(a), TerrainMap.TerrainHash(b));
    }

    [Fact]
    public void DifferentSeedDifferentMap()
    {
        var hashes = new[] { 1, 2, 3, 42 }.Select(seed => TerrainMap.TerrainHash(Generate(Small(seed)).Map)).ToList();
        Assert.Equal(hashes.Count, hashes.Distinct().Count());
    }

    [Fact]
    public void HashIsStableAcrossRuns() =>
        // "biomi-1" is published and frozen: saves regenerate their terrain with it. Never change it to fit a
        // new generator; add a new generator name ("biomi-2") and keep this one (ADR 0005).
        Assert.Equal(10891508615017362469UL, TerrainMap.TerrainHash(Generate(Small(42)).Map));

    [Fact]
    public void ClassicStaysTheDefault()
    {
        var factory = NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120);
        Assert.Equal("classico", factory.Terrain!.Generator);
    }

    [Theory]
    [InlineData(42)]
    [InlineData(1234)]
    [InlineData(-77)]
    public void AllFourBiomesOnMediumMap(int seed)
    {
        var (map, _) = Generate(MapGenSettings.Normal(MapSize.Media, seed));
        Assert.Equal(1000, map.Width);
        var counts = BiomeCounts(map);
        var cells = (double)map.Width * map.Height;
        output.WriteLine(string.Join(", ", counts.OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key} {kv.Value / cells:P1}")));
        foreach (var biome in MapGenSettings.AllBiomes)
        {
            Assert.True(counts.GetValueOrDefault(biome) > cells * 0.08, $"{biome}: {counts.GetValueOrDefault(biome) / cells:P1}");
        }

        var water = counts.GetValueOrDefault("acqua") / cells;
        Assert.InRange(water, 0.02, 0.2);
    }

    [Fact]
    public void BiomeSubsetIsRespected()
    {
        var (map, _) = Generate(new MapGenSettings(256, 256, 5, ["neve"]));
        Assert.Equal(["acqua", "neve"], BiomeCounts(map).Keys.Order(StringComparer.Ordinal).ToArray());
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                Assert.True(map[x, y].Wall is WallKind.None or WallKind.IceWall);
            }
        }
    }

    [Fact]
    public void StartAreaIsFreeAndReachable()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var settings = Small(seed * 7919 + 3);
            AssertGoodStart(settings, Label(settings));
        }

        // Harsh settings: walls everywhere, one biome without coal bonus, a tiny map.
        foreach (var settings in new[]
        {
            new MapGenSettings(256, 256, 11, MapGenSettings.AllBiomes, WallDensityPercent: 400, WaterPercent: 300),
            new MapGenSettings(256, 256, 12, ["neve"], WallDensityPercent: 300),
            new MapGenSettings(256, 256, 13, ["sabbia"], DepositPercent: 0),
            new MapGenSettings(40, 40, 14, MapGenSettings.AllBiomes, WallDensityPercent: 300)
        })
        {
            AssertGoodStart(settings, Label(settings));
        }
    }

    /// <summary>The Nuova partita panel accepts any int seed: negative and extreme ones are valid and deterministic.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(-123456789)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void NegativeAndExtremeSeedsWork(int seed)
    {
        var settings = Small(seed);
        Assert.Equal(TerrainMap.TerrainHash(Generate(settings).Map), TerrainMap.TerrainHash(Generate(settings).Map));
        AssertGoodStart(settings, Label(settings));
    }

    private static void AssertGoodStart(MapGenSettings settings, string label)
    {
        var (map, core) = Generate(settings);

        // Free buildable disc of radius 10 around the 2×2 Core.
        for (var y = core.Y - 10; y <= core.Y + 11; y++)
        {
            for (var x = core.X - 10; x <= core.X + 11; x++)
            {
                var dx = Math.Max(0, Math.Max(core.X - x, x - core.X - 1));
                var dy = Math.Max(0, Math.Max(core.Y - y, y - core.Y - 1));
                if (dx * dx + dy * dy <= 100 && map.InBounds(new GridPosition(x, y)))
                {
                    Assert.False(map[x, y].BlocksBuilding, $"{label}: ({x}, {y}) bloccata");
                }
            }
        }

        // Starter iron and the corridor the playtests use.
        Assert.Equal(4, map.CountDepositTiles(new GridPosition(core.X - 4, core.Y), 2, DepositKind.Iron));
        for (var x = core.X - 6; x <= core.X - 1; x++)
        {
            Assert.False(map[x, core.Y].BlocksBuilding, $"{label}: corridoio ({x}, {core.Y})");
        }

        // Copper within 12 cells of the Core.
        var copperNear = false;
        for (var y = core.Y - 12; y <= core.Y + 13; y++)
        {
            for (var x = core.X - 12; x <= core.X + 13; x++)
            {
                copperNear |= map.InBounds(new GridPosition(x, y)) && map[x, y].Deposit == DepositKind.Copper;
            }
        }

        Assert.True(copperNear, $"{label}: niente rame vicino");

        var reached = LandReachableDeposits(map, core);
        foreach (var ore in new[] { DepositKind.Iron, DepositKind.Copper, DepositKind.Coal })
        {
            Assert.True(reached.Contains(ore), $"{label}: {ore} non raggiungibile");
        }

        AssertShallowShore(map, label);
    }

    /// <summary>Deep water always has a shallow ring: no land or wall cell touches it.</summary>
    private static void AssertShallowShore(TerrainMap map, string label)
    {
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (map[x, y].Floor != FloorKind.DeepWater)
                {
                    continue;
                }

                foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (map.InBounds(new GridPosition(nx, ny)))
                    {
                        Assert.True(map[nx, ny].Floor is FloorKind.ShallowWater or FloorKind.DeepWater,
                            $"{label}: terra ({nx}, {ny}) accanto all'acqua profonda ({x}, {y})");
                    }
                }
            }
        }
    }

    [Fact]
    public void FallbackCoalNeverHidesTheStarterPatches()
    {
        // Core near the top edge: copper flips below the iron; no coal anywhere on the map.
        var settings = new MapGenSettings(64, 64, 21, ["sabbia"], DepositPercent: 0);
        var coreOrigin = new GridPosition(30, 61);
        var map = NewGame.Sandbox(Content, coreOrigin, map: settings).Terrain!;
        var iron = new GridPosition(26, 61);
        Assert.Equal(4, map.CountDepositTiles(iron, 2, DepositKind.Iron));
        Assert.Equal(4, map.CountDepositTiles(new GridPosition(26, 58), 2, DepositKind.Copper));
        var reached = LandReachableDeposits(map, coreOrigin);
        Assert.Contains(DepositKind.Coal, reached);
        Assert.Contains(DepositKind.Copper, reached);
    }

    [Fact]
    public void GuaranteeDigsToAnEnclosedOre()
    {
        // Grass 40×40, the Core at (20,20) walled in by a square ring of walls; coal only outside it.
        const int size = 40;
        var tiles = new TerrainTile[size, size];
        for (var x = 0; x < size; x++)
        {
            for (var y = 0; y < size; y++)
            {
                var ring = Math.Max(Math.Abs(x - 20), Math.Abs(y - 20)) == 12;
                tiles[x, y] = new TerrainTile(FloorKind.Grass, ring ? WallKind.StoneWall : WallKind.None, DepositKind.None);
            }
        }

        tiles[16, 20] = tiles[17, 20] = new TerrainTile(FloorKind.Stone, WallKind.None, DepositKind.Iron);
        tiles[16, 23] = new TerrainTile(FloorKind.Stone, WallKind.None, DepositKind.Copper);
        tiles[2, 30] = tiles[3, 30] = new TerrainTile(FloorKind.Stone, WallKind.None, DepositKind.Coal);
        // A deep river across the way, too.
        for (var y = 0; y < size; y++)
        {
            tiles[5, y] = new TerrainTile(FloorKind.DeepWater, WallKind.None, DepositKind.None);
        }

        var map = TerrainMap.FromTiles(tiles, generator: MapGenerator.Name);
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(20, 20), 2);
        Assert.DoesNotContain(DepositKind.Coal, LandReachableDeposits(map, new GridPosition(20, 20)));

        MapGenerator.EnsureLandPaths(map, core, new GridPosition(16, 20));

        var reached = LandReachableDeposits(map, new GridPosition(20, 20));
        Assert.Contains(DepositKind.Coal, reached);
        Assert.Contains(DepositKind.Iron, reached);
        Assert.Contains(DepositKind.Copper, reached);
        // L-shaped: along y = 20 to x = 3 (wall at (8,20) gone, river at (5,20) shallow), then up x = 3.
        Assert.Equal(WallKind.None, map[8, 20].Wall);
        Assert.Equal(FloorKind.ShallowWater, map[5, 20].Floor);
        Assert.Equal(WallKind.StoneWall, map[8, 21].Wall);
    }

    [Fact]
    public void NoDepositOnWallOrDeepWater()
    {
        foreach (var settings in new[] { Small(42), Small(9), new MapGenSettings(256, 256, 3, MapGenSettings.AllBiomes, 300, 300, 300) })
        {
            var (map, _) = Generate(settings);
            var deposits = 0;
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    var tile = map[x, y];
                    if (tile.Deposit == DepositKind.None)
                    {
                        continue;
                    }

                    deposits++;
                    Assert.Equal(WallKind.None, tile.Wall);
                    Assert.NotEqual(FloorKind.DeepWater, tile.Floor);
                }
            }

            Assert.True(deposits > 100, $"{settings}: {deposits} giacimenti");
        }
    }

    [Fact]
    public void MapHasWallsWaterAndEveryOre()
    {
        var (map, _) = Generate(MapGenSettings.Normal(MapSize.Media, 42));
        var walls = 0;
        var deep = 0;
        var ores = new int[6];
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var tile = map[x, y];
                walls += tile.Wall != WallKind.None ? 1 : 0;
                deep += tile.Floor == FloorKind.DeepWater ? 1 : 0;
                ores[(int)tile.Deposit]++;
            }
        }

        output.WriteLine($"pareti {walls}, acqua profonda {deep}, giacimenti {string.Join(" ", ores.Skip(1))}");
        Assert.InRange(walls, 20_000, 250_000);
        Assert.True(deep > 5_000);
        Assert.All(ores.Skip(1), count => Assert.True(count > 1_000));
    }

    [Fact]
    public void SettingsAreValidated()
    {
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(20, 20), 2);
        var starter = new GridPosition(16, 20);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MapGenerator.Generate(new MapGenSettings(2001, 100, 1, MapGenSettings.AllBiomes), core, starter));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MapGenerator.Generate(new MapGenSettings(100, 100, 1, MapGenSettings.AllBiomes, WaterPercent: -1), core, starter));
        Assert.Throws<ArgumentException>(() =>
            MapGenerator.Generate(new MapGenSettings(100, 100, 1, ["lava"]), core, starter));
        Assert.Throws<ArgumentException>(() =>
            MapGenerator.Generate(new MapGenSettings(100, 100, 1, []), core, starter));
        Assert.Equal(2000, Factory.MaxMapEdge);
        Assert.Equal(1500, MapGenSettings.Normal(MapSize.Grande, 1).Width);
    }

    [Fact]
    public void NoiseIsIntegerAndBounded()
    {
        Assert.Equal(Noise.Value2D(-5, 9, 3, 16), Noise.Value2D(-5, 9, 3, 16));
        for (var i = -300; i < 300; i += 7)
        {
            Assert.InRange(Noise.Value2D(i, i * 3, 99, 32), 0, Noise.Max);
            Assert.InRange(Noise.Fractal(i, -i, 99, 64, 3), 0, Noise.Max);
        }

        // Smooth: neighbours differ little at a large cell size.
        Assert.True(Math.Abs(Noise.Value2D(100, 100, 5, 64) - Noise.Value2D(101, 100, 5, 64)) < 3000);
    }

    [ReleaseFact]
    [Trait("Category", "Perf")]
    public void GenerationTimeBudget()
    {
        Generate(Small(1)); // warm-up (JIT)
        foreach (var (size, budget) in new[] { (MapSize.Media, 1500), (MapSize.Enorme, 6000) })
        {
            var watch = Stopwatch.StartNew();
            Generate(MapGenSettings.Normal(size, 42));
            watch.Stop();
            output.WriteLine($"{(int)size}²: {watch.ElapsedMilliseconds} ms (limite {budget} ms)");
            Assert.True(watch.ElapsedMilliseconds <= budget, $"{(int)size}²: {watch.ElapsedMilliseconds} ms");
        }
    }

    /// <summary>Writes colour previews to <c>.dev/g2a-task2/</c> (or the folder in TINDUSTRY_MAP_PREVIEW).</summary>
    [PreviewFact]
    public void WritePreviews()
    {
        var env = Environment.GetEnvironmentVariable("TINDUSTRY_MAP_PREVIEW")!;
        var dir = Directory.Exists(env) ? env : Path.Combine(RepoRoot(), ".dev", "g2a-task2");
        Directory.CreateDirectory(dir);
        foreach (var (settings, name) in new[]
        {
            (MapGenSettings.Normal(MapSize.Media, 42), "seme42-1000.png"),
            (Small(42), "seme42-256.png"),
            (MapGenSettings.Normal(MapSize.Media, 7), "seme7-1000.png")
        })
        {
            var map = Generate(settings).Map;
            var path = Path.Combine(dir, name);
            PngWriter.Write(path, map.Width, map.Height, Colours(map));
            output.WriteLine(path);
        }

        // The start of seed 42 at 1000², 160×160 cells around the Core, 4× larger.
        var (big, core) = Generate(MapGenSettings.Normal(MapSize.Media, 42));
        var full = Colours(big);
        const int crop = 160, zoom = 4;
        var rgb = new byte[crop * zoom * crop * zoom * 3];
        for (var y = 0; y < crop * zoom; y++)
        {
            for (var x = 0; x < crop * zoom; x++)
            {
                var source = ((core.Y - crop / 2 + y / zoom) * big.Width + core.X - crop / 2 + x / zoom) * 3;
                Array.Copy(full, source, rgb, (y * crop * zoom + x) * 3, 3);
            }
        }

        PngWriter.Write(Path.Combine(dir, "seme42-partenza.png"), crop * zoom, crop * zoom, rgb);

        // Same map with the walls in magenta, to judge their shapes.
        PngWriter.Write(Path.Combine(dir, "seme42-1000-pareti.png"), big.Width, big.Height, Colours(big, markWalls: true));
    }

    private static byte[] Colours(TerrainMap map, bool markWalls = false)
    {
        var rgb = new byte[map.Width * map.Height * 3];
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var tile = map[x, y];
                var hex = tile.Deposit switch
                {
                    DepositKind.Iron => "#C46A4A",
                    DepositKind.Copper => "#E09A3A",
                    DepositKind.Coal => "#B0FF40", // preview only: stone walls are near-black
                    DepositKind.Lead => "#8A6AB8",
                    DepositKind.Titanium => "#7AE0F0",
                    _ => tile.Wall != WallKind.None
                        ? markWalls ? "#E0206A" : Content.Terrain.Wall(tile.Wall)!.Color
                        : Content.Terrain.Floor(tile.Floor)!.Color
                };
                var i = (y * map.Width + x) * 3;
                rgb[i] = Convert.ToByte(hex.Substring(1, 2), 16);
                rgb[i + 1] = Convert.ToByte(hex.Substring(3, 2), 16);
                rgb[i + 2] = Convert.ToByte(hex.Substring(5, 2), 16);
            }
        }

        return rgb;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tIndustry.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("tIndustry.sln non trovato");
    }

    /// <summary>Biome of each cell from its floor (content.json table): counts per biome id.</summary>
    private static Dictionary<string, int> BiomeCounts(TerrainMap map)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var biome = Content.Terrain.Floor(map[x, y].Floor)!.Biome;
                counts[biome] = counts.GetValueOrDefault(biome) + 1;
            }
        }

        return counts;
    }

    /// <summary>Deposit kinds reachable from the Core over cells that do not block building (4 neighbours).</summary>
    private static HashSet<DepositKind> LandReachableDeposits(TerrainMap map, GridPosition core)
    {
        var seen = new bool[map.Width, map.Height];
        var queue = new Queue<GridPosition>([core]);
        seen[core.X, core.Y] = true;
        var found = new HashSet<DepositKind>();
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            found.Add(map[p].Deposit);
            foreach (var n in new[] { new GridPosition(p.X + 1, p.Y), new GridPosition(p.X - 1, p.Y), new GridPosition(p.X, p.Y + 1), new GridPosition(p.X, p.Y - 1) })
            {
                if (map.InBounds(n) && !seen[n.X, n.Y] && !map[n].BlocksBuilding)
                {
                    seen[n.X, n.Y] = true;
                    queue.Enqueue(n);
                }
            }
        }

        return found;
    }
}
