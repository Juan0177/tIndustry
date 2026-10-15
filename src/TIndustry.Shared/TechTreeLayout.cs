namespace TIndustry.Shared;

/// <summary>
/// Mindustry-style research tree: a CORE root on top, each structure under its prerequisite,
/// laid out as a tidy top-down tree (leaves get their own column, parents sit centred over their
/// children). Structures with several prerequisites hang under the deepest one; the others are
/// kept as secondary edges.
/// </summary>
public static class TechTreeLayout
{
    /// <summary>Synthetic root (not a buildable structure).</summary>
    public const string RootId = "core";

    /// <summary>Square node size in layout units (icon tile; the name is drawn below it).</summary>
    public const int NodeSize = 64;
    public const int NodeWidth = NodeSize;
    public const int NodeHeight = NodeSize;

    /// <summary>Horizontal distance between sibling slots / vertical distance between levels.</summary>
    public const int SlotWidth = 128;
    public const int LevelHeight = 116;

    public const float MinZoom = 0.35f;
    public const float MaxZoom = 1.85f;

    /// <param name="X">Left edge of the node square.</param>
    /// <param name="Y">Top edge of the node square.</param>
    /// <param name="Column">Horizontal slot (may be fractional for parents, rounded).</param>
    /// <param name="Row">Depth below the root.</param>
    public sealed record Node(
        StructureDefinition Structure,
        int Column,
        int Row,
        int X,
        int Y)
    {
        public bool IsRoot => Structure.Id == RootId;
    }

    /// <param name="Primary">True for the tree edge; false for an extra prerequisite.</param>
    public sealed record Edge(
        string FromId,
        string ToId,
        int FromX,
        int FromY,
        int ToX,
        int ToY,
        bool Primary = true);

    public sealed record Graph(IReadOnlyList<Node> Nodes, IReadOnlyList<Edge> Edges);

    public static readonly StructureDefinition Root =
        new(RootId, "CORE", "core", UnlockedByDefault: true, Unlock: null, IsStub: false, Prerequisites: []);

    public static Graph Build(IEnumerable<StructureDefinition> structures)
    {
        var list = structures.Where(s => s.Id != RootId).ToList();
        var byId = list.ToDictionary(s => s.Id, StringComparer.Ordinal);
        var order = list.Select((s, i) => (s.Id, i)).ToDictionary(p => p.Id, p => p.i, StringComparer.Ordinal);
        var depth = new Dictionary<string, int>(StringComparer.Ordinal);

        int DepthOf(string id, HashSet<string> visiting)
        {
            if (depth.TryGetValue(id, out var cached))
            {
                return cached;
            }

            if (!byId.TryGetValue(id, out var s) || !visiting.Add(id))
            {
                return 0; // unknown id or a cycle in content: treat as a root
            }

            var parents = s.Prerequisites.Where(byId.ContainsKey).ToList();
            var value = parents.Count == 0 ? 1 : parents.Max(p => DepthOf(p, visiting)) + 1;
            visiting.Remove(id);
            depth[id] = value;
            return value;
        }

        foreach (var s in list)
        {
            DepthOf(s.Id, []);
        }

        // Primary parent = deepest prerequisite (ties: first listed) → a real tree.
        var parentOf = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var s in list)
        {
            var parents = s.Prerequisites.Where(byId.ContainsKey).ToList();
            parentOf[s.Id] = parents.Count == 0
                ? RootId
                : parents.OrderByDescending(p => depth[p]).ThenBy(p => s.Prerequisites.ToList().IndexOf(p)).First();
        }

        var children = new Dictionary<string, List<string>>(StringComparer.Ordinal) { [RootId] = [] };
        foreach (var s in list)
        {
            children.TryAdd(s.Id, []);
        }

        foreach (var s in list)
        {
            children[parentOf[s.Id]].Add(s.Id);
        }

        foreach (var kids in children.Values)
        {
            // Content order keeps the author's intent (e.g. T1 before T2).
            kids.Sort((a, b) => order[a].CompareTo(order[b]));
        }

        // Tidy tree: leaves take consecutive slots, parents centre over their children.
        var slot = new Dictionary<string, float>(StringComparer.Ordinal);
        var nextLeaf = 0f;

        float Place(string id)
        {
            var kids = children[id];
            if (kids.Count == 0)
            {
                slot[id] = nextLeaf;
                nextLeaf += 1f;
                return slot[id];
            }

            var xs = kids.Select(Place).ToList();
            slot[id] = (xs.First() + xs.Last()) / 2f;
            return slot[id];
        }

        Place(RootId);

        var nodes = new List<Node>();
        var nodeById = new Dictionary<string, Node>(StringComparer.Ordinal);

        void Emit(StructureDefinition s, int row)
        {
            var x = (int)MathF.Round(slot[s.Id] * SlotWidth);
            var node = new Node(s, (int)MathF.Round(slot[s.Id]), row, x, row * LevelHeight);
            nodes.Add(node);
            nodeById[s.Id] = node;
        }

        Emit(Root, 0);
        var queue = new Queue<(string Id, int Row)>();
        queue.Enqueue((RootId, 0));
        while (queue.Count > 0)
        {
            var (id, row) = queue.Dequeue();
            foreach (var child in children[id])
            {
                Emit(byId[child], row + 1);
                queue.Enqueue((child, row + 1));
            }
        }

        var edges = new List<Edge>();

        void Link(string fromId, string toId, bool primary)
        {
            var from = nodeById[fromId];
            var to = nodeById[toId];
            edges.Add(new Edge(
                fromId,
                toId,
                from.X + NodeSize / 2,
                from.Y + NodeSize,
                to.X + NodeSize / 2,
                to.Y,
                primary));
        }

        foreach (var s in list)
        {
            Link(parentOf[s.Id], s.Id, primary: true);
            foreach (var extra in s.Prerequisites.Where(byId.ContainsKey).Where(p => p != parentOf[s.Id]))
            {
                Link(extra, s.Id, primary: false);
            }
        }

        return new Graph(nodes, edges);
    }

    public static Graph Build(FactoryContent content) => Build(content.Structures);

    /// <summary>
    /// Selected node + recursive prerequisites + downstream unlock dependents.
    /// </summary>
    public static HashSet<string> CollectRelatedIds(Graph graph, string selectedId)
    {
        var related = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(selectedId))
        {
            return related;
        }

        var parents = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var edge in graph.Edges)
        {
            (parents.TryGetValue(edge.ToId, out var p) ? p : parents[edge.ToId] = []).Add(edge.FromId);
            (children.TryGetValue(edge.FromId, out var c) ? c : children[edge.FromId] = []).Add(edge.ToId);
        }

        if (graph.Nodes.All(n => n.Structure.Id != selectedId))
        {
            return related;
        }

        void Walk(string id, Dictionary<string, List<string>> next)
        {
            if (!next.TryGetValue(id, out var list))
            {
                return;
            }

            foreach (var other in list)
            {
                if (related.Add(other))
                {
                    Walk(other, next);
                }
            }
        }

        related.Add(selectedId);
        Walk(selectedId, parents);
        Walk(selectedId, children);
        return related;
    }

    public static bool IsEdgeOnPath(Edge edge, IReadOnlySet<string> related) =>
        related.Contains(edge.FromId) && related.Contains(edge.ToId);

    public static float ClampZoom(float zoom) =>
        Math.Clamp(zoom, MinZoom, MaxZoom);
}
