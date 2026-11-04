using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The stock-exchange screen (spec 2a «Schermo di borsa»): a 2×2 block, one only (finished or being built), placed with a
/// cell touching the Core edge to edge. It is active when finished and touching the Core; only then
/// <see cref="GameState.HasMarketScreen"/> is true and a "sell all" in a spike can be a jackpot attempt. A Core that grows
/// over it takes it apart with the normal refund.
/// </summary>
public sealed class MarketScreenTests
{
    private const int Size = 40;
    private const int Player = 0;
    private const string Plate = "iron-plate";

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    /// <summary>The shard's top-left cell (cells 20..22 on both axes).</summary>
    private static readonly GridPosition Origin = new(20, 20);

    /// <summary>Right of the shard: (23..24, 20..21), touching (22, 20) and (22, 21); the foundation's new column covers it.</summary>
    private static readonly GridPosition Right = new(23, 20);

    /// <summary>Left of the shard: (18..19, 20..21), touching (20, 20) and (20, 21); the foundation leaves it touching.</summary>
    private static readonly GridPosition Left = new(18, 20);

    private static readonly TerrainTile Grass = new(FloorKind.Grass, WallKind.None, DepositKind.None);

    /// <summary>40×40 grass, shard at (20, 20), the screen researched and its build cost (×3) in the Core.</summary>
    private static Factory NewFactory(bool instant = true)
    {
        var factory = NewGame.Sandbox(
            Content, Origin, mapWidth: Size, mapHeight: Size, rules: new GameRules(InstantBuild: instant));
        var tiles = new TerrainTile[Size, Size];
        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                tiles[x, y] = Grass;
            }
        }

        factory.Terrain = TerrainMap.FromTiles(tiles);
        factory.Research.ForceUnlock(MarketScreen.BuildingId);
        foreach (var entry in factory.CostOf(Spec(Right)))
        {
            if (entry.ItemId is { } itemId)
            {
                factory.Wallet.AddMaterial(itemId, entry.Amount * 3);
            }
        }

        return factory;
    }

    private static BuildSpec Spec(GridPosition origin) =>
        new(MarketScreen.BuildingId, BuildingKind.MarketScreen, origin, Direction.East);

    private static PlaceBuilding Place(GridPosition origin) => new(Player, BuildingKind.MarketScreen, origin, Direction.East);

    /// <summary>Submits <paramref name="command"/>, runs one tick and returns the refusal (null when applied).</summary>
    private static Refusal? Submit(SimHost host, SimCommand command)
    {
        Refusal? reason = null;
        void OnRejected(SimCommand c, Refusal r) => reason = r;
        host.CommandRejected += OnRejected;
        host.Submit(command);
        host.AdvanceTicks(1);
        host.CommandRejected -= OnRejected;
        return reason;
    }

    private static void RunUntil(SimHost host, Func<bool> done, int maxTicks = 20_000)
    {
        for (var tick = 0; tick < maxTicks; tick++)
        {
            if (done())
            {
                return;
            }

            host.AdvanceTicks(1);
        }

        throw new InvalidOperationException("condizione mai raggiunta");
    }

    private static bool HasScreenSite(Factory f) =>
        f.Sites.Any(s => s.Spec.Kind == BuildingKind.MarketScreen && !s.Deconstructing);

    /// <summary>Researches the foundation and puts its build cost in the Core.</summary>
    private static void PrepareFoundation(Factory f)
    {
        var definition = Content.CoreTiers[1];
        f.Research.ForceUnlock(definition.Id);
        foreach (var entry in definition.BuildCost)
        {
            f.Wallet.AddMaterial(entry.ItemId, entry.Amount);
        }
    }

    [Fact]
    public void Content_HasTheScreenAfterTheAssembler()
    {
        var structure = Content.FindStructure(MarketScreen.BuildingId);
        Assert.NotNull(structure);
        Assert.Equal(["assembler"], structure.Prerequisites);
        Assert.NotNull(structure.Unlock);
        Assert.Equal(3000, structure.Unlock.Money);
        Assert.Equal(
            [new ResourceAmount("iron-plate", 80), new ResourceAmount("copper-wire", 40)],
            structure.Unlock.Materials);
        var building = Content.FindBuilding(MarketScreen.BuildingId);
        Assert.NotNull(building);
        Assert.Equal(0, building.MoneyCost);
        Assert.Equal(
            [new ResourceAmount("iron-plate", 60), new ResourceAmount("copper-wire", 25), new ResourceAmount("lead-plate", 10)],
            building.BuildCost);
        Assert.Equal(BuildingKind.MarketScreen, BuildSpec.KindFor(MarketScreen.BuildingId));
    }

    [Fact]
    public void Placed_TouchingTheCore_BecomesASite_ThenAnActiveScreen()
    {
        var f = NewFactory(instant: false);
        var host = new SimHost(f);

        Assert.Null(Submit(host, Place(Right)));
        RunUntil(host, () => HasScreenSite(f));
        Assert.Null(f.MarketScreen);
        Assert.False(f.State.HasMarketScreen);

        RunUntil(host, () => f.MarketScreen is not null);
        Assert.False(HasScreenSite(f));
        var screen = f.MarketScreen!;
        Assert.Equal(Right, screen.Position);
        Assert.True(screen.Active);
        Assert.Null(screen.WatchedItemId);
        Assert.True(f.State.HasMarketScreen);
        Assert.Same(screen, ((IGameView)f).MarketScreen);
        Assert.True(f.TryGetSpecAt(new GridPosition(24, 21), out var spec, out var id));
        Assert.Equal(Spec(Right), spec);
        Assert.Equal(screen.Id, id);
    }

    [Theory]
    [InlineData(26, 20)] // a cell away on the right
    [InlineData(23, 23)] // only a corner touches the shard's corner (22, 22)
    [InlineData(10, 10)]
    public void Placed_AwayFromTheCore_IsRefused(int x, int y)
    {
        foreach (var instant in new[] { true, false })
        {
            var f = NewFactory(instant);
            var host = new SimHost(f);

            Assert.Equal(RefusalCode.MarketScreenNotTouchingCore, Submit(host, Place(new GridPosition(x, y)))?.Code);
            Assert.Null(f.MarketScreen);
            Assert.False(HasScreenSite(f));
            Assert.Empty(f.Builders.SelectMany(b => b.Plans));
        }
    }

    [Fact]
    public void SecondScreen_IsRefused_WithTheFirstPlanned_Building_OrFinished()
    {
        var f = NewFactory(instant: false);
        var host = new SimHost(f);
        Assert.Null(Submit(host, Place(Right)));

        // The first is still a plan (or its site is starting).
        Assert.Equal(RefusalCode.MarketScreenAlreadyPresent, Submit(host, Place(Left))?.Code);

        RunUntil(host, () => HasScreenSite(f));
        Assert.Equal(RefusalCode.MarketScreenAlreadyPresent, Submit(host, Place(Left))?.Code);

        RunUntil(host, () => f.MarketScreen is not null);
        Assert.Equal(RefusalCode.MarketScreenAlreadyPresent, Submit(host, Place(Left))?.Code);
        Assert.False(HasScreenSite(f));

        var instant = NewFactory();
        var instantHost = new SimHost(instant);
        Assert.Null(Submit(instantHost, Place(Right)));
        Assert.Equal(RefusalCode.MarketScreenAlreadyPresent, Submit(instantHost, Place(Left))?.Code);
    }

    [Fact]
    public void Removed_TurnsTheJackpotOff()
    {
        var f = NewFactory();
        var host = new SimHost(f);
        Assert.Null(Submit(host, Place(Right)));
        Assert.True(f.State.HasMarketScreen);
        var plates = f.Wallet.MaterialCount(Plate);

        Assert.Null(Submit(host, new Remove(Player, new GridPosition(24, 21))));

        Assert.Null(f.MarketScreen);
        Assert.False(f.State.HasMarketScreen);
        Assert.Equal(plates + 60, f.Wallet.MaterialCount(Plate));
        // A new one can be placed again.
        Assert.Null(Submit(host, Place(Left)));
        Assert.True(f.State.HasMarketScreen);
    }

    [Fact]
    public void Deconstructed_WithTheDrone_TurnsTheJackpotOff()
    {
        var f = NewFactory(instant: false);
        var host = new SimHost(f);
        Assert.Null(Submit(host, Place(Right)));
        RunUntil(host, () => f.MarketScreen is not null);
        Assert.True(f.State.HasMarketScreen);

        Assert.Null(Submit(host, new Remove(Player, Right)));
        RunUntil(host, () => f.MarketScreen is null);

        Assert.False(f.State.HasMarketScreen);
    }

    [Fact]
    public void CoreGrowingOverTheScreen_TakesItApartWithRefund()
    {
        var f = NewFactory();
        var host = new SimHost(f);
        Assert.Null(Submit(host, Place(Right)));
        PrepareFoundation(f);
        var before = f.CostOf(Spec(Right)).Where(e => e.ItemId is not null)
            .ToDictionary(e => e.ItemId!, e => f.Wallet.MaterialCount(e.ItemId!));
        Assert.Null(f.CoreUpgradeRefusal());

        Assert.Null(Submit(host, new PlaceBuilding(Player, BuildingKind.CoreFoundation, Origin, Direction.East)));

        Assert.Equal(1, f.CoreTier);
        Assert.Null(f.MarketScreen);
        Assert.False(f.State.HasMarketScreen);
        foreach (var entry in f.CostOf(Spec(Right)).Where(e => e.ItemId is not null))
        {
            var paid = Content.CoreTiers[1].BuildCost.Where(c => c.ItemId == entry.ItemId).Sum(c => c.Amount);
            Assert.Equal(before[entry.ItemId!] - paid + entry.Amount, f.Wallet.MaterialCount(entry.ItemId!));
        }
    }

    [Fact]
    public void CoreSiteGrowingOverTheScreen_TakesItApartWhenTheSiteStarts()
    {
        var f = NewFactory(instant: false);
        var host = new SimHost(f);
        Assert.Null(Submit(host, Place(Right)));
        RunUntil(host, () => f.MarketScreen is not null);
        // Researched but not paid yet: the site starts and waits, so the plates only move by the screen's refund.
        f.Research.ForceUnlock(Content.CoreTiers[1].Id);
        var plates = f.Wallet.MaterialCount(Plate);

        Assert.Null(Submit(host, new PlaceBuilding(Player, BuildingKind.CoreFoundation, Origin, Direction.East)));
        RunUntil(host, () => f.Sites.Any(s => s.Spec.Kind == BuildingKind.CoreFoundation));

        Assert.Null(f.MarketScreen);
        Assert.False(f.State.HasMarketScreen);
        Assert.False(f.TryGetSpecAt(Right, out _, out _));
        var site = f.Sites.Single(s => s.Spec.Kind == BuildingKind.CoreFoundation);
        var spentPlates = site.Cost.Select((c, i) => c.ItemId == Plate ? site.Spent[i] : 0).Sum();
        Assert.Equal(plates + 60 - spentPlates, f.Wallet.MaterialCount(Plate));

        foreach (var entry in Content.CoreTiers[1].BuildCost)
        {
            f.Wallet.AddMaterial(entry.ItemId, entry.Amount);
        }

        RunUntil(host, () => f.CoreTier == 1);
        Assert.False(f.State.HasMarketScreen);
    }

    [Fact]
    public void CoreGrowingBesideTheScreen_KeepsItActive()
    {
        var f = NewFactory();
        var host = new SimHost(f);
        Assert.Null(Submit(host, Place(Left)));
        PrepareFoundation(f);

        Assert.Null(Submit(host, new PlaceBuilding(Player, BuildingKind.CoreFoundation, Origin, Direction.East)));

        Assert.Equal(1, f.CoreTier);
        Assert.True(f.MarketScreen!.Active);
        Assert.True(f.State.HasMarketScreen);
    }

    [Fact]
    public void ScreenLeftDetached_IsInactive_UntilTheCoreTouchesItAgain()
    {
        // Growth keeps every touching screen touching or covers it; a Core put elsewhere (the rule the spec states for a
        // detached screen) turns it off, and back on when the Core touches it again.
        var f = NewFactory();
        var host = new SimHost(f);
        Assert.Null(Submit(host, Place(Left)));

        f.State.SetCore(new GridPosition(30, 30), 0);

        Assert.NotNull(f.MarketScreen);
        Assert.False(f.MarketScreen!.Active);
        Assert.False(f.State.HasMarketScreen);

        f.State.SetCore(Origin, 0);

        Assert.True(f.MarketScreen.Active);
        Assert.True(f.State.HasMarketScreen);
    }

    [Fact]
    public void Snapshot_KeepsTheScreen_AndRecomputesWhetherItIsActive()
    {
        // The generated terrain (a save regenerates it), the screen placed directly.
        var f = NewGame.Sandbox(Content, Origin, mapWidth: Size, mapHeight: Size, rules: new GameRules(InstantBuild: true));
        Assert.True(f.PlaceInstant(Spec(Right), charge: false));
        var bytes = Snapshot.Write(f.State);

        var restored = new Factory(Snapshot.Read(Content, bytes));

        Assert.Equal(f.MarketScreen!.Id, restored.MarketScreen!.Id);
        Assert.Equal(Right, restored.MarketScreen.Position);
        Assert.True(restored.MarketScreen.Active);
        Assert.True(restored.State.HasMarketScreen);
        Assert.Equal(Snapshot.Checksum(bytes), Snapshot.Checksum(Snapshot.Write(restored.State)));
    }

    [Fact]
    public void SellAllInASpike_IsAJackpotAttemptOnlyWithAnActiveScreen()
    {
        foreach (var withScreen in new[] { true, false })
        {
            var f = NewFactory();
            var host = new SimHost(f);
            if (withScreen)
            {
                Assert.Null(Submit(host, Place(Right)));
            }

            var item = f.State.Exchange.Find(Plate)!;
            item.Phase = MarketPhase.SpikeHold;
            item.PhaseStartTick = f.State.CurrentTick;
            item.SpikeTopMilli = 2900;
            item.PriceMilli = 2900;
            Array.Clear(item.WindowPieces);
            f.State.Exchange.SellAllBlockedUntilTick = 0;
            f.Wallet.TryRemoveMaterial(Plate, f.Wallet.MaterialCount(Plate));
            f.Wallet.AddMaterial(Plate, 400);
            f.State.JackpotRollForTests = _ => false;
            var sales = new List<SaleResolved>();
            host.EventRaised += e =>
            {
                if (e is SaleResolved sale)
                {
                    sales.Add(sale);
                }
            };

            Assert.Null(Submit(host, new SellAll(Player, Plate)));

            var resolved = Assert.Single(sales);
            Assert.Equal(400, resolved.Pieces);
            Assert.Equal(withScreen, resolved.Attempt);
        }
    }
}
