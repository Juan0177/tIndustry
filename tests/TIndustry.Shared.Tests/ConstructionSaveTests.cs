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

    [Fact]
    public void Version10SaveIsRejectedWithClearMessage()
    {
        var host = new SimHost(NewFactory(new GameRules()));
        FactorySaveStore.Save("v10-cantiere", host);
        var path = FactorySaveStore.SlotPath("v10-cantiere");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"version\": 11", "\"version\": 10"));

        Assert.False(FactorySaveStore.TryLoad(Content, "v10-cantiere", out var loaded, out var error));
        Assert.Null(loaded);
        Assert.Equal("Salvataggio di una versione precedente: non compatibile.", error);
    }
}
