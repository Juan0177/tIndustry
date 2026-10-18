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

    /// <summary>UI scale range and step, percent (G4: HUD and panels only, never the world).</summary>
    public const int UiScaleMin = 75;
    public const int UiScaleMax = 200;
    public const int UiScaleStep = 5;
    /// <summary>Default UI scale: the pixel text at its native size (font pixels whole squares).</summary>
    public const int UiScaleDefault = 100;
    /// <summary>Frame rate caps (0 = none).</summary>
    public static readonly int[] FpsLimitPresets = [0, 30, 60, 120, 144];
    /// <summary>World render resolution, percent of the window (see <see cref="RenderScale"/>).</summary>
    public static readonly int[] RenderScalePresets = [100, 75, 50];

    /// <summary>
    /// Set to 1 to keep the graphics settings in a playtest. Without it a playtest shows no particles, no shadows, no
    /// glow and no night whatever the settings say, so its captures stay stable.
    /// </summary>
    public const string PlaytestEffectsEnv = "TINDUSTRY_PLAYTEST_EFFETTI";

    public bool VSync { get; set; } = true;
    /// <summary>
    /// HUD and panels scale percent, <see cref="UiScaleMin"/>–<see cref="UiScaleMax"/> in steps of
    /// <see cref="UiScaleStep"/> (older files with 100/125/150/200 stay valid as they are).
    /// </summary>
    public int UiScalePercent { get; set; } = UiScaleDefault;
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
    /// <summary>
    /// Fullscreen at the screen's resolution (null = never chosen: fullscreen in release builds, window in dev/test).
    /// A playtest is always windowed 1280×720 (<see cref="StartupDisplay"/>).
    /// </summary>
    public bool? SchermoIntero { get; set; }

    /// <summary>Language of the texts (<see cref="Testi.Lingue"/>); missing or unknown = Italian.</summary>
    public string Lingua { get; set; } = Testi.LinguaPredefinita;

    /// <summary>
    /// The fullscreen choice the settings panel shows: <see cref="SchermoIntero"/>, or when never chosen what this run
    /// actually does.
    /// </summary>
    [JsonIgnore]
    public bool SchermoInteroScelto =>
        SchermoIntero ?? StartupDisplay.Fullscreen(null, OS.IsDebugBuild(), GameDataPaths.IsPlaytest);

    /// <summary>True while a playtest forces effects, shadows, glow and night off (no <see cref="PlaytestEffectsEnv"/>=1).</summary>
    public static bool PlaytestForcesEffectsOff =>
        GameDataPaths.IsPlaytest && OS.GetEnvironment(PlaytestEffectsEnv) != "1";

    /// <summary>The effects quality actually drawn: <see cref="Effetti"/>, or Spenti in a playtest.</summary>
    [JsonIgnore]
    public EffectsQuality EffectiveEffects => PlaytestForcesEffectsOff ? EffectsQuality.Spenti : Effetti;

    /// <summary>The glow actually drawn: <see cref="Bagliore"/>, or off in a playtest.</summary>
    [JsonIgnore]
    public bool EffectiveGlow => Bagliore && !PlaytestForcesEffectsOff;

    /// <summary>The shadows actually drawn: <see cref="Ombre"/>, or off in a playtest.</summary>
    [JsonIgnore]
    public bool EffectiveShadows => Ombre && !PlaytestForcesEffectsOff;

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
                // First run: the UI scale follows the screen (release builds only; dev and test stay at 100%).
                var screen = DisplayServer.ScreenGetSize(DisplayServer.WindowGetCurrentScreen());
                return new ClientSettings
                {
                    UiScalePercent = NormalizeUiScale(
                        StartupDisplay.FirstRunUiScale(screen.Y, OS.IsDebugBuild(), GameDataPaths.IsPlaytest))
                };
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
        ScalaRendering = ScalaRendering,
        SchermoIntero = SchermoIntero,
        Lingua = Lingua
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
        SchermoIntero = other.SchermoIntero;
        Lingua = other.Lingua;
        Normalize();
    }

    public void Normalize()
    {
        UiScalePercent = NormalizeUiScale(UiScalePercent);
        LimiteFps = Nearest(FpsLimitPresets, LimiteFps);
        ScalaRendering = Nearest(RenderScalePresets, ScalaRendering);
        if (!Enum.IsDefined(Effetti))
        {
            Effetti = EffectsQuality.Alti;
        }

        if (!Testi.Lingue.Contains(Lingua))
        {
            Lingua = Testi.LinguaPredefinita;
        }
    }

    /// <summary><paramref name="value"/> if it is a preset, else the closest preset (first one on a tie).</summary>
    private static int Nearest(int[] presets, int value) =>
        presets.Contains(value) ? value : presets.OrderBy(v => Math.Abs(v - value)).First();

    /// <summary>Button text of a frame rate cap: a number, or the key of "none" for 0.</summary>
    public static string FpsLimitLabel(int fps) =>
        fps == 0 ? "impostazioni.fps.nessuno" : fps.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary><paramref name="percent"/> clamped to the UI scale range and rounded to the nearest step (half up).</summary>
    public static int NormalizeUiScale(int percent)
    {
        var clamped = Math.Clamp(percent, UiScaleMin, UiScaleMax);
        return (clamped + UiScaleStep / 2) / UiScaleStep * UiScaleStep;
    }

    public static string UiScaleLabel(int percent) => $"{percent}%";

    /// <summary>
    /// Apply window mode (<see cref="StartupDisplay.Fullscreen"/>), VSync and frame rate cap to the engine, and keep the window's content scale at 1: the UI scale acts only
    /// on the HUD (<see cref="UiScale"/>, World.ApplyUiScale), never on the world. The rest of the graphics settings act
    /// on nodes of the world (World.ApplyGraphicsSettings: effects, render scale, glow, shadows, animations).
    /// </summary>
    public void ApplyToEngine(Window? window = null)
    {
        // A new language reaches every open panel and the HUD at once (NotificationTranslationChanged).
        if (TranslationServer.GetLocale() != Lingua)
        {
            TranslationServer.SetLocale(Lingua);
        }

        Engine.MaxFps = LimiteFps;
        DisplayServer.WindowSetVsyncMode(
            VSync
                ? DisplayServer.VSyncMode.Enabled
                : DisplayServer.VSyncMode.Disabled);

        ApplyWindowMode();
        window ??= (Engine.GetMainLoop() as SceneTree)?.Root;
        if (window is not null)
        {
            window.ContentScaleFactor = 1f;
        }
    }

    /// <summary>
    /// Fullscreen (borderless, the screen's own resolution: alt-tab stays instant) or a centered 1280×720 window.
    /// The world is drawn 1:1 (stretch mode disabled), so a bigger screen shows more map.
    /// </summary>
    private void ApplyWindowMode()
    {
        var fullscreen = StartupDisplay.Fullscreen(SchermoIntero, OS.IsDebugBuild(), GameDataPaths.IsPlaytest);
        var isFullscreen = DisplayServer.WindowGetMode()
            is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
        if (fullscreen && !isFullscreen)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        }
        else if (!fullscreen && isFullscreen)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            // Windows applies the mode change asynchronously: size and place the window once it has.
            Callable.From(PlaceDevWindow).CallDeferred();
        }
    }

    /// <summary>The 1280×720 window (smaller if the usable screen is), centered in the usable screen area.</summary>
    private static void PlaceDevWindow()
    {
        var usable = DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen());
        var size = new Vector2I(
            Math.Min(StartupDisplay.DevWidth, usable.Size.X),
            Math.Min(StartupDisplay.DevHeight, usable.Size.Y));
        DisplayServer.WindowSetSize(size);
        DisplayServer.WindowSetPosition(usable.Position + new Vector2I(
            Math.Max(0, (usable.Size.X - size.X) / 2),
            Math.Max(0, (usable.Size.Y - size.Y) / 2)));
    }
}
