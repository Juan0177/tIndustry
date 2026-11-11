namespace TIndustry.Shared;

/// <summary>
/// Advances the sites a drone is working on (Mindustry's <c>ConstructBuild.construct/deconstruct</c>).
/// Per site, in id order: the drone whose current site it is, when within <c>buildRange</c>, adds its
/// build speed (<see cref="BuilderSystem"/> gives a drone only the first plan of its queue within range). A site stalled
/// for a second goes to the back of the queue when another plan of the queue is within range. A
/// finished site pops the drone's first plan and raises
/// <see cref="ConstructFinished"/> or <see cref="DeconstructFinished"/>.
/// </summary>
public sealed class ConstructSystem : ISimulationSystem
{
    public string Name => "Construct";

    public void Run(GameState state, SystemContext ctx)
    {
        if (state.Sites.Count == 0)
        {
            return;
        }

        var unit = state.EffectiveUnit;
        Factory? factory = null;
        // Advance removes finished sites: iterate a copy, still in id order.
        foreach (var site in state.Sites.ToArray())
        {
            var builder = WorkerOf(state, site);
            if (builder is null)
            {
                continue;
            }

            factory ??= new Factory(state);
            var (x, y) = Factory.SiteCentre(site);
            if (BuilderSystem.Distance(builder, x, y) > unit.BuildRange)
            {
                continue;
            }

            var deconstructing = site.Deconstructing;
            var step = Construction.Advance(state, site, unit.BuildSpeed);
            if (step == ConstructStep.Stalled)
            {
                SendToBackWhenStuck(factory, builder, site);
                continue;
            }

            site.StalledTicks = 0;
            if (step != ConstructStep.Finished)
            {
                continue;
            }

            builder.CurrentSiteId = null;
            if (builder.Plans.Count > 0)
            {
                builder.PlanQueue.RemoveAt(0);
            }

            if (deconstructing)
            {
                state.Events.Add(new DeconstructFinished(builder.OwnerPlayerId, site.Id, site.Spec));
            }
            else
            {
                factory.TryGetSpecAt(site.Spec.Origin, out _, out var entityId);
                state.Events.Add(new ConstructFinished(builder.OwnerPlayerId, site.Id, entityId, site.Spec));
            }
        }
    }

    /// <summary>
    /// Counts the stalled ticks of <paramref name="site"/>; after <see cref="Construction.StuckTicks"/> in a row,
    /// when another plan of the queue is within range, its plan goes to the back of the queue and the drone moves on,
    /// leaving the site where it stopped (Mindustry's <c>plan.stuck</c>). A stalled plan with no other plan in range
    /// just waits: sending it back would only bring it to the front again on the next tick.
    /// </summary>
    private static void SendToBackWhenStuck(Factory factory, BuilderUnit builder, ConstructSite site)
    {
        site.StalledTicks = Math.Min(site.StalledTicks + 1, Construction.StuckTicks);
        if (site.StalledTicks < Construction.StuckTicks || !AnotherPlanInRange(factory, builder))
        {
            return;
        }

        var plan = builder.PlanQueue[0];
        builder.PlanQueue.RemoveAt(0);
        builder.PlanQueue.Add(plan);
        builder.CurrentSiteId = null;
        site.StalledTicks = 0;
    }

    private static bool AnotherPlanInRange(Factory factory, BuilderUnit builder)
    {
        for (var i = 1; i < builder.PlanQueue.Count; i++)
        {
            if (factory.IsPlanInRange(builder, builder.PlanQueue[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static BuilderUnit? WorkerOf(GameState state, ConstructSite site)
    {
        foreach (var builder in state.Builders)
        {
            if (builder.CurrentSiteId == site.Id)
            {
                return builder;
            }
        }

        return null;
    }
}
