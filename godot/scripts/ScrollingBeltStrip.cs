using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Continuous Mindustry-style belt strip: shader chevrons scroll along flow at
/// <see cref="ConveyorDefinition.RateItemsPerSecond"/> tiles/second (same units as item Advance).
/// Straight lanes only — corners/junctions are a follow-up for the full port.
/// </summary>
public partial class ScrollingBeltStrip : Node2D
{
    private ShaderMaterial? _material;
    private Sprite2D? _sprite;
    private float _scroll;
    private float _rateTilesPerSecond = 0.5f;

    public float ScrollTiles => _scroll;
    public float RateTilesPerSecond => _rateTilesPerSecond;

    public void Configure(IReadOnlyList<GridPosition> path, Direction direction, float rateItemsPerSecond, int tileSize)
    {
        if (path.Count == 0)
        {
            throw new ArgumentException("Path vuoto.", nameof(path));
        }

        var cellCount = path.Count;
        _rateTilesPerSecond = Math.Max(0.05f, rateItemsPerSecond);

        var first = path[0];
        var last = path[^1];
        var minX = Math.Min(first.X, last.X);
        var maxX = Math.Max(first.X, last.X);
        var minY = Math.Min(first.Y, last.Y);
        var maxY = Math.Max(first.Y, last.Y);

        var horizontal = direction is Direction.East or Direction.West;
        float lengthPx;
        float thicknessPx;
        Vector2 center;
        if (horizontal)
        {
            lengthPx = (maxX - minX + 1) * tileSize;
            thicknessPx = tileSize * 0.78f;
            center = new Vector2((minX + maxX + 1) * 0.5f * tileSize, (first.Y + 0.5f) * tileSize);
        }
        else
        {
            lengthPx = (maxY - minY + 1) * tileSize;
            thicknessPx = tileSize * 0.78f;
            center = new Vector2((first.X + 0.5f) * tileSize, (minY + maxY + 1) * 0.5f * tileSize);
        }

        EnsureVisual();
        _sprite!.Texture = CreateUnitTexture();
        _sprite.Centered = true;
        _sprite.Position = center;
        // Always author as length × thickness in local space, then rotate so +local X = flow.
        _sprite.Scale = new Vector2(lengthPx, thicknessPx);
        _sprite.RotationDegrees = direction switch
        {
            Direction.East => 0f,
            Direction.South => 90f,
            Direction.West => 180f,
            Direction.North => -90f,
            _ => 0f
        };

        _material!.SetShaderParameter("cell_count", (float)cellCount);
        _material.SetShaderParameter("marks_per_tile", 2.5f);
        _material.SetShaderParameter("scroll", _scroll);
    }

    public override void _Process(double delta)
    {
        if (_material is null)
        {
            return;
        }

        _scroll += _rateTilesPerSecond * (float)delta;
        if (_scroll > 1024f)
        {
            _scroll %= 1f;
        }

        _material.SetShaderParameter("scroll", _scroll);
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
            ZIndex = 1
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
