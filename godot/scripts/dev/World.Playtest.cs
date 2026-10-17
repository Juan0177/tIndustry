using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Dev-only scripted playtests (<c>TINDUSTRY_PLAYTEST=tutorial</c>): drive the real input pipeline with
/// synthetic key/mouse events and save a frame per step to <see cref="CaptureDirectory"/>.
/// Requires <c>TINDUSTRY_DATA_DIR</c> so it never touches the player's real settings or saves.
/// </summary>
public partial class World
{
    /// <summary>Device id stamped on synthetic events; anything else is the player's real input.</summary>
    private const int PlaytestDevice = 4242;

    /// <summary>
    /// While a playtest runs, drop real mouse/keyboard input before UI and world see it: the window is on
    /// the player's screen and a stray click or Esc made runs flaky (Home opened mid-test).
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (PlaytestRequested && @event.Device != PlaytestDevice)
        {
            GetViewport().SetInputAsHandled();
        }
    }

    private static bool PlaytestRequested => GameDataPaths.IsPlaytest;

    /// <summary>
    /// Runs first in <see cref="_Ready"/>, before settings or progress are read: a playtest without an
    /// absolute isolated profile quits at once, and any later write the guard refuses quits too.
    /// </summary>
    private bool GuardPlaytestProfile()
    {
        if (!PlaytestRequested)
        {
            return true;
        }

        GameDataPaths.WriteRefused += OnPlayerDataWriteRefused;
        if (GameDataPaths.PlaytestProfileProblem() is not { } problem)
        {
            return true;
        }

        OnPlayerDataWriteRefused($"{problem} Playtest annullato.");
        SetProcess(false);
        SetPhysicsProcess(false);
        return false;
    }

    public override void _ExitTree() => GameDataPaths.WriteRefused -= OnPlayerDataWriteRefused;

    private void OnPlayerDataWriteRefused(string message)
    {
        GD.PushError(message);
        GetTree().Quit(3);
    }

    private void StartPlaytestIfRequested()
    {
        if (!PlaytestRequested)
        {
            return;
        }

        var timer = GetTree().CreateTimer(1.0);
        timer.Timeout += () => _ = RunPlaytestAsync(OS.GetEnvironment(GameDataPaths.PlaytestEnv));
    }

    private async Task RunPlaytestAsync(string name)
    {
        var dir = CaptureDirectory();
        if (dir is null)
        {
            GetTree().Quit(2);
            return;
        }

        var ok = name switch
        {
            "tutorial" => await PlaytestTutorialAsync(dir),
            "ui" => await PlaytestUiAsync(dir),
            _ => Fail($"playtest sconosciuto: {name}")
        };
        GD.Print(ok ? "PLAYTEST OK" : "PLAYTEST FAIL");
        GetTree().Quit(ok ? 0 : 1);
    }

    /// <summary>First run → Nuova partita → every tutorial step done by input, frame saved after each.</summary>
    private async Task<bool> PlaytestTutorialAsync(string dir)
    {
        var shot = 0;
        async Task Shot(string label)
        {
            await PlaytestWait(0.25);
            GetViewport().GetTexture().GetImage()
                .SavePng(Path.Combine(dir, $"tutorial-{shot++:00}-{label}.png"));
        }

        bool Expect(TutorialPanel.Step step, string label)
        {
            if (_tutorial is { IsOpen: true } t && t.CurrentStep == step)
            {
                GD.Print($"PLAYTEST step ok: {label}");
                return true;
            }

            return Fail($"atteso passo {step} dopo '{label}', trovato {(_tutorial?.IsOpen == true ? _tutorial.CurrentStep.ToString() : "chiuso")}");
        }

        await Shot("home");
        OnHomeNewGame();
        await PlaytestWait(0.5);
        await Shot("step1-camera");
        if (!Expect(TutorialPanel.Step.Camera, "nuova partita"))
        {
            return false;
        }

        // 1) Left-drag pan in cursor mode (also proves the world is playable under the card).
        var mid = ViewportToWindow(GetViewport().GetVisibleRect().Size / 2);
        await PlaytestDrag(MouseButton.Left, mid, mid + new Vector2(-260, -140));
        await PlaytestWait(1.3);
        await Shot("step2-palette");
        if (!Expect(TutorialPanel.Step.Palette, "trascina visuale"))
        {
            return false;
        }

        // 2) Pick the miner from the keyboard; 3) place it next to the Core.
        FocusCameraOnCore();
        await PlaytestKey(Key.Key2);
        await PlaytestWait(1.3);
        if (!Expect(TutorialPanel.Step.Miner, "tasto 2"))
        {
            return false;
        }

        var core = new GridPosition(_view!.CoreTiles.Min(t => t.X), _view.CoreTiles.Min(t => t.Y));
        var minerCell = new GridPosition(Math.Max(0, core.X - Miner.Size - 2), core.Y);
        await PlaytestClick(CellToScreen(minerCell));
        await PlaytestWait(0.3);
        await Shot("step3-miner-placed");
        await PlaytestWait(1.2);
        if (!Expect(TutorialPanel.Step.BeltToCore, "click minatore"))
        {
            return false;
        }

        // 4) Belt: key 1 then drag from the miner output to the Core.
        await PlaytestKey(Key.Key1);
        var from = CellToScreen(new GridPosition(minerCell.X + Miner.Size, core.Y));
        var to = CellToScreen(new GridPosition(core.X - 1, core.Y));
        await PlaytestDrag(MouseButton.Left, from, to);
        await PlaytestKey(Key.Escape);
        for (var i = 0; i < 80 && _tutorial?.CurrentStep == TutorialPanel.Step.BeltToCore; i++)
        {
            await PlaytestWait(0.25);
        }

        await Shot("step5-mercato");
        if (!Expect(TutorialPanel.Step.Mercato, "nastro fino al CORE"))
        {
            return false;
        }

        // 5) Mercato, 6) Ricerca: open with the hotkey, close with the same key.
        await PlaytestKey(Key.M);
        await PlaytestWait(0.4);
        await Shot("mercato-open");
        await PlaytestKey(Key.M);
        await PlaytestWait(1.2);
        if (!Expect(TutorialPanel.Step.Ricerca, "M mercato"))
        {
            return false;
        }

        await PlaytestKey(Key.T);
        await PlaytestWait(0.4);
        await PlaytestKey(Key.T);
        await PlaytestWait(1.2);
        await Shot("step7-forno");
        if (!Expect(TutorialPanel.Step.Forno, "T ricerca"))
        {
            return false;
        }

        // 7) Forno via the manual path (Invio = Avanti), 8) Fine.
        await PlaytestKey(Key.Enter);
        await PlaytestWait(0.4);
        await Shot("step8-impostazioni");
        if (!Expect(TutorialPanel.Step.Impostazioni, "Invio"))
        {
            return false;
        }

        await PlaytestKey(Key.Enter);
        await PlaytestWait(0.4);
        await Shot("done");
        if (_tutorial?.IsOpen == true || !_settings.TutorialCompleted)
        {
            return Fail("tutorial ancora aperto o non segnato completato");
        }

        return true;
    }

    /// <summary>World cell → window pixels (what a real mouse event carries, before stretch/UI scale).</summary>
    /// <summary>Screens of the main UI with a running factory: HUD, belt→CORE close-up, Mercato, Ricerca.</summary>
    private async Task<bool> PlaytestUiAsync(string dir)
    {
        async Task Shot(string label)
        {
            await PlaytestWait(0.3);
            GetViewport().GetTexture().GetImage().SavePng(Path.Combine(dir, $"ui-{label}.png"));
        }

        // Leaving Home before playing (Esci / window close) must not write the startup sandbox to Continua.
        AutoSaveContinue(quiet: true);
        if (FactorySaveStore.Exists(FactorySaveStore.ContinueSlotId))
        {
            return Fail("autosave su Continua senza una partita giocata");
        }

        _settings.TutorialCompleted = true;
        OnHomeNewGame();
        _tutorial?.Close();

        // Isolation check: an event from a real device (the player's mouse/keyboard on this window)
        // must not reach the game while a playtest drives it.
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = false });
        await PlaytestWait(0.2);
        if (_home?.IsOpen == true)
        {
            return Fail("un input reale (Esc) ha raggiunto il gioco durante il playtest");
        }
        SeedCoreDeliveryLine();
        FocusCameraOnCore();
        for (var i = 0; i < 60 && _view!.CoreDeliveredItems < 3; i++)
        {
            await PlaytestWait(0.25);
        }

        await Shot("hud");

        if (GetNodeOrNull<Camera2D>("Camera") is { } cam)
        {
            var core = new GridPosition(_view!.CoreTiles.Min(t => t.X), _view.CoreTiles.Min(t => t.Y));
            cam.Position = CellCenter(new GridPosition(core.X - 1, core.Y));
            cam.Zoom = new Vector2(1.6f, 1.6f);
            await Shot("belt-into-core");
            FocusCameraOnCore();
        }

        await PlaytestKey(Key.Key2);
        await Shot("palette-info");
        await PlaytestKey(Key.Escape);

        await PlaytestKey(Key.M);
        await Shot("mercato");
        await PlaytestKey(Key.M);

        await PlaytestKey(Key.T);
        await Shot("ricerca");
        await PlaytestKey(Key.T);

        AutoSaveContinue(quiet: true);
        if (!FactorySaveStore.Exists(FactorySaveStore.ContinueSlotId))
        {
            return Fail("autosave su Continua mancante dopo una partita giocata");
        }

        return _view!.CoreDeliveredItems > 0 || Fail("nessun oggetto consegnato al CORE");
    }

    private Vector2 CellToScreen(GridPosition cell) =>
        ViewportToWindow(GetViewport().GetCanvasTransform() * CellCenter(cell));

    private Vector2 ViewportToWindow(Vector2 viewportPos) => GetViewport().GetFinalTransform() * viewportPos;

    private async Task PlaytestWait(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private async Task PlaytestKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true, Device = PlaytestDevice });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false, Device = PlaytestDevice });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task PlaytestClick(Vector2 at)
    {
        PlaytestMove(at, 0);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left, Pressed = true, Position = at, GlobalPosition = at,
            ButtonMask = MouseButtonMask.Left, Device = PlaytestDevice
        });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left, Pressed = false, Position = at, GlobalPosition = at, Device = PlaytestDevice
        });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task PlaytestDrag(MouseButton button, Vector2 from, Vector2 to, int steps = 16)
    {
        var mask = button == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Middle;
        PlaytestMove(from, 0);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = button, Pressed = true, Position = from, GlobalPosition = from, ButtonMask = mask,
            Device = PlaytestDevice
        });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (var i = 1; i <= steps; i++)
        {
            PlaytestMove(from.Lerp(to, i / (float)steps), mask);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = button, Pressed = false, Position = to, GlobalPosition = to, Device = PlaytestDevice
        });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void PlaytestMove(Vector2 at, MouseButtonMask mask) =>
        Input.ParseInputEvent(new InputEventMouseMotion
        {
            Position = at, GlobalPosition = at, ButtonMask = mask, Device = PlaytestDevice
        });

    private static bool Fail(string message)
    {
        GD.PushError($"PLAYTEST: {message}");
        return false;
    }

    private static string? CaptureDirectory()
    {
        var dir = OS.GetEnvironment("TINDUSTRY_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = ProjectSettings.GlobalizePath("res://artifacts");
        }

        try
        {
            Directory.CreateDirectory(dir);
            return dir;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Capture dir non creabile ({dir}): {ex.Message}");
            return null;
        }
    }
}
