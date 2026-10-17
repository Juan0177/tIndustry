namespace TIndustry.Shared;

/// <summary>
/// Root folder for player data (saves and campaign progress).
/// Client settings live under Godot's user dir (see <c>ClientSettings.SettingsDirectory</c>);
/// <c>TINDUSTRY_DATA_DIR</c> redirects both to an isolated profile for dev runs.
/// Every player-data write goes through <see cref="EnsureWritable"/>: during a playtest it refuses
/// anything outside that profile, so a scripted run can never overwrite the player's real files.
/// </summary>
public static class GameDataPaths
{
    public const string DataDirEnv = "TINDUSTRY_DATA_DIR";
    public const string PlaytestEnv = "TINDUSTRY_PLAYTEST";

    /// <summary>
    /// Raised (before the exception is thrown) when <see cref="EnsureWritable"/> refuses a write,
    /// so the client can log it and quit instead of carrying on without saving.
    /// </summary>
    public static event Action<string>? WriteRefused;

    /// <summary>Override folder from <see cref="DataDirEnv"/> as a full path, or null for the normal locations.</summary>
    public static string? Override
    {
        get
        {
            var dir = Environment.GetEnvironmentVariable(DataDirEnv);
            return string.IsNullOrWhiteSpace(dir) ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir.Trim()));
        }
    }

    /// <summary>True when <see cref="PlaytestEnv"/> asks for a scripted playtest.</summary>
    public static bool IsPlaytest => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PlaytestEnv));

    public static string Root =>
        Override ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "tIndustry");

    /// <summary>
    /// Why a playtest may not write player data (Italian, for the log), or null when it may:
    /// the profile must be set and absolute — a relative value would resolve against the working dir.
    /// Always null outside a playtest.
    /// </summary>
    public static string? PlaytestProfileProblem()
    {
        if (!IsPlaytest)
        {
            return null;
        }

        var raw = Environment.GetEnvironmentVariable(DataDirEnv);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return $"{PlaytestEnv} richiede {DataDirEnv} (profilo isolato).";
        }

        return Path.IsPathFullyQualified(raw.Trim())
            ? null
            : $"{DataDirEnv} deve essere un percorso assoluto durante un playtest: \"{raw}\".";
    }

    /// <summary>
    /// Throws <see cref="PlayerDataWriteRefusedException"/> when a playtest would write
    /// <paramref name="path"/> outside the isolated profile; a no-op outside a playtest.
    /// </summary>
    public static void EnsureWritable(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!IsPlaytest)
        {
            return;
        }

        var problem = PlaytestProfileProblem();
        if (problem is null && !IsInside(Override!, path))
        {
            problem = $"playtest: scrittura fuori dal profilo {Override} rifiutata: {Path.GetFullPath(path)}";
        }

        if (problem is null)
        {
            return;
        }

        WriteRefused?.Invoke(problem);
        throw new PlayerDataWriteRefusedException(problem);
    }

    /// <summary><see cref="Directory.CreateDirectory(string)"/> behind <see cref="EnsureWritable"/>.</summary>
    public static void CreateDirectory(string dir)
    {
        EnsureWritable(dir);
        Directory.CreateDirectory(dir);
    }

    private static bool IsInside(string root, string path)
    {
        var full = Path.GetFullPath(path);
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(full, prefix, comparison))
        {
            return true;
        }

        // A drive root (C:\) keeps its separator; anything else needs one so "profile-bis" is outside "profile".
        return full.StartsWith(Path.EndsInDirectorySeparator(prefix) ? prefix : prefix + Path.DirectorySeparatorChar, comparison);
    }
}

/// <summary>A playtest tried to write player data outside its isolated profile.</summary>
public sealed class PlayerDataWriteRefusedException(string message) : IOException(message);
