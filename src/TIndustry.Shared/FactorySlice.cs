namespace TIndustry.Shared;

/// <summary>
/// Minimal playable factory slice: miner → belt lane → core stock.
/// Pure C#; Godot / Raylib wrap visuals around this.
/// </summary>
public sealed class FactorySlice
{
    private long nextItemId = 1;

    public FactorySlice(
        FactoryContent content,
        MinerProducer miner,
        BeltLane belt,
        IReadOnlySet<GridPosition> coreTiles,
        EconomyWallet? wallet = null)
    {
        Content = content;
        Miner = miner;
        Belt = belt;
        CoreTiles = coreTiles;
        Wallet = wallet ?? new EconomyWallet();
        // Items must wait at cells pointing into the core so CoreStockSink can stock them.
        Belt.AutoDropAtEnd = false;
    }

    public FactoryContent Content { get; }
    public MinerProducer Miner { get; }
    public BeltLane Belt { get; }
    public IReadOnlySet<GridPosition> CoreTiles { get; }
    public EconomyWallet Wallet { get; }
    public long CoreDeliveredItems { get; private set; }

    /// <summary>
    /// Demo layout matching the Godot spike L-belt: miner west of east-leg start, core south of south-leg end.
    /// </summary>
    public static FactorySlice CreateSpikeDemo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var beltDef = content.RequireConveyor(beltId);
        var path = BuildSpikeLPath();
        var belt = new BeltLane(path, beltDef);
        // 2×2 miner: (2,7)-(3,8); east outputs include (4,8) = belt start.
        var miner = new MinerProducer(new GridPosition(2, 7), Direction.East);
        // South leg ends at (10,13) facing south → core tile (10,14).
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), size: 2);
        return new FactorySlice(content, miner, belt, core);
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

    public void Tick(float deltaSeconds)
    {
        Miner.Tick(deltaSeconds, Belt, ref nextItemId);
        Belt.Tick(deltaSeconds);
        CoreDeliveredItems += CoreStockSink.Drain(Belt, CoreTiles, Wallet);
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
}
