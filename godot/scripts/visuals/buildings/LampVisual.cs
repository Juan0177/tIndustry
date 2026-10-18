using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// 1×1 lamp: plate with a dark glass dome (<c>lamp-base</c>) and the lit dome (<c>lamp-glow</c>) shown while it gets
/// power. Its light at night is a PointLight2D of <see cref="NightLayer"/>; with "Bagliore" the dome blooms.
/// </summary>
public partial class LampVisual : Node2D
{
    private Sprite2D? _glow;

    public GridPosition Origin { get; private set; }

    public static LampVisual Create(Lamp lamp, int tileSize)
    {
        var root = new LampVisual
        {
            Name = $"Lamp_{lamp.Position.X}_{lamp.Position.Y}",
            ZIndex = 3,
            Origin = lamp.Position
        };
        var texture = Sprites.For("lamp-base");
        BuildingPad.AddShadow(root, texture, Lamp.Size, tileSize);
        root.AddChild(BuildingPad.Layer("Base", texture, Lamp.Size, tileSize, 0));
        root._glow = BuildingPad.Layer("Glow", Sprites.For("lamp-glow"), Lamp.Size, tileSize, 1);
        root._glow.SelfModulate = new Color(BuildingPad.GlowBoost, BuildingPad.GlowBoost, BuildingPad.GlowBoost);
        root.AddChild(root._glow);
        root.Sync(lamp);
        return root;
    }

    public void Sync(Lamp lamp)
    {
        if (_glow is not null)
        {
            _glow.Visible = lamp.IsLit;
        }
    }
}
