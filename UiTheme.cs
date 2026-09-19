using System.Numerics;
using Raylib_cs;

namespace TIndustry.Logistics;

public enum BuildTool
{
    Conveyor,
    Miner,
    Smelter,
    Remove,
    Junction,
    Splitter,
    Bridge,
    Assembler,
    Generator
}

/// <summary>
/// Crisp UI fonts, item colors/labels, and Mindustry-style build-dock definitions.
/// </summary>
public static class UiTheme
{
    // Mindustry-like dock metrics (pixel-aligned square cells).
    public const int DockCellSize = 48;
    public const int DockCellGap = 2;
    public const int DockPadding = 4;
    public const int DockGridCols = 4;
    public const int DockMargin = 8;
    public const int DockAccentThickness = 2;

    // Legacy name kept for any remaining layout references; dock overlays the map.
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

    /// <summary>Vertical category rail on the far-right build dock.</summary>
    public enum BuildCategory
    {
        Production = 0,
        Logistics = 1,
        Power = 2,
        Tools = 3,
        Inventory = 4
    }

    public enum DockEntryKind
    {
        BuildTool,
        ConveyorVariant,
        Direction,
        InventoryItem
    }

    public sealed record InventoryItemDef(
        string ItemId,
        string DisplayName,
        string ShortName,
        string Abbrev,
        ItemCategory Category);

    public sealed record DockEntry(
        string Id,
        string Label,
        string Glyph,
        DockEntryKind Kind,
        BuildTool? Tool = null,
        string? ResearchId = null,
        string? ConveyorId = null,
        Direction? Facing = null,
        string? ItemId = null);

    public static readonly InventoryItemDef[] InventoryItems =
    [
        new("iron-ore", "Ferro grezzo", "Ferro", "Fe", ItemCategory.Materials),
        new("copper-ore", "Rame grezzo", "Rame", "Ra", ItemCategory.Materials),
        new("iron-plate", "Lastra di ferro", "Lastre", "Ls", ItemCategory.Intermediate),
        new("copper-wire", "Filo di rame", "Fili", "Fi", ItemCategory.Products)
    ];

    public static readonly BuildCategory[] BuildCategories =
    [
        BuildCategory.Production,
        BuildCategory.Logistics,
        BuildCategory.Power,
        BuildCategory.Tools,
        BuildCategory.Inventory
    ];

    public static readonly Color PanelFill = new(18, 20, 22, 200);
    public static readonly Color PanelBorder = new(40, 44, 48, 220);
    public static readonly Color CellFill = new(28, 30, 34, 230);
    public static readonly Color CellFillLocked = new(22, 22, 24, 200);
    public static readonly Color Accent = new(255, 196, 48, 255);
    public static readonly Color AccentDim = new(180, 140, 40, 255);
    public static readonly Color TextPrimary = new(236, 236, 230, 255);
    public static readonly Color TextMuted = new(150, 156, 148, 255);

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

    public static string BuildCategoryLabel(BuildCategory category) => category switch
    {
        BuildCategory.Production => "Produzione",
        BuildCategory.Logistics => "Logistica",
        BuildCategory.Power => "Potenza",
        BuildCategory.Tools => "Strumenti",
        BuildCategory.Inventory => "Inventario",
        _ => "?"
    };

    public static string BuildCategoryGlyph(BuildCategory category) => category switch
    {
        BuildCategory.Production => "Pr",
        BuildCategory.Logistics => "Lo",
        BuildCategory.Power => "Po",
        BuildCategory.Tools => "St",
        BuildCategory.Inventory => "In",
        _ => "?"
    };

    public static Color BuildCategoryTint(BuildCategory category) => category switch
    {
        BuildCategory.Production => new Color(210, 150, 70, 255),
        BuildCategory.Logistics => new Color(120, 170, 210, 255),
        BuildCategory.Power => new Color(230, 200, 70, 255),
        BuildCategory.Tools => new Color(190, 120, 110, 255),
        BuildCategory.Inventory => new Color(140, 190, 140, 255),
        _ => TextPrimary
    };

    public static DockEntry[] EntriesFor(BuildCategory category) => category switch
    {
        BuildCategory.Production =>
        [
            new("miner", "Minatore", "Mn", DockEntryKind.BuildTool, Tool: BuildTool.Miner, ResearchId: "miner"),
            new("smelter", "Forno", "Fo", DockEntryKind.BuildTool, Tool: BuildTool.Smelter, ResearchId: "smelter"),
            new("assembler", "Assembl.", "As", DockEntryKind.BuildTool, Tool: BuildTool.Assembler, ResearchId: "assembler")
        ],
        BuildCategory.Logistics =>
        [
            new("conveyor-basic", "Nastro", "Na", DockEntryKind.ConveyorVariant, Tool: BuildTool.Conveyor,
                ResearchId: "conveyor-basic", ConveyorId: "conveyor-basic"),
            new("conveyor-fast", "Veloce", "Ve", DockEntryKind.ConveyorVariant, Tool: BuildTool.Conveyor,
                ResearchId: "conveyor-fast", ConveyorId: "conveyor-fast"),
            new("junction", "Incrocio", "In", DockEntryKind.BuildTool, Tool: BuildTool.Junction, ResearchId: "junction"),
            new("splitter", "Sdoppiatore", "Sd", DockEntryKind.BuildTool, Tool: BuildTool.Splitter, ResearchId: "splitter"),
            new("bridge", "Ponte", "Po", DockEntryKind.BuildTool, Tool: BuildTool.Bridge, ResearchId: "conveyor-bridge")
        ],
        BuildCategory.Power =>
        [
            new("generator", "Generatore", "Ge", DockEntryKind.BuildTool, Tool: BuildTool.Generator, ResearchId: "generator")
        ],
        BuildCategory.Tools =>
        [
            new("remove", "Rimuovi", "X", DockEntryKind.BuildTool, Tool: BuildTool.Remove),
            new("dir-n", "Nord", "N", DockEntryKind.Direction, Facing: Direction.North),
            new("dir-e", "Est", "E", DockEntryKind.Direction, Facing: Direction.East),
            new("dir-s", "Sud", "S", DockEntryKind.Direction, Facing: Direction.South),
            new("dir-w", "Ovest", "O", DockEntryKind.Direction, Facing: Direction.West)
        ],
        BuildCategory.Inventory => InventoryItems
            .Select(item => new DockEntry(
                item.ItemId,
                item.ShortName,
                item.Abbrev,
                DockEntryKind.InventoryItem,
                ItemId: item.ItemId))
            .ToArray(),
        _ => []
    };

    public static int DockRailWidth => DockPadding * 2 + DockCellSize;

    public static int DockGridWidth(int entryCount)
    {
        var cols = Math.Min(DockGridCols, Math.Max(1, entryCount));
        var rows = Math.Max(1, (int)Math.Ceiling(entryCount / (double)cols));
        // Prefer Mindustry-like width even with few entries.
        cols = DockGridCols;
        rows = Math.Max(rows, 1);
        _ = rows;
        return DockPadding * 2 + cols * DockCellSize + (cols - 1) * DockCellGap;
    }

    public static int DockGridHeight(int entryCount)
    {
        var cols = DockGridCols;
        var rows = Math.Max(1, (int)Math.Ceiling(Math.Max(1, entryCount) / (double)cols));
        // Keep a compact Mindustry panel (at least 2 rows visually when few items).
        rows = Math.Max(rows, 2);
        return DockPadding * 2 + rows * DockCellSize + (rows - 1) * DockCellGap;
    }

    public static int DockTotalWidth(int entryCount) =>
        DockGridWidth(entryCount) + DockCellGap + DockRailWidth;

    public static int DockTotalHeight(int entryCount) =>
        Math.Max(DockGridHeight(entryCount), DockPadding * 2 + BuildCategories.Length * DockCellSize
            + (BuildCategories.Length - 1) * DockCellGap);

    public static void DrawAccentRect(int x, int y, int w, int h, Color color, int thickness = DockAccentThickness)
    {
        for (var i = 0; i < thickness; i++)
        {
            Raylib.DrawRectangleLines(x + i, y + i, w - i * 2, h - i * 2, color);
        }
    }
}
