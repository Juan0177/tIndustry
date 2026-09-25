using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>1×1 extractor: rounded pad + center filter dot (grey → item color).</summary>
public partial class ExtractorVisual : Node2D
{
    private Polygon2D? _dot;
    public GridPosition Origin { get; private set; }

    public static ExtractorVisual Create(ExtractorStub ex, int tileSize)
    {
        var root = new ExtractorVisual
        {
            Name = $"Extractor_{ex.Position.X}_{ex.Position.Y}",
            ZIndex = 3,
            Origin = ex.Position
        };
        var padTex = GD.Load<Texture2D>("res://assets/extractor.png");
        // Stretch the 64×64 pad; baked center is covered by the live filter dot.
        var pad = new Sprite2D
        {
            Texture = padTex,
            Centered = true,
            TextureFilter = TextureFilterEnum.Nearest,
            Scale = new Vector2(tileSize / (float)padTex.GetWidth(), tileSize / (float)padTex.GetHeight())
        };
        root.AddChild(pad);

        // Cover baked palette dot so recolor is unambiguous.
        var coverR = tileSize * 0.13f;
        root.AddChild(new Polygon2D
        {
            Name = "DotWell",
            Color = new Color(0.10f, 0.11f, 0.12f),
            Polygon = Circle(coverR, 12),
            ZIndex = 1
        });
        var r = tileSize * 0.11f;
        root._dot = new Polygon2D
        {
            Name = "FilterDot",
            Color = ItemPalette.IdleGrey,
            Polygon = Circle(r, 12),
            ZIndex = 2
        };
        root.AddChild(root._dot);
        root.Sync(ex);
        return root;
    }

    public void Sync(ExtractorStub ex)
    {
        if (_dot is null)
        {
            return;
        }

        _dot.Color = string.IsNullOrWhiteSpace(ex.FilterItemId)
            ? ItemPalette.IdleGrey
            : ItemPalette.ColorFor(ex.FilterItemId);
    }

    private static Vector2[] Circle(float radius, int segments)
    {
        var pts = new Vector2[segments];
        for (var i = 0; i < segments; i++)
        {
            var a = i * Mathf.Tau / segments;
            pts[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }

        return pts;
    }
}
