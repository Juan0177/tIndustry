using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The drone is the player (spec «Il drone sei tu»): it moves only along the held input of
/// <see cref="SetDroneInput"/>, at its tier's speed, inside the map and over everything; it works on the first plan of
/// its queue within build range (27.5 tiles), while plans out of range wait in the queue.
/// </summary>
public sealed class DroneMovementTests
{
    private const int Player = 0;
    private const int Size = 200;
    private const string Plate = "iron-plate";
    private const string Ore = "iron-ore";

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    /// <summary>Shard 3×3 on (100..102, 100..102): the drone starts at (101.5, 101.5).</summary>
    private static readonly GridPosition CoreOrigin = new(100, 100);

    private static readonly TerrainTile Grass = new(FloorKind.Grass, WallKind.None, DepositKind.None);

    /// <summary>A <see cref="Size"/>² grass sandbox with real (not instant) construction and plenty of materials.</summary>
    private static Factory NewFactory(GridPosition? coreOrigin = null, int size = Size)
    {
        var factory = NewGame.Sandbox(
            Content, coreOrigin ?? CoreOrigin, mapWidth: size, mapHeight: size, rules: new GameRules(InstantBuild: false));
        var tiles = new TerrainTile[size, size];
        for (var x = 0; x < size; x++)
        {
            for (var y = 0; y < size; y++)
            {
                tiles[x, y] = Grass;
            }
        }

        factory.Terrain = TerrainMap.FromTiles(tiles);
        foreach (var id in ResearchState.DemoStructureIds)
        {
            factory.Research.ForceUnlock(id);
        }

        factory.Wallet.AddMaterial(Ore, 500);
        factory.Wallet.AddMaterial(Plate, 500);
        factory.Wallet.AddMaterial("copper-wire", 200);
        return factory;
    }

    private static BuilderUnit Drone(Factory factory) => factory.State.Builders[0];

    private static PlaceBelt Belt(GridPosition tile) => new(Player, tile, Direction.East, "conveyor-basic");

    private static float Distance(BuilderUnit drone, float x, float y) =>
        MathF.Sqrt((x - drone.X) * (x - drone.X) + (y - drone.Y) * (y - drone.Y));

    private static int RunUntil(SimHost host, Func<bool> done, int maxTicks = 20_000)
    {
        for (var tick = 1; tick <= maxTicks; tick++)
        {
            host.AdvanceTicks(1);
            if (done())
            {
                return tick;
            }
        }

        throw new InvalidOperationException("condizione mai raggiunta");
    }

    [Theory]
    [InlineData(0, 22.5f)]
    [InlineData(1, 24.75f)]
    [InlineData(2, 26.6f)]
    public void MovesAtItsTierSpeedForOneSecond(int tier, float speed)
    {
        var factory = NewFactory();
        factory.State.SetCore(CoreOrigin, tier);
        var host = new SimHost(factory);
        var drone = Drone(factory);
        var (x0, y0) = (drone.X, drone.Y);

        host.Submit(new SetDroneInput(Player, 1, 0));
        host.AdvanceTicks(Simulation.TickRate);

        Assert.Equal(x0 + speed, drone.X, 3);
        Assert.Equal(y0, drone.Y);
        Assert.Equal(0f, drone.Rotation);
    }

    [Fact]
    public void DiagonalIsNormalized()
    {
        var factory = NewFactory();
        var host = new SimHost(factory);
        var drone = Drone(factory);
        var (x0, y0) = (drone.X, drone.Y);

        host.Submit(new SetDroneInput(Player, -1, 1));
        host.AdvanceTicks(Simulation.TickRate);

        var dx = drone.X - x0;
        var dy = drone.Y - y0;
        Assert.Equal(22.5f, MathF.Sqrt(dx * dx + dy * dy), 3);
        Assert.Equal(-dx, dy, 4);
        Assert.Equal(135f, drone.Rotation, 3);
    }

    [Fact]
    public void StaysInsideTheMap()
    {
        var factory = NewFactory(coreOrigin: new GridPosition(5, 5), size: 40);
        var host = new SimHost(factory);
        var drone = Drone(factory);

        host.Submit(new SetDroneInput(Player, -1, -1));
        host.AdvanceTicks(2 * Simulation.TickRate);
        Assert.Equal(0.5f, drone.X);
        Assert.Equal(0.5f, drone.Y);

        host.Submit(new SetDroneInput(Player, 1, 1));
        host.AdvanceTicks(4 * Simulation.TickRate);
        Assert.Equal(39.5f, drone.X);
        Assert.Equal(39.5f, drone.Y);
    }

    [Fact]
    public void StaysStillWithoutInput()
    {
        var factory = NewFactory();
        var host = new SimHost(factory);
        var drone = Drone(factory);
        // A far plan does not pull the drone any more.
        host.Submit(Belt(new GridPosition(180, 101)));

        host.AdvanceTicks(5 * Simulation.TickRate);

        Assert.Equal(101.5f, drone.X);
        Assert.Equal(101.5f, drone.Y);
        Assert.Single(drone.Plans);
        Assert.Empty(factory.Sites);
    }

    [Fact]
    public void InputIsHeldBetweenTicksUntilChanged()
    {
        var factory = NewFactory();
        var host = new SimHost(factory);
        var drone = Drone(factory);

        host.Submit(new SetDroneInput(Player, 0, 1));
        host.AdvanceTicks(30);
        var y30 = drone.Y;
        host.AdvanceTicks(30);
        Assert.Equal(101.5f + 22.5f, drone.Y, 3);
        Assert.True(drone.Y > y30);
        Assert.Equal(90f, drone.Rotation, 3);

        host.Submit(new SetDroneInput(Player, 0, 0));
        host.AdvanceTicks(1);
        var stopped = (drone.X, drone.Y);
        host.AdvanceTicks(60);
        Assert.Equal(stopped, (drone.X, drone.Y));
        Assert.Equal(90f, drone.Rotation, 3); // the heading stays
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(0, -2)]
    [InlineData(5, 5)]
    public void InputOutsideMinusOneToOneIsRefused(int dx, int dy)
    {
        var factory = NewFactory();
        var host = new SimHost(factory);
        var rejected = new List<RefusalCode>();
        host.CommandRejected += (_, r) => rejected.Add(r.Code);

        host.Submit(new SetDroneInput(Player, dx, dy));
        host.AdvanceTicks(10);

        Assert.Equal([RefusalCode.InvalidCommand], rejected);
        Assert.Equal(101.5f, Drone(factory).X);
        Assert.Equal(101.5f, Drone(factory).Y);
    }

    [Fact]
    public void InputForAPlayerWithoutDroneIsRefused()
    {
        var factory = NewFactory();
        var host = new SimHost(factory);
        var rejected = new List<RefusalCode>();
        host.CommandRejected += (_, r) => rejected.Add(r.Code);

        host.Submit(new SetDroneInput(7, 1, 0));
        host.AdvanceTicks(1);

        Assert.Equal([RefusalCode.InvalidCommand], rejected);
    }

    [Fact]
    public void OutOfRangePlanWaitsAndStartsWhenTheDroneGetsClose()
    {
        var factory = NewFactory();
        var host = new SimHost(factory);
        var drone = Drone(factory);
        // Belt centre (161.5, 101.5): 60 tiles east of the drone.
        var far = new GridPosition(161, 101);
        host.Submit(Belt(far));
        host.AdvanceTicks(120);
        Assert.Single(drone.Plans);
        Assert.Empty(factory.Sites);
        Assert.False(factory.IsPlanInRange(drone, drone.Plans[0]));

        host.Submit(new SetDroneInput(Player, 1, 0));
        var flown = RunUntil(host, () => factory.Sites.Count == 1, maxTicks: 300);
        // (60 − 27.5) / 22.5 s ≈ 86.7 ticks.
        Assert.InRange(flown, 86, 89);
        Assert.True(Distance(drone, 161.5f, 101.5f) <= 27.5f);

        host.Submit(new SetDroneInput(Player, 0, 0));
        RunUntil(host, () => factory.Belts.Contains(far));
        Assert.Empty(drone.Plans);
    }

    [Fact]
    public void OutOfRangePlanAtTheHeadDoesNotBlockThoseBehindInRange()
    {
        var factory = NewFactory();
        var host = new SimHost(factory);
        var drone = Drone(factory);
        var far = new GridPosition(170, 101);
        var near = new[] { new GridPosition(105, 101), new GridPosition(106, 101) };
        host.Submit(Belt(far));
        foreach (var tile in near)
        {
            host.Submit(Belt(tile));
        }

        RunUntil(host, () => near.All(factory.Belts.Contains), maxTicks: 600);

        var plan = Assert.Single(drone.Plans);
        Assert.Equal(far, plan.Spec.Origin);
        Assert.False(factory.Belts.Contains(far));
        Assert.Empty(factory.Sites);
    }

    [Fact]
    public void LeavingRangeLetsTheSiteBeAndComingBackResumesIt()
    {
        var factory = NewFactory();
        var host = new SimHost(factory);
        var drone = Drone(factory);
        // Miner centre (126, 101): 24.5 tiles east, 3 tiles inside the range.
        var miner = new GridPosition(125, 100);
        host.Submit(new PlaceBuilding(Player, BuildingKind.Miner, miner, Direction.East));
        host.AdvanceTicks(5);
        var site = Assert.Single(factory.Sites);
        Assert.Equal(site.Id, drone.CurrentSiteId);
        var progress = site.Progress;
        Assert.True(progress > 0f);

        // Fly 2 s west: 45 tiles, out of range after 8 ticks, long before the miner is done.
        host.Submit(new SetDroneInput(Player, -1, 0));
        host.AdvanceTicks(2 * Simulation.TickRate);
        host.Submit(new SetDroneInput(Player, 0, 0));
        host.AdvanceTicks(1);
        Assert.Null(drone.CurrentSiteId);
        var stopped = site.Progress;
        host.AdvanceTicks(120);
        Assert.Equal(stopped, site.Progress);
        Assert.Contains(site, factory.Sites);
        Assert.Single(drone.Plans);

        host.Submit(new SetDroneInput(Player, 1, 0));
        RunUntil(host, () => drone.CurrentSiteId == site.Id, maxTicks: 300);
        host.Submit(new SetDroneInput(Player, 0, 0));
        RunUntil(host, () => factory.Miners.Count == 1);
        Assert.Empty(factory.Sites);
        Assert.Empty(drone.Plans);
    }

    [Fact]
    public void BreakingPlanInRangeTakesTheBlockApart()
    {
        var factory = NewFactory();
        var near = new GridPosition(108, 101);
        var far = new GridPosition(170, 101);
        Assert.True(factory.PlaceInstant(new BuildSpec("conveyor-basic", null, near, Direction.East), charge: false));
        Assert.True(factory.PlaceInstant(new BuildSpec("conveyor-basic", null, far, Direction.East), charge: false));
        var host = new SimHost(factory);
        var drone = Drone(factory);

        host.Submit(new Remove(Player, far));
        host.Submit(new Remove(Player, near));
        RunUntil(host, () => drone.Plans.Count == 1 && factory.Sites.Count == 0, maxTicks: 600);

        Assert.False(factory.Belts.Contains(near));
        Assert.True(factory.Belts.Contains(far));
        var plan = Assert.Single(drone.Plans);
        Assert.True(plan.Breaking);
        Assert.Equal(far, plan.Spec.Origin);
        host.AdvanceTicks(120);
        Assert.True(factory.Belts.Contains(far));
    }

    [Fact]
    public void DroneStandingInsideABigSiteBuildsIt()
    {
        // 40×40, shard at (30, 30): the drone at its centre stands inside the foundation's 4×4 site.
        var factory = NewFactory(coreOrigin: new GridPosition(30, 30), size: 40);
        var definition = Content.CoreTiers[1];
        factory.Research.ForceUnlock(definition.Id);
        foreach (var entry in definition.BuildCost)
        {
            factory.Wallet.AddMaterial(entry.ItemId, entry.Amount);
        }

        var host = new SimHost(factory);
        var drone = Drone(factory);
        host.Submit(new PlaceBuilding(Player, BuildingKind.CoreFoundation, new GridPosition(30, 30), Direction.East));
        host.AdvanceTicks(1);
        Assert.Single(factory.Sites);
        // The foundation covers (30..33, 30..33): the drone stands inside it and never moves.
        Assert.InRange(drone.X, 30f, 34f);
        Assert.InRange(drone.Y, 30f, 34f);

        RunUntil(host, () => factory.CoreTier == 1, maxTicks: 60_000);

        Assert.Empty(factory.Sites);
        Assert.Empty(drone.Plans);
        Assert.Equal(31.5f, drone.X);
        Assert.Equal(31.5f, drone.Y);
    }
}
