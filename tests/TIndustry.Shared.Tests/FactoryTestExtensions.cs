using TIndustry.Shared;

namespace TIndustry.Shared.Tests;

/// <summary>Fixed-tick helpers for tests (the sim only advances through <see cref="Factory.Step"/>).</summary>
internal static class FactoryTestExtensions
{
    /// <summary>Runs <c>ceil(seconds / TickSeconds)</c> fixed ticks.</summary>
    public static void RunSeconds(this Factory factory, float seconds)
    {
        // Small epsilon so float noise (e.g. 10 / 0.016666668 = 599.99998) does not add a tick.
        var ticks = (int)Math.Ceiling(seconds / (double)SimulationClock.TickSeconds - 1e-4);
        factory.RunTicks(ticks);
    }

    public static void RunTicks(this Factory factory, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            factory.Step();
        }
    }
}
