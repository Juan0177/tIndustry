using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Dev mode (<c>TINDUSTRY_DEV=1</c>, debug builds only — exported release games ignore it):
/// console (F1), pause (F2), slower/faster (F3/F4), single tick while paused (.), and a status line
/// with speed, tick, game time and the cell under the mouse. Off during scripted playtests.
/// </summary>
public partial class World
{
    private DevConsole? _dev;
    private DevConsolePanel? _devPanel;
    private Label? _devStatus;
    private DevSpeedBar? _devSpeedBar;

    private static bool DevModeRequested =>
        DevConsole.RequestedByEnvironment && OS.IsDebugBuild() && !PlaytestRequested;

    private bool DevConsoleOpen => _devPanel?.IsOpen == true;

    private void EnsureDevTools()
    {
        if (!DevModeRequested)
        {
            return;
        }

        var dev = new DevConsole();
        _dev = dev;
        var layer = GetNode<CanvasLayer>("Hud");

        // Top-right corner: the top-left column belongs to menu / core / objectives.
        _devSpeedBar = new DevSpeedBar();
        _devSpeedBar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        // Zero-size anchor point 12 px from the right edge; the bar grows leftwards from it.
        _devSpeedBar.OffsetLeft = _devSpeedBar.OffsetRight = -12;
        _devSpeedBar.OffsetTop = 8;
        _devSpeedBar.SpeedChosen += speed =>
        {
            dev.SetSpeed(speed);
            RefreshDevSpeedBar();
        };
        _devSpeedBar.PauseToggled += () =>
        {
            dev.TogglePause();
            RefreshDevSpeedBar();
        };
        _devSpeedBar.FastForwardRequested += seconds => OnDevCommand($"avanti {seconds}", toast: true);
        _devSpeedBar.ConsoleRequested += () => _devPanel?.Toggle();
        layer.AddChild(_devSpeedBar);

        _devStatus = new Label
        {
            Name = "DevStatus",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _devStatus.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _devStatus.OffsetLeft = -520;
        _devStatus.OffsetRight = -14;
        _devStatus.OffsetTop = 44;
        _devStatus.OffsetBottom = 64;
        _devStatus.AddThemeColorOverride("font_color", HudStyle.SlotSelected);
        _devStatus.AddThemeColorOverride("font_outline_color", Colors.Black);
        _devStatus.AddThemeConstantOverride("outline_size", 4);
        _devStatus.AddThemeFontSizeOverride("font_size", 14);
        layer.AddChild(_devStatus);

        // FPS / resource overlays share the top-right corner: move them below the bar.
        foreach (var overlay in new Control?[] { _fpsLabel, _resourceLabel })
        {
            if (overlay is not null)
            {
                overlay.OffsetTop += 60;
                overlay.OffsetBottom += 60;
            }
        }

        // Created after every other panel: drawn on top of Home, Ricerca, Saves…
        _devPanel = new DevConsolePanel();
        layer.AddChild(_devPanel);
        _devPanel.CommandSubmitted += OnDevCommand;
        GD.Print($"{DevConsole.EnvVar}=1: modalità dev attiva (F1 console)");
        QueueDevStartupCommands();
    }

    /// <summary>
    /// After a game starts (new, campaign level, load): run <c>TINDUSTRY_DEV_AVVIO</c> on it. Deferred so the
    /// session setup (visuals, HUD, camera) is finished first.
    /// </summary>
    private void QueueDevStartupCommands()
    {
        if (_dev is not null && DevConsole.StartupCommands().Count > 0)
        {
            CallDeferred(MethodName.RunDevStartupCommands);
        }
    }

    private void RunDevStartupCommands()
    {
        foreach (var command in DevConsole.StartupCommands())
        {
            _devPanel?.Print("> " + command);
            OnDevCommand(command);
        }
    }

    /// <summary>Simulation ticks for this frame: dev speed/pause when active, else the plain fixed clock.</summary>
    private int SimTicksThisFrame(double delta) =>
        _dev?.TicksForFrame(_clock, delta) ?? _clock.Accumulate(delta);

    private void UpdateDevStatus()
    {
        if (_dev is null || _devStatus is null || _slice is null)
        {
            return;
        }

        var cell = _hover is { } h ? $" · cella {h.X},{h.Y}" : "";
        _devStatus.Text = _dev.StatusText(_slice) + cell;
        RefreshDevSpeedBar();
    }

    private void RefreshDevSpeedBar()
    {
        if (_dev is not null)
        {
            _devSpeedBar?.Refresh(_dev.Speed, _dev.Paused);
        }
    }

    /// <summary>Dev hotkeys; true when <paramref name="key"/> was one of them.</summary>
    private bool HandleDevHotkey(InputEventKey key)
    {
        if (_dev is null)
        {
            return false;
        }

        switch (key.Keycode)
        {
            case Key.F2:
                _dev.TogglePause();
                _hud?.ShowToast(_dev.Paused ? "Pausa (F2)" : "Ripresa");
                return true;
            case Key.F3:
                _dev.StepSpeed(-1);
                _hud?.ShowToast($"Velocità {_dev.Speed:0.##}×");
                return true;
            case Key.F4:
                _dev.StepSpeed(1);
                _hud?.ShowToast($"Velocità {_dev.Speed:0.##}×");
                return true;
            case Key.Period when _dev.Paused:
                _dev.QueueSteps(1);
                return true;
            default:
                return false;
        }
    }

    /// <param name="toast">Also show the last output line as a toast (commands run from the speed bar).</param>
    private void OnDevCommand(string line) => OnDevCommand(line, toast: false);

    private void OnDevCommand(string line, bool toast)
    {
        if (_dev is null || _devPanel is null || _slice is null)
        {
            return;
        }

        var result = _dev.Execute(line, _slice);
        if (result.ClearLog)
        {
            _devPanel.ClearLog();
        }

        foreach (var text in result.Lines)
        {
            _devPanel.Print(text);
        }

        if (toast && result.Lines.Count > 0)
        {
            _hud?.ShowToast(result.Lines[^1]);
        }

        if (result.ResearchChanged)
        {
            SyncResearchLocks();
        }

        if (result.CompleteLevel && _activeLevel is not null && _progress is not null)
        {
            _progress.MarkComplete(_activeLevel.Id, _progressPath);
            _campaignSelect?.Refresh();
            _hud?.ShowToast($"Livello completato · {_activeLevel.Name}");
        }

        if (result.CameraToCore)
        {
            FocusCameraOnCore();
        }
        else if (result.CameraTarget is { } target && GetNodeOrNull<WorldCamera>("Camera") is { } cam)
        {
            cam.Position = new Vector2((target.X + 0.5f) * TileSize, (target.Y + 0.5f) * TileSize);
            cam.ClampToMap();
        }

        if (result.WorldChanged || result.ResearchChanged)
        {
            UpdateHud();
            CheckCampaignComplete();
        }
    }
}
