namespace TIndustry.Shared;

/// <summary>Edge of the square maps of a normal game.</summary>
public enum MapSize
{
    Media = 1000,
    Grande = 1500,
    Enorme = 2000
}

/// <summary>
/// Inputs of <see cref="MapGenerator"/>: size, seed, allowed biomes (ids of <see cref="TerrainTable.Biomes"/>
/// but <c>acqua</c>) and how much wall, water and ore to place, in percent of the normal amount.
/// </summary>
public sealed record MapGenSettings(
    int Width,
    int Height,
    int Seed,
    IReadOnlyList<string> Biomes,
    int WallDensityPercent = 100,
    int WaterPercent = 100,
    int DepositPercent = 100)
{
    /// <summary>The four land biomes, in the order the generator numbers them.</summary>
    public static readonly IReadOnlyList<string> AllBiomes = ["rocce", "sabbia", "neve", "prati"];

    /// <summary>A normal game: square map of <paramref name="size"/> with every biome.</summary>
    public static MapGenSettings Normal(MapSize size, int seed) => new((int)size, (int)size, seed, AllBiomes);
}
