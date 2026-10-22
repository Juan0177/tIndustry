namespace TIndustry.Shared;

/// <summary>Market catalog: which items are traded, their names and base values B (prices live in <see cref="MarketState"/>).</summary>
public sealed class MarketCatalog
{
    private readonly Dictionary<string, MarketItemDefinition> items;

    public MarketCatalog(IEnumerable<MarketItemDefinition> definitions)
    {
        items = definitions.ToDictionary(entry => entry.ItemId, StringComparer.Ordinal);
        Items = items.Values.OrderByDescending(entry => entry.BaseValue).ToList();
    }

    public IReadOnlyList<MarketItemDefinition> Items { get; }

    public static MarketCatalog FromContent(FactoryContent content) =>
        content.Market.Count > 0 ? new MarketCatalog(content.Market) : CreateDefault();

    public static MarketCatalog CreateDefault() => new(
    [
        new MarketItemDefinition("iron-ore", "Ferro grezzo", 6),
        new MarketItemDefinition("iron-plate", "Lastra di ferro", 23),
        new MarketItemDefinition("copper-ore", "Rame grezzo", 5),
        new MarketItemDefinition("copper-wire", "Filo di rame", 12),
        new MarketItemDefinition("coal", "Carbone", 3),
        new MarketItemDefinition("lead-ore", "Piombo grezzo", 4),
        new MarketItemDefinition("lead-plate", "Lastra di piombo", 14),
        new MarketItemDefinition("titanium-ore", "Titanio grezzo", 14),
        new MarketItemDefinition("titanium-plate", "Lastra di titanio", 52),
        new MarketItemDefinition("graphite", "Grafite", 12),
        new MarketItemDefinition("silicon", "Silicio", 32)
    ]);

    public int BaseValueOf(string itemId) =>
        items.TryGetValue(itemId, out var item) ? item.BaseValue : 1;

    public string GetDisplayName(string itemId) =>
        items.TryGetValue(itemId, out var item) ? item.DisplayName : itemId;

    public bool TryGetItem(string itemId, out MarketItemDefinition item) =>
        items.TryGetValue(itemId, out item!);
}
