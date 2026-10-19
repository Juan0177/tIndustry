using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Construction sites (Mindustry's ConstructBuild): build time from the formula, gradual
/// consumption from the Core, stalls without materials, refunds on deconstruction and cancel.
/// <see cref="Construction.Advance"/> is driven by hand at the drone's build speed (0.5).
/// </summary>
public sealed class ConstructionTests
{
    private const float BuildSpeed = 0.5f;
    private const string Iron = "iron-plate";

    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly FactoryContent Content = FactoryContent.Load(Path.Combine(DataDir, "content.json"));

    private static readonly GridPosition MinerOrigin = new(2, 2);

    private static Factory NewFactory(int ironPlates, FactoryContent? content = null, int money = 0)
    {
        var wallet = new EconomyWallet(
            money,
            new Dictionary<string, int>(StringComparer.Ordinal) { [Iron] = ironPlates });
        var factory = new Factory(
            content ?? Content,
            new BeltGrid(),
            CoreStockSink.MakeCoreTiles(new GridPosition(20, 20), 2),
            wallet);
        factory.State.Rules = new GameRules(InstantBuild: false);
        return factory;
    }

    private static BuildSpec MinerSpec(GridPosition? origin = null) =>
        new(Miner.BasicId, BuildingKind.Miner, origin ?? MinerOrigin, Direction.East);

    private static int IndexOf(ConstructSite site, string? itemId)
    {
        for (var i = 0; i < site.Cost.Count; i++)
        {
            if (site.Cost[i].ItemId == itemId)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"voce {itemId} assente");
    }

    /// <summary>Advances until Finished; returns the number of ticks (1-based).</summary>
    private static int RunUntilFinished(Factory factory, ConstructSite site, int maxTicks = 10_000)
    {
        for (var tick = 1; tick <= maxTicks; tick++)
        {
            if (Construction.Advance(factory.State, site, BuildSpeed) == ConstructStep.Finished)
            {
                return tick;
            }
        }

        throw new InvalidOperationException("il cantiere non finisce");
    }

    [Fact]
    public void MinerBuildTicksMatchFormula()
    {
        var factory = NewFactory(ironPlates: 10);
        var site = Construction.BeginBuild(factory.State, MinerSpec(), ownerPlayerId: 0);

        var ticks = RunUntilFinished(factory, site);

        var expected = (int)Math.Ceiling((20 + 4 * 0.7) / BuildSpeed); // 46
        Assert.InRange(ticks, expected - 1, expected + 1);
        var miner = Assert.Single(factory.Miners);
        Assert.Equal(MinerOrigin, miner.Position);
        Assert.Empty(factory.State.Sites);
    }

    [Fact]
    public void ConveyorBuildTicksMatchFormula()
    {
        var factory = NewFactory(ironPlates: 10);
        var spec = new BuildSpec("conveyor-basic", null, new GridPosition(5, 5), Direction.East);
        var site = Construction.BeginBuild(factory.State, spec, ownerPlayerId: 0);

        var ticks = RunUntilFinished(factory, site);

        var expected = (int)Math.Ceiling((20 + 0.7) / BuildSpeed); // 42
        Assert.InRange(ticks, expected - 1, expected + 1);
        Assert.True(factory.Belts.Contains(new GridPosition(5, 5)));
        Assert.Empty(factory.State.Sites);
        Assert.Equal(9, factory.Wallet.MaterialCount(Iron));
    }

    [Fact]
    public void ConsumptionIsGradual()
    {
        var factory = NewFactory(ironPlates: 10);
        var site = Construction.BeginBuild(factory.State, MinerSpec(), ownerPlayerId: 0);
        var iron = IndexOf(site, Iron);
        Assert.Equal(0, site.Spent[iron]);

        for (var i = 0; i < 23; i++)
        {
            Assert.Equal(ConstructStep.Progressed, Construction.Advance(factory.State, site, BuildSpeed));
        }

        Assert.InRange(site.Spent[iron], 1, 3);
        Assert.Equal(10 - site.Spent[iron], factory.Wallet.MaterialCount(Iron));

        RunUntilFinished(factory, site);

        Assert.Equal(4, site.Spent[iron]);
        Assert.Equal(6, factory.Wallet.MaterialCount(Iron));
    }

    [Fact]
    public void SiteStallsWithoutMaterialsAndResumes()
    {
        var factory = NewFactory(ironPlates: 2);
        var site = Construction.BeginBuild(factory.State, MinerSpec(), ownerPlayerId: 0);

        var stalled = false;
        for (var i = 0; i < 200 && !stalled; i++)
        {
            var step = Construction.Advance(factory.State, site, BuildSpeed);
            Assert.NotEqual(ConstructStep.Finished, step);
            stalled = step == ConstructStep.Stalled;
        }

        Assert.True(stalled);
        Assert.Equal(Iron, site.Missing?.ItemId);
        Assert.True(site.Progress <= 0.5f, $"Progress {site.Progress}");
        Assert.Equal(0, factory.Wallet.MaterialCount(Iron));
        Assert.Empty(factory.Miners);
        // Still stalled on the next ticks.
        Assert.Equal(ConstructStep.Stalled, Construction.Advance(factory.State, site, BuildSpeed));

        factory.Wallet.AddMaterial(Iron, 2);
        RunUntilFinished(factory, site);

        Assert.Single(factory.Miners);
        Assert.Equal(0, factory.Wallet.MaterialCount(Iron));
        Assert.Equal(4, site.Spent[IndexOf(site, Iron)]);
    }

    [Fact]
    public void SiteStallsWhenMaterialsSoldMidBuild()
    {
        var factory = NewFactory(ironPlates: 4);
        var site = Construction.BeginBuild(factory.State, MinerSpec(), ownerPlayerId: 0);
        var iron = IndexOf(site, Iron);
        for (var i = 0; i < 23; i++)
        {
            Construction.Advance(factory.State, site, BuildSpeed);
        }

        var left = factory.Wallet.MaterialCount(Iron);
        Assert.True(left > 0);
        Assert.True(factory.TrySellFromWallet(Iron, left));
        Assert.Equal(0, factory.Wallet.MaterialCount(Iron));

        var stalled = false;
        for (var i = 0; i < 200 && !stalled; i++)
        {
            var step = Construction.Advance(factory.State, site, BuildSpeed);
            Assert.NotEqual(ConstructStep.Finished, step);
            Assert.True(factory.Wallet.MaterialCount(Iron) >= 0);
            Assert.True(factory.Wallet.Money >= 0);
            stalled = step == ConstructStep.Stalled;
        }

        Assert.True(stalled);
        Assert.Equal(Iron, site.Missing?.ItemId);
        Assert.Equal(0, factory.Wallet.MaterialCount(Iron));
        Assert.Equal(4 - left, site.Spent[iron]);

        factory.Wallet.AddMaterial(Iron, left);
        RunUntilFinished(factory, site);

        Assert.Single(factory.Miners);
        Assert.Equal(4, site.Spent[iron]);
        Assert.Equal(0, factory.Wallet.MaterialCount(Iron));
        Assert.Null(site.Missing);
    }

    [Theory]
    [InlineData(100, 4)]
    [InlineData(50, 2)]
    public void FinishedBuildingRefundFollowsRule(int refundPercent, int expectedRefund)
    {
        var factory = NewFactory(ironPlates: 0);
        factory.State.Rules = new GameRules(RefundPercent: refundPercent, InstantBuild: false);
        Assert.True(factory.PlaceInstant(MinerSpec(), charge: false));

        var site = Construction.BeginDeconstruct(factory.State, new GridPosition(3, 3), ownerPlayerId: 0);

        Assert.Empty(factory.Miners);
        Assert.True(site.Deconstructing);
        Assert.Equal(1f, site.Progress);
        Assert.Equal(refundPercent * 100, site.RefundBasisPoints);
        Assert.Equal(4, site.Spent[IndexOf(site, Iron)]);
        Assert.Equal(0, factory.Wallet.MaterialCount(Iron));
        // The cells stay taken while the drone takes the building apart.
        Assert.False(factory.CanPlace(MinerSpec(), out _));

        // Refunds arrive gradually, never above the total.
        var ticks = 0;
        ConstructStep step;
        do
        {
            step = Construction.Advance(factory.State, site, BuildSpeed);
            ticks++;
            Assert.InRange(factory.Wallet.MaterialCount(Iron), 0, expectedRefund);
        }
        while (step != ConstructStep.Finished);

        Assert.InRange(ticks, 45, 47);
        Assert.Equal(expectedRefund, factory.Wallet.MaterialCount(Iron));
        Assert.Empty(factory.State.Sites);
        Assert.Empty(factory.Miners);
        Assert.True(factory.CanPlace(MinerSpec(), out _));
    }

    [Fact]
    public void CancelledSiteRefundsAllSpent()
    {
        var factory = NewFactory(ironPlates: 10);
        // Cancel ignores the game's refund rule: everything spent comes back.
        factory.State.Rules = new GameRules(RefundPercent: 50, InstantBuild: false);
        var site = Construction.BeginBuild(factory.State, MinerSpec(), ownerPlayerId: 0);
        var iron = IndexOf(site, Iron);
        while (site.Spent[iron] < 2)
        {
            Assert.Equal(ConstructStep.Progressed, Construction.Advance(factory.State, site, BuildSpeed));
        }

        Assert.Equal(2, site.Spent[iron]);
        Assert.Equal(8, factory.Wallet.MaterialCount(Iron));

        Construction.Cancel(factory.State, site);
        Assert.True(site.Deconstructing);
        Assert.Equal(Construction.FullRefundBasisPoints, site.RefundBasisPoints);

        RunUntilFinished(factory, site);

        Assert.Equal(10, factory.Wallet.MaterialCount(Iron));
        Assert.Empty(factory.Miners);
        Assert.Empty(factory.State.Sites);
    }

    [Fact]
    public void BridgeCostCountsBothCells()
    {
        var factory = NewFactory(ironPlates: 0);
        var def = Content.RequireConveyor("conveyor-bridge");
        var spec = new BuildSpec("conveyor-bridge", BuildingKind.Bridge, new GridPosition(4, 4), Direction.East);

        var cost = factory.CostOf(spec);

        var expected = new List<CostEntry>();
        if (def.MoneyCost > 0)
        {
            expected.Add(new CostEntry(null, def.MoneyCost * 2));
        }

        expected.AddRange(def.EffectiveBuildCost
            .OrderBy(a => a.ItemId, StringComparer.Ordinal)
            .Select(a => new CostEntry(a.ItemId, a.Amount * 2)));
        Assert.Equal(expected, cost);
        Assert.Equal(2, factory.Footprint(spec).Count);
    }

    [Fact]
    public void SiteCellsAreOccupied()
    {
        var factory = NewFactory(ironPlates: 10);
        Construction.BeginBuild(factory.State, MinerSpec(), ownerPlayerId: 0);

        var belt = new BuildSpec("conveyor-basic", null, new GridPosition(3, 3), Direction.East);
        Assert.False(factory.CanPlace(belt, out var reason));
        Assert.Equal(RefusalCode.Occupied, reason);
        Assert.False(factory.CanPlace(MinerSpec(new GridPosition(3, 3)), out reason));
        Assert.Equal(RefusalCode.Occupied, reason);
        Assert.True(factory.IsBuildingTile(new GridPosition(2, 2)));
        Assert.False(factory.TryPlaceBelt(new GridPosition(3, 2), Direction.East));
        Assert.False(factory.PlaceInstant(belt, charge: false));

        Assert.True(factory.CanPlace(MinerSpec(new GridPosition(4, 2)), out reason));
        Assert.Null(reason);
    }

    [Fact]
    public void PlaceInstantWithChargeMatchesTryPlace()
    {
        static Factory Make()
        {
            var f = NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120);
            f.UnlockAllForDemo();
            foreach (var structure in Content.Structures)
            {
                f.Research.ForceUnlock(structure.Id);
            }

            return f;
        }

        var viaTryPlace = Make();
        var viaSpec = Make();
        var smelt = Content.FindRecipe("smelt-iron")!;
        var wire = Content.FindRecipe("craft-copper-wire")!;

        Assert.True(viaTryPlace.TryPlaceMiner(new GridPosition(10, 10), Direction.East));
        Assert.True(viaTryPlace.TryPlaceMiner(new GridPosition(10, 13), Direction.East, definitionId: Miner.AdvancedId));
        Assert.True(viaTryPlace.TryPlaceSmelter(new GridPosition(14, 10), Direction.East, smelt));
        Assert.True(viaTryPlace.TryPlaceAssembler(new GridPosition(18, 10), Direction.North, wire));
        Assert.True(viaTryPlace.TryPlaceGenerator(new GridPosition(22, 10), Direction.South));
        Assert.True(viaTryPlace.TryPlaceExtractor(new GridPosition(26, 10), Direction.West, "copper-wire"));
        Assert.True(viaTryPlace.TryPlacePowerNode(new GridPosition(30, 10)));
        Assert.True(viaTryPlace.TryPlacePowerNode(new GridPosition(32, 10), PowerNode.Tier2Id));
        Assert.True(viaTryPlace.TryPlaceLamp(new GridPosition(35, 10)));
        Assert.True(viaTryPlace.TryPlaceBelt(new GridPosition(10, 20), Direction.East));
        Assert.True(viaTryPlace.TryPlaceJunction(new GridPosition(11, 20), Direction.East));
        Assert.True(viaTryPlace.TryPlaceSplitter(new GridPosition(12, 20), Direction.East));
        Assert.True(viaTryPlace.TryPlaceSorter(new GridPosition(13, 20), Direction.East, "copper-ore"));
        Assert.True(viaTryPlace.TryPlaceBridge(new GridPosition(14, 20), Direction.East));

        BuildSpec[] specs =
        [
            new(Miner.BasicId, BuildingKind.Miner, new GridPosition(10, 10), Direction.East),
            new(Miner.AdvancedId, BuildingKind.MinerAdvanced, new GridPosition(10, 13), Direction.East),
            new(Crafter.SmelterBuildingId, BuildingKind.Smelter, new GridPosition(14, 10), Direction.East, smelt.Id),
            new(Crafter.AssemblerBuildingId, BuildingKind.Assembler, new GridPosition(18, 10), Direction.North, wire.Id),
            new(Generator.BuildingId, BuildingKind.Generator, new GridPosition(22, 10), Direction.South),
            new(Extractor.BuildingId, BuildingKind.Extractor, new GridPosition(26, 10), Direction.West, FilterItemId: "copper-wire"),
            new(PowerNode.Tier1Id, BuildingKind.PowerNode, new GridPosition(30, 10), Direction.East),
            new(PowerNode.Tier2Id, BuildingKind.PowerNodeT2, new GridPosition(32, 10), Direction.East),
            new(Lamp.BuildingId, BuildingKind.Lamp, new GridPosition(35, 10), Direction.East),
            new("conveyor-basic", null, new GridPosition(10, 20), Direction.East),
            new("junction", BuildingKind.Junction, new GridPosition(11, 20), Direction.East),
            new("splitter", BuildingKind.Splitter, new GridPosition(12, 20), Direction.East),
            new("sorter", BuildingKind.Sorter, new GridPosition(13, 20), Direction.East, FilterItemId: "copper-ore"),
            new("conveyor-bridge", BuildingKind.Bridge, new GridPosition(14, 20), Direction.East)
        ];
        foreach (var spec in specs)
        {
            Assert.True(viaSpec.PlaceInstant(spec, charge: true), spec.StructureId);
        }

        Assert.Equal(viaTryPlace.Wallet.Money, viaSpec.Wallet.Money);
        Assert.Equal(viaTryPlace.Wallet.MaterialsSnapshot(), viaSpec.Wallet.MaterialsSnapshot());
        Assert.Equal(
            JsonSerializer.Serialize(viaTryPlace.Capture(), FactorySaveStore.JsonOptions),
            JsonSerializer.Serialize(viaSpec.Capture(), FactorySaveStore.JsonOptions));

        // Every placed entity reads back as the spec that built it.
        foreach (var spec in specs)
        {
            Assert.True(viaSpec.TryGetSpecAt(spec.Origin, out var read, out _), spec.StructureId);
            Assert.Equal(spec, read);
        }
    }

    [Fact]
    public void CampaignRefundPercentFromLevel()
    {
        var catalog = CampaignCatalog.Load(Path.Combine(DataDir, "campaign.json"));

        var first = NewGame.Campaign(Content, catalog, catalog.Levels[0], mapWidth: 60, mapHeight: 60);
        var fourth = NewGame.Campaign(Content, catalog, catalog.Levels[3], mapWidth: 60, mapHeight: 60);

        Assert.Equal(100, first.State.Rules.RefundPercent);
        Assert.Equal(50, fourth.State.Rules.RefundPercent);
        Assert.False(first.State.Rules.InstantBuild);
        Assert.True(NewGame.Demo(Content).State.Rules.InstantBuild);
        Assert.Equal(100, NewGame.Sandbox(Content, new GridPosition(30, 30), mapWidth: 60, mapHeight: 60)
            .State.Rules.RefundPercent);
        // The caller's InstantBuild wins over the mode's default.
        var instantCampaign = NewGame.Campaign(
            Content, catalog, catalog.Levels[3], mapWidth: 60, mapHeight: 60, rules: new GameRules(InstantBuild: true));
        Assert.True(instantCampaign.State.Rules.InstantBuild);
        Assert.Equal(50, instantCampaign.State.Rules.RefundPercent);
    }

    /// <summary>The shipped content with one building definition changed (synthetic cases).</summary>
    private static FactoryContent WithBuilding(string id, Func<BuildingDefinition, BuildingDefinition> change) => new()
    {
        Conveyors = Content.Conveyors,
        Recipes = Content.Recipes,
        Buildings = Content.Buildings.Select(b => b.Id == id ? change(b) : b).ToList(),
        Market = Content.Market,
        Structures = Content.Structures,
        CoreUpgrade = Content.CoreUpgrade,
        BuildWeights = Content.BuildWeights,
        Drone = Content.Drone
    };

    [Theory]
    [InlineData(100)]
    [InlineData(50)]
    public void CancelRefundIsGradual(int ruleRefundPercent)
    {
        var factory = NewFactory(ironPlates: 10);
        factory.State.Rules = new GameRules(RefundPercent: ruleRefundPercent, InstantBuild: false);
        var site = Construction.BeginBuild(factory.State, MinerSpec(), ownerPlayerId: 0);
        var iron = IndexOf(site, Iron);
        while (site.Progress < 0.5f)
        {
            Construction.Advance(factory.State, site, BuildSpeed);
        }

        var spent = site.Spent[iron];
        Assert.InRange(spent, 2, 3);
        Construction.Cancel(factory.State, site);

        var before = factory.Wallet.MaterialCount(Iron);
        Assert.Equal(ConstructStep.Progressed, Construction.Advance(factory.State, site, BuildSpeed));
        Assert.InRange(factory.Wallet.MaterialCount(Iron) - before, 0, 1);

        var previous = factory.Wallet.MaterialCount(Iron);
        while (Construction.Advance(factory.State, site, BuildSpeed) != ConstructStep.Finished)
        {
            Assert.InRange(factory.Wallet.MaterialCount(Iron) - previous, 0, 1);
            previous = factory.Wallet.MaterialCount(Iron);
        }

        Assert.Equal(spent, site.Refunded[iron]);
        Assert.Equal(10, factory.Wallet.MaterialCount(Iron));
        Assert.Empty(factory.Miners);
    }

    [Theory]
    [InlineData(100, 10, 4)]
    [InlineData(50, 5, 2)]
    public void FinishedRefundIsGradualAndExactPerLine(int ruleRefundPercent, int ironBack, int wireBack)
    {
        const string Wire = "copper-wire";
        var factory = NewFactory(ironPlates: 0);
        factory.State.Rules = new GameRules(RefundPercent: ruleRefundPercent, InstantBuild: false);
        var spec = new BuildSpec(Miner.AdvancedId, BuildingKind.MinerAdvanced, MinerOrigin, Direction.East);
        Assert.True(factory.PlaceInstant(spec, charge: false));

        var site = Construction.BeginDeconstruct(factory.State, MinerOrigin, ownerPlayerId: 0);
        Assert.Equal(ConstructStep.Progressed, Construction.Advance(factory.State, site, BuildSpeed));
        Assert.InRange(factory.Wallet.MaterialCount(Iron), 0, 1);
        Assert.InRange(factory.Wallet.MaterialCount(Wire), 0, 1);

        RunUntilFinished(factory, site);

        Assert.Equal(ironBack, factory.Wallet.MaterialCount(Iron));
        Assert.Equal(wireBack, factory.Wallet.MaterialCount(Wire));
        Assert.Equal(ironBack, site.Refunded[IndexOf(site, Iron)]);
        Assert.Equal(wireBack, site.Refunded[IndexOf(site, Wire)]);
    }

    [Fact]
    public void RefundFloorsOnceOverRuleAndBuildingShare()
    {
        // Cost 8, rule 50%, building 75%: floor(8 × 0.5 × 0.75) = 3 (a double floor would give 2).
        var content = WithBuilding(Crafter.SmelterBuildingId, b => b with
        {
            BuildCost = [new ResourceAmount(Iron, 8)],
            RefundPercent = 75
        });
        var spec = new BuildSpec(Crafter.SmelterBuildingId, BuildingKind.Smelter, MinerOrigin, Direction.East);

        var instant = NewFactory(ironPlates: 0, content);
        instant.State.Rules = new GameRules(RefundPercent: 50);
        Assert.Equal(3750, instant.RefundBasisPointsFor(spec));
        Assert.True(instant.PlaceInstant(spec, charge: false));
        Assert.True(instant.RemoveInstant(MinerOrigin, refund: true));
        Assert.Equal(3, instant.Wallet.MaterialCount(Iron));

        var gradual = NewFactory(ironPlates: 0, content);
        gradual.State.Rules = new GameRules(RefundPercent: 50);
        Assert.True(gradual.PlaceInstant(spec, charge: false));
        var site = Construction.BeginDeconstruct(gradual.State, MinerOrigin, ownerPlayerId: 0);
        RunUntilFinished(gradual, site);
        Assert.Equal(3, gradual.Wallet.MaterialCount(Iron));
    }

    [Fact]
    public void InvalidSpecIsRejected()
    {
        var factory = NewFactory(ironPlates: 10);
        BuildSpec[] invalid =
        [
            new("conveyor-nope", null, new GridPosition(5, 5), Direction.East),
            new(Crafter.SmelterBuildingId, BuildingKind.Smelter, MinerOrigin, Direction.East, "craft-copper-wire"),
            new(Crafter.AssemblerBuildingId, BuildingKind.Assembler, MinerOrigin, Direction.East, "smelt-nope"),
            new(Miner.AdvancedId, BuildingKind.Miner, MinerOrigin, Direction.East)
        ];

        foreach (var spec in invalid)
        {
            Assert.False(factory.IsValidSpec(spec), spec.ToString());
            Assert.False(factory.CanPlace(spec, out var reason), spec.ToString());
            Assert.Equal(RefusalCode.InvalidCommand, reason);
            Assert.Throws<InvalidOperationException>(() => Construction.BeginBuild(factory.State, spec, 0));
            Assert.False(factory.PlaceInstant(spec, charge: true), spec.ToString());
        }

        Assert.Empty(factory.State.Sites);
        Assert.Equal(10, factory.Wallet.MaterialCount(Iron));
        Assert.True(factory.IsValidSpec(
            new BuildSpec(Crafter.SmelterBuildingId, BuildingKind.Smelter, MinerOrigin, Direction.East, "smelt-iron")));
    }

    [Fact]
    public void MoneyLineIsPaidGraduallyAndStalls()
    {
        var content = WithBuilding(Miner.BasicId, b => b with { MoneyCost = 10 });
        var factory = NewFactory(ironPlates: 10, content, money: 6);
        var site = Construction.BeginBuild(factory.State, MinerSpec(), ownerPlayerId: 0);
        Assert.Equal(new CostEntry(null, 10), site.Cost[0]);
        var money = IndexOf(site, null);

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(ConstructStep.Progressed, Construction.Advance(factory.State, site, BuildSpeed));
        }

        Assert.InRange(site.Spent[money], 1, 4);
        Assert.Equal(6 - site.Spent[money], factory.Wallet.Money);

        var stalled = false;
        for (var i = 0; i < 200 && !stalled; i++)
        {
            var step = Construction.Advance(factory.State, site, BuildSpeed);
            Assert.NotEqual(ConstructStep.Finished, step);
            Assert.True(factory.Wallet.Money >= 0);
            stalled = step == ConstructStep.Stalled;
        }

        Assert.True(stalled);
        Assert.Equal(new CostEntry(null, 4), site.Missing);
        Assert.Equal(0, factory.Wallet.Money);
        Assert.Equal(6, site.Spent[money]);

        factory.Wallet.AddMoney(4);
        RunUntilFinished(factory, site);

        Assert.Equal(10, site.Spent[money]);
        Assert.Equal(0, factory.Wallet.Money);
        Assert.Equal(6, factory.Wallet.MaterialCount(Iron));
        Assert.Single(factory.Miners);
    }
}
