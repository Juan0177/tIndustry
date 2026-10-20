namespace TIndustry.Shared;

/// <summary>
/// Placeable logistics grid: belts, junctions, splitters, sorters, bridges.
/// Bridge mid-span is empty (ends only); sorter filters Mindustry-style.
/// Cells are kept in canonical (Y, X) order: every pass over the grid (advance, handoff,
/// core drain, save) visits them the same way regardless of placement history.
/// </summary>
public sealed class BeltGrid
{
    private readonly SortedDictionary<GridPosition, BeltGridCell> cells = new(CanonicalOrder.Instance);

    public IReadOnlyDictionary<GridPosition, BeltGridCell> Cells => cells;

    public ConveyorDefinition? DefaultDefinition { get; private set; }

    public int Count => cells.Count;

    /// <summary>Bumped on every change of the layout (cells, kinds, directions, partners); items do not count.</summary>
    public int Version { get; private set; }

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
                Version++;
                return true;
            }

            existing.Direction = direction;
            Version++;
            return true;
        }

        var cell = new BeltGridCell(position, direction, definition);
        if (definition.Kind == LogisticsKind.Sorter)
        {
            cell.SetFilterItem(BeltGridCell.DefaultSorterFilter);
        }

        cells[position] = cell;
        Version++;
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
        Version++;
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
            Version++;
            return true;
        }

        Version++;
        return cells.Remove(position);
    }

    public void Clear()
    {
        cells.Clear();
        Version++;
    }

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
        Version++;
        return true;
    }

    public bool TryOrient(GridPosition position, Direction direction)
    {
        if (!cells.TryGetValue(position, out var cell))
        {
            return false;
        }

        cell.Direction = direction;
        Version++;
        return true;
    }

    /// <summary>
    /// Puts <paramref name="item"/> on the cell at <paramref name="position"/>. <paramref name="source"/> is the tile
    /// the item comes from (a building's footprint tile beside the cell); without it, a handoff between cells comes
    /// from <c>position.Step(Opposite(fromDirection))</c>. An armored belt refuses what <see cref="ArmoredAccepts"/> refuses.
    /// </summary>
    public bool TryInsert(
        GridPosition position,
        TransportedItem item,
        Direction? fromDirection = null,
        GridPosition? source = null)
    {
        var origin = source ?? (fromDirection is { } from ? position.Step(DirectionMath.Opposite(from)) : null);
        if (!cells.TryGetValue(position, out var cell) || !ArmoredAccepts(cell, origin) || HeadOn(cell, origin))
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

        // Two passes, both in canonical order: a cell whose handoff fails in the first (the next cell was still full)
        // retries once, so a cell downstream that emptied later in the first pass takes its item in the same tick
        // whatever the line's direction.
        var refused = new List<BeltGridCell>();
        foreach (var cell in cells.Values.ToList())
        {
            TryHandoff(cell);
            if (cell.Items.Any(item => item.Progress >= 1f))
            {
                refused.Add(cell);
            }
        }

        foreach (var cell in refused)
        {
            TryHandoff(cell);
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
                if (!cells.TryGetValue(target, out var next)
                    || !ArmoredAccepts(next, cell.Position)
                    || !next.TryInsert(ready, exit))
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

    /// <summary>
    /// Mindustry's armored conveyor: an armored belt takes items from the cell behind it or from a plain belt on any
    /// side, never from a building or a special on its side; an item of unknown origin is refused. Other cells take all.
    /// </summary>
    private bool ArmoredAccepts(BeltGridCell cell, GridPosition? source) =>
        !cell.Definition.IsArmoredBelt
        || (source is { } from
            && (from == cell.Position.Step(DirectionMath.Opposite(cell.Direction))
                || (cells.TryGetValue(from, out var feeder) && feeder.Kind == LogisticsKind.Belt)));

    /// <summary>Mindustry's conveyor: a belt never takes from the belt it faces (two belts head-on would bounce items).</summary>
    private bool HeadOn(BeltGridCell cell, GridPosition? source) =>
        cell.Kind == LogisticsKind.Belt && source == cell.OutputPosition
        && cells.TryGetValue(cell.OutputPosition, out var feeder) && feeder.Kind == LogisticsKind.Belt;

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
    /// Flow entering the belt at <paramref name="position"/>, Mindustry-style: a feeder behind wins (straight,
    /// incoming = the belt's own direction); otherwise exactly one feeder on a side gives a corner from that side.
    /// Feeders on both sides and none behind (a T merge) or no feeder at all: false.
    /// <paramref name="buildingFeeds"/>(source, target) says whether a building on tile source ejects into the
    /// logistics cell at target; without it only logistics cells count.
    /// </summary>
    public bool TryGetIncomingDirection(
        GridPosition position,
        out Direction incoming,
        Func<GridPosition, GridPosition, bool>? buildingFeeds = null)
    {
        incoming = default;
        if (!cells.TryGetValue(position, out var cell))
        {
            return false;
        }

        var dir = cell.Direction;
        var (back, left, right) = Feeders(position, dir, buildingFeeds);
        return IncomingFrom(dir, back, left, right, out incoming);
    }

    internal static bool IncomingFrom(Direction dir, bool back, bool left, bool right, out Direction incoming)
    {
        incoming = dir;
        if (back)
        {
            return true;
        }

        if (left == right)
        {
            return false;
        }

        // A feeder on the left side flows rightwards into the cell, and vice versa.
        incoming = left ? DirectionMath.Right(dir) : DirectionMath.Left(dir);
        return true;
    }

    /// <summary>
    /// How a plain belt is drawn (visual only, Mindustry's conveyor blend): no side feeder = straight, exactly
    /// one side feeder = corner from that side (<paramref name="incoming"/>), feeders on both sides and none
    /// behind = T; a feeder behind plus side feeders = a side join (<see cref="ShapeFrom"/>). Specials and empty
    /// cells are straight; <paramref name="incoming"/> is then the cell's direction (default for an empty cell).
    /// <paramref name="buildingFeeds"/> decides which buildings count (the client view passes "exchanges items
    /// with belts", Mindustry's blend by capability).
    /// </summary>
    public BeltShape GetShape(
        GridPosition position,
        out Direction incoming,
        Func<GridPosition, GridPosition, bool>? buildingFeeds = null)
    {
        incoming = default;
        if (!cells.TryGetValue(position, out var cell))
        {
            return BeltShape.Straight;
        }

        var dir = cell.Direction;
        incoming = dir;
        if (cell.Kind is not LogisticsKind.Belt)
        {
            return BeltShape.Straight;
        }

        var (back, left, right) = Feeders(position, dir, buildingFeeds);
        IncomingFrom(dir, back, left, right, out incoming);
        return ShapeFrom(back, left, right, BuildingFeeders(position, dir, buildingFeeds));
    }

    /// <summary>
    /// <see cref="GetShape"/> of a plain belt from its feeders (behind, left, right) and which sides are fed by a
    /// building. Behind plus one side (belt or building) = the dot block open on that side; behind plus belts on both
    /// sides = the T open behind; behind plus both sides with a building on at least one = the dot block open on both.
    /// </summary>
    internal static BeltShape ShapeFrom(bool back, bool left, bool right, (bool Left, bool Right) building = default) =>
        !left && !right ? BeltShape.Straight
        : !back ? (left && right ? BeltShape.T : BeltShape.Corner)
        : !(left && right) ? (left ? BeltShape.JoinLeft : BeltShape.JoinRight)
        : building.Left || building.Right ? BeltShape.JoinBuildingBoth
        : BeltShape.JoinBoth;

    /// <summary>Which sides of the belt at <paramref name="position"/> facing <paramref name="dir"/> a building feeds.</summary>
    internal (bool Left, bool Right) BuildingFeeders(
        GridPosition position,
        Direction dir,
        Func<GridPosition, GridPosition, bool>? buildingFeeds)
    {
        if (cells.TryGetValue(position, out var cell) && cell.Definition.IsArmoredBelt)
        {
            return (false, false); // an armored belt never blends with a building on its side
        }

        bool Feeds(GridPosition side) => !cells.ContainsKey(side) && buildingFeeds?.Invoke(side, position) == true;
        return (Feeds(position.Step(DirectionMath.Left(dir))), Feeds(position.Step(DirectionMath.Right(dir))));
    }

    public bool IsCorner(GridPosition position, Func<GridPosition, GridPosition, bool>? buildingFeeds = null) =>
        GetShape(position, out _, buildingFeeds) == BeltShape.Corner;

    /// <summary>
    /// Whether the junction at <paramref name="position"/> has an input or an output on <paramref name="side"/>
    /// (visual: a side without either keeps its rail). Input: the neighbour there feeds into the junction. Output: items
    /// cross toward that side (something feeds the opposite side) and a logistics cell there takes them. Buildings
    /// never connect: a junction refuses their directionless inserts and only hands items to logistics cells.
    /// </summary>
    public bool IsJunctionSideConnected(GridPosition position, Direction side)
    {
        if (!cells.TryGetValue(position, out var cell) || cell.Kind != LogisticsKind.Junction)
        {
            return false;
        }

        bool FeedsFrom(Direction from) =>
            FeedsToward(position.Step(from), DirectionMath.Opposite(from), null, JunctionChainLimit);

        return FeedsFrom(side) || (cells.ContainsKey(position.Step(side)) && FeedsFrom(DirectionMath.Opposite(side)));
    }

    /// <summary>Whether something feeds the cell (facing <paramref name="dir"/>) from behind, from its left and from its right.</summary>
    internal (bool Back, bool Left, bool Right) Feeders(
        GridPosition position,
        Direction dir,
        Func<GridPosition, GridPosition, bool>? buildingFeeds)
    {
        var left = DirectionMath.Left(dir);
        var right = DirectionMath.Right(dir);
        // Mirrors ArmoredAccepts: on an armored belt only a plain belt counts on a side.
        var armored = cells.TryGetValue(position, out var cell) && cell.Definition.IsArmoredBelt;
        bool Side(GridPosition side, Direction flow) =>
            armored
                ? cells.TryGetValue(side, out var feeder) && feeder.Kind == LogisticsKind.Belt && feeder.Direction == flow
                : FeedsToward(side, flow, buildingFeeds, JunctionChainLimit);
        return (
            FeedsToward(position.Step(DirectionMath.Opposite(dir)), dir, buildingFeeds, JunctionChainLimit),
            Side(position.Step(left), right),
            Side(position.Step(right), left));
    }

    // Junctions pass items straight through: look this many junctions back for the real source.
    private const int JunctionChainLimit = 4;

    /// <summary>True when whatever sits on <paramref name="source"/> can eject items toward <paramref name="flow"/>.</summary>
    private bool FeedsToward(
        GridPosition source,
        Direction flow,
        Func<GridPosition, GridPosition, bool>? buildingFeeds,
        int junctionsLeft)
    {
        if (!cells.TryGetValue(source, out var cell))
        {
            return buildingFeeds?.Invoke(source, source.Step(flow)) == true;
        }

        // Mirrors TryHandoff: which exits each kind can use.
        var back = DirectionMath.Opposite(cell.Direction);
        return cell.Kind switch
        {
            LogisticsKind.Belt => cell.Direction == flow,
            LogisticsKind.Splitter => flow != cell.Direction && flow != back,
            LogisticsKind.Sorter => flow != back,
            LogisticsKind.Bridge when cell.BridgePartner is { } partner =>
                !IsBridgeEntry(cell, partner) && flow != back,
            LogisticsKind.Bridge => cell.Direction == flow,
            // Buildings insert without a direction, which a junction refuses: only logistics feed through it.
            LogisticsKind.Junction => junctionsLeft > 0
                && FeedsToward(source.Step(DirectionMath.Opposite(flow)), flow, null, junctionsLeft - 1),
            _ => false
        };
    }

    public bool IsSpecial(GridPosition position) =>
        cells.TryGetValue(position, out var cell) && cell.Kind is not LogisticsKind.Belt;

    /// <summary>Row-major order: Y first, then X.</summary>
    public sealed class CanonicalOrder : IComparer<GridPosition>
    {
        public static readonly CanonicalOrder Instance = new();

        private CanonicalOrder()
        {
        }

        public int Compare(GridPosition a, GridPosition b)
        {
            var byRow = a.Y.CompareTo(b.Y);
            return byRow != 0 ? byRow : a.X.CompareTo(b.X);
        }
    }
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
    public Direction Direction { get; internal set; } // only BeltGrid writes it, bumping Version
    public ConveyorDefinition Definition => inner.Definition;
    public LogisticsKind Kind => inner.Kind;
    public IReadOnlyList<TransportedItem> Items => inner.Items;
    public GridPosition OutputPosition => Position.Step(Direction);
    public int SplitterToggle => splitterToggle;
    public GridPosition? BridgePartner { get; private set; }
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

/// <summary>How a plain belt cell is drawn (<see cref="BeltGrid.GetShape"/>).</summary>
public enum BeltShape
{
    Straight,
    Corner,
    T,

    /// <summary>Fed from behind and from the left of its flow (belt or building): the dot block open on the left.</summary>
    JoinLeft,

    /// <summary>Fed from behind and from the right of its flow (belt or building): the dot block open on the right.</summary>
    JoinRight,

    /// <summary>Fed from behind and by belts on both sides: a T open behind too.</summary>
    JoinBoth,

    /// <summary>Fed from behind and on both sides, a building on at least one: the dot block open on both.</summary>
    JoinBuildingBoth
}
