using System.Text.Json;

namespace TIndustry.Logistics;

public sealed class GameSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public bool ShowFps { get; set; }
    public bool ShowResourceOverlay { get; set; } = true;

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
            return loaded ?? new GameSettings();
        }
        catch
        {
            return new GameSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
