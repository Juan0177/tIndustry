using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Mercato overlay: stocked rows, dynamic price, vendi 1 / tutti.</summary>
public partial class MercatoPanel : Control
{
    private static readonly Color PanelBg = new(0.08f, 0.10f, 0.11f, 0.96f);
    private static readonly Color Accent = new(0.92f, 0.82f, 0.42f, 1f);
    private static readonly Color TextPrimary = new(0.93f, 0.95f, 0.90f, 1f);
    private static readonly Color TextMuted = new(0.70f, 0.76f, 0.70f, 1f);
    private static readonly Color RowBg = new(0.16f, 0.18f, 0.17f, 1f);

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
        foreach (var state in new[] { "normal", "hover", "pressed" })
        {
            _autoSellToggle.AddThemeStyleboxOverride(state, new StyleBoxFlat
            {
                BgColor = on ? new Color(0.30f, 0.26f, 0.10f, 1f) : new Color(0.16f, 0.18f, 0.17f, 1f),
                BorderColor = on ? Accent : new Color(0.40f, 0.44f, 0.42f, 1f),
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4
            });
        }

        _autoSellToggle.AddThemeColorOverride("font_color", on ? Accent : TextMuted);
        _autoSellToggle.AddThemeColorOverride("font_hover_color", on ? Accent : TextPrimary);
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
            Color = new Color(0f, 0f, 0f, 0.55f),
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        // Card sized to its content (Mindustry dialogs): no fixed height, no empty slab.
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { Name = "MercatoCard", CustomMinimumSize = new Vector2(660, 0) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = PanelBg,
            BorderColor = Accent,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 12,
            ContentMarginBottom = 12
        });
        center.AddChild(panel);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        panel.AddChild(root);

        // Title · Fondi
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);
        var title = new Label { Text = "Mercato", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeColorOverride("font_color", Accent);
        title.AddThemeFontSizeOverride("font_size", 22);
        header.AddChild(title);
        header.AddChild(Icon(HudStyle.ItemTexture("money"), 22, MoneyColor));
        _walletLabel = new Label { Text = "$0", VerticalAlignment = VerticalAlignment.Center };
        _walletLabel.AddThemeColorOverride("font_color", MoneyColor);
        _walletLabel.AddThemeFontSizeOverride("font_size", 18);
        header.AddChild(_walletLabel);

        // Vendita automatica (toggle + one-line explanation)
        var autoRow = new HBoxContainer();
        autoRow.AddThemeConstantOverride("separation", 10);
        root.AddChild(autoRow);
        var autoLabel = new Label { Text = "Vendita automatica al CORE", VerticalAlignment = VerticalAlignment.Center };
        autoLabel.AddThemeColorOverride("font_color", TextPrimary);
        autoLabel.AddThemeFontSizeOverride("font_size", 14);
        autoRow.AddChild(autoLabel);
        _autoSellToggle = new Button
        {
            Text = "OFF",
            CustomMinimumSize = new Vector2(64, 30),
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        _autoSellToggle.AddThemeFontSizeOverride("font_size", 13);
        _autoSellToggle.Pressed += OnAutoSellTogglePressed;
        autoRow.AddChild(_autoSellToggle);
        _autoSellHint = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _autoSellHint.AddThemeColorOverride("font_color", TextMuted);
        _autoSellHint.AddThemeFontSizeOverride("font_size", 12);
        autoRow.AddChild(_autoSellHint);

        root.AddChild(new HSeparator());

        // Table header
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 10);
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
        _list.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_list);

        _hintLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _hintLabel.AddThemeColorOverride("font_color", TextMuted);
        _hintLabel.AddThemeFontSizeOverride("font_size", 12);
        root.AddChild(_hintLabel);

        // Footer: status · Indietro (Mindustry puts "back" at the bottom)
        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 10);
        root.AddChild(footer);
        _statusLabel = new Label { Text = "", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
        _statusLabel.AddThemeColorOverride("font_color", Accent);
        _statusLabel.AddThemeFontSizeOverride("font_size", 13);
        footer.AddChild(_statusLabel);
        var closeBtn = new Button { Text = "Indietro", CustomMinimumSize = new Vector2(140, 36), FocusMode = FocusModeEnum.None };
        closeBtn.Pressed += Close;
        footer.AddChild(closeBtn);
    }

    private const float ColStock = 80f;
    private const float ColPrice = 96f;
    private const float ColValue = 80f;
    private const float ColActions = 176f;
    private const float RowHeight = 40f;
    private const int MaxVisibleRows = 8;

    private static readonly Color MoneyColor = new(0.98f, 0.82f, 0.38f, 1f);
    private static readonly Color PriceDown = new(0.90f, 0.55f, 0.40f, 1f);

    private static TextureRect Icon(Texture2D? tex, float size, Color? tint = null)
    {
        var icon = new TextureRect
        {
            Texture = tex,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(size, size),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore
        };
        if (tint is { } t)
        {
            icon.Modulate = t;
        }

        return icon;
    }

    private static void AddColumnHeader(HBoxContainer row, string text, float width, bool expand = false)
    {
        var label = new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(width, 0),
            SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin,
            HorizontalAlignment = expand ? HorizontalAlignment.Left : HorizontalAlignment.Right
        };
        label.AddThemeColorOverride("font_color", TextMuted);
        label.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(label);
    }

    private static Label Cell(string text, float width, Color color, int size = 14)
    {
        var label = new Label
        {
            Text = text,
            CustomMinimumSize = new Vector2(width, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

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
                CustomMinimumSize = new Vector2(0, RowHeight)
            };
            empty.AddThemeColorOverride("font_color", TextMuted);
            empty.AddThemeFontSizeOverride("font_size", 14);
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
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = RowBg,
            ContentMarginLeft = 8,
            ContentMarginRight = 6,
            ContentMarginTop = 2,
            ContentMarginBottom = 2
        });
        _list.AddChild(row);

        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 10);
        row.AddChild(h);

        var nameBox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        nameBox.AddThemeConstantOverride("separation", 8);
        h.AddChild(nameBox);
        var icon = Icon(HudStyle.ItemTexture(item.ItemId), 26);
        ItemPalette.Tint(icon, item.ItemId);
        nameBox.AddChild(icon);
        var name = new Label { Text = item.DisplayName, VerticalAlignment = VerticalAlignment.Center };
        name.AddThemeColorOverride("font_color", TextPrimary);
        name.AddThemeFontSizeOverride("font_size", 14);
        nameBox.AddChild(name);

        h.AddChild(Cell($"{stock}", ColStock, TextPrimary));
        var priceCell = Cell($"${price}", ColPrice, price < item.SellPrice ? PriceDown : TextPrimary);
        priceCell.TooltipText = price < item.SellPrice
            ? $"Listino ${item.SellPrice}: il prezzo scende se vendi molto dello stesso oggetto"
            : $"Listino ${item.SellPrice}";
        priceCell.MouseFilter = MouseFilterEnum.Pass;
        h.AddChild(priceCell);
        h.AddChild(Cell($"${price * stock}", ColValue, MoneyColor));

        var actions = new HBoxContainer { CustomMinimumSize = new Vector2(ColActions, 0) };
        actions.AddThemeConstantOverride("separation", 4);
        h.AddChild(actions);
        var id = item.ItemId;
        var btn1 = new Button { Text = "Vendi 1", CustomMinimumSize = new Vector2(80, 32), FocusMode = FocusModeEnum.None };
        btn1.Pressed += () => RequestSale(id, 1);
        actions.AddChild(btn1);
        var btnAll = new Button { Text = "Vendi tutto", CustomMinimumSize = new Vector2(92, 32), FocusMode = FocusModeEnum.None };
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
