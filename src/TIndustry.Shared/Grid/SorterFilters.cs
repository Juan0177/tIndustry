namespace TIndustry.Shared;

/// <summary>
/// The items the client's C key cycles a sorter's filter through, in order. The simulation takes any item of the game
/// as a filter (<see cref="FactoryContent.IsKnownItem"/>, <see cref="SetBlockFilter"/>).
/// </summary>
public static class SorterFilters
{
    private static readonly string[] ids =
    [
        "iron-ore", "copper-ore", "iron-plate", "copper-wire", "coal"
    ];

    public static IReadOnlyList<string> Ids => ids;

    public static bool IsValid(string? itemId) =>
        itemId is not null && Array.IndexOf(ids, itemId) >= 0;

    /// <summary>Next filter after <paramref name="current"/> (unknown/null → the first one).</summary>
    public static string Cycle(string? current)
    {
        var idx = current is null ? -1 : Array.IndexOf(ids, current);
        return ids[(idx + 1 + ids.Length) % ids.Length];
    }
}
