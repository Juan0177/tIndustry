using Godot;

namespace TIndustry.Godot;

/// <summary>
/// Impostazioni overlay: graphics (effects, shadows, glow, building animations, FPS cap, VSync, render scale) and
/// interface (language, UI scale, FPS / resource overlays, replay tutorial). The options scroll inside the card; Applica saves
/// and applies everything at once, without a restart. Controls carry stable node names (used by the playtest).
/// </summary>
public partial class SettingsPanel : Control
{
    private ClientSettings? _live;
    private ClientSettings? _draft;
    private Label? _statusLabel;
    // Every check button with how it reads/writes the draft, and every row of choice buttons with the value each
    // button stands for and the draft's current value: SyncControls redraws both from the draft.
    private readonly List<(CheckButton Button, Func<ClientSettings, bool> Get)> _checks = [];
    private readonly List<(Button Button, int Value, Func<ClientSettings, int> Get)> _choices = [];
    private HSlider? _uiScale;
    private Label? _uiScaleLabel;

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
            _statusLabel.Text = "impostazioni.stato";
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
            Color = UiColors.Oscuramento,
            MouseFilter = MouseFilterEnum.Stop
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var card = new PanelContainer { Name = "SettingsCard", ThemeTypeVariation = UiTheme.Scheda };
        AddChild(card);
        CardLayout.Center(card, new Vector2(540, 560));

        var root = new VBoxContainer { ThemeTypeVariation = UiTheme.ColonnaLarga };
        card.AddChild(root);

        var title = new Label
        {
            Text = "home.impostazioni",
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = UiTheme.Titolo
        };
        root.AddChild(title);

        var blurb = new Label
        {
            Text = "impostazioni.descrizione",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = UiTheme.TestoTenue
        };
        root.AddChild(blurb);

        // The options scroll; title, status and the Chiudi / Applica row stay in place.
        var scroll = new ScrollContainer
        {
            Name = "Opzioni",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        root.AddChild(scroll);

        var clear = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, ThemeTypeVariation = UiTheme.MargineBarra };
        scroll.AddChild(clear);
        var options = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, ThemeTypeVariation = UiTheme.ColonnaLarga };
        clear.AddChild(options);

        AddSection(options, "impostazioni.grafica");
        AddChoices(options, "Effetti", "impostazioni.effetti",
            [
                ("Alti", "impostazioni.effetti.alti", (int)EffectsQuality.Alti),
                ("Bassi", "impostazioni.effetti.bassi", (int)EffectsQuality.Bassi),
                ("Spenti", "impostazioni.effetti.spenti", (int)EffectsQuality.Spenti)
            ],
            s => (int)s.Effetti, (s, v) => s.Effetti = (EffectsQuality)v);
        AddCheck(options, "Ombre", "impostazioni.ombre", s => s.Ombre, (s, on) => s.Ombre = on);
        AddCheck(options, "Bagliore", "impostazioni.bagliore", s => s.Bagliore, (s, on) => s.Bagliore = on);
        AddCheck(options, "AnimazioniBlocchi", "impostazioni.animazioni", s => s.AnimazioniBlocchi, (s, on) => s.AnimazioniBlocchi = on)
            .TooltipText = "impostazioni.animazioni.suggerimento";
        AddChoices(options, "LimiteFps", "impostazioni.fps.limite",
            ClientSettings.FpsLimitPresets
                .Select(v => (v == 0 ? "Nessuno" : v.ToString(System.Globalization.CultureInfo.InvariantCulture), ClientSettings.FpsLimitLabel(v), v))
                .ToArray(),
            s => s.LimiteFps, (s, v) => s.LimiteFps = v);
        AddCheck(options, "VSync", "VSync", s => s.VSync, (s, on) => s.VSync = on);
        AddCheck(options, "SchermoIntero", "impostazioni.schermo.intero", s => s.SchermoInteroScelto, (s, on) => s.SchermoIntero = on);
        AddChoices(options, "ScalaRendering", "impostazioni.scala.rendering",
            ClientSettings.RenderScalePresets.Select(v => ($"{v}", $"{v}%", v)).ToArray(),
            s => s.ScalaRendering, (s, v) => s.ScalaRendering = v);

        AddSection(options, "impostazioni.interfaccia");
        // Each language is named in itself (lingua.<code>), whatever the current one.
        AddChoices(options, "Lingua", "impostazioni.lingua",
            Testi.Lingue.Select((code, i) => (code, $"lingua.{code}", i)).ToArray(),
            s => Array.IndexOf(Testi.Lingue, s.Lingua), (s, v) => s.Lingua = Testi.Lingue[v]);
        AddUiScaleSlider(options);
        AddCheck(options, "MostraFps", "impostazioni.mostra.fps", s => s.ShowFps, (s, on) => s.ShowFps = on);
        AddCheck(options, "MostraRisorse", "impostazioni.mostra.risorse",
            s => s.ShowResourceOverlay, (s, on) => s.ShowResourceOverlay = on);

        var replay = new Button
        {
            Name = "RivediTutorial",
            Text = "impostazioni.rivedi.tutorial",
            CustomMinimumSize = new Vector2(0, 38)
        };
        replay.Pressed += OnReplayTutorial;
        options.AddChild(replay);

        _statusLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = UiTheme.TestoTenue
        };
        root.AddChild(_statusLabel);

        var row = new HBoxContainer { ThemeTypeVariation = UiTheme.RigaLarga };
        root.AddChild(row);

        var cancel = new Button
        {
            Name = "Chiudi",
            Text = "comune.chiudi",
            CustomMinimumSize = new Vector2(0, 40),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        cancel.Pressed += Close;
        row.AddChild(cancel);

        var apply = new Button
        {
            Name = "Applica",
            Text = "impostazioni.applica",
            CustomMinimumSize = new Vector2(0, 40),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ThemeTypeVariation = UiTheme.PulsantePrimario
        };
        apply.Pressed += Apply;
        row.AddChild(apply);
    }

    private static void AddSection(VBoxContainer parent, string text)
    {
        parent.AddChild(new Label { Text = text, ThemeTypeVariation = UiTheme.TestoAccento });
    }

    /// <summary>A check button named <paramref name="name"/> bound to one boolean of the draft.</summary>
    private CheckButton AddCheck(VBoxContainer parent, string name, string text,
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
        return check;
    }

    /// <summary>A label and a row of buttons named "<paramref name="name"/>_&lt;id&gt;", one per choice.</summary>
    private void AddChoices(VBoxContainer parent, string name, string text, (string Id, string Label, int Value)[] choices,
        Func<ClientSettings, int> get, Action<ClientSettings, int> set)
    {
        parent.AddChild(new Label { Text = text });

        var row = new HBoxContainer { Name = name, ThemeTypeVariation = UiTheme.RigaLarga };
        parent.AddChild(row);
        foreach (var (id, choiceLabel, value) in choices)
        {
            var button = new Button
            {
                Name = $"{name}_{id}",
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

    /// <summary>
    /// "Scala UI" slider (node "ScalaUi", 75–200% in steps of 5) with its value; the hint names the scales where the
    /// pixel text is sharpest. Applied with the rest on Applica.
    /// </summary>
    private void AddUiScaleSlider(VBoxContainer parent)
    {
        _uiScaleLabel = new Label { Name = "ScalaUiValore" };
        parent.AddChild(_uiScaleLabel);
        _uiScale = new HSlider
        {
            Name = "ScalaUi",
            MinValue = ClientSettings.UiScaleMin,
            MaxValue = ClientSettings.UiScaleMax,
            Step = ClientSettings.UiScaleStep,
            TickCount = (ClientSettings.UiScaleMax - ClientSettings.UiScaleMin) / 25 + 1,
            TicksOnBorders = true,
            CustomMinimumSize = new Vector2(0, 24),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _uiScale.ValueChanged += value =>
        {
            if (_draft is null)
            {
                return;
            }

            _draft.UiScalePercent = ClientSettings.NormalizeUiScale((int)Math.Round(value));
            SyncUiScaleLabel();
        };
        parent.AddChild(_uiScale);
        parent.AddChild(new Label
        {
            Text = "impostazioni.scala.ui.nitido",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            ThemeTypeVariation = UiTheme.TestoTenue
        });
    }

    private void SyncUiScaleLabel()
    {
        if (_uiScaleLabel is not null && _draft is not null)
        {
            _uiScaleLabel.Text = Testi.F("impostazioni.scala.ui", ClientSettings.UiScaleLabel(_draft.UiScalePercent));
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationTranslationChanged)
        {
            SyncUiScaleLabel();
        }
    }

    /// <summary>Moves the UI scale slider as a player would (playtests); the draft follows.</summary>
    internal void SetUiScaleDraft(int percent) => _uiScale?.SetValue(percent);

    private void SyncControls()
    {
        if (_draft is null)
        {
            return;
        }

        _uiScale?.SetValueNoSignal(_draft.UiScalePercent);
        SyncUiScaleLabel();

        foreach (var (button, get) in _checks)
        {
            button.SetPressedNoSignal(get(_draft));
        }

        foreach (var (button, value, get) in _choices)
        {
            button.ThemeTypeVariation = get(_draft) == value ? UiTheme.PulsanteScelto : "";
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
            _statusLabel.Text = Testi.F("impostazioni.salvato",
                Testi.T($"impostazioni.effetti.{_live.Effetti.ToString().ToLowerInvariant()}"),
                ClientSettings.UiScaleLabel(_live.UiScalePercent),
                Testi.T(_live.VSync ? "comune.on" : "comune.off"));
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
