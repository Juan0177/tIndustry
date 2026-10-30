using System.Text.Json.Nodes;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// G3 3/4: the Lamp (1×1 power consumer, lit only while it gets power). The world's time it lights up is in
/// <see cref="DayNightTests"/> and, saved and in the campaign, in <see cref="DayNightSaveTests"/>.
/// </summary>
public sealed class LampAndNightTests
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly FactoryContent Content = FactoryContent.Load(Path.Combine(DataDir, "content.json"));

    /// <summary>Lamps want to work only while the world's light is below half: these tests play at night.</summary>
    private static readonly GameRules Night = new(InstantBuild: true, DayNight: DayNightMode.AlwaysNight);

    private static Factory Sandbox(GameRules? rules = null)
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7,
            rules: rules ?? Night);
        factory.Research.ForceUnlock(Lamp.BuildingId);
        factory.Research.ForceUnlock(Generator.BuildingId);
        factory.Research.ForceUnlock(PowerNode.Tier1Id);
        return factory;
    }

    [Fact]
    public void LampIsInTheContent()
    {
        var structure = Content.FindStructure(Lamp.BuildingId);
        Assert.NotNull(structure);
        Assert.Equal("Lampada", structure.DisplayName);
        Assert.Equal([PowerNode.Tier1Id], structure.Prerequisites);
        Assert.False(structure.UnlockedByDefault);

        var building = Content.FindBuilding(Lamp.BuildingId);
        Assert.NotNull(building);
        Assert.Equal(
            [new ResourceAmount("iron-plate", 2), new ResourceAmount("copper-wire", 1)],
            building.BuildCost);
        Assert.Equal(1f, building.PowerUse);
        Assert.Equal(BuildingKind.Lamp, Enum.Parse<BuildingKind>("Lamp"));
        Assert.Equal(Lamp.BuildingId, BuildSpec.StructureIdFor(BuildingKind.Lamp));
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("negative")]
    [InlineData("missing")]
    [InlineData("no-building")]
    public void LampWithoutPowerUseIsRejected(string change)
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(DataDir, "content.json")))!;
        var buildings = root["buildings"]!.AsArray();
        var lamp = buildings.First(b => (string?)b!["id"] == Lamp.BuildingId)!;
        switch (change)
        {
            case "zero": lamp["powerUse"] = 0; break;
            case "negative": lamp["powerUse"] = -1; break;
            case "missing": lamp.AsObject().Remove("powerUse"); break;
            case "no-building": buildings.Remove(lamp); break;
        }

        var dir = Directory.CreateTempSubdirectory("tindustry-content-");
        try
        {
            var path = Path.Combine(dir.FullName, "content.json");
            File.WriteAllText(path, root.ToJsonString());
            var error = Assert.Throws<InvalidDataException>(() => FactoryContent.Load(path));
            Assert.Contains("la Lampada deve avere un consumo di corrente", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void LampConsumesPowerAndIsBuildable()
    {
        var factory = Sandbox();
        var host = new SimHost(factory);
        factory.Wallet.AddMaterial("iron-plate", 20);
        factory.Wallet.AddMaterial("copper-wire", 10);
        var plates = factory.Wallet.MaterialCount("iron-plate");
        var wire = factory.Wallet.MaterialCount("copper-wire");

        // Built through a command like every block: it charges its cost and occupies one cell.
        host.Submit(new PlaceBuilding(1, BuildingKind.Lamp, new GridPosition(20, 20), Direction.East));
        host.Advance(SimulationClock.TickSeconds);
        var lamp = Assert.Single(factory.Lamps);
        Assert.Equal(new GridPosition(20, 20), lamp.Position);
        Assert.Equal(plates - 2, factory.Wallet.MaterialCount("iron-plate"));
        Assert.Equal(wire - 1, factory.Wallet.MaterialCount("copper-wire"));
        Assert.True(factory.TryGetSpecAt(lamp.Position, out var spec, out var entityId));
        Assert.Equal(new BuildSpec(Lamp.BuildingId, BuildingKind.Lamp, lamp.Position, Direction.East), spec);
        Assert.Equal(lamp.Id, entityId);
        Assert.False(factory.CanOccupy(lamp.Position));

        // No generator: no power, no light.
        factory.RunSeconds(1f);
        Assert.False(lamp.IsLit);

        // A burning generator next to it: lit, and its power use is in the demand of its network.
        var gen = factory.Generators.Count;
        Assert.True(factory.TryPlaceGenerator(new GridPosition(21, 20)));
        factory.Generators[gen].RestoreFuel(Generator.FuelBufferCapacity, Generator.SecondsPerFuel);
        factory.RunTicks(30);
        Assert.True(lamp.IsLit);
        Assert.Equal(1f, lamp.Light);
        var network = factory.PowerGrid.NetworkOf(lamp.Id)!;
        Assert.Equal(1_000, network.RequestedRate);
        Assert.Equal(10_000, network.SatisfactionBp);

        // Brownout: nothing burning and no battery leaves it dark again.
        factory.Generators[gen].RestoreFuel(0, 0f);
        factory.Step();
        Assert.False(lamp.IsLit);
    }

    [Fact]
    public void GeneratorAdjacencyFastPathMatchesTheFootprintRule()
    {
        // The bounding-box test in Generator.IsAdjacentTo must never change the answer of FootprintsAdjacent.
        var gen = new Generator(new GridPosition(10, 10));
        foreach (var size in new[] { 1, 2, 3 })
        {
            for (var y = 4; y <= 16; y++)
            {
                for (var x = 4; x <= 16; x++)
                {
                    var origin = new GridPosition(x, y);
                    Assert.Equal(
                        Generator.FootprintsAdjacent(gen.Position, Generator.Size, origin, size),
                        gen.IsAdjacentTo(origin, size));
                }
            }
        }
    }

    [Fact]
    public void LampIsLitThroughAPowerNode()
    {
        var factory = Sandbox();
        Assert.True(factory.TryPlaceGenerator(new GridPosition(10, 10)));
        Assert.True(factory.TryPlacePowerNode(new GridPosition(14, 10)));
        Assert.True(factory.PlaceInstant(new BuildSpec(Lamp.BuildingId, BuildingKind.Lamp, new GridPosition(15, 10), Direction.East), charge: false));
        factory.Generators[0].RestoreFuel(Generator.FuelBufferCapacity, Generator.SecondsPerFuel);
        factory.RunTicks(2);
        Assert.True(factory.Lamps[0].IsLit);
    }

    [Fact]
    public void LampSurvivesSaveAndLoadWithTheSameChecksum()
    {
        var factory = Sandbox();
        var host = new SimHost(factory);
        Assert.True(factory.TryPlaceGenerator(new GridPosition(10, 10)));
        Assert.True(factory.PlaceInstant(new BuildSpec(Lamp.BuildingId, BuildingKind.Lamp, new GridPosition(12, 10), Direction.East), charge: false));
        factory.Generators[0].RestoreFuel(Generator.FuelBufferCapacity, Generator.SecondsPerFuel);
        for (var i = 0; i < 20; i++)
        {
            host.Advance(SimulationClock.TickSeconds);
        }

        var bytes = Snapshot.Write(factory.State);
        var restored = Snapshot.Read(Content, bytes);
        Assert.Equal(Snapshot.Checksum(bytes), Snapshot.Checksum(Snapshot.Write(restored)));
        var lamp = Assert.Single(restored.Lamps);
        Assert.Equal(factory.Lamps[0].Id, lamp.Id);
        Assert.Equal(factory.Lamps[0].Position, lamp.Position);
        Simulation.Step(restored);
        Assert.True(lamp.IsLit);
    }
    /// <summary>A lamp with a charged battery and no generator: what it asks and what the battery loses.</summary>
    private static (Factory Factory, Lamp Lamp, Battery Battery) LampOnABattery(DayNightMode mode)
    {
        var factory = Sandbox(new GameRules(InstantBuild: true, DayNight: mode));
        factory.Research.ForceUnlock(Battery.BuildingId);
        Assert.True(factory.PlaceInstant(new BuildSpec(Battery.BuildingId, BuildingKind.Battery, new GridPosition(10, 10), Direction.East), charge: false));
        Assert.True(factory.PlaceInstant(new BuildSpec(Lamp.BuildingId, BuildingKind.Lamp, new GridPosition(11, 10), Direction.East), charge: false));
        var battery = factory.Batteries.Single();
        battery.Charge = 100_000;
        return (factory, factory.Lamps.Single(), battery);
    }

    [Fact]
    public void LampByDayAsksForNothingAndLeavesTheBatteryAlone()
    {
        var (factory, lamp, battery) = LampOnABattery(DayNightMode.AlwaysDay);
        factory.RunTicks(10);

        var network = factory.PowerGrid.NetworkOf(lamp.Id)!;
        Assert.Equal(0, network.RequestedRate);
        Assert.False(lamp.PowerRequested);
        Assert.False(lamp.IsLit);
        Assert.Equal(100_000, battery.Charge);
    }

    [Fact]
    public void LampAtNightAsksForItsPowerAndDrainsTheBattery()
    {
        var (factory, lamp, battery) = LampOnABattery(DayNightMode.AlwaysNight);
        factory.RunTicks(10);

        var network = factory.PowerGrid.NetworkOf(lamp.Id)!;
        Assert.Equal(1_000, network.RequestedRate);
        Assert.True(lamp.PowerRequested);
        Assert.True(lamp.IsLit);
        Assert.Equal(100_000 - 10 * 1_000, battery.Charge);
    }

    [Fact]
    public void LampFollowsTheLightOfTheCycle()
    {
        // The cycle starts by day: nothing asked.
        var (factory, lamp, battery) = LampOnABattery(DayNightMode.Cycle);
        factory.Step();
        Assert.False(lamp.PowerRequested);

        // Dusk: the lamp asks once the light of the tick is below half (500 of 1000).
        var dusk = DayNightClock.DuskStart;
        long TickWithLight(Func<int, bool> want)
        {
            for (var t = dusk; t < DayNightClock.NightStart; t++)
            {
                if (want(DayNightClock.LightAt(DayNightMode.Cycle, 0, t)))
                {
                    return t;
                }
            }

            throw new InvalidOperationException("no such tick");
        }

        var lastHalf = TickWithLight(l => l < 500) - 1;
        Assert.Equal(500, DayNightClock.LightAt(DayNightMode.Cycle, 0, lastHalf));
        factory.State.FirstBuildTick = 0;
        factory.State.CurrentTick = lastHalf;
        factory.Step();
        Assert.False(lamp.PowerRequested);
        factory.Step();
        Assert.True(lamp.PowerRequested);
        Assert.True(battery.Charge < 100_000);
    }
}
