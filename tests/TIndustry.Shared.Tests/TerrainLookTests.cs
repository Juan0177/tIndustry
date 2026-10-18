using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The terrain art choices of the client (<see cref="TerrainLook"/>): variants, edges, dual-grid walls, deposits and
/// decorations, checked on their own, across chunk seams and against the Python reference (tools/art/tavola.py).
/// </summary>
public sealed class TerrainLookTests
{
    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly TerrainLook Look = new(Content.Terrain);

    private static TerrainMap Fill(int w, int h, Func<int, int, TerrainTile> cell)
    {
        var tiles = new TerrainTile[w, h];
        for (var x = 0; x < w; x++)
        {
            for (var y = 0; y < h; y++)
            {
                tiles[x, y] = cell(x, y);
            }
        }

        return TerrainMap.FromTiles(tiles);
    }

    private static TerrainTile Floor(FloorKind f) => new(f, WallKind.None, DepositKind.None);

    /// <summary>A mixed map from a hash of its own: every floor, wall and deposit, in blobs and single cells.</summary>
    private static TerrainMap Mixed(int w, int h)
    {
        var floors = Enum.GetValues<FloorKind>();
        var walls = Enum.GetValues<WallKind>().Where(k => k != WallKind.None).ToArray();
        var deposits = Enum.GetValues<DepositKind>().Where(k => k != DepositKind.None).ToArray();
        return Fill(w, h, (x, y) =>
        {
            var floor = floors[TerrainLook.CellHash(x / 3, y / 4, 901) % (uint)floors.Length];
            if (TerrainLook.CellHash(x, y, 902) % 6 == 0)
            {
                floor = floors[TerrainLook.CellHash(x, y, 903) % (uint)floors.Length];
            }

            var wall = TerrainLook.CellHash(x / 2, y / 2, 904) % 5 == 0
                ? walls[TerrainLook.CellHash(x / 5, y / 5, 905) % (uint)walls.Length]
                : WallKind.None;
            var deposit = TerrainLook.CellHash(x, y, 906) % 8 == 0
                ? deposits[TerrainLook.CellHash(x, y, 907) % (uint)deposits.Length]
                : DepositKind.None;
            return new TerrainTile(floor, wall, deposit);
        });
    }

    [Fact]
    public void VariantIsDeterministicAndSpread()
    {
        var counts = new int[TerrainLook.Variants];
        var sameAsRight = 0;
        for (var y = -32; y < 32; y++)
        {
            for (var x = -32; x < 32; x++)
            {
                var v = TerrainLook.Variant(x, y);
                Assert.InRange(v, 0, TerrainLook.Variants - 1);
                Assert.Equal(v, TerrainLook.Variant(x, y));
                counts[v]++;
                if (v == TerrainLook.Variant(x + 1, y))
                {
                    sameAsRight++;
                }
            }
        }

        // 4096 cells: each variant near a quarter, and neighbours no more alike than chance.
        Assert.All(counts, c => Assert.InRange(c, 4096 / 4 - 160, 4096 / 4 + 160));
        Assert.InRange(sameAsRight, 4096 / 4 - 160, 4096 / 4 + 160);
        // Pinned values: the same look on every platform and load (same as hash_cella in Python).
        Assert.Equal(1040418471u, TerrainLook.CellHash(3, -7, 1));
        Assert.Equal(2861530548u, TerrainLook.CellHash(12345, -678, 4));
        Assert.Equal(3306091032u, TerrainLook.CellHash(-1, -1, 2));
        Assert.Equal(TerrainLook.CellHash(3, -7, 1) % 4, (uint)TerrainLook.Variant(3, -7));
    }

    [Fact]
    public void WallMaskMatchesCorners()
    {
        // Stone walls at (1,1), (2,1), (2,2); a sand wall at (0,3).
        var map = Fill(4, 4, (x, y) => (x, y) switch
        {
            (1, 1) or (2, 1) or (2, 2) => new TerrainTile(FloorKind.Stone, WallKind.StoneWall, DepositKind.None),
            (0, 3) => new TerrainTile(FloorKind.Sand, WallKind.SandWall, DepositKind.None),
            _ => Floor(FloorKind.Stone)
        });
        const int no = 1, ne = 2, se = 4, so = 8;
        Assert.Equal(se, TerrainLook.WallMask(map, WallKind.StoneWall, 1, 1));
        Assert.Equal(so | se, TerrainLook.WallMask(map, WallKind.StoneWall, 2, 1));
        Assert.Equal(no | ne | se, TerrainLook.WallMask(map, WallKind.StoneWall, 2, 2));
        Assert.Equal(no, TerrainLook.WallMask(map, WallKind.StoneWall, 3, 3));
        Assert.Equal(so, TerrainLook.WallMask(map, WallKind.StoneWall, 3, 1));
        Assert.Equal(0, TerrainLook.WallMask(map, WallKind.StoneWall, 0, 0));
        // Each wall kind on its own; corners on the map border see no wall outside the map.
        Assert.Equal(0, TerrainLook.WallMask(map, WallKind.SandWall, 2, 2));
        Assert.Equal(ne, TerrainLook.WallMask(map, WallKind.SandWall, 0, 4));
        Assert.Equal(se, TerrainLook.WallMask(map, WallKind.SandWall, 0, 3));
        Assert.Equal(no, TerrainLook.WallMask(map, WallKind.SandWall, 1, 4));
        Assert.Equal(0, TerrainLook.WallMask(map, WallKind.SandWall, -1, -1));
    }

    [Fact]
    public void EdgesComeFromHigherPriorityNeighboursOnly()
    {
        var priority = Content.Terrain.Floors.ToDictionary(f => f.Kind, f => f.EdgePriority);
        var order = priority.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToArray();
        FloorKind low = order[2], other = order[^2], high = order[^1];
        const int n = 0, e = 1, ne = 4;

        List<(FloorKind, int)> Edges(params (int Dx, int Dy, FloorKind Floor)[] around)
        {
            var map = Fill(3, 3, (x, y) =>
                Floor(around.Where(a => a.Dx == x - 1 && a.Dy == y - 1).Select(a => a.Floor).DefaultIfEmpty(low).First()));
            return TerrainLook.EdgesOn(map, 1, 1, priority).ToList();
        }

        Assert.Empty(Edges());
        Assert.Equal([(high, ne)], Edges((1, -1, high)));
        // A side of the same floor already covers the corner.
        Assert.Equal([(high, n)], Edges((0, -1, high), (1, -1, high)));
        Assert.Equal([(high, n), (high, e)], Edges((0, -1, high), (1, 0, high), (1, -1, high)));
        // Lowest priority first, so the highest floor ends on top.
        Assert.Equal([(other, n), (high, ne)], Edges((0, -1, other), (1, -1, high)));
        Assert.Equal([(other, e), (high, n)], Edges((0, -1, high), (1, 0, other)));
        // A lower or equal neighbour draws nothing on a higher cell.
        var top = Fill(3, 3, (x, y) => Floor(x == 1 && y == 1 ? high : other));
        Assert.Empty(TerrainLook.EdgesOn(top, 1, 1, priority));
        // Past the map border the neighbour is the cell itself: no edge.
        var corner = Fill(2, 2, (x, y) => Floor(x == 0 && y == 0 ? low : low));
        Assert.Empty(TerrainLook.EdgesOn(corner, 0, 0, priority));
    }

    [Fact]
    public void DecorationNeverOnWallsWaterOrDeposits()
    {
        var map = Mixed(80, 80);
        var water = Content.Terrain.Floors.Where(f => f.Biome == "acqua").Select(f => f.Kind).ToHashSet();
        var decorated = 0;
        var looks = new HashSet<int>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (Look.Decoration(map, x, y) is not { } k)
                {
                    continue;
                }

                var cell = map[x, y];
                Assert.Equal(WallKind.None, cell.Wall);
                Assert.Equal(DepositKind.None, cell.Deposit);
                Assert.DoesNotContain(cell.Floor, water);
                Assert.InRange(k, 0, TerrainLook.DecorationLooks - 1);
                Assert.Equal(k, Look.Decoration(map, x, y));
                looks.Add(k);
                decorated++;
            }
        }

        Assert.InRange(decorated, 100, 800);
        Assert.Equal(TerrainLook.DecorationLooks, looks.Count);
    }

    [Fact]
    public void ChunkSeamsMatch()
    {
        // Drawn chunk by chunk (each clipped to its 16×16 cells) the map looks exactly as drawn in one go.
        var map = Mixed(40, 37);
        const int unit = TerrainLook.CellPixels / 2; // every piece starts on a half-cell
        var wu = map.Width * 2;
        var hu = map.Height * 2;
        var whole = new ulong[wu * hu];
        var chunked = new ulong[wu * hu];
        var pieces = new List<TerrainPiece>();
        Look.Plan(map, 0, 0, map.Width, map.Height, pieces);
        Paint(whole, pieces, 0, 0, wu, hu);
        for (var cy = 0; cy * 16 < map.Height; cy++)
        {
            for (var cx = 0; cx * 16 < map.Width; cx++)
            {
                var w = Math.Min(16, map.Width - cx * 16);
                var h = Math.Min(16, map.Height - cy * 16);
                pieces.Clear();
                Look.Plan(map, cx * 16, cy * 16, w, h, pieces);
                Paint(chunked, pieces, cx * 32, cy * 32, w * 2, h * 2);
            }
        }

        Assert.Equal(whole, chunked);
        return;

        // Each half-cell unit keeps a running hash of the pieces drawn over it, in order, clipped to the window.
        void Paint(ulong[] raster, List<TerrainPiece> list, int ux0, int uy0, int uw, int uh)
        {
            foreach (var p in list)
            {
                var id = (ulong)(uint)StringComparer.Ordinal.GetHashCode(p.Sprite) * 31 + (ulong)p.Index;
                for (var dy = 0; dy < 2; dy++)
                {
                    for (var dx = 0; dx < 2; dx++)
                    {
                        var ux = p.Px / unit + dx;
                        var uy = p.Py / unit + dy;
                        if (ux < ux0 || uy < uy0 || ux >= ux0 + uw || uy >= uy0 + uh || ux >= wu || uy >= hu)
                        {
                            continue;
                        }

                        ref var v = ref raster[uy * wu + ux];
                        v = v * 1099511628211UL ^ (id * 4 + (ulong)(dy * 2 + dx));
                    }
                }
            }
        }
    }

    [Fact]
    public void MatchesPythonReference()
    {
        // Fixture written by tools/art/fixture_terreno.py from tavola.pezzi_mappa (the reference composition).
        using var doc = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "terrain-look-24.json")));
        var root = doc.RootElement;
        var size = root.GetProperty("size").GetInt32();
        var floors = Content.Terrain.Floors.ToDictionary(f => f.Id, f => f.Kind);
        var walls = Content.Terrain.Walls.ToDictionary(w => w.Id, w => w.Kind);
        var ores = Enum.GetValues<DepositKind>().Where(d => d != DepositKind.None)
            .ToDictionary(TerrainMap.ItemIdForDeposit);
        var rows = root.GetProperty("cells").EnumerateArray().Select(r => r.EnumerateArray().ToArray()).ToArray();
        var map = Fill(size, size, (x, y) =>
        {
            var c = rows[y][x].EnumerateArray().ToArray();
            return new TerrainTile(
                floors[c[0].GetString()!],
                c[1].GetString() is { } wall ? walls[wall] : WallKind.None,
                c[2].GetString() is { } ore ? ores[ore] : DepositKind.None);
        });
        var expected = root.GetProperty("pieces").EnumerateArray()
            .Select(p => p.EnumerateArray().ToArray())
            .Select(p => $"{p[0].GetString()} {p[1].GetInt32()} {p[2].GetInt32()} {p[3].GetInt32()}")
            .ToList();
        var pieces = new List<TerrainPiece>();
        Look.Plan(map, 0, 0, size, size, pieces);
        var actual = pieces.Select(p => $"{p.Sprite} {p.Index} {p.Px} {p.Py}").ToList();
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.True(expected[i] == actual[i], $"pezzo {i}: atteso '{expected[i]}', ottenuto '{actual[i]}'");
        }
    }
}
