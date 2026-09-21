namespace TIndustry.Logistics;

/// <summary>
/// Placeable power relay buildings. Links are straight geometric lines (any angle)
/// between node↔node, node↔generator, and node↔consumer within range, limited by maxLinks.
/// CORE is never a power endpoint.
/// </summary>
public sealed class PowerNodeBuilding
{
    public const string Tier1Id = "power-node";
    public const string Tier2Id = "power-node-t2";

    public const int Tier1Size = 1;
    public const int Tier1MaxLinks = 4;
    public const float Tier1Range = 6f;

    public const int Tier2Size = 2;
    public const int Tier2MaxLinks = 8;
    public const float Tier2Range = 10f;

    public PowerNodeBuilding(GridPosition position, string definitionId = Tier1Id)
    {
        Position = position;
        DefinitionId = definitionId == Tier2Id ? Tier2Id : Tier1Id;
        Size = DefinitionId == Tier2Id ? Tier2Size : Tier1Size;
        MaxLinks = DefinitionId == Tier2Id ? Tier2MaxLinks : Tier1MaxLinks;
        LinkRange = DefinitionId == Tier2Id ? Tier2Range : Tier1Range;
    }

    public GridPosition Position { get; }
    public string DefinitionId { get; }
    public int Size { get; }
    public int MaxLinks { get; }
    public float LinkRange { get; }

    public IEnumerable<GridPosition> OccupiedTiles()
    {
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                yield return new GridPosition(Position.X + x, Position.Y + y);
            }
        }
    }

    public static bool IsKnownId(string id) =>
        id is Tier1Id or Tier2Id;

    public static int SizeFor(string definitionId) =>
        definitionId == Tier2Id ? Tier2Size : Tier1Size;

    public static int MaxLinksFor(string definitionId) =>
        definitionId == Tier2Id ? Tier2MaxLinks : Tier1MaxLinks;

    public static float RangeFor(string definitionId) =>
        definitionId == Tier2Id ? Tier2Range : Tier1Range;
}

public enum PowerEndpointKind
{
    /// <summary>Legacy save value — ignored on load; CORE is not on the power graph.</summary>
    Core,
    Generator,
    Node,
    Consumer
}

/// <summary>Stable identity for a power graph endpoint (origin tile + kind).</summary>
public readonly record struct PowerEndpointId(PowerEndpointKind Kind, GridPosition Origin)
    : IComparable<PowerEndpointId>
{
    public int CompareTo(PowerEndpointId other)
    {
        var kind = Kind.CompareTo(other.Kind);
        if (kind != 0)
        {
            return kind;
        }

        var x = Origin.X.CompareTo(other.Origin.X);
        return x != 0 ? x : Origin.Y.CompareTo(other.Origin.Y);
    }

    public override string ToString() => $"{Kind}:{Origin.X},{Origin.Y}";
}

/// <summary>Undirected geometric power link (normalized so A ≤ B).</summary>
public readonly record struct PowerLink(PowerEndpointId A, PowerEndpointId B)
{
    public static PowerLink Create(PowerEndpointId left, PowerEndpointId right)
    {
        return left.CompareTo(right) <= 0
            ? new PowerLink(left, right)
            : new PowerLink(right, left);
    }

    public bool Involves(PowerEndpointId id) => A.Equals(id) || B.Equals(id);

    public PowerEndpointId Other(PowerEndpointId id) =>
        A.Equals(id) ? B : A;
}

/// <summary>Resolved power connectivity for one simulation tick.</summary>
public sealed class PowerNetworkState
{
    private readonly HashSet<PowerEndpointId> liveEndpoints;
    private readonly HashSet<PowerLink> liveLinks;
    private readonly HashSet<GridPosition> adjacencyPoweredOrigins;

    public PowerNetworkState(
        HashSet<PowerEndpointId> liveEndpoints,
        HashSet<PowerLink> liveLinks,
        HashSet<GridPosition> adjacencyPoweredOrigins,
        float capacity,
        float generationPerSecond)
    {
        this.liveEndpoints = liveEndpoints;
        this.liveLinks = liveLinks;
        this.adjacencyPoweredOrigins = adjacencyPoweredOrigins;
        Capacity = capacity;
        GenerationPerSecond = generationPerSecond;
    }

    public static PowerNetworkState Empty { get; } = new(
        [],
        [],
        [],
        FactoryWorld.CorePowerCapacity,
        FactoryWorld.CorePowerGeneration);

    public float Capacity { get; }
    public float GenerationPerSecond { get; }
    public IReadOnlyCollection<PowerLink> LiveLinks => liveLinks;

    public bool IsEndpointLive(PowerEndpointId id) => liveEndpoints.Contains(id);

    public bool IsLinkLive(PowerLink link) => liveLinks.Contains(link);

    /// <summary>True when a building origin is powered via 4-connected footprint adjacency.</summary>
    public bool IsAdjacencyPowered(GridPosition origin) => adjacencyPoweredOrigins.Contains(origin);
}

public static class PowerNetworking
{
    /// <summary>
    /// Adjacency uses 4-connected footprint touch (N/E/S/W), matching building I/O transfer.
    /// Structures next to a running generator are powered; adjacent structures share power
    /// through the cluster (packed factory next to a gen needs no nodes).
    /// </summary>
    public const string AdjacencyRule =
        "Adiacenza 4-connessa (N/E/S/O, come I/O edifici): edifici a contatto di un gen in funzione "
        + "sono alimentati; edifici a contatto tra loro condividono la potenza (cluster senza nodi).";

    /// <summary>
    /// Auto-link rule (Mindustry-like): when a node is placed, connect to the nearest
    /// eligible endpoints within its range until maxLinks is filled, prioritizing generators.
    /// When a generator or consumer is placed, nearby nodes with spare capacity auto-link to it.
    /// Links are straight lines (any angle) between generators, nodes, and consumers — never CORE.
    /// A network/node is live only when there is a path through links to a fueled/generating generator.
    /// CORE never needs or provides power; stock intake is always available.
    /// </summary>
    public const string AutoLinkRule =
        "Auto-link Mindustry-like: al piazzamento di un nodo collega fino a maxLinks gli endpoint più vicini entro range "
        + "(priorità: generatori, poi nodi, poi forni/assemblatori — mai CORE). Al piazzamento di gen/forno/assemblatore "
        + "i nodi in range con slot liberi si collegano. Linee rette geometriche (anche diagonali). "
        + "Nodo/rete live solo se path a gen con fuel. CORE non fornisce né richiede potenza; "
        + AdjacencyRule
        + " Solo forno/assemblatore brown-out senza gen adiacente/cluster o link live.";

    public static PowerNetworkState Build(
        FactoryWorld world,
        IReadOnlyDictionary<GridPosition, PowerNodeBuilding> nodes,
        IReadOnlyCollection<PowerLink> links,
        IEnumerable<GeneratorBuilding> generators)
    {
        var generating = generators.Where(g => g.IsGenerating).ToList();
        // Drop legacy CORE links from the live graph.
        var graphLinks = links
            .Where(l => l.A.Kind != PowerEndpointKind.Core && l.B.Kind != PowerEndpointKind.Core)
            .ToList();
        var adjacency = BuildAdjacency(graphLinks);
        var live = new HashSet<PowerEndpointId>();
        var queue = new Queue<PowerEndpointId>();

        void Seed(PowerEndpointId id)
        {
            if (id.Kind == PowerEndpointKind.Core)
            {
                return;
            }

            if (live.Add(id))
            {
                queue.Enqueue(id);
            }
        }

        foreach (var gen in generating)
        {
            Seed(new PowerEndpointId(PowerEndpointKind.Generator, gen.Position));
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var neighbors))
            {
                continue;
            }

            foreach (var next in neighbors)
            {
                Seed(next);
            }
        }

        var liveLinks = new HashSet<PowerLink>();
        foreach (var link in graphLinks)
        {
            if (live.Contains(link.A) && live.Contains(link.B))
            {
                liveLinks.Add(link);
            }
        }

        // Generators themselves are always "sources" while burning.
        foreach (var gen in generating)
        {
            live.Add(new PowerEndpointId(PowerEndpointKind.Generator, gen.Position));
        }

        var adjacencyPowered = BuildAdjacencyPoweredOrigins(world, generating);

        // Consumers powered only by adjacency still count as live endpoints for queries.
        foreach (var origin in adjacencyPowered)
        {
            if (world.Smelters.ContainsKey(origin) || world.Assemblers.ContainsKey(origin))
            {
                live.Add(new PowerEndpointId(PowerEndpointKind.Consumer, origin));
            }
        }

        var capacity = FactoryWorld.CorePowerCapacity
            + generating.Count * GeneratorBuilding.CapacityBonus;
        var generation = FactoryWorld.CorePowerGeneration
            + generating.Count * GeneratorBuilding.GenerationPerSecond;
        return new PowerNetworkState(live, liveLinks, adjacencyPowered, capacity, generation);
    }

    public static bool IsBuildingPowered(
        FactoryWorld world,
        PowerNetworkState networks,
        GridPosition origin,
        int size)
    {
        // 4-connected adjacency cluster from a running generator (no node required).
        if (networks.IsAdjacencyPowered(origin))
        {
            return true;
        }

        // Direct check when state is empty/stale (e.g. before first Refresh).
        if (IsInPoweredAdjacencyCluster(world, origin, size))
        {
            return true;
        }

        var consumerId = new PowerEndpointId(PowerEndpointKind.Consumer, origin);
        return networks.IsEndpointLive(consumerId);
    }

    /// <summary>
    /// True when the node has a geometric-link path to any placed generator (fueled or not).
    /// Used for placement validity / status — live beams still require a fueled gen.
    /// </summary>
    public static bool NodeReachesGenerator(
        PowerNodeBuilding node,
        FactoryWorld world,
        IReadOnlyCollection<PowerLink> links)
    {
        var nodeId = new PowerEndpointId(PowerEndpointKind.Node, node.Position);
        var graphLinks = links
            .Where(l => l.A.Kind != PowerEndpointKind.Core && l.B.Kind != PowerEndpointKind.Core)
            .ToList();
        var adjacency = BuildAdjacency(graphLinks);
        var seen = new HashSet<PowerEndpointId>();
        var queue = new Queue<PowerEndpointId>();
        seen.Add(nodeId);
        queue.Enqueue(nodeId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Kind == PowerEndpointKind.Generator
                && world.Generators.ContainsKey(current.Origin))
            {
                return true;
            }

            if (!adjacency.TryGetValue(current, out var neighbors))
            {
                continue;
            }

            foreach (var next in neighbors)
            {
                if (seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return false;
    }

    public static float DistanceCenters(
        GridPosition originA,
        int sizeA,
        GridPosition originB,
        int sizeB)
    {
        var ax = originA.X + sizeA * 0.5f;
        var ay = originA.Y + sizeA * 0.5f;
        var bx = originB.X + sizeB * 0.5f;
        var by = originB.Y + sizeB * 0.5f;
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    public static bool InRange(
        PowerNodeBuilding node,
        GridPosition otherOrigin,
        int otherSize) =>
        DistanceCenters(node.Position, node.Size, otherOrigin, otherSize) <= node.LinkRange + 0.001f;

    public static int CountLinksFor(PowerEndpointId id, IReadOnlyCollection<PowerLink> links)
    {
        var count = 0;
        foreach (var link in links)
        {
            if (link.Involves(id))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Creates auto-links for a newly placed (or restored) node: nearest eligible
    /// endpoints within range until MaxLinks is reached. Generators are preferred.
    /// Never links to CORE.
    /// </summary>
    public static void AutoLinkNode(
        FactoryWorld world,
        PowerNodeBuilding node,
        List<PowerLink> links)
    {
        var nodeId = new PowerEndpointId(PowerEndpointKind.Node, node.Position);
        var remaining = node.MaxLinks - CountLinksFor(nodeId, links);
        if (remaining <= 0)
        {
            return;
        }

        var candidates = new List<(PowerEndpointId Id, float Distance, int Priority, int? OtherNodeMax)>();

        foreach (var gen in world.Generators.Values)
        {
            if (!InRange(node, gen.Position, GeneratorBuilding.Size))
            {
                continue;
            }

            var dist = DistanceCenters(node.Position, node.Size, gen.Position, GeneratorBuilding.Size);
            candidates.Add((
                new PowerEndpointId(PowerEndpointKind.Generator, gen.Position),
                dist,
                0,
                null));
        }

        foreach (var other in world.PowerNodes.Values)
        {
            if (other.Position.Equals(node.Position))
            {
                continue;
            }

            // Mutual range for node↔node.
            if (!InRange(node, other.Position, other.Size) || !InRange(other, node.Position, node.Size))
            {
                continue;
            }

            var dist = DistanceCenters(node.Position, node.Size, other.Position, other.Size);
            var otherId = new PowerEndpointId(PowerEndpointKind.Node, other.Position);
            candidates.Add((otherId, dist, 1, other.MaxLinks));
        }

        foreach (var smelter in world.Smelters.Values)
        {
            if (!InRange(node, smelter.Position, SmelterBuilding.Size))
            {
                continue;
            }

            var dist = DistanceCenters(node.Position, node.Size, smelter.Position, SmelterBuilding.Size);
            candidates.Add((
                new PowerEndpointId(PowerEndpointKind.Consumer, smelter.Position),
                dist,
                2,
                null));
        }

        foreach (var assembler in world.Assemblers.Values)
        {
            if (!InRange(node, assembler.Position, SmelterBuilding.Size))
            {
                continue;
            }

            var dist = DistanceCenters(node.Position, node.Size, assembler.Position, SmelterBuilding.Size);
            candidates.Add((
                new PowerEndpointId(PowerEndpointKind.Consumer, assembler.Position),
                dist,
                2,
                null));
        }

        foreach (var (id, _, _, otherMax) in candidates
                     .OrderBy(c => c.Priority)
                     .ThenBy(c => c.Distance))
        {
            if (remaining <= 0)
            {
                break;
            }

            if (id.Kind == PowerEndpointKind.Core)
            {
                continue;
            }

            var link = PowerLink.Create(nodeId, id);
            if (links.Contains(link))
            {
                continue;
            }

            if (id.Kind == PowerEndpointKind.Node && otherMax is int max)
            {
                if (CountLinksFor(id, links) >= max)
                {
                    continue;
                }
            }

            links.Add(link);
            remaining--;
        }
    }

    /// <summary>
    /// After placing a generator or consumer, let nearby nodes with spare slots link in.
    /// CORE is never an auto-link target.
    /// </summary>
    public static void AutoLinkEndpoint(
        FactoryWorld world,
        PowerEndpointId endpoint,
        GridPosition origin,
        int size,
        List<PowerLink> links)
    {
        if (endpoint.Kind == PowerEndpointKind.Core)
        {
            return;
        }

        var candidates = new List<(PowerNodeBuilding Node, float Distance)>();
        foreach (var node in world.PowerNodes.Values)
        {
            if (!InRange(node, origin, size))
            {
                continue;
            }

            var dist = DistanceCenters(node.Position, node.Size, origin, size);
            candidates.Add((node, dist));
        }

        foreach (var (node, _) in candidates.OrderBy(c => c.Distance))
        {
            var nodeId = new PowerEndpointId(PowerEndpointKind.Node, node.Position);
            if (CountLinksFor(nodeId, links) >= node.MaxLinks)
            {
                continue;
            }

            var link = PowerLink.Create(nodeId, endpoint);
            if (links.Contains(link))
            {
                continue;
            }

            links.Add(link);
        }
    }

    public static void RemoveEndpointLinks(PowerEndpointId id, List<PowerLink> links) =>
        links.RemoveAll(link => link.Involves(id));

    /// <summary>
    /// Flood-fill origins of craft buildings powered by 4-connected adjacency from
    /// running generators through touching structure footprints (gen / forno / assy).
    /// </summary>
    private static HashSet<GridPosition> BuildAdjacencyPoweredOrigins(
        FactoryWorld world,
        IReadOnlyList<GeneratorBuilding> generating)
    {
        var powered = new HashSet<GridPosition>();
        if (generating.Count == 0)
        {
            return powered;
        }

        // Tile → structure origin for buildings that participate in the adjacency cluster.
        var tileToOrigin = new Dictionary<GridPosition, GridPosition>();
        var originSize = new Dictionary<GridPosition, int>();

        void Register(GridPosition origin, int size)
        {
            originSize[origin] = size;
            foreach (var tile in Footprint(origin, size))
            {
                tileToOrigin[tile] = origin;
            }
        }

        foreach (var gen in world.Generators.Values)
        {
            Register(gen.Position, GeneratorBuilding.Size);
        }

        foreach (var smelter in world.Smelters.Values)
        {
            Register(smelter.Position, SmelterBuilding.Size);
        }

        foreach (var assembler in world.Assemblers.Values)
        {
            Register(assembler.Position, SmelterBuilding.Size);
        }

        var queue = new Queue<GridPosition>();
        foreach (var gen in generating)
        {
            if (powered.Add(gen.Position))
            {
                queue.Enqueue(gen.Position);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!originSize.TryGetValue(current, out var size))
            {
                continue;
            }

            foreach (var tile in Footprint(current, size))
            {
                for (var d = 0; d < DirectionMath.All.Length; d++)
                {
                    var n = tile.Step(DirectionMath.All[d]);
                    if (!tileToOrigin.TryGetValue(n, out var neighborOrigin))
                    {
                        continue;
                    }

                    if (powered.Add(neighborOrigin))
                    {
                        queue.Enqueue(neighborOrigin);
                    }
                }
            }
        }

        // Only craft consumers need the flag for IsBuildingPowered queries;
        // keep gens in the set so IsAdjacencyPowered(gen) is true while burning.
        return powered;
    }

    private static bool IsInPoweredAdjacencyCluster(
        FactoryWorld world,
        GridPosition origin,
        int size)
    {
        var generating = world.Generators.Values.Where(g => g.IsGenerating).ToList();
        if (generating.Count == 0)
        {
            return false;
        }

        var powered = BuildAdjacencyPoweredOrigins(world, generating);
        return powered.Contains(origin)
            || FootprintsAdjacentToAnyPowered(origin, size, powered, world);
    }

    private static bool FootprintsAdjacentToAnyPowered(
        GridPosition origin,
        int size,
        HashSet<GridPosition> poweredOrigins,
        FactoryWorld world)
    {
        // Query building may not yet be registered (preview); check footprint touch vs powered footprints.
        foreach (var poweredOrigin in poweredOrigins)
        {
            var poweredSize = world.Generators.ContainsKey(poweredOrigin) ? GeneratorBuilding.Size
                : world.Smelters.ContainsKey(poweredOrigin) || world.Assemblers.ContainsKey(poweredOrigin)
                    ? SmelterBuilding.Size
                    : 0;
            if (poweredSize > 0 && FootprintsAdjacent(origin, size, poweredOrigin, poweredSize))
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<PowerEndpointId, List<PowerEndpointId>> BuildAdjacency(
        IReadOnlyCollection<PowerLink> links)
    {
        var adjacency = new Dictionary<PowerEndpointId, List<PowerEndpointId>>();
        foreach (var link in links)
        {
            if (link.A.Kind == PowerEndpointKind.Core || link.B.Kind == PowerEndpointKind.Core)
            {
                continue;
            }

            if (!adjacency.TryGetValue(link.A, out var listA))
            {
                listA = [];
                adjacency[link.A] = listA;
            }

            if (!adjacency.TryGetValue(link.B, out var listB))
            {
                listB = [];
                adjacency[link.B] = listB;
            }

            listA.Add(link.B);
            listB.Add(link.A);
        }

        return adjacency;
    }

    private static bool FootprintsAdjacent(GridPosition a, int sizeA, GridPosition b, int sizeB)
    {
        foreach (var tileA in Footprint(a, sizeA))
        {
            for (var d = 0; d < DirectionMath.All.Length; d++)
            {
                var n = tileA.Step(DirectionMath.All[d]);
                if (n.X >= b.X && n.X < b.X + sizeB && n.Y >= b.Y && n.Y < b.Y + sizeB)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<GridPosition> Footprint(GridPosition origin, int size)
    {
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                yield return new GridPosition(origin.X + x, origin.Y + y);
            }
        }
    }
}
