using Godot;

namespace TIndustry.Godot;

/// <summary>
/// Thin first-run tutorial overlay (Raylib parity, 8 IT steps — not the full 14).
/// Avanti / Salta · Backspace skips · persists via <see cref="ClientSettings.TutorialCompleted"/>.
/// </summary>
public partial class TutorialPanel : Control
{
    private static readonly Color Dim = new(0.04f, 0.05f, 0.05f, 0.55f);
    private static readonly Color CardBg = new(0.08f, 0.10f, 0.11f, 0.97f);
    private static readonly Color Accent = new(0.92f, 0.78f, 0.28f, 1f);
    private static readonly Color TextPrimary = new(0.93f, 0.95f, 0.90f, 1f);
    private static readonly Color TextMuted = new(0.68f, 0.74f, 0.68f, 1f);

    /// <summary>Godot-adapted steps (camera → place → sell → research → power → settings).</summary>
    public static readonly string[] Steps =
    [
        "Camera: trascina col mouse (o WASD), rotella = zoom. H torna al CORE.",
        "Dock in basso: scegli Nastro / Minatore / Forno. Esc = cursore; Esc di nuovo = Home.",
        "Piazza un MINATORE (tasto 2) sul giacimento di ferro a ovest del CORE.",
        "Nastri (1): R ruota. Collega l'uscita del minatore fino al CORE — i minerali entrano in magazzino.",
        "MERCATO (M): vendi ore/lastre, oppure attiva Vendita automatica. Soldi = ricerca e edifici.",
        "RICERCA (T): sblocca FORNO e altri pezzi. I costi di piazzamento usano lastre/fili dal magazzino.",
        "FORNO: serve carbone O corrente (+20% se alimentato). GENERATORE brucia carbone; nodi potenza espandono la rete.",
        "IMPOSTAZIONI (I o Home): scala UI, VSync, rivedi tutorial. Backspace salta questo tutorial."
    ];

    private Label? _stepLabel;
    private Label? _bodyLabel;
    private Button? _nextBtn;
    private int _step;
    private ClientSettings? _settings;

    public event Action? Completed;
    public event Action? Skipped;

    public override void _Ready()
    {
        Name = "TutorialPanel";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        BuildUi();
    }

    public bool IsOpen => Visible;

    public void Open(ClientSettings settings, int startStep = 0)
    {
        _settings = settings;
        _step = Math.Clamp(startStep, 0, Steps.Length - 1);
        Visible = true;
        Refresh();
    }

    public void Close() => Visible = false;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
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

            if (key.Keycode == Key.Enter || key.Keycode == Key.Space)
            {
                Advance();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void BuildUi()
    {
        var dim = new ColorRect
        {
            Color = Dim,
            MouseFilter = MouseFilterEnum.Ignore
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var card = new PanelContainer { Name = "TutorialCard" };
        card.SetAnchorsPreset(LayoutPreset.BottomWide);
        card.OffsetLeft = 24;
        card.OffsetRight = -24;
        card.OffsetTop = -168;
        card.OffsetBottom = -16;
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
            ContentMarginTop = 14,
            ContentMarginBottom = 14
        });
        AddChild(card);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        card.AddChild(root);

        _stepLabel = new Label();
        _stepLabel.AddThemeColorOverride("font_color", Accent);
        _stepLabel.AddThemeFontSizeOverride("font_size", 14);
        root.AddChild(_stepLabel);

        _bodyLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 56)
        };
        _bodyLabel.AddThemeColorOverride("font_color", TextPrimary);
        _bodyLabel.AddThemeFontSizeOverride("font_size", 15);
        root.AddChild(_bodyLabel);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        root.AddChild(row);

        var skip = new Button { Text = "Salta (Backspace)", CustomMinimumSize = new Vector2(0, 36) };
        skip.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        skip.Pressed += Skip;
        row.AddChild(skip);

        _nextBtn = new Button { Text = "Avanti", CustomMinimumSize = new Vector2(0, 36) };
        _nextBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _nextBtn.Pressed += Advance;
        row.AddChild(_nextBtn);

        var hint = new Label
        {
            Text = "Invio / Spazio = avanti · Esc chiude toast/menu (non il tutorial).",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        hint.AddThemeColorOverride("font_color", TextMuted);
        hint.AddThemeFontSizeOverride("font_size", 11);
        root.AddChild(hint);
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
