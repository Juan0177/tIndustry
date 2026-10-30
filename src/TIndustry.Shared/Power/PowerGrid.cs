namespace TIndustry.Shared;

/// <summary>What a power block does in its network (spec «Rete di corrente»).</summary>
public enum PowerBlockKind
{
    Generator,
    Node,
    Battery,

    /// <summary>A block with a power use: forno, assembler, Miner T2, lamp.</summary>
    Consumer
}

/// <summary>A block of the power network: its entity id, square and role.</summary>
public readonly record struct PowerBlock(long Id, GridPosition Origin, int Size, PowerBlockKind Kind);

/// <summary>
/// One power network: the power blocks joined by contact (side against side) or by a laser, in id order.
/// <see cref="Index"/> is its place in <see cref="PowerGrid.Networks"/> (ordered by the lowest member id).
/// The balance of the last tick (<see cref="PowerSystem"/>) is in integers: rates in thousandths of a unit per second,
/// charge in sixty-thousandths of a unit (one tick moves exactly a rate's worth of them), satisfaction in basis points.
/// </summary>
public sealed class PowerNetwork
{
    private readonly List<Battery> batteries = [];

    public PowerNetwork(int index, IReadOnlyList<long> members)
    {
        Index = index;
        Members = members;
    }

    public int Index { get; }

    /// <summary>Ids of the power blocks of this network, ascending.</summary>
    public IReadOnlyList<long> Members { get; }

    /// <summary>The batteries of this network, in id order.</summary>
    public IReadOnlyList<Battery> Batteries => batteries;

    /// <summary>Burning generators' output in the last balance, thousandths of a unit per second.</summary>
    public long ProducedRate { get; internal set; }

    /// <summary>Demand of the machines that wanted to work in the last balance, thousandths of a unit per second.</summary>
    public long RequestedRate { get; internal set; }

    /// <summary>
    /// Share of the demand served in the last balance, 0–10 000 basis points: 10 000 when production and batteries
    /// cover it (or nothing asks), else floor((produced + taken from the batteries) × 10 000 / requested).
    /// </summary>
    public int SatisfactionBp { get; internal set; }

    /// <summary>Total charge of the batteries, sixty-thousandths of a unit.</summary>
    public long Stored
    {
        get
        {
            long sum = 0;
            foreach (var b in batteries)
            {
                sum += b.Charge;
            }

            return sum;
        }
    }

    /// <summary>Total capacity of the batteries, sixty-thousandths of a unit.</summary>
    public long Capacity { get; private set; }

    internal void AddBattery(Battery battery, long capacity)
    {
        batteries.Add(battery);
        Capacity += capacity;
    }
}

/// <summary>
/// The lasers between power nodes and other power blocks, and the networks they form with contact. The lasers are
/// the only stored state; <see cref="Rebuild"/> recomputes the networks (union-find over the blocks in id order) and
/// is called whenever a power block or a laser changes. Everything is in id order: no dictionary order is ever read.
/// </summary>
public sealed class PowerGrid
{
    private static readonly IComparer<(long A, long B)> LinkOrder =
        Comparer<(long A, long B)>.Create((x, y) => x.A != y.A ? x.A.CompareTo(y.A) : x.B.CompareTo(y.B));

    private readonly List<(long A, long B)> links = [];
    private readonly List<PowerNetwork> networks = [];
    private readonly Dictionary<long, PowerNetwork> networkOf = [];
    private readonly HashSet<int> networksWithSource = [];

    /// <summary>Lasers as id pairs with <c>A &lt; B</c>, ordered by A then B.</summary>
    public IReadOnlyList<(long A, long B)> Links => links;

    /// <summary>Networks after the last <see cref="Rebuild"/>, ordered by their lowest member id.</summary>
    public IReadOnlyList<PowerNetwork> Networks => networks;

    public bool HasLink(long a, long b) => a != b && links.BinarySearch(Pair(a, b), LinkOrder) >= 0;

    /// <summary>Lasers that end on <paramref name="nodeId"/> (a laser between two nodes counts on both).</summary>
    public int LinksOf(long nodeId)
    {
        var count = 0;
        foreach (var (a, b) in links)
        {
            if (a == nodeId || b == nodeId)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Network of the power block <paramref name="entityId"/>; null for anything else.</summary>
    public PowerNetwork? NetworkOf(long entityId) => networkOf.GetValueOrDefault(entityId);

    /// <summary>True when <paramref name="network"/> has a generator or a battery.</summary>
    internal bool HasSource(PowerNetwork network) => networksWithSource.Contains(network.Index);

    internal bool AddLink(long a, long b)
    {
        if (a == b)
        {
            return false;
        }

        var pair = Pair(a, b);
        var at = links.BinarySearch(pair, LinkOrder);
        if (at >= 0)
        {
            return false;
        }

        links.Insert(~at, pair);
        return true;
    }

    internal bool RemoveLink(long a, long b)
    {
        var at = links.BinarySearch(Pair(a, b), LinkOrder);
        if (at < 0)
        {
            return false;
        }

        links.RemoveAt(at);
        return true;
    }

    /// <summary>Drops every laser of <paramref name="entityId"/> (the block was taken away).</summary>
    internal void RemoveLinksOf(long entityId) => links.RemoveAll(l => l.A == entityId || l.B == entityId);

    /// <summary>Drops every laser (a restored game sets them again).</summary>
    internal void ClearLinks() => links.Clear();

    /// <summary>
    /// Recomputes the networks from the power blocks of <paramref name="s"/>, their contacts and the lasers. A laser
    /// whose end is no longer a power block is dropped.
    /// </summary>
    public void Rebuild(GameState s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var blocks = Blocks(s);
        var indexOf = new Dictionary<long, int>(blocks.Count);
        var cellOwner = new Dictionary<GridPosition, int>();
        for (var i = 0; i < blocks.Count; i++)
        {
            indexOf[blocks[i].Id] = i;
            foreach (var tile in Tiles(blocks[i]))
            {
                cellOwner[tile] = i;
            }
        }

        var parent = new int[blocks.Count];
        for (var i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }

        int Find(int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }

            return i;
        }

        void Union(int a, int b)
        {
            var ra = Find(a);
            var rb = Find(b);
            if (ra != rb)
            {
                // The lower index (lower id) stays the root: same result whatever the order of the unions.
                parent[Math.Max(ra, rb)] = Math.Min(ra, rb);
            }
        }

        // Contact: every cell just outside a block's square that another block covers.
        for (var i = 0; i < blocks.Count; i++)
        {
            var b = blocks[i];
            for (var k = 0; k < b.Size; k++)
            {
                foreach (var neighbour in new[]
                         {
                             new GridPosition(b.Origin.X + k, b.Origin.Y - 1),
                             new GridPosition(b.Origin.X + k, b.Origin.Y + b.Size),
                             new GridPosition(b.Origin.X - 1, b.Origin.Y + k),
                             new GridPosition(b.Origin.X + b.Size, b.Origin.Y + k)
                         })
                {
                    if (cellOwner.TryGetValue(neighbour, out var other))
                    {
                        Union(i, other);
                    }
                }
            }
        }

        links.RemoveAll(l => !indexOf.ContainsKey(l.A) || !indexOf.ContainsKey(l.B));
        foreach (var (a, b) in links)
        {
            Union(indexOf[a], indexOf[b]);
        }

        networks.Clear();
        networkOf.Clear();
        networksWithSource.Clear();
        var membersOfRoot = new Dictionary<int, List<long>>();
        var rootOrder = new List<int>();
        for (var i = 0; i < blocks.Count; i++)
        {
            var root = Find(i);
            if (!membersOfRoot.TryGetValue(root, out var members))
            {
                members = [];
                membersOfRoot[root] = members;
                rootOrder.Add(root);
            }

            members.Add(blocks[i].Id);
        }

        var batteryById = new Dictionary<long, Battery>(s.Batteries.Count);
        foreach (var battery in s.Batteries)
        {
            batteryById[battery.Id] = battery;
        }

        var batteryCapacity = Battery.CapacityOf(s.Content);
        foreach (var root in rootOrder)
        {
            var network = new PowerNetwork(networks.Count, membersOfRoot[root]);
            networks.Add(network);
            foreach (var id in network.Members)
            {
                networkOf[id] = network;
                var kind = blocks[indexOf[id]].Kind;
                if (kind is PowerBlockKind.Generator or PowerBlockKind.Battery)
                {
                    networksWithSource.Add(network.Index);
                }

                if (kind == PowerBlockKind.Battery)
                {
                    network.AddBattery(batteryById[id], batteryCapacity);
                }
            }
        }
    }

    /// <summary>
    /// The power blocks of <paramref name="s"/> in id order: generators, nodes, batteries and the blocks with a power
    /// use (forno, assembler, Miner T2, lamp). Belts, Miner T1, extractor, storage and the Core are not.
    /// </summary>
    public static List<PowerBlock> Blocks(GameState s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var blocks = new List<PowerBlock>();
        foreach (var g in s.Generators)
        {
            blocks.Add(new PowerBlock(g.Id, g.Position, Generator.Size, PowerBlockKind.Generator));
        }

        foreach (var n in s.PowerNodes)
        {
            blocks.Add(new PowerBlock(n.Id, n.Position, n.Size, PowerBlockKind.Node));
        }

        foreach (var b in s.Batteries)
        {
            blocks.Add(new PowerBlock(b.Id, b.Position, Battery.Size, PowerBlockKind.Battery));
        }

        foreach (var c in s.CraftMachines())
        {
            blocks.Add(new PowerBlock(c.Id, c.Position, Crafter.Size, PowerBlockKind.Consumer));
        }

        foreach (var m in s.Miners)
        {
            if (m.DefinitionId == Miner.AdvancedId)
            {
                blocks.Add(new PowerBlock(m.Id, m.Position, Miner.Size, PowerBlockKind.Consumer));
            }
        }

        foreach (var l in s.Lamps)
        {
            blocks.Add(new PowerBlock(l.Id, l.Position, Lamp.Size, PowerBlockKind.Consumer));
        }

        blocks.Sort((a, b) => a.Id.CompareTo(b.Id));
        return blocks;
    }

    /// <summary>The power block with id <paramref name="entityId"/>, or null.</summary>
    public static PowerBlock? FindBlock(GameState s, long entityId)
    {
        foreach (var block in Blocks(s))
        {
            if (block.Id == entityId)
            {
                return block;
            }
        }

        return null;
    }

    /// <summary>
    /// Squared distance between the edges of two squares, in cells: 0 when they touch or overlap, else the gap
    /// between them (dx² + dy², each the cells strictly between the two squares on that axis).
    /// </summary>
    public static long EdgeGapSquared(GridPosition a, int aSize, GridPosition b, int bSize)
    {
        long dx = Math.Max(0, Math.Max(b.X - (a.X + aSize), a.X - (b.X + bSize)));
        long dy = Math.Max(0, Math.Max(b.Y - (a.Y + aSize), a.Y - (b.Y + bSize)));
        return dx * dx + dy * dy;
    }

    /// <summary>Laser range of <paramref name="node"/> in cells (content, or the spec's numbers for code-built content).</summary>
    public static int RangeOf(FactoryContent content, PowerNode node) =>
        content.FindBuilding(node.DefinitionId)?.PowerLinkRange is > 0 and var range
            ? range
            : node.DefinitionId == PowerNode.Tier2Id ? PowerNode.Tier2LinkRange : PowerNode.Tier1LinkRange;

    /// <summary>Lasers <paramref name="node"/> can hold (content, or the spec's numbers for code-built content).</summary>
    public static int MaxLinksOf(FactoryContent content, PowerNode node) =>
        content.FindBuilding(node.DefinitionId)?.MaxPowerLinks is > 0 and var max
            ? max
            : node.DefinitionId == PowerNode.Tier2Id ? PowerNode.Tier2MaxLinks : PowerNode.Tier1MaxLinks;

    /// <summary>True when the edge distance of <paramref name="node"/> and <paramref name="other"/> is within <paramref name="range"/>.</summary>
    public static bool Reaches(PowerNode node, int range, PowerBlock other)
    {
        var gap = EdgeGapSquared(node.Position, node.Size, other.Origin, other.Size);
        return gap <= (long)range * range;
    }

    private static (long A, long B) Pair(long a, long b) => a < b ? (a, b) : (b, a);

    private static IEnumerable<GridPosition> Tiles(PowerBlock block)
    {
        for (var y = 0; y < block.Size; y++)
        {
            for (var x = 0; x < block.Size; x++)
            {
                yield return new GridPosition(block.Origin.X + x, block.Origin.Y + y);
            }
        }
    }
}
