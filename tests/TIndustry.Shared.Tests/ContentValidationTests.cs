using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Consistency checks on the single shipped data set (data/*.json + godot/assets icons).</summary>
public sealed class ContentValidationTests
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(DataDir, "content.json"));

    // Same lookup as Sprites.For in the client: assets/{id}.png, no fallbacks.
    private static string IconPath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "assets", $"{name}.png");

    [Fact]
    public void StructureIdsAreUnique() =>
        Assert.Empty(Content.Structures.GroupBy(s => s.Id).Where(g => g.Count() > 1).Select(g => g.Key));

    [Fact]
    public void ItemIdsAreUnique() =>
        Assert.Empty(Content.Market.GroupBy(m => m.ItemId).Where(g => g.Count() > 1).Select(g => g.Key));

    [Fact]
    public void PrerequisitesExistAndHaveNoCycles()
    {
        var byId = Content.Structures.ToDictionary(s => s.Id);
        foreach (var s in Content.Structures)
        {
            foreach (var p in s.Prerequisites)
            {
                Assert.True(byId.ContainsKey(p), $"{s.Id}: prerequisito sconosciuto '{p}'");
            }
        }

        var state = new Dictionary<string, int>(); // 1 = visiting, 2 = done
        void Visit(string id)
        {
            if (state.GetValueOrDefault(id) == 2)
            {
                return;
            }

            Assert.False(state.GetValueOrDefault(id) == 1, $"ciclo nei prerequisiti su '{id}'");
            state[id] = 1;
            foreach (var p in byId[id].Prerequisites)
            {
                Visit(p);
            }

            state[id] = 2;
        }

        foreach (var s in Content.Structures)
        {
            Visit(s.Id);
        }
    }

    [Fact]
    public void EveryStructureHasAnIcon() =>
        Assert.Empty(Content.Structures
            .Where(s => !File.Exists(IconPath(s.Id)))
            .Select(s => s.Id));

    [Fact]
    public void EveryMarketItemHasAnIcon() =>
        Assert.Empty(Content.Market
            .Where(m => !File.Exists(IconPath(m.ItemId)))
            .Select(m => m.ItemId));

    [Fact]
    public void BeltItemTexturesExistForEveryMarketItem()
    {
        // Market items' <id>.png is covered by EveryMarketItemHasAnIcon; money has no market entry.
        var missing = Content.Market.Select(m => $"{m.ItemId}-belt")
            .Concat(["money", "money-belt"])
            .Where(name => !File.Exists(IconPath(name)))
            .ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void BuildWeightsArePositiveKnownItems()
    {
        var items = Content.Market.Select(m => m.ItemId).ToHashSet();
        Assert.NotEmpty(Content.BuildWeights);
        foreach (var (itemId, weight) in Content.BuildWeights)
        {
            Assert.True(weight > 0f, $"buildWeights['{itemId}'] = {weight}");
            Assert.True(items.Contains(itemId), $"buildWeights: item sconosciuto '{itemId}'");
        }
    }

    [Fact]
    public void CoreTiers_HaveSpecValues()
    {
        Assert.Equal(3, Content.CoreTiers.Count);
        Assert.Equal(
            [(3, 1000, 10, "alpha"), (4, 4000, 25, "beta"), (5, 12000, 60, "gamma")],
            Content.CoreTiers.Select(t => (t.Size, t.ItemCapacity, t.AutoSellPerSecond, t.UnitId)).ToArray());
        Assert.Equal(["core-shard", "core-foundation", "core-nucleus"], Content.CoreTiers.Select(t => t.Id).ToArray());

        Assert.Empty(Content.CoreTiers[0].BuildCost);
        Assert.Equal(
            [new ResourceAmount("iron-plate", 800), new ResourceAmount("copper-wire", 500), new ResourceAmount("lead-plate", 400)],
            Content.CoreTiers[1].BuildCost);
        Assert.Equal(
            [new ResourceAmount("iron-plate", 3000), new ResourceAmount("lead-plate", 2000),
                new ResourceAmount("silicon", 1500), new ResourceAmount("titanium-plate", 1000)],
            Content.CoreTiers[2].BuildCost);

        // Research reads the structures, not the cores block.
        var foundation = Content.RequireStructure("core-foundation").Unlock!;
        Assert.Equal(5000, foundation.Money);
        Assert.Equal(
            [new ResourceAmount("iron-plate", 200), new ResourceAmount("copper-wire", 100)],
            foundation.Materials);
        var nucleus = Content.RequireStructure("core-nucleus").Unlock!;
        Assert.Equal(25000, nucleus.Money);
        Assert.Equal(
            [new ResourceAmount("titanium-plate", 400), new ResourceAmount("silicon", 300)],
            nucleus.Materials);
    }

    [Fact]
    public void CoreBuildCost_FitsPreviousTier()
    {
        for (var i = 1; i < Content.CoreTiers.Count; i++)
        {
            foreach (var row in Content.CoreTiers[i].BuildCost)
            {
                Assert.True(row.Amount < Content.CoreTiers[i - 1].ItemCapacity,
                    $"{Content.CoreTiers[i].Id}: {row.ItemId} {row.Amount} non entra nel livello prima");
            }
        }
    }

    [Fact]
    public void Units_MatchMindustry()
    {
        Assert.Equal(new UnitDefinition(0.5f, 27.5f, 22.5f), Content.Units["alpha"]);
        Assert.Equal(new UnitDefinition(0.75f, 27.5f, 24.75f), Content.Units["beta"]);
        Assert.Equal(new UnitDefinition(1.0f, 27.5f, 26.6f), Content.Units["gamma"]);
        Assert.Equal(Content.Units["alpha"], Content.UnitForTier(0));
        Assert.Equal(Content.Units["beta"], Content.UnitForTier(1));
        Assert.Equal(Content.Units["gamma"], Content.UnitForTier(2));
    }

    [Fact]
    public void StorageStructures()
    {
        var container = Content.RequireStructure("container");
        Assert.Equal("storage", container.Kind);
        Assert.Equal(300, container.ItemCapacity);
        Assert.Equal(["junction"], container.Prerequisites);
        Assert.Equal(400, container.Unlock!.Money);
        Assert.Equal(
            [new ResourceAmount("iron-plate", 20), new ResourceAmount("lead-plate", 10)],
            container.Unlock.Materials);

        var vault = Content.RequireStructure("vault");
        Assert.Equal("storage", vault.Kind);
        Assert.Equal(1000, vault.ItemCapacity);
        Assert.Equal(["container"], vault.Prerequisites);
        Assert.Equal(900, vault.Unlock!.Money);
        Assert.Equal([new ResourceAmount("titanium-plate", 20)], vault.Unlock.Materials);

        var foundation = Content.RequireStructure("core-foundation");
        Assert.Equal("core-upgrade", foundation.Kind);
        Assert.Equal(0, foundation.ItemCapacity);
        Assert.Equal(5000, foundation.Unlock!.Money);
        var nucleus = Content.RequireStructure("core-nucleus");
        Assert.Equal("core-upgrade", nucleus.Kind);
        Assert.Equal(["core-foundation"], nucleus.Prerequisites);
        Assert.Equal(25000, nucleus.Unlock!.Money);

        Assert.Equal(
            [new ResourceAmount("iron-plate", 40), new ResourceAmount("lead-plate", 20)],
            Content.FindBuilding("container")!.BuildCost);
        Assert.Equal(
            [new ResourceAmount("titanium-plate", 60), new ResourceAmount("iron-plate", 40)],
            Content.FindBuilding("vault")!.BuildCost);
        Assert.Equal(0, Content.RequireStructure("miner").ItemCapacity);
    }

    [Fact]
    public void PowerBlocksHaveTheNumbersOfThePowerNetwork()
    {
        // Spec 2026-10-30 «Rete di corrente»: output and uses per second, node range and links, battery.
        Assert.Equal(30f, Content.FindBuilding(Generator.BuildingId)!.PowerOutput);
        Assert.Equal(8f, Content.FindBuilding(Crafter.SmelterBuildingId)!.PowerUse);
        Assert.Equal(10f, Content.FindBuilding(Crafter.AssemblerBuildingId)!.PowerUse);
        Assert.Equal(0.5f, Content.FindBuilding(Miner.AdvancedId)!.PowerUse);
        Assert.Equal(1f, Content.FindBuilding(Lamp.BuildingId)!.PowerUse);

        var node = Content.FindBuilding(PowerNode.Tier1Id)!;
        Assert.Equal((6, 6), (node.PowerLinkRange, node.MaxPowerLinks));
        var nodeT2 = Content.FindBuilding(PowerNode.Tier2Id)!;
        Assert.Equal((10, 10), (nodeT2.PowerLinkRange, nodeT2.MaxPowerLinks));

        var battery = Content.FindBuilding(Battery.BuildingId)!;
        Assert.Equal(1000, battery.PowerCapacity);
        Assert.Equal([new ResourceAmount("lead-plate", 10), new ResourceAmount("copper-wire", 4)], battery.BuildCost);
        var structure = Content.RequireStructure(Battery.BuildingId);
        Assert.Equal("Batteria", structure.DisplayName);
        Assert.False(structure.UnlockedByDefault);
        Assert.Equal([PowerNode.Tier1Id], structure.Prerequisites);
        Assert.Equal(300, structure.Unlock!.Money);
        Assert.Equal(
            [new ResourceAmount("lead-plate", 10), new ResourceAmount("copper-wire", 5)],
            structure.Unlock.Materials);
    }

    [Fact]
    public void CampaignRefundPercentsAreInRange()
    {
        var catalog = CampaignCatalog.Load(Path.Combine(DataDir, "campaign.json"));
        Assert.All(catalog.Levels, level => Assert.InRange(level.RefundPercent, 0, 100));
    }

    [Fact]
    public void CampaignObjectivesReferenceKnownIds()
    {
        var catalog = CampaignCatalog.Load(Path.Combine(DataDir, "campaign.json"));
        var items = Content.Market.Select(m => m.ItemId).ToHashSet();
        var structures = Content.Structures.Select(s => s.Id).ToHashSet();
        var levelIds = catalog.Levels.Select(l => l.Id).ToList();
        Assert.Equal(levelIds.Count, levelIds.Distinct().Count());

        foreach (var level in catalog.Levels)
        {
            if (!string.IsNullOrWhiteSpace(level.UnlocksNext))
            {
                Assert.Contains(level.UnlocksNext, levelIds);
            }

            foreach (var m in level.StartingMaterials ?? [])
            {
                Assert.True(items.Contains(m.ItemId), $"{level.Id}: materiale iniziale sconosciuto '{m.ItemId}'");
            }

            foreach (var o in level.Objectives ?? [])
            {
                if (o.Type is CampaignObjectiveType.StockItem or CampaignObjectiveType.SellItem)
                {
                    Assert.True(o.ItemId is not null && items.Contains(o.ItemId),
                        $"{level.Id}: item sconosciuto '{o.ItemId}'");
                }
                else if (o.Type == CampaignObjectiveType.UnlockResearch)
                {
                    Assert.True(o.StructureId is not null && structures.Contains(o.StructureId),
                        $"{level.Id}: struttura sconosciuta '{o.StructureId}'");
                }
            }
        }
    }
}
