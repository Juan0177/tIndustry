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
    public const int DefaultMapWidth = (int)MapSize.Media;
    public const int DefaultMapHeight = (int)MapSize.Media;

    /// <summary>Edge of the biome map behind Home at startup (seed 42).</summary>
    private const int StartupBackgroundEdge = 256;

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
    private TerrainChunks? _terrainChunks;
    private Node2D? _buildingsLayer;
    private ConstructionLayer? _constructionLayer;
    private Hud? _hud;
    private HomePanel? _home;
    private NewGamePanel? _newGame;
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
    // Cells a right-drag already covered: a block gets one Remove per drag.
    private readonly HashSet<GridPosition> _removedThisDrag = [];
    // Last cell a left or right drag reached: the next one fills the cells in between.
    private GridPosition _dragCell;
    // Where the right button went down, and whether the drag has left that cell: a click without moving
    // toggles the mark there on release, a real drag only marks.
    private GridPosition _removeStart;
    private bool _removeMoved;
    private bool _visualDirty = true;
    private bool _buildingsDirty = true;
    private bool _returnHomeAfterCampaign;

    public override void _Ready()
    {
        if (!GuardPlaytestProfile())
        {
            return;
        }

        // Closing the window goes through QuitGame (Continua autosave), like Home "Esci".
        GetTree().AutoAcceptQuit = false;
        _contentPath = ResolveContentPath();
        _campaignPath = ResolveCampaignPath();
        _settings = ClientSettings.Load();
        _settings.ApplyToEngine(GetTree().Root);
        var content = FactoryContent.Load(_contentPath);
        _campaign = CampaignCatalog.Load(_campaignPath);
        _progressPath = CampaignProgress.ProgressPath;
        _progress = CampaignProgress.Load(_progressPath);
        // Startup sandbox behind Home (not a played game): a small biome map, quick to make and usually thrown
        // away by Continua or Nuova partita. TINDUSTRY_FRESH skips Home and plays it, so there it is Media.
        var startupSize = OS.GetEnvironment("TINDUSTRY_FRESH") == "1" && !PlaytestRequested
            ? MapSize.Media
            : (MapSize)StartupBackgroundEdge;
        AttachGame(NewSandboxFactory(content, startupSize, PlaytestSeed));

        _itemsLayer = GetNode<Node2D>("Items");
        EnsureTerrainChunks();
        EnsureHud();
        EnsureResearchPanel();
        EnsureMercatoPanel();
        EnsureCampaignSelectPanel();
        EnsureSettingsPanel();
        EnsureTutorialPanel();
        EnsureSavesPanel();
        EnsureHomePanel();
        EnsureNewGamePanel();
        EnsureMapOverlay();
        EnsureFpsOverlay();
        EnsureResourceOverlay();
        UseSmoothUiFiltering();
        EnsureBuildingsLayer();
        EnsureConstructionLayer();

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

        SyncTerrainChunks();
        SyncTutorial();
        if (GetNodeOrNull<WorldCamera>("Camera") is { } cam)
        {
            cam.LeftDragPans = _tool == BuildTool.Cursor && _home?.IsOpen != true && _newGame?.IsOpen != true;
        }

        if (_home?.IsOpen == true || _newGame?.IsOpen == true || _view is null)
        {
            _hud?.SetSiteTooltip(null, default);
            _hud?.SetPlacementHint(null);
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
        SyncHover();
        SyncSiteTooltip();
        SyncBuildingVisuals((float)delta);
        UpdateHud();
        CheckCampaignComplete();
        QueueRedraw();
    }

    /// <summary>
    /// Leaving the tree (any quit: Home "Esci", window close, end of a playtest): unhook the playtest guard and run
    /// the pending .NET finalizers while the engine is still whole. Godot wrappers already unreachable but not yet
    /// finalized (UI <c>StyleBoxFlat</c>s and <c>CompressedTexture2D</c>s created in C#) would otherwise be finalized
    /// by the GC during engine teardown, which crashes it ("Leaked unsafe reference", "!rc_owner"); bigger maps mean
    /// more GC pressure and made it likely.
    /// </summary>
    public override void _ExitTree()
    {
        GameDataPaths.WriteRefused -= OnPlayerDataWriteRefused;
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private void EnsureTerrainChunks()
    {
        _terrainChunks = new TerrainChunks { Name = "Terrain" };
        AddChild(_terrainChunks);
        MoveChild(_terrainChunks, 0);
    }

    /// <summary>
    /// Every frame, also behind Home: follows the attached game's terrain (a new map drops the old chunks) and
    /// builds the chunks in view.
    /// </summary>
    private void SyncTerrainChunks()
    {
        if (_terrainChunks is null)
        {
            return;
        }

        _terrainChunks.SetMap(_view?.Terrain, _view?.Content.Terrain ?? TerrainTable.Empty, TileSize);
        GetVisibleTileRange(out var minX, out var minY, out var maxX, out var maxY);
        _terrainChunks.Sync(minX, minY, maxX, maxY);
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
    /// Folder of the campaign map files (<c>data/maps/</c>, see <see cref="CampaignMapFile"/>), searched like
    /// content.json; null when missing. Campaign levels start from their map file here, and saves on a campaign map
    /// reload it.
    /// </summary>
    internal static string? ResolveMapsPath()
    {
        var candidates = new[]
        {
            ProjectSettings.GlobalizePath("res://../data/maps"),
            ProjectSettings.GlobalizePath("res://data/maps"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "data", "maps")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "data", "maps")),
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "data", "maps")),
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    /// <summary>
    /// The project default is Nearest (crisp pixel-art blocks on the map). UI is scaled (UI scale 125%,
    /// 32 px icons drawn at 16–28 px), where Nearest drops pixels: grainy text and icons. Smooth filtering
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
