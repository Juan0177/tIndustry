namespace TIndustry.Shared;

/// <summary>
/// Guards the automatic write to the Continua slot: only a session the player actually played
/// (new game, campaign level or successful load) may overwrite it. The startup sandbox shown behind
/// Home is not a game, so quitting from Home must never replace an existing (or incompatible) Continua.
/// </summary>
public sealed class ContinueAutoSave
{
    public bool HasPlayed { get; private set; }

    public void MarkPlayed() => HasPlayed = true;

    /// <summary>Writes <paramref name="slice"/> to Continua when allowed; returns true if it was written.</summary>
    public bool TrySave(Factory? slice)
    {
        if (!HasPlayed || slice is null)
        {
            return false;
        }

        FactorySaveStore.Save(FactorySaveStore.ContinueSlotId, slice.Capture());
        return true;
    }
}
