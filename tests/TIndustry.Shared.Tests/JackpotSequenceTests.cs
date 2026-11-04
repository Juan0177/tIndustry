using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

public class JackpotSequenceTests
{
    private static SaleResolved Sale(bool jackpot = false) =>
        new(0, "copper", 500, 1000, 100, false, true, jackpot, 500);

    [Fact]
    public void DurationsAndTotal()
    {
        double[] expected = { 1, 1, 2, 2, 2, 2, 2 };
        for (var i = 0; i < 7; i++) Assert.Equal(expected[i], JackpotSequence.StepSeconds((JackpotStep)i));
        Assert.Equal(12, JackpotSequence.TotalSeconds);
        Assert.Equal(JackpotSequence.TotalSeconds, expected.Sum());
    }

    [Fact]
    public void AdvanceChangesStepExactlyAtBoundaries()
    {
        var s = new JackpotSequence(Sale());
        s.Advance(0.999);
        Assert.Equal(JackpotStep.Zoom, s.Step);
        s.Advance(0.001);
        Assert.Equal(JackpotStep.CambioCanale, s.Step);
        Assert.Equal(0, s.StepProgress, 6);
        s.Advance(1.0);
        Assert.Equal(JackpotStep.CorsaAlCore, s.Step);
        s.Advance(1.0);
        Assert.Equal(0.5, s.StepProgress, 6);
    }

    [Fact]
    public void LargeAdvanceReachesFinishedInOrder()
    {
        var s = new JackpotSequence(Sale());
        s.Advance(30);
        Assert.Equal(JackpotStep.Finita, s.Step);
        Assert.True(s.Finished);
        Assert.Equal(12, s.Elapsed);
    }

    [Fact]
    public void StepsAreVisitedInOrder()
    {
        var s = new JackpotSequence(Sale());
        var seen = new List<JackpotStep>();
        while (!s.Finished)
        {
            if (seen.Count == 0 || seen[^1] != s.Step) seen.Add(s.Step);
            s.Advance(0.25);
        }
        Assert.Equal(7, seen.Count);
        Assert.Equal(seen.OrderBy(x => x), seen);
    }

    [Fact]
    public void PausedStopsElapsed()
    {
        var s = new JackpotSequence(Sale());
        s.Advance(2);
        s.Paused = true;
        s.Advance(5);
        Assert.Equal(2, s.Elapsed);
        s.Paused = false;
        s.Advance(1);
        Assert.Equal(3, s.Elapsed);
    }

    [Fact]
    public void SkipFromEarlyStepJumpsToResult()
    {
        var s = new JackpotSequence(Sale());
        s.Advance(5);
        Assert.True(s.TrySkip());
        Assert.Equal(JackpotStep.Risultato, s.Step);
        Assert.Equal(0, s.StepProgress);
        Assert.Equal(10, s.Elapsed);
    }

    [Fact]
    public void SkipDuringResultFinishes()
    {
        var s = new JackpotSequence(Sale());
        s.Advance(11);
        Assert.True(s.TrySkip());
        Assert.Equal(JackpotStep.Finita, s.Step);
        Assert.False(s.TrySkip());
    }

    [Fact]
    public void WonJackpotCannotBeSkipped()
    {
        var s = new JackpotSequence(Sale(jackpot: true));
        s.Advance(3);
        Assert.False(s.TrySkip());
        Assert.Equal(JackpotStep.CorsaAlCore, s.Step);
        Assert.Equal(3, s.Elapsed);
    }

    [Fact]
    public void CloseFinishesAtOnce()
    {
        var s = new JackpotSequence(Sale(jackpot: true));
        s.Advance(1);
        s.Close();
        Assert.Equal(JackpotStep.Finita, s.Step);
        Assert.True(s.Finished);
    }

    [Fact]
    public void HudAndInputFlagsFollowSequence()
    {
        var s = new JackpotSequence(Sale());
        Assert.True(s.HidesHud);
        Assert.True(s.BlocksWorldInput);
        s.Advance(11.99);
        Assert.True(s.HidesHud);
        s.Advance(1);
        Assert.False(s.HidesHud);
        Assert.False(s.BlocksWorldInput);
    }

    [Fact]
    public void InvalidAdvanceIsIgnored()
    {
        var s = new JackpotSequence(Sale());
        s.Advance(1);
        s.Advance(-5);
        s.Advance(double.NaN);
        s.Advance(double.PositiveInfinity);
        s.Advance(double.NegativeInfinity);
        Assert.Equal(1, s.Elapsed);
        Assert.Equal(JackpotStep.CambioCanale, s.Step);
    }
}
