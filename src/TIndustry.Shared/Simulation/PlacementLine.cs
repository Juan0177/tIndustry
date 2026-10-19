namespace TIndustry.Shared;

/// <summary>Shape of a placement drag: <see cref="Straight"/> along the dominant axis (diagonal lines come later).</summary>
public enum LineShape
{
    Straight,
    Diagonal
}

/// <summary>
/// A placement drag of <paramref name="StructureId"/> (a conveyor id or a building's structure id) pressed on
/// <paramref name="Anchor"/> and now on <paramref name="Cursor"/> (the same cell for a click). <paramref name="Brush"/>
/// is the player's direction (R); <paramref name="FixedDirection"/> (R pressed during the drag, the agents'
/// <c>direzione</c>) makes every cell face it.
/// </summary>
public sealed record LineRequest(
    int PlayerId,
    string StructureId,
    GridPosition Anchor,
    GridPosition Cursor,
    Direction Brush,
    bool FixedDirection = false,
    LineShape Shape = LineShape.Straight,
    string? RecipeId = null,
    string? FilterItemId = null);

/// <summary>
/// One cell of a <see cref="PlacementLine"/>: the direction it faces and the command it sends. <paramref name="Refusal"/>
/// is the refusal the line already foresees (the cell is shown red and not sent; <paramref name="Command"/> is then the
/// command it would have been). <paramref name="Spanned"/>: a cell a bridge passes over, with no command.
/// </summary>
public sealed record LinePlacement(
    GridPosition Cell,
    Direction Direction,
    SimCommand? Command,
    RefusalCode? Refusal,
    bool Spanned = false);

/// <summary>
/// Mindustry's placement line, shared by the client's drag and the agents' <c>piazza</c> so both send the same
/// commands. Pure: it reads the view and changes nothing (no time, no unordered collections).
/// </summary>
public static class PlacementLine
{
    /// <summary>Longest line, in cells from the anchor (= <see cref="AgentGame.MaxListItems"/>).</summary>
    public const int MaxCells = 200;

    /// <summary>
    /// The cells of <paramref name="request"/> in order from the anchor. Plain belts, junctions, splitters and sorters
    /// make a line facing the next cell (the last one: the plain belt it ends on, else the line's way; a lone cell: the
    /// brush); every other block is one cell on the anchor facing the drag. A plain belt inside the line over a
    /// perpendicular belt is a crossing; a belt whose last cell touches the Core faces it.
    /// </summary>
    public static IReadOnlyList<LinePlacement> Plan(IGameView view, LineRequest request)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(request);
        var kind = BuildSpec.KindFor(request.StructureId);
        var belt = kind is null;
        var inLine = belt || kind is BuildingKind.Junction or BuildingKind.Splitter or BuildingKind.Sorter;
        // shortcut: LineShape.Diagonal is planned as Straight until the diagonal line lands.
        IReadOnlyList<GridPosition> cells = inLine ? Straight(request.Anchor, request.Cursor) : [request.Anchor];
        var dragged = request.Cursor != request.Anchor;
        var lineWay = dragged ? Dominant(request.Anchor, request.Cursor) : request.Brush;

        var placements = new List<LinePlacement>(cells.Count);
        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            var last = i == cells.Count - 1;
            var overBelt = view.Belts.TryGetValue(cell, out var under) && under.Kind == LogisticsKind.Belt;
            // A drag of a line has at least two cells: its last one over a plain belt takes that belt's way.
            var direction = request.FixedDirection || !dragged ? request.Brush
                : inLine && last && overBelt ? under!.Direction
                : lineWay;
            if (belt && last && !request.FixedDirection && !overBelt
                && CoreStockSink.PreferDirectionIntoCore(cell, view.CoreTiles) is { } intoCore)
            {
                direction = intoCore;
            }

            GridPosition? previous = i > 0 ? cells[i - 1] : null;
            GridPosition? next = last ? null : cells[i + 1];
            var crossing = belt
                && BeltLine.IsInterior(previous, cell, next, direction)
                && BeltLine.MayCross(view, cell, direction);
            SimCommand command = belt
                ? new PlaceBelt(request.PlayerId, cell, direction, request.StructureId, crossing)
                : new PlaceBuilding(request.PlayerId, kind!.Value, cell, direction, request.RecipeId, request.FilterItemId);
            placements.Add(new LinePlacement(cell, direction, command, Foreseen(view, command, crossing)));
        }

        return placements;
    }

    /// <summary>The commands to send for <paramref name="line"/>: its accepted cells, in line order.</summary>
    public static IReadOnlyList<SimCommand> Commands(IReadOnlyList<LinePlacement> line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return line.Where(p => p.Refusal is null && !p.Spanned && p.Command is not null).Select(p => p.Command!).ToList();
    }

    /// <summary>
    /// Cells from <paramref name="anchor"/> towards <paramref name="cursor"/> along the dominant axis (ties go to x, as
    /// in <see cref="DragLine"/>), on the anchor's row or column, at most <see cref="MaxCells"/> of them.
    /// </summary>
    public static IReadOnlyList<GridPosition> Straight(GridPosition anchor, GridPosition cursor)
    {
        long dx = (long)cursor.X - anchor.X, dy = (long)cursor.Y - anchor.Y;
        var alongX = Math.Abs(dx) >= Math.Abs(dy);
        var length = (int)Math.Min(Math.Abs(alongX ? dx : dy), MaxCells - 1);
        var step = Math.Sign(alongX ? dx : dy);
        var cells = new List<GridPosition>(length + 1);
        for (var k = 0; k <= length; k++)
        {
            cells.Add(alongX ? new GridPosition(anchor.X + step * k, anchor.Y) : new GridPosition(anchor.X, anchor.Y + step * k));
        }

        return cells;
    }

    /// <summary>The way from <paramref name="from"/> to <paramref name="to"/> along the dominant axis (ties go to x).</summary>
    private static Direction Dominant(GridPosition from, GridPosition to)
    {
        long dx = (long)to.X - from.X, dy = (long)to.Y - from.Y;
        return Math.Abs(dx) >= Math.Abs(dy)
            ? dx > 0 ? Direction.East : Direction.West
            : dy > 0 ? Direction.South : Direction.North;
    }

    /// <summary>
    /// The refusal the simulation would give that the line can already tell: none for a crossing or a belt over a belt
    /// (it turns, merges or changes tier), else <see cref="IGameView.CanPlace"/>'s reason, or Occupied over a plan.
    /// </summary>
    private static RefusalCode? Foreseen(IGameView view, SimCommand command, bool crossing)
    {
        var spec = command is PlaceBelt placeBelt ? BuildSpec.FromCommand(placeBelt) : BuildSpec.FromCommand((PlaceBuilding)command);
        if (spec.Kind is null && (crossing || view.BeltOverBeltAt(spec.Origin, spec.Direction, spec.StructureId) != BeltOverBelt.None))
        {
            return null;
        }

        if (!view.CanPlace(spec, out var reason))
        {
            return reason ?? RefusalCode.Occupied;
        }

        return view.Footprint(spec).Any(view.IsPlannedTile) ? RefusalCode.Occupied : null;
    }
}
