namespace TIndustry.Shared;

public enum MarketPhase
{
    Normal,
    SpikeRise,
    SpikeHold,
    SpikeFall,
    Crash
}

/// <summary>Price state of one market item. Prices are integers in thousandths of the base value B.</summary>
public sealed class MarketItemState
{
    private readonly List<int> history = [];

    public MarketItemState(string itemId, int baseValue)
    {
        ItemId = itemId;
        BaseValue = baseValue;
    }

    public string ItemId { get; }
    public int BaseValue { get; }

    /// <summary>Normal-trend price (milli B), interpolated between the steps.</summary>
    public int TrendMilli { get; set; }
    /// <summary>Price right now (milli B).</summary>
    public int PriceMilli { get; set; }
    public MarketPhase Phase { get; set; }
    public long PhaseStartTick { get; set; }
    public int SpikeTopMilli { get; set; }
    /// <summary>Trend value the current spike started from (the rise starts here).</summary>
    public int SpikeFromMilli { get; set; }
    public long NextSpikeTick { get; set; }
    /// <summary>Trend interpolation: value at the start of the current step and its target.</summary>
    public int StepFromMilli { get; set; }
    public int StepTargetMilli { get; set; }

    /// <summary>Tick the crash ends (valid while <see cref="Phase"/> is <see cref="MarketPhase.Crash"/>).</summary>
    public long CrashEndTick { get; set; }
    /// <summary>Small random offset above B shown during a crash, redrawn every second (milli B).</summary>
    public int CrashJitterMilli { get; set; }
    /// <summary>A crash dip below B lasts until this tick and sits at <see cref="CrashDipMilli"/>.</summary>
    public long CrashDipEndTick { get; set; }
    public int CrashDipMilli { get; set; }

    // Driven by later tasks (jackpot, auto-sell); inert for now.
    public long BoostEndTick { get; set; }

    /// <summary>Price drop from sales, in millionths of B (a piece drops less than one milli).</summary>
    public int SaleDropMicro { get; set; }
    /// <summary>Tick at which the sale drop has fully recovered (it shrinks linearly to zero).</summary>
    public long SaleRecoveryEndTick { get; set; }
    public int SaleDropMilli => SaleDropMicro / 1000;

    /// <summary>Pieces sold per second of the crash window (ring buffer indexed by second); <see cref="WindowSecond"/> is the last second written.</summary>
    public int[] WindowPieces { get; set; } = [];
    public long WindowSecond { get; set; }
    public int PityMisses { get; set; }
    public bool AutoSell { get; set; }

    /// <summary>One point per second, oldest first, at most <c>historySeconds</c> points.</summary>
    public IReadOnlyList<int> HistoryMilli => history;

    internal void PushHistory(int value, int max)
    {
        if (max <= 0)
        {
            return;
        }

        if (history.Count >= max)
        {
            history.RemoveAt(0);
        }

        history.Add(value);
    }

    internal void ClearHistory() => history.Clear();
}

/// <summary>Market of a game: one <see cref="MarketItemState"/> per <c>content.market</c> item, in content order.</summary>
public sealed class MarketState
{
    private const ulong SeedMix = 0x4D41524B45545F31UL; // "MARKET_1"

    private readonly MarketItemState[] items;

    public MarketState(FactoryContent content, int gameSeed)
    {
        // Content built by hand without Load has no rules: the market is then empty and inert.
        items = content.MarketRules is null
            ? []
            : content.Market.Select(m => new MarketItemState(m.ItemId, m.BaseValue)).ToArray();
        Random = new MarketRandom(0);
        Reseed(content.MarketRules, gameSeed);
    }

    public IReadOnlyList<MarketItemState> Items => items;
    public long SellAllBlockedUntilTick { get; set; }
    public MarketRandom Random { get; private set; }

    public MarketItemState? Find(string itemId)
    {
        for (var i = 0; i < items.Length; i++)
        {
            if (string.Equals(items[i].ItemId, itemId, StringComparison.Ordinal))
            {
                return items[i];
            }
        }

        return null;
    }

    /// <summary>Restarts the market from the game seed: prices at the average, first spikes scheduled.</summary>
    public void Reseed(MarketRules? rules, int gameSeed)
    {
        Random = new MarketRandom(unchecked((ulong)(uint)gameSeed ^ SeedMix));
        SellAllBlockedUntilTick = 0;
        if (rules is null)
        {
            return;
        }

        foreach (var item in items)
        {
            item.Phase = MarketPhase.Normal;
            item.PhaseStartTick = 0;
            item.TrendMilli = item.StepFromMilli = item.StepTargetMilli = item.PriceMilli = rules.NormalAverageMilli;
            item.SaleDropMicro = 0;
            item.SaleRecoveryEndTick = 0;
            item.CrashEndTick = item.CrashDipEndTick = item.BoostEndTick = 0;
            item.CrashJitterMilli = item.CrashDipMilli = 0;
            item.PityMisses = 0;
            item.WindowPieces = new int[Math.Max(1, rules.CrashWindowSeconds)];
            item.WindowSecond = 0;
            item.NextSpikeTick = SpikeInterval(rules, Random);
            item.ClearHistory();
        }
    }

    internal static long SpikeInterval(MarketRules rules, MarketRandom random) =>
        (long)random.NextRange(rules.SpikeIntervalMinMinutes, rules.SpikeIntervalMaxMinutes) * 60 * SimulationClock.TickRate;
}
