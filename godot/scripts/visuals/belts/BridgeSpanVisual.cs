using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Thin bridge mid-span (~70% width): the repeated <c>conveyor-bridge-span</c> deck with 3 procedural
/// chevrons that light in sequence along flow; off chevrons are invisible.
/// </summary>
public partial class BridgeSpanVisual : Node2D
{
    /// <summary>Height of <c>conveyor-bridge-span.png</c> in art pixels (tools/art/blocchi/logistica.py).</summary>
    public const int DeckPixels = 22;

    /// <summary>Span thickness in cells (~0.70): the deck at the art's integer pixel scale, 44 px on a 64 px cell.</summary>
    public const float ThicknessScale = DeckPixels / (float)Sprites.PixelsPerCell;
    private const float CycleSeconds = 0.9f;

    private readonly List<Polygon2D> _arrows = [];
    private float _time;

    public void Configure(
        GridPosition entry,
        GridPosition exit,
        Direction direction,
        int tileSize)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _arrows.Clear();
        _time = 0f;

        var minX = Math.Min(entry.X, exit.X);
        var maxX = Math.Max(entry.X, exit.X);
        var minY = Math.Min(entry.Y, exit.Y);
        var maxY = Math.Max(entry.Y, exit.Y);

        float thicknessPx = tileSize * ThicknessScale;
        float lengthPx;
        Vector2 center;
        if (direction is Direction.East or Direction.West)
        {
            lengthPx = (maxX - minX + 1) * tileSize;
            center = new Vector2((minX + maxX + 1) * 0.5f * tileSize, (entry.Y + 0.5f) * tileSize);
        }
        else
        {
            lengthPx = (maxY - minY + 1) * tileSize;
            center = new Vector2((entry.X + 0.5f) * tileSize, (minY + maxY + 1) * 0.5f * tileSize);
        }

        Position = center;
        RotationDegrees = direction switch
        {
            Direction.East => 0f,
            Direction.South => 90f,
            Direction.West => 180f,
            Direction.North => -90f,
            _ => 0f
        };
        ZIndex = 2;

        // Deck sprite (local +X = flow): the 16 px span texture repeated along the whole length,
        // at the art's pixel scale (2 on a 64 px cell) both along the flow and across it.
        var deck = Sprites.For("conveyor-bridge-span");
        var pixelScale = tileSize / (float)Sprites.PixelsPerCell;
        AddChild(new Sprite2D
        {
            Name = "Deck",
            Texture = deck,
            Centered = true,
            RegionEnabled = true,
            RegionRect = new Rect2(0, 0, lengthPx / pixelScale, deck.GetHeight()),
            TextureRepeat = TextureRepeatEnum.Enabled,
            TextureFilter = TextureFilterEnum.Nearest,
            Scale = new Vector2(pixelScale, thicknessPx / deck.GetHeight())
        });
        var halfT = thicknessPx * 0.5f;

        // 3 chevrons centered along span (tip +local X).
        var spanInner = lengthPx * 0.55f;
        for (var i = 0; i < 3; i++)
        {
            var t = (i + 0.5f) / 3f;
            var x = -spanInner * 0.5f + t * spanInner;
            var arrow = MakeChevron(x, halfT * 0.55f);
            arrow.Name = $"Arrow_{i}";
            arrow.Modulate = new Color(1, 1, 1, 0);
            AddChild(arrow);
            _arrows.Add(arrow);
        }
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        // Sequence: 0 lit → 1 lit → 2 lit → all off → repeat.
        var phase = (int)(_time / (CycleSeconds / 4f)) % 4;
        for (var i = 0; i < _arrows.Count; i++)
        {
            var on = phase < 3 && phase == i;
            _arrows[i].Modulate = on
                ? new Color(0.525f, 0.655f, 0.722f, 1f) // Blu4
                : new Color(1, 1, 1, 0);
        }
    }

    private static Polygon2D MakeChevron(float cx, float halfH)
    {
        // Tip points +X (flow).
        var tip = 7f;
        var back = 6f;
        return new Polygon2D
        {
            Color = Colors.White,
            Polygon =
            [
                new(cx + tip, 0),
                new(cx - back, -halfH),
                new(cx - back * 0.35f, 0),
                new(cx - back, halfH)
            ]
        };
    }
}
