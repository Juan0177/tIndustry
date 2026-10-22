namespace TIndustry.Shared;

/// <summary>
/// PCG32 (XSH-RR) written in the project so the stream never changes with the .NET version. All the
/// randomness of the market goes through one instance, in a fixed order.
/// </summary>
public sealed class MarketRandom
{
    private const ulong Multiplier = 6364136223846793005UL;
    private const ulong DefaultStream = 54;

    private ulong state;
    private readonly ulong inc;

    public MarketRandom(ulong seed)
    {
        inc = (DefaultStream << 1) | 1;
        NextUInt();
        state += seed;
        NextUInt();
    }

    private MarketRandom(ulong state, ulong inc)
    {
        this.state = state;
        this.inc = inc | 1;
    }

    public ulong State => state;

    public ulong Increment => inc;

    public static MarketRandom FromState(ulong state, ulong inc) => new(state, inc);

    public uint NextUInt()
    {
        var old = state;
        state = unchecked(old * Multiplier + inc);
        var xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        var rot = (int)(old >> 59);
        return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
    }

    /// <summary>Uniform in [0, <paramref name="exclusiveMax"/>) without modulo bias.</summary>
    public int NextInt(int exclusiveMax)
    {
        if (exclusiveMax <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
        }

        var bound = (uint)exclusiveMax;
        var threshold = unchecked(0u - bound) % bound;
        while (true)
        {
            var r = NextUInt();
            if (r >= threshold)
            {
                return (int)(r % bound);
            }
        }
    }

    /// <summary>Uniform in [<paramref name="min"/>, <paramref name="maxInclusive"/>].</summary>
    public int NextRange(int min, int maxInclusive) => min + NextInt(maxInclusive - min + 1);
}
