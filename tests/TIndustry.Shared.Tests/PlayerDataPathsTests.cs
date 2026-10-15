using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Own collection so it never runs while another test has set <c>SavesDirectoryOverride</c>.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PlayerDataPathsCollection
{
    public const string Name = "PlayerDataPaths";
}

[Collection(PlayerDataPathsCollection.Name)]
public sealed class PlayerDataPathsTests
{
    [Fact]
    public void PlayerDataPathsAreUnchanged()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.Equal(Path.Combine(local, "tIndustry"), GameDataPaths.Root);
        Assert.Equal(Path.Combine(local, "tIndustry", "godot-saves"), FactorySaveStore.SavesDirectory);
        Assert.Equal("continua", FactorySaveStore.ContinueSlotId);
    }
}
