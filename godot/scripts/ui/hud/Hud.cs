using Godot;
using TIndustry.Shared;
using static TIndustry.Godot.HudStyle;

namespace TIndustry.Godot;

/// <summary>
/// Mindustry-style factory HUD: corner sprite palette + block info + Core stock.
/// Italian strings; cursor-default; R rotate / RMB remove stay off the palette.
/// Composes <see cref="CorePanel"/> (top-left), <see cref="BlockInfoPanel"/> and
/// <see cref="BuildPalette"/> (bottom-right stack) and the toast; World talks only to this class.
/// </summary>
public partial class Hud : Control
{
    public enum ToolKind
    {
        Cursor,
        Belt,
        BeltFast,
        BeltExpress,
        Miner,
        MinerAdvanced,
        Smelter,
        Assembler,
        Junction,
        Splitter,
        Generator,
        Sorter,
        Bridge,
        Extractor,
        PowerNode,
        PowerNodeT2
    }

    internal enum BuildCategory
    {
        Tools,
        Logistics,
        Production,
        Power
    }

    /// <summary>Width of the bottom-right stack (info slot + palette).</summary>
    internal const float CornerStackWidth = 292f;

    private readonly CorePanel _core = new();
    private readonly BuildPalette _palette = new();
    private readonly BlockInfoPanel _info = new();
    private readonly BuildQueuePanel _queue = new();

    private Label? _dirLabel;
    private PanelContainer? _siteTip;
    private Label? _siteTipLabel;
    private string? _siteTipText;
    private Label? _toastLabel;
    private Tween? _toastTween;

    public event Action<ToolKind>? ToolChosen;
    public event Action? ResearchRequested;
    public event Action? MercatoRequested;
    public event Action? CampaignRequested;
    public event Action? CoreUpgradeRequested;
    public event Action? MenuRequested;

    /// <summary>Click on the build queue: world rect of the block the drone works on.</summary>
    public event Action<Rect2>? BuildQueueFocusRequested;

    public ToolKind SelectedTool => _palette.Selected;
    public bool IsCursorMode => _palette.Selected == ToolKind.Cursor;

    public Hud()
    {
        _core.MenuRequested += () => MenuRequested?.Invoke();
        _core.ResearchRequested += () => ResearchRequested?.Invoke();
        _core.MercatoRequested += () => MercatoRequested?.Invoke();
        _core.CampaignRequested += () => CampaignRequested?.Invoke();
        _core.CoreUpgradeRequested += () => CoreUpgradeRequested?.Invoke();

        _palette.Hovered += _ => RefreshBlockInfo();
        _palette.ToolChosen += tool =>
        {
            RefreshBlockInfo();
            ToolChosen?.Invoke(tool);
        };
        _palette.ToastRequested += ShowToast;
        _queue.FocusRequested += rect => BuildQueueFocusRequested?.Invoke(rect);

        _info.Stock = _core;
        _info.DetailRequested += () =>
        {
            if (DetailFocus() is { } focus)
            {
                _info.OpenDetail(focus);
            }
        };
    }

    public override void _Ready()
    {
        Name = "FactoryHudRoot";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        AddChild(_core);
        AddChild(_queue);
        BuildCornerDock();
        AddChild(_info.BuildDetailOverlay());
        BuildToast();
        BuildSiteTooltip();
        SetSelectedTool(ToolKind.Cursor);
        SetDirectionLabel("Est");
        ClearObjectives();
        _palette.ShowCategory(BuildCategory.Logistics);
    }

    /// <summary>Bind content + wallet so the info panel can show costs / barred mats.</summary>
    public void BindEconomy(FactoryContent? content, IWalletView? wallet)
    {
        _core.Bind(content, wallet);
        _palette.Content = content;
        _info.Bind(content, wallet);
        RefreshBlockInfo();
    }

    public void SetSelectedTool(ToolKind tool)
    {
        _palette.Select(tool);
        RefreshBlockInfo();
    }

    /// <summary>Brush filter shown on the sorter palette glyph (and used when placing).</summary>
    public void SetSorterFilterBrush(string itemId) => _palette.SetSorterFilterBrush(itemId);

    public void SetToolLocked(ToolKind tool, bool locked)
    {
        if (tool != ToolKind.Cursor)
        {
            SetToolsLocked([(tool, locked)]);
        }
    }

    public void SetToolsLocked(IEnumerable<(ToolKind Tool, bool Locked)> states)
    {
        _palette.SetLocked(states);
        RefreshBlockInfo();
    }

    public bool IsToolLocked(ToolKind tool) => _palette.IsLocked(tool);

    public void ClearToolSelection(bool toast = false)
    {
        SetSelectedTool(ToolKind.Cursor);
        ToolChosen?.Invoke(ToolKind.Cursor);
        if (toast)
        {
            ShowToast("Cursore");
        }
    }

    public void SetDirectionLabel(string directionIt)
    {
        if (_dirLabel is not null)
        {
            _dirLabel.Text = $"R · {directionIt} · destro = elimina";
        }
    }

    public void UpdateStock(
        string oreName, int ore,
        string plateName, int plate,
        string copperName, int copper,
        string wireName, int wire,
        long delivered,
        int onBelt,
        int money = 0,
        int generatorsLive = 0,
        int generatorsTotal = 0,
        float powerBuffer = -1f,
        float powerCapacity = -1f)
    {
        _core.UpdateStock(oreName, ore, plateName, plate, copperName, copper, wireName, wire,
            delivered, onBelt, money, generatorsLive, generatorsTotal, powerBuffer, powerCapacity);
        RefreshBlockInfo();
    }

    /// <summary>CORE upgrade chip: cost while available, LV+bonus when done.</summary>
    public void SyncCoreUpgrade(IGameView? factory) => _core.SyncCoreUpgrade(factory);

    public void ClearObjectives() => _core.ClearObjectives();

    public void UpdateObjectives(
        CampaignLevelDefinition level,
        IWalletView wallet,
        ISessionView session,
        IResearchView research,
        Func<string, string>? itemName = null) =>
        _core.UpdateObjectives(level, wallet, session, research, itemName);

    /// <summary>Build queue box (top centre): the local drone's current block, its bar and the plans left.</summary>
    public void SyncBuildQueue(IGameView? view) => _queue.Sync(view);

    /// <summary>
    /// Tooltip beside the mouse at <paramref name="mouse"/> (viewport coordinates) over a stalled site
    /// ("Mancano: …"); null hides it. The text is set only when it changes.
    /// </summary>
    public void SetSiteTooltip(string? text, Vector2 mouse)
    {
        if (_siteTip is null || _siteTipLabel is null)
        {
            return;
        }

        if (text is null)
        {
            _siteTip.Visible = false;
            return;
        }

        if (!string.Equals(_siteTipText, text, StringComparison.Ordinal))
        {
            _siteTipText = text;
            _siteTipLabel.Text = text;
            _siteTip.ResetSize();
        }

        _siteTip.Position = mouse + new Vector2(16, 18);
        _siteTip.Visible = true;
    }

    public void ShowToast(string message)
    {
        if (_toastLabel is null)
        {
            return;
        }

        _toastLabel.Text = message;
        _toastLabel.Modulate = Colors.White;
        _toastTween?.Kill();
        _toastTween = CreateTween();
        _toastTween.TweenInterval(1.6);
        _toastTween.TweenProperty(_toastLabel, "modulate:a", 0f, 0.45);
    }

    public static string StructureIdFor(ToolKind tool) => tool switch
    {
        ToolKind.Belt => "conveyor-basic",
        ToolKind.BeltFast => "conveyor-fast",
        ToolKind.BeltExpress => "conveyor-express",
        ToolKind.Miner => "miner",
        ToolKind.MinerAdvanced => "miner-advanced",
        ToolKind.Smelter => "smelter",
        ToolKind.Assembler => "assembler",
        ToolKind.Junction => "junction",
        ToolKind.Splitter => "splitter",
        ToolKind.Generator => "generator",
        ToolKind.Sorter => "sorter",
        ToolKind.Bridge => "conveyor-bridge",
        ToolKind.Extractor => "extractor",
        ToolKind.PowerNode => "power-node",
        ToolKind.PowerNodeT2 => "power-node-t2",
        _ => "conveyor-basic"
    };

    /// <summary>Info strip shows the hovered cell, else the selected tool; empty in cursor mode.</summary>
    private void RefreshBlockInfo()
    {
        var selected = _palette.Selected;
        var focus = _palette.HoveredTool ?? (selected == ToolKind.Cursor ? null : selected);
        if (focus is null || focus == ToolKind.Cursor)
        {
            focus = selected != ToolKind.Cursor ? selected : null;
        }

        _info.Show(focus, focus is { } f && _palette.IsLocked(f));
    }

    /// <summary>Block the "?" overlay describes: hovered cell, else the selected place tool.</summary>
    private ToolKind? DetailFocus()
    {
        var selected = _palette.Selected;
        return _palette.HoveredTool ?? (selected == ToolKind.Cursor ? null : selected);
    }

    private void BuildCornerDock()
    {
        // Bottom-right stack: [reserved info slot] then [build palette].
        // Slot keeps fixed height when empty so the palette never jumps.
        var cornerStack = new VBoxContainer
        {
            Name = "CornerDockStack",
            MouseFilter = MouseFilterEnum.Ignore
        };
        cornerStack.SetAnchorsPreset(LayoutPreset.BottomRight);
        cornerStack.GrowHorizontal = GrowDirection.Begin;
        cornerStack.GrowVertical = GrowDirection.Begin;
        cornerStack.OffsetLeft = -CornerStackWidth - 8;
        cornerStack.OffsetRight = -8;
        cornerStack.OffsetBottom = -8;
        cornerStack.OffsetTop = -8; // grows upward via min sizes
        cornerStack.AddThemeConstantOverride("separation", 8);
        AddChild(cornerStack);

        cornerStack.AddChild(_info);
        // Build palette dock — always below the reserved slot.
        cornerStack.AddChild(_palette);
    }

    private void BuildSiteTooltip()
    {
        _siteTip = MakePanel("SiteTooltip");
        _siteTip.MouseFilter = MouseFilterEnum.Ignore;
        _siteTip.Visible = false;
        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        _siteTip.AddChild(margin);
        _siteTipLabel = new Label { MouseFilter = MouseFilterEnum.Ignore };
        _siteTipLabel.AddThemeColorOverride("font_color", Insufficient);
        _siteTipLabel.AddThemeFontSizeOverride("font_size", 13);
        margin.AddChild(_siteTipLabel);
        AddChild(_siteTip);
    }

    private void BuildToast()
    {
        _toastLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _toastLabel.SetAnchorsPreset(LayoutPreset.CenterBottom);
        _toastLabel.GrowHorizontal = GrowDirection.Both;
        _toastLabel.OffsetLeft = -220;
        _toastLabel.OffsetRight = 220;
        _toastLabel.OffsetTop = -236;
        _toastLabel.OffsetBottom = -208;
        _toastLabel.AddThemeColorOverride("font_color", TextPrimary);
        _toastLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.75f));
        _toastLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _toastLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        _toastLabel.AddThemeFontSizeOverride("font_size", 15);
        _toastLabel.Modulate = new Color(1, 1, 1, 0);
        AddChild(_toastLabel);
    }
}
