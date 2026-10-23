namespace TIndustry.Shared;

internal static class MarketMath
{
    /// <summary>Price of B itself, in thousandths of B.</summary>
    public const int OneB = 1000;

    /// <summary>Money for one piece: <c>round(B × price)</c>, price in thousandths of B.</summary>
    public static int UnitIncome(int baseValue, int priceMilli) => (int)(((long)baseValue * priceMilli + 500) / 1000);

    /// <summary>Chance (basis points) of a crash dip below B; lower by <c>lostBoost</c> after a lost 50/50.</summary>
    internal static int CrashDipChance(MarketRules r, bool boosted) =>
        boosted ? r.CrashDipChanceBp * (10_000 - r.LostBoostBp) / 10_000 : r.CrashDipChanceBp;

    /// <summary>
    /// Jackpot chance in basis points for a "sell all" of <paramref name="pieces"/>: 0 under the minimum, base plus a
    /// step per block of pieces up to the cap; from the pity size on each missed attempt adds a step, and the
    /// guaranteed streak makes it certain.
    /// </summary>
    internal static int JackpotChance(MarketRules r, int pieces, int pityMisses)
    {
        if (pieces < r.JackpotMinPieces)
        {
            return 0;
        }

        var chance = Math.Min(r.JackpotCapBp, r.JackpotBaseBp + (pieces - r.JackpotMinPieces) / r.JackpotStepPieces * r.JackpotStepBp);
        if (pieces >= r.PityPieces)
        {
            chance = pityMisses >= r.PityGuaranteeMisses ? 10_000 : chance + pityMisses * r.PityStepBp;
        }

        return Math.Min(10_000, chance);
    }
}

internal readonly record struct SaleOutcome(int Pieces, int PriceMilli, int Income, bool Crash);

/// <summary>The one place where selling from the stock happens (<see cref="Sell"/> and <see cref="SellAll"/>).</summary>
internal static class MarketSales
{
    /// <summary>
    /// One tick of auto-sell, items in market order: an item with the switch on owes <c>rate / 60</c> pieces per tick
    /// (rate = the Core tier's pieces per second; the remainder is carried in sixtieths of a piece, so 10 per second is
    /// exactly 100 pieces in 10 seconds, with no rounding loss). With
    /// "only when full" it sells just what would not fit under the maximum next second. Each piece pays
    /// <c>min(B, price)</c> and is recorded as a sale; the price drop, the crash window and the generator are left alone.
    /// An item with the switch off drops its carry.
    /// </summary>
    public static void AutoSellTick(GameState s)
    {
        var rate = s.Content.CoreTiers[s.CoreTier].AutoSellPerSecond;
        foreach (var item in s.Exchange.Items)
        {
            if (!item.AutoSell)
            {
                item.AutoSellCarry = 0;
                continue;
            }

            var carry = item.AutoSellCarry + rate;
            var due = carry / SimulationClock.TickRate;
            item.AutoSellCarry = carry % SimulationClock.TickRate;
            var stock = s.Wallet.MaterialCount(item.ItemId);
            var n = item.AutoSellWhenFull
                ? Math.Min(due, Math.Max(0, stock - (s.Storage.Capacity(item.ItemId) - rate)))
                : Math.Min(due, stock);
            if (n <= 0 || !s.Wallet.TryRemoveMaterial(item.ItemId, n))
            {
                continue;
            }

            var income = (long)n * Math.Min(item.BaseValue, MarketMath.UnitIncome(item.BaseValue, item.PriceMilli));
            s.Wallet.AddMoney((int)Math.Min(income, int.MaxValue));
            s.Session.RecordSales(item.ItemId, (int)Math.Min(income, int.MaxValue), n);
        }
    }

    /// <summary>
    /// Sells <paramref name="pieces"/> of <paramref name="item"/> from the wallet at the price of the moment, then applies
    /// the per-piece price drop and the crash window. Null when the stock is short. <paramref name="sellAll"/> is
    /// for the jackpot attempt (see <see cref="MarketMath.JackpotChance"/>).
    /// </summary>
    public static SaleOutcome? Sell(GameState s, MarketItemState item, int pieces, bool sellAll, int playerId)
    {
        var rules = s.Content.MarketRules;
        if (rules is null || pieces <= 0 || !s.Wallet.TryRemoveMaterial(item.ItemId, pieces))
        {
            return null;
        }

        var price = item.PriceMilli;
        var unit = MarketMath.UnitIncome(item.BaseValue, price);
        var attempt = sellAll && s.HasMarketScreen && pieces >= rules.JackpotMinPieces && price >= rules.SpikeThresholdMilli;
        var probability = 0;
        var won = false;
        if (attempt)
        {
            probability = MarketMath.JackpotChance(rules, pieces, item.PityMisses);
            won = s.JackpotRollForTests?.Invoke(probability) ?? s.Exchange.Random.NextInt(10_000) < probability;
            if (won)
            {
                // B + bonus (floored) + the rise over B.
                unit = item.BaseValue + (int)((long)item.BaseValue * rules.JackpotBonusPermille / 1000) + (unit - item.BaseValue);
            }
        }

        // Saturates instead of throwing: the stock of an item is uncapped.
        var income = (int)Math.Min((long)pieces * unit, int.MaxValue);
        s.Wallet.AddMoney(income);
        s.Session.RecordSales(item.ItemId, income, pieces);

        var crash = won;
        if (won)
        {
            StartCrash(s, item, rules, SimulationClock.TickRate);
            item.PityMisses = 0;
        }
        else if (item.Phase != MarketPhase.Crash)
        {
            var rate = SimulationClock.TickRate;
            var room = Math.Max(0, price - MarketMath.OneB) * 1000L;
            var added = (int)Math.Min((long)pieces * rules.SaleImpactMicro, room);
            item.SaleDropMicro += added;
            item.PriceMilli = Math.Max(MarketMath.OneB, price - ((item.SaleDropMicro / 1000) - ((item.SaleDropMicro - added) / 1000)));
            item.SaleRecoveryEndTick = s.CurrentTick + (long)rules.SaleRecoverySeconds * rate;

            crash = AddToWindow(s.CurrentTick, item, pieces, rules, rate) >= rules.CrashPieces;
            if (crash)
            {
                StartCrash(s, item, rules, rate);
            }
        }

        if (attempt && !won)
        {
            AfterMiss(s, item, pieces, crash, rules);
        }

        s.Events.Add(new SaleResolved(playerId, item.ItemId, pieces, price, income, crash, attempt, won, probability));
        return new SaleOutcome(pieces, price, income, crash);
    }

    private static void AfterMiss(GameState s, MarketItemState item, int pieces, bool crash, MarketRules rules)
    {
        var rate = SimulationClock.TickRate;
        if (pieces >= rules.PityPieces)
        {
            item.PityMisses++;
        }

        if (crash)
        {
            if (pieces >= rules.LostPieces)
            {
                item.BoostEndTick = s.CurrentTick + (long)rules.LostBoostMinutes * 60 * rate;
            }

            return;
        }

        s.Exchange.SellAllBlockedUntilTick = s.CurrentTick + (long)rules.MissCooldownSeconds * rate;
        item.NextSpikeTick = s.CurrentTick + MarketState.SpikeInterval(rules, s.Exchange.Random) + (long)rules.MissSpikeDelaySeconds * rate;
    }

    /// <summary>Adds to the current second of the ring buffer (clearing the seconds skipped) and returns the window total.</summary>
    private static int AddToWindow(long tick, MarketItemState item, int pieces, MarketRules rules, int rate)
    {
        var buckets = item.WindowPieces;
        var second = tick / rate;
        var skipped = (int)Math.Min(second - item.WindowSecond, buckets.Length);
        for (var k = 1; k <= skipped; k++)
        {
            buckets[(int)((item.WindowSecond + k) % buckets.Length)] = 0;
        }

        item.WindowSecond = second;
        buckets[(int)(second % buckets.Length)] += pieces;
        return buckets.Sum();
    }

    private static void StartCrash(GameState s, MarketItemState item, MarketRules rules, int rate)
    {
        item.Phase = MarketPhase.Crash;
        item.PhaseStartTick = s.CurrentTick;
        item.CrashEndTick = s.CurrentTick + (long)rules.CrashMinutes * 60 * rate;
        item.CrashDipEndTick = 0;
        item.SaleDropMicro = 0;
        Array.Clear(item.WindowPieces);
        item.CrashJitterMilli = s.Exchange.Random.NextRange(0, rules.CrashJitterMilli);
        item.PriceMilli = MarketMath.OneB + item.CrashJitterMilli;
    }
}
