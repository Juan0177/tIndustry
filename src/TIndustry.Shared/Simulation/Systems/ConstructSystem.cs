namespace TIndustry.Shared;

/// <summary>
/// Advances the sites a drone is working on (Mindustry's <c>ConstructBuild.construct/deconstruct</c>).
/// Per site, in id order: the drone whose current site it is, when within <c>buildRange</c>, adds its
/// build speed. A finished site pops the drone's first plan and raises
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

        var unit = state.Content.Drone;
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
            if (Construction.Advance(state, site, unit.BuildSpeed) != ConstructStep.Finished)
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
                state.Events.Add(new DeconstructFinished(builder.OwnerPlayerId, site.Id));
            }
            else
            {
                factory.TryGetSpecAt(site.Spec.Origin, out _, out var entityId);
                state.Events.Add(new ConstructFinished(builder.OwnerPlayerId, site.Id, entityId));
            }
        }
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
