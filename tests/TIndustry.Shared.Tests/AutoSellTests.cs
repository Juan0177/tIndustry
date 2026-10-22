using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Auto-sell is one switch per item and pays min(B, price) without touching the market.</summary>
public sealed class AutoSellTests
{
    private static readonly string ContentPath = Path.Combine(AppContext.BaseDirectory, "data", "content.json");
    private const string Coal = "coal";
    private const string Iron = "iron-ore";

    private static Factory NewFactory(int seed = 42) =>
        NewGame.Sandbox(FactoryContent.Load(ContentPath), new GridPosition(60, 60), mapWidth: 120, mapHeight: 120,
            seed: seed, rules: new GameRules(InstantBuild: true));

    private static MarketItemState Force(Factory f, string itemId, int priceMilli)
    {
        var item = f.State.Exchange.Find(itemId)!;
        item.Phase = MarketPhase.SpikeHold;
        item.PhaseStartTick = f.State.CurrentTick;
        item.SpikeTopMilli = priceMilli;
        item.PriceMilli = priceMilli;
        return item;
    }

    /// <summary>One piece reaches the core from the west by an adjacent building.</summary>
    private static void DeliverByBuilding(Factory f, string itemId) =>
        Assert.True(f.State.TryDeliverAdjacent(new GridPosition(58, 60), 2, itemId));

    /// <summary>One piece reaches the core on a belt cell facing it.</summary>
    private static void DeliverByBelt(Factory f, string itemId)
    {
        var cell = new GridPosition(59, 60);
        Assert.True(f.TryPlaceBelt(cell, Direction.East));
        Assert.True(f.Belts.TryInsert(cell, new TransportedItem(1, itemId), Direction.East));
        for (var i = 0; i < 300 && f.CoreDeliveredItems == 0; i++)
        {
            f.Step();
        }

        Assert.Equal(1, f.CoreDeliveredItems);
    }

    [Fact]
    public void AutoSellIsPerItem()
    {
        var f = NewFactory();
        f.State.Exchange.Find(Coal)!.AutoSell = true;
        var money = f.Wallet.Money;
        var coalStock = f.Wallet.MaterialCount(Coal);
        var ironStock = f.Wallet.MaterialCount(Iron);

        DeliverByBuilding(f, Coal);
        DeliverByBuilding(f, Iron);

        Assert.Equal(coalStock, f.Wallet.MaterialCount(Coal));
        Assert.Equal(ironStock + 1, f.Wallet.MaterialCount(Iron));
        Assert.True(f.Wallet.Money > money);
        Assert.Equal(1, f.Session.SoldByItem[Coal]);
        Assert.False(f.Session.SoldByItem.ContainsKey(Iron));
    }

    [Fact]
    public void AutoSellPaysTheMinimumPrice()
    {
        var f = NewFactory();
        var item = f.State.Exchange.Find(Coal)!;
        item.AutoSell = true;

        Force(f, Coal, 1300);
        var money = f.Wallet.Money;
        DeliverByBuilding(f, Coal);
        Assert.Equal(item.BaseValue, f.Wallet.Money - money);

        Force(f, Coal, 650);
        money = f.Wallet.Money;
        DeliverByBuilding(f, Coal);
        Assert.Equal(MarketMath.UnitIncome(item.BaseValue, 650), f.Wallet.Money - money);
    }

    [Fact]
    public void AutoSellDoesNotMoveTheMarket()
    {
        static (int Price, int Drop, long Recovery, int[] Window, int Roll) Run(bool autoSell)
        {
            var f = NewFactory();
            var item = Force(f, Coal, 1800);
            item.AutoSell = autoSell;
            for (var i = 0; i < 20; i++)
            {
                DeliverByBuilding(f, Coal);
            }

            var snapshot = (item.PriceMilli, item.SaleDropMicro, item.SaleRecoveryEndTick, (int[])item.WindowPieces.Clone());
            return (snapshot.PriceMilli, snapshot.SaleDropMicro, snapshot.SaleRecoveryEndTick, snapshot.Item4,
                f.State.Exchange.Random.NextInt(1_000_000));
        }

        var on = Run(true);
        var off = Run(false);

        Assert.Equal(off.Price, on.Price);
        Assert.Equal(0, on.Drop);
        Assert.Equal(off.Recovery, on.Recovery);
        Assert.Equal(off.Window, on.Window);
        Assert.Equal(off.Roll, on.Roll);
    }

    [Fact]
    public void BeltsAndBuildingsSellTheSameWay()
    {
        var byBelt = NewFactory();
        var byBuilding = NewFactory();
        foreach (var f in new[] { byBelt, byBuilding })
        {
            f.State.Exchange.Find(Coal)!.AutoSell = true;
            Force(f, Coal, 1500);
        }

        var beltMoney = byBelt.Wallet.Money;
        var buildingMoney = byBuilding.Wallet.Money;
        DeliverByBelt(byBelt, Coal);
        DeliverByBuilding(byBuilding, Coal);

        Assert.Equal(byBuilding.Wallet.Money - buildingMoney, byBelt.Wallet.Money - beltMoney);
        Assert.Equal(byBuilding.Wallet.MaterialCount(Coal), byBelt.Wallet.MaterialCount(Coal));
        Assert.Equal(1, byBelt.Session.SoldByItem[Coal]);
        Assert.Equal(1, byBuilding.Session.SoldByItem[Coal]);
    }

    [Fact]
    public void SetAutoSellTogglesOneItemAndRefusesUnknownOnes()
    {
        var f = NewFactory();
        var host = new SimHost(f);
        var rejected = new List<RefusalCode>();
        host.CommandRejected += (_, r) => rejected.Add(r.Code);

        host.Submit(new SetAutoSell(1, Coal, true));
        host.Submit(new SetAutoSell(1, "not-an-item", true));
        host.Advance(SimulationClock.TickSeconds);

        Assert.True(f.State.Exchange.Find(Coal)!.AutoSell);
        Assert.False(f.State.Exchange.Find(Iron)!.AutoSell);
        Assert.Equal([RefusalCode.InvalidCommand], rejected);
    }
}
