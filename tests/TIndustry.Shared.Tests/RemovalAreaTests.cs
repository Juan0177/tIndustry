using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>The right drag's removal rectangle (<see cref="RemovalArea"/>), shared by the client and the agents.</summary>
public sealed class RemovalAreaTests
{
    private const int Player = 0;

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly GridPosition MinerAt = new(30, 30);
    private static readonly GridPosition Planned = new(37, 31);
    private static readonly GridPosition[] Belts = [new(33, 30), new(34, 30), new(35, 30)];

    /// <summary>A 2x2 miner and three belts standing, plus one belt plan (not started: the drone is far away).</summary>
    private static (Factory Factory, SimHost Host) NewScene()
    {
        var wallet = new EconomyWallet(0, new Dictionary<string, int>(StringComparer.Ordinal) { ["iron-ore"] = 500 });
        var factory = new Factory(Content, new BeltGrid(), CoreStockSink.MakeCoreTiles(new GridPosition(2, 2), 2), wallet);
        factory.State.Rules = new GameRules(InstantBuild: false);
        factory.AddBuilder(Player);
        Assert.True(factory.PlaceInstant(new BuildSpec(Miner.BasicId, BuildingKind.Miner, MinerAt, Direction.East), charge: false));
        foreach (var belt in Belts)
        {
            Assert.True(factory.PlaceInstant(new BuildSpec("conveyor-basic", null, belt, Direction.East), charge: false));
        }

        var host = new SimHost(factory);
        host.Submit(new PlaceBelt(Player, Planned, Direction.East, "conveyor-basic"));
        host.AdvanceTicks(1);
        Assert.Contains(factory.PlansOf(Player), p => !p.Breaking && p.Spec.Origin == Planned);
        return (factory, host);
    }

    private static void Send(SimHost host, IEnumerable<Remove> commands)
    {
        foreach (var command in commands)
        {
            host.Submit(command);
        }

        host.AdvanceTicks(1);
    }

    [Fact]
    public void OneRemovePerBlockInsideTheRectangle()
    {
        var (factory, _) = NewScene();

        var commands = RemovalArea.Commands(factory, Player, new GridPosition(38, 32), new GridPosition(29, 29));

        Assert.Equal(
            [MinerAt, .. Belts, Planned],
            commands.Select(c => c.Tile).ToArray());
        Assert.All(commands, c => Assert.True(c.OnlyMark));
    }

    [Fact]
    public void PlansInsideAreCancelledAndBlocksMarked()
    {
        var (factory, host) = NewScene();

        Send(host, RemovalArea.Commands(factory, Player, new GridPosition(29, 29), new GridPosition(38, 32)));

        var plans = factory.PlansOf(Player);
        Assert.All(plans, p => Assert.True(p.Breaking));
        Assert.Equal([MinerAt, .. Belts], plans.Select(p => p.Spec.Origin).ToArray());
    }

    [Fact]
    public void EmptyCellsSendNothing()
    {
        var (factory, _) = NewScene();

        Assert.Empty(RemovalArea.Commands(factory, Player, new GridPosition(40, 40), new GridPosition(50, 45)));
        // A rectangle that only grazes the miner's last cell still takes it.
        Assert.Equal(new GridPosition(31, 31),
            Assert.Single(RemovalArea.Commands(factory, Player, new GridPosition(31, 31), new GridPosition(32, 31))).Tile);
    }

    [Fact]
    public void RectangleIsClampedTo64()
    {
        Assert.Equal((new GridPosition(0, 0), new GridPosition(63, 63)), RemovalArea.Rect(new GridPosition(0, 0), new GridPosition(100, 100)));
        Assert.Equal((new GridPosition(37, 90), new GridPosition(100, 100)), RemovalArea.Rect(new GridPosition(100, 100), new GridPosition(0, 90)));
    }

    /// <summary>A plan over a standing block (here a tier change) is two things: the plan is cancelled, then the block marked.</summary>
    [Fact]
    public void APlanOverABlockGetsTwoRemovesOnItsCell()
    {
        var (factory, host) = NewScene();
        factory.Research.ForceUnlock("conveyor-fast");
        var belt = Belts[1];
        host.Submit(new PlaceBelt(Player, belt, Direction.East, "conveyor-fast"));
        host.AdvanceTicks(1);
        Assert.Contains(factory.PlansOf(Player), p => !p.Breaking && p.Spec.Origin == belt);

        var commands = RemovalArea.Commands(factory, Player, belt, belt);
        Assert.Equal([belt, belt], commands.Select(c => c.Tile).ToArray());

        Send(host, commands);
        Assert.DoesNotContain(factory.PlansOf(Player), p => !p.Breaking && p.Spec.Origin == belt);
        Assert.Contains(factory.PlansOf(Player), p => p.Breaking && p.Spec.Origin == belt);
    }

    /// <summary>A site under construction gets one Remove (not one more for its plan); once taken apart it gets none.</summary>
    [Fact]
    public void ASiteIsCancelledOnceAndSkippedWhileTakenApart()
    {
        var (factory, host) = NewScene();
        // Halfway built, so the cancelled site is still there being taken apart a tick later.
        for (var tick = 0; tick < 20_000 && !(factory.TryGetSiteAt(Planned, out var s) && s.Progress > 0.5f); tick++)
        {
            host.AdvanceTicks(1);
        }

        Assert.True(factory.TryGetSiteAt(Planned, out var site) && !site.Deconstructing);
        var commands = RemovalArea.Commands(factory, Player, Planned, Planned);
        Assert.Equal(Planned, Assert.Single(commands).Tile);

        Send(host, commands);
        Assert.True(factory.TryGetSiteAt(Planned, out var cancelled) && cancelled.Deconstructing);
        Assert.Empty(RemovalArea.Commands(factory, Player, Planned, Planned));
    }

    [Fact]
    public void ARectangleNeverUnmarks()
    {
        var (factory, host) = NewScene();
        Send(host, RemovalArea.Commands(factory, Player, new GridPosition(29, 29), new GridPosition(38, 32)));
        var marked = factory.PlansOf(Player).ToList();

        // An overlapping second rectangle: the marked blocks stay marked.
        Send(host, RemovalArea.Commands(factory, Player, new GridPosition(31, 30), new GridPosition(36, 30)));

        Assert.Equal(marked, factory.PlansOf(Player));
    }
}
