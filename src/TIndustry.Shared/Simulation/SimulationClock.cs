namespace TIndustry.Shared;

/// <summary>
/// Fixed-step accumulator: turns variable frame times into a whole number of simulation ticks.
/// The simulation itself never sees frame time — only <see cref="Factory.Step"/> × N.
/// </summary>
public sealed class SimulationClock
{
    public const int TickRate = 60;
    public const float TickSeconds = 1f / TickRate;
    /// <summary>Upper bound per frame at speed 1 so a long hitch cannot trigger a catch-up spiral.</summary>
    public const int MaxTicksPerFrame = 5;

    /// <summary>The speeds the clock accepts (dev mode): ×1, ×5, ×10, ×20.</summary>
    public static IReadOnlyList<int> Speeds { get; } = [1, 5, 10, 20];

    /// <summary>True when <paramref name="speed"/> is one of <see cref="Speeds"/>.</summary>
    public static bool IsValidSpeed(int speed) => speed is 1 or 5 or 10 or 20;

    // Accumulate in double against the exact tick length so 1/60 s frames give exactly one tick.
    private const double TickDuration = 1.0 / TickRate;

    private double accumulator;
    private int speed = 1;

    /// <summary>
    /// Game time per real time: each frame counts <c>Speed</c> times, and the per-frame cap scales with it
    /// (<see cref="MaxTicksThisFrame"/>). It only changes how many ticks run per frame, never what a tick does,
    /// so the state after N ticks is the same at every speed. Only values in <see cref="Speeds"/> are accepted.
    /// </summary>
    public int Speed
    {
        get => speed;
        set
        {
            if (!IsValidSpeed(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Velocità ammesse: 1, 5, 10, 20.");
            }

            speed = value;
        }
    }

    /// <summary>Tick cap of one frame at the current speed: <see cref="MaxTicksPerFrame"/> × <see cref="Speed"/>.</summary>
    public int MaxTicksThisFrame => MaxTicksPerFrame * speed;

    /// <summary>True when the last <see cref="Accumulate"/> dropped whole ticks over <see cref="MaxTicksThisFrame"/> (a hitch).</summary>
    public bool DroppedTicks { get; private set; }

    /// <summary>
    /// Fraction of the next tick already elapsed, in [0, 1): the client draws items between the last two ticks with it.
    /// Drawing only — it never enters <see cref="Factory.Step"/>, the snapshot or the checksum.
    /// </summary>
    public double Alpha => Math.Clamp(accumulator / TickDuration, 0.0, Math.BitDecrement(1.0));

    /// <summary>
    /// Adds <paramref name="frameSeconds"/> (× <see cref="Speed"/>) and returns how many ticks to run now
    /// (never more than <see cref="MaxTicksThisFrame"/>; time beyond the cap is dropped). A paused game passes 0.
    /// </summary>
    public int Accumulate(double frameSeconds)
    {
        var add = double.IsFinite(frameSeconds) && frameSeconds > 0 ? frameSeconds : 0.0;
        var cap = MaxTicksThisFrame;
        var ticks = 0;
        // At speed N the frame counts as N frames of the same length, each drained in turn: exactly the ticks
        // N separate frames would give at speed 1 (no rounding drift from multiplying the frame time).
        for (var part = 0; part < speed; part++)
        {
            accumulator += add;
            while (accumulator >= TickDuration && ticks < cap)
            {
                accumulator -= TickDuration;
                ticks++;
            }
        }

        DroppedTicks = accumulator >= TickDuration;
        if (DroppedTicks)
        {
            // Over the cap: drop whole ticks, keep only the sub-tick remainder.
            accumulator %= TickDuration;
        }

        return ticks;
    }
}
