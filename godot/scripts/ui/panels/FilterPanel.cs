using Godot;
using TIndustry.Shared;
using static TIndustry.Godot.HudStyle;

namespace TIndustry.Godot;

/// <summary>
/// The filter of a sorter, an inverted sorter or an extractor, and the item the market screen follows, as Mindustry's
/// configuration: a click with no block selected on the block opens a small card over it with the block's name, a grid of item icons (the chosen one marked) and a
/// «nessuno» button. A choice raises <see cref="FilterChosen"/> (the world sends <see cref="SetBlockFilter"/>) and
/// closes the card; the chosen item clicked again also clears the filter, as in Mindustry. Esc, a click elsewhere on
/// the map, another tool or the block going away close it (see the world).
/// </summary>
public partial class FilterPanel : PanelContainer
{
    /// <summary>Icons per row, as Mindustry's item selection.</summary>
    private const int Columns = 4;

    /// <summary>Side of an item icon in UI units (the 32 px item art at its native size at 100%).</summary>
    private const float IconSide = 32f;

    /// <summary>Gap between the card and the block it configures, in UI units.</summary>
    private const float Gap = 6f;

    private VBoxContainer? _body;
    private string _structureId = "";
    private string? _current;
    private IReadOnlyList<string> _itemIds = [];
    private FactoryContent? _content;

    /// <summary>The player chose a filter (null = none) for the block on the tile.</summary>
    public event Action<GridPosition, string?>? FilterChosen;

    /// <summary>The tile of the block being configured, null while closed.</summary>
    public GridPosition? Tile { get; private set; }

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        StylePanel(this, "FilterPanel");
        Visible = false;
        _body = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_body);
    }

    /// <summary>
    /// Opens the card for the block <paramref name="structureId"/> on <paramref name="tile"/> whose filter is
    /// <paramref name="current"/>, offering <paramref name="itemIds"/>.
    /// </summary>
    public void Open(GridPosition tile, string structureId, string? current, IReadOnlyList<string> itemIds, FactoryContent? content)
    {
        Tile = tile;
        _structureId = structureId;
        _current = current;
        _itemIds = itemIds;
        _content = content;
        Rebuild();
        Visible = true;
        // Shrink to the new contents (a block with fewer items than the last one opened).
        Size = Vector2.Zero;
    }

    public void Close()
    {
        Visible = false;
        Tile = null;
    }

    /// <summary>
    /// Puts the card above the point <paramref name="blockTop"/> (the middle of the block's top edge, in the HUD root's
    /// units), or under <paramref name="blockBottom"/> when there is no room above, kept inside the HUD root.
    /// </summary>
    public void PlaceNear(Vector2 blockTop, Vector2 blockBottom)
    {
        var size = GetCombinedMinimumSize();
        var area = GetParentAreaSize();
        var x = Math.Clamp(blockTop.X - size.X / 2f, 0f, Math.Max(0f, area.X - size.X));
        var y = blockTop.Y - Gap - size.Y;
        if (y < 0f)
        {
            y = blockBottom.Y + Gap;
        }

        y = Math.Clamp(y, 0f, Math.Max(0f, area.Y - size.Y));
        Position = new Vector2(MathF.Round(x), MathF.Round(y));
    }

    public override void _Notification(int what)
    {
        if (what == NotificationTranslationChanged && Visible)
        {
            Rebuild(); // new language: title, names and tooltips follow
        }
    }

    private void Rebuild()
    {
        if (_body is null)
        {
            return;
        }

        foreach (var child in _body.GetChildren())
        {
            _body.RemoveChild(child);
            child.QueueFree();
        }

        var screen = _structureId == MarketScreen.BuildingId;
        _body.AddChild(MakeLabel(Testi.F(screen ? "filtro.titolo.schermo" : "filtro.titolo", Testi.Struttura(_structureId, _content)),
            UiTheme.TestoTenue));

        var grid = new GridContainer { Columns = Columns, MouseFilter = MouseFilterEnum.Ignore };
        _body.AddChild(grid);
        foreach (var itemId in _itemIds)
        {
            AddItemCell(grid, itemId);
        }

        var none = new Button
        {
            // Stable name for the agents' ui tool.
            Name = "FiltroNessuno",
            Text = Testi.T("filtro.nessuno"),
            TooltipText = Testi.T(screen ? "filtro.nessuno.suggerimento.schermo" : "filtro.nessuno.suggerimento"),
            FocusMode = FocusModeEnum.None,
            ThemeTypeVariation = _current is null ? UiTheme.PulsanteScelto : ""
        };
        none.Pressed += () => Choose(null);
        _body.AddChild(none);
    }

    private void AddItemCell(Control parent, string itemId)
    {
        var slot = new PanelContainer
        {
            Name = $"Filtro_{itemId}",
            CustomMinimumSize = new Vector2(IconSide + 8, IconSide + 8),
            MouseFilter = MouseFilterEnum.Stop,
            TooltipText = Testi.Oggetto(itemId, _content),
            ThemeTypeVariation = itemId == _current ? UiTheme.SlotScelto : UiTheme.Slot
        };
        parent.AddChild(slot);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        slot.AddChild(center);
        center.AddChild(MakeIcon(ItemTexture(itemId), IconSide));
        slot.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                // The chosen item clicked again clears the filter (Mindustry).
                Choose(itemId == _current ? null : itemId);
                AcceptEvent();
            }
        };
    }

    private void Choose(string? itemId)
    {
        if (Tile is not { } tile)
        {
            return;
        }

        Close();
        FilterChosen?.Invoke(tile, itemId);
    }
}
