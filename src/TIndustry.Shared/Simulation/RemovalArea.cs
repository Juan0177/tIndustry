namespace TIndustry.Shared;

/// <summary>
/// The removal rectangle of a right drag (Mindustry): every block, site and build plan of the player touching it,
/// one marking <see cref="Remove"/> each. Pure; the client's right drag and the agents' <c>smonta {da, a}</c> use it.
/// </summary>
public static class RemovalArea
{
    /// <summary>Longest side of the rectangle, in cells (the agents' <c>area</c> limit too).</summary>
    public const int MaxEdge = 64;

    /// <summary>The rectangle from <paramref name="anchor"/> to <paramref name="cursor"/>, at most <see cref="MaxEdge"/> cells a side from the anchor.</summary>
    public static (GridPosition Min, GridPosition Max) Rect(GridPosition anchor, GridPosition cursor)
    {
        var x = Math.Clamp(cursor.X, anchor.X - (MaxEdge - 1), anchor.X + (MaxEdge - 1));
        var y = Math.Clamp(cursor.Y, anchor.Y - (MaxEdge - 1), anchor.Y + (MaxEdge - 1));
        return (new GridPosition(Math.Min(anchor.X, x), Math.Min(anchor.Y, y)),
            new GridPosition(Math.Max(anchor.X, x), Math.Max(anchor.Y, y)));
    }

    /// <summary>
    /// Row by row (Y then X), a <c>Remove(OnlyMark: true)</c> on the first cell met of each not yet started build plan
    /// of <paramref name="playerId"/>, each site (not one already being taken apart) and each block standing in the
    /// rectangle. Empty cells send nothing; marks are never taken off.
    /// </summary>
    public static IReadOnlyList<Remove> Commands(IGameView view, int playerId, GridPosition anchor, GridPosition cursor)
    {
        ArgumentNullException.ThrowIfNull(view);
        var (min, max) = Rect(anchor, cursor);
        // Lookup only (never iterated): the plan covering each tile, the first in queue order.
        var planAt = new Dictionary<GridPosition, BuildPlan>();
        foreach (var plan in view.PlansOf(playerId))
        {
            if (!plan.Breaking && view.SiteOfPlan(plan) is null)
            {
                foreach (var tile in view.PlanTiles(plan))
                {
                    planAt.TryAdd(tile, plan);
                }
            }
        }

        var done = new HashSet<GridPosition>[3] { [], [], [] }; // plans, sites, blocks
        var commands = new List<Remove>();
        void Once(int what, GridPosition cell, IEnumerable<GridPosition> tiles)
        {
            if (done[what].Contains(cell))
            {
                return;
            }

            done[what].UnionWith(tiles);
            done[what].Add(cell);
            commands.Add(new Remove(playerId, cell, OnlyMark: true));
        }

        for (var y = min.Y; y <= max.Y; y++)
        {
            for (var x = min.X; x <= max.X; x++)
            {
                var cell = new GridPosition(x, y);
                if (planAt.TryGetValue(cell, out var plan))
                {
                    Once(0, cell, view.PlanTiles(plan));
                }

                if (view.TryGetSiteAt(cell, out var site))
                {
                    // A site stands in for its block (SimHost cancels the site first); one being taken apart is marked already.
                    if (!site.Deconstructing)
                    {
                        Once(1, cell, site.Tiles);
                    }

                    // A tier change also marks the old belt its cancelled site gives back, as a pending one does.
                    if (site.Replaced is not null)
                    {
                        Once(2, cell, site.Tiles);
                    }
                }
                else if (view.TryGetSpecAt(cell, out var spec, out _))
                {
                    Once(2, cell, view.Footprint(spec));
                }
            }
        }

        return commands;
    }
}
