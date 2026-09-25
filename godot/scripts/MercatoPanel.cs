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

    private FactorySlice? _slice;
    private VBoxContainer? _list;
    private Label? _walletLabel;
    private Label? _hintLabel;
    private Label? _statusLabel;

    public event Action? Closed;
    public event Action? SoldChanged;

    public override void _Ready()
    {
        Name = "MercatoPanel";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        BuildUi();
    }

    public void Open(FactorySlice slice)
    {
        _slice = slice;
        Visible = true;
        Refresh();
    }

    public void Close()
    {
        Visible = false;
        Closed?.Invoke();
    }

    public bool IsOpen => Visible;

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

        var panel = new PanelContainer { Name = "MercatoCard" };
        panel.SetAnchorsPreset(LayoutPreset.Center);
        panel.OffsetLeft = -380;
        panel.OffsetTop = -260;
        panel.OffsetRight = 380;
        panel.OffsetBottom = 260;
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = PanelBg,
            BorderColor = Accent,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 14,
            ContentMarginBottom = 14
        });
        AddChild(panel);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        panel.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        root.AddChild(header);

        var title = new Label { Text = "Mercato", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeColorOverride("font_color", TextPrimary);
        title.AddThemeFontSizeOverride("font_size", 22);
        header.AddChild(title);

        _walletLabel = new Label { Text = "Magazzino: $0" };
        _walletLabel.AddThemeColorOverride("font_color", TextMuted);
        _walletLabel.AddThemeFontSizeOverride("font_size", 14);
        header.AddChild(_walletLabel);

        var closeBtn = new Button { Text = "Indietro · Esc" };
        closeBtn.Pressed += Close;
        header.AddChild(closeBtn);

        _hintLabel = new Label
        {
            Text = "Vendi stock del Core · prezzi reagiscono alla quantità · M apre / Esc chiude",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _hintLabel.AddThemeColorOverride("font_color", TextMuted);
        _hintLabel.AddThemeFontSizeOverride("font_size", 12);
        root.AddChild(_hintLabel);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 320),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        root.AddChild(scroll);

        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 6);
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_list);

        _statusLabel = new Label { Text = "" };
        _statusLabel.AddThemeColorOverride("font_color", Accent);
        _statusLabel.AddThemeFontSizeOverride("font_size", 13);
        root.AddChild(_statusLabel);
    }

    public void Refresh()
    {
        if (_slice is null || _list is null)
        {
            return;
        }

        foreach (var child in _list.GetChildren())
        {
            child.QueueFree();
        }

        if (_walletLabel is not null)
        {
            _walletLabel.Text =
                $"Magazzino: ${_slice.Wallet.Money} · vendite ${_slice.Session.SaleIncome}";
        }

        if (_hintLabel is not null)
        {
            _hintLabel.Text = _slice.Market.BestValueHint()
                + " · M apre / Esc chiude";
        }

        var rows = 0;
        foreach (var item in _slice.Market.Items)
        {
            var stock = _slice.Wallet.MaterialCount(item.ItemId);
            if (stock <= 0)
            {
                continue;
            }

            AddRow(item, stock);
            rows++;
            if (rows >= 8)
            {
                break;
            }
        }

        if (rows == 0)
        {
            var empty = new Label
            {
                Text = "Nessuna merce in magazzino. Porta ore/lastre al Core, poi vendi qui."
            };
            empty.AddThemeColorOverride("font_color", TextMuted);
            empty.AddThemeFontSizeOverride("font_size", 14);
            _list.AddChild(empty);
        }
    }

    private void AddRow(MarketItemDefinition item, int stock)
    {
        if (_slice is null || _list is null)
        {
            return;
        }

        var price = _slice.PreviewSellPrice(item.ItemId);
        var row = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 52),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = RowBg,
            BorderColor = new Color(0.30f, 0.34f, 0.32f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        });
        _list.AddChild(row);

        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 10);
        row.AddChild(h);

        var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        h.AddChild(info);

        var name = new Label { Text = $"{item.DisplayName}  ·  stock {stock}" };
        name.AddThemeColorOverride("font_color", TextPrimary);
        name.AddThemeFontSizeOverride("font_size", 14);
        info.AddChild(name);

        var priceLabel = new Label
        {
            Text = $"Prezzo ${price}/u  ·  tutti ${price * stock}  ·  listino ${item.SellPrice}"
        };
        priceLabel.AddThemeColorOverride("font_color", TextMuted);
        priceLabel.AddThemeFontSizeOverride("font_size", 12);
        info.AddChild(priceLabel);

        var btn1 = new Button
        {
            Text = "1",
            CustomMinimumSize = new Vector2(56, 36)
        };
        var id = item.ItemId;
        btn1.Pressed += () => Sell(id, 1);
        h.AddChild(btn1);

        var btnAll = new Button
        {
            Text = "tutti",
            CustomMinimumSize = new Vector2(72, 36)
        };
        btnAll.Pressed += () =>
        {
            if (_slice is null)
            {
                return;
            }

            Sell(id, _slice.Wallet.MaterialCount(id));
        };
        h.AddChild(btnAll);
    }

    private void Sell(string itemId, int amount)
    {
        if (_slice is null || amount <= 0)
        {
            return;
        }

        var before = _slice.Wallet.Money;
        var name = _slice.Market.GetDisplayName(itemId);
        if (!_slice.TrySellFromWallet(itemId, amount))
        {
            if (_statusLabel is not null)
            {
                _statusLabel.Text = "Vendita fallita (stock insufficiente).";
            }

            return;
        }

        var gained = _slice.Wallet.Money - before;
        if (_statusLabel is not null)
        {
            _statusLabel.Text = $"Venduti {amount}× {name} → +${gained}";
        }

        SoldChanged?.Invoke();
        Refresh();
    }
}
