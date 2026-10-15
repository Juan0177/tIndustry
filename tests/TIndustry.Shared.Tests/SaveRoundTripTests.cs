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
        var slice = Factory.CreateSandboxSlice(content, new GridPosition(9, 14), seed: 7);
        Assert.True(slice.TryPlaceMiner(new GridPosition(2, 7), Direction.East));
        Assert.True(slice.TryPlaceBelt(new GridPosition(4, 8), Direction.East));

        FactorySaveStore.Save("roundtrip", slice.Capture());
        var reloaded = Factory.Restore(content, FactorySaveStore.Load("roundtrip"));

        Assert.Equal(slice.Miners.Count, reloaded.Miners.Count);
        Assert.Equal(slice.Belts.Count, reloaded.Belts.Count);
        Assert.Equal(slice.Wallet.Money, reloaded.Wallet.Money);
        Assert.NotEmpty(reloaded.Miners);
    }

    [Fact]
    public void OldSaveVersionIsRejectedWithClearMessage()
    {
        var content = FactoryContent.Load(ContentPath);
        var slice = Factory.CreateSandboxSlice(content, new GridPosition(9, 14), seed: 7);
        FactorySaveStore.Save("vecchio", slice.Capture());
        RewriteVersion("vecchio", FactorySaveData.CurrentVersion - 1);

        var ex = Assert.Throws<InvalidDataException>(() => FactorySaveStore.Load("vecchio"));
        Assert.Equal("Salvataggio di una versione precedente: non compatibile.", ex.Message);
    }

    [Fact]
    public void FutureSaveVersionIsRejected()
    {
        var content = FactoryContent.Load(ContentPath);
        var slice = Factory.CreateSandboxSlice(content, new GridPosition(9, 14), seed: 7);
        FactorySaveStore.Save("futuro", slice.Capture());
        RewriteVersion("futuro", FactorySaveData.CurrentVersion + 1);

        var ex = Assert.Throws<InvalidDataException>(() => FactorySaveStore.Load("futuro"));
        Assert.Equal($"Versione save non supportata: {FactorySaveData.CurrentVersion + 1}", ex.Message);
    }

    [Fact]
    public void TryLoadReportsTheErrorMessage()
    {
        var content = FactoryContent.Load(ContentPath);
        var slice = Factory.CreateSandboxSlice(content, new GridPosition(9, 14), seed: 7);
        FactorySaveStore.Save("vecchio2", slice.Capture());
        RewriteVersion("vecchio2", FactorySaveData.CurrentVersion - 1);

        Assert.False(FactorySaveStore.TryLoad("vecchio2", out var data, out var error));
        Assert.Null(data);
        Assert.Equal("Salvataggio di una versione precedente: non compatibile.", error);
    }

    [Fact]
    public void CorruptSaveReportsUnreadableMessage()
    {
        Directory.CreateDirectory(FactorySaveStore.SavesDirectory);
        File.WriteAllText(FactorySaveStore.SlotPath("rotto"), "{ not json");

        Assert.False(FactorySaveStore.TryLoad("rotto", out var data, out var error));
        Assert.Null(data);
        Assert.Equal("Salvataggio non leggibile.", error);
    }

    [Fact]
    public void MissingSaveHasNoErrorMessage()
    {
        Assert.False(FactorySaveStore.TryLoad("inesistente", out _, out var error));
        Assert.Null(error);
    }

    private static void RewriteVersion(string slotId, int version)
    {
        var path = FactorySaveStore.SlotPath(slotId);
        var json = File.ReadAllText(path);
        var current = $"\"version\": {FactorySaveData.CurrentVersion}";
        Assert.Contains(current, json);
        File.WriteAllText(path, json.Replace(current, $"\"version\": {version}"));
    }
}
