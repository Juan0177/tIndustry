using Godot;

namespace TIndustry.Godot;

/// <summary>
/// Impostazioni overlay: VSync, scala UI, overlay FPS, rivedi tutorial (Raylib thin port).
/// </summary>
public partial class SettingsPanel : Control
{
    private static readonly Color Dim = new(0.04f, 0.05f, 0.05f, 0.82f);
    private static readonly Color CardBg = new(0.08f, 0.10f, 0.11f, 0.96f);
    private static readonly Color Accent = new(0.92f, 0.78f, 0.28f, 1f);
    private static readonly Color TextPrimary = new(0.93f, 0.95f, 0.90f, 1f);
    private static readonly Color TextMuted = new(0.68f, 0.74f, 0.68f, 1f);

    private ClientSettings? _live;
    private ClientSettings? _draft;
    private CheckButton? _vsyncToggle;
    private CheckButton? _fpsToggle;
    private Label? _scaleLabel;
    private Label? _statusLabel;
    private HBoxContainer? _scaleRow;

    public event Action? Closed;
    public event Action? Applied;
    public event Action? ReplayTutorialRequested;

    public override void _Ready()
    {
        Name = "SettingsPanel";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        BuildUi();
    }

    public bool IsOpen => Visible;

    public void Open(ClientSettings settings)
    {
        _live = settings;
        _draft = settings.Clone();
        Visible = true;
        SyncControls();
        if (_statusLabel is not null)
        {
            _statusLabel.Text = "Modifica e premi Applica per salvare.";
        }
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
        var dim = new ColorRect
        {
            Color = Dim,
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var card = new PanelContainer { Name = "SettingsCard" };
        card.SetAnchorsPreset(LayoutPreset.Center);
        card.OffsetLeft = -240;
        card.OffsetTop = -220;
        card.OffsetRight = 240;
        card.OffsetBottom = 220;
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
            ContentMarginLeft = 22,
            ContentMarginRight = 22,
            ContentMarginTop = 18,
            ContentMarginBottom = 18
        });
        AddChild(card);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        card.AddChild(root);

        var title = new Label
        {
            Text = "Impostazioni",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeColorOverride("font_color", Accent);
        title.AddThemeFontSizeOverride("font_size", 22);
        root.AddChild(title);

        var blurb = new Label
        {
            Text = "Scala UI, VSync e overlay. Applica salva su disco.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        blurb.AddThemeColorOverride("font_color", TextMuted);
        blurb.AddThemeFontSizeOverride("font_size", 12);
        root.AddChild(blurb);

        _vsyncToggle = new CheckButton { Text = "VSync" };
        _vsyncToggle.Toggled += on =>
        {
            if (_draft is not null)
            {
                _draft.VSync = on;
            }
        };
        root.AddChild(_vsyncToggle);

        _fpsToggle = new CheckButton { Text = "Mostra FPS" };
        _fpsToggle.Toggled += on =>
        {
            if (_draft is not null)
            {
                _draft.ShowFps = on;
            }
        };
        root.AddChild(_fpsToggle);

        _scaleLabel = new Label { Text = "Scala UI" };
        _scaleLabel.AddThemeColorOverride("font_color", TextPrimary);
        _scaleLabel.AddThemeFontSizeOverride("font_size", 13);
        root.AddChild(_scaleLabel);

        _scaleRow = new HBoxContainer();
        _scaleRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(_scaleRow);
        foreach (var percent in ClientSettings.UiScalePresets)
        {
            var p = percent;
            var btn = new Button
            {
                Text = ClientSettings.UiScaleLabel(p),
                CustomMinimumSize = new Vector2(0, 34),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            btn.Pressed += () => SetScale(p);
            _scaleRow.AddChild(btn);
        }

        var replay = new Button
        {
            Text = "Rivedi tutorial",
            CustomMinimumSize = new Vector2(0, 38)
        };
        replay.Pressed += OnReplayTutorial;
        root.AddChild(replay);

        _statusLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _statusLabel.AddThemeColorOverride("font_color", TextMuted);
        _statusLabel.AddThemeFontSizeOverride("font_size", 12);
        root.AddChild(_statusLabel);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        root.AddChild(row);

        var cancel = new Button
        {
            Text = "Chiudi",
            CustomMinimumSize = new Vector2(0, 40),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        cancel.Pressed += Close;
        row.AddChild(cancel);

        var apply = new Button
        {
            Text = "Applica",
            CustomMinimumSize = new Vector2(0, 40),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        apply.Pressed += Apply;
        row.AddChild(apply);
    }

    private void SyncControls()
    {
        if (_draft is null)
        {
            return;
        }

        if (_vsyncToggle is not null)
        {
            _vsyncToggle.SetPressedNoSignal(_draft.VSync);
        }

        if (_fpsToggle is not null)
        {
            _fpsToggle.SetPressedNoSignal(_draft.ShowFps);
        }

        if (_scaleLabel is not null)
        {
            _scaleLabel.Text = $"Scala UI · attuale {ClientSettings.UiScaleLabel(_draft.UiScalePercent)}";
        }

        HighlightScaleButtons();
    }

    private void SetScale(int percent)
    {
        if (_draft is null)
        {
            return;
        }

        _draft.UiScalePercent = percent;
        _draft.Normalize();
        if (_scaleLabel is not null)
        {
            _scaleLabel.Text = $"Scala UI · attuale {ClientSettings.UiScaleLabel(_draft.UiScalePercent)}";
        }

        HighlightScaleButtons();
    }

    private void HighlightScaleButtons()
    {
        if (_scaleRow is null || _draft is null)
        {
            return;
        }

        foreach (var child in _scaleRow.GetChildren())
        {
            if (child is Button btn)
            {
                var selected = btn.Text == ClientSettings.UiScaleLabel(_draft.UiScalePercent);
                btn.Modulate = selected
                    ? new Color(1f, 0.95f, 0.55f, 1f)
                    : Colors.White;
            }
        }
    }

    private void Apply()
    {
        if (_live is null || _draft is null)
        {
            return;
        }

        _live.CopyFrom(_draft);
        _live.Save();
        _live.ApplyToEngine(GetTree().Root);
        if (_statusLabel is not null)
        {
            _statusLabel.Text =
                $"Salvato · UI {ClientSettings.UiScaleLabel(_live.UiScalePercent)} · VSync {(_live.VSync ? "ON" : "OFF")}";
        }

        Applied?.Invoke();
    }

    private void OnReplayTutorial()
    {
        if (_live is null)
        {
            return;
        }

        _live.TutorialCompleted = false;
        _live.Save();
        Close();
        ReplayTutorialRequested?.Invoke();
    }
}
