namespace TIndustry.Shared;

/// <summary>
/// Playable factory slice: miners → belts → forni/assemblatori → core stock.
/// Phase F: optional generators power adjacent craft machines (+20% speed).
/// </summary>
public sealed class FactorySlice
{
    private long nextItemId = 1;
    private readonly List<MinerProducer> miners = [];
    private readonly List<SmelterStub> smelters = [];
    private readonly List<SmelterStub> assemblers = [];
    private readonly List<GeneratorStub> generators = [];

    public FactorySlice(
        FactoryContent content,
        BeltGrid belts,
        IReadOnlySet<GridPosition> coreTiles,
        EconomyWallet? wallet = null,
        ResearchState? research = null,
        EconomySession? session = null)
    {
        Content = content;
        Belts = belts;
        CoreTiles = coreTiles;
        Wallet = wallet ?? new EconomyWallet();
        Research = research ?? ResearchState.CreateNew(content);
        Market = MarketCatalog.FromContent(content);
        Session = session ?? new EconomySession(Wallet.Money);
        BeltDefinition = belts.DefaultDefinition
            ?? content.RequireConveyor("conveyor-basic");
        JunctionDefinition = content.RequireConveyor("junction");
        SplitterDefinition = content.RequireConveyor("splitter");
        SorterDefinition = content.RequireConveyor("sorter");
        BridgeDefinition = content.RequireConveyor("conveyor-bridge");
    }

    public FactoryContent Content { get; }
    public BeltGrid Belts { get; }
    public ConveyorDefinition BeltDefinition { get; }
    public ConveyorDefinition JunctionDefinition { get; }
    public ConveyorDefinition SplitterDefinition { get; }
    public ConveyorDefinition SorterDefinition { get; }
    public ConveyorDefinition BridgeDefinition { get; }
    public IReadOnlySet<GridPosition> CoreTiles { get; }
    public EconomyWallet Wallet { get; }
    public ResearchState Research { get; }
    public MarketCatalog Market { get; }
    public EconomySession Session { get; }
    public string? ActiveCampaignLevelId { get; set; }
    public bool AutoSellAtCore { get; set; }
    public long CoreDeliveredItems { get; private set; }
    public IReadOnlyList<MinerProducer> Miners => miners;
    public IReadOnlyList<SmelterStub> Smelters => smelters;
    public IReadOnlyList<SmelterStub> Assemblers => assemblers;
    public IReadOnlyList<GeneratorStub> Generators => generators;
    public long NextItemId => nextItemId;

    /// <summary>Primary / first miner (compat for HUD).</summary>
    public MinerProducer? Miner => miners.Count > 0 ? miners[0] : null;

    public FactorySliceSaveData Capture()
    {
        var coreOrigin = CoreTiles.Count == 0
            ? new GridPosition(9, 14)
            : new GridPosition(CoreTiles.Min(t => t.X), CoreTiles.Min(t => t.Y));
        var coreSize = CoreTiles.Count == 0
            ? 2
            : Math.Max(1, (int)Math.Round(Math.Sqrt(CoreTiles.Count)));

        return new FactorySliceSaveData
        {
            Version = FactorySliceSaveData.CurrentVersion,
            NextItemId = nextItemId,
            CoreDeliveredItems = CoreDeliveredItems,
            CoreX = coreOrigin.X,
            CoreY = coreOrigin.Y,
            CoreSize = coreSize,
            Money = Wallet.Money,
            Materials = Wallet.MaterialsSnapshot(),
            UnlockedStructures = Research.UnlockedIds.OrderBy(id => id, StringComparer.Ordinal).ToList(),
            SaleIncome = Session.SaleIncome,
            SoldByItem = Session.SoldByItem.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
            StartingMoney = Session.StartingMoney,
            ActiveCampaignLevelId = ActiveCampaignLevelId,
            AutoSellAtCore = AutoSellAtCore,
            Miners = miners.Select(m => new MinerSaveDto
            {
                X = m.Position.X,
                Y = m.Position.Y,
                Direction = m.Direction.ToString(),
                Progress = m.Progress,
                EjectIndex = m.EjectIndex,
                OutputItemId = m.OutputItemId,
                DefinitionId = m.DefinitionId
            }).ToList(),
            Smelters = smelters.Select(CaptureCraft).ToList(),
            Assemblers = assemblers.Select(CaptureCraft).ToList(),
            Generators = generators.Select(g => new GeneratorSaveDto
            {
                X = g.Position.X,
                Y = g.Position.Y,
                Direction = g.Direction.ToString(),
                FuelBuffer = g.FuelBuffer,
                BurnRemaining = g.BurnRemaining,
                FuelConsumed = g.FuelConsumed
            }).ToList(),
            Belts = Belts.Cells.Values.Select(c => new BeltSaveDto
            {
                X = c.Position.X,
                Y = c.Position.Y,
                Direction = c.Direction.ToString(),
                DefinitionId = c.Definition.Id,
                SplitterToggle = c.SplitterToggle,
                BridgePartnerX = c.BridgePartner?.X,
                BridgePartnerY = c.BridgePartner?.Y,
                FilterItemId = c.Kind == LogisticsKind.Sorter ? c.FilterItemId : null,
                Items = c.Items.Select(it => new ItemSaveDto
                {
                    Id = it.Id,
                    ItemId = it.ItemId,
                    Progress = it.Progress,
                    Travel = it.Travel?.ToString()
                }).ToList()
            }).ToList()
        };
    }

    private static CraftSaveDto CaptureCraft(SmelterStub craft) => new()
    {
        X = craft.Position.X,
        Y = craft.Position.Y,
        Direction = craft.Direction.ToString(),
        RecipeId = craft.Recipe.Id,
        DefinitionId = craft.DefinitionId,
        Progress = craft.Progress,
        IsCrafting = craft.IsCrafting,
        InputBuffer = craft.InputBuffer.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal),
        OutputQueue = craft.OutputQueue.ToList(),
        EjectIndex = craft.EjectIndex,
        ItemsCrafted = craft.ItemsCrafted
    };

    public static FactorySlice Restore(FactoryContent content, FactorySliceSaveData data)
    {
        var belts = new BeltGrid();
        foreach (var cell in data.Belts)
        {
            var def = content.FindConveyor(cell.DefinitionId)
                ?? content.RequireConveyor("conveyor-basic");
            var dir = ParseDirection(cell.Direction);
            var items = cell.Items.Select(it => new TransportedItem(
                it.Id,
                it.ItemId,
                it.Progress,
                string.IsNullOrWhiteSpace(it.Travel) ? null : ParseDirection(it.Travel))).ToList();
            GridPosition? partner = null;
            if (cell.BridgePartnerX is { } bx && cell.BridgePartnerY is { } by)
            {
                partner = new GridPosition(bx, by);
            }

            belts.TryRestore(
                new GridPosition(cell.X, cell.Y),
                dir,
                def,
                cell.SplitterToggle,
                items,
                partner,
                cell.FilterItemId);
        }

        var core = CoreStockSink.MakeCoreTiles(
            new GridPosition(data.CoreX, data.CoreY),
            Math.Max(1, data.CoreSize));
        var wallet = new EconomyWallet(data.Money, data.Materials);
        var research = ResearchState.FromSaved(data.UnlockedStructures, content);
        var session = new EconomySession(data.StartingMoney > 0 ? data.StartingMoney : data.Money);
        session.Restore(
            data.StartingMoney > 0 ? data.StartingMoney : data.Money,
            data.SaleIncome,
            data.SoldByItem);
        var slice = new FactorySlice(content, belts, core, wallet, research, session)
        {
            nextItemId = Math.Max(1, data.NextItemId),
            CoreDeliveredItems = Math.Max(0, data.CoreDeliveredItems),
            ActiveCampaignLevelId = data.ActiveCampaignLevelId,
            AutoSellAtCore = data.AutoSellAtCore
        };

        foreach (var m in data.Miners)
        {
            var miner = new MinerProducer(
                new GridPosition(m.X, m.Y),
                ParseDirection(m.Direction),
                outputItemId: m.OutputItemId,
                definitionId: m.DefinitionId);
            miner.RestoreProgress(m.Progress, m.EjectIndex);
            slice.miners.Add(miner);
        }

        foreach (var s in data.Smelters)
        {
            slice.smelters.Add(RestoreCraft(content, s, SmelterStub.SmelterBuildingId));
        }

        foreach (var a in data.Assemblers)
        {
            slice.assemblers.Add(RestoreCraft(content, a, SmelterStub.AssemblerBuildingId));
        }

        foreach (var g in data.Generators)
        {
            var gen = new GeneratorStub(new GridPosition(g.X, g.Y), ParseDirection(g.Direction));
            gen.RestoreFuel(g.FuelBuffer, g.BurnRemaining, g.FuelConsumed);
            slice.generators.Add(gen);
        }

        return slice;
    }

    private static SmelterStub RestoreCraft(FactoryContent content, CraftSaveDto dto, string fallbackId)
    {
        var recipe = content.FindRecipe(dto.RecipeId)
            ?? content.FindRecipe(fallbackId == SmelterStub.AssemblerBuildingId
                ? "craft-copper-wire"
                : "smelt-iron")
            ?? throw new InvalidDataException($"Ricetta '{dto.RecipeId}' mancante nel save.");
        var buildingId = string.IsNullOrWhiteSpace(dto.DefinitionId) ? fallbackId : dto.DefinitionId;
        var craft = new SmelterStub(
            new GridPosition(dto.X, dto.Y),
            ParseDirection(dto.Direction),
            recipe,
            buildingId);
        craft.RestoreCraftState(
            dto.Progress,
            dto.IsCrafting,
            dto.InputBuffer,
            dto.OutputQueue,
            dto.EjectIndex,
            dto.ItemsCrafted);
        return craft;
    }

    private static Direction ParseDirection(string? value) =>
        Enum.TryParse<Direction>(value, ignoreCase: true, out var dir) ? dir : Direction.East;

    /// <summary>Phase C seed: miner → belts → forno → belts → core (plates stock).</summary>
    public static FactorySlice CreatePhaseCDemo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var beltDef = content.RequireConveyor(beltId);
        var recipe = content.FindRecipe("smelt-iron")
            ?? throw new InvalidDataException("Ricetta smelt-iron mancante.");
        var grid = new BeltGrid();
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var slice = new FactorySlice(content, grid, core);
        slice.UnlockGodotSliceDemo();

        // Miner 2×2 at (2,7); forno 2×2 at (6,7).
        slice.TryPlaceMiner(new GridPosition(2, 7), Direction.East);
        slice.TryPlaceSmelter(new GridPosition(6, 7), Direction.East, recipe);

        // Feed: (4,8)(5,8) → into forno west edge (6,8).
        grid.PlacePath(
        [
            new GridPosition(4, 8),
            new GridPosition(5, 8)
        ], beltDef);
        // Orient last feed cell into smelter.
        grid.TryOrient(new GridPosition(5, 8), Direction.East);

        // Output: east of forno then L south into core.
        grid.PlacePath(
        [
            new GridPosition(8, 8),
            new GridPosition(9, 8),
            new GridPosition(10, 8),
            new GridPosition(10, 9),
            new GridPosition(10, 10),
            new GridPosition(10, 11),
            new GridPosition(10, 12),
            new GridPosition(10, 13)
        ], beltDef);

        return slice;
    }

    /// <summary>
    /// Phase D seed: iron miner→forno→plates + copper miner → assemblatore
    /// (<c>craft-copper-wire</c>) → wire → core.
    /// </summary>
    public static FactorySlice CreatePhaseDDemo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var beltDef = content.RequireConveyor(beltId);
        var smelt = content.FindRecipe("smelt-iron")
            ?? throw new InvalidDataException("Ricetta smelt-iron mancante.");
        var wire = content.FindRecipe("craft-copper-wire")
            ?? throw new InvalidDataException("Ricetta craft-copper-wire mancante.");
        var grid = new BeltGrid();
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var slice = new FactorySlice(content, grid, core);
        slice.UnlockGodotSliceDemo();

        // Iron miner (2,7) → forno (6,7) → plates east toward assembler (12,7).
        slice.TryPlaceMiner(new GridPosition(2, 7), Direction.East, "iron-ore");
        slice.TryPlaceSmelter(new GridPosition(6, 7), Direction.East, smelt);
        slice.TryPlaceAssembler(new GridPosition(12, 7), Direction.East, wire);
        // Copper miner south of iron row.
        slice.TryPlaceMiner(new GridPosition(2, 10), Direction.East, "copper-ore");

        // Iron ore feed into forno.
        grid.PlacePath(
        [
            new GridPosition(4, 8),
            new GridPosition(5, 8)
        ], beltDef);
        grid.TryOrient(new GridPosition(5, 8), Direction.East);

        // Plates: forno east → into assembler west edge (12,8).
        grid.PlacePath(
        [
            new GridPosition(8, 8),
            new GridPosition(9, 8),
            new GridPosition(10, 8),
            new GridPosition(11, 8)
        ], beltDef);
        grid.TryOrient(new GridPosition(11, 8), Direction.East);

        // Copper ore: miner → east then north into assembler south edge (12,9).
        grid.PlacePath(
        [
            new GridPosition(4, 11),
            new GridPosition(5, 11),
            new GridPosition(6, 11),
            new GridPosition(7, 11),
            new GridPosition(8, 11),
            new GridPosition(9, 11),
            new GridPosition(10, 11),
            new GridPosition(11, 11),
            new GridPosition(12, 11),
            new GridPosition(12, 10),
            new GridPosition(12, 9)
        ], beltDef);
        grid.TryOrient(new GridPosition(12, 9), Direction.North);

        // Wire out: assembler east (14,8) → south → west into core north (10,13).
        grid.PlacePath(
        [
            new GridPosition(14, 8),
            new GridPosition(14, 9),
            new GridPosition(14, 10),
            new GridPosition(14, 11),
            new GridPosition(14, 12),
            new GridPosition(14, 13),
            new GridPosition(13, 13),
            new GridPosition(12, 13),
            new GridPosition(11, 13),
            new GridPosition(10, 13)
        ], beltDef);
        grid.TryOrient(new GridPosition(10, 13), Direction.South);

        return slice;
    }

    /// <summary>
    /// Phase E seed: Phase D craft loop + junction pass-through on plates + splitter fork on wire.
    /// </summary>
    public static FactorySlice CreatePhaseEDemo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var beltDef = content.RequireConveyor(beltId);
        var junctionDef = content.RequireConveyor("junction");
        var splitterDef = content.RequireConveyor("splitter");
        var smelt = content.FindRecipe("smelt-iron")
            ?? throw new InvalidDataException("Ricetta smelt-iron mancante.");
        var wire = content.FindRecipe("craft-copper-wire")
            ?? throw new InvalidDataException("Ricetta craft-copper-wire mancante.");
        var grid = new BeltGrid();
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var slice = new FactorySlice(content, grid, core);
        slice.UnlockGodotSliceDemo();

        slice.TryPlaceMiner(new GridPosition(2, 7), Direction.East, "iron-ore");
        slice.TryPlaceSmelter(new GridPosition(6, 7), Direction.East, smelt);
        slice.TryPlaceAssembler(new GridPosition(12, 7), Direction.East, wire);
        slice.TryPlaceMiner(new GridPosition(2, 10), Direction.East, "copper-ore");

        // Iron ore → forno.
        grid.PlacePath([new GridPosition(4, 8), new GridPosition(5, 8)], beltDef);
        grid.TryOrient(new GridPosition(5, 8), Direction.East);

        // Plates → junction (10,8) pass-through → (11,8) into assembler.
        grid.PlacePath([new GridPosition(8, 8), new GridPosition(9, 8)], beltDef);
        grid.TryOrient(new GridPosition(9, 8), Direction.East);
        Assert(grid.TryPlaceFree(new GridPosition(10, 8), Direction.East, junctionDef), "junction seed");
        grid.PlacePath([new GridPosition(11, 8)], beltDef);
        grid.TryOrient(new GridPosition(11, 8), Direction.East);

        // Copper → assembler south (same corridor as Phase D).
        grid.PlacePath(
        [
            new GridPosition(4, 11),
            new GridPosition(5, 11),
            new GridPosition(6, 11),
            new GridPosition(7, 11),
            new GridPosition(8, 11),
            new GridPosition(9, 11),
            new GridPosition(10, 11),
            new GridPosition(11, 11),
            new GridPosition(12, 11),
            new GridPosition(12, 10),
            new GridPosition(12, 9)
        ], beltDef);
        grid.TryOrient(new GridPosition(12, 9), Direction.North);

        // Wire → splitter T-fork facing South.
        grid.PlacePath([new GridPosition(14, 8), new GridPosition(14, 9)], beltDef);
        grid.TryOrient(new GridPosition(14, 9), Direction.South);
        Assert(grid.TryPlaceFree(new GridPosition(14, 10), Direction.South, splitterDef), "splitter seed");

        // West arm → core (route east of copper column).
        grid.PlacePath(
        [
            new GridPosition(13, 10),
            new GridPosition(13, 11),
            new GridPosition(13, 12),
            new GridPosition(13, 13),
            new GridPosition(12, 13),
            new GridPosition(11, 13),
            new GridPosition(10, 13)
        ], beltDef);
        grid.TryOrient(new GridPosition(10, 13), Direction.South);

        // East arm dead-end (proves fork).
        grid.PlacePath([new GridPosition(15, 10), new GridPosition(16, 10)], beltDef);
        grid.TryOrient(new GridPosition(16, 10), Direction.East);

        return slice;
    }

    /// <summary>
    /// Phase F seed: Phase E loop + coal miner → generator adjacent to forno (powered craft).
    /// </summary>
    public static FactorySlice CreatePhaseFDemo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var slice = CreatePhaseEDemo(content, beltId);
        var beltDef = slice.BeltDefinition;

        // Generator north of forno (6,7) → adjacent on south edge.
        Assert(slice.TryPlaceGenerator(new GridPosition(6, 5), Direction.East), "generator seed");
        // Coal miner west of generator.
        Assert(slice.TryPlaceMiner(new GridPosition(2, 4), Direction.East, "coal"), "coal miner");
        slice.Belts.PlacePath(
        [
            new GridPosition(4, 5),
            new GridPosition(5, 5)
        ], beltDef);
        slice.Belts.TryOrient(new GridPosition(5, 5), Direction.East);

        return slice;
    }

    /// <summary>
    /// Sorter + bridge demo: Phase F loop + Mindustry sorter fork + thin bridge span (decision 14).
    /// </summary>
    public static FactorySlice CreateSorterBridgeDemo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var slice = CreatePhaseFDemo(content, beltId);
        var beltDef = slice.BeltDefinition;

        // Sorter corridor (north of craft loop): feed → sorter → match east / overflow N+S.
        slice.Belts.PlacePath(
        [
            new GridPosition(16, 2),
            new GridPosition(17, 2)
        ], beltDef);
        slice.Belts.TryOrient(new GridPosition(17, 2), Direction.East);
        Assert(slice.TryPlaceSorter(new GridPosition(18, 2), Direction.East), "sorter seed");
        slice.Belts.PlacePath([new GridPosition(19, 2), new GridPosition(20, 2)], beltDef);
        slice.Belts.TryOrient(new GridPosition(20, 2), Direction.East);
        slice.Belts.PlacePath([new GridPosition(18, 1)], beltDef); // overflow left of East = North
        slice.Belts.TryOrient(new GridPosition(18, 1), Direction.North);
        slice.Belts.PlacePath([new GridPosition(18, 3)], beltDef); // overflow right of East = South
        slice.Belts.TryOrient(new GridPosition(18, 3), Direction.South);

        // Bridge span 2 over mid (16,12)→(18,12); mid stays empty for thin-span visual.
        // Underpass: belt corridor south of mid (proves mid freeness without covering the span).
        Assert(slice.TryPlaceBridge(new GridPosition(16, 12), Direction.East), "bridge seed");
        slice.Belts.PlacePath(
        [
            new GridPosition(15, 12)
        ], beltDef);
        slice.Belts.TryOrient(new GridPosition(15, 12), Direction.East);
        slice.Belts.PlacePath(
        [
            new GridPosition(19, 12),
            new GridPosition(20, 12)
        ], beltDef);
        slice.Belts.TryOrient(new GridPosition(20, 12), Direction.East);
        // Parallel underpass corridor (y=13) — mid (17,12) remains empty for decision-14 thin span.
        slice.Belts.PlacePath(
        [
            new GridPosition(16, 13),
            new GridPosition(17, 13),
            new GridPosition(18, 13)
        ], beltDef);
        slice.Belts.TryOrient(new GridPosition(18, 13), Direction.East);

        return slice;
    }

    /// <summary>Legacy L-belt demo without forno (ore → core).</summary>
    public static FactorySlice CreateSpikeDemo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var beltDef = content.RequireConveyor(beltId);
        var grid = new BeltGrid();
        grid.PlacePath(BuildSpikeLPath(), beltDef);
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var slice = new FactorySlice(content, grid, core);
        slice.UnlockGodotSliceDemo();
        slice.TryPlaceMiner(new GridPosition(2, 7), Direction.East);
        return slice;
    }

    public static List<GridPosition> BuildSpikeLPath()
    {
        var path = new List<GridPosition>();
        for (var x = 4; x <= 10; x++)
        {
            path.Add(new GridPosition(x, 8));
        }

        for (var y = 9; y <= 13; y++)
        {
            path.Add(new GridPosition(10, y));
        }

        return path;
    }

    public bool CanOccupy(GridPosition position) => CanOccupyFootprint(position, 1);

    public bool CanOccupyFootprint(GridPosition origin, int size)
    {
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var tile = new GridPosition(origin.X + x, origin.Y + y);
                if (tile.X < 0 || tile.Y < 0)
                {
                    return false;
                }

                if (CoreTiles.Contains(tile) || Belts.Contains(tile) || IsBuildingTile(tile))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public bool IsBuildingTile(GridPosition tile)
    {
        foreach (var miner in miners)
        {
            foreach (var t in miner.OccupiedTiles())
            {
                if (t.Equals(tile))
                {
                    return true;
                }
            }
        }

        foreach (var craft in CraftMachines())
        {
            if (craft.Occupies(tile))
            {
                return true;
            }
        }

        foreach (var gen in generators)
        {
            if (gen.Occupies(tile))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Unlock all Godot-slice placeables so seeded demos keep working under gates.</summary>
    public void UnlockGodotSliceDemo()
    {
        foreach (var id in ResearchState.GodotSliceStructureIds)
        {
            Research.ForceUnlock(id);
        }
    }

    public bool IsStructureUnlocked(string structureId) => Research.IsUnlocked(structureId);

    public bool TryUnlockStructure(string structureId)
    {
        var structure = Content.FindStructure(structureId);
        return structure is not null && Research.TryUnlock(structure, Wallet);
    }

    /// <summary>Sell stocked materials at dynamic market price (stock before removal).</summary>
    public bool TrySellFromWallet(string itemId, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        var stockBefore = Wallet.MaterialCount(itemId);
        if (stockBefore < amount || !Wallet.TryRemoveMaterial(itemId, amount))
        {
            return false;
        }

        var unitPrice = Market.GetDynamicSellPrice(itemId, stockBefore);
        var total = unitPrice * amount;
        Wallet.AddMoney(total);
        for (var i = 0; i < amount; i++)
        {
            Session.RecordSale(itemId, unitPrice);
        }

        return true;
    }

    public int PreviewSellPrice(string itemId) =>
        Market.GetDynamicSellPrice(itemId, Wallet.MaterialCount(itemId));

    /// <summary>Empty factory around fixed core with campaign wallet/session/research.</summary>
    public static FactorySlice CreateCampaignSlice(
        FactoryContent content,
        CampaignCatalog catalog,
        CampaignLevelDefinition level,
        GridPosition coreOrigin,
        int coreSize = 2)
    {
        var core = CoreStockSink.MakeCoreTiles(coreOrigin, Math.Max(1, coreSize));
        var wallet = catalog.CreateWallet(level);
        var session = new EconomySession(level.StartingMoney);
        var research = ResearchState.CreateNew(content);
        return new FactorySlice(content, new BeltGrid(), core, wallet, research, session)
        {
            ActiveCampaignLevelId = level.Id
        };
    }

    public static string? StructureIdForTool(string toolKey) => toolKey switch
    {
        "belt" => "conveyor-basic",
        "miner" => "miner",
        "smelter" => "smelter",
        "assembler" => "assembler",
        "junction" => "junction",
        "splitter" => "splitter",
        "generator" => "generator",
        "sorter" => "sorter",
        "bridge" => "conveyor-bridge",
        _ => null
    };

    private bool RequireUnlocked(string structureId) => Research.IsUnlocked(structureId);

    public bool TryPlaceBelt(GridPosition position, Direction direction) =>
        RequireUnlocked("conveyor-basic")
        && Belts.TryPlaceFree(position, direction, BeltDefinition, CanOccupy);

    public bool TryPlaceJunction(GridPosition position, Direction direction) =>
        RequireUnlocked("junction")
        && Belts.TryPlaceFree(position, direction, JunctionDefinition, CanOccupy);

    public bool TryPlaceSplitter(GridPosition position, Direction direction) =>
        RequireUnlocked("splitter")
        && Belts.TryPlaceFree(position, direction, SplitterDefinition, CanOccupy);

    public bool TryPlaceSorter(GridPosition position, Direction direction, string? filterItemId = null)
    {
        if (!RequireUnlocked("sorter"))
        {
            return false;
        }

        if (!Belts.TryPlaceFree(position, direction, SorterDefinition, CanOccupy))
        {
            return false;
        }

        if (Belts.TryGet(position, out var cell) && cell.Kind == LogisticsKind.Sorter)
        {
            cell.SetFilterItem(filterItemId ?? BeltGridCell.DefaultSorterFilter);
        }

        return true;
    }

    public bool TryPlaceBridge(GridPosition entry, Direction direction) =>
        RequireUnlocked("conveyor-bridge")
        && Belts.TryPlaceBridge(entry, direction, BridgeDefinition, CanOccupy);

    public bool TryRemoveBelt(GridPosition position) => Belts.TryRemove(position);

    public bool TryPlaceMiner(
        GridPosition origin,
        Direction direction,
        string outputItemId = MinerProducer.DefaultOutputItemId)
    {
        if (!RequireUnlocked("miner"))
        {
            return false;
        }

        if (!CanOccupyFootprint(origin, MinerProducer.Size))
        {
            if (miners.Count == 1 && FootprintClearExcept(origin, MinerProducer.Size, miner: miners[0]))
            {
                miners[0].Relocate(origin, direction);
                return true;
            }

            return false;
        }

        miners.Add(new MinerProducer(origin, direction, outputItemId: outputItemId));
        return true;
    }

    public bool TryPlaceSmelter(GridPosition origin, Direction direction, RecipeDefinition? recipe = null)
    {
        if (!RequireUnlocked("smelter"))
        {
            return false;
        }

        recipe ??= Content.FindRecipe("smelt-iron")
            ?? throw new InvalidDataException("Ricetta smelt-iron mancante.");

        if (!CanOccupyFootprint(origin, SmelterStub.Size))
        {
            if (smelters.Count == 1 && FootprintClearExcept(origin, SmelterStub.Size, craft: smelters[0]))
            {
                smelters[0].Relocate(origin, direction);
                return true;
            }

            return false;
        }

        smelters.Add(new SmelterStub(origin, direction, recipe, SmelterStub.SmelterBuildingId));
        return true;
    }

    public bool TryPlaceAssembler(GridPosition origin, Direction direction, RecipeDefinition? recipe = null)
    {
        if (!RequireUnlocked("assembler"))
        {
            return false;
        }

        recipe ??= Content.FindRecipe("craft-copper-wire")
            ?? throw new InvalidDataException("Ricetta craft-copper-wire mancante.");

        if (!CanOccupyFootprint(origin, SmelterStub.Size))
        {
            if (assemblers.Count == 1 && FootprintClearExcept(origin, SmelterStub.Size, craft: assemblers[0]))
            {
                assemblers[0].Relocate(origin, direction);
                return true;
            }

            return false;
        }

        assemblers.Add(new SmelterStub(origin, direction, recipe, SmelterStub.AssemblerBuildingId));
        return true;
    }

    public bool TryPlaceGenerator(GridPosition origin, Direction direction = Direction.East)
    {
        if (!RequireUnlocked("generator"))
        {
            return false;
        }

        if (!CanOccupyFootprint(origin, GeneratorStub.Size))
        {
            if (generators.Count == 1
                && FootprintClearExcept(origin, GeneratorStub.Size, generator: generators[0]))
            {
                generators[0].Relocate(origin, direction);
                return true;
            }

            return false;
        }

        generators.Add(new GeneratorStub(origin, direction));
        return true;
    }

    public bool TryRemoveBuildingAt(GridPosition tile)
    {
        for (var i = miners.Count - 1; i >= 0; i--)
        {
            foreach (var t in miners[i].OccupiedTiles())
            {
                if (!t.Equals(tile))
                {
                    continue;
                }

                miners.RemoveAt(i);
                return true;
            }
        }

        for (var i = smelters.Count - 1; i >= 0; i--)
        {
            if (!smelters[i].Occupies(tile))
            {
                continue;
            }

            smelters.RemoveAt(i);
            return true;
        }

        for (var i = assemblers.Count - 1; i >= 0; i--)
        {
            if (!assemblers[i].Occupies(tile))
            {
                continue;
            }

            assemblers.RemoveAt(i);
            return true;
        }

        for (var i = generators.Count - 1; i >= 0; i--)
        {
            if (!generators[i].Occupies(tile))
            {
                continue;
            }

            generators.RemoveAt(i);
            return true;
        }

        return false;
    }

    private IEnumerable<SmelterStub> CraftMachines()
    {
        foreach (var s in smelters)
        {
            yield return s;
        }

        foreach (var a in assemblers)
        {
            yield return a;
        }
    }

    private bool FootprintClearExcept(
        GridPosition origin,
        int size,
        MinerProducer? miner = null,
        SmelterStub? craft = null,
        GeneratorStub? generator = null)
    {
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var tile = new GridPosition(origin.X + x, origin.Y + y);
                if (tile.X < 0 || tile.Y < 0 || CoreTiles.Contains(tile) || Belts.Contains(tile))
                {
                    return false;
                }

                foreach (var m in miners)
                {
                    if (miner is not null && ReferenceEquals(m, miner))
                    {
                        continue;
                    }

                    foreach (var t in m.OccupiedTiles())
                    {
                        if (t.Equals(tile))
                        {
                            return false;
                        }
                    }
                }

                foreach (var s in CraftMachines())
                {
                    if (craft is not null && ReferenceEquals(s, craft))
                    {
                        continue;
                    }

                    if (s.Occupies(tile))
                    {
                        return false;
                    }
                }

                foreach (var g in generators)
                {
                    if (generator is not null && ReferenceEquals(g, generator))
                    {
                        continue;
                    }

                    if (g.Occupies(tile))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    public void Tick(float deltaSeconds)
    {
        foreach (var miner in miners)
        {
            miner.Tick(deltaSeconds, Belts, ref nextItemId);
        }

        // Belts advance first so handoffs reach craft/core edges.
        Belts.Tick(deltaSeconds);

        var liveGens = new List<GeneratorStub>();
        foreach (var gen in generators)
        {
            if (gen.Tick(deltaSeconds, Belts))
            {
                liveGens.Add(gen);
            }
        }

        foreach (var craft in CraftMachines())
        {
            var powered = liveGens.Any(g => g.IsAdjacentTo(craft.Position, SmelterStub.Size));
            craft.Tick(deltaSeconds, Belts, ref nextItemId, powered);
        }

        // Second belt tick so freshly emitted items can move the same frame.
        Belts.Tick(0f);
        CoreDeliveredItems += CoreStockSink.Drain(
            Belts, CoreTiles, Wallet, Market, Session, AutoSellAtCore);
    }

    public static void SelfTest(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var slice = CreateSpikeDemo(content);
        const float dt = 1f / 30f;
        for (var i = 0; i < 30 * 40; i++)
        {
            slice.Tick(dt);
            if (slice.CoreDeliveredItems > 0 && slice.Wallet.MaterialCount("iron-ore") > 0)
            {
                return;
            }
        }

        throw new InvalidOperationException(
            "FactorySlice self-test fallito: nessun ferro grezzo arrivato al core entro 40s sim.");
    }

    public static void SelfTestPlaceable(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var grid = new BeltGrid();
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var slice = new FactorySlice(content, grid, core);
        Assert(slice.TryPlaceMiner(new GridPosition(2, 7), Direction.East), "miner");

        Assert(slice.TryPlaceBelt(new GridPosition(4, 8), Direction.East), "place (4,8)");
        Assert(slice.TryPlaceBelt(new GridPosition(5, 8), Direction.East), "place (5,8)");
        Assert(slice.TryPlaceBelt(new GridPosition(6, 8), Direction.East), "place (6,8)");
        Assert(slice.TryPlaceBelt(new GridPosition(7, 8), Direction.East), "place (7,8)");
        Assert(slice.TryPlaceBelt(new GridPosition(8, 8), Direction.East), "place (8,8)");
        Assert(slice.TryPlaceBelt(new GridPosition(9, 8), Direction.East), "place (9,8)");
        Assert(slice.TryPlaceBelt(new GridPosition(10, 8), Direction.South), "corner (10,8)");
        Assert(slice.TryPlaceBelt(new GridPosition(10, 9), Direction.South), "place (10,9)");
        Assert(slice.TryPlaceBelt(new GridPosition(10, 10), Direction.South), "place (10,10)");
        Assert(slice.TryPlaceBelt(new GridPosition(10, 11), Direction.South), "place (10,11)");
        Assert(slice.TryPlaceBelt(new GridPosition(10, 12), Direction.South), "place (10,12)");
        Assert(slice.TryPlaceBelt(new GridPosition(10, 13), Direction.South), "place (10,13)");

        Assert(grid.IsCorner(new GridPosition(10, 8)), "corner detect (10,8)");
        const float dt = 1f / 30f;
        for (var i = 0; i < 30 * 45; i++)
        {
            slice.Tick(dt);
            if (slice.CoreDeliveredItems > 0)
            {
                return;
            }
        }

        throw new InvalidOperationException("Placeable belt self-test: nessun item al core.");
    }

    /// <summary>Phase C: ore through forno yields iron-plate in core stock.</summary>
    public static void SelfTestSmelterLoop(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var slice = CreatePhaseCDemo(content);
        Assert(slice.Smelters.Count == 1, "forno seed");
        Assert(slice.Miners.Count == 1, "miner seed");

        const float dt = 1f / 30f;
        // Mining 2s + belt + smelt 2s + belt — budget ~90s sim.
        for (var i = 0; i < 30 * 90; i++)
        {
            slice.Tick(dt);
            if (slice.Wallet.MaterialCount("iron-plate") > 0)
            {
                return;
            }
        }

        throw new InvalidOperationException(
            "Smelter loop self-test: nessuna lastra di ferro al core entro 90s sim.");
    }

    /// <summary>Phase D: plates + copper ore through assembler yield copper-wire in core.</summary>
    public static void SelfTestAssemblerLoop(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var slice = CreatePhaseDDemo(content);
        Assert(slice.Smelters.Count == 1, "forno seed");
        Assert(slice.Assemblers.Count == 1, "assemblatore seed");
        Assert(slice.Miners.Count == 2, "iron+copper miners");
        Assert(slice.Miners.Any(m => m.OutputItemId == "copper-ore"), "copper miner");
        Assert(slice.TryPlaceAssembler(new GridPosition(16, 4), Direction.South), "place second assembler");
        Assert(slice.TryRemoveBuildingAt(new GridPosition(16, 4)), "remove assembler");

        const float dt = 1f / 30f;
        // Iron mine+smelt + copper mine + craft 1.5s + belts — budget ~120s sim.
        for (var i = 0; i < 30 * 120; i++)
        {
            slice.Tick(dt);
            if (slice.Wallet.MaterialCount("copper-wire") > 0)
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
        var slice = CreatePhaseEDemo(content);
        Assert(slice.Belts.Cells.Values.Any(c => c.Kind == LogisticsKind.Junction), "seed junction");
        Assert(slice.Belts.Cells.Values.Any(c => c.Kind == LogisticsKind.Splitter), "seed splitter");
        Assert(slice.TryPlaceJunction(new GridPosition(18, 4), Direction.East), "place junction");
        Assert(slice.TryPlaceSplitter(new GridPosition(18, 5), Direction.South), "place splitter");
        Assert(slice.TryRemoveBelt(new GridPosition(18, 4)), "remove junction");
        Assert(slice.TryRemoveBelt(new GridPosition(18, 5)), "remove splitter");

        for (var i = 0; i < 30 * 120; i++)
        {
            slice.Tick(dt);
            if (slice.Wallet.MaterialCount("copper-wire") > 0)
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
        var slice = new FactorySlice(content, grid, core);
        slice.UnlockGodotSliceDemo();

        Assert(slice.TryPlaceSmelter(new GridPosition(4, 4), Direction.East, recipe), "smelter");
        Assert(slice.TryPlaceGenerator(new GridPosition(4, 2), Direction.East), "gen north of forno");
        Assert(slice.Generators[0].IsAdjacentTo(slice.Smelters[0].Position, SmelterStub.Size), "adjacent");
        Assert(slice.Generators[0].TryAcceptFuel("coal"), "fuel 1");
        Assert(slice.Generators[0].TryAcceptFuel("coal"), "fuel 2");

        var sm = slice.Smelters[0];
        Assert(sm.TryAccept("iron-ore") && sm.TryAccept("iron-ore"), "ore for craft");

        const float dt = 1f / 30f;
        var sawPowered = false;
        for (var i = 0; i < 30 * 8; i++)
        {
            slice.Tick(dt);
            if (sm.IsPowered)
            {
                sawPowered = true;
                break;
            }
        }

        Assert(sawPowered, "forno powered while generator burns");

        // Speed: powered progress > unpowered over same window.
        var coldGrid = new BeltGrid();
        var cold = new FactorySlice(content, coldGrid, core);
        cold.UnlockGodotSliceDemo();
        Assert(cold.TryPlaceSmelter(new GridPosition(1, 1), Direction.East, recipe), "cold smelter");
        var coldSm = cold.Smelters[0];
        Assert(coldSm.TryAccept("iron-ore") && coldSm.TryAccept("iron-ore"), "cold ore");

        // Reset powered smelter craft.
        var hot = slice;
        var hotSm = hot.Smelters[0];
        if (!hotSm.IsCrafting)
        {
            Assert(hotSm.TryAccept("iron-ore") && hotSm.TryAccept("iron-ore"), "hot ore");
        }

        // Ensure gen still fueled.
        hot.Generators[0].TryAcceptFuel("coal");
        var hotBefore = hotSm.Progress;
        var coldBefore = coldSm.Progress;
        for (var i = 0; i < 15; i++)
        {
            hot.Tick(dt);
            cold.Tick(dt);
        }

        var hotDelta = hotSm.Progress - hotBefore;
        var coldDelta = coldSm.Progress - coldBefore;
        if (hotSm.IsCrafting && coldSm.IsCrafting && coldDelta > 0.001f)
        {
            Assert(hotDelta > coldDelta * 1.05f,
                $"powered faster (hotΔ={hotDelta:F3} coldΔ={coldDelta:F3})");
        }

        Assert(slice.TryPlaceGenerator(new GridPosition(16, 4), Direction.South), "place second gen");
        Assert(slice.TryRemoveBuildingAt(new GridPosition(16, 4)), "remove gen");

        var demo = CreatePhaseFDemo(content);
        Assert(demo.Generators.Count == 1, "seed generator");
        Assert(demo.Miners.Any(m => m.OutputItemId == "coal"), "coal miner");
        Assert(demo.Smelters.Count == 1 && demo.Assemblers.Count == 1, "craft chain");

        for (var i = 0; i < 30 * 150; i++)
        {
            demo.Tick(dt);
            if (demo.Wallet.MaterialCount("copper-wire") > 0)
            {
                return;
            }
        }

        throw new InvalidOperationException(
            "Phase F self-test: nessun filo al core entro 150s sim (power seed).");
    }

    /// <summary>Save/load round-trip: capture Phase F mid-sim, restore, keep wire delivery.</summary>
    public static void SelfTestSaveLoad(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var slice = CreatePhaseFDemo(content);
        const float dt = 1f / 30f;
        for (var i = 0; i < 30 * 25; i++)
        {
            slice.Tick(dt);
        }

        var snap = slice.Capture();
        Assert(snap.Miners.Count >= 3, "capture miners");
        Assert(snap.Generators.Count == 1, "capture generator");
        Assert(snap.Belts.Count > 10, "capture belts");
        Assert(snap.NextItemId >= 1, "nextItemId");

        FactorySliceSaveStore.Delete("selftest-tmp");
        FactorySliceSaveStore.Save("selftest-tmp", snap);
        Assert(FactorySliceSaveStore.Exists("selftest-tmp"), "slot exists");
        var loaded = FactorySliceSaveStore.Load("selftest-tmp");
        var restored = Restore(content, loaded);

        Assert(restored.Miners.Count == slice.Miners.Count, "miners count");
        Assert(restored.Smelters.Count == slice.Smelters.Count, "smelters");
        Assert(restored.Assemblers.Count == slice.Assemblers.Count, "assemblers");
        Assert(restored.Generators.Count == slice.Generators.Count, "generators");
        Assert(restored.Belts.Count == slice.Belts.Count, "belts");
        Assert(restored.Wallet.Money == slice.Wallet.Money, "money");
        Assert(restored.NextItemId == slice.NextItemId, "nextItemId match");

        // Loaded generator keeps fuel if any was captured.
        if (snap.Generators[0].FuelBuffer > 0 || snap.Generators[0].BurnRemaining > 0f)
        {
            Assert(
                restored.Generators[0].FuelBuffer == snap.Generators[0].FuelBuffer
                && Math.Abs(restored.Generators[0].BurnRemaining - snap.Generators[0].BurnRemaining) < 0.01f,
                "generator fuel restore");
        }

        // Continue sim after load still delivers wire.
        for (var i = 0; i < 30 * 150; i++)
        {
            restored.Tick(dt);
            if (restored.Wallet.MaterialCount("copper-wire") > 0)
            {
                FactorySliceSaveStore.Delete("selftest-tmp");
                return;
            }
        }

        FactorySliceSaveStore.Delete("selftest-tmp");
        throw new InvalidOperationException(
            "Save/load self-test: nessun filo al core dopo restore entro 150s sim.");
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

        Assert(bridgeGrid.TryInsert(new GridPosition(0, 0), new TransportedItem(10, "iron-plate")), "bridge feed");
        var teleported = false;
        for (var i = 0; i < 30 * 20; i++)
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
        Assert(bridgeGrid.TryRemove(new GridPosition(1, 0)), "paired remove");
        Assert(!bridgeGrid.Contains(new GridPosition(1, 0)) && !bridgeGrid.Contains(new GridPosition(3, 0)),
            "both bridge ends removed");

        // Place API + save/load partner/filter.
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var slice = new FactorySlice(content, new BeltGrid(), core);
        slice.UnlockGodotSliceDemo();
        Assert(slice.TryPlaceSorter(new GridPosition(4, 4), Direction.East, "iron-plate"), "TryPlaceSorter");
        Assert(slice.Belts.TryGet(new GridPosition(4, 4), out var placedSorter)
            && placedSorter.FilterItemId == "iron-plate", "placed filter");
        Assert(slice.TryPlaceBridge(new GridPosition(6, 4), Direction.East), "TryPlaceBridge");
        Assert(slice.TryRemoveBelt(new GridPosition(6, 4)), "remove bridge pair");
        Assert(!slice.Belts.Contains(new GridPosition(6, 4)) && !slice.Belts.Contains(new GridPosition(8, 4)),
            "paired gone");
        Assert(slice.TryPlaceBridge(new GridPosition(6, 4), Direction.East), "re-place bridge");

        var snap = slice.Capture();
        Assert(snap.Belts.Any(b => b.DefinitionId == "sorter" && b.FilterItemId == "iron-plate"),
            "capture filter");
        Assert(snap.Belts.Any(b =>
                b.DefinitionId == "conveyor-bridge"
                && b.BridgePartnerX is not null
                && b.BridgePartnerY is not null),
            "capture bridge partner");
        FactorySliceSaveStore.Delete("selftest-sorter-bridge");
        FactorySliceSaveStore.Save("selftest-sorter-bridge", snap);
        var restored = Restore(content, FactorySliceSaveStore.Load("selftest-sorter-bridge"));
        Assert(restored.Belts.Cells.Values.Any(c =>
                c.Kind == LogisticsKind.Sorter && c.FilterItemId == "iron-plate"),
            "restore filter");
        Assert(restored.Belts.Cells.Values.Count(c => c.Kind == LogisticsKind.Bridge) == 2,
            "restore bridge pair");
        FactorySliceSaveStore.Delete("selftest-sorter-bridge");

        // Full demo seed still delivers wire (HUD/save/power kept).
        var demo = CreateSorterBridgeDemo(content);
        Assert(demo.Belts.Cells.Values.Any(c => c.Kind == LogisticsKind.Sorter), "seed sorter");
        Assert(demo.Belts.Cells.Values.Count(c => c.Kind == LogisticsKind.Bridge) == 2, "seed bridge");
        Assert(demo.Generators.Count == 1, "seed keeps generator");

        for (var i = 0; i < 30 * 150; i++)
        {
            demo.Tick(dt);
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
        var slice = new FactorySlice(content, new BeltGrid(), core);

        var graph = TechTreeLayout.Build(content);
        Assert(graph.Nodes.Count >= 9, "graph has structures");
        Assert(graph.Edges.Count >= 1, "graph has prereq edges");
        Assert(graph.Nodes.Any(n => n.Structure.Id == "smelter"), "smelter in graph");
        var path = TechTreeLayout.CollectRelatedIds(graph, "assembler");
        Assert(path.Contains("assembler"), "path includes selected");
        Assert(TechTreeLayout.ClampZoom(0.1f) == TechTreeLayout.MinZoom, "zoom clamp lo");
        Assert(TechTreeLayout.ClampZoom(9f) == TechTreeLayout.MaxZoom, "zoom clamp hi");

        Assert(slice.Research.IsUnlocked("conveyor-basic"), "default belt");
        Assert(slice.Research.IsUnlocked("miner"), "default miner");
        Assert(!slice.Research.IsUnlocked("smelter"), "forno locked");
        Assert(!slice.Research.IsUnlocked("junction"), "junction locked");
        Assert(!slice.TryPlaceSmelter(new GridPosition(4, 4), Direction.East), "place forno gated");
        Assert(!slice.TryPlaceJunction(new GridPosition(5, 5), Direction.East), "place junction gated");

        var smelter = content.RequireStructure("smelter");
        Assert(slice.Research.GetNodeState(smelter) == ResearchNodeState.Available, "smelter available");
        Assert(!slice.Research.TryUnlock(smelter, slice.Wallet), "cannot afford smelter");

        slice.Wallet.AddMoney(500);
        slice.Wallet.AddMaterial("iron-plate", 40);
        Assert(slice.TryUnlockStructure("smelter"), "unlock smelter");
        Assert(slice.Research.IsUnlocked("smelter"), "smelter unlocked");
        Assert(slice.Wallet.Money == 250, "spent 250");
        Assert(slice.Wallet.MaterialCount("iron-plate") == 25, "spent 15 plates");
        Assert(slice.TryPlaceSmelter(new GridPosition(4, 4), Direction.East), "place forno after unlock");

        var assembler = content.RequireStructure("assembler");
        Assert(slice.Research.GetNodeState(assembler) == ResearchNodeState.Available, "assembler avail");
        // Need copper-ore for assembler unlock.
        slice.Wallet.AddMoney(500);
        slice.Wallet.AddMaterial("iron-plate", 30);
        slice.Wallet.AddMaterial("copper-ore", 10);
        Assert(slice.TryUnlockStructure("assembler"), "unlock assembler");

        Assert(!slice.Research.IsUnlocked("splitter"), "splitter still locked");
        slice.Wallet.AddMoney(2000);
        slice.Wallet.AddMaterial("iron-plate", 100);
        Assert(slice.TryUnlockStructure("junction"), "unlock junction");
        Assert(slice.TryUnlockStructure("splitter"), "unlock splitter after junction");
        Assert(slice.TryUnlockStructure("sorter"), "unlock sorter");
        Assert(slice.TryUnlockStructure("conveyor-bridge"), "unlock bridge");
        Assert(slice.TryUnlockStructure("generator"), "unlock generator");

        var snap = slice.Capture();
        Assert(snap.UnlockedStructures.Contains("smelter"), "capture smelter");
        Assert(snap.UnlockedStructures.Contains("conveyor-bridge"), "capture bridge");
        Assert(snap.Version == FactorySliceSaveData.CurrentVersion, "save v2");

        FactorySliceSaveStore.Delete("selftest-research");
        FactorySliceSaveStore.Save("selftest-research", snap);
        var restored = Restore(content, FactorySliceSaveStore.Load("selftest-research"));
        Assert(restored.Research.IsUnlocked("smelter"), "restore smelter");
        Assert(restored.Research.IsUnlocked("sorter"), "restore sorter");
        Assert(restored.Research.IsUnlocked("conveyor-basic"), "restore defaults");
        Assert(restored.Wallet.Money == slice.Wallet.Money, "money after unlocks");
        FactorySliceSaveStore.Delete("selftest-research");

        // Demo seed still unlocks all slice tools.
        var demo = CreateSorterBridgeDemo(content);
        Assert(demo.Research.IsUnlocked("sorter"), "demo sorter");
        Assert(demo.Research.IsUnlocked("generator"), "demo generator");
    }

    /// <summary>Mercato sell: dynamic price, session ledger, save v3 round-trip.</summary>
    public static void SelfTestMercato(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var slice = new FactorySlice(content, new BeltGrid(), core);

        Assert(slice.Market.Items.Count >= 4, "market items");
        var listino = slice.Market.GetSellPrice("iron-ore");
        Assert(listino >= 1, "listino ore");
        Assert(slice.Market.GetDynamicSellPrice("iron-ore", 1) == listino
            || slice.Market.GetDynamicSellPrice("iron-ore", 1) >= 1, "low stock ≈ listino");

        var high = slice.Market.GetDynamicSellPrice("iron-ore", 80);
        Assert(high <= listino, "high stock softens");

        Assert(!slice.TrySellFromWallet("iron-ore", 1), "empty stock fail");
        Assert(slice.Wallet.Money == 0, "money unchanged");

        slice.Wallet.AddMaterial("iron-ore", 5);
        var price1 = slice.PreviewSellPrice("iron-ore");
        Assert(slice.TrySellFromWallet("iron-ore", 1), "sell 1");
        Assert(slice.Wallet.MaterialCount("iron-ore") == 4, "stock -1");
        Assert(slice.Wallet.Money == price1, "money +price");
        Assert(slice.Session.SaleIncome == price1, "sale income");
        Assert(slice.Session.SoldByItem.GetValueOrDefault("iron-ore") == 1, "sold count");

        var moneyBefore = slice.Wallet.Money;
        var remaining = slice.Wallet.MaterialCount("iron-ore");
        var bulkPrice = slice.Market.GetDynamicSellPrice("iron-ore", remaining);
        Assert(slice.TrySellFromWallet("iron-ore", remaining), "sell tutti");
        Assert(slice.Wallet.MaterialCount("iron-ore") == 0, "ore empty");
        Assert(slice.Wallet.Money == moneyBefore + bulkPrice * remaining, "tutti money");
        Assert(slice.Session.SoldByItem.GetValueOrDefault("iron-ore") == 5, "sold 5");

        // Unlock still works after sell income.
        slice.Wallet.AddMaterial("iron-plate", 40);
        slice.Wallet.AddMoney(Math.Max(0, 250 - slice.Wallet.Money));
        Assert(slice.TryUnlockStructure("smelter"), "unlock after sell");

        var snap = slice.Capture();
        Assert(snap.Version == FactorySliceSaveData.CurrentVersion, "save v3");
        Assert(snap.SaleIncome == slice.Session.SaleIncome, "capture sale income");
        Assert(snap.SoldByItem.GetValueOrDefault("iron-ore") == 5, "capture sold");

        FactorySliceSaveStore.Delete("selftest-mercato");
        FactorySliceSaveStore.Save("selftest-mercato", snap);
        var restored = Restore(content, FactorySliceSaveStore.Load("selftest-mercato"));
        Assert(restored.Wallet.Money == slice.Wallet.Money, "restore money");
        Assert(restored.Session.SaleIncome == slice.Session.SaleIncome, "restore income");
        Assert(restored.Session.SoldByItem.GetValueOrDefault("iron-ore") == 5, "restore sold");
        Assert(restored.Research.IsUnlocked("smelter"), "restore research");
        FactorySliceSaveStore.Delete("selftest-mercato");
    }

    /// <summary>Auto-sell at core: liquidate on drain; toggle persists in save.</summary>
    public static void SelfTestAutoSell(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var slice = CreateSpikeDemo(content);
        slice.AutoSellAtCore = true;

        var moneyBefore = slice.Wallet.Money;
        var stockBefore = slice.Wallet.MaterialCount("iron-ore");
        const float dt = 1f / 30f;
        var soldViaAuto = false;
        for (var i = 0; i < 30 * 45; i++)
        {
            slice.Tick(dt);
            if (slice.CoreDeliveredItems > 0
                && slice.Session.SaleIncome > 0
                && slice.Wallet.Money > moneyBefore)
            {
                soldViaAuto = true;
                break;
            }
        }

        Assert(soldViaAuto, "auto-sell liquidates core deliveries");
        Assert(slice.Wallet.MaterialCount("iron-ore") <= stockBefore, "stock not forced up");
        Assert(slice.Session.SoldByItem.GetValueOrDefault("iron-ore") >= 1, "session recorded");

        var snap = slice.Capture();
        Assert(snap.AutoSellAtCore, "capture auto-sell");
        FactorySliceSaveStore.Delete("selftest-autosell");
        FactorySliceSaveStore.Save("selftest-autosell", snap);
        var restored = Restore(content, FactorySliceSaveStore.Load("selftest-autosell"));
        Assert(restored.AutoSellAtCore, "restore auto-sell");
        FactorySliceSaveStore.Delete("selftest-autosell");

        // OFF path still stocks (stock-first default).
        var stockSlice = CreateSpikeDemo(content);
        Assert(!stockSlice.AutoSellAtCore, "default OFF");
        moneyBefore = stockSlice.Wallet.Money;
        for (var i = 0; i < 30 * 45; i++)
        {
            stockSlice.Tick(dt);
            if (stockSlice.CoreDeliveredItems > 0 && stockSlice.Wallet.MaterialCount("iron-ore") > 0)
            {
                Assert(stockSlice.Session.SaleIncome == 0, "no auto sale when OFF");
                Assert(stockSlice.Wallet.Money == moneyBefore, "money unchanged when OFF");
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

            var slice = CreateCampaignSlice(content, catalog, l01, new GridPosition(9, 14));
            Assert(slice.Wallet.Money == l01.StartingMoney, "start money");
            Assert(slice.Session.SaleIncome == 0, "sale income 0");
            Assert(slice.ActiveCampaignLevelId == l01.Id, "active level");

            // sellItem + earnMoney via Mercato path
            slice.Wallet.AddMaterial("iron-ore", 10);
            Assert(slice.TrySellFromWallet("iron-ore", 3), "sell 3 ore");
            Assert(CampaignProgress.IsObjectiveComplete(
                l01.Objectives!.First(o => o.Type == CampaignObjectiveType.SellItem),
                slice.Wallet, slice.Session, slice.Research), "sellItem done");
            // May need more sells for $24 earnMoney depending on price
            while (!CampaignProgress.AreAllObjectivesComplete(
                       l01, slice.Wallet, slice.Session, slice.Research)
                   && slice.Wallet.MaterialCount("iron-ore") > 0)
            {
                Assert(slice.TrySellFromWallet("iron-ore", 1), "sell more");
            }

            if (!CampaignProgress.AreAllObjectivesComplete(
                    l01, slice.Wallet, slice.Session, slice.Research))
            {
                // Force earnMoney if soft prices didn't reach $24
                var earn = l01.Objectives!.First(o => o.Type == CampaignObjectiveType.EarnMoney);
                while (slice.Session.SaleIncome < earn.Amount)
                {
                    slice.Wallet.AddMaterial("iron-ore", 1);
                    Assert(slice.TrySellFromWallet("iron-ore", 1), "force sell");
                }
            }

            Assert(CampaignProgress.AreAllObjectivesComplete(
                l01, slice.Wallet, slice.Session, slice.Research), "L01 complete");

            // stockItem
            var stockObj = new CampaignObjectiveDefinition(
                CampaignObjectiveType.StockItem, 5, ItemId: "iron-ore", Label: "test stock");
            slice.Wallet.AddMaterial("iron-ore", 5);
            Assert(CampaignProgress.IsObjectiveComplete(stockObj, slice.Wallet, slice.Session, slice.Research),
                "stock complete");

            // unlockResearch
            slice.Wallet.AddMoney(500);
            slice.Wallet.AddMaterial("iron-plate", 40);
            Assert(slice.TryUnlockStructure("smelter"), "unlock smelter");
            var unlockObj = new CampaignObjectiveDefinition(
                CampaignObjectiveType.UnlockResearch, 1, StructureId: "smelter");
            Assert(CampaignProgress.IsObjectiveComplete(unlockObj, slice.Wallet, slice.Session, slice.Research),
                "unlock complete");

            progress.MarkComplete(l01.Id, progressPath);
            var reloaded = CampaignProgress.Load(progressPath);
            Assert(reloaded.IsCompleted(l01.Id), "progress saved");
            Assert(reloaded.IsUnlocked(l02!, catalog), "L02 unlocked");

            var snap = slice.Capture();
            Assert(snap.Version == FactorySliceSaveData.CurrentVersion, "save v4");
            Assert(snap.ActiveCampaignLevelId == l01.Id, "capture level id");
            FactorySliceSaveStore.Delete("selftest-campaign");
            FactorySliceSaveStore.Save("selftest-campaign", snap);
            var restored = Restore(content, FactorySliceSaveStore.Load("selftest-campaign"));
            Assert(restored.ActiveCampaignLevelId == l01.Id, "restore level id");
            Assert(restored.Session.SaleIncome == slice.Session.SaleIncome, "restore sales");
            FactorySliceSaveStore.Delete("selftest-campaign");
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
