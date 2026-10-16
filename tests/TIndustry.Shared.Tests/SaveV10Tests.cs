using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Save format v10: metadata + canonical snapshot, written and read through <see cref="SimHost"/>.</summary>
[Collection(SaveStoreCollection.Name)]
public sealed class SaveV10Tests
{
    private static readonly string ContentPath =
        Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    private static FactoryContent LoadContent() => FactoryContent.Load(ContentPath);

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
            content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7));
        host.Submit(new PlaceBuilding(1, BuildingKind.Miner, new GridPosition(2, 7), Direction.East));
        for (var x = 4; x <= 12; x++)
        {
            host.Submit(new PlaceBelt(1, new GridPosition(x, 8), Direction.East, "conveyor-basic"));
        }

        RunTicks(host, 600);
        var before = host.Checksum();

        FactorySaveStore.Save("v10-checksum", host);
        Assert.True(FactorySaveStore.TryLoad(content, "v10-checksum", out var loaded, out var error), error);

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
        Assert.Contains("\"version\": 10", json);
        File.WriteAllText(path, json.Replace("\"version\": 10", "\"version\": 9"));

        Assert.False(FactorySaveStore.TryLoad(content, "v9-rifiutato", out var loaded, out var error));
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
        FactorySaveStore.Save("v10-pendente", host);
        Assert.True(FactorySaveStore.TryLoad(content, "v10-pendente", out var loaded, out var error), error);

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
        FactorySaveStore.Save("v10-elenco", host);

        var slot = Assert.Single(FactorySaveStore.ListSlots(), s => s.Id == "v10-elenco");
        Assert.Equal(333, slot.Money);
        Assert.Equal(11, slot.Seed);
        Assert.Equal(120, slot.MapWidth);
        Assert.Equal(100, slot.MapHeight);
    }
}
