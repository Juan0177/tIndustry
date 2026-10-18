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
        string Hotkey);

    private static readonly PaletteEntry[] ToolsEntries =
    [
        new(ToolKind.Cursor, "", "Esc")
    ];

    private static readonly PaletteEntry[] LogisticsEntries =
    [
        new(ToolKind.Belt, "conveyor-basic", "1"),
        new(ToolKind.BeltFast, "conveyor-fast", ""),
        new(ToolKind.BeltExpress, "conveyor-express", ""),
        new(ToolKind.Junction, "junction", "5"),
        new(ToolKind.Splitter, "splitter", "6"),
        new(ToolKind.Sorter, "sorter", "8"),
        new(ToolKind.Bridge, "conveyor-bridge", "9")
    ];

    private static readonly PaletteEntry[] ProductionEntries =
    [
        new(ToolKind.Miner, "miner", "2"),
        new(ToolKind.MinerAdvanced, "miner-advanced", ""),
        new(ToolKind.Smelter, "smelter", "3"),
        new(ToolKind.Assembler, "assembler", "4"),
        new(ToolKind.Extractor, "extractor", "")
    ];

    private static readonly PaletteEntry[] PowerEntries =
    [
        new(ToolKind.Generator, "generator", "7"),
        new(ToolKind.PowerNode, "power-node", ""),
        new(ToolKind.PowerNodeT2, "power-node-t2", ""),
        new(ToolKind.Lamp, "lamp", "")
    ];

    private readonly Dictionary<ToolKind, PanelContainer> _toolSlots = [];
    private readonly Dictionary<BuildCategory, PanelContainer> _categorySlots = [];
    private readonly HashSet<ToolKind> _lockedTools = [];

    private GridContainer? _blockGrid;
    private VBoxContainer? _categoryRail;

    /// <summary>The grid is always this many columns (Mindustry), with as many rows as the largest category needs.</summary>
    private const int GridColumnCount = 4;

    private static readonly int GridRowCount =
        new[] { ToolsEntries, LogisticsEntries, ProductionEntries, PowerEntries }
            .Max(e => (e.Length + GridColumnCount - 1) / GridColumnCount);

    /// <summary>Side of a palette cell and of a category button, in UI units at 100%: the 32 px icon plus border.</summary>
    private const float CellSide = 40f;
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
        // Fixed size (GridColumns × GridRows cells, the whole category rail), right-aligned under the info slot.
        SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
    }

    public override void _Ready()
    {
        var top = new HBoxContainer { ThemeTypeVariation = UiTheme.RigaLarga };
        AddChild(top);

        var gridWrap = new VBoxContainer();
        gridWrap.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        top.AddChild(gridWrap);

        _categoryTitle = MakeLabel(CategoryLabel(_category), UiTheme.TestoTenue);
        gridWrap.AddChild(_categoryTitle);

        _blockGrid = new GridContainer
        {
            Columns = GridColumnCount,
            MouseFilter = MouseFilterEnum.Ignore
        };
        gridWrap.AddChild(_blockGrid);

        var catRail = new VBoxContainer();
        _categoryRail = catRail;
        top.AddChild(catRail);
        AddCategoryButton(catRail, BuildCategory.Tools, "cat-strumenti");
        AddCategoryButton(catRail, BuildCategory.Logistics, "cat-logistica");
        AddCategoryButton(catRail, BuildCategory.Production, "cat-produzione");
        AddCategoryButton(catRail, BuildCategory.Power, "cat-potenza");
        SyncCategoryButtons();
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

    /// <summary>
    /// Locked blocks (not yet researched) are left out of the palette, and a category with none unlocked loses its
    /// button; an unlock shows the block at once. The grid is rebuilt only when the locked set changes.
    /// </summary>
    public void SetLocked(IEnumerable<(ToolKind Tool, bool Locked)> states)
    {
        var changed = false;
        foreach (var (tool, locked) in states)
        {
            if (tool == ToolKind.Cursor)
            {
                continue;
            }

            changed |= locked ? _lockedTools.Add(tool) : _lockedTools.Remove(tool);
        }

        if (changed)
        {
            SyncCategoryButtons();
            RebuildGrid();
        }

        RefreshToolChrome();
    }

    /// <summary>
    /// A category button shows only while one of its blocks is unlocked (Strumenti always: the cursor). When the shown
    /// category loses its button (a new or loaded game with fewer unlocks), the palette switches to the first category
    /// still shown, so the grid never stays on a category that cannot be reached.
    /// </summary>
    private void SyncCategoryButtons()
    {
        foreach (var (category, slot) in _categorySlots)
        {
            slot.Visible = HasUnlocked(category);
        }

        if (!HasUnlocked(_category))
        {
            _category = new[] { BuildCategory.Logistics, BuildCategory.Production, BuildCategory.Power }
                .FirstOrDefault(HasUnlocked, BuildCategory.Tools);
            if (_categoryTitle is not null)
            {
                _categoryTitle.Text = CategoryLabel(_category);
            }
        }
    }

    internal bool HasUnlocked(BuildCategory category) => EntriesFor(category).Any(e => !_lockedTools.Contains(e.Tool));

    /// <summary>The category whose blocks the grid shows (playtests).</summary>
    internal BuildCategory ShownCategory => _category;

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

    /// <summary>Block name by structure id (the cursor has its own).</summary>
    internal static string DisplayName(PaletteEntry entry, FactoryContent? content) =>
        entry.Tool == ToolKind.Cursor ? Testi.T("strumento.cursore") : Testi.Struttura(entry.StructureId, content);

    /// <summary>
    /// One-line description of the block (key <c>struttura.&lt;id&gt;.suggerimento</c>); a miner's has the ores it digs,
    /// from the data (<see cref="Testi.Scava"/>).
    /// </summary>
    internal static string Hint(PaletteEntry entry, FactoryContent? content) => entry.Tool switch
    {
        ToolKind.Cursor => Testi.T("strumento.cursore.suggerimento"),
        ToolKind.Miner or ToolKind.MinerAdvanced =>
            Testi.F($"struttura.{entry.StructureId}.suggerimento", Testi.Scava(content, entry.StructureId)),
        _ => Testi.T($"struttura.{entry.StructureId}.suggerimento")
    };

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
            ApplySlotChrome(slot, cat == _category, locked: false);
        }
    }

    /// <summary>Selected, locked or idle slot look; a locked block is also dimmed.</summary>
    private static void ApplySlotChrome(PanelContainer slot, bool selected, bool locked)
    {
        slot.ThemeTypeVariation = selected ? UiTheme.SlotScelto : locked ? UiTheme.SlotBloccato : UiTheme.Slot;
        slot.Modulate = locked && !selected ? new Color(0.55f, 0.55f, 0.55f, 1f) : Colors.White;
    }

    private void AddCategoryButton(Control parent, BuildCategory category, string icon)
    {
        var slot = new PanelContainer
        {
            CustomMinimumSize = new Vector2(CellSide, CellSide),
            MouseFilter = MouseFilterEnum.Stop,
            TooltipText = CategoryLabel(category),
            ThemeTypeVariation = UiTheme.Slot
        };
        parent.AddChild(slot);
        _categorySlots[category] = slot;

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        slot.AddChild(center);
        center.AddChild(MakeIcon(UiTheme.Icon(icon), CellSide - 8, pixel: true));

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
        // Every old cell is freed, the cursor one too (Strumenti): a kept ref would be a disposed object.
        _toolSlots.Clear();

        var shown = 0;
        foreach (var entry in EntriesFor(_category))
        {
            if (!_lockedTools.Contains(entry.Tool))
            {
                AddPaletteCell(_blockGrid, entry);
                shown++;
            }
        }

        // Fixed grid whatever the category and the unlocks: the free cells are empty space (locked blocks are hidden).
        var side = BlockIconSide();
        for (; shown < GridColumnCount * GridRowCount; shown++)
        {
            _blockGrid.AddChild(new Control
            {
                CustomMinimumSize = new Vector2(side + 8, side + 8),
                MouseFilter = MouseFilterEnum.Ignore
            });
        }

        SyncFixedSize();

        if (_hovered is { } hovered && !_toolSlots.ContainsKey(hovered))
        {
            _hovered = null;
            Hovered?.Invoke(null);
        }
    }

    /// <summary>
    /// The parts whose size could follow the unlocks are pinned: the title is as wide as the widest category name and
    /// the rail as tall as all four buttons (a hidden category leaves empty space below).
    /// </summary>
    private void SyncFixedSize()
    {
        if (_categoryTitle is null || !_categoryTitle.IsInsideTree())
        {
            return;
        }

        var font = _categoryTitle.GetThemeFont("font");
        var fontSize = _categoryTitle.GetThemeFontSize("font_size");
        _categoryTitle.CustomMinimumSize = new Vector2(
            Enum.GetValues<BuildCategory>().Max(c => font.GetStringSize(CategoryLabel(c), HorizontalAlignment.Left, -1, fontSize).X)
            + _categoryTitle.GetThemeStylebox("normal").GetMinimumSize().X,
            0);
        if (_categoryRail is not null)
        {
            var buttons = _categorySlots.Count;
            _categoryRail.CustomMinimumSize = new Vector2(
                0, buttons * CellSide + (buttons - 1) * _categoryRail.GetThemeConstant("separation"));
        }
    }

    public override void _Notification(int what)
    {
        // Also sent on entering the tree: the width is measured with the real theme font.
        if (what == NotificationThemeChanged)
        {
            SyncFixedSize();
        }
        else if (what == NotificationTranslationChanged && _categoryTitle is not null)
        {
            // New language: names, tooltips and the title width follow.
            _categoryTitle.Text = CategoryLabel(_category);
            foreach (var (category, slot) in _categorySlots)
            {
                slot.TooltipText = CategoryLabel(category);
            }

            RebuildGrid();
        }
    }

    /// <summary>Grid columns (playtests).</summary>
    internal int GridColumns => _blockGrid?.Columns ?? 0;

    /// <summary>
    /// Block icons are drawn this many screen pixels wide with the Nearest filter: 32 px sprites (1×1 blocks) at their
    /// native size, 64 px sprites (2×2 blocks) halved, so every screen pixel is a whole art pixel. From 200% UI scale on
    /// they are drawn twice that (still whole multiples).
    /// </summary>
    private const float BlockIconScreenPx = 32f;

    /// <summary>Side of a block icon in UI units for the current UI scale (read when the grid is rebuilt).</summary>
    internal static float BlockIconSide(float uiScale) =>
        BlockIconScreenPx * Math.Max(1f, MathF.Floor(uiScale)) / uiScale;

    private static float BlockIconSide() => BlockIconSide(UiScale.Factor);

    /// <summary>The UI scale changed: block icons are rebuilt at their new size in UI units.</summary>
    public void OnUiScaleChanged() => RebuildGrid();

    private TextureRect BlockIcon(string structureId, float side) => new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        CustomMinimumSize = new Vector2(side, side),
        TextureFilter = TextureFilterEnum.Nearest,
        MouseFilter = MouseFilterEnum.Ignore,
        Texture = Sprites.TryFor(structureId)
    };

    private void AddPaletteCell(Control parent, PaletteEntry entry)
    {
        var side = BlockIconSide();
        var slot = new PanelContainer
        {
            CustomMinimumSize = new Vector2(side + 8, side + 8),
            MouseFilter = MouseFilterEnum.Stop,
            TooltipText = string.IsNullOrEmpty(entry.Hotkey) || entry.Tool == ToolKind.Cursor
                ? DisplayName(entry)
                : $"{DisplayName(entry)} [{entry.Hotkey}]",
            ThemeTypeVariation = UiTheme.Slot
        };
        parent.AddChild(slot);
        _toolSlots[entry.Tool] = slot;

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        slot.AddChild(center);

        if (entry.Tool == ToolKind.Cursor)
        {
            var glyph = MakeLabel("↖", UiTheme.TitoloChiaro);
            glyph.HorizontalAlignment = HorizontalAlignment.Center;
            center.AddChild(glyph);
        }
        else if (entry.Tool == ToolKind.Sorter)
        {
            // Frame + dynamic filter glyph (~70% of slot).
            var stack = new Control
            {
                CustomMinimumSize = new Vector2(side, side),
                MouseFilter = MouseFilterEnum.Ignore
            };
            var frame = BlockIcon(entry.StructureId, side);
            frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            stack.AddChild(frame);

            // Item filter in the empty middle of the sorter (14/32 of the sprite): the 32 px item art drawn smaller,
            // smoothly.
            var half = side * 7f / 32f;
            var filter = new TextureRect
            {
                Name = "SorterFilter",
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(2 * half, 2 * half),
                MouseFilter = MouseFilterEnum.Ignore,
                TextureFilter = TextureFilterEnum.Linear
            };
            filter.SetAnchorsPreset(LayoutPreset.Center);
            filter.OffsetLeft = -half;
            filter.OffsetTop = -half;
            filter.OffsetRight = half;
            filter.OffsetBottom = half;
            ApplySorterFilterTexture(filter);
            stack.AddChild(filter);
            _sorterFilterIcon = filter;
            center.AddChild(stack);
        }
        else
        {
            center.AddChild(BlockIcon(entry.StructureId, side));
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
            Choose(ToolKind.Cursor, Testi.T("strumento.cursore"));
            return;
        }

        if (_lockedTools.Contains(entry.Tool))
        {
            ToastRequested?.Invoke(Testi.F("rifiuto.Locked", DisplayName(entry)));
            return;
        }

        if (_selected == entry.Tool)
        {
            Choose(ToolKind.Cursor, Testi.T("strumento.cursore"));
            return;
        }

        Choose(entry.Tool, Testi.F("palette.selezionato", DisplayName(entry)));
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
        BuildCategory.Tools => Testi.T("categoria.strumenti"),
        BuildCategory.Logistics => Testi.T("categoria.logistica"),
        BuildCategory.Production => Testi.T("categoria.produzione"),
        BuildCategory.Power => Testi.T("categoria.potenza"),
        _ => ""
    };

    private static BuildCategory CategoryFor(ToolKind tool) => tool switch
    {
        ToolKind.Cursor => BuildCategory.Tools,
        ToolKind.Belt or ToolKind.BeltFast or ToolKind.BeltExpress or ToolKind.Junction or ToolKind.Splitter or ToolKind.Sorter or ToolKind.Bridge
            => BuildCategory.Logistics,
        ToolKind.Miner or ToolKind.MinerAdvanced or ToolKind.Smelter or ToolKind.Assembler or ToolKind.Extractor
            => BuildCategory.Production,
        ToolKind.Generator or ToolKind.PowerNode or ToolKind.PowerNodeT2 or ToolKind.Lamp => BuildCategory.Power,
        _ => BuildCategory.Logistics
    };
}
