namespace TIndustry.Shared;

/// <summary>
/// Placeable belt cells (straight belts only for Phase B). Free place/remove for Godot sandbox;
/// Advance + neighbor handoff mirrors Logistics ConveyorGrid belt path.
/// </summary>
public sealed class BeltGrid
{
    private readonly Dictionary<GridPosition, BeltGridCell> cells = new();

    public IReadOnlyDictionary<GridPosition, BeltGridCell> Cells => cells;

    public ConveyorDefinition? DefaultDefinition { get; private set; }

    public int Count => cells.Count;

    public bool Contains(GridPosition position) => cells.ContainsKey(position);

    public bool TryGet(GridPosition position, out BeltGridCell cell) =>
        cells.TryGetValue(position, out cell!);

    /// <summary>Sandbox place: no wallet/research. Overwrites direction if cell already exists.</summary>
    public bool TryPlaceFree(
        GridPosition position,
        Direction direction,
        ConveyorDefinition definition,
        Func<GridPosition, bool>? canOccupy = null)
    {
        DefaultDefinition ??= definition;
        if (canOccupy is not null && !canOccupy(position))
        {
            return false;
        }

        if (cells.TryGetValue(position, out var existing))
        {
            existing.Direction = direction;
            return true;
        }

        cells[position] = new BeltGridCell(position, direction, definition);
        return true;
    }

    public bool TryRemove(GridPosition position)
    {
        return cells.Remove(position);
    }

    public bool TryOrient(GridPosition position, Direction direction)
    {
        if (!cells.TryGetValue(position, out var cell))
        {
            return false;
        }

        cell.Direction = direction;
        return true;
    }

    public bool TryInsert(GridPosition position, TransportedItem item) =>
        cells.TryGetValue(position, out var cell) && cell.TryInsert(item);

    public void Tick(float deltaSeconds)
    {
        // Advance all cells (order does not matter for spacing within a cell).
        foreach (var cell in cells.Values)
        {
            cell.Advance(deltaSeconds);
        }

        // Handoff: each ready item tries its output neighbor.
        // Snapshot keys so mutation during place is safe; handoff only moves items.
        var positions = cells.Keys.ToList();
        foreach (var pos in positions)
        {
            if (!cells.TryGetValue(pos, out var from))
            {
                continue;
            }

            while (from.PeekOutput() is { } item)
            {
                var target = pos.Step(from.Direction);
                if (!cells.TryGetValue(target, out var to) || !to.TryInsert(item))
                {
                    break;
                }

                from.RemoveOutput();
            }
        }
    }

    /// <summary>
    /// Seed a contiguous path (directions derived cell→next; last keeps prior step).
    /// </summary>
    public void PlacePath(IReadOnlyList<GridPosition> path, ConveyorDefinition definition)
    {
        if (path.Count == 0)
        {
            return;
        }

        for (var i = 0; i < path.Count; i++)
        {
            Direction dir;
            if (i < path.Count - 1)
            {
                dir = BeltLane.DirectionBetween(path[i], path[i + 1]);
            }
            else if (i > 0)
            {
                dir = BeltLane.DirectionBetween(path[i - 1], path[i]);
            }
            else
            {
                dir = Direction.East;
            }

            TryPlaceFree(path[i], dir, definition);
        }
    }

    /// <summary>
    /// Incoming flow direction into <paramref name="position"/> from a neighbor that points here, if any.
    /// </summary>
    public bool TryGetIncomingDirection(GridPosition position, out Direction incoming)
    {
        foreach (var dir in DirectionMath.All)
        {
            var neighbor = position.Step(DirectionMath.Opposite(dir));
            if (cells.TryGetValue(neighbor, out var cell) && cell.Direction == dir)
            {
                incoming = dir;
                return true;
            }
        }

        incoming = default;
        return false;
    }

    public bool IsCorner(GridPosition position)
    {
        if (!cells.TryGetValue(position, out var cell))
        {
            return false;
        }

        if (!TryGetIncomingDirection(position, out var incoming))
        {
            return false;
        }

        return incoming != cell.Direction
            && IsPerpendicular(incoming, cell.Direction);
    }

    private static bool IsPerpendicular(Direction a, Direction b) =>
        DirectionMath.ToOffset(a).Dx * DirectionMath.ToOffset(b).Dx
            + DirectionMath.ToOffset(a).Dy * DirectionMath.ToOffset(b).Dy
        == 0;
}

public sealed class BeltGridCell
{
    private readonly BeltCell inner;

    public BeltGridCell(GridPosition position, Direction direction, ConveyorDefinition definition)
    {
        Direction = direction;
        inner = new BeltCell(position, definition);
    }

    public GridPosition Position => inner.Position;
    public Direction Direction { get; set; }
    public ConveyorDefinition Definition => inner.Definition;
    public IReadOnlyList<TransportedItem> Items => inner.Items;
    public GridPosition OutputPosition => Position.Step(Direction);

    public bool TryInsert(TransportedItem item) => inner.TryInsert(item);
    public void Advance(float deltaSeconds) => inner.Advance(deltaSeconds);
    public TransportedItem? PeekOutput() => inner.PeekOutput();
    public void RemoveOutput() => inner.RemoveOutput();
}
