using System.Numerics;
using Raylib_cs;
using TIndustry.Logistics;

internal enum BuildTool
{
    Conveyor,
    Miner,
    Smelter,
    Remove
}

internal enum AppScreen
{
    Home,
    Playing,
    SaveManager,
    Research
}

internal static class FactoryGameApp
{
    private const int ScreenWidth = 1240;
    private const int ScreenHeight = 760;
    public const int MapWidth = 1000;
    public const int MapHeight = 1000;
    private const int BaseTileSize = 36;
    private const int HeaderHeight = 132;
    private const int PanelWidth = 296;
    private const int ViewportLeft = 0;
    private const int ViewportTop = HeaderHeight;
    private const int ViewportRight = ScreenWidth - PanelWidth;
    private const int ViewportBottom = ScreenHeight;
    private const float FixedStep = 1f / 30f;
    private const int DefaultSeed = 7429;

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
        var smeltRecipe = content.Recipes.Single(recipe => recipe.Id == "smelt-iron");
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
        var quitRequested = false;

        // Headless smoke/capture paths jump straight into a playable session.
        if (maximumFrames is not null || screenshotPath is not null)
        {
            StartNewGame(content, out world, out conveyors, out wallet, out camera, out research, out session, out market, out nextItemId);
            screen = AppScreen.Playing;
        }

        Raylib.SetConfigFlags(ConfigFlags.VSyncHint);
        Raylib.InitWindow(ScreenWidth, ScreenHeight, "tIndustry");
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(KeyboardKey.Null);

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
                        ref quitRequested);
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
                        basicConveyor,
                        fastConveyor,
                        smeltRecipe,
                        ref selectedConveyor,
                        ref tool,
                        ref direction,
                        ref previousDragPosition,
                        ref isPanning,
                        ref panAnchor,
                        ref panCameraX,
                        ref panCameraY,
                        ref screen,
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
                case AppScreen.SaveManager:
                    DrawSaveManager(saveSlots, selectedSlotIndex, statusMessage);
                    break;
                case AppScreen.Research:
                    DrawResearch(content, wallet!, research!, selectedResearchIndex, statusMessage);
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
                        basicConveyor,
                        fastConveyor,
                        selectedConveyor,
                        smeltRecipe,
                        minerBuilding,
                        smelterBuilding,
                        tool,
                        direction);
                    break;
            }

            Raylib.EndDrawing();

            if (screenshotPath is not null && renderedFrames == 1)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
                Raylib.TakeScreenshot(screenshotPath);
            }

            renderedFrames++;
        }

        if ((screen is AppScreen.Playing or AppScreen.Research)
            && world is not null && camera is not null && research is not null)
        {
            AutoSaveContinue(world, conveyors!, wallet!, camera, research, session!, nextItemId);
        }

        Raylib.CloseWindow();
    }

    private static void StartNewGame(
        GameContent content,
        out FactoryWorld world,
        out ConveyorGrid conveyors,
        out EconomyWallet wallet,
        out WorldCamera camera,
        out ResearchState research,
        out EconomySession session,
        out MarketCatalog market,
        out long nextItemId)
    {
        world = new FactoryWorld(MapWidth, MapHeight, DefaultSeed);
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
        ref bool quitRequested)
    {
        var mouse = Raylib.GetMousePosition();
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            return;
        }

        if (Contains(mouse, HomeButtonX, HomeButtonY(0), HomeButtonWidth, HomeButtonHeight))
        {
            if (TryLoadSlot(
                    GameSaveStore.ContinueSlotId,
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
                statusMessage = null;
                screen = AppScreen.Playing;
            }
            else
            {
                statusMessage = string.IsNullOrEmpty(error)
                    ? "Nessuna partita da continuare."
                    : error;
            }

            return;
        }

        if (Contains(mouse, HomeButtonX, HomeButtonY(1), HomeButtonWidth, HomeButtonHeight))
        {
            StartNewGame(content, out world, out conveyors, out wallet, out camera, out research, out session, out market, out nextItemId);
            tool = BuildTool.Conveyor;
            direction = Direction.East;
            selectedConveyor = basicConveyor;
            previousDragPosition = null;
            statusMessage = null;
            screen = AppScreen.Playing;
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
            quitRequested = true;
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
            if (TryLoadSlot(
                    selected.Id,
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
                statusMessage = null;
                screen = AppScreen.Playing;
            }
            else
            {
                statusMessage = error;
            }

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
        ConveyorDefinition basicConveyor,
        ConveyorDefinition fastConveyor,
        RecipeDefinition smeltRecipe,
        ref ConveyorDefinition selectedConveyor,
        ref BuildTool tool,
        ref Direction direction,
        ref GridPosition? previousDragPosition,
        ref bool isPanning,
        ref Vector2 panAnchor,
        ref float panCameraX,
        ref float panCameraY,
        ref AppScreen screen,
        ref int selectedResearchIndex,
        ref string? statusMessage,
        long nextItemId,
        float frameTime)
    {
        _ = market;
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left)
                && Contains(Raylib.GetMousePosition(), ScreenWidth - 170, 20, 140, 36)))
        {
            AutoSaveContinue(world, conveyors, wallet, camera, research, session, nextItemId);
            statusMessage = null;
            screen = AppScreen.Home;
            return;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.T)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left)
                && Contains(Raylib.GetMousePosition(), ScreenWidth - 320, 20, 140, 36)))
        {
            selectedResearchIndex = 0;
            statusMessage = null;
            screen = AppScreen.Research;
            return;
        }

        UpdateCamera(camera, world, ref isPanning, ref panAnchor, ref panCameraX, ref panCameraY, frameTime);

        if (Raylib.IsKeyPressed(KeyboardKey.R))
        {
            direction = (Direction)(((int)direction + 1) % 4);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.One))
        {
            tool = BuildTool.Conveyor;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Two) && research.IsUnlocked("miner"))
        {
            tool = BuildTool.Miner;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Three) && research.IsUnlocked("smelter"))
        {
            tool = BuildTool.Smelter;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Four))
        {
            tool = BuildTool.Remove;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Q))
        {
            selectedConveyor = basicConveyor;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.E) && research.IsUnlocked(fastConveyor.Id))
        {
            selectedConveyor = fastConveyor;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.U)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left)
                && Contains(Raylib.GetMousePosition(), ViewportRight + 22, ViewportTop + 520, 252, 36)))
        {
            if (world.TryUpgradeCore(wallet, economy.CoreUpgrade, session))
            {
                statusMessage = "Core potenziato: +vendite!";
            }
        }

        var mouse = Raylib.GetMousePosition();
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (TrySelectToolbar(mouse, research, basicConveyor, fastConveyor, ref tool, ref direction, ref selectedConveyor))
            {
                previousDragPosition = null;
                return;
            }
        }

        if (isPanning)
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
                    conveyors.TryPlace(position, direction, selectedConveyor, wallet, research, session);
                    ConnectAdjacentMiner(world, conveyors, position);
                    ConnectAdjacentSmelter(world, conveyors, position);
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
            else if (tool == BuildTool.Remove
                && !world.TryRemoveMiner(position, wallet, minerBuilding, session)
                && !world.TryRemoveSmelter(position, wallet, smelterBuilding, session))
            {
                conveyors.TryRemove(position, wallet, session);
            }
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            var cell = MouseCell(mouse, camera, world);
            if (cell is { } position
                && !world.TryRemoveMiner(position, wallet, minerBuilding, session)
                && !world.TryRemoveSmelter(position, wallet, smelterBuilding, session))
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
        float frameTime)
    {
        var mouse = Raylib.GetMousePosition();
        var wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0
            && mouse.X >= ViewportLeft
            && mouse.X < ViewportRight
            && mouse.Y >= ViewportTop
            && mouse.Y < ViewportBottom)
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

        var inViewport = mouse.X >= ViewportLeft
            && mouse.X < ViewportRight
            && mouse.Y >= ViewportTop
            && mouse.Y < ViewportBottom;

        if (inViewport && !isPanning)
        {
            var edge = Vector2.Zero;
            if (mouse.X <= ViewportLeft + WorldCamera.EdgePanMargin)
            {
                edge.X -= 1f;
            }

            if (mouse.X >= ViewportRight - WorldCamera.EdgePanMargin)
            {
                edge.X += 1f;
            }

            if (mouse.Y <= ViewportTop + WorldCamera.EdgePanMargin)
            {
                edge.Y -= 1f;
            }

            if (mouse.Y >= ViewportBottom - WorldCamera.EdgePanMargin)
            {
                edge.Y += 1f;
            }

            if (edge != Vector2.Zero)
            {
                edge = Vector2.Normalize(edge) * (WorldCamera.EdgePanSpeed / camera.Zoom) * frameTime;
                camera.Pan(edge.X, edge.Y);
            }
        }

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
                    || !conveyors.TryPlace(next, stepDirection, definition, wallet, research, session))
                {
                    return;
                }

                ConnectAdjacentMiner(world, conveyors, next);
                ConnectAdjacentSmelter(world, conveyors, next);
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

    private static bool TrySelectToolbar(
        Vector2 mouse,
        ResearchState research,
        ConveyorDefinition basicConveyor,
        ConveyorDefinition fastConveyor,
        ref BuildTool tool,
        ref Direction direction,
        ref ConveyorDefinition selectedConveyor)
    {
        if (Contains(mouse, 28, 88, 100, 34))
        {
            tool = BuildTool.Conveyor;
            return true;
        }

        if (Contains(mouse, 138, 88, 100, 34) && research.IsUnlocked("miner"))
        {
            tool = BuildTool.Miner;
            return true;
        }

        if (Contains(mouse, 248, 88, 100, 34) && research.IsUnlocked("smelter"))
        {
            tool = BuildTool.Smelter;
            return true;
        }

        if (Contains(mouse, 358, 88, 100, 34))
        {
            tool = BuildTool.Remove;
            return true;
        }

        if (Contains(mouse, 480, 88, 70, 34))
        {
            selectedConveyor = basicConveyor;
            tool = BuildTool.Conveyor;
            return true;
        }

        if (Contains(mouse, 556, 88, 70, 34) && research.IsUnlocked(fastConveyor.Id))
        {
            selectedConveyor = fastConveyor;
            tool = BuildTool.Conveyor;
            return true;
        }

        for (var index = 0; index < Directions.Length; index++)
        {
            if (Contains(mouse, 650 + index * 40, 88, 34, 34))
            {
                direction = Directions[index];
                return true;
            }
        }

        return false;
    }

    private const int HomeButtonX = 420;
    private const int HomeButtonWidth = 400;
    private const int HomeButtonHeight = 52;

    private static int HomeButtonY(int index) => 250 + index * 70;

    private static void DrawHome(string? statusMessage)
    {
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color(14, 18, 18, 255));
        Raylib.DrawRectangleGradientV(0, 0, ScreenWidth, ScreenHeight,
            new Color(18, 28, 24, 255), new Color(10, 12, 12, 255));
        Raylib.DrawText("tINDUSTRY", 420, 120, 48, new Color(239, 238, 224, 255));
        Raylib.DrawText("Settore Foundry — mappa 1000×1000", 420, 180, 18, new Color(112, 124, 119, 255));

        DrawMenuButton(HomeButtonX, HomeButtonY(0), HomeButtonWidth, HomeButtonHeight, "Continua");
        DrawMenuButton(HomeButtonX, HomeButtonY(1), HomeButtonWidth, HomeButtonHeight, "Nuova partita");
        DrawMenuButton(HomeButtonX, HomeButtonY(2), HomeButtonWidth, HomeButtonHeight, "Gestione salvataggi");
        DrawMenuButton(HomeButtonX, HomeButtonY(3), HomeButtonWidth, HomeButtonHeight, "Esci");

        if (!string.IsNullOrEmpty(statusMessage))
        {
            Raylib.DrawText(statusMessage, 420, 540, 18, new Color(225, 140, 110, 255));
        }

        Raylib.DrawText("WASD / bordi / Shift+drag: pan   ·   rotella: zoom   ·   T: ricerca   ·   Esc: menu",
            180, ScreenHeight - 40, 16, new Color(90, 100, 96, 255));
    }

    private static void DrawResearch(
        GameContent content,
        EconomyWallet wallet,
        ResearchState research,
        int selectedIndex,
        string? statusMessage)
    {
        Raylib.DrawRectangle(0, 0, ScreenWidth, ScreenHeight, new Color(14, 18, 18, 255));
        Raylib.DrawText("Ricerca / Sblocchi", 60, 36, 32, new Color(239, 238, 224, 255));
        Raylib.DrawText("Seleziona una struttura, verifica i requisiti, conferma per sbloccare.", 60, 80, 18,
            new Color(112, 124, 119, 255));
        Raylib.DrawText($"Wallet: $ {wallet.Money}   ·   Piastre {wallet.MaterialCount("iron-plate")}   ·   Fili {wallet.MaterialCount("copper-wire")}",
            60, 108, 16, new Color(164, 173, 168, 255));

        var entries = ResearchEntries(content);
        if (entries.Count == 0)
        {
            Raylib.DrawText("Nessuna struttura da sbloccare.", 60, 160, 22, new Color(164, 173, 168, 255));
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
                Raylib.DrawText(structure.DisplayName, 76, y + 8, 20, new Color(232, 233, 221, 255));
                Raylib.DrawText(
                    $"{status}  ·  {FormatUnlockRequirement(structure.Unlock)}{(structure.IsStub ? "  ·  stub" : "")}",
                    76, y + 28, 14, accent);
            }

            var selectedStructure = entries[selectedIndex];
            var canUnlock = research.CanUnlock(selectedStructure, wallet);
            var buttonLabel = research.IsUnlocked(selectedStructure.Id)
                ? "Già sbloccato"
                : canUnlock ? "Conferma sblocco" : "Risorse insufficienti";
            DrawMenuButton(800, 140, 320, 50, buttonLabel);
            Raylib.DrawText("Lo sblocco consuma denaro e materiali.", 800, 210, 14, new Color(126, 137, 132, 255));
            if (selectedStructure.IsStub)
            {
                Raylib.DrawText("Stub: non piazzabile in Phase 3.", 800, 234, 14, new Color(180, 120, 100, 255));
            }
        }

        DrawMenuButton(28, ScreenHeight - 70, 180, 40, "Indietro");
        if (!string.IsNullOrEmpty(statusMessage))
        {
            Raylib.DrawText(statusMessage, 230, ScreenHeight - 58, 18, new Color(225, 140, 110, 255));
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
        Raylib.DrawText("Gestione salvataggi", 60, 40, 32, new Color(239, 238, 224, 255));
        Raylib.DrawText("Seleziona uno slot, poi Carica o Elimina.", 60, 90, 18, new Color(112, 124, 119, 255));

        if (slots.Count == 0)
        {
            Raylib.DrawText("Nessun salvataggio presente.", 60, 160, 22, new Color(164, 173, 168, 255));
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
                Raylib.DrawText(
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
            Raylib.DrawText(statusMessage, 230, ScreenHeight - 58, 18, new Color(225, 140, 110, 255));
        }
    }

    private static void DrawMenuButton(int x, int y, int width, int height, string label)
    {
        var mouse = Raylib.GetMousePosition();
        var hover = Contains(mouse, x, y, width, height);
        Raylib.DrawRectangle(x, y, width, height,
            hover ? new Color(211, 164, 76, 255) : new Color(45, 52, 50, 255));
        var text = hover ? new Color(25, 28, 26, 255) : new Color(215, 219, 210, 255);
        var textWidth = Raylib.MeasureText(label, 20);
        Raylib.DrawText(label, x + (width - textWidth) / 2, y + (height - 20) / 2, 20, text);
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
        ConveyorDefinition basicConveyor,
        ConveyorDefinition fastConveyor,
        ConveyorDefinition selectedConveyor,
        RecipeDefinition smeltRecipe,
        BuildingDefinition minerBuilding,
        BuildingDefinition smelterBuilding,
        BuildTool tool,
        Direction direction)
    {
        DrawWorld(world, conveyors, camera);
        DrawPreview(
            world, conveyors, wallet, research, camera, selectedConveyor, smeltRecipe,
            minerBuilding, smelterBuilding, tool, direction);
        DrawHeader(
            wallet, research, session, market, economy, basicConveyor, fastConveyor,
            selectedConveyor, minerBuilding, smelterBuilding, tool, direction, world, camera);
        DrawPanel(world, conveyors, research, wallet, session, market, economy);
    }

    private static void DrawHeader(
        EconomyWallet wallet,
        ResearchState research,
        EconomySession session,
        MarketCatalog market,
        EconomyConfig economy,
        ConveyorDefinition basicConveyor,
        ConveyorDefinition fastConveyor,
        ConveyorDefinition selectedConveyor,
        BuildingDefinition minerBuilding,
        BuildingDefinition smelterBuilding,
        BuildTool tool,
        Direction direction,
        FactoryWorld world,
        WorldCamera camera)
    {
        Raylib.DrawRectangle(0, 0, ScreenWidth, HeaderHeight, new Color(16, 20, 20, 245));
        Raylib.DrawText("tINDUSTRY", 28, 14, 26, new Color(239, 238, 224, 255));
        Raylib.DrawText(
            $"FOUNDRY  seed {world.Seed}  ·  {world.Terrain.Width}×{world.Terrain.Height}  ·  zoom {camera.Zoom:0.00}",
            28, 46, 13, new Color(112, 124, 119, 255));

        // Multi-material wallet
        Raylib.DrawText($"$ {wallet.Money}", 520, 14, 20, new Color(112, 218, 145, 255));
        Raylib.DrawCircle(524, 48, 5, ItemColor("iron-plate"));
        Raylib.DrawText($"Lastre {wallet.MaterialCount("iron-plate")}", 536, 40, 14, new Color(196, 201, 193, 255));
        Raylib.DrawCircle(640, 48, 5, ItemColor("copper-wire"));
        Raylib.DrawText($"Fili {wallet.MaterialCount("copper-wire")}", 652, 40, 14, new Color(196, 201, 193, 255));

        var net = session.NetWorthDelta(wallet);
        var netColor = net >= 0 ? new Color(112, 218, 145, 255) : new Color(225, 120, 100, 255);
        Raylib.DrawText($"Sessione {(net >= 0 ? "+" : "")}{net}", 520, 62, 13, netColor);

        DrawButton(28, 88, 100, 34, "NASTRO", tool == BuildTool.Conveyor);
        DrawButton(138, 88, 100, 34, research.IsUnlocked("miner") ? "MINATORE" : "LOCK",
            tool == BuildTool.Miner);
        DrawButton(248, 88, 100, 34, research.IsUnlocked("smelter") ? "FORNO" : "LOCK",
            tool == BuildTool.Smelter);
        DrawButton(358, 88, 100, 34, "RIMUOVI", tool == BuildTool.Remove);

        var fastUnlocked = research.IsUnlocked(fastConveyor.Id);
        DrawButton(480, 88, 70, 34, "BASE", selectedConveyor.Id == basicConveyor.Id);
        DrawButton(556, 88, 70, 34, fastUnlocked ? "VELOCE" : "LOCK",
            selectedConveyor.Id == fastConveyor.Id);

        var labels = new[] { "N", "E", "S", "O" };
        for (var index = 0; index < Directions.Length; index++)
        {
            DrawButton(650 + index * 40, 88, 34, 34, labels[index], direction == Directions[index]);
        }

        var cost = tool switch
        {
            BuildTool.Miner => research.IsUnlocked("miner")
                ? FormatBuildingCost(minerBuilding)
                : "Sblocca in Ricerca",
            BuildTool.Smelter => research.IsUnlocked("smelter")
                ? FormatBuildingCost(smelterBuilding)
                : "Sblocca in Ricerca",
            BuildTool.Conveyor => FormatConveyorCost(selectedConveyor, research),
            _ => economy.RefundPolicyNote
        };
        Raylib.DrawText(cost, 28, 70, 14, new Color(164, 173, 168, 255));
        DrawButton(ScreenWidth - 320, 20, 140, 36, "RICERCA", false);
        DrawButton(ScreenWidth - 170, 20, 140, 36, "MENU", false);
        _ = market;
    }

    private static string FormatBuildingCost(BuildingDefinition building)
    {
        var plates = building.BuildCost.FirstOrDefault(entry => entry.ItemId == "iron-plate")?.Amount ?? 0;
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
        var size = Math.Max(1, (int)MathF.Ceiling(tileSize));
        var ix = (int)x;
        var iy = (int)y;
        var color = tile.Terrain switch
        {
            TerrainKind.Grass => new Color(51, 73, 56, 255),
            TerrainKind.Soil => new Color(81, 70, 52, 255),
            TerrainKind.Stone => new Color(69, 74, 72, 255),
            TerrainKind.Water => new Color(35, 77, 91, 255),
            _ => Color.Black
        };
        Raylib.DrawRectangle(ix, iy, size, size, color);
        if (tileSize >= 10f)
        {
            Raylib.DrawRectangleLines(ix, iy, size, size, new Color(20, 27, 26, 38));
        }

        if (tileSize >= 14f)
        {
            var detail = (position.X * 17 + position.Y * 31) % 19;
            var detailColor = new Color(color.R + 7, color.G + 7, color.B + 6, 150);
            Raylib.DrawRectangle(
                ix + (int)(5 * tileSize / BaseTileSize) + detail % 14,
                iy + (int)(6 * tileSize / BaseTileSize) + detail * 2 % 19,
                Math.Max(1, (int)(3 * tileSize / BaseTileSize)),
                Math.Max(1, (int)(3 * tileSize / BaseTileSize)),
                detailColor);
        }

        if (tile.Deposit == DepositKind.Iron && tileSize >= 8f)
        {
            var s = tileSize / BaseTileSize;
            Raylib.DrawCircle(ix + (int)(10 * s), iy + (int)(12 * s), Math.Max(1.5f, 4 * s), new Color(151, 88, 51, 255));
            Raylib.DrawCircle(ix + (int)(24 * s), iy + (int)(21 * s), Math.Max(2f, 6 * s), new Color(205, 132, 73, 255));
            Raylib.DrawCircle(ix + (int)(12 * s), iy + (int)(27 * s), Math.Max(1f, 3 * s), new Color(236, 168, 91, 255));
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
            Raylib.DrawText("CORE", x + size / 2 - 24, y + size - (int)(34 * scale), Math.Max(10, (int)(18 * scale)),
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
            var efficiencyWidth = Raylib.MeasureText(efficiencyLabel, 12);
            Raylib.DrawRectangle(x + (size - efficiencyWidth) / 2 - 4, y + size - 27,
                efficiencyWidth + 8, 16, new Color(20, 24, 23, alpha));
            Raylib.DrawText(efficiencyLabel, x + (size - efficiencyWidth) / 2, y + size - 25,
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
            Raylib.DrawText("FORNO", x + size / 2 - 22, y + 8, 12, new Color(240, 200, 170, alpha));
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

        foreach (var item in conveyor.Items)
        {
            var vector = DirectionVector(conveyor.Direction);
            var itemPosition = center - vector * (tileSize * 0.5f) + vector * (item.Progress * tileSize);
            var itemSize = Math.Max(4, (int)(10 * tileSize / BaseTileSize));
            var fill = ItemColor(item.ItemId);
            Raylib.DrawRectangle((int)itemPosition.X - itemSize / 2, (int)itemPosition.Y - itemSize / 2, itemSize, itemSize,
                fill);
            Raylib.DrawRectangleLines((int)itemPosition.X - itemSize / 2, (int)itemPosition.Y - itemSize / 2, itemSize, itemSize,
                new Color(255, 240, 210, 255));
        }
    }

    private static Color ItemColor(string itemId) => itemId switch
    {
        "iron-ore" => new Color(211, 117, 55, 255),
        "iron-plate" => new Color(168, 184, 196, 255),
        "copper-wire" => new Color(196, 132, 72, 255),
        _ => new Color(200, 90, 200, 255)
    };

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
        if (world.IsMinerTile(neighborPosition) || world.IsSmelterTile(neighborPosition))
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
        ConveyorDefinition definition,
        RecipeDefinition smeltRecipe,
        BuildingDefinition minerBuilding,
        BuildingDefinition smelterBuilding,
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
        var valid = tool switch
        {
            BuildTool.Conveyor => world.CanPlaceConveyor(position)
                && !conveyors.Cells.ContainsKey(position)
                && research.IsUnlocked(definition.Id)
                && wallet.CanAfford(definition.MoneyCost, definition.BuildCost),
            BuildTool.Miner => research.IsUnlocked("miner")
                && world.CanPlaceMiner(position, conveyors)
                && wallet.CanAfford(minerBuilding.MoneyCost, minerBuilding.BuildCost),
            BuildTool.Smelter => research.IsUnlocked("smelter")
                && world.CanPlaceSmelter(position, conveyors)
                && wallet.CanAfford(smelterBuilding.MoneyCost, smelterBuilding.BuildCost),
            BuildTool.Remove => world.Miners.ContainsKey(position)
                || world.IsMinerTile(position)
                || world.Smelters.ContainsKey(position)
                || world.IsSmelterTile(position)
                || conveyors.Cells.ContainsKey(position),
            _ => false
        };

        if (tool == BuildTool.Conveyor
            && conveyors.Cells.TryGetValue(position, out var existing)
            && definition.Tier > existing.Definition.Tier
            && research.IsUnlocked(definition.Id)
            && wallet.CanAfford(definition.MoneyCost, definition.BuildCost))
        {
            valid = true;
        }

        var previewColor = valid
            ? new Color(105, 225, 142, 125)
            : new Color(225, 92, 80, 125);
        var previewSize = tool is BuildTool.Miner or BuildTool.Smelter
            ? tileSize * MinerBuilding.Size
            : tileSize;
        Raylib.BeginScissorMode(ViewportLeft, ViewportTop, (int)ViewportWidth, (int)ViewportHeight);
        Raylib.DrawRectangle((int)screen.X + 2, (int)screen.Y + 2, (int)previewSize - 5, (int)previewSize - 5, previewColor);

        if (tool == BuildTool.Conveyor && valid && !conveyors.Cells.ContainsKey(position))
        {
            var preview = new ConveyorCell(position, direction, definition);
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

        Raylib.EndScissorMode();
    }

    private static void DrawPanel(
        FactoryWorld world,
        ConveyorGrid conveyors,
        ResearchState research,
        EconomyWallet wallet,
        EconomySession session,
        MarketCatalog market,
        EconomyConfig economy)
    {
        Raylib.DrawRectangle(ViewportRight, ViewportTop, PanelWidth, (int)ViewportHeight, new Color(24, 29, 29, 255));
        Raylib.DrawText("MERCATO", ViewportRight + 22, ViewportTop + 22, 20, new Color(232, 233, 221, 255));
        Raylib.DrawLine(ViewportRight + 22, ViewportTop + 54, ViewportRight + 274, ViewportTop + 54, new Color(62, 72, 68, 255));

        var marketY = ViewportTop + 70;
        foreach (var item in market.Items)
        {
            var effective = world.EffectiveSalePrice(item.ItemId, market);
            Raylib.DrawCircle(ViewportRight + 31, marketY + 8, 6, ItemColor(item.ItemId));
            var priceLabel = effective != item.SellPrice
                ? $"${item.SellPrice}→${effective}"
                : $"${item.SellPrice}";
            Raylib.DrawText($"{item.DisplayName}  {priceLabel}", ViewportRight + 48, marketY, 14,
                new Color(196, 201, 193, 255));
            marketY += 26;
        }

        Raylib.DrawText(market.BestValueHint(), ViewportRight + 22, marketY + 4, 12, new Color(211, 164, 76, 255));

        var ledgerY = marketY + 36;
        Raylib.DrawRectangle(ViewportRight + 22, ledgerY, 252, 1, new Color(62, 72, 68, 255));
        ledgerY += 12;
        Raylib.DrawText("SESSIONE", ViewportRight + 22, ledgerY, 14, new Color(164, 173, 168, 255));
        ledgerY += 22;
        var net = session.NetWorthDelta(wallet);
        DrawMetric("SALDO NETTO", $"{(net >= 0 ? "+" : "")}{net}", ViewportRight + 22, ledgerY,
            net >= 0 ? new Color(112, 218, 145, 255) : new Color(225, 120, 100, 255));
        ledgerY += 52;
        Raylib.DrawText($"Vendite +{session.SaleIncome}  ·  Build −{session.BuildSpend}",
            ViewportRight + 22, ledgerY, 12, new Color(140, 150, 145, 255));
        ledgerY += 18;
        Raylib.DrawText($"Unlock −{session.UnlockSpend}  ·  Upgrade −{session.UpgradeSpend}",
            ViewportRight + 22, ledgerY, 12, new Color(140, 150, 145, 255));
        ledgerY += 18;
        Raylib.DrawText($"Rimborsi +{session.RefundIncome}  ·  Item {world.SoldItems}",
            ViewportRight + 22, ledgerY, 12, new Color(140, 150, 145, 255));

        ledgerY += 28;
        Raylib.DrawText($"Minatori {world.Miners.Count}  Forni {world.Smelters.Count}  Nastri {conveyors.Cells.Count}",
            ViewportRight + 22, ledgerY, 13, new Color(180, 186, 178, 255));
        ledgerY += 22;
        Raylib.DrawText(
            research.IsUnlocked("smelter") ? "Forno SBLOCCATO" : "Forno bloccato",
            ViewportRight + 22, ledgerY, 13,
            research.IsUnlocked("smelter") ? new Color(112, 218, 145, 255) : new Color(180, 120, 100, 255));
        ledgerY += 18;
        Raylib.DrawText(
            research.IsUnlocked("conveyor-fast") ? "Nastro veloce SBLOCCATO" : "Nastro veloce bloccato",
            ViewportRight + 22, ledgerY, 13,
            research.IsUnlocked("conveyor-fast") ? new Color(112, 218, 145, 255) : new Color(180, 120, 100, 255));

        ledgerY += 28;
        Raylib.DrawText(economy.RefundPolicyNote, ViewportRight + 22, ledgerY, 11, new Color(126, 137, 132, 255));

        var upgrade = economy.CoreUpgrade;
        var upgradeY = ViewportTop + 520;
        var upgradeLabel = world.CoreUpgradeLevel > 0
            ? $"CORE LV{world.CoreUpgradeLevel} (+{world.CoreSaleBonusPercent}%)"
            : $"POTENZIA CORE ${upgrade.MoneyCost}+{upgrade.BuildCost.FirstOrDefault()?.Amount ?? 0}P";
        DrawButton(ViewportRight + 22, upgradeY, 252, 36, upgradeLabel, world.CoreUpgradeLevel > 0);
        Raylib.DrawText("U · potenzia vendite core", ViewportRight + 22, upgradeY + 44, 12,
            new Color(126, 137, 132, 255));
        Raylib.DrawText("T ricerca · Esc menu", ViewportRight + 22, upgradeY + 62, 12,
            new Color(126, 137, 132, 255));
    }

    private static void DrawMetric(string label, string value, int x, int y, Color accent)
    {
        Raylib.DrawText(label, x, y, 13, new Color(126, 137, 132, 255));
        Raylib.DrawText(value, x, y + 22, 22, accent);
    }

    private static void DrawButton(int x, int y, int width, int height, string label, bool active)
    {
        var fill = active ? new Color(211, 164, 76, 255) : new Color(45, 52, 50, 255);
        var text = active ? new Color(25, 28, 26, 255) : new Color(215, 219, 210, 255);
        Raylib.DrawRectangle(x, y, width, height, fill);
        var textWidth = Raylib.MeasureText(label, 15);
        Raylib.DrawText(label, x + (width - textWidth) / 2, y + 12, 15, text);
    }

    private static GridPosition? MouseCell(Vector2 mouse, WorldCamera camera, FactoryWorld world) =>
        camera.ScreenToCell(
            mouse.X,
            mouse.Y,
            ViewportLeft,
            ViewportTop,
            ViewportRight,
            ViewportBottom,
            world.Terrain.Width,
            world.Terrain.Height,
            BaseTileSize);

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
