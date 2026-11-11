using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The separator's random output: one piece per craft, picked by
/// <c>SplitMix64(seed ^ (id × 0x9E3779B97F4A7C15) ^ itemsCrafted)</c> modulo the sum of the weights.
/// </summary>
public sealed class SeparatorTests
{
    private static readonly FactoryContent Content =
        FactoryContent.Load(Path.Combine(AppContext.BaseDirectory, "data", "content.json"));

    private static readonly RecipeDefinition Separate = Content.FindRecipe("separate-sand")!;

    private static List<string> Sequence(long seed, long id, int count) =>
        Enumerable.Range(0, count).Select(n => Crafter.PickRandomOutput(Separate, seed, id, n)).ToList();

    [Fact]
    public void SeparatorFrequencies()
    {
        const int draws = 20_000;
        var counts = Sequence(seed: 42, id: 17, draws).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(["copper-ore", "iron-ore", "lead-ore", "titanium-ore"], counts.Keys.Order());
        foreach (var ore in new[] { "iron-ore", "copper-ore", "lead-ore" })
        {
            Assert.InRange(counts[ore] * 100.0 / draws, 28.0, 32.0);
        }

        Assert.InRange(counts["titanium-ore"] * 100.0 / draws, 8.5, 11.5);
    }

    [Fact]
    public void SeparatorSameSeedSameSequence()
    {
        Assert.Equal(Sequence(7, 3, 50), Sequence(7, 3, 50));
        Assert.Equal(4, Sequence(7, 3, 50).Distinct().Count());
    }

    [Fact]
    public void DifferentSeedDifferentSequence()
    {
        Assert.NotEqual(Sequence(7, 3, 50), Sequence(8, 3, 50));
        // Another machine of the same game draws its own sequence.
        Assert.NotEqual(Sequence(7, 3, 50), Sequence(7, 4, 50));
    }

    [Fact]
    public void PickWalksTheOutputsInContentOrder()
    {
        // The formula, written out: the value modulo 100 falls in [0, 30) iron, [30, 60) copper, [60, 90) lead, then titanium.
        for (var n = 0; n < 200; n++)
        {
            var x = unchecked(5UL ^ (9UL * 0x9E3779B97F4A7C15UL) ^ (ulong)n);
            x = unchecked(x + 0x9E3779B97F4A7C15UL);
            x = unchecked((x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL);
            x = unchecked((x ^ (x >> 27)) * 0x94D049BB133111EBUL);
            x ^= x >> 31;
            var expected = (x % 100) switch
            {
                < 30 => "iron-ore",
                < 60 => "copper-ore",
                < 90 => "lead-ore",
                _ => "titanium-ore"
            };
            Assert.Equal(expected, Crafter.PickRandomOutput(Separate, 5, 9, n));
        }
    }

    [Fact]
    public void SeparatorMakesOnePieceAtATimeWithTheGameSeed()
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 7,
            rules: new GameRules(InstantBuild: true));
        factory.Research.ForceUnlock(Generator.BuildingId);
        factory.Research.ForceUnlock(Crafter.SeparatorId);
        var genAt = new GridPosition(10, 20);
        Assert.True(factory.PlaceInstant(new BuildSpec(Generator.BuildingId, BuildingKind.Generator, genAt, Direction.East), charge: false));
        factory.Generators.Single().RestoreFuel(Generator.FuelBufferCapacity, Generator.SecondsPerFuel);
        Assert.True(factory.PlaceInstant(
            new BuildSpec(Crafter.SeparatorId, BuildingKind.Separator, new GridPosition(12, 20), Direction.East), charge: false));
        var separator = factory.Machines.Single();
        var seed = factory.Terrain!.Seed;
        Assert.Equal(7, seed);

        var made = new List<string>();
        for (var craft = 0; craft < 5; craft++)
        {
            Assert.True(separator.TryAccept("sand") && separator.TryAccept("sand"));
            var ticks = 0;
            while (separator.OutputQueue.Count == 0)
            {
                factory.Step();
                Assert.True(++ticks <= 2 * SimulationClock.TickRate + 1, "2 s a corrente piena");
            }

            Assert.Single(separator.OutputQueue);
            Assert.Equal(craft + 1, separator.ItemsCrafted);
            var piece = separator.OutputQueue.Single();
            Assert.True(separator.TryTakeOutput(piece));
            made.Add(piece);
        }

        Assert.Equal(Sequence(seed, separator.Id, 5), made);
        // The instance overload draws with the machine's own id and count.
        Assert.Equal(Crafter.PickRandomOutput(Separate, 99, separator.Id, separator.ItemsCrafted),
            separator.PickRandomOutput(Separate, 99));
    }
}
