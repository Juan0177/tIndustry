namespace TIndustry.Shared;

/// <summary>
/// Integer value noise for the map generator: every result is 0..65535 and comes from integer
/// arithmetic only, so a seed gives the same map on every platform and runtime.
/// </summary>
/// <remarks>
/// <see cref="Value2D"/> hashes the four lattice corners of the cell holding (x, y) and blends them with a
/// fixed-point smoothstep (16 fractional bits). <see cref="Fractal"/> sums octaves of it, halving the cell
/// and the weight each time, with a different seed and offset per octave so the lattices never line up.
/// </remarks>
public static class Noise
{
    public const int Max = 65535;

    /// <summary>Hash of a lattice point, 0..65535.</summary>
    public static int Hash(int x, int y, int seed)
    {
        unchecked
        {
            var h = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ (uint)seed * 0xCB1AB31Fu;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return (int)(h >> 16);
        }
    }

    /// <summary>Smooth value noise with lattice cells of <paramref name="cellSize"/> map cells, 0..65535.</summary>
    public static int Value2D(int x, int y, int seed, int cellSize)
    {
        if (cellSize <= 1)
        {
            return Hash(x, y, seed);
        }

        var cx = FloorDiv(x, cellSize);
        var cy = FloorDiv(y, cellSize);
        var sx = Smooth((x - cx * cellSize) * 65536 / cellSize);
        var sy = Smooth((y - cy * cellSize) * 65536 / cellSize);
        var a = Hash(cx, cy, seed);
        var b = Hash(cx + 1, cy, seed);
        var c = Hash(cx, cy + 1, seed);
        var d = Hash(cx + 1, cy + 1, seed);
        var top = a + (int)(((long)(b - a) * sx) >> 16);
        var bottom = c + (int)(((long)(d - c) * sx) >> 16);
        return top + (int)(((long)(bottom - top) * sy) >> 16);
    }

    /// <summary>
    /// <paramref name="octaves"/> octaves of <see cref="Value2D"/> from <paramref name="baseCell"/> down,
    /// each with half the cell and half the weight; 0..65535.
    /// </summary>
    public static int Fractal(int x, int y, int seed, int baseCell, int octaves)
    {
        long total = 0;
        long weights = 0;
        var cell = baseCell;
        var weight = 1 << Math.Max(0, octaves - 1);
        for (var o = 0; o < octaves; o++)
        {
            unchecked
            {
                var offset = o * 7919;
                total += (long)Value2D(x + offset, y - offset, seed + o * 1013904223, Math.Max(1, cell)) * weight;
            }

            weights += weight;
            cell >>= 1;
            weight >>= 1;
        }

        return (int)(total / Math.Max(1, weights));
    }

    // t in 0..65536 (16 fractional bits) -> 3t² - 2t³, same scale.
    private static int Smooth(int t) => (int)(((long)t * t * (3L * 65536 - 2L * t)) >> 32);

    private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
}
