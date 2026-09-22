namespace TIndustry.Logistics;

/// <summary>
/// Layered left-to-right layout for the research tech tree (nodes + prerequisite edges).
/// Production (top) and logistics (bottom) share columns but sit on separate vertical lanes.
/// </summary>
public static class TechTreeLayout
{
    public const int NodeWidth = 156;
    public const int NodeHeight = 58;
    public const int ColumnGap = 44;
    public const int RowGap = 14;
    public const int OriginX = 36;
    public const int OriginY = 128;
    public const int LaneGap = 20;

    public const float MinZoom = 0.55f;
    public const float MaxZoom = 1.85f;

    public sealed record Node(
        StructureDefinition Structure,
        int Column,
        int Row,
        int X,
        int Y);

    public sealed record Edge(
        string FromId,
        string ToId,
        int FromX,
        int FromY,
        int ToX,
        int ToY);

    public sealed record Graph(IReadOnlyList<Node> Nodes, IReadOnlyList<Edge> Edges);

    public static Graph Build(GameContent content)
    {
        var structures = content.Structures.ToList();
        var byId = structures.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        var depth = new Dictionary<string, int>(StringComparer.Ordinal);

        int DepthOf(string id)
        {
            if (depth.TryGetValue(id, out var cached))
            {
                return cached;
            }

            if (!byId.TryGetValue(id, out var structure))
            {
                depth[id] = 0;
                return 0;
            }

            var value = structure.Requires.Count == 0
                ? 0
                : structure.Requires
                    .Where(byId.ContainsKey)
                    .Select(DepthOf)
                    .DefaultIfEmpty(-1)
                    .Max() + 1;
            depth[id] = value;
            return value;
        }

        foreach (var structure in structures)
        {
            DepthOf(structure.Id);
        }

        var production = structures
            .Where(IsProductionLane)
            .OrderBy(structure => depth[structure.Id])
            .ThenBy(structure => structure.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var logistics = structures
            .Where(structure => !IsProductionLane(structure))
            .OrderBy(structure => depth[structure.Id])
            .ThenBy(structure => structure.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var nodes = new List<Node>();
        var productionMaxRow = PlaceLane(production, depth, rowOffset: 0, laneGap: 0, nodes);
        var logisticsOffset = production.Count == 0 ? 0 : productionMaxRow + 1;
        PlaceLane(logistics, depth, logisticsOffset, LaneGap, nodes);

        var nodeById = nodes.ToDictionary(node => node.Structure.Id, StringComparer.Ordinal);
        var edges = new List<Edge>();
        foreach (var node in nodes)
        {
            foreach (var prereqId in node.Structure.Requires)
            {
                if (!nodeById.TryGetValue(prereqId, out var from))
                {
                    continue;
                }

                edges.Add(new Edge(
                    from.Structure.Id,
                    node.Structure.Id,
                    from.X + NodeWidth,
                    from.Y + NodeHeight / 2,
                    node.X,
                    node.Y + NodeHeight / 2));
            }
        }

        return new Graph(nodes, edges);
    }

    /// <summary>
    /// Selected node + recursive prerequisites + downstream unlock dependents.
    /// Used to highlight the active research path in the tech-tree UI.
    /// </summary>
    public static HashSet<string> CollectRelatedIds(Graph graph, string selectedId)
    {
        var related = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(selectedId))
        {
            return related;
        }

        var byId = graph.Nodes.ToDictionary(node => node.Structure.Id, StringComparer.Ordinal);
        if (!byId.ContainsKey(selectedId))
        {
            return related;
        }

        void WalkAncestors(string id)
        {
            if (!related.Add(id) || !byId.TryGetValue(id, out var node))
            {
                return;
            }

            foreach (var prereq in node.Structure.Requires)
            {
                WalkAncestors(prereq);
            }
        }

        WalkAncestors(selectedId);

        var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var edge in graph.Edges)
        {
            if (!children.TryGetValue(edge.FromId, out var list))
            {
                list = [];
                children[edge.FromId] = list;
            }

            list.Add(edge.ToId);
        }

        var queue = new Queue<string>();
        queue.Enqueue(selectedId);
        var seenDescendants = new HashSet<string>(StringComparer.Ordinal) { selectedId };
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!children.TryGetValue(current, out var next))
            {
                continue;
            }

            foreach (var child in next)
            {
                if (seenDescendants.Add(child))
                {
                    related.Add(child);
                    queue.Enqueue(child);
                }
            }
        }

        return related;
    }

    public static bool IsEdgeOnPath(Edge edge, IReadOnlySet<string> related) =>
        related.Contains(edge.FromId) && related.Contains(edge.ToId);

    public static float ClampZoom(float zoom) =>
        Math.Clamp(zoom, MinZoom, MaxZoom);

    /// <returns>Highest row index used in this lane (or -1 if empty).</returns>
    private static int PlaceLane(
        IReadOnlyList<StructureDefinition> lane,
        IReadOnlyDictionary<string, int> depth,
        int rowOffset,
        int laneGap,
        List<Node> nodes)
    {
        var maxRow = -1;
        var columns = lane
            .GroupBy(structure => depth[structure.Id])
            .OrderBy(group => group.Key);

        foreach (var group in columns)
        {
            var column = group.Key;
            var bucket = group
                .OrderBy(structure => structure.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            for (var index = 0; index < bucket.Count; index++)
            {
                var structure = bucket[index];
                var row = rowOffset + index;
                maxRow = Math.Max(maxRow, row);
                var x = OriginX + column * (NodeWidth + ColumnGap);
                var y = OriginY + row * (NodeHeight + RowGap) + laneGap;
                nodes.Add(new Node(structure, column, row, x, y));
            }
        }

        return maxRow;
    }

    private static bool IsProductionLane(StructureDefinition structure) =>
        structure.Kind is StructureKind.Building or StructureKind.Stub
        || structure.Id is "miner" or "smelter" or "assembler" or "generator" or "miner-advanced" or "extractor"
        || structure.Id == "conveyor-fast"; // depends on forno — keep near production
}
