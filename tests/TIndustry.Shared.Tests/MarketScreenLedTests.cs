using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The stock-exchange screen's watched item and last "sell all" outcome (spec 2a «Schermo di borsa»: «Oggetto seguito»,
/// «Esito del Vendi tutto», «LED»), with the cases of the «Esempi di vendita» sheet: yellow from 10 000 pieces (won or
/// lost) or with the jackpot, else blue when the price fell at most 10% of B without a crash, else purple.
/// </summary>
public sealed class MarketScreenLedTests
{
    private const int Size = 40;
    private const int Player = 0;
    private const string Plate = "iron-plate";
    private const string Wire = "copper-wire";

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly MarketRules Rules = Content.MarketRules!;

    /// <summary>The shard's top-left cell (cells 20..22 on both axes).</summary>
    private static readonly GridPosition Origin = new(20, 20);

    /// <summary>Left of the shard: (18..19, 20..21), touching it.</summary>
    private static readonly GridPosition Left = new(18, 20);

    private static readonly TerrainTile Grass = new(FloorKind.Grass, WallKind.None, DepositKind.None);

    /// <summary>40×40 grass, shard at (20, 20), instant build, with an active screen on the left when asked.</summary>
    private static (Factory Factory, SimHost Host) Start(bool screen = true)
    {
        var factory = NewGame.Sandbox(
            Content, Origin, mapWidth: Size, mapHeight: Size, rules: new GameRules(InstantBuild: true));
        var tiles = new TerrainTile[Size, Size];
        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                tiles[x, y] = Grass;
            }
        }

        factory.Terrain = TerrainMap.FromTiles(tiles);
        var host = new SimHost(factory);
        if (screen)
        {
            Assert.True(factory.PlaceInstant(ScreenSpec(Left), charge: false));
            Assert.True(factory.MarketScreen!.Active);
        }

        return (factory, host);
    }

    private static BuildSpec ScreenSpec(GridPosition origin) =>
        new(MarketScreen.BuildingId, BuildingKind.MarketScreen, origin, Direction.East);

    /// <summary>Puts <paramref name="itemId"/> at <paramref name="priceMilli"/> (a spike hold when at or over the threshold) with <paramref name="stock"/> pieces.</summary>
    private static MarketItemState Prepare(Factory f, string itemId, int priceMilli, int stock)
    {
        var item = f.State.Exchange.Find(itemId)!;
        if (priceMilli >= Rules.SpikeThresholdMilli)
        {
            item.Phase = MarketPhase.SpikeHold;
            item.PhaseStartTick = f.State.CurrentTick;
            item.SpikeTopMilli = priceMilli;
        }

        item.PriceMilli = priceMilli;
        item.SaleDropMicro = 0;
        Array.Clear(item.WindowPieces);
        f.State.Exchange.SellAllBlockedUntilTick = 0;
        f.Wallet.TryRemoveMaterial(itemId, f.Wallet.MaterialCount(itemId));
        f.Wallet.AddMaterial(itemId, stock);
        return item;
    }

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

    /// <summary>A "sell all" of <paramref name="pieces"/> plates at <paramref name="priceMilli"/>; the roll answers <paramref name="win"/>.</summary>
    private static (Factory Factory, SaleResolved Sale, long Tick) SellAllPlates(int priceMilli, int pieces, bool win = false)
    {
        var (f, host) = Start();
        Prepare(f, Plate, priceMilli, pieces);
        f.State.JackpotRollForTests = _ => win;
        var sales = new List<SaleResolved>();
        host.EventRaised += e =>
        {
            if (e is SaleResolved sale)
            {
                sales.Add(sale);
            }
        };
        var tick = f.State.CurrentTick;

        Assert.Null(Submit(host, new SellAll(Player, Plate)));

        return (f, Assert.Single(sales), tick);
    }

    [Fact]
    public void Content_HasTheLedRules()
    {
        Assert.Equal(100, Rules.LedMildDropMilli);
        Assert.Equal(10_000, Rules.LedYellowPieces);
        Assert.Equal(360, MarketScreen.ResultTicks);
    }

    [Theory]
    [InlineData(200, false, false, 80, ScreenLed.Blue)]
    [InlineData(250, false, false, 100, ScreenLed.Blue)]
    [InlineData(255, false, false, 102, ScreenLed.Purple)]
    [InlineData(2000, false, false, 800, ScreenLed.Purple)]
    [InlineData(100, true, false, 0, ScreenLed.Purple)]
    [InlineData(5000, true, false, 1900, ScreenLed.Purple)]
    [InlineData(9999, true, false, 1900, ScreenLed.Purple)]
    [InlineData(10_000, true, false, 1900, ScreenLed.Yellow)]
    [InlineData(10_000, true, true, 1900, ScreenLed.Yellow)]
    [InlineData(2000, true, true, 1900, ScreenLed.Yellow)]
    [InlineData(400, false, true, 0, ScreenLed.Yellow)]
    public void LedFor_FollowsTheSheet(int pieces, bool crash, bool jackpot, int drop, ScreenLed expected)
    {
        Assert.Equal(expected, MarketSales.LedFor(Rules, pieces, crash, jackpot, drop));
    }

    [Fact]
    public void SmallSellAllInASpike_IsBlue()
    {
        // Sheet case 3: 200 pieces at 2.9 × B, the price falls 0.08 × B.
        var (f, sale, tick) = SellAllPlates(2900, 200);

        var result = f.MarketScreen!.LastResult;
        Assert.NotNull(result);
        Assert.Equal(new ScreenSaleResult(Plate, sale.Income, ScreenLed.Blue, false, tick), result);
        Assert.Same(result, ((IGameView)f).MarketScreen!.LastResult);
    }

    [Fact]
    public void SellAllFallingOverTenPercent_IsPurple()
    {
        // Sheet case 4: 2 000 pieces at 2.9 × B, jackpot missed, no crash, the price falls 0.8 × B.
        var (f, sale, _) = SellAllPlates(2900, 2000);

        Assert.True(sale.Attempt);
        Assert.False(sale.Crash);
        Assert.Equal(ScreenLed.Purple, f.MarketScreen!.LastResult!.Led);
        Assert.False(f.MarketScreen.LastResult.Jackpot);
    }

    [Fact]
    public void SellAllThatCrashesUnderTenThousand_IsPurple()
    {
        var (f, sale, _) = SellAllPlates(2900, 5000);

        Assert.True(sale.Crash);
        Assert.Equal(ScreenLed.Purple, f.MarketScreen!.LastResult!.Led);
    }

    [Fact]
    public void SellAllThatCrashes_IsPurple_EvenWithASmallDrop()
    {
        // 4 900 plates sold first bring the price down to B; the "sell all" of the last 100 crosses the crash window
        // without moving the price.
        var (f, host) = Start();
        Prepare(f, Plate, 1300, 5000);
        Assert.Null(Submit(host, new Sell(Player, Plate, 4900)));
        Assert.Null(f.MarketScreen!.LastResult);

        Assert.Null(Submit(host, new SellAll(Player, Plate)));

        Assert.Equal(MarketPhase.Crash, f.State.Exchange.Find(Plate)!.Phase);
        Assert.Equal(ScreenLed.Purple, f.MarketScreen.LastResult!.Led);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SellAllOfTenThousand_IsYellow_WonOrLost(bool win)
    {
        // Sheet case 9: the 50/50.
        var (f, sale, _) = SellAllPlates(2900, 10_000, win);

        Assert.Equal(win, sale.Jackpot);
        var result = f.MarketScreen!.LastResult!;
        Assert.Equal(ScreenLed.Yellow, result.Led);
        Assert.Equal(win, result.Jackpot);
        Assert.Equal(sale.Income, result.Income);
    }

    [Fact]
    public void Jackpot_IsAlwaysYellow()
    {
        var (f, sale, _) = SellAllPlates(2900, 2000, win: true);

        Assert.True(sale.Jackpot);
        Assert.Equal(ScreenLed.Yellow, f.MarketScreen!.LastResult!.Led);
        Assert.True(f.MarketScreen.LastResult.Jackpot);
    }

    [Fact]
    public void SellAllOutsideASpike_LeavesAResultToo()
    {
        // Sheet case 5: 2 000 pieces at 1.4 × B, no jackpot attempt; the price falls to B (0.4 × B).
        var (f, sale, _) = SellAllPlates(1400, 2000);

        Assert.False(sale.Attempt);
        Assert.Equal(ScreenLed.Purple, f.MarketScreen!.LastResult!.Led);
    }

    [Fact]
    public void SellOfAHundred_LeavesNoResult()
    {
        var (f, host) = Start();
        Prepare(f, Plate, 2900, 500);

        Assert.Null(Submit(host, new Sell(Player, Plate, 100)));

        Assert.Null(f.MarketScreen!.LastResult);
    }

    [Fact]
    public void AutoSell_LeavesNoResult()
    {
        var (f, host) = Start();
        var item = Prepare(f, Plate, 1300, 500);
        item.AutoSell = true;

        host.AdvanceTicks(120);

        Assert.True(f.Wallet.MaterialCount(Plate) < 500);
        Assert.Null(f.MarketScreen!.LastResult);
    }

    [Fact]
    public void SellAllWithAnInactiveScreen_LeavesNoResult()
    {
        var (f, host) = Start();
        f.State.SetCore(new GridPosition(30, 30), 0);
        Assert.False(f.MarketScreen!.Active);
        Prepare(f, Plate, 2900, 200);

        Assert.Null(Submit(host, new SellAll(Player, Plate)));

        Assert.Null(f.MarketScreen.LastResult);
    }

    [Fact]
    public void TwoSellAllsInOneTick_KeepTheSecond()
    {
        var (f, host) = Start();
        Prepare(f, Plate, 1300, 200);
        Prepare(f, Wire, 1300, 100);
        var tick = f.State.CurrentTick;

        host.Submit(new SellAll(Player, Plate));
        host.Submit(new SellAll(Player, Wire));
        host.AdvanceTicks(1);

        var result = f.MarketScreen!.LastResult!;
        Assert.Equal(Wire, result.ItemId);
        Assert.Equal(tick, result.Tick);
        Assert.Equal(0, f.Wallet.MaterialCount(Plate));
    }

    [Fact]
    public void SetBlockFilter_OnTheScreen_SetsAndClearsTheWatchedItem()
    {
        var (f, host) = Start();
        var cell = new GridPosition(Left.X + 1, Left.Y + 1);

        Assert.Null(Submit(host, new SetBlockFilter(Player, cell, Wire)));
        Assert.Equal(Wire, f.MarketScreen!.WatchedItemId);

        Assert.Null(Submit(host, new SetBlockFilter(Player, Left, null)));
        Assert.Null(f.MarketScreen.WatchedItemId);
    }

    [Fact]
    public void SetBlockFilter_OnTheScreen_RefusesAnUnknownItem()
    {
        var (f, host) = Start();
        Assert.Null(Submit(host, new SetBlockFilter(Player, Left, Plate)));

        Assert.Equal(RefusalCode.InvalidCommand, Submit(host, new SetBlockFilter(Player, Left, "unobtainium"))?.Code);

        Assert.Equal(Plate, f.MarketScreen!.WatchedItemId);
    }
}
