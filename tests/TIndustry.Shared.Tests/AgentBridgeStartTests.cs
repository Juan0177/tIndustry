using TIndustry.Godot;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>The agents' channel in the client (godot/scripts/dev/AgentBridge.Start.cs, linked): never in an exported game.</summary>
public sealed class AgentBridgeStartTests
{
    [Theory]
    [InlineData(false, "4711", false)] // exported (release) build: never, even with the port set
    [InlineData(false, null, false)]
    [InlineData(true, null, false)]    // dev build without the variable
    [InlineData(true, "", false)]
    [InlineData(true, "abc", false)]
    [InlineData(true, "0", false)]
    [InlineData(true, "-5", false)]
    [InlineData(true, "65536", false)]
    [InlineData(true, " 4711", false)]
    [InlineData(true, "4711", true)]
    [InlineData(true, "65535", true)]
    public void Starts_only_in_debug_builds_with_a_valid_port(bool isDebug, string? port, bool expected) =>
        Assert.Equal(expected, AgentBridge.ShouldStart(isDebug, port));
}
