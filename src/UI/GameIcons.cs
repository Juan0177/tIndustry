using System.Numerics;
using Raylib_cs;

namespace TIndustry.Logistics;

/// <summary>
/// Loads PNG icons from assets/icons and draws them as Raylib textures.
/// White-on-transparent glyphs are tinted at draw time.
/// </summary>
public static class GameIcons
{
    private static readonly Dictionary<string, Texture2D> Textures = new(StringComparer.Ordinal);
    private static bool loaded;

    public static void Load()
    {
        if (loaded)
        {
            return;
        }

        var root = Path.Combine(AppContext.BaseDirectory, "assets", "icons");
        TryLoad(root, "items/money.png", "money");
        TryLoad(root, "items/iron-ore.png", "iron-ore");
        TryLoad(root, "items/copper-ore.png", "copper-ore");
        TryLoad(root, "items/coal.png", "coal");
        TryLoad(root, "items/iron-plate.png", "iron-plate");
        TryLoad(root, "items/copper-wire.png", "copper-wire");

        TryLoad(root, "buildings/miner.png", "miner");
        TryLoad(root, "buildings/smelter.png", "smelter");
        TryLoad(root, "buildings/assembler.png", "assembler");
        TryLoad(root, "buildings/generator.png", "generator");
        TryLoad(root, "buildings/conveyor-basic.png", "conveyor-basic");
        TryLoad(root, "buildings/conveyor-fast.png", "conveyor-fast");
        TryLoad(root, "buildings/junction.png", "junction");
        TryLoad(root, "buildings/splitter.png", "splitter");
        TryLoad(root, "buildings/sorter.png", "sorter");
        TryLoad(root, "buildings/bridge.png", "bridge");
        TryLoad(root, "buildings/remove.png", "remove");

        TryLoad(root, "ui/dir-n.png", "dir-n");
        TryLoad(root, "ui/dir-e.png", "dir-e");
        TryLoad(root, "ui/dir-s.png", "dir-s");
        TryLoad(root, "ui/dir-w.png", "dir-w");
        TryLoad(root, "ui/settings.png", "settings");
        TryLoad(root, "ui/research.png", "research");
        TryLoad(root, "ui/menu.png", "menu");
        TryLoad(root, "ui/locked.png", "locked");
        TryLoad(root, "ui/power.png", "power");
        TryLoad(root, "ui/sell.png", "sell");

        TryLoad(root, "categories/production.png", "cat-production");
        TryLoad(root, "categories/logistics.png", "cat-logistics");
        TryLoad(root, "categories/power.png", "cat-power");
        TryLoad(root, "categories/tools.png", "cat-tools");

        loaded = true;
    }

    public static void Unload()
    {
        if (!loaded)
        {
            return;
        }

        foreach (var texture in Textures.Values)
        {
            Raylib.UnloadTexture(texture);
        }

        Textures.Clear();
        loaded = false;
    }

    public static bool Has(string key) => Textures.ContainsKey(key);

    public static int LoadedCount => Textures.Count;

    public static void Draw(string key, int x, int y, int size, Color tint)
    {
        if (!Textures.TryGetValue(key, out var texture))
        {
            return;
        }

        var src = new Rectangle(0, 0, texture.Width, texture.Height);
        var dst = new Rectangle(x, y, size, size);
        Raylib.DrawTexturePro(texture, src, dst, Vector2.Zero, 0f, tint);
    }

    public static bool TryDraw(string key, int x, int y, int size, Color tint)
    {
        if (!Textures.ContainsKey(key))
        {
            return false;
        }

        Draw(key, x, y, size, tint);
        return true;
    }

    public static string? ItemKey(string itemId) => itemId switch
    {
        "iron-ore" or "copper-ore" or "coal" or "iron-plate" or "copper-wire" => itemId,
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

    private static void TryLoad(string root, string relativePath, string key)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            return;
        }

        var texture = Raylib.LoadTexture(path);
        Raylib.SetTextureFilter(texture, TextureFilter.Bilinear);
        Textures[key] = texture;
    }
}
