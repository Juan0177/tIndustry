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
    private const int GridWidth = 18;
    private const int GridHeight = 11;
    private const int TileSize = 48;
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

    public static void Run(GameContent content, int? maximumFrames = null)
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

        Raylib.SetConfigFlags(ConfigFlags.VSyncHint);
        Raylib.InitWindow(ScreenWidth, ScreenHeight, "tIndustry - Foundry Sector 7429");
        Raylib.SetTargetFPS(60);

        while (!Raylib.WindowShouldClose()
            && (maximumFrames is null || renderedFrames < maximumFrames))
        {
            accumulator += Math.Min(Raylib.GetFrameTime(), 0.1f);
            HandleInput(world, conveyors, wallet, conveyorDefinition, ref tool, ref direction);

            while (accumulator >= FixedStep)
            {
                world.Update(FixedStep, conveyors, wallet, ref nextItemId);
                accumulator -= FixedStep;
            }

            Draw(world, conveyors, wallet, conveyorDefinition, tool, direction);
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
        ref Direction direction)
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
                return;
            }

            var cell = MouseCell(mouse);
            if (cell is not { } position)
            {
                return;
            }

            switch (tool)
            {
                case BuildTool.Conveyor when world.CanPlaceConveyor(position):
                    conveyors.TryPlace(position, direction, definition, wallet);
                    break;
                case BuildTool.Miner:
                    world.TryPlaceMiner(position, direction, conveyors, wallet);
                    break;
                case BuildTool.Remove:
                    if (!world.TryRemoveMiner(position, wallet))
                    {
                        conveyors.TryRemove(position, wallet);
                    }
                    break;
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

                if (world.CoreTiles.Contains(position))
                {
                    DrawCoreTile(position, screenX, screenY);
                }
                else if (world.Miners.TryGetValue(position, out var miner))
                {
                    DrawMiner(miner, screenX, screenY, false);
                }
                else if (conveyors.Cells.TryGetValue(position, out var conveyor))
                {
                    DrawConveyor(conveyor, conveyors, world, screenX, screenY, false);
                }
            }
        }

        var coreOrigin = world.CoreTiles.OrderBy(position => position.X).ThenBy(position => position.Y).First();
        Raylib.DrawText("CORE", GridLeft + coreOrigin.X * TileSize + 28, GridTop + coreOrigin.Y * TileSize + 38,
            18, new Color(211, 237, 218, 255));
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
        Raylib.DrawRectangle(x, y, TileSize - 1, TileSize - 1, color);

        var detail = (position.X * 17 + position.Y * 31) % 19;
        var detailColor = new Color(color.R + 7, color.G + 7, color.B + 6, 150);
        Raylib.DrawRectangle(x + 7 + detail % 17, y + 8 + detail * 2 % 24, 3, 3, detailColor);

        if (tile.Deposit == DepositKind.Iron)
        {
            Raylib.DrawCircle(x + 15, y + 17, 6, new Color(181, 113, 64, 255));
            Raylib.DrawCircle(x + 31, y + 28, 8, new Color(205, 132, 73, 255));
            Raylib.DrawCircle(x + 17, y + 35, 4, new Color(236, 168, 91, 255));
        }
    }

    private static void DrawCoreTile(GridPosition position, int x, int y)
    {
        Raylib.DrawRectangle(x + 2, y + 2, TileSize - 5, TileSize - 5, new Color(45, 91, 66, 255));
        Raylib.DrawRectangleLines(x + 4, y + 4, TileSize - 9, TileSize - 9, new Color(105, 202, 137, 255));
        var pulse = 5f + MathF.Sin((float)Raylib.GetTime() * 3f + position.X) * 1.5f;
        Raylib.DrawCircle(x + TileSize / 2, y + TileSize / 2, pulse, new Color(137, 235, 165, 255));
    }

    private static void DrawMiner(MinerCell miner, int x, int y, bool preview)
    {
        var alpha = preview ? 150 : 255;
        Raylib.DrawRectangle(x + 5, y + 5, 38, 38, new Color(41, 45, 44, alpha));
        Raylib.DrawRectangleLines(x + 7, y + 7, 34, 34, new Color(229, 184, 91, alpha));
        Raylib.DrawCircle(x + 24, y + 22, 9, new Color(117, 126, 121, alpha));
        Raylib.DrawCircleLines(x + 24, y + 22, 9, new Color(224, 226, 214, alpha));
        Raylib.DrawRectangle(x + 8, y + 38, (int)(32 * miner.Progress), 3, new Color(230, 166, 72, alpha));
        DrawDirectionMark(new Vector2(x + 24, y + 24), miner.Direction, alpha);
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
        var connectedDirections = Directions.Where(candidate => IsConnected(conveyor, candidate, conveyors, world));
        Raylib.DrawCircleV(center, 15, new Color(45, 50, 49, alpha));
        foreach (var connectedDirection in connectedDirections)
        {
            var edge = center + DirectionVector(connectedDirection) * 24f;
            Raylib.DrawLineEx(center, edge, 25f, new Color(45, 50, 49, alpha));
            Raylib.DrawLineEx(center, edge, 3f, new Color(126, 139, 133, alpha));
        }

        var output = center + DirectionVector(conveyor.Direction) * 24f;
        Raylib.DrawLineEx(center, output, 25f, new Color(45, 50, 49, alpha));
        Raylib.DrawLineEx(center, output, 3f, new Color(126, 139, 133, alpha));
        DrawDirectionMark(center, conveyor.Direction, alpha);

        foreach (var item in conveyor.Items)
        {
            var vector = DirectionVector(conveyor.Direction);
            var itemPosition = center - vector * 24f + vector * (item.Progress * TileSize);
            Raylib.DrawCircleV(itemPosition, 8, new Color(225, 137, 66, 255));
            Raylib.DrawCircleLines((int)itemPosition.X, (int)itemPosition.Y, 8, new Color(255, 207, 128, 255));
        }
    }

    private static bool IsConnected(
        ConveyorCell conveyor,
        Direction direction,
        ConveyorGrid conveyors,
        FactoryWorld world)
    {
        var neighborPosition = conveyor.Position.Step(direction);
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
        Raylib.DrawRectangle(x + 2, y + 2, TileSize - 5, TileSize - 5, previewColor);

        if (tool == BuildTool.Conveyor && valid)
        {
            var preview = new ConveyorCell(position, direction, definition);
            DrawConveyor(preview, conveyors, world, x, y, true);
        }
        else if (tool == BuildTool.Miner && valid)
        {
            DrawMiner(new MinerCell(position, direction), x, y, true);
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