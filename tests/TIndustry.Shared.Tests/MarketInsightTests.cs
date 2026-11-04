using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The three Finance modules (spec 2a «Ramo Finanza della Ricerca»): forecast, percent over B and jackpot odds, read
/// through <see cref="IGameView.MarketInsight"/> only with the module researched and an active screen.
/// </summary>
public sealed class MarketInsightTests
{
    private const int Size = 40;
    private const int Player = 0;
    private const string Plate = "iron-plate";
    private const string Wire = "copper-wire";

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly MarketRules Rules = Content.MarketRules!;

    private static readonly GridPosition Origin = new(20, 20);
    private static readonly GridPosition Left = new(18, 20);
    private static readonly TerrainTile Grass = new(FloorKind.Grass, WallKind.None, DepositKind.None);

    private static readonly string[] AllModules =
        [MarketInsight.ForecastNodeId, MarketInsight.PercentNodeId, MarketInsight.OddsNodeId];

    private static (Factory Factory, SimHost Host) Start(bool screen = true, params string[] modules)
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
            Assert.True(factory.PlaceInstant(
                new BuildSpec(MarketScreen.BuildingId, BuildingKind.MarketScreen, Left, Direction.East), charge: false));
            Assert.True(factory.MarketScreen!.Active);
        }

        foreach (var id in modules)
        {
            factory.Research.ForceUnlock(id);
        }

        return (factory, host);
    }

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

    private static MarketInsight Read(Factory f, string itemId = Plate) => ((IGameView)f).MarketInsight(itemId);

    private static void AssertEmpty(MarketInsight insight)
    {
        Assert.Null(insight.SecondsToSpike);
        Assert.Null(insight.SecondsToCrashEnd);
        Assert.Null(insight.SecondsSellAllBlocked);
        Assert.False(insight.SellNow);
        Assert.Null(insight.PercentOverB);
        Assert.Null(insight.JackpotChancePercent);
    }

    [Fact]
    public void Content_HasTheThreeModulesInARow()
    {
        Assert.Equal([MarketScreen.BuildingId], Content.FindStructure(MarketInsight.ForecastNodeId)!.Prerequisites);
        Assert.Equal([MarketInsight.ForecastNodeId], Content.FindStructure(MarketInsight.PercentNodeId)!.Prerequisites);
        Assert.Equal([MarketInsight.PercentNodeId], Content.FindStructure(MarketInsight.OddsNodeId)!.Prerequisites);
        foreach (var id in AllModules)
        {
            var node = Content.FindStructure(id)!;
            Assert.Equal(MarketInsight.ModuleKind, node.Kind);
            Assert.False(node.UnlockedByDefault);
            Assert.Null(Content.FindBuilding(id));
            Assert.Null(Content.FindConveyor(id));
        }

        Assert.Equal(6000, Content.FindStructure(MarketInsight.ForecastNodeId)!.Unlock!.Money);
        Assert.Equal([new ResourceAmount("lead-plate", 60), new ResourceAmount("copper-wire", 30)],
            Content.FindStructure(MarketInsight.ForecastNodeId)!.Unlock!.Materials);
        Assert.Equal(12_000, Content.FindStructure(MarketInsight.PercentNodeId)!.Unlock!.Money);
        Assert.Equal([new ResourceAmount("titanium-plate", 80)], Content.FindStructure(MarketInsight.PercentNodeId)!.Unlock!.Materials);
        Assert.Equal(25_000, Content.FindStructure(MarketInsight.OddsNodeId)!.Unlock!.Money);
        Assert.Equal([new ResourceAmount("silicon", 120), new ResourceAmount("graphite", 80)],
            Content.FindStructure(MarketInsight.OddsNodeId)!.Unlock!.Materials);
    }

    [Fact]
    public void ModuleNodes_CanBeResearched_ButNotPlaced()
    {
        var (f, host) = Start();
        f.Research.ForceUnlock(MarketScreen.BuildingId);
        f.Wallet.AddMoney(100_000);
        f.Wallet.AddMaterial("lead-plate", 60);
        f.Wallet.AddMaterial("copper-wire", 30);
        var rejected = new List<Refusal>();
        host.CommandRejected += (_, r) => rejected.Add(r);

        host.Submit(new UnlockStructure(Player, MarketInsight.ForecastNodeId));
        host.AdvanceTicks(1);

        Assert.Empty(rejected);
        Assert.True(f.IsStructureUnlocked(MarketInsight.ForecastNodeId));
        Assert.False(f.CanPlace(new BuildSpec(MarketInsight.ForecastNodeId, null, new GridPosition(5, 5), Direction.East), out _));
    }

    [Fact]
    public void WithoutResearch_EverythingIsNull()
    {
        var (f, _) = Start();
        Prepare(f, Plate, 2900, 5000);

        AssertEmpty(Read(f));
    }

    [Fact]
    public void WithResearch_WithoutAnActiveScreen_EverythingIsNull()
    {
        var (f, _) = Start(screen: false, AllModules);
        Prepare(f, Plate, 2900, 5000);
        AssertEmpty(Read(f));

        // A screen left detached by a moved Core is not active either.
        var (g, _) = Start(screen: true, AllModules);
        g.State.SetCore(new GridPosition(30, 30), 0);
        Assert.False(g.MarketScreen!.Active);
        Prepare(g, Plate, 2900, 5000);
        AssertEmpty(Read(g));
    }

    [Fact]
    public void UnknownItem_IsEmpty()
    {
        var (f, _) = Start(screen: true, AllModules);
        AssertEmpty(Read(f, "unobtainium"));
    }

    [Theory]
    [InlineData(60, 1)]
    [InlineData(61, 2)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    [InlineData(600 * 60, 600)]
    public void Forecast_SecondsToSpike_RoundUp(int ticksLeft, int seconds)
    {
        var (f, _) = Start(screen: true, MarketInsight.ForecastNodeId);
        var item = Prepare(f, Plate, 1300, 100);
        item.Phase = MarketPhase.Normal;
        item.NextSpikeTick = f.CurrentTick + ticksLeft;

        var insight = Read(f);

        Assert.Equal(seconds, insight.SecondsToSpike);
        Assert.False(insight.SellNow);
        Assert.Null(insight.SecondsToCrashEnd);
        Assert.Null(insight.SecondsSellAllBlocked);
        Assert.Null(insight.PercentOverB);
        Assert.Null(insight.JackpotChancePercent);
    }

    [Theory]
    [InlineData(2800, true)]
    [InlineData(2950, true)]
    [InlineData(2799, false)]
    public void Forecast_SellNow_OnlyInTheSpike(int priceMilli, bool sellNow)
    {
        var (f, _) = Start(screen: true, MarketInsight.ForecastNodeId);
        var item = Prepare(f, Plate, priceMilli, 100);
        item.Phase = MarketPhase.SpikeFall;

        Assert.Equal(sellNow, Read(f).SellNow);
    }

    [Fact]
    public void Forecast_InACrash_GivesTheEndOfTheCrash()
    {
        var (f, _) = Start(screen: true, MarketInsight.ForecastNodeId);
        var item = Prepare(f, Plate, 1000, 100);
        item.Phase = MarketPhase.Crash;
        item.CrashEndTick = f.CurrentTick + 125;

        var insight = Read(f);

        Assert.Null(insight.SecondsToSpike);
        Assert.Equal(3, insight.SecondsToCrashEnd);
        Assert.False(insight.SellNow);
    }

    [Fact]
    public void Forecast_DuringTheGlobalWait_GivesTheSecondsLeft()
    {
        var (f, _) = Start(screen: true, MarketInsight.ForecastNodeId);
        Prepare(f, Plate, 1300, 100);
        Assert.Null(Read(f).SecondsSellAllBlocked);

        f.State.Exchange.SellAllBlockedUntilTick = f.CurrentTick + 90;

        Assert.Equal(2, Read(f).SecondsSellAllBlocked);
        Assert.Equal(2, ((IGameView)f).SellAllCooldownSeconds);
    }

    [Theory]
    [InlineData(2850, 185)]
    [InlineData(1000, 0)]
    [InlineData(1009, 0)]
    [InlineData(1010, 1)]
    [InlineData(3000, 200)]
    [InlineData(995, -1)]
    public void Percent_IsFlooredOverB(int priceMilli, int percent)
    {
        var (f, _) = Start(screen: true, MarketInsight.ForecastNodeId, MarketInsight.PercentNodeId);
        Prepare(f, Plate, priceMilli, 100);

        var insight = Read(f);

        Assert.Equal(percent, insight.PercentOverB);
        Assert.Null(insight.JackpotChancePercent);
    }

    [Fact]
    public void Odds_AloneShowOnlyTheOdds()
    {
        var (f, _) = Start(screen: true, MarketInsight.OddsNodeId);
        Prepare(f, Plate, 2900, 400);

        var insight = Read(f);

        Assert.Equal(2, insight.JackpotChancePercent);
        Assert.Null(insight.SecondsToSpike);
        Assert.Null(insight.PercentOverB);
    }

    [Theory]
    [InlineData(400, 0, 2)]
    [InlineData(1000, 0, 5)]
    [InlineData(3000, 0, 15)]
    [InlineData(3000, 1, 40)]
    [InlineData(3000, 2, 65)]
    [InlineData(3000, 3, 100)]
    [InlineData(20_000, 0, 50)]
    public void Odds_AreTheChanceSellUsesNow(int pieces, int pityMisses, int percent)
    {
        var (f, host) = Start(screen: true, AllModules);
        var item = Prepare(f, Plate, 2900, pieces);
        item.PityMisses = pityMisses;

        var insight = Read(f);
        Assert.Equal(percent, insight.JackpotChancePercent);

        var asked = new List<int>();
        f.State.JackpotRollForTests = p =>
        {
            asked.Add(p);
            return false;
        };
        host.Submit(new SellAll(Player, Plate));
        host.AdvanceTicks(1);

        Assert.Equal([percent * 100], asked);
    }

    [Fact]
    public void Odds_AreZero_OutsideTheSpike_UnderTheMinimum_AndDuringTheWait()
    {
        var (f, _) = Start(screen: true, AllModules);
        Prepare(f, Plate, 1400, 5000);
        Assert.Equal(0, Read(f).JackpotChancePercent);

        Prepare(f, Plate, 2900, Rules.JackpotMinPieces - 1);
        Assert.Equal(0, Read(f).JackpotChancePercent);

        Prepare(f, Plate, 2900, 5000);
        Assert.True(Read(f).JackpotChancePercent > 0);
        f.State.Exchange.SellAllBlockedUntilTick = f.CurrentTick + 60;
        Assert.Equal(0, Read(f).JackpotChancePercent);

        // No pieces at all: nothing to sell.
        Prepare(f, Wire, 2900, 0);
        Assert.Equal(0, Read(f, Wire).JackpotChancePercent);
    }

    [Fact]
    public void Reading_DoesNotChangeTheGame()
    {
        var (f, host) = Start(screen: true, AllModules);
        Prepare(f, Plate, 2900, 5000);
        host.AdvanceTicks(5);
        var before = host.Checksum();
        var randomBefore = f.State.Exchange.Random.NextInt(1_000_000);
        var (g, hostG) = Start(screen: true, AllModules);
        Prepare(g, Plate, 2900, 5000);
        hostG.AdvanceTicks(5);
        Assert.Equal(before, hostG.Checksum());

        foreach (var item in ((IGameView)g).MarketItems)
        {
            _ = Read(g, item.ItemId);
        }

        Assert.Equal(before, hostG.Checksum());
        Assert.Equal(randomBefore, g.State.Exchange.Random.NextInt(1_000_000));
    }
}
