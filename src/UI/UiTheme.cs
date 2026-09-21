using System.Numerics;
using Raylib_cs;

namespace TIndustry.Logistics;

public enum BuildTool
{
    Conveyor,
    Miner,
    MinerAdvanced,
    Smelter,
    Remove,
    Junction,
    Splitter,
    Bridge,
    Assembler,
    Generator,
    PowerNode,
    PowerNodeT2,
    Sorter
}

/// <summary>
/// Crisp UI fonts, item colors/labels, and Mindustry-style build-dock definitions.
/// </summary>
public static class UiTheme
{
    // Mindustry-like dock metrics (logical pixels; scaled via Scale).
    public const int DockCellSizeBase = 48;
    public const int DockCellGapBase = 2;
    public const int DockPaddingBase = 4;
    public const int DockGridCols = 4;
    public const int DockMarginBase = 8;
    public const int DockAccentThickness = 2;
    // Tall enough for recipe usage (Input / Output) + build-cost row.
    public const int DockHoverBarHeightBase = 72;

    // Legacy name kept for any remaining layout references; dock overlays the map.
    public const int InventoryBarHeight = 100;

    private static Font uiFont;
    private static Font uiFontBold;
    private static bool fontsLoaded;
    private static bool ownsFonts;

    /// <summary>Active UI scale factor (1.0 = 100%). Applied to fonts and dock metrics.</summary>
    public static float Scale { get; private set; } = 1.25f;

    /// <summary>Scale a logical pixel size for HUD/dock layout.</summary>
    public static int S(int px) => Math.Max(1, (int)MathF.Round(px * Scale));

    public static int DockCellSize => S(DockCellSizeBase);
    public static int DockCellGap => Math.Max(1, S(DockCellGapBase));
    public static int DockPadding => S(DockPaddingBase);
    public static int DockMargin => S(DockMarginBase);
    public static int DockHoverBarHeight => S(DockHoverBarHeightBase);

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
        string? ItemId = null,
        string? Hint = null);

    public static readonly InventoryItemDef[] InventoryItems =
    [
        new("iron-ore", "Ferro grezzo", "Ferro", "Fe", ItemCategory.Materials),
        new("copper-ore", "Rame grezzo", "Rame", "Ra", ItemCategory.Materials),
        new("coal", "Carbone", "Carb.", "Ca", ItemCategory.Materials),
        new("iron-plate", "Lastra di ferro", "Lastre", "Ls", ItemCategory.Intermediate),
        new("copper-wire", "Filo di rame", "Fili", "Fi", ItemCategory.Products)
    ];

    /// <summary>Dock rail categories — Strumenti removed; Rimuovi lives in Produzione.</summary>
    public static readonly BuildCategory[] BuildCategories =
    [
        BuildCategory.Production,
        BuildCategory.Logistics,
        BuildCategory.Power
    ];

    // Higher-contrast HUD chrome (readable over busy factory viewports).
    public static readonly Color PanelFill = new(12, 14, 16, 236);
    public static readonly Color PanelBorder = new(72, 82, 78, 255);
    public static readonly Color PanelBorderBright = new(118, 132, 124, 255);
    public static readonly Color CellFill = new(32, 36, 40, 242);
    public static readonly Color CellFillLocked = new(20, 20, 22, 220);
    public static readonly Color Accent = new(255, 196, 48, 255);
    public static readonly Color AccentDim = new(180, 140, 40, 255);
    public static readonly Color TextPrimary = new(244, 244, 236, 255);
    public static readonly Color TextMuted = new(168, 176, 168, 255);
    public static readonly Color MoneyGreen = new(120, 228, 150, 255);
    public static readonly Color MoneyRed = new(235, 120, 100, 255);

    public static void Load()
    {
        if (fontsLoaded)
        {
            return;
        }

        LoadFonts();
        fontsLoaded = true;
        GameIcons.Load();
    }

    /// <summary>
    /// Apply a UI scale and rebuild the font atlas at a matching base size so text stays crisp
    /// (avoids blurry upscale of a small atlas).
    /// </summary>
    public static void ApplyScale(float scale)
    {
        var clamped = Math.Clamp(scale, 1f, 2f);
        if (fontsLoaded && Math.Abs(Scale - clamped) < 0.001f)
        {
            return;
        }

        Scale = clamped;
        if (!fontsLoaded)
        {
            return;
        }

        UnloadFonts();
        LoadFonts();
        fontsLoaded = true;
    }

    public static void ApplyScalePercent(int percent) => ApplyScale(percent / 100f);

    private static void LoadFonts()
    {
        var baseDir = AppContext.BaseDirectory;
        var regularPath = Path.Combine(baseDir, "assets", "fonts", "DejaVuSans.ttf");
        var boldPath = Path.Combine(baseDir, "assets", "fonts", "DejaVuSans-Bold.ttf");
        // Bake glyphs larger than typical draw sizes so UI scale downsamples cleanly (not upscale-blur).
        // 96×scale keeps HUD text sharp at 100–200%; bilinear avoids the point-filter "pixel" look.
        var atlasSize = Math.Clamp((int)MathF.Round(96f * Scale), 72, 224);
        var codepoints = new int[95 + 96 + 1];
        for (var i = 0; i < 95; i++)
        {
            codepoints[i] = 32 + i;
        }

        for (var i = 0; i < 96; i++)
        {
            codepoints[95 + i] = 160 + i;
        }

        codepoints[^1] = 0x0394; // Δ (delta sessione)

        if (File.Exists(regularPath))
        {
            uiFont = Raylib.LoadFontEx(regularPath, atlasSize, codepoints, codepoints.Length);
            Raylib.SetTextureFilter(uiFont.Texture, TextureFilter.Bilinear);
            ownsFonts = true;
        }
        else
        {
            uiFont = Raylib.GetFontDefault();
            ownsFonts = false;
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
    }

    private static void UnloadFonts()
    {
        if (!ownsFonts)
        {
            return;
        }

        Raylib.UnloadFont(uiFont);
        if (!uiFontBold.Equals(uiFont))
        {
            Raylib.UnloadFont(uiFontBold);
        }

        ownsFonts = false;
    }

    public static void Unload()
    {
        GameIcons.Unload();
        if (!fontsLoaded)
        {
            return;
        }

        UnloadFonts();
        fontsLoaded = false;
    }

    /// <summary>Session net-worth delta label shown in the resource strip.</summary>
    public static string SessionDeltaLabel(int net) =>
        $"Δ sessione {(net >= 0 ? "+" : "")}{net}";

    public static string SessionDeltaTooltip =>
        "Variazione del patrimonio netto dall'inizio della partita (vendite − spese).";

    public static void DrawText(string text, int x, int y, int size, Color color, bool bold = false)
    {
        var drawSize = Math.Max(1, (int)MathF.Round(size * Scale));
        if (!fontsLoaded)
        {
            Raylib.DrawText(text, x, y, drawSize, color);
            return;
        }

        var font = bold ? uiFontBold : uiFont;
        var spacing = Math.Max(0.4f, drawSize * 0.045f);
        Raylib.DrawTextEx(font, text, new Vector2(x, y), drawSize, spacing, color);
    }

    public static int Measure(string text, int size, bool bold = false)
    {
        var drawSize = Math.Max(1, (int)MathF.Round(size * Scale));
        if (!fontsLoaded)
        {
            return Raylib.MeasureText(text, drawSize);
        }

        var font = bold ? uiFontBold : uiFont;
        var spacing = Math.Max(0.4f, drawSize * 0.045f);
        return (int)Raylib.MeasureTextEx(font, text, drawSize, spacing).X;
    }

    public static Color ItemColor(string itemId) => itemId switch
    {
        // Ores share the stone-pile silhouette; tint only (gray / orange / charcoal).
        "iron-ore" => new Color(168, 176, 188, 255),
        "iron-plate" => new Color(196, 210, 224, 255),
        "copper-ore" => new Color(232, 128, 48, 255),
        "copper-wire" => new Color(240, 152, 40, 255),
        "coal" => new Color(58, 54, 50, 255),
        _ => new Color(210, 120, 210, 255)
    };

    public static Color ItemOutline(string itemId) => itemId switch
    {
        "iron-ore" => new Color(36, 42, 52, 255),
        "iron-plate" => new Color(40, 52, 64, 255),
        "copper-ore" => new Color(96, 40, 8, 255),
        "copper-wire" => new Color(96, 48, 8, 255),
        "coal" => new Color(8, 8, 10, 255),
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

    public static string ItemDisplayName(string itemId)
    {
        foreach (var item in InventoryItems)
        {
            if (item.ItemId == itemId)
            {
                return item.DisplayName;
            }
        }

        return itemId;
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

    /// <summary>Short Italian label under dock category icons.</summary>
    public static string BuildCategoryShortLabel(BuildCategory category) => category switch
    {
        BuildCategory.Production => "Prod",
        BuildCategory.Logistics => "Log",
        BuildCategory.Power => "PWR",
        BuildCategory.Tools => "Tool",
        _ => "?"
    };

    /// <summary>Short Italian label drawn under a dock entry icon.</summary>
    public static string DockEntryShortLabel(DockEntry entry) => entry.Id switch
    {
        "miner" => "T1",
        "miner-advanced" => "T2",
        "smelter" => "Forno",
        "assembler" => "Assem",
        "generator" => "Gener",
        "conveyor-basic" => "T1",
        "conveyor-fast" => "T2",
        "conveyor-express" => "T3",
        "junction" => "Incroc",
        "splitter" => "Split",
        "sorter" => "Filtro",
        "bridge" => "Ponte",
        "remove" => "Rimuovi",
        _ => entry.Label.Length <= 6 ? entry.Label : entry.Label[..5] + "…"
    };

    public static string BuildCategoryGlyph(BuildCategory category) => category switch
    {
        BuildCategory.Production => "Pr",
        BuildCategory.Logistics => "Lo",
        BuildCategory.Power => "⚡",
        BuildCategory.Tools => "St",
        BuildCategory.Inventory => "In",
        _ => "?"
    };

    // DockHoverBarHeight is scaled via DockHoverBarHeight property above.

    /// <summary>Short label for hover tooltip under the dock.</summary>
    public static string BuildCategoryHint(BuildCategory category) => BuildCategoryLabel(category);

    public static Color BuildCategoryTint(BuildCategory category) => category switch
    {
        BuildCategory.Production => new Color(210, 150, 70, 255),
        BuildCategory.Logistics => new Color(120, 170, 210, 255),
        BuildCategory.Power => new Color(230, 200, 70, 255),
        BuildCategory.Tools => new Color(190, 120, 110, 255),
        BuildCategory.Inventory => new Color(140, 190, 140, 255),
        _ => TextPrimary
    };

    // Cached once — EntriesFor used every play frame for dock bounds/draw/input.
    private static readonly DockEntry[] ProductionEntries =
    [
        new("miner", "Minatore T1", "T1", DockEntryKind.BuildTool, Tool: BuildTool.Miner, ResearchId: "miner",
            Hint: "Estrae minerali · uscita su tutti i lati"),
        new("miner-advanced", "Minatore T2", "T2", DockEntryKind.BuildTool, Tool: BuildTool.MinerAdvanced,
            ResearchId: "miner-advanced",
            Hint: "T2: 2× velocità · +25% efficienza · uscita multi-lato"),
        new("smelter", "Forno", "Fo", DockEntryKind.BuildTool, Tool: BuildTool.Smelter, ResearchId: "smelter",
            Hint: "Fonde ore in lastre · R ruota uscita"),
        new("assembler", "Assembl.", "As", DockEntryKind.BuildTool, Tool: BuildTool.Assembler, ResearchId: "assembler",
            Hint: "Assembla prodotti · R ruota uscita"),
        new("remove", "Rimuovi", "X", DockEntryKind.BuildTool, Tool: BuildTool.Remove,
            Hint: "Demolisci edifici e nastri (tasto 4)")
    ];

    private static readonly DockEntry[] LogisticsEntries =
    [
        new("conveyor-basic", "Nastro T1", "T1", DockEntryKind.ConveyorVariant, Tool: BuildTool.Conveyor,
            ResearchId: "conveyor-basic", ConveyorId: "conveyor-basic",
            Hint: "Nastro T1 · flusso unidirezionale · R/rotella"),
        new("conveyor-fast", "Nastro T2", "T2", DockEntryKind.ConveyorVariant, Tool: BuildTool.Conveyor,
            ResearchId: "conveyor-fast", ConveyorId: "conveyor-fast",
            Hint: "Nastro T2 · R/rotella · E"),
        new("conveyor-express", "Nastro T3", "T3", DockEntryKind.ConveyorVariant, Tool: BuildTool.Conveyor,
            ResearchId: "conveyor-express", ConveyorId: "conveyor-express",
            Hint: "Nastro T3 · R/rotella · Y"),
        new("junction", "Incrocio", "In", DockEntryKind.BuildTool, Tool: BuildTool.Junction, ResearchId: "junction",
            Hint: "Incrocio a croce (6)"),
        new("splitter", "Sdoppiatore", "Sd", DockEntryKind.BuildTool, Tool: BuildTool.Splitter, ResearchId: "splitter",
            Hint: "Nastro a T · alterna sinistra/destra"),
        new("sorter", "Selezionatore", "Se", DockEntryKind.BuildTool, Tool: BuildTool.Sorter, ResearchId: "sorter",
            Hint: "Filtro item · match avanti, altri ai lati · F cicla"),
        new("bridge", "Ponte", "Po", DockEntryKind.BuildTool, Tool: BuildTool.Bridge, ResearchId: "conveyor-bridge",
            Hint: "Ponte a due capi (8)")
    ];

    private static readonly DockEntry[] PowerEntries =
    [
        new("generator", "Generatore", "Ge", DockEntryKind.BuildTool, Tool: BuildTool.Generator, ResearchId: "generator",
            Hint: "Brucia carbone per energia (9) · rete locale via nodi"),
        new("power-node", "Nodo T1", "T1", DockEntryKind.BuildTool, Tool: BuildTool.PowerNode, ResearchId: "power-node",
            Hint: "Nodo T1 · 1×1 · 4 link · range 6 · auto-link gen (mai CORE)"),
        new("power-node-t2", "Nodo T2", "T2", DockEntryKind.BuildTool, Tool: BuildTool.PowerNodeT2, ResearchId: "power-node-t2",
            Hint: "Nodo T2 · 2×2 · 8 link · range 10 · auto-link gen (mai CORE)")
    ];

    private static readonly DockEntry[] EmptyEntries = [];

    public static DockEntry[] EntriesFor(BuildCategory category) => category switch
    {
        BuildCategory.Production => ProductionEntries,
        BuildCategory.Logistics => LogisticsEntries,
        BuildCategory.Power => PowerEntries,
        // Legacy enum values kept for switch exhaustiveness; not on the rail.
        BuildCategory.Tools => EmptyEntries,
        BuildCategory.Inventory => EmptyEntries,
        _ => EmptyEntries
    };

    /// <summary>Italian tooltip line for dock hover: name + short hint.</summary>
    public static string DockHoverText(DockEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Hint))
        {
            return entry.Label;
        }

        return $"{entry.Label} — {entry.Hint}";
    }

    public static string DockHoverText(BuildCategory category) => category switch
    {
        BuildCategory.Production => "Produzione — edifici e demolizione",
        BuildCategory.Logistics => "Logistica — nastri e routing",
        BuildCategory.Power => "Potenza — generatori e nodi",
        _ => BuildCategoryLabel(category)
    };

    public static void DrawBuildCategoryIcon(BuildCategory category, int cx, int cy, int size, Color color)
    {
        var key = GameIcons.CategoryKey(category);
        var labelReserve = Math.Max(10, size / 5);
        var iconArea = size - labelReserve;
        if (key is not null && GameIcons.TryDraw(key, cx + 6, cy + 4, iconArea - 8, color))
        {
            return;
        }

        var pad = size / 6;
        var x = cx + pad;
        var y = cy + pad / 2;
        var s = iconArea - pad * 2;
        switch (category)
        {
            case BuildCategory.Production:
                // Factory block + roof + chimney stack.
                Raylib.DrawRectangle(x + 2, y + s / 2, s - 4, s / 2 - 1, color);
                Raylib.DrawTriangle(
                    new Vector2(x + s / 2f, y + s / 4f),
                    new Vector2(x + 2, y + s / 2f),
                    new Vector2(x + s - 2, y + s / 2f),
                    color);
                Raylib.DrawRectangle(x + s - 10, y + 2, 5, s / 2 - 2, color);
                break;
            case BuildCategory.Logistics:
                // Belt: chevron arrow.
                Raylib.DrawRectangle(x + 1, y + s / 2 - 2, s - 10, 4, color);
                Raylib.DrawTriangle(
                    new Vector2(x + s, y + s / 2f),
                    new Vector2(x + s - 12, y + 3),
                    new Vector2(x + s - 12, y + s - 3),
                    color);
                break;
            case BuildCategory.Power:
                // Lightning bolt.
                Raylib.DrawTriangle(
                    new Vector2(x + s * 0.58f, y + 1),
                    new Vector2(x + 3, y + s * 0.52f),
                    new Vector2(x + s * 0.52f, y + s * 0.52f),
                    color);
                Raylib.DrawTriangle(
                    new Vector2(x + s * 0.42f, y + s * 0.42f),
                    new Vector2(x + s - 3, y + s * 0.42f),
                    new Vector2(x + s * 0.38f, y + s - 1),
                    color);
                break;
            case BuildCategory.Tools:
                // Crossed tools: horizontal bar + diagonal handle.
                Raylib.DrawRectangle(x + 2, y + s / 2 - 2, s - 4, 4, color);
                Raylib.DrawLineEx(
                    new Vector2(x + 6, y + s - 4),
                    new Vector2(x + s - 6, y + 4),
                    3.5f,
                    color);
                Raylib.DrawRectangle(x + s - 12, y + 2, 8, 6, color);
                break;
            default:
                Raylib.DrawRectangleLines(x, y, s, s, color);
                break;
        }
    }

    public static void DrawDockEntryIcon(string entryId, int cx, int cy, int size, Color color)
    {
        var labelReserve = Math.Max(10, size / 5);
        var iconArea = size - labelReserve;
        if (GameIcons.TryDraw(entryId, cx + 6, cy + 4, iconArea - 8, color))
        {
            return;
        }

        var pad = 8;
        var x = cx + pad;
        var y = cy + 4;
        var s = iconArea - pad * 2;
        switch (entryId)
        {
            case "miner":
                // Drill bit.
                Raylib.DrawTriangle(
                    new Vector2(x + s / 2, y + s),
                    new Vector2(x + 2, y + 4),
                    new Vector2(x + s - 2, y + 4),
                    color);
                Raylib.DrawRectangle(x + s / 2 - 3, y, 6, 8, color);
                break;
            case "miner-advanced":
                Raylib.DrawTriangle(
                    new Vector2(x + s / 2, y + s),
                    new Vector2(x + 2, y + 4),
                    new Vector2(x + s - 2, y + 4),
                    color);
                Raylib.DrawRectangle(x + s / 2 - 3, y, 6, 8, color);
                Raylib.DrawRectangle(x + 4, y + 2, 4, 4, color);
                Raylib.DrawRectangle(x + s - 8, y + 2, 4, 4, color);
                break;
            case "smelter":
                // Furnace.
                Raylib.DrawRectangle(x + 2, y + 6, s - 4, s - 8, color);
                Raylib.DrawRectangle(x + s / 2 - 4, y, 8, 8, color);
                Raylib.DrawRectangle(x + 6, y + s - 10, s - 12, 4, new Color(28, 30, 34, 255));
                break;
            case "assembler":
                // Two gears (circles).
                Raylib.DrawCircle(x + s / 3, y + s / 2, s / 3, color);
                Raylib.DrawCircle(x + 2 * s / 3, y + s / 2, s / 4, color);
                Raylib.DrawCircle(x + s / 3, y + s / 2, 3, new Color(28, 30, 34, 255));
                break;
            case "conveyor-basic":
                Raylib.DrawRectangle(x, y + s / 2 - 3, s - 6, 6, color);
                Raylib.DrawTriangle(
                    new Vector2(x + s, y + s / 2),
                    new Vector2(x + s - 10, y + 2),
                    new Vector2(x + s - 10, y + s - 2),
                    color);
                break;
            case "conveyor-fast":
                Raylib.DrawRectangle(x, y + s / 2 - 4, s - 8, 8, color);
                Raylib.DrawTriangle(
                    new Vector2(x + s, y + s / 2),
                    new Vector2(x + s - 12, y),
                    new Vector2(x + s - 12, y + s),
                    color);
                Raylib.DrawLineEx(new Vector2(x + 4, y + 4), new Vector2(x + s - 14, y + 4), 2f, color);
                break;
            case "conveyor-express":
                Raylib.DrawRectangle(x, y + s / 2 - 5, s - 8, 10, color);
                Raylib.DrawTriangle(
                    new Vector2(x + s, y + s / 2),
                    new Vector2(x + s - 14, y),
                    new Vector2(x + s - 14, y + s),
                    color);
                Raylib.DrawLineEx(new Vector2(x + 3, y + 3), new Vector2(x + s - 16, y + 3), 2f, color);
                Raylib.DrawLineEx(new Vector2(x + 3, y + s - 3), new Vector2(x + s - 16, y + s - 3), 2f, color);
                break;
            case "junction":
                Raylib.DrawRectangle(x + s / 2 - 3, y, 6, s, color);
                Raylib.DrawRectangle(x, y + s / 2 - 3, s, 6, color);
                break;
            case "splitter":
                Raylib.DrawRectangle(x, y + s / 2 - 3, s / 2, 6, color);
                Raylib.DrawTriangle(
                    new Vector2(x + s / 2, y + s / 2),
                    new Vector2(x + s, y + 2),
                    new Vector2(x + s, y + s - 2),
                    color);
                break;
            case "sorter":
                // Funnel: inlet → center, side exits.
                Raylib.DrawRectangle(x + 8, y + 4, s - 16, 6, color);
                Raylib.DrawTriangle(
                    new Vector2(x + s / 2, y + s / 2),
                    new Vector2(x + 10, y + 12),
                    new Vector2(x + s - 10, y + 12),
                    color);
                Raylib.DrawRectangle(x + 2, y + s / 2 - 2, 10, 4, color);
                Raylib.DrawRectangle(x + s - 12, y + s / 2 - 2, 10, 4, color);
                Raylib.DrawRectangle(x + s / 2 - 3, y + s / 2, 6, s / 2 - 4, color);
                break;
            case "bridge":
                Raylib.DrawRectangle(x, y + s / 2 - 2, s, 4, color);
                Raylib.DrawRectangle(x + 2, y + 4, 6, s - 8, color);
                Raylib.DrawRectangle(x + s - 8, y + 4, 6, s - 8, color);
                break;
            case "generator":
                // Bolt.
                Raylib.DrawTriangle(
                    new Vector2(x + s * 0.55f, y),
                    new Vector2(x + 2, y + s * 0.55f),
                    new Vector2(x + s * 0.5f, y + s * 0.55f),
                    color);
                Raylib.DrawTriangle(
                    new Vector2(x + s * 0.45f, y + s * 0.42f),
                    new Vector2(x + s - 2, y + s * 0.42f),
                    new Vector2(x + s * 0.4f, y + s),
                    color);
                break;
            case "remove":
                Raylib.DrawLineEx(new Vector2(x + 4, y + 4), new Vector2(x + s - 4, y + s - 4), 3f, color);
                Raylib.DrawLineEx(new Vector2(x + s - 4, y + 4), new Vector2(x + 4, y + s - 4), 3f, color);
                break;
            case "dir-n":
                Raylib.DrawTriangle(
                    new Vector2(x + s / 2, y + 2),
                    new Vector2(x + 4, y + s - 4),
                    new Vector2(x + s - 4, y + s - 4),
                    color);
                break;
            case "dir-e":
                Raylib.DrawTriangle(
                    new Vector2(x + s - 2, y + s / 2),
                    new Vector2(x + 4, y + 4),
                    new Vector2(x + 4, y + s - 4),
                    color);
                break;
            case "dir-s":
                Raylib.DrawTriangle(
                    new Vector2(x + s / 2, y + s - 2),
                    new Vector2(x + 4, y + 4),
                    new Vector2(x + s - 4, y + 4),
                    color);
                break;
            case "dir-w":
                Raylib.DrawTriangle(
                    new Vector2(x + 2, y + s / 2),
                    new Vector2(x + s - 4, y + 4),
                    new Vector2(x + s - 4, y + s - 4),
                    color);
                break;
            default:
                Raylib.DrawRectangle(x + 4, y + 4, s - 8, s - 8, color);
                break;
        }
    }

    /// <summary>Header chrome: gear (Impostazioni).</summary>
    public static void DrawGearIcon(int cx, int cy, int size, Color color)
    {
        if (GameIcons.TryDraw("settings", cx, cy, size, color))
        {
            return;
        }

        var r = size * 0.38f;
        var cxF = cx + size / 2f;
        var cyF = cy + size / 2f;
        Raylib.DrawCircle((int)cxF, (int)cyF, r, color);
        Raylib.DrawCircle((int)cxF, (int)cyF, r * 0.45f, new Color(45, 52, 50, 255));
        for (var i = 0; i < 6; i++)
        {
            var a = i * MathF.PI / 3f;
            var ox = MathF.Cos(a) * r * 0.85f;
            var oy = MathF.Sin(a) * r * 0.85f;
            Raylib.DrawRectangle((int)(cxF + ox - 3), (int)(cyF + oy - 3), 6, 6, color);
        }
    }

    /// <summary>Header chrome: research (Ricerca).</summary>
    public static void DrawTreeIcon(int cx, int cy, int size, Color color)
    {
        if (GameIcons.TryDraw("research", cx, cy, size, color))
        {
            return;
        }

        var trunkW = Math.Max(3, size / 7);
        var trunkH = size / 3;
        Raylib.DrawRectangle(cx + (size - trunkW) / 2, cy + size - trunkH - 3, trunkW, trunkH, color);
        Raylib.DrawTriangle(
            new Vector2(cx + size / 2f, cy + 3),
            new Vector2(cx + 3, cy + size * 0.48f),
            new Vector2(cx + size - 3, cy + size * 0.48f),
            color);
        Raylib.DrawTriangle(
            new Vector2(cx + size / 2f, cy + size * 0.22f),
            new Vector2(cx + 5, cy + size * 0.68f),
            new Vector2(cx + size - 5, cy + size * 0.68f),
            color);
    }

    /// <summary>Header chrome: menu grid.</summary>
    public static void DrawMenuIcon(int cx, int cy, int size, Color color)
    {
        if (GameIcons.TryDraw("menu", cx, cy, size, color))
        {
            return;
        }

        var padX = size / 5;
        var lineH = Math.Max(2, size / 10);
        var gap = (size - padX * 2 - lineH * 3) / 2;
        var y = cy + padX;
        for (var i = 0; i < 3; i++)
        {
            Raylib.DrawRectangle(cx + padX, y + i * (lineH + gap), size - padX * 2, lineH, color);
        }
    }

    /// <summary>Draw an inventory item glyph (texture or letter fallback).</summary>
    public static void DrawItemIcon(string itemId, int x, int y, int size, Color? tint = null)
    {
        var color = tint ?? ItemColor(itemId);
        if (GameIcons.TryDraw(itemId, x, y, size, color))
        {
            return;
        }

        Raylib.DrawRectangle(x, y, size, size, color);
        Raylib.DrawRectangleLines(x, y, size, size, ItemOutline(itemId));
        var abbrev = ItemAbbrev(itemId);
        var fontSize = Math.Max(9, size - 10);
        var abbrevW = Measure(abbrev, fontSize);
        DrawText(abbrev, x + (size - abbrevW) / 2, y + (size - fontSize) / 2, fontSize, new Color(18, 16, 12, 255));
    }

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
            + (BuildCategories.Length - 1) * DockCellGap)
        + DockHoverBarHeight;

    public static void DrawAccentRect(int x, int y, int w, int h, Color color, int thickness = DockAccentThickness)
    {
        for (var i = 0; i < thickness; i++)
        {
            Raylib.DrawRectangleLines(x + i, y + i, w - i * 2, h - i * 2, color);
        }
    }
}
