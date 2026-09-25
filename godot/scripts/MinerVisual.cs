using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// World miner: bordered square pad + one large centered gear (fits inside the border).
/// Gear spins while working; T2 lit perno (center) when electricity-boosted.
/// </summary>
public partial class MinerVisual : Node2D
{
    private const float SpinRadPerSec = 2.4f;
    /// <summary>Gear diameter as fraction of inner pad (inside border).</summary>
    private const float GearFitFraction = 0.90f;

    private Sprite2D? _gear;
    private Sprite2D? _pernoLit;
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
        const float borderInset = 8f;
        var inner = span - borderInset * 2f;
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

        // Pad body (static).
        var pad = new Sprite2D
        {
            Name = "Pad",
            Texture = padTex,
            Centered = true,
            TextureFilter = TextureFilterEnum.Nearest,
            Scale = new Vector2(span / (float)padTex.GetWidth(), span / (float)padTex.GetHeight()),
            ZIndex = 0
        };
        root.AddChild(pad);

        // One large gear centered; diameter stays inside the border.
        var gearPx = inner * GearFitFraction;
        var gearScale = gearPx / gearTex.GetWidth();
        root._gear = new Sprite2D
        {
            Name = "Gear",
            Texture = gearTex,
            Centered = true,
            TextureFilter = TextureFilterEnum.Nearest,
            Position = Vector2.Zero,
            Scale = new Vector2(gearScale, gearScale),
            ZIndex = 1
        };
        root.AddChild(root._gear);

        // Lit perno at center (T2 boost).
        var litScale = (span * 0.14f) / litTex.GetWidth();
        root._pernoLit = MakePernoLit(litTex, Vector2.Zero, litScale);
        root.AddChild(root._pernoLit);

        var outline = new Line2D
        {
            Name = "Border",
            Width = 2f,
            // Light blue-grey frame (matches pad art / logistics Blu4).
            DefaultColor = new Color(0.53f, 0.65f, 0.72f, 1f),
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

    /// <summary>Advance gear spin while working; show lit center perno when T2 is powered.</summary>
    public void Sync(MinerProducer miner, float delta)
    {
        var working = miner.Efficiency > 0f;
        if (working && _gear is not null)
        {
            _angle += delta * SpinRadPerSec;
            _gear.Rotation = _angle;
        }

        if (_pernoLit is not null)
        {
            _pernoLit.Visible = _advanced && miner.IsPowered;
        }
    }
}
