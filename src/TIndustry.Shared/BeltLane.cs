namespace TIndustry.Shared;

/// <summary>
/// Belt lane using the same Advance math as Logistics ConveyorCell
/// (rate × dt, spacing limits). Supports straight strips and L-turns via path cells.
/// </summary>
public sealed class BeltLane
{
    private readonly List<BeltCell> cells = [];
    private readonly List<Direction> cellDirections = [];
    private long nextItemId = 1;

    /// <summary>Straight lane (all cells share <paramref name="direction"/>).</summary>
    public BeltLane(IReadOnlyList<GridPosition> path, Direction direction, ConveyorDefinition definition)
        : this(path, definition, direction)
    {
    }

    /// <summary>
    /// Path-derived directions: each cell faces the next; the last keeps the final step direction.
    /// </summary>
    public BeltLane(IReadOnlyList<GridPosition> path, ConveyorDefinition definition, Direction? fallbackDirection = null)
    {
        if (path.Count == 0)
        {
            throw new ArgumentException("Path vuoto.", nameof(path));
        }

        Definition = definition;
        for (var i = 0; i < path.Count; i++)
        {
            Direction dir;
            if (i < path.Count - 1)
            {
                dir = DirectionBetween(path[i], path[i + 1]);
            }
            else if (i > 0)
            {
                dir = DirectionBetween(path[i - 1], path[i]);
            }
            else if (fallbackDirection is { } fb)
            {
                dir = fb;
            }
            else
            {
                throw new ArgumentException("Serve almeno 2 celle o una direzione.", nameof(path));
            }

            cells.Add(new BeltCell(path[i], definition));
            cellDirections.Add(dir);
        }

        Direction = cellDirections[0];
    }

    public Direction Direction { get; }
    public ConveyorDefinition Definition { get; }
    public IReadOnlyList<BeltCell> Cells => cells;

    public Direction DirectionAt(int cellIndex) => cellDirections[cellIndex];

    public Direction DirectionAt(GridPosition position)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            if (cells[i].Position.Equals(position))
            {
                return cellDirections[i];
            }
        }

        return Direction;
    }

    public bool TrySpawnAtStart(string itemId)
    {
        return cells[0].TryInsert(new TransportedItem(nextItemId++, itemId));
    }

    public void Tick(float deltaSeconds)
    {
        // Advance rear → front so spacing limits use updated leaders (same as Logistics grid tick).
        for (var i = cells.Count - 1; i >= 0; i--)
        {
            cells[i].Advance(deltaSeconds);
        }

        // Handoff front → next
        for (var i = 0; i < cells.Count - 1; i++)
        {
            var from = cells[i];
            var to = cells[i + 1];
            var outgoing = from.PeekOutput();
            if (outgoing is null)
            {
                continue;
            }

            if (to.TryInsert(outgoing))
            {
                from.RemoveOutput();
            }
        }

        // Drop off end of belt (consumed / “arrived”)
        var last = cells[^1];
        if (last.PeekOutput() is not null)
        {
            last.RemoveOutput();
        }
    }

    public static Direction DirectionBetween(GridPosition from, GridPosition to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        if (Math.Abs(dx) + Math.Abs(dy) != 1)
        {
            throw new ArgumentException($"Celle non adiacenti: {from} → {to}");
        }

        if (dx == 1)
        {
            return Direction.East;
        }

        if (dx == -1)
        {
            return Direction.West;
        }

        return dy == 1 ? Direction.South : Direction.North;
    }

    public static bool IsClockwiseTurn(Direction from, Direction to) => (from, to) switch
    {
        (Direction.East, Direction.South) => true,
        (Direction.South, Direction.West) => true,
        (Direction.West, Direction.North) => true,
        (Direction.North, Direction.East) => true,
        (Direction.East, Direction.North) => false,
        (Direction.North, Direction.West) => false,
        (Direction.West, Direction.South) => false,
        (Direction.South, Direction.East) => false,
        _ => throw new ArgumentException($"Non è una svolta 90°: {from} → {to}")
    };
}

public sealed class BeltCell
{
    private readonly List<TransportedItem> items;

    public BeltCell(GridPosition position, ConveyorDefinition definition)
    {
        Position = position;
        Definition = definition;
        items = new List<TransportedItem>(definition.Capacity);
    }

    public GridPosition Position { get; }
    public ConveyorDefinition Definition { get; }
    public IReadOnlyList<TransportedItem> Items => items;

    public bool TryInsert(TransportedItem item)
    {
        if (items.Count >= Definition.Capacity)
        {
            return false;
        }

        var rear = items.Count == 0 ? null : items[^1];
        if (rear is not null && rear.Progress < Definition.ItemSpacing)
        {
            return false;
        }

        item.Progress = 0f;
        items.Add(item);
        return true;
    }

    public void Advance(float deltaSeconds)
    {
        var movement = Definition.RateItemsPerSecond * deltaSeconds;
        for (var index = 0; index < items.Count; index++)
        {
            var limit = index == 0
                ? 1f
                : items[index - 1].Progress - Definition.ItemSpacing;
            items[index].Progress = Math.Min(items[index].Progress + movement, limit);
        }
    }

    public TransportedItem? PeekOutput() =>
        items.Count > 0 && items[0].Progress >= 1f ? items[0] : null;

    public void RemoveOutput() => items.RemoveAt(0);
}
