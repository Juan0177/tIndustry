using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The large battery (<c>battery-large</c>, 3×3, capacity 20 000): a battery of its own definition; in a network with
/// other batteries the charge is shared by each one's own capacity.
/// </summary>
public sealed class LargeBatteryTests
{
    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly GridPosition At = new(20, 20);

    private static Battery PlaceBattery(Factory factory, GridPosition origin, bool large)
    {
        var spec = large
            ? new BuildSpec(Battery.LargeId, BuildingKind.BatteryLarge, origin, Direction.East)
            : new BuildSpec(Battery.BuildingId, BuildingKind.Battery, origin, Direction.East);
        Assert.True(factory.PlaceInstant(spec, charge: false));
        return factory.Batteries.Single(b => b.Position == origin);
    }

    [Fact]
    public void LargeBatteryIsInTheContent()
    {
        var building = Content.FindBuilding(Battery.LargeId)!;
        Assert.Equal(20_000, building.PowerCapacity);
        Assert.Equal(
            [new ResourceAmount("lead-plate", 40), new ResourceAmount("titanium-plate", 20), new ResourceAmount("silicon", 10)],
            building.BuildCost);
        Assert.Equal(100, building.RefundPercent);
        var structure = Content.FindStructure(Battery.LargeId)!;
        Assert.Equal("Batteria grande", structure.DisplayName);
        Assert.Equal([Battery.BuildingId], structure.Prerequisites);
        Assert.Equal(Battery.LargeId, BuildSpec.StructureIdFor(BuildingKind.BatteryLarge));
        Assert.Equal(BuildingKind.BatteryLarge, BuildSpec.KindFor(Battery.LargeId));
    }

    [Fact]
    public void LargeBatteryHolds20000AndIsThreeByThree()
    {
        var factory = CombustionGeneratorTests.Sandbox();
        var battery = PlaceBattery(factory, At, large: true);
        Assert.Equal(Battery.LargeId, battery.DefinitionId);
        Assert.Equal(3, battery.Size);
        Assert.Equal(20_000 * BuildingDefinition.ChargePerUnit, Battery.CapacityOf(Content, Battery.LargeId));
        Assert.Equal(1_000 * BuildingDefinition.ChargePerUnit, Battery.CapacityOf(Content));

        // Nine cells, all of it, each one the same block.
        Assert.Equal(9, battery.OccupiedTiles().Count());
        foreach (var tile in battery.OccupiedTiles())
        {
            Assert.False(factory.CanOccupy(tile));
            Assert.True(factory.TryGetSpecAt(tile, out var spec, out var id));
            Assert.Equal(new BuildSpec(Battery.LargeId, BuildingKind.BatteryLarge, At, Direction.East), spec);
            Assert.Equal(battery.Id, id);
        }

        Assert.True(factory.CanOccupy(new GridPosition(At.X + 3, At.Y)));
        Assert.False(factory.PlaceInstant(
            new BuildSpec(Battery.LargeId, BuildingKind.BatteryLarge, new GridPosition(At.X + 2, At.Y + 2), Direction.East),
            charge: false));

        // On its network it is the whole capacity; filled by a burning generator it stops at 20 000.
        var gen = CombustionGeneratorTests.Place(factory, new GridPosition(At.X + 3, At.Y));
        factory.Step();
        var network = factory.PowerGrid.NetworkOf(battery.Id)!;
        Assert.Same(network, factory.PowerGrid.NetworkOf(gen.Id));
        Assert.Equal(20_000 * BuildingDefinition.ChargePerUnit, network.Capacity);
        battery.Charge = network.Capacity - 1_000;
        Assert.True(gen.TryAcceptFuel("coal"));
        factory.RunTicks(5);
        Assert.Equal(network.Capacity, battery.Charge);
    }

    [Fact]
    public void MixedBatteriesShareByCapacity()
    {
        var factory = CombustionGeneratorTests.Sandbox();
        var small = PlaceBattery(factory, At, large: false);
        var large = PlaceBattery(factory, new GridPosition(At.X + 1, At.Y), large: true);
        var gen = CombustionGeneratorTests.Place(factory, new GridPosition(At.X + 4, At.Y));
        Assert.True(gen.TryAcceptFuel("coal"));
        factory.RunTicks(30);

        var network = factory.PowerGrid.NetworkOf(small.Id)!;
        Assert.Same(network, factory.PowerGrid.NetworkOf(large.Id));
        Assert.Equal(21_000 * BuildingDefinition.ChargePerUnit, network.Capacity);
        Assert.Equal(
            [1_000 * BuildingDefinition.ChargePerUnit, 20_000 * BuildingDefinition.ChargePerUnit],
            network.BatteryCapacities);

        // 30 ticks at 90/s: 2 700 000 sixty-thousandths, split 1 000 : 20 000 (remainder one each by id).
        var stored = 30 * 90_000L;
        Assert.Equal(stored, small.Charge + large.Charge);
        Assert.Equal(stored * 1_000 / 21_000, small.Charge, tolerance: 1);
        Assert.Equal(stored * 20_000 / 21_000, large.Charge, tolerance: 1);
    }

    [Fact]
    public void RemovingTheLargeBatteryRefundsItsOwnCost()
    {
        var factory = CombustionGeneratorTests.Sandbox();
        PlaceBattery(factory, At, large: true);
        var lead = factory.Wallet.MaterialCount("lead-plate");
        var titanium = factory.Wallet.MaterialCount("titanium-plate");
        Assert.True(factory.TryRemoveBuildingAt(new GridPosition(At.X + 2, At.Y + 2)));
        Assert.Empty(factory.Batteries);
        Assert.Equal(lead + 40, factory.Wallet.MaterialCount("lead-plate"));
        Assert.Equal(titanium + 20, factory.Wallet.MaterialCount("titanium-plate"));
    }
}
