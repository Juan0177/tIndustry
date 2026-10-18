using System.Globalization;

namespace TIndustry.Godot;

/// <summary>
/// When the agents' control channel exists (pure, no Godot types: tested in TIndustry.Shared.Tests as a linked file):
/// only in a debug build and only with <see cref="PortEnv"/> set to a valid port, so an exported game never opens it.
/// </summary>
public partial class AgentBridge
{
    public const string PortEnv = "TINDUSTRY_AGENT_PORT";

    public static bool ShouldStart(bool isDebug, string? envPort) => isDebug && TryParsePort(envPort, out _);

    public static bool TryParsePort(string? text, out int port) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port is > 0 and <= 65535;
}
