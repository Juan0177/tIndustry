using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

public sealed class MarketRandomTests
{
    [Fact]
    public void KnownSequence()
    {
        // Reference PCG32 (O'Neill's pcg32-demo: srandom(42, 54)), computed independently in Python.
        var random = new MarketRandom(42);
        Assert.Equal(
            new uint[] { 0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b },
            Enumerable.Range(0, 5).Select(_ => random.NextUInt()).ToArray());
    }

    [Fact]
    public void SameSeedSameSequence()
    {
        var a = new MarketRandom(7);
        var b = new MarketRandom(7);
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(a.NextUInt(), b.NextUInt());
        }
    }

    [Fact]
    public void FromStateContinuesTheSequence()
    {
        var a = new MarketRandom(9);
        a.NextUInt();
        var b = MarketRandom.FromState(a.State, a.Increment);
        Assert.Equal(a.NextUInt(), b.NextUInt());
    }

    [Fact]
    public void BoundedValuesStayInRange()
    {
        var random = new MarketRandom(1);
        for (var i = 0; i < 2000; i++)
        {
            Assert.InRange(random.NextInt(7), 0, 6);
            Assert.InRange(random.NextRange(-3, 3), -3, 3);
        }
    }
}
