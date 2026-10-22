namespace TIndustry.Shared;

internal static class MarketMath
{
    /// <summary>Price of B itself, in thousandths of B.</summary>
    public const int OneB = 1000;

    /// <summary>The crash price sits between B and B plus this many thousandths.</summary>
    public const int CrashJitterMaxMilli = 40;

    /// <summary>Money for one piece: <c>round(B × price)</c>, price in thousandths of B.</summary>
    public static int UnitIncome(int baseValue, int priceMilli) => (int)(((long)baseValue * priceMilli + 500) / 1000);
}

internal readonly record struct SaleOutcome(int Pieces, int PriceMilli, int Income, bool Crash);

/// <summary>The one place where selling from the stock happens (<see cref="Sell"/> and <see cref="SellAll"/>).</summary>
internal static class MarketSales
{
    /// <summary>
    /// Sells <paramref name="pieces"/> of <paramref name="item"/> from the wallet at the price of the moment, then applies
    /// the per-piece price drop and the crash window. Null when the stock is short. <paramref name="sellAll"/> is
    /// for the jackpot attempt (later task).
    /// </summary>
    public static SaleOutcome? Sell(GameState s, MarketItemState item, int pieces, bool sellAll, int playerId = 1)
    {
        var rules = s.Content.MarketRules;
        if (rules is null || pieces <= 0 || !s.Wallet.TryRemoveMaterial(item.ItemId, pieces))
        {
            return null;
        }

        _ = sellAll;
        var price = item.PriceMilli;
        var unit = MarketMath.UnitIncome(item.BaseValue, price);
        var income = pieces * unit;
        s.Wallet.AddMoney(income);
        for (var i = 0; i < pieces; i++)
        {
            s.Session.RecordSale(item.ItemId, unit);
        }

        var crash = false;
        if (item.Phase != MarketPhase.Crash)
        {
            var rate = SimulationClock.TickRate;
            var room = Math.Max(0, price - rules.NormalMinMilli) * 1000L;
            var added = (int)Math.Min((long)pieces * rules.SaleImpactMicro, room);
            item.SaleDropMicro += added;
            item.PriceMilli = Math.Max(rules.NormalMinMilli, price - ((item.SaleDropMicro / 1000) - ((item.SaleDropMicro - added) / 1000)));
            item.SaleRecoveryEndTick = s.CurrentTick + (long)rules.SaleRecoverySeconds * rate;

            crash = AddToWindow(s.CurrentTick, item, pieces, rules, rate) >= rules.CrashPieces;
            if (crash)
            {
                StartCrash(s, item, rules, rate);
            }
        }

        s.Events.Add(new SaleResolved(playerId, item.ItemId, pieces, price, income, crash, false, false, 0));
        return new SaleOutcome(pieces, price, income, crash);
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
        item.CrashJitterMilli = s.Exchange.Random.NextRange(0, MarketMath.CrashJitterMaxMilli);
        item.PriceMilli = MarketMath.OneB + item.CrashJitterMilli;
    }
}
