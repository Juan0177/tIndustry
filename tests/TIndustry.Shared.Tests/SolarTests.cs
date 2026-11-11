using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Solar panels (<c>solar</c> 1×1 3/s, <c>solar-large</c> 3×3 36/s): generators of the power network that need no fuel
/// and produce their nominal output × the world's light (<see cref="DayNightClock.LightAt"/>) / 1000, in integer
/// thousandths: full by day, nothing by night, linear through dusk and dawn.
/// </summary>
public sealed class SolarTests
{
    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly GridPosition At = new(20, 20);

    private static Factory Sandbox(DayNightMode mode = DayNightMode.Cycle)
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7,
            startingMoney: 1_000_000, rules: new GameRules(InstantBuild: true, DayNight: mode));
        factory.UnlockAllForDemo();
        factory.Research.ForceUnlock(SolarPanel.SmallId);
        factory.Research.ForceUnlock(SolarPanel.LargeId);
        factory.Research.ForceUnlock(Battery.BuildingId);
        foreach (var item in factory.State.Exchange.Items)
        {
            item.AutoSell = false;
        }

        return factory;
    }

    private static BuildSpec SolarSpec(GridPosition origin, bool large = false) =>
        large
            ? new BuildSpec(SolarPanel.LargeId, BuildingKind.SolarLarge, origin, Direction.East)
            : new BuildSpec(SolarPanel.SmallId, BuildingKind.Solar, origin, Direction.East);

    private static SolarPanel PlaceSolar(Factory factory, GridPosition origin, bool large = false)
    {
        Assert.True(factory.PlaceInstant(SolarSpec(origin, large), charge: false));
        return factory.SolarPanels.Single(p => p.Position == origin);
    }

    /// <summary>Runs the tick at cycle time <paramref name="cycleTime"/> (the world's time started at tick 0).</summary>
    private static void StepAt(Factory factory, long cycleTime)
    {
        factory.State.FirstBuildTick = 0;
        factory.State.CurrentTick = cycleTime;
        factory.Step();
    }

    [Fact]
    public void SolarPanelsAreInTheContent()
    {
        var small = Content.FindBuilding(SolarPanel.SmallId)!;
        Assert.True(small.Solar);
        Assert.Equal(3f, small.PowerOutput);
        Assert.Equal([new ResourceAmount("copper-wire", 6), new ResourceAmount("silicon", 4)], small.BuildCost);
        var large = Content.FindBuilding(SolarPanel.LargeId)!;
        Assert.True(large.Solar);
        Assert.Equal(36f, large.PowerOutput);
        Assert.Equal(
            [new ResourceAmount("lead-plate", 30), new ResourceAmount("silicon", 25), new ResourceAmount("titanium-plate", 10)],
            large.BuildCost);
        Assert.False(Content.FindBuilding(Generator.BuildingId)!.Solar);

        Assert.Equal("Pannello solare", Content.FindStructure(SolarPanel.SmallId)!.DisplayName);
        Assert.Equal([PowerNode.Tier1Id], Content.FindStructure(SolarPanel.SmallId)!.Prerequisites);
        Assert.Equal("Pannello solare grande", Content.FindStructure(SolarPanel.LargeId)!.DisplayName);
        Assert.Equal([SolarPanel.SmallId], Content.FindStructure(SolarPanel.LargeId)!.Prerequisites);
        Assert.Equal(SolarPanel.SmallId, BuildSpec.StructureIdFor(BuildingKind.Solar));
        Assert.Equal(SolarPanel.LargeId, BuildSpec.StructureIdFor(BuildingKind.SolarLarge));
        Assert.Equal(BuildingKind.Solar, BuildSpec.KindFor(SolarPanel.SmallId));
        Assert.Equal(BuildingKind.SolarLarge, BuildSpec.KindFor(SolarPanel.LargeId));
    }

    [Fact]
    public void SolarFullAtNoon()
    {
        var factory = Sandbox();
        var panel = PlaceSolar(factory, At);
        StepAt(factory, DayNightClock.DuskStart / 2);

        Assert.Equal(3_000, panel.CurrentRate);
        var network = factory.PowerGrid.NetworkOf(panel.Id)!;
        Assert.Equal(3_000, network.ProducedRate);
    }

    [Fact]
    public void SolarZeroAtNight()
    {
        var factory = Sandbox();
        var panel = PlaceSolar(factory, At);
        StepAt(factory, (DayNightClock.NightStart + DayNightClock.DawnStart) / 2);

        Assert.Equal(0, panel.CurrentRate);
        Assert.Equal(0, factory.PowerGrid.NetworkOf(panel.Id)!.ProducedRate);
    }

    [Fact]
    public void SolarHalfAtMidDusk()
    {
        var factory = Sandbox();
        var panel = PlaceSolar(factory, At);
        StepAt(factory, DayNightClock.DuskStart + DayNightClock.FadeTicks / 2);

        Assert.InRange(panel.CurrentRate, 1_497, 1_503);
    }

    [Fact]
    public void SolarMonotoneAtDusk()
    {
        var factory = Sandbox();
        var panel = PlaceSolar(factory, At);
        StepAt(factory, DayNightClock.DuskStart - 1);
        Assert.Equal(3_000, panel.CurrentRate);

        var last = panel.CurrentRate;
        for (var t = DayNightClock.DuskStart; t <= DayNightClock.NightStart; t++)
        {
            factory.Step(); // runs the tick at cycle time t
            Assert.True(panel.CurrentRate <= last, $"tick {t}: {panel.CurrentRate} > {last}");
            last = panel.CurrentRate;
        }

        Assert.Equal(0, last);

        // And back up through the dawn.
        StepAt(factory, DayNightClock.DawnStart - 1);
        Assert.Equal(0, panel.CurrentRate);
        for (var t = DayNightClock.DawnStart; t < DayNightClock.CycleTicks; t++)
        {
            factory.Step();
            Assert.True(panel.CurrentRate >= last, $"tick {t}: {panel.CurrentRate} < {last}");
            last = panel.CurrentRate;
        }

        factory.Step(); // the next cycle's day
        Assert.Equal(3_000, panel.CurrentRate);
    }

    [Fact]
    public void SolarBeforeTheFirstBlockIsDay()
    {
        var factory = Sandbox();
        var panel = PlaceSolar(factory, At);
        factory.State.FirstBuildTick = null;
        factory.State.CurrentTick = DayNightClock.NightStart + 100;
        factory.Step();
        Assert.Equal(3_000, panel.CurrentRate);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(DayNightClock.DuskStart + 600)]
    [InlineData(DayNightClock.NightStart + 1_000)]
    [InlineData(DayNightClock.DawnStart + 300)]
    public void AlwaysDayAlwaysFull(long cycleTime)
    {
        var factory = Sandbox(DayNightMode.AlwaysDay);
        var panel = PlaceSolar(factory, At);
        StepAt(factory, cycleTime);
        Assert.Equal(3_000, panel.CurrentRate);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(DayNightClock.DuskStart / 2)]
    [InlineData(DayNightClock.DuskStart + 600)]
    [InlineData(DayNightClock.DawnStart + 300)]
    public void AlwaysNightAlwaysZero(long cycleTime)
    {
        var factory = Sandbox(DayNightMode.AlwaysNight);
        var panel = PlaceSolar(factory, At);
        StepAt(factory, cycleTime);
        Assert.Equal(0, panel.CurrentRate);
        Assert.Equal(0, factory.PowerGrid.NetworkOf(panel.Id)!.ProducedRate);
    }

    [Fact]
    public void LargeSolarIs36AndThreeByThree()
    {
        var factory = Sandbox();
        var panel = PlaceSolar(factory, At, large: true);
        Assert.Equal(3, panel.Size);
        Assert.Equal(9, factory.Footprint(SolarSpec(At, large: true)).Count);
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                var tile = new GridPosition(At.X + x, At.Y + y);
                Assert.False(factory.CanOccupy(tile));
                Assert.True(factory.TryGetSpecAt(tile, out var spec, out var entityId));
                Assert.Equal(SolarSpec(At, large: true), spec);
                Assert.Equal(panel.Id, entityId);
            }
        }

        Assert.True(factory.CanOccupy(new GridPosition(At.X + 3, At.Y)));

        StepAt(factory, 1_000);
        Assert.Equal(36_000, panel.CurrentRate);
        StepAt(factory, DayNightClock.DuskStart + DayNightClock.FadeTicks / 2);
        Assert.Equal(18_000, panel.CurrentRate);

        // A small panel touching its right edge shares the network: 36 + 3.
        var small = PlaceSolar(factory, new GridPosition(At.X + 3, At.Y + 2));
        Assert.Equal(1, small.Size);
        StepAt(factory, 1_000);
        Assert.Same(factory.PowerGrid.NetworkOf(panel.Id), factory.PowerGrid.NetworkOf(small.Id));
        Assert.Equal(39_000, factory.PowerGrid.NetworkOf(panel.Id)!.ProducedRate);

        // Taken away: its 9 cells are free again.
        Assert.True(factory.TryRemoveBuildingAt(new GridPosition(At.X + 1, At.Y + 1)));
        Assert.DoesNotContain(factory.SolarPanels, p => p.Id == panel.Id);
        Assert.True(factory.CanOccupy(At));
    }

    [Fact]
    public void SolarIsPlacedThroughACommandAndCharged()
    {
        var factory = Sandbox();
        var host = new SimHost(factory);
        factory.Wallet.AddMaterial("copper-wire", 20);
        factory.Wallet.AddMaterial("silicon", 20);
        var wire = factory.Wallet.MaterialCount("copper-wire");
        var silicon = factory.Wallet.MaterialCount("silicon");

        host.Submit(new PlaceBuilding(1, BuildingKind.Solar, At, Direction.East));
        host.Advance(SimulationClock.TickSeconds);
        var panel = Assert.Single(factory.SolarPanels);
        Assert.Equal(At, panel.Position);
        Assert.Equal(SolarPanel.SmallId, panel.DefinitionId);
        Assert.Equal(wire - 6, factory.Wallet.MaterialCount("copper-wire"));
        Assert.Equal(silicon - 4, factory.Wallet.MaterialCount("silicon"));
        Assert.Contains(factory.PowerBlocks, b => b.Id == panel.Id && b.Kind == PowerBlockKind.Generator);
    }

    [Fact]
    public void SolarChargesBattery()
    {
        var factory = Sandbox();
        var panel = PlaceSolar(factory, At);
        Assert.True(factory.PlaceInstant(
            new BuildSpec(Battery.BuildingId, BuildingKind.Battery, new GridPosition(At.X + 1, At.Y), Direction.East),
            charge: false));
        var battery = Assert.Single(factory.Batteries);
        Assert.Same(factory.PowerGrid.NetworkOf(panel.Id), factory.PowerGrid.NetworkOf(battery.Id));

        // One second of full day: 3 units = 180 000 sixty-thousandths.
        factory.State.FirstBuildTick = 0;
        factory.State.CurrentTick = 1_000;
        factory.RunTicks(SimulationClock.TickRate);
        Assert.Equal(3 * BuildingDefinition.ChargePerUnit, battery.Charge);

        // At night it stops.
        factory.State.CurrentTick = DayNightClock.NightStart + 10;
        factory.RunTicks(SimulationClock.TickRate);
        Assert.Equal(3 * BuildingDefinition.ChargePerUnit, battery.Charge);
    }

    [Fact]
    public void SolarSurvivesSave()
    {
        var factory = Sandbox();
        var small = PlaceSolar(factory, At);
        var large = PlaceSolar(factory, new GridPosition(At.X + 4, At.Y), large: true);
        var nodeCell = new GridPosition(At.X + 2, At.Y + 6);
        Assert.True(factory.PlaceInstant(
            new BuildSpec(PowerNode.Tier1Id, BuildingKind.PowerNode, nodeCell, Direction.East), charge: false));
        var node = factory.PowerNodes.Single(n => n.Position == nodeCell);
        // The node's automatic lasers join both panels into one network.
        Assert.True(factory.PowerGrid.HasLink(node.Id, small.Id));
        Assert.True(factory.PowerGrid.HasLink(node.Id, large.Id));
        StepAt(factory, 1_000);

        var data = factory.Capture();
        Assert.Equal(
            [(small.Id, At.X, At.Y, SolarPanel.SmallId), (large.Id, At.X + 4, At.Y, SolarPanel.LargeId)],
            data.SolarPanels!.Select(d => (d.Id, d.X, d.Y, d.DefinitionId)).ToList());

        var json = JsonSerializer.Serialize(data, FactorySaveStore.JsonOptions);
        var restored = Factory.Restore(Content, JsonSerializer.Deserialize<FactorySaveData>(json, FactorySaveStore.JsonOptions)!);
        Assert.Equal(json, JsonSerializer.Serialize(restored.Capture(), FactorySaveStore.JsonOptions));
        Assert.Equal(2, restored.SolarPanels.Count);
        Assert.Equal(3, restored.SolarPanels.Single(p => p.Id == large.Id).Size);
        Assert.True(restored.PowerGrid.HasLink(node.Id, small.Id));
        Assert.True(restored.NextEntityId > Math.Max(small.Id, large.Id));

        // Both copies go on the same way, through the snapshot too.
        var copy = new Factory(Snapshot.Read(Content, Snapshot.Write(factory.State)));
        for (var tick = 0; tick < 60; tick++)
        {
            factory.Step();
            copy.Step();
            Assert.Equal(Snapshot.Checksum(Snapshot.Write(factory.State)), Snapshot.Checksum(Snapshot.Write(copy.State)));
        }

        Assert.Equal(39_000, copy.PowerGrid.NetworkOf(large.Id)!.ProducedRate);

        // No panels: the list is left out of the save.
        Assert.Null(Sandbox().Capture().SolarPanels);
        Assert.DoesNotContain("SolarPanels", JsonSerializer.Serialize(Sandbox().Capture(), FactorySaveStore.JsonOptions));
    }

    [Fact]
    public void ASolarPanelOfAnotherKindMakesTheSaveUnreadable()
    {
        var factory = Sandbox();
        PlaceSolar(factory, At);
        var data = factory.Capture();
        data.SolarPanels![0].DefinitionId = Generator.BuildingId;
        Assert.Throws<InvalidDataException>(() => Factory.Restore(Content, data));
    }
}
