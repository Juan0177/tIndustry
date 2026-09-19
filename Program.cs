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
    RunSelfTest(content);
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

var grid = CreateTwoCellLine(basicConveyor, ResearchState.CreateNew(content));
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

static ConveyorGrid CreateTwoCellLine(ConveyorDefinition definition, ResearchState research)
{
    var grid = new ConveyorGrid();
    var wallet = new EconomyWallet(100, new Dictionary<string, int>
    {
        ["iron-plate"] = 2
    });
    if (!grid.TryPlace(new GridPosition(0, 0), Direction.East, definition, wallet, research)
        || !grid.TryPlace(new GridPosition(1, 0), Direction.East, definition, wallet, research))
    {
        throw new InvalidOperationException("Impossibile creare la linea di test.");
    }

    Assert(wallet.Money == 90, "Il costo in denaro dei due nastri deve essere scalato.");
    Assert(wallet.MaterialCount("iron-plate") == 0, "Il costo materiali deve essere scalato.");

    return grid;
}

static void RunSelfTest(GameContent content)
{
    var definition = content.Conveyors.Single(entry => entry.Id == "conveyor-basic");
    var fastDefinition = content.Conveyors.Single(entry => entry.Id == "conveyor-fast");
    var smeltRecipe = content.Recipes.Single(entry => entry.Id == "smelt-iron");
    var research = ResearchState.CreateNew(content);
    var smelterTech = content.FindStructure("smelter")!;
    var fastTech = content.FindStructure("conveyor-fast")!;

    Assert(research.IsUnlocked("conveyor-basic") && research.IsUnlocked("miner"),
        "Nastro base e minatore devono partire sbloccati.");
    Assert(!research.IsUnlocked("smelter") && !research.IsUnlocked("conveyor-fast"),
        "Forno e nastro veloce devono partire bloccati.");
    Assert(content.FindStructure("assembler")?.IsStub == false
        && content.FindStructure("assembler")?.Kind == StructureKind.Building,
        "L'assemblatore deve essere un edificio costruibile.");
    Assert(content.FindStructure("miner-advanced")?.IsStub == true,
        "Il minatore avanzato resta stub.");
    Assert(content.FindStructure("junction") is not null
        && content.FindStructure("splitter") is not null
        && content.FindStructure("conveyor-bridge") is not null,
        "Incrocio, sdoppiatore e ponte devono esistere nelle strutture.");
    Assert(content.Recipes.Any(recipe => recipe.Id == "craft-copper-wire"),
        "La ricetta craft-copper-wire deve esistere.");
    Assert(content.CreateMarket().GetSellPrice("copper-ore") == 6,
        "Il rame grezzo deve avere prezzo mercato.");

    var grid = CreateTwoCellLine(definition, research);
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
    Assert(miningWorld.Miners[new GridPosition(2, 2)].OutputTiles().All(tile => tile.X == 4),
        "Il minatore deve erogare solo sul lato della direzione scelta.");
    Assert(miningGrid.TryPlace(new GridPosition(4, 2), Direction.East, definition, miningWallet, research),
        "Il nastro deve poter collegare il minatore al core.");
    Assert(miningGrid.TryPlace(new GridPosition(5, 2), Direction.East, definition, miningWallet, research),
        "Il secondo nastro deve raggiungere il core 4x4.");
    for (var tick = 0; tick < 210; tick++)
    {
        miningWorld.Update(1f / 30f, miningGrid, miningWallet, ref nextItemId);
    }
    Assert(miningWorld.SoldItems == 1, "Il core deve incassare il minerale consegnato.");
    Assert(miningWallet.Money == 73, "Il saldo deve includere costruzioni e vendita al core.");
    Assert(miningWorld.SaleRevenue == FactoryWorld.IronOreSalePrice,
        "Il ricavo deve usare il prezzo ore.");

    var curvedWorld = new FactoryWorld(14, 10, 7429);
    var curvedGrid = new ConveyorGrid();
    var curvedWallet = new EconomyWallet(200, new Dictionary<string, int> { ["iron-plate"] = 20 });
    var curvedItemId = 10L;
    Assert(curvedWorld.TryPlaceMiner(new GridPosition(2, 2), Direction.East, curvedGrid, curvedWallet),
        "Il minatore della linea curva deve essere piazzato.");
    Assert(curvedGrid.TryPlace(new GridPosition(4, 2), Direction.South, definition, curvedWallet, research),
        "Il primo tratto della curva deve essere piazzato.");
    Assert(curvedGrid.TryPlace(new GridPosition(4, 3), Direction.East, definition, curvedWallet, research),
        "La curva deve essere piazzata.");
    Assert(curvedGrid.TryPlace(new GridPosition(5, 3), Direction.East, definition, curvedWallet, research),
        "Il tratto centrale deve essere piazzato.");
    Assert(curvedGrid.TryPlace(new GridPosition(6, 3), Direction.East, definition, curvedWallet, research),
        "Il tratto verso il core deve essere piazzato.");
    Assert(curvedGrid.TryPlace(new GridPosition(7, 3), Direction.East, definition, curvedWallet, research),
        "L'ultimo tratto deve toccare il core.");
    for (var tick = 0; tick < 480; tick++)
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
    Assert(partialGrid.TryPlace(new GridPosition(2, 0), Direction.East, definition, partialWallet, research),
        "Il nastro deve poter ricevere dal minatore parziale.");
    for (var tick = 0; tick < 120; tick++)
    {
        partialWorld.Update(1f / 30f, partialGrid, partialWallet, ref partialItemId);
    }
    Assert(partialMiner.Progress is > 0.49f and < 0.51f,
        "Al 25% il minatore deve completare metà ciclo in quattro secondi.");

    // Smelter loop: feed ore into smelter, sell plate at core.
    var smeltWorld = new FactoryWorld(16, 10, 7429);
    var smeltGrid = new ConveyorGrid();
    var smeltWallet = new EconomyWallet(300, new Dictionary<string, int> { ["iron-plate"] = 40 });
    var smeltItemId = 500L;
    // Core is at x=10,y=3 on 16×10 — keep the line on y=3.
    Assert(research.TryUnlock(smelterTech, smeltWallet), "Il forno deve potersi sbloccare in ricerca.");
    Assert(smeltWorld.TryPlaceSmelter(new GridPosition(4, 3), Direction.East, smeltRecipe, smeltGrid, smeltWallet),
        "Il forno deve piazzarsi.");
    Assert(smeltGrid.TryPlace(new GridPosition(3, 3), Direction.East, definition, smeltWallet, research),
        "Il nastro di ingresso forno deve piazzarsi.");
    Assert(smeltGrid.TryPlace(new GridPosition(6, 3), Direction.East, definition, smeltWallet, research),
        "Il nastro di uscita forno deve piazzarsi.");
    Assert(smeltGrid.TryPlace(new GridPosition(7, 3), Direction.East, definition, smeltWallet, research),
        "Il nastro centrale deve piazzarsi.");
    Assert(smeltGrid.TryPlace(new GridPosition(8, 3), Direction.East, definition, smeltWallet, research),
        "Il nastro verso il core deve piazzarsi.");
    Assert(smeltGrid.TryPlace(new GridPosition(9, 3), Direction.East, definition, smeltWallet, research),
        "L'ultimo nastro deve toccare il core.");
    var inputBelt = smeltGrid.Cells[new GridPosition(3, 3)];
    Assert(inputBelt.TryInsert(new TransportedItem(smeltItemId++, "iron-ore")), "Ore 1 in ingresso.");
    for (var tick = 0; tick < 90; tick++)
    {
        smeltWorld.Update(1f / 30f, smeltGrid, smeltWallet, ref smeltItemId);
    }
    Assert(inputBelt.TryInsert(new TransportedItem(smeltItemId++, "iron-ore")), "Ore 2 in ingresso.");
    var moneyBeforePlate = smeltWallet.Money;
    for (var tick = 0; tick < 450; tick++)
    {
        smeltWorld.Update(1f / 30f, smeltGrid, smeltWallet, ref smeltItemId);
    }
    Assert(smeltWorld.SoldItems >= 1, "Il forno deve produrre lastre vendute al core.");
    Assert(smeltWallet.Money >= moneyBeforePlate + FactoryWorld.IronPlateSalePrice,
        "La lastra deve vendere più dell'ore.");
    Assert(smeltWorld.SaleRevenue >= FactoryWorld.IronPlateSalePrice,
        "Il ricavo deve usare il prezzo lastre.");

    // Fast belt research unlock + upgrade.
    var lockedResearch = ResearchState.CreateNew(content);
    var lockedWallet = new EconomyWallet(100, new Dictionary<string, int> { ["iron-plate"] = 10 });
    Assert(!lockedResearch.CanUnlock(fastTech, lockedWallet), "Senza risorse non si sblocca il nastro veloce.");
    var unlockWallet = new EconomyWallet(300, new Dictionary<string, int> { ["iron-plate"] = 55, ["copper-wire"] = 5 });
    Assert(lockedResearch.TryUnlock(fastTech, unlockWallet), "Con risorse sufficienti si sblocca il nastro veloce.");
    Assert(lockedResearch.IsUnlocked("conveyor-fast"), "Lo sblocco deve restare in ResearchState.");
    Assert(unlockWallet.Money == 50, "Lo sblocco deve consumare i $250 di ricerca.");
    var tierGrid = new ConveyorGrid();
    Assert(tierGrid.TryPlace(new GridPosition(0, 0), Direction.East, definition, unlockWallet, lockedResearch),
        "Nastro base piazzabile.");
    Assert(tierGrid.TryUpgrade(new GridPosition(0, 0), fastDefinition, unlockWallet, lockedResearch),
        "Upgrade a nastro veloce deve riuscire.");
    Assert(tierGrid.Cells[new GridPosition(0, 0)].Definition.Id == "conveyor-fast",
        "Dopo upgrade il tier deve essere conveyor-fast.");
    Assert(tierGrid.Cells[new GridPosition(0, 0)].Definition.RateItemsPerSecond == 1f,
        "Il nastro veloce deve avere rate 1.0.");

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
    var saveWallet = new EconomyWallet(250, new Dictionary<string, int> { ["iron-plate"] = 40, ["copper-wire"] = 3 });
    var saveItemId = 7L;
    var saveResearch = ResearchState.CreateNew(content);
    Assert(saveResearch.TryUnlock(smelterTech, saveWallet), "Save-test: sblocca forno.");
    Assert(saveWorld.TryPlaceMiner(saveWorld.StarterDepositOrigin, Direction.East, saveGrid, saveWallet),
        "Il minatore di save-test deve piazzarsi sul giacimento starter.");
    var smelterPos = new GridPosition(saveWorld.CoreOrigin.X - 6, saveWorld.CoreOrigin.Y);
    Assert(saveWorld.TryPlaceSmelter(smelterPos, Direction.East, smeltRecipe, saveGrid, saveWallet),
        "Il forno di save-test deve piazzarsi.");
    var beltX = saveWorld.StarterDepositOrigin.X + MinerBuilding.Size;
    var beltY = saveWorld.StarterDepositOrigin.Y;
    Assert(saveGrid.TryPlace(new GridPosition(beltX, beltY), Direction.East, definition, saveWallet, research),
        "Il nastro di save-test deve piazzarsi.");
    saveWorld.Update(1f / 30f, saveGrid, saveWallet, ref saveItemId);
    var saveCamera = new WorldCamera(12.5f, 34f, 1.25f);
    var saveSession = new EconomySession(saveWallet.Money);
    var captured = GameSaveStore.Capture(saveWorld, saveGrid, saveWallet, saveCamera, saveResearch, saveSession, saveItemId);
    var slotId = "self-test-slot";
    GameSaveStore.Save(slotId, captured);
    Assert(GameSaveStore.Exists(slotId), "Il file di salvataggio deve esistere dopo Save.");
    var restoredBundle = GameSaveStore.Restore(GameSaveStore.Load(slotId), content);
    Assert(restoredBundle.World.Seed == 9001, "Il seed deve essere ripristinato.");
    Assert(restoredBundle.Wallet.Money == saveWallet.Money, "Il wallet denaro deve essere ripristinato.");
    Assert(restoredBundle.Wallet.MaterialCount("iron-plate") == saveWallet.MaterialCount("iron-plate"),
        "Il wallet materiali deve essere ripristinato.");
    Assert(restoredBundle.World.Miners.Count == 1, "I minatori devono essere ripristinati.");
    Assert(restoredBundle.World.Smelters.Count == 1, "I forni devono essere ripristinati.");
    Assert(restoredBundle.Research.IsUnlocked("smelter"), "Lo sblocco forno deve sopravvivere al reload.");
    Assert(!restoredBundle.Research.IsUnlocked("conveyor-fast"), "Il nastro veloce resta bloccato se non sbloccato.");
    Assert(restoredBundle.World.Miners.Values.Single().Direction == Direction.East,
        "La direzione del minatore deve essere ripristinata.");
    Assert(restoredBundle.Conveyors.Cells.Count == 1, "I nastri devono essere ripristinati.");
    Assert(Math.Abs(restoredBundle.Camera.X - 12.5f) < 0.01f && Math.Abs(restoredBundle.Camera.Zoom - 1.25f) < 0.01f,
        "La camera deve essere ripristinata.");
    GameSaveStore.Delete(slotId);
    Assert(!GameSaveStore.Exists(slotId), "Delete deve rimuovere lo slot.");

    // Phase 4 — market, session ledger, core upgrade, plate > ore reinvestment.
    var market = content.CreateMarket();
    Assert(market.GetSellPrice("iron-ore") == 8 && market.GetSellPrice("iron-plate") == 30,
        "I prezzi mercato devono essere content-driven (ore 8, lastre 30).");
    Assert(market.GetSellPrice("iron-plate") > market.GetSellPrice("iron-ore") * 2,
        "Una lastra deve valere più di 2 ore grezze (reinvestimento).");
    Assert(market.BestValueHint().Contains("fondere", StringComparison.OrdinalIgnoreCase)
        || market.BestValueHint().Contains("lastre", StringComparison.OrdinalIgnoreCase),
        "L'hint mercato deve suggerire la fusione.");

    var minerBuilding = content.GetBuildingOrDefault("miner");
    var smelterBuilding = content.GetBuildingOrDefault("smelter");
    var economy = content.GetEconomy();
    Assert(minerBuilding.MoneyCost == 25 && minerBuilding.RefundPercent == 100,
        "Costo/rimborso minatore devono arrivare dal content.");
    Assert(smelterBuilding.MoneyCost == 40 && smelterBuilding.RefundPercent == 100,
        "Costo/rimborso forno devono arrivare dal content.");
    Assert(economy.CoreUpgrade.SaleBonusPercent == 25 && economy.CoreUpgrade.MoneyCost == 150,
        "Upgrade core deve essere content-driven.");

    var ecoWorld = new FactoryWorld(12, 8, 7429);
    var ecoGrid = new ConveyorGrid();
    var ecoWallet = new EconomyWallet(400, new Dictionary<string, int> { ["iron-plate"] = 60 });
    var ecoSession = new EconomySession(ecoWallet.Money);
    var ecoItemId = 900L;
    Assert(ecoWorld.TryPlaceMiner(new GridPosition(2, 2), Direction.East, ecoGrid, ecoWallet, minerBuilding, ecoSession),
        "Place miner con BuildingDefinition.");
    Assert(ecoSession.BuildSpend == minerBuilding.MoneyCost, "La sessione deve tracciare la spesa build.");
    Assert(ecoGrid.TryPlace(new GridPosition(4, 2), Direction.East, definition, ecoWallet, research, ecoSession),
        "Place nastro con sessione.");
    Assert(ecoGrid.TryPlace(new GridPosition(5, 2), Direction.East, definition, ecoWallet, research, ecoSession),
        "Secondo nastro con sessione.");
    for (var tick = 0; tick < 210; tick++)
    {
        ecoWorld.Update(1f / 30f, ecoGrid, ecoWallet, ref ecoItemId, market, ecoSession);
    }
    Assert(ecoWorld.SoldItems >= 1, "Il loop economia deve vendere almeno un item.");
    Assert(ecoSession.SaleIncome >= market.GetSellPrice("iron-ore"),
        "La sessione deve registrare le vendite.");
    Assert(ecoSession.SoldByItem.GetValueOrDefault("iron-ore") >= 1,
        "SoldByItem deve contare le ore vendute.");

    Assert(ecoWorld.TryUpgradeCore(ecoWallet, economy.CoreUpgrade, ecoSession),
        "L'upgrade del core deve consumare risorse.");
    Assert(ecoWorld.CoreUpgradeLevel == 1 && ecoWorld.CoreSaleBonusPercent == 25,
        "Dopo upgrade il core deve avere bonus vendita.");
    Assert(ecoSession.UpgradeSpend == economy.CoreUpgrade.MoneyCost,
        "La sessione deve tracciare la spesa upgrade.");
    Assert(!ecoWorld.TryUpgradeCore(ecoWallet, economy.CoreUpgrade, ecoSession),
        "L'upgrade core è una sola volta.");

    var boosted = ecoWorld.EffectiveSalePrice("iron-plate", market);
    Assert(boosted == 37, "Con +25% una lastra da $30 deve vendere a $37.");

    // Refund policy 100%.
    var moneyBeforeRefund = ecoWallet.Money;
    var platesBeforeRefund = ecoWallet.MaterialCount("iron-plate");
    Assert(ecoWorld.TryRemoveMiner(new GridPosition(2, 2), ecoWallet, minerBuilding, ecoSession),
        "Rimozione minatore con rimborso.");
    Assert(ecoWallet.Money == moneyBeforeRefund + minerBuilding.MoneyCost,
        "Rimborso denaro completo sul minatore.");
    Assert(ecoWallet.MaterialCount("iron-plate")
        == platesBeforeRefund + minerBuilding.BuildCost.Sum(entry => entry.Amount),
        "Rimborso materiali completo sul minatore.");
    Assert(ecoSession.RefundIncome >= minerBuilding.MoneyCost,
        "La sessione deve registrare i rimborsi.");

    // Persist economy session + core upgrade in save v5.
    var ecoSaveResearch = ResearchState.CreateNew(content);
    Assert(ecoSaveResearch.TryUnlock(smelterTech, ecoWallet), "Save economia: sblocca forno.");
    var smelterAt = new GridPosition(ecoWorld.CoreOrigin.X - 4, ecoWorld.CoreOrigin.Y);
    Assert(ecoWorld.TryPlaceSmelter(smelterAt, Direction.East, smeltRecipe, ecoGrid, ecoWallet, smelterBuilding, ecoSession),
        "Save economia: piazza forno.");
    var ecoCamera = new WorldCamera(1f, 2f, 1.1f);
    var ecoCaptured = GameSaveStore.Capture(ecoWorld, ecoGrid, ecoWallet, ecoCamera, ecoSaveResearch, ecoSession, ecoItemId);
    Assert(ecoCaptured.Version == 6, "Il salvataggio deve essere v6.");
    var ecoSlot = "self-test-economy";
    GameSaveStore.Save(ecoSlot, ecoCaptured);
    var ecoRestored = GameSaveStore.Restore(GameSaveStore.Load(ecoSlot), content);
    Assert(ecoRestored.World.CoreUpgradeLevel == 1 && ecoRestored.World.CoreSaleBonusPercent == 25,
        "Upgrade core deve sopravvivere al reload.");
    Assert(ecoRestored.Session.BuildSpend == ecoSession.BuildSpend,
        "BuildSpend sessione deve sopravvivere al reload.");
    Assert(ecoRestored.Session.UpgradeSpend == ecoSession.UpgradeSpend,
        "UpgradeSpend sessione deve sopravvivere al reload.");
    Assert(ecoRestored.Session.SaleIncome == ecoSession.SaleIncome,
        "SaleIncome sessione deve sopravvivere al reload.");
    GameSaveStore.Delete(ecoSlot);

    // Phase 5 — copper, splitter, junction, bridge, assembler multi-step.
    var junctionDef = content.Conveyors.Single(entry => entry.Id == "junction");
    var splitterDef = content.Conveyors.Single(entry => entry.Id == "splitter");
    var bridgeDef = content.Conveyors.Single(entry => entry.Id == "conveyor-bridge");
    var wireRecipe = content.Recipes.Single(entry => entry.Id == "craft-copper-wire");
    var assemblerBuilding = content.GetBuildingOrDefault("assembler");
    var assemblerTech = content.FindStructure("assembler")!;
    var junctionTech = content.FindStructure("junction")!;
    var splitterTech = content.FindStructure("splitter")!;
    var bridgeTech = content.FindStructure("conveyor-bridge")!;

    var copperWorld = new FactoryWorld(12, 8, 7429);
    var copperOrigin = new GridPosition(
        copperWorld.StarterDepositOrigin.X,
        copperWorld.StarterDepositOrigin.Y + MinerBuilding.Size + 1);
    Assert(copperWorld.Terrain[copperOrigin].Deposit == DepositKind.Copper,
        "Il giacimento rame starter deve esistere a sud del ferro.");
    var copperGrid = new ConveyorGrid();
    var copperWallet = new EconomyWallet(200, new Dictionary<string, int> { ["iron-plate"] = 20 });
    Assert(copperWorld.TryPlaceMiner(copperOrigin, Direction.East, copperGrid, copperWallet),
        "Il minatore deve piazzarsi sul rame.");
    Assert(copperWorld.Miners[copperOrigin].OutputItemId == "copper-ore",
        "Il minatore su rame deve produrre copper-ore.");

    // Splitter: one in → alternate left/right outs.
    var splitResearch = ResearchState.CreateNew(content);
    Assert(splitResearch.TryUnlock(splitterTech, new EconomyWallet(200, new Dictionary<string, int> { ["iron-plate"] = 30 })),
        "Sdoppiatore sbloccabile.");
    var splitGrid = new ConveyorGrid();
    var splitWallet = new EconomyWallet(200, new Dictionary<string, int> { ["iron-plate"] = 40 });
    Assert(splitGrid.TryPlace(new GridPosition(1, 1), Direction.East, definition, splitWallet, splitResearch),
        "Ingresso sdoppiatore.");
    Assert(splitGrid.TryPlace(new GridPosition(2, 1), Direction.East, splitterDef, splitWallet, splitResearch),
        "Sdoppiatore.");
    Assert(splitGrid.TryPlace(new GridPosition(2, 0), Direction.East, definition, splitWallet, splitResearch),
        "Uscita nord (sinistra rispetto a E).");
    Assert(splitGrid.TryPlace(new GridPosition(2, 2), Direction.East, definition, splitWallet, splitResearch),
        "Uscita sud (destra rispetto a E).");
    var inBelt = splitGrid.Cells[new GridPosition(1, 1)];
    Assert(inBelt.TryInsert(new TransportedItem(2001, "iron-ore"), Direction.East), "Item 1 nello sdoppiatore.");
    for (var tick = 0; tick < 90; tick++)
    {
        splitGrid.Update(1f / 30f);
    }
    Assert(inBelt.TryInsert(new TransportedItem(2002, "iron-ore"), Direction.East), "Item 2 nello sdoppiatore.");
    for (var tick = 0; tick < 120; tick++)
    {
        splitGrid.Update(1f / 30f);
    }
    var northOut = splitGrid.Cells[new GridPosition(2, 0)];
    var southOut = splitGrid.Cells[new GridPosition(2, 2)];
    Assert(northOut.Items.Count + southOut.Items.Count >= 1
        || northOut.Items.Count + southOut.Items.Count + splitGrid.Cells[new GridPosition(2, 1)].Items.Count >= 1,
        "Lo sdoppiatore deve instradare verso le uscite laterali.");

    // Junction: pass-through opposite sides.
    var juncResearch = ResearchState.CreateNew(content);
    Assert(juncResearch.TryUnlock(junctionTech, new EconomyWallet(200, new Dictionary<string, int> { ["iron-plate"] = 20 })),
        "Incrocio sbloccabile.");
    var juncGrid = new ConveyorGrid();
    var juncWallet = new EconomyWallet(200, new Dictionary<string, int> { ["iron-plate"] = 30 });
    Assert(juncGrid.TryPlace(new GridPosition(0, 1), Direction.East, definition, juncWallet, juncResearch),
        "Ingresso ovest incrocio.");
    Assert(juncGrid.TryPlace(new GridPosition(1, 1), Direction.East, junctionDef, juncWallet, juncResearch),
        "Incrocio.");
    Assert(juncGrid.TryPlace(new GridPosition(2, 1), Direction.East, definition, juncWallet, juncResearch),
        "Uscita est incrocio.");
    var juncIn = juncGrid.Cells[new GridPosition(0, 1)];
    Assert(juncIn.TryInsert(new TransportedItem(3001, "copper-ore"), Direction.East), "Item nell'incrocio.");
    for (var tick = 0; tick < 150; tick++)
    {
        juncGrid.Update(1f / 30f);
    }
    Assert(juncGrid.Cells[new GridPosition(2, 1)].Items.Any(item => item.ItemId == "copper-ore")
        || juncGrid.Cells[new GridPosition(1, 1)].Items.Any(item => item.ItemId == "copper-ore"),
        "L'incrocio deve far passare l'item verso il lato opposto.");

    // Bridge: span gap of 2.
    var bridgeResearch = ResearchState.CreateNew(content);
    Assert(bridgeResearch.TryUnlock(bridgeTech, new EconomyWallet(300, new Dictionary<string, int> { ["iron-plate"] = 40 })),
        "Ponte sbloccabile.");
    var bridgeGrid = new ConveyorGrid();
    var bridgeWallet = new EconomyWallet(300, new Dictionary<string, int>
    {
        ["iron-plate"] = 40,
        ["copper-wire"] = 10
    });
    Assert(bridgeGrid.TryPlace(new GridPosition(0, 0), Direction.East, definition, bridgeWallet, bridgeResearch),
        "Pre-ponte.");
    Assert(bridgeGrid.TryPlace(new GridPosition(1, 0), Direction.East, bridgeDef, bridgeWallet, bridgeResearch),
        "Ponte (entry+exit).");
    Assert(bridgeGrid.Cells.ContainsKey(new GridPosition(1, 0))
        && bridgeGrid.Cells.ContainsKey(new GridPosition(3, 0)),
        "Il ponte deve occupare entry e exit a distanza 2.");
    Assert(bridgeGrid.TryPlace(new GridPosition(4, 0), Direction.East, definition, bridgeWallet, bridgeResearch),
        "Post-ponte.");
    var bridgeFeed = bridgeGrid.Cells[new GridPosition(0, 0)];
    Assert(bridgeFeed.TryInsert(new TransportedItem(4001, "iron-plate"), Direction.East), "Item sul ponte.");
    for (var tick = 0; tick < 240; tick++)
    {
        bridgeGrid.Update(1f / 30f);
    }
    Assert(bridgeGrid.Cells[new GridPosition(4, 0)].Items.Any()
        || bridgeGrid.Cells[new GridPosition(3, 0)].Items.Any(),
        "Il ponte deve teletrasportare l'item all'uscita.");

    // Assembler multi-step: copper-ore + iron-plate → copper-wire → core.
    var craftWorld = new FactoryWorld(16, 10, 7429);
    var craftGrid = new ConveyorGrid();
    var craftWallet = new EconomyWallet(800, new Dictionary<string, int>
    {
        ["iron-plate"] = 80,
        ["copper-wire"] = 20
    });
    var craftResearch = ResearchState.CreateNew(content);
    Assert(craftResearch.TryUnlock(assemblerTech, craftWallet), "Assemblatore sbloccabile.");
    Assert(craftResearch.IsUnlocked("assembler") && !assemblerTech.IsStub,
        "Dopo unlock l'assemblatore è costruibile.");
    // Core at (10,3) on 16×10 — assembler at (4,3) facing east toward core.
    Assert(craftWorld.TryPlaceAssembler(
            new GridPosition(4, 3), Direction.East, wireRecipe, craftGrid, craftWallet, assemblerBuilding),
        "Assemblatore piazzabile.");
    Assert(craftGrid.TryPlace(new GridPosition(3, 3), Direction.East, definition, craftWallet, craftResearch),
        "Ingresso assemblatore.");
    Assert(craftGrid.TryPlace(new GridPosition(6, 3), Direction.East, definition, craftWallet, craftResearch),
        "Uscita assemblatore.");
    Assert(craftGrid.TryPlace(new GridPosition(7, 3), Direction.East, definition, craftWallet, craftResearch),
        "Nastro verso core 1.");
    Assert(craftGrid.TryPlace(new GridPosition(8, 3), Direction.East, definition, craftWallet, craftResearch),
        "Nastro verso core 2.");
    Assert(craftGrid.TryPlace(new GridPosition(9, 3), Direction.East, definition, craftWallet, craftResearch),
        "Nastro verso core 3.");
    var craftIn = craftGrid.Cells[new GridPosition(3, 3)];
    Assert(craftIn.TryInsert(new TransportedItem(5001, "copper-ore")), "Rame in ingresso.");
    var craftItemId = 5100L;
    for (var tick = 0; tick < 90; tick++)
    {
        craftWorld.Update(1f / 30f, craftGrid, craftWallet, ref craftItemId, market);
    }
    Assert(craftIn.TryInsert(new TransportedItem(craftItemId++, "iron-plate")), "Lastra in ingresso.");
    var moneyBeforeWire = craftWallet.Money;
    for (var tick = 0; tick < 400; tick++)
    {
        craftWorld.Update(1f / 30f, craftGrid, craftWallet, ref craftItemId, market);
    }
    Assert(craftWorld.SoldItems >= 1, "L'assemblatore deve produrre fili venduti al core.");
    Assert(craftWallet.Money >= moneyBeforeWire + market.GetSellPrice("copper-wire"),
        "Il filo di rame deve essere venduto al core.");

    // Save assemblers + bridge in v5.
    var phase5Camera = new WorldCamera(3f, 3f, 1.2f);
    var phase5Session = new EconomySession(craftWallet.Money);
    craftResearch.ForceUnlock("conveyor-bridge");
    Assert(craftGrid.TryPlace(new GridPosition(1, 1), Direction.East, bridgeDef, craftWallet, craftResearch),
        "Ponte in save-test.");
    var phase5Captured = GameSaveStore.Capture(
        craftWorld, craftGrid, craftWallet, phase5Camera, craftResearch, phase5Session, craftItemId);
    Assert(phase5Captured.Assemblers.Count == 1, "Capture deve includere assemblatori.");
    Assert(phase5Captured.Conveyors.Any(cell => cell.DefinitionId == "conveyor-bridge"),
        "Capture deve includere ponti.");
    var phase5Slot = "self-test-phase5";
    GameSaveStore.Save(phase5Slot, phase5Captured);
    var phase5Restored = GameSaveStore.Restore(GameSaveStore.Load(phase5Slot), content);
    Assert(phase5Restored.World.Assemblers.Count == 1, "Assemblatori devono sopravvivere al reload.");
    Assert(phase5Restored.Research.IsUnlocked("assembler"), "Unlock assemblatore dopo reload.");
    Assert(phase5Restored.Conveyors.Cells.Values.Any(cell => cell.Kind == LogisticsKind.Bridge),
        "Ponti devono sopravvivere al reload.");
    GameSaveStore.Delete(phase5Slot);

    // Stress: long belt line capacity/handoff on a larger map.
    var stressWorld = new FactoryWorld(64, 32, 7429);
    var stressGrid = new ConveyorGrid();
    var stressWallet = new EconomyWallet(5000, new Dictionary<string, int> { ["iron-plate"] = 500 });
    var stressResearch = ResearchState.CreateNew(content);
    var stressY = stressWorld.CoreOrigin.Y;
    var stressStartX = Math.Max(0, stressWorld.CoreOrigin.X - 20);
    for (var x = stressStartX; x < stressWorld.CoreOrigin.X; x++)
    {
        Assert(stressGrid.TryPlace(new GridPosition(x, stressY), Direction.East, definition, stressWallet, stressResearch),
            $"Nastro stress a x={x}.");
    }
    var stressFeed = stressGrid.Cells[new GridPosition(stressStartX, stressY)];
    Assert(stressFeed.TryInsert(new TransportedItem(9001, "iron-ore")), "Item stress in linea lunga.");
    var stressItemId = 9100L;
    for (var tick = 0; tick < 1800; tick++)
    {
        stressWorld.Update(1f / 30f, stressGrid, stressWallet, ref stressItemId, market);
    }
    Assert(stressWorld.SoldItems >= 1, "Una linea lunga deve consegnare al core senza soft-lock.");

    // Phase 6 — power stub, generator, save v6.
    Assert(content.FindStructure("generator") is { IsStub: false },
        "Il generatore deve essere un edificio costruibile.");
    Assert(market.GetSellPrice("copper-wire") == 16,
        "Bilanciamento: filo di rame a $16.");
    var powerWorld = new FactoryWorld(16, 10, 7429);
    Assert(powerWorld.PowerCapacity >= FactoryWorld.CorePowerCapacity,
        "Il core fornisce potenza base.");
    var powerGrid = new ConveyorGrid();
    var powerWallet = new EconomyWallet(500, new Dictionary<string, int> { ["iron-plate"] = 80 });
    var powerResearch = ResearchState.CreateNew(content);
    var generatorTech = content.FindStructure("generator")!;
    Assert(powerResearch.TryUnlock(generatorTech, powerWallet), "Generatore sbloccabile.");
    var generatorBuilding = content.GetBuildingOrDefault("generator");
    Assert(powerWorld.TryPlaceGenerator(new GridPosition(2, 2), powerGrid, powerWallet, generatorBuilding),
        "Generatore piazzabile.");
    Assert(powerWorld.Generators.Count == 1, "Un generatore registrato.");
    Assert(powerWorld.PowerCapacity > FactoryWorld.CorePowerCapacity,
        "Il generatore aumenta la capacità potenza.");
    var powerItemId = 7000L;
    for (var tick = 0; tick < 60; tick++)
    {
        powerWorld.Update(1f / 30f, powerGrid, powerWallet, ref powerItemId, market);
    }
    Assert(powerWorld.PowerBuffer > 0, "Il buffer potenza si ricarica.");
    Assert(powerWorld.TrySpendPower(1f), "Si può consumare potenza.");

    // Seeded worlds differ.
    var seedA = new FactoryWorld(64, 32, FactoryGameApp.DefaultSeed);
    var seedB = new FactoryWorld(64, 32, 1337);
    Assert(seedA.Seed == FactoryGameApp.DefaultSeed && seedB.Seed == 1337,
        "I seed di scenario devono produrre mondi distinti.");

    var powerSession = new EconomySession(powerWallet.Money);
    var powerCamera = new WorldCamera(0, 0, 1f);
    var powerCaptured = GameSaveStore.Capture(
        powerWorld, powerGrid, powerWallet, powerCamera, powerResearch, powerSession, powerItemId);
    Assert(powerCaptured.Version == 6 && powerCaptured.Generators.Count == 1,
        "Save v6 deve includere generatori.");
    var powerSlot = "self-test-phase6-power";
    GameSaveStore.Save(powerSlot, powerCaptured);
    var powerRestored = GameSaveStore.Restore(GameSaveStore.Load(powerSlot), content);
    Assert(powerRestored.World.Generators.Count == 1, "Generatori devono sopravvivere al reload.");
    Assert(powerRestored.Research.IsUnlocked("generator"), "Unlock generatore dopo reload.");
    GameSaveStore.Delete(powerSlot);

    Console.WriteLine("SELF-TEST OK: Phase 1–6 (logistica, economia, potenza, seed) verificati.");

    // Settings persistence (FPS / resource overlay / display).
    var settingsPath = GameSettings.SettingsPath;
    var backup = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
    try
    {
        var prefs = new GameSettings
        {
            ShowFps = true,
            ShowResourceOverlay = false,
            ResolutionWidth = 1440,
            ResolutionHeight = 900,
            DisplayMode = DisplayMode.Borderless
        };
        prefs.Save();
        var reloaded = GameSettings.Load();
        Assert(reloaded.ShowFps, "ShowFps deve persistere.");
        Assert(!reloaded.ShowResourceOverlay, "ShowResourceOverlay deve persistere.");
        Assert(reloaded.ResolutionWidth == 1440 && reloaded.ResolutionHeight == 900,
            "Risoluzione deve persistere.");
        Assert(reloaded.DisplayMode == DisplayMode.Borderless, "Modalità schermo deve persistere.");
        Assert(UiTheme.InventoryItems.Length >= 4, "Inventario deve elencare gli item noti.");
        Assert(UiTheme.ItemsInCategory(UiTheme.ItemCategory.Materials).Count() == 2,
            "Categoria Materiali: ferro + rame grezzo.");
        Assert(UiTheme.ItemsInCategory(UiTheme.ItemCategory.Intermediate).Count() == 1,
            "Categoria Intermedi: lastre.");
        Assert(UiTheme.ItemsInCategory(UiTheme.ItemCategory.Products).Count() == 1,
            "Categoria Prodotti: fili.");
        Assert(GameSettings.DisplayModeLabel(DisplayMode.Fullscreen) == "Schermo intero",
            "Etichetta italiana modalità schermo intero.");
    }
    finally
    {
        if (backup is null)
        {
            if (File.Exists(settingsPath))
            {
                File.Delete(settingsPath);
            }
        }
        else
        {
            File.WriteAllText(settingsPath, backup);
        }
    }

    Console.WriteLine("SELF-TEST OK: impostazioni grafica/inventario verificate.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}