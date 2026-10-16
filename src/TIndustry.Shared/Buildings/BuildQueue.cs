namespace TIndustry.Shared;

/// <summary>
/// A placement made while the game is paused: drawn as a see-through ghost and built after resume,
/// as soon as the wallet covers it. <paramref name="Tool"/> is the client's tool id, passed back to build it.
/// </summary>
public sealed record PlannedBuild(
    int Tool,
    string StructureId,
    GridPosition Origin,
    int Size,
    Direction Direction,
    int CostMultiplier = 1,
    string? FilterItemId = null)
{
    public bool Covers(GridPosition tile) =>
        tile.X >= Origin.X && tile.X < Origin.X + Size && tile.Y >= Origin.Y && tile.Y < Origin.Y + Size;

    public IEnumerable<GridPosition> Tiles()
    {
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                yield return new GridPosition(Origin.X + x, Origin.Y + y);
            }
        }
    }
}

/// <summary>Outcome of one <see cref="BuildQueue.Process"/> pass.</summary>
public readonly record struct BuildQueueResult(int Built, int Waiting, int Dropped);

/// <summary>
/// Placements planned during pause (Mindustry-style ghosts). Built in order after resume: a plan the wallet
/// cannot cover yet waits, one whose cells got taken (or that the factory rejects) is dropped.
/// </summary>
public sealed class BuildQueue
{
    private readonly List<PlannedBuild> plans = [];

    public IReadOnlyList<PlannedBuild> Plans => plans;

    public int Count => plans.Count;

    /// <summary>Adds the plan when its cells are free in the factory and not taken by another plan.</summary>
    public bool TryAdd(PlannedBuild plan, Factory factory)
    {
        if (!factory.CanOccupyFootprint(plan.Origin, plan.Size)
            || plan.Tiles().Any(tile => plans.Any(p => p.Covers(tile))))
        {
            return false;
        }

        plans.Add(plan);
        return true;
    }

    public bool IsPlanned(GridPosition tile) => plans.Any(p => p.Covers(tile));

    /// <summary>Removes the plan covering <paramref name="tile"/> (right-click on a ghost).</summary>
    public bool RemoveAt(GridPosition tile) => plans.RemoveAll(p => p.Covers(tile)) > 0;

    public void Clear() => plans.Clear();

    /// <summary>
    /// One pass in plan order: affordable plans are built through <paramref name="place"/>, the rest wait.
    /// </summary>
    public BuildQueueResult Process(Factory factory, Func<PlannedBuild, bool> place)
    {
        int built = 0, waiting = 0, dropped = 0;
        foreach (var plan in plans.ToList())
        {
            if (!factory.CanOccupyFootprint(plan.Origin, plan.Size))
            {
                plans.Remove(plan);
                dropped++;
            }
            else if (!factory.CanAffordStructure(plan.StructureId, plan.CostMultiplier))
            {
                waiting++;
            }
            else
            {
                plans.Remove(plan);
                if (place(plan))
                {
                    built++;
                }
                else
                {
                    dropped++;
                }
            }
        }

        return new BuildQueueResult(built, waiting, dropped);
    }
}
