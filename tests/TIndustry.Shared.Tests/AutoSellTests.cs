using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Auto-sell sells from the Core stock at the tier's rate (pieces per second), one switch per item, paying
/// min(B, price) without touching the market; "only when the Core is full" keeps the stock near its maximum.
/// </summary>
public sealed class AutoSellTests
{
    private static readonly string ContentPath = Path.Combine(AppContext.BaseDirectory, "data", "content.json");
    private const string Coal = "coal";
    private const string Plate = "iron-plate";
    private const string Iron = "iron-ore";

    private static Factory NewFactory(int seed = 42) =>
        NewGame.Sandbox(FactoryContent.Load(ContentPath), new GridPosition(60, 60), mapWidth: 120, mapHeight: 120,
            seed: seed, rules: new GameRules(InstantBuild: true));

    private static void SetStock(Factory f, string itemId, int amount)
    {
        var have = f.Wallet.MaterialCount(itemId);
        if (have < amount)
        {
            f.Wallet.AddMaterial(itemId, amount - have);
        }
        else
        {
            Assert.True(have == amount || f.Wallet.TryRemoveMaterial(itemId, have - amount));
        }
    }

    private static MarketItemState Force(Factory f, string itemId, int priceMilli)
    {
        var item = f.State.Exchange.Find(itemId)!;
        item.Phase = MarketPhase.SpikeHold;
        item.PhaseStartTick = f.State.CurrentTick;
        item.SpikeTopMilli = priceMilli;
        item.PriceMilli = priceMilli;
        return item;
    }

    private static int MinPrice(MarketItemState item) =>
        Math.Min(item.BaseValue, MarketMath.UnitIncome(item.BaseValue, item.PriceMilli));

    /// <summary>Runs <paramref name="seconds"/> and returns the pieces sold and the money expected from them at the price of each tick.</summary>
    private static (int Sold, long Expected) Run(Factory f, string itemId, float seconds)
    {
        var item = f.State.Exchange.Find(itemId)!;
        var sold = 0;
        long expected = 0;
        for (var i = 0; i < (int)(seconds * SimulationClock.TickRate); i++)
        {
            var before = f.Wallet.MaterialCount(itemId);
            var price = MinPrice(item);
            f.Step();
            var delta = before - f.Wallet.MaterialCount(itemId);
            sold += delta;
            expected += (long)delta * price;
        }

        return (sold, expected);
    }

    [Fact]
    public void AutoSell_Shard_Sells10PerSecond()
    {
        var f = NewFactory();
        SetStock(f, Plate, 1000);
        var item = f.State.Exchange.Find(Plate)!;
        item.AutoSell = true;
        var money = f.Wallet.Money;

        var (sold, expected) = Run(f, Plate, 10f);

        Assert.Equal(100, sold);
        Assert.Equal(900, f.Wallet.MaterialCount(Plate));
        Assert.Equal(expected, f.Wallet.Money - money);
        Assert.True(expected > 0);
        Assert.Equal(0, item.SaleDropMicro);
    }

    [Fact]
    public void AutoSell_RateFollowsTier()
    {
        foreach (var (tier, rate) in new[] { (1, 25), (2, 60) })
        {
            var f = NewFactory();
            f.State.SetCore(new GridPosition(60, 60), tier);
            SetStock(f, Plate, 3000);
            f.State.Exchange.Find(Plate)!.AutoSell = true;

            var (sold, _) = Run(f, Plate, 10f);

            Assert.Equal(rate * 10, sold);
        }
    }

    [Fact]
    public void AutoSell_WhenFull_WaitsUntilNearCapacity()
    {
        var f = NewFactory();
        var item = f.State.Exchange.Find(Plate)!;
        item.AutoSell = true;
        item.AutoSellWhenFull = true;
        SetStock(f, Plate, 500);

        Assert.Equal(0, Run(f, Plate, 10f).Sold);
        Assert.Equal(500, f.Wallet.MaterialCount(Plate));

        SetStock(f, Plate, 1000);
        var lowest = 1000;
        for (var i = 0; i < 10 * SimulationClock.TickRate; i++)
        {
            f.Step();
            lowest = Math.Min(lowest, f.Wallet.MaterialCount(Plate));
        }

        Assert.True(lowest < 1000);
        Assert.True(lowest >= 990, $"stock fell to {lowest}");
    }

    private static long nextId = 7_000_000;

    /// <summary>Express belts carry 7.2 per second each: a "line" of 8 or 15 per second is several of them side by side on the Core's west side.</summary>
    private static (Factory F, GridPosition[] Starts) FullShardWithLines(string itemId, int lines)
    {
        var f = NewFactory();
        f.UnlockAllForDemo();
        f.Research.ForceUnlock("conveyor-express");
        foreach (var material in new[] { "titanium-ore", "silicon", "graphite" })
        {
            f.Wallet.AddMaterial(material, 50);
        }

        var starts = new List<GridPosition>();
        for (var row = 0; row < lines; row++)
        {
            for (var i = 0; i < 6; i++)
            {
                Assert.True(f.TryPlaceBelt(new GridPosition(54 + i, 60 + row), Direction.East, "conveyor-express"));
            }

            starts.Add(new GridPosition(54, 60 + row));
        }

        SetStock(f, itemId, 1000);
        var item = f.State.Exchange.Find(itemId)!;
        item.AutoSell = true;
        item.AutoSellWhenFull = true;
        return (f, [.. starts]);
    }

    /// <summary>Feeds <paramref name="perSecond"/> pieces per second in all, split over the lines; returns how many insertions were refused.</summary>
    private static int Feed(Factory f, GridPosition[] starts, string itemId, int perSecond, int seconds)
    {
        var refused = 0;
        var acc = 0;
        for (var i = 0; i < seconds * SimulationClock.TickRate; i++)
        {
            acc += perSecond;
            while (acc >= SimulationClock.TickRate)
            {
                acc -= SimulationClock.TickRate;
                var start = starts[(i + acc) % starts.Length];
                if (!f.Belts.TryInsert(start, new TransportedItem(Interlocked.Increment(ref nextId), itemId), Direction.East))
                {
                    refused++;
                }
            }

            f.Step();
        }

        return refused;
    }

    [Fact]
    public void AutoSell_KeepsLineFlowing()
    {
        var (f, starts) = FullShardWithLines(Iron, 2);

        Assert.Equal(0, Feed(f, starts, Iron, 8, 60));
        Assert.True(f.Wallet.MaterialCount(Iron) >= 990);
    }

    [Fact]
    public void AutoSell_LineFasterThanRate_Stops()
    {
        var (f, starts) = FullShardWithLines(Iron, 3);

        var refused = Feed(f, starts, Iron, 15, 60);

        Assert.True(refused > 0);
        Assert.InRange(f.Wallet.MaterialCount(Iron), 990, 1000);
    }

    [Fact]
    public void AutoSell_OffResetsCarry()
    {
        var f = NewFactory();
        SetStock(f, Plate, 1000);
        var item = f.State.Exchange.Find(Plate)!;
        item.AutoSell = true;
        f.RunTicks(5);
        Assert.NotEqual(0, item.AutoSellCarry);
        item.AutoSell = false;
        f.RunTicks(1);
        Assert.Equal(0, item.AutoSellCarry);
    }

    [Fact]
    public void AutoSellIsPerItem()
    {
        var f = NewFactory();
        SetStock(f, Coal, 100);
        SetStock(f, Iron, 100);
        f.State.Exchange.Find(Coal)!.AutoSell = true;

        f.RunSeconds(2f);

        Assert.True(f.Wallet.MaterialCount(Coal) < 100);
        Assert.Equal(100, f.Wallet.MaterialCount(Iron));
        Assert.True(f.Session.SoldByItem[Coal] > 0);
        Assert.False(f.Session.SoldByItem.ContainsKey(Iron));
    }

    [Fact]
    public void AutoSellPaysTheMinimumPrice()
    {
        foreach (var (price, expectedUnit) in new[] { (1300, -1), (650, MarketMath.UnitIncome(10, 650)) })
        {
            var f = NewFactory();
            SetStock(f, Coal, 100);
            var item = f.State.Exchange.Find(Coal)!;
            item.AutoSell = true;
            Force(f, Coal, price);
            var unit = MinPrice(item);
            if (expectedUnit < 0)
            {
                Assert.Equal(item.BaseValue, unit);
            }

            var money = f.Wallet.Money;
            var stock = f.Wallet.MaterialCount(Coal);
            f.Step();
            var sold = stock - f.Wallet.MaterialCount(Coal);
            Assert.Equal(unit * sold, f.Wallet.Money - money);
        }
    }

    [Fact]
    public void AutoSellDoesNotMoveTheMarket()
    {
        static (int Price, int Drop, long Recovery, int[] Window, int Roll) Go(bool autoSell)
        {
            var f = NewFactory();
            SetStock(f, Coal, 100);
            var item = Force(f, Coal, 1800);
            item.AutoSell = autoSell;
            f.RunTicks(30);
            return (item.PriceMilli, item.SaleDropMicro, item.SaleRecoveryEndTick, (int[])item.WindowPieces.Clone(),
                f.State.Exchange.Random.NextInt(1_000_000));
        }

        var on = Go(true);
        var off = Go(false);

        Assert.Equal(off.Price, on.Price);
        Assert.Equal(0, on.Drop);
        Assert.Equal(off.Recovery, on.Recovery);
        Assert.Equal(off.Window, on.Window);
        Assert.Equal(off.Roll, on.Roll);
    }

    [Fact]
    public void SetAutoSellTogglesOneItemAndRefusesUnknownOnes()
    {
        var f = NewFactory();
        var host = new SimHost(f);
        var rejected = new List<RefusalCode>();
        host.CommandRejected += (_, r) => rejected.Add(r.Code);

        host.Submit(new SetAutoSell(1, Coal, true));
        host.Submit(new SetAutoSell(1, "not-an-item", true));
        host.Advance(SimulationClock.TickSeconds);

        Assert.True(f.State.Exchange.Find(Coal)!.AutoSell);
        Assert.False(f.State.Exchange.Find(Iron)!.AutoSell);
        Assert.Equal([RefusalCode.InvalidCommand], rejected);
    }

    [Fact]
    public void SetAutoSellWhenFull_TogglesAndRoundTrips_AndUnknownItemRefused()
    {
        var f = NewFactory();
        var host = new SimHost(f);
        var rejected = new List<RefusalCode>();
        host.CommandRejected += (_, r) => rejected.Add(r.Code);

        host.Submit(new SetAutoSellWhenFull(1, Coal, true));
        host.Submit(new SetAutoSellWhenFull(1, "not-an-item", true));
        host.Advance(SimulationClock.TickSeconds);

        var item = f.State.Exchange.Find(Coal)!;
        Assert.True(item.AutoSellWhenFull);
        Assert.True(host.State.MarketItem(Coal)!.AutoSellWhenFull);
        Assert.Equal([RefusalCode.InvalidCommand], rejected);

        item.AutoSellCarry = 45;
        var restored = Factory.Restore(FactoryContent.Load(ContentPath), f.Capture());
        var back = restored.State.Exchange.Find(Coal)!;
        Assert.True(back.AutoSellWhenFull);
        Assert.Equal(45, back.AutoSellCarry);
    }
}
