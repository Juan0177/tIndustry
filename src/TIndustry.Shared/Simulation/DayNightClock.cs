namespace TIndustry.Shared;

/// <summary>Phase of the world's time (<see cref="DayNightClock.PhaseAt"/>).</summary>
public enum DayNightPhase
{
    Day,
    Dusk,
    Night,
    Dawn
}

/// <summary>
/// The world's time, a pure function of the rule (<see cref="GameRules.DayNight"/>), the first block's tick
/// (<see cref="GameState.FirstBuildTick"/>) and the tick: integers only. The time is <c>t = tick − firstBuildTick</c>;
/// before the first block it does not run. The cycle lasts <see cref="CycleTicks"/>: Day, then Dusk (light fading),
/// Night, then Dawn (light coming back), then Day again.
/// </summary>
public static class DayNightClock
{
    /// <summary>Whole cycle: 14 minutes at 60 ticks per second.</summary>
    public const long CycleTicks = 50_400;

    /// <summary>Start of Dusk in the cycle (9:40).</summary>
    public const long DuskStart = 34_800;

    /// <summary>Start of Night in the cycle (10:00).</summary>
    public const long NightStart = 36_000;

    /// <summary>Start of Dawn in the cycle (13:40).</summary>
    public const long DawnStart = 49_200;

    /// <summary>Length of Dusk and Dawn (20 seconds).</summary>
    public const long FadeTicks = 1_200;

    /// <summary>Always night: ticks from the first block to the first attack (10 minutes).</summary>
    public const long FirstAttackTicks = 36_000;

    /// <summary>Light of full day; night is 0.</summary>
    public const int FullLight = 1000;

    /// <summary>
    /// Phase at <paramref name="tick"/>. Before the first block (or before its tick) the cycle is Day; always day and
    /// always night do not depend on the first block.
    /// </summary>
    public static DayNightPhase PhaseAt(DayNightMode mode, long? firstBuildTick, long tick)
    {
        switch (mode)
        {
            case DayNightMode.AlwaysDay:
                return DayNightPhase.Day;
            case DayNightMode.AlwaysNight:
                return DayNightPhase.Night;
        }

        if (CycleTime(firstBuildTick, tick) is not { } t)
        {
            return DayNightPhase.Day;
        }

        return t switch
        {
            < DuskStart => DayNightPhase.Day,
            < NightStart => DayNightPhase.Dusk,
            < DawnStart => DayNightPhase.Night,
            _ => DayNightPhase.Dawn
        };
    }

    /// <summary>Light at <paramref name="tick"/>: <see cref="FullLight"/> by day, 0 by night, linear through Dusk and Dawn.</summary>
    public static int LightAt(DayNightMode mode, long? firstBuildTick, long tick)
    {
        var phase = PhaseAt(mode, firstBuildTick, tick);
        switch (phase)
        {
            case DayNightPhase.Day:
                return FullLight;
            case DayNightPhase.Night:
                return 0;
        }

        // Only the cycle has Dusk and Dawn, and only once the time runs.
        var t = CycleTime(firstBuildTick, tick)!.Value;
        var faded = phase == DayNightPhase.Dusk ? t - DuskStart : t - DawnStart;
        var rising = (int)(faded * FullLight / FadeTicks);
        return phase == DayNightPhase.Dusk ? FullLight - rising : rising;
    }

    /// <summary>
    /// Ticks to the next change the HUD counts down: in the cycle, to the next Night during Day and Dusk and to the
    /// next Day during Night and Dawn; in always night, to the first attack until it is due, then null. Null in always
    /// day and before the first block.
    /// </summary>
    public static long? TicksToNextChange(DayNightMode mode, long? firstBuildTick, long tick)
    {
        if (mode == DayNightMode.AlwaysDay || Elapsed(firstBuildTick, tick) is not { } elapsed)
        {
            return null;
        }

        if (mode == DayNightMode.AlwaysNight)
        {
            return elapsed < FirstAttackTicks ? FirstAttackTicks - elapsed : null;
        }

        var t = elapsed % CycleTicks;
        return t < NightStart ? NightStart - t : CycleTicks - t;
    }

    /// <summary>True during Night proper (Dusk over, Dawn not yet begun): the time of the attacks.</summary>
    public static bool IsNight(DayNightMode mode, long? firstBuildTick, long tick) =>
        PhaseAt(mode, firstBuildTick, tick) == DayNightPhase.Night;

    /// <summary>True when the first attack of always night is due at <paramref name="tick"/> (or overdue).</summary>
    internal static bool FirstAttackDueAt(DayNightMode mode, long? firstBuildTick, long tick) =>
        mode == DayNightMode.AlwaysNight && Elapsed(firstBuildTick, tick) >= FirstAttackTicks;

    private static long? Elapsed(long? firstBuildTick, long tick) =>
        firstBuildTick is { } first && tick >= first ? tick - first : null;

    private static long? CycleTime(long? firstBuildTick, long tick) =>
        Elapsed(firstBuildTick, tick) is { } elapsed ? elapsed % CycleTicks : null;
}
