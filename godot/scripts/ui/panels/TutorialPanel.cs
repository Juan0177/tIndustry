using Godot;

namespace TIndustry.Godot;

/// <summary>
/// First-run tutorial: a non-modal card in the bottom-left corner. The game stays playable while it is
/// open; each step advances on its own when the world reports the action done (<see cref="CompleteStep"/>),
/// or manually with Avanti. Backspace skips · persists via <see cref="ClientSettings.TutorialCompleted"/>.
/// </summary>
public partial class TutorialPanel : Control
{
    private static readonly Color CardBg = new(0.08f, 0.10f, 0.11f, 0.94f);
    private static readonly Color Accent = new(0.92f, 0.78f, 0.28f, 1f);
    private static readonly Color TextPrimary = new(0.93f, 0.95f, 0.90f, 1f);
    private static readonly Color TextMuted = new(0.68f, 0.74f, 0.68f, 1f);
    private static readonly Color Done = new(0.55f, 0.85f, 0.50f, 1f);

    /// <summary>Step ids the world checks against (see World.SyncTutorial).</summary>
    public enum Step
    {
        Camera,
        Palette,
        Miner,
        BeltToCore,
        Mercato,
        Ricerca,
        Forno,
        Impostazioni
    }

    /// <summary>Text per <see cref="Step"/>; the last line of each is the goal that auto-advances it.</summary>
    public static readonly string[] Steps =
    [
        "Muovi la visuale: WASD, rotella premuta, oppure trascina col tasto sinistro (in modalità cursore). Rotella = zoom · H torna al CORE.",
        "La palette è in basso a destra: Lo = Logistica, Pr = Produzione, Po = Potenza. Scegli uno strumento (o premi 1–9). Esc torna al cursore.",
        "Premi 2 (Minatore) e piazzalo su un giacimento di ferro (rocce grigie) vicino al CORE. R ruota l'uscita. Il tuo drone costruisce ciò che pianifichi: guardalo volare e disegnare il blocco.",
        "Premi 1 (Nastro) e trascina una linea dall'uscita del minatore fino al CORE: i minerali entrano in magazzino.",
        "Premi M per aprire il MERCATO: vendi minerali e lastre, oppure attiva la Vendita automatica.",
        "Premi T per aprire la RICERCA: con soldi e lastre sblocchi il FORNO e gli altri edifici.",
        "Piazza un FORNO (3): fonde i minerali in lastre. Serve carbone OPPURE corrente (generatore + nodi potenza, +20%).",
        "IMPOSTAZIONI (I, o dalla Home): scala UI, VSync e Rivedi tutorial. Premi Fine per chiudere."
    ];

    private PanelContainer? _card;
    private Label? _stepLabel;
    private Label? _bodyLabel;
    private Label? _doneLabel;
    private Button? _nextBtn;
    private int _step;
    private bool _suppressed;
    private ClientSettings? _settings;

    public event Action? Completed;
    public event Action? Skipped;

    public override void _Ready()
    {
        Name = "TutorialPanel";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // Only the card takes clicks: the world underneath stays playable.
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        BuildUi();
    }

    public bool IsOpen => Visible;

    public Step CurrentStep => (Step)_step;

    public void Open(ClientSettings settings, int startStep = 0)
    {
        _settings = settings;
        _step = Math.Clamp(startStep, 0, Steps.Length - 1);
        Visible = true;
        Refresh();
    }

    public void Close() => Visible = false;

    /// <summary>Hide the card while a full-screen panel (Home, Ricerca, Mercato…) is on top.</summary>
    public void SetSuppressed(bool suppressed)
    {
        _suppressed = suppressed;
        if (_card is not null)
        {
            _card.Visible = !suppressed;
        }
    }

    /// <summary>Called by the world when the player has done what <paramref name="step"/> asks.</summary>
    public void CompleteStep(Step step)
    {
        if (!Visible || (int)step != _step || _doneLabel?.Visible == true)
        {
            return;
        }

        AdvanceDone();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || _suppressed)
        {
            return;
        }

        if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode == Key.Backspace)
            {
                Skip();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter)
            {
                Advance();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void BuildUi()
    {
        _card = new PanelContainer { Name = "TutorialCard", MouseFilter = MouseFilterEnum.Stop };
        // Bottom-left: the only corner free of HUD (stock top-right, palette bottom-right, objectives top-left).
        _card.SetAnchorsPreset(LayoutPreset.BottomLeft);
        _card.OffsetLeft = 16;
        _card.OffsetBottom = -16;
        _card.GrowVertical = GrowDirection.Begin;
        _card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
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
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 10,
            ContentMarginBottom = 10
        });
        AddChild(_card);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 6);
        _card.AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);

        _stepLabel = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _stepLabel.AddThemeColorOverride("font_color", Accent);
        _stepLabel.AddThemeFontSizeOverride("font_size", 13);
        header.AddChild(_stepLabel);

        _doneLabel = new Label { Text = "✓ fatto", Visible = false };
        _doneLabel.AddThemeColorOverride("font_color", Done);
        _doneLabel.AddThemeFontSizeOverride("font_size", 13);
        header.AddChild(_doneLabel);

        _bodyLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(440, 0)
        };
        _bodyLabel.AddThemeColorOverride("font_color", TextPrimary);
        _bodyLabel.AddThemeFontSizeOverride("font_size", 15);
        root.AddChild(_bodyLabel);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        root.AddChild(row);

        var hint = new Label
        {
            Text = "Avanza da solo quando completi il passo · Invio = avanti",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        hint.AddThemeColorOverride("font_color", TextMuted);
        hint.AddThemeFontSizeOverride("font_size", 11);
        row.AddChild(hint);

        var skip = new Button { Text = "Salta tutorial", CustomMinimumSize = new Vector2(120, 30), FocusMode = FocusModeEnum.None };
        skip.Pressed += Skip;
        row.AddChild(skip);

        _nextBtn = new Button { Text = "Avanti", CustomMinimumSize = new Vector2(96, 30), FocusMode = FocusModeEnum.None };
        _nextBtn.Pressed += Advance;
        row.AddChild(_nextBtn);
    }

    private void Refresh()
    {
        if (_stepLabel is not null)
        {
            _stepLabel.Text = $"Tutorial {_step + 1}/{Steps.Length}";
        }

        if (_bodyLabel is not null)
        {
            _bodyLabel.Text = Steps[_step];
        }

        if (_nextBtn is not null)
        {
            _nextBtn.Text = _step >= Steps.Length - 1 ? "Fine" : "Avanti";
        }
    }

    private void Advance()
    {
        if (_step >= Steps.Length - 1)
        {
            Finish(completed: true);
            return;
        }

        _step++;
        Refresh();
    }

    /// <summary>Auto-complete: flash "✓ fatto" on the finished step, then move on.</summary>
    private async void AdvanceDone()
    {
        var from = _step;
        if (_doneLabel is not null)
        {
            _doneLabel.Visible = true;
        }

        await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
        if (_doneLabel is not null)
        {
            _doneLabel.Visible = false;
        }

        // Player may have pressed Avanti / closed meanwhile.
        if (Visible && _step == from)
        {
            Advance();
        }
    }

    private void Skip() => Finish(completed: false);

    private void Finish(bool completed)
    {
        if (_settings is not null)
        {
            _settings.TutorialCompleted = true;
            _settings.Save();
        }

        Close();
        if (completed)
        {
            Completed?.Invoke();
        }
        else
        {
            Skipped?.Invoke();
        }
    }
}
