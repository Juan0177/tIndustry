namespace TIndustry.Shared;

/// <summary>
/// Core stock sink: items ready to leave a belt cell whose output tile is in the core
/// set are removed. Default path stocks the wallet; optional auto-sell liquidates to $.
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
    /// Returns how many items were stocked (stock-first; no auto-sell).
    /// </summary>
    public static int Drain(BeltLane belt, IReadOnlySet<GridPosition> coreTiles, EconomyWallet wallet) =>
        Drain(belt, coreTiles, wallet, market: null, session: null, autoSellAtCore: false);

    public static int Drain(BeltGrid grid, IReadOnlySet<GridPosition> coreTiles, EconomyWallet wallet) =>
        Drain(grid, coreTiles, wallet, market: null, session: null, autoSellAtCore: false);

    /// <summary>
    /// Drain into stock, or liquidate immediately when <paramref name="autoSellAtCore"/> is on
    /// (Raylib Mercato “Vendita automatica” parity).
    /// </summary>
    public static int Drain(
        BeltLane belt,
        IReadOnlySet<GridPosition> coreTiles,
        EconomyWallet wallet,
        MarketCatalog? market,
        EconomySession? session,
        bool autoSellAtCore)
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
                AcceptAtCore(wallet, item.ItemId, market, session, autoSellAtCore);
                delivered++;
            }
        }

        return delivered;
    }

    public static int Drain(
        BeltGrid grid,
        IReadOnlySet<GridPosition> coreTiles,
        EconomyWallet wallet,
        MarketCatalog? market,
        EconomySession? session,
        bool autoSellAtCore)
    {
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

            while (cell.PeekOutput() is { } item)
            {
                cell.RemoveOutput();
                AcceptAtCore(wallet, item.ItemId, market, session, autoSellAtCore);
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

    private static void AcceptAtCore(
        EconomyWallet wallet,
        string itemId,
        MarketCatalog? market,
        EconomySession? session,
        bool autoSellAtCore)
    {
        if (!autoSellAtCore || market is null)
        {
            wallet.AddMaterial(itemId, 1);
            return;
        }

        // Price from stock *before* this unit lands (same curve as manual Mercato sell).
        var stockBefore = wallet.MaterialCount(itemId);
        var unitPrice = market.GetDynamicSellPrice(itemId, stockBefore);
        wallet.AddMoney(unitPrice);
        session?.RecordSale(itemId, unitPrice);
    }
}
