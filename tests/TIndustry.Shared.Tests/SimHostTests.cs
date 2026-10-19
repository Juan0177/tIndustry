using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Command-driven host: commands queue for the next tick, are validated when applied
/// (in submit order) and report the outcome as events with today's Italian toast texts.
/// </summary>
public sealed class SimHostTests
{
    private static readonly string ContentPath =
        Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    private static FactoryContent LoadContent() => FactoryContent.Load(ContentPath);

    private static string Serialize(Factory factory) =>
        JsonSerializer.Serialize(factory.Capture(), FactorySaveStore.JsonOptions);

    /// <summary>Small sandbox: $180, 48 iron plates, 10 copper wire; only belts and miners unlocked.</summary>
    // These tests pin today's instant placement; real construction is covered by BuilderTests.
    private static Factory NewSandbox(FactoryContent content) =>
        NewGame.Sandbox(
            content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, rules: new GameRules(InstantBuild: true));

    [Fact]
    public void SubmittedCommandAppliesOnNextTick()
    {
        var factory = NewSandbox(LoadContent());
        var host = new SimHost(factory);
        var applied = new List<SimCommand>();
        host.CommandApplied += applied.Add;
        var cmd = new PlaceBelt(1, new GridPosition(10, 10), Direction.East, "conveyor-basic");

        host.Submit(cmd);

        Assert.False(host.State.Belts.ContainsKey(new GridPosition(10, 10)));
        Assert.Empty(applied);

        Assert.Equal(1, host.Advance(SimulationClock.TickSeconds));

        Assert.True(host.State.Belts.ContainsKey(new GridPosition(10, 10)));
        var single = Assert.Single(applied);
        Assert.Equal(cmd with { Tick = 1 }, single);
        Assert.Equal(1, host.State.CurrentTick);
    }

    [Fact]
    public void ManyCommandsInOneFrameApplyInOrder()
    {
        var factory = NewSandbox(LoadContent());
        var host = new SimHost(factory);
        var applied = new List<SimCommand>();
        var rejected = new List<(SimCommand, RefusalCode)>();
        host.CommandApplied += applied.Add;
        host.CommandRejected += (c, r) => rejected.Add((c, r.Code));
        var submitted = new List<SimCommand>();
        for (var x = 0; x < 30; x++)
        {
            var cmd = new PlaceBelt(1, new GridPosition(5 + x, 20), Direction.East, "conveyor-basic");
            submitted.Add(cmd);
            host.Submit(cmd);
        }

        Assert.Equal(1, host.Advance(SimulationClock.TickSeconds));

        Assert.Empty(rejected);
        for (var x = 0; x < 30; x++)
        {
            Assert.True(host.State.Belts.ContainsKey(new GridPosition(5 + x, 20)));
        }

        Assert.Equal(submitted.Select(c => c with { Tick = 1 }), applied);
    }

    [Fact]
    public void RejectedCommandLeavesStateUnchanged()
    {
        var factory = NewSandbox(LoadContent());
        var host = new SimHost(factory);
        var before = Serialize(factory);
        string? atRejection = null;
        Refusal? reason = null;
        host.CommandRejected += (_, r) =>
        {
            reason = r;
            // Raised while the command is applied, before the tick's Step(): nothing may have changed.
            atRejection = Serialize(factory);
        };
        var applied = 0;
        host.CommandApplied += _ => applied++;

        host.Submit(new PlaceBuilding(1, BuildingKind.Smelter, new GridPosition(20, 20), Direction.East));
        host.Advance(SimulationClock.TickSeconds);

        Assert.Equal(0, applied);
        Assert.Equal(new Refusal(RefusalCode.Locked, "smelter"), reason);
        Assert.Equal(before, atRejection);
        Assert.Empty(host.State.Smelters);
    }

    [Fact]
    public void SecondCommandRejectedWhenFirstSpentTheMoney()
    {
        var content = LoadContent();
        // Junction costs $200 + 10 plates, generator $280 + 12 plates: enough for one only.
        var wallet = new EconomyWallet(
            300,
            new Dictionary<string, int>(StringComparer.Ordinal) { ["iron-plate"] = 30 });
        var factory = new Factory(
            content,
            new BeltGrid(),
            CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), 2),
            wallet);
        var host = new SimHost(factory);
        var applied = new List<SimCommand>();
        var rejected = new List<(SimCommand Cmd, RefusalCode Reason)>();
        host.CommandApplied += applied.Add;
        host.CommandRejected += (c, r) => rejected.Add((c, r.Code));

        host.Submit(new UnlockStructure(1, "junction"));
        host.Submit(new UnlockStructure(2, "generator"));
        host.Advance(SimulationClock.TickSeconds);

        var ok = Assert.Single(applied);
        Assert.Equal("junction", ((UnlockStructure)ok).StructureId);
        var ko = Assert.Single(rejected);
        Assert.Equal("generator", ((UnlockStructure)ko.Cmd).StructureId);
        Assert.Equal(RefusalCode.UnlockUnaffordable, ko.Reason);
        Assert.True(host.State.Research.IsUnlocked("junction"));
        Assert.False(host.State.Research.IsUnlocked("generator"));
        Assert.Equal(100, host.State.Wallet.Money);
    }

    [Fact]
    public void SellAndAutoSellAndUpgradeGoThroughCommands()
    {
        var content = LoadContent();
        Factory Make()
        {
            var f = NewSandbox(content);
            f.EnsureDemoBuildStock(ironPlates: 500, copperWire: 200, copperOre: 100, money: 100_000);
            foreach (var entry in content.CoreUpgrade.BuildCost)
            {
                f.Wallet.AddMaterial(entry.ItemId, entry.Amount);
            }

            return f;
        }

        var direct = Make();
        var commanded = Make();
        var host = new SimHost(commanded);
        var rejected = new List<RefusalCode>();
        host.CommandRejected += (_, r) => rejected.Add(r.Code);

        // Today's direct calls (applied before the tick), then one tick.
        Assert.True(direct.TrySellFromWallet("iron-plate", 7));
        direct.AutoSellAtCore = true;
        Assert.True(direct.TryUpgradeCore());
        direct.Step();

        host.Submit(new Sell(1, "iron-plate", 7));
        host.Submit(new SetAutoSell(1, true));
        host.Submit(new UpgradeCore(1));
        host.Advance(SimulationClock.TickSeconds);

        Assert.Empty(rejected);
        Assert.Equal(Serialize(direct), Serialize(commanded));
        Assert.True(host.State.AutoSellAtCore);
        Assert.Equal(1, host.State.CoreUpgradeLevel);

        // Selling beyond the stock is rejected with today's Mercato text; state unchanged.
        host.Submit(new Sell(1, "iron-plate", 1_000_000));
        host.Advance(SimulationClock.TickSeconds);
        Assert.Equal([RefusalCode.SellFailed], rejected);
    }

    /// <summary>Applies one command through the host and returns (reason, capture before, capture at rejection).</summary>
    private static (Refusal? Reason, string Before, string? AtRejection, int Applied) RunOne(
        Factory factory, SimCommand command)
    {
        var host = new SimHost(factory);
        var before = Serialize(factory);
        Refusal? reason = null;
        string? atRejection = null;
        var applied = 0;
        host.CommandRejected += (_, r) =>
        {
            reason = r;
            atRejection = Serialize(factory);
        };
        host.CommandApplied += _ => applied++;
        host.Submit(command);
        Assert.Equal(1, host.Advance(SimulationClock.TickSeconds));
        return (reason, before, atRejection, applied);
    }

    [Fact]
    public void PlacementWithoutResourcesIsRejectedWithNeedMessage()
    {
        var content = LoadContent();
        var factory = new Factory(
            content,
            new BeltGrid(),
            CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), 2),
            new EconomyWallet(0));
        factory.State.Rules = new GameRules(InstantBuild: true); // the need message exists only with instant build
        var expected = factory.Shortfall("conveyor-basic");
        Assert.NotNull(expected);
        Assert.Equal(RefusalCode.InsufficientResources, expected.Code);
        Assert.Equal("conveyor-basic", expected.StructureId);
        Assert.NotEmpty(expected.MissingItems!);

        var (reason, before, atRejection, applied) = RunOne(
            factory, new PlaceBelt(1, new GridPosition(2, 2), Direction.East, "conveyor-basic"));

        Assert.Equal(0, applied);
        Assert.Equal(expected, reason);
        Assert.Equal(before, atRejection);
    }

    [Fact]
    public void PlacementOnOccupiedTileIsRejected()
    {
        var factory = NewSandbox(LoadContent());
        // A junction: a belt over a plain belt would turn it in place (BeltOverBeltTests).
        Assert.True(factory.PlaceInstant(
            new BuildSpec("junction", BuildingKind.Junction, new GridPosition(10, 10), Direction.East), charge: false));

        var (reason, before, atRejection, applied) = RunOne(
            factory, new PlaceBelt(1, new GridPosition(10, 10), Direction.North, "conveyor-basic"));

        Assert.Equal(0, applied);
        Assert.Equal(RefusalCode.Occupied, reason?.Code);
        Assert.Equal(before, atRejection);
    }

    /// <summary>The Core rule belongs to the line (its last cell): the simulation builds the direction it is sent.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ABeltBesideTheCoreKeepsItsDirection(bool instantBuild)
    {
        var factory = NewGame.Sandbox(
            LoadContent(), new GridPosition(60, 60), mapWidth: 120, mapHeight: 120,
            rules: new GameRules(InstantBuild: instantBuild));
        if (!instantBuild)
        {
            factory.AddBuilder(1);
        }

        var host = new SimHost(factory);
        var beside = new GridPosition(factory.CoreTiles.Min(t => t.X) - 1, factory.CoreTiles.Min(t => t.Y));

        host.Submit(new PlaceBelt(1, beside, Direction.North, "conveyor-basic"));
        host.AdvanceTicks(1);
        if (!instantBuild)
        {
            Assert.Equal(Direction.North, Assert.Single(factory.PlansOf(1)).Spec.Direction);
        }

        for (var tick = 0; tick < 20_000 && !factory.Belts.Cells.ContainsKey(beside); tick++)
        {
            host.AdvanceTicks(1);
        }

        Assert.Equal(Direction.North, factory.Belts.Cells[beside].Direction);
    }

    [Fact]
    public void PlacementOnCoreIsRejected()
    {
        var factory = NewSandbox(LoadContent());

        var (reason, before, atRejection, applied) = RunOne(
            factory, new PlaceBuilding(1, BuildingKind.Miner, new GridPosition(60, 60), Direction.East));

        Assert.Equal(0, applied);
        Assert.Equal(RefusalCode.OnCore, reason?.Code);
        Assert.Equal(before, atRejection);
    }

    [Fact]
    public void MinerOnOreThenRemoveGoThroughCommands()
    {
        var factory = NewSandbox(LoadContent());
        var terrain = factory.Terrain!;
        GridPosition? ore = null;
        for (var y = 1; y < terrain.Height - Miner.Size && ore is null; y++)
        {
            for (var x = 1; x < terrain.Width - Miner.Size; x++)
            {
                var p = new GridPosition(x, y);
                if (terrain.MajorityDeposit(p, Miner.Size) != DepositKind.None
                    && factory.CanOccupyFootprint(p, Miner.Size))
                {
                    ore = p;
                    break;
                }
            }
        }

        Assert.NotNull(ore);
        var host = new SimHost(factory);
        var applied = new List<SimCommand>();
        host.CommandApplied += applied.Add;

        host.Submit(new PlaceBuilding(1, BuildingKind.Miner, ore.Value, Direction.East));
        host.Advance(SimulationClock.TickSeconds);
        var miner = Assert.Single(host.State.Miners);
        Assert.True(miner.Id > 0);
        Assert.Equal(ore.Value, miner.Position);

        host.Submit(new Remove(1, ore.Value));
        host.Advance(SimulationClock.TickSeconds);
        Assert.Empty(host.State.Miners);
        Assert.Equal(2, applied.Count);

        var (reason, before, atRejection, appliedEmpty) = RunOne(factory, new Remove(1, ore.Value));
        Assert.Equal(0, appliedEmpty);
        Assert.Equal(RefusalCode.NothingToRemove, reason?.Code);
        Assert.Equal(before, atRejection);
    }

    [Fact]
    public void SetSorterFilterChangesTheSorterOnly()
    {
        var factory = NewSandbox(LoadContent());
        factory.UnlockAllForDemo();
        var sorter = new GridPosition(20, 20);
        Assert.True(factory.TryPlaceSorter(sorter, Direction.East));

        var (okReason, _, _, okApplied) = RunOne(factory, new SetSorterFilter(1, sorter, "coal"));
        Assert.Null(okReason);
        Assert.Equal(1, okApplied);
        Assert.True(factory.Belts.TryGet(sorter, out var cell));
        Assert.Equal("coal", cell.FilterItemId);

        Assert.True(factory.TryPlaceBelt(new GridPosition(25, 25), Direction.East));
        foreach (var bad in new SimCommand[]
                 {
                     new SetSorterFilter(1, new GridPosition(25, 25), "coal"), // plain belt
                     new SetSorterFilter(1, new GridPosition(40, 40), "coal"), // empty tile
                     new SetSorterFilter(1, sorter, "gold-ingot")              // not a filter id
                 })
        {
            var (reason, before, atRejection, applied) = RunOne(factory, bad);
            Assert.Equal(0, applied);
            Assert.Equal(RefusalCode.InvalidCommand, reason?.Code);
            Assert.Equal(before, atRejection);
        }

        Assert.Equal("iron-plate", SorterFilters.Cycle("copper-ore"));
        Assert.Equal(SorterFilters.Ids[0], SorterFilters.Cycle("coal"));
    }

    [Fact]
    public void ThrowingHandlerDoesNotBreakTheTick()
    {
        var factory = NewSandbox(LoadContent());
        var host = new SimHost(factory);
        var seen = new List<SimCommand>();
        host.CommandApplied += cmd =>
        {
            seen.Add(cmd);
            if (seen.Count == 1)
            {
                throw new InvalidOperationException("handler boom");
            }
        };
        var secondSubscriberSaw = 0;
        host.CommandApplied += _ => secondSubscriberSaw++;

        host.Submit(new PlaceBelt(1, new GridPosition(10, 10), Direction.East, "conveyor-basic"));
        host.Submit(new PlaceBelt(1, new GridPosition(11, 10), Direction.East, "conveyor-basic"));

        // Two ticks in one frame: the whole frame runs, then the handler failure surfaces.
        var error = Assert.Throws<AggregateException>(() => host.Advance(2 * SimulationClock.TickSeconds));
        Assert.IsType<InvalidOperationException>(Assert.Single(error.InnerExceptions));
        Assert.Equal(2, host.State.CurrentTick);
        Assert.True(host.State.Belts.ContainsKey(new GridPosition(10, 10)));
        Assert.True(host.State.Belts.ContainsKey(new GridPosition(11, 10)));
        Assert.Equal(2, seen.Count);
        Assert.Equal(2, secondSubscriberSaw);

        // The host keeps working afterwards.
        Assert.Equal(1, host.Advance(SimulationClock.TickSeconds));
        Assert.Equal(3, host.State.CurrentTick);
    }

    [Fact]
    public void HostAdvanceRespectsTickCap()
    {
        var host = new SimHost(NewSandbox(LoadContent()));
        Assert.Equal(SimulationClock.MaxTicksPerFrame, host.Advance(1.0));
        Assert.Equal(5, host.State.CurrentTick);
    }

    [Fact]
    public void PendingShowsQueuedCommandsInSubmitOrderUntilTheTick()
    {
        var host = new SimHost(NewSandbox(LoadContent()));
        Assert.Empty(host.Pending);

        var first = new SetAutoSell(1, true);
        var second = new SetAutoSell(1, false);
        var third = new PlaceBelt(1, new GridPosition(10, 10), Direction.East, "conveyor-basic");
        host.Submit(first);
        host.Submit(second);
        host.Submit(third);

        Assert.Equal(
            [first with { Tick = 1 }, second with { Tick = 1 }, third with { Tick = 1 }],
            host.Pending);

        host.Advance(SimulationClock.TickSeconds);
        Assert.Empty(host.Pending);
    }

    [Fact]
    public void PendingKeepsCommandsScheduledForLaterTicks()
    {
        var host = new SimHost(NewSandbox(LoadContent()));
        var later = new SetAutoSell(1, true) with { Tick = 5 };
        host.Submit(later);

        host.Advance(SimulationClock.TickSeconds);

        Assert.Equal([later], host.Pending);
    }

    [Fact]
    public void EachSaleReportsItsOwnMoneyDelta()
    {
        var content = LoadContent();
        Factory Make()
        {
            var f = NewSandbox(content);
            f.EnsureDemoBuildStock(ironPlates: 500, copperWire: 200, copperOre: 100, money: 1_000);
            return f;
        }

        var direct = Make();
        var expected = new List<int>();
        foreach (var amount in new[] { 7, 11 })
        {
            var before = direct.Wallet.Money;
            Assert.True(direct.TrySellFromWallet("iron-plate", amount));
            expected.Add(direct.Wallet.Money - before);
        }

        var host = new SimHost(Make());
        var seen = new List<int>();
        host.CommandApplied += _ => seen.Add(host.LastCommandMoneyDelta);
        host.Submit(new Sell(1, "iron-plate", 7));
        host.Submit(new Sell(1, "iron-plate", 11));
        host.Advance(SimulationClock.TickSeconds);

        Assert.Equal(expected, seen);
        Assert.NotEqual(expected[0], expected[0] + expected[1]);
    }

    [Fact]
    public void UpgradeCoreAlreadyAtMaxLevelSaysSo()
    {
        var content = LoadContent();
        var factory = NewSandbox(content);
        factory.EnsureDemoBuildStock(ironPlates: 500, copperWire: 200, copperOre: 100, money: 100_000);
        Assert.True(factory.TryUpgradeCore());

        var (reason, before, atRejection, applied) = RunOne(factory, new UpgradeCore(1));

        Assert.Equal(0, applied);
        Assert.Equal(new Refusal(RefusalCode.CoreAlreadyUpgraded), reason);
        Assert.Equal(before, atRejection);
    }

    [Fact]
    public void UpgradeCoreMissingMoneyNamesTheMoney()
    {
        var content = LoadContent();
        var factory = NewSandbox(content);
        factory.EnsureDemoBuildStock(ironPlates: 500, copperWire: 200, copperOre: 100, money: 0);
        factory.Wallet.TrySpend(factory.Wallet.Money - 10, []);

        var (reason, before, atRejection, applied) = RunOne(factory, new UpgradeCore(1));

        Assert.Equal(0, applied);
        Assert.Equal(new Refusal(RefusalCode.InsufficientResources, MissingMoney: content.CoreUpgrade.MoneyCost - 10, MissingItems: []), reason);
        Assert.Equal(before, atRejection);
    }

    [Fact]
    public void UpgradeCoreMissingMaterialsNamesTheMaterials()
    {
        var content = LoadContent();
        var factory = NewSandbox(content);
        factory.EnsureDemoBuildStock(ironPlates: 500, copperWire: 200, copperOre: 100, money: 100_000);
        var entry = content.CoreUpgrade.BuildCost[0];
        Assert.True(factory.Wallet.TryRemoveMaterial(entry.ItemId, factory.Wallet.MaterialCount(entry.ItemId)));

        var (reason, before, atRejection, applied) = RunOne(factory, new UpgradeCore(1));

        Assert.Equal(0, applied);
        Assert.Equal(new Refusal(RefusalCode.InsufficientResources, MissingItems: [entry]), reason);
        Assert.Equal(before, atRejection);
    }

    [Fact]
    public void UpgradeCoreMissingMoneyAndMaterialsNamesBoth()
    {
        var content = LoadContent();
        var factory = NewSandbox(content);
        factory.EnsureDemoBuildStock(ironPlates: 0, copperWire: 0, copperOre: 0, money: 0);
        var entry = content.CoreUpgrade.BuildCost[0];
        factory.Wallet.TrySpend(factory.Wallet.Money, []);
        factory.Wallet.TryRemoveMaterial(entry.ItemId, factory.Wallet.MaterialCount(entry.ItemId));

        var (reason, _, _, _) = RunOne(factory, new UpgradeCore(1));

        Assert.Equal(
            new Refusal(RefusalCode.InsufficientResources, MissingMoney: content.CoreUpgrade.MoneyCost, MissingItems: [entry]),
            reason);
    }

    [Fact]
    public void AdvanceTicksRunsExactlyThatManyTicksWithoutTouchingTheClock()
    {
        var host = new SimHost(NewSandbox(LoadContent()));
        host.Submit(new PlaceBelt(1, new GridPosition(10, 10), Direction.East, "conveyor-basic"));

        Assert.Equal(1, host.AdvanceTicks(1));
        Assert.Equal(1, host.State.CurrentTick);
        Assert.True(host.State.Belts.ContainsKey(new GridPosition(10, 10)));

        // More than the per-frame cap is fine: it is an explicit request, not frame time.
        Assert.Equal(8, host.AdvanceTicks(8));
        Assert.Equal(9, host.State.CurrentTick);
        Assert.Equal(0, host.AdvanceTicks(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => host.AdvanceTicks(-1));

        // The frame clock is independent: a 1/60 s frame is still exactly one tick.
        Assert.Equal(1, host.Advance(SimulationClock.TickSeconds));
    }
}
