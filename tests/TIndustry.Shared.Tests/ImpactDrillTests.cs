using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The impact drill («Trapano a impatto»): a 3×3 miner of tier 3 that digs every ore (and sand), works only with
/// power (8 × efficiency × satisfaction, four times the Miner T2's speed) and ejects on its 12 outer edge cells.
/// </summary>
public sealed class ImpactDrillTests
{
    private const int MapSize = 40;

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly GridPosition CoreOrigin = new(30, 30);

    /// <summary>A 40×40 sandbox on stone with <paramref name="cells"/> painted over it, everything unlocked.</summary>
    private static Factory Sandbox(params (int X, int Y, FloorKind Floor, DepositKind Deposit)[] cells)
    {
        var factory = NewGame.Sandbox(
            Content, CoreOrigin, mapWidth: MapSize, mapHeight: MapSize, rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        factory.Research.ForceUnlock(Miner.ImpactDrillId);
        factory.Research.ForceUnlock(Miner.AdvancedId);
        factory.Research.ForceUnlock(Generator.BuildingId);
        var tiles = new TerrainTile[MapSize, MapSize];
        for (var x = 0; x < MapSize; x++)
        {
            for (var y = 0; y < MapSize; y++)
            {
                tiles[x, y] = new TerrainTile(FloorKind.Stone, WallKind.None, DepositKind.None);
            }
        }

        foreach (var (x, y, floor, deposit) in cells)
        {
            tiles[x, y] = new TerrainTile(floor, WallKind.None, deposit);
        }

        factory.Terrain = TerrainMap.FromTiles(tiles);
        return factory;
    }

    private static (int, int, FloorKind, DepositKind)[] Block(int x, int y, int size, FloorKind floor, DepositKind deposit)
    {
        var cells = new List<(int, int, FloorKind, DepositKind)>();
        for (var dy = 0; dy < size; dy++)
        {
            for (var dx = 0; dx < size; dx++)
            {
                cells.Add((x + dx, y + dy, floor, deposit));
            }
        }

        return cells.ToArray();
    }

    private static BuildSpec DrillSpec(int x, int y) =>
        new(Miner.ImpactDrillId, BuildingKind.ImpactDrill, new GridPosition(x, y), Direction.East);

    private static Miner PlaceDrill(Factory factory, int x, int y)
    {
        Assert.True(factory.PlaceInstant(DrillSpec(x, y), charge: false), $"trapano su {x},{y}");
        return factory.Miners.Single(m => m.Position == new GridPosition(x, y));
    }

    private static void BurningGenerator(Factory factory, int x, int y)
    {
        var spec = new BuildSpec(Generator.BuildingId, BuildingKind.Generator, new GridPosition(x, y), Direction.East);
        Assert.True(factory.PlaceInstant(spec, charge: false), $"generatore su {x},{y}");
        factory.Generators[^1].RestoreFuel(Generator.FuelBufferCapacity, Generator.SecondsPerFuel);
    }

    private static void Run(Factory factory, int seconds)
    {
        for (var i = 0; i < SimulationClock.TickRate * seconds; i++)
        {
            factory.Step();
        }
    }

    [Fact]
    public void DrillHasItsDefinition()
    {
        Assert.Equal("impact-drill", BuildSpec.StructureIdFor(BuildingKind.ImpactDrill));
        Assert.Equal(BuildingKind.ImpactDrill, BuildSpec.KindFor("impact-drill"));
        Assert.Equal(3, Miner.SizeFor(Miner.ImpactDrillId));
        Assert.Equal(2, Miner.SizeFor(Miner.AdvancedId));
        Assert.Equal(2, Miner.SizeFor(Miner.BasicId));

        var building = Content.FindBuilding(Miner.ImpactDrillId)!;
        Assert.Equal(3, building.Tier);
        Assert.Equal(30f, building.PowerUse);
        Assert.Equal(
            [("titanium-plate", 40), ("silicon", 20), ("graphite", 20)],
            building.BuildCost.Select(c => (c.ItemId, c.Amount)));
        Assert.Equal("Trapano a impatto", Content.RequireStructure(Miner.ImpactDrillId).DisplayName);
        Assert.Equal(["miner-advanced"], Content.RequireStructure(Miner.ImpactDrillId).Prerequisites);
    }

    [Fact]
    public void DrillOccupiesThreeByThree()
    {
        var factory = Sandbox(Block(10, 10, 3, FloorKind.Stone, DepositKind.Iron));
        var drill = PlaceDrill(factory, 10, 10);

        Assert.Equal(3, drill.Size);
        Assert.Equal(9, drill.OccupiedTiles().Count());
        Assert.Equal(9, factory.Footprint(DrillSpec(20, 20)).Count);
        for (var y = 10; y < 13; y++)
        {
            for (var x = 10; x < 13; x++)
            {
                Assert.True(factory.TryGetSpecAt(new GridPosition(x, y), out var spec, out var id), $"{x},{y}");
                Assert.Equal(BuildingKind.ImpactDrill, spec.Kind);
                Assert.Equal(drill.Id, id);
            }
        }

        Assert.False(factory.TryGetSpecAt(new GridPosition(13, 10), out _, out _));
        Assert.False(factory.TryPlaceBelt(new GridPosition(12, 12), Direction.East));
        Assert.False(factory.CanPlace(
            new BuildSpec("conveyor-basic", null, new GridPosition(11, 11), Direction.East), out _));
        Assert.True(factory.TryPlaceBelt(new GridPosition(13, 12), Direction.East));
    }

    [Fact]
    public void DrillDigsTitanium()
    {
        var factory = Sandbox(Block(10, 10, 3, FloorKind.Stone, DepositKind.Titanium));
        Assert.True(factory.CanPlace(DrillSpec(10, 10), out _));
        var drill = PlaceDrill(factory, 10, 10);

        Assert.Equal("titanium-ore", drill.OutputItemId);
        Assert.Equal(9, drill.CoveredDepositTiles);
        Assert.Equal(1f, drill.Efficiency, 3);
    }

    [Fact]
    public void DrillEfficiencyIsCoveredOverNineWithoutBonus()
    {
        var factory = Sandbox(Block(10, 10, 2, FloorKind.Stone, DepositKind.Copper));
        var drill = PlaceDrill(factory, 10, 10);

        Assert.Equal("copper-ore", drill.OutputItemId);
        Assert.Equal(4, drill.CoveredDepositTiles);
        Assert.Equal(4f / 9f, drill.Efficiency, 4);
    }

    [Fact]
    public void DrillIsFourTimesT2()
    {
        // Both deliver straight into the Core, so nothing but their speed limits them.
        // Drill at (27,30) touches the Core's West edge; its generator at (25,30) touches the drill.
        var drillGame = Sandbox(Block(27, 30, 3, FloorKind.Stone, DepositKind.Iron));
        var drill = PlaceDrill(drillGame, 27, 30);
        BurningGenerator(drillGame, 25, 30);

        // Miner T2 at (28,30) on 4 iron cells: its own speed, no power (MiningSpeed 2 against the drill's 8).
        var t2Game = Sandbox(Block(28, 30, 2, FloorKind.Stone, DepositKind.Iron));
        Assert.True(t2Game.PlaceInstant(
            new BuildSpec(Miner.AdvancedId, BuildingKind.MinerAdvanced, new GridPosition(28, 30), Direction.East),
            charge: false));
        var t2 = t2Game.Miners.Single();

        Run(drillGame, 1);
        Run(t2Game, 1);
        Assert.Equal(10_000, drill.PowerBp);
        var drillStart = drill.ItemsProduced;
        var t2Start = t2.ItemsProduced;
        Run(drillGame, 60);
        Run(t2Game, 60);

        var drillPieces = drill.ItemsProduced - drillStart;
        var t2Pieces = t2.ItemsProduced - t2Start;
        Assert.Equal(60, t2Pieces, 1f); // 2 / 2 s
        Assert.Equal(240, drillPieces, 1f); // 8 × 1 × 1 / 2 s
        Assert.InRange(drillPieces, 4 * t2Pieces - 1, 4 * t2Pieces + 1);
        Assert.Equal(4 * Miner.SpeedOf(Miner.AdvancedId), Miner.SpeedOf(Miner.ImpactDrillId));
    }

    [Fact]
    public void DrillStopsWithoutPower()
    {
        var factory = Sandbox(Block(27, 30, 3, FloorKind.Stone, DepositKind.Iron));
        var drill = PlaceDrill(factory, 27, 30);

        Assert.True(drill.IsWorking);
        Assert.True(drill.WantsPower);
        Run(factory, 10);
        Assert.Equal(0, drill.ItemsProduced);
        Assert.Equal(0f, drill.Progress);
        Assert.False(drill.IsPowered);
    }

    [Fact]
    public void DrillIsAPowerConsumer()
    {
        var factory = Sandbox(Block(27, 30, 3, FloorKind.Stone, DepositKind.Iron));
        var drill = PlaceDrill(factory, 27, 30);
        BurningGenerator(factory, 25, 30);
        Run(factory, 1);

        var network = factory.PowerGrid.NetworkOf(drill.Id)!;
        Assert.Same(network, factory.PowerGrid.NetworkOf(factory.Generators[0].Id));
        Assert.Equal(Content.FindBuilding(Miner.ImpactDrillId)!.PowerUseRate, network.RequestedRate);
        Assert.Contains(PowerGrid.Blocks(factory.State), b => b.Id == drill.Id && b.Size == 3);
    }

    [Fact]
    public void DrillOnSand()
    {
        var factory = Sandbox(Block(10, 10, 3, FloorKind.Sand, DepositKind.None));
        var drill = PlaceDrill(factory, 10, 10);

        Assert.Equal("sand", drill.OutputItemId);
        Assert.Equal(9, drill.CoveredDepositTiles);
        Assert.Equal(1f, drill.Efficiency, 3);
    }

    [Fact]
    public void DrillPartlyOutsideMapRefused()
    {
        var factory = Sandbox(Block(MapSize - 2, 10, 2, FloorKind.Stone, DepositKind.Iron));

        Assert.False(factory.CanPlace(DrillSpec(MapSize - 2, 10), out _));
        Assert.False(factory.PlaceInstant(DrillSpec(MapSize - 2, 10), charge: false));
        Assert.False(factory.CanPlace(DrillSpec(10, MapSize - 1), out _));
        Assert.Empty(factory.Miners);
        Assert.True(factory.CanPlace(DrillSpec(MapSize - 3, 10), out _));
    }

    [Fact]
    public void DrillEjectsOnAllTwelveSides()
    {
        var factory = Sandbox(Block(10, 10, 3, FloorKind.Stone, DepositKind.Iron));
        var drill = PlaceDrill(factory, 10, 10);

        // Power by laser, so that no block touches the drill: generator (17..18, 10..11) + node (16, 10) beside it.
        BurningGenerator(factory, 17, 10);
        var node = new BuildSpec(PowerNode.Tier1Id, BuildingKind.PowerNode, new GridPosition(16, 10), Direction.East);
        Assert.True(factory.PlaceInstant(node, charge: false));
        Assert.True(factory.TryGetSpecAt(new GridPosition(16, 10), out _, out var nodeId));
        // A new node links itself to the power blocks in its range (the drill among them).
        Assert.Same(factory.PowerGrid.NetworkOf(nodeId), factory.PowerGrid.NetworkOf(drill.Id));

        var outputs = Enumerable.Range(0, drill.OutputTileCount).Select(drill.OutputTileAt).ToList();
        Assert.Equal(12, outputs.Distinct().Count());
        foreach (var cell in outputs)
        {
            Assert.DoesNotContain(cell, drill.OccupiedTiles());
        }

        // One outward belt on each of the 12 edge cells.
        var belts = new List<GridPosition>();
        for (var i = 0; i < 3; i++)
        {
            foreach (var (cell, dir) in new[]
                     {
                         (new GridPosition(10 + i, 9), Direction.North), (new GridPosition(13, 10 + i), Direction.East),
                         (new GridPosition(10 + i, 13), Direction.South), (new GridPosition(9, 10 + i), Direction.West)
                     })
            {
                Assert.True(factory.TryPlaceBelt(cell, dir), $"nastro su {cell}");
                belts.Add(cell);
            }
        }

        Assert.Equal(outputs.OrderBy(p => p.X).ThenBy(p => p.Y), belts.OrderBy(p => p.X).ThenBy(p => p.Y));
        var fed = new HashSet<GridPosition>();
        for (var tick = 0; tick < SimulationClock.TickRate * 20 && fed.Count < belts.Count; tick++)
        {
            factory.Step();
            fed.UnionWith(belts.Where(b => factory.Belts.TryGet(b, out var cell) && cell.Items.Count > 0));
        }

        Assert.True(drill.IsPowered);
        Assert.Equal(belts.Count, fed.Count);
    }

    [Fact]
    public void DrillTouchingCoreDeliversDirectly()
    {
        var factory = Sandbox(Block(27, 30, 3, FloorKind.Stone, DepositKind.Iron));
        var drill = PlaceDrill(factory, 27, 30);
        BurningGenerator(factory, 25, 30);
        var before = factory.State.CoreDeliveredItems;

        Run(factory, 5);

        Assert.True(drill.ItemsProduced > 0);
        Assert.Equal(drill.ItemsProduced, factory.State.CoreDeliveredItems - before);
    }

    [Fact]
    public void RemovingTheDrillFreesItsNineCells()
    {
        var factory = Sandbox(Block(10, 10, 3, FloorKind.Stone, DepositKind.Iron));
        PlaceDrill(factory, 10, 10);

        Assert.True(factory.TryRemoveBuildingAt(new GridPosition(12, 12)));
        Assert.Empty(factory.Miners);
        Assert.True(factory.CanPlace(DrillSpec(10, 10), out _));
    }

    [Fact]
    public void ToolKeysNameTheNewBlocks()
    {
        foreach (var id in new[]
                 {
                     Miner.ImpactDrillId, Crafter.SmelterAdvancedId, Crafter.GraphitePressId, Crafter.SeparatorId,
                     Crafter.PlasticizerId
                 })
        {
            Assert.Equal(id, Factory.StructureIdForTool(id));
        }
    }
}
