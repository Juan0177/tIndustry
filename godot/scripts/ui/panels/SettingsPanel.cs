using Godot;

namespace TIndustry.Godot;

/// <summary>
/// Impostazioni overlay: graphics (effects, shadows, glow, building animations, FPS cap, VSync, render scale) and
/// interface (UI scale, FPS / resource overlays, replay tutorial). The options scroll inside the card; Applica saves
/// and applies everything at once, without a restart. Controls carry stable node names (used by the playtest).
/// </summary>
public partial class SettingsPanel : Control
{
    private static readonly Color Dim = new(0.04f, 0.05f, 0.05f, 0.82f);
    private static readonly Color CardBg = new(0.08f, 0.10f, 0.11f, 0.96f);
    private static readonly Color Accent = new(0.92f, 0.78f, 0.28f, 1f);
    private static readonly Color TextPrimary = new(0.93f, 0.95f, 0.90f, 1f);
    private static readonly Color TextMuted = new(0.68f, 0.74f, 0.68f, 1f);
    private static readonly Color Selected = new(1f, 0.95f, 0.55f, 1f);

    private ClientSettings? _live;
    private ClientSettings? _draft;
    private Label? _statusLabel;
    // Every check button with how it reads/writes the draft, and every row of choice buttons with the value each
    // button stands for and the draft's current value: SyncControls redraws both from the draft.
    private readonly List<(CheckButton Button, Func<ClientSettings, bool> Get)> _checks = [];
    private readonly List<(Button Button, int Value, Func<ClientSettings, int> Get)> _choices = [];

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
        card.OffsetTop = -260;
        card.OffsetRight = 240;
        card.OffsetBottom = 260;
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
        root.AddThemeConstantOverride("separation", 10);
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
            Text = "Grafica e interfaccia. Applica salva su disco e vale subito.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        blurb.AddThemeColorOverride("font_color", TextMuted);
        blurb.AddThemeFontSizeOverride("font_size", 12);
        root.AddChild(blurb);

        // The options scroll; title, status and the Chiudi / Applica row stay in place.
        var scroll = new ScrollContainer
        {
            Name = "Opzioni",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        root.AddChild(scroll);

        var options = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        options.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(options);

        AddSection(options, "Grafica");
        AddChoices(options, "Effetti", "Effetti",
            [("Alti", (int)EffectsQuality.Alti), ("Bassi", (int)EffectsQuality.Bassi), ("Spenti", (int)EffectsQuality.Spenti)],
            s => (int)s.Effetti, (s, v) => s.Effetti = (EffectsQuality)v);
        AddCheck(options, "Ombre", "Ombre sotto gli edifici", s => s.Ombre, (s, on) => s.Ombre = on);
        AddCheck(options, "Bagliore", "Bagliore", s => s.Bagliore, (s, on) => s.Bagliore = on);
        AddCheck(options, "AnimazioniBlocchi", "Animazioni dei blocchi", s => s.AnimazioniBlocchi, (s, on) => s.AnimazioniBlocchi = on);
        AddChoices(options, "LimiteFps", "Limite FPS",
            ClientSettings.FpsLimitPresets.Select(v => (ClientSettings.FpsLimitLabel(v), v)).ToArray(),
            s => s.LimiteFps, (s, v) => s.LimiteFps = v);
        AddCheck(options, "VSync", "VSync", s => s.VSync, (s, on) => s.VSync = on);
        AddChoices(options, "ScalaRendering", "Scala di rendering (mondo)",
            ClientSettings.RenderScalePresets.Select(v => ($"{v}%", v)).ToArray(),
            s => s.ScalaRendering, (s, v) => s.ScalaRendering = v);

        AddSection(options, "Interfaccia");
        AddChoices(options, "ScalaUi", "Scala UI",
            ClientSettings.UiScalePresets.Select(v => (ClientSettings.UiScaleLabel(v), v)).ToArray(),
            s => s.UiScalePercent, (s, v) => s.UiScalePercent = v);
        AddCheck(options, "MostraFps", "Mostra FPS", s => s.ShowFps, (s, on) => s.ShowFps = on);
        AddCheck(options, "MostraRisorse", "Mostra risorse sistema (CPU · GPU · RAM)",
            s => s.ShowResourceOverlay, (s, on) => s.ShowResourceOverlay = on);

        var replay = new Button
        {
            Name = "RivediTutorial",
            Text = "Rivedi tutorial",
            CustomMinimumSize = new Vector2(0, 38)
        };
        replay.Pressed += OnReplayTutorial;
        options.AddChild(replay);

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
            Name = "Chiudi",
            Text = "Chiudi",
            CustomMinimumSize = new Vector2(0, 40),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        cancel.Pressed += Close;
        row.AddChild(cancel);

        var apply = new Button
        {
            Name = "Applica",
            Text = "Applica",
            CustomMinimumSize = new Vector2(0, 40),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        apply.Pressed += Apply;
        row.AddChild(apply);
    }

    private static void AddSection(VBoxContainer parent, string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", Accent);
        // 14, not 15, to keep the playtest captures stable; not a real fix. Labels rasterize their glyphs into the
        // shared font atlas of their size even while hidden, and the HUD draws with LinearWithMipmaps
        // (World.UseSmoothUiFiltering): new glyphs in the size-15 atlas changed its mipmaps and so the pixels of the
        // toast (size 15). Any new text at a size the captured HUD uses can do the same. Deferred to G4, whose theme
        // replaces fonts and UI filtering.
        label.AddThemeFontSizeOverride("font_size", 14);
        parent.AddChild(label);
    }

    /// <summary>A check button named <paramref name="name"/> bound to one boolean of the draft.</summary>
    private void AddCheck(VBoxContainer parent, string name, string text,
        Func<ClientSettings, bool> get, Action<ClientSettings, bool> set)
    {
        var check = new CheckButton { Name = name, Text = text };
        check.Toggled += on =>
        {
            if (_draft is not null)
            {
                set(_draft, on);
            }
        };
        parent.AddChild(check);
        _checks.Add((check, get));
    }

    /// <summary>A label and a row of buttons named "<paramref name="name"/>_&lt;value&gt;", one per choice.</summary>
    private void AddChoices(VBoxContainer parent, string name, string text, (string Label, int Value)[] choices,
        Func<ClientSettings, int> get, Action<ClientSettings, int> set)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", TextPrimary);
        label.AddThemeFontSizeOverride("font_size", 13);
        parent.AddChild(label);

        var row = new HBoxContainer { Name = name };
        row.AddThemeConstantOverride("separation", 8);
        parent.AddChild(row);
        foreach (var (choiceLabel, value) in choices)
        {
            var button = new Button
            {
                Name = $"{name}_{choiceLabel.TrimEnd('%')}",
                Text = choiceLabel,
                CustomMinimumSize = new Vector2(0, 32),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            button.Pressed += () =>
            {
                if (_draft is null)
                {
                    return;
                }

                set(_draft, value);
                _draft.Normalize();
                SyncControls();
            };
            row.AddChild(button);
            _choices.Add((button, value, get));
        }
    }

    private void SyncControls()
    {
        if (_draft is null)
        {
            return;
        }

        foreach (var (button, get) in _checks)
        {
            button.SetPressedNoSignal(get(_draft));
        }

        foreach (var (button, value, get) in _choices)
        {
            button.Modulate = get(_draft) == value ? Selected : Colors.White;
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
                $"Salvato · Effetti {_live.Effetti} · UI {ClientSettings.UiScaleLabel(_live.UiScalePercent)} · VSync {(_live.VSync ? "ON" : "OFF")}";
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
