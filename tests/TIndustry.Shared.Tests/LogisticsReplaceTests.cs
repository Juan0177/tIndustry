using System.Text;
using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Any 1×1 logistics block placed over another 1×1 logistics block replaces it, as in Mindustry: belts of every tier
/// (armored and stack too), junction, router, both sorters and both gates. Bridges never replace nor are replaced. The new
/// block pays only the part of its cost the old one does not cover, keeps the items it can hold through its normal
/// acceptance and sends the rest to the Core; cancelling a replacement site puts the old block back with its items and
/// its state (filter, turns, router state).
/// </summary>
[Collection(SaveStoreCollection.Name)]
public sealed class LogisticsReplaceTests
{
    private const int Player = 0;
    private const string Basic = BuildSpec.DefaultConveyorId;
    private const string Stack = "conveyor-stack";
    private const string Coal = "coal";

    /// <summary>Everything the 1×1 logistics blocks cost; coal (no cost line) is the transported item.</summary>
    private static readonly string[] Items =
        ["iron-ore", "lead-ore", "titanium-ore", "graphite", "silicon", "plastic", "lead-plate", "copper-wire", "titanium-plate"];

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    /// <summary>Two cells East of the 3×3 Core (20, 20), no neighbour: items stay where they are.</summary>
    private static readonly GridPosition Cell = new(24, 21);

    private static (Factory Factory, SimHost Host, List<Refusal> Refusals) NewHost(
        bool instantBuild, int refundPercent = 100, Dictionary<string, int>? stock = null)
    {
        // Below the Core's maximum (1000), so refunds are never clamped here.
        var wallet = new EconomyWallet(0, stock ?? Items.ToDictionary(i => i, _ => 500, StringComparer.Ordinal));
        var factory = new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(new GridPosition(20, 20)), wallet);
        factory.State.Rules = new GameRules(RefundPercent: refundPercent, InstantBuild: instantBuild);
        foreach (var conveyor in Content.Conveyors)
        {
            factory.Research.ForceUnlock(conveyor.Id);
        }

        factory.AddBuilder(Player);
        var host = new SimHost(factory);
        var refusals = new List<Refusal>();
        host.CommandRejected += (_, refusal) => refusals.Add(refusal);
        return (factory, host, refusals);
    }

    private static BuildSpec Spec(string id, string? filter = null) =>
        new(id, BuildSpec.KindFor(id), Cell, Direction.East, FilterItemId: filter);

    /// <summary>Puts <paramref name="id"/> on <see cref="Cell"/> (free) holding <paramref name="items"/>.</summary>
    private static BeltGridCell Put(Factory factory, string id, IEnumerable<TransportedItem>? items = null, string? filter = null)
    {
        Assert.True(factory.PlaceInstant(Spec(id, filter), charge: false));
        var cell = factory.Belts.Cells[Cell];
        if (items is not null)
        {
            cell.RestoreState(items, filter: filter);
        }

        return cell;
    }

    /// <summary>Three coal pieces on a belt heading East, the first at the (blocked) end.</summary>
    private static List<TransportedItem> ThreeCoal() =>
        [new(101, Coal, 1f), new(102, Coal, 0.6f), new(103, Coal, 0.2f)];

    private static void Place(SimHost host, string id, string? filter = null, Direction direction = Direction.East)
    {
        host.Submit(BuildSpec.KindFor(id) is { } kind
            ? new PlaceBuilding(Player, kind, Cell, direction, FilterItemId: filter)
            : new PlaceBelt(Player, Cell, direction, id));
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

    /// <summary>Places <paramref name="id"/> over <see cref="Cell"/> and waits until the drone is done (at once with instant build).</summary>
    private static void Replace(SimHost host, Factory factory, string id, string? filter = null)
    {
        Place(host, id, filter);
        RunUntil(host, () => Idle(factory));
    }

    /// <summary>Money (under "$") and every cost item in the wallet.</summary>
    private static Dictionary<string, int> Stock(Factory factory)
    {
        var stock = Items.ToDictionary(i => i, factory.Wallet.MaterialCount, StringComparer.Ordinal);
        stock["$"] = factory.Wallet.Money;
        return stock;
    }

    /// <summary>Pieces of <paramref name="itemId"/> on the logistics grid (packs count their pieces).</summary>
    private static int OnGrid(Factory factory, string itemId) =>
        factory.Belts.Cells.Values.SelectMany(c => c.Items).Where(i => i.ItemId == itemId).Sum(i => i.Count);

    private static int Line(IReadOnlyList<CostEntry> cost, string? itemId) =>
        cost.Where(e => e.ItemId == itemId).Sum(e => e.Amount);

    /// <summary>The stock after paying <paramref name="newId"/> over <paramref name="oldId"/>, the excess back at <paramref name="refundPercent"/>.</summary>
    private static Dictionary<string, int> AfterReplace(
        Factory factory, Dictionary<string, int> before, string oldId, string newId, int refundPercent) =>
        before.ToDictionary(
            kv => kv.Key,
            kv =>
            {
                var itemId = kv.Key == "$" ? null : kv.Key;
                var oldLine = Line(factory.CostOf(Spec(oldId)), itemId);
                var newLine = Line(factory.CostOf(Spec(newId)), itemId);
                var credit = Math.Min(oldLine, newLine);
                return kv.Value - (newLine - credit) + Construction.RefundShare(oldLine - credit, refundPercent * 100);
            },
            StringComparer.Ordinal);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BeltToRouterKeepsOneItemAndSendsTheRestToTheCore(bool instantBuild)
    {
        var (factory, host, refusals) = NewHost(instantBuild, refundPercent: 50);
        Put(factory, Basic, ThreeCoal());
        var before = Stock(factory);

        Replace(host, factory, "router");

        Assert.Empty(refusals);
        var cell = factory.Belts.Cells[Cell];
        Assert.Equal(LogisticsKind.Router, cell.Kind);
        Assert.Equal(101, Assert.Single(cell.Items).Id);
        // The item came in from behind the belt (West): the router never hands it back there.
        Assert.Equal(Direction.West, cell.RouterFrom);
        Assert.Equal(2, factory.Wallet.MaterialCount(Coal));
        Assert.Equal(3, OnGrid(factory, Coal) + factory.Wallet.MaterialCount(Coal));
        Assert.Equal(AfterReplace(factory, before, Basic, "router", refundPercent: 50), Stock(factory));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RouterToBeltKeepsTheRoutersItem(bool instantBuild)
    {
        var (factory, host, refusals) = NewHost(instantBuild, refundPercent: 50);
        var router = Put(factory, "router", [new TransportedItem(7, Coal)]);
        router.RestoreRouter(turn: 2, waitTicks: 3, from: Direction.North);
        var before = Stock(factory);

        Replace(host, factory, Basic);

        Assert.Empty(refusals);
        var cell = factory.Belts.Cells[Cell];
        Assert.Equal(Basic, cell.Definition.Id);
        Assert.Equal(Direction.East, cell.Direction);
        Assert.Equal(7, Assert.Single(cell.Items).Id);
        Assert.Null(cell.Items[0].Travel);
        Assert.Equal(0, factory.Wallet.MaterialCount(Coal));
        // Downgrade: the router's iron beyond the belt's comes back at 50%, rounded up.
        Assert.Equal(AfterReplace(factory, before, "router", Basic, refundPercent: 50), Stock(factory));
    }

    public static TheoryData<bool, string> PassThroughBlocks()
    {
        var data = new TheoryData<bool, string>();
        foreach (var id in new[] { "sorter", "sorter-inverted", "overflow-gate", "underflow-gate" })
        {
            data.Add(true, id);
            data.Add(false, id);
        }

        return data;
    }

    /// <summary>Review focus: a sorter or a gate holds nothing, so every piece goes to the Core, none lost, none twice.</summary>
    [Theory]
    [MemberData(nameof(PassThroughBlocks))]
    public void BeltToSorterOrGateSendsEveryPieceToTheCore(bool instantBuild, string id)
    {
        var (factory, host, refusals) = NewHost(instantBuild);
        Put(factory, Basic, ThreeCoal());
        var before = Stock(factory);

        Replace(host, factory, id);

        Assert.Empty(refusals);
        var cell = factory.Belts.Cells[Cell];
        Assert.Equal(id, cell.Definition.Id);
        Assert.Empty(cell.Items);
        Assert.Equal(3, factory.Wallet.MaterialCount(Coal));
        Assert.Equal(AfterReplace(factory, before, Basic, id, refundPercent: 100), Stock(factory));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void APackOverASorterGoesToTheCoreWithAllItsPieces(bool instantBuild)
    {
        var (factory, host, refusals) = NewHost(instantBuild);
        Put(factory, Stack, [new TransportedItem(9, Coal, 0.5f) { Count = 7 }]);

        Replace(host, factory, "sorter", filter: "iron-ore");

        Assert.Empty(refusals);
        Assert.Equal(LogisticsKind.Sorter, factory.Belts.Cells[Cell].Kind);
        // The command's filter, not the old block's.
        Assert.Equal("iron-ore", factory.Belts.Cells[Cell].FilterItemId);
        Assert.Equal(7, factory.Wallet.MaterialCount(Coal));
        Assert.Equal(0, OnGrid(factory, Coal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SorterToGatePaysTheDifferenceAndStartsWithoutTheSortersState(bool instantBuild)
    {
        var (factory, host, refusals) = NewHost(instantBuild, refundPercent: 50);
        Put(factory, "sorter", filter: Coal).RestoreSorter(0b0101);
        var before = Stock(factory);

        Replace(host, factory, "overflow-gate");

        Assert.Empty(refusals);
        var cell = factory.Belts.Cells[Cell];
        Assert.Equal("overflow-gate", cell.Definition.Id);
        Assert.Equal(0, cell.SorterTurns);
        Assert.Null(cell.FilterItemId);
        Assert.Equal(AfterReplace(factory, before, "sorter", "overflow-gate", refundPercent: 50), Stock(factory));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void JunctionToBeltKeepsWhatFitsAndSendsTheRestToTheCore(bool instantBuild)
    {
        var (factory, host, refusals) = NewHost(instantBuild);
        Put(factory, "junction",
        [
            new TransportedItem(1, Coal, 1f, Direction.East) { JunctionTicks = 26 },
            new TransportedItem(2, Coal, 0.5f, Direction.North) { JunctionTicks = 13 },
            new TransportedItem(3, Coal, 0.3f, Direction.East) { JunctionTicks = 8 },
            new TransportedItem(4, Coal, 0.1f, Direction.South) { JunctionTicks = 3 },
            new TransportedItem(5, Coal, 0f, Direction.West) { JunctionTicks = 0 }
        ]);

        Replace(host, factory, Basic);

        Assert.Empty(refusals);
        var cell = factory.Belts.Cells[Cell];
        Assert.Equal(Basic, cell.Definition.Id);
        Assert.Equal(cell.Definition.Capacity, cell.Items.Count);
        Assert.All(cell.Items, i => Assert.Null(i.Travel));
        Assert.All(cell.Items, i => Assert.Null(i.JunctionTicks));
        // A valid belt: front first, spaced, inside the cell.
        for (var i = 0; i < cell.Items.Count; i++)
        {
            Assert.InRange(cell.Items[i].Progress, 0f, 1f);
            if (i > 0)
            {
                Assert.True(cell.Items[i].Progress <= cell.Items[i - 1].Progress - cell.Definition.ItemSpacing + 1e-4f);
            }
        }

        Assert.Equal(5 - cell.Definition.Capacity, factory.Wallet.MaterialCount(Coal));
        Assert.Equal(5, OnGrid(factory, Coal) + factory.Wallet.MaterialCount(Coal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BeltToJunctionQueuesTheItemsInTheBeltsDirection(bool instantBuild)
    {
        var (factory, host, refusals) = NewHost(instantBuild);
        Put(factory, Basic, ThreeCoal());

        Replace(host, factory, "junction");

        Assert.Empty(refusals);
        var cell = factory.Belts.Cells[Cell];
        Assert.Equal(LogisticsKind.Junction, cell.Kind);
        Assert.Equal([101L, 102L, 103L], cell.Items.Select(i => i.Id));
        Assert.All(cell.Items, i => Assert.Equal(Direction.East, i.Travel));
        Assert.Equal(0, factory.Wallet.MaterialCount(Coal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BeltToStackAndBackPacksOnTheLoadingCellAndNeverLosesAPiece(bool instantBuild)
    {
        var (factory, host, refusals) = NewHost(instantBuild);
        Put(factory, Basic,
            [new TransportedItem(101, Coal, 1f), new TransportedItem(102, "sand", 0.6f), new TransportedItem(103, Coal, 0.2f)]);

        Replace(host, factory, Stack);

        Assert.Empty(refusals);
        var cell = factory.Belts.Cells[Cell];
        Assert.Equal(LogisticsKind.Stack, cell.Kind);
        Assert.True(cell.IsStackLoader);
        // The coal joins one pack; the sand (another item) goes to the Core.
        var pack = Assert.Single(cell.Items);
        Assert.Equal((101L, Coal, 2), (pack.Id, pack.ItemId, pack.Count));
        Assert.Equal((0, 1), (factory.Wallet.MaterialCount(Coal), factory.Wallet.MaterialCount("sand")));

        // And back: a belt takes single pieces only, the pack goes to the Core whole.
        Replace(host, factory, Basic);

        Assert.Empty(refusals);
        Assert.Equal(Basic, factory.Belts.Cells[Cell].Definition.Id);
        Assert.Empty(factory.Belts.Cells[Cell].Items);
        Assert.Equal((2, 1), (factory.Wallet.MaterialCount(Coal), factory.Wallet.MaterialCount("sand")));
    }

    [Fact]
    public void InstantReplacementShortOfTheDifferenceChangesNothing()
    {
        // Belt (1 iron) to router (3 iron): only 2 iron are due; with 1 the router is refused and nothing changes.
        Dictionary<string, int> Short() => Items.ToDictionary(i => i, i => i == "iron-ore" ? 1 : 0, StringComparer.Ordinal);
        var (factory, host, refusals) = NewHost(instantBuild: true, stock: Short());
        var (control, controlHost, _) = NewHost(instantBuild: true, stock: Short());
        Put(factory, Basic, ThreeCoal());
        Put(control, Basic, ThreeCoal());

        Place(host, "router");
        controlHost.AdvanceTicks(1);

        var refusal = Assert.Single(refusals);
        Assert.Equal(RefusalCode.InsufficientResources, refusal.Code);
        Assert.Equal([new ResourceAmount("iron-ore", 1)], refusal.MissingItems);
        Assert.Equal(controlHost.Checksum(), host.Checksum());
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(0.9f)]
    public void CancellingPutsTheOldSorterBackWithItsFilterAndTurns(float cancelAt)
    {
        var (factory, host, refusals) = NewHost(instantBuild: false);
        Put(factory, "sorter", filter: Coal).RestoreSorter(0b1010);
        var before = Stock(factory);

        Place(host, "router");
        RunUntil(host, () => factory.Sites.Count == 1 && factory.Sites[0].Progress >= cancelAt);
        Assert.True(factory.Sites[0].Progress < 1f);
        host.Submit(new Remove(Player, Cell));
        RunUntil(host, () => Idle(factory));

        Assert.Empty(refusals);
        var cell = factory.Belts.Cells[Cell];
        Assert.Equal("sorter", cell.Definition.Id);
        Assert.Equal(Coal, cell.FilterItemId);
        Assert.Equal(0b1010, cell.SorterTurns);
        Assert.Equal(before, Stock(factory));
    }

    [Fact]
    public void CancellingAtProgressZeroPutsTheRouterBackWithItsItemAndState()
    {
        var (factory, _, _) = NewHost(instantBuild: false);
        var router = Put(factory, "router", [new TransportedItem(7, Coal)]);
        router.RestoreRouter(turn: 2, waitTicks: 5, from: Direction.North);
        var before = Stock(factory);
        var site = Construction.BeginReplace(factory.State, Spec(Basic), Player);
        Assert.Equal(0f, site.Progress);
        Assert.Equal("router", Assert.Single(site.Replaced!).Spec.StructureId);

        Construction.Cancel(factory.State, site);

        Assert.Equal(ConstructStep.Finished, Construction.Advance(factory.State, site, buildSpeed: 1f));
        var cell = factory.Belts.Cells[Cell];
        Assert.Equal(LogisticsKind.Router, cell.Kind);
        Assert.Equal(7, Assert.Single(cell.Items).Id);
        Assert.Equal((2, 5, Direction.North), (cell.RouterTurn, cell.RouterWaitTicks, cell.RouterFrom));
        Assert.Equal(before, Stock(factory));
        Assert.Equal(0, factory.Wallet.MaterialCount(Coal));
    }

    [Theory]
    [InlineData(true, Basic, "conveyor-bridge")]
    [InlineData(false, Basic, "conveyor-bridge")]
    [InlineData(true, "router", "conveyor-bridge")]
    [InlineData(false, "router", "bridge-long")]
    [InlineData(true, "conveyor-bridge", Basic)]
    [InlineData(false, "conveyor-bridge", "router")]
    [InlineData(true, "bridge-long", Basic)]
    public void BridgesAreNeverReplacedNorReplace(bool instantBuild, string newId, string oldId)
    {
        var (factory, host, refusals) = NewHost(instantBuild);
        Put(factory, oldId);
        factory.Belts.Cells.TryGetValue(Cell, out var before);

        Assert.Equal(BeltOverBelt.None, factory.BeltOverBeltAt(Cell, Direction.East, newId));
        Place(host, newId);
        host.AdvanceTicks(5);

        Assert.Equal(RefusalCode.Occupied, Assert.Single(refusals).Code);
        Assert.Same(before, factory.Belts.Cells[Cell]);
        Assert.Empty(factory.Sites);
    }

    [Theory]
    [InlineData(true, "router")]
    [InlineData(false, "router")]
    [InlineData(true, "junction")]
    [InlineData(false, "overflow-gate")]
    [InlineData(true, "sorter")]
    [InlineData(false, "sorter")]
    public void TheSameBlockOverItselfDoesNothing(bool instantBuild, string id)
    {
        var (factory, host, refusals) = NewHost(instantBuild);
        var (control, controlHost, _) = NewHost(instantBuild);
        Put(factory, id, filter: id == "sorter" ? Coal : null);
        Put(control, id, filter: id == "sorter" ? Coal : null);
        Assert.Equal(BeltOverBelt.Same, factory.BeltOverBeltAt(Cell, Direction.South, id));

        // Another direction and another filter: still nothing (the filter is changed from its panel).
        Place(host, id, filter: id == "sorter" ? "iron-ore" : null, direction: Direction.South);
        controlHost.AdvanceTicks(1);

        Assert.Empty(refusals);
        Assert.Empty(factory.PlansOf(Player));
        Assert.Equal(controlHost.Checksum(), host.Checksum());
    }

    [Fact]
    public void EveryPairOfOneCellLogisticsBlocksIsAReplacement()
    {
        string[] ids =
        [
            Basic, "conveyor-fast", "conveyor-express", "conveyor-armored", Stack,
            "junction", "router", "sorter", "sorter-inverted", "overflow-gate", "underflow-gate"
        ];
        foreach (var oldId in ids)
        {
            var (factory, _, _) = NewHost(instantBuild: true);
            Put(factory, oldId);
            foreach (var newId in ids.Where(id => id != oldId))
            {
                Assert.Equal(BeltOverBelt.Replace, factory.BeltOverBeltAt(Cell, Direction.East, newId));
            }
        }
    }

    [Fact]
    public void ABeltLineDraggedThroughAJunctionKeepsIt()
    {
        // A line East over (24, 21) holding a junction, and over the plain belt (26, 21) heading South: the junction
        // stays (it already crosses), the belt becomes a junction (the auto-junction).
        var (factory, host, refusals) = NewHost(instantBuild: true);
        Put(factory, "junction");
        for (var y = 20; y <= 22; y++)
        {
            Assert.True(factory.PlaceInstant(new BuildSpec(Basic, null, new GridPosition(26, y), Direction.South), charge: false));
        }

        var line = PlacementLine.Plan(factory, new LineRequest(Player, Basic, new GridPosition(23, 21), new GridPosition(28, 21), Direction.East));
        foreach (var command in PlacementLine.Commands(line))
        {
            host.Submit(command);
        }

        host.AdvanceTicks(1);

        Assert.Empty(refusals);
        Assert.Equal(LogisticsKind.Junction, factory.Belts.Cells[Cell].Kind);
        Assert.Equal(LogisticsKind.Junction, factory.Belts.Cells[new GridPosition(26, 21)].Kind);
        Assert.Equal(LogisticsKind.Belt, factory.Belts.Cells[new GridPosition(25, 21)].Kind);

        // A single belt placed on the junction (not a line through it) replaces it.
        host.Submit(new PlaceBelt(Player, Cell, Direction.North, Basic));
        host.AdvanceTicks(1);
        Assert.Equal(LogisticsKind.Belt, factory.Belts.Cells[Cell].Kind);
    }

    [Fact]
    public void AReplacementSiteSavedHalfwayLoadsWithTheOldBlocksStateAndCancelsBackToIt()
    {
        var (factory, host, refusals) = NewHost(instantBuild: false);
        Put(factory, "sorter", filter: Coal).RestoreSorter(0b0110);
        var before = Stock(factory);
        Place(host, "router");
        RunUntil(host, () => factory.Sites.Count == 1 && factory.Sites[0].Progress >= 0.4f);
        Assert.True(factory.Sites[0].Progress < 1f);

        FactorySaveStore.Save("v23-sostituzione-logistica", host);
        var json = File.ReadAllText(FactorySaveStore.SlotPath("v23-sostituzione-logistica"));
        Assert.Contains("\"version\": 23", json);
        Assert.True(FactorySaveStore.TryLoad(Content, "v23-sostituzione-logistica", out var loaded, out var error), error);
        Assert.Equal(host.Checksum(), loaded!.Checksum());
        var old = Assert.Single(Assert.Single(loaded.State.Sites).Replaced!);
        Assert.Equal(("sorter", Coal), (old.Spec.StructureId, old.Spec.FilterItemId));

        // Both go on the same way; the loaded one is then cancelled back to the sorter.
        host.AdvanceTicks(5);
        loaded.AdvanceTicks(5);
        Assert.Equal(host.Checksum(), loaded.Checksum());
        loaded.Submit(new Remove(Player, Cell));
        for (var t = 0; t < 3000 && (loaded.State.Sites.Count > 0 || loaded.State.Builders[0].Plans.Count > 0); t++)
        {
            loaded.AdvanceTicks(1);
        }

        var cell = loaded.State.Belts[Cell];
        Assert.Equal(("sorter", Coal, 0b0110), (cell.Definition.Id, cell.FilterItemId, cell.SorterTurns));
        Assert.Empty(refusals);
        var reread = Snapshot.Read(Content, loaded.CreateSnapshot());
        Assert.Equal(before["lead-ore"], reread.Wallet.MaterialCount("lead-ore"));
        Assert.Equal(before["iron-ore"], reread.Wallet.MaterialCount("iron-ore"));
    }

    [Fact]
    public void AReplacementSiteOfARouterWithItsItemRoundTripsTheSnapshot()
    {
        var (factory, host, _) = NewHost(instantBuild: false);
        var router = Put(factory, "router", [new TransportedItem(7, Coal)]);
        router.RestoreRouter(turn: 3, waitTicks: 8, from: Direction.South);
        Place(host, "junction");
        RunUntil(host, () => factory.Sites.Count == 1 && factory.Sites[0].Progress > 0f);

        var snapshot = host.CreateSnapshot();
        var text = Encoding.UTF8.GetString(snapshot);
        Assert.Contains("\"replaced\":[", text);
        var state = Snapshot.Read(Content, snapshot);
        Assert.Equal(host.Checksum(), Snapshot.Checksum(Snapshot.Write(state)));
        var old = Assert.Single(Assert.Single(state.Sites).Replaced!);
        Assert.Equal(7, Assert.Single(old.Items).Id);

        // Cancelled on the reread state: the router comes back with its item and its state.
        Construction.Cancel(state, state.Sites[0]);
        for (var t = 0; t < 3000 && state.Sites.Count > 0; t++)
        {
            Construction.Advance(state, state.Sites[0], buildSpeed: 1f);
        }

        var cell = state.Belts.Cells[Cell];
        Assert.Equal(7, Assert.Single(cell.Items).Id);
        Assert.Equal((3, 8, Direction.South), (cell.RouterTurn, cell.RouterWaitTicks, cell.RouterFrom));
    }

    [Fact]
    public void SavesAreVersion23AndVersion22IsRefused()
    {
        Assert.Equal(23, FactorySaveData.CurrentVersion);
        Assert.Equal(23, FactorySaveData.MinReadableVersion);
        var (_, host, _) = NewHost(instantBuild: true);
        FactorySaveStore.Save("v22-rifiutato-logistica", host);
        var path = FactorySaveStore.SlotPath("v22-rifiutato-logistica");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"version\": 23", "\"version\": 22"));

        Assert.False(FactorySaveStore.TryLoadSlot(Content, "v22-rifiutato-logistica", out var loaded, out var failure));
        Assert.Null(loaded);
        Assert.Equal(SaveLoadError.OlderVersion, failure!.Code);
        var snapshot = Encoding.UTF8.GetString(host.CreateSnapshot()).Replace("\"version\":23", "\"version\":22");
        Assert.Throws<InvalidDataException>(() => Snapshot.Read(Content, Encoding.UTF8.GetBytes(snapshot)));
    }

    [Fact]
    public void ALoadedReplacementOfABridgeIsDroppedWithItsItemsToTheCore()
    {
        var (factory, host, _) = NewHost(instantBuild: false);
        Put(factory, Basic, [new TransportedItem(77, Coal, 1f)]);
        Place(host, "router");
        RunUntil(host, () => factory.Sites.Count == 1);
        var data = factory.Capture();
        var replaced = Assert.Single(data.Sites[0].Replaced!);
        replaced.Spec.StructureId = "conveyor-bridge";
        replaced.Spec.Kind = nameof(BuildingKind.Bridge);

        var restored = Factory.Restore(Content, data);

        Assert.Empty(restored.Sites);
        Assert.Equal(1, restored.Wallet.MaterialCount(Coal));
    }

    [Fact]
    public void PiazzaOverALogisticsBlockSaysWhatItReplaced()
    {
        var factory = NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120,
            rules: new GameRules(InstantBuild: true));
        factory.UnlockAllForDemo();
        factory.EnsureDemoBuildStock();
        var host = new SimHost(factory);
        var game = new AgentGame(host, Content);
        JsonElement Call(string json) => game.Call("piazza", JsonDocument.Parse(json).RootElement);

        var first = Call("""{"tipo":"conveyor-basic","cella":{"x":40,"y":40},"direzione":"est"}""");
        Assert.False(first.GetProperty("celle")[0].TryGetProperty("sostituisce", out _));
        var router = Call("""{"tipo":"router","cella":{"x":40,"y":40}}""");

        Assert.Equal("conveyor-basic", router.GetProperty("celle")[0].GetProperty("sostituisce").GetString());
        Assert.Equal(LogisticsKind.Router, factory.Belts.Cells[new GridPosition(40, 40)].Kind);
    }
}
