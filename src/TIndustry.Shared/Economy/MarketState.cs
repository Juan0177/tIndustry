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

    // End of the lost-50/50 price boost: set when a jackpot attempt is lost, read by the crash price.
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
    /// <summary>With <see cref="AutoSell"/>: sell only the part of the stock that would not fit under the maximum next second.</summary>
    public bool AutoSellWhenFull { get; set; }
    /// <summary>Fraction of a piece owed by the auto-sell rate, in sixtieths of a piece (one per tick; the name is historical), 0..59; back to 0 while auto-sell is off.</summary>
    public int AutoSellCarryMilli { get; set; }

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

    /// <summary>
    /// Dev only: the client playtest draws a known chart with it. No command, save or system calls it; outside a
    /// playtest it would only change what the chart shows (prices and sales never read the history).
    /// </summary>
    public void ForPlaytestSetHistory(IEnumerable<int> values) => RestoreHistory(values);

    internal void RestoreHistory(IEnumerable<int> values)
    {
        history.Clear();
        history.AddRange(values);
    }
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

    /// <summary>Restarts the market from the game seed: prices at the average, first spikes 10-20 minutes after <paramref name="fromTick"/>.</summary>
    public void Reseed(MarketRules? rules, int gameSeed, long fromTick = 0)
    {
        Random = new MarketRandom(unchecked((ulong)(uint)gameSeed ^ SeedMix));
        SellAllBlockedUntilTick = 0;
        if (rules is null)
        {
            return;
        }

        foreach (var item in items)
        {
            ResetItem(item, rules, fromTick);
        }
    }

    private void ResetItem(MarketItemState item, MarketRules rules, long fromTick)
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
        item.NextSpikeTick = fromTick + SpikeInterval(rules, Random);
        item.ClearHistory();
    }

    internal static long SpikeInterval(MarketRules rules, MarketRandom random) =>
        (long)random.NextRange(rules.SpikeIntervalMinMinutes, rules.SpikeIntervalMaxMinutes) * 60 * SimulationClock.TickRate;

    internal MarketSaveDto Capture() => new()
    {
        RandomState = Random.State,
        RandomInc = Random.Increment,
        SellAllBlockedUntilTick = SellAllBlockedUntilTick,
        Items = items.Select(i => new MarketItemSaveDto
        {
            ItemId = i.ItemId,
            TrendMilli = i.TrendMilli,
            PriceMilli = i.PriceMilli,
            Phase = i.Phase,
            PhaseStartTick = i.PhaseStartTick,
            SpikeTopMilli = i.SpikeTopMilli,
            SpikeFromMilli = i.SpikeFromMilli,
            NextSpikeTick = i.NextSpikeTick,
            StepFromMilli = i.StepFromMilli,
            StepTargetMilli = i.StepTargetMilli,
            CrashEndTick = i.CrashEndTick,
            CrashJitterMilli = i.CrashJitterMilli,
            CrashDipEndTick = i.CrashDipEndTick,
            CrashDipMilli = i.CrashDipMilli,
            BoostEndTick = i.BoostEndTick,
            SaleDropMicro = i.SaleDropMicro,
            SaleRecoveryEndTick = i.SaleRecoveryEndTick,
            WindowPieces = [.. i.WindowPieces],
            WindowSecond = i.WindowSecond,
            PityMisses = i.PityMisses,
            AutoSell = i.AutoSell,
            AutoSellWhenFull = i.AutoSellWhenFull,
            AutoSellCarryMilli = i.AutoSellCarryMilli,
            History = [.. i.HistoryMilli]
        }).ToList()
    };

    /// <summary>Loads a saved market; items the save does not know keep their fresh state, unknown saved items are dropped.</summary>
    internal void Restore(MarketSaveDto dto, MarketRules? rules, long currentTick)
    {
        Random = MarketRandom.FromState(dto.RandomState, dto.RandomInc);
        SellAllBlockedUntilTick = Math.Max(0, dto.SellAllBlockedUntilTick);
        var window = Math.Max(1, rules?.CrashWindowSeconds ?? 1);
        var maxPrice = rules?.SpikeMaxMilli ?? int.MaxValue;
        int Price(int v) => Math.Clamp(v, 0, maxPrice);
        var seen = new HashSet<MarketItemState>();
        foreach (var d in dto.Items ?? [])
        {
            if (Find(d.ItemId) is not { } i)
            {
                continue;
            }

            seen.Add(i);
            i.TrendMilli = Price(d.TrendMilli);
            i.PriceMilli = Price(d.PriceMilli);
            i.Phase = Enum.IsDefined(d.Phase) ? d.Phase : MarketPhase.Normal;
            i.PhaseStartTick = d.PhaseStartTick;
            i.SpikeTopMilli = Price(d.SpikeTopMilli);
            i.SpikeFromMilli = Price(d.SpikeFromMilli);
            i.NextSpikeTick = d.NextSpikeTick;
            i.StepFromMilli = Price(d.StepFromMilli);
            i.StepTargetMilli = Price(d.StepTargetMilli);
            i.CrashEndTick = d.CrashEndTick;
            i.CrashJitterMilli = Math.Clamp(d.CrashJitterMilli, 0, rules?.CrashJitterMilli ?? 0);
            i.CrashDipEndTick = d.CrashDipEndTick;
            i.CrashDipMilli = Price(d.CrashDipMilli);
            i.BoostEndTick = d.BoostEndTick;
            i.SaleDropMicro = Math.Max(0, d.SaleDropMicro);
            i.SaleRecoveryEndTick = d.SaleRecoveryEndTick;
            // The ring buffer keeps the length the rules ask for, whatever the file holds.
            i.WindowPieces = new int[window];
            var windowPieces = d.WindowPieces ?? [];
            Array.Copy(windowPieces, i.WindowPieces, Math.Min(window, windowPieces.Length));
            // A second outside [0, now] would index the ring buffer out of range: restart the window.
            var nowSecond = currentTick / SimulationClock.TickRate;
            if (d.WindowSecond < 0 || d.WindowSecond > nowSecond)
            {
                Array.Clear(i.WindowPieces);
                i.WindowSecond = nowSecond;
            }
            else
            {
                i.WindowSecond = d.WindowSecond;
                for (var k = 0; k < i.WindowPieces.Length; k++)
                {
                    i.WindowPieces[k] = Math.Clamp(i.WindowPieces[k], 0, rules?.CrashPieces ?? 0);
                }
            }

            i.PityMisses = Math.Max(0, d.PityMisses);
            i.AutoSell = d.AutoSell;
            i.AutoSellWhenFull = d.AutoSellWhenFull;
            i.AutoSellCarryMilli = Math.Clamp(d.AutoSellCarryMilli, 0, SimulationClock.TickRate - 1);
            i.RestoreHistory((d.History ?? []).Select(Price).TakeLast(Math.Max(0, rules?.HistorySeconds ?? 0)));
        }

        // Items the save does not list start fresh from now (after the stream is restored, in content order).
        if (rules is not null)
        {
            foreach (var item in items)
            {
                if (!seen.Contains(item))
                {
                    ResetItem(item, rules, currentTick);
                }
            }
        }
    }
}
