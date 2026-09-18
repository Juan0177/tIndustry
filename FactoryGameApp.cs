using System.Numerics;
using Raylib_cs;
using TIndustry.Logistics;

internal enum BuildTool
{
    Conveyor,
    Miner,
    Remove
}

internal static class FactoryGameApp
{
    private const int ScreenWidth = 1240;
    private const int ScreenHeight = 760;
    private const int GridWidth = 24;
    private const int GridHeight = 15;
    private const int TileSize = 36;
    private const int GridLeft = 28;
    private const int GridTop = 152;
    private const int PanelLeft = 916;
    private const float FixedStep = 1f / 30f;

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
        var conveyors = new ConveyorGrid();
        var world = new FactoryWorld(GridWidth, GridHeight, 7429);
        var wallet = new EconomyWallet(180, new Dictionary<string, int>
        {
            ["iron-plate"] = 48,
            ["copper-wire"] = 10
        });
        var tool = BuildTool.Conveyor;
        var direction = Direction.East;
        var accumulator = 0f;
        var nextItemId = 1L;
        var renderedFrames = 0;
        GridPosition? previousDragPosition = null;

        Raylib.SetConfigFlags(ConfigFlags.VSyncHint);
        Raylib.InitWindow(ScreenWidth, ScreenHeight, "tIndustry - Foundry Sector 7429");
        Raylib.SetTargetFPS(60);

        while (!Raylib.WindowShouldClose()
            && (maximumFrames is null || renderedFrames < maximumFrames))
        {
            accumulator += Math.Min(Raylib.GetFrameTime(), 0.1f);
            HandleInput(world, conveyors, wallet, conveyorDefinition, ref tool, ref direction,
                ref previousDragPosition);

            while (accumulator >= FixedStep)
            {
                world.Update(FixedStep, conveyors, wallet, ref nextItemId);
                accumulator -= FixedStep;
            }

            Draw(world, conveyors, wallet, conveyorDefinition, tool, direction);
            if (screenshotPath is not null && renderedFrames == 1)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);
                Raylib.TakeScreenshot(screenshotPath);
            }
            renderedFrames++;
        }

        Raylib.CloseWindow();
    }

    private static void HandleInput(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        ConveyorDefinition definition,
        ref BuildTool tool,
        ref Direction direction,
        ref GridPosition? previousDragPosition)
    {
        var wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0)
        {
            var offset = wheel > 0 ? 1 : 3;
            direction = (Direction)(((int)direction + offset) % 4);
        }

        if (Raylib.IsKeyPressed(KeyboardKey.R))
        {
            direction = (Direction)(((int)direction + 1) % 4);
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

        if (tool == BuildTool.Conveyor && Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            var cell = MouseCell(mouse);
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
            var cell = MouseCell(mouse);
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
            var cell = MouseCell(mouse);
            if (cell is { } position && !world.TryRemoveMiner(position, wallet))
            {
                conveyors.TryRemove(position, wallet);
            }
        }
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

    private static void Draw(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        ConveyorDefinition definition,
        BuildTool tool,
        Direction direction)
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(14, 18, 18, 255));
        DrawHeader(wallet, definition, tool, direction);
        DrawWorld(world, conveyors);
        DrawPreview(world, conveyors, wallet, definition, tool, direction);
        DrawPanel(world, conveyors);
        Raylib.EndDrawing();
    }

    private static void DrawHeader(
        EconomyWallet wallet,
        ConveyorDefinition definition,
        BuildTool tool,
        Direction direction)
    {
        Raylib.DrawText("tINDUSTRY", 28, 22, 30, new Color(239, 238, 224, 255));
        Raylib.DrawText("FOUNDRY SECTOR 7429", 28, 56, 14, new Color(112, 124, 119, 255));
        Raylib.DrawText($"$ {wallet.Money}", 1010, 25, 24, new Color(112, 218, 145, 255));
        Raylib.DrawText($"PIASTRE  {wallet.MaterialCount("iron-plate")}", 1010, 57, 17, new Color(220, 179, 93, 255));

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
    }

    private static void DrawWorld(FactoryWorld world, ConveyorGrid conveyors)
    {
        for (var y = 0; y < GridHeight; y++)
        {
            for (var x = 0; x < GridWidth; x++)
            {
                var position = new GridPosition(x, y);
                var screenX = GridLeft + x * TileSize;
                var screenY = GridTop + y * TileSize;
                DrawTerrainTile(world.Terrain[position], position, screenX, screenY);
            }
        }

        foreach (var conveyor in conveyors.Cells.Values)
        {
            DrawConveyor(
                conveyor,
                conveyors,
                world,
                GridLeft + conveyor.Position.X * TileSize,
                GridTop + conveyor.Position.Y * TileSize,
                false);
        }

        DrawCore(world);
        foreach (var miner in world.Miners.Values)
        {
            DrawMiner(
                miner,
                GridLeft + miner.Position.X * TileSize,
                GridTop + miner.Position.Y * TileSize,
                false);
        }
    }

    private static void DrawTerrainTile(TerrainTile tile, GridPosition position, int x, int y)
    {
        var color = tile.Terrain switch
        {
            TerrainKind.Grass => new Color(51, 73, 56, 255),
            TerrainKind.Soil => new Color(81, 70, 52, 255),
            TerrainKind.Stone => new Color(69, 74, 72, 255),
            TerrainKind.Water => new Color(35, 77, 91, 255),
            _ => Color.Black
        };
        Raylib.DrawRectangle(x, y, TileSize, TileSize, color);
        Raylib.DrawRectangleLines(x, y, TileSize, TileSize, new Color(20, 27, 26, 38));

        var detail = (position.X * 17 + position.Y * 31) % 19;
        var detailColor = new Color(color.R + 7, color.G + 7, color.B + 6, 150);
        Raylib.DrawRectangle(x + 5 + detail % 14, y + 6 + detail * 2 % 19, 3, 3, detailColor);

        if (tile.Deposit == DepositKind.Iron)
        {
            Raylib.DrawCircle(x + 10, y + 12, 4, new Color(151, 88, 51, 255));
            Raylib.DrawCircle(x + 24, y + 21, 6, new Color(205, 132, 73, 255));
            Raylib.DrawCircle(x + 12, y + 27, 3, new Color(236, 168, 91, 255));
        }
    }

    private static void DrawCore(FactoryWorld world)
    {
        var left = world.CoreTiles.Min(position => position.X);
        var top = world.CoreTiles.Min(position => position.Y);
        var x = GridLeft + left * TileSize;
        var y = GridTop + top * TileSize;
        var size = TileSize * 4;
        Raylib.DrawRectangle(x + 5, y + 7, size, size, new Color(11, 16, 15, 145));
        Raylib.DrawRectangle(x + 2, y + 2, size - 4, size - 4, new Color(35, 60, 48, 255));
        Raylib.DrawRectangleLines(x + 5, y + 5, size - 10, size - 10, new Color(91, 184, 121, 255));
        Raylib.DrawRectangle(x + 17, y + 17, size - 34, size - 34, new Color(27, 39, 35, 255));
        Raylib.DrawRectangleLines(x + 20, y + 20, size - 40, size - 40, new Color(65, 109, 82, 255));

        var pulse = 23f + MathF.Sin((float)Raylib.GetTime() * 3f) * 3f;
        var center = new Vector2(x + size / 2f, y + size / 2f);
        Raylib.DrawCircleV(center, pulse + 8, new Color(44, 104, 68, 255));
        Raylib.DrawCircleV(center, pulse, new Color(103, 225, 139, 255));
        Raylib.DrawCircleV(center, 12, new Color(210, 251, 218, 255));
        Raylib.DrawText("CORE", x + 48, y + size - 34, 18, new Color(201, 232, 207, 255));
    }

    private static void DrawMiner(MinerBuilding miner, int x, int y, bool preview)
    {
        var alpha = preview ? 150 : 255;
        var size = TileSize * MinerBuilding.Size;
        Raylib.DrawRectangle(x + 4, y + 6, size - 4, size - 4, new Color(16, 20, 19, alpha));
        Raylib.DrawRectangle(x + 2, y + 2, size - 4, size - 4, new Color(49, 53, 51, alpha));
        Raylib.DrawRectangleLines(x + 5, y + 5, size - 10, size - 10, new Color(222, 168, 76, alpha));
        Raylib.DrawRectangle(x + 12, y + 12, size - 24, size - 24, new Color(30, 34, 33, alpha));

        var center = new Vector2(x + size / 2f, y + size / 2f - 3);
        var angle = (float)Raylib.GetTime() * 90f;
        Raylib.DrawPoly(center, 8, 18, angle, new Color(116, 125, 120, alpha));
        Raylib.DrawPolyLinesEx(center, 8, 18, angle, 3, new Color(225, 216, 186, alpha));
        Raylib.DrawCircleV(center, 7, new Color(210, 143, 68, alpha));
        Raylib.DrawRectangle(x + 9, y + size - 10, size - 18, 4, new Color(25, 29, 28, alpha));
        Raylib.DrawRectangle(x + 9, y + size - 10, (int)((size - 18) * miner.Progress), 4,
            new Color(231, 166, 66, alpha));
        var efficiencyLabel = $"{miner.Efficiency:P0}";
        var efficiencyWidth = Raylib.MeasureText(efficiencyLabel, 12);
        Raylib.DrawRectangle(x + (size - efficiencyWidth) / 2 - 4, y + size - 27,
            efficiencyWidth + 8, 16, new Color(20, 24, 23, alpha));
        Raylib.DrawText(efficiencyLabel, x + (size - efficiencyWidth) / 2, y + size - 25,
            12, new Color(233, 190, 96, alpha));
    }

    private static void DrawConveyor(
        ConveyorCell conveyor,
        ConveyorGrid conveyors,
        FactoryWorld world,
        int x,
        int y,
        bool preview)
    {
        var alpha = preview ? 145 : 255;
        var center = new Vector2(x + TileSize / 2f, y + TileSize / 2f);
        Raylib.DrawRectangle(x + 8, y + 8, TileSize - 16, TileSize - 16, new Color(38, 43, 42, alpha));
        foreach (var connectedDirection in Directions)
        {
            if (IsConnected(conveyor, connectedDirection, conveyors, world))
            {
                DrawConveyorArm(center, connectedDirection, alpha);
            }
        }

        DrawConveyorArm(center, conveyor.Direction, alpha);
        Raylib.DrawRectangle(x + 9, y + 9, TileSize - 18, TileSize - 18, new Color(70, 77, 74, alpha));
        DrawDirectionMark(center, conveyor.Direction, alpha);

        foreach (var item in conveyor.Items)
        {
            var vector = DirectionVector(conveyor.Direction);
            var itemPosition = center - vector * 18f + vector * (item.Progress * TileSize);
            Raylib.DrawRectangle((int)itemPosition.X - 5, (int)itemPosition.Y - 5, 10, 10,
                new Color(211, 117, 55, 255));
            Raylib.DrawRectangleLines((int)itemPosition.X - 5, (int)itemPosition.Y - 5, 10, 10,
                new Color(255, 199, 112, 255));
        }
    }

    private static void DrawConveyorArm(Vector2 center, Direction direction, int alpha)
    {
        var vector = DirectionVector(direction);
        var edge = center + vector * (TileSize / 2f);
        var horizontal = direction is Direction.East or Direction.West;
        var left = (int)Math.Min(center.X, edge.X) - (horizontal ? 0 : 9);
        var top = (int)Math.Min(center.Y, edge.Y) - (horizontal ? 9 : 0);
        var width = horizontal ? (int)Math.Abs(edge.X - center.X) + 1 : 18;
        var height = horizontal ? 18 : (int)Math.Abs(edge.Y - center.Y) + 1;
        Raylib.DrawRectangle(left, top, width, height, new Color(38, 43, 42, alpha));

        var innerLeft = horizontal ? left : left + 3;
        var innerTop = horizontal ? top + 3 : top;
        var innerWidth = horizontal ? width : width - 6;
        var innerHeight = horizontal ? height - 6 : height;
        Raylib.DrawRectangle(innerLeft, innerTop, innerWidth, innerHeight, new Color(84, 92, 88, alpha));

        var phase = (float)(Raylib.GetTime() * 10 % 9);
        for (var offset = -12f + phase; offset <= 12f; offset += 9f)
        {
            var mark = center + vector * offset;
            var side = new Vector2(-vector.Y, vector.X) * 5f;
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

    private static void DrawDirectionMark(Vector2 center, Direction direction, int alpha)
    {
        var vector = DirectionVector(direction);
        var side = new Vector2(-vector.Y, vector.X);
        var point = center + vector * 12f;
        Raylib.DrawTriangle(
            point + vector * 5f,
            point - vector * 5f + side * 5f,
            point - vector * 5f - side * 5f,
            new Color(224, 207, 142, alpha));
    }

    private static void DrawPreview(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        ConveyorDefinition definition,
        BuildTool tool,
        Direction direction)
    {
        var cell = MouseCell(Raylib.GetMousePosition());
        if (cell is not { } position)
        {
            return;
        }

        var x = GridLeft + position.X * TileSize;
        var y = GridTop + position.Y * TileSize;
        var valid = tool switch
        {
            BuildTool.Conveyor => world.CanPlaceConveyor(position)
                && !conveyors.Cells.ContainsKey(position)
                && wallet.CanAfford(definition.MoneyCost, definition.BuildCost),
            BuildTool.Miner => world.CanPlaceMiner(position, conveyors)
                && wallet.CanAfford(FactoryWorld.MinerMoneyCost,
                    [new ResourceAmount("iron-plate", FactoryWorld.MinerPlateCost)]),
            BuildTool.Remove => world.Miners.ContainsKey(position) || conveyors.Cells.ContainsKey(position),
            _ => false
        };

        var previewColor = valid
            ? new Color(105, 225, 142, 125)
            : new Color(225, 92, 80, 125);
        var previewSize = tool == BuildTool.Miner ? TileSize * MinerBuilding.Size : TileSize;
        Raylib.DrawRectangle(x + 2, y + 2, previewSize - 5, previewSize - 5, previewColor);

        if (tool == BuildTool.Conveyor && valid)
        {
            var preview = new ConveyorCell(position, direction, definition);
            DrawConveyor(preview, conveyors, world, x, y, true);
        }
        else if (tool == BuildTool.Miner && valid)
        {
            DrawMiner(new MinerBuilding(position, world.CountCoveredDepositTiles(position)), x, y, true);
        }
    }

    private static void DrawPanel(FactoryWorld world, ConveyorGrid conveyors)
    {
        Raylib.DrawRectangle(PanelLeft, GridTop, 296, GridHeight * TileSize, new Color(24, 29, 29, 255));
        Raylib.DrawText("LOGISTICS", PanelLeft + 22, GridTop + 22, 20, new Color(232, 233, 221, 255));
        Raylib.DrawLine(PanelLeft + 22, GridTop + 54, PanelLeft + 274, GridTop + 54, new Color(62, 72, 68, 255));
        DrawMetric("THROUGHPUT", $"{world.SoldItems * FactoryWorld.IronOreSalePrice} crediti", PanelLeft + 22, GridTop + 84,
            new Color(112, 218, 145, 255));
        DrawMetric("MINATORI", world.Miners.Count.ToString(), PanelLeft + 22, GridTop + 150,
            new Color(225, 173, 79, 255));
        DrawMetric("NASTRI", conveyors.Cells.Count.ToString(), PanelLeft + 150, GridTop + 150,
            new Color(211, 216, 207, 255));
        DrawMetric("ITEM CONSEGNATI", world.SoldItems.ToString(), PanelLeft + 22, GridTop + 216,
            new Color(211, 216, 207, 255));

        Raylib.DrawRectangle(PanelLeft + 22, GridTop + 305, 252, 1, new Color(62, 72, 68, 255));
        Raylib.DrawCircle(PanelLeft + 31, GridTop + 340, 7, new Color(205, 132, 73, 255));
        Raylib.DrawText("FERRO GREZZO", PanelLeft + 48, GridTop + 332, 16, new Color(196, 201, 193, 255));
        Raylib.DrawText($"${FactoryWorld.IronOreSalePrice} / item", PanelLeft + 48, GridTop + 354, 15,
            new Color(112, 218, 145, 255));
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

    private static GridPosition? MouseCell(Vector2 mouse)
    {
        if (mouse.X < GridLeft
            || mouse.X >= GridLeft + GridWidth * TileSize
            || mouse.Y < GridTop
            || mouse.Y >= GridTop + GridHeight * TileSize)
        {
            return null;
        }

        return new GridPosition(
            (int)((mouse.X - GridLeft) / TileSize),
            (int)((mouse.Y - GridTop) / TileSize));
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