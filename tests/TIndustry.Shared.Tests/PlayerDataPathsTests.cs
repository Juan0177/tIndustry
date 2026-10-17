using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Own collection so it never runs while another test has set <c>SavesDirectoryOverride</c>,
/// and never in parallel with anything else: these tests change process environment variables.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PlayerDataPathsCollection
{
    public const string Name = "PlayerDataPaths";
}

/// <summary>Sets environment variables for one test and restores the previous values.</summary>
internal sealed class EnvScope : IDisposable
{
    private readonly Dictionary<string, string?> _previous = new(StringComparer.Ordinal);

    public EnvScope Set(string name, string? value)
    {
        if (!_previous.ContainsKey(name))
        {
            _previous[name] = Environment.GetEnvironmentVariable(name);
        }

        Environment.SetEnvironmentVariable(name, value);
        return this;
    }

    public void Dispose()
    {
        foreach (var (name, value) in _previous)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }
}

[Collection(PlayerDataPathsCollection.Name)]
public sealed class PlayerDataPathsTests : IDisposable
{
    private static readonly string ContentPath =
        Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    private readonly string _profile = Path.Combine(Path.GetTempPath(), $"tindustry-profile-{Guid.NewGuid():N}");
    private readonly string _outside = Path.Combine(Path.GetTempPath(), $"tindustry-outside-{Guid.NewGuid():N}");

    public void Dispose()
    {
        foreach (var dir in new[] { _profile, _outside })
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private static SimHost NewHost() =>
        new(NewGame.Sandbox(FactoryContent.Load(ContentPath), new GridPosition(9, 14), seed: 7, startingMoney: 50));

    private static bool IsInside(string root, string path)
    {
        var full = Path.GetFullPath(path);
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlayerDataPathsAreUnchanged()
    {
        using var env = new EnvScope().Set(GameDataPaths.DataDirEnv, null);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.Equal(Path.Combine(local, "tIndustry"), GameDataPaths.Root);
        Assert.Equal(Path.Combine(local, "tIndustry", "godot-saves"), FactorySaveStore.SavesDirectory);
        Assert.Equal("continua", FactorySaveStore.ContinueSlotId);
    }

    [Theory]
    [InlineData("continua")]
    [InlineData("slot-1")]
    [InlineData("")]
    [InlineData("..")]
    [InlineData("../../evil")]
    [InlineData(@"..\..\evil")]
    [InlineData(@"C:\Windows\evil")]
    [InlineData("/etc/evil")]
    public void DataDirOverrideKeepsEveryPathInsideIt(string slotId)
    {
        using var env = new EnvScope().Set(GameDataPaths.DataDirEnv, _profile);

        Assert.True(IsInside(_profile, Path.Combine(GameDataPaths.Root, "x")), GameDataPaths.Root);
        Assert.True(IsInside(_profile, FactorySaveStore.SavesDirectory), FactorySaveStore.SavesDirectory);
        Assert.True(IsInside(_profile, FactorySaveStore.SlotPath(slotId)), FactorySaveStore.SlotPath(slotId));
        Assert.True(IsInside(_profile, FactorySaveStore.ContinueBackupPath), FactorySaveStore.ContinueBackupPath);
        Assert.True(IsInside(_profile, CampaignProgress.ProgressPath), CampaignProgress.ProgressPath);
    }

    [Fact]
    public void DataDirOverrideIsNormalizedToAFullPath()
    {
        using var env = new EnvScope().Set(GameDataPaths.DataDirEnv, _profile + Path.DirectorySeparatorChar + ".");
        Assert.Equal(Path.GetFullPath(_profile), GameDataPaths.Root);
    }

    [Fact]
    public void OutsideAPlaytestAnyPathIsWritable()
    {
        using var env = new EnvScope().Set(GameDataPaths.PlaytestEnv, null).Set(GameDataPaths.DataDirEnv, null);
        GameDataPaths.EnsureWritable(Path.Combine(_outside, "continua.json"));
    }

    [Fact]
    public void PlaytestWritesInsideTheProfile()
    {
        using var env = new EnvScope().Set(GameDataPaths.PlaytestEnv, "ui").Set(GameDataPaths.DataDirEnv, _profile);
        Assert.Null(GameDataPaths.PlaytestProfileProblem());
        GameDataPaths.EnsureWritable(Path.Combine(_profile, "godot-saves", "continua.json"));
    }

    [Fact]
    public void PlaytestRefusesWritesOutsideTheProfile()
    {
        using var env = new EnvScope().Set(GameDataPaths.PlaytestEnv, "ui").Set(GameDataPaths.DataDirEnv, _profile);
        Assert.Throws<PlayerDataWriteRefusedException>(
            () => GameDataPaths.EnsureWritable(Path.Combine(_outside, "continua.json")));
        // A sibling folder whose name starts with the profile's is still outside it.
        Assert.Throws<PlayerDataWriteRefusedException>(
            () => GameDataPaths.EnsureWritable(_profile + "-bis" + Path.DirectorySeparatorChar + "continua.json"));
        Assert.Throws<PlayerDataWriteRefusedException>(
            () => GameDataPaths.EnsureWritable(Path.Combine(_profile, "..", "continua.json")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative-profile")]
    public void PlaytestWithoutAnAbsoluteProfileRefusesEveryWrite(string? dataDir)
    {
        using var env = new EnvScope().Set(GameDataPaths.PlaytestEnv, "tutorial").Set(GameDataPaths.DataDirEnv, dataDir);
        Assert.NotNull(GameDataPaths.PlaytestProfileProblem());
        Assert.Throws<PlayerDataWriteRefusedException>(
            () => GameDataPaths.EnsureWritable(Path.Combine(_outside, "settings.json")));
    }

    [Fact]
    public void PlaytestSaveOutsideTheProfileWritesNothingAndRaisesTheHook()
    {
        using var env = new EnvScope().Set(GameDataPaths.PlaytestEnv, "ui").Set(GameDataPaths.DataDirEnv, _profile);
        string? refused = null;
        void OnRefused(string message) => refused = message;
        GameDataPaths.WriteRefused += OnRefused;
        FactorySaveStore.SavesDirectoryOverride = _outside;
        try
        {
            Assert.Throws<PlayerDataWriteRefusedException>(
                () => FactorySaveStore.Save(FactorySaveStore.ContinueSlotId, NewHost()));
            Assert.Throws<PlayerDataWriteRefusedException>(
                () => new CampaignProgress().Save(Path.Combine(_outside, "campaignProgress.json")));
        }
        finally
        {
            FactorySaveStore.SavesDirectoryOverride = null;
            GameDataPaths.WriteRefused -= OnRefused;
        }

        Assert.False(Directory.Exists(_outside));
        Assert.NotNull(refused);
    }

    [Fact]
    public void PlaytestSavesLandInTheProfile()
    {
        using var env = new EnvScope().Set(GameDataPaths.PlaytestEnv, "ui").Set(GameDataPaths.DataDirEnv, _profile);
        FactorySaveStore.Save(FactorySaveStore.ContinueSlotId, NewHost());
        new CampaignProgress().Save();

        Assert.True(File.Exists(Path.Combine(_profile, "godot-saves", "continua.json")));
        Assert.True(File.Exists(Path.Combine(_profile, "campaignProgress.json")));
    }
}
