using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The extractor as Mindustry's Unloader: no direction, 11 items a second (+11 a tick, one item every 60); it takes the
/// filter's item (or, without a filter, the next item by id) from a touching block that has it (the Core, also through
/// a linked storage block, a standalone storage block, a forno's or an assembler's output) and puts it into the next
/// touching block that takes it, in turn, never back into the block it took it from. No taker, no pull; never from belts.
/// </summary>
public sealed class UnloaderTests
{
    private const int Size = 120;
    private const string Coal = "coal";
    private const string Plate = "iron-plate";
    private const string Lead = "lead-plate";

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    /// <summary>Shard covers (60..62, 60..62).</summary>
    private static readonly GridPosition Origin = new(60, 60);

    /// <summary>East of the Core, touching it at (62, 61).</summary>
    private static readonly GridPosition BesideCore = new(63, 61);

    private static readonly TerrainTile Grass = new(FloorKind.Grass, WallKind.None, DepositKind.None);

    /// <summary>
    /// 120×120 grass (or the classic generator's terrain, which a save can rebuild), shard at (60, 60), instant build,
    /// everything researched, no auto-sell.
    /// </summary>
    private static Factory NewFactory(bool grass = true)
    {
        var f = NewGame.Sandbox(Content, Origin, mapWidth: Size, mapHeight: Size, rules: new GameRules(InstantBuild: true));
        if (grass)
        {
            SetGrass(f);
        }

        f.UnlockAllForDemo();
        f.Research.ForceUnlock("container");
        f.Research.ForceUnlock("vault");
        foreach (var item in f.State.Exchange.Items)
        {
            item.AutoSell = false;
        }

        return f;
    }

    private static void SetGrass(Factory f)
    {
        var tiles = new TerrainTile[Size, Size];
        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                tiles[x, y] = Grass;
            }
        }

        f.Terrain = TerrainMap.FromTiles(tiles);
    }

    private static void SetStock(Factory f, string itemId, int amount)
    {
        var have = f.Wallet.MaterialCount(itemId);
        if (have < amount)
        {
            f.Wallet.AddMaterial(itemId, amount - have);
        }
        else if (have > amount)
        {
            Assert.True(f.Wallet.TryRemoveMaterial(itemId, have - amount));
        }
    }

    private static Extractor PlaceExtractor(Factory f, GridPosition at, string? filter = null, Direction dir = Direction.North)
    {
        Assert.True(f.PlaceInstant(
            new BuildSpec(Extractor.BuildingId, BuildingKind.Extractor, at, dir, FilterItemId: filter), charge: false));
        return f.Extractors.Single(e => e.Position == at);
    }

    private static StorageBlock PlaceContainer(Factory f, int x, int y)
    {
        Assert.True(f.PlaceInstant(new BuildSpec("container", BuildingKind.Container, new GridPosition(x, y), Direction.East), charge: false));
        return f.Storages.Single(s => s.Origin == new GridPosition(x, y));
    }

    private static void Fill(StorageBlock storage, string itemId, int amount)
    {
        for (var i = 0; i < amount; i++)
        {
            Assert.True(storage.TryAccept(itemId));
        }
    }

    private static GridPosition[] Line(Factory f, GridPosition start, Direction dir, int length)
    {
        var cells = Enumerable.Range(0, length).Select(i => start.Step(dir, i)).ToArray();
        foreach (var cell in cells)
        {
            Assert.True(f.TryPlaceBelt(cell, dir), $"belt at {cell}");
        }

        return cells;
    }

    private static int ItemsOn(Factory f, IEnumerable<GridPosition> cells) =>
        cells.Sum(c => f.Belts.Cells[c].Items.Count);

    private static int ItemsOn(Factory f, IEnumerable<GridPosition> cells, string itemId) =>
        cells.Sum(c => f.Belts.Cells[c].Items.Count(i => i.ItemId == itemId));

    private static void Run(Factory f, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            f.Step();
        }
    }

    /// <summary>Three belt lines leaving <see cref="BesideCore"/> north, east and south (the Core is west of it).</summary>
    private static (GridPosition[] North, GridPosition[] East, GridPosition[] South) ThreeLines(Factory f, int length = 20) =>
        (Line(f, BesideCore.Step(Direction.North), Direction.North, length),
            Line(f, BesideCore.Step(Direction.East), Direction.East, length),
            Line(f, BesideCore.Step(Direction.South), Direction.South, length));

    [Fact]
    public void ABeltFacingTheExtractor_NeverTakesFromIt()
    {
        var f = NewFactory();
        var extractor = PlaceExtractor(f, BesideCore);
        var east = BesideCore.Step(Direction.East);
        Assert.True(f.TryPlaceBelt(east, Direction.West)); // faces the extractor: a belt never takes from its front

        Run(f, 300);

        Assert.Equal(0, ItemsOn(f, new[] { east }));
        Assert.Equal(0, extractor.ItemsExtracted);
    }

    [Fact]
    public void NumbersAreMindustrys()
    {
        Assert.Equal(11, Extractor.ItemsPerSecond);
        Assert.Equal(SimulationClock.TickRate, Extractor.Threshold);
    }

    [Fact]
    public void ElevenItemsASecond_FromTheCore()
    {
        var f = NewFactory();
        SetStock(f, Coal, 900);
        var ex = PlaceExtractor(f, BesideCore, Coal);
        var (north, east, south) = ThreeLines(f);

        Run(f, 10 * SimulationClock.TickRate);

        Assert.Equal(110, ex.ItemsExtracted);
        var onBelts = ItemsOn(f, north) + ItemsOn(f, east) + ItemsOn(f, south);
        Assert.Equal(110, onBelts);
        Assert.Equal(900 - 110, f.Wallet.MaterialCount(Coal));
    }

    [Fact]
    public void TakesFromTheCoreThroughALinkedStorageBlock()
    {
        var f = NewFactory();
        SetStock(f, Coal, 100);
        var linked = PlaceContainer(f, 63, 60); // 63..64 × 60..61: touches the shard
        Assert.True(linked.IsLinked);
        // (65, 60) touches only the linked container (west of it); a belt leaves east.
        var ex = PlaceExtractor(f, new GridPosition(65, 60), Coal);
        var line = Line(f, new GridPosition(66, 60), Direction.East, 20);

        Run(f, 2 * SimulationClock.TickRate);

        // One basic belt takes fewer than 11 a second: the rest waits in the Core.
        Assert.True(ex.ItemsExtracted > 0);
        Assert.Equal(ex.ItemsExtracted, ItemsOn(f, line, Coal));
        Assert.Equal(100 - ex.ItemsExtracted, f.Wallet.MaterialCount(Coal));
    }

    [Fact]
    public void EmptiesAStandaloneStorageBlockOntoABelt()
    {
        var f = NewFactory();
        var container = PlaceContainer(f, 80, 60); // 80..81 × 60..61
        Fill(container, Plate, 10);
        var plates = f.Wallet.MaterialCount(Plate);
        PlaceExtractor(f, new GridPosition(82, 60), Plate);
        var line = Line(f, new GridPosition(83, 60), Direction.East, 12);

        Run(f, 3 * SimulationClock.TickRate);

        Assert.Equal(0, container.Count(Plate));
        Assert.Equal(10, ItemsOn(f, line));
        Assert.Equal(plates, f.Wallet.MaterialCount(Plate));
    }

    [Fact]
    public void FromAFornoOutputIntoAnAssembler()
    {
        var f = NewFactory();
        // Forno 20..21 × 20..21, extractor (22, 20), assembler 23..24 × 20..21 (copper wire takes iron plates).
        Assert.True(f.TryPlaceSmelter(new GridPosition(20, 20), Direction.East));
        Assert.True(f.TryPlaceAssembler(new GridPosition(23, 20), Direction.East));
        var forno = f.Smelters.Single();
        var assembler = f.Assemblers.Single();
        forno.RestoreCraftState(0f, false, new Dictionary<string, int> { ["iron-ore"] = 1 }, [Plate, Plate, Plate], 0);
        var ex = PlaceExtractor(f, new GridPosition(22, 20));

        Run(f, SimulationClock.TickRate);

        Assert.Empty(forno.OutputQueue);
        Assert.Equal(3, assembler.InputBuffer.GetValueOrDefault(Plate));
        // Never from an input buffer: the forno's ore stays.
        Assert.Equal(1, forno.InputBuffer.GetValueOrDefault("iron-ore"));
        Assert.Equal(3, ex.ItemsExtracted);
    }

    [Fact]
    public void DestinationsInTurn()
    {
        var f = NewFactory();
        SetStock(f, Coal, 500);
        PlaceExtractor(f, BesideCore, Coal);
        var (north, east, south) = ThreeLines(f);

        Run(f, 3 * SimulationClock.TickRate); // 33 items

        Assert.Equal(11, ItemsOn(f, north));
        Assert.Equal(11, ItemsOn(f, east));
        Assert.Equal(11, ItemsOn(f, south));
    }

    [Fact]
    public void NeverBackIntoTheBlockItTookFrom()
    {
        var f = NewFactory();
        var container = PlaceContainer(f, 80, 60);
        Fill(container, Plate, 10);
        // Its only neighbour is the container: it could only put it back.
        var ex = PlaceExtractor(f, new GridPosition(82, 60), Plate);

        Run(f, 2 * SimulationClock.TickRate);

        Assert.Equal(0, ex.ItemsExtracted);
        Assert.Equal(10, container.Count(Plate));
    }

    [Fact]
    public void CoreAndContainerOnly_NothingMoves()
    {
        var f = NewFactory();
        SetStock(f, Plate, 50);
        var container = PlaceContainer(f, 64, 61); // standalone, east of BesideCore
        Assert.False(container.IsLinked);
        Fill(container, Plate, 20);
        var delivered = f.State.CoreDeliveredItems;
        var ex = PlaceExtractor(f, BesideCore);

        Run(f, 5 * SimulationClock.TickRate);

        Assert.Equal(0, ex.ItemsExtracted);
        Assert.Equal(50, f.Wallet.MaterialCount(Plate));
        Assert.Equal(20, container.Count(Plate));
        Assert.Equal(delivered, f.State.CoreDeliveredItems);
    }

    [Fact]
    public void ContainerAndContainerOnly_NothingMoves()
    {
        var f = NewFactory();
        var west = PlaceContainer(f, 80, 60); // 80..81
        var east = PlaceContainer(f, 83, 60); // 83..84
        Fill(west, Plate, 10);
        Fill(east, Lead, 10);
        var ex = PlaceExtractor(f, new GridPosition(82, 60));

        Run(f, 5 * SimulationClock.TickRate);

        Assert.Equal(0, ex.ItemsExtracted);
        Assert.Equal(10, west.Count(Plate));
        Assert.Equal(10, east.Count(Lead));
        Assert.Equal(0, west.Count(Lead) + east.Count(Plate));
    }

    [Fact]
    public void FromAFornoOutputIntoTheCore()
    {
        var f = NewFactory();
        SetStock(f, Plate, 0);
        // Forno 64..65 × 61..62 east of BesideCore: its output goes to the Core (west).
        Assert.True(f.TryPlaceSmelter(new GridPosition(64, 61), Direction.East));
        var forno = f.Smelters.Single();
        forno.RestoreCraftState(0f, false, null, [Plate, Plate], 0);
        var delivered = f.State.CoreDeliveredItems;
        // Filtered: without a filter it would also feed the Core's coal and ore to the forno.
        var ex = PlaceExtractor(f, BesideCore, Plate);

        Run(f, SimulationClock.TickRate);

        Assert.Equal(2, ex.ItemsExtracted);
        Assert.Equal(2, f.Wallet.MaterialCount(Plate));
        Assert.Empty(forno.OutputQueue);
        Assert.Equal(delivered + 2, f.State.CoreDeliveredItems);
    }

    [Fact]
    public void WithoutAFilter_ItemsInTurnById()
    {
        var f = NewFactory();
        var container = PlaceContainer(f, 80, 60);
        Fill(container, Lead, 2);
        Fill(container, Plate, 2);
        Fill(container, Coal, 2);
        var ex = PlaceExtractor(f, new GridPosition(82, 60));
        Assert.Null(ex.FilterItemId);
        var line = Line(f, new GridPosition(83, 60), Direction.East, 20);

        Run(f, 2 * SimulationClock.TickRate);

        var order = line.SelectMany(c => f.Belts.Cells[c].Items).OrderBy(i => i.Id).Select(i => i.ItemId).ToArray();
        Assert.Equal([Coal, Plate, Lead, Coal, Plate, Lead], order);
        Assert.Equal(0, container.Count(Coal) + container.Count(Plate) + container.Count(Lead));
    }

    [Fact]
    public void NoDestination_NoPull()
    {
        var f = NewFactory();
        SetStock(f, Coal, 100);
        var ex = PlaceExtractor(f, BesideCore, Coal);

        Run(f, 2 * SimulationClock.TickRate);

        Assert.Equal(0, ex.ItemsExtracted);
        Assert.Equal(100, f.Wallet.MaterialCount(Coal));

        // A belt that fills up: it stops pulling once the belt is full, and nothing is lost.
        var belt = Line(f, BesideCore.Step(Direction.East), Direction.East, 1);
        Run(f, 10 * SimulationClock.TickRate);
        var onBelt = ItemsOn(f, belt);
        Assert.True(onBelt > 0);
        Assert.Equal(onBelt, ex.ItemsExtracted);
        Assert.Equal(100, f.Wallet.MaterialCount(Coal) + onBelt);
        // The accumulator does not bank time while blocked: at most one item per threshold.
        Assert.True(ex.Accumulator <= Extractor.Threshold);
    }

    [Fact]
    public void OnlyBeltsAttached_TakesNothing()
    {
        var f = NewFactory();
        var at = new GridPosition(40, 40);
        var ex = PlaceExtractor(f, at);
        // A belt pointing into it, with items, and three belts leaving it.
        var feeder = Line(f, new GridPosition(36, 40), Direction.East, 4);
        Line(f, at.Step(Direction.North), Direction.North, 3);
        Line(f, at.Step(Direction.East), Direction.East, 3);
        Line(f, at.Step(Direction.South), Direction.South, 3);
        for (var i = 0; i < 3; i++)
        {
            f.Belts.TryInsert(feeder[0], new TransportedItem(f.State.NextItemId++, Plate), Direction.East);
            Run(f, 20);
        }

        Run(f, 5 * SimulationClock.TickRate);

        Assert.Equal(0, ex.ItemsExtracted);
        Assert.Equal(3, ItemsOn(f, feeder));
    }

    [Fact]
    public void Conservation_CoreAndAStandaloneBlock()
    {
        var f = NewFactory();
        SetStock(f, Plate, 50);
        // (63, 61) touches the Core (62, 61) and a standalone container 64..65 × 61..62; a belt leaves north.
        var container = PlaceContainer(f, 64, 61);
        Assert.False(container.IsLinked);
        Fill(container, Plate, 50);
        var ex = PlaceExtractor(f, BesideCore, Plate);
        var line = Line(f, BesideCore.Step(Direction.North), Direction.North, 30);

        for (var i = 0; i < 60 * SimulationClock.TickRate; i++)
        {
            f.Step();
            Assert.Equal(100, f.Wallet.MaterialCount(Plate) + container.Count(Plate) + ItemsOn(f, line, Plate));
        }

        Assert.True(ex.ItemsExtracted > 0);
    }

    [Fact]
    public void PlacedWithoutDirection_FilterByCommand()
    {
        var f = NewFactory();
        var host = new SimHost(f);
        var at = new GridPosition(40, 40);
        host.Submit(new PlaceBuilding(0, BuildingKind.Extractor, at, Direction.East));
        host.Advance(SimulationClock.TickSeconds);
        var ex = f.Extractors.Single();
        Assert.Equal(Direction.North, ex.Direction);
        Assert.Null(ex.FilterItemId);

        host.Submit(new SetBlockFilter(0, at, Coal));
        host.Advance(SimulationClock.TickSeconds);
        Assert.Equal(Coal, ex.FilterItemId);

        host.Submit(new SetBlockFilter(0, at, null));
        host.Advance(SimulationClock.TickSeconds);
        Assert.Null(ex.FilterItemId);

        var refusals = new List<Refusal>();
        host.CommandRejected += (_, refusal) => refusals.Add(refusal);
        host.Submit(new SetBlockFilter(0, at, "gold-ingot"));
        host.Advance(SimulationClock.TickSeconds);
        Assert.Null(ex.FilterItemId);
        Assert.Equal(RefusalCode.InvalidCommand, Assert.Single(refusals).Code);
    }

    [Fact]
    public void SaveKeepsAccumulatorTurnsAndFilter()
    {
        var f = NewFactory(grass: false);
        SetStock(f, Coal, 500);
        SetStock(f, Plate, 500);
        PlaceExtractor(f, BesideCore);
        ThreeLines(f);
        Run(f, 97);
        var before = f.Extractors.Single();
        Assert.NotNull(before.LastItemId);

        var restored = Factory.Restore(Content, f.Capture());
        var after = restored.Extractors.Single();
        Assert.Equal(before.Accumulator, after.Accumulator);
        Assert.Equal(before.SourceTurn, after.SourceTurn);
        Assert.Equal(before.DestinationTurn, after.DestinationTurn);
        Assert.Equal(before.LastItemId, after.LastItemId);
        Assert.Null(after.FilterItemId);

        f.Step();
        restored.Step();
        Assert.Equal(
            System.Text.Json.JsonSerializer.Serialize(f.Capture(), FactorySaveStore.JsonOptions),
            System.Text.Json.JsonSerializer.Serialize(restored.Capture(), FactorySaveStore.JsonOptions));
    }
}
