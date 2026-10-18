using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TIndustry.Godot;

/// <summary>
/// Pure rules of the agents' control channel (no Godot types: tested in TIndustry.Shared.Tests as a linked file). The
/// channel exists only in a debug build, with a valid <see cref="PortEnv"/>, a <see cref="TokenEnv"/> secret and an
/// isolated profile (<c>TINDUSTRY_DATA_DIR</c> set, never the player's real data), so an exported game or a run on the
/// player's own profile never opens it.
/// </summary>
public partial class AgentBridge
{
    public const string PortEnv = "TINDUSTRY_AGENT_PORT";
    public const string TokenEnv = "TINDUSTRY_AGENT_TOKEN";

    /// <summary>Set to 1 by tools/AgentPlay only: the game quits when the launcher's authenticated connection drops.</summary>
    public const string QuitWithoutClientEnv = "TINDUSTRY_AGENT_ESCI_SENZA_CLIENT";

    /// <summary>Longest request line; the real ones are a few hundred bytes.</summary>
    public const int MaxLineChars = 64 * 1024;

    // A browser's cross-protocol request: "POST /x HTTP/1.1" or a "Host: ..." header line.
    private static readonly Regex HttpLine = new(
        "^((GET|POST|PUT|DELETE|HEAD|OPTIONS|PATCH|CONNECT|TRACE) |[A-Za-z0-9-]+:\\s)", RegexOptions.CultureInvariant);

    public static bool ShouldStart(
        bool isDebug, string? envPort, string? envToken, string? dataDir, IEnumerable<string> realDataDirs) =>
        isDebug && TryParsePort(envPort, out _) && !string.IsNullOrWhiteSpace(envToken) && IsIsolatedProfile(dataDir, realDataDirs);

    public static bool TryParsePort(string? text, out int port) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out port) && port is > 0 and <= 65535;

    /// <summary>An absolute folder that is not one of <paramref name="realDataDirs"/> and not inside one.</summary>
    public static bool IsIsolatedProfile(string? dataDir, IEnumerable<string> realDataDirs) =>
        !string.IsNullOrWhiteSpace(dataDir) && Path.IsPathFullyQualified(dataDir.Trim())
        && !realDataDirs.Any(real => FolderPrefix(dataDir).StartsWith(FolderPrefix(real), StringComparison.OrdinalIgnoreCase));

    /// <summary>The player's real data folders (from the OS, never from TINDUSTRY_DATA_DIR or APPDATA).</summary>
    public static string[] RealDataDirs() =>
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "tIndustry"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Godot", "app_userdata", "tIndustry")
    ];

    private static string FolderPrefix(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim())) + Path.DirectorySeparatorChar;

    /// <summary>The handshake line <c>{"token":"..."}</c> carries <paramref name="token"/> (constant-time compare).</summary>
    public static bool TokenMatches(string line, string token)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("token", out var sent) || sent.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(sent.GetString()!), Encoding.UTF8.GetBytes(token));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool LooksLikeHttp(string line) => HttpLine.IsMatch(line);

    /// <summary>
    /// Where <c>cattura</c> writes: a plain relative name under <c>&lt;profile&gt;/catture</c> (default
    /// <c>cattura-NNN.png</c>), or null for an absolute path, a <c>..</c> segment, a non-PNG name or anything resolving
    /// outside that folder.
    /// </summary>
    public static string? CapturePath(string profile, string? wanted, int shot)
    {
        var folder = Path.Combine(Path.GetFullPath(profile), "catture");
        if (wanted is null)
        {
            return Path.Combine(folder, $"cattura-{shot:000}.png");
        }

        if (wanted.Length == 0 || Path.IsPathRooted(wanted) || wanted.Split('/', '\\').Contains("..")
            || !wanted.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var full = Path.GetFullPath(Path.Combine(folder, wanted));
        return full.StartsWith(FolderPrefix(folder), StringComparison.OrdinalIgnoreCase) ? full : null;
    }
}
