using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// How a block about to be built looks, Mindustry style: its sprite half transparent (belts turned to their
/// direction), a footprint outline (accent when it can go there, red when refused, the sprite then tinted red too and
/// crossed by an X, so the refusal does not rest on colour alone) and, for blocks with a direction, a small arrow on
/// the side they face. Used by the placement ghost under the mouse and by
/// the local player's plans (a belt drag), so both read the same.
/// </summary>
public static class PlacementPreview
{
    /// <summary>Alpha of the block sprite.</summary>
    public const float SpriteAlpha = 0.5f;

    /// <summary>Alpha of the block sprite of a plan out of the drone's build range (fainter than a plan in range).</summary>
    public const float OutOfRangeSpriteAlpha = 0.22f;

    /// <summary>Outline width in world px (a cell is 64).</summary>
    public const float OutlineWidth = 3f;

    /// <summary>Dash length of the out of range outline, world px (a cell is 64: three dashes a side).</summary>
    public const float DashLength = 10f;

    /// <summary>
    /// Draws the preview of <paramref name="structureId"/> over <paramref name="rect"/> (world px) on
    /// <paramref name="canvas"/>. <paramref name="turnSprite"/>: the sprite is turned to <paramref name="direction"/>
    /// (belts); <paramref name="arrow"/>: a direction arrow is drawn (every block placed with a direction). A null
    /// <paramref name="structureId"/> draws only the outline (and the X when refused): one cell of a bigger block.
    /// <paramref name="outOfRange"/>: a plan the drone cannot reach from where it is, drawn fainter with a dashed
    /// <see cref="UiColors.FuoriRaggio"/> outline (<see cref="DrawOutOfRange"/>) instead of the solid one.
    /// </summary>
    public static void Draw(CanvasItem canvas, string? structureId, Rect2 rect, Direction direction, bool turnSprite,
        bool arrow, bool valid, bool outOfRange = false)
    {
        var outline = valid ? UiColors.FantasmaValido : UiColors.FantasmaNonValido;
        var tint = outOfRange ? new Color(1f, 1f, 1f, OutOfRangeSpriteAlpha)
            : valid ? new Color(1f, 1f, 1f, SpriteAlpha)
            : UiColors.FantasmaNonValido with { A = SpriteAlpha };
        if (structureId is not null && Sprites.TryFor(structureId) is { } tex)
        {
            if (turnSprite)
            {
                canvas.DrawSetTransform(rect.GetCenter(), Angle(direction), Vector2.One);
                canvas.DrawTextureRect(tex, new Rect2(-rect.Size / 2f, rect.Size), false, tint);
                canvas.DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            }
            else
            {
                canvas.DrawTextureRect(tex, rect, false, tint);
            }
        }

        if (outOfRange)
        {
            DrawOutOfRange(canvas, rect);
            if (arrow)
            {
                DrawArrow(canvas, rect, direction, UiColors.FuoriRaggio with { A = 0.6f });
            }

            return;
        }

        // The high-contrast variant draws every sign twice: a wider dark stroke first, then the colour.
        var under = UiColors.SottoSegno;
        foreach (var (color, extra) in under.A > 0f ? new[] { (under, 4f), (outline, 0f) } : new[] { (outline, 0f) })
        {
            var width = OutlineWidth + extra;
            canvas.DrawRect(rect.Grow(-OutlineWidth / 2f), color, filled: false, width: width);
            if (!valid)
            {
                var cross = rect.Grow(-Mathf.Min(rect.Size.X, rect.Size.Y) * 0.22f);
                canvas.DrawLine(cross.Position, cross.End, color, width);
                canvas.DrawLine(new Vector2(cross.End.X, cross.Position.Y), new Vector2(cross.Position.X, cross.End.Y), color,
                    width);
            }

            if (arrow)
            {
                DrawArrow(canvas, rect, direction, color, extra);
            }
        }
    }

    /// <summary>
    /// The "fuori raggio" outline: dashes of <see cref="UiColors.FuoriRaggio"/> round <paramref name="rect"/> (over the
    /// dark under-stroke in the high-contrast variant), so a waiting plan reads apart from one in range without colour.
    /// </summary>
    public static void DrawOutOfRange(CanvasItem canvas, Rect2 rect)
    {
        var box = rect.Grow(-OutlineWidth / 2f);
        var under = UiColors.SottoSegno;
        if (under.A > 0f)
        {
            DrawDashedRect(canvas, box, under, OutlineWidth + 4f);
        }

        DrawDashedRect(canvas, box, UiColors.FuoriRaggio, OutlineWidth);
    }

    private static void DrawDashedRect(CanvasItem canvas, Rect2 box, Color color, float width)
    {
        var a = box.Position;
        var b = new Vector2(box.End.X, box.Position.Y);
        var c = box.End;
        var d = new Vector2(box.Position.X, box.End.Y);
        canvas.DrawDashedLine(a, b, color, width, DashLength);
        canvas.DrawDashedLine(b, c, color, width, DashLength);
        canvas.DrawDashedLine(c, d, color, width, DashLength);
        canvas.DrawDashedLine(d, a, color, width, DashLength);
    }

    /// <summary>
    /// True for blocks placed with a direction that matters: belts (<paramref name="kind"/> null), bridges and
    /// assemblers (their output side). Junctions, routers, sorters and extractors have none (Mindustry).
    /// </summary>
    public static bool HasDirection(BuildingKind? kind) =>
        kind is null or BuildingKind.Bridge or BuildingKind.BridgeLong or BuildingKind.Assembler;

    /// <summary>Angle of <paramref name="direction"/> (East = 0, clockwise on screen).</summary>
    public static float Angle(Direction direction)
    {
        var (dx, dy) = DirectionMath.ToOffset(direction);
        return Mathf.Atan2(dy, dx);
    }

    /// <summary>
    /// Diagonal stripes (top-left to bottom-right, every <paramref name="spacing"/> px) clipped to
    /// <paramref name="rect"/>: the hatching that marks a block to take apart, besides its colour.
    /// </summary>
    public static void DrawHatch(CanvasItem canvas, Rect2 rect, Color color, float spacing, float width)
    {
        var (w, h) = (rect.Size.X, rect.Size.Y);
        // Lines y = x - c in rect-local coordinates, c from -h to w.
        for (var c = -h + spacing / 2f; c < w; c += spacing)
        {
            var x0 = Mathf.Max(0f, c);
            var x1 = Mathf.Min(w, h + c);
            if (x1 > x0)
            {
                canvas.DrawLine(rect.Position + new Vector2(x0, x0 - c), rect.Position + new Vector2(x1, x1 - c), color, width);
            }
        }
    }

    /// <summary>
    /// A small filled triangle (grown by <paramref name="grow"/> px) inside the edge the block faces, pointing out of it.
    /// </summary>
    private static void DrawArrow(CanvasItem canvas, Rect2 rect, Direction direction, Color color, float grow = 0f)
    {
        var (dx, dy) = DirectionMath.ToOffset(direction);
        var forward = new Vector2(dx, dy);
        var side = new Vector2(-dy, dx);
        var half = Mathf.Min(rect.Size.X, rect.Size.Y) / 2f;
        var tip = rect.GetCenter() + forward * (half - OutlineWidth * 2f);
        var back = tip - forward * half * 0.35f;
        var wing = half * 0.25f;
        // grow > 0: the same triangle a little bigger (the dark under-stroke).
        var centre = (tip + back) / 2f;
        Vector2 Out(Vector2 p) => p + (p - centre).Normalized() * grow;
        canvas.DrawColoredPolygon([Out(tip), Out(back + side * wing), Out(back - side * wing)], color);
    }
}
