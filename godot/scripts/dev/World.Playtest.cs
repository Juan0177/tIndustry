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
        // The click only plans the miner: the drone builds it, and the step completes once it stands.
        var finishedBefore = ConstructFinishedCount;
        await PlaytestClick(CellToScreen(minerCell));
        if (!await PlaytestWaitUntil(() => ConstructFinishedCount > finishedBefore && _view.Miners.Count > 0, MinerBuildTimeout))
        {
            return Fail($"minatore non costruito entro {MinerBuildTimeout} s");
        }

        _droneSample = null;
        if (!await PlaytestWaitUntil(DroneSettled, DroneSettleTimeout))
        {
            return Fail("il drone non torna a riposo dopo il minatore");
        }

        await Shot("step3-miner-placed");
        await PlaytestWaitUntil(() => _tutorial?.CurrentStep != TutorialPanel.Step.Miner, StepTimeout);
        if (!Expect(TutorialPanel.Step.BeltToCore, "minatore costruito"))
        {
            return false;
        }

        // 4) Belt: key 1 then drag from the miner output to the Core; the drone builds the belts in order.
        await PlaytestKey(Key.Key1);
        var firstBelt = minerCell.X + Miner.Size;
        var lastBelt = core.X - 1;
        var from = CellToScreen(new GridPosition(firstBelt, core.Y));
        var to = CellToScreen(new GridPosition(lastBelt, core.Y));
        finishedBefore = ConstructFinishedCount;
        await PlaytestDrag(MouseButton.Left, from, to);
        await PlaytestKey(Key.Escape);
        var belts = lastBelt - firstBelt + 1;
        if (!await PlaytestWaitUntil(
                () => ConstructFinishedCount - finishedBefore >= belts && LocalQueueEmpty(),
                BeltDragTimeout))
        {
            return Fail($"{belts} nastri non costruiti entro {BeltDragTimeout} s");
        }

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

        if (OS.GetEnvironment(PlaytestPerfEnv) == "1")
        {
            await PlaytestBeltPerfAsync();
        }

        if (!await PlaytestRemoveDragOutAndBackAsync())
        {
            return false;
        }

        AutoSaveContinue(quiet: true);
        if (!FactorySaveStore.Exists(FactorySaveStore.ContinueSlotId))
        {
            return Fail("autosave su Continua mancante dopo una partita giocata");
        }

        return _view!.CoreDeliveredItems > 0 || Fail("nessun oggetto consegnato al CORE");
    }

    /// <summary>Set to 1 to run <see cref="PlaytestBeltPerfAsync"/> in the <c>ui</c> playtest.</summary>
    private const string PlaytestPerfEnv = "TINDUSTRY_PLAYTEST_PERF";

    /// <summary>
    /// Performance probe (only with <c>TINDUSTRY_PLAYTEST_PERF=1</c>), no screenshot and no pass/fail: lays 200 belts (a 20×10 serpentine of the three
    /// tiers, so with corners) below the Core, frames them at 1280×720 and prints average and minimum FPS
    /// over 3 s without VSync, with the node count.
    /// </summary>
    private async Task PlaytestBeltPerfAsync()
    {
        if (_view is not Factory factory || GetNodeOrNull<Camera2D>("Camera") is not { } cam)
        {
            return;
        }

        const int cols = 20, rows = 10;
        string[] tiers = ["conveyor-basic", "conveyor-fast", "conveyor-express"];
        var core = new GridPosition(_view.CoreTiles.Min(t => t.X), _view.CoreTiles.Min(t => t.Y));
        var origin = new GridPosition(core.X - cols / 2, core.Y + 5);
        var placed = 0;
        for (var r = 0; r < rows; r++)
        {
            var east = r % 2 == 0;
            for (var c = 0; c < cols; c++)
            {
                var cell = new GridPosition(origin.X + (east ? c : cols - 1 - c), origin.Y + r);
                var dir = c == cols - 1 ? Direction.South : east ? Direction.East : Direction.West;
                if (factory.PlaceInstant(new BuildSpec(tiers[r % tiers.Length], null, cell, dir), charge: false))
                {
                    placed++;
                }
            }
        }

        _visualDirty = true;
        cam.Position = new Vector2((origin.X + cols * 0.5f) * TileSize, (origin.Y + rows * 0.5f) * TileSize);
        cam.Zoom = new Vector2(0.9f, 0.9f);
        // Uncapped: VSync would pin every run to the monitor's refresh rate.
        var vsync = DisplayServer.WindowGetVsyncMode();
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        await PlaytestWait(1.0);

        var frames = 0;
        var worst = 0UL;
        var start = Time.GetTicksUsec();
        var last = start;
        while (Time.GetTicksUsec() - start < 3_000_000UL)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var now = Time.GetTicksUsec();
            worst = Math.Max(worst, now - last);
            last = now;
            frames++;
        }

        DisplayServer.WindowSetVsyncMode(vsync);
        var average = frames / ((last - start) / 1e6);
        var nodes = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        GD.Print($"PLAYTEST perf: {placed} nastri, fps medio {average:F0}, minimo {1e6 / Math.Max(1UL, worst):F0}, nodi {nodes:F0}");
    }

    /// <summary>
    /// Right-drag over the seed line (miner + belts) out to the Core and back, ending on other cells of the
    /// miner, then a second drag over cells already marked: every block must end with a breaking plan, a
    /// deconstruction or nothing left (never toggled back).
    /// No screenshot: it runs after the last one.
    /// </summary>
    private async Task<bool> PlaytestRemoveDragOutAndBackAsync()
    {
        var core = new GridPosition(_view!.CoreTiles.Min(t => t.X), _view.CoreTiles.Min(t => t.Y));
        var minerCell = new GridPosition(Math.Max(0, core.X - Miner.Size - 2), core.Y);
        var lastBelt = new GridPosition(core.X - 1, core.Y);
        if (GetNodeOrNull<Camera2D>("Camera") is { } cam)
        {
            cam.Position = CellCenter(new GridPosition(core.X - 2, core.Y));
            cam.Zoom = new Vector2(1.6f, 1.6f);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        for (var x = minerCell.X; x <= lastBelt.X; x++)
        {
            if (_view.CanOccupy(new GridPosition(x, core.Y)))
            {
                return Fail($"linea del seed incompleta prima del trascinamento: {x},{core.Y} vuota");
            }
        }

        await PlaytestDrag(MouseButton.Right,
        [
            CellToScreen(minerCell),
            CellToScreen(lastBelt),
            CellToScreen(new GridPosition(minerCell.X + 1, core.Y)),
            CellToScreen(new GridPosition(minerCell.X, core.Y + 1))
        ]);
        if (!await PlaytestWaitUntil(() => PendingCommands.Count == 0, StepTimeout))
        {
            return Fail("comandi del trascinamento non applicati");
        }

        for (var x = minerCell.X; x <= lastBelt.X; x++)
        {
            var cell = new GridPosition(x, core.Y);
            if (!RemovedOrBeingRemoved(cell))
            {
                return Fail($"trascinamento col destro andata e ritorno: {cell} senza smontaggio");
            }
        }

        // A second drag over cells already marked (starting on one) only marks: nothing is toggled back.
        await PlaytestDrag(MouseButton.Right, CellToScreen(lastBelt), CellToScreen(new GridPosition(minerCell.X + 1, core.Y)));
        if (!await PlaytestWaitUntil(() => PendingCommands.Count == 0, StepTimeout))
        {
            return Fail("comandi del secondo trascinamento non applicati");
        }

        for (var x = minerCell.X; x <= lastBelt.X; x++)
        {
            var cell = new GridPosition(x, core.Y);
            if (!RemovedOrBeingRemoved(cell))
            {
                return Fail($"secondo trascinamento col destro: {cell} smarcato");
            }
        }

        GD.Print($"PLAYTEST step ok: smontaggio trascinato andata e ritorno (fps {Engine.GetFramesPerSecond()})");
        return true;
    }

    /// <summary>
    /// True when the block on <paramref name="cell"/> is going or gone: a breaking plan queued, a deconstruction
    /// running, or nothing left standing. Counting "gone" keeps the check independent of the frame rate (at
    /// 60 fps the drag outlasts the miner's deconstruction); a toggled cell keeps its block with no plan.
    /// </summary>
    private bool RemovedOrBeingRemoved(GridPosition cell)
    {
        if (_view!.TryGetSiteAt(cell, out var site))
        {
            return site.Deconstructing;
        }

        if (_view.CanOccupy(cell))
        {
            return true;
        }

        foreach (var plan in _view.PlansOf(LocalPlayer))
        {
            if (plan.Breaking && _view.PlanTiles(plan).Contains(cell))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Seconds a single building may take to be built (it needs well under one).</summary>
    private const double MinerBuildTimeout = 10;

    /// <summary>Seconds the drone may take to build a whole belt drag.</summary>
    private const double BeltDragTimeout = 30;

    private const double DroneSettleTimeout = 5;
    private const double StepTimeout = 5;

    /// <summary>
    /// Polls <paramref name="done"/> once per frame until it holds or <paramref name="timeoutSeconds"/> pass
    /// (no fixed sleep: the wait ends as soon as the game gets there). True when it held.
    /// </summary>
    private async Task<bool> PlaytestWaitUntil(Func<bool> done, double timeoutSeconds)
    {
        var deadline = Time.GetTicksMsec() + (ulong)(timeoutSeconds * 1000);
        while (!done())
        {
            if (Time.GetTicksMsec() > deadline)
            {
                return false;
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        return true;
    }

    private bool LocalQueueEmpty() => _view is not null && _view.PlansOf(LocalPlayer).Count == 0 && _view.Sites.Count == 0;

    private (float X, float Y, long Tick)? _droneSample;

    /// <summary>Queue empty and the drone did not move over the last simulation tick (back at rest).</summary>
    private bool DroneSettled()
    {
        if (!LocalQueueEmpty() || _view!.Builders.Count == 0)
        {
            _droneSample = null;
            return false;
        }

        var drone = _view.Builders[0];
        var now = (drone.X, drone.Y, _view.CurrentTick);
        var settled = _droneSample is { } last && last.Tick != now.CurrentTick && last.X == now.X && last.Y == now.Y;
        if (_droneSample is not { } prev || prev.Tick != now.CurrentTick)
        {
            _droneSample = now;
        }

        return settled;
    }

    /// <summary>World cell → window pixels (what a real mouse event carries, before stretch/UI scale).</summary>
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

    private Task PlaytestDrag(MouseButton button, Vector2 from, Vector2 to, int steps = 16) =>
        PlaytestDrag(button, [from, to], steps);

    /// <summary>Press at the first point, move through the others (<paramref name="steps"/> moves per leg), release.</summary>
    private async Task PlaytestDrag(MouseButton button, Vector2[] path, int steps = 16)
    {
        var mask = button switch
        {
            MouseButton.Left => MouseButtonMask.Left,
            MouseButton.Right => MouseButtonMask.Right,
            _ => MouseButtonMask.Middle
        };
        var from = path[0];
        PlaytestMove(from, 0);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = button, Pressed = true, Position = from, GlobalPosition = from, ButtonMask = mask,
            Device = PlaytestDevice
        });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (var leg = 1; leg < path.Length; leg++)
        {
            for (var i = 1; i <= steps; i++)
            {
                PlaytestMove(path[leg - 1].Lerp(path[leg], i / (float)steps), mask);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }

        var to = path[^1];
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
