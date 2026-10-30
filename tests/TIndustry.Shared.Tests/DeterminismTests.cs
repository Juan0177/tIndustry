using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Determinism foundations: fixed tick, frame-rate independence, canonical iteration order,
/// stable building ids across save/restore.
/// </summary>
public sealed class DeterminismTests
{
    private static readonly string ContentPath =
        Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    /// <summary>
    /// Iron ore delivered to the CORE in 10 s of play by one T1 miner touching the CORE,
    /// measured on the variable-step code before the fixed tick: 300 × <c>Tick(1f / 30f)</c> → 4.
    /// </summary>
    private const long DeliveredInTenSecondsBeforeFixedTick = 4;

    private static FactoryContent LoadContent() => FactoryContent.Load(ContentPath);

    private static string Serialize(Factory factory) =>
        JsonSerializer.Serialize(factory.Capture(), FactorySaveStore.JsonOptions);

    [Fact]
    public void StepAdvancesExactlyOneTick()
    {
        var factory = NewGame.Demo(LoadContent());
        Assert.Equal(0, factory.CurrentTick);
        factory.Step();
        Assert.Equal(1, factory.CurrentTick);
        factory.Step();
        Assert.Equal(2, factory.CurrentTick);
    }

    [Fact]
    public void LongFrameIsCappedAtMaxTicks()
    {
        var clock = new SimulationClock();
        Assert.Equal(SimulationClock.MaxTicksPerFrame, clock.Accumulate(0.2));
        Assert.Equal(5, SimulationClock.MaxTicksPerFrame);
        Assert.Equal(0, clock.Accumulate(0));
    }

    [Fact]
    public void FrameRateDoesNotChangeTheOutcome()
    {
        const long target = 1200;
        var content = LoadContent();

        var a = NewGame.Demo(content);
        var clockA = new SimulationClock();
        while (a.CurrentTick < target)
        {
            var ticks = clockA.Accumulate(1.0 / 60.0);
            for (var i = 0; i < ticks && a.CurrentTick < target; i++)
            {
                a.Step();
            }
        }

        var b = NewGame.Demo(content);
        var clockB = new SimulationClock();
        var random = new Random(42);
        while (b.CurrentTick < target)
        {
            var frame = 0.004 + random.NextDouble() * (0.05 - 0.004);
            var ticks = clockB.Accumulate(frame);
            for (var i = 0; i < ticks && b.CurrentTick < target; i++)
            {
                b.Step();
            }
        }

        Assert.Equal(target, a.CurrentTick);
        Assert.Equal(target, b.CurrentTick);
        // 20 s is not enough for ore to reach the CORE on the long L path, but items must be in flight.
        Assert.True(a.Miners[0].ItemsProduced > 0, "the demo must actually produce something");
        Assert.Equal(Serialize(a), Serialize(b));
    }

    [Fact]
    public void PlacementOrderDoesNotChangeOutcome()
    {
        var content = LoadContent();
        var forward = BuildMinerLine(content, reverse: false);
        var backward = BuildMinerLine(content, reverse: true);

        forward.RunTicks(1200);
        backward.RunTicks(1200);

        Assert.True(forward.CoreDeliveredItems > 0, "the line must actually deliver something");
        Assert.Equal(Serialize(forward), Serialize(backward));
    }

    /// <summary>
    /// Belts full of items carry movement between cells within a tick (<see cref="TransportedItem.Carry"/>, not saved):
    /// a game saved and loaded at any tick must go on exactly like one that never saved. Saturated mixed tiers into the
    /// Core, a bridge, a junction with a crossing line, a sorter with side outputs and side merges; no item enters from
    /// outside the simulation.
    /// </summary>
    [Fact]
    public void SaturatedBeltsGoOnTheSameAfterSaveAndLoad()
    {
        var content = LoadContent();
        const int total = 1500;
        var reference = BusyBelts(content);
        reference.RunTicks(total);
        Assert.True(reference.CoreDeliveredItems > 80, $"delivered {reference.CoreDeliveredItems}");
        var expected = Serialize(reference);

        foreach (var saveAt in new[] { 1, 97, 333, 800, 1201 })
        {
            var run = BusyBelts(content);
            run.RunTicks(saveAt);
            var loaded = Factory.Restore(content, run.Capture());
            loaded.RunTicks(total - saveAt);
            Assert.True(expected == Serialize(loaded), $"saved at tick {saveAt}");
        }
    }

    /// <summary>Main line East on y 30 into the Core (30..32), every belt cell full (3 items) at the start.</summary>
    private static Factory BusyBelts(FactoryContent content)
    {
        var factory = NewGame.Sandbox(content, new GridPosition(30, 30), mapWidth: 60, mapHeight: 60,
            rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        factory.Research.ForceUnlock("conveyor-armored");
        factory.Research.ForceUnlock("conveyor-express");
        foreach (var material in new[] { "iron-ore", "lead-ore", "titanium-ore", "graphite", "silicon" })
        {
            factory.Wallet.AddMaterial(material, 10_000);
        }

        var tiers = new[] { "conveyor-basic", "conveyor-express", "conveyor-fast", "conveyor-armored" };
        void Belt(int x, int y, Direction dir, string? id = null) =>
            Assert.True(factory.TryPlaceBelt(new GridPosition(x, y), dir, id ?? tiers[x / 4 % 4]), $"belt {x},{y}");

        Assert.True(factory.TryPlaceBridge(new GridPosition(8, 30), Direction.East)); // exit (10, 30), 9 stays empty
        Assert.True(factory.TryPlaceJunction(new GridPosition(14, 30), Direction.East));
        Assert.True(factory.TryPlaceSorter(new GridPosition(20, 30), Direction.East, "iron-ore"));
        foreach (var x in Enumerable.Range(2, 28).Where(x => x is not (8 or 9 or 10 or 14 or 20)))
        {
            Belt(x, 30, Direction.East);
        }

        for (var y = 36; y >= 31; y--)
        {
            Belt(14, y, Direction.North, "conveyor-basic"); // crosses the junction
            Belt(24, y, Direction.North, "conveyor-fast"); // side merge from the south
        }

        for (var y = 29; y >= 26; y--)
        {
            Belt(14, y, Direction.North, "conveyor-basic");
            Belt(20, y, Direction.North, "conveyor-basic"); // the sorter's lead, aside
        }

        for (var y = 24; y <= 29; y++)
        {
            Belt(24, y, Direction.South, "conveyor-express"); // side merge from the north
        }

        for (var y = 31; y <= 33; y++)
        {
            Belt(20, y, Direction.South, "conveyor-basic");
        }

        // Built: no iron or lead left in the Core, so its maximum never stops the lines here.
        foreach (var material in new[] { "iron-ore", "lead-ore" })
        {
            Assert.True(factory.Wallet.TryRemoveMaterial(material, factory.Wallet.MaterialCount(material)));
        }

        long id = 100_000;
        foreach (var cell in factory.Belts.Cells.Values.Where(c => c.Kind == LogisticsKind.Belt))
        {
            // Lead only at the start of the main line: the sorter puts it aside, its dead ends hold all of it.
            var item = cell.Position is { Y: 30, X: <= 5 } ? "lead-ore" : "iron-ore";
            cell.RestoreState(0, new[] { 1f, 0.6f, 0.2f }.Select(p => new TransportedItem(id++, item, p)));
        }

        return factory;
    }

    [Fact]
    public void SaveRestoreMidGameKeepsSimulationIdentical()
    {
        var content = LoadContent();
        var original = Factory.CreateSorterBridgeDemo(content);
        // A battery against the generator: by tick 700 it holds charge, so the round trip also has to keep it.
        original.Research.ForceUnlock(Battery.BuildingId);
        var gen = original.Generators[0].Position;
        var placed = false;
        for (var k = 0; k < Generator.Size && !placed; k++)
        {
            foreach (var tile in new[]
                     {
                         new GridPosition(gen.X + k, gen.Y - 1), new GridPosition(gen.X + k, gen.Y + Generator.Size),
                         new GridPosition(gen.X - 1, gen.Y + k), new GridPosition(gen.X + Generator.Size, gen.Y + k)
                     })
            {
                if (original.PlaceInstant(new BuildSpec(Battery.BuildingId, BuildingKind.Battery, tile, Direction.East), charge: false))
                {
                    placed = true;
                    break;
                }
            }
        }

        Assert.True(placed, "battery against the generator");
        original.RunTicks(700);
        Assert.True(original.Batteries[0].Charge > 0, "power in play at restore");

        var copy = Factory.Restore(content, original.Capture());
        Assert.Equal(Serialize(original), Serialize(copy));

        original.RunTicks(300);
        copy.RunTicks(300);

        Assert.Equal(1000, copy.CurrentTick);
        Assert.Equal(Serialize(original), Serialize(copy));
    }

    /// <summary>
    /// A game with a shard on the classic map, a linked vault, a standalone container with copper, auto-sell (one item
    /// only when full) and a foundation built by the drone, driven by the same commands at the same ticks.
    /// </summary>
    private static (SimHost Host, List<(long Tick, SimCommand Command)> Script) UpgradeStoragesAutoSellGame(FactoryContent content)
    {
        var origin = new GridPosition(60, 60);
        var factory = NewGame.Sandbox(content, origin, mapWidth: 120, mapHeight: 120, seed: 42, rules: new GameRules());
        factory.UnlockAllForDemo();
        factory.Research.ForceUnlock("container");
        factory.Research.ForceUnlock("vault");
        factory.Research.ForceUnlock(content.CoreTiers[1].Id);
        foreach (var entry in content.CoreTiers[1].BuildCost)
        {
            factory.Wallet.AddMaterial(entry.ItemId, entry.Amount);
        }

        factory.Wallet.AddMaterial("iron-ore", 1000);
        factory.Wallet.AddMaterial("titanium-plate", 200);
        factory.Wallet.AddMaterial("lead-plate", 200);
        factory.Wallet.AddMaterial("iron-plate", 200);
        factory.Wallet.AddMaterial("coal", 300);
        Assert.True(factory.PlaceInstant(
            new BuildSpec("container", BuildingKind.Container, new GridPosition(80, 60), Direction.East), charge: false));
        var container = factory.Storages.Single();
        for (var i = 0; i < 120; i++)
        {
            Assert.True(container.TryAccept("copper-ore"));
        }

        var script = new List<(long Tick, SimCommand Command)>
        {
            (5, new PlaceBuilding(0, BuildingKind.CoreFoundation, origin, Direction.East)),
            (10, new SetAutoSell(0, "iron-ore", true)),
            (10, new SetAutoSellWhenFull(0, "iron-ore", true)),
            (20, new SetAutoSell(0, "coal", true)),
            (30, new PlaceBuilding(0, BuildingKind.Vault, new GridPosition(57, 60), Direction.East)),
            (50, new PlaceBuilding(0, BuildingKind.Extractor, new GridPosition(82, 60), Direction.East)),
            (60, new PlaceBelt(0, new GridPosition(83, 60), Direction.East, "conveyor-basic")),
            (4000, new SetAutoSellWhenFull(0, "iron-ore", false)),
            (9000, new Sell(0, "titanium-plate", 5))
        };
        return (new SimHost(factory), script);
    }

    [Fact]
    public void Determinism_UpgradeStoragesAutoSell()
    {
        const int minute = 60 * SimulationClock.TickRate;
        var content = LoadContent();
        var (a, script) = UpgradeStoragesAutoSellGame(content);
        var (b, _) = UpgradeStoragesAutoSellGame(content);
        var refusedA = new List<string>();
        var refusedB = new List<string>();
        a.CommandRejected += (c, r) => refusedA.Add($"{c}:{r.Code}");
        b.CommandRejected += (c, r) => refusedB.Add($"{c}:{r.Code}");
        var checksums = new List<(ulong A, ulong B)>();
        // Five minutes (the drone finishes the foundation within them), checksums every minute.
        for (var tick = 0L; tick < 5 * minute; tick++)
        {
            foreach (var (when, command) in script)
            {
                if (when == tick)
                {
                    a.Submit(command);
                    b.Submit(command);
                }
            }

            a.AdvanceTicks(1);
            b.AdvanceTicks(1);
            if ((tick + 1) % minute == 0)
            {
                checksums.Add((a.Checksum(), b.Checksum()));
            }
        }

        Assert.Equal(5, checksums.Count);
        Assert.All(checksums, c => Assert.Equal(c.A, c.B));
        Assert.Equal(5, checksums.Select(c => c.A).Distinct().Count());
        Assert.Empty(refusedA);
        Assert.Equal(refusedA, refusedB);
        // Everything the script asks for happened: the foundation is built, the vault linked, auto-sell sold.
        Assert.True(a.State.CoreTier == 1, $"foundation built (sites: {string.Join(", ", a.State.Sites.Select(x => $"{x.Spec.StructureId} {x.Progress:0.00}"))})");
        Assert.Contains(a.State.Storages, s => s.Kind == BuildingKind.Vault && s.IsLinked);
        Assert.Contains(a.State.Storages, s => s.Kind == BuildingKind.Container && !s.IsLinked);
        Assert.True(a.State.Session.SoldByItem.GetValueOrDefault("iron-ore") > 0, "auto-sell sold iron ore");
    }

    [Fact]
    public void GameplayRatesMatchWallClock()
    {
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14));
        var factory = new Factory(LoadContent(), new BeltGrid(), core);
        factory.UnlockAllForDemo();
        // T1 miner on iron (no terrain = full deposit) touching the CORE west edge.
        Assert.True(factory.TryPlaceMiner(new GridPosition(7, 14), Direction.East));

        factory.RunSeconds(10f);

        Assert.InRange(
            factory.CoreDeliveredItems,
            DeliveredInTenSecondsBeforeFixedTick - 1,
            DeliveredInTenSecondsBeforeFixedTick + 1);
    }

    /// <summary>Miner (2,13) → belts (4..8, 14) east → CORE (9,14), placed in either order.</summary>
    private static Factory BuildMinerLine(FactoryContent content, bool reverse)
    {
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14));
        var factory = new Factory(content, new BeltGrid(), core);
        factory.UnlockAllForDemo();

        var belts = Enumerable.Range(4, 5).Select(x => new GridPosition(x, 14)).ToList();
        if (reverse)
        {
            belts.Reverse();
            foreach (var pos in belts)
            {
                Assert.True(factory.TryPlaceBelt(pos, Direction.East));
            }

            Assert.True(factory.TryPlaceMiner(new GridPosition(2, 13), Direction.East));
        }
        else
        {
            Assert.True(factory.TryPlaceMiner(new GridPosition(2, 13), Direction.East));
            foreach (var pos in belts)
            {
                Assert.True(factory.TryPlaceBelt(pos, Direction.East));
            }
        }

        return factory;
    }
}
