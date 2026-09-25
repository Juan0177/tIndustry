using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>2×2 forno: symmetric chamber; heat glow pulses while crafting.</summary>
public partial class SmelterVisual : Node2D
{
    private Polygon2D? _glow;
    private Polygon2D? _halo;
    private float _pulse;
    public GridPosition Origin { get; private set; }

    public static SmelterVisual Create(SmelterStub sm, int tileSize)
    {
        var root = new SmelterVisual
        {
            Name = $"Smelter_{sm.Position.X}_{sm.Position.Y}",
            ZIndex = 3,
            Origin = sm.Position
        };
        var span = SmelterStub.Size * tileSize;
        var padTex = GD.Load<Texture2D>("res://assets/smelter.png");
        var pad = new Sprite2D
        {
            Texture = padTex,
            Centered = true,
            TextureFilter = TextureFilterEnum.Nearest,
            Scale = new Vector2(span / (float)padTex.GetWidth(), span / (float)padTex.GetHeight())
        };
        root.AddChild(pad);

        var r = span * 0.18f;
        root._glow = new Polygon2D
        {
            Name = "HeatGlow",
            Color = new Color(1f, 0.45f, 0.12f, 0.55f),
            Polygon = Circle(r, 16),
            Position = new Vector2(0, span * 0.02f),
            ZIndex = 1,
            Visible = false
        };
        root.AddChild(root._glow);
        // Soft outer halo
        root._halo = new Polygon2D
        {
            Name = "HeatHalo",
            Color = new Color(1f, 0.35f, 0.08f, 0.0f),
            Polygon = Circle(r * 1.55f, 16),
            Position = new Vector2(0, span * 0.02f),
            ZIndex = 0,
            Visible = false
        };
        root.AddChild(root._halo);
        root.Sync(sm, 0f);
        return root;
    }

    public void Sync(SmelterStub sm, float delta)
    {
        if (_glow is null)
        {
            return;
        }

        if (!sm.IsCrafting)
        {
            _glow.Visible = false;
            if (_halo is not null)
            {
                _halo.Visible = false;
            }

            _pulse = 0f;
            return;
        }

        _glow.Visible = true;
        if (_halo is not null)
        {
            _halo.Visible = true;
        }

        _pulse += delta * 3.6f;
        var wave = 0.5f + 0.5f * Mathf.Sin(_pulse);
        var t = 0.40f + 0.55f * wave;
        _glow.Color = new Color(1f, 0.42f + 0.35f * wave, 0.08f + 0.12f * wave, t);
        var s = 0.75f + 0.45f * wave;
        _glow.Scale = new Vector2(s, s);
        if (_halo is not null)
        {
            _halo.Color = new Color(1f, 0.3f, 0.05f, 0.12f + 0.22f * wave);
            _halo.Scale = new Vector2(0.9f + 0.35f * wave, 0.9f + 0.35f * wave);
        }
    }

    private static Vector2[] Circle(float radius, int segments)
    {
        var pts = new Vector2[segments];
        for (var i = 0; i < segments; i++)
        {
            var a = i * Mathf.Tau / segments;
            pts[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }

        return pts;
    }
}
