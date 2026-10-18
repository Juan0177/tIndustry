using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Gestione salvataggi (Raylib parity): lista slot, Carica / Elimina (confirm) /
/// Duplica Continua / Salva come / tastiera ↑↓.
/// </summary>
public partial class SavesPanel : Control
{
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
        var dim = new ColorRect { Color = UiColors.Oscuramento, MouseFilter = MouseFilterEnum.Stop };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var card = new PanelContainer { Name = "SavesCard", ThemeTypeVariation = UiTheme.Scheda };
        AddChild(card);
        CardLayout.Center(card, new Vector2(760, 540));

        var root = new VBoxContainer { ThemeTypeVariation = UiTheme.ColonnaLarga };
        card.AddChild(root);

        var header = new HBoxContainer { ThemeTypeVariation = UiTheme.RigaAmpia, Alignment = BoxContainer.AlignmentMode.Center };
        root.AddChild(header);
        var icon = HudStyle.MakeIcon(UiTheme.Icon("salvataggi"), 32, pixel: true);
        icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(icon);
        header.AddChild(new Label { Text = "Gestione salvataggi", ThemeTypeVariation = UiTheme.Titolo });

        _hint = new Label
        {
            Text = "↑↓ seleziona · Invio carica · Elimina (due click) · Duplica Continua · Salva come.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = UiTheme.TestoTenue
        };
        root.AddChild(_hint);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 120),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        root.AddChild(scroll);

        _list = new VBoxContainer();
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_list);

        _status = new Label { HorizontalAlignment = HorizontalAlignment.Center, ThemeTypeVariation = UiTheme.TestoTenue };
        root.AddChild(_status);

        // The actions wrap to a second row when the scaled window is narrow.
        var actions = new HFlowContainer();
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

        _slots = FactorySaveStore.ListSlots();
        if (_slots.Count == 0)
        {
            var empty = new Label
            {
                Text = "Nessun salvataggio. F5 in partita, Continua, o Salva come dopo la prima sessione.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center,
                ThemeTypeVariation = UiTheme.TestoTenue
            };
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
                CustomMinimumSize = new Vector2(0, 0),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Alignment = HorizontalAlignment.Left,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                ThemeTypeVariation = id == _selectedId ? UiTheme.PulsanteRigaScelta : UiTheme.PulsanteRiga
            };
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

    /// <summary>Display name of a campaign map file (the level that uses it); the id itself when unset or unknown.</summary>
    public Func<string, string?>? MapName { get; set; }

    private string FormatRow(SaveSlotInfo slot)
    {
        var when = slot.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        var label = slot.Id == FactorySaveStore.ContinueSlotId
            ? "Continua (salvataggio automatico)"
            : slot.Id;
        // Two lines, so a row fits a narrow card: name and funds, then map and date.
        return $"{label} · ${slot.Money}\n{FormatMap(slot)} · {when}";
    }

    /// <summary>
    /// "Biomi · 1500 · seme N" for a normal game, the map's name for a campaign map file, "Classico · W×H · seme N"
    /// for the classic generator (also files that do not name one: v11 saves load on it).
    /// </summary>
    private string FormatMap(SaveSlotInfo slot)
    {
        var level = string.IsNullOrWhiteSpace(slot.ActiveCampaignLevelId) ? "" : $" · {slot.ActiveCampaignLevelId}";
        if (slot.Generator == FactorySaveData.CampaignGenerator && !string.IsNullOrWhiteSpace(slot.MapId))
        {
            return $"Mappa {MapName?.Invoke(slot.MapId) ?? slot.MapId} · {slot.MapWidth}×{slot.MapHeight}";
        }

        if (MapGenerator.IsName(slot.Generator))
        {
            return $"Biomi · {slot.MapWidth} · seme {slot.Seed}";
        }

        if (slot.MapWidth <= 0 || slot.MapHeight <= 0)
        {
            return $"senza terreno{level}";
        }

        return $"Classico · {slot.MapWidth}×{slot.MapHeight} · seme {slot.Seed}{level}";
    }

    /// <summary>Shows a message on the status line (e.g. a failed load) without closing the panel.</summary>
    public void ShowStatus(string message)
    {
        if (_status is not null)
        {
            _status.Text = message;
        }
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

        FactorySaveStore.Delete(_selectedId);
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
        var id = FactorySaveStore.DuplicateSlot(FactorySaveStore.ContinueSlotId);
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

    /// <summary>Called by World after a successful Salva come snapshot.</summary>
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
