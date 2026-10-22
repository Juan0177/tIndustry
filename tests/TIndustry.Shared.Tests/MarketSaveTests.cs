using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Save v14 carries the whole market; older saves start a fresh one.</summary>
public sealed class MarketSaveTests
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly FactoryContent Content = FactoryContent.Load(Path.Combine(DataDir, "content.json"));
    private const int Minute = 3600;

    private static Factory NewFactory() =>
        NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120,
            seed: 42, rules: new GameRules(InstantBuild: true));

    private static void Run(Factory f, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            f.Step();
        }
    }

    private static Factory RoundTrip(Factory f, Action<FactorySaveData>? edit = null)
    {
        var json = JsonSerializer.Serialize(f.Capture(), FactorySaveStore.JsonOptions);
        var data = JsonSerializer.Deserialize<FactorySaveData>(json, FactorySaveStore.JsonOptions)!;
        edit?.Invoke(data);
        return Factory.Restore(Content, data);
    }

    private static ulong Sum(Factory f) => Snapshot.Checksum(Snapshot.Write(f.State));

    /// <summary>Spike on item 0, crash on item 1, sell-all cooldown, a sale drop on item 2, auto-sell on item 3.</summary>
    private static Factory MidEverything()
    {
        var f = NewFactory();
        var rules = Content.MarketRules;
        Run(f, 2 * Minute);
        var items = f.State.Exchange.Items;
        var tick = f.State.CurrentTick;

        items[0].Phase = MarketPhase.SpikeHold;
        items[0].PhaseStartTick = tick;
        items[0].SpikeFromMilli = 1200;
        items[0].SpikeTopMilli = 2800;

        items[1].Phase = MarketPhase.Crash;
        items[1].PhaseStartTick = tick;
        items[1].CrashEndTick = tick + (long)rules.CrashMinutes * Minute;
        items[1].CrashDipEndTick = tick + 100;
        items[1].CrashDipMilli = 700;
        items[1].PityMisses = 2;
        items[1].BoostEndTick = tick + 5 * Minute;

        items[2].SaleDropMicro = 150_000;
        items[2].SaleRecoveryEndTick = tick + 30 * 60;
        items[2].WindowPieces[3] = 7;
        items[2].WindowSecond = tick / 60;

        items[3].AutoSell = true;
        f.State.Exchange.SellAllBlockedUntilTick = tick + 3 * Minute;
        return f;
    }

    [Fact]
    public void MarketSurvivesSaveAndLoad()
    {
        var f = MidEverything();
        Run(f, 3000);

        var g = RoundTrip(f);
        Assert.Equal(FactorySaveData.CurrentVersion, f.Capture().Version);
        var a = f.State.Exchange;
        var b = g.State.Exchange;
        Assert.Equal(a.Random.State, b.Random.State);
        Assert.Equal(a.Random.Increment, b.Random.Increment);
        Assert.Equal(a.SellAllBlockedUntilTick, b.SellAllBlockedUntilTick);
        Assert.Equal(a.Items.Count, b.Items.Count);
        for (var i = 0; i < a.Items.Count; i++)
        {
            var x = a.Items[i];
            var y = b.Items[i];
            Assert.Equal(x.ItemId, y.ItemId);
            Assert.Equal(x.TrendMilli, y.TrendMilli);
            Assert.Equal(x.PriceMilli, y.PriceMilli);
            Assert.Equal(x.Phase, y.Phase);
            Assert.Equal(x.PhaseStartTick, y.PhaseStartTick);
            Assert.Equal(x.SpikeTopMilli, y.SpikeTopMilli);
            Assert.Equal(x.SpikeFromMilli, y.SpikeFromMilli);
            Assert.Equal(x.NextSpikeTick, y.NextSpikeTick);
            Assert.Equal(x.StepFromMilli, y.StepFromMilli);
            Assert.Equal(x.StepTargetMilli, y.StepTargetMilli);
            Assert.Equal(x.CrashEndTick, y.CrashEndTick);
            Assert.Equal(x.CrashJitterMilli, y.CrashJitterMilli);
            Assert.Equal(x.CrashDipEndTick, y.CrashDipEndTick);
            Assert.Equal(x.CrashDipMilli, y.CrashDipMilli);
            Assert.Equal(x.BoostEndTick, y.BoostEndTick);
            Assert.Equal(x.SaleDropMicro, y.SaleDropMicro);
            Assert.Equal(x.SaleRecoveryEndTick, y.SaleRecoveryEndTick);
            Assert.Equal(x.WindowPieces, y.WindowPieces);
            Assert.Equal(x.WindowSecond, y.WindowSecond);
            Assert.Equal(x.PityMisses, y.PityMisses);
            Assert.Equal(x.AutoSell, y.AutoSell);
            Assert.Equal(x.HistoryMilli, y.HistoryMilli);
        }

        Assert.NotEmpty(a.Items[0].HistoryMilli);
        Assert.Equal(Sum(f), Sum(g));
    }

    [Fact]
    public void SaveMidSpikeCrashAndCooldownContinuesIdentically()
    {
        var f = MidEverything();
        Run(f, 60);
        var g = RoundTrip(f);
        Assert.Equal(Sum(f), Sum(g));

        Run(f, 10 * Minute);
        Run(g, 10 * Minute);
        Assert.Equal(Sum(f), Sum(g));
    }

    [Fact]
    public void OldSaveStartsAFreshMarket()
    {
        var f = MidEverything();
        Run(f, 3 * 60 * Minute);
        var tick = f.State.CurrentTick;

        var g = RoundTrip(f, d =>
        {
            d.Version = 13;
            d.Market = null;
        });

        Assert.Equal(tick, g.State.CurrentTick);
        var rules = Content.MarketRules;
        foreach (var item in g.State.Exchange.Items)
        {
            Assert.Equal(rules.NormalAverageMilli, item.PriceMilli);
            Assert.Equal(MarketPhase.Normal, item.Phase);
            Assert.Empty(item.HistoryMilli);
            Assert.InRange(item.NextSpikeTick - tick,
                (long)rules.SpikeIntervalMinMinutes * Minute, (long)rules.SpikeIntervalMaxMinutes * Minute);
        }

        Assert.Equal(0, g.State.Exchange.SellAllBlockedUntilTick);
    }

    [Fact]
    public void OldSaveAutoSellTurnsOnEveryItem()
    {
        var g = RoundTrip(NewFactory(), d =>
        {
            d.Version = 13;
            d.Market = null;
            d.AutoSellAtCore = true;
        });
        Assert.All(g.State.Exchange.Items, i => Assert.True(i.AutoSell));

        var h = RoundTrip(NewFactory(), d =>
        {
            d.Version = 13;
            d.Market = null;
            d.AutoSellAtCore = false;
        });
        Assert.All(h.State.Exchange.Items, i => Assert.False(i.AutoSell));
    }

    [Fact]
    public void CurrentSaveNoLongerWritesTheOldSwitchOrBonus()
    {
        var f = NewFactory();
        foreach (var item in f.State.Exchange.Items) { item.AutoSell = true; }
        var data = f.Capture();
        var json = JsonSerializer.Serialize(data, FactorySaveStore.JsonOptions);
        Assert.DoesNotContain("autoSellAtCore", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coreSaleBonusPercent", json, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(data.Market);
        Assert.All(data.Market!.Items, i => Assert.True(i.AutoSell));
    }

    [Fact]
    public void HostileWindowSecondDoesNotCrashTheStep()
    {
        var g = RoundTrip(NewFactory(), d =>
        {
            foreach (var i in d.Market!.Items)
            {
                i.WindowSecond = -5;
            }
        });
        foreach (var i in g.State.Exchange.Items)
        {
            i.AutoSell = false;
        }

        Assert.All(g.State.Exchange.Items, i => Assert.InRange(i.WindowSecond, 0, 1));
        Run(g, 2 * Minute);
    }

    [Fact]
    public void NullListsInAHandEditedSaveAreRepaired()
    {
        var g = RoundTrip(NewFactory(), d =>
        {
            d.Market!.Items[0].WindowPieces = null!;
            d.Market.Items[0].History = null!;
            d.Market.Items[1].History = null!;
        });
        Run(g, 60);
        var g2 = RoundTrip(NewFactory(), d => d.Market!.Items = null!);
        Run(g2, 60);
    }

    [Fact]
    public void UndefinedPhaseBecomesNormal()
    {
        var g = RoundTrip(NewFactory(), d => d.Market!.Items[0].Phase = (MarketPhase)99);
        Assert.Equal(MarketPhase.Normal, g.State.Exchange.Items[0].Phase);
        Run(g, 60);
    }

    [Fact]
    public void ItemMissingFromTheSaveStartsFreshFromNow()
    {
        var f = NewFactory();
        Run(f, 3 * 60 * Minute);
        var tick = f.State.CurrentTick;
        var g = RoundTrip(f, d => d.Market!.Items.RemoveAt(0));
        var rules = Content.MarketRules;
        var fresh = g.State.Exchange.Items[0];
        Assert.InRange(fresh.NextSpikeTick - tick,
            (long)rules.SpikeIntervalMinMinutes * Minute, (long)rules.SpikeIntervalMaxMinutes * Minute);
        Assert.Equal(rules.NormalAverageMilli, fresh.PriceMilli);
    }

    [Fact]
    public void NegativeAndHugePricesAreClamped()
    {
        var g = RoundTrip(NewFactory(), d =>
        {
            var i = d.Market!.Items[0];
            i.PriceMilli = i.TrendMilli = -9000;
            i.SpikeTopMilli = int.MaxValue;
            i.StepTargetMilli = i.CrashDipMilli = int.MinValue;
            i.History = [-1, int.MaxValue];
            i.CrashJitterMilli = int.MaxValue;
            i.WindowPieces = [int.MaxValue, int.MaxValue, int.MaxValue];
            i.WindowSecond = 0;
        });
        var x = g.State.Exchange.Items[0];
        var max = Content.MarketRules.SpikeMaxMilli;
        Assert.All(new[] { x.PriceMilli, x.TrendMilli, x.SpikeTopMilli, x.StepTargetMilli, x.CrashDipMilli },
            v => Assert.InRange(v, 0, max));
        Assert.All(x.HistoryMilli, v => Assert.InRange(v, 0, max));
        Assert.InRange(x.CrashJitterMilli, 0, Content.MarketRules.CrashJitterMilli);
        Assert.All(x.WindowPieces, v => Assert.InRange(v, 0, Content.MarketRules.CrashPieces));
        x.Phase = MarketPhase.Crash;
        x.CrashEndTick = g.State.CurrentTick + 10 * 60;
        Run(g, 5 * 60);
        Assert.All(g.State.Exchange.Items, it => Assert.True(it.PriceMilli >= 0));
    }
}
