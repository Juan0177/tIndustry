using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>HUD panels and overlays (Ricerca, Mercato, Campagna, Home, Salvataggi, Impostazioni, Tutorial, FPS/risorse).</summary>
public partial class World
{
    /// <summary>The HUD's root Control (<see cref="UiScale.RootOf"/>): parent of every HUD control and panel.</summary>
    internal Control UiRoot => UiScale.RootOf(GetNode<CanvasLayer>("Hud"));

    private void EnsureHud()
    {
        var hudRoot = UiRoot;
        // Demote legacy debug label if present.
        if (hudRoot.HasNode("Status"))
        {
            hudRoot.GetNode("Status").QueueFree();
        }

        if (hudRoot.HasNode("FactoryHud"))
        {
            _hud = hudRoot.GetNode<Hud>("FactoryHud");
        }
        else
        {
            _hud = new Hud { Name = "FactoryHud" };
            hudRoot.AddChild(_hud);
        }

        _hud.ToolChosen += OnHudToolChosen;
        _hud.ResearchRequested += ToggleResearch;
        _hud.MercatoRequested += ToggleMercato;
        _hud.CampaignRequested += ToggleCampaign;
        _hud.BuildQueueFocusRequested += FocusCameraOn;
        _hud.MenuRequested += () =>
        {
            AutoSaveContinue(quiet: true);
            _home?.Open(inGame: true);
        };
        _hud.SetSelectedTool(Hud.ToolKind.Cursor);
        _hud.SetDirectionLabel(_placeDir);
    }

    private void EnsureResearchPanel()
    {
        var hudRoot = UiRoot;
        if (hudRoot.HasNode("ResearchPanel"))
        {
            _research = hudRoot.GetNode<ResearchPanel>("ResearchPanel");
        }
        else
        {
            _research = new ResearchPanel { Name = "ResearchPanel" };
            hudRoot.AddChild(_research);
        }

        _research.UnlockedChanged += () =>
        {
            SyncResearchLocks();
            CheckCampaignComplete();
            if (_view is not null)
            {
                _hud?.ShowToast(Testi.T("ricerca.sbloccato"));
            }
        };
    }

    private void EnsureMercatoPanel()
    {
        var hudRoot = UiRoot;
        if (hudRoot.HasNode("MercatoPanel"))
        {
            _mercato = hudRoot.GetNode<MercatoPanel>("MercatoPanel");
        }
        else
        {
            _mercato = new MercatoPanel { Name = "MercatoPanel" };
            hudRoot.AddChild(_mercato);
        }

        _mercato.SoldChanged += () =>
        {
            UpdateHud();
            CheckCampaignComplete();
            if (_view is not null)
            {
                _hud?.ShowToast(Testi.F("mercato.venduto.fondi", _view.Wallet.Money));
            }
        };
        _mercato.AutoSellChanged += UpdateHud;
    }

    private void EnsureCampaignSelectPanel()
    {
        var hudRoot = UiRoot;
        if (hudRoot.HasNode("CampaignSelectPanel"))
        {
            _campaignSelect = hudRoot.GetNode<CampaignSelectPanel>("CampaignSelectPanel");
        }
        else
        {
            _campaignSelect = new CampaignSelectPanel { Name = "CampaignSelectPanel" };
            hudRoot.AddChild(_campaignSelect);
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
        var hudRoot = UiRoot;
        var overlay = new Control { Name = "MapOverlay", MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var dim = new ColorRect { Color = UiColors.Oscuramento, MouseFilter = Control.MouseFilterEnum.Ignore };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(dim);
        var label = new Label
        {
            Text = "mappa.generazione",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ThemeTypeVariation = UiTheme.Titolo
        };
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(label);
        hudRoot.AddChild(overlay);
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
        var hudRoot = UiRoot;
        if (hudRoot.HasNode("HomePanel"))
        {
            _home = hudRoot.GetNode<HomePanel>("HomePanel");
        }
        else
        {
            _home = new HomePanel { Name = "HomePanel" };
            hudRoot.AddChild(_home);
        }

        // Keep Home above other HUD so it covers dock/info while choosing.
        hudRoot.MoveChild(_home, hudRoot.GetChildCount() - 1);

        _home.ResumeChosen += () => _home?.Close();
        _home.ContinuaChosen += () => RunMapTask(OnHomeContinuaAsync, m => _home?.ShowError(m));
        _home.CampaignChosen += OnHomeCampaign;
        _home.NewGameChosen += OpenNewGamePanel;
        _home.SavesChosen += OpenSavesManager;
        _home.SettingsChosen += OpenSettings;
        _home.QuitChosen += QuitGame;
    }

    private void EnsureNewGamePanel()
    {
        var hudRoot = UiRoot;
        if (hudRoot.HasNode("NewGamePanel"))
        {
            _newGame = hudRoot.GetNode<NewGamePanel>("NewGamePanel");
        }
        else
        {
            _newGame = new NewGamePanel { Name = "NewGamePanel" };
            hudRoot.AddChild(_newGame);
        }

        hudRoot.MoveChild(_newGame, hudRoot.GetChildCount() - 1);
        _newGame.StartRequested += (size, seed, night) => RunMapTask(() => StartNewGameFromPanelAsync(size, seed, night), ShowNewGameError);
        _newGame.Cancelled += () => _home?.Open();
    }

    /// <summary>A failed new game: on the Nuova partita panel while it is open (a toast would sit under it).</summary>
    private void ShowNewGameError(string message)
    {
        if (_newGame?.IsOpen == true)
        {
            _newGame.ShowError(message);
        }
        else
        {
            _hud?.ShowToast(message);
        }
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
        else if (what == NotificationApplicationFocusOut)
        {
            CancelDrags(); // the release would never arrive: the drags send nothing
        }
    }

    private void EnsureSavesPanel()
    {
        var hudRoot = UiRoot;
        if (hudRoot.HasNode("SavesPanel"))
        {
            _saves = hudRoot.GetNode<SavesPanel>("SavesPanel");
        }
        else
        {
            _saves = new SavesPanel { Name = "SavesPanel" };
            hudRoot.AddChild(_saves);
        }

        hudRoot.MoveChild(_saves, hudRoot.GetChildCount() - 1);
        _saves.SlotLoadRequested += slotId => RunMapTask(() => OnSavesSlotLoadAsync(slotId), m => _saves?.ShowStatus(m));
        _saves.SaveAsRequested += OnSavesSaveAs;
        _saves.Closed += () => _home?.Open();
        _saves.MapName = CampaignMapName;
        _saves.LevelName = CampaignLevelName;
    }

    private void OpenSavesManager()
    {
        _home?.Close();
        _research?.Close();
        _mercato?.Close();
        _campaignSelect?.Close();
        _settingsFromHome = false; // Saves goes back to the Home itself: closing Impostazioni must not reopen it under
        _settingsPanel?.Close();
        _saves?.Open();
    }

    private async Task OnSavesSlotLoadAsync(string slotId)
    {
        var loaded = await WithMapOverlayAsync(
            SlotMapEdge(slotId), () => LoadGame(slotId, Testi.F("salvataggi.caricato", slotId), showOnPanel: m => _saves?.ShowStatus(m)));
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
            _hud?.ShowToast(Testi.T("salvataggi.nessuna.partita"));
            return;
        }

        var id = FactorySaveStore.CreateSlotId();
        SaveGame(id, Testi.F("salvataggi.salvato.come", id));
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
                _hud?.ShowToast(Testi.T("salvataggi.continua.aggiornata"));
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
        var hudRoot = UiRoot;
        if (hudRoot.HasNode("SettingsPanel"))
        {
            _settingsPanel = hudRoot.GetNode<SettingsPanel>("SettingsPanel");
        }
        else
        {
            _settingsPanel = new SettingsPanel { Name = "SettingsPanel" };
            hudRoot.AddChild(_settingsPanel);
        }

        hudRoot.MoveChild(_settingsPanel, hudRoot.GetChildCount() - 1);
        // Opened from the Home: closing it goes back to the Home.
        _settingsPanel.Closed += () =>
        {
            if (_settingsFromHome)
            {
                _settingsFromHome = false;
                _home?.Open();
            }
        };
        _settingsPanel.Applied += OnSettingsApplied;
        // The tutorial plays on the game behind (as before), not under a reopened Home.
        _settingsPanel.ReplayTutorialRequested += () =>
        {
            _home?.Close();
            BeginTutorialIfNeeded(force: true);
        };
    }

    private void EnsureTutorialPanel()
    {
        var hudRoot = UiRoot;
        if (hudRoot.HasNode("TutorialPanel"))
        {
            _tutorial = hudRoot.GetNode<TutorialPanel>("TutorialPanel");
        }
        else
        {
            _tutorial = new TutorialPanel { Name = "TutorialPanel" };
            hudRoot.AddChild(_tutorial);
        }

        hudRoot.MoveChild(_tutorial, hudRoot.GetChildCount() - 1);
        _tutorial.Completed += () => _hud?.ShowToast(Testi.T("tutorial.completato"));
        _tutorial.Skipped += () => _hud?.ShowToast(Testi.T("tutorial.saltato"));
    }

    private void EnsureFpsOverlay()
    {
        var hudRoot = UiRoot;
        if (hudRoot.HasNode("FpsOverlay"))
        {
            _fpsLabel = hudRoot.GetNode<Label>("FpsOverlay");
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
            _fpsLabel.OffsetBottom = 32;
            _fpsLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _fpsLabel.ThemeTypeVariation = UiTheme.Avviso;
            hudRoot.AddChild(_fpsLabel);
        }

        SyncFpsOverlay();
    }

    private void EnsureResourceOverlay()
    {
        var hudRoot = UiRoot;
        if (hudRoot.HasNode("ResourceOverlay"))
        {
            _resourceLabel = hudRoot.GetNode<Label>("ResourceOverlay");
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
            _resourceLabel.OffsetBottom = 140;
            _resourceLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _resourceLabel.ThemeTypeVariation = UiTheme.Avviso;
            hudRoot.AddChild(_resourceLabel);
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
        _settingsFromHome = _home?.IsOpen == true;
        _home?.Close();
        _research?.Close();
        _mercato?.Close();
        _campaignSelect?.Close();
        _settingsPanel?.Open(_settings);
    }

    private void OnSettingsApplied()
    {
        ApplyUiScale();
        ApplyGraphicsSettings();
        SyncFpsOverlay();
        SyncResourceOverlay();
        _hud?.ShowToast(Testi.F("impostazioni.applicate.toast",
            ClientSettings.UiScaleLabel(_settings.UiScalePercent), Testi.T(_settings.VSync ? "comune.on" : "comune.off")));
    }

    /// <summary>The UI scale of the settings on the HUD (the palette redraws its icons for the new factor).</summary>
    private void ApplyUiScale()
    {
        UiScale.Apply(_settings.UiScalePercent);
        _hud?.OnUiScaleChanged();
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

        _tutorial.SetSuppressed(PanelOpen);

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
