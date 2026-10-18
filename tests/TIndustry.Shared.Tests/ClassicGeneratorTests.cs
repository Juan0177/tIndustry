using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The classic generator stays byte for byte what it was before G2a (v11 saves regenerate their terrain
/// with it): the fingerprints below were taken on the pre-G2a code, classic water = shallow water.
/// </summary>
public sealed class ClassicGeneratorTests
{
    private static HashSet<GridPosition> Core(int x, int y) =>
        [new(x, y), new(x + 1, y), new(x, y + 1), new(x + 1, y + 1)];

    // FNV-1a 64 over (floor, wall, deposit) of every cell, rows from y = 0.
    private static ulong Fingerprint(TerrainMap map)
    {
        var hash = 14695981039346656037UL;
        void Add(byte b)
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var tile = map[x, y];
                Add((byte)tile.Floor);
                Add((byte)tile.Wall);
                Add((byte)tile.Deposit);
            }
        }

        return hash;
    }

    [Fact]
    public void ClassicGeneratorIsUnchanged()
    {
        var small = TerrainMap.Generate(120, 120, 7, Core(60, 60), new GridPosition(56, 60));
        Assert.Equal(0x41CCA7DA8FF693B8UL, Fingerprint(small));
        Assert.Equal("classico", small.Generator);

        // >= 256 wide: the other ore threshold and the scout patches.
        var large = TerrainMap.Generate(300, 260, 42, Core(150, 130), new GridPosition(146, 130));
        Assert.Equal(0x0C60EB1DF401E169UL, Fingerprint(large));
    }

    [Fact]
    public void ClassicMapHasNoWallsNorDeepWater()
    {
        var map = ClassicTerrainGenerator.Generate(300, 260, 42, Core(150, 130), new GridPosition(146, 130));
        var floors = new HashSet<FloorKind>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                Assert.False(map[x, y].BlocksBuilding);
                floors.Add(map[x, y].Floor);
            }
        }

        Assert.Equal(
            new[] { FloorKind.Stone, FloorKind.Grass, FloorKind.Soil, FloorKind.ShallowWater },
            floors.Order().ToArray());
    }
}
