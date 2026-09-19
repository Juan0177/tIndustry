using System.Numerics;
using Raylib_cs;
using TIndustry.Logistics;

internal enum AppScreen
{
    Home,
    Playing,
    SaveManager,
    Research,
    Settings,
    NewGame,
    Loading
}

internal static class FactoryGameApp
{
    private static int ScreenWidth = 1240;
    private static int ScreenHeight = 760;
    public const int MapWidth = 1000;
    public const int MapHeight = 1000;
    private const int BaseTileSize = 36;
    private const int HeaderHeight = 64;
    private const int InfoPanelWidth = 260;
    private const int HeaderIconSize = 40;
    private const int HeaderIconGap = 8;
    private const float EntryAnimDuration = 0.85f;
    private const float LoadingMinSeconds = 0.55f;
    private const int ViewportLeft = 0;
    private const int ViewportTop = HeaderHeight;
    private static int ViewportRight => ScreenWidth;
    private static int ViewportBottom => ScreenHeight;
    private const float FixedStep = 1f / 30f;
    public const int DefaultSeed = 7429;
    private const int SeedEspanso = 1337;
    private const int SeedArcipelago = 9001;
    private static UiTheme.BuildCategory DockCategory = UiTheme.BuildCategory.Logistics;
    private static string? DockSelectedId = "conveyor-basic";
    private static GameSettings? SettingsDraft;
    private static float EntryAnimT = 1f;
    private static float LoadingElapsed;
    private static int LoadingSeed = DefaultSeed;
    private static bool LoadingFromSave;
    private static string? LoadingSlotId;
    private static string LoadingLabel = "Caricamento…";
    private static bool LoadingWorldReady;

    private static readonly Direction[] Directions =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West
    ];

    public static void Run(GameContent content, int? maximumFrames = null, string? screenshotPath = null)
    {
        var basicConveyor = content.Conveyors.Single(definition => definition.Id == "conveyor-basic");
        var fastConveyor = content.Conveyors.Single(definition => definition.Id == "conveyor-fast");
        var junctionConveyor = content.Conveyors.Single(definition => definition.Id == "junction");
        var splitterConveyor = content.Conveyors.Single(definition => definition.Id == "splitter");
        var bridgeConveyor = content.Conveyors.Single(definition => definition.Id == "conveyor-bridge");
        var smeltRecipe = content.Recipes.Single(recipe => recipe.Id == "smelt-iron");
        var wireRecipe = content.Recipes.Single(recipe => recipe.Id == "craft-copper-wire");
        var selectedConveyor = basicConveyor;
        var screen = AppScreen.Home;
        FactoryWorld? world = null;
        ConveyorGrid? conveyors = null;
        EconomyWallet? wallet = null;
        WorldCamera? camera = null;
        ResearchState? research = null;
        EconomySession? session = null;
        MarketCatalog? market = null;
        var economy = content.GetEconomy();
        var minerBuilding = content.GetBuildingOrDefault("miner");
        var smelterBuilding = content.GetBuildingOrDefault("smelter");
        var assemblerBuilding = content.GetBuildingOrDefault("assembler");
        var generatorBuilding = content.GetBuildingOrDefault("generator");
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
        var settingsReturnScreen = AppScreen.Home;
        SyncLayoutSize(settings);

        // Headless smoke/capture paths jump straight into a playable session.
        if (maximumFrames is not null || screenshotPath is not null)
        {
            StartNewGame(content, DefaultSeed, out world, out conveyors, out wallet, out camera, out research, out session, out market, out nextItemId);
            screen = AppScreen.Playing;
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
        DisplayApplier.Apply(settings);
        SyncLayoutSize(settings);

        while (!quitRequested
            && !Raylib.WindowShouldClose()
            && (maximumFrames is null || renderedFrames < maximumFrames))
        {
            accumulator += Math.Min(Raylib.GetFrameTime(), 0.1f);
            var frameTime = Math.Min(Raylib.GetFrameTime(), 0.1f);

            switch (screen)
            {
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
                        smelterBuilding,
                        assemblerBuilding,
                        generatorBuilding,
                        basicConveyor,
                        fastConveyor,
                        junctionConveyor,
                        splitterConveyor,
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
                        world!.Update(FixedStep, conveyors!, wallet!, ref nextItemId, market!, session!);
                        accumulator -= FixedStep;
                    }

                    break;
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(new Color(14, 18, 18, 255));
            switch (screen)
            {
                case AppScreen.Home:
                    DrawHome(statusMessage);
                    break;
                case AppScreen.NewGame:
                    DrawNewGame(pendingSeed, statusMessage);
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
                        junctionConveyor,
                        splitterConveyor,
                        bridgeConveyor,
                        selectedConveyor,
                        smeltRecipe,
                        wireRecipe,
                        minerBuilding,
                        smelterBuilding,
                        assemblerBuilding,
                        generatorBuilding,
                        tool,
                        direction,
                        statusMessage,
                        frameTime);
                    break;
            }

            // FPS overlay only on non-play screens — in-game FPS lives in the header.
            if (screen is not AppScreen.Playing)
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

    private static void DrawUiText(string text, int x, int y, int size, Color color) =>
        UiTheme.DrawText(text, x, y, size, color);

    private static int MeasureUiText(string text, int size) =>
        UiTheme.Measure(text, size);

    private static void BeginLoadingNewGame(int seed, ref AppScreen screen, ref string? statusMessage)
    {
        LoadingSeed = seed;
        LoadingFromSave = false;
        LoadingSlotId = null;
        LoadingElapsed = 0f;
        LoadingWorldReady = false;
        LoadingLabel = "Generazione mappa…";
        EntryAnimT = 0f;
        statusMessage = null;
        screen = AppScreen.Loading;
    }

    private static void BeginLoadingSave(string slotId, ref AppScreen screen, ref string? statusMessage)
    {
        LoadingSeed = DefaultSeed;
        LoadingFromSave = true;
        LoadingSlotId = slotId;
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

    private static EconomyWallet CreateStartingWallet() =>
        new(180, new Dictionary<string, int>
        {
            ["iron-plate"] = 48,
            ["copper-wire"] = 10
        });

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

        if (Contains(mouse, HomeButtonX, HomeButtonY(0), HomeButtonWidth, HomeButtonHeight))
        {
            BeginLoadingSave(GameSaveStore.ContinueSlotId, ref screen, ref statusMessage);
            return;
        }

        if (Contains(mouse, HomeButtonX, HomeButtonY(1), HomeButtonWidth, HomeButtonHeight))
        {
            pendingSeed = DefaultSeed;
            statusMessage = null;
            settingsReturnScreen = AppScreen.Home;
            screen = AppScreen.NewGame;
            return;
        }

        if (Contains(mouse, HomeButtonX, HomeButtonY(2), HomeButtonWidth, HomeButtonHeight))
        {
            saveSlots = GameSaveStore.ListSlots().ToArray();
            selectedSlotIndex = 0;
            statusMessage = null;
            screen = AppScreen.SaveManager;
            return;
        }

        if (Contains(mouse, HomeButtonX, HomeButtonY(3), HomeButtonWidth, HomeButtonHeight))
        {
            statusMessage = null;
            settingsReturnScreen = AppScreen.Home;
            SettingsDraft = null;
            screen = AppScreen.Settings;
            return;
        }

        if (Contains(mouse, HomeButtonX, HomeButtonY(4), HomeButtonWidth, HomeButtonHeight))
        {
            quitRequested = true;
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
        var entries = ResearchEntries(content);
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left)
                && Contains(Raylib.GetMousePosition(), 28, ScreenHeight - 70, 180, 40)))
        {
            statusMessage = null;
            screen = AppScreen.Playing;
            return;
        }

        if (entries.Count > 0)
        {
            if (Raylib.IsKeyPressed(KeyboardKey.Up))
            {
                selectedResearchIndex = (selectedResearchIndex + entries.Count - 1) % entries.Count;
            }

            if (Raylib.IsKeyPressed(KeyboardKey.Down))
            {
                selectedResearchIndex = (selectedResearchIndex + 1) % entries.Count;
            }
        }

        if (!Raylib.IsMouseButtonPressed(MouseButton.Left) || entries.Count == 0)
        {
            return;
        }

        var mouse = Raylib.GetMousePosition();
        for (var index = 0; index < entries.Count; index++)
        {
            if (Contains(mouse, 60, 140 + index * 58, 700, 50))
            {
                selectedResearchIndex = index;
            }
        }

        selectedResearchIndex = Math.Clamp(selectedResearchIndex, 0, entries.Count - 1);
        var selected = entries[selectedResearchIndex];
        if (!Contains(mouse, 800, 140, 320, 50))
        {
            return;
        }

        if (research.IsUnlocked(selected.Id))
        {
            statusMessage = "Già sbloccato.";
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
        content.Structures
            .Where(structure => !structure.UnlockedByDefault)
            .OrderBy(structure => structure.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

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
        BuildingDefinition smelterBuilding,
        BuildingDefinition assemblerBuilding,
        BuildingDefinition generatorBuilding,
        ConveyorDefinition basicConveyor,
        ConveyorDefinition fastConveyor,
        ConveyorDefinition junctionConveyor,
        ConveyorDefinition splitterConveyor,
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
        _ = market;
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left)
                && HitHeaderIcon(Raylib.GetMousePosition(), 0)))
        {
            AutoSaveContinue(world, conveyors, wallet, camera, research, session, nextItemId);
            statusMessage = null;
            screen = AppScreen.Home;
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.T)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left)
                && HitHeaderIcon(Raylib.GetMousePosition(), 1)))
        {
            selectedResearchIndex = 0;
            statusMessage = null;
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
                ref tool,
                ref direction,
                ref selectedConveyor))
        {
            previousDragPosition = null;
            return;
        }

        GetInfoBounds(out var infoX, out var infoY, out var infoW, out _);
        var upgradeY = InfoUpgradeY(infoY);
        if (Raylib.IsKeyPressed(KeyboardKey.U)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left)
                && Contains(mouse, infoX + 10, upgradeY, infoW - 20, 32)))
        {
            if (world.TryUpgradeCore(wallet, economy.CoreUpgrade, session))
            {
                statusMessage = "Core potenziato: +vendite!";
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
            // Stay in Strumenti when adjusting facing; do not bounce to Produzione/Logistica.
            SyncFacingHighlight(direction);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.R))
        {
            direction = (Direction)(((int)direction + 1) % 4);
            SyncFacingHighlight(direction);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.One))
        {
            tool = BuildTool.Conveyor;
            SyncDockSelection(tool, selectedConveyor, direction);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Two) && research.IsUnlocked("miner"))
        {
            tool = BuildTool.Miner;
            SyncDockSelection(tool, selectedConveyor, direction);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Three) && research.IsUnlocked("smelter"))
        {
            tool = BuildTool.Smelter;
            SyncDockSelection(tool, selectedConveyor, direction);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Four))
        {
            tool = BuildTool.Remove;
            SyncDockSelection(tool, selectedConveyor, direction);
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
                world.TryPlaceMiner(position, direction, conveyors, wallet, minerBuilding, session);
            }
            else if (tool == BuildTool.Smelter && research.IsUnlocked("smelter"))
            {
                world.TryPlaceSmelter(position, direction, smeltRecipe, conveyors, wallet, smelterBuilding, session);
            }
            else if (tool == BuildTool.Assembler && research.IsUnlocked("assembler"))
            {
                world.TryPlaceAssembler(position, direction, wireRecipe, conveyors, wallet, assemblerBuilding, session);
            }
            else if (tool == BuildTool.Generator && research.IsUnlocked("generator"))
            {
                world.TryPlaceGenerator(position, conveyors, wallet, generatorBuilding, session);
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
                && !world.TryRemoveMiner(position, wallet, minerBuilding, session)
                && !world.TryRemoveSmelter(position, wallet, smelterBuilding, session)
                && !world.TryRemoveAssembler(position, wallet, assemblerBuilding, session)
                && !world.TryRemoveGenerator(position, wallet, generatorBuilding, session))
            {
                conveyors.TryRemove(position, wallet, session);
            }
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            var cell = MouseCell(mouse, camera, world);
            if (cell is { } position
                && !world.TryRemoveMiner(position, wallet, minerBuilding, session)
                && !world.TryRemoveSmelter(position, wallet, smelterBuilding, session)
                && !world.TryRemoveAssembler(position, wallet, assemblerBuilding, session)
                && !world.TryRemoveGenerator(position, wallet, generatorBuilding, session))
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
                || !conveyors.Cells.ContainsKey(conveyorPosition))
            {
                continue;
            }

            if (smelter.InputTiles().Contains(conveyorPosition))
            {
                conveyors.TryOrientToward(conveyorPosition, neighbor);
            }
            else if (smelter.OutputTiles().Contains(conveyorPosition))
            {
                var away = conveyorPosition.Step(Opposite(direction));
                conveyors.TryOrientToward(conveyorPosition, away);
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
                || !conveyors.Cells.ContainsKey(conveyorPosition))
            {
                continue;
            }

            if (assembler.InputTiles().Contains(conveyorPosition))
            {
                conveyors.TryOrientToward(conveyorPosition, neighbor);
            }
            else if (assembler.OutputTiles().Contains(conveyorPosition))
            {
                var away = conveyorPosition.Step(Opposite(direction));
                conveyors.TryOrientToward(conveyorPosition, away);
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
            case BuildTool.Bridge:
                DockCategory = UiTheme.BuildCategory.Logistics;
                DockSelectedId = "bridge";
                break;
            case BuildTool.Generator:
                DockCategory = UiTheme.BuildCategory.Power;
                DockSelectedId = "generator";
                break;
            case BuildTool.Remove:
                DockCategory = UiTheme.BuildCategory.Tools;
                DockSelectedId = "remove";
                break;
            default:
                break;
        }

        _ = direction;
    }

    /// <summary>Update facing-cell highlight without leaving the Tools category.</summary>
    private static void SyncFacingHighlight(Direction direction)
    {
        if (DockCategory != UiTheme.BuildCategory.Tools)
        {
            return;
        }

        if (DockSelectedId == "remove")
        {
            return;
        }

        DockSelectedId = DirectionDockId(direction);
    }

    private static string DirectionDockId(Direction direction) => direction switch
    {
        Direction.North => "dir-n",
        Direction.East => "dir-e",
        Direction.South => "dir-s",
        _ => "dir-w"
    };

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

    private static void GetInfoBounds(out int x, out int y, out int width, out int height)
    {
        width = InfoPanelWidth;
        height = 230;
        x = ScreenWidth - width - UiTheme.DockMargin;
        y = ViewportTop + 8;
    }

    private static int InfoUpgradeY(int infoY) => infoY + 186;

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

        GetInfoBounds(out var ix, out var iy, out var iw, out var ih);
        return Contains(mouse, ix, iy, iw, ih);
    }

    private static bool TrySelectBuildDock(
        Vector2 mouse,
        ResearchState research,
        ConveyorDefinition basicConveyor,
        ConveyorDefinition fastConveyor,
        ref BuildTool tool,
        ref Direction direction,
        ref ConveyorDefinition selectedConveyor)
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
                if (DockCategory == UiTheme.BuildCategory.Tools)
                {
                    // Opening Strumenti must not steal the active placeable tool (e.g. minatore → Rimuovi).
                    if (tool == BuildTool.Remove)
                    {
                        DockSelectedId = "remove";
                    }
                    else
                    {
                        DockSelectedId = DirectionDockId(direction);
                    }

                    return true;
                }

                var first = UiTheme.EntriesFor(DockCategory).FirstOrDefault();
                if (first is not null
                    && (DockSelectedId is null
                        || UiTheme.EntriesFor(DockCategory).All(e => e.Id != DockSelectedId)))
                {
                    ApplyDockEntry(first, research, basicConveyor, fastConveyor,
                        ref tool, ref direction, ref selectedConveyor);
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

            ApplyDockEntry(entries[i], research, basicConveyor, fastConveyor,
                ref tool, ref direction, ref selectedConveyor);
            return true;
        }

        return true;
    }

    private static void ApplyDockEntry(
        UiTheme.DockEntry entry,
        ResearchState research,
        ConveyorDefinition basicConveyor,
        ConveyorDefinition fastConveyor,
        ref BuildTool tool,
        ref Direction direction,
        ref ConveyorDefinition selectedConveyor)
    {
        if (entry.ResearchId is not null && !research.IsUnlocked(entry.ResearchId))
        {
            DockSelectedId = entry.Id;
            return;
        }

        DockSelectedId = entry.Id;
        switch (entry.Kind)
        {
            case UiTheme.DockEntryKind.BuildTool when entry.Tool is { } buildTool:
                tool = buildTool;
                break;
            case UiTheme.DockEntryKind.ConveyorVariant:
                tool = BuildTool.Conveyor;
                selectedConveyor = entry.ConveyorId == fastConveyor.Id ? fastConveyor : basicConveyor;
                break;
            case UiTheme.DockEntryKind.Direction when entry.Facing is { } facing:
                direction = facing;
                DockCategory = UiTheme.BuildCategory.Tools;
                // Keep current placeable tool so facing applies to it.
                break;
            case UiTheme.DockEntryKind.InventoryItem:
                // Inventory cells are informational; keep current build tool.
                break;
        }
    }

    private const int HomeButtonX = 420;
    private const int HomeButtonWidth = 400;
    private const int HomeButtonHeight = 52;

    private static int HomeButtonY(int index) => 220 + index * 62;

    private static void DrawHome(string? statusMessage)
    {
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color(14, 18, 18, 255));
        Raylib.DrawRectangleGradientV(0, 0, ScreenWidth, ScreenHeight,
            new Color(18, 28, 24, 255), new Color(10, 12, 12, 255));
        DrawUiText("tINDUSTRY", 420, 100, 48, new Color(239, 238, 224, 255));
        DrawUiText("Settore Foundry — mappa 1000×1000", 420, 160, 18, new Color(112, 124, 119, 255));

        DrawMenuButton(HomeButtonX, HomeButtonY(0), HomeButtonWidth, HomeButtonHeight, "Continua");
        DrawMenuButton(HomeButtonX, HomeButtonY(1), HomeButtonWidth, HomeButtonHeight, "Nuova partita");
        DrawMenuButton(HomeButtonX, HomeButtonY(2), HomeButtonWidth, HomeButtonHeight, "Gestione salvataggi");
        DrawMenuButton(HomeButtonX, HomeButtonY(3), HomeButtonWidth, HomeButtonHeight, "Impostazioni");
        DrawMenuButton(HomeButtonX, HomeButtonY(4), HomeButtonWidth, HomeButtonHeight, "Esci");

        if (!string.IsNullOrEmpty(statusMessage))
        {
            DrawUiText(statusMessage, 420, 560, 18, new Color(225, 140, 110, 255));
        }

        DrawUiText("WASD / Shift+drag / rotella centrale: pan   ·   Ctrl+rotella: zoom   ·   H/Home: core   ·   T: ricerca   ·   I: impostazioni   ·   Esc: menu",
            80, ScreenHeight - 40, 15, new Color(90, 100, 96, 255));
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
        if (Contains(mouse, 120, 150, 420, 36))
        {
            settings.ShowFps = !settings.ShowFps;
            draft.ShowFps = settings.ShowFps;
            settings.Save();
            statusMessage = settings.ShowFps ? "Contatore FPS attivato." : "Contatore FPS disattivato.";
            return;
        }

        if (Contains(mouse, 120, 192, 420, 36))
        {
            settings.ShowResourceOverlay = !settings.ShowResourceOverlay;
            draft.ShowResourceOverlay = settings.ShowResourceOverlay;
            settings.Save();
            statusMessage = settings.ShowResourceOverlay
                ? "Inventario risorse attivato."
                : "Inventario risorse disattivato.";
            return;
        }

        if (Contains(mouse, 120, 234, 420, 36))
        {
            draft.VSync = !draft.VSync;
            statusMessage = draft.VSync
                ? "VSync: ON (limita al refresh; preferenza FPS salvata)."
                : "VSync: OFF (usa il limite FPS).";
            return;
        }

        // Auto resolution
        if (Contains(mouse, 120, 300, 160, 34))
        {
            draft.UseAutoResolution = true;
            DisplayApplier.CaptureDesktopResolution(draft);
            statusMessage = $"Auto risoluzione: {draft.ResolutionWidth}×{draft.ResolutionHeight}";
            return;
        }

        // Resolution presets
        for (var i = 0; i < GameSettings.ResolutionPresets.Length; i++)
        {
            var x = 120 + (i % 4) * 155;
            var y = 340 + (i / 4) * 38;
            if (!Contains(mouse, x, y, 148, 34))
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
            var x = 120 + (i % 4) * 155;
            var y = 450 + (i / 4) * 36;
            if (!Contains(mouse, x, y, 148, 32))
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
            if (!Contains(mouse, 120 + i * 160, 540, 150, 36))
            {
                continue;
            }

            draft.DisplayMode = modes[i];
            statusMessage = $"Modalità: {GameSettings.DisplayModeLabel(modes[i])}";
            return;
        }

        // Apply
        if (Contains(mouse, 120, 590, 180, 40))
        {
            settings.CopyFrom(draft);
            settings.Save();
            DisplayApplier.Apply(settings);
            SyncLayoutSize(settings);
            statusMessage = "Grafica applicata e salvata.";
            return;
        }

        // Revert draft to last applied
        if (Contains(mouse, 320, 590, 180, 40))
        {
            draft.CopyFrom(settings);
            statusMessage = "Selezione grafica ripristinata.";
        }
    }

    private static void DrawSettings(GameSettings settings, GameSettings draft, string? statusMessage)
    {
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color(14, 18, 18, 255));
        DrawUiText("Impostazioni", 120, 40, 32, new Color(239, 238, 224, 255));
        DrawUiText("Overlay e grafica. Applica per salvare risoluzione, VSync e limite FPS.", 120, 78, 15,
            new Color(112, 124, 119, 255));

        DrawToggleRow(120, 150, 420, 36, "Mostra contatore FPS", settings.ShowFps);
        DrawToggleRow(120, 192, 420, 36, "Mostra inventario risorse", settings.ShowResourceOverlay);
        DrawToggleRow(120, 234, 420, 36, "VSync", draft.VSync);

        DrawUiText("Risoluzione", 120, 280, 16, new Color(196, 201, 193, 255));
        DrawButton(120, 300, 160, 34, "Auto risoluzione", draft.UseAutoResolution);
        for (var i = 0; i < GameSettings.ResolutionPresets.Length; i++)
        {
            var preset = GameSettings.ResolutionPresets[i];
            var x = 120 + (i % 4) * 155;
            var y = 340 + (i / 4) * 38;
            var selected = !draft.UseAutoResolution
                && draft.ResolutionWidth == preset.Width
                && draft.ResolutionHeight == preset.Height;
            DrawButton(x, y, 148, 34, preset.Label, selected);
        }

        DrawUiText("Limite FPS (con VSync: preferenza salvata, sync al refresh)", 120, 424, 15,
            new Color(196, 201, 193, 255));
        for (var i = 0; i < GameSettings.FpsLimitPresets.Length; i++)
        {
            var fps = GameSettings.FpsLimitPresets[i];
            var x = 120 + (i % 4) * 155;
            var y = 450 + (i / 4) * 36;
            DrawButton(x, y, 148, 32, GameSettings.FpsLimitLabel(fps), draft.TargetFps == fps);
        }

        DrawUiText("Modalità schermo", 120, 520, 16, new Color(196, 201, 193, 255));
        DrawButton(120, 540, 150, 36, "Finestra", draft.DisplayMode == DisplayMode.Windowed);
        DrawButton(280, 540, 150, 36, "Senza bordi", draft.DisplayMode == DisplayMode.Borderless);
        DrawButton(440, 540, 150, 36, "Schermo intero", draft.DisplayMode == DisplayMode.Fullscreen);

        var dirty = !draft.MatchesDisplay(settings);
        DrawMenuButton(120, 590, 180, 40, dirty ? "Applica*" : "Applica");
        DrawMenuButton(320, 590, 180, 40, "Annulla");

        var resLabel = settings.UseAutoResolution
            ? $"Auto {settings.ResolutionWidth}×{settings.ResolutionHeight}"
            : $"{settings.ResolutionWidth}×{settings.ResolutionHeight}";
        DrawUiText(
            $"Attuale: {resLabel} · {GameSettings.DisplayModeLabel(settings.DisplayMode)} · VSync {(settings.VSync ? "ON" : "OFF")} · {GameSettings.FpsLimitLabel(settings.TargetFps)}",
            120, 640, 13, new Color(126, 137, 132, 255));
        DrawUiText($"File: {GameSettings.SettingsPath}", 120, 660, 12, new Color(90, 100, 96, 255));

        DrawMenuButton(28, ScreenHeight - 70, 180, 40, "Indietro");
        if (!string.IsNullOrEmpty(statusMessage))
        {
            DrawUiText(statusMessage, 230, ScreenHeight - 58, 16, new Color(112, 218, 145, 255));
        }
    }

    private static void DrawToggleRow(int x, int y, int width, int height, string label, bool enabled)
    {
        var mouse = Raylib.GetMousePosition();
        var hover = Contains(mouse, x, y, width, height);
        Raylib.DrawRectangle(x, y, width, height,
            hover ? new Color(55, 66, 60, 255) : new Color(32, 38, 36, 255));
        Raylib.DrawRectangleLines(x, y, width, height, new Color(70, 82, 76, 255));
        DrawUiText(label, x + 18, y + (height - 20) / 2, 20, new Color(232, 233, 221, 255));
        var badge = enabled ? "ON" : "OFF";
        var badgeColor = enabled ? new Color(112, 218, 145, 255) : new Color(180, 120, 100, 255);
        var badgeWidth = MeasureUiText(badge, 20);
        DrawUiText(badge, x + width - badgeWidth - 20, y + (height - 20) / 2, 20, badgeColor);
    }

    private static void DrawDebugOverlays(GameSettings settings, EconomyWallet? wallet)
    {
        if (settings.ShowFps)
        {
            Raylib.DrawRectangle(8, 8, 88, 28, new Color(10, 12, 12, 180));
            DrawUiText($"FPS {Raylib.GetFPS()}", 16, 14, 18, new Color(211, 164, 76, 255));
        }

        // Corner resource strip when overlay is on but header wallet is hidden is N/A —
        // header already respects ShowResourceOverlay. Keep a compact strip on Home/Settings only.
        if (settings.ShowResourceOverlay && wallet is not null)
        {
            // No extra strip during Playing/Research — header/research already show resources.
        }
    }

    private static void DrawResearch(
        GameContent content,
        EconomyWallet wallet,
        ResearchState research,
        int selectedIndex,
        string? statusMessage,
        GameSettings settings)
    {
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color(14, 18, 18, 255));
        DrawUiText("Ricerca / Sblocchi", 60, 36, 32, new Color(239, 238, 224, 255));
        DrawUiText("Seleziona una struttura, verifica i requisiti, conferma per sbloccare.", 60, 80, 18,
            new Color(112, 124, 119, 255));
        if (settings.ShowResourceOverlay)
        {
            DrawUiText($"Wallet: $ {wallet.Money}   ·   risorse nella strip in alto (overlay)",
                60, 108, 16, new Color(164, 173, 168, 255));
        }
        else
        {
            DrawUiText("Inventario risorse disattivato (Impostazioni).", 60, 108, 16, new Color(126, 137, 132, 255));
        }

        var entries = ResearchEntries(content);
        if (entries.Count == 0)
        {
            DrawUiText("Nessuna struttura da sbloccare.", 60, 160, 22, new Color(164, 173, 168, 255));
        }
        else
        {
            selectedIndex = Math.Clamp(selectedIndex, 0, entries.Count - 1);
            for (var index = 0; index < entries.Count; index++)
            {
                var structure = entries[index];
                var y = 140 + index * 58;
                var selected = index == selectedIndex;
                var unlocked = research.IsUnlocked(structure.Id);
                Raylib.DrawRectangle(60, y, 700, 50,
                    selected ? new Color(55, 72, 62, 255) : new Color(28, 34, 33, 255));
                var status = unlocked
                    ? (structure.IsStub ? "SBLOCCATO (stub)" : "SBLOCCATO")
                    : "BLOCCATO";
                var accent = unlocked ? new Color(112, 218, 145, 255) : new Color(220, 170, 110, 255);
                DrawUiText(structure.DisplayName, 76, y + 8, 20, new Color(232, 233, 221, 255));
                DrawUiText(
                    $"{status}  ·  {FormatUnlockRequirement(structure.Unlock)}{(structure.IsStub ? "  ·  stub" : "")}",
                    76, y + 28, 14, accent);
            }

            var selectedStructure = entries[selectedIndex];
            var canUnlock = research.CanUnlock(selectedStructure, wallet);
            var buttonLabel = research.IsUnlocked(selectedStructure.Id)
                ? "Già sbloccato"
                : canUnlock ? "Conferma sblocco" : "Risorse insufficienti";
            DrawMenuButton(800, 140, 320, 50, buttonLabel);
            DrawUiText("Lo sblocco consuma denaro e materiali.", 800, 210, 14, new Color(126, 137, 132, 255));
            if (selectedStructure.IsStub)
            {
                DrawUiText("Stub: non costruibile ancora.", 800, 234, 14, new Color(180, 120, 100, 255));
            }
        }

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
        ConveyorDefinition junctionConveyor,
        ConveyorDefinition splitterConveyor,
        ConveyorDefinition bridgeConveyor,
        ConveyorDefinition selectedConveyor,
        RecipeDefinition smeltRecipe,
        RecipeDefinition wireRecipe,
        BuildingDefinition minerBuilding,
        BuildingDefinition smelterBuilding,
        BuildingDefinition assemblerBuilding,
        BuildingDefinition generatorBuilding,
        BuildTool tool,
        Direction direction,
        string? statusMessage,
        float frameTime)
    {
        DrawWorld(world, conveyors, camera);
        DrawPreview(
            world, conveyors, wallet, research, camera, selectedConveyor,
            junctionConveyor, splitterConveyor, bridgeConveyor,
            smeltRecipe, wireRecipe,
            minerBuilding, smelterBuilding, assemblerBuilding, generatorBuilding, tool, direction);
        DrawHeader(
            wallet, research, session, market, economy, settings, basicConveyor, fastConveyor,
            junctionConveyor, splitterConveyor, bridgeConveyor,
            selectedConveyor, minerBuilding, smelterBuilding, assemblerBuilding, generatorBuilding,
            tool, direction, world, camera);
        DrawInfoPanel(world, conveyors, research, wallet, session, market, economy);
        DrawBuildDock(wallet, research, selectedConveyor, direction, tool);

        if (!string.IsNullOrEmpty(statusMessage))
        {
            Raylib.DrawRectangle(ViewportLeft + 16, ViewportTop + 10, 520, 28, new Color(10, 14, 14, 200));
            DrawUiText(statusMessage, ViewportLeft + 24, ViewportTop + 16, 16,
                new Color(112, 218, 145, 255));
        }

        DrawEntryOverlay(frameTime);
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
        ConveyorDefinition junctionConveyor,
        ConveyorDefinition splitterConveyor,
        ConveyorDefinition bridgeConveyor,
        ConveyorDefinition selectedConveyor,
        BuildingDefinition minerBuilding,
        BuildingDefinition smelterBuilding,
        BuildingDefinition assemblerBuilding,
        BuildingDefinition generatorBuilding,
        BuildTool tool,
        Direction direction,
        FactoryWorld world,
        WorldCamera camera)
    {
        Raylib.DrawRectangle(0, 0, ScreenWidth, HeaderHeight, new Color(14, 16, 18, 230));
        Raylib.DrawRectangle(0, HeaderHeight - 1, ScreenWidth, 1, new Color(48, 52, 56, 255));
        DrawUiText("tINDUSTRY", 16, 8, 20, new Color(239, 238, 224, 255));
        DrawUiText(
            $"seed {world.Seed}  ·  zoom {camera.Zoom:0.00}",
            16, 30, 12, new Color(128, 140, 134, 255));

        // Compact top resource strip (Mindustry-like), toggled by Impostazioni overlay flag.
        if (settings.ShowResourceOverlay)
        {
            DrawResourceStrip(wallet, session);
        }

        var cost = tool switch
        {
            BuildTool.Miner => research.IsUnlocked("miner")
                ? FormatBuildingCost(minerBuilding)
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
            BuildTool.Junction => FormatConveyorCost(junctionConveyor, research),
            BuildTool.Splitter => FormatConveyorCost(splitterConveyor, research),
            BuildTool.Bridge => FormatConveyorCost(bridgeConveyor, research),
            BuildTool.Conveyor => FormatConveyorCost(selectedConveyor, research),
            _ => economy.RefundPolicyNote
        };
        DrawUiText(cost, 16, 48, 12, new Color(164, 173, 168, 255));

        var mouse = Raylib.GetMousePosition();
        // Icon buttons right → left: Menu (0), Ricerca (1), Impostazioni (2).
        DrawHeaderIconButton(0, HitHeaderIcon(mouse, 0), () =>
            UiTheme.DrawMenuIcon(HeaderIconX(0) + 4, 16, HeaderIconSize - 8, UiTheme.TextPrimary));
        DrawHeaderIconButton(1, HitHeaderIcon(mouse, 1), () =>
            UiTheme.DrawTreeIcon(HeaderIconX(1) + 4, 16, HeaderIconSize - 8, UiTheme.TextPrimary));
        DrawHeaderIconButton(2, HitHeaderIcon(mouse, 2), () =>
            UiTheme.DrawGearIcon(HeaderIconX(2) + 4, 16, HeaderIconSize - 8, UiTheme.TextPrimary));

        if (settings.ShowFps)
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
        _ = direction;
    }

    private static void DrawResourceStrip(EconomyWallet wallet, EconomySession session)
    {
        var items = UiTheme.InventoryItems;
        var moneyLabel = $"$ {wallet.Money}";
        var net = session.NetWorthDelta(wallet);
        var netLabel = $"sess {(net >= 0 ? "+" : "")}{net}";
        var moneyW = MeasureUiText(moneyLabel, 15);
        var netW = MeasureUiText(netLabel, 12);

        var chipsW = 0;
        var chipWidths = new int[items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            var count = wallet.MaterialCount(items[i].ItemId);
            var countW = MeasureUiText(count.ToString(), 14);
            chipWidths[i] = 28 + countW + 14;
            chipsW += chipWidths[i];
        }

        var stripW = 12 + Math.Max(moneyW, netW) + 16 + chipsW + 10;
        var stripX = Math.Clamp((ScreenWidth - stripW) / 2, 200, Math.Max(200, HeaderIconX(2) - stripW - 100));
        var stripY = 8;
        var stripH = 48;
        Raylib.DrawRectangle(stripX, stripY, stripW, stripH, UiTheme.PanelFill);
        UiTheme.DrawAccentRect(stripX, stripY, stripW, stripH, UiTheme.PanelBorder, 1);

        // Money + session delta stacked on the left — never overlaps resource counts.
        DrawUiText(moneyLabel, stripX + 10, stripY + 8, 15, new Color(112, 218, 145, 255));
        DrawUiText(
            netLabel,
            stripX + 10,
            stripY + 28,
            12,
            net >= 0 ? new Color(112, 218, 145, 255) : new Color(225, 120, 100, 255));

        var x = stripX + 10 + Math.Max(moneyW, netW) + 16;
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var count = wallet.MaterialCount(item.ItemId);
            Raylib.DrawRectangle(x, stripY + 13, 22, 22, UiTheme.ItemColor(item.ItemId));
            Raylib.DrawRectangleLines(x, stripY + 13, 22, 22, UiTheme.ItemOutline(item.ItemId));
            var abbrevW = MeasureUiText(item.Abbrev, 11);
            DrawUiText(item.Abbrev, x + (22 - abbrevW) / 2, stripY + 17, 11, new Color(18, 16, 12, 255));
            DrawUiText(count.ToString(), x + 28, stripY + 16, 14, UiTheme.TextPrimary);
            x += chipWidths[i];
        }
    }

    private static void DrawBuildDock(
        EconomyWallet wallet,
        ResearchState research,
        ConveyorDefinition selectedConveyor,
        Direction direction,
        BuildTool tool)
    {
        // Safety: Inventory was removed from the rail.
        if (DockCategory == UiTheme.BuildCategory.Inventory)
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
        string? hoverName = null;

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
            else
            {
                Raylib.DrawRectangleLines(cx, cy, UiTheme.DockCellSize, UiTheme.DockCellSize, UiTheme.PanelBorder);
            }

            var tint = active ? UiTheme.Accent : UiTheme.BuildCategoryTint(category);
            UiTheme.DrawBuildCategoryIcon(category, cx, cy, UiTheme.DockCellSize, tint);
            if (hovered)
            {
                hoverName = UiTheme.BuildCategoryLabel(category);
            }
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
                UiTheme.DrawAccentRect(cx, cy, UiTheme.DockCellSize, UiTheme.DockCellSize, UiTheme.Accent);
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
                hoverName = entry.Label;
            }
        }

        // Hover name bar at the bottom of the dock.
        var barY = dockY + dockH - UiTheme.DockHoverBarHeight;
        Raylib.DrawRectangle(dockX, barY, dockW, UiTheme.DockHoverBarHeight, UiTheme.PanelFill);
        UiTheme.DrawAccentRect(dockX, barY, dockW, UiTheme.DockHoverBarHeight, UiTheme.PanelBorder, 1);
        var label = hoverName ?? UiTheme.BuildCategoryLabel(DockCategory);
        var labelW = MeasureUiText(label, 12);
        DrawUiText(label, dockX + Math.Max(4, (dockW - labelW) / 2), barY + 3, 12,
            hoverName is null ? UiTheme.TextMuted : UiTheme.Accent);

        _ = wallet;
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
            UiTheme.DockEntryKind.Direction => entry.Facing == direction
                && DockCategory == UiTheme.BuildCategory.Tools,
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
            "junction" => new Color(140, 180, 140, 255),
            "splitter" => new Color(170, 190, 110, 255),
            "bridge" => new Color(150, 160, 200, 255),
            "generator" => new Color(230, 200, 70, 255),
            "remove" => new Color(220, 100, 90, 255),
            _ => UiTheme.TextPrimary
        };

        UiTheme.DrawDockEntryIcon(entry.Id, cx, cy, UiTheme.DockCellSize, color);
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
        return $"${definition.MoneyCost} + {plates} P · t{definition.Tier} · rimborso 100%";
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
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var position = new GridPosition(x, y);
                var screen = camera.WorldToScreen(x * BaseTileSize, y * BaseTileSize, ViewportLeft, ViewportTop);
                DrawTerrainTile(world.Terrain[position], position, screen.X, screen.Y, tileSize);
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

        Raylib.EndScissorMode();
    }

    private static void DrawTerrainTile(TerrainTile tile, GridPosition position, float x, float y, float tileSize)
    {
        // Integer pixel bounds from floor→next floor keep cells flush (no muddy float gaps).
        var ix = (int)MathF.Floor(x);
        var iy = (int)MathF.Floor(y);
        var size = Math.Max(1, (int)MathF.Floor(x + tileSize) - ix);
        var sizeY = Math.Max(1, (int)MathF.Floor(y + tileSize) - iy);
        var color = tile.Terrain switch
        {
            TerrainKind.Grass => new Color(58, 86, 64, 255),
            TerrainKind.Soil => new Color(96, 82, 60, 255),
            TerrainKind.Stone => new Color(82, 88, 86, 255),
            TerrainKind.Water => new Color(40, 90, 108, 255),
            _ => Color.Black
        };
        Raylib.DrawRectangle(ix, iy, size, sizeY, color);
        if (tileSize >= 12f)
        {
            Raylib.DrawRectangleLines(ix, iy, size, sizeY, new Color(18, 24, 22, 55));
        }

        if (tile.Deposit == DepositKind.Iron && tileSize >= 8f)
        {
            var s = tileSize / BaseTileSize;
            var fill = UiTheme.ItemColor("iron-ore");
            Raylib.DrawCircle(ix + (int)(10 * s), iy + (int)(12 * s), Math.Max(2f, 5 * s), fill);
            Raylib.DrawCircle(ix + (int)(24 * s), iy + (int)(21 * s), Math.Max(2.5f, 7 * s), fill);
            Raylib.DrawCircle(ix + (int)(12 * s), iy + (int)(27 * s), Math.Max(1.5f, 3.5f * s),
                new Color(255, 200, 120, 255));
            if (tileSize >= 22f)
            {
                DrawUiText("Fe", ix + 2, iy + 2, Math.Max(10, (int)(11 * s)), new Color(255, 230, 190, 255));
            }
        }
        else if (tile.Deposit == DepositKind.Copper && tileSize >= 8f)
        {
            var s = tileSize / BaseTileSize;
            var fill = UiTheme.ItemColor("copper-ore");
            Raylib.DrawCircle(ix + (int)(11 * s), iy + (int)(13 * s), Math.Max(2f, 5 * s), fill);
            Raylib.DrawCircle(ix + (int)(23 * s), iy + (int)(20 * s), Math.Max(2.5f, 7 * s), fill);
            Raylib.DrawCircle(ix + (int)(14 * s), iy + (int)(26 * s), Math.Max(1.5f, 3.5f * s),
                new Color(190, 255, 230, 255));
            if (tileSize >= 22f)
            {
                DrawUiText("Ra", ix + 2, iy + 2, Math.Max(10, (int)(11 * s)), new Color(210, 255, 240, 255));
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
        Raylib.DrawRectangle(x + 5, y + 7, size, size, new Color(11, 16, 15, 145));
        Raylib.DrawRectangle(x + 2, y + 2, size - 4, size - 4, new Color(35, 60, 48, 255));
        Raylib.DrawRectangleLines(x + 5, y + 5, size - 10, size - 10, new Color(91, 184, 121, 255));
        Raylib.DrawRectangle(x + 17, y + 17, size - 34, size - 34, new Color(27, 39, 35, 255));
        Raylib.DrawRectangleLines(x + 20, y + 20, size - 40, size - 40, new Color(65, 109, 82, 255));

        var pulse = 23f + MathF.Sin((float)Raylib.GetTime() * 3f) * 3f;
        var scale = tileSize / BaseTileSize;
        var center = new Vector2(x + size / 2f, y + size / 2f);
        Raylib.DrawCircleV(center, (pulse + 8) * scale, new Color(44, 104, 68, 255));
        Raylib.DrawCircleV(center, pulse * scale, new Color(103, 225, 139, 255));
        Raylib.DrawCircleV(center, 12 * scale, new Color(210, 251, 218, 255));
        if (tileSize >= 12f)
        {
            DrawUiText("CORE", x + size / 2 - 24, y + size - (int)(34 * scale), Math.Max(10, (int)(18 * scale)),
                new Color(201, 232, 207, 255));
        }
    }

    private static void DrawMiner(MinerBuilding miner, float fx, float fy, float tileSize, bool preview)
    {
        var alpha = preview ? 150 : 255;
        var x = (int)fx;
        var y = (int)fy;
        var size = (int)(tileSize * MinerBuilding.Size);
        var scale = tileSize / BaseTileSize;
        Raylib.DrawRectangle(x + 4, y + 6, size - 4, size - 4, new Color(16, 20, 19, alpha));
        Raylib.DrawRectangle(x + 2, y + 2, size - 4, size - 4, new Color(49, 53, 51, alpha));
        Raylib.DrawRectangleLines(x + 5, y + 5, size - 10, size - 10, new Color(222, 168, 76, alpha));
        Raylib.DrawRectangle(x + 12, y + 12, size - 24, size - 24, new Color(30, 34, 33, alpha));

        var center = new Vector2(x + size / 2f, y + size / 2f - 3 * scale);
        var angle = (float)Raylib.GetTime() * 90f;
        Raylib.DrawPoly(center, 8, 18 * scale, angle, new Color(116, 125, 120, alpha));
        Raylib.DrawPolyLinesEx(center, 8, 18 * scale, angle, 3, new Color(225, 216, 186, alpha));
        Raylib.DrawCircleV(center, 7 * scale, new Color(210, 143, 68, alpha));
        DrawDirectionMark(center + DirectionVector(miner.Direction) * (14f * scale), miner.Direction, alpha, tileSize);
        Raylib.DrawRectangle(x + 9, y + size - 10, size - 18, 4, new Color(25, 29, 28, alpha));
        Raylib.DrawRectangle(x + 9, y + size - 10, (int)((size - 18) * miner.Progress), 4,
            new Color(231, 166, 66, alpha));
        if (tileSize >= 12f)
        {
            var efficiencyLabel = $"{miner.Efficiency:P0}";
            var efficiencyWidth = MeasureUiText(efficiencyLabel, 12);
            Raylib.DrawRectangle(x + (size - efficiencyWidth) / 2 - 4, y + size - 27,
                efficiencyWidth + 8, 16, new Color(20, 24, 23, alpha));
            DrawUiText(efficiencyLabel, x + (size - efficiencyWidth) / 2, y + size - 25,
                12, new Color(233, 190, 96, alpha));
        }
    }

    private static void DrawSmelter(SmelterBuilding smelter, float fx, float fy, float tileSize, bool preview)
    {
        var alpha = preview ? 150 : 255;
        var x = (int)fx;
        var y = (int)fy;
        var size = (int)(tileSize * SmelterBuilding.Size);
        var scale = tileSize / BaseTileSize;
        Raylib.DrawRectangle(x + 4, y + 6, size - 4, size - 4, new Color(18, 14, 14, alpha));
        Raylib.DrawRectangle(x + 2, y + 2, size - 4, size - 4, new Color(72, 42, 36, alpha));
        Raylib.DrawRectangleLines(x + 5, y + 5, size - 10, size - 10, new Color(220, 110, 72, alpha));
        Raylib.DrawRectangle(x + 12, y + 12, size - 24, size - 24, new Color(34, 22, 20, alpha));

        var center = new Vector2(x + size / 2f, y + size / 2f);
        var glow = 10f + MathF.Sin((float)Raylib.GetTime() * 4f) * 3f;
        Raylib.DrawCircleV(center, glow * scale, new Color(180, 70, 40, alpha));
        Raylib.DrawCircleV(center, 6 * scale, new Color(240, 170, 80, alpha));
        DrawDirectionMark(center + DirectionVector(smelter.Direction) * (16f * scale), smelter.Direction, alpha, tileSize);

        var barProgress = smelter.IsCrafting ? smelter.Progress : 0f;
        Raylib.DrawRectangle(x + 9, y + size - 10, size - 18, 4, new Color(25, 29, 28, alpha));
        Raylib.DrawRectangle(x + 9, y + size - 10, (int)((size - 18) * barProgress), 4,
            new Color(235, 120, 70, alpha));
        if (tileSize >= 12f)
        {
            var label = "FORNO";
            var labelW = MeasureUiText(label, 13);
            Raylib.DrawRectangle(x + 10, y + 6, labelW + 8, 16, new Color(20, 12, 10, 180));
            DrawUiText(label, x + 14, y + 8, 13, new Color(255, 220, 190, alpha));
        }
    }

    private static void DrawAssembler(SmelterBuilding assembler, float fx, float fy, float tileSize, bool preview)
    {
        var alpha = preview ? 150 : 255;
        var x = (int)fx;
        var y = (int)fy;
        var size = (int)(tileSize * SmelterBuilding.Size);
        var scale = tileSize / BaseTileSize;
        Raylib.DrawRectangle(x + 4, y + 6, size - 4, size - 4, new Color(12, 18, 22, alpha));
        Raylib.DrawRectangle(x + 2, y + 2, size - 4, size - 4, new Color(28, 62, 78, alpha));
        Raylib.DrawRectangleLines(x + 5, y + 5, size - 10, size - 10, new Color(72, 188, 196, alpha));
        Raylib.DrawRectangle(x + 12, y + 12, size - 24, size - 24, new Color(18, 36, 48, alpha));

        var center = new Vector2(x + size / 2f, y + size / 2f);
        var glow = 10f + MathF.Sin((float)Raylib.GetTime() * 4f) * 3f;
        Raylib.DrawCircleV(center, glow * scale, new Color(40, 120, 140, alpha));
        Raylib.DrawCircleV(center, 6 * scale, new Color(110, 210, 220, alpha));
        DrawDirectionMark(center + DirectionVector(assembler.Direction) * (16f * scale), assembler.Direction, alpha, tileSize);

        var barProgress = assembler.IsCrafting ? assembler.Progress : 0f;
        Raylib.DrawRectangle(x + 9, y + size - 10, size - 18, 4, new Color(25, 29, 28, alpha));
        Raylib.DrawRectangle(x + 9, y + size - 10, (int)((size - 18) * barProgress), 4,
            new Color(80, 190, 200, alpha));
        if (tileSize >= 12f)
        {
            var label = "ASSY";
            var labelW = MeasureUiText(label, 13);
            Raylib.DrawRectangle(x + 10, y + 6, labelW + 8, 16, new Color(10, 20, 24, 180));
            DrawUiText(label, x + 14, y + 8, 13, new Color(190, 240, 246, alpha));
        }
    }

    private static void DrawGenerator(GeneratorBuilding generator, float fx, float fy, float tileSize, bool preview)
    {
        _ = generator;
        var alpha = preview ? 150 : 255;
        var x = (int)fx;
        var y = (int)fy;
        var size = (int)(tileSize * GeneratorBuilding.Size);
        var scale = tileSize / BaseTileSize;
        Raylib.DrawRectangle(x + 4, y + 6, size - 4, size - 4, new Color(22, 18, 10, alpha));
        Raylib.DrawRectangle(x + 2, y + 2, size - 4, size - 4, new Color(120, 88, 28, alpha));
        Raylib.DrawRectangleLines(x + 5, y + 5, size - 10, size - 10, new Color(230, 190, 70, alpha));
        Raylib.DrawRectangle(x + 12, y + 12, size - 24, size - 24, new Color(48, 36, 14, alpha));

        var center = new Vector2(x + size / 2f, y + size / 2f);
        var pulse = 10f + MathF.Sin((float)Raylib.GetTime() * 5f) * 3.5f;
        Raylib.DrawCircleV(center, (pulse + 4) * scale, new Color(180, 120, 30, alpha));
        Raylib.DrawCircleV(center, pulse * scale, new Color(240, 190, 60, alpha));
        Raylib.DrawCircleV(center, 5 * scale, new Color(255, 235, 150, alpha));
        if (tileSize >= 12f)
        {
            var label = "GEN";
            var labelW = MeasureUiText(label, 13);
            Raylib.DrawRectangle(x + 10, y + 6, labelW + 8, 16, new Color(24, 18, 8, 180));
            DrawUiText(label, x + 14, y + 8, 13, new Color(255, 235, 170, alpha));
        }
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
            case LogisticsKind.Bridge:
                DrawBridgeGlyph(conveyor, fx, fy, tileSize, alpha);
                break;
            default:
                Raylib.DrawRectangle(x + size / 5, y + size / 5, size - size * 2 / 5, size - size * 2 / 5,
                    new Color(38, 43, 42, alpha));
                foreach (var connectedDirection in Directions)
                {
                    if (IsConnected(conveyor, connectedDirection, conveyors, world))
                    {
                        DrawConveyorArm(center, connectedDirection, alpha, tileSize);
                    }
                }

                DrawConveyorArm(center, conveyor.Direction, alpha, tileSize);
                Raylib.DrawRectangle(x + size / 4, y + size / 4, size / 2, size / 2,
                    conveyor.Definition.Tier >= 2
                        ? new Color(56, 92, 110, alpha)
                        : new Color(70, 77, 74, alpha));
                DrawDirectionMark(center, conveyor.Direction, alpha, tileSize);
                break;
        }

        foreach (var item in conveyor.Items)
        {
            var vector = DirectionVector(conveyor.Direction);
            var itemPosition = center - vector * (tileSize * 0.5f) + vector * (item.Progress * tileSize);
            var itemSize = Math.Max(6, (int)(12 * tileSize / BaseTileSize));
            var ix = (int)itemPosition.X - itemSize / 2;
            var iy = (int)itemPosition.Y - itemSize / 2;
            var fill = UiTheme.ItemColor(item.ItemId);
            Raylib.DrawRectangle(ix, iy, itemSize, itemSize, fill);
            Raylib.DrawRectangleLines(ix, iy, itemSize, itemSize, UiTheme.ItemOutline(item.ItemId));
            if (tileSize >= 16f)
            {
                var abbrev = UiTheme.ItemAbbrev(item.ItemId);
                var fontSize = Math.Max(10, Math.Min(14, itemSize - 2));
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
        var arm = Math.Max(4, (int)(10 * tileSize / BaseTileSize));
        var span = Math.Max(10, (int)(26 * tileSize / BaseTileSize));
        var forward = DirectionVector(direction);
        var side = new Vector2(-forward.Y, forward.X);
        // Stem toward input (opposite of output direction)
        var stemEnd = center - forward * (span * 0.45f);
        Raylib.DrawLineEx(center, stemEnd, arm, new Color(88, 78, 58, alpha));
        // Branch bar across side exits
        var left = center + side * (span * 0.4f);
        var right = center - side * (span * 0.4f);
        Raylib.DrawLineEx(left, right, arm, new Color(88, 78, 58, alpha));
        Raylib.DrawCircleV(center, Math.Max(3f, 5 * tileSize / BaseTileSize), new Color(210, 170, 90, alpha));
        DrawDirectionMark(center + forward * (10f * tileSize / BaseTileSize), direction, alpha, tileSize);
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

        if (tileSize < 10f)
        {
            return;
        }

        var phase = (float)(Raylib.GetTime() * 10 % 9);
        for (var offset = -12f + phase; offset <= 12f; offset += 9f)
        {
            var mark = center + vector * (offset * tileSize / BaseTileSize);
            var side = new Vector2(-vector.Y, vector.X) * (5f * tileSize / BaseTileSize);
            Raylib.DrawLineEx(mark - side, mark + side, 2, new Color(47, 53, 51, alpha));
        }
    }

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
        Raylib.DrawTriangle(
            point + vector * (5f * scale),
            point - vector * (5f * scale) + side * (5f * scale),
            point - vector * (5f * scale) - side * (5f * scale),
            new Color(224, 207, 142, alpha));
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
        ConveyorDefinition bridgeConveyor,
        RecipeDefinition smeltRecipe,
        RecipeDefinition wireRecipe,
        BuildingDefinition minerBuilding,
        BuildingDefinition smelterBuilding,
        BuildingDefinition assemblerBuilding,
        BuildingDefinition generatorBuilding,
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
            BuildTool.Bridge => bridgeConveyor,
            _ => selectedConveyor
        };
        var valid = tool switch
        {
            BuildTool.Conveyor => world.CanPlaceConveyor(position)
                && !conveyors.Cells.ContainsKey(position)
                && research.IsUnlocked(selectedConveyor.Id)
                && wallet.CanAfford(selectedConveyor.MoneyCost, selectedConveyor.BuildCost),
            BuildTool.Junction or BuildTool.Splitter => world.CanPlaceConveyor(position)
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
            BuildTool.Smelter => research.IsUnlocked("smelter")
                && world.CanPlaceSmelter(position, conveyors)
                && wallet.CanAfford(smelterBuilding.MoneyCost, smelterBuilding.BuildCost),
            BuildTool.Assembler => research.IsUnlocked("assembler")
                && world.CanPlaceAssembler(position, conveyors)
                && wallet.CanAfford(assemblerBuilding.MoneyCost, assemblerBuilding.BuildCost),
            BuildTool.Generator => research.IsUnlocked("generator")
                && world.CanPlaceGenerator(position, conveyors)
                && wallet.CanAfford(generatorBuilding.MoneyCost, generatorBuilding.BuildCost),
            BuildTool.Remove => world.Miners.ContainsKey(position)
                || world.IsMinerTile(position)
                || world.Smelters.ContainsKey(position)
                || world.IsSmelterTile(position)
                || world.Assemblers.ContainsKey(position)
                || world.IsAssemblerTile(position)
                || world.Generators.ContainsKey(position)
                || world.IsGeneratorTile(position)
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
        var previewSize = tool is BuildTool.Miner or BuildTool.Smelter or BuildTool.Assembler or BuildTool.Generator
            ? tileSize * MinerBuilding.Size
            : tileSize;
        Raylib.BeginScissorMode(ViewportLeft, ViewportTop, (int)ViewportWidth, (int)ViewportHeight);
        Raylib.DrawRectangle((int)screen.X + 2, (int)screen.Y + 2, (int)previewSize - 5, (int)previewSize - 5, previewColor);

        if (tool == BuildTool.Conveyor && valid && !conveyors.Cells.ContainsKey(position))
        {
            var preview = new ConveyorCell(position, direction, selectedConveyor);
            DrawConveyor(preview, conveyors, world, screen.X, screen.Y, tileSize, true);
        }
        else if (tool is BuildTool.Junction or BuildTool.Splitter or BuildTool.Bridge
            && valid
            && !conveyors.Cells.ContainsKey(position))
        {
            var preview = new ConveyorCell(position, direction, logisticsDef);
            DrawConveyor(preview, conveyors, world, screen.X, screen.Y, tileSize, true);
        }
        else if (tool == BuildTool.Miner && valid)
        {
            DrawMiner(new MinerBuilding(position, direction, world.CountCoveredDepositTiles(position)),
                screen.X, screen.Y, tileSize, true);
        }
        else if (tool == BuildTool.Smelter && valid)
        {
            DrawSmelter(new SmelterBuilding(position, direction, smeltRecipe), screen.X, screen.Y, tileSize, true);
        }
        else if (tool == BuildTool.Assembler && valid)
        {
            DrawAssembler(new SmelterBuilding(position, direction, wireRecipe), screen.X, screen.Y, tileSize, true);
        }
        else if (tool == BuildTool.Generator && valid)
        {
            DrawGenerator(new GeneratorBuilding(position), screen.X, screen.Y, tileSize, true);
        }

        Raylib.EndScissorMode();
    }

    private static void DrawInfoPanel(
        FactoryWorld world,
        ConveyorGrid conveyors,
        ResearchState research,
        EconomyWallet wallet,
        EconomySession session,
        MarketCatalog market,
        EconomyConfig economy)
    {
        GetInfoBounds(out var x, out var y, out var w, out var h);
        Raylib.DrawRectangle(x, y, w, h, UiTheme.PanelFill);
        UiTheme.DrawAccentRect(x, y, w, h, UiTheme.PanelBorder, 1);

        DrawUiText("MERCATO", x + 10, y + 8, 14, UiTheme.TextPrimary);
        var marketY = y + 30;
        var nameMaxW = w - 90;
        foreach (var item in market.Items.Take(4))
        {
            var effective = world.EffectiveSalePrice(item.ItemId, market);
            Raylib.DrawCircle(x + 16, marketY + 7, 4, ItemColor(item.ItemId));

            var name = item.DisplayName;
            while (name.Length > 3 && MeasureUiText(name, 12) > nameMaxW)
            {
                name = name[..^1];
            }

            if (name != item.DisplayName && name.Length > 0)
            {
                name = name.TrimEnd() + "…";
            }

            DrawUiText(name, x + 28, marketY, 12, UiTheme.TextMuted);

            // Price column right-aligned: base→effective or single price.
            string priceLabel;
            Color priceColor;
            if (effective != item.SellPrice)
            {
                priceLabel = $"${item.SellPrice}→${effective}";
                priceColor = new Color(211, 164, 76, 255);
            }
            else
            {
                priceLabel = $"${item.SellPrice}";
                priceColor = UiTheme.TextPrimary;
            }

            var pw = MeasureUiText(priceLabel, 12);
            DrawUiText(priceLabel, x + w - 10 - pw, marketY, 12, priceColor);
            marketY += 18;
        }

        var net = session.NetWorthDelta(wallet);
        DrawUiText(
            $"Netto {(net >= 0 ? "+" : "")}{net}  ·  PWR {world.PowerBuffer:0}/{world.PowerCapacity:0}",
            x + 10, marketY + 6, 11,
            net >= 0 ? new Color(112, 218, 145, 255) : new Color(225, 120, 100, 255));

        DrawUiText(
            $"M{world.Miners.Count} F{world.Smelters.Count} A{world.Assemblers.Count} N{conveyors.Cells.Count} G{world.Generators.Count}",
            x + 10, marketY + 24, 11, UiTheme.TextMuted);

        var tip = GetOnboardingTip(world, conveyors, research, wallet);
        DrawWrappedTip(tip, x + 10, marketY + 42, w - 20);

        var upgrade = economy.CoreUpgrade;
        var upgradeY = InfoUpgradeY(y);
        var upgradeLabel = world.CoreUpgradeLevel > 0
            ? $"CORE LV{world.CoreUpgradeLevel}"
            : $"CORE ${upgrade.MoneyCost}";
        DrawButton(x + 10, upgradeY, w - 20, 32, upgradeLabel, world.CoreUpgradeLevel > 0);
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
            return "Collega un nastro al CORE per vendere.";
        }

        if (!research.IsUnlocked("smelter") && wallet.Money >= 80)
        {
            return "Apri RICERCA (T) e sblocca il forno.";
        }

        if (research.IsUnlocked("smelter") && world.Smelters.Count == 0)
        {
            return "Piazza un FORNO per fondere il ferro.";
        }

        var crafters = world.Smelters.Count + world.Assemblers.Count;
        var powerLow = world.PowerCapacity > 0
            && world.PowerBuffer < world.PowerCapacity * 0.35f;
        if (powerLow || (world.Generators.Count == 0 && crafters >= 2))
        {
            return research.IsUnlocked("generator")
                ? "Piazza un GENERATORE (9) per più potenza."
                : "Sblocca il GENERATORE in RICERCA (T).";
        }

        var tips = new[]
        {
            "Esplora giacimenti di rame a sud-est.",
            "Sblocca lo sdoppiatore per biforcare i flussi.",
            "L'assemblatore trasforma il rame in fili.",
            "Vendi lastre al CORE per guadagnare di più."
        };
        var index = (int)(Raylib.GetTime() / 8.0) % tips.Length;
        return tips[index];
    }

    private static void DrawWrappedTip(string tip, int x, int y, int maxWidth)
    {
        const int fontSize = 12;
        if (MeasureUiText(tip, fontSize) <= maxWidth)
        {
            DrawUiText(tip, x, y, fontSize, new Color(211, 164, 76, 255));
            return;
        }

        var words = tip.Split(' ');
        var line = string.Empty;
        var lineY = y;
        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(line) ? word : $"{line} {word}";
            if (MeasureUiText(candidate, fontSize) > maxWidth && !string.IsNullOrEmpty(line))
            {
                DrawUiText(line, x, lineY, fontSize, new Color(211, 164, 76, 255));
                lineY += 16;
                line = word;
            }
            else
            {
                line = candidate;
            }
        }

        if (!string.IsNullOrEmpty(line))
        {
            DrawUiText(line, x, lineY, fontSize, new Color(211, 164, 76, 255));
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
}
