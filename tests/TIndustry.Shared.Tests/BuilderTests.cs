using System.Globalization;
using System.Text;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Real construction through <see cref="SimHost"/> (<c>InstantBuild = false</c>): placing queues a plan on
/// the player's drone, the drone flies within range, the site takes the cost gradually and finishes;
/// Remove cancels plans and sites or queues a breaking plan.
/// </summary>
public sealed class BuilderTests
{
    private const string Iron = "iron-plate";
    private const int Player = 0;

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    /// <summary>Core 2×2 at (20, 20): the drone starts at its centre (21, 21).</summary>
    private static readonly GridPosition CoreOrigin = new(20, 20);

    private static readonly GridPosition NearMiner = new(24, 20);

    private static Factory NewFactory(
        int ironPlates, int refundPercent = 100, bool instantBuild = false, bool unlockAll = true)
    {
        var wallet = new EconomyWallet(
            0,
            new Dictionary<string, int>(StringComparer.Ordinal) { [Iron] = ironPlates });
        var factory = new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(CoreOrigin, 2), wallet);
        factory.State.Rules = new GameRules(refundPercent, instantBuild);
        if (unlockAll)
        {
            // Not UnlockAllForDemo: it also tops up the wallet.
            foreach (var id in ResearchState.DemoStructureIds)
            {
                factory.Research.ForceUnlock(id);
            }
        }

        factory.AddBuilder(Player);
        return factory;
    }

    private static PlaceBuilding PlaceMiner(GridPosition origin) =>
        new(Player, BuildingKind.Miner, origin, Direction.East);

    private static PlaceBelt Belt(GridPosition tile) =>
        new(Player, tile, Direction.East, "conveyor-basic");

    private static BuilderUnit Drone(Factory factory) => factory.State.Builders[0];

    private static float DistanceTo(BuilderUnit drone, float x, float y) =>
        MathF.Sqrt((x - drone.X) * (x - drone.X) + (y - drone.Y) * (y - drone.Y));

    /// <summary>Runs single ticks until <paramref name="done"/>; returns how many ran.</summary>
    private static int RunUntil(SimHost host, Func<bool> done, int maxTicks = 20_000)
    {
        for (var tick = 1; tick <= maxTicks; tick++)
        {
            host.AdvanceTicks(1);
            if (done())
            {
                return tick;
            }
        }

        throw new InvalidOperationException("condizione mai raggiunta");
    }

    [Fact]
    public void NewGameSpawnsDroneAtCoreCentre()
    {
        var factory = NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120);

        var drone = Assert.Single(factory.Builders);
        Assert.Equal(Player, drone.OwnerPlayerId);
        Assert.Equal(61f, drone.X);
        Assert.Equal(61f, drone.Y);
        Assert.Empty(drone.Plans);
        Assert.Null(drone.CurrentSiteId);
    }

    [Fact]
    public void IdleDroneFliesToRestPointAndStays()
    {
        var factory = NewFactory(ironPlates: 0);
        var host = new SimHost(factory);

        host.AdvanceTicks(120);

        var drone = Drone(factory);
        // One tile west of the Core, middle of its west side: cell (19, 20).
        Assert.Equal(19.5f, drone.X);
        Assert.Equal(20.5f, drone.Y);
        var rotation = drone.Rotation;
        host.AdvanceTicks(10);
        Assert.Equal(rotation, drone.Rotation);
    }

    [Fact]
    public void PlaceBuildingQueuesPlanNotBuilding()
    {
        var factory = NewFactory(ironPlates: 10);
        var host = new SimHost(factory);
        host.Submit(PlaceMiner(NearMiner));

        host.AdvanceTicks(1);

        Assert.Empty(factory.Miners);
        var plan = Assert.Single(factory.PlansOf(Player));
        Assert.False(plan.Breaking);
        Assert.Equal(NearMiner, plan.Spec.Origin);
        Assert.Single(factory.Sites); // in range: work began on the first tick

        var ticks = 1 + RunUntil(host, () => factory.Miners.Count == 1);

        var expected = (int)Math.Ceiling((20 + 4 * 0.7) / 0.5); // 46, no flight
        Assert.InRange(ticks, expected - 1, expected + 1);
        Assert.Equal(6, factory.Wallet.MaterialCount(Iron));
        Assert.Empty(factory.PlansOf(Player));
        Assert.Empty(factory.Sites);
        Assert.Null(Drone(factory).CurrentSiteId);
    }

    [Fact]
    public void DroneFliesToFarPlanBeforeBuilding()
    {
        var factory = NewFactory(ironPlates: 10);
        var host = new SimHost(factory);
        // Miner centre (61, 21): exactly 40 tiles east of the drone at (21, 21).
        var origin = new GridPosition(60, 20);
        host.Submit(PlaceMiner(origin));
        var drone = Drone(factory);
        var workRange = Content.Drone.BuildRange - 1f; // 26.5

        var started = 0;
        for (var tick = 1; tick <= 200 && started == 0; tick++)
        {
            host.AdvanceTicks(1);
            if (factory.Sites.Count == 0)
            {
                Assert.True(DistanceTo(drone, 61f, 21f) > workRange);
                Assert.Null(drone.CurrentSiteId);
            }
            else
            {
                started = tick;
            }
        }

        // (40 − 26.5) / 22.5 s = 0.6 s = 36 ticks of flight.
        var expectedFlight = (int)Math.Round((40 - 26.5) / 22.5 * Simulation.TickRate);
        Assert.InRange(started, expectedFlight - 1, expectedFlight + 1);
        Assert.True(DistanceTo(drone, 61f, 21f) <= workRange);
        Assert.Equal(0f, drone.Rotation); // flew along +X
        Assert.Equal(21f, drone.Y);

        var (x, y) = (drone.X, drone.Y);
        RunUntil(host, () => factory.Miners.Count == 1);
        Assert.Equal((x, y), (drone.X, drone.Y)); // stayed put while building
    }

    [Fact]
    public void FiftyDraggedBeltsWaitForMaterialsThenFinishInOrder()
    {
        var factory = NewFactory(ironPlates: 0);
        var host = new SimHost(factory);
        var rejected = new List<string>();
        host.CommandRejected += (_, reason) => rejected.Add(reason);
        var finished = new List<long>();
        host.EventRaised += e =>
        {
            if (e is ConstructFinished done)
            {
                finished.Add(done.SiteId);
            }
        };
        var tiles = Enumerable.Range(0, 50).Select(i => new GridPosition(22 + i, 24)).ToList();
        foreach (var tile in tiles)
        {
            host.Submit(Belt(tile));
        }

        host.AdvanceTicks(600);

        Assert.Empty(rejected);
        Assert.Empty(factory.Belts.Cells);
        Assert.Equal(50, factory.PlansOf(Player).Count);
        var stalled = Assert.Single(factory.Sites);
        Assert.Equal(new CostEntry(Iron, 1), stalled.Missing);

        factory.Wallet.AddMaterial(Iron, 50);
        var siteOrigins = new Dictionary<long, GridPosition>();
        RunUntil(host, () =>
        {
            foreach (var site in factory.Sites)
            {
                siteOrigins[site.Id] = site.Spec.Origin;
            }

            return factory.Belts.Count == 50 && factory.Sites.Count == 0;
        });

        Assert.Equal(tiles, finished.Select(id => siteOrigins[id]).ToList());
        Assert.Equal(0, factory.Wallet.MaterialCount(Iron));
        Assert.Empty(factory.PlansOf(Player));
    }

    [Fact]
    public void PlanOnPlannedCellsIsRejected()
    {
        var factory = NewFactory(ironPlates: 10);
        var host = new SimHost(factory);
        var rejected = new List<(SimCommand Command, string Reason)>();
        host.CommandRejected += (c, r) => rejected.Add((c, r));
        var far = new GridPosition(90, 21);

        host.Submit(Belt(far));
        host.Submit(Belt(far));
        host.Submit(PlaceMiner(new GridPosition(89, 20))); // its 2×2 covers the planned belt cell
        host.AdvanceTicks(1);

        Assert.Equal(2, rejected.Count);
        Assert.All(rejected, r => Assert.Equal(CommandReasons.Occupied, r.Reason));
        Assert.Single(factory.PlansOf(Player));
        Assert.Empty(factory.Sites);
    }

    [Fact]
    public void PlanDroppedWhenCellsTakenBeforeArrival()
    {
        var factory = NewFactory(ironPlates: 10);
        var host = new SimHost(factory);
        var dropped = new List<PlanDropped>();
        host.EventRaised += e =>
        {
            if (e is PlanDropped d)
            {
                dropped.Add(d);
            }
        };
        var farMiner = new GridPosition(80, 20);
        var next = new GridPosition(82, 21);
        host.Submit(PlaceMiner(farMiner));
        host.Submit(Belt(next));
        host.AdvanceTicks(1);

        // Someone else builds on the planned cells while the drone is still flying.
        Assert.True(factory.PlaceInstant(new BuildSpec("conveyor-basic", null, new GridPosition(81, 21), Direction.East), charge: false));

        RunUntil(host, () => dropped.Count > 0);

        var drop = Assert.Single(dropped);
        Assert.Equal(Player, drop.PlayerId);
        Assert.Equal(farMiner, drop.Plan.Spec.Origin);
        Assert.Equal(CommandReasons.Occupied, drop.Reason);
        var remaining = Assert.Single(factory.PlansOf(Player));
        Assert.Equal(next, remaining.Spec.Origin);

        RunUntil(host, () => factory.Belts.Contains(next));
        Assert.Empty(factory.Miners);
        Assert.Equal(9, factory.Wallet.MaterialCount(Iron));
    }

    [Fact]
    public void RemoveOnSiteUnderConstructionRefundsSpent()
    {
        var factory = NewFactory(ironPlates: 10);
        var host = new SimHost(factory);
        var finished = new List<SimEvent>();
        host.EventRaised += finished.Add;
        host.Submit(PlaceMiner(NearMiner));
        host.AdvanceTicks(23);

        var site = Assert.Single(factory.Sites);
        Assert.InRange(site.Progress, 0.4f, 0.6f);
        Assert.True(factory.Wallet.MaterialCount(Iron) < 10);

        host.Submit(new Remove(Player, new GridPosition(NearMiner.X + 1, NearMiner.Y + 1)));
        RunUntil(host, () => factory.Sites.Count == 0);

        Assert.Equal(10, factory.Wallet.MaterialCount(Iron));
        Assert.Empty(factory.Miners);
        Assert.Empty(factory.PlansOf(Player));
        Assert.Null(Drone(factory).CurrentSiteId);
        var done = Assert.IsType<DeconstructFinished>(Assert.Single(finished));
        Assert.Equal(site.Id, done.SiteId);

        // Nothing left behind: a later tick changes nothing.
        host.AdvanceTicks(60);
        Assert.Equal(10, factory.Wallet.MaterialCount(Iron));
        Assert.Empty(factory.Miners);
    }

    [Fact]
    public void RemoveOnQueuedPlanCancelsIt()
    {
        var factory = NewFactory(ironPlates: 10);
        var host = new SimHost(factory);
        var applied = new List<SimCommand>();
        host.CommandApplied += applied.Add;
        var far = new GridPosition(90, 20);
        host.Submit(PlaceMiner(far));
        host.AdvanceTicks(1);

        host.Submit(new Remove(Player, new GridPosition(far.X + 1, far.Y))); // any cell of the 2×2
        host.AdvanceTicks(1);

        Assert.Equal(2, applied.Count);
        Assert.Empty(factory.PlansOf(Player));
        Assert.Empty(factory.Sites);
        Assert.Equal(10, factory.Wallet.MaterialCount(Iron));
        host.AdvanceTicks(600);
        Assert.Empty(factory.Miners);
        Assert.Equal(10, factory.Wallet.MaterialCount(Iron));
    }

    [Fact]
    public void PlanTilesCoverBlockOrBreakingTarget()
    {
        var factory = NewFactory(ironPlates: 0);
        IGameView view = factory;
        var smelterOrigin = new GridPosition(30, 20);
        Assert.True(factory.PlaceInstant(
            new BuildSpec(Crafter.SmelterBuildingId, BuildingKind.Smelter, smelterOrigin, Direction.East), charge: false));
        var minerPlan = new BuildPlan(BuildSpec.FromCommand(PlaceMiner(NearMiner)), Breaking: false);
        var clicked = new GridPosition(smelterOrigin.X + 1, smelterOrigin.Y + 1);
        var breakPlan = new BuildPlan(
            new BuildSpec(Crafter.SmelterBuildingId, BuildingKind.Smelter, clicked, Direction.East), Breaking: true);
        var emptyBreak = new BuildPlan(BuildSpec.FromCommand(Belt(new GridPosition(40, 40))), Breaking: true);

        var minerTiles = view.PlanTiles(minerPlan);
        var breakTiles = view.PlanTiles(breakPlan);

        Assert.Equal(Miner.Size * Miner.Size, minerTiles.Count);
        Assert.Contains(NearMiner, minerTiles);
        Assert.Equal(Crafter.Size * Crafter.Size, breakTiles.Count); // the whole smelter, not the clicked cell
        Assert.Contains(smelterOrigin, breakTiles);
        Assert.Equal([new GridPosition(40, 40)], view.PlanTiles(emptyBreak));
    }

    [Fact]
    public void BreakingPlanDeconstructsWithRuleRefund()
    {
        var factory = NewFactory(ironPlates: 0, refundPercent: 50);
        var host = new SimHost(factory);
        var origin = new GridPosition(24, 20);
        Assert.True(factory.PlaceInstant(
            new BuildSpec(Crafter.SmelterBuildingId, BuildingKind.Smelter, origin, Direction.East), charge: false));
        var smelter = Assert.Single(factory.Smelters);
        Assert.True(smelter.TryAccept("iron-ore"));
        Assert.True(smelter.TryAccept("iron-ore"));
        var events = new List<SimEvent>();
        host.EventRaised += events.Add;

        host.Submit(new Remove(Player, origin));
        host.Submit(new Remove(Player, new GridPosition(origin.X + 1, origin.Y))); // same building: no second plan
        host.AdvanceTicks(1);

        var plan = Assert.Single(factory.PlansOf(Player));
        Assert.True(plan.Breaking);
        Assert.Equal(origin, plan.Spec.Origin);

        RunUntil(host, () => events.Count > 0);

        Assert.IsType<DeconstructFinished>(Assert.Single(events));
        Assert.Empty(factory.Smelters);
        Assert.Empty(factory.Sites);
        Assert.Empty(factory.PlansOf(Player));
        Assert.Equal(3, factory.Wallet.MaterialCount(Iron)); // floor(6 × 50%)
        Assert.Equal(0, factory.Wallet.MaterialCount("iron-ore")); // items inside are lost
    }

    [Fact]
    public void ItemsWaitBeforeBeltSite()
    {
        var factory = NewFactory(ironPlates: 0);
        var host = new SimHost(factory);
        var y = 30;
        foreach (var x in new[] { 25, 26, 27, 29, 30 })
        {
            Assert.True(factory.PlaceInstant(
                new BuildSpec("conveyor-basic", null, new GridPosition(x, y), Direction.East), charge: false));
        }

        var nextId = 1000L;
        foreach (var x in new[] { 25, 26, 27 })
        {
            Assert.True(factory.Belts.TryInsert(new GridPosition(x, y), new TransportedItem(nextId++, "iron-ore")));
        }

        int ItemsOn(int x) => factory.Belts.TryGet(new GridPosition(x, y), out var cell) ? cell.Items.Count : 0;
        int TotalItems() => factory.Belts.Cells.Values.Sum(c => c.Items.Count);
        var gap = new GridPosition(28, y);
        host.Submit(Belt(gap)); // no materials: the site stalls at 0

        host.AdvanceTicks(600);

        Assert.Single(factory.Sites);
        Assert.Equal(3, TotalItems());
        Assert.Equal(3, ItemsOn(25) + ItemsOn(26) + ItemsOn(27)); // queued upstream of the site
        Assert.True(factory.Belts.TryGet(new GridPosition(27, y), out var front));
        Assert.Equal(1f, front.Items.Max(i => i.Progress)); // the first one waits at the edge
        Assert.Equal(0, ItemsOn(29) + ItemsOn(30));

        factory.Wallet.AddMaterial(Iron, 1);
        RunUntil(host, () => factory.Belts.Contains(gap));
        host.AdvanceTicks(600);

        Assert.Equal(3, TotalItems());
        Assert.Equal(0, ItemsOn(25) + ItemsOn(26) + ItemsOn(27)); // the finished belt lets them through
    }

    [Fact]
    public void PlacementDoesNotCheckResources()
    {
        var factory = NewFactory(ironPlates: 0);
        var host = new SimHost(factory);
        var applied = new List<SimCommand>();
        host.CommandApplied += applied.Add;

        host.Submit(PlaceMiner(NearMiner));
        host.AdvanceTicks(1);

        Assert.Single(applied);
        Assert.Single(factory.PlansOf(Player));
        Assert.Empty(factory.Miners);
    }

    [Fact]
    public void LockedStructureIsRejectedAtPlacement()
    {
        var factory = NewFactory(ironPlates: 10, unlockAll: false);
        var host = new SimHost(factory);
        var rejected = new List<string>();
        host.CommandRejected += (_, r) => rejected.Add(r);

        host.Submit(new PlaceBuilding(Player, BuildingKind.Generator, NearMiner, Direction.East));
        host.AdvanceTicks(1);

        Assert.Equal([CommandReasons.Locked("Generatore")], rejected);
        Assert.Empty(factory.PlansOf(Player));
    }

    [Fact]
    public void InstantBuildKeepsTodayBehaviour()
    {
        var factory = NewFactory(ironPlates: 6, instantBuild: true);
        var host = new SimHost(factory);
        var rejected = new List<string>();
        host.CommandRejected += (_, r) => rejected.Add(r);

        host.Submit(PlaceMiner(NearMiner));
        host.AdvanceTicks(1);

        Assert.Single(factory.Miners);
        Assert.Empty(factory.Sites);
        Assert.Empty(factory.PlansOf(Player));
        Assert.Equal(2, factory.Wallet.MaterialCount(Iron));

        var need = factory.FormatNeedMessage(Miner.BasicId);
        host.Submit(PlaceMiner(new GridPosition(30, 20)));
        host.AdvanceTicks(1);

        var expected = string.IsNullOrEmpty(need) ? CommandReasons.InsufficientResources : need;
        Assert.Equal([expected], rejected);
        Assert.Single(factory.Miners);
        Assert.Equal(2, factory.Wallet.MaterialCount(Iron));

        host.Submit(new Remove(Player, NearMiner));
        host.AdvanceTicks(1);
        Assert.Empty(factory.Miners);
        Assert.Equal(6, factory.Wallet.MaterialCount(Iron));
    }

    [Fact]
    public void EventHandlerExceptionsAreAggregatedAfterTheFrame()
    {
        var factory = NewFactory(ironPlates: 10);
        var host = new SimHost(factory);
        var seen = new List<SimEvent>();
        host.EventRaised += _ => throw new InvalidOperationException("gestore rotto");
        host.EventRaised += seen.Add;
        host.Submit(PlaceMiner(NearMiner));

        var error = Assert.Throws<AggregateException>(() => host.AdvanceTicks(200));

        Assert.IsType<InvalidOperationException>(Assert.Single(error.InnerExceptions));
        Assert.IsType<ConstructFinished>(Assert.Single(seen));
        Assert.Equal(200, factory.CurrentTick); // every tick ran
        Assert.Empty(factory.State.Events);
    }

    /// <summary>Core 2×2 at (60, 49): the drone starts at (61, 50), west of the bridges below.</summary>
    private static Factory NewBridgeFactory()
    {
        var wallet = new EconomyWallet(
            1_000,
            new Dictionary<string, int>(StringComparer.Ordinal) { [Iron] = 200, ["copper-wire"] = 200 });
        var factory = new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(new GridPosition(60, 49), 2), wallet);
        factory.State.Rules = new GameRules(InstantBuild: false);
        foreach (var id in ResearchState.DemoStructureIds)
        {
            factory.Research.ForceUnlock(id);
        }

        factory.AddBuilder(Player);
        return factory;
    }

    /// <summary>Belts on the cells between the ends, so the bridge's exit lands 5 cells away.</summary>
    private static void FillSpan(Factory factory, params int[] xs)
    {
        foreach (var x in xs)
        {
            Assert.True(factory.PlaceInstant(
                new BuildSpec("conveyor-basic", null, new GridPosition(x, 50), Direction.East), charge: false));
        }
    }

    [Fact]
    public void BridgeBrokenFromItsExitEndFinishes()
    {
        var factory = NewBridgeFactory();
        FillSpan(factory, 101, 102, 103, 104);
        var entry = new GridPosition(105, 50);
        var exit = new GridPosition(100, 50);
        Assert.True(factory.PlaceInstant(
            new BuildSpec("conveyor-bridge", BuildingKind.Bridge, entry, Direction.West), charge: false));
        Assert.True(factory.Belts.TryGet(exit, out var exitCell) && exitCell.Kind == LogisticsKind.Bridge);
        var host = new SimHost(factory);
        var events = new List<SimEvent>();
        host.EventRaised += events.Add;

        // Clicked on the exit end, the one nearer to the drone coming from the west.
        host.Submit(new Remove(Player, exit));
        RunUntil(host, () => events.Count > 0, maxTicks: 2_000);

        Assert.IsType<DeconstructFinished>(Assert.Single(events));
        Assert.False(factory.Belts.Contains(entry));
        Assert.False(factory.Belts.Contains(exit));
        Assert.Empty(factory.Sites);
        Assert.Null(Drone(factory).CurrentSiteId);
    }

    [Fact]
    public void BridgeBuiltFarFromTheDroneFinishes()
    {
        var factory = NewBridgeFactory();
        FillSpan(factory, 102, 103, 104);
        var entry = new GridPosition(100, 50);
        var host = new SimHost(factory);
        var events = new List<SimEvent>();
        host.EventRaised += events.Add;

        host.Submit(new PlaceBuilding(Player, BuildingKind.Bridge, entry, Direction.East));
        RunUntil(host, () => events.Count > 0, maxTicks: 2_000);

        Assert.IsType<ConstructFinished>(Assert.Single(events));
        Assert.True(factory.Belts.TryGet(entry, out var cell) && cell.BridgePartner == new GridPosition(105, 50));
        Assert.Empty(factory.Sites);
    }

    [Fact]
    public void DroneWhoseSiteVanishedDropsThePlan()
    {
        var factory = NewFactory(ironPlates: 10);
        var host = new SimHost(factory);
        host.Submit(PlaceMiner(NearMiner));
        host.Submit(Belt(new GridPosition(23, 24)));
        host.AdvanceTicks(5);
        Assert.NotNull(Drone(factory).CurrentSiteId);

        factory.State.Sites.Clear(); // test-only: the site disappears behind the drone's back

        host.AdvanceTicks(1);

        var drone = Drone(factory);
        var next = Assert.Single(drone.Plans);
        Assert.Equal(new GridPosition(23, 24), next.Spec.Origin);
        RunUntil(host, () => factory.Belts.Contains(new GridPosition(23, 24)));
    }

    [Fact]
    public void ViewListsAreReadOnly()
    {
        var factory = NewFactory(ironPlates: 10);
        new SimHost(factory).Submit(PlaceMiner(NearMiner));
        IGameView view = factory;

        Assert.False(view.Builders is List<BuilderUnit>);
        Assert.False(view.Sites is List<ConstructSite>);
        Assert.False(view.Builders[0].Plans is List<BuildPlan>);
        Assert.True(((ICollection<BuilderUnit>)view.Builders).IsReadOnly);
        Assert.True(((ICollection<ConstructSite>)view.Sites).IsReadOnly);
        Assert.True(((ICollection<BuildPlan>)view.Builders[0].Plans).IsReadOnly);
    }

    [Fact]
    public void SameCommandsSameChecksum()
    {
        static (ulong Checksum, string Construction) Run()
        {
            var factory = NewFactory(ironPlates: 20);
            var host = new SimHost(factory);
            for (var i = 0; i < 12; i++)
            {
                host.Submit(Belt(new GridPosition(23 + i, 26)) with { Tick = 1 + i });
            }

            host.Submit(PlaceMiner(new GridPosition(70, 10)) with { Tick = 5 });
            host.Submit(PlaceMiner(NearMiner) with { Tick = 7 });
            host.Submit(new Remove(Player, new GridPosition(25, 26)) with { Tick = 400 });
            host.Submit(new Remove(Player, NearMiner) with { Tick = 2_000 });
            host.Submit(new Sell(Player, Iron, 3) with { Tick = 3_000 });
            host.AdvanceTicks(10_000);

            // Construction really happened: 12 belts − the removed one, the far miner kept, the near one
            // taken apart; plates 20 − 12 − 2×4 + 1 + 4 (refunds) − 3 (sold) = 2.
            Assert.Equal(11, factory.Belts.Count);
            Assert.False(factory.Belts.Contains(new GridPosition(25, 26)));
            Assert.Equal(new GridPosition(70, 10), Assert.Single(factory.Miners).Position);
            Assert.Equal(2, factory.Wallet.MaterialCount(Iron));
            Assert.Empty(factory.Sites);
            Assert.Empty(factory.PlansOf(Player));
            return (host.Checksum(), Fingerprint(factory));
        }

        var a = Run();
        var b = Run();

        Assert.Equal(a, b);
    }

    /// <summary>Drones (exact float bits), queues and sites: not in the snapshot until saves carry them.</summary>
    private static string Fingerprint(Factory factory)
    {
        var sb = new StringBuilder();
        foreach (var d in factory.Builders)
        {
            sb.Append(CultureInfo.InvariantCulture, $"d{d.Id}:{BitConverter.SingleToInt32Bits(d.X)},{BitConverter.SingleToInt32Bits(d.Y)},{BitConverter.SingleToInt32Bits(d.Rotation)},{d.CurrentSiteId};");
            foreach (var p in d.Plans)
            {
                sb.Append(CultureInfo.InvariantCulture, $"p{p.Spec.Origin}{p.Breaking};");
            }
        }

        foreach (var s in factory.Sites)
        {
            sb.Append(CultureInfo.InvariantCulture, $"s{s.Id}:{BitConverter.SingleToInt32Bits(s.Progress)},{string.Join(',', s.Spent)};");
        }

        return sb.ToString();
    }
}
