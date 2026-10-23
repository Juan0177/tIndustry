namespace TIndustry.Shared;

/// <summary>World time chosen when the world is made (see <see cref="DayNightClock"/>).</summary>
public enum DayNightMode
{
    /// <summary>Day and night follow each other (<see cref="DayNightClock.CycleTicks"/>).</summary>
    Cycle,

    /// <summary>Always day ("Pacifica"): no count, never an attack.</summary>
    AlwaysDay,

    /// <summary>Always night: a count from the first block to the first attack (<see cref="FirstAttackDue"/>).</summary>
    AlwaysNight
}

/// <summary>
/// Per-game rules (Mindustry's <c>Rules</c>).
/// <paramref name="RefundPercent"/> (0–100) scales what deconstructing a finished building gives back;
/// <paramref name="InstantBuild"/> completes plans in the tick they are applied (tests, demo, dev seeds);
/// <paramref name="DayNight"/> is the world's time (<see cref="DayNightClock"/>; it changes no game rule until the attacks exist).
/// </summary>
public sealed record GameRules(int RefundPercent = 100, bool InstantBuild = false, DayNightMode DayNight = DayNightMode.Cycle);
