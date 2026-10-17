namespace TIndustry.Shared;

/// <summary>
/// Per-game rules (Mindustry's <c>Rules</c>).
/// <paramref name="RefundPercent"/> (0–100) scales what deconstructing a finished building gives back;
/// <paramref name="InstantBuild"/> completes plans in the tick they are applied (tests, demo, dev seeds).
/// </summary>
public sealed record GameRules(int RefundPercent = 100, bool InstantBuild = false);
