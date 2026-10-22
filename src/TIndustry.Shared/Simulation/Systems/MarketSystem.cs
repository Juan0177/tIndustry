namespace TIndustry.Shared;

/// <summary>Moves every item's price: normal trend (a walk pulled to the average) plus periodic spikes.</summary>
public sealed class MarketSystem : ISimulationSystem
{
    public string Name => "Market";

    public void Run(GameState state, SystemContext ctx)
    {
        var rules = state.Content.MarketRules;
        if (rules is null)
        {
            return;
        }

        var market = state.Exchange;
        var tick = state.CurrentTick;
        var rate = SimulationClock.TickRate;
        var stepTicks = rules.NormalStepSeconds * rate;
        var inStep = (int)(tick % stepTicks);
        var items = market.Items;
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (inStep == 0)
            {
                // New target: pulled toward the average, plus noise, kept in the normal band.
                var prev = item.StepTargetMilli;
                var target = prev + (rules.NormalAverageMilli - prev) * rules.NormalPullPermille / 1000
                    + market.Random.NextRange(-rules.NormalNoiseMilli, rules.NormalNoiseMilli);
                item.StepFromMilli = item.TrendMilli;
                item.StepTargetMilli = Math.Clamp(target, rules.NormalMinMilli, rules.NormalMaxMilli);
            }

            item.TrendMilli = item.StepFromMilli + (item.StepTargetMilli - item.StepFromMilli) * inStep / stepTicks;

            if (item.SaleDropMicro > 0)
            {
                // Linear recovery: the remaining drop shrinks by 1/left each tick and is gone at the end tick.
                var left = item.SaleRecoveryEndTick - tick;
                item.SaleDropMicro = left <= 1 ? 0 : item.SaleDropMicro - item.SaleDropMicro / (int)left;
            }

            if (item.Phase == MarketPhase.Crash)
            {
                if (tick >= item.CrashEndTick)
                {
                    item.Phase = MarketPhase.Normal;
                    item.PhaseStartTick = tick;
                    item.SaleDropMicro = 0;
                }
                else
                {
                    // The spike countdown stands still during the crash: it resumes with the same time left.
                    item.NextSpikeTick++;
                }
            }

            if (item.Phase == MarketPhase.Normal && tick >= item.NextSpikeTick)
            {
                item.Phase = MarketPhase.SpikeRise;
                item.PhaseStartTick = tick;
                item.SpikeFromMilli = item.TrendMilli;
                item.SpikeTopMilli = market.Random.NextRange(rules.SpikeThresholdMilli, rules.SpikeMaxMilli);
                item.NextSpikeTick = tick + MarketState.SpikeInterval(rules, market.Random);
            }

            var phasePrice = item.TrendMilli;
            if (item.Phase is MarketPhase.SpikeRise or MarketPhase.SpikeHold or MarketPhase.SpikeFall)
            {
                phasePrice = SpikePrice(item, rules, tick, rate);
            }

            item.PriceMilli = item.Phase == MarketPhase.Crash
                ? CrashPrice(item, market, rules, tick, rate)
                : Math.Max(rules.NormalMinMilli, phasePrice - item.SaleDropMilli);

            if (tick % rate == 0)
            {
                item.PushHistory(item.PriceMilli, rules.HistorySeconds);
            }
        }
    }

    /// <summary>
    /// Crash level: about B with a small jitter redrawn every second; in the first <c>crashDipMinutes</c> every
    /// <c>crashDipEverySeconds</c> there is a chance to drop to the dip range for that long.
    /// </summary>
    private static int CrashPrice(MarketItemState item, MarketState market, MarketRules rules, long tick, int rate)
    {
        if (tick % rate == 0)
        {
            item.CrashJitterMilli = market.Random.NextRange(0, rules.CrashJitterMilli);
        }

        var since = tick - (item.CrashEndTick - (long)rules.CrashMinutes * 60 * rate);
        var dipEvery = (long)rules.CrashDipEverySeconds * rate;
        if (since < (long)rules.CrashDipMinutes * 60 * rate && since % dipEvery == 0
            // The sale drop is always 0 during a crash, so of the lost 50/50 only the rarer dips below B can act
            && market.Random.NextInt(10_000) < MarketMath.CrashDipChance(rules, tick < item.BoostEndTick))
        {
            item.CrashDipMilli = market.Random.NextRange(rules.CrashDipMinMilli, rules.CrashDipMaxMilli);
            item.CrashDipEndTick = tick + dipEvery;
        }

        return tick < item.CrashDipEndTick ? item.CrashDipMilli : MarketMath.OneB + item.CrashJitterMilli;
    }

    /// <summary>Advances the spike phase and returns its curve value (milli B).</summary>
    private static int SpikePrice(MarketItemState item, MarketRules rules, long tick, int rate)
    {
        var rise = (long)rules.SpikeRiseSeconds * rate;
        var hold = (long)rules.SpikeHoldSeconds * rate;
        var fall = (long)rules.SpikeFallSeconds * rate;
        var t = tick - item.PhaseStartTick;
        if (item.Phase == MarketPhase.SpikeRise && t >= rise)
        {
            item.Phase = MarketPhase.SpikeHold;
            item.PhaseStartTick = tick;
            t = 0;
        }

        if (item.Phase == MarketPhase.SpikeHold && t >= hold)
        {
            item.Phase = MarketPhase.SpikeFall;
            item.PhaseStartTick = tick;
            t = 0;
        }

        if (item.Phase == MarketPhase.SpikeFall && t >= fall)
        {
            item.Phase = MarketPhase.Normal;
            return item.TrendMilli;
        }

        return item.Phase switch
        {
            MarketPhase.SpikeRise => item.SpikeFromMilli + (int)((item.SpikeTopMilli - item.SpikeFromMilli) * t / rise),
            MarketPhase.SpikeHold => item.SpikeTopMilli,
            _ => item.SpikeTopMilli + (int)((item.TrendMilli - item.SpikeTopMilli) * t / fall)
        };
    }
}
