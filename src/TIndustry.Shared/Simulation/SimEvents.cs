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
/// (0 for belt pieces, which have no entity id).
/// </summary>
public sealed record ConstructFinished(int PlayerId, long SiteId, long EntityId) : SimEvent(PlayerId);

/// <summary>Site <paramref name="SiteId"/> was fully taken apart (deconstruction or cancelled build).</summary>
public sealed record DeconstructFinished(int PlayerId, long SiteId) : SimEvent(PlayerId);
