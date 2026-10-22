namespace TIndustry.Shared;

/// <summary>Sale / spend ledger for the game economy (enough for Mercato + future campaign).</summary>
public sealed class EconomySession : ISessionView
{
    private readonly Dictionary<string, int> soldByItem = new(StringComparer.Ordinal);

    public EconomySession(int startingMoney = 0)
    {
        StartingMoney = startingMoney;
    }

    public int StartingMoney { get; private set; }
    public int SaleIncome { get; private set; }
    public IReadOnlyDictionary<string, int> SoldByItem => soldByItem;

    /// <summary>Records <paramref name="count"/> pieces sold together for <paramref name="income"/> in total (saturating).</summary>
    public void RecordSales(string itemId, int income, int count)
    {
        SaleIncome = (int)Math.Min((long)SaleIncome + income, int.MaxValue);
        soldByItem[itemId] = (int)Math.Min((long)soldByItem.GetValueOrDefault(itemId) + count, int.MaxValue);
    }

    public void RecordSale(string itemId, int price)
    {
        SaleIncome = (int)Math.Min((long)SaleIncome + price, int.MaxValue);
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
