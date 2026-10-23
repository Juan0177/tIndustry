namespace TIndustry.Shared;

/// <summary>
/// Core stock sink: items ready to leave a belt cell whose output tile is in the core
/// set enter the Core's storage unless their item is at the maximum.
/// </summary>
public static class CoreStockSink
{
    public static HashSet<GridPosition> MakeCoreTiles(GridPosition origin, int size = 3)
    {
        var tiles = new HashSet<GridPosition>();
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                tiles.Add(new GridPosition(origin.X + x, origin.Y + y));
            }
        }

        return tiles;
    }

    /// <summary>
    /// Delivers the ready outputs of every belt cell that points into the Core (or dead-ends against it) through
    /// <see cref="GameState.TryAcceptAtCore"/>, in ascending cell order. A piece the Core does not take (its item
    /// is at the maximum) stays on the cell, blocking the pieces behind it; the next cell is tried.
    /// Returns how many pieces were delivered.
    /// </summary>
    public static int Drain(GameState state)
    {
        var grid = state.Belts;
        var coreTiles = state.CoreTiles;
        var delivered = 0;
        foreach (var cell in grid.Cells.Values)
        {
            // Face into core, or dead-end against core (wrong facing / no next belt).
            var outInCore = coreTiles.Contains(cell.OutputPosition);
            var sinksHere = outInCore
                || (IsEdgeAdjacentToCore(cell.Position, coreTiles)
                    && !grid.Contains(cell.OutputPosition));
            if (!sinksHere)
            {
                continue;
            }

            while (cell.PeekOutput() is { } item && state.TryAcceptAtCore(item.ItemId))
            {
                cell.RemoveOutput();
                delivered++;
            }
        }

        return delivered;
    }

    /// <summary>True when <paramref name="pos"/> shares a 4-edge with any core tile.</summary>
    public static bool IsEdgeAdjacentToCore(GridPosition pos, IReadOnlySet<GridPosition> coreTiles)
    {
        foreach (var dir in DirectionMath.All)
        {
            if (coreTiles.Contains(pos.Step(dir)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Prefer a facing that steps into the core when the cell touches it.</summary>
    public static Direction? PreferDirectionIntoCore(
        GridPosition pos,
        IReadOnlySet<GridPosition> coreTiles)
    {
        foreach (var dir in DirectionMath.All)
        {
            if (coreTiles.Contains(pos.Step(dir)))
            {
                return dir;
            }
        }

        return null;
    }
}
