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

        // Base sprite: from=East → to=South (CW). CCW uses horizontal flip first
        // (East→South mirrored = West→South), then rotate.
        var clockwise = BeltLane.IsClockwiseTurn(from, to);
        float rotation;
        var flipX = !clockwise;
        if (clockwise)
        {
            rotation = from switch
            {
                Direction.East => 0f,
                Direction.South => 90f,
                Direction.West => 180f,
                Direction.North => -90f,
                _ => 0f
            };
        }
        else
        {
            rotation = from switch
            {
                Direction.West => 0f,
                Direction.South => -90f,
                Direction.East => 180f,
                Direction.North => 90f,
                _ => 0f
            };
        }

        _sprite.RotationDegrees = rotation;
        var span = (float)tileSize;
        _sprite.Scale = new Vector2(flipX ? -span : span, span);

        _ = scrollPhaseTiles;
    }

    public void SetScroll(float scrollTiles) => _ = scrollTiles;

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
