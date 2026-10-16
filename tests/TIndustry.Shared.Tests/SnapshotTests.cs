using System.Diagnostics;
using System.Text;
using TIndustry.Shared;
using Xunit;
using Xunit.Abstractions;

namespace TIndustry.Shared.Tests;

/// <summary>Canonical snapshot bytes and their FNV-1a 64 checksum (basis for co-op desync detection).</summary>
public sealed class SnapshotTests(ITestOutputHelper output)
{
    private static readonly string ContentPath =
        Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    private static FactoryContent LoadContent() => FactoryContent.Load(ContentPath);

    /// <summary>FNV-1a 64 of UTF-8 "tIndustry", computed independently with a Python one-off.</summary>
    private const ulong TIndustryFnv1a64 = 0x6d127b9050ea8331UL;

    private static void RunTicks(SimHost host, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            Assert.Equal(1, host.Advance(SimulationClock.TickSeconds));
        }
    }

    /// <summary>Commands submitted at fixed ticks: belts, a sell, auto-sell, a removal.</summary>
    private static void SubmitScript(SimHost host)
    {
        long tick = 5;
        for (var x = 11; x <= 20; x++)
        {
            host.Submit(new PlaceBelt(1, new GridPosition(x, 8), Direction.East, "conveyor-basic") { Tick = tick++ });
        }

        host.Submit(new Sell(1, "iron-plate", 3) { Tick = 100 });
        host.Submit(new SetAutoSell(1, true) { Tick = 200 });
        host.Submit(new Remove(1, new GridPosition(15, 8)) { Tick = 400 });
        host.Submit(new PlaceBelt(1, new GridPosition(15, 8), Direction.East, "conveyor-basic") { Tick = 450 });
    }

    [Fact]
    public void ReplayGivesSameChecksum()
    {
        var content = LoadContent();
        ulong Run()
        {
            var host = new SimHost(NewGame.Demo(content));
            SubmitScript(host);
            RunTicks(host, 10_000);
            return host.Checksum();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void SnapshotRoundTripMidGame()
    {
        var content = LoadContent();
        var host1 = new SimHost(NewGame.Demo(content));
        SubmitScript(host1);
        RunTicks(host1, 3000);

        // Guard against a vacuous pass: the snapshot must be taken with real activity in it.
        Assert.True(host1.State.Belts.Values.Sum(c => c.Items.Count) > 0, "nessun oggetto in transito sui nastri");

        var host2 = SimHost.FromSnapshot(content, host1.CreateSnapshot());
        Assert.Equal(host1.Checksum(), host2.Checksum());

        RunTicks(host1, 3000);
        RunTicks(host2, 3000);

        Assert.Equal(host1.Checksum(), host2.Checksum());
        Assert.Equal(6000, host2.State.CurrentTick);
    }

    [Fact]
    public void SnapshotSurvivesJsonRoundTrip()
    {
        var content = LoadContent();
        var host = new SimHost(NewGame.Demo(content));
        SubmitScript(host);
        RunTicks(host, 1500);

        var state = ((Factory)host.State).State;
        var bytes = Snapshot.Write(state);
        var again = Snapshot.Write(Snapshot.Read(content, bytes));

        Assert.Equal(bytes, again);
    }

    [Fact]
    public void SnapshotIsCanonical()
    {
        var content = LoadContent();

        Factory Build(bool reverse)
        {
            var f = NewGame.Sandbox(content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120);
            f.UnlockAllForDemo();
            // Buildings in the SAME order in both: ids are part of the state by design.
            Assert.True(f.TryPlaceGenerator(new GridPosition(20, 20)));
            Assert.True(f.TryPlacePowerNode(new GridPosition(30, 20)));
            Assert.True(f.TryPlaceGenerator(new GridPosition(40, 20)));

            var belts = new List<GridPosition>();
            for (var x = 5; x < 15; x++)
            {
                belts.Add(new GridPosition(x, 40));
                belts.Add(new GridPosition(x, 43));
            }

            if (reverse)
            {
                belts.Reverse();
            }

            foreach (var p in belts)
            {
                Assert.True(f.TryPlaceBelt(p, Direction.East));
            }

            string[] materials = ["copper-ore", "coal", "iron-ore", "stone"];
            foreach (var m in reverse ? materials.Reverse() : materials)
            {
                f.Wallet.AddMaterial(m, 7);
            }

            return f;
        }

        var forward = Snapshot.Write(Build(reverse: false).State);
        var backward = Snapshot.Write(Build(reverse: true).State);

        Assert.Equal(forward, backward);
        Assert.Equal(Snapshot.Checksum(forward), Snapshot.Checksum(backward));
    }

    [Fact]
    public void ChecksumKnownVector() =>
        Assert.Equal(TIndustryFnv1a64, Snapshot.Checksum(Encoding.UTF8.GetBytes("tIndustry")));

    // Manual measurement, kept out of the default run (building 62k belts is slow). To run it:
    // remove the Skip argument (or use a temporary copy) and run
    // dotnet test tests/TIndustry.Shared.Tests -c Release --filter MeasureLargeMap --logger "console;verbosity=detailed"
    [Fact(Skip = "misura manuale: 1000x1000 con ~62k nastri, lenta; togli Skip per eseguirla")]
    public void MeasureLargeMap()
    {
        var content = LoadContent();
        var factory = NewGame.Sandbox(content, new GridPosition(500, 500), mapWidth: 1000, mapHeight: 1000);
        factory.UnlockAllForDemo();
        factory.Wallet.AddMaterial("iron-plate", 100_000_000);
        var belts = 0;
        for (var y = 0; y < 1000; y += 4)
        {
            for (var x = 0; x < 1000; x += 4)
            {
                if (factory.TryPlaceBelt(new GridPosition(x, y), Direction.East))
                {
                    belts++;
                }
            }
        }

        var host = new SimHost(factory);
        var sw = Stopwatch.StartNew();
        var snapshot = host.CreateSnapshot();
        var snapMs = sw.Elapsed.TotalMilliseconds;
        sw.Restart();
        var checksum = Snapshot.Checksum(snapshot);
        var sumMs = sw.Elapsed.TotalMilliseconds;
        output.WriteLine($"1000x1000, {belts} belts: snapshot {snapshot.Length} bytes, " +
            $"CreateSnapshot {snapMs:F1} ms, Checksum {sumMs:F1} ms, sum {checksum:X16}");
        Assert.NotEmpty(snapshot);
    }
}
