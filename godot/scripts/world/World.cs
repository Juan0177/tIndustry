using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Playable world (factory view) + Hud + Ricerca. Default = cursore.
/// Hotkeys 1–9 place · Esc cursore · T ricerca · M mercato · I impostazioni · R ruota · C filtro · RMB elimina.
/// </summary>
public partial class World : Node2D
{
    public const int TileSize = 64;
    public const int DefaultMapWidth = 1000;
    public const int DefaultMapHeight = 1000;

    /// <summary>Active playfield size from terrain (campaign/sandbox); falls back to default size.</summary>
    public int ActiveMapWidth => _view?.Terrain?.Width ?? DefaultMapWidth;
    public int ActiveMapHeight => _view?.Terrain?.Height ?? DefaultMapHeight;

    private enum BuildTool
    {
        Cursor,
        Belt,
        BeltFast,
        BeltExpress,
        Miner,
        MinerAdvanced,
        Smelter,
        Assembler,
        Junction,
        Splitter,
        Generator,
        Sorter,
        Bridge,
        Extractor,
        PowerNode,
        PowerNodeT2
    }

    // The simulation lives behind the host: the client reads IGameView and submits commands.
    // A fresh host per game (new / campaign level / load) restarts the fixed-tick clock at tick 0.
    private SimHost? _host;
    private IGameView? _view => _host?.State;
    // The startup sandbox behind Home is not a played game: it must not overwrite Continua.
    private readonly ContinueAutoSave _continueAutoSave = new();
    private MindustryBeltVisual? _beltVisual;
    private readonly Dictionary<long, Sprite2D> _itemSprites = [];
    private Node2D? _itemsLayer;
    private Node2D? _buildingsLayer;
    private ConstructionLayer? _constructionLayer;
    private Hud? _hud;
    private HomePanel? _home;
    private ResearchPanel? _research;
    private MercatoPanel? _mercato;
    private CampaignSelectPanel? _campaignSelect;
    private SettingsPanel? _settingsPanel;
    private TutorialPanel? _tutorial;
    private (Vector2 Position, float Zoom)? _tutorialCamAnchor;
    private SavesPanel? _saves;
    private ClientSettings _settings = new();
    private Label? _fpsLabel;
    private Label? _resourceLabel;
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
    private Vector2 _hoverScreen;
    private bool _draggingPlace;
    private bool _draggingRemove;
    private GridPosition? _lastRemoveCell;
    private bool _visualDirty = true;
    private bool _buildingsDirty = true;
    private Texture2D? _oreTex;
    private Texture2D? _plateTex;
    private Texture2D? _copperOreTex;
    private Texture2D? _copperWireTex;
    private Texture2D? _coalTex;
    private Texture2D? _leadOreTex;
    private Texture2D? _titaniumOreTex;
    private Texture2D? _coreTex;
    private bool _returnHomeAfterCampaign;

    public override void _Ready()
    {
        _contentPath = ResolveContentPath();
        _campaignPath = ResolveCampaignPath();
        _settings = ClientSettings.Load();
        _settings.ApplyToEngine(GetTree().Root);
        var content = FactoryContent.Load(_contentPath);
        _campaign = CampaignCatalog.Load(_campaignPath);
        _progressPath = CampaignProgress.ProgressPath;
        _progress = CampaignProgress.Load(_progressPath);
        AttachGame(NewGame.Sandbox(
            content,
            Factory.CenteredCoreOrigin(DefaultMapWidth, DefaultMapHeight),
            coreSize: 2,
            mapWidth: DefaultMapWidth,
            mapHeight: DefaultMapHeight,
            seed: 42,
            startingMoney: 180,
            rules: ClientRules()));

        _itemsLayer = GetNode<Node2D>("Items");
        EnsureHud();
        EnsureResearchPanel();
        EnsureMercatoPanel();
        EnsureCampaignSelectPanel();
        EnsureSettingsPanel();
        EnsureTutorialPanel();
        EnsureSavesPanel();
        EnsureHomePanel();
        EnsureFpsOverlay();
        EnsureResourceOverlay();
        UseSmoothUiFiltering();
        EnsureBuildingsLayer();
        EnsureConstructionLayer();
        _oreTex = GD.Load<Texture2D>("res://assets/iron-ore.png");
        _plateTex = GD.Load<Texture2D>("res://assets/iron-plate.png");
        _copperOreTex = GD.Load<Texture2D>("res://assets/copper-ore.png");
        _copperWireTex = GD.Load<Texture2D>("res://assets/copper-wire.png");
        _coalTex = GD.Load<Texture2D>("res://assets/coal.png");
        _leadOreTex = GD.Load<Texture2D>("res://assets/lead-ore.png");
        _titaniumOreTex = GD.Load<Texture2D>("res://assets/titanium-ore.png");
        _coreTex = GD.Load<Texture2D>("res://assets/core.png");

        EnsureBeltVisual();
        RebuildBeltVisual();
        RebuildBuildingVisuals();
        SyncResearchLocks();
        UpdateHud();

        if (OS.GetEnvironment("TINDUSTRY_SEED_CORE_LINE") == "1" && _view is not null)
        {
            SeedCoreDeliveryLine();
        }

        // Fresh: skip Home. Otherwise show splash.
        if (OS.GetEnvironment("TINDUSTRY_FRESH") == "1" && !PlaytestRequested)
        {
            _home?.Close();
        }
        else
        {
            _home?.Open();
        }

        if (HasNode("Camera"))
        {
            SyncCameraMapBounds();
            FocusCameraOnCore();
        }

        StartPlaytestIfRequested();
    }

    public override void _Process(double delta)
    {
        if (_fpsLabel is not null && _settings.ShowFps && !_settings.ShowResourceOverlay)
        {
            _fpsLabel.Text = $"FPS {Engine.GetFramesPerSecond()}";
        }

        if (_settings.ShowResourceOverlay)
        {
            SystemMonitor.Update((float)delta);
            if (_resourceLabel is not null)
            {
                var fps = Engine.GetFramesPerSecond();
                var procMs = Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0;
                var gpu = SystemMonitor.GpuLabel;
                var lines = $"FPS {fps}\nCPU {SystemMonitor.CpuPercent:0.0}%\nRAM {SystemMonitor.FormatRam()}\nProc {procMs:0.0} ms";
                if (!string.IsNullOrWhiteSpace(gpu))
                {
                    lines += $"\n{gpu}";
                }

                _resourceLabel.Text = lines;
            }
        }

        SyncTutorial();
        if (GetNodeOrNull<WorldCamera>("Camera") is { } cam)
        {
            cam.LeftDragPans = _tool == BuildTool.Cursor && _home?.IsOpen != true;
        }

        if (_home?.IsOpen == true || _view is null)
        {
            _hud?.SetSiteTooltip(null, default);
            return;
        }

        AdvanceHost(delta);
        SyncConstructionVisuals((float)delta);

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

        _structureCount = StructureCount();
        SyncItemSprites();
        SyncSiteTooltip();
        SyncBuildingVisuals((float)delta);
        UpdateHud();
        CheckCampaignComplete();
        QueueRedraw();
    }

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

    /// <summary>
    /// The project default is Nearest (crisp pixel-art blocks on the map). UI is scaled (UI scale 125%,
    /// 64px icons drawn at 16–26px), where Nearest drops pixels: grainy text and icons. Smooth filtering
    /// for everything on the HUD layer only; children inherit it.
    /// </summary>
    private void UseSmoothUiFiltering()
    {
        var layer = GetNode<CanvasLayer>("Hud");
        foreach (var child in layer.GetChildren())
        {
            if (child is CanvasItem item)
            {
                item.TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps;
            }
        }

        layer.ChildEnteredTree += node =>
        {
            if (node.GetParent() == layer && node is CanvasItem item)
            {
                item.TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps;
            }
        };
    }
}
