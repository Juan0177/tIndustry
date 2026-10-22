using System.Globalization;
using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Mercato overview: one row per item the CORE holds (or sells automatically) with icon, name, quantity, price with
/// ▲/▼, a chart of the last 2 minutes, "Crollo · mm:ss" during a crash, Vendi 1 / Vendi 100 / Vendi tutto and the
/// item's auto-sell switch. No button sells every item at once. The rows follow the market a few times a second.
/// </summary>
public partial class MercatoPanel : Control
{
    /// <summary>Seconds between two reads of the market (the view copies every item's history on each read).</summary>
    private const double RefreshSeconds = 0.25;

    private const float RowHeight = 76f;
    private const int MaxVisibleRows = 4;

    private IGameView? _view;
    private Action<SimCommand>? _submit;
    private Func<IReadOnlyList<SimCommand>>? _pendingCommands;
    private readonly List<Row> _rows = [];
    private double _sinceRefresh;

    // The status line is kept as a function so a new language writes it again.
    private Func<string>? _status;
    private VBoxContainer? _list;
    private Label? _walletLabel;
    private Label? _waitLabel;
    private Label? _statusLabel;
    private ScrollContainer? _scroll;

    public event Action? Closed;
    public event Action? SoldChanged;
    public event Action? AutoSellChanged;

    /// <summary>The controls of one item row that change while the panel is open.</summary>
    private sealed record Row(
        string ItemId, Label Quantity, Label Price, MarketSparkline Chart, Label Crash, Button SellOne, Button SellHundred,
        Button SellAll, CheckBox Auto);

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
        Visible = true;
        Refresh();
    }

    public void Close()
    {
        Visible = false;
        Closed?.Invoke();
    }

    public bool IsOpen => Visible;

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _sinceRefresh += delta;
        if (_sinceRefresh >= RefreshSeconds)
        {
            Refresh();
        }
    }

    /// <summary>Host applied a Mercato command: update the rows and notify the world.</summary>
    public void OnCommandApplied(SimCommand command)
    {
        if (_view is null)
        {
            return;
        }

        switch (command)
        {
            case Sell or SellAll:
                SoldChanged?.Invoke();
                break;
            case SetAutoSell autoSell:
                SetStatus(() => Testi.T(autoSell.Enabled ? "mercato.auto.attivata" : "mercato.auto.disattivata"));
                AutoSellChanged?.Invoke();
                break;
        }

        Refresh();
    }

    /// <summary>A sale was resolved at the click: what it sold and paid, or the jackpot with its income.</summary>
    public void OnSaleResolved(SaleResolved sale)
    {
        var content = _view?.Content;
        var crashMinutes = content?.MarketRules?.CrashMinutes ?? 0;
        SetStatus(sale.Jackpot
            ? () => Testi.F("mercato.jackpot", sale.Income)
            : sale.Crash
                ? () => Testi.F("mercato.venduti.crollo", sale.Pieces, Testi.Oggetto(sale.ItemId, content), sale.Income, crashMinutes)
                : () => Testi.F("mercato.venduti", sale.Pieces, Testi.Oggetto(sale.ItemId, content), sale.Income));
    }

    /// <summary>Host refused a Mercato command: show its reason.</summary>
    public void OnCommandRejected(SimCommand command, Refusal reason)
    {
        if (command is not (Sell or SellAll or SetAutoSell))
        {
            return;
        }

        var view = _view;
        SetStatus(() => Testi.Rifiuto(reason, view));
        // A refused switch goes back to what the market says.
        Refresh();
    }

    private void SetStatus(Func<string>? status)
    {
        _status = status;
        if (_statusLabel is not null)
        {
            _statusLabel.Text = status?.Invoke() ?? "";
        }
    }

    /// <summary>The status line as shown (playtests).</summary>
    internal string StatusText => _statusLabel?.Text ?? "";

    public override void _Notification(int what)
    {
        // New language: the status line (also while closed) and the formatted texts of the rows are written again.
        if (what != NotificationTranslationChanged)
        {
            return;
        }

        SetStatus(_status);
        // Item names are written into the rows: build them again.
        _rows.Clear();
        if (Visible)
        {
            Refresh();
        }
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
        header.AddChild(new Label { Text = "menu.mercato", SizeFlagsHorizontal = SizeFlags.ExpandFill, ThemeTypeVariation = UiTheme.Titolo });
        header.AddChild(Icon(HudStyle.ItemTexture("money"), 32));
        _walletLabel = new Label
        {
            Text = "$0",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Pass,
            ThemeTypeVariation = UiTheme.TestoDenaro
        };
        header.AddChild(_walletLabel);

        root.AddChild(new HSeparator());

        _scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        root.AddChild(_scroll);
        var clear = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, ThemeTypeVariation = UiTheme.MargineBarra };
        _scroll.AddChild(clear);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        clear.AddChild(_list);

        // "Vendi tutto" locked for every item after a missed jackpot attempt: how long, under the rows.
        _waitLabel = new Label
        {
            Name = "Attesa",
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ThemeTypeVariation = UiTheme.TestoRosso
        };
        root.AddChild(_waitLabel);

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
        var closeBtn = new Button { Text = "comune.indietro", CustomMinimumSize = new Vector2(140, 36), FocusMode = FocusModeEnum.None };
        closeBtn.Pressed += Close;
        footer.AddChild(closeBtn);
    }

    private static TextureRect Icon(Texture2D? tex, float size, bool pixel = false)
    {
        var icon = HudStyle.MakeIcon(tex, size, pixel: pixel);
        icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        return icon;
    }

    /// <summary>Reads the market once: rebuilds the rows when the listed items change, else updates them in place.</summary>
    public void Refresh()
    {
        _sinceRefresh = 0;
        if (_view is null || _list is null)
        {
            return;
        }

        if (_walletLabel is not null)
        {
            _walletLabel.Text = $"${_view.Wallet.Money}";
            _walletLabel.TooltipText = Testi.F("mercato.fondi", _view.Session.SaleIncome);
        }

        var wait = _view.SellAllCooldownSeconds;
        if (_waitLabel is not null)
        {
            _waitLabel.Visible = wait > 0;
            _waitLabel.Text = wait > 0 ? Testi.F("mercato.attesa", wait) : "";
        }

        // An item selling automatically never reaches the CORE: it stays listed so its switch can be turned off.
        var listed = _view.MarketItems.Where(i => i.AutoSell || _view.Wallet.MaterialCount(i.ItemId) > 0).ToList();
        if (!listed.Select(i => i.ItemId).SequenceEqual(_rows.Select(r => r.ItemId)))
        {
            Rebuild(listed);
        }

        foreach (var (row, item) in _rows.Zip(listed))
        {
            UpdateRow(row, item, _view.Wallet.MaterialCount(item.ItemId), wait);
        }
    }

    private void Rebuild(IReadOnlyList<MarketItemView> listed)
    {
        // Out of the tree now: a row still queued for deletion would make the new one of the same item get a
        // different name (Riga_x renamed by Godot).
        foreach (var child in _list!.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }

        _rows.Clear();
        foreach (var item in listed)
        {
            _rows.Add(AddRow(item.ItemId));
        }

        if (listed.Count == 0)
        {
            _list.AddChild(new Label
            {
                Text = "mercato.vuoto",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, RowHeight),
                ThemeTypeVariation = UiTheme.TestoTenue
            });
        }

        if (_scroll is not null)
        {
            _scroll.CustomMinimumSize = new Vector2(0, Math.Clamp(listed.Count, 1, MaxVisibleRows) * (RowHeight + 4));
        }
    }

    private Row AddRow(string itemId)
    {
        var panel = new PanelContainer
        {
            Name = $"Riga_{itemId}",
            CustomMinimumSize = new Vector2(0, RowHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ThemeTypeVariation = UiTheme.Riga
        };
        _list!.AddChild(panel);
        var lines = new VBoxContainer { ThemeTypeVariation = UiTheme.ColonnaStretta, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        panel.AddChild(lines);

        // Icon · name · quantity · chart · price
        var top = new HBoxContainer { ThemeTypeVariation = UiTheme.RigaAmpia };
        lines.AddChild(top);
        top.AddChild(Icon(HudStyle.ItemTexture(itemId), 32));
        top.AddChild(new Label
        {
            Text = Testi.Oggetto(itemId, _view?.Content),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        });
        var quantity = new Label
        {
            Name = "Quantita",
            CustomMinimumSize = new Vector2(72, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        top.AddChild(quantity);
        var chart = new MarketSparkline { Name = "Grafico", CustomMinimumSize = new Vector2(MarketSparkline.Span + 40, 30) };
        top.AddChild(chart);
        var price = new Label
        {
            Name = "Prezzo",
            CustomMinimumSize = new Vector2(96, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Pass
        };
        top.AddChild(price);

        // Crollo · Vendi 1 · Vendi 100 · Vendi tutto · Automatica
        var bottom = new HBoxContainer { ThemeTypeVariation = UiTheme.RigaLarga };
        lines.AddChild(bottom);
        var crash = new Label
        {
            Name = "Crollo",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            ThemeTypeVariation = UiTheme.TestoCrollo
        };
        bottom.AddChild(crash);
        Button SellButton(string name, string text, Func<SimCommand> command)
        {
            var button = new Button
            {
                Name = name,
                Text = text,
                CustomMinimumSize = new Vector2(0, 30),
                FocusMode = FocusModeEnum.None,
                MouseDefaultCursorShape = CursorShape.PointingHand
            };
            button.Pressed += () => _submit?.Invoke(command());
            bottom.AddChild(button);
            return button;
        }

        var one = SellButton("Vendi1", "mercato.vendi.uno", () => new Sell(0, itemId, 1));
        var hundred = SellButton("Vendi100", "mercato.vendi.cento", () => new Sell(0, itemId, 100));
        var all = SellButton("VendiTutto", "mercato.vendi.tutto", () => new SellAll(0, itemId));
        var auto = new CheckBox
        {
            Name = "Automatica",
            Text = "mercato.auto.oggetto",
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        auto.Toggled += on => _submit?.Invoke(new SetAutoSell(0, itemId, on));
        bottom.AddChild(auto);
        return new Row(itemId, quantity, price, chart, crash, one, hundred, all, auto);
    }

    private void UpdateRow(Row row, MarketItemView item, int stock, int wait)
    {
        row.Quantity.Text = $"{stock}";

        // ▲/▼ against the start of the chart (2 minutes ago), so arrow and chart tell the same story.
        var start = item.HistoryMilli.Count > 0 ? item.HistoryMilli[0] : item.PriceMilli;
        var trend = item.PriceMilli > start ? MarketTrend.Su : item.PriceMilli < start ? MarketTrend.Giu : MarketTrend.Piatto;
        row.Price.Text = trend switch
        {
            MarketTrend.Su => $"${item.UnitIncome} ▲",
            MarketTrend.Giu => $"${item.UnitIncome} ▼",
            _ => $"${item.UnitIncome}"
        };
        row.Price.ThemeTypeVariation = trend switch
        {
            MarketTrend.Su => UiTheme.TestoSu,
            MarketTrend.Giu => UiTheme.TestoGiu,
            _ => ""
        };
        row.Price.TooltipText = Testi.F("mercato.listino", item.BaseValue);

        var crashed = item.Phase == MarketPhase.Crash;
        row.Chart.SetData(item.HistoryMilli, crashed ? MarketTrend.Crollo : trend);
        var left = item.CrashSecondsLeft;
        row.Crash.Text = crashed
            ? Testi.F("mercato.crollo", string.Create(CultureInfo.InvariantCulture, $"{left / 60:00}:{left % 60:00}"))
            : "";

        row.SellOne.Disabled = stock < 1;
        row.SellHundred.Disabled = stock < 100;
        row.SellAll.Disabled = wait > 0 || stock < 1;
        row.SellAll.TooltipText = wait > 0 ? Testi.F("mercato.attesa", wait) : "";

        // The switch shows what the host will have once switches clicked this tick are applied.
        var pending = _pendingCommands?.Invoke().OfType<SetAutoSell>().LastOrDefault(c => c.ItemId == item.ItemId);
        row.Auto.SetPressedNoSignal(pending?.Enabled ?? item.AutoSell);
    }
}
