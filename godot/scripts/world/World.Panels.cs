using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>HUD panels and overlays (Ricerca, Mercato, Campagna, Home, Salvataggi, Impostazioni, Tutorial, FPS/risorse).</summary>
public partial class World
{
    private void EnsureHud()
    {
        var layer = GetNode<CanvasLayer>("Hud");
        // Demote legacy debug label if present.
        if (layer.HasNode("Status"))
        {
            layer.GetNode("Status").QueueFree();
        }

        if (layer.HasNode("FactoryHud"))
        {
            _hud = layer.GetNode<Hud>("FactoryHud");
        }
        else
        {
            _hud = new Hud { Name = "FactoryHud" };
            layer.AddChild(_hud);
        }

        _hud.ToolChosen += OnHudToolChosen;
        _hud.ResearchRequested += ToggleResearch;
        _hud.MercatoRequested += ToggleMercato;
        _hud.CampaignRequested += ToggleCampaign;
        _hud.CoreUpgradeRequested += OnCoreUpgradeRequested;
        _hud.BuildQueueFocusRequested += FocusCameraOn;
        _hud.MenuRequested += () =>
        {
            AutoSaveContinue(quiet: true);
            _home?.Open();
        };
        _hud.SetSelectedTool(Hud.ToolKind.Cursor);
        _hud.SetDirectionLabel(DirectionIt(_placeDir));
    }

    private void EnsureResearchPanel()
    {
        var layer = GetNode<CanvasLayer>("Hud");
        if (layer.HasNode("ResearchPanel"))
        {
            _research = layer.GetNode<ResearchPanel>("ResearchPanel");
        }
        else
        {
            _research = new ResearchPanel { Name = "ResearchPanel" };
            layer.AddChild(_research);
        }

        _research.UnlockedChanged += () =>
        {
            SyncResearchLocks();
            CheckCampaignComplete();
            if (_view is not null)
            {
                _hud?.ShowToast("Sblocco applicato");
            }
        };
    }

    private void EnsureMercatoPanel()
    {
        var layer = GetNode<CanvasLayer>("Hud");
        if (layer.HasNode("MercatoPanel"))
        {
            _mercato = layer.GetNode<MercatoPanel>("MercatoPanel");
        }
        else
        {
            _mercato = new MercatoPanel { Name = "MercatoPanel" };
            layer.AddChild(_mercato);
        }

        _mercato.SoldChanged += () =>
        {
            UpdateHud();
            CheckCampaignComplete();
            if (_view is not null)
            {
                _hud?.ShowToast($"Venduto · Fondi ${_view.Wallet.Money}");
            }
        };
        _mercato.AutoSellChanged += () =>
        {
            UpdateHud();
            if (_view is not null)
            {
                _hud?.ShowToast(_view.AutoSellAtCore
                    ? "Vendita automatica ON"
                    : "Vendita automatica OFF");
            }
        };
    }

    private void EnsureCampaignSelectPanel()
    {
        var layer = GetNode<CanvasLayer>("Hud");
        if (layer.HasNode("CampaignSelectPanel"))
        {
            _campaignSelect = layer.GetNode<CampaignSelectPanel>("CampaignSelectPanel");
        }
        else
        {
            _campaignSelect = new CampaignSelectPanel { Name = "CampaignSelectPanel" };
            layer.AddChild(_campaignSelect);
        }

        _campaignSelect.LevelChosen += OnCampaignLevelChosen;
        _campaignSelect.Closed += () =>
        {
            if (_returnHomeAfterCampaign)
            {
                _returnHomeAfterCampaign = false;
                _home?.Open();
            }
        };
    }

    private Control? _mapOverlay;

    /// <summary>Full-screen "Generazione della mappa…" card above every panel (see <see cref="WithMapOverlayAsync{T}"/>).</summary>
    private void EnsureMapOverlay()
    {
        var layer = GetNode<CanvasLayer>("Hud");
        var overlay = new Control { Name = "MapOverlay", MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var dim = new ColorRect { Color = new Color(0.04f, 0.05f, 0.05f, 0.88f), MouseFilter = Control.MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(dim);
        var label = new Label
        {
            Text = "Generazione della mappa…",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        label.AddThemeColorOverride("font_color", new Color(0.92f, 0.78f, 0.28f, 1f));
        label.AddThemeFontSizeOverride("font_size", 26);
        overlay.AddChild(label);
        layer.AddChild(overlay);
        _mapOverlay = overlay;
    }

    /// <summary>A level picked on the campaign list; one whose map cannot load keeps the list open with the reason.</summary>
    private void OnCampaignLevelChosen(CampaignLevelDefinition level)
    {
        if (StartCampaignLevel(level, toast: true) is { } error)
        {
            _campaignSelect?.ShowError(error);
            return;
        }

        _returnHomeAfterCampaign = false;
        _campaignSelect?.Close();
        _home?.Close();
    }

    private void EnsureHomePanel()
    {
        var layer = GetNode<CanvasLayer>("Hud");
        if (layer.HasNode("HomePanel"))
        {
            _home = layer.GetNode<HomePanel>("HomePanel");
        }
        else
        {
            _home = new HomePanel { Name = "HomePanel" };
            layer.AddChild(_home);
        }

        // Keep Home above other HUD so it covers dock/info while choosing.
        layer.MoveChild(_home, layer.GetChildCount() - 1);

        _home.ContinuaChosen += () => RunMapTask(OnHomeContinuaAsync, m => _home?.ShowError(m));
        _home.CampaignChosen += OnHomeCampaign;
        _home.NewGameChosen += OpenNewGamePanel;
        _home.SavesChosen += OpenSavesManager;
        _home.SettingsChosen += OpenSettings;
        _home.QuitChosen += QuitGame;
    }

    private void EnsureNewGamePanel()
    {
        var layer = GetNode<CanvasLayer>("Hud");
        if (layer.HasNode("NewGamePanel"))
        {
            _newGame = layer.GetNode<NewGamePanel>("NewGamePanel");
        }
        else
        {
            _newGame = new NewGamePanel { Name = "NewGamePanel" };
            layer.AddChild(_newGame);
        }

        layer.MoveChild(_newGame, layer.GetChildCount() - 1);
        _newGame.StartRequested += (size, seed) => RunMapTask(() => StartNewGameFromPanelAsync(size, seed), m => _hud?.ShowToast(m));
        _newGame.Cancelled += () => _home?.Open();
    }

    /// <summary>Home → Nuova partita: pick size and seed (<see cref="NewGamePanel"/>), or <paramref name="seed"/> (playtests).</summary>
    private void OpenNewGamePanel() => OpenNewGamePanel(null);

    private void OpenNewGamePanel(int? seed)
    {
        _home?.Close();
        _newGame?.Open(seed);
    }

    /// <summary>
    /// The one way out of the game, for Home "Esci" and the window's close button (Godot's automatic quit on close is
    /// off, see <see cref="_Ready"/>): Continua is saved if a game was played (Raylib parity), then the tree quits.
    /// </summary>
    private void QuitGame()
    {
        AutoSaveContinue(quiet: true);
        GetTree().Quit();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            QuitGame();
        }
    }

    private void EnsureSavesPanel()
    {
        var layer = GetNode<CanvasLayer>("Hud");
        if (layer.HasNode("SavesPanel"))
        {
            _saves = layer.GetNode<SavesPanel>("SavesPanel");
        }
        else
        {
            _saves = new SavesPanel { Name = "SavesPanel" };
            layer.AddChild(_saves);
        }

        layer.MoveChild(_saves, layer.GetChildCount() - 1);
        _saves.SlotLoadRequested += slotId => RunMapTask(() => OnSavesSlotLoadAsync(slotId), m => _saves?.ShowStatus(m));
        _saves.SaveAsRequested += OnSavesSaveAs;
        _saves.Closed += () => _home?.Open();
        _saves.MapName = mapId => _campaign?.Levels.FirstOrDefault(l => l.Map == mapId)?.Name;
    }

    private void OpenSavesManager()
    {
        _home?.Close();
        _research?.Close();
        _mercato?.Close();
        _campaignSelect?.Close();
        _settingsPanel?.Close();
        _saves?.Open();
    }

    private async Task OnSavesSlotLoadAsync(string slotId)
    {
        var loaded = await WithMapOverlayAsync(
            SlotMapEdge(slotId), () => LoadGame(slotId, $"Caricato · {slotId}", showOnPanel: m => _saves?.ShowStatus(m)));
        if (!loaded)
        {
            return; // stay on the saves panel with the error visible
        }

        _saves?.Close();
        _home?.Close();
        BeginTutorialIfNeeded();
    }

    private void OnSavesSaveAs()
    {
        if (_view is null)
        {
            _hud?.ShowToast("Nessuna partita attiva da salvare");
            return;
        }

        var id = FactorySaveStore.CreateSlotId();
        SaveGame(id, $"Salvato come {id}");
        _saves?.NotifySavedAs(id);
    }

    /// <summary>Raylib AutoSaveContinue: write live factory to Continua when leaving play (only if a game was played).</summary>
    private void AutoSaveContinue(bool quiet = false)
    {
        try
        {
            if (!_continueAutoSave.TrySave(_host))
            {
                return; // nothing played this session: keep the existing Continua untouched
            }

            if (!quiet)
            {
                _hud?.ShowToast("Continua aggiornata");
            }

            GD.Print($"Autosave Continua → {FactorySaveStore.SlotPath(FactorySaveStore.ContinueSlotId)}");
        }
        catch (Exception ex)
        {
            GD.PushError($"Autosave failed: {ex.Message}");
        }
    }

    private void EnsureSettingsPanel()
    {
        var layer = GetNode<CanvasLayer>("Hud");
        if (layer.HasNode("SettingsPanel"))
        {
            _settingsPanel = layer.GetNode<SettingsPanel>("SettingsPanel");
        }
        else
        {
            _settingsPanel = new SettingsPanel { Name = "SettingsPanel" };
            layer.AddChild(_settingsPanel);
        }

        layer.MoveChild(_settingsPanel, layer.GetChildCount() - 1);
        _settingsPanel.Closed += () => { };
        _settingsPanel.Applied += OnSettingsApplied;
        _settingsPanel.ReplayTutorialRequested += () => BeginTutorialIfNeeded(force: true);
    }

    private void EnsureTutorialPanel()
    {
        var layer = GetNode<CanvasLayer>("Hud");
        if (layer.HasNode("TutorialPanel"))
        {
            _tutorial = layer.GetNode<TutorialPanel>("TutorialPanel");
        }
        else
        {
            _tutorial = new TutorialPanel { Name = "TutorialPanel" };
            layer.AddChild(_tutorial);
        }

        layer.MoveChild(_tutorial, layer.GetChildCount() - 1);
        _tutorial.Completed += () => _hud?.ShowToast("Tutorial completato");
        _tutorial.Skipped += () => _hud?.ShowToast("Tutorial saltato");
    }

    private void EnsureFpsOverlay()
    {
        var layer = GetNode<CanvasLayer>("Hud");
        if (layer.HasNode("FpsOverlay"))
        {
            _fpsLabel = layer.GetNode<Label>("FpsOverlay");
        }
        else
        {
            _fpsLabel = new Label
            {
                Name = "FpsOverlay",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _fpsLabel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            _fpsLabel.OffsetLeft = -120;
            _fpsLabel.OffsetTop = 8;
            _fpsLabel.OffsetRight = -12;
            _fpsLabel.OffsetBottom = 28;
            _fpsLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _fpsLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.9f, 0.75f, 0.9f));
            _fpsLabel.AddThemeFontSizeOverride("font_size", 13);
            layer.AddChild(_fpsLabel);
        }

        SyncFpsOverlay();
    }

    private void EnsureResourceOverlay()
    {
        var layer = GetNode<CanvasLayer>("Hud");
        if (layer.HasNode("ResourceOverlay"))
        {
            _resourceLabel = layer.GetNode<Label>("ResourceOverlay");
        }
        else
        {
            _resourceLabel = new Label
            {
                Name = "ResourceOverlay",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Text = "FPS —\nCPU —\nRAM —"
            };
            // Top-right: the top-left column belongs to menu / core / objectives.
            _resourceLabel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            _resourceLabel.OffsetLeft = -280;
            _resourceLabel.OffsetTop = 8;
            _resourceLabel.OffsetRight = -12;
            _resourceLabel.OffsetBottom = 120;
            _resourceLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _resourceLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.93f, 0.86f, 0.92f));
            _resourceLabel.AddThemeFontSizeOverride("font_size", 13);
            layer.AddChild(_resourceLabel);
        }

        SyncResourceOverlay();
    }

    private void SyncFpsOverlay()
    {
        if (_fpsLabel is null)
        {
            return;
        }

        // Raylib: corner FPS only when resource overlay is off.
        _fpsLabel.Visible = _settings.ShowFps && !_settings.ShowResourceOverlay;
    }

    private void SyncResourceOverlay()
    {
        if (_resourceLabel is null)
        {
            return;
        }

        _resourceLabel.Visible = _settings.ShowResourceOverlay;
    }

    private void OpenSettings()
    {
        _home?.Close();
        _research?.Close();
        _mercato?.Close();
        _campaignSelect?.Close();
        _settingsPanel?.Open(_settings);
    }

    private void OnSettingsApplied()
    {
        SyncFpsOverlay();
        SyncResourceOverlay();
        _hud?.ShowToast(
            $"Impostazioni · UI {ClientSettings.UiScaleLabel(_settings.UiScalePercent)} · VSync {(_settings.VSync ? "ON" : "OFF")}");
    }

    private void BeginTutorialIfNeeded(bool force = false)
    {
        if (!force && _settings.TutorialCompleted)
        {
            return;
        }

        if (force)
        {
            _settings.TutorialCompleted = false;
            _settings.Save();
        }

        _home?.Close();
        _settingsPanel?.Close();
        _tutorialCamAnchor = null;
        _tutorial?.Open(_settings);
    }

    /// <summary>
    /// Per-frame: hide the tutorial card under full-screen panels and auto-complete the current step
    /// once the player has done what it asks.
    /// </summary>
    private void SyncTutorial()
    {
        if (_tutorial is null || !_tutorial.IsOpen)
        {
            return;
        }

        var modalOpen = _home?.IsOpen == true || _newGame?.IsOpen == true || _saves?.IsOpen == true || _settingsPanel?.IsOpen == true
            || _research?.IsOpen == true || _mercato?.IsOpen == true || _campaignSelect?.IsOpen == true;
        _tutorial.SetSuppressed(modalOpen);

        if (_view is null)
        {
            return;
        }

        var done = _tutorial.CurrentStep switch
        {
            TutorialPanel.Step.Camera => CameraMovedSinceTutorialStart(),
            TutorialPanel.Step.Palette => _tool != BuildTool.Cursor,
            TutorialPanel.Step.Miner => _view.Miners.Count > 0,
            TutorialPanel.Step.BeltToCore => _view.CoreDeliveredItems > 0,
            TutorialPanel.Step.Mercato => _mercato?.IsOpen == true,
            TutorialPanel.Step.Ricerca => _research?.IsOpen == true,
            TutorialPanel.Step.Forno => _view.Smelters.Count > 0,
            _ => false
        };
        if (done)
        {
            _tutorial.CompleteStep(_tutorial.CurrentStep);
        }
    }

    private bool CameraMovedSinceTutorialStart()
    {
        if (GetNodeOrNull<Camera2D>("Camera") is not { } cam)
        {
            return false;
        }

        var now = (cam.Position, cam.Zoom.X);
        _tutorialCamAnchor ??= now;
        var anchor = _tutorialCamAnchor.Value;
        return anchor.Position.DistanceTo(now.Position) > TileSize * 2 || Math.Abs(anchor.Zoom - now.X) > 0.05f;
    }
}
