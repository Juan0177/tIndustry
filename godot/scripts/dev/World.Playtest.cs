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
    internal const int PlaytestDevice = 4242;

    /// <summary>
    /// While a playtest runs, real mouse/keyboard input is dropped before UI and world see it (see
    /// <see cref="_Input"/>): the window is on the player's screen and a stray click or Esc made runs flaky
    /// (Home opened mid-test).
    /// </summary>
    private static bool IsRealInputDuringPlaytest(InputEvent @event) =>
        PlaytestRequested && !IsSyntheticInput(@event);

    /// <summary>An event made by a playtest or the agents' bridge (mouse: <see cref="PlaytestDevice"/>; keys: marked).</summary>
    internal static bool IsSyntheticInput(InputEvent @event) =>
        @event.Device == PlaytestDevice || @event.HasMeta(SyntheticKeyMeta);

    /// <summary>
    /// Marks synthetic key events: they keep device 0, because the built-in UI actions (ui_text_select_all and the
    /// other text shortcuts) only match keyboard events of device 0.
    /// </summary>
    private const string SyntheticKeyMeta = "tindustry_synthetic";

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
            "agenti" => await PlaytestAgentiAsync(dir),
            _ => Fail($"playtest sconosciuto: {name}")
        };
        GD.Print(ok ? "PLAYTEST OK" : "PLAYTEST FAIL");
        GetTree().Quit(ok ? 0 : 1);
    }

    /// <summary>First run → Nuova partita → every tutorial step done by input, frame saved after each.</summary>
    private async Task<bool> PlaytestTutorialAsync(string dir)
    {
        var shot = 0;
        string? layoutProblem = null;
        async Task Shot(string label)
        {
            await PlaytestTerrainReady();
            await PlaytestWait(0.25);
            SaveViewportPng(Path.Combine(dir, $"tutorial-{shot++:00}-{label}.png"));
            // Every step's card and the HUD fit the window at the run's UI scale (text not cut, no panel on another).
            if (MustFit(_settings.UiScalePercent) && layoutProblem is null
                && PlaytestLayoutProblems(hudOnly: true) is { Count: > 0 } problems)
            {
                layoutProblem = $"{label}: {problems.Count} controlli fuori posto (il primo: {problems[0]})";
            }
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
        // The cells the tutorial asks for (miner and the belt to the Core) must not be under the card or a HUD panel.
        var hudRects = PlaytestHudPanelRects();
        for (var x = minerCell.X; x < core.X; x++)
        {
            if (PlaytestCellUnderHud(new GridPosition(x, core.Y), 1, hudRects)
                || PlaytestCellUnderHud(new GridPosition(x, core.Y + 1), 1, hudRects))
            {
                return Fail($"la cella ({x},{core.Y}) del minatore o del nastro è sotto un pannello dell'HUD (scala {_settings.UiScalePercent}%)");
            }
        }

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

        if (layoutProblem is not null)
        {
            return Fail($"tutorial a scala {_settings.UiScalePercent}% · {layoutProblem}");
        }

        GD.Print($"PLAYTEST tutorial a scala {_settings.UiScalePercent}%: rettangoli ok");
        return true;
    }

    /// <summary>Screens of the main UI with a running factory: HUD, belt→CORE close-up, Mercato, Ricerca.</summary>
    private async Task<bool> PlaytestUiAsync(string dir)
    {
        async Task Shot(string label)
        {
            await PlaytestTerrainReady();
            await PlaytestWait(0.3);
            SaveViewportPng(Path.Combine(dir, $"ui-{label}.png"));
        }

        // Dev and test runs are a 1280×720 window, never fullscreen (StartupDisplay).
        if (DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Windowed)
        {
            return Fail($"finestra {DisplayServer.WindowGetMode()} invece che in finestra");
        }

        var windowSize = DisplayServer.WindowGetSize();
        var devSize = new Vector2I(StartupDisplay.DevWidth, StartupDisplay.DevHeight);
        if (DisplayServer.GetName() != "headless" && windowSize != devSize)
        {
            // A screen too small for 1280×720 plus title bar and borders: the OS shrinks the window, not a bug.
            var usable = DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen()).Size;
            if (usable.X >= devSize.X + 16 && usable.Y >= devSize.Y + 64)
            {
                return Fail($"finestra {windowSize} invece di {devSize} (schermo utile {usable})");
            }

            GD.Print($"PLAYTEST finestra {windowSize}: lo schermo utile {usable} non contiene 1280×720");
        }

        // Leaving Home before playing (Esci / window close) must not write the startup sandbox to Continua.
        AutoSaveContinue(quiet: true);
        if (FactorySaveStore.Exists(FactorySaveStore.ContinueSlotId))
        {
            return Fail("autosave su Continua senza una partita giocata");
        }

        // Empty temp profile: Home has no Continua button at all.
        _home?.RefreshContinua();
        if (_home?.ContinuaShown != false)
        {
            return Fail("Home: pulsante Continua mostrato senza salvataggi");
        }

        // The Home card is as tall as its buttons: no blank band where Continua would be.
        if (await PlaytestHomeCardBlankAsync() is var blankWithout and > 2f)
        {
            return Fail($"Home senza Continua: {blankWithout:0} di spazio vuoto nella scheda");
        }

        // The Home shows the generated logo; behind it the world takes no input and, in a playtest, stays still.
        await Shot("home");
        if (_home?.FindChild("Logo", recursive: true, owned: false) is not TextureRect { Texture: not null }
            || GetNodeOrNull<WorldCamera>("Camera") is not { InputEnabled: false, DriftSpeed: 0f })
        {
            return Fail("Home: logo mancante, o telecamera dietro la Home che accetta input o si muove nel playtest");
        }

        if (!await PlaytestHomeDriftAsync())
        {
            return false;
        }

        // Esc on the start Home asks before quitting; a second Esc cancels and the game keeps running.
        await PlaytestKey(Key.Escape);
        await PlaytestWait(0.2);
        if (_home?.QuitConfirmOpen != true)
        {
            return Fail("Home iniziale: Esc non apre la conferma di uscita");
        }

        await Shot("home-esci");
        await PlaytestKey(Key.Escape);
        await PlaytestWait(0.2);
        if (_home?.QuitConfirmOpen != false || _home.IsOpen != true || !IsInsideTree())
        {
            return Fail("Home iniziale: secondo Esc non annulla la conferma di uscita");
        }

        _settings.TutorialCompleted = true;
        // Home → Nuova partita, with the playtest seed in the field instead of a random one.
        OpenNewGamePanel(PlaytestSeed);
        await Shot("nuova-partita");
        if (_newGame?.IsOpen != true || _home?.IsOpen == true)
        {
            return Fail("Nuova partita: pannello non aperto sopra Home");
        }

        OnHomeNewGame();
        _tutorial?.Close();
        if (_newGame?.IsOpen == true || _view?.Terrain is not { Generator: MapGenerator.Name, Width: (int)MapSize.Media })
        {
            return Fail("Nuova partita: attesa mappa a biomi Media");
        }

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
        if (ClientSettings.PlaytestForcesEffectsOff && PlaytestLooksOff() is { } looks)
        {
            return Fail(looks);
        }

        if (GetNodeOrNull<Camera2D>("Camera") is { } cam)
        {
            var core = new GridPosition(_view!.CoreTiles.Min(t => t.X), _view.CoreTiles.Min(t => t.Y));
            cam.Position = CellCenter(new GridPosition(core.X - 1, core.Y));
            cam.Zoom = new Vector2(1.6f, 1.6f);
            await Shot("belt-into-core");
            await PlaytestOutputCornersShotAsync(cam, Shot);
            FocusCameraOnCore();
        }

        await PlaytestKey(Key.Key2);
        await Shot("palette-info");
        // The placement preview (Minatore, 2×2): on free ground (accent outline) and on the Core (red).
        if (PlaytestFreeFootprint(Miner.Size) is { } free)
        {
            PlaytestMove(CellToScreen(free), 0);
            await Shot("fantasma-valido");
        }
        else
        {
            return Fail("nessuna cella libera vicino al Core per il fantasma");
        }

        PlaytestMove(CellToScreen(new GridPosition(_view!.CoreTiles.Min(t => t.X), _view.CoreTiles.Min(t => t.Y))), 0);
        await Shot("fantasma-non-valido");
        await PlaytestKey(Key.Escape);
        if (!await PlaytestRefusalToastAsync(Shot))
        {
            return false;
        }

        await PlaytestKey(Key.M);
        await Shot("mercato");
        await PlaytestKey(Key.M);

        await PlaytestKey(Key.T);
        await Shot("ricerca");
        await PlaytestKey(Key.T);

        if (!await PlaytestLanguageSwitchAsync(Shot))
        {
            return false;
        }

        if (OS.GetEnvironment(PlaytestPerfEnv) == "1")
        {
            await PlaytestBeltPerfAsync();
        }

        if (OS.GetEnvironment(PlaytestBlocksEnv) == "1")
        {
            await PlaytestBlocksShotAsync(dir);
        }

        // Effects probe (G3): only with effects forced on, so the usual captures never see a particle.
        if (OS.GetEnvironment(ClientSettings.PlaytestEffectsEnv) == "1" && !await PlaytestEffectsAsync(dir))
        {
            return false;
        }

        if (!await PlaytestRemoveDragOutAndBackAsync())
        {
            return false;
        }

        // Colour variants (G4 5/6) while the seed line is still marked for removal.
        if (!await PlaytestColourVariantsAsync(Shot))
        {
            return false;
        }

        AutoSaveContinue(quiet: true);
        if (!FactorySaveStore.Exists(FactorySaveStore.ContinueSlotId))
        {
            return Fail("autosave su Continua mancante dopo una partita giocata");
        }

        // With the save, Home shows Continua again.
        _home?.RefreshContinua();
        if (_home?.ContinuaShown != true)
        {
            return Fail("Home: pulsante Continua assente con un salvataggio");
        }

        if (await PlaytestHomeCardBlankAsync() is var blankWith and > 2f)
        {
            return Fail($"Home con Continua: {blankWith:0} di spazio vuoto nella scheda");
        }

        if (_view!.CoreDeliveredItems <= 0)
        {
            return Fail("nessun oggetto consegnato al CORE");
        }

        // A campaign level starts on its map file (data/maps), not on a generated map.
        if (_campaign?.FirstLevel is not { } level)
        {
            return Fail("campagna senza livelli");
        }

        // The seed line being taken apart: its sites carry the removal hatching, not just the colour.
        if (!await PlaytestWaitUntil(() => _constructionLayer?.HatchedSites > 0 || LocalQueueEmpty(), StepTimeout)
            || _constructionLayer?.HatchedSites is not > 0)
        {
            return Fail($"smontaggio: nessun cantiere in smontaggio tratteggiato (cantieri {_view.Sites.Count})");
        }

        GD.Print($"PLAYTEST step ok: {_constructionLayer.HatchedSites} cantieri in smontaggio tratteggiati");

        // Let the drone finish the deconstruction first: a bar still moving in the queue panel would make the
        // next capture depend on timing.
        if (!await PlaytestWaitUntil(LocalQueueEmpty, StepTimeout))
        {
            return Fail("smontaggio non finito prima della campagna");
        }

        // Auto-junction of a belt drag (real mouse drag, drone builds it), then back at rest.
        if (!await PlaytestAutoJunctionAsync(Shot))
        {
            return false;
        }

        // Terrain art captures here, with the game at rest: nothing moves while the camera looks around.
        await PlaytestTerrainShotsAsync(Shot);

        // A level whose map file is missing: the list stays open with the reason, the game is untouched.
        var before = _view;
        ToggleCampaign();
        OnCampaignLevelChosen(level with { Map = "mappa-inesistente" });
        if (_campaignSelect?.IsOpen != true || !ReferenceEquals(_view, before))
        {
            return Fail("livello con mappa mancante: elenco chiuso o partita cambiata");
        }

        await Shot("campagna-mappa-mancante");
        _campaignSelect.Close();

        if (StartCampaignLevel(level, toast: true) is { } campaignError)
        {
            return Fail($"livello {level.Id}: {campaignError}");
        }

        if (_view?.Terrain?.Generator != CampaignMapFile.GeneratorPrefix + level.Map)
        {
            return Fail($"livello {level.Id}: mappa {_view?.Terrain?.Generator ?? "assente"} invece del file {level.Map}");
        }

        await Shot("campagna");
        if (!PlaytestPaletteCategoryFollowsLocks())
        {
            return false;
        }

        if (!await PlaytestPaletteFitsAsync(Shot))
        {
            return false;
        }

        if (GetNodeOrNull<Camera2D>("Camera") is { } campaignCam)
        {
            // The level start with the terrain art (the shot above is at the colour level of detail).
            campaignCam.Zoom = Vector2.One;
            await Shot("terreno-campagna");
        }

        // UI scale (G4): every panel at 100/150/200%, the world unchanged, a mouse click on the right cell at 200%.
        if (!await PlaytestUiScaleAsync(Shot))
        {
            return false;
        }

        // Graphics settings and effect manager (G3), after every other capture: it changes and restores settings.
        if (!await PlaytestGraphicsSettingsAsync(dir, Shot))
        {
            return false;
        }

        if (OS.GetEnvironment(PlaytestPerfEnv) == "1")
        {
            await PlaytestHugeMapPerfAsync(dir);
        }

        return true;
    }

    /// <summary>Set to 1 to run <see cref="PlaytestBeltPerfAsync"/> in the <c>ui</c> playtest.</summary>
    private const string PlaytestPerfEnv = "TINDUSTRY_PLAYTEST_PERF";

    /// <summary>
    /// A miner and a smelter each feeding a belt on its side that turns away: both cells next to the buildings
    /// must be curves (Mindustry blending), like the belt-to-belt turn above them. Everything is taken away
    /// again without refund, so later captures see the same game.
    /// </summary>
    private async Task PlaytestOutputCornersShotAsync(Camera2D cam, Func<string, Task> shot)
    {
        if (_view is not Factory factory || _view.Terrain is not { } terrain)
        {
            return;
        }

        var core = new GridPosition(_view.CoreTiles.Min(t => t.X), _view.CoreTiles.Min(t => t.Y));
        // Miner at the anchor, smelter 5 cells east; each feeds a belt on its east side that heads North,
        // then turns East (a belt-to-belt turn for comparison).
        BuildSpec[] Layout(GridPosition a)
        {
            BuildSpec Belt(int dx, int dy, Direction dir) =>
                new(BuildSpec.DefaultConveyorId, null, new GridPosition(a.X + dx, a.Y + dy), dir);
            return
            [
                new BuildSpec(Miner.BasicId, BuildingKind.Miner, a, Direction.East),
                new BuildSpec(Crafter.SmelterBuildingId, BuildingKind.Smelter, new GridPosition(a.X + 5, a.Y), Direction.East),
                Belt(2, 1, Direction.North), Belt(2, 0, Direction.North), Belt(2, -1, Direction.East), Belt(3, -1, Direction.East),
                Belt(7, 1, Direction.North), Belt(7, 0, Direction.North), Belt(7, -1, Direction.East), Belt(8, -1, Direction.East)
            ];
        }

        // Plain ground only: an idle miner keeps the capture still.
        bool Free(GridPosition a)
        {
            for (var y = a.Y - 1; y <= a.Y + 1; y++)
            {
                for (var x = a.X; x <= a.X + 8; x++)
                {
                    var cell = new GridPosition(x, y);
                    if (!terrain.InBounds(cell) || terrain[cell].Deposit != DepositKind.None
                        || !factory.CanPlace(new BuildSpec(BuildSpec.DefaultConveyorId, null, cell, Direction.East), out _))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        GridPosition? anchor = null;
        for (var dy = 4; dy < 40 && anchor is null; dy++)
        {
            foreach (var y in new[] { core.Y + dy, core.Y - dy })
            {
                var a = new GridPosition(core.X - 4, y);
                if (Free(a))
                {
                    anchor = a;
                    break;
                }
            }
        }

        if (anchor is not { } at)
        {
            GD.Print("PLAYTEST curve: nessuno spazio libero");
            return;
        }

        // Hold the sim so the extra frames do not shift the timing of the later captures.
        PlaytestHeld = true;
        try
        {
            await PlaytestOutputCornersAsync(cam, shot, factory, at, Layout(at));
            await PlaytestBuildingsBesideBeltAsync(cam, shot, factory, terrain, core);
        }
        finally
        {
            PlaytestHeld = false;
        }
    }

    private async Task PlaytestOutputCornersAsync(
        Camera2D cam, Func<string, Task> shot, Factory factory, GridPosition at, BuildSpec[] layout)
    {
        foreach (var spec in layout)
        {
            if (!factory.PlaceInstant(spec, charge: false))
            {
                GD.Print($"PLAYTEST curve: {spec.StructureId} su {spec.Origin} rifiutato");
            }
        }

        var curves = new[] { new GridPosition(at.X + 2, at.Y + 1), new GridPosition(at.X + 7, at.Y + 1) }
            .Count(factory.IsBeltCorner);
        GD.Print($"PLAYTEST curve: {curves}/2 curve all'uscita degli edifici da {at}");
        _visualDirty = true;
        _buildingsDirty = true;
        cam.Position = new Vector2((at.X + 4.5f) * TileSize, (at.Y + 0.5f) * TileSize);
        cam.Zoom = new Vector2(1.6f, 1.6f);
        await PlaytestWait(0.3);
        await shot("curva-uscita");

        foreach (var spec in layout)
        {
            factory.RemoveInstant(spec.Origin, refund: false);
        }

        _visualDirty = true;
        _buildingsDirty = true;
        await PlaytestWait(0.1);

        // A belt running past a miner: both cells beside it are side joins open towards the miner.
        BuildSpec[] past =
        [
            new(Miner.BasicId, BuildingKind.Miner, new GridPosition(at.X + 1, at.Y), Direction.East),
            .. Enumerable.Range(0, 5).Select(dx =>
                new BuildSpec(BuildSpec.DefaultConveyorId, null, new GridPosition(at.X + dx, at.Y - 1), Direction.East))
        ];
        foreach (var spec in past)
        {
            factory.PlaceInstant(spec, charge: false);
        }

        GD.Print($"PLAYTEST nastro accanto al minatore: {factory.GetBeltShape(past[2].Origin)}, {factory.GetBeltShape(past[3].Origin)}");
        _visualDirty = true;
        _buildingsDirty = true;
        await PlaytestWait(0.3);
        await shot("nastro-accanto-minatore");

        foreach (var spec in past)
        {
            factory.RemoveInstant(spec.Origin, refund: false);
        }

        _visualDirty = true;
        _buildingsDirty = true;
        await PlaytestWait(0.1);
        await PlaytestTMergeShotAsync(cam, shot, factory, at);
    }

    /// <summary>
    /// Performance probe (only with <c>TINDUSTRY_PLAYTEST_PERF=1</c>), no screenshot and no pass/fail: lays 200 belts (a 20×10 serpentine of the three
    /// tiers, so with corners) below the Core and, below them, 100 layered 2×2 buildings, all working: 25 smelters,
    /// 25 assemblers (fed), 25 generators (fuelled) in a packed grid and 25 basic miners on the first free deposits.
    /// Frames belts and grid at 1280×720 and prints average and minimum FPS over 3 s without VSync, with the node
    /// count and how many buildings were working during the measure.
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

        // 100 layered buildings: a 15×5 grid of 2×2 crafters and generators, then 25 miners on deposits.
        const int gridCols = 15, gridRows = 5;
        var gridAt = new GridPosition(origin.X - 5, origin.Y + rows + 1);
        var buildings = 0;
        for (var i = 0; i < gridCols * gridRows; i++)
        {
            var at = new GridPosition(gridAt.X + i % gridCols * 2, gridAt.Y + i / gridCols * 2);
            var (id, kind) = (i % 3) switch
            {
                0 => (Crafter.SmelterBuildingId, BuildingKind.Smelter),
                1 => (Crafter.AssemblerBuildingId, BuildingKind.Assembler),
                _ => (Generator.BuildingId, BuildingKind.Generator)
            };
            if (factory.PlaceInstant(new BuildSpec(id, kind, at, Direction.East), charge: false))
            {
                buildings++;
            }
        }

        if (_view.Terrain is { } terrain)
        {
            var miners = 0;
            for (var y = 0; y + 1 < terrain.Height && miners < 25; y++)
            {
                for (var x = 0; x + 1 < terrain.Width && miners < 25; x++)
                {
                    var at = new GridPosition(x, y);
                    var onDeposit = new[] { at, new GridPosition(x + 1, y), new GridPosition(x, y + 1), new GridPosition(x + 1, y + 1) }
                        .All(c => terrain[c].Deposit != DepositKind.None);
                    if (onDeposit && factory.PlaceInstant(new BuildSpec(Miner.BasicId, BuildingKind.Miner, at, Direction.East), charge: false))
                    {
                        miners++;
                    }
                }
            }

            buildings += miners;
        }

        foreach (var gen in factory.Generators)
        {
            gen.RestoreFuel(Generator.FuelBufferCapacity, Generator.SecondsPerFuel);
        }

        foreach (var crafter in factory.Smelters.Concat(factory.Assemblers))
        {
            crafter.SeedFuel();
            foreach (var input in crafter.Recipe.Inputs)
            {
                for (var i = 0; i < input.Amount * 20; i++)
                {
                    crafter.TryAccept(input.ItemId);
                }
            }
        }

        _visualDirty = true;
        _buildingsDirty = true;
        var bottom = gridAt.Y + gridRows * 2;
        cam.Position = new Vector2((origin.X + cols * 0.5f) * TileSize, (origin.Y + bottom) * 0.5f * TileSize);
        cam.Zoom = new Vector2(0.4f, 0.4f);
        // Uncapped: VSync would pin every run to the monitor's refresh rate.
        var vsync = DisplayServer.WindowGetVsyncMode();
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        await PlaytestWait(1.0);

        var working = 0;
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
            working = Math.Max(working, factory.Smelters.Concat(factory.Assemblers).Count(c => c.IsCrafting)
                + factory.Generators.Count(g => g.IsGenerating) + factory.Miners.Count(m => m.Efficiency > 0f));
        }

        DisplayServer.WindowSetVsyncMode(vsync);
        var average = frames / ((last - start) / 1e6);
        var nodes = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
        GD.Print($"PLAYTEST perf: {placed} nastri, {buildings} edifici a strati ({working} al lavoro), "
            + $"fps medio {average:F0}, minimo {1e6 / Math.Max(1UL, worst):F0}, nodi {nodes:F0}");
    }

    /// <summary>
    /// Second performance probe (with <c>TINDUSTRY_PLAYTEST_PERF=1</c>, at the end of the <c>ui</c> playtest), no
    /// pass/fail: a new Enorme map (2000², seed 42) at minimum zoom, saved as <c>ui-enorme-zoom-min.png</c>; average
    /// and minimum FPS over 3 s standing still and 3 s sweeping the camera across the map (chunks built and freed
    /// on the way), without VSync, with chunk counts and memory (Godot static, .NET heap, video, process). Then the
    /// terrain art (G2b): zoom 0.08 → 2 → 0.08 in 4 s each way and 4 s panning at zoom 1, with average and minimum
    /// FPS, slowest frame, slowest art chunk, most art chunks alive and the highest process memory seen. Last, with
    /// effects on, the night with hundreds of lit lamps (<see cref="PlaytestHugeMapNightAsync"/>).
    /// </summary>
    private async Task PlaytestHugeMapPerfAsync(string dir)
    {
        // The overlay's look (it is up for only one blocking frame below).
        if (_mapOverlay is not null)
        {
            _mapOverlay.Visible = true;
            await PlaytestWait(0.3);
            SaveViewportPng(Path.Combine(dir, "ui-generazione-mappa.png"));
            _mapOverlay.Visible = false;
        }

        // Through the generation overlay, as the Nuova partita panel does; timed from the blocking work only.
        var startMs = await WithMapOverlayAsync((int)MapSize.Enorme, () =>
        {
            var started = Time.GetTicksUsec();
            StartNewSandbox(MapSize.Enorme, PlaytestSeed, toast: false);
            return (Time.GetTicksUsec() - started) / 1000.0;
        });
        if (GetNodeOrNull<WorldCamera>("Camera") is not { } cam || _terrainChunks is null)
        {
            return;
        }

        cam.Zoom = new Vector2(cam.MinZoom, cam.MinZoom);
        cam.ClampToMap();
        // Past the campaign toast (1.6 s + fade).
        await PlaytestWait(2.5);
        SaveViewportPng(Path.Combine(dir, "ui-enorme-zoom-min.png"));

        var vsync = DisplayServer.WindowGetVsyncMode();
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        var still = await MeasureFramesAsync(3.0, null);
        // Sweep diagonally, 4 cells east and 3 south per frame, bouncing off the map edges: new chunks on the way
        // and, past the cap, the oldest ones freed.
        var velocity = new Vector2(4f, 3f) * TileSize;
        var sweep = await MeasureFramesAsync(3.0, () =>
        {
            var before = cam.Position;
            cam.Position += velocity;
            cam.ClampToMap();
            if (Math.Abs(cam.Position.X - before.X) < 1f)
            {
                velocity.X = -velocity.X;
            }

            if (Math.Abs(cam.Position.Y - before.Y) < 1f)
            {
                velocity.Y = -velocity.Y;
            }
        });
        // The colour level of detail first (same report as before the terrain art), then the art one.
        const double mib = 1024.0 * 1024.0;
        var video = Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed);
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        GD.Print($"PLAYTEST perf enorme: {ActiveMapWidth}×{ActiveMapHeight}, partenza {startMs:F0} ms, zoom {cam.Zoom.X.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}; "
            + $"fermo fps medio {still.Average:F0}, minimo {still.Minimum:F0}; "
            + $"panoramica fps medio {sweep.Average:F0}, minimo {sweep.Minimum:F0}; "
            + $"pezzi vivi {_terrainChunks.ChunkCount}, creati {_terrainChunks.BuiltSinceReset}; "
            + $"memoria statica {OS.GetStaticMemoryUsage() / mib:F0} MiB, .NET {GC.GetTotalMemory(false) / mib:F0} MiB, "
            + $"video {video / mib:F0} MiB, processo {process.WorkingSet64 / mib:F0} MiB");
        GD.Print(await PlaytestTerrainArtPerfAsync(cam));
        GD.Print(await PlaytestHugeMapNightAsync(cam));
        DisplayServer.WindowSetVsyncMode(vsync);
    }

    /// <summary>
    /// The art part of <see cref="PlaytestHugeMapPerfAsync"/> (VSync already off): zoom sweep around the current
    /// position, then a diagonal pan at zoom 1 (40 cells/s, bouncing off the map edges); returns the report line.
    /// </summary>
    private async Task<string> PlaytestTerrainArtPerfAsync(WorldCamera cam)
    {
        var chunks = _terrainChunks!;
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var peakMemory = 0L;
        var peakVideo = 0.0;
        var frame = 0;
        void SampleMemory()
        {
            // Every 15 frames: refreshing the process info is not free.
            if (frame++ % 15 == 0)
            {
                process.Refresh();
                peakMemory = Math.Max(peakMemory, process.WorkingSet64);
                peakVideo = Math.Max(peakVideo, Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed));
            }
        }

        const double seconds = 4.0;
        var low = Math.Log(cam.MinZoom);
        var high = Math.Log(2.0);
        chunks.ResetProbe();
        var zoomStart = Time.GetTicksUsec();
        var zoom = await MeasureFramesAsync(2 * seconds, () =>
        {
            SampleMemory();
            var t = (Time.GetTicksUsec() - zoomStart) / 1e6 / seconds; // 0..2
            var k = t <= 1 ? t : Math.Max(0, 2 - t);
            var z = (float)Math.Exp(low + (high - low) * k);
            cam.Zoom = new Vector2(z, z);
            cam.ClampToMap();
        });
        var zoomPeak = chunks.ArtPeak;
        var zoomSlowest = chunks.SlowestArtMs;

        cam.Zoom = Vector2.One;
        cam.ClampToMap();
        chunks.ResetProbe();
        var velocity = new Vector2(40f, 28f) * TileSize; // per second
        var last = Time.GetTicksUsec();
        var pan = await MeasureFramesAsync(seconds, () =>
        {
            SampleMemory();
            var now = Time.GetTicksUsec();
            var before = cam.Position;
            cam.Position += velocity * (float)((now - last) / 1e6);
            last = now;
            cam.ClampToMap();
            if (Math.Abs(cam.Position.X - before.X) < 0.01f)
            {
                velocity.X = -velocity.X;
            }

            if (Math.Abs(cam.Position.Y - before.Y) < 0.01f)
            {
                velocity.Y = -velocity.Y;
            }
        });
        process.Refresh();
        peakMemory = Math.Max(peakMemory, process.WorkingSet64);
        peakVideo = Math.Max(peakVideo, Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed));
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return string.Create(inv,
            $"PLAYTEST perf arte: zoom 0.08-2-0.08 fps medio {zoom.Average:F0}, minimo {zoom.Minimum:F0}, "
            + $"frame più lento {1000 / zoom.Minimum:F1} ms, pezzi d'arte max {zoomPeak}, pezzo più lento {zoomSlowest:F1} ms; "
            + $"scorrimento a zoom 1 fps medio {pan.Average:F0}, minimo {pan.Minimum:F0}, "
            + $"frame più lento {1000 / pan.Minimum:F1} ms, pezzi d'arte max {chunks.ArtPeak}, pezzo più lento {chunks.SlowestArtMs:F1} ms; "
            + $"pezzi d'arte creati {chunks.ArtBuiltSinceReset}; memoria massima del processo {peakMemory / (1024.0 * 1024.0):F0} MiB, "
            + $"video massima {peakVideo / (1024.0 * 1024.0):F0} MiB");
    }

    /// <summary>Waits (at most 10 s) until the terrain art of the view is composed, so captures never catch colours.</summary>
    private async Task PlaytestTerrainReady()
    {
        // One frame first: the counters must come from a Sync after the latest camera move or zoom.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (var i = 0; i < 600 && _terrainChunks is { ArtShown: true, ArtPending: > 0 }; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        if (_terrainChunks is { ArtShown: true, ArtPending: > 0 } chunks)
        {
            // The capture would catch colour chunks: the playtest log gets an ERROR line, so the run fails.
            GD.PushError($"PLAYTEST: arte del terreno non pronta dopo 600 frame ({chunks.ArtPending} pezzi mancanti)");
        }
    }

    /// <summary>
    /// Terrain art captures on the playtest map (G2b): the start at zoom 1 (<c>ui-terreno-partenza</c>), the window with
    /// the most biomes (water counts as one) at zoom 0.5 (<c>ui-terreno-biomi</c>) and the one with the most ore kinds,
    /// away from the HUD panels, at zoom 0.5 (<c>ui-terreno-minerali</c>); the camera goes back to the Core.
    /// </summary>
    private async Task PlaytestTerrainShotsAsync(Func<string, Task> shot)
    {
        if (_view?.Terrain is not { } map || GetNodeOrNull<WorldCamera>("Camera") is not { } cam)
        {
            return;
        }

        FocusCameraOnCore();
        cam.Zoom = Vector2.One;
        cam.ClampToMap();
        await shot("terreno-partenza");

        var table = _view.Content.Terrain;
        var biomes = TerrainTable.Biomes.ToList();
        var biomeOf = Enum.GetValues<FloorKind>()
            .Select(k => table.Floor(k) is { } f ? biomes.IndexOf(f.Biome) : -1)
            .ToArray();
        var cells = GetViewportRect().Size / TileSize;
        var (biomesAt, _) = BestWindow(map, (int)(cells.X * 2), (int)(cells.Y * 2), 8, biomes.Count, (x, y, seen) =>
        {
            var b = biomeOf[(int)map[x, y].Floor];
            if (b >= 0)
            {
                seen[b] = true;
            }
        });
        cam.Position = biomesAt * TileSize;
        cam.Zoom = new Vector2(0.5f, 0.5f);
        cam.ClampToMap();
        await shot("terreno-biomi");

        // Scored on the middle of the zoom 0.5 view only, so no ore hides under the HUD panels in the corners.
        var ores = Enum.GetValues<DepositKind>().Length - 1;
        var (oresAt, oreKinds) = BestWindow(map, (int)(cells.X * 1.5f), (int)(cells.Y * 1.4f), 4, ores + 1, (x, y, seen) =>
        {
            var d = (int)map[x, y].Deposit;
            if (d > 0)
            {
                seen[d] = true;
            }
        });
        GD.Print($"PLAYTEST terreno: {oreKinds} minerali su {ores} nella cattura dei minerali");
        cam.Position = oresAt * TileSize;
        cam.Zoom = new Vector2(0.5f, 0.5f);
        cam.ClampToMap();
        await shot("terreno-minerali");
        FocusCameraOnCore();
    }

    /// <summary>
    /// Centre (in cells) and number of kinds of the <paramref name="w"/>×<paramref name="h"/> window, on a grid of <paramref name="step"/>
    /// cells, where <paramref name="mark"/> sees the most of <paramref name="kinds"/> distinct kinds (every other cell
    /// sampled); the first in row order wins a tie, so the choice is the same on every run.
    /// </summary>
    private static (Vector2 Centre, int Kinds) BestWindow(
        TerrainMap map, int w, int h, int step, int kinds, Action<int, int, bool[]> mark)
    {
        var best = (Score: -1, X: 0, Y: 0);
        var seen = new bool[kinds];
        for (var y0 = 0; y0 + h <= map.Height; y0 += step)
        {
            for (var x0 = 0; x0 + w <= map.Width; x0 += step)
            {
                Array.Clear(seen);
                for (var y = y0; y < y0 + h; y += 2)
                {
                    for (var x = x0; x < x0 + w; x += 2)
                    {
                        mark(x, y, seen);
                    }
                }

                var score = seen.Count(v => v);
                if (score > best.Score)
                {
                    best = (score, x0, y0);
                }
            }
        }

        return (new Vector2(best.X + w / 2f, best.Y + h / 2f), best.Score);
    }

    /// <summary>Frames for <paramref name="seconds"/>, running <paramref name="perFrame"/> before each: average and worst-frame FPS.</summary>
    private async Task<(double Average, double Minimum)> MeasureFramesAsync(double seconds, Action? perFrame)
    {
        var frames = 0;
        var worst = 0UL;
        var start = Time.GetTicksUsec();
        var last = start;
        while (Time.GetTicksUsec() - start < (ulong)(seconds * 1e6))
        {
            perFrame?.Invoke();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var now = Time.GetTicksUsec();
            worst = Math.Max(worst, now - last);
            last = now;
            frames++;
        }

        return (frames / ((last - start) / 1e6), 1e6 / Math.Max(1UL, worst));
    }

    /// <summary>Set to 1 to run <see cref="PlaytestBlocksShotAsync"/> in the <c>ui</c> playtest.</summary>
    private const string PlaytestBlocksEnv = "TINDUSTRY_PLAYTEST_BLOCCHI";

    /// <summary>
    /// Art check (only with <c>TINDUSTRY_PLAYTEST_BLOCCHI=1</c>), no pass/fail: builds every production and power
    /// block twice, idle (off a deposit, no input, no fuel) and working (miners on a deposit, the advanced one beside a
    /// burning generator, smelter and assembler fed), and saves <c>ui-blocchi-spenti.png</c>,
    /// <c>ui-blocchi-accesi.png</c>, both groups at zoom 0.4 (<c>ui-blocchi-zoom04.png</c>) and the Core with its drone
    /// up close (<c>ui-blocchi-core.png</c>), and every item on each belt tier (<c>ui-blocchi-oggetti.png</c>).
    /// </summary>
    private async Task PlaytestBlocksShotAsync(string dir)
    {
        if (_view is not Factory factory || _view.Terrain is not { } terrain || GetNodeOrNull<Camera2D>("Camera") is not { } cam)
        {
            return;
        }

        var core = new GridPosition(_view.CoreTiles.Min(t => t.X), _view.CoreTiles.Min(t => t.Y));
        void Place(string id, BuildingKind kind, GridPosition at)
        {
            if (!factory.PlaceInstant(new BuildSpec(id, kind, at, Direction.East), charge: false))
            {
                GD.Print($"PLAYTEST blocchi: {id} su {at} rifiutato");
            }
        }

        bool Free(GridPosition origin, int w, int h, bool deposit)
        {
            for (var y = origin.Y; y < origin.Y + h; y++)
            {
                for (var x = origin.X; x < origin.X + w; x++)
                {
                    var cell = new GridPosition(x, y);
                    if (!terrain.InBounds(cell) || (terrain[cell].Deposit != DepositKind.None) != deposit
                        || !factory.CanPlace(new BuildSpec(BuildSpec.DefaultConveyorId, null, cell, Direction.East), out _))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // Nearest free w×h spot around the Core (square rings), on or off a deposit.
        GridPosition? Find(int w, int h, bool deposit)
        {
            for (var r = 3; r < 60; r++)
            {
                for (var dy = -r; dy <= r; dy++)
                {
                    for (var dx = -r; dx <= r; dx++)
                    {
                        var at = new GridPosition(core.X + dx, core.Y + dy);
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && Free(at, w, h, deposit))
                        {
                            return at;
                        }
                    }
                }
            }

            return null;
        }

        // Idle group: one row on plain ground.
        if (Find(17, 2, deposit: false) is not { } off)
        {
            GD.Print("PLAYTEST blocchi: nessuno spazio libero");
            return;
        }

        Place(Miner.BasicId, BuildingKind.Miner, off);
        Place(Miner.AdvancedId, BuildingKind.MinerAdvanced, new GridPosition(off.X + 3, off.Y));
        Place(Crafter.SmelterBuildingId, BuildingKind.Smelter, new GridPosition(off.X + 6, off.Y));
        Place(Crafter.AssemblerBuildingId, BuildingKind.Assembler, new GridPosition(off.X + 9, off.Y));
        Place(Generator.BuildingId, BuildingKind.Generator, new GridPosition(off.X + 12, off.Y));
        Place("extractor", BuildingKind.Extractor, new GridPosition(off.X + 15, off.Y));
        Place("power-node", BuildingKind.PowerNode, new GridPosition(off.X + 16, off.Y + 1));

        // Working group: miners on a deposit (the advanced one powered by the generator beside it).
        var on = Find(2, 2, deposit: true) ?? off;
        var genAt = new GridPosition(on.X + 2, on.Y);
        Place(Miner.AdvancedId, BuildingKind.MinerAdvanced, on);
        Place(Generator.BuildingId, BuildingKind.Generator, genAt);
        var on2 = Find(2, 2, deposit: true) ?? on;
        Place(Miner.BasicId, BuildingKind.Miner, on2);

        var crafters = Find(11, 2, deposit: false) ?? off;
        Place(Crafter.SmelterBuildingId, BuildingKind.Smelter, crafters);
        Place(Crafter.AssemblerBuildingId, BuildingKind.Assembler, new GridPosition(crafters.X + 3, crafters.Y));
        Place("power-node-t2", BuildingKind.PowerNodeT2, new GridPosition(crafters.X + 6, crafters.Y));
        Place("extractor", BuildingKind.Extractor, new GridPosition(crafters.X + 9, crafters.Y));
        Place("power-node", BuildingKind.PowerNode, new GridPosition(crafters.X + 10, crafters.Y + 1));

        foreach (var gen in factory.Generators.Where(g => g.Position.Equals(genAt)))
        {
            gen.RestoreFuel(Generator.FuelBufferCapacity, Generator.SecondsPerFuel);
        }

        var crafterCells = new[] { crafters, new GridPosition(crafters.X + 3, crafters.Y) };
        foreach (var crafter in factory.Smelters.Concat(factory.Assemblers).Where(c => crafterCells.Contains(c.Position)))
        {
            crafter.SeedFuel();
            foreach (var input in crafter.Recipe.Inputs)
            {
                for (var i = 0; i < input.Amount * 3; i++)
                {
                    crafter.TryAccept(input.ItemId);
                }
            }
        }

        _buildingsDirty = true;
        async Task Frame(GridPosition a, GridPosition b, float zoom, string label)
        {
            cam.Position = new Vector2((a.X + b.X + 1) * 0.5f * TileSize, (a.Y + b.Y + 1) * 0.5f * TileSize);
            cam.Zoom = new Vector2(zoom, zoom);
            await PlaytestWait(0.6);
            SaveViewportPng(Path.Combine(dir, $"ui-blocchi-{label}.png"));
        }

        // 1280×720 at zoom z shows 20/z × 11/z cells; keep a margin for the HUD.
        static float Fit(GridPosition a, GridPosition b) =>
            Math.Min(1.2f, Math.Min(16f / (b.X - a.X + 1), 8f / (b.Y - a.Y + 1)));

        var offMax = new GridPosition(off.X + 16, off.Y + 1);
        await Frame(off, offMax, Fit(off, offMax), "spenti");
        GridPosition[] onCells = [on, new GridPosition(genAt.X + 1, genAt.Y + 1), on2, new GridPosition(on2.X + 1, on2.Y + 1),
            crafters, new GridPosition(crafters.X + 10, crafters.Y + 1)];
        var onMin = new GridPosition(onCells.Min(c => c.X), onCells.Min(c => c.Y));
        var onMax = new GridPosition(onCells.Max(c => c.X), onCells.Max(c => c.Y));
        await Frame(onMin, onMax, Fit(onMin, onMax), "accesi");
        var all = onCells.Append(off).Append(offMax).ToArray();
        await Frame(new GridPosition(all.Min(c => c.X), all.Min(c => c.Y)), new GridPosition(all.Max(c => c.X), all.Max(c => c.Y)), 0.4f, "zoom04");
        // Close-up of the Core (core-1 with its nucleus light) and the drone resting beside it.
        await Frame(new GridPosition(core.X - 2, core.Y - 1), new GridPosition(core.X + 3, core.Y + 2), 2f, "core");

        // Every item on each belt tier: three rows of 12 belts heading East, one item per cell.
        string[] itemIds = [.. _view.Content.Market.Select(m => m.ItemId), "money"];
        string[] tiers = ["conveyor-basic", "conveyor-fast", "conveyor-express"];
        if (Find(itemIds.Length, tiers.Length, deposit: false) is { } beltsAt)
        {
            var nextId = 900_000L;
            for (var r = 0; r < tiers.Length; r++)
            {
                for (var c = 0; c < itemIds.Length; c++)
                {
                    var cell = new GridPosition(beltsAt.X + c, beltsAt.Y + r);
                    factory.PlaceInstant(new BuildSpec(tiers[r], null, cell, Direction.East), charge: false);
                    var itemId = itemIds[(c + r) % itemIds.Length];
                    if (!factory.Belts.TryInsert(cell, new TransportedItem(nextId++, itemId, 0.5f)))
                    {
                        GD.Print($"PLAYTEST blocchi: {itemId} non inserito sul nastro {tiers[r]} in {cell}");
                    }
                }
            }

            _visualDirty = true;
            await Frame(beltsAt, new GridPosition(beltsAt.X + itemIds.Length - 1, beltsAt.Y + tiers.Length - 1), 1.2f, "oggetti");
        }

        GD.Print($"PLAYTEST blocchi: spenti da {off}, accesi da {on}, {on2} e {crafters}; "
                 + $"minatori al lavoro {factory.Miners.Count(m => m.Efficiency > 0f)}, alimentati {factory.Miners.Count(m => m.IsPowered)}, "
                 + $"forni/assemblatori attivi {factory.Smelters.Concat(factory.Assemblers).Count(c => c.IsCrafting)}, "
                 + $"generatori accesi {factory.Generators.Count(g => g.IsGenerating)}");
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
    internal Vector2 CellToScreen(GridPosition cell) =>
        ViewportToWindow(GetViewport().GetCanvasTransform() * CellCenter(cell));

    private Vector2 ViewportToWindow(Vector2 viewportPos) => GetViewport().GetFinalTransform() * viewportPos;

    private async Task PlaytestWait(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    /// <summary>Press and release <paramref name="key"/> with <paramref name="modifiers"/> held; <paramref name="unicode"/> types a character.</summary>
    internal async Task PlaytestKey(Key key, KeyModifierMask modifiers = 0, long unicode = 0)
    {
        foreach (var pressed in new[] { true, false })
        {
            var @event = new InputEventKey
            {
                Keycode = key, PhysicalKeycode = key, Pressed = pressed,
                Unicode = pressed ? unicode : 0,
                CtrlPressed = modifiers.HasFlag(KeyModifierMask.MaskCtrl),
                ShiftPressed = modifiers.HasFlag(KeyModifierMask.MaskShift),
                AltPressed = modifiers.HasFlag(KeyModifierMask.MaskAlt),
                MetaPressed = modifiers.HasFlag(KeyModifierMask.MaskMeta)
            };
            @event.SetMeta(SyntheticKeyMeta, true);
            Input.ParseInputEvent(@event);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    internal async Task PlaytestClick(Vector2 at, MouseButton button = MouseButton.Left)
    {
        PlaytestMove(at, 0);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = button, Pressed = true, Position = at, GlobalPosition = at,
            ButtonMask = button == MouseButton.Right ? MouseButtonMask.Right : MouseButtonMask.Left, Device = PlaytestDevice
        });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = button, Pressed = false, Position = at, GlobalPosition = at, Device = PlaytestDevice
        });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    internal Task PlaytestDrag(MouseButton button, Vector2 from, Vector2 to, int steps = 16) =>
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

    internal static void PlaytestMove(Vector2 at, MouseButtonMask mask) =>
        Input.ParseInputEvent(new InputEventMouseMotion
        {
            Position = at, GlobalPosition = at, ButtonMask = mask, Device = PlaytestDevice
        });

    /// <summary>Saves the current frame; the image is released at once (not left to the finalizer).</summary>
    /// <summary>
    /// The free footprint of <paramref name="size"/> nearest to <paramref name="around"/> (default: the Core's corner),
    /// searched on square rings from distance 3 to 10 in a fixed order, or null: for previews of a placeable block.
    /// </summary>
    private GridPosition? PlaytestFreeFootprint(int size, GridPosition? around = null)
    {
        if (_view is null || (around is null && _view.CoreTiles.Count == 0))
        {
            return null;
        }

        var centre = around ?? new GridPosition(_view.CoreTiles.Min(t => t.X), _view.CoreTiles.Min(t => t.Y));
        for (var r = 3; r <= 10; r++)
        {
            for (var dy = -r; dy <= r; dy++)
            {
                for (var dx = -r; dx <= r; dx++)
                {
                    var cell = new GridPosition(centre.X + dx, centre.Y + dy);
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && _view.CanOccupyFootprint(cell, size)
                        && !FootprintPlanned(cell, size))
                    {
                        return cell;
                    }
                }
            }
        }

        return null;
    }

    private void SaveViewportPng(string path)
    {
        using var image = CaptureFrame();
        image.SavePng(path);
    }

    /// <summary>
    /// The frame just drawn as the window shows it. With HDR 2D ("Bagliore") the viewport draws in linear light and its
    /// texture reads back linear: converted to sRGB here, pixel by pixel from the float data (an 8-bit conversion first
    /// would band the night's dark tones).
    /// </summary>
    internal Image CaptureFrame()
    {
        var image = GetViewport().GetTexture().GetImage();
        if (!GetViewport().UseHdr2D)
        {
            return image;
        }

        var width = image.GetWidth();
        var height = image.GetHeight();
        var srgb = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                srgb.SetPixel(x, y, image.GetPixel(x, y).LinearToSrgb());
            }
        }

        image.Dispose();
        return srgb;
    }

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
