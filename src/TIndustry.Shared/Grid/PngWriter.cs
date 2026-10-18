using System.IO.Compression;
using System.Text;

namespace TIndustry.Shared;

/// <summary>
/// Minimal PNG encoder (8-bit RGB or RGBA, one IDAT, filter 0) for the campaign map files and previews;
/// <see cref="PngReader"/> reads what it writes.
/// </summary>
public static class PngWriter
{
    /// <summary>Writes <paramref name="rgb"/> (3 bytes per pixel, row by row) as an RGB PNG.</summary>
    public static void Write(string path, int width, int height, byte[] rgb) => Write(path, width, height, rgb, 3);

    /// <summary>Writes <paramref name="rgba"/> (4 bytes per pixel, row by row) as an RGBA PNG.</summary>
    public static void WriteRgba(string path, int width, int height, byte[] rgba) => Write(path, width, height, rgba, 4);

    private static void Write(string path, int width, int height, byte[] pixels, int channels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0 || height <= 0 || pixels.Length != width * height * channels)
        {
            throw new ArgumentException($"PNG {width}×{height}: servono {width * height * channels} byte di pixel.");
        }

        using var file = File.Create(path);
        file.Write(PngReader.Signature);

        var header = new byte[13];
        BigEndian(header, 0, (uint)width);
        BigEndian(header, 4, (uint)height);
        header[8] = 8; // bit depth
        header[9] = (byte)(channels == 4 ? 6 : 2); // colour type RGBA or RGB
        Chunk(file, "IHDR", header);

        using var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            for (var y = 0; y < height; y++)
            {
                z.WriteByte(0);
                z.Write(pixels, y * width * channels, width * channels);
            }
        }

        Chunk(file, "IDAT", raw.ToArray());
        Chunk(file, "IEND", []);
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var buffer = new byte[4];
        BigEndian(buffer, 0, (uint)data.Length);
        s.Write(buffer);
        var body = new byte[4 + data.Length];
        Encoding.ASCII.GetBytes(type, 0, 4, body, 0);
        data.CopyTo(body, 4);
        s.Write(body);
        BigEndian(buffer, 0, PngReader.Crc(body));
        s.Write(buffer);
    }

    private static void BigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
