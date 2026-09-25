namespace TIndustry.Shared;

/// <summary>Sale / spend ledger for slice economy (enough for Mercato + future campaign).</summary>
public sealed class EconomySession
{
    private readonly Dictionary<string, int> soldByItem = new(StringComparer.Ordinal);

    public EconomySession(int startingMoney = 0)
    {
        StartingMoney = startingMoney;
    }

    public int StartingMoney { get; private set; }
    public int SaleIncome { get; private set; }
    public IReadOnlyDictionary<string, int> SoldByItem => soldByItem;

    public void RecordSale(string itemId, int price)
    {
        SaleIncome += price;
        soldByItem[itemId] = soldByItem.GetValueOrDefault(itemId) + 1;
    }

    public void Restore(
        int startingMoney,
        int saleIncome,
        IReadOnlyDictionary<string, int>? soldByItemSnapshot)
    {
        StartingMoney = startingMoney;
        SaleIncome = saleIncome;
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
