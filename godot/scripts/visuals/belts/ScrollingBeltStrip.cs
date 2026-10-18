using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// One straight belt cell: a frame of the <c>{id}-anim</c> strip (drawn flowing East) rotated to the
/// belt's direction. The frame is chosen by <see cref="MindustryBeltVisual"/> from a clock shared by
/// every cell of the same tier, so runs scroll in step.
/// </summary>
public partial class ScrollingBeltStrip : Sprite2D
{
    public void Configure(GridPosition cell, Direction direction, int tileSize, Texture2D frame)
    {
        Texture = frame;
        Centered = true;
        TextureFilter = TextureFilterEnum.Nearest;
        Position = new Vector2((cell.X + 0.5f) * tileSize, (cell.Y + 0.5f) * tileSize);
        Scale = Vector2.One * Sprites.ScaleToCells(frame, 1, tileSize);
        RotationDegrees = RotationFor(direction);
        ZIndex = 1;
    }

    /// <summary>Godot 2D rotation (clockwise) that turns an East-facing sprite to <paramref name="direction"/>.</summary>
    internal static float RotationFor(Direction direction) => direction switch
    {
        Direction.South => 90f,
        Direction.West => 180f,
        Direction.North => -90f,
        _ => 0f
    };
}
