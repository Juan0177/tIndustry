using Godot;
using TIndustry.Shared;
using static TIndustry.Godot.HudStyle;

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

    private static readonly PaletteEntry[] ToolsEntries =
    [
        new(ToolKind.Cursor, "", "", "Esc", "Cursore: pan / guarda · Esc o riesci sul tool")
    ];

    private static readonly PaletteEntry[] LogisticsEntries =
    [
        new(ToolKind.Belt, "conveyor-basic", "res://assets/conveyor-basic.png", "1",
            "Nastro T1 · flusso unidirezionale · R/rotella"),
        new(ToolKind.BeltFast, "conveyor-fast", "res://assets/conveyor-fast.png", "",
            "Nastro T2 · più veloce · sblocca in Ricerca"),
        new(ToolKind.BeltExpress, "conveyor-express", "res://assets/conveyor-fast.png", "",
            "Nastro T3 · max velocità · sblocca in Ricerca"),
        new(ToolKind.Junction, "junction", "res://assets/junction.png", "5",
            "Incrocio a croce"),
        new(ToolKind.Splitter, "splitter", "res://assets/splitter.png", "6",
            "Nastro a T · alterna sinistra/destra"),
        new(ToolKind.Sorter, "sorter", "res://assets/sorter.png", "8",
            "Filtro item · icona al centro · C cicla · match avanti"),
        new(ToolKind.Bridge, "conveyor-bridge", "res://assets/bridge.png", "9",
            "Ponte span 2–5 · I/O 3 lati · teleporto istantaneo")
    ];

    private static readonly PaletteEntry[] ProductionEntries =
    [
        new(ToolKind.Miner, "miner", "res://assets/miner.png", "2",
            "Estrae minerali · uscita su tutti i lati · 2×2"),
        new(ToolKind.MinerAdvanced, "miner-advanced", "res://assets/miner-advanced.png", "",
            "Minatore T2 · 2× velocità · +20% se alimentato · sblocca in Ricerca"),
        new(ToolKind.Smelter, "smelter", "res://assets/smelter.png", "3",
            "Carbone o corrente · +20% craft se alimentato · 2×2"),
        new(ToolKind.Assembler, "assembler", "res://assets/assembler.png", "4",
            "Assembla prodotti · R ruota uscita · 2×2"),
        new(ToolKind.Extractor, "extractor", "res://assets/extractor.png", "",
            "Estrae dal Core → nastro · F filtro · R uscita · 1×1")
    ];

    private static readonly PaletteEntry[] PowerEntries =
    [
        new(ToolKind.Generator, "generator", "res://assets/generator.png", "7",
            "Brucia carbone per energia · 2×2"),
        new(ToolKind.PowerNode, "power-node", "res://assets/power-node.png", "",
            "Nodo T1 · raggio 6 · collega generatore ↔ forno"),
        new(ToolKind.PowerNodeT2, "power-node-t2", "res://assets/power-node-t2.png", "",
            "Nodo T2 · 2×2 · raggio 10 · sblocca in Ricerca")
    ];

    private readonly Dictionary<ToolKind, PanelContainer> _toolSlots = [];
    private readonly HashSet<ToolKind> _lockedTools = [];
    private readonly Dictionary<BuildCategory, PanelContainer> _categorySlots = [];
    private readonly List<PanelContainer> _gridSlots = [];

    private Label? _dirLabel;
    private Label? _toastLabel;
    private Label? _infoHotkey;
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
    private string _sorterFilterId = "iron-ore";
    private TextureRect? _sorterFilterIcon;
    private EconomyWallet? _wallet;
    private readonly CorePanel _core = new();

    /// <summary>Reserved height above the palette for the hover/select strip (empty = invisible).</summary>
    private const float InfoSlotHeight = 78f;
    private const float CornerStackWidth = 292f;

    public event Action<ToolKind>? ToolChosen;
    public event Action? ResearchRequested;
    public event Action? MercatoRequested;
    public event Action? CampaignRequested;
    public event Action? CoreUpgradeRequested;
    public event Action? MenuRequested;

    public ToolKind SelectedTool => _selected;
    public bool IsCursorMode => _selected == ToolKind.Cursor;

    public FactoryHud()
    {
        _core.MenuRequested += () => MenuRequested?.Invoke();
        _core.ResearchRequested += () => ResearchRequested?.Invoke();
        _core.MercatoRequested += () => MercatoRequested?.Invoke();
        _core.CampaignRequested += () => CampaignRequested?.Invoke();
        _core.CoreUpgradeRequested += () => CoreUpgradeRequested?.Invoke();
    }

    public override void _Ready()
    {
        Name = "FactoryHudRoot";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        AddChild(_core);
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
        _core.Bind(content, wallet);
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

    /// <summary>Brush filter shown on the sorter palette glyph (and used when placing).</summary>
    public void SetSorterFilterBrush(string itemId)
    {
        _sorterFilterId = string.IsNullOrWhiteSpace(itemId) ? "iron-ore" : itemId;
        if (_sorterFilterIcon is not null)
        {
            ApplySorterFilterTexture(_sorterFilterIcon);
        }
    }

    private void ApplySorterFilterTexture(TextureRect target)
    {
        var path = $"res://assets/{_sorterFilterId}.png";
        target.Texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        ItemPalette.Tint(target, _sorterFilterId);
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
        int generatorsTotal = 0,
        float powerBuffer = -1f,
        float powerCapacity = -1f)
    {
        _core.UpdateStock(oreName, ore, plateName, plate, copperName, copper, wireName, wire,
            delivered, onBelt, money, generatorsLive, generatorsTotal, powerBuffer, powerCapacity);

        RefreshBlockInfo();
    }

    /// <summary>CORE upgrade chip: cost while available, LV+bonus when done.</summary>
    public void SyncCoreUpgrade(Factory? slice) => _core.SyncCoreUpgrade(slice);

    public void ClearObjectives() => _core.ClearObjectives();

    public void UpdateObjectives(
        CampaignLevelDefinition level,
        EconomyWallet wallet,
        EconomySession session,
        ResearchState research,
        Func<string, string>? itemName = null) =>
        _core.UpdateObjectives(level, wallet, session, research, itemName);

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

        _infoHotkey = new Label { MouseFilter = MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center };
        _infoHotkey.AddThemeColorOverride("font_color", TextMuted);
        _infoHotkey.AddThemeFontSizeOverride("font_size", 12);
        header.AddChild(_infoHotkey);

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
        AddCategoryButton(catRail, BuildCategory.Tools, "res://assets/ui/cat-tools.png", CatTools, "Strumenti");
        AddCategoryButton(catRail, BuildCategory.Logistics, "res://assets/ui/cat-logistics.png", CatLogistics, "Logistica");
        AddCategoryButton(catRail, BuildCategory.Production, "res://assets/ui/cat-production.png", CatProduction, "Produzione");
        AddCategoryButton(catRail, BuildCategory.Power, "res://assets/ui/cat-power.png", CatPower, "Potenza");

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
        string iconPath,
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
        center.AddChild(MakeIcon(
            ResourceLoader.Exists(iconPath) ? GD.Load<Texture2D>(iconPath) : null, 24, tint));

        slot.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                ShowCategory(category);
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
        _sorterFilterIcon = null;
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
                : string.IsNullOrEmpty(entry.Hotkey) ? HotDisplayName(entry) : $"{HotDisplayName(entry)} [{entry.Hotkey}]"
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
        else if (entry.Tool == ToolKind.Sorter)
        {
            // Frame + dynamic filter glyph (~70% of slot).
            var stack = new Control
            {
                CustomMinimumSize = new Vector2(40, 40),
                MouseFilter = MouseFilterEnum.Ignore
            };
            var frame = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(40, 40),
                MouseFilter = MouseFilterEnum.Ignore
            };
            if (ResourceLoader.Exists(entry.TexPath))
            {
                frame.Texture = GD.Load<Texture2D>(entry.TexPath);
            }

            frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            stack.AddChild(frame);

            var filter = new TextureRect
            {
                Name = "SorterFilter",
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(28, 28),
                MouseFilter = MouseFilterEnum.Ignore
            };
            filter.SetAnchorsPreset(LayoutPreset.Center);
            filter.OffsetLeft = -14;
            filter.OffsetTop = -14;
            filter.OffsetRight = 14;
            filter.OffsetBottom = 14;
            ApplySorterFilterTexture(filter);
            stack.AddChild(filter);
            _sorterFilterIcon = filter;
            center.AddChild(stack);
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
        if (_infoHotkey is not null)
        {
            _infoHotkey.Text = string.IsNullOrEmpty(entry.Hotkey) ? "" : $"[{entry.Hotkey}]";
        }

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
                ?? _core.StockCount(mat.ItemId);
            var ok = have >= mat.Amount;
            _infoCostRow.AddChild(MakeCostChip(mat.ItemId, mat.Amount, ok, _content));
        }

        if (money > 0)
        {
            var cashOk = (_wallet?.Money ?? _core.Money) >= money;
            _infoCostRow.AddChild(MakeMoneyChip(money, cashOk));
        }
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
        ToolKind.Belt or ToolKind.BeltFast or ToolKind.BeltExpress or ToolKind.Junction or ToolKind.Splitter or ToolKind.Sorter or ToolKind.Bridge
            => BuildCategory.Logistics,
        ToolKind.Miner or ToolKind.MinerAdvanced or ToolKind.Smelter or ToolKind.Assembler or ToolKind.Extractor
            => BuildCategory.Production,
        ToolKind.Generator or ToolKind.PowerNode or ToolKind.PowerNodeT2 => BuildCategory.Power,
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
        ToolKind.BeltExpress => "Nastro T3",
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
        ToolKind.PowerNodeT2 => "Nodo T2",
        _ => tool.ToString()
    };

    private static string ToolHint(ToolKind tool) =>
        FindEntry(tool)?.HintIt ?? ToolDisplayFallback(tool);

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
}
