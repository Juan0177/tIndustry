using System.Globalization;
using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Time controls. Space pauses and resumes the game for every player: while paused the host gets no frame time, so
/// no tick runs and the commands the player submits stay queued in <see cref="SimHost"/> until the resume. With
/// <c>TINDUSTRY_DEV=1</c> (read once at start, never in playtests) F1–F4 set the clock speed ×1/×5/×10/×20
/// (<see cref="SimHost.Speed"/>) and a small bar top-right shows speed, tick and game time. Playtests (and the agents'
/// own pause, <see cref="PlaytestHeld"/>) are untouched: there the player's pause and the speeds never apply.
/// </summary>
public partial class World
{
    /// <summary>Environment variable that turns on the dev speeds.</summary>
    public const string DevEnv = "TINDUSTRY_DEV";

    /// <summary>Dev speeds requested at start (<c>TINDUSTRY_DEV=1</c>) and not a playtest.</summary>
    private static readonly bool DevSpeeds =
        OS.GetEnvironment(DevEnv) == "1" && !GameDataPaths.IsPlaytest;

    /// <summary>The player paused the game with Space (reset by every new game, level or load).</summary>
    private bool _playerPaused;

    /// <summary>Dev speed kept across games of the session (always 1 without dev mode).</summary>
    private int _devSpeed = 1;

    private Label? _pauseLabel;
    private Label? _devSpeedLabel;

    /// <summary>The player's pause holds the simulation (never in playtests nor in agent games).</summary>
    internal bool PlayerPaused => _playerPaused && !PlaytestRequested && !AgentBridgeActive;

    /// <summary>
    /// Set by <see cref="AgentBridge"/> when it starts: the agents own time (pausa, riprendi, esegui, avanza), so
    /// Space is inert, whether a human or a synthetic key presses it.
    /// </summary>
    internal bool AgentBridgeActive { get; set; }

    /// <summary>The agents' riprendi, esegui and avanza lift a player pause as well.</summary>
    internal void ClearPlayerPause() => _playerPaused = false;

    private void EnsureTimeOverlays()
    {
        var hudRoot = UiRoot;
        _pauseLabel = hudRoot.GetNodeOrNull<Label>("InPausa");
        if (_pauseLabel is null)
        {
            _pauseLabel = new Label
            {
                Name = "InPausa",
                Text = "pausa.etichetta",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ThemeTypeVariation = UiTheme.AvvisoPausa,
                Visible = false
            };
            _pauseLabel.SetAnchorsPreset(Control.LayoutPreset.Center);
            _pauseLabel.GrowHorizontal = Control.GrowDirection.Both;
            _pauseLabel.GrowVertical = Control.GrowDirection.Both;
            hudRoot.AddChild(_pauseLabel);
        }

        if (!DevSpeeds)
        {
            return;
        }

        _devSpeedLabel = hudRoot.GetNodeOrNull<Label>("VelocitaDev");
        if (_devSpeedLabel is null)
        {
            _devSpeedLabel = new Label
            {
                Name = "VelocitaDev",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Right,
                ThemeTypeVariation = UiTheme.Avviso,
                Visible = false
            };
            _devSpeedLabel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            _devSpeedLabel.OffsetLeft = -420;
            _devSpeedLabel.OffsetRight = -12;
            hudRoot.AddChild(_devSpeedLabel);
        }

        GD.Print($"{DevEnv}=1: velocità dev attive (F1–F4)");
    }

    /// <summary>A new game, level or load starts unpaused, at the session's dev speed.</summary>
    private void ResetTimeControls(SimHost host)
    {
        _playerPaused = false;
        host.Speed = DevSpeeds ? _devSpeed : 1;
    }

    /// <summary>
    /// Space in <see cref="_Input"/> (before the GUI, so a focused button never takes it as a press): pause or resume
    /// during a game, not in the menus (Home, Nuova partita, saves, settings, campaign), not while a text field or a
    /// button has the focus, not in playtests and not in agent games. True when the event was used.
    /// </summary>
    private bool HandlePauseKey(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space } key
            || key.CtrlPressed || key.AltPressed || key.MetaPressed || key.ShiftPressed
            || PlaytestRequested || AgentBridgeActive || !GameInPlay || TimeMenuOpen || FocusTakesSpace())
        {
            return false;
        }

        _playerPaused = !_playerPaused;
        CancelDrags(); // a drag across the toggle would send a line planned before the pause
        return true;
    }

    /// <summary>Dev speed keys F1–F4 (×1, ×5, ×10, ×20); true when the event was one of them.</summary>
    private bool HandleDevSpeedKey(InputEventKey key)
    {
        if (!DevSpeeds || _host is null)
        {
            return false;
        }

        var index = key.Keycode switch
        {
            Key.F1 => 0,
            Key.F2 => 1,
            Key.F3 => 2,
            Key.F4 => 3,
            _ => -1
        };
        if (index < 0)
        {
            return false;
        }

        _devSpeed = SimulationClock.Speeds[index];
        _host.Speed = _devSpeed;
        _hud?.ShowToast(Testi.F("dev.velocita.toast", _devSpeed));
        return true;
    }

    /// <summary>Menus where Space does not pause: the ones that leave the game (or pick another one).</summary>
    private bool TimeMenuOpen => _saves?.IsOpen == true || _settingsPanel?.IsOpen == true
        || _campaignSelect?.IsOpen == true || LoadingShown;

    /// <summary>
    /// The focused control uses Space itself: a text field (typing) or a button of an in-game panel (Space presses
    /// it, as everywhere in Godot), so the pause leaves the key to it.
    /// </summary>
    private bool FocusTakesSpace() =>
        GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit or SpinBox or BaseButton;

    /// <summary>Per frame: the dev bar's text follows the game (whether it shows: <see cref="SyncHudVisibility"/>).</summary>
    private void SyncTimeOverlays()
    {
        if (_devSpeedLabel is null || !GameInPlay || _view is null)
        {
            return;
        }

        // Below the FPS line or the resource overlay, which share the corner.
        var top = _settings.ShowResourceOverlay ? 144 : _settings.ShowFps ? 36 : 8;
        _devSpeedLabel.OffsetTop = top;
        _devSpeedLabel.OffsetBottom = top + 24;
        var tick = _view.CurrentTick;
        var seconds = tick / SimulationClock.TickRate;
        var time = string.Create(CultureInfo.InvariantCulture, $"{seconds / 60:00}:{seconds % 60:00}");
        _devSpeedLabel.Text = Testi.F(
            "dev.velocita.barra", _host?.Speed ?? 1, tick.ToString(CultureInfo.InvariantCulture), time);
    }
}
