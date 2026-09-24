namespace TIndustry.Shared;

/// <summary>
/// Core stock sink: items ready to leave a belt cell whose output tile is in the core
/// set are removed and added to the wallet (stock-first, no auto-sell).
/// </summary>
public static class CoreStockSink
{
    public static HashSet<GridPosition> MakeCoreTiles(GridPosition origin, int size = 2)
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
    /// Drain ready outputs from any belt cell that points into <paramref name="coreTiles"/>.
    /// Returns how many items were stocked.
    /// </summary>
    public static int Drain(BeltLane belt, IReadOnlySet<GridPosition> coreTiles, EconomyWallet wallet)
    {
        var delivered = 0;
        for (var i = 0; i < belt.Cells.Count; i++)
        {
            var cell = belt.Cells[i];
            var output = cell.Position.Step(belt.DirectionAt(i));
            if (!coreTiles.Contains(output))
            {
                continue;
            }

            while (cell.PeekOutput() is { } item)
            {
                cell.RemoveOutput();
                wallet.AddMaterial(item.ItemId, 1);
                delivered++;
            }
        }

        return delivered;
    }

    public static int Drain(BeltGrid grid, IReadOnlySet<GridPosition> coreTiles, EconomyWallet wallet)
    {
        var delivered = 0;
        foreach (var cell in grid.Cells.Values)
        {
            if (!coreTiles.Contains(cell.OutputPosition))
            {
                continue;
            }

            while (cell.PeekOutput() is { } item)
            {
                cell.RemoveOutput();
                wallet.AddMaterial(item.ItemId, 1);
                delivered++;
            }
        }

        return delivered;
    }
}
