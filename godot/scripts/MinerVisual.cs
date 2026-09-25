using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// World miner: static square pad + two corner-clipped gears.
/// Gears spin while working; T2 lit perno when electricity-boosted.
/// </summary>
public partial class MinerVisual : Node2D
{
    private const float LargeSpinRadPerSec = 2.4f;
    private const float SmallSpinRatio = -1.45f;

    private Sprite2D? _largeGear;
    private Sprite2D? _smallGear;
    private Sprite2D? _pernoLarge;
    private Sprite2D? _pernoSmall;
    private float _angle;
    private bool _advanced;

    public GridPosition MinerOrigin { get; private set; }

    public static MinerVisual Create(MinerProducer miner, int tileSize)
    {
        var advanced = miner.DefinitionId == MinerProducer.AdvancedId;
        var root = new MinerVisual
        {
            Name = $"Miner_{miner.Position.X}_{miner.Position.Y}",
            ZIndex = 3
        };
        root._advanced = advanced;
        root.MinerOrigin = miner.Position;

        var span = MinerProducer.Size * tileSize;
        var half = span * 0.5f;
        Vector2[] box =
        [
            new(-half + 1f, -half + 1f),
            new(half - 1f, -half + 1f),
            new(half - 1f, half - 1f),
            new(-half + 1f, half - 1f)
        ];

        var padPath = advanced
            ? "res://assets/miner-pad-advanced.png"
            : "res://assets/miner-pad.png";
        var gearPath = advanced
            ? "res://assets/gear-red.png"
            : "res://assets/gear-rust.png";
        var padTex = GD.Load<Texture2D>(padPath);
        var gearTex = GD.Load<Texture2D>(gearPath);
        var litTex = GD.Load<Texture2D>("res://assets/gear-perno-lit.png");

        // Control clip_contents reliably masks rotating gears to the square pad.
        var clip = new Control
        {
            Name = "Clip",
            ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Position = new Vector2(-half, -half),
            Size = new Vector2(span, span),
            ZIndex = 0
        };
        root.AddChild(clip);

        var pad = new TextureRect
        {
            Name = "Pad",
            Texture = padTex,
            TextureFilter = TextureFilterEnum.Nearest,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Position = Vector2.Zero,
            Size = new Vector2(span, span),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        clip.AddChild(pad);

        // Diameters ≈ 2× / 1.7× footprint so visible quarters fill the whole pad
        // (pivot on opposite corners; ClipContents keeps only the in-pad quarter).
        var gearScaleLarge = (span * 2.05f) / gearTex.GetWidth();
        var gearScaleSmall = (span * 1.70f) / gearTex.GetWidth();

        root._largeGear = new Sprite2D
        {
            Name = "GearLarge",
            Texture = gearTex,
            Centered = true,
            TextureFilter = TextureFilterEnum.Nearest,
            Position = Vector2.Zero,
            Scale = new Vector2(gearScaleLarge, gearScaleLarge),
            ZIndex = 1
        };
        clip.AddChild(root._largeGear);

        root._smallGear = new Sprite2D
        {
            Name = "GearSmall",
            Texture = gearTex,
            Centered = true,
            TextureFilter = TextureFilterEnum.Nearest,
            Position = new Vector2(span, span),
            Scale = new Vector2(gearScaleSmall, gearScaleSmall),
            ZIndex = 1
        };
        clip.AddChild(root._smallGear);

        // Lit perno overlays (T2 boost) — sized to the corner hub, not the full gear.
        var litScale = (span * 0.16f) / litTex.GetWidth();
        root._pernoLarge = MakePernoLit(litTex, new Vector2(-half, -half), litScale);
        root._pernoSmall = MakePernoLit(litTex, new Vector2(half, half), litScale);
        root.AddChild(root._pernoLarge);
        root.AddChild(root._pernoSmall);

        var outline = new Line2D
        {
            Name = "Border",
            Width = 2f,
            DefaultColor = advanced
                ? new Color(0.95f, 0.45f, 0.35f, 1f)
                : new Color(0.85f, 0.72f, 0.4f, 1f),
            Antialiased = false,
            Closed = true,
            Points = box,
            ZIndex = 2
        };
        root.AddChild(outline);

        root.Sync(miner, 0f);
        return root;
    }

    private static Sprite2D MakePernoLit(Texture2D tex, Vector2 pos, float scale) =>
        new()
        {
            Name = "PernoLit",
            Texture = tex,
            Centered = true,
            TextureFilter = TextureFilterEnum.Nearest,
            Position = pos,
            Scale = new Vector2(scale, scale),
            Visible = false,
            ZIndex = 3,
            Modulate = new Color(1.2f, 1.15f, 0.7f, 1f)
        };

    /// <summary>Advance gear spin while working; show lit perno when T2 is powered.</summary>
    public void Sync(MinerProducer miner, float delta)
    {
        var working = miner.Efficiency > 0f;
        if (working)
        {
            _angle += delta * LargeSpinRadPerSec;
            if (_largeGear is not null)
            {
                _largeGear.Rotation = _angle;
            }

            if (_smallGear is not null)
            {
                _smallGear.Rotation = _angle * SmallSpinRatio;
            }
        }

        var lit = _advanced && miner.IsPowered;
        if (_pernoLarge is not null)
        {
            _pernoLarge.Visible = lit;
        }

        if (_pernoSmall is not null)
        {
            _pernoSmall.Visible = lit;
        }
    }
}
