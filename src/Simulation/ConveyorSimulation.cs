namespace TIndustry.Logistics;

public enum Direction
{
    North,
    East,
    South,
    West
}

public static class DirectionMath
{
    public static Direction Opposite(Direction direction) => direction switch
    {
        Direction.North => Direction.South,
        Direction.East => Direction.West,
        Direction.South => Direction.North,
        Direction.West => Direction.East,
        _ => direction
    };

    public static Direction Left(Direction direction) => direction switch
    {
        Direction.North => Direction.West,
        Direction.East => Direction.North,
        Direction.South => Direction.East,
        Direction.West => Direction.South,
        _ => direction
    };

    public static Direction Right(Direction direction) => Opposite(Left(direction));

    public static readonly Direction[] All =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West
    ];
}

public readonly record struct GridPosition(int X, int Y)
{
    public GridPosition Step(Direction direction) => direction switch
    {
        Direction.North => this with { Y = Y - 1 },
        Direction.East => this with { X = X + 1 },
        Direction.South => this with { Y = Y + 1 },
        Direction.West => this with { X = X - 1 },
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };

    public GridPosition Step(Direction direction, int distance)
    {
        var result = this;
        for (var i = 0; i < distance; i++)
        {
            result = result.Step(direction);
        }

        return result;
    }
}

public sealed class TransportedItem
{
    public TransportedItem(long id, string itemId, float progress = 0f, Direction? travel = null)
    {
        Id = id;
        ItemId = itemId;
        Progress = progress;
        Travel = travel;
    }

    public long Id { get; }
    public string ItemId { get; }
    public float Progress { get; internal set; }
    /// <summary>Exit direction through a junction (incoming axis). Null on normal belts.</summary>
    public Direction? Travel { get; internal set; }
}

public sealed class EconomyWallet
{
    private readonly Dictionary<string, int> materials;

    public EconomyWallet(int money, IReadOnlyDictionary<string, int>? materials = null)
    {
        Money = money;
        this.materials = materials is null
            ? []
            : new Dictionary<string, int>(materials);
    }

    public int Money { get; private set; }

    public int MaterialCount(string itemId) => materials.GetValueOrDefault(itemId);

    public Dictionary<string, int> MaterialsSnapshot() => new(materials);

    public bool CanAfford(int money, IReadOnlyList<ResourceAmount> cost)
    {
        if (Money < money)
        {
            return false;
        }

        for (var i = 0; i < cost.Count; i++)
        {
            if (MaterialCount(cost[i].ItemId) < cost[i].Amount)
            {
                return false;
            }
        }

        return true;
    }

    public void AddMoney(int amount) => Money += amount;

    public void AddMaterial(string itemId, int amount) =>
        materials[itemId] = MaterialCount(itemId) + amount;

    public bool TryRemoveMaterial(string itemId, int amount)
    {
        if (amount <= 0 || MaterialCount(itemId) < amount)
        {
            return false;
        }

        materials[itemId] = MaterialCount(itemId) - amount;
        return true;
    }

    public bool TrySpend(int money, IReadOnlyList<ResourceAmount> cost)
    {
        if (!CanAfford(money, cost))
        {
            return false;
        }

        Money -= money;
        foreach (var entry in cost)
        {
            materials[entry.ItemId] -= entry.Amount;
        }

        return true;
    }

    public bool MeetsUnlock(UnlockRequirement? unlock) =>
        unlock is null
        || (Money >= unlock.Money
            && unlock.Materials.All(entry => MaterialCount(entry.ItemId) >= entry.Amount));
}

public sealed class ConveyorCell
{
    public const int MinBridgeSpan = 2;
    public const int MaxBridgeSpan = 5;

    private readonly List<TransportedItem> items;
    private int splitterToggle;
    private string? filterItemId;

    public ConveyorCell(
        GridPosition position,
        Direction direction,
        ConveyorDefinition definition,
        GridPosition? bridgePartner = null,
        int splitterToggle = 0,
        string? filterItemId = null)
    {
        Position = position;
        Direction = direction;
        Definition = definition;
        BridgePartner = bridgePartner;
        this.splitterToggle = splitterToggle;
        this.filterItemId = NormalizeFilter(definition.Kind, filterItemId);
        items = new List<TransportedItem>(definition.Capacity);
        RoutedExit = direction;
    }

    public GridPosition Position { get; }
    public Direction Direction { get; private set; }
    public ConveyorDefinition Definition { get; private set; }
    public LogisticsKind Kind => Definition.Kind;
    public GridPosition? BridgePartner { get; set; }
    public Direction RoutedExit { get; private set; }
    public int SplitterToggle => splitterToggle;
    /// <summary>Matched items exit forward; others left/right. Default <c>iron-ore</c> for sorters.</summary>
    public string? FilterItemId => filterItemId;
    public IReadOnlyList<TransportedItem> Items => items;
    public GridPosition OutputPosition => Position.Step(RoutedExit);

    private static string? NormalizeFilter(LogisticsKind kind, string? filterItemId)
    {
        if (kind != LogisticsKind.Sorter)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(filterItemId) ? "iron-ore" : filterItemId;
    }

    public bool MatchesFilter(string itemId) =>
        Kind == LogisticsKind.Sorter
        && !string.IsNullOrEmpty(filterItemId)
        && string.Equals(filterItemId, itemId, StringComparison.Ordinal);

    public void SetFilterItem(string? itemId)
    {
        if (Kind != LogisticsKind.Sorter)
        {
            return;
        }

        filterItemId = string.IsNullOrWhiteSpace(itemId) ? "iron-ore" : itemId;
    }

    /// <summary>Cycle filter through known inventory item ids (Italian dock list).</summary>
    public void CycleFilterItem(IReadOnlyList<string> itemIds)
    {
        if (Kind != LogisticsKind.Sorter || itemIds.Count == 0)
        {
            return;
        }

        var current = filterItemId ?? itemIds[0];
        var index = 0;
        for (var i = 0; i < itemIds.Count; i++)
        {
            if (string.Equals(itemIds[i], current, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        filterItemId = itemIds[(index + 1) % itemIds.Count];
    }

    public bool TryInsert(TransportedItem item, Direction? fromDirection = null)
    {
        if (items.Count >= Definition.Capacity)
        {
            return false;
        }

        if (Kind == LogisticsKind.Junction)
        {
            // Cross traffic: one item per axis (EW / NS). Perpendicular streams never block each other.
            if (fromDirection is not { } incoming)
            {
                return false;
            }

            if (items.Any(existing => SameAxis(existing.Travel ?? Direction, incoming)))
            {
                return false;
            }

            item.Progress = 0f;
            item.Travel = incoming;
            RoutedExit = incoming;
            items.Add(item);
            return true;
        }

        var rearItem = items.Count == 0 ? null : items[^1];
        if (rearItem is not null && rearItem.Progress < Definition.ItemSpacing)
        {
            return false;
        }

        RoutedExit = ResolveExit(fromDirection);
        item.Progress = 0f;
        item.Travel = null;
        items.Add(item);
        return true;
    }

    private static bool SameAxis(Direction a, Direction b) =>
        (a is Direction.North or Direction.South) == (b is Direction.North or Direction.South);

    private Direction ResolveExit(Direction? fromDirection)
    {
        return Kind switch
        {
            LogisticsKind.Junction when fromDirection is { } incoming =>
                incoming,
            // Splitters/sorters travel like belts along facing; exits chosen at handoff.
            LogisticsKind.Splitter => Direction,
            LogisticsKind.Sorter => Direction,
            LogisticsKind.Bridge => Direction,
            _ => Direction
        };
    }

    /// <summary>Left/right exits relative to facing (T-fork). Order rotates with <see cref="SplitterToggle"/>.</summary>
    public Direction PreferredSplitterExit =>
        splitterToggle % 2 == 0
            ? DirectionMath.Left(Direction)
            : DirectionMath.Right(Direction);

    public Direction AlternateSplitterExit =>
        PreferredSplitterExit == DirectionMath.Left(Direction)
            ? DirectionMath.Right(Direction)
            : DirectionMath.Left(Direction);

    internal void AdvanceSplitterToggle() => splitterToggle++;

    internal void Advance(float deltaSeconds)
    {
        var movement = Definition.RateItemsPerSecond * deltaSeconds;
        if (Kind == LogisticsKind.Junction)
        {
            // Independent lanes: each item only yields to same-axis traffic ahead.
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var travel = item.Travel ?? Direction;
                var limit = 1f;
                for (var other = 0; other < items.Count; other++)
                {
                    if (other == index)
                    {
                        continue;
                    }

                    var ahead = items[other];
                    if (!SameAxis(ahead.Travel ?? Direction, travel)
                        || ahead.Progress <= item.Progress)
                    {
                        continue;
                    }

                    limit = Math.Min(limit, ahead.Progress - Definition.ItemSpacing);
                }

                item.Progress = Math.Min(item.Progress + movement, Math.Max(item.Progress, limit));
            }

            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var limit = index == 0
                ? 1f
                : items[index - 1].Progress - Definition.ItemSpacing;
            items[index].Progress = Math.Min(items[index].Progress + movement, limit);
        }
    }

    internal TransportedItem? PeekOutput() =>
        items.Count > 0 && items[0].Progress >= 1f ? items[0] : null;

    internal void RemoveOutput() => items.RemoveAt(0);

    internal bool TryRemoveItem(TransportedItem item) => items.Remove(item);

    public void Rotate(Direction direction)
    {
        Direction = direction;
        if (Kind is LogisticsKind.Belt or LogisticsKind.Bridge or LogisticsKind.Splitter or LogisticsKind.Sorter)
        {
            RoutedExit = direction;
        }
    }

    internal void RestoreItems(IEnumerable<TransportedItem> restored)
    {
        items.Clear();
        foreach (var item in restored)
        {
            items.Add(item);
        }
    }

    public void Upgrade(ConveyorDefinition definition)
    {
        if (definition.Capacity < items.Count)
        {
            throw new InvalidOperationException("Il nuovo tier non può contenere gli item presenti.");
        }

        if (definition.Kind != Kind)
        {
            throw new InvalidOperationException("Non si può cambiare il tipo di logistica con un upgrade.");
        }

        Definition = definition;
    }
}

public sealed class ConveyorGrid
{
    private readonly Dictionary<GridPosition, ConveyorCell> cells = [];

    public IReadOnlyDictionary<GridPosition, ConveyorCell> Cells => cells;

    public bool TryPlace(
        GridPosition position,
        Direction direction,
        ConveyorDefinition definition,
        EconomyWallet wallet,
        ResearchState research,
        EconomySession? session = null,
        Func<GridPosition, bool>? canOccupy = null)
    {
        if (definition.Kind == LogisticsKind.Bridge)
        {
            return TryPlaceBridge(position, direction, definition, wallet, research, session, canOccupy);
        }

        if (cells.ContainsKey(position)
            || (canOccupy is not null && !canOccupy(position))
            || !research.IsUnlocked(definition.Id)
            || !wallet.TrySpend(definition.MoneyCost, definition.BuildCost))
        {
            return false;
        }

        cells.Add(position, new ConveyorCell(position, direction, definition));
        session?.RecordBuildSpend(definition.MoneyCost);
        return true;
    }

    public bool TryPlaceBridge(
        GridPosition entry,
        Direction direction,
        ConveyorDefinition definition,
        EconomyWallet wallet,
        ResearchState research,
        EconomySession? session = null,
        Func<GridPosition, bool>? canOccupy = null)
    {
        if (!research.IsUnlocked(definition.Id)
            || cells.ContainsKey(entry)
            || (canOccupy is not null && !canOccupy(entry)))
        {
            return false;
        }

        GridPosition? exit = null;
        for (var span = ConveyorCell.MinBridgeSpan; span <= ConveyorCell.MaxBridgeSpan; span++)
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

        // Entry + exit cost the same definition once each.
        var totalMoney = definition.MoneyCost * 2;
        var buildCost = definition.BuildCost;
        ResourceAmount[]? rented = null;
        IReadOnlyList<ResourceAmount> totalMaterials;
        if (buildCost.Count == 0)
        {
            totalMaterials = Array.Empty<ResourceAmount>();
        }
        else if (buildCost.Count == 1)
        {
            totalMaterials = [new ResourceAmount(buildCost[0].ItemId, buildCost[0].Amount * 2)];
        }
        else
        {
            rented = new ResourceAmount[buildCost.Count];
            for (var i = 0; i < buildCost.Count; i++)
            {
                rented[i] = new ResourceAmount(buildCost[i].ItemId, buildCost[i].Amount * 2);
            }

            totalMaterials = rented;
        }

        if (!wallet.TrySpend(totalMoney, totalMaterials))
        {
            return false;
        }

        var entryCell = new ConveyorCell(entry, direction, definition, exit);
        var exitCell = new ConveyorCell(exit.Value, direction, definition, entry);
        cells.Add(entry, entryCell);
        cells.Add(exit.Value, exitCell);
        session?.RecordBuildSpend(totalMoney);
        return true;
    }

    public bool TryRestore(
        GridPosition position,
        Direction direction,
        ConveyorDefinition definition,
        IEnumerable<TransportedItem>? items = null,
        GridPosition? bridgePartner = null,
        int splitterToggle = 0,
        string? filterItemId = null)
    {
        if (cells.ContainsKey(position))
        {
            return false;
        }

        var cell = new ConveyorCell(position, direction, definition, bridgePartner, splitterToggle, filterItemId);
        if (items is not null)
        {
            cell.RestoreItems(items);
        }

        cells.Add(position, cell);
        return true;
    }

    public bool TryRemove(GridPosition position, EconomyWallet wallet, EconomySession? session = null)
    {
        if (!cells.TryGetValue(position, out var cell))
        {
            return false;
        }

        if (cell.Kind == LogisticsKind.Bridge && cell.BridgePartner is { } partner)
        {
            cells.Remove(position);
            RefundCell(cell, wallet, session);
            if (cells.Remove(partner, out var partnerCell))
            {
                RefundCell(partnerCell, wallet, session);
            }

            return true;
        }

        cells.Remove(position);
        RefundCell(cell, wallet, session);
        return true;
    }

    private static void RefundCell(ConveyorCell cell, EconomyWallet wallet, EconomySession? session)
    {
        wallet.AddMoney(cell.Definition.MoneyCost);
        foreach (var entry in cell.Definition.BuildCost)
        {
            wallet.AddMaterial(entry.ItemId, entry.Amount);
        }

        session?.RecordRefund(cell.Definition.MoneyCost);
    }

    public bool TryUpgrade(
        GridPosition position,
        ConveyorDefinition definition,
        EconomyWallet wallet,
        ResearchState research,
        EconomySession? session = null)
    {
        if (!cells.TryGetValue(position, out var cell)
            || cell.Kind != LogisticsKind.Belt
            || definition.Kind != LogisticsKind.Belt
            || cell.Definition.Tier >= definition.Tier
            || !research.IsUnlocked(definition.Id)
            || !wallet.TrySpend(definition.MoneyCost, definition.BuildCost))
        {
            return false;
        }

        wallet.AddMoney(cell.Definition.MoneyCost);
        foreach (var entry in cell.Definition.BuildCost)
        {
            wallet.AddMaterial(entry.ItemId, entry.Amount);
        }

        var netSpend = Math.Max(0, definition.MoneyCost - cell.Definition.MoneyCost);
        session?.RecordBuildSpend(netSpend);
        cell.Upgrade(definition);
        return true;
    }

    public bool TryOrientToward(GridPosition from, GridPosition to)
    {
        if (!cells.TryGetValue(from, out var cell)
            || cell.Kind is LogisticsKind.Junction or LogisticsKind.Splitter or LogisticsKind.Bridge
                or LogisticsKind.Sorter
            || !TryDirectionBetween(from, to, out var direction))
        {
            return false;
        }

        cell.Rotate(direction);
        return true;
    }

    public static bool TryDirectionBetween(GridPosition from, GridPosition to, out Direction direction)
    {
        var deltaX = to.X - from.X;
        var deltaY = to.Y - from.Y;
        direction = (deltaX, deltaY) switch
        {
            (0, -1) => Direction.North,
            (1, 0) => Direction.East,
            (0, 1) => Direction.South,
            (-1, 0) => Direction.West,
            _ => default
        };
        return Math.Abs(deltaX) + Math.Abs(deltaY) == 1;
    }

    public void Update(float fixedDeltaSeconds)
    {
        foreach (var cell in cells.Values)
        {
            cell.Advance(fixedDeltaSeconds);
        }

        foreach (var cell in cells.Values)
        {
            TryHandoff(cell);
        }
    }

    private void TryHandoff(ConveyorCell cell)
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

                var exit = ready.Travel ?? cell.RoutedExit;
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
            // Entry faces partner along Direction; exit faces away — O(1) vs span loop.
            if (IsBridgeEntry(cell, partner)
                && cells.TryGetValue(partner, out var exitCell)
                && exitCell.TryInsert(item, cell.Direction))
            {
                cell.RemoveOutput();
                return;
            }
        }

        if (cell.Kind == LogisticsKind.Splitter)
        {
            // Fair T-fork: prefer alternating left/right; fall back to the other side if blocked.
            if (TryInsertNeighbor(cell, cell.PreferredSplitterExit, item)
                || TryInsertNeighbor(cell, cell.AlternateSplitterExit, item))
            {
                cell.AdvanceSplitterToggle();
            }

            return;
        }

        if (cell.Kind == LogisticsKind.Sorter)
        {
            // Mindustry-style: filter match → facing; others → left then right.
            if (cell.MatchesFilter(item.ItemId))
            {
                TryInsertNeighbor(cell, cell.Direction, item);
            }
            else if (TryInsertNeighbor(cell, DirectionMath.Left(cell.Direction), item)
                || TryInsertNeighbor(cell, DirectionMath.Right(cell.Direction), item))
            {
                // overflow routed
            }

            return;
        }

        TryInsertNeighbor(cell, cell.RoutedExit, item);
    }

    private static bool IsBridgeEntry(ConveyorCell cell, GridPosition partner)
    {
        var dx = partner.X - cell.Position.X;
        var dy = partner.Y - cell.Position.Y;
        var span = Math.Abs(dx) + Math.Abs(dy);
        if (span < ConveyorCell.MinBridgeSpan || span > ConveyorCell.MaxBridgeSpan)
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

    private bool TryInsertNeighbor(ConveyorCell cell, Direction exit, TransportedItem item)
    {
        var target = cell.Position.Step(exit);
        if (cells.TryGetValue(target, out var next) && next.TryInsert(item, exit))
        {
            cell.RemoveOutput();
            return true;
        }

        return false;
    }
}
