using Godot;
using TIndustry.Shared;
using static TIndustry.Godot.HudStyle;
using BuildCategory = TIndustry.Godot.Hud.BuildCategory;
using ToolKind = TIndustry.Godot.Hud.ToolKind;

namespace TIndustry.Godot;

/// <summary>
/// Bottom-right build palette: category rail + sprite grid. Owns the tool selection,
/// the locked tools, the shown category and the hovered cell.
/// </summary>
public partial class BuildPalette : PanelContainer
{
    internal sealed record PaletteEntry(
        ToolKind Tool,
        string StructureId,
        string Hotkey,
        string HintIt);

    private static readonly PaletteEntry[] ToolsEntries =
    [
        new(ToolKind.Cursor, "", "Esc", "Cursore: pan / guarda · Esc o riesci sul tool")
    ];

    private static readonly PaletteEntry[] LogisticsEntries =
    [
        new(ToolKind.Belt, "conveyor-basic", "1",
            "Nastro T1 · flusso unidirezionale · R/rotella"),
        new(ToolKind.BeltFast, "conveyor-fast", "",
            "Nastro T2 · più veloce · sblocca in Ricerca"),
        new(ToolKind.BeltExpress, "conveyor-express", "",
            "Nastro T3 · max velocità · sblocca in Ricerca"),
        new(ToolKind.Junction, "junction", "5",
            "Incrocio a croce"),
        new(ToolKind.Splitter, "splitter", "6",
            "Nastro a T · alterna sinistra/destra"),
        new(ToolKind.Sorter, "sorter", "8",
            "Filtro item · icona al centro · C cicla · match avanti"),
        new(ToolKind.Bridge, "conveyor-bridge", "9",
            "Ponte span 2–5 · I/O 3 lati · teleporto istantaneo")
    ];

    private static readonly PaletteEntry[] ProductionEntries =
    [
        new(ToolKind.Miner, "miner", "2",
            "Estrae minerali · uscita su tutti i lati · 2×2"),
        new(ToolKind.MinerAdvanced, "miner-advanced", "",
            "Minatore T2 · 2× velocità · +20% se alimentato · sblocca in Ricerca"),
        new(ToolKind.Smelter, "smelter", "3",
            "Carbone o corrente · +20% craft se alimentato · 2×2"),
        new(ToolKind.Assembler, "assembler", "4",
            "Assembla prodotti · R ruota uscita · 2×2"),
        new(ToolKind.Extractor, "extractor", "",
            "Estrae dal Core → nastro · F filtro · R uscita · 1×1")
    ];

    private static readonly PaletteEntry[] PowerEntries =
    [
        new(ToolKind.Generator, "generator", "7",
            "Brucia carbone per energia · 2×2"),
        new(ToolKind.PowerNode, "power-node", "",
            "Nodo T1 · raggio 6 · collega generatore ↔ forno"),
        new(ToolKind.PowerNodeT2, "power-node-t2", "",
            "Nodo T2 · 2×2 · raggio 10 · sblocca in Ricerca")
    ];

    private readonly Dictionary<ToolKind, PanelContainer> _toolSlots = [];
    private readonly Dictionary<BuildCategory, PanelContainer> _categorySlots = [];
    private readonly HashSet<ToolKind> _lockedTools = [];

    private GridContainer? _blockGrid;
    private Label? _categoryTitle;
    private ToolKind _selected = ToolKind.Cursor;
    private BuildCategory _category = BuildCategory.Logistics;
    private ToolKind? _hovered;
    private string _sorterFilterId = "iron-ore";
    private TextureRect? _sorterFilterIcon;

    /// <summary>Selection changed from a palette click (cursor when deselecting).</summary>
    public event Action<ToolKind>? ToolChosen;

    /// <summary>Pointer entered a cell (tool) or left the hovered cell (null).</summary>
    public event Action<ToolKind?>? Hovered;

    /// <summary>Feedback text for the HUD toast after a palette click.</summary>
    public event Action<string>? ToastRequested;

    /// <summary>Content used for block names (tooltips, toasts).</summary>
    public FactoryContent? Content { get; set; }

    public ToolKind Selected => _selected;
    public ToolKind? HoveredTool => _hovered;

    public BuildPalette()
    {
        StylePanel(this, "BuildDock");
        CustomMinimumSize = new Vector2(Hud.CornerStackWidth, 0);
        SizeFlagsHorizontal = SizeFlags.Fill;
    }

    public override void _Ready()
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        AddChild(margin);

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

    /// <summary>Select a tool (no event); switches to its category unless it is the cursor.</summary>
    public void Select(ToolKind tool)
    {
        _selected = tool;
        var cat = CategoryFor(tool);
        if (cat != _category && tool != ToolKind.Cursor)
        {
            ShowCategory(cat);
        }

        RefreshToolChrome();
    }

    public void SetLocked(IEnumerable<(ToolKind Tool, bool Locked)> states)
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
    }

    public bool IsLocked(ToolKind tool) => _lockedTools.Contains(tool);

    /// <summary>Brush filter shown on the sorter palette glyph (and used when placing).</summary>
    public void SetSorterFilterBrush(string itemId)
    {
        _sorterFilterId = string.IsNullOrWhiteSpace(itemId) ? "iron-ore" : itemId;
        if (_sorterFilterIcon is not null)
        {
            ApplySorterFilterTexture(_sorterFilterIcon);
        }
    }

    internal void ShowCategory(BuildCategory category)
    {
        _category = category;
        if (_categoryTitle is not null)
        {
            _categoryTitle.Text = CategoryLabel(category);
        }

        RebuildGrid();
        RefreshToolChrome();
    }

    /// <summary>Block name from content, falling back to the Italian tool name.</summary>
    internal static string DisplayName(PaletteEntry entry, FactoryContent? content)
    {
        if (content is not null && !string.IsNullOrEmpty(entry.StructureId))
        {
            var s = content.FindStructure(entry.StructureId);
            if (s is not null)
            {
                return s.DisplayName;
            }
        }

        return ToolDisplayFallback(entry.Tool);
    }

    internal static PaletteEntry? FindEntry(ToolKind tool)
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

    private string DisplayName(PaletteEntry entry) => DisplayName(entry, Content);

    private void ApplySorterFilterTexture(TextureRect target)
    {
        target.Texture = ItemTexture(_sorterFilterId);
    }

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
                : string.IsNullOrEmpty(entry.Hotkey) ? DisplayName(entry) : $"{DisplayName(entry)} [{entry.Hotkey}]"
        };
        slot.AddThemeStyleboxOverride("panel", MakeSlotStyle(SlotIdle, 1));
        parent.AddChild(slot);
        _toolSlots[entry.Tool] = slot;

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
            frame.Texture = Sprites.TryFor(entry.StructureId);
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
            icon.Texture = Sprites.TryFor(entry.StructureId);
            center.AddChild(icon);
        }

        slot.MouseEntered += () =>
        {
            _hovered = entry.Tool;
            Hovered?.Invoke(_hovered);
        };
        slot.MouseExited += () =>
        {
            if (_hovered == entry.Tool)
            {
                _hovered = null;
                Hovered?.Invoke(null);
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
            Choose(ToolKind.Cursor, "Cursore");
            return;
        }

        if (_lockedTools.Contains(entry.Tool))
        {
            ToastRequested?.Invoke($"{DisplayName(entry)} bloccato: sbloccalo in Ricerca (T).");
            return;
        }

        if (_selected == entry.Tool)
        {
            Choose(ToolKind.Cursor, "Cursore");
            return;
        }

        Choose(entry.Tool, $"{DisplayName(entry)} selezionato");
    }

    private void Choose(ToolKind tool, string toast)
    {
        Select(tool);
        ToolChosen?.Invoke(tool);
        ToastRequested?.Invoke(toast);
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
}
