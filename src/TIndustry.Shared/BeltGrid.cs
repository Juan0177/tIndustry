namespace TIndustry.Shared;

/// <summary>
/// Placeable logistics grid: belts, junctions, splitters, sorters, bridges.
/// Bridge mid-span is empty (ends only); sorter filters Mindustry-style.
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
                var rebuilt = new BeltGridCell(position, direction, definition);
                if (definition.Kind == LogisticsKind.Sorter)
                {
                    rebuilt.SetFilterItem(BeltGridCell.DefaultSorterFilter);
                }

                cells[position] = rebuilt;
                return true;
            }

            existing.Direction = direction;
            return true;
        }

        var cell = new BeltGridCell(position, direction, definition);
        if (definition.Kind == LogisticsKind.Sorter)
        {
            cell.SetFilterItem(BeltGridCell.DefaultSorterFilter);
        }

        cells[position] = cell;
        return true;
    }

    /// <summary>
    /// Place paired bridge ends (span 2–5). Mid tiles stay empty so belts can cross.
    /// </summary>
    public bool TryPlaceBridge(
        GridPosition entry,
        Direction direction,
        ConveyorDefinition definition,
        Func<GridPosition, bool>? canOccupy = null)
    {
        if (definition.Kind != LogisticsKind.Bridge
            || cells.ContainsKey(entry)
            || (canOccupy is not null && !canOccupy(entry)))
        {
            return false;
        }

        GridPosition? exit = null;
        for (var span = BeltGridCell.MinBridgeSpan; span <= BeltGridCell.MaxBridgeSpan; span++)
        {
            var candidate = entry.Step(direction, span);
            if (cells.ContainsKey(candidate))
            {
                continue;
            }

            if (canOccupy is not null && !canOccupy(candidate))
            {
                continue;
            }

            exit = candidate;
            break;
        }

        if (exit is null)
        {
            return false;
        }

        cells[entry] = new BeltGridCell(entry, direction, definition, bridgePartner: exit);
        cells[exit.Value] = new BeltGridCell(exit.Value, direction, definition, bridgePartner: entry);
        return true;
    }

    public bool TryRemove(GridPosition position)
    {
        if (!cells.TryGetValue(position, out var cell))
        {
            return false;
        }

        if (cell.Kind == LogisticsKind.Bridge && cell.BridgePartner is { } partner)
        {
            cells.Remove(position);
            cells.Remove(partner);
            return true;
        }

        return cells.Remove(position);
    }

    public void Clear() => cells.Clear();

    /// <summary>Place or replace a cell and restore items/toggle/partner/filter (save/load).</summary>
    public bool TryRestore(
        GridPosition position,
        Direction direction,
        ConveyorDefinition definition,
        int splitterToggle,
        IEnumerable<TransportedItem> items,
        GridPosition? bridgePartner = null,
        string? filterItemId = null)
    {
        if (definition.Kind == LogisticsKind.Belt)
        {
            DefaultDefinition ??= definition;
        }

        var cell = new BeltGridCell(position, direction, definition, bridgePartner, splitterToggle, filterItemId);
        cell.RestoreState(splitterToggle, items, bridgePartner, filterItemId);
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

    public bool TryInsert(GridPosition position, TransportedItem item, Direction? fromDirection = null)
    {
        if (!cells.TryGetValue(position, out var cell))
        {
            return false;
        }

        // Bridge start: accept 3 sides (not span); teleport instantly to end when free.
        if (cell.Kind == LogisticsKind.Bridge
            && cell.BridgePartner is { } partner
            && IsBridgeEntry(cell, partner))
        {
            if (fromDirection is { } incoming
                && incoming == DirectionMath.Opposite(cell.Direction))
            {
                // Coming from the span side — reject.
                return false;
            }

            if (cells.TryGetValue(partner, out var exitCell)
                && exitCell.TryInsert(item, cell.Direction))
            {
                // Instantaneous start→end; ready to leave end immediately.
                item.Progress = 1f;
                return true;
            }

            // End blocked: hold on start, already ready to teleport next handoff.
            if (!cell.TryInsert(item, fromDirection))
            {
                return false;
            }

            item.Progress = 1f;
            return true;
        }

        // Bridge end: only receives via teleport from start (handled above / handoff).
        if (cell.Kind == LogisticsKind.Bridge
            && cell.BridgePartner is { } endPartner
            && !IsBridgeEntry(cell, endPartner)
            && fromDirection is { } approach
            && approach == cell.Direction)
        {
            // External insert from the span side into the end pad — reject.
            return false;
        }

        return cell.TryInsert(item, fromDirection);
    }

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

        if (cell.Kind == LogisticsKind.Bridge && cell.BridgePartner is { } partner)
        {
            if (IsBridgeEntry(cell, partner)
                && cells.TryGetValue(partner, out var exitCell)
                && exitCell.TryInsert(item, cell.Direction))
            {
                item.Progress = 1f; // instantaneous; end ready to eject
                cell.RemoveOutput();
                return;
            }

            if (!IsBridgeEntry(cell, partner))
            {
                // End: output to all 3 sides except the span (Opposite(Direction)).
                if (TryHandoffTo(cell, cell.Direction, item)
                    || TryHandoffTo(cell, DirectionMath.Left(cell.Direction), item)
                    || TryHandoffTo(cell, DirectionMath.Right(cell.Direction), item))
                {
                    return;
                }
            }

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

        if (cell.Kind == LogisticsKind.Sorter)
        {
            if (cell.MatchesFilter(item.ItemId))
            {
                TryHandoffTo(cell, cell.Direction, item);
            }
            else if (TryHandoffTo(cell, DirectionMath.Left(cell.Direction), item)
                || TryHandoffTo(cell, DirectionMath.Right(cell.Direction), item))
            {
                // overflow routed sideways
            }

            return;
        }

        // Belt / bridge exit: facing neighbor.
        TryHandoffTo(cell, cell.Direction, item);
    }

    private static bool IsBridgeEntry(BeltGridCell cell, GridPosition partner)
    {
        var dx = partner.X - cell.Position.X;
        var dy = partner.Y - cell.Position.Y;
        var span = Math.Abs(dx) + Math.Abs(dy);
        if (span < BeltGridCell.MinBridgeSpan || span > BeltGridCell.MaxBridgeSpan)
        {
            return false;
        }

        return cell.Direction switch
        {
            Direction.North => dx == 0 && dy < 0,
            Direction.East => dy == 0 && dx > 0,
            Direction.South => dx == 0 && dy > 0,
            Direction.West => dy == 0 && dx < 0,
            _ => false
        };
    }

    private bool TryHandoffTo(BeltGridCell cell, Direction exit, TransportedItem item)
    {
        var target = cell.Position.Step(exit);
        // Route through grid TryInsert so bridge start gets 3-side + instant teleport.
        if (!TryInsert(target, item, exit))
        {
            // #region agent log
            if (!cells.ContainsKey(target) && item.Progress >= 1f
                && CoreDeliveryDebugLog.ShouldLogEdgeLeave())
            {
                CoreDeliveryDebugLog.Write(
                    "A,B",
                    "BeltGrid.cs:TryHandoffTo",
                    "edge_leave_blocked",
                    new
                    {
                        fromX = cell.Position.X,
                        fromY = cell.Position.Y,
                        dir = cell.Direction.ToString(),
                        exit = exit.ToString(),
                        targetX = target.X,
                        targetY = target.Y,
                        item.ItemId,
                        item.Progress,
                        targetOccupiedByBelt = false
                    });
            }
            // #endregion
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
    public const int MinBridgeSpan = 2;
    public const int MaxBridgeSpan = 5;
    public const string DefaultSorterFilter = "iron-ore";

    private readonly BeltCell inner;
    private int splitterToggle;
    private string? filterItemId;

    public BeltGridCell(
        GridPosition position,
        Direction direction,
        ConveyorDefinition definition,
        GridPosition? bridgePartner = null,
        int splitterToggle = 0,
        string? filterItemId = null)
    {
        Direction = direction;
        BridgePartner = bridgePartner;
        this.splitterToggle = Math.Max(0, splitterToggle);
        inner = new BeltCell(position, definition);
        if (definition.Kind == LogisticsKind.Sorter)
        {
            SetFilterItem(filterItemId);
        }
    }

    public GridPosition Position => inner.Position;
    public Direction Direction { get; set; }
    public ConveyorDefinition Definition => inner.Definition;
    public LogisticsKind Kind => inner.Kind;
    public IReadOnlyList<TransportedItem> Items => inner.Items;
    public GridPosition OutputPosition => Position.Step(Direction);
    public int SplitterToggle => splitterToggle;
    public GridPosition? BridgePartner { get; set; }
    public string? FilterItemId => filterItemId;

    public Direction PreferredSplitterExit =>
        splitterToggle % 2 == 0
            ? DirectionMath.Left(Direction)
            : DirectionMath.Right(Direction);

    public Direction AlternateSplitterExit =>
        PreferredSplitterExit == DirectionMath.Left(Direction)
            ? DirectionMath.Right(Direction)
            : DirectionMath.Left(Direction);

    public void AdvanceSplitterToggle() => splitterToggle++;

    public bool MatchesFilter(string itemId) =>
        Kind == LogisticsKind.Sorter
        && !string.IsNullOrWhiteSpace(filterItemId)
        && string.Equals(filterItemId, itemId, StringComparison.Ordinal);

    public void SetFilterItem(string? itemId)
    {
        if (Kind != LogisticsKind.Sorter)
        {
            filterItemId = null;
            return;
        }

        filterItemId = string.IsNullOrWhiteSpace(itemId) ? DefaultSorterFilter : itemId;
    }

    public void CycleFilterItem(IReadOnlyList<string> itemIds)
    {
        if (Kind != LogisticsKind.Sorter || itemIds.Count == 0)
        {
            return;
        }

        var current = filterItemId ?? DefaultSorterFilter;
        var idx = itemIds.ToList().FindIndex(id => string.Equals(id, current, StringComparison.Ordinal));
        var next = itemIds[(idx + 1 + itemIds.Count) % itemIds.Count];
        SetFilterItem(next);
    }

    public void RestoreState(
        int toggle,
        IEnumerable<TransportedItem> restoredItems,
        GridPosition? bridgePartner = null,
        string? filter = null)
    {
        splitterToggle = Math.Max(0, toggle);
        BridgePartner = bridgePartner;
        if (Kind == LogisticsKind.Sorter)
        {
            SetFilterItem(filter);
        }

        inner.RestoreItems(restoredItems);
    }

    public bool TryInsert(TransportedItem item, Direction? fromDirection = null) =>
        inner.TryInsert(item, fromDirection);

    public void Advance(float deltaSeconds) => inner.Advance(deltaSeconds);
    public TransportedItem? PeekOutput() => inner.PeekOutput();
    public void RemoveOutput() => inner.RemoveOutput();
    public bool TryRemoveItem(TransportedItem item) => inner.TryRemoveItem(item);
}
