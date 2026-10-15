using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

[Collection(SaveStoreCollection.Name)]
public sealed class SaveRoundTripTests
{
    private static readonly string ContentPath =
        Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    [Fact]
    public void NewGameSavesAndReloads()
    {
        var content = FactoryContent.Load(ContentPath);
        var slice = NewGame.Sandbox(content, new GridPosition(9, 14), seed: 7);
        Assert.True(slice.TryPlaceMiner(new GridPosition(2, 7), Direction.East));
        Assert.True(slice.TryPlaceBelt(new GridPosition(4, 8), Direction.East));

        FactorySaveStore.Save("roundtrip", new SimHost(slice));
        Assert.True(FactorySaveStore.TryLoad(content, "roundtrip", out var host, out _));
        var reloaded = (Factory)host!.State;

        Assert.Equal(slice.Miners.Count, reloaded.Miners.Count);
        Assert.Equal(slice.Belts.Count, reloaded.Belts.Count);
        Assert.Equal(slice.Wallet.Money, reloaded.Wallet.Money);
        Assert.NotEmpty(reloaded.Miners);
    }

    [Fact]
    public void OldSaveVersionIsRejectedWithClearMessage()
    {
        var content = FactoryContent.Load(ContentPath);
        var slice = NewGame.Sandbox(content, new GridPosition(9, 14), seed: 7);
        FactorySaveStore.Save("vecchio", new SimHost(slice));
        RewriteVersion("vecchio", FactorySaveData.CurrentVersion - 1);

        Assert.False(FactorySaveStore.TryLoad(content, "vecchio", out _, out var error));
        Assert.Equal("Salvataggio di una versione precedente: non compatibile.", error);
    }

    [Fact]
    public void FutureSaveVersionIsRejected()
    {
        var content = FactoryContent.Load(ContentPath);
        var slice = NewGame.Sandbox(content, new GridPosition(9, 14), seed: 7);
        FactorySaveStore.Save("futuro", new SimHost(slice));
        RewriteVersion("futuro", FactorySaveData.CurrentVersion + 1);

        Assert.False(FactorySaveStore.TryLoad(content, "futuro", out _, out var error));
        Assert.Equal($"Versione save non supportata: {FactorySaveData.CurrentVersion + 1}", error);
    }

    [Fact]
    public void TryLoadReportsTheErrorMessage()
    {
        var content = FactoryContent.Load(ContentPath);
        var slice = NewGame.Sandbox(content, new GridPosition(9, 14), seed: 7);
        FactorySaveStore.Save("vecchio2", new SimHost(slice));
        RewriteVersion("vecchio2", FactorySaveData.CurrentVersion - 1);

        Assert.False(FactorySaveStore.TryLoad(content, "vecchio2", out var host, out var error));
        Assert.Null(host);
        Assert.Equal("Salvataggio di una versione precedente: non compatibile.", error);
    }

    [Fact]
    public void CorruptSaveReportsUnreadableMessage()
    {
        Directory.CreateDirectory(FactorySaveStore.SavesDirectory);
        File.WriteAllText(FactorySaveStore.SlotPath("rotto"), "{ not json");

        Assert.False(FactorySaveStore.TryLoad(LoadContent(), "rotto", out var host, out var error));
        Assert.Null(host);
        Assert.Equal("Salvataggio non leggibile.", error);
    }

    [Fact]
    public void MissingSaveHasNoErrorMessage()
    {
        Assert.False(FactorySaveStore.TryLoad(LoadContent(), "inesistente", out _, out var error));
        Assert.Null(error);
    }

    private static FactoryContent LoadContent() => FactoryContent.Load(ContentPath);

    private static void RewriteVersion(string slotId, int version)
    {
        var path = FactorySaveStore.SlotPath(slotId);
        var json = File.ReadAllText(path);
        var current = $"\"version\": {FactorySaveData.CurrentVersion}";
        Assert.Contains(current, json);
        File.WriteAllText(path, json.Replace(current, $"\"version\": {version}"));
    }
}
