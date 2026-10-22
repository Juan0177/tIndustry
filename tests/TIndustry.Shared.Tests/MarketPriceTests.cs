using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

public sealed class MarketPriceTests
{
    private static readonly string ContentPath = Path.Combine(AppContext.BaseDirectory, "data", "content.json");
    private const int Minute = 3600;

    private static GameState NewState(int seed = 42) =>
        NewGame.Sandbox(FactoryContent.Load(ContentPath), new GridPosition(60, 60), mapWidth: 120, mapHeight: 120,
            seed: seed).State;

    /// <summary>Steps <paramref name="ticks"/> ticks calling <paramref name="each"/> after every one.</summary>
    private static void Run(GameState state, int ticks, Action<int> each)
    {
        for (var i = 0; i < ticks; i++)
        {
            Simulation.Step(state);
            each(i);
        }
    }

    /// <summary>Start ticks (first Rise tick) of every spike of item <paramref name="index"/> in <paramref name="ticks"/>.</summary>
    private static List<(long Start, int Peak, int Rise, int Hold, int Fall)> Spikes(GameState state, int index, int ticks)
    {
        var item = state.Exchange.Items[index];
        var list = new List<(long, int, int, int, int)>();
        long start = -1;
        var peak = 0;
        var rise = 0;
        var hold = 0;
        var fall = 0;
        Run(state, ticks, _ =>
        {
            switch (item.Phase)
            {
                case MarketPhase.SpikeRise: if (item.PhaseStartTick != start) { start = item.PhaseStartTick; peak = 0; rise = hold = fall = 0; } rise++; break;
                case MarketPhase.SpikeHold: hold++; break;
                case MarketPhase.SpikeFall: fall++; break;
            }

            peak = Math.Max(peak, item.PriceMilli);
            if (item.Phase == MarketPhase.Normal && start >= 0 && (list.Count == 0 || list[^1].Item1 != start))
            {
                list.Add((start, peak, rise, hold, fall));
            }
        });
        return list;
    }

    [Fact]
    public void NormalPriceStaysInBand()
    {
        var state = NewState();
        var inSpike = false;
        Run(state, 10 * 60 * Minute, _ =>
        {
            foreach (var item in state.Exchange.Items)
            {
                if (item.Phase == MarketPhase.Normal)
                {
                    Assert.InRange(item.PriceMilli, 1000, 1600);
                }
                else
                {
                    inSpike = true;
                }
            }
        });
        Assert.True(inSpike);
    }

    [Fact]
    public void SpikesArriveEveryTenToTwentyMinutes()
    {
        var spikes = Spikes(NewState(), 0, 3 * 60 * Minute);
        Assert.True(spikes.Count >= 8);
        Assert.InRange(spikes[0].Start, 10 * Minute, 20 * Minute);
        for (var i = 1; i < spikes.Count; i++)
        {
            Assert.InRange(spikes[i].Start - spikes[i - 1].Start, 10 * Minute, 20 * Minute);
        }
    }

    [Fact]
    public void SpikeShapeIsTenTenThirty()
    {
        var spikes = Spikes(NewState(), 0, 2 * 60 * Minute);
        Assert.NotEmpty(spikes);
        foreach (var s in spikes)
        {
            Assert.Equal(600, s.Rise);
            Assert.Equal(600, s.Hold);
            Assert.Equal(1800, s.Fall);
            Assert.InRange(s.Peak, 2800, 3000);
        }
    }

    [Fact]
    public void EverySpikeHasASellingWindow()
    {
        var state = NewState();
        var item = state.Exchange.Items[3];
        long lastStart = -1;
        var high = new Dictionary<long, int>();
        Run(state, 2 * 60 * Minute, _ =>
        {
            if (item.Phase == MarketPhase.SpikeRise)
            {
                lastStart = item.PhaseStartTick;
                high.TryAdd(lastStart, 0);
            }

            if (item.PriceMilli >= 2800 && lastStart >= 0 && item.Phase != MarketPhase.Normal)
            {
                high[lastStart]++;
            }
        });
        Assert.NotEmpty(high);
        Assert.All(high.Values, n => Assert.True(n >= 1));
    }

    [Fact]
    public void ItemsMoveIndependently()
    {
        var state = NewState();
        var a = new List<long>();
        var b = new List<long>();
        Run(state, 2 * 60 * Minute, _ =>
        {
            if (state.Exchange.Items[0].Phase == MarketPhase.SpikeRise && !a.Contains(state.Exchange.Items[0].PhaseStartTick))
            {
                a.Add(state.Exchange.Items[0].PhaseStartTick);
            }

            if (state.Exchange.Items[1].Phase == MarketPhase.SpikeRise && !b.Contains(state.Exchange.Items[1].PhaseStartTick))
            {
                b.Add(state.Exchange.Items[1].PhaseStartTick);
            }
        });
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void HistoryKeepsOnePointPerSecond()
    {
        var state = NewState();
        Run(state, 200 * 60, _ => { });
        var item = state.Exchange.Items[0];
        Assert.Equal(state.Content.MarketRules.HistorySeconds, item.HistoryMilli.Count);
        Assert.InRange(item.HistoryMilli[^1], 1000, 3000);
    }

    [Fact]
    public void MarketIsDeterministic()
    {
        var a = NewState();
        var b = NewState();
        Run(a, 60 * Minute, _ => { });
        Run(b, 60 * Minute, _ => { });
        Assert.Equal(
            a.Exchange.Items.Select(i => (i.PriceMilli, i.Phase, i.NextSpikeTick)),
            b.Exchange.Items.Select(i => (i.PriceMilli, i.Phase, i.NextSpikeTick)));
    }

    [Fact]
    public void DifferentGameSeedsGiveDifferentMarkets()
    {
        Assert.NotEqual(NewState(1).Exchange.Items[0].NextSpikeTick, NewState(2).Exchange.Items[0].NextSpikeTick);
    }
}
