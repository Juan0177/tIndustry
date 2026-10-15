using Godot;

namespace TIndustry.Godot;

/// <summary>
/// Full-footprint block sprite (opaque tile art) with optional thin edge highlight.
/// Used for in-world buildings so map + palette share the same sprites.
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

        if (icon is not null)
        {
            // Stretch the 64×64 block art across the footprint (nearest-neighbor via TextureFilter).
            var span = footprintTiles * tileSize;
            var sprite = new Sprite2D
            {
                Name = "Block",
                Texture = icon,
                Centered = true,
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                Scale = new Vector2(span / icon.GetWidth(), span / icon.GetHeight()) * iconScale,
                Modulate = Colors.White,
                ZIndex = 0
            };
            root.AddChild(sprite);

            var outline = new Line2D
            {
                Name = "Border",
                Width = 2f,
                DefaultColor = border,
                Antialiased = false,
                Closed = true,
                Points = box,
                ZIndex = 1
            };
            root.AddChild(outline);
            return root;
        }

        var pad = new Polygon2D
        {
            Name = "Pad",
            Polygon = box,
            Color = fill,
            ZIndex = 0
        };
        root.AddChild(pad);

        var fallbackOutline = new Line2D
        {
            Name = "Border",
            Width = 3.5f,
            DefaultColor = border,
            Antialiased = false,
            Closed = true,
            Points = box,
            ZIndex = 1
        };
        root.AddChild(fallbackOutline);
        return root;
    }
}
