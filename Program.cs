using TIndustry.Logistics;

var dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
var jsonContentPath = Path.Combine(dataDirectory, "content.json");
var excelContentPath = Path.Combine(dataDirectory, "game-data.xlsx");

if (args.Contains("--export-excel"))
{
    var argumentIndex = Array.IndexOf(args, "--export-excel");
    var destination = argumentIndex + 1 < args.Length
        ? Path.GetFullPath(args[argumentIndex + 1])
        : Path.Combine(Directory.GetCurrentDirectory(), "data", "game-data.xlsx");
    ExcelContentStore.Save(destination, GameContent.Load(jsonContentPath));
    Console.WriteLine($"Database Excel creato: {destination}");
    return;
}

var content = GameContent.Load(File.Exists(excelContentPath) ? excelContentPath : jsonContentPath);
var basicConveyor = content.Conveyors.Single(definition => definition.Id == "conveyor-basic");

if (args.Contains("--self-test"))
{
    RunSelfTest(basicConveyor);
    return;
}

if (!args.Contains("--console-demo"))
{
    var capture = args.Contains("--capture");
    FactoryGameApp.Run(
        content,
        args.Contains("--smoke-test") || capture ? 3 : null,
        capture ? Path.Combine("artifacts", "game-preview.png") : null);
    return;
}

var grid = CreateTwoCellLine(basicConveyor);
var first = grid.Cells[new GridPosition(0, 0)];
first.TryInsert(new TransportedItem(1, "iron-ore"));

Console.WriteLine("Simulazione nastro A(0,0) -> B(1,0)");
for (var tick = 1; tick <= 24; tick++)
{
    grid.Update(0.1f);
    var location = grid.Cells
        .Single(pair => pair.Value.Items.Any(item => item.Id == 1));
    var item = location.Value.Items.Single(candidate => candidate.Id == 1);
    Console.WriteLine($"tick {tick,2}: cella {location.Key}, progresso {item.Progress:F2}");
}

static ConveyorGrid CreateTwoCellLine(ConveyorDefinition definition)
{
    var grid = new ConveyorGrid();
    var wallet = new EconomyWallet(100, new Dictionary<string, int>
    {
        ["iron-plate"] = 2
    });
    if (!grid.TryPlace(new GridPosition(0, 0), Direction.East, definition, wallet)
        || !grid.TryPlace(new GridPosition(1, 0), Direction.East, definition, wallet))
    {
        throw new InvalidOperationException("Impossibile creare la linea di test.");
    }

    Assert(wallet.Money == 90, "Il costo in denaro dei due nastri deve essere scalato.");
    Assert(wallet.MaterialCount("iron-plate") == 0, "Il costo materiali deve essere scalato.");

    return grid;
}

static void RunSelfTest(ConveyorDefinition definition)
{
    var grid = CreateTwoCellLine(definition);
    var first = grid.Cells[new GridPosition(0, 0)];
    var second = grid.Cells[new GridPosition(1, 0)];
    var item = new TransportedItem(1, "iron-ore");

    Assert(first.TryInsert(item), "L'item deve entrare nella cella A.");
    grid.Update(1f);
    Assert(first.Items.Contains(item), "L'item non deve trasferirsi prima del bordo.");
    Assert(item.Progress == 0.5f, "Dopo un secondo l'item deve trovarsi a metà tile.");
    grid.Update(1f);
    Assert(!first.Items.Contains(item), "L'item deve lasciare la cella A.");
    Assert(second.Items.Contains(item), "L'item deve entrare nella cella B.");
    Assert(item.Progress == 0f, "Il progresso deve ripartire da zero nella cella B.");

    var miningWorld = new FactoryWorld(12, 8, 7429);
    var miningGrid = new ConveyorGrid();
    var miningWallet = new EconomyWallet(100, new Dictionary<string, int>
    {
        ["iron-plate"] = 10
    });
    var nextItemId = 2L;
    Assert(miningWorld.TryPlaceMiner(new GridPosition(2, 2), Direction.East, miningGrid, miningWallet),
        "Il minatore deve poter essere piazzato sul giacimento garantito.");
    Assert(miningGrid.TryPlace(new GridPosition(4, 2), Direction.East, definition, miningWallet),
        "Il nastro deve poter collegare il minatore al core.");
    Assert(miningGrid.TryPlace(new GridPosition(5, 2), Direction.East, definition, miningWallet),
        "Il secondo nastro deve raggiungere il core 4x4.");
    for (var tick = 0; tick < 210; tick++)
    {
        miningWorld.Update(1f / 30f, miningGrid, miningWallet, ref nextItemId);
    }
    Assert(miningWorld.SoldItems == 1, "Il core deve incassare il minerale consegnato.");
    Assert(miningWallet.Money == 73, "Il saldo deve includere costruzioni e vendita al core.");

    var curvedWorld = new FactoryWorld(14, 10, 7429);
    var curvedGrid = new ConveyorGrid();
    var curvedWallet = new EconomyWallet(200, new Dictionary<string, int> { ["iron-plate"] = 20 });
    var curvedItemId = 10L;
    Assert(curvedWorld.TryPlaceMiner(new GridPosition(2, 2), Direction.East, curvedGrid, curvedWallet),
        "Il minatore della linea curva deve essere piazzato.");
    Assert(curvedGrid.TryPlace(new GridPosition(4, 2), Direction.South, definition, curvedWallet),
        "Il primo tratto della curva deve essere piazzato.");
    Assert(curvedGrid.TryPlace(new GridPosition(4, 3), Direction.East, definition, curvedWallet),
        "La curva deve essere piazzata.");
    Assert(curvedGrid.TryPlace(new GridPosition(5, 3), Direction.East, definition, curvedWallet),
        "Il tratto centrale deve essere piazzato.");
    Assert(curvedGrid.TryPlace(new GridPosition(6, 3), Direction.East, definition, curvedWallet),
        "Il tratto verso il core deve essere piazzato.");
    Assert(curvedGrid.TryPlace(new GridPosition(7, 3), Direction.East, definition, curvedWallet),
        "L'ultimo tratto deve toccare il core.");
    for (var tick = 0; tick < 360; tick++)
    {
        curvedWorld.Update(1f / 30f, curvedGrid, curvedWallet, ref curvedItemId);
    }
    Assert(curvedWorld.SoldItems > 0, "Una linea con curva deve consegnare minerale al core.");

    var partialWorld = new FactoryWorld(12, 8, 7429);
    var partialGrid = new ConveyorGrid();
    var partialWallet = new EconomyWallet(100, new Dictionary<string, int> { ["iron-plate"] = 10 });
    var partialItemId = 100L;
    Assert(partialWorld.CountCoveredDepositTiles(new GridPosition(0, 0)) == 1,
        "Il giacimento parziale deve coprire una sola tile del footprint.");
    Assert(partialWorld.TryPlaceMiner(new GridPosition(0, 0), Direction.East, partialGrid, partialWallet),
        "Il minatore deve poter essere piazzato anche con una sola tile mineraria.");
    var partialMiner = partialWorld.Miners[new GridPosition(0, 0)];
    Assert(partialMiner.Efficiency == 0.25f, "Una tile mineraria su quattro deve dare efficienza 25%.");
    Assert(partialGrid.TryPlace(new GridPosition(2, 0), Direction.East, definition, partialWallet),
        "Il nastro deve poter ricevere dal minatore parziale.");
    for (var tick = 0; tick < 120; tick++)
    {
        partialWorld.Update(1f / 30f, partialGrid, partialWallet, ref partialItemId);
    }
    Assert(partialMiner.Progress is > 0.49f and < 0.51f,
        "Al 25% il minatore deve completare metà ciclo in quattro secondi.");

    var largeWorld = new FactoryWorld(FactoryGameApp.MapWidth, FactoryGameApp.MapHeight, 7429);
    Assert(largeWorld.Terrain.Width == 1000 && largeWorld.Terrain.Height == 1000,
        "La mappa di gioco deve essere 1000×1000.");
    Assert(largeWorld.CoreTiles.Count == 16, "Il core 4×4 deve esistere sulla mappa grande.");
    Assert(largeWorld.Terrain[largeWorld.StarterDepositOrigin].Deposit == DepositKind.Iron,
        "Il giacimento iniziale deve stare vicino al core.");

    var camera = new WorldCamera(0, 0, 1f);
    camera.CenterOnTile(largeWorld.CoreOrigin, 36, 944, 628);
    camera.GetVisibleTileRange(944, 628, 1000, 1000, 36, out var minX, out var minY, out var maxX, out var maxY);
    Assert(maxX - minX < 80 && maxY - minY < 60,
        "Il culling camera deve limitare i tile visibili rispetto all'intera mappa.");
    camera.ZoomAt(400, 300, 0, 132, 2f);
    Assert(camera.Zoom == 2f, "Lo zoom deve rispettare il fattore richiesto entro i limiti.");
    camera.SetZoom(0.1f);
    Assert(camera.Zoom == WorldCamera.MinZoom, "Lo zoom minimo deve essere clampato.");
    camera.SetZoom(9f);
    Assert(camera.Zoom == WorldCamera.MaxZoom, "Lo zoom massimo deve essere clampato.");

    var saveWorld = new FactoryWorld(24, 16, 9001);
    var saveGrid = new ConveyorGrid();
    var saveWallet = new EconomyWallet(150, new Dictionary<string, int> { ["iron-plate"] = 20, ["copper-wire"] = 3 });
    var saveItemId = 7L;
    Assert(saveWorld.TryPlaceMiner(saveWorld.StarterDepositOrigin, Direction.East, saveGrid, saveWallet),
        "Il minatore di save-test deve piazzarsi sul giacimento starter.");
    var beltX = saveWorld.StarterDepositOrigin.X + MinerBuilding.Size;
    var beltY = saveWorld.StarterDepositOrigin.Y;
    Assert(saveGrid.TryPlace(new GridPosition(beltX, beltY), Direction.East, definition, saveWallet),
        "Il nastro di save-test deve piazzarsi.");
    saveWorld.Update(1f / 30f, saveGrid, saveWallet, ref saveItemId);
    var saveCamera = new WorldCamera(12.5f, 34f, 1.25f);
    var captured = GameSaveStore.Capture(saveWorld, saveGrid, saveWallet, saveCamera, saveItemId);
    var slotId = "self-test-slot";
    GameSaveStore.Save(slotId, captured);
    Assert(GameSaveStore.Exists(slotId), "Il file di salvataggio deve esistere dopo Save.");
    var restoredBundle = GameSaveStore.Restore(GameSaveStore.Load(slotId), new GameContent
    {
        Conveyors = [definition],
        Recipes = []
    });
    Assert(restoredBundle.World.Seed == 9001, "Il seed deve essere ripristinato.");
    Assert(restoredBundle.Wallet.Money == saveWallet.Money, "Il wallet denaro deve essere ripristinato.");
    Assert(restoredBundle.Wallet.MaterialCount("iron-plate") == saveWallet.MaterialCount("iron-plate"),
        "Il wallet materiali deve essere ripristinato.");
    Assert(restoredBundle.World.Miners.Count == 1, "I minatori devono essere ripristinati.");
    Assert(restoredBundle.Conveyors.Cells.Count == 1, "I nastri devono essere ripristinati.");
    Assert(Math.Abs(restoredBundle.Camera.X - 12.5f) < 0.01f && Math.Abs(restoredBundle.Camera.Zoom - 1.25f) < 0.01f,
        "La camera deve essere ripristinata.");
    GameSaveStore.Delete(slotId);
    Assert(!GameSaveStore.Exists(slotId), "Delete deve rimuovere lo slot.");

    Console.WriteLine("SELF-TEST OK: trasporto, core, minatori, camera 1000×1000 e save/load verificati.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}