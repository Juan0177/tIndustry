using System.Text.Json;
using System.Text.Json.Nodes;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The upgrades of the research (spec «Ricerca allargata», «Potenziamenti»): nodes of kind <c>upgrade</c> with nothing
/// to place whose bonus the simulation reads (<see cref="Upgrades"/>): the drone (build speed, hold, range, hand-mining
/// rate), the miners, the forno / advanced smelter / assembler and the maximum per item of the Core and of the storage
/// blocks. Level 2 replaces level 1; without upgrades every number is today's.
/// </summary>
public sealed class UpgradeTests
{
    private const int Player = 0;
    private const int Size = 200;
    private const string IronOre = "iron-ore";
    private const string Plate = "iron-plate";

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    /// <summary>Shard 3×3 on (100..102, 100..102).</summary>
    private static readonly GridPosition CoreOrigin = new(100, 100);

    /// <summary>Iron far from the Core: a drone over (125.5, 101.5) mines it 20+ cells from the Core.</summary>
    private static readonly GridPosition IronFar = new(130, 101);

    /// <summary>A T1 miner's 2×2 of iron right east of the shard: it hands its ore straight to the Core.</summary>
    private static readonly GridPosition MinerOrigin = new(103, 100);

    private static readonly string[] DroneLevelOne =
        [Upgrades.DroneBuildId, Upgrades.DroneHoldId, Upgrades.DroneRangeId, Upgrades.DroneMineId];

    /// <summary>Sandbox on a grass map with the deposits of these tests (instant build unless asked).</summary>
    private static Factory NewFactory(bool instantBuild = true)
    {
        var factory = NewGame.Sandbox(
            Content, CoreOrigin, mapWidth: Size, mapHeight: Size, rules: new GameRules(InstantBuild: instantBuild));
        var tiles = new TerrainTile[Size, Size];
        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                tiles[x, y] = new TerrainTile(FloorKind.Grass, WallKind.None, DepositKind.None);
            }
        }

        tiles[IronFar.X, IronFar.Y] = new TerrainTile(FloorKind.Grass, WallKind.None, DepositKind.Iron);
        for (var dx = 0; dx < 2; dx++)
        {
            for (var dy = 0; dy < 2; dy++)
            {
                tiles[MinerOrigin.X + dx, MinerOrigin.Y + dy] = new TerrainTile(FloorKind.Grass, WallKind.None, DepositKind.Iron);
            }
        }

        factory.Terrain = TerrainMap.FromTiles(tiles);
        return factory;
    }

    private static void Unlock(Factory factory, params string[] ids)
    {
        foreach (var id in ids)
        {
            factory.Research.ForceUnlock(id);
        }
    }

    private static BuilderUnit Drone(Factory factory) => factory.State.Builders[0];

    private static void AwayFromCore(BuilderUnit drone)
    {
        drone.X = 125.5f;
        drone.Y = 101.5f;
    }

    private static void SetStock(Factory f, string itemId, int amount)
    {
        var have = f.Wallet.MaterialCount(itemId);
        if (have < amount)
        {
            f.Wallet.AddMaterial(itemId, amount - have);
        }
        else
        {
            Assert.True(have == amount || f.Wallet.TryRemoveMaterial(itemId, have - amount));
        }
    }

    // ---------------------------------------------------------------- Content and reader

    [Fact]
    public void ContentHasTheTenUpgradeNodes()
    {
        var expected = new (string Id, string Name, string[] Prerequisites)[]
        {
            (Upgrades.DroneBuildId, "Drone: costruzione", []),
            (Upgrades.DroneHoldId, "Drone: stiva", []),
            (Upgrades.DroneRangeId, "Drone: raggio", []),
            (Upgrades.DroneMineId, "Drone: scavo", []),
            (Upgrades.DroneBoostId, "Drone potenziato", DroneLevelOne),
            (Upgrades.MinersOneId, "Minatori I", [Miner.AdvancedId]),
            (Upgrades.MinersTwoId, "Minatori II", [Upgrades.MinersOneId]),
            (Upgrades.MachinesOneId, "Macchine I", [Crafter.AssemblerBuildingId]),
            (Upgrades.MachinesTwoId, "Macchine II", [Upgrades.MachinesOneId, Crafter.SmelterAdvancedId]),
            (Upgrades.StorageOneId, "Magazzino I", ["container"]),
            (Upgrades.StorageTwoId, "Magazzino II", [Upgrades.StorageOneId, "vault"]),
        };

        Assert.Equal(expected.Select(e => e.Id).Order(), Upgrades.NodeIds.Order());
        Assert.Equal(
            expected.Select(e => e.Id).Order(),
            Content.Structures.Where(s => s.Kind == Upgrades.Kind).Select(s => s.Id).Order());
        foreach (var (id, name, prerequisites) in expected)
        {
            var node = Content.RequireStructure(id);
            Assert.Equal(name, node.DisplayName);
            Assert.Equal(prerequisites, node.Prerequisites);
            Assert.False(node.UnlockedByDefault);
            Assert.NotNull(node.Unlock);
            Assert.Null(Content.FindBuilding(id));
            Assert.Null(Content.FindConveyor(id));
        }
    }

    [Fact]
    public void ContentRefusesUnknownOrMisplacedUpgrades()
    {
        // An upgrade id the simulation does not read, a known upgrade under another kind, an upgrade with a block.
        LoadMutated(root => Structure(root, Upgrades.MinersOneId)["id"] = "upgrade-miners-3");
        LoadMutated(root => Structure(root, Upgrades.StorageOneId)["kind"] = "building");
        LoadMutated(root => Structure(root, "container")["kind"] = Upgrades.Kind);
    }

    private static JsonObject Structure(JsonObject root, string id) =>
        root["structures"]!.AsArray().Single(s => (string?)s!["id"] == id)!.AsObject();

    private static void LoadMutated(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "content.json")))!.AsObject();
        mutate(root);
        var path = Path.Combine(Path.GetTempPath(), $"tindustry-upgrades-{Guid.NewGuid():N}.json");
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

    [Fact]
    public void LevelTwoReplacesLevelOne()
    {
        var research = NewFactory().Research;
        Assert.Equal(
            (0, 0, 0, 0, 0, 0, 0),
            (Upgrades.DroneBuildPercent(research), Upgrades.DroneHoldPercent(research), Upgrades.DroneRangeCells(research),
                Upgrades.DroneMinePercent(research), Upgrades.MinerPercent(research), Upgrades.MachinePercent(research),
                Upgrades.StoragePercent(research)));

        foreach (var id in DroneLevelOne.Concat([Upgrades.MinersOneId, Upgrades.MachinesOneId, Upgrades.StorageOneId]))
        {
            research.ForceUnlock(id);
        }

        Assert.Equal(
            (25, 50, 5, 25, 10, 15, 25),
            (Upgrades.DroneBuildPercent(research), Upgrades.DroneHoldPercent(research), Upgrades.DroneRangeCells(research),
                Upgrades.DroneMinePercent(research), Upgrades.MinerPercent(research), Upgrades.MachinePercent(research),
                Upgrades.StoragePercent(research)));

        foreach (var id in new[] { Upgrades.DroneBoostId, Upgrades.MinersTwoId, Upgrades.MachinesTwoId, Upgrades.StorageTwoId })
        {
            research.ForceUnlock(id);
        }

        Assert.Equal(
            (50, 100, 10, 50, 20, 30, 50),
            (Upgrades.DroneBuildPercent(research), Upgrades.DroneHoldPercent(research), Upgrades.DroneRangeCells(research),
                Upgrades.DroneMinePercent(research), Upgrades.MinerPercent(research), Upgrades.MachinePercent(research),
                Upgrades.StoragePercent(research)));

        // Level 2 alone (a campaign reward given without its prerequisites) is level 2.
        var reward = NewFactory().Research;
        reward.ForceUnlock(Upgrades.DroneBoostId);
        Assert.Equal((50, 100, 10, 50), (Upgrades.DroneBuildPercent(reward), Upgrades.DroneHoldPercent(reward),
            Upgrades.DroneRangeCells(reward), Upgrades.DroneMinePercent(reward)));
    }

    [Fact]
    public void WithoutUpgradesTheDroneIsTheContentUnit()
    {
        foreach (var tier in new[] { 0, 1, 2 })
        {
            var factory = NewFactory();
            if (tier != 0)
            {
                factory.State.SetCore(CoreOrigin, tier);
            }

            Assert.Same(Content.UnitForTier(tier), factory.State.EffectiveUnit);
            Assert.Same(Content.UnitForTier(tier), ((IGameView)factory).EffectiveUnit);
        }
    }

    // ---------------------------------------------------------------- Drone

    [Theory]
    [InlineData(new string[0], 30, 27.5f, 700L, 0.5f)]
    [InlineData(new[] { Upgrades.DroneBuildId, Upgrades.DroneHoldId, Upgrades.DroneRangeId, Upgrades.DroneMineId }, 45, 32.5f, 875L, 0.625f)]
    [InlineData(new[] { Upgrades.DroneBoostId }, 60, 37.5f, 1050L, 0.75f)]
    public void DroneUnitHasTheBonusOfItsLevel(string[] unlocked, int hold, float range, long mineMilli, float buildSpeed)
    {
        var factory = NewFactory();
        Unlock(factory, unlocked);

        var unit = factory.State.EffectiveUnit;

        Assert.Equal(hold, unit.HoldCapacity);
        Assert.Equal(range, unit.BuildRange);
        Assert.Equal(mineMilli, unit.MineRateMilli);
        Assert.Equal(buildSpeed, unit.BuildSpeed);
        Assert.Equal(Content.UnitForTier(0).Speed, unit.Speed);
        Assert.Equal(Content.UnitForTier(0).MineItems, unit.MineItems);
    }

    [Fact]
    public void EachDroneUpgradeChangesOnlyItsNumber()
    {
        var alpha = Content.UnitForTier(0);
        var build = NewFactory();
        Unlock(build, Upgrades.DroneBuildId);
        Assert.Equal(alpha with { BuildSpeed = 0.625f }, build.State.EffectiveUnit);
        var hold = NewFactory();
        Unlock(hold, Upgrades.DroneHoldId);
        Assert.Equal(alpha with { HoldCapacity = 45 }, hold.State.EffectiveUnit);
        var range = NewFactory();
        Unlock(range, Upgrades.DroneRangeId);
        Assert.Equal(alpha with { BuildRange = 32.5f }, range.State.EffectiveUnit);
        var mine = NewFactory();
        Unlock(mine, Upgrades.DroneMineId);
        Assert.Equal(875L, mine.State.EffectiveUnit.MineRateMilli);
        Assert.Equal((alpha.BuildSpeed, alpha.BuildRange, alpha.HoldCapacity),
            (mine.State.EffectiveUnit.BuildSpeed, mine.State.EffectiveUnit.BuildRange, mine.State.EffectiveUnit.HoldCapacity));
    }

    [Fact]
    public void HoldAndRateOfTheOtherTiers()
    {
        // Beta 50 / 0.9, gamma 70 / 1.1: +50% / +100% hold, +25% / +50% rate, rounded down in integers.
        var beta = NewFactory();
        beta.State.SetCore(CoreOrigin, 1);
        Unlock(beta, Upgrades.DroneHoldId, Upgrades.DroneMineId);
        Assert.Equal((75, 1125L), (beta.State.EffectiveUnit.HoldCapacity, beta.State.EffectiveUnit.MineRateMilli));
        var gamma = NewFactory();
        gamma.State.SetCore(CoreOrigin, 2);
        Unlock(gamma, Upgrades.DroneBoostId);
        Assert.Equal((140, 1650L, 37.5f, 1.5f),
            (gamma.State.EffectiveUnit.HoldCapacity, gamma.State.EffectiveUnit.MineRateMilli,
                gamma.State.EffectiveUnit.BuildRange, gamma.State.EffectiveUnit.BuildSpeed));
    }

    /// <summary>Ticks for the drone to finish a T1 miner next to the Core (real construction).</summary>
    private static int TicksToBuildAMiner(params string[] unlocked)
    {
        var factory = NewFactory(instantBuild: false);
        Unlock(factory, unlocked);
        SetStock(factory, IronOre, 100);
        var host = new SimHost(factory);
        var rejected = new List<RefusalCode>();
        host.CommandRejected += (_, r) => rejected.Add(r.Code);
        host.Submit(new PlaceBuilding(Player, BuildingKind.Miner, MinerOrigin, Direction.East));
        for (var tick = 1; tick <= 20_000; tick++)
        {
            host.AdvanceTicks(1);
            if (factory.Miners.Count == 1)
            {
                Assert.Empty(rejected);
                return tick;
            }
        }

        throw new InvalidOperationException("cantiere mai finito");
    }

    [Fact]
    public void BuildSpeedFinishesSitesSooner()
    {
        var plain = TicksToBuildAMiner();
        var level1 = TicksToBuildAMiner(Upgrades.DroneBuildId);
        var level2 = TicksToBuildAMiner(Upgrades.DroneBuildId, Upgrades.DroneBoostId);

        Assert.True(plain > 20, $"{plain}");
        Assert.InRange(level1, (int)(plain / 1.25) - 1, (int)Math.Ceiling(plain / 1.25) + 1);
        Assert.InRange(level2, (int)(plain / 1.5) - 1, (int)Math.Ceiling(plain / 1.5) + 1);
    }

    [Theory]
    [InlineData(new string[0], false)]
    [InlineData(new[] { Upgrades.DroneRangeId }, true)]
    [InlineData(new[] { Upgrades.DroneBoostId }, true)]
    public void RangeReachesFartherPlans(string[] unlocked, bool inRange)
    {
        var factory = NewFactory();
        Unlock(factory, unlocked);
        var drone = Drone(factory);
        // Miner centre (131, 101): about 29.5 cells from the drone at the shard's centre (101.5, 101.5).
        var plan = new BuildPlan(new BuildSpec("miner", BuildingKind.Miner, new GridPosition(130, 100), Direction.East), false);

        Assert.Equal(inRange, factory.IsPlanInRange(drone, plan));
        // About 35.5 cells: only level 2 (37.5) reaches it.
        var far = new BuildPlan(new BuildSpec("miner", BuildingKind.Miner, new GridPosition(136, 100), Direction.East), false);
        Assert.Equal(unlocked.Contains(Upgrades.DroneBoostId), factory.IsPlanInRange(drone, far));
    }

    [Fact]
    public void MiningReachesFartherCells()
    {
        // The drone over the Core's centre is 29 cells from IronFar: out of alpha's 27.5, in the upgraded 32.5.
        var factory = NewFactory();
        Assert.Equal(RefusalCode.MiningOutOfRange, factory.MiningRefusal(Player, IronFar));
        Unlock(factory, Upgrades.DroneRangeId);
        Assert.Null(factory.MiningRefusal(Player, IronFar));
    }

    [Theory]
    [InlineData(new string[0], 30, 700L)]
    [InlineData(new[] { Upgrades.DroneHoldId, Upgrades.DroneMineId }, 45, 875L)]
    [InlineData(new[] { Upgrades.DroneBoostId }, 60, 1050L)]
    public void HandMiningUsesTheUpgradedRateAndHold(string[] unlocked, int hold, long perTick)
    {
        var factory = NewFactory();
        Unlock(factory, unlocked);
        var drone = Drone(factory);
        AwayFromCore(drone);
        var host = new SimHost(factory);

        host.Submit(new SetMining(Player, IronFar));
        host.AdvanceTicks(1);
        Assert.Equal(perTick, drone.MiningProgress);

        host.AdvanceTicks(10_000);
        Assert.Equal(hold, drone.HoldCount); // full, waits
        Assert.False(factory.IsMiningNow(drone));
        Assert.Equal(IronFar, drone.MiningTile);
    }

    [Fact]
    public void UnlockingMidMiningLosesNothing()
    {
        var factory = NewFactory();
        var drone = Drone(factory);
        AwayFromCore(drone);
        var host = new SimHost(factory);
        host.Submit(new SetMining(Player, IronFar));
        host.AdvanceTicks(6_000); // 30 pieces after 2572 ticks, then the full hold waits with its progress
        Assert.Equal(30, drone.HoldCount);
        var progress = drone.MiningProgress;
        Assert.True(progress > 0);

        // The hold grows: mining goes on from the kept progress at the same rate, no piece lost or added.
        factory.Wallet.AddMoney(Content.RequireStructure(Upgrades.DroneHoldId).Unlock!.Money);
        foreach (var m in Content.RequireStructure(Upgrades.DroneHoldId).Unlock!.Materials)
        {
            factory.Wallet.AddMaterial(m.ItemId, m.Amount);
        }

        host.Submit(new UnlockStructure(Player, Upgrades.DroneHoldId));
        host.AdvanceTicks(1);
        Assert.True(factory.Research.IsUnlocked(Upgrades.DroneHoldId));
        var expected = progress + 700;
        Assert.Equal(30 + (int)(expected / UnitDefinition.MineProgressPerPiece), drone.HoldCount);
        Assert.Equal(expected % UnitDefinition.MineProgressPerPiece, drone.MiningProgress);

        // The rate grows mid-piece: the progress already made is kept and the next tick adds the new rate.
        var before = drone.MiningProgress;
        var count = drone.HoldCount;
        factory.Research.ForceUnlock(Upgrades.DroneMineId);
        host.AdvanceTicks(1);
        var total = before + 875;
        Assert.Equal(count + (int)(total / UnitDefinition.MineProgressPerPiece), drone.HoldCount);
        Assert.Equal(total % UnitDefinition.MineProgressPerPiece, drone.MiningProgress);

        host.AdvanceTicks(10_000);
        Assert.Equal(45, drone.HoldCount);
    }

    [Fact]
    public void FullUpgradedHoldSurvivesASave()
    {
        var factory = NewGame.Sandbox(Content, CoreOrigin, mapWidth: Size, mapHeight: Size, seed: 11);
        Unlock(factory, Upgrades.DroneBoostId);
        var drone = Drone(factory);
        drone.HoldItemId = IronOre;
        drone.HoldCount = 60;
        AwayFromCore(drone);

        var data = factory.Capture();
        var restored = Factory.Restore(Content, data);

        Assert.Equal(60, Drone(restored).HoldCount);
        Assert.Equal(IronOre, Drone(restored).HoldItemId);
        Assert.Equal(Snapshot.Checksum(Snapshot.Write(factory.State)), Snapshot.Checksum(Snapshot.Write(restored.State)));

        // The same save without the upgrade: never more than the plain alpha holds.
        data.UnlockedStructures!.Remove(Upgrades.DroneBoostId);
        Assert.Equal(30, Drone(Factory.Restore(Content, data)).HoldCount);
    }

    // ---------------------------------------------------------------- Miners and machines

    private static int MinedIn(int ticks, params string[] unlocked)
    {
        var factory = NewFactory();
        Unlock(factory, unlocked);
        Assert.True(factory.PlaceInstant(new BuildSpec("miner", BuildingKind.Miner, MinerOrigin, Direction.East), charge: false));
        var miner = Assert.Single(factory.Miners);
        Assert.Equal(1f, miner.Efficiency);
        for (var i = 0; i < ticks; i++)
        {
            factory.Step();
        }

        Assert.Equal(100 + Upgrades.MinerPercent(factory.Research), miner.RatePercent);
        return (int)miner.ItemsProduced;
    }

    [Fact]
    public void MinersDigFaster()
    {
        // A T1 on four iron cells: one piece every 2 s (5 in 600 ticks, 50 in 6000).
        Assert.InRange(MinedIn(600), 4, 6);
        Assert.InRange(MinedIn(6_000), 49, 51);
        Assert.InRange(MinedIn(6_000, Upgrades.MinersOneId), 54, 56);
        Assert.InRange(MinedIn(6_000, Upgrades.MinersOneId, Upgrades.MinersTwoId), 59, 61);
        Assert.InRange(MinedIn(600, Upgrades.MinersOneId, Upgrades.MinersTwoId), 5, 7);
    }

    private static Factory MachineSandbox(params string[] unlocked)
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7,
            rules: new GameRules(InstantBuild: true));
        Unlock(factory, Generator.BuildingId, Crafter.SmelterBuildingId, Crafter.AssemblerBuildingId, Crafter.SmelterAdvancedId,
            Crafter.GraphitePressId, Crafter.SeparatorId, Crafter.PlasticizerId);
        Unlock(factory, unlocked);
        return factory;
    }

    private static void Place(Factory factory, BuildingKind kind, int x, int y) =>
        Assert.True(factory.PlaceInstant(
            new BuildSpec(BuildSpec.StructureIdFor(kind)!, kind, new GridPosition(x, y), Direction.East), charge: false), $"{kind}");

    private static Crafter Feed(Crafter crafter, params string[] items)
    {
        foreach (var item in items)
        {
            Assert.True(crafter.TryAccept(item), item);
        }

        return crafter;
    }

    private static int TicksToOutput(Factory factory, Crafter crafter, int pieces = 1)
    {
        for (var tick = 1; tick <= 2_000; tick++)
        {
            factory.Step();
            if (crafter.OutputQueue.Count >= pieces)
            {
                return tick;
            }
        }

        return -1;
    }

    /// <summary>Ticks for one craft of each machine: forno on coal, advanced smelter on a burning generator, assembler unpowered, press.</summary>
    private static (int Smelter, int Advanced, int Assembler, int Press) CraftTicks(params string[] unlocked)
    {
        var forno = MachineSandbox(unlocked);
        Place(forno, BuildingKind.Smelter, 10, 20);
        var smelter = Feed(forno.Smelters.Single(), IronOre, IronOre);
        smelter.SeedFuel();

        var powered = MachineSandbox(unlocked);
        Place(powered, BuildingKind.Generator, 10, 20);
        powered.Generators.Single().RestoreFuel(Generator.FuelBufferCapacity, Generator.SecondsPerFuel);
        Place(powered, BuildingKind.SmelterAdvanced, 12, 20);
        var advanced = Feed(powered.Machines.Single(), IronOre, IronOre);

        var plain = MachineSandbox(unlocked);
        Place(plain, BuildingKind.Assembler, 10, 20);
        var assembler = Feed(plain.Assemblers.Single(), "copper-ore", Plate);

        var pressing = MachineSandbox(unlocked);
        Place(pressing, BuildingKind.GraphitePress, 10, 20);
        var press = Feed(pressing.Machines.Single(), "coal", "coal", "coal");

        return (TicksToOutput(forno, smelter), TicksToOutput(powered, advanced), TicksToOutput(plain, assembler),
            TicksToOutput(pressing, press, pieces: 2));
    }

    [Fact]
    public void MachinesCraftFaster()
    {
        var plain = CraftTicks();
        var level1 = CraftTicks(Upgrades.MachinesOneId);
        var level2 = CraftTicks(Upgrades.MachinesOneId, Upgrades.MachinesTwoId);

        // forno 2 s on coal, advanced smelter 1 s with full power, assembler 1.5 s without power, press 1.5 s per two.
        Assert.Equal((120, 60, 90), (Near(plain.Smelter, 120), Near(plain.Advanced, 60), Near(plain.Assembler, 90)));
        Assert.Equal((104, 52, 78), (Near(level1.Smelter, 104), Near(level1.Advanced, 52), Near(level1.Assembler, 78)));
        Assert.Equal((92, 46, 69), (Near(level2.Smelter, 92), Near(level2.Advanced, 46), Near(level2.Assembler, 69)));
        // The press and the separator are not in the group: unchanged.
        Assert.Equal(plain.Press, level1.Press);
        Assert.Equal(plain.Press, level2.Press);
    }

    /// <summary><paramref name="expected"/> when <paramref name="ticks"/> is within one tick of it, else the ticks.</summary>
    private static int Near(int ticks, int expected) => Math.Abs(ticks - expected) <= 1 ? expected : ticks;

    [Fact]
    public void OnlyFornoAdvancedSmelterAndAssemblerGetTheMachineBonus()
    {
        var factory = MachineSandbox(Upgrades.MachinesOneId, Upgrades.MachinesTwoId);
        Place(factory, BuildingKind.Smelter, 10, 20);
        Place(factory, BuildingKind.Assembler, 12, 20);
        Place(factory, BuildingKind.SmelterAdvanced, 14, 20);
        Place(factory, BuildingKind.GraphitePress, 16, 20);
        Place(factory, BuildingKind.Separator, 18, 20);
        Place(factory, BuildingKind.Plasticizer, 20, 20);
        factory.Step();

        Assert.Equal(
            [(Crafter.SmelterBuildingId, 30), (Crafter.AssemblerBuildingId, 30), (Crafter.SmelterAdvancedId, 30),
                (Crafter.GraphitePressId, 0), (Crafter.SeparatorId, 0), (Crafter.PlasticizerId, 0)],
            factory.State.CraftMachines().Select(c => (c.DefinitionId, c.BonusPercent)).OrderBy(p => p.Item1 switch
            {
                Crafter.SmelterBuildingId => 0,
                Crafter.AssemblerBuildingId => 1,
                Crafter.SmelterAdvancedId => 2,
                Crafter.GraphitePressId => 3,
                Crafter.SeparatorId => 4,
                _ => 5
            }));
    }

    // ---------------------------------------------------------------- Storage

    [Theory]
    [InlineData(new string[0], 1000, 300, 1300)]
    [InlineData(new[] { Upgrades.StorageOneId }, 1250, 375, 1625)]
    [InlineData(new[] { Upgrades.StorageOneId, Upgrades.StorageTwoId }, 1500, 450, 1950)]
    public void StorageMaximumsGrow(string[] unlocked, int core, int standalone, int linked)
    {
        var factory = NewFactory();
        Unlock(factory, unlocked);
        Assert.Equal(core, factory.CoreCapacity(Plate));
        Assert.Equal(core, ((IGameView)factory).CoreCapacity(IronOre));

        Assert.True(factory.PlaceInstant(new BuildSpec("container", BuildingKind.Container, new GridPosition(150, 150), Direction.East), charge: false));
        var container = Assert.Single(factory.Storages);
        Assert.False(container.IsLinked);
        Assert.Equal(standalone, container.Capacity);
        for (var i = 0; i < standalone; i++)
        {
            Assert.True(container.TryAccept(Plate));
        }

        Assert.False(container.TryAccept(Plate));
        Assert.Equal(core, factory.CoreCapacity(Plate));

        // Linked (touching the shard's east edge): the percent applies to tier + linked space.
        Assert.True(factory.PlaceInstant(new BuildSpec("container", BuildingKind.Container, new GridPosition(103, 100), Direction.East), charge: false));
        Assert.Equal(linked, factory.CoreCapacity(Plate));
    }

    [Fact]
    public void UnlockingStorageKeepsEveryItemAndOpensTheCore()
    {
        var factory = NewFactory();
        SetStock(factory, Plate, 1000);
        Assert.False(factory.State.Storage.CanAccept(Plate));

        Unlock(factory, Upgrades.StorageOneId);

        Assert.Equal(1000, factory.Wallet.MaterialCount(Plate));
        Assert.True(factory.State.Storage.CanAccept(Plate));
        Assert.Equal(250, factory.State.Storage.AddClamped(Plate, 400));
        Assert.Equal(1250, factory.Wallet.MaterialCount(Plate));
    }

    [Fact]
    public void AutoSellWhenFullUsesTheUpgradedMaximum()
    {
        var plain = NewFactory();
        var upgraded = NewFactory();
        Unlock(upgraded, Upgrades.StorageOneId);
        foreach (var f in new[] { plain, upgraded })
        {
            var item = f.State.Exchange.Find(Plate)!;
            item.AutoSell = true;
            item.AutoSellWhenFull = true;
            SetStock(f, Plate, 1100);
            for (var i = 0; i < 5 * SimulationClock.TickRate; i++)
            {
                f.Step();
            }
        }

        // Without the upgrade 1100 is over the maximum 1000 and sells at the shard's 10 per second; with it 1100 is
        // under 1250 and stays.
        Assert.Equal(1050, plain.Wallet.MaterialCount(Plate));
        Assert.Equal(1100, upgraded.Wallet.MaterialCount(Plate));

        SetStock(upgraded, Plate, 1250);
        for (var i = 0; i < 5 * SimulationClock.TickRate; i++)
        {
            upgraded.Step();
        }

        Assert.InRange(upgraded.Wallet.MaterialCount(Plate), 1240, 1249);
    }

    [Fact]
    public void StandaloneStorageKeepsItsContentsOverTheSave()
    {
        var factory = NewGame.Sandbox(Content, CoreOrigin, mapWidth: Size, mapHeight: Size, seed: 11,
            rules: new GameRules(InstantBuild: true));
        Unlock(factory, Upgrades.StorageOneId, Upgrades.StorageTwoId);
        var origin = new GridPosition(CoreOrigin.X + 20, CoreOrigin.Y + 20);
        Assert.True(factory.PlaceInstant(new BuildSpec("container", BuildingKind.Container, origin, Direction.East), charge: false));
        var container = Assert.Single(factory.Storages);
        for (var i = 0; i < 450; i++)
        {
            Assert.True(container.TryAccept(Plate));
        }

        var restored = Factory.Restore(Content, factory.Capture());

        var copy = Assert.Single(restored.Storages);
        Assert.Equal(450, copy.Count(Plate));
        Assert.Equal(450, copy.Capacity);
        Assert.False(copy.CanAccept(Plate));
    }

    // ---------------------------------------------------------------- Agents

    [Fact]
    public void AgentStatoShowsTheUpgrades()
    {
        var factory = NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120,
            rules: new GameRules(InstantBuild: true));
        var game = new AgentGame(new SimHost(factory), Content);

        var none = game.Call("stato", JsonDocument.Parse("{}").RootElement);
        var zero = none.GetProperty("potenziamenti");
        foreach (var key in new[] { "costruzione", "stiva", "raggio", "scavo", "minatori", "macchine", "magazzino" })
        {
            Assert.Equal(0, zero.GetProperty(key).GetInt32());
        }

        Assert.Equal(27.5, none.GetProperty("drone").GetProperty("raggio").GetDouble());
        Assert.Equal(30, none.GetProperty("drone").GetProperty("stiva").GetProperty("capienza").GetInt32());

        Unlock(factory, Upgrades.DroneBuildId, Upgrades.DroneBoostId, Upgrades.MinersOneId, Upgrades.MachinesOneId,
            Upgrades.MachinesTwoId, Upgrades.StorageOneId);
        var stato = game.Call("stato", JsonDocument.Parse("{}").RootElement);
        var upgrades = stato.GetProperty("potenziamenti");

        Assert.Equal(
            (50, 100, 10, 50, 10, 30, 25),
            (upgrades.GetProperty("costruzione").GetInt32(), upgrades.GetProperty("stiva").GetInt32(),
                upgrades.GetProperty("raggio").GetInt32(), upgrades.GetProperty("scavo").GetInt32(),
                upgrades.GetProperty("minatori").GetInt32(), upgrades.GetProperty("macchine").GetInt32(),
                upgrades.GetProperty("magazzino").GetInt32()));
        Assert.Equal(37.5, stato.GetProperty("drone").GetProperty("raggio").GetDouble());
        Assert.Equal(60, stato.GetProperty("drone").GetProperty("stiva").GetProperty("capienza").GetInt32());
        Assert.Equal(1250, stato.GetProperty("core").GetProperty("massimo").GetInt32());
    }
}
