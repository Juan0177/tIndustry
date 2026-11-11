using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The machines of the «Ricerca allargata» (advanced smelter, graphite press, separator, plasticizer) and the circuit
/// recipe of the assembler, gated by the research node <c>circuit</c>.
/// </summary>
public sealed class AdvancedMachinesTests
{
    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private const int X = 10;
    private const int Y = 20;

    private static Factory Sandbox()
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7,
            rules: new GameRules(InstantBuild: true));
        foreach (var id in new[]
                 {
                     Generator.BuildingId, Crafter.AssemblerBuildingId, Crafter.SmelterAdvancedId,
                     Crafter.GraphitePressId, Crafter.SeparatorId, Crafter.PlasticizerId
                 })
        {
            factory.Research.ForceUnlock(id);
        }

        return factory;
    }

    private static long Place(Factory factory, BuildingKind kind, int x, int y)
    {
        var origin = new GridPosition(x, y);
        var spec = new BuildSpec(BuildSpec.StructureIdFor(kind)!, kind, origin, Direction.East);
        Assert.True(factory.PlaceInstant(spec, charge: false), $"{kind} su {origin}");
        Assert.True(factory.TryGetSpecAt(origin, out var placed, out var id));
        Assert.Equal(kind, placed.Kind);
        return id;
    }

    private static long BurningGenerator(Factory factory, int x, int y)
    {
        var id = Place(factory, BuildingKind.Generator, x, y);
        factory.Generators.Single(g => g.Id == id).RestoreFuel(Generator.FuelBufferCapacity, Generator.SecondsPerFuel);
        return id;
    }

    private static Crafter Machine(Factory factory, long id) => factory.Machines.Single(m => m.Id == id);

    private static Crafter Feed(Crafter crafter, params string[] items)
    {
        foreach (var item in items)
        {
            Assert.True(crafter.TryAccept(item), item);
        }

        return crafter;
    }

    /// <summary>Steps the factory until <paramref name="crafter"/> has <paramref name="pieces"/> finished: -1 after <paramref name="limit"/> ticks.</summary>
    private static int TicksToOutput(Factory factory, Crafter crafter, int pieces = 1, int limit = 2_000)
    {
        for (var tick = 1; tick <= limit; tick++)
        {
            factory.Step();
            if (crafter.OutputQueue.Count >= pieces)
            {
                return tick;
            }
        }

        return -1;
    }

    [Fact]
    public void NewMachinesHaveTheirProfile()
    {
        var factory = Sandbox();
        var advanced = Machine(factory, Place(factory, BuildingKind.SmelterAdvanced, 10, 10));
        var press = Machine(factory, Place(factory, BuildingKind.GraphitePress, 13, 10));
        var separator = Machine(factory, Place(factory, BuildingKind.Separator, 16, 10));
        var plasticizer = Machine(factory, Place(factory, BuildingKind.Plasticizer, 19, 10));

        Assert.Equal(
            [Crafter.SmelterAdvancedId, Crafter.GraphitePressId, Crafter.SeparatorId, Crafter.PlasticizerId],
            factory.Machines.Select(m => m.DefinitionId));
        Assert.Empty(factory.Smelters);
        Assert.Empty(factory.Assemblers);
        Assert.Equal(factory.Machines, ((IGameView)factory).Machines);

        Assert.Equal((CraftPowerMode.Required, 200), (advanced.PowerMode, advanced.CraftSpeedPercent));
        Assert.Equal(Content.SmelterRecipes().Select(r => r.Id), advanced.AvailableRecipes.Select(r => r.Id));
        Assert.Equal(CraftPowerMode.None, press.PowerMode);
        Assert.Equal(["press-graphite"], press.AvailableRecipes.Select(r => r.Id));
        Assert.Equal(CraftPowerMode.Required, separator.PowerMode);
        Assert.Equal(["separate-sand"], separator.AvailableRecipes.Select(r => r.Id));
        Assert.Equal(CraftPowerMode.Required, plasticizer.PowerMode);
        Assert.Equal(["plasticize-plastic"], plasticizer.AvailableRecipes.Select(r => r.Id));

        foreach (var (kind, id) in new[]
                 {
                     (BuildingKind.SmelterAdvanced, "smelter-advanced"), (BuildingKind.GraphitePress, "graphite-press"),
                     (BuildingKind.Separator, "separator"), (BuildingKind.Plasticizer, "plasticizer")
                 })
        {
            Assert.Equal(id, BuildSpec.StructureIdFor(kind));
            Assert.Equal(kind, BuildSpec.KindFor(id));
            Assert.Equal(4, factory.Footprint(new BuildSpec(id, kind, new GridPosition(40, 40), Direction.East)).Count);
        }
    }

    [Fact]
    public void AdvancedSmelterTwiceAsFastWithFullPower()
    {
        var factory = Sandbox();
        BurningGenerator(factory, X, Y);
        var smelter = Feed(Machine(factory, Place(factory, BuildingKind.SmelterAdvanced, X + 2, Y)), "iron-ore", "iron-ore");

        var ticks = TicksToOutput(factory, smelter);

        Assert.Equal(10_000, factory.State.SatisfactionOf(smelter.Id));
        Assert.InRange(ticks, SimulationClock.TickRate - 1, SimulationClock.TickRate + 1);
        Assert.Equal(["iron-plate"], smelter.OutputQueue);
        Assert.Equal(1, smelter.ItemsCrafted);
    }

    [Fact]
    public void AdvancedSmelterHalfPowerHalfSpeed()
    {
        // 30 per second for three advanced smelters of 20: half of what they ask, so the speed 2 becomes 1.
        var factory = Sandbox();
        BurningGenerator(factory, X, Y);
        var smelters = new[] { 2, 4, 6 }
            .Select(dx => Feed(Machine(factory, Place(factory, BuildingKind.SmelterAdvanced, X + dx, Y)), "iron-ore", "iron-ore"))
            .ToArray();

        var ticks = TicksToOutput(factory, smelters[0]);

        Assert.InRange(ticks, 2 * SimulationClock.TickRate - 1, 2 * SimulationClock.TickRate + 1);
        Assert.All(smelters, s => Assert.Equal(["iron-plate"], s.OutputQueue));
    }

    [Fact]
    public void AdvancedSmelterStopsWithoutPower()
    {
        var factory = Sandbox();
        var smelter = Feed(Machine(factory, Place(factory, BuildingKind.SmelterAdvanced, X, Y)), "iron-ore", "iron-ore");
        // A belt brings coal into it: it never takes it (no recipe of its uses coal and it burns none).
        var beltAt = new GridPosition(X - 1, Y);
        Assert.True(factory.PlaceInstant(new BuildSpec(BuildSpec.DefaultConveyorId, null, beltAt, Direction.East), charge: false));
        Assert.True(factory.Belts.TryInsert(beltAt, new TransportedItem(9_999, "coal")));
        Assert.False(smelter.CanAcceptFuel("coal"));
        Assert.False(smelter.CanAccept("coal"));

        Assert.Equal(-1, TicksToOutput(factory, smelter, limit: 600));

        Assert.True(smelter.IsCrafting);
        Assert.Equal(0f, smelter.Progress);
        Assert.False(smelter.IsPowered);
        Assert.Equal(0, smelter.FuelBuffer);
        Assert.Empty(smelter.InputBuffer);
        Assert.Equal(["coal"], factory.Belts.Cells[beltAt].Items.Select(i => i.ItemId));
    }

    [Fact]
    public void PressMakesTwoGraphiteFromThreeCoalWithoutPower()
    {
        var factory = Sandbox();
        var press = Feed(Machine(factory, Place(factory, BuildingKind.GraphitePress, X, Y)), "coal", "coal", "coal");

        var ticks = TicksToOutput(factory, press, pieces: 2);

        Assert.InRange(ticks, 90 - 1, 90 + 1);
        Assert.Equal(["graphite", "graphite"], press.OutputQueue);
        Assert.False(press.IsPowered);
        Assert.Null(factory.PowerGrid.NetworkOf(press.Id));
        Assert.Equal(0, press.FuelBuffer);
    }

    [Fact]
    public void PlasticizerMakesPlasticWithPower()
    {
        var factory = Sandbox();
        BurningGenerator(factory, X, Y);
        var plasticizer = Feed(Machine(factory, Place(factory, BuildingKind.Plasticizer, X + 2, Y)), "coal", "coal");

        var ticks = TicksToOutput(factory, plasticizer);

        Assert.InRange(ticks, 2 * SimulationClock.TickRate - 1, 2 * SimulationClock.TickRate + 1);
        Assert.Equal(["plastic"], plasticizer.OutputQueue);
        Assert.Equal(0, plasticizer.FuelBuffer); // its coal is the recipe's, never fuel
    }

    [Fact]
    public void PlasticizerStopsWithoutPower()
    {
        var factory = Sandbox();
        var plasticizer = Feed(Machine(factory, Place(factory, BuildingKind.Plasticizer, X, Y)), "coal", "coal");

        Assert.Equal(-1, TicksToOutput(factory, plasticizer, limit: 600));
        Assert.True(plasticizer.IsCrafting);
        Assert.Equal(0f, plasticizer.Progress);
    }

    [Fact]
    public void NewMachinesArePowerConsumers()
    {
        var factory = Sandbox();
        var gen = BurningGenerator(factory, X, Y);
        var advanced = Feed(Machine(factory, Place(factory, BuildingKind.SmelterAdvanced, X + 2, Y)), "iron-ore", "iron-ore");
        var separator = Feed(Machine(factory, Place(factory, BuildingKind.Separator, X + 4, Y)), "sand", "sand");
        var plasticizer = Feed(Machine(factory, Place(factory, BuildingKind.Plasticizer, X + 6, Y)), "coal", "coal");
        var press = Feed(Machine(factory, Place(factory, BuildingKind.GraphitePress, X + 8, Y)), "coal", "coal", "coal");

        factory.Step();

        var net = factory.PowerGrid.NetworkOf(gen);
        Assert.NotNull(net);
        foreach (var consumer in new[] { advanced, separator, plasticizer })
        {
            Assert.Same(net, factory.PowerGrid.NetworkOf(consumer.Id));
        }

        var uses = new[] { Crafter.SmelterAdvancedId, Crafter.SeparatorId, Crafter.PlasticizerId }
            .Sum(id => Content.FindBuilding(id)!.PowerUseRate);
        Assert.Equal(34_000, uses);
        Assert.Equal(uses, net.RequestedRate);
        Assert.Null(factory.PowerGrid.NetworkOf(press.Id));
        Assert.DoesNotContain(PowerGrid.Blocks(factory.State), b => b.Id == press.Id);
        Assert.True(press.IsWorking);
    }

    [Theory]
    [InlineData(BuildingKind.SmelterAdvanced)]
    [InlineData(BuildingKind.GraphitePress)]
    [InlineData(BuildingKind.Separator)]
    [InlineData(BuildingKind.Plasticizer)]
    public void RemoveRefundsBuildCost(BuildingKind kind)
    {
        var factory = Sandbox();
        foreach (var item in new[] { "iron-plate", "lead-plate", "titanium-plate", "copper-wire", "silicon" })
        {
            factory.Wallet.AddMaterial(item, 100);
        }

        var cost = Content.FindBuilding(BuildSpec.StructureIdFor(kind)!)!.BuildCost;
        Assert.NotEmpty(cost);
        var before = cost.ToDictionary(c => c.ItemId, c => factory.Wallet.MaterialCount(c.ItemId));
        var host = new SimHost(factory);
        RefusalCode? refused = null;
        host.CommandRejected += (_, r) => refused = r.Code;
        var origin = new GridPosition(X, Y);

        host.Submit(new PlaceBuilding(0, kind, origin, Direction.East));
        host.AdvanceTicks(1);
        Assert.Null(refused);
        Assert.Single(factory.Machines);
        foreach (var c in cost)
        {
            Assert.Equal(before[c.ItemId] - c.Amount, factory.Wallet.MaterialCount(c.ItemId));
        }

        host.Submit(new Remove(0, origin));
        host.AdvanceTicks(1);
        Assert.Null(refused);
        Assert.Empty(factory.Machines);
        foreach (var c in cost)
        {
            Assert.Equal(before[c.ItemId], factory.Wallet.MaterialCount(c.ItemId));
        }
    }

    [Fact]
    public void CircuitNeedsResearch()
    {
        var factory = Sandbox();
        var circuit = Content.FindRecipe("craft-circuit")!;
        Assert.Equal("circuit", circuit.Research);
        Assert.Equal(3f, circuit.DurationSeconds);
        Assert.Contains(circuit, Content.RecipesFor(Crafter.AssemblerBuildingId));
        Assert.DoesNotContain(circuit, factory.RecipesAvailableTo(Crafter.AssemblerBuildingId));

        var assemblerId = Place(factory, BuildingKind.Assembler, X, Y);
        var assembler = factory.Assemblers.Single(a => a.Id == assemblerId);
        Assert.DoesNotContain(assembler.AvailableRecipes, r => r.Id == circuit.Id);
        // Wire and silicon are inputs of the circuit only: refused while it is gated.
        Assert.False(assembler.TryAccept("copper-wire"));
        Assert.False(assembler.TryAccept("silicon"));
        // Even with all three in its buffer, nothing is made.
        assembler.RestoreCraftState(0f, false,
            new Dictionary<string, int> { ["copper-wire"] = 1, ["silicon"] = 1, ["lead-plate"] = 1 }, null, 0);
        Assert.Equal(-1, TicksToOutput(factory, assembler, limit: 300));
        assembler.RestoreCraftState(0f, false, null, null, 0);

        // The research node, through the command: money and materials of its cost.
        var node = Content.RequireStructure("circuit");
        Assert.Equal("recipe", node.Kind);
        Assert.Equal("Ricetta dei circuiti", node.DisplayName);
        Assert.Equal([Crafter.AssemblerBuildingId], node.Prerequisites);
        factory.Wallet.AddMoney(node.Unlock!.Money);
        foreach (var m in node.Unlock.Materials)
        {
            factory.Wallet.AddMaterial(m.ItemId, m.Amount);
        }

        var host = new SimHost(factory);
        RefusalCode? refused = null;
        host.CommandRejected += (_, r) => refused = r.Code;
        host.Submit(new UnlockStructure(0, "circuit"));
        host.AdvanceTicks(1);
        Assert.Null(refused);
        Assert.True(factory.IsStructureUnlocked("circuit"));

        // Same assembler, not placed again.
        Assert.Contains(assembler.AvailableRecipes, r => r.Id == circuit.Id);
        Assert.Contains(circuit, factory.RecipesAvailableTo(Crafter.AssemblerBuildingId));
        Feed(assembler, "copper-wire", "silicon", "lead-plate");
        var ticks = TicksToOutput(factory, assembler);
        Assert.InRange(ticks, 3 * SimulationClock.TickRate - 1, 3 * SimulationClock.TickRate + 1);
        Assert.Equal(["circuit"], assembler.OutputQueue);
    }

    [Fact]
    public void AssemblerPlacedAfterTheResearchKnowsTheCircuit()
    {
        var factory = Sandbox();
        factory.Research.ForceUnlock("circuit");
        var assemblerId = Place(factory, BuildingKind.Assembler, X, Y);
        var assembler = factory.Assemblers.Single(a => a.Id == assemblerId);
        Assert.Contains(assembler.AvailableRecipes, r => r.Id == "craft-circuit");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestoredAssemblerFollowsTheResearch(bool researched)
    {
        var factory = Sandbox();
        if (researched)
        {
            factory.Research.ForceUnlock("circuit");
        }

        Place(factory, BuildingKind.Assembler, X, Y);
        var restored = Factory.Restore(Content, factory.Capture());

        Assert.Equal(researched, restored.Assemblers.Single().AvailableRecipes.Any(r => r.Id == "craft-circuit"));
    }
}
