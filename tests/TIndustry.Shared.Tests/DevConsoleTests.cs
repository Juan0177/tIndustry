using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Dev console (<c>TINDUSTRY_DEV=1</c>): speed, pause, single steps and cheat commands.</summary>
public sealed class DevConsoleTests
{
    private static readonly string ContentPath =
        Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    private static Factory NewSandbox() =>
        Factory.CreateSandboxSlice(
            FactoryContent.Load(ContentPath),
            Factory.CenteredCoreOrigin(64, 64),
            coreSize: 2,
            mapWidth: 64,
            mapHeight: 64,
            seed: 42,
            startingMoney: 180);

    [Fact]
    public void NormalSpeedRunsOneTickPerSixtiethOfASecond()
    {
        var dev = new DevConsole();
        var clock = new SimulationClock();
        var total = 0;
        for (var i = 0; i < 60; i++)
        {
            total += dev.TicksForFrame(clock, 1.0 / 60);
        }

        Assert.Equal(60, total);
    }

    [Fact]
    public void HighSpeedIsNotClippedToTheNormalFrameCap()
    {
        var dev = new DevConsole();
        dev.SetSpeed(16);
        var clock = new SimulationClock();
        var total = 0;
        for (var i = 0; i < 60; i++)
        {
            total += dev.TicksForFrame(clock, 1.0 / 60);
        }

        Assert.Equal(16 * 60, total);
    }

    [Fact]
    public void PauseRunsOnlyQueuedSteps()
    {
        var dev = new DevConsole();
        var clock = new SimulationClock();
        dev.TogglePause();
        Assert.Equal(0, dev.TicksForFrame(clock, 1.0));

        dev.QueueSteps(3);
        Assert.Equal(3, dev.TicksForFrame(clock, 1.0 / 60));
        Assert.Equal(0, dev.TicksForFrame(clock, 1.0 / 60));
        Assert.True(dev.Paused);
    }

    [Fact]
    public void SpeedZeroPausesAndSpeedIsClamped()
    {
        var dev = new DevConsole();
        var factory = NewSandbox();
        dev.Execute("velocita 0", factory);
        Assert.True(dev.Paused);

        dev.Execute("vel 1000", factory);
        Assert.False(dev.Paused);
        Assert.Equal(DevConsole.MaxSpeed, dev.Speed);

        dev.Execute("speed 0,5", factory);
        Assert.Equal(0.5f, dev.Speed);
    }

    [Fact]
    public void SpeedHotkeysWalkTheSteps()
    {
        var dev = new DevConsole();
        dev.StepSpeed(1);
        Assert.Equal(2f, dev.Speed);
        dev.StepSpeed(-1);
        dev.StepSpeed(-1);
        Assert.Equal(0.5f, dev.Speed);
        for (var i = 0; i < 20; i++)
        {
            dev.StepSpeed(1);
        }

        Assert.Equal(DevConsole.SpeedSteps[^1], dev.Speed);
    }

    [Fact]
    public void FastForwardAdvancesTheSimulation()
    {
        var dev = new DevConsole();
        var factory = NewSandbox();
        var result = dev.Execute("avanti 10", factory);

        Assert.Equal(600, factory.CurrentTick);
        Assert.True(result.WorldChanged);
    }

    [Fact]
    public void FastForwardRejectsOutOfRange()
    {
        var dev = new DevConsole();
        var factory = NewSandbox();
        dev.Execute($"avanti {DevConsole.MaxFastForwardSeconds + 1}", factory);
        dev.Execute("avanti -5", factory);
        dev.Execute("avanti tanto", factory);

        Assert.Equal(0, factory.CurrentTick);
    }

    [Fact]
    public void MoneyAndItemsAreAdded()
    {
        var dev = new DevConsole();
        var factory = NewSandbox();
        var money = factory.Wallet.Money;
        var plates = factory.Wallet.MaterialCount("iron-plate");
        var coal = factory.Wallet.MaterialCount("coal");

        dev.Execute("soldi 500", factory);
        dev.Execute("dai iron-plate 25", factory);
        dev.Execute("dai coal", factory);

        Assert.Equal(money + 500, factory.Wallet.Money);
        Assert.Equal(plates + 25, factory.Wallet.MaterialCount("iron-plate"));
        Assert.Equal(coal + DevConsole.DefaultGiveAmount, factory.Wallet.MaterialCount("coal"));
    }

    [Fact]
    public void GiveAllCoversEveryKnownItemAndRejectsUnknown()
    {
        var dev = new DevConsole();
        var factory = NewSandbox();
        dev.Execute("dai tutto 7", factory);
        foreach (var id in DevConsole.KnownItemIds(factory.Content))
        {
            Assert.True(factory.Wallet.MaterialCount(id) >= 7, id);
        }

        var result = dev.Execute("dai unobtainium", factory);
        Assert.False(result.WorldChanged);
        Assert.Contains(result.Lines, l => l.Contains("sconosciuto"));
    }

    [Fact]
    public void UnlockAllUnlocksEveryStructure()
    {
        var dev = new DevConsole();
        var factory = NewSandbox();
        Assert.Contains(factory.Content.Structures, s => !factory.IsStructureUnlocked(s.Id));

        var result = dev.Execute("sblocca tutto", factory);

        Assert.True(result.ResearchChanged);
        Assert.All(factory.Content.Structures, s => Assert.True(factory.IsStructureUnlocked(s.Id), s.Id));
        Assert.False(dev.Execute("sblocca tutto", factory).ResearchChanged);
    }

    [Fact]
    public void UnlockSingleStructure()
    {
        var dev = new DevConsole();
        var factory = NewSandbox();
        Assert.False(factory.IsStructureUnlocked("smelter"));

        dev.Execute("sblocca smelter", factory);

        Assert.True(factory.IsStructureUnlocked("smelter"));
    }

    [Fact]
    public void PowerFillsTheBuffer()
    {
        var dev = new DevConsole();
        var factory = NewSandbox();
        factory.SetPowerBuffer(0);

        dev.Execute("energia", factory);

        Assert.Equal(factory.PowerCapacity, factory.PowerBuffer);
    }

    [Fact]
    public void GoToAndWinReportClientActions()
    {
        var dev = new DevConsole();
        var factory = NewSandbox();

        Assert.Equal(new GridPosition(10, 20), dev.Execute("vai 10 20", factory).CameraTarget);
        Assert.True(dev.Execute("vai core", factory).CameraToCore);
        Assert.False(dev.Execute("vinci", factory).CompleteLevel);

        factory.ActiveCampaignLevelId = "level-1";
        Assert.True(dev.Execute("vinci", factory).CompleteLevel);
    }

    [Fact]
    public void UnknownCommandAndHelpPrintSomething()
    {
        var dev = new DevConsole();
        var factory = NewSandbox();

        Assert.Contains("sconosciuto", dev.Execute("boh", factory).Lines[0]);
        Assert.True(dev.Execute("aiuto", factory).Lines.Count > 5);
        Assert.Empty(dev.Execute("   ", factory).Lines);
    }

    [Fact]
    public void CompletionMatchesCommandPrefixes()
    {
        Assert.Equal(["sblocca"], DevConsole.Complete("sb"));
        Assert.Equal(["pausa", "passo", "pulisci"], DevConsole.Complete("p"));
        Assert.Empty(DevConsole.Complete("zz"));
    }

    [Fact]
    public void StartupCommandsSplitOnSemicolons()
    {
        Assert.Equal(["sblocca tutto", "soldi 10000"], DevConsole.SplitCommands(" sblocca tutto ;; soldi 10000; "));
        Assert.Empty(DevConsole.SplitCommands(null));
    }

    [Fact]
    public void GameTimeFormatting()
    {
        Assert.Equal("00:00", DevConsole.FormatGameTime(0));
        Assert.Equal("01:30", DevConsole.FormatGameTime(90 * 60));
        Assert.Equal("1:00:05", DevConsole.FormatGameTime(3605 * 60));
    }
}
