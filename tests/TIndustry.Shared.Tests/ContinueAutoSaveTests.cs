using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

[Collection(SaveStoreCollection.Name)]
public sealed class ContinueAutoSaveTests
{
    private static readonly string ContentPath =
        Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    private static Factory NewFactory(int money)
    {
        var content = FactoryContent.Load(ContentPath);
        return NewGame.Sandbox(content, new GridPosition(9, 14), seed: 7, startingMoney: money);
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

        Assert.False(autoSave.TrySave(NewFactory(180)));
        Assert.False(FactorySaveStore.Exists(FactorySaveStore.ContinueSlotId));
    }

    [Fact]
    public void ExistingContinueSurvivesQuitWithoutPlaying()
    {
        ClearContinue();
        FactorySaveStore.Save(FactorySaveStore.ContinueSlotId, NewFactory(999).Capture());
        var before = File.ReadAllText(FactorySaveStore.SlotPath(FactorySaveStore.ContinueSlotId));

        // A failed load (e.g. an incompatible save) does not count as playing.
        var autoSave = new ContinueAutoSave();
        Assert.False(autoSave.TrySave(NewFactory(180)));

        Assert.Equal(before, File.ReadAllText(FactorySaveStore.SlotPath(FactorySaveStore.ContinueSlotId)));
    }

    [Fact]
    public void SavesOnceTheSessionWasPlayed()
    {
        ClearContinue();
        var autoSave = new ContinueAutoSave();
        autoSave.MarkPlayed();

        Assert.True(autoSave.TrySave(NewFactory(321)));
        Assert.Equal(321, FactorySaveStore.Load(FactorySaveStore.ContinueSlotId).Money);
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
