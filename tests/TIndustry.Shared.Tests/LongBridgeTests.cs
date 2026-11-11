using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The long bridge (<c>bridge-long</c>, Mindustry's phase conveyor without the phase): a bridge whose ends stand 2 to 8
/// cells apart (<see cref="ConveyorDefinition.MaxSpan"/>; the plain bridge stays at 4) and whose entry is a power
/// consumer of 1/s (<see cref="BridgePower"/>: an entity with an id, size 1, linked by contact and by node lasers).
/// Without power (satisfaction 0) nothing crosses and the items wait on the entry; with some power they cross.
/// </summary>
public sealed class LongBridgeTests
{
    private const string LongId = "bridge-long";
    private const string Iron = "iron-ore";

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly ConveyorDefinition Belt = Content.RequireConveyor("conveyor-basic");
    private static readonly ConveyorDefinition Plain = Content.RequireConveyor("conveyor-bridge");
    private static readonly ConveyorDefinition Long = Content.RequireConveyor(LongId);

    private static readonly GridPosition Entry = new(20, 20);

    private static Factory Sandbox(GameRules? rules = null)
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7,
            startingMoney: 1_000_000, rules: rules ?? new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        foreach (var item in factory.State.Exchange.Items)
        {
            item.AutoSell = false;
        }

        return factory;
    }

    private static BuildSpec LongSpec(GridPosition entry, Direction direction = Direction.East) =>
        new(LongId, BuildingKind.BridgeLong, entry, direction);

    /// <summary>A long bridge East from <see cref="Entry"/> whose exit is 8 cells ahead (cells 2–7 taken by belts).</summary>
    private static GridPosition PlaceLongEight(Factory factory)
    {
        for (var span = 2; span <= 7; span++)
        {
            Assert.True(factory.Belts.TryPlaceFree(Entry.Step(Direction.East, span), Direction.South, Belt));
        }

        Assert.True(factory.PlaceInstant(LongSpec(Entry), charge: false));
        return Entry.Step(Direction.East, 8);
    }

    /// <summary>A burning generator whose square touches <paramref name="cell"/> from below.</summary>
    private static Generator PowerBelow(Factory factory, GridPosition cell)
    {
        var count = factory.Generators.Count;
        Assert.True(factory.PlaceInstant(
            new BuildSpec(Generator.BuildingId, BuildingKind.Generator, new GridPosition(cell.X, cell.Y + 1), Direction.East),
            charge: false));
        var generator = factory.Generators[count];
        generator.RestoreFuel(Generator.FuelBufferCapacity, Generator.SecondsPerFuel);
        return generator;
    }

    private static TransportedItem NewItem(long id = 9_000_001) => new(id, Iron);

    [Fact]
    public void LongBridgeIsInTheContent()
    {
        Assert.Equal(LogisticsKind.Bridge, Long.Kind);
        Assert.Equal(8, Long.MaxSpan);
        Assert.Equal(1f, Long.PowerUse);
        Assert.Equal(1_000, Long.PowerUseRate);
        Assert.Equal(Plain.RateItemsPerSecond, Long.RateItemsPerSecond);
        Assert.Equal(Plain.Capacity, Long.Capacity);
        Assert.Equal(Plain.ItemSpacing, Long.ItemSpacing);
        Assert.Equal(
            [new ResourceAmount("titanium-plate", 6), new ResourceAmount("lead-plate", 6), new ResourceAmount("silicon", 2)],
            Long.EffectiveBuildCost);

        var structure = Content.FindStructure(LongId);
        Assert.NotNull(structure);
        Assert.Equal("Ponte lungo", structure.DisplayName);
        Assert.Equal(["conveyor-bridge"], structure.Prerequisites);
        Assert.Equal(LongId, BuildSpec.StructureIdFor(BuildingKind.BridgeLong));
        Assert.Equal(BuildingKind.BridgeLong, BuildSpec.KindFor(LongId));
    }

    [Fact]
    public void LongBridgeSpansEight()
    {
        // Bare grid: cells 2–7 ahead taken, the exit lands 8 ahead.
        var grid = new BeltGrid();
        for (var x = 2; x <= 7; x++)
        {
            grid.TryPlaceFree(new GridPosition(x, 0), Direction.South, Belt);
        }

        Assert.True(grid.TryPlaceBridge(new GridPosition(0, 0), Direction.East, Long));
        Assert.Equal(new GridPosition(8, 0), grid.Cells[new GridPosition(0, 0)].BridgePartner);
        Assert.True(BeltGrid.IsBridgeSpan(new GridPosition(0, 0), new GridPosition(8, 0), Long.MaxSpan));
        Assert.False(BeltGrid.IsBridgeSpan(new GridPosition(0, 0), new GridPosition(9, 0), Long.MaxSpan));

        // Cells 2–8 taken: no exit within reach (9 is too far).
        var nine = new BeltGrid();
        for (var x = 2; x <= 8; x++)
        {
            nine.TryPlaceFree(new GridPosition(x, 0), Direction.South, Belt);
        }

        Assert.False(nine.TryPlaceBridge(new GridPosition(0, 0), Direction.East, Long));
        Assert.False(nine.Contains(new GridPosition(9, 0)));

        // In a game: the same, and the refusal of the 9-cell gap.
        var factory = Sandbox();
        var exit = PlaceLongEight(factory);
        Assert.True(factory.Belts.TryGet(Entry, out var entryCell));
        Assert.Equal(exit, entryCell.BridgePartner);
        Assert.Equal(Long, entryCell.Definition);
        Assert.Equal(Long, factory.Belts.Cells[exit].Definition);
        Assert.Equal([Entry, exit], factory.Footprint(LongSpec(Entry)));

        var far = new GridPosition(20, 30);
        for (var span = 2; span <= 8; span++)
        {
            Assert.True(factory.Belts.TryPlaceFree(far.Step(Direction.East, span), Direction.South, Belt));
        }

        Assert.False(factory.CanPlace(LongSpec(far), out var reason));
        Assert.Equal(RefusalCode.Occupied, reason);
        Assert.False(factory.PlaceInstant(LongSpec(far), charge: false));
        Assert.False(factory.Belts.Contains(far));
    }

    [Fact]
    public void OldBridgeStillFour()
    {
        Assert.Equal(4, Plain.MaxSpan);
        Assert.Equal(0f, Plain.PowerUse);
        Assert.Equal(2, BeltGridCell.MinBridgeSpan);

        var factory = Sandbox();
        for (var span = 2; span <= 3; span++)
        {
            Assert.True(factory.Belts.TryPlaceFree(Entry.Step(Direction.East, span), Direction.South, Belt));
        }

        var spec = new BuildSpec("conveyor-bridge", BuildingKind.Bridge, Entry, Direction.East);
        Assert.True(factory.PlaceInstant(spec, charge: false));
        Assert.Equal(Entry.Step(Direction.East, 4), factory.Belts.Cells[Entry].BridgePartner);
        Assert.Empty(factory.BridgePowers);

        var far = new GridPosition(20, 30);
        for (var span = 2; span <= 4; span++)
        {
            Assert.True(factory.Belts.TryPlaceFree(far.Step(Direction.East, span), Direction.South, Belt));
        }

        Assert.False(factory.PlaceInstant(spec with { Origin = far }, charge: false));
        Assert.False(BeltGrid.IsBridgeSpan(new GridPosition(0, 0), new GridPosition(5, 0), Plain.MaxSpan));

        // An item crosses a plain bridge without any power.
        Assert.True(factory.Belts.TryInsert(Entry, NewItem(), Direction.East));
        Assert.Empty(factory.Belts.Cells[Entry].Items);
        Assert.Single(factory.Belts.Cells[Entry.Step(Direction.East, 4)].Items);
    }

    [Fact]
    public void LongBridgeCostsTwoEndsAndIsResearchedAfterTheBridge()
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7,
            startingMoney: 1_000_000, rules: new GameRules(InstantBuild: true));
        var host = new SimHost(factory);
        var rejected = new List<RefusalCode>();
        host.CommandRejected += (_, r) => rejected.Add(r.Code);
        host.Submit(new PlaceBuilding(1, BuildingKind.BridgeLong, Entry, Direction.East));
        host.AdvanceTicks(1);
        Assert.Equal([RefusalCode.Locked], rejected);

        factory.Research.ForceUnlock(LongId);
        foreach (var (item, amount) in new[] { ("titanium-plate", 12), ("lead-plate", 12), ("silicon", 4) })
        {
            factory.Wallet.AddMaterial(item, amount);
        }

        var before = new[] { "titanium-plate", "lead-plate", "silicon" }.Select(factory.Wallet.MaterialCount).ToArray();
        host.Submit(new PlaceBuilding(1, BuildingKind.BridgeLong, Entry, Direction.East));
        host.AdvanceTicks(1);
        Assert.Single(rejected);
        Assert.True(factory.Belts.TryGet(Entry, out var cell) && cell.Definition.Id == LongId);
        Assert.Equal(
            [before[0] - 12, before[1] - 12, before[2] - 4],
            new[] { "titanium-plate", "lead-plate", "silicon" }.Select(factory.Wallet.MaterialCount).ToArray());
        Assert.Single(factory.BridgePowers);

        // Taken away: both ends, its power entity and the refund of both ends.
        Assert.True(factory.RemoveInstant(Entry.Step(Direction.East, 2), refund: true));
        Assert.Empty(factory.Belts.Cells);
        Assert.Empty(factory.BridgePowers);
        Assert.Equal(before, new[] { "titanium-plate", "lead-plate", "silicon" }.Select(factory.Wallet.MaterialCount).ToArray());
    }

    [Fact]
    public void LongBridgeNeedsPower()
    {
        var factory = Sandbox();
        var exit = PlaceLongEight(factory);

        // No network: the item waits on the entry, nothing crosses and nothing is lost.
        Assert.True(factory.Belts.TryInsert(Entry, NewItem(), Direction.East));
        factory.RunTicks(60);
        Assert.Single(factory.Belts.Cells[Entry].Items);
        Assert.Empty(factory.Belts.Cells[exit].Items);
        var power = Assert.Single(factory.BridgePowers);
        Assert.Equal(0, power.SatisfactionBp);

        // A burning generator against the entry: the item crosses.
        PowerBelow(factory, Entry);
        factory.RunTicks(2);
        Assert.Equal(10_000, power.SatisfactionBp);
        Assert.Empty(factory.Belts.Cells[Entry].Items);
        Assert.Single(factory.Belts.Cells[exit].Items);
    }

    [Fact]
    public void PartialPowerStillLetsItemsCross()
    {
        var factory = Sandbox(new GameRules(InstantBuild: true, DayNight: DayNightMode.AlwaysNight));
        var exit = PlaceLongEight(factory);
        var generator = PowerBelow(factory, Entry);

        // 41 lamps on the generator's network at night ask more than it makes: satisfaction below full, above 0.
        for (var i = 0; i < 40; i++)
        {
            Assert.True(factory.PlaceInstant(
                new BuildSpec(Lamp.BuildingId, BuildingKind.Lamp, new GridPosition(Entry.X + 2 + i, Entry.Y + 3), Direction.East),
                charge: false));
        }

        Assert.True(factory.PlaceInstant(
            new BuildSpec(Lamp.BuildingId, BuildingKind.Lamp, new GridPosition(Entry.X + 2, Entry.Y + 2), Direction.East),
            charge: false));
        factory.RunTicks(2);
        var power = Assert.Single(factory.BridgePowers);
        Assert.Same(factory.PowerGrid.NetworkOf(generator.Id), factory.PowerGrid.NetworkOf(power.Id));
        Assert.InRange(power.SatisfactionBp, 1, 9_999);

        Assert.True(factory.Belts.TryInsert(Entry, NewItem(), Direction.East));
        Assert.Empty(factory.Belts.Cells[Entry].Items);
        Assert.Single(factory.Belts.Cells[exit].Items);
    }

    [Fact]
    public void LongBridgeIsAPowerConsumer()
    {
        var factory = Sandbox();
        PlaceLongEight(factory);
        var power = Assert.Single(factory.BridgePowers);
        Assert.Equal(Entry, power.Entry);
        Assert.Contains(new PowerBlock(power.Id, Entry, 1, PowerBlockKind.Consumer), factory.PowerBlocks);
        Assert.True(factory.TryGetSpecAt(Entry, out var spec, out var entityId));
        Assert.Equal(LongSpec(Entry), spec);
        Assert.Equal(power.Id, entityId);
        Assert.Equal(power.Id, factory.PowerBlockAt(Entry)?.Id);

        // Its network asks 1/s more while it stands, even with nothing on it (an idle bridge still draws).
        var generator = PowerBelow(factory, Entry);
        factory.RunTicks(2);
        var network = factory.PowerGrid.NetworkOf(generator.Id)!;
        Assert.Same(network, factory.PowerGrid.NetworkOf(power.Id));
        Assert.Equal(1_000, network.RequestedRate);
        Assert.True(power.PowerRequested);

        // Taken away, the network asks nothing.
        Assert.True(factory.RemoveInstant(Entry, refund: false));
        Assert.Empty(factory.BridgePowers);
        factory.RunTicks(2);
        Assert.Equal(0, factory.PowerGrid.NetworkOf(generator.Id)!.RequestedRate);
    }

    [Fact]
    public void NodeLaserLinksLongBridge()
    {
        var factory = Sandbox();
        var host = new SimHost(factory);
        var exit = PlaceLongEight(factory);
        var power = Assert.Single(factory.BridgePowers);

        // A node 3 cells below the entry, a burning generator against the node: the node's laser reaches the entry.
        var nodeCell = new GridPosition(Entry.X, Entry.Y + 4);
        Assert.True(factory.PlaceInstant(
            new BuildSpec(PowerNode.Tier1Id, BuildingKind.PowerNode, nodeCell, Direction.East), charge: false));
        var node = factory.PowerNodes.Single(n => n.Position == nodeCell);
        Assert.True(factory.PowerGrid.HasLink(node.Id, power.Id));
        PowerBelow(factory, nodeCell);

        // The player's click takes the laser away: no power, the item waits.
        host.Submit(new TogglePowerLink(1, node.Id, power.Id));
        host.AdvanceTicks(2);
        Assert.False(factory.PowerGrid.HasLink(node.Id, power.Id));
        Assert.True(factory.Belts.TryInsert(Entry, NewItem(), Direction.East));
        host.AdvanceTicks(10);
        Assert.Single(factory.Belts.Cells[Entry].Items);

        // And back: the item crosses.
        host.Submit(new TogglePowerLink(1, node.Id, power.Id));
        host.AdvanceTicks(3);
        Assert.True(factory.PowerGrid.HasLink(node.Id, power.Id));
        Assert.Empty(factory.Belts.Cells[Entry].Items);
        Assert.Single(factory.Belts.Cells[exit].Items);
    }

    [Fact]
    public void LongBridgeSurvivesSave()
    {
        var factory = Sandbox();
        var exit = PlaceLongEight(factory);
        var power = Assert.Single(factory.BridgePowers);
        var nodeCell = new GridPosition(Entry.X, Entry.Y + 4);
        Assert.True(factory.PlaceInstant(
            new BuildSpec(PowerNode.Tier1Id, BuildingKind.PowerNode, nodeCell, Direction.East), charge: false));
        var node = factory.PowerNodes.Single(n => n.Position == nodeCell);
        Assert.True(factory.PowerGrid.HasLink(node.Id, power.Id));

        // No power: an item waits on the entry.
        var item = NewItem();
        Assert.True(factory.Belts.TryInsert(Entry, item, Direction.East));
        factory.RunTicks(5);

        var data = factory.Capture();
        var saved = data.Belts.Single(b => b.X == Entry.X && b.Y == Entry.Y);
        Assert.Equal(power.Id, saved.PowerId);
        Assert.Null(data.Belts.Single(b => b.X == exit.X && b.Y == exit.Y).PowerId);
        Assert.All(data.Belts.Where(b => b.DefinitionId != LongId), b => Assert.Null(b.PowerId));

        var json = JsonSerializer.Serialize(data, FactorySaveStore.JsonOptions);
        var restored = Factory.Restore(Content, JsonSerializer.Deserialize<FactorySaveData>(json, FactorySaveStore.JsonOptions)!);
        Assert.Equal(json, JsonSerializer.Serialize(restored.Capture(), FactorySaveStore.JsonOptions));
        Assert.True(restored.Belts.TryGet(Entry, out var entryCell));
        Assert.Equal(LongId, entryCell.Definition.Id);
        Assert.Equal(exit, entryCell.BridgePartner);
        Assert.Equal(Entry, restored.Belts.Cells[exit].BridgePartner);
        var restoredPower = Assert.Single(restored.BridgePowers);
        Assert.Equal(power.Id, restoredPower.Id);
        Assert.Equal(Entry, restoredPower.Entry);
        Assert.True(restored.PowerGrid.HasLink(node.Id, power.Id));
        Assert.Equal(item.Id, Assert.Single(entryCell.Items).Id);
        Assert.True(restored.NextEntityId > power.Id);

        // Through the snapshot too, and both copies go on the same way.
        var copy = new Factory(Snapshot.Read(Content, Snapshot.Write(factory.State)));
        Assert.Equal(Snapshot.Checksum(Snapshot.Write(factory.State)), Snapshot.Checksum(Snapshot.Write(copy.State)));

        // Power comes: the waiting item crosses in the restored game.
        PowerBelow(restored, nodeCell);
        restored.RunTicks(3);
        Assert.Empty(restored.Belts.Cells[Entry].Items);
        Assert.Equal(item.Id, Assert.Single(restored.Belts.Cells[exit].Items).Id);
    }

    [Fact]
    public void APoweredBridgeGoesOnTheSameAfterALoad()
    {
        // Items flowing over a powered long bridge: the belts of a tick move before its balance, so a loaded copy must
        // start from the saved share of power or its first tick would hold the items back.
        var factory = Sandbox();
        var exit = PlaceLongEight(factory);
        PowerBelow(factory, Entry);
        long id = 1;
        for (var x = Entry.X - 6; x < Entry.X; x++)
        {
            var at = new GridPosition(x, Entry.Y);
            Assert.True(factory.Belts.TryPlaceFree(at, Direction.East, Belt));
            factory.Belts.Cells[at].RestoreState([new TransportedItem(id++, Iron, 0.9f), new TransportedItem(id++, Iron, 0.3f)]);
        }

        for (var x = exit.X + 1; x <= exit.X + 6; x++)
        {
            Assert.True(factory.Belts.TryPlaceFree(new GridPosition(x, Entry.Y), Direction.East, Belt));
        }

        factory.RunTicks(30);
        var power = Assert.Single(factory.BridgePowers);
        Assert.Equal(10_000, power.SatisfactionBp);
        Assert.Equal(10_000, factory.Capture().Belts.Single(b => b.X == Entry.X && b.Y == Entry.Y).PowerSatisfactionBp);

        var copy = new Factory(Snapshot.Read(Content, Snapshot.Write(factory.State)));
        Assert.Equal(10_000, Assert.Single(copy.BridgePowers).SatisfactionBp);
        for (var tick = 0; tick < 120; tick++)
        {
            factory.Step();
            copy.Step();
            Assert.Equal(Snapshot.Checksum(Snapshot.Write(factory.State)), Snapshot.Checksum(Snapshot.Write(copy.State)));
        }

        Assert.NotEmpty(factory.Belts.Cells[exit.Step(Direction.East, 6)].Items);
    }

    [Fact]
    public void EndsOfDifferentBridgesDoNotPairInASave()
    {
        var factory = Sandbox();
        var exit = PlaceLongEight(factory);
        var data = factory.Capture();
        // A hand-edited save: the exit turned into a plain bridge end (a long entry with a short exit).
        data.Belts.Single(b => b.X == exit.X && b.Y == exit.Y).DefinitionId = "conveyor-bridge";
        var restored = Factory.Restore(Content, data);
        Assert.False(restored.Belts.Contains(Entry));
        Assert.False(restored.Belts.Contains(exit));
        Assert.Empty(restored.BridgePowers);
    }

    [Theory]
    [InlineData("exit")]
    [InlineData("plain")]
    [InlineData("share")]
    public void PowerOnACellThatIsNotALongEntryMakesTheSaveUnreadable(string change)
    {
        var factory = Sandbox();
        var exit = PlaceLongEight(factory);
        Assert.True(factory.PlaceInstant(
            new BuildSpec("conveyor-bridge", BuildingKind.Bridge, new GridPosition(20, 30), Direction.East), charge: false));
        var data = factory.Capture();
        var entry = data.Belts.Single(b => b.X == Entry.X && b.Y == Entry.Y);
        switch (change)
        {
            case "exit": data.Belts.Single(b => b.X == exit.X && b.Y == exit.Y).PowerId = 777; break;
            case "plain": data.Belts.Single(b => b.X == 20 && b.Y == 30).PowerId = 777; break;
            case "share": entry.PowerSatisfactionBp = 10_001; break;
        }

        Assert.Throws<InvalidDataException>(() => Factory.Restore(Content, data));
    }

    [Fact]
    public void ALongBridgeFromASiteGetsItsPower()
    {
        var wallet = new EconomyWallet(
            0,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["titanium-plate"] = 50, ["lead-plate"] = 50, ["silicon"] = 50
            });
        var factory = new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(new GridPosition(20, 20)), wallet);
        factory.Research.ForceUnlock(LongId);
        factory.AddBuilder(1);
        var host = new SimHost(factory);
        var rejected = new List<RefusalCode>();
        host.CommandRejected += (_, r) => rejected.Add(r.Code);
        var entry = new GridPosition(24, 28);
        host.Submit(new PlaceBuilding(1, BuildingKind.BridgeLong, entry, Direction.East));
        host.AdvanceTicks(1);
        var site = Assert.Single(factory.Sites);
        Assert.Equal([entry, entry.Step(Direction.East, 2)], site.Tiles);
        for (var tick = 0; tick < 6_000 && !factory.Belts.Contains(entry); tick++)
        {
            host.AdvanceTicks(1);
        }

        Assert.Empty(rejected);
        Assert.True(factory.Belts.TryGet(entry, out var cell));
        Assert.Equal(LongId, cell.Definition.Id);
        var power = Assert.Single(factory.BridgePowers);
        Assert.Equal(entry, power.Entry);
        Assert.Equal(50 - 12, factory.Wallet.MaterialCount("titanium-plate"));
    }

    [Fact]
    public void ADraggedLineBridgesOnlyWithThePlainBridge()
    {
        var factory = Sandbox();
        var line = PlacementLine.Plan(factory, new LineRequest(
            1, LongId, Entry, Entry.Step(Direction.East, 5), Direction.East));
        var placement = Assert.Single(line, p => p.Command is not null);
        Assert.Equal(new PlaceBuilding(1, BuildingKind.BridgeLong, Entry, Direction.East), placement.Command);
    }
}
