namespace TIndustry.Shared;

/// <summary>
/// Minimal playable factory slice: miner → placeable belt grid → core stock.
/// </summary>
public sealed class FactorySlice
{
    private long nextItemId = 1;

    public FactorySlice(
        FactoryContent content,
        MinerProducer miner,
        BeltGrid belts,
        IReadOnlySet<GridPosition> coreTiles,
        EconomyWallet? wallet = null)
    {
        Content = content;
        Miner = miner;
        Belts = belts;
        CoreTiles = coreTiles;
        Wallet = wallet ?? new EconomyWallet();
        BeltDefinition = belts.DefaultDefinition
            ?? content.RequireConveyor("conveyor-basic");
    }

    public FactoryContent Content { get; }
    public MinerProducer Miner { get; }
    public BeltGrid Belts { get; }
    public ConveyorDefinition BeltDefinition { get; }
    public IReadOnlySet<GridPosition> CoreTiles { get; }
    public EconomyWallet Wallet { get; }
    public long CoreDeliveredItems { get; private set; }

    /// <summary>
    /// Demo layout: miner west of east-leg, core south of south-leg; seed L path (editable).
    /// </summary>
    public static FactorySlice CreateSpikeDemo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var beltDef = content.RequireConveyor(beltId);
        var grid = new BeltGrid();
        grid.PlacePath(BuildSpikeLPath(), beltDef);
        var miner = new MinerProducer(new GridPosition(2, 7), Direction.East);
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        return new FactorySlice(content, miner, grid, core);
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

    public bool CanOccupy(GridPosition position)
    {
        if (CoreTiles.Contains(position))
        {
            return false;
        }

        foreach (var tile in Miner.OccupiedTiles())
        {
            if (tile.Equals(position))
            {
                return false;
            }
        }

        return position.X >= 0 && position.Y >= 0;
    }

    public bool TryPlaceBelt(GridPosition position, Direction direction) =>
        Belts.TryPlaceFree(position, direction, BeltDefinition, CanOccupy);

    public bool TryRemoveBelt(GridPosition position) => Belts.TryRemove(position);

    public void Tick(float deltaSeconds)
    {
        Miner.Tick(deltaSeconds, Belts, ref nextItemId);
        Belts.Tick(deltaSeconds);
        CoreDeliveredItems += CoreStockSink.Drain(Belts, CoreTiles, Wallet);
    }

    /// <summary>Headless smoke: mine until at least one ore is stocked, or timeout.</summary>
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

    /// <summary>Place a fresh L corner path and confirm corner detection + stock.</summary>
    public static void SelfTestPlaceable(string contentJsonPath)
    {
        var content = FactoryContent.Load(contentJsonPath);
        var beltDef = content.RequireConveyor();
        var grid = new BeltGrid();
        var miner = new MinerProducer(new GridPosition(2, 7), Direction.East);
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        var slice = new FactorySlice(content, miner, grid, core);

        // Minimal L: east then south into core.
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
        Assert(!grid.IsCorner(new GridPosition(5, 8)), "straight (5,8)");

        // Re-orient corner cell to east (no longer corner), then back.
        Assert(grid.TryOrient(new GridPosition(10, 8), Direction.East), "orient east");
        Assert(!grid.IsCorner(new GridPosition(10, 8)), "not corner after orient");
        Assert(grid.TryOrient(new GridPosition(10, 8), Direction.South), "orient south");
        Assert(grid.IsCorner(new GridPosition(10, 8)), "corner again");

        Assert(slice.TryRemoveBelt(new GridPosition(7, 8)), "remove mid");
        Assert(!grid.Contains(new GridPosition(7, 8)), "removed");
        Assert(slice.TryPlaceBelt(new GridPosition(7, 8), Direction.East), "replace mid");

        _ = beltDef;
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

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Assert fallito: {message}");
        }
    }
}
