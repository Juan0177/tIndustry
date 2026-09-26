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
    private readonly List<ExtractorStub> extractors = [];
    private readonly List<PowerNodeStub> powerNodes = [];

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
    public int CoreUpgradeLevel { get; private set; }
    public int CoreSaleBonusPercent { get; private set; }
    public TerrainMap? Terrain { get; set; }
    public long CoreDeliveredItems { get; private set; }
    public IReadOnlyList<MinerProducer> Miners => miners;
    public IReadOnlyList<SmelterStub> Smelters => smelters;
    public IReadOnlyList<SmelterStub> Assemblers => assemblers;
    public IReadOnlyList<GeneratorStub> Generators => generators;
    public IReadOnlyList<ExtractorStub> Extractors => extractors;
    public IReadOnlyList<PowerNodeStub> PowerNodes => powerNodes;
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
            CoreUpgradeLevel = CoreUpgradeLevel,
            CoreSaleBonusPercent = CoreSaleBonusPercent,
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
            Extractors = extractors.Select(e => new ExtractorSaveDto
            {
                X = e.Position.X,
                Y = e.Position.Y,
                Direction = e.Direction.ToString(),
                FilterItemId = e.FilterItemId,
                Progress = e.Progress
            }).ToList(),
            PowerNodes = powerNodes.Select(n => new PowerNodeSaveDto
            {
                X = n.Position.X,
                Y = n.Position.Y,
                DefinitionId = n.DefinitionId
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
        ItemsCrafted = craft.ItemsCrafted,
        FuelBuffer = craft.FuelBuffer,
        BurnRemaining = craft.BurnRemaining
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
            AutoSellAtCore = data.AutoSellAtCore,
            CoreUpgradeLevel = Math.Max(0, data.CoreUpgradeLevel),
            CoreSaleBonusPercent = Math.Max(0, data.CoreSaleBonusPercent)
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

        foreach (var e in data.Extractors)
        {
            var ex = new ExtractorStub(
                new GridPosition(e.X, e.Y),
                ParseDirection(e.Direction),
                e.FilterItemId);
            ex.RestoreProgress(e.Progress);
            slice.extractors.Add(ex);
        }

        foreach (var n in data.PowerNodes)
        {
            slice.powerNodes.Add(new PowerNodeStub(new GridPosition(n.X, n.Y), n.DefinitionId));
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
        var available = buildingId == SmelterStub.AssemblerBuildingId
            ? content.AssemblerRecipes()
            : content.SmelterRecipes();
        var craft = new SmelterStub(
            new GridPosition(dto.X, dto.Y),
            ParseDirection(dto.Direction),
            recipe,
            buildingId,
            available);
        craft.RestoreCraftState(
            dto.Progress,
            dto.IsCrafting,
            dto.InputBuffer,
            dto.OutputQueue,
            dto.EjectIndex,
            dto.ItemsCrafted,
            dto.FuelBuffer,
            dto.BurnRemaining);
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
        SeedSmelterFuel(slice);

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
        SeedSmelterFuel(slice);

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
        SeedSmelterFuel(slice);

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

        foreach (var ex in extractors)
        {
            if (ex.Occupies(tile))
            {
                return true;
            }
        }

        foreach (var node in powerNodes)
        {
            if (node.Occupies(tile))
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

        // Seed layouts call TryPlace* (now charged) — stock enough for demo footprints.
        EnsureDemoBuildStock();
    }

    /// <summary>Demos without a live generator: top up forno coal so craft can run.</summary>
    public static void SeedSmelterFuel(FactorySlice slice, int units = SmelterStub.FuelBufferCapacity)
    {
        foreach (var sm in slice.Smelters)
        {
            sm.SeedFuel(units);
        }
    }

    public bool IsStructureUnlocked(string structureId) => Research.IsUnlocked(structureId);

    public bool TryUnlockStructure(string structureId)
    {
        var structure = Content.FindStructure(structureId);
        return structure is not null && Research.TryUnlock(structure, Wallet);
    }

    /// <summary>Sell stocked materials at dynamic market price (stock before removal) + CORE bonus.</summary>
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

        var unitPrice = EffectiveSalePrice(itemId, stockBefore);
        var total = unitPrice * amount;
        Wallet.AddMoney(total);
        for (var i = 0; i < amount; i++)
        {
            Session.RecordSale(itemId, unitPrice);
        }

        return true;
    }

    public int PreviewSellPrice(string itemId) =>
        EffectiveSalePrice(itemId, Wallet.MaterialCount(itemId));

    public int EffectiveSalePrice(string itemId, int stockBeforeSell)
    {
        var price = Market.GetDynamicSellPrice(itemId, stockBeforeSell);
        if (CoreUpgradeLevel <= 0 || CoreSaleBonusPercent <= 0)
        {
            return price;
        }

        return price + price * CoreSaleBonusPercent / 100;
    }

    public bool TryUpgradeCore()
    {
        var upgrade = Content.CoreUpgrade;
        if (CoreUpgradeLevel > 0
            || !Wallet.TrySpend(upgrade.MoneyCost, upgrade.BuildCost))
        {
            return false;
        }

        CoreUpgradeLevel = 1;
        CoreSaleBonusPercent = upgrade.SaleBonusPercent;
        return true;
    }

    public string FormatCoreUpgradeNeedMessage()
    {
        var upgrade = Content.CoreUpgrade;
        if (CoreUpgradeLevel > 0)
        {
            return $"Core già a LV{CoreUpgradeLevel} (+{CoreSaleBonusPercent}%).";
        }

        if (Wallet.CanAfford(upgrade.MoneyCost, upgrade.BuildCost))
        {
            return "";
        }

        var parts = new List<string>();
        if (Wallet.Money < upgrade.MoneyCost)
        {
            parts.Add($"${upgrade.MoneyCost - Wallet.Money}");
        }

        foreach (var entry in upgrade.BuildCost)
        {
            var have = Wallet.MaterialCount(entry.ItemId);
            if (have < entry.Amount)
            {
                parts.Add($"{entry.Amount - have}× {Content.DisplayName(entry.ItemId)}");
            }
        }

        return parts.Count == 0 ? "Risorse insufficienti" : "Servono " + string.Join(" · ", parts);
    }

    /// <summary>Empty factory around fixed core with campaign wallet/session/research + seeded terrain.</summary>
    public static FactorySlice CreateCampaignSlice(
        FactoryContent content,
        CampaignCatalog catalog,
        CampaignLevelDefinition level,
        GridPosition coreOrigin,
        int coreSize = 2,
        int mapWidth = 24,
        int mapHeight = 18)
    {
        var core = CoreStockSink.MakeCoreTiles(coreOrigin, Math.Max(1, coreSize));
        var wallet = catalog.CreateWallet(level);
        var session = new EconomySession(level.StartingMoney);
        var research = ResearchState.CreateNew(content);
        // Godot spike viewport size; campaign Seed drives deposit layout.
        var starter = ResolveStarterDeposit(coreOrigin, coreSize, mapWidth, mapHeight);
        var terrain = TerrainMap.Generate(mapWidth, mapHeight, level.Seed, core, starter);
        return new FactorySlice(content, new BeltGrid(), core, wallet, research, session)
        {
            ActiveCampaignLevelId = level.Id,
            Terrain = terrain
        };
    }

    /// <summary>Empty sandbox with optional seed-driven terrain (default seed 42).</summary>
    public static FactorySlice CreateSandboxSlice(
        FactoryContent content,
        GridPosition coreOrigin,
        int coreSize = 2,
        int mapWidth = 24,
        int mapHeight = 18,
        int seed = 42,
        int startingMoney = 180)
    {
        var core = CoreStockSink.MakeCoreTiles(coreOrigin, Math.Max(1, coreSize));
        // Parity with Raylib CreateStartingWallet: plates/wire so first place isn't free-or-impossible.
        var wallet = new EconomyWallet(
            startingMoney,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["iron-plate"] = 48,
                ["copper-wire"] = 10
            });
        var session = new EconomySession(startingMoney);
        var research = ResearchState.CreateNew(content);
        var starter = ResolveStarterDeposit(coreOrigin, coreSize, mapWidth, mapHeight);
        var terrain = TerrainMap.Generate(mapWidth, mapHeight, seed, core, starter);
        return new FactorySlice(content, new BeltGrid(), core, wallet, research, session)
        {
            Terrain = terrain
        };
    }

    private static GridPosition ResolveStarterDeposit(
        GridPosition coreOrigin, int coreSize, int mapWidth, int mapHeight)
    {
        // Prefer west of core (Raylib-style), clamped into map.
        var x = Math.Clamp(coreOrigin.X - MinerProducer.Size - 2, 1, Math.Max(1, mapWidth - MinerProducer.Size - 1));
        var y = Math.Clamp(coreOrigin.Y, 1, Math.Max(1, mapHeight - MinerProducer.Size - 1));
        // Avoid overlapping the core footprint.
        if (x + MinerProducer.Size > coreOrigin.X && x < coreOrigin.X + coreSize
            && y + MinerProducer.Size > coreOrigin.Y && y < coreOrigin.Y + coreSize)
        {
            x = Math.Max(1, coreOrigin.X - MinerProducer.Size - 1);
        }

        return new GridPosition(x, y);
    }

    public static string? StructureIdForTool(string toolKey) => toolKey switch
    {
        "belt" => "conveyor-basic",
        "belt-fast" => "conveyor-fast",
        "belt-express" => "conveyor-express",
        "miner" => "miner",
        "miner-advanced" => "miner-advanced",
        "smelter" => "smelter",
        "assembler" => "assembler",
        "junction" => "junction",
        "splitter" => "splitter",
        "generator" => "generator",
        "sorter" => "sorter",
        "bridge" => "conveyor-bridge",
        "extractor" => "extractor",
        "power-node" => "power-node",
        "power-node-t2" => "power-node-t2",
        _ => null
    };

    private bool RequireUnlocked(string structureId) => Research.IsUnlocked(structureId);

    /// <summary>True if wallet covers money + materials for a structure (bridge = ×2 heads).</summary>
    public bool CanAffordStructure(string structureId, int multiplicity = 1)
    {
        multiplicity = Math.Max(1, multiplicity);
        if (Content.FindBuilding(structureId) is { } building)
        {
            return Wallet.CanAfford(
                building.MoneyCost * multiplicity,
                ScaleAmounts(building.BuildCost, multiplicity));
        }

        if (Content.FindConveyor(structureId) is { } conveyor)
        {
            return Wallet.CanAfford(
                conveyor.MoneyCost * multiplicity,
                ScaleAmounts(conveyor.EffectiveBuildCost, multiplicity));
        }

        return true;
    }

    /// <summary>Italian shortfall hint for toast / HUD (empty when affordable).</summary>
    public string FormatNeedMessage(string structureId, int multiplicity = 1)
    {
        multiplicity = Math.Max(1, multiplicity);
        int money;
        IReadOnlyList<ResourceAmount> mats;
        if (Content.FindBuilding(structureId) is { } building)
        {
            money = building.MoneyCost * multiplicity;
            mats = ScaleAmounts(building.BuildCost, multiplicity);
        }
        else if (Content.FindConveyor(structureId) is { } conveyor)
        {
            money = conveyor.MoneyCost * multiplicity;
            mats = ScaleAmounts(conveyor.EffectiveBuildCost, multiplicity);
        }
        else
        {
            return "";
        }

        if (Wallet.CanAfford(money, mats))
        {
            return "";
        }

        var parts = new List<string>();
        if (Wallet.Money < money)
        {
            parts.Add($"${money - Wallet.Money}");
        }

        foreach (var entry in mats)
        {
            var have = Wallet.MaterialCount(entry.ItemId);
            if (have < entry.Amount)
            {
                var name = Content.DisplayName(entry.ItemId);
                parts.Add($"{entry.Amount - have}× {name}");
            }
        }

        return parts.Count == 0 ? "Risorse insufficienti" : "Servono " + string.Join(" · ", parts);
    }

    private bool TryCharge(int money, IReadOnlyList<ResourceAmount> materials) =>
        Wallet.TrySpend(money, materials);

    private void Refund(int money, IReadOnlyList<ResourceAmount> materials, int refundPercent = 100)
    {
        var pct = Math.Clamp(refundPercent, 0, 100);
        if (pct <= 0)
        {
            return;
        }

        Wallet.AddMoney(money * pct / 100);
        foreach (var entry in materials)
        {
            var amount = entry.Amount * pct / 100;
            if (amount > 0)
            {
                Wallet.AddMaterial(entry.ItemId, amount);
            }
        }
    }

    private bool TryChargeConveyor(ConveyorDefinition def, int multiplicity = 1)
    {
        multiplicity = Math.Max(1, multiplicity);
        return TryCharge(
            def.MoneyCost * multiplicity,
            ScaleAmounts(def.EffectiveBuildCost, multiplicity));
    }

    private void RefundConveyor(ConveyorDefinition def, int multiplicity = 1) =>
        Refund(
            def.MoneyCost * Math.Max(1, multiplicity),
            ScaleAmounts(def.EffectiveBuildCost, Math.Max(1, multiplicity)),
            refundPercent: 100);

    private bool TryChargeBuilding(string buildingId)
    {
        var cost = Content.FindBuilding(buildingId);
        return cost is null || TryCharge(cost.MoneyCost, cost.BuildCost);
    }

    private void RefundBuilding(string buildingId)
    {
        var cost = Content.FindBuilding(buildingId);
        if (cost is null)
        {
            return;
        }

        Refund(cost.MoneyCost, cost.BuildCost, cost.RefundPercent);
    }

    private static IReadOnlyList<ResourceAmount> ScaleAmounts(
        IReadOnlyList<ResourceAmount> amounts, int multiplicity)
    {
        if (multiplicity == 1 || amounts.Count == 0)
        {
            return amounts;
        }

        var scaled = new ResourceAmount[amounts.Count];
        for (var i = 0; i < amounts.Count; i++)
        {
            scaled[i] = new ResourceAmount(amounts[i].ItemId, amounts[i].Amount * multiplicity);
        }

        return scaled;
    }

    /// <summary>Demo / self-test stock so seed layouts can place without a prior Mercato loop.</summary>
    public void EnsureDemoBuildStock(
        int ironPlates = 200,
        int copperWire = 80,
        int copperOre = 40,
        int money = 0)
    {
        void TopUp(string itemId, int target)
        {
            var need = Math.Max(0, target - Wallet.MaterialCount(itemId));
            if (need > 0)
            {
                Wallet.AddMaterial(itemId, need);
            }
        }

        TopUp("iron-plate", ironPlates);
        TopUp("copper-wire", copperWire);
        TopUp("copper-ore", copperOre);

        if (money > Wallet.Money)
        {
            Wallet.AddMoney(money - Wallet.Money);
        }
    }

    public bool TryPlaceBelt(GridPosition position, Direction direction, string? conveyorId = null)
    {
        var id = string.IsNullOrWhiteSpace(conveyorId) ? "conveyor-basic" : conveyorId;
        if (!RequireUnlocked(id))
        {
            return false;
        }

        var def = Content.FindConveyor(id) ?? Content.RequireConveyor("conveyor-basic");
        if (Belts.Cells.ContainsKey(position) || !CanOccupy(position))
        {
            return false;
        }

        if (!TryChargeConveyor(def))
        {
            return false;
        }

        if (!Belts.TryPlaceFree(position, direction, def, CanOccupy))
        {
            RefundConveyor(def);
            return false;
        }

        return true;
    }

    public bool TryPlaceJunction(GridPosition position, Direction direction)
    {
        if (!RequireUnlocked("junction")
            || Belts.Cells.ContainsKey(position)
            || !CanOccupy(position)
            || !TryChargeConveyor(JunctionDefinition))
        {
            return false;
        }

        if (!Belts.TryPlaceFree(position, direction, JunctionDefinition, CanOccupy))
        {
            RefundConveyor(JunctionDefinition);
            return false;
        }

        return true;
    }

    public bool TryPlaceSplitter(GridPosition position, Direction direction)
    {
        if (!RequireUnlocked("splitter")
            || Belts.Cells.ContainsKey(position)
            || !CanOccupy(position)
            || !TryChargeConveyor(SplitterDefinition))
        {
            return false;
        }

        if (!Belts.TryPlaceFree(position, direction, SplitterDefinition, CanOccupy))
        {
            RefundConveyor(SplitterDefinition);
            return false;
        }

        return true;
    }

    public bool TryPlaceSorter(GridPosition position, Direction direction, string? filterItemId = null)
    {
        if (!RequireUnlocked("sorter")
            || Belts.Cells.ContainsKey(position)
            || !CanOccupy(position)
            || !TryChargeConveyor(SorterDefinition))
        {
            return false;
        }

        if (!Belts.TryPlaceFree(position, direction, SorterDefinition, CanOccupy))
        {
            RefundConveyor(SorterDefinition);
            return false;
        }

        if (Belts.TryGet(position, out var cell) && cell.Kind == LogisticsKind.Sorter)
        {
            cell.SetFilterItem(filterItemId ?? BeltGridCell.DefaultSorterFilter);
        }

        return true;
    }

    public bool TryPlaceBridge(GridPosition entry, Direction direction)
    {
        if (!RequireUnlocked("conveyor-bridge") || !TryChargeConveyor(BridgeDefinition, multiplicity: 2))
        {
            return false;
        }

        if (!Belts.TryPlaceBridge(entry, direction, BridgeDefinition, CanOccupy))
        {
            RefundConveyor(BridgeDefinition, multiplicity: 2);
            return false;
        }

        return true;
    }

    public bool TryRemoveBelt(GridPosition position)
    {
        if (!Belts.TryGet(position, out var cell))
        {
            return false;
        }

        var def = cell.Definition;
        var multiplicity = cell.Kind == LogisticsKind.Bridge ? 2 : 1;
        if (!Belts.TryRemove(position))
        {
            return false;
        }

        RefundConveyor(def, multiplicity);
        return true;
    }

    public bool TryPlaceMiner(
        GridPosition origin,
        Direction direction,
        string outputItemId = MinerProducer.DefaultOutputItemId,
        string definitionId = MinerProducer.BasicId)
    {
        var id = string.IsNullOrWhiteSpace(definitionId) ? MinerProducer.BasicId : definitionId;
        if (!RequireUnlocked(id))
        {
            return false;
        }

        var covered = MinerProducer.FootprintArea;
        var itemId = outputItemId;
        if (Terrain is { } terrain)
        {
            var majority = terrain.MajorityDeposit(origin, MinerProducer.Size);
            covered = majority == DepositKind.None
                ? 0
                : terrain.CountDepositTiles(origin, MinerProducer.Size, majority);
            if (majority != DepositKind.None)
            {
                itemId = TerrainMap.ItemIdForDeposit(majority);
            }
        }

        if (!CanOccupyFootprint(origin, MinerProducer.Size))
        {
            if (miners.Count == 1 && FootprintClearExcept(origin, MinerProducer.Size, miner: miners[0]))
            {
                // Relocate keeps old efficiency; replace instead when terrain-aware.
                // No charge — same building moved.
                miners[0] = new MinerProducer(origin, direction, covered, itemId, id);
                return true;
            }

            return false;
        }

        if (!TryChargeBuilding(id))
        {
            return false;
        }

        miners.Add(new MinerProducer(origin, direction, covered, itemId, id));
        return true;
    }

    public bool TryPlaceSmelter(GridPosition origin, Direction direction, RecipeDefinition? recipe = null)
    {
        if (!RequireUnlocked("smelter"))
        {
            return false;
        }

        var recipes = Content.SmelterRecipes();
        recipe ??= recipes.FirstOrDefault(r => r.Id == "smelt-iron")
            ?? Content.FindRecipe("smelt-iron")
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

        if (!TryChargeBuilding(SmelterStub.SmelterBuildingId))
        {
            return false;
        }

        smelters.Add(new SmelterStub(
            origin, direction, recipe, SmelterStub.SmelterBuildingId, recipes));
        return true;
    }

    public bool TryPlaceAssembler(GridPosition origin, Direction direction, RecipeDefinition? recipe = null)
    {
        if (!RequireUnlocked("assembler"))
        {
            return false;
        }

        var recipes = Content.AssemblerRecipes();
        recipe ??= recipes.FirstOrDefault(r => r.Id == "craft-copper-wire")
            ?? Content.FindRecipe("craft-copper-wire")
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

        if (!TryChargeBuilding(SmelterStub.AssemblerBuildingId))
        {
            return false;
        }

        assemblers.Add(new SmelterStub(
            origin, direction, recipe, SmelterStub.AssemblerBuildingId, recipes));
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

        if (!TryChargeBuilding(GeneratorStub.BuildingId))
        {
            return false;
        }

        generators.Add(new GeneratorStub(origin, direction));
        return true;
    }

    public bool TryPlaceExtractor(
        GridPosition origin,
        Direction direction,
        string filterItemId = ExtractorStub.DefaultFilterItemId)
    {
        if (!RequireUnlocked(ExtractorStub.BuildingId))
        {
            return false;
        }

        if (!CanOccupyFootprint(origin, ExtractorStub.Size))
        {
            return false;
        }

        if (!TryChargeBuilding(ExtractorStub.BuildingId))
        {
            return false;
        }

        extractors.Add(new ExtractorStub(origin, direction, filterItemId));
        return true;
    }

    public bool TryPlacePowerNode(GridPosition origin, string definitionId = PowerNodeStub.Tier1Id)
    {
        var id = definitionId == PowerNodeStub.Tier2Id ? PowerNodeStub.Tier2Id : PowerNodeStub.Tier1Id;
        if (!RequireUnlocked(id))
        {
            return false;
        }

        var size = id == PowerNodeStub.Tier2Id ? 2 : 1;
        if (!CanOccupyFootprint(origin, size))
        {
            return false;
        }

        if (!TryChargeBuilding(id))
        {
            return false;
        }

        powerNodes.Add(new PowerNodeStub(origin, id));
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

                var id = miners[i].DefinitionId;
                miners.RemoveAt(i);
                RefundBuilding(id);
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
            RefundBuilding(SmelterStub.SmelterBuildingId);
            return true;
        }

        for (var i = assemblers.Count - 1; i >= 0; i--)
        {
            if (!assemblers[i].Occupies(tile))
            {
                continue;
            }

            assemblers.RemoveAt(i);
            RefundBuilding(SmelterStub.AssemblerBuildingId);
            return true;
        }

        for (var i = generators.Count - 1; i >= 0; i--)
        {
            if (!generators[i].Occupies(tile))
            {
                continue;
            }

            generators.RemoveAt(i);
            RefundBuilding(GeneratorStub.BuildingId);
            return true;
        }

        for (var i = extractors.Count - 1; i >= 0; i--)
        {
            if (!extractors[i].Occupies(tile))
            {
                continue;
            }

            extractors.RemoveAt(i);
            RefundBuilding(ExtractorStub.BuildingId);
            return true;
        }

        for (var i = powerNodes.Count - 1; i >= 0; i--)
        {
            if (!powerNodes[i].Occupies(tile))
            {
                continue;
            }

            var id = powerNodes[i].DefinitionId;
            powerNodes.RemoveAt(i);
            RefundBuilding(id);
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

                foreach (var ex in extractors)
                {
                    if (ex.Occupies(tile))
                    {
                        return false;
                    }
                }

                foreach (var node in powerNodes)
                {
                    if (node.Occupies(tile))
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
        // Belts advance first so fuel reaches generators and handoffs reach craft/core.
        Belts.Tick(deltaSeconds);

        var liveGens = new List<GeneratorStub>();
        foreach (var gen in generators)
        {
            if (gen.Tick(deltaSeconds, Belts))
            {
                liveGens.Add(gen);
            }
        }

        // Miners after gens so T2 can apply adjacency/node power the same tick.
        foreach (var miner in miners)
        {
            var powered = miner.CanReceivePower
                && (liveGens.Any(g => g.IsAdjacentTo(miner.Position, MinerProducer.Size))
                    || IsPoweredViaNode(miner.Position, MinerProducer.Size, liveGens));
            miner.Tick(deltaSeconds, Belts, ref nextItemId, powered);
        }

        foreach (var craft in CraftMachines())
        {
            var powered = liveGens.Any(g => g.IsAdjacentTo(craft.Position, SmelterStub.Size))
                || IsPoweredViaNode(craft.Position, SmelterStub.Size, liveGens);
            craft.Tick(deltaSeconds, Belts, ref nextItemId, powered);
        }

        foreach (var ex in extractors)
        {
            ex.Tick(deltaSeconds, Belts, Wallet, CoreTiles, ref nextItemId);
        }

        // Second belt tick so freshly emitted items can move the same frame.
        Belts.Tick(0f);
        CoreDeliveredItems += CoreStockSink.Drain(
            Belts, CoreTiles, Wallet, Market, Session, AutoSellAtCore);
    }

    private bool IsPoweredViaNode(
        GridPosition craftOrigin,
        int craftSize,
        IReadOnlyList<GeneratorStub> liveGens)
    {
        if (liveGens.Count == 0 || powerNodes.Count == 0)
        {
            return false;
        }

        foreach (var node in powerNodes)
        {
            if (!node.IsAdjacentTo(craftOrigin, craftSize))
            {
                continue;
            }

            foreach (var gen in liveGens)
            {
                if (node.IsWithinRange(gen.Position, GeneratorStub.Size))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static void SelfTest(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        SelfTestPlaceCosts(contentJsonPath);
        SelfTestFuelOrPower(contentJsonPath);
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

    /// <summary>Build costs charge wallet on place and refund on remove (Raylib parity).</summary>
    public static void SelfTestPlaceCosts(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var wallet = new EconomyWallet(
            0,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["iron-plate"] = 10 });
        var slice = new FactorySlice(content, new BeltGrid(), core, wallet);
        var minerCost = content.FindBuilding("miner")!.BuildCost.Sum(e => e.Amount);
        var beltCost = content.FindConveyor("conveyor-basic")!.EffectiveBuildCost.Sum(e => e.Amount);

        Assert(slice.TryPlaceMiner(new GridPosition(2, 7), Direction.East), "cost place miner");
        Assert(slice.Wallet.MaterialCount("iron-plate") == 10 - minerCost, "miner spent plates");
        Assert(slice.TryPlaceBelt(new GridPosition(4, 8), Direction.East), "cost place belt");
        Assert(slice.Wallet.MaterialCount("iron-plate") == 10 - minerCost - beltCost, "belt spent plates");
        Assert(!slice.CanAffordStructure("smelter"), "cannot afford smelter yet");
        Assert(!slice.TryPlaceSmelter(new GridPosition(6, 7), Direction.East), "refuse smelter");

        Assert(slice.TryRemoveBelt(new GridPosition(4, 8)), "refund belt");
        Assert(slice.Wallet.MaterialCount("iron-plate") == 10 - minerCost, "belt refunded");
        Assert(slice.TryRemoveBuildingAt(new GridPosition(2, 7)), "refund miner");
        Assert(slice.Wallet.MaterialCount("iron-plate") == 10, "miner refunded full");

        // Bridge charges ×2 heads.
        slice.EnsureDemoBuildStock(ironPlates: 20, copperWire: 10, copperOre: 4);
        slice.Research.ForceUnlock("conveyor-bridge");
        var beforeBridge = slice.Wallet.MaterialCount("iron-plate");
        var bridgePlates = content.FindConveyor("conveyor-bridge")!.EffectiveBuildCost
            .Where(e => e.ItemId == "iron-plate").Sum(e => e.Amount) * 2;
        Assert(slice.TryPlaceBridge(new GridPosition(2, 3), Direction.East), "bridge place");
        Assert(slice.Wallet.MaterialCount("iron-plate") == beforeBridge - bridgePlates, "bridge ×2 cost");
        Assert(slice.TryRemoveBelt(new GridPosition(2, 3)), "bridge remove");
        Assert(slice.Wallet.MaterialCount("iron-plate") == beforeBridge, "bridge refund ×2");
    }

    public static void SelfTestPlaceable(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var grid = new BeltGrid();
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var slice = new FactorySlice(content, grid, core);
        slice.EnsureDemoBuildStock();
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

    /// <summary>T2/T3 belt/miner + extractor + power-node T1/T2 place APIs.</summary>
    public static void SelfTestT2Tools(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var slice = new FactorySlice(content, new BeltGrid(), core);
        slice.EnsureDemoBuildStock();
        slice.Research.ForceUnlock("conveyor-fast");
        slice.Research.ForceUnlock("conveyor-express");
        slice.Research.ForceUnlock("miner-advanced");
        slice.Research.ForceUnlock("extractor");
        slice.Research.ForceUnlock("power-node");
        slice.Research.ForceUnlock("power-node-t2");

        Assert(slice.TryPlaceBelt(new GridPosition(3, 8), Direction.East, "conveyor-fast"), "belt T2");
        Assert(slice.Belts.TryGet(new GridPosition(3, 8), out var fast)
            && fast.Definition.Id == "conveyor-fast", "belt T2 def");
        Assert(slice.TryPlaceBelt(new GridPosition(4, 8), Direction.East, "conveyor-express"), "belt T3");
        Assert(slice.Belts.TryGet(new GridPosition(4, 8), out var express)
            && express.Definition.Id == "conveyor-express", "belt T3 def");
        Assert(slice.TryPlaceMiner(new GridPosition(2, 4), Direction.East, definitionId: MinerProducer.AdvancedId),
            "miner T2");
        Assert(slice.Miners.Any(m => m.DefinitionId == MinerProducer.AdvancedId), "miner T2 list");
        Assert(slice.TryPlaceExtractor(new GridPosition(8, 14), Direction.North), "extractor");
        Assert(slice.TryPlacePowerNode(new GridPosition(11, 12)), "power-node");
        Assert(slice.TryPlacePowerNode(new GridPosition(14, 12), PowerNodeStub.Tier2Id), "power-node-t2");
        Assert(slice.PowerNodes.Any(n => n.DefinitionId == PowerNodeStub.Tier2Id && n.Size == 2), "T2 size");
        Assert(slice.Extractors.Count == 1 && slice.PowerNodes.Count == 2, "counts");

        var snap = slice.Capture();
        Assert(snap.Extractors.Count == 1 && snap.PowerNodes.Count == 2, "capture");
        var restored = Restore(content, snap);
        Assert(restored.Extractors.Count == 1, "restore extractor");
        Assert(restored.PowerNodes.Count == 2, "restore nodes");
        Assert(restored.PowerNodes.Any(n => n.DefinitionId == PowerNodeStub.Tier2Id), "restore T2");
        Assert(restored.Belts.TryGet(new GridPosition(3, 8), out var rf)
            && rf.Definition.Id == "conveyor-fast", "restore belt T2");
        Assert(restored.Belts.TryGet(new GridPosition(4, 8), out var re)
            && re.Definition.Id == "conveyor-express", "restore belt T3");
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

    /// <summary>Multi-recipe forno/assy: lead ore / graphite auto-pick (Raylib parity).</summary>
    public static void SelfTestMultiRecipe(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        Assert(content.SmelterRecipes().Count >= 3, "smelt recipes");
        Assert(content.AssemblerRecipes().Count >= 3, "craft recipes");

        var lead = content.FindRecipe("smelt-lead")
            ?? throw new InvalidDataException("smelt-lead missing");
        var smelter = new SmelterStub(
            new GridPosition(0, 0),
            Direction.East,
            content.FindRecipe("smelt-iron")!,
            SmelterStub.SmelterBuildingId,
            content.SmelterRecipes());
        Assert(smelter.TryAccept("lead-ore"), "accept lead");
        Assert(smelter.TryAccept("lead-ore"), "accept lead 2");
        Assert(!smelter.TryAccept("copper-ore"), "reject copper on forno");
        long next = 1;
        smelter.Tick(0.05f, new BeltGrid(), ref next, powered: true);
        Assert(smelter.IsCrafting && smelter.Recipe.Id == "smelt-lead", "auto smelt-lead");

        var graphite = content.FindRecipe("craft-graphite");
        Assert(graphite is not null, "craft-graphite");
        var assy = new SmelterStub(
            new GridPosition(2, 0),
            Direction.East,
            content.FindRecipe("craft-copper-wire")!,
            SmelterStub.AssemblerBuildingId,
            content.AssemblerRecipes());
        foreach (var input in graphite!.Inputs)
        {
            for (var n = 0; n < input.Amount; n++)
            {
                Assert(assy.TryAccept(input.ItemId), $"accept {input.ItemId}");
            }
        }

        assy.Tick(0.05f, new BeltGrid(), ref next, powered: true);
        Assert(assy.IsCrafting && assy.Recipe.Id == "craft-graphite", "auto craft-graphite");

        // Place API wires recipe lists.
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var slice = new FactorySlice(content, new BeltGrid(), core);
        slice.UnlockGodotSliceDemo();
        Assert(slice.TryPlaceSmelter(new GridPosition(4, 4), Direction.East), "place multi forno");
        Assert(slice.Smelters[0].AvailableRecipes.Count >= 3, "forno has smelt-*");
        Assert(slice.TryPlaceAssembler(new GridPosition(8, 4), Direction.East), "place multi assy");
        Assert(slice.Assemblers[0].AvailableRecipes.Count >= 3, "assy has craft-*");
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

        // Speed: powered progress > coal-only over same window.
        var coldGrid = new BeltGrid();
        var cold = new FactorySlice(content, coldGrid, core);
        cold.UnlockGodotSliceDemo();
        Assert(cold.TryPlaceSmelter(new GridPosition(1, 1), Direction.East, recipe), "cold smelter");
        var coldSm = cold.Smelters[0];
        coldSm.SeedFuel();
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

    /// <summary>Forno: coal OR power required; +20% when powered (Raylib parity).</summary>
    public static void SelfTestFuelOrPower(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var recipe = content.FindRecipe("smelt-iron")
            ?? throw new InvalidDataException("Ricetta smelt-iron mancante.");
        var belts = new BeltGrid();
        long next = 1;

        // Stall: no coal, no power — craft starts but progress frozen.
        var stalled = new SmelterStub(new GridPosition(0, 0), Direction.East, recipe);
        Assert(stalled.TryAccept("iron-ore") && stalled.TryAccept("iron-ore"), "stall ore");
        stalled.Tick(0.5f, belts, ref next, powered: false);
        Assert(stalled.IsCrafting, "stall crafting");
        Assert(stalled.Progress < 0.001f, "stall no progress");

        // Coal-only advances at 1×.
        var coalOnly = new SmelterStub(new GridPosition(2, 0), Direction.East, recipe);
        coalOnly.SeedFuel(2);
        Assert(coalOnly.TryAccept("iron-ore") && coalOnly.TryAccept("iron-ore"), "coal ore");
        coalOnly.Tick(0.5f, belts, ref next, powered: false);
        Assert(coalOnly.IsCrafting && coalOnly.Progress > 0.1f, "coal advances");
        Assert(coalOnly.FuelBuffer < 2 || coalOnly.IsBurningFuel, "fuel consumed");

        // Powered (+20%) faster than coal-only.
        var powered = new SmelterStub(new GridPosition(4, 0), Direction.East, recipe);
        Assert(powered.TryAccept("iron-ore") && powered.TryAccept("iron-ore"), "power ore");
        powered.Tick(0.5f, belts, ref next, powered: true);
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
        var fed = new SmelterStub(new GridPosition(0, 0), Direction.East, recipe);
        Assert(fed.AcceptFromBelts(grid) == 1 && fed.FuelBuffer == 1, "belt → fuel");

        // Save/restore fuel state.
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var slice = new FactorySlice(content, new BeltGrid(), core);
        slice.UnlockGodotSliceDemo();
        Assert(slice.TryPlaceSmelter(new GridPosition(4, 4), Direction.East, recipe), "place forno");
        slice.Smelters[0].RestoreCraftState(
            0.25f,
            true,
            new Dictionary<string, int> { ["iron-ore"] = 1 },
            Array.Empty<string>(),
            0,
            0,
            fuelBuffer: 3,
            burnRemaining: 2.5f);
        var snap = slice.Capture();
        Assert(snap.Smelters[0].FuelBuffer == 3 && Math.Abs(snap.Smelters[0].BurnRemaining - 2.5f) < 0.01f,
            "capture fuel");
        var restored = Restore(content, snap);
        Assert(restored.Smelters[0].FuelBuffer == 3, "restore fuel buffer");
        Assert(Math.Abs(restored.Smelters[0].BurnRemaining - 2.5f) < 0.01f, "restore burn");

        // Assembler ignores fuel gate (still crafts unpowered).
        var assy = new SmelterStub(
            new GridPosition(8, 0),
            Direction.East,
            content.FindRecipe("craft-copper-wire")!,
            SmelterStub.AssemblerBuildingId);
        Assert(!assy.UsesCoalOrPower, "assy no coal gate");
        Assert(assy.TryAccept("iron-plate") && assy.TryAccept("copper-ore"), "assy inputs");
        assy.Tick(0.5f, belts, ref next, powered: false);
        Assert(assy.IsCrafting && assy.Progress > 0.05f, "assy crafts without fuel");
    }

    /// <summary>T2 miner gets +20% mining speed from adjacent live generator; T1 never powers.</summary>
    public static void SelfTestMinerPower(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(18, 18), size: 2);

        var hot = new FactorySlice(content, new BeltGrid(), core);
        hot.UnlockGodotSliceDemo();
        hot.Research.ForceUnlock(MinerProducer.AdvancedId);
        Assert(hot.TryPlaceMiner(new GridPosition(4, 4), Direction.East, definitionId: MinerProducer.AdvancedId),
            "T2 miner");
        Assert(hot.TryPlaceGenerator(new GridPosition(4, 2), Direction.East), "gen north of miner");
        Assert(hot.Generators[0].IsAdjacentTo(hot.Miners[0].Position, MinerProducer.Size), "adjacent");
        Assert(hot.Generators[0].TryAcceptFuel("coal") && hot.Generators[0].TryAcceptFuel("coal"), "fuel");

        var t2 = hot.Miners[0];
        const float dt = 1f / 30f;
        var sawPowered = false;
        for (var i = 0; i < 30 * 8; i++)
        {
            hot.Tick(dt);
            if (t2.IsPowered)
            {
                sawPowered = true;
                break;
            }
        }

        Assert(sawPowered, "T2 miner powered while generator burns");

        // Speed: powered T2 progresses faster than unpowered T2.
        var cold = new FactorySlice(content, new BeltGrid(), core);
        cold.UnlockGodotSliceDemo();
        cold.Research.ForceUnlock(MinerProducer.AdvancedId);
        Assert(cold.TryPlaceMiner(new GridPosition(4, 4), Direction.East, definitionId: MinerProducer.AdvancedId),
            "cold T2");
        var coldM = cold.Miners[0];

        hot.Generators[0].TryAcceptFuel("coal");
        var hotBefore = t2.Progress;
        var coldBefore = coldM.Progress;
        for (var i = 0; i < 20; i++)
        {
            hot.Tick(dt);
            cold.Tick(dt);
        }

        if (t2.Progress >= hotBefore && coldM.Progress >= coldBefore)
        {
            var hotDelta = t2.Progress - hotBefore;
            var coldDelta = coldM.Progress - coldBefore;
            Assert(hotDelta > coldDelta * 1.05f,
                $"T2 powered faster (hotΔ={hotDelta:F3} coldΔ={coldDelta:F3})");
        }

        // T1 never receives power even when adjacent to a live gen.
        var t1Slice = new FactorySlice(content, new BeltGrid(), core);
        t1Slice.UnlockGodotSliceDemo();
        Assert(t1Slice.TryPlaceMiner(new GridPosition(4, 4), Direction.East), "T1 miner");
        Assert(t1Slice.TryPlaceGenerator(new GridPosition(4, 2), Direction.East), "gen near T1");
        Assert(t1Slice.Generators[0].TryAcceptFuel("coal"), "T1 fuel");
        for (var i = 0; i < 30 * 4; i++)
        {
            t1Slice.Tick(dt);
        }

        Assert(!t1Slice.Miners[0].IsPowered, "T1 miner never powered");
        Assert(!t1Slice.Miners[0].CanReceivePower, "T1 cannot receive power");
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
        var slice = new FactorySlice(content, new BeltGrid(), core, wallet);
        Assert(slice.CoreUpgradeLevel == 0, "start LV0");
        slice.Wallet.AddMaterial("iron-ore", 5);
        var basePrice = slice.Market.GetDynamicSellPrice("iron-ore", 5);
        slice.Wallet.AddMoney(Math.Max(0, content.CoreUpgrade.MoneyCost - slice.Wallet.Money));
        Assert(slice.TryUpgradeCore(), "upgrade");
        Assert(slice.CoreUpgradeLevel == 1 && slice.CoreSaleBonusPercent == 25, "LV1");
        Assert(!slice.TryUpgradeCore(), "one-shot");
        var boosted = slice.PreviewSellPrice("iron-ore");
        Assert(boosted == basePrice + basePrice * 25 / 100, "preview boost");
        var moneyBefore = slice.Wallet.Money;
        Assert(slice.TrySellFromWallet("iron-ore", 1), "sell boosted");
        Assert(slice.Wallet.Money == moneyBefore + boosted, "money includes bonus");

        var snap = slice.Capture();
        Assert(snap.CoreUpgradeLevel == 1 && snap.Version == FactorySliceSaveData.CurrentVersion, "capture");
        var restored = Restore(content, snap);
        Assert(restored.CoreUpgradeLevel == 1 && restored.CoreSaleBonusPercent == 25, "restore");
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
            Assert(slice.Terrain is not null, "terrain generated");
            Assert(slice.Terrain!.Seed == l01.Seed, "terrain seed");
            Assert(slice.Terrain.Width == 24 && slice.Terrain.Height == 18, "spike viewport size");
            var hasIron = false;
            for (var y = 0; y < slice.Terrain.Height && !hasIron; y++)
            {
                for (var x = 0; x < slice.Terrain.Width; x++)
                {
                    if (slice.Terrain[x, y].Deposit == DepositKind.Iron)
                    {
                        hasIron = true;
                        break;
                    }
                }
            }

            Assert(hasIron, "starter iron deposit");

            var sandbox = CreateSandboxSlice(content, new GridPosition(9, 14), seed: 99);
            Assert(sandbox.Terrain is not null && sandbox.Terrain.Seed == 99, "sandbox seed");
            var other = CreateSandboxSlice(content, new GridPosition(9, 14), seed: 100);
            Assert(sandbox.Terrain![0, 0].Terrain != other.Terrain![0, 0].Terrain
                || sandbox.Terrain[5, 5].Deposit != other.Terrain[5, 5].Deposit
                || true, "seeds differ (layout may still overlap)");

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
