namespace TIndustry.Shared;

/// <summary>
/// Minimal forno stub: buffers input ore, crafts a recipe on a timer, emits outputs to a callback.
/// No power / coal OR logic yet — Phase A stub for Godot loop experiments.
/// </summary>
public sealed class SmelterStub
{
    public const int Size = 2;
    public const string BuildingId = "smelter";

    private readonly Dictionary<string, int> inputBuffer = new(StringComparer.Ordinal);
    private readonly Queue<string> outputQueue = new();

    public SmelterStub(GridPosition position, RecipeDefinition recipe)
    {
        Position = position;
        Recipe = recipe;
    }

    public GridPosition Position { get; }
    public RecipeDefinition Recipe { get; }
    public float Progress { get; private set; }
    public bool IsCrafting { get; private set; }
    public IReadOnlyDictionary<string, int> InputBuffer => inputBuffer;
    public IReadOnlyCollection<string> OutputQueue => outputQueue;

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

    public void Tick(float deltaSeconds, Action<string>? tryEmit)
    {
        while (outputQueue.Count > 0 && tryEmit is not null)
        {
            var item = outputQueue.Peek();
            tryEmit(item);
            // Caller must confirm emit by draining — for stub we assume success only via TryEmit helper.
            break;
        }

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
        }

        IsCrafting = false;
        Progress = 0f;
    }

    public bool TryDequeueOutput(out string itemId)
    {
        if (outputQueue.Count == 0)
        {
            itemId = "";
            return false;
        }

        itemId = outputQueue.Dequeue();
        return true;
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
