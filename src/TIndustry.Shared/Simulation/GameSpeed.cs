namespace TIndustry.Shared;

/// <summary>
/// Player-controlled simulation speed: pause (Space), ×1 / ×10 / ×60 from the speed bar, single ticks while
/// paused. Turns frame time into fixed ticks through a <see cref="SimulationClock"/>.
/// </summary>
public sealed class GameSpeed
{
    public const float MinSpeed = 0.1f;
    public const float MaxSpeed = 60f;

    /// <summary>Speeds the dev hotkeys step through (slower / faster).</summary>
    public static readonly float[] SpeedSteps = [0.25f, 0.5f, 1f, 2f, 5f, 10f, 30f, 60f];

    /// <summary>Speeds offered by the on-screen speed bar.</summary>
    public static readonly float[] BarSpeeds = [1f, 10f, 60f];

    private float speed = 1f;
    private int pendingSteps;

    public float Speed => speed;

    public bool Paused { get; private set; }

    /// <summary>Sets the speed and resumes; 0 or less pauses instead.</summary>
    public void SetSpeed(float value)
    {
        if (value <= 0f)
        {
            Paused = true;
            return;
        }

        speed = Math.Clamp(value, MinSpeed, MaxSpeed);
        Paused = false;
    }

    public void SetPaused(bool paused) => Paused = paused;

    public void TogglePause() => Paused = !Paused;

    /// <summary>Next lower (<paramref name="direction"/> &lt; 0) or higher step of <see cref="SpeedSteps"/>.</summary>
    public void StepSpeed(int direction)
    {
        speed = direction < 0
            ? SpeedSteps.LastOrDefault(s => s < speed - 0.001f, SpeedSteps[0])
            : SpeedSteps.FirstOrDefault(s => s > speed + 0.001f, SpeedSteps[^1]);
    }

    /// <summary>Queue single ticks to run while paused (pauses first if running).</summary>
    public void QueueSteps(int ticks)
    {
        Paused = true;
        pendingSteps += Math.Max(0, ticks);
    }

    /// <summary>
    /// Ticks to run this frame: frame time scaled by <see cref="Speed"/>, the per-frame cap scaled with it
    /// so 10× is not clipped to 5 ticks; while paused only the queued single steps.
    /// </summary>
    public int TicksForFrame(SimulationClock clock, double frameSeconds)
    {
        if (Paused)
        {
            var steps = pendingSteps;
            pendingSteps = 0;
            return steps;
        }

        var cap = (int)Math.Ceiling(SimulationClock.MaxTicksPerFrame * Math.Max(1f, speed));
        return clock.Accumulate(frameSeconds * speed, cap);
    }
}
