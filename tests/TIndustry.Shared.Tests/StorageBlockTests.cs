using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Container (2×2, 300 per item) and vault (3×3, 1000): touching the Core they add their space to every item's
/// maximum and belts or buildings feeding them feed the Core; elsewhere they keep their own contents, which the
/// extractor empties and taking them apart loses. A standalone block reached by a growing Core pours into it.
/// </summary>
public sealed class StorageBlockTests
{
    private const int Size = 120;
    private const string Ore = "iron-ore";
    private const string Copper = "copper-ore";
    private const string Plate = "iron-plate";
    private const string Lead = "lead-plate";

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    /// <summary>Shard covers (60..62, 60..62).</summary>
    private static readonly GridPosition Origin = new(60, 60);

    private static readonly TerrainTile Grass = new(FloorKind.Grass, WallKind.None, DepositKind.None);

    private static long nextItemId = 9_000_000;

    /// <summary>
    /// 120×120 grass (or the classic generator's terrain, which a save can rebuild), shard at (60, 60), instant
    /// build, everything researched, no auto-sell.
    /// </summary>
    private static Factory NewFactory(bool grass = true)
    {
        var f = NewGame.Sandbox(Content, Origin, mapWidth: Size, mapHeight: Size, rules: new GameRules(InstantBuild: true));
        if (!grass)
        {
            return Prepare(f);
        }

        var tiles = new TerrainTile[Size, Size];
        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                tiles[x, y] = Grass;
            }
        }

        f.Terrain = TerrainMap.FromTiles(tiles);
        return Prepare(f);
    }

    private static Factory Prepare(Factory f)
    {
        f.UnlockAllForDemo();
        f.Research.ForceUnlock("container");
        f.Research.ForceUnlock("vault");
        f.Research.ForceUnlock(Content.CoreTiers[1].Id);
        // No auto-sell: the stock counts below must not move on their own.
        foreach (var item in f.State.Exchange.Items)
        {
            item.AutoSell = false;
        }

        return f;
    }

    private static BuildSpec ContainerAt(int x, int y) =>
        new("container", BuildingKind.Container, new GridPosition(x, y), Direction.East);

    private static BuildSpec VaultAt(int x, int y) =>
        new("vault", BuildingKind.Vault, new GridPosition(x, y), Direction.East);

    private static StorageBlock Place(Factory f, BuildSpec spec)
    {
        Assert.True(f.PlaceInstant(spec, charge: false), $"{spec.StructureId} at {spec.Origin}");
        return f.Storages.Single(s => s.Origin == spec.Origin);
    }

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

    private static void Fill(StorageBlock storage, string itemId, int amount)
    {
        for (var i = 0; i < amount; i++)
        {
            Assert.True(storage.TryAccept(itemId));
        }
    }

    /// <summary>A straight line of <paramref name="length"/> belts facing <paramref name="dir"/>, from <paramref name="start"/>.</summary>
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

    private static void RunFeeding(Factory f, GridPosition start, Direction dir, string itemId, float seconds)
    {
        var ticks = (int)Math.Round(seconds * SimulationClock.TickRate);
        for (var i = 0; i < ticks; i++)
        {
            f.Belts.TryInsert(start, new TransportedItem(Interlocked.Increment(ref nextItemId), itemId), dir);
            f.Step();
        }
    }

    private static void Run(Factory f, float seconds)
    {
        var ticks = (int)Math.Round(seconds * SimulationClock.TickRate);
        for (var i = 0; i < ticks; i++)
        {
            f.Step();
        }
    }

    [Fact]
    public void LinkedVault_AddsCapacity()
    {
        var f = NewFactory();

        var vault = Place(f, VaultAt(63, 60));
        Assert.True(vault.IsLinked);
        Assert.Equal(BuildingKind.Vault, vault.Kind);
        Assert.Equal(2000, f.CoreCapacity(Ore));

        var container = Place(f, ContainerAt(60, 63));
        Assert.True(container.IsLinked);
        Assert.Equal(2300, f.CoreCapacity(Ore));
        Assert.Equal(2300, f.CoreCapacity(Copper));

        // Touching only the vault (not the Core) does not link.
        var far = Place(f, ContainerAt(66, 60));
        Assert.False(far.IsLinked);
        Assert.Equal(2300, f.CoreCapacity(Ore));
    }

    [Fact]
    public void StoragePlacedThroughCommand_PaysBuildCost()
    {
        var f = NewFactory();
        SetStock(f, Plate, 40);
        SetStock(f, Lead, 20);
        var host = new SimHost(f);

        host.Submit(new PlaceBuilding(1, BuildingKind.Container, new GridPosition(80, 80), Direction.East));
        host.AdvanceTicks(1);

        var container = Assert.Single(f.Storages);
        Assert.Equal(new GridPosition(80, 80), container.Origin);
        Assert.Equal(2, container.Size);
        Assert.Equal(300, container.Capacity);
        Assert.Equal(0, f.Wallet.MaterialCount(Plate));
        Assert.Equal(0, f.Wallet.MaterialCount(Lead));
        Assert.True(f.IsBuildingTile(new GridPosition(81, 81)));
        Assert.False(f.CanOccupy(new GridPosition(81, 80)));
    }

    [Fact]
    public void LinkedStorage_BeltDeliversToCore()
    {
        var f = NewFactory();
        var vault = Place(f, VaultAt(63, 60)); // covers 63..65
        var line = Line(f, new GridPosition(71, 61), Direction.West, 6); // 71..66, last points into the vault (65, 61)
        SetStock(f, Ore, 0);

        RunFeeding(f, line[0], Direction.West, Ore, 5f);

        Assert.True(f.Wallet.MaterialCount(Ore) > 10);
        Assert.Equal(0, vault.Count(Ore));
    }

    [Fact]
    public void LinkedStorage_BeltStopsAtCoreMaximum()
    {
        var f = NewFactory();
        Place(f, ContainerAt(63, 60)); // linked: 1300
        var line = Line(f, new GridPosition(70, 60), Direction.West, 6); // 70..65, last points into the container (64, 60)
        SetStock(f, Ore, 1300);

        RunFeeding(f, line[0], Direction.West, Ore, 3f);

        Assert.Equal(1300, f.Wallet.MaterialCount(Ore));
        Assert.NotNull(f.Belts.Cells[line[^1]].PeekOutput());
    }

    [Fact]
    public void RemovingLinked_LowersCap_KeepsItems()
    {
        var f = NewFactory();
        Place(f, VaultAt(63, 60));
        var line = Line(f, new GridPosition(54, 61), Direction.East, 6); // 54..59, last points into the Core
        SetStock(f, Ore, 1800);

        Assert.True(f.RemoveInstant(new GridPosition(64, 61), refund: true));

        Assert.Empty(f.Storages);
        Assert.Equal(1000, f.CoreCapacity(Ore));
        Assert.Equal(1800, f.Wallet.MaterialCount(Ore));
        RunFeeding(f, line[0], Direction.East, Ore, 3f);
        Assert.Equal(1800, f.Wallet.MaterialCount(Ore));
        Assert.NotNull(f.Belts.Cells[line[^1]].PeekOutput());
    }

    [Fact]
    public void StandaloneContainer_FillsTo300PerItem_ThenStopsBelt()
    {
        var f = NewFactory();
        var container = Place(f, ContainerAt(80, 60));
        Assert.False(container.IsLinked);
        Fill(container, Ore, 290);
        var line = Line(f, new GridPosition(74, 60), Direction.East, 6); // 74..79, last points into (80, 60)
        SetStock(f, Ore, 0);

        RunFeeding(f, line[0], Direction.East, Ore, 5f);

        Assert.Equal(300, container.Count(Ore));
        Assert.False(container.TryAccept(Ore));
        Assert.NotNull(f.Belts.Cells[line[^1]].PeekOutput());
        Assert.Equal(0, f.Wallet.MaterialCount(Ore));
        // Another item still has room.
        Assert.True(container.TryAccept(Copper));
        Assert.Equal(1, container.Count(Copper));
        Assert.Equal([(Copper, 1), (Ore, 300)], container.ContentsInOrder());
    }

    [Fact]
    public void AdjacentBuilding_DeliversIntoStandalone_AndThroughLinkedIntoCore()
    {
        var f = NewFactory();
        var standalone = Place(f, ContainerAt(80, 60));
        Place(f, ContainerAt(63, 60));
        SetStock(f, Ore, 0);

        // A 2×2 footprint at (78, 60) touches the standalone container's west side.
        Assert.True(f.TryDeliverAdjacent(new GridPosition(78, 60), 2, Ore));
        Assert.Equal(1, standalone.Count(Ore));
        Assert.Equal(0, f.Wallet.MaterialCount(Ore));

        // A 1×1 footprint at (65, 60) touches only the linked container: the piece goes to the Core.
        Assert.True(f.TryDeliverAdjacent(new GridPosition(65, 60), 1, Ore));
        Assert.Equal(1, f.Wallet.MaterialCount(Ore));

        // Full standalone: the building keeps the piece.
        Fill(standalone, Ore, 299);
        Assert.False(f.TryDeliverAdjacent(new GridPosition(78, 60), 2, Ore));
    }

    [Fact]
    public void StandaloneContainer_ExtractorEmptiesIt()
    {
        var f = NewFactory();
        var container = Place(f, ContainerAt(80, 60)); // covers 80..81
        Fill(container, Plate, 10);
        var plates = f.Wallet.MaterialCount(Plate);
        Assert.True(f.PlaceInstant(
            new BuildSpec(Extractor.BuildingId, BuildingKind.Extractor, new GridPosition(82, 60), Direction.East, FilterItemId: Plate),
            charge: false));
        var line = Line(f, new GridPosition(83, 60), Direction.East, 12);

        Run(f, 8f);

        Assert.Equal(0, container.Count(Plate));
        Assert.Equal(10, ItemsOn(f, line));
        Assert.Equal(plates, f.Wallet.MaterialCount(Plate));
    }

    [Fact]
    public void StandaloneContainer_ExtractorBlocked_PutsBack()
    {
        var f = NewFactory();
        var container = Place(f, ContainerAt(80, 60));
        Fill(container, Plate, 20);
        var plates = f.Wallet.MaterialCount(Plate);
        Assert.True(f.PlaceInstant(
            new BuildSpec(Extractor.BuildingId, BuildingKind.Extractor, new GridPosition(82, 60), Direction.East, FilterItemId: Plate),
            charge: false));
        // One belt, then a wall of nothing: it fills and blocks.
        var line = Line(f, new GridPosition(83, 60), Direction.East, 1);

        Run(f, 12f);

        var onBelt = ItemsOn(f, line);
        Assert.True(onBelt > 0);
        Assert.True(container.Count(Plate) > 0);
        Assert.Equal(20, container.Count(Plate) + onBelt);
        Assert.Equal(plates, f.Wallet.MaterialCount(Plate));
    }

    [Fact]
    public void Extractor_TouchingCoreAndStandalone_PrefersCore()
    {
        var f = NewFactory();
        var container = Place(f, ContainerAt(64, 61)); // 64..65 × 61..62: not touching the shard (60..62)
        Assert.False(container.IsLinked);
        Fill(container, Plate, 5);
        SetStock(f, Plate, 5);
        // (63, 61) touches the Core (62, 61) and the container (64, 61); it ejects north.
        Assert.True(f.PlaceInstant(
            new BuildSpec(Extractor.BuildingId, BuildingKind.Extractor, new GridPosition(63, 61), Direction.North, FilterItemId: Plate),
            charge: false));
        var line = Line(f, new GridPosition(63, 60), Direction.North, 10);

        Run(f, 1f);

        Assert.True(ItemsOn(f, line) > 0);
        Assert.Equal(5, container.Count(Plate));
        Assert.True(f.Wallet.MaterialCount(Plate) < 5);
    }

    [Fact]
    public void StandaloneDeconstructed_ContentsLost_MaterialsRefunded()
    {
        var f = NewFactory();
        SetStock(f, Plate, 40);
        SetStock(f, Lead, 20);
        SetStock(f, Copper, 0);
        var host = new SimHost(f);
        host.Submit(new PlaceBuilding(1, BuildingKind.Container, new GridPosition(80, 60), Direction.East));
        host.AdvanceTicks(1);
        var container = Assert.Single(f.Storages);
        Fill(container, Copper, 50);
        Assert.Equal(0, f.Wallet.MaterialCount(Plate));

        host.Submit(new Remove(1, new GridPosition(81, 61)));
        host.AdvanceTicks(1);

        Assert.Empty(f.Storages);
        Assert.Equal(40, f.Wallet.MaterialCount(Plate));
        Assert.Equal(20, f.Wallet.MaterialCount(Lead));
        Assert.Equal(0, f.Wallet.MaterialCount(Copper));
        Assert.True(f.CanOccupyFootprint(new GridPosition(80, 60), 2));
    }

    [Fact]
    public void StandaloneBecomesLinked_OnCoreUpgrade_PoursIn()
    {
        var f = NewFactory();
        // 64..65 × 60..61: the foundation's new column x = 63 touches it.
        var container = Place(f, ContainerAt(64, 60));
        Assert.False(container.IsLinked);
        Fill(container, Ore, 300);
        SetStock(f, Ore, 100);

        Assert.True(f.PlaceInstant(f.CoreUpgradeSpec()!, charge: false));

        Assert.Equal(1, f.CoreTier);
        Assert.True(container.IsLinked);
        Assert.Equal(0, container.Count(Ore));
        Assert.Empty(container.ContentsInOrder());
        Assert.Equal(4300, f.CoreCapacity(Ore));
        Assert.Equal(400, f.Wallet.MaterialCount(Ore));
    }

    [Fact]
    public void StandaloneBecomesLinked_PoursUpToMaximum_RestLost()
    {
        var f = NewFactory();
        var container = Place(f, ContainerAt(64, 60));
        Fill(container, Ore, 300);
        Fill(container, Copper, 7);
        SetStock(f, Ore, 4200);
        SetStock(f, Copper, 0);

        f.State.SetCore(Origin, 1);

        Assert.True(container.IsLinked);
        Assert.Equal(4300, f.Wallet.MaterialCount(Ore));
        Assert.Equal(7, f.Wallet.MaterialCount(Copper));
        Assert.Equal(0, container.Count(Ore));
        Assert.False(container.TryAccept(Copper));
    }

    [Fact]
    public void Upgrade_BlockedByLinkedStorageOnGrowthRow()
    {
        var f = NewFactory();
        var container = Place(f, ContainerAt(60, 63)); // the foundation's new row y = 63
        Assert.True(container.IsLinked);
        Content.CoreTiers[1].BuildCost.ToList().ForEach(e => f.Wallet.AddMaterial(e.ItemId, e.Amount));

        Assert.Equal(RefusalCode.CoreUpgradeBlocked, f.CoreUpgradeRefusal()?.Code);
        Assert.False(f.PlaceInstant(f.CoreUpgradeSpec()!, charge: false));
        Assert.Equal(0, f.CoreTier);
    }

    [Fact]
    public void Storages_RoundTripInSave_WithLinksAndContents()
    {
        var f = NewFactory(grass: false);
        Place(f, VaultAt(63, 60));
        var standalone = Place(f, ContainerAt(80, 60));
        Fill(standalone, Copper, 120);
        Fill(standalone, Ore, 3);

        var restored = Factory.Restore(Content, f.Capture());

        Assert.Equal(2, restored.Storages.Count);
        Assert.Equal(f.Storages.Select(s => s.Id), restored.Storages.Select(s => s.Id));
        Assert.Equal(2000, restored.CoreCapacity(Ore));
        var vault = restored.Storages.Single(s => s.Kind == BuildingKind.Vault);
        Assert.True(vault.IsLinked);
        var container = restored.Storages.Single(s => s.Kind == BuildingKind.Container);
        Assert.False(container.IsLinked);
        Assert.Equal([(Copper, 120), (Ore, 3)], container.ContentsInOrder());
        Assert.Equal(
            Snapshot.Checksum(Snapshot.Write(f.State)),
            Snapshot.Checksum(Snapshot.Write(restored.State)));
    }
}
