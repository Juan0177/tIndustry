namespace TIndustry.Shared;

/// <summary>
/// Per-game rules (Mindustry's <c>Rules</c>).
/// <paramref name="RefundPercent"/> (0–100) scales what deconstructing a finished building gives back;
/// <paramref name="InstantBuild"/> completes plans in the tick they are applied (tests, demo, dev seeds);
/// <paramref name="Night"/> darkens the map in the client (looks only: no system reads it, it is saved with the game).
/// </summary>
public sealed record GameRules(int RefundPercent = 100, bool InstantBuild = false, bool Night = false);
