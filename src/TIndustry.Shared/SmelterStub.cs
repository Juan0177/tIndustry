namespace TIndustry.Shared;

/// <summary>
/// Minimal craft machine: belt intake → timed recipe → emit onto outward belts.
/// Used for forno (Phase C) and assembler (Phase D). Phase F: optional powered speed.
/// </summary>
public sealed class SmelterStub
{
    public const int Size = 2;
    public const int FootprintArea = Size * Size;
    public const int OutputTileCount = Size * 4;
    public const string SmelterBuildingId = "smelter";
    public const string AssemblerBuildingId = "assembler";

    /// <summary>Legacy alias — prefer <see cref="SmelterBuildingId"/>.</summary>
    public const string BuildingId = SmelterBuildingId;

    private readonly Dictionary<string, int> inputBuffer = new(StringComparer.Ordinal);
    private readonly Queue<string> outputQueue = new();

    public SmelterStub(
        GridPosition position,
        Direction direction,
        RecipeDefinition recipe,
        string buildingId = SmelterBuildingId)
    {
        Position = position;
        Direction = direction;
        Recipe = recipe;
        DefinitionId = string.IsNullOrWhiteSpace(buildingId) ? SmelterBuildingId : buildingId;
    }

    public GridPosition Position { get; private set; }
    public Direction Direction { get; private set; }
    public RecipeDefinition Recipe { get; }
    public string DefinitionId { get; }
    public bool IsAssembler => DefinitionId == AssemblerBuildingId;
    public float Progress { get; private set; }
    public bool IsCrafting { get; private set; }
    public bool IsPowered { get; private set; }
    public long ItemsCrafted { get; private set; }
    public IReadOnlyDictionary<string, int> InputBuffer => inputBuffer;
    public IReadOnlyCollection<string> OutputQueue => outputQueue;
    public int EjectIndex { get; private set; }

    public void Relocate(GridPosition position, Direction direction)
    {
        Position = position;
        Direction = direction;
    }

    public IEnumerable<GridPosition> OccupiedTiles()
    {
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                yield return new GridPosition(Position.X + x, Position.Y + y);
            }
        }
    }

    public bool Occupies(GridPosition tile)
    {
        foreach (var t in OccupiedTiles())
        {
            if (t.Equals(tile))
            {
                return true;
            }
        }

        return false;
    }

    public GridPosition OutputTileAt(int index)
    {
        var i = ((index % OutputTileCount) + OutputTileCount) % OutputTileCount;
        var edge = DirectionMath.All[i / Size];
        var offset = i % Size;
        return edge switch
        {
            Direction.North => new GridPosition(Position.X + offset, Position.Y - 1),
            Direction.East => new GridPosition(Position.X + Size, Position.Y + offset),
            Direction.South => new GridPosition(Position.X + offset, Position.Y + Size),
            Direction.West => new GridPosition(Position.X - 1, Position.Y + offset),
            _ => Position
        };
    }

    public bool TryAccept(string itemId)
    {
        var need = Recipe.Inputs.FirstOrDefault(i => i.ItemId == itemId);
        if (need is null)
        {
            return false;
        }

        var have = inputBuffer.GetValueOrDefault(itemId);
        if (have >= need.Amount * 4)
        {
            return false;
        }

        inputBuffer[itemId] = have + 1;
        return true;
    }

    /// <summary>Pull ready items from belts that point into the footprint.</summary>
    public int AcceptFromBelts(BeltGrid belts)
    {
        var accepted = 0;
        foreach (var cell in belts.Cells.Values)
        {
            if (!Occupies(cell.OutputPosition))
            {
                continue;
            }

            while (cell.PeekOutput() is { } item)
            {
                if (!TryAccept(item.ItemId))
                {
                    break;
                }

                cell.RemoveOutput();
                accepted++;
            }
        }

        return accepted;
    }

    public void Tick(float deltaSeconds, BeltGrid belts, ref long nextItemId, bool powered = false)
    {
        IsPowered = powered;
        AcceptFromBelts(belts);
        var speed = powered ? GeneratorStub.PoweredCraftSpeedMultiplier : 1f;
        AdvanceCraft(deltaSeconds * speed);
        EmitToBelts(belts, ref nextItemId);
    }

    private void AdvanceCraft(float deltaSeconds)
    {
        if (!IsCrafting)
        {
            if (!CanStart())
            {
                return;
            }

            ConsumeInputs();
            IsCrafting = true;
            Progress = 0f;
        }

        Progress += deltaSeconds / Math.Max(0.05f, Recipe.DurationSeconds);
        if (Progress < 1f)
        {
            return;
        }

        foreach (var output in Recipe.Outputs)
        {
            for (var n = 0; n < output.Amount; n++)
            {
                outputQueue.Enqueue(output.ItemId);
            }

            ItemsCrafted += output.Amount;
        }

        IsCrafting = false;
        Progress = 0f;
    }

    private void EmitToBelts(BeltGrid belts, ref long nextItemId)
    {
        while (outputQueue.Count > 0)
        {
            var itemId = outputQueue.Peek();
            if (!TryInsertOutward(belts, itemId, ref nextItemId))
            {
                return;
            }

            outputQueue.Dequeue();
        }
    }

    private bool TryInsertOutward(BeltGrid belts, string itemId, ref long nextItemId)
    {
        var start = EjectIndex;
        for (var step = 0; step < OutputTileCount; step++)
        {
            var slot = (start + step) % OutputTileCount;
            var outputPosition = OutputTileAt(slot);
            if (!belts.TryGet(outputPosition, out var cell))
            {
                continue;
            }

            // Outward: next cell after belt must not re-enter footprint.
            var next = outputPosition.Step(cell.Direction);
            if (Occupies(next))
            {
                continue;
            }

            if (!belts.TryInsert(outputPosition, new TransportedItem(nextItemId, itemId)))
            {
                continue;
            }

            nextItemId++;
            EjectIndex = (slot + 1) % OutputTileCount;
            return true;
        }

        return false;
    }

    private bool CanStart()
    {
        foreach (var input in Recipe.Inputs)
        {
            if (inputBuffer.GetValueOrDefault(input.ItemId) < input.Amount)
            {
                return false;
            }
        }

        return true;
    }

    private void ConsumeInputs()
    {
        foreach (var input in Recipe.Inputs)
        {
            inputBuffer[input.ItemId] = inputBuffer.GetValueOrDefault(input.ItemId) - input.Amount;
        }
    }
}
