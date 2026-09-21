namespace TIndustry.Logistics;

/// <summary>
/// Placeable 1×1 power conduit tiles. Local networks are 4-connected cable components;
/// generators and the core inject power when adjacent to a cable (or a consumer touches them).
/// </summary>
public sealed class PowerCableGrid
{
    private readonly HashSet<GridPosition> cells = [];

    public IReadOnlyCollection<GridPosition> Cells => cells;
    public int Count => cells.Count;
    public bool Contains(GridPosition position) => cells.Contains(position);

    public bool TryAdd(GridPosition position) => cells.Add(position);

    public bool TryRemove(GridPosition position) => cells.Remove(position);

    public void Clear() => cells.Clear();
}

/// <summary>Resolved power connectivity for one simulation tick.</summary>
public sealed class PowerNetworkState
{
    private readonly HashSet<GridPosition> poweredCables;
    private readonly HashSet<GridPosition> poweredGeneratorOrigins;
    private readonly bool coreLive;

    public PowerNetworkState(
        HashSet<GridPosition> poweredCables,
        HashSet<GridPosition> poweredGeneratorOrigins,
        bool coreLive,
        float capacity,
        float generationPerSecond)
    {
        this.poweredCables = poweredCables;
        this.poweredGeneratorOrigins = poweredGeneratorOrigins;
        this.coreLive = coreLive;
        Capacity = capacity;
        GenerationPerSecond = generationPerSecond;
    }

    public static PowerNetworkState Empty { get; } = new([], [], true, FactoryWorld.CorePowerCapacity, FactoryWorld.CorePowerGeneration);

    public float Capacity { get; }
    public float GenerationPerSecond { get; }
    public int PoweredCableCount => poweredCables.Count;

    public bool IsCablePowered(GridPosition position) => poweredCables.Contains(position);

    public bool IsGeneratorPowered(GridPosition origin) => poweredGeneratorOrigins.Contains(origin);
}

public static class PowerNetworking
{
    /// <summary>
    /// Builds powered cable sets: a cable component is live when it touches the core
    /// or at least one currently generating generator.
    /// </summary>
    public static PowerNetworkState Build(
        FactoryWorld world,
        PowerCableGrid cables,
        IEnumerable<GeneratorBuilding> generators)
    {
        var generating = generators.Where(g => g.IsGenerating).ToList();
        var poweredCables = new HashSet<GridPosition>();
        var poweredGens = new HashSet<GridPosition>();
        var visited = new HashSet<GridPosition>();

        foreach (var start in cables.Cells)
        {
            if (!visited.Add(start))
            {
                continue;
            }

            var component = new List<GridPosition>();
            var queue = new Queue<GridPosition>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);
                for (var d = 0; d < DirectionMath.All.Length; d++)
                {
                    var next = current.Step(DirectionMath.All[d]);
                    if (!cables.Contains(next) || !visited.Add(next))
                    {
                        continue;
                    }

                    queue.Enqueue(next);
                }
            }

            var touchesCore = component.Any(cable => TouchesTiles(cable, world.CoreTiles));
            var linkedGens = new List<GeneratorBuilding>();
            foreach (var gen in generating)
            {
                if (component.Any(cable => TouchesFootprint(cable, gen.Position, GeneratorBuilding.Size)))
                {
                    linkedGens.Add(gen);
                }
            }

            if (!touchesCore && linkedGens.Count == 0)
            {
                continue;
            }

            foreach (var cable in component)
            {
                poweredCables.Add(cable);
            }

            foreach (var gen in linkedGens)
            {
                poweredGens.Add(gen.Position);
            }
        }

        // Generators with no cables still form a tiny local node (direct adjacency).
        foreach (var gen in generating)
        {
            poweredGens.Add(gen.Position);
        }

        var capacity = FactoryWorld.CorePowerCapacity
            + generating.Count * GeneratorBuilding.CapacityBonus;
        var generation = FactoryWorld.CorePowerGeneration
            + generating.Count * GeneratorBuilding.GenerationPerSecond;
        return new PowerNetworkState(poweredCables, poweredGens, true, capacity, generation);
    }

    public static bool IsBuildingPowered(
        FactoryWorld world,
        PowerNetworkState networks,
        GridPosition origin,
        int size)
    {
        // Touching the core always counts (early-game without conduits).
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

        // Adjacent to any powered cable tile.
        foreach (var tile in Footprint(origin, size))
        {
            for (var d = 0; d < DirectionMath.All.Length; d++)
            {
                var neighbor = tile.Step(DirectionMath.All[d]);
                if (networks.IsCablePowered(neighbor))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TouchesTiles(GridPosition cable, IReadOnlySet<GridPosition> tiles)
    {
        for (var d = 0; d < DirectionMath.All.Length; d++)
        {
            if (tiles.Contains(cable.Step(DirectionMath.All[d])))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TouchesFootprint(GridPosition cable, GridPosition origin, int size)
    {
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var tile = new GridPosition(origin.X + x, origin.Y + y);
                for (var d = 0; d < DirectionMath.All.Length; d++)
                {
                    if (cable == tile.Step(DirectionMath.All[d]))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
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
