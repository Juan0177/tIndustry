using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The research costs written by tools/dev/genera_finanza.py (sheet «Bilanciamento»): about six hours for the whole
/// tree, and money that saturates at <see cref="int.MaxValue"/> instead of wrapping when incomes and jackpots pile up.
/// </summary>
public sealed class ResearchCostTests
{
    private static readonly string ContentPath = Path.Combine(AppContext.BaseDirectory, "data", "content.json");
    private static readonly FactoryContent Content = FactoryContent.Load(ContentPath);

    [Fact]
    public void ResearchMoneyTotalsBetween22And26Point7Million()
    {
        // 25.5 M since the impact drill (Ricerca allargata, plan 1), 26.7 M since the stack conveyor (plan 2); plan 3
        // rebalances the whole tree.
        var total = Content.Structures.Sum(s => (long)(s.Unlock?.Money ?? 0));
        Assert.InRange(total, 22_000_000L, 26_700_000L);
    }

    [Fact]
    public void ConveyorCopiesOfTheUnlockMatchTheStructures()
    {
        foreach (var conveyor in Content.Conveyors.Where(c => c.Unlock is not null))
        {
            var structure = Content.RequireStructure(conveyor.Id).Unlock!;
            Assert.Equal(structure.Money, conveyor.Unlock!.Money);
            Assert.Equal(structure.Materials, conveyor.Unlock.Materials);
        }
    }

    [Fact]
    public void UnlockingTheWholeTreeNearIntMaxDoesNotOverflow()
    {
        var factory = NewGame.Sandbox(Content, new GridPosition(30, 30), mapWidth: 60, mapHeight: 60,
            rules: new GameRules(InstantBuild: true));
        factory.Wallet.AddMoney(int.MaxValue);
        foreach (var s in Content.Structures)
        {
            foreach (var m in s.Unlock?.Materials ?? [])
            {
                factory.Wallet.AddMaterial(m.ItemId, m.Amount);
            }
        }

        var total = 0L;
        var progress = true;
        while (progress)
        {
            progress = false;
            foreach (var s in Content.Structures.Where(s => !factory.IsStructureUnlocked(s.Id)))
            {
                if (factory.TryUnlockStructure(s.Id))
                {
                    total += s.Unlock?.Money ?? 0;
                    progress = true;
                }
            }
        }

        Assert.All(Content.Structures, s => Assert.True(factory.IsStructureUnlocked(s.Id), s.Id));
        Assert.Equal(int.MaxValue - total, factory.Wallet.Money);

        // Income on top of a nearly full wallet saturates.
        factory.Wallet.AddMoney(int.MaxValue);
        factory.Wallet.AddMoney(int.MaxValue);
        Assert.Equal(int.MaxValue, factory.Wallet.Money);
    }

    [Fact]
    public void JackpotOnANearlyFullWalletSaturates()
    {
        var f = NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120,
            rules: new GameRules(InstantBuild: true));
        f.State.HasMarketScreen = true;
        f.State.JackpotRollForTests = _ => true;
        var item = f.State.Exchange.Find("titanium-plate")!;
        item.Phase = MarketPhase.SpikeHold;
        item.PhaseStartTick = f.State.CurrentTick;
        item.SpikeTopMilli = 2800;
        item.PriceMilli = 2800;
        f.Wallet.AddMoney(int.MaxValue - f.Wallet.Money - 1000);
        f.Wallet.AddMaterial("titanium-plate", 10_000);

        Assert.True(f.TrySellAll("titanium-plate", 1));

        Assert.Equal(int.MaxValue, f.Wallet.Money);
    }

    /// <summary>The 50/50 of the sheet: 10,000 titanium plates at the spike threshold, close to a CORE Nucleus.</summary>
    [Fact]
    public void ATenThousandTitaniumJackpotIsWorthAboutFiveAndAHalfMillion()
    {
        var f = NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120,
            rules: new GameRules(InstantBuild: true));
        f.State.HasMarketScreen = true;
        f.State.JackpotRollForTests = _ => true;
        var item = f.State.Exchange.Find("titanium-plate")!;
        item.Phase = MarketPhase.SpikeHold;
        item.PhaseStartTick = f.State.CurrentTick;
        item.SpikeTopMilli = 2800;
        item.PriceMilli = 2800;
        f.Wallet.AddMaterial("titanium-plate", 10_000 - f.Wallet.MaterialCount("titanium-plate"));
        var before = f.Wallet.Money;

        Assert.True(f.TrySellAll("titanium-plate", 1));

        var jackpot = f.Wallet.Money - before;
        Assert.InRange(jackpot, 5_000_000, 6_000_000);
        Assert.True(jackpot > Content.RequireStructure("core-nucleus").Unlock!.Money * 2 / 3);
    }
}
