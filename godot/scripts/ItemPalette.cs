using Godot;

namespace TIndustry.Godot;

/// <summary>Godot port of UiTheme.ItemColor for extractor filter dots.</summary>
public static class ItemPalette
{
    public static Color ColorFor(string itemId) => itemId switch
    {
        "iron-ore" => new Color(0.66f, 0.69f, 0.74f),
        "iron-plate" => new Color(0.77f, 0.82f, 0.88f),
        "copper-ore" => new Color(0.91f, 0.50f, 0.19f),
        "copper-wire" => new Color(0.94f, 0.60f, 0.16f),
        "coal" => new Color(0.23f, 0.21f, 0.20f),
        "lead-ore" => new Color(0.47f, 0.55f, 0.66f),
        "lead-plate" => new Color(0.58f, 0.64f, 0.74f),
        "titanium-ore" => new Color(0.47f, 0.78f, 0.82f),
        "titanium-plate" => new Color(0.67f, 0.90f, 0.93f),
        "graphite" => new Color(0.35f, 0.36f, 0.38f),
        "silicon" => new Color(0.82f, 0.78f, 0.47f),
        _ => new Color(0.42f, 0.44f, 0.47f) // idle grey
    };

    public static Color IdleGrey => new(0.42f, 0.44f, 0.47f);
}
