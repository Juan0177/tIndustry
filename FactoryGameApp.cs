using System.Numerics;
using Raylib_cs;
using TIndustry.Logistics;

internal enum BuildTool
{
    Conveyor,
    Miner,
    Remove
}

internal enum AppScreen
{
    Home,
    Playing,
    SaveManager
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
        var conveyorDefinition = content.Conveyors.Single(definition => definition.Id == "conveyor-basic");
        var screen = AppScreen.Home;
        FactoryWorld? world = null;
        ConveyorGrid? conveyors = null;
        EconomyWallet? wallet = null;
        WorldCamera? camera = null;
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
        var quitRequested = false;

        // Headless smoke/capture paths jump straight into a playable session.
        if (maximumFrames is not null || screenshotPath is not null)
        {
            StartNewGame(content, out world, out conveyors, out wallet, out camera, out nextItemId);
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
                        ref nextItemId,
                        ref tool,
                        ref direction,
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
                        ref nextItemId,
                        ref tool,
                        ref direction,
                        ref previousDragPosition,
                        ref statusMessage,
                        ref saveSlots,
                        ref selectedSlotIndex);
                    break;

                case AppScreen.Playing:
                    HandlePlayingInput(
                        world!,
                        conveyors!,
                        wallet!,
                        camera!,
                        conveyorDefinition,
                        ref tool,
                        ref direction,
                        ref previousDragPosition,
                        ref isPanning,
                        ref panAnchor,
                        ref panCameraX,
                        ref panCameraY,
                        ref screen,
                        ref statusMessage,
                        nextItemId,
                        frameTime);
                    while (accumulator >= FixedStep)
                    {
                        world!.Update(FixedStep, conveyors!, wallet!, ref nextItemId);
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
                case AppScreen.Playing:
                    DrawPlaying(world!, conveyors!, wallet!, camera!, conveyorDefinition, tool, direction);
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

        if (screen == AppScreen.Playing && world is not null && camera is not null)
        {
            AutoSaveContinue(world, conveyors!, wallet!, camera, nextItemId);
        }

        Raylib.CloseWindow();
    }

    private static void StartNewGame(
        GameContent content,
        out FactoryWorld world,
        out ConveyorGrid conveyors,
        out EconomyWallet wallet,
        out WorldCamera camera,
        out long nextItemId)
    {
        world = new FactoryWorld(MapWidth, MapHeight, DefaultSeed);
        conveyors = new ConveyorGrid();
        wallet = CreateStartingWallet();
        camera = CreateCameraFocusedOnCore(world);
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
        long nextItemId)
    {
        var data = GameSaveStore.Capture(world, conveyors, wallet, camera, nextItemId);
        GameSaveStore.Save(GameSaveStore.ContinueSlotId, data);
    }

    private static bool TryLoadSlot(
        string slotId,
        GameContent content,
        out FactoryWorld world,
        out ConveyorGrid conveyors,
        out EconomyWallet wallet,
        out WorldCamera camera,
        out long nextItemId,
        out string error)
    {
        world = null!;
        conveyors = null!;
        wallet = null!;
        camera = null!;
        nextItemId = 1L;
        error = string.Empty;
        if (!GameSaveStore.TryLoad(slotId, out var data))
        {
            error = "Salvataggio non disponibile.";
            return false;
        }

        try
        {
            (world, conveyors, wallet, camera, nextItemId) = GameSaveStore.Restore(data, content);
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
        ref long nextItemId,
        ref BuildTool tool,
        ref Direction direction,
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
                    out nextItemId,
                    out var error))
            {
                tool = BuildTool.Conveyor;
                direction = Direction.East;
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
            StartNewGame(content, out world, out conveyors, out wallet, out camera, out nextItemId);
            tool = BuildTool.Conveyor;
            direction = Direction.East;
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
        ref long nextItemId,
        ref BuildTool tool,
        ref Direction direction,
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
                    out nextItemId,
                    out var error))
            {
                tool = BuildTool.Conveyor;
                direction = Direction.East;
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

    private static void HandlePlayingInput(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        WorldCamera camera,
        ConveyorDefinition definition,
        ref BuildTool tool,
        ref Direction direction,
        ref GridPosition? previousDragPosition,
        ref bool isPanning,
        ref Vector2 panAnchor,
        ref float panCameraX,
        ref float panCameraY,
        ref AppScreen screen,
        ref string? statusMessage,
        long nextItemId,
        float frameTime)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)
            || (Raylib.IsMouseButtonPressed(MouseButton.Left)
                && Contains(Raylib.GetMousePosition(), ScreenWidth - 170, 20, 140, 36)))
        {
            AutoSaveContinue(world, conveyors, wallet, camera, nextItemId);
            statusMessage = null;
            screen = AppScreen.Home;
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

        if (Raylib.IsKeyPressed(KeyboardKey.Two))
        {
            tool = BuildTool.Miner;
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Three))
        {
            tool = BuildTool.Remove;
        }

        var mouse = Raylib.GetMousePosition();
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (TrySelectToolbar(mouse, ref tool, ref direction))
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
                    existing.Rotate(direction);
                }
                else if (world.CanPlaceConveyor(position))
                {
                    conveyors.TryPlace(position, direction, definition, wallet);
                    ConnectAdjacentMiner(world, conveyors, position);
                    ConnectToAdjacentCore(world, conveyors, position);
                }

                previousDragPosition = position;
                return;
            }

            ExtendConveyorPath(world, conveyors, wallet, definition, previous, position, ref direction);
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

            if (tool == BuildTool.Miner)
            {
                world.TryPlaceMiner(position, direction, conveyors, wallet);
            }
            else if (tool == BuildTool.Remove && !world.TryRemoveMiner(position, wallet))
            {
                conveyors.TryRemove(position, wallet);
            }
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Right))
        {
            var cell = MouseCell(mouse, camera, world);
            if (cell is { } position && !world.TryRemoveMiner(position, wallet))
            {
                conveyors.TryRemove(position, wallet);
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
                    || !conveyors.TryPlace(next, stepDirection, definition, wallet))
                {
                    return;
                }

                ConnectAdjacentMiner(world, conveyors, next);
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

    private static bool TrySelectToolbar(Vector2 mouse, ref BuildTool tool, ref Direction direction)
    {
        var tools = new[] { BuildTool.Conveyor, BuildTool.Miner, BuildTool.Remove };
        for (var index = 0; index < tools.Length; index++)
        {
            if (Contains(mouse, 28 + index * 126, 92, 116, 38))
            {
                tool = tools[index];
                return true;
            }
        }

        for (var index = 0; index < Directions.Length; index++)
        {
            if (Contains(mouse, 430 + index * 46, 92, 38, 38))
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

        Raylib.DrawText("WASD / bordi / Shift+drag o rotella centrale: pan   ·   rotella: zoom   ·   Esc: menu",
            220, ScreenHeight - 40, 16, new Color(90, 100, 96, 255));
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
        ConveyorDefinition definition,
        BuildTool tool,
        Direction direction)
    {
        DrawWorld(world, conveyors, camera);
        DrawPreview(world, conveyors, wallet, camera, definition, tool, direction);
        DrawHeader(wallet, definition, tool, direction, world, camera);
        DrawPanel(world, conveyors);
    }

    private static void DrawHeader(
        EconomyWallet wallet,
        ConveyorDefinition definition,
        BuildTool tool,
        Direction direction,
        FactoryWorld world,
        WorldCamera camera)
    {
        Raylib.DrawRectangle(0, 0, ScreenWidth, HeaderHeight, new Color(16, 20, 20, 245));
        Raylib.DrawText("tINDUSTRY", 28, 18, 28, new Color(239, 238, 224, 255));
        Raylib.DrawText(
            $"FOUNDRY  seed {world.Seed}  ·  {world.Terrain.Width}×{world.Terrain.Height}  ·  zoom {camera.Zoom:0.00}",
            28, 52, 14, new Color(112, 124, 119, 255));
        Raylib.DrawText($"$ {wallet.Money}", 780, 22, 24, new Color(112, 218, 145, 255));
        Raylib.DrawText($"PIASTRE  {wallet.MaterialCount("iron-plate")}", 780, 54, 17, new Color(220, 179, 93, 255));

        DrawButton(28, 92, 116, 38, "NASTRO", tool == BuildTool.Conveyor);
        DrawButton(154, 92, 116, 38, "MINATORE", tool == BuildTool.Miner);
        DrawButton(280, 92, 116, 38, "RIMUOVI", tool == BuildTool.Remove);

        var labels = new[] { "N", "E", "S", "O" };
        for (var index = 0; index < Directions.Length; index++)
        {
            DrawButton(430 + index * 46, 92, 38, 38, labels[index], direction == Directions[index]);
        }

        var cost = tool == BuildTool.Miner
            ? $"${FactoryWorld.MinerMoneyCost} + {FactoryWorld.MinerPlateCost} PIASTRE"
            : tool == BuildTool.Conveyor
                ? $"${definition.MoneyCost} + {definition.BuildCost[0].Amount} PIASTRA"
                : "RIMBORSO COMPLETO";
        Raylib.DrawText(cost, 650, 103, 16, new Color(164, 173, 168, 255));
        DrawButton(ScreenWidth - 170, 20, 140, 36, "MENU", false);
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
        Raylib.DrawRectangle(x + size / 4, y + size / 4, size / 2, size / 2, new Color(70, 77, 74, alpha));
        DrawDirectionMark(center, conveyor.Direction, alpha, tileSize);

        foreach (var item in conveyor.Items)
        {
            var vector = DirectionVector(conveyor.Direction);
            var itemPosition = center - vector * (tileSize * 0.5f) + vector * (item.Progress * tileSize);
            var itemSize = Math.Max(4, (int)(10 * tileSize / BaseTileSize));
            Raylib.DrawRectangle((int)itemPosition.X - itemSize / 2, (int)itemPosition.Y - itemSize / 2, itemSize, itemSize,
                new Color(211, 117, 55, 255));
            Raylib.DrawRectangleLines((int)itemPosition.X - itemSize / 2, (int)itemPosition.Y - itemSize / 2, itemSize, itemSize,
                new Color(255, 199, 112, 255));
        }
    }

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
        if (world.IsMinerTile(neighborPosition))
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
        WorldCamera camera,
        ConveyorDefinition definition,
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
                && wallet.CanAfford(definition.MoneyCost, definition.BuildCost),
            BuildTool.Miner => world.CanPlaceMiner(position, conveyors)
                && wallet.CanAfford(FactoryWorld.MinerMoneyCost,
                    [new ResourceAmount("iron-plate", FactoryWorld.MinerPlateCost)]),
            BuildTool.Remove => world.Miners.ContainsKey(position) || conveyors.Cells.ContainsKey(position)
                || world.IsMinerTile(position),
            _ => false
        };

        var previewColor = valid
            ? new Color(105, 225, 142, 125)
            : new Color(225, 92, 80, 125);
        var previewSize = tool == BuildTool.Miner ? tileSize * MinerBuilding.Size : tileSize;
        Raylib.BeginScissorMode(ViewportLeft, ViewportTop, (int)ViewportWidth, (int)ViewportHeight);
        Raylib.DrawRectangle((int)screen.X + 2, (int)screen.Y + 2, (int)previewSize - 5, (int)previewSize - 5, previewColor);

        if (tool == BuildTool.Conveyor && valid)
        {
            var preview = new ConveyorCell(position, direction, definition);
            DrawConveyor(preview, conveyors, world, screen.X, screen.Y, tileSize, true);
        }
        else if (tool == BuildTool.Miner && valid)
        {
            DrawMiner(new MinerBuilding(position, world.CountCoveredDepositTiles(position)), screen.X, screen.Y, tileSize, true);
        }

        Raylib.EndScissorMode();
    }

    private static void DrawPanel(FactoryWorld world, ConveyorGrid conveyors)
    {
        Raylib.DrawRectangle(ViewportRight, ViewportTop, PanelWidth, (int)ViewportHeight, new Color(24, 29, 29, 255));
        Raylib.DrawText("LOGISTICA", ViewportRight + 22, ViewportTop + 22, 20, new Color(232, 233, 221, 255));
        Raylib.DrawLine(ViewportRight + 22, ViewportTop + 54, ViewportRight + 274, ViewportTop + 54, new Color(62, 72, 68, 255));
        DrawMetric("THROUGHPUT", $"{world.SoldItems * FactoryWorld.IronOreSalePrice} crediti", ViewportRight + 22, ViewportTop + 84,
            new Color(112, 218, 145, 255));
        DrawMetric("MINATORI", world.Miners.Count.ToString(), ViewportRight + 22, ViewportTop + 150,
            new Color(225, 173, 79, 255));
        DrawMetric("NASTRI", conveyors.Cells.Count.ToString(), ViewportRight + 150, ViewportTop + 150,
            new Color(211, 216, 207, 255));
        DrawMetric("ITEM CONSEGNATI", world.SoldItems.ToString(), ViewportRight + 22, ViewportTop + 216,
            new Color(211, 216, 207, 255));

        Raylib.DrawRectangle(ViewportRight + 22, ViewportTop + 305, 252, 1, new Color(62, 72, 68, 255));
        Raylib.DrawCircle(ViewportRight + 31, ViewportTop + 340, 7, new Color(205, 132, 73, 255));
        Raylib.DrawText("FERRO GREZZO", ViewportRight + 48, ViewportTop + 332, 16, new Color(196, 201, 193, 255));
        Raylib.DrawText($"${FactoryWorld.IronOreSalePrice} / item", ViewportRight + 48, ViewportTop + 354, 15,
            new Color(112, 218, 145, 255));
        Raylib.DrawText("Esc / MENU → home", ViewportRight + 22, ViewportTop + 420, 14, new Color(126, 137, 132, 255));
        Raylib.DrawText("(autosave Continua)", ViewportRight + 22, ViewportTop + 442, 14, new Color(126, 137, 132, 255));
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
