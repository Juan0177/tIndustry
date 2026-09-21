namespace TIndustry.Shared;

/// <summary>
/// Single-cell belt lane using the same Advance math as Logistics ConveyorCell
/// (rate × dt, spacing limits). Spike-sized: one strip of cells, no junction/splitter.
/// </summary>
public sealed class BeltLane
{
    private readonly List<BeltCell> cells = [];
    private long nextItemId = 1;

    public BeltLane(IReadOnlyList<GridPosition> path, Direction direction, ConveyorDefinition definition)
    {
        if (path.Count == 0)
        {
            throw new ArgumentException("Path vuoto.", nameof(path));
        }

        Direction = direction;
        Definition = definition;
        foreach (var pos in path)
        {
            cells.Add(new BeltCell(pos, definition));
        }
    }

    public Direction Direction { get; }
    public ConveyorDefinition Definition { get; }
    public IReadOnlyList<BeltCell> Cells => cells;

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
