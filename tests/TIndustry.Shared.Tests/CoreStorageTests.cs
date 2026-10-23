using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The Core's storage has a maximum per item (shard: 1000): an item at its maximum does not enter, belts and
/// adjacent buildings keep it and the line stops; other items still enter; refunds are clamped, pieces put back
/// are not; nothing is lost or duplicated.
/// </summary>
public sealed class CoreStorageTests
{
    private const string Iron = "iron-ore";
    private const string Copper = "copper-ore";
    private const string Plate = "iron-plate";

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    // Core covers (60..62, 60..62).
    private static Factory NewFactory()
    {
        var f = NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120,
            rules: new GameRules(InstantBuild: true));
        f.UnlockAllForDemo();
        return f;
    }

    /// <summary>Sets the Core stock of <paramref name="itemId"/> to exactly <paramref name="amount"/>.</summary>
    private static void SetStock(Factory f, string itemId, int amount)
    {
        var have = f.Wallet.MaterialCount(itemId);
        if (have < amount)
        {
            f.Wallet.AddMaterial(itemId, amount - have);
        }
        else
        {
            Assert.True(have == amount || f.Wallet.TryRemoveMaterial(itemId, have - amount));
        }

        Assert.Equal(amount, f.Wallet.MaterialCount(itemId));
    }

    private static long nextId = 5_000_000;

    private static TransportedItem NewItem(string itemId) => new(Interlocked.Increment(ref nextId), itemId);

    /// <summary>A straight east line on row <paramref name="y"/> ending against the Core's west side (x = 59).</summary>
    private static GridPosition[] LineIntoCore(Factory f, int y, int length = 6)
    {
        var cells = Enumerable.Range(0, length).Select(i => new GridPosition(60 - length + i, y)).ToArray();
        foreach (var cell in cells)
        {
            Assert.True(f.TryPlaceBelt(cell, Direction.East), $"belt at {cell}");
        }

        return cells;
    }

    private static int ItemsOn(Factory f, IEnumerable<GridPosition> cells, string? itemId = null) =>
        cells.Sum(c => f.Belts.Cells[c].Items.Count(i => itemId is null || i.ItemId == itemId));

    /// <summary>Runs <paramref name="seconds"/>, feeding <paramref name="itemId"/> at the start of the line every tick.</summary>
    private static void RunFeeding(Factory f, GridPosition start, string itemId, float seconds)
    {
        var ticks = (int)Math.Round(seconds * SimulationClock.TickRate);
        for (var i = 0; i < ticks; i++)
        {
            f.Belts.TryInsert(start, NewItem(itemId), Direction.East);
            f.Step();
        }
    }

    [Fact]
    public void NewGame_StartsWithShard3x3()
    {
        var f = NewFactory();

        Assert.Equal(0, f.CoreTier);
        Assert.Equal(9, f.CoreTiles.Count);
        Assert.Equal(new GridPosition(60, 60), f.State.CoreOrigin);
        Assert.Contains(new GridPosition(62, 62), f.CoreTiles);
        Assert.Equal(1000, f.CoreCapacity(Iron));
    }

    [Fact]
    public void SetCore_RecomputesTilesFromTier_AndRejectsUnknownTier()
    {
        var f = NewFactory();

        f.State.SetCore(new GridPosition(60, 60), 1);
        Assert.Equal(16, f.CoreTiles.Count);
        Assert.Equal(4000, f.CoreCapacity(Iron));

        Assert.Throws<ArgumentOutOfRangeException>(() => f.State.SetCore(new GridPosition(60, 60), 3));
        Assert.Equal(1, f.CoreTier);
    }

    [Fact]
    public void BeltIntoFullCore_Stops()
    {
        var f = NewFactory();
        var line = LineIntoCore(f, 61);
        SetStock(f, Iron, 1000);

        RunFeeding(f, line[0], Iron, 10f);

        Assert.Equal(1000, f.Wallet.MaterialCount(Iron));
        Assert.NotNull(f.Belts.Cells[line[^1]].PeekOutput());
        var capacity = f.Belts.Cells[line[0]].Definition.Capacity;
        Assert.All(line, c => Assert.Equal(capacity, f.Belts.Cells[c].Items.Count));

        // Five sold: the waiting pieces enter again within a second.
        Assert.True(f.Wallet.TryRemoveMaterial(Iron, 5));
        RunFeeding(f, line[0], Iron, 1f);
        Assert.Equal(1000, f.Wallet.MaterialCount(Iron));
    }

    [Fact]
    public void BeltQueue_HeadBlocksTail()
    {
        var f = NewFactory();
        var line = LineIntoCore(f, 61, length: 2);
        SetStock(f, Iron, 1000);
        var copper = f.Wallet.MaterialCount(Copper);

        // Iron at the head, copper behind it.
        Assert.True(f.Belts.TryInsert(line[1], NewItem(Iron), Direction.East));
        f.RunSeconds(2f);
        Assert.True(f.Belts.TryInsert(line[0], NewItem(Copper), Direction.East));
        f.RunSeconds(5f);

        Assert.Equal(1000, f.Wallet.MaterialCount(Iron));
        Assert.Equal(copper, f.Wallet.MaterialCount(Copper));
        Assert.Equal(1, ItemsOn(f, line, Iron));
        Assert.Equal(1, ItemsOn(f, line, Copper));
    }

    [Fact]
    public void OtherItemsStillEnter()
    {
        var f = NewFactory();
        var ironLine = LineIntoCore(f, 60, length: 3);
        var copperLine = LineIntoCore(f, 62, length: 3);
        SetStock(f, Iron, 1000);
        var copper = f.Wallet.MaterialCount(Copper);

        for (var i = 0; i < 3; i++)
        {
            Assert.True(f.Belts.TryInsert(ironLine[0], NewItem(Iron), Direction.East));
            Assert.True(f.Belts.TryInsert(copperLine[0], NewItem(Copper), Direction.East));
            f.RunSeconds(0.5f);
        }

        f.RunSeconds(5f);

        Assert.Equal(1000, f.Wallet.MaterialCount(Iron));
        Assert.Equal(3, ItemsOn(f, ironLine, Iron));
        Assert.Equal(copper + 3, f.Wallet.MaterialCount(Copper));
        Assert.Equal(0, ItemsOn(f, copperLine));
    }

    [Fact]
    public void AdjacentSmelter_StopsWhenPlatesFull()
    {
        var f = NewFactory();
        // Smelter covers (58..59, 60..61), touching the Core's west side.
        Assert.True(f.TryPlaceSmelter(new GridPosition(58, 60), Direction.East));
        var smelter = Assert.Single(f.State.Smelters);
        SetStock(f, Plate, 998);
        smelter.SeedFuel();
        smelter.RestoreCraftState(0f, false, new Dictionary<string, int> { [Iron] = 8 }, [Plate, Plate, Plate, Plate], 0);
        var total = f.Wallet.MaterialCount(Plate) + smelter.OutputQueue.Count;

        for (var i = 0; i < 10 * SimulationClock.TickRate; i++)
        {
            f.Step();
            Assert.Equal(total, f.Wallet.MaterialCount(Plate) + smelter.OutputQueue.Count);
        }

        Assert.Equal(1000, f.Wallet.MaterialCount(Plate));
        Assert.Equal(2, smelter.OutputQueue.Count);
        Assert.False(smelter.IsCrafting);
    }

    [Fact]
    public void AdjacentMiner_StopsWhenFull_AndGoesOnWhenSold()
    {
        // No terrain: full iron deposit everywhere. Miner (7..8, 14..15) touches the Core (9..11, 14..16).
        var f = new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(new GridPosition(9, 14)));
        f.UnlockAllForDemo();
        Assert.True(f.TryPlaceMiner(new GridPosition(7, 14), Direction.East));
        var miner = Assert.Single(f.State.Miners);
        SetStock(f, Iron, 1000);

        f.RunSeconds(10f);
        var produced = miner.ItemsProduced;
        f.RunSeconds(10f);
        Assert.Equal(1000, f.Wallet.MaterialCount(Iron));
        Assert.Equal(produced, miner.ItemsProduced);

        Assert.True(f.Wallet.TryRemoveMaterial(Iron, 2));
        f.RunSeconds(10f);
        Assert.Equal(1000, f.Wallet.MaterialCount(Iron));
        Assert.Equal(produced + 2, miner.ItemsProduced);
    }

    [Fact]
    public void SaveRoundTrip_KeepsTierAndCells()
    {
        // No terrain: a generated map is regenerated from the Core cells it started with (the upgrade's task).
        var f = new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(new GridPosition(9, 14)));
        f.State.SetCore(new GridPosition(9, 14), 1);

        var restored = Factory.Restore(Content, f.Capture());

        Assert.Equal(1, restored.CoreTier);
        Assert.Equal(new GridPosition(9, 14), restored.State.CoreOrigin);
        Assert.Equal(f.CoreTiles.OrderBy(t => t.Y).ThenBy(t => t.X), restored.CoreTiles.OrderBy(t => t.Y).ThenBy(t => t.X));
        Assert.Equal(4000, restored.CoreCapacity(Iron));
    }

    [Fact]
    public void Extractor_ReturnsWhenBeltBlocked_EvenIfFull()
    {
        var f = NewFactory();
        // Extractor west of the Core, belt west of it ending against nothing (blocked once full).
        var belt = new GridPosition(58, 61);
        Assert.True(f.TryPlaceExtractor(new GridPosition(59, 61), Direction.West, Plate));
        Assert.True(f.TryPlaceBelt(belt, Direction.West));
        SetStock(f, Plate, 1000);

        f.RunSeconds(10f);
        var onBelt = ItemsOn(f, [belt]);
        Assert.True(onBelt > 0);
        Assert.Equal(1000, f.Wallet.MaterialCount(Plate) + onBelt);

        // Full again with the belt blocked: every pull is put back, even at the maximum.
        SetStock(f, Plate, 1000);
        for (var i = 0; i < 10 * SimulationClock.TickRate; i++)
        {
            f.Step();
            Assert.Equal(1000 + onBelt, f.Wallet.MaterialCount(Plate) + ItemsOn(f, [belt]));
        }

        Assert.Equal(1000, f.Wallet.MaterialCount(Plate));
        Assert.Equal(onBelt, ItemsOn(f, [belt]));
    }

    [Fact]
    public void Refund_OverCapacity_IsLost()
    {
        var f = NewFactory();
        // Two generators worth 8 iron plates each: 16 back on removal.
        Assert.True(f.TryPlaceGenerator(new GridPosition(70, 70)));
        Assert.True(f.TryPlaceGenerator(new GridPosition(74, 70)));
        SetStock(f, Plate, 995);

        Assert.True(f.TryRemoveBuildingAt(new GridPosition(70, 70)));
        Assert.Equal(1000, f.Wallet.MaterialCount(Plate));
        Assert.True(f.TryRemoveBuildingAt(new GridPosition(74, 70)));
        Assert.Equal(1000, f.Wallet.MaterialCount(Plate));
    }

    [Fact]
    public void Refund_OfDeconstructionSite_IsClamped()
    {
        var f = NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120);
        f.UnlockAllForDemo();
        var host = new SimHost(f);
        SetStock(f, Plate, 500);
        host.Submit(new PlaceBuilding(0, BuildingKind.Generator, new GridPosition(64, 60), Direction.East));
        for (var i = 0; i < 60 * SimulationClock.TickRate && f.State.Generators.Count == 0; i++)
        {
            host.AdvanceTicks(1);
        }

        Assert.Single(f.State.Generators);
        SetStock(f, Plate, 995);
        host.Submit(new Remove(0, new GridPosition(64, 60)));
        for (var i = 0; i < 60 * SimulationClock.TickRate && (f.State.Generators.Count > 0 || f.State.Sites.Count > 0); i++)
        {
            host.AdvanceTicks(1);
        }

        Assert.Empty(f.State.Generators);
        Assert.Empty(f.State.Sites);
        Assert.Equal(1000, f.Wallet.MaterialCount(Plate));
    }

    [Fact]
    public void ExistingStockOverCapacity_IsKept()
    {
        var f = NewFactory();
        var line = LineIntoCore(f, 61, length: 3);
        SetStock(f, Iron, 1500);

        RunFeeding(f, line[0], Iron, 5f);

        Assert.Equal(1500, f.Wallet.MaterialCount(Iron));
        Assert.True(f.State.Storage.IsFull(Iron));
        Assert.Equal(0, f.State.Storage.AddClamped(Iron, 10));
        Assert.Equal(1500, f.Wallet.MaterialCount(Iron));
    }

    [Fact]
    public void Storage_AddClampedAndReturn()
    {
        var f = NewFactory();
        var storage = f.State.Storage;
        SetStock(f, Plate, 990);

        Assert.Equal(10, storage.AddClamped(Plate, 25));
        Assert.Equal(1000, f.Wallet.MaterialCount(Plate));
        Assert.False(storage.TryAccept(Plate));
        storage.Return(Plate, 3);
        Assert.Equal(1003, f.Wallet.MaterialCount(Plate));
        Assert.Equal(0, storage.LinkedCapacity);
    }
}
