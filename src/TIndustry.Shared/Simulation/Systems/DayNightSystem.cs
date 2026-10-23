namespace TIndustry.Shared;

/// <summary>
/// World time, last: the clock itself is a pure function of the state (<see cref="DayNightClock"/>), so this system
/// only raises <see cref="FirstAttackDue"/> once in always night, in the first tick at or past the attack time (a
/// tick is never skipped, whatever the clock speed).
/// </summary>
public sealed class DayNightSystem : ISimulationSystem
{
    public string Name => "DayNight";

    public void Run(GameState state, SystemContext ctx)
    {
        if (state.FirstAttackRaised
            || !DayNightClock.FirstAttackDueAt(state.Rules.DayNight, state.FirstBuildTick, state.CurrentTick))
        {
            return;
        }

        state.FirstAttackRaised = true;
        state.Events.Add(new FirstAttackDue(0));
    }
}
