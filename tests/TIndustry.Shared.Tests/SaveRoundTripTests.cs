using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

[Collection(SaveStoreCollection.Name)]
public sealed class SaveRoundTripTests
{
    private static readonly string ContentPath =
        Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    [Fact]
    public void NewGameSavesAndReloads()
    {
        var content = FactoryContent.Load(ContentPath);
        var factory = NewGame.Sandbox(content, new GridPosition(9, 14), seed: 7);
        Assert.True(factory.TryPlaceMiner(new GridPosition(2, 7), Direction.East));
        Assert.True(factory.TryPlaceBelt(new GridPosition(4, 8), Direction.East));

        FactorySaveStore.Save("roundtrip", new SimHost(factory));
        Assert.True(FactorySaveStore.TryLoad(content, "roundtrip", out var host, out _));
        var reloaded = (Factory)host!.State;

        Assert.Equal(factory.Miners.Count, reloaded.Miners.Count);
        Assert.Equal(factory.Belts.Count, reloaded.Belts.Count);
        Assert.Equal(factory.Wallet.Money, reloaded.Wallet.Money);
        Assert.NotEmpty(reloaded.Miners);
    }

    [Fact]
    public void OldSaveVersionIsRejectedWithClearMessage()
    {
        var content = FactoryContent.Load(ContentPath);
        var factory = NewGame.Sandbox(content, new GridPosition(9, 14), seed: 7);
        FactorySaveStore.Save("vecchio", new SimHost(factory));
        RewriteVersion("vecchio", FactorySaveData.MinReadableVersion - 1);

        Assert.False(FactorySaveStore.TryLoad(content, "vecchio", out _, out var error));
        Assert.Equal("Salvataggio di una versione precedente: non compatibile.", error);
        Assert.False(FactorySaveStore.TryLoadSlot(content, "vecchio", out _, out var failure));
        Assert.Equal(SaveLoadError.OlderVersion, failure!.Code);
    }

    [Fact]
    public void FutureSaveVersionIsRejected()
    {
        var content = FactoryContent.Load(ContentPath);
        var factory = NewGame.Sandbox(content, new GridPosition(9, 14), seed: 7);
        FactorySaveStore.Save("futuro", new SimHost(factory));
        RewriteVersion("futuro", FactorySaveData.CurrentVersion + 1);

        Assert.False(FactorySaveStore.TryLoad(content, "futuro", out _, out var error));
        // Technical version text must not reach the player.
        Assert.Equal("Salvataggio non leggibile.", error);
    }

    [Fact]
    public void TryLoadReportsTheErrorMessage()
    {
        var content = FactoryContent.Load(ContentPath);
        var factory = NewGame.Sandbox(content, new GridPosition(9, 14), seed: 7);
        FactorySaveStore.Save("vecchio2", new SimHost(factory));
        RewriteVersion("vecchio2", FactorySaveData.MinReadableVersion - 1);

        Assert.False(FactorySaveStore.TryLoad(content, "vecchio2", out var host, out var error));
        Assert.Null(host);
        Assert.Equal("Salvataggio di una versione precedente: non compatibile.", error);
    }

    [Fact]
    public void CorruptSaveReportsUnreadableMessage()
    {
        Directory.CreateDirectory(FactorySaveStore.SavesDirectory);
        File.WriteAllText(FactorySaveStore.SlotPath("rotto"), "{ not json");

        Assert.False(FactorySaveStore.TryLoad(LoadContent(), "rotto", out var host, out var error));
        Assert.Null(host);
        Assert.Equal("Salvataggio non leggibile.", error);
    }

    [Fact]
    public void MissingSaveHasNoErrorMessage()
    {
        Assert.False(FactorySaveStore.TryLoad(LoadContent(), "inesistente", out _, out var error));
        Assert.Null(error);
    }

    /// <summary>Researches the Core tier's structure and puts its build cost in the Core.</summary>
    private static void PrepareTier(Factory factory, int tier)
    {
        var definition = factory.Content.CoreTiers[tier];
        factory.Research.ForceUnlock(definition.Id);
        foreach (var entry in definition.BuildCost)
        {
            factory.Wallet.AddMaterial(entry.ItemId, entry.Amount);
        }
    }

    private static void NoAutoSell(Factory factory)
    {
        foreach (var item in factory.State.Exchange.Items)
        {
            item.AutoSell = false;
        }
    }

    private static void Apply(SimHost host, SimCommand command)
    {
        Refusal? refusal = null;
        void OnRejected(SimCommand c, Refusal r) => refusal = r;
        host.CommandRejected += OnRejected;
        host.Submit(command);
        host.AdvanceTicks(1);
        host.CommandRejected -= OnRejected;
        Assert.Null(refusal);
    }

    private static object ViewOf(IGameView view) => new
    {
        view.CoreTier,
        Core = string.Join(';', view.CoreTiles.OrderBy(t => t.Y).ThenBy(t => t.X)),
        Capacity = view.CoreCapacity("iron-ore"),
        Storages = string.Join(';', view.Storages.Select(s =>
            $"{s.Id}:{s.Kind}:{s.Origin}:{s.IsLinked}:{string.Join(',', s.ContentsInOrder())}")),
        Market = string.Join(';', view.MarketItems.Select(i =>
            $"{i.ItemId}:{i.PriceMilli}:{i.Phase}:{i.AutoSell}:{i.AutoSellWhenFull}:{string.Join(',', i.HistoryMilli)}"))
    };

    [Fact]
    public void RoundTrip_FoundationWithStoragesAndAutoSell()
    {
        var content = LoadContent();
        var origin = new GridPosition(60, 60);
        var factory = NewGame.Sandbox(
            content, origin, mapWidth: 120, mapHeight: 120, seed: 42, rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        factory.Research.ForceUnlock("container");
        factory.Research.ForceUnlock("vault");
        NoAutoSell(factory);
        PrepareTier(factory, 1);
        var host = new SimHost(factory);
        Apply(host, new PlaceBuilding(0, BuildingKind.CoreFoundation, origin, Direction.East));
        Assert.Equal(1, factory.CoreTier);
        // Linked vault on the foundation's east side, standalone container far away with 120 copper ore.
        Assert.True(factory.PlaceInstant(new BuildSpec("vault", BuildingKind.Vault, new GridPosition(64, 60), Direction.East), charge: false));
        Assert.True(factory.PlaceInstant(new BuildSpec("container", BuildingKind.Container, new GridPosition(80, 60), Direction.East), charge: false));
        var container = factory.Storages.Single(s => s.Kind == BuildingKind.Container);
        for (var i = 0; i < 120; i++)
        {
            Assert.True(container.TryAccept("copper-ore"));
        }

        var ore = factory.State.Exchange.Find("iron-ore")!;
        ore.AutoSell = true;
        ore.AutoSellWhenFull = true;
        host.AdvanceTicks(1);
        ore.AutoSellCarry = 30;
        Assert.True(factory.Storages.Single(s => s.Kind == BuildingKind.Vault).IsLinked);
        Assert.False(container.IsLinked);

        FactorySaveStore.Save("v15-foundation", host);
        Assert.True(FactorySaveStore.TryLoad(content, "v15-foundation", out var loaded, out var error), error);

        Assert.Equal(host.Checksum(), loaded!.Checksum());
        Assert.Equal(ViewOf(host.State), ViewOf(loaded.State));
        Assert.Equal(1, loaded.State.CoreTier);
        Assert.Equal(5000, loaded.State.CoreCapacity("iron-ore"));
        Assert.Equal([("copper-ore", 120)], loaded.State.Storages.Single(s => !s.IsLinked).ContentsInOrder());
        var back = ((Factory)loaded.State).State.Exchange.Find("iron-ore")!;
        Assert.Equal((true, true, 30), (back.AutoSell, back.AutoSellWhenFull, back.AutoSellCarry));

        host.AdvanceTicks(600);
        loaded.AdvanceTicks(600);
        Assert.Equal(host.Checksum(), loaded.Checksum());
    }

    [Fact]
    public void RoundTrip_MarketScreenWithWatchedItemAndLastResult()
    {
        var content = LoadContent();
        var origin = new GridPosition(60, 60);
        var factory = NewGame.Sandbox(
            content, origin, mapWidth: 120, mapHeight: 120, seed: 42, rules: new GameRules(InstantBuild: true));
        NoAutoSell(factory);
        factory.Research.ForceUnlock(MarketScreen.BuildingId);
        // Right of the shard (cells 60..62): the screen touches the Core, so it is active.
        var cell = new GridPosition(63, 60);
        Assert.True(factory.PlaceInstant(
            new BuildSpec(MarketScreen.BuildingId, BuildingKind.MarketScreen, cell, Direction.East), charge: false));
        var host = new SimHost(factory);
        Apply(host, new SetBlockFilter(0, cell, "iron-plate"));
        factory.Wallet.AddMaterial("iron-plate", 50);
        Apply(host, new SellAll(0, "iron-plate"));
        var screen = factory.MarketScreen!;
        Assert.Equal("iron-plate", screen.WatchedItemId);
        var result = screen.LastResult;
        Assert.NotNull(result);

        FactorySaveStore.Save("v20-schermo", host);
        var json = File.ReadAllText(FactorySaveStore.SlotPath("v20-schermo"));
        Assert.Contains($"\"version\": {FactorySaveData.CurrentVersion}", json);
        Assert.True(FactorySaveStore.TryLoad(content, "v20-schermo", out var loaded, out var error), error);

        Assert.Equal(host.Checksum(), loaded!.Checksum());
        var back = ((Factory)loaded.State).MarketScreen!;
        Assert.Equal(screen.Id, back.Id);
        Assert.Equal(cell, back.Position);
        Assert.True(back.Active);
        Assert.Equal("iron-plate", back.WatchedItemId);
        Assert.Equal(result, back.LastResult);

        host.AdvanceTicks(600);
        loaded.AdvanceTicks(600);
        Assert.Equal(host.Checksum(), loaded.Checksum());
    }

    [Fact]
    public void Restore_WithTooLowNextEntityId_NewEntitiesGetIdsAboveTheScreens()
    {
        var content = LoadContent();
        var factory = NewGame.Sandbox(
            content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 42, rules: new GameRules(InstantBuild: true));
        Assert.True(factory.PlaceInstant(
            new BuildSpec(MarketScreen.BuildingId, BuildingKind.MarketScreen, new GridPosition(63, 60), Direction.East), charge: false));
        var screenId = factory.MarketScreen!.Id;
        var data = factory.Capture();
        data.NextEntityId = 1;

        var restored = Factory.Restore(content, data);

        Assert.Equal(screenId, restored.MarketScreen!.Id);
        Assert.True(restored.NextEntityId > screenId);
    }

    [Fact]
    public void Checksum_CoversTheScreensWatchedItemAndLastResult()
    {
        var content = LoadContent();
        var factory = NewGame.Sandbox(
            content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 42, rules: new GameRules(InstantBuild: true));
        Assert.True(factory.PlaceInstant(
            new BuildSpec(MarketScreen.BuildingId, BuildingKind.MarketScreen, new GridPosition(63, 60), Direction.East), charge: false));
        var screen = factory.MarketScreen!;
        var plain = Snapshot.Checksum(Snapshot.Write(factory.State));

        screen.WatchedItemId = "coal";
        var watched = Snapshot.Checksum(Snapshot.Write(factory.State));
        Assert.NotEqual(plain, watched);

        screen.LastResult = new ScreenSaleResult("coal", 120, ScreenLed.Purple, Jackpot: false, Tick: 5);
        var withResult = Snapshot.Checksum(Snapshot.Write(factory.State));
        Assert.NotEqual(watched, withResult);

        screen.LastResult = screen.LastResult with { Led = ScreenLed.Yellow, Jackpot = true };
        var bytes = Snapshot.Write(factory.State);
        Assert.NotEqual(withResult, Snapshot.Checksum(bytes));

        var restored = new Factory(Snapshot.Read(content, bytes));
        Assert.Equal("coal", restored.MarketScreen!.WatchedItemId);
        Assert.Equal(screen.LastResult, restored.MarketScreen.LastResult);
        Assert.Equal(Snapshot.Checksum(bytes), Snapshot.Checksum(Snapshot.Write(restored.State)));
    }

    [Fact]
    public void RoundTrip_DuringCoreUpgradeSite()
    {
        var content = LoadContent();
        var origin = new GridPosition(60, 60);
        var factory = NewGame.Sandbox(content, origin, mapWidth: 120, mapHeight: 120, seed: 42, rules: new GameRules());
        NoAutoSell(factory);
        factory.State.SetCore(origin, 1);
        PrepareTier(factory, 2);
        var cost = content.CoreTiers[2].BuildCost;
        var before = cost.Select(c => factory.Wallet.MaterialCount(c.ItemId)).ToList();
        var host = new SimHost(factory);
        var nucleus = factory.CoreUpgradeSpec()!;
        Apply(host, new PlaceBuilding(0, BuildingKind.CoreNucleus, nucleus.Origin, Direction.East));
        for (var i = 0; i < 100_000 && !(factory.Sites.Count > 0 && factory.Sites[0].Progress >= 0.4f); i++)
        {
            host.AdvanceTicks(1);
        }

        var site = Assert.Single(factory.Sites);
        Assert.InRange(site.Progress, 0.4f, 0.6f);
        Assert.Equal(1, factory.CoreTier);

        FactorySaveStore.Save("v15-nucleus", host);
        Assert.True(FactorySaveStore.TryLoad(content, "v15-nucleus", out var loaded, out var error), error);
        Assert.Equal(host.Checksum(), loaded!.Checksum());

        var upgrades = 0;
        var tier = loaded.State.CoreTier;
        for (var i = 0; i < 100_000 && loaded.State.Sites.Count > 0; i++)
        {
            loaded.AdvanceTicks(1);
            if (loaded.State.CoreTier != tier)
            {
                upgrades++;
                tier = loaded.State.CoreTier;
            }
        }

        loaded.AdvanceTicks(600);
        Assert.Equal(2, loaded.State.CoreTier);
        Assert.Equal(1, upgrades);
        Assert.Empty(loaded.State.Sites);
        var after = (Factory)loaded.State;
        Assert.Equal(
            before.Select((count, i) => count - cost[i].Amount),
            cost.Select(c => after.Wallet.MaterialCount(c.ItemId)));
    }

    // ---- Power network (save v17): lasers and battery charge. ----

    private static long PlacePower(Factory factory, BuildingKind kind, int x, int y)
    {
        var origin = new GridPosition(x, y);
        var spec = new BuildSpec(BuildSpec.StructureIdFor(kind)!, kind, origin, Direction.East);
        Assert.True(factory.PlaceInstant(spec, charge: false), $"{kind} su {origin}");
        Assert.True(factory.TryGetSpecAt(origin, out _, out var id));
        return id;
    }

    /// <summary>
    /// A burning generator, two nodes, a busy forno and a battery joined by a laser set by hand (automatic links would
    /// not reach it: a battery is a source). Returns the game and the ids.
    /// </summary>
    internal static (Factory Factory, long Gen, long Node1, long Node2, long Forno, long Battery) PowerLayout(FactoryContent content)
    {
        var factory = NewGame.Sandbox(content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7,
            rules: new GameRules(InstantBuild: true));
        foreach (var id in new[] { Generator.BuildingId, PowerNode.Tier1Id, Battery.BuildingId, Crafter.SmelterBuildingId })
        {
            factory.Research.ForceUnlock(id);
        }

        var gen = PlacePower(factory, BuildingKind.Generator, 10, 20);
        factory.Generators.Single(g => g.Id == gen).RestoreFuel(Generator.FuelBufferCapacity, Generator.SecondsPerFuel);
        var battery = PlacePower(factory, BuildingKind.Battery, 21, 26);
        var node1 = PlacePower(factory, BuildingKind.PowerNode, 16, 20);
        var forno = PlacePower(factory, BuildingKind.Smelter, 24, 20);
        var smelter = factory.Smelters.Single(s => s.Id == forno);
        Assert.True(smelter.TryAccept("iron-ore") && smelter.TryAccept("iron-ore"));
        var node2 = PlacePower(factory, BuildingKind.PowerNode, 21, 20);
        if (!factory.PowerGrid.HasLink(node2, battery))
        {
            Assert.Null(factory.TogglePowerLink(node2, battery));
        }

        Assert.True(factory.PowerGrid.HasLink(node2, battery));
        Assert.Single(factory.PowerGrid.Networks);
        return (factory, gen, node1, node2, forno, battery);
    }

    [Fact]
    public void V17_LasersAndBatteryChargeRoundTripWithTheSameChecksum()
    {
        var content = LoadContent();
        var (factory, _, _, _, _, battery) = PowerLayout(content);
        var host = new SimHost(factory);
        host.AdvanceTicks(120);
        var charge = factory.Batteries.Single(b => b.Id == battery).Charge;
        Assert.True(charge > 0, "the surplus charged the battery");
        Assert.True(charge < Battery.CapacityOf(content));
        var before = host.Checksum();

        FactorySaveStore.Save("v17-corrente", host);
        Assert.True(FactorySaveStore.TryLoadSlot(content, "v17-corrente", out var loaded, out var failure), failure?.Detail);
        var after = (Factory)loaded!.State;

        Assert.Equal(before, loaded.Checksum());
        Assert.Equal(factory.PowerGrid.Links, after.PowerGrid.Links);
        Assert.Equal(charge, after.Batteries.Single(b => b.Id == battery).Charge);
        Assert.Equal(
            factory.PowerGrid.Networks.Select(n => n.Members.ToArray()),
            after.PowerGrid.Networks.Select(n => n.Members.ToArray()));

        // Both go on the same.
        host.AdvanceTicks(600);
        loaded.AdvanceTicks(600);
        Assert.Equal(host.Checksum(), loaded.Checksum());
    }

    [Fact]
    public void LasersAreInTheSnapshotAndItsChecksum()
    {
        var content = LoadContent();
        var (factory, _, _, node2, _, battery) = PowerLayout(content);
        var with = Snapshot.Checksum(Snapshot.Write(factory.State));
        var json = System.Text.Encoding.UTF8.GetString(Snapshot.Write(factory.State));
        Assert.Contains($"{{\"a\":{Math.Min(node2, battery)},\"b\":{Math.Max(node2, battery)}}}", json);

        Assert.Null(factory.TogglePowerLink(node2, battery));
        Assert.NotEqual(with, Snapshot.Checksum(Snapshot.Write(factory.State)));

        Assert.Null(factory.TogglePowerLink(node2, battery));
        Assert.Equal(with, Snapshot.Checksum(Snapshot.Write(factory.State)));
    }

    [Fact]
    public void GameWithoutLasersWritesNoLaserList()
    {
        var factory = NewGame.Sandbox(LoadContent(), new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7);
        Assert.Null(factory.Capture().PowerLinks);
    }

    [Fact]
    public void V19_DroneMiningAndMovingRoundTripsWithTheSameChecksum()
    {
        var content = LoadContent();
        var factory = NewGame.Sandbox(content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7);
        var host = new SimHost(factory);
        var rejected = new List<RefusalCode>();
        host.CommandRejected += (_, r) => rejected.Add(r.Code);
        var iron = DroneFlight.NearestDeposit(factory, DepositKind.Iron, minDistance: 12f);
        DroneFlight.FlyTo(host, factory, iron.X + 0.5f, iron.Y + 0.5f);
        host.Submit(new SetMining(0, iron));
        host.AdvanceTicks(5 * Simulation.TickRate + 17);
        // Half way through a piece, and flying away from the deposit (still within range).
        host.Submit(new SetDroneInput(0, 1, -1));
        host.AdvanceTicks(7);
        Assert.Empty(rejected);
        var drone = factory.BuilderOf(0)!;
        Assert.True(drone.HoldCount > 0 && drone.MiningProgress > 0, $"{drone.HoldCount} {drone.MiningProgress}");
        Assert.Equal((1, -1), (drone.InputX, drone.InputY));
        var before = host.Checksum();

        FactorySaveStore.Save("v19-drone", host);
        Assert.True(FactorySaveStore.TryLoadSlot(content, "v19-drone", out var loaded, out var failure), failure?.Detail);
        var copy = ((Factory)loaded!.State).BuilderOf(0)!;

        Assert.Equal(before, loaded.Checksum());
        Assert.Equal((drone.X, drone.Y, drone.InputX, drone.InputY), (copy.X, copy.Y, copy.InputX, copy.InputY));
        Assert.Equal((drone.MiningTile, drone.MiningProgress), (copy.MiningTile, copy.MiningProgress));
        Assert.Equal((drone.HoldItemId, drone.HoldCount), (copy.HoldItemId, copy.HoldCount));

        // Both go on the same: still flying, still mining.
        host.AdvanceTicks(20);
        loaded.AdvanceTicks(20);
        host.Submit(new SetDroneInput(0, 0, 0));
        loaded.Submit(new SetDroneInput(0, 0, 0));
        host.AdvanceTicks(600);
        loaded.AdvanceTicks(600);
        Assert.Equal(host.Checksum(), loaded.Checksum());
        Assert.NotEqual(before, host.Checksum());
    }

    [Fact]
    public void DroneInputMiningAndHoldAreInTheChecksum()
    {
        var factory = NewGame.Sandbox(LoadContent(), new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7);
        var drone = factory.BuilderOf(0)!;
        ulong Sum() => Snapshot.Checksum(Snapshot.Write(factory.State));
        var seen = new HashSet<ulong> { Sum() };

        drone.InputX = 1;
        Assert.True(seen.Add(Sum()));
        drone.InputY = -1;
        Assert.True(seen.Add(Sum()));
        drone.MiningTile = new GridPosition(3, 4);
        Assert.True(seen.Add(Sum()));
        drone.MiningProgress = 123;
        Assert.True(seen.Add(Sum()));
        drone.HoldItemId = "iron-ore";
        drone.HoldCount = 5;
        Assert.True(seen.Add(Sum()));
        drone.HoldItemId = "copper-ore";
        Assert.True(seen.Add(Sum()));
    }

    // ---- The new machines (save v21): crafters of the research, the impact drill and the miners' covered cells. ----

    /// <summary>True when every cell of the <paramref name="width"/>×<paramref name="height"/> rectangle is free to build on.</summary>
    private static bool Free(Factory factory, int x, int y, int width, int height)
    {
        for (var dy = 0; dy < height; dy++)
        {
            for (var dx = 0; dx < width; dx++)
            {
                if (!factory.CanOccupyFootprint(new GridPosition(x + dx, y + dy), 1))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// First origin (row by row) where a <paramref name="size"/> block and, if <paramref name="withGenerator"/>, a 2×2
    /// generator on its right fit with one free cell all around, and <paramref name="ok"/> holds.
    /// </summary>
    private static GridPosition Spot(Factory factory, int size, bool withGenerator, Func<GridPosition, bool> ok)
    {
        var terrain = factory.Terrain!;
        var width = size + (withGenerator ? Generator.Size : 0);
        for (var y = 1; y + size + 1 < terrain.Height; y++)
        {
            for (var x = 1; x + width + 1 < terrain.Width; x++)
            {
                var origin = new GridPosition(x, y);
                if (Free(factory, x - 1, y - 1, width + 2, Math.Max(size, 2) + 2) && ok(origin))
                {
                    return origin;
                }
            }
        }

        throw new InvalidOperationException($"Nessun posto per un blocco {size}×{size}.");
    }

    /// <summary>Places <paramref name="id"/> at <paramref name="origin"/> and a burning generator touching its right side.</summary>
    private static void PlacePowered(Factory factory, string id, GridPosition origin, int size, string? recipeId = null)
    {
        var spec = new BuildSpec(id, BuildSpec.KindFor(id), origin, Direction.East, recipeId);
        Assert.True(factory.PlaceInstant(spec, charge: false), $"{id} su {origin}");
        var genAt = new GridPosition(origin.X + size, origin.Y);
        Assert.True(factory.PlaceInstant(new BuildSpec(Generator.BuildingId, BuildingKind.Generator, genAt, Direction.East), charge: false));
        factory.Generators[^1].RestoreFuel(Generator.FuelBufferCapacity, Generator.SecondsPerFuel);
    }

    /// <summary>Gives <paramref name="craft"/> every input of its recipe it can take.</summary>
    private static void Feed(Crafter craft)
    {
        foreach (var input in craft.Recipe.Inputs)
        {
            while (craft.TryAccept(input.ItemId))
            {
            }
        }
    }

    /// <summary>
    /// A biome map with each new machine (fed and powered), an impact drill partly on ore, a miner on sand, a miner
    /// partly on ore and an assembler on the circuit recipe (researched).
    /// </summary>
    internal static Factory NewMachinesLayout(FactoryContent content)
    {
        var factory = NewGame.Sandbox(content, new GridPosition(64, 64), rules: new GameRules(InstantBuild: true),
            map: MapGenSettings.Normal((MapSize)128, 5));
        factory.UnlockAllForDemo();
        foreach (var id in new[]
                 {
                     Generator.BuildingId, Crafter.SmelterAdvancedId, Crafter.GraphitePressId, Crafter.SeparatorId,
                     Crafter.PlasticizerId, Miner.ImpactDrillId, Miner.AdvancedId, Crafter.AssemblerBuildingId, "circuit"
                 })
        {
            factory.Research.ForceUnlock(id);
        }

        factory.RefreshAvailableRecipes();
        var terrain = factory.Terrain!;
        bool NoOre(GridPosition o, int size) => terrain.MajorityDeposit(o, size) == DepositKind.None && terrain.CountSandTiles(o, size) == 0;

        foreach (var id in new[] { Crafter.SmelterAdvancedId, Crafter.GraphitePressId, Crafter.SeparatorId, Crafter.PlasticizerId })
        {
            PlacePowered(factory, id, Spot(factory, Crafter.Size, withGenerator: true, o => NoOre(o, Crafter.Size)), Crafter.Size);
        }

        // The drill on part of a deposit (it digs every ore), the T1 on part of a diggable one, the T1 on sand only.
        PlacePowered(factory, Miner.ImpactDrillId, Spot(factory, Miner.ImpactDrillSize, withGenerator: true, o =>
            terrain.MajorityDeposit(o, Miner.ImpactDrillSize) is var d and not DepositKind.None
            && terrain.CountDepositTiles(o, Miner.ImpactDrillSize, d) is > 0 and < 9), Miner.ImpactDrillSize);
        bool T1Digs(DepositKind d) => content.MinerCanDig(Miner.BasicId, d);
        var partial = Spot(factory, Miner.T1Size, withGenerator: false, o =>
            terrain.MajorityDeposit(o, Miner.T1Size, T1Digs) is var d and not DepositKind.None
            && terrain.CountDepositTiles(o, Miner.T1Size, d) is > 0 and < 4);
        Assert.True(factory.PlaceInstant(new BuildSpec(Miner.BasicId, BuildingKind.Miner, partial, Direction.East), charge: false));
        var sand = Spot(factory, Miner.T1Size, withGenerator: false, o =>
            terrain.MajorityDeposit(o, Miner.T1Size) == DepositKind.None && terrain.CountSandTiles(o, Miner.T1Size) > 0);
        Assert.True(factory.PlaceInstant(new BuildSpec(Miner.BasicId, BuildingKind.Miner, sand, Direction.East), charge: false));
        var assemblerAt = Spot(factory, Crafter.Size, withGenerator: false, o => NoOre(o, Crafter.Size));
        Assert.True(factory.PlaceInstant(
            new BuildSpec(Crafter.AssemblerBuildingId, BuildingKind.Assembler, assemblerAt, Direction.East, "craft-circuit"), charge: false));

        foreach (var craft in factory.Machines.Concat(factory.Assemblers))
        {
            Feed(craft);
        }

        return factory;
    }

    [Fact]
    public void V21_NewMachinesDrillAndSandMinerRoundTripWithTheSameChecksum()
    {
        var content = LoadContent();
        var factory = NewMachinesLayout(content);
        Assert.Equal(
            [Crafter.SmelterAdvancedId, Crafter.GraphitePressId, Crafter.SeparatorId, Crafter.PlasticizerId],
            factory.Machines.Select(m => m.DefinitionId));
        var host = new SimHost(factory);
        host.AdvanceTicks(SimulationClock.TickRate / 2);

        var separator = factory.Machines.Single(m => m.DefinitionId == Crafter.SeparatorId);
        Assert.True(separator.IsCrafting && separator.Progress is > 0f and < 1f, $"separatore a metà: {separator.Progress}");
        var drill = factory.Miners.Single(m => m.DefinitionId == Miner.ImpactDrillId);
        Assert.True(drill.CoveredDepositTiles is > 0 and < 9, $"trapano: {drill.CoveredDepositTiles}");
        Assert.True(drill.IsPowered);
        Assert.Contains(factory.Miners, m => m.OutputItemId == "sand");
        Assert.Contains(factory.Miners, m => m.DefinitionId == Miner.BasicId && m.CoveredDepositTiles is > 0 and < 4 && m.OutputItemId != "sand");
        Assert.Equal("craft-circuit", factory.Assemblers.Single().Recipe.Id);
        var before = host.Checksum();

        FactorySaveStore.Save("v21-macchine", host);
        Assert.True(FactorySaveStore.TryLoadSlot(content, "v21-macchine", out var loaded, out var failure), failure?.Detail);
        var after = (Factory)loaded!.State;

        Assert.Equal(before, loaded.Checksum());
        Assert.Equal(factory.Machines.Select(m => (m.Id, m.DefinitionId, m.Position, m.Recipe.Id, m.IsCrafting, m.Progress, m.ItemsCrafted)),
            after.Machines.Select(m => (m.Id, m.DefinitionId, m.Position, m.Recipe.Id, m.IsCrafting, m.Progress, m.ItemsCrafted)));
        Assert.Equal(factory.Machines.Select(m => m.AvailableRecipes.Select(r => r.Id).ToArray()),
            after.Machines.Select(m => m.AvailableRecipes.Select(r => r.Id).ToArray()));
        Assert.Equal(factory.Miners.Select(m => (m.Id, m.DefinitionId, m.Size, m.OutputItemId, m.CoveredDepositTiles, m.Efficiency)),
            after.Miners.Select(m => (m.Id, m.DefinitionId, m.Size, m.OutputItemId, m.CoveredDepositTiles, m.Efficiency)));
        Assert.Equal("craft-circuit", after.Assemblers.Single().Recipe.Id);
        Assert.Contains("craft-circuit", after.Assemblers.Single().AvailableRecipes.Select(r => r.Id));
        Assert.Equal(factory.PowerGrid.Networks.Select(n => n.Members.ToArray()), after.PowerGrid.Networks.Select(n => n.Members.ToArray()));

        // Both go on the same: the separator draws the same 20 pieces, the assembler makes circuits.
        List<string> RunSeparator(SimHost game, out int circuits)
        {
            var state = (Factory)game.State;
            var sep = state.Machines.Single(m => m.DefinitionId == Crafter.SeparatorId);
            var assembler = state.Assemblers.Single();
            var pieces = new List<string>();
            circuits = 0;
            for (var tick = 0; pieces.Count < 20; tick++)
            {
                Assert.True(tick < 60 * SimulationClock.TickRate, "20 pezzi in un minuto");
                Feed(sep);
                Feed(assembler);
                game.AdvanceTicks(1);
                foreach (var piece in sep.OutputQueue.ToList())
                {
                    Assert.True(sep.TryTakeOutput(piece));
                    pieces.Add(piece);
                }

                foreach (var piece in assembler.OutputQueue.ToList())
                {
                    Assert.True(assembler.TryTakeOutput(piece));
                    Assert.Equal("circuit", piece);
                    circuits++;
                }
            }

            return pieces;
        }

        var crafted = separator.ItemsCrafted;
        var never = RunSeparator(host, out var circuitsNever);
        var reloaded = RunSeparator(loaded, out var circuitsLoaded);
        Assert.Equal(never, reloaded);
        Assert.Equal(
            Enumerable.Range(0, 20).Select(n => Crafter.PickRandomOutput(content.FindRecipe("separate-sand")!, factory.Terrain!.Seed, separator.Id, crafted + n)),
            reloaded);
        Assert.True(circuitsLoaded > 0, "l'assemblatore fa circuiti dopo il caricamento");
        Assert.Equal(circuitsNever, circuitsLoaded);
        Assert.Equal(host.Checksum(), loaded.Checksum());
        Assert.NotEqual(before, host.Checksum());
    }

    [Fact]
    public void MachinesAndCoveredCellsAreInTheSnapshot()
    {
        var factory = NewMachinesLayout(LoadContent());
        var json = System.Text.Encoding.UTF8.GetString(Snapshot.Write(factory.State));
        Assert.Contains("\"machines\":[", json);
        Assert.Contains("\"definitionId\":\"separator\"", json);
        Assert.Contains("\"coveredDepositTiles\":", json);
        Assert.Equal(4, factory.Capture().Machines!.Count);
    }

    [Fact]
    public void MachineThatIsNotANewMachine_Unreadable()
    {
        var content = LoadContent();
        var data = NewMachinesLayout(content).Capture();
        foreach (var bad in new[] { Crafter.SmelterBuildingId, Crafter.AssemblerBuildingId, Generator.BuildingId, "non-esiste", "" })
        {
            data.Machines![0].DefinitionId = bad;
            Assert.Throws<InvalidDataException>(() => Factory.Restore(content, data));
        }

        // Covered cells beyond the footprint are cut to it; an absent key is the whole footprint.
        data = NewMachinesLayout(content).Capture();
        data.Miners[0].CoveredDepositTiles = 99;
        data.Miners[1].CoveredDepositTiles = null;
        var restored = Factory.Restore(content, data);
        Assert.Equal(restored.Miners[0].FootprintArea, restored.Miners[0].CoveredDepositTiles);
        Assert.Equal(restored.Miners[1].FootprintArea, restored.Miners[1].CoveredDepositTiles);
    }

    private static FactoryContent LoadContent() => FactoryContent.Load(ContentPath);

    private static void RewriteVersion(string slotId, int version)
    {
        var path = FactorySaveStore.SlotPath(slotId);
        var json = File.ReadAllText(path);
        var current = $"\"version\": {FactorySaveData.CurrentVersion}";
        Assert.Contains(current, json);
        File.WriteAllText(path, json.Replace(current, $"\"version\": {version}"));
    }

    [Fact]
    public void AllEightPlanTwoBlocksRoundTripAndGoOnTheSame()
    {
        var content = LoadContent();
        var factory = PlanTwoScenario.Build(content);
        // Tick 808: a pack in transit and one being formed at once, an item on the powered long bridge. It was found by
        // running the scenario and checking each tick for those three conditions; if a tuning change (belt speed, pack
        // timing, bridge power) breaks the asserts below, search again for a tick where all three hold and put it here.
        PlanTwoScenario.RunTo(factory, 808);

        var data = factory.Capture();
        // Every block has live state now: this is what the round trip has to keep.
        Assert.Contains(data.Belts, b => b.DefinitionId == "overflow-gate" && b.SorterTurns is > 0);
        Assert.Contains(data.Belts, b => b.DefinitionId == "underflow-gate" && b.SorterTurns is > 0);
        Assert.Contains(data.Belts, b => b.DefinitionId == "conveyor-stack" && b.StackWaitTicks is > 0);
        Assert.Contains(data.Belts, b => b.DefinitionId == "conveyor-stack" && b.X > 53 && b.Items!.Count > 0);
        Assert.Contains(data.Belts, b => b.DefinitionId == "bridge-long" && b.Items!.Count > 0);
        Assert.Contains(data.Belts, b => b.DefinitionId == "bridge-long" && b.PowerSatisfactionBp is > 0);
        Assert.Equal(2, data.SolarPanels!.Count);
        var generator = Assert.Single(data.Generators, g => g.DefinitionId == "combustion-generator");
        Assert.Contains(generator.Fuels!, f => f.ItemId == "coal" && f.Count > 0);
        Assert.Contains(generator.Fuels!, f => f.ItemId == "graphite" && f.Count > 0);
        Assert.Equal(2, data.Batteries!.Count);
        Assert.All(data.Batteries, b => Assert.True(b.Charge > 0));

        FactorySaveStore.Save("v22-piano-2", new SimHost(factory));
        Assert.True(FactorySaveStore.TryLoad(content, "v22-piano-2", out var host, out _));
        var loaded = (Factory)host!.State;
        Assert.Equal(Checksum(factory), Checksum(loaded));

        PlanTwoScenario.RunTo(factory, 1408);
        PlanTwoScenario.RunTo(loaded, 1408);
        Assert.Equal(Checksum(factory), Checksum(loaded));
    }

    private static ulong Checksum(Factory factory) => Snapshot.Checksum(Snapshot.Write(factory.State));
}
