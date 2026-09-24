using Godot;

namespace TIndustry.Godot;

/// <summary>
/// Opaque footprint box + border with optional centered icon (miner/forno/assemblatore).
/// </summary>
public static class BuildingPad
{
    public static Node2D Create(
        int footprintTiles,
        int tileSize,
        Color fill,
        Color border,
        Texture2D? icon,
        float iconScale = 1f)
    {
        var root = new Node2D { ZIndex = 3 };
        var half = footprintTiles * tileSize * 0.5f - 1f;
        Vector2[] box =
        [
            new(-half, -half),
            new(half, -half),
            new(half, half),
            new(-half, half)
        ];

        var pad = new Polygon2D
        {
            Name = "Pad",
            Polygon = box,
            Color = fill,
            ZIndex = 0
        };
        root.AddChild(pad);

        var outline = new Line2D
        {
            Name = "Border",
            Width = 3.5f,
            DefaultColor = border,
            Antialiased = false,
            Closed = true,
            Points = box,
            ZIndex = 1
        };
        root.AddChild(outline);

        if (icon is not null)
        {
            var sprite = new Sprite2D
            {
                Name = "Icon",
                Texture = icon,
                Centered = true,
                Scale = Vector2.One * iconScale,
                Modulate = Colors.White,
                ZIndex = 2
            };
            root.AddChild(sprite);
        }

        return root;
    }
}
