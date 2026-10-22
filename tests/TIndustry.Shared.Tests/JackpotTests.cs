using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Jackpot chance, pity per item, the wait after a missed attempt and the recovery boost of a lost 50/50.</summary>
public sealed class JackpotTests
{
    private static readonly string ContentPath = Path.Combine(AppContext.BaseDirectory, "data", "content.json");
    private const int Second = 60;
    private const string Plate = "iron-plate";
    private const string Ore = "iron-ore";
    private const string Titanium = "titanium-plate";

    private static Factory NewFactory(int seed = 42)
    {
        var f = NewGame.Sandbox(FactoryContent.Load(ContentPath), new GridPosition(60, 60), mapWidth: 120, mapHeight: 120,
            seed: seed, rules: new GameRules(InstantBuild: true));
        f.State.HasMarketScreen = true;
        return f;
    }

    private static MarketRules Rules(Factory f) => f.State.Content.MarketRules!;

    /// <summary>Holds the item at <paramref name="priceMilli"/> in a spike hold, with a clean window.</summary>
    private static MarketItemState Force(Factory f, string itemId, int priceMilli = 2900)
    {
        var item = f.State.Exchange.Find(itemId)!;
        item.Phase = MarketPhase.SpikeHold;
        item.PhaseStartTick = f.State.CurrentTick;
        item.SpikeTopMilli = priceMilli;
        item.PriceMilli = priceMilli;
        Array.Clear(item.WindowPieces);
        return item;
    }

    /// <summary>Records the probabilities asked and answers with the given outcomes in order.</summary>
    private static List<int> Script(Factory f, params bool[] outcomes)
    {
        var asked = new List<int>();
        var next = 0;
        f.State.JackpotRollForTests = p =>
        {
            asked.Add(p);
            return outcomes[next++];
        };
        return asked;
    }

    /// <summary>One "sell all" of exactly <paramref name="pieces"/> in a fresh spike, past any wait.</summary>
    private static void Attempt(Factory f, string itemId, int pieces)
    {
        f.State.Exchange.SellAllBlockedUntilTick = 0;
        Force(f, itemId);
        f.Wallet.TryRemoveMaterial(itemId, f.Wallet.MaterialCount(itemId));
        f.Wallet.AddMaterial(itemId, pieces);
        Assert.True(f.TrySellAll(itemId));
    }

    private static void SetStock(Factory f, string itemId, int pieces)
    {
        f.Wallet.TryRemoveMaterial(itemId, f.Wallet.MaterialCount(itemId));
        f.Wallet.AddMaterial(itemId, pieces);
    }

    private static void Run(Factory f, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            f.Step();
        }
    }

    [Theory]
    [InlineData(399, 0, 0)]
    [InlineData(400, 0, 200)]
    [InlineData(600, 0, 300)]
    [InlineData(3000, 0, 1500)]
    [InlineData(10_000, 0, 5000)]
    [InlineData(12_000, 0, 5000)]
    [InlineData(3000, 1, 4000)]
    [InlineData(3000, 3, 10_000)]
    [InlineData(2999, 3, 1400)]
    public void ChanceTable(int pieces, int misses, int expected) =>
        Assert.Equal(expected, MarketMath.JackpotChance(Rules(NewFactory()), pieces, misses));

    [Fact]
    public void NoAttemptOutsideSpikeBelowFourHundredOrWithoutScreen()
    {
        var f = NewFactory();
        var asked = Script(f, true, true, true);
        var item = f.State.Exchange.Find(Plate)!;

        // Outside the spike (price under the threshold).
        Force(f, Plate, 2000);
        SetStock(f, Plate, 1000);
        Assert.True(f.TrySellAll(Plate));
        // Under four hundred pieces.
        Force(f, Plate);
        f.Wallet.AddMaterial(Plate, 399);
        Assert.True(f.TrySellAll(Plate));
        // No exchange screen.
        f.State.HasMarketScreen = false;
        Force(f, Plate);
        SetStock(f, Plate, 1000);
        Assert.True(f.TrySellAll(Plate));

        Assert.Empty(asked);
        Assert.Equal(0, f.State.Exchange.SellAllBlockedUntilTick);
        Assert.Equal(0, item.PityMisses);
    }

    [Fact]
    public void PityScenarioA()
    {
        var f = NewFactory();
        var asked = Script(f, false, false, false, true);
        for (var i = 0; i < 4; i++)
        {
            Attempt(f, Plate, 5000);
        }

        Assert.Equal([2500, 5000, 7500, 10_000], asked);
        Assert.Equal(0, f.State.Exchange.Find(Plate)!.PityMisses);
    }

    [Fact]
    public void PityScenarioB()
    {
        var f = NewFactory();
        var asked = Script(f, false, true, false, false);
        for (var i = 0; i < 4; i++)
        {
            Attempt(f, Plate, 3000);
        }

        Assert.Equal([1500, 4000, 1500, 4000], asked);
    }

    [Fact]
    public void PityScenarioC()
    {
        var f = NewFactory();
        var asked = Script(f, false, false, false, false, false, true);
        foreach (var pieces in new[] { 1000, 3500, 800, 3500, 3500, 3500 })
        {
            Attempt(f, Plate, pieces);
        }

        Assert.Equal([500, 1700, 400, 4200, 6700, 10_000], asked);
    }

    [Fact]
    public void PityScenarioD()
    {
        var f = NewFactory();
        var asked = Script(f, false, false, true);
        for (var i = 0; i < 3; i++)
        {
            Attempt(f, Plate, 10_000);
        }

        Assert.Equal([5000, 7500, 10_000], asked);
    }

    [Fact]
    public void PityScenarioE()
    {
        var f = NewFactory();
        var asked = Script(f, true, false);
        Attempt(f, Plate, 400);
        Assert.Equal(0, f.State.Exchange.Find(Plate)!.PityMisses);
        Attempt(f, Plate, 400);

        Assert.Equal([200, 200], asked);
        Assert.Equal(0, f.State.Exchange.Find(Plate)!.PityMisses);
    }

    [Fact]
    public void PityIsPerItem()
    {
        var f = NewFactory();
        Script(f, false, false);
        Attempt(f, Plate, 3000);
        Attempt(f, Ore, 3000);

        Assert.Equal(1, f.State.Exchange.Find(Plate)!.PityMisses);
        Assert.Equal(1, f.State.Exchange.Find(Ore)!.PityMisses);
    }

    [Fact]
    public void SmallAttemptDoesNotTouchThePitySeries()
    {
        var f = NewFactory();
        var item = f.State.Exchange.Find(Plate)!;
        item.PityMisses = 2;
        var asked = Script(f, false);

        Attempt(f, Plate, 400);

        Assert.Equal([200], asked);
        Assert.Equal(2, item.PityMisses);
    }

    [Fact]
    public void JackpotPaysBasePlusBonusPlusRise()
    {
        var f = NewFactory();
        Script(f, true);
        var item = new MarketItemState("test-item", 10) { PriceMilli = 3000, WindowPieces = new int[120] };
        f.Wallet.AddMaterial("test-item", 400);
        var before = f.Wallet.Money;

        Assert.NotNull(MarketSales.Sell(f.State, item, 400, sellAll: true));

        Assert.Equal(400 * 107, f.Wallet.Money - before);
        Assert.Equal(400 * 107, f.Session.SaleIncome);
    }

    [Fact]
    public void JackpotCrashesAndResetsPity()
    {
        var f = NewFactory();
        var events = new List<SaleResolved>();
        var host = new SimHost(f);
        host.EventRaised += e => { if (e is SaleResolved s) { events.Add(s); } };
        var item = Force(f, Plate);
        item.PityMisses = 2;
        Script(f, true);
        f.Wallet.AddMaterial(Plate, 3000);

        host.Submit(new SellAll(1, Plate));
        host.Advance(1);

        Assert.Equal(MarketPhase.Crash, item.Phase);
        Assert.Equal(0, item.PityMisses);
        var sale = Assert.Single(events);
        Assert.True(sale.Attempt && sale.Jackpot && sale.Crash);
        Assert.Equal(1500 + 2500 * 2, sale.ProbabilityBasisPoints);
        Assert.Equal(0, f.State.Exchange.SellAllBlockedUntilTick);
    }

    [Fact]
    public void MissWithoutCrashBlocksSellAllEverywhereAndDelaysThatSpike()
    {
        var f = NewFactory();
        var host = new SimHost(f);
        var rejected = new List<Refusal>();
        host.CommandRejected += (_, r) => rejected.Add(r);
        var item = Force(f, Plate);
        Force(f, Ore);
        Script(f, false, false);
        SetStock(f, Plate, 1000);
        SetStock(f, Ore, 1000);
        var now = f.State.CurrentTick;
        var rules = Rules(f);

        host.Submit(new SellAll(1, Plate));
        host.Submit(new SellAll(1, Ore));
        host.Advance(1);

        Assert.Equal(now + 300 * Second, f.State.Exchange.SellAllBlockedUntilTick);
        var min = now + rules.SpikeIntervalMinMinutes * 60L * Second + rules.MissSpikeDelaySeconds * (long)Second;
        var max = now + rules.SpikeIntervalMaxMinutes * 60L * Second + rules.MissSpikeDelaySeconds * (long)Second;
        Assert.InRange(item.NextSpikeTick, min, max);
        var refusal = Assert.Single(rejected);
        Assert.Equal(RefusalCode.SellAllCooldown, refusal.Code);
        Assert.InRange(refusal.WaitSeconds, 299, 300);
        Assert.Equal(1000, f.Wallet.MaterialCount(Ore));

        Run(f, 300 * Second);
        Force(f, Ore);
        host.Submit(new SellAll(1, Ore));
        host.Advance(1);
        Assert.Equal(0, f.Wallet.MaterialCount(Ore));
    }

    [Fact]
    public void LostFiftyFiftyGetsTheRecoveryBoost()
    {
        var f = NewFactory();
        var rules = Rules(f);
        Script(f, false, false);
        var lost = f.State.Exchange.Find(Plate)!;
        var small = f.State.Exchange.Find(Ore)!;
        var now = f.State.CurrentTick;

        Attempt(f, Plate, 10_000);
        Attempt(f, Ore, 5000);

        Assert.Equal(MarketPhase.Crash, lost.Phase);
        Assert.Equal(now + rules.LostBoostMinutes * 60L * Second, lost.BoostEndTick);
        Assert.Equal(MarketPhase.Crash, small.Phase);
        Assert.Equal(0, small.BoostEndTick);

        // Dips below B are 20% less likely while the boost lasts.
        Assert.Equal(rules.CrashDipChanceBp * (10_000 - rules.LostBoostBp) / 10_000, MarketMath.CrashDipChance(rules, true));
        Assert.Equal(rules.CrashDipChanceBp, MarketMath.CrashDipChance(rules, false));
        Assert.True(MarketMath.CrashDipChance(rules, true) < MarketMath.CrashDipChance(rules, false));
    }

    [Fact]
    public void MissUnderLostPiecesDoesNotSetTheBoost()
    {
        var f = NewFactory();
        Script(f, false);
        Attempt(f, Plate, Rules(f).LostPieces - 1);

        Assert.Equal(0, f.State.Exchange.Find(Plate)!.BoostEndTick);
    }

    [Fact]
    public void SecondSellAllInSameTickIsRefused()
    {
        var f = NewFactory();
        var host = new SimHost(f);
        var rejected = new List<Refusal>();
        host.CommandRejected += (_, r) => rejected.Add(r);
        Force(f, Plate);
        var rolls = Script(f, false, false);
        SetStock(f, Plate, 500);

        host.Submit(new SellAll(1, Plate));
        host.Advance(1.0 / 60);
        var blocked = f.State.Exchange.SellAllBlockedUntilTick;
        host.Submit(new SellAll(1, Plate));
        host.Submit(new SellAll(1, Plate));
        host.Advance(1.0 / 60);

        Assert.Equal([RefusalCode.SellFailed, RefusalCode.SellFailed], rejected.Select(r => r.Code));
        Assert.Single(rolls);
        Assert.Equal(blocked, f.State.Exchange.SellAllBlockedUntilTick);
    }

    [Fact]
    public void SellAllOfAnotherItemWaitsForTheCooldown()
    {
        var f = NewFactory();
        var host = new SimHost(f);
        var rejected = new List<Refusal>();
        host.CommandRejected += (_, r) => rejected.Add(r);
        Force(f, Plate);
        Force(f, Ore);
        Script(f, false);
        SetStock(f, Plate, 500);
        SetStock(f, Ore, 500);

        host.Submit(new SellAll(1, Plate));
        host.Submit(new SellAll(1, Ore));
        host.Advance(1);

        Assert.Equal(RefusalCode.SellAllCooldown, Assert.Single(rejected).Code);
        Assert.Equal(500, f.Wallet.MaterialCount(Ore));
    }

    [Fact]
    public void RefusedSellAllLeavesMarketUntouched()
    {
        var f = NewFactory();
        var item = Force(f, Plate);
        Force(f, Ore);
        var asked = Script(f, true);
        SetStock(f, Plate, 1000);
        SetStock(f, Ore, 0);
        f.Wallet.AddMaterial("not-in-market", 5);
        var market = f.State.Exchange;
        var money = f.Wallet.Money;
        var generator = market.Random.State;
        var price = item.PriceMilli;

        // No stock.
        Assert.False(f.TrySellAll(Ore));
        // Not in the market.
        Assert.False(f.TrySellAll("not-in-market"));
        // Waiting.
        market.SellAllBlockedUntilTick = f.State.CurrentTick + 600;
        Assert.False(f.TrySellAll(Plate));

        Assert.Empty(asked);
        Assert.Equal(generator, market.Random.State);
        Assert.Equal(1000, f.Wallet.MaterialCount(Plate));
        Assert.Equal(5, f.Wallet.MaterialCount("not-in-market"));
        Assert.Equal(money, f.Wallet.Money);
        Assert.Equal(price, item.PriceMilli);
        Assert.Equal(0, item.PityMisses);
        Assert.Equal(0, item.SaleDropMicro);
        Assert.Equal(MarketPhase.SpikeHold, item.Phase);
        Assert.Equal(f.State.CurrentTick + 600, market.SellAllBlockedUntilTick);
    }

    [Fact]
    public void BiggestJackpotFitsInMoney()
    {
        var f = NewFactory();
        Script(f, true);
        Force(f, Titanium, 3000);
        f.Wallet.AddMaterial(Titanium, 12_000);
        var before = f.Wallet.Money;

        Assert.True(f.TrySellAll(Titanium));

        Assert.True(f.Wallet.Money - before > 6_000_000);
        Assert.Equal(f.Wallet.Money - before, f.Session.SaleIncome);
    }

    [Fact]
    public void MoneyAndSaleIncomeSaturateInsteadOfWrapping()
    {
        var f = NewFactory();
        f.Wallet.AddMoney(int.MaxValue);
        f.Wallet.AddMoney(1000);
        f.Session.RecordSale(Plate, int.MaxValue);
        f.Session.RecordSale(Plate, 1000);

        Assert.Equal(int.MaxValue, f.Wallet.Money);
        Assert.Equal(int.MaxValue, f.Session.SaleIncome);
    }

    [Fact]
    public void HugeStockSaturatesWithoutThrowing()
    {
        var f = NewFactory();
        var host = new SimHost(f);
        var events = new List<SaleResolved>();
        host.EventRaised += e => { if (e is SaleResolved sale) { events.Add(sale); } };
        Script(f, true);
        Force(f, Titanium, 3000);
        SetStock(f, Titanium, 50_000_000);

        host.Submit(new SellAll(1, Titanium));
        host.Advance(1.0 / 60);

        Assert.True(Assert.Single(events).Jackpot);
        Assert.Equal(int.MaxValue, f.Wallet.Money);
        Assert.Equal(int.MaxValue, f.Session.SaleIncome);
        Assert.Equal(50_000_000, f.Session.SoldByItem[Titanium]);
    }
}
