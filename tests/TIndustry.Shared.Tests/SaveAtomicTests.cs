using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Slots are written through a temp file and renamed, so a crash never leaves a truncated slot.</summary>
[Collection(SaveStoreCollection.Name)]
public sealed class SaveAtomicTests
{
    private static readonly string ContentPath =
        Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    [Fact]
    public void SaveOverExistingSlotLeavesReadableFileAndNoTmp()
    {
        var content = FactoryContent.Load(ContentPath);
        var host = new SimHost(NewGame.Demo(content));
        FactorySaveStore.Save("atomico", host);
        host.Advance(SimulationClock.TickSeconds * 3);
        FactorySaveStore.Save("atomico", host);

        Assert.True(FactorySaveStore.TryLoad(content, "atomico", out var loaded, out var error), error);
        Assert.Equal(host.State.CurrentTick, loaded!.State.CurrentTick);
        Assert.Empty(Directory.GetFiles(FactorySaveStore.SavesDirectory, "*.tmp"));
    }

    [Fact]
    public void LeftoverTmpIsNotListedAsSlot()
    {
        var content = FactoryContent.Load(ContentPath);
        FactorySaveStore.Save("con-tmp", new SimHost(NewGame.Demo(content)));
        var real = FactorySaveStore.SlotPath("con-tmp");
        File.Copy(real, real + ".tmp", overwrite: true);
        File.WriteAllText(Path.Combine(FactorySaveStore.SavesDirectory, "orfano.json.tmp"), "{ troncato");

        var ids = FactorySaveStore.ListSlots().Select(s => s.Id).ToList();

        Assert.Contains("con-tmp", ids);
        Assert.DoesNotContain("orfano", ids);
        Assert.DoesNotContain("orfano.json", ids);
        Assert.Single(ids, id => id.StartsWith("con-tmp", StringComparison.Ordinal));
    }

    [Fact]
    public void DuplicateSlotLeavesNoTmp()
    {
        var content = FactoryContent.Load(ContentPath);
        FactorySaveStore.Save("sorgente", new SimHost(NewGame.Demo(content)));

        var id = FactorySaveStore.DuplicateSlot("sorgente");

        Assert.NotNull(id);
        Assert.True(FactorySaveStore.TryLoad(content, id!, out _, out var error), error);
        Assert.Empty(Directory.GetFiles(FactorySaveStore.SavesDirectory, "*.tmp"));
    }

    [Fact]
    public void FailedWriteLeavesPreviousSlotIntact()
    {
        var content = FactoryContent.Load(ContentPath);
        var host = new SimHost(NewGame.Demo(content));
        FactorySaveStore.Save("intatto", host);
        var path = FactorySaveStore.SlotPath("intatto");
        var before = File.ReadAllBytes(path);
        host.Advance(SimulationClock.TickSeconds * 3);

        // A directory squatting on the temp name makes the write fail midway: the slot must survive.
        Directory.CreateDirectory(path + ".tmp");
        try
        {
            Assert.ThrowsAny<Exception>(() => FactorySaveStore.Save("intatto", host));
            Assert.Equal(before, File.ReadAllBytes(path));
        }
        finally
        {
            Directory.Delete(path + ".tmp");
        }
    }
}
