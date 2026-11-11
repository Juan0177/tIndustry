using System.Text.Json;
using System.Text.Json.Nodes;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The combustion generator (<c>combustion-generator</c>, 2×2, 90/s): it burns 1 coal every 2 s or 1 graphite every
/// 4 s (its <c>fuels</c> in content.json, coal first), holding 8 pieces in all; the generator of before burns its one
/// fuel (<c>fuels: [{coal, 8}]</c>) exactly as it always did.
/// </summary>
public sealed class CombustionGeneratorTests
{
    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly GridPosition At = new(20, 20);

    internal static Factory Sandbox()
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7,
            startingMoney: 1_000_000, rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        factory.Research.ForceUnlock(Generator.CombustionId);
        factory.Research.ForceUnlock(Battery.LargeId);
        foreach (var item in factory.State.Exchange.Items)
        {
            item.AutoSell = false;
        }

        return factory;
    }

    internal static Generator Place(Factory factory, GridPosition origin, bool combustion = true)
    {
        var spec = combustion
            ? new BuildSpec(Generator.CombustionId, BuildingKind.CombustionGenerator, origin, Direction.East)
            : new BuildSpec(Generator.BuildingId, BuildingKind.Generator, origin, Direction.East);
        Assert.True(factory.PlaceInstant(spec, charge: false));
        return factory.Generators.Single(g => g.Position == origin);
    }

    private static void Fill(Generator gen, string itemId)
    {
        while (gen.TryAcceptFuel(itemId))
        {
        }
    }

    /// <summary>Pieces burnt in <paramref name="ticks"/> ticks with the buffer kept full of <paramref name="itemId"/>.</summary>
    private static long BurntWithAFullBuffer(string itemId, int ticks)
    {
        var factory = Sandbox();
        var gen = Place(factory, At);
        for (var t = 0; t < ticks; t++)
        {
            Fill(gen, itemId);
            factory.Step();
        }

        return gen.FuelConsumed;
    }

    [Fact]
    public void CombustionGeneratorIsInTheContent()
    {
        var building = Content.FindBuilding(Generator.CombustionId)!;
        Assert.Equal(90f, building.PowerOutput);
        Assert.Equal([new FuelDefinition("coal", 2f), new FuelDefinition("graphite", 4f)], building.Fuels);
        Assert.Equal(
            [new ResourceAmount("iron-plate", 20), new ResourceAmount("lead-plate", 10), new ResourceAmount("copper-wire", 6)],
            building.BuildCost);
        Assert.Equal(100, building.RefundPercent);
        Assert.Equal([new FuelDefinition("coal", 8f)], Content.FindBuilding(Generator.BuildingId)!.Fuels);
        Assert.Empty(Content.FindBuilding(Battery.BuildingId)!.Fuels);

        var structure = Content.FindStructure(Generator.CombustionId)!;
        Assert.Equal("Generatore a combustione", structure.DisplayName);
        Assert.Equal([Generator.BuildingId], structure.Prerequisites);
        Assert.Equal(Generator.CombustionId, BuildSpec.StructureIdFor(BuildingKind.CombustionGenerator));
        Assert.Equal(BuildingKind.CombustionGenerator, BuildSpec.KindFor(Generator.CombustionId));
    }

    [Fact]
    public void OldGeneratorUnchanged()
    {
        // Measured on the code before the fuels per definition: two coals burn 960 ticks, the second taken at
        // tick 481, at 30/s throughout.
        var factory = Sandbox();
        var gen = Place(factory, At, combustion: false);
        Assert.Equal(Generator.BuildingId, gen.DefinitionId);
        Assert.Equal([new FuelDefinition("coal", 8f)], gen.Fuels);
        Assert.False(gen.TryAcceptFuel("graphite"));
        Assert.True(gen.TryAcceptFuel("coal"));
        Assert.True(gen.TryAcceptFuel("coal"));

        var producing = 0;
        var secondAt = -1;
        for (var t = 1; t <= 2000; t++)
        {
            factory.Step();
            var rate = factory.PowerGrid.NetworkOf(gen.Id)!.ProducedRate;
            if (rate > 0)
            {
                Assert.Equal(30_000, rate);
                producing++;
            }

            if (gen.FuelConsumed == 2 && secondAt < 0)
            {
                secondAt = t;
            }
        }

        Assert.Equal(960, producing);
        Assert.Equal(481, secondAt);
        Assert.Equal(2, gen.FuelConsumed);
        Assert.Equal(0f, gen.BurnRemaining);
    }

    [Fact]
    public void CombustionBurnsCoalEveryTwoSeconds()
    {
        Assert.Equal(30, BurntWithAFullBuffer("coal", 60 * SimulationClock.TickRate));
    }

    [Fact]
    public void GraphiteEveryFourSeconds()
    {
        Assert.Equal(15, BurntWithAFullBuffer("graphite", 60 * SimulationClock.TickRate));
    }

    [Fact]
    public void CoalBurnsBeforeGraphite()
    {
        var factory = Sandbox();
        var gen = Place(factory, At);
        // The graphite arrives first, but the coal (listed first) burns first.
        for (var i = 0; i < 3; i++)
        {
            Assert.True(gen.TryAcceptFuel("graphite"));
        }

        for (var i = 0; i < 2; i++)
        {
            Assert.True(gen.TryAcceptFuel("coal"));
        }

        factory.Step();
        Assert.Equal((1, 3), (gen.FuelCount("coal"), gen.FuelCount("graphite")));

        // Two coals of 2 s each, then the graphite.
        var ticks = StepUntilConsumed(factory, gen, 3);
        Assert.InRange(ticks, 2 * 2 * SimulationClock.TickRate, 2 * 2 * SimulationClock.TickRate + 2);
        Assert.Equal((0, 2), (gen.FuelCount("coal"), gen.FuelCount("graphite")));

        // A coal arriving while graphite burns waits for the burn to end (4 s), then goes before the graphite left.
        Assert.True(gen.TryAcceptFuel("coal"));
        ticks = StepUntilConsumed(factory, gen, 4);
        Assert.InRange(ticks, 4 * SimulationClock.TickRate - 1, 4 * SimulationClock.TickRate + 1);
        Assert.Equal((0, 2), (gen.FuelCount("coal"), gen.FuelCount("graphite")));
    }

    /// <summary>Steps until <paramref name="gen"/> has taken its <paramref name="consumed"/>-th piece; returns the ticks run.</summary>
    private static int StepUntilConsumed(Factory factory, Generator gen, long consumed)
    {
        var ticks = 0;
        while (gen.FuelConsumed < consumed)
        {
            factory.Step();
            ticks++;
            Assert.True(ticks < 10_000);
        }

        return ticks;
    }

    [Fact]
    public void BufferHoldsEightInAll()
    {
        var factory = Sandbox();
        var gen = Place(factory, At);
        for (var i = 0; i < 5; i++)
        {
            Assert.True(gen.TryAcceptFuel("graphite"));
        }

        for (var i = 0; i < 3; i++)
        {
            Assert.True(gen.TryAcceptFuel("coal"));
        }

        Assert.Equal(Generator.FuelBufferCapacity, gen.FuelBuffer);
        Assert.False(gen.CanAcceptFuel("coal"));
        Assert.False(gen.TryAcceptFuel("coal"));
        Assert.False(gen.TryAcceptFuel("graphite"));
        Assert.Equal((3, 5), (gen.FuelCount("coal"), gen.FuelCount("graphite")));

        // Nothing else is fuel, even with room.
        factory.Step();
        Assert.Equal(7, gen.FuelBuffer);
        Assert.False(gen.CanAcceptFuel("iron-plate"));
        Assert.True(gen.CanAcceptFuel("graphite"));

        // Logistics hand it both fuels by the same rule (a router's or a sorter's exit).
        Assert.True(factory.State.CanReceiveFromLogistics(At, "graphite"));
        Assert.True(factory.State.ReceiveFromLogistics(At, "graphite"));
        Assert.False(factory.State.CanReceiveFromLogistics(At, "coal"));
        Assert.Equal(6, gen.FuelCount("graphite"));
    }

    [Fact]
    public void BeltsFeedTheCombustionGeneratorGraphite()
    {
        var factory = Sandbox();
        var gen = Place(factory, At);
        // A belt pointing into its west side, carrying graphite.
        var belt = new GridPosition(At.X - 1, At.Y);
        Assert.True(factory.TryPlaceBelt(belt, Direction.East));
        Assert.True(factory.Belts.TryInsert(belt, new TransportedItem(1, "graphite"), Direction.East));
        factory.RunTicks(SimulationClock.TickRate);

        Assert.Equal(1, gen.FuelConsumed);
        Assert.True(gen.IsGenerating);
    }

    [Fact]
    public void CombustionGives90()
    {
        var factory = Sandbox();
        var gen = Place(factory, At);
        Assert.True(gen.TryAcceptFuel("graphite"));
        factory.Step();
        Assert.Equal(90_000, factory.PowerGrid.NetworkOf(gen.Id)!.ProducedRate);
        Assert.Equal(Generator.Size, gen.OccupiedTiles().Max(t => t.X) - At.X + 1);

        // The generator of before next to it (contact): each gives its own output.
        var old = Place(factory, new GridPosition(At.X + 2, At.Y), combustion: false);
        Assert.True(old.TryAcceptFuel("coal"));
        factory.Step();
        Assert.Same(factory.PowerGrid.NetworkOf(gen.Id), factory.PowerGrid.NetworkOf(old.Id));
        Assert.Equal(120_000, factory.PowerGrid.NetworkOf(gen.Id)!.ProducedRate);
    }

    [Fact]
    public void CombustionGeneratorIsABuildingOfItsKind()
    {
        var factory = Sandbox();
        var gen = Place(factory, At);
        foreach (var tile in gen.OccupiedTiles())
        {
            Assert.False(factory.CanOccupy(tile));
            Assert.True(factory.TryGetSpecAt(tile, out var spec, out var id));
            Assert.Equal(
                new BuildSpec(Generator.CombustionId, BuildingKind.CombustionGenerator, At, Direction.East), spec);
            Assert.Equal(gen.Id, id);
        }

        Assert.Equal(4, gen.OccupiedTiles().Count());
    }

    [Fact]
    public void GeneratorsAndBatteriesSurviveSave()
    {
        var factory = Sandbox();
        var old = Place(factory, At, combustion: false);
        var gen = Place(factory, new GridPosition(At.X + 2, At.Y));
        Assert.True(factory.PlaceInstant(
            new BuildSpec(Battery.BuildingId, BuildingKind.Battery, new GridPosition(At.X + 4, At.Y), Direction.East),
            charge: false));
        Assert.True(factory.PlaceInstant(
            new BuildSpec(Battery.LargeId, BuildingKind.BatteryLarge, new GridPosition(At.X + 5, At.Y), Direction.East),
            charge: false));
        Fill(old, "coal");
        for (var i = 0; i < 3; i++)
        {
            Assert.True(gen.TryAcceptFuel("graphite"));
        }

        Fill(gen, "coal");
        factory.RunTicks(150);
        Assert.True(gen.IsGenerating);
        Assert.Equal((3, 3), (gen.FuelCount("coal"), gen.FuelCount("graphite")));

        var data = factory.Capture();
        var json = JsonSerializer.Serialize(data, FactorySaveStore.JsonOptions);
        var restored = Factory.Restore(Content, JsonSerializer.Deserialize<FactorySaveData>(json, FactorySaveStore.JsonOptions)!);
        Assert.Equal(json, JsonSerializer.Serialize(restored.Capture(), FactorySaveStore.JsonOptions));

        // The generator of before keeps the fields of before: no definition, no fuels per item.
        var oldDto = data.Generators.Single(g => g.Id == old.Id);
        Assert.Null(oldDto.DefinitionId);
        Assert.Null(oldDto.Fuels);
        var genDto = data.Generators.Single(g => g.Id == gen.Id);
        Assert.Equal(Generator.CombustionId, genDto.DefinitionId);
        Assert.Equal(6, genDto.FuelBuffer);
        Assert.Equal(
            [("coal", 3), ("graphite", 3)],
            genDto.Fuels!.Select(f => (f.ItemId, f.Count)));
        Assert.Null(data.Batteries!.Single(b => b.X == At.X + 4).DefinitionId);
        Assert.Equal(Battery.LargeId, data.Batteries!.Single(b => b.X == At.X + 5).DefinitionId);

        var back = restored.Generators.Single(g => g.Id == gen.Id);
        Assert.Equal(Generator.CombustionId, back.DefinitionId);
        Assert.Equal((3, 3), (back.FuelCount("coal"), back.FuelCount("graphite")));
        Assert.Equal(gen.BurnRemaining, back.BurnRemaining);
        Assert.Equal(gen.FuelConsumed, back.FuelConsumed);
        Assert.Equal(Generator.BuildingId, restored.Generators.Single(g => g.Id == old.Id).DefinitionId);
        var large = restored.Batteries.Single(b => b.DefinitionId == Battery.LargeId);
        Assert.Equal(Battery.LargeSize, large.Size);
        Assert.Equal(factory.Batteries.Single(b => b.DefinitionId == Battery.LargeId).Charge, large.Charge);
        Assert.True(large.Charge > 0);

        // Both run on identically.
        for (var i = 0; i < 600; i++)
        {
            factory.Step();
            restored.Step();
        }

        Assert.Equal(Snapshot.Checksum(Snapshot.Write(factory.State)), Snapshot.Checksum(Snapshot.Write(restored.State)));
    }

    [Fact]
    public void AnUnknownGeneratorOrBatteryMakesTheSaveUnreadable()
    {
        var factory = Sandbox();
        Place(factory, At);
        var data = factory.Capture();
        data.Generators[0].DefinitionId = "smelter";
        Assert.Throws<InvalidDataException>(() => Factory.Restore(Content, data));
    }

    [Fact]
    public void ContentWithoutFuelsForAGeneratorIsRefused()
    {
        JsonObject Building(JsonObject root, string id) =>
            root["buildings"]!.AsArray().Single(b => (string?)b!["id"] == id)!.AsObject();

        // A generator without fuels, an unknown fuel, a fuel burning no time, fuels on a building that is not a generator.
        LoadMutated(root => Building(root, Generator.BuildingId).Remove("fuels"));
        LoadMutated(root => Building(root, Generator.CombustionId)["fuels"]![1]!["itemId"] = "unobtainium");
        LoadMutated(root => Building(root, Generator.CombustionId)["fuels"]![1]!["seconds"] = 0);
        LoadMutated(root => Building(root, Battery.BuildingId)["fuels"] = JsonNode.Parse("""[{"itemId":"coal","seconds":8}]"""));
    }

    private static void LoadMutated(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "content.json")))!.AsObject();
        mutate(root);
        var path = Path.Combine(Path.GetTempPath(), $"tindustry-fuels-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, root.ToJsonString());
        try
        {
            Assert.Throws<InvalidDataException>(() => FactoryContent.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
