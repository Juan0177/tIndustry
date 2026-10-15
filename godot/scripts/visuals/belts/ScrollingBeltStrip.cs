using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Continuous Mindustry-style straight belt strip. Chevrons tip with flow (+local X);
/// scroll is driven by <see cref="MindustryBeltVisual"/> (shared phase across L segments).
/// Regular belts occupy the <b>full tile</b> — outer rails sit on tile edges (no green margin).
/// Thin (~0.78) thickness is reserved for bridges only — never use it here.
/// </summary>
public partial class ScrollingBeltStrip : Node2D
{
    private ShaderMaterial? _material;
    private Sprite2D? _sprite;

    public void Configure(
        IReadOnlyList<GridPosition> path,
        Direction direction,
        int tileSize,
        float scrollPhaseTiles = 0f,
        float thicknessScale = 1f,
        bool accentRails = false)
    {
        if (path.Count == 0)
        {
            throw new ArgumentException("Path vuoto.", nameof(path));
        }

        var cellCount = path.Count;
        var first = path[0];
        var last = path[^1];
        var minX = Math.Min(first.X, last.X);
        var maxX = Math.Max(first.X, last.X);
        var minY = Math.Min(first.Y, last.Y);
        var maxY = Math.Max(first.Y, last.Y);

        // Exact tile thickness (integer N/S rails) unless bridge thin-span override.
        // +2px length under corners so the corner (ZIndex above) covers the butt.
        var scale = Math.Clamp(thicknessScale, 0.2f, 1f);
        float thicknessPx = tileSize * scale;
        float lengthPx;
        Vector2 center;
        if (direction is Direction.East or Direction.West)
        {
            lengthPx = (maxX - minX + 1) * tileSize + (scale >= 0.99f ? 2f : 0f);
            center = new Vector2((minX + maxX + 1) * 0.5f * tileSize, (first.Y + 0.5f) * tileSize);
        }
        else
        {
            lengthPx = (maxY - minY + 1) * tileSize + (scale >= 0.99f ? 2f : 0f);
            center = new Vector2((first.X + 0.5f) * tileSize, (minY + maxY + 1) * 0.5f * tileSize);
        }

        EnsureVisual();
        _sprite!.Texture = CreateUnitTexture();
        _sprite.Centered = true;
        _sprite.Position = center;
        _sprite.Scale = new Vector2(lengthPx, thicknessPx);
        _sprite.RotationDegrees = direction switch
        {
            Direction.East => 0f,
            Direction.South => 90f,
            Direction.West => 180f,
            Direction.North => -90f,
            _ => 0f
        };

        _material!.SetShaderParameter("cell_count", (float)Math.Max(1, cellCount));
        _material.SetShaderParameter("marks_per_tile", 2.0f);
        _material.SetShaderParameter("scroll_phase", scrollPhaseTiles);
        _material.SetShaderParameter("scroll", 0f);
        // T2: Blu4 bordino only — same single arrow row as T1.
        _material.SetShaderParameter("accent_rails", accentRails ? 1.0f : 0.0f);

        // Bridge thin-span: raise above underpass belts.
        _sprite.ZIndex = scale < 0.99f ? 2 : 1;
    }

    public void SetScroll(float scrollTiles)
    {
        _material?.SetShaderParameter("scroll", scrollTiles);
    }

    private void EnsureVisual()
    {
        if (_sprite is not null)
        {
            return;
        }

        var shader = GD.Load<Shader>("res://shaders/belt_scroll.gdshader");
        _material = new ShaderMaterial { Shader = shader };
        _sprite = new Sprite2D
        {
            Material = _material,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ZIndex = 1,
        };
        AddChild(_sprite);
    }

    private static ImageTexture CreateUnitTexture()
    {
        var img = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        img.SetPixel(0, 0, Colors.White);
        return ImageTexture.CreateFromImage(img);
    }
}
