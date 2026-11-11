using System.Text;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Mindustry's plastanium conveyor (<c>conveyor-stack</c>, «Nastro a pacchi»): the first cell of a line (no stack cell
/// feeds it) packs single items of one kind, up to <see cref="BeltGrid.StackPackSize"/>; the pack leaves when full or
/// <see cref="BeltGrid.StackWaitTicks"/> ticks after the last piece came in, at 21.6 cells/s, one pack per cell. The cells
/// after it take only packs from the stack cell behind them; the last cell (no stack cell ahead) unloads only into a
/// building or the Core, piece by piece, never into a belt or another logistics block.
/// </summary>
[Collection(SaveStoreCollection.Name)]
public sealed class StackConveyorTests
{
    private const string StackId = "conveyor-stack";
    private const string Iron = "iron-ore";
    private const string Copper = "copper-ore";
    private const int Player = 0;

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly ConveyorDefinition Stack = Content.RequireConveyor(StackId);
    private static readonly ConveyorDefinition Belt = Content.RequireConveyor("conveyor-basic");

    private long nextId = 1;

    private TransportedItem NewItem(string itemId = Iron) => new(nextId++, itemId);

    private static void Tick(BeltGrid grid, int ticks = 1)
    {
        for (var i = 0; i < ticks; i++)
        {
            grid.Tick(SimulationClock.TickSeconds);
        }
    }

    /// <summary>A stack line of <paramref name="length"/> cells from (0, 0) facing East.</summary>
    private static BeltGrid StackLine(int length)
    {
        var grid = new BeltGrid();
        for (var x = 0; x < length; x++)
        {
            Assert.True(grid.TryPlaceFree(new GridPosition(x, 0), Direction.East, Stack));
        }

        return grid;
    }

    /// <summary>Puts <paramref name="count"/> pieces on the loader at (0, 0), coming from the West.</summary>
    private void Load(BeltGrid grid, int count, string itemId = Iron, GridPosition? at = null)
    {
        for (var i = 0; i < count; i++)
        {
            Assert.True(grid.TryInsert(at ?? new GridPosition(0, 0), NewItem(itemId), Direction.East));
        }
    }

    /// <summary>The only pack on the grid: its cell and the pack.</summary>
    private static (BeltGridCell Cell, TransportedItem Pack) OnlyPack(BeltGrid grid)
    {
        var cell = Assert.Single(grid.Cells.Values, c => c.Items.Count > 0);
        return (cell, Assert.Single(cell.Items));
    }

    /// <summary>A sandbox game with its Core on (30..32, 30..32), instant build, the stack conveyor researched.</summary>
    private static Factory CoreFactory()
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(30, 30), mapWidth: 60, mapHeight: 60, rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        factory.Research.ForceUnlock(StackId);
        return factory;
    }

    /// <summary>Places a stack cell for free (research is checked by <see cref="PlacedLikeABeltBehindResearchAndPaidPerCell"/>).</summary>
    private static void PlaceStack(Factory factory, GridPosition at, Direction direction = Direction.East) =>
        Assert.True(factory.PlaceInstant(new BuildSpec(StackId, null, at, direction), charge: false));

    [Fact]
    public void TheContentHasTheStackConveyor()
    {
        Assert.Equal(LogisticsKind.Stack, Stack.Kind);
        Assert.Equal(4, Stack.Tier);
        Assert.Equal(1, Stack.Capacity);
        Assert.Equal(1f, Stack.ItemSpacing);
        Assert.Equal(10, BeltGrid.StackPackSize);
        Assert.Equal(60, BeltGrid.StackWaitTicks);
        Assert.Equal("Nastro a pacchi", Content.RequireStructure(StackId).DisplayName);
        Assert.Equal(1, new TransportedItem(1, Iron).Count);
        Assert.Equal(StackId, Factory.StructureIdForTool("belt-stack"));
    }

    [Fact]
    public void LineRolesComeFromTheLine()
    {
        var grid = StackLine(3);
        Assert.True(grid.Cells[new GridPosition(0, 0)].IsStackLoader);
        Assert.False(grid.Cells[new GridPosition(0, 0)].IsStackUnloader);
        Assert.False(grid.Cells[new GridPosition(1, 0)].IsStackLoader);
        Assert.False(grid.Cells[new GridPosition(1, 0)].IsStackUnloader);
        Assert.True(grid.Cells[new GridPosition(2, 0)].IsStackUnloader);

        // A lone cell is both.
        Assert.True(grid.TryRemove(new GridPosition(2, 0)));
        Assert.True(grid.Cells[new GridPosition(1, 0)].IsStackUnloader);
        var lone = StackLine(1);
        Assert.True(lone.Cells[new GridPosition(0, 0)].IsStackLoader);
        Assert.True(lone.Cells[new GridPosition(0, 0)].IsStackUnloader);
        // A corner: (0, 0) East into the side of (1, 0) turned South still feeds it.
        Assert.True(grid.TryOrient(new GridPosition(1, 0), Direction.South));
        Assert.False(grid.Cells[new GridPosition(0, 0)].IsStackUnloader);
        Assert.False(grid.Cells[new GridPosition(1, 0)].IsStackLoader);
        Assert.True(grid.Cells[new GridPosition(1, 0)].IsStackUnloader);
        Assert.True(grid.TryPlaceFree(new GridPosition(1, 1), Direction.East, Stack));
        Assert.False(grid.Cells[new GridPosition(1, 1)].IsStackLoader);
        Assert.False(grid.Cells[new GridPosition(1, 0)].IsStackUnloader);
        // Turned to face (0, 0) head-on (as two belts head-on): the cell ahead never counts as a feeder, so both load and
        // neither unloads; a pack between them never passes (each refuses what comes from its front).
        Assert.True(grid.TryOrient(new GridPosition(1, 0), Direction.West));
        Assert.True(grid.Cells[new GridPosition(1, 1)].IsStackLoader);
        Assert.True(grid.Cells[new GridPosition(0, 0)].IsStackLoader);
        Assert.False(grid.Cells[new GridPosition(0, 0)].IsStackUnloader);
        Assert.True(grid.Cells[new GridPosition(1, 0)].IsStackLoader);
        Assert.False(grid.Cells[new GridPosition(1, 0)].IsStackUnloader);
        Load(grid, 10);
        Tick(grid, 30);
        Assert.Equal(new GridPosition(0, 0), OnlyPack(grid).Cell.Position);
        // Plain belts never count.
        Assert.True(grid.TryPlaceFree(new GridPosition(5, 5), Direction.East, Belt));
        Assert.False(grid.Cells[new GridPosition(5, 5)].IsStackLoader);
    }

    [Fact]
    public void LoaderPacksTenIdenticalItems()
    {
        var grid = StackLine(5);
        Load(grid, 10);
        var (cell, pack) = OnlyPack(grid);
        Assert.Equal(new GridPosition(0, 0), cell.Position);
        Assert.Equal(10, pack.Count);
        Assert.Equal(0f, pack.Progress);
        Assert.False(grid.TryInsert(new GridPosition(0, 0), NewItem(), Direction.East)); // full

        // Full: it leaves on the next tick, at the stack's speed.
        Tick(grid);
        Assert.Equal(Stack.RateItemsPerSecond * SimulationClock.TickSeconds, OnlyPack(grid).Pack.Progress, 4);
        Assert.Equal(0, cell.StackWaitTicks);
        Assert.False(grid.TryInsert(new GridPosition(0, 0), NewItem(), Direction.East)); // the cell is taken
    }

    [Fact]
    public void LoaderTakesFromABeltBehindAndFromTheSidesButNotFromAhead()
    {
        // Belt (−1, 0) East into the loader (0, 0); a belt on its North side facing South; one ahead facing West.
        var grid = StackLine(3);
        Assert.True(grid.TryPlaceFree(new GridPosition(-1, 0), Direction.East, Belt));
        Assert.True(grid.TryInsert(new GridPosition(-1, 0), NewItem(), Direction.East));
        Tick(grid, 60);
        Assert.Equal(1, grid.Cells[new GridPosition(0, 0)].Items.Single().Count);

        Assert.True(grid.TryInsert(new GridPosition(0, 0), NewItem(), Direction.South)); // from the North side
        Assert.Equal(2, grid.Cells[new GridPosition(0, 0)].Items.Single().Count);
        // From the cell ahead (a stack cell): refused, not a pack of its line.
        Assert.False(grid.TryInsert(new GridPosition(0, 0), NewItem(), Direction.West));
        // From a building ahead (no cell there): refused, as a belt never takes from its front.
        var lone = new BeltGrid();
        Assert.True(lone.TryPlaceFree(new GridPosition(0, 0), Direction.East, Stack));
        Assert.False(lone.TryInsert(new GridPosition(0, 0), NewItem(), source: new GridPosition(1, 0)));
        Assert.True(lone.TryInsert(new GridPosition(0, 0), NewItem(), source: new GridPosition(0, 1)));
    }

    [Fact]
    public void PartialPackLeavesAfter60Ticks()
    {
        var grid = StackLine(5);
        Load(grid, 2);
        Tick(grid, 30);
        Load(grid, 1); // the wait starts again from the last piece
        var (cell, pack) = OnlyPack(grid);
        Assert.Equal(3, pack.Count);
        Assert.Equal(0, cell.StackWaitTicks);

        Tick(grid, BeltGrid.StackWaitTicks - 1);
        Assert.Equal(0f, pack.Progress);
        Assert.Equal(BeltGrid.StackWaitTicks - 1, cell.StackWaitTicks);
        Tick(grid, 0); // the settle pass (dt 0) is not a tick
        grid.Tick(0f);
        Assert.Equal(0f, pack.Progress);

        Tick(grid);
        Assert.True(pack.Progress > 0f);
        Assert.Equal(3, pack.Count);
        Assert.Equal(0, cell.StackWaitTicks);
        // Once it moves it takes nothing more.
        Assert.False(grid.TryInsert(new GridPosition(0, 0), NewItem(), Direction.East));
    }

    [Fact]
    public void DifferentItemRefusedWhilePacking()
    {
        var grid = StackLine(3);
        Load(grid, 2);
        Assert.False(grid.TryInsert(new GridPosition(0, 0), NewItem(Copper), Direction.East));
        Assert.True(grid.TryInsert(new GridPosition(0, 0), NewItem(Iron), Direction.East));
        var (_, pack) = OnlyPack(grid);
        Assert.Equal(3, pack.Count);
        Assert.Equal(Iron, pack.ItemId);

        // A copper piece waiting on the belt behind stays there until the iron pack has left.
        Assert.True(grid.TryPlaceFree(new GridPosition(-1, 0), Direction.East, Belt));
        grid.Cells[new GridPosition(-1, 0)].RestoreState([new TransportedItem(nextId++, Copper, 1f)]);
        Tick(grid, BeltGrid.StackWaitTicks - 1);
        Assert.Single(grid.Cells[new GridPosition(-1, 0)].Items);
        Tick(grid, 10);
        Assert.Empty(grid.Cells[new GridPosition(-1, 0)].Items);
        Assert.Equal(Copper, grid.Cells[new GridPosition(0, 0)].Items.Single().ItemId);
    }

    [Fact]
    public void MiddleCellsRefuseSideInput()
    {
        var grid = StackLine(4);
        var middle = new GridPosition(2, 0);
        // A belt North of the middle cell facing South into it, with an item at its end.
        var side = new GridPosition(2, -1);
        Assert.True(grid.TryPlaceFree(side, Direction.South, Belt));
        grid.Cells[side].RestoreState([new TransportedItem(nextId++, Iron, 1f)]);
        Tick(grid, 30);
        Assert.Single(grid.Cells[side].Items);
        Assert.Empty(grid.Cells[middle].Items);

        // Nor directly, from any side or without a side.
        Assert.False(grid.TryInsert(middle, NewItem(), Direction.South));
        Assert.False(grid.TryInsert(middle, NewItem(), Direction.North));
        Assert.False(grid.TryInsert(middle, NewItem()));

        // A router beside the middle cell keeps its item; one beside the loader hands it in.
        var router = Content.RequireConveyor("router");
        Assert.True(grid.TryPlaceFree(new GridPosition(1, 1), Direction.North, router));
        Assert.True(grid.TryInsert(new GridPosition(1, 1), NewItem(), Direction.North));
        Tick(grid, 30);
        Assert.Single(grid.Cells[new GridPosition(1, 1)].Items);
        var fresh = StackLine(2);
        Assert.True(fresh.TryPlaceFree(new GridPosition(0, 1), Direction.North, router));
        Assert.True(fresh.TryInsert(new GridPosition(0, 1), NewItem(), Direction.North));
        Tick(fresh, 10);
        Assert.Empty(fresh.Cells[new GridPosition(0, 1)].Items);
        Assert.Equal(1, fresh.Cells[new GridPosition(0, 0)].Items.Single().Count);
    }

    [Fact]
    public void GatesSeeOnlyTheLoaderAsFree()
    {
        // An overflow gate (5, 5) with a middle stack cell ahead (East) and a belt on its North side: the item goes North.
        var grid = new BeltGrid();
        var gate = new GridPosition(5, 5);
        Assert.True(grid.TryPlaceFree(gate, Direction.North, Content.RequireConveyor("overflow-gate")));
        Assert.True(grid.TryPlaceFree(new GridPosition(6, 4), Direction.South, Stack)); // feeds (6, 5)
        Assert.True(grid.TryPlaceFree(new GridPosition(6, 5), Direction.East, Stack));
        Assert.True(grid.TryPlaceFree(new GridPosition(5, 4), Direction.North, Belt));
        var item = NewItem();
        Assert.True(grid.TryInsert(gate, item, Direction.East));
        Assert.Contains(item, grid.Cells[new GridPosition(5, 4)].Items);
        Assert.Empty(grid.Cells[new GridPosition(6, 5)].Items);

        // Without the stack cell feeding it, (6, 5) is a loader: straight on.
        Assert.True(grid.TryRemove(new GridPosition(6, 4)));
        var next = NewItem();
        Assert.True(grid.TryInsert(gate, next, Direction.East));
        Assert.Contains(next, grid.Cells[new GridPosition(6, 5)].Items);

        // An underflow gate whose sides are middle stack cells goes straight on to a belt.
        var under = new BeltGrid();
        Assert.True(under.TryPlaceFree(gate, Direction.North, Content.RequireConveyor("underflow-gate")));
        Assert.True(under.TryPlaceFree(new GridPosition(4, 4), Direction.East, Stack));
        Assert.True(under.TryPlaceFree(new GridPosition(5, 4), Direction.East, Stack));
        Assert.True(under.TryPlaceFree(new GridPosition(4, 6), Direction.East, Stack));
        Assert.True(under.TryPlaceFree(new GridPosition(5, 6), Direction.East, Stack));
        Assert.True(under.TryPlaceFree(new GridPosition(6, 5), Direction.East, Belt));
        var third = NewItem();
        Assert.True(under.TryInsert(gate, third, Direction.East));
        Assert.Contains(third, under.Cells[new GridPosition(6, 5)].Items);
    }

    [Fact]
    public void PackSpeedIsTripleExpress()
    {
        Assert.Equal(3f * Content.RequireConveyor("conveyor-express").RateItemsPerSecond, Stack.RateItemsPerSecond, 4);
        var grid = StackLine(40);
        Load(grid, 10);
        Tick(grid, 60); // it leaves on the first of these ticks
        var (cell, pack) = OnlyPack(grid);
        var travelled = cell.Position.X + pack.Progress;
        Assert.InRange(travelled, 21.6f - 0.36f, 21.6f + 0.36f);
        Assert.Equal(10, pack.Count);
    }

    [Fact]
    public void UnloaderDeliversIntoCore()
    {
        // A line (24..29, 31) East into the Core (30..32, 30..32).
        var factory = CoreFactory();
        var stock = factory.Wallet.MaterialCount(Copper);
        var money = factory.Wallet.Money;
        for (var x = 24; x <= 29; x++)
        {
            PlaceStack(factory, new GridPosition(x, 31));
        }

        Assert.True(factory.Belts.Cells[new GridPosition(29, 31)].IsStackUnloader);
        Assert.Equal(StackId, factory.Belts.Cells[new GridPosition(29, 31)].Definition.Id);
        Load(factory.Belts, 10, Copper, new GridPosition(24, 31));
        Assert.Equal(stock, factory.Wallet.MaterialCount(Copper));
        for (var i = 0; i < 30; i++)
        {
            factory.Step();
        }

        Assert.Equal(stock + 10, factory.Wallet.MaterialCount(Copper));
        Assert.All(factory.Belts.Cells.Values, c => Assert.Empty(c.Items));
        Assert.Equal(money, factory.Wallet.Money);
    }

    [Fact]
    public void UnloaderFeedsABuildingSeveralPiecesATick()
    {
        // A lone stack cell (39, 40) facing a forno (40..41, 40..41): on the tick the pack arrives the forno takes
        // more than one piece; what it does not take waits on the cell, still a pack, until the forno has room again.
        var factory = CoreFactory();
        Assert.True(factory.TryPlaceSmelter(new GridPosition(40, 40), Direction.East));
        PlaceStack(factory, new GridPosition(39, 40));
        Load(factory.Belts, 10, Iron, new GridPosition(39, 40));
        var cell = factory.Belts.Cells[new GridPosition(39, 40)];
        var steps = 0;
        while (cell.Items.Sum(p => p.Count) == 10 && steps++ < 10)
        {
            factory.Step();
        }

        // Several pieces on that one tick (the forno's buffer takes them all here, and the empty pack is gone).
        var left = cell.Items.Sum(p => p.Count);
        Assert.InRange(left, 0, 8);
        Assert.True(left == 0 ? cell.Items.Count == 0 : cell.Items.Single().Progress >= 1f);
    }

    [Fact]
    public void UnloaderNeverFeedsABelt()
    {
        foreach (var front in new[] { "conveyor-basic", "router", "junction", "overflow-gate" })
        {
            var grid = StackLine(3);
            Assert.True(grid.TryPlaceFree(new GridPosition(3, 0), Direction.East, Content.RequireConveyor(front)));
            Assert.True(grid.TryPlaceFree(new GridPosition(4, 0), Direction.East, Belt));
            Assert.True(grid.Cells[new GridPosition(2, 0)].IsStackUnloader);
            Load(grid, 10);
            Tick(grid, 200);
            var (cell, pack) = OnlyPack(grid);
            Assert.Equal(new GridPosition(2, 0), cell.Position);
            Assert.Equal(1f, pack.Progress);
            Assert.Equal(10, pack.Count);
        }
    }

    [Fact]
    public void StackCellsAreNotPulledByBuildings()
    {
        var grid = StackLine(1);
        Load(grid, 10);
        var cell = grid.Cells[new GridPosition(0, 0)];
        Assert.Equal(0, cell.OutputTargetCount);
        Tick(grid, 10);
        Assert.Null(cell.PeekOutputToward(cell.OutputPosition));
    }

    [Fact]
    public void PackSurvivesSave()
    {
        // A line (20..27, 10) East: a full pack mid-line, a partial pack waiting on the loader.
        var factory = CoreFactory();
        for (var x = 20; x <= 27; x++)
        {
            PlaceStack(factory, new GridPosition(x, 10));
        }

        Load(factory.Belts, 10, Iron, new GridPosition(20, 10));
        for (var i = 0; i < 6; i++)
        {
            factory.Step();
        }

        Load(factory.Belts, 3, Copper, new GridPosition(20, 10));
        for (var i = 0; i < 20; i++)
        {
            factory.Step();
        }

        var loader = factory.Belts.Cells[new GridPosition(20, 10)];
        Assert.Equal(20, loader.StackWaitTicks);
        var host = new SimHost(factory);
        var snapshot = host.CreateSnapshot();
        var json = Encoding.UTF8.GetString(snapshot);
        Assert.Contains("\"count\":10", json, StringComparison.Ordinal);
        Assert.Contains("\"count\":3", json, StringComparison.Ordinal);
        Assert.Contains("\"stackWaitTicks\":20", json, StringComparison.Ordinal);

        FactorySaveStore.Save("v22-pacchi", host);
        Assert.True(FactorySaveStore.TryLoadSlot(Content, "v22-pacchi", out var loaded, out var failure), failure?.Detail);
        var restored = (Factory)loaded!.State;
        Assert.Equal(snapshot, loaded.CreateSnapshot());
        var again = restored.Belts.Cells[new GridPosition(20, 10)];
        Assert.Equal(20, again.StackWaitTicks);
        Assert.Equal(3, again.Items.Single().Count);
        Assert.Equal(10, restored.Belts.Cells.Values.Where(c => c.Position.X > 20).Sum(c => c.Items.Sum(p => p.Count)));

        // The partial pack leaves after the 40 ticks it still had to wait, in both games.
        for (var i = 0; i < 39; i++)
        {
            factory.Step();
            restored.Step();
        }

        Assert.Equal(0f, again.Items.Single().Progress);
        factory.Step();
        restored.Step();
        Assert.True(again.Items.Single().Progress > 0f);
        Assert.Equal(host.CreateSnapshot(), loaded.CreateSnapshot());
    }

    [Fact]
    public void PlainGamesWriteNoPackField()
    {
        var factory = CoreFactory();
        Assert.True(factory.TryPlaceBelt(new GridPosition(20, 10), Direction.East));
        Assert.True(factory.Belts.TryInsert(new GridPosition(20, 10), NewItem(), Direction.East));
        var json = Encoding.UTF8.GetString(new SimHost(factory).CreateSnapshot());
        Assert.DoesNotContain("\"count\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("stackWaitTicks", json, StringComparison.Ordinal);
    }

    [Fact]
    public void BadPackValuesMakeTheSaveUnreadable()
    {
        var factory = CoreFactory();
        PlaceStack(factory, new GridPosition(20, 10));
        Assert.True(factory.TryPlaceBelt(new GridPosition(22, 10), Direction.East));
        Load(factory.Belts, 3, Iron, new GridPosition(20, 10));
        Assert.True(factory.Belts.TryInsert(new GridPosition(22, 10), NewItem(), Direction.East));
        factory.Step();
        var json = Encoding.UTF8.GetString(new SimHost(factory).CreateSnapshot());
        Assert.Contains("\"count\":3", json, StringComparison.Ordinal);
        foreach (var bad in new[]
                 {
                     json.Replace("\"count\":3", "\"count\":11"),
                     json.Replace("\"count\":3", "\"count\":0"),
                     json.Replace("\"stackWaitTicks\":1", "\"stackWaitTicks\":60"),
                     // A plain belt never carries a pack.
                     json.Replace("\"itemId\":\"iron-ore\",\"progress\":0", "\"itemId\":\"iron-ore\",\"count\":2,\"progress\":0")
                 })
        {
            Assert.NotEqual(json, bad);
            Assert.Throws<InvalidDataException>(() => Snapshot.Read(Content, Encoding.UTF8.GetBytes(bad)));
        }
    }

    [Fact]
    public void V21Refused()
    {
        Assert.Equal(22, FactorySaveData.CurrentVersion);
        Assert.Equal(22, FactorySaveData.MinReadableVersion);
        var host = new SimHost(NewGame.Demo(Content));
        FactorySaveStore.Save("v21-rifiutato", host);
        var path = FactorySaveStore.SlotPath("v21-rifiutato");
        var json = File.ReadAllText(path);
        Assert.Contains("\"version\": 22", json, StringComparison.Ordinal);
        File.WriteAllText(path, json.Replace("\"version\": 22", "\"version\": 21"));

        Assert.False(FactorySaveStore.TryLoadSlot(Content, "v21-rifiutato", out var loaded, out var failure));
        Assert.Null(loaded);
        Assert.Equal(SaveLoadError.OlderVersion, failure!.Code);
        var snapshot = Encoding.UTF8.GetString(host.CreateSnapshot()).Replace("\"version\":22", "\"version\":21");
        Assert.Throws<InvalidDataException>(() => Snapshot.Read(Content, Encoding.UTF8.GetBytes(snapshot)));
    }

    [Fact]
    public void PlacedLikeABeltBehindResearchAndPaidPerCell()
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(30, 30), mapWidth: 60, mapHeight: 60, rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        Assert.False(factory.TryPlaceBelt(new GridPosition(10, 10), Direction.East, StackId));
        factory.Research.ForceUnlock(StackId);
        foreach (var id in new[] { "plastic", "silicon", "graphite" })
        {
            factory.Wallet.AddMaterial(id, 5);
        }

        var before = new[] { "plastic", "silicon", "graphite" }.Select(factory.Wallet.MaterialCount).ToArray();
        Assert.True(factory.TryPlaceBelt(new GridPosition(10, 10), Direction.East, StackId));
        Assert.Equal(
            before.Select(n => n - 1).ToArray(),
            new[] { "plastic", "silicon", "graphite" }.Select(factory.Wallet.MaterialCount).ToArray());
        Assert.True(factory.TryGetSpecAt(new GridPosition(10, 10), out var spec, out _));
        Assert.Null(spec.Kind);
        Assert.Equal(StackId, spec.StructureId);
        Assert.Equal(
            ["plastic:1", "silicon:1", "graphite:1"],
            Stack.EffectiveBuildCost.Select(c => $"{c.ItemId}:{c.Amount}").ToArray());
    }

    [Fact]
    public void DraggedLineNeverMakesCrossingsNorBridges()
    {
        var factory = CoreFactory();
        // A plain belt crossing the line at (12, 20), facing South with belts before and after it.
        for (var y = 18; y <= 22; y++)
        {
            Assert.True(factory.TryPlaceBelt(new GridPosition(12, y), Direction.South));
        }

        var plain = PlacementLine.Plan(factory, new LineRequest(
            Player, "conveyor-basic", new GridPosition(10, 20), new GridPosition(14, 20), Direction.East));
        Assert.Contains(plain, p => p.Command is PlaceBelt { Crossing: true });

        var line = PlacementLine.Plan(factory, new LineRequest(
            Player, StackId, new GridPosition(10, 20), new GridPosition(14, 20), Direction.East));
        Assert.Equal(5, line.Count);
        Assert.All(line, p => Assert.IsType<PlaceBelt>(p.Command));
        Assert.All(line, p => Assert.False(((PlaceBelt)p.Command!).Crossing));
        Assert.All(line, p => Assert.Equal(Direction.East, p.Direction));
        Assert.NotNull(line[2].Refusal); // the crossed belt stays: the stack line stops there

        // Across a forno (40..41, 40..41): a plain belt line bridges it, a stack line does not.
        Assert.True(factory.TryPlaceSmelter(new GridPosition(40, 40), Direction.East));
        var belt = PlacementLine.Plan(factory, new LineRequest(
            Player, "conveyor-basic", new GridPosition(38, 40), new GridPosition(44, 40), Direction.East));
        Assert.Contains(belt, p => p.Command is PlaceBuilding { Kind: BuildingKind.Bridge });
        var stack = PlacementLine.Plan(factory, new LineRequest(
            Player, StackId, new GridPosition(38, 40), new GridPosition(44, 40), Direction.East));
        Assert.DoesNotContain(stack, p => p.Command is PlaceBuilding);
        Assert.DoesNotContain(stack, p => p.Spanned);

        // Even a crossing asked by hand (an agent's command) is not made.
        var host = new SimHost(factory);
        host.Submit(new PlaceBelt(Player, new GridPosition(12, 20), Direction.East, StackId, Crossing: true));
        host.Advance(SimulationClock.TickSeconds);
        Assert.Equal(LogisticsKind.Belt, factory.Belts.Cells[new GridPosition(12, 20)].Kind);
    }

    [Fact]
    public void DraggingOverTheLineTurnsItLikeABelt()
    {
        var factory = CoreFactory();
        PlaceStack(factory, new GridPosition(10, 10));
        Assert.Equal(BeltOverBelt.Same, factory.BeltOverBeltAt(new GridPosition(10, 10), Direction.East, StackId));
        Assert.Equal(BeltOverBelt.Rotate, factory.BeltOverBeltAt(new GridPosition(10, 10), Direction.South, StackId));
        // Never a tier change between a belt and a stack conveyor.
        Assert.Equal(BeltOverBelt.None, factory.BeltOverBeltAt(new GridPosition(10, 10), Direction.East, "conveyor-basic"));
        Assert.True(factory.TryPlaceBelt(new GridPosition(11, 10), Direction.East));
        Assert.Equal(BeltOverBelt.None, factory.BeltOverBeltAt(new GridPosition(11, 10), Direction.East, StackId));
    }
}
