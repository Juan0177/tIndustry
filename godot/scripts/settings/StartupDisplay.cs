namespace TIndustry.Godot;

/// <summary>
/// Window mode and first-run UI scale (pure, no Godot types: tested in TIndustry.Shared.Tests as a linked file).
/// Release builds start fullscreen at the screen's resolution; dev and test runs (debug build or playtest) start in a
/// 1280×720 window at UI 100%. A playtest forces that whatever the settings say, so its captures stay comparable.
/// </summary>
public static class StartupDisplay
{
    /// <summary>Window size of dev and test runs (project.godot viewport size).</summary>
    public const int DevWidth = 1280;
    public const int DevHeight = 720;

    /// <summary>Fullscreen or not: <paramref name="saved"/> wins, except in a playtest (always windowed).</summary>
    public static bool Fullscreen(bool? saved, bool isDebug, bool isPlaytest) =>
        !isPlaytest && (saved ?? !isDebug);

    /// <summary>UI scale percent when no settings file exists yet: 100 in dev/test, else from the screen height.</summary>
    public static int FirstRunUiScale(int screenHeight, bool isDebug, bool isPlaytest) =>
        isDebug || isPlaytest || screenHeight < 1400 ? 100
        : screenHeight < 2000 ? 150
        : 200;
}
