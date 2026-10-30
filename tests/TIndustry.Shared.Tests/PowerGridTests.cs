using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Power network (spec 2026-10-30 «Rete di corrente come Mindustry»): node lasers (automatic and by hand), contact,
/// batteries and the networks they form. The per-network balance is tested with Task 2.
/// </summary>
public sealed class PowerGridTests
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly FactoryContent Content = FactoryContent.Load(Path.Combine(DataDir, "content.json"));

    private static Factory Sandbox(GameRules? rules = null)
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7,
            rules: rules ?? new GameRules(InstantBuild: true));
        foreach (var id in new[]
                 {
                     Generator.BuildingId, PowerNode.Tier1Id, PowerNode.Tier2Id, Lamp.BuildingId, Battery.BuildingId,
                     Crafter.SmelterBuildingId, Crafter.AssemblerBuildingId, Miner.AdvancedId
                 })
        {
            factory.Research.ForceUnlock(id);
        }

        return factory;
    }

    private static long Place(Factory factory, BuildingKind kind, int x, int y)
    {
        var origin = new GridPosition(x, y);
        var spec = new BuildSpec(BuildSpec.StructureIdFor(kind)!, kind, origin, Direction.East);
        Assert.True(factory.PlaceInstant(spec, charge: false), $"{kind} su {origin}");
        Assert.True(factory.TryGetSpecAt(origin, out _, out var id));
        return id;
    }

    private static int NetworkIndex(Factory factory, long id) =>
        (factory.PowerGrid.NetworkOf(id) ?? throw new InvalidOperationException($"{id} fuori rete")).Index;

    // Generator on [X, X+2), five T1 nodes, a forno 30 cells past the generator's edge.
    private const int X = 10;
    private const int Y = 20;

    private static (long Gen, long[] Nodes, long Smelter) Chain(Factory factory)
    {
        var gen = Place(factory, BuildingKind.Generator, X, Y);
        var smelter = Place(factory, BuildingKind.Smelter, X + 32, Y);
        var nodes = new[] { 6, 12, 18, 24, 29 }.Select(dx => Place(factory, BuildingKind.PowerNode, X + dx, Y)).ToArray();
        return (gen, nodes, smelter);
    }

    [Fact]
    public void ChainOfFiveNodesBringsTheGeneratorToAFornoThirtyCellsAway()
    {
        var factory = Sandbox();
        var (gen, nodes, smelter) = Chain(factory);
        var grid = factory.PowerGrid;

        Assert.True(grid.HasLink(nodes[0], gen));
        for (var i = 1; i < nodes.Length; i++)
        {
            Assert.True(grid.HasLink(nodes[i - 1], nodes[i]), $"nodo {i - 1} - nodo {i}");
        }

        Assert.True(grid.HasLink(smelter, nodes[^1]));
        Assert.Equal(6, grid.Links.Count);
        Assert.All(grid.Links, l => Assert.True(l.A < l.B));
        Assert.Equal(grid.Links.OrderBy(l => l.A).ThenBy(l => l.B), grid.Links);

        var network = grid.NetworkOf(gen);
        Assert.NotNull(network);
        Assert.Same(network, grid.NetworkOf(smelter));
        Assert.Equal(new[] { gen, smelter }.Concat(nodes).Order(), network.Members);
        Assert.Single(grid.Networks);
    }

    [Fact]
    public void GeneratorTouchingAFornoIsOneNetworkWithoutLasers()
    {
        var factory = Sandbox();
        var gen = Place(factory, BuildingKind.Generator, X, Y);
        var smelter = Place(factory, BuildingKind.Smelter, X + 2, Y);
        var far = Place(factory, BuildingKind.Smelter, X + 6, Y);

        Assert.Empty(factory.PowerGrid.Links);
        Assert.Equal(NetworkIndex(factory, gen), NetworkIndex(factory, smelter));
        Assert.NotEqual(NetworkIndex(factory, gen), NetworkIndex(factory, far));
        Assert.Equal(2, factory.PowerGrid.Networks.Count);
    }

    [Fact]
    public void FornoTouchingTwoGeneratorsJoinsThemInOneNetwork()
    {
        var factory = Sandbox();
        var left = Place(factory, BuildingKind.Generator, X, Y);
        var right = Place(factory, BuildingKind.Generator, X + 4, Y);
        Assert.NotEqual(NetworkIndex(factory, left), NetworkIndex(factory, right));

        var smelter = Place(factory, BuildingKind.Smelter, X + 2, Y);
        var network = Assert.Single(factory.PowerGrid.Networks);
        Assert.Equal(new[] { left, right, smelter }.Order(), network.Members);
    }

    [Fact]
    public void BlocksOutsideThePowerNetworkHaveNoNetwork()
    {
        var factory = Sandbox();
        var miner = Place(factory, BuildingKind.Miner, X, Y);
        var gen = Place(factory, BuildingKind.Generator, X + 2, Y);
        var advanced = Place(factory, BuildingKind.MinerAdvanced, X + 4, Y);

        Assert.Null(factory.PowerGrid.NetworkOf(miner));
        Assert.Equal(NetworkIndex(factory, gen), NetworkIndex(factory, advanced));
        Assert.Null(factory.PowerGrid.NetworkOf(987654));
    }

    [Fact]
    public void BatteryIsAPowerBlockBuiltWithItsCost()
    {
        var factory = Sandbox();
        var host = new SimHost(factory);
        factory.Wallet.AddMaterial("lead-plate", 30);
        factory.Wallet.AddMaterial("copper-wire", 10);
        var lead = factory.Wallet.MaterialCount("lead-plate");
        var wire = factory.Wallet.MaterialCount("copper-wire");
        var gen = Place(factory, BuildingKind.Generator, X, Y);

        host.Submit(new PlaceBuilding(1, BuildingKind.Battery, new GridPosition(X + 2, Y), Direction.East));
        host.AdvanceTicks(1);

        var battery = Assert.Single(factory.Batteries);
        Assert.Equal(new GridPosition(X + 2, Y), battery.Position);
        Assert.Equal(lead - 10, factory.Wallet.MaterialCount("lead-plate"));
        Assert.Equal(wire - 4, factory.Wallet.MaterialCount("copper-wire"));
        Assert.True(factory.TryGetSpecAt(battery.Position, out var spec, out var id));
        Assert.Equal(new BuildSpec(Battery.BuildingId, BuildingKind.Battery, battery.Position, Direction.East), spec);
        Assert.Equal(battery.Id, id);
        Assert.Equal(NetworkIndex(factory, gen), NetworkIndex(factory, battery.Id));

        // Saved and restored with its id.
        var restored = Factory.Restore(Content, factory.Capture());
        Assert.Equal(battery.Id, Assert.Single(restored.Batteries).Id);
        Assert.Equal(NetworkIndex(restored, gen), NetworkIndex(restored, battery.Id));

        Assert.True(factory.RemoveInstant(battery.Position, refund: false));
        Assert.Empty(factory.Batteries);
        Assert.Null(factory.PowerGrid.NetworkOf(battery.Id));
    }

    // A T1 node in the middle of isolated lamps at growing edge distances (gap = cells between the two blocks).
    private const int CX = 30;
    private const int CY = 30;

    [Fact]
    public void AutomaticLinksGoToTheNearestUpToTheLimitAndTiesToTheLowerId()
    {
        var factory = Sandbox();
        // Placed far ones first, so the ids do not follow the distance.
        var outOfRange = Place(factory, BuildingKind.Lamp, CX, CY - 8);    // gap 7
        var tieLow = Place(factory, BuildingKind.Lamp, CX - 7, CY);       // gap 6, lower id
        var tieHigh = Place(factory, BuildingKind.Lamp, CX, CY + 7);      // gap 6, higher id
        var gap5 = Place(factory, BuildingKind.Lamp, CX + 6, CY);
        var gap4 = Place(factory, BuildingKind.Lamp, CX, CY - 5);
        var gap3 = Place(factory, BuildingKind.Lamp, CX, CY + 4);
        var gap2 = Place(factory, BuildingKind.Lamp, CX - 3, CY);
        var gap1 = Place(factory, BuildingKind.Lamp, CX + 2, CY);

        var node = Place(factory, BuildingKind.PowerNode, CX, CY);
        var grid = factory.PowerGrid;

        Assert.Equal(6, grid.LinksOf(node));
        foreach (var lamp in new[] { gap1, gap2, gap3, gap4, gap5, tieLow })
        {
            Assert.True(grid.HasLink(node, lamp), $"lampada {lamp}");
        }

        Assert.False(grid.HasLink(node, tieHigh));
        Assert.False(grid.HasLink(node, outOfRange));
    }

    [Fact]
    public void LaserBetweenNodesCountsOnBothAndNeedsAFreeSlotOnBoth()
    {
        var factory = Sandbox();
        var first = Place(factory, BuildingKind.PowerNode, CX, CY);
        var second = Place(factory, BuildingKind.PowerNode, CX + 5, CY);
        var grid = factory.PowerGrid;

        Assert.True(grid.HasLink(first, second));
        Assert.True(grid.HasLink(second, first));
        Assert.Equal(1, grid.LinksOf(first));
        Assert.Equal(1, grid.LinksOf(second));

        // Fill the first node by hand (5 more lamps): a new node in its range does not link to it.
        var host = new SimHost(factory);
        foreach (var (dx, dy) in new[] { (-2, 0), (0, 2), (0, -2), (-3, 3), (-3, -3) })
        {
            var lamp = Place(factory, BuildingKind.Lamp, CX + dx, CY + dy);
            host.Submit(new TogglePowerLink(1, first, lamp));
        }

        host.AdvanceTicks(1);
        Assert.Equal(6, grid.LinksOf(first));
        var third = Place(factory, BuildingKind.PowerNode, CX, CY + 5);
        Assert.False(grid.HasLink(first, third));
        Assert.True(grid.HasLink(second, third));
        Assert.Equal(6, grid.LinksOf(first));
    }

    [Fact]
    public void NodeFilledByNodeLasersIsOnTheirNetwork()
    {
        var factory = Sandbox();
        // Seven nodes in reach of the middle one: its six slots fill in step 1 with one candidate left over.
        var ring = new[] { (-6, 0), (6, 0), (0, -6), (0, 6), (-5, -5), (5, 5), (5, -5) }
            .Select(d => Place(factory, BuildingKind.PowerNode, CX + d.Item1, CY + d.Item2))
            .ToArray();
        var grid = factory.PowerGrid;
        Assert.True(ring.Count(id => grid.LinksOf(id) < Content.FindBuilding(PowerNode.Tier1Id)!.MaxPowerLinks) > 6);

        var hub = Place(factory, BuildingKind.PowerNode, CX, CY);
        Assert.Equal(6, grid.LinksOf(hub));
        var network = grid.NetworkOf(hub);
        Assert.NotNull(network);
        foreach (var id in ring.Where(id => grid.HasLink(hub, id)))
        {
            Assert.Same(network, grid.NetworkOf(id));
        }

        Assert.Contains(hub, network.Members);
        Assert.Equal(network.Members.Count, grid.Networks.Sum(n => n.Members.Contains(hub) ? n.Members.Count : 0));
    }

    [Fact]
    public void NodeToNodeLaserNeedsOnlyOneOfTheTwoRanges()
    {
        var factory = Sandbox();
        var big = Place(factory, BuildingKind.PowerNodeT2, CX, CY);   // 2×2, range 10
        var small = Place(factory, BuildingKind.PowerNode, CX + 11, CY); // gap 9: only the T2 reaches
        Assert.True(factory.PowerGrid.HasLink(big, small));

        var tooFar = Place(factory, BuildingKind.PowerNode, CX + 2, CY + 13); // gap 11
        Assert.False(factory.PowerGrid.HasLink(big, tooFar));
    }

    [Fact]
    public void ToggleAddsAndRemovesAndRefusesWithAReason()
    {
        var factory = Sandbox();
        var host = new SimHost(factory);
        var rejected = new List<RefusalCode>();
        host.CommandRejected += (_, r) => rejected.Add(r.Code);
        var lamp = Place(factory, BuildingKind.Lamp, CX + 3, CY);
        var node = Place(factory, BuildingKind.PowerNode, CX, CY);
        var grid = factory.PowerGrid;
        Assert.True(grid.HasLink(node, lamp));

        host.Submit(new TogglePowerLink(1, node, lamp));
        host.AdvanceTicks(1);
        Assert.False(grid.HasLink(node, lamp));
        Assert.NotEqual(NetworkIndex(factory, node), NetworkIndex(factory, lamp));

        host.Submit(new TogglePowerLink(1, node, lamp));
        host.AdvanceTicks(1);
        Assert.True(grid.HasLink(node, lamp));
        Assert.Equal(NetworkIndex(factory, node), NetworkIndex(factory, lamp));
        Assert.Empty(rejected);

        var far = Place(factory, BuildingKind.Lamp, CX + 8, CY); // gap 7
        var miner = Place(factory, BuildingKind.Miner, CX + 5, CY - 6);
        host.Submit(new TogglePowerLink(1, node, far));
        host.Submit(new TogglePowerLink(1, node, miner));
        host.Submit(new TogglePowerLink(1, node, 987654));
        host.Submit(new TogglePowerLink(1, node, node));
        host.AdvanceTicks(1);
        Assert.Equal(
            [
                RefusalCode.PowerLinkOutOfRange, RefusalCode.PowerLinkNotPowerBlock,
                RefusalCode.PowerLinkNotPowerBlock, RefusalCode.PowerLinkSameBlock
            ],
            rejected);

        // Five more lamps fill the node; the seventh is refused.
        rejected.Clear();
        var extra = new[] { (-2, 0), (0, 2), (0, -2), (-3, 3), (-3, -3), (3, 3) }
            .Select(d => Place(factory, BuildingKind.Lamp, CX + d.Item1, CY + d.Item2))
            .ToArray();
        foreach (var l in extra)
        {
            host.Submit(new TogglePowerLink(1, node, l));
        }

        host.AdvanceTicks(1);
        Assert.Equal(6, grid.LinksOf(node));
        Assert.Equal([RefusalCode.PowerLinkNoFreeSlots], rejected);
        Assert.False(grid.HasLink(node, extra[^1]));

        // Removing a laser of a full node is always allowed.
        rejected.Clear();
        host.Submit(new TogglePowerLink(1, node, lamp));
        host.AdvanceTicks(1);
        Assert.Empty(rejected);
        Assert.Equal(5, grid.LinksOf(node));
    }

    [Fact]
    public void ToggleFromSomethingThatIsNotANodeIsInvalid()
    {
        var factory = Sandbox();
        var host = new SimHost(factory);
        var rejected = new List<RefusalCode>();
        host.CommandRejected += (_, r) => rejected.Add(r.Code);
        var lamp = Place(factory, BuildingKind.Lamp, CX, CY);
        var other = Place(factory, BuildingKind.Lamp, CX + 3, CY);

        host.Submit(new TogglePowerLink(1, lamp, other));
        host.AdvanceTicks(1);
        Assert.Equal([RefusalCode.InvalidCommand], rejected);
        Assert.Empty(factory.PowerGrid.Links);
    }

    [Fact]
    public void RemovingTheMiddleNodeSplitsTheNetworkAndDropsItsLasers()
    {
        var factory = Sandbox();
        var (gen, nodes, smelter) = Chain(factory);
        Assert.Equal(NetworkIndex(factory, gen), NetworkIndex(factory, smelter));

        Assert.True(factory.RemoveInstant(new GridPosition(X + 18, Y), refund: false));
        var grid = factory.PowerGrid;
        Assert.Equal(0, grid.LinksOf(nodes[2]));
        Assert.DoesNotContain(grid.Links, l => l.A == nodes[2] || l.B == nodes[2]);
        Assert.Equal(4, grid.Links.Count);
        Assert.Null(grid.NetworkOf(nodes[2]));
        Assert.Equal(2, grid.Networks.Count);
        Assert.NotEqual(NetworkIndex(factory, gen), NetworkIndex(factory, smelter));
        Assert.Equal(NetworkIndex(factory, gen), NetworkIndex(factory, nodes[1]));
        Assert.Equal(NetworkIndex(factory, smelter), NetworkIndex(factory, nodes[3]));
    }

    [Fact]
    public void NodeUnderConstructionHasNoLasersUntilItIsBuilt()
    {
        var factory = Sandbox(new GameRules());
        var host = new SimHost(factory);
        var rejected = new List<RefusalCode>();
        host.CommandRejected += (_, r) => rejected.Add(r.Code);
        factory.Wallet.AddMaterial("copper-wire", 20);
        var core = factory.State.CoreOrigin;
        var lamp = Place(factory, BuildingKind.Lamp, core.X + 6, core.Y);
        var builtNode = Place(factory, BuildingKind.PowerNode, core.X + 6, core.Y + 6);
        Assert.True(factory.PowerGrid.HasLink(builtNode, lamp));

        host.Submit(new PlaceBuilding(0, BuildingKind.PowerNode, new GridPosition(core.X + 9, core.Y), Direction.East));
        for (var i = 0; i < 60 * 60 && factory.Sites.Count == 0; i++)
        {
            host.AdvanceTicks(1);
        }

        var site = Assert.Single(factory.Sites);
        Assert.Single(factory.PowerNodes);
        Assert.Single(factory.PowerGrid.Links);
        Assert.DoesNotContain(factory.PowerGrid.Links, l => l.A == site.Id || l.B == site.Id);

        // A site is not a power block: no laser to it, none from it.
        host.Submit(new TogglePowerLink(1, builtNode, site.Id));
        host.Submit(new TogglePowerLink(1, site.Id, lamp));
        host.AdvanceTicks(1);
        Assert.Equal([RefusalCode.PowerLinkNotPowerBlock, RefusalCode.InvalidCommand], rejected);

        // Once the drone finishes it, the node links on its own.
        for (var i = 0; i < 60 * 60 && factory.PowerNodes.Count < 2; i++)
        {
            host.AdvanceTicks(1);
        }

        Assert.Equal(2, factory.PowerNodes.Count);
        var node = factory.PowerNodes.Single(n => n.Id != builtNode).Id;
        Assert.True(factory.PowerGrid.HasLink(node, builtNode));
    }

    [Fact]
    public void AutomaticLinksSkipConsumersAlreadyPoweredButNotGenerators()
    {
        var factory = Sandbox();
        // A lamp touching a generator is already on a powered network: the node does not spend a laser on it.
        var gen = Place(factory, BuildingKind.Generator, CX + 3, CY);
        var poweredLamp = Place(factory, BuildingKind.Lamp, CX + 3, CY - 1);
        var darkLamp = Place(factory, BuildingKind.Lamp, CX - 3, CY);
        var node = Place(factory, BuildingKind.PowerNode, CX, CY);
        var grid = factory.PowerGrid;

        Assert.True(grid.HasLink(node, gen));
        Assert.True(grid.HasLink(node, darkLamp));
        Assert.False(grid.HasLink(node, poweredLamp));
        Assert.Single(grid.Networks);
    }

    [Fact]
    public void SameLayoutGivesTheSameLinksEveryRun()
    {
        IReadOnlyList<(long, long)> Run()
        {
            var factory = Sandbox();
            Chain(factory);
            Place(factory, BuildingKind.Lamp, CX - 4, CY);
            Place(factory, BuildingKind.Lamp, CX + 4, CY);
            Place(factory, BuildingKind.Lamp, CX, CY - 4);
            Place(factory, BuildingKind.Lamp, CX, CY + 4);
            Place(factory, BuildingKind.PowerNode, CX, CY);
            return factory.PowerGrid.Links.Select(l => (l.A, l.B)).ToList();
        }

        Assert.Equal(Run(), Run());
    }

    // ---- Balance (Task 2): production, demand, satisfaction and batteries, per network. ----

    private const long GeneratorRate = 30_000; // milli-units per second
    private const long FornoRate = 8_000;
    private const long FullBattery = 1000L * 60_000; // sixty-thousandths of a unit

    private static Crafter Forno(Factory factory, long id) => factory.Smelters.Single(s => s.Id == id);

    /// <summary>A forno at (x, y) with the ore of one smelt-iron and no coal: it wants power from the first tick.</summary>
    private static long BusyForno(Factory factory, int x, int y)
    {
        var id = Place(factory, BuildingKind.Smelter, x, y);
        var forno = Forno(factory, id);
        Assert.True(forno.TryAccept("iron-ore") && forno.TryAccept("iron-ore"));
        return id;
    }

    private static long BurningGenerator(Factory factory, int x, int y)
    {
        var id = Place(factory, BuildingKind.Generator, x, y);
        factory.Generators.Single(g => g.Id == id).RestoreFuel(Generator.FuelBufferCapacity, Generator.SecondsPerFuel);
        return id;
    }

    private static Battery BatteryOf(Factory factory, long id) => factory.Batteries.Single(b => b.Id == id);

    private static PowerNetwork Net(Factory factory, long id) =>
        factory.PowerGrid.NetworkOf(id) ?? throw new InvalidOperationException($"{id} fuori rete");

    [Fact]
    public void OneGeneratorCoversThreeFornos()
    {
        var factory = Sandbox();
        var gen = BurningGenerator(factory, X, Y);
        var fornos = new[] { 2, 4, 6 }.Select(dx => BusyForno(factory, X + dx, Y)).ToArray();

        factory.Step();

        var net = Net(factory, gen);
        Assert.Equal(GeneratorRate, net.ProducedRate);
        Assert.Equal(3 * FornoRate, net.RequestedRate);
        Assert.Equal(10_000, net.SatisfactionBp);
        foreach (var id in fornos)
        {
            Assert.Same(net, Net(factory, id));
            Assert.Equal(10_000, factory.State.SatisfactionOf(id));
            Assert.True(Forno(factory, id).IsPowered);
        }
    }

    [Fact]
    public void OneGeneratorAndFiveFornosGiveThreeQuarters()
    {
        var factory = Sandbox();
        var gen = BurningGenerator(factory, X, Y);
        var fornos = new[] { 2, 4, 6, 8, 10 }.Select(dx => BusyForno(factory, X + dx, Y)).ToArray();

        factory.Step();

        var net = Net(factory, gen);
        Assert.Equal(5 * FornoRate, net.RequestedRate);
        Assert.Equal(7_500, net.SatisfactionBp);
        // Every machine of the network gets the same share: 1.2 × 0.75 = 0.9 of the recipe speed.
        var duration = Forno(factory, fornos[0]).Recipe.DurationSeconds;
        foreach (var id in fornos)
        {
            Assert.Equal(7_500, factory.State.SatisfactionOf(id));
            Assert.Equal(0.9f * SimulationClock.TickSeconds / duration, Forno(factory, id).Progress, 5);
        }
    }

    [Fact]
    public void NoDemandIsFullSatisfactionAndTheSurplusFillsTheBatteries()
    {
        var factory = Sandbox();
        var gen = BurningGenerator(factory, X, Y);
        var battery = Place(factory, BuildingKind.Battery, X + 2, Y);

        factory.RunTicks(SimulationClock.TickRate);

        var net = Net(factory, gen);
        Assert.Same(net, Net(factory, battery));
        Assert.Equal(0, net.RequestedRate);
        Assert.Equal(10_000, net.SatisfactionBp);
        Assert.Equal(GeneratorRate * SimulationClock.TickRate, BatteryOf(factory, battery).Charge);
        Assert.Equal(BatteryOf(factory, battery).Charge, net.Stored);
        Assert.Equal(FullBattery, net.Capacity);

        // A network with nothing at all (one lone node) has nothing to divide by either.
        var node = Place(factory, BuildingKind.PowerNode, X + 40, Y + 40);
        factory.Step();
        Assert.Equal(10_000, Net(factory, node).SatisfactionBp);
    }

    [Fact]
    public void BatteriesStopAtTheirCapacity()
    {
        var factory = Sandbox();
        BurningGenerator(factory, X, Y);
        var battery = Place(factory, BuildingKind.Battery, X + 2, Y);
        BatteryOf(factory, battery).Charge = FullBattery - 10_000;

        factory.Step();

        Assert.Equal(FullBattery, BatteryOf(factory, battery).Charge);
    }

    [Fact]
    public void ABatteryCoversTheGapWhenTheCoalRunsOutThenEmpties()
    {
        var factory = Sandbox();
        var gen = Place(factory, BuildingKind.Generator, X, Y); // no coal
        var battery = Place(factory, BuildingKind.Battery, X, Y + 2);
        var forno = BusyForno(factory, X + 2, Y);
        BatteryOf(factory, battery).Charge = 2 * FornoRate + FornoRate / 2; // two ticks and a half

        factory.Step();
        Assert.Equal(10_000, Net(factory, gen).SatisfactionBp);
        Assert.Equal(0, Net(factory, gen).ProducedRate);
        Assert.Equal(FornoRate + FornoRate / 2, BatteryOf(factory, battery).Charge);

        factory.Step();
        Assert.Equal(10_000, Net(factory, gen).SatisfactionBp);
        Assert.Equal(FornoRate / 2, BatteryOf(factory, battery).Charge);

        // Half a tick left: half the demand, rounded down, and the battery stays empty.
        factory.Step();
        Assert.Equal(5_000, factory.State.SatisfactionOf(forno));
        Assert.Equal(0, BatteryOf(factory, battery).Charge);

        factory.Step();
        Assert.Equal(0, factory.State.SatisfactionOf(forno));
        Assert.False(Forno(factory, forno).IsPowered);
        Assert.False(Forno(factory, forno).IsWorking);
    }

    [Fact]
    public void SeparateNetworksDoNotHelpEachOther()
    {
        var factory = Sandbox();
        // Network 1: a burning generator with a battery and nothing to feed.
        var gen = BurningGenerator(factory, X, Y);
        var fullOne = Place(factory, BuildingKind.Battery, X + 2, Y);
        // Network 2, far away: an empty battery and a busy forno.
        var emptyOne = Place(factory, BuildingKind.Battery, X + 40, Y);
        var forno = BusyForno(factory, X + 41, Y);

        factory.RunTicks(10);

        Assert.NotSame(Net(factory, gen), Net(factory, forno));
        Assert.Equal(10 * GeneratorRate, BatteryOf(factory, fullOne).Charge);
        Assert.Equal(0, BatteryOf(factory, emptyOne).Charge);
        Assert.Equal(0, factory.State.SatisfactionOf(forno));
        Assert.Equal(FornoRate, Net(factory, forno).RequestedRate);
    }

    [Fact]
    public void ChargeIsSplitByCapacityAndKeptWhenNetworksSplitOrJoin()
    {
        var factory = Sandbox();
        var a = Place(factory, BuildingKind.Battery, X, Y);
        var b = Place(factory, BuildingKind.Battery, X + 8, Y);
        Place(factory, BuildingKind.PowerNode, X + 4, Y);
        Assert.Same(Net(factory, a), Net(factory, b));
        BatteryOf(factory, a).Charge = 1001;

        factory.Step();
        Assert.Equal(501, BatteryOf(factory, a).Charge); // the remainder goes to the lowest id
        Assert.Equal(500, BatteryOf(factory, b).Charge);
        Assert.Equal(1001, Net(factory, a).Stored);
        Assert.Equal(2 * FullBattery, Net(factory, a).Capacity);

        // Split: each battery keeps its own charge.
        BatteryOf(factory, a).Charge = 700;
        BatteryOf(factory, b).Charge = 300;
        Assert.True(factory.TryRemoveBuildingAt(new GridPosition(X + 4, Y)));
        factory.Step();
        Assert.NotSame(Net(factory, a), Net(factory, b));
        Assert.Equal(700, BatteryOf(factory, a).Charge);
        Assert.Equal(300, BatteryOf(factory, b).Charge);

        // Join again: the charge of the joined network is split by capacity.
        Place(factory, BuildingKind.PowerNode, X + 4, Y);
        factory.Step();
        Assert.Equal(500, BatteryOf(factory, a).Charge);
        Assert.Equal(500, BatteryOf(factory, b).Charge);
    }

    [Fact]
    public void ChargeIsSharedByEachBatterysOwnCapacity()
    {
        // A future second battery type: the share follows each one's capacity, never the average.
        var network = new PowerNetwork(0, [1, 2]);
        var small = new Battery(new GridPosition(0, 0), 1);
        var big = new Battery(new GridPosition(2, 0), 2);
        network.AddBattery(small, 1_000);
        network.AddBattery(big, 3_000);

        PowerSystem.ShareCharge(network, 2_001);
        Assert.Equal((501L, 1_500L), (small.Charge, big.Charge)); // remainder to the lowest id

        PowerSystem.ShareCharge(network, 4_000);
        Assert.Equal((1_000L, 3_000L), (small.Charge, big.Charge));

        // A full small battery never takes the remainder past its capacity.
        PowerSystem.ShareCharge(network, 3_999);
        Assert.True(small.Charge <= 1_000 && big.Charge <= 3_000);
        Assert.Equal(3_999, small.Charge + big.Charge);
    }

    [Fact]
    public void FornoWithCoalBurnsItBelowTheBreakEvenAndAsksForNothing()
    {
        var factory = Sandbox();
        var gen = BurningGenerator(factory, X, Y);
        var fornos = new[] { 2, 4, 6, 8, 10 }.Select(dx => BusyForno(factory, X + dx, Y)).ToArray();
        var coal = Forno(factory, fornos[4]);
        coal.SeedFuel(2);

        factory.Step();

        // With all five the share would be 75% (1.2 × 0.75 < 1): the forno with coal burns it and leaves the power.
        var net = Net(factory, gen);
        Assert.Equal(4 * FornoRate, net.RequestedRate);
        Assert.Equal(9_375, net.SatisfactionBp);
        Assert.False(coal.IsPowered);
        Assert.True(coal.IsWorking);
        Assert.True(coal.IsBurningFuel);
        Assert.Equal(1, coal.FuelBuffer);
        Assert.Equal(SimulationClock.TickSeconds / coal.Recipe.DurationSeconds, coal.Progress, 5);
    }

    [Fact]
    public void FornoNextToTheCoreGetsNoPower()
    {
        var factory = Sandbox();
        var core = factory.CoreTiles.Min(t => (t.X, t.Y));
        var forno = BusyForno(factory, core.X - 2, core.Y);

        factory.RunTicks(10);

        Assert.Equal(0, factory.State.SatisfactionOf(forno));
        Assert.Single(Net(factory, forno).Members);
        Assert.Equal(0f, Forno(factory, forno).Progress);
    }

    [Fact]
    public void MachinesOffTheGridHaveNoSatisfaction()
    {
        var factory = Sandbox();
        Assert.Equal(0, factory.State.SatisfactionOf(987654));
    }

    [Fact]
    public void AssemblerAtHalfPowerRunsAtOnePointOne()
    {
        var recipe = Content.FindRecipe("craft-copper-wire")!;
        var assembler = new Crafter(new GridPosition(0, 0), Direction.East, recipe, Crafter.AssemblerBuildingId);
        foreach (var input in recipe.Inputs)
        {
            for (var n = 0; n < input.Amount; n++)
            {
                Assert.True(assembler.TryAccept(input.ItemId));
            }
        }

        long next = 1;
        assembler.Tick(0.1f, new BeltGrid(), ref next, powerBp: 5_000);
        Assert.Equal(0.1f * 1.1f / recipe.DurationSeconds, assembler.Progress, 5);
        Assert.True(assembler.IsPowered);
    }

    [Fact]
    public void FornoAtHalfPowerWithoutCoalRunsAtPointSix()
    {
        var recipe = Content.FindRecipe("smelt-iron")!;
        var forno = new Crafter(new GridPosition(0, 0), Direction.East, recipe);
        Assert.True(forno.TryAccept("iron-ore") && forno.TryAccept("iron-ore"));

        long next = 1;
        forno.Tick(0.1f, new BeltGrid(), ref next, powerBp: 5_000);
        Assert.Equal(0.1f * 0.6f / recipe.DurationSeconds, forno.Progress, 5);
        Assert.True(forno.IsPowered);
    }

    [Fact]
    public void FornoAtHalfPowerWithCoalBurnsItAtOne()
    {
        var recipe = Content.FindRecipe("smelt-iron")!;
        var forno = new Crafter(new GridPosition(0, 0), Direction.East, recipe);
        Assert.True(forno.TryAccept("iron-ore") && forno.TryAccept("iron-ore"));
        forno.SeedFuel(1);

        long next = 1;
        forno.Tick(0.1f, new BeltGrid(), ref next, powerBp: 5_000);
        Assert.Equal(0.1f / recipe.DurationSeconds, forno.Progress, 5);
        Assert.False(forno.IsPowered);
        Assert.True(forno.IsBurningFuel);

        // Above the break-even (1.2 × s ≥ 1) power wins over coal.
        var fast = new Crafter(new GridPosition(0, 0), Direction.East, recipe);
        Assert.True(fast.TryAccept("iron-ore") && fast.TryAccept("iron-ore"));
        fast.SeedFuel(1);
        fast.Tick(0.1f, new BeltGrid(), ref next, powerBp: 10_000);
        Assert.Equal(0.1f * 1.2f / recipe.DurationSeconds, fast.Progress, 5);
        Assert.True(fast.IsPowered);
        Assert.Equal(1, fast.FuelBuffer);
    }

    [Fact]
    public void AdvancedMinerAtHalfPowerIsTenPercentFaster()
    {
        Miner Make() => new(new GridPosition(0, 0), Direction.East, Miner.FootprintArea, "iron-ore", Miner.AdvancedId);
        var half = Make();
        var none = Make();
        long next = 1;
        half.Tick(0.1f, new BeltGrid(), ref next, powerBp: 5_000);
        none.Tick(0.1f, new BeltGrid(), ref next, powerBp: 0);
        Assert.Equal(none.Progress * 1.1f, half.Progress, 5);
        Assert.True(half.IsPowered);
        Assert.False(none.IsPowered);
    }

    [Fact]
    public void LampAtHalfPowerGivesHalfTheLight()
    {
        var lamp = new Lamp(new GridPosition(0, 0));
        lamp.Tick(5_000);
        Assert.Equal(0.5f, lamp.Light);
        Assert.True(lamp.IsLit);
        lamp.Tick(0);
        Assert.Equal(0f, lamp.Light);
        Assert.False(lamp.IsLit);
    }

    [Fact]
    public void RemovingAMiddleNodeSplitsTheBalanceInTheSameTick()
    {
        var factory = Sandbox();
        var (gen, nodes, smelter) = Chain(factory);
        factory.Generators.Single(g => g.Id == gen).RestoreFuel(Generator.FuelBufferCapacity, Generator.SecondsPerFuel);
        var forno = Forno(factory, smelter);
        Assert.True(forno.TryAccept("iron-ore") && forno.TryAccept("iron-ore"));

        factory.Step();
        Assert.Equal(10_000, factory.State.SatisfactionOf(smelter));
        Assert.Equal(FornoRate, Net(factory, gen).RequestedRate);

        Assert.True(factory.TryRemoveBuildingAt(new GridPosition(X + 18, Y)));
        factory.Step();
        Assert.Equal(0, Net(factory, gen).RequestedRate);
        Assert.Equal(10_000, Net(factory, gen).SatisfactionBp);
        Assert.Equal(0, factory.State.SatisfactionOf(smelter));
        Assert.False(forno.IsPowered);
    }

    [Fact]
    public void BalanceIsTheSameEveryRunWhileNetworksSplitAndJoin()
    {
        byte[] Run()
        {
            var factory = Sandbox();
            var (gen, _, smelter) = Chain(factory);
            factory.Generators.Single(g => g.Id == gen).RestoreFuel(Generator.FuelBufferCapacity, Generator.SecondsPerFuel);
            Place(factory, BuildingKind.Battery, X, Y + 2);
            Place(factory, BuildingKind.Battery, X + 34, Y);
            var forno = Forno(factory, smelter);
            for (var round = 0; round < 3; round++)
            {
                forno.TryAccept("iron-ore");
                forno.TryAccept("iron-ore");
                factory.RunTicks(40);
                Assert.True(factory.TryRemoveBuildingAt(new GridPosition(X + 18, Y)));
                factory.RunTicks(40);
                Place(factory, BuildingKind.PowerNode, X + 18, Y);
                factory.RunTicks(40);
            }

            return Snapshot.Write(factory.State);
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void TheViewGivesTheClientBlocksNodeLinksAndTheRangeRule()
    {
        var factory = Sandbox();
        IGameView view = factory;
        var lamp = Place(factory, BuildingKind.Lamp, CX + 3, CY);
        var far = Place(factory, BuildingKind.Lamp, CX + 8, CY); // gap 7
        var node = Place(factory, BuildingKind.PowerNode, CX, CY);
        var miner = Place(factory, BuildingKind.Miner, CX + 5, CY - 6);

        Assert.Equal(PowerGrid.Blocks(factory.State), view.PowerBlocks);
        Assert.Equal(lamp, view.PowerBlockAt(new GridPosition(CX + 3, CY))?.Id);
        Assert.Equal(PowerBlockKind.Node, view.PowerBlockAt(new GridPosition(CX, CY))?.Kind);
        Assert.Null(view.PowerBlockAt(new GridPosition(CX + 5, CY - 6))); // Miner T1: not a power block
        Assert.Null(view.PowerBlockAt(new GridPosition(CX + 1, CY + 1)));

        Assert.Equal(new PowerNodeLinks(node, 1, 6, 6), view.PowerNodeLinksOf(node));
        Assert.Null(view.PowerNodeLinksOf(lamp));

        Assert.True(view.PowerLinkInRange(node, lamp));
        Assert.False(view.PowerLinkInRange(node, far));
        Assert.False(view.PowerLinkInRange(node, miner));
        Assert.False(view.PowerLinkInRange(lamp, node));
    }
}
