using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Godot port of UiTheme.ItemColor — RGB from Shared <see cref="ItemVisualColors"/>.</summary>
public static class ItemPalette
{
    public static Color ColorFor(string itemId)
    {
        var (r, g, b) = ItemVisualColors.Rgb(itemId);
        return new Color(r / 255f, g / 255f, b / 255f);
    }

    public static Color OutlineFor(string itemId)
    {
        var (r, g, b) = ItemVisualColors.OutlineRgb(itemId);
        return new Color(r / 255f, g / 255f, b / 255f);
    }

    /// <summary>White-silhouette PNG → Raylib-style item tint.</summary>
    public static void Tint(CanvasItem node, string itemId) =>
        node.Modulate = ColorFor(itemId);

    public static Color IdleGrey => new(0.42f, 0.44f, 0.47f);
}
