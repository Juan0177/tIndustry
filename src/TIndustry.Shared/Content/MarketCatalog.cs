namespace TIndustry.Shared;

/// <summary>Thin market catalog — dynamic sell prices from stock (Raylib parity).</summary>
public sealed class MarketCatalog
{
    public const int DefaultSoftStock = 24;

    private readonly Dictionary<string, MarketItemDefinition> items;

    public MarketCatalog(IEnumerable<MarketItemDefinition> definitions)
    {
        items = definitions.ToDictionary(entry => entry.ItemId, StringComparer.Ordinal);
        Items = items.Values.OrderByDescending(entry => entry.SellPrice).ToList();
    }

    public IReadOnlyList<MarketItemDefinition> Items { get; }

    public static MarketCatalog FromContent(FactoryContent content) =>
        content.Market.Count > 0 ? new MarketCatalog(content.Market) : CreateDefault();

    public static MarketCatalog CreateDefault() => new(
    [
        new MarketItemDefinition("iron-ore", "Ferro grezzo", 8, 3, 14, 24),
        new MarketItemDefinition("iron-plate", "Lastra di ferro", 30, 12, 48, 20),
        new MarketItemDefinition("copper-ore", "Rame grezzo", 6, 2, 11, 24),
        new MarketItemDefinition("copper-wire", "Filo di rame", 16, 6, 28, 20),
        new MarketItemDefinition("coal", "Carbone", 4, 1, 8, 30),
        new MarketItemDefinition("lead-ore", "Piombo grezzo", 5, 2, 10, 24),
        new MarketItemDefinition("lead-plate", "Lastra di piombo", 18, 7, 32, 18),
        new MarketItemDefinition("titanium-ore", "Titanio grezzo", 12, 5, 22, 16),
        new MarketItemDefinition("titanium-plate", "Lastra di titanio", 45, 18, 72, 12),
        new MarketItemDefinition("graphite", "Grafite", 10, 4, 18, 20),
        new MarketItemDefinition("silicon", "Silicio", 28, 11, 48, 16)
    ]);

    public int GetSellPrice(string itemId) =>
        items.TryGetValue(itemId, out var item) ? item.SellPrice : 1;

    public string GetDisplayName(string itemId) =>
        items.TryGetValue(itemId, out var item) ? item.DisplayName : itemId;

    public bool TryGetItem(string itemId, out MarketItemDefinition item) =>
        items.TryGetValue(itemId, out item!);

    /// <summary>
    /// Supply curve: low stock → listino; high stock softens toward min.
    /// </summary>
    public int GetDynamicSellPrice(string itemId, int stockOnHand)
    {
        if (!items.TryGetValue(itemId, out var item))
        {
            return 1;
        }

        var soft = Math.Max(1, item.SoftStock ?? DefaultSoftStock);
        var min = item.MinSellPrice ?? Math.Max(1, (int)MathF.Round(item.SellPrice * 0.4f));
        var max = item.MaxSellPrice ?? Math.Max(item.SellPrice, (int)MathF.Round(item.SellPrice * 1.6f));
        if (min > max)
        {
            (min, max) = (max, min);
        }

        var stock = Math.Max(0, stockOnHand);
        var supply = Math.Max(0, stock - 1);
        if (supply == 0)
        {
            return Math.Clamp(item.SellPrice, min, max);
        }

        var pressure = supply / (float)(soft + supply);
        var price = item.SellPrice + (min - item.SellPrice) * pressure;
        return Math.Clamp((int)MathF.Round(price), min, max);
    }

    public string BestValueHint()
    {
        if (!items.TryGetValue("iron-ore", out var ore)
            || !items.TryGetValue("iron-plate", out var plate))
        {
            var best = Items.FirstOrDefault();
            return best is null
                ? "Nessun prezzo mercato."
                : $"Conviene vendere: {best.DisplayName} (${best.SellPrice}).";
        }

        var orePairValue = ore.SellPrice * 2;
        var margin = plate.SellPrice - orePairValue;
        return margin > 0
            ? $"Stock → costruisci, oppure vendi lastre (+${margin} vs 2 ore). Prezzi reagiscono allo stock."
            : "Accumula in magazzino; vendi dal Mercato quando ti servono $.";
    }
}
