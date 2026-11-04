namespace TIndustry.Shared;

/// <summary>
/// 2×2 stock-exchange screen (spec 2a «Schermo di borsa»): no direction, no power, one per game, placed with a cell
/// touching the Core edge to edge. <see cref="Active"/> (finished and touching the Core) turns the jackpot on
/// (<see cref="GameState.HasMarketScreen"/>); <see cref="GameState.RefreshMarketScreen"/> recomputes it.
/// </summary>
public sealed class MarketScreen
{
    public const int Size = 2;
    public const string BuildingId = "market-screen";

    public MarketScreen(GridPosition position, long id = 0)
    {
        Id = id;
        Position = position;
    }

    /// <summary>Stable entity id assigned by <see cref="Factory"/> at creation (saved).</summary>
    public long Id { get; }

    /// <summary>Top-left cell.</summary>
    public GridPosition Position { get; }

    /// <summary>Market item the screen follows (null = none chosen yet).</summary>
    public string? WatchedItemId { get; set; }

    /// <summary>True when the screen touches the Core edge to edge (set by <see cref="GameState.RefreshMarketScreen"/>).</summary>
    public bool Active { get; internal set; }

    public bool Occupies(GridPosition tile) => BuildingIo.Occupies(Position, Size, tile);

    public IEnumerable<GridPosition> OccupiedTiles()
    {
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                yield return new GridPosition(Position.X + x, Position.Y + y);
            }
        }
    }

    /// <summary>True when a cell of a 2×2 screen at <paramref name="origin"/> is next to (edge to edge) one of <paramref name="coreTiles"/>.</summary>
    public static bool TouchesCore(GridPosition origin, IReadOnlySet<GridPosition> coreTiles)
    {
        foreach (var (neighbor, _) in BuildingIo.PerimeterSlots(origin, Size))
        {
            if (coreTiles.Contains(neighbor))
            {
                return true;
            }
        }

        return false;
    }
}
