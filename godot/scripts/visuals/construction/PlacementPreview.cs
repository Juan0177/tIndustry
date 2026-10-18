using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// How a block about to be built looks, Mindustry style: its sprite half transparent (belts turned to their
/// direction), a footprint outline (accent when it can go there, red when refused, the sprite then tinted red too) and,
/// for blocks with a direction, a small arrow on the side they face. Used by the placement ghost under the mouse and by
/// the local player's plans (a belt drag), so both read the same.
/// </summary>
public static class PlacementPreview
{
    /// <summary>Alpha of the block sprite.</summary>
    public const float SpriteAlpha = 0.5f;

    /// <summary>Outline width in world px (a cell is 64).</summary>
    public const float OutlineWidth = 3f;

    /// <summary>
    /// Draws the preview of <paramref name="structureId"/> over <paramref name="rect"/> (world px) on
    /// <paramref name="canvas"/>. <paramref name="turnSprite"/>: the sprite is turned to <paramref name="direction"/>
    /// (belts); <paramref name="arrow"/>: a direction arrow is drawn (every block placed with a direction).
    /// </summary>
    public static void Draw(CanvasItem canvas, string structureId, Rect2 rect, Direction direction, bool turnSprite,
        bool arrow, bool valid)
    {
        var outline = valid ? UiColors.FantasmaValido : UiColors.FantasmaNonValido;
        var tint = valid ? new Color(1f, 1f, 1f, SpriteAlpha) : UiColors.FantasmaNonValido with { A = SpriteAlpha };
        if (Sprites.TryFor(structureId) is { } tex)
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

        canvas.DrawRect(rect.Grow(-OutlineWidth / 2f), outline, filled: false, width: OutlineWidth);
        if (arrow)
        {
            DrawArrow(canvas, rect, direction, outline);
        }
    }

    /// <summary>
    /// True for blocks placed with a direction that matters: belts (<paramref name="kind"/> null), splitters, sorters,
    /// bridges, extractors and assemblers (their output side).
    /// </summary>
    public static bool HasDirection(BuildingKind? kind) =>
        kind is null or BuildingKind.Splitter or BuildingKind.Sorter or BuildingKind.Bridge or BuildingKind.Extractor
            or BuildingKind.Assembler;

    /// <summary>Angle of <paramref name="direction"/> (East = 0, clockwise on screen).</summary>
    public static float Angle(Direction direction)
    {
        var (dx, dy) = DirectionMath.ToOffset(direction);
        return Mathf.Atan2(dy, dx);
    }

    /// <summary>A small filled triangle inside the edge the block faces, pointing out of it.</summary>
    private static void DrawArrow(CanvasItem canvas, Rect2 rect, Direction direction, Color color)
    {
        var (dx, dy) = DirectionMath.ToOffset(direction);
        var forward = new Vector2(dx, dy);
        var side = new Vector2(-dy, dx);
        var half = Mathf.Min(rect.Size.X, rect.Size.Y) / 2f;
        var tip = rect.GetCenter() + forward * (half - OutlineWidth * 2f);
        var back = tip - forward * half * 0.35f;
        var wing = half * 0.25f;
        canvas.DrawColoredPolygon([tip, back + side * wing, back - side * wing], color);
    }
}
