using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Mindustry-style factory HUD: corner sprite palette + block info + Core stock.
/// Italian strings; cursor-default; R rotate / RMB remove stay off the palette.
/// </summary>
public partial class FactoryHud : Control
{
    public enum ToolKind
    {
        Cursor,
        Belt,
        BeltFast,
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
        PowerNode
    }

    private enum BuildCategory
    {
        Tools,
        Logistics,
        Production,
        Power
    }

    private sealed record PaletteEntry(
        ToolKind Tool,
        string StructureId,
        string TexPath,
        string Hotkey,
        string HintIt);

    private static readonly Color PanelBg = new(0.08f, 0.09f, 0.10f, 0.92f);
    private static readonly Color PanelEdge = new(0.28f, 0.32f, 0.30f, 1f);
    private static readonly Color SlotBg = new(0.14f, 0.16f, 0.15f, 1f);
    private static readonly Color SlotIdle = new(0.38f, 0.42f, 0.40f, 1f);
    private static readonly Color SlotSelected = new(0.92f, 0.78f, 0.28f, 1f);
    private static readonly Color TextPrimary = new(0.93f, 0.95f, 0.90f, 1f);
    private static readonly Color TextMuted = new(0.68f, 0.74f, 0.68f, 1f);
    private static readonly Color Insufficient = new(0.90f, 0.38f, 0.32f, 1f);
    private static readonly Color CatTools = new(0.78f, 0.52f, 0.48f, 1f);
    private static readonly Color CatLogistics = new(0.48f, 0.68f, 0.84f, 1f);
    private static readonly Color CatProduction = new(0.84f, 0.60f, 0.30f, 1f);
    private static readonly Color CatPower = new(0.92f, 0.80f, 0.30f, 1f);

    private static readonly PaletteEntry[] ToolsEntries =
    [
        new(ToolKind.Cursor, "", "", "Esc", "Cursore: pan / guarda · Esc o riesci sul tool")
    ];

    private static readonly PaletteEntry[] LogisticsEntries =
    [
        new(ToolKind.Belt, "conveyor-basic", "res://assets/conveyor-basic.png", "1",
            "Nastro T1 · flusso unidirezionale · R/rotella"),
        new(ToolKind.BeltFast, "conveyor-fast", "res://assets/conveyor-basic.png", "",
            "Nastro T2 · più veloce · sblocca in Ricerca"),
        new(ToolKind.Junction, "junction", "res://assets/junction.png", "5",
            "Incrocio a croce"),
        new(ToolKind.Splitter, "splitter", "res://assets/splitter.png", "6",
            "Nastro a T · alterna sinistra/destra"),
        new(ToolKind.Sorter, "sorter", "res://assets/sorter.png", "8",
            "Filtro item · C cicla · match avanti"),
        new(ToolKind.Bridge, "conveyor-bridge", "res://assets/bridge.png", "9",
            "Ponte span 2–4 · estremi 1×1")
    ];

    private static readonly PaletteEntry[] ProductionEntries =
    [
        new(ToolKind.Miner, "miner", "res://assets/miner.png", "2",
            "Estrae minerali · uscita su tutti i lati · 2×2"),
        new(ToolKind.MinerAdvanced, "miner-advanced", "res://assets/miner.png", "",
            "Minatore T2 · 2× velocità · sblocca in Ricerca"),
        new(ToolKind.Smelter, "smelter", "res://assets/smelter.png", "3",
            "Carbone o corrente · +20% craft se alimentato · 2×2"),
        new(ToolKind.Assembler, "assembler", "res://assets/assembler.png", "4",
            "Assembla prodotti · R ruota uscita · 2×2"),
        new(ToolKind.Extractor, "extractor", "res://assets/miner.png", "",
            "Estrae dal Core → nastro · F filtro · R uscita · 1×1")
    ];

    private static readonly PaletteEntry[] PowerEntries =
    [
        new(ToolKind.Generator, "generator", "res://assets/generator.png", "7",
            "Brucia carbone per energia · 2×2"),
        new(ToolKind.PowerNode, "power-node", "res://assets/generator.png", "",
            "Nodo T1 · raggio 6 · collega generatore ↔ forno")
    ];

    private readonly Dictionary<ToolKind, PanelContainer> _toolSlots = [];
    private readonly Dictionary<string, Label> _stockLabels = [];
    private readonly Dictionary<string, int> _stockCounts = [];
    private readonly HashSet<ToolKind> _lockedTools = [];
    private readonly Dictionary<BuildCategory, PanelContainer> _categorySlots = [];
    private readonly List<PanelContainer> _gridSlots = [];

    private Label? _dirLabel;
    private Label? _toastLabel;
    private Label? _titleLabel;
    private Label? _moneyLabel;
    private PanelContainer? _objectivesPanel;
    private Label? _objectivesTitle;
    private VBoxContainer? _objectivesList;
    private PanelContainer? _infoPanel;
    private Control? _infoSlot;
    private Label? _infoName;
    private HBoxContainer? _infoCostRow;
    private PanelContainer? _detailOverlay;
    private Label? _detailBody;
    private GridContainer? _blockGrid;
    private Label? _categoryTitle;
    private PanelContainer? _buildDock;
    private VBoxContainer? _cornerStack;
    private ToolKind _selected = ToolKind.Cursor;
    private BuildCategory _category = BuildCategory.Logistics;
    private ToolKind? _hovered;
    private Tween? _toastTween;
    private FactoryContent? _content;
    private EconomyWallet? _wallet;
    private int _money;

    /// <summary>Reserved height above the palette for the hover/select strip (empty = invisible).</summary>
    private const float InfoSlotHeight = 78f;
    private const float CornerStackWidth = 292f;

    public event Action<ToolKind>? ToolChosen;
    public event Action? SaveRequested;
    public event Action? LoadRequested;
    public event Action? SaveSlotRequested;
    public event Action? LoadSlotRequested;
    public event Action? ResearchRequested;
    public event Action? MercatoRequested;
    public event Action? CampaignRequested;

    public ToolKind SelectedTool => _selected;
    public bool IsCursorMode => _selected == ToolKind.Cursor;

    public override void _Ready()
    {
        Name = "FactoryHudRoot";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        BuildStockPanel();
        BuildObjectivesPanel();
        BuildCornerDock();
        BuildDetailOverlay();
        BuildToast();
        SetSelectedTool(ToolKind.Cursor);
        SetDirectionLabel("Est");
        ClearObjectives();
        ShowCategory(BuildCategory.Logistics);
    }

    /// <summary>Bind content + wallet so the info panel can show costs / barred mats.</summary>
    public void BindEconomy(FactoryContent? content, EconomyWallet? wallet)
    {
        _content = content;
        _wallet = wallet;
        RefreshBlockInfo();
    }

    public void SetSelectedTool(ToolKind tool)
    {
        _selected = tool;
        var cat = CategoryFor(tool);
        if (cat != _category && tool != ToolKind.Cursor)
        {
            ShowCategory(cat);
        }
        else if (tool == ToolKind.Cursor && _category != BuildCategory.Tools)
        {
            // Keep current category grid; cursor lives in Tools but utility strip also clears.
        }

        RefreshToolChrome();
        RefreshBlockInfo();
    }

    public void SetToolLocked(ToolKind tool, bool locked)
    {
        if (tool == ToolKind.Cursor)
        {
            return;
        }

        if (locked)
        {
            _lockedTools.Add(tool);
        }
        else
        {
            _lockedTools.Remove(tool);
        }

        RefreshToolChrome();
        RefreshBlockInfo();
    }

    public void SetToolsLocked(IEnumerable<(ToolKind Tool, bool Locked)> states)
    {
        foreach (var (tool, locked) in states)
        {
            if (tool == ToolKind.Cursor)
            {
                continue;
            }

            if (locked)
            {
                _lockedTools.Add(tool);
            }
            else
            {
                _lockedTools.Remove(tool);
            }
        }

        RefreshToolChrome();
        RefreshBlockInfo();
    }

    public bool IsToolLocked(ToolKind tool) => _lockedTools.Contains(tool);

    private void RefreshToolChrome()
    {
        foreach (var (kind, slot) in _toolSlots)
        {
            ApplySlotChrome(slot, kind == _selected, _lockedTools.Contains(kind));
        }

        foreach (var (cat, slot) in _categorySlots)
        {
            ApplySlotChrome(slot, cat == _category, locked: false, selectedBorder: SlotSelected);
        }
    }

    private static void ApplySlotChrome(
        PanelContainer slot,
        bool selected,
        bool locked,
        Color? selectedBorder = null)
    {
        var accent = selectedBorder ?? SlotSelected;
        var style = (StyleBoxFlat)slot.GetThemeStylebox("panel").Duplicate();
        style.BorderColor = selected
            ? accent
            : locked
                ? new Color(0.35f, 0.28f, 0.28f, 1f)
                : SlotIdle;
        var bw = selected ? 3 : 1;
        style.BorderWidthLeft = bw;
        style.BorderWidthTop = bw;
        style.BorderWidthRight = bw;
        style.BorderWidthBottom = bw;
        style.BgColor = selected
            ? new Color(0.22f, 0.22f, 0.16f, 1f)
            : locked
                ? new Color(0.10f, 0.10f, 0.10f, 1f)
                : SlotBg;
        slot.AddThemeStyleboxOverride("panel", style);
        slot.Modulate = locked ? new Color(0.55f, 0.55f, 0.55f, 1f) : Colors.White;
    }

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
        int generatorsTotal = 0)
    {
        SetStock("iron-ore", oreName, ore);
        SetStock("iron-plate", plateName, plate);
        SetStock("copper-ore", copperName, copper);
        SetStock("copper-wire", wireName, wire);
        _money = money;
        if (_moneyLabel is not null)
        {
            _moneyLabel.Text = $"Magazzino  ${money}  ·  M Mercato";
        }

        if (_titleLabel is not null)
        {
            var power = generatorsTotal == 0
                ? "potenza —"
                : generatorsLive > 0
                    ? $"potenza ON ({generatorsLive}/{generatorsTotal})"
                    : $"potenza off ({generatorsLive}/{generatorsTotal})";
            _titleLabel.Text = $"Core · consegnati {delivered} · nastro {onBelt} · {power}";
        }

        RefreshBlockInfo();
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

    private void SetStock(string id, string name, int count)
    {
        _stockCounts[id] = count;
        if (_stockLabels.TryGetValue(id, out var label))
        {
            label.Text = $"{name}\n{count}";
        }
    }

    private void BuildStockPanel()
    {
        var panel = MakePanel("StockPanel");
        panel.SetAnchorsPreset(LayoutPreset.TopRight);
        panel.GrowHorizontal = GrowDirection.Begin;
        panel.OffsetLeft = -320;
        panel.OffsetTop = 12;
        panel.OffsetRight = -12;
        panel.OffsetBottom = 168;
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vbox);

        _titleLabel = new Label
        {
            Text = "Core",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _titleLabel.AddThemeColorOverride("font_color", TextMuted);
        _titleLabel.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(_titleLabel);

        _moneyLabel = new Label
        {
            Text = "Magazzino  $0  ·  M Mercato"
        };
        _moneyLabel.AddThemeColorOverride("font_color", SlotSelected);
        _moneyLabel.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(_moneyLabel);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        vbox.AddChild(row);

        AddStockSlot(row, "iron-ore", "res://assets/iron-ore.png", "Ferro grezzo");
        AddStockSlot(row, "iron-plate", "res://assets/iron-plate.png", "Lastra ferro");
        AddStockSlot(row, "copper-ore", "res://assets/copper-ore.png", "Rame grezzo");
        AddStockSlot(row, "copper-wire", "res://assets/copper-wire.png", "Filo rame");
    }

    private void AddStockSlot(Control parent, string id, string texPath, string fallbackName)
    {
        var box = new VBoxContainer();
        box.CustomMinimumSize = new Vector2(68, 0);
        box.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        parent.AddChild(box);

        var iconWrap = new PanelContainer();
        iconWrap.CustomMinimumSize = new Vector2(48, 48);
        iconWrap.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        iconWrap.AddThemeStyleboxOverride("panel", MakeSlotStyle(SlotIdle, 1));
        box.AddChild(iconWrap);

        var icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(40, 40)
        };
        if (ResourceLoader.Exists(texPath))
        {
            icon.Texture = GD.Load<Texture2D>(texPath);
        }

        iconWrap.AddChild(icon);

        var label = new Label
        {
            Text = $"{fallbackName}\n0",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        label.AddThemeColorOverride("font_color", TextPrimary);
        label.AddThemeFontSizeOverride("font_size", 11);
        box.AddChild(label);
        _stockLabels[id] = label;
    }

    private void BuildObjectivesPanel()
    {
        _objectivesPanel = MakePanel("ObjectivesPanel");
        _objectivesPanel.SetAnchorsPreset(LayoutPreset.TopLeft);
        _objectivesPanel.OffsetLeft = 12;
        _objectivesPanel.OffsetTop = 12;
        _objectivesPanel.OffsetRight = 360;
        _objectivesPanel.OffsetBottom = 140;
        _objectivesPanel.Visible = false;
        AddChild(_objectivesPanel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        _objectivesPanel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        margin.AddChild(vbox);

        _objectivesTitle = new Label { Text = "OBIETTIVO" };
        _objectivesTitle.AddThemeColorOverride("font_color", SlotSelected);
        _objectivesTitle.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(_objectivesTitle);

        _objectivesList = new VBoxContainer();
        _objectivesList.AddThemeConstantOverride("separation", 2);
        vbox.AddChild(_objectivesList);
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
        EconomyWallet wallet,
        EconomySession session,
        ResearchState research,
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

        var lines = Math.Max(1, objectives.Count);
        _objectivesPanel.OffsetBottom = 12 + 36 + lines * 20;
    }

    private void BuildCornerDock()
    {
        // Bottom-right stack: [reserved info slot] then [build palette].
        // Slot keeps fixed height when empty so the palette never jumps.
        _cornerStack = new VBoxContainer
        {
            Name = "CornerDockStack",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _cornerStack.SetAnchorsPreset(LayoutPreset.BottomRight);
        _cornerStack.GrowHorizontal = GrowDirection.Begin;
        _cornerStack.GrowVertical = GrowDirection.Begin;
        _cornerStack.OffsetLeft = -CornerStackWidth - 8;
        _cornerStack.OffsetRight = -8;
        _cornerStack.OffsetBottom = -8;
        _cornerStack.OffsetTop = -8; // grows upward via min sizes
        _cornerStack.AddThemeConstantOverride("separation", 8);
        AddChild(_cornerStack);

        _infoSlot = new Control
        {
            Name = "InfoSlot",
            CustomMinimumSize = new Vector2(CornerStackWidth, InfoSlotHeight),
            SizeFlagsHorizontal = SizeFlags.Fill,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _cornerStack.AddChild(_infoSlot);

        _infoPanel = MakePanel("BlockInfo");
        _infoPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _infoPanel.Visible = false;
        _infoPanel.MouseFilter = MouseFilterEnum.Ignore;
        _infoSlot.AddChild(_infoPanel);

        var infoMargin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        infoMargin.AddThemeConstantOverride("margin_left", 8);
        infoMargin.AddThemeConstantOverride("margin_right", 8);
        infoMargin.AddThemeConstantOverride("margin_top", 6);
        infoMargin.AddThemeConstantOverride("margin_bottom", 6);
        _infoPanel.AddChild(infoMargin);

        var infoRoot = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        infoRoot.AddThemeConstantOverride("separation", 4);
        infoMargin.AddChild(infoRoot);

        var header = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        header.AddThemeConstantOverride("separation", 6);
        infoRoot.AddChild(header);

        _infoName = new Label
        {
            Text = "",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center
        };
        _infoName.AddThemeColorOverride("font_color", TextPrimary);
        _infoName.AddThemeFontSizeOverride("font_size", 14);
        header.AddChild(_infoName);

        var infoBtn = new PanelContainer
        {
            CustomMinimumSize = new Vector2(26, 26),
            MouseFilter = MouseFilterEnum.Stop,
            TooltipText = "Dettaglio blocco"
        };
        infoBtn.AddThemeStyleboxOverride("panel", MakeSlotStyle(SlotIdle, 1));
        header.AddChild(infoBtn);
        var infoCenter = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        infoBtn.AddChild(infoCenter);
        var q = new Label
        {
            Text = "?",
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        q.AddThemeColorOverride("font_color", SlotSelected);
        q.AddThemeFontSizeOverride("font_size", 13);
        infoCenter.AddChild(q);
        infoBtn.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                OpenDetailOverlay();
                AcceptEvent();
            }
        };

        _infoCostRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _infoCostRow.AddThemeConstantOverride("separation", 4);
        infoRoot.AddChild(_infoCostRow);

        // Build palette dock — always below the reserved slot.
        _buildDock = MakePanel("BuildDock");
        _buildDock.CustomMinimumSize = new Vector2(CornerStackWidth, 0);
        _buildDock.SizeFlagsHorizontal = SizeFlags.Fill;
        _cornerStack.AddChild(_buildDock);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        _buildDock.AddChild(margin);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        margin.AddChild(col);

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 6);
        col.AddChild(top);

        var gridWrap = new VBoxContainer();
        gridWrap.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        gridWrap.AddThemeConstantOverride("separation", 4);
        top.AddChild(gridWrap);

        _categoryTitle = new Label { Text = "Logistica", MouseFilter = MouseFilterEnum.Ignore };
        _categoryTitle.AddThemeColorOverride("font_color", TextMuted);
        _categoryTitle.AddThemeFontSizeOverride("font_size", 11);
        gridWrap.AddChild(_categoryTitle);

        _blockGrid = new GridContainer
        {
            Columns = 4,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _blockGrid.AddThemeConstantOverride("h_separation", 4);
        _blockGrid.AddThemeConstantOverride("v_separation", 4);
        gridWrap.AddChild(_blockGrid);

        var catRail = new VBoxContainer();
        catRail.AddThemeConstantOverride("separation", 4);
        top.AddChild(catRail);
        AddCategoryButton(catRail, BuildCategory.Tools, "St", CatTools, "Strumenti");
        AddCategoryButton(catRail, BuildCategory.Logistics, "Lo", CatLogistics, "Logistica");
        AddCategoryButton(catRail, BuildCategory.Production, "Pr", CatProduction, "Produzione");
        AddCategoryButton(catRail, BuildCategory.Power, "Po", CatPower, "Potenza");

        var util = new HBoxContainer();
        util.AddThemeConstantOverride("separation", 4);
        col.AddChild(util);
        AddUtilityIcon(util, "res://assets/research.png", null, "Ricerca", "T",
            () => ResearchRequested?.Invoke());
        AddUtilityIcon(util, null, "$", "Mercato", "M",
            () => MercatoRequested?.Invoke());
        AddUtilityIcon(util, null, "▣", "Campagna", "G",
            () => CampaignRequested?.Invoke());
        AddUtilityIcon(util, null, "⇩", "Salva continua", "F5",
            () => SaveRequested?.Invoke());
        AddUtilityIcon(util, null, "⇧", "Carica continua", "F9",
            () => LoadRequested?.Invoke());
        AddUtilityIcon(util, null, "▤", "Salva slot", "F6",
            () => SaveSlotRequested?.Invoke());
        AddUtilityIcon(util, null, "▥", "Carica slot", "F7",
            () => LoadSlotRequested?.Invoke());
    }

    private void SetInfoSlotFilled(bool filled)
    {
        if (_infoPanel is null)
        {
            return;
        }

        _infoPanel.Visible = filled;
        _infoPanel.MouseFilter = filled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
    }

    private void AddCategoryButton(
        Control parent,
        BuildCategory category,
        string glyph,
        Color tint,
        string tooltip)
    {
        var slot = new PanelContainer
        {
            CustomMinimumSize = new Vector2(40, 40),
            MouseFilter = MouseFilterEnum.Stop,
            TooltipText = tooltip
        };
        slot.AddThemeStyleboxOverride("panel", MakeSlotStyle(SlotIdle, 1));
        parent.AddChild(slot);
        _categorySlots[category] = slot;

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        slot.AddChild(center);
        var label = new Label
        {
            Text = glyph,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeColorOverride("font_color", tint);
        label.AddThemeFontSizeOverride("font_size", 13);
        center.AddChild(label);

        slot.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                ShowCategory(category);
                AcceptEvent();
            }
        };
    }

    private void AddUtilityIcon(
        Control parent,
        string? texPath,
        string? glyph,
        string tip,
        string hotkeyHint,
        Action onClick)
    {
        var slot = new PanelContainer
        {
            CustomMinimumSize = new Vector2(34, 30),
            MouseFilter = MouseFilterEnum.Stop,
            TooltipText = $"{tip} ({hotkeyHint})"
        };
        slot.AddThemeStyleboxOverride("panel", MakeSlotStyle(SlotIdle, 1));
        parent.AddChild(slot);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        slot.AddChild(center);

        if (!string.IsNullOrEmpty(texPath) && ResourceLoader.Exists(texPath))
        {
            var icon = new TextureRect
            {
                Texture = GD.Load<Texture2D>(texPath),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(20, 20),
                MouseFilter = MouseFilterEnum.Ignore
            };
            center.AddChild(icon);
        }
        else
        {
            var text = new Label
            {
                Text = glyph ?? "·",
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            text.AddThemeColorOverride("font_color", TextPrimary);
            text.AddThemeFontSizeOverride("font_size", 14);
            center.AddChild(text);
        }

        slot.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                onClick();
                AcceptEvent();
            }
        };
    }

    private void ShowCategory(BuildCategory category)
    {
        _category = category;
        if (_categoryTitle is not null)
        {
            _categoryTitle.Text = CategoryLabel(category);
        }

        RebuildGrid();
        RefreshToolChrome();
    }

    private void RebuildGrid()
    {
        if (_blockGrid is null)
        {
            return;
        }

        foreach (var child in _blockGrid.GetChildren())
        {
            child.QueueFree();
        }

        _gridSlots.Clear();
        // Drop stale tool slot refs for place tools; cursor stays if present.
        var keep = new HashSet<ToolKind> { ToolKind.Cursor };
        foreach (var kind in _toolSlots.Keys.ToList())
        {
            if (!keep.Contains(kind))
            {
                _toolSlots.Remove(kind);
            }
        }

        foreach (var entry in EntriesFor(_category))
        {
            AddPaletteCell(_blockGrid, entry);
        }
    }

    private void AddPaletteCell(Control parent, PaletteEntry entry)
    {
        var slot = new PanelContainer
        {
            CustomMinimumSize = new Vector2(52, 52),
            MouseFilter = MouseFilterEnum.Stop,
            TooltipText = entry.Tool == ToolKind.Cursor
                ? "Cursore"
                : $"{HotDisplayName(entry)} · {entry.Hotkey}"
        };
        slot.AddThemeStyleboxOverride("panel", MakeSlotStyle(SlotIdle, 1));
        parent.AddChild(slot);
        _toolSlots[entry.Tool] = slot;
        _gridSlots.Add(slot);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        slot.AddChild(center);

        if (entry.Tool == ToolKind.Cursor)
        {
            var glyph = new Label
            {
                Text = "↖",
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            glyph.AddThemeColorOverride("font_color", TextPrimary);
            glyph.AddThemeFontSizeOverride("font_size", 26);
            center.AddChild(glyph);
        }
        else
        {
            var icon = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(40, 40),
                MouseFilter = MouseFilterEnum.Ignore
            };
            if (ResourceLoader.Exists(entry.TexPath))
            {
                icon.Texture = GD.Load<Texture2D>(entry.TexPath);
            }

            center.AddChild(icon);
        }

        // Tiny hotkey badge — top-left, not under-icon text.
        var badge = new Label
        {
            Text = entry.Hotkey,
            MouseFilter = MouseFilterEnum.Ignore
        };
        badge.AddThemeColorOverride("font_color", new Color(SlotSelected.R, SlotSelected.G, SlotSelected.B, 0.85f));
        badge.AddThemeFontSizeOverride("font_size", 9);
        badge.SetAnchorsPreset(LayoutPreset.TopLeft);
        badge.OffsetLeft = 3;
        badge.OffsetTop = 1;
        slot.AddChild(badge);

        slot.MouseEntered += () =>
        {
            _hovered = entry.Tool;
            RefreshBlockInfo();
        };
        slot.MouseExited += () =>
        {
            if (_hovered == entry.Tool)
            {
                _hovered = null;
                RefreshBlockInfo();
            }
        };

        slot.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                OnPaletteClicked(entry);
                AcceptEvent();
            }
        };

        ApplySlotChrome(slot, entry.Tool == _selected, _lockedTools.Contains(entry.Tool));
    }

    private void OnPaletteClicked(PaletteEntry entry)
    {
        if (entry.Tool == ToolKind.Cursor)
        {
            ClearToolSelection(toast: true);
            return;
        }

        if (_lockedTools.Contains(entry.Tool))
        {
            ShowToast($"{HotDisplayName(entry)} bloccato: sbloccalo in Ricerca (T).");
            return;
        }

        if (_selected == entry.Tool)
        {
            ClearToolSelection(toast: true);
            return;
        }

        SetSelectedTool(entry.Tool);
        ToolChosen?.Invoke(entry.Tool);
        ShowToast($"{HotDisplayName(entry)} selezionato");
    }

    private string HotDisplayName(PaletteEntry entry)
    {
        if (_content is not null && !string.IsNullOrEmpty(entry.StructureId))
        {
            var s = _content.FindStructure(entry.StructureId);
            if (s is not null)
            {
                return s.DisplayName;
            }
        }

        return ToolDisplayFallback(entry.Tool);
    }

    private void RefreshBlockInfo()
    {
        if (_infoPanel is null || _infoName is null || _infoCostRow is null)
        {
            return;
        }

        var focus = _hovered ?? (_selected == ToolKind.Cursor ? null : _selected);
        if (focus is null || focus == ToolKind.Cursor)
        {
            if (_selected != ToolKind.Cursor)
            {
                focus = _selected;
            }
            else
            {
                SetInfoSlotFilled(false);
                return;
            }
        }

        var entry = FindEntry(focus.Value);
        if (entry is null)
        {
            SetInfoSlotFilled(false);
            return;
        }

        SetInfoSlotFilled(true);
        var name = HotDisplayName(entry);
        var locked = _lockedTools.Contains(entry.Tool);
        _infoName.Text = locked ? $"{name} · bloccato" : name;

        foreach (var child in _infoCostRow.GetChildren())
        {
            child.QueueFree();
        }

        ResolveCost(entry.StructureId, out var money, out var materials);
        if (money <= 0 && materials.Count == 0)
        {
            var free = new Label
            {
                Text = "—",
                MouseFilter = MouseFilterEnum.Ignore
            };
            free.AddThemeColorOverride("font_color", TextMuted);
            free.AddThemeFontSizeOverride("font_size", 12);
            _infoCostRow.AddChild(free);
            return;
        }

        foreach (var mat in materials.Where(m => m.Amount > 0))
        {
            var have = _wallet?.MaterialCount(mat.ItemId)
                ?? (_stockCounts.TryGetValue(mat.ItemId, out var c) ? c : 0);
            var ok = have >= mat.Amount;
            _infoCostRow.AddChild(MakeCostChip(mat.ItemId, mat.Amount, ok));
        }

        if (money > 0)
        {
            var cashOk = (_wallet?.Money ?? _money) >= money;
            var cash = new Label
            {
                Text = $"${money}",
                MouseFilter = MouseFilterEnum.Ignore
            };
            cash.AddThemeColorOverride("font_color", cashOk ? TextPrimary : Insufficient);
            cash.AddThemeFontSizeOverride("font_size", 12);
            _infoCostRow.AddChild(cash);
        }
    }

    private Control MakeCostChip(string itemId, int amount, bool ok)
    {
        var wrap = new PanelContainer
        {
            CustomMinimumSize = new Vector2(44, 28),
            MouseFilter = MouseFilterEnum.Ignore,
            TooltipText = _content?.DisplayName(itemId) ?? itemId
        };
        wrap.AddThemeStyleboxOverride("panel", MakeSlotStyle(ok ? SlotIdle : Insufficient, 1));

        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 2);
        wrap.AddChild(row);

        var iconHost = new Control
        {
            CustomMinimumSize = new Vector2(22, 22),
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddChild(iconHost);

        var texPath = $"res://assets/{itemId}.png";
        var icon = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(20, 20),
            Position = new Vector2(1, 1),
            MouseFilter = MouseFilterEnum.Ignore
        };
        if (ResourceLoader.Exists(texPath))
        {
            icon.Texture = GD.Load<Texture2D>(texPath);
        }

        iconHost.AddChild(icon);

        if (!ok)
        {
            // Mindustry-style red slash over missing material.
            var slash = new ColorRect
            {
                Color = Insufficient,
                MouseFilter = MouseFilterEnum.Ignore,
                Rotation = Mathf.DegToRad(-38f),
                PivotOffset = new Vector2(11, 1)
            };
            slash.Position = new Vector2(0, 10);
            slash.Size = new Vector2(24, 3);
            iconHost.AddChild(slash);

            var slash2 = new ColorRect
            {
                Color = new Color(0.15f, 0.05f, 0.05f, 0.85f),
                MouseFilter = MouseFilterEnum.Ignore,
                Rotation = Mathf.DegToRad(-38f),
                PivotOffset = new Vector2(11, 1)
            };
            slash2.Position = new Vector2(0, 12);
            slash2.Size = new Vector2(24, 2);
            iconHost.AddChild(slash2);
        }

        var qty = new Label
        {
            Text = $"×{amount}",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        qty.AddThemeColorOverride("font_color", ok ? TextPrimary : Insufficient);
        qty.AddThemeFontSizeOverride("font_size", 11);
        row.AddChild(qty);

        return wrap;
    }

    private void ResolveCost(string structureId, out int money, out IReadOnlyList<ResourceAmount> materials)
    {
        money = 0;
        materials = [];
        if (_content is null || string.IsNullOrEmpty(structureId))
        {
            return;
        }

        var building = _content.FindBuilding(structureId);
        if (building is not null)
        {
            money = building.MoneyCost;
            materials = building.BuildCost;
            return;
        }

        var conveyor = _content.FindConveyor(structureId);
        if (conveyor is not null)
        {
            money = conveyor.MoneyCost;
            materials = conveyor.EffectiveBuildCost;
        }
    }

    private string ResolveIoLine(string structureId)
    {
        if (_content is null || string.IsNullOrEmpty(structureId))
        {
            return "";
        }

        var recipeId = structureId switch
        {
            "smelter" => "smelt-iron",
            "assembler" => "craft-copper-wire",
            "generator" => null,
            _ => null
        };
        if (recipeId is null)
        {
            return structureId switch
            {
                "miner" => "I/O · estrae dal deposito sotto",
                "generator" => "I/O · carbone → potenza",
                "conveyor-basic" or "junction" or "splitter" or "sorter" or "conveyor-bridge"
                    => "I/O · trasporto item",
                _ => ""
            };
        }

        var recipe = _content.FindRecipe(recipeId);
        if (recipe is null)
        {
            return "";
        }

        string Fmt(IReadOnlyList<ResourceAmount> list) =>
            string.Join("+", list.Select(a =>
                $"{a.Amount}×{_content.DisplayName(a.ItemId)}"));

        return $"I/O · {Fmt(recipe.Inputs)} → {Fmt(recipe.Outputs)} ({recipe.DurationSeconds:0.#}s)";
    }

    private void BuildDetailOverlay()
    {
        _detailOverlay = MakePanel("BlockDetail");
        _detailOverlay.SetAnchorsPreset(LayoutPreset.Center);
        _detailOverlay.GrowHorizontal = GrowDirection.Both;
        _detailOverlay.GrowVertical = GrowDirection.Both;
        _detailOverlay.OffsetLeft = -220;
        _detailOverlay.OffsetRight = 220;
        _detailOverlay.OffsetTop = -140;
        _detailOverlay.OffsetBottom = 140;
        _detailOverlay.Visible = false;
        _detailOverlay.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_detailOverlay);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        _detailOverlay.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        margin.AddChild(vbox);

        var title = new Label { Text = "Dettaglio blocco" };
        title.AddThemeColorOverride("font_color", SlotSelected);
        title.AddThemeFontSizeOverride("font_size", 16);
        vbox.AddChild(title);

        _detailBody = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _detailBody.AddThemeColorOverride("font_color", TextPrimary);
        _detailBody.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(_detailBody);

        var close = new PanelContainer
        {
            CustomMinimumSize = new Vector2(100, 32),
            MouseFilter = MouseFilterEnum.Stop
        };
        close.AddThemeStyleboxOverride("panel", MakeSlotStyle(SlotIdle, 1));
        vbox.AddChild(close);
        var cc = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        close.AddChild(cc);
        var cl = new Label { Text = "Chiudi", MouseFilter = MouseFilterEnum.Ignore };
        cl.AddThemeColorOverride("font_color", TextPrimary);
        cc.AddChild(cl);
        close.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                _detailOverlay.Visible = false;
                AcceptEvent();
            }
        };
    }

    private void OpenDetailOverlay()
    {
        if (_detailOverlay is null || _detailBody is null)
        {
            return;
        }

        var focus = _hovered ?? (_selected == ToolKind.Cursor ? null : _selected);
        if (focus is null)
        {
            return;
        }

        var entry = FindEntry(focus.Value);
        if (entry is null)
        {
            return;
        }

        var name = HotDisplayName(entry);
        ResolveCost(entry.StructureId, out var money, out var materials);
        var costParts = materials
            .Where(m => m.Amount > 0)
            .Select(m =>
            {
                var have = _wallet?.MaterialCount(m.ItemId) ?? 0;
                var mark = have >= m.Amount ? "ok" : "manca";
                return $"{m.Amount}× {_content?.DisplayName(m.ItemId) ?? m.ItemId} ({mark})";
            });
        var costLine = string.Join(", ", costParts);
        if (money > 0)
        {
            costLine = string.IsNullOrEmpty(costLine) ? $"${money}" : $"{costLine}, ${money}";
        }

        if (string.IsNullOrEmpty(costLine))
        {
            costLine = "—";
        }

        _detailBody.Text =
            $"{name}\n\n{entry.HintIt}\n\n{ResolveIoLine(entry.StructureId)}\n\nCosto: {costLine}\n\n"
            + "Hotkey: " + entry.Hotkey
            + "\nR = ruota · destro = elimina · Esc = cursore";
        _detailOverlay.Visible = true;
    }

    /// <summary>Capture helper: open the ? detail modal for the current selection.</summary>
    public void OpenBlockDetailForCapture() => OpenDetailOverlay();

    /// <summary>Capture helper: close the ? detail modal.</summary>
    public void CloseBlockDetailForCapture()
    {
        if (_detailOverlay is not null)
        {
            _detailOverlay.Visible = false;
        }
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

    private static PanelContainer MakePanel(string name)
    {
        var panel = new PanelContainer
        {
            Name = name,
            MouseFilter = MouseFilterEnum.Stop
        };
        var style = new StyleBoxFlat
        {
            BgColor = PanelBg,
            BorderColor = PanelEdge,
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 0,
            ContentMarginRight = 0,
            ContentMarginTop = 0,
            ContentMarginBottom = 0
        };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static StyleBoxFlat MakeSlotStyle(Color border, int width) => new()
    {
        BgColor = SlotBg,
        BorderColor = border,
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = 3,
        CornerRadiusTopRight = 3,
        CornerRadiusBottomLeft = 3,
        CornerRadiusBottomRight = 3,
        ContentMarginLeft = 2,
        ContentMarginRight = 2,
        ContentMarginTop = 2,
        ContentMarginBottom = 2
    };

    private static PaletteEntry[] EntriesFor(BuildCategory category) => category switch
    {
        BuildCategory.Tools => ToolsEntries,
        BuildCategory.Logistics => LogisticsEntries,
        BuildCategory.Production => ProductionEntries,
        BuildCategory.Power => PowerEntries,
        _ => LogisticsEntries
    };

    private static string CategoryLabel(BuildCategory category) => category switch
    {
        BuildCategory.Tools => "Strumenti",
        BuildCategory.Logistics => "Logistica",
        BuildCategory.Production => "Produzione",
        BuildCategory.Power => "Potenza",
        _ => ""
    };

    private static BuildCategory CategoryFor(ToolKind tool) => tool switch
    {
        ToolKind.Cursor => BuildCategory.Tools,
        ToolKind.Belt or ToolKind.BeltFast or ToolKind.Junction or ToolKind.Splitter or ToolKind.Sorter or ToolKind.Bridge
            => BuildCategory.Logistics,
        ToolKind.Miner or ToolKind.MinerAdvanced or ToolKind.Smelter or ToolKind.Assembler or ToolKind.Extractor
            => BuildCategory.Production,
        ToolKind.Generator or ToolKind.PowerNode => BuildCategory.Power,
        _ => BuildCategory.Logistics
    };

    private static PaletteEntry? FindEntry(ToolKind tool)
    {
        foreach (var cat in new[]
                 {
                     BuildCategory.Tools, BuildCategory.Logistics,
                     BuildCategory.Production, BuildCategory.Power
                 })
        {
            foreach (var e in EntriesFor(cat))
            {
                if (e.Tool == tool)
                {
                    return e;
                }
            }
        }

        return null;
    }

    private static string ToolDisplayFallback(ToolKind tool) => tool switch
    {
        ToolKind.Cursor => "Cursore",
        ToolKind.Belt => "Nastro",
        ToolKind.BeltFast => "Nastro T2",
        ToolKind.Miner => "Minatore",
        ToolKind.MinerAdvanced => "Minatore T2",
        ToolKind.Smelter => "Forno",
        ToolKind.Assembler => "Assemblatore",
        ToolKind.Junction => "Giunzione",
        ToolKind.Splitter => "Splitter",
        ToolKind.Generator => "Generatore",
        ToolKind.Sorter => "Selezionatore",
        ToolKind.Bridge => "Ponte",
        ToolKind.Extractor => "Estrattore",
        ToolKind.PowerNode => "Nodo potenza",
        _ => tool.ToString()
    };

    private static string ToolHint(ToolKind tool) =>
        FindEntry(tool)?.HintIt ?? ToolDisplayFallback(tool);

    public static string StructureIdFor(ToolKind tool) => tool switch
    {
        ToolKind.Belt => "conveyor-basic",
        ToolKind.BeltFast => "conveyor-fast",
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
        _ => "conveyor-basic"
    };
}
