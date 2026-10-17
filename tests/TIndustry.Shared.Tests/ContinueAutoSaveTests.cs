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
        foreach (var path in new[]
                 {
                     FactorySaveStore.SlotPath(FactorySaveStore.ContinueSlotId), FactorySaveStore.ContinueBackupPath
                 })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static string ContinueText() =>
        File.ReadAllText(FactorySaveStore.SlotPath(FactorySaveStore.ContinueSlotId));

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

    [Fact]
    public void FirstAutosaveKeepsThePreviousContinueAsBackup()
    {
        ClearContinue();
        FactorySaveStore.Save(FactorySaveStore.ContinueSlotId, NewHost(999));
        var players = ContinueText();

        var autoSave = new ContinueAutoSave();
        autoSave.MarkPlayed();
        Assert.True(autoSave.TrySave(NewHost(111)));

        Assert.Equal(players, File.ReadAllText(FactorySaveStore.ContinueBackupPath));
        Assert.NotEqual(players, ContinueText());
    }

    [Fact]
    public void LaterAutosavesInTheSameSessionKeepTheSessionStartBackup()
    {
        // New game → Home (autosave) → Continua → quit (autosave): the backup must still hold
        // the Continua the session started with, not the game it wrote a minute earlier.
        ClearContinue();
        FactorySaveStore.Save(FactorySaveStore.ContinueSlotId, NewHost(999));
        var players = ContinueText();

        var autoSave = new ContinueAutoSave();
        autoSave.MarkPlayed();
        Assert.True(autoSave.TrySave(NewHost(111)));
        Assert.True(autoSave.TrySave(NewHost(222)));

        Assert.Equal(players, File.ReadAllText(FactorySaveStore.ContinueBackupPath));
    }

    [Fact]
    public void NoBackupWithoutAPreviousContinue()
    {
        ClearContinue();
        var autoSave = new ContinueAutoSave();
        autoSave.MarkPlayed();

        Assert.True(autoSave.TrySave(NewHost(111)));
        Assert.False(File.Exists(FactorySaveStore.ContinueBackupPath));
    }

    [Fact]
    public void BackupSitsBesideContinue()
    {
        Assert.Equal(
            Path.Combine(FactorySaveStore.SavesDirectory, "continua.prev.json"),
            FactorySaveStore.ContinueBackupPath);
    }
}
