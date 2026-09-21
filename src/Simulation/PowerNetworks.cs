namespace TIndustry.Logistics;

/// <summary>
/// Placeable power relay buildings. Links are straight geometric lines (any angle)
/// between node↔node and node↔powered endpoints within range, limited by maxLinks.
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

    public PowerNetworkState(
        HashSet<PowerEndpointId> liveEndpoints,
        HashSet<PowerLink> liveLinks,
        float capacity,
        float generationPerSecond)
    {
        this.liveEndpoints = liveEndpoints;
        this.liveLinks = liveLinks;
        Capacity = capacity;
        GenerationPerSecond = generationPerSecond;
    }

    public static PowerNetworkState Empty { get; } = new(
        [],
        [],
        FactoryWorld.CorePowerCapacity,
        FactoryWorld.CorePowerGeneration);

    public float Capacity { get; }
    public float GenerationPerSecond { get; }
    public IReadOnlyCollection<PowerLink> LiveLinks => liveLinks;

    public bool IsEndpointLive(PowerEndpointId id) => liveEndpoints.Contains(id);

    public bool IsLinkLive(PowerLink link) => liveLinks.Contains(link);
}

public static class PowerNetworking
{
    /// <summary>
    /// Auto-link rule (Mindustry-like): when a node is placed, connect to the nearest
    /// eligible endpoints within its range until maxLinks is filled. When a generator
    /// or consumer is placed, nearby nodes with spare capacity auto-link to it.
    /// Links are straight lines (any angle). A network is live when there is a path
    /// through links to the core (always-on baseline source) or a fueled generator.
    /// The CORE itself never brown-outs and never needs a power connection — only craft
    /// buildings (forno, assemblatore, …) gate on a live network. Stock intake at core
    /// is always available.
    /// </summary>
    public const string AutoLinkRule =
        "Auto-link Mindustry-like: al piazzamento di un nodo collega fino a maxLinks gli endpoint più vicini entro range "
        + "(nodi, generatori, core, forni, assemblatori). Al piazzamento di gen/forno/assemblatore i nodi in range "
        + "con slot liberi si collegano. Linee rette geometriche (anche diagonali). Rete live se path a core (sempre attivo) "
        + "o gen con fuel. Il CORE non richiede potenza/rete; solo edifici craft (forno/assemblatore) brown-out senza link live.";

    public static PowerNetworkState Build(
        FactoryWorld world,
        IReadOnlyDictionary<GridPosition, PowerNodeBuilding> nodes,
        IReadOnlyCollection<PowerLink> links,
        IEnumerable<GeneratorBuilding> generators)
    {
        var generating = generators.Where(g => g.IsGenerating).ToList();
        var adjacency = BuildAdjacency(links);
        var live = new HashSet<PowerEndpointId>();
        var queue = new Queue<PowerEndpointId>();

        void Seed(PowerEndpointId id)
        {
            if (live.Add(id))
            {
                queue.Enqueue(id);
            }
        }

        Seed(new PowerEndpointId(PowerEndpointKind.Core, world.CoreOrigin));
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
        foreach (var link in links)
        {
            if (live.Contains(link.A) && live.Contains(link.B))
            {
                liveLinks.Add(link);
            }
        }

        // Generators themselves are always "sources" while burning (direct adjacency still works).
        foreach (var gen in generating)
        {
            live.Add(new PowerEndpointId(PowerEndpointKind.Generator, gen.Position));
        }

        var capacity = FactoryWorld.CorePowerCapacity
            + generating.Count * GeneratorBuilding.CapacityBonus;
        var generation = FactoryWorld.CorePowerGeneration
            + generating.Count * GeneratorBuilding.GenerationPerSecond;
        return new PowerNetworkState(live, liveLinks, capacity, generation);
    }

    public static bool IsBuildingPowered(
        FactoryWorld world,
        PowerNetworkState networks,
        GridPosition origin,
        int size)
    {
        // Touching the core always counts (early-game without nodes).
        if (FootprintTouches(origin, size, world.CoreTiles))
        {
            return true;
        }

        // Direct adjacency to a fueled/generating generator.
        foreach (var gen in world.Generators.Values)
        {
            if (!gen.IsGenerating)
            {
                continue;
            }

            if (FootprintsAdjacent(origin, size, gen.Position, GeneratorBuilding.Size))
            {
                return true;
            }
        }

        var consumerId = new PowerEndpointId(PowerEndpointKind.Consumer, origin);
        return networks.IsEndpointLive(consumerId);
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
    /// endpoints within range until MaxLinks is reached.
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

        var candidates = new List<(PowerEndpointId Id, float Distance, int? OtherNodeMax)>();

        // Core
        var coreDist = DistanceCenters(node.Position, node.Size, world.CoreOrigin, FactoryWorld.CoreSize);
        if (coreDist <= node.LinkRange + 0.001f)
        {
            candidates.Add((new PowerEndpointId(PowerEndpointKind.Core, world.CoreOrigin), coreDist, null));
        }

        foreach (var gen in world.Generators.Values)
        {
            if (!InRange(node, gen.Position, GeneratorBuilding.Size))
            {
                continue;
            }

            var dist = DistanceCenters(node.Position, node.Size, gen.Position, GeneratorBuilding.Size);
            candidates.Add((new PowerEndpointId(PowerEndpointKind.Generator, gen.Position), dist, null));
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
            candidates.Add((otherId, dist, other.MaxLinks));
        }

        foreach (var smelter in world.Smelters.Values)
        {
            if (!InRange(node, smelter.Position, SmelterBuilding.Size))
            {
                continue;
            }

            var dist = DistanceCenters(node.Position, node.Size, smelter.Position, SmelterBuilding.Size);
            candidates.Add((new PowerEndpointId(PowerEndpointKind.Consumer, smelter.Position), dist, null));
        }

        foreach (var assembler in world.Assemblers.Values)
        {
            if (!InRange(node, assembler.Position, SmelterBuilding.Size))
            {
                continue;
            }

            var dist = DistanceCenters(node.Position, node.Size, assembler.Position, SmelterBuilding.Size);
            candidates.Add((new PowerEndpointId(PowerEndpointKind.Consumer, assembler.Position), dist, null));
        }

        foreach (var (id, _, otherMax) in candidates.OrderBy(c => c.Distance))
        {
            if (remaining <= 0)
            {
                break;
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
    /// </summary>
    public static void AutoLinkEndpoint(
        FactoryWorld world,
        PowerEndpointId endpoint,
        GridPosition origin,
        int size,
        List<PowerLink> links)
    {
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

    private static Dictionary<PowerEndpointId, List<PowerEndpointId>> BuildAdjacency(
        IReadOnlyCollection<PowerLink> links)
    {
        var adjacency = new Dictionary<PowerEndpointId, List<PowerEndpointId>>();
        foreach (var link in links)
        {
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

    private static bool FootprintTouches(GridPosition origin, int size, IReadOnlySet<GridPosition> tiles)
    {
        foreach (var tile in Footprint(origin, size))
        {
            for (var d = 0; d < DirectionMath.All.Length; d++)
            {
                if (tiles.Contains(tile.Step(DirectionMath.All[d])))
                {
                    return true;
                }
            }
        }

        return false;
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
