using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// 2×2 assembler in layers: still base (<c>assembler-base</c>) and two arms (<c>assembler-arm</c>, the right
/// one mirrored) sliding on the rails. Idle = open; crafting = press toward the centre.
/// </summary>
public partial class AssemblerVisual : Node2D
{
    private Sprite2D? _leftArm;
    private Sprite2D? _rightArm;
    private float _press; // 0 open → 1 closed
    private float _span;
    private float _artPixel;
    public GridPosition Origin { get; private set; }

    public static AssemblerVisual Create(Crafter asm, int tileSize)
    {
        var root = new AssemblerVisual
        {
            Name = $"Assembler_{asm.Position.X}_{asm.Position.Y}",
            ZIndex = 3,
            Origin = asm.Position
        };
        root._span = Crafter.Size * tileSize;
        root._artPixel = tileSize / (float)Sprites.PixelsPerCell;

        root.AddChild(BuildingPad.Layer("Base", Sprites.For("assembler-base"), Crafter.Size, tileSize, 0));
        var arm = Sprites.For("assembler-arm");
        root._leftArm = BuildingPad.Layer("LeftArm", arm, Crafter.Size, tileSize, 1);
        root._rightArm = BuildingPad.Layer("RightArm", arm, Crafter.Size, tileSize, 1);
        root._rightArm.FlipH = true;
        root.AddChild(root._leftArm);
        root.AddChild(root._rightArm);

        root.ApplyPress(0f);
        root.Sync(asm, 0f);
        return root;
    }

    public void Sync(Crafter asm, float delta)
    {
        var target = asm.IsCrafting ? 1f : 0f;
        var speed = asm.IsCrafting ? 2.8f : 3.5f;
        // Floor delta so press settles under uncapped headless frame rates.
        var step = Mathf.Max(delta, 1f / 30f) * speed;
        _press = Mathf.MoveToward(_press, target, step);
        ApplyPress(_press);
    }

    private void ApplyPress(float t)
    {
        // Open rest ≈28% of span from center; closed almost meet. Snapped to whole art pixels.
        var open = 0.28f;
        var closed = 0.06f;
        var x = Mathf.Round(Mathf.Lerp(open, closed, t) * _span / _artPixel) * _artPixel;
        if (_leftArm is not null)
        {
            _leftArm.Position = new Vector2(-x, 0);
        }

        if (_rightArm is not null)
        {
            _rightArm.Position = new Vector2(x, 0);
        }
    }
}
