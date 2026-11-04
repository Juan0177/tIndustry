namespace TIndustry.Shared;

/// <summary>
/// What the three Finance modules show for one market item (spec 2a «Ramo Finanza della Ricerca»), read at the
/// current tick without touching the game. A module's fields are null until its node is researched, and all of them
/// are null without an active <see cref="MarketScreen"/>.
/// <list type="bullet">
/// <item>Forecast (<see cref="ForecastNodeId"/>): seconds (rounded up) to the next spike, or to the end of a crash
/// (then no spike countdown), the global "sell all" wait after a missed attempt, and <see cref="SellNow"/> in the
/// spike. The client turns the seconds into "about N min".</item>
/// <item>Percent (<see cref="PercentNodeId"/>): the price of the moment as whole percent over B, floored.</item>
/// <item>Odds (<see cref="OddsNodeId"/>): the jackpot chance of a "sell all" of every piece of the item in the Core
/// now, pity included, from <see cref="MarketSales.SellAllAttempt"/> (the very check the sale uses).</item>
/// </list>
/// </summary>
public sealed record MarketInsight(
    int? SecondsToSpike,
    int? SecondsToCrashEnd,
    int? SecondsSellAllBlocked,
    bool SellNow,
    int? PercentOverB,
    int? JackpotChancePercent)
{
    /// <summary>Kind of the research nodes that unlock a module: researchable, never placed.</summary>
    public const string ModuleKind = "module";

    public const string ForecastNodeId = "finance-forecast";
    public const string PercentNodeId = "finance-percent";
    public const string OddsNodeId = "finance-odds";

    /// <summary>Nothing to show: no active screen, no module researched or an unknown item.</summary>
    public static readonly MarketInsight None = new(null, null, null, false, null, null);

    /// <summary>The insight of <paramref name="itemId"/> at the current tick; reads only (no random draw, no state change).</summary>
    internal static MarketInsight Of(GameState s, string itemId)
    {
        var rules = s.Content.MarketRules;
        if (rules is null || s.MarketScreen is not { Active: true } || s.Exchange.Find(itemId) is not { } item)
        {
            return None;
        }

        var tick = s.CurrentTick;
        int? toSpike = null, toCrashEnd = null, blocked = null;
        var sellNow = false;
        if (s.Research.IsUnlocked(ForecastNodeId))
        {
            if (item.Phase == MarketPhase.Crash)
            {
                toCrashEnd = SecondsUp(item.CrashEndTick - tick);
            }
            else
            {
                toSpike = SecondsUp(item.NextSpikeTick - tick);
            }

            var wait = s.Exchange.SellAllBlockedUntilTick - tick;
            blocked = wait > 0 ? SecondsUp(wait) : null;
            sellNow = MarketSales.InSpike(rules, item.PriceMilli);
        }

        int? percent = s.Research.IsUnlocked(PercentNodeId)
            ? FloorDiv(item.PriceMilli - MarketMath.OneB, 10)
            : null;

        int? odds = null;
        if (s.Research.IsUnlocked(OddsNodeId))
        {
            var (attempt, chanceBp) = MarketSales.SellAllAttempt(s, item, s.Wallet.MaterialCount(item.ItemId));
            odds = attempt ? chanceBp / 100 : 0;
        }

        return new MarketInsight(toSpike, toCrashEnd, blocked, sellNow, percent, odds);
    }

    private static int SecondsUp(long ticks) =>
        (int)Math.Min(int.MaxValue, (Math.Max(0, ticks) + SimulationClock.TickRate - 1) / SimulationClock.TickRate);

    private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
}
