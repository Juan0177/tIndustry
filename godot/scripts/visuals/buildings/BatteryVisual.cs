using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Battery (1×1 or the large 3×3): lead cell with an empty window (<c>&lt;id&gt;-base</c>) and, over it, the lit charge
/// bars (frames of <c>&lt;id&gt;-charge-anim</c>: 0 to 3, 0 to 5 for the large one) picked from its own charge; with
/// "Bagliore" the bars bloom.
/// </summary>
public partial class BatteryVisual : Node2D
{
    private Sprite2D? _charge;
    private int _level = -1;
    private string _id = Battery.BuildingId;

    public GridPosition Origin { get; private set; }

    public static BatteryVisual Create(Battery battery, long capacity, int tileSize)
    {
        var root = new BatteryVisual
        {
            Name = $"Battery_{battery.Position.X}_{battery.Position.Y}",
            ZIndex = 3,
            Origin = battery.Position,
            _id = battery.DefinitionId
        };
        var texture = Sprites.For(battery.DefinitionId + "-base");
        BuildingPad.AddShadow(root, texture, battery.Size, tileSize);
        root.AddChild(BuildingPad.Layer("Base", texture, battery.Size, tileSize, 0));
        var frames = Sprites.Frames(battery.DefinitionId + "-charge");
        root._charge = BuildingPad.Layer("Charge", frames[0], battery.Size, tileSize, 1);
        root._charge.SelfModulate = new Color(BuildingPad.GlowBoost, BuildingPad.GlowBoost, BuildingPad.GlowBoost);
        root.AddChild(root._charge);
        root.Sync(battery, capacity);
        return root;
    }

    /// <summary>Bars lit for <paramref name="charge"/> of <paramref name="capacity"/>: none when empty, all when full.</summary>
    public static int LevelOf(long charge, long capacity, int bars) =>
        charge <= 0 || capacity <= 0 ? 0 : (int)Math.Clamp((charge * bars + capacity - 1) / capacity, 1, bars);

    public void Sync(Battery battery, long capacity)
    {
        if (_charge is null)
        {
            return;
        }

        var frames = Sprites.Frames(_id + "-charge");
        var level = LevelOf(battery.Charge, capacity, frames.Length - 1);
        if (level != _level)
        {
            _level = level;
            _charge.Texture = frames[level];
        }
    }
}
