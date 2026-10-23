using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Day and night: the world's time starts at the first block placed (<see cref="GameState.FirstBuildTick"/>); phase,
/// light and the time to the next change are pure functions of the rule, that tick and the current tick
/// (<see cref="DayNightClock"/>); in always-night the simulation raises <see cref="FirstAttackDue"/> once.
/// </summary>
public sealed class DayNightTests
{
    private const int Player = 0;
    private const long First = 1_000;

    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static Factory Sandbox(GameRules rules)
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7, rules: rules);
        foreach (var id in ResearchState.DemoStructureIds)
        {
            factory.Research.ForceUnlock(id);
        }

        return factory;
    }

    private static PlaceBelt Belt(GridPosition tile) => new(Player, tile, Direction.East, "conveyor-basic");

    // The shard covers (60..62, 60..62): cells to its east are free on the classic terrain at seed 7.
    private static readonly GridPosition FreeCell = new(66, 61);

    [Theory]
    // t from the first block: phase, light, ticks to the next change.
    [InlineData(0, DayNightPhase.Day, 1000, 36_000)]
    [InlineData(34_799, DayNightPhase.Day, 1000, 1_201)]
    [InlineData(34_800, DayNightPhase.Dusk, 1000, 1_200)]
    [InlineData(35_400, DayNightPhase.Dusk, 500, 600)]
    [InlineData(36_000, DayNightPhase.Night, 0, 14_400)]
    [InlineData(49_199, DayNightPhase.Night, 0, 1_201)]
    [InlineData(49_200, DayNightPhase.Dawn, 0, 1_200)]
    [InlineData(49_800, DayNightPhase.Dawn, 500, 600)]
    [InlineData(50_400, DayNightPhase.Day, 1000, 36_000)]
    [InlineData(86_400, DayNightPhase.Night, 0, 14_400)]
    public void CyclePhasesAndLight(long t, DayNightPhase phase, int light, long toNext)
    {
        var tick = First + t;
        Assert.Equal(phase, DayNightClock.PhaseAt(DayNightMode.Cycle, First, tick));
        Assert.Equal(light, DayNightClock.LightAt(DayNightMode.Cycle, First, tick));
        Assert.Equal(toNext, DayNightClock.TicksToNextChange(DayNightMode.Cycle, First, tick));
        Assert.Equal(phase == DayNightPhase.Night, DayNightClock.IsNight(DayNightMode.Cycle, First, tick));
    }

    [Fact]
    public void ConstantsMatchTheSpecification()
    {
        Assert.Equal(50_400, DayNightClock.CycleTicks);
        Assert.Equal(34_800, DayNightClock.DuskStart);
        Assert.Equal(36_000, DayNightClock.NightStart);
        Assert.Equal(49_200, DayNightClock.DawnStart);
        Assert.Equal(1_200, DayNightClock.FadeTicks);
        Assert.Equal(36_000, DayNightClock.FirstAttackTicks);
        Assert.Equal(1000, DayNightClock.FullLight);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(40_000L)]
    [InlineData(1_000_000L)]
    public void BeforeTheFirstBlockItIsDayWithNoCount(long tick)
    {
        Assert.Equal(DayNightPhase.Day, DayNightClock.PhaseAt(DayNightMode.Cycle, null, tick));
        Assert.Equal(1000, DayNightClock.LightAt(DayNightMode.Cycle, null, tick));
        Assert.Null(DayNightClock.TicksToNextChange(DayNightMode.Cycle, null, tick));
        Assert.False(DayNightClock.IsNight(DayNightMode.Cycle, null, tick));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(35_400L)]
    [InlineData(40_000L)]
    [InlineData(1_000_000L)]
    public void AlwaysDayIsAlwaysFullDay(long t)
    {
        foreach (long? first in new long?[] { null, First })
        {
            var tick = First + t;
            Assert.Equal(DayNightPhase.Day, DayNightClock.PhaseAt(DayNightMode.AlwaysDay, first, tick));
            Assert.Equal(1000, DayNightClock.LightAt(DayNightMode.AlwaysDay, first, tick));
            Assert.Null(DayNightClock.TicksToNextChange(DayNightMode.AlwaysDay, first, tick));
            Assert.False(DayNightClock.IsNight(DayNightMode.AlwaysDay, first, tick));
        }
    }

    [Theory]
    [InlineData(0L, 36_000L)]
    [InlineData(1L, 35_999L)]
    [InlineData(35_400L, 600L)]
    [InlineData(35_999L, 1L)]
    [InlineData(36_000L, null)]
    [InlineData(50_400L, null)]
    [InlineData(1_000_000L, null)]
    public void AlwaysNightCountsToTheFirstAttack(long t, long? toNext)
    {
        var tick = First + t;
        Assert.Equal(DayNightPhase.Night, DayNightClock.PhaseAt(DayNightMode.AlwaysNight, First, tick));
        Assert.Equal(0, DayNightClock.LightAt(DayNightMode.AlwaysNight, First, tick));
        Assert.Equal(toNext, DayNightClock.TicksToNextChange(DayNightMode.AlwaysNight, First, tick));
        Assert.True(DayNightClock.IsNight(DayNightMode.AlwaysNight, First, tick));
    }

    [Fact]
    public void AlwaysNightBeforeTheFirstBlockIsNightWithNoCount()
    {
        Assert.Equal(DayNightPhase.Night, DayNightClock.PhaseAt(DayNightMode.AlwaysNight, null, 500));
        Assert.Equal(0, DayNightClock.LightAt(DayNightMode.AlwaysNight, null, 500));
        Assert.Null(DayNightClock.TicksToNextChange(DayNightMode.AlwaysNight, null, 500));
    }

    [Fact]
    public void DefaultRuleIsTheCycle()
    {
        Assert.Equal(DayNightMode.Cycle, new GameRules().DayNight);
        Assert.Equal(DayNightMode.Cycle, Sandbox(new GameRules(InstantBuild: true)).State.Rules.DayNight);
        Assert.Equal(
            DayNightMode.AlwaysNight,
            Sandbox(new GameRules(InstantBuild: true, DayNight: DayNightMode.AlwaysNight)).State.Rules.DayNight);
    }

    [Fact]
    public void FirstBeltStartsTheTime()
    {
        var factory = Sandbox(new GameRules(InstantBuild: true));
        var host = new SimHost(factory);
        host.AdvanceTicks(50);
        Assert.Null(factory.State.FirstBuildTick);
        Assert.Null(factory.FirstBuildTick);
        Assert.Null(factory.TicksToNextChange);

        var applyTick = factory.CurrentTick;
        host.Submit(Belt(FreeCell));
        host.AdvanceTicks(1);

        Assert.Equal(1, factory.Belts.Count);
        Assert.Equal(applyTick, factory.State.FirstBuildTick);
        Assert.Equal(applyTick, factory.FirstBuildTick);
        Assert.Equal(DayNightClock.NightStart - 1, factory.TicksToNextChange);
        Assert.Equal(DayNightPhase.Day, factory.DayNightPhase);
        Assert.Equal(1000, factory.Light);
        Assert.Equal(DayNightMode.Cycle, factory.DayNight);

        // A second block does not move it.
        host.AdvanceTicks(30);
        host.Submit(Belt(new GridPosition(FreeCell.X + 1, FreeCell.Y)));
        host.AdvanceTicks(1);
        Assert.Equal(2, factory.Belts.Count);
        Assert.Equal(applyTick, factory.State.FirstBuildTick);
    }

    [Fact]
    public void FirstBuildingWithRealConstructionStartsTheTimeWhenTheSiteOpens()
    {
        var factory = Sandbox(new GameRules());
        var host = new SimHost(factory);
        host.Submit(new PlaceBuilding(Player, BuildingKind.Junction, FreeCell, Direction.East));
        host.AdvanceTicks(1);

        // The plan alone does not start the time: the site does.
        long ticks = 1;
        while (factory.Sites.Count == 0)
        {
            Assert.Null(factory.State.FirstBuildTick);
            host.AdvanceTicks(1);
            Assert.True(++ticks < 5_000, "il cantiere non si apre");
        }

        // The site opened during the tick that just ran (CurrentTick was one less while it ran).
        Assert.Equal(factory.CurrentTick - 1, factory.State.FirstBuildTick);
    }

    [Fact]
    public void RefusedCommandDoesNotStartTheTime()
    {
        var factory = Sandbox(new GameRules(InstantBuild: true));
        var host = new SimHost(factory);
        var refused = 0;
        host.CommandRejected += (_, _) => refused++;

        // On the Core: refused.
        host.Submit(Belt(new GridPosition(61, 61)));
        // Unknown belt: invalid.
        host.Submit(new PlaceBelt(Player, FreeCell, Direction.East, "no-such-belt"));
        host.AdvanceTicks(1);

        Assert.Equal(2, refused);
        Assert.Null(factory.State.FirstBuildTick);

        // Same without instant build (the plan is refused, no site ever opens).
        var real = Sandbox(new GameRules());
        var realHost = new SimHost(real);
        realHost.CommandRejected += (_, _) => refused++;
        realHost.Submit(Belt(new GridPosition(61, 61)));
        realHost.AdvanceTicks(200);
        Assert.Equal(3, refused);
        Assert.Null(real.State.FirstBuildTick);
    }

    [Fact]
    public void FirstAttackIsRaisedOnceInAlwaysNightEvenAtTwentyTimes()
    {
        var attacks = new List<(FirstAttackDue Event, long Tick)>();
        var factory = Sandbox(new GameRules(InstantBuild: true, DayNight: DayNightMode.AlwaysNight));
        var host = new SimHost(factory);
        host.EventRaised += e =>
        {
            if (e is FirstAttackDue due)
            {
                attacks.Add((due, factory.CurrentTick));
            }
        };

        host.AdvanceTicks(10);
        host.Submit(Belt(FreeCell));
        host.AdvanceTicks(37);
        var first = factory.State.FirstBuildTick!.Value;
        Assert.False(factory.State.FirstAttackRaised);

        host.Speed = 20;
        var frames = 0;
        // 1/12 s × 20 = 100 ticks per frame: the attack tick falls inside a frame, never on its edge.
        while (factory.CurrentTick < first + DayNightClock.FirstAttackTicks + 5_000)
        {
            Assert.InRange(host.Advance(1.0 / 12.0), 99, 100);
            frames++;
        }

        var (attack, raisedAfterTick) = Assert.Single(attacks);
        Assert.Equal(0, attack.PlayerId);
        // Raised by the tick at t = 36 000 (CurrentTick is one more once that tick has run).
        Assert.Equal(first + DayNightClock.FirstAttackTicks + 1, raisedAfterTick);
        Assert.True(factory.State.FirstAttackRaised);
        Assert.Null(factory.TicksToNextChange);
        Assert.Equal(DayNightPhase.Night, factory.DayNightPhase);
        Assert.Equal(0, factory.Light);
        Assert.True(frames > 300);
    }

    [Theory]
    [InlineData(DayNightMode.Cycle)]
    [InlineData(DayNightMode.AlwaysDay)]
    public void FirstAttackIsNeverRaisedOutsideAlwaysNight(DayNightMode mode)
    {
        var raised = 0;
        var factory = Sandbox(new GameRules(InstantBuild: true, DayNight: mode));
        var host = new SimHost(factory);
        host.EventRaised += e => raised += e is FirstAttackDue ? 1 : 0;
        host.Submit(Belt(FreeCell));
        host.AdvanceTicks((int)(DayNightClock.CycleTicks + DayNightClock.NightStart + 10));

        Assert.Equal(0, raised);
        Assert.False(factory.State.FirstAttackRaised);
    }

    [Fact]
    public void ViewFollowsTheCycle()
    {
        var factory = Sandbox(new GameRules(InstantBuild: true));
        var host = new SimHost(factory);
        host.Submit(Belt(FreeCell));
        host.AdvanceTicks(1);
        var first = factory.FirstBuildTick!.Value;

        host.AdvanceTicks((int)(first + 35_400 - factory.CurrentTick));
        Assert.Equal(DayNightPhase.Dusk, factory.DayNightPhase);
        Assert.Equal(500, factory.Light);
        Assert.Equal(600, factory.TicksToNextChange);

        host.AdvanceTicks(600);
        Assert.Equal(DayNightPhase.Night, factory.DayNightPhase);
        Assert.Equal(0, factory.Light);
        Assert.Equal(14_400, factory.TicksToNextChange);
    }
}
