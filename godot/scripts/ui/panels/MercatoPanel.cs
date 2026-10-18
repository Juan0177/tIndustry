using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Mercato overlay: stocked rows, dynamic price, vendi 1 / tutti.</summary>
public partial class MercatoPanel : Control
{
    private IGameView? _view;
    private Action<SimCommand>? _submit;
    private Func<IReadOnlyList<SimCommand>>? _pendingCommands;
    // Sales submitted and not yet answered, in order: (amount, name) at click time.
    private readonly Queue<(int Amount, string Name)> _pendingSales = new();
    private VBoxContainer? _list;
    private Label? _walletLabel;
    private Label? _hintLabel;
    private Label? _statusLabel;
    private Label? _autoSellHint;
    private Button? _autoSellToggle;
    private ScrollContainer? _scroll;

    public event Action? Closed;
    public event Action? SoldChanged;
    public event Action? AutoSellChanged;

    public override void _Ready()
    {
        Name = "MercatoPanel";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        BuildUi();
    }

    /// <param name="pendingCommands">Commands submitted but not yet applied (see <see cref="SimHost.Pending"/>).</param>
    public void Open(IGameView factory, Action<SimCommand> submit, Func<IReadOnlyList<SimCommand>> pendingCommands)
    {
        _view = factory;
        _submit = submit;
        _pendingCommands = pendingCommands;
        _pendingSales.Clear();
        Visible = true;
        if (_autoSellToggle is not null)
        {
            SyncAutoSellToggle();
        }

        Refresh();
    }

    public void Close()
    {
        Visible = false;
        Closed?.Invoke();
    }

    public bool IsOpen => Visible;

    private void SyncAutoSellToggle()
    {
        if (_autoSellToggle is null || _view is null)
        {
            return;
        }

        var on = _view.AutoSellAtCore;
        _autoSellToggle.Text = on ? "ON" : "OFF";
        _autoSellToggle.ThemeTypeVariation = on ? UiTheme.PulsanteScelto : "";
    }

    private void OnAutoSellTogglePressed()
    {
        if (_view is null || _submit is null)
        {
            return;
        }

        // Toggle from the value the host will have once earlier same-tick toggles are applied.
        var effective = _pendingCommands?.Invoke().OfType<SetAutoSell>().LastOrDefault()?.Enabled
            ?? _view.AutoSellAtCore;
        _submit(new SetAutoSell(0, !effective));
    }

    /// <summary>Host applied a Mercato command: update status, toggle and notify the world.</summary>
    /// <param name="moneyDelta">Money change caused by this command alone (<see cref="SimHost.LastCommandMoneyDelta"/>).</param>
    public void OnCommandApplied(SimCommand command, int moneyDelta)
    {
        if (_view is null)
        {
            return;
        }

        switch (command)
        {
            case SetAutoSell:
                if (_statusLabel is not null)
                {
                    _statusLabel.Text = _view.AutoSellAtCore
                        ? "Vendita automatica attivata."
                        : "Vendita automatica disattivata.";
                }

                SyncAutoSellToggle();
                RefreshAutoSellHint();
                AutoSellChanged?.Invoke();
                break;
            case Sell when _pendingSales.Count > 0:
                var (amount, name) = _pendingSales.Dequeue();
                if (_statusLabel is not null)
                {
                    _statusLabel.Text = $"Venduti {amount}× {name} → +${moneyDelta}";
                }

                SoldChanged?.Invoke();
                Refresh();
                break;
        }
    }

    /// <summary>Host refused a Mercato command: show its reason.</summary>
    public void OnCommandRejected(SimCommand command, string reason)
    {
        if (command is Sell && _pendingSales.Count > 0)
        {
            _pendingSales.Dequeue();
        }

        if (command is Sell or SetAutoSell && _statusLabel is not null)
        {
            _statusLabel.Text = reason;
        }
    }

    private void RefreshAutoSellHint()
    {
        if (_autoSellHint is null || _view is null)
        {
            return;
        }

        _autoSellHint.Text = _view.AutoSellAtCore
            ? "Tutto ciò che arriva al CORE viene venduto subito."
            : "Gli oggetti restano nel CORE: vendili da qui.";
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
        {
            return;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo
            && (key.Keycode == Key.Escape || key.Keycode == Key.M))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildUi()
    {
        var dim = new ColorRect
        {
            Color = UiColors.Oscuramento,
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        // Card sized to its content (Mindustry dialogs): no fixed height, no empty slab; it scrolls when the scaled
        // window is shorter.
        var panel = new PanelContainer { Name = "MercatoCard", ThemeTypeVariation = UiTheme.Scheda };
        AddChild(panel);
        var outer = new ScrollContainer();
        panel.AddChild(outer);
        var root = new VBoxContainer { ThemeTypeVariation = UiTheme.ColonnaLarga, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        outer.AddChild(root);
        CardLayout.CenterToContent(panel, outer, root, 800);

        // Title · Fondi
        var header = new HBoxContainer { ThemeTypeVariation = UiTheme.RigaLarga };
        root.AddChild(header);
        header.AddChild(Icon(UiTheme.Icon("mercato"), 32, pixel: true));
        header.AddChild(new Label { Text = "Mercato", SizeFlagsHorizontal = SizeFlags.ExpandFill, ThemeTypeVariation = UiTheme.Titolo });
        header.AddChild(Icon(HudStyle.ItemTexture("money"), 32));
        _walletLabel = new Label
        {
            Text = "$0",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Pass,
            ThemeTypeVariation = UiTheme.TestoDenaro
        };
        header.AddChild(_walletLabel);

        // Vendita automatica (toggle + one-line explanation)
        var autoRow = new HBoxContainer { ThemeTypeVariation = UiTheme.RigaAmpia };
        root.AddChild(autoRow);
        autoRow.AddChild(new Label { Text = "Vendita automatica al CORE", VerticalAlignment = VerticalAlignment.Center });
        _autoSellToggle = new Button
        {
            Text = "OFF",
            CustomMinimumSize = new Vector2(64, 30),
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        _autoSellToggle.Pressed += OnAutoSellTogglePressed;
        autoRow.AddChild(_autoSellToggle);
        _autoSellHint = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ThemeTypeVariation = UiTheme.TestoTenue
        };
        autoRow.AddChild(_autoSellHint);

        root.AddChild(new HSeparator());

        // Table header
        var columns = new HBoxContainer { ThemeTypeVariation = UiTheme.RigaAmpia };
        root.AddChild(columns);
        AddColumnHeader(columns, "Oggetto", 0, expand: true);
        AddColumnHeader(columns, "Quantità", ColStock);
        AddColumnHeader(columns, "Prezzo", ColPrice);
        AddColumnHeader(columns, "Valore", ColValue);
        AddColumnHeader(columns, "", ColActions);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        root.AddChild(scroll);
        _scroll = scroll;

        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(_list);

        _hintLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, ThemeTypeVariation = UiTheme.TestoTenue };
        root.AddChild(_hintLabel);

        // Footer: status · Indietro (Mindustry puts "back" at the bottom)
        var footer = new HBoxContainer { ThemeTypeVariation = UiTheme.RigaAmpia };
        root.AddChild(footer);
        _statusLabel = new Label
        {
            Text = "",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            ThemeTypeVariation = UiTheme.TestoAccento
        };
        footer.AddChild(_statusLabel);
        var closeBtn = new Button { Text = "Indietro", CustomMinimumSize = new Vector2(140, 36), FocusMode = FocusModeEnum.None };
        closeBtn.Pressed += Close;
        footer.AddChild(closeBtn);
    }

    private const float ColStock = 90f;
    private const float ColPrice = 90f;
    private const float ColValue = 90f;
    private const float ColActions = 268f;
    private const float RowHeight = 40f;
    private const int MaxVisibleRows = 6;

    private static TextureRect Icon(Texture2D? tex, float size, bool pixel = false)
    {
        var icon = HudStyle.MakeIcon(tex, size, pixel: pixel);
        icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        return icon;
    }

    private static void AddColumnHeader(HBoxContainer row, string text, float width, bool expand = false)
    {
        row.AddChild(new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(width, 0),
            SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin,
            HorizontalAlignment = expand ? HorizontalAlignment.Left : HorizontalAlignment.Right,
            ThemeTypeVariation = UiTheme.TestoTenue
        });
    }

    /// <summary>A right-aligned table cell with the theme look <paramref name="look"/> (empty = plain text).</summary>
    private static Label Cell(string text, float width, string look = "") => new()
    {
        Text = text,
        CustomMinimumSize = new Vector2(width, 0),
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center,
        ThemeTypeVariation = look
    };

    public void Refresh()
    {
        if (_view is null || _list is null)
        {
            return;
        }

        foreach (var child in _list.GetChildren())
        {
            child.QueueFree();
        }

        if (_walletLabel is not null)
        {
            _walletLabel.Text = $"${_view.Wallet.Money}";
            _walletLabel.TooltipText = $"Fondi · venduto finora ${_view.Session.SaleIncome}";
        }

        SyncAutoSellToggle();
        RefreshAutoSellHint();

        if (_hintLabel is not null)
        {
            _hintLabel.Text = _view.Market.BestValueHint();
        }

        var rows = 0;
        foreach (var item in _view.Market.Items)
        {
            var stock = _view.Wallet.MaterialCount(item.ItemId);
            if (stock <= 0)
            {
                continue;
            }

            AddRow(item, stock);
            rows++;
        }

        if (rows == 0)
        {
            var empty = new Label
            {
                Text = "Niente da vendere: porta minerali o lastre al CORE con i nastri.",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CustomMinimumSize = new Vector2(0, RowHeight),
                ThemeTypeVariation = UiTheme.TestoTenue
            };
            _list.AddChild(empty);
            rows = 1;
        }

        if (_scroll is not null)
        {
            _scroll.CustomMinimumSize = new Vector2(0, Math.Min(rows, MaxVisibleRows) * (RowHeight + 4));
        }
    }

    private void AddRow(MarketItemDefinition item, int stock)
    {
        if (_view is null || _list is null)
        {
            return;
        }

        var price = _view.PreviewSellPrice(item.ItemId);
        var row = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, RowHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ThemeTypeVariation = UiTheme.Riga
        };
        _list.AddChild(row);

        var h = new HBoxContainer { ThemeTypeVariation = UiTheme.RigaAmpia };
        row.AddChild(h);

        var nameBox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, ThemeTypeVariation = UiTheme.RigaLarga };
        h.AddChild(nameBox);
        nameBox.AddChild(Icon(HudStyle.ItemTexture(item.ItemId), 32));
        nameBox.AddChild(new Label { Text = item.DisplayName, VerticalAlignment = VerticalAlignment.Center });

        h.AddChild(Cell($"{stock}", ColStock));
        var priceCell = Cell($"${price}", ColPrice, price < item.SellPrice ? UiTheme.TestoRosso : "");
        priceCell.TooltipText = price < item.SellPrice
            ? $"Listino ${item.SellPrice}: il prezzo scende se vendi molto dello stesso oggetto"
            : $"Listino ${item.SellPrice}";
        priceCell.MouseFilter = MouseFilterEnum.Pass;
        h.AddChild(priceCell);
        h.AddChild(Cell($"${price * stock}", ColValue, UiTheme.TestoDenaro));

        var actions = new HBoxContainer { CustomMinimumSize = new Vector2(ColActions, 0), Alignment = BoxContainer.AlignmentMode.End };
        h.AddChild(actions);
        var id = item.ItemId;
        var btn1 = new Button { Text = "Vendi 1", CustomMinimumSize = new Vector2(0, 32), FocusMode = FocusModeEnum.None };
        btn1.Pressed += () => RequestSale(id, 1);
        actions.AddChild(btn1);
        var btnAll = new Button { Text = "Vendi tutto", CustomMinimumSize = new Vector2(0, 32), FocusMode = FocusModeEnum.None };
        btnAll.Pressed += () =>
        {
            if (_view is not null)
            {
                RequestSale(id, _view.Wallet.MaterialCount(id));
            }
        };
        actions.AddChild(btnAll);
    }

    private void RequestSale(string itemId, int amount)
    {
        if (_view is null || _submit is null || amount <= 0)
        {
            return;
        }

        _pendingSales.Enqueue((amount, _view.Market.GetDisplayName(itemId)));
        _submit(new Sell(0, itemId, amount));
    }
}
