using Godot;

namespace TIndustry.Godot;

/// <summary>
/// Full-screen loading screen shown while a game opens (new game, campaign level, save, Continua): the logo,
/// "Caricamento…", a bar that advances one step per phase and the phase's line (map generation, save read, world,
/// terrain). It sits above every panel, stops the mouse (World's <c>_Input</c> drops every other event meanwhile) and
/// fades out once the first frame of the game is ready. The work it covers is blocking: World shows it, lets frames
/// draw, then runs each phase on the main thread (see <c>World.WithLoadingAsync</c>).
/// </summary>
public partial class LoadingScreen : Control
{
    /// <summary>Logo pixels per sprite pixel (at 100% UI scale), as on the Home.</summary>
    private const int LogoScale = 5;

    /// <summary>
    /// Size of the bar in UI units: the screen is a child of the HUD root, so the HUD layer's scale (<see cref="UiScale"/>)
    /// magnifies it with the rest of the interface, like every panel's sizes.
    /// </summary>
    private static readonly Vector2 BarSize = new(360, 16);

    /// <summary>Seconds of the fade out.</summary>
    private const double FadeSeconds = 0.3;

    private ColorRect? _background;
    private ProgressBar? _bar;
    private Label? _phase;

    // Seconds left of the fade out; 0 = not fading. No Tween: nothing left for the GC at exit.
    private double _fadeLeft;

    /// <summary>True while the screen covers the game (fade out included).</summary>
    public bool IsShown => Visible;

    /// <summary>The phase line as shown (a key of testi.csv), for the playtests.</summary>
    internal string PhaseText => _phase?.Text ?? "";

    public override void _Ready()
    {
        Name = "LoadingScreen";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _background = new ColorRect { Name = "Fondo", MouseFilter = MouseFilterEnum.Ignore };
        _background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_background);

        var center = new CenterContainer { Name = "Centro", MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var column = new VBoxContainer { ThemeTypeVariation = UiTheme.ColonnaLarga, MouseFilter = MouseFilterEnum.Ignore };
        center.AddChild(column);

        var logo = Sprites.For("ui/ui-logo");
        column.AddChild(new TextureRect
        {
            Name = "Logo",
            Texture = logo,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = logo.GetSize() * LogoScale,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            TextureFilter = TextureFilterEnum.Nearest,
            MouseFilter = MouseFilterEnum.Ignore
        });
        column.AddChild(new Label
        {
            Name = "Titolo",
            Text = "caricamento.titolo",
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = UiTheme.Titolo,
            MouseFilter = MouseFilterEnum.Ignore
        });
        _bar = new ProgressBar
        {
            Name = "Barra",
            MinValue = 0,
            MaxValue = 1,
            Step = 0,
            ShowPercentage = false,
            CustomMinimumSize = BarSize,
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore
        };
        column.AddChild(_bar);
        _phase = new Label
        {
            Name = "Fase",
            HorizontalAlignment = HorizontalAlignment.Center,
            ThemeTypeVariation = UiTheme.TestoTenue,
            MouseFilter = MouseFilterEnum.Ignore
        };
        column.AddChild(_phase);
    }

    /// <summary>Shows the screen at once, empty bar, over <paramref name="phases"/> steps (at least one).</summary>
    public void Begin(int phases)
    {
        _fadeLeft = 0;
        Modulate = Colors.White;
        if (_background is not null)
        {
            // Read now: the colour variant may have changed since the screen was built.
            _background.Color = UiColors.FondoSchermo;
        }

        if (_bar is not null)
        {
            _bar.MaxValue = Math.Max(1, phases);
            _bar.Value = 0;
        }

        if (_phase is not null)
        {
            _phase.Text = "";
        }

        Visible = true;
        // Nothing behind keeps the keyboard focus (a focused Home button would take Enter or Space).
        GetViewport()?.GuiReleaseFocus();
    }

    /// <summary>Shows phase <paramref name="key"/> (a key of testi.csv) with <paramref name="done"/> steps done.</summary>
    public void SetPhase(string key, int done)
    {
        if (_phase is not null)
        {
            _phase.Text = key;
        }

        if (_bar is not null)
        {
            _bar.Value = Math.Clamp(done, 0, (int)_bar.MaxValue);
        }
    }

    /// <summary>Hides the screen: a short fade, or at once with <paramref name="instant"/> (playtests, failures).</summary>
    public void End(bool instant)
    {
        if (instant || !Visible)
        {
            _fadeLeft = 0;
            Visible = false;
            Modulate = Colors.White;
            return;
        }

        if (_bar is not null)
        {
            _bar.Value = _bar.MaxValue;
        }

        _fadeLeft = FadeSeconds;
    }

    public override void _Process(double delta)
    {
        if (_fadeLeft <= 0)
        {
            return;
        }

        _fadeLeft = Math.Max(0, _fadeLeft - delta);
        Modulate = new Color(1, 1, 1, (float)(_fadeLeft / FadeSeconds));
        if (_fadeLeft <= 0)
        {
            Visible = false;
            Modulate = Colors.White;
        }
    }
}
