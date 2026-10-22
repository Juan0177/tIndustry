namespace TIndustry.Shared;

/// <summary>Read-only look at one market item: prices in thousandths of the base value B, income in money per piece.</summary>
public sealed record MarketItemView(
    string ItemId, int BaseValue, int PriceMilli, int UnitIncome, MarketPhase Phase, int CrashSecondsLeft,
    bool AutoSell, IReadOnlyList<int> HistoryMilli);
