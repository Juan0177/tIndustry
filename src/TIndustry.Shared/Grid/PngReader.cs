using System.IO.Compression;

namespace TIndustry.Shared;

/// <summary>
/// Minimal PNG decoder for the campaign maps: 8-bit RGB or RGBA, not interlaced, filters 0–4, any number of
/// IDAT chunks. Ancillary chunks are skipped; every chunk CRC is checked. Meant for untrusted files: sizes are
/// checked before anything is allocated, and every failure is an <see cref="InvalidDataException"/> in Italian.
/// </summary>
public static class PngReader
{
    internal static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Default longest edge accepted: the map limit (<see cref="Factory.MaxMapEdge"/>).</summary>
    public const int DefaultMaxEdge = Factory.MaxMapEdge;

    /// <summary>Longest chunk accepted.</summary>
    public const int MaxChunkBytes = 64 << 20;

    /// <summary>
    /// Most compressed image data (all IDAT chunks together) accepted for an image whose filtered rows take
    /// <paramref name="rawBytes"/> bytes: even a stored (uncompressed) zlib stream or fixed Huffman codes
    /// (at most 9 bits per byte) stay under one eighth more, plus a margin for headers and block boundaries.
    /// </summary>
    public static long MaxImageDataBytes(long rawBytes) => rawBytes + rawBytes / 8 + 1024;

    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>
    /// Decodes <paramref name="s"/>; pixels are RGBA, row by row (alpha 255 for RGB images). Images with an edge
    /// over <paramref name="maxEdge"/> are refused from the header, before any pixel memory is allocated.
    /// </summary>
    /// <exception cref="InvalidDataException">The stream is not a PNG this reader supports.</exception>
    public static (int Width, int Height, byte[] Rgba) Read(Stream s, int maxEdge = DefaultMaxEdge)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEdge, 1);
        var signature = ReadExactly(s, 8, "firma");
        if (!signature.AsSpan().SequenceEqual(Signature))
        {
            throw new InvalidDataException("Non è un file PNG (firma errata).");
        }

        var width = 0;
        var height = 0;
        var channels = 0;
        var maxImageData = 0L;
        var sawHeader = false;
        using var compressed = new MemoryStream();
        while (true)
        {
            var length = ReadUInt32(ReadExactly(s, 4, "lunghezza del blocco"), 0);
            if (length > MaxChunkBytes)
            {
                throw new InvalidDataException($"PNG: blocco di {length} byte, oltre il massimo di {MaxChunkBytes}.");
            }

            if (s.CanSeek && length + 8L > s.Length - s.Position)
            {
                throw new InvalidDataException("PNG troncato (blocco più lungo del file).");
            }

            var typeAndData = ReadExactly(s, 4 + (int)length, "blocco");
            var crc = ReadUInt32(ReadExactly(s, 4, "CRC"), 0);
            if (Crc(typeAndData) != crc)
            {
                throw new InvalidDataException("PNG: CRC errato, file danneggiato.");
            }

            var type = System.Text.Encoding.ASCII.GetString(typeAndData, 0, 4);
            var data = typeAndData.AsSpan(4);
            if (!sawHeader && type != "IHDR")
            {
                throw new InvalidDataException("PNG: il primo blocco deve essere IHDR.");
            }

            switch (type)
            {
                case "IHDR":
                    if (sawHeader)
                    {
                        throw new InvalidDataException("PNG: intestazione IHDR ripetuta.");
                    }

                    (width, height, channels) = ReadHeader(data, maxEdge);
                    maxImageData = MaxImageDataBytes((long)height * (width * channels + 1));
                    sawHeader = true;
                    break;
                case "IDAT":
                    if (compressed.Length + data.Length > maxImageData)
                    {
                        throw new InvalidDataException(
                            $"PNG: dati dell'immagine oltre {maxImageData} byte, troppi per {width}×{height}.");
                    }

                    compressed.Write(data);
                    break;
                case "IEND":
                    if (s.ReadByte() >= 0)
                    {
                        throw new InvalidDataException("PNG: dati dopo la fine dell'immagine (IEND).");
                    }

                    return (width, height, Decode(compressed.ToArray(), width, height, channels));
                case "PLTE":
                    // Optional suggested palette of a truecolour image: not needed.
                    break;
                default:
                    if (char.IsUpper(type[0]))
                    {
                        throw new InvalidDataException($"PNG: blocco critico '{type}' non supportato.");
                    }

                    break;
            }
        }
    }

    private static (int Width, int Height, int Channels) ReadHeader(ReadOnlySpan<byte> h, int maxEdge)
    {
        if (h.Length != 13)
        {
            throw new InvalidDataException("PNG: intestazione IHDR non valida.");
        }

        var width = ReadUInt32(h, 0);
        var height = ReadUInt32(h, 4);
        if (width == 0 || height == 0 || width > maxEdge || height > maxEdge)
        {
            throw new InvalidDataException($"PNG: misura {width}×{height} non valida (lato da 1 a {maxEdge}).");
        }

        if (h[8] != 8)
        {
            throw new InvalidDataException($"PNG: profondità {h[8]} bit non supportata (serve 8 bit per canale).");
        }

        var channels = h[9] switch
        {
            2 => 3,
            6 => 4,
            _ => throw new InvalidDataException(
                $"PNG: tipo di colore {h[9]} non supportato (serve RGB o RGBA, senza tavolozza).")
        };
        if (h[10] != 0 || h[11] != 0)
        {
            throw new InvalidDataException("PNG: metodo di compressione o di filtro sconosciuto.");
        }

        if (h[12] != 0)
        {
            throw new InvalidDataException("PNG interlacciato non supportato: salvalo senza interlacciamento.");
        }

        return ((int)width, (int)height, channels);
    }

    private static byte[] Decode(byte[] compressed, int width, int height, int channels)
    {
        var stride = width * channels;
        var raw = new byte[(long)height * (stride + 1)];
        using (var z = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress))
        {
            var read = 0;
            try
            {
                while (read < raw.Length)
                {
                    var n = z.Read(raw, read, raw.Length - read);
                    if (n == 0)
                    {
                        break;
                    }

                    read += n;
                }
            }
            catch (InvalidDataException)
            {
                throw new InvalidDataException("PNG: dati compressi danneggiati.");
            }

            if (read < raw.Length)
            {
                throw new InvalidDataException("PNG: dati dell'immagine incompleti.");
            }

            // Data past the last row means the file does not match its header: refuse it rather than guess.
            bool surplus;
            try
            {
                surplus = z.ReadByte() >= 0;
            }
            catch (InvalidDataException)
            {
                throw new InvalidDataException("PNG: dati compressi danneggiati.");
            }

            if (surplus)
            {
                throw new InvalidDataException("PNG: dati dell'immagine in eccesso rispetto alla misura.");
            }
        }

        var rgba = new byte[width * height * 4];
        var previous = new byte[stride];
        var current = new byte[stride];
        for (var y = 0; y < height; y++)
        {
            var offset = y * (stride + 1);
            var filter = raw[offset];
            Array.Copy(raw, offset + 1, current, 0, stride);
            Unfilter(filter, current, previous, channels, y);
            for (var x = 0; x < width; x++)
            {
                var o = (y * width + x) * 4;
                rgba[o] = current[x * channels];
                rgba[o + 1] = current[x * channels + 1];
                rgba[o + 2] = current[x * channels + 2];
                rgba[o + 3] = channels == 4 ? current[x * channels + 3] : (byte)255;
            }

            (previous, current) = (current, previous);
        }

        return rgba;
    }

    private static void Unfilter(byte filter, byte[] row, byte[] up, int bpp, int y)
    {
        for (var i = 0; i < row.Length; i++)
        {
            int a = i >= bpp ? row[i - bpp] : 0;
            int b = up[i];
            int c = i >= bpp ? up[i - bpp] : 0;
            var predictor = filter switch
            {
                0 => 0,
                1 => a,
                2 => b,
                3 => (a + b) / 2,
                4 => Paeth(a, b, c),
                _ => throw new InvalidDataException($"PNG: filtro {filter} sconosciuto alla riga {y}.")
            };
            row[i] = (byte)(row[i] + predictor);
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static byte[] ReadExactly(Stream s, int count, string what)
    {
        var buffer = new byte[count];
        var read = 0;
        while (read < count)
        {
            var n = s.Read(buffer, read, count - read);
            if (n == 0)
            {
                throw new InvalidDataException($"PNG troncato (manca: {what}).");
            }

            read += n;
        }

        return buffer;
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> b, int offset) =>
        (uint)(b[offset] << 24 | b[offset + 1] << 16 | b[offset + 2] << 8 | b[offset + 3]);

    /// <summary>CRC-32 of a chunk's type and data, as PNG stores it.</summary>
    internal static uint Crc(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (var n = 0u; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
