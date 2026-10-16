namespace TIndustry.Shared;

/// <summary>Items a sorter can filter on, in the order the client cycles them (shared by client and host).</summary>
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
