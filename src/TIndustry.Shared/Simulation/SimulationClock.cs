namespace TIndustry.Shared;

/// <summary>
/// Fixed-step accumulator: turns variable frame times into a whole number of simulation ticks.
/// The simulation itself never sees frame time — only <see cref="Factory.Step"/> × N.
/// </summary>
public sealed class SimulationClock
{
    public const int TickRate = 60;
    public const float TickSeconds = 1f / TickRate;
    /// <summary>Upper bound per frame so a long hitch cannot trigger a catch-up spiral.</summary>
    public const int MaxTicksPerFrame = 5;

    // Accumulate in double against the exact tick length so 1/60 s frames give exactly one tick.
    private const double TickDuration = 1.0 / TickRate;

    private double accumulator;

    /// <summary>
    /// Fraction of the next tick already elapsed, in [0, 1): the client draws items between the last two ticks with it.
    /// Drawing only — it never enters <see cref="Factory.Step"/>, the snapshot or the checksum.
    /// </summary>
    public double Alpha => Math.Clamp(accumulator / TickDuration, 0.0, Math.BitDecrement(1.0));

    /// <summary>
    /// Adds <paramref name="frameSeconds"/> and returns how many ticks to run now
    /// (never more than <see cref="MaxTicksPerFrame"/>; time beyond the cap is dropped).
    /// </summary>
    public int Accumulate(double frameSeconds)
    {
        if (double.IsFinite(frameSeconds) && frameSeconds > 0)
        {
            accumulator += frameSeconds;
        }

        var ticks = 0;
        while (accumulator >= TickDuration && ticks < MaxTicksPerFrame)
        {
            accumulator -= TickDuration;
            ticks++;
        }

        if (accumulator >= TickDuration)
        {
            // Over the cap: drop whole ticks, keep only the sub-tick remainder.
            accumulator %= TickDuration;
        }

        return ticks;
    }
}
