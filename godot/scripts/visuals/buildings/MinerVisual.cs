using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// World miner in layers: still base (<c>{id}-base</c>), drill bit (<c>{id}-drill-anim</c>) that turns while
/// the miner works, and for the advanced miner a lamp (<c>miner-advanced-lamp</c>) lit while it is powered.
/// Sparks fly off the bit while it works (G3 effect <c>scintille-trivella</c>).
/// </summary>
public partial class MinerVisual : Node2D
{
    /// <summary>Drill frames per second while working (8 frames = a quarter turn of the 4-fold bit).</summary>
    private const float DrillFramesPerSecond = 12f;

    private Sprite2D? _drill;
    private Texture2D[] _frames = [];
    private Sprite2D? _lamp;
    private float _clock;
    private EffectManager? _fx;
    private int _sparks;

    public GridPosition MinerOrigin { get; private set; }

    public static MinerVisual Create(Miner miner, int tileSize)
    {
        var id = miner.DefinitionId == Miner.AdvancedId ? Miner.AdvancedId : Miner.BasicId;
        var root = new MinerVisual
        {
            Name = $"Miner_{miner.Position.X}_{miner.Position.Y}",
            ZIndex = 3,
            MinerOrigin = miner.Position,
            _clock = BuildingPad.Phase(miner.Position)
        };

        root.AddChild(BuildingPad.Layer("Base", Sprites.For(id + "-base"), Miner.Size, tileSize, 0));
        root._frames = Sprites.Frames(id + "-drill");
        root._drill = BuildingPad.Layer("Drill", root.Frame(), Miner.Size, tileSize, 1);
        root.AddChild(root._drill);
        if (id == Miner.AdvancedId)
        {
            root._lamp = BuildingPad.Layer("Lamp", Sprites.For("miner-advanced-lamp"), Miner.Size, tileSize, 2);
            root._lamp.Visible = false;
            root.AddChild(root._lamp);
        }

        root.Sync(miner, 0f, null);
        return root;
    }

    /// <summary>Turn the drill while the miner works (on a deposit); light the lamp while powered.</summary>
    public void Sync(Miner miner, float delta, EffectManager? fx)
    {
        if (fx is not null)
        {
            _fx = fx;
            _sparks = fx.Keep(_sparks, miner.Efficiency > 0f, "scintille-trivella", this, Vector2.Zero);
        }

        if (miner.Efficiency > 0f && _drill is not null && _frames.Length > 0)
        {
            _clock += delta;
            _drill.Texture = Frame();
        }

        if (_lamp is not null)
        {
            _lamp.Visible = miner.IsPowered;
        }
    }

    public override void _ExitTree() => EffectManager.DetachFrom(_fx, ref _sparks);

    private Texture2D Frame() => _frames[(int)(_clock * DrillFramesPerSecond) % _frames.Length];
}
