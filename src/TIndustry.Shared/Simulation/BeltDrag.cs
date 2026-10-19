namespace TIndustry.Shared;

/// <summary>
/// The automatic crossing of a placement drag, one cell at a time: the client feeds it the cells of a mouse drag,
/// the agents' <c>piazza</c> a whole line, so both send the same <see cref="PlaceBelt"/> cells with the same
/// <see cref="PlaceBelt.Crossing"/> flag. A belt cell over a perpendicular belt (<see cref="BeltLine.MayCross"/>) is held
/// back until the next cell: then the next cell goes first and the held one follows, as a crossing when it lies inside
/// the line (<see cref="BeltLine.IsInterior"/>); the simulation turns it into a junction, or without the junction
/// turns the crossed belt in place so its line merges into the new one (<see cref="BeltOverBelt.Rotate"/>).
/// A held cell the drag never passes is dropped.
/// </summary>
public sealed class BeltDrag
{
    private GridPosition? previous;
    private (GridPosition Cell, GridPosition? Previous)? held;

    /// <summary>The cell held back for now (the client draws a junction ghost there).</summary>
    public GridPosition? HeldCell => held?.Cell;

    /// <summary>Starts a drag on the press cell, which is always placed as it is (never a crossing).</summary>
    public void Begin(GridPosition cell)
    {
        previous = cell;
        held = null;
    }

    /// <summary>
    /// The cells to place, in order, once the drag moved onto <paramref name="cell"/>. <paramref name="belts"/> false
    /// (junctions, splitters, sorters dragged in a line): every cell is placed at once.
    /// </summary>
    public List<(GridPosition Cell, bool Crossing)> Next(IGameView view, GridPosition cell, Direction direction, bool belts)
    {
        ArgumentNullException.ThrowIfNull(view);
        var place = new List<(GridPosition, bool)>();
        var mayCross = belts && BeltLine.MayCross(view, cell, direction);
        if (held is { } h)
        {
            held = null;
            if (mayCross)
            {
                place.Add((h.Cell, false)); // two crossings in a row: the first one is not inside a line of belts
            }
            else
            {
                place.Add((cell, false));
                place.Add((h.Cell, BeltLine.IsInterior(h.Previous, h.Cell, cell, direction)));
                previous = cell;
                return place;
            }
        }

        if (mayCross)
        {
            held = (cell, previous);
        }
        else
        {
            place.Add((cell, false));
        }

        previous = cell;
        return place;
    }

    /// <summary>
    /// A whole drag from <paramref name="from"/> to <paramref name="to"/> (<see cref="DragLine.Cells"/>): the cells to
    /// place in order, and the last cell when it was held and dropped (null otherwise).
    /// </summary>
    public static (List<(GridPosition Cell, bool Crossing)> Place, GridPosition? Dropped) Line(
        IGameView view, GridPosition from, GridPosition to, Direction direction, bool belts)
    {
        var drag = new BeltDrag();
        drag.Begin(from);
        var place = new List<(GridPosition Cell, bool Crossing)> { (from, false) };
        foreach (var cell in DragLine.Cells(from, to))
        {
            place.AddRange(drag.Next(view, cell, direction, belts));
        }

        return (place, drag.HeldCell);
    }
}
