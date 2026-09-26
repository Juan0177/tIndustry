using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Gestione salvataggi (Raylib parity): lista slot, Carica / Elimina (confirm) /
/// Duplica Continua / Salva come / tastiera ↑↓.
/// </summary>
public partial class SavesPanel : Control
{
    private static readonly Color Dim = new(0.04f, 0.05f, 0.05f, 0.82f);
    private static readonly Color CardBg = new(0.08f, 0.10f, 0.11f, 0.96f);
    private static readonly Color Accent = new(0.92f, 0.78f, 0.28f, 1f);
    private static readonly Color TextMuted = new(0.68f, 0.74f, 0.68f, 1f);

    private VBoxContainer? _list;
    private Label? _hint;
    private Label? _status;
    private Button? _deleteButton;
    private string? _selectedId;
    private string? _deleteArmedId;
    private IReadOnlyList<SaveSlotInfo> _slots = [];

    public event Action? Closed;
    public event Action<string>? SlotLoadRequested;
    /// <summary>Request a live-session snapshot into a new timestamped slot (Salva come).</summary>
    public event Action? SaveAsRequested;

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

        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        if (key.Keycode == Key.Escape)
        {
            Close();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_slots.Count == 0)
        {
            return;
        }

        if (key.Keycode is Key.Up or Key.Down)
        {
            MoveSelection(key.Keycode == Key.Down ? 1 : -1);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter)
        {
            OnLoadPressed();
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
        card.OffsetLeft = -380;
        card.OffsetTop = -300;
        card.OffsetRight = 380;
        card.OffsetBottom = 300;
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
            Text = "↑↓ seleziona · Invio carica · Elimina (due click) · Duplica Continua · Salva come.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _hint.AddThemeColorOverride("font_color", TextMuted);
        _hint.AddThemeFontSizeOverride("font_size", 12);
        root.AddChild(_hint);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 300),
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
        _deleteButton = MakeAction("Elimina", OnDeletePressed);
        actions.AddChild(_deleteButton);
        actions.AddChild(MakeAction("Duplica Continua", OnDuplicateContinua));
        actions.AddChild(MakeAction("Salva come", OnSaveAsPressed));
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

    private void MoveSelection(int delta)
    {
        if (_slots.Count == 0)
        {
            return;
        }

        var idx = 0;
        if (!string.IsNullOrWhiteSpace(_selectedId))
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Id == _selectedId)
                {
                    idx = i;
                    break;
                }
            }
        }

        idx = (idx + delta + _slots.Count) % _slots.Count;
        _selectedId = _slots[idx].Id;
        _deleteArmedId = null;
        Refresh();
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

        _slots = FactorySliceSaveStore.ListSlots();
        if (_slots.Count == 0)
        {
            var empty = new Label
            {
                Text = "Nessun salvataggio. F5 in partita, Continua, o Salva come dopo la prima sessione.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            empty.AddThemeColorOverride("font_color", TextMuted);
            _list.AddChild(empty);
            if (_status is not null)
            {
                _status.Text = "";
            }

            SyncDeleteButton();
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedId)
            || !_slots.Any(s => s.Id == _selectedId))
        {
            _selectedId = _slots[0].Id;
        }

        foreach (var slot in _slots)
        {
            var id = slot.Id;
            var row = new Button
            {
                Text = FormatRow(slot),
                CustomMinimumSize = new Vector2(0, 42),
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
            _status.Text = _deleteArmedId == _selectedId
                ? $"Conferma Elimina su «{_selectedId}»"
                : $"Selezionato: {_selectedId} · {_slots.Count} slot";
        }

        SyncDeleteButton();
    }

    private void SyncDeleteButton()
    {
        if (_deleteButton is null)
        {
            return;
        }

        _deleteButton.Text = _deleteArmedId is not null && _deleteArmedId == _selectedId
            ? "Conferma elimina"
            : "Elimina";
    }

    private static string FormatRow(SaveSlotInfo slot)
    {
        var when = slot.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        var campaign = string.IsNullOrWhiteSpace(slot.ActiveCampaignLevelId)
            ? "sandbox"
            : slot.ActiveCampaignLevelId;
        var label = slot.Id == FactorySliceSaveStore.ContinueSlotId
            ? "Continua (salvataggio automatico)"
            : slot.Id;
        var map = slot.MapWidth > 0 && slot.MapHeight > 0
            ? $"  ·  {slot.MapWidth}×{slot.MapHeight}"
            : "";
        var seed = slot.Seed != 0 ? $"  ·  seed {slot.Seed}" : "";
        return $"{label}  ·  ${slot.Money}{seed}{map}  ·  {campaign}  ·  {when}";
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

    private void OnSaveAsPressed()
    {
        SaveAsRequested?.Invoke();
    }

    /// <summary>Called by SpikeWorld after a successful Salva come snapshot.</summary>
    public void NotifySavedAs(string slotId)
    {
        _selectedId = slotId;
        _deleteArmedId = null;
        Refresh();
        if (_status is not null)
        {
            _status.Text = $"Salvato come {slotId}";
        }
    }
}
