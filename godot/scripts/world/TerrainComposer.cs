using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// The terrain sprite strips as raw RGBA bytes, loaded once per <see cref="TerrainLook"/> (the composer blits from
/// them without touching Godot images). A missing PNG is the magenta placeholder of <see cref="Sprites.For"/>.
/// </summary>
public sealed class TerrainSprites
{
    private readonly Dictionary<string, Strip> _strips = new(StringComparer.Ordinal);

    public TerrainSprites(IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            Get(name);
        }
    }

    /// <summary>RGBA8 pixels of strip <paramref name="name"/> (pieces of 32×32 side by side).</summary>
    public Strip Get(string name)
    {
        if (_strips.TryGetValue(name, out var strip))
        {
            return strip;
        }

        using var image = Sprites.For(name).GetImage();
        if (image.IsCompressed())
        {
            image.Decompress();
        }

        image.Convert(Image.Format.Rgba8);
        strip = new Strip(image.GetData(), image.GetWidth(), image.GetHeight());
        _strips[name] = strip;
        return strip;
    }

    public sealed record Strip(byte[] Rgba, int Width, int Height);
}

/// <summary>
/// Composes the art of one terrain chunk (<see cref="TerrainChunks.ArtChunkCells"/> cells a side, 32 px per cell)
/// from the pieces of <see cref="TerrainLook.Plan"/>: opaque floor and water beds, then edges, deposits, decorations
/// and walls alpha-blended in order, clipped to the chunk. The alpha channel of the result is the water mask
/// (255 where the water bed is uncovered, less where a piece covers it partly, 0 on land) for <c>water.gdshader</c>;
/// the colour is always opaque terrain. Main thread only (it reuses its buffers).
/// </summary>
public static class TerrainComposer
{
    private const int T = TerrainLook.CellPixels;

    private static readonly List<TerrainPiece> Pieces = [];
    private static byte[] _rgba = [];
    private static byte[] _mask = [];

    /// <summary>Image (RGBA8, 32 px per cell, edge chunks cut to the map) of chunk (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
    public static Image Compose(TerrainLook look, TerrainMap map, int cx, int cy, TerrainSprites sprites)
    {
        const int cells = TerrainChunks.ArtChunkCells;
        var x0 = cx * cells;
        var y0 = cy * cells;
        var w = Math.Min(cells, map.Width - x0);
        var h = Math.Min(cells, map.Height - y0);
        var pw = w * T;
        var ph = h * T;
        if (_rgba.Length == 0)
        {
            // Sized for a whole chunk once; edge chunks use the start of it.
            _rgba = new byte[cells * T * cells * T * 4];
            _mask = new byte[cells * T * cells * T];
        }

        Pieces.Clear();
        look.Plan(map, x0, y0, w, h, Pieces);
        var ox = x0 * T;
        var oy = y0 * T;
        foreach (var piece in Pieces)
        {
            var strip = sprites.Get(piece.Sprite);
            switch (piece.Kind)
            {
                case TerrainPieceKind.Floor:
                case TerrainPieceKind.Water:
                    CopyOpaque(strip, piece, piece.Px - ox, piece.Py - oy, pw, piece.Kind == TerrainPieceKind.Water);
                    break;
                default:
                    Blend(strip, piece, piece.Px - ox, piece.Py - oy, pw, ph);
                    break;
            }
        }

        // The mask goes into the alpha channel of the texture.
        var n = pw * ph;
        for (var i = 0; i < n; i++)
        {
            _rgba[i * 4 + 3] = _mask[i];
        }

        var bytes = _rgba.Length == n * 4 ? _rgba : _rgba.AsSpan(0, n * 4).ToArray();
        return Image.CreateFromData(pw, ph, false, Image.Format.Rgba8, bytes);
    }

    /// <summary>A floor or water bed: always a whole cell inside the chunk.</summary>
    private static void CopyOpaque(TerrainSprites.Strip strip, TerrainPiece piece, int dx, int dy, int pw, bool water)
    {
        var sx = piece.Index * T;
        var size = Math.Min(T, strip.Height);
        for (var y = 0; y < size; y++)
        {
            var src = (y * strip.Width + sx) * 4;
            var dst = (dy + y) * pw + dx;
            Buffer.BlockCopy(strip.Rgba, src, _rgba, dst * 4, Math.Min(T, strip.Width - sx) * 4);
            _mask.AsSpan(dst, T).Fill(water ? (byte)255 : (byte)0);
        }
    }

    /// <summary>Source-over blend of a piece, clipped to the chunk; it covers the water mask by its alpha.</summary>
    private static void Blend(TerrainSprites.Strip strip, TerrainPiece piece, int dx, int dy, int pw, int ph)
    {
        var sx = piece.Index * T;
        if (sx + T > strip.Width)
        {
            return; // a strip shorter than the piece index (placeholder): nothing to draw
        }

        var xa = Math.Max(0, -dx);
        var xb = Math.Min(T, pw - dx);
        var ya = Math.Max(0, -dy);
        var yb = Math.Min(Math.Min(T, strip.Height), ph - dy);
        var src = strip.Rgba;
        for (var y = ya; y < yb; y++)
        {
            var s = (y * strip.Width + sx + xa) * 4;
            var d = (dy + y) * pw + dx + xa;
            for (var x = xa; x < xb; x++, s += 4, d++)
            {
                int a = src[s + 3];
                if (a == 0)
                {
                    continue;
                }

                var di = d * 4;
                if (a == 255)
                {
                    _rgba[di] = src[s];
                    _rgba[di + 1] = src[s + 1];
                    _rgba[di + 2] = src[s + 2];
                    _mask[d] = 0;
                    continue;
                }

                var inv = 255 - a;
                _rgba[di] = (byte)((src[s] * a + _rgba[di] * inv + 127) / 255);
                _rgba[di + 1] = (byte)((src[s + 1] * a + _rgba[di + 1] * inv + 127) / 255);
                _rgba[di + 2] = (byte)((src[s + 2] * a + _rgba[di + 2] * inv + 127) / 255);
                _mask[d] = (byte)((_mask[d] * inv + 127) / 255);
            }
        }
    }
}
