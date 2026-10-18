using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>Game session flow: Home/Campagna/Nuova partita, camera, campaign completion, research locks, save/load.</summary>
public partial class World
{
    private async Task OnHomeContinuaAsync()
    {
        if (!FactorySaveStore.Exists(FactorySaveStore.ContinueSlotId))
        {
            _home?.RefreshContinua();
            _hud?.ShowToast("Nessun salvataggio continua");
            return;
        }

        var loaded = await WithMapOverlayAsync(
            SlotMapEdge(FactorySaveStore.ContinueSlotId),
            () => LoadGame(FactorySaveStore.ContinueSlotId, "Continua caricata", showOnPanel: m => _home?.ShowError(m)));
        if (!loaded)
        {
            return; // stay on Home with the error visible
        }

        _home?.Close();
        BeginTutorialIfNeeded();
    }

    private void OnHomeCampaign()
    {
        _home?.Close();
        _returnHomeAfterCampaign = true;
        ToggleCampaign();
    }

    /// <summary>
    /// Nuova partita without the panel (scripted playtests): Media map, seed 42, biome generator. The generator's
    /// start guarantee keeps the tutorial's iron patch and belt corridor west of the Core.
    /// </summary>
    private void OnHomeNewGame() => StartNewGameFromHome(MapSize.Media, PlaytestSeed, night: false);

    /// <summary>Seed of the scripted playtests' sandbox.</summary>
    private const int PlaytestSeed = 42;

    /// <summary>Inizia on the Nuova partita panel: Grande and Enorme show the generation overlay first.</summary>
    private async Task StartNewGameFromPanelAsync(MapSize size, int seed, bool night) =>
        await WithMapOverlayAsync((int)size, () =>
        {
            StartNewGameFromHome(size, seed, night);
            return true;
        });

    private void StartNewGameFromHome(MapSize size, int seed, bool night)
    {
        StartNewSandbox(size, seed, toast: true, night);
        _newGame?.Close();
        _home?.Close();
        BeginTutorialIfNeeded();
    }

    /// <summary>
    /// Rules of the games the client starts: real construction, as in Mindustry (plans, drone, sites).
    /// The refund share is the mode's own (sandbox 100, campaign = the level's), set by <see cref="NewGame"/>.
    /// </summary>
    private static GameRules ClientRules(bool night = false) => new(Night: night);

    /// <summary>
    /// A normal game: square map of <paramref name="size"/> from the biome generator, Core at the centre, at night with
    /// <paramref name="night"/>.
    /// </summary>
    private static Factory NewSandboxFactory(FactoryContent content, MapSize size, int seed, bool night = false)
    {
        var edge = (int)size;
        return NewGame.Sandbox(
            content,
            Factory.CenteredCoreOrigin(edge, edge),
            coreSize: 2,
            startingMoney: 180,
            rules: ClientRules(night),
            map: MapGenSettings.Normal(size, seed));
    }

    private void StartNewSandbox(MapSize size, int seed, bool toast, bool night = false)
    {
        var content = FactoryContent.Load(_contentPath);
        AttachGame(NewSandboxFactory(content, size, seed, night));
        _continueAutoSave.MarkPlayed();
        _activeLevel = null;
        _levelCompleteToastShown = false;
        _tool = BuildTool.Cursor;
        _visualDirty = true;
        _buildingsDirty = true;
        RebuildBeltVisual();
        RebuildBuildingVisuals();
        SyncResearchLocks();
        UpdateHud();
        _hud?.ClearObjectives();
        SyncCameraMapBounds();
        FocusCameraOnCore();
        QueueRedraw();
        if (toast)
        {
            _hud?.ShowToast($"Nuova partita · {NewGamePanel.SizeLabel(size)} {(int)size}×{(int)size} · seme {seed}");
        }
    }

    /// <summary>Map edge of a save slot, reading only that slot's file (0 when unknown).</summary>
    private static int SlotMapEdge(string slotId) =>
        FactorySaveStore.ReadSlotInfo(slotId) is { } slot
            ? Math.Max(slot.MapWidth, slot.MapHeight)
            : 0;

    private bool _mapWorkBusy;

    /// <summary>
    /// Starts a map load or new game from a UI signal without waiting for it. An exception it throws is never
    /// lost: it goes to the log and, in Italian, to <paramref name="showError"/> (the panel the player is on).
    /// </summary>
    private async void RunMapTask(Func<Task> task, Action<string> showError)
    {
        try
        {
            await task();
        }
        catch (Exception ex)
        {
            GD.PushError($"Caricamento della mappa non riuscito: {ex}");
            showError($"Caricamento della mappa non riuscito: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs <paramref name="work"/> (a map generated or loaded). Above the Media edge it first shows the
    /// "Generazione della mappa…" overlay and lets two frames go by so the player sees it before the blocking work;
    /// otherwise it runs at once. A second request while one waits is dropped (false/default).
    /// </summary>
    private async Task<T?> WithMapOverlayAsync<T>(int edge, Func<T> work)
    {
        if (edge <= (int)MapSize.Media || _mapOverlay is null)
        {
            return work();
        }

        if (_mapWorkBusy)
        {
            return default;
        }

        _mapWorkBusy = true;
        _mapOverlay.Visible = true;
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            return work();
        }
        finally
        {
            _mapOverlay.Visible = false;
            _mapWorkBusy = false;
        }
    }

    private void ToggleResearch()
    {
        if (_view is null || _research is null)
        {
            return;
        }

        if (_research.IsOpen)
        {
            _research.Close();
            return;
        }

        _mercato?.Close();
        _campaignSelect?.Close();
        ClearToCursor(toast: false);
        _research.Open(_view, c => SubmitCommand(c));
    }

    private void ToggleMercato()
    {
        if (_view is null || _mercato is null)
        {
            return;
        }

        if (_mercato.IsOpen)
        {
            _mercato.Close();
            return;
        }

        _research?.Close();
        _campaignSelect?.Close();
        ClearToCursor(toast: false);
        _mercato.Open(_view, c => SubmitCommand(c), () => PendingCommands);
    }

    private void OnCoreUpgradeRequested()
    {
        if (_view is null)
        {
            return;
        }

        if (_view.CoreUpgradeLevel > 0)
        {
            _hud?.ShowToast($"Core già a LV{_view.CoreUpgradeLevel} (+{_view.CoreSaleBonusPercent}%).");
            return;
        }

        var need = _view.FormatCoreUpgradeNeedMessage();
        if (!string.IsNullOrEmpty(need))
        {
            _hud?.ShowToast(need);
            return;
        }

        // Toast and HUD refresh follow the host's CommandApplied (see OnCommandApplied).
        SubmitCommand(new UpgradeCore(LocalPlayer));
    }

    private void ToggleCampaign()
    {
        if (_campaign is null || _progress is null || _campaignSelect is null)
        {
            return;
        }

        if (_campaignSelect.IsOpen)
        {
            _campaignSelect.Close();
            return;
        }

        _research?.Close();
        _mercato?.Close();
        _home?.Close();
        ClearToCursor(toast: false);
        _campaignSelect.Open(_campaign, _progress);
    }

    /// <summary>
    /// Starts <paramref name="level"/> on its map file. Returns null when the level started, otherwise the Italian
    /// reason (map file missing or invalid); the current game is then left as it was.
    /// </summary>
    private string? StartCampaignLevel(CampaignLevelDefinition level, bool toast)
    {
        if (_campaign is null)
        {
            return "Campagna non disponibile.";
        }

        // A level with a map needs data/maps/: never fall back to the classic generator (NewGame would, for tests).
        var mapsDirectory = ResolveMapsPath();
        if (level.Map is not null && mapsDirectory is null)
        {
            GD.PushWarning($"Campaign map folder data/maps not found for level {level.Id}.");
            return $"Mappa del livello «{level.Name}» non disponibile: cartella delle mappe (data/maps) non trovata.";
        }

        Factory factory;
        try
        {
            var content = FactoryContent.Load(_contentPath);
            factory = NewGame.Campaign(content, _campaign, level, rules: ClientRules(), mapsDirectory: mapsDirectory);
        }
        catch (Exception ex)
        {
            GD.Print($"Campaign map failed: {ex.Message}");
            return $"Mappa del livello «{level.Name}» non disponibile: {ex.Message}";
        }

        AttachGame(factory);

        _continueAutoSave.MarkPlayed();
        _activeLevel = level;
        _levelCompleteToastShown = false;
        _tool = BuildTool.Cursor;
        _visualDirty = true;
        _buildingsDirty = true;
        RebuildBeltVisual();
        RebuildBuildingVisuals();
        SyncResearchLocks();
        UpdateHud();
        SyncCameraMapBounds();
        FocusCameraOnCore();
        QueueRedraw();
        if (toast)
        {
            _hud?.ShowToast($"Campagna · {level.Name} · {ActiveMapWidth}×{ActiveMapHeight}");
            // Raylib: campaign disables first-run tutorial (objectives HUD only).
        }

        return null;
    }

    /// <summary>Centres the camera on <paramref name="worldRect"/> (build queue click), keeping the zoom.</summary>
    private void FocusCameraOn(Rect2 worldRect)
    {
        if (GetNodeOrNull<WorldCamera>("Camera") is not { } cam)
        {
            return;
        }

        cam.Position = worldRect.GetCenter();
        cam.ClampToMap();
    }

    private void FocusCameraOnCore()
    {
        if (!HasNode("Camera") || _view is null || _view.CoreTiles.Count == 0)
        {
            return;
        }

        var cam = GetNode<Camera2D>("Camera");
        var cx = _view.CoreTiles.Average(t => t.X);
        var cy = _view.CoreTiles.Average(t => t.Y);
        cam.Position = new Vector2((float)(cx + 0.5) * TileSize, (float)(cy + 0.5) * TileSize);
        // Fit a bit of map around the core; zoom out on larger campaigns (culling keeps draw cheap).
        var edge = Math.Max(ActiveMapWidth, ActiveMapHeight);
        var z = edge <= 32 ? 0.55f
            : edge <= 64 ? 0.35f
            : edge <= 128 ? 0.25f
            : edge <= 256 ? 0.18f
            : 0.14f;
        cam.Zoom = new Vector2(z, z);
        if (cam is WorldCamera worldCam)
        {
            // Allow deep zoom-out on 1000² without fighting Clamp.
            worldCam.MinZoom = edge >= 256 ? 0.08f : 0.12f;
            // Playable zoom near core so deposits/icons are readable (Raylib icons need ~16px).
            if (edge >= 256 && z < 0.35f)
            {
                z = 0.42f;
                cam.Zoom = new Vector2(z, z);
            }
        }

        SyncCameraMapBounds();
        if (cam is WorldCamera mapCam)
        {
            mapCam.ClampToMap();
        }
    }

    private void SyncCameraMapBounds()
    {
        if (!HasNode("Camera") || GetNode("Camera") is not WorldCamera cam)
        {
            return;
        }

        cam.MapPixelWidth = ActiveMapWidth * TileSize;
        cam.MapPixelHeight = ActiveMapHeight * TileSize;
        cam.ClampToMap();
    }

    /// <summary>Viewport tile cull (Raylib WorldCamera.GetVisibleTileRange) for 1000² maps.</summary>
    private void GetVisibleTileRange(out int minX, out int minY, out int maxX, out int maxY)
    {
        var mapW = Math.Max(1, ActiveMapWidth);
        var mapH = Math.Max(1, ActiveMapHeight);
        if (!HasNode("Camera"))
        {
            minX = 0;
            minY = 0;
            maxX = mapW - 1;
            maxY = mapH - 1;
            return;
        }

        var cam = GetNode<Camera2D>("Camera");
        var zoom = Math.Max(0.01f, cam.Zoom.X);
        var vp = GetViewport().GetVisibleRect().Size;
        var halfW = (vp.X / zoom) * 0.5f;
        var halfH = (vp.Y / zoom) * 0.5f;
        var left = cam.Position.X - halfW;
        var top = cam.Position.Y - halfH;
        var right = cam.Position.X + halfW;
        var bottom = cam.Position.Y + halfH;
        const int margin = 2;
        minX = Math.Max(0, (int)MathF.Floor(left / TileSize) - margin);
        minY = Math.Max(0, (int)MathF.Floor(top / TileSize) - margin);
        maxX = Math.Min(mapW - 1, (int)MathF.Floor(right / TileSize) + margin);
        maxY = Math.Min(mapH - 1, (int)MathF.Floor(bottom / TileSize) + margin);
        if (minX > maxX || minY > maxY)
        {
            minX = 0;
            minY = 0;
            maxX = Math.Min(mapW - 1, 48);
            maxY = Math.Min(mapH - 1, 36);
        }
    }

    private void CheckCampaignComplete()
    {
        if (_view is null || _activeLevel is null || _progress is null || _campaign is null)
        {
            return;
        }

        if (!CampaignProgress.AreAllObjectivesComplete(
                _activeLevel, _view.Wallet, _view.Session, _view.Research))
        {
            return;
        }

        if (!_progress.IsCompleted(_activeLevel.Id))
        {
            _progress.MarkComplete(_activeLevel.Id, _progressPath);
        }

        if (!_levelCompleteToastShown)
        {
            _levelCompleteToastShown = true;
            _hud?.ShowToast($"Livello completato · {_activeLevel.Name}");
            _campaignSelect?.Refresh();
        }
    }

    private void RestoreActiveLevelFromGame()
    {
        if (_view?.ActiveCampaignLevelId is not { } id || _campaign is null)
        {
            _activeLevel = null;
            _hud?.ClearObjectives();
            return;
        }

        _activeLevel = _campaign.Find(id);
        _levelCompleteToastShown = false;
    }

    private void SyncResearchLocks()
    {
        if (_hud is null || _view is null)
        {
            return;
        }

        var tools = new[]
        {
            Hud.ToolKind.Belt,
            Hud.ToolKind.BeltFast,
            Hud.ToolKind.BeltExpress,
            Hud.ToolKind.Miner,
            Hud.ToolKind.MinerAdvanced,
            Hud.ToolKind.Smelter,
            Hud.ToolKind.Assembler,
            Hud.ToolKind.Junction,
            Hud.ToolKind.Splitter,
            Hud.ToolKind.Generator,
            Hud.ToolKind.Sorter,
            Hud.ToolKind.Bridge,
            Hud.ToolKind.Extractor,
            Hud.ToolKind.PowerNode,
            Hud.ToolKind.PowerNodeT2,
            Hud.ToolKind.Lamp
        };
        _hud.SetToolsLocked(tools.Select(t =>
            (t, !_view.IsStructureUnlocked(Hud.StructureIdFor(t)))));

        if (_tool != BuildTool.Cursor && _hud.IsToolLocked(ToHudTool(_tool)))
        {
            ClearToCursor(toast: false);
        }
    }

    private void SaveGame(string slotId, string toast)
    {
        if (_host is null)
        {
            return;
        }

        try
        {
            FactorySaveStore.Save(slotId, _host);
            _hud?.ShowToast(toast);
            GD.Print($"Saved game → {FactorySaveStore.SlotPath(slotId)}");
        }
        catch (Exception ex)
        {
            _hud?.ShowToast("Salvataggio fallito");
            GD.PushError($"Save failed: {ex.Message}");
        }
    }

    /// <summary>Returns true when the slot was loaded; on failure the current game is untouched.</summary>
    private bool LoadGame(string slotId, string toast, bool quietFail = false, Action<string>? showOnPanel = null)
    {
        SimHost? loaded = null;
        string? error = null;
        try
        {
            var content = FactoryContent.Load(_contentPath);
            if (!FactorySaveStore.TryLoad(content, slotId, out loaded, out error, ResolveMapsPath()))
            {
                loaded = null;
            }
        }
        catch (Exception ex)
        {
            GD.PushError($"Load failed: {ex.Message}");
            loaded = null;
            error = "Salvataggio non leggibile.";
        }

        if (loaded is null)
        {
            if (!quietFail)
            {
                // Incompatible (older) saves explain themselves; the current game is left untouched.
                var message = error ?? $"Nessun save ({slotId})";
                _hud?.ShowToast(message);
                showOnPanel?.Invoke(message);
            }

            return false;
        }

        var previous = _host;
        try
        {
            AttachGame(loaded);
            _continueAutoSave.MarkPlayed();
            RefreshAfterAttach();
            _hud?.ShowToast(toast);
            GD.Print($"Loaded game ← {FactorySaveStore.SlotPath(slotId)}");
            return true;
        }
        catch (Exception ex)
        {
            GD.PushError($"Load failed: {ex.Message}");
            RollBackAttach(previous);
            _hud?.ShowToast("Caricamento fallito");
            showOnPanel?.Invoke("Caricamento fallito");
            return false;
        }
    }

    /// <summary>
    /// Everything the world must re-sync after a different game is attached: item layer, belt and
    /// building graphics, research locks, active campaign level, HUD and camera.
    /// </summary>
    private void RefreshAfterAttach()
    {
        ClearItemLayer();
        _visualDirty = true;
        _buildingsDirty = true;
        RebuildBeltVisual();
        RebuildBuildingVisuals();
        SyncResearchLocks();
        RestoreActiveLevelFromGame();
        UpdateHud();
        SyncCameraMapBounds();
        FocusCameraOnCore();
        QueueRedraw();
    }

    private void ClearItemLayer()
    {
        _itemSprites.Clear();
        if (_itemsLayer is not null)
        {
            foreach (var child in _itemsLayer.GetChildren())
            {
                child.QueueFree();
            }
        }
    }

    /// <summary>
    /// A load failed after the new host was attached: bring the previous game back and re-sync it
    /// like a fresh attach. On a first load (no previous game) return to the "no game" state.
    /// </summary>
    private void RollBackAttach(SimHost? previous)
    {
        if (previous is null)
        {
            DetachGame();
            ClearItemLayer();
            _activeLevel = null;
            _hud?.ClearObjectives();
            QueueRedraw();
            return;
        }

        try
        {
            AttachGame(previous);
            RefreshAfterAttach();
        }
        catch (Exception ex)
        {
            GD.PushError($"Rollback refresh failed: {ex.Message}");
        }
    }
}
