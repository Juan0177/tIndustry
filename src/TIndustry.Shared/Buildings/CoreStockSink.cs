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
    /// A cell pointing into a storage block delivers through <see cref="GameState.TryAcceptAtStorage"/>: a linked
    /// block to the Core, a standalone one into itself up to its space (a refused piece waits the same way).
    /// Returns how many pieces reached the Core.
    /// </summary>
    public static int Drain(GameState state)
    {
        var grid = state.Belts;
        var coreTiles = state.CoreTiles;
        var delivered = 0;
        foreach (var cell in grid.Cells.Values)
        {
            if (cell.Kind == LogisticsKind.Junction)
            {
                delivered += DrainJunction(state, cell);
                continue;
            }

            if (cell.Kind is LogisticsKind.Router or LogisticsKind.Sorter)
            {
                continue; // a router and a sorter hand items to the Core themselves (GameState.ReceiveFromLogistics)
            }

            // Face into core, or dead-end against core (wrong facing / no next belt).
            var outInCore = coreTiles.Contains(cell.OutputPosition);
            var sinksHere = outInCore
                || (IsEdgeAdjacentToCore(cell.Position, coreTiles)
                    && !grid.Contains(cell.OutputPosition));
            if (!sinksHere)
            {
                if (state.Storages.Count > 0 && state.StorageAt(cell.OutputPosition) is { } storage)
                {
                    while (cell.PeekOutput() is { } piece && state.TryAcceptAtStorage(storage, piece.ItemId))
                    {
                        cell.RemoveOutput();
                        delivered += storage.IsLinked ? 1 : 0;
                    }
                }

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

    /// <summary>
    /// A junction hands each queue's crossed head to the Core or a storage block on the far side of that queue (one
    /// item per queue per tick, as to a belt).
    /// </summary>
    private static int DrainJunction(GameState state, BeltGridCell cell)
    {
        var delivered = 0;
        for (var index = 0; index < cell.OutputTargetCount; index++)
        {
            var target = cell.OutputTarget(index);
            if (cell.PeekOutputToward(target) is not { } item)
            {
                continue;
            }

            if (state.CoreTiles.Contains(target))
            {
                if (state.TryAcceptAtCore(item.ItemId))
                {
                    cell.RemoveOutputToward(target);
                    delivered++;
                }
            }
            else if (state.Storages.Count > 0
                && state.StorageAt(target) is { } storage
                && state.TryAcceptAtStorage(storage, item.ItemId))
            {
                cell.RemoveOutputToward(target);
                delivered += storage.IsLinked ? 1 : 0;
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
