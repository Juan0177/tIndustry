namespace TIndustry.Shared;

/// <summary>
/// Craft machine: belt intake → timed recipe → emit onto outward belts.
/// Used for forno (Phase C) and assembler (Phase D). Phase F: optional powered speed.
/// Multi-recipe: auto-picks the first available recipe whose inputs are buffered (Raylib parity).
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

    public const string FuelItemId = "coal";
    public const int FuelBufferCapacity = 8;
    /// <summary>Seconds of coal-only craft time from one fuel unit (Raylib parity).</summary>
    public const float SecondsPerFuel = 6f;

    private readonly Dictionary<string, int> inputBuffer = new(StringComparer.Ordinal);
    private readonly Queue<string> outputQueue = new();
    private RecipeDefinition activeRecipe;

    public SmelterStub(
        GridPosition position,
        Direction direction,
        RecipeDefinition recipe,
        string buildingId = SmelterBuildingId,
        IReadOnlyList<RecipeDefinition>? availableRecipes = null)
    {
        Position = position;
        Direction = direction;
        AvailableRecipes = availableRecipes is { Count: > 0 }
            ? availableRecipes
            : [recipe];
        activeRecipe = AvailableRecipes.FirstOrDefault(r => r.Id == recipe.Id) ?? AvailableRecipes[0];
        DefinitionId = string.IsNullOrWhiteSpace(buildingId) ? SmelterBuildingId : buildingId;
    }

    public GridPosition Position { get; private set; }
    public Direction Direction { get; private set; }
    public IReadOnlyList<RecipeDefinition> AvailableRecipes { get; }
    public RecipeDefinition Recipe => activeRecipe;
    public string DefinitionId { get; }
    public bool IsAssembler => DefinitionId == AssemblerBuildingId;
    /// <summary>Forno only: coal-or-power gate. Assembler ignores fuel.</summary>
    public bool UsesCoalOrPower => !IsAssembler;
    public float Progress { get; private set; }
    public bool IsCrafting { get; private set; }
    public bool IsPowered { get; private set; }
    public int FuelBuffer { get; private set; }
    public float BurnRemaining { get; private set; }
    public bool IsBurningFuel => BurnRemaining > 0f;
    public long ItemsCrafted { get; private set; }
    public IReadOnlyDictionary<string, int> InputBuffer => inputBuffer;
    public IReadOnlyCollection<string> OutputQueue => outputQueue;
    public int EjectIndex { get; private set; }

    public void Relocate(GridPosition position, Direction direction)
    {
        Position = position;
        Direction = direction;
    }

    public void RestoreCraftState(
        float progress,
        bool isCrafting,
        IReadOnlyDictionary<string, int>? inputs,
        IEnumerable<string>? outputs,
        int ejectIndex,
        long itemsCrafted = 0,
        int fuelBuffer = 0,
        float burnRemaining = 0f)
    {
        Progress = Math.Clamp(progress, 0f, 1f);
        IsCrafting = isCrafting;
        inputBuffer.Clear();
        if (inputs is not null)
        {
            foreach (var (id, amount) in inputs)
            {
                if (amount > 0)
                {
                    inputBuffer[id] = amount;
                }
            }
        }

        outputQueue.Clear();
        if (outputs is not null)
        {
            foreach (var id in outputs)
            {
                outputQueue.Enqueue(id);
            }
        }

        EjectIndex = ((ejectIndex % OutputTileCount) + OutputTileCount) % OutputTileCount;
        ItemsCrafted = Math.Max(0, itemsCrafted);
        if (UsesCoalOrPower)
        {
            FuelBuffer = Math.Clamp(fuelBuffer, 0, FuelBufferCapacity);
            BurnRemaining = Math.Max(0f, burnRemaining);
        }
        else
        {
            FuelBuffer = 0;
            BurnRemaining = 0f;
        }
    }

    public bool TryAcceptFuel(string itemId)
    {
        if (!UsesCoalOrPower || itemId != FuelItemId || FuelBuffer >= FuelBufferCapacity)
        {
            return false;
        }

        FuelBuffer++;
        return true;
    }

    /// <summary>Demo/self-test: fill coal buffer so forno can craft without a live gen.</summary>
    public void SeedFuel(int units = FuelBufferCapacity)
    {
        if (!UsesCoalOrPower)
        {
            return;
        }

        FuelBuffer = Math.Clamp(units, 0, FuelBufferCapacity);
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
        var maxNeeded = 0;
        foreach (var recipe in AvailableRecipes)
        {
            var needed = recipe.Inputs.FirstOrDefault(entry => entry.ItemId == itemId);
            if (needed is not null)
            {
                maxNeeded = Math.Max(maxNeeded, needed.Amount);
            }
        }

        if (maxNeeded <= 0)
        {
            return false;
        }

        var have = inputBuffer.GetValueOrDefault(itemId);
        // Cap buffer at 4× the largest single-recipe need (matches prior single-recipe headroom).
        if (have >= maxNeeded * 4)
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
                var took = UsesCoalOrPower && item.ItemId == FuelItemId
                    ? TryAcceptFuel(item.ItemId)
                    : TryAccept(item.ItemId);
                if (!took)
                {
                    break;
                }

                cell.RemoveOutput();
                accepted++;
            }
        }

        return accepted;
    }

    public void Tick(
        float deltaSeconds,
        BeltGrid belts,
        ref long nextItemId,
        bool networkConnected = false,
        Func<float, bool>? trySpendPower = null,
        float powerDrawPerSecond = 0f)
    {
        AcceptFromBelts(belts);

        if (!IsCrafting)
        {
            TryStartCraft();
        }

        IsPowered = false;
        if (IsCrafting)
        {
            var canAdvance = false;
            var speed = 1f;
            // Spend only while crafting (Raylib). Null callback = free power for unit tests.
            var gotPower = false;
            if (networkConnected)
            {
                if (trySpendPower is not null && powerDrawPerSecond > 0f)
                {
                    gotPower = trySpendPower(powerDrawPerSecond * deltaSeconds);
                }
                else
                {
                    gotPower = true;
                }
            }

            IsPowered = gotPower;

            if (UsesCoalOrPower)
            {
                // Forno: corrente (+20%) OR carbone (baseline). Soft brownout → coal.
                if (gotPower)
                {
                    canAdvance = true;
                    speed = GeneratorStub.PoweredCraftSpeedMultiplier;
                }
                else if (TickFuel(deltaSeconds))
                {
                    canAdvance = true;
                    speed = 1f;
                }
            }
            else
            {
                // Assembler: soft — crafts without power; +20% when spend succeeds.
                canAdvance = true;
                speed = gotPower ? GeneratorStub.PoweredCraftSpeedMultiplier : 1f;
            }

            if (canAdvance)
            {
                AdvanceCraft(deltaSeconds * speed);
            }
        }

        EmitToBelts(belts, ref nextItemId);
    }

    /// <summary>Burns coal; true while forno can craft on carbone this tick.</summary>
    public bool TickFuel(float deltaSeconds)
    {
        if (!UsesCoalOrPower)
        {
            return false;
        }

        if (BurnRemaining <= 0f)
        {
            if (FuelBuffer <= 0)
            {
                return false;
            }

            FuelBuffer--;
            BurnRemaining = SecondsPerFuel;
        }

        BurnRemaining = Math.Max(0f, BurnRemaining - deltaSeconds);
        return true;
    }

    private void AdvanceCraft(float deltaSeconds)
    {
        if (!IsCrafting)
        {
            return;
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

    private bool TryStartCraft()
    {
        if (IsCrafting || outputQueue.Count > 0)
        {
            return false;
        }

        RecipeDefinition? chosen = null;
        foreach (var recipe in AvailableRecipes)
        {
            if (recipe.Inputs.All(entry => inputBuffer.GetValueOrDefault(entry.ItemId) >= entry.Amount))
            {
                chosen = recipe;
                break;
            }
        }

        if (chosen is null)
        {
            return false;
        }

        activeRecipe = chosen;
        foreach (var entry in chosen.Inputs)
        {
            var left = inputBuffer.GetValueOrDefault(entry.ItemId) - entry.Amount;
            if (left <= 0)
            {
                inputBuffer.Remove(entry.ItemId);
            }
            else
            {
                inputBuffer[entry.ItemId] = left;
            }
        }

        IsCrafting = true;
        Progress = 0f;
        return true;
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
}
