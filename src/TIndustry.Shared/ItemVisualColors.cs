namespace TIndustry.Shared;

/// <summary>
/// Canonical item icon tints (Raylib UiTheme.ItemColor / Godot ItemPalette).
/// Ore PNGs share one white silhouette — distinguishability comes from these RGB values.
/// </summary>
public static class ItemVisualColors
{
    /// <summary>Opaque RGB 0–255 matching Raylib Color channels.</summary>
    public static (byte R, byte G, byte B) Rgb(string itemId) => itemId switch
    {
        "iron-ore" => (168, 176, 188),
        "iron-plate" => (196, 210, 224),
        "copper-ore" => (232, 128, 48),
        "copper-wire" => (240, 152, 40),
        "coal" => (58, 54, 50),
        "lead-ore" => (120, 140, 168),
        "lead-plate" => (148, 164, 188),
        "titanium-ore" => (120, 200, 210),
        "titanium-plate" => (170, 230, 236),
        "graphite" => (88, 92, 98),
        "silicon" => (210, 200, 120),
        _ => (210, 120, 210)
    };

    public static (byte R, byte G, byte B) OutlineRgb(string itemId) => itemId switch
    {
        "iron-ore" => (36, 42, 52),
        "iron-plate" => (40, 52, 64),
        "copper-ore" => (96, 40, 8),
        "copper-wire" => (96, 48, 8),
        "coal" => (8, 8, 10),
        "lead-ore" => (28, 36, 52),
        "lead-plate" => (32, 40, 56),
        "titanium-ore" => (20, 48, 56),
        "titanium-plate" => (24, 56, 64),
        "graphite" => (20, 22, 26),
        "silicon" => (60, 56, 20),
        _ => (40, 20, 40)
    };

    /// <summary>Self-test: iron grey vs copper orange vs coal charcoal must stay distinct.</summary>
    public static string? SelfTest()
    {
        var iron = Rgb("iron-ore");
        var copper = Rgb("copper-ore");
        var coal = Rgb("coal");
        var wire = Rgb("copper-wire");
        if (!(copper.R > 180 && copper.G < 170 && copper.B < 100))
        {
            return $"copper-ore must be orange (got {copper})";
        }

        if (!(coal.R < 80 && coal.G < 80 && coal.B < 80))
        {
            return $"coal must be dark charcoal (got {coal})";
        }

        var ferroRame = Math.Abs(iron.R - copper.R)
            + Math.Abs(iron.G - copper.G)
            + Math.Abs(iron.B - copper.B);
        if (ferroRame < 80)
        {
            return $"iron/copper too similar (delta={ferroRame})";
        }

        var ferroWire = Math.Abs(iron.R - wire.R)
            + Math.Abs(iron.G - wire.G)
            + Math.Abs(iron.B - wire.B);
        if (ferroWire < 80)
        {
            return $"iron/wire too similar (delta={ferroWire})";
        }

        return null;
    }
}
