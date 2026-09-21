using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Corner cell as a static blue metal platform (no scrolling arrows).
/// Base art: enter-west → exit-south; rotation / Y-flip covers other 90° turns.
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
        // Base art: East→South (clockwise). Map other turns via rotation + optional Y flip.
        var baseFrom = clockwise ? from : MirrorHorizontal(from);
        _sprite.RotationDegrees = baseFrom switch
        {
            Direction.East => 0f,
            Direction.South => 90f,
            Direction.West => 180f,
            Direction.North => -90f,
            _ => 0f
        };
        // Slight overscale covers 1px seams against adjacent straight strips.
        var span = tileSize + 2f;
        _sprite.Scale = new Vector2(span, clockwise ? span : -span);

        _material!.SetShaderParameter("half_width", 0.39f);
        // Platform is static — scroll phase unused (kept in signature for call-site stability).
        _ = scrollPhaseTiles;
    }

    /// <summary>No-op: platform corner does not scroll marks.</summary>
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

        var shader = GD.Load<Shader>("res://shaders/belt_corner.gdshader");
        _material = new ShaderMaterial { Shader = shader };
        _sprite = new Sprite2D
        {
            Material = _material,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            // Above adjacent strip ends so the platform elbow reads cleanly.
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
