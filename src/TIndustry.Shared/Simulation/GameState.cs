namespace TIndustry.Shared;

/// <summary>
/// Whole mutable simulation state: buildings, belts, economy, power, counters.
/// <see cref="Simulation.Step"/> advances it; <see cref="Factory"/> is the facade that places/edits it.
/// Building lists are kept in ascending id order (see determinism rules).
/// </summary>
public sealed class GameState
{
    /// <summary>Raylib <c>FactoryWorld.CorePowerCapacity</c>.</summary>
    public const float CorePowerCapacity = 24f;
    /// <summary>Raylib <c>FactoryWorld.CorePowerGeneration</c> (always ticks into buffer).</summary>
    public const float CorePowerGeneration = 12f;

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

    /// <summary>The Core's storage: the single entry point of items into the Core stock.</summary>
    public CoreStorage Storage { get; }
    public EconomyWallet Wallet { get; }
    public ResearchState Research { get; }
    public EconomySession Session { get; }
    /// <summary>Price state of the exchange (spikes, crashes); base values B come from <c>Content.Market</c>.</summary>
    public MarketState Exchange { get; }

    /// <summary>True when the game has a stock-exchange screen: only then "sell all" can be a jackpot attempt (set by tests until specification 2).</summary>
    public bool HasMarketScreen { get; internal set; }

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

    /// <summary>Construction and deconstruction sites, in ascending id order.</summary>
    public List<ConstructSite> Sites { get; } = [];

    /// <summary>Construction drones, in ascending id order.</summary>
    public List<BuilderUnit> Builders { get; } = [];

    /// <summary>
    /// Events raised by the systems during the current tick (cleared at the start of each
    /// <see cref="Simulation.Step"/>; <see cref="SimHost"/> drains them after the tick). Not in the snapshot.
    /// </summary>
    public List<SimEvent> Events { get; } = [];

    /// <summary>Rules of this game (refund share, instant build, night).</summary>
    public GameRules Rules { get; set; } = new();

    public string? ActiveCampaignLevelId { get; set; }
    public int CoreUpgradeLevel { get; set; }
    /// <summary>Shared power buffer (Raylib parity). Recharges from core + live gens.</summary>
    public float PowerBuffer { get; set; }
    /// <summary>Max buffer: core capacity + live generators × CapacityBonus.</summary>
    public float PowerCapacity { get; set; } = CorePowerCapacity;
    /// <summary>Last tick generation rate (core + live gens).</summary>
    public float PowerGenerationPerSecond { get; set; } = CorePowerGeneration;
    public long CoreDeliveredItems { get; set; }
    /// <summary>Fixed ticks simulated so far (<see cref="SimulationClock.TickSeconds"/> each).</summary>
    public long CurrentTick { get; set; }

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
    /// tier's size and the linked storage blocks are recomputed.
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
    }

    public void SetPowerBuffer(float buffer) =>
        PowerBuffer = Math.Clamp(buffer, 0f, Math.Max(PowerCapacity, CorePowerCapacity));

    public void RecalculatePowerCapacity(int liveGeneratorCount)
    {
        PowerCapacity = CorePowerCapacity
            + Math.Max(0, liveGeneratorCount) * Generator.CapacityBonus;
        PowerBuffer = Math.Min(PowerBuffer, PowerCapacity);
    }

    /// <summary>Drain shared buffer; false on brownout (Raylib parity).</summary>
    public bool TrySpendPower(float amount)
    {
        if (amount <= 0f)
        {
            return true;
        }

        if (PowerBuffer < amount)
        {
            return false;
        }

        PowerBuffer -= amount;
        return true;
    }

    /// <summary>
    /// Spend only when the building is adjacency/node-connected to a live generator.
    /// </summary>
    public bool TrySpendPowerForBuilding(
        GridPosition origin,
        int size,
        float amount,
        IReadOnlyList<Generator>? liveGens = null)
    {
        if (amount <= 0f)
        {
            return true;
        }

        liveGens ??= Generators.Where(g => g.IsGenerating).ToList();
        var connected = liveGens.Any(g => g.IsAdjacentTo(origin, size))
            || IsPoweredViaNode(origin, size, liveGens);
        if (!connected)
        {
            return false;
        }

        return TrySpendPower(amount);
    }

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
        }

        return false;
    }

    /// <summary>
    /// One piece of <paramref name="itemId"/> arrives at the Core (belt or adjacent building): auto-sold when its
    /// switch is on, else stocked through <see cref="CoreStorage.TryAccept"/>. False when the item is at its maximum:
    /// the caller keeps the piece.
    /// </summary>
    internal bool TryAcceptAtCore(string itemId) =>
        MarketSales.TryAutoSell(Exchange, Wallet, Session, itemId) || Storage.TryAccept(itemId);

    public bool IsPoweredViaNode(
        GridPosition craftOrigin,
        int craftSize,
        IReadOnlyList<Generator> liveGens)
    {
        if (liveGens.Count == 0 || PowerNodes.Count == 0)
        {
            return false;
        }

        foreach (var node in PowerNodes)
        {
            if (!node.IsAdjacentTo(craftOrigin, craftSize))
            {
                continue;
            }

            foreach (var gen in liveGens)
            {
                if (node.IsWithinRange(gen.Position, Generator.Size))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
