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
        // #region agent log
        var candidates = new List<object>();
        var pointingAtCore = 0;
        var readyAtCoreEdge = 0;
        var itemsNearCore = new List<object>();
        // #endregion
        var delivered = 0;
        foreach (var cell in grid.Cells.Values)
        {
            // #region agent log
            var outInCore = coreTiles.Contains(cell.OutputPosition);
            var cellInCore = coreTiles.Contains(cell.Position);
            var dist = coreTiles.Count == 0
                ? 99
                : coreTiles.Min(t => Math.Abs(t.X - cell.Position.X) + Math.Abs(t.Y - cell.Position.Y));
            if (dist <= 2 && cell.Items.Count > 0)
            {
                itemsNearCore.Add(new
                {
                    x = cell.Position.X,
                    y = cell.Position.Y,
                    dir = cell.Direction.ToString(),
                    outX = cell.OutputPosition.X,
                    outY = cell.OutputPosition.Y,
                    outInCore,
                    cellInCore,
                    dist,
                    items = cell.Items.Select(i => new { i.ItemId, i.Progress, travel = i.Travel?.ToString() }).ToList()
                });
            }
            // #endregion
            if (!outInCore)
            {
                continue;
            }

            // #region agent log
            pointingAtCore++;
            // #endregion
            while (cell.PeekOutput() is { } item)
            {
                // #region agent log
                readyAtCoreEdge++;
                candidates.Add(new
                {
                    x = cell.Position.X,
                    y = cell.Position.Y,
                    dir = cell.Direction.ToString(),
                    outX = cell.OutputPosition.X,
                    outY = cell.OutputPosition.Y,
                    item.ItemId,
                    item.Progress
                });
                // #endregion
                cell.RemoveOutput();
                AcceptAtCore(wallet, item.ItemId, market, session, autoSellAtCore);
                delivered++;
            }

            // #region agent log
            if (cell.Items.Count > 0)
            {
                candidates.Add(new
                {
                    x = cell.Position.X,
                    y = cell.Position.Y,
                    dir = cell.Direction.ToString(),
                    outX = cell.OutputPosition.X,
                    outY = cell.OutputPosition.Y,
                    status = "pointing_but_not_ready",
                    items = cell.Items.Select(i => new { i.ItemId, i.Progress }).ToList()
                });
            }
            // #endregion
        }

        // #region agent log
        if (delivered > 0 || pointingAtCore > 0 || itemsNearCore.Count > 0
            || CoreDeliveryDebugLog.ShouldLogSummary())
        {
            CoreDeliveryDebugLog.Write(
                "A",
                "CoreStockSink.cs:Drain(BeltGrid)",
                "drain_pass",
                new
                {
                    pointingAtCore,
                    readyAtCoreEdge,
                    delivered,
                    autoSellAtCore,
                    ironOre = wallet.MaterialCount("iron-ore"),
                    copperOre = wallet.MaterialCount("copper-ore"),
                    money = wallet.Money,
                    coreTiles = coreTiles.Select(t => new { t.X, t.Y }).ToList(),
                    candidates,
                    itemsNearCore,
                    beltCellCount = grid.Count,
                    beltItemCount = grid.Cells.Values.Sum(c => c.Items.Count)
                });
        }
        // #endregion

        return delivered;
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
            // #region agent log
            CoreDeliveryDebugLog.Write(
                "D,E",
                "CoreStockSink.cs:AcceptAtCore",
                "stocked",
                new { itemId, count = wallet.MaterialCount(itemId), autoSellAtCore });
            // #endregion
            return;
        }

        // Price from stock *before* this unit lands (same curve as manual Mercato sell).
        var stockBefore = wallet.MaterialCount(itemId);
        var unitPrice = market.GetDynamicSellPrice(itemId, stockBefore);
        wallet.AddMoney(unitPrice);
        session?.RecordSale(itemId, unitPrice);
        // #region agent log
        CoreDeliveryDebugLog.Write(
            "E",
            "CoreStockSink.cs:AcceptAtCore",
            "auto_sold",
            new { itemId, stockBefore, unitPrice, money = wallet.Money });
        // #endregion
    }
}
