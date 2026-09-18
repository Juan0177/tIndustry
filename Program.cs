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
    FactoryGameApp.Run(content, args.Contains("--smoke-test") ? 3 : null);
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

    var miningWorld = new FactoryWorld(8, 5, 7429);
    var miningGrid = new ConveyorGrid();
    var miningWallet = new EconomyWallet(100, new Dictionary<string, int>
    {
        ["iron-plate"] = 10
    });
    var nextItemId = 2L;
    Assert(miningWorld.TryPlaceMiner(new GridPosition(2, 2), Direction.East, miningGrid, miningWallet),
        "Il minatore deve poter essere piazzato sul giacimento garantito.");
    Assert(miningGrid.TryPlace(new GridPosition(3, 2), Direction.East, definition, miningWallet),
        "Il nastro deve poter collegare il minatore al core.");
    miningWorld.Update(1f, miningGrid, miningWallet, ref nextItemId);
    miningWorld.Update(1f, miningGrid, miningWallet, ref nextItemId);
    miningWorld.Update(1f, miningGrid, miningWallet, ref nextItemId);
    Assert(miningWorld.SoldItems == 1, "Il core deve incassare il minerale consegnato.");
    Assert(miningWallet.Money == 78, "Il saldo deve includere costruzioni e vendita al core.");

    Console.WriteLine("SELF-TEST OK: costi, trasporto, minatore e vendita al core verificati.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}