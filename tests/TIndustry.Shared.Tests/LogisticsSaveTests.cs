using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Save version 18: the Mindustry-style logistics state (junction queues, router item and turn, sorter turns and
/// nullable filter, extractor accumulator and turns) goes through Capture/Restore and the snapshot checksum.
/// </summary>
public sealed class LogisticsSaveTests
{
    private const string Plate = "iron-plate";
    private const string Lead = "lead-plate";
    private const string Coal = "coal";
    private const int Minute = 60 * SimulationClock.TickRate;

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static Factory NewFactory()
    {
        var f = NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120,
            rules: new GameRules(InstantBuild: true));
        f.UnlockAllForDemo();
        foreach (var item in f.State.Exchange.Items)
        {
            item.AutoSell = false;
        }

        foreach (var (id, amount) in new[] { (Plate, 100), (Lead, 100), (Coal, 100), ("iron-ore", 100) })
        {
            f.Wallet.AddMaterial(id, amount);
        }

        return f;
    }

    private static string Json(Factory f) => JsonSerializer.Serialize(f.Capture(), FactorySaveStore.JsonOptions);

    private static ulong Checksum(Factory f) => Snapshot.Checksum(Snapshot.Write(f.State));

    /// <summary>Saves and loads through the snapshot, the way a save file does.</summary>
    private static Factory RoundTrip(Factory f) =>
        new Factory(Snapshot.Read(Content, Snapshot.Write(f.State)));

    private static void AssertSameAfterRoundTrip(Factory f)
    {
        var copy = RoundTrip(f);
        Assert.Equal(Checksum(f), Checksum(copy));
        Assert.Equal(Json(f), Json(copy));
        f.RunTicks(120);
        copy.RunTicks(120);
        Assert.Equal(Checksum(f), Checksum(copy));
    }

    private static void Line(Factory f, GridPosition start, Direction dir, int length)
    {
        for (var i = 0; i < length; i++)
        {
            Assert.True(f.TryPlaceBelt(start.Step(dir, i), dir), $"belt {start.Step(dir, i)} {dir}");
        }
    }

    [Fact]
    public void FullJunction_RoundTrips()
    {
        var f = NewFactory();
        var at = new GridPosition(20, 20);
        Assert.True(f.TryPlaceJunction(at, Direction.East));
        // Both lines feed it, nothing takes from it: the queues fill and the items keep waiting.
        Line(f, new GridPosition(10, 20), Direction.East, 10);
        Line(f, new GridPosition(20, 30), Direction.North, 10);
        // Dead ends past the junction: they fill, then so do its queues.
        Line(f, new GridPosition(21, 20), Direction.East, 3);
        Line(f, new GridPosition(20, 19), Direction.North, 3);
        long id = 1000;
        foreach (var cell in f.Belts.Cells.Values.Where(c => c.Kind == LogisticsKind.Belt))
        {
            cell.RestoreState(new[] { 1f, 0.6f, 0.2f }.Select(p => new TransportedItem(id++, "iron-ore", p)));
        }

        f.RunTicks(400);

        var junction = f.Belts.Cells[at];
        Assert.True(junction.Items.Count >= 2, $"junction holds {junction.Items.Count}");
        Assert.Contains(junction.Items, i => i.JunctionTicks > 0);
        AssertSameAfterRoundTrip(f);
        var copy = RoundTrip(f);
        Assert.Equal(junction.Items.Select(i => (i.Id, i.JunctionTicks)),
            copy.Belts.Cells[at].Items.Select(i => (i.Id, i.JunctionTicks)));
    }

    [Fact]
    public void RouterMidTurn_RoundTrips()
    {
        var f = NewFactory();
        var at = new GridPosition(30, 30);
        Assert.True(f.TryPlaceRouter(at, Direction.East));
        Line(f, new GridPosition(26, 30), Direction.East, 4);
        // Exits that fill up: the router ends up holding an item and waiting.
        Line(f, new GridPosition(30, 31), Direction.South, 2);
        Line(f, new GridPosition(30, 29), Direction.North, 2);
        Line(f, new GridPosition(31, 30), Direction.East, 2);
        long id = 1000;
        foreach (var cell in f.Belts.Cells.Values.Where(c => c.Kind == LogisticsKind.Belt))
        {
            cell.RestoreState(new[] { 1f, 0.6f, 0.2f }.Select(p => new TransportedItem(id++, "iron-ore", p)));
        }

        var router = f.Belts.Cells[at];
        var seenTurns = new HashSet<int>();
        for (var t = 0; t < 600 && (router.Items.Count == 0 || router.RouterWaitTicks == 0); t++)
        {
            f.RunTicks(1);
            seenTurns.Add(router.RouterTurn);
        }

        Assert.NotEmpty(router.Items);
        Assert.True(router.RouterWaitTicks > 0);
        Assert.NotNull(router.RouterFrom);
        AssertSameAfterRoundTrip(f);
        var copy = RoundTrip(f);
        var back = copy.Belts.Cells[at];
        Assert.Equal((router.RouterTurn, router.RouterWaitTicks, router.RouterFrom), (back.RouterTurn, back.RouterWaitTicks, back.RouterFrom));
        Assert.Equal(router.Items.Select(i => i.Id), back.Items.Select(i => i.Id));
    }

    [Fact]
    public void SortersWithoutFilter_StayWithoutFilter()
    {
        var f = NewFactory();
        var sorter = new GridPosition(30, 30);
        var inverted = new GridPosition(40, 30);
        Assert.True(f.TryPlaceSorter(sorter, Direction.East));
        Assert.True(f.TryPlaceSorterInverted(inverted, Direction.East));
        Line(f, new GridPosition(26, 30), Direction.East, 4);
        Line(f, new GridPosition(30, 31), Direction.South, 6);
        Line(f, new GridPosition(30, 29), Direction.North, 6);
        Line(f, new GridPosition(36, 30), Direction.East, 4);
        Line(f, new GridPosition(40, 31), Direction.South, 6);
        Line(f, new GridPosition(40, 29), Direction.North, 6);
        Line(f, new GridPosition(41, 30), Direction.East, 6);
        long id = 1000;
        // 9 items on the sorter's line (odd: its turn bit ends flipped) and 6 on the inverted one's.
        foreach (var cell in f.Belts.Cells.Values.Where(c => c.Position is { Y: 30, X: >= 27 and < 30 } or { Y: 30, X: >= 36 and < 38 }))
        {
            cell.RestoreState(new[] { 1f, 0.6f, 0.2f }.Select(p => new TransportedItem(id++, "iron-ore", p)));
        }

        f.RunTicks(300);

        Assert.Null(f.Belts.Cells[sorter].FilterItemId);
        Assert.Null(f.Belts.Cells[inverted].FilterItemId);
        AssertSameAfterRoundTrip(f);
        var copy = RoundTrip(f);
        Assert.Null(copy.Belts.Cells[sorter].FilterItemId);
        Assert.Null(copy.Belts.Cells[inverted].FilterItemId);
        Assert.Equal(f.Belts.Cells[sorter].SorterTurns, copy.Belts.Cells[sorter].SorterTurns);
        Assert.Equal(f.Belts.Cells[inverted].SorterTurns, copy.Belts.Cells[inverted].SorterTurns);
        Assert.NotEqual(0, f.Belts.Cells[sorter].SorterTurns);
    }

    [Fact]
    public void ExtractorMidAccumulator_RoundTrips()
    {
        var f = NewFactory();
        // Unfiltered extractor east of the Core with two lines to alternate between.
        Assert.True(f.TryPlaceExtractor(new GridPosition(63, 61)));
        Line(f, new GridPosition(64, 61), Direction.East, 12);
        Line(f, new GridPosition(63, 62), Direction.South, 12);
        f.RunTicks(203);

        var ex = Assert.Single(f.Extractors);
        Assert.InRange(ex.Accumulator, 1, 59);
        Assert.NotNull(ex.LastItemId);
        Assert.True(ex.ItemsExtracted > 0);
        AssertSameAfterRoundTrip(f);
        var copy = Assert.Single(RoundTrip(f).Extractors);
        Assert.Equal(
            (ex.Accumulator, ex.SourceTurn, ex.DestinationTurn, ex.LastItemId, ex.ItemsExtracted, ex.FilterItemId),
            (copy.Accumulator, copy.SourceTurn, copy.DestinationTurn, copy.LastItemId, copy.ItemsExtracted, copy.FilterItemId));
        Assert.Null(copy.FilterItemId);
    }

    /// <summary>
    /// Extractors from the Core feed a main line (junction crossing a second line, a router pair, a sorter and an
    /// inverted sorter with side returns); a forno west of the Core is fed and emptied by two more extractors.
    /// </summary>
    private static Factory EveryBlockGame()
    {
        var f = NewFactory();
        void Place(bool ok, string what) => Assert.True(ok, what);
        void Row(int y, int x0, int x1, Direction dir)
        {
            var step = x0 <= x1 ? 1 : -1;
            for (var x = x0; x != x1 + step; x += step)
            {
                Place(f.TryPlaceBelt(new GridPosition(x, y), dir), $"belt {x},{y}");
            }
        }

        Place(f.TryPlaceExtractor(new GridPosition(63, 61)), "extractor 1");
        Place(f.TryPlaceExtractor(new GridPosition(63, 60)), "extractor 2");
        Row(61, 64, 69, Direction.East);
        Place(f.TryPlaceJunction(new GridPosition(70, 61), Direction.East), "junction");
        Row(61, 71, 73, Direction.East);
        Place(f.TryPlaceRouter(new GridPosition(74, 61), Direction.East), "router 1");
        Place(f.TryPlaceRouter(new GridPosition(74, 62), Direction.East), "router 2");
        // The crossing line ends in the junction (nothing takes from it: its queue stays full); a return along y 62 into the Core.
        Row(60, 64, 69, Direction.East);
        Place(f.TryPlaceBelt(new GridPosition(70, 60), Direction.South), "cross");
        Row(62, 68, 63, Direction.West);
        // Router 2 north exit: y 63 West with the sorters, their sides returning along y 62 and y 64.
        Row(63, 74, 69, Direction.West);
        Place(f.TryPlaceSorter(new GridPosition(68, 63), Direction.West, Coal), "sorter");
        Row(63, 67, 67, Direction.West);
        Place(f.TryPlaceSorterInverted(new GridPosition(66, 63), Direction.West, Plate), "inverted sorter");
        Row(63, 65, 62, Direction.West);
        Place(f.TryPlaceBelt(new GridPosition(61, 63), Direction.North), "return top");
        Row(64, 68, 62, Direction.West);
        Place(f.TryPlaceBelt(new GridPosition(61, 64), Direction.North), "return top 2");
        // Router 1 south exit back into the Core from below.
        Place(f.TryPlaceBelt(new GridPosition(74, 60), Direction.North), "south 1");
        Row(59, 74, 62, Direction.West);
        Place(f.TryPlaceBelt(new GridPosition(61, 59), Direction.South), "return bottom");
        // West of the Core: ore in, plates out of a forno, both by extractor.
        Place(f.TryPlaceSmelter(new GridPosition(57, 61), Direction.East), "forno");
        Factory.SeedSmelterFuel(f);
        Place(f.TryPlaceExtractor(new GridPosition(59, 61), "iron-ore"), "extractor ore");
        Place(f.TryPlaceExtractor(new GridPosition(59, 62), Plate), "extractor plates");
        return f;
    }

    [Fact]
    public void FiveMinutes_AllBlocks_Deterministic_AndRestorable()
    {
        var a = EveryBlockGame();
        var b = EveryBlockGame();
        Factory? restored = null;
        var junctionBusy = false;
        var routerTurns = new HashSet<int>();
        var sorterTurns = new HashSet<int>();
        var extractorSourceTurns = new HashSet<int>();
        var extractorDestinationTurns = new HashSet<int>();
        var delivered = a.State.CoreDeliveredItems;
        var checksums = new List<ulong>();
        for (var tick = 0; tick < 5 * Minute; tick++)
        {
            a.RunTicks(1);
            b.RunTicks(1);
            restored?.RunTicks(1);
            {
                junctionBusy |= a.Belts.Cells[new GridPosition(70, 61)].Items.Count > 0;
                routerTurns.Add(a.Belts.Cells[new GridPosition(74, 61)].RouterTurn);
                routerTurns.Add(a.Belts.Cells[new GridPosition(74, 62)].RouterTurn);
                sorterTurns.Add(a.Belts.Cells[new GridPosition(68, 63)].SorterTurns);
                sorterTurns.Add(a.Belts.Cells[new GridPosition(66, 63)].SorterTurns);
                foreach (var e in a.Extractors)
                {
                    extractorSourceTurns.Add(e.SourceTurn);
                    extractorDestinationTurns.Add(e.DestinationTurn);
                }
            }

            if ((tick + 1) % Minute == 0)
            {
                Assert.Equal(Checksum(a), Checksum(b));
                checksums.Add(Checksum(a));
                if (tick + 1 == 2 * Minute)
                {
                    restored = RoundTrip(a);
                }
            }
        }

        Assert.Equal(5, checksums.Distinct().Count());
        Assert.Equal(Checksum(a), Checksum(restored!));
        // The game really ran: items delivered, the forno crafted, turns moved.
        Assert.True(a.State.CoreDeliveredItems - delivered > 100, $"delivered {a.State.CoreDeliveredItems - delivered}");
        Assert.True(a.Smelters.Single().ItemsCrafted > 0, "forno crafted");
        Assert.True(junctionBusy, "junction carried items");
        Assert.True(routerTurns.Count > 1, "router turns changed");
        Assert.True(sorterTurns.Count > 1, "sorter turns changed");
        Assert.True(extractorSourceTurns.Count > 1 || extractorDestinationTurns.Count > 1, "extractor turns changed");
        Assert.All(a.Extractors, e => Assert.True(e.ItemsExtracted > 0));
    }
}
