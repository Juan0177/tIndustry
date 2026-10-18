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
    public void DroneIsDefined()
    {
        // FactoryContent.Load throws when units.drone is missing: loading it proves it is there.
        Assert.Equal(new UnitDefinition(BuildSpeed: 0.5f, BuildRange: 27.5f, Speed: 22.5f), Content.Drone);
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
