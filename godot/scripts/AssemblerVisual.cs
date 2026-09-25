using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// 2×2 assembler: two mirrored T-arms. Idle = open; crafting = press toward center.
/// </summary>
public partial class AssemblerVisual : Node2D
{
    private Node2D? _leftArm;
    private Node2D? _rightArm;
    private float _press; // 0 open → 1 closed
    private float _span;
    public GridPosition Origin { get; private set; }

    public static AssemblerVisual Create(SmelterStub asm, int tileSize)
    {
        var root = new AssemblerVisual
        {
            Name = $"Assembler_{asm.Position.X}_{asm.Position.Y}",
            ZIndex = 3,
            Origin = asm.Position
        };
        var span = SmelterStub.Size * tileSize;
        root._span = span;
        var half = span * 0.5f;

        // Pad without baked arms — draw arms procedurally.
        var pad = new Polygon2D
        {
            Name = "Pad",
            Color = new Color(0.20f, 0.24f, 0.28f),
            Polygon =
            [
                new(-half + 2, -half + 2),
                new(half - 2, -half + 2),
                new(half - 2, half - 2),
                new(-half + 2, half - 2)
            ]
        };
        root.AddChild(pad);
        var inset = new Polygon2D
        {
            Name = "Inset",
            Color = new Color(0.14f, 0.16f, 0.18f),
            Polygon =
            [
                new(-half + 8, -half + 8),
                new(half - 8, -half + 8),
                new(half - 8, half - 8),
                new(-half + 8, half - 8)
            ]
        };
        root.AddChild(inset);

        var outline = new Line2D
        {
            Width = 2f,
            DefaultColor = new Color(0.53f, 0.65f, 0.72f),
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

        var armColor = new Color(0.53f, 0.65f, 0.72f);
        var tipColor = new Color(0.77f, 0.48f, 0.23f);
        root._leftArm = MakeTArm("LeftArm", armColor, tipColor, mirrored: false, span);
        root._rightArm = MakeTArm("RightArm", armColor, tipColor, mirrored: true, span);
        root.AddChild(root._leftArm);
        root.AddChild(root._rightArm);

        root.ApplyPress(0f);
        root.Sync(asm, 0f);
        return root;
    }

    public void Sync(SmelterStub asm, float delta)
    {
        var target = asm.IsCrafting ? 1f : 0f;
        var speed = asm.IsCrafting ? 2.8f : 3.5f;
        _press = Mathf.MoveToward(_press, target, delta * speed);
        ApplyPress(_press);
    }

    private void ApplyPress(float t)
    {
        // Open rest ≈28% of span from center; closed almost meet.
        var open = 0.28f;
        var closed = 0.06f;
        var x = Mathf.Lerp(open, closed, t) * _span;
        if (_leftArm is not null)
        {
            _leftArm.Position = new Vector2(-x, 0);
        }

        if (_rightArm is not null)
        {
            _rightArm.Position = new Vector2(x, 0);
        }
    }

    private static Node2D MakeTArm(string name, Color body, Color tip, bool mirrored, float span)
    {
        var root = new Node2D { Name = name, ZIndex = 1 };
        var stemW = span * 0.08f;
        var stemH = span * 0.42f;
        var barW = span * 0.22f;
        var barH = span * 0.08f;
        var sign = mirrored ? -1f : 1f;

        // Vertical stem
        root.AddChild(new Polygon2D
        {
            Color = body,
            Polygon =
            [
                new(sign * -stemW, -stemH * 0.5f),
                new(sign * stemW, -stemH * 0.5f),
                new(sign * stemW, stemH * 0.5f),
                new(sign * -stemW, stemH * 0.5f)
            ]
        });
        // Horizontal bar toward center
        root.AddChild(new Polygon2D
        {
            Color = body,
            Polygon =
            [
                new(mirrored ? -barW : 0, -barH * 0.5f),
                new(mirrored ? 0 : barW, -barH * 0.5f),
                new(mirrored ? 0 : barW, barH * 0.5f),
                new(mirrored ? -barW : 0, barH * 0.5f)
            ]
        });
        // Tip accent at bar end
        var tipX = mirrored ? -barW : barW;
        root.AddChild(new Polygon2D
        {
            Color = tip,
            Polygon =
            [
                new(tipX - 3, -3),
                new(tipX + 3, -3),
                new(tipX + 3, 3),
                new(tipX - 3, 3)
            ]
        });
        return root;
    }
}
