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
    [InlineData("conveyor-stack", "plastic:1,silicon:1,graphite:1")]
    [InlineData("conveyor-armored", "iron-ore:1,lead-ore:1,titanium-ore:2,graphite:1")]
    [InlineData("junction", "iron-ore:3")]
    [InlineData("router", "iron-ore:3")]
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
    [InlineData("conveyor-fast", 73_000, "iron-ore:24,lead-ore:24,titanium-ore:24")]
    [InlineData("conveyor-express", 1_300_000, "titanium-ore:32,silicon:32,graphite:32")]
    [InlineData("conveyor-stack", 1_440_000, "plastic:144,silicon:144,graphite:144")] // campaign reward: ×3 of the sheet
    [InlineData("conveyor-armored", 1_300_000, "iron-ore:32,lead-ore:32,titanium-ore:40,graphite:32")]
    [InlineData("junction", 6_900, "iron-ore:26")]
    [InlineData("router", 9_700, "iron-ore:26")]
    [InlineData("sorter", 73_000, "iron-ore:30,lead-ore:30")]
    [InlineData("conveyor-bridge", 91_000, "iron-ore:63,lead-ore:63")]
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

    /// <summary>Speed in cells per second (Mindustry's speed × 60), items per second = speed / spacing (Task 3).</summary>
    [Theory]
    [InlineData("conveyor-basic", 2.1f, 3, 0.4f, 5.25f)]
    [InlineData("conveyor-fast", 4.8f, 3, 0.4f, 12f)]
    [InlineData("conveyor-armored", 4.8f, 3, 0.4f, 12f)]
    [InlineData("conveyor-express", 7.2f, 3, 0.4f, 18f)]
    [InlineData("conveyor-stack", 21.6f, 1, 1f, 21.6f)] // packs per second, up to 10 pieces each
    [InlineData("junction", 18f, 6, 1f, 18f)]
    [InlineData("router", 8f, 1, 1f, 8f)]
    [InlineData("sorter", 8f, 1, 1f, 8f)]
    [InlineData("conveyor-bridge", 5.25f, 1, 1f, 5.25f)]
    public void RatesMatchTheTable(string id, float speed, int capacity, float spacing, float itemsPerSecond)
    {
        var conveyor = Content.RequireConveyor(id);
        Assert.Equal(speed, conveyor.RateItemsPerSecond);
        Assert.Equal(capacity, conveyor.Capacity);
        Assert.Equal(spacing, conveyor.ItemSpacing);
        Assert.Equal(itemsPerSecond, conveyor.RateItemsPerSecond / conveyor.ItemSpacing, 3);
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

    [Fact]
    public void SandboxStartsWithRawIronForTheFirstMinerAndBelts()
    {
        var wallet = NewGame.Sandbox(Content, new GridPosition(30, 30), mapWidth: 60, mapHeight: 60).Wallet;

        Assert.Equal(48, wallet.MaterialCount("iron-ore"));
        Assert.Equal(48, wallet.MaterialCount("iron-plate"));
        Assert.Equal(10, wallet.MaterialCount("copper-wire"));
    }

    [Fact]
    public void DemoStockCoversEveryLogisticsMaterial()
    {
        var factory = new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), 2));
        factory.EnsureDemoBuildStock();
        Assert.All(
            new[] { "iron-ore", "lead-ore", "titanium-ore", "graphite", "silicon" },
            item => Assert.Equal(200, factory.Wallet.MaterialCount(item)));

        // A top-up, not an add: a second call and a smaller target change nothing.
        factory.EnsureDemoBuildStock(logistics: 50);
        Assert.Equal(200, factory.Wallet.MaterialCount("silicon"));
    }

    /// <summary>Basic belts the starting stock must pay for besides the first miner.</summary>
    private const int BootstrapBelts = 4;

    /// <summary>
    /// Levels whose stock objectives are already covered by the starting stock: a defect older than the research
    /// balancing, left to the campaign rework (aspect 8, docs/STATO.md). For them a covered objective drives no research.
    /// </summary>
    private static readonly HashSet<string> CoveredAtStart = new(StringComparer.Ordinal)
    {
        "level-03-lastre",
        "level-04-fili", // starts with 6 copper wires for «Magazzino 4 fili di rame»
        "level-08-logistica",
    };

    /// <summary>
    /// Every level pays its first miner and <see cref="BootstrapBelts"/> belts from the starting stock, has the money
    /// for all the research it needs at once (<see cref="ResearchNeeded"/>: its research objectives, the machines of
    /// the recipes behind its stock and sell objectives, and their locked prerequisites), and has (or can mine and
    /// craft on its map) the materials of the smelter, the basic belt and all that research. An ore counts as mined
    /// only if a miner the level has (unlocked at start, or one of the research it needs) can dig it (titanium needs
    /// the Miner T2). A stock objective of a made item is unmet both at start and once that research is paid.
    /// Limits: amounts beyond the starting stock are not checked once an item is producible, sale income is
    /// ignored (the money must be there at start), stock and sell objectives only drive the research (their
    /// completion is not played), and in <see cref="CoveredAtStart"/> an objective the start covers is skipped.
    /// </summary>
    [Fact]
    public void CampaignUnlockObjectivesAreReachable()
    {
        var catalog = CampaignCatalog.Load(Path.Combine(DataDir, "campaign.json"));
        var problems = new List<string>();
        foreach (var level in catalog.Levels)
        {
            var factory = NewGame.Campaign(Content, catalog, level, mapsDirectory: MapsDir);
            var wallet = factory.Wallet;
            var research = ResearchNeeded(level, factory);
            var miners = new[] { Miner.BasicId, Miner.AdvancedId }
                .Where(id => factory.Research.IsUnlocked(id) || research.Any(r => r.Id == id))
                .ToList();
            var deposits = DepositsOn(factory.Terrain!, miners);

            var money = research.Sum(r => r.Unlock?.Money ?? 0);
            if (level.StartingMoney < money)
            {
                var ids = string.Join(", ", research.Select(r => r.Id));
                problems.Add($"{level.Id}: parte con ${level.StartingMoney}, gli sblocchi che servono ({ids}) costano ${money}");
            }

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
                .Concat(research.SelectMany(r => r.Unlock?.Materials ?? []));
            foreach (var cost in needed)
            {
                if (wallet.MaterialCount(cost.ItemId) < cost.Amount
                    && !Producible(cost.ItemId, deposits, wallet, []))
                {
                    problems.Add($"{level.Id}: {cost.Amount} {cost.ItemId} né in partenza né producibile sulla mappa coi minatori {string.Join("/", miners)}");
                }
            }

            // A stock objective that asks for research must be open at start and once that research is paid.
            if (!CoveredAtStart.Contains(level.Id))
            {
                foreach (var o in (level.Objectives ?? []).Where(o => o.Type == CampaignObjectiveType.StockItem && IsMade(o.ItemId!)))
                {
                    var start = wallet.MaterialCount(o.ItemId!);
                    if (start >= o.Amount)
                    {
                        problems.Add($"{level.Id}: «{o.Label}» è già fatto al primo tick ({start} {o.ItemId})");
                    }

                    var left = start
                        - research.SelectMany(r => r.Unlock?.Materials ?? []).Where(m => m.ItemId == o.ItemId).Sum(m => m.Amount);
                    if (left >= o.Amount)
                    {
                        problems.Add($"{level.Id}: «{o.Label}» è già fatto in partenza ({left} {o.ItemId} dopo gli sblocchi)");
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>True when the item comes from a recipe or needs the Miner T2 (it drives research); other ores are mined.</summary>
    private static bool IsMade(string itemId) =>
        itemId == "titanium-ore" || (!IsOre(itemId) && Content.Recipes.Any(r => r.Outputs.Any(o => o.ItemId == itemId)));

    /// <summary>True for the item of a deposit (mined on the map even when a recipe also makes it, like the separator's).</summary>
    private static bool IsOre(string itemId) =>
        Enum.GetValues<DepositKind>().Any(d => d != DepositKind.None && TerrainMap.ItemIdForDeposit(d) == itemId);

    /// <summary>The craft machine that runs <paramref name="recipe"/> (its <c>recipes</c> prefix), null when none does yet.</summary>
    private static string? MachineOf(RecipeDefinition recipe) =>
        Content.Buildings.FirstOrDefault(b => b.IsCrafter && recipe.Id.StartsWith(b.RecipePrefix!, StringComparison.Ordinal))?.Id;

    /// <summary>
    /// Locked research a level needs: its research objectives; for each stock or sell objective (in
    /// <see cref="CoveredAtStart"/> only those the start does not already cover), the machine of the item's first recipe that a
    /// machine runs (<see cref="MachineOf"/>; titanium ore needs the Miner T2, the other ores are mined), following the inputs the start has none of; then every locked prerequisite, prerequisites first.
    /// </summary>
    private static List<StructureDefinition> ResearchNeeded(CampaignLevelDefinition level, Factory factory)
    {
        var wanted = new List<string>();
        void NeedItem(string itemId, HashSet<string> seen)
        {
            if (!seen.Add(itemId))
            {
                return;
            }

            if (itemId == "titanium-ore")
            {
                wanted.Add(Miner.AdvancedId);
                return;
            }

            var recipe = IsOre(itemId)
                ? null
                : Content.Recipes.FirstOrDefault(r => r.Outputs.Any(o => o.ItemId == itemId) && MachineOf(r) is not null);
            if (recipe is null)
            {
                return;
            }

            wanted.Add(MachineOf(recipe)!);
            foreach (var input in recipe.Inputs.Where(i => factory.Wallet.MaterialCount(i.ItemId) == 0))
            {
                NeedItem(input.ItemId, seen);
            }
        }

        foreach (var o in level.Objectives ?? [])
        {
            if (o.Type == CampaignObjectiveType.UnlockResearch)
            {
                wanted.Add(o.StructureId!);
            }
            else if (o.Type is CampaignObjectiveType.StockItem or CampaignObjectiveType.SellItem
                && (!CoveredAtStart.Contains(level.Id) || factory.Wallet.MaterialCount(o.ItemId!) < o.Amount))
            {
                NeedItem(o.ItemId!, []);
            }
        }

        var ordered = new List<StructureDefinition>();
        void Add(string id)
        {
            if (factory.Research.IsUnlocked(id) || ordered.Any(s => s.Id == id))
            {
                return;
            }

            var structure = Content.RequireStructure(id);
            foreach (var pre in structure.Prerequisites)
            {
                Add(pre);
            }

            ordered.Add(structure);
        }

        foreach (var id in wanted)
        {
            Add(id);
        }

        return ordered;
    }

    /// <summary>Items of the deposits on the map that one of <paramref name="miners"/> can dig.</summary>
    private static HashSet<string> DepositsOn(TerrainMap terrain, IReadOnlyList<string> miners)
    {
        var items = new HashSet<string>(StringComparer.Ordinal);
        for (var y = 0; y < terrain.Height; y++)
        {
            for (var x = 0; x < terrain.Width; x++)
            {
                if (terrain[x, y].Deposit is var d and not DepositKind.None && miners.Any(m => Content.MinerCanDig(m, d)))
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
