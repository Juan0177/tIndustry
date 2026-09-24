namespace TIndustry.Shared;

/// <summary>
/// Placeable logistics grid: belts, junctions (cross-axis), splitters (T-fork).
/// Advance + handoff mirrors Logistics ConveyorGrid (sans bridge/sorter).
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
        if (definition.Kind == LogisticsKind.Belt)
        {
            DefaultDefinition ??= definition;
        }

        if (canOccupy is not null && !canOccupy(position))
        {
            return false;
        }

        if (cells.TryGetValue(position, out var existing))
        {
            // Replace kind by rebuilding cell when definition changes.
            if (!ReferenceEquals(existing.Definition, definition)
                && existing.Definition.Id != definition.Id)
            {
                cells[position] = new BeltGridCell(position, direction, definition);
                return true;
            }

            existing.Direction = direction;
            return true;
        }

        cells[position] = new BeltGridCell(position, direction, definition);
        return true;
    }

    public bool TryRemove(GridPosition position) => cells.Remove(position);

    public void Clear() => cells.Clear();

    /// <summary>Place or replace a cell and restore items/toggle (save/load).</summary>
    public bool TryRestore(
        GridPosition position,
        Direction direction,
        ConveyorDefinition definition,
        int splitterToggle,
        IEnumerable<TransportedItem> items)
    {
        if (definition.Kind == LogisticsKind.Belt)
        {
            DefaultDefinition ??= definition;
        }

        var cell = new BeltGridCell(position, direction, definition);
        cell.RestoreState(splitterToggle, items);
        cells[position] = cell;
        return true;
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

    public bool TryInsert(GridPosition position, TransportedItem item, Direction? fromDirection = null) =>
        cells.TryGetValue(position, out var cell) && cell.TryInsert(item, fromDirection);

    public void Tick(float deltaSeconds)
    {
        foreach (var cell in cells.Values)
        {
            cell.Advance(deltaSeconds);
        }

        var positions = cells.Keys.ToList();
        foreach (var pos in positions)
        {
            if (!cells.TryGetValue(pos, out var from))
            {
                continue;
            }

            TryHandoff(from);
        }
    }

    private void TryHandoff(BeltGridCell cell)
    {
        if (cell.Kind == LogisticsKind.Junction)
        {
            // Both axes leave independently — a blocked exit must not stall the other stream.
            for (var i = 0; i < cell.Items.Count;)
            {
                var ready = cell.Items[i];
                if (ready.Progress < 1f)
                {
                    i++;
                    continue;
                }

                var exit = ready.Travel ?? cell.Direction;
                var target = cell.Position.Step(exit);
                if (!cells.TryGetValue(target, out var next) || !next.TryInsert(ready, exit))
                {
                    i++;
                    continue;
                }

                cell.TryRemoveItem(ready);
            }

            return;
        }

        var item = cell.PeekOutput();
        if (item is null)
        {
            return;
        }

        if (cell.Kind == LogisticsKind.Splitter)
        {
            if (TryHandoffTo(cell, cell.PreferredSplitterExit, item)
                || TryHandoffTo(cell, cell.AlternateSplitterExit, item))
            {
                cell.AdvanceSplitterToggle();
            }

            return;
        }

        // Belt (and future bridge/sorter stubs): exit along facing.
        TryHandoffTo(cell, cell.Direction, item);
    }

    private bool TryHandoffTo(BeltGridCell cell, Direction exit, TransportedItem item)
    {
        var target = cell.Position.Step(exit);
        if (!cells.TryGetValue(target, out var next) || !next.TryInsert(item, exit))
        {
            return false;
        }

        cell.RemoveOutput();
        return true;
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

        // Specials are not gallery corners.
        if (cell.Kind is not LogisticsKind.Belt)
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

    public bool IsSpecial(GridPosition position) =>
        cells.TryGetValue(position, out var cell) && cell.Kind is not LogisticsKind.Belt;

    private static bool IsPerpendicular(Direction a, Direction b) =>
        DirectionMath.ToOffset(a).Dx * DirectionMath.ToOffset(b).Dx
            + DirectionMath.ToOffset(a).Dy * DirectionMath.ToOffset(b).Dy
        == 0;
}

public sealed class BeltGridCell
{
    private readonly BeltCell inner;
    private int splitterToggle;

    public BeltGridCell(GridPosition position, Direction direction, ConveyorDefinition definition)
    {
        Direction = direction;
        inner = new BeltCell(position, definition);
    }

    public GridPosition Position => inner.Position;
    public Direction Direction { get; set; }
    public ConveyorDefinition Definition => inner.Definition;
    public LogisticsKind Kind => inner.Kind;
    public IReadOnlyList<TransportedItem> Items => inner.Items;
    public GridPosition OutputPosition => Position.Step(Direction);
    public int SplitterToggle => splitterToggle;

    public Direction PreferredSplitterExit =>
        splitterToggle % 2 == 0
            ? DirectionMath.Left(Direction)
            : DirectionMath.Right(Direction);

    public Direction AlternateSplitterExit =>
        PreferredSplitterExit == DirectionMath.Left(Direction)
            ? DirectionMath.Right(Direction)
            : DirectionMath.Left(Direction);

    public void AdvanceSplitterToggle() => splitterToggle++;

    public void RestoreState(int toggle, IEnumerable<TransportedItem> restoredItems)
    {
        splitterToggle = Math.Max(0, toggle);
        inner.RestoreItems(restoredItems);
    }

    public bool TryInsert(TransportedItem item, Direction? fromDirection = null) =>
        inner.TryInsert(item, fromDirection);

    public void Advance(float deltaSeconds) => inner.Advance(deltaSeconds);
    public TransportedItem? PeekOutput() => inner.PeekOutput();
    public void RemoveOutput() => inner.RemoveOutput();
    public bool TryRemoveItem(TransportedItem item) => inner.TryRemoveItem(item);
}
