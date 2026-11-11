using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// 2×2 craft machine of <see cref="IGameView.Machines"/> (advanced smelter, graphite press, separator, plasticizer) in
/// layers: still base (<c>{id}-base</c>) and the working overlay (<c>{id}-anim</c>, 4 frames: flames, ram, drum,
/// bubbles) shown and advanced only while the machine works (<see cref="Crafter.IsWorking"/>). Without a base the
/// block's icon (<c>{id}</c>) stands still; without the strip there is no overlay. The advanced smelter is a furnace:
/// its flames glow like the forno's mouth (<see cref="BuildingPad.GlowBoost"/>) and it smokes while it works (G3
/// effect <c>fumo-forno</c>).
/// </summary>
public partial class MachineVisual : Node2D
{
    /// <summary>Overlay frames per second while working (4 frames: half a second per loop).</summary>
    private const float FramesPerSecond = 8f;

    private Sprite2D? _overlay;
    private Texture2D[] _frames = [];
    private float _clock;
    private bool _furnace;
    private Vector2 _smokeAt;
    private EffectManager? _fx;
    private int _smoke;
    public GridPosition Origin { get; private set; }

    public static MachineVisual Create(Crafter machine, int tileSize)
    {
        var id = machine.DefinitionId;
        var root = new MachineVisual
        {
            Name = $"Machine_{machine.Position.X}_{machine.Position.Y}",
            ZIndex = 3,
            Origin = machine.Position,
            _clock = BuildingPad.Phase(machine.Position),
            _furnace = id == Crafter.SmelterAdvancedId,
            _smokeAt = new Vector2(0f, -tileSize * 0.45f)
        };

        var baseTexture = Sprites.TryFor(id + "-base") ?? Sprites.For(id);
        BuildingPad.AddShadow(root, baseTexture, Crafter.Size, tileSize);
        root.AddChild(BuildingPad.Layer("Base", baseTexture, Crafter.Size, tileSize, 0));
        if (Sprites.TryFor(id + "-anim") is not null)
        {
            root._frames = Sprites.Frames(id);
            root._overlay = BuildingPad.Layer("Overlay", root.Frame(), Crafter.Size, tileSize, 1);
            root._overlay.Visible = false;
            if (root._furnace)
            {
                root._overlay.SelfModulate = new Color(BuildingPad.GlowBoost, BuildingPad.GlowBoost, BuildingPad.GlowBoost);
            }

            root.AddChild(root._overlay);
        }

        root.Sync(machine, 0f, null);
        return root;
    }

    public override void _ExitTree() => EffectManager.DetachFrom(_fx, ref _smoke);

    /// <summary>Shows and advances the overlay while the machine works; hides it (the base alone) when it stops.</summary>
    public void Sync(Crafter machine, float delta, EffectManager? fx)
    {
        if (fx is not null && _furnace)
        {
            _fx = fx;
            _smoke = fx.Keep(_smoke, machine.IsWorking, "fumo-forno", this, _smokeAt, Origin);
        }

        if (_overlay is null)
        {
            return;
        }

        _overlay.Visible = machine.IsWorking;
        if (!machine.IsWorking)
        {
            return;
        }

        _clock += delta;
        _overlay.Texture = Frame();
    }

    private Texture2D Frame() => _frames[(int)(_clock * FramesPerSecond) % _frames.Length];
}
