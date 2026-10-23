using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Sales at the click price, the per-piece drop and its recovery, the crash, SellAll and SaleResolved.</summary>
public sealed class MarketSaleTests
{
    private static readonly string ContentPath = Path.Combine(AppContext.BaseDirectory, "data", "content.json");
    private const int Second = 60;
    private const int Minute = 3600;
    private const string Plate = "iron-plate";
    private const string Ore = "iron-ore";

    private static Factory NewFactory(int seed = 42) =>
        NewGame.Sandbox(FactoryContent.Load(ContentPath), new GridPosition(60, 60), mapWidth: 120, mapHeight: 120,
            seed: seed, rules: new GameRules(InstantBuild: true));

    /// <summary>Holds the item at <paramref name="priceMilli"/> by pinning a spike top (the phase lasts ten seconds).</summary>
    private static MarketItemState Force(Factory f, string itemId, int priceMilli)
    {
        var item = f.State.Exchange.Find(itemId)!;
        item.Phase = MarketPhase.SpikeHold;
        item.PhaseStartTick = f.State.CurrentTick;
        item.SpikeTopMilli = priceMilli;
        item.PriceMilli = priceMilli;
        return item;
    }

    private static void Run(Factory f, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            f.Step();
        }
    }

    private static int Unit(Factory f, string itemId, int priceMilli) =>
        MarketMath.UnitIncome(f.State.Exchange.Find(itemId)!.BaseValue, priceMilli);

    [Fact]
    public void AllPiecesPaidAtClickPrice()
    {
        var f = NewFactory();
        Force(f, Plate, 2900);
        f.Wallet.AddMaterial(Plate, 10);
        var before = f.Wallet.Money;

        Assert.True(f.TrySellFromWallet(Plate, 10, 1));

        Assert.Equal(10 * 67, f.Wallet.Money - before);
        Assert.Equal(670, f.Session.SaleIncome);
        Assert.Equal(10, f.Session.SoldByItem[Plate]);
    }

    [Fact]
    public void EachPieceLowersPriceByFourHundredthsOfAPercent()
    {
        var f = NewFactory();
        var item = Force(f, Plate, 2900);
        f.Wallet.AddMaterial(Plate, 200);

        Assert.True(f.TrySellFromWallet(Plate, 200, 1));

        Assert.Equal(2820, item.PriceMilli);
    }

    [Fact]
    public void SingleSalesAddUp()
    {
        var f = NewFactory();
        var item = Force(f, Plate, 2900);
        f.Wallet.AddMaterial(Plate, 200);
        for (var i = 0; i < 200; i++)
        {
            Assert.True(f.TrySellFromWallet(Plate, 1, 1));
        }

        Assert.Equal(2820, item.PriceMilli);
    }

    [Fact]
    public void SmallSalesNeverGoBelowB()
    {
        var f = NewFactory();
        var item = Force(f, Ore, 1400);
        f.Wallet.AddMaterial(Ore, 2000);

        Assert.True(f.TrySellFromWallet(Ore, 2000, 1));
        Assert.Equal(1000, item.PriceMilli);
        Assert.NotEqual(MarketPhase.Crash, item.Phase);
        Run(f, 5);
        Assert.InRange(item.PriceMilli, 1000, 1005); // the drop is already recovering
    }

    [Fact]
    public void PriceRecoversInTwoMinutes()
    {
        var f = NewFactory();
        var item = Force(f, Plate, 2900);
        f.Wallet.AddMaterial(Plate, 1000);
        Assert.True(f.TrySellFromWallet(Plate, 1000, 1));
        Assert.Equal(400, item.SaleDropMilli);

        Run(f, 60 * Second);
        Assert.InRange(item.SaleDropMilli, 195, 205);
        Run(f, 60 * Second);
        Assert.Equal(0, item.SaleDropMilli);
    }

    [Fact]
    public void FiveThousandInTwoMinutesCrash()
    {
        var f = NewFactory();
        var item = f.State.Exchange.Find(Ore)!;
        f.Wallet.AddMaterial(Ore, 6000);
        Assert.True(f.TrySellFromWallet(Ore, 2000, 1));
        Run(f, 45 * Second);
        Assert.True(f.TrySellFromWallet(Ore, 2000, 1));
        Assert.NotEqual(MarketPhase.Crash, item.Phase);
        Run(f, 45 * Second);
        Assert.True(f.TrySellFromWallet(Ore, 2000, 1));
        Assert.Equal(MarketPhase.Crash, item.Phase);
    }

    [Fact]
    public void SalesThreeMinutesApartNeverCrash()
    {
        var f = NewFactory();
        var item = f.State.Exchange.Find(Ore)!;
        f.Wallet.AddMaterial(Ore, 6000);
        for (var i = 0; i < 3; i++)
        {
            Assert.True(f.TrySellFromWallet(Ore, 2000, 1));
            Run(f, 180 * Second);
        }

        Assert.NotEqual(MarketPhase.Crash, item.Phase);
    }

    [Fact]
    public void CrashLastsTwentyMinutesWithoutSpikes()
    {
        var f = NewFactory();
        var item = f.State.Exchange.Find(Ore)!;
        item.NextSpikeTick = f.State.CurrentTick + 5000;
        var remaining = item.NextSpikeTick - f.State.CurrentTick;
        var start = f.State.CurrentTick;
        f.Wallet.AddMaterial(Ore, 5000);
        Assert.True(f.TrySellFromWallet(Ore, 5000, 1));
        Assert.Equal(start + 20 * Minute, item.CrashEndTick);

        for (var i = 0; i < 20 * Minute; i++)
        {
            Assert.Equal(MarketPhase.Crash, item.Phase);
            f.Step();
        }

        f.Step(); // the tick at which the crash ends
        Assert.Equal(MarketPhase.Normal, item.Phase);
        var end = start + 20 * Minute;
        Assert.Equal(end + remaining, item.NextSpikeTick);
        while (item.Phase != MarketPhase.SpikeRise)
        {
            f.Step();
        }

        Assert.Equal(end + remaining, item.PhaseStartTick);
    }

    [Fact]
    public void CrashDipsOnlyInFirstFiveMinutes()
    {
        var f = NewFactory();
        var item = f.State.Exchange.Find(Ore)!;
        f.Wallet.AddMaterial(Ore, 5000);
        Assert.True(f.TrySellFromWallet(Ore, 5000, 1));
        var dips = 0;
        for (var i = 0; i < 20 * Minute; i++)
        {
            f.Step();
            var p = item.PriceMilli;
            if (i < 5 * Minute)
            {
                Assert.True(p is (>= 600 and <= 700) or (>= 1000 and <= 1040), "tick " + i + ": " + p);
                dips += p < 1000 ? 1 : 0;
            }
            else
            {
                Assert.InRange(p, 1000, 1040);
            }
        }

        Assert.True(dips > 0);
    }

    [Fact]
    public void SellAllSellsTheWholeStock()
    {
        var f = NewFactory();
        var host = new SimHost(f);
        var rejected = new List<RefusalCode>();
        host.CommandRejected += (_, r) => rejected.Add(r.Code);
        f.Wallet.AddMaterial(Plate, 50);
        var stock = f.Wallet.MaterialCount(Plate);
        var price = f.State.Exchange.Find(Plate)!.PriceMilli;
        var before = f.Wallet.Money;

        host.Submit(new SellAll(1, Plate));
        host.Submit(new SellAll(1, Plate));
        host.Advance(SimulationClock.TickSeconds);

        Assert.Equal(0, f.Wallet.MaterialCount(Plate));
        Assert.Equal(stock * Unit(f, Plate, price), f.Wallet.Money - before);
        Assert.Equal([RefusalCode.SellFailed], rejected);
    }

    [Fact]
    public void SellOfUnknownItemOrTooManyPiecesFails()
    {
        var f = NewFactory();
        var host = new SimHost(f);
        var rejected = new List<RefusalCode>();
        host.CommandRejected += (_, r) => rejected.Add(r.Code);
        f.Wallet.AddMaterial(Plate, 5);
        var stockBefore = f.Wallet.MaterialCount(Plate);

        host.Submit(new Sell(1, "nothing", 1));
        host.Submit(new Sell(1, Plate, 1_000_000));
        host.Submit(new Sell(1, Plate, 0));
        host.Advance(SimulationClock.TickSeconds);

        Assert.Equal([RefusalCode.SellFailed, RefusalCode.SellFailed, RefusalCode.InvalidCommand], rejected);
        Assert.Equal(stockBefore, f.Wallet.MaterialCount(Plate));
    }

    [Fact]
    public void SaleResolvedReportsTheSale()
    {
        var f = NewFactory();
        var host = new SimHost(f);
        var events = new List<SaleResolved>();
        host.EventRaised += e => { if (e is SaleResolved s) { events.Add(s); } };
        Force(f, Plate, 2900);
        f.Wallet.AddMaterial(Plate, 10);
        f.Wallet.AddMaterial(Ore, 5000);
        var oreStock = f.Wallet.MaterialCount(Ore);

        host.Submit(new Sell(1, Plate, 10));
        host.Submit(new SellAll(1, Ore));
        host.Advance(SimulationClock.TickSeconds);

        Assert.Equal(2, events.Count);
        Assert.Equal(new SaleResolved(1, Plate, 10, 2900, 670, false, false, false, 0), events[0]);
        Assert.Equal(Ore, events[1].ItemId);
        Assert.Equal(oreStock, events[1].Pieces);
        Assert.True(events[1].Crash);
        Assert.False(events[1].Attempt || events[1].Jackpot);
    }
}
