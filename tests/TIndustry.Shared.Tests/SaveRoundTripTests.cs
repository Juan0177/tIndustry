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
        var factory = NewGame.Sandbox(content, new GridPosition(9, 14), seed: 7);
        Assert.True(factory.TryPlaceMiner(new GridPosition(2, 7), Direction.East));
        Assert.True(factory.TryPlaceBelt(new GridPosition(4, 8), Direction.East));

        FactorySaveStore.Save("roundtrip", new SimHost(factory));
        Assert.True(FactorySaveStore.TryLoad(content, "roundtrip", out var host, out _));
        var reloaded = (Factory)host!.State;

        Assert.Equal(factory.Miners.Count, reloaded.Miners.Count);
        Assert.Equal(factory.Belts.Count, reloaded.Belts.Count);
        Assert.Equal(factory.Wallet.Money, reloaded.Wallet.Money);
        Assert.NotEmpty(reloaded.Miners);
    }

    [Fact]
    public void OldSaveVersionIsRejectedWithClearMessage()
    {
        var content = FactoryContent.Load(ContentPath);
        var factory = NewGame.Sandbox(content, new GridPosition(9, 14), seed: 7);
        FactorySaveStore.Save("vecchio", new SimHost(factory));
        RewriteVersion("vecchio", FactorySaveData.MinReadableVersion - 1);

        Assert.False(FactorySaveStore.TryLoad(content, "vecchio", out _, out var error));
        Assert.Equal("Salvataggio di una versione precedente: non compatibile.", error);
        Assert.False(FactorySaveStore.TryLoadSlot(content, "vecchio", out _, out var failure));
        Assert.Equal(SaveLoadError.OlderVersion, failure!.Code);
    }

    [Fact]
    public void FutureSaveVersionIsRejected()
    {
        var content = FactoryContent.Load(ContentPath);
        var factory = NewGame.Sandbox(content, new GridPosition(9, 14), seed: 7);
        FactorySaveStore.Save("futuro", new SimHost(factory));
        RewriteVersion("futuro", FactorySaveData.CurrentVersion + 1);

        Assert.False(FactorySaveStore.TryLoad(content, "futuro", out _, out var error));
        // Technical version text must not reach the player.
        Assert.Equal("Salvataggio non leggibile.", error);
    }

    [Fact]
    public void TryLoadReportsTheErrorMessage()
    {
        var content = FactoryContent.Load(ContentPath);
        var factory = NewGame.Sandbox(content, new GridPosition(9, 14), seed: 7);
        FactorySaveStore.Save("vecchio2", new SimHost(factory));
        RewriteVersion("vecchio2", FactorySaveData.MinReadableVersion - 1);

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
