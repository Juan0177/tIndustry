using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Full-cell corner tile: opaque square covering 100% of the cell (no green in the
/// unused quadrant). Outer rails align with <see cref="ScrollingBeltStrip"/>;
/// recessed L channel joins the track band. Base art: enter-west → exit-south.
/// </summary>
public partial class BeltCornerTile : Node2D
{
    private ShaderMaterial? _material;
    private Sprite2D? _sprite;

    public void Configure(GridPosition cell, Direction from, Direction to, int tileSize, float scrollPhaseTiles)
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
        // Full tile + 1px seal into adjacent straights (corner draws above strips).
        var span = tileSize + 1f;
        _sprite.Scale = new Vector2(span, clockwise ? span : -span);

        // Narrower groove so unused quadrant clearly shows base_color (square fill).
        _material!.SetShaderParameter("half_width", 0.32f);
        _ = scrollPhaseTiles;
    }

    /// <summary>No-op: corner does not scroll marks.</summary>
    public void SetScroll(float scrollTiles)
    {
        _ = scrollTiles;
    }

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
