using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// World miner in layers: still base (<c>{id}-base</c>), drill bit (<c>{id}-drill-anim</c>) that turns while
/// the miner works, and for the advanced miner a lamp (<c>miner-advanced-lamp</c>) lit while it is powered.
/// </summary>
public partial class MinerVisual : Node2D
{
    /// <summary>Drill frames per second while working (8 frames = a quarter turn of the 4-fold bit).</summary>
    private const float DrillFramesPerSecond = 12f;

    private Sprite2D? _drill;
    private Texture2D[] _frames = [];
    private Sprite2D? _lamp;
    private float _clock;

    public GridPosition MinerOrigin { get; private set; }

    public static MinerVisual Create(Miner miner, int tileSize)
    {
        var id = miner.DefinitionId == Miner.AdvancedId ? Miner.AdvancedId : Miner.BasicId;
        var root = new MinerVisual
        {
            Name = $"Miner_{miner.Position.X}_{miner.Position.Y}",
            ZIndex = 3,
            MinerOrigin = miner.Position
        };

        root.AddChild(BuildingPad.Layer("Base", Sprites.For(id + "-base"), Miner.Size, tileSize, 0));
        root._frames = Sprites.Frames(id + "-drill");
        root._drill = BuildingPad.Layer("Drill", root._frames[0], Miner.Size, tileSize, 1);
        root.AddChild(root._drill);
        if (id == Miner.AdvancedId)
        {
            root._lamp = BuildingPad.Layer("Lamp", Sprites.For("miner-advanced-lamp"), Miner.Size, tileSize, 2);
            root._lamp.Visible = false;
            root.AddChild(root._lamp);
        }

        root.Sync(miner, 0f);
        return root;
    }

    /// <summary>Turn the drill while the miner works (on a deposit); light the lamp while powered.</summary>
    public void Sync(Miner miner, float delta)
    {
        if (miner.Efficiency > 0f && _drill is not null && _frames.Length > 0)
        {
            _clock += delta;
            _drill.Texture = _frames[(int)(_clock * DrillFramesPerSecond) % _frames.Length];
        }

        if (_lamp is not null)
        {
            _lamp.Visible = miner.IsPowered;
        }
    }
}
