using System.Text.Json;
using System.Text.Json.Serialization;
using Raylib_cs;

namespace TIndustry.Logistics;

public enum DisplayMode
{
    Windowed = 0,
    Borderless = 1,
    Fullscreen = 2
}

public sealed class GameSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>Manual resolution presets (Auto is handled separately via UseAutoResolution).</summary>
    public static readonly (int Width, int Height, string Label)[] ResolutionPresets =
    [
        (960, 600, "960×600"),
        (1240, 760, "1240×760"),
        (1440, 900, "1440×900"),
        (1600, 900, "1600×900"),
        (1920, 1080, "1920×1080"),
        (2560, 1440, "2560×1440 (2K)"),
        (3840, 2160, "3840×2160 (4K)")
    ];

    /// <summary>FPS limiter steps; 0 = Illimitato.</summary>
    public static readonly int[] FpsLimitPresets =
    [
        30, 60, 120, 144, 240, 360, 600, 0
    ];

    /// <summary>UI scale presets (percent). Default 125% for readable 1080p+ HUD text.</summary>
    public static readonly int[] UiScalePresets = [100, 125, 150, 200];

    public bool ShowFps { get; set; }
    public bool ShowResourceOverlay { get; set; } = true;
    public int ResolutionWidth { get; set; } = 1240;
    public int ResolutionHeight { get; set; } = 760;
    public bool UseAutoResolution { get; set; }
    public DisplayMode DisplayMode { get; set; } = DisplayMode.Windowed;
    public bool VSync { get; set; } = true;
    /// <summary>Target FPS; 0 means unlimited (Illimitato). Stored even when VSync is on.</summary>
    public int TargetFps { get; set; } = 60;
    /// <summary>HUD/font scale percent (100/125/150/200). Default 125.</summary>
    public int UiScalePercent { get; set; } = 125;
    /// <summary>True after the first-run tutorial is finished or skipped.</summary>
    public bool TutorialCompleted { get; set; }

    public static string SettingsDirectory
    {
        get
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "tIndustry");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static GameSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new GameSettings();
            }

            var loaded = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(SettingsPath), JsonOptions);
            var settings = loaded ?? new GameSettings();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return new GameSettings();
        }
    }

    public void Save()
    {
        Normalize();
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public GameSettings Clone() => new()
    {
        ShowFps = ShowFps,
        ShowResourceOverlay = ShowResourceOverlay,
        ResolutionWidth = ResolutionWidth,
        ResolutionHeight = ResolutionHeight,
        UseAutoResolution = UseAutoResolution,
        DisplayMode = DisplayMode,
        VSync = VSync,
        TargetFps = TargetFps,
        UiScalePercent = UiScalePercent,
        TutorialCompleted = TutorialCompleted
    };

    public void CopyFrom(GameSettings other)
    {
        ShowFps = other.ShowFps;
        ShowResourceOverlay = other.ShowResourceOverlay;
        ResolutionWidth = other.ResolutionWidth;
        ResolutionHeight = other.ResolutionHeight;
        UseAutoResolution = other.UseAutoResolution;
        DisplayMode = other.DisplayMode;
        VSync = other.VSync;
        TargetFps = other.TargetFps;
        UiScalePercent = other.UiScalePercent;
        TutorialCompleted = other.TutorialCompleted;
        Normalize();
    }

    public bool MatchesDisplay(GameSettings other) =>
        ResolutionWidth == other.ResolutionWidth
        && ResolutionHeight == other.ResolutionHeight
        && UseAutoResolution == other.UseAutoResolution
        && DisplayMode == other.DisplayMode
        && VSync == other.VSync
        && TargetFps == other.TargetFps
        && UiScalePercent == other.UiScalePercent;

    public float UiScaleFactor => UiScalePercent / 100f;

    public static string UiScaleLabel(int percent) => $"{percent}%";

    public void Normalize()
    {
        if (ResolutionWidth < 800)
        {
            ResolutionWidth = 800;
        }

        if (ResolutionHeight < 500)
        {
            ResolutionHeight = 500;
        }

        if (!Enum.IsDefined(DisplayMode))
        {
            DisplayMode = DisplayMode.Windowed;
        }

        if (TargetFps < 0)
        {
            TargetFps = 0;
        }

        if (TargetFps > 0 && !FpsLimitPresets.Contains(TargetFps))
        {
            // Snap to nearest allowed step (excluding unlimited).
            TargetFps = FpsLimitPresets
                .Where(v => v > 0)
                .OrderBy(v => Math.Abs(v - TargetFps))
                .FirstOrDefault(60);
        }

        if (!UiScalePresets.Contains(UiScalePercent))
        {
            UiScalePercent = UiScalePresets
                .OrderBy(v => Math.Abs(v - UiScalePercent))
                .FirstOrDefault(125);
        }
    }

    public static string DisplayModeLabel(DisplayMode mode) => mode switch
    {
        DisplayMode.Fullscreen => "Schermo intero",
        DisplayMode.Borderless => "Senza bordi",
        DisplayMode.Windowed => "Finestra",
        _ => "Finestra"
    };

    public static string FpsLimitLabel(int fps) =>
        fps <= 0 ? "Illimitato" : $"{fps} FPS";

    public int ResolutionPresetIndex()
    {
        if (UseAutoResolution)
        {
            return -2;
        }

        for (var i = 0; i < ResolutionPresets.Length; i++)
        {
            if (ResolutionPresets[i].Width == ResolutionWidth
                && ResolutionPresets[i].Height == ResolutionHeight)
            {
                return i;
            }
        }

        return -1;
    }

    public int FpsLimitPresetIndex()
    {
        for (var i = 0; i < FpsLimitPresets.Length; i++)
        {
            if (FpsLimitPresets[i] == TargetFps)
            {
                return i;
            }
        }

        return 1; // default 60
    }
}

/// <summary>
/// Applies resolution, display mode, VSync, and FPS limit through Raylib APIs.
/// </summary>
public static class DisplayApplier
{
    public static void Apply(GameSettings settings)
    {
        settings.Normalize();

        if (settings.UseAutoResolution && Raylib.IsWindowReady())
        {
            var monitor = Raylib.GetCurrentMonitor();
            settings.ResolutionWidth = Math.Max(800, Raylib.GetMonitorWidth(monitor));
            settings.ResolutionHeight = Math.Max(500, Raylib.GetMonitorHeight(monitor));
        }

        var width = settings.ResolutionWidth;
        var height = settings.ResolutionHeight;

        // Leave exclusive/borderless first so size changes stick in windowed mode.
        if (Raylib.IsWindowFullscreen())
        {
            Raylib.ToggleFullscreen();
        }

        if (Raylib.IsWindowState(ConfigFlags.BorderlessWindowMode))
        {
            Raylib.ClearWindowState(ConfigFlags.BorderlessWindowMode);
        }

        if (Raylib.IsWindowReady())
        {
            Raylib.SetWindowSize(width, height);
        }

        switch (settings.DisplayMode)
        {
            case DisplayMode.Fullscreen:
                if (!Raylib.IsWindowFullscreen())
                {
                    Raylib.ToggleFullscreen();
                }

                break;
            case DisplayMode.Borderless:
                Raylib.SetWindowState(ConfigFlags.BorderlessWindowMode);
                break;
            case DisplayMode.Windowed:
            default:
                if (Raylib.IsWindowReady())
                {
                    Raylib.SetWindowPosition(
                        Math.Max(40, (Raylib.GetMonitorWidth(0) - width) / 2),
                        Math.Max(40, (Raylib.GetMonitorHeight(0) - height) / 2));
                }

                break;
        }

        ApplyFramePacing(settings);
    }

    /// <summary>
    /// VSync on: prefer monitor refresh pacing (SetTargetFPS 0) while still storing the user's FPS preference.
    /// VSync off: apply the FPS limiter (0 = Illimitato).
    /// </summary>
    public static void ApplyFramePacing(GameSettings settings)
    {
        if (!Raylib.IsWindowReady())
        {
            return;
        }

        if (settings.VSync)
        {
            Raylib.SetWindowState(ConfigFlags.VSyncHint);
            Raylib.SetTargetFPS(0);
        }
        else
        {
            Raylib.ClearWindowState(ConfigFlags.VSyncHint);
            Raylib.SetTargetFPS(settings.TargetFps);
        }
    }

    public static void CaptureDesktopResolution(GameSettings settings)
    {
        if (!Raylib.IsWindowReady())
        {
            return;
        }

        var monitor = Raylib.GetCurrentMonitor();
        settings.ResolutionWidth = Math.Max(800, Raylib.GetMonitorWidth(monitor));
        settings.ResolutionHeight = Math.Max(500, Raylib.GetMonitorHeight(monitor));
        settings.UseAutoResolution = true;
    }
}
