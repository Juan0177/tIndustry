using Godot;
using TIndustry.Shared;
using static TIndustry.Godot.HudStyle;

namespace TIndustry.Godot;

/// <summary>
/// Top-left HUD column (Mindustry layout): menu buttons → core resources (funds, power, stock,
/// CORE upgrade) → campaign objectives. Owns the last stock counts and funds shown.
/// </summary>
public partial class CorePanel : VBoxContainer
{
    /// <summary>Core stock order (Mindustry shows every core item; we show the ones you have).</summary>
    private static readonly string[] StockOrder =
    [
        "iron-ore", "copper-ore", "coal", "lead-ore", "titanium-ore",
        "iron-plate", "copper-wire", "lead-plate", "titanium-plate", "graphite", "silicon"
    ];

    private readonly Dictionary<string, (Control Cell, Label Count)> _stockCells = [];
    private readonly Dictionary<string, int> _stockCounts = [];

    private Label? _powerLabel;
    private TextureRect? _powerIcon;
    private Label? _moneyLabel;
    private GridContainer? _stockGrid;
    private Label? _stockEmpty;
    private Button? _coreUpgradeButton;
    private HBoxContainer? _coreUpgradeCost;
    private (int Level, bool Affordable, int Money)? _coreUpgradeKey;
    private PanelContainer? _objectivesPanel;
    private Label? _objectivesTitle;
    private VBoxContainer? _objectivesList;
    private FactoryContent? _content;
    private IWalletView? _wallet;
    private int _money;

    public event Action? MenuRequested;
    public event Action? ResearchRequested;
    public event Action? MercatoRequested;
    public event Action? CampaignRequested;
    public event Action? CoreUpgradeRequested;

    /// <summary>Funds from the last <see cref="UpdateStock"/> (fallback when no wallet is bound).</summary>
    public int Money => _money;

    public CorePanel()
    {
        Name = "TopLeftColumn";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.TopLeft);
        OffsetLeft = 10;
        OffsetTop = 10;
        AddThemeConstantOverride("separation", 6);
    }

    public override void _Ready()
    {
        var menuRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        menuRow.AddThemeConstantOverride("separation", 4);
        AddChild(menuRow);
        AddMenuButton(menuRow, "res://assets/ui/menu.png", "Menu", "Esc", () => MenuRequested?.Invoke());
        AddMenuButton(menuRow, "res://assets/ui/research.png", "Ricerca", "T", () => ResearchRequested?.Invoke());
        AddMenuButton(menuRow, "res://assets/ui/sell.png", "Mercato", "M", () => MercatoRequested?.Invoke());
        AddMenuButton(menuRow, "res://assets/core.png", "Campagna", "G", () => CampaignRequested?.Invoke(), tintIcon: false);

        BuildCorePanel();
        BuildObjectivesPanel();
    }

    /// <summary>Bind content (stock tooltips, cost chip names) and wallet (stock counts).</summary>
    public void Bind(FactoryContent? content, IWalletView? wallet)
    {
        if (!ReferenceEquals(content, _content) && content is not null)
        {
            foreach (var (id, cell) in _stockCells)
            {
                cell.Cell.TooltipText = content.DisplayName(id);
            }
        }

        _content = content;
        _wallet = wallet;
    }

    /// <summary>Count from the last <see cref="UpdateStock"/> (fallback when no wallet is bound).</summary>
    public int StockCount(string itemId) => _stockCounts.TryGetValue(itemId, out var c) ? c : 0;

    public void UpdateStock(
        string oreName, int ore,
        string plateName, int plate,
        string copperName, int copper,
        string wireName, int wire,
        long delivered,
        int onBelt,
        int money,
        int generatorsLive,
        int generatorsTotal,
        float powerBuffer,
        float powerCapacity)
    {
        _money = money;
        if (_moneyLabel is not null)
        {
            _moneyLabel.Text = $"${money}";
        }

        var any = false;
        foreach (var id in StockOrder)
        {
            var count = _wallet?.MaterialCount(id) ?? id switch
            {
                "iron-ore" => ore,
                "iron-plate" => plate,
                "copper-ore" => copper,
                "copper-wire" => wire,
                _ => 0
            };
            SetStock(id, count);
            any |= count > 0;
        }

        if (_stockEmpty is not null)
        {
            _stockEmpty.Visible = !any;
        }

        if (_powerLabel is not null && _powerIcon is not null)
        {
            var hasGrid = powerCapacity > 0f && powerBuffer >= 0f;
            _powerLabel.Text = hasGrid ? $"{powerBuffer:0}/{powerCapacity:0}" : "—";
            var low = hasGrid && powerBuffer < powerCapacity * 0.2f;
            var tint = !hasGrid ? TextMuted : low ? Insufficient : PowerColor;
            _powerLabel.AddThemeColorOverride("font_color", tint);
            _powerIcon.Modulate = tint;
            var gens = generatorsTotal > 0 ? $" · generatori attivi {generatorsLive}/{generatorsTotal}" : "";
            _powerLabel.TooltipText = hasGrid ? $"Energia accumulata / capacità{gens}" : "Nessuna rete elettrica";
        }
    }

    /// <summary>CORE upgrade chip: cost while available, LV+bonus when done.</summary>
    public void SyncCoreUpgrade(IGameView? factory)
    {
        if (_coreUpgradeButton is null || _coreUpgradeCost is null)
        {
            return;
        }

        var level = factory?.CoreUpgradeLevel ?? -1;
        var affordable = factory is not null
            && factory.Wallet.CanAfford(factory.Content.CoreUpgrade.MoneyCost, factory.Content.CoreUpgrade.BuildCost);
        var key = (level, affordable, factory?.Wallet.Money ?? 0);
        if (_coreUpgradeKey == key)
        {
            return;
        }

        _coreUpgradeKey = key;
        foreach (var child in _coreUpgradeCost.GetChildren())
        {
            child.QueueFree();
        }

        if (factory is null)
        {
            _coreUpgradeButton.Text = "Potenzia CORE";
            _coreUpgradeButton.Disabled = true;
            return;
        }

        var upgrade = factory.Content.CoreUpgrade;
        if (factory.CoreUpgradeLevel > 0)
        {
            _coreUpgradeButton.Text = $"CORE LV{factory.CoreUpgradeLevel} · +{factory.CoreSaleBonusPercent}% vendite";
            _coreUpgradeButton.Disabled = true;
            _coreUpgradeButton.TooltipText = "";
            return;
        }

        _coreUpgradeButton.Text = "Potenzia CORE";
        _coreUpgradeButton.Disabled = !affordable;
        _coreUpgradeButton.TooltipText = factory.FormatCoreUpgradeNeedMessage();
        if (upgrade.MoneyCost > 0)
        {
            _coreUpgradeCost.AddChild(MakeMoneyChip(upgrade.MoneyCost, factory.Wallet.Money >= upgrade.MoneyCost));
        }

        foreach (var cost in upgrade.BuildCost.Where(c => c.Amount > 0))
        {
            _coreUpgradeCost.AddChild(MakeCostChip(
                cost.ItemId, cost.Amount, factory.Wallet.MaterialCount(cost.ItemId) >= cost.Amount, _content));
        }
    }

    public void ClearObjectives()
    {
        if (_objectivesPanel is not null)
        {
            _objectivesPanel.Visible = false;
        }

        if (_objectivesList is null)
        {
            return;
        }

        foreach (var child in _objectivesList.GetChildren())
        {
            child.QueueFree();
        }
    }

    public void UpdateObjectives(
        CampaignLevelDefinition level,
        IWalletView wallet,
        ISessionView session,
        IResearchView research,
        Func<string, string>? itemName = null)
    {
        if (_objectivesPanel is null || _objectivesTitle is null || _objectivesList is null)
        {
            return;
        }

        _objectivesPanel.Visible = true;
        _objectivesTitle.Text = $"OBIETTIVO · {level.Name}";

        foreach (var child in _objectivesList.GetChildren())
        {
            child.QueueFree();
        }

        var objectives = level.Objectives ?? [];
        foreach (var objective in objectives)
        {
            var current = CampaignProgress.GetObjectiveCurrent(objective, wallet, session, research);
            var done = CampaignProgress.IsObjectiveComplete(objective, wallet, session, research);
            var text = CampaignCatalog.FormatObjectiveProgress(objective, current, itemName);
            var label = new Label { Text = text };
            label.AddThemeColorOverride("font_color",
                done ? new Color(0.45f, 0.85f, 0.55f) : TextMuted);
            label.AddThemeFontSizeOverride("font_size", 12);
            _objectivesList.AddChild(label);
        }
    }

    private void SetStock(string id, int count)
    {
        _stockCounts[id] = count;
        if (!_stockCells.TryGetValue(id, out var cell))
        {
            return;
        }

        cell.Cell.Visible = count > 0;
        var text = FormatCount(count);
        if (cell.Count.Text != text)
        {
            cell.Count.Text = text;
        }
    }

    private static void AddMenuButton(Control parent, string texPath, string label, string hotkey, Action onClick, bool tintIcon = true)
    {
        var button = new Button
        {
            Text = label,
            Icon = ResourceLoader.Exists(texPath) ? GD.Load<Texture2D>(texPath) : null,
            FocusMode = FocusModeEnum.None,
            TooltipText = $"{label} ({hotkey})",
            MouseDefaultCursorShape = CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(0, 34)
        };
        button.AddThemeConstantOverride("icon_max_width", 18);
        button.AddThemeConstantOverride("h_separation", 6);
        button.AddThemeFontSizeOverride("font_size", 13);
        button.AddThemeColorOverride("font_color", TextPrimary);
        button.AddThemeColorOverride("font_hover_color", SlotSelected);
        button.AddThemeColorOverride("font_pressed_color", SlotSelected);
        button.AddThemeColorOverride("icon_normal_color", tintIcon ? TextPrimary : Colors.White);
        button.AddThemeColorOverride("icon_hover_color", tintIcon ? SlotSelected : Colors.White);
        button.AddThemeColorOverride("icon_pressed_color", tintIcon ? SlotSelected : Colors.White);
        foreach (var state in new[] { "normal", "hover", "pressed" })
        {
            var style = MakeSlotStyle(state == "hover" ? SlotSelected : PanelEdge, 1);
            style.BgColor = state == "pressed" ? new Color(0.22f, 0.22f, 0.16f, 0.95f) : PanelBg;
            style.ContentMarginLeft = 8;
            style.ContentMarginRight = 10;
            button.AddThemeStyleboxOverride(state, style);
        }

        button.Pressed += onClick;
        parent.AddChild(button);
    }

    private void BuildCorePanel()
    {
        var panel = MakePanel("CorePanel");
        panel.CustomMinimumSize = new Vector2(236, 0);
        panel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        AddChild(panel);

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        panel.AddChild(margin);

        var vbox = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        vbox.AddThemeConstantOverride("separation", 6);
        margin.AddChild(vbox);

        // Fondi · energia
        var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", 6);
        vbox.AddChild(head);
        head.AddChild(MakeIcon(ItemTexture("money"), 20, MoneyColor));
        _moneyLabel = new Label { Text = "$0", TooltipText = "Fondi", MouseFilter = MouseFilterEnum.Pass };
        _moneyLabel.AddThemeColorOverride("font_color", MoneyColor);
        _moneyLabel.AddThemeFontSizeOverride("font_size", 16);
        head.AddChild(_moneyLabel);
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        _powerIcon = MakeIcon(
            ResourceLoader.Exists("res://assets/ui/power.png") ? GD.Load<Texture2D>("res://assets/ui/power.png") : null,
            16,
            TextMuted);
        _powerIcon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(_powerIcon);
        _powerLabel = new Label { Text = "—", MouseFilter = MouseFilterEnum.Pass, VerticalAlignment = VerticalAlignment.Center };
        _powerLabel.AddThemeFontSizeOverride("font_size", 13);
        head.AddChild(_powerLabel);

        // Core items: icon + count, only what you have (names on hover).
        _stockGrid = new GridContainer { Columns = 4, MouseFilter = MouseFilterEnum.Ignore };
        _stockGrid.AddThemeConstantOverride("h_separation", 10);
        _stockGrid.AddThemeConstantOverride("v_separation", 4);
        vbox.AddChild(_stockGrid);
        foreach (var id in StockOrder)
        {
            AddStockCell(_stockGrid, id);
        }

        _stockEmpty = new Label { Text = "Core vuoto: porta minerali con i nastri", MouseFilter = MouseFilterEnum.Ignore };
        _stockEmpty.AddThemeColorOverride("font_color", TextMuted);
        _stockEmpty.AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(_stockEmpty);

        var upgradeRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        upgradeRow.AddThemeConstantOverride("separation", 6);
        vbox.AddChild(upgradeRow);
        _coreUpgradeButton = new Button
        {
            Text = "Potenzia CORE",
            Flat = true,
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        _coreUpgradeButton.AddThemeColorOverride("font_color", TextPrimary);
        _coreUpgradeButton.AddThemeColorOverride("font_hover_color", SlotSelected);
        _coreUpgradeButton.AddThemeColorOverride("font_disabled_color", TextMuted);
        _coreUpgradeButton.AddThemeFontSizeOverride("font_size", 12);
        _coreUpgradeButton.Pressed += () => CoreUpgradeRequested?.Invoke();
        upgradeRow.AddChild(_coreUpgradeButton);
        _coreUpgradeCost = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _coreUpgradeCost.AddThemeConstantOverride("separation", 4);
        upgradeRow.AddChild(_coreUpgradeCost);
    }

    private void AddStockCell(Control parent, string id)
    {
        var cell = new HBoxContainer
        {
            Visible = false,
            MouseFilter = MouseFilterEnum.Pass,
            TooltipText = _content?.DisplayName(id) ?? id,
            CustomMinimumSize = new Vector2(46, 22)
        };
        cell.AddThemeConstantOverride("separation", 3);
        parent.AddChild(cell);
        var icon = MakeIcon(ItemTexture(id), 20);
        ItemPalette.Tint(icon, id);
        cell.AddChild(icon);
        var count = new Label { Text = "0", MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        count.AddThemeColorOverride("font_color", TextPrimary);
        count.AddThemeFontSizeOverride("font_size", 13);
        cell.AddChild(count);
        _stockCells[id] = (cell, count);
    }

    private void BuildObjectivesPanel()
    {
        _objectivesPanel = MakePanel("ObjectivesPanel");
        _objectivesPanel.CustomMinimumSize = new Vector2(236, 0);
        _objectivesPanel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _objectivesPanel.Visible = false;
        AddChild(_objectivesPanel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        _objectivesPanel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        margin.AddChild(vbox);

        _objectivesTitle = new Label { Text = "OBIETTIVO" };
        _objectivesTitle.AddThemeColorOverride("font_color", SlotSelected);
        _objectivesTitle.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(_objectivesTitle);

        _objectivesList = new VBoxContainer();
        _objectivesList.AddThemeConstantOverride("separation", 2);
        vbox.AddChild(_objectivesList);
    }
}
