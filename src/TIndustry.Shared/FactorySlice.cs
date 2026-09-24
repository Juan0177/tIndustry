namespace TIndustry.Shared;

/// <summary>
/// Playable factory slice: miners → belts → optional forni → core stock.
/// </summary>
public sealed class FactorySlice
{
    private long nextItemId = 1;
    private readonly List<MinerProducer> miners = [];
    private readonly List<SmelterStub> smelters = [];

    public FactorySlice(
        FactoryContent content,
        BeltGrid belts,
        IReadOnlySet<GridPosition> coreTiles,
        EconomyWallet? wallet = null)
    {
        Content = content;
        Belts = belts;
        CoreTiles = coreTiles;
        Wallet = wallet ?? new EconomyWallet();
        BeltDefinition = belts.DefaultDefinition
            ?? content.RequireConveyor("conveyor-basic");
    }

    public FactoryContent Content { get; }
    public BeltGrid Belts { get; }
    public ConveyorDefinition BeltDefinition { get; }
    public IReadOnlySet<GridPosition> CoreTiles { get; }
    public EconomyWallet Wallet { get; }
    public long CoreDeliveredItems { get; private set; }
    public IReadOnlyList<MinerProducer> Miners => miners;
    public IReadOnlyList<SmelterStub> Smelters => smelters;

    /// <summary>Primary / first miner (compat for HUD).</summary>
    public MinerProducer? Miner => miners.Count > 0 ? miners[0] : null;

    /// <summary>Phase C seed: miner → belts → forno → belts → core (plates stock).</summary>
    public static FactorySlice CreatePhaseCDemo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var beltDef = content.RequireConveyor(beltId);
        var recipe = content.FindRecipe("smelt-iron")
            ?? throw new InvalidDataException("Ricetta smelt-iron mancante.");
        var grid = new BeltGrid();
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var slice = new FactorySlice(content, grid, core);

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

    /// <summary>Legacy L-belt demo without forno (ore → core).</summary>
    public static FactorySlice CreateSpikeDemo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var beltDef = content.RequireConveyor(beltId);
        var grid = new BeltGrid();
        grid.PlacePath(BuildSpikeLPath(), beltDef);
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var slice = new FactorySlice(content, grid, core);
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

        foreach (var smelter in smelters)
        {
            if (smelter.Occupies(tile))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryPlaceBelt(GridPosition position, Direction direction) =>
        Belts.TryPlaceFree(position, direction, BeltDefinition, CanOccupy);

    public bool TryRemoveBelt(GridPosition position) => Belts.TryRemove(position);

    public bool TryPlaceMiner(GridPosition origin, Direction direction)
    {
        if (!CanOccupyFootprint(origin, MinerProducer.Size))
        {
            // Allow relocating the single Phase-C miner onto a free footprint.
            if (miners.Count == 1 && FootprintClearExcept(origin, MinerProducer.Size, miners[0]))
            {
                // Relocate: remove conceptual occupancy by replacing miner instance.
                miners[0] = new MinerProducer(origin, direction);
                return true;
            }

            return false;
        }

        miners.Add(new MinerProducer(origin, direction));
        return true;
    }

    public bool TryPlaceSmelter(GridPosition origin, Direction direction, RecipeDefinition? recipe = null)
    {
        recipe ??= Content.FindRecipe("smelt-iron")
            ?? throw new InvalidDataException("Ricetta smelt-iron mancante.");

        if (!CanOccupyFootprint(origin, SmelterStub.Size))
        {
            if (smelters.Count == 1 && FootprintClearExcept(origin, SmelterStub.Size, smelter: smelters[0]))
            {
                smelters[0].Relocate(origin, direction);
                return true;
            }

            return false;
        }

        smelters.Add(new SmelterStub(origin, direction, recipe));
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

                // Keep at least one miner in demos? Allow remove all.
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

        return false;
    }

    private bool FootprintClearExcept(
        GridPosition origin,
        int size,
        MinerProducer? miner = null,
        SmelterStub? smelter = null)
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

                foreach (var s in smelters)
                {
                    if (smelter is not null && ReferenceEquals(s, smelter))
                    {
                        continue;
                    }

                    if (s.Occupies(tile))
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

        // Belts advance first so handoffs reach smelter/core edges.
        Belts.Tick(deltaSeconds);

        foreach (var smelter in smelters)
        {
            smelter.Tick(deltaSeconds, Belts, ref nextItemId);
        }

        // Second belt tick so freshly emitted plates can move the same frame.
        Belts.Tick(0f);
        CoreDeliveredItems += CoreStockSink.Drain(Belts, CoreTiles, Wallet);
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

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Assert fallito: {message}");
        }
    }
}
