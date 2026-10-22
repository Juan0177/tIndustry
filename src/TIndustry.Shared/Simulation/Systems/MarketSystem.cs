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

            if (item.Phase != MarketPhase.Crash)
            {
                item.PriceMilli = Math.Max(rules.NormalMinMilli, phasePrice - item.SaleDropMilli);
            }

            if (tick % rate == 0)
            {
                item.PushHistory(item.PriceMilli, rules.HistorySeconds);
            }
        }
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
