using System.Text.Json;
using System.Text.Json.Nodes;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// G3 3/4: the Lamp (1×1 power consumer, lit only while it gets power) and the night flag of the game rules
/// (looks only: it is saved with the game and read from campaign.json, the simulation never reads it).
/// </summary>
public sealed class LampAndNightTests
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly FactoryContent Content = FactoryContent.Load(Path.Combine(DataDir, "content.json"));

    private static Factory Sandbox(GameRules? rules = null)
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7,
            rules: rules ?? new GameRules(InstantBuild: true));
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

        // A burning generator next to it: lit, and it drains the shared buffer by its power use.
        Assert.True(factory.TryPlaceGenerator(new GridPosition(21, 20)));
        factory.Generators[0].RestoreFuel(Generator.FuelBufferCapacity, Generator.SecondsPerFuel);
        factory.SetPowerBuffer(10f);
        var withLamp = Factory.Restore(Content, factory.Capture());
        factory.RemoveInstant(lamp.Position, refund: false);
        var withoutLamp = Factory.Restore(Content, factory.Capture());
        withLamp.RunTicks(30);
        withoutLamp.RunTicks(30);
        Assert.True(withLamp.Lamps[0].IsLit);
        var drawn = withoutLamp.PowerBuffer - withLamp.PowerBuffer;
        Assert.InRange(drawn, 30 * SimulationClock.TickSeconds * 0.99f, 30 * SimulationClock.TickSeconds * 1.01f);

        // Brownout: an empty buffer and nothing burning leaves it dark again.
        withLamp.Generators[0].RestoreFuel(0, 0f);
        withLamp.SetPowerBuffer(0f);
        withLamp.Step();
        Assert.False(withLamp.Lamps[0].IsLit);
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
        factory.SetPowerBuffer(10f);
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

    [Fact]
    public void NightFlagRoundTripsInSave()
    {
        var night = Sandbox(new GameRules(InstantBuild: true, DayNight: DayNightMode.AlwaysNight));
        Assert.Equal(DayNightMode.AlwaysNight, night.State.Rules.DayNight);
        var restored = Factory.Restore(Content, night.Capture());
        Assert.Equal(DayNightMode.AlwaysNight, restored.State.Rules.DayNight);
        Assert.Equal(FactorySaveData.CurrentVersion, night.Capture().Version);

        // Additive field: a save written before it has no "night" and loads as day.
        var json = JsonSerializer.Serialize(Sandbox().Capture(), FactorySaveStore.JsonOptions);
        Assert.DoesNotContain("\"night\"", json, StringComparison.OrdinalIgnoreCase);
        // No lamps: no "lamps" key either, so day saves without lamps keep their v12 bytes.
        Assert.DoesNotContain("\"lamps\"", json, StringComparison.OrdinalIgnoreCase);
        var old = JsonSerializer.Deserialize<FactorySaveData>(json, FactorySaveStore.JsonOptions)!;
        Assert.Equal(DayNightMode.Cycle, Factory.Restore(Content, old).State.Rules.DayNight);
    }

    [Fact]
    public void CampaignNightFlagLoads()
    {
        var catalog = CampaignCatalog.Load(Path.Combine(DataDir, "campaign.json"));
        Assert.Equal(
            ["level-06-corrente", "level-10-espansione"],
            catalog.Levels.Where(l => l.Night).Select(l => l.Id).ToArray());

        var level = catalog.Find("level-06-corrente")!;
        var factory = NewGame.Campaign(Content, catalog, level, mapWidth: 80, mapHeight: 80);
        Assert.Equal(DayNightMode.AlwaysNight, factory.State.Rules.DayNight);
        var day = NewGame.Campaign(Content, catalog, catalog.FirstLevel!, mapWidth: 80, mapHeight: 80);
        Assert.Equal(DayNightMode.Cycle, day.State.Rules.DayNight);
    }
}
