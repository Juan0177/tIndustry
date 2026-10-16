using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Dev console (<c>TINDUSTRY_DEV=1</c>): speed and jump commands, cheats, completion.</summary>
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
    public void SpeedCommandsDriveTheSharedGameSpeed()
    {
        var speed = new GameSpeed();
        var dev = new DevConsole(speed);
        var factory = NewSandbox();
        dev.Execute("velocita 0", factory);
        Assert.True(speed.Paused);

        dev.Execute("vel 1000", factory);
        Assert.False(speed.Paused);
        Assert.Equal(GameSpeed.MaxSpeed, speed.Speed);

        dev.Execute("speed 0,5", factory);
        Assert.Equal(0.5f, speed.Speed);

        dev.Execute("pausa", factory);
        Assert.True(speed.Paused);
        dev.Execute("passo 3", factory);
        Assert.Equal(3, speed.TicksForFrame(new SimulationClock(), 1.0));
    }

    [Fact]
    public void FastForwardAsksForATimeJump()
    {
        var dev = new DevConsole(new GameSpeed());
        var factory = NewSandbox();

        Assert.Equal(120, dev.Execute("avanti 120", factory).JumpSeconds);
        Assert.Equal(0, factory.CurrentTick);
        Assert.Null(dev.Execute($"avanti {TimeJump.MaxSeconds + 1}", factory).JumpSeconds);
        Assert.Null(dev.Execute("avanti -5", factory).JumpSeconds);
        Assert.Null(dev.Execute("avanti tanto", factory).JumpSeconds);
    }

    [Fact]
    public void MoneyAndItemsAreAdded()
    {
        var dev = new DevConsole(new GameSpeed());
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
        var dev = new DevConsole(new GameSpeed());
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
        var dev = new DevConsole(new GameSpeed());
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
        var dev = new DevConsole(new GameSpeed());
        var factory = NewSandbox();
        Assert.False(factory.IsStructureUnlocked("smelter"));

        dev.Execute("sblocca smelter", factory);

        Assert.True(factory.IsStructureUnlocked("smelter"));
    }

    [Fact]
    public void PowerFillsTheBuffer()
    {
        var dev = new DevConsole(new GameSpeed());
        var factory = NewSandbox();
        factory.SetPowerBuffer(0);

        dev.Execute("energia", factory);

        Assert.Equal(factory.PowerCapacity, factory.PowerBuffer);
    }

    [Fact]
    public void GoToAndWinReportClientActions()
    {
        var dev = new DevConsole(new GameSpeed());
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
        var dev = new DevConsole(new GameSpeed());
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

}
