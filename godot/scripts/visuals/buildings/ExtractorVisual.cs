using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// 1×1 extractor: the <c>extractor</c> sprite + a filter dot in its empty centre well (grey → item color), and a small
/// puff when it pulls an item (G3 effect <c>sbuffo-estrattore</c>, at most one every <see cref="PuffSeconds"/>).
/// </summary>
public partial class ExtractorVisual : Node2D
{
    /// <summary>Shortest time between two puffs of one extractor (it pulls an item every 0.4 s).</summary>
    private const double PuffSeconds = 0.8;

    private Polygon2D? _dot;
    private long _extracted;
    private double _nextPuff;
    public GridPosition Origin { get; private set; }

    public static ExtractorVisual Create(Extractor ex, int tileSize)
    {
        var root = new ExtractorVisual
        {
            Name = $"Extractor_{ex.Position.X}_{ex.Position.Y}",
            ZIndex = 3,
            Origin = ex.Position,
            _extracted = ex.ItemsExtracted
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
        root.Sync(ex, null);
        return root;
    }

    public void Sync(Extractor ex, EffectManager? fx)
    {
        if (fx is not null && ex.ItemsExtracted != _extracted)
        {
            _extracted = ex.ItemsExtracted;
            if (fx.Clock >= _nextPuff)
            {
                _nextPuff = fx.Clock + PuffSeconds;
                fx.Play("sbuffo-estrattore", GlobalPosition);
            }
        }

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
