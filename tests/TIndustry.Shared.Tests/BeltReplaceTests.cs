using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// A belt of another tier placed over a plain belt replaces it, as in Mindustry: the new belt pays only the part of its
/// cost the old one does not cover (the credit), keeps the old belt's items, and gives back the rest of the old cost at
/// the refund rule. With plans it is a replacement site that remembers the old belt; cancelling it puts the old belt back.
/// </summary>
public sealed class BeltReplaceTests
{
    private const int Player = 0;
    private const string Basic = BuildSpec.DefaultConveyorId;
    private const string Fast = "conveyor-fast";
    private const string Express = "conveyor-express";
    private const string Iron = "iron-ore";
    private const string Lead = "lead-ore";

    /// <summary>Everything the three belt tiers cost.</summary>
    private static readonly string[] Items = ["iron-ore", "lead-ore", "titanium-ore", "graphite", "silicon"];

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    /// <summary>Four cells East of the Core (20, 20), its output cell free.</summary>
    private static readonly GridPosition Cell = new(24, 21);

    private static (Factory Factory, SimHost Host, List<Refusal> Refusals) NewHost(
        bool instantBuild, Dictionary<string, int>? stock = null, int refundPercent = 100)
    {
        var wallet = new EconomyWallet(0, stock ?? Items.ToDictionary(i => i, _ => 1000, StringComparer.Ordinal));
        var factory = new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(new GridPosition(20, 20), 2), wallet);
        factory.State.Rules = new GameRules(RefundPercent: refundPercent, InstantBuild: instantBuild);
        foreach (var id in ResearchState.DemoStructureIds)
        {
            factory.Research.ForceUnlock(id);
        }

        factory.Research.ForceUnlock(Express);
        factory.AddBuilder(Player);
        var host = new SimHost(factory);
        var refusals = new List<Refusal>();
        host.CommandRejected += (_, refusal) => refusals.Add(refusal);
        return (factory, host, refusals);
    }

    /// <summary>A belt on <see cref="Cell"/> heading East with three items, the first at the (blocked) end.</summary>
    private static List<TransportedItem> OldBelt(Factory factory, string id = Basic)
    {
        Assert.True(factory.PlaceInstant(new BuildSpec(id, null, Cell, Direction.East), charge: false));
        var items = new List<TransportedItem>
        {
            new(101, Iron, 1f), new(102, Lead, 0.6f), new(103, Iron, 0.2f) // a full cell, held by its head at the dead end
        };
        factory.Belts.Cells[Cell].RestoreState(0, items);
        return items.Select(i => new TransportedItem(i.Id, i.ItemId, i.Progress)).ToList();
    }

    private static void Place(SimHost host, string id, bool crossing = false)
    {
        host.Submit(new PlaceBelt(Player, Cell, Direction.East, id, crossing));
        host.AdvanceTicks(1);
    }

    private static void RunUntil(SimHost host, Func<bool> done, int maxTicks = 3000)
    {
        for (var t = 0; t < maxTicks && !done(); t++)
        {
            host.AdvanceTicks(1);
        }

        Assert.True(done(), "condition not reached");
    }

    private static bool Idle(Factory factory) => factory.Sites.Count == 0 && factory.PlansOf(Player).Count == 0;

    /// <summary>Money (under "$") and every belt item in the wallet.</summary>
    private static Dictionary<string, int> Stock(Factory factory)
    {
        var stock = Items.ToDictionary(i => i, factory.Wallet.MaterialCount, StringComparer.Ordinal);
        stock["$"] = factory.Wallet.Money;
        return stock;
    }

    /// <summary>The stock after paying <paramref name="newId"/> over <paramref name="oldId"/>, the excess back at <paramref name="refundPercent"/>.</summary>
    private static Dictionary<string, int> AfterReplace(
        Factory factory, Dictionary<string, int> before, string oldId, string newId, int refundPercent = 100) =>
        before.ToDictionary(
            kv => kv.Key,
            kv =>
            {
                var (due, excess) = Split(factory, oldId, newId, kv.Key == "$" ? null : kv.Key);
                return kv.Value - due + excess * refundPercent / 100;
            },
            StringComparer.Ordinal);

    private static int Line(IReadOnlyList<CostEntry> cost, string? itemId) =>
        cost.Where(e => e.ItemId == itemId).Sum(e => e.Amount);

    /// <summary>What the new belt pays (new − credit) and what is left of the old cost (old − credit), per item.</summary>
    private static (int Due, int Excess) Split(Factory factory, string oldId, string newId, string? itemId)
    {
        var oldLine = Line(factory.CostOf(new BuildSpec(oldId, null, Cell, Direction.East)), itemId);
        var newLine = Line(factory.CostOf(new BuildSpec(newId, null, Cell, Direction.East)), itemId);
        var credit = Math.Min(oldLine, newLine);
        return (newLine - credit, oldLine - credit);
    }

    private static void AssertKeeps(Factory factory, IReadOnlyList<TransportedItem> old)
    {
        var cell = factory.Belts.Cells[Cell];
        var expected = old.Take(cell.Definition.Capacity).ToList();
        Assert.Equal(expected.Select(i => (i.Id, i.ItemId)), cell.Items.Select(i => (i.Id, i.ItemId)));
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Progress, cell.Items[i].Progress, 3);
        }
    }

    [Fact]
    public void UpgradeWithInstantBuildPaysTheDifferenceAndKeepsItems()
    {
        var (factory, host, refusals) = NewHost(instantBuild: true);
        var old = OldBelt(factory);
        var before = Stock(factory);

        Place(host, Fast);

        Assert.Empty(refusals);
        var cell = factory.Belts.Cells[Cell];
        Assert.Equal(Fast, cell.Definition.Id);
        Assert.Equal(Direction.East, cell.Direction);
        AssertKeeps(factory, old);
        Assert.Equal(AfterReplace(factory, before, Basic, Fast, refundPercent: 0), Stock(factory));
    }

    [Fact]
    public void UpgradeNeedsOnlyTheDifference()
    {
        var (probe, _, _) = NewHost(instantBuild: true);
        var (factory, host, refusals) = NewHost(
            instantBuild: true, Items.ToDictionary(i => i, i => Split(probe, Basic, Fast, i).Due, StringComparer.Ordinal));
        OldBelt(factory);

        Place(host, Fast);

        Assert.Empty(refusals);
        Assert.Equal(Fast, factory.Belts.Cells[Cell].Definition.Id);
        Assert.All(Stock(factory).Values, amount => Assert.Equal(0, amount));
    }

    [Fact]
    public void UpgradeShortOfTheDifferenceChangesNothing()
    {
        var (probe, _, _) = NewHost(instantBuild: true);
        var leadDue = Split(probe, Basic, Fast, Lead).Due;
        Assert.True(leadDue > 0);
        Dictionary<string, int> ShortOfLead() =>
            Items.ToDictionary(i => i, i => i == Lead ? 0 : Split(probe, Basic, Fast, i).Due, StringComparer.Ordinal);
        var (factory, host, refusals) = NewHost(instantBuild: true, ShortOfLead());
        var (control, controlHost, _) = NewHost(instantBuild: true, ShortOfLead());
        OldBelt(factory);
        OldBelt(control);

        Place(host, Fast);
        controlHost.AdvanceTicks(1);

        var refusal = Assert.Single(refusals);
        Assert.Equal(RefusalCode.InsufficientResources, refusal.Code);
        Assert.Equal([new ResourceAmount(Lead, leadDue)], refusal.MissingItems);
        Assert.Equal(controlHost.Checksum(), host.Checksum());
    }

    [Fact]
    public void UpgradeWithPlansBuildsAReplacementSite()
    {
        var (factory, host, refusals) = NewHost(instantBuild: false);
        var old = OldBelt(factory);
        var before = Stock(factory);

        Place(host, Fast);

        Assert.Empty(refusals);
        Assert.Single(factory.PlansOf(Player));
        RunUntil(host, () => factory.Sites.Count == 1);
        var site = factory.Sites[0];
        Assert.Equal(Basic, site.Replaced!.Spec.StructureId);
        Assert.False(factory.Belts.Cells.ContainsKey(Cell));
        RunUntil(host, () => Idle(factory));
        Assert.Equal(Fast, factory.Belts.Cells[Cell].Definition.Id);
        AssertKeeps(factory, old);
        Assert.Equal(AfterReplace(factory, before, Basic, Fast, refundPercent: 0), Stock(factory));
    }

    [Theory]
    [InlineData(true, Express, Basic)]
    [InlineData(false, Express, Basic)]
    // Excess of 1 per item at 50%: the floor gives nothing back.
    [InlineData(true, Express, Fast)]
    [InlineData(false, Express, Fast)]
    public void DowngradeRefundsTheExcessAtTheRuleShare(bool instantBuild, string oldId, string newId)
    {
        var (factory, host, refusals) = NewHost(instantBuild, refundPercent: 50);
        var old = OldBelt(factory, oldId);
        var before = Stock(factory);

        Place(host, newId);
        RunUntil(host, () => Idle(factory));

        Assert.Empty(refusals);
        Assert.Equal(newId, factory.Belts.Cells[Cell].Definition.Id);
        AssertKeeps(factory, old);
        Assert.True(Items.Sum(i => Split(factory, oldId, newId, i).Excess) > 0);
        Assert.Equal(AfterReplace(factory, before, oldId, newId, refundPercent: 50), Stock(factory));
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(0.9f)]
    public void CancellingAReplacementGivesBackTheOldBeltAndOnlyWhatWasPaid(float cancelAt)
    {
        var (factory, host, refusals) = NewHost(instantBuild: false);
        var old = OldBelt(factory);
        var itemsBefore = factory.Belts.Cells.Values.Sum(c => c.Items.Count);
        var before = Stock(factory);

        Place(host, Fast);
        RunUntil(host, () => factory.Sites.Count == 1 && factory.Sites[0].Progress >= cancelAt);
        Assert.True(factory.Sites[0].Progress < 1f);
        Assert.True(Stock(factory)[Lead] < before[Lead], "the site has paid part of the difference");
        host.Submit(new Remove(Player, Cell));
        RunUntil(host, () => Idle(factory));

        Assert.Empty(refusals);
        var cell = factory.Belts.Cells[Cell];
        Assert.Equal(Basic, cell.Definition.Id);
        Assert.Equal(Direction.East, cell.Direction);
        Assert.Equal(old.Select(i => i.Id), cell.Items.Select(i => i.Id));
        Assert.Equal(itemsBefore, factory.Belts.Cells.Values.Sum(c => c.Items.Count));
        Assert.Equal(before, Stock(factory));
    }

    [Fact]
    public void CancellingAReplacementAtProgressZeroPutsTheOldBeltBackAtOnce()
    {
        var (factory, _, _) = NewHost(instantBuild: false);
        var old = OldBelt(factory);
        var before = Stock(factory);
        var site = Construction.BeginReplace(factory.State, new BuildSpec(Fast, null, Cell, Direction.East), Player);
        Assert.Equal(0f, site.Progress);

        Construction.Cancel(factory.State, site);

        Assert.Equal(ConstructStep.Finished, Construction.Advance(factory.State, site, buildSpeed: 1f));
        Assert.Empty(factory.Sites);
        Assert.Equal(Basic, factory.Belts.Cells[Cell].Definition.Id);
        Assert.Equal(old.Select(i => (i.Id, i.Progress)), factory.Belts.Cells[Cell].Items.Select(i => (i.Id, i.Progress)));
        Assert.Equal(before, Stock(factory));
    }

    [Fact]
    public void ABeltRemovedBeforeTheDroneArrivesIsBuiltAsNew()
    {
        var (factory, host, _) = NewHost(instantBuild: false);
        OldBelt(factory);
        factory.State.Builders[0].X = 80f; // far away: the plan waits for it
        Place(host, Fast);
        Assert.Empty(factory.Sites);
        Assert.True(factory.RemoveInstant(Cell, refund: false));
        var before = Stock(factory);

        RunUntil(host, () => factory.Sites.Count == 1);
        Assert.Null(factory.Sites[0].Replaced);
        RunUntil(host, () => Idle(factory));

        var cost = factory.CostOf(new BuildSpec(Fast, null, Cell, Direction.East));
        Assert.Equal(Fast, factory.Belts.Cells[Cell].Definition.Id);
        Assert.Empty(factory.Belts.Cells[Cell].Items);
        Assert.Equal(before.ToDictionary(kv => kv.Key, kv => kv.Value - Line(cost, kv.Key == "$" ? null : kv.Key)), Stock(factory));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LockedJunctionOverAnotherTierReplacesTheCrossedBelt(bool instantBuild)
    {
        // A straight line heading South through Cell: a dragged fast belt East over it, junction locked.
        var (factory, host, refusals) = NewHost(instantBuild);
        factory.Research.ForceLock("junction");
        for (var y = Cell.Y - 2; y <= Cell.Y + 2; y++)
        {
            Assert.True(factory.PlaceInstant(new BuildSpec(Basic, null, new GridPosition(Cell.X, y), Direction.South), charge: false));
        }

        // The dragged line on both sides of the crossing (the client sends the crossing once the line goes past it).
        foreach (var x in new[] { Cell.X - 1, Cell.X + 1 })
        {
            Assert.True(factory.PlaceInstant(new BuildSpec(Fast, null, new GridPosition(x, Cell.Y), Direction.East), charge: false));
        }

        Assert.Equal(BeltCrossing.JunctionLocked, factory.BeltCrossingAt(Cell, Direction.East));

        Place(host, Fast, crossing: true);
        RunUntil(host, () => Idle(factory));

        Assert.Empty(refusals);
        Assert.Equal(Fast, factory.Belts.Cells[Cell].Definition.Id);
        Assert.Equal(Direction.East, factory.Belts.Cells[Cell].Direction);
    }
}
