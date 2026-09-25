using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Playable factory slice + FactoryHud + Ricerca. Default = cursore.
/// Hotkeys 1–9 place · Esc cursore · T ricerca · R ruota · C filtro · RMB elimina.
/// </summary>
public partial class SpikeWorld : Node2D
{
    public const int TileSize = 64;
    public const int MapWidth = 24;
    public const int MapHeight = 18;

    private static readonly string[] SorterFilterIds =
    [
        "iron-ore", "copper-ore", "iron-plate", "copper-wire", "coal"
    ];

    private enum BuildTool
    {
        Cursor,
        Belt,
        Miner,
        Smelter,
        Assembler,
        Junction,
        Splitter,
        Generator,
        Sorter,
        Bridge
    }

    private FactorySlice? _slice;
    private MindustryBeltVisual? _beltVisual;
    private readonly Dictionary<long, Sprite2D> _itemSprites = [];
    private Node2D? _itemsLayer;
    private Node2D? _buildingsLayer;
    private FactoryHud? _hud;
    private HomePanel? _home;
    private ResearchPanel? _research;
    private MercatoPanel? _mercato;
    private CampaignSelectPanel? _campaignSelect;
    private CampaignCatalog? _campaign;
    private CampaignProgress? _progress;
    private string _progressPath = CampaignProgress.ProgressPath;
    private CampaignLevelDefinition? _activeLevel;
    private bool _levelCompleteToastShown;
    private string _contentPath = "";
    private string _campaignPath = "";
    private Direction _placeDir = Direction.East;
    private BuildTool _tool = BuildTool.Cursor;
    private string _sorterFilterId = BeltGridCell.DefaultSorterFilter;
    private GridPosition? _hover;
    private bool _draggingPlace;
    private bool _draggingRemove;
    private bool _visualDirty = true;
    private bool _buildingsDirty = true;
    private Texture2D? _oreTex;
    private Texture2D? _plateTex;
    private Texture2D? _copperOreTex;
    private Texture2D? _copperWireTex;
    private Texture2D? _coalTex;
    private bool _lastGenLive;
    private bool _returnHomeAfterCampaign;

    public override void _Ready()
    {
        _contentPath = ResolveContentPath();
        _campaignPath = ResolveCampaignPath();
        var content = FactoryContent.Load(_contentPath);
        _campaign = CampaignCatalog.Load(_campaignPath);
        _progressPath = CampaignProgress.ProgressPath;
        _progress = CampaignProgress.Load(_progressPath);
        _slice = FactorySlice.CreateSorterBridgeDemo(content);

        _itemsLayer = GetNode<Node2D>("Items");
        EnsureFactoryHud();
        EnsureResearchPanel();
        EnsureMercatoPanel();
        EnsureCampaignSelectPanel();
        EnsureHomePanel();
        EnsureBuildingsLayer();
        _oreTex = GD.Load<Texture2D>("res://assets/iron-ore.png");
        _plateTex = GD.Load<Texture2D>("res://assets/iron-plate.png");
        _copperOreTex = GD.Load<Texture2D>("res://assets/copper-ore.png");
        _copperWireTex = GD.Load<Texture2D>("res://assets/copper-wire.png");
        _coalTex = GD.Load<Texture2D>("res://assets/coal.png");

        EnsureBeltVisual();
        RebuildBeltVisual();
        RebuildBuildingVisuals();
        SyncResearchLocks();
        UpdateHud();

        // Campaign capture: select + objectives on L01.
        if (OS.GetEnvironment("TINDUSTRY_CAPTURE") == "1"
            && OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "campaign")
        {
            // Fresh progress so L02 stays locked in select shot.
            var captureProgressPath = Path.Combine(Path.GetTempPath(), "tindustry-capture-campaign-progress.json");
            if (File.Exists(captureProgressPath))
            {
                File.Delete(captureProgressPath);
            }

            _progressPath = captureProgressPath;
            _progress = CampaignProgress.Load(_progressPath);
            if (_campaign?.FirstLevel is { } l01)
            {
                StartCampaignLevel(l01, toast: false);
            }
        }
        else if (OS.GetEnvironment("TINDUSTRY_CAPTURE") == "1"
            && OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "mindustry-ui")
        {
            // Sparse wallet so cost chips show barred / insufficient materials.
            var fresh = FactoryContent.Load(_contentPath);
            _slice = new FactorySlice(
                fresh,
                new BeltGrid(),
                CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2));
            _slice.Wallet.AddMoney(5);
            _slice.Wallet.AddMaterial("iron-ore", 2);
            _slice.Wallet.AddMaterial("iron-plate", 0);
            _slice.Wallet.AddMaterial("copper-ore", 1);
            _slice.TryPlaceMiner(new GridPosition(2, 7), Direction.East);
            _visualDirty = true;
            _buildingsDirty = true;
            RebuildBeltVisual();
            RebuildBuildingVisuals();
            SyncResearchLocks();
            UpdateHud();
        }
        else if (OS.GetEnvironment("TINDUSTRY_CAPTURE") == "1"
            && OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") != "cursor"
            && OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") != "research"
            && OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") != "mindustry-ui")
        {
            var fresh = FactoryContent.Load(_contentPath);
            _slice = new FactorySlice(
                fresh,
                new BeltGrid(),
                CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2));
            _slice.Wallet.AddMoney(40);
            _slice.Wallet.AddMaterial("iron-ore", 12);
            _slice.Wallet.AddMaterial("iron-plate", 6);
            _slice.Wallet.AddMaterial("copper-ore", 4);
            _slice.Wallet.AddMaterial("copper-wire", 3);
            _slice.TryPlaceMiner(new GridPosition(2, 7), Direction.East);
            _visualDirty = true;
            _buildingsDirty = true;
            RebuildBeltVisual();
            RebuildBuildingVisuals();
            SyncResearchLocks();
            UpdateHud();
        }
        else if (OS.GetEnvironment("TINDUSTRY_CAPTURE") == "1"
            && OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "research")
        {
            var fresh = FactoryContent.Load(_contentPath);
            _slice = new FactorySlice(
                fresh,
                new BeltGrid(),
                CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2));
            _slice.Wallet.AddMoney(800);
            _slice.Wallet.AddMaterial("iron-plate", 80);
            _slice.Wallet.AddMaterial("copper-ore", 12);
            _slice.Wallet.AddMaterial("copper-wire", 12);
            _slice.TryPlaceMiner(new GridPosition(2, 7), Direction.East);
            _visualDirty = true;
            _buildingsDirty = true;
            RebuildBeltVisual();
            RebuildBuildingVisuals();
            SyncResearchLocks();
            UpdateHud();
        }

        // Capture / fresh: skip Home (except home capture mode). Otherwise show splash.
        var capture = OS.GetEnvironment("TINDUSTRY_CAPTURE") == "1";
        var captureMode = OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE");
        if (capture && captureMode == "home")
        {
            _home?.Open();
            var timer = GetTree().CreateTimer(1.0);
            timer.Timeout += () => _ = SavePortScreenshotsAsync();
        }
        else if (capture)
        {
            _home?.Close();
            var timer = GetTree().CreateTimer(1.0);
            timer.Timeout += () => _ = SavePortScreenshotsAsync();
        }
        else if (OS.GetEnvironment("TINDUSTRY_FRESH") == "1")
        {
            _home?.Close();
        }
        else
        {
            _home?.Open();
        }

        if (HasNode("Camera"))
        {
            var cam = GetNode<Camera2D>("Camera");
            // Frame sorter (18,2) + bridge (16–18,12) + craft loop.
            cam.Position = new Vector2(12f * TileSize, 8f * TileSize);
            cam.Zoom = new Vector2(0.5f, 0.5f);
        }
    }

    private void EnsureFactoryHud()
    {
        var layer = GetNode<CanvasLayer>("Hud");
        // Demote legacy debug label if present.
        if (layer.HasNode("Status"))
        {
            layer.GetNode("Status").QueueFree();
        }

        if (layer.HasNode("FactoryHud"))
        {
            _hud = layer.GetNode<FactoryHud>("FactoryHud");
        }
        else
        {
            _hud = new FactoryHud { Name = "FactoryHud" };
            layer.AddChild(_hud);
        }

        _hud.ToolChosen += OnHudToolChosen;
        _hud.SaveRequested += () => SaveSlice(FactorySliceSaveStore.ContinueSlotId, "Partita salvata (continua)");
        _hud.LoadRequested += () => LoadSlice(FactorySliceSaveStore.ContinueSlotId, "Partita caricata (continua)");
        _hud.SaveSlotRequested += () => SaveSlice(FactorySliceSaveStore.QuickSlotId, "Slot-1 salvato");
        _hud.LoadSlotRequested += () => LoadSlice(FactorySliceSaveStore.QuickSlotId, "Slot-1 caricato");
        _hud.ResearchRequested += ToggleResearch;
        _hud.MercatoRequested += ToggleMercato;
        _hud.CampaignRequested += ToggleCampaign;
        _hud.SetSelectedTool(FactoryHud.ToolKind.Cursor);
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
            if (_slice is not null)
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
            if (_slice is not null)
            {
                _hud?.ShowToast($"Vendita · Magazzino ${_slice.Wallet.Money}");
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

        _campaignSelect.LevelChosen += level =>
        {
            _returnHomeAfterCampaign = false;
            StartCampaignLevel(level, toast: true);
            _campaignSelect.Close();
            _home?.Close();
        };
        _campaignSelect.Closed += () =>
        {
            if (_returnHomeAfterCampaign)
            {
                _returnHomeAfterCampaign = false;
                _home?.Open();
            }
        };
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

        _home.ContinuaChosen += OnHomeContinua;
        _home.CampaignChosen += OnHomeCampaign;
        _home.NewGameChosen += OnHomeNewGame;
        _home.QuitChosen += () => GetTree().Quit();
    }

    private void OnHomeContinua()
    {
        if (!FactorySliceSaveStore.Exists(FactorySliceSaveStore.ContinueSlotId))
        {
            _home?.RefreshContinua();
            _hud?.ShowToast("Nessun salvataggio continua");
            return;
        }

        LoadSlice(FactorySliceSaveStore.ContinueSlotId, "Continua caricata");
        _home?.Close();
    }

    private void OnHomeCampaign()
    {
        _home?.Close();
        _returnHomeAfterCampaign = true;
        ToggleCampaign();
    }

    private void OnHomeNewGame()
    {
        StartNewSandbox(toast: true);
        _home?.Close();
    }

    private void StartNewSandbox(bool toast)
    {
        var content = FactoryContent.Load(_contentPath);
        _slice = new FactorySlice(
            content,
            new BeltGrid(),
            CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2));
        _slice.Wallet.AddMoney(180);
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
        if (toast)
        {
            _hud?.ShowToast("Nuova partita · sandbox");
        }
    }

    private void ToggleResearch()
    {
        if (_slice is null || _research is null)
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
        _research.Open(_slice);
    }

    private void ToggleMercato()
    {
        if (_slice is null || _mercato is null)
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
        _mercato.Open(_slice);
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

    private void StartCampaignLevel(CampaignLevelDefinition level, bool toast)
    {
        if (_campaign is null)
        {
            return;
        }

        var content = FactoryContent.Load(_contentPath);
        _slice = FactorySlice.CreateCampaignSlice(
            content, _campaign, level, new GridPosition(9, 14), coreSize: 2);
        // L01 teach: seed a bit of ore so Mercato sell objectives are reachable quickly
        // after placing miner, but also allow starting sells from stock if we add ore.
        if (level.Id.Contains("primi-passi", StringComparison.Ordinal))
        {
            _slice.Wallet.AddMaterial("iron-ore", 8);
        }

        _activeLevel = level;
        _levelCompleteToastShown = false;
        _tool = BuildTool.Cursor;
        _visualDirty = true;
        _buildingsDirty = true;
        RebuildBeltVisual();
        RebuildBuildingVisuals();
        SyncResearchLocks();
        UpdateHud();
        if (toast)
        {
            _hud?.ShowToast($"Campagna · {level.Name}");
        }
    }

    private void CheckCampaignComplete()
    {
        if (_slice is null || _activeLevel is null || _progress is null || _campaign is null)
        {
            return;
        }

        if (!CampaignProgress.AreAllObjectivesComplete(
                _activeLevel, _slice.Wallet, _slice.Session, _slice.Research))
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

    private void RestoreActiveLevelFromSlice()
    {
        if (_slice?.ActiveCampaignLevelId is not { } id || _campaign is null)
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
        if (_hud is null || _slice is null)
        {
            return;
        }

        var tools = new[]
        {
            FactoryHud.ToolKind.Belt,
            FactoryHud.ToolKind.Miner,
            FactoryHud.ToolKind.Smelter,
            FactoryHud.ToolKind.Assembler,
            FactoryHud.ToolKind.Junction,
            FactoryHud.ToolKind.Splitter,
            FactoryHud.ToolKind.Generator,
            FactoryHud.ToolKind.Sorter,
            FactoryHud.ToolKind.Bridge
        };
        _hud.SetToolsLocked(tools.Select(t =>
            (t, !_slice.IsStructureUnlocked(FactoryHud.StructureIdFor(t)))));

        if (_tool != BuildTool.Cursor && _hud.IsToolLocked(ToHudTool(_tool)))
        {
            ClearToCursor(toast: false);
        }
    }

    private void SaveSlice(string slotId, string toast)
    {
        if (_slice is null)
        {
            return;
        }

        try
        {
            FactorySliceSaveStore.Save(slotId, _slice.Capture());
            _hud?.ShowToast(toast);
            GD.Print($"Saved slice → {FactorySliceSaveStore.SlotPath(slotId)}");
        }
        catch (Exception ex)
        {
            _hud?.ShowToast("Salvataggio fallito");
            GD.PushError($"Save failed: {ex.Message}");
        }
    }

    private void LoadSlice(string slotId, string toast, bool quietFail = false)
    {
        if (!FactorySliceSaveStore.TryLoad(slotId, out var data) || data is null)
        {
            if (!quietFail)
            {
                _hud?.ShowToast($"Nessun save ({slotId})");
            }

            return;
        }

        try
        {
            var content = FactoryContent.Load(_contentPath);
            _slice = FactorySlice.Restore(content, data);
            _itemSprites.Clear();
            if (_itemsLayer is not null)
            {
                foreach (var child in _itemsLayer.GetChildren())
                {
                    child.QueueFree();
                }
            }

            _visualDirty = true;
            _buildingsDirty = true;
            RebuildBeltVisual();
            RebuildBuildingVisuals();
            SyncResearchLocks();
            RestoreActiveLevelFromSlice();
            UpdateHud();
            QueueRedraw();
            _hud?.ShowToast(toast);
            GD.Print($"Loaded slice ← {FactorySliceSaveStore.SlotPath(slotId)}");
        }
        catch (Exception ex)
        {
            _hud?.ShowToast("Caricamento fallito");
            GD.PushError($"Load failed: {ex.Message}");
        }
    }

    private void OnHudToolChosen(FactoryHud.ToolKind kind)
    {
        _tool = FromHudTool(kind);
        QueueRedraw();
    }

    private void ClearToCursor(bool toast = true)
    {
        _tool = BuildTool.Cursor;
        _draggingPlace = false;
        _hud?.ClearToolSelection(toast);
        QueueRedraw();
    }

    private void OnHudRotate()
    {
        _placeDir = DirectionMath.Right(_placeDir);
        _hud?.SetDirectionLabel(DirectionIt(_placeDir));
        _hud?.ShowToast($"Direzione: {DirectionIt(_placeDir)}");
        QueueRedraw();
    }

    private void SelectTool(BuildTool tool, bool toast = false)
    {
        if (tool != BuildTool.Cursor
            && _slice is not null
            && !_slice.IsStructureUnlocked(FactoryHud.StructureIdFor(ToHudTool(tool))))
        {
            _hud?.ShowToast($"{ToolIt(tool)} bloccato: sbloccalo in Ricerca (T).");
            return;
        }

        _tool = tool;
        _hud?.SetSelectedTool(ToHudTool(tool));
        if (toast)
        {
            _hud?.ShowToast(tool == BuildTool.Cursor ? "Cursore" : $"{ToolIt(tool)} selezionato");
        }

        QueueRedraw();
    }

    private static FactoryHud.ToolKind ToHudTool(BuildTool tool) => tool switch
    {
        BuildTool.Cursor => FactoryHud.ToolKind.Cursor,
        BuildTool.Miner => FactoryHud.ToolKind.Miner,
        BuildTool.Smelter => FactoryHud.ToolKind.Smelter,
        BuildTool.Assembler => FactoryHud.ToolKind.Assembler,
        BuildTool.Junction => FactoryHud.ToolKind.Junction,
        BuildTool.Splitter => FactoryHud.ToolKind.Splitter,
        BuildTool.Generator => FactoryHud.ToolKind.Generator,
        BuildTool.Sorter => FactoryHud.ToolKind.Sorter,
        BuildTool.Bridge => FactoryHud.ToolKind.Bridge,
        BuildTool.Belt => FactoryHud.ToolKind.Belt,
        _ => FactoryHud.ToolKind.Cursor
    };

    private static BuildTool FromHudTool(FactoryHud.ToolKind tool) => tool switch
    {
        FactoryHud.ToolKind.Cursor => BuildTool.Cursor,
        FactoryHud.ToolKind.Miner => BuildTool.Miner,
        FactoryHud.ToolKind.Smelter => BuildTool.Smelter,
        FactoryHud.ToolKind.Assembler => BuildTool.Assembler,
        FactoryHud.ToolKind.Junction => BuildTool.Junction,
        FactoryHud.ToolKind.Splitter => BuildTool.Splitter,
        FactoryHud.ToolKind.Generator => BuildTool.Generator,
        FactoryHud.ToolKind.Sorter => BuildTool.Sorter,
        FactoryHud.ToolKind.Bridge => BuildTool.Bridge,
        FactoryHud.ToolKind.Belt => BuildTool.Belt,
        _ => BuildTool.Cursor
    };

    private static string ToolIt(BuildTool tool) => tool switch
    {
        BuildTool.Cursor => "Cursore",
        BuildTool.Belt => "Nastro",
        BuildTool.Miner => "Minatore",
        BuildTool.Smelter => "Forno",
        BuildTool.Assembler => "Assemblatore",
        BuildTool.Junction => "Giunzione",
        BuildTool.Splitter => "Splitter",
        BuildTool.Generator => "Generatore",
        BuildTool.Sorter => "Selezionatore",
        BuildTool.Bridge => "Ponte",
        _ => "?"
    };

    private static string DirectionIt(Direction dir) => dir switch
    {
        Direction.North => "Nord",
        Direction.East => "Est",
        Direction.South => "Sud",
        Direction.West => "Ovest",
        _ => "?"
    };

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_home?.IsOpen == true)
        {
            return;
        }

        if (_slice is null)
        {
            return;
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.T)
            {
                ToggleResearch();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.M)
            {
                ToggleMercato();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.G)
            {
                ToggleCampaign();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_research?.IsOpen == true || _mercato?.IsOpen == true || _campaignSelect?.IsOpen == true)
            {
                return;
            }

            if (key.Keycode == Key.Escape || key.Keycode == Key.Quoteleft)
            {
                if (_tool != BuildTool.Cursor)
                {
                    ClearToCursor();
                }
                else if (key.Keycode == Key.Escape)
                {
                    // Raylib parity: Esc from cursor → Home.
                    _home?.Open();
                }
                else
                {
                    ClearToCursor();
                }

                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key1 || key.Keycode == Key.N)
            {
                SelectTool(BuildTool.Belt, toast: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key2)
            {
                SelectTool(BuildTool.Miner, toast: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key3 || key.Keycode == Key.F)
            {
                SelectTool(BuildTool.Smelter, toast: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key4 || key.Keycode == Key.A)
            {
                SelectTool(BuildTool.Assembler, toast: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key5 || key.Keycode == Key.J)
            {
                SelectTool(BuildTool.Junction, toast: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key6)
            {
                SelectTool(BuildTool.Splitter, toast: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key7 || key.Keycode == Key.G)
            {
                SelectTool(BuildTool.Generator, toast: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key8)
            {
                SelectTool(BuildTool.Sorter, toast: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.Key9)
            {
                SelectTool(BuildTool.Bridge, toast: true);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.C)
            {
                CycleSorterFilter();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.F5)
            {
                SaveSlice(FactorySliceSaveStore.ContinueSlotId, "Partita salvata (continua)");
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.F9)
            {
                LoadSlice(FactorySliceSaveStore.ContinueSlotId, "Partita caricata (continua)");
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.F6)
            {
                SaveSlice(FactorySliceSaveStore.QuickSlotId, "Slot-1 salvato");
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.F7)
            {
                LoadSlice(FactorySliceSaveStore.QuickSlotId, "Slot-1 caricato");
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.Keycode == Key.R)
            {
                OnHudRotate();
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (_research?.IsOpen == true || _mercato?.IsOpen == true || _campaignSelect?.IsOpen == true)
        {
            return;
        }

        if (@event is InputEventMouseButton mouse)
        {
            var cell = ScreenToCell(mouse.Position);
            if (mouse.ButtonIndex == MouseButton.Left)
            {
                if (mouse.Pressed)
                {
                    if (_tool == BuildTool.Cursor)
                    {
                        // Cursor mode: no place — leave click for camera pan / look.
                        return;
                    }

                    _draggingPlace = _tool is BuildTool.Belt or BuildTool.Junction or BuildTool.Splitter
                        or BuildTool.Sorter;
                    TryPlaceAt(cell);
                    GetViewport().SetInputAsHandled();
                }
                else
                {
                    _draggingPlace = false;
                }
            }
            else if (mouse.ButtonIndex == MouseButton.Right)
            {
                if (mouse.Pressed)
                {
                    _draggingRemove = true;
                    TryRemoveAt(cell);
                    GetViewport().SetInputAsHandled();
                }
                else
                {
                    _draggingRemove = false;
                }
            }
        }
        else if (@event is InputEventMouseMotion motion)
        {
            var cell = ScreenToCell(motion.Position);
            if (!_hover.Equals(cell))
            {
                _hover = cell;
                QueueRedraw();
            }

            if (_draggingPlace && _tool is BuildTool.Belt or BuildTool.Junction or BuildTool.Splitter
                or BuildTool.Sorter)
            {
                TryPlaceAt(cell);
            }
            else if (_draggingRemove)
            {
                TryRemoveAt(cell);
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_home?.IsOpen == true || _slice is null)
        {
            return;
        }

        _slice.Tick((float)delta);
        if (_visualDirty)
        {
            RebuildBeltVisual();
            _visualDirty = false;
        }

        if (_buildingsDirty)
        {
            RebuildBuildingVisuals();
            _buildingsDirty = false;
        }

        SyncItemSprites();
        var genLive = _slice.Generators.Any(g => g.IsGenerating);
        if (genLive != _lastGenLive)
        {
            _lastGenLive = genLive;
            _buildingsDirty = true;
        }

        UpdateHud();
        CheckCampaignComplete();
        QueueRedraw();
    }

    public override void _Draw()
    {
        var ground = new Color(0.14f, 0.18f, 0.16f);
        var groundAlt = new Color(0.16f, 0.21f, 0.18f);
        for (var y = 0; y < MapHeight; y++)
        {
            for (var x = 0; x < MapWidth; x++)
            {
                var rect = new Rect2(x * TileSize, y * TileSize, TileSize, TileSize);
                DrawRect(rect, ((x + y) % 2 == 0) ? ground : groundAlt);
            }
        }

        var grid = new Color(0.22f, 0.28f, 0.24f, 0.85f);
        for (var x = 0; x <= MapWidth; x++)
        {
            var px = x * TileSize;
            DrawLine(new Vector2(px, 0), new Vector2(px, MapHeight * TileSize), grid, 1f);
        }

        for (var y = 0; y <= MapHeight; y++)
        {
            var py = y * TileSize;
            DrawLine(new Vector2(0, py), new Vector2(MapWidth * TileSize, py), grid, 1f);
        }

        if (_slice is null)
        {
            return;
        }

        var coreFill = new Color(0.18f, 0.32f, 0.48f, 1f);
        var coreEdge = new Color(0.55f, 0.82f, 1f, 1f);
        // Draw core as one footprint box (not per-tile wash).
        if (_slice.CoreTiles.Count > 0)
        {
            var minX = _slice.CoreTiles.Min(t => t.X);
            var minY = _slice.CoreTiles.Min(t => t.Y);
            var maxX = _slice.CoreTiles.Max(t => t.X);
            var maxY = _slice.CoreTiles.Max(t => t.Y);
            var rect = new Rect2(
                minX * TileSize + 1,
                minY * TileSize + 1,
                (maxX - minX + 1) * TileSize - 2,
                (maxY - minY + 1) * TileSize - 2);
            DrawRect(rect, coreFill);
            DrawRect(rect, coreEdge, false, 3f);
        }

        // Deposit tint under miners stays soft; the building pad is the readable frame.
        foreach (var miner in _slice.Miners)
        {
            var deposit = miner.OutputItemId switch
            {
                "copper-ore" => new Color(0.55f, 0.38f, 0.22f, 0.4f),
                "coal" => new Color(0.18f, 0.18f, 0.16f, 0.45f),
                _ => new Color(0.45f, 0.32f, 0.18f, 0.35f)
            };
            foreach (var tile in miner.OccupiedTiles())
            {
                DrawRect(new Rect2(tile.X * TileSize, tile.Y * TileSize, TileSize, TileSize), deposit);
            }
        }

        if (_hover is { } hover
            && hover.X >= 0 && hover.Y >= 0
            && hover.X < MapWidth && hover.Y < MapHeight
            && _tool != BuildTool.Cursor)
        {
            DrawGhost(hover);
        }
    }

    private void DrawGhost(GridPosition hover)
    {
        if (_slice is null)
        {
            return;
        }

        var size = _tool switch
        {
            BuildTool.Miner => MinerProducer.Size,
            BuildTool.Smelter => SmelterStub.Size,
            BuildTool.Assembler => SmelterStub.Size,
            BuildTool.Generator => GeneratorStub.Size,
            _ => 1
        };

        var ok = size == 1
            ? _slice.CanOccupy(hover)
            : _slice.CanOccupyFootprint(hover, size)
              || (_tool == BuildTool.Miner && _slice.Miners.Count == 1)
              || (_tool == BuildTool.Smelter && _slice.Smelters.Count == 1)
              || (_tool == BuildTool.Assembler && _slice.Assemblers.Count == 1)
              || (_tool == BuildTool.Generator && _slice.Generators.Count == 1);

        var ghost = ok
            ? new Color(0.35f, 0.85f, 0.55f, 0.35f)
            : new Color(0.9f, 0.25f, 0.2f, 0.35f);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var rect = new Rect2((hover.X + x) * TileSize, (hover.Y + y) * TileSize, TileSize, TileSize);
                DrawRect(rect, ghost);
                DrawRect(rect, new Color(0.9f, 0.95f, 0.85f, 0.9f), false, 2f);
            }
        }

        if (_tool is BuildTool.Belt or BuildTool.Junction or BuildTool.Splitter
            or BuildTool.Sorter or BuildTool.Bridge)
        {
            DrawDirectionHint(hover, _placeDir);
        }

        if (_tool == BuildTool.Bridge)
        {
            DrawBridgeGhost(hover);
        }
    }

    private void DrawBridgeGhost(GridPosition entry)
    {
        if (_slice is null)
        {
            return;
        }

        for (var span = BeltGridCell.MinBridgeSpan; span <= BeltGridCell.MaxBridgeSpan; span++)
        {
            var exit = entry.Step(_placeDir, span);
            if (!_slice.CanOccupy(entry) || !_slice.CanOccupy(exit))
            {
                continue;
            }

            if (_slice.Belts.Contains(entry) || _slice.Belts.Contains(exit))
            {
                continue;
            }

            var mid = new Color(0.55f, 0.7f, 0.95f, 0.28f);
            var tip = new Color(0.7f, 0.85f, 1f, 0.55f);
            DrawRect(new Rect2(entry.X * TileSize, entry.Y * TileSize, TileSize, TileSize), tip);
            DrawRect(new Rect2(exit.X * TileSize, exit.Y * TileSize, TileSize, TileSize), tip);
            var a = CellCenter(entry);
            var b = CellCenter(exit);
            DrawLine(a, b, mid, TileSize * MindustryBeltVisual.BridgeThicknessScale);
            return;
        }
    }

    private void DrawDirectionHint(GridPosition cell, Direction dir)
    {
        var center = CellCenter(cell);
        var (dx, dy) = DirectionMath.ToOffset(dir);
        var tip = center + new Vector2(dx, dy) * (TileSize * 0.32f);
        DrawLine(center, tip, new Color(0.95f, 0.95f, 0.7f, 0.95f), 3f);
        DrawCircle(tip, 4f, new Color(0.95f, 0.95f, 0.7f, 0.95f));
    }

    private void TryPlaceAt(GridPosition cell)
    {
        if (_slice is null || _tool == BuildTool.Cursor)
        {
            return;
        }

        var structureId = FactoryHud.StructureIdFor(ToHudTool(_tool));
        if (!_slice.IsStructureUnlocked(structureId))
        {
            _hud?.ShowToast($"{ToolIt(_tool)} bloccato: sbloccalo in Ricerca (T).");
            return;
        }

        switch (_tool)
        {
            case BuildTool.Belt:
                if (_slice.TryPlaceBelt(cell, _placeDir))
                {
                    _visualDirty = true;
                }

                break;
            case BuildTool.Miner:
                if (_slice.TryPlaceMiner(cell, _placeDir))
                {
                    _buildingsDirty = true;
                }

                break;
            case BuildTool.Smelter:
                if (_slice.TryPlaceSmelter(cell, _placeDir))
                {
                    _buildingsDirty = true;
                }

                break;
            case BuildTool.Assembler:
                if (_slice.TryPlaceAssembler(cell, _placeDir))
                {
                    _buildingsDirty = true;
                }

                break;
            case BuildTool.Junction:
                if (_slice.TryPlaceJunction(cell, _placeDir))
                {
                    _visualDirty = true;
                }

                break;
            case BuildTool.Splitter:
                if (_slice.TryPlaceSplitter(cell, _placeDir))
                {
                    _visualDirty = true;
                }

                break;
            case BuildTool.Generator:
                if (_slice.TryPlaceGenerator(cell, _placeDir))
                {
                    _buildingsDirty = true;
                }

                break;
            case BuildTool.Sorter:
                if (_slice.TryPlaceSorter(cell, _placeDir, _sorterFilterId))
                {
                    _visualDirty = true;
                }

                break;
            case BuildTool.Bridge:
                if (_slice.TryPlaceBridge(cell, _placeDir))
                {
                    _visualDirty = true;
                }

                break;
        }
    }

    private void CycleSorterFilter()
    {
        var idx = Array.FindIndex(
            SorterFilterIds,
            id => string.Equals(id, _sorterFilterId, StringComparison.Ordinal));
        _sorterFilterId = SorterFilterIds[(idx + 1 + SorterFilterIds.Length) % SorterFilterIds.Length];

        // Also cycle hovered / selected sorter cell if present.
        if (_slice is not null
            && _hover is { } hover
            && _slice.Belts.TryGet(hover, out var cell)
            && cell.Kind == LogisticsKind.Sorter)
        {
            cell.CycleFilterItem(SorterFilterIds);
            _sorterFilterId = cell.FilterItemId ?? _sorterFilterId;
            _visualDirty = true;
        }

        var label = _slice?.Content.DisplayName(_sorterFilterId) ?? _sorterFilterId;
        _hud?.ShowToast($"Filtro: {label}");
    }

    private void TryRemoveAt(GridPosition cell)
    {
        if (_slice is null)
        {
            return;
        }

        if (_slice.TryRemoveBuildingAt(cell))
        {
            _buildingsDirty = true;
            return;
        }

        if (_slice.TryRemoveBelt(cell))
        {
            _visualDirty = true;
        }
    }

    private GridPosition ScreenToCell(Vector2 screenPos)
    {
        _ = screenPos;
        var world = GetGlobalMousePosition();
        var x = Mathf.FloorToInt(world.X / TileSize);
        var y = Mathf.FloorToInt(world.Y / TileSize);
        return new GridPosition(x, y);
    }

    private void EnsureBuildingsLayer()
    {
        if (HasNode("Buildings"))
        {
            _buildingsLayer = GetNode<Node2D>("Buildings");
            return;
        }

        _buildingsLayer = new Node2D { Name = "Buildings", ZIndex = 3 };
        AddChild(_buildingsLayer);
        // Hide legacy scene Miner node if present — rebuilt dynamically.
        if (HasNode("Miner"))
        {
            GetNode("Miner").QueueFree();
        }
    }

    private void RebuildBuildingVisuals()
    {
        if (_slice is null || _buildingsLayer is null)
        {
            return;
        }

        foreach (var child in _buildingsLayer.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var miner in _slice.Miners)
        {
            var node = BuildingPad.Create(
                MinerProducer.Size,
                TileSize,
                fill: new Color(0.22f, 0.2f, 0.16f, 1f),
                border: new Color(0.85f, 0.72f, 0.4f, 1f),
                icon: GD.Load<Texture2D>("res://assets/miner.png"),
                iconScale: 1.05f);
            node.Name = $"Miner_{miner.Position.X}_{miner.Position.Y}";
            node.Position = FootprintCenter(miner.Position, MinerProducer.Size);
            _buildingsLayer.AddChild(node);
        }

        foreach (var smelter in _slice.Smelters)
        {
            var node = BuildingPad.Create(
                SmelterStub.Size,
                TileSize,
                fill: new Color(0.2f, 0.16f, 0.14f, 1f),
                border: new Color(0.95f, 0.55f, 0.28f, 1f),
                icon: GD.Load<Texture2D>("res://assets/smelter.png"),
                iconScale: 1.1f);
            node.Name = $"Smelter_{smelter.Position.X}_{smelter.Position.Y}";
            node.Position = FootprintCenter(smelter.Position, SmelterStub.Size);
            _buildingsLayer.AddChild(node);
        }

        foreach (var assembler in _slice.Assemblers)
        {
            var node = BuildingPad.Create(
                SmelterStub.Size,
                TileSize,
                fill: new Color(0.14f, 0.18f, 0.22f, 1f),
                border: new Color(0.45f, 0.78f, 0.95f, 1f),
                icon: GD.Load<Texture2D>("res://assets/assembler.png"),
                iconScale: 1.1f);
            node.Name = $"Assembler_{assembler.Position.X}_{assembler.Position.Y}";
            node.Position = FootprintCenter(assembler.Position, SmelterStub.Size);
            _buildingsLayer.AddChild(node);
        }

        foreach (var gen in _slice.Generators)
        {
            var live = gen.IsGenerating;
            var node = BuildingPad.Create(
                GeneratorStub.Size,
                TileSize,
                fill: new Color(0.18f, 0.16f, 0.12f, 1f),
                border: live
                    ? new Color(0.98f, 0.82f, 0.28f, 1f)
                    : new Color(0.55f, 0.48f, 0.28f, 1f),
                icon: GD.Load<Texture2D>("res://assets/generator.png"),
                iconScale: 1.05f);
            node.Name = $"Generator_{gen.Position.X}_{gen.Position.Y}";
            node.Position = FootprintCenter(gen.Position, GeneratorStub.Size);
            _buildingsLayer.AddChild(node);
        }
    }

    private static Vector2 FootprintCenter(GridPosition origin, int size) =>
        new((origin.X + size * 0.5f) * TileSize, (origin.Y + size * 0.5f) * TileSize);

    private void EnsureBeltVisual()
    {
        var belts = GetNode<Node2D>("Belts");
        foreach (var child in belts.GetChildren())
        {
            child.QueueFree();
        }

        _beltVisual = new MindustryBeltVisual { Name = "MindustryBelt" };
        belts.AddChild(_beltVisual);
    }

    private void RebuildBeltVisual()
    {
        if (_slice is null || _beltVisual is null)
        {
            return;
        }

        _beltVisual.ConfigureFromGrid(
            _slice.Belts,
            _slice.BeltDefinition.RateItemsPerSecond,
            TileSize);
    }

    private void SyncItemSprites()
    {
        if (_slice is null || _itemsLayer is null)
        {
            return;
        }

        var live = new HashSet<long>();
        foreach (var cell in _slice.Belts.Cells.Values)
        {
            foreach (var item in cell.Items)
            {
                live.Add(item.Id);
                if (!_itemSprites.TryGetValue(item.Id, out var sprite))
                {
                    var holder = new Node2D
                    {
                        Name = $"Item_{item.Id}",
                        ZIndex = 5
                    };
                    // Opaque disc so PNG alpha never shows the belt through the item.
                    var back = new Polygon2D
                    {
                        Name = "Back",
                        Color = new Color(0.1f, 0.12f, 0.14f, 1f),
                        Polygon =
                        [
                            new(-14, -14), new(14, -14), new(14, 14), new(-14, 14)
                        ]
                    };
                    holder.AddChild(back);
                    var tex = ResolveItemTexture(item.ItemId);
                    sprite = new Sprite2D
                    {
                        Name = "Sprite",
                        Texture = tex,
                        Centered = true,
                        Scale = new Vector2(0.85f, 0.85f),
                        Modulate = Colors.White
                    };
                    holder.AddChild(sprite);
                    _itemsLayer.AddChild(holder);
                    _itemSprites[item.Id] = sprite;
                }
                else
                {
                    var tex = ResolveItemTexture(item.ItemId);
                    if (tex is not null)
                    {
                        sprite.Texture = tex;
                    }

                    sprite.Modulate = Colors.White;
                }

                var from = CellCenter(cell.Position);
                var to = CellCenter(cell.Position.Step(cell.Direction));
                var pos = from.Lerp(to, Mathf.Clamp(item.Progress, 0f, 1f));
                var holderNode = sprite.GetParent() as Node2D ?? sprite;
                holderNode.Position = pos;
                // Corner = galleria: items disappear while inside the turn.
                holderNode.Visible = !_slice.Belts.IsCorner(cell.Position);
            }
        }

        var dead = _itemSprites.Keys.Where(id => !live.Contains(id)).ToList();
        foreach (var id in dead)
        {
            var sprite = _itemSprites[id];
            var parent = sprite.GetParent();
            if (parent is not null && parent != _itemsLayer)
            {
                parent.QueueFree();
            }
            else
            {
                sprite.QueueFree();
            }

            _itemSprites.Remove(id);
        }
    }

    private void UpdateHud()
    {
        if (_hud is null || _slice is null)
        {
            return;
        }

        var onBelt = _slice.Belts.Cells.Values.Sum(c => c.Items.Count);
        var gensLive = _slice.Generators.Count(g => g.IsGenerating);
        _hud.BindEconomy(_slice.Content, _slice.Wallet);
        _hud.UpdateStock(
            ShortName(_slice.Content.DisplayName("iron-ore")),
            _slice.Wallet.MaterialCount("iron-ore"),
            ShortName(_slice.Content.DisplayName("iron-plate")),
            _slice.Wallet.MaterialCount("iron-plate"),
            ShortName(_slice.Content.DisplayName("copper-ore")),
            _slice.Wallet.MaterialCount("copper-ore"),
            ShortName(_slice.Content.DisplayName("copper-wire")),
            _slice.Wallet.MaterialCount("copper-wire"),
            _slice.CoreDeliveredItems,
            onBelt,
            _slice.Wallet.Money,
            gensLive,
            _slice.Generators.Count);
        _hud.SetDirectionLabel(DirectionIt(_placeDir));
        if (_activeLevel is not null)
        {
            _hud.UpdateObjectives(
                _activeLevel,
                _slice.Wallet,
                _slice.Session,
                _slice.Research,
                id => ShortName(_slice.Content.DisplayName(id)));
        }
        else
        {
            _hud.ClearObjectives();
        }
    }

    private static string ShortName(string display)
    {
        // Compact stock labels: "Lastra di ferro" → "Lastra ferro"
        return display
            .Replace(" di ", " ", StringComparison.Ordinal)
            .Replace("grezzo", "grezzo", StringComparison.Ordinal);
    }

    private Texture2D? ResolveItemTexture(string itemId) => itemId switch
    {
        "iron-plate" => _plateTex,
        "copper-ore" => _copperOreTex,
        "copper-wire" => _copperWireTex,
        "coal" => _coalTex,
        _ => _oreTex
    };

    private static Vector2 CellCenter(GridPosition cell) =>
        new((cell.X + 0.5f) * TileSize, (cell.Y + 0.5f) * TileSize);

    private static string ResolveContentPath()
    {
        var candidates = new[]
        {
            ProjectSettings.GlobalizePath("res://../data/content.json"),
            ProjectSettings.GlobalizePath("res://data/content.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "data", "content.json")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "data", "content.json")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "data", "content.json")),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException(
            "content.json non trovato. Apri il progetto dalla cartella godot/ del repo tIndustry.");
    }

    private static string ResolveCampaignPath()
    {
        var candidates = new[]
        {
            ProjectSettings.GlobalizePath("res://../data/campaign.json"),
            ProjectSettings.GlobalizePath("res://data/campaign.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "data", "campaign.json")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "data", "campaign.json")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "data", "campaign.json")),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException(
            "campaign.json non trovato. Apri il progetto dalla cartella godot/ del repo tIndustry.");
    }

    private async Task CaptureHomeShotsAsync(string destDir)
    {
        _home?.Open();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        var splash = GetViewport().GetTexture().GetImage();
        splash.SavePng(Path.Combine(destDir, "godot-port-home-splash.png"));
        splash.SavePng("/opt/cursor/artifacts/godot-port-home-splash.png");
        GD.Print($"Saved home splash → {destDir}");

        _home?.Close();
        _returnHomeAfterCampaign = true;
        if (_campaign is not null && _progress is not null)
        {
            _campaignSelect?.Open(_campaign, _progress);
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
        var campagna = GetViewport().GetTexture().GetImage();
        campagna.SavePng(Path.Combine(destDir, "godot-port-home-campagna.png"));
        campagna.SavePng("/opt/cursor/artifacts/godot-port-home-campagna.png");

        _returnHomeAfterCampaign = false;
        _campaignSelect?.Close();
        StartNewSandbox(toast: false);
        _home?.Close();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        var sandbox = GetViewport().GetTexture().GetImage();
        sandbox.SavePng(Path.Combine(destDir, "godot-port-home-sandbox.png"));
        sandbox.SavePng("/opt/cursor/artifacts/godot-port-home-sandbox.png");

        GD.Print("Home screenshot set complete.");
        GetTree().Quit();
    }

    private async Task CaptureCampaignShotsAsync(string destDir)
    {
        if (_slice is null || _hud is null || _campaign is null || _progress is null)
        {
            return;
        }

        _campaignSelect?.Open(_campaign, _progress);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        var selectShot = GetViewport().GetTexture().GetImage();
        selectShot.SavePng(Path.Combine(destDir, "godot-port-campaign-select.png"));
        selectShot.SavePng("/opt/cursor/artifacts/godot-port-campaign-select.png");
        GD.Print($"Saved campaign select → {destDir}");

        _campaignSelect?.Close();
        if (_campaign.FirstLevel is { } l01)
        {
            StartCampaignLevel(l01, toast: false);
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
        var objShot = GetViewport().GetTexture().GetImage();
        objShot.SavePng(Path.Combine(destDir, "godot-port-campaign-objectives.png"));
        objShot.SavePng("/opt/cursor/artifacts/godot-port-campaign-objectives.png");

        // Progress: sell toward L01 objectives.
        while (_slice.Wallet.MaterialCount("iron-ore") > 0
               && _activeLevel is not null
               && !CampaignProgress.AreAllObjectivesComplete(
                   _activeLevel, _slice.Wallet, _slice.Session, _slice.Research))
        {
            if (!_slice.TrySellFromWallet("iron-ore", 1))
            {
                break;
            }
        }

        CheckCampaignComplete();
        UpdateHud();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        var progressShot = GetViewport().GetTexture().GetImage();
        progressShot.SavePng(Path.Combine(destDir, "godot-port-campaign-progress.png"));
        progressShot.SavePng("/opt/cursor/artifacts/godot-port-campaign-progress.png");

        _campaignSelect?.Open(_campaign, _progress);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        var unlockedShot = GetViewport().GetTexture().GetImage();
        unlockedShot.SavePng(Path.Combine(destDir, "godot-port-campaign-l02-unlocked.png"));
        unlockedShot.SavePng("/opt/cursor/artifacts/godot-port-campaign-l02-unlocked.png");

        GD.Print("Campaign screenshot set complete.");
        GetTree().Quit();
    }

    private async Task SavePortScreenshotsAsync()
    {
        var mediaCandidates = new[]
        {
            "/cursor/stores/bc-6a5b42f7-8a62-4dc2-b71f-4a3a4f9c2232/media",
            ProjectSettings.GlobalizePath("res://artifacts"),
            "/opt/cursor/artifacts"
        };

        string? destDir = null;
        foreach (var d in mediaCandidates)
        {
            try
            {
                Directory.CreateDirectory(d);
                destDir = d;
                break;
            }
            catch
            {
                // try next
            }
        }

        if (destDir is null || _slice is null || _hud is null)
        {
            GD.PushWarning("Nessuna cartella screenshot / hud null.");
            return;
        }

        var cam = HasNode("Camera") ? GetNode<Camera2D>("Camera") : null;
        if (cam is not null)
        {
            cam.Position = new Vector2(8f * TileSize, 8f * TileSize);
            cam.Zoom = new Vector2(0.65f, 0.65f);
        }

        ClearToCursor(toast: false);
        SyncResearchLocks();
        UpdateHud();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);

        if (OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "research")
        {
            await CaptureResearchShotsAsync(destDir);
            return;
        }

        if (OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "campaign")
        {
            await CaptureCampaignShotsAsync(destDir);
            return;
        }

        if (OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "mindustry-ui")
        {
            await CaptureMindustryUiShotsAsync(destDir);
            return;
        }

        if (OS.GetEnvironment("TINDUSTRY_CAPTURE_MODE") == "home")
        {
            await CaptureHomeShotsAsync(destDir);
            return;
        }

        // Default / mercato: Core $ HUD + Mercato sell loop.
        var coreShot = GetViewport().GetTexture().GetImage();
        coreShot.SavePng(Path.Combine(destDir, "godot-port-mercato-core-money.png"));
        coreShot.SavePng("/opt/cursor/artifacts/godot-port-mercato-core-money.png");
        GD.Print($"Saved mercato core-money → {destDir}");

        _mercato?.Open(_slice);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        var panelShot = GetViewport().GetTexture().GetImage();
        panelShot.SavePng(Path.Combine(destDir, "godot-port-mercato-panel.png"));
        panelShot.SavePng("/opt/cursor/artifacts/godot-port-mercato-panel.png");

        var beforeMoney = _slice.Wallet.Money;
        if (!_slice.TrySellFromWallet("iron-ore", 3))
        {
            GD.PushError("Capture sell iron-ore failed");
        }
        else
        {
            _hud.ShowToast($"Venduti 3 ore → +${_slice.Wallet.Money - beforeMoney}");
        }

        _mercato?.Refresh();
        UpdateHud();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
        var sellShot = GetViewport().GetTexture().GetImage();
        sellShot.SavePng(Path.Combine(destDir, "godot-port-mercato-sell.png"));
        sellShot.SavePng("/opt/cursor/artifacts/godot-port-mercato-sell.png");

        SaveSlice(FactorySliceSaveStore.ContinueSlotId, "Partita salvata (continua)");
        LoadSlice(FactorySliceSaveStore.ContinueSlotId, "Partita caricata (continua)");
        if (_slice.Session.SaleIncome <= 0)
        {
            GD.PushError("capture restore sale income");
        }

        _mercato?.Open(_slice);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        var roundtrip = GetViewport().GetTexture().GetImage();
        roundtrip.SavePng(Path.Combine(destDir, "godot-port-mercato-save-roundtrip.png"));
        roundtrip.SavePng("/opt/cursor/artifacts/godot-port-mercato-save-roundtrip.png");

        GD.Print("Mercato screenshot set complete.");
        GetTree().Quit();
    }

    private async Task CaptureMindustryUiShotsAsync(string destDir)
    {
        if (_slice is null || _hud is null)
        {
            return;
        }

        async Task SaveNamed(string name)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree().CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
            var img = GetViewport().GetTexture().GetImage();
            img.SavePng(Path.Combine(destDir, name));
            img.SavePng(Path.Combine("/opt/cursor/artifacts", name));
            GD.Print($"Screenshot: {name}");
        }

        // 1) Empty reserved info slot (cursor) — palette anchored, slot invisible.
        ClearToCursor(toast: false);
        UpdateHud();
        await SaveNamed("godot-port-mindustry-ui-info-slot-empty.png");

        var fullEmpty = GetViewport().GetTexture().GetImage();
        var dockW = Math.Min(360, fullEmpty.GetWidth());
        var dockH = Math.Min(300, fullEmpty.GetHeight());
        var emptyCrop = fullEmpty.GetRegion(new Rect2I(
            fullEmpty.GetWidth() - dockW, fullEmpty.GetHeight() - dockH, dockW, dockH));
        emptyCrop.SavePng(Path.Combine(destDir, "godot-port-mindustry-ui-info-slot-empty-crop.png"));
        emptyCrop.SavePng("/opt/cursor/artifacts/godot-port-mindustry-ui-info-slot-empty-crop.png");

        // 2) Filled slot — miner selected → name + barred costs in reserved strip.
        _hud.SetSelectedTool(FactoryHud.ToolKind.Miner);
        OnHudToolChosen(FactoryHud.ToolKind.Miner);
        UpdateHud();
        await SaveNamed("godot-port-mindustry-ui-info-slot-filled.png");

        var fullFilled = GetViewport().GetTexture().GetImage();
        var filledCrop = fullFilled.GetRegion(new Rect2I(
            fullFilled.GetWidth() - dockW, fullFilled.GetHeight() - dockH, dockW, dockH));
        filledCrop.SavePng(Path.Combine(destDir, "godot-port-mindustry-ui-info-slot-filled-crop.png"));
        filledCrop.SavePng("/opt/cursor/artifacts/godot-port-mindustry-ui-info-slot-filled-crop.png");

        // 3) ? modal still carries prose / I/O.
        if (!_slice.IsStructureUnlocked("smelter"))
        {
            _slice.Wallet.AddMoney(500);
            _slice.Wallet.AddMaterial("iron-plate", 40);
            _ = _slice.TryUnlockStructure("smelter");
            SyncResearchLocks();
        }

        _hud.SetSelectedTool(FactoryHud.ToolKind.Smelter);
        OnHudToolChosen(FactoryHud.ToolKind.Smelter);
        while (_slice.Wallet.MaterialCount("iron-plate") > 0)
        {
            _slice.Wallet.TrySpend(0, [new ResourceAmount("iron-plate", 1)]);
        }

        UpdateHud();
        _hud.OpenBlockDetailForCapture();
        await SaveNamed("godot-port-mindustry-ui-info-slot-detail-modal.png");
        _hud.CloseBlockDetailForCapture();

        GD.Print("Mindustry info-slot screenshot set complete.");
        GetTree().Quit();
    }

    private async Task CaptureResearchShotsAsync(string destDir)
    {
        if (_slice is null || _hud is null)
        {
            return;
        }

        var lockedFull = GetViewport().GetTexture().GetImage();
        var barH = Math.Min(170, lockedFull.GetHeight());
        var lockedBar = lockedFull.GetRegion(
            new Rect2I(0, lockedFull.GetHeight() - barH, lockedFull.GetWidth(), barH));
        lockedBar.SavePng(Path.Combine(destDir, "godot-port-techtree-graph-locked-toolbar.png"));
        lockedBar.SavePng("/opt/cursor/artifacts/godot-port-techtree-graph-locked-toolbar.png");

        _research?.Open(_slice);
        _research?.SelectStructure("smelter");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.55), SceneTreeTimer.SignalName.Timeout);
        var panelShot = GetViewport().GetTexture().GetImage();
        panelShot.SavePng(Path.Combine(destDir, "godot-port-techtree-graph-panel.png"));
        panelShot.SavePng("/opt/cursor/artifacts/godot-port-techtree-graph-panel.png");

        if (!_slice.TryUnlockStructure("smelter"))
        {
            GD.PushError("Capture unlock smelter failed");
        }

        _research?.SelectStructure("assembler");
        _research?.Refresh();
        SyncResearchLocks();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.45), SceneTreeTimer.SignalName.Timeout);
        var afterUnlock = GetViewport().GetTexture().GetImage();
        afterUnlock.SavePng(Path.Combine(destDir, "godot-port-techtree-graph-path.png"));
        afterUnlock.SavePng("/opt/cursor/artifacts/godot-port-techtree-graph-path.png");

        GD.Print("Tech-tree graph screenshot set complete.");
        GetTree().Quit();
    }

    private static Image CropCenterCells(Image src, int cropCells)
    {
        var span = cropCells * TileSize;
        var x0 = Math.Max(0, (src.GetWidth() - span) / 2);
        var y0 = Math.Max(0, (src.GetHeight() - span) / 2);
        var w = Math.Min(span, src.GetWidth() - x0);
        var h = Math.Min(span, src.GetHeight() - y0);
        return src.GetRegion(new Rect2I(x0, y0, w, h));
    }

    // Legacy corner QA capture kept for local debugging (not auto-run in Phase D).
    private async Task SaveCornerShotSetAsync(string destDir)
    {
        if (_slice is null)
        {
            return;
        }

        if (_buildingsLayer is not null)
        {
            _buildingsLayer.Visible = false;
        }

        if (_itemsLayer is not null)
        {
            _itemsLayer.Visible = false;
        }

        if (_hud is not null)
        {
            _hud.Visible = false;
        }

        var beltDef = _slice.BeltDefinition;
        var cam = HasNode("Camera") ? GetNode<Camera2D>("Camera") : null;

        // 1) Corner block alone — crop one cell.
        await CaptureBeltLayout(
            destDir,
            "godot-port-corner-alone.png",
            [
                new GridPosition(9, 8),
                new GridPosition(10, 8),
                new GridPosition(10, 9)
            ],
            beltDef,
            cam,
            focusCellX: 10.5f,
            focusCellY: 8.5f,
            cropCells: 1);

        // 2) Corner with conveyor belts (L).
        await CaptureBeltLayout(
            destDir,
            "godot-port-corner-belts.png",
            [
                new GridPosition(7, 8),
                new GridPosition(8, 8),
                new GridPosition(9, 8),
                new GridPosition(10, 8),
                new GridPosition(10, 9),
                new GridPosition(10, 10),
                new GridPosition(10, 11)
            ],
            beltDef,
            cam,
            focusCellX: 9.5f,
            focusCellY: 9.0f,
            cropCells: 5);

        // 3) U shape (open west).
        await CaptureBeltLayout(
            destDir,
            "godot-port-corner-u.png",
            [
                new GridPosition(8, 8),
                new GridPosition(9, 8),
                new GridPosition(10, 8),
                new GridPosition(10, 9),
                new GridPosition(10, 10),
                new GridPosition(9, 10),
                new GridPosition(8, 10)
            ],
            beltDef,
            cam,
            focusCellX: 9.0f,
            focusCellY: 9.0f,
            cropCells: 5);

        // 4) Z shape.
        await CaptureBeltLayout(
            destDir,
            "godot-port-corner-z.png",
            [
                new GridPosition(7, 8),
                new GridPosition(8, 8),
                new GridPosition(9, 8),
                new GridPosition(9, 9),
                new GridPosition(9, 10),
                new GridPosition(10, 10),
                new GridPosition(11, 10),
                new GridPosition(12, 10)
            ],
            beltDef,
            cam,
            focusCellX: 9.5f,
            focusCellY: 9.0f,
            cropCells: 6);

        // Pixel asserts on the L/belts layout (reload + one more frame).
        await CaptureBeltLayout(
            destDir,
            "godot-port-corner-recipe-close.png",
            [
                new GridPosition(7, 8),
                new GridPosition(8, 8),
                new GridPosition(9, 8),
                new GridPosition(10, 8),
                new GridPosition(10, 9),
                new GridPosition(10, 10),
                new GridPosition(10, 11)
            ],
            beltDef,
            cam,
            focusCellX: 10.5f,
            focusCellY: 8.5f,
            cropCells: 5,
            runAsserts: true);

        GD.Print("Corner shot set complete (alone / belts / U / Z).");
    }

    private void ClearAllBelts()
    {
        if (_slice is null)
        {
            return;
        }

        foreach (var pos in _slice.Belts.Cells.Keys.ToList())
        {
            _slice.TryRemoveBelt(pos);
        }
    }

    private async Task CaptureBeltLayout(
        string destDir,
        string fileName,
        List<GridPosition> path,
        ConveyorDefinition beltDef,
        Camera2D? cam,
        float focusCellX,
        float focusCellY,
        int cropCells,
        bool runAsserts = false)
    {
        ClearAllBelts();
        _slice!.Belts.PlacePath(path, beltDef);
        RebuildBeltVisual();
        if (cam is not null)
        {
            cam.Position = new Vector2(focusCellX * TileSize, focusCellY * TileSize);
            cam.Zoom = new Vector2(1f, 1f);
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var img = GetViewport().GetTexture().GetImage();
        var vpW = img.GetWidth();
        var vpH = img.GetHeight();
        var size = TileSize * cropCells;
        var region = img.GetRegion(new Rect2I(
            (vpW - size) / 2, (vpH - size) / 2, size, size));
        var pathOut = Path.Combine(destDir, fileName);
        var err = region.SavePng(pathOut);
        GD.Print(err == Error.Ok ? $"Screenshot: {pathOut}" : $"Screenshot failed: {err}");

        if (runAsserts)
        {
            var cell = TileSize;
            var cornerX0 = (vpW / 2) - (cell / 2);
            var cornerY0 = (vpH / 2) - (cell / 2);
            AssertCellOpaque(img, cornerX0, cornerY0, cell, "corner(10,8)");
            AssertCellOpaque(img, cornerX0 - cell, cornerY0, cell, "straight(9,8)");
            AssertNorthEdgeFlush(img, cornerX0 - cell, cornerX0 + cell, cornerY0);
            AssertSeamRailContinuous(img, cornerX0, cornerY0, cell);
            AssertCornerRecipe(img, cornerX0, cornerY0, cell);
            img.SavePng(Path.Combine(destDir, "godot-port-corner-recipe-map.png"));
        }
    }

    private static bool IsTerrain(Color c) =>
        c.G > c.R + 5f / 255f && c.G >= c.B && c.B < 60f / 255f && c.R < 55f / 255f;

    private static bool IsRail(Color c) =>
        !IsTerrain(c) && c.R <= 30f / 255f && c.G <= 36f / 255f && c.B <= 42f / 255f;

    private static void AssertCellOpaque(Image img, int x0, int y0, int cell, string label)
    {
        var terrain = 0;
        for (var y = y0; y < y0 + cell; y++)
        {
            for (var x = x0; x < x0 + cell; x++)
            {
                if (x < 0 || y < 0 || x >= img.GetWidth() || y >= img.GetHeight())
                {
                    continue;
                }

                if (IsTerrain(img.GetPixel(x, y)))
                {
                    terrain++;
                }
            }
        }

        var total = cell * cell;
        GD.Print($"PixelCheck {label}: terrain={terrain}/{total} ({100.0 * terrain / total:F2}%)");
        if (terrain > 0)
        {
            GD.PushError($"FULLTILE FAIL {label}: terrain pixels inside cell (want 0).");
        }
    }

    private static void AssertNorthEdgeFlush(Image img, int x0, int x1, int yTop)
    {
        var beltOnEdge = 0;
        var span = 0;
        for (var x = x0; x < x1; x++)
        {
            if (x < 0 || x >= img.GetWidth() || yTop < 1 || yTop >= img.GetHeight())
            {
                continue;
            }

            span++;
            if (!IsTerrain(img.GetPixel(x, yTop)))
            {
                beltOnEdge++;
            }
        }

        GD.Print($"PixelCheck north-edge flush: beltOnEdge={beltOnEdge}/{span}");
        if (span > 0 && beltOnEdge < span * 0.95)
        {
            GD.PushError("FULLTILE FAIL north edge: belt does not sit on tile edge.");
        }
    }

    /// <summary>
    /// Top/bottom rail band thickness must match across the straight↔corner seam
    /// (no rientranza / notch where west rail used to thicken N/S borders).
    /// </summary>
    private static void AssertSeamRailContinuous(Image img, int cornerX0, int cornerY0, int cell)
    {
        static int TopRailEnd(Image image, int x, int y0)
        {
            var end = y0 - 1;
            for (var y = y0; y < y0 + 16; y++)
            {
                if (x < 0 || x >= image.GetWidth() || y < 0 || y >= image.GetHeight())
                {
                    break;
                }

                if (!IsRail(image.GetPixel(x, y)))
                {
                    break;
                }

                end = y;
            }

            return end;
        }

        static int BotRailStart(Image image, int x, int y0, int cellSize)
        {
            var start = y0 + cellSize;
            for (var y = y0 + cellSize - 1; y >= y0 + cellSize - 16; y--)
            {
                if (x < 0 || x >= image.GetWidth() || y < 0 || y >= image.GetHeight())
                {
                    break;
                }

                if (!IsRail(image.GetPixel(x, y)))
                {
                    break;
                }

                start = y;
            }

            return start;
        }

        var straightX = cornerX0 - 8;
        var seamX = cornerX0 + 2;
        var topStraight = TopRailEnd(img, straightX, cornerY0);
        var topSeam = TopRailEnd(img, seamX, cornerY0);
        var botStraight = BotRailStart(img, straightX, cornerY0, cell);
        var botSeam = BotRailStart(img, seamX, cornerY0, cell);
        var topDelta = Math.Abs(topSeam - topStraight);
        var botDelta = Math.Abs(botSeam - botStraight);
        GD.Print(
            $"PixelCheck seam rails: topStraightEnd={topStraight} topSeamEnd={topSeam} Δ={topDelta} " +
            $"botStraightStart={botStraight} botSeamStart={botSeam} Δ={botDelta}");
        if (topDelta > 1 || botDelta > 1)
        {
            GD.PushError("SEAM FAIL: N/S rail band steps across straight↔corner join (rientranza).");
        }

        // Tube (Blu4) meets the straight — colors differ by design; only log.
        var yBody = cornerY0 + cell / 4;
        var straightBody = img.GetPixel(cornerX0 - 8, yBody);
        var cornerTube = img.GetPixel(cornerX0 + cell / 4, yBody);
        GD.Print(
            $"PixelCheck seam tube: straight=({straightBody.R:F3},{straightBody.G:F3},{straightBody.B:F3}) " +
            $"cornerTube=({cornerTube.R:F3},{cornerTube.G:F3},{cornerTube.B:F3})");

        AssertInnerCornerKnuckle(img, cornerX0, cornerY0, cell);
        AssertEastRailFlush(img, cornerX0, cornerY0, cell);
    }

    /// <summary>
    /// Recipe: Blu1 N/E + SW knuckle, Blu4 tube near focus, Blu3 outside arc (NE).
    /// </summary>
    private static void AssertCornerRecipe(Image img, int cornerX0, int cornerY0, int cell)
    {
        static bool Near(Color c, float r, float g, float b, float tol = 0.05f) =>
            Math.Abs(c.R - r) <= tol && Math.Abs(c.G - g) <= tol && Math.Abs(c.B - b) <= tol;

        // Blu1 #191E24 on north edge mid.
        var n = img.GetPixel(cornerX0 + cell / 2, cornerY0 + 2);
        var nOk = IsRail(n) || Near(n, 0.098f, 0.118f, 0.141f);
        GD.Print($"PixelCheck recipe Blu1 N: ({n.R:F3},{n.G:F3},{n.B:F3}) ok={nOk}");
        if (!nOk)
        {
            GD.PushError("RECIPE FAIL: north edge is not Blu1.");
        }

        // Blu4 tube near focus (SW quadrant).
        var tube = img.GetPixel(cornerX0 + cell / 3, cornerY0 + cell * 2 / 3);
        var tubeOk = Near(tube, 0.525f, 0.655f, 0.722f, 0.08f);
        GD.Print($"PixelCheck recipe Blu4 tube: ({tube.R:F3},{tube.G:F3},{tube.B:F3}) ok={tubeOk}");
        if (!tubeOk)
        {
            GD.PushError("RECIPE FAIL: tube interior is not Blu4.");
        }

        // Blu3 exterior opposite focus (NE tip inside borders).
        var ext = img.GetPixel(cornerX0 + cell * 88 / 100, cornerY0 + cell * 12 / 100);
        var extOk = Near(ext, 0.196f, 0.235f, 0.275f, 0.08f);
        GD.Print($"PixelCheck recipe Blu3 exterior: ({ext.R:F3},{ext.G:F3},{ext.B:F3}) ok={extOk}");
        if (!extOk)
        {
            GD.PushError("RECIPE FAIL: NE exterior of arc is not Blu3.");
        }
    }

    /// <summary>
    /// SW ┘ knuckle must be solid rail; bottom edge beside it must NOT extend a
    /// groove/rail stub (that read as the inner-corner notch).
    /// </summary>
    private static void AssertInnerCornerKnuckle(Image img, int cornerX0, int cornerY0, int cell)
    {
        var railBand = Math.Max(3, cell * 8 / 100);
        var knuckleRails = 0;
        var knuckleCells = 0;
        for (var y = cornerY0 + cell - railBand; y < cornerY0 + cell; y++)
        {
            for (var x = cornerX0; x < cornerX0 + railBand; x++)
            {
                knuckleCells++;
                if (IsRail(img.GetPixel(x, y)))
                {
                    knuckleRails++;
                }
            }
        }

        GD.Print($"PixelCheck SW knuckle: rail={knuckleRails}/{knuckleCells}");
        if (knuckleCells > 0 && knuckleRails < knuckleCells * 0.9)
        {
            GD.PushError("CORNER FAIL: SW knuckle is not solid rail (inner notch).");
        }

        // Immediately east of knuckle on the bottom edge: body, not rail/groove stub.
        var stubRails = 0;
        var stubSamples = 0;
        var yBot = cornerY0 + cell - 1;
        for (var x = cornerX0 + railBand; x < cornerX0 + railBand + railBand && x < cornerX0 + cell - railBand; x++)
        {
            stubSamples++;
            if (IsRail(img.GetPixel(x, yBot)))
            {
                stubRails++;
            }
        }

        GD.Print($"PixelCheck SW bottom-beside-knuckle: rail={stubRails}/{stubSamples} (want 0)");
        if (stubRails > 0)
        {
            GD.PushError("CORNER FAIL: south-rail stub east of SW knuckle (inner step).");
        }
    }

    /// <summary>
    /// East outer rail X must stay flush from corner into the vertical strip
    /// (no SE bump / width jump at the join).
    /// </summary>
    private static void AssertEastRailFlush(Image img, int cornerX0, int cornerY0, int cell)
    {
        static int EastRailStart(Image image, int y, int xRight)
        {
            for (var x = xRight; x >= xRight - 16; x--)
            {
                if (x < 0 || y < 0 || x >= image.GetWidth() || y >= image.GetHeight())
                {
                    break;
                }

                if (!IsRail(image.GetPixel(x, y)))
                {
                    return x + 1;
                }
            }

            return xRight - 16;
        }

        var xRight = cornerX0 + cell - 1;
        var midCorner = EastRailStart(img, cornerY0 + cell / 2, xRight);
        var botCorner = EastRailStart(img, cornerY0 + cell - 2, xRight);
        var topVert = EastRailStart(img, cornerY0 + cell + 4, xRight);
        var midVert = EastRailStart(img, cornerY0 + cell + cell / 2, xRight);
        var d1 = Math.Abs(botCorner - midCorner);
        var d2 = Math.Abs(topVert - botCorner);
        var d3 = Math.Abs(midVert - midCorner);
        GD.Print(
            $"PixelCheck east rail: midCorner={midCorner} botCorner={botCorner} " +
            $"topVert={topVert} midVert={midVert} Δ=({d1},{d2},{d3})");
        if (d1 > 1 || d2 > 1 || d3 > 1)
        {
            GD.PushError("CORNER FAIL: east rail X jumps at corner→vertical (SE bump).");
        }

        // SE bottom interior (left of east rail) must not be a south-rail stub.
        var seStub = 0;
        var ySe = cornerY0 + cell - 1;
        for (var x = cornerX0 + cell / 2; x < cornerX0 + cell - 8; x++)
        {
            if (IsRail(img.GetPixel(x, ySe)))
            {
                seStub++;
            }
        }

        GD.Print($"PixelCheck SE south stub: rail={seStub} (want 0)");
        if (seStub > 0)
        {
            GD.PushError("CORNER FAIL: SE south-rail stub present (outer bump).");
        }
    }
}
