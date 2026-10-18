using Godot;
using TIndustry.Shared;
using static TIndustry.Godot.HudStyle;

namespace TIndustry.Godot;

/// <summary>
/// The local drone's build queue (Mindustry's build plan box): the block being worked on with its
/// progress bar (red while taking apart, amber with "Manca: ..." while stalled for materials) and "+N"
/// plans still waiting. A click asks the world to centre the camera on that block. Hidden while the queue
/// is empty. Widgets change only when what they show does.
/// </summary>
public partial class BuildQueuePanel : PanelContainer
{
    private const int LocalPlayer = 0;
    private const float IconSize = 32f;
    private const float BarWidth = 120f;
    private const float BarHeight = 8f;


    private TextureRect? _icon;
    private Label? _name;
    private ColorRect? _barFill;
    private Label? _more;
    private Label? _missing;
    private string? _structureId;
    private ColorRect? _barBack;
    private CostEntry? _missingShown;
    private int _waiting = -1;
    private IGameView? _view;

    /// <summary>World rect of the block at the head of the queue, raised on a click.</summary>
    public event Action<Rect2>? FocusRequested;

    public BuildQueuePanel()
    {
        StylePanel(this, "BuildQueuePanel");
        Visible = false;
        TooltipText = Testi.T("coda.suggerimento");
        MouseDefaultCursorShape = CursorShape.PointingHand;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationTranslationChanged)
        {
            // New language: the tooltip now, the missing line at the next sync.
            TooltipText = Testi.T("coda.suggerimento");
            _missingShown = null;
            _structureId = null; // the site name too
        }
    }

    public override void _Ready()
    {
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, ThemeTypeVariation = UiTheme.RigaLarga };
        AddChild(row);

        var slot = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, ThemeTypeVariation = UiTheme.SlotScelto };
        row.AddChild(slot);
        _icon = MakeIcon(null, IconSize);
        slot.AddChild(_icon);

        var column = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        row.AddChild(column);

        _name = MakeLabel();
        column.AddChild(_name);

        _barBack = new ColorRect
        {
            Color = UiColors.FondoSlot,
            CustomMinimumSize = new Vector2(BarWidth, BarHeight),
            MouseFilter = MouseFilterEnum.Ignore
        };
        column.AddChild(_barBack);
        _barFill = new ColorRect
        {
            Color = UiColors.Costruzione,
            Size = new Vector2(0f, BarHeight),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _barBack.AddChild(_barFill);

        _missing = MakeLabel("", UiTheme.TestoRosso);
        _missing.Visible = false;
        column.AddChild(_missing);

        _more = MakeLabel("", UiTheme.TestoTenue);
        _more.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _more.CustomMinimumSize = new Vector2(28, 0);
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
        // Before the drone is back at a plan sent to the back while stalled, its site still stands.
        var site = builder.CurrentSiteId is { } siteId ? SiteById(view, siteId) : view.SiteOfPlan(plan);

        var spec = site?.Spec ?? plan.Spec;
        var deconstructing = site?.Deconstructing ?? plan.Breaking;
        if (spec.StructureId != _structureId)
        {
            _structureId = spec.StructureId;
            if (_icon is not null)
            {
                _icon.Texture = Sprites.For(spec.StructureId);
            }

            if (_name is not null)
            {
                _name.Text = Testi.Struttura(spec.StructureId, view.Content);
            }
        }

        var missing = site is { Deconstructing: false } ? site.Missing : null;
        var stalled = missing is not null;
        if (_barFill is not null)
        {
            // Every frame: a new colour variant shows at once.
            _barBack!.Color = UiColors.FondoSlot;
            _barFill.Color = deconstructing ? UiColors.Smontaggio
                : stalled ? UiColors.Fermo
                : UiColors.Costruzione;
        }

        if (missing != _missingShown && _missing is not null)
        {
            _missingShown = missing;
            _missing.Visible = missing is not null;
            if (missing is { } line)
            {
                // Same wording as the site tooltip; money is named, materials by their display name.
                _missing.Text = Testi.F("coda.manca", line.ItemId is null
                    ? Testi.T("coda.denaro")
                    : Testi.Oggetto(line.ItemId, view.Content));
            }
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

        var site = builder.CurrentSiteId is { } siteId ? SiteById(view, siteId) : view.SiteOfPlan(builder.Plans[0]);
        if (site is { Tiles.Count: > 0 })
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
