namespace TIndustry.Shared;

/// <summary>
/// Whole mutable simulation state: buildings, belts, economy, power, counters.
/// <see cref="Simulation.Step"/> advances it; <see cref="Factory"/> is the facade that places/edits it.
/// Building lists are kept in ascending id order (see determinism rules).
/// </summary>
public sealed class GameState
{
    /// <summary>Next item id (field: passed by <c>ref</c> to buildings that emit items).</summary>
    public long NextItemId = 1;
    /// <summary>Next stable entity id (buildings and construction sites).</summary>
    public long NextEntityId = 1;

    public GameState(
        FactoryContent content,
        BeltGrid belts,
        IReadOnlySet<GridPosition> coreTiles,
        EconomyWallet wallet,
        ResearchState research,
        EconomySession session)
    {
        Content = content;
        Belts = belts;
        Wallet = wallet;
        Research = research;
        Session = session;
        Exchange = new MarketState(content, 0);
        Storage = new CoreStorage(this);
        // Given cells (new games, restored saves, tests): kept as they are, origin = their top-left corner.
        this.coreTiles = [.. coreTiles];
        CoreOrigin = coreTiles.Count == 0
            ? default
            : new GridPosition(coreTiles.Min(t => t.X), coreTiles.Min(t => t.Y));
        CoreStart = CoreOrigin;
        belts.BuildingTakesAt = TakesItemsAt;
        belts.BuildingReceives = ReceiveFromLogistics;
        belts.BuildingWouldReceive = CanReceiveFromLogistics;
    }

    /// <summary>
    /// A logistics block hands one <paramref name="itemId"/> to the building on <paramref name="tile"/> (a router's or a
    /// sorter's exit): the Core, a storage block, a forno or assembler, a generator, by the rules of a belt pointing into it.
    /// False when nothing there takes it.
    /// </summary>
    public bool ReceiveFromLogistics(GridPosition tile, string itemId)
    {
        if (coreTiles.Contains(tile))
        {
            if (!TryAcceptAtCore(itemId))
            {
                return false;
            }

            CoreDeliveredItems++;
            return true;
        }

        if (StorageAt(tile) is { } storage)
        {
            if (!TryAcceptAtStorage(storage, itemId))
            {
                return false;
            }

            CoreDeliveredItems += storage.IsLinked ? 1 : 0;
            return true;
        }

        foreach (var craft in CraftMachines())
        {
            if (craft.Occupies(tile))
            {
                return craft.UsesCoalOrPower && itemId == Crafter.FuelItemId
                    ? craft.TryAcceptFuel(itemId)
                    : craft.TryAccept(itemId);
            }
        }

        foreach (var generator in Generators)
        {
            if (generator.Occupies(tile))
            {
                return generator.TryAcceptFuel(itemId);
            }
        }

        return false;
    }

    /// <summary>Whether <see cref="ReceiveFromLogistics"/> would take <paramref name="itemId"/> (changes nothing).</summary>
    public bool CanReceiveFromLogistics(GridPosition tile, string itemId)
    {
        var storage = StorageAt(tile);
        if (coreTiles.Contains(tile) || storage is { IsLinked: true })
        {
            return Storage.CanAccept(itemId);
        }

        if (storage is not null)
        {
            return storage.CanAccept(itemId);
        }

        foreach (var craft in CraftMachines())
        {
            if (craft.Occupies(tile))
            {
                return craft.UsesCoalOrPower && itemId == Crafter.FuelItemId
                    ? craft.CanAcceptFuel(itemId)
                    : craft.CanAccept(itemId);
            }
        }

        foreach (var generator in Generators)
        {
            if (generator.Occupies(tile))
            {
                return generator.CanAcceptFuel(itemId);
            }
        }

        return false;
    }

    /// <summary>
    /// What an extractor can take from on <paramref name="tile"/>: the Core stock (a Core cell or a linked storage
    /// block), a standalone storage block, or a forno's or an assembler's output; nothing elsewhere (belts, other
    /// extractors, miners, generators).
    /// </summary>
    public ExtractorSource ExtractorSourceAt(GridPosition tile)
    {
        if (coreTiles.Contains(tile))
        {
            return ExtractorSource.Core(Storage);
        }

        if (StorageAt(tile) is { } storage)
        {
            return storage.IsLinked ? ExtractorSource.Core(Storage) : ExtractorSource.Of(storage);
        }

        foreach (var craft in CraftMachines())
        {
            if (craft.Occupies(tile))
            {
                return ExtractorSource.Of(craft);
            }
        }

        return default;
    }

    /// <summary>
    /// The block on <paramref name="tile"/> that takes items from logistics, as an identity: the logistics cell, the
    /// Core stock (<see cref="Storage"/>, for a Core cell and a linked storage block alike), the standalone storage
    /// block, the crafter or the generator; null when nothing there takes items. An extractor never hands an item back
    /// to the block it took it from (<see cref="ExtractorSource.Block"/>).
    /// </summary>
    public object? LogisticsBlockAt(GridPosition tile)
    {
        if (Belts.TryGet(tile, out var cell))
        {
            return cell;
        }

        if (coreTiles.Contains(tile))
        {
            return Storage;
        }

        if (StorageAt(tile) is { } storage)
        {
            return storage.IsLinked ? Storage : storage;
        }

        foreach (var craft in CraftMachines())
        {
            if (craft.Occupies(tile))
            {
                return craft;
            }
        }

        foreach (var generator in Generators)
        {
            if (generator.Occupies(tile))
            {
                return generator;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether a building on <paramref name="tile"/> takes items from logistics: the Core, a storage block, a forno or
    /// assembler, a generator (a junction hands items only toward one of these or a logistics cell).
    /// </summary>
    public bool TakesItemsAt(GridPosition tile)
    {
        if (coreTiles.Contains(tile) || StorageAt(tile) is not null)
        {
            return true;
        }

        foreach (var craft in CraftMachines())
        {
            if (craft.Occupies(tile))
            {
                return true;
            }
        }

        foreach (var generator in Generators)
        {
            if (generator.Occupies(tile))
            {
                return true;
            }
        }

        return false;
    }

    public FactoryContent Content { get; }
    public BeltGrid Belts { get; }
    private HashSet<GridPosition> coreTiles;

    /// <summary>Cells of the Core: <see cref="CoreOrigin"/> plus the size of <see cref="CoreTier"/> (<see cref="SetCore"/>).</summary>
    public IReadOnlySet<GridPosition> CoreTiles => coreTiles;

    /// <summary>Top-left cell of the Core.</summary>
    public GridPosition CoreOrigin { get; private set; }

    /// <summary>Core tier: index in <see cref="FactoryContent.CoreTiers"/> (0 shard, 1 foundation, 2 nucleus).</summary>
    public int CoreTier { get; internal set; }

    /// <summary>
    /// Top-left cell of the Core the game started with: the terrain generators made the map around it, so a save
    /// regenerates its terrain from it. <see cref="SetCore"/> does not move it.
    /// </summary>
    public GridPosition CoreStart { get; internal set; }

    /// <summary>The Core's storage: the single entry point of items into the Core stock.</summary>
    public CoreStorage Storage { get; }
    public EconomyWallet Wallet { get; }
    public ResearchState Research { get; }
    public EconomySession Session { get; }
    /// <summary>Price state of the exchange (spikes, crashes); base values B come from <c>Content.Market</c>.</summary>
    public MarketState Exchange { get; }

    /// <summary>
    /// True when the game has an active stock-exchange screen (<see cref="MarketScreen"/> finished and touching the Core):
    /// only then "sell all" can be a jackpot attempt. Recomputed by <see cref="RefreshMarketScreen"/>; tests may set it.
    /// </summary>
    public bool HasMarketScreen { get; internal set; }

    /// <summary>The finished stock-exchange screen (one per game), or null.</summary>
    public MarketScreen? MarketScreen { get; internal set; }

    /// <summary>
    /// Recomputes <see cref="MarketScreen.Active"/> (the screen touches a Core cell edge to edge) and
    /// <see cref="HasMarketScreen"/> from it. Called after the screen is placed or removed, by <see cref="SetCore"/> and on
    /// restore: only the screen and the Core decide it.
    /// </summary>
    public void RefreshMarketScreen()
    {
        if (MarketScreen is not { } screen)
        {
            HasMarketScreen = false;
            return;
        }

        screen.Active = MarketScreen.TouchesCore(screen.Position, coreTiles);
        HasMarketScreen = screen.Active;
    }

    /// <summary>Test hook: receives the jackpot probability (basis points) and decides the outcome; null draws from the market generator.</summary>
    internal Func<int, bool>? JackpotRollForTests { get; set; }

    private TerrainMap? terrain;

    // Warning: assigning Terrain reseeds the market; only construction and restore may set it.
    public TerrainMap? Terrain
    {
        get => terrain;
        set
        {
            terrain = value;
            // The market is seeded from the game seed, known only once the terrain is set.
            Exchange.Reseed(Content.MarketRules, value?.Seed ?? 0);
        }
    }

    public List<Miner> Miners { get; } = [];
    public List<Crafter> Smelters { get; } = [];
    public List<Crafter> Assemblers { get; } = [];
    public List<Generator> Generators { get; } = [];
    public List<Extractor> Extractors { get; } = [];
    public List<PowerNode> PowerNodes { get; } = [];
    public List<Lamp> Lamps { get; } = [];
    public List<Battery> Batteries { get; } = [];

    /// <summary>Lasers and power networks (<see cref="PowerGrid.Rebuild"/> after every change of power blocks or lasers).</summary>
    public PowerGrid PowerGrid { get; } = new();

    /// <summary>Containers and vaults, in ascending id order.</summary>
    public List<StorageBlock> Storages { get; } = [];

    /// <summary>Construction and deconstruction sites, in ascending id order.</summary>
    public List<ConstructSite> Sites { get; } = [];

    /// <summary>Construction drones, in ascending id order.</summary>
    public List<BuilderUnit> Builders { get; } = [];

    /// <summary>
    /// Events raised by the systems during the current tick (cleared at the start of each
    /// <see cref="Simulation.Step"/>; <see cref="SimHost"/> drains them after the tick). Not in the snapshot.
    /// </summary>
    public List<SimEvent> Events { get; } = [];

    /// <summary>Rules of this game (refund share, instant build, world time).</summary>
    public GameRules Rules { get; set; } = new();

    public string? ActiveCampaignLevelId { get; set; }
    public long CoreDeliveredItems { get; set; }
    /// <summary>Fixed ticks simulated so far (<see cref="SimulationClock.TickSeconds"/> each).</summary>
    public long CurrentTick { get; set; }

    /// <summary>
    /// Tick of the first block placed (a site opened or an instant placement, belts included): the world's time starts
    /// here (<see cref="DayNightClock"/>). Null until then; set once (<see cref="Factory.NoteFirstBuild"/>).
    /// </summary>
    public long? FirstBuildTick { get; set; }

    /// <summary>True once <see cref="FirstAttackDue"/> was raised (always night): it is raised once per game.</summary>
    public bool FirstAttackRaised { get; set; }

    public IEnumerable<Crafter> CraftMachines()
    {
        foreach (var s in Smelters)
        {
            yield return s;
        }

        foreach (var a in Assemblers)
        {
            yield return a;
        }
    }

    /// <summary>
    /// Puts the Core at <paramref name="origin"/> with tier <paramref name="tier"/>: its cells are recomputed from the
    /// tier's size, then the linked storage blocks and the market screen.
    /// </summary>
    public void SetCore(GridPosition origin, int tier)
    {
        if (tier < 0 || tier >= Content.CoreTiers.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(tier), tier, "Livello del CORE inesistente.");
        }

        CoreTier = tier;
        CoreOrigin = origin;
        coreTiles = CoreStockSink.MakeCoreTiles(origin, Content.CoreTiers[tier].Size);
        Storage.RecomputeLinks();
        RefreshMarketScreen();
    }

    /// <summary>
    /// Satisfaction of the network of power block <paramref name="entityId"/> in the last balance, 0–10 000 basis
    /// points; 0 for anything that is on no network (not a power block).
    /// </summary>
    public int SatisfactionOf(long entityId) => PowerGrid.NetworkOf(entityId)?.SatisfactionBp ?? 0;

    /// <summary>
    /// Direct building→building (or building→core) when footprints touch (Raylib parity).
    /// Prefer over belts for compact layouts.
    /// </summary>
    public bool TryDeliverAdjacent(
        GridPosition origin,
        int size,
        string itemId,
        Crafter? excludeCrafter = null)
    {
        foreach (var (neighbor, _) in BuildingIo.PerimeterSlots(origin, size))
        {
            foreach (var craft in CraftMachines())
            {
                if (excludeCrafter is not null && ReferenceEquals(craft, excludeCrafter))
                {
                    continue;
                }

                if (!craft.Occupies(neighbor))
                {
                    continue;
                }

                // Coal into forno fuel buffer when applicable.
                if (craft.UsesCoalOrPower
                    && itemId == Crafter.FuelItemId
                    && craft.TryAcceptFuel(itemId))
                {
                    return true;
                }

                if (craft.TryAccept(itemId))
                {
                    return true;
                }
            }

            // A full Core does not take it: the building keeps the piece in its output buffer.
            if (CoreTiles.Contains(neighbor) && TryAcceptAtCore(itemId))
            {
                CoreDeliveredItems++;
                return true;
            }

            // A storage block: linked ones are part of the Core, standalone ones keep the piece up to their space.
            if (StorageAt(neighbor) is { } storage && TryAcceptAtStorage(storage, itemId))
            {
                if (storage.IsLinked)
                {
                    CoreDeliveredItems++;
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>The storage block covering <paramref name="tile"/> (lowest id first), or null.</summary>
    public StorageBlock? StorageAt(GridPosition tile)
    {
        foreach (var storage in Storages)
        {
            if (storage.Occupies(tile))
            {
                return storage;
            }
        }

        return null;
    }

    /// <summary>
    /// One piece of <paramref name="itemId"/> arrives at <paramref name="storage"/> (belt or adjacent building): a
    /// linked block hands it to the Core (<see cref="TryAcceptAtCore"/>), a standalone one keeps it up to its space.
    /// False when it does not fit: the caller keeps the piece.
    /// </summary>
    internal bool TryAcceptAtStorage(StorageBlock storage, string itemId) =>
        storage.IsLinked ? TryAcceptAtCore(itemId) : storage.TryAccept(itemId);

    /// <summary>
    /// One piece of <paramref name="itemId"/> arrives at the Core (belt or adjacent building): stocked through
    /// <see cref="CoreStorage.TryAccept"/>; auto-sell sells from the stock at its own rate (<see cref="MarketSales.AutoSellTick"/>).
    /// False when the item is at its maximum: the caller keeps the piece.
    /// </summary>
    internal bool TryAcceptAtCore(string itemId) => Storage.TryAccept(itemId);
}
