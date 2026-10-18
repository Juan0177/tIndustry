using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Flat item colours from Shared <see cref="ItemVisualColors"/>, for markers that are not item icons
/// (extractor filter dot, deposit tint under miners, zoomed-out deposit dots). Item icons are drawn in their own
/// colours (<see cref="Sprites.Item"/>, <see cref="Sprites.BeltItem"/>) and are never tinted.
/// </summary>
public static class ItemPalette
{
    public static Color ColorFor(string itemId)
    {
        var (r, g, b) = ItemVisualColors.Rgb(itemId);
        return new Color(r / 255f, g / 255f, b / 255f);
    }

    public static Color IdleGrey => new(0.42f, 0.44f, 0.47f);
}
