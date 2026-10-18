using TIndustry.Godot;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Window mode and first-run UI scale of the client (godot/scripts/settings/StartupDisplay.cs, linked).</summary>
public sealed class StartupDisplayTests
{
    [Theory]
    [InlineData(null, false, false, true)]   // release, first run: fullscreen
    [InlineData(null, true, false, false)]   // dev run: window
    [InlineData(null, false, true, false)]   // playtest: window
    [InlineData(false, false, false, false)] // saved choice wins in release
    [InlineData(true, true, false, true)]    // and in a plain dev run
    [InlineData(true, false, true, false)]   // but never in a playtest
    [InlineData(true, true, true, false)]
    public void Fullscreen_follows_mode_and_saved_choice(bool? saved, bool isDebug, bool isPlaytest, bool expected) =>
        Assert.Equal(expected, StartupDisplay.Fullscreen(saved, isDebug, isPlaytest));

    [Theory]
    [InlineData(720, 100)]
    [InlineData(1080, 100)]
    [InlineData(1399, 100)]
    [InlineData(1400, 150)]
    [InlineData(1600, 150)]
    [InlineData(1999, 150)]
    [InlineData(2000, 200)]
    [InlineData(2160, 200)]
    public void First_run_ui_scale_follows_screen_height_in_release(int height, int expected) =>
        Assert.Equal(expected, StartupDisplay.FirstRunUiScale(height, isDebug: false, isPlaytest: false));

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void First_run_ui_scale_is_100_in_dev_and_test(bool isDebug, bool isPlaytest) =>
        Assert.Equal(100, StartupDisplay.FirstRunUiScale(2160, isDebug, isPlaytest));
}
