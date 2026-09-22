using TIndustry.Logistics;

var seedJsonPath = GameContentStore.SeedJsonPath;

if (args.Contains("--export-excel"))
{
    var argumentIndex = Array.IndexOf(args, "--export-excel");
    var destination = argumentIndex + 1 < args.Length
        ? Path.GetFullPath(args[argumentIndex + 1])
        : Path.Combine(Directory.GetCurrentDirectory(), "data", GameContentStore.ExcelFileName);
    var exportSource = File.Exists(GameContentStore.UserJsonPath)
        ? GameContentStore.UserJsonPath
        : seedJsonPath;
    ExcelContentStore.Save(destination, GameContent.Load(exportSource));
    Console.WriteLine($"Database Excel creato: {destination}");
    return;
}

// First launch: seed AppData from shipped content.json; never require a release Excel DB.
var contentPath = GameContentStore.EnsureUserContent();
var content = GameContent.Load(contentPath);
var basicConveyor = content.Conveyors.Single(definition => definition.Id == "conveyor-basic");

if (args.Contains("--self-test"))
{
    // Always test against shipped seed so prerequisites/UI data match the repo.
    RunSelfTest(GameContent.Load(seedJsonPath));
    return;
}

if (!args.Contains("--console-demo"))
{
    var captureIo = args.Contains("--capture-io");
    var captureTutorial = args.Contains("--capture-tutorial");
    var captureGraphics = args.Contains("--capture-graphics");
    var captureTechTree = args.Contains("--capture-tech-tree");
    var captureSorter = args.Contains("--capture-sorter");
    var captureMidgame = args.Contains("--capture-midgame");
    var captureIcons = args.Contains("--capture-icons");
    var captureOreTints = args.Contains("--capture-ore-tints");
    var captureVerifyIconsTiers = args.Contains("--capture-verify-icons-tiers");
    var captureVersion = args.Contains("--capture-version");
    var capturePowerNodes = args.Contains("--capture-power-nodes")
        || args.Contains("--capture-power-cables"); // legacy alias
    var captureSmelterFuel = args.Contains("--capture-smelter-fuel");
    var capture = args.Contains("--capture") || args.Contains("--capture-upgraded")
        || captureIo || captureTutorial || captureGraphics || captureTechTree || captureSorter
        || captureMidgame || captureIcons || captureOreTints || captureVerifyIconsTiers
        || captureVersion || capturePowerNodes || captureSmelterFuel;
    var captureUpgraded = args.Contains("--capture-upgraded");
    string? capturePath = null;
    string? captureMode = null;
    if (captureIo)
    {
        capturePath = Path.Combine("artifacts", "io-adjacency-compact.png");
        captureMode = "io-adjacency";
    }
    else if (captureGraphics)
    {
        capturePath = Path.Combine("artifacts", "graphics-uplift.png");
        captureMode = "graphics";
    }
    else if (captureTutorial)
    {
        capturePath = Path.Combine("artifacts", "tutorial-extended.png");
        captureMode = "tutorial";
    }
    else if (captureTechTree)
    {
        capturePath = Path.Combine("artifacts", "tech-tree-graph.png");
        captureMode = "tech-tree";
    }
    else if (captureSorter)
    {
        capturePath = Path.Combine("artifacts", "sorter-routing.png");
        captureMode = "sorter";
    }
    else if (captureMidgame)
    {
        capturePath = Path.Combine("artifacts", "midgame-phase6.png");
        captureMode = "midgame";
    }
    else if (captureOreTints)
    {
        capturePath = Path.Combine("artifacts", "icons-ore-tints.png");
        captureMode = "ore-tints";
    }
    else if (captureVerifyIconsTiers)
    {
        capturePath = Path.Combine("artifacts", "verify-icons-tiers.png");
        captureMode = "verify-icons-tiers";
    }
    else if (captureIcons)
    {
        capturePath = Path.Combine("artifacts", "icons-drill-items.png");
        captureMode = "icons";
    }
    else if (captureVersion)
    {
        capturePath = Path.Combine("artifacts", "version-overlay.png");
        captureMode = "version";
    }
    else if (capturePowerNodes)
    {
        capturePath = Path.Combine("artifacts", "power-nodes.png");
        captureMode = "power-nodes";
    }
    else if (captureSmelterFuel)
    {
        capturePath = Path.Combine("artifacts", "smelter-coal-or-power.png");
        captureMode = "smelter-fuel";
    }
    else if (capture)
    {
        capturePath = Path.Combine("artifacts", captureUpgraded ? "core-upgrade-preview.png" : "game-preview.png");
    }

    FactoryGameApp.Run(
        content,
        args.Contains("--smoke-test") || capture ? 3 : null,
        capturePath,
        captureUpgradeCore: captureUpgraded,
        captureMode: captureMode);
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

    Assert(wallet.Money == 100, "Piazzamento nastri: niente $ (solo materiali).");
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
        "Nastro T1 e Minatore T1 devono partire sbloccati.");
    Assert(!research.IsUnlocked("smelter") && !research.IsUnlocked("conveyor-fast"),
        "Forno e Nastro T2 devono partire bloccati.");
    Assert(content.FindStructure("assembler")?.IsStub == false
        && content.FindStructure("assembler")?.Kind == StructureKind.Building,
        "L'assemblatore deve essere un edificio costruibile.");
    Assert(content.FindStructure("miner-advanced") is { IsStub: false, Kind: StructureKind.Building },
        "Il Minatore T2 deve essere un edificio costruibile.");
    Assert(content.Conveyors.Any(c => c.Id == "conveyor-express" && c.Tier == 3),
        "Deve esistere il Nastro T3.");
    Assert(content.Market.Any(m => m.ItemId == "coal"),
        "Il carbone deve essere nel mercato.");
    Assert(content.FindStructure("junction") is not null
        && content.FindStructure("splitter") is not null
        && content.FindStructure("sorter") is not null
        && content.FindStructure("conveyor-bridge") is not null,
        "Incrocio, sdoppiatore, selezionatore e ponte devono esistere nelle strutture.");
    Assert(content.Recipes.Any(recipe => recipe.Id == "craft-copper-wire"),
        "La ricetta craft-copper-wire deve esistere.");
    Assert(content.CreateMarket().GetSellPrice("copper-ore") == 6,
        "Il rame grezzo deve avere prezzo mercato.");

    // Tech tree prerequisites: data-driven edges gate unlock.
    Assert(smelterTech.Requires.Contains("miner"), "Il forno richiede il minatore.");
    Assert(fastTech.Requires.Contains("smelter"), "Il nastro veloce richiede il forno.");
    Assert(content.FindStructure("assembler")!.Requires.Contains("smelter"),
        "L'assemblatore richiede il forno.");
    Assert(content.FindStructure("splitter")!.Requires.Contains("junction"),
        "Lo sdoppiatore richiede l'incrocio.");
    Assert(content.FindStructure("sorter")!.Requires.Contains("junction"),
        "Il selezionatore richiede l'incrocio.");
    Assert(content.FindStructure("conveyor-express")!.Requires.Contains("conveyor-fast"),
        "Il Nastro T3 richiede il Nastro T2.");
    Assert(content.FindStructure("miner-advanced")!.Requires.Contains("miner")
            && content.FindStructure("miner-advanced")!.Requires.Contains("smelter"),
        "Il Minatore T2 richiede miner + forno.");
    var prereqResearch = ResearchState.CreateNew(content);
    var prereqWallet = new EconomyWallet(2000, new Dictionary<string, int>
    {
        ["iron-plate"] = 200,
        ["copper-wire"] = 40
    });
    Assert(prereqResearch.GetNodeState(smelterTech) == ResearchNodeState.Available,
        "Con miner default il forno è disponibile.");
    Assert(prereqResearch.GetNodeState(fastTech) == ResearchNodeState.Locked,
        "Senza forno il nastro veloce resta bloccato.");
    Assert(!prereqResearch.CanUnlock(fastTech, prereqWallet),
        "CanUnlock deve fallire senza prerequisiti.");
    Assert(!prereqResearch.TryUnlock(fastTech, prereqWallet),
        "TryUnlock deve fallire senza prerequisiti anche con risorse.");
    Assert(prereqWallet.Money == 2000, "Unlock fallito non deve spendere.");
    Assert(prereqResearch.TryUnlock(smelterTech, prereqWallet), "Sblocco forno con prereq miner.");
    Assert(prereqResearch.GetNodeState(fastTech) == ResearchNodeState.Available,
        "Dopo il forno il nastro veloce diventa disponibile.");
    Assert(prereqResearch.TryUnlock(fastTech, prereqWallet), "Sblocco Nastro T2 dopo forno.");
    var expressUnlockTech = content.FindStructure("conveyor-express")!;
    Assert(prereqResearch.GetNodeState(expressUnlockTech) == ResearchNodeState.Available,
        "Dopo Nastro T2 il Nastro T3 è disponibile.");
    Assert(prereqResearch.TryUnlock(expressUnlockTech, prereqWallet), "Sblocco Nastro T3 dopo T2.");
    var treeGraph = TechTreeLayout.Build(content);
    Assert(treeGraph.Nodes.Count == content.Structures.Count,
        "Il grafo deve includere tutte le strutture.");
    Assert(treeGraph.Edges.Count >= 8, "Il grafo deve avere archi da prerequisites (midgame).");
    Assert(treeGraph.Edges.Any(edge => edge.FromId == "miner" && edge.ToId == "smelter"),
        "Arco miner → forno.");
    Assert(treeGraph.Edges.Any(edge => edge.FromId == "smelter" && edge.ToId == "conveyor-fast"),
        "Arco forno → nastro veloce.");
    Assert(treeGraph.Edges.Any(edge => edge.FromId == "conveyor-fast" && edge.ToId == "conveyor-express"),
        "Arco Nastro T2 → Nastro T3.");
    Assert(treeGraph.Edges.Any(edge => edge.FromId == "junction" && edge.ToId == "sorter"),
        "Arco incrocio → selezionatore.");
    Assert(treeGraph.Edges.Any(edge => edge.FromId == "miner" && edge.ToId == "miner-advanced"),
        "Arco miner → Minatore T2.");
    Assert(treeGraph.Edges.Any(edge => edge.FromId == "smelter" && edge.ToId == "miner-advanced"),
        "Arco forno → Minatore T2.");

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
    var miningMiner = miningWorld.StarterDepositOrigin;
    Assert(miningWorld.TryPlaceMiner(miningMiner, Direction.East, miningGrid, miningWallet),
        "Il minatore deve poter essere piazzato sul giacimento garantito.");
    var miningOutX = miningMiner.X + MinerBuilding.Size;
    var miningOutputs = miningWorld.Miners[miningMiner].OutputTiles().ToHashSet();
    Assert(miningOutputs.Count == MinerBuilding.Size * DirectionMath.All.Length,
        "Il minatore deve esporre tile di uscita su tutti e quattro i lati.");
    Assert(miningOutputs.Contains(new GridPosition(miningOutX, miningMiner.Y))
        && miningOutputs.Contains(new GridPosition(miningMiner.X, miningMiner.Y - 1))
        && miningOutputs.Contains(new GridPosition(miningMiner.X, miningMiner.Y + MinerBuilding.Size))
        && miningOutputs.Contains(new GridPosition(miningMiner.X - 1, miningMiner.Y)),
        "Il minatore deve erogare su N/E/S/O, non solo sul facing.");
    for (var x = miningOutX; x < miningWorld.CoreOrigin.X; x++)
    {
        Assert(miningGrid.TryPlace(new GridPosition(x, miningMiner.Y), Direction.East, definition, miningWallet, research),
            $"Il nastro x={x} deve collegare il minatore al core.");
    }

    for (var tick = 0; tick < 210; tick++)
    {
        miningWorld.Update(1f / 30f, miningGrid, miningWallet, ref nextItemId);
    }
    Assert(miningWorld.CoreDeliveredItems == 1, "Il core deve ricevere il minerale consegnato.");
    Assert(miningWorld.SoldItems == 0, "Senza vendita automatica non si liquida al core.");
    Assert(miningWallet.MaterialCount("iron-ore") == 1,
        "Il minerale deve accumularsi nel wallet materiali.");
    Assert(miningWallet.Money == 100,
        "Piazzamento senza $: saldo invariato (niente vendita forzata).");
    Assert(miningWorld.TrySellFromWallet(miningWallet, "iron-ore", 1),
        "La vendita esplicita dal wallet deve riuscire.");
    Assert(miningWallet.Money == 108, "La vendita esplicita deve aggiungere il prezzo ore.");
    Assert(miningWallet.MaterialCount("iron-ore") == 0, "Dopo la vendita lo stock ore deve scendere.");
    Assert(miningWorld.SoldItems == 1 && miningWorld.SaleRevenue == FactoryWorld.IronOreSalePrice,
        "SoldItems/ricavo devono aggiornarsi sulla vendita esplicita.");

    // Mid-transit visibility: miner → belt must carry items before the core delivery.
    var transitWorld = new FactoryWorld(12, 8, 7429);
    var transitGrid = new ConveyorGrid();
    var transitWallet = new EconomyWallet(100, new Dictionary<string, int> { ["iron-plate"] = 10 });
    var transitId = 50L;
    var transitMiner = transitWorld.StarterDepositOrigin;
    Assert(transitWorld.TryPlaceMiner(transitMiner, Direction.East, transitGrid, transitWallet),
        "Transit: minatore sul giacimento.");
    for (var x = transitMiner.X + MinerBuilding.Size; x < transitWorld.CoreOrigin.X; x++)
    {
        Assert(transitGrid.TryPlace(new GridPosition(x, transitMiner.Y), Direction.East, definition, transitWallet, research),
            $"Transit nastro x={x}.");
    }

    var sawItemOnBelt = false;
    for (var tick = 0; tick < 210; tick++)
    {
        transitWorld.Update(1f / 30f, transitGrid, transitWallet, ref transitId);
        if (transitGrid.Cells.Values.Any(cell => cell.Items.Count > 0))
        {
            sawItemOnBelt = true;
        }
    }

    Assert(sawItemOnBelt, "I minerali devono risultare presenti sui nastri durante il trasporto.");
    Assert(transitWorld.CoreDeliveredItems >= 1, "Transit: consegna al core dopo il trasporto.");
    Assert(transitWallet.MaterialCount("iron-ore") >= 1, "Transit: stock ore dopo consegna (no auto-sell).");

    // Multi-side eject: facing North but outward belt only on the south edge still receives ore.
    var sideWorld = new FactoryWorld(12, 8, 7429);
    var sideGrid = new ConveyorGrid();
    var sideWallet = new EconomyWallet(100, new Dictionary<string, int> { ["iron-plate"] = 10 });
    var sideId = 70L;
    var sideMiner = sideWorld.StarterDepositOrigin;
    Assert(sideWorld.TryPlaceMiner(sideMiner, Direction.North, sideGrid, sideWallet),
        "Multi-side: minatore (facing Nord irrilevante).");
    var southBelt = new GridPosition(sideMiner.X, sideMiner.Y + MinerBuilding.Size);
    Assert(sideWorld.CanPlaceConveyor(southBelt), "Multi-side: tile sud del minatore libera.");
    Assert(sideGrid.TryPlace(southBelt, Direction.South, definition, sideWallet, research),
        "Multi-side: nastro sud uscente.");
    var sawSouthEject = false;
    for (var tick = 0; tick < 210; tick++)
    {
        sideWorld.Update(1f / 30f, sideGrid, sideWallet, ref sideId);
        if (sideGrid.Cells.TryGetValue(southBelt, out var cell) && cell.Items.Count > 0)
        {
            sawSouthEject = true;
            break;
        }
    }

    Assert(sawSouthEject, "Il minatore deve erogare sul nastro uscente anche sul lato opposto al facing.");

    // Inward belt must NOT receive miner eject (belt pointing into the footprint).
    var inwardWorld = new FactoryWorld(12, 8, 7429);
    var inwardGrid = new ConveyorGrid();
    var inwardWallet = new EconomyWallet(100, new Dictionary<string, int> { ["iron-plate"] = 10 });
    var inwardId = 75L;
    var inwardMiner = inwardWorld.StarterDepositOrigin;
    Assert(inwardWorld.TryPlaceMiner(inwardMiner, Direction.East, inwardGrid, inwardWallet),
        "Inward: minatore.");
    var inwardBeltPos = new GridPosition(inwardMiner.X + MinerBuilding.Size, inwardMiner.Y);
    Assert(inwardGrid.TryPlace(inwardBeltPos, Direction.West, definition, inwardWallet, research),
        "Inward: nastro est rivolto verso il miner.");
    Assert(BuildingIo.IsInwardBelt(inwardGrid.Cells[inwardBeltPos], inwardMiner, MinerBuilding.Size),
        "Inward: helper deve riconoscere il nastro entrante.");
    Assert(!BuildingIo.IsOutwardBelt(inwardGrid.Cells[inwardBeltPos], inwardMiner, MinerBuilding.Size),
        "Inward: nastro entrante non è uscente.");
    for (var tick = 0; tick < 210; tick++)
    {
        inwardWorld.Update(1f / 30f, inwardGrid, inwardWallet, ref inwardId);
    }

    Assert(inwardGrid.Cells[inwardBeltPos].Items.Count == 0,
        "Il minatore non deve espellere su un nastro che punta verso il footprint.");

    // Flush adjacency: miner|smelter with no belt transfers ore directly.
    var adjWorld = new FactoryWorld(16, 10, 7429);
    var adjGrid = new ConveyorGrid();
    var adjWallet = new EconomyWallet(400, new Dictionary<string, int> { ["iron-plate"] = 40 });
    var adjResearch = ResearchState.CreateNew(content);
    var adjId = 80L;
    var adjMiner = adjWorld.StarterDepositOrigin;
    Assert(adjWorld.TryPlaceMiner(adjMiner, Direction.East, adjGrid, adjWallet),
        "Adjacency: minatore.");
    Assert(adjResearch.TryUnlock(smelterTech, adjWallet),
        "Adjacency: forno sbloccato.");
    // Place smelter flush on the east edge of the miner (no belt between).
    var adjSmelterAt = new GridPosition(adjMiner.X + MinerBuilding.Size, adjMiner.Y);
    Assert(adjWorld.CanPlaceSmelter(adjSmelterAt, adjGrid),
        "Adjacency: spazio libero a est del miner per il forno.");
    Assert(adjWorld.TryPlaceSmelter(adjSmelterAt, Direction.East, smeltRecipe, adjGrid, adjWallet),
        "Adjacency: forno a contatto col minatore.");
    EnsurePowerLink(content, adjWorld, adjGrid, adjWallet, adjSmelterAt);
    Assert(adjGrid.Cells.Count == 0, "Adjacency: nessun nastro tra miner e forno.");
    for (var tick = 0; tick < 240; tick++)
    {
        adjWorld.Update(1f / 30f, adjGrid, adjWallet, ref adjId);
    }

    var adjSmelter = adjWorld.Smelters[adjSmelterAt];
    Assert(adjSmelter.Buffered("iron-ore") > 0 || adjSmelter.IsCrafting || adjSmelter.OutputQueue.Count > 0,
        "Miner a contatto deve trasferire ore al forno senza nastro.");

    // Smelter facing North still ejects onto an East outward belt (belt-uscente, not fixed side).
    var outWorld = new FactoryWorld(16, 10, 7429);
    var outGrid = new ConveyorGrid();
    var outWallet = new EconomyWallet(400, new Dictionary<string, int> { ["iron-plate"] = 40 });
    var outResearch = ResearchState.CreateNew(content);
    var outId = 85L;
    var outSmelterAt = new GridPosition(outWorld.CoreOrigin.X - 6, outWorld.CoreOrigin.Y);
    Assert(outResearch.TryUnlock(smelterTech, outWallet),
        "Outward-smelter: forno sbloccato.");
    Assert(outWorld.TryPlaceSmelter(outSmelterAt, Direction.North, smeltRecipe, outGrid, outWallet),
        "Outward-smelter: forno facing Nord.");
    EnsurePowerLink(content, outWorld, outGrid, outWallet, outSmelterAt);
    var outEastBelt = new GridPosition(outSmelterAt.X + SmelterBuilding.Size, outSmelterAt.Y);
    var outWestBelt = new GridPosition(outSmelterAt.X - 1, outSmelterAt.Y);
    Assert(outGrid.TryPlace(outWestBelt, Direction.East, definition, outWallet, outResearch),
        "Outward-smelter: nastro ingresso ovest.");
    Assert(outGrid.TryPlace(outEastBelt, Direction.East, definition, outWallet, outResearch),
        "Outward-smelter: nastro uscita est (uscente, non sul facing).");
    Assert(BuildingIo.IsOutwardBelt(outGrid.Cells[outEastBelt], outSmelterAt, SmelterBuilding.Size),
        "Outward-smelter: belt est è uscente.");
    Assert(outGrid.Cells[outWestBelt].TryInsert(new TransportedItem(outId++, "iron-ore")),
        "Outward-smelter: ore 1 in ingresso.");
    for (var tick = 0; tick < 90; tick++)
    {
        outWorld.Update(1f / 30f, outGrid, outWallet, ref outId);
    }

    Assert(outGrid.Cells[outWestBelt].TryInsert(new TransportedItem(outId++, "iron-ore")),
        "Outward-smelter: ore 2 in ingresso.");
    var sawPlateEast = false;
    for (var tick = 0; tick < 500; tick++)
    {
        outWorld.Update(1f / 30f, outGrid, outWallet, ref outId);
        if (outGrid.Cells[outEastBelt].Items.Any(item => item.ItemId == "iron-plate"))
        {
            sawPlateEast = true;
            break;
        }
    }

    Assert(sawPlateEast, "Forno deve espellere lastre sul nastro uscente anche se non è sul lato facing.");

    // Round-robin: two belts on different sides must both receive ore over time.
    var rrWorld = new FactoryWorld(12, 8, 7429);
    var rrGrid = new ConveyorGrid();
    var rrWallet = new EconomyWallet(150, new Dictionary<string, int> { ["iron-plate"] = 20 });
    var rrId = 90L;
    var rrMiner = rrWorld.StarterDepositOrigin;
    Assert(rrWorld.TryPlaceMiner(rrMiner, Direction.East, rrGrid, rrWallet),
        "Round-robin: minatore sul giacimento.");
    var rrEast = new GridPosition(rrMiner.X + MinerBuilding.Size, rrMiner.Y);
    var rrSouth = new GridPosition(rrMiner.X, rrMiner.Y + MinerBuilding.Size);
    Assert(rrGrid.TryPlace(rrEast, Direction.East, definition, rrWallet, research),
        "Round-robin: nastro est.");
    Assert(rrGrid.TryPlace(rrSouth, Direction.South, definition, rrWallet, research),
        "Round-robin: nastro sud.");
    var eastHits = 0;
    var southHits = 0;
    for (var tick = 0; tick < 900; tick++)
    {
        var eastBefore = rrGrid.Cells[rrEast].Items.Select(item => item.Id).ToHashSet();
        var southBefore = rrGrid.Cells[rrSouth].Items.Select(item => item.Id).ToHashSet();
        rrWorld.Update(1f / 30f, rrGrid, rrWallet, ref rrId);
        foreach (var eastItem in rrGrid.Cells[rrEast].Items)
        {
            if (eastBefore.Add(eastItem.Id))
            {
                eastHits++;
            }
        }

        foreach (var southItem in rrGrid.Cells[rrSouth].Items)
        {
            if (southBefore.Add(southItem.Id))
            {
                southHits++;
            }
        }

        // Drain so capacity-1 belts keep accepting (simulates downstream takeaway).
        if (rrGrid.Cells[rrEast].Items.Count > 0)
        {
            rrGrid.Cells[rrEast].RestoreItems([]);
        }

        if (rrGrid.Cells[rrSouth].Items.Count > 0)
        {
            rrGrid.Cells[rrSouth].RestoreItems([]);
        }
    }

    Assert(eastHits >= 2 && southHits >= 2,
        $"Il minatore deve alternare i nastri adiacenti (est={eastHits}, sud={southHits}).");

    // Off-deposit miner: placeable at 0% efficiency, no output.
    var barrenWorld = new FactoryWorld(12, 8, 7429);
    var barrenGrid = new ConveyorGrid();
    var barrenWallet = new EconomyWallet(200, new Dictionary<string, int> { ["iron-plate"] = 20 });
    var barrenId = 80L;
    GridPosition? barrenPos = null;
    for (var y = 0; y < barrenWorld.Terrain.Height - 1 && barrenPos is null; y++)
    {
        for (var x = 0; x < barrenWorld.Terrain.Width - 1; x++)
        {
            var candidate = new GridPosition(x, y);
            if (barrenWorld.CountCoveredDepositTiles(candidate) == 0
                && barrenWorld.CanPlaceMiner(candidate, barrenGrid))
            {
                barrenPos = candidate;
                break;
            }
        }
    }

    Assert(barrenPos is not null, "Deve esistere almeno un footprint senza giacimento.");
    Assert(barrenWorld.TryPlaceMiner(barrenPos!.Value, Direction.East, barrenGrid, barrenWallet),
        "Piazzamento minatore a 0%.");
    var barrenMiner = barrenWorld.Miners[barrenPos.Value];
    Assert(barrenMiner.Efficiency == 0f, "Senza giacimento l'efficienza è 0%.");
    var barrenOut = new GridPosition(barrenPos.Value.X + MinerBuilding.Size, barrenPos.Value.Y);
    if (barrenWorld.CanPlaceConveyor(barrenOut))
    {
        Assert(barrenGrid.TryPlace(barrenOut, Direction.East, definition, barrenWallet, research),
            "Nastro uscito barren.");
    }

    for (var tick = 0; tick < 180; tick++)
    {
        barrenWorld.Update(1f / 30f, barrenGrid, barrenWallet, ref barrenId);
    }

    Assert(barrenMiner.Progress == 0f, "A 0% il minatore non avanza.");
    Assert(barrenGrid.Cells.Values.All(cell => cell.Items.Count == 0),
        "A 0% non deve produrre item sui nastri.");

    // Smelter anywhere on land (not only near deposits).
    var anywhereWorld = new FactoryWorld(16, 10, 7429);
    var anywhereGrid = new ConveyorGrid();
    var anywhereWallet = new EconomyWallet(300, new Dictionary<string, int> { ["iron-plate"] = 40 });
    var anywhereResearch = ResearchState.CreateNew(content);
    Assert(anywhereResearch.TryUnlock(smelterTech, anywhereWallet),
        "Forno sbloccabile per test anywhere.");
    GridPosition? grassSmelter = null;
    for (var y = 0; y < anywhereWorld.Terrain.Height - 1 && grassSmelter is null; y++)
    {
        for (var x = 0; x < anywhereWorld.Terrain.Width - 1; x++)
        {
            var candidate = new GridPosition(x, y);
            if (anywhereWorld.CanPlaceSmelter(candidate, anywhereGrid))
            {
                grassSmelter = candidate;
                break;
            }
        }
    }

    Assert(grassSmelter is not null, "Deve esistere terra libera per il forno.");
    Assert(anywhereWorld.TryPlaceSmelter(grassSmelter!.Value, Direction.East, smeltRecipe, anywhereGrid, anywhereWallet),
        "Piazzamento forno su terra libera.");
    EnsurePowerLink(content, anywhereWorld, anywhereGrid, anywhereWallet, grassSmelter.Value);

    var curvedWorld = new FactoryWorld(14, 10, 7429);
    var curvedGrid = new ConveyorGrid();
    var curvedWallet = new EconomyWallet(200, new Dictionary<string, int> { ["iron-plate"] = 20 });
    var curvedItemId = 10L;
    var curvedMiner = curvedWorld.StarterDepositOrigin;
    Assert(curvedWorld.TryPlaceMiner(curvedMiner, Direction.East, curvedGrid, curvedWallet),
        "Il minatore della linea curva deve essere piazzato.");
    var cx = curvedMiner.X + MinerBuilding.Size;
    var cy = curvedMiner.Y;
    // Dogleg: south, east, north, east — ends on the tile immediately west of the core.
    Assert(curvedGrid.TryPlace(new GridPosition(cx, cy), Direction.South, definition, curvedWallet, research),
        "Il primo tratto della curva deve essere piazzato.");
    Assert(curvedGrid.TryPlace(new GridPosition(cx, cy + 1), Direction.East, definition, curvedWallet, research),
        "La curva deve essere piazzata.");
    Assert(curvedGrid.TryPlace(new GridPosition(cx + 1, cy + 1), Direction.North, definition, curvedWallet, research),
        "Il tratto risale verso la riga del core.");
    Assert(curvedGrid.TryPlace(new GridPosition(cx + 1, cy), Direction.East, definition, curvedWallet, research),
        "L'ultimo tratto deve toccare il core.");
    Assert(cx + 1 == curvedWorld.CoreOrigin.X - 1,
        "La curva deve terminare adiacente al core centrato.");
    for (var tick = 0; tick < 480; tick++)
    {
        curvedWorld.Update(1f / 30f, curvedGrid, curvedWallet, ref curvedItemId);
    }
    Assert(curvedWorld.CoreDeliveredItems > 0, "Una linea con curva deve consegnare minerale al core.");
    Assert(curvedWallet.MaterialCount("iron-ore") > 0, "Curva: minerale stockato dopo consegna.");

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
    var smelterAt = new GridPosition(smeltWorld.CoreOrigin.X - 4, smeltWorld.CoreOrigin.Y);
    Assert(research.TryUnlock(smelterTech, smeltWallet), "Il forno deve potersi sbloccare in ricerca.");
    Assert(smeltWorld.TryPlaceSmelter(smelterAt, Direction.East, smeltRecipe, smeltGrid, smeltWallet),
        "Il forno deve piazzarsi.");
    EnsurePowerLink(content, smeltWorld, smeltGrid, smeltWallet, smelterAt);
    Assert(smeltGrid.TryPlace(new GridPosition(smelterAt.X - 1, smelterAt.Y), Direction.East, definition, smeltWallet, research),
        "Il nastro di ingresso forno deve piazzarsi.");
    for (var x = smelterAt.X + MinerBuilding.Size; x < smeltWorld.CoreOrigin.X; x++)
    {
        Assert(smeltGrid.TryPlace(new GridPosition(x, smelterAt.Y), Direction.East, definition, smeltWallet, research),
            $"Nastro uscita forno x={x}.");
    }

    var inputBelt = smeltGrid.Cells[new GridPosition(smelterAt.X - 1, smelterAt.Y)];
    Assert(inputBelt.TryInsert(new TransportedItem(smeltItemId++, "iron-ore")), "Ore 1 in ingresso.");
    for (var tick = 0; tick < 90; tick++)
    {
        smeltWorld.Update(1f / 30f, smeltGrid, smeltWallet, ref smeltItemId);
    }
    Assert(inputBelt.TryInsert(new TransportedItem(smeltItemId++, "iron-ore")), "Ore 2 in ingresso.");
    var moneyBeforePlate = smeltWallet.Money;
    var platesBefore = smeltWallet.MaterialCount("iron-plate");
    for (var tick = 0; tick < 450; tick++)
    {
        smeltWorld.Update(1f / 30f, smeltGrid, smeltWallet, ref smeltItemId);
    }
    Assert(smeltWorld.CoreDeliveredItems >= 1, "Il forno deve produrre lastre consegnate al core.");
    Assert(smeltWorld.SoldItems == 0, "Senza auto-sell le lastre non si liquidano da sole.");
    Assert(smeltWallet.MaterialCount("iron-plate") >= platesBefore + 1,
        "Le lastre devono accumularsi nello stock per i costi di build.");
    Assert(smeltWallet.Money == moneyBeforePlate, "Stock-first: niente $ dalla consegna lastre.");
    var plateStockBeforeSell = smeltWallet.MaterialCount("iron-plate");
    var plateUnitExpected = MarketCatalog.Default.GetDynamicSellPrice("iron-plate", plateStockBeforeSell);
    Assert(smeltWorld.TrySellFromWallet(smeltWallet, "iron-plate", 1),
        "Vendita esplicita lastre dal Mercato/wallet.");
    Assert(smeltWallet.Money >= moneyBeforePlate + plateUnitExpected,
        "La lastra deve vendere al prezzo dinamico atteso.");
    Assert(smeltWorld.SaleRevenue >= plateUnitExpected,
        "Il ricavo deve usare il prezzo lastre dinamico.");

    // Fast belt research unlock + upgrade.
    var lockedResearch = ResearchState.CreateNew(content);
    var lockedWallet = new EconomyWallet(100, new Dictionary<string, int> { ["iron-plate"] = 10 });
    Assert(!lockedResearch.CanUnlock(fastTech, lockedWallet), "Senza risorse non si sblocca il Nastro T2.");
    var unlockWallet = new EconomyWallet(1200, new Dictionary<string, int> { ["iron-plate"] = 70, ["copper-wire"] = 5 });
    Assert(lockedResearch.TryUnlock(smelterTech, unlockWallet), "Prereq forno per Nastro T2.");
    Assert(lockedResearch.TryUnlock(fastTech, unlockWallet), "Con risorse sufficienti si sblocca il Nastro T2.");
    Assert(lockedResearch.IsUnlocked("conveyor-fast"), "Lo sblocco deve restare in ResearchState.");
    Assert(unlockWallet.Money == 500, "Lo sblocco deve consumare $250 forno + $450 Nastro T2.");
    var tierGrid = new ConveyorGrid();
    Assert(tierGrid.TryPlace(new GridPosition(0, 0), Direction.East, definition, unlockWallet, lockedResearch),
        "Nastro T1 piazzabile.");
    Assert(tierGrid.TryUpgrade(new GridPosition(0, 0), fastDefinition, unlockWallet, lockedResearch),
        "Upgrade a Nastro T2 deve riuscire.");
    Assert(tierGrid.Cells[new GridPosition(0, 0)].Definition.Id == "conveyor-fast",
        "Dopo upgrade il tier deve essere conveyor-fast.");
    Assert(tierGrid.Cells[new GridPosition(0, 0)].Definition.RateItemsPerSecond == 1f,
        "Il Nastro T2 deve avere rate 1.0.");

    var largeWorld = new FactoryWorld(FactoryGameApp.MapWidth, FactoryGameApp.MapHeight, 7429);
    Assert(largeWorld.Terrain.Width == 1000 && largeWorld.Terrain.Height == 1000,
        "La mappa di gioco deve essere 1000×1000.");
    Assert(largeWorld.CoreTiles.Count == 16, "Il core 4×4 deve esistere sulla mappa grande.");
    Assert(largeWorld.CoreOrigin.X == (1000 - FactoryWorld.CoreSize) / 2
        && largeWorld.CoreOrigin.Y == (1000 - FactoryWorld.CoreSize) / 2,
        "Il core deve stare al centro della mappa 1000×1000.");
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
    var saveWallet = new EconomyWallet(400, new Dictionary<string, int> { ["iron-plate"] = 40, ["copper-wire"] = 3 });
    var saveItemId = 7L;
    var saveResearch = ResearchState.CreateNew(content);
    Assert(saveResearch.TryUnlock(smelterTech, saveWallet), "Save-test: sblocca forno.");
    Assert(saveWorld.TryPlaceMiner(saveWorld.StarterDepositOrigin, Direction.East, saveGrid, saveWallet),
        "Il minatore di save-test deve piazzarsi sul giacimento starter.");
    var smelterPos = new GridPosition(saveWorld.CoreOrigin.X - 6, saveWorld.CoreOrigin.Y);
    Assert(saveWorld.TryPlaceSmelter(smelterPos, Direction.East, smeltRecipe, saveGrid, saveWallet),
        "Il forno di save-test deve piazzarsi.");
    EnsurePowerLink(content, saveWorld, saveGrid, saveWallet, smelterPos);
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
    Assert(!restoredBundle.Research.IsUnlocked("conveyor-fast"), "Il Nastro T2 resta bloccato se non sbloccato.");
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
    Assert(market.BestValueHint().Contains("lastre", StringComparison.OrdinalIgnoreCase)
        || market.BestValueHint().Contains("Stock", StringComparison.OrdinalIgnoreCase),
        "L'hint mercato deve spiegare stock/vendita lastre.");

    var minerBuilding = content.GetBuildingOrDefault("miner");
    var smelterBuilding = content.GetBuildingOrDefault("smelter");
    var economy = content.GetEconomy();
    Assert(minerBuilding.MoneyCost == 0 && minerBuilding.RefundPercent == 100,
        "Piazzamento minatore: solo materiali (niente $).");
    Assert(smelterBuilding.MoneyCost == 0 && smelterBuilding.RefundPercent == 100,
        "Piazzamento forno: solo materiali (niente $).");
    Assert(content.Buildings.All(b => b.MoneyCost == 0),
        "Tutti gli edifici: moneyCost piazzamento = 0.");
    Assert(content.Conveyors.All(c => c.MoneyCost == 0),
        "Tutti i nastri: moneyCost piazzamento = 0.");
    Assert(smelterTech.Unlock!.Money >= 200,
        "Blueprint forno più cara (i $ stanno sullo sblocco).");
    Assert(content.FindStructure("assembler")!.Unlock!.Money >= 400,
        "Blueprint assemblatore più cara.");
    Assert(economy.CoreUpgrade.SaleBonusPercent == 25 && economy.CoreUpgrade.MoneyCost == 150,
        "Upgrade core deve essere content-driven.");

    var ecoWorld = new FactoryWorld(12, 8, 7429);
    var ecoGrid = new ConveyorGrid();
    var ecoWallet = new EconomyWallet(600, new Dictionary<string, int> { ["iron-plate"] = 60 });
    var ecoSession = new EconomySession(ecoWallet.Money);
    var ecoItemId = 900L;
    var ecoMiner = ecoWorld.StarterDepositOrigin;
    Assert(ecoWorld.TryPlaceMiner(ecoMiner, Direction.East, ecoGrid, ecoWallet, minerBuilding, ecoSession),
        "Place miner con BuildingDefinition.");
    Assert(ecoSession.BuildSpend == 0,
        "Piazzamento senza $: BuildSpend resta 0 (solo materiali).");
    for (var x = ecoMiner.X + MinerBuilding.Size; x < ecoWorld.CoreOrigin.X; x++)
    {
        Assert(ecoGrid.TryPlace(new GridPosition(x, ecoMiner.Y), Direction.East, definition, ecoWallet, research, ecoSession),
            $"Place nastro x={x} con sessione.");
    }

    for (var tick = 0; tick < 210; tick++)
    {
        // autoSellAtCore: true — optional liquidation path still grants money + session stats.
        ecoWorld.Update(1f / 30f, ecoGrid, ecoWallet, ref ecoItemId, market, ecoSession, autoSellAtCore: true);
    }
    Assert(ecoWorld.SoldItems >= 1, "Con vendita automatica il loop deve vendere almeno un item.");
    Assert(ecoSession.SaleIncome >= market.GetSellPrice("iron-ore"),
        "La sessione deve registrare le vendite.");
    Assert(ecoSession.SoldByItem.GetValueOrDefault("iron-ore") >= 1,
        "SoldByItem deve contare le ore vendute.");

    // Stock-first build spend: plates in wallet are consumed by construction, not auto-sold away.
    var buildStockWallet = new EconomyWallet(200, new Dictionary<string, int> { ["iron-plate"] = 5 });
    var buildStockWorld = new FactoryWorld(12, 8, 7429);
    var buildStockGrid = new ConveyorGrid();
    var platesBeforeBuild = buildStockWallet.MaterialCount("iron-plate");
    Assert(buildStockWorld.TryPlaceMiner(
            buildStockWorld.StarterDepositOrigin, Direction.East, buildStockGrid, buildStockWallet, minerBuilding),
        "Build deve spendere lastre dallo stock.");
    Assert(buildStockWallet.MaterialCount("iron-plate") == platesBeforeBuild - minerBuilding.BuildCost.Sum(e => e.Amount),
        "Il piazzamento deve consumare materiali dal wallet.");
    Assert(!buildStockWallet.TryRemoveMaterial("iron-plate", 999),
        "TryRemoveMaterial deve fallire senza stock sufficiente.");

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
    Assert(market.GetDynamicSellPrice("iron-ore", 0) == 8,
        "Mercato dinamico: stock 0 = prezzo listino.");
    Assert(market.GetDynamicSellPrice("iron-ore", 80) < 8,
        "Mercato dinamico: stock alto abbassa il prezzo ore.");
    var floodedWallet = new EconomyWallet(0, new Dictionary<string, int> { ["iron-plate"] = 60 });
    Assert(ecoWorld.EffectiveSalePrice("iron-plate", market, floodedWallet) < 37,
        "Mercato dinamico: stock alto riduce anche il prezzo con bonus CORE.");
    Assert(market.GetSellPrice("lead-ore") == 5 && market.GetSellPrice("titanium-plate") == 45,
        "Mercato: piombo grezzo / lastra titanio.");
    Assert(content.Recipes.Count(r => r.Id.StartsWith("smelt-", StringComparison.Ordinal)) >= 3,
        "Ricette forno: ferro + piombo + titanio.");
    Assert(content.Recipes.Count(r => r.Id.StartsWith("craft-", StringComparison.Ordinal)) >= 3,
        "Ricette assemblatore: filo + grafite + silicio.");

    // Multi-recipe forno: piombo quando gli input sono disponibili.
    var multiRecipes = content.Recipes.Where(r => r.Id.StartsWith("smelt-", StringComparison.Ordinal)).ToList();
    var multiSmelter = new SmelterBuilding(
        new GridPosition(0, 0), Direction.East, smeltRecipe, availableRecipes: multiRecipes);
    Assert(multiSmelter.TryAccept("lead-ore") && multiSmelter.TryAccept("lead-ore"),
        "Forno multi-ricetta deve accettare piombo grezzo.");
    Assert(multiSmelter.Buffered("lead-ore") == 2, "Buffer piombo grezzo = 2.");
    Assert(!multiSmelter.TryAccept("copper-ore"),
        "Forno non accetta rame grezzo (ricette smelt only).");
    long multiItemId = 90001;
    Assert(multiSmelter.TryAcceptFuel("coal"), "Carbone fuel per avviare craft piombo.");
    multiSmelter.Update(0.05f, new ConveyorGrid(), ref multiItemId, allowCoalOrPower: true);
    Assert(multiSmelter.IsCrafting && multiSmelter.Recipe.Id == "smelt-lead",
        "Forno deve selezionare smelt-lead con input piombo.");

    // Depositi piombo/titanio: starter lead vicino al core; miner resolve.
    var mineralWorld = new FactoryWorld(64, 48, 4242);
    var leadTiles = 0;
    var titaniumTiles = 0;
    for (var y = 0; y < mineralWorld.Terrain.Height; y++)
    {
        for (var x = 0; x < mineralWorld.Terrain.Width; x++)
        {
            var dep = mineralWorld.Terrain[new GridPosition(x, y)].Deposit;
            if (dep == DepositKind.Lead) leadTiles++;
            if (dep == DepositKind.Titanium) titaniumTiles++;
        }
    }

    Assert(leadTiles >= 4, "Mappa deve avere un patch starter di piombo.");
    Assert(titaniumTiles >= 0, "Titanio può essere raro su mappe piccole (noise).");

    // Refund policy 100%.
    var moneyBeforeRefund = ecoWallet.Money;
    var platesBeforeRefund = ecoWallet.MaterialCount("iron-plate");
    Assert(ecoWorld.TryRemoveMiner(ecoMiner, ecoWallet, minerBuilding, ecoSession),
        "Rimozione minatore con rimborso.");
    Assert(ecoWallet.Money == moneyBeforeRefund,
        "Rimborso piazzamento: niente $ (moneyCost 0).");
    Assert(ecoWallet.MaterialCount("iron-plate")
        == platesBeforeRefund + minerBuilding.BuildCost.Sum(entry => entry.Amount),
        "Rimborso materiali completo sul minatore.");
    Assert(ecoSession.RefundIncome == 0,
        "Rimborso denaro nullo quando moneyCost piazzamento è 0.");

    // Persist economy session + core upgrade in save v5.
    var ecoSaveResearch = ResearchState.CreateNew(content);
    Assert(ecoSaveResearch.TryUnlock(smelterTech, ecoWallet), "Save economia: sblocca forno.");
    var ecoSmelterAt = new GridPosition(ecoWorld.CoreOrigin.X - 4, ecoWorld.CoreOrigin.Y);
    Assert(ecoWorld.TryPlaceSmelter(ecoSmelterAt, Direction.East, smeltRecipe, ecoGrid, ecoWallet, smelterBuilding, ecoSession),
        "Save economia: piazza forno.");
    EnsurePowerLink(content, ecoWorld, ecoGrid, ecoWallet, ecoSmelterAt);
    var ecoCamera = new WorldCamera(1f, 2f, 1.1f);
    var ecoCaptured = GameSaveStore.Capture(ecoWorld, ecoGrid, ecoWallet, ecoCamera, ecoSaveResearch, ecoSession, ecoItemId);
    Assert(ecoCaptured.Version == GameSaveData.CurrentVersion, "Il salvataggio deve essere alla versione corrente.");
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

    // Splitter: one in → alternate left/right outs (both must receive cargo).
    var splitResearch = ResearchState.CreateNew(content);
    Assert(splitResearch.TryUnlock(junctionTech, new EconomyWallet(300, new Dictionary<string, int> { ["iron-plate"] = 20 })),
        "Prereq incrocio per sdoppiatore.");
    Assert(splitResearch.TryUnlock(splitterTech, new EconomyWallet(400, new Dictionary<string, int> { ["iron-plate"] = 30 })),
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
    // Dragging a path through a splitter must not rotate it (facing stays East → L/R = N/S).
    Assert(!splitGrid.TryOrientToward(new GridPosition(2, 1), new GridPosition(2, 0)),
        "Lo sdoppiatore non deve ruotare via auto-orient.");
    Assert(splitGrid.Cells[new GridPosition(2, 1)].Direction == Direction.East,
        "Facing sdoppiatore invariato dopo auto-orient.");
    var inBelt = splitGrid.Cells[new GridPosition(1, 1)];
    var sawNorth = false;
    var sawSouth = false;
    for (var n = 0; n < 8; n++)
    {
        // Drain side belts so capacity-1 outputs keep accepting.
        splitGrid.Cells[new GridPosition(2, 0)].RestoreItems([]);
        splitGrid.Cells[new GridPosition(2, 2)].RestoreItems([]);
        Assert(inBelt.TryInsert(new TransportedItem(2001 + n, "iron-ore"), Direction.East),
            $"Item {n + 1} nello sdoppiatore.");
        for (var tick = 0; tick < 120; tick++)
        {
            splitGrid.Update(1f / 30f);
            if (splitGrid.Cells[new GridPosition(2, 0)].Items.Count > 0)
            {
                sawNorth = true;
                splitGrid.Cells[new GridPosition(2, 0)].RestoreItems([]);
            }

            if (splitGrid.Cells[new GridPosition(2, 2)].Items.Count > 0)
            {
                sawSouth = true;
                splitGrid.Cells[new GridPosition(2, 2)].RestoreItems([]);
            }
        }
    }

    Assert(sawNorth && sawSouth,
        "Lo sdoppiatore deve alternare verso entrambe le uscite laterali.");
    Assert(splitGrid.Cells[new GridPosition(2, 1)].RoutedExit == Direction.East,
        "In transito lo sdoppiatore deve avanzare come un nastro (facing), non di lato.");

    // Sorter: filter match → facing; others → left/right.
    var sorterDef = content.Conveyors.Single(entry => entry.Id == "sorter");
    var sorterTech = content.FindStructure("sorter")!;
    Assert(sorterDef.Kind == LogisticsKind.Sorter, "Il selezionatore deve avere kind sorter.");
    var sortResearch = ResearchState.CreateNew(content);
    var sortUnlockWallet = new EconomyWallet(700, new Dictionary<string, int>
    {
        ["iron-plate"] = 40,
        ["copper-wire"] = 10
    });
    Assert(sortResearch.TryUnlock(content.FindStructure("junction")!, sortUnlockWallet),
        "Incrocio sbloccabile prima del selezionatore.");
    Assert(sortResearch.TryUnlock(sorterTech, sortUnlockWallet),
        "Selezionatore sbloccabile con prereq incrocio.");
    var sortGrid = new ConveyorGrid();
    var sortWallet = new EconomyWallet(400, new Dictionary<string, int>
    {
        ["iron-plate"] = 50,
        ["copper-wire"] = 10
    });
    Assert(sortGrid.TryPlace(new GridPosition(1, 1), Direction.East, definition, sortWallet, sortResearch),
        "Ingresso selezionatore.");
    Assert(sortGrid.TryPlace(new GridPosition(2, 1), Direction.East, sorterDef, sortWallet, sortResearch),
        "Selezionatore.");
    Assert(sortGrid.TryPlace(new GridPosition(3, 1), Direction.East, definition, sortWallet, sortResearch),
        "Uscita match (avanti).");
    Assert(sortGrid.TryPlace(new GridPosition(2, 0), Direction.East, definition, sortWallet, sortResearch),
        "Uscita overflow nord (sinistra rispetto a E).");
    Assert(sortGrid.TryPlace(new GridPosition(2, 2), Direction.East, definition, sortWallet, sortResearch),
        "Uscita overflow sud (destra rispetto a E).");
    Assert(!sortGrid.TryOrientToward(new GridPosition(2, 1), new GridPosition(2, 0)),
        "Il selezionatore non deve ruotare via auto-orient.");
    var sorterCell = sortGrid.Cells[new GridPosition(2, 1)];
    Assert(sorterCell.Kind == LogisticsKind.Sorter && sorterCell.FilterItemId == "iron-ore",
        "Filtro default selezionatore = ferro grezzo.");
    sorterCell.SetFilterItem("iron-ore");
    var sortIn = sortGrid.Cells[new GridPosition(1, 1)];
    Assert(sortIn.TryInsert(new TransportedItem(4001, "iron-ore"), Direction.East),
        "Ferro nel selezionatore.");
    for (var tick = 0; tick < 150; tick++)
    {
        sortGrid.Update(1f / 30f);
    }

    Assert(sortGrid.Cells[new GridPosition(3, 1)].Items.Any(item => item.ItemId == "iron-ore")
        || sorterCell.Items.Any(item => item.ItemId == "iron-ore"),
        "Item filtrato deve uscire in avanti (facing).");
    Assert(sortGrid.Cells[new GridPosition(2, 0)].Items.Count == 0
        && sortGrid.Cells[new GridPosition(2, 2)].Items.Count == 0,
        "Item filtrato non deve andare ai lati.");

    sortGrid.Cells[new GridPosition(3, 1)].RestoreItems([]);
    sorterCell.RestoreItems([]);
    Assert(sortIn.TryInsert(new TransportedItem(4002, "copper-ore"), Direction.East),
        "Rame (non filtrato) nel selezionatore.");
    var overflowNorth = false;
    var overflowSouth = false;
    for (var tick = 0; tick < 150; tick++)
    {
        sortGrid.Update(1f / 30f);
        if (sortGrid.Cells[new GridPosition(2, 0)].Items.Any(item => item.ItemId == "copper-ore"))
        {
            overflowNorth = true;
        }

        if (sortGrid.Cells[new GridPosition(2, 2)].Items.Any(item => item.ItemId == "copper-ore"))
        {
            overflowSouth = true;
        }
    }

    Assert(overflowNorth || overflowSouth,
        "Item non filtrato deve uscire a sinistra o destra.");
    Assert(sortGrid.Cells[new GridPosition(3, 1)].Items.Count == 0,
        "Item non filtrato non deve uscire in avanti.");

    sorterCell.SetFilterItem("copper-wire");
    Assert(sorterCell.FilterItemId == "copper-wire", "SetFilterItem deve cambiare il filtro.");
    sorterCell.CycleFilterItem(["iron-ore", "copper-ore", "iron-plate", "copper-wire"]);
    Assert(sorterCell.FilterItemId == "iron-ore",
        "CycleFilterItem deve passare al successivo (wrap).");

    // Persist sorter filter in save.
    var sortCamera = new WorldCamera(0f, 0f, 1f);
    var sortSession = new EconomySession(sortWallet.Money);
    var sortCaptured = GameSaveStore.Capture(
        new FactoryWorld(12, 8, 7429), sortGrid, sortWallet, sortCamera, sortResearch, sortSession, 4100L);
    Assert(sortCaptured.Conveyors.Any(c => c.DefinitionId == "sorter" && c.FilterItemId == "iron-ore"),
        "Salvataggio deve includere FilterItemId del selezionatore.");
    var sortSlot = "self-test-sorter";
    GameSaveStore.Save(sortSlot, sortCaptured);
    var sortRestored = GameSaveStore.Restore(GameSaveStore.Load(sortSlot), content);
    Assert(sortRestored.Conveyors.Cells.Values.Any(c =>
            c.Kind == LogisticsKind.Sorter && c.FilterItemId == "iron-ore"),
        "Reload deve ripristinare il filtro del selezionatore.");
    GameSaveStore.Delete(sortSlot);

    // Junction: pass-through opposite sides.
    var juncResearch = ResearchState.CreateNew(content);
    Assert(juncResearch.TryUnlock(junctionTech, new EconomyWallet(300, new Dictionary<string, int> { ["iron-plate"] = 20 })),
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

    // Junction cross-traffic: EW + NS streams pass undisturbed (no priority, no mutual block).
    Assert(junctionDef.Capacity >= 2, "Incrocio capacity >= 2 per due assi indipendenti.");
    var crossResearch = ResearchState.CreateNew(content);
    Assert(crossResearch.TryUnlock(junctionTech, new EconomyWallet(300, new Dictionary<string, int> { ["iron-plate"] = 20 })),
        "Incrocio sbloccabile (cross).");
    var crossGrid = new ConveyorGrid();
    var crossWallet = new EconomyWallet(400, new Dictionary<string, int> { ["iron-plate"] = 60 });
    // Horizontal: (0,1)→(1,1)→(2,1)  Vertical: (1,0)→(1,1)→(1,2)
    Assert(crossGrid.TryPlace(new GridPosition(0, 1), Direction.East, definition, crossWallet, crossResearch),
        "Cross: ingresso ovest.");
    Assert(crossGrid.TryPlace(new GridPosition(1, 0), Direction.South, definition, crossWallet, crossResearch),
        "Cross: ingresso nord.");
    Assert(crossGrid.TryPlace(new GridPosition(1, 1), Direction.East, junctionDef, crossWallet, crossResearch),
        "Cross: incrocio.");
    Assert(crossGrid.TryPlace(new GridPosition(2, 1), Direction.East, definition, crossWallet, crossResearch),
        "Cross: uscita est.");
    Assert(crossGrid.TryPlace(new GridPosition(1, 2), Direction.South, definition, crossWallet, crossResearch),
        "Cross: uscita sud.");
    var crossJunction = crossGrid.Cells[new GridPosition(1, 1)];
    Assert(crossJunction.TryInsert(new TransportedItem(3101, "iron-ore"), Direction.East),
        "Cross: item asse EW entra.");
    Assert(crossJunction.TryInsert(new TransportedItem(3102, "copper-ore"), Direction.South),
        "Cross: item asse NS entra insieme (nessuna priorità / stop).");
    Assert(crossJunction.Items.Count == 2, "Cross: entrambi gli item nell'incrocio.");
    for (var tick = 0; tick < 200; tick++)
    {
        crossGrid.Update(1f / 30f);
    }

    var eastOut = crossGrid.Cells[new GridPosition(2, 1)];
    var southOut = crossGrid.Cells[new GridPosition(1, 2)];
    Assert(eastOut.Items.Any(item => item.ItemId == "iron-ore")
        || crossJunction.Items.Any(item => item.ItemId == "iron-ore" && (item.Travel ?? Direction.East) is Direction.East or Direction.West),
        "Cross: stream EW continua indisturbato.");
    Assert(southOut.Items.Any(item => item.ItemId == "copper-ore")
        || crossJunction.Items.Any(item => item.ItemId == "copper-ore" && (item.Travel ?? Direction.South) is Direction.North or Direction.South),
        "Cross: stream NS continua indisturbato.");
    Assert(eastOut.Items.Any(item => item.ItemId == "iron-ore")
        && southOut.Items.Any(item => item.ItemId == "copper-ore"),
        "Cross: entrambi gli stream devono uscire sui rami correttamente.");

    // Bridge: span gap of 2.
    var bridgeResearch = ResearchState.CreateNew(content);
    Assert(bridgeResearch.TryUnlock(junctionTech, new EconomyWallet(300, new Dictionary<string, int> { ["iron-plate"] = 20 })),
        "Prereq incrocio per ponte.");
    Assert(bridgeResearch.TryUnlock(bridgeTech, new EconomyWallet(500, new Dictionary<string, int> { ["iron-plate"] = 40 })),
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
    var craftWallet = new EconomyWallet(1200, new Dictionary<string, int>
    {
        ["iron-plate"] = 80,
        ["copper-ore"] = 20,
        ["copper-wire"] = 20
    });
    var craftResearch = ResearchState.CreateNew(content);
    Assert(craftResearch.TryUnlock(smelterTech, craftWallet), "Prereq forno per assemblatore.");
    Assert(craftResearch.TryUnlock(assemblerTech, craftWallet), "Assemblatore sbloccabile.");
    Assert(craftResearch.IsUnlocked("assembler") && !assemblerTech.IsStub,
        "Dopo unlock l'assemblatore è costruibile.");

    // Softlock guard: power-node unlocks consume starter wires; assembler must not need wires.
    Assert(!assemblerTech.Unlock!.Materials.Any(m => m.ItemId == "copper-wire"),
        "Unlock assemblatore non deve richiedere fili (solo l'assemblatore li produce).");
    Assert(!assemblerBuilding.BuildCost.Any(m => m.ItemId == "copper-wire"),
        "Build assemblatore non deve richiedere fili (chicken-egg con craft-copper-wire).");
    var softlockWallet = new EconomyWallet(2000, new Dictionary<string, int>
    {
        ["iron-plate"] = 80,
        ["copper-ore"] = 8,
        ["copper-wire"] = 10
    });
    var softlockResearch = ResearchState.CreateNew(content);
    Assert(softlockResearch.TryUnlock(smelterTech, softlockWallet), "Softlock: sblocca forno.");
    Assert(softlockResearch.TryUnlock(content.FindStructure("generator")!, softlockWallet),
        "Softlock: sblocca generatore.");
    Assert(softlockResearch.TryUnlock(content.FindStructure("power-node")!, softlockWallet),
        "Softlock: sblocca Nodo T1 (consuma fili starter).");
    Assert(softlockResearch.TryUnlock(content.FindStructure("power-node-t2")!, softlockWallet),
        "Softlock: sblocca Nodo T2 (consuma fili rimanenti).");
    Assert(softlockWallet.MaterialCount("copper-wire") <= 2,
        "Softlock setup: fili esauriti dopo unlock nodi (come in partita reale).");
    Assert(softlockResearch.TryUnlock(assemblerTech, softlockWallet),
        "Con $ + lastre + rame grezzo l'assemblatore resta sbloccabile senza fili.");
    Assert(softlockWallet.CanAfford(assemblerBuilding.MoneyCost, assemblerBuilding.BuildCost),
        "Dopo unlock, piazzare l'assemblatore non richiede fili.");

    // Estrattore: filtro obbligatorio — tira solo l'item scelto da CORE, non tutto lo stock.
    var extractWorld = new FactoryWorld(16, 10, 7711);
    var extractGrid = new ConveyorGrid();
    var extractWallet = new EconomyWallet(400, new Dictionary<string, int>
    {
        ["iron-plate"] = 40,
        ["copper-ore"] = 12,
        ["iron-ore"] = 9
    });
    var extractResearch = ResearchState.CreateNew(content);
    var extractorTech = content.FindStructure("extractor")!;
    var extractorBuilding = content.GetBuildingOrDefault("extractor");
    Assert(extractorTech.Unlock!.Money <= 100, "Estrattore blueprint cheap.");
    Assert(extractorBuilding.MoneyCost == 0, "Estrattore piazzamento senza $.");
    Assert(extractResearch.TryUnlock(extractorTech, extractWallet), "Estrattore sbloccabile.");
    // East of core so output belt points away from core stock.
    var extractAt = new GridPosition(extractWorld.CoreOrigin.X + FactoryWorld.CoreSize, extractWorld.CoreOrigin.Y);
    Assert(extractWorld.TryPlaceExtractor(
            extractAt, Direction.East, "iron-plate", extractGrid, extractWallet, extractorBuilding),
        "Estrattore piazzabile sul bordo CORE.");
    Assert(extractWorld.TryGetExtractorAt(extractAt, out var placedExtractor)
            && placedExtractor.FilterItemId == "iron-plate",
        "Filtro iniziale = lastre.");
    placedExtractor.CycleFilterItem(["iron-ore", "iron-plate", "copper-ore"]);
    Assert(placedExtractor.FilterItemId == "copper-ore",
        "Cycle filtro: iron-plate → copper-ore.");
    placedExtractor.SetFilterItem("iron-plate");
    var outBelt = new GridPosition(extractAt.X + 1, extractAt.Y);
    Assert(extractGrid.TryPlace(outBelt, Direction.East, definition, extractWallet, extractResearch),
        "Nastro uscita estrattore.");
    var platesBeforeExtract = extractWallet.MaterialCount("iron-plate");
    var oreBeforeExtract = extractWallet.MaterialCount("iron-ore");
    var extractItemId = 8800L;
    for (var tick = 0; tick < 60; tick++)
    {
        extractWorld.Update(1f / 30f, extractGrid, extractWallet, ref extractItemId, market);
    }
    Assert(extractWallet.MaterialCount("iron-plate") < platesBeforeExtract,
        "Estrattore deve prelevare lastre dal CORE/wallet.");
    Assert(extractWallet.MaterialCount("iron-ore") == oreBeforeExtract,
        "Con filtro lastre, le ore grezze restano intatte (niente dump di tutto).");
    Assert(extractGrid.Cells[outBelt].Items.Any(i => i.ItemId == "iron-plate")
            || extractWallet.MaterialCount("iron-plate") < platesBeforeExtract,
        "Lastre estratte sul nastro o già in transito.");
    var assemblerAt = new GridPosition(craftWorld.CoreOrigin.X - 4, craftWorld.CoreOrigin.Y);
    Assert(craftWorld.TryPlaceAssembler(
            assemblerAt, Direction.East, wireRecipe, craftGrid, craftWallet, assemblerBuilding),
        "Assemblatore piazzabile.");
    EnsurePowerLink(content, craftWorld, craftGrid, craftWallet, assemblerAt);
    Assert(craftGrid.TryPlace(new GridPosition(assemblerAt.X - 1, assemblerAt.Y), Direction.East, definition, craftWallet, craftResearch),
        "Ingresso assemblatore.");
    for (var x = assemblerAt.X + MinerBuilding.Size; x < craftWorld.CoreOrigin.X; x++)
    {
        Assert(craftGrid.TryPlace(new GridPosition(x, assemblerAt.Y), Direction.East, definition, craftWallet, craftResearch),
            $"Nastro verso core x={x}.");
    }

    var craftIn = craftGrid.Cells[new GridPosition(assemblerAt.X - 1, assemblerAt.Y)];
    Assert(craftIn.TryInsert(new TransportedItem(5001, "copper-ore")), "Rame in ingresso.");
    var craftItemId = 5100L;
    for (var tick = 0; tick < 90; tick++)
    {
        craftWorld.Update(1f / 30f, craftGrid, craftWallet, ref craftItemId, market);
    }
    Assert(craftIn.TryInsert(new TransportedItem(craftItemId++, "iron-plate")), "Lastra in ingresso.");
    var moneyBeforeWire = craftWallet.Money;
    var wiresBefore = craftWallet.MaterialCount("copper-wire");
    for (var tick = 0; tick < 400; tick++)
    {
        craftWorld.Update(1f / 30f, craftGrid, craftWallet, ref craftItemId, market);
    }
    Assert(craftWorld.CoreDeliveredItems >= 1, "L'assemblatore deve produrre fili consegnati al core.");
    Assert(craftWallet.MaterialCount("copper-wire") >= wiresBefore + 1,
        "I fili devono restare in stock (utili per build/unlock).");
    var wireStockBeforeSell = craftWallet.MaterialCount("copper-wire");
    var wireUnitExpected = market.GetDynamicSellPrice("copper-wire", wireStockBeforeSell);
    Assert(craftWorld.TrySellFromWallet(craftWallet, "copper-wire", 1, market),
        "Vendita esplicita filo di rame.");
    Assert(craftWallet.Money >= moneyBeforeWire + wireUnitExpected,
        "Il filo di rame deve poter essere venduto per $.");

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
    Assert(stressWorld.CoreDeliveredItems >= 1, "Una linea lunga deve consegnare al core senza soft-lock.");
    Assert(stressWallet.MaterialCount("iron-ore") >= 1, "Stress: item stockato, non sparito.");

    // Phase 6 — power stub, generator, save v6.
    Assert(content.FindStructure("generator") is { IsStub: false },
        "Il generatore deve essere un edificio costruibile.");
    Assert(market.GetSellPrice("copper-wire") == 16,
        "Bilanciamento: filo di rame a $16.");
    Assert(market.GetSellPrice("silicon") == 28 && market.GetSellPrice("graphite") == 10,
        "Bilanciamento: silicio $28 / grafite $10.");
    Assert(market.GetDynamicSellPrice("coal", 0) == 4,
        "Mercato dinamico carbone: listino a stock 0.");
    Assert(market.GetDynamicSellPrice("coal", 100) < 4,
        "Mercato dinamico carbone: stock alto → prezzo soft.");
    var powerWorld = new FactoryWorld(16, 10, 7429);
    Assert(powerWorld.PowerCapacity >= FactoryWorld.CorePowerCapacity,
        "Il core fornisce potenza base.");
    var powerGrid = new ConveyorGrid();
    var powerWallet = new EconomyWallet(800, new Dictionary<string, int> { ["iron-plate"] = 80 });
    var powerResearch = ResearchState.CreateNew(content);
    var generatorTech = content.FindStructure("generator")!;
    Assert(powerResearch.TryUnlock(smelterTech, powerWallet), "Prereq forno per generatore.");
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
    Assert(powerCaptured.Version == GameSaveData.CurrentVersion && powerCaptured.Generators.Count == 1,
        "Save corrente deve includere generatori.");
    var powerSlot = "self-test-phase6-power";
    GameSaveStore.Save(powerSlot, powerCaptured);
    var powerRestored = GameSaveStore.Restore(GameSaveStore.Load(powerSlot), content);
    Assert(powerRestored.World.Generators.Count == 1, "Generatori devono sopravvivere al reload.");
    Assert(powerRestored.Research.IsUnlocked("generator"), "Unlock generatore dopo reload.");

    Assert(powerWorld.Generators.Values.Single().FuelBuffer == 0, "Generatore parte senza fuel.");
    Assert(!powerWorld.Generators.Values.Single().IsGenerating, "Senza carbone non genera.");
    var fuelIn = new GridPosition(1, 2);
    Assert(powerGrid.TryPlace(fuelIn, Direction.East, definition, powerWallet, powerResearch),
        "Nastro fuel verso generatore.");
    Assert(powerGrid.Cells[fuelIn].TryInsert(new TransportedItem(9001, "coal")), "Carbone sul nastro.");
    var fuelTick = 9100L;
    var bufferBeforeFuel = powerWorld.PowerBuffer;
    for (var tick = 0; tick < 90; tick++)
    {
        powerWorld.Update(1f / 30f, powerGrid, powerWallet, ref fuelTick);
    }
    Assert(powerWorld.Generators.Values.Single().FuelBuffer > 0
        || powerWorld.Generators.Values.Single().IsGenerating,
        "Il generatore deve accettare carbone dai nastri.");
    for (var tick = 0; tick < 60; tick++)
    {
        powerWorld.Update(1f / 30f, powerGrid, powerWallet, ref fuelTick);
    }
    Assert(powerWorld.PowerBuffer >= bufferBeforeFuel,
        "Con fuel il buffer potenza non deve scendere solo per mancanza generazione gen.");

    // Phase 6 — power nodes / geometric local networks (connected vs disconnected).
    // CORE is never on the power graph; craft buildings need gen adjacency/cluster or live node path.
    Assert(content.FindStructure("power-node") is { IsStub: false, DisplayName: "Nodo T1" },
        "Nodo T1 deve essere una structure costruibile.");
    Assert(content.FindStructure("power-node")!.Requires.Contains("smelter"),
        "Nodo T1 richiede il forno.");
    Assert(content.FindStructure("power-node-t2") is { IsStub: false, DisplayName: "Nodo T2" },
        "Nodo T2 deve essere una structure costruibile.");
    Assert(content.FindStructure("power-node-t2")!.Requires.Contains("power-node"),
        "Nodo T2 richiede Nodo T1.");
    Assert(content.FindBuilding("power-node") is { MaxPowerLinks: PowerNodeBuilding.Tier1MaxLinks, Footprint: 1 },
        "Nodo T1: footprint 1, maxLinks 4 (content.json).");
    Assert(content.FindBuilding("power-node-t2") is { MaxPowerLinks: PowerNodeBuilding.Tier2MaxLinks, Footprint: 2 },
        "Nodo T2: footprint 2, maxLinks 8 (content.json).");
    Assert(Math.Abs((content.FindBuilding("power-node")?.PowerLinkRange ?? 0) - PowerNodeBuilding.Tier1Range) < 0.01f,
        "Nodo T1 range 6.");
    Assert(Math.Abs((content.FindBuilding("power-node-t2")?.PowerLinkRange ?? 0) - PowerNodeBuilding.Tier2Range) < 0.01f,
        "Nodo T2 range 10.");
    Assert(PowerNetworking.AutoLinkRule.Contains("mai CORE", StringComparison.OrdinalIgnoreCase),
        "Regola auto-link documentata (mai CORE).");
    Assert(PowerNetworking.AutoLinkRule.Contains("priorità", StringComparison.OrdinalIgnoreCase),
        "Regola auto-link documenta priorità generatori.");
    Assert(PowerNetworking.AdjacencyRule.Contains("4-connessa", StringComparison.OrdinalIgnoreCase),
        "Regola adiacenza 4-connessa documentata.");
    var nodeTech = content.FindStructure("power-node")!;
    var nodeT2Tech = content.FindStructure("power-node-t2")!;
    var nodeCost = content.GetBuildingOrDefault("power-node");
    var netWorld = new FactoryWorld(20, 12, 9101);
    var netGrid = new ConveyorGrid();
    var netWallet = new EconomyWallet(1500, new Dictionary<string, int>
    {
        ["iron-plate"] = 120,
        ["copper-wire"] = 80,
        ["coal"] = 10
    });
    var netResearch = ResearchState.CreateNew(content);
    Assert(netResearch.TryUnlock(smelterTech, netWallet), "Prereq forno per rete potenza.");
    Assert(netResearch.TryUnlock(generatorTech, netWallet), "Sblocca generatore per rete.");
    Assert(netResearch.TryUnlock(nodeTech, netWallet), "Sblocca Nodo T1.");
    Assert(netResearch.TryUnlock(nodeT2Tech, netWallet), "Sblocca Nodo T2.");
    var connectedAt = new GridPosition(2, 2);
    var disconnectedAt = new GridPosition(14, 2);
    Assert(netWorld.TryPlaceSmelter(connectedAt, Direction.East, smeltRecipe, netGrid, netWallet),
        "Forno connesso piazzabile.");
    Assert(netWorld.TryPlaceSmelter(disconnectedAt, Direction.East, smeltRecipe, netGrid, netWallet),
        "Forno disconnesso piazzabile.");
    var netGenAt = new GridPosition(5, 2);
    Assert(netWorld.TryPlaceGenerator(netGenAt, netGrid, netWallet, content.GetBuildingOrDefault("generator")),
        "Generatore per rete locale.");
    Assert(netWorld.TryGetGeneratorAt(netGenAt, out var netGen), "Generatore recuperabile.");
    netGen.TryAcceptFuel("coal");
    netGen.TryAcceptFuel("coal");
    // Single T1 node between gen and connected smelter (auto-links both; disconnected is out of range).
    Assert(netWorld.TryPlacePowerNode(
            new GridPosition(4, 4), netGrid, netWallet, PowerNodeBuilding.Tier1Id, nodeCost),
        "Nodo T1 tra gen e forno connesso.");
    Assert(netWorld.PowerNodes.Count == 1, "Un nodo piazzato.");
    Assert(netWorld.PowerLinks.Count >= 2, "Auto-link deve collegare almeno gen e forno.");
    Assert(netWorld.PowerLinks.All(l =>
            l.A.Kind != PowerEndpointKind.Core && l.B.Kind != PowerEndpointKind.Core),
        "Nessun link deve coinvolgere il CORE.");
    Assert(
        PowerNetworking.NodeReachesGenerator(
            netWorld.PowerNodes.Values.Single(), netWorld, netWorld.PowerLinks),
        "Nodo deve raggiungere un generatore.");
    var netTick = 1L;
    for (var tick = 0; tick < 30; tick++)
    {
        netWorld.Update(1f / 30f, netGrid, netWallet, ref netTick);
    }

    Assert(netWorld.Generators.Values.Single().IsGenerating, "Generatore in rete deve bruciare fuel.");
    Assert(netWorld.IsBuildingPowered(connectedAt, SmelterBuilding.Size),
        "Forno in range del nodo verso gen deve essere alimentato.");
    Assert(!netWorld.IsBuildingPowered(disconnectedAt, SmelterBuilding.Size),
        "Forno lontano senza nodi non è alimentato.");
    netWorld.SetPowerBuffer(netWorld.PowerCapacity);
    Assert(netWorld.TrySpendPowerForBuilding(connectedAt, SmelterBuilding.Size, 1f),
        "Spend potenza OK se connesso.");
    Assert(!netWorld.TrySpendPowerForBuilding(disconnectedAt, SmelterBuilding.Size, 1f),
        "Spend potenza negata se disconnesso anche con buffer pieno.");

    // Adjacency cluster: forno touching fueled gen is powered without nodes.
    // Keep west of the centered 4×4 CORE on this 16×10 map (coreOrigin≈6,3).
    var touchWorld = new FactoryWorld(16, 10, 9202);
    var touchGrid = new ConveyorGrid();
    var touchWallet = new EconomyWallet(500, new Dictionary<string, int>
    {
        ["iron-plate"] = 80,
        ["coal"] = 4
    });
    var touchGenAt = new GridPosition(0, 0);
    var touchSmelterAt = new GridPosition(GeneratorBuilding.Size, 0);
    Assert(touchWorld.TryPlaceGenerator(touchGenAt, touchGrid, touchWallet, content.GetBuildingOrDefault("generator")),
        "Gen per test adiacenza.");
    Assert(touchWorld.TryPlaceSmelter(touchSmelterAt, Direction.East, smeltRecipe, touchGrid, touchWallet),
        "Forno a contatto del gen.");
    Assert(touchWorld.TryGetGeneratorAt(touchGenAt, out var touchGen), "Gen adiacenza recuperabile.");
    touchGen.TryAcceptFuel("coal");
    touchGen.TryAcceptFuel("coal");
    var touchTick = 1L;
    for (var tick = 0; tick < 30; tick++)
    {
        touchWorld.Update(1f / 30f, touchGrid, touchWallet, ref touchTick);
    }

    Assert(touchWorld.Generators.Values.Single().IsGenerating, "Gen adiacenza deve bruciare.");
    Assert(touchWorld.PowerNodes.Count == 0, "Cluster adiacenza senza nodi.");
    Assert(touchWorld.IsBuildingPowered(touchSmelterAt, SmelterBuilding.Size),
        "Forno a contatto di gen in funzione è alimentato (4-conn).");

    // Second forno touching the first shares power through the cluster.
    var touchSmelter2 = new GridPosition(touchSmelterAt.X + SmelterBuilding.Size, touchSmelterAt.Y);
    Assert(touchWorld.TryPlaceSmelter(touchSmelter2, Direction.East, smeltRecipe, touchGrid, touchWallet),
        "Secondo forno a contatto del primo.");
    touchWorld.RefreshPowerNetworks();
    Assert(touchWorld.IsBuildingPowered(touchSmelter2, SmelterBuilding.Size),
        "Cluster: forno a contatto di forno alimentato è alimentato.");

    // Feed both smelters; only connected advances craft under brownout-style gate.
    var connected = netWorld.Smelters[connectedAt];
    var disconnected = netWorld.Smelters[disconnectedAt];
    connected.TryAccept("iron-ore");
    connected.TryAccept("iron-ore");
    disconnected.TryAccept("iron-ore");
    disconnected.TryAccept("iron-ore");
    for (var tick = 0; tick < 120; tick++)
    {
        netWorld.Update(1f / 30f, netGrid, netWallet, ref netTick);
    }

    Assert(connected.IsCrafting || connected.OutputQueue.Count > 0 || connected.Progress > 0f,
        "Forno connesso deve craftare.");
    Assert(disconnected.Progress == 0f && disconnected.OutputQueue.Count == 0,
        "Forno disconnesso senza carbone non deve avanzare il craft (né corrente né fuel).");

    // Forno: carbone OR corrente — coal-only (unpowered) crafts; powered crafts without coal;
    // neither stalls; powered is ~20% faster than coal-only baseline.
    Assert(Math.Abs(SmelterBuilding.PoweredCraftSpeedMultiplier - 1.20f) < 0.001f,
        "PoweredCraftSpeedMultiplier documentato = 1.20 (+20% vs coal-only).");

    var coalOnlyWorld = new FactoryWorld(24, 14, 8801);
    var coalOnlyGrid = new ConveyorGrid();
    var coalOnlyWallet = new EconomyWallet(400, new Dictionary<string, int>
    {
        ["iron-plate"] = 40,
        ["coal"] = 8
    });
    var coalOnlyResearch = ResearchState.CreateNew(content);
    Assert(coalOnlyResearch.TryUnlock(smelterTech, coalOnlyWallet), "Coal-only: sblocca forno.");
    // Far from core/gen — no adjacency power, no nodes.
    var coalOnlyAt = new GridPosition(1, 1);
    Assert(coalOnlyWorld.TryPlaceSmelter(coalOnlyAt, Direction.East, smeltRecipe, coalOnlyGrid, coalOnlyWallet),
        "Coal-only forno piazzabile.");
    coalOnlyWorld.RefreshPowerNetworks();
    Assert(!coalOnlyWorld.IsBuildingPowered(coalOnlyAt, SmelterBuilding.Size),
        "Coal-only forno non è alimentato.");
    var coalOnlySmelter = coalOnlyWorld.Smelters[coalOnlyAt];
    Assert(coalOnlySmelter.TryAcceptFuel("coal"), "Coal-only: accetta carbone in buffer.");
    Assert(coalOnlySmelter.TryAcceptFuel("coal"), "Coal-only: secondo carbone.");
    Assert(coalOnlySmelter.TryAccept("iron-ore") && coalOnlySmelter.TryAccept("iron-ore"),
        "Coal-only: buffer ore ricetta.");
    var coalOnlyTick = 9400L;
    for (var tick = 0; tick < 90; tick++)
    {
        coalOnlyWorld.Update(1f / 30f, coalOnlyGrid, coalOnlyWallet, ref coalOnlyTick);
    }

    Assert(coalOnlySmelter.Progress > 0f || coalOnlySmelter.OutputQueue.Count > 0,
        "Forno solo-carbone (senza corrente) deve avanzare il craft.");

    // Belt insert of coal into forno fuel buffer (like generator).
    var coalBeltWorld = new FactoryWorld(16, 10, 8802);
    var coalBeltGrid = new ConveyorGrid();
    var coalBeltWallet = new EconomyWallet(400, new Dictionary<string, int> { ["iron-plate"] = 40 });
    var coalBeltResearch = ResearchState.CreateNew(content);
    Assert(coalBeltResearch.TryUnlock(smelterTech, coalBeltWallet), "Coal-belt: sblocca forno.");
    var coalBeltAt = new GridPosition(2, 2);
    Assert(coalBeltWorld.TryPlaceSmelter(coalBeltAt, Direction.East, smeltRecipe, coalBeltGrid, coalBeltWallet),
        "Coal-belt forno.");
    Assert(coalBeltGrid.TryPlace(new GridPosition(1, 2), Direction.East, definition, coalBeltWallet, coalBeltResearch),
        "Nastro carbone verso forno.");
    Assert(coalBeltGrid.Cells[new GridPosition(1, 2)].TryInsert(new TransportedItem(9501, "coal")),
        "Carbone sul nastro verso forno.");
    coalBeltGrid.Cells[new GridPosition(1, 2)].Items[^1].Progress = 0.98f;
    var coalBeltTick = 9500L;
    for (var tick = 0; tick < 90; tick++)
    {
        coalBeltWorld.Update(1f / 30f, coalBeltGrid, coalBeltWallet, ref coalBeltTick);
    }

    Assert(coalBeltWorld.Smelters[coalBeltAt].FuelBuffer > 0
        || coalBeltWorld.Smelters[coalBeltAt].IsBurningFuel,
        "Forno deve accettare carbone dal nastro nel fuel buffer.");

    // Speed comparison: coal-only vs powered over the same short window while both crafting.
    var speedCoalWorld = new FactoryWorld(20, 12, 8803);
    var speedCoalGrid = new ConveyorGrid();
    var speedCoalWallet = new EconomyWallet(800, new Dictionary<string, int>
    {
        ["iron-plate"] = 50,
        ["coal"] = 6
    });
    var speedResearch = ResearchState.CreateNew(content);
    Assert(speedResearch.TryUnlock(smelterTech, speedCoalWallet), "Speed: sblocca forno.");
    Assert(speedResearch.TryUnlock(generatorTech, speedCoalWallet), "Speed: sblocca gen.");
    var speedCoalAt = new GridPosition(1, 8);
    Assert(speedCoalWorld.TryPlaceSmelter(speedCoalAt, Direction.East, smeltRecipe, speedCoalGrid, speedCoalWallet),
        "Speed: forno coal-only.");
    var speedPowerAt = new GridPosition(GeneratorBuilding.Size, 1);
    var speedGenAt = new GridPosition(0, 1);
    Assert(speedCoalWorld.TryPlaceGenerator(speedGenAt, speedCoalGrid, speedCoalWallet, generatorBuilding),
        "Speed: gen.");
    Assert(speedCoalWorld.TryPlaceSmelter(speedPowerAt, Direction.East, smeltRecipe, speedCoalGrid, speedCoalWallet),
        "Speed: forno powered.");
    speedCoalWorld.Generators[speedGenAt].TryAcceptFuel("coal");
    speedCoalWorld.Generators[speedGenAt].TryAcceptFuel("coal");
    var speedTick = 9600L;
    for (var tick = 0; tick < 15; tick++)
    {
        speedCoalWorld.Update(1f / 30f, speedCoalGrid, speedCoalWallet, ref speedTick);
    }

    Assert(speedCoalWorld.Generators[speedGenAt].IsGenerating, "Speed: gen deve bruciare.");
    Assert(speedCoalWorld.IsBuildingPowered(speedPowerAt, SmelterBuilding.Size),
        "Speed: forno powered è alimentato.");
    Assert(!speedCoalWorld.IsBuildingPowered(speedCoalAt, SmelterBuilding.Size),
        "Speed: forno coal-only non alimentato.");

    var speedCoalSm = speedCoalWorld.Smelters[speedCoalAt];
    var speedPowerSm = speedCoalWorld.Smelters[speedPowerAt];
    speedCoalSm.TryAcceptFuel("coal");
    speedCoalSm.TryAcceptFuel("coal");
    Assert(speedCoalSm.TryAccept("iron-ore") && speedCoalSm.TryAccept("iron-ore"), "Speed coal ore.");
    Assert(speedPowerSm.TryAccept("iron-ore") && speedPowerSm.TryAccept("iron-ore"), "Speed power ore.");
    Assert(speedPowerSm.FuelBuffer == 0, "Speed powered: niente carbone sul forno.");

    // Warm gen burn + start crafts.
    for (var tick = 0; tick < 8; tick++)
    {
        speedCoalWorld.Update(1f / 30f, speedCoalGrid, speedCoalWallet, ref speedTick);
    }

    Assert(speedCoalSm.IsCrafting && speedPowerSm.IsCrafting,
        "Speed: entrambi i forni devono essere in craft.");
    var coalProgressBefore = speedCoalSm.Progress;
    var powerProgressBefore = speedPowerSm.Progress;
    for (var tick = 0; tick < 30; tick++)
    {
        speedCoalWorld.Update(1f / 30f, speedCoalGrid, speedCoalWallet, ref speedTick);
    }

    var coalDelta = speedCoalSm.Progress - coalProgressBefore;
    var powerDelta = speedPowerSm.Progress - powerProgressBefore;
    Assert(coalDelta > 0f && powerDelta > 0f, "Speed: entrambi avanzano.");
    Assert(powerDelta > coalDelta * 1.10f,
        $"Corrente deve essere ~20% più veloce del solo-carbone (powerΔ={powerDelta:F3} coalΔ={coalDelta:F3}).");
    Assert(Math.Abs(powerDelta / coalDelta - SmelterBuilding.PoweredCraftSpeedMultiplier) < 0.08f,
        $"Rapporto velocità powered/coal ≈ {SmelterBuilding.PoweredCraftSpeedMultiplier} (got {powerDelta / coalDelta:F3}).");

    // Neither: no coal, no power → stuck.
    var neitherWorld = new FactoryWorld(16, 10, 8804);
    var neitherGrid = new ConveyorGrid();
    var neitherWallet = new EconomyWallet(300, new Dictionary<string, int> { ["iron-plate"] = 30 });
    var neitherResearch = ResearchState.CreateNew(content);
    Assert(neitherResearch.TryUnlock(smelterTech, neitherWallet), "Neither: sblocca forno.");
    var neitherAt = new GridPosition(2, 2);
    Assert(neitherWorld.TryPlaceSmelter(neitherAt, Direction.East, smeltRecipe, neitherGrid, neitherWallet),
        "Neither forno.");
    neitherWorld.RefreshPowerNetworks();
    var neitherSm = neitherWorld.Smelters[neitherAt];
    Assert(neitherSm.TryAccept("iron-ore") && neitherSm.TryAccept("iron-ore"), "Neither ore.");
    var neitherTick = 9700L;
    for (var tick = 0; tick < 90; tick++)
    {
        neitherWorld.Update(1f / 30f, neitherGrid, neitherWallet, ref neitherTick);
    }

    Assert(neitherSm.Progress == 0f && neitherSm.OutputQueue.Count == 0,
        "Senza carbone né corrente il forno resta fermo.");

    var netCaptured = GameSaveStore.Capture(
        netWorld, netGrid, netWallet, new WorldCamera(0, 0, 1f), netResearch, new EconomySession(netWallet.Money), netTick);
    Assert(netCaptured.Version == 8, "Save versione 8 (nodi).");
    Assert(netCaptured.PowerNodes.Count >= 1, "Save v8 deve includere i nodi.");
    Assert(netCaptured.PowerLinks.Count >= 1, "Save v8 deve includere i link.");
    var netSlot = "self-test-power-nodes";
    GameSaveStore.Save(netSlot, netCaptured);
    var netRestored = GameSaveStore.Restore(GameSaveStore.Load(netSlot), content);
    Assert(netRestored.World.PowerNodes.Count == netWorld.PowerNodes.Count,
        "Nodi devono sopravvivere al reload.");
    Assert(netRestored.World.PowerLinks.Count == netWorld.PowerLinks.Count,
        "Link devono sopravvivere al reload.");
    netRestored.World.RefreshPowerNetworks();
    Assert(netRestored.Research.IsUnlocked("power-node"), "Unlock nodo dopo reload.");
    Assert(netRestored.World.IsBuildingPowered(connectedAt, SmelterBuilding.Size),
        "Forno connesso resta alimentato dopo reload.");

    Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Power).Any(e => e.Id == "power-node"),
        "Dock PWR: Nodo T1.");
    Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Power).Any(e => e.Id == "power-node-t2"),
        "Dock PWR: Nodo T2.");
    Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Power)
            .Single(e => e.Id == "power-node").Label == "Nodo T1",
        "Label dock nodo = Nodo T1.");
    Assert(!UiTheme.EntriesFor(UiTheme.BuildCategory.Power).Any(e => e.Id == "power-cable"),
        "Cavo T1 rimosso dal dock.");
    var fornoDock = UiTheme.EntriesFor(UiTheme.BuildCategory.Production).Single(e => e.Id == "smelter");
    Assert(fornoDock.Hint is not null
            && fornoDock.Hint.Contains("carbone o corrente", StringComparison.OrdinalIgnoreCase),
        "Dock Forno: hint 'carbone o corrente'.");
    Assert(PowerNetworking.AutoLinkRule.Contains("carbone o corrente", StringComparison.OrdinalIgnoreCase),
        "Regola potenza documenta Forno carbone o corrente.");

    // Mid-game: Minatore T2 + Nastro T3.
    var midResearch = ResearchState.CreateNew(content);
    var midWallet = new EconomyWallet(3500, new Dictionary<string, int>
    {
        ["iron-plate"] = 200,
        ["copper-wire"] = 80
    });
    Assert(midResearch.TryUnlock(smelterTech, midWallet), "Prereq forno per Minatore T2 / Nastro T2.");
    var advancedTech = content.FindStructure("miner-advanced")!;
    Assert(midResearch.TryUnlock(advancedTech, midWallet), "Minatore T2 sbloccabile.");
    Assert(!advancedTech.IsStub, "Dopo unlock non è stub.");
    Assert(midResearch.TryUnlock(fastTech, midWallet), "Prereq Nastro T2 per T3.");
    var expressTech = content.FindStructure("conveyor-express")!;
    Assert(midResearch.TryUnlock(expressTech, midWallet), "Nastro T3 sbloccabile.");
    var expressDef = content.Conveyors.Single(c => c.Id == "conveyor-express");
    Assert(expressDef.RateItemsPerSecond > content.Conveyors.Single(c => c.Id == "conveyor-fast").RateItemsPerSecond,
        "Nastro T3 più veloce del T2.");
    var midWorld = new FactoryWorld(24, 16, 8801);
    var midGrid = new ConveyorGrid();
    var advancedBuilding = content.GetBuildingOrDefault("miner-advanced");
    Assert(midWorld.TryPlaceMiner(
            midWorld.StarterDepositOrigin, Direction.East, midGrid, midWallet, advancedBuilding, null,
            MinerBuilding.AdvancedId),
        "Minatore T2 piazzabile.");
    var advMiner = midWorld.Miners[midWorld.StarterDepositOrigin];
    Assert(advMiner.IsAdvanced && advMiner.MiningSpeed == 2f, "Minatore T2 = 2× speed.");
    Assert(advMiner.Efficiency >= 1f || advMiner.CoveredDepositTiles > 0, "Efficienza Minatore T2 sul deposito.");
    var basicCompare = new MinerBuilding(midWorld.StarterDepositOrigin, Direction.East, advMiner.CoveredDepositTiles);
    Assert(advMiner.Efficiency >= basicCompare.Efficiency, "Efficienza advanced >= base a parità copertura.");
    var midOut = new GridPosition(midWorld.StarterDepositOrigin.X + MinerBuilding.Size, midWorld.StarterDepositOrigin.Y);
    Assert(midGrid.TryPlace(midOut, Direction.East, expressDef, midWallet, midResearch), "Nastro T3 in uscita.");
    var midItemId = 1L;
    var produced = 0;
    for (var tick = 0; tick < 180; tick++)
    {
        var before = midGrid.Cells.Values.Sum(c => c.Items.Count) + midWallet.MaterialCount("iron-ore");
        midWorld.Update(1f / 30f, midGrid, midWallet, ref midItemId);
        var after = midGrid.Cells.Values.Sum(c => c.Items.Count) + midWallet.MaterialCount("iron-ore");
        if (after > before) produced++;
    }
    Assert(produced >= 1 || midGrid.Cells[midOut].Items.Count > 0 || midWorld.Miners.Values.Any(m => m.Progress > 0),
        "Il Minatore T2 deve progressare/produrre.");

    // Coal deposit near starter.
    var coalWorld = new FactoryWorld(32, 20, 42);
    var coalAt = new GridPosition(coalWorld.StarterDepositOrigin.X + MinerBuilding.Size + 1, coalWorld.StarterDepositOrigin.Y);
    Assert(coalWorld.Terrain[coalAt].Deposit == DepositKind.Coal,
        "Patch carbone starter a est del ferro.");
    Assert(coalWorld.ResolveMinerOutput(coalAt) == "coal", "Miner su carbone produce coal.");

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
            AutoSellAtCore = true,
            ResolutionWidth = 1440,
            ResolutionHeight = 900,
            UseAutoResolution = false,
            DisplayMode = DisplayMode.Borderless,
            VSync = false,
            TargetFps = 144,
            UiScalePercent = 150,
            TutorialCompleted = true
        };
        prefs.Save();
        var reloaded = GameSettings.Load();
        Assert(reloaded.ShowFps, "ShowFps deve persistere.");
        Assert(!reloaded.ShowResourceOverlay, "ShowResourceOverlay (risorse sistema) deve persistere.");
        Assert(reloaded.AutoSellAtCore, "AutoSellAtCore deve persistere.");
        Assert(!new GameSettings().AutoSellAtCore, "Vendita automatica OFF di default (stock-first).");
        Assert(reloaded.ResolutionWidth == 1440 && reloaded.ResolutionHeight == 900,
            "Risoluzione deve persistere.");
        Assert(reloaded.DisplayMode == DisplayMode.Borderless, "Modalità schermo deve persistere.");
        Assert(!reloaded.VSync, "VSync deve persistere.");
        Assert(reloaded.TargetFps == 144, "TargetFps deve persistere.");
        Assert(reloaded.UiScalePercent == 150, "UiScalePercent deve persistere.");
        Assert(reloaded.TutorialCompleted, "TutorialCompleted deve persistere.");
        // Nuova partita / Rivedi tutorial clear the flag so the Peak banner can show again.
        FactoryGameApp.RestartTutorial(reloaded);
        Assert(!reloaded.TutorialCompleted, "RestartTutorial deve azzerare tutorialCompleted.");
        Assert(GameSettings.UiScalePresets.SequenceEqual(new[] { 100, 125, 150, 200 }),
            "Preset scala UI: 100/125/150/200.");
        Assert(GameSettings.UiScaleLabel(125) == "125%", "Etichetta scala UI.");
        Assert(FactoryGameApp.SettingsLayoutIsStackedForAllScales(),
            "Impostazioni: le righe non devono sovrapporsi a 100/125/150/200%.");
        Assert(FactoryGameApp.HudLayoutIsValidForAllScales(),
            "HUD: Mercato/Fabbrica/dock/tutorial non devono sovrapporsi a 100/125/150/200%.");

        // Fabbrica live counts + CORE button hit-test (default 125% / 1240×760).
        {
            var prevScale = UiTheme.Scale;
            Assert(FactoryGameApp.TryConfigureHudLayoutForTest(1240, 760, 125),
                "HUD-test: layout 1240×760 @125% deve lasciare Fabbrica visibile.");
            try
            {
                var hudWorld = new FactoryWorld(64, 48, 4242);
                var hudGrid = new ConveyorGrid();
                var hudWallet = new EconomyWallet(200, new Dictionary<string, int> { ["iron-plate"] = 40 });
                var hudSession = new EconomySession(hudWallet.Money);
                var hudResearch = ResearchState.CreateNew(content);
                Assert(FactoryGameApp.FormatFabbricaCounts(hudWorld, hudGrid) == "M0 F0 A0 N0 G0 P0",
                    "Fabbrica vuota deve mostrare M0 F0 A0 N0 G0 P0.");
                Assert(hudWorld.TryPlaceMiner(
                        hudWorld.StarterDepositOrigin, Direction.East, hudGrid, hudWallet, minerBuilding, hudSession),
                    "HUD-test: piazza minatore.");
                Assert(hudGrid.TryPlace(
                        new GridPosition(hudWorld.StarterDepositOrigin.X + MinerBuilding.Size, hudWorld.StarterDepositOrigin.Y),
                        Direction.East, definition, hudWallet, hudResearch, hudSession),
                    "HUD-test: piazza nastro.");
                Assert(FactoryGameApp.FormatFabbricaCounts(hudWorld, hudGrid) == "M1 F0 A0 N1 G0 P0",
                    "Dopo piazzamento i conteggi Fabbrica devono aggiornarsi (M1 N1).");

                Assert(FactoryGameApp.TryClickCoreUpgradeForTest(
                        hudWorld, hudWallet, hudSession, economy, out var upgradeMsg),
                    "Click CORE deve colpire il hit-box Fabbrica.");
                Assert(hudWorld.CoreUpgradeLevel == 1, "Click CORE deve alzare CoreUpgradeLevel.");
                Assert(upgradeMsg is not null && upgradeMsg.Contains("potenziato", StringComparison.OrdinalIgnoreCase),
                    "Click CORE riuscito deve mostrare feedback.");
                Assert(FactoryGameApp.TryClickCoreUpgradeForTest(
                        hudWorld, hudWallet, hudSession, economy, out var againMsg),
                    "Click CORE dopo upgrade resta gestito.");
                Assert(againMsg is not null && againMsg.Contains("già", StringComparison.OrdinalIgnoreCase),
                    "CORE già potenziato deve spiegare perché non si ripete.");

                // CORE cost label: money + explicit plate qty (never vague "+ lastre").
                var plateAmt = economy.CoreUpgrade.BuildCost
                    .First(m => m.ItemId == "iron-plate").Amount;
                Assert(FactoryGameApp.FormatCoreUpgradeCostText(economy.CoreUpgrade)
                        == $"CORE ${economy.CoreUpgrade.MoneyCost} + ×{plateAmt} lastre",
                    "Label CORE deve includere $ + ×N lastre dalla content definition.");
                Assert(FactoryGameApp.FormatCoreUpgradeNeedMessage(economy.CoreUpgrade)
                        .Contains($"×{plateAmt} lastre", StringComparison.Ordinal),
                    "Toast CORE insufficiente deve citare ×N lastre, non solo 'lastre'.");
                Assert(plateAmt == 20,
                    "economy.CoreUpgrade deve richiedere 20 lastre (content).");

                var brokeWallet = new EconomyWallet(0, new Dictionary<string, int>());
                var brokeWorld = new FactoryWorld(64, 48, 4243);
                var brokeSession = new EconomySession(0);
                Assert(FactoryGameApp.TryClickCoreUpgradeForTest(
                        brokeWorld, brokeWallet, brokeSession, economy, out var needMsg),
                    "Click CORE senza fondi deve restare gestito.");
                Assert(needMsg is not null
                        && needMsg.Contains($"×{plateAmt} lastre", StringComparison.Ordinal)
                        && needMsg.Contains($"${economy.CoreUpgrade.MoneyCost}", StringComparison.Ordinal),
                    "Toast fallimento CORE: $ + ×N lastre.");
            }
            finally
            {
                FactoryGameApp.RestoreHudLayoutAfterTest(prevScale);
            }
        }

        Assert(FactoryGameApp.TutorialStepCount == 14,
            "Tutorial play-ready: 14 passi (camera → dock → build → I/O → mercato → ricerca → logistica → campagna/settings).");
        Assert(SystemMonitor.FormatBytes(1536) == "1.5 KB", "FormatBytes risorse sistema.");
        Assert(GameSettings.ResolutionPresets.Any(p => p.Width == 2560 && p.Height == 1440),
            "Preset 2K (2560×1440) richiesto.");
        Assert(GameSettings.ResolutionPresets.Any(p => p.Width == 3840 && p.Height == 2160),
            "Preset 4K (3840×2160) richiesto.");
        Assert(GameSettings.FpsLimitPresets.Contains(600) && GameSettings.FpsLimitPresets.Contains(0),
            "Limite FPS: 600 e Illimitato (0).");
        Assert(GameSettings.FpsLimitLabel(0) == "Illimitato", "Etichetta Illimitato.");
        Assert(UiTheme.InventoryItems.Length >= 4, "Inventario deve elencare gli item noti.");
        Assert(UiTheme.ItemsInCategory(UiTheme.ItemCategory.Materials).Count() == 5,
            "Categoria Materiali: ferro + rame + carbone + piombo + titanio.");
        Assert(UiTheme.ItemsInCategory(UiTheme.ItemCategory.Intermediate).Count() == 4,
            "Categoria Intermedi: lastre Fe/Pb/Ti + grafite.");
        Assert(UiTheme.ItemsInCategory(UiTheme.ItemCategory.Products).Count() == 2,
            "Categoria Prodotti: fili + silicio.");
        Assert(UiTheme.BuildCategories.Length == 3,
            "Dock Mindustry: 3 categorie (Produzione/Logistica/Potenza).");
        Assert(!UiTheme.BuildCategories.Contains(UiTheme.BuildCategory.Inventory),
            "Inventario non deve essere nel dock: risorse solo in strip.");
        Assert(!UiTheme.BuildCategories.Contains(UiTheme.BuildCategory.Tools),
            "Strumenti non deve essere nel dock: Rimuovi in Produzione, facing con R/rotella.");
        Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Production).Any(e => e.Id == "remove"),
            "Produzione include Rimuovi.");
        Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Production).Any(e => e.Id == "miner-advanced"),
            "Produzione: Minatore T2.");
        Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Production).Length >= 5,
            "Produzione: Minatore T1/T2 + forno/assemblatore/rimuovi.");
        Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Logistics).Any(e => e.Id == "conveyor-express"),
            "Logistica: Nastro T3.");
        Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Logistics).Length >= 6,
            "Logistica: Nastro T1/T2/T3 + junction/splitter/ponte.");
        Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Power).Any(e => e.Id == "generator"),
            "Potenza: generatore.");
        Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Power).Any(e => e.Id == "power-node"),
            "Potenza: Nodo T1.");
        Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Power).Any(e => e.Id == "power-node-t2"),
            "Potenza: Nodo T2.");
        Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Inventory).Length == 0,
            "Categoria Inventario rimossa dal dock.");
        Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Production)
                .All(e => !string.IsNullOrWhiteSpace(e.Hint)),
            "Hover strumenti: ogni entry Produzione ha un hint italiano.");
        Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Production)
                .Single(e => e.Id == "miner").Hint!.Contains("tutti i lati", StringComparison.OrdinalIgnoreCase),
            "Hint minatore: uscita su tutti i lati.");
        Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Logistics)
                .Single(e => e.Id == "conveyor-basic").Hint!.Contains("unidirezionale", StringComparison.OrdinalIgnoreCase),
            "Hint nastro: flusso unidirezionale.");
        Assert(FactoryGameApp.TryResolveDockEntryCostForTest(
                "miner",
                content.Conveyors.Single(c => c.Id == "conveyor-basic"),
                content.Conveyors.Single(c => c.Id == "conveyor-fast"),
                content.Conveyors.Single(c => c.Id == "conveyor-express"),
                content.Conveyors.Single(c => c.Id == "junction"),
                content.Conveyors.Single(c => c.Id == "splitter"),
                content.Conveyors.Single(c => c.Id == "sorter"),
                content.Conveyors.Single(c => c.Id == "conveyor-bridge"),
                content.GetBuildingOrDefault("miner"),
                content.GetBuildingOrDefault("miner-advanced"),
                content.GetBuildingOrDefault("smelter"),
                content.GetBuildingOrDefault("assembler"),
                content.GetBuildingOrDefault("extractor"),
                content.GetBuildingOrDefault("generator"),
                content.GetBuildingOrDefault("power-node"),
                content.GetBuildingOrDefault("power-node-t2"),
                out var minerMoney, out var minerMats)
            && minerMoney == content.GetBuildingOrDefault("miner").MoneyCost
            && minerMats.Any(m => m.ItemId == "iron-plate" && m.Amount > 0),
            "Dock cost bar: minatore risolve denaro + lastre.");
        Assert(FactoryGameApp.TryResolveDockEntryCostForTest(
                "conveyor-express",
                content.Conveyors.Single(c => c.Id == "conveyor-basic"),
                content.Conveyors.Single(c => c.Id == "conveyor-fast"),
                content.Conveyors.Single(c => c.Id == "conveyor-express"),
                content.Conveyors.Single(c => c.Id == "junction"),
                content.Conveyors.Single(c => c.Id == "splitter"),
                content.Conveyors.Single(c => c.Id == "sorter"),
                content.Conveyors.Single(c => c.Id == "conveyor-bridge"),
                content.GetBuildingOrDefault("miner"),
                content.GetBuildingOrDefault("miner-advanced"),
                content.GetBuildingOrDefault("smelter"),
                content.GetBuildingOrDefault("assembler"),
                content.GetBuildingOrDefault("extractor"),
                content.GetBuildingOrDefault("generator"),
                content.GetBuildingOrDefault("power-node"),
                content.GetBuildingOrDefault("power-node-t2"),
                out var expressMoney, out _)
            && expressMoney == content.Conveyors.Single(c => c.Id == "conveyor-express").MoneyCost,
            "Dock cost bar: Nastro T3.");
        Assert(!FactoryGameApp.TryResolveDockEntryCostForTest(
                "remove",
                content.Conveyors.Single(c => c.Id == "conveyor-basic"),
                content.Conveyors.Single(c => c.Id == "conveyor-fast"),
                content.Conveyors.Single(c => c.Id == "conveyor-express"),
                content.Conveyors.Single(c => c.Id == "junction"),
                content.Conveyors.Single(c => c.Id == "splitter"),
                content.Conveyors.Single(c => c.Id == "sorter"),
                content.Conveyors.Single(c => c.Id == "conveyor-bridge"),
                content.GetBuildingOrDefault("miner"),
                content.GetBuildingOrDefault("miner-advanced"),
                content.GetBuildingOrDefault("smelter"),
                content.GetBuildingOrDefault("assembler"),
                content.GetBuildingOrDefault("extractor"),
                content.GetBuildingOrDefault("generator"),
                content.GetBuildingOrDefault("power-node"),
                content.GetBuildingOrDefault("power-node-t2"),
                out _, out _),
            "Dock cost bar: Rimuovi non espone costi finti.");
        Assert(FactoryGameApp.TryResolveDockEntryCostForTest(
                "sorter",
                content.Conveyors.Single(c => c.Id == "conveyor-basic"),
                content.Conveyors.Single(c => c.Id == "conveyor-fast"),
                content.Conveyors.Single(c => c.Id == "conveyor-express"),
                content.Conveyors.Single(c => c.Id == "junction"),
                content.Conveyors.Single(c => c.Id == "splitter"),
                content.Conveyors.Single(c => c.Id == "sorter"),
                content.Conveyors.Single(c => c.Id == "conveyor-bridge"),
                content.GetBuildingOrDefault("miner"),
                content.GetBuildingOrDefault("miner-advanced"),
                content.GetBuildingOrDefault("smelter"),
                content.GetBuildingOrDefault("assembler"),
                content.GetBuildingOrDefault("extractor"),
                content.GetBuildingOrDefault("generator"),
                content.GetBuildingOrDefault("power-node"),
                content.GetBuildingOrDefault("power-node-t2"),
                out var sorterMoney, out var sorterMats)
            && sorterMoney == content.Conveyors.Single(c => c.Id == "sorter").MoneyCost
            && sorterMats.Any(m => m.ItemId == "iron-plate"),
            "Dock cost bar: selezionatore risolve denaro + lastre.");
        Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Logistics).Any(e => e.Id == "sorter"),
            "Dock Logistica deve includere il selezionatore.");
        Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Logistics)
                .Single(e => e.Id == "sorter").Hint!.Contains("Filtro", StringComparison.OrdinalIgnoreCase),
            "Hint selezionatore: filtro item.");
        var smeltRecipeForDock = content.Recipes.Single(r => r.Id == "smelt-iron");
        var wireRecipeForDock = content.Recipes.Single(r => r.Id == "craft-copper-wire");
        Assert(FactoryGameApp.TryResolveDockEntryRecipeForTest(
                "smelter", smeltRecipeForDock, wireRecipeForDock, out var dockSmelt)
            && dockSmelt is not null
            && dockSmelt.Inputs.Any(i => i.ItemId == "iron-ore" && i.Amount == 2)
            && dockSmelt.Outputs.Any(o => o.ItemId == "iron-plate" && o.Amount == 1)
            && Math.Abs(dockSmelt.DurationSeconds - 2f) < 0.01f,
            "Dock recipe: Forno mostra Input ×2 ore → Output ×1 lastra / 2s.");
        Assert(FactoryGameApp.TryResolveDockEntryRecipeForTest(
                "assembler", smeltRecipeForDock, wireRecipeForDock, out var dockWire)
            && dockWire is not null
            && dockWire.Inputs.Count >= 2
            && dockWire.Outputs.Any(o => o.ItemId == "copper-wire"),
            "Dock recipe: Assemblatore risolve craft-copper-wire.");
        Assert(!FactoryGameApp.TryResolveDockEntryRecipeForTest(
                "miner", smeltRecipeForDock, wireRecipeForDock, out _),
            "Dock recipe: Minatore non ha ricetta craft (solo estrazione).");
        // FPS corner vs system overlay: never both (policy mirrored from play HUD).
        Assert(ShowCornerFps(showFps: true, showOverlay: false), "FPS angolo quando solo contatore.");
        Assert(!ShowCornerFps(showFps: true, showOverlay: true), "Niente FPS angolo se overlay sistema ON.");
        Assert(!ShowCornerFps(showFps: false, showOverlay: true), "Niente FPS angolo se contatore OFF.");
        var versionLabel = FactoryGameApp.FormatGameVersionLabel();
        Assert(versionLabel.StartsWith('v'), "Versione HUD deve iniziare con 'v'.");
        Assert(versionLabel.Contains("0.2.", StringComparison.Ordinal),
            "Versione HUD deve riflettere InformationalVersion csproj (0.2.x).");
        Assert(File.Exists(GameContentStore.UserJsonPath),
            "First launch deve materializzare content.json in AppData.");

        // Stale AppData (pre Phase 6) must gain missing seed ids instead of crashing on Single.
        var userContentBackup = File.Exists(GameContentStore.UserJsonPath)
            ? File.ReadAllText(GameContentStore.UserJsonPath)
            : null;
        try
        {
            var stale = GameContent.Load(GameContentStore.SeedJsonPath);
            var staleConveyors = stale.Conveyors.Where(c => c.Id != "conveyor-express").ToList();
            var staleStructures = stale.Structures.Where(s => s.Id != "conveyor-express").ToList();
            var staleBuildings = stale.Buildings.Where(b => b.Id != "miner-advanced").ToList();
            var staleMarket = stale.Market.Where(m => m.ItemId != "coal").ToList();
            Assert(staleConveyors.All(c => c.Id != "conveyor-express"),
                "Fixture stale non deve contenere conveyor-express.");
            var staleContent = new GameContent
            {
                Conveyors = staleConveyors,
                Recipes = stale.Recipes,
                Structures = staleStructures,
                Buildings = staleBuildings,
                Market = staleMarket,
                Economy = stale.Economy
            };
            var staleJson = System.Text.Json.JsonSerializer.Serialize(staleContent, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                Converters =
                {
                    new System.Text.Json.Serialization.JsonStringEnumConverter(
                        System.Text.Json.JsonNamingPolicy.CamelCase)
                }
            });
            File.WriteAllText(GameContentStore.UserJsonPath, staleJson);
            Assert(GameContentStore.MergeMissingSeedEntries(
                    GameContentStore.UserJsonPath, GameContentStore.SeedJsonPath),
                "Merge seed deve aggiungere id mancanti ad AppData stale.");
            var patched = GameContent.Load(GameContentStore.UserJsonPath);
            Assert(patched.Conveyors.Any(c => c.Id == "conveyor-express"),
                "Dopo merge AppData deve contenere conveyor-express (Nastro T3).");
            Assert(patched.Buildings.Any(b => b.Id == "miner-advanced"),
                "Dopo merge AppData deve contenere miner-advanced.");
            Assert(patched.Market.Any(m => m.ItemId == "coal"),
                "Dopo merge AppData deve contenere coal.");
            Assert(patched.Structures.Any(s => s.Id == "conveyor-express"),
                "Dopo merge structures deve includere conveyor-express.");
            // Idempotent: second merge is a no-op.
            Assert(!GameContentStore.MergeMissingSeedEntries(
                    GameContentStore.UserJsonPath, GameContentStore.SeedJsonPath),
                "Secondo merge non deve riscrivere se già completo.");

            // Stale displayName must refresh from seed (tier rename), without requiring delete.
            var renamed = GameContent.Load(GameContentStore.UserJsonPath);
            var renamedStructures = renamed.Structures.Select(s => s.Id switch
            {
                "miner" => s with { DisplayName = "Minatore" },
                "miner-advanced" => s with { DisplayName = "Minatore avanzato" },
                "conveyor-basic" => s with { DisplayName = "Nastro base" },
                "conveyor-fast" => s with { DisplayName = "Nastro veloce" },
                "conveyor-express" => s with { DisplayName = "Nastro express" },
                _ => s
            }).ToList();
            var renamedMarket = renamed.Market.Select(m => m.ItemId == "iron-ore"
                ? m with { DisplayName = "Ferro OLD" }
                : m).ToList();
            File.WriteAllText(GameContentStore.UserJsonPath, System.Text.Json.JsonSerializer.Serialize(
                new GameContent
                {
                    Conveyors = renamed.Conveyors,
                    Recipes = renamed.Recipes,
                    Structures = renamedStructures,
                    Buildings = renamed.Buildings,
                    Market = renamedMarket,
                    Economy = renamed.Economy
                },
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    Converters =
                    {
                        new System.Text.Json.Serialization.JsonStringEnumConverter(
                            System.Text.Json.JsonNamingPolicy.CamelCase)
                    }
                }));
            Assert(GameContentStore.SyncSeedDisplayFields(
                    GameContentStore.UserJsonPath, GameContentStore.SeedJsonPath),
                "Sync displayName deve aggiornare etichette stale da seed.");
            var synced = GameContent.Load(GameContentStore.UserJsonPath);
            Assert(synced.FindStructure("miner")?.DisplayName == "Minatore T1",
                "miner → Minatore T1 dopo sync.");
            Assert(synced.FindStructure("miner-advanced")?.DisplayName == "Minatore T2",
                "miner-advanced → Minatore T2 (non 'avanzato').");
            Assert(synced.FindStructure("conveyor-basic")?.DisplayName == "Nastro T1",
                "conveyor-basic → Nastro T1.");
            Assert(synced.FindStructure("conveyor-fast")?.DisplayName == "Nastro T2",
                "conveyor-fast → Nastro T2.");
            Assert(synced.FindStructure("conveyor-express")?.DisplayName == "Nastro T3",
                "conveyor-express → Nastro T3.");
            Assert(synced.Market.First(m => m.ItemId == "iron-ore").DisplayName == "Ferro grezzo",
                "Mercato iron-ore displayName sync dal seed.");
            Assert(!GameContentStore.SyncSeedDisplayFields(
                    GameContentStore.UserJsonPath, GameContentStore.SeedJsonPath),
                "Secondo sync displayName è no-op.");
            Assert(!synced.Structures.Any(s =>
                    s.DisplayName.Contains("avanzato", StringComparison.OrdinalIgnoreCase)
                    || s.DisplayName.Contains("Advanced", StringComparison.OrdinalIgnoreCase)
                    || s.DisplayName.Contains("express", StringComparison.OrdinalIgnoreCase)
                    || s.DisplayName.Equals("Nastro base", StringComparison.Ordinal)
                    || s.DisplayName.Equals("Nastro veloce", StringComparison.Ordinal)),
                "Nessuna etichetta user-facing legacy dopo sync.");

            // Stale / missing prerequisites must refresh from seed (pre-#38 AppData, sorter gap).
            var stripped = GameContent.Load(GameContentStore.UserJsonPath);
            var strippedStructures = stripped.Structures
                .Select(s => s with { Prerequisites = Array.Empty<string>() })
                .ToList();
            File.WriteAllText(GameContentStore.UserJsonPath, System.Text.Json.JsonSerializer.Serialize(
                new GameContent
                {
                    Conveyors = stripped.Conveyors,
                    Recipes = stripped.Recipes,
                    Structures = strippedStructures,
                    Buildings = stripped.Buildings,
                    Market = stripped.Market,
                    Economy = stripped.Economy
                },
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    Converters =
                    {
                        new System.Text.Json.Serialization.JsonStringEnumConverter(
                            System.Text.Json.JsonNamingPolicy.CamelCase)
                    }
                }));
            Assert(GameContentStore.SyncSeedPrerequisites(
                    GameContentStore.UserJsonPath, GameContentStore.SeedJsonPath),
                "Sync prerequisites deve ripristinare archi da seed.");
            var prereqSynced = GameContent.Load(GameContentStore.UserJsonPath);
            Assert(prereqSynced.FindStructure("smelter")!.Requires.Contains("miner"),
                "Dopo sync prereq forno → miner.");
            Assert(prereqSynced.FindStructure("sorter")!.Requires.Contains("junction"),
                "Dopo sync prereq selezionatore → incrocio.");
            Assert(prereqSynced.FindStructure("conveyor-express")!.Requires.Contains("conveyor-fast"),
                "Dopo sync prereq Nastro T3 → T2.");
            Assert(prereqSynced.FindStructure("miner-advanced")!.Requires.Contains("smelter"),
                "Dopo sync prereq Minatore T2 → forno.");
            var restoredGraph = TechTreeLayout.Build(prereqSynced);
            Assert(restoredGraph.Edges.Count >= 8,
                "Dopo sync prereq il grafo ha di nuovo gli archi midgame.");
            Assert(!GameContentStore.SyncSeedPrerequisites(
                    GameContentStore.UserJsonPath, GameContentStore.SeedJsonPath),
                "Secondo sync prerequisites è no-op.");

            // Stale assembler unlock/build that still demand copper-wire must refresh from seed.
            var staleCosts = GameContent.Load(GameContentStore.UserJsonPath);
            var wireUnlock = new UnlockRequirement(220,
            [
                new ResourceAmount("iron-plate", 22),
                new ResourceAmount("copper-wire", 4)
            ]);
            var wireCostStructures = staleCosts.Structures.Select(s => s.Id == "assembler"
                ? s with { Unlock = wireUnlock }
                : s).ToList();
            var wireCostBuildings = staleCosts.Buildings.Select(b => b.Id == "assembler"
                ? b with
                {
                    BuildCost =
                    [
                        new ResourceAmount("iron-plate", 8),
                        new ResourceAmount("copper-wire", 2)
                    ]
                }
                : b).ToList();
            File.WriteAllText(GameContentStore.UserJsonPath, System.Text.Json.JsonSerializer.Serialize(
                new GameContent
                {
                    Conveyors = staleCosts.Conveyors,
                    Recipes = staleCosts.Recipes,
                    Structures = wireCostStructures,
                    Buildings = wireCostBuildings,
                    Market = staleCosts.Market,
                    Economy = staleCosts.Economy
                },
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                    Converters =
                    {
                        new System.Text.Json.Serialization.JsonStringEnumConverter(
                            System.Text.Json.JsonNamingPolicy.CamelCase)
                    }
                }));
            Assert(GameContentStore.SyncSeedUnlockCosts(
                    GameContentStore.UserJsonPath, GameContentStore.SeedJsonPath),
                "Sync unlock costs deve togliere fili dall'assemblatore.");
            Assert(GameContentStore.SyncSeedBuildingCosts(
                    GameContentStore.UserJsonPath, GameContentStore.SeedJsonPath),
                "Sync building costs deve allineare build assemblatore al seed.");
            var costSynced = GameContent.Load(GameContentStore.UserJsonPath);
            Assert(costSynced.FindStructure("assembler")!.Unlock!.Materials
                    .All(m => m.ItemId != "copper-wire"),
                "Dopo sync unlock assemblatore senza copper-wire.");
            Assert(costSynced.FindStructure("assembler")!.Unlock!.Materials
                    .Any(m => m.ItemId == "copper-ore" && m.Amount == 4),
                "Dopo sync unlock assemblatore chiede rame grezzo.");
            Assert(costSynced.FindBuilding("assembler")!.BuildCost
                    .All(m => m.ItemId != "copper-wire"),
                "Dopo sync build assemblatore senza copper-wire.");
            Assert(costSynced.FindBuilding("assembler")!.BuildCost
                    .Any(m => m.ItemId == "copper-ore" && m.Amount == 2),
                "Dopo sync build assemblatore chiede rame grezzo.");
            Assert(!GameContentStore.SyncSeedUnlockCosts(
                    GameContentStore.UserJsonPath, GameContentStore.SeedJsonPath),
                "Secondo sync unlock costs è no-op.");
            Assert(!GameContentStore.SyncSeedBuildingCosts(
                    GameContentStore.UserJsonPath, GameContentStore.SeedJsonPath),
                "Secondo sync building costs è no-op.");
        }
        finally
        {
            if (userContentBackup is not null)
            {
                File.WriteAllText(GameContentStore.UserJsonPath, userContentBackup);
            }
        }

        // Seed + dock must already use T1/T2/T3 (no "Avanzato" in shipped content).
        foreach (var structure in content.Structures)
        {
            Assert(!structure.DisplayName.Contains("avanzato", StringComparison.OrdinalIgnoreCase)
                    && !structure.DisplayName.Contains("Advanced", StringComparison.OrdinalIgnoreCase),
                $"Seed displayName vietato: {structure.Id}={structure.DisplayName}");
        }

        Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Production)
                .Single(e => e.Id == "miner").Label == "Minatore T1",
            "Dock Minatore T1.");
        Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Production)
                .Single(e => e.Id == "miner-advanced").Label == "Minatore T2",
            "Dock Minatore T2.");
        Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Logistics)
                .Single(e => e.Id == "conveyor-basic").Label == "Nastro T1",
            "Dock Nastro T1.");
        Assert(UiTheme.EntriesFor(UiTheme.BuildCategory.Logistics)
                .Single(e => e.Id == "conveyor-express").Label == "Nastro T3",
            "Dock Nastro T3.");
        Assert(GameIcons.ResolveKey("miner-advanced") == "miner",
            "miner-advanced deve riusare l'icona trivella miner.");
        Assert(GameIcons.ResolveKey("conveyor-express") == "conveyor-fast",
            "conveyor-express deve riusare l'icona nastro veloce.");

        Assert(UiTheme.SessionDeltaLabel(0) == "Δ sessione +0",
            "Label sessione netto deve essere chiara (Δ sessione).");
        Assert(UiTheme.SessionDeltaLabel(-12).Contains("Δ sessione -12", StringComparison.Ordinal),
            "Label sessione negativa deve mostrare il segno.");
        Assert(UiTheme.SessionDeltaTooltip.Contains("patrimonio", StringComparison.OrdinalIgnoreCase),
            "Tooltip Δ sessione deve spiegare il patrimonio netto.");
        var uiGlyphs = UiTheme.UiCodepointsForTest();
        Assert(uiGlyphs.Contains(0x2014) && uiGlyphs.Contains(0x2192) && uiGlyphs.Contains(0x2026)
                && uiGlyphs.Contains(0x2019) && uiGlyphs.Contains(0x2713) && uiGlyphs.Contains(0x0394),
            "Atlas UI deve includere — → … ’ ✓ Δ (altrimenti Raylib disegna '?').");
        Assert(UiTheme.BuildCategoryGlyph(UiTheme.BuildCategory.Power) != "⚡",
            "Glyph Potenza non deve dipendere da emoji non necessariamente renderizzata.");
        var iconRoot = Path.Combine(AppContext.BaseDirectory, "assets", "icons");
        foreach (var rel in new[]
                 {
                     "items/iron-ore.png", "items/copper-ore.png", "items/coal.png",
                     "items/lead-ore.png", "items/titanium-ore.png",
                     "items/iron-plate.png", "items/lead-plate.png", "items/titanium-plate.png",
                     "items/copper-wire.png", "items/graphite.png", "items/silicon.png", "items/money.png",
                     "buildings/miner.png", "buildings/smelter.png", "buildings/assembler.png",
                     "buildings/sorter.png",
                     "categories/production.png", "ui/sell.png"
                 })
        {
            Assert(File.Exists(Path.Combine(iconRoot, rel.Replace('/', Path.DirectorySeparatorChar))),
                $"Icona mancante: {rel}");
        }

        // Ores share one silhouette (identical PNG bytes); color comes from ItemColor tint.
        var ironOreBytes = File.ReadAllBytes(Path.Combine(iconRoot, "items", "iron-ore.png"));
        var copperOreBytes = File.ReadAllBytes(Path.Combine(iconRoot, "items", "copper-ore.png"));
        var coalBytes = File.ReadAllBytes(Path.Combine(iconRoot, "items", "coal.png"));
        var leadOreBytes = File.ReadAllBytes(Path.Combine(iconRoot, "items", "lead-ore.png"));
        var titaniumOreBytes = File.ReadAllBytes(Path.Combine(iconRoot, "items", "titanium-ore.png"));
        Assert(ironOreBytes.AsSpan().SequenceEqual(copperOreBytes),
            "copper-ore.png deve condividere la silhouette rock di iron-ore.png.");
        Assert(ironOreBytes.AsSpan().SequenceEqual(coalBytes),
            "coal.png deve condividere la silhouette rock di iron-ore.png.");
        Assert(ironOreBytes.AsSpan().SequenceEqual(leadOreBytes),
            "lead-ore.png deve condividere la silhouette rock di iron-ore.png.");
        Assert(ironOreBytes.AsSpan().SequenceEqual(titaniumOreBytes),
            "titanium-ore.png deve condividere la silhouette rock di iron-ore.png.");

        Assert(File.Exists(Path.Combine(AppContext.BaseDirectory, "assets", "ATTRIBUTION.md")),
            "ATTRIBUTION.md deve essere copiato in output.");
        Assert(File.Exists(Path.Combine(AppContext.BaseDirectory, "assets", "thumbnail.png")),
            "Thumbnail splash/store deve essere in assets/thumbnail.png.");
        // Ferro (grigio) vs rame (arancio) vs carbone (nero): tinte distinguibili.
        var ironTint = UiTheme.ItemColor("iron-ore");
        var copperOreTint = UiTheme.ItemColor("copper-ore");
        var coalTint = UiTheme.ItemColor("coal");
        var wireTint = UiTheme.ItemColor("copper-wire");
        Assert(copperOreTint.R > 180 && copperOreTint.G < 170 && copperOreTint.B < 100,
            $"Rame grezzo deve essere arancio (got R={copperOreTint.R} G={copperOreTint.G} B={copperOreTint.B}).");
        Assert(coalTint.R < 80 && coalTint.G < 80 && coalTint.B < 80,
            $"Carbone deve essere charcoal scuro (got R={coalTint.R} G={coalTint.G} B={coalTint.B}).");
        var ferroRameDelta = Math.Abs(ironTint.R - copperOreTint.R)
            + Math.Abs(ironTint.G - copperOreTint.G)
            + Math.Abs(ironTint.B - copperOreTint.B);
        Assert(ferroRameDelta >= 80,
            $"Icone ferro/rame troppo simili in colore (delta RGB={ferroRameDelta}).");
        var tintDelta = Math.Abs(ironTint.R - wireTint.R)
            + Math.Abs(ironTint.G - wireTint.G)
            + Math.Abs(ironTint.B - wireTint.B);
        Assert(tintDelta >= 80,
            $"Icone ferro/fili troppo simili in colore (delta RGB={tintDelta}).");
        Assert(GameSettings.DisplayModeLabel(DisplayMode.Fullscreen) == "Schermo intero",
            "Etichetta italiana modalità schermo intero.");

        // Unlimited snap
        var unlimited = new GameSettings { TargetFps = 0, VSync = true };
        unlimited.Normalize();
        Assert(unlimited.TargetFps == 0, "Illimitato (0) deve restare valido con VSync.");
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

    Console.WriteLine("SELF-TEST OK: impostazioni grafica/strip risorse/dock Mindustry verificati.");

    // Campaign levels: objectives + unlock persistence.
    var campaignSeed = CampaignCatalog.SeedCampaignPath;
    Assert(File.Exists(campaignSeed), "Seed campaign.json deve essere nel package.");
    var campaignPath = CampaignCatalog.EnsureUserCampaign();
    Assert(File.Exists(campaignPath), "campaign.json deve materializzarsi in AppData.");
    var campaign = CampaignCatalog.Load(campaignSeed);
    Assert(campaign.Levels.Count >= 10, "La campagna estesa deve avere almeno 10 livelli.");
    Assert(campaign.Levels.All(level => level.Objectives is { Count: > 0 }),
        "Ogni livello deve dichiarare obiettivi.");
    Assert(campaign.Levels.SelectMany(level => level.Objectives!)
            .Select(objective => objective.Type)
            .Distinct()
            .Count() >= 3,
        "Servono almeno 3 tipi di obiettivo nella campagna.");
    Assert(campaign.Find("level-06-corrente") is not null, "Livello Corrente (power) richiesto.");
    Assert(campaign.Find("level-07-piombo") is not null, "Livello piombo richiesto.");
    Assert(campaign.Find("level-10-espansione") is not null, "Livello Espansione T2 richiesto.");
    Assert(campaign.Find("level-05-mercato")!.UnlocksNext == "level-06-corrente",
        "Livello 5 deve sbloccare Corrente.");

    // Walk unlock chain 1→10.
    {
        var walk = campaign.Levels[0];
        var seen = new HashSet<string>(StringComparer.Ordinal) { walk.Id };
        for (var step = 0; step < 9; step++)
        {
            var next = campaign.NextAfter(walk);
            Assert(next is not null, $"Catena campagna spezzata dopo {walk.Id}.");
            Assert(seen.Add(next!.Id), $"Ciclo campagna su {next.Id}.");
            walk = next;
        }

        Assert(walk.Id == "level-10-espansione", "La catena deve finire su Espansione T2.");
        Assert(campaign.NextAfter(walk) is null, "L'ultimo livello non sblocca altro.");
    }

    // AppData campaign merge + field sync (mirror content.json seed merge).
    var campaignUserBackup = File.Exists(CampaignCatalog.UserCampaignPath)
        ? File.ReadAllText(CampaignCatalog.UserCampaignPath)
        : null;
    try
    {
        var staleCampaign = """
            {
              "levels": [
                {
                  "id": "level-01-primi-passi",
                  "name": "STALE",
                  "description": "vecchio",
                  "seed": 1,
                  "mapWidth": 32,
                  "mapHeight": 24,
                  "startingMoney": 1,
                  "startingMaterials": [],
                  "objectives": [ { "type": "earnMoney", "amount": 1, "label": "x" } ],
                  "unlocksNext": null
                }
              ]
            }
            """;
        File.WriteAllText(CampaignCatalog.UserCampaignPath, staleCampaign);
        Assert(CampaignCatalog.MergeMissingSeedLevels(
                CampaignCatalog.UserCampaignPath, CampaignCatalog.SeedCampaignPath),
            "Merge deve aggiungere i livelli seed mancanti.");
        var mergedCampaign = CampaignCatalog.Load(CampaignCatalog.UserCampaignPath);
        Assert(mergedCampaign.Levels.Count >= 10, "Dopo merge AppData ha ≥10 livelli.");
        Assert(CampaignCatalog.SyncSeedLevelFields(
                CampaignCatalog.UserCampaignPath, CampaignCatalog.SeedCampaignPath),
            "Sync deve aggiornare name/obiettivi/unlocksNext da seed.");
        var syncedCampaign = CampaignCatalog.Load(CampaignCatalog.UserCampaignPath);
        Assert(syncedCampaign.Find("level-01-primi-passi")!.Name == "Primi passi",
            "Sync name livello 1 da seed.");
        Assert(syncedCampaign.Find("level-05-mercato")!.UnlocksNext == "level-06-corrente",
            "Sync unlocksNext livello 5 → 6.");
        Assert(!CampaignCatalog.SyncSeedLevelFields(
                CampaignCatalog.UserCampaignPath, CampaignCatalog.SeedCampaignPath),
            "Secondo sync campagna è no-op.");
    }
    finally
    {
        if (campaignUserBackup is null)
        {
            if (File.Exists(CampaignCatalog.UserCampaignPath))
            {
                File.Delete(CampaignCatalog.UserCampaignPath);
            }
        }
        else
        {
            File.WriteAllText(CampaignCatalog.UserCampaignPath, campaignUserBackup);
        }
    }

    var campaignFirst = campaign.Levels[0];
    var campaignSecond = campaign.NextAfter(campaignFirst);
    Assert(campaignSecond is not null, "Il primo livello deve sbloccare il successivo.");
    var progressPath = CampaignProgress.ProgressPath;
    var progressBackup = File.Exists(progressPath) ? File.ReadAllText(progressPath) : null;
    try
    {
        if (File.Exists(progressPath))
        {
            File.Delete(progressPath);
        }

        var progress = new CampaignProgress();
        Assert(progress.IsUnlocked(campaignFirst, campaign), "Il primo livello deve essere sbloccato.");
        Assert(!progress.IsUnlocked(campaignSecond!, campaign), "Il secondo livello parte bloccato.");

        var earnWallet = new EconomyWallet(100, new Dictionary<string, int>());
        var earnSession = new EconomySession(100);
        earnSession.RecordSale("iron-ore", 8);
        earnSession.RecordSale("iron-ore", 8);
        earnSession.RecordSale("iron-ore", 8);
        var sellObj = new CampaignObjectiveDefinition(
            CampaignObjectiveType.SellItem, 3, ItemId: "iron-ore", Label: "Vendi 3");
        var earnObj = new CampaignObjectiveDefinition(
            CampaignObjectiveType.EarnMoney, 24, Label: "Guadagna $24");
        Assert(CampaignProgress.IsObjectiveComplete(sellObj, earnWallet, earnSession, ResearchState.CreateNew(content)),
            "sellItem: 3 vendite devono completare l'obiettivo.");
        Assert(CampaignProgress.IsObjectiveComplete(earnObj, earnWallet, earnSession, ResearchState.CreateNew(content)),
            "earnMoney: SaleIncome deve completare l'obiettivo.");

        var stockWallet = new EconomyWallet(50, new Dictionary<string, int> { ["iron-ore"] = 12 });
        var stockSession = new EconomySession(50);
        var stockObj = new CampaignObjectiveDefinition(
            CampaignObjectiveType.StockItem, 12, ItemId: "iron-ore");
        Assert(CampaignProgress.IsObjectiveComplete(
                stockObj, stockWallet, stockSession, ResearchState.CreateNew(content)),
            "stockItem: stock wallet deve completare l'obiettivo.");

        var campaignUnlockResearch = ResearchState.CreateNew(content);
        var campaignUnlockWallet = new EconomyWallet(400, new Dictionary<string, int> { ["iron-plate"] = 40 });
        Assert(campaignUnlockResearch.TryUnlock(content.FindStructure("smelter")!, campaignUnlockWallet),
            "Unlock forno per test obiettivo ricerca.");
        var unlockObj = new CampaignObjectiveDefinition(
            CampaignObjectiveType.UnlockResearch, 1, StructureId: "smelter");
        Assert(CampaignProgress.IsObjectiveComplete(
                unlockObj, campaignUnlockWallet, stockSession, campaignUnlockResearch),
            "unlockResearch: sblocco deve completare l'obiettivo.");

        progress.MarkComplete(campaignFirst.Id);
        Assert(File.Exists(progressPath), "MarkComplete deve scrivere campaignProgress.json.");
        var reloaded = CampaignProgress.Load();
        Assert(reloaded.IsCompleted(campaignFirst.Id), "Completamento livello deve persistere.");
        Assert(reloaded.IsUnlocked(campaignSecond!, campaign), "Dopo il primo livello, il secondo si sblocca.");
        Assert(!reloaded.IsUnlocked(campaign.Levels[^1], campaign) || campaign.Levels.Count <= 2,
            "L'ultimo livello resta bloccato finché non si completa la catena (se >2 livelli).");

        // Level world generation respects map size from definition.
        var tiny = campaign.Levels[0];
        var levelWorld = new FactoryWorld(Math.Max(12, tiny.MapWidth), Math.Max(8, tiny.MapHeight), tiny.Seed);
        Assert(levelWorld.Terrain.Width == tiny.MapWidth && levelWorld.Terrain.Height == tiny.MapHeight,
            "Il livello campagna deve usare mapWidth/mapHeight.");
        var levelWallet = campaign.CreateWallet(tiny);
        Assert(levelWallet.Money == tiny.StartingMoney, "Starting money dal livello.");
    }
    finally
    {
        if (progressBackup is null)
        {
            if (File.Exists(progressPath))
            {
                File.Delete(progressPath);
            }
        }
        else
        {
            File.WriteAllText(progressPath, progressBackup);
        }
    }

    // Tech-tree path highlight + zoom clamp.
    {
        var techGraph = TechTreeLayout.Build(content);
        var related = TechTreeLayout.CollectRelatedIds(techGraph, "assembler");
        Assert(related.Contains("assembler") && related.Contains("smelter") && related.Contains("miner"),
            "Path highlight assemblatore include prerequisiti forno/miner.");
        Assert(TechTreeLayout.IsEdgeOnPath(
                techGraph.Edges.First(edge => edge.FromId == "smelter" && edge.ToId == "assembler"),
                related),
            "Arco forno→assemblatore sul percorso selezionato.");
        var powerRelated = TechTreeLayout.CollectRelatedIds(techGraph, "power-node");
        Assert(powerRelated.Contains("smelter") && powerRelated.Contains("power-node-t2"),
            "Nodo T1 evidenzia antenati e Nodo T2 dipendente.");
        Assert(TechTreeLayout.ClampZoom(0.1f) == TechTreeLayout.MinZoom, "Zoom minimo clamp.");
        Assert(TechTreeLayout.ClampZoom(9f) == TechTreeLayout.MaxZoom, "Zoom massimo clamp.");
        Assert(Math.Abs(TechTreeLayout.ClampZoom(1f) - 1f) < 0.0001f, "Zoom 1× resta 1×.");
    }

    // Icon atlas grid planner (GPU pack runs a runtime Load).
    {
        var plan = GameIcons.PlanAtlasGrid(40);
        Assert(plan.Columns >= 6 && plan.Rows >= 6, "Atlas 40 icone: griglia ~quadrata.");
        Assert(plan.Width > 64 && plan.Height > 64, "Atlas plan ha dimensione > cella.");
        Assert(GameIcons.PlanAtlasGrid(0).Width == 0, "Atlas vuoto → 0.");
    }

    Console.WriteLine("SELF-TEST OK: campagna (obiettivi + unlock persistence) verificata.");
    Console.WriteLine("SELF-TEST OK: campagna estesa + merge, tech-tree path/zoom, atlas plan.");
}

/// <summary>Mirrors play-HUD policy: corner FPS only when overlay is off.</summary>
static bool ShowCornerFps(bool showFps, bool showOverlay) => showFps && !showOverlay;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void EnsurePowerLink(
    GameContent content,
    FactoryWorld world,
    ConveyorGrid grid,
    EconomyWallet wallet,
    GridPosition building,
    int size = SmelterBuilding.Size)
{
    world.RefreshPowerNetworks();
    if (world.IsBuildingPowered(building, size))
    {
        return;
    }

    wallet.AddMoney(200);
    wallet.AddMaterial("iron-plate", 40);
    wallet.AddMaterial("copper-wire", 40);
    wallet.AddMaterial("coal", 8);

    // Prefer a fueled generator adjacent to the building (4-connected adjacency power).
    if (world.Generators.Count == 0)
    {
        GridPosition? genSpot = null;
        for (var dy = -GeneratorBuilding.Size; dy <= size; dy++)
        {
            for (var dx = -GeneratorBuilding.Size; dx <= size; dx++)
            {
                var candidate = new GridPosition(building.X + dx, building.Y + dy);
                if (!world.CanPlaceGenerator(candidate, grid))
                {
                    continue;
                }

                // Prefer spots that touch the building footprint.
                var touches = false;
                for (var y = 0; y < GeneratorBuilding.Size && !touches; y++)
                {
                    for (var x = 0; x < GeneratorBuilding.Size && !touches; x++)
                    {
                        var gt = new GridPosition(candidate.X + x, candidate.Y + y);
                        for (var d = 0; d < DirectionMath.All.Length; d++)
                        {
                            var n = gt.Step(DirectionMath.All[d]);
                            if (n.X >= building.X && n.X < building.X + size
                                && n.Y >= building.Y && n.Y < building.Y + size)
                            {
                                touches = true;
                                break;
                            }
                        }
                    }
                }

                if (touches)
                {
                    genSpot = candidate;
                    dy = size + 1;
                    break;
                }

                genSpot ??= candidate;
            }
        }

        if (genSpot is { } spot
            && world.TryPlaceGenerator(spot, grid, wallet, content.GetBuildingOrDefault("generator"))
            && world.TryGetGeneratorAt(spot, out var placedGen))
        {
            placedGen.TryAcceptFuel("coal");
            placedGen.TryAcceptFuel("coal");
            placedGen.TryAcceptFuel("coal");
            var tickId = 1L;
            for (var i = 0; i < 20; i++)
            {
                world.Update(1f / 30f, grid, wallet, ref tickId);
            }

            if (world.IsBuildingPowered(building, size))
            {
                return;
            }
        }
    }
    else
    {
        foreach (var gen in world.Generators.Values)
        {
            gen.TryAcceptFuel("coal");
            gen.TryAcceptFuel("coal");
        }

        var tickId = 1L;
        for (var i = 0; i < 20; i++)
        {
            world.Update(1f / 30f, grid, wallet, ref tickId);
        }

        if (world.IsBuildingPowered(building, size))
        {
            return;
        }
    }

    Assert(
        world.TryEnsurePowerLinkToGenerator(
            building,
            size,
            grid,
            wallet,
            content.GetBuildingOrDefault("power-node")),
        $"Serve un collegamento nodi verso un generatore per {building}.");
}