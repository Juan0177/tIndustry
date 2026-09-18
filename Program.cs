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

    Console.WriteLine("SELF-TEST OK: trasporto, core e minatori a efficienza variabile verificati.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}