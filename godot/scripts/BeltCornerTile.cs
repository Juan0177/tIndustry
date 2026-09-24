using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Full-cell rounded L corner (galleria). Overlaps abutting strips; tunnel mouth
/// ombrette at entry/exit when that opening is an effective extremity (neighbor
/// is not another corner). Base art: enter-west → exit-south.
/// </summary>
public partial class BeltCornerTile : Node2D
{
    private ShaderMaterial? _material;
    private Sprite2D? _sprite;

    public void Configure(
        GridPosition cell,
        Direction from,
        Direction to,
        int tileSize,
        float scrollPhaseTiles,
        bool shadeEntry = true,
        bool shadeExit = true)
    {
        EnsureVisual();
        _sprite!.Texture = CreateUnitTexture();
        _sprite.Centered = true;
        _sprite.Position = new Vector2((cell.X + 0.5f) * tileSize, (cell.Y + 0.5f) * tileSize);

        var clockwise = BeltLane.IsClockwiseTurn(from, to);
        var baseFrom = clockwise ? from : MirrorHorizontal(from);
        _sprite.RotationDegrees = baseFrom switch
        {
            Direction.East => 0f,
            Direction.South => 90f,
            Direction.West => 180f,
            Direction.North => -90f,
            _ => 0f
        };

        // Exact tile span so N/S rails share one Y with strips. Strips overlap
        // +2px under this corner; matching colors hide the butt.
        var span = (float)tileSize;
        _sprite.Scale = new Vector2(span, clockwise ? span : -span);

        // 0.42 matches full-tile straight body; SDF arc radius.
        _material!.SetShaderParameter("half_width", 0.42f);
        _material.SetShaderParameter("shade_entry", shadeEntry ? 1f : 0f);
        _material.SetShaderParameter("shade_exit", shadeExit ? 1f : 0f);
        _ = scrollPhaseTiles;
    }

    public void SetScroll(float scrollTiles) => _ = scrollTiles;

    private static Direction MirrorHorizontal(Direction d) => d switch
    {
        Direction.East => Direction.East,
        Direction.West => Direction.West,
        Direction.North => Direction.South,
        Direction.South => Direction.North,
        _ => d
    };

    private void EnsureVisual()
    {
        if (_sprite is not null)
        {
            return;
        }

        var shader = GD.Load<Shader>("res://shaders/belt_corner_v2.gdshader")
            ?? GD.Load<Shader>("res://shaders/belt_corner.gdshader");
        _material = new ShaderMaterial { Shader = shader };
        _sprite = new Sprite2D
        {
            Material = _material,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ZIndex = 2
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
