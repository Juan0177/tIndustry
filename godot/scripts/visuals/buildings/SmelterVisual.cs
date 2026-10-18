using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// 2×2 smelter in layers: still base (<c>smelter-base</c>) and, while crafting, the glowing mouth
/// (<c>smelter-glow-anim</c>) flickering through its frames with a pulsing brightness.
/// </summary>
public partial class SmelterVisual : Node2D
{
    private const float GlowFramesPerSecond = 8f;

    private Sprite2D? _glow;
    private Texture2D[] _frames = [];
    private float _clock;
    private float _pulse;
    private float _phase; // pulse offset from the origin cell, so neighbouring smelters do not flicker together
    public GridPosition Origin { get; private set; }

    public static SmelterVisual Create(Crafter sm, int tileSize)
    {
        var root = new SmelterVisual
        {
            Name = $"Smelter_{sm.Position.X}_{sm.Position.Y}",
            ZIndex = 3,
            Origin = sm.Position,
            _clock = BuildingPad.Phase(sm.Position)
        };
        root._phase = root._clock * 3.6f;
        root.AddChild(BuildingPad.Layer("Base", Sprites.For("smelter-base"), Crafter.Size, tileSize, 0));
        root._frames = Sprites.Frames("smelter-glow");
        root._glow = BuildingPad.Layer("Glow", root._frames[0], Crafter.Size, tileSize, 1);
        root._glow.Visible = false;
        root.AddChild(root._glow);
        root.Sync(sm, 0f);
        return root;
    }

    public void Sync(Crafter sm, float delta)
    {
        if (_glow is null)
        {
            return;
        }

        if (!sm.IsCrafting)
        {
            _glow.Visible = false;
            _pulse = 0f;
            return;
        }

        _glow.Visible = true;
        _clock += delta;
        _glow.Texture = _frames[(int)(_clock * GlowFramesPerSecond) % _frames.Length];
        _pulse += delta * 3.6f;
        var wave = 0.5f + 0.5f * Mathf.Sin(_pulse + _phase);
        var light = 0.85f + 0.3f * wave;
        _glow.Modulate = new Color(light, light, light, 0.8f + 0.2f * wave);
    }
}
