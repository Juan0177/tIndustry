using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// 2×2 generator in layers: still base (<c>generator-base</c>) and the rotor (<c>generator-rotor-anim</c>)
/// that turns while a fuel block burns and stops where it is when the generator is idle; steam rises while it
/// generates (G3 effect <c>vapore-generatore</c>).
/// </summary>
public partial class GeneratorVisual : Node2D
{
    /// <summary>Rotor frames per second while generating (4 frames = a sixth of a turn of the 6-blade rotor).</summary>
    private const float RotorFramesPerSecond = 10f;

    private Sprite2D? _rotor;
    private Texture2D[] _frames = [];
    private float _clock;
    private Vector2 _steamAt;
    private EffectManager? _fx;
    private int _steam;
    public GridPosition Origin { get; private set; }

    public static GeneratorVisual Create(Generator gen, int tileSize)
    {
        var root = new GeneratorVisual
        {
            Name = $"Generator_{gen.Position.X}_{gen.Position.Y}",
            ZIndex = 3,
            Origin = gen.Position,
            _clock = BuildingPad.Phase(gen.Position),
            _steamAt = new Vector2(0f, -tileSize * 0.4f)
        };
        BuildingPad.AddShadow(root, Sprites.For("generator-base"), Generator.Size, tileSize);
        root.AddChild(BuildingPad.Layer("Base", Sprites.For("generator-base"), Generator.Size, tileSize, 0));
        root._frames = Sprites.Frames("generator-rotor");
        root._rotor = BuildingPad.Layer("Rotor", root.Frame(), Generator.Size, tileSize, 1);
        root.AddChild(root._rotor);
        root.Sync(gen, 0f, null);
        return root;
    }

    public override void _ExitTree() => EffectManager.DetachFrom(_fx, ref _steam);

    public void Sync(Generator gen, float delta, EffectManager? fx)
    {
        if (fx is not null)
        {
            _fx = fx;
            _steam = fx.Keep(_steam, gen.IsGenerating, "vapore-generatore", this, _steamAt, Origin);
        }

        if (!gen.IsGenerating || _rotor is null)
        {
            return;
        }

        _clock += delta;
        _rotor.Texture = Frame();
    }

    private Texture2D Frame() => _frames[(int)(_clock * RotorFramesPerSecond) % _frames.Length];
}
