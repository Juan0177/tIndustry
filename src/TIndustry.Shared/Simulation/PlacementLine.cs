namespace TIndustry.Shared;

/// <summary>
/// Shape of a placement drag: <see cref="Straight"/> along the dominant axis; <see cref="Diagonal"/> (Ctrl) follows the
/// belt chain between anchor and cursor or an A* path, belts only.
/// </summary>
public enum LineShape
{
    Straight,
    Diagonal
}

/// <summary>
/// A placement drag of <paramref name="StructureId"/> (a conveyor id or a building's structure id) pressed on
/// <paramref name="Anchor"/> and now on <paramref name="Cursor"/> (the same cell for a click). <paramref name="Brush"/>
/// is the player's direction (R); <paramref name="FixedDirection"/> (R pressed during the drag, the agents'
/// <c>direzione</c>) makes every cell face it, except on a Ctrl path or chain (<see cref="LineShape.Diagonal"/>).
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
    /// <summary>Longest line, in cells from the anchor: as many as an agent's reply lists.</summary>
    public const int MaxCells = AgentGame.MaxListItems;

    /// <summary>Most nodes the Ctrl path (A*) expands before falling back to the straight line.</summary>
    public const int MaxSearchNodes = 1000;

    private const int TurnCost = 8;
    private const int RefusedCellCost = 20;

    private static readonly string BridgeId = BuildSpec.StructureIdFor(BuildingKind.Bridge)!;

    /// <summary>
    /// The cells of <paramref name="request"/> in order from the anchor. Plain belts, junctions, splitters and sorters
    /// make a line facing the next cell (the last one: the plain belt it ends on, else the line's way; a lone cell: the
    /// brush); every other block is one cell on the anchor facing the drag. A plain belt inside the line over a
    /// perpendicular belt is a crossing; a belt whose last cell touches the Core faces it. A straight belt line jumps a
    /// run of walls, deep water or buildings with a bridge when it can (<see cref="Bridge"/>). Ctrl
    /// (<see cref="LineShape.Diagonal"/>, belts) follows the chain between anchor and cursor (<see cref="Chain"/>), else
    /// the A* path (<see cref="Path"/>), else the straight line.
    /// </summary>
    public static IReadOnlyList<LinePlacement> Plan(IGameView view, LineRequest request)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(request);
        var kind = BuildSpec.KindFor(request.StructureId);
        var belt = kind is null;
        var inLine = belt || kind is BuildingKind.Junction or BuildingKind.Splitter or BuildingKind.Sorter;
        var dragged = request.Cursor != request.Anchor;
        var ctrl = belt && dragged && request.Shape == LineShape.Diagonal;
        var chain = ctrl ? Chain(view, request.Anchor, request.Cursor) : null;
        var path = ctrl && chain is null ? Path(view, request) : null;
        IReadOnlyList<GridPosition> cells = !inLine ? [request.Anchor]
            : chain ?? path ?? Straight(request.Anchor, request.Cursor);
        var lineWay = dragged ? Dominant(request.Anchor, request.Cursor) : request.Brush;
        // A Ctrl path or chain lays a working line (Mindustry): R fixes the direction only of a straight line.
        var fixedWay = request.FixedDirection && chain is null && path is null;

        var placements = new List<LinePlacement>(cells.Count);
        for (var i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            var last = i == cells.Count - 1;
            var overBelt = view.Belts.TryGetValue(cell, out var under) && under.Kind == LogisticsKind.Belt;
            GridPosition? previous = i > 0 ? cells[i - 1] : null;
            GridPosition? next = last ? null : cells[i + 1];
            // A drag of a line has at least two cells: its last one over a plain belt takes that belt's way. A chain
            // keeps every cell's own way.
            var direction = chain is not null ? under!.Direction
                : fixedWay || !dragged ? request.Brush
                : inLine && last && overBelt ? under!.Direction
                : inLine ? next is { } ahead ? BeltLane.DirectionBetween(cell, ahead) : BeltLane.DirectionBetween(previous!.Value, cell)
                : lineWay;
            if (belt && last && !fixedWay && !overBelt
                && CoreStockSink.PreferDirectionIntoCore(cell, view.CoreTiles) is { } intoCore)
            {
                direction = intoCore;
            }

            var crossing = belt
                && BeltLine.IsInterior(previous, cell, next, direction)
                && BeltLine.MayCross(view, cell, direction);
            SimCommand command = belt
                ? new PlaceBelt(request.PlayerId, cell, direction, request.StructureId, crossing)
                : new PlaceBuilding(request.PlayerId, kind!.Value, cell, direction, request.RecipeId, request.FilterItemId);
            placements.Add(new LinePlacement(cell, direction, command, Foreseen(view, command, crossing)));
        }

        if (belt && dragged && chain is null && path is null
            && (!request.FixedDirection || request.Brush == lineWay)
            && view.IsStructureUnlocked(BridgeId))
        {
            Bridge(view, request.PlayerId, lineWay, placements);
        }

        // A crossing needs the line on both sides: beside a refused cell it is a plain belt over the belt instead.
        for (var i = 1; i < placements.Count - 1; i++)
        {
            if (placements[i].Command is PlaceBelt { Crossing: true } crossing
                && (placements[i - 1].Refusal is not null || placements[i + 1].Refusal is not null))
            {
                var plain = crossing with { Crossing = false };
                placements[i] = placements[i] with { Command = plain, Refusal = Foreseen(view, plain, crossing: false) };
            }
        }

        return placements;
    }

    /// <summary>
    /// Jumps each run of cells blocked by a wall, deep water or a building (or site) with a bridge facing
    /// <paramref name="way"/>: from the accepted cell before the run, when the bridge can stand there and its exit
    /// (<see cref="IGameView.Footprint"/>) is the first cell after the run, inside the line. The run and the exit are
    /// then spanned, with no command; otherwise the run stays refused.
    /// </summary>
    private static void Bridge(IGameView view, int playerId, Direction way, List<LinePlacement> placements)
    {
        bool Blocked(int i) =>
            placements[i].Refusal is RefusalCode.Wall or RefusalCode.DeepWater || view.IsBuildingTile(placements[i].Cell);

        for (var start = 1; start < placements.Count; start++)
        {
            if (!Blocked(start))
            {
                continue;
            }

            var after = start;
            while (after < placements.Count && Blocked(after))
            {
                after++;
            }

            var entry = placements[start - 1];
            var bridge = new PlaceBuilding(playerId, BuildingKind.Bridge, entry.Cell, way);
            if (after < placements.Count
                && entry.Refusal is null
                && !entry.Spanned
                && Foreseen(view, bridge, crossing: false) is null
                && view.Footprint(BuildSpec.FromCommand(bridge)) is [_, var exit]
                && exit == placements[after].Cell)
            {
                placements[start - 1] = new LinePlacement(entry.Cell, way, bridge, null);
                // The exit's own refusal (none, or a plan's) is cleared too: FindBridgeExit, behind Footprint and
                // CanPlace, proved the exit free and Foreseen checked the plans on both ends, so this holds only while
                // Footprint and CanPlace keep using the same exit rule.
                for (var k = start; k <= after; k++)
                {
                    placements[k] = placements[k] with { Command = null, Refusal = null, Spanned = true };
                }
            }

            start = after;
        }
    }

    /// <summary>
    /// The chain of plain belts from <paramref name="anchor"/> to <paramref name="cursor"/>, from the anchor: going down
    /// the chain (each belt to the cell it faces) from one end reaches the other within <see cref="MaxCells"/>.
    /// </summary>
    private static IReadOnlyList<GridPosition>? Chain(IGameView view, GridPosition anchor, GridPosition cursor)
    {
        List<GridPosition>? Down(GridPosition from, GridPosition to)
        {
            var cells = new List<GridPosition> { from };
            var at = from;
            while (cells.Count < MaxCells && view.Belts.TryGetValue(at, out var cell) && cell.Kind == LogisticsKind.Belt)
            {
                at = at.Step(cell.Direction);
                if (!view.Belts.TryGetValue(at, out var ahead) || ahead.Kind != LogisticsKind.Belt)
                {
                    return null;
                }

                cells.Add(at);
                if (at == to)
                {
                    return cells;
                }
            }

            return null;
        }

        if (Down(anchor, cursor) is { } downstream)
        {
            return downstream;
        }

        var upstream = Down(cursor, anchor);
        upstream?.Reverse();
        return upstream;
    }

    /// <summary>
    /// The cheapest path of at most <see cref="MaxCells"/> cells on the four sides from the anchor to the cursor (A*):
    /// 1 a step, <see cref="TurnCost"/> a turn, <see cref="RefusedCellCost"/> into a cell the belt would be refused on
    /// (not a belt over a belt); walls, deep water and cells off the map are not crossed. Ties go to the lower f, then
    /// g, then the first queued. Null when there is none within <see cref="MaxSearchNodes"/> expanded nodes.
    /// </summary>
    private static IReadOnlyList<GridPosition>? Path(IGameView view, LineRequest request)
    {
        var terrain = view.Terrain;
        bool Passable(GridPosition cell) => terrain is null || (terrain.InBounds(cell) && !terrain[cell].BlocksBuilding);
        int Estimate(GridPosition cell) =>
            Math.Abs(request.Cursor.X - cell.X) + Math.Abs(request.Cursor.Y - cell.Y);

        if (!Passable(request.Cursor))
        {
            return null;
        }

        // Search states are (cell, way in): a turn's cost depends on it. -1 = the anchor, entered from nowhere.
        var nodes = new List<(GridPosition Cell, int Way, int G, int Cells, int Parent)> { (request.Anchor, -1, 0, 1, -1) };
        var best = new Dictionary<(GridPosition, int), int> { [(request.Anchor, -1)] = 0 };
        var open = new PriorityQueue<int, (int F, int G, int Order)>();
        open.Enqueue(0, (Estimate(request.Anchor), 0, 0));
        var expanded = 0;
        while (open.TryDequeue(out var index, out var priority))
        {
            var node = nodes[index];
            if (priority.G > best[(node.Cell, node.Way)])
            {
                continue;
            }

            if (node.Cell == request.Cursor)
            {
                var path = new List<GridPosition>(node.Cells);
                for (var at = index; at >= 0; at = nodes[at].Parent)
                {
                    path.Add(nodes[at].Cell);
                }

                path.Reverse();
                return path;
            }

            if (++expanded > MaxSearchNodes)
            {
                return null;
            }

            if (node.Cells == MaxCells)
            {
                continue;
            }

            foreach (var way in DirectionMath.All)
            {
                var cell = node.Cell.Step(way);
                if (!Passable(cell))
                {
                    continue;
                }

                var g = node.G + 1
                    + (node.Way >= 0 && node.Way != (int)way ? TurnCost : 0)
                    + (Foreseen(view, new PlaceBelt(request.PlayerId, cell, way, request.StructureId), crossing: false) is null ? 0 : RefusedCellCost);
                if (best.TryGetValue((cell, (int)way), out var known) && known <= g)
                {
                    continue;
                }

                best[(cell, (int)way)] = g;
                nodes.Add((cell, (int)way, g, node.Cells + 1, index));
                open.Enqueue(nodes.Count - 1, (g + Estimate(cell), g, nodes.Count - 1));
            }
        }

        return null;
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
