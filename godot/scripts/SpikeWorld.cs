using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Playable factory slice + FactoryHud + Ricerca. Default = cursore.
/// Hotkeys 1–9 place · Esc cursore · T ricerca · M mercato · I impostazioni · R ruota · C filtro · RMB elimina.
/// </summary>
public partial class SpikeWorld : Node2D
{
    public const int TileSize = 64;
    public const int DefaultMapWidth = 1000;
    public const int DefaultMapHeight = 1000;

    /// <summary>Active playfield size from terrain (campaign/sandbox); falls back to spike defaults.</summary>
    public int ActiveMapWidth => _slice?.Terrain?.Width ?? DefaultMapWidth;
    public int ActiveMapHeight => _slice?.Terrain?.Height ?? DefaultMapHeight;

    private static readonly string[] SorterFilterIds =
    [
        "iron-ore", "copper-ore", "iron-plate", "copper-wire", "coal"
    ];

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
    private SettingsPanel? _settingsPanel;
    private TutorialPanel? _tutorial;
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
    private bool _draggingPlace;
    private bool _draggingRemove;
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
        _slice = FactorySlice.CreateSandboxSlice(
            content,
            FactorySlice.CenteredCoreOrigin(DefaultMapWidth, DefaultMapHeight),
            coreSize: 2,
            mapWidth: DefaultMapWidth,
            mapHeight: DefaultMapHeight,
            seed: 42,
            startingMoney: 180);

        _itemsLayer = GetNode<Node2D>("Items");
        EnsureFactoryHud();
        EnsureResearchPanel();
        EnsureMercatoPanel();
        EnsureCampaignSelectPanel();
        EnsureSettingsPanel();
        EnsureTutorialPanel();
        EnsureSavesPanel();
        EnsureHomePanel();
        EnsureFpsOverlay();
        EnsureResourceOverlay();
        EnsureBuildingsLayer();
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

        if (OS.GetEnvironment("TINDUSTRY_SEED_CORE_LINE") == "1" && _slice is not null)
        {
            SeedCoreDeliveryLine();
        }

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
            SyncCameraMapBounds();
            FocusCameraOnCore();
        }
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

}
