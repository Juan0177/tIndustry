using Godot;

namespace TIndustry.Godot;

/// <summary>
/// First-run tutorial: a non-modal card in the bottom-left corner. The game stays playable while it is
/// open; each step advances on its own when the world reports the action done (<see cref="CompleteStep"/>),
/// or manually with Avanti. Backspace skips · persists via <see cref="ClientSettings.TutorialCompleted"/>.
/// </summary>
public partial class TutorialPanel : Control
{
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
        "Premi 2 (Minatore) e piazzalo su un giacimento di ferro (sassolini grigio-azzurri) vicino al CORE. R ruota l'uscita. Il tuo drone costruisce ciò che pianifichi: guardalo volare e disegnare il blocco.",
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

    /// <summary>Widest text column of the card, UI units (it narrows to stay clear of the build palette).</summary>
    private const float BodyMaxWidth = 720f;

    /// <summary>Narrowest text column, UI units (the scaled window is very small).</summary>
    private const float BodyMinWidth = 300f;

    /// <summary>Gap between the card, the window edge and the build palette, UI units.</summary>
    private const float Gap = 8f;

    private void BuildUi()
    {
        // Compact card in the bottom-left corner (the HUD's free corner: menu, stock and objectives top-left, palette
        // bottom-right): step and buttons on one row, the text on as few lines as the room beside the palette allows.
        _card = new PanelContainer { Name = "TutorialCard", MouseFilter = MouseFilterEnum.Stop };
        _card.SetAnchorsPreset(LayoutPreset.BottomLeft);
        _card.OffsetLeft = Gap;
        _card.OffsetBottom = -Gap;
        _card.GrowVertical = GrowDirection.Begin;
        AddChild(_card);

        var root = new VBoxContainer();
        _card.AddChild(root);

        var header = new HBoxContainer { ThemeTypeVariation = UiTheme.RigaLarga };
        root.AddChild(header);

        _stepLabel = new Label { VerticalAlignment = VerticalAlignment.Center, ThemeTypeVariation = UiTheme.TestoAccento };
        header.AddChild(_stepLabel);

        _doneLabel = new Label
        {
            Text = "✓ fatto",
            Visible = false,
            VerticalAlignment = VerticalAlignment.Center,
            ThemeTypeVariation = UiTheme.TestoVerde
        };
        header.AddChild(_doneLabel);

        header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

        var skip = new Button { Text = "Salta tutorial", FocusMode = FocusModeEnum.None, ThemeTypeVariation = UiTheme.PulsantePiatto };
        skip.Pressed += Skip;
        header.AddChild(skip);

        _nextBtn = new Button
        {
            Text = "Avanti",
            CustomMinimumSize = new Vector2(80, 0),
            TooltipText = "Il passo avanza da solo quando lo completi · Invio = avanti",
            FocusMode = FocusModeEnum.None,
            ThemeTypeVariation = UiTheme.PulsantePrimario
        };
        _nextBtn.Pressed += Advance;
        header.AddChild(_nextBtn);

        _bodyLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        root.AddChild(_bodyLabel);

        Resized += FitBody;
        FitBody();
    }

    /// <summary>The text column takes the room left of the build palette, up to <see cref="BodyMaxWidth"/>.</summary>
    private void FitBody()
    {
        if (_bodyLabel is null || _card is null)
        {
            return;
        }

        var frame = _card.GetThemeStylebox("panel").GetMinimumSize().X;
        // Window less the palette stack (and its 8 px edge), the card's edge gap, the gap to the palette, the frame.
        var room = Size.X - Hud.CornerStackWidth - 8f - 2 * Gap - frame;
        var width = MathF.Floor(Math.Clamp(room, BodyMinWidth, BodyMaxWidth));
        _bodyLabel.CustomMinimumSize = new Vector2(width, 0);
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
