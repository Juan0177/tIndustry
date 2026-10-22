using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>The read-only market view the client and the agents read (<see cref="IGameView.MarketItems"/>).</summary>
public sealed class MarketViewTests
{
    private static Factory NewFactory() => NewGame.Sandbox(
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json")),
        new GridPosition(60, 60), mapWidth: 120, mapHeight: 120);

    [Fact]
    public void ViewMirrorsTheStateInContentOrder()
    {
        var factory = NewFactory();
        IGameView view = factory;
        var state = factory.State.Exchange;
        state.Items[1].AutoSell = true;
        state.Items[0].Phase = MarketPhase.Crash;
        state.Items[0].CrashEndTick = view.CurrentTick + 61;

        Assert.Equal(state.Items.Select(i => i.ItemId), view.MarketItems.Select(i => i.ItemId));
        var first = view.MarketItems[0];
        Assert.Equal(MarketPhase.Crash, first.Phase);
        Assert.Equal(2, first.CrashSecondsLeft);
        Assert.Equal(MarketMath.UnitIncome(first.BaseValue, first.PriceMilli), first.UnitIncome);
        Assert.True(view.MarketItems[1].AutoSell);
        Assert.Equal(0, view.MarketItems[1].CrashSecondsLeft);
        Assert.Equal(state.Items[1].ItemId, view.MarketItem(state.Items[1].ItemId)!.ItemId);
        Assert.Null(view.MarketItem("nonesiste"));
        Assert.Equal(0, view.SellAllCooldownSeconds);
    }

    [Fact]
    public void HistoryIsACopyAndTheCooldownRoundsUp()
    {
        var factory = NewFactory();
        var item = factory.State.Exchange.Items[0];
        item.PushHistory(1000, 10);
        var view = ((IGameView)factory).MarketItem(item.ItemId)!;
        item.PushHistory(1100, 10);

        Assert.Single(view.HistoryMilli);
        factory.State.Exchange.SellAllBlockedUntilTick = factory.CurrentTick + 1;
        Assert.Equal(1, ((IGameView)factory).SellAllCooldownSeconds);
    }
}
