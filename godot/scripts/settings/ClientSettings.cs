using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Godot client preferences (Raylib <c>GameSettings</c> thin port).
/// Persists under user data: VSync, UI scale, FPS overlay, tutorial flag and the graphics settings (G3).
/// A file written by an older version stays valid: missing fields keep their defaults.
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
    /// <summary>Frame rate caps (0 = none).</summary>
    public static readonly int[] FpsLimitPresets = [0, 30, 60, 120, 144];
    /// <summary>World render resolution, percent of the window (see <see cref="RenderScale"/>).</summary>
    public static readonly int[] RenderScalePresets = [100, 75, 50];

    /// <summary>
    /// Set to 1 to keep the graphics settings in a playtest. Without it a playtest shows no particles, no glow and
    /// no night whatever the settings say, so its captures stay stable.
    /// </summary>
    public const string PlaytestEffectsEnv = "TINDUSTRY_PLAYTEST_EFFETTI";

    public bool VSync { get; set; } = true;
    /// <summary>HUD/content scale percent (100/125/150/200).</summary>
    public int UiScalePercent { get; set; } = 125;
    public bool ShowFps { get; set; }
    /// <summary>Session overlay: FPS · CPU · RAM · GPU (Raylib ShowResourceOverlay).</summary>
    public bool ShowResourceOverlay { get; set; }
    /// <summary>True after first-run tutorial is finished or skipped.</summary>
    public bool TutorialCompleted { get; set; }

    /// <summary>Particle effects: Alti (all), Bassi (fewer, no ambient), Spenti (none, still water).</summary>
    [JsonConverter(typeof(TolerantEnumConverter<EffectsQuality>))]
    public EffectsQuality Effetti { get; set; } = EffectsQuality.Alti;
    /// <summary>Soft shadows under buildings.</summary>
    public bool Ombre { get; set; } = true;
    /// <summary>Glow (2D HDR) on furnaces, generators, Core, lamps.</summary>
    public bool Bagliore { get; set; } = true;
    /// <summary>Building animations (drills, rotors, furnace mouths, Core light); off = still frames.</summary>
    public bool AnimazioniBlocchi { get; set; } = true;
    /// <summary>Frame rate cap (<see cref="FpsLimitPresets"/>, 0 = none).</summary>
    public int LimiteFps { get; set; }
    /// <summary>World render resolution percent (<see cref="RenderScalePresets"/>).</summary>
    public int ScalaRendering { get; set; } = 100;

    /// <summary>True while a playtest forces effects, glow and night off (no <see cref="PlaytestEffectsEnv"/>=1).</summary>
    public static bool PlaytestForcesEffectsOff =>
        GameDataPaths.IsPlaytest && OS.GetEnvironment(PlaytestEffectsEnv) != "1";

    /// <summary>The effects quality actually drawn: <see cref="Effetti"/>, or Spenti in a playtest.</summary>
    [JsonIgnore]
    public EffectsQuality EffectiveEffects => PlaytestForcesEffectsOff ? EffectsQuality.Spenti : Effetti;

    /// <summary>The glow actually drawn: <see cref="Bagliore"/>, or off in a playtest.</summary>
    [JsonIgnore]
    public bool EffectiveGlow => Bagliore && !PlaytestForcesEffectsOff;

    public static string SettingsDirectory
    {
        get
        {
            var path = Path.Combine(
                GameDataPaths.Override ?? OS.GetUserDataDir(),
                "tIndustry");
            GameDataPaths.CreateDirectory(path);
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

            return FromJson(File.ReadAllText(SettingsPath));
        }
        catch
        {
            return new ClientSettings();
        }
    }

    /// <summary>Settings from a settings.json text (missing fields = defaults), normalized. Throws on bad JSON.</summary>
    public static ClientSettings FromJson(string json)
    {
        var settings = JsonSerializer.Deserialize<ClientSettings>(json, JsonOptions) ?? new ClientSettings();
        settings.Normalize();
        return settings;
    }

    public void Save()
    {
        Normalize();
        GameDataPaths.EnsureWritable(SettingsPath);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public ClientSettings Clone() => new()
    {
        VSync = VSync,
        UiScalePercent = UiScalePercent,
        ShowFps = ShowFps,
        ShowResourceOverlay = ShowResourceOverlay,
        TutorialCompleted = TutorialCompleted,
        Effetti = Effetti,
        Ombre = Ombre,
        Bagliore = Bagliore,
        AnimazioniBlocchi = AnimazioniBlocchi,
        LimiteFps = LimiteFps,
        ScalaRendering = ScalaRendering
    };

    public void CopyFrom(ClientSettings other)
    {
        VSync = other.VSync;
        UiScalePercent = other.UiScalePercent;
        ShowFps = other.ShowFps;
        ShowResourceOverlay = other.ShowResourceOverlay;
        TutorialCompleted = other.TutorialCompleted;
        Effetti = other.Effetti;
        Ombre = other.Ombre;
        Bagliore = other.Bagliore;
        AnimazioniBlocchi = other.AnimazioniBlocchi;
        LimiteFps = other.LimiteFps;
        ScalaRendering = other.ScalaRendering;
        Normalize();
    }

    public void Normalize()
    {
        UiScalePercent = Nearest(UiScalePresets, UiScalePercent);
        LimiteFps = Nearest(FpsLimitPresets, LimiteFps);
        ScalaRendering = Nearest(RenderScalePresets, ScalaRendering);
        if (!Enum.IsDefined(Effetti))
        {
            Effetti = EffectsQuality.Alti;
        }
    }

    /// <summary><paramref name="value"/> if it is a preset, else the closest preset (first one on a tie).</summary>
    private static int Nearest(int[] presets, int value) =>
        presets.Contains(value) ? value : presets.OrderBy(v => Math.Abs(v - value)).First();

    public static string FpsLimitLabel(int fps) => fps == 0 ? "Nessuno" : fps.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public float UiScaleFactor => UiScalePercent / 100f;

    public static string UiScaleLabel(int percent) => $"{percent}%";

    /// <summary>
    /// Apply VSync, frame rate cap and content scale to the active window / root. The rest of the graphics
    /// settings act on nodes of the world (World.ApplyGraphicsSettings: effects, render scale, animations).
    /// </summary>
    public void ApplyToEngine(Window? window = null)
    {
        Engine.MaxFps = LimiteFps;
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
