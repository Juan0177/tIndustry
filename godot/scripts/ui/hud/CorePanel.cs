using Godot;
using TIndustry.Shared;
using static TIndustry.Godot.HudStyle;

namespace TIndustry.Godot;

/// <summary>
/// Top-left HUD column (Mindustry layout): menu buttons → core resources (funds, power, stock with the Core's
/// maximum, the Core's tier, the world's time) → build queue → campaign objectives. Owns the last stock counts and funds shown.
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

    private Control? _belowStock;
    private Label? _powerLabel;
    private TextureRect? _powerIcon;
    private Label? _moneyLabel;
    private GridContainer? _stockGrid;
    private Label? _stockEmpty;
    private Label? _tierLabel;
    private Label? _timeLabel;
    private CoreTierDefinition? _tier;
    private Func<string, int>? _capacityOf;
    // What the tier line shows now (it is written again only when one of them changes, or the language does).
    private (string? TierId, int Max) _tierShown;
    private PanelContainer? _objectivesPanel;
    private Label? _objectivesTitle;
    private VBoxContainer? _objectivesList;
    private FactoryContent? _content;
    private IWalletView? _wallet;
    private int _money;
    private readonly List<(Button Button, string Key, string Hotkey)> _menuButtons = [];
    private bool _hasGrid;
    private int _generatorsLive;
    private int _generatorsTotal;

    public event Action? MenuRequested;
    public event Action? ResearchRequested;
    public event Action? MercatoRequested;
    public event Action? CampaignRequested;

    /// <summary>Funds from the last <see cref="UpdateStock"/> (fallback when no wallet is bound).</summary>
    public int Money => _money;

    public CorePanel()
    {
        Name = "TopLeftColumn";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.TopLeft);
        OffsetLeft = 10;
        OffsetTop = 10;
        ThemeTypeVariation = UiTheme.ColonnaLarga;
    }

    public override void _Ready()
    {
        var menuRow = new HBoxContainer { Name = "MenuRow", MouseFilter = MouseFilterEnum.Ignore };
        AddChild(menuRow);
        AddMenuButton(menuRow, "menu", "menu.menu", "Esc", () => MenuRequested?.Invoke());
        AddMenuButton(menuRow, "ricerca", "menu.ricerca", "T", () => ResearchRequested?.Invoke());
        AddMenuButton(menuRow, "mercato", "menu.mercato", "M", () => MercatoRequested?.Invoke());
        AddMenuButton(menuRow, "campagna", "menu.campagna", "G", () => CampaignRequested?.Invoke());

        BuildCorePanel();
        if (_belowStock is not null)
        {
            AddChild(_belowStock);
        }

        BuildObjectivesPanel();
    }

    public override void _Notification(int what)
    {
        if (what != NotificationTranslationChanged)
        {
            return;
        }

        // New language: texts made of several parts are written again (the next sync redoes the upgrade button).
        foreach (var (button, key, hotkey) in _menuButtons)
        {
            button.TooltipText = $"{Testi.T(key)} ({hotkey})";
        }

        SyncPowerTooltip();
        _tierShown = default;
        SyncTier();
        foreach (var id in _stockCells.Keys)
        {
            SyncStockTooltip(id);
        }
    }

    /// <summary>
    /// Puts <paramref name="control"/> in this column right under the core resources (the build queue): it
    /// flows with the column, so it never covers the menu buttons whatever the window width or UI scale.
    /// Call before the panel enters the tree.
    /// </summary>
    public void PlaceBelowStock(Control control)
    {
        control.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _belowStock = control;
    }

    /// <summary>Bind content (stock tooltips, cost chip names) and wallet (stock counts).</summary>
    public void Bind(FactoryContent? content, IWalletView? wallet)
    {
        var newContent = !ReferenceEquals(content, _content) && content is not null;
        _content = content;
        _wallet = wallet;
        if (newContent)
        {
            foreach (var id in _stockCells.Keys)
            {
                SyncStockTooltip(id);
            }
        }
    }

    /// <summary>The funds tooltip as shown (playtests).</summary>
    internal string FundsTooltip => Testi.T(_moneyLabel?.TooltipText ?? "");

    /// <summary>Count from the last <see cref="UpdateStock"/> (fallback when no wallet is bound).</summary>
    public int StockCount(string itemId) => _stockCounts.TryGetValue(itemId, out var c) ? c : 0;

    public void UpdateStock(
        int ore,
        int plate,
        int copper,
        int wire,
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
            var low = hasGrid && powerBuffer < powerCapacity * 0.2f;
            // Low power is red and also says so with a "!" (meaning not by colour alone).
            _powerLabel.Text = hasGrid ? $"{powerBuffer:0}/{powerCapacity:0}{(low ? " !" : "")}" : "—";
            _powerLabel.ThemeTypeVariation = !hasGrid ? UiTheme.TestoTenue : low ? UiTheme.TestoRosso : UiTheme.TestoEnergia;
            _powerIcon.Modulate = !hasGrid ? UiColors.TestoTenue : low ? UiColors.Insufficiente : UiColors.Energia;
            if (hasGrid != _hasGrid || generatorsLive != _generatorsLive || generatorsTotal != _generatorsTotal)
            {
                (_hasGrid, _generatorsLive, _generatorsTotal) = (hasGrid, generatorsLive, generatorsTotal);
                SyncPowerTooltip();
            }
        }
    }

    private void SyncPowerTooltip()
    {
        if (_powerLabel is null)
        {
            return;
        }

        var gens = _generatorsTotal > 0 ? Testi.F("core.energia.generatori", _generatorsLive, _generatorsTotal) : "";
        _powerLabel.TooltipText = _hasGrid ? Testi.T("core.energia") + gens : Testi.T("core.energia.nessuna");
    }

    /// <summary>
    /// The Core's tier (name, maximum per item, auto-sell rate and drone on hover) and each item's maximum: a stock at the
    /// maximum shows in the "pieno" colour, its tooltip says so (the belts carrying it stop).
    /// </summary>
    public void UpdateCore(CoreTierDefinition tier, Func<string, int> capacityOf)
    {
        _tier = tier;
        _capacityOf = capacityOf;
        SyncTier();
    }

    private void SyncTier()
    {
        if (_tierLabel is null || _tier is null || _capacityOf is null)
        {
            return;
        }

        var max = _capacityOf(StockOrder[0]);
        if (_tierShown == (_tier.Id, max))
        {
            return;
        }

        _tierShown = (_tier.Id, max);
        _tierLabel.Text = Testi.F("core.livello", Testi.Struttura(_tier.Id, _content), max);
        _tierLabel.TooltipText = Testi.F("core.livello.suggerimento", _tier.AutoSellPerSecond, Testi.T($"unita.{_tier.UnitId}.nome"));
        foreach (var id in _stockCells.Keys)
        {
            SyncStockTooltip(id);
        }
    }

    /// <summary>
    /// The world's time line under the Core resources, every frame from the simulation's clock (ticks, so it stops
    /// with the pause): hidden in always day; <paramref name="attackAlert"/> shows "Attacco!".
    /// </summary>
    public void UpdateWorldTime(DayNightMode mode, DayNightPhase phase, long? firstBuildTick, long? ticksToNext, bool attackAlert)
    {
        if (_timeLabel is null)
        {
            return;
        }

        var text = WorldTimeText(mode, phase, firstBuildTick, ticksToNext, attackAlert);
        _timeLabel.Visible = text is not null;
        if (text is not null && _timeLabel.Text != text)
        {
            _timeLabel.Text = text;
        }
    }

    /// <summary>
    /// Text of the world's time line, null when there is none (always day): before the first block the time waits;
    /// in the cycle "Notte fra m:ss" by day and dusk, "Giorno fra m:ss" by night and dawn; in always night the first
    /// attack's countdown, then "Attacco!" while <paramref name="attackAlert"/>, then "Notte".
    /// </summary>
    internal static string? WorldTimeText(
        DayNightMode mode, DayNightPhase phase, long? firstBuildTick, long? ticksToNext, bool attackAlert)
    {
        if (mode == DayNightMode.AlwaysDay)
        {
            return null;
        }

        if (firstBuildTick is null)
        {
            return Testi.T("tempo.attesa");
        }

        if (attackAlert)
        {
            return Testi.T("tempo.attacco");
        }

        if (ticksToNext is not { } ticks)
        {
            return Testi.T("tempo.notte");
        }

        var clock = Clock(ticks);
        if (mode == DayNightMode.AlwaysNight)
        {
            return Testi.F("tempo.attacco.fra", clock);
        }

        return Testi.F(phase is DayNightPhase.Day or DayNightPhase.Dusk ? "tempo.notte.fra" : "tempo.giorno.fra", clock);
    }

    /// <summary>Ticks as m:ss, whole seconds rounded up (a full 10 minutes reads 10:00, the last second 0:01).</summary>
    internal static string Clock(long ticks)
    {
        var seconds = (Math.Max(0, ticks) + SimulationClock.TickRate - 1) / SimulationClock.TickRate;
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{seconds / 60}:{seconds % 60:00}");
    }

    /// <summary>The stock cell's colour and tooltip from its count and the Core maximum.</summary>
    private void SyncStockTooltip(string id)
    {
        if (!_stockCells.TryGetValue(id, out var cell))
        {
            return;
        }

        var count = StockCount(id);
        var max = _capacityOf?.Invoke(id) ?? 0;
        var full = max > 0 && count >= max;
        cell.Count.ThemeTypeVariation = full ? UiTheme.TestoPieno : "";
        var name = Testi.Oggetto(id, _content);
        cell.Cell.TooltipText = max <= 0 ? name : Testi.F(full ? "core.scorta.pieno" : "core.scorta", name, count, max);
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
        IResearchView research)
    {
        if (_objectivesPanel is null || _objectivesTitle is null || _objectivesList is null)
        {
            return;
        }

        _objectivesPanel.Visible = true;
        _objectivesTitle.Text = Testi.F("core.obiettivo", Testi.LivelloNome(level));

        foreach (var child in _objectivesList.GetChildren())
        {
            child.QueueFree();
        }

        var objectives = level.Objectives ?? [];
        for (var i = 0; i < objectives.Count; i++)
        {
            var objective = objectives[i];
            var current = CampaignProgress.GetObjectiveCurrent(objective, wallet, session, research);
            var done = CampaignProgress.IsObjectiveComplete(objective, wallet, session, research);
            var label = Testi.Obiettivo(level, i, _content);
            var target = Math.Max(1, objective.Amount);
            var text = objective.Type == CampaignObjectiveType.UnlockResearch
                ? (current >= 1 ? $"{label} ✓" : label)
                : $"{label} ({Math.Min(current, target)}/{target})";
            _objectivesList.AddChild(MakeLabel(text, done ? UiTheme.TestoVerde : UiTheme.TestoTenue));
        }
    }

    private void SetStock(string id, int count)
    {
        var old = _stockCounts.TryGetValue(id, out var before) ? before : -1;
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

        if (old != count)
        {
            SyncStockTooltip(id);
        }
    }

    private void AddMenuButton(Control parent, string icon, string label, string hotkey, Action onClick)
    {
        var button = new Button
        {
            // Stable name for the agents' ui tool: Menu, Ricerca, Mercato, Campagna.
            Name = char.ToUpperInvariant(icon[0]) + icon[1..],
            Text = label,
            Icon = UiTheme.Icon(icon, small: true),
            FocusMode = FocusModeEnum.None,
            TooltipText = $"{Testi.T(label)} ({hotkey})",
            MouseDefaultCursorShape = CursorShape.PointingHand,
            CustomMinimumSize = new Vector2(0, 34)
        };
        button.Pressed += onClick;
        parent.AddChild(button);
        _menuButtons.Add((button, label, hotkey));
    }

    private void BuildCorePanel()
    {
        var panel = MakePanel("CorePanel");
        panel.CustomMinimumSize = new Vector2(236, 0);
        panel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        AddChild(panel);

        var vbox = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, ThemeTypeVariation = UiTheme.ColonnaLarga };
        panel.AddChild(vbox);

        // Fondi · energia
        var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, ThemeTypeVariation = UiTheme.RigaLarga };
        vbox.AddChild(head);
        head.AddChild(MakeIcon(ItemTexture("money"), 20));
        _moneyLabel = MakeLabel("$0", UiTheme.TestoDenaro);
        _moneyLabel.TooltipText = "core.fondi";
        _moneyLabel.MouseFilter = MouseFilterEnum.Pass;
        head.AddChild(_moneyLabel);
        head.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        _powerIcon = MakeIcon(UiTheme.Icon("energia", small: true), 16, UiColors.TestoTenue, pixel: true);
        _powerIcon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        head.AddChild(_powerIcon);
        _powerLabel = MakeLabel("—", UiTheme.TestoTenue);
        _powerLabel.MouseFilter = MouseFilterEnum.Pass;
        head.AddChild(_powerLabel);

        // Core items: icon + count, only what you have (names on hover).
        _stockGrid = new GridContainer { Columns = 4, MouseFilter = MouseFilterEnum.Ignore, ThemeTypeVariation = UiTheme.GrigliaLarga };
        vbox.AddChild(_stockGrid);
        foreach (var id in StockOrder)
        {
            AddStockCell(_stockGrid, id);
        }

        _stockEmpty = MakeLabel("core.vuoto", UiTheme.TestoTenue);
        vbox.AddChild(_stockEmpty);

        // CORE tier · maximum per item (rate and drone on hover).
        _tierLabel = MakeLabel("", UiTheme.TestoTenue);
        _tierLabel.Name = "LivelloCore";
        _tierLabel.MouseFilter = MouseFilterEnum.Pass;
        vbox.AddChild(_tierLabel);

        // World's time: countdown to the night, the day or the first attack (hidden in always day).
        _timeLabel = MakeLabel("", UiTheme.TestoTenue);
        _timeLabel.Name = "TempoMondo";
        _timeLabel.Visible = false;
        vbox.AddChild(_timeLabel);
    }

    private void AddStockCell(Control parent, string id)
    {
        var cell = new HBoxContainer
        {
            Visible = false,
            MouseFilter = MouseFilterEnum.Pass,
            TooltipText = Testi.Oggetto(id, _content),
            CustomMinimumSize = new Vector2(46, 22),
            ThemeTypeVariation = UiTheme.RigaStretta
        };
        parent.AddChild(cell);
        cell.AddChild(MakeIcon(ItemTexture(id), 20));
        var count = MakeLabel("0");
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

        var vbox = new VBoxContainer();
        _objectivesPanel.AddChild(vbox);

        _objectivesTitle = MakeLabel("core.obiettivo.titolo", UiTheme.TestoAccento);
        vbox.AddChild(_objectivesTitle);

        _objectivesList = new VBoxContainer { ThemeTypeVariation = UiTheme.ColonnaStretta };
        vbox.AddChild(_objectivesList);
    }
}
