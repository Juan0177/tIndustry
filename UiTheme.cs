using System.Numerics;
using Raylib_cs;

namespace TIndustry.Logistics;

/// <summary>
/// Crisp UI fonts, item colors/labels, and inventory category definitions.
/// </summary>
public static class UiTheme
{
    public const int InventoryBarHeight = 100;

    private static Font uiFont;
    private static Font uiFontBold;
    private static bool fontsLoaded;
    private static bool ownsFonts;

    public enum ItemCategory
    {
        All = 0,
        Materials = 1,
        Intermediate = 2,
        Products = 3
    }

    public sealed record InventoryItemDef(
        string ItemId,
        string DisplayName,
        string ShortName,
        string Abbrev,
        ItemCategory Category);

    public static readonly InventoryItemDef[] InventoryItems =
    [
        new("iron-ore", "Ferro grezzo", "Ferro", "Fe", ItemCategory.Materials),
        new("copper-ore", "Rame grezzo", "Rame", "Ra", ItemCategory.Materials),
        new("iron-plate", "Lastra di ferro", "Lastre", "Ls", ItemCategory.Intermediate),
        new("copper-wire", "Filo di rame", "Fili", "Fi", ItemCategory.Products)
    ];

    public static void Load()
    {
        if (fontsLoaded)
        {
            return;
        }

        var baseDir = AppContext.BaseDirectory;
        var regularPath = Path.Combine(baseDir, "assets", "fonts", "DejaVuSans.ttf");
        var boldPath = Path.Combine(baseDir, "assets", "fonts", "DejaVuSans-Bold.ttf");
        // Atlas covers Latin-1 so Italian punctuation (· × à è …) stays crisp.
        const int atlasSize = 64;
        var codepoints = new int[95 + 96];
        for (var i = 0; i < 95; i++)
        {
            codepoints[i] = 32 + i;
        }

        for (var i = 0; i < 96; i++)
        {
            codepoints[95 + i] = 160 + i;
        }

        if (File.Exists(regularPath))
        {
            uiFont = Raylib.LoadFontEx(regularPath, atlasSize, codepoints, codepoints.Length);
            Raylib.SetTextureFilter(uiFont.Texture, TextureFilter.Bilinear);
            ownsFonts = true;
        }
        else
        {
            uiFont = Raylib.GetFontDefault();
        }

        if (File.Exists(boldPath))
        {
            uiFontBold = Raylib.LoadFontEx(boldPath, atlasSize, codepoints, codepoints.Length);
            Raylib.SetTextureFilter(uiFontBold.Texture, TextureFilter.Bilinear);
        }
        else
        {
            uiFontBold = uiFont;
        }

        fontsLoaded = true;
    }

    public static void Unload()
    {
        if (!fontsLoaded || !ownsFonts)
        {
            fontsLoaded = false;
            return;
        }

        Raylib.UnloadFont(uiFont);
        if (!uiFontBold.Equals(uiFont))
        {
            Raylib.UnloadFont(uiFontBold);
        }

        fontsLoaded = false;
        ownsFonts = false;
    }

    public static void DrawText(string text, int x, int y, int size, Color color, bool bold = false)
    {
        if (!fontsLoaded)
        {
            Raylib.DrawText(text, x, y, size, color);
            return;
        }

        var font = bold ? uiFontBold : uiFont;
        var spacing = Math.Max(0.4f, size * 0.045f);
        Raylib.DrawTextEx(font, text, new Vector2(x, y), size, spacing, color);
    }

    public static int Measure(string text, int size, bool bold = false)
    {
        if (!fontsLoaded)
        {
            return Raylib.MeasureText(text, size);
        }

        var font = bold ? uiFontBold : uiFont;
        var spacing = Math.Max(0.4f, size * 0.045f);
        return (int)Raylib.MeasureTextEx(font, text, size, spacing).X;
    }

    public static Color ItemColor(string itemId) => itemId switch
    {
        "iron-ore" => new Color(232, 140, 64, 255),
        "iron-plate" => new Color(196, 210, 224, 255),
        "copper-ore" => new Color(64, 196, 176, 255),
        "copper-wire" => new Color(232, 156, 72, 255),
        _ => new Color(210, 120, 210, 255)
    };

    public static Color ItemOutline(string itemId) => itemId switch
    {
        "iron-ore" => new Color(90, 42, 12, 255),
        "iron-plate" => new Color(40, 52, 64, 255),
        "copper-ore" => new Color(12, 56, 52, 255),
        "copper-wire" => new Color(90, 48, 12, 255),
        _ => new Color(40, 20, 40, 255)
    };

    public static string ItemAbbrev(string itemId)
    {
        foreach (var item in InventoryItems)
        {
            if (item.ItemId == itemId)
            {
                return item.Abbrev;
            }
        }

        return "?";
    }

    public static string CategoryLabel(ItemCategory category) => category switch
    {
        ItemCategory.All => "Tutto",
        ItemCategory.Materials => "Materiali",
        ItemCategory.Intermediate => "Intermedi",
        ItemCategory.Products => "Prodotti",
        _ => "Tutto"
    };

    public static IEnumerable<InventoryItemDef> ItemsInCategory(ItemCategory category)
    {
        if (category == ItemCategory.All)
        {
            return InventoryItems;
        }

        return InventoryItems.Where(item => item.Category == category);
    }
}

/// <summary>
/// Applies resolution and display mode through Raylib window APIs.
/// </summary>
public static class DisplayApplier
{
    public static void Apply(GameSettings settings)
    {
        settings.Normalize();
        var width = settings.ResolutionWidth;
        var height = settings.ResolutionHeight;

        // Leave exclusive/borderless first so size changes stick in windowed mode.
        if (Raylib.IsWindowFullscreen())
        {
            Raylib.ToggleFullscreen();
        }

        if (Raylib.IsWindowState(ConfigFlags.BorderlessWindowMode))
        {
            Raylib.ClearWindowState(ConfigFlags.BorderlessWindowMode);
        }

        Raylib.SetWindowSize(width, height);

        switch (settings.DisplayMode)
        {
            case DisplayMode.Fullscreen:
                if (!Raylib.IsWindowFullscreen())
                {
                    Raylib.ToggleFullscreen();
                }

                break;
            case DisplayMode.Borderless:
                Raylib.SetWindowState(ConfigFlags.BorderlessWindowMode);
                // Borderless typically covers the monitor; keep logical UI size from settings.
                break;
            case DisplayMode.Windowed:
            default:
                Raylib.SetWindowPosition(
                    Math.Max(40, (Raylib.GetMonitorWidth(0) - width) / 2),
                    Math.Max(40, (Raylib.GetMonitorHeight(0) - height) / 2));
                break;
        }
    }
}
