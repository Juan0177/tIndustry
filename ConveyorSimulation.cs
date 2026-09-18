namespace TIndustry.Logistics;

public enum Direction
{
    North,
    East,
    South,
    West
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
}

public sealed class ConveyorCell
{
    private readonly List<TransportedItem> items;

    public ConveyorCell(
        GridPosition position,
        Direction direction,
        ConveyorDefinition definition)
    {
        Position = position;
        Direction = direction;
        Definition = definition;
        items = new List<TransportedItem>(definition.Capacity);
    }

    public GridPosition Position { get; }
    public Direction Direction { get; private set; }
    public ConveyorDefinition Definition { get; private set; }
    public IReadOnlyList<TransportedItem> Items => items;
    public GridPosition OutputPosition => Position.Step(Direction);

    public bool TryInsert(TransportedItem item)
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

        item.Progress = 0f;
        items.Add(item);
        return true;
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

    public void Rotate(Direction direction) => Direction = direction;

    public void Upgrade(ConveyorDefinition definition)
    {
        if (definition.Capacity < items.Count)
        {
            throw new InvalidOperationException("Il nuovo tier non può contenere gli item presenti.");
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
        EconomyWallet wallet)
    {
        if (cells.ContainsKey(position)
            || !wallet.TrySpend(definition.MoneyCost, definition.BuildCost))
        {
            return false;
        }

        cells.Add(position, new ConveyorCell(position, direction, definition));
        return true;
    }

    public bool TryRemove(GridPosition position, EconomyWallet wallet)
    {
        if (!cells.Remove(position, out var cell))
        {
            return false;
        }

        wallet.AddMoney(cell.Definition.MoneyCost);
        foreach (var entry in cell.Definition.BuildCost)
        {
            wallet.AddMaterial(entry.ItemId, entry.Amount);
        }

        return true;
    }

    public bool TryOrientToward(GridPosition from, GridPosition to)
    {
        if (!cells.TryGetValue(from, out var cell)
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
            var item = cell.PeekOutput();
            if (item is not null
                && cells.TryGetValue(cell.OutputPosition, out var next)
                && next.TryInsert(item))
            {
                cell.RemoveOutput();
            }
        }
    }
}