using System.Numerics;
using Raylib_cs;

namespace TIndustry.Logistics;

/// <summary>
/// Loads PNG icons from assets/icons into a single texture atlas and draws UV rects.
/// White-on-transparent glyphs are tinted at draw time. Public Draw/TryDraw API unchanged.
/// </summary>
public static class GameIcons
{
    private const int CellSize = 64;
    private const int AtlasPadding = 2;

    private static readonly Dictionary<string, Rectangle> SrcRects = new(StringComparer.Ordinal);
    private static Texture2D atlas;
    private static bool loaded;
    private static bool hasAtlas;

    /// <summary>True after a successful atlas pack (single GPU texture).</summary>
    public static bool UsesAtlas => loaded && hasAtlas;

    public static int AtlasWidth => hasAtlas ? atlas.Width : 0;

    public static int AtlasHeight => hasAtlas ? atlas.Height : 0;

    public static void Load()
    {
        if (loaded)
        {
            return;
        }

        var root = Path.Combine(AppContext.BaseDirectory, "assets", "icons");
        var entries = new (string RelativePath, string Key)[]
        {
            ("items/money.png", "money"),
            ("items/iron-ore.png", "iron-ore"),
            ("items/copper-ore.png", "copper-ore"),
            ("items/coal.png", "coal"),
            ("items/lead-ore.png", "lead-ore"),
            ("items/titanium-ore.png", "titanium-ore"),
            ("items/iron-plate.png", "iron-plate"),
            ("items/lead-plate.png", "lead-plate"),
            ("items/titanium-plate.png", "titanium-plate"),
            ("items/copper-wire.png", "copper-wire"),
            ("items/graphite.png", "graphite"),
            ("items/silicon.png", "silicon"),
            ("buildings/miner.png", "miner"),
            ("buildings/smelter.png", "smelter"),
            ("buildings/assembler.png", "assembler"),
            ("buildings/extractor.png", "extractor"),
            ("buildings/generator.png", "generator"),
            ("buildings/power-node.png", "power-node"),
            ("buildings/power-node-t2.png", "power-node-t2"),
            ("buildings/power-cable.png", "power-cable"),
            ("buildings/conveyor-basic.png", "conveyor-basic"),
            ("buildings/conveyor-fast.png", "conveyor-fast"),
            ("buildings/junction.png", "junction"),
            ("buildings/splitter.png", "splitter"),
            ("buildings/sorter.png", "sorter"),
            ("buildings/bridge.png", "bridge"),
            ("buildings/remove.png", "remove"),
            ("ui/dir-n.png", "dir-n"),
            ("ui/dir-e.png", "dir-e"),
            ("ui/dir-s.png", "dir-s"),
            ("ui/dir-w.png", "dir-w"),
            ("ui/settings.png", "settings"),
            ("ui/research.png", "research"),
            ("ui/menu.png", "menu"),
            ("ui/locked.png", "locked"),
            ("ui/power.png", "power"),
            ("ui/sell.png", "sell"),
            ("categories/production.png", "cat-production"),
            ("categories/logistics.png", "cat-logistics"),
            ("categories/power.png", "cat-power"),
            ("categories/tools.png", "cat-tools"),
        };

        var loadedImages = new List<(string Key, Image Image)>();
        foreach (var (relativePath, key) in entries)
        {
            var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                continue;
            }

            var image = Raylib.LoadImage(path);
            if (image.Width <= 0 || image.Height <= 0)
            {
                Raylib.UnloadImage(image);
                continue;
            }

            if (image.Width != CellSize || image.Height != CellSize)
            {
                Raylib.ImageResize(ref image, CellSize, CellSize);
            }

            loadedImages.Add((key, image));
        }

        if (loadedImages.Count == 0)
        {
            loaded = true;
            hasAtlas = false;
            return;
        }

        var columns = (int)Math.Ceiling(Math.Sqrt(loadedImages.Count));
        columns = Math.Max(1, columns);
        var rows = (int)Math.Ceiling(loadedImages.Count / (double)columns);
        var cellStride = CellSize + AtlasPadding;
        var atlasW = columns * cellStride + AtlasPadding;
        var atlasH = rows * cellStride + AtlasPadding;
        var atlasImage = Raylib.GenImageColor(atlasW, atlasH, new Color(0, 0, 0, 0));

        for (var index = 0; index < loadedImages.Count; index++)
        {
            var (key, image) = loadedImages[index];
            var col = index % columns;
            var row = index / columns;
            var dx = AtlasPadding + col * cellStride;
            var dy = AtlasPadding + row * cellStride;
            var src = new Rectangle(0, 0, image.Width, image.Height);
            var dst = new Rectangle(dx, dy, CellSize, CellSize);
            Raylib.ImageDraw(ref atlasImage, image, src, dst, Color.White);
            SrcRects[key] = new Rectangle(dx, dy, CellSize, CellSize);
            Raylib.UnloadImage(image);
        }

        atlas = Raylib.LoadTextureFromImage(atlasImage);
        Raylib.SetTextureFilter(atlas, TextureFilter.Bilinear);
        Raylib.UnloadImage(atlasImage);
        hasAtlas = true;
        loaded = true;
    }

    public static void Unload()
    {
        if (!loaded)
        {
            return;
        }

        if (hasAtlas)
        {
            Raylib.UnloadTexture(atlas);
            hasAtlas = false;
        }

        SrcRects.Clear();
        loaded = false;
    }

    public static bool Has(string key) => SrcRects.ContainsKey(ResolveKey(key));

    public static int LoadedCount => SrcRects.Count;

    public static void Draw(string key, int x, int y, int size, Color tint)
    {
        if (!hasAtlas || !SrcRects.TryGetValue(ResolveKey(key), out var src))
        {
            return;
        }

        var dst = new Rectangle(x, y, size, size);
        Raylib.DrawTexturePro(atlas, src, dst, Vector2.Zero, 0f, tint);
    }

    public static bool TryDraw(string key, int x, int y, int size, Color tint)
    {
        if (!Has(key))
        {
            return false;
        }

        Draw(key, x, y, size, tint);
        return true;
    }

    /// <summary>
    /// Tier variants reuse the base glyph when a dedicated PNG is not shipped.
    /// </summary>
    public static string ResolveKey(string key) => key switch
    {
        "miner-advanced" => "miner",
        "conveyor-express" => "conveyor-fast",
        _ => key
    };

    public static string? ItemKey(string itemId) => itemId switch
    {
        "iron-ore" or "copper-ore" or "coal" or "lead-ore" or "titanium-ore"
            or "iron-plate" or "lead-plate" or "titanium-plate"
            or "copper-wire" or "graphite" or "silicon" => itemId,
        _ => null
    };

    public static string? CategoryKey(UiTheme.BuildCategory category) => category switch
    {
        UiTheme.BuildCategory.Production => "cat-production",
        UiTheme.BuildCategory.Logistics => "cat-logistics",
        UiTheme.BuildCategory.Power => "cat-power",
        UiTheme.BuildCategory.Tools => "cat-tools",
        _ => null
    };

    /// <summary>Pure grid sizing helper (no GPU) for self-tests.</summary>
    public static (int Columns, int Rows, int Width, int Height) PlanAtlasGrid(int iconCount)
    {
        if (iconCount <= 0)
        {
            return (0, 0, 0, 0);
        }

        var columns = (int)Math.Ceiling(Math.Sqrt(iconCount));
        columns = Math.Max(1, columns);
        var rows = (int)Math.Ceiling(iconCount / (double)columns);
        var cellStride = CellSize + AtlasPadding;
        var width = columns * cellStride + AtlasPadding;
        var height = rows * cellStride + AtlasPadding;
        return (columns, rows, width, height);
    }
}
