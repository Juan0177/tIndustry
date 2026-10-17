using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

[Collection(SaveStoreCollection.Name)]
public sealed class ContinueAutoSaveTests
{
    private static readonly string ContentPath =
        Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    private static SimHost NewHost(int money)
    {
        var content = FactoryContent.Load(ContentPath);
        return new SimHost(NewGame.Sandbox(content, new GridPosition(9, 14), seed: 7, startingMoney: money));
    }

    private static void ClearContinue()
    {
        var path = FactorySaveStore.SlotPath(FactorySaveStore.ContinueSlotId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void StartupSandboxIsNotWrittenToContinue()
    {
        ClearContinue();
        var autoSave = new ContinueAutoSave();

        Assert.False(autoSave.TrySave(NewHost(180)));
        Assert.False(FactorySaveStore.Exists(FactorySaveStore.ContinueSlotId));
    }

    [Fact]
    public void ExistingContinueSurvivesQuitWithoutPlaying()
    {
        ClearContinue();
        FactorySaveStore.Save(FactorySaveStore.ContinueSlotId, NewHost(999));
        var before = File.ReadAllText(FactorySaveStore.SlotPath(FactorySaveStore.ContinueSlotId));

        // A failed load (e.g. an incompatible save) does not count as playing.
        var autoSave = new ContinueAutoSave();
        Assert.False(autoSave.TrySave(NewHost(180)));

        Assert.Equal(before, File.ReadAllText(FactorySaveStore.SlotPath(FactorySaveStore.ContinueSlotId)));
    }

    [Fact]
    public void SavesOnceTheSessionWasPlayed()
    {
        ClearContinue();
        var autoSave = new ContinueAutoSave();
        autoSave.MarkPlayed();

        Assert.True(autoSave.TrySave(NewHost(321)));
        var saved = Assert.Single(FactorySaveStore.ListSlots(), s => s.Id == FactorySaveStore.ContinueSlotId);
        Assert.Equal(321, saved.Money);
        Assert.True(FactorySaveStore.TryLoad(
            FactoryContent.Load(ContentPath), FactorySaveStore.ContinueSlotId, out var loaded, out _));
        Assert.Equal(321, loaded!.State.Wallet.Money);
    }

    [Fact]
    public void NothingToSaveWithoutAGame()
    {
        ClearContinue();
        var autoSave = new ContinueAutoSave();
        autoSave.MarkPlayed();

        Assert.False(autoSave.TrySave(null));
        Assert.False(FactorySaveStore.Exists(FactorySaveStore.ContinueSlotId));
    }
}
