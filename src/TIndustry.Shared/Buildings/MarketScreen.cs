namespace TIndustry.Shared;

/// <summary>Colour of the screen's LEDs after a "sell all" (see <see cref="MarketSales.LedFor"/>).</summary>
public enum ScreenLed
{
    /// <summary>Under the yellow size, no crash, the price fell at most <see cref="MarketRules.LedMildDropMilli"/>.</summary>
    Blue,

    /// <summary>Every other sale under the yellow size: a bigger fall or a crash.</summary>
    Purple,

    /// <summary>The 50/50 (from <see cref="MarketRules.LedYellowPieces"/> pieces, won or lost) or a jackpot.</summary>
    Yellow,
}

/// <summary>
/// Last "sell all" shown on the screen: item, money earned, LED colour, whether it was a jackpot and the tick of the sale.
/// </summary>
public sealed record ScreenSaleResult(string ItemId, long Income, ScreenLed Led, bool Jackpot, long Tick);

/// <summary>
/// 2×2 stock-exchange screen (spec 2a «Schermo di borsa»): no direction, no power, one per game, placed with a cell
/// touching the Core edge to edge. <see cref="Active"/> (finished and touching the Core) turns the jackpot on
/// (<see cref="GameState.HasMarketScreen"/>); <see cref="GameState.RefreshMarketScreen"/> recomputes it.
/// </summary>
public sealed class MarketScreen
{
    public const int Size = 2;
    public const string BuildingId = "market-screen";

    /// <summary>How long the client shows <see cref="LastResult"/> after its tick: 6 seconds.</summary>
    public const int ResultTicks = 6 * SimulationClock.TickRate;

    public MarketScreen(GridPosition position, long id = 0)
    {
        Id = id;
        Position = position;
    }

    /// <summary>Stable entity id assigned by <see cref="Factory"/> at creation (saved).</summary>
    public long Id { get; }

    /// <summary>Top-left cell.</summary>
    public GridPosition Position { get; }

    /// <summary>Market item the screen follows (null = none chosen yet); set with the <see cref="SetBlockFilter"/> command.</summary>
    public string? WatchedItemId { get; internal set; }

    /// <summary>
    /// Outcome of the last "sell all" made while the screen was active (null = none yet), written by
    /// <see cref="MarketSales.Sell"/>; the client shows it for <see cref="ResultTicks"/> from its tick.
    /// </summary>
    public ScreenSaleResult? LastResult { get; internal set; }

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
