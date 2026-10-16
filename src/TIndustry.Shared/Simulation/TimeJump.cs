using System.Diagnostics;

namespace TIndustry.Shared;

/// <summary>
/// A jump forward in game time (+1h, +6h, <c>avanti</c>) run a slice per frame, so the client can pause the
/// game, show a progress bar and stay responsive instead of freezing for the whole jump.
/// </summary>
public sealed class TimeJump
{
    public const int MaxSeconds = 6 * 3600;

    /// <summary>Ticks between clock checks: cheap enough to keep each slice close to its budget.</summary>
    private const int TicksPerCheck = 60;

    public TimeJump(long totalTicks)
    {
        TotalTicks = Math.Max(0, totalTicks);
    }

    public static TimeJump FromSeconds(int seconds) =>
        new((long)Math.Clamp(seconds, 0, MaxSeconds) * SimulationClock.TickRate);

    public long TotalTicks { get; }

    public long DoneTicks { get; private set; }

    public bool IsComplete => DoneTicks >= TotalTicks;

    /// <summary>0..1 for the progress bar.</summary>
    public double Progress => TotalTicks == 0 ? 1 : DoneTicks / (double)TotalTicks;

    /// <summary>Game time for <paramref name="ticks"/>: <c>mm:ss</c>, or <c>h:mm:ss</c> from one hour.</summary>
    public static string FormatGameTime(long ticks)
    {
        var t = TimeSpan.FromSeconds(ticks / (double)SimulationClock.TickRate);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t:mm\\:ss}" : $"{t:mm\\:ss}";
    }

    /// <summary>Runs ticks until the jump is complete or <paramref name="budget"/> is spent; at least one check's worth.</summary>
    public void Advance(Factory factory, TimeSpan budget)
    {
        var watch = Stopwatch.StartNew();
        do
        {
            Advance(factory, TicksPerCheck);
        }
        while (!IsComplete && watch.Elapsed < budget);
    }

    /// <summary>Runs up to <paramref name="maxTicks"/> ticks of the jump.</summary>
    public void Advance(Factory factory, int maxTicks)
    {
        var ticks = Math.Min(maxTicks, TotalTicks - DoneTicks);
        for (var i = 0; i < ticks; i++)
        {
            factory.Step();
        }

        DoneTicks += Math.Max(0, ticks);
    }
}
