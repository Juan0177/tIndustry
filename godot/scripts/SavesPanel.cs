using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Gestione salvataggi (Raylib thin): lista slot, Carica / Elimina / Duplica Continua.
/// </summary>
public partial class SavesPanel : Control
{
    private static readonly Color Dim = new(0.04f, 0.05f, 0.05f, 0.82f);
    private static readonly Color CardBg = new(0.08f, 0.10f, 0.11f, 0.96f);
    private static readonly Color Accent = new(0.92f, 0.78f, 0.28f, 1f);
    private static readonly Color TextPrimary = new(0.93f, 0.95f, 0.90f, 1f);
    private static readonly Color TextMuted = new(0.68f, 0.74f, 0.68f, 1f);
    private static readonly Color RowBg = new(0.16f, 0.18f, 0.17f, 1f);
    private static readonly Color RowSelected = new(0.28f, 0.26f, 0.14f, 1f);

    private VBoxContainer? _list;
    private Label? _hint;
    private Label? _status;
    private string? _selectedId;
    private string? _deleteArmedId;

    public event Action? Closed;
    public event Action<string>? SlotLoadRequested;

    public override void _Ready()
    {
        Name = "SavesPanel";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        BuildUi();
    }

    public bool IsOpen => Visible;

    public void Open()
    {
        _selectedId = null;
        _deleteArmedId = null;
        Visible = true;
        Refresh();
    }

    public void Close()
    {
        Visible = false;
        Closed?.Invoke();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
        {
            return;
        }

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildUi()
    {
        var dim = new ColorRect { Color = Dim, MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var card = new PanelContainer { Name = "SavesCard" };
        card.SetAnchorsPreset(LayoutPreset.Center);
        card.OffsetLeft = -340;
        card.OffsetTop = -280;
        card.OffsetRight = 340;
        card.OffsetBottom = 280;
        card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = CardBg,
            BorderColor = Accent,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 18,
            ContentMarginRight = 18,
            ContentMarginTop = 16,
            ContentMarginBottom = 16
        });
        AddChild(card);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        card.AddChild(root);

        var title = new Label
        {
            Text = "Gestione salvataggi",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeColorOverride("font_color", Accent);
        title.AddThemeFontSizeOverride("font_size", 22);
        root.AddChild(title);

        _hint = new Label
        {
            Text = "Seleziona uno slot · Carica / Elimina · Duplica Continua crea una copia.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _hint.AddThemeColorOverride("font_color", TextMuted);
        _hint.AddThemeFontSizeOverride("font_size", 12);
        root.AddChild(_hint);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 280),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddChild(scroll);

        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 6);
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_list);

        _status = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _status.AddThemeColorOverride("font_color", TextMuted);
        _status.AddThemeFontSizeOverride("font_size", 12);
        root.AddChild(_status);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);

        actions.AddChild(MakeAction("Carica", OnLoadPressed));
        actions.AddChild(MakeAction("Elimina", OnDeletePressed));
        actions.AddChild(MakeAction("Duplica Continua", OnDuplicateContinua));
        actions.AddChild(MakeAction("Indietro", Close));
    }

    private static Button MakeAction(string label, Action onClick)
    {
        var btn = new Button
        {
            Text = label,
            CustomMinimumSize = new Vector2(0, 36),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        btn.Pressed += onClick;
        return btn;
    }

    private void Refresh()
    {
        if (_list is null)
        {
            return;
        }

        foreach (var child in _list.GetChildren())
        {
            child.QueueFree();
        }

        var slots = FactorySliceSaveStore.ListSlots();
        if (slots.Count == 0)
        {
            var empty = new Label
            {
                Text = "Nessun salvataggio. Usa F5 in partita o Continua dopo la prima sessione.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            empty.AddThemeColorOverride("font_color", TextMuted);
            _list.AddChild(empty);
            if (_status is not null)
            {
                _status.Text = "";
            }

            return;
        }

        foreach (var slot in slots)
        {
            var id = slot.Id;
            var row = new Button
            {
                Text = FormatRow(slot),
                CustomMinimumSize = new Vector2(0, 40),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Alignment = HorizontalAlignment.Left
            };
            row.Modulate = id == _selectedId ? new Color(1f, 0.95f, 0.7f) : Colors.White;
            row.Pressed += () =>
            {
                _selectedId = id;
                _deleteArmedId = null;
                Refresh();
            };
            _list.AddChild(row);
        }

        if (_status is not null)
        {
            _status.Text = _selectedId is null
                ? $"{slots.Count} slot"
                : _deleteArmedId == _selectedId
                    ? $"Conferma Elimina su «{_selectedId}»"
                    : $"Selezionato: {_selectedId}";
        }
    }

    private static string FormatRow(SaveSlotInfo slot)
    {
        var when = slot.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        var campaign = string.IsNullOrWhiteSpace(slot.ActiveCampaignLevelId)
            ? "sandbox"
            : slot.ActiveCampaignLevelId;
        var label = slot.Id == FactorySliceSaveStore.ContinueSlotId
            ? "Continua"
            : slot.Id;
        var map = slot.MapWidth > 0 && slot.MapHeight > 0
            ? $"  ·  {slot.MapWidth}×{slot.MapHeight}"
            : "";
        return $"{label}  ·  ${slot.Money}  ·  {campaign}{map}  ·  {when}";
    }

    private void OnLoadPressed()
    {
        if (string.IsNullOrWhiteSpace(_selectedId))
        {
            if (_status is not null)
            {
                _status.Text = "Seleziona uno slot da caricare.";
            }

            return;
        }

        SlotLoadRequested?.Invoke(_selectedId);
    }

    private void OnDeletePressed()
    {
        if (string.IsNullOrWhiteSpace(_selectedId))
        {
            if (_status is not null)
            {
                _status.Text = "Seleziona uno slot da eliminare.";
            }

            return;
        }

        if (_deleteArmedId != _selectedId)
        {
            _deleteArmedId = _selectedId;
            Refresh();
            return;
        }

        FactorySliceSaveStore.Delete(_selectedId);
        _deleteArmedId = null;
        _selectedId = null;
        Refresh();
        if (_status is not null)
        {
            _status.Text = "Slot eliminato.";
        }
    }

    private void OnDuplicateContinua()
    {
        var id = FactorySliceSaveStore.DuplicateSlot(FactorySliceSaveStore.ContinueSlotId);
        if (id is null)
        {
            if (_status is not null)
            {
                _status.Text = "Nessuna Continua da duplicare.";
            }

            return;
        }

        _selectedId = id;
        Refresh();
        if (_status is not null)
        {
            _status.Text = $"Creato {id}";
        }
    }
}
