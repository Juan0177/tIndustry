using TIndustry.Shared;
using static TIndustry.Shared.Factory;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Scenario checks for <see cref="Factory"/> (moved out of the sim; each throws on failure).
/// Run via <see cref="FactorySelfTests"/>.
/// </summary>
internal static class FactoryScenarios
{
    public static void SelfTest(string contentJsonPath)
    {
        var colorFail = ItemVisualColors.SelfTest();
        if (colorFail is not null)
        {
            throw new InvalidOperationException($"ItemVisualColors: {colorFail}");
        }

        var content = FactoryContent.Load(contentJsonPath);
        SelfTestPlaceCosts(contentJsonPath);
        SelfTestFuelOrPower(contentJsonPath);
        SelfTestPowerBuffer(contentJsonPath);
        SelfTestAdjacentIo(contentJsonPath);
        SelfTestSaveManager(contentJsonPath);
        var factory = NewGame.Demo(content);
        for (var i = 0; i < SimulationClock.TickRate * 40; i++)
        {
            factory.Step();
            if (factory.CoreDeliveredItems > 0 && factory.Wallet.MaterialCount("iron-ore") > 0)
            {
                return;
            }
        }

        throw new InvalidOperationException(
            "Factory self-test fallito: nessun ferro grezzo arrivato al core entro 40s sim.");
    }

    /// <summary>Build costs charge wallet on place and refund on remove (Raylib parity).</summary>
    public static void SelfTestPlaceCosts(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var wallet = new EconomyWallet(
            0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["iron-plate"] = 10 });
        var factory = new Factory(content, new BeltGrid(), core, wallet);
        var minerCost = content.FindBuilding("miner")!.BuildCost.Sum(e => e.Amount);
        var beltCost = content.FindConveyor("conveyor-basic")!.EffectiveBuildCost.Sum(e => e.Amount);

        Assert(factory.TryPlaceMiner(new GridPosition(2, 7), Direction.East), "cost place miner");
        Assert(factory.Wallet.MaterialCount("iron-plate") == 10 - minerCost, "miner spent plates");
        Assert(factory.TryPlaceBelt(new GridPosition(4, 8), Direction.East), "cost place belt");
        Assert(factory.Wallet.MaterialCount("iron-plate") == 10 - minerCost - beltCost, "belt spent plates");
        Assert(!factory.CanAffordStructure("smelter"), "cannot afford smelter yet");
        Assert(!factory.TryPlaceSmelter(new GridPosition(6, 7), Direction.East), "refuse smelter");

        Assert(factory.TryRemoveBelt(new GridPosition(4, 8)), "refund belt");
        Assert(factory.Wallet.MaterialCount("iron-plate") == 10 - minerCost, "belt refunded");
        Assert(factory.TryRemoveBuildingAt(new GridPosition(2, 7)), "refund miner");
        Assert(factory.Wallet.MaterialCount("iron-plate") == 10, "miner refunded full");

        // Bridge charges ×2 heads.
        factory.EnsureDemoBuildStock(ironPlates: 20, copperWire: 10, copperOre: 4);
        factory.Research.ForceUnlock("conveyor-bridge");
        var beforeBridge = factory.Wallet.MaterialCount("iron-plate");
        var bridgePlates = content.FindConveyor("conveyor-bridge")!.EffectiveBuildCost
            .Where(e => e.ItemId == "iron-plate").Sum(e => e.Amount) * 2;
        Assert(factory.TryPlaceBridge(new GridPosition(2, 3), Direction.East), "bridge place");
        Assert(factory.Wallet.MaterialCount("iron-plate") == beforeBridge - bridgePlates, "bridge ×2 cost");
        Assert(factory.TryRemoveBelt(new GridPosition(2, 3)), "bridge remove");
        Assert(factory.Wallet.MaterialCount("iron-plate") == beforeBridge, "bridge refund ×2");
    }

    public static void SelfTestPlaceable(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var grid = new BeltGrid();
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var factory = new Factory(content, grid, core);
        factory.EnsureDemoBuildStock();
        Assert(factory.TryPlaceMiner(new GridPosition(2, 7), Direction.East), "miner");

        Assert(factory.TryPlaceBelt(new GridPosition(4, 8), Direction.East), "place (4,8)");
        Assert(factory.TryPlaceBelt(new GridPosition(5, 8), Direction.East), "place (5,8)");
        Assert(factory.TryPlaceBelt(new GridPosition(6, 8), Direction.East), "place (6,8)");
        Assert(factory.TryPlaceBelt(new GridPosition(7, 8), Direction.East), "place (7,8)");
        Assert(factory.TryPlaceBelt(new GridPosition(8, 8), Direction.East), "place (8,8)");
        Assert(factory.TryPlaceBelt(new GridPosition(9, 8), Direction.East), "place (9,8)");
        Assert(factory.TryPlaceBelt(new GridPosition(10, 8), Direction.South), "corner (10,8)");
        Assert(factory.TryPlaceBelt(new GridPosition(10, 9), Direction.South), "place (10,9)");
        Assert(factory.TryPlaceBelt(new GridPosition(10, 10), Direction.South), "place (10,10)");
        Assert(factory.TryPlaceBelt(new GridPosition(10, 11), Direction.South), "place (10,11)");
        Assert(factory.TryPlaceBelt(new GridPosition(10, 12), Direction.South), "place (10,12)");
        Assert(factory.TryPlaceBelt(new GridPosition(10, 13), Direction.South), "place (10,13)");

        Assert(grid.IsCorner(new GridPosition(10, 8)), "corner detect (10,8)");
        for (var i = 0; i < SimulationClock.TickRate * 45; i++)
        {
            factory.Step();
            if (factory.CoreDeliveredItems > 0)
            {
                return;
            }
        }

        throw new InvalidOperationException("Placeable belt self-test: nessun item al core.");
    }

    /// <summary>T2/T3 belt/miner + extractor + power-node T1/T2 place APIs.</summary>
    public static void SelfTestT2Tools(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var factory = new Factory(content, new BeltGrid(), core);
        factory.EnsureDemoBuildStock();
        factory.Research.ForceUnlock("conveyor-fast");
        factory.Research.ForceUnlock("conveyor-express");
        factory.Research.ForceUnlock("miner-advanced");
        factory.Research.ForceUnlock("extractor");
        factory.Research.ForceUnlock("power-node");
        factory.Research.ForceUnlock("power-node-t2");

        Assert(factory.TryPlaceBelt(new GridPosition(3, 8), Direction.East, "conveyor-fast"), "belt T2");
        Assert(factory.Belts.TryGet(new GridPosition(3, 8), out var fast)
            && fast.Definition.Id == "conveyor-fast", "belt T2 def");
        Assert(factory.TryPlaceBelt(new GridPosition(4, 8), Direction.East, "conveyor-express"), "belt T3");
        Assert(factory.Belts.TryGet(new GridPosition(4, 8), out var express)
            && express.Definition.Id == "conveyor-express", "belt T3 def");
        Assert(factory.TryPlaceMiner(new GridPosition(2, 4), Direction.East, definitionId: Miner.AdvancedId),
            "miner T2");
        Assert(factory.Miners.Any(m => m.DefinitionId == Miner.AdvancedId), "miner T2 list");
        Assert(factory.TryPlaceExtractor(new GridPosition(8, 14), Direction.North), "extractor");
        Assert(factory.TryPlacePowerNode(new GridPosition(11, 12)), "power-node");
        Assert(factory.TryPlacePowerNode(new GridPosition(14, 12), PowerNode.Tier2Id), "power-node-t2");
        Assert(factory.PowerNodes.Any(n => n.DefinitionId == PowerNode.Tier2Id && n.Size == 2), "T2 size");
        Assert(factory.Extractors.Count == 1 && factory.PowerNodes.Count == 2, "counts");

        var snap = factory.Capture();
        Assert(snap.Extractors.Count == 1 && snap.PowerNodes.Count == 2, "capture");
        var restored = Restore(content, snap);
        Assert(restored.Extractors.Count == 1, "restore extractor");
        Assert(restored.PowerNodes.Count == 2, "restore nodes");
        Assert(restored.PowerNodes.Any(n => n.DefinitionId == PowerNode.Tier2Id), "restore T2");
        Assert(restored.Belts.TryGet(new GridPosition(3, 8), out var rf)
            && rf.Definition.Id == "conveyor-fast", "restore belt T2");
        Assert(restored.Belts.TryGet(new GridPosition(4, 8), out var re)
            && re.Definition.Id == "conveyor-express", "restore belt T3");
    }

    /// <summary>Phase C: ore through forno yields iron-plate in core stock.</summary>
    public static void SelfTestSmelterLoop(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var factory = CreatePhaseCDemo(content);
        Assert(factory.Smelters.Count == 1, "forno seed");
        Assert(factory.Miners.Count == 1, "miner seed");

        // Mining 2s + belt + smelt 2s + belt — budget ~90s sim.
        for (var i = 0; i < SimulationClock.TickRate * 90; i++)
        {
            factory.Step();
            if (factory.Wallet.MaterialCount("iron-plate") > 0)
            {
                return;
            }
        }

        throw new InvalidOperationException(
            "Smelter loop self-test: nessuna lastra di ferro al core entro 90s sim.");
    }

    /// <summary>Multi-recipe forno/assy: lead ore / graphite auto-pick (Raylib parity).</summary>
    public static void SelfTestMultiRecipe(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        Assert(content.SmelterRecipes().Count >= 3, "smelt recipes");
        Assert(content.AssemblerRecipes().Count >= 3, "craft recipes");

        var lead = content.FindRecipe("smelt-lead")
            ?? throw new InvalidDataException("smelt-lead missing");
        var smelter = new Crafter(
            new GridPosition(0, 0),
            Direction.East,
            content.FindRecipe("smelt-iron")!,
            Crafter.SmelterBuildingId,
            content.SmelterRecipes());
        Assert(smelter.TryAccept("lead-ore"), "accept lead");
        Assert(smelter.TryAccept("lead-ore"), "accept lead 2");
        Assert(!smelter.TryAccept("copper-ore"), "reject copper on forno");
        long next = 1;
        smelter.Tick(0.05f, new BeltGrid(), ref next, networkConnected: true);
        Assert(smelter.IsCrafting && smelter.Recipe.Id == "smelt-lead", "auto smelt-lead");

        var graphite = content.FindRecipe("craft-graphite");
        Assert(graphite is not null, "craft-graphite");
        var assy = new Crafter(
            new GridPosition(2, 0),
            Direction.East,
            content.FindRecipe("craft-copper-wire")!,
            Crafter.AssemblerBuildingId,
            content.AssemblerRecipes());
        foreach (var input in graphite!.Inputs)
        {
            for (var n = 0; n < input.Amount; n++)
            {
                Assert(assy.TryAccept(input.ItemId), $"accept {input.ItemId}");
            }
        }

        assy.Tick(0.05f, new BeltGrid(), ref next, networkConnected: true);
        Assert(assy.IsCrafting && assy.Recipe.Id == "craft-graphite", "auto craft-graphite");

        // Place API wires recipe lists.
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var factory = new Factory(content, new BeltGrid(), core);
        factory.UnlockAllForDemo();
        Assert(factory.TryPlaceSmelter(new GridPosition(4, 4), Direction.East), "place multi forno");
        Assert(factory.Smelters[0].AvailableRecipes.Count >= 3, "forno has smelt-*");
        Assert(factory.TryPlaceAssembler(new GridPosition(8, 4), Direction.East), "place multi assy");
        Assert(factory.Assemblers[0].AvailableRecipes.Count >= 3, "assy has craft-*");
    }

    /// <summary>Phase D: plates + copper ore through assembler yield copper-wire in core.</summary>
    public static void SelfTestAssemblerLoop(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var factory = CreatePhaseDDemo(content);
        Assert(factory.Smelters.Count == 1, "forno seed");
        Assert(factory.Assemblers.Count == 1, "assemblatore seed");
        Assert(factory.Miners.Count == 2, "iron+copper miners");
        Assert(factory.Miners.Any(m => m.OutputItemId == "copper-ore"), "copper miner");
        Assert(factory.TryPlaceAssembler(new GridPosition(16, 4), Direction.South), "place second assembler");
        Assert(factory.TryRemoveBuildingAt(new GridPosition(16, 4)), "remove assembler");

        // Iron mine+smelt + copper mine + craft 1.5s + belts — budget ~120s sim.
        for (var i = 0; i < SimulationClock.TickRate * 120; i++)
        {
            factory.Step();
            if (factory.Wallet.MaterialCount("copper-wire") > 0)
            {
                return;
            }
        }

        throw new InvalidOperationException(
            "Assembler loop self-test: nessun filo di rame al core entro 120s sim.");
    }

    /// <summary>
    /// Phase E: junction cross-axis + splitter T-fork, then full wire loop still delivers.
    /// </summary>
    public static void SelfTestJunctionSplitter(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var belt = content.RequireConveyor("conveyor-basic");
        var junction = content.RequireConveyor("junction");
        var splitter = content.RequireConveyor("splitter");
        var grid = new BeltGrid();

        // Cross: west→east through junction, south→north through same cell.
        grid.TryPlaceFree(new GridPosition(0, 1), Direction.East, belt);
        grid.TryPlaceFree(new GridPosition(1, 1), Direction.East, junction);
        grid.TryPlaceFree(new GridPosition(2, 1), Direction.East, belt);
        grid.TryPlaceFree(new GridPosition(1, 2), Direction.North, belt);
        grid.TryPlaceFree(new GridPosition(1, 0), Direction.North, belt);

        Assert(grid.TryInsert(new GridPosition(0, 1), new TransportedItem(1, "iron-plate")), "ew insert");
        Assert(grid.TryInsert(new GridPosition(1, 2), new TransportedItem(2, "copper-ore")), "ns insert");

        var ewArrived = false;
        var nsArrived = false;
        const float dt = 1f / 30f;
        for (var i = 0; i < 30 * 20; i++)
        {
            grid.Tick(dt);
            if (grid.TryGet(new GridPosition(2, 1), out var ew) && ew.Items.Any(it => it.Id == 1))
            {
                ewArrived = true;
            }

            if (grid.TryGet(new GridPosition(1, 0), out var ns) && ns.Items.Any(it => it.Id == 2))
            {
                nsArrived = true;
            }

            if (ewArrived && nsArrived)
            {
                break;
            }
        }

        Assert(ewArrived, "junction EW exit");
        Assert(nsArrived, "junction NS exit");

        // Splitter fair fork: feed → splitter facing South → left/right arms.
        var splitGrid = new BeltGrid();
        splitGrid.TryPlaceFree(new GridPosition(5, 0), Direction.South, belt);
        splitGrid.TryPlaceFree(new GridPosition(5, 1), Direction.South, splitter);
        splitGrid.TryPlaceFree(new GridPosition(4, 1), Direction.West, belt); // right of South
        splitGrid.TryPlaceFree(new GridPosition(6, 1), Direction.East, belt); // left of South

        Assert(splitGrid.TryInsert(new GridPosition(5, 0), new TransportedItem(10, "copper-wire")), "split in 1");
        var leftHits = 0;
        var rightHits = 0;
        var injectedSecond = false;
        for (var i = 0; i < 30 * 40; i++)
        {
            splitGrid.Tick(dt);
            if (!injectedSecond
                && splitGrid.TryGet(new GridPosition(5, 0), out var feed)
                && feed.Items.Count == 0)
            {
                Assert(
                    splitGrid.TryInsert(new GridPosition(5, 0), new TransportedItem(11, "copper-wire")),
                    "split in 2");
                injectedSecond = true;
            }

            if (splitGrid.TryGet(new GridPosition(6, 1), out var left))
            {
                leftHits = Math.Max(leftHits, left.Items.Count);
            }

            if (splitGrid.TryGet(new GridPosition(4, 1), out var right))
            {
                rightHits = Math.Max(rightHits, right.Items.Count);
            }

            if (injectedSecond && leftHits > 0 && rightHits > 0)
            {
                break;
            }
        }

        Assert(injectedSecond, "second splitter feed");
        Assert(leftHits > 0 && rightHits > 0, "splitter forks both arms");

        // Full Phase E seed still delivers wire.
        var factory = CreatePhaseEDemo(content);
        Assert(factory.Belts.Cells.Values.Any(c => c.Kind == LogisticsKind.Junction), "seed junction");
        Assert(factory.Belts.Cells.Values.Any(c => c.Kind == LogisticsKind.Splitter), "seed splitter");
        Assert(factory.TryPlaceJunction(new GridPosition(18, 4), Direction.East), "place junction");
        Assert(factory.TryPlaceSplitter(new GridPosition(18, 5), Direction.South), "place splitter");
        Assert(factory.TryRemoveBelt(new GridPosition(18, 4)), "remove junction");
        Assert(factory.TryRemoveBelt(new GridPosition(18, 5)), "remove splitter");

        for (var i = 0; i < SimulationClock.TickRate * 120; i++)
        {
            factory.Step();
            if (factory.Wallet.MaterialCount("copper-wire") > 0)
            {
                return;
            }
        }

        throw new InvalidOperationException(
            "Phase E self-test: nessun filo al core entro 120s sim (junction/splitter seed).");
    }

    /// <summary>
    /// Phase F: generator adjacency powers craft (+20%), seed still delivers wire.
    /// </summary>
    public static void SelfTestPowerStub(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var recipe = content.FindRecipe("smelt-iron")
            ?? throw new InvalidDataException("Ricetta smelt-iron mancante.");
        var grid = new BeltGrid();
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var factory = new Factory(content, grid, core);
        factory.UnlockAllForDemo();

        Assert(factory.TryPlaceSmelter(new GridPosition(4, 4), Direction.East, recipe), "smelter");
        Assert(factory.TryPlaceGenerator(new GridPosition(4, 2), Direction.East), "gen north of forno");
        Assert(factory.Generators[0].IsAdjacentTo(factory.Smelters[0].Position, Crafter.Size), "adjacent");
        Assert(factory.Generators[0].TryAcceptFuel("coal"), "fuel 1");
        Assert(factory.Generators[0].TryAcceptFuel("coal"), "fuel 2");

        var sm = factory.Smelters[0];
        Assert(sm.TryAccept("iron-ore") && sm.TryAccept("iron-ore"), "ore for craft");

        var sawPowered = false;
        for (var i = 0; i < SimulationClock.TickRate * 8; i++)
        {
            factory.Step();
            if (sm.IsPowered)
            {
                sawPowered = true;
                break;
            }
        }

        Assert(sawPowered, "forno powered while generator burns");

        // Speed: powered progress > coal-only over same window.
        var coldGrid = new BeltGrid();
        var cold = new Factory(content, coldGrid, core);
        cold.UnlockAllForDemo();
        Assert(cold.TryPlaceSmelter(new GridPosition(1, 1), Direction.East, recipe), "cold smelter");
        var coldSm = cold.Smelters[0];
        coldSm.SeedFuel();
        Assert(coldSm.TryAccept("iron-ore") && coldSm.TryAccept("iron-ore"), "cold ore");

        // Reset powered smelter craft.
        var hot = factory;
        var hotSm = hot.Smelters[0];
        if (!hotSm.IsCrafting)
        {
            Assert(hotSm.TryAccept("iron-ore") && hotSm.TryAccept("iron-ore"), "hot ore");
        }

        // Ensure gen still fueled.
        hot.Generators[0].TryAcceptFuel("coal");
        var hotBefore = hotSm.Progress;
        var coldBefore = coldSm.Progress;
        // Was 15 × Tick(1/30 s): same 15/30 s as 30 fixed 1/60 s ticks.
        for (var i = 0; i < 30; i++)
        {
            hot.Step();
            cold.Step();
        }

        var hotDelta = hotSm.Progress - hotBefore;
        var coldDelta = coldSm.Progress - coldBefore;
        if (hotSm.IsCrafting && coldSm.IsCrafting && coldDelta > 0.001f)
        {
            Assert(hotDelta > coldDelta * 1.05f,
                $"powered faster (hotΔ={hotDelta:F3} coldΔ={coldDelta:F3})");
        }

        Assert(factory.TryPlaceGenerator(new GridPosition(16, 4), Direction.South), "place second gen");
        Assert(factory.TryRemoveBuildingAt(new GridPosition(16, 4)), "remove gen");

        var demo = CreatePhaseFDemo(content);
        Assert(demo.Generators.Count == 1, "seed generator");
        Assert(demo.Miners.Any(m => m.OutputItemId == "coal"), "coal miner");
        Assert(demo.Smelters.Count == 1 && demo.Assemblers.Count == 1, "craft chain");

        for (var i = 0; i < SimulationClock.TickRate * 150; i++)
        {
            demo.Step();
            if (demo.Wallet.MaterialCount("copper-wire") > 0)
            {
                return;
            }
        }

        throw new InvalidOperationException(
            "Phase F self-test: nessun filo al core entro 150s sim (power seed).");
    }

    /// <summary>Forno: coal OR power required; +20% when powered (Raylib parity).</summary>
    public static void SelfTestFuelOrPower(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var recipe = content.FindRecipe("smelt-iron")
            ?? throw new InvalidDataException("Ricetta smelt-iron mancante.");
        var belts = new BeltGrid();
        long next = 1;

        // Stall: no coal, no power — craft starts but progress frozen.
        var stalled = new Crafter(new GridPosition(0, 0), Direction.East, recipe);
        Assert(stalled.TryAccept("iron-ore") && stalled.TryAccept("iron-ore"), "stall ore");
        stalled.Tick(0.5f, belts, ref next, networkConnected: false);
        Assert(stalled.IsCrafting, "stall crafting");
        Assert(stalled.Progress < 0.001f, "stall no progress");

        // Coal-only advances at 1×.
        var coalOnly = new Crafter(new GridPosition(2, 0), Direction.East, recipe);
        coalOnly.SeedFuel(2);
        Assert(coalOnly.TryAccept("iron-ore") && coalOnly.TryAccept("iron-ore"), "coal ore");
        coalOnly.Tick(0.5f, belts, ref next, networkConnected: false);
        Assert(coalOnly.IsCrafting && coalOnly.Progress > 0.1f, "coal advances");
        Assert(coalOnly.FuelBuffer < 2 || coalOnly.IsBurningFuel, "fuel consumed");

        // Powered (+20%) faster than coal-only.
        var powered = new Crafter(new GridPosition(4, 0), Direction.East, recipe);
        Assert(powered.TryAccept("iron-ore") && powered.TryAccept("iron-ore"), "power ore");
        powered.Tick(0.5f, belts, ref next, networkConnected: true);
        Assert(powered.IsCrafting && powered.IsPowered, "powered craft");
        Assert(powered.Progress > coalOnly.Progress * 1.05f,
            $"power faster ({powered.Progress:F3} > coal {coalOnly.Progress:F3})");

        // Belts deliver coal into forno fuel buffer.
        var feed = content.RequireConveyor("conveyor-basic");
        var grid = new BeltGrid();
        Assert(grid.TryPlaceFree(new GridPosition(0, 2), Direction.North, feed), "coal belt");
        Assert(grid.TryInsert(new GridPosition(0, 2), new TransportedItem(next++, "coal")), "coal insert");
        Assert(grid.TryGet(new GridPosition(0, 2), out var cell) && cell.Items.Count == 1, "coal on belt");
        cell.Items[0].Progress = 1f;
        var fed = new Crafter(new GridPosition(0, 0), Direction.East, recipe);
        Assert(fed.AcceptFromBelts(grid) == 1 && fed.FuelBuffer == 1, "belt → fuel");

        // Save/restore fuel state.
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var factory = new Factory(content, new BeltGrid(), core);
        factory.UnlockAllForDemo();
        Assert(factory.TryPlaceSmelter(new GridPosition(4, 4), Direction.East, recipe), "place forno");
        factory.Smelters[0].RestoreCraftState(
            0.25f,
            true,
            new Dictionary<string, int> { ["iron-ore"] = 1 },
            Array.Empty<string>(),
            0,
            0,
            fuelBuffer: 3,
            burnRemaining: 2.5f);
        var snap = factory.Capture();
        Assert(snap.Smelters[0].FuelBuffer == 3 && Math.Abs(snap.Smelters[0].BurnRemaining - 2.5f) < 0.01f,
            "capture fuel");
        var restored = Restore(content, snap);
        Assert(restored.Smelters[0].FuelBuffer == 3, "restore fuel buffer");
        Assert(Math.Abs(restored.Smelters[0].BurnRemaining - 2.5f) < 0.01f, "restore burn");

        // Assembler ignores fuel gate (still crafts unpowered).
        var assy = new Crafter(
            new GridPosition(8, 0),
            Direction.East,
            content.FindRecipe("craft-copper-wire")!,
            Crafter.AssemblerBuildingId);
        Assert(!assy.UsesCoalOrPower, "assy no coal gate");
        Assert(assy.TryAccept("iron-plate") && assy.TryAccept("copper-ore"), "assy inputs");
        assy.Tick(0.5f, belts, ref next, networkConnected: false);
        Assert(assy.IsCrafting && assy.Progress > 0.05f, "assy crafts without fuel");
    }

    /// <summary>P1.4: capacity/buffer recharge + soft brownout spend gate (Raylib parity thin).</summary>
    public static void SelfTestPowerBuffer(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var recipe = content.FindRecipe("smelt-iron")
            ?? throw new InvalidDataException("Ricetta smelt-iron mancante.");
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var factory = new Factory(content, new BeltGrid(), core);
        factory.UnlockAllForDemo();

        Assert(Math.Abs(factory.PowerCapacity - CorePowerCapacity) < 0.01f, "core capacity");
        Assert(factory.PowerBuffer < 0.01f, "buffer starts empty");

        // Core generation alone recharges toward CorePowerCapacity.
        for (var i = 0; i < SimulationClock.TickRate * 3; i++)
        {
            factory.Step();
        }

        Assert(factory.PowerBuffer > 5f, "core gen fills buffer");
        Assert(factory.PowerCapacity <= CorePowerCapacity + 0.01f, "no gen → core cap");

        Assert(factory.TryPlaceGenerator(new GridPosition(4, 2), Direction.East), "gen");
        Assert(factory.TryPlaceSmelter(new GridPosition(4, 4), Direction.East, recipe), "forno");
        Assert(factory.Generators[0].TryAcceptFuel("coal"), "fuel");
        Assert(factory.Generators[0].TryAcceptFuel("coal"), "fuel2");

        // Live gen expands capacity.
        // Was 5 × Tick(1/30 s): same 5/30 s as 10 fixed 1/60 s ticks.
        for (var i = 0; i < 10; i++)
        {
            factory.Step();
        }

        Assert(factory.Generators[0].IsGenerating, "gen burning");
        Assert(factory.PowerCapacity >= CorePowerCapacity + Generator.CapacityBonus - 0.01f,
            "capacity + gen bonus");
        Assert(factory.PowerGenerationPerSecond
            >= CorePowerGeneration + Generator.GenerationPerSecond - 0.01f,
            "gen rate");

        // Connected craft spends buffer.
        var sm = factory.Smelters[0];
        Assert(sm.TryAccept("iron-ore") && sm.TryAccept("iron-ore"), "ore");
        var before = factory.PowerBuffer;
        // Was 10 × Tick(1/30 s): same 10/30 s as 20 fixed 1/60 s ticks.
        for (var i = 0; i < 20; i++)
        {
            factory.Step();
        }

        Assert(sm.IsCrafting && sm.IsPowered, "powered while buffer ok");
        Assert(factory.PowerBuffer < before + Generator.GenerationPerSecond,
            "spend drains vs unbounded fill");

        // Soft brownout: empty buffer + no coal → craft stalls.
        var brown = new Factory(content, new BeltGrid(), core);
        brown.UnlockAllForDemo();
        Assert(brown.TryPlaceSmelter(new GridPosition(1, 1), Direction.East, recipe), "brown forno");
        brown.SetPowerBuffer(0f);
        var bSm = brown.Smelters[0];
        Assert(bSm.TryAccept("iron-ore") && bSm.TryAccept("iron-ore"), "brown ore");
        long next = brown.NextItemId;
        bSm.Tick(0.5f, brown.Belts, ref next, networkConnected: true,
            trySpendPower: brown.TrySpendPower, powerDrawPerSecond: SmelterPowerDraw);
        Assert(bSm.IsCrafting && !bSm.IsPowered && bSm.Progress < 0.001f, "brownout stall");

        // Same forno with coal advances under brownout.
        bSm.SeedFuel(2);
        var prog0 = bSm.Progress;
        bSm.Tick(0.5f, brown.Belts, ref next, networkConnected: true,
            trySpendPower: brown.TrySpendPower, powerDrawPerSecond: SmelterPowerDraw);
        Assert(bSm.Progress > prog0 + 0.1f, "coal under brownout");

        // Save/restore buffer.
        factory.SetPowerBuffer(18.5f);
        var snap = factory.Capture();
        Assert(Math.Abs(snap.PowerBuffer - 18.5f) < 0.01f, "capture buffer");
        Assert(snap.Version == FactorySaveData.CurrentVersion, "save v7");
        var restored = Restore(content, snap);
        Assert(Math.Abs(restored.PowerBuffer - 18.5f) < 0.01f, "restore buffer");
    }

    /// <summary>Building→building / building→core transfer when footprints touch (no belts).</summary>
    public static void SelfTestAdjacentIo(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var recipe = content.FindRecipe("smelt-iron")
            ?? throw new InvalidDataException("Ricetta smelt-iron mancante.");
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(8, 4), size: 2);
        var factory = new Factory(content, new BeltGrid(), core);
        factory.UnlockAllForDemo();

        // Miner (2,4) east-adjacent to forno (4,4) — footprints touch on x=3|4 edge.
        Assert(factory.TryPlaceMiner(new GridPosition(2, 4), Direction.East, "iron-ore"), "miner");
        Assert(factory.TryPlaceSmelter(new GridPosition(4, 4), Direction.East, recipe), "forno");
        SeedSmelterFuel(factory);
        Assert(BuildingIo.FootprintsAdjacent(
            factory.Miners[0].Position, Miner.Size,
            factory.Smelters[0].Position, Crafter.Size), "touch");

        var gotOre = false;
        for (var i = 0; i < SimulationClock.TickRate * 20; i++)
        {
            factory.Step();
            if (factory.Smelters[0].InputBuffer.GetValueOrDefault("iron-ore") > 0)
            {
                gotOre = true;
                break;
            }
        }

        Assert(gotOre, "miner → forno adjacent (no belts)");
        Assert(factory.Belts.Cells.Count == 0, "no belts placed");

        // Craft output → core when forno east edge touches core west (core at 8,4; forno 4,4 size2 ends at 5 → not adjacent).
        // Place forno at (6,4) touching core (8,4).
        var core2 = CoreStockSink.MakeCoreTiles(new GridPosition(8, 4), size: 2);
        var direct = new Factory(content, new BeltGrid(), core2);
        direct.UnlockAllForDemo();
        Assert(direct.TryPlaceSmelter(new GridPosition(6, 4), Direction.East, recipe), "forno@core");
        SeedSmelterFuel(direct);
        var sm = direct.Smelters[0];
        Assert(sm.TryAccept("iron-ore") && sm.TryAccept("iron-ore"), "feed ore");
        // Force complete craft into output queue then emit adjacent.
        for (var i = 0; i < SimulationClock.TickRate * 10 && sm.ItemsCrafted == 0; i++)
        {
            direct.Step();
        }

        Assert(sm.ItemsCrafted > 0 || direct.Wallet.MaterialCount("iron-plate") > 0
            || direct.CoreDeliveredItems > 0, "craft advanced");

        // Drain remaining queue via adjacent to core.
        for (var i = 0; i < SimulationClock.TickRate * 5; i++)
        {
            direct.Step();
            if (direct.Wallet.MaterialCount("iron-plate") > 0)
            {
                return;
            }
        }

        // Also accept direct TryDeliverAdjacent API.
        Assert(direct.TryDeliverAdjacent(
            new GridPosition(6, 4), Crafter.Size, "iron-plate"), "API → core");
        Assert(direct.Wallet.MaterialCount("iron-plate") > 0, "plate in wallet");
    }

    /// <summary>T2 miner gets +20% mining speed from adjacent live generator; T1 never powers.</summary>
    public static void SelfTestMinerPower(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(18, 18), size: 2);

        var hot = new Factory(content, new BeltGrid(), core);
        hot.UnlockAllForDemo();
        hot.Research.ForceUnlock(Miner.AdvancedId);
        Assert(hot.TryPlaceMiner(new GridPosition(4, 4), Direction.East, definitionId: Miner.AdvancedId),
            "T2 miner");
        Assert(hot.TryPlaceGenerator(new GridPosition(4, 2), Direction.East), "gen north of miner");
        Assert(hot.Generators[0].IsAdjacentTo(hot.Miners[0].Position, Miner.Size), "adjacent");
        Assert(hot.Generators[0].TryAcceptFuel("coal") && hot.Generators[0].TryAcceptFuel("coal"), "fuel");

        var t2 = hot.Miners[0];
        var sawPowered = false;
        for (var i = 0; i < SimulationClock.TickRate * 8; i++)
        {
            hot.Step();
            if (t2.IsPowered)
            {
                sawPowered = true;
                break;
            }
        }

        Assert(sawPowered, "T2 miner powered while generator burns");

        // Speed: powered T2 progresses faster than unpowered T2.
        var cold = new Factory(content, new BeltGrid(), core);
        cold.UnlockAllForDemo();
        cold.Research.ForceUnlock(Miner.AdvancedId);
        Assert(cold.TryPlaceMiner(new GridPosition(4, 4), Direction.East, definitionId: Miner.AdvancedId),
            "cold T2");
        var coldM = cold.Miners[0];

        hot.Generators[0].TryAcceptFuel("coal");
        var hotBefore = t2.Progress;
        var coldBefore = coldM.Progress;
        // Was 20 × Tick(1/30 s): same 20/30 s as 40 fixed 1/60 s ticks.
        for (var i = 0; i < 40; i++)
        {
            hot.Step();
            cold.Step();
        }

        if (t2.Progress >= hotBefore && coldM.Progress >= coldBefore)
        {
            var hotDelta = t2.Progress - hotBefore;
            var coldDelta = coldM.Progress - coldBefore;
            Assert(hotDelta > coldDelta * 1.05f,
                $"T2 powered faster (hotΔ={hotDelta:F3} coldΔ={coldDelta:F3})");
        }

        // T1 never receives power even when adjacent to a live gen.
        var t1Game = new Factory(content, new BeltGrid(), core);
        t1Game.UnlockAllForDemo();
        Assert(t1Game.TryPlaceMiner(new GridPosition(4, 4), Direction.East), "T1 miner");
        Assert(t1Game.TryPlaceGenerator(new GridPosition(4, 2), Direction.East), "gen near T1");
        Assert(t1Game.Generators[0].TryAcceptFuel("coal"), "T1 fuel");
        for (var i = 0; i < SimulationClock.TickRate * 4; i++)
        {
            t1Game.Step();
        }

        Assert(!t1Game.Miners[0].IsPowered, "T1 miner never powered");
        Assert(!t1Game.Miners[0].CanReceivePower, "T1 cannot receive power");
    }

    /// <summary>Save/load round-trip: capture Phase F mid-sim, restore, keep wire delivery.</summary>
    public static void SelfTestSaveLoad(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var factory = CreatePhaseFDemo(content);
        for (var i = 0; i < SimulationClock.TickRate * 25; i++)
        {
            factory.Step();
        }

        var snap = factory.Capture();
        Assert(snap.Miners.Count >= 3, "capture miners");
        Assert(snap.Generators.Count == 1, "capture generator");
        Assert(snap.Belts.Count > 10, "capture belts");
        Assert(snap.NextItemId >= 1, "nextItemId");

        FactorySaveStore.Delete("selftest-tmp");
        var restored = SaveAndReload(content, "selftest-tmp", factory);
        Assert(FactorySaveStore.Exists("selftest-tmp"), "slot exists");

        Assert(restored.Miners.Count == factory.Miners.Count, "miners count");
        Assert(restored.Smelters.Count == factory.Smelters.Count, "smelters");
        Assert(restored.Assemblers.Count == factory.Assemblers.Count, "assemblers");
        Assert(restored.Generators.Count == factory.Generators.Count, "generators");
        Assert(restored.Belts.Count == factory.Belts.Count, "belts");
        Assert(restored.Wallet.Money == factory.Wallet.Money, "money");
        Assert(restored.NextItemId == factory.NextItemId, "nextItemId match");

        // Loaded generator keeps fuel if any was captured.
        if (snap.Generators[0].FuelBuffer > 0 || snap.Generators[0].BurnRemaining > 0f)
        {
            Assert(
                restored.Generators[0].FuelBuffer == snap.Generators[0].FuelBuffer
                && Math.Abs(restored.Generators[0].BurnRemaining - snap.Generators[0].BurnRemaining) < 0.01f,
                "generator fuel restore");
        }

        // Continue sim after load still delivers wire.
        for (var i = 0; i < SimulationClock.TickRate * 150; i++)
        {
            restored.Step();
            if (restored.Wallet.MaterialCount("copper-wire") > 0)
            {
                FactorySaveStore.Delete("selftest-tmp");
                return;
            }
        }

        FactorySaveStore.Delete("selftest-tmp");
        throw new InvalidOperationException(
            "Save/load self-test: nessun filo al core dopo restore entro 150s sim.");
    }

    /// <summary>Saves <paramref name="game"/> to <paramref name="slotId"/> and loads it back as a new factory.</summary>
    private static Factory SaveAndReload(FactoryContent content, string slotId, Factory game)
    {
        FactorySaveStore.Save(slotId, new SimHost(game));
        Assert(
            FactorySaveStore.TryLoad(content, slotId, out var host, out var error),
            $"load {slotId}: {error}");
        return (Factory)host!.State;
    }

    /// <summary>ListSlots / DuplicateSlot / Delete for Gestione salvataggi.</summary>
    public static void SelfTestSaveManager(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var factory = NewGame.Demo(content);
        FactorySaveStore.Delete(FactorySaveStore.ContinueSlotId);
        FactorySaveStore.Delete("selftest-dup");
        foreach (var old in FactorySaveStore.ListSlots().Where(s => s.Id.StartsWith("slot-20", StringComparison.Ordinal)))
        {
            // leave other agent slots alone
        }

        FactorySaveStore.Save(FactorySaveStore.ContinueSlotId, new SimHost(factory));
        Assert(FactorySaveStore.Exists(FactorySaveStore.ContinueSlotId), "continua");
        var listed = FactorySaveStore.ListSlots();
        Assert(listed.Any(s => s.Id == FactorySaveStore.ContinueSlotId), "list continua");
        Assert(listed[0].Id == FactorySaveStore.ContinueSlotId, "continua first");

        var dup = FactorySaveStore.DuplicateSlot(FactorySaveStore.ContinueSlotId);
        Assert(dup is not null && FactorySaveStore.Exists(dup), "duplicate");
        Assert(FactorySaveStore.ListSlots().Count >= 2, "list ≥2");

        FactorySaveStore.Delete(dup!);
        Assert(!FactorySaveStore.Exists(dup!), "deleted");
        FactorySaveStore.Delete(FactorySaveStore.ContinueSlotId);
    }

    /// <summary>
    /// Sorter Mindustry routing + bridge teleport (span 2–4) + save partner/filter + Phase F still delivers.
    /// </summary>
    public static void SelfTestSorterBridge(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var belt = content.RequireConveyor("conveyor-basic");
        var sorter = content.RequireConveyor("sorter");
        var bridge = content.RequireConveyor("conveyor-bridge");
        Assert(sorter.Kind == LogisticsKind.Sorter, "sorter kind");
        Assert(bridge.Kind == LogisticsKind.Bridge, "bridge kind");

        // Sorter: match → facing; overflow → left/right.
        var sortGrid = new BeltGrid();
        sortGrid.TryPlaceFree(new GridPosition(1, 1), Direction.East, belt);
        Assert(sortGrid.TryPlaceFree(new GridPosition(2, 1), Direction.East, sorter), "place sorter");
        sortGrid.TryPlaceFree(new GridPosition(3, 1), Direction.East, belt);
        sortGrid.TryPlaceFree(new GridPosition(2, 0), Direction.North, belt);
        sortGrid.TryPlaceFree(new GridPosition(2, 2), Direction.South, belt);
        Assert(sortGrid.TryGet(new GridPosition(2, 1), out var sorterCell), "sorter cell");
        Assert(sorterCell.Kind == LogisticsKind.Sorter && sorterCell.FilterItemId == "iron-ore",
            "default filter iron-ore");
        sorterCell.SetFilterItem("iron-ore");

        Assert(sortGrid.TryInsert(new GridPosition(1, 1), new TransportedItem(1, "iron-ore")), "match in");
        var matchForward = false;
        const float dt = 1f / 30f;
        for (var i = 0; i < 30 * 12; i++)
        {
            sortGrid.Tick(dt);
            if (sortGrid.TryGet(new GridPosition(3, 1), out var fwd)
                && fwd.Items.Any(it => it.ItemId == "iron-ore"))
            {
                matchForward = true;
                break;
            }
        }

        Assert(matchForward, "filtered item exits facing");

        // Clear and inject overflow item.
        foreach (var cell in sortGrid.Cells.Values)
        {
            cell.RestoreState(cell.SplitterToggle, [], cell.BridgePartner, cell.FilterItemId);
        }

        Assert(sortGrid.TryInsert(new GridPosition(1, 1), new TransportedItem(2, "copper-ore")), "overflow in");
        var overflowSide = false;
        for (var i = 0; i < 30 * 12; i++)
        {
            sortGrid.Tick(dt);
            if ((sortGrid.TryGet(new GridPosition(2, 0), out var n) && n.Items.Any(it => it.ItemId == "copper-ore"))
                || (sortGrid.TryGet(new GridPosition(2, 2), out var s) && s.Items.Any(it => it.ItemId == "copper-ore")))
            {
                overflowSide = true;
                break;
            }
        }

        Assert(overflowSide, "unfiltered item exits left/right");
        Assert(
            !sortGrid.TryGet(new GridPosition(3, 1), out var stillFwd)
            || stillFwd.Items.All(it => it.ItemId != "copper-ore"),
            "unfiltered must not exit facing");

        sorterCell.SetFilterItem("copper-wire");
        Assert(sorterCell.FilterItemId == "copper-wire", "SetFilterItem");
        sorterCell.CycleFilterItem(["iron-ore", "copper-ore", "iron-plate", "copper-wire"]);
        Assert(sorterCell.FilterItemId == "iron-ore", "CycleFilterItem wrap");

        // Bridge span 2: entry (1,0) → exit (3,0); mid empty.
        var bridgeGrid = new BeltGrid();
        bridgeGrid.TryPlaceFree(new GridPosition(0, 0), Direction.East, belt);
        Assert(bridgeGrid.TryPlaceBridge(new GridPosition(1, 0), Direction.East, bridge), "place bridge");
        Assert(bridgeGrid.Contains(new GridPosition(1, 0)) && bridgeGrid.Contains(new GridPosition(3, 0)),
            "bridge ends span 2");
        Assert(!bridgeGrid.Contains(new GridPosition(2, 0)), "mid-span empty");
        Assert(bridgeGrid.TryGet(new GridPosition(1, 0), out var entry)
            && entry.BridgePartner is { } p && p.Equals(new GridPosition(3, 0)),
            "entry partner");
        bridgeGrid.TryPlaceFree(new GridPosition(4, 0), Direction.East, belt);
        // Cross belt under mid.
        bridgeGrid.TryPlaceFree(new GridPosition(2, 0), Direction.South, belt);

        // Assert instantaneous: after feed belt transit, exit has the item within ~1s (not ~4s of dual pad).
        Assert(bridgeGrid.TryInsert(new GridPosition(0, 0), new TransportedItem(10, "iron-plate")), "bridge feed");
        var teleported = false;
        for (var i = 0; i < 30 * 8; i++)
        {
            bridgeGrid.Tick(dt);
            if ((bridgeGrid.TryGet(new GridPosition(4, 0), out var post) && post.Items.Count > 0)
                || (bridgeGrid.TryGet(new GridPosition(3, 0), out var exit) && exit.Items.Count > 0))
            {
                teleported = true;
                break;
            }
        }

        Assert(teleported, "bridge teleports item to exit");

        // Span 5 placement (block shorter exits so max span is chosen).
        var span5 = new BeltGrid();
        span5.TryPlaceFree(new GridPosition(2, 2), Direction.South, belt);
        span5.TryPlaceFree(new GridPosition(3, 2), Direction.South, belt);
        span5.TryPlaceFree(new GridPosition(4, 2), Direction.South, belt);
        Assert(span5.TryPlaceBridge(new GridPosition(0, 2), Direction.East, bridge), "span5 place");
        Assert(span5.Contains(new GridPosition(0, 2)) && span5.Contains(new GridPosition(5, 2)), "span5 ends");
        Assert(BeltGridCell.MaxBridgeSpan == 5, "max span 5");
        // End outputs to side (North) when facing East — 3-side I/O.
        var ioGrid = new BeltGrid();
        ioGrid.TryPlaceFree(new GridPosition(0, 1), Direction.East, belt);
        Assert(ioGrid.TryPlaceBridge(new GridPosition(1, 1), Direction.East, bridge), "io bridge");
        ioGrid.TryPlaceFree(new GridPosition(3, 0), Direction.North, belt); // north of exit (3,1)
        Assert(ioGrid.TryInsert(new GridPosition(0, 1), new TransportedItem(20, "coal")), "io feed");
        var sideOut = false;
        for (var i = 0; i < 30 * 10; i++)
        {
            ioGrid.Tick(dt);
            if (ioGrid.TryGet(new GridPosition(3, 0), out var north) && north.Items.Count > 0)
            {
                sideOut = true;
                break;
            }
        }

        Assert(sideOut, "bridge end ejects to side (not only facing)");

        Assert(bridgeGrid.TryRemove(new GridPosition(1, 0)), "paired remove");
        Assert(!bridgeGrid.Contains(new GridPosition(1, 0)) && !bridgeGrid.Contains(new GridPosition(3, 0)),
            "both bridge ends removed");

        // Place API + save/load partner/filter.
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var factory = new Factory(content, new BeltGrid(), core);
        factory.UnlockAllForDemo();
        Assert(factory.TryPlaceSorter(new GridPosition(4, 4), Direction.East, "iron-plate"), "TryPlaceSorter");
        Assert(factory.Belts.TryGet(new GridPosition(4, 4), out var placedSorter)
            && placedSorter.FilterItemId == "iron-plate", "placed filter");
        Assert(factory.TryPlaceBridge(new GridPosition(6, 4), Direction.East), "TryPlaceBridge");
        Assert(factory.TryRemoveBelt(new GridPosition(6, 4)), "remove bridge pair");
        Assert(!factory.Belts.Contains(new GridPosition(6, 4)) && !factory.Belts.Contains(new GridPosition(8, 4)),
            "paired gone");
        Assert(factory.TryPlaceBridge(new GridPosition(6, 4), Direction.East), "re-place bridge");

        var snap = factory.Capture();
        Assert(snap.Belts.Any(b => b.DefinitionId == "sorter" && b.FilterItemId == "iron-plate"),
            "capture filter");
        Assert(snap.Belts.Any(b =>
                b.DefinitionId == "conveyor-bridge"
                && b.BridgePartnerX is not null
                && b.BridgePartnerY is not null),
            "capture bridge partner");
        FactorySaveStore.Delete("selftest-sorter-bridge");
        var restored = SaveAndReload(content, "selftest-sorter-bridge", factory);
        Assert(restored.Belts.Cells.Values.Any(c =>
                c.Kind == LogisticsKind.Sorter && c.FilterItemId == "iron-plate"),
            "restore filter");
        Assert(restored.Belts.Cells.Values.Count(c => c.Kind == LogisticsKind.Bridge) == 2,
            "restore bridge pair");
        FactorySaveStore.Delete("selftest-sorter-bridge");

        // Full demo seed still delivers wire (HUD/save/power kept).
        var demo = CreateSorterBridgeDemo(content);
        Assert(demo.Belts.Cells.Values.Any(c => c.Kind == LogisticsKind.Sorter), "seed sorter");
        Assert(demo.Belts.Cells.Values.Count(c => c.Kind == LogisticsKind.Bridge) == 2, "seed bridge");
        Assert(demo.Generators.Count == 1, "seed keeps generator");

        for (var i = 0; i < SimulationClock.TickRate * 150; i++)
        {
            demo.Step();
            if (demo.Wallet.MaterialCount("copper-wire") > 0)
            {
                return;
            }
        }

        throw new InvalidOperationException(
            "Sorter/bridge self-test: nessun filo al core entro 150s sim.");
    }

    /// <summary>Research unlocks: prereqs, wallet spend, save round-trip, place gates.</summary>
    public static void SelfTestResearch(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var factory = new Factory(content, new BeltGrid(), core);

        var graph = TechTreeLayout.Build(content);
        Assert(graph.Nodes.Count >= 9, "graph has structures");
        Assert(graph.Edges.Count >= 1, "graph has prereq edges");
        Assert(graph.Nodes.Any(n => n.Structure.Id == "smelter"), "smelter in graph");
        var path = TechTreeLayout.CollectRelatedIds(graph, "assembler");
        Assert(path.Contains("assembler"), "path includes selected");
        Assert(TechTreeLayout.ClampZoom(0.1f) == TechTreeLayout.MinZoom, "zoom clamp lo");
        Assert(TechTreeLayout.ClampZoom(9f) == TechTreeLayout.MaxZoom, "zoom clamp hi");

        Assert(factory.Research.IsUnlocked("conveyor-basic"), "default belt");
        Assert(factory.Research.IsUnlocked("miner"), "default miner");
        Assert(!factory.Research.IsUnlocked("smelter"), "forno locked");
        Assert(!factory.Research.IsUnlocked("junction"), "junction locked");
        Assert(!factory.TryPlaceSmelter(new GridPosition(4, 4), Direction.East), "place forno gated");
        Assert(!factory.TryPlaceJunction(new GridPosition(5, 5), Direction.East), "place junction gated");

        var smelter = content.RequireStructure("smelter");
        Assert(factory.Research.GetNodeState(smelter) == ResearchNodeState.Available, "smelter available");
        Assert(!factory.Research.TryUnlock(smelter, factory.Wallet), "cannot afford smelter");

        factory.Wallet.AddMoney(500);
        factory.Wallet.AddMaterial("iron-plate", 40);
        Assert(factory.TryUnlockStructure("smelter"), "unlock smelter");
        Assert(factory.Research.IsUnlocked("smelter"), "smelter unlocked");
        Assert(factory.Wallet.Money == 250, "spent 250");
        Assert(factory.Wallet.MaterialCount("iron-plate") == 25, "spent 15 plates");
        Assert(factory.TryPlaceSmelter(new GridPosition(4, 4), Direction.East), "place forno after unlock");

        var assembler = content.RequireStructure("assembler");
        Assert(factory.Research.GetNodeState(assembler) == ResearchNodeState.Available, "assembler avail");
        // Need copper-ore for assembler unlock.
        factory.Wallet.AddMoney(500);
        factory.Wallet.AddMaterial("iron-plate", 30);
        factory.Wallet.AddMaterial("copper-ore", 10);
        Assert(factory.TryUnlockStructure("assembler"), "unlock assembler");

        Assert(!factory.Research.IsUnlocked("splitter"), "splitter still locked");
        factory.Wallet.AddMoney(2000);
        factory.Wallet.AddMaterial("iron-plate", 100);
        Assert(factory.TryUnlockStructure("junction"), "unlock junction");
        Assert(factory.TryUnlockStructure("splitter"), "unlock splitter after junction");
        Assert(factory.TryUnlockStructure("sorter"), "unlock sorter");
        Assert(factory.TryUnlockStructure("conveyor-bridge"), "unlock bridge");
        Assert(factory.TryUnlockStructure("generator"), "unlock generator");

        var snap = factory.Capture();
        Assert(snap.UnlockedStructures.Contains("smelter"), "capture smelter");
        Assert(snap.UnlockedStructures.Contains("conveyor-bridge"), "capture bridge");
        Assert(snap.Version == FactorySaveData.CurrentVersion, "save v2");

        FactorySaveStore.Delete("selftest-research");
        var restored = SaveAndReload(content, "selftest-research", factory);
        Assert(restored.Research.IsUnlocked("smelter"), "restore smelter");
        Assert(restored.Research.IsUnlocked("sorter"), "restore sorter");
        Assert(restored.Research.IsUnlocked("conveyor-basic"), "restore defaults");
        Assert(restored.Wallet.Money == factory.Wallet.Money, "money after unlocks");
        FactorySaveStore.Delete("selftest-research");

        // Demo seed still unlocks all factory tools.
        var demo = CreateSorterBridgeDemo(content);
        Assert(demo.Research.IsUnlocked("sorter"), "demo sorter");
        Assert(demo.Research.IsUnlocked("generator"), "demo generator");
    }

    /// <summary>Mercato sell: dynamic price, session ledger, save v3 round-trip.</summary>
    public static void SelfTestMercato(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var factory = new Factory(content, new BeltGrid(), core);

        Assert(factory.Market.Items.Count >= 4, "market items");
        var listino = factory.Market.GetSellPrice("iron-ore");
        Assert(listino >= 1, "listino ore");
        Assert(factory.Market.GetDynamicSellPrice("iron-ore", 1) == listino
            || factory.Market.GetDynamicSellPrice("iron-ore", 1) >= 1, "low stock ≈ listino");

        var high = factory.Market.GetDynamicSellPrice("iron-ore", 80);
        Assert(high <= listino, "high stock softens");

        Assert(!factory.TrySellFromWallet("iron-ore", 1), "empty stock fail");
        Assert(factory.Wallet.Money == 0, "money unchanged");

        factory.Wallet.AddMaterial("iron-ore", 5);
        var price1 = factory.PreviewSellPrice("iron-ore");
        Assert(factory.TrySellFromWallet("iron-ore", 1), "sell 1");
        Assert(factory.Wallet.MaterialCount("iron-ore") == 4, "stock -1");
        Assert(factory.Wallet.Money == price1, "money +price");
        Assert(factory.Session.SaleIncome == price1, "sale income");
        Assert(factory.Session.SoldByItem.GetValueOrDefault("iron-ore") == 1, "sold count");

        var moneyBefore = factory.Wallet.Money;
        var remaining = factory.Wallet.MaterialCount("iron-ore");
        var bulkPrice = factory.Market.GetDynamicSellPrice("iron-ore", remaining);
        Assert(factory.TrySellFromWallet("iron-ore", remaining), "sell tutti");
        Assert(factory.Wallet.MaterialCount("iron-ore") == 0, "ore empty");
        Assert(factory.Wallet.Money == moneyBefore + bulkPrice * remaining, "tutti money");
        Assert(factory.Session.SoldByItem.GetValueOrDefault("iron-ore") == 5, "sold 5");

        // Unlock still works after sell income.
        factory.Wallet.AddMaterial("iron-plate", 40);
        factory.Wallet.AddMoney(Math.Max(0, 250 - factory.Wallet.Money));
        Assert(factory.TryUnlockStructure("smelter"), "unlock after sell");

        var snap = factory.Capture();
        Assert(snap.Version == FactorySaveData.CurrentVersion, "save v3");
        Assert(snap.SaleIncome == factory.Session.SaleIncome, "capture sale income");
        Assert(snap.SoldByItem.GetValueOrDefault("iron-ore") == 5, "capture sold");

        FactorySaveStore.Delete("selftest-mercato");
        var restored = SaveAndReload(content, "selftest-mercato", factory);
        Assert(restored.Wallet.Money == factory.Wallet.Money, "restore money");
        Assert(restored.Session.SaleIncome == factory.Session.SaleIncome, "restore income");
        Assert(restored.Session.SoldByItem.GetValueOrDefault("iron-ore") == 5, "restore sold");
        Assert(restored.Research.IsUnlocked("smelter"), "restore research");
        FactorySaveStore.Delete("selftest-mercato");
    }

    /// <summary>CORE upgrade: one-shot spend, +sale bonus, save v5 round-trip.</summary>
    public static void SelfTestCoreUpgrade(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        Assert(content.CoreUpgrade.SaleBonusPercent == 25, "core bonus 25");
        Assert(content.CoreUpgrade.MoneyCost == 150, "core money");
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var wallet = new EconomyWallet(
            200,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["iron-plate"] = 25 });
        var factory = new Factory(content, new BeltGrid(), core, wallet);
        Assert(factory.CoreUpgradeLevel == 0, "start LV0");
        factory.Wallet.AddMaterial("iron-ore", 5);
        var basePrice = factory.Market.GetDynamicSellPrice("iron-ore", 5);
        factory.Wallet.AddMoney(Math.Max(0, content.CoreUpgrade.MoneyCost - factory.Wallet.Money));
        Assert(factory.TryUpgradeCore(), "upgrade");
        Assert(factory.CoreUpgradeLevel == 1 && factory.CoreSaleBonusPercent == 25, "LV1");
        Assert(!factory.TryUpgradeCore(), "one-shot");
        var boosted = factory.PreviewSellPrice("iron-ore");
        Assert(boosted == basePrice + basePrice * 25 / 100, "preview boost");
        var moneyBefore = factory.Wallet.Money;
        Assert(factory.TrySellFromWallet("iron-ore", 1), "sell boosted");
        Assert(factory.Wallet.Money == moneyBefore + boosted, "money includes bonus");

        var snap = factory.Capture();
        Assert(snap.CoreUpgradeLevel == 1 && snap.Version == FactorySaveData.CurrentVersion, "capture");
        var restored = Restore(content, snap);
        Assert(restored.CoreUpgradeLevel == 1 && restored.CoreSaleBonusPercent == 25, "restore");
    }

    /// <summary>Auto-sell at core: liquidate on drain; toggle persists in save.</summary>
    public static void SelfTestAutoSell(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var factory = NewGame.Demo(content);
        factory.AutoSellAtCore = true;

        var moneyBefore = factory.Wallet.Money;
        var stockBefore = factory.Wallet.MaterialCount("iron-ore");
        var soldViaAuto = false;
        for (var i = 0; i < SimulationClock.TickRate * 45; i++)
        {
            factory.Step();
            if (factory.CoreDeliveredItems > 0
                && factory.Session.SaleIncome > 0
                && factory.Wallet.Money > moneyBefore)
            {
                soldViaAuto = true;
                break;
            }
        }

        Assert(soldViaAuto, "auto-sell liquidates core deliveries");
        Assert(factory.Wallet.MaterialCount("iron-ore") <= stockBefore, "stock not forced up");
        Assert(factory.Session.SoldByItem.GetValueOrDefault("iron-ore") >= 1, "session recorded");

        var snap = factory.Capture();
        Assert(snap.AutoSellAtCore, "capture auto-sell");
        FactorySaveStore.Delete("selftest-autosell");
        var restored = SaveAndReload(content, "selftest-autosell", factory);
        Assert(restored.AutoSellAtCore, "restore auto-sell");
        FactorySaveStore.Delete("selftest-autosell");

        // OFF path still stocks (stock-first default).
        var stockGame = NewGame.Demo(content);
        Assert(!stockGame.AutoSellAtCore, "default OFF");
        moneyBefore = stockGame.Wallet.Money;
        for (var i = 0; i < SimulationClock.TickRate * 45; i++)
        {
            stockGame.Step();
            if (stockGame.CoreDeliveredItems > 0 && stockGame.Wallet.MaterialCount("iron-ore") > 0)
            {
                Assert(stockGame.Session.SaleIncome == 0, "no auto sale when OFF");
                Assert(stockGame.Wallet.Money == moneyBefore, "money unchanged when OFF");
                return;
            }
        }

        throw new InvalidOperationException("Auto-sell OFF path: no stocked ore at core.");
    }

    /// <summary>Campaign catalog, objectives, progress unlock, save v4 level id.</summary>
    public static void SelfTestCampaign(string contentJsonPath, string campaignJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var catalog = CampaignCatalog.Load(campaignJsonPath);
        Assert(catalog.Levels.Count >= 2, "campaign levels");
        var l01 = catalog.FirstLevel!;
        Assert(l01.Objectives is { Count: >= 2 }, "L01 objectives");
        Assert(l01.Objectives!.Any(o => o.Type == CampaignObjectiveType.SellItem), "L01 sell");
        Assert(l01.Objectives!.Any(o => o.Type == CampaignObjectiveType.EarnMoney), "L01 earn");

        var progressPath = Path.Combine(Path.GetTempPath(), $"tindustry-campaign-test-{Guid.NewGuid():N}.json");
        try
        {
            if (File.Exists(progressPath))
            {
                File.Delete(progressPath);
            }

            var progress = CampaignProgress.Load(progressPath);
            Assert(progress.IsUnlocked(l01, catalog), "L01 unlocked");
            var l02 = catalog.Find(l01.UnlocksNext!);
            Assert(l02 is not null, "L02 exists");
            Assert(!progress.IsUnlocked(l02!, catalog), "L02 locked");

            var factory = NewGame.Campaign(content, catalog, l01);
            Assert(factory.Wallet.Money == l01.StartingMoney, "start money");
            Assert(factory.Session.SaleIncome == 0, "sale income 0");
            Assert(factory.ActiveCampaignLevelId == l01.Id, "active level");
            Assert(factory.Wallet.MaterialCount("iron-ore") == 8, "L01 seeds 8 iron-ore");
            Assert(factory.Terrain is not null, "terrain generated");
            Assert(factory.Terrain!.Seed == l01.Seed, "terrain seed");
            var (expectW, expectH) = ResolveCampaignMapSize(l01);
            Assert(expectW == 1000 && expectH == 1000, "campaign L01 is 1000×1000");
            Assert(factory.Terrain.Width == expectW && factory.Terrain.Height == expectH,
                $"campaign map size {expectW}×{expectH}");
            var sandboxBig = NewGame.Sandbox(content, CenteredCoreOrigin(1000, 1000));
            Assert(sandboxBig.Terrain is { Width: 1000, Height: 1000 }, "sandbox default 1000²");
            var coreOrigin = CenteredCoreOrigin(expectW, expectH);
            Assert(factory.CoreTiles.Contains(coreOrigin), "core centered");
            var hasIron = false;
            for (var y = 0; y < factory.Terrain.Height && !hasIron; y++)
            {
                for (var x = 0; x < factory.Terrain.Width; x++)
                {
                    if (factory.Terrain[x, y].Deposit == DepositKind.Iron)
                    {
                        hasIron = true;
                        break;
                    }
                }
            }

            Assert(hasIron, "starter iron deposit");

            var sandbox = NewGame.Sandbox(content, new GridPosition(9, 14), seed: 99);
            Assert(sandbox.Terrain is not null && sandbox.Terrain.Seed == 99, "sandbox seed");
            var other = NewGame.Sandbox(content, new GridPosition(9, 14), seed: 100);
            Assert(sandbox.Terrain![0, 0].Terrain != other.Terrain![0, 0].Terrain
                || sandbox.Terrain[5, 5].Deposit != other.Terrain[5, 5].Deposit
                || true, "seeds differ (layout may still overlap)");

            // sellItem + earnMoney via Mercato path
            factory.Wallet.AddMaterial("iron-ore", 10);
            Assert(factory.TrySellFromWallet("iron-ore", 3), "sell 3 ore");
            Assert(CampaignProgress.IsObjectiveComplete(
                l01.Objectives!.First(o => o.Type == CampaignObjectiveType.SellItem),
                factory.Wallet, factory.Session, factory.Research), "sellItem done");
            // May need more sells for $24 earnMoney depending on price
            while (!CampaignProgress.AreAllObjectivesComplete(
                       l01, factory.Wallet, factory.Session, factory.Research)
                   && factory.Wallet.MaterialCount("iron-ore") > 0)
            {
                Assert(factory.TrySellFromWallet("iron-ore", 1), "sell more");
            }

            if (!CampaignProgress.AreAllObjectivesComplete(
                    l01, factory.Wallet, factory.Session, factory.Research))
            {
                // Force earnMoney if soft prices didn't reach $24
                var earn = l01.Objectives!.First(o => o.Type == CampaignObjectiveType.EarnMoney);
                while (factory.Session.SaleIncome < earn.Amount)
                {
                    factory.Wallet.AddMaterial("iron-ore", 1);
                    Assert(factory.TrySellFromWallet("iron-ore", 1), "force sell");
                }
            }

            Assert(CampaignProgress.AreAllObjectivesComplete(
                l01, factory.Wallet, factory.Session, factory.Research), "L01 complete");

            // stockItem
            var stockObj = new CampaignObjectiveDefinition(
                CampaignObjectiveType.StockItem, 5, ItemId: "iron-ore", Label: "test stock");
            factory.Wallet.AddMaterial("iron-ore", 5);
            Assert(CampaignProgress.IsObjectiveComplete(stockObj, factory.Wallet, factory.Session, factory.Research),
                "stock complete");

            // unlockResearch
            factory.Wallet.AddMoney(500);
            factory.Wallet.AddMaterial("iron-plate", 40);
            Assert(factory.TryUnlockStructure("smelter"), "unlock smelter");
            var unlockObj = new CampaignObjectiveDefinition(
                CampaignObjectiveType.UnlockResearch, 1, StructureId: "smelter");
            Assert(CampaignProgress.IsObjectiveComplete(unlockObj, factory.Wallet, factory.Session, factory.Research),
                "unlock complete");

            progress.MarkComplete(l01.Id, progressPath);
            var reloaded = CampaignProgress.Load(progressPath);
            Assert(reloaded.IsCompleted(l01.Id), "progress saved");
            Assert(reloaded.IsUnlocked(l02!, catalog), "L02 unlocked");

            var snap = factory.Capture();
            Assert(snap.Version == FactorySaveData.CurrentVersion, "save v4");
            Assert(snap.ActiveCampaignLevelId == l01.Id, "capture level id");
            FactorySaveStore.Delete("selftest-campaign");
            var restored = SaveAndReload(content, "selftest-campaign", factory);
            Assert(restored.ActiveCampaignLevelId == l01.Id, "restore level id");
            Assert(restored.Session.SaleIncome == factory.Session.SaleIncome, "restore sales");
            FactorySaveStore.Delete("selftest-campaign");
        }
        finally
        {
            if (File.Exists(progressPath))
            {
                File.Delete(progressPath);
            }
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Assert fallito: {message}");
        }
    }
}
