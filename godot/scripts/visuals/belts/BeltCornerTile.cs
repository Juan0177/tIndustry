using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// One belt corner: a frame of the <c>{id}-corner-anim</c> strip. Base art enters from the West edge
/// and leaves from the South edge (incoming East → outgoing South, clockwise); other turns rotate
/// it, counter-clockwise turns mirror it first.
/// </summary>
public partial class BeltCornerTile : Sprite2D
{
    public void Configure(GridPosition cell, Direction from, Direction to, int tileSize, Texture2D frame)
    {
        Texture = frame;
        Centered = true;
        TextureFilter = TextureFilterEnum.Nearest;
        Position = new Vector2((cell.X + 0.5f) * tileSize, (cell.Y + 0.5f) * tileSize);
        Scale = Vector2.One * Sprites.ScaleToCells(frame, 1, tileSize);
        var (rotation, flipX) = ResolveTransform(from, to);
        RotationDegrees = rotation;
        FlipH = flipX;
        ZIndex = 2;
    }

    /// <summary>
    /// Godot 2D: positive rotation is clockwise. FlipX mirrors the CW base into CCW.
    /// </summary>
    internal static (float RotationDegrees, bool FlipX) ResolveTransform(Direction from, Direction to)
    {
        var clockwise = BeltLane.IsClockwiseTurn(from, to);
        if (clockwise)
        {
            return from switch
            {
                Direction.East => (0f, false),
                Direction.South => (90f, false),
                Direction.West => (180f, false),
                Direction.North => (-90f, false),
                _ => (0f, false)
            };
        }

        return from switch
        {
            Direction.West => (0f, true),
            Direction.South => (-90f, true), // S→E (Z bottom)
            Direction.East => (180f, true),
            Direction.North => (90f, true),
            _ => (0f, true)
        };
    }
}
