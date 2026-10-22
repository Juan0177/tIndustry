namespace TIndustry.Shared;

/// <summary>
/// Something the simulation did on its own during a tick (not the outcome of a command). Systems queue
/// them in <see cref="GameState.Events"/>; <see cref="SimHost"/> raises them after the tick through
/// <see cref="SimHost.EventRaised"/>. They are not part of the snapshot.
/// </summary>
public abstract record SimEvent(int PlayerId);

/// <summary>
/// The drone reached <paramref name="Plan"/> but could not start it; <paramref name="Reason"/> says why
/// (<see cref="RefusalCode.LockedByResearch"/> or a placement refusal such as <see cref="RefusalCode.Occupied"/>).
/// </summary>
public sealed record PlanDropped(int PlayerId, BuildPlan Plan, RefusalCode Reason) : SimEvent(PlayerId);

/// <summary>
/// Site <paramref name="SiteId"/> was completed; <paramref name="EntityId"/> is the new building's id
/// (0 for belt pieces, which have no entity id); <paramref name="Spec"/> is what the site built (null when unknown).
/// </summary>
public sealed record ConstructFinished(int PlayerId, long SiteId, long EntityId, BuildSpec? Spec = null) : SimEvent(PlayerId);

/// <summary>Site <paramref name="SiteId"/> was fully taken apart (deconstruction or cancelled build); <paramref name="Spec"/> is its block.</summary>
public sealed record DeconstructFinished(int PlayerId, long SiteId, BuildSpec? Spec = null) : SimEvent(PlayerId);

/// <summary>
/// A sale was resolved at the click: <paramref name="Pieces"/> of <paramref name="ItemId"/> at
/// <paramref name="PriceMilli"/> (thousandths of B) paid <paramref name="Income"/>. <paramref name="Crash"/> is true when
/// this sale started a crash; <paramref name="Attempt"/>, <paramref name="Jackpot"/> and
/// <paramref name="ProbabilityBasisPoints"/> describe a jackpot attempt (only possible while the game has a market screen).
/// </summary>
public sealed record SaleResolved(
    int PlayerId, string ItemId, int Pieces, int PriceMilli, int Income,
    bool Crash, bool Attempt, bool Jackpot, int ProbabilityBasisPoints) : SimEvent(PlayerId);
