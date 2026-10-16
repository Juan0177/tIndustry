using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Player speed controls: <see cref="GameSpeed"/>, <see cref="TimeJump"/> and the paused <see cref="BuildQueue"/>.</summary>
public sealed class GameSpeedTests
{
    private static readonly string ContentPath =
        Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    private const int BeltTool = 1;

    private static Factory NewSandbox() =>
        Factory.CreateSandboxSlice(
            FactoryContent.Load(ContentPath),
            Factory.CenteredCoreOrigin(64, 64),
            coreSize: 2,
            mapWidth: 64,
            mapHeight: 64,
            seed: 42,
            startingMoney: 180);

    private static int RunFrames(GameSpeed speed, int frames)
    {
        var clock = new SimulationClock();
        var total = 0;
        for (var i = 0; i < frames; i++)
        {
            total += speed.TicksForFrame(clock, 1.0 / 60);
        }

        return total;
    }

    private static void EmptyWallet(Factory factory)
    {
        factory.Wallet.AddMoney(-factory.Wallet.Money);
        foreach (var (id, count) in factory.Wallet.MaterialsSnapshot())
        {
            factory.Wallet.TryRemoveMaterial(id, count);
        }
    }

    private static PlannedBuild BeltPlan(int x, int y) =>
        new(BeltTool, "conveyor-basic", new GridPosition(x, y), 1, Direction.East);

    [Fact]
    public void NormalSpeedRunsOneTickPerSixtiethOfASecond() =>
        Assert.Equal(60, RunFrames(new GameSpeed(), 60));

    [Fact]
    public void BarSpeedsAreNotClippedToTheNormalFrameCap()
    {
        foreach (var value in GameSpeed.BarSpeeds)
        {
            var speed = new GameSpeed();
            speed.SetSpeed(value);
            var expected = (int)value * 60;
            // Repeated subtraction of 1/60 can leave the last tick in the accumulator for the next frame.
            Assert.InRange(RunFrames(speed, 60), expected - 1, expected);
        }
    }

    [Fact]
    public void BarAndHotkeySpeedsAreReachable()
    {
        Assert.All(GameSpeed.BarSpeeds, s => Assert.Contains(s, GameSpeed.SpeedSteps));
        Assert.Equal(GameSpeed.MaxSpeed, GameSpeed.SpeedSteps[^1]);
        Assert.Equal(GameSpeed.MaxSpeed, GameSpeed.BarSpeeds[^1]);
    }

    [Fact]
    public void PauseRunsOnlyQueuedSteps()
    {
        var speed = new GameSpeed();
        var clock = new SimulationClock();
        speed.TogglePause();
        Assert.Equal(0, speed.TicksForFrame(clock, 1.0));

        speed.QueueSteps(3);
        Assert.Equal(3, speed.TicksForFrame(clock, 1.0 / 60));
        Assert.Equal(0, speed.TicksForFrame(clock, 1.0 / 60));
        Assert.True(speed.Paused);
    }

    [Fact]
    public void SpeedZeroPausesAndSpeedIsClamped()
    {
        var speed = new GameSpeed();
        speed.SetSpeed(0);
        Assert.True(speed.Paused);

        speed.SetSpeed(1000);
        Assert.False(speed.Paused);
        Assert.Equal(GameSpeed.MaxSpeed, speed.Speed);
    }

    [Fact]
    public void SpeedHotkeysWalkTheSteps()
    {
        var speed = new GameSpeed();
        speed.StepSpeed(1);
        Assert.Equal(2f, speed.Speed);
        speed.StepSpeed(-1);
        speed.StepSpeed(-1);
        Assert.Equal(0.5f, speed.Speed);
        for (var i = 0; i < 20; i++)
        {
            speed.StepSpeed(1);
        }

        Assert.Equal(GameSpeed.MaxSpeed, speed.Speed);
    }

    [Fact]
    public void TimeJumpRunsInSlicesUntilComplete()
    {
        var factory = NewSandbox();
        var jump = TimeJump.FromSeconds(10);
        Assert.Equal(600, jump.TotalTicks);

        jump.Advance(factory, 250);
        Assert.Equal(250, factory.CurrentTick);
        Assert.False(jump.IsComplete);
        Assert.Equal(250 / 600.0, jump.Progress, 6);

        jump.Advance(factory, 1000);
        Assert.True(jump.IsComplete);
        Assert.Equal(600, factory.CurrentTick);

        jump.Advance(factory, 1000);
        Assert.Equal(600, factory.CurrentTick);
    }

    [Fact]
    public void TimeJumpWithBudgetFinishesAShortJump()
    {
        var factory = NewSandbox();
        var jump = TimeJump.FromSeconds(5);
        jump.Advance(factory, TimeSpan.FromSeconds(5));

        Assert.True(jump.IsComplete);
        Assert.Equal(300, factory.CurrentTick);
    }

    [Fact]
    public void TimeJumpIsCappedAtSixHours() =>
        Assert.Equal((long)TimeJump.MaxSeconds * SimulationClock.TickRate, TimeJump.FromSeconds(99_999).TotalTicks);

    [Fact]
    public void GameTimeFormatting()
    {
        Assert.Equal("00:00", TimeJump.FormatGameTime(0));
        Assert.Equal("01:30", TimeJump.FormatGameTime(90 * 60));
        Assert.Equal("1:00:05", TimeJump.FormatGameTime(3605 * 60));
    }

    [Fact]
    public void PlansRejectTakenCellsAndOverlaps()
    {
        var factory = NewSandbox();
        var queue = new BuildQueue();
        var core = factory.CoreTiles.First();

        Assert.False(queue.TryAdd(BeltPlan(core.X, core.Y), factory));
        Assert.True(queue.TryAdd(BeltPlan(5, 5), factory));
        Assert.False(queue.TryAdd(BeltPlan(5, 5), factory));
        Assert.False(queue.TryAdd(new PlannedBuild(BeltTool, "smelter", new GridPosition(4, 4), 2, Direction.East), factory));
        Assert.True(queue.IsPlanned(new GridPosition(5, 5)));

        Assert.True(queue.RemoveAt(new GridPosition(5, 5)));
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void PlansWaitForResourcesThenBuildInOrder()
    {
        var factory = NewSandbox();
        var queue = new BuildQueue();
        Assert.True(queue.TryAdd(BeltPlan(5, 5), factory));
        Assert.True(queue.TryAdd(BeltPlan(6, 5), factory));
        EmptyWallet(factory);

        var order = new List<GridPosition>();
        bool Place(PlannedBuild plan)
        {
            order.Add(plan.Origin);
            return factory.TryPlaceBelt(plan.Origin, plan.Direction);
        }

        Assert.Equal(new BuildQueueResult(0, 2, 0), queue.Process(factory, Place));
        Assert.Equal(2, queue.Count);

        factory.Wallet.AddMaterial("iron-plate", 1);
        Assert.Equal(new BuildQueueResult(1, 1, 0), queue.Process(factory, Place));

        factory.Wallet.AddMaterial("iron-plate", 1);
        Assert.Equal(new BuildQueueResult(1, 0, 0), queue.Process(factory, Place));
        Assert.Equal([new GridPosition(5, 5), new GridPosition(6, 5)], order);
        Assert.True(factory.Belts.Contains(new GridPosition(6, 5)));
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void PlansAreDroppedWhenTheirCellsGetTaken()
    {
        var factory = NewSandbox();
        var queue = new BuildQueue();
        Assert.True(queue.TryAdd(BeltPlan(5, 5), factory));
        Assert.True(factory.TryPlaceBelt(new GridPosition(5, 5), Direction.North));

        var result = queue.Process(factory, _ => throw new InvalidOperationException("must not build"));

        Assert.Equal(new BuildQueueResult(0, 0, 1), result);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void PlansTheFactoryRejectsAreDropped()
    {
        var factory = NewSandbox();
        var queue = new BuildQueue();
        Assert.True(queue.TryAdd(BeltPlan(5, 5), factory));

        Assert.Equal(new BuildQueueResult(0, 0, 1), queue.Process(factory, _ => false));
        Assert.Equal(0, queue.Count);
    }
}
