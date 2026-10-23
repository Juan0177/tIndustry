using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Save format v15: metadata + canonical snapshot, written and read through <see cref="SimHost"/>; older versions are refused.</summary>
[Collection(SaveStoreCollection.Name)]
public sealed class SaveVersionTests
{
    private static readonly string ContentPath =
        Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    private static FactoryContent LoadContent() => FactoryContent.Load(ContentPath);

    private static readonly string Current = $"\"version\": {FactorySaveData.CurrentVersion}";

    private static void RunTicks(SimHost host, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            Assert.Equal(1, host.Advance(SimulationClock.TickSeconds));
        }
    }

    [Fact]
    public void NewGameSaveLoadKeepsChecksum()
    {
        var content = LoadContent();
        var host = new SimHost(NewGame.Sandbox(
            content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7,
            rules: new GameRules(InstantBuild: true)));
        host.Submit(new PlaceBuilding(1, BuildingKind.Miner, new GridPosition(2, 7), Direction.East));
        for (var x = 4; x <= 12; x++)
        {
            host.Submit(new PlaceBelt(1, new GridPosition(x, 8), Direction.East, "conveyor-basic"));
        }

        RunTicks(host, 600);
        var before = host.Checksum();

        FactorySaveStore.Save("v11-checksum", host);
        Assert.True(FactorySaveStore.TryLoad(content, "v11-checksum", out var loaded, out var error), error);

        Assert.NotNull(loaded);
        Assert.Null(error);
        Assert.Equal(before, loaded!.Checksum());
        Assert.Equal(600, loaded.State.CurrentTick);
        Assert.NotEmpty(loaded.State.Miners);
    }

    [Fact]
    public void Version9SaveIsRejectedWithClearMessage()
    {
        var content = LoadContent();
        var host = new SimHost(NewGame.Demo(content));
        FactorySaveStore.Save("v9-rifiutato", host);
        var path = FactorySaveStore.SlotPath("v9-rifiutato");
        var json = File.ReadAllText(path);
        Assert.Contains(Current, json);
        File.WriteAllText(path, json.Replace(Current, "\"version\": 9"));

        Assert.False(FactorySaveStore.TryLoad(content, "v9-rifiutato", out var loaded, out var error));
        Assert.Null(loaded);
        Assert.Equal("Salvataggio di una versione precedente: non compatibile.", error);
    }

    [Fact]
    public void Version10SaveIsRejectedWithClearMessage()
    {
        var content = LoadContent();
        var host = new SimHost(NewGame.Demo(content));
        FactorySaveStore.Save("v10-rifiutato", host);
        var path = FactorySaveStore.SlotPath("v10-rifiutato");
        var json = File.ReadAllText(path);
        File.WriteAllText(path, json.Replace(Current, "\"version\": 10"));

        Assert.False(FactorySaveStore.TryLoad(content, "v10-rifiutato", out var loaded, out var error));
        Assert.Null(loaded);
        Assert.Equal("Salvataggio di una versione precedente: non compatibile.", error);
    }

    [Fact]
    public void SaveWithPendingCommandsKeepsThemForNextTick()
    {
        var content = LoadContent();
        var host = new SimHost(NewGame.Demo(content));
        RunTicks(host, 10);
        var beltsBefore = host.State.Belts.Count;
        var cell = new GridPosition(30, 30);

        host.Submit(new PlaceBelt(1, cell, Direction.East, "conveyor-basic"));
        FactorySaveStore.Save("v11-pendente", host);
        Assert.True(FactorySaveStore.TryLoad(content, "v11-pendente", out var loaded, out var error), error);

        // A save holds the applied state only: the queued command is not in it.
        Assert.Equal(beltsBefore, loaded!.State.Belts.Count);
        Assert.False(loaded.State.Belts.ContainsKey(cell));

        // The original host still applies it on the next tick.
        RunTicks(host, 1);
        Assert.True(host.State.Belts.ContainsKey(cell));
        Assert.Equal(beltsBefore + 1, host.State.Belts.Count);
    }

    [Fact]
    public void SlotListShowsMetadataWithoutLoadingTheSnapshot()
    {
        var content = LoadContent();
        var host = new SimHost(NewGame.Sandbox(
            content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 100, seed: 11, startingMoney: 333));
        FactorySaveStore.Save("v11-elenco", host);

        var slot = Assert.Single(FactorySaveStore.ListSlots(), s => s.Id == "v11-elenco");
        Assert.Equal(333, slot.Money);
        Assert.Equal(11, slot.Seed);
        Assert.Equal(120, slot.MapWidth);
        Assert.Equal(100, slot.MapHeight);
    }

    [Fact]
    public void FutureVersionSaveIsUnreadableWithoutTechnicalText()
    {
        var content = LoadContent();
        var host = new SimHost(NewGame.Demo(content));
        FactorySaveStore.Save("v11-futuro", host);
        var path = FactorySaveStore.SlotPath("v11-futuro");
        var json = File.ReadAllText(path);
        File.WriteAllText(path, json.Replace(Current, $"\"version\": {FactorySaveData.CurrentVersion + 1}"));

        Assert.False(FactorySaveStore.TryLoad(content, "v11-futuro", out var loaded, out var error));
        Assert.Null(loaded);
        Assert.Equal("Salvataggio non leggibile.", error);
    }

    [Fact]
    public void SaveWritesVersion15()
    {
        Assert.Equal(15, FactorySaveData.CurrentVersion);
        Assert.Equal(15, FactorySaveData.MinReadableVersion);
        var host = new SimHost(NewGame.Sandbox(LoadContent(), new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 3));
        FactorySaveStore.Save("v15-versione", host);

        Assert.Contains("\"version\": 15", File.ReadAllText(FactorySaveStore.SlotPath("v15-versione")));
        var snapshot = System.Text.Encoding.UTF8.GetString(host.CreateSnapshot());
        Assert.Contains("\"version\":15", snapshot);
        Assert.Contains("\"coreStartX\":60,\"coreStartY\":60", snapshot);
        Assert.DoesNotContain("coreUpgradeLevel", snapshot, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("autoSellAtCore", snapshot, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("autoSellCarryMilli", snapshot, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"autoSellCarry\":", snapshot);
    }

    [Fact]
    public void Version14_Refused_OlderVersion()
    {
        var content = LoadContent();
        var host = new SimHost(NewGame.Demo(content));
        FactorySaveStore.Save("v14-rifiutato", host);
        var path = FactorySaveStore.SlotPath("v14-rifiutato");
        var json = File.ReadAllText(path);
        Assert.Contains(Current, json);
        File.WriteAllText(path, json.Replace(Current, "\"version\": 14"));

        Assert.False(FactorySaveStore.TryLoadSlot(content, "v14-rifiutato", out var loaded, out var failure));
        Assert.Null(loaded);
        Assert.Equal(SaveLoadError.OlderVersion, failure!.Code);

        // A v14 snapshot inside a v15 envelope is not read either.
        var snapshot = System.Text.Encoding.UTF8.GetString(host.CreateSnapshot())
            .Replace($"\"version\":{FactorySaveData.CurrentVersion}", "\"version\":14");
        Assert.Throws<InvalidDataException>(() => Snapshot.Read(content, System.Text.Encoding.UTF8.GetBytes(snapshot)));
    }

    [Fact]
    public void CoreSizeNotOfItsTier_Unreadable()
    {
        var content = LoadContent();
        var factory = NewGame.Sandbox(content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 3);
        var data = factory.Capture();
        Assert.Equal((0, content.CoreTiers[0].Size), (data.CoreTier, data.CoreSize));
        data.CoreSize = 2;
        Assert.Throws<InvalidDataException>(() => Factory.Restore(content, data));

        data.CoreSize = content.CoreTiers[0].Size;
        data.CoreTier = 1;
        Assert.Throws<InvalidDataException>(() => Factory.Restore(content, data));

        // Through the save store: the player sees "unreadable".
        data.CoreTier = 0;
        data.CoreSize = 4;
        var snapshot = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(data, FactorySaveStore.JsonOptions);
        Directory.CreateDirectory(FactorySaveStore.SavesDirectory);
        File.WriteAllText(FactorySaveStore.SlotPath("v15-nucleo-storto"), System.Text.Json.JsonSerializer.Serialize(
            new SaveFileData { Snapshot = Convert.ToBase64String(snapshot) }, FactorySaveStore.JsonOptions));
        Assert.False(FactorySaveStore.TryLoadSlot(content, "v15-nucleo-storto", out _, out var failure));
        Assert.Equal(SaveLoadError.Unreadable, failure!.Code);
    }
}
