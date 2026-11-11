using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Solar panel (1×1 <c>solar</c> or the large 3×3 <c>solar-large</c>): its sprite, lit by what it produces now
/// (<see cref="SolarPanel.CurrentRate"/> over its full-light output): in full colour by day, dimmed to
/// <see cref="Off"/> at night when it makes nothing, in between at dusk and dawn. The night's darkness
/// (<see cref="NightLayer"/>) falls on top like on every block.
/// </summary>
public partial class SolarVisual : Node2D
{
    /// <summary>Tint of a panel that produces nothing: grey-blue cells, switched off.</summary>
    private static readonly Color Off = new(0.5f, 0.54f, 0.66f);

    private Sprite2D? _body;
    private long _fullRate;
    private int _shownPercent = -1;

    public GridPosition Origin { get; private set; }

    /// <param name="fullRate">The panel's output at full light, thousandths of a unit per second.</param>
    public static SolarVisual Create(SolarPanel panel, long fullRate, int tileSize)
    {
        var root = new SolarVisual
        {
            Name = $"SolarPanel_{panel.Position.X}_{panel.Position.Y}",
            ZIndex = 3,
            Origin = panel.Position,
            _fullRate = fullRate
        };
        var texture = Sprites.For(panel.DefinitionId);
        BuildingPad.AddShadow(root, texture, panel.Size, tileSize);
        root._body = BuildingPad.Layer("Block", texture, panel.Size, tileSize, 0);
        root.AddChild(root._body);
        root.Sync(panel);
        return root;
    }

    /// <summary>Share of the full-light output the panel makes now, in percent (0 without an output).</summary>
    public static int LitPercent(long currentRate, long fullRate) =>
        fullRate <= 0 ? 0 : (int)Math.Clamp(currentRate * 100 / fullRate, 0, 100);

    public void Sync(SolarPanel panel)
    {
        if (_body is null)
        {
            return;
        }

        var percent = LitPercent(panel.CurrentRate, _fullRate);
        if (percent == _shownPercent)
        {
            return;
        }

        _shownPercent = percent;
        _body.SelfModulate = Off.Lerp(Colors.White, percent / 100f);
    }
}
