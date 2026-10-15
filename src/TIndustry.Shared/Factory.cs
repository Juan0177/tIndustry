namespace TIndustry.Shared;

/// <summary>
/// Playable factory slice: miners → belts → forni/assemblatori → core stock.
/// Phase F: generators feed a shared power buffer; connected craft spends for +20%.
/// Soft brownout: empty buffer → forno falls back to coal; assembler loses bonus.
/// </summary>
public sealed class Factory : IGameView
{
    /// <summary>Raylib <c>FactoryWorld.CorePowerCapacity</c>.</summary>
    public const float CorePowerCapacity = GameState.CorePowerCapacity;
    /// <summary>Raylib <c>FactoryWorld.CorePowerGeneration</c> (always ticks into buffer).</summary>
    public const float CorePowerGeneration = GameState.CorePowerGeneration;
    /// <summary>Raylib <c>FactoryWorld.SmelterPowerDraw</c> (per second while powered craft).</summary>
    public const float SmelterPowerDraw = CraftingSystem.SmelterPowerDraw;
    /// <summary>Raylib <c>FactoryWorld.AssemblerPowerDraw</c>.</summary>
    public const float AssemblerPowerDraw = CraftingSystem.AssemblerPowerDraw;

    /// <summary>The whole simulation state; <see cref="Simulation.Step"/> advances it.</summary>
    public GameState State { get; }

    private long nextItemId
    {
        get => State.NextItemId;
        set => State.NextItemId = value;
    }

    private long nextEntityId
    {
        get => State.NextEntityId;
        set => State.NextEntityId = value;
    }

    private List<Miner> miners => State.Miners;
    private List<Crafter> smelters => State.Smelters;
    private List<Crafter> assemblers => State.Assemblers;
    private List<Generator> generators => State.Generators;
    private List<Extractor> extractors => State.Extractors;
    private List<PowerNode> powerNodes => State.PowerNodes;

    public Factory(
        FactoryContent content,
        BeltGrid belts,
        IReadOnlySet<GridPosition> coreTiles,
        EconomyWallet? wallet = null,
        ResearchState? research = null,
        EconomySession? session = null)
    {
        var resolvedWallet = wallet ?? new EconomyWallet();
        State = new GameState(
            content,
            belts,
            coreTiles,
            resolvedWallet,
            research ?? ResearchState.CreateNew(content),
            session ?? new EconomySession(resolvedWallet.Money),
            MarketCatalog.FromContent(content));
        BeltDefinition = belts.DefaultDefinition
            ?? content.RequireConveyor("conveyor-basic");
        JunctionDefinition = content.RequireConveyor("junction");
        SplitterDefinition = content.RequireConveyor("splitter");
        SorterDefinition = content.RequireConveyor("sorter");
        BridgeDefinition = content.RequireConveyor("conveyor-bridge");
    }

    /// <summary>Wraps an existing state (used by snapshots); definitions come from its content.</summary>
    internal Factory(GameState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        BeltDefinition = state.Belts.DefaultDefinition
            ?? state.Content.RequireConveyor("conveyor-basic");
        JunctionDefinition = state.Content.RequireConveyor("junction");
        SplitterDefinition = state.Content.RequireConveyor("splitter");
        SorterDefinition = state.Content.RequireConveyor("sorter");
        BridgeDefinition = state.Content.RequireConveyor("conveyor-bridge");
    }

    public FactoryContent Content => State.Content;
    public BeltGrid Belts => State.Belts;
    public ConveyorDefinition BeltDefinition { get; }
    public ConveyorDefinition JunctionDefinition { get; }
    public ConveyorDefinition SplitterDefinition { get; }
    public ConveyorDefinition SorterDefinition { get; }
    public ConveyorDefinition BridgeDefinition { get; }
    public IReadOnlySet<GridPosition> CoreTiles => State.CoreTiles;
    public EconomyWallet Wallet => State.Wallet;
    public ResearchState Research => State.Research;
    public MarketCatalog Market => State.Market;
    public EconomySession Session => State.Session;
    public string? ActiveCampaignLevelId
    {
        get => State.ActiveCampaignLevelId;
        set => State.ActiveCampaignLevelId = value;
    }

    public bool AutoSellAtCore
    {
        get => State.AutoSellAtCore;
        set => State.AutoSellAtCore = value;
    }

    public int CoreUpgradeLevel
    {
        get => State.CoreUpgradeLevel;
        private set => State.CoreUpgradeLevel = value;
    }

    public int CoreSaleBonusPercent
    {
        get => State.CoreSaleBonusPercent;
        private set => State.CoreSaleBonusPercent = value;
    }

    /// <summary>Shared power buffer (Raylib parity). Recharges from core + live gens.</summary>
    public float PowerBuffer
    {
        get => State.PowerBuffer;
        private set => State.PowerBuffer = value;
    }

    /// <summary>Max buffer: core capacity + live generators × CapacityBonus.</summary>
    public float PowerCapacity
    {
        get => State.PowerCapacity;
        private set => State.PowerCapacity = value;
    }

    /// <summary>Last tick generation rate (core + live gens).</summary>
    public float PowerGenerationPerSecond => State.PowerGenerationPerSecond;

    public TerrainMap? Terrain
    {
        get => State.Terrain;
        set => State.Terrain = value;
    }

    public long CoreDeliveredItems
    {
        get => State.CoreDeliveredItems;
        private set => State.CoreDeliveredItems = value;
    }

    public IReadOnlyList<Miner> Miners => miners;
    public IReadOnlyList<Crafter> Smelters => smelters;
    public IReadOnlyList<Crafter> Assemblers => assemblers;
    public IReadOnlyList<Generator> Generators => generators;
    public IReadOnlyList<Extractor> Extractors => extractors;
    public IReadOnlyList<PowerNode> PowerNodes => powerNodes;
    public long NextItemId => nextItemId;
    public long NextEntityId => nextEntityId;

    /// <summary>Fixed ticks simulated so far (<see cref="SimulationClock.TickSeconds"/> each).</summary>
    public long CurrentTick
    {
        get => State.CurrentTick;
        private set => State.CurrentTick = value;
    }

    IReadOnlyDictionary<GridPosition, BeltGridCell> IGameView.Belts => Belts.Cells;
    IWalletView IGameView.Wallet => Wallet;
    IResearchView IGameView.Research => Research;
    ISessionView IGameView.Session => Session;

    public bool IsBeltCorner(GridPosition position) => Belts.IsCorner(position);

    public bool TryGetBeltIncomingDirection(GridPosition position, out Direction incoming) =>
        Belts.TryGetIncomingDirection(position, out incoming);

    /// <summary>Primary / first miner (compat for HUD).</summary>
    public Miner? Miner => miners.Count > 0 ? miners[0] : null;

    public FactorySaveData Capture()
    {
        var coreOrigin = CoreTiles.Count == 0
            ? new GridPosition(9, 14)
            : new GridPosition(CoreTiles.Min(t => t.X), CoreTiles.Min(t => t.Y));
        var coreSize = CoreTiles.Count == 0
            ? 2
            : Math.Max(1, (int)Math.Round(Math.Sqrt(CoreTiles.Count)));

        return new FactorySaveData
        {
            Version = FactorySaveData.CurrentVersion,
            NextItemId = nextItemId,
            NextEntityId = nextEntityId,
            CurrentTick = CurrentTick,
            CoreDeliveredItems = CoreDeliveredItems,
            CoreX = coreOrigin.X,
            CoreY = coreOrigin.Y,
            CoreSize = coreSize,
            Money = Wallet.Money,
            Materials = SortedByKey(Wallet.MaterialsSnapshot()),
            UnlockedStructures = Research.UnlockedIds.OrderBy(id => id, StringComparer.Ordinal).ToList(),
            SaleIncome = Session.SaleIncome,
            SoldByItem = SortedByKey(Session.SoldByItem),
            StartingMoney = Session.StartingMoney,
            ActiveCampaignLevelId = ActiveCampaignLevelId,
            AutoSellAtCore = AutoSellAtCore,
            CoreUpgradeLevel = CoreUpgradeLevel,
            CoreSaleBonusPercent = CoreSaleBonusPercent,
            PowerBuffer = PowerBuffer,
            PowerCapacity = PowerCapacity,
            Seed = Terrain?.Seed ?? 0,
            MapWidth = Terrain?.Width ?? 0,
            MapHeight = Terrain?.Height ?? 0,
            // Canonical order (ids, then belt cells by Y, X) so captures compare byte for byte.
            Miners = miners.OrderBy(m => m.Id).Select(m => new MinerSaveDto
            {
                Id = m.Id,
                X = m.Position.X,
                Y = m.Position.Y,
                Direction = m.Direction.ToString(),
                Progress = m.Progress,
                EjectIndex = m.EjectIndex,
                OutputItemId = m.OutputItemId,
                DefinitionId = m.DefinitionId
            }).ToList(),
            Smelters = smelters.OrderBy(s => s.Id).Select(CaptureCraft).ToList(),
            Assemblers = assemblers.OrderBy(a => a.Id).Select(CaptureCraft).ToList(),
            Generators = generators.OrderBy(g => g.Id).Select(g => new GeneratorSaveDto
            {
                Id = g.Id,
                X = g.Position.X,
                Y = g.Position.Y,
                Direction = g.Direction.ToString(),
                FuelBuffer = g.FuelBuffer,
                BurnRemaining = g.BurnRemaining,
                FuelConsumed = g.FuelConsumed
            }).ToList(),
            Extractors = extractors.OrderBy(e => e.Id).Select(e => new ExtractorSaveDto
            {
                Id = e.Id,
                X = e.Position.X,
                Y = e.Position.Y,
                Direction = e.Direction.ToString(),
                FilterItemId = e.FilterItemId,
                Progress = e.Progress
            }).ToList(),
            PowerNodes = powerNodes.OrderBy(n => n.Id).Select(n => new PowerNodeSaveDto
            {
                Id = n.Id,
                X = n.Position.X,
                Y = n.Position.Y,
                DefinitionId = n.DefinitionId
            }).ToList(),
            // BeltGrid.Cells already enumerates in canonical (Y, X) order.
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

    private static Dictionary<string, int> SortedByKey(IEnumerable<KeyValuePair<string, int>> source)
    {
        // Fresh dictionary filled in ordinal key order: enumerates (and serializes) in that order.
        var sorted = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var kv in source.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            sorted[kv.Key] = kv.Value;
        }

        return sorted;
    }

    private static CraftSaveDto CaptureCraft(Crafter craft) => new()
    {
        Id = craft.Id,
        X = craft.Position.X,
        Y = craft.Position.Y,
        Direction = craft.Direction.ToString(),
        RecipeId = craft.Recipe.Id,
        DefinitionId = craft.DefinitionId,
        Progress = craft.Progress,
        IsCrafting = craft.IsCrafting,
        InputBuffer = SortedByKey(craft.InputBuffer),
        OutputQueue = craft.OutputQueue.ToList(),
        EjectIndex = craft.EjectIndex,
        ItemsCrafted = craft.ItemsCrafted,
        FuelBuffer = craft.FuelBuffer,
        BurnRemaining = craft.BurnRemaining
    };

    public static Factory Restore(FactoryContent content, FactorySaveData data)
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
        var session = new EconomySession(data.StartingMoney);
        session.Restore(data.StartingMoney, data.SaleIncome, data.SoldByItem);
        var slice = new Factory(content, belts, core, wallet, research, session)
        {
            nextItemId = Math.Max(1, data.NextItemId),
            CurrentTick = Math.Max(0, data.CurrentTick),
            CoreDeliveredItems = Math.Max(0, data.CoreDeliveredItems),
            ActiveCampaignLevelId = data.ActiveCampaignLevelId,
            AutoSellAtCore = data.AutoSellAtCore,
            CoreUpgradeLevel = Math.Max(0, data.CoreUpgradeLevel),
            CoreSaleBonusPercent = Math.Max(0, data.CoreSaleBonusPercent)
        };
        slice.SetPowerBuffer(data.PowerBuffer);
        // Capacity recalculates on first Tick from live gens; seed from save when present.
        if (data.PowerCapacity > 0f)
        {
            slice.PowerCapacity = Math.Max(CorePowerCapacity, data.PowerCapacity);
            slice.PowerBuffer = Math.Clamp(slice.PowerBuffer, 0f, slice.PowerCapacity);
        }

        // Defensive: a building without a stable id (0) gets a fresh one in list order.
        var maxSavedId = data.Miners.Select(d => d.Id)
            .Concat(data.Smelters.Select(d => d.Id))
            .Concat(data.Assemblers.Select(d => d.Id))
            .Concat(data.Generators.Select(d => d.Id))
            .Concat(data.Extractors.Select(d => d.Id))
            .Concat(data.PowerNodes.Select(d => d.Id))
            .DefaultIfEmpty(0)
            .Max();
        slice.nextEntityId = Math.Max(Math.Max(1, data.NextEntityId), maxSavedId + 1);
        long RestoredId(long savedId) => savedId > 0 ? savedId : slice.nextEntityId++;

        foreach (var m in data.Miners)
        {
            var miner = new Miner(
                new GridPosition(m.X, m.Y),
                ParseDirection(m.Direction),
                outputItemId: m.OutputItemId,
                definitionId: m.DefinitionId,
                id: RestoredId(m.Id));
            miner.RestoreProgress(m.Progress, m.EjectIndex);
            slice.miners.Add(miner);
        }

        foreach (var s in data.Smelters)
        {
            slice.smelters.Add(RestoreCraft(content, s, Crafter.SmelterBuildingId, RestoredId(s.Id)));
        }

        foreach (var a in data.Assemblers)
        {
            slice.assemblers.Add(RestoreCraft(content, a, Crafter.AssemblerBuildingId, RestoredId(a.Id)));
        }

        foreach (var g in data.Generators)
        {
            var gen = new Generator(new GridPosition(g.X, g.Y), ParseDirection(g.Direction), RestoredId(g.Id));
            gen.RestoreFuel(g.FuelBuffer, g.BurnRemaining, g.FuelConsumed);
            slice.generators.Add(gen);
        }

        foreach (var e in data.Extractors)
        {
            var ex = new Extractor(
                new GridPosition(e.X, e.Y),
                ParseDirection(e.Direction),
                e.FilterItemId,
                RestoredId(e.Id));
            ex.RestoreProgress(e.Progress);
            slice.extractors.Add(ex);
        }

        foreach (var n in data.PowerNodes)
        {
            slice.powerNodes.Add(new PowerNode(new GridPosition(n.X, n.Y), n.DefinitionId, RestoredId(n.Id)));
        }

        if (data.MapWidth >= 12 && data.MapHeight >= 8)
        {
            var w = Math.Min(Math.Max(12, data.MapWidth), MaxGodotCampaignMapEdge);
            var h = Math.Min(Math.Max(8, data.MapHeight), MaxGodotCampaignMapEdge);
            var starter = ResolveStarterDeposit(
                new GridPosition(data.CoreX, data.CoreY),
                Math.Max(1, data.CoreSize),
                w,
                h);
            slice.Terrain = TerrainMap.Generate(
                w,
                h,
                data.Seed,
                slice.CoreTiles,
                starter);
        }

        SortById(slice.miners, m => m.Id);
        SortById(slice.smelters, s => s.Id);
        SortById(slice.assemblers, a => a.Id);
        SortById(slice.generators, g => g.Id);
        SortById(slice.extractors, e => e.Id);
        SortById(slice.powerNodes, n => n.Id);
        return slice;
    }

    /// <summary>Systems iterate buildings in ascending id order (stable sort keeps list order on ties).</summary>
    private static void SortById<T>(List<T> list, Func<T, long> id)
    {
        var sorted = list.OrderBy(id).ToList();
        list.Clear();
        list.AddRange(sorted);
    }

    private static Crafter RestoreCraft(FactoryContent content, CraftSaveDto dto, string fallbackId, long id)
    {
        var recipe = content.FindRecipe(dto.RecipeId)
            ?? content.FindRecipe(fallbackId == Crafter.AssemblerBuildingId
                ? "craft-copper-wire"
                : "smelt-iron")
            ?? throw new InvalidDataException($"Ricetta '{dto.RecipeId}' mancante nel save.");
        var buildingId = string.IsNullOrWhiteSpace(dto.DefinitionId) ? fallbackId : dto.DefinitionId;
        var available = buildingId == Crafter.AssemblerBuildingId
            ? content.AssemblerRecipes()
            : content.SmelterRecipes();
        var craft = new Crafter(
            new GridPosition(dto.X, dto.Y),
            ParseDirection(dto.Direction),
            recipe,
            buildingId,
            available,
            id);
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
    public static Factory CreatePhaseCDemo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var beltDef = content.RequireConveyor(beltId);
        var recipe = content.FindRecipe("smelt-iron")
            ?? throw new InvalidDataException("Ricetta smelt-iron mancante.");
        var grid = new BeltGrid();
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var slice = new Factory(content, grid, core);
        slice.UnlockAllForDemo();

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
    public static Factory CreatePhaseDDemo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var beltDef = content.RequireConveyor(beltId);
        var smelt = content.FindRecipe("smelt-iron")
            ?? throw new InvalidDataException("Ricetta smelt-iron mancante.");
        var wire = content.FindRecipe("craft-copper-wire")
            ?? throw new InvalidDataException("Ricetta craft-copper-wire mancante.");
        var grid = new BeltGrid();
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var slice = new Factory(content, grid, core);
        slice.UnlockAllForDemo();

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
    public static Factory CreatePhaseEDemo(FactoryContent content, string beltId = "conveyor-basic")
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
        var slice = new Factory(content, grid, core);
        slice.UnlockAllForDemo();

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
        Require(grid.TryPlaceFree(new GridPosition(10, 8), Direction.East, junctionDef), "junction seed");
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
        Require(grid.TryPlaceFree(new GridPosition(14, 10), Direction.South, splitterDef), "splitter seed");

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
    public static Factory CreatePhaseFDemo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var slice = CreatePhaseEDemo(content, beltId);
        var beltDef = slice.BeltDefinition;

        // Generator north of forno (6,7) → adjacent on south edge.
        Require(slice.TryPlaceGenerator(new GridPosition(6, 5), Direction.East), "generator seed");
        // Coal miner west of generator.
        Require(slice.TryPlaceMiner(new GridPosition(2, 4), Direction.East, "coal"), "coal miner");
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
    public static Factory CreateSorterBridgeDemo(FactoryContent content, string beltId = "conveyor-basic")
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
        Require(slice.TryPlaceSorter(new GridPosition(18, 2), Direction.East), "sorter seed");
        slice.Belts.PlacePath([new GridPosition(19, 2), new GridPosition(20, 2)], beltDef);
        slice.Belts.TryOrient(new GridPosition(20, 2), Direction.East);
        slice.Belts.PlacePath([new GridPosition(18, 1)], beltDef); // overflow left of East = North
        slice.Belts.TryOrient(new GridPosition(18, 1), Direction.North);
        slice.Belts.PlacePath([new GridPosition(18, 3)], beltDef); // overflow right of East = South
        slice.Belts.TryOrient(new GridPosition(18, 3), Direction.South);

        // Bridge span 2 over mid (16,12)→(18,12); mid stays empty for thin-span visual.
        // Underpass: belt corridor south of mid (proves mid freeness without covering the span).
        Require(slice.TryPlaceBridge(new GridPosition(16, 12), Direction.East), "bridge seed");
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
    public void UnlockAllForDemo()
    {
        foreach (var id in ResearchState.DemoStructureIds)
        {
            Research.ForceUnlock(id);
        }

        // Seed layouts call TryPlace* (now charged) — stock enough for demo footprints.
        EnsureDemoBuildStock();
    }

    /// <summary>Demos without a live generator: top up forno coal so craft can run.</summary>
    public static void SeedSmelterFuel(Factory slice, int units = Crafter.FuelBufferCapacity)
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

    public int EffectiveSalePrice(string itemId, int stockBeforeSell) =>
        State.EffectiveSalePrice(itemId, stockBeforeSell);

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

    /// <summary>Raylib maps go to 1000²; Godot clamps campaign/sandbox edges for safety.</summary>
    public const int MaxGodotCampaignMapEdge = 1000;

    public static GridPosition CenteredCoreOrigin(int mapWidth, int mapHeight, int coreSize = 2) =>
        new(
            Math.Max(0, (Math.Max(coreSize, mapWidth) - coreSize) / 2),
            Math.Max(0, (Math.Max(coreSize, mapHeight) - coreSize) / 2));

    public static (int Width, int Height) ResolveCampaignMapSize(CampaignLevelDefinition level)
    {
        var w = Math.Max(12, level.MapWidth);
        var h = Math.Max(8, level.MapHeight);
        w = Math.Min(w, MaxGodotCampaignMapEdge);
        h = Math.Min(h, MaxGodotCampaignMapEdge);
        return (w, h);
    }

    internal static GridPosition ResolveStarterDeposit(
        GridPosition coreOrigin, int coreSize, int mapWidth, int mapHeight)
    {
        // Prefer west of core (Raylib-style), clamped into map.
        var x = Math.Clamp(coreOrigin.X - Miner.Size - 2, 1, Math.Max(1, mapWidth - Miner.Size - 1));
        var y = Math.Clamp(coreOrigin.Y, 1, Math.Max(1, mapHeight - Miner.Size - 1));
        // Avoid overlapping the core footprint.
        if (x + Miner.Size > coreOrigin.X && x < coreOrigin.X + coreSize
            && y + Miner.Size > coreOrigin.Y && y < coreOrigin.Y + coreSize)
        {
            x = Math.Max(1, coreOrigin.X - Miner.Size - 1);
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

        // Belts that touch the Core must face into it so Drain can absorb.
        if (CoreStockSink.PreferDirectionIntoCore(position, CoreTiles) is { } intoCore)
        {
            direction = intoCore;
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
        string outputItemId = Miner.DefaultOutputItemId,
        string definitionId = Miner.BasicId)
    {
        var id = string.IsNullOrWhiteSpace(definitionId) ? Miner.BasicId : definitionId;
        if (!RequireUnlocked(id))
        {
            return false;
        }

        var covered = Miner.FootprintArea;
        var itemId = outputItemId;
        if (Terrain is { } terrain)
        {
            var majority = terrain.MajorityDeposit(origin, Miner.Size);
            covered = majority == DepositKind.None
                ? 0
                : terrain.CountDepositTiles(origin, Miner.Size, majority);
            if (majority != DepositKind.None)
            {
                itemId = TerrainMap.ItemIdForDeposit(majority);
            }
        }

        if (!CanOccupyFootprint(origin, Miner.Size))
        {
            if (miners.Count == 1 && FootprintClearExcept(origin, Miner.Size, miner: miners[0]))
            {
                // Relocate keeps old efficiency; replace instead when terrain-aware.
                // No charge — same building moved.
                miners[0] = new Miner(origin, direction, covered, itemId, id, miners[0].Id);
                return true;
            }

            return false;
        }

        if (!TryChargeBuilding(id))
        {
            return false;
        }

        miners.Add(new Miner(origin, direction, covered, itemId, id, nextEntityId++));
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

        if (!CanOccupyFootprint(origin, Crafter.Size))
        {
            if (smelters.Count == 1 && FootprintClearExcept(origin, Crafter.Size, craft: smelters[0]))
            {
                smelters[0].Relocate(origin, direction);
                return true;
            }

            return false;
        }

        if (!TryChargeBuilding(Crafter.SmelterBuildingId))
        {
            return false;
        }

        smelters.Add(new Crafter(
            origin, direction, recipe, Crafter.SmelterBuildingId, recipes, nextEntityId++));
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

        if (!CanOccupyFootprint(origin, Crafter.Size))
        {
            if (assemblers.Count == 1 && FootprintClearExcept(origin, Crafter.Size, craft: assemblers[0]))
            {
                assemblers[0].Relocate(origin, direction);
                return true;
            }

            return false;
        }

        if (!TryChargeBuilding(Crafter.AssemblerBuildingId))
        {
            return false;
        }

        assemblers.Add(new Crafter(
            origin, direction, recipe, Crafter.AssemblerBuildingId, recipes, nextEntityId++));
        return true;
    }

    public bool TryPlaceGenerator(GridPosition origin, Direction direction = Direction.East)
    {
        if (!RequireUnlocked("generator"))
        {
            return false;
        }

        if (!CanOccupyFootprint(origin, Generator.Size))
        {
            if (generators.Count == 1
                && FootprintClearExcept(origin, Generator.Size, generator: generators[0]))
            {
                generators[0].Relocate(origin, direction);
                return true;
            }

            return false;
        }

        if (!TryChargeBuilding(Generator.BuildingId))
        {
            return false;
        }

        generators.Add(new Generator(origin, direction, nextEntityId++));
        return true;
    }

    public bool TryPlaceExtractor(
        GridPosition origin,
        Direction direction,
        string filterItemId = Extractor.DefaultFilterItemId)
    {
        if (!RequireUnlocked(Extractor.BuildingId))
        {
            return false;
        }

        if (!CanOccupyFootprint(origin, Extractor.Size))
        {
            return false;
        }

        if (!TryChargeBuilding(Extractor.BuildingId))
        {
            return false;
        }

        extractors.Add(new Extractor(origin, direction, filterItemId, nextEntityId++));
        return true;
    }

    public bool TryPlacePowerNode(GridPosition origin, string definitionId = PowerNode.Tier1Id)
    {
        var id = definitionId == PowerNode.Tier2Id ? PowerNode.Tier2Id : PowerNode.Tier1Id;
        if (!RequireUnlocked(id))
        {
            return false;
        }

        var size = id == PowerNode.Tier2Id ? 2 : 1;
        if (!CanOccupyFootprint(origin, size))
        {
            return false;
        }

        if (!TryChargeBuilding(id))
        {
            return false;
        }

        powerNodes.Add(new PowerNode(origin, id, nextEntityId++));
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
            RefundBuilding(Crafter.SmelterBuildingId);
            return true;
        }

        for (var i = assemblers.Count - 1; i >= 0; i--)
        {
            if (!assemblers[i].Occupies(tile))
            {
                continue;
            }

            assemblers.RemoveAt(i);
            RefundBuilding(Crafter.AssemblerBuildingId);
            return true;
        }

        for (var i = generators.Count - 1; i >= 0; i--)
        {
            if (!generators[i].Occupies(tile))
            {
                continue;
            }

            generators.RemoveAt(i);
            RefundBuilding(Generator.BuildingId);
            return true;
        }

        for (var i = extractors.Count - 1; i >= 0; i--)
        {
            if (!extractors[i].Occupies(tile))
            {
                continue;
            }

            extractors.RemoveAt(i);
            RefundBuilding(Extractor.BuildingId);
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

    private IEnumerable<Crafter> CraftMachines() => State.CraftMachines();

    private bool FootprintClearExcept(
        GridPosition origin,
        int size,
        Miner? miner = null,
        Crafter? craft = null,
        Generator? generator = null)
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

    /// <summary>Advances the simulation by exactly one fixed tick (<see cref="SimulationClock.TickSeconds"/>).</summary>
    public void Step() => Simulation.Step(State);

    public void SetPowerBuffer(float buffer) => State.SetPowerBuffer(buffer);

    public void RecalculatePowerCapacity(int liveGeneratorCount) =>
        State.RecalculatePowerCapacity(liveGeneratorCount);

    /// <summary>Drain shared buffer; false on brownout (Raylib parity).</summary>
    public bool TrySpendPower(float amount) => State.TrySpendPower(amount);

    /// <summary>
    /// Spend only when the building is adjacency/node-connected to a live generator.
    /// </summary>
    public bool TrySpendPowerForBuilding(
        GridPosition origin,
        int size,
        float amount,
        IReadOnlyList<Generator>? liveGens = null) =>
        State.TrySpendPowerForBuilding(origin, size, amount, liveGens);

    /// <summary>
    /// Direct building→building (or building→core) when footprints touch (Raylib parity).
    /// Prefer over belts for compact layouts.
    /// </summary>
    public bool TryDeliverAdjacent(
        GridPosition origin,
        int size,
        string itemId,
        Crafter? excludeCrafter = null) =>
        State.TryDeliverAdjacent(origin, size, itemId, excludeCrafter);

    /// <summary>Guard for demo/seed layouts: throws if a scripted placement fails.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Demo seed fallito: {message}");
        }
    }
}
