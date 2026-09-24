namespace TIndustry.Shared;

public enum ResearchNodeState
{
    Locked,
    Available,
    Unlocked
}

/// <summary>Thin research unlock state — mirror of Raylib ResearchState for Godot slice.</summary>
public sealed class ResearchState
{
    private readonly HashSet<string> unlocked;

    private ResearchState(HashSet<string> unlocked)
    {
        this.unlocked = unlocked;
    }

    public IReadOnlyCollection<string> UnlockedIds => unlocked;

    public static ResearchState CreateNew(FactoryContent content)
    {
        var unlocked = content.Structures
            .Where(structure => structure.UnlockedByDefault)
            .Select(structure => structure.Id)
            .ToHashSet(StringComparer.Ordinal);
        return new ResearchState(unlocked);
    }

    public static ResearchState FromSaved(IEnumerable<string>? unlockedIds, FactoryContent content)
    {
        var unlocked = (unlockedIds ?? []).ToHashSet(StringComparer.Ordinal);
        foreach (var structure in content.Structures.Where(entry => entry.UnlockedByDefault))
        {
            unlocked.Add(structure.Id);
        }

        return new ResearchState(unlocked);
    }

    public bool IsUnlocked(string structureId) => unlocked.Contains(structureId);

    public void ForceUnlock(string structureId) => unlocked.Add(structureId);

    public bool MeetsPrerequisites(StructureDefinition structure) =>
        structure.Requires.All(IsUnlocked);

    public ResearchNodeState GetNodeState(StructureDefinition structure)
    {
        if (IsUnlocked(structure.Id))
        {
            return ResearchNodeState.Unlocked;
        }

        return MeetsPrerequisites(structure)
            ? ResearchNodeState.Available
            : ResearchNodeState.Locked;
    }

    public bool CanUnlock(StructureDefinition structure, EconomyWallet wallet) =>
        !IsUnlocked(structure.Id)
        && MeetsPrerequisites(structure)
        && (structure.Unlock is null
            || wallet.CanAfford(structure.Unlock.Money, structure.Unlock.Materials));

    public bool TryUnlock(StructureDefinition structure, EconomyWallet wallet)
    {
        if (IsUnlocked(structure.Id) || !MeetsPrerequisites(structure))
        {
            return false;
        }

        if (structure.Unlock is null)
        {
            unlocked.Add(structure.Id);
            return true;
        }

        if (!wallet.TrySpend(structure.Unlock.Money, structure.Unlock.Materials))
        {
            return false;
        }

        unlocked.Add(structure.Id);
        return true;
    }

    /// <summary>Godot slice tools that map to research structures (v1 list).</summary>
    public static readonly string[] GodotSliceStructureIds =
    [
        "conveyor-basic",
        "miner",
        "smelter",
        "assembler",
        "junction",
        "splitter",
        "generator",
        "sorter",
        "conveyor-bridge"
    ];
}
