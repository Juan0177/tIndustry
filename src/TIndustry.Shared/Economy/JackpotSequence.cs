namespace TIndustry.Shared;

/// <summary>The seven steps of the jackpot-attempt sequence, then <see cref="Finita"/> once it is over.</summary>
public enum JackpotStep { Zoom, CambioCanale, CorsaAlCore, Ritorno, Calcolo, Led, Risultato, Finita }

/// <summary>
/// Timeline of the 12-second jackpot-attempt sequence. Client presentation logic, not simulation: it runs on real
/// seconds (double) and must never be used from <c>Step()</c>. No Godot dependency. The constructor accepts any
/// <see cref="SaleResolved"/>; the caller decides when to start it.
/// </summary>
public sealed class JackpotSequence
{
    public const double TotalSeconds = 12;

    /// <summary>
    /// Seconds at the start in which a skip is ignored: a double click on «Vendi tutto» must not skip the sequence it
    /// just started.
    /// </summary>
    public const double SkipGuardSeconds = 0.2;

    private static readonly double[] Durations = { 1, 1, 2, 2, 2, 2, 2 };
    private static readonly double[] Starts = { 0, 1, 2, 4, 6, 8, 10 };

    private bool _closed;

    public JackpotSequence(SaleResolved sale)
    {
        Sale = sale ?? throw new ArgumentNullException(nameof(sale));
    }

    public SaleResolved Sale { get; }
    public bool Paused { get; set; }
    public double Elapsed { get; private set; }

    public static double StepSeconds(JackpotStep step) =>
        step == JackpotStep.Finita ? 0 : Durations[(int)step];

    public bool Finished => _closed || Elapsed >= TotalSeconds;
    public bool HidesHud => !Finished;
    public bool BlocksWorldInput => !Finished;

    public JackpotStep Step
    {
        get
        {
            if (Finished) return JackpotStep.Finita;
            for (var i = Starts.Length - 1; i >= 0; i--)
                if (Elapsed >= Starts[i]) return (JackpotStep)i;
            return JackpotStep.Zoom;
        }
    }

    /// <summary>Progress inside the current step, 0 to 1 (1 once finished).</summary>
    public double StepProgress
    {
        get
        {
            var step = Step;
            if (step == JackpotStep.Finita) return 1;
            var i = (int)step;
            return Math.Clamp((Elapsed - Starts[i]) / Durations[i], 0, 1);
        }
    }

    /// <summary>Moves the timeline forward; ignored while paused, finished, or for negative/NaN/infinite input.</summary>
    public void Advance(double realSeconds)
    {
        if (Paused || Finished) return;
        if (double.IsNaN(realSeconds) || double.IsInfinity(realSeconds) || realSeconds <= 0) return;
        Elapsed = Math.Min(Elapsed + realSeconds, TotalSeconds);
    }

    /// <summary>
    /// Skips: steps 1-6 jump to the start of the result; the result step ends the sequence. A won jackpot cannot be
    /// skipped, nor any sequence in its first <see cref="SkipGuardSeconds"/>. Returns whether anything changed.
    /// </summary>
    public bool TrySkip()
    {
        if (Sale.Jackpot || Finished || Elapsed < SkipGuardSeconds) return false;
        if (Step == JackpotStep.Risultato) Close();
        else Elapsed = Starts[(int)JackpotStep.Risultato];
        return true;
    }

    /// <summary>Ends the sequence at once (Home, load, new game, screen removed).</summary>
    public void Close() => _closed = true;
}
