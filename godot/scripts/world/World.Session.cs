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
            _hud?.ShowToast(Testi.T("salvataggi.continua.nessuno"));
            return;
        }

        var loaded = await LoadGameWithScreenAsync(
            FactorySaveStore.ContinueSlotId, Testi.T("salvataggi.continua.caricata"), m => _home?.ShowError(m),
            () => _home?.Close());
        if (loaded)
        {
            BeginTutorialIfNeeded();
        }
        // Otherwise stay on Home with the error visible.
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
    private void OnHomeNewGame() => StartNewGameFromHome(MapSize.Media, PlaytestSeed, DayNightMode.Cycle);

    /// <summary>Seed of the scripted playtests' sandbox.</summary>
    private const int PlaytestSeed = 42;

    /// <summary>Inizia on the Nuova partita panel: the map is generated, then the game opens, behind the loading screen.</summary>
    private Task StartNewGameFromPanelAsync(MapSize size, int seed, DayNightMode dayNight) =>
        WithLoadingAsync(2, async phase =>
        {
            await phase("mappa.generazione");
            var host = new SimHost(NewSandboxFactory(FactoryContent.Load(_contentPath), size, seed, dayNight));
            await phase("caricamento.fase.mondo");
            StartNewGameFromHome(size, seed, dayNight, host);
            return true;
        });

    /// <summary>
    /// The agents' launcher (<see cref="AgentBridge"/> <c>nuova_partita</c>): a save slot as from the saves panel, a
    /// campaign level as from the list, or else the <paramref name="sandbox"/> as from Nuova partita (its host made by
    /// <see cref="AgentGameSetup.CreateHost"/>, like the replay of its journal). Returns null when the game started,
    /// otherwise the reason.
    /// </summary>
    internal string? AgentStartGame(AgentGameSetup sandbox, CampaignLevelDefinition? level, string? slot)
    {
        if (slot is not null)
        {
            string? failure = null;
            if (!LoadGame(slot, Testi.F("salvataggi.caricato", slot), showOnPanel: m => failure = m))
            {
                return failure ?? slot;
            }
        }
        else if (level is not null)
        {
            if (StartCampaignLevel(level, toast: true) is { } error)
            {
                return error;
            }

            _returnHomeAfterCampaign = false;
        }
        else
        {
            var host = sandbox.CreateHost(FactoryContent.Load(_contentPath));
            StartNewSandbox((MapSize)sandbox.Size, sandbox.Seed, toast: true, host: host);
        }

        _newGame?.Close();
        _campaignSelect?.Close();
        _home?.Close();
        return null;
    }

    private void StartNewGameFromHome(MapSize size, int seed, DayNightMode dayNight, SimHost? host = null)
    {
        StartNewSandbox(size, seed, toast: true, dayNight, host);
        _newGame?.Close();
        _home?.Close();
        BeginTutorialIfNeeded();
    }

    /// <summary>
    /// Rules of the games the client starts: real construction, as in Mindustry (plans, drone, sites).
    /// The refund share is the mode's own (sandbox 100, campaign = the level's), set by <see cref="NewGame"/>.
    /// </summary>
    private static GameRules ClientRules(DayNightMode dayNight = DayNightMode.Cycle) => new(DayNight: dayNight);

    /// <summary>
    /// A normal game: square map of <paramref name="size"/> from the biome generator, Core at the centre, with the
    /// world's time <paramref name="dayNight"/>.
    /// </summary>
    private static Factory NewSandboxFactory(FactoryContent content, MapSize size, int seed, DayNightMode dayNight = DayNightMode.Cycle)
    {
        var edge = (int)size;
        return NewGame.Sandbox(
            content,
            Factory.CenteredCoreOrigin(edge, edge),
            startingMoney: 180,
            rules: ClientRules(dayNight),
            map: MapGenSettings.Normal(size, seed));
    }

    private void StartNewSandbox(MapSize size, int seed, bool toast, DayNightMode dayNight = DayNightMode.Cycle, SimHost? host = null)
    {
        AttachGame(host ?? new SimHost(NewSandboxFactory(FactoryContent.Load(_contentPath), size, seed, dayNight)));
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
            // A size without a name (the agents' numeric misura) shows only its edge, not "64 64×64".
            _hud?.ShowToast(Enum.IsDefined(size)
                ? Testi.F("partita.nuova.toast", NewGamePanel.SizeLabel(size), (int)size, seed)
                : Testi.F("partita.nuova.toast.lato", (int)size, seed));
        }
    }

    /// <summary>Translated name of the campaign level that uses map <paramref name="mapId"/>, or null.</summary>
    private string? CampaignMapName(string mapId) =>
        _campaign?.Levels.FirstOrDefault(l => l.Map == mapId) is { } level ? Testi.LivelloNome(level) : null;

    /// <summary>Translated name of campaign level <paramref name="levelId"/>, or null.</summary>
    private string? CampaignLevelName(string levelId) =>
        _campaign?.Levels.FirstOrDefault(l => l.Id == levelId) is { } level ? Testi.LivelloNome(level) : null;

    /// <summary>
    /// True while <see cref="WithLoadingAsync"/> works: the loading screen is up, every input is dropped
    /// (<see cref="_Input"/>), the simulation does not advance and a second request is refused.
    /// </summary>
    private bool _loadingBusy;

    /// <summary>The loading screen is working or still fading out: input is dropped and no other load starts.</summary>
    private bool LoadingShown => _loadingBusy || _loading?.IsShown == true;

    /// <summary>Frames the loading screen waits at most for the terrain art around the camera (about 3 s).</summary>
    private const int LoadingTerrainFrames = 180;

    /// <summary>
    /// Starts a map load or new game from a UI signal without waiting for it. An exception it throws is never
    /// lost: it goes to the log and, translated, to <paramref name="showError"/> (the panel the player is on).
    /// </summary>
    private async void RunMapTask(Func<Task> task, Action<string> showError)
    {
        try
        {
            await task();
        }
        catch (Exception ex)
        {
            // The detail goes to the log only: the player gets the translated line.
            GD.PushError($"Caricamento della mappa non riuscito: {ex}");
            showError(Testi.T("mappa.errore.caricamento"));
        }
    }

    /// <summary>
    /// Opens a game behind the loading screen (<see cref="LoadingScreen"/>): the screen shows first and two frames go
    /// by so the player sees it before the blocking work; <paramref name="work"/> then runs its
    /// <paramref name="phases"/> phases, each announced with the <c>phase</c> callback (its line on the screen, then
    /// two frames drawn) and run on the main thread. When the work succeeds (true) the screen waits for the terrain
    /// art around the camera (the first frame of the game) and fades out; on failure it goes at once, leaving the
    /// error on the panel. Playtests skip the fade. A second request while one works is dropped (false).
    /// </summary>
    private async Task<bool> WithLoadingAsync(int phases, Func<Func<string, Task>, Task<bool>> work)
    {
        if (LoadingShown)
        {
            return false;
        }

        if (_loading is null)
        {
            return await work(_ => Task.CompletedTask);
        }

        _loadingBusy = true;
        CancelDrags();
        var done = 0;
        var ok = false;
        _loading.Begin(phases + 1);
        try
        {
            async Task Phase(string key)
            {
                _loading.SetPhase(key, done++);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }

            ok = await work(Phase);
            if (ok)
            {
                _loading.SetPhase("caricamento.fase.terreno", phases);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                for (var i = 0; i < LoadingTerrainFrames && _terrainChunks is { ArtShown: true, ArtPending: > 0 }; i++)
                {
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                }
            }

            return ok;
        }
        finally
        {
            _loadingBusy = false;
            _loading.End(instant: !ok || PlaytestRequested);
        }
    }

    /// <summary>
    /// Loads save <paramref name="slotId"/> behind the loading screen: the save is read, then the world is attached;
    /// <paramref name="onLoaded"/> (closing the panel the player was on) runs before the screen fades. A failure goes
    /// to <paramref name="showOnPanel"/> (and the toast), and the current game is untouched.
    /// </summary>
    private Task<bool> LoadGameWithScreenAsync(string slotId, string toast, Action<string>? showOnPanel, Action? onLoaded = null) =>
        WithLoadingAsync(2, async phase =>
        {
            await phase("caricamento.fase.salvataggio");
            if (ReadSlot(slotId, quietFail: false, showOnPanel) is not { } loaded)
            {
                return false;
            }

            await phase("caricamento.fase.mondo");
            if (!AttachLoadedGame(slotId, loaded, toast, showOnPanel))
            {
                return false;
            }

            onLoaded?.Invoke();
            return true;
        });

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
    /// Starts <paramref name="level"/> on its map file. Returns null when the level started, otherwise the translated
    /// reason (map file missing or invalid); the current game is then left as it was.
    /// </summary>
    private string? StartCampaignLevel(CampaignLevelDefinition level, bool toast)
    {
        if (_campaign is null)
        {
            return Testi.T("campagna.non.disponibile");
        }

        // A level with a map needs data/maps/: never fall back to the classic generator (NewGame would, for tests).
        var mapsDirectory = ResolveMapsPath();
        if (level.Map is not null && mapsDirectory is null)
        {
            GD.PushWarning($"Campaign map folder data/maps not found for level {level.Id}.");
            return Testi.F("mappa.errore.cartella", Testi.LivelloNome(level));
        }

        Factory factory;
        try
        {
            var content = FactoryContent.Load(_contentPath);
            factory = NewGame.Campaign(content, _campaign, level, rules: ClientRules(), mapsDirectory: mapsDirectory);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Campaign map failed: {ex.Message}");
            return Testi.F("mappa.errore.livello", Testi.LivelloNome(level));
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
            _hud?.ShowToast(Testi.F("campagna.avvio.toast", Testi.LivelloNome(level), ActiveMapWidth, ActiveMapHeight));
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
            _hud?.ShowToast(Testi.F("campagna.completato.toast", Testi.LivelloNome(_activeLevel)));
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

    /// <summary>Core tier the palette's Core blocks were last shown for (a finished upgrade shows the next one).</summary>
    private int _locksCoreTier = -1;

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
            Hud.ToolKind.BeltArmored,
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
            Hud.ToolKind.Lamp,
            Hud.ToolKind.Container,
            Hud.ToolKind.Vault,
            Hud.ToolKind.CoreFoundation,
            Hud.ToolKind.CoreNucleus
        };
        // A Core block shows only while it is the next tier: the one built and the one after next stay out.
        _locksCoreTier = _view.CoreTier;
        bool Locked(Hud.ToolKind tool)
        {
            var id = Hud.StructureIdFor(tool);
            var tier = BuildSpec.CoreTierOf(BuildSpec.KindFor(id));
            return !_view.IsStructureUnlocked(id) || (tier > 0 && tier != _view.CoreTier + 1);
        }

        _hud.SetToolsLocked(tools.Select(t => (t, Locked(t))));

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
            _hud?.ShowToast(Testi.T("salvataggi.errore.salvataggio"));
            GD.PushError($"Save failed: {ex.Message}");
        }
    }

    /// <summary>Returns true when the slot was loaded; on failure the current game is untouched.</summary>
    private bool LoadGame(string slotId, string toast, bool quietFail = false, Action<string>? showOnPanel = null) =>
        ReadSlot(slotId, quietFail, showOnPanel) is { } loaded && AttachLoadedGame(slotId, loaded, toast, showOnPanel);

    /// <summary>
    /// Reads save <paramref name="slotId"/> into a new host, or null; a failure (unless <paramref name="quietFail"/>)
    /// says why on the toast and on <paramref name="showOnPanel"/>.
    /// </summary>
    private SimHost? ReadSlot(string slotId, bool quietFail, Action<string>? showOnPanel)
    {
        SimHost? loaded = null;
        SaveLoadFailure? failure = null;
        try
        {
            var content = FactoryContent.Load(_contentPath);
            if (!FactorySaveStore.TryLoadSlot(content, slotId, out loaded, out failure, ResolveMapsPath()))
            {
                loaded = null;
            }
        }
        catch (Exception ex)
        {
            GD.PushError($"Load failed: {ex.Message}");
            loaded = null;
            failure = new SaveLoadFailure(SaveLoadError.Unreadable, null, ex.Message);
        }

        if (loaded is null && !quietFail)
        {
            // Every failure says why (older version, campaign map missing or changed…); the detail goes to the log.
            if (failure is not null)
            {
                GD.Print($"Load of {slotId} refused: {failure.Code} · {failure.Detail}");
            }

            var message = failure is null
                ? Testi.F("salvataggi.errore.nessuno", slotId)
                : Testi.Salvataggio(failure, CampaignMapName);
            _hud?.ShowToast(message);
            showOnPanel?.Invoke(message);
        }

        return loaded;
    }

    /// <summary>Attaches the host read from <paramref name="slotId"/>; on failure the previous game comes back.</summary>
    private bool AttachLoadedGame(string slotId, SimHost loaded, string toast, Action<string>? showOnPanel)
    {
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
            _hud?.ShowToast(Testi.T("salvataggi.errore.caricamento"));
            showOnPanel?.Invoke(Testi.T("salvataggi.errore.caricamento"));
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
        _itemMotion.Clear();
        _itemsTick = -1;
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
