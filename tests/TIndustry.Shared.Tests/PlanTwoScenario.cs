using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// One factory with all eight blocks of the wider research's second plan (overflow and underflow gate, stack conveyor,
/// long bridge, small and large solar panel, combustion generator, large battery), fed by extractors from the Core, so
/// that every one of them holds live state: gate alternation bits, a pack being formed and packs in transit, an item
/// waiting at a powered long bridge, a mid-burn generator with two fuels and charged batteries.
/// </summary>
internal static class PlanTwoScenario
{
    private const string Iron = "iron-ore";
    private const int InjectEvery = 600;

    private static readonly GridPosition Entry = new(20, 20);

    public static readonly GridPosition Generator = new(20, 21);

    private static void Place(Factory factory, BuildSpec spec) => Assert.True(factory.PlaceInstant(spec, charge: false), spec.ToString());

    private static void Belt(Factory factory, GridPosition at, Direction dir, string id = "conveyor-basic") =>
        Assert.True(factory.Belts.TryPlaceFree(at, dir, factory.Content.RequireConveyor(id)), $"belt {at}");

    public static Factory Build(FactoryContent content, DayNightMode mode = DayNightMode.Cycle)
    {
        var factory = NewGame.Sandbox(
            content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7, startingMoney: 1_000_000,
            rules: new GameRules(InstantBuild: true, DayNight: mode));
        factory.UnlockAllForDemo();
        foreach (var item in factory.State.Exchange.Items)
        {
            item.AutoSell = false;
        }

        foreach (var id in new[] { "overflow-gate", "underflow-gate", "conveyor-stack", "bridge-long", "solar", "solar-large",
                     "combustion-generator", "battery-large", "battery", "extractor" })
        {
            factory.Research.ForceUnlock(id);
        }

        factory.Wallet.AddMaterial(Iron, 250);

        // Gates: an extractor on the Core feeds belts into an overflow gate (front, then the sides), then an underflow gate.
        Place(factory, new BuildSpec(Extractor.BuildingId, BuildingKind.Extractor, new GridPosition(63, 60), Direction.East, FilterItemId: Iron));
        Belt(factory, new GridPosition(64, 60), Direction.East);
        Belt(factory, new GridPosition(65, 60), Direction.East);
        Place(factory, new BuildSpec("overflow-gate", BuildingKind.OverflowGate, new GridPosition(66, 60), Direction.North));
        Belt(factory, new GridPosition(67, 60), Direction.East);
        Place(factory, new BuildSpec("underflow-gate", BuildingKind.UnderflowGate, new GridPosition(68, 60), Direction.North));
        Belt(factory, new GridPosition(69, 60), Direction.East);
        foreach (var x in new[] { 66, 68 })
        {
            Belt(factory, new GridPosition(x, 59), Direction.North);
            Belt(factory, new GridPosition(x, 58), Direction.North);
            Belt(factory, new GridPosition(x, 61), Direction.South);
            Belt(factory, new GridPosition(x, 62), Direction.South);
        }

        // Stack line: extractor -> belts -> loader (53, 61) -> stack cells East into the Core.
        Place(factory, new BuildSpec(Extractor.BuildingId, BuildingKind.Extractor, new GridPosition(59, 62), Direction.West, FilterItemId: Iron));
        for (var x = 58; x >= 53; x--)
        {
            Belt(factory, new GridPosition(x, 62), Direction.West);
        }

        Belt(factory, new GridPosition(52, 62), Direction.North);
        Belt(factory, new GridPosition(52, 61), Direction.East);
        for (var x = 53; x <= 59; x++)
        {
            Place(factory, new BuildSpec("conveyor-stack", null, new GridPosition(x, 61), Direction.East));
        }

        // Power: a combustion generator touching the long bridge's entry, a large and a small battery and both solar panels.
        for (var span = 2; span <= 7; span++)
        {
            Belt(factory, Entry.Step(Direction.East, span), Direction.South);
        }

        Place(factory, new BuildSpec("bridge-long", BuildingKind.BridgeLong, Entry, Direction.East));
        var exit = Entry.Step(Direction.East, 8);
        for (var x = exit.X + 1; x <= exit.X + 3; x++)
        {
            Belt(factory, new GridPosition(x, Entry.Y), Direction.East);
        }

        for (var x = Entry.X - 6; x < Entry.X; x++)
        {
            Belt(factory, new GridPosition(x, Entry.Y), Direction.East);
        }

        Place(factory, new BuildSpec("combustion-generator", BuildingKind.CombustionGenerator, Generator, Direction.East));
        Place(factory, new BuildSpec("battery-large", BuildingKind.BatteryLarge, new GridPosition(22, 21), Direction.East));
        Place(factory, new BuildSpec("battery", BuildingKind.Battery, new GridPosition(20, 23), Direction.East));
        Place(factory, new BuildSpec("solar-large", BuildingKind.SolarLarge, new GridPosition(22, 24), Direction.East));
        Place(factory, new BuildSpec("solar", BuildingKind.Solar, new GridPosition(25, 21), Direction.East));

        Refuel(factory);
        Inject(factory, 0);
        return factory;
    }

    /// <summary>Coal and graphite together: the generator burns the coal first, then the graphite.</summary>
    private static void Refuel(Factory factory)
    {
        var generator = factory.Generators.Single(g => g.Position == Generator);
        for (var i = 0; i < 4; i++)
        {
            generator.TryAcceptFuel("coal");
        }

        for (var i = 0; i < 4; i++)
        {
            generator.TryAcceptFuel("graphite");
        }
    }

    /// <summary>Items on the bridge's feeder belts (deterministic ids from the tick).</summary>
    private static void Inject(Factory factory, long tick)
    {
        long id = 50_000_000 + tick * 16;
        for (var x = Entry.X - 6; x < Entry.X; x++)
        {
            factory.Belts.TryInsert(new GridPosition(x, Entry.Y), new TransportedItem(id++, Iron), Direction.East);
        }
    }

    /// <summary>
    /// Runs to <paramref name="tick"/>; every <see cref="InjectEvery"/> ticks the generator gets fuel and the bridge's feeder
    /// belts get items (the same at any tick, so a game saved and loaded in between goes on identically).
    /// </summary>
    public static void RunTo(Factory factory, long tick)
    {
        while (factory.CurrentTick < tick)
        {
            if (factory.CurrentTick > 0 && factory.CurrentTick % InjectEvery == 0)
            {
                Refuel(factory);
                Inject(factory, factory.CurrentTick);
            }

            factory.Step();
        }
    }
}
