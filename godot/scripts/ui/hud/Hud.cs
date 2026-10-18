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
        PowerNodeT2,
        Lamp
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
    // Seconds since the toast was shown; it holds, then fades (no Tween: nothing left for the GC at exit).
    private double _toastAge = ToastHold + ToastFade;
    private const double ToastHold = 1.6;
    private const double ToastFade = 0.45;
    private Label? _placementHint;

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
        _core.PlaceBelowStock(_queue);

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
        BuildCornerDock();
        AddChild(_info.BuildDetailOverlay());
        BuildToast();
        BuildPlacementHint();
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

    /// <summary>The UI scale changed (<see cref="UiScale"/>): the palette rebuilds its block icons.</summary>
    public void OnUiScaleChanged() => _palette.OnUiScaleChanged();

    /// <summary>The category the build palette shows (playtests).</summary>
    internal BuildCategory PaletteCategory => _palette.ShownCategory;

    /// <summary>Build queue box (top-left column, under the core resources): current block, its bar and the plans left.</summary>
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

        // The mouse comes in viewport pixels; the HUD is laid out in UI units (scaled layer).
        _siteTip.Position = UiScale.ToUi(mouse) + new Vector2(16, 18);
        _siteTip.Visible = true;
    }

    /// <summary>
    /// Bottom hint under the toast line: why the ghost under the mouse is refused by the terrain ("C'è una parete",
    /// "Acqua profonda"); null hides it. The text is set only when it changes.
    /// </summary>
    public void SetPlacementHint(string? text)
    {
        if (_placementHint is null)
        {
            return;
        }

        _placementHint.Visible = text is not null;
        if (text is not null && !string.Equals(_placementHint.Text, text, StringComparison.Ordinal))
        {
            _placementHint.Text = text;
        }
    }

    public void ShowToast(string message)
    {
        if (_toastLabel is null)
        {
            return;
        }

        _toastLabel.Text = message;
        _toastLabel.Modulate = Colors.White;
        _toastAge = 0;
    }

    public override void _Process(double delta)
    {
        if (_toastLabel is null || _toastAge >= ToastHold + ToastFade)
        {
            return;
        }

        _toastAge += delta;
        var alpha = _toastAge <= ToastHold ? 1.0 : Math.Max(0.0, 1.0 - (_toastAge - ToastHold) / ToastFade);
        _toastLabel.Modulate = new Color(1, 1, 1, (float)alpha);
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
        ToolKind.Lamp => "lamp",
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
        cornerStack.ThemeTypeVariation = UiTheme.ColonnaLarga;
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
        _siteTipLabel = MakeLabel("", UiTheme.TestoRosso);
        _siteTip.AddChild(_siteTipLabel);
        AddChild(_siteTip);
    }

    private void BuildPlacementHint()
    {
        _placementHint = new Label
        {
            Name = "PlacementHint",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            ThemeTypeVariation = UiTheme.AvvisoRosso,
            Visible = false
        };
        _placementHint.SetAnchorsPreset(LayoutPreset.CenterBottom);
        _placementHint.GrowHorizontal = GrowDirection.Both;
        _placementHint.OffsetLeft = -220;
        _placementHint.OffsetRight = 220;
        _placementHint.OffsetTop = -44;
        _placementHint.OffsetBottom = -16;
        AddChild(_placementHint);
    }

    private void BuildToast()
    {
        _toastLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            ThemeTypeVariation = UiTheme.Avviso
        };
        _toastLabel.SetAnchorsPreset(LayoutPreset.CenterBottom);
        _toastLabel.GrowHorizontal = GrowDirection.Both;
        _toastLabel.OffsetLeft = -220;
        _toastLabel.OffsetRight = 220;
        _toastLabel.OffsetTop = -236;
        _toastLabel.OffsetBottom = -208;
        _toastLabel.Modulate = new Color(1, 1, 1, 0);
        AddChild(_toastLabel);
    }
}
