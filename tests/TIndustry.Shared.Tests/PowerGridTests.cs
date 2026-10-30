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
}
