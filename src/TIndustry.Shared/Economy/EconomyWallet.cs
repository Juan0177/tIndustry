namespace TIndustry.Shared;

/// <summary>Stock-first wallet (materials + money). Mirror of Logistics EconomyWallet.</summary>
public sealed class EconomyWallet
{
    private readonly Dictionary<string, int> materials;

    public EconomyWallet(int money = 0, IReadOnlyDictionary<string, int>? materials = null)
    {
        Money = money;
        this.materials = materials is null
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : new Dictionary<string, int>(materials, StringComparer.Ordinal);
    }

    public int Money { get; private set; }

    public int MaterialCount(string itemId) => materials.GetValueOrDefault(itemId);

    public Dictionary<string, int> MaterialsSnapshot() => new(materials, StringComparer.Ordinal);

    public bool CanAfford(int money, IReadOnlyList<ResourceAmount> cost)
    {
        if (Money < money)
        {
            return false;
        }

        for (var i = 0; i < cost.Count; i++)
        {
            if (MaterialCount(cost[i].ItemId) < cost[i].Amount)
            {
                return false;
            }
        }

        return true;
    }

    public void AddMoney(int amount) => Money += amount;

    public void AddMaterial(string itemId, int amount)
    {
        if (amount <= 0 || string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        materials[itemId] = MaterialCount(itemId) + amount;
    }

    public bool TryRemoveMaterial(string itemId, int amount)
    {
        if (amount <= 0 || MaterialCount(itemId) < amount)
        {
            return false;
        }

        materials[itemId] = MaterialCount(itemId) - amount;
        return true;
    }

    public bool TrySpend(int money, IReadOnlyList<ResourceAmount> cost)
    {
        if (!CanAfford(money, cost))
        {
            return false;
        }

        Money -= money;
        foreach (var entry in cost)
        {
            materials[entry.ItemId] -= entry.Amount;
        }

        return true;
    }

    public bool MeetsUnlock(UnlockRequirement? unlock) =>
        unlock is null
        || (Money >= unlock.Money
            && unlock.Materials.All(entry => MaterialCount(entry.ItemId) >= entry.Amount));
}
