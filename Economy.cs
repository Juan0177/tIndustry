namespace TIndustry.Logistics;

public sealed record MarketItemDefinition(
    string ItemId,
    string DisplayName,
    int SellPrice);

public sealed record BuildingDefinition(
    string Id,
    int MoneyCost,
    IReadOnlyList<ResourceAmount> BuildCost,
    int RefundPercent = 100);

public sealed record CoreUpgradeDefinition(
    int MoneyCost,
    IReadOnlyList<ResourceAmount> BuildCost,
    int SaleBonusPercent);

public sealed record EconomyConfig(
    CoreUpgradeDefinition CoreUpgrade,
    string RefundPolicyNote);

public sealed class MarketCatalog
{
    private readonly Dictionary<string, MarketItemDefinition> items;

    public MarketCatalog(IEnumerable<MarketItemDefinition> definitions)
    {
        items = definitions.ToDictionary(entry => entry.ItemId, StringComparer.Ordinal);
        Items = items.Values.OrderByDescending(entry => entry.SellPrice).ToList();
    }

    public IReadOnlyList<MarketItemDefinition> Items { get; }

    public static MarketCatalog CreateDefault() => new(
    [
        new MarketItemDefinition("iron-ore", "Ferro grezzo", 8),
        new MarketItemDefinition("iron-plate", "Lastra di ferro", 30),
        new MarketItemDefinition("copper-wire", "Filo di rame", 14)
    ]);

    public int GetSellPrice(string itemId) =>
        items.TryGetValue(itemId, out var item) ? item.SellPrice : 1;

    public string GetDisplayName(string itemId) =>
        items.TryGetValue(itemId, out var item) ? item.DisplayName : itemId;

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
            ? $"Conviene fondere: lastre +${margin} vs 2 ore grezze."
            : "Vendere ore grezze può essere sufficiente a questo prezzo.";
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
