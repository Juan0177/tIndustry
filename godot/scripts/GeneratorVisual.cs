using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// 2×2 generator: concentric rings + 4 orbiting dots while a fuel block burns.
/// Dots freeze when idle; orbit restarts when a new fuel block starts (FuelConsumed↑).
/// </summary>
public partial class GeneratorVisual : Node2D
{
    private readonly List<Polygon2D> _dots = [];
    private float _angle;
    private float _orbitRadius;
    private long _lastFuelConsumed = -1;
    public GridPosition Origin { get; private set; }

    public static GeneratorVisual Create(GeneratorStub gen, int tileSize)
    {
        var root = new GeneratorVisual
        {
            Name = $"Generator_{gen.Position.X}_{gen.Position.Y}",
            ZIndex = 3,
            Origin = gen.Position
        };
        var span = GeneratorStub.Size * tileSize;
        var half = span * 0.5f;

        var pad = new Polygon2D
        {
            Color = new Color(0.23f, 0.20f, 0.11f),
            Polygon =
            [
                new(-half + 2, -half + 2),
                new(half - 2, -half + 2),
                new(half - 2, half - 2),
                new(-half + 2, half - 2)
            ]
        };
        root.AddChild(pad);

        var outline = new Line2D
        {
            Width = 2f,
            DefaultColor = new Color(0.83f, 0.71f, 0.29f),
            Antialiased = false,
            Closed = true,
            Points =
            [
                new(-half + 1, -half + 1),
                new(half - 1, -half + 1),
                new(half - 1, half - 1),
                new(-half + 1, half - 1)
            ],
            ZIndex = 2
        };
        root.AddChild(outline);

        var rOuter = span * 0.34f;
        var rInner = span * 0.18f;
        root.AddChild(Ring("Outer", rOuter, new Color(0.83f, 0.71f, 0.29f), 3f));
        root.AddChild(Ring("Inner", rInner, new Color(0.94f, 0.86f, 0.48f), 2f));
        root.AddChild(new Polygon2D
        {
            Name = "Hub",
            Color = new Color(0.11f, 0.09f, 0.06f),
            Polygon = Circle(span * 0.08f, 12)
        });

        root._orbitRadius = (rOuter + rInner) * 0.5f;
        var dotR = span * 0.035f;
        for (var i = 0; i < 4; i++)
        {
            var dot = new Polygon2D
            {
                Name = $"Dot{i}",
                Color = new Color(0.94f, 0.86f, 0.48f),
                Polygon = Circle(dotR, 8),
                ZIndex = 1
            };
            root._dots.Add(dot);
            root.AddChild(dot);
        }

        root._lastFuelConsumed = gen.FuelConsumed;
        root.PlaceDots(root._orbitRadius, 0f);
        root.Sync(gen, 0f);
        return root;
    }

    public void Sync(GeneratorStub gen, float delta)
    {
        if (gen.FuelConsumed != _lastFuelConsumed)
        {
            // New fuel block started — restart orbit from angle 0.
            _lastFuelConsumed = gen.FuelConsumed;
            _angle = 0f;
        }

        if (gen.IsGenerating)
        {
            _angle += delta * 2.6f;
            PlaceDots(_orbitRadius, _angle);
        }
        else
        {
            // Freeze at last angle (dots stop when fuel block ends).
            PlaceDots(_orbitRadius, _angle);
        }
    }

    private void PlaceDots(float radius, float baseAngle)
    {
        for (var i = 0; i < _dots.Count; i++)
        {
            var a = baseAngle + i * (Mathf.Tau / 4f) - Mathf.Pi * 0.5f;
            _dots[i].Position = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }
    }

    private static Line2D Ring(string name, float radius, Color color, float width)
    {
        const int segs = 32;
        var pts = new Vector2[segs];
        for (var i = 0; i < segs; i++)
        {
            var a = i * Mathf.Tau / segs;
            pts[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }

        return new Line2D
        {
            Name = name,
            Width = width,
            DefaultColor = color,
            Antialiased = false,
            Closed = true,
            Points = pts,
            ZIndex = 1
        };
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
