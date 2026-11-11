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

    /// <summary>
    /// When true (default), ready items fall off the last cell.
    /// When false, leave them for <see cref="CoreStockSink"/> / handoff consumers.
    /// </summary>
    public bool AutoDropAtEnd { get; set; } = true;

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

    public bool TryGetCell(GridPosition position, out BeltCell cell)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            if (cells[i].Position.Equals(position))
            {
                cell = cells[i];
                return true;
            }
        }

        cell = null!;
        return false;
    }

    public bool TryInsertAt(GridPosition position, TransportedItem item) =>
        TryGetCell(position, out var cell) && cell.TryInsert(item);

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

            if (to.TryInsert(outgoing, cellDirections[i]))
            {
                from.RemoveOutput();
            }
        }

        if (!AutoDropAtEnd)
        {
            return;
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
    private float tickMovement; // how far an item moves in one tick here (last Advance)
    private int sentThisTick; // junction: bit (int)Direction set for each queue that handed an item on this tick

    public BeltCell(GridPosition position, ConveyorDefinition definition)
    {
        Position = position;
        Definition = definition;
        items = new List<TransportedItem>(Math.Max(1, definition.Capacity));
    }

    public GridPosition Position { get; }
    public ConveyorDefinition Definition { get; }
    public LogisticsKind Kind => Definition.Kind;
    public IReadOnlyList<TransportedItem> Items => items;

    /// <summary>
    /// Stack conveyor's loading cell: ticks the pack being formed has waited since its last piece came in (0 to
    /// <see cref="BeltGrid.StackWaitTicks"/> − 1); it leaves when full or when this reaches
    /// <see cref="BeltGrid.StackWaitTicks"/>. 0 elsewhere. Saved.
    /// </summary>
    public int StackWaitTicks { get; private set; }

    /// <summary>
    /// Stack conveyor's loading cell: whether <see cref="TryLoad"/> would take <paramref name="item"/> (changes nothing).
    /// A single piece (<see cref="TransportedItem.Count"/> 1) starts a pack on an empty cell or joins the pack being formed
    /// (still at the start of the cell) if it is the same item and not full.
    /// </summary>
    public bool CanLoad(TransportedItem item)
    {
        if (Kind != LogisticsKind.Stack || item.Count != 1)
        {
            return false;
        }

        if (items.Count == 0)
        {
            return true;
        }

        var pack = items[0];
        return items.Count == 1
            && pack.Progress <= 0f
            && pack.Count < BeltGrid.StackPackSize
            && string.Equals(pack.ItemId, item.ItemId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Stack conveyor's loading cell: <paramref name="item"/> starts a pack or joins the pack being formed (the item
    /// object is then dropped: the pack counts it); the wait starts again (<see cref="StackWaitTicks"/> 0).
    /// </summary>
    public bool TryLoad(TransportedItem item)
    {
        if (!CanLoad(item))
        {
            return false;
        }

        if (items.Count == 0)
        {
            item.Progress = 0f;
            item.Carry = null;
            item.Travel = null;
            item.JunctionTicks = null;
            items.Add(item);
        }
        else
        {
            items[0].Count++;
        }

        StackWaitTicks = 0;
        return true;
    }

    /// <summary>Stack conveyor state for save/load: ticks the pack being formed has waited (kept only on a stack cell holding a pack).</summary>
    public void RestoreStackWait(int ticks) =>
        StackWaitTicks = Kind == LogisticsKind.Stack && items.Count > 0
            ? Math.Clamp(ticks, 0, BeltGrid.StackWaitTicks - 1)
            : 0;

    /// <summary>Whether <see cref="TryInsert"/> would take an item now (changes nothing).</summary>
    public bool CanInsert(Direction? fromDirection = null) =>
        Kind == LogisticsKind.Junction
            ? fromDirection is { } way && QueueCount(way) < BeltGridCell.JunctionQueueLength
            : items.Count < Definition.Capacity && (items.Count == 0 || items[^1].Progress >= Definition.ItemSpacing);

    public bool TryInsert(TransportedItem item, Direction? fromDirection = null)
    {
        if (Kind != LogisticsKind.Junction && items.Count >= Definition.Capacity)
        {
            return false;
        }

        if (Kind == LogisticsKind.Junction)
        {
            return TryEnqueue(item, fromDirection);
        }

        var rear = items.Count == 0 ? null : items[^1];
        if (rear is not null && rear.Progress < Definition.ItemSpacing)
        {
            return false;
        }

        // Where the item would be had it entered as soon as there was room: a handoff moves for what is left of its tick,
        // at this cell's speed; a new item behind another (its source was waiting) starts right behind it, at most a tick in.
        var start = item.Carry is { } left ? left * tickMovement : rear is null ? 0f : tickMovement;
        item.Progress = rear is null ? Math.Min(start, 1f) : Math.Min(start, rear.Progress - Definition.ItemSpacing);
        item.Carry = null;
        item.Travel = null;
        item.JunctionTicks = null;
        items.Add(item);
        return true;
    }

    /// <summary>
    /// Junction (Mindustry's Junction): the item joins the queue of its direction of travel
    /// (<paramref name="travel"/>) if that queue holds fewer than <see cref="BeltGridCell.JunctionQueueLength"/> items.
    /// The queues share <see cref="items"/> (in order of entry); an item's <see cref="TransportedItem.Travel"/> says
    /// which queue it is in.
    /// </summary>
    private bool TryEnqueue(TransportedItem item, Direction? travel)
    {
        if (travel is not { } way || QueueCount(way) >= BeltGridCell.JunctionQueueLength)
        {
            return false;
        }

        item.Progress = 0f;
        item.Carry = null;
        item.Travel = way;
        item.JunctionTicks = 0;
        items.Add(item);
        return true;
    }

    private int QueueCount(Direction way)
    {
        var count = 0;
        foreach (var item in items)
        {
            count += item.Travel == way ? 1 : 0;
        }

        return count;
    }

    /// <summary>
    /// Junction: the head of the queue travelling <paramref name="way"/> once it has crossed
    /// (<see cref="BeltGridCell.JunctionCrossTicks"/> ticks inside) and if that queue has not handed an item on this
    /// tick yet; null otherwise. Only the head can leave: a blocked exit holds its own queue back, never the others.
    /// </summary>
    public TransportedItem? PeekQueueHead(Direction way)
    {
        if ((sentThisTick & (1 << (int)way)) != 0)
        {
            return null;
        }

        foreach (var item in items)
        {
            if (item.Travel == way)
            {
                return item.JunctionTicks >= BeltGridCell.JunctionCrossTicks ? item : null;
            }
        }

        return null;
    }

    /// <summary>
    /// Junction: takes away <paramref name="head"/>, the head <see cref="PeekQueueHead"/> returned for
    /// <paramref name="way"/> (by reference: the cell that took it may already have changed its fields).
    /// </summary>
    public void RemoveQueueHead(Direction way, TransportedItem head)
    {
        if (items.Remove(head))
        {
            sentThisTick |= 1 << (int)way;
        }
    }

    /// <summary>Overwrite items for save/load (bypasses spacing / capacity checks).</summary>
    public void RestoreItems(IEnumerable<TransportedItem> restored)
    {
        items.Clear();
        foreach (var item in restored)
        {
            if (Kind == LogisticsKind.Junction)
            {
                // A save without the tick count: what its progress across the junction says.
                item.JunctionTicks = Math.Clamp(
                    item.JunctionTicks ?? (int)Math.Round(item.Progress * BeltGridCell.JunctionCrossTicks),
                    0,
                    BeltGridCell.JunctionCrossTicks);
                item.Travel ??= Direction.East;
            }

            items.Add(item);
        }
    }

    public void Advance(float deltaSeconds) => Advance(deltaSeconds, stackLoader: false);

    /// <summary>
    /// <see cref="Advance(float)"/>; on a stack conveyor's loading cell (<paramref name="stackLoader"/>) a pack being
    /// formed (at the start of the cell, not full) stays put and counts the tick; it leaves on the tick it is full or
    /// has waited <see cref="BeltGrid.StackWaitTicks"/> ticks.
    /// </summary>
    internal void Advance(float deltaSeconds, bool stackLoader)
    {
        var movement = Definition.RateItemsPerSecond * deltaSeconds;
        if (movement <= 0f)
        {
            // The settle pass (Dt 0) keeps this tick's carries. The junction ignores the rate for its speed (it ages by
            // ticks) but still needs a positive rate in its definition to tell a real tick from the settle pass.
            return;
        }

        tickMovement = movement;

        if (Kind == LogisticsKind.Stack)
        {
            var forming = stackLoader
                && items.Count > 0
                && items[0].Progress <= 0f
                && items[0].Count < BeltGrid.StackPackSize;
            if (forming && ++StackWaitTicks < BeltGrid.StackWaitTicks)
            {
                return;
            }

            StackWaitTicks = 0;
        }

        if (Kind == LogisticsKind.Junction)
        {
            // Every queued item ages, blocked heads included: an item behind a blocked head is ready as soon as it leaves.
            sentThisTick = 0;
            foreach (var item in items)
            {
                var ticks = Math.Min((item.JunctionTicks ?? 0) + 1, BeltGridCell.JunctionCrossTicks);
                item.JunctionTicks = ticks;
                item.Progress = (float)ticks / BeltGridCell.JunctionCrossTicks;
            }

            return;
        }

        Move(1f);
    }

    /// <summary>
    /// Moves every item for <paramref name="ticks"/> of a tick (null: for its <see cref="TransportedItem.Carry"/>), never
    /// past the cell end nor closer than the spacing to the item ahead; the part of the tick it cannot use stays its carry.
    /// </summary>
    private void Move(float? ticks)
    {
        for (var index = 0; index < items.Count; index++)
        {
            var limit = index == 0
                ? 1f
                : items[index - 1].Progress - Definition.ItemSpacing;
            var item = items[index];
            var budget = (ticks ?? item.Carry ?? 0f) * tickMovement;
            var moved = Math.Min(item.Progress + budget, limit);
            item.Carry = budget > 0f ? Math.Clamp(item.Progress + budget - moved, 0f, budget) / tickMovement : 0f;
            item.Progress = moved;
        }
    }

    /// <summary>The head of a belt once at the end of the cell; always null on a junction (<see cref="PeekQueueHead"/>).</summary>
    public TransportedItem? PeekOutput() =>
        Kind != LogisticsKind.Junction && items.Count > 0 && items[0].Progress >= 1f ? items[0] : null;

    /// <summary>
    /// Takes the head away. The items behind it then spend the movement it held back this tick, as if it had left as
    /// soon as it reached the end (Mindustry passes an item on within its move), so a saturated line keeps its spacing.
    /// </summary>
    public void RemoveOutput()
    {
        items.RemoveAt(0);
        Move(null);
    }

    public bool TryRemoveItem(TransportedItem item)
    {
        var idx = items.IndexOf(item);
        if (idx < 0)
        {
            return false;
        }

        items.RemoveAt(idx);
        return true;
    }

}
