using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Full-cell corner (recipe Blu1–4). Base art: enter-west → exit-south
/// (incoming East → outgoing South, CW). Other turns = rotate / flipX.
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
        float scrollPhaseTiles)
    {
        EnsureVisual();
        _sprite!.Texture = CreateUnitTexture();
        _sprite.Centered = true;
        _sprite.Position = new Vector2((cell.X + 0.5f) * tileSize, (cell.Y + 0.5f) * tileSize);

        // Base art: East→South (CW), Blu4 on W+S, knuckle SW.
        // CCW: flipX first → West→South, then rotate. Z bottom S→E = flipX + −90°
        // so Blu4 opens on N+E (not the old flipY mapping that left it on S+W).
        var (rotation, flipX) = ResolveTransform(from, to);
        _sprite.RotationDegrees = rotation;
        var span = (float)tileSize;
        _sprite.Scale = new Vector2(flipX ? -span : span, span);

        _ = scrollPhaseTiles;
    }

    public void SetScroll(float scrollTiles) => _ = scrollTiles;

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
