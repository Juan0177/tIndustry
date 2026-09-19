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
    public TransportedItem(long id, string itemId, float progress = 0f)
    {
        Id = id;
        ItemId = itemId;
        Progress = progress;
    }

    public long Id { get; }
    public string ItemId { get; }
    public float Progress { get; internal set; }
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

    public bool CanAfford(int money, IReadOnlyList<ResourceAmount> cost) =>
        Money >= money && cost.All(entry => MaterialCount(entry.ItemId) >= entry.Amount);

    public void AddMoney(int amount) => Money += amount;

    public void AddMaterial(string itemId, int amount) =>
        materials[itemId] = MaterialCount(itemId) + amount;

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
    public const int MaxBridgeSpan = 4;

    private readonly List<TransportedItem> items;
    private int splitterToggle;

    public ConveyorCell(
        GridPosition position,
        Direction direction,
        ConveyorDefinition definition,
        GridPosition? bridgePartner = null,
        int splitterToggle = 0)
    {
        Position = position;
        Direction = direction;
        Definition = definition;
        BridgePartner = bridgePartner;
        this.splitterToggle = splitterToggle;
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
    public IReadOnlyList<TransportedItem> Items => items;
    public GridPosition OutputPosition => Position.Step(RoutedExit);

    public bool TryInsert(TransportedItem item, Direction? fromDirection = null)
    {
        if (items.Count >= Definition.Capacity)
        {
            return false;
        }

        var rearItem = items.Count == 0 ? null : items[^1];
        if (rearItem is not null && rearItem.Progress < Definition.ItemSpacing)
        {
            return false;
        }

        RoutedExit = ResolveExit(fromDirection);
        item.Progress = 0f;
        items.Add(item);
        return true;
    }

    private Direction ResolveExit(Direction? fromDirection)
    {
        return Kind switch
        {
            LogisticsKind.Junction when fromDirection is { } incoming =>
                incoming,
            LogisticsKind.Splitter => ResolveSplitterExit(),
            LogisticsKind.Bridge => Direction,
            _ => Direction
        };
    }

    private Direction ResolveSplitterExit()
    {
        var exit = splitterToggle % 2 == 0
            ? DirectionMath.Left(Direction)
            : DirectionMath.Right(Direction);
        splitterToggle++;
        return exit;
    }

    internal void Advance(float deltaSeconds)
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

    internal TransportedItem? PeekOutput() =>
        items.Count > 0 && items[0].Progress >= 1f ? items[0] : null;

    internal void RemoveOutput() => items.RemoveAt(0);

    public void Rotate(Direction direction)
    {
        Direction = direction;
        if (Kind is LogisticsKind.Belt or LogisticsKind.Bridge)
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
        var totalMaterials = definition.BuildCost
            .Select(entryCost => new ResourceAmount(entryCost.ItemId, entryCost.Amount * 2))
            .ToArray();
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
        int splitterToggle = 0)
    {
        if (cells.ContainsKey(position))
        {
            return false;
        }

        var cell = new ConveyorCell(position, direction, definition, bridgePartner, splitterToggle);
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
            || cell.Kind is LogisticsKind.Junction or LogisticsKind.Bridge
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
        var item = cell.PeekOutput();
        if (item is null)
        {
            return;
        }

        if (cell.Kind == LogisticsKind.Bridge && cell.BridgePartner is { } partner)
        {
            var isEntry = false;
            for (var span = ConveyorCell.MinBridgeSpan; span <= ConveyorCell.MaxBridgeSpan; span++)
            {
                if (cell.Position.Step(cell.Direction, span) == partner)
                {
                    isEntry = true;
                    break;
                }
            }

            if (isEntry
                && cells.TryGetValue(partner, out var exitCell)
                && exitCell.TryInsert(item, cell.Direction))
            {
                cell.RemoveOutput();
                return;
            }
        }

        if (cell.Kind == LogisticsKind.Splitter)
        {
            // Prefer routed side; if blocked, try the other side once.
            if (TryInsertNeighbor(cell, cell.RoutedExit, item))
            {
                return;
            }

            var alternate = cell.RoutedExit == DirectionMath.Left(cell.Direction)
                ? DirectionMath.Right(cell.Direction)
                : DirectionMath.Left(cell.Direction);
            TryInsertNeighbor(cell, alternate, item);
            return;
        }

        TryInsertNeighbor(cell, cell.RoutedExit, item);
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
