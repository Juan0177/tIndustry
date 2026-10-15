namespace TIndustry.Shared;

/// <summary>
/// Root folder for player data (saves and campaign progress).
/// Client settings live under Godot's user dir (see <c>ClientSettings.SettingsDirectory</c>);
/// <c>TINDUSTRY_DATA_DIR</c> redirects both to an isolated profile for dev runs.
/// </summary>
public static class GameDataPaths
{
    public const string DataDirEnv = "TINDUSTRY_DATA_DIR";

    /// <summary>Override folder from <see cref="DataDirEnv"/>, or null for the normal locations.</summary>
    public static string? Override
    {
        get
        {
            var dir = Environment.GetEnvironmentVariable(DataDirEnv);
            return string.IsNullOrWhiteSpace(dir) ? null : dir;
        }
    }

    public static string Root =>
        Override ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "tIndustry");
}
