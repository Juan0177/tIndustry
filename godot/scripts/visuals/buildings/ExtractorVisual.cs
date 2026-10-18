using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>1×1 extractor: the <c>extractor</c> sprite + a filter dot in its empty centre well (grey → item color).</summary>
public partial class ExtractorVisual : Node2D
{
    private Polygon2D? _dot;
    public GridPosition Origin { get; private set; }

    public static ExtractorVisual Create(Extractor ex, int tileSize)
    {
        var root = new ExtractorVisual
        {
            Name = $"Extractor_{ex.Position.X}_{ex.Position.Y}",
            ZIndex = 3,
            Origin = ex.Position
        };
        root.AddChild(BuildingPad.Layer("Block", Sprites.For("extractor"), Extractor.Size, tileSize, 0));

        // The sprite's dark well is 8 px across (radius 0.125 cell); the dot sits inside it.
        var r = tileSize * 0.11f;
        root._dot = new Polygon2D
        {
            Name = "FilterDot",
            Color = ItemPalette.IdleGrey,
            Polygon = Circle(r, 12),
            ZIndex = 1
        };
        root.AddChild(root._dot);
        root.Sync(ex);
        return root;
    }

    public void Sync(Extractor ex)
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
