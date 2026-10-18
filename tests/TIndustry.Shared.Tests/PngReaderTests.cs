using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>The PNG decoder against fixtures written by Fixtures/png/make_fixtures.py (checked with Pillow).</summary>
public sealed class PngReaderTests
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "png");

    private const int Width = 7;
    private const int Height = 5;

    /// <summary>The fixture pattern (same formula as make_fixtures.py).</summary>
    private static (byte R, byte G, byte B, byte A) Expected(int x, int y) =>
        ((byte)((x * 37 + y * 11) & 255), (byte)((x * 5 + y * 71) & 255), (byte)(((x ^ y) * 13) & 255),
            (byte)((x * y * 9 + 40) & 255));

    private static (int Width, int Height, byte[] Rgba) Read(string name)
    {
        using var stream = File.OpenRead(Path.Combine(FixtureDir, name));
        return PngReader.Read(stream);
    }

    [Theory]
    [InlineData("rgb-filtro0.png", false)]
    [InlineData("rgb-filtro1.png", false)]
    [InlineData("rgb-filtro2.png", false)]
    [InlineData("rgb-filtro3.png", false)]
    [InlineData("rgb-filtro4.png", false)]
    [InlineData("rgba-filtro0.png", true)]
    [InlineData("rgba-filtro1.png", true)]
    [InlineData("rgba-filtro2.png", true)]
    [InlineData("rgba-filtro3.png", true)]
    [InlineData("rgba-filtro4.png", true)]
    [InlineData("rgba-misto.png", true)]
    public void PngReaderDecodesAllFilters(string name, bool alpha)
    {
        var (w, h, rgba) = Read(name);

        Assert.Equal(Width, w);
        Assert.Equal(Height, h);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var e = Expected(x, y);
                var o = (y * w + x) * 4;
                Assert.Equal((e.R, e.G, e.B, alpha ? e.A : (byte)255), (rgba[o], rgba[o + 1], rgba[o + 2], rgba[o + 3]));
            }
        }
    }

    [Fact]
    public void PngReaderRejectsInterlaced()
    {
        var error = Assert.Throws<InvalidDataException>(() => Read("interlacciato.png"));
        Assert.Contains("interlacciato", error.Message);
    }

    [Fact]
    public void PngReaderRejectsOtherFilesInItalian()
    {
        var notPng = Assert.Throws<InvalidDataException>(() => PngReader.Read(new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8])));
        Assert.Contains("Non è un file PNG", notPng.Message);

        var bytes = File.ReadAllBytes(Path.Combine(FixtureDir, "rgb-filtro0.png"));
        var truncated = Assert.Throws<InvalidDataException>(() => PngReader.Read(new MemoryStream(bytes[..40])));
        Assert.Contains("troncato", truncated.Message);

        bytes[20] ^= 0xFF; // inside IHDR: the CRC no longer matches
        var corrupt = Assert.Throws<InvalidDataException>(() => PngReader.Read(new MemoryStream(bytes)));
        Assert.Contains("CRC", corrupt.Message);
    }

    [Fact]
    public void PngReaderReadsWhatTheTestWriterWrites()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tindustry-png-{Guid.NewGuid():N}.png");
        var pixels = new byte[3 * 2 * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(i * 11);
        }

        try
        {
            PngWriter.WriteRgba(path, 3, 2, pixels);
            using var stream = File.OpenRead(path);
            var (w, h, rgba) = PngReader.Read(stream);
            Assert.Equal((3, 2), (w, h));
            Assert.Equal(pixels, rgba);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
