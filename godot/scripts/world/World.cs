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

    /// <summary>Screen px per second the map behind the Home scrolls.</summary>
    private const float HomeDriftSpeed = 24f;

    /// <summary>True while Impostazioni opened from the Home are open (closing them goes back to the Home).</summary>
    private bool _settingsFromHome;

    /// <summary>
    /// The map is the Home's background: the Home, or a panel opened from it (Nuova partita, Gestione salvataggi, which
    /// both go back to the Home, or Impostazioni opened there), is open.
    /// </summary>
    private bool HomeBackdrop => _home?.IsOpen == true || _newGame?.IsOpen == true || _saves?.IsOpen == true
        || (_settingsFromHome && _settingsPanel?.IsOpen == true);

    /// <summary>Drift speed forced by a playtest step (null: still in playtests).</summary>
    private float? _playtestDriftSpeed;

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
        PowerNodeT2,
        Lamp
    }

    // The simulation lives behind the host: the client reads IGameView and submits commands.
    // A fresh host per game (new / campaign level / load) restarts the fixed-tick clock at tick 0.
    private SimHost? _host;
    private IGameView? _view => _host?.State;
    // The startup sandbox behind Home is not a played game: it must not overwrite Continua.
    private readonly ContinueAutoSave _continueAutoSave = new();
    private MindustryBeltVisual? _beltVisual;
    private readonly Dictionary<long, Sprite2D> _itemSprites = [];

    /// <summary>Where each item sprite was at the tick before the last one and at the last one (drawn in between).</summary>
    private readonly Dictionary<long, ItemMotion> _itemMotion = [];

    /// <summary>Tick the item sprites were last synced at; -1 = none yet (new game, load).</summary>
    private long _itemsTick = -1;

    /// <summary>Host the item motions belong to: a different one (load, new game) starts every item over.</summary>
    private SimHost? _itemsHost;

    private Node2D? _itemsLayer;
    private TerrainChunks? _terrainChunks;
    private Node2D? _buildingsLayer;
    private ConstructionLayer? _constructionLayer;
    private EffectManager? _effects;
    private AmbientEffects? _ambient;
    private RenderScale? _renderScale;
    private NightLayer? _night;
    private WorldEnvironment? _glowNode;
    private global::Godot.Environment? _glowEnv;
    // Graphics setting "Animazioni dei blocchi": false = building visuals get no time (still frames).
    private bool _blockAnimations = true;
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
        // Earliest point the settings can be read (after the playtest profile guard; World is the main scene and
        // there is no autoload): the window mode is applied before the first frame is drawn, so the 1280×720 window
        // of project.godot shows at most for the instant before fullscreen.
        _settings = ClientSettings.Load();
        ApplyPlaytestUiScale();
        ApplyPlaytestLanguage();
        _settings.ApplyToEngine(GetTree().Root);
        // One theme for the whole client UI (G4): the window (popups, tooltips) and every panel on the HUD root.
        UiTheme.ApplyTo(GetTree().Root, UiRoot);
        // UI-only scale: the HUD layer and its popups (tooltips); the world keeps its size.
        ApplyUiScale();
        GetTree().NodeAdded += UiScale.ScalePopup;
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
        EnsurePlacementOverlay();
        EnsureBuildingsLayer();
        EnsureConstructionLayer();
        EnsureEffects();
        ApplyGraphicsSettings();

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
            _home?.Open(inGame: false);
        }

        if (HasNode("Camera"))
        {
            SyncCameraMapBounds();
            FocusCameraOnCore();
        }

        // Agents' control channel: dev builds with port, secret and an isolated profile only (never in an exported game,
        // never on the player's real data).
        if (AgentBridge.ShouldStart(OS.IsDebugBuild(), OS.GetEnvironment(AgentBridge.PortEnv),
                OS.GetEnvironment(AgentBridge.TokenEnv), OS.GetEnvironment(GameDataPaths.DataDirEnv), AgentBridge.RealDataDirs()))
        {
            AddChild(new AgentBridge { Name = "AgentBridge" });
        }

        StartPlaytestIfRequested();
    }

    /// <summary>The running game's host, for <see cref="AgentBridge"/> (it changes on new game, level or load).</summary>
    internal SimHost? AgentHost => _host;

    internal CampaignCatalog? AgentCampaign => _campaign;

    /// <summary>The player's placement direction (HUD, R), which the agents' <c>stato</c>, <c>ruota</c> and <c>piazza</c> use.</summary>
    internal Direction AgentDirection => _placeDir;

    /// <summary>The agents' <c>ruota</c>: the player's R (direction label and toast included).</summary>
    internal void AgentRotate() => OnHudRotate();

    /// <summary>A game is being played: not behind Home or Nuova partita. <see cref="_Process"/> runs the world only then.</summary>
    internal bool GameInPlay => _home?.IsOpen != true && _newGame?.IsOpen != true && _view is not null;

    /// <summary>The single guard of <see cref="_Process"/> for advancing the simulation (also read by the agents' bridge).</summary>
    internal bool SimAdvances => GameInPlay && !PlaytestHeld;

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
        SyncNight();
        SyncAmbientEffects(delta);
        SyncTutorial();
        if (GetNodeOrNull<WorldCamera>("Camera") is { } cam)
        {
            // Behind the Home and the panels opened from it the map scrolls slowly on its own and the player cannot
            // move it; still in playtests (stable captures).
            cam.LeftDragPans = _tool == BuildTool.Cursor && !HomeBackdrop && _newGame?.IsOpen != true;
            cam.InputEnabled = !HomeBackdrop;
            cam.DriftSpeed = !HomeBackdrop ? 0f : _playtestDriftSpeed ?? (PlaytestRequested ? 0f : HomeDriftSpeed);
        }

        if (!GameInPlay)
        {
            _hud?.SetSiteTooltip(null, default);
            _hud?.SetPlacementHint(null);
            return;
        }

        if (SimAdvances)
        {
            AdvanceHost(delta);
        }

        var visualDelta = PlaytestHeld ? 0f : (float)delta;
        SyncConstructionVisuals(visualDelta);

        // Buildings next to a belt decide its corners (Mindustry blending), so they redraw the belts too.
        if (_visualDirty || _buildingsDirty)
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
        SyncBuildingVisuals(visualDelta);
        UpdateHud();
        CheckCampaignComplete();
        QueueRedraw();
        _placementOverlay?.QueueRedraw();
    }

    /// <summary>
    /// Leaving the tree (any quit: Home "Esci", window close, end of a playtest): unhook the playtest guard. The UI
    /// leaves no Godot wrapper for the GC to finalize during engine teardown (which used to crash it: "Leaked unsafe
    /// reference"): styles, fonts and icons live in the static <see cref="UiTheme"/> and <see cref="Sprites"/> caches.
    /// </summary>
    public override void _ExitTree()
    {
        GameDataPaths.WriteRefused -= OnPlayerDataWriteRefused;
        GetTree().NodeAdded -= UiScale.ScalePopup;
        UiScale.Detach();
    }

    private void EnsureEffects()
    {
        _effects = new EffectManager { Name = "Effetti", TileSize = TileSize };
        AddChild(_effects);
        // Either may be made first: each one hands the effects to the construction layer.
        if (_constructionLayer is not null)
        {
            _constructionLayer.Effects = _effects;
        }

        _ambient = new AmbientEffects();
        _renderScale = new RenderScale { Name = "ScalaRendering" };
        AddChild(_renderScale);
        _renderScale.Setup(this);
        _night = new NightLayer { Name = "Notte" };
        AddChild(_night);
        _glowEnv = MakeGlowEnvironment();
        _glowNode = new WorldEnvironment { Name = "Bagliore" };
        AddChild(_glowNode);
    }

    /// <summary>
    /// Glow of the world (G3 "Bagliore"): canvas background, so the canvas layers up to 0 (the world, and the reduced
    /// world image of <see cref="RenderScale"/> at -10) get the glow and the HUD (layer 20) never does. With HDR 2D only
    /// colours above 1 bloom (glowing parts boosted by <see cref="BuildingPad.GlowBoost"/>, lights at night, HDR
    /// particles), so light ground such as snow is not washed out.
    /// </summary>
    private static global::Godot.Environment MakeGlowEnvironment()
    {
        var env = new global::Godot.Environment
        {
            BackgroundMode = global::Godot.Environment.BGMode.Canvas,
            BackgroundCanvasMaxLayer = 0,
            TonemapMode = global::Godot.Environment.ToneMapper.Linear,
            GlowEnabled = true,
            GlowNormalized = false,
            GlowIntensity = 0.9f,
            GlowStrength = 1f,
            GlowBloom = 0f,
            GlowHdrThreshold = 1f,
            GlowHdrScale = 2f,
            GlowBlendMode = global::Godot.Environment.GlowBlendModeEnum.Additive
        };
        // Wide, soft levels: a 64 px block's glow spreads about half a block around it.
        for (var level = 0; level < 7; level++)
        {
            env.SetGlowLevel(level, level is 2 or 3 or 4 ? 1f : 0f);
        }

        return env;
    }

    /// <summary>
    /// "Bagliore" on or off: HDR 2D on the viewport that draws the world (the root, or the reduced one of
    /// <see cref="RenderScale"/>, which gets a World3D of its own so the glow is applied once). Off = no environment
    /// and no HDR at all, the world drawn exactly as before the G3: the reduced viewport's World3D is only created when
    /// the glow is on (an existing one just loses its environment).
    /// </summary>
    private void ApplyGlow(bool on)
    {
        if (_glowNode is null || _glowEnv is null)
        {
            return;
        }

        var root = GetTree().Root;
        var target = _renderScale?.Target;
        root.UseHdr2D = on;
        _glowNode.Environment = on && target is null ? _glowEnv : null;
        if (target is not null)
        {
            target.UseHdr2D = on;
            if (on)
            {
                target.World3D ??= new World3D();
                target.World3D.Environment = _glowEnv;
            }
            else if (target.World3D is { } world)
            {
                world.Environment = null;
            }
        }
    }

    /// <summary>True when the glow is drawn (playtest checks).</summary>
    private bool GlowActive => _glowEnv is not null
        && (_renderScale?.Target is { } target ? target.World3D?.Environment == _glowEnv : _glowNode?.Environment == _glowEnv);

    /// <summary>
    /// Every frame: the night of the running game (not behind Home, never in a playtest without effects) with the lights
    /// of the sources in view; the ambient effects follow it (fireflies at night).
    /// </summary>
    private void SyncNight()
    {
        if (_night is null)
        {
            return;
        }

        var night = _view?.Rules.Night == true && _home?.IsOpen != true && !ClientSettings.PlaytestForcesEffectsOff;
        if (night != _night.Night)
        {
            _night.SetNight(night);
        }

        if (_ambient is not null)
        {
            _ambient.Night = night;
        }

        if (!night)
        {
            return;
        }

        GetVisibleTileRange(out var minX, out var minY, out var maxX, out var maxY);
        var view = new Rect2(minX * TileSize, minY * TileSize, (maxX - minX + 1) * TileSize, (maxY - minY + 1) * TileSize);
        _night.Sync(_view, view, _constructionLayer?.DronePositions ?? [], TileSize);
    }

    /// <summary>
    /// Graphics settings that act on world nodes (the engine ones are in <see cref="ClientSettings.ApplyToEngine"/>):
    /// effects quality (Spenti in playtests unless TINDUSTRY_PLAYTEST_EFFETTI=1), still water with Effetti Spenti,
    /// render scale, glow, shadows (a change rebuilds the building visuals), building animations. Called at start and on
    /// every Applica, so changes act at once.
    /// </summary>
    private void ApplyGraphicsSettings()
    {
        var quality = _settings.EffectiveEffects;
        if (_effects is not null)
        {
            _effects.Quality = quality;
        }

        if (_terrainChunks is not null)
        {
            _terrainChunks.AnimateWater = quality != EffectsQuality.Spenti;
        }

        _renderScale?.Apply(_settings.ScalaRendering);
        _blockAnimations = _settings.AnimazioniBlocchi;
        var glow = _settings.EffectiveGlow;
        ApplyGlow(glow);
        var boost = glow ? GlowBoostOn : 1f;
        if (BuildingPad.Shadows != _settings.EffectiveShadows || !Mathf.IsEqualApprox(BuildingPad.GlowBoost, boost))
        {
            BuildingPad.Shadows = _settings.EffectiveShadows;
            BuildingPad.GlowBoost = boost;
            _buildingsDirty = true;
        }
    }

    /// <summary>Brightness of the glowing block parts with "Bagliore" on (above 1: they bloom).</summary>
    private const float GlowBoostOn = 1.5f;

    private void EnsureTerrainChunks()
    {
        _terrainChunks = new TerrainChunks { Name = "Terrain" };
        AddChild(_terrainChunks);
        MoveChild(_terrainChunks, 0);
    }

    /// <summary>
    /// Every frame, also behind Home: follows the attached game's terrain (a new map drops the old chunks) and
    /// builds the chunks in view, with their art when the cells are big enough on screen.
    /// </summary>
    private void SyncTerrainChunks()
    {
        if (_terrainChunks is null)
        {
            return;
        }

        _terrainChunks.SetMap(_view?.Terrain, _view?.Content.Terrain ?? TerrainTable.Empty, TileSize);
        GetVisibleTileRange(out var minX, out var minY, out var maxX, out var maxY);
        var zoom = GetNodeOrNull<Camera2D>("Camera") is { } cam ? cam.Zoom.X : 1f;
        _terrainChunks.Sync(minX, minY, maxX, maxY, TileSize * zoom);
    }

    /// <summary>Ambient particles of the biomes in view (also behind Home, over its background map).</summary>
    private void SyncAmbientEffects(double delta)
    {
        if (_ambient is null || _effects is null)
        {
            return;
        }

        GetVisibleTileRange(out var minX, out var minY, out var maxX, out var maxY);
        _ambient.Sync(_effects, _view?.Terrain, _view?.Content.Terrain, new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1), TileSize, delta);
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
}
