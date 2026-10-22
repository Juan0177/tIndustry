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
        EconomySession session,
        MarketCatalog market)
    {
        Content = content;
        Belts = belts;
        CoreTiles = coreTiles;
        Wallet = wallet;
        Research = research;
        Session = session;
        Market = market;
        Exchange = new MarketState(content, 0);
    }

    public FactoryContent Content { get; }
    public BeltGrid Belts { get; }
    public IReadOnlySet<GridPosition> CoreTiles { get; }
    public EconomyWallet Wallet { get; }
    public ResearchState Research { get; }
    public MarketCatalog Market { get; }
    public EconomySession Session { get; }
    /// <summary>Price state of the exchange (spikes, crashes); <see cref="Market"/> is the catalog of base prices.</summary>
    public MarketState Exchange { get; }

    private TerrainMap? terrain;

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
    public bool AutoSellAtCore { get; set; }
    public int CoreUpgradeLevel { get; set; }
    public int CoreSaleBonusPercent { get; set; }
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

    public int EffectiveSalePrice(string itemId, int stockBeforeSell)
    {
        var price = Market.GetDynamicSellPrice(itemId, stockBeforeSell);
        if (CoreUpgradeLevel <= 0 || CoreSaleBonusPercent <= 0)
        {
            return price;
        }

        return price + price * CoreSaleBonusPercent / 100;
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

            if (CoreTiles.Contains(neighbor))
            {
                CoreDeliveredItems++;
                if (AutoSellAtCore)
                {
                    var price = EffectiveSalePrice(itemId, Wallet.MaterialCount(itemId));
                    Wallet.AddMoney(price);
                    Session.RecordSale(itemId, price);
                }
                else
                {
                    Wallet.AddMaterial(itemId, 1);
                }

                return true;
            }
        }

        return false;
    }

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
