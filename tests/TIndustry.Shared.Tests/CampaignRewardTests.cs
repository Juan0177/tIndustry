using System.Text.Json;
using System.Text.Json.Nodes;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The campaign rewards of the research (spec «Ricerca allargata», «Premi della campagna»): four nodes, one per chapter
/// (<see cref="StructureDefinition.CampaignReward"/>), that the campaign gives for free when a chapter closes
/// (<see cref="Factory.GrantCampaignReward"/>) and never sells (<see cref="RefusalCode.CampaignRewardOnly"/>); in free
/// play they are bought like any node, at three times the price of the sheet (tools/dev/genera_finanza.py).
/// </summary>
public sealed class CampaignRewardTests
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly FactoryContent Content = FactoryContent.Load(Path.Combine(DataDir, "content.json"));
    private static readonly CampaignCatalog Catalog = CampaignCatalog.Load(Path.Combine(DataDir, "campaign.json"));

    private static readonly (string Id, int Chapter)[] Rewards =
    [
        ("combustion-generator", 1),
        ("conveyor-stack", 2),
        ("circuit", 3),
        ("drone-boost", 4)
    ];

    private static Factory Campaign() =>
        NewGame.Campaign(Content, Catalog, Catalog.Levels[0], mapWidth: 60, mapHeight: 60,
            rules: new GameRules(InstantBuild: true));

    private static Factory Sandbox() =>
        NewGame.Sandbox(Content, new GridPosition(30, 30), mapWidth: 60, mapHeight: 60,
            rules: new GameRules(InstantBuild: true));

    /// <summary>Unlocks the prerequisites and puts the unlock cost in the wallet (plus a margin).</summary>
    private static void Afford(Factory factory, StructureDefinition node)
    {
        foreach (var pre in node.Prerequisites)
        {
            factory.Research.ForceUnlock(pre);
        }

        factory.Wallet.AddMoney(node.Unlock!.Money + 1000);
        foreach (var m in node.Unlock.Materials)
        {
            factory.Wallet.AddMaterial(m.ItemId, m.Amount + 10);
        }
    }

    private static Refusal? SubmitUnlock(SimHost host, string id)
    {
        Refusal? refused = null;
        host.CommandRejected += (_, r) => refused = r;
        host.Submit(new UnlockStructure(0, id));
        host.AdvanceTicks(1);
        return refused;
    }

    [Fact]
    public void ContentHasExactlyTheFourChapterRewards()
    {
        var rewards = Content.Structures
            .Where(s => s.CampaignReward is not null)
            .Select(s => (s.Id, s.CampaignReward!.Value))
            .OrderBy(r => r.Value)
            .ToArray();
        Assert.Equal(Rewards, rewards);
        foreach (var (id, _) in Rewards)
        {
            Assert.NotNull(Content.RequireStructure(id).Unlock);
        }
    }

    [Fact]
    public void ContentRefusesBadRewards()
    {
        // A chapter out of 1–4, the same chapter twice, a chapter without its reward, a reward without an unlock.
        LoadMutated(root => Structure(root, "circuit")["campaignReward"] = 5);
        LoadMutated(root => Structure(root, "circuit")["campaignReward"] = 0);
        LoadMutated(root => Structure(root, "circuit")["campaignReward"] = 2);
        LoadMutated(root => Structure(root, "circuit").Remove("campaignReward"));
        LoadMutated(root =>
        {
            Structure(root, "circuit").Remove("campaignReward");
            Structure(root, "conveyor-basic")["campaignReward"] = 3;
        });
    }

    private static JsonObject Structure(JsonObject root, string id) =>
        root["structures"]!.AsArray().Single(s => (string?)s!["id"] == id)!.AsObject();

    private static void LoadMutated(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(DataDir, "content.json")))!.AsObject();
        mutate(root);
        var path = Path.Combine(Path.GetTempPath(), $"tindustry-rewards-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, root.ToJsonString());
        try
        {
            Assert.Throws<InvalidDataException>(() => FactoryContent.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("combustion-generator")]
    [InlineData("conveyor-stack")]
    [InlineData("circuit")]
    [InlineData("drone-boost")]
    public void InTheCampaignARewardIsNotForSale(string id)
    {
        var factory = Campaign();
        var node = Content.RequireStructure(id);
        Afford(factory, node);
        var money = factory.Wallet.Money;
        var materials = factory.Wallet.MaterialsSnapshot().ToDictionary(kv => kv.Key, kv => kv.Value);
        var host = new SimHost(factory);

        var refused = SubmitUnlock(host, id);

        Assert.NotNull(refused);
        Assert.Equal(RefusalCode.CampaignRewardOnly, refused!.Code);
        Assert.Equal(id, refused.StructureId);
        Assert.False(factory.IsStructureUnlocked(id));
        Assert.Equal(money, factory.Wallet.Money);
        Assert.Equal(materials, factory.Wallet.MaterialsSnapshot().ToDictionary(kv => kv.Key, kv => kv.Value));
        Assert.False(factory.TryUnlockStructure(id));
        Assert.False(factory.Research.CanUnlock(node, factory.Wallet, inCampaign: true));
        Assert.Equal(money, factory.Wallet.Money);
    }

    [Fact]
    public void InTheCampaignAnOrdinaryNodeIsStillForSale()
    {
        var factory = Campaign();
        var node = Content.RequireStructure("smelter");
        Afford(factory, node);
        var money = factory.Wallet.Money;

        Assert.Null(SubmitUnlock(new SimHost(factory), "smelter"));

        Assert.True(factory.IsStructureUnlocked("smelter"));
        Assert.Equal(money - node.Unlock!.Money, factory.Wallet.Money);
    }

    [Theory]
    [InlineData("combustion-generator")]
    [InlineData("conveyor-stack")]
    [InlineData("circuit")]
    [InlineData("drone-boost")]
    public void InFreePlayARewardIsBoughtLikeAnyNode(string id)
    {
        var factory = Sandbox();
        var node = Content.RequireStructure(id);
        Afford(factory, node);
        var money = factory.Wallet.Money;
        Assert.True(factory.Research.CanUnlock(node, factory.Wallet, inCampaign: false));

        Assert.Null(SubmitUnlock(new SimHost(factory), id));

        Assert.True(factory.IsStructureUnlocked(id));
        Assert.Equal(money - node.Unlock!.Money, factory.Wallet.Money);
    }

    [Fact]
    public void GrantUnlocksForFreeEvenWithoutPrerequisites()
    {
        var factory = Campaign();
        var money = factory.Wallet.Money;
        var materials = factory.Wallet.MaterialsSnapshot().ToDictionary(kv => kv.Key, kv => kv.Value);
        Assert.False(factory.Research.MeetsPrerequisites(Content.RequireStructure("drone-boost")));

        foreach (var (id, _) in Rewards)
        {
            Assert.True(factory.GrantCampaignReward(id), id);
            Assert.True(factory.IsStructureUnlocked(id), id);
        }

        Assert.Equal(money, factory.Wallet.Money);
        Assert.Equal(materials, factory.Wallet.MaterialsSnapshot().ToDictionary(kv => kv.Key, kv => kv.Value));
        // The prerequisites stay locked: only the reward is given.
        Assert.False(factory.IsStructureUnlocked("upgrade-drone-build"));
        Assert.False(factory.IsStructureUnlocked("conveyor-express"));
        // The drone level 2 works at once.
        Assert.Equal(100, Upgrades.DroneHoldPercent(factory.Research));
    }

    [Fact]
    public void GrantRefusesUnknownAndOrdinaryNodes()
    {
        var factory = Campaign();
        Assert.False(factory.GrantCampaignReward("no-such-node"));
        Assert.False(factory.GrantCampaignReward("smelter"));
        Assert.False(factory.IsStructureUnlocked("smelter"));
        // A second grant of the same reward changes nothing.
        Assert.True(factory.GrantCampaignReward("circuit"));
        Assert.False(factory.GrantCampaignReward("circuit"));
    }

    [Fact]
    public void GrantedCircuitOpensTheRecipeOnAnAssemblerAlreadyBuilt()
    {
        var factory = Campaign();
        factory.Research.ForceUnlock(Crafter.AssemblerBuildingId);
        var origin = new GridPosition(factory.State.CoreOrigin.X + 8, factory.State.CoreOrigin.Y + 8);
        Assert.True(factory.PlaceInstant(new BuildSpec(Crafter.AssemblerBuildingId, BuildingKind.Assembler, origin, Direction.East),
            charge: false));
        var assembler = Assert.Single(factory.Assemblers);
        Assert.DoesNotContain(assembler.AvailableRecipes, r => r.Id == "craft-circuit");

        Assert.True(factory.GrantCampaignReward("circuit"));

        Assert.Contains(assembler.AvailableRecipes, r => r.Id == "craft-circuit");
    }

    [Fact]
    public void AgentsSeeTheChapterAndTheRefusal()
    {
        var factory = Campaign();
        Afford(factory, Content.RequireStructure("circuit"));
        using var game = new AgentGame(new SimHost(factory), Content, campaign: Catalog);

        var nodes = AgentGameCall(game, "catalogo").GetProperty("strutture").EnumerateArray().ToList();
        var circuit = nodes.Single(s => s.GetProperty("id").GetString() == "circuit");
        Assert.Equal(3, circuit.GetProperty("premio").GetInt32());
        var smelter = nodes.Single(s => s.GetProperty("id").GetString() == "smelter");
        Assert.False(smelter.TryGetProperty("premio", out _));

        var refused = Assert.Throws<AgentException>(() => AgentGameCall(game, "sblocca", """{"struttura":"circuit"}"""));
        Assert.Equal(nameof(RefusalCode.CampaignRewardOnly), refused.Code);
        Assert.Equal(3, (int)refused.Missing!["premio"]!);
        Assert.False(factory.IsStructureUnlocked("circuit"));
    }

    private static JsonElement AgentGameCall(AgentGame game, string method, string json = "{}") =>
        game.Call(method, JsonDocument.Parse(json).RootElement);

    [Fact]
    public void AGrantedRewardSurvivesTheSave()
    {
        var factory = Campaign();
        Assert.True(factory.GrantCampaignReward("conveyor-stack"));

        var json = JsonSerializer.Serialize(factory.Capture(), FactorySaveStore.JsonOptions);
        var restored = Factory.Restore(Content, JsonSerializer.Deserialize<FactorySaveData>(json, FactorySaveStore.JsonOptions)!);

        Assert.Equal(Catalog.Levels[0].Id, restored.ActiveCampaignLevelId);
        Assert.True(restored.IsStructureUnlocked("conveyor-stack"));
        Assert.False(restored.IsStructureUnlocked("conveyor-express"));
    }
}
