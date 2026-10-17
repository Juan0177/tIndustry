using Godot;
using TIndustry.Shared;
using static TIndustry.Godot.HudStyle;

namespace TIndustry.Godot;

/// <summary>
/// The local drone's build queue (Mindustry's build plan box): the block being worked on with its
/// progress bar (red while taking apart) and "+N" plans still waiting. A click asks the world to centre
/// the camera on that block. Hidden while the queue is empty. Widgets change only when what they show does.
/// </summary>
public partial class BuildQueuePanel : PanelContainer
{
    private const int LocalPlayer = 0;
    private const float IconSize = 32f;
    private const float BarWidth = 120f;
    private const float BarHeight = 8f;

    private static readonly Color BarBack = new(0.14f, 0.16f, 0.15f, 1f);

    private TextureRect? _icon;
    private Label? _name;
    private ColorRect? _barFill;
    private Label? _more;
    private string? _structureId;
    private bool _deconstructing;
    private int _waiting = -1;
    private IGameView? _view;

    /// <summary>World rect of the block at the head of the queue, raised on a click.</summary>
    public event Action<Rect2>? FocusRequested;

    public BuildQueuePanel()
    {
        StylePanel(this, "BuildQueuePanel");
        Visible = false;
        TooltipText = "Coda di costruzione · clic = mostra il cantiere";
        MouseDefaultCursorShape = CursorShape.PointingHand;
    }

    public override void _Ready()
    {
        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 6);
        }

        AddChild(margin);

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        margin.AddChild(row);

        var slot = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        slot.AddThemeStyleboxOverride("panel", MakeSlotStyle(SlotSelected, 1));
        row.AddChild(slot);
        _icon = MakeIcon(null, IconSize);
        slot.AddChild(_icon);

        var column = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        column.AddThemeConstantOverride("separation", 4);
        row.AddChild(column);

        _name = new Label { MouseFilter = MouseFilterEnum.Ignore };
        _name.AddThemeColorOverride("font_color", TextPrimary);
        _name.AddThemeFontSizeOverride("font_size", 12);
        column.AddChild(_name);

        var bar = new ColorRect
        {
            Color = BarBack,
            CustomMinimumSize = new Vector2(BarWidth, BarHeight),
            MouseFilter = MouseFilterEnum.Ignore
        };
        column.AddChild(bar);
        _barFill = new ColorRect
        {
            Color = ConstructionLayer.Accent,
            Size = new Vector2(0f, BarHeight),
            MouseFilter = MouseFilterEnum.Ignore
        };
        bar.AddChild(_barFill);

        _more = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(28, 0)
        };
        _more.AddThemeColorOverride("font_color", TextMuted);
        _more.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(_more);
    }

    /// <summary>Shows the head of the local drone's queue from <paramref name="view"/> (hidden when empty).</summary>
    public void Sync(IGameView? view)
    {
        var builder = LocalBuilder(view);
        if (view is null || builder is null || builder.Plans.Count == 0)
        {
            Visible = false;
            return;
        }

        var plan = builder.Plans[0];
        ConstructSite? site = null;
        if (builder.CurrentSiteId is { } siteId)
        {
            site = SiteById(view, siteId);
        }

        var spec = site?.Spec ?? plan.Spec;
        var deconstructing = site?.Deconstructing ?? plan.Breaking;
        if (spec.StructureId != _structureId)
        {
            _structureId = spec.StructureId;
            if (_icon is not null)
            {
                _icon.Texture = ConstructionLayer.TextureFor(spec.StructureId);
            }

            if (_name is not null)
            {
                _name.Text = view.Content.FindStructure(spec.StructureId)?.DisplayName ?? spec.StructureId;
            }
        }

        if (deconstructing != _deconstructing && _barFill is not null)
        {
            _deconstructing = deconstructing;
            _barFill.Color = deconstructing ? ConstructionLayer.Remove : ConstructionLayer.Accent;
        }

        if (_barFill is not null)
        {
            // Before the drone reaches the plan there is no site: a build starts empty, a breaking plan full.
            var progress = site?.Progress ?? (plan.Breaking ? 1f : 0f);
            _barFill.Size = new Vector2(BarWidth * Mathf.Clamp(progress, 0f, 1f), BarHeight);
        }

        var waiting = builder.Plans.Count - 1;
        if (waiting != _waiting && _more is not null)
        {
            _waiting = waiting;
            _more.Text = waiting > 0 ? $"+{waiting}" : "";
        }

        _view = view;
        Visible = true;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }
            && HeadBounds(_view) is { } target)
        {
            FocusRequested?.Invoke(target);
            AcceptEvent();
        }
    }

    /// <summary>World rect of the site at the head of the queue, or of its first plan before work began.</summary>
    private static Rect2? HeadBounds(IGameView? view)
    {
        if (view is null || LocalBuilder(view) is not { Plans.Count: > 0 } builder)
        {
            return null;
        }

        if (builder.CurrentSiteId is { } siteId && SiteById(view, siteId) is { } site)
        {
            return ConstructionLayer.Bounds(site.Tiles);
        }

        return ConstructionLayer.Bounds(view.PlanTiles(builder.Plans[0]));
    }

    private static BuilderUnit? LocalBuilder(IGameView? view)
    {
        if (view is null)
        {
            return null;
        }

        // Index loops: a foreach over the read-only lists would box an enumerator every frame.
        var builders = view.Builders;
        for (var i = 0; i < builders.Count; i++)
        {
            if (builders[i].OwnerPlayerId == LocalPlayer)
            {
                return builders[i];
            }
        }

        return null;
    }

    private static ConstructSite? SiteById(IGameView view, long siteId)
    {
        var sites = view.Sites;
        for (var i = 0; i < sites.Count; i++)
        {
            if (sites[i].Id == siteId)
            {
                return sites[i];
            }
        }

        return null;
    }
}
