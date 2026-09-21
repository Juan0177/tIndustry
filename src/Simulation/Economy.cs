namespace TIndustry.Logistics;

public sealed record MarketItemDefinition(
    string ItemId,
    string DisplayName,
    int SellPrice,
    int? MinSellPrice = null,
    int? MaxSellPrice = null,
    int? SoftStock = null);

public sealed record BuildingDefinition(
    string Id,
    int MoneyCost,
    IReadOnlyList<ResourceAmount> BuildCost,
    int RefundPercent = 100,
    /// <summary>Footprint edge length for power nodes (1 or 2). 0 = not a node.</summary>
    int Footprint = 0,
    /// <summary>Max geometric power links for power nodes. 0 = not a node.</summary>
    int MaxPowerLinks = 0,
    /// <summary>Max link range in tiles (center-to-center) for power nodes.</summary>
    float PowerLinkRange = 0f);

public sealed record CoreUpgradeDefinition(
    int MoneyCost,
    IReadOnlyList<ResourceAmount> BuildCost,
    int SaleBonusPercent);

public sealed record EconomyConfig(
    CoreUpgradeDefinition CoreUpgrade,
    string RefundPolicyNote);

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

    /// <summary>Shared default catalog — avoid reallocating on hot sale/sim paths.</summary>
    public static MarketCatalog Default { get; } = CreateDefault();

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
    /// Supply curve: empty stock → list/base price (or slight premium via max);
    /// high stock softens toward min. Pure function of base + wallet stock — no session state.
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
        // stock ≤ 1 → list/base (selling your only unit stays at listino);
        // surplus softens toward min (supply pressure).
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

        // smelt-iron consumes 2 ore → 1 plate
        var orePairValue = ore.SellPrice * 2;
        var margin = plate.SellPrice - orePairValue;
        return margin > 0
            ? $"Stock → costruisci, oppure vendi lastre (+${margin} vs 2 ore). Prezzi reagiscono allo stock."
            : "Accumula in magazzino; vendi dal Mercato quando ti servono $.";
    }
}

public sealed class EconomySession
{
    private readonly Dictionary<string, int> soldByItem = new(StringComparer.Ordinal);

    public EconomySession(int startingMoney)
    {
        StartingMoney = startingMoney;
    }

    public int StartingMoney { get; private set; }
    public int BuildSpend { get; private set; }
    public int UnlockSpend { get; private set; }
    public int UpgradeSpend { get; private set; }
    public int SaleIncome { get; private set; }
    public int RefundIncome { get; private set; }
    public IReadOnlyDictionary<string, int> SoldByItem => soldByItem;

    public int NetWorthDelta(EconomyWallet wallet) => wallet.Money - StartingMoney;

    public void RecordSale(string itemId, int price)
    {
        SaleIncome += price;
        soldByItem[itemId] = soldByItem.GetValueOrDefault(itemId) + 1;
    }

    public void RecordBuildSpend(int money) => BuildSpend += Math.Max(0, money);

    public void RecordUnlockSpend(int money) => UnlockSpend += Math.Max(0, money);

    public void RecordUpgradeSpend(int money) => UpgradeSpend += Math.Max(0, money);

    public void RecordRefund(int money) => RefundIncome += Math.Max(0, money);

    public void Restore(
        int startingMoney,
        int buildSpend,
        int unlockSpend,
        int upgradeSpend,
        int saleIncome,
        int refundIncome,
        IReadOnlyDictionary<string, int>? soldByItemSnapshot)
    {
        StartingMoney = startingMoney;
        BuildSpend = buildSpend;
        UnlockSpend = unlockSpend;
        UpgradeSpend = upgradeSpend;
        SaleIncome = saleIncome;
        RefundIncome = refundIncome;
        soldByItem.Clear();
        if (soldByItemSnapshot is null)
        {
            return;
        }

        foreach (var pair in soldByItemSnapshot)
        {
            soldByItem[pair.Key] = pair.Value;
        }
    }
}
