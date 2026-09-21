using System.Globalization;
using System.Numerics;
using System.Reflection;
using Raylib_cs;
using TIndustry.Logistics;

internal enum AppScreen
{
    Splash,
    Home,
    Playing,
    SaveManager,
    Research,
    Settings,
    NewGame,
    CampaignSelect,
    Loading
}

internal static class FactoryGameApp
{
    private static int ScreenWidth = 1240;
    private static int ScreenHeight = 760;
    public const int MapWidth = 1000;
    public const int MapHeight = 1000;
    private const int BaseTileSize = 36;
    private const int HeaderHeightBase = 64;
    private const int InfoPanelWidthBase = 312;
    private const int MercatoRowHeightBase = 40;
    private const int MercatoPadBase = 12;
    private const int MercatoSellOneWBase = 28;
    private const int MercatoSellAllWBase = 52;
    private const int MercatoSellGapBase = 5;
    private const int StatusPanelHeightBase = 148;
    /// <summary>Minimum Fabbrica content: title + PWR + counts + CORE button.</summary>
    private const int StatusPanelMinHeightBase = 100;
    private const int HeaderIconSizeBase = 40;
    private const int HeaderIconGapBase = 8;
    private const float EntryAnimDuration = 0.85f;
    private const float LoadingMinSeconds = 0.55f;
    private const float SplashMinSeconds = 1.2f;
    private const float StatusToastSeconds = 3.5f;
    private const float SplashMaxSeconds = 8f;
    private const int ViewportLeft = 0;
    private static int HeaderHeight => UiTheme.S(HeaderHeightBase);
    private static int InfoPanelWidth => UiTheme.S(InfoPanelWidthBase);
    private static int HeaderIconSize => UiTheme.S(HeaderIconSizeBase);
    private static int HeaderIconGap => UiTheme.S(HeaderIconGapBase);
    private static int ViewportTop => HeaderHeight;
    private static int ViewportRight => ScreenWidth;
    private static int ViewportBottom => ScreenHeight;
    private const float FixedStep = 1f / 30f;
    public const int DefaultSeed = 7429;
    private const int SeedEspanso = 1337;
    private const int SeedArcipelago = 9001;
    private static UiTheme.BuildCategory DockCategory = UiTheme.BuildCategory.Logistics;
    private static string? DockSelectedId = "conveyor-basic";
    /// <summary>Filter item applied when placing a new sorter (F cycles; default ferro grezzo).</summary>
    private static string SorterBrushFilterId = "iron-ore";
    private static readonly string[] SorterFilterItemIds =
    [
        "iron-ore",
        "copper-ore",
        "iron-plate",
        "copper-wire"
    ];
    private static GameSettings? SettingsDraft;
    private static float SettingsScrollY;
    private static float EntryAnimT = 1f;
    private static float LoadingElapsed;
    private static int LoadingSeed = DefaultSeed;
    private static bool LoadingFromSave;
    private static string? LoadingSlotId;
    private static string LoadingLabel = "Caricamento…";
    private static bool LoadingWorldReady;
    private static float SplashElapsed;
    private static Texture2D SplashThumbnail;
    private static bool SplashThumbnailLoaded;

    // First-run tutorial (Italian); skipped/completed persists in settings.json.
    private static bool TutorialActive;
    private static int TutorialStep;
    private static float TutorialCameraMoved;
    private static bool TutorialPlacedMiner;
    private static bool TutorialPlacedBelt;
    private static bool TutorialSoldOre;
    private static bool TutorialUsedEconomy;
    private static bool TutorialOpenedResearch;
    private static bool TutorialOpenedDock;
    private static bool TutorialSwitchedDockCategory;
    private static bool TutorialUsedRemove;
    private static bool TutorialOpenedSettings;
    private static bool TutorialRotatedPiece;
    private static int TutorialSoldBaseline = -1;
    private static int TutorialMoneyBaseline = -1;
    private static int TutorialMaterialBaseline = -1;
    private static UiTheme.BuildCategory TutorialDockBaseline;

    private static readonly string[] TutorialSteps =
    [
        "Camera: WASD / frecce, Shift+trascina o rotella centrale. Ctrl+rotella = zoom. H/Home = torna al CORE.",
        "Dock in basso: tocca le categorie Logistica / Produzione / Energia per cambiare i pezzi disponibili.",
        "Piazza un MINATORE (tasto 2 o dock Produzione) sul giacimento di ferro a ovest del CORE.",
        "Nastri (1): rotella o R ruota la direzione. Amber = uscita (nastro che punta via); ciano = ingresso.",
        "Layout compatto: minatore a contatto col forno trasferisce ore senza nastro in mezzo (stile Mindustry).",
        "Collega NASTRI uscenti fino al CORE: i minerali entrano nello stock (strip in alto).",
        "MERCATO (pannello a sinistra): vendi con 1 / tutti, oppure attiva Vendita automatica.",
        "FABBRICA: conteggi M/F/A/N/G e upgrade CORE (bottone o tasto U) per +25% prezzi vendita.",
        "Apri RICERCA (T o icona albero) e sblocca FORNO, poi altri edifici quando puoi.",
        "Dopo lo sblocco: FORNO (3), ASSEMBLATORE (5), GENERATORE (9), NODI potenza (dock Energia).",
        "Logistica: INCROCIO (6), SDOPPIATORE (7), PONTE (8). Q/E/Y = Nastro T1/T2/T3.",
        "RIMUOVI (4 / X nel dock): rimborso 100% di edifici e nastri.",
        "Campagna: Esc → Home → Campagna per livelli con obiettivi. Sandbox = questa partita libera.",
        "Impostazioni (I): scala UI, VSync, Rivedi tutorial. Backspace salta il tutorial; Esc chiude toast/menu."
    ];

    /// <summary>Self-test hook: stock-first tutorial length.</summary>
    internal static int TutorialStepCount => TutorialSteps.Length;

    private static GameSettings? ActiveSettings;

    // Ephemeral status toast — auto-clears so it cannot linger over the HUD.
    private static string? StatusToastTracked;
    private static double StatusToastUntil;

    private static readonly Direction[] Directions =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West
    ];

    private static readonly Color TerrainGrid = new(14, 18, 16, 70);

    private static readonly HomeAction[] HomeActionsWithContinue =
    [
        HomeAction.Continue,
        HomeAction.Campaign,
        HomeAction.NewGame,
        HomeAction.SaveManager,
        HomeAction.Settings,
        HomeAction.Quit
    ];

    private static readonly HomeAction[] HomeActionsFresh =
    [
        HomeAction.Campaign,
        HomeAction.NewGame,
        HomeAction.SaveManager,
        HomeAction.Settings,
        HomeAction.Quit
    ];

    // Campaign mode (data-driven levels from campaign.json).
    private static CampaignCatalog? Campaign;
    private static CampaignProgress? CampaignProgressState;
    private static CampaignLevelDefinition? ActiveCampaignLevel;
    private static bool CampaignLevelComplete;
    private static bool CampaignVictoryHandled;
    private static string? LoadingCampaignLevelId;
    private static int CampaignSelectScroll;
    private static float TechTreePanX;
    private static float TechTreePanY;
    private static bool TechTreePanning;
    private static Vector2 TechTreePanAnchor;
    private static float TechTreePanStartX;
    private static float TechTreePanStartY;

    public static void Run(
        GameContent content,
        int? maximumFrames = null,
        string? screenshotPath = null,
        bool captureUpgradeCore = false,
        string? captureMode = null)
    {
        var basicConveyor = RequireContent(content.Conveyors, "conveyor-basic", "nastro");
        var fastConveyor = RequireContent(content.Conveyors, "conveyor-fast", "nastro");
        var expressConveyor = RequireContent(content.Conveyors, "conveyor-express", "nastro");
        var junctionConveyor = RequireContent(content.Conveyors, "junction", "nastro");
        var splitterConveyor = RequireContent(content.Conveyors, "splitter", "nastro");
        var sorterConveyor = RequireContent(content.Conveyors, "sorter", "nastro");
        var bridgeConveyor = RequireContent(content.Conveyors, "conveyor-bridge", "nastro");
        var smeltRecipe = RequireRecipe(content.Recipes, "smelt-iron");
        var wireRecipe = RequireRecipe(content.Recipes, "craft-copper-wire");
        var selectedConveyor = basicConveyor;
        var screen = AppScreen.Splash;
        FactoryWorld? world = null;
        ConveyorGrid? conveyors = null;
        EconomyWallet? wallet = null;
        WorldCamera? camera = null;
        ResearchState? research = null;
        EconomySession? session = null;
        MarketCatalog? market = null;
        var economy = content.GetEconomy();
        var minerBuilding = content.GetBuildingOrDefault("miner");
        var advancedMinerBuilding = content.GetBuildingOrDefault("miner-advanced");
        var smelterBuilding = content.GetBuildingOrDefault("smelter");
        var assemblerBuilding = content.GetBuildingOrDefault("assembler");
        var generatorBuilding = content.GetBuildingOrDefault("generator");
        var powerNodeBuilding = content.GetBuildingOrDefault("power-node");
        var powerNodeT2Building = content.GetBuildingOrDefault("power-node-t2");
        var tool = BuildTool.Conveyor;
        var direction = Direction.East;
        var accumulator = 0f;
        var nextItemId = 1L;
        var renderedFrames = 0;
        GridPosition? previousDragPosition = null;
        var isPanning = false;
        Vector2 panAnchor = Vector2.Zero;
        float panCameraX = 0f;
        float panCameraY = 0f;
        string? statusMessage = null;
        var saveSlots = Array.Empty<SaveSlotInfo>();
        var selectedSlotIndex = 0;
        var selectedResearchIndex = 0;
        var pendingSeed = DefaultSeed;
        var quitRequested = false;
        var settings = GameSettings.Load();
        ActiveSettings = settings;
        var settingsReturnScreen = AppScreen.Home;
        Campaign = CampaignCatalog.Load();
        CampaignProgressState = CampaignProgress.Load();
        SyncLayoutSize(settings);
        UiTheme.ApplyScalePercent(settings.UiScalePercent);

        // Headless smoke/capture paths jump straight into a playable session.
        if (maximumFrames is not null || screenshotPath is not null)
        {
            StartNewGame(content, DefaultSeed, out world, out conveyors, out wallet, out camera, out research, out session, out market, out nextItemId);
            screen = AppScreen.Playing;
            // Capture demos: Production dock + live Fabbrica counts (miner + belts) + stock.
            tool = BuildTool.Miner;
            DockCategory = UiTheme.BuildCategory.Production;
            DockSelectedId = "miner";
            wallet!.AddMaterial("iron-plate", 12);
            wallet.AddMaterial("iron-ore", 48);
            wallet.AddMaterial("copper-ore", 20);
            wallet.AddMaterial("copper-wire", 8);
            if (captureMode == "io-adjacency")
            {
                SeedCaptureIoAdjacency(
                    world!, conveyors!, wallet, research!, session!, content, basicConveyor, smeltRecipe);
                BeginTutorialIfNeeded(settings);
                TutorialActive = false;
                // Zoom first, then center — CenterOnTile depends on current Zoom.
                camera!.SetZoom(2.0f);
                camera.CenterOnTile(
                    new GridPosition(world!.StarterDepositOrigin.X + 2, world.StarterDepositOrigin.Y + 1),
                    BaseTileSize, ViewportWidth - InfoPanelWidth, ViewportHeight);
                camera.ClampToMap(world.Terrain.Width, world.Terrain.Height, BaseTileSize, ViewportWidth, ViewportHeight);
                // Warm sim so adjacent transfer / I/O tints are visible in the still.
                for (var warm = 0; warm < 90; warm++)
                {
                    world.Update(1f / 30f, conveyors!, wallet!, ref nextItemId, market, session);
                }
            }
            else if (captureMode == "graphics")
            {
                SeedCaptureGraphics(
                    world!, conveyors!, wallet, research!, session!, content, basicConveyor, smeltRecipe, wireRecipe);
                BeginTutorialIfNeeded(settings);
                TutorialActive = false;
                // Zoom first, then center — CenterOnTile depends on current Zoom.
                camera!.SetZoom(1.55f);
                camera.CenterOnTile(
                    new GridPosition(world!.StarterDepositOrigin.X + 1, world.StarterDepositOrigin.Y + 4),
                    BaseTileSize, ViewportWidth - InfoPanelWidth, ViewportHeight);
                camera.ClampToMap(world.Terrain.Width, world.Terrain.Height, BaseTileSize, ViewportWidth, ViewportHeight);
                for (var warm = 0; warm < 120; warm++)
                {
                    world.Update(1f / 30f, conveyors!, wallet!, ref nextItemId, market, session);
                }
            }
            else if (captureMode == "sorter")
            {
                SeedCaptureSorter(
                    world!, conveyors!, wallet, research!, session!, content, basicConveyor, sorterConveyor);
                BeginTutorialIfNeeded(settings);
                TutorialActive = false;
                tool = BuildTool.Sorter;
                DockCategory = UiTheme.BuildCategory.Logistics;
                DockSelectedId = "sorter";
                SorterBrushFilterId = "iron-ore";
                camera!.SetZoom(2.2f);
                camera.CenterOnTile(new GridPosition(8, 6), BaseTileSize, ViewportWidth - InfoPanelWidth, ViewportHeight);
                camera.ClampToMap(world.Terrain.Width, world.Terrain.Height, BaseTileSize, ViewportWidth, ViewportHeight);
                for (var warm = 0; warm < 100; warm++)
                {
                    world.Update(1f / 30f, conveyors!, wallet!, ref nextItemId, market, session);
                }
            }
            else if (captureMode == "icons")
            {
                SeedCaptureIcons(
                    world!, conveyors!, wallet, research!, session!, content, basicConveyor);
                BeginTutorialIfNeeded(settings);
                TutorialActive = false;
                tool = BuildTool.Miner;
                DockCategory = UiTheme.BuildCategory.Production;
                DockSelectedId = "miner";
                camera!.SetZoom(2.4f);
                camera.CenterOnTile(
                    new GridPosition(world!.StarterDepositOrigin.X + 3, world.StarterDepositOrigin.Y + 1),
                    BaseTileSize, ViewportWidth - InfoPanelWidth, ViewportHeight);
                camera.ClampToMap(world.Terrain.Width, world.Terrain.Height, BaseTileSize, ViewportWidth, ViewportHeight);
                // No warm ticks — keep ferro/fili chips parked mid-belt for the still.
            }
            else if (captureMode == "ore-tints")
            {
                SeedCaptureOreTints(
                    world!, conveyors!, wallet, research!, session!, content, basicConveyor);
                BeginTutorialIfNeeded(settings);
                TutorialActive = false;
                tool = BuildTool.Miner;
                DockCategory = UiTheme.BuildCategory.Production;
                DockSelectedId = "miner";
                camera!.SetZoom(2.8f);
                camera.CenterOnTile(
                    new GridPosition(world!.StarterDepositOrigin.X + 1, world.StarterDepositOrigin.Y + MinerBuilding.Size),
                    BaseTileSize, ViewportWidth - InfoPanelWidth, ViewportHeight);
                camera.ClampToMap(world.Terrain.Width, world.Terrain.Height, BaseTileSize, ViewportWidth, ViewportHeight);
                // No warm ticks — keep ferro/rame/carbone parked mid-belt for the still.
            }
            else if (captureMode == "verify-icons-tiers")
            {
                SeedCaptureVerifyIconsTiers(
                    world!, conveyors!, wallet, research!, session!, content,
                    basicConveyor, expressConveyor, smeltRecipe);
                BeginTutorialIfNeeded(settings);
                TutorialActive = false;
                tool = BuildTool.MinerAdvanced;
                DockCategory = UiTheme.BuildCategory.Production;
                DockSelectedId = "miner-advanced";
                statusMessage =
                    $"{content.FindStructure("miner")!.DisplayName} · {content.FindStructure("miner-advanced")!.DisplayName} · "
                    + $"{content.FindStructure("conveyor-basic")!.DisplayName}/{content.FindStructure("conveyor-fast")!.DisplayName}/{content.FindStructure("conveyor-express")!.DisplayName}";
                camera!.SetZoom(2.0f);
                camera.CenterOnTile(
                    new GridPosition(world!.StarterDepositOrigin.X + 2, world.StarterDepositOrigin.Y + 1),
                    BaseTileSize, ViewportWidth - InfoPanelWidth, ViewportHeight);
                camera.ClampToMap(world.Terrain.Width, world.Terrain.Height, BaseTileSize, ViewportWidth, ViewportHeight);
            }
            else if (captureMode == "midgame")
            {
                SeedCaptureMidgame(
                    world!, conveyors!, wallet, research!, session!, content,
                    basicConveyor, expressConveyor, smeltRecipe);
                BeginTutorialIfNeeded(settings);
                TutorialActive = false;
                tool = BuildTool.MinerAdvanced;
                DockCategory = UiTheme.BuildCategory.Production;
                DockSelectedId = "miner-advanced";
                camera!.CenterOnTile(
                    new GridPosition(world!.StarterDepositOrigin.X + 3, world.StarterDepositOrigin.Y + 1),
                    BaseTileSize, ViewportWidth, ViewportHeight);
                camera.SetZoom(1.75f);
                for (var warm = 0; warm < 120; warm++)
                {
                    world.Update(1f / 30f, conveyors!, wallet!, ref nextItemId, market, session);
                }
            }
            else if (captureMode == "power-nodes")
            {
                SeedCapturePowerNodes(
                    world!, conveyors!, wallet, research!, session!, content, basicConveyor, smeltRecipe);
                BeginTutorialIfNeeded(settings);
                TutorialActive = false;
                tool = BuildTool.PowerNode;
                DockCategory = UiTheme.BuildCategory.Power;
                DockSelectedId = "power-node";
                camera!.SetZoom(2.1f);
                camera.CenterOnTile(
                    new GridPosition(world!.CoreOrigin.X - 4, world.CoreOrigin.Y + 1),
                    BaseTileSize, ViewportWidth - InfoPanelWidth, ViewportHeight);
                camera.ClampToMap(world.Terrain.Width, world.Terrain.Height, BaseTileSize, ViewportWidth, ViewportHeight);
                for (var warm = 0; warm < 90; warm++)
                {
                    world.Update(1f / 30f, conveyors!, wallet!, ref nextItemId, market, session);
                }
            }
            else if (captureMode == "tutorial")
            {
                SeedCaptureFactory(
                    world!, conveyors!, wallet, research!, session!, content, economy, basicConveyor,
                    upgradeCore: false);
                settings.TutorialCompleted = false;
                BeginTutorialIfNeeded(settings);
                TutorialActive = true;
                TutorialStep = 4; // compact-layout step — shows extended tutorial banner
            }
            else if (captureMode == "tech-tree")
            {
                SeedCaptureTechTree(wallet!, research!, content);
                BeginTutorialIfNeeded(settings);
                TutorialActive = false;
                TechTreePanX = 0f;
                TechTreePanY = 0f;
                screen = AppScreen.Research;
                var smelterIdx = -1;
                var researchList = ResearchEntries(content);
                for (var i = 0; i < researchList.Count; i++)
                {
                    if (researchList[i].Id == "smelter")
                    {
                        smelterIdx = i;
                        break;
                    }
                }

                selectedResearchIndex = Math.Max(0, smelterIdx);
            }
            else
            {
                SeedCaptureFactory(
                    world!, conveyors!, wallet, research!, session!, content, economy, basicConveyor,
                    upgradeCore: captureUpgradeCore);
                BeginTutorialIfNeeded(settings);
                TutorialActive = false;
            }
        }

        var flags = ConfigFlags.Msaa4xHint;
        if (settings.VSync)
        {
            flags |= ConfigFlags.VSyncHint;
        }

        Raylib.SetConfigFlags(flags);
        Raylib.InitWindow(ScreenWidth, ScreenHeight, "tIndustry");
        Raylib.SetExitKey(KeyboardKey.Null);
        UiTheme.Load();
        UiTheme.ApplyScalePercent(settings.UiScalePercent);
        LoadSplashThumbnail();
        DisplayApplier.Apply(settings);
        SyncLayoutSize(settings);

        while (!quitRequested
            && !Raylib.WindowShouldClose()
            && (maximumFrames is null || renderedFrames < maximumFrames))
        {
            accumulator += Math.Min(Raylib.GetFrameTime(), 0.1f);
            var frameTime = Math.Min(Raylib.GetFrameTime(), 0.1f);
            SystemMonitor.Update(frameTime);

            switch (screen)
            {
                case AppScreen.Splash:
                    HandleSplashInput(ref screen, frameTime);
                    break;

                case AppScreen.Home:
                    HandleHomeInput(
                        content,
                        ref screen,
                        ref world,
                        ref conveyors,
                        ref wallet,
                        ref camera,
                        ref research,
                        ref session,
                        ref market,
                        ref nextItemId,
                        ref tool,
                        ref direction,
                        ref selectedConveyor,
                        basicConveyor,
                        ref previousDragPosition,
                        ref statusMessage,
                        ref saveSlots,
                        ref selectedSlotIndex,
                        ref quitRequested,
                        ref settingsReturnScreen,
                        ref pendingSeed);
                    break;

                case AppScreen.NewGame:
                    HandleNewGameInput(
                        content,
                        ref screen,
                        ref world,
                        ref conveyors,
                        ref wallet,
                        ref camera,
                        ref research,
                        ref session,
                        ref market,
                        ref nextItemId,
                        ref tool,
                        ref direction,
                        ref selectedConveyor,
                        basicConveyor,
                        ref previousDragPosition,
                        ref statusMessage,
                        ref pendingSeed);
                    break;

                case AppScreen.CampaignSelect:
                    HandleCampaignSelectInput(
                        ref screen,
                        ref statusMessage);
                    break;

                case AppScreen.SaveManager:
                    HandleSaveManagerInput(
                        content,
                        ref screen,
                        ref world,
                        ref conveyors,
                        ref wallet,
                        ref camera,
                        ref research,
                        ref session,
                        ref market,
                        ref nextItemId,
                        ref tool,
                        ref direction,
                        ref selectedConveyor,
                        basicConveyor,
                        ref previousDragPosition,
                        ref statusMessage,
                        ref saveSlots,
                        ref selectedSlotIndex);
                    break;

                case AppScreen.Research:
                    HandleResearchInput(
                        content,
                        wallet!,
                        research!,
                        session!,
                        ref screen,
                        ref selectedResearchIndex,
                        ref statusMessage);
                    break;

                case AppScreen.Settings:
                    SettingsDraft ??= settings.Clone();
                    HandleSettingsInput(
                        settings,
                        SettingsDraft,
                        ref screen,
                        settingsReturnScreen,
                        ref statusMessage);
                    break;

                case AppScreen.Loading:
                    HandleLoadingInput(
                        content,
                        ref screen,
                        ref world,
                        ref conveyors,
                        ref wallet,
                        ref camera,
                        ref research,
                        ref session,
                        ref market,
                        ref nextItemId,
                        ref tool,
                        ref direction,
                        ref selectedConveyor,
                        basicConveyor,
                        ref previousDragPosition,
                        ref statusMessage,
                        frameTime);
                    break;

                case AppScreen.Playing:
                    HandlePlayingInput(
                        world!,
                        conveyors!,
                        wallet!,
                        camera!,
                        research!,
                        session!,
                        market!,
                        economy,
                        minerBuilding,
                        advancedMinerBuilding,
                        smelterBuilding,
                        assemblerBuilding,
                        generatorBuilding,
                        powerNodeBuilding,
                        powerNodeT2Building,
                        basicConveyor,
                        fastConveyor,
                        expressConveyor,
                        junctionConveyor,
                        splitterConveyor,
                        sorterConveyor,
                        bridgeConveyor,
                        smeltRecipe,
                        wireRecipe,
                        ref selectedConveyor,
                        ref tool,
                        ref direction,
                        ref previousDragPosition,
                        ref isPanning,
                        ref panAnchor,
                        ref panCameraX,
                        ref panCameraY,
                        ref screen,
                        ref settingsReturnScreen,
                        ref selectedResearchIndex,
                        ref statusMessage,
                        nextItemId,
                        frameTime);
                    while (accumulator >= FixedStep)
                    {
                        world!.Update(
                            FixedStep,
                            conveyors!,
                            wallet!,
                            ref nextItemId,
                            market!,
                            session!,
                            settings.AutoSellAtCore);
                        accumulator -= FixedStep;
                    }

                    if (TutorialActive && world is not null && conveyors is not null && wallet is not null)
                    {
                        UpdateTutorialProgress(world, conveyors, wallet, settings);
                    }

                    if (ActiveCampaignLevel is not null
                        && !CampaignLevelComplete
                        && wallet is not null
                        && session is not null
                        && research is not null
                        && CampaignProgress.AreAllObjectivesComplete(
                            ActiveCampaignLevel, wallet, session, research))
                    {
                        CampaignLevelComplete = true;
                    }

                    break;
            }

            // After input so a fresh message starts its TTL before draw (no blank first frame).
            TickStatusToast(ref statusMessage);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(14, 18, 18, 255));
            switch (screen)
            {
                case AppScreen.Splash:
                    DrawSplash();
                    break;
                case AppScreen.Home:
                    DrawHome(statusMessage);
                    break;
                case AppScreen.NewGame:
                    DrawNewGame(pendingSeed, statusMessage);
                    break;
                case AppScreen.CampaignSelect:
                    DrawCampaignSelect(statusMessage);
                    break;
                case AppScreen.SaveManager:
                    DrawSaveManager(saveSlots, selectedSlotIndex, statusMessage);
                    break;
                case AppScreen.Research:
                    DrawResearch(content, wallet!, research!, selectedResearchIndex, statusMessage, settings);
                    break;
                case AppScreen.Settings:
                    DrawSettings(settings, SettingsDraft ?? settings, statusMessage);
                    break;
                case AppScreen.Loading:
                    DrawLoading(LoadingElapsed);
                    break;
                case AppScreen.Playing:
                    DrawPlaying(
                        world!,
                        conveyors!,
                        wallet!,
                        camera!,
                        research!,
                        session!,
                        market!,
                        economy,
                        settings,
                        basicConveyor,
                        fastConveyor,
                        expressConveyor,
                        junctionConveyor,
                        splitterConveyor,
                        sorterConveyor,
                        bridgeConveyor,
                        selectedConveyor,
                        smeltRecipe,
                        wireRecipe,
                        minerBuilding,
                        advancedMinerBuilding,
                        smelterBuilding,
                        assemblerBuilding,
                        generatorBuilding,
                        powerNodeBuilding,
                        powerNodeT2Building,
                        tool,
                        direction,
                        statusMessage,
                        frameTime);
                    break;
            }

            // FPS overlay only on non-play screens — in-game FPS lives in the header.
            if (screen is not AppScreen.Playing and not AppScreen.Splash)
            {
                DrawDebugOverlays(settings, wallet);
            }
            Raylib.EndDrawing();

            if (screenshotPath is not null && renderedFrames == 1)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
                Raylib.TakeScreenshot(screenshotPath);
            }

            renderedFrames++;
        }

        if ((screen is AppScreen.Playing or AppScreen.Research or AppScreen.Settings)
            && world is not null && camera is not null && research is not null && session is not null)
        {
            AutoSaveContinue(world, conveyors!, wallet!, camera, research, session, nextItemId);
        }

        UnloadSplashThumbnail();
        UiTheme.Unload();
        Raylib.CloseWindow();
    }

    private static void SyncLayoutSize(GameSettings settings)
    {
        if (Raylib.IsWindowReady())
        {
            ScreenWidth = Math.Max(800, Raylib.GetScreenWidth());
            ScreenHeight = Math.Max(500, Raylib.GetScreenHeight());
            return;
        }

        ScreenWidth = Math.Max(800, settings.ResolutionWidth);
        ScreenHeight = Math.Max(500, settings.ResolutionHeight);
    }

    private static void LoadSplashThumbnail()
    {
        if (SplashThumbnailLoaded)
        {
            return;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "assets", "thumbnail.png");
        if (!File.Exists(path))
        {
            return;
        }

        SplashThumbnail = Raylib.LoadTexture(path);
        Raylib.SetTextureFilter(SplashThumbnail, TextureFilter.Bilinear);
        SplashThumbnailLoaded = true;
    }

    private static void UnloadSplashThumbnail()
    {
        if (!SplashThumbnailLoaded)
        {
            return;
        }

        Raylib.UnloadTexture(SplashThumbnail);
        SplashThumbnailLoaded = false;
    }

    private static void HandleSplashInput(ref AppScreen screen, float frameTime)
    {
        SplashElapsed += frameTime;
        var dismiss =
            SplashElapsed >= SplashMaxSeconds
            || (SplashElapsed >= SplashMinSeconds
                && (Raylib.IsMouseButtonPressed(MouseButton.Left)
                    || Raylib.IsMouseButtonPressed(MouseButton.Right)
                    || Raylib.GetKeyPressed() != 0
                    || Raylib.IsGamepadButtonPressed(0, GamepadButton.RightFaceDown)));

        if (dismiss)
        {
            screen = AppScreen.Home;
        }
    }

    private static void DrawSplash()
    {
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color(10, 12, 14, 255));
        Raylib.DrawRectangleGradientV(0, 0, ScreenWidth, ScreenHeight,
            new Color(22, 34, 30, 255), new Color(8, 10, 10, 255));

        var pulse = 0.5f + 0.5f * MathF.Sin((float)Raylib.GetTime() * 2.4f);
        var title = "tINDUSTRY";
        var titleSize = 52;
        var titleW = MeasureUiText(title, titleSize);
        DrawUiText(title, (ScreenWidth - titleW) / 2, ScreenHeight / 2 - 250, titleSize,
            new Color(239, 238, 224, 255));

        var thumbSize = Math.Min(280, Math.Min(ScreenWidth, ScreenHeight) / 2);
        var thumbX = (ScreenWidth - thumbSize) / 2;
        var thumbY = ScreenHeight / 2 - thumbSize / 2 - 20;
        if (SplashThumbnailLoaded)
        {
            var src = new Rectangle(0, 0, SplashThumbnail.Width, SplashThumbnail.Height);
            var dst = new Rectangle(thumbX, thumbY, thumbSize, thumbSize);
            Raylib.DrawRectangle(thumbX - 6, thumbY - 6, thumbSize + 12, thumbSize + 12,
                new Color(30, 38, 36, 255));
            Raylib.DrawRectangleLines(thumbX - 6, thumbY - 6, thumbSize + 12, thumbSize + 12,
                new Color(211, 164, 76, (int)(140 + pulse * 80)));
            Raylib.DrawTexturePro(SplashThumbnail, src, dst, Vector2.Zero, 0f, Color.White);
        }
        else
        {
            Raylib.DrawRectangle(thumbX, thumbY, thumbSize, thumbSize, new Color(32, 48, 42, 255));
            UiTheme.DrawBuildCategoryIcon(UiTheme.BuildCategory.Production,
                thumbX + thumbSize / 4, thumbY + thumbSize / 4, thumbSize / 2, new Color(211, 164, 76, 255));
        }

        var tagline = "Settore Foundry";
        var tagW = MeasureUiText(tagline, 18);
        DrawUiText(tagline, (ScreenWidth - tagW) / 2, thumbY + thumbSize + 24, 18,
            new Color(164, 173, 168, 255));

        if (SplashElapsed >= SplashMinSeconds)
        {
            var hint = "Clicca o premi un tasto per continuare";
            var hintW = MeasureUiText(hint, 16);
            var hintAlpha = (int)(140 + pulse * 100);
            DrawUiText(hint, (ScreenWidth - hintW) / 2, ScreenHeight - 64, 16,
                new Color(211, 164, 76, hintAlpha));
        }
    }

    private static void DrawUiText(string text, int x, int y, int size, Color color) =>
        UiTheme.DrawText(text, x, y, size, color);

    private static int MeasureUiText(string text, int size) =>
        UiTheme.Measure(text, size);

    private static void BeginLoadingNewGame(int seed, ref AppScreen screen, ref string? statusMessage)
    {
        LoadingSeed = seed;
        LoadingFromSave = false;
        LoadingSlotId = null;
        LoadingCampaignLevelId = null;
        ActiveCampaignLevel = null;
        CampaignLevelComplete = false;
        CampaignVictoryHandled = false;
        LoadingElapsed = 0f;
        LoadingWorldReady = false;
        LoadingLabel = "Generazione mappa…";
        EntryAnimT = 0f;
        statusMessage = null;
        screen = AppScreen.Loading;
    }

    private static void BeginLoadingCampaignLevel(
        CampaignLevelDefinition level,
        ref AppScreen screen,
        ref string? statusMessage)
    {
        LoadingSeed = level.Seed;
        LoadingFromSave = false;
        LoadingSlotId = null;
        LoadingCampaignLevelId = level.Id;
        ActiveCampaignLevel = level;
        CampaignLevelComplete = false;
        CampaignVictoryHandled = false;
        LoadingElapsed = 0f;
        LoadingWorldReady = false;
        LoadingLabel = $"Livello: {level.Name}…";
        EntryAnimT = 0f;
        statusMessage = null;
        screen = AppScreen.Loading;
    }

    private static void BeginLoadingSave(string slotId, ref AppScreen screen, ref string? statusMessage)
    {
        LoadingSeed = DefaultSeed;
        LoadingFromSave = true;
        LoadingSlotId = slotId;
        LoadingCampaignLevelId = null;
        ActiveCampaignLevel = null;
        CampaignLevelComplete = false;
        CampaignVictoryHandled = false;
        LoadingElapsed = 0f;
        LoadingWorldReady = false;
        LoadingLabel = "Caricamento salvataggio…";
        EntryAnimT = 0f;
        statusMessage = null;
        screen = AppScreen.Loading;
    }

    private static void HandleLoadingInput(
        GameContent content,
        ref AppScreen screen,
        ref FactoryWorld? world,
        ref ConveyorGrid? conveyors,
        ref EconomyWallet? wallet,
        ref WorldCamera? camera,
        ref ResearchState? research,
        ref EconomySession? session,
        ref MarketCatalog? market,
        ref long nextItemId,
        ref BuildTool tool,
        ref Direction direction,
        ref ConveyorDefinition selectedConveyor,
        ConveyorDefinition basicConveyor,
        ref GridPosition? previousDragPosition,
        ref string? statusMessage,
        float frameTime)
    {
        LoadingElapsed += frameTime;

        // First frames: paint the loading UI, then build the world once.
        if (!LoadingWorldReady && LoadingElapsed >= 0.08f)
        {
            if (LoadingFromSave)
            {
                if (TryLoadSlot(
                        LoadingSlotId ?? GameSaveStore.ContinueSlotId,
                        content,
                        out world,
                        out conveyors,
                        out wallet,
                        out camera,
                        out research,
                        out session,
                        out market,
                        out nextItemId,
                        out var error))
                {
                    tool = BuildTool.Conveyor;
                    direction = Direction.East;
                    selectedConveyor = basicConveyor;
                    previousDragPosition = null;
                    DockCategory = UiTheme.BuildCategory.Logistics;
                    SyncDockSelection(tool, selectedConveyor, direction);
                    LoadingWorldReady = true;
                    LoadingLabel = "Avvio…";
                }
                else
                {
                    statusMessage = string.IsNullOrEmpty(error)
                        ? "Nessuna partita da continuare."
                        : error;
                    screen = AppScreen.Home;
                    return;
                }
            }
            else
            {
                if (LoadingCampaignLevelId is not null
                    && Campaign is not null
                    && Campaign.Find(LoadingCampaignLevelId) is { } campaignLevel)
                {
                    ActiveCampaignLevel = campaignLevel;
                    StartCampaignLevel(
                        content,
                        campaignLevel,
                        out world,
                        out conveyors,
                        out wallet,
                        out camera,
                        out research,
                        out session,
                        out market,
                        out nextItemId);
                }
                else
                {
                    ActiveCampaignLevel = null;
                    StartNewGame(
                        content,
                        LoadingSeed,
                        out world,
                        out conveyors,
                        out wallet,
                        out camera,
                        out research,
                        out session,
                        out market,
                        out nextItemId);
                }

                tool = BuildTool.Conveyor;
                direction = Direction.East;
                selectedConveyor = basicConveyor;
                previousDragPosition = null;
                DockCategory = UiTheme.BuildCategory.Logistics;
                SyncDockSelection(tool, selectedConveyor, direction);
                LoadingWorldReady = true;
                LoadingLabel = "Avvio…";
            }
        }

        if (LoadingWorldReady && LoadingElapsed >= LoadingMinSeconds)
        {
            EntryAnimT = 0f;
            statusMessage = null;
            screen = AppScreen.Playing;
            if (!LoadingFromSave && ActiveSettings is not null && ActiveCampaignLevel is null)
            {
                // Every confirmed Nuova partita restarts the Peak-style banner tutorial.
                // Campaign levels use objectives instead.
                RestartTutorial(ActiveSettings);
            }
            else if (ActiveCampaignLevel is not null)
            {
                TutorialActive = false;
            }
        }
    }

    private static void DrawLoading(float elapsed)
    {
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color(12, 14, 16, 255));
        Raylib.DrawRectangleGradientV(0, 0, ScreenWidth, ScreenHeight,
            new Color(22, 32, 28, 255), new Color(10, 12, 12, 255));

        DrawUiText("tINDUSTRY", ScreenWidth / 2 - MeasureUiText("tINDUSTRY", 42) / 2, ScreenHeight / 2 - 90,
            42, new Color(239, 238, 224, 255));
        DrawUiText(LoadingLabel, ScreenWidth / 2 - MeasureUiText(LoadingLabel, 18) / 2, ScreenHeight / 2 - 30,
            18, new Color(164, 173, 168, 255));

        var barW = 360;
        var barX = (ScreenWidth - barW) / 2;
        var barY = ScreenHeight / 2 + 10;
        var progress = Math.Clamp(elapsed / LoadingMinSeconds, 0f, 1f);
        if (LoadingWorldReady)
        {
            progress = Math.Max(progress, 0.92f);
        }
        else
        {
            progress = Math.Min(progress, 0.55f);
        }

        Raylib.DrawRectangle(barX, barY, barW, 14, new Color(32, 38, 36, 255));
        Raylib.DrawRectangle(barX, barY, (int)(barW * progress), 14, new Color(211, 164, 76, 255));
        Raylib.DrawRectangleLines(barX, barY, barW, 14, new Color(70, 82, 76, 255));

        // Soft pulse rings while waiting (cheap motion).
        var pulse = 0.5f + 0.5f * MathF.Sin(elapsed * 4f);
        var ringR = 28 + (int)(pulse * 10);
        Raylib.DrawCircleLines(ScreenWidth / 2, ScreenHeight / 2 + 70, ringR, new Color(211, 164, 76, (int)(80 + pulse * 100)));
    }

    private static void DrawEntryOverlay(float frameTime)
    {
        if (EntryAnimT >= 1f)
        {
            return;
        }

        EntryAnimT = Math.Min(1f, EntryAnimT + frameTime / EntryAnimDuration);
        // Ease-out: fade black + slight letterbox.
        var t = EntryAnimT;
        var ease = 1f - (1f - t) * (1f - t);
        var alpha = (int)((1f - ease) * 255);
        if (alpha <= 0)
        {
            return;
        }

        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color(8, 10, 10, alpha));
        var band = (int)((1f - ease) * 48);
        if (band > 0)
        {
            Raylib.DrawRectangle(0, 0, ScreenWidth, band, new Color(0, 0, 0, alpha));
            Raylib.DrawRectangle(0, ScreenHeight - band, ScreenWidth, band, new Color(0, 0, 0, alpha));
        }
    }

    private static int HeaderIconX(int indexFromRight) =>
        ScreenWidth - 16 - HeaderIconSize - indexFromRight * (HeaderIconSize + HeaderIconGap);

    private static bool HitHeaderIcon(Vector2 mouse, int indexFromRight) =>
        Contains(mouse, HeaderIconX(indexFromRight), 12, HeaderIconSize, HeaderIconSize);

    private static void DrawHeaderIconButton(int indexFromRight, bool hover, Action drawIcon)
    {
        var x = HeaderIconX(indexFromRight);
        const int y = 12;
        Raylib.DrawRectangle(x, y, HeaderIconSize, HeaderIconSize,
            hover ? new Color(55, 66, 60, 255) : new Color(45, 52, 50, 255));
        Raylib.DrawRectangleLines(x, y, HeaderIconSize, HeaderIconSize,
            hover ? UiTheme.Accent : new Color(70, 82, 76, 255));
        drawIcon();
    }

    private static void StartNewGame(
        GameContent content,
        int seed,
        out FactoryWorld world,
        out ConveyorGrid conveyors,
        out EconomyWallet wallet,
        out WorldCamera camera,
        out ResearchState research,
        out EconomySession session,
        out MarketCatalog market,
        out long nextItemId)
    {
        world = new FactoryWorld(MapWidth, MapHeight, seed);
        conveyors = new ConveyorGrid();
        wallet = CreateStartingWallet();
        camera = CreateCameraFocusedOnCore(world);
        research = ResearchState.CreateNew(content);
        market = content.CreateMarket();
        session = new EconomySession(wallet.Money);
        nextItemId = 1L;
    }

    private static void StartCampaignLevel(
        GameContent content,
        CampaignLevelDefinition level,
        out FactoryWorld world,
        out ConveyorGrid conveyors,
        out EconomyWallet wallet,
        out WorldCamera camera,
        out ResearchState research,
        out EconomySession session,
        out MarketCatalog market,
        out long nextItemId)
    {
        var width = Math.Max(12, level.MapWidth);
        var height = Math.Max(8, level.MapHeight);
        world = new FactoryWorld(width, height, level.Seed);
        conveyors = new ConveyorGrid();
        wallet = (Campaign ?? CampaignCatalog.Load()).CreateWallet(level);
        camera = CreateCameraFocusedOnCore(world);
        research = ResearchState.CreateNew(content);
        market = content.CreateMarket();
        session = new EconomySession(wallet.Money);
        nextItemId = 1L;
        CampaignLevelComplete = false;
        CampaignVictoryHandled = false;
    }

    private static EconomyWallet CreateStartingWallet() =>
        new(180, new Dictionary<string, int>
        {
            ["iron-plate"] = 48,
            ["copper-wire"] = 10
        });

    /// <summary>
    /// Places a starter miner + belt run for --capture HUD stills (non-zero Fabbrica counts).
    /// Optionally upgrades the core when <paramref name="upgradeCore"/> is true.
    /// </summary>
    /// <summary>
    /// Mid-game Phase 6 still: advanced miner, express belt, coal→generator fuel.
    /// </summary>
    private static void SeedCaptureMidgame(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        ResearchState research,
        EconomySession session,
        GameContent content,
        ConveyorDefinition basicConveyor,
        ConveyorDefinition expressConveyor,
        RecipeDefinition smeltRecipe)
    {
        research.ForceUnlock("smelter");
        research.ForceUnlock("generator");
        research.ForceUnlock(MinerBuilding.AdvancedId);
        research.ForceUnlock("conveyor-express");
        research.ForceUnlock("conveyor-fast");

        wallet.AddMoney(500);
        wallet.AddMaterial("iron-plate", 80);
        wallet.AddMaterial("copper-wire", 40);
        wallet.AddMaterial("coal", 12);

        var advancedMiner = content.GetBuildingOrDefault("miner-advanced");
        var minerAt = world.StarterDepositOrigin;
        world.TryPlaceMiner(
            minerAt, Direction.East, conveyors, wallet, advancedMiner, session, MinerBuilding.AdvancedId);

        var coalAt = new GridPosition(minerAt.X + MinerBuilding.Size + 1, minerAt.Y);
        var basicMiner = content.GetBuildingOrDefault("miner");
        if (world.Terrain[coalAt].Deposit == DepositKind.Coal)
        {
            world.TryPlaceMiner(coalAt, Direction.South, conveyors, wallet, basicMiner, session);
        }

        var expressY = minerAt.Y;
        var startX = minerAt.X + MinerBuilding.Size;
        // Leave coal footprint clear if occupied.
        for (var x = startX; x < world.CoreOrigin.X; x++)
        {
            var pos = new GridPosition(x, expressY);
            if (!world.CanPlaceConveyor(pos) || world.IsMinerTile(pos))
            {
                continue;
            }

            conveyors.TryPlace(pos, Direction.East, expressConveyor, wallet, research, session, world.CanPlaceConveyor);
        }

        var smelterBuilding = content.GetBuildingOrDefault("smelter");
        var smelterAt = new GridPosition(minerAt.X, minerAt.Y + MinerBuilding.Size + 2);
        if (world.CanPlaceSmelter(smelterAt, conveyors))
        {
            world.TryPlaceSmelter(smelterAt, Direction.East, smeltRecipe, conveyors, wallet, smelterBuilding, session);
        }

        var generatorBuilding = content.GetBuildingOrDefault("generator");
        var genAt = new GridPosition(smelterAt.X + SmelterBuilding.Size + 2, smelterAt.Y);
        if (world.CanPlaceGenerator(genAt, conveyors))
        {
            world.TryPlaceGenerator(genAt, conveyors, wallet, generatorBuilding, session);
            var fuelBelt = new GridPosition(genAt.X - 1, genAt.Y);
            if (world.CanPlaceConveyor(fuelBelt))
            {
                conveyors.TryPlace(fuelBelt, Direction.East, basicConveyor, wallet, research, session, world.CanPlaceConveyor);
                if (conveyors.Cells.TryGetValue(fuelBelt, out var cell))
                {
                    cell.TryInsert(new TransportedItem(1, "coal"));
                    cell.TryInsert(new TransportedItem(2, "coal"));
                }
            }

            if (world.TryGetGeneratorAt(genAt, out var gen))
            {
                gen.TryAcceptFuel("coal");
                gen.TryAcceptFuel("coal");
                gen.TryAcceptFuel("coal");
            }
        }

        var nodeCost = content.GetBuildingOrDefault("power-node");
        research.ForceUnlock("power-node");
        research.ForceUnlock("power-node-t2");
        if (world.Smelters.Count > 0)
        {
            var smelterPos = world.Smelters.Keys.First();
            world.TryEnsurePowerLinkToCore(smelterPos, SmelterBuilding.Size, conveyors, wallet, nodeCost, session);
        }

        if (world.Generators.Count > 0)
        {
            var genPos = world.Generators.Keys.First();
            world.TryEnsurePowerLinkToCore(genPos, GeneratorBuilding.Size, conveyors, wallet, nodeCost, session);
        }

        world.RefreshPowerNetworks();
    }

    private static void SeedCapturePowerNodes(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        ResearchState research,
        EconomySession session,
        GameContent content,
        ConveyorDefinition basicConveyor,
        RecipeDefinition smeltRecipe)
    {
        research.ForceUnlock("smelter");
        research.ForceUnlock("generator");
        research.ForceUnlock("power-node");
        research.ForceUnlock("power-node-t2");
        wallet.AddMoney(400);
        wallet.AddMaterial("iron-plate", 60);
        wallet.AddMaterial("copper-wire", 40);
        wallet.AddMaterial("coal", 8);

        var smelterBuilding = content.GetBuildingOrDefault("smelter");
        var generatorBuilding = content.GetBuildingOrDefault("generator");
        var nodeT1 = content.GetBuildingOrDefault("power-node");
        var nodeT2 = content.GetBuildingOrDefault("power-node-t2");

        // Smelter west of core — within T1 node range once a relay is placed.
        var smelterAt = new GridPosition(world.CoreOrigin.X - 5, world.CoreOrigin.Y);
        world.TryPlaceSmelter(smelterAt, Direction.East, smeltRecipe, conveyors, wallet, smelterBuilding, session);

        // Fueled generator further west, still within T1 hop range via a mid node.
        var genAt = new GridPosition(smelterAt.X - GeneratorBuilding.Size - 2, smelterAt.Y);
        world.TryPlaceGenerator(genAt, conveyors, wallet, generatorBuilding, session);
        if (world.TryGetGeneratorAt(genAt, out var gen))
        {
            gen.TryAcceptFuel("coal");
            gen.TryAcceptFuel("coal");
            gen.TryAcceptFuel("coal");
        }

        var fuelBelt = new GridPosition(genAt.X - 1, genAt.Y);
        if (world.CanPlaceConveyor(fuelBelt))
        {
            conveyors.TryPlace(fuelBelt, Direction.East, basicConveyor, wallet, research, session, world.CanPlaceConveyor);
            if (conveyors.Cells.TryGetValue(fuelBelt, out var cell))
            {
                cell.TryInsert(new TransportedItem(1, "coal"));
            }
        }

        // T1 node between gen and smelter — auto-links both + toward core.
        var nodeAt = new GridPosition(smelterAt.X - 1, smelterAt.Y + SmelterBuilding.Size);
        if (world.CanPlacePowerNode(nodeAt, PowerNodeBuilding.Tier1Size, conveyors))
        {
            world.TryPlacePowerNode(nodeAt, conveyors, wallet, PowerNodeBuilding.Tier1Id, nodeT1, session);
        }
        else
        {
            world.TryEnsurePowerLinkToCore(smelterAt, SmelterBuilding.Size, conveyors, wallet, nodeT1, session);
        }

        // Optional T2 further south for capture visual variety.
        var t2At = new GridPosition(smelterAt.X + 1, smelterAt.Y + SmelterBuilding.Size + 3);
        if (world.CanPlacePowerNode(t2At, PowerNodeBuilding.Tier2Size, conveyors))
        {
            world.TryPlacePowerNode(t2At, conveyors, wallet, PowerNodeBuilding.Tier2Id, nodeT2, session);
        }

        world.RefreshPowerNetworks();
        // Tick once so the generator enters burn state and live beams light up.
        var tickId = 1L;
        world.Update(1f / 30f, conveyors, wallet, ref tickId);
    }

    private static void SeedCaptureFactory(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        ResearchState research,
        EconomySession session,
        GameContent content,
        EconomyConfig economy,
        ConveyorDefinition basicConveyor,
        bool upgradeCore = false)
    {
        var minerBuilding = content.GetBuildingOrDefault("miner");
        var minerAt = world.StarterDepositOrigin;
        world.TryPlaceMiner(minerAt, Direction.East, conveyors, wallet, minerBuilding, session);

        var beltY = minerAt.Y;
        var startX = minerAt.X + MinerBuilding.Size;
        var endX = world.CoreOrigin.X - 1;
        for (var x = startX; x <= endX; x++)
        {
            conveyors.TryPlace(
                new GridPosition(x, beltY),
                Direction.East,
                basicConveyor,
                wallet,
                research,
                session,
                world.CanPlaceConveyor);
        }

        if (upgradeCore)
        {
            // Ensure affordability for the still (capture wallet already has stock + $).
            wallet.AddMoney(Math.Max(0, economy.CoreUpgrade.MoneyCost - wallet.Money + 10));
            wallet.AddMaterial("iron-plate", 20);
            world.TryUpgradeCore(wallet, economy.CoreUpgrade, session);
        }
    }

    private static void SeedCaptureTechTree(
        EconomyWallet wallet,
        ResearchState research,
        GameContent content)
    {
        wallet.AddMoney(400);
        wallet.AddMaterial("iron-plate", 80);
        wallet.AddMaterial("copper-wire", 12);

        // Mixed states: forno unlocked, logistica base unlocked, rest locked/available.
        research.ForceUnlock("smelter");
        research.ForceUnlock("junction");
        _ = content;
    }

    /// <summary>
    /// Showcase layout for --capture-graphics: distinct silhouettes for miner/forno/assy/gen/core + belts.
    /// Buildings spread south of the starter deposit so they stay west of the CORE (deposit is at coreLeft-4).
    /// </summary>
    private static void SeedCaptureGraphics(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        ResearchState research,
        EconomySession session,
        GameContent content,
        ConveyorDefinition basicConveyor,
        RecipeDefinition smeltRecipe,
        RecipeDefinition wireRecipe)
    {
        foreach (var id in new[] { "smelter", "assembler", "generator", "conveyor-fast" })
        {
            if (!research.IsUnlocked(id))
            {
                research.ForceUnlock(id);
            }
        }

        wallet.AddMoney(500);
        wallet.AddMaterial("iron-plate", 80);
        wallet.AddMaterial("copper-wire", 40);

        var minerBuilding = content.GetBuildingOrDefault("miner");
        var smelterBuilding = content.GetBuildingOrDefault("smelter");
        var assemblerBuilding = content.GetBuildingOrDefault("assembler");
        var generatorBuilding = content.GetBuildingOrDefault("generator");
        var fastConveyor = content.Conveyors.Single(definition => definition.Id == "conveyor-fast");

        var minerAt = world.StarterDepositOrigin;
        world.TryPlaceMiner(minerAt, Direction.East, conveyors, wallet, minerBuilding, session);

        // Keep the production column west of CORE (starter deposit sits at coreLeft-4).
        var smelterAt = new GridPosition(minerAt.X, minerAt.Y + MinerBuilding.Size + 1);
        world.TryPlaceSmelter(smelterAt, Direction.South, smeltRecipe, conveyors, wallet, smelterBuilding, session);

        var feedBelt = new GridPosition(minerAt.X, minerAt.Y + MinerBuilding.Size);
        conveyors.TryPlace(feedBelt, Direction.South, basicConveyor, wallet, research, session, world.CanPlaceConveyor);

        var assemblerAt = new GridPosition(smelterAt.X, smelterAt.Y + SmelterBuilding.Size + 1);
        world.TryPlaceAssembler(assemblerAt, Direction.South, wireRecipe, conveyors, wallet, assemblerBuilding, session);
        var plateBelt = new GridPosition(smelterAt.X, smelterAt.Y + SmelterBuilding.Size);
        conveyors.TryPlace(plateBelt, Direction.South, fastConveyor, wallet, research, session, world.CanPlaceConveyor);

        // Generator west of smelter (still clear of CORE).
        var generatorAt = new GridPosition(Math.Max(0, smelterAt.X - GeneratorBuilding.Size - 1), smelterAt.Y);
        world.TryPlaceGenerator(generatorAt, conveyors, wallet, generatorBuilding, session);

        // Eastward belt stubs from miner/smelter for chevron + I/O readability (stop before CORE).
        foreach (var (origin, size, dir) in new[]
                 {
                     (minerAt, MinerBuilding.Size, Direction.East),
                     (smelterAt, SmelterBuilding.Size, Direction.East)
                 })
        {
            var belt = new GridPosition(origin.X + size, origin.Y);
            if (belt.X < world.CoreOrigin.X && world.CanPlaceConveyor(belt))
            {
                conveyors.TryPlace(belt, dir, basicConveyor, wallet, research, session, world.CanPlaceConveyor);
            }
        }

        // Short eastbound row under assembler for fast-tier chevrons.
        var beltY = assemblerAt.Y + 1;
        for (var i = 0; i < 3; i++)
        {
            var at = new GridPosition(assemblerAt.X + SmelterBuilding.Size + i, beltY);
            if (at.X >= world.CoreOrigin.X || !world.CanPlaceConveyor(at))
            {
                break;
            }

            conveyors.TryPlace(
                at,
                Direction.East,
                i == 0 ? basicConveyor : fastConveyor,
                wallet,
                research,
                session,
                world.CanPlaceConveyor);
        }
    }

    /// <summary>
    /// Compact Mindustry layout for --capture-io: miner flush against smelter + outward belts tinted.
    /// </summary>
    private static void SeedCaptureIoAdjacency(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        ResearchState research,
        EconomySession session,
        GameContent content,
        ConveyorDefinition basicConveyor,
        RecipeDefinition smeltRecipe)
    {
        var minerBuilding = content.GetBuildingOrDefault("miner");
        var smelterBuilding = content.GetBuildingOrDefault("smelter");
        var minerAt = world.StarterDepositOrigin;
        world.TryPlaceMiner(minerAt, Direction.East, conveyors, wallet, minerBuilding, session);

        var smelterTech = content.FindStructure("smelter");
        if (smelterTech is not null && !research.IsUnlocked("smelter"))
        {
            research.ForceUnlock("smelter");
        }

        var smelterAt = new GridPosition(minerAt.X + MinerBuilding.Size, minerAt.Y);
        world.TryPlaceSmelter(smelterAt, Direction.East, smeltRecipe, conveyors, wallet, smelterBuilding, session);

        // Outward output belt east of smelter (amber overlay) + inward feed stub north (cyan).
        var outBelt = new GridPosition(smelterAt.X + SmelterBuilding.Size, smelterAt.Y);
        conveyors.TryPlace(outBelt, Direction.East, basicConveyor, wallet, research, session, world.CanPlaceConveyor);
        var inBelt = new GridPosition(smelterAt.X, smelterAt.Y - 1);
        if (world.CanPlaceConveyor(inBelt))
        {
            conveyors.TryPlace(inBelt, Direction.South, basicConveyor, wallet, research, session, world.CanPlaceConveyor);
        }

        // Extra outward belt on miner south for amber I/O contrast.
        var minerSouth = new GridPosition(minerAt.X, minerAt.Y + MinerBuilding.Size);
        if (world.CanPlaceConveyor(minerSouth) && !conveyors.Cells.ContainsKey(minerSouth))
        {
            conveyors.TryPlace(minerSouth, Direction.South, basicConveyor, wallet, research, session, world.CanPlaceConveyor);
        }
    }

    /// <summary>
    /// Capture scene for --capture-icons: Minatore dock=drill + belt ferro vs fili side-by-side.
    /// </summary>
    private static void SeedCaptureIcons(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        ResearchState research,
        EconomySession session,
        GameContent content,
        ConveyorDefinition basicConveyor)
    {
        wallet.AddMoney(200);
        wallet.AddMaterial("iron-plate", 40);
        wallet.AddMaterial("iron-ore", 24);
        wallet.AddMaterial("copper-wire", 24);

        var minerBuilding = content.GetBuildingOrDefault("miner");
        var minerAt = world.StarterDepositOrigin;
        world.TryPlaceMiner(minerAt, Direction.East, conveyors, wallet, minerBuilding, session);

        // Eastbound showcase belt: ferro then fili, mid-tile for readable glyphs.
        var beltY = minerAt.Y + 1;
        var startX = minerAt.X + MinerBuilding.Size;
        for (var i = 0; i < 5; i++)
        {
            var at = new GridPosition(startX + i, beltY);
            if (!world.CanPlaceConveyor(at))
            {
                break;
            }

            if (!conveyors.TryPlace(at, Direction.East, basicConveyor, wallet, research, session, world.CanPlaceConveyor))
            {
                continue;
            }
        }

        void Park(GridPosition at, string itemId, long id, float progress)
        {
            if (!conveyors.Cells.TryGetValue(at, out var cell))
            {
                return;
            }

            cell.TryInsert(new TransportedItem(id, itemId));
            if (cell.Items.Count > 0)
            {
                cell.Items[^1].Progress = progress;
            }
        }

        Park(new GridPosition(startX, beltY), "iron-ore", 91001, 0.45f);
        Park(new GridPosition(startX + 1, beltY), "copper-wire", 91002, 0.55f);
        Park(new GridPosition(startX + 2, beltY), "iron-ore", 91003, 0.40f);
        Park(new GridPosition(startX + 3, beltY), "copper-wire", 91004, 0.60f);
    }

    /// <summary>
    /// Capture scene for --capture-ore-tints: same rock silhouette, ferro / rame / carbone tints.
    /// </summary>
    private static void SeedCaptureOreTints(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        ResearchState research,
        EconomySession session,
        GameContent content,
        ConveyorDefinition basicConveyor)
    {
        wallet.AddMoney(200);
        wallet.AddMaterial("iron-plate", 40);
        wallet.AddMaterial("iron-ore", 18);
        wallet.AddMaterial("copper-ore", 18);
        wallet.AddMaterial("coal", 18);

        var minerBuilding = content.GetBuildingOrDefault("miner");
        var minerAt = world.StarterDepositOrigin;
        world.TryPlaceMiner(minerAt, Direction.East, conveyors, wallet, minerBuilding, session);

        // Clear eastbound belt south of the miner so ferro / rame / carbone sit side-by-side.
        var beltY = minerAt.Y + MinerBuilding.Size;
        var startX = minerAt.X;
        for (var i = 0; i < 5; i++)
        {
            var at = new GridPosition(startX + i, beltY);
            if (!world.CanPlaceConveyor(at))
            {
                continue;
            }

            conveyors.TryPlace(at, Direction.East, basicConveyor, wallet, research, session, world.CanPlaceConveyor);
        }

        void Park(GridPosition at, string itemId, long id, float progress)
        {
            if (!conveyors.Cells.TryGetValue(at, out var cell))
            {
                return;
            }

            cell.TryInsert(new TransportedItem(id, itemId));
            if (cell.Items.Count > 0)
            {
                cell.Items[^1].Progress = progress;
            }
        }

        Park(new GridPosition(startX, beltY), "iron-ore", 92001, 0.50f);
        Park(new GridPosition(startX + 1, beltY), "copper-ore", 92002, 0.50f);
        Park(new GridPosition(startX + 2, beltY), "coal", 92003, 0.50f);
    }

    /// <summary>
    /// Proof still: Minatore T2 + drill dock, ore rock tints + fili, toast with T1/T2/T3 names.
    /// </summary>
    private static void SeedCaptureVerifyIconsTiers(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        ResearchState research,
        EconomySession session,
        GameContent content,
        ConveyorDefinition basicConveyor,
        ConveyorDefinition expressConveyor,
        RecipeDefinition smeltRecipe)
    {
        SeedCaptureMidgame(
            world, conveyors, wallet, research, session, content,
            basicConveyor, expressConveyor, smeltRecipe);

        wallet.AddMaterial("iron-ore", 12);
        wallet.AddMaterial("copper-ore", 12);
        wallet.AddMaterial("copper-wire", 12);
        wallet.AddMaterial("coal", 12);

        var minerAt = world.StarterDepositOrigin;
        var beltY = minerAt.Y + MinerBuilding.Size;
        var startX = minerAt.X;
        for (var i = 0; i < 5; i++)
        {
            var at = new GridPosition(startX + i, beltY);
            if (!world.CanPlaceConveyor(at))
            {
                continue;
            }

            conveyors.TryPlace(at, Direction.East, basicConveyor, wallet, research, session, world.CanPlaceConveyor);
        }

        void Park(GridPosition at, string itemId, long id, float progress)
        {
            if (!conveyors.Cells.TryGetValue(at, out var cell))
            {
                return;
            }

            cell.TryInsert(new TransportedItem(id, itemId));
            if (cell.Items.Count > 0)
            {
                cell.Items[^1].Progress = progress;
            }
        }

        Park(new GridPosition(startX, beltY), "iron-ore", 93001, 0.42f);
        Park(new GridPosition(startX + 1, beltY), "copper-ore", 93002, 0.50f);
        Park(new GridPosition(startX + 2, beltY), "coal", 93003, 0.48f);
        Park(new GridPosition(startX + 3, beltY), "copper-wire", 93004, 0.55f);
    }

    /// <summary>
    /// Capture scene for --capture-sorter: mixed cargo into a filter facing east.
    /// Match (iron-ore) goes forward; overflow (copper-ore) exits north.
    /// </summary>
    private static void SeedCaptureSorter(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        ResearchState research,
        EconomySession session,
        GameContent content,
        ConveyorDefinition basicConveyor,
        ConveyorDefinition sorterConveyor)
    {
        research.ForceUnlock("sorter");
        wallet.AddMoney(500);
        wallet.AddMaterial("iron-plate", 80);
        wallet.AddMaterial("copper-wire", 20);

        var sorterAt = new GridPosition(8, 6);
        AssertPlace(conveyors.TryPlace(
            new GridPosition(6, 6), Direction.East, basicConveyor, wallet, research, session, world.CanPlaceConveyor));
        AssertPlace(conveyors.TryPlace(
            new GridPosition(7, 6), Direction.East, basicConveyor, wallet, research, session, world.CanPlaceConveyor));
        AssertPlace(conveyors.TryPlace(
            sorterAt, Direction.East, sorterConveyor, wallet, research, session, world.CanPlaceConveyor));
        conveyors.Cells[sorterAt].SetFilterItem("iron-ore");
        AssertPlace(conveyors.TryPlace(
            new GridPosition(9, 6), Direction.East, basicConveyor, wallet, research, session, world.CanPlaceConveyor));
        AssertPlace(conveyors.TryPlace(
            new GridPosition(10, 6), Direction.East, basicConveyor, wallet, research, session, world.CanPlaceConveyor));
        AssertPlace(conveyors.TryPlace(
            new GridPosition(8, 5), Direction.North, basicConveyor, wallet, research, session, world.CanPlaceConveyor));
        AssertPlace(conveyors.TryPlace(
            new GridPosition(8, 4), Direction.North, basicConveyor, wallet, research, session, world.CanPlaceConveyor));
        AssertPlace(conveyors.TryPlace(
            new GridPosition(8, 7), Direction.South, basicConveyor, wallet, research, session, world.CanPlaceConveyor));

        var feed = conveyors.Cells[new GridPosition(6, 6)];
        feed.TryInsert(new TransportedItem(90001, "iron-ore"));
        // Second item after warm ticks via extra insert on neighbor during warm loop is awkward;
        // place copper already mid-line so both routes are visible.
        conveyors.Cells[new GridPosition(7, 6)].TryInsert(new TransportedItem(90002, "copper-ore"));

        static void AssertPlace(bool ok)
        {
            if (!ok)
            {
                throw new InvalidOperationException("SeedCaptureSorter: piazzamento fallito.");
            }
        }
    }

    private static WorldCamera CreateCameraFocusedOnCore(FactoryWorld world)
    {
        var camera = new WorldCamera(0, 0);
        camera.CenterOnTile(
            new GridPosition(world.CoreOrigin.X + 1, world.CoreOrigin.Y + 1),
            BaseTileSize,
            ViewportWidth,
            ViewportHeight);
        camera.ClampToMap(world.Terrain.Width, world.Terrain.Height, BaseTileSize, ViewportWidth, ViewportHeight);
        return camera;
    }

    private static float ViewportWidth => ViewportRight - ViewportLeft;
    private static float ViewportHeight => ViewportBottom - ViewportTop;

    private static void AutoSaveContinue(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        WorldCamera camera,
        ResearchState research,
        EconomySession session,
        long nextItemId)
    {
        var data = GameSaveStore.Capture(world, conveyors, wallet, camera, research, session, nextItemId);
        GameSaveStore.Save(GameSaveStore.ContinueSlotId, data);
    }

    private static bool TryLoadSlot(
        string slotId,
        GameContent content,
        out FactoryWorld world,
        out ConveyorGrid conveyors,
        out EconomyWallet wallet,
        out WorldCamera camera,
        out ResearchState research,
        out EconomySession session,
        out MarketCatalog market,
        out long nextItemId,
        out string error)
    {
        world = null!;
        conveyors = null!;
        wallet = null!;
        camera = null!;
        research = null!;
        session = null!;
        market = null!;
        nextItemId = 1L;
        error = string.Empty;
        if (!GameSaveStore.TryLoad(slotId, out var data))
        {
            error = "Salvataggio non disponibile.";
            return false;
        }

        try
        {
            (world, conveyors, wallet, camera, research, session, nextItemId) = GameSaveStore.Restore(data, content);
            market = content.CreateMarket();
            camera.ClampToMap(world.Terrain.Width, world.Terrain.Height, BaseTileSize, ViewportWidth, ViewportHeight);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static void HandleHomeInput(
        GameContent content,
        ref AppScreen screen,
        ref FactoryWorld? world,
        ref ConveyorGrid? conveyors,
        ref EconomyWallet? wallet,
        ref WorldCamera? camera,
        ref ResearchState? research,
        ref EconomySession? session,
        ref MarketCatalog? market,
        ref long nextItemId,
        ref BuildTool tool,
        ref Direction direction,
        ref ConveyorDefinition selectedConveyor,
        ConveyorDefinition basicConveyor,
        ref GridPosition? previousDragPosition,
        ref string? statusMessage,
        ref SaveSlotInfo[] saveSlots,
        ref int selectedSlotIndex,
        ref bool quitRequested,
        ref AppScreen settingsReturnScreen,
        ref int pendingSeed)
    {
        var mouse = Raylib.GetMousePosition();
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            return;
        }

        var actions = GetHomeActions();
        for (var i = 0; i < actions.Length; i++)
        {
            if (!Contains(mouse, HomeButtonX, HomeButtonY(i), HomeButtonWidth, HomeButtonHeight))
            {
                continue;
            }

            switch (actions[i])
            {
                case HomeAction.Continue:
                    BeginLoadingSave(GameSaveStore.ContinueSlotId, ref screen, ref statusMessage);
                    break;
                case HomeAction.Campaign:
                    CampaignSelectScroll = 0;
                    statusMessage = null;
                    screen = AppScreen.CampaignSelect;
                    break;
                case HomeAction.NewGame:
                    pendingSeed = DefaultSeed;
                    statusMessage = null;
                    settingsReturnScreen = AppScreen.Home;
                    screen = AppScreen.NewGame;
                    break;
                case HomeAction.SaveManager:
                    saveSlots = GameSaveStore.ListSlots().ToArray();
                    selectedSlotIndex = 0;
                    statusMessage = null;
                    screen = AppScreen.SaveManager;
                    break;
                case HomeAction.Settings:
                    statusMessage = null;
                    settingsReturnScreen = AppScreen.Home;
                    SettingsDraft = null;
                    SettingsScrollY = 0f;
                    screen = AppScreen.Settings;
                    break;
                case HomeAction.Quit:
                    quitRequested = true;
                    break;
            }

            return;
        }
    }

    private static void HandleNewGameInput(
        GameContent content,
        ref AppScreen screen,
        ref FactoryWorld? world,
        ref ConveyorGrid? conveyors,
        ref EconomyWallet? wallet,
        ref WorldCamera? camera,
        ref ResearchState? research,
        ref EconomySession? session,
        ref MarketCatalog? market,
        ref long nextItemId,
        ref BuildTool tool,
        ref Direction direction,
        ref ConveyorDefinition selectedConveyor,
        ConveyorDefinition basicConveyor,
        ref GridPosition? previousDragPosition,
        ref string? statusMessage,
        ref int pendingSeed)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left)
                && Contains(Raylib.GetMousePosition(), 28, ScreenHeight - 70, 180, 40)))
        {
            statusMessage = null;
            screen = AppScreen.Home;
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Left))
        {
            pendingSeed = Math.Max(0, pendingSeed - 1);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Right))
        {
            pendingSeed = Math.Min(99999, pendingSeed + 1);
        }

        AppendSeedDigit(ref pendingSeed);

        var mouse = Raylib.GetMousePosition();
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            return;
        }

        if (Contains(mouse, 120, 220, 280, 48))
        {
            pendingSeed = DefaultSeed;
            return;
        }

        if (Contains(mouse, 420, 220, 280, 48))
        {
            pendingSeed = SeedEspanso;
            return;
        }

        if (Contains(mouse, 720, 220, 280, 48))
        {
            pendingSeed = SeedArcipelago;
            return;
        }

        if (Contains(mouse, 420, 320, 48, 48))
        {
            pendingSeed = Math.Max(0, pendingSeed - 1);
            return;
        }

        if (Contains(mouse, 620, 320, 48, 48))
        {
            pendingSeed = Math.Min(99999, pendingSeed + 1);
            return;
        }

        if (Contains(mouse, 420, 420, 280, 52))
        {
            BeginLoadingNewGame(pendingSeed, ref screen, ref statusMessage);
        }
    }

    private static void AppendSeedDigit(ref int pendingSeed)
    {
        for (var digit = 0; digit <= 9; digit++)
        {
            var key = (KeyboardKey)((int)KeyboardKey.Zero + digit);
            var keypad = (KeyboardKey)((int)KeyboardKey.Kp0 + digit);
            if (!Raylib.IsKeyPressed(key) && !Raylib.IsKeyPressed(keypad))
            {
                continue;
            }

            var next = pendingSeed * 10 + digit;
            if (next > 99999)
            {
                next = digit;
            }

            pendingSeed = next;
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Backspace))
        {
            pendingSeed /= 10;
        }
    }

    private static void HandleSaveManagerInput(
        GameContent content,
        ref AppScreen screen,
        ref FactoryWorld? world,
        ref ConveyorGrid? conveyors,
        ref EconomyWallet? wallet,
        ref WorldCamera? camera,
        ref ResearchState? research,
        ref EconomySession? session,
        ref MarketCatalog? market,
        ref long nextItemId,
        ref BuildTool tool,
        ref Direction direction,
        ref ConveyorDefinition selectedConveyor,
        ConveyorDefinition basicConveyor,
        ref GridPosition? previousDragPosition,
        ref string? statusMessage,
        ref SaveSlotInfo[] saveSlots,
        ref int selectedSlotIndex)
    {
        var mouse = Raylib.GetMousePosition();
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left)
                && Contains(mouse, 28, ScreenHeight - 70, 180, 40)))
        {
            screen = AppScreen.Home;
            statusMessage = null;
            return;
        }

        if (saveSlots.Length > 0)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Up))
            {
                selectedSlotIndex = (selectedSlotIndex + saveSlots.Length - 1) % saveSlots.Length;
            }

            if (Raylib.IsKeyPressed(KeyboardKey.Down))
            {
                selectedSlotIndex = (selectedSlotIndex + 1) % saveSlots.Length;
            }
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            return;
        }

        for (var index = 0; index < saveSlots.Length; index++)
        {
            if (Contains(mouse, 60, 150 + index * 52, 760, 44))
            {
                selectedSlotIndex = index;
            }
        }

        if (saveSlots.Length == 0)
        {
            return;
        }

        var selected = saveSlots[Math.Clamp(selectedSlotIndex, 0, saveSlots.Length - 1)];
        if (Contains(mouse, 860, 150, 280, 44))
        {
            BeginLoadingSave(selected.Id, ref screen, ref statusMessage);
            return;
        }

        if (Contains(mouse, 860, 210, 280, 44))
        {
            GameSaveStore.Delete(selected.Id);
            saveSlots = GameSaveStore.ListSlots().ToArray();
            selectedSlotIndex = Math.Clamp(selectedSlotIndex, 0, Math.Max(0, saveSlots.Length - 1));
            statusMessage = "Salvataggio eliminato.";
            return;
        }

        if (Contains(mouse, 860, 270, 280, 44))
        {
            if (!GameSaveStore.TryLoad(GameSaveStore.ContinueSlotId, out var continueData))
            {
                statusMessage = "Nessun Continua da duplicare.";
                return;
            }

            var copyId = GameSaveStore.CreateSlotId();
            GameSaveStore.Save(copyId, continueData);
            saveSlots = GameSaveStore.ListSlots().ToArray();
            selectedSlotIndex = Array.FindIndex(saveSlots, slot => slot.Id == copyId);
            if (selectedSlotIndex < 0)
            {
                selectedSlotIndex = 0;
            }

            statusMessage = $"Creato {copyId}.";
        }
    }

    private static void HandleResearchInput(
        GameContent content,
        EconomyWallet wallet,
        ResearchState research,
        EconomySession session,
        ref AppScreen screen,
        ref int selectedResearchIndex,
        ref string? statusMessage)
    {
        var graph = TechTreeLayout.Build(content);
        var entries = ResearchEntries(content);
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left)
                && Contains(Raylib.GetMousePosition(), 28, ScreenHeight - 70, 180, 40)))
        {
            statusMessage = null;
            TechTreePanning = false;
            screen = AppScreen.Playing;
            return;
        }

        if (entries.Count > 0)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Up) || Raylib.IsKeyPressed(KeyboardKey.Left))
            {
                selectedResearchIndex = (selectedResearchIndex + entries.Count - 1) % entries.Count;
            }

            if (Raylib.IsKeyPressed(KeyboardKey.Down) || Raylib.IsKeyPressed(KeyboardKey.Right))
            {
                selectedResearchIndex = (selectedResearchIndex + 1) % entries.Count;
            }
        }

        var mouse = Raylib.GetMousePosition();
        GetTechTreeCanvas(out var canvasX, out var canvasY, out var canvasW, out var canvasH);
        var overCanvas = Contains(mouse, canvasX, canvasY, canvasW, canvasH);

        if (Raylib.IsMouseButtonPressed(MouseButton.Middle)
            || (Raylib.IsMouseButtonPressed(MouseButton.Right) && overCanvas)
            || (Raylib.IsKeyDown(KeyboardKey.LeftShift) && Raylib.IsMouseButtonPressed(MouseButton.Left) && overCanvas))
        {
            TechTreePanning = true;
            TechTreePanAnchor = mouse;
            TechTreePanStartX = TechTreePanX;
            TechTreePanStartY = TechTreePanY;
        }

        if (TechTreePanning)
        {
            if (Raylib.IsMouseButtonDown(MouseButton.Middle)
                || Raylib.IsMouseButtonDown(MouseButton.Right)
                || (Raylib.IsKeyDown(KeyboardKey.LeftShift) && Raylib.IsMouseButtonDown(MouseButton.Left)))
            {
                TechTreePanX = TechTreePanStartX + (mouse.X - TechTreePanAnchor.X);
                TechTreePanY = TechTreePanStartY + (mouse.Y - TechTreePanAnchor.Y);
            }
            else
            {
                TechTreePanning = false;
            }
        }

        if (overCanvas)
        {
            var wheel = Raylib.GetMouseWheelMove();
            if (Math.Abs(wheel) > 0.01f)
            {
                TechTreePanY += wheel * 36f;
            }
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left) || TechTreePanning || entries.Count == 0)
        {
            return;
        }

        if (Raylib.IsKeyDown(KeyboardKey.LeftShift))
        {
            return;
        }

        if (overCanvas)
        {
            for (var index = 0; index < graph.Nodes.Count; index++)
            {
                var node = graph.Nodes[index];
                var nx = (int)(node.X + TechTreePanX);
                var ny = (int)(node.Y + TechTreePanY);
                if (!Contains(mouse, nx, ny, TechTreeLayout.NodeWidth, TechTreeLayout.NodeHeight))
                {
                    continue;
                }

                var entryIndex = -1;
                for (var i = 0; i < entries.Count; i++)
                {
                    if (entries[i].Id == node.Structure.Id)
                    {
                        entryIndex = i;
                        break;
                    }
                }

                if (entryIndex >= 0)
                {
                    selectedResearchIndex = entryIndex;
                }
            }
        }

        selectedResearchIndex = Math.Clamp(selectedResearchIndex, 0, entries.Count - 1);
        var selected = entries[selectedResearchIndex];
        GetTechTreeUnlockButton(out var btnX, out var btnY, out var btnW, out var btnH);
        if (!Contains(mouse, btnX, btnY, btnW, btnH))
        {
            return;
        }

        if (research.IsUnlocked(selected.Id))
        {
            statusMessage = "Già sbloccato.";
            return;
        }

        if (!research.MeetsPrerequisites(selected))
        {
            statusMessage = "Prerequisiti mancanti.";
            return;
        }

        var moneyBefore = wallet.Money;
        if (research.TryUnlock(selected, wallet))
        {
            session.RecordUnlockSpend(moneyBefore - wallet.Money);
            statusMessage = selected.IsStub
                ? $"{selected.DisplayName} sbloccato (stub — non costruibile ancora)."
                : $"{selected.DisplayName} sbloccato!";
        }
        else
        {
            statusMessage = "Risorse insufficienti per sbloccare.";
        }
    }

    private static IReadOnlyList<StructureDefinition> ResearchEntries(GameContent content) =>
        TechTreeLayout.Build(content).Nodes.Select(node => node.Structure).ToList();

    private static void GetTechTreeCanvas(out int x, out int y, out int w, out int h)
    {
        x = 24;
        y = 120;
        GetTechTreeDetailPanel(out var detailX, out _, out _, out _);
        w = Math.Max(280, detailX - x - 16);
        h = Math.Max(200, ScreenHeight - y - 90);
    }

    private static void GetTechTreeDetailPanel(out int x, out int y, out int w, out int h)
    {
        w = Math.Clamp(320, 280, Math.Max(280, ScreenWidth / 3));
        x = ScreenWidth - w - 28;
        y = 132;
        h = Math.Max(220, ScreenHeight - y - 90);
    }

    /// <summary>Unlock confirm button inside the research detail panel (must match DrawResearch).</summary>
    private const int TechTreeUnlockButtonOffsetY = 156;
    private const int TechTreeUnlockButtonHeight = 48;

    private static void GetTechTreeUnlockButton(out int x, out int y, out int w, out int h)
    {
        GetTechTreeDetailPanel(out var detailX, out var detailY, out var detailW, out _);
        x = detailX + 16;
        y = detailY + TechTreeUnlockButtonOffsetY;
        w = detailW - 32;
        h = TechTreeUnlockButtonHeight;
    }

    private static void HandlePlayingInput(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        WorldCamera camera,
        ResearchState research,
        EconomySession session,
        MarketCatalog market,
        EconomyConfig economy,
        BuildingDefinition minerBuilding,
        BuildingDefinition advancedMinerBuilding,
        BuildingDefinition smelterBuilding,
        BuildingDefinition assemblerBuilding,
        BuildingDefinition generatorBuilding,
        BuildingDefinition powerNodeBuilding,
        BuildingDefinition powerNodeT2Building,
        ConveyorDefinition basicConveyor,
        ConveyorDefinition fastConveyor,
        ConveyorDefinition expressConveyor,
        ConveyorDefinition junctionConveyor,
        ConveyorDefinition splitterConveyor,
        ConveyorDefinition sorterConveyor,
        ConveyorDefinition bridgeConveyor,
        RecipeDefinition smeltRecipe,
        RecipeDefinition wireRecipe,
        ref ConveyorDefinition selectedConveyor,
        ref BuildTool tool,
        ref Direction direction,
        ref GridPosition? previousDragPosition,
        ref bool isPanning,
        ref Vector2 panAnchor,
        ref float panCameraX,
        ref float panCameraY,
        ref AppScreen screen,
        ref AppScreen settingsReturnScreen,
        ref int selectedResearchIndex,
        ref string? statusMessage,
        long nextItemId,
        float frameTime)
    {
        if (CampaignLevelComplete && ActiveCampaignLevel is not null)
        {
            if (HandleCampaignVictoryInput(ref screen, ref statusMessage))
            {
                return;
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left)
                && HitHeaderIcon(Raylib.GetMousePosition(), 0)))
        {
            // Esc dismisses a lingering toast before leaving the session.
            if (Raylib.IsKeyPressed(KeyboardKey.Escape) && !string.IsNullOrEmpty(statusMessage))
            {
                ClearStatusToast(ref statusMessage);
                return;
            }

            if (ActiveCampaignLevel is null)
            {
                AutoSaveContinue(world, conveyors, wallet, camera, research, session, nextItemId);
            }

            ClearStatusToast(ref statusMessage);
            screen = ActiveCampaignLevel is not null ? AppScreen.CampaignSelect : AppScreen.Home;
            ActiveCampaignLevel = null;
            CampaignLevelComplete = false;
            return;
        }

        if (TutorialActive && ActiveSettings is not null
            && HandleTutorialInput(ActiveSettings, ref statusMessage))
        {
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.T)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left)
                && HitHeaderIcon(Raylib.GetMousePosition(), 1)))
        {
            selectedResearchIndex = 0;
            TechTreePanX = 0f;
            TechTreePanY = 0f;
            statusMessage = null;
            TutorialOpenedResearch = true;
            screen = AppScreen.Research;
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.I)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left)
                && HitHeaderIcon(Raylib.GetMousePosition(), 2)))
        {
            statusMessage = null;
            settingsReturnScreen = AppScreen.Playing;
            SettingsDraft = null;
            SettingsScrollY = 0f;
            if (TutorialActive)
            {
                TutorialOpenedSettings = true;
            }

            screen = AppScreen.Settings;
            return;
        }

        // Snap camera back to core (H / Home).
        if (Raylib.IsKeyPressed(KeyboardKey.H) || Raylib.IsKeyPressed(KeyboardKey.Home))
        {
            camera.CenterOnTile(
                new GridPosition(world.CoreOrigin.X + 1, world.CoreOrigin.Y + 1),
                BaseTileSize,
                ViewportWidth,
                ViewportHeight);
            camera.ClampToMap(world.Terrain.Width, world.Terrain.Height, BaseTileSize, ViewportWidth, ViewportHeight);
            statusMessage = "Camera sul core.";
        }

        var mouse = Raylib.GetMousePosition();
        var wheel = Raylib.GetMouseWheelMove();
        if (Raylib.IsMouseButtonPressed(MouseButton.Left)
            && TrySelectBuildDock(
                mouse,
                research,
                basicConveyor,
                fastConveyor,
                expressConveyor,
                ref tool,
                ref direction,
                ref selectedConveyor,
                ref statusMessage))
        {
            previousDragPosition = null;
            return;
        }

        GetMercatoBounds(out var mercatoX, out var mercatoY, out var mercatoW, out _);
        if (Raylib.IsMouseButtonPressed(MouseButton.Left)
            && TryHandleMercatoClick(mouse, world, wallet, session, market, mercatoX, mercatoY, mercatoW, ref statusMessage))
        {
            previousDragPosition = null;
            return;
        }

        GetStatusBounds(out var statusX, out var statusY, out var statusW, out _);
        if (Raylib.IsMouseButtonPressed(MouseButton.Left)
            && TryHandleStatusClick(mouse, world, wallet, session, economy, statusX, statusY, statusW, ref statusMessage))
        {
            previousDragPosition = null;
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.U))
        {
            if (world.TryUpgradeCore(wallet, economy.CoreUpgrade, session))
            {
                statusMessage = "Core potenziato: +prezzo vendite!";
            }
        }

        UpdateCamera(camera, world, ref isPanning, ref panAnchor, ref panCameraX, ref panCameraY, frameTime, wheel);

        var ctrlHeld = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);
        if (wheel != 0 && !ctrlHeld && !IsOverHudChrome(mouse))
        {
            // Bare wheel rotates building/belt facing (Mindustry-like).
            var steps = wheel > 0 ? 1 : -1;
            var next = ((int)direction + steps) % 4;
            if (next < 0)
            {
                next += 4;
            }

            direction = (Direction)next;
            if (TutorialActive)
            {
                TutorialRotatedPiece = true;
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.R))
        {
            direction = (Direction)(((int)direction + 1) % 4);
            if (TutorialActive)
            {
                TutorialRotatedPiece = true;
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.One))
        {
            tool = BuildTool.Conveyor;
            SyncDockSelection(tool, selectedConveyor, direction);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Two))
        {
            if (research.IsUnlocked("miner"))
            {
                tool = BuildTool.Miner;
                SyncDockSelection(tool, selectedConveyor, direction);
            }
            else
            {
                statusMessage = "Minatore già sbloccato di default — selezionalo dal dock.";
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Three))
        {
            if (research.IsUnlocked("smelter"))
            {
                tool = BuildTool.Smelter;
                SyncDockSelection(tool, selectedConveyor, direction);
            }
            else
            {
                statusMessage = "Forno bloccato: aprilo in Ricerca (T) prima di piazzarlo.";
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Four))
        {
            tool = BuildTool.Remove;
            SyncDockSelection(tool, selectedConveyor, direction);
            if (TutorialActive)
            {
                TutorialUsedRemove = true;
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Five) && research.IsUnlocked("assembler"))
        {
            tool = BuildTool.Assembler;
            SyncDockSelection(tool, selectedConveyor, direction);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Six) && research.IsUnlocked("junction"))
        {
            tool = BuildTool.Junction;
            SyncDockSelection(tool, selectedConveyor, direction);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Seven) && research.IsUnlocked("splitter"))
        {
            tool = BuildTool.Splitter;
            SyncDockSelection(tool, selectedConveyor, direction);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Zero) && research.IsUnlocked("sorter"))
        {
            tool = BuildTool.Sorter;
            SyncDockSelection(tool, selectedConveyor, direction);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Eight) && research.IsUnlocked("conveyor-bridge"))
        {
            tool = BuildTool.Bridge;
            SyncDockSelection(tool, selectedConveyor, direction);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Nine) && research.IsUnlocked("generator"))
        {
            tool = BuildTool.Generator;
            SyncDockSelection(tool, selectedConveyor, direction);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.F))
        {
            var hover = MouseCell(mouse, camera, world);
            if (hover is { } hoverPos
                && conveyors.Cells.TryGetValue(hoverPos, out var hoverCell)
                && hoverCell.Kind == LogisticsKind.Sorter)
            {
                hoverCell.CycleFilterItem(SorterFilterItemIds);
                SorterBrushFilterId = hoverCell.FilterItemId ?? "iron-ore";
                statusMessage = $"Filtro selezionatore: {UiTheme.ItemDisplayName(SorterBrushFilterId)}";
            }
            else if (tool == BuildTool.Sorter)
            {
                CycleSorterBrushFilter();
                statusMessage = $"Filtro pennello: {UiTheme.ItemDisplayName(SorterBrushFilterId)}";
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Q))
        {
            selectedConveyor = basicConveyor;
            tool = BuildTool.Conveyor;
            SyncDockSelection(tool, selectedConveyor, direction);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.E) && research.IsUnlocked(fastConveyor.Id))
        {
            selectedConveyor = fastConveyor;
            tool = BuildTool.Conveyor;
            SyncDockSelection(tool, selectedConveyor, direction);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Y) && research.IsUnlocked(expressConveyor.Id))
        {
            selectedConveyor = expressConveyor;
            tool = BuildTool.Conveyor;
            SyncDockSelection(tool, selectedConveyor, direction);
        }

        if (isPanning)
        {
            return;
        }

        if (IsOverHudChrome(mouse))
        {
            return;
        }

        if (tool == BuildTool.Conveyor && Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            var cell = MouseCell(mouse, camera, world);
            if (cell is not { } position)
            {
                return;
            }

            if (previousDragPosition is not { } previous)
            {
                if (conveyors.Cells.TryGetValue(position, out var existing))
                {
                    if (selectedConveyor.Tier > existing.Definition.Tier
                        && research.IsUnlocked(selectedConveyor.Id))
                    {
                        conveyors.TryUpgrade(position, selectedConveyor, wallet, research, session);
                    }
                    else
                    {
                        existing.Rotate(direction);
                    }
                }
                else if (world.CanPlaceConveyor(position)
                    && research.IsUnlocked(selectedConveyor.Id))
                {
                    conveyors.TryPlace(
                        position,
                        direction,
                        selectedConveyor,
                        wallet,
                        research,
                        session,
                        world.CanPlaceConveyor);
                    TutorialPlacedBelt = true;
                    ConnectAdjacentMiner(world, conveyors, position);
                    ConnectAdjacentSmelter(world, conveyors, position);
                    ConnectAdjacentAssembler(world, conveyors, position);
                    ConnectToAdjacentCore(world, conveyors, position);
                }

                previousDragPosition = position;
                return;
            }

            ExtendConveyorPath(world, conveyors, wallet, research, session, selectedConveyor, previous, position, ref direction);
            previousDragPosition = position;
            return;
        }

        if (Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            previousDragPosition = null;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            var cell = MouseCell(mouse, camera, world);
            if (cell is not { } position)
            {
                return;
            }

            if (tool == BuildTool.Miner && research.IsUnlocked("miner"))
            {
                if (!world.TryPlaceMiner(position, direction, conveyors, wallet, minerBuilding, session,
                        definitionId: MinerBuilding.BasicId))
                {
                    statusMessage = world.CanPlaceMiner(position, conveyors)
                        ? "Risorse insufficienti per il minatore."
                        : "Minatore: serve terreno libero (no acqua/core/edifici/nastri).";
                }
                else
                {
                    TutorialPlacedMiner = true;
                    var eff = world.Miners[position].Efficiency;
                    statusMessage = eff <= 0f
                        ? "Minatore T1 a 0% — senza giacimento non produce."
                        : $"Minatore T1 piazzato ({eff:P0}) — uscita su tutti i lati.";
                }
            }
            else if (tool == BuildTool.MinerAdvanced && research.IsUnlocked(MinerBuilding.AdvancedId))
            {
                if (!world.TryPlaceMiner(position, direction, conveyors, wallet, advancedMinerBuilding, session,
                        definitionId: MinerBuilding.AdvancedId))
                {
                    statusMessage = world.CanPlaceMiner(position, conveyors)
                        ? "Risorse insufficienti per il Minatore T2."
                        : "Minatore T2: serve terreno libero (no acqua/core/edifici/nastri).";
                }
                else
                {
                    var placed = world.Miners[position];
                    statusMessage = placed.Efficiency <= 0f
                        ? "Minatore T2 a 0% — senza giacimento non produce."
                        : $"Minatore T2 ({placed.Efficiency:P0}, 2×) — uscita multi-lato.";
                }
            }
            else if (tool == BuildTool.MinerAdvanced && !research.IsUnlocked(MinerBuilding.AdvancedId))
            {
                statusMessage = "Minatore T2 bloccato: sbloccalo in Ricerca (T).";
            }
            else if (tool == BuildTool.Smelter && research.IsUnlocked("smelter"))
            {
                if (!world.TryPlaceSmelter(position, direction, smeltRecipe, conveyors, wallet, smelterBuilding, session))
                {
                    statusMessage = world.CanPlaceSmelter(position, conveyors)
                        ? "Risorse insufficienti per il forno."
                        : "Forno: serve un’area 2×2 libera su terra (no acqua/core/edifici/nastri).";
                }
                else
                {
                    statusMessage = "Forno piazzato.";
                }
            }
            else if (tool == BuildTool.Smelter && !research.IsUnlocked("smelter"))
            {
                statusMessage = "Forno bloccato: sbloccalo in Ricerca (T).";
            }
            else if (tool == BuildTool.Assembler && research.IsUnlocked("assembler"))
            {
                if (!world.TryPlaceAssembler(position, direction, wireRecipe, conveyors, wallet, assemblerBuilding, session))
                {
                    statusMessage = world.CanPlaceAssembler(position, conveyors)
                        ? "Risorse insufficienti per l’assemblatore."
                        : "Assemblatore: serve un’area 2×2 libera su terra.";
                }
            }
            else if (tool == BuildTool.Generator && research.IsUnlocked("generator"))
            {
                if (!world.TryPlaceGenerator(position, conveyors, wallet, generatorBuilding, session))
                {
                    statusMessage = world.CanPlaceGenerator(position, conveyors)
                        ? "Risorse insufficienti per il generatore."
                        : "Generatore: serve un’area 2×2 libera su terra.";
                }
                else
                {
                    statusMessage = "Generatore piazzato — collega i nodi e alimentalo con carbone.";
                }
            }
            else if (tool == BuildTool.PowerNode && research.IsUnlocked("power-node"))
            {
                if (!world.TryPlacePowerNode(
                        position, conveyors, wallet, PowerNodeBuilding.Tier1Id, powerNodeBuilding, session))
                {
                    statusMessage = world.CanPlacePowerNode(position, PowerNodeBuilding.Tier1Size, conveyors)
                        ? "Risorse insufficienti per il Nodo T1."
                        : "Nodo T1: tile libera (no nastri/edifici/acqua).";
                }
                else
                {
                    statusMessage = "Nodo T1 piazzato — auto-link entro range 6.";
                }
            }
            else if (tool == BuildTool.PowerNodeT2 && research.IsUnlocked("power-node-t2"))
            {
                if (!world.TryPlacePowerNode(
                        position, conveyors, wallet, PowerNodeBuilding.Tier2Id, powerNodeT2Building, session))
                {
                    statusMessage = world.CanPlacePowerNode(position, PowerNodeBuilding.Tier2Size, conveyors)
                        ? "Risorse insufficienti per il Nodo T2."
                        : "Nodo T2: area 2×2 libera su terra.";
                }
                else
                {
                    statusMessage = "Nodo T2 piazzato — auto-link entro range 10.";
                }
            }
            else if (tool == BuildTool.Junction && research.IsUnlocked("junction"))
            {
                if (conveyors.TryPlace(
                    position,
                    direction,
                    junctionConveyor,
                    wallet,
                    research,
                    session,
                    world.CanPlaceConveyor))
                {
                    ConnectAdjacentMiner(world, conveyors, position);
                    ConnectAdjacentSmelter(world, conveyors, position);
                    ConnectAdjacentAssembler(world, conveyors, position);
                    ConnectToAdjacentCore(world, conveyors, position);
                }
            }
            else if (tool == BuildTool.Splitter && research.IsUnlocked("splitter"))
            {
                if (conveyors.TryPlace(
                    position,
                    direction,
                    splitterConveyor,
                    wallet,
                    research,
                    session,
                    world.CanPlaceConveyor))
                {
                    ConnectAdjacentMiner(world, conveyors, position);
                    ConnectAdjacentSmelter(world, conveyors, position);
                    ConnectAdjacentAssembler(world, conveyors, position);
                    ConnectToAdjacentCore(world, conveyors, position);
                }
            }
            else if (tool == BuildTool.Sorter && research.IsUnlocked("sorter"))
            {
                if (conveyors.TryPlace(
                    position,
                    direction,
                    sorterConveyor,
                    wallet,
                    research,
                    session,
                    world.CanPlaceConveyor))
                {
                    if (conveyors.Cells.TryGetValue(position, out var sorterCell))
                    {
                        sorterCell.SetFilterItem(SorterBrushFilterId);
                    }

                    ConnectAdjacentMiner(world, conveyors, position);
                    ConnectAdjacentSmelter(world, conveyors, position);
                    ConnectAdjacentAssembler(world, conveyors, position);
                    ConnectToAdjacentCore(world, conveyors, position);
                }
            }
            else if (tool == BuildTool.Bridge && research.IsUnlocked("conveyor-bridge"))
            {
                if (conveyors.TryPlace(
                    position,
                    direction,
                    bridgeConveyor,
                    wallet,
                    research,
                    session,
                    world.CanPlaceConveyor))
                {
                    ConnectAdjacentMiner(world, conveyors, position);
                    ConnectAdjacentSmelter(world, conveyors, position);
                    ConnectAdjacentAssembler(world, conveyors, position);
                    ConnectToAdjacentCore(world, conveyors, position);
                    if (conveyors.Cells.TryGetValue(position, out var bridgeCell)
                        && bridgeCell.BridgePartner is { } partner)
                    {
                        ConnectAdjacentMiner(world, conveyors, partner);
                        ConnectAdjacentSmelter(world, conveyors, partner);
                        ConnectAdjacentAssembler(world, conveyors, partner);
                        ConnectToAdjacentCore(world, conveyors, partner);
                    }
                }
            }
            else if (tool == BuildTool.Remove
                && !world.TryRemoveMiner(position, wallet, session: session)
                && !world.TryRemoveSmelter(position, wallet, smelterBuilding, session)
                && !world.TryRemoveAssembler(position, wallet, assemblerBuilding, session)
                && !world.TryRemoveGenerator(position, wallet, generatorBuilding, session)
                && !world.TryRemovePowerNode(position, wallet, session: session))
            {
                conveyors.TryRemove(position, wallet, session);
            }
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            var cell = MouseCell(mouse, camera, world);
            if (cell is { } position
                && !world.TryRemoveMiner(position, wallet, session: session)
                && !world.TryRemoveSmelter(position, wallet, smelterBuilding, session)
                && !world.TryRemoveAssembler(position, wallet, assemblerBuilding, session)
                && !world.TryRemoveGenerator(position, wallet, generatorBuilding, session)
                && !world.TryRemovePowerNode(position, wallet, session: session))
            {
                conveyors.TryRemove(position, wallet, session);
            }
        }
    }

    private static void UpdateCamera(
        WorldCamera camera,
        FactoryWorld world,
        ref bool isPanning,
        ref Vector2 panAnchor,
        ref float panCameraX,
        ref float panCameraY,
        float frameTime,
        float wheel)
    {
        var mouse = Raylib.GetMousePosition();
        // Zoom only with Ctrl + wheel; bare wheel rotates placeables (handled in playing input).
        var ctrlHeld = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);
        if (wheel != 0
            && ctrlHeld
            && mouse.X >= ViewportLeft
            && mouse.X < ViewportRight
            && mouse.Y >= ViewportTop
            && mouse.Y < ViewportBottom
            && !IsOverHudChrome(mouse))
        {
            var factor = wheel > 0 ? 1.12f : 1f / 1.12f;
            camera.ZoomAt(mouse.X, mouse.Y, ViewportLeft, ViewportTop, factor);
        }

        var pan = Vector2.Zero;
        if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up))
        {
            pan.Y -= 1f;
        }

        if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down))
        {
            pan.Y += 1f;
        }

        if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left))
        {
            pan.X -= 1f;
        }

        if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right))
        {
            pan.X += 1f;
        }

        if (pan != Vector2.Zero)
        {
            pan = Vector2.Normalize(pan) * (WorldCamera.PanSpeedPixels / camera.Zoom) * frameTime;
            camera.Pan(pan.X, pan.Y);
            if (TutorialActive)
            {
                TutorialCameraMoved += MathF.Abs(pan.X) + MathF.Abs(pan.Y);
            }
        }

        // Edge pan intentionally disabled — only WASD / middle-drag / Shift+drag move the camera.

        if (Raylib.IsMouseButtonPressed(MouseButton.Middle)
            || (Raylib.IsKeyDown(KeyboardKey.LeftShift) && Raylib.IsMouseButtonPressed(MouseButton.Left)))
        {
            isPanning = true;
            panAnchor = mouse;
            panCameraX = camera.X;
            panCameraY = camera.Y;
        }

        if (isPanning
            && (Raylib.IsMouseButtonDown(MouseButton.Middle)
                || (Raylib.IsKeyDown(KeyboardKey.LeftShift) && Raylib.IsMouseButtonDown(MouseButton.Left))))
        {
            var delta = mouse - panAnchor;
            camera.X = panCameraX - delta.X / camera.Zoom;
            camera.Y = panCameraY - delta.Y / camera.Zoom;
            if (TutorialActive)
            {
                TutorialCameraMoved += MathF.Abs(delta.X) + MathF.Abs(delta.Y);
            }
        }
        else
        {
            isPanning = false;
        }

        camera.ClampToMap(world.Terrain.Width, world.Terrain.Height, BaseTileSize, ViewportWidth, ViewportHeight);
    }

    private static void ExtendConveyorPath(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        ResearchState research,
        EconomySession session,
        ConveyorDefinition definition,
        GridPosition from,
        GridPosition destination,
        ref Direction selectedDirection)
    {
        var cursor = from;
        while (cursor != destination)
        {
            var deltaX = destination.X - cursor.X;
            var deltaY = destination.Y - cursor.Y;
            var stepDirection = Math.Abs(deltaX) >= Math.Abs(deltaY)
                ? deltaX > 0 ? Direction.East : Direction.West
                : deltaY > 0 ? Direction.South : Direction.North;
            var next = cursor.Step(stepDirection);

            conveyors.TryOrientToward(cursor, next);

            if (world.CoreTiles.Contains(next))
            {
                selectedDirection = stepDirection;
                return;
            }

            if (!conveyors.Cells.ContainsKey(next))
            {
                if (!world.CanPlaceConveyor(next)
                    || !conveyors.TryPlace(
                        next,
                        stepDirection,
                        definition,
                        wallet,
                        research,
                        session,
                        world.CanPlaceConveyor))
                {
                    return;
                }

                TutorialPlacedBelt = true;
                ConnectAdjacentMiner(world, conveyors, next);
                ConnectAdjacentSmelter(world, conveyors, next);
                ConnectAdjacentAssembler(world, conveyors, next);
            }

            cursor = next;
            selectedDirection = stepDirection;
        }

        ConnectToAdjacentCore(world, conveyors, cursor);
    }

    private static void ConnectAdjacentMiner(
        FactoryWorld world,
        ConveyorGrid conveyors,
        GridPosition conveyorPosition)
    {
        foreach (var direction in Directions)
        {
            var neighbor = conveyorPosition.Step(direction);
            if (world.IsMinerTile(neighbor)
                && ConveyorGrid.TryDirectionBetween(neighbor, conveyorPosition, out var outputDirection))
            {
                conveyors.TryOrientToward(conveyorPosition, conveyorPosition.Step(outputDirection));
            }
        }
    }

    private static void ConnectAdjacentSmelter(
        FactoryWorld world,
        ConveyorGrid conveyors,
        GridPosition conveyorPosition)
    {
        foreach (var direction in Directions)
        {
            var neighbor = conveyorPosition.Step(direction);
            if (!world.TryGetSmelterAt(neighbor, out var smelter)
                || !conveyors.Cells.TryGetValue(conveyorPosition, out var cell))
            {
                continue;
            }

            if (!BuildingIo.TryTravelOut(conveyorPosition, smelter.Position, SmelterBuilding.Size, out var away))
            {
                continue;
            }

            // Keep intentional inputs (facing into the footprint); otherwise make outward output.
            var toward = DirectionMath.Opposite(away);
            if (cell.Direction != toward)
            {
                conveyors.TryOrientToward(conveyorPosition, conveyorPosition.Step(away));
            }
        }
    }

    private static void ConnectAdjacentAssembler(
        FactoryWorld world,
        ConveyorGrid conveyors,
        GridPosition conveyorPosition)
    {
        foreach (var direction in Directions)
        {
            var neighbor = conveyorPosition.Step(direction);
            if (!world.TryGetAssemblerAt(neighbor, out var assembler)
                || !conveyors.Cells.TryGetValue(conveyorPosition, out var cell))
            {
                continue;
            }

            if (!BuildingIo.TryTravelOut(conveyorPosition, assembler.Position, SmelterBuilding.Size, out var away))
            {
                continue;
            }

            var toward = DirectionMath.Opposite(away);
            if (cell.Direction != toward)
            {
                conveyors.TryOrientToward(conveyorPosition, conveyorPosition.Step(away));
            }
        }
    }

    private static Direction Opposite(Direction direction) => direction switch
    {
        Direction.North => Direction.South,
        Direction.East => Direction.West,
        Direction.South => Direction.North,
        Direction.West => Direction.East,
        _ => direction
    };

    private static void ConnectToAdjacentCore(
        FactoryWorld world,
        ConveyorGrid conveyors,
        GridPosition conveyorPosition)
    {
        foreach (var direction in Directions)
        {
            var neighbor = conveyorPosition.Step(direction);
            if (world.CoreTiles.Contains(neighbor))
            {
                conveyors.TryOrientToward(conveyorPosition, neighbor);
                return;
            }
        }
    }

    private static void SyncDockSelection(BuildTool tool, ConveyorDefinition selectedConveyor, Direction direction)
    {
        switch (tool)
        {
            case BuildTool.Miner:
                DockCategory = UiTheme.BuildCategory.Production;
                DockSelectedId = "miner";
                break;
            case BuildTool.MinerAdvanced:
                DockCategory = UiTheme.BuildCategory.Production;
                DockSelectedId = "miner-advanced";
                break;
            case BuildTool.Smelter:
                DockCategory = UiTheme.BuildCategory.Production;
                DockSelectedId = "smelter";
                break;
            case BuildTool.Assembler:
                DockCategory = UiTheme.BuildCategory.Production;
                DockSelectedId = "assembler";
                break;
            case BuildTool.Conveyor:
                DockCategory = UiTheme.BuildCategory.Logistics;
                DockSelectedId = selectedConveyor.Id;
                break;
            case BuildTool.Junction:
                DockCategory = UiTheme.BuildCategory.Logistics;
                DockSelectedId = "junction";
                break;
            case BuildTool.Splitter:
                DockCategory = UiTheme.BuildCategory.Logistics;
                DockSelectedId = "splitter";
                break;
            case BuildTool.Sorter:
                DockCategory = UiTheme.BuildCategory.Logistics;
                DockSelectedId = "sorter";
                break;
            case BuildTool.Bridge:
                DockCategory = UiTheme.BuildCategory.Logistics;
                DockSelectedId = "bridge";
                break;
            case BuildTool.Generator:
                DockCategory = UiTheme.BuildCategory.Power;
                DockSelectedId = "generator";
                break;
            case BuildTool.PowerNode:
                DockCategory = UiTheme.BuildCategory.Power;
                DockSelectedId = "power-node";
                break;
            case BuildTool.PowerNodeT2:
                DockCategory = UiTheme.BuildCategory.Power;
                DockSelectedId = "power-node-t2";
                break;
            case BuildTool.Remove:
                DockCategory = UiTheme.BuildCategory.Production;
                DockSelectedId = "remove";
                break;
            default:
                break;
        }

        _ = direction;
    }

    private static void GetDockPanels(
        out int dockX,
        out int dockY,
        out int gridW,
        out int gridH,
        out int railX,
        out int railW,
        out int railH)
    {
        var entries = UiTheme.EntriesFor(DockCategory);
        gridW = UiTheme.DockGridWidth(entries.Length);
        gridH = UiTheme.DockGridHeight(entries.Length);
        railW = UiTheme.DockRailWidth;
        railH = UiTheme.DockPadding * 2
            + UiTheme.BuildCategories.Length * UiTheme.DockCellSize
            + (UiTheme.BuildCategories.Length - 1) * UiTheme.DockCellGap;
        var width = gridW + UiTheme.DockCellGap + railW;
        var height = Math.Max(gridH, railH) + UiTheme.DockHoverBarHeight;
        dockX = ScreenWidth - width - UiTheme.DockMargin;
        dockY = ScreenHeight - height - UiTheme.DockMargin;
        railX = dockX + gridW + UiTheme.DockCellGap;
    }

    private static void GetDockBounds(out int x, out int y, out int width, out int height)
    {
        GetDockPanels(out x, out y, out var gridW, out var gridH, out _, out var railW, out var railH);
        width = gridW + UiTheme.DockCellGap + railW;
        height = Math.Max(gridH, railH) + UiTheme.DockHoverBarHeight;
    }

    private static void GetMercatoBounds(out int x, out int y, out int width, out int height)
    {
        // Soft-scale Mercato so 150–200% stays readable without eating the dock.
        width = MercatoS(InfoPanelWidthBase);
        x = ScreenWidth - width - UiTheme.DockMargin;
        y = ViewportTop + 8;

        var desired = MercatoS(24) + MercatoS(26) + MercatoS(16) + 4 * MercatoS(MercatoRowHeightBase) + MercatoS(10);

        GetDockBounds(out _, out var dockY, out _, out _);
        var gap = UiTheme.S(8);
        // Reserve Fabbrica content + the two S(6) gaps (Mercato→Fabbrica, Fabbrica→dock).
        var minStatus = MercatoS(StatusPanelMinHeightBase) + UiTheme.S(12);
        // Prefer leaving a Fabbrica strip; if not enough room, Mercato takes space above the dock.
        var withStatus = dockY - y - gap - minStatus;
        var withoutStatus = dockY - y - gap;
        var available = withStatus >= MercatoS(140) ? withStatus : withoutStatus;
        height = Math.Min(desired, Math.Max(0, available));
    }

    private static void GetStatusBounds(out int x, out int y, out int width, out int height)
    {
        GetMercatoBounds(out x, out var mercatoY, out width, out var mercatoH);
        y = mercatoY + mercatoH + UiTheme.S(6);

        GetDockBounds(out _, out var dockY, out _, out _);
        var maxBottom = dockY - UiTheme.S(6);
        height = Math.Min(MercatoS(StatusPanelHeightBase), Math.Max(0, maxBottom - y));
        if (height < MercatoS(StatusPanelMinHeightBase))
        {
            height = 0;
        }
    }

    /// <summary>Live Fabbrica building counts shown under PWR.</summary>
    internal static string FormatFabbricaCounts(FactoryWorld world, ConveyorGrid conveyors) =>
        $"M{world.Miners.Count} F{world.Smelters.Count} A{world.Assemblers.Count} N{conveyors.Cells.Count} G{world.Generators.Count} P{world.PowerNodes.Count}";

    /// <summary>
    /// Shared Fabbrica content metrics so draw + CORE hit-test stay aligned.
    /// Returns false when the panel is hidden.
    /// </summary>
    internal static bool TryGetFabbricaContentMetrics(
        out int panelX,
        out int panelY,
        out int panelW,
        out int panelH,
        out int pad,
        out int countsY,
        out int upgradeX,
        out int upgradeY,
        out int upgradeW,
        out int upgradeH)
    {
        GetStatusBounds(out panelX, out panelY, out panelW, out panelH);
        pad = MercatoS(MercatoPadBase);
        countsY = panelY + MercatoS(40);
        upgradeH = MercatoS(28);
        upgradeW = Math.Max(0, panelW - pad * 2);
        upgradeX = panelX + pad;
        // Anchor to bottom, but never cover the live M/F/A/N/G line (default 125% was crushing this).
        var bottomAnchored = panelY + panelH - MercatoS(34);
        var belowCounts = countsY + MercatoS(16);
        upgradeY = Math.Max(bottomAnchored, belowCounts);
        if (panelH <= 0 || upgradeY + upgradeH > panelY + panelH + 1)
        {
            // Keep the button inside the panel when height is tight.
            upgradeY = Math.Max(panelY + MercatoS(52), panelY + panelH - upgradeH - MercatoS(4));
        }

        return panelH > 0;
    }

    /// <summary>
    /// Mercato/Fabbrica soft scale — caps growth so HUD chrome remains usable at 150–200%.
    /// </summary>
    private static int MercatoS(int px)
    {
        var soft = Math.Min(UiTheme.Scale, 1.35f);
        return Math.Max(1, (int)MathF.Round(px * soft));
    }

    private static int MercatoAutoSellY(int panelY) => panelY + MercatoS(24);

    private static int MercatoHintY(int panelY) => panelY + MercatoS(48);

    private static int MercatoHeaderBlock() => MercatoS(66);

    private static int MercatoRowHeight(int panelH)
    {
        var avail = Math.Max(4, panelH - MercatoHeaderBlock() - MercatoS(4));
        var fit = Math.Max(1, avail / 4);
        return Math.Min(MercatoS(MercatoRowHeightBase), fit);
    }

    private static int MercatoRowY(int panelY, int panelH, int index) =>
        panelY + MercatoHeaderBlock() + index * MercatoRowHeight(panelH);

    private static bool IsOverHudChrome(Vector2 mouse)
    {
        if (mouse.Y < HeaderHeight)
        {
            return true;
        }

        // Only the drawn grid panel OR category rail block the map (not empty L-shaped space).
        GetDockPanels(out var dockX, out var dockY, out var gridW, out var gridH, out var railX, out var railW, out var railH);
        if (Contains(mouse, dockX, dockY, gridW, gridH)
            || Contains(mouse, railX, dockY, railW, railH)
            || Contains(
                mouse,
                dockX,
                dockY + Math.Max(gridH, railH),
                gridW + UiTheme.DockCellGap + railW,
                UiTheme.DockHoverBarHeight))
        {
            return true;
        }

        GetMercatoBounds(out var mx, out var my, out var mw, out var mh);
        if (Contains(mouse, mx, my, mw, mh))
        {
            return true;
        }

        GetStatusBounds(out var sx, out var sy, out var sw, out var sh);
        if (Contains(mouse, sx, sy, sw, sh))
        {
            return true;
        }

        if (TutorialActive)
        {
            GetTutorialPanelBounds(out var tx, out var ty, out var tw, out var th);
            if (Contains(mouse, tx, ty, tw, th))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TrySelectBuildDock(
        Vector2 mouse,
        ResearchState research,
        ConveyorDefinition basicConveyor,
        ConveyorDefinition fastConveyor,
        ConveyorDefinition expressConveyor,
        ref BuildTool tool,
        ref Direction direction,
        ref ConveyorDefinition selectedConveyor,
        ref string? statusMessage)
    {
        GetDockPanels(out var dockX, out var dockY, out var gridW, out var gridH, out var railX, out var railW, out var railH);
        var onGrid = Contains(mouse, dockX, dockY, gridW, gridH);
        var onRail = Contains(mouse, railX, dockY, railW, railH);
        if (!onGrid && !onRail)
        {
            return false;
        }

        var entries = UiTheme.EntriesFor(DockCategory);
        var railY = dockY + UiTheme.DockPadding;

        if (onRail)
        {
            for (var i = 0; i < UiTheme.BuildCategories.Length; i++)
            {
                var cy = railY + i * (UiTheme.DockCellSize + UiTheme.DockCellGap);
                if (!Contains(mouse, railX + UiTheme.DockPadding, cy, UiTheme.DockCellSize, UiTheme.DockCellSize))
                {
                    continue;
                }

                DockCategory = UiTheme.BuildCategories[i];
                if (TutorialActive && DockCategory != TutorialDockBaseline)
                {
                    TutorialSwitchedDockCategory = true;
                    TutorialOpenedDock = true;
                }

                var first = UiTheme.EntriesFor(DockCategory).FirstOrDefault();
                if (first is not null
                    && (DockSelectedId is null
                        || UiTheme.EntriesFor(DockCategory).All(e => e.Id != DockSelectedId)))
                {
                    ApplyDockEntry(first, research, basicConveyor, fastConveyor, expressConveyor,
                        ref tool, ref direction, ref selectedConveyor, ref statusMessage);
                }

                return true;
            }

            return true;
        }

        var gridX = dockX + UiTheme.DockPadding;
        var gridY = dockY + UiTheme.DockPadding;
        for (var i = 0; i < entries.Length; i++)
        {
            var col = i % UiTheme.DockGridCols;
            var row = i / UiTheme.DockGridCols;
            var cx = gridX + col * (UiTheme.DockCellSize + UiTheme.DockCellGap);
            var cy = gridY + row * (UiTheme.DockCellSize + UiTheme.DockCellGap);
            if (!Contains(mouse, cx, cy, UiTheme.DockCellSize, UiTheme.DockCellSize))
            {
                continue;
            }

            ApplyDockEntry(entries[i], research, basicConveyor, fastConveyor, expressConveyor,
                ref tool, ref direction, ref selectedConveyor, ref statusMessage);
            return true;
        }

        return true;
    }

    private static void ApplyDockEntry(
        UiTheme.DockEntry entry,
        ResearchState research,
        ConveyorDefinition basicConveyor,
        ConveyorDefinition fastConveyor,
        ConveyorDefinition expressConveyor,
        ref BuildTool tool,
        ref Direction direction,
        ref ConveyorDefinition selectedConveyor,
        ref string? statusMessage)
    {
        if (entry.ResearchId is not null && !research.IsUnlocked(entry.ResearchId))
        {
            // Do NOT change DockSelectedId / tool — that left Forno highlighted while still
            // placing belts, which felt like "non posso piazzare il forno".
            statusMessage = $"Sblocca {entry.Label} in Ricerca (T) prima di selezionarlo.";
            return;
        }

        DockSelectedId = entry.Id;
        switch (entry.Kind)
        {
            case UiTheme.DockEntryKind.BuildTool when entry.Tool is { } buildTool:
                tool = buildTool;
                if (TutorialActive && buildTool == BuildTool.Remove)
                {
                    TutorialUsedRemove = true;
                }

                break;
            case UiTheme.DockEntryKind.ConveyorVariant:
                tool = BuildTool.Conveyor;
                selectedConveyor = entry.ConveyorId switch
                {
                    _ when entry.ConveyorId == expressConveyor.Id => expressConveyor,
                    _ when entry.ConveyorId == fastConveyor.Id => fastConveyor,
                    _ => basicConveyor
                };
                break;
            case UiTheme.DockEntryKind.Direction when entry.Facing is { } facing:
                direction = facing;
                break;
            case UiTheme.DockEntryKind.InventoryItem:
                // Inventory cells are informational; keep current build tool.
                break;
        }
    }

    private const int HomeButtonX = 420;
    private const int HomeButtonWidth = 400;
    private const int HomeButtonHeight = 48;

    private static int HomeButtonY(int index) => 200 + index * 56;

    private enum HomeAction
    {
        Continue,
        Campaign,
        NewGame,
        SaveManager,
        Settings,
        Quit
    }

    private static bool HasValidContinueSlot() =>
        GameSaveStore.TryLoad(GameSaveStore.ContinueSlotId, out _);

    private static HomeAction[] GetHomeActions() =>
        HasValidContinueSlot() ? HomeActionsWithContinue : HomeActionsFresh;

    private static string HomeActionLabel(HomeAction action) => action switch
    {
        HomeAction.Continue => "Continua",
        HomeAction.Campaign => "Campagna",
        HomeAction.NewGame => "Nuova partita",
        HomeAction.SaveManager => "Gestione salvataggi",
        HomeAction.Settings => "Impostazioni",
        HomeAction.Quit => "Esci",
        _ => "?"
    };

    private static void DrawHome(string? statusMessage)
    {
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color(14, 18, 18, 255));
        Raylib.DrawRectangleGradientV(0, 0, ScreenWidth, ScreenHeight,
            new Color(18, 28, 24, 255), new Color(10, 12, 12, 255));
        DrawUiText("tINDUSTRY", 420, 100, 48, new Color(239, 238, 224, 255));
        DrawUiText("Settore Foundry — mappa 1000×1000", 420, 160, 18, new Color(112, 124, 119, 255));

        var actions = GetHomeActions();
        for (var i = 0; i < actions.Length; i++)
        {
            DrawMenuButton(HomeButtonX, HomeButtonY(i), HomeButtonWidth, HomeButtonHeight,
                HomeActionLabel(actions[i]));
        }

        if (!string.IsNullOrEmpty(statusMessage))
        {
            DrawUiText(statusMessage, 420, ScreenHeight - 120, 18, new Color(225, 140, 110, 255));
        }

        DrawUiText("WASD / Shift+drag / rotella centrale: pan   ·   Ctrl+rotella: zoom   ·   H/Home: core   ·   T: ricerca   ·   I: impostazioni   ·   Esc: menu",
            80, ScreenHeight - 40, 15, new Color(90, 100, 96, 255));
    }

    private static void HandleCampaignSelectInput(ref AppScreen screen, ref string? statusMessage)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left)
                && Contains(Raylib.GetMousePosition(), 28, ScreenHeight - 70, 180, 40)))
        {
            statusMessage = null;
            screen = AppScreen.Home;
            return;
        }

        var catalog = Campaign ?? CampaignCatalog.Load();
        Campaign ??= catalog;
        CampaignProgressState ??= CampaignProgress.Load();

        var wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0)
        {
            CampaignSelectScroll = Math.Clamp(CampaignSelectScroll - (int)(wheel * 48), 0, 2000);
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            return;
        }

        var mouse = Raylib.GetMousePosition();
        var cardW = 340;
        var cardH = 168;
        var cols = 2;
        var startX = 80;
        var startY = 160 - CampaignSelectScroll;
        for (var i = 0; i < catalog.Levels.Count; i++)
        {
            var level = catalog.Levels[i];
            var col = i % cols;
            var row = i / cols;
            var x = startX + col * (cardW + 24);
            var y = startY + row * (cardH + 20);
            if (y + cardH < 120 || y > ScreenHeight - 90)
            {
                continue;
            }

            if (!Contains(mouse, x, y, cardW, cardH))
            {
                continue;
            }

            var unlocked = CampaignProgressState.IsUnlocked(level, catalog);
            if (!unlocked)
            {
                statusMessage = "Livello bloccato. Completa il precedente.";
                return;
            }

            BeginLoadingCampaignLevel(level, ref screen, ref statusMessage);
            return;
        }
    }

    private static void DrawCampaignSelect(string? statusMessage)
    {
        var catalog = Campaign ?? CampaignCatalog.Load();
        Campaign ??= catalog;
        CampaignProgressState ??= CampaignProgress.Load();

        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color(14, 18, 18, 255));
        Raylib.DrawRectangleGradientV(0, 0, ScreenWidth, ScreenHeight,
            new Color(18, 28, 24, 255), new Color(10, 12, 12, 255));
        DrawUiText("Campagna", 80, 70, 36, new Color(239, 238, 224, 255));
        DrawUiText("Completa un livello per sbloccare il successivo.", 80, 118, 18,
            new Color(112, 124, 119, 255));

        var cardW = 340;
        var cardH = 168;
        var cols = 2;
        var startX = 80;
        var startY = 160 - CampaignSelectScroll;
        for (var i = 0; i < catalog.Levels.Count; i++)
        {
            var level = catalog.Levels[i];
            var col = i % cols;
            var row = i / cols;
            var x = startX + col * (cardW + 24);
            var y = startY + row * (cardH + 20);
            if (y + cardH < 100 || y > ScreenHeight - 80)
            {
                continue;
            }

            var unlocked = CampaignProgressState.IsUnlocked(level, catalog);
            var completed = CampaignProgressState.IsCompleted(level.Id);
            var fill = unlocked
                ? new Color(32, 40, 36, 255)
                : new Color(22, 24, 24, 255);
            var border = completed
                ? new Color(120, 228, 150, 200)
                : unlocked
                    ? UiTheme.Accent
                    : new Color(50, 56, 52, 255);
            Raylib.DrawRectangle(x, y, cardW, cardH, fill);
            Raylib.DrawRectangleLines(x, y, cardW, cardH, border);

            var titleColor = unlocked ? new Color(239, 238, 224, 255) : new Color(90, 96, 92, 255);
            var bodyColor = unlocked ? new Color(164, 173, 168, 255) : new Color(70, 76, 72, 255);
            var indexLabel = $"{i + 1}. {level.Name}";
            DrawUiText(indexLabel, x + 14, y + 12, 20, titleColor);
            if (completed)
            {
                DrawUiText("Completato", x + cardW - 110, y + 14, 14, new Color(120, 228, 150, 255));
            }
            else if (!unlocked)
            {
                DrawUiText("Bloccato", x + cardW - 90, y + 14, 14, new Color(120, 110, 100, 255));
            }

            DrawWrappedTip(level.Description, x + 14, y + 44, cardW - 28, 48);
            DrawUiText(catalog.ObjectiveSummary(level), x + 14, y + 100, 13, bodyColor);
            DrawUiText($"{level.MapWidth}×{level.MapHeight} · seed {level.Seed}", x + 14, y + 140, 12,
                unlocked ? new Color(112, 124, 119, 255) : new Color(60, 64, 62, 255));
        }

        DrawMenuButton(28, ScreenHeight - 70, 180, 40, "Indietro");
        if (!string.IsNullOrEmpty(statusMessage))
        {
            DrawUiText(statusMessage, 230, ScreenHeight - 58, 18, new Color(225, 140, 110, 255));
        }
    }

    private static void DrawNewGame(int pendingSeed, string? statusMessage)
    {
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color(14, 18, 18, 255));
        Raylib.DrawRectangleGradientV(0, 0, ScreenWidth, ScreenHeight,
            new Color(18, 28, 24, 255), new Color(10, 12, 12, 255));
        DrawUiText("Nuova partita", 120, 80, 36, new Color(239, 238, 224, 255));
        DrawUiText("Scegli uno scenario o regola il seed, poi conferma.", 120, 130, 18,
            new Color(112, 124, 119, 255));

        DrawMenuButton(120, 220, 280, 48, "Classico (7429)");
        DrawMenuButton(420, 220, 280, 48, "Espanso (1337)");
        DrawMenuButton(720, 220, 280, 48, "Arcipelago (9001)");

        DrawUiText("Seed personalizzato", 420, 290, 18, new Color(164, 173, 168, 255));
        DrawMenuButton(420, 320, 48, 48, "−");
        Raylib.DrawRectangle(480, 320, 128, 48, new Color(32, 38, 36, 255));
        Raylib.DrawRectangleLines(480, 320, 128, 48, new Color(70, 82, 76, 255));
        var seedLabel = pendingSeed.ToString();
        var seedWidth = MeasureUiText(seedLabel, 24);
        DrawUiText(seedLabel, 480 + (128 - seedWidth) / 2, 332, 24, new Color(232, 233, 221, 255));
        DrawMenuButton(620, 320, 48, 48, "+");
        DrawUiText("←/→ o +/− · cifre opzionali · Backspace", 420, 380, 14,
            new Color(126, 137, 132, 255));

        DrawMenuButton(420, 420, 280, 52, "Conferma");
        DrawMenuButton(28, ScreenHeight - 70, 180, 40, "Indietro");

        if (!string.IsNullOrEmpty(statusMessage))
        {
            DrawUiText(statusMessage, 230, ScreenHeight - 58, 18, new Color(225, 140, 110, 255));
        }
    }

    private static void HandleSettingsInput(
        GameSettings settings,
        GameSettings draft,
        ref AppScreen screen,
        AppScreen returnScreen,
        ref string? statusMessage)
    {
        var wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0)
        {
            SettingsScrollY = Math.Clamp(SettingsScrollY - wheel * 40f, 0f, 4000f);
        }

        var layout = BuildSettingsLayout((int)SettingsScrollY);
        // Re-clamp scroll against actual content.
        SettingsScrollY = Math.Max(0f, SettingsScrollY);
        layout = BuildSettingsLayout((int)SettingsScrollY);

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left)
                && Contains(Raylib.GetMousePosition(), 28, ScreenHeight - 70, 180, 40)))
        {
            // Discard unapplied display draft; overlay toggles already saved.
            SettingsDraft = null;
            statusMessage = null;
            screen = returnScreen;
            return;
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            return;
        }

        var mouse = Raylib.GetMousePosition();
        if (Contains(mouse, layout.Left, layout.FpsToggleY, layout.ToggleWidth, layout.ToggleHeight))
        {
            settings.ShowFps = !settings.ShowFps;
            draft.ShowFps = settings.ShowFps;
            settings.Save();
            statusMessage = settings.ShowFps ? "Contatore FPS attivato." : "Contatore FPS disattivato.";
            return;
        }

        if (Contains(mouse, layout.Left, layout.OverlayToggleY, layout.ToggleWidth, layout.ToggleHeight))
        {
            settings.ShowResourceOverlay = !settings.ShowResourceOverlay;
            draft.ShowResourceOverlay = settings.ShowResourceOverlay;
            settings.Save();
            statusMessage = settings.ShowResourceOverlay
                ? "Overlay risorse sistema attivato (CPU · GPU · RAM)."
                : "Overlay risorse sistema disattivato.";
            return;
        }

        if (Contains(mouse, layout.Left, layout.VsyncToggleY, layout.ToggleWidth, layout.ToggleHeight))
        {
            draft.VSync = !draft.VSync;
            statusMessage = draft.VSync
                ? "VSync: ON (limita al refresh; preferenza FPS salvata)."
                : "VSync: OFF (usa il limite FPS).";
            return;
        }

        if (Contains(mouse, layout.Left, layout.AutoSellToggleY, layout.ToggleWidth, layout.ToggleHeight))
        {
            settings.AutoSellAtCore = !settings.AutoSellAtCore;
            draft.AutoSellAtCore = settings.AutoSellAtCore;
            settings.Save();
            statusMessage = settings.AutoSellAtCore
                ? "Vendita automatica ON: il core liquida in $."
                : "Vendita automatica OFF: stock in magazzino; vendi dal Mercato.";
            return;
        }

        if (Contains(mouse, layout.Left, layout.ReviewTutorialY, layout.ReviewTutorialWidth, layout.ButtonHeight))
        {
            RestartTutorial(settings);
            draft.TutorialCompleted = false;
            statusMessage = returnScreen == AppScreen.Playing
                ? "Tutorial ripartito. Torna in gioco per continuare."
                : "Tutorial ripristinato. Conferma Nuova partita per rivederlo.";
            return;
        }

        // UI scale — apply immediately for crisp font reload
        for (var i = 0; i < GameSettings.UiScalePresets.Length; i++)
        {
            var x = layout.Left + i * layout.ScaleButtonStride;
            if (!Contains(mouse, x, layout.ScaleButtonsY, layout.ScaleButtonWidth, layout.ButtonHeight))
            {
                continue;
            }

            var percent = GameSettings.UiScalePresets[i];
            draft.UiScalePercent = percent;
            settings.UiScalePercent = percent;
            settings.Save();
            UiTheme.ApplyScalePercent(percent);
            statusMessage = $"Scala UI: {GameSettings.UiScaleLabel(percent)}";
            return;
        }

        // Auto resolution
        if (Contains(mouse, layout.Left, layout.AutoResY, layout.AutoResWidth, layout.ButtonHeight))
        {
            draft.UseAutoResolution = true;
            DisplayApplier.CaptureDesktopResolution(draft);
            statusMessage = $"Auto risoluzione: {draft.ResolutionWidth}×{draft.ResolutionHeight}";
            return;
        }

        // Resolution presets
        for (var i = 0; i < GameSettings.ResolutionPresets.Length; i++)
        {
            var x = layout.Left + (i % 4) * layout.GridStride;
            var y = layout.ResGridY + (i / 4) * layout.GridRowStride;
            if (!Contains(mouse, x, y, layout.GridButtonWidth, layout.ButtonHeight))
            {
                continue;
            }

            draft.UseAutoResolution = false;
            draft.ResolutionWidth = GameSettings.ResolutionPresets[i].Width;
            draft.ResolutionHeight = GameSettings.ResolutionPresets[i].Height;
            statusMessage = $"Risoluzione: {GameSettings.ResolutionPresets[i].Label}";
            return;
        }

        // FPS limiter
        for (var i = 0; i < GameSettings.FpsLimitPresets.Length; i++)
        {
            var x = layout.Left + (i % 4) * layout.GridStride;
            var y = layout.FpsGridY + (i / 4) * layout.GridRowStride;
            if (!Contains(mouse, x, y, layout.GridButtonWidth, layout.ButtonHeight))
            {
                continue;
            }

            draft.TargetFps = GameSettings.FpsLimitPresets[i];
            statusMessage = draft.VSync
                ? $"Limite FPS salvato: {GameSettings.FpsLimitLabel(draft.TargetFps)} (VSync attivo)."
                : $"Limite FPS: {GameSettings.FpsLimitLabel(draft.TargetFps)}";
            return;
        }

        // Display mode
        DisplayMode[] modes = [DisplayMode.Windowed, DisplayMode.Borderless, DisplayMode.Fullscreen];
        for (var i = 0; i < modes.Length; i++)
        {
            if (!Contains(mouse, layout.Left + i * layout.ModeStride, layout.ModeButtonsY, layout.ModeButtonWidth, layout.ButtonHeight))
            {
                continue;
            }

            draft.DisplayMode = modes[i];
            statusMessage = $"Modalità: {GameSettings.DisplayModeLabel(modes[i])}";
            return;
        }

        // Apply
        if (Contains(mouse, layout.Left, layout.ApplyY, layout.ApplyWidth, layout.ApplyHeight))
        {
            settings.CopyFrom(draft);
            settings.Save();
            DisplayApplier.Apply(settings);
            UiTheme.ApplyScalePercent(settings.UiScalePercent);
            SyncLayoutSize(settings);
            statusMessage = "Grafica applicata e salvata.";
            return;
        }

        // Revert draft to last applied
        if (Contains(mouse, layout.Left + layout.ApplyWidth + layout.ApplyGap, layout.ApplyY, layout.ApplyWidth, layout.ApplyHeight))
        {
            draft.CopyFrom(settings);
            statusMessage = "Selezione grafica ripristinata.";
        }
    }

    /// <summary>
    /// Shared Impostazioni geometry — scales with UI so rows never stack on top of each other.
    /// </summary>
    private readonly struct SettingsPanelLayout
    {
        public int Left { get; init; }
        public int TitleY { get; init; }
        public int SubtitleY { get; init; }
        public int ToggleWidth { get; init; }
        public int ToggleHeight { get; init; }
        public int FpsToggleY { get; init; }
        public int OverlayToggleY { get; init; }
        public int VsyncToggleY { get; init; }
        public int AutoSellToggleY { get; init; }
        public int ReviewTutorialY { get; init; }
        public int ReviewTutorialWidth { get; init; }
        public int ScaleLabelY { get; init; }
        public int ScaleButtonsY { get; init; }
        public int ScaleButtonWidth { get; init; }
        public int ScaleButtonStride { get; init; }
        public int ButtonHeight { get; init; }
        public int ResLabelY { get; init; }
        public int AutoResY { get; init; }
        public int AutoResWidth { get; init; }
        public int ResGridY { get; init; }
        public int FpsLabelY { get; init; }
        public int FpsGridY { get; init; }
        public int ModeLabelY { get; init; }
        public int ModeButtonsY { get; init; }
        public int ModeButtonWidth { get; init; }
        public int ModeStride { get; init; }
        public int GridStride { get; init; }
        public int GridRowStride { get; init; }
        public int GridButtonWidth { get; init; }
        public int ApplyY { get; init; }
        public int ApplyWidth { get; init; }
        public int ApplyHeight { get; init; }
        public int ApplyGap { get; init; }
        public int StatusY { get; init; }
        public int PathY { get; init; }
    }

    private static SettingsPanelLayout BuildSettingsLayout(int scrollY = 0)
    {
        var left = UiTheme.S(100);
        // Compact spacing at 150–200% so Impostazioni fits without overlapping rows.
        var compact = UiTheme.Scale >= 1.5f;
        var toggleHeight = UiTheme.S(compact ? 34 : 40);
        var rowGap = UiTheme.S(compact ? 6 : 10);
        var sectionGap = UiTheme.S(compact ? 8 : 14);
        var labelGap = UiTheme.S(compact ? 4 : 6);
        var buttonHeight = UiTheme.S(compact ? 30 : 34);
        var gridRow = buttonHeight + UiTheme.S(compact ? 4 : 6);
        var gridStride = UiTheme.S(compact ? 140 : 155);
        var gridButtonWidth = UiTheme.S(compact ? 132 : 148);

        var y = UiTheme.S(compact ? 96 : 130);
        var fpsY = y;
        y += toggleHeight + rowGap;
        var overlayY = y;
        y += toggleHeight + rowGap;
        var vsyncY = y;
        y += toggleHeight + rowGap;
        var autoSellY = y;
        y += toggleHeight + sectionGap;
        var reviewTutorialY = y;
        y += buttonHeight + sectionGap;
        var scaleLabelY = y;
        y += UiTheme.S(20) + labelGap;
        var scaleButtonsY = y;
        y += buttonHeight + sectionGap;
        var resLabelY = y;
        y += UiTheme.S(20) + labelGap;
        var autoResY = y;
        y += buttonHeight + UiTheme.S(8);
        var resGridY = y;
        var resRows = (GameSettings.ResolutionPresets.Length + 3) / 4;
        y += resRows * gridRow + sectionGap;
        var fpsLabelY = y;
        y += UiTheme.S(18) + labelGap;
        var fpsGridY = y;
        var fpsRows = (GameSettings.FpsLimitPresets.Length + 3) / 4;
        y += fpsRows * gridRow + sectionGap;
        var modeLabelY = y;
        y += UiTheme.S(20) + labelGap;
        var modeButtonsY = y;
        y += buttonHeight + sectionGap;
        var applyY = y;
        var applyHeight = UiTheme.S(40);
        y += applyHeight + UiTheme.S(12);
        var statusY = y;
        var pathY = y + UiTheme.S(20);
        var contentBottom = pathY + UiTheme.S(8);
        var viewportBottom = ScreenHeight - 80;
        var maxScroll = Math.Max(0, contentBottom - viewportBottom);
        scrollY = Math.Clamp(scrollY, 0, maxScroll);

        var toggleWidth = Math.Min(UiTheme.S(640), Math.Max(UiTheme.S(480), ScreenWidth - left * 2));

        return new SettingsPanelLayout
        {
            Left = left,
            TitleY = UiTheme.S(40) - scrollY,
            SubtitleY = UiTheme.S(78) - scrollY,
            ToggleWidth = toggleWidth,
            ToggleHeight = toggleHeight,
            FpsToggleY = fpsY - scrollY,
            OverlayToggleY = overlayY - scrollY,
            VsyncToggleY = vsyncY - scrollY,
            AutoSellToggleY = autoSellY - scrollY,
            ReviewTutorialY = reviewTutorialY - scrollY,
            ReviewTutorialWidth = UiTheme.S(220),
            ScaleLabelY = scaleLabelY - scrollY,
            ScaleButtonsY = scaleButtonsY - scrollY,
            ScaleButtonWidth = UiTheme.S(100),
            ScaleButtonStride = UiTheme.S(110),
            ButtonHeight = buttonHeight,
            ResLabelY = resLabelY - scrollY,
            AutoResY = autoResY - scrollY,
            AutoResWidth = UiTheme.S(180),
            ResGridY = resGridY - scrollY,
            FpsLabelY = fpsLabelY - scrollY,
            FpsGridY = fpsGridY - scrollY,
            ModeLabelY = modeLabelY - scrollY,
            ModeButtonsY = modeButtonsY - scrollY,
            ModeButtonWidth = UiTheme.S(150),
            ModeStride = UiTheme.S(160),
            GridStride = gridStride,
            GridRowStride = gridRow,
            GridButtonWidth = gridButtonWidth,
            ApplyY = applyY - scrollY,
            ApplyWidth = UiTheme.S(180),
            ApplyHeight = applyHeight,
            ApplyGap = UiTheme.S(20),
            StatusY = statusY - scrollY,
            PathY = pathY - scrollY
        };
    }

    private static void DrawSettings(GameSettings settings, GameSettings draft, string? statusMessage)
    {
        var layout = BuildSettingsLayout((int)SettingsScrollY);
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color(14, 18, 18, 255));
        DrawUiText("Impostazioni", layout.Left, layout.TitleY, 32, new Color(239, 238, 224, 255));
        DrawUiText("Overlay, vendita automatica, scala UI e grafica. Applica per salvare risoluzione/VSync/FPS.",
            layout.Left, layout.SubtitleY, 15, new Color(112, 124, 119, 255));

        DrawToggleRow(layout.Left, layout.FpsToggleY, layout.ToggleWidth, layout.ToggleHeight,
            "Mostra contatore FPS", settings.ShowFps);
        DrawToggleRow(layout.Left, layout.OverlayToggleY, layout.ToggleWidth, layout.ToggleHeight,
            "Mostra risorse sistema (CPU · GPU · RAM)", settings.ShowResourceOverlay);
        DrawToggleRow(layout.Left, layout.VsyncToggleY, layout.ToggleWidth, layout.ToggleHeight,
            "VSync", draft.VSync);
        DrawToggleRow(layout.Left, layout.AutoSellToggleY, layout.ToggleWidth, layout.ToggleHeight,
            "Vendita automatica al core", settings.AutoSellAtCore);

        DrawMenuButton(layout.Left, layout.ReviewTutorialY, layout.ReviewTutorialWidth, layout.ButtonHeight,
            "Rivedi tutorial");

        DrawUiText("Scala interfaccia", layout.Left, layout.ScaleLabelY, 16, new Color(196, 201, 193, 255));
        for (var i = 0; i < GameSettings.UiScalePresets.Length; i++)
        {
            var percent = GameSettings.UiScalePresets[i];
            DrawButton(
                layout.Left + i * layout.ScaleButtonStride,
                layout.ScaleButtonsY,
                layout.ScaleButtonWidth,
                layout.ButtonHeight,
                GameSettings.UiScaleLabel(percent),
                settings.UiScalePercent == percent);
        }

        DrawUiText("Risoluzione", layout.Left, layout.ResLabelY, 16, new Color(196, 201, 193, 255));
        DrawButton(layout.Left, layout.AutoResY, layout.AutoResWidth, layout.ButtonHeight,
            "Auto risoluzione", draft.UseAutoResolution);
        for (var i = 0; i < GameSettings.ResolutionPresets.Length; i++)
        {
            var preset = GameSettings.ResolutionPresets[i];
            var x = layout.Left + (i % 4) * layout.GridStride;
            var y = layout.ResGridY + (i / 4) * layout.GridRowStride;
            var selected = !draft.UseAutoResolution
                && draft.ResolutionWidth == preset.Width
                && draft.ResolutionHeight == preset.Height;
            DrawButton(x, y, layout.GridButtonWidth, layout.ButtonHeight, preset.Label, selected);
        }

        DrawUiText("Limite FPS (con VSync: preferenza salvata, sync al refresh)",
            layout.Left, layout.FpsLabelY, 15, new Color(196, 201, 193, 255));
        for (var i = 0; i < GameSettings.FpsLimitPresets.Length; i++)
        {
            var fps = GameSettings.FpsLimitPresets[i];
            var x = layout.Left + (i % 4) * layout.GridStride;
            var y = layout.FpsGridY + (i / 4) * layout.GridRowStride;
            DrawButton(x, y, layout.GridButtonWidth, layout.ButtonHeight,
                GameSettings.FpsLimitLabel(fps), draft.TargetFps == fps);
        }

        DrawUiText("Modalità schermo", layout.Left, layout.ModeLabelY, 16, new Color(196, 201, 193, 255));
        DrawButton(layout.Left, layout.ModeButtonsY, layout.ModeButtonWidth, layout.ButtonHeight,
            "Finestra", draft.DisplayMode == DisplayMode.Windowed);
        DrawButton(layout.Left + layout.ModeStride, layout.ModeButtonsY, layout.ModeButtonWidth, layout.ButtonHeight,
            "Senza bordi", draft.DisplayMode == DisplayMode.Borderless);
        DrawButton(layout.Left + layout.ModeStride * 2, layout.ModeButtonsY, layout.ModeButtonWidth, layout.ButtonHeight,
            "Schermo intero", draft.DisplayMode == DisplayMode.Fullscreen);

        var dirty = !draft.MatchesDisplay(settings);
        DrawMenuButton(layout.Left, layout.ApplyY, layout.ApplyWidth, layout.ApplyHeight,
            dirty ? "Applica*" : "Applica");
        DrawMenuButton(layout.Left + layout.ApplyWidth + layout.ApplyGap, layout.ApplyY,
            layout.ApplyWidth, layout.ApplyHeight, "Annulla");

        var resLabel = settings.UseAutoResolution
            ? $"Auto {settings.ResolutionWidth}×{settings.ResolutionHeight}"
            : $"{settings.ResolutionWidth}×{settings.ResolutionHeight}";
        DrawUiText(
            $"Attuale: {resLabel} · {GameSettings.DisplayModeLabel(settings.DisplayMode)} · UI {GameSettings.UiScaleLabel(settings.UiScalePercent)} · VSync {(settings.VSync ? "ON" : "OFF")} · {GameSettings.FpsLimitLabel(settings.TargetFps)}",
            layout.Left, layout.StatusY, 13, new Color(126, 137, 132, 255));
        DrawUiText($"File: {GameSettings.SettingsPath}", layout.Left, layout.PathY, 12, new Color(90, 100, 96, 255));

        DrawMenuButton(28, ScreenHeight - 70, 180, 40, "Indietro");
        if (SettingsScrollY > 2 || layout.PathY > ScreenHeight - 90)
        {
            DrawUiText("Rotella: scorri le impostazioni", ScreenWidth - UiTheme.S(280), ScreenHeight - 58, 13,
                UiTheme.TextMuted);
        }

        if (!string.IsNullOrEmpty(statusMessage))
        {
            DrawUiText(statusMessage, 230, ScreenHeight - 58, 16, new Color(112, 218, 145, 255));
        }
    }

    private static void DrawToggleRow(int x, int y, int width, int height, string label, bool enabled)
    {
        const int labelSize = 16;
        const int badgeSize = 16;
        var mouse = Raylib.GetMousePosition();
        var hover = Contains(mouse, x, y, width, height);
        Raylib.DrawRectangle(x, y, width, height,
            hover ? new Color(55, 66, 60, 255) : new Color(32, 38, 36, 255));
        Raylib.DrawRectangleLines(x, y, width, height, new Color(70, 82, 76, 255));

        var badge = enabled ? "ON" : "OFF";
        var badgeColor = enabled ? new Color(112, 218, 145, 255) : new Color(180, 120, 100, 255);
        var badgeWidth = MeasureUiText(badge, badgeSize);
        var gutter = Math.Max(UiTheme.S(72), badgeWidth + UiTheme.S(28));
        var labelPad = UiTheme.S(14);
        var labelMax = Math.Max(40, width - gutter - labelPad);
        var drawLabel = TruncateUiText(label, labelSize, labelMax);
        var textY = y + Math.Max(0, (height - UiTheme.S(labelSize)) / 2);
        DrawUiText(drawLabel, x + labelPad, textY, labelSize, new Color(232, 233, 221, 255));
        DrawUiText(badge, x + width - badgeWidth - labelPad, textY, badgeSize, badgeColor);
    }

    private static string TruncateUiText(string text, int fontSize, int maxWidth)
    {
        if (MeasureUiText(text, fontSize) <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "…";
        var ellipsisW = MeasureUiText(ellipsis, fontSize);
        if (ellipsisW >= maxWidth)
        {
            return ellipsis;
        }

        var truncated = text;
        while (truncated.Length > 0
               && MeasureUiText(truncated, fontSize) + ellipsisW > maxWidth)
        {
            truncated = truncated[..^1];
        }

        return string.IsNullOrEmpty(truncated) ? ellipsis : truncated.TrimEnd() + ellipsis;
    }

    /// <summary>Self-test: Impostazioni rows stay stacked (no Y overlap) at every UI scale.</summary>
    internal static bool SettingsLayoutIsStackedForAllScales()
    {
        var previous = UiTheme.Scale;
        var prevW = ScreenWidth;
        var prevH = ScreenHeight;
        try
        {
            ScreenWidth = 1280;
            ScreenHeight = 720;
            foreach (var percent in GameSettings.UiScalePresets)
            {
                UiTheme.ApplyScalePercent(percent);
                var baseLayout = BuildSettingsLayout(0);
                if (baseLayout.FpsToggleY + baseLayout.ToggleHeight > baseLayout.OverlayToggleY
                    || baseLayout.OverlayToggleY + baseLayout.ToggleHeight > baseLayout.VsyncToggleY
                    || baseLayout.VsyncToggleY + baseLayout.ToggleHeight > baseLayout.AutoSellToggleY
                    || baseLayout.AutoSellToggleY + baseLayout.ToggleHeight > baseLayout.ReviewTutorialY
                    || baseLayout.ReviewTutorialY + baseLayout.ButtonHeight > baseLayout.ScaleLabelY
                    || baseLayout.ScaleLabelY >= baseLayout.ScaleButtonsY
                    || baseLayout.ScaleButtonsY + baseLayout.ButtonHeight > baseLayout.ResLabelY
                    || baseLayout.ApplyY <= baseLayout.ModeButtonsY)
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            ScreenWidth = prevW;
            ScreenHeight = prevH;
            UiTheme.ApplyScale(previous);
        }
    }

    /// <summary>
    /// Self-test: Mercato, Fabbrica, dock and tutorial banner do not overlap at 100–200%.
    /// </summary>
    internal static bool HudLayoutIsValidForAllScales()
    {
        var previous = UiTheme.Scale;
        var prevW = ScreenWidth;
        var prevH = ScreenHeight;
        try
        {
            // Typical play window used by self-tests / default launch.
            ScreenWidth = 1280;
            ScreenHeight = 720;
            foreach (var percent in GameSettings.UiScalePresets)
            {
                UiTheme.ApplyScalePercent(percent);
                GetMercatoBounds(out var mx, out var my, out var mw, out var mh);
                GetStatusBounds(out var sx, out var sy, out var sw, out var sh);
                GetDockBounds(out var dx, out var dy, out var dw, out var dh);
                GetTutorialPanelBounds(out var tx, out var ty, out var tw, out var th);

                if (mx + mw > ScreenWidth || my < ViewportTop)
                {
                    return false;
                }

                // Mercato above Fabbrica (or Fabbrica hidden), both above dock.
                if (sh > 0 && sy < my + mh)
                {
                    return false;
                }

                if (my + mh > dy - 4)
                {
                    return false;
                }

                if (sh > 0 && sy + sh > dy)
                {
                    return false;
                }

                if (dx + dw > ScreenWidth || dy + dh > ScreenHeight)
                {
                    return false;
                }

                // Tutorial must not cover the dock grid.
                if (ty + th > dy && tx + tw > dx - 4)
                {
                    return false;
                }

                // Row helpers stay ordered inside Mercato.
                if (MercatoAutoSellY(my) >= MercatoHintY(my)
                    || MercatoHintY(my) >= MercatoRowY(my, mh, 0)
                    || MercatoRowY(my, mh, 0) >= MercatoRowY(my, mh, 1)
                    || MercatoRowY(my, mh, 3) + MercatoRowHeight(mh) > my + mh + 2)
                {
                    return false;
                }

                // Sell buttons must stay inside panel padding (no border clip on "tutti").
                GetMercatoSellButtonMetrics(out var oneW, out var allW, out var gap, out var rightPad);
                if (oneW + gap + allW + rightPad + MercatoS(8) > mw)
                {
                    return false;
                }

                GetMercatoSellAllBounds(mx, my, mw, mh, 0, out var allX, out _, out var allBtnW, out _);
                if (allX + allBtnW > mx + mw - rightPad + 1)
                {
                    return false;
                }

                // Fabbrica shares the Mercato column (same x + width).
                if (sh > 0 && (sx != mx || sw != mw))
                {
                    return false;
                }

                // Live counts must stay above the CORE button (default 125% used to overlap).
                if (sh > 0
                    && TryGetFabbricaContentMetrics(
                        out _, out _, out _, out _, out _, out var countsY,
                        out _, out var upgradeY, out _, out var upgradeH))
                {
                    if (upgradeY < countsY + MercatoS(12))
                    {
                        return false;
                    }

                    if (upgradeY + upgradeH > sy + sh + 2)
                    {
                        return false;
                    }
                }

                _ = mh;
            }

            return true;
        }
        finally
        {
            ScreenWidth = prevW;
            ScreenHeight = prevH;
            UiTheme.ApplyScale(previous);
        }
    }

    private static void DrawDebugOverlays(GameSettings settings, EconomyWallet? wallet)
    {
        _ = wallet;
        // Modals (Ricerca / Impostazioni / menu): never draw the system box — it covers titles.
        // Corner FPS only when the FPS toggle is on and the in-game system overlay is off.
        if (ShouldDrawCornerFps(settings))
        {
            var label = $"FPS {Raylib.GetFPS()}";
            var w = MeasureUiText(label, 18) + 16;
            var x = Math.Max(8, ScreenWidth - w - 8);
            Raylib.DrawRectangle(x, 8, w, 28, new Color(10, 12, 12, 180));
            DrawUiText(label, x + 8, 14, 18, new Color(211, 164, 76, 255));
        }
    }

    /// <summary>
    /// Corner FPS when the FPS toggle is on and the system-resource overlay is off.
    /// With the overlay on, FPS lives only inside that box (play HUD).
    /// </summary>
    private static bool ShouldDrawCornerFps(GameSettings settings) =>
        settings.ShowFps && !settings.ShowResourceOverlay;

    private static void DrawResearch(
        GameContent content,
        EconomyWallet wallet,
        ResearchState research,
        int selectedIndex,
        string? statusMessage,
        GameSettings settings)
    {
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color(14, 18, 18, 255));
        DrawUiText("Albero tecnologico", 28, 28, 30, new Color(239, 238, 224, 255));
        DrawUiText("Nodi e prerequisiti · T apre / Esc chiude · Shift+trascina o rotella per pan", 28, 66, 16,
            new Color(112, 124, 119, 255));
        DrawUiText($"Wallet: $ {wallet.Money}   ·   verde = sbloccato · ambra = disponibile · grigio = bloccato",
            28, 90, 15, new Color(164, 173, 168, 255));
        _ = settings;

        var graph = TechTreeLayout.Build(content);
        var entries = ResearchEntries(content);
        if (entries.Count == 0)
        {
            DrawUiText("Nessuna struttura nell'albero.", 60, 160, 22, new Color(164, 173, 168, 255));
            DrawMenuButton(28, ScreenHeight - 70, 180, 40, "Indietro");
            return;
        }

        selectedIndex = Math.Clamp(selectedIndex, 0, entries.Count - 1);
        var selected = entries[selectedIndex];

        GetTechTreeCanvas(out var canvasX, out var canvasY, out var canvasW, out var canvasH);
        Raylib.DrawRectangle(canvasX, canvasY, canvasW, canvasH, new Color(20, 24, 23, 255));
        Raylib.DrawRectangleLines(canvasX, canvasY, canvasW, canvasH, new Color(48, 56, 52, 255));

        Raylib.BeginScissorMode(canvasX + 1, canvasY + 1, canvasW - 2, canvasH - 2);

        foreach (var edge in graph.Edges)
        {
            var fromUnlocked = research.IsUnlocked(edge.FromId);
            var toState = research.GetNodeState(
                graph.Nodes.First(node => node.Structure.Id == edge.ToId).Structure);
            var edgeColor = toState == ResearchNodeState.Unlocked
                ? new Color(80, 160, 110, 220)
                : toState == ResearchNodeState.Available && fromUnlocked
                    ? new Color(200, 160, 90, 200)
                    : new Color(70, 78, 74, 180);
            var x1 = (int)(edge.FromX + TechTreePanX);
            var y1 = (int)(edge.FromY + TechTreePanY);
            var x2 = (int)(edge.ToX + TechTreePanX);
            var y2 = (int)(edge.ToY + TechTreePanY);
            var midX = (x1 + x2) / 2;
            Raylib.DrawLineEx(new Vector2(x1, y1), new Vector2(midX, y1), 2.5f, edgeColor);
            Raylib.DrawLineEx(new Vector2(midX, y1), new Vector2(midX, y2), 2.5f, edgeColor);
            Raylib.DrawLineEx(new Vector2(midX, y2), new Vector2(x2, y2), 2.5f, edgeColor);
            // Arrow tip
            Raylib.DrawTriangle(
                new Vector2(x2, y2),
                new Vector2(x2 - 10, y2 - 5),
                new Vector2(x2 - 10, y2 + 5),
                edgeColor);
        }

        foreach (var node in graph.Nodes)
        {
            var structure = node.Structure;
            var nx = (int)(node.X + TechTreePanX);
            var ny = (int)(node.Y + TechTreePanY);
            var state = research.GetNodeState(structure);
            var isSelected = structure.Id == selected.Id;
            var fill = state switch
            {
                ResearchNodeState.Unlocked => new Color(36, 62, 48, 255),
                ResearchNodeState.Available => new Color(58, 48, 32, 255),
                _ => new Color(28, 32, 31, 255)
            };
            if (isSelected)
            {
                fill = state switch
                {
                    ResearchNodeState.Unlocked => new Color(48, 84, 64, 255),
                    ResearchNodeState.Available => new Color(78, 64, 40, 255),
                    _ => new Color(42, 48, 46, 255)
                };
            }

            var border = state switch
            {
                ResearchNodeState.Unlocked => new Color(112, 218, 145, 255),
                ResearchNodeState.Available => new Color(220, 170, 110, 255),
                _ => new Color(90, 96, 92, 255)
            };
            if (isSelected)
            {
                Raylib.DrawRectangle(nx - 3, ny - 3, TechTreeLayout.NodeWidth + 6, TechTreeLayout.NodeHeight + 6,
                    new Color(211, 164, 76, 90));
            }

            Raylib.DrawRectangle(nx, ny, TechTreeLayout.NodeWidth, TechTreeLayout.NodeHeight, fill);
            Raylib.DrawRectangleLines(nx, ny, TechTreeLayout.NodeWidth, TechTreeLayout.NodeHeight, border);

            var statusLabel = state switch
            {
                ResearchNodeState.Unlocked => structure.IsStub ? "SBLOCCATO · stub" : "SBLOCCATO",
                ResearchNodeState.Available => "DISPONIBILE",
                _ => "BLOCCATO"
            };
            DrawUiText(structure.DisplayName, nx + 10, ny + 10, 17, new Color(232, 233, 221, 255));
            DrawUiText(statusLabel, nx + 10, ny + 36, 13, border);
        }

        Raylib.EndScissorMode();

        GetTechTreeDetailPanel(out var detailX, out var detailY, out var detailW, out var detailH);
        Raylib.DrawRectangle(detailX, detailY, detailW, detailH, new Color(24, 30, 28, 255));
        Raylib.DrawRectangleLines(detailX, detailY, detailW, detailH, new Color(60, 70, 64, 255));
        DrawUiText(selected.DisplayName, detailX + 16, detailY + 16, 22, new Color(239, 238, 224, 255));

        var selectedState = research.GetNodeState(selected);
        var stateText = selectedState switch
        {
            ResearchNodeState.Unlocked => selected.IsStub ? "Stato: sbloccato (stub)" : "Stato: sbloccato",
            ResearchNodeState.Available => "Stato: disponibile",
            _ => "Stato: bloccato"
        };
        DrawUiText(stateText, detailX + 16, detailY + 48, 15,
            selectedState == ResearchNodeState.Unlocked
                ? new Color(112, 218, 145, 255)
                : selectedState == ResearchNodeState.Available
                    ? new Color(220, 170, 110, 255)
                    : new Color(140, 148, 142, 255));

        DrawUiText($"Costo: {FormatUnlockRequirement(selected.Unlock)}", detailX + 16, detailY + 74, 15,
            new Color(164, 173, 168, 255));

        var prereqLabel = selected.Requires.Count == 0
            ? "Prerequisiti: nessuno"
            : "Prerequisiti: " + string.Join(", ",
                selected.Requires.Select(id =>
                {
                    var name = content.FindStructure(id)?.DisplayName ?? id;
                    return research.IsUnlocked(id) ? name : $"{name} (manca)";
                }));
        DrawUiText(prereqLabel, detailX + 16, detailY + 98, 14, new Color(164, 173, 168, 255));

        if (selected.IsStub)
        {
            DrawUiText("Stub: non costruibile ancora.", detailX + 16, detailY + 122, 14,
                new Color(180, 120, 100, 255));
        }

        var canUnlock = research.CanUnlock(selected, wallet);
        var buttonLabel = research.IsUnlocked(selected.Id)
            ? "Già sbloccato"
            : !research.MeetsPrerequisites(selected)
                ? "Prerequisiti mancanti"
                : canUnlock ? "Conferma sblocco" : "Risorse insufficienti";
        GetTechTreeUnlockButton(out var unlockX, out var unlockY, out var unlockW, out var unlockH);
        DrawMenuButton(unlockX, unlockY, unlockW, unlockH, buttonLabel);
        DrawUiText("Lo sblocco consuma denaro e materiali.", detailX + 16, detailY + 216, 13,
            new Color(126, 137, 132, 255));

        DrawMenuButton(28, ScreenHeight - 70, 180, 40, "Indietro");
        if (!string.IsNullOrEmpty(statusMessage))
        {
            DrawUiText(statusMessage, 230, ScreenHeight - 58, 18, new Color(225, 140, 110, 255));
        }
    }

    private static string FormatUnlockRequirement(UnlockRequirement? unlock)
    {
        if (unlock is null)
        {
            return "gratis";
        }

        var materials = string.Join(", ",
            unlock.Materials.Select(entry => $"{entry.Amount} {entry.ItemId}"));
        return materials.Length == 0
            ? $"${unlock.Money}"
            : $"${unlock.Money} + {materials}";
    }

    private static void DrawSaveManager(IReadOnlyList<SaveSlotInfo> slots, int selectedIndex, string? statusMessage)
    {
        DrawUiText("Gestione salvataggi", 60, 40, 32, new Color(239, 238, 224, 255));
        DrawUiText("Seleziona uno slot, poi Carica o Elimina.", 60, 90, 18, new Color(112, 124, 119, 255));

        if (slots.Count == 0)
        {
            DrawUiText("Nessun salvataggio presente.", 60, 160, 22, new Color(164, 173, 168, 255));
        }
        else
        {
            for (var index = 0; index < slots.Count; index++)
            {
                var slot = slots[index];
                var selected = index == selectedIndex;
                var y = 150 + index * 52;
                Raylib.DrawRectangle(60, y, 760, 44,
                    selected ? new Color(55, 72, 62, 255) : new Color(28, 34, 33, 255));
                var label = slot.Id == GameSaveStore.ContinueSlotId ? "Continua (autosave)" : slot.Id;
                DrawUiText(
                    $"{label}  ·  seed {slot.Seed}  ·  ${slot.Money}  ·  {slot.MapWidth}×{slot.MapHeight}",
                    76, y + 12, 18, new Color(220, 224, 214, 255));
            }

            DrawMenuButton(860, 150, 280, 44, "Carica");
            DrawMenuButton(860, 210, 280, 44, "Elimina");
            DrawMenuButton(860, 270, 280, 44, "Duplica Continua");
        }

        DrawMenuButton(28, ScreenHeight - 70, 180, 40, "Indietro");
        if (!string.IsNullOrEmpty(statusMessage))
        {
            DrawUiText(statusMessage, 230, ScreenHeight - 58, 18, new Color(225, 140, 110, 255));
        }
    }

    private static void DrawMenuButton(int x, int y, int width, int height, string label)
    {
        var mouse = Raylib.GetMousePosition();
        var hover = Contains(mouse, x, y, width, height);
        Raylib.DrawRectangle(x, y, width, height,
            hover ? new Color(211, 164, 76, 255) : new Color(45, 52, 50, 255));
        var text = hover ? new Color(25, 28, 26, 255) : new Color(215, 219, 210, 255);
        var textWidth = MeasureUiText(label, 20);
        DrawUiText(label, x + (width - textWidth) / 2, y + (height - 20) / 2, 20, text);
    }

    private static void DrawPlaying(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        WorldCamera camera,
        ResearchState research,
        EconomySession session,
        MarketCatalog market,
        EconomyConfig economy,
        GameSettings settings,
        ConveyorDefinition basicConveyor,
        ConveyorDefinition fastConveyor,
        ConveyorDefinition expressConveyor,
        ConveyorDefinition junctionConveyor,
        ConveyorDefinition splitterConveyor,
        ConveyorDefinition sorterConveyor,
        ConveyorDefinition bridgeConveyor,
        ConveyorDefinition selectedConveyor,
        RecipeDefinition smeltRecipe,
        RecipeDefinition wireRecipe,
        BuildingDefinition minerBuilding,
        BuildingDefinition advancedMinerBuilding,
        BuildingDefinition smelterBuilding,
        BuildingDefinition assemblerBuilding,
        BuildingDefinition generatorBuilding,
        BuildingDefinition powerNodeBuilding,
        BuildingDefinition powerNodeT2Building,
        BuildTool tool,
        Direction direction,
        string? statusMessage,
        float frameTime)
    {
        DrawWorld(world, conveyors, camera);
        DrawPreview(
            world, conveyors, wallet, research, camera, selectedConveyor,
            junctionConveyor, splitterConveyor, sorterConveyor, bridgeConveyor,
            smeltRecipe, wireRecipe,
            minerBuilding, advancedMinerBuilding, smelterBuilding, assemblerBuilding, generatorBuilding,
            powerNodeBuilding, powerNodeT2Building, tool, direction);
        DrawHeader(
            wallet, research, session, market, economy, settings, basicConveyor, fastConveyor, expressConveyor,
            junctionConveyor, splitterConveyor, sorterConveyor, bridgeConveyor,
            selectedConveyor, minerBuilding, advancedMinerBuilding, smelterBuilding, assemblerBuilding, generatorBuilding,
            powerNodeBuilding, powerNodeT2Building,
            tool, direction, world, camera);
        DrawMercatoPanel(world, wallet, market);
        DrawStatusPanel(world, conveyors, research, wallet, session, economy);
        DrawBuildDock(
            wallet, research, selectedConveyor, direction, tool,
            basicConveyor, fastConveyor, expressConveyor, junctionConveyor, splitterConveyor, sorterConveyor, bridgeConveyor,
            smeltRecipe, wireRecipe,
            minerBuilding, advancedMinerBuilding, smelterBuilding, assemblerBuilding, generatorBuilding,
            powerNodeBuilding, powerNodeT2Building);

        if (!string.IsNullOrEmpty(statusMessage))
        {
            var label = statusMessage!;
            var textW = MeasureUiText(label, 16);
            var boxW = Math.Clamp(textW + 24, 160, Math.Min(560, ScreenWidth - 40));
            var remaining = StatusToastUntil - Raylib.GetTime();
            var alpha = remaining < 0.6
                ? (int)Math.Clamp(remaining / 0.6 * 200, 0, 200)
                : 200;
            var textAlpha = remaining < 0.6
                ? (int)Math.Clamp(remaining / 0.6 * 255, 0, 255)
                : 255;
            Raylib.DrawRectangle(ViewportLeft + 16, ViewportTop + 10, boxW, 28, new Color(10, 14, 14, alpha));
            DrawUiText(label, ViewportLeft + 24, ViewportTop + 16, 16,
                new Color(112, 218, 145, textAlpha));
        }

        if (TutorialActive)
        {
            DrawTutorialBanner(settings);
        }

        if (ActiveCampaignLevel is not null && wallet is not null && session is not null)
        {
            DrawCampaignObjectiveHud(ActiveCampaignLevel, wallet, session, research);
        }

        if (CampaignLevelComplete && ActiveCampaignLevel is not null)
        {
            DrawCampaignVictoryBanner(ActiveCampaignLevel);
        }

        DrawVersionOverlay();
        DrawEntryOverlay(frameTime);
    }

    /// <summary>
    /// Release label from assembly InformationalVersion (csproj Version), e.g. "v0.2.7".
    /// Drawn bottom-left on the play HUD; not hit-tested so it never blocks clicks.
    /// </summary>
    internal static string FormatGameVersionLabel()
    {
        var info = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
            ?? "0.0.0";
        var version = info.Split('+', 2)[0].Trim();
        if (version.Length == 0)
        {
            version = "0.0.0";
        }

        return version.StartsWith('v') || version.StartsWith('V')
            ? version
            : "v" + version;
    }

    private static void DrawVersionOverlay()
    {
        var label = FormatGameVersionLabel();
        var size = 12;
        var x = UiTheme.S(10);
        var y = ScreenHeight - UiTheme.S(22);
        // Muted, low-alpha — readable but non-intrusive over the map.
        DrawUiText(label, x, y, size, new Color(168, 176, 168, 110));
    }

    private static void DrawCampaignObjectiveHud(
        CampaignLevelDefinition level,
        EconomyWallet wallet,
        EconomySession session,
        ResearchState research)
    {
        var objectives = level.Objectives ?? [];
        if (objectives.Count == 0)
        {
            return;
        }

        var panelW = UiTheme.S(320);
        var lineH = UiTheme.S(18);
        var panelH = UiTheme.S(36) + objectives.Count * lineH + UiTheme.S(10);
        var x = 16;
        var y = HeaderHeight + (ActiveSettings?.ShowResourceOverlay == true ? UiTheme.S(78) : UiTheme.S(12));

        Raylib.DrawRectangle(x, y, panelW, panelH, new Color(10, 14, 14, 210));
        Raylib.DrawRectangleLines(x, y, panelW, panelH, UiTheme.AccentDim);
        DrawUiText($"OBIETTIVO · {level.Name}", x + 10, y + 6, 13, UiTheme.Accent);

        for (var i = 0; i < objectives.Count; i++)
        {
            var objective = objectives[i];
            var current = CampaignProgress.GetObjectiveCurrent(objective, wallet, session, research);
            var done = CampaignProgress.IsObjectiveComplete(objective, wallet, session, research);
            var label = CampaignCatalog.FormatObjectiveProgress(objective, current);
            DrawUiText(label, x + 10, y + UiTheme.S(28) + i * lineH, 12,
                done ? new Color(120, 228, 150, 255) : UiTheme.TextPrimary);
        }
    }

    private static void GetCampaignVictoryBounds(out int x, out int y, out int w, out int h)
    {
        w = 480;
        h = 220;
        x = (ScreenWidth - w) / 2;
        y = (ScreenHeight - h) / 2;
    }

    private static void DrawCampaignVictoryBanner(CampaignLevelDefinition level)
    {
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color(0, 0, 0, 150));
        GetCampaignVictoryBounds(out var x, out var y, out var w, out var h);
        Raylib.DrawRectangle(x, y, w, h, new Color(22, 30, 26, 255));
        Raylib.DrawRectangleLines(x, y, w, h, new Color(120, 228, 150, 255));

        var title = "Livello completato";
        var titleW = MeasureUiText(title, 28);
        DrawUiText(title, x + (w - titleW) / 2, y + 28, 28, new Color(120, 228, 150, 255));
        var sub = level.Name;
        var subW = MeasureUiText(sub, 18);
        DrawUiText(sub, x + (w - subW) / 2, y + 70, 18, UiTheme.TextPrimary);

        var catalog = Campaign ?? CampaignCatalog.Load();
        var hasNext = catalog.NextAfter(level) is not null;
        var continueLabel = hasNext ? "Continua campagna" : "Selezione livelli";
        DrawMenuButton(x + 40, y + 130, 190, 48, continueLabel);
        DrawMenuButton(x + w - 230, y + 130, 190, 48, "Menu");
    }

    private static bool HandleCampaignVictoryInput(ref AppScreen screen, ref string? statusMessage)
    {
        if (ActiveCampaignLevel is null || Campaign is null)
        {
            return false;
        }

        CampaignProgressState ??= CampaignProgress.Load();
        GetCampaignVictoryBounds(out var x, out var y, out var w, out _);
        var mouse = Raylib.GetMousePosition();
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)
            && !Raylib.IsKeyPressed(KeyboardKey.Enter)
            && !Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            return true; // absorb input while banner is up
        }

        var level = ActiveCampaignLevel;
        var next = Campaign.NextAfter(level);
        var continueClicked = Raylib.IsMouseButtonPressed(MouseButton.Left)
            && Contains(mouse, x + 40, y + 130, 190, 48);
        var menuClicked = Raylib.IsMouseButtonPressed(MouseButton.Left)
            && Contains(mouse, x + w - 230, y + 130, 190, 48);
        var enter = Raylib.IsKeyPressed(KeyboardKey.Enter);
        var esc = Raylib.IsKeyPressed(KeyboardKey.Escape);

        if (!continueClicked && !menuClicked && !enter && !esc)
        {
            return true;
        }

        if (!CampaignVictoryHandled)
        {
            CampaignProgressState.MarkComplete(level.Id);
            CampaignVictoryHandled = true;
        }

        if (menuClicked || esc)
        {
            ActiveCampaignLevel = null;
            CampaignLevelComplete = false;
            CampaignVictoryHandled = false;
            statusMessage = null;
            screen = AppScreen.Home;
            return true;
        }

        // Continua campagna / Enter
        if (next is not null)
        {
            BeginLoadingCampaignLevel(next, ref screen, ref statusMessage);
        }
        else
        {
            ActiveCampaignLevel = null;
            CampaignLevelComplete = false;
            CampaignVictoryHandled = false;
            statusMessage = "Campagna completata!";
            screen = AppScreen.CampaignSelect;
        }

        return true;
    }

    private static void DrawHeader(
        EconomyWallet wallet,
        ResearchState research,
        EconomySession session,
        MarketCatalog market,
        EconomyConfig economy,
        GameSettings settings,
        ConveyorDefinition basicConveyor,
        ConveyorDefinition fastConveyor,
        ConveyorDefinition expressConveyor,
        ConveyorDefinition junctionConveyor,
        ConveyorDefinition splitterConveyor,
        ConveyorDefinition sorterConveyor,
        ConveyorDefinition bridgeConveyor,
        ConveyorDefinition selectedConveyor,
        BuildingDefinition minerBuilding,
        BuildingDefinition advancedMinerBuilding,
        BuildingDefinition smelterBuilding,
        BuildingDefinition assemblerBuilding,
        BuildingDefinition generatorBuilding,
        BuildingDefinition powerNodeBuilding,
        BuildingDefinition powerNodeT2Building,
        BuildTool tool,
        Direction direction,
        FactoryWorld world,
        WorldCamera camera)
    {
        // Sharper header bar with bright bottom edge.
        Raylib.DrawRectangle(0, 0, ScreenWidth, HeaderHeight, new Color(10, 12, 14, 245));
        Raylib.DrawRectangle(0, HeaderHeight - 2, ScreenWidth, 2, UiTheme.PanelBorderBright);
        DrawUiText("tINDUSTRY", 16, 8, 20, UiTheme.TextPrimary);
        DrawUiText(
            $"seed {world.Seed}  ·  zoom {camera.Zoom:0.00}",
            16, 30, 12, UiTheme.TextMuted);

        // Game inventory strip always visible (separate from system-resource overlay).
        DrawResourceStrip(wallet, session);

        if (settings.ShowResourceOverlay)
        {
            // Below header on the left — play HUD only (modals never draw this box).
            DrawSystemResourceOverlay(16, HeaderHeight + 8);
        }

        var cost = tool switch
        {
            BuildTool.Miner => research.IsUnlocked("miner")
                ? FormatBuildingCost(minerBuilding)
                : "Sblocca in Ricerca",
            BuildTool.MinerAdvanced => research.IsUnlocked(MinerBuilding.AdvancedId)
                ? FormatBuildingCost(advancedMinerBuilding)
                : "Sblocca in Ricerca",
            BuildTool.Smelter => research.IsUnlocked("smelter")
                ? FormatBuildingCost(smelterBuilding)
                : "Sblocca in Ricerca",
            BuildTool.Assembler => research.IsUnlocked("assembler")
                ? FormatBuildingCost(assemblerBuilding)
                : "Sblocca in Ricerca",
            BuildTool.Generator => research.IsUnlocked("generator")
                ? FormatBuildingCost(generatorBuilding)
                : "Sblocca in Ricerca",
            BuildTool.PowerNode => research.IsUnlocked("power-node")
                ? FormatBuildingCost(powerNodeBuilding)
                : "Sblocca in Ricerca",
            BuildTool.PowerNodeT2 => research.IsUnlocked("power-node-t2")
                ? FormatBuildingCost(powerNodeT2Building)
                : "Sblocca in Ricerca",
            BuildTool.Junction => FormatConveyorCost(junctionConveyor, research),
            BuildTool.Splitter => FormatConveyorCost(splitterConveyor, research),
            BuildTool.Sorter => FormatConveyorCost(sorterConveyor, research),
            BuildTool.Bridge => FormatConveyorCost(bridgeConveyor, research),
            BuildTool.Conveyor => FormatConveyorCost(selectedConveyor, research),
            _ => economy.RefundPolicyNote
        };
        // Keep cost inside the header so it never collides with the system overlay below.
        DrawUiText(cost, 16, Math.Min(48, HeaderHeight - 16), 12, new Color(164, 173, 168, 255));

        var mouse = Raylib.GetMousePosition();
        // Icon buttons right → left: Menu (0), Ricerca (1), Impostazioni (2).
        DrawHeaderIconButton(0, HitHeaderIcon(mouse, 0), () =>
            UiTheme.DrawMenuIcon(HeaderIconX(0) + 4, 16, HeaderIconSize - 8, UiTheme.TextPrimary));
        DrawHeaderIconButton(1, HitHeaderIcon(mouse, 1), () =>
            UiTheme.DrawTreeIcon(HeaderIconX(1) + 4, 16, HeaderIconSize - 8, UiTheme.TextPrimary));
        DrawHeaderIconButton(2, HitHeaderIcon(mouse, 2), () =>
            UiTheme.DrawGearIcon(HeaderIconX(2) + 4, 16, HeaderIconSize - 8, UiTheme.TextPrimary));

        if (ShouldDrawCornerFps(settings))
        {
            var fpsLabel = $"FPS {Raylib.GetFPS()}";
            var fpsW = MeasureUiText(fpsLabel, 14) + 16;
            var fpsX = HeaderIconX(2) - fpsW - 12;
            Raylib.DrawRectangle(fpsX, 14, fpsW, 24, new Color(10, 12, 12, 180));
            DrawUiText(fpsLabel, fpsX + 8, 18, 14, new Color(211, 164, 76, 255));
        }

        _ = market;
        _ = basicConveyor;
        _ = fastConveyor;
        _ = expressConveyor;
        _ = advancedMinerBuilding;
        _ = direction;
    }

    private static void DrawResourceStrip(EconomyWallet wallet, EconomySession session)
    {
        var items = UiTheme.InventoryItems;
        var moneyLabel = $"$ {wallet.Money}";
        var net = session.NetWorthDelta(wallet);
        var netLabel = UiTheme.SessionDeltaLabel(net);
        var moneyW = MeasureUiText(moneyLabel, 15);
        var netW = MeasureUiText(netLabel, 12);

        var chipsW = 0;
        var chipWidths = new int[items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            var count = wallet.MaterialCount(items[i].ItemId);
            var countW = MeasureUiText(count.ToString(), 14);
            var labelW = MeasureUiText(items[i].ShortName, 10);
            chipWidths[i] = Math.Max(UiTheme.S(52), 34 + Math.Max(countW, labelW) + 10);
            chipsW += chipWidths[i];
        }

        var moneyColW = Math.Max(moneyW, netW) + 28; // room for coin icon
        var stripW = 12 + moneyColW + 16 + chipsW + 10;
        var stripX = Math.Clamp((ScreenWidth - stripW) / 2, 200, Math.Max(200, HeaderIconX(2) - stripW - 100));
        var stripY = 8;
        var stripH = 48;
        Raylib.DrawRectangle(stripX, stripY, stripW, stripH, UiTheme.PanelFill);
        UiTheme.DrawAccentRect(stripX, stripY, stripW, stripH, UiTheme.PanelBorderBright, 1);

        // Coin + money / Δ sessione stacked on the left.
        GameIcons.TryDraw("money", stripX + 8, stripY + 6, 18, UiTheme.MoneyGreen);
        DrawUiText(moneyLabel, stripX + 30, stripY + 6, 15, UiTheme.MoneyGreen);
        var netColor = net >= 0 ? UiTheme.MoneyGreen : UiTheme.MoneyRed;
        var netX = stripX + 10;
        var netY = stripY + 28;
        DrawUiText(netLabel, netX, netY, 12, netColor);

        var mouse = Raylib.GetMousePosition();
        if (Contains(mouse, netX - 2, netY - 2, netW + 6, 16))
        {
            DrawTooltip(UiTheme.SessionDeltaTooltip, (int)mouse.X + 12, (int)mouse.Y + 18);
        }

        var x = stripX + 10 + moneyColW + 10;
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var count = wallet.MaterialCount(item.ItemId);
            var iconBox = UiTheme.S(26);
            Raylib.DrawRectangle(x, stripY + 8, iconBox, iconBox, new Color(24, 28, 30, 255));
            Raylib.DrawRectangleLines(x, stripY + 8, iconBox, iconBox, UiTheme.ItemOutline(item.ItemId));
            UiTheme.DrawItemIcon(item.ItemId, x + 3, stripY + 11, iconBox - 6);
            DrawUiText(count.ToString(), x + iconBox + 4, stripY + 8, 14, UiTheme.TextPrimary);
            DrawUiText(item.ShortName, x + iconBox + 4, stripY + 28, 10, UiTheme.TextMuted);
            x += chipWidths[i];
        }
    }

    private static void DrawTooltip(string text, int x, int y)
    {
        var pad = 8;
        var tw = MeasureUiText(text, 12);
        var w = tw + pad * 2;
        var h = 24;
        var drawX = Math.Clamp(x, 8, ScreenWidth - w - 8);
        var drawY = Math.Clamp(y, HeaderHeight + 4, ScreenHeight - h - 8);
        Raylib.DrawRectangle(drawX, drawY, w, h, new Color(8, 10, 12, 235));
        UiTheme.DrawAccentRect(drawX, drawY, w, h, UiTheme.AccentDim, 1);
        DrawUiText(text, drawX + pad, drawY + 5, 12, UiTheme.TextPrimary);
    }

    private static void DrawBuildDock(
        EconomyWallet wallet,
        ResearchState research,
        ConveyorDefinition selectedConveyor,
        Direction direction,
        BuildTool tool,
        ConveyorDefinition basicConveyor,
        ConveyorDefinition fastConveyor,
        ConveyorDefinition expressConveyor,
        ConveyorDefinition junctionConveyor,
        ConveyorDefinition splitterConveyor,
        ConveyorDefinition sorterConveyor,
        ConveyorDefinition bridgeConveyor,
        RecipeDefinition smeltRecipe,
        RecipeDefinition wireRecipe,
        BuildingDefinition minerBuilding,
        BuildingDefinition advancedMinerBuilding,
        BuildingDefinition smelterBuilding,
        BuildingDefinition assemblerBuilding,
        BuildingDefinition generatorBuilding,
        BuildingDefinition powerNodeBuilding,
        BuildingDefinition powerNodeT2Building)
    {
        // Safety: removed rail categories must not stick as active.
        if (DockCategory is UiTheme.BuildCategory.Inventory or UiTheme.BuildCategory.Tools)
        {
            DockCategory = UiTheme.BuildCategory.Logistics;
        }

        var entries = UiTheme.EntriesFor(DockCategory);
        GetDockBounds(out var dockX, out var dockY, out var dockW, out var dockH);
        var gridW = UiTheme.DockGridWidth(entries.Length);
        var gridH = UiTheme.DockGridHeight(entries.Length);
        var railW = UiTheme.DockRailWidth;
        var railX = dockX + gridW + UiTheme.DockCellGap;
        var mouse = Raylib.GetMousePosition();
        UiTheme.DockEntry? hoveredEntry = null;
        UiTheme.DockEntry? selectedEntry = null;

        // Building grid panel (left of rail).
        Raylib.DrawRectangle(dockX, dockY, gridW, gridH, UiTheme.PanelFill);
        UiTheme.DrawAccentRect(dockX, dockY, gridW, gridH, UiTheme.PanelBorder, 1);

        // Category rail (far right).
        Raylib.DrawRectangle(railX, dockY, railW, dockH - UiTheme.DockHoverBarHeight, UiTheme.PanelFill);
        UiTheme.DrawAccentRect(railX, dockY, railW, dockH - UiTheme.DockHoverBarHeight, UiTheme.PanelBorder, 1);

        for (var i = 0; i < UiTheme.BuildCategories.Length; i++)
        {
            var category = UiTheme.BuildCategories[i];
            var cx = railX + UiTheme.DockPadding;
            var cy = dockY + UiTheme.DockPadding + i * (UiTheme.DockCellSize + UiTheme.DockCellGap);
            var active = DockCategory == category;
            var hovered = Contains(mouse, cx, cy, UiTheme.DockCellSize, UiTheme.DockCellSize);
            Raylib.DrawRectangle(cx, cy, UiTheme.DockCellSize, UiTheme.DockCellSize, UiTheme.CellFill);
            if (active)
            {
                UiTheme.DrawAccentRect(cx, cy, UiTheme.DockCellSize, UiTheme.DockCellSize, UiTheme.Accent);
            }
            else if (hovered)
            {
                UiTheme.DrawAccentRect(cx, cy, UiTheme.DockCellSize, UiTheme.DockCellSize, UiTheme.AccentDim);
            }
            else
            {
                Raylib.DrawRectangleLines(cx, cy, UiTheme.DockCellSize, UiTheme.DockCellSize, UiTheme.PanelBorder);
            }

            var tint = active ? UiTheme.Accent : UiTheme.BuildCategoryTint(category);
            UiTheme.DrawBuildCategoryIcon(category, cx, cy, UiTheme.DockCellSize, tint);
            var catLabel = UiTheme.BuildCategoryShortLabel(category);
            var catLw = MeasureUiText(catLabel, 9);
            DrawUiText(catLabel, cx + (UiTheme.DockCellSize - catLw) / 2,
                cy + UiTheme.DockCellSize - UiTheme.S(12), 9,
                active ? UiTheme.Accent : UiTheme.TextMuted);
        }

        var gridX = dockX + UiTheme.DockPadding;
        var gridY = dockY + UiTheme.DockPadding;
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var col = i % UiTheme.DockGridCols;
            var row = i / UiTheme.DockGridCols;
            var cx = gridX + col * (UiTheme.DockCellSize + UiTheme.DockCellGap);
            var cy = gridY + row * (UiTheme.DockCellSize + UiTheme.DockCellGap);
            var locked = entry.ResearchId is not null && !research.IsUnlocked(entry.ResearchId);
            var selected = IsDockEntrySelected(entry, selectedConveyor, direction, tool);
            var hovered = Contains(mouse, cx, cy, UiTheme.DockCellSize, UiTheme.DockCellSize);

            Raylib.DrawRectangle(cx, cy, UiTheme.DockCellSize, UiTheme.DockCellSize,
                locked ? UiTheme.CellFillLocked : UiTheme.CellFill);

            DrawDockGlyph(entry, cx, cy, locked);

            if (selected)
            {
                selectedEntry = entry;
                UiTheme.DrawAccentRect(cx, cy, UiTheme.DockCellSize, UiTheme.DockCellSize, UiTheme.Accent);
            }
            else if (hovered)
            {
                UiTheme.DrawAccentRect(cx, cy, UiTheme.DockCellSize, UiTheme.DockCellSize, UiTheme.AccentDim);
            }
            else
            {
                Raylib.DrawRectangleLines(cx, cy, UiTheme.DockCellSize, UiTheme.DockCellSize,
                    locked ? new Color(50, 50, 52, 255) : UiTheme.PanelBorder);
            }

            if (locked)
            {
                Raylib.DrawRectangle(cx, cy, UiTheme.DockCellSize, UiTheme.DockCellSize, new Color(8, 8, 10, 120));
            }

            if (hovered)
            {
                hoveredEntry = entry;
            }
        }

        selectedEntry ??= FindSelectedDockEntry(selectedConveyor, tool)
            ?? entries.FirstOrDefault(e => IsDockEntrySelected(e, selectedConveyor, direction, tool));

        var barEntry = hoveredEntry ?? selectedEntry;
        var barY = dockY + dockH - UiTheme.DockHoverBarHeight;
        Raylib.DrawRectangle(dockX, barY, dockW, UiTheme.DockHoverBarHeight, UiTheme.PanelFill);
        UiTheme.DrawAccentRect(dockX, barY, dockW, UiTheme.DockHoverBarHeight, UiTheme.PanelBorder, 1);
        DrawDockCostBar(
            dockX, barY, dockW, UiTheme.DockHoverBarHeight,
            barEntry, wallet, research,
            basicConveyor, fastConveyor, expressConveyor, junctionConveyor, splitterConveyor, sorterConveyor, bridgeConveyor,
            smeltRecipe, wireRecipe,
            minerBuilding, advancedMinerBuilding, smelterBuilding, assemblerBuilding, generatorBuilding,
            powerNodeBuilding, powerNodeT2Building);
    }

    private static UiTheme.DockEntry? FindSelectedDockEntry(ConveyorDefinition selectedConveyor, BuildTool tool)
    {
        foreach (var category in UiTheme.BuildCategories)
        {
            foreach (var entry in UiTheme.EntriesFor(category))
            {
                if (DockSelectedId == entry.Id)
                {
                    return entry;
                }

                if (entry.Kind == UiTheme.DockEntryKind.ConveyorVariant
                    && tool == BuildTool.Conveyor
                    && entry.ConveyorId == selectedConveyor.Id)
                {
                    return entry;
                }

                if (entry.Kind == UiTheme.DockEntryKind.BuildTool && entry.Tool == tool)
                {
                    return entry;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Dock footer: recipe usage (Input / Output / time) when the held block crafts,
    /// then Peak-style cost row (name · materials · +$cost).
    /// </summary>
    private static void DrawDockCostBar(
        int barX,
        int barY,
        int barW,
        int barH,
        UiTheme.DockEntry? entry,
        EconomyWallet wallet,
        ResearchState research,
        ConveyorDefinition basicConveyor,
        ConveyorDefinition fastConveyor,
        ConveyorDefinition expressConveyor,
        ConveyorDefinition junctionConveyor,
        ConveyorDefinition splitterConveyor,
        ConveyorDefinition sorterConveyor,
        ConveyorDefinition bridgeConveyor,
        RecipeDefinition smeltRecipe,
        RecipeDefinition wireRecipe,
        BuildingDefinition minerBuilding,
        BuildingDefinition advancedMinerBuilding,
        BuildingDefinition smelterBuilding,
        BuildingDefinition assemblerBuilding,
        BuildingDefinition generatorBuilding,
        BuildingDefinition powerNodeBuilding,
        BuildingDefinition powerNodeT2Building)
    {
        if (entry is null)
        {
            var fallback = UiTheme.BuildCategoryLabel(DockCategory);
            var fw = MeasureUiText(fallback, 12);
            DrawUiText(fallback, barX + Math.Max(4, (barW - fw) / 2),
                barY + Math.Max(4, (barH - UiTheme.S(12)) / 2), 12, UiTheme.TextMuted);
            return;
        }

        if (entry.Id == "remove" || entry.Tool == BuildTool.Remove)
        {
            const string removeLabel = "Rimuovi · demolisci edifici e nastri";
            var rw = MeasureUiText(removeLabel, 12);
            DrawUiText(removeLabel, barX + Math.Max(4, (barW - rw) / 2),
                barY + Math.Max(4, (barH - UiTheme.S(12)) / 2), 12, UiTheme.TextPrimary);
            return;
        }

        if (entry.ResearchId is not null && !research.IsUnlocked(entry.ResearchId))
        {
            var lockedLabel = $"{entry.Label} · Sblocca in Ricerca";
            var lw = MeasureUiText(lockedLabel, 12);
            DrawUiText(lockedLabel, barX + Math.Max(4, (barW - lw) / 2),
                barY + Math.Max(4, (barH - UiTheme.S(12)) / 2), 12, UiTheme.TextMuted);
            return;
        }

        var hasRecipe = TryResolveDockEntryRecipe(entry, smeltRecipe, wireRecipe, out var recipe);
        var pad = UiTheme.S(6);
        var usageH = hasRecipe ? UiTheme.S(36) : 0;
        var costY = barY + (hasRecipe ? usageH : 0);
        var costH = barH - (hasRecipe ? usageH : 0);

        if (hasRecipe && recipe is not null)
        {
            DrawDockRecipeUsage(barX + pad, barY + UiTheme.S(2), barW - pad * 2, usageH - UiTheme.S(4), recipe);
            Raylib.DrawRectangle(barX + UiTheme.S(4), costY, barW - UiTheme.S(8), 1, UiTheme.PanelBorder);
        }

        DrawDockCostRow(
            barX, costY, barW, costH,
            entry, wallet,
            basicConveyor, fastConveyor, expressConveyor, junctionConveyor, splitterConveyor, sorterConveyor, bridgeConveyor,
            minerBuilding, advancedMinerBuilding, smelterBuilding, assemblerBuilding, generatorBuilding,
            powerNodeBuilding, powerNodeT2Building,
            hasRecipe ? entry.Hint : null);
    }

    /// <summary>
    /// Two-line Peak-style recipe: Input ×N [icon]… / Output ×N [icon] / Ns
    /// </summary>
    private static void DrawDockRecipeUsage(int x, int y, int w, int h, RecipeDefinition recipe)
    {
        const int fontSize = 11;
        var iconSize = UiTheme.S(14);
        var lineH = Math.Max(UiTheme.S(fontSize) + 2, iconSize + 2);
        var inY = y;
        var outY = y + lineH;

        DrawUiText("Input", x, inY + Math.Max(0, (lineH - UiTheme.S(fontSize)) / 2), fontSize, UiTheme.TextMuted);
        var cursor = x + MeasureUiText("Input", fontSize) + UiTheme.S(6);
        foreach (var input in recipe.Inputs.Where(m => m.Amount > 0))
        {
            cursor = DrawDockQtyIcon(cursor, inY, lineH, fontSize, iconSize, input.Amount, input.ItemId);
            cursor += UiTheme.S(6);
        }

        DrawUiText("Output", x, outY + Math.Max(0, (lineH - UiTheme.S(fontSize)) / 2), fontSize, UiTheme.TextMuted);
        cursor = x + MeasureUiText("Output", fontSize) + UiTheme.S(6);
        foreach (var output in recipe.Outputs.Where(m => m.Amount > 0))
        {
            cursor = DrawDockQtyIcon(cursor, outY, lineH, fontSize, iconSize, output.Amount, output.ItemId);
            cursor += UiTheme.S(6);
        }

        var duration = FormatRecipeDuration(recipe.DurationSeconds);
        var durW = MeasureUiText(duration, fontSize);
        var durX = Math.Max(cursor, x + w - durW);
        DrawUiText(duration, durX, outY + Math.Max(0, (lineH - UiTheme.S(fontSize)) / 2), fontSize, UiTheme.Accent);
    }

    private static int DrawDockQtyIcon(
        int x, int rowY, int lineH, int fontSize, int iconSize, int amount, string itemId)
    {
        var qty = $"×{amount}";
        var qtyW = MeasureUiText(qty, fontSize);
        var textY = rowY + Math.Max(0, (lineH - UiTheme.S(fontSize)) / 2);
        var iconY = rowY + Math.Max(0, (lineH - iconSize) / 2);
        DrawUiText(qty, x, textY, fontSize, UiTheme.TextPrimary);
        x += qtyW + UiTheme.S(2);
        Raylib.DrawRectangle(x - 1, iconY - 1, iconSize + 2, iconSize + 2, new Color(24, 28, 30, 255));
        UiTheme.DrawItemIcon(itemId, x, iconY, iconSize);
        return x + iconSize;
    }

    private static string FormatRecipeDuration(float seconds)
    {
        if (seconds <= 0)
        {
            return "";
        }

        // Prefer compact "2s" / "1.5s" without trailing .0
        var rounded = Math.Round(seconds, 1);
        var label = Math.Abs(rounded - Math.Truncate(rounded)) < 0.05
            ? ((int)Math.Truncate(rounded)).ToString(CultureInfo.InvariantCulture)
            : rounded.ToString("0.#", CultureInfo.InvariantCulture);
        return $" / {label}s";
    }

    private static void DrawDockCostRow(
        int barX,
        int barY,
        int barW,
        int barH,
        UiTheme.DockEntry entry,
        EconomyWallet wallet,
        ConveyorDefinition basicConveyor,
        ConveyorDefinition fastConveyor,
        ConveyorDefinition expressConveyor,
        ConveyorDefinition junctionConveyor,
        ConveyorDefinition splitterConveyor,
        ConveyorDefinition sorterConveyor,
        ConveyorDefinition bridgeConveyor,
        BuildingDefinition minerBuilding,
        BuildingDefinition advancedMinerBuilding,
        BuildingDefinition smelterBuilding,
        BuildingDefinition assemblerBuilding,
        BuildingDefinition generatorBuilding,
        BuildingDefinition powerNodeBuilding,
        BuildingDefinition powerNodeT2Building,
        string? usageHintFallback)
    {
        if (!TryResolveDockEntryCost(
                entry, basicConveyor, fastConveyor, expressConveyor, junctionConveyor, splitterConveyor, sorterConveyor, bridgeConveyor,
                minerBuilding, advancedMinerBuilding, smelterBuilding, assemblerBuilding, generatorBuilding,
                powerNodeBuilding, powerNodeT2Building,
                out var money, out var materials))
        {
            var hint = TruncateUiText(usageHintFallback ?? entry.Hint ?? entry.Label, 12, barW - UiTheme.S(16));
            var hw = MeasureUiText(hint, 12);
            DrawUiText(hint, barX + Math.Max(4, (barW - hw) / 2),
                barY + Math.Max(4, (barH - UiTheme.S(12)) / 2), 12, UiTheme.TextMuted);
            return;
        }

        const int fontSize = 12;
        var iconSize = UiTheme.S(16);
        var sep = " · ";
        var sepW = MeasureUiText(sep, fontSize);
        var nameW = MeasureUiText(entry.Label, fontSize);
        var moneyLabel = $"+${money}";
        var moneyW = MeasureUiText(moneyLabel, fontSize);

        var matChunks = new List<(string Qty, int QtyW, string ItemId, int Amount)>();
        var matsW = 0;
        foreach (var mat in materials.Where(m => m.Amount > 0))
        {
            var qty = $"×{mat.Amount}";
            var qw = MeasureUiText(qty, fontSize);
            matChunks.Add((qty, qw, mat.ItemId, mat.Amount));
            matsW += sepW + qw + UiTheme.S(2) + iconSize;
        }

        var totalW = nameW + matsW + sepW + moneyW;
        var x = barX + Math.Max(UiTheme.S(6), (barW - totalW) / 2);
        var textY = barY + Math.Max(2, (barH - UiTheme.S(fontSize)) / 2);
        var iconY = barY + Math.Max(2, (barH - iconSize) / 2);

        DrawUiText(entry.Label, x, textY, fontSize, UiTheme.TextPrimary);
        x += nameW;

        var canAfford = wallet.CanAfford(money, materials);
        foreach (var (qty, qw, itemId, amount) in matChunks)
        {
            DrawUiText(sep, x, textY, fontSize, UiTheme.TextMuted);
            x += sepW;
            var qtyColor = wallet.MaterialCount(itemId) >= amount
                ? UiTheme.TextPrimary
                : new Color(220, 120, 100, 255);
            DrawUiText(qty, x, textY, fontSize, qtyColor);
            x += qw + UiTheme.S(2);
            Raylib.DrawRectangle(x - 1, iconY - 1, iconSize + 2, iconSize + 2, new Color(24, 28, 30, 255));
            UiTheme.DrawItemIcon(itemId, x, iconY, iconSize);
            x += iconSize;
        }

        DrawUiText(sep, x, textY, fontSize, UiTheme.TextMuted);
        x += sepW;
        DrawUiText(moneyLabel, x, textY, fontSize,
            canAfford ? UiTheme.MoneyGreen : new Color(220, 120, 100, 255));
    }

    /// <summary>Self-test: resolve crafting recipe for a dock entry id.</summary>
    internal static bool TryResolveDockEntryRecipeForTest(
        string entryId,
        RecipeDefinition smeltRecipe,
        RecipeDefinition wireRecipe,
        out RecipeDefinition? recipe)
    {
        var entry = UiTheme.BuildCategories
            .SelectMany(UiTheme.EntriesFor)
            .FirstOrDefault(e => e.Id == entryId);
        if (entry is null)
        {
            recipe = null;
            return false;
        }

        return TryResolveDockEntryRecipe(entry, smeltRecipe, wireRecipe, out recipe);
    }

    private static bool TryResolveDockEntryRecipe(
        UiTheme.DockEntry entry,
        RecipeDefinition smeltRecipe,
        RecipeDefinition wireRecipe,
        out RecipeDefinition? recipe)
    {
        recipe = entry.Id switch
        {
            "smelter" => smeltRecipe,
            "assembler" => wireRecipe,
            _ => null
        };
        return recipe is not null;
    }

    /// <summary>Self-test / layout helper: resolve money + materials for a dock entry id.</summary>
    internal static bool TryResolveDockEntryCostForTest(
        string entryId,
        ConveyorDefinition basicConveyor,
        ConveyorDefinition fastConveyor,
        ConveyorDefinition expressConveyor,
        ConveyorDefinition junctionConveyor,
        ConveyorDefinition splitterConveyor,
        ConveyorDefinition sorterConveyor,
        ConveyorDefinition bridgeConveyor,
        BuildingDefinition minerBuilding,
        BuildingDefinition advancedMinerBuilding,
        BuildingDefinition smelterBuilding,
        BuildingDefinition assemblerBuilding,
        BuildingDefinition generatorBuilding,
        BuildingDefinition powerNodeBuilding,
        BuildingDefinition powerNodeT2Building,
        out int money,
        out IReadOnlyList<ResourceAmount> materials)
    {
        var entry = UiTheme.BuildCategories
            .SelectMany(UiTheme.EntriesFor)
            .FirstOrDefault(e => e.Id == entryId);
        if (entry is null)
        {
            money = 0;
            materials = Array.Empty<ResourceAmount>();
            return false;
        }

        return TryResolveDockEntryCost(
            entry, basicConveyor, fastConveyor, expressConveyor, junctionConveyor, splitterConveyor, sorterConveyor, bridgeConveyor,
            minerBuilding, advancedMinerBuilding, smelterBuilding, assemblerBuilding, generatorBuilding,
            powerNodeBuilding, powerNodeT2Building,
            out money, out materials);
    }

    private static bool TryResolveDockEntryCost(
        UiTheme.DockEntry entry,
        ConveyorDefinition basicConveyor,
        ConveyorDefinition fastConveyor,
        ConveyorDefinition expressConveyor,
        ConveyorDefinition junctionConveyor,
        ConveyorDefinition splitterConveyor,
        ConveyorDefinition sorterConveyor,
        ConveyorDefinition bridgeConveyor,
        BuildingDefinition minerBuilding,
        BuildingDefinition advancedMinerBuilding,
        BuildingDefinition smelterBuilding,
        BuildingDefinition assemblerBuilding,
        BuildingDefinition generatorBuilding,
        BuildingDefinition powerNodeBuilding,
        BuildingDefinition powerNodeT2Building,
        out int money,
        out IReadOnlyList<ResourceAmount> materials)
    {
        money = 0;
        materials = Array.Empty<ResourceAmount>();

        switch (entry.Id)
        {
            case "miner":
                money = minerBuilding.MoneyCost;
                materials = minerBuilding.BuildCost;
                return true;
            case "miner-advanced":
                money = advancedMinerBuilding.MoneyCost;
                materials = advancedMinerBuilding.BuildCost;
                return true;
            case "smelter":
                money = smelterBuilding.MoneyCost;
                materials = smelterBuilding.BuildCost;
                return true;
            case "assembler":
                money = assemblerBuilding.MoneyCost;
                materials = assemblerBuilding.BuildCost;
                return true;
            case "generator":
                money = generatorBuilding.MoneyCost;
                materials = generatorBuilding.BuildCost;
                return true;
            case "power-node":
                money = powerNodeBuilding.MoneyCost;
                materials = powerNodeBuilding.BuildCost;
                return true;
            case "power-node-t2":
                money = powerNodeT2Building.MoneyCost;
                materials = powerNodeT2Building.BuildCost;
                return true;
            case "conveyor-basic":
                money = basicConveyor.MoneyCost;
                materials = basicConveyor.BuildCost;
                return true;
            case "conveyor-fast":
                money = fastConveyor.MoneyCost;
                materials = fastConveyor.BuildCost;
                return true;
            case "conveyor-express":
                money = expressConveyor.MoneyCost;
                materials = expressConveyor.BuildCost;
                return true;
            case "junction":
                money = junctionConveyor.MoneyCost;
                materials = junctionConveyor.BuildCost;
                return true;
            case "splitter":
                money = splitterConveyor.MoneyCost;
                materials = splitterConveyor.BuildCost;
                return true;
            case "sorter":
                money = sorterConveyor.MoneyCost;
                materials = sorterConveyor.BuildCost;
                return true;
            case "bridge":
                // Bridge places two heads — mirror placement cost.
                money = bridgeConveyor.MoneyCost * 2;
                materials = bridgeConveyor.BuildCost
                    .Select(c => new ResourceAmount(c.ItemId, c.Amount * 2))
                    .ToArray();
                return true;
            default:
                return false;
        }
    }

    private static bool IsDockEntrySelected(
        UiTheme.DockEntry entry,
        ConveyorDefinition selectedConveyor,
        Direction direction,
        BuildTool tool)
    {
        if (DockSelectedId == entry.Id)
        {
            return true;
        }

        return entry.Kind switch
        {
            UiTheme.DockEntryKind.ConveyorVariant =>
                tool == BuildTool.Conveyor && entry.ConveyorId == selectedConveyor.Id,
            UiTheme.DockEntryKind.BuildTool => entry.Tool == tool,
            _ => false
        };
    }

    private static void DrawDockGlyph(UiTheme.DockEntry entry, int cx, int cy, bool locked)
    {
        var color = locked ? UiTheme.TextMuted : entry.Id switch
        {
            "miner" => new Color(210, 150, 70, 255),
            "smelter" => new Color(220, 110, 70, 255),
            "assembler" => new Color(160, 140, 210, 255),
            "conveyor-basic" => new Color(120, 170, 210, 255),
            "conveyor-fast" => new Color(90, 200, 220, 255),
            "conveyor-express" => new Color(70, 230, 200, 255),
            "miner-advanced" => new Color(100, 170, 220, 255),
            "junction" => new Color(140, 180, 140, 255),
            "splitter" => new Color(170, 190, 110, 255),
            "sorter" => new Color(200, 160, 90, 255),
            "bridge" => new Color(150, 160, 200, 255),
            "generator" => new Color(230, 200, 70, 255),
            "power-node" => new Color(210, 175, 60, 255),
            "power-node-t2" => new Color(120, 200, 220, 255),
            "remove" => new Color(220, 100, 90, 255),
            _ => UiTheme.TextPrimary
        };

        UiTheme.DrawDockEntryIcon(entry.Id, cx, cy, UiTheme.DockCellSize, color);
        var label = UiTheme.DockEntryShortLabel(entry);
        var lw = MeasureUiText(label, 9);
        DrawUiText(label, cx + (UiTheme.DockCellSize - lw) / 2,
            cy + UiTheme.DockCellSize - UiTheme.S(12), 9,
            locked ? UiTheme.TextMuted : UiTheme.TextPrimary);
    }

    private static string FormatBuildingCost(BuildingDefinition building)
    {
        var plates = building.BuildCost.FirstOrDefault(entry => entry.ItemId == "iron-plate")?.Amount ?? 0;
        var wires = building.BuildCost.FirstOrDefault(entry => entry.ItemId == "copper-wire")?.Amount ?? 0;
        if (wires > 0)
        {
            return $"${building.MoneyCost} + {plates} P + {wires} F · rimborso {building.RefundPercent}%";
        }

        return $"${building.MoneyCost} + {plates} P · rimborso {building.RefundPercent}%";
    }

    private static string FormatConveyorCost(ConveyorDefinition definition, ResearchState research)
    {
        if (!research.IsUnlocked(definition.Id))
        {
            return "Sblocca in Ricerca (T)";
        }

        var plates = definition.BuildCost.FirstOrDefault(entry => entry.ItemId == "iron-plate")?.Amount ?? 0;
        return $"${definition.MoneyCost} + {plates} P · T{definition.Tier} · rimborso 100%";
    }

    private static void DrawWorld(FactoryWorld world, ConveyorGrid conveyors, WorldCamera camera)
    {
        Raylib.BeginScissorMode(ViewportLeft, ViewportTop, (int)ViewportWidth, (int)ViewportHeight);
        camera.GetVisibleTileRange(
            ViewportWidth,
            ViewportHeight,
            world.Terrain.Width,
            world.Terrain.Height,
            BaseTileSize,
            out var minX,
            out var minY,
            out var maxX,
            out var maxY);

        var tileSize = camera.TileSize(BaseTileSize);
        var originScreen = camera.WorldToScreen(minX * BaseTileSize, minY * BaseTileSize, ViewportLeft, ViewportTop);
        for (var y = minY; y <= maxY; y++)
        {
            var rowScreenY = originScreen.Y + (y - minY) * tileSize;
            for (var x = minX; x <= maxX; x++)
            {
                var screenX = originScreen.X + (x - minX) * tileSize;
                DrawTerrainTile(world.Terrain[x, y], screenX, rowScreenY, tileSize, x, y);
            }
        }

        foreach (var conveyor in conveyors.Cells.Values)
        {
            if (conveyor.Position.X < minX || conveyor.Position.X > maxX
                || conveyor.Position.Y < minY || conveyor.Position.Y > maxY)
            {
                continue;
            }

            var screen = camera.WorldToScreen(
                conveyor.Position.X * BaseTileSize,
                conveyor.Position.Y * BaseTileSize,
                ViewportLeft,
                ViewportTop);
            DrawConveyor(conveyor, conveyors, world, screen.X, screen.Y, tileSize, false);
        }

        foreach (var node in world.PowerNodes.Values)
        {
            if (node.Position.X + node.Size < minX || node.Position.X > maxX
                || node.Position.Y + node.Size < minY || node.Position.Y > maxY)
            {
                continue;
            }

            var screen = camera.WorldToScreen(
                node.Position.X * BaseTileSize,
                node.Position.Y * BaseTileSize,
                ViewportLeft,
                ViewportTop);
            DrawPowerNode(node, screen.X, screen.Y, tileSize, false);
        }

        DrawPowerBeams(world, camera, tileSize);

        DrawCore(world, camera, tileSize, minX, minY, maxX, maxY);
        foreach (var smelter in world.Smelters.Values)
        {
            if (smelter.Position.X + SmelterBuilding.Size < minX || smelter.Position.X > maxX
                || smelter.Position.Y + SmelterBuilding.Size < minY || smelter.Position.Y > maxY)
            {
                continue;
            }

            var screen = camera.WorldToScreen(
                smelter.Position.X * BaseTileSize,
                smelter.Position.Y * BaseTileSize,
                ViewportLeft,
                ViewportTop);
            DrawSmelter(smelter, screen.X, screen.Y, tileSize, false);
        }

        foreach (var assembler in world.Assemblers.Values)
        {
            if (assembler.Position.X + SmelterBuilding.Size < minX || assembler.Position.X > maxX
                || assembler.Position.Y + SmelterBuilding.Size < minY || assembler.Position.Y > maxY)
            {
                continue;
            }

            var screen = camera.WorldToScreen(
                assembler.Position.X * BaseTileSize,
                assembler.Position.Y * BaseTileSize,
                ViewportLeft,
                ViewportTop);
            DrawAssembler(assembler, screen.X, screen.Y, tileSize, false);
        }

        foreach (var generator in world.Generators.Values)
        {
            if (generator.Position.X + GeneratorBuilding.Size < minX || generator.Position.X > maxX
                || generator.Position.Y + GeneratorBuilding.Size < minY || generator.Position.Y > maxY)
            {
                continue;
            }

            var screen = camera.WorldToScreen(
                generator.Position.X * BaseTileSize,
                generator.Position.Y * BaseTileSize,
                ViewportLeft,
                ViewportTop);
            DrawGenerator(generator, screen.X, screen.Y, tileSize, false);
        }

        foreach (var miner in world.Miners.Values)
        {
            if (miner.Position.X + MinerBuilding.Size < minX || miner.Position.X > maxX
                || miner.Position.Y + MinerBuilding.Size < minY || miner.Position.Y > maxY)
            {
                continue;
            }

            var screen = camera.WorldToScreen(
                miner.Position.X * BaseTileSize,
                miner.Position.Y * BaseTileSize,
                ViewportLeft,
                ViewportTop);
            DrawMiner(miner, screen.X, screen.Y, tileSize, false);
        }

        DrawBuildingIoOverlays(world, conveyors, camera, tileSize, minX, minY, maxX, maxY);

        // Items on top of buildings so ores stay visible while moving.
        DrawConveyorItems(conveyors, camera, tileSize, minX, minY, maxX, maxY);

        Raylib.EndScissorMode();
    }

    /// <summary>
    /// Tint perimeter belts by role: amber = outward output, cyan = inward input.
    /// Matches runtime belt-uscente / AcceptFromBelts rules (not fixed building facing).
    /// </summary>
    private static void DrawBuildingIoOverlays(
        FactoryWorld world,
        ConveyorGrid conveyors,
        WorldCamera camera,
        float tileSize,
        int minX,
        int minY,
        int maxX,
        int maxY)
    {
        void DrawForFootprint(GridPosition origin, int size)
        {
            foreach (var (neighbor, _) in BuildingIo.PerimeterSlots(origin, size))
            {
                if (neighbor.X < minX || neighbor.X > maxX || neighbor.Y < minY || neighbor.Y > maxY)
                {
                    continue;
                }

                if (!conveyors.Cells.TryGetValue(neighbor, out var belt))
                {
                    continue;
                }

                Color? tint = null;
                if (BuildingIo.IsOutwardBelt(belt, origin, size))
                {
                    tint = new Color(235, 170, 70, 90);
                }
                else if (BuildingIo.IsInwardBelt(belt, origin, size))
                {
                    tint = new Color(70, 190, 210, 90);
                }

                if (tint is null)
                {
                    continue;
                }

                var screen = camera.WorldToScreen(
                    neighbor.X * BaseTileSize,
                    neighbor.Y * BaseTileSize,
                    ViewportLeft,
                    ViewportTop);
                var sizePx = (int)tileSize;
                Raylib.DrawRectangle((int)screen.X + 1, (int)screen.Y + 1, sizePx - 2, sizePx - 2, tint.Value);
                var outline = BuildingIo.IsOutwardBelt(belt, origin, size)
                    ? new Color(235, 170, 70, 200)
                    : new Color(70, 190, 210, 200);
                Raylib.DrawRectangleLines(
                    (int)screen.X + 1,
                    (int)screen.Y + 1,
                    sizePx - 2,
                    sizePx - 2,
                    outline);
            }
        }

        foreach (var miner in world.Miners.Values)
        {
            DrawForFootprint(miner.Position, MinerBuilding.Size);
        }

        foreach (var smelter in world.Smelters.Values)
        {
            DrawForFootprint(smelter.Position, SmelterBuilding.Size);
        }

        foreach (var assembler in world.Assemblers.Values)
        {
            DrawForFootprint(assembler.Position, SmelterBuilding.Size);
        }
    }

    private static void DrawConveyorItems(
        ConveyorGrid conveyors,
        WorldCamera camera,
        float tileSize,
        int minX,
        int minY,
        int maxX,
        int maxY)
    {
        foreach (var conveyor in conveyors.Cells.Values)
        {
            if (conveyor.Items.Count == 0
                || conveyor.Position.X < minX || conveyor.Position.X > maxX
                || conveyor.Position.Y < minY || conveyor.Position.Y > maxY)
            {
                continue;
            }

            var screen = camera.WorldToScreen(
                conveyor.Position.X * BaseTileSize,
                conveyor.Position.Y * BaseTileSize,
                ViewportLeft,
                ViewportTop);
            DrawConveyorItemChips(conveyor, screen.X, screen.Y, tileSize, 255);
        }
    }

    private static void DrawConveyorItemChips(
        ConveyorCell conveyor,
        float fx,
        float fy,
        float tileSize,
        int alpha)
    {
        var x = (int)fx;
        var y = (int)fy;
        var center = new Vector2(x + tileSize / 2f, y + tileSize / 2f);
        // Travel along routed exit (junctions/splitters) so chips match simulation.
        var vector = DirectionVector(conveyor.RoutedExit);
        foreach (var item in conveyor.Items)
        {
            var itemPosition = center - vector * (tileSize * 0.5f) + vector * (item.Progress * tileSize);
            var itemSize = Math.Max(12, (int)(20 * tileSize / BaseTileSize));
            var ix = (int)itemPosition.X - itemSize / 2;
            var iy = (int)itemPosition.Y - itemSize / 2;
            var fill = UiTheme.ItemColor(item.ItemId);
            Raylib.DrawRectangle(ix - 2, iy - 2, itemSize + 4, itemSize + 4, new Color(8, 10, 10, alpha));
            Raylib.DrawRectangle(ix, iy, itemSize, itemSize, fill);
            Raylib.DrawRectangleLines(ix, iy, itemSize, itemSize, UiTheme.ItemOutline(item.ItemId));
            if (tileSize >= 10f && GameIcons.Has(item.ItemId))
            {
                var inset = Math.Max(2, itemSize / 6);
                // White glyph on saturated chip — same-tint-as-fill made ferro/fili unreadable.
                UiTheme.DrawItemIcon(
                    item.ItemId,
                    ix + inset,
                    iy + inset,
                    itemSize - inset * 2,
                    new Color(255, 255, 255, alpha));
            }
            else if (tileSize >= 14f)
            {
                var abbrev = UiTheme.ItemAbbrev(item.ItemId);
                var fontSize = Math.Max(10, Math.Min(16, itemSize - 4));
                var abbrevWidth = MeasureUiText(abbrev, fontSize);
                DrawUiText(
                    abbrev,
                    (int)itemPosition.X - abbrevWidth / 2,
                    (int)itemPosition.Y - fontSize / 2,
                    fontSize,
                    new Color(18, 16, 12, 255));
            }
        }
    }

    private static void DrawTerrainTile(TerrainTile tile, float x, float y, float tileSize, int worldX, int worldY)
    {
        // Integer pixel bounds from floor→next floor keep cells flush (no muddy float gaps).
        var ix = (int)MathF.Floor(x);
        var iy = (int)MathF.Floor(y);
        var size = Math.Max(1, (int)MathF.Floor(x + tileSize) - ix);
        var sizeY = Math.Max(1, (int)MathF.Floor(y + tileSize) - iy);
        var color = WorldGraphics.TerrainColor(tile.Terrain, worldX, worldY);
        Raylib.DrawRectangle(ix, iy, size, sizeY, color);
        WorldGraphics.DrawTerrainDetail(tile.Terrain, ix, iy, size, sizeY, tileSize, worldX, worldY);
        if (tileSize >= 14f)
        {
            Raylib.DrawRectangleLines(ix, iy, size, sizeY, TerrainGrid);
        }

        if (tile.Deposit == DepositKind.Iron && tileSize >= 8f)
        {
            var s = tileSize / BaseTileSize;
            var iconSize = Math.Max(10, (int)(18 * s));
            if (tileSize >= 16f && GameIcons.Has("iron-ore"))
            {
                UiTheme.DrawItemIcon("iron-ore", ix + (size - iconSize) / 2, iy + (sizeY - iconSize) / 2, iconSize);
            }
            else
            {
                var fill = UiTheme.ItemColor("iron-ore");
                Raylib.DrawCircle(ix + (int)(10 * s), iy + (int)(12 * s), Math.Max(2f, 5 * s), fill);
                Raylib.DrawCircle(ix + (int)(24 * s), iy + (int)(21 * s), Math.Max(2.5f, 7 * s), fill);
            }
        }
        else if (tile.Deposit == DepositKind.Copper && tileSize >= 8f)
        {
            var s = tileSize / BaseTileSize;
            var iconSize = Math.Max(10, (int)(18 * s));
            if (tileSize >= 16f && GameIcons.Has("copper-ore"))
            {
                UiTheme.DrawItemIcon("copper-ore", ix + (size - iconSize) / 2, iy + (sizeY - iconSize) / 2, iconSize);
            }
            else
            {
                var fill = UiTheme.ItemColor("copper-ore");
                Raylib.DrawCircle(ix + (int)(11 * s), iy + (int)(13 * s), Math.Max(2f, 5 * s), fill);
                Raylib.DrawCircle(ix + (int)(23 * s), iy + (int)(20 * s), Math.Max(2.5f, 7 * s), fill);
            }
        }
        else if (tile.Deposit == DepositKind.Coal && tileSize >= 8f)
        {
            var s = tileSize / BaseTileSize;
            var iconSize = Math.Max(10, (int)(18 * s));
            if (tileSize >= 16f && GameIcons.Has("coal"))
            {
                UiTheme.DrawItemIcon("coal", ix + (size - iconSize) / 2, iy + (sizeY - iconSize) / 2, iconSize);
            }
            else
            {
                var fill = UiTheme.ItemColor("coal");
                Raylib.DrawCircle(ix + (int)(10 * s), iy + (int)(12 * s), Math.Max(2f, 5 * s), fill);
                Raylib.DrawCircle(ix + (int)(24 * s), iy + (int)(21 * s), Math.Max(2.5f, 7 * s), fill);
            }
        }
    }

    private static void DrawCore(
        FactoryWorld world,
        WorldCamera camera,
        float tileSize,
        int minX,
        int minY,
        int maxX,
        int maxY)
    {
        var left = world.CoreOrigin.X;
        var top = world.CoreOrigin.Y;
        if (left + FactoryWorld.CoreSize < minX || left > maxX || top + FactoryWorld.CoreSize < minY || top > maxY)
        {
            return;
        }

        var screen = camera.WorldToScreen(left * BaseTileSize, top * BaseTileSize, ViewportLeft, ViewportTop);
        var x = (int)screen.X;
        var y = (int)screen.Y;
        var size = (int)(tileSize * FactoryWorld.CoreSize);
        WorldGraphics.DrawCoreSilhouette(x, y, size, tileSize, DrawBuildingNameplate);
    }

    private static void DrawMiner(MinerBuilding miner, float fx, float fy, float tileSize, bool preview)
    {
        var x = (int)fx;
        var y = (int)fy;
        var size = (int)(tileSize * MinerBuilding.Size);
        WorldGraphics.DrawMinerSilhouette(
            x, y, size, miner.Progress, miner.Efficiency, preview, tileSize, DrawBuildingNameplate,
            isAdvanced: miner.IsAdvanced);
    }

    private static void DrawSmelter(SmelterBuilding smelter, float fx, float fy, float tileSize, bool preview)
    {
        var x = (int)fx;
        var y = (int)fy;
        var size = (int)(tileSize * SmelterBuilding.Size);
        WorldGraphics.DrawSmelterSilhouette(
            x, y, size, smelter.Progress, smelter.IsCrafting, smelter.Direction, preview, tileSize,
            DrawBuildingNameplate, DrawDirectionMark);
    }

    private static void DrawAssembler(SmelterBuilding assembler, float fx, float fy, float tileSize, bool preview)
    {
        var x = (int)fx;
        var y = (int)fy;
        var size = (int)(tileSize * SmelterBuilding.Size);
        WorldGraphics.DrawAssemblerSilhouette(
            x, y, size, assembler.Progress, assembler.IsCrafting, assembler.Direction, preview, tileSize,
            DrawBuildingNameplate, DrawDirectionMark);
    }

    private static void DrawGenerator(GeneratorBuilding generator, float fx, float fy, float tileSize, bool preview)
    {
        var x = (int)fx;
        var y = (int)fy;
        var size = (int)(tileSize * GeneratorBuilding.Size);
        WorldGraphics.DrawGeneratorSilhouette(
            x, y, size, preview, tileSize, DrawBuildingNameplate,
            fuelBuffer: generator.FuelBuffer,
            isGenerating: generator.IsGenerating);
    }

    private static void DrawPowerNode(
        PowerNodeBuilding node,
        float fx,
        float fy,
        float tileSize,
        bool preview)
    {
        var x = (int)fx;
        var y = (int)fy;
        var size = (int)(tileSize * node.Size);
        var alpha = preview ? 160 : 255;
        var isT2 = node.DefinitionId == PowerNodeBuilding.Tier2Id;
        var copper = isT2
            ? new Color(120, 200, 220, alpha)
            : new Color(230, 190, 70, alpha);
        var core = isT2
            ? new Color(200, 240, 250, alpha)
            : new Color(255, 230, 120, alpha);
        var baseColor = new Color(28, 32, 36, alpha);
        var pad = Math.Max(4, (int)(size * 0.18f));
        var cx = x + size / 2;
        var cy = y + size / 2;

        Raylib.DrawRectangle(x + 2, y + 2, size - 4, size - 4, baseColor);
        Raylib.DrawRectangleLines(x + 2, y + 2, size - 4, size - 4, copper);
        // Cross arms
        var thickness = Math.Max(2, (int)(size * 0.1f));
        Raylib.DrawRectangle(cx - thickness / 2, y + pad, thickness, size - pad * 2, copper);
        Raylib.DrawRectangle(x + pad, cy - thickness / 2, size - pad * 2, thickness, copper);
        // Center pole
        var pole = Math.Max(4, (int)(size * 0.28f));
        Raylib.DrawRectangle(cx - pole / 2, cy - pole / 2, pole, pole, copper);
        Raylib.DrawRectangle(cx - pole / 4, cy - pole / 4, Math.Max(1, pole / 2), Math.Max(1, pole / 2), core);

        if (!preview)
        {
            var iconSize = Math.Max(10, (int)(size * 0.45f));
            GameIcons.TryDraw(node.DefinitionId, cx - iconSize / 2, cy - iconSize / 2, iconSize, Color.White);
        }
    }

    private static void DrawPowerBeams(FactoryWorld world, WorldCamera camera, float tileSize)
    {
        foreach (var link in world.PowerLinks)
        {
            if (!TryPowerEndpointCenter(world, link.A, out var a)
                || !TryPowerEndpointCenter(world, link.B, out var b))
            {
                continue;
            }

            var sa = camera.WorldToScreen(a.X * BaseTileSize, a.Y * BaseTileSize, ViewportLeft, ViewportTop);
            var sb = camera.WorldToScreen(b.X * BaseTileSize, b.Y * BaseTileSize, ViewportLeft, ViewportTop);
            var live = world.PowerNetworks.IsLinkLive(link);
            var color = live
                ? new Color(255, 210, 90, 200)
                : new Color(90, 80, 50, 110);
            var thickness = live ? Math.Max(1.5f, tileSize * 0.06f) : Math.Max(1f, tileSize * 0.04f);
            Raylib.DrawLineEx(sa, sb, thickness, color);
            if (live)
            {
                Raylib.DrawLineEx(sa, sb, Math.Max(1f, thickness * 0.35f), new Color(255, 245, 180, 160));
            }
        }
    }

    private static bool TryPowerEndpointCenter(FactoryWorld world, PowerEndpointId id, out Vector2 center)
    {
        switch (id.Kind)
        {
            case PowerEndpointKind.Core:
                center = new Vector2(
                    world.CoreOrigin.X + FactoryWorld.CoreSize * 0.5f,
                    world.CoreOrigin.Y + FactoryWorld.CoreSize * 0.5f);
                return true;
            case PowerEndpointKind.Generator:
                center = new Vector2(
                    id.Origin.X + GeneratorBuilding.Size * 0.5f,
                    id.Origin.Y + GeneratorBuilding.Size * 0.5f);
                return world.Generators.ContainsKey(id.Origin);
            case PowerEndpointKind.Node:
                if (!world.PowerNodes.TryGetValue(id.Origin, out var node))
                {
                    center = default;
                    return false;
                }

                center = new Vector2(id.Origin.X + node.Size * 0.5f, id.Origin.Y + node.Size * 0.5f);
                return true;
            case PowerEndpointKind.Consumer:
                center = new Vector2(
                    id.Origin.X + SmelterBuilding.Size * 0.5f,
                    id.Origin.Y + SmelterBuilding.Size * 0.5f);
                return world.Smelters.ContainsKey(id.Origin) || world.Assemblers.ContainsKey(id.Origin);
            default:
                center = default;
                return false;
        }
    }

    /// <summary>
    /// Nameplate helper for world silhouettes.
    /// Efficiency / CORE: <paramref name="x"/> is center. Name tags: <paramref name="x"/> is text left.
    /// </summary>
    private static void DrawBuildingNameplate(string text, int x, int y, int alpha, Color color)
    {
        var fontSize = text is "FORNO" or "ASSY" or "GEN" || text.StartsWith("GEN ", StringComparison.Ordinal)
            ? 13
            : text == "CORE" ? Math.Max(10, 18) : 12;
        var width = MeasureUiText(text, fontSize);
        if (text.Contains('%', StringComparison.Ordinal) || text == "CORE")
        {
            var left = x - width / 2;
            if (text.Contains('%', StringComparison.Ordinal))
            {
                Raylib.DrawRectangle(left - 4, y - 2, width + 8, 16, new Color(20, 24, 23, alpha));
            }

            DrawUiText(text, left, y, fontSize, color);
            return;
        }

        Raylib.DrawRectangle(x - 4, y - 2, width + 8, 16, new Color(16, 14, 12, Math.Clamp(alpha - 40, 120, 200)));
        DrawUiText(text, x, y, fontSize, color);
    }

    private static void DrawConveyor(
        ConveyorCell conveyor,
        ConveyorGrid conveyors,
        FactoryWorld world,
        float fx,
        float fy,
        float tileSize,
        bool preview)
    {
        var alpha = preview ? 145 : 255;
        var x = (int)fx;
        var y = (int)fy;
        var size = (int)tileSize;
        var center = new Vector2(x + tileSize / 2f, y + tileSize / 2f);

        switch (conveyor.Kind)
        {
            case LogisticsKind.Junction:
                DrawJunctionGlyph(center, alpha, tileSize);
                break;
            case LogisticsKind.Splitter:
                DrawSplitterGlyph(center, conveyor.Direction, alpha, tileSize);
                break;
            case LogisticsKind.Sorter:
                DrawSorterGlyph(center, conveyor.Direction, conveyor.FilterItemId, alpha, tileSize);
                break;
            case LogisticsKind.Bridge:
                DrawBridgeGlyph(conveyor, fx, fy, tileSize, alpha);
                break;
            default:
                WorldGraphics.DrawBeltTrack(x, y, size, alpha, conveyor.Definition.Tier >= 2);
                foreach (var connectedDirection in Directions)
                {
                    if (IsConnected(conveyor, connectedDirection, conveyors, world))
                    {
                        DrawConveyorArm(center, connectedDirection, alpha, tileSize);
                    }
                }

                DrawConveyorArm(center, conveyor.Direction, alpha, tileSize);
                // Re-draw center plate so arms sit under the track face.
                Raylib.DrawRectangle(x + size / 4, y + size / 4, size / 2, size / 2,
                    conveyor.Definition.Tier >= 2
                        ? new Color(56, 92, 110, alpha)
                        : new Color(70, 77, 74, alpha));
                Raylib.DrawRectangle(x + size / 4, y + size / 4, size / 2, Math.Max(1, size / 20),
                    conveyor.Definition.Tier >= 2
                        ? new Color(90, 140, 160, alpha)
                        : new Color(100, 110, 105, alpha));
                WorldGraphics.DrawFlowChevrons(center, conveyor.Direction, alpha, tileSize);
                DrawDirectionMark(center, conveyor.Direction, alpha, tileSize);
                break;
        }

        // Items are drawn in a later world pass (DrawConveyorItems) so buildings never hide them.
        if (preview)
        {
            DrawConveyorItemChips(conveyor, fx, fy, tileSize, alpha);
        }
    }

    private static void DrawJunctionGlyph(Vector2 center, int alpha, float tileSize)
    {
        var arm = Math.Max(4, (int)(10 * tileSize / BaseTileSize));
        var span = Math.Max(10, (int)(28 * tileSize / BaseTileSize));
        Raylib.DrawRectangle((int)center.X - span / 2, (int)center.Y - arm / 2, span, arm, new Color(70, 88, 92, alpha));
        Raylib.DrawRectangle((int)center.X - arm / 2, (int)center.Y - span / 2, arm, span, new Color(70, 88, 92, alpha));
        Raylib.DrawRectangle((int)center.X - arm, (int)center.Y - arm, arm * 2, arm * 2, new Color(120, 160, 150, alpha));
    }

    private static void DrawSplitterGlyph(Vector2 center, Direction direction, int alpha, float tileSize)
    {
        var x = (int)(center.X - tileSize / 2f);
        var y = (int)(center.Y - tileSize / 2f);
        var size = (int)tileSize;
        var left = DirectionMath.Left(direction);
        var right = DirectionMath.Right(direction);
        var input = DirectionMath.Opposite(direction);

        // Belt-like body so the splitter reads as a directed conveyor, not a special glyph.
        Raylib.DrawRectangle(x + size / 5, y + size / 5, size - size * 2 / 5, size - size * 2 / 5,
            new Color(38, 43, 42, alpha));
        DrawConveyorArm(center, input, alpha, tileSize);
        DrawConveyorArm(center, left, alpha, tileSize);
        DrawConveyorArm(center, right, alpha, tileSize);
        Raylib.DrawRectangle(x + size / 4, y + size / 4, size / 2, size / 2,
            new Color(88, 96, 64, alpha));
        Raylib.DrawRectangle(x + size / 4, y + size / 4, size / 2, Math.Max(1, size / 20),
            new Color(130, 145, 90, alpha));
        // Chevrons along facing (same travel feel as belts); cargo exits L/R at handoff.
        WorldGraphics.DrawFlowChevrons(center, direction, alpha, tileSize);
        DrawDirectionMark(center, direction, alpha, tileSize);
        var tick = tileSize * 0.75f;
        DrawDirectionMark(center + DirectionVector(left) * (8f * tileSize / BaseTileSize), left, alpha, tick);
        DrawDirectionMark(center + DirectionVector(right) * (8f * tileSize / BaseTileSize), right, alpha, tick);
    }

    private static void DrawSorterGlyph(
        Vector2 center,
        Direction direction,
        string? filterItemId,
        int alpha,
        float tileSize)
    {
        var x = (int)(center.X - tileSize / 2f);
        var y = (int)(center.Y - tileSize / 2f);
        var size = (int)tileSize;
        var left = DirectionMath.Left(direction);
        var right = DirectionMath.Right(direction);
        var input = DirectionMath.Opposite(direction);

        Raylib.DrawRectangle(x + size / 5, y + size / 5, size - size * 2 / 5, size - size * 2 / 5,
            new Color(38, 43, 42, alpha));
        DrawConveyorArm(center, input, alpha, tileSize);
        DrawConveyorArm(center, direction, alpha, tileSize);
        DrawConveyorArm(center, left, alpha, tileSize);
        DrawConveyorArm(center, right, alpha, tileSize);
        Raylib.DrawRectangle(x + size / 4, y + size / 4, size / 2, size / 2,
            new Color(120, 92, 48, alpha));
        DrawConveyorFlowChevrons(center, direction, alpha, tileSize);
        DrawDirectionMark(center, direction, alpha, tileSize);
        var tick = tileSize * 0.7f;
        DrawDirectionMark(center + DirectionVector(left) * (8f * tileSize / BaseTileSize), left, alpha, tick);
        DrawDirectionMark(center + DirectionVector(right) * (8f * tileSize / BaseTileSize), right, alpha, tick);

        var filter = filterItemId ?? "iron-ore";
        var iconSize = Math.Max(8, (int)(tileSize * 0.38f));
        var ix = (int)(center.X - iconSize / 2f);
        var iy = (int)(center.Y - iconSize / 2f);
        Raylib.DrawRectangle(ix - 1, iy - 1, iconSize + 2, iconSize + 2, new Color(24, 20, 12, alpha));
        if (tileSize >= 14f && GameIcons.Has(filter))
        {
            UiTheme.DrawItemIcon(filter, ix, iy, iconSize);
        }
        else
        {
            Raylib.DrawRectangle(ix, iy, iconSize, iconSize, UiTheme.ItemColor(filter));
        }
    }

    private static void CycleSorterBrushFilter()
    {
        var index = 0;
        for (var i = 0; i < SorterFilterItemIds.Length; i++)
        {
            if (string.Equals(SorterFilterItemIds[i], SorterBrushFilterId, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        SorterBrushFilterId = SorterFilterItemIds[(index + 1) % SorterFilterItemIds.Length];
    }

    private static void DrawBridgeGlyph(
        ConveyorCell conveyor,
        float fx,
        float fy,
        float tileSize,
        int alpha)
    {
        var center = new Vector2(fx + tileSize / 2f, fy + tileSize / 2f);
        var scale = tileSize / BaseTileSize;
        var forward = DirectionVector(conveyor.Direction);
        var side = new Vector2(-forward.Y, forward.X);
        var baseColor = new Color(96, 118, 148, alpha);
        var archColor = new Color(160, 190, 220, alpha);

        // Base pads
        Raylib.DrawRectangle(
            (int)(center.X - 8 * scale),
            (int)(center.Y - 8 * scale),
            Math.Max(6, (int)(16 * scale)),
            Math.Max(6, (int)(16 * scale)),
            baseColor);

        // Linked arches toward partner / forward
        var tip = center + forward * (14f * scale);
        Raylib.DrawLineEx(center + side * (6 * scale), tip, Math.Max(2f, 3 * scale), archColor);
        Raylib.DrawLineEx(center - side * (6 * scale), tip, Math.Max(2f, 3 * scale), archColor);
        Raylib.DrawCircleV(tip, Math.Max(2f, 3.5f * scale), new Color(200, 220, 240, alpha));

        if (conveyor.BridgePartner is { } partner)
        {
            var partnerCenter = new Vector2(
                fx + (partner.X - conveyor.Position.X) * tileSize + tileSize / 2f,
                fy + (partner.Y - conveyor.Position.Y) * tileSize + tileSize / 2f);
            // Only draw the link from the "entry" side to avoid double-draw
            if (conveyor.Position.X + conveyor.Position.Y * 1000
                <= partner.X + partner.Y * 1000)
            {
                Raylib.DrawLineEx(center, partnerCenter, Math.Max(1.5f, 2.5f * scale),
                    new Color(130, 170, 210, Math.Clamp(alpha - 40, 40, 255)));
            }
        }

        DrawDirectionMark(center, conveyor.Direction, alpha, tileSize);
    }

    private static Color ItemColor(string itemId) => UiTheme.ItemColor(itemId);

    private static string ItemAbbrev(string itemId) => UiTheme.ItemAbbrev(itemId);

    private static void DrawConveyorArm(Vector2 center, Direction direction, int alpha, float tileSize)
    {
        var vector = DirectionVector(direction);
        var edge = center + vector * (tileSize / 2f);
        var horizontal = direction is Direction.East or Direction.West;
        var arm = Math.Max(4, (int)(18 * tileSize / BaseTileSize));
        var left = (int)Math.Min(center.X, edge.X) - (horizontal ? 0 : arm / 2);
        var top = (int)Math.Min(center.Y, edge.Y) - (horizontal ? arm / 2 : 0);
        var width = horizontal ? (int)Math.Abs(edge.X - center.X) + 1 : arm;
        var height = horizontal ? arm : (int)Math.Abs(edge.Y - center.Y) + 1;
        Raylib.DrawRectangle(left, top, width, height, new Color(38, 43, 42, alpha));

        var inset = Math.Max(1, arm / 6);
        var innerLeft = horizontal ? left : left + inset;
        var innerTop = horizontal ? top + inset : top;
        var innerWidth = horizontal ? width : width - inset * 2;
        var innerHeight = horizontal ? height - inset * 2 : height;
        Raylib.DrawRectangle(innerLeft, innerTop, innerWidth, innerHeight, new Color(84, 92, 88, alpha));
    }

    /// <summary>
    /// Animated chevrons that scroll only along <paramref name="direction"/> so belt flow
    /// reads as one-way (facing), not bidirectional.
    /// </summary>
    private static void DrawConveyorFlowChevrons(Vector2 center, Direction direction, int alpha, float tileSize) =>
        WorldGraphics.DrawFlowChevrons(center, direction, alpha, tileSize);

    private static bool IsConnected(
        ConveyorCell conveyor,
        Direction direction,
        ConveyorGrid conveyors,
        FactoryWorld world)
    {
        var neighborPosition = conveyor.Position.Step(direction);
        if (world.IsMinerTile(neighborPosition)
            || world.IsSmelterTile(neighborPosition)
            || world.IsAssemblerTile(neighborPosition))
        {
            return true;
        }

        if (world.CoreTiles.Contains(neighborPosition) && conveyor.OutputPosition == neighborPosition)
        {
            return true;
        }

        return conveyors.Cells.TryGetValue(neighborPosition, out var neighbor)
            && (conveyor.OutputPosition == neighborPosition || neighbor.OutputPosition == conveyor.Position);
    }

    private static void DrawDirectionMark(Vector2 center, Direction direction, int alpha, float tileSize)
    {
        var vector = DirectionVector(direction);
        var side = new Vector2(-vector.Y, vector.X);
        var scale = tileSize / BaseTileSize;
        var point = center + vector * (12f * scale);
        var tip = point + vector * (5.5f * scale);
        var left = point - vector * (5f * scale) + side * (5.5f * scale);
        var right = point - vector * (5f * scale) - side * (5.5f * scale);
        // Soft shadow under arrow for rim readability
        Raylib.DrawTriangle(
            tip + new Vector2(1.2f * scale, 1.2f * scale),
            left + new Vector2(1.2f * scale, 1.2f * scale),
            right + new Vector2(1.2f * scale, 1.2f * scale),
            new Color(12, 14, 12, Math.Clamp(alpha - 40, 80, 200)));
        Raylib.DrawTriangle(tip, left, right, new Color(235, 215, 130, alpha));
    }

    private static void DrawPreview(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        ResearchState research,
        WorldCamera camera,
        ConveyorDefinition selectedConveyor,
        ConveyorDefinition junctionConveyor,
        ConveyorDefinition splitterConveyor,
        ConveyorDefinition sorterConveyor,
        ConveyorDefinition bridgeConveyor,
        RecipeDefinition smeltRecipe,
        RecipeDefinition wireRecipe,
        BuildingDefinition minerBuilding,
        BuildingDefinition advancedMinerBuilding,
        BuildingDefinition smelterBuilding,
        BuildingDefinition assemblerBuilding,
        BuildingDefinition generatorBuilding,
        BuildingDefinition powerNodeBuilding,
        BuildingDefinition powerNodeT2Building,
        BuildTool tool,
        Direction direction)
    {
        var cell = MouseCell(Raylib.GetMousePosition(), camera, world);
        if (cell is not { } position)
        {
            return;
        }

        var screen = camera.WorldToScreen(position.X * BaseTileSize, position.Y * BaseTileSize, ViewportLeft, ViewportTop);
        var tileSize = camera.TileSize(BaseTileSize);
        var logisticsDef = tool switch
        {
            BuildTool.Junction => junctionConveyor,
            BuildTool.Splitter => splitterConveyor,
            BuildTool.Sorter => sorterConveyor,
            BuildTool.Bridge => bridgeConveyor,
            _ => selectedConveyor
        };
        var valid = tool switch
        {
            BuildTool.Conveyor => world.CanPlaceConveyor(position)
                && !conveyors.Cells.ContainsKey(position)
                && research.IsUnlocked(selectedConveyor.Id)
                && wallet.CanAfford(selectedConveyor.MoneyCost, selectedConveyor.BuildCost),
            BuildTool.Junction or BuildTool.Splitter or BuildTool.Sorter => world.CanPlaceConveyor(position)
                && !conveyors.Cells.ContainsKey(position)
                && research.IsUnlocked(logisticsDef.Id)
                && wallet.CanAfford(logisticsDef.MoneyCost, logisticsDef.BuildCost),
            BuildTool.Bridge => world.CanPlaceConveyor(position)
                && !conveyors.Cells.ContainsKey(position)
                && research.IsUnlocked(bridgeConveyor.Id)
                && wallet.CanAfford(bridgeConveyor.MoneyCost * 2,
                    bridgeConveyor.BuildCost.Select(c => new ResourceAmount(c.ItemId, c.Amount * 2)).ToArray()),
            BuildTool.Miner => research.IsUnlocked("miner")
                && world.CanPlaceMiner(position, conveyors)
                && wallet.CanAfford(minerBuilding.MoneyCost, minerBuilding.BuildCost),
            BuildTool.MinerAdvanced => research.IsUnlocked(MinerBuilding.AdvancedId)
                && world.CanPlaceMiner(position, conveyors)
                && wallet.CanAfford(advancedMinerBuilding.MoneyCost, advancedMinerBuilding.BuildCost),
            BuildTool.Smelter => research.IsUnlocked("smelter")
                && world.CanPlaceSmelter(position, conveyors)
                && wallet.CanAfford(smelterBuilding.MoneyCost, smelterBuilding.BuildCost),
            BuildTool.Assembler => research.IsUnlocked("assembler")
                && world.CanPlaceAssembler(position, conveyors)
                && wallet.CanAfford(assemblerBuilding.MoneyCost, assemblerBuilding.BuildCost),
            BuildTool.Generator => research.IsUnlocked("generator")
                && world.CanPlaceGenerator(position, conveyors)
                && wallet.CanAfford(generatorBuilding.MoneyCost, generatorBuilding.BuildCost),
            BuildTool.PowerNode => research.IsUnlocked("power-node")
                && world.CanPlacePowerNode(position, PowerNodeBuilding.Tier1Size, conveyors)
                && wallet.CanAfford(powerNodeBuilding.MoneyCost, powerNodeBuilding.BuildCost),
            BuildTool.PowerNodeT2 => research.IsUnlocked("power-node-t2")
                && world.CanPlacePowerNode(position, PowerNodeBuilding.Tier2Size, conveyors)
                && wallet.CanAfford(powerNodeT2Building.MoneyCost, powerNodeT2Building.BuildCost),
            BuildTool.Remove => world.Miners.ContainsKey(position)
                || world.IsMinerTile(position)
                || world.Smelters.ContainsKey(position)
                || world.IsSmelterTile(position)
                || world.Assemblers.ContainsKey(position)
                || world.IsAssemblerTile(position)
                || world.Generators.ContainsKey(position)
                || world.IsGeneratorTile(position)
                || world.TryGetPowerNodeAt(position, out _)
                || conveyors.Cells.ContainsKey(position),
            _ => false
        };

        if (tool == BuildTool.Conveyor
            && conveyors.Cells.TryGetValue(position, out var existing)
            && selectedConveyor.Tier > existing.Definition.Tier
            && research.IsUnlocked(selectedConveyor.Id)
            && wallet.CanAfford(selectedConveyor.MoneyCost, selectedConveyor.BuildCost))
        {
            valid = true;
        }

        var previewColor = valid
            ? new Color(105, 225, 142, 125)
            : new Color(225, 92, 80, 125);
        var previewSize = tool is BuildTool.Miner or BuildTool.MinerAdvanced or BuildTool.Smelter
            or BuildTool.Assembler or BuildTool.Generator or BuildTool.PowerNodeT2
            ? tileSize * MinerBuilding.Size
            : tool == BuildTool.PowerNode
                ? tileSize * PowerNodeBuilding.Tier1Size
                : tileSize;
        Raylib.BeginScissorMode(ViewportLeft, ViewportTop, (int)ViewportWidth, (int)ViewportHeight);
        Raylib.DrawRectangle((int)screen.X + 2, (int)screen.Y + 2, (int)previewSize - 5, (int)previewSize - 5, previewColor);

        if (tool == BuildTool.Conveyor && valid && !conveyors.Cells.ContainsKey(position))
        {
            var preview = new ConveyorCell(position, direction, selectedConveyor);
            DrawConveyor(preview, conveyors, world, screen.X, screen.Y, tileSize, true);
        }
        else if (tool is BuildTool.Junction or BuildTool.Splitter or BuildTool.Sorter or BuildTool.Bridge
            && valid
            && !conveyors.Cells.ContainsKey(position))
        {
            var preview = new ConveyorCell(
                position,
                direction,
                logisticsDef,
                filterItemId: tool == BuildTool.Sorter ? SorterBrushFilterId : null);
            DrawConveyor(preview, conveyors, world, screen.X, screen.Y, tileSize, true);
        }
        else if (tool == BuildTool.Miner && valid)
        {
            DrawMiner(new MinerBuilding(position, direction, world.CountCoveredDepositTiles(position)),
                screen.X, screen.Y, tileSize, true);
            DrawGhostIoHints(position, MinerBuilding.Size, conveyors, camera, tileSize);
        }
        else if (tool == BuildTool.MinerAdvanced && valid)
        {
            DrawMiner(new MinerBuilding(position, direction, world.CountCoveredDepositTiles(position),
                    definitionId: MinerBuilding.AdvancedId),
                screen.X, screen.Y, tileSize, true);
            DrawGhostIoHints(position, MinerBuilding.Size, conveyors, camera, tileSize);
        }
        else if (tool == BuildTool.Smelter && valid)
        {
            DrawSmelter(new SmelterBuilding(position, direction, smeltRecipe), screen.X, screen.Y, tileSize, true);
            DrawGhostIoHints(position, SmelterBuilding.Size, conveyors, camera, tileSize);
        }
        else if (tool == BuildTool.Assembler && valid)
        {
            DrawAssembler(new SmelterBuilding(position, direction, wireRecipe), screen.X, screen.Y, tileSize, true);
            DrawGhostIoHints(position, SmelterBuilding.Size, conveyors, camera, tileSize);
        }
        else if (tool == BuildTool.Generator && valid)
        {
            DrawGenerator(new GeneratorBuilding(position), screen.X, screen.Y, tileSize, true);
        }
        else if (tool == BuildTool.PowerNode && valid)
        {
            DrawPowerNode(new PowerNodeBuilding(position, PowerNodeBuilding.Tier1Id), screen.X, screen.Y, tileSize, true);
        }
        else if (tool == BuildTool.PowerNodeT2 && valid)
        {
            DrawPowerNode(new PowerNodeBuilding(position, PowerNodeBuilding.Tier2Id), screen.X, screen.Y, tileSize, true);
        }

        Raylib.EndScissorMode();
    }

    /// <summary>
    /// Ghost placement: tint neighboring belts amber (output / uscente) or cyan (input / entrante).
    /// </summary>
    private static void DrawGhostIoHints(
        GridPosition origin,
        int size,
        ConveyorGrid conveyors,
        WorldCamera camera,
        float tileSize)
    {
        foreach (var (neighbor, _) in BuildingIo.PerimeterSlots(origin, size))
        {
            if (!conveyors.Cells.TryGetValue(neighbor, out var belt))
            {
                continue;
            }

            Color tint;
            if (BuildingIo.IsOutwardBelt(belt, origin, size))
            {
                tint = new Color(235, 170, 70, 140);
            }
            else if (BuildingIo.IsInwardBelt(belt, origin, size))
            {
                tint = new Color(70, 190, 210, 140);
            }
            else
            {
                // Sideways belt on perimeter — not a valid I/O until rotated.
                tint = new Color(160, 120, 120, 100);
            }

            var screen = camera.WorldToScreen(
                neighbor.X * BaseTileSize,
                neighbor.Y * BaseTileSize,
                ViewportLeft,
                ViewportTop);
            var sizePx = (int)tileSize;
            Raylib.DrawRectangle((int)screen.X + 2, (int)screen.Y + 2, sizePx - 4, sizePx - 4, tint);
        }
    }

    private static void DrawMercatoPanel(
        FactoryWorld world,
        EconomyWallet wallet,
        MarketCatalog market)
    {
        GetMercatoBounds(out var x, out var y, out var w, out var h);
        Raylib.DrawRectangle(x, y, w, h, UiTheme.PanelFill);
        UiTheme.DrawAccentRect(x, y, w, h, UiTheme.Accent, 2);

        var pad = MercatoS(MercatoPadBase);
        DrawUiText("MERCATO", x + pad, y + MercatoS(8), 15, UiTheme.TextPrimary);
        GameIcons.TryDraw("sell", x + w - pad - MercatoS(20), y + MercatoS(6), MercatoS(20), UiTheme.MoneyGreen);

        var autoSell = ActiveSettings?.AutoSellAtCore ?? false;
        DrawToggleRow(x + pad, MercatoAutoSellY(y), w - pad * 2, MercatoS(24), "Vendita automatica", autoSell);

        DrawUiText(
            autoSell ? "ON: il core liquida subito in $." : "OFF: stock in magazzino; vendi qui.",
            x + pad, MercatoHintY(y), 11, UiTheme.TextMuted);

        var iconSize = MercatoS(22);
        var index = 0;
        foreach (var item in market.Items.Take(4))
        {
            var rowY = MercatoRowY(y, h, index);
            var stock = wallet.MaterialCount(item.ItemId);
            var effective = world.EffectiveSalePrice(item.ItemId, market);
            var shortName = UiTheme.InventoryItems.FirstOrDefault(i => i.ItemId == item.ItemId)?.ShortName
                ?? item.DisplayName;

            GetMercatoSellOneBounds(x, y, w, h, index, out var oneX, out var oneY, out var oneW, out var oneH);
            GetMercatoSellAllBounds(x, y, w, h, index, out var allX, out var allY, out var allW, out var allH);

            Raylib.DrawRectangle(x + pad - 2, rowY, iconSize + 4, iconSize + 4, new Color(24, 28, 30, 255));
            Raylib.DrawRectangleLines(x + pad - 2, rowY, iconSize + 4, iconSize + 4, UiTheme.ItemOutline(item.ItemId));
            UiTheme.DrawItemIcon(item.ItemId, x + pad, rowY + 2, iconSize);

            string priceLabel;
            Color priceColor;
            if (effective != item.SellPrice)
            {
                priceLabel = $"${effective}";
                priceColor = new Color(211, 164, 76, 255);
            }
            else
            {
                priceLabel = $"${item.SellPrice}";
                priceColor = UiTheme.TextPrimary;
            }

            var pw = MeasureUiText(priceLabel, 12);
            var priceGap = MercatoS(6);
            var priceX = oneX - priceGap - pw;
            DrawUiText(priceLabel, priceX, rowY + MercatoS(2), 12, priceColor);

            var nameX = x + pad + iconSize + MercatoS(4);
            var nameMaxW = Math.Max(MercatoS(36), priceX - MercatoS(6) - nameX);
            var name = TruncateUiText(shortName, 12, nameMaxW);
            DrawUiText(name, nameX, rowY + 1, 12, UiTheme.TextPrimary);
            DrawUiText($"stock ×{stock}", nameX, rowY + MercatoS(16), 10,
                stock > 0 ? UiTheme.TextMuted : new Color(120, 110, 100, 255));

            DrawCompactMarketButton(oneX, oneY, oneW, oneH, "1", stock > 0);
            DrawCompactMarketButton(allX, allY, allW, allH, "tutti", stock > 0);
            index++;
        }
    }

    private static void DrawStatusPanel(
        FactoryWorld world,
        ConveyorGrid conveyors,
        ResearchState research,
        EconomyWallet wallet,
        EconomySession session,
        EconomyConfig economy)
    {
        if (!TryGetFabbricaContentMetrics(
                out var x, out var y, out var w, out var h, out var pad,
                out var countsY, out var upgradeX, out var upgradeY, out var upgradeW, out var upgradeH))
        {
            return;
        }

        Raylib.DrawRectangle(x, y, w, h, UiTheme.PanelFill);
        // Same accent column as Mercato so the HUD stack reads as one width.
        UiTheme.DrawAccentRect(x, y, w, h, UiTheme.Accent, 2);

        DrawUiText("FABBRICA", x + pad, y + MercatoS(6), 13, UiTheme.TextMuted);

        var net = session.NetWorthDelta(wallet);
        DrawUiText(
            $"{UiTheme.SessionDeltaLabel(net)}  ·  PWR {world.PowerBuffer:0}/{world.PowerCapacity:0}",
            x + pad, y + MercatoS(24), 11,
            net >= 0 ? UiTheme.MoneyGreen : UiTheme.MoneyRed);

        DrawUiText(
            FormatFabbricaCounts(world, conveyors),
            x + pad, countsY, 11, UiTheme.TextMuted);

        var tip = GetOnboardingTip(world, conveyors, research, wallet);
        var tipMaxH = upgradeY - (y + MercatoS(56)) - MercatoS(4);
        if (tipMaxH >= MercatoS(14))
        {
            DrawWrappedTip(tip, x + pad, y + MercatoS(56), w - pad * 2, tipMaxH);
        }

        var upgrade = economy.CoreUpgrade;
        var upgraded = world.CoreUpgradeLevel > 0;
        if (upgraded)
        {
            DrawButton(upgradeX, upgradeY, upgradeW, upgradeH, $"CORE LV{world.CoreUpgradeLevel}", active: true);
        }
        else
        {
            DrawCoreUpgradeButton(upgradeX, upgradeY, upgradeW, upgradeH, upgrade, wallet);
        }
    }

    /// <summary>
    /// Text label for CORE cost (toast / self-test): money + explicit plate qty, never vague "+ lastre".
    /// </summary>
    internal static string FormatCoreUpgradeCostText(CoreUpgradeDefinition upgrade)
    {
        var mats = FormatCoreUpgradeMaterialsText(upgrade.BuildCost);
        return string.IsNullOrEmpty(mats)
            ? $"CORE ${upgrade.MoneyCost}"
            : $"CORE ${upgrade.MoneyCost} + {mats}";
    }

    /// <summary>Afford-failure toast: includes ×N for every required material.</summary>
    internal static string FormatCoreUpgradeNeedMessage(CoreUpgradeDefinition upgrade)
    {
        var mats = FormatCoreUpgradeMaterialsText(upgrade.BuildCost);
        return string.IsNullOrEmpty(mats)
            ? $"CORE: servono ${upgrade.MoneyCost}."
            : $"CORE: servono ${upgrade.MoneyCost} + {mats}.";
    }

    /// <summary>×N lastre (iron-plate) / ×N itemId for other materials — always with quantity.</summary>
    internal static string FormatCoreUpgradeMaterialsText(IReadOnlyList<ResourceAmount> buildCost)
    {
        var parts = buildCost
            .Where(m => m.Amount > 0)
            .Select(m => m.ItemId switch
            {
                "iron-plate" => $"×{m.Amount} lastre",
                "copper-wire" => $"×{m.Amount} fili",
                "iron-ore" => $"×{m.Amount} ferro",
                "copper-ore" => $"×{m.Amount} rame",
                _ => $"×{m.Amount} {m.ItemId}"
            });
        return string.Join(" + ", parts);
    }

    /// <summary>
    /// Peak-style CORE upgrade affordance: CORE · ×N [icon] · +$cost (matches dock cost footer).
    /// Falls back to compact text if the icon row would overflow the button.
    /// </summary>
    private static void DrawCoreUpgradeButton(
        int x, int y, int width, int height, CoreUpgradeDefinition upgrade, EconomyWallet wallet)
    {
        Raylib.DrawRectangle(x, y, width, height, new Color(45, 52, 50, 255));

        const int fontSize = 12;
        var iconSize = Math.Min(UiTheme.S(14), Math.Max(10, height - UiTheme.S(8)));
        var sep = " · ";
        var sepW = MeasureUiText(sep, fontSize);
        const string name = "CORE";
        var nameW = MeasureUiText(name, fontSize);
        var moneyLabel = $"+${upgrade.MoneyCost}";
        var moneyW = MeasureUiText(moneyLabel, fontSize);

        var matChunks = new List<(string Qty, int QtyW, string ItemId, int Amount)>();
        var matsW = 0;
        foreach (var mat in upgrade.BuildCost.Where(m => m.Amount > 0))
        {
            var qty = $"×{mat.Amount}";
            var qw = MeasureUiText(qty, fontSize);
            matChunks.Add((qty, qw, mat.ItemId, mat.Amount));
            matsW += sepW + qw + UiTheme.S(2) + iconSize;
        }

        var totalW = nameW + matsW + sepW + moneyW;
        var pad = UiTheme.S(4);
        if (totalW + pad * 2 > width || matChunks.Count == 0)
        {
            // Compact Italian text when icons won't fit (or no materials).
            var fallback = FormatCoreUpgradeCostText(upgrade);
            var tw = MeasureUiText(fallback, fontSize);
            var text = tw + pad * 2 <= width
                ? fallback
                : TruncateUiText(fallback, fontSize, width - pad * 2);
            tw = MeasureUiText(text, fontSize);
            DrawUiText(
                text,
                x + Math.Max(pad, (width - tw) / 2),
                y + Math.Max(2, (height - UiTheme.S(fontSize)) / 2),
                fontSize,
                new Color(215, 219, 210, 255));
            return;
        }

        var cursor = x + Math.Max(pad, (width - totalW) / 2);
        var textY = y + Math.Max(2, (height - UiTheme.S(fontSize)) / 2);
        var iconY = y + Math.Max(2, (height - iconSize) / 2);

        DrawUiText(name, cursor, textY, fontSize, new Color(215, 219, 210, 255));
        cursor += nameW;

        var canAfford = wallet.CanAfford(upgrade.MoneyCost, upgrade.BuildCost);
        foreach (var (qty, qw, itemId, amount) in matChunks)
        {
            DrawUiText(sep, cursor, textY, fontSize, UiTheme.TextMuted);
            cursor += sepW;
            var qtyColor = wallet.MaterialCount(itemId) >= amount
                ? new Color(215, 219, 210, 255)
                : new Color(220, 120, 100, 255);
            DrawUiText(qty, cursor, textY, fontSize, qtyColor);
            cursor += qw + UiTheme.S(2);
            Raylib.DrawRectangle(cursor - 1, iconY - 1, iconSize + 2, iconSize + 2, new Color(24, 28, 30, 255));
            UiTheme.DrawItemIcon(itemId, cursor, iconY, iconSize);
            cursor += iconSize;
        }

        DrawUiText(sep, cursor, textY, fontSize, UiTheme.TextMuted);
        cursor += sepW;
        DrawUiText(
            moneyLabel,
            cursor,
            textY,
            fontSize,
            canAfford ? UiTheme.MoneyGreen : new Color(220, 120, 100, 255));
    }

    /// <summary>
    /// Sell-button metrics: widths follow full UI scale (labels use Measure/Draw at Scale),
    /// while the panel soft-scales via MercatoS — so buttons must not use soft-capped widths alone.
    /// </summary>
    private static void GetMercatoSellButtonMetrics(out int oneW, out int allW, out int gap, out int rightPad)
    {
        gap = Math.Max(MercatoS(MercatoSellGapBase), UiTheme.S(MercatoSellGapBase));
        rightPad = Math.Max(MercatoS(MercatoPadBase), UiTheme.S(8));
        // Floor with soft scale for touch targets; grow with full scale so "tutti" never clips.
        oneW = Math.Max(MercatoS(MercatoSellOneWBase), UiTheme.S(MercatoSellOneWBase));
        allW = Math.Max(MercatoS(MercatoSellAllWBase), UiTheme.S(MercatoSellAllWBase));
    }

    private static void GetMercatoSellOneBounds(
        int panelX, int panelY, int panelW, int panelH, int index,
        out int x, out int y, out int w, out int h)
    {
        GetMercatoSellButtonMetrics(out w, out var allW, out var gap, out var rightPad);
        h = Math.Max(MercatoS(18), MercatoRowHeight(panelH) - MercatoS(10));
        x = panelX + panelW - rightPad - allW - gap - w;
        y = MercatoRowY(panelY, panelH, index) + MercatoS(6);
    }

    private static void GetMercatoSellAllBounds(
        int panelX, int panelY, int panelW, int panelH, int index,
        out int x, out int y, out int w, out int h)
    {
        GetMercatoSellButtonMetrics(out _, out w, out _, out var rightPad);
        h = Math.Max(MercatoS(18), MercatoRowHeight(panelH) - MercatoS(10));
        x = panelX + panelW - rightPad - w;
        y = MercatoRowY(panelY, panelH, index) + MercatoS(6);
    }

    private static void DrawCompactMarketButton(int x, int y, int width, int height, string label, bool enabled)
    {
        var fill = enabled ? new Color(55, 66, 60, 255) : new Color(36, 40, 38, 255);
        var text = enabled ? UiTheme.TextPrimary : UiTheme.TextMuted;
        Raylib.DrawRectangle(x, y, width, height, fill);
        Raylib.DrawRectangleLines(x, y, width, height, enabled ? UiTheme.AccentDim : UiTheme.PanelBorder);
        const int labelSize = 11;
        var tw = MeasureUiText(label, labelSize);
        var th = UiTheme.S(labelSize);
        DrawUiText(label, x + Math.Max(2, (width - tw) / 2), y + Math.Max(2, (height - th) / 2), labelSize, text);
    }

    private static bool TryHandleMercatoClick(
        Vector2 mouse,
        FactoryWorld world,
        EconomyWallet wallet,
        EconomySession session,
        MarketCatalog market,
        int panelX,
        int panelY,
        int panelW,
        ref string? statusMessage)
    {
        GetMercatoBounds(out _, out _, out _, out var panelH);
        var autoY = MercatoAutoSellY(panelY);
        var autoH = MercatoS(24);
        var pad = MercatoS(MercatoPadBase);
        if (Contains(mouse, panelX + pad, autoY, panelW - pad * 2, autoH) && ActiveSettings is not null)
        {
            ActiveSettings.AutoSellAtCore = !ActiveSettings.AutoSellAtCore;
            ActiveSettings.Save();
            statusMessage = ActiveSettings.AutoSellAtCore
                ? "Vendita automatica ON: il core liquida in $."
                : "Vendita automatica OFF: stock in magazzino.";
            return true;
        }

        var items = market.Items.Take(4).ToList();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var stock = wallet.MaterialCount(item.ItemId);
            GetMercatoSellOneBounds(panelX, panelY, panelW, panelH, i, out var oneX, out var oneY, out var oneW, out var oneH);
            if (Contains(mouse, oneX, oneY, oneW, oneH))
            {
                if (stock <= 0)
                {
                    statusMessage = $"Nessun {item.DisplayName} in magazzino.";
                    return true;
                }

                if (world.TrySellFromWallet(wallet, item.ItemId, 1, market, session))
                {
                    var price = world.EffectiveSalePrice(item.ItemId, market);
                    statusMessage = $"Venduto 1× {item.DisplayName} (+${price}).";
                    TutorialUsedEconomy = true;
                }

                return true;
            }

            GetMercatoSellAllBounds(panelX, panelY, panelW, panelH, i, out var allX, out var allY, out var allW, out var allH);
            if (Contains(mouse, allX, allY, allW, allH))
            {
                if (stock <= 0)
                {
                    statusMessage = $"Nessun {item.DisplayName} in magazzino.";
                    return true;
                }

                if (world.TrySellFromWallet(wallet, item.ItemId, stock, market, session))
                {
                    statusMessage = $"Venduti {stock}× {item.DisplayName}.";
                    TutorialUsedEconomy = true;
                }

                return true;
            }
        }

        return false;
    }

    private static bool TryHandleStatusClick(
        Vector2 mouse,
        FactoryWorld world,
        EconomyWallet wallet,
        EconomySession session,
        EconomyConfig economy,
        int panelX,
        int panelY,
        int panelW,
        ref string? statusMessage)
    {
        if (!TryGetFabbricaContentMetrics(
                out var sx, out var sy, out var sw, out _, out _, out _,
                out var upgradeX, out var upgradeY, out var upgradeW, out var upgradeH))
        {
            return false;
        }

        _ = panelX;
        _ = panelY;
        _ = panelW;
        _ = sx;
        _ = sy;
        _ = sw;
        if (!Contains(mouse, upgradeX, upgradeY, upgradeW, upgradeH))
        {
            return false;
        }

        if (world.CoreUpgradeLevel > 0)
        {
            statusMessage = $"Core già a LV{world.CoreUpgradeLevel}.";
            return true;
        }

        var upgrade = economy.CoreUpgrade;
        if (!wallet.CanAfford(upgrade.MoneyCost, upgrade.BuildCost))
        {
            statusMessage = FormatCoreUpgradeNeedMessage(upgrade);
            return true;
        }

        if (world.TryUpgradeCore(wallet, upgrade, session))
        {
            statusMessage = "Core potenziato: +prezzo vendite!";
        }

        return true;
    }

    /// <summary>Self-test / capture helper: simulate a CORE button click at the live hit box.</summary>
    internal static bool TryClickCoreUpgradeForTest(
        FactoryWorld world,
        EconomyWallet wallet,
        EconomySession session,
        EconomyConfig economy,
        out string? statusMessage)
    {
        statusMessage = null;
        if (!TryGetFabbricaContentMetrics(
                out var panelX, out var panelY, out var panelW, out _, out _, out _,
                out var upgradeX, out var upgradeY, out var upgradeW, out var upgradeH))
        {
            return false;
        }

        var mouse = new Vector2(upgradeX + upgradeW / 2f, upgradeY + upgradeH / 2f);
        return TryHandleStatusClick(
            mouse, world, wallet, session, economy, panelX, panelY, panelW, ref statusMessage);
    }

    private static int HudLayoutTestPrevWidth;
    private static int HudLayoutTestPrevHeight;

    /// <summary>Self-test: pin window size + UI scale so Fabbrica metrics are deterministic.</summary>
    internal static bool TryConfigureHudLayoutForTest(int width, int height, int uiScalePercent)
    {
        HudLayoutTestPrevWidth = ScreenWidth;
        HudLayoutTestPrevHeight = ScreenHeight;
        ScreenWidth = width;
        ScreenHeight = height;
        UiTheme.ApplyScalePercent(uiScalePercent);
        DockCategory = UiTheme.BuildCategory.Production;
        GetStatusBounds(out _, out _, out _, out var statusH);
        return statusH > 0;
    }

    internal static void RestoreHudLayoutAfterTest(float previousScale)
    {
        ScreenWidth = HudLayoutTestPrevWidth;
        ScreenHeight = HudLayoutTestPrevHeight;
        UiTheme.ApplyScale(previousScale);
    }

    private static string GetOnboardingTip(
        FactoryWorld world,
        ConveyorGrid conveyors,
        ResearchState research,
        EconomyWallet wallet)
    {
        if (world.Miners.Count == 0)
        {
            return "Piazza un minatore su un giacimento.";
        }

        if (conveyors.Cells.Count == 0)
        {
            return "Collega un nastro al CORE per accumulare stock.";
        }

        if (world.CoreDeliveredItems == 0)
        {
            return "Porta ore al CORE: entrano in magazzino.";
        }

        if (wallet.MaterialCount("iron-ore") > 0 && world.Smelters.Count == 0
            && !research.IsUnlocked("smelter") && wallet.Money < 80)
        {
            return "Vendi ore dal Mercato (pulsante 1/tutti) se ti servono $.";
        }

        if (!research.IsUnlocked("smelter") && wallet.Money >= 80)
        {
            return "Apri RICERCA (T) e sblocca il forno.";
        }

        if (research.IsUnlocked("smelter") && world.Smelters.Count == 0)
        {
            return "Piazza un FORNO: le lastre servono per costruire.";
        }

        var crafters = world.Smelters.Count + world.Assemblers.Count;
        var powerLow = world.PowerCapacity > 0
            && world.PowerBuffer < world.PowerCapacity * 0.35f;
        if (powerLow || (world.Generators.Count == 0 && crafters >= 2)
            || world.Generators.Values.Any(g => g.FuelBuffer <= 0 && !g.IsGenerating && crafters >= 1))
        {
            return research.IsUnlocked("generator")
                ? "GENERATORE: alimentalo con carbone (nastro in ingresso)."
                : "Sblocca il GENERATORE in RICERCA (T).";
        }

        var tips = new[]
        {
            "Produce → stock → costruisci, oppure vendi dal Mercato.",
            "Esplora giacimenti di rame e carbone vicino al core.",
            "Sblocca lo sdoppiatore per biforcare i flussi.",
            "L'assemblatore trasforma il rame in fili.",
            "Nastro T3 (Y) per linee ad alto throughput.",
            "Vendita automatica OFF tiene lastre e fili per i costi."
        };
        var index = (int)(Raylib.GetTime() / 8.0) % tips.Length;
        return tips[index];
    }

    private static void DrawWrappedTip(
        string tip, int x, int y, int maxWidth, int maxHeight = int.MaxValue, Color? color = null)
    {
        const int fontSize = 12;
        var lineStep = UiTheme.S(16);
        var maxLines = Math.Max(1, maxHeight / Math.Max(1, lineStep));
        var drawColor = color ?? new Color(211, 164, 76, 255);
        if (MeasureUiText(tip, fontSize) <= maxWidth)
        {
            DrawUiText(tip, x, y, fontSize, drawColor);
            return;
        }

        var words = tip.Split(' ');
        var line = string.Empty;
        var lineY = y;
        var lines = 0;
        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(line) ? word : $"{line} {word}";
            if (MeasureUiText(candidate, fontSize) > maxWidth && !string.IsNullOrEmpty(line))
            {
                DrawUiText(line, x, lineY, fontSize, drawColor);
                lineY += lineStep;
                lines++;
                if (lines >= maxLines)
                {
                    return;
                }

                line = word;
            }
            else
            {
                line = candidate;
            }
        }

        if (!string.IsNullOrEmpty(line) && lines < maxLines)
        {
            DrawUiText(line, x, lineY, fontSize, drawColor);
        }
    }

    private static void DrawMetric(string label, string value, int x, int y, Color accent)
    {
        DrawUiText(label, x, y, 13, new Color(126, 137, 132, 255));
        DrawUiText(value, x, y + 22, 22, accent);
    }

    private static void DrawButton(int x, int y, int width, int height, string label, bool active)
    {
        var fill = active ? new Color(211, 164, 76, 255) : new Color(45, 52, 50, 255);
        var text = active ? new Color(25, 28, 26, 255) : new Color(215, 219, 210, 255);
        Raylib.DrawRectangle(x, y, width, height, fill);
        var textWidth = MeasureUiText(label, 15);
        DrawUiText(label, x + (width - textWidth) / 2, y + 12, 15, text);
    }

    private static GridPosition? MouseCell(Vector2 mouse, WorldCamera camera, FactoryWorld world)
    {
        if (IsOverHudChrome(mouse))
        {
            return null;
        }

        return camera.ScreenToCell(
            mouse.X,
            mouse.Y,
            ViewportLeft,
            ViewportTop,
            ViewportRight,
            ViewportBottom,
            world.Terrain.Width,
            world.Terrain.Height,
            BaseTileSize);
    }

    private static bool Contains(Vector2 point, float x, float y, float width, float height) =>
        point.X >= x && point.X <= x + width && point.Y >= y && point.Y <= y + height;

    private static Vector2 DirectionVector(Direction direction) => direction switch
    {
        Direction.North => new Vector2(0, -1),
        Direction.East => new Vector2(1, 0),
        Direction.South => new Vector2(0, 1),
        Direction.West => new Vector2(-1, 0),
        _ => Vector2.Zero
    };

    /// <summary>
    /// When <paramref name="statusMessage"/> changes, start a short TTL so toasts
    /// cannot linger forever over the HUD. Call once per frame.
    /// </summary>
    private static void TickStatusToast(ref string? statusMessage)
    {
        if (!string.Equals(statusMessage, StatusToastTracked, StringComparison.Ordinal))
        {
            StatusToastTracked = statusMessage;
            StatusToastUntil = statusMessage is null
                ? 0
                : Raylib.GetTime() + StatusToastSeconds;
        }

        if (statusMessage is not null && Raylib.GetTime() >= StatusToastUntil)
        {
            ClearStatusToast(ref statusMessage);
        }
    }

    private static void ClearStatusToast(ref string? statusMessage)
    {
        statusMessage = null;
        StatusToastTracked = null;
        StatusToastUntil = 0;
    }

    private static void DrawSystemResourceOverlay(int x, int y)
    {
        var gpu = SystemMonitor.GpuLabel;
        var lines = gpu is null ? 3 : 4;
        var w = 280;
        var h = 14 + lines * 18;
        Raylib.DrawRectangle(x, y, w, h, new Color(8, 10, 12, 210));
        UiTheme.DrawAccentRect(x, y, w, h, UiTheme.PanelBorderBright, 1);
        var row = y + 6;
        DrawUiText($"FPS {Raylib.GetFPS()}", x + 10, row, 13, new Color(211, 164, 76, 255));
        row += 18;
        DrawUiText($"CPU {SystemMonitor.CpuPercent:0.0}%", x + 10, row, 13, UiTheme.TextPrimary);
        row += 18;
        DrawUiText($"RAM {SystemMonitor.FormatRam()}", x + 10, row, 13, UiTheme.TextPrimary);
        if (gpu is not null)
        {
            row += 18;
            DrawUiText(gpu, x + 10, row, 13, UiTheme.TextMuted);
        }
    }

    /// <summary>
    /// Clears the persisted skip/finish flag and starts the bottom banner from step 1.
    /// Used by confirmed Nuova partita and Impostazioni → Rivedi tutorial.
    /// </summary>
    internal static void RestartTutorial(GameSettings settings)
    {
        settings.TutorialCompleted = false;
        if (SettingsDraft is not null)
        {
            SettingsDraft.TutorialCompleted = false;
        }

        settings.Save();
        BeginTutorialIfNeeded(settings);
    }

    private static void BeginTutorialIfNeeded(GameSettings settings)
    {
        if (settings.TutorialCompleted)
        {
            TutorialActive = false;
            return;
        }

        TutorialActive = true;
        TutorialStep = 0;
        TutorialCameraMoved = 0f;
        TutorialPlacedMiner = false;
        TutorialPlacedBelt = false;
        TutorialSoldOre = false;
        TutorialUsedEconomy = false;
        TutorialOpenedResearch = false;
        TutorialOpenedDock = false;
        TutorialSwitchedDockCategory = false;
        TutorialUsedRemove = false;
        TutorialOpenedSettings = false;
        TutorialRotatedPiece = false;
        TutorialSoldBaseline = -1;
        TutorialMoneyBaseline = -1;
        TutorialMaterialBaseline = -1;
        TutorialDockBaseline = DockCategory;
    }

    private static void CompleteTutorial(GameSettings settings)
    {
        TutorialActive = false;
        settings.TutorialCompleted = true;
        settings.Save();
    }

    private static bool HandleTutorialInput(GameSettings settings, ref string? statusMessage)
    {
        var mouse = Raylib.GetMousePosition();
        GetTutorialSkipBounds(out var sx, out var sy, out var sw, out var sh);
        if (Raylib.IsKeyPressed(KeyboardKey.Backspace)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left) && Contains(mouse, sx, sy, sw, sh)))
        {
            CompleteTutorial(settings);
            statusMessage = "Tutorial saltato.";
            return true;
        }

        GetTutorialNextBounds(out var nx, out var ny, out var nw, out var nh);
        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && Contains(mouse, nx, ny, nw, nh))
        {
            AdvanceTutorialManual(settings);
            return true;
        }

        return false;
    }

    private static void AdvanceTutorialManual(GameSettings settings)
    {
        if (TutorialStep >= TutorialSteps.Length - 1)
        {
            CompleteTutorial(settings);
            return;
        }

        TutorialStep++;
    }

    private static void UpdateTutorialProgress(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        GameSettings settings)
    {
        if (!TutorialActive)
        {
            return;
        }

        if (TutorialSoldBaseline < 0)
        {
            TutorialSoldBaseline = world.CoreDeliveredItems;
        }

        if (TutorialMoneyBaseline < 0)
        {
            TutorialMoneyBaseline = wallet.Money;
            TutorialMaterialBaseline = UiTheme.InventoryItems.Sum(i => wallet.MaterialCount(i.ItemId));
        }

        if (world.CoreDeliveredItems > TutorialSoldBaseline)
        {
            TutorialSoldOre = true;
        }

        if (world.Miners.Count > 0)
        {
            TutorialPlacedMiner = true;
        }

        if (conveyors.Cells.Count > 0)
        {
            TutorialPlacedBelt = true;
        }

        var materialsNow = UiTheme.InventoryItems.Sum(i => wallet.MaterialCount(i.ItemId));
        if (wallet.Money > TutorialMoneyBaseline || materialsNow < TutorialMaterialBaseline)
        {
            // Sold via Mercato (money up) or spent stock on a build (materials down).
            TutorialUsedEconomy = true;
        }

        while (true)
        {
            // Auto-advance when the player performs the step action; informational
            // steps (compact layout, campaign, settings highlights, …) use Avanti.
            var done = TutorialStep switch
            {
                0 => TutorialCameraMoved > 80f,
                1 => TutorialSwitchedDockCategory || TutorialOpenedDock,
                2 => TutorialPlacedMiner,
                3 => TutorialPlacedBelt || TutorialRotatedPiece,
                4 => false, // adjacent transfer — explain then Avanti
                5 => TutorialSoldOre,
                6 => TutorialUsedEconomy,
                7 => world.CoreUpgradeLevel > 0, // Fabbrica / CORE; else Avanti
                8 => TutorialOpenedResearch,
                9 => world.Smelters.Count > 0 || world.Assemblers.Count > 0 || world.Generators.Count > 0,
                10 => false, // logistics advanced — Avanti
                11 => TutorialUsedRemove,
                12 => false, // campagna — Avanti
                13 => TutorialOpenedSettings,
                _ => false
            };
            if (!done)
            {
                break;
            }

            if (TutorialStep >= TutorialSteps.Length - 1)
            {
                CompleteTutorial(settings);
                break;
            }

            TutorialStep++;
        }
    }

    private static void GetTutorialPanelBounds(out int x, out int y, out int w, out int h)
    {
        w = Math.Min(UiTheme.S(640), ScreenWidth - 40);
        h = UiTheme.S(132);
        x = (ScreenWidth - w) / 2;
        y = ScreenHeight - h - 16;

        // Stay clear of the build dock (clamp width and/or nudge up only if needed).
        GetDockBounds(out var dockX, out var dockY, out _, out _);
        if (y + h > dockY && x + w > dockX - 8)
        {
            w = Math.Max(UiTheme.S(320), dockX - x - 16);
            if (x + w > dockX - 8)
            {
                // Center a narrower banner that ends left of the dock.
                w = Math.Max(UiTheme.S(280), dockX - 24);
                x = Math.Max(8, (dockX - w) / 2);
            }
        }
    }

    private static void GetTutorialSkipBounds(out int x, out int y, out int w, out int h)
    {
        GetTutorialPanelBounds(out var px, out var py, out var pw, out _);
        w = UiTheme.S(110);
        h = UiTheme.S(32);
        x = px + pw - w - 12;
        y = py + UiTheme.S(52);
    }

    private static void GetTutorialNextBounds(out int x, out int y, out int w, out int h)
    {
        GetTutorialSkipBounds(out var sx, out var sy, out _, out _);
        w = UiTheme.S(110);
        h = UiTheme.S(32);
        x = sx - w - 8;
        y = sy;
    }

    private static void DrawTutorialBanner(GameSettings settings)
    {
        _ = settings;
        GetTutorialPanelBounds(out var x, out var y, out var w, out var h);
        Raylib.DrawRectangle(x, y, w, h, new Color(10, 14, 16, 230));
        UiTheme.DrawAccentRect(x, y, w, h, UiTheme.Accent, 2);
        var step = Math.Clamp(TutorialStep, 0, TutorialSteps.Length - 1);
        DrawUiText($"Tutorial {step + 1}/{TutorialSteps.Length}", x + 14, y + 8, 14, UiTheme.Accent);

        GetTutorialNextBounds(out var nx, out var ny, out var nw, out var nh);
        var textMaxW = Math.Max(80, nx - x - 24);
        DrawWrappedTip(TutorialSteps[step], x + 14, y + UiTheme.S(28), textMaxW, ny - y - UiTheme.S(32), UiTheme.TextPrimary);

        DrawMenuButton(nx, ny, nw, nh, step >= TutorialSteps.Length - 1 ? "Fine" : "Avanti");
        GetTutorialSkipBounds(out var sx, out var sy, out var sw, out var sh);
        DrawMenuButton(sx, sy, sw, sh, "Salta");
    }

    private static ConveyorDefinition RequireContent(
        IEnumerable<ConveyorDefinition> conveyors,
        string id,
        string kind)
    {
        var match = conveyors.FirstOrDefault(definition => definition.Id == id);
        if (match is null)
        {
            throw new InvalidOperationException(
                $"Contenuto mancante: {kind} '{id}'. " +
                $"Aggiorna o elimina {GameContentStore.UserJsonPath} e rilancia " +
                "(EnsureUserContent dovrebbe aver fuso gli id seed mancanti).");
        }

        return match;
    }

    private static RecipeDefinition RequireRecipe(
        IEnumerable<RecipeDefinition> recipes,
        string id)
    {
        var match = recipes.FirstOrDefault(recipe => recipe.Id == id);
        if (match is null)
        {
            throw new InvalidOperationException(
                $"Contenuto mancante: ricetta '{id}'. " +
                $"Aggiorna o elimina {GameContentStore.UserJsonPath} e rilancia.");
        }

        return match;
    }
}
