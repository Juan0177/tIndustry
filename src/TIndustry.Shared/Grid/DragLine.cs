namespace TIndustry.Shared;

/// <summary>Cells a placement drag crosses (pure; the agents' <c>piazza</c> line uses it).</summary>
public static class DragLine
{
    /// <summary>
    /// Cells from <paramref name="from"/> (excluded) to <paramref name="to"/> (included), in order: a 4-connected
    /// line (one axis per step, as Mindustry does for belts), so a diagonal drag never leaves corner-only gaps.
    /// Same algorithm as the client's <c>World.DragCells</c>.
    /// </summary>
    public static List<GridPosition> Cells(GridPosition from, GridPosition to)
    {
        var cells = new List<GridPosition>();
        int x = from.X, y = from.Y;
        int dx = Math.Abs(to.X - x), dy = Math.Abs(to.Y - y);
        int sx = x < to.X ? 1 : -1, sy = y < to.Y ? 1 : -1;
        int ix = 0, iy = 0;
        while (ix < dx || iy < dy)
        {
            // Step along the axis that lags behind the ideal line; ties go to x.
            if ((1 + 2 * ix) * dy <= (1 + 2 * iy) * dx && ix < dx)
            {
                x += sx;
                ix++;
            }
            else
            {
                y += sy;
                iy++;
            }

            cells.Add(new GridPosition(x, y));
        }

        return cells;
    }
}
