using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Redirects <see cref="FactorySaveStore"/> to a temp dir for the whole run.</summary>
public sealed class TempSaveStoreFixture : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"tindustry-tests-{Guid.NewGuid():N}");

    public TempSaveStoreFixture() => FactorySaveStore.SavesDirectoryOverride = _dir;

    public void Dispose()
    {
        FactorySaveStore.SavesDirectoryOverride = null;
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SaveStoreCollection : ICollectionFixture<TempSaveStoreFixture>
{
    public const string Name = "SaveStore";
}
