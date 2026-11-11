namespace TIndustry.Shared;

/// <summary>
/// The run of belts a belt belongs to, from its first cell to its last: only belt cells (<see cref="LogisticsKind.Belt"/>
/// and <see cref="LogisticsKind.Stack"/>), never junctions, routers, sorters, gates or bridges. Forward it follows the
/// flow while the next cell is a belt; backward it follows the single belt that feeds a cell and stops where several do.
/// Each cell is listed once, so a loop terminates.
/// </summary>
public static class BeltPath
{
    public static IReadOnlyList<GridPosition> Walk(IReadOnlyDictionary<GridPosition, BeltGridCell> belts, GridPosition start)
    {
        if (!IsBelt(belts, start))
        {
            return Array.Empty<GridPosition>();
        }

        var visited = new HashSet<GridPosition> { start };

        // Backward: the cells before start, nearest first.
        var before = new List<GridPosition>();
        var current = start;
        while (true)
        {
            GridPosition? feeder = null;
            var count = 0;
            foreach (var dir in DirectionMath.All)
            {
                var neighbour = current.Step(dir);
                if (IsBelt(belts, neighbour) && belts[neighbour].OutputPosition == current)
                {
                    feeder = neighbour;
                    count++;
                }
            }

            if (count != 1 || !visited.Add(feeder!.Value))
            {
                break;
            }

            before.Add(feeder.Value);
            current = feeder.Value;
        }

        // Forward: the cells after start, following the flow.
        var path = new List<GridPosition>(before.Count + 1);
        for (var i = before.Count - 1; i >= 0; i--)
        {
            path.Add(before[i]);
        }

        path.Add(start);
        current = start;
        while (true)
        {
            var next = belts[current].OutputPosition;
            if (!IsBelt(belts, next) || !visited.Add(next))
            {
                break;
            }

            path.Add(next);
            current = next;
        }

        return path;
    }

    private static bool IsBelt(IReadOnlyDictionary<GridPosition, BeltGridCell> belts, GridPosition position) =>
        belts.TryGetValue(position, out var cell) && cell.Kind is LogisticsKind.Belt or LogisticsKind.Stack;
}
