using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// v1 Ricerca overlay: Italian list of Godot-slice unlocks (not full graph).
/// </summary>
public partial class ResearchPanel : Control
{
    private static readonly Color PanelBg = new(0.08f, 0.10f, 0.11f, 0.96f);
    private static readonly Color RowUnlocked = new(0.22f, 0.38f, 0.26f, 1f);
    private static readonly Color RowAvailable = new(0.42f, 0.34f, 0.16f, 1f);
    private static readonly Color RowLocked = new(0.18f, 0.18f, 0.18f, 1f);
    private static readonly Color TextPrimary = new(0.93f, 0.95f, 0.90f, 1f);
    private static readonly Color TextMuted = new(0.70f, 0.76f, 0.70f, 1f);
    private static readonly Color Accent = new(0.92f, 0.82f, 0.42f, 1f);

    private FactorySlice? _slice;
    private VBoxContainer? _list;
    private Label? _detailTitle;
    private Label? _detailState;
    private Label? _detailCost;
    private Label? _detailPrereq;
    private Label? _walletLabel;
    private Button? _unlockButton;
    private string? _selectedId;
    private readonly Dictionary<string, PanelContainer> _rows = [];

    public event Action? Closed;
    public event Action? UnlockedChanged;

    public override void _Ready()
    {
        Name = "ResearchPanel";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        BuildUi();
    }

    public void Open(FactorySlice slice)
    {
        _slice = slice;
        Visible = true;
        _selectedId ??= FirstSelectableId();
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
            && (key.Keycode == Key.Escape || key.Keycode == Key.T))
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

        var panel = new PanelContainer
        {
            Name = "ResearchCard"
        };
        panel.SetAnchorsPreset(LayoutPreset.Center);
        panel.OffsetLeft = -420;
        panel.OffsetTop = -260;
        panel.OffsetRight = 420;
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
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 12,
            ContentMarginBottom = 12
        });
        AddChild(panel);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        panel.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        root.AddChild(header);

        var icon = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/research.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(28, 28)
        };
        header.AddChild(icon);

        var title = new Label { Text = "Ricerca", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeColorOverride("font_color", TextPrimary);
        title.AddThemeFontSizeOverride("font_size", 22);
        header.AddChild(title);

        _walletLabel = new Label { Text = "Magazzino: $0" };
        _walletLabel.AddThemeColorOverride("font_color", TextMuted);
        _walletLabel.AddThemeFontSizeOverride("font_size", 13);
        header.AddChild(_walletLabel);

        var closeBtn = new Button { Text = "Indietro · Esc" };
        closeBtn.Pressed += Close;
        header.AddChild(closeBtn);

        var hint = new Label
        {
            Text = "verde = sbloccato · ambra = disponibile · grigio = bloccato · T apre / Esc chiude"
        };
        hint.AddThemeColorOverride("font_color", TextMuted);
        hint.AddThemeFontSizeOverride("font_size", 12);
        root.AddChild(hint);

        var body = new HBoxContainer();
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        body.AddThemeConstantOverride("separation", 14);
        root.AddChild(body);

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(320, 360),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        body.AddChild(scroll);
        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 6);
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_list);

        var detail = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(340, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        detail.AddThemeConstantOverride("separation", 8);
        body.AddChild(detail);

        _detailTitle = new Label { Text = "—" };
        _detailTitle.AddThemeColorOverride("font_color", TextPrimary);
        _detailTitle.AddThemeFontSizeOverride("font_size", 18);
        detail.AddChild(_detailTitle);

        _detailState = new Label { Text = "" };
        _detailState.AddThemeColorOverride("font_color", Accent);
        detail.AddChild(_detailState);

        _detailCost = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _detailCost.AddThemeColorOverride("font_color", TextMuted);
        detail.AddChild(_detailCost);

        _detailPrereq = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _detailPrereq.AddThemeColorOverride("font_color", TextMuted);
        detail.AddChild(_detailPrereq);

        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        detail.AddChild(spacer);

        _unlockButton = new Button
        {
            Text = "Conferma sblocco",
            CustomMinimumSize = new Vector2(0, 40)
        };
        _unlockButton.Pressed += OnUnlockPressed;
        detail.AddChild(_unlockButton);
    }

    private string? FirstSelectableId()
    {
        if (_slice is null)
        {
            return null;
        }

        foreach (var id in ResearchState.GodotSliceStructureIds)
        {
            if (_slice.Content.FindStructure(id) is not null)
            {
                return id;
            }
        }

        return null;
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

        _rows.Clear();

        if (_walletLabel is not null)
        {
            _walletLabel.Text =
                $"Magazzino: ${_slice.Wallet.Money} · lastre {_slice.Wallet.MaterialCount("iron-plate")} · filo {_slice.Wallet.MaterialCount("copper-wire")}";
        }

        foreach (var id in ResearchState.GodotSliceStructureIds)
        {
            var structure = _slice.Content.FindStructure(id);
            if (structure is null)
            {
                continue;
            }

            var state = _slice.Research.GetNodeState(structure);
            var row = new PanelContainer
            {
                CustomMinimumSize = new Vector2(0, 44),
                MouseFilter = MouseFilterEnum.Stop
            };
            var bg = state switch
            {
                ResearchNodeState.Unlocked => RowUnlocked,
                ResearchNodeState.Available => RowAvailable,
                _ => RowLocked
            };
            var selected = string.Equals(id, _selectedId, StringComparison.Ordinal);
            row.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = bg,
                BorderColor = selected ? Accent : new Color(0.3f, 0.34f, 0.32f),
                BorderWidthLeft = selected ? 2 : 1,
                BorderWidthTop = selected ? 2 : 1,
                BorderWidthRight = selected ? 2 : 1,
                BorderWidthBottom = selected ? 2 : 1,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
                ContentMarginLeft = 10,
                ContentMarginRight = 10,
                ContentMarginTop = 6,
                ContentMarginBottom = 6
            });

            var label = new Label
            {
                Text = $"{structure.DisplayName}  ·  {StateIt(state)}",
                MouseFilter = MouseFilterEnum.Ignore
            };
            label.AddThemeColorOverride("font_color", TextPrimary);
            label.AddThemeFontSizeOverride("font_size", 14);
            row.AddChild(label);

            var captured = id;
            row.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                {
                    _selectedId = captured;
                    Refresh();
                }
            };

            _list.AddChild(row);
            _rows[id] = row;
        }

        RefreshDetail();
    }

    private void RefreshDetail()
    {
        if (_slice is null || _selectedId is null
            || _detailTitle is null || _detailState is null
            || _detailCost is null || _detailPrereq is null || _unlockButton is null)
        {
            return;
        }

        var structure = _slice.Content.FindStructure(_selectedId);
        if (structure is null)
        {
            return;
        }

        var state = _slice.Research.GetNodeState(structure);
        _detailTitle.Text = structure.DisplayName;
        _detailState.Text = StateIt(state);
        _detailCost.Text = FormatCost(structure, _slice);
        _detailPrereq.Text = FormatPrereqs(structure, _slice);

        _unlockButton.Disabled = state != ResearchNodeState.Available
            || !_slice.Research.CanUnlock(structure, _slice.Wallet);
        _unlockButton.Text = state switch
        {
            ResearchNodeState.Unlocked => "Già sbloccato",
            ResearchNodeState.Locked => "Prerequisiti mancanti",
            _ when !_slice.Research.CanUnlock(structure, _slice.Wallet) => "Risorse insufficienti",
            _ => "Conferma sblocco"
        };
    }

    private void OnUnlockPressed()
    {
        if (_slice is null || _selectedId is null)
        {
            return;
        }

        if (_slice.TryUnlockStructure(_selectedId))
        {
            UnlockedChanged?.Invoke();
            Refresh();
        }
    }

    private static string StateIt(ResearchNodeState state) => state switch
    {
        ResearchNodeState.Unlocked => "SBLOCCATO",
        ResearchNodeState.Available => "DISPONIBILE",
        _ => "BLOCCATO"
    };

    private static string FormatCost(StructureDefinition structure, FactorySlice slice)
    {
        if (structure.Unlock is null)
        {
            return "Costo: gratis";
        }

        var parts = new List<string>();
        if (structure.Unlock.Money > 0)
        {
            parts.Add($"${structure.Unlock.Money}");
        }

        foreach (var mat in structure.Unlock.Materials)
        {
            var name = slice.Content.DisplayName(mat.ItemId);
            parts.Add($"{mat.Amount} {name}");
        }

        return parts.Count == 0 ? "Costo: gratis" : "Costo: " + string.Join(" + ", parts);
    }

    private static string FormatPrereqs(StructureDefinition structure, FactorySlice slice)
    {
        if (structure.Requires.Count == 0)
        {
            return "Prerequisiti: nessuno";
        }

        var bits = structure.Requires.Select(id =>
        {
            var name = slice.Content.FindStructure(id)?.DisplayName ?? id;
            return slice.Research.IsUnlocked(id) ? name : $"{name} (manca)";
        });
        return "Prerequisiti: " + string.Join(", ", bits);
    }
}
