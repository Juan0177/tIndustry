using TIndustry.Godot;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Rules of the agents' channel in the client (godot/scripts/dev/AgentBridge.Start.cs, linked): never in an exported
/// game, never on the player's real profile, a secret first, no browser requests, captures only inside the profile.
/// </summary>
public sealed class AgentBridgeStartTests
{
    private static readonly string Base = Path.Combine(Path.GetTempPath(), "agentbridge-tests");
    private static readonly string RealLocal = Path.Combine(Base, "Local", "tIndustry");
    private static readonly string RealGodot = Path.Combine(Base, "Roaming", "Godot", "app_userdata", "tIndustry");
    private static readonly string[] Real = [RealLocal, RealGodot];
    private static readonly string Profile = Path.Combine(Base, "profilo");

    [Theory]
    [InlineData(false, "4711", "t", true, false)] // exported (release) build: never, even with everything set
    [InlineData(true, null, "t", true, false)]    // dev build without the port
    [InlineData(true, "", "t", true, false)]
    [InlineData(true, "abc", "t", true, false)]
    [InlineData(true, "0", "t", true, false)]
    [InlineData(true, "-5", "t", true, false)]
    [InlineData(true, "65536", "t", true, false)]
    [InlineData(true, " 4711", "t", true, false)]
    [InlineData(true, "4711", null, true, false)] // no secret
    [InlineData(true, "4711", " ", true, false)]
    [InlineData(true, "4711", "t", false, false)] // no TINDUSTRY_DATA_DIR
    [InlineData(true, "4711", "t", true, true)]
    [InlineData(true, "65535", "t", true, true)]
    public void Starts_only_in_debug_builds_with_port_token_and_profile(
        bool isDebug, string? port, string? token, bool withProfile, bool expected) =>
        Assert.Equal(expected, AgentBridge.ShouldStart(isDebug, port, token, withProfile ? Profile : null, Real));

    [Fact]
    public void The_profile_is_never_the_players_real_data()
    {
        Assert.True(AgentBridge.IsIsolatedProfile(Profile, Real));
        Assert.True(AgentBridge.IsIsolatedProfile(RealLocal + "-altro", Real)); // a sibling is not inside
        Assert.False(AgentBridge.IsIsolatedProfile(RealLocal, Real));
        Assert.False(AgentBridge.IsIsolatedProfile(RealLocal + Path.DirectorySeparatorChar, Real));
        Assert.False(AgentBridge.IsIsolatedProfile(RealLocal.ToUpperInvariant(), Real));
        Assert.False(AgentBridge.IsIsolatedProfile(Path.Combine(RealLocal, "sub"), Real));
        Assert.False(AgentBridge.IsIsolatedProfile(Path.Combine(RealGodot, "logs"), Real));
        Assert.False(AgentBridge.IsIsolatedProfile(Path.Combine(Profile, "..", "Local", "tIndustry"), Real));
        Assert.False(AgentBridge.IsIsolatedProfile("profilo-relativo", Real));
        Assert.False(AgentBridge.IsIsolatedProfile("", Real));
        Assert.Equal(2, AgentBridge.RealDataDirs().Length);
    }

    [Fact]
    public void The_handshake_needs_the_exact_token()
    {
        Assert.True(AgentBridge.TokenMatches("{\"token\":\"s3gr3to\"}", "s3gr3to"));
        Assert.False(AgentBridge.TokenMatches("{\"token\":\"s3gr3t\"}", "s3gr3to"));
        Assert.False(AgentBridge.TokenMatches("{\"token\":\"s3gr3to!\"}", "s3gr3to"));
        Assert.False(AgentBridge.TokenMatches("{\"token\":1}", "s3gr3to"));
        Assert.False(AgentBridge.TokenMatches("{\"id\":1,\"metodo\":\"stato\"}", "s3gr3to"));
        Assert.False(AgentBridge.TokenMatches("[\"s3gr3to\"]", "s3gr3to"));
        Assert.False(AgentBridge.TokenMatches("s3gr3to", "s3gr3to"));
    }

    [Theory]
    [InlineData("POST / HTTP/1.1", true)]
    [InlineData("GET /stato HTTP/1.1", true)]
    [InlineData("OPTIONS * HTTP/1.1", true)]
    [InlineData("Host: 127.0.0.1:4711", true)]
    [InlineData("Origin: http://example.com", true)]
    [InlineData("content-type: text/plain", true)]
    [InlineData("{\"id\":1,\"metodo\":\"stato\"}", false)]
    [InlineData("questa riga non è JSON", false)]
    [InlineData("POSTA", false)]
    public void Browser_requests_are_recognised(string line, bool expected) =>
        Assert.Equal(expected, AgentBridge.LooksLikeHttp(line));

    [Fact]
    public void Captures_stay_inside_the_profile()
    {
        var folder = Path.Combine(Profile, "catture");
        Assert.Equal(Path.Combine(folder, "cattura-007.png"), AgentBridge.CapturePath(Profile, null, 7));
        Assert.Equal(Path.Combine(folder, "a.png"), AgentBridge.CapturePath(Profile, "a.png", 1));
        Assert.Equal(Path.Combine(folder, "sub", "a.png"), AgentBridge.CapturePath(Profile, Path.Combine("sub", "a.png"), 1));
        Assert.Null(AgentBridge.CapturePath(Profile, "..\\..\\x.png", 1));
        Assert.Null(AgentBridge.CapturePath(Profile, "../../x.png", 1));
        Assert.Null(AgentBridge.CapturePath(Profile, "sub/../../x.png", 1));
        Assert.Null(AgentBridge.CapturePath(Profile, Path.Combine(Path.GetTempPath(), "altro", "x.png"), 1));
        Assert.Null(AgentBridge.CapturePath(Profile, Path.Combine(folder, "x.png"), 1)); // absolute, even inside
        Assert.Null(AgentBridge.CapturePath(Profile, "a.txt", 1));
        Assert.Null(AgentBridge.CapturePath(Profile, "", 1));
    }
}
