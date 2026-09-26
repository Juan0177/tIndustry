using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace TIndustry.Godot;

/// <summary>
/// Godot client preferences (Raylib <c>GameSettings</c> thin port).
/// Persists under user data: VSync, UI scale, FPS overlay, tutorial flag.
/// </summary>
public sealed class ClientSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static readonly int[] UiScalePresets = [100, 125, 150, 200];

    public bool VSync { get; set; } = true;
    /// <summary>HUD/content scale percent (100/125/150/200).</summary>
    public int UiScalePercent { get; set; } = 125;
    public bool ShowFps { get; set; }
    /// <summary>Session overlay: FPS · CPU · RAM · GPU (Raylib ShowResourceOverlay).</summary>
    public bool ShowResourceOverlay { get; set; } = true;
    /// <summary>True after first-run tutorial is finished or skipped.</summary>
    public bool TutorialCompleted { get; set; }

    public static string SettingsDirectory
    {
        get
        {
            var path = Path.Combine(
                OS.GetUserDataDir(),
                "tIndustry");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static ClientSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new ClientSettings();
            }

            var loaded = JsonSerializer.Deserialize<ClientSettings>(
                File.ReadAllText(SettingsPath), JsonOptions);
            var settings = loaded ?? new ClientSettings();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return new ClientSettings();
        }
    }

    public void Save()
    {
        Normalize();
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public ClientSettings Clone() => new()
    {
        VSync = VSync,
        UiScalePercent = UiScalePercent,
        ShowFps = ShowFps,
        ShowResourceOverlay = ShowResourceOverlay,
        TutorialCompleted = TutorialCompleted
    };

    public void CopyFrom(ClientSettings other)
    {
        VSync = other.VSync;
        UiScalePercent = other.UiScalePercent;
        ShowFps = other.ShowFps;
        ShowResourceOverlay = other.ShowResourceOverlay;
        TutorialCompleted = other.TutorialCompleted;
        Normalize();
    }

    public void Normalize()
    {
        if (!UiScalePresets.Contains(UiScalePercent))
        {
            UiScalePercent = UiScalePresets
                .OrderBy(v => Math.Abs(v - UiScalePercent))
                .First();
        }
    }

    public float UiScaleFactor => UiScalePercent / 100f;

    public static string UiScaleLabel(int percent) => $"{percent}%";

    /// <summary>Apply VSync + content scale to the active window / root.</summary>
    public void ApplyToEngine(Window? window = null)
    {
        DisplayServer.WindowSetVsyncMode(
            VSync
                ? DisplayServer.VSyncMode.Enabled
                : DisplayServer.VSyncMode.Disabled);

        window ??= (Engine.GetMainLoop() as SceneTree)?.Root;
        if (window is not null)
        {
            window.ContentScaleFactor = Math.Clamp(UiScaleFactor, 0.75f, 2.5f);
        }
    }
}
