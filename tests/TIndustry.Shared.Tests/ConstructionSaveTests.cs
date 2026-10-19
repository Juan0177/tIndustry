using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Save v11 and the canonical snapshot carry the rules, drones, plan queues and construction sites.</summary>
[Collection(SaveStoreCollection.Name)]
public sealed class ConstructionSaveTests
{
    private const string Iron = "iron-plate";
    private const int Player = 0;

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static Factory NewFactory(GameRules rules)
    {
        var wallet = new EconomyWallet(
            0,
            new Dictionary<string, int>(StringComparer.Ordinal) { [Iron] = 1000 });
        var factory = new Factory(
            Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(new GridPosition(20, 20), 2), wallet);
        factory.State.Rules = rules;
        foreach (var id in ResearchState.DemoStructureIds)
        {
            factory.Research.ForceUnlock(id);
        }

        factory.AddBuilder(Player);
        return factory;
    }

    private static SimHost RoundTrip(SimHost host, string slot)
    {
        FactorySaveStore.Save(slot, host);
        Assert.True(FactorySaveStore.TryLoad(Content, slot, out var loaded, out var error), error);
        return loaded!;
    }

    [Fact]
    public void SaveMidConstructionKeepsChecksum()
    {
        var factory = NewFactory(new GameRules(RefundPercent: 50));
        var host = new SimHost(factory);
        // First plan in range (miner, ~46 ticks), then nine belts about 40 tiles away.
        host.Submit(new PlaceBuilding(Player, BuildingKind.Miner, new GridPosition(24, 20), Direction.East));
        for (var x = 60; x < 69; x++)
        {
            host.Submit(new PlaceBelt(Player, new GridPosition(x, 20), Direction.East, "conveyor-basic"));
        }

        host.AdvanceTicks(20);
        var site = Assert.Single(factory.Sites);
        Assert.InRange(site.Progress, 0.1f, 0.9f);
        Assert.Equal(10, factory.PlansOf(Player).Count);

        var loaded = RoundTrip(host, "v11-cantiere");
        Assert.Equal(host.Checksum(), loaded.Checksum());
        var reread = Snapshot.Read(Content, loaded.CreateSnapshot());
        Assert.Equal(new GameRules(RefundPercent: 50), reread.Rules);
        Assert.Equal(factory.State.NextEntityId, reread.NextEntityId);

        // The miner is done after about 46 ticks; then the drone flies towards the first belt.
        host.AdvanceTicks(35);
        var drone = factory.Builders[0];
        Assert.Empty(factory.Sites);
        Assert.True(drone.X > 22f && drone.X < 58f, $"drone x={drone.X}");
        var inFlight = RoundTrip(host, "v11-volo");
        Assert.Equal(host.Checksum(), inFlight.Checksum());
        Assert.Equal(9, Snapshot.Read(Content, inFlight.CreateSnapshot()).Builders[0].Plans.Count);

        // Missing is transient: after more ticks the restored hosts still match the original.
        loaded.AdvanceTicks(35);
        Assert.Equal(host.Checksum(), loaded.Checksum());
        loaded.AdvanceTicks(600);
        inFlight.AdvanceTicks(600);
        host.AdvanceTicks(600);
        Assert.Equal(host.Checksum(), loaded.Checksum());
        Assert.Equal(host.Checksum(), inFlight.Checksum());
    }

    [Fact]
    public void SnapshotRoundTripWithSites()
    {
        var factory = NewFactory(new GameRules(RefundPercent: 75));
        var host = new SimHost(factory);
        var miner = new GridPosition(24, 20);
        host.Submit(new PlaceBuilding(Player, BuildingKind.Miner, miner, Direction.East));
        host.AdvanceTicks(80);
        Assert.Single(factory.Miners);

        // Taking the miner apart, with two more plans queued behind it, halfway through.
        host.Submit(new Remove(Player, miner));
        host.Submit(new PlaceBelt(Player, new GridPosition(60, 20), Direction.East, "conveyor-basic"));
        host.Submit(new PlaceBuilding(Player, BuildingKind.Bridge, new GridPosition(24, 28), Direction.East));
        host.AdvanceTicks(15);
        var apart = Assert.Single(factory.Sites);
        Assert.True(apart.Deconstructing);
        Assert.InRange(apart.Progress, 0.05f, 0.95f);
        AssertRoundTrip(host, factory);

        // Later the drone is on the bridge: a site whose cells are the two ends.
        host.AdvanceTicks(150);
        var bridge = Assert.Single(factory.Sites, s => s.Spec.Kind == BuildingKind.Bridge);
        Assert.Equal(2, bridge.Tiles.Count);
        AssertRoundTrip(host, factory);
    }

    private static void AssertRoundTrip(SimHost host, Factory factory)
    {
        var bytes = host.CreateSnapshot();
        var back = Snapshot.Read(Content, bytes);
        Assert.Equal(bytes, Snapshot.Write(back));

        Assert.Equal(factory.Sites.Count, back.Sites.Count);
        for (var i = 0; i < back.Sites.Count; i++)
        {
            var was = factory.Sites[i];
            var now = back.Sites[i];
            Assert.Equal(was.Id, now.Id);
            Assert.Equal(was.Spec, now.Spec);
            Assert.Equal(was.Tiles, now.Tiles);
            Assert.Equal(was.Cost, now.Cost);
            Assert.Equal(was.Spent, now.Spent);
            Assert.Equal(was.Refunded, now.Refunded);
            Assert.Equal(was.Progress, now.Progress);
            Assert.Equal(was.Deconstructing, now.Deconstructing);
            Assert.Equal(was.RefundBasisPoints, now.RefundBasisPoints);
            Assert.Equal(was.OwnerPlayerId, now.OwnerPlayerId);
        }

        var drone = factory.Builders[0];
        var droneBack = back.Builders[0];
        Assert.Equal(drone.Id, droneBack.Id);
        Assert.Equal(drone.X, droneBack.X);
        Assert.Equal(drone.Y, droneBack.Y);
        Assert.Equal(drone.Rotation, droneBack.Rotation);
        Assert.Equal(drone.CurrentSiteId, droneBack.CurrentSiteId);
        Assert.Equal(drone.Plans, droneBack.Plans);
        Assert.Equal(factory.State.NextEntityId, back.NextEntityId);
    }

    /// <summary>Host with one site of <paramref name="kind"/> at (24, 20) worked on for <paramref name="ticks"/> ticks.</summary>
    private static (Factory Factory, SimHost Host) WithSite(BuildingKind kind, int ticks, string? recipeId = null)
    {
        var factory = NewFactory(new GameRules());
        factory.Wallet.AddMaterial("copper-wire", 1000);
        var host = new SimHost(factory);
        host.Submit(new PlaceBuilding(Player, kind, new GridPosition(24, 20), Direction.East, recipeId));
        host.Submit(new PlaceBelt(Player, new GridPosition(60, 20), Direction.East, "conveyor-basic"));
        host.AdvanceTicks(ticks);
        var site = Assert.Single(factory.Sites);
        Assert.InRange(site.Progress, 0.05f, 0.95f);
        return (factory, host);
    }

    [Fact]
    public void StalledTicksSurviveASave()
    {
        var factory = NewFactory(new GameRules());
        var host = new SimHost(factory);
        // No copper wire: the advanced miner stalls at once and goes to the back after 60 ticks.
        host.Submit(new PlaceBuilding(Player, BuildingKind.MinerAdvanced, new GridPosition(24, 20), Direction.East));
        host.Submit(new PlaceBelt(Player, new GridPosition(23, 24), Direction.East, "conveyor-basic"));
        host.AdvanceTicks(40);
        Assert.Equal(new GridPosition(24, 20), factory.Builders[0].Plans[0].Spec.Origin);

        var loaded = RoundTrip(host, "v11-fermo");
        Assert.Equal(host.Checksum(), loaded.Checksum());
        host.AdvanceTicks(30);
        loaded.AdvanceTicks(30);

        Assert.Equal(new GridPosition(23, 24), factory.Builders[0].Plans[0].Spec.Origin);
        Assert.Equal(host.Checksum(), loaded.Checksum());
    }

    [Fact]
    public void RestoreClampsSiteValuesAndRebuildsEmptyCells()
    {
        var (factory, _) = WithSite(BuildingKind.Miner, 20);
        var data = factory.Capture();
        var dto = data.Sites[0];
        dto.Progress = 7f;
        dto.RefundBasisPoints = 99_999;
        dto.Tiles = [];
        dto.SpentByItem = [new CostLineSaveDto { ItemId = Iron, Amount = 999 }];
        dto.RefundedByItem = [new CostLineSaveDto { ItemId = Iron, Amount = -5 }];

        var site = Assert.Single(Factory.Restore(Content, data).Sites);

        Assert.Equal(1f, site.Progress);
        Assert.Equal(Construction.FullRefundBasisPoints, site.RefundBasisPoints);
        Assert.Equal([4], site.Spent);
        Assert.Equal([0], site.Refunded);
        Assert.Equal(Miner.Size * Miner.Size, site.Tiles.Count);
        Assert.Contains(new GridPosition(25, 21), site.Tiles);

        dto.Progress = -3f;
        dto.RefundBasisPoints = -1;
        var again = Assert.Single(Factory.Restore(Content, data).Sites);
        Assert.Equal(0f, again.Progress);
        Assert.Equal(0, again.RefundBasisPoints);
    }

    [Fact]
    public void RestoreDropsSiteWhoseSpecIsNoLongerValid()
    {
        var (factory, _) = WithSite(BuildingKind.Smelter, 20);
        var spent = factory.Sites[0].Spent[0];
        Assert.True(spent > 0);
        var ironBefore = factory.Wallet.MaterialCount(Iron);
        var data = factory.Capture();
        // A recipe that a content update removed: the site could never finish.
        data.Sites[0].Spec.RecipeId = "ricetta-sparita";
        data.Builders[0].Plans[0].Spec.RecipeId = "ricetta-sparita";

        var restored = Factory.Restore(Content, data);

        Assert.Empty(restored.Sites);
        var drone = restored.Builders[0];
        Assert.Null(drone.CurrentSiteId);
        var next = Assert.Single(drone.Plans);
        Assert.Equal(new GridPosition(60, 20), next.Spec.Origin);
        Assert.Equal(ironBefore + spent, restored.Wallet.MaterialCount(Iron));
    }

    [Fact]
    public void RestoreClearsACurrentSiteThatDoesNotExist()
    {
        var factory = NewFactory(new GameRules());
        var host = new SimHost(factory);
        host.Submit(new PlaceBelt(Player, new GridPosition(60, 20), Direction.East, "conveyor-basic"));
        host.AdvanceTicks(1);
        var data = factory.Capture();
        data.Builders[0].CurrentSiteId = 12_345;

        var drone = Factory.Restore(Content, data).Builders[0];

        Assert.Null(drone.CurrentSiteId);
        Assert.Single(drone.Plans);
    }

    [Fact]
    public void SpentIsSavedPerItemAndMappedByItemId()
    {
        var (factory, _) = WithSite(BuildingKind.MinerAdvanced, 30);
        var site = factory.Sites[0];
        Assert.Equal([new CostEntry("copper-wire", 4), new CostEntry(Iron, 10)], site.Cost);
        var data = factory.Capture();
        var dto = data.Sites[0];

        // Written per item, in ordinal order; the old index lists are no longer written.
        Assert.Equal(
            [("copper-wire", site.Spent[0]), (Iron, site.Spent[1])],
            dto.SpentByItem!.Select(l => (l.ItemId, l.Amount)).ToList());
        Assert.Null(dto.Spent);
        Assert.Null(dto.Refunded);

        // Lines in another order, plus one the block no longer costs: mapped by item id, the extra dropped.
        dto.SpentByItem =
        [
            new CostLineSaveDto { ItemId = Iron, Amount = 3 },
            new CostLineSaveDto { ItemId = "gold", Amount = 7 },
            new CostLineSaveDto { ItemId = "copper-wire", Amount = 1 }
        ];
        Assert.Equal([1, 3], Assert.Single(Factory.Restore(Content, data).Sites).Spent);
    }

    [Fact]
    public void SpentFromAnOlderV11SaveIsMappedByIndex()
    {
        var (factory, _) = WithSite(BuildingKind.MinerAdvanced, 30);
        var data = factory.Capture();
        var dto = data.Sites[0];
        // As written before the per-item lines existed.
        var legacy = JsonSerializer.Deserialize<SiteSaveDto>(
            """{ "spent": [1, 3], "refunded": [0, 1] }""", FactorySaveStore.JsonOptions)!;
        Assert.Null(legacy.SpentByItem);
        dto.SpentByItem = null;
        dto.RefundedByItem = null;
        dto.Spent = legacy.Spent;
        dto.Refunded = legacy.Refunded;

        var site = Assert.Single(Factory.Restore(Content, data).Sites);

        Assert.Equal([1, 3], site.Spent);
        Assert.Equal([0, 1], site.Refunded);
    }

    [Fact]
    public void Version10SaveIsRejectedWithClearMessage()
    {
        var host = new SimHost(NewFactory(new GameRules()));
        FactorySaveStore.Save("v10-cantiere", host);
        var path = FactorySaveStore.SlotPath("v10-cantiere");
        File.WriteAllText(path, File.ReadAllText(path).Replace($"\"version\": {FactorySaveData.CurrentVersion}", "\"version\": 10"));

        Assert.False(FactorySaveStore.TryLoad(Content, "v10-cantiere", out var loaded, out var error));
        Assert.Null(loaded);
        Assert.Equal("Salvataggio di una versione precedente: non compatibile.", error);
    }
    [Fact]
    public void ReplacementSiteSurvivesSaveAndLoad()
    {
        var factory = NewFactory(new GameRules());
        factory.Wallet.AddMaterial("copper-wire", 100);
        var cell = new GridPosition(24, 21);
        Assert.True(factory.PlaceInstant(new BuildSpec("conveyor-basic", null, cell, Direction.East), charge: false));
        factory.Belts.Cells[cell].RestoreState(0, [new TransportedItem(77, Iron, 1f)]);
        var host = new SimHost(factory);
        host.Submit(new PlaceBelt(Player, cell, Direction.East, "conveyor-fast"));
        for (var t = 0; t < 600 && (factory.Sites.Count == 0 || factory.Sites[0].Progress < 0.3f); t++)
        {
            host.AdvanceTicks(1);
        }

        var site = Assert.Single(factory.Sites);
        Assert.InRange(site.Progress, 0.3f, 0.9f);
        Assert.NotNull(site.Replaced);

        var loaded = RoundTrip(host, "v13-sostituzione");
        Assert.Contains("\"version\": 13", File.ReadAllText(FactorySaveStore.SlotPath("v13-sostituzione")));
        Assert.Equal(host.Checksum(), loaded.Checksum());
        var restored = Assert.Single(loaded.State.Sites);
        Assert.Equal("conveyor-basic", restored.Replaced!.Spec.StructureId);
        Assert.Equal(77, Assert.Single(restored.Replaced.Items).Id);

        host.AdvanceTicks(600);
        loaded.AdvanceTicks(600);
        Assert.Empty(loaded.State.Sites);
        Assert.Equal("conveyor-fast", loaded.State.Belts[cell].Definition.Id);
        Assert.Equal(77, Assert.Single(loaded.State.Belts[cell].Items).Id);
        Assert.Equal(host.Checksum(), loaded.Checksum());
    }

    [Fact]
    public void Version12SaveStillLoads()
    {
        var host = new SimHost(NewFactory(new GameRules()));
        host.Submit(new PlaceBuilding(Player, BuildingKind.Miner, new GridPosition(24, 20), Direction.East));
        host.AdvanceTicks(20);
        Assert.Single(host.State.Sites);
        var json = System.Text.Encoding.UTF8.GetString(host.CreateSnapshot());
        Assert.DoesNotContain("replaced", json);
        var v12 = json.Replace($"\"version\":{FactorySaveData.CurrentVersion}", "\"version\":12");
        Assert.Contains("\"version\":12", v12);

        var state = Snapshot.Read(Content, System.Text.Encoding.UTF8.GetBytes(v12));

        Assert.Equal(host.Checksum(), Snapshot.Checksum(Snapshot.Write(state)));
        Assert.Null(Assert.Single(state.Sites).Replaced);
    }
}
