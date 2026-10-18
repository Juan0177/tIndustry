using Godot;
using TIndustry.Shared;

namespace TIndustry.Godot;

/// <summary>
/// Ambient particles of the biomes (G3): snowflakes on snow, sand grains blown East over the desert, leaves drifting
/// on the meadows (rocks and water have none: the water ripples are in the terrain shader). A few bursts per second
/// on random land cells of the view, so the cost stays flat whatever the map size or zoom; the effects are
/// <c>qualita: Alti</c> in <c>effetti.json</c>, so Bassi and Spenti draw none. Looks only: the randomness is the
/// client's own and never reaches the simulation.
/// </summary>
public sealed class AmbientEffects
{
    /// <summary>Effect of each biome that has one (biome ids of <c>content.json</c>).</summary>
    private static readonly Dictionary<string, string> EffectOfBiome = new(StringComparer.Ordinal)
    {
        ["neve"] = "neve",
        ["sabbia"] = "sabbia",
        ["prati"] = "foglie"
    };

    /// <summary>One burst per second for this many visible cells (about 1.5 at zoom 1 on 1280×720)...</summary>
    private const double CellsPerBurstPerSecond = 150;

    /// <summary>...but never more than this many per second (zoomed out on a big map).</summary>
    private const double MaxBurstsPerSecond = 10;

    /// <summary>A burst spreads over a square of this many cells around the picked one.</summary>
    private const float BurstCells = 2f;

    private readonly Random _random = new(20261018);
    private TerrainTable? _table;
    private string?[] _effectOfFloor = [];
    private double _due;

    /// <summary>
    /// Plays the bursts due this frame on <paramref name="map"/> within <paramref name="cells"/> (the view in cells).
    /// No map (or effects off) = nothing, and nothing piles up for later.
    /// </summary>
    public void Sync(EffectManager fx, TerrainMap? map, TerrainTable? table, Rect2I cells, int tileSize, double delta)
    {
        if (map is null || table is null || fx.Quality == EffectsQuality.Spenti || cells.Size.X <= 0 || cells.Size.Y <= 0)
        {
            _due = 0;
            return;
        }

        if (!ReferenceEquals(table, _table))
        {
            _table = table;
            var kinds = Enum.GetValues<FloorKind>();
            _effectOfFloor = new string?[kinds.Max(k => (int)k) + 1];
            foreach (var kind in kinds)
            {
                _effectOfFloor[(int)kind] = table.Floor(kind) is { } floor && EffectOfBiome.TryGetValue(floor.Biome, out var name)
                    ? name
                    : null;
            }
        }

        var rate = Math.Min(MaxBurstsPerSecond, (double)cells.Size.X * cells.Size.Y / CellsPerBurstPerSecond);
        // A long frame (loading, a stall) does not turn into a flurry.
        _due = Math.Min(_due + rate * delta, 3);
        var half = Vector2.One * (BurstCells * tileSize * 0.5f);
        while (_due >= 1)
        {
            _due -= 1;
            var x = cells.Position.X + _random.Next(cells.Size.X);
            var y = cells.Position.Y + _random.Next(cells.Size.Y);
            if ((uint)x >= (uint)map.Width || (uint)y >= (uint)map.Height)
            {
                continue;
            }

            var tile = map[x, y];
            if (tile.Wall != WallKind.None || _effectOfFloor[(int)tile.Floor] is not { } effect)
            {
                continue;
            }

            fx.Play(effect, new Vector2((x + 0.5f) * tileSize, (y + 0.5f) * tileSize), 0f, half);
        }
    }
}
