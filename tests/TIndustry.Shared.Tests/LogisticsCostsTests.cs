using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The logistics costs approved for the "nastri come Mindustry" plan (Task 2): raw ores for belts, miner and smelter,
/// graphite and silicon for the high tiers; and every campaign level can still afford what it is asked to unlock.
/// </summary>
public sealed class LogisticsCostsTests
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly string MapsDir = Path.Combine(DataDir, "maps");
    private static readonly FactoryContent Content = FactoryContent.Load(Path.Combine(DataDir, "content.json"));

    /// <summary>"iron-ore:1,lead-ore:1" → amounts by item id.</summary>
    private static Dictionary<string, int> Parse(string costs) =>
        costs.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split(':'))
            .ToDictionary(p => p[0], p => int.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture));

    private static Dictionary<string, int> ToMap(IEnumerable<ResourceAmount> amounts) =>
        amounts.ToDictionary(a => a.ItemId, a => a.Amount);

    [Theory]
    [InlineData("conveyor-basic", "iron-ore:1")]
    [InlineData("conveyor-fast", "iron-ore:1,lead-ore:1,titanium-ore:1")]
    [InlineData("conveyor-express", "titanium-ore:1,silicon:1,graphite:1")]
    [InlineData("conveyor-armored", "iron-ore:1,lead-ore:1,titanium-ore:2,graphite:1")]
    [InlineData("junction", "iron-ore:3")]
    [InlineData("splitter", "iron-ore:3")]
    [InlineData("sorter", "iron-ore:2,lead-ore:2")]
    [InlineData("conveyor-bridge", "iron-ore:6,lead-ore:6")]
    [InlineData("miner", "iron-ore:4")]
    [InlineData("smelter", "iron-ore:6")]
    public void BuildCostsMatchTheTable(string id, string cost)
    {
        var actual = Content.FindConveyor(id)?.EffectiveBuildCost ?? Content.FindBuilding(id)!.BuildCost;
        Assert.Equal(Parse(cost), ToMap(actual));
    }

    [Theory]
    [InlineData("conveyor-fast", 450, "iron-ore:8,lead-ore:8,titanium-ore:8")]
    [InlineData("conveyor-express", 850, "titanium-ore:8,silicon:8,graphite:8")]
    [InlineData("conveyor-armored", 600, "iron-ore:8,lead-ore:8,titanium-ore:10,graphite:8")]
    [InlineData("junction", 200, "iron-ore:13")]
    [InlineData("splitter", 280, "iron-ore:13")]
    [InlineData("sorter", 320, "iron-ore:10,lead-ore:10")]
    [InlineData("conveyor-bridge", 340, "iron-ore:21,lead-ore:21")]
    public void UnlockCostsMatchTheTable(string id, int money, string materials)
    {
        // The research tree reads structures; the conveyor's own copy must say the same.
        foreach (var unlock in new[] { Content.RequireStructure(id).Unlock, Content.FindConveyor(id)!.Unlock })
        {
            Assert.NotNull(unlock);
            Assert.Equal(money, unlock!.Money);
            Assert.Equal(Parse(materials), ToMap(unlock.Materials));
        }
    }

    [Fact]
    public void BasicBeltStaysUnlocked() =>
        Assert.Null(Content.RequireStructure("conveyor-basic").Unlock);

    [Theory]
    [InlineData("iron-ore", 0.5f)]
    [InlineData("lead-ore", 0.7f)]
    [InlineData("titanium-ore", 1.0f)]
    [InlineData("graphite", 1.0f)]
    [InlineData("silicon", 0.8f)]
    public void BuildWeightsMatchMindustry(string itemId, float weight) =>
        Assert.Equal(weight, Content.BuildWeight(itemId));

    /// <summary>Basic belts the starting stock must pay for besides the first miner.</summary>
    private const int BootstrapBelts = 4;

    [Fact]
    public void CampaignUnlockObjectivesAreReachable()
    {
        var catalog = CampaignCatalog.Load(Path.Combine(DataDir, "campaign.json"));
        var problems = new List<string>();
        foreach (var level in catalog.Levels)
        {
            var factory = NewGame.Campaign(Content, catalog, level, mapsDirectory: MapsDir);
            var wallet = factory.Wallet;
            var deposits = DepositsOn(factory.Terrain!);

            // Nothing is mined before the first miner: it and a few belts come from the starting stock.
            var bootstrap = ToMap(Content.FindBuilding("miner")!.BuildCost);
            foreach (var belt in Content.RequireConveyor().EffectiveBuildCost)
            {
                bootstrap[belt.ItemId] = bootstrap.GetValueOrDefault(belt.ItemId) + belt.Amount * BootstrapBelts;
            }

            foreach (var (item, amount) in bootstrap)
            {
                if (wallet.MaterialCount(item) < amount)
                {
                    problems.Add($"{level.Id}: parte con {wallet.MaterialCount(item)} {item}, il primo minatore e {BootstrapBelts} nastri ne chiedono {amount}");
                }
            }

            var needed = Content.FindBuilding("smelter")!.BuildCost
                .Concat(Content.RequireConveyor().EffectiveBuildCost)
                .Concat((level.Objectives ?? [])
                    .Where(o => o.Type == CampaignObjectiveType.UnlockResearch)
                    .SelectMany(o => Content.RequireStructure(o.StructureId!).Unlock?.Materials ?? []));
            foreach (var cost in needed)
            {
                if (wallet.MaterialCount(cost.ItemId) < cost.Amount
                    && !Producible(cost.ItemId, deposits, wallet, []))
                {
                    problems.Add($"{level.Id}: {cost.Amount} {cost.ItemId} né in partenza né producibile sulla mappa");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static HashSet<string> DepositsOn(TerrainMap terrain)
    {
        var items = new HashSet<string>(StringComparer.Ordinal);
        for (var y = 0; y < terrain.Height; y++)
        {
            for (var x = 0; x < terrain.Width; x++)
            {
                if (terrain[x, y].Deposit is var d and not DepositKind.None)
                {
                    items.Add(TerrainMap.ItemIdForDeposit(d));
                }
            }
        }

        return items;
    }

    /// <summary>Mined on the map, or made by a recipe whose inputs are (smelting also burns coal).</summary>
    private static bool Producible(string itemId, HashSet<string> deposits, EconomyWallet wallet, HashSet<string> seen)
    {
        if (deposits.Contains(itemId))
        {
            return true;
        }

        if (!seen.Add(itemId))
        {
            return false;
        }

        bool Available(string input) => wallet.MaterialCount(input) > 0 || Producible(input, deposits, wallet, seen);
        return Content.Recipes
            .Where(r => r.Outputs.Any(o => o.ItemId == itemId))
            .Any(r => r.Inputs.All(i => Available(i.ItemId))
                && (!r.Id.StartsWith("smelt-", StringComparison.Ordinal) || Available(Crafter.FuelItemId)));
    }
}
