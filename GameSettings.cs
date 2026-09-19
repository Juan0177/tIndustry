using System.Text.Json;
using System.Text.Json.Serialization;

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

    public static readonly (int Width, int Height, string Label)[] ResolutionPresets =
    [
        (960, 600, "960×600"),
        (1240, 760, "1240×760"),
        (1440, 900, "1440×900"),
        (1600, 900, "1600×900"),
        (1920, 1080, "1920×1080")
    ];

    public bool ShowFps { get; set; }
    public bool ShowResourceOverlay { get; set; } = true;
    public int ResolutionWidth { get; set; } = 1240;
    public int ResolutionHeight { get; set; } = 760;
    public DisplayMode DisplayMode { get; set; } = DisplayMode.Windowed;

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
        DisplayMode = DisplayMode
    };

    public void CopyFrom(GameSettings other)
    {
        ShowFps = other.ShowFps;
        ShowResourceOverlay = other.ShowResourceOverlay;
        ResolutionWidth = other.ResolutionWidth;
        ResolutionHeight = other.ResolutionHeight;
        DisplayMode = other.DisplayMode;
        Normalize();
    }

    public bool MatchesDisplay(GameSettings other) =>
        ResolutionWidth == other.ResolutionWidth
        && ResolutionHeight == other.ResolutionHeight
        && DisplayMode == other.DisplayMode;

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

        // Snap to nearest known preset when close; keep custom sizes otherwise.
        foreach (var preset in ResolutionPresets)
        {
            if (preset.Width == ResolutionWidth && preset.Height == ResolutionHeight)
            {
                return;
            }
        }
    }

    public static string DisplayModeLabel(DisplayMode mode) => mode switch
    {
        DisplayMode.Fullscreen => "Schermo intero",
        DisplayMode.Borderless => "Senza bordi",
        DisplayMode.Windowed => "Finestra",
        _ => "Finestra"
    };

    public int ResolutionPresetIndex()
    {
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
}
