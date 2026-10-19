using Xunit;

namespace TIndustry.AgentPlay.Tests;

/// <summary>GameEngine.IsInside: the bridge's journal path is read and deleted only inside the game's profile.</summary>
public sealed class IsInsideTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "tindustry-isinside");
    private static readonly string Profile = Path.Combine(Root, "profile");

    [Fact]
    public void AFileInsideTheProfileIsInside() =>
        Assert.True(GameEngine.IsInside(Profile, Path.Combine(Profile, "diari", "partita.json")));

    [Fact]
    public void TheProfileItselfIsNotInside() =>
        Assert.False(GameEngine.IsInside(Profile, Profile));

    [Fact]
    public void DotDotOutOfTheProfileIsNotInside() =>
        Assert.False(GameEngine.IsInside(Profile, Path.Combine(Profile, "diari", "..", "..", "altro", "partita.json")));

    [Fact]
    public void AFolderWithTheSamePrefixIsNotInside() =>
        Assert.False(GameEngine.IsInside(Path.Combine(Root, "prof"), Path.Combine(Profile, "partita.json")));

    [Fact]
    public void ARelativePathIsNotInside() =>
        Assert.False(GameEngine.IsInside(Profile, Path.Combine("diari", "partita.json")));
}
