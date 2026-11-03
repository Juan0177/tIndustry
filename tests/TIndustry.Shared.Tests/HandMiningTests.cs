using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Hand mining (spec «Scavo a mano»): <see cref="SetMining"/> points the drone at a deposit cell within build range;
/// it mines one kind of ore at a time into its hold at its tier's rate (0.7 / 0.9 / 1.1 pieces/s, hold 30 / 50 / 70)
/// and, within 6 cells of the Core, unloads it up to the Core maximum, the rest staying in the hold.
/// </summary>
public sealed class HandMiningTests
{
    private const int Player = 0;
    private const int Size = 200;
    private const string IronOre = "iron-ore";
    private const string CopperOre = "copper-ore";
    private const string TitaniumOre = "titanium-ore";

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    /// <summary>Shard 3×3 on (100..102, 100..102): its east edge is x = 103.</summary>
    private static readonly GridPosition CoreOrigin = new(100, 100);

    /// <summary>Iron by the Core (9 tiles from the drone's start), copper and titanium next to it.</summary>
    private static readonly GridPosition IronNear = new(110, 101);

    private static readonly GridPosition CopperNear = new(111, 101);

    private static readonly GridPosition TitaniumNear = new(112, 101);

    /// <summary>Iron far from the Core: a drone over (125.5, 101.5) mines it 20+ cells from the Core.</summary>
    private static readonly GridPosition IronFar = new(130, 101);

    private static readonly GridPosition CopperFar = new(131, 101);

    /// <summary>Iron 38.5 tiles from the drone's start: out of build range (27.5).</summary>
    private static readonly GridPosition IronOutOfRange = new(140, 101);

    private static Factory NewFactory(int tier = 0)
    {
        var factory = NewGame.Sandbox(
            Content, CoreOrigin, mapWidth: Size, mapHeight: Size, rules: new GameRules(InstantBuild: true));
        var tiles = new TerrainTile[Size, Size];
        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                tiles[x, y] = new TerrainTile(FloorKind.Grass, WallKind.None, DepositKind.None);
            }
        }

        foreach (var (tile, deposit) in new[]
                 {
                     (IronNear, DepositKind.Iron), (CopperNear, DepositKind.Copper), (TitaniumNear, DepositKind.Titanium),
                     (IronFar, DepositKind.Iron), (CopperFar, DepositKind.Copper), (IronOutOfRange, DepositKind.Iron)
                 })
        {
            tiles[tile.X, tile.Y] = new TerrainTile(FloorKind.Grass, WallKind.None, deposit);
        }

        factory.Terrain = TerrainMap.FromTiles(tiles);
        if (tier != 0)
        {
            factory.State.SetCore(CoreOrigin, tier);
        }

        return factory;
    }

    private static BuilderUnit Drone(Factory factory) => factory.State.Builders[0];

    /// <summary>Puts the drone over (125.5, 101.5): 22.5 cells from the Core, the far deposits in range.</summary>
    private static void AwayFromCore(BuilderUnit drone)
    {
        drone.X = 125.5f;
        drone.Y = 101.5f;
    }

    private static (SimHost Host, List<RefusalCode> Rejected) Host(Factory factory)
    {
        var host = new SimHost(factory);
        var rejected = new List<RefusalCode>();
        host.CommandRejected += (_, r) => rejected.Add(r.Code);
        return (host, rejected);
    }

    [Theory]
    [InlineData(0, 7)]
    [InlineData(1, 9)]
    [InlineData(2, 11)]
    public void MinesAtItsTierRateForTenSeconds(int tier, int pieces)
    {
        var factory = NewFactory(tier);
        var drone = Drone(factory);
        AwayFromCore(drone);
        var (host, rejected) = Host(factory);

        host.Submit(new SetMining(Player, IronFar));
        host.AdvanceTicks(10 * Simulation.TickRate - 1);
        Assert.Equal(pieces - 1, drone.HoldCount); // integer progress: the last piece lands on the very last tick
        host.AdvanceTicks(1);

        Assert.Empty(rejected);
        Assert.Equal(IronOre, drone.HoldItemId);
        Assert.Equal(pieces, drone.HoldCount);
        Assert.Equal(IronFar, drone.MiningTile);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void TitaniumOnlyWithGamma(int tier, bool mines)
    {
        var factory = NewFactory(tier);
        var drone = Drone(factory);
        drone.X = 112.5f; // over the titanium, 9.5 cells from the Core edge (nucleus: x = 105, 7.5 cells)
        var (host, rejected) = Host(factory);

        host.Submit(new SetMining(Player, TitaniumNear));
        host.AdvanceTicks(5 * Simulation.TickRate);

        if (mines)
        {
            Assert.Empty(rejected);
            Assert.Equal(TitaniumOre, drone.HoldItemId);
            Assert.Equal(5, drone.HoldCount); // 1.1/s × 5 s = 5.5
        }
        else
        {
            Assert.Equal([RefusalCode.MiningNotMineable], rejected);
            Assert.Null(drone.MiningTile);
            Assert.Equal(0, drone.HoldCount);
        }
    }

    [Fact]
    public void FullHoldStopsMining()
    {
        var factory = NewFactory();
        var drone = Drone(factory);
        AwayFromCore(drone);
        var (host, _) = Host(factory);

        host.Submit(new SetMining(Player, IronFar));
        host.AdvanceTicks(60 * Simulation.TickRate); // 42 pieces' worth

        Assert.Equal(30, drone.HoldCount);
        Assert.Equal(IronFar, drone.MiningTile); // still pointed at the deposit, just waiting
        Assert.InRange(drone.MiningProgress, 0, UnitDefinition.MineProgressPerPiece - 1);
        Assert.Equal(DepositKind.Iron, factory.Terrain![IronFar].Deposit); // the deposit is not consumed
    }

    [Fact]
    public void OneItemAtATime()
    {
        var factory = NewFactory();
        var drone = Drone(factory);
        AwayFromCore(drone);
        var (host, rejected) = Host(factory);
        host.Submit(new SetMining(Player, IronFar));
        host.AdvanceTicks(5 * Simulation.TickRate);
        Assert.Equal(3, drone.HoldCount);

        // Copper while the hold carries iron: refused, the drone keeps mining iron.
        host.Submit(new SetMining(Player, CopperFar));
        host.AdvanceTicks(1);
        Assert.Equal([RefusalCode.MiningHoldOtherItem], rejected);
        Assert.Equal(IronFar, drone.MiningTile);
        Assert.Equal(IronOre, drone.HoldItemId);

        // Back by the Core the iron goes in, then copper is fine.
        var ironBefore = factory.Wallet.MaterialCount(IronOre);
        drone.X = 101.5f;
        host.Submit(new SetMining(Player, null));
        host.AdvanceTicks(1);
        Assert.Equal(0, drone.HoldCount);
        Assert.Null(drone.HoldItemId);
        Assert.Equal(ironBefore + 3, factory.Wallet.MaterialCount(IronOre));

        drone.X = 125.5f;
        host.Submit(new SetMining(Player, CopperFar));
        host.AdvanceTicks(5 * Simulation.TickRate);
        Assert.Single(rejected);
        Assert.Equal(CopperOre, drone.HoldItemId);
        Assert.Equal(3, drone.HoldCount);
    }

    [Theory]
    [InlineData(109f, true)] // 6 cells east of the Core's edge (x = 103)
    [InlineData(110f, false)] // 7 cells
    public void UnloadsWithinSixCellsOfTheCore(float x, bool unloads)
    {
        var factory = NewFactory();
        var drone = Drone(factory);
        drone.X = x;
        drone.HoldItemId = IronOre;
        drone.HoldCount = 5;
        var before = factory.Wallet.MaterialCount(IronOre);
        var (host, _) = Host(factory);

        host.AdvanceTicks(1);

        Assert.Equal(unloads ? 0 : 5, drone.HoldCount);
        Assert.Equal(before + (unloads ? 5 : 0), factory.Wallet.MaterialCount(IronOre));
    }

    [Fact]
    public void UnloadsUpToTheCoreMaximumAndKeepsTheRest()
    {
        var factory = NewFactory();
        var drone = Drone(factory);
        var capacity = factory.State.Storage.Capacity(IronOre);
        TopUp(factory, IronOre, capacity - 5);
        drone.HoldItemId = IronOre;
        drone.HoldCount = 20;
        var (host, _) = Host(factory);

        host.AdvanceTicks(1);

        Assert.Equal(capacity, factory.Wallet.MaterialCount(IronOre));
        Assert.Equal(15, drone.HoldCount);
        Assert.Equal(IronOre, drone.HoldItemId);
    }

    [Fact]
    public void FullCoreStopsMiningAndNothingIsLost()
    {
        var factory = NewFactory();
        var drone = Drone(factory);
        var capacity = factory.State.Storage.Capacity(IronOre);
        TopUp(factory, IronOre, capacity);
        var (host, _) = Host(factory);

        // Next to the Core with the Core full of iron: the hold fills up and mining stops.
        host.Submit(new SetMining(Player, IronNear));
        host.AdvanceTicks(60 * Simulation.TickRate);
        Assert.Equal(30, drone.HoldCount);
        Assert.Equal(capacity, factory.Wallet.MaterialCount(IronOre));

        // Room again in the Core: the hold unloads the very next tick and mining resumes.
        Assert.True(factory.Wallet.TryRemoveMaterial(IronOre, 10));
        host.AdvanceTicks(1);
        Assert.Equal(capacity, factory.Wallet.MaterialCount(IronOre));
        Assert.Equal(20, drone.HoldCount);
        host.AdvanceTicks(20 * Simulation.TickRate);
        Assert.Equal(30, drone.HoldCount);
        Assert.Equal(DepositKind.Iron, factory.Terrain![IronNear].Deposit);
    }

    [Fact]
    public void MinedNearTheCoreGoesStraightIn()
    {
        var factory = NewFactory();
        var drone = Drone(factory);
        var before = factory.Wallet.MaterialCount(IronOre);
        var (host, _) = Host(factory);

        host.Submit(new SetMining(Player, IronNear));
        host.AdvanceTicks(10 * Simulation.TickRate);

        Assert.Equal(0, drone.HoldCount);
        Assert.Equal(before + 7, factory.Wallet.MaterialCount(IronOre));
        Assert.Equal(0, factory.CoreDeliveredItems); // the belt counter (tutorial step) stays for belts
    }

    [Fact]
    public void RefusesOutOfRangeNotMineableAndNoDeposit()
    {
        var factory = NewFactory();
        var drone = Drone(factory);
        var (host, rejected) = Host(factory);

        host.Submit(new SetMining(Player, IronOutOfRange));
        host.Submit(new SetMining(Player, TitaniumNear));
        host.Submit(new SetMining(Player, new GridPosition(105, 105)));
        host.Submit(new SetMining(Player, new GridPosition(-1, 5)));
        host.Submit(new SetMining(7, IronNear));
        host.AdvanceTicks(1);

        Assert.Equal(
            [RefusalCode.MiningOutOfRange, RefusalCode.MiningNotMineable, RefusalCode.MiningNoDeposit,
             RefusalCode.MiningNoDeposit, RefusalCode.InvalidCommand],
            rejected);
        Assert.Null(drone.MiningTile);
        Assert.Equal(RefusalCode.MiningOutOfRange, factory.MiningRefusal(Player, IronOutOfRange));
        Assert.Null(factory.MiningRefusal(Player, IronNear));
    }

    [Fact]
    public void LeavingRangeStopsAndStopCommandEnds()
    {
        var factory = NewFactory();
        var drone = Drone(factory);
        AwayFromCore(drone);
        var (host, _) = Host(factory);
        host.Submit(new SetMining(Player, IronFar));
        host.AdvanceTicks(2 * Simulation.TickRate);
        Assert.Equal(1, drone.HoldCount);

        // 30+ cells away: no progress while out of range (the target stays).
        drone.X = 160.5f;
        host.AdvanceTicks(10 * Simulation.TickRate);
        Assert.Equal(1, drone.HoldCount);
        Assert.Equal(IronFar, drone.MiningTile);

        drone.X = 125.5f;
        host.Submit(new SetMining(Player, null));
        host.AdvanceTicks(10 * Simulation.TickRate);
        Assert.Null(drone.MiningTile);
        Assert.Equal(0, drone.MiningProgress);
        Assert.Equal(1, drone.HoldCount);
    }

    [Fact]
    public void PausedGameDoesNotMine()
    {
        var factory = NewFactory();
        var drone = Drone(factory);
        AwayFromCore(drone);
        var (host, _) = Host(factory);
        host.Submit(new SetMining(Player, IronFar));
        host.AdvanceTicks(5 * Simulation.TickRate);
        var progress = drone.MiningProgress;

        for (var i = 0; i < 600; i++)
        {
            Assert.Equal(0, host.Advance(0));
        }

        Assert.Equal(3, drone.HoldCount);
        Assert.Equal(progress, drone.MiningProgress);
    }

    [Fact]
    public void TwentyTimesSpeedMinesAndUnloadsLikeSingleTicks()
    {
        var fast = NewFactory();
        var slow = NewFactory();
        var (fastHost, _) = Host(fast);
        var (slowHost, _) = Host(slow);
        // Mining the iron by the Core with the Core almost full: unloads every tick, then the hold fills.
        foreach (var factory in new[] { fast, slow })
        {
            TopUp(factory, IronOre, factory.State.Storage.Capacity(IronOre) - 20);
        }

        fastHost.Submit(new SetMining(Player, IronNear));
        slowHost.Submit(new SetMining(Player, IronNear));
        fastHost.Speed = 20;
        var ticks = 0;
        while (ticks < 120 * Simulation.TickRate)
        {
            ticks += fastHost.Advance(1.0 / 60.0);
        }

        slowHost.AdvanceTicks(ticks);

        Assert.Equal(fast.CurrentTick, slow.CurrentTick);
        Assert.Equal(slow.Wallet.MaterialCount(IronOre), fast.Wallet.MaterialCount(IronOre));
        Assert.Equal(slow.State.Storage.Capacity(IronOre), fast.Wallet.MaterialCount(IronOre));
        Assert.Equal(Drone(slow).HoldCount, Drone(fast).HoldCount);
        Assert.Equal(30, Drone(fast).HoldCount); // 84 mined: 20 into the Core, 30 in the hold, the rest never dug
        Assert.Equal(Drone(slow).MiningProgress, Drone(fast).MiningProgress);
    }

    [Fact]
    public void UnitsCarryTheMiningNumbers()
    {
        Assert.Equal(new[] { IronOre, CopperOre, "lead-ore", "coal" }, Content.Units["alpha"].MineItems);
        Assert.Equal(new[] { IronOre, CopperOre, "lead-ore", "coal" }, Content.Units["beta"].MineItems);
        Assert.Equal(new[] { IronOre, CopperOre, "lead-ore", "coal", TitaniumOre }, Content.Units["gamma"].MineItems);
        Assert.Equal(new[] { 700L, 900L, 1100L }, new[] { "alpha", "beta", "gamma" }.Select(u => Content.Units[u].MineRateMilli));
        Assert.Equal(new[] { 30, 50, 70 }, new[] { "alpha", "beta", "gamma" }.Select(u => Content.Units[u].HoldCapacity));
        foreach (var (id, unit) in FactoryContent.DefaultUnits)
        {
            Assert.Equal(unit, Content.Units[id]);
        }
    }

    [Fact]
    public void HoldAndMiningSurviveASave()
    {
        var factory = NewFactory();
        var drone = Drone(factory);
        AwayFromCore(drone);
        var (host, _) = Host(factory);
        host.Submit(new SetMining(Player, IronFar));
        host.AdvanceTicks(5 * Simulation.TickRate + 17);

        var data = factory.Capture();
        data.TerrainHash = null; // the hand-made terrain is not the classic one the save regenerates
        var restored = Factory.Restore(Content, data);
        var copy = Drone(restored);

        Assert.Equal(drone.MiningTile, copy.MiningTile);
        Assert.Equal(drone.HoldItemId, copy.HoldItemId);
        Assert.Equal(drone.HoldCount, copy.HoldCount);
        Assert.Equal(drone.MiningProgress, copy.MiningProgress);
    }

    private static void TopUp(Factory factory, string itemId, int target)
    {
        var have = factory.Wallet.MaterialCount(itemId);
        if (have < target)
        {
            factory.Wallet.AddMaterial(itemId, target - have);
        }
        else if (have > target)
        {
            Assert.True(factory.Wallet.TryRemoveMaterial(itemId, have - target));
        }
    }
}
