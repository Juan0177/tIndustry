namespace TIndustry.Shared;

/// <summary>
/// Moves the drones and starts their sites (Mindustry's builder AI and <c>beginPlace</c>). Per drone, in
/// id order: with plans and no site it flies straight to the first plan's centre and, once within
/// <c>buildRange − 1</c>, revalidates the plan and turns it into a site; with no plans it flies back to
/// the rest point beside the Core.
/// </summary>
public sealed class BuilderSystem : ISimulationSystem
{
    public string Name => "Builder";

    public void Run(GameState state, SystemContext ctx)
    {
        if (state.Builders.Count == 0)
        {
            return;
        }

        var factory = new Factory(state);
        var unit = state.Content.Drone;
        var workRange = unit.BuildRange - 1f;
        foreach (var builder in state.Builders)
        {
            if (builder.CurrentSiteId is not null)
            {
                continue;
            }

            if (builder.Plans.Count == 0)
            {
                var (restX, restY) = factory.BuilderRestPoint();
                MoveTowards(builder, restX, restY, unit.Speed * ctx.Dt);
                continue;
            }

            var (x, y) = factory.PlanCentre(builder.Plans[0]);
            if (Distance(builder, x, y) > workRange)
            {
                MoveTowards(builder, x, y, unit.Speed * ctx.Dt);
            }

            if (Distance(builder, x, y) <= workRange)
            {
                BeginWork(state, factory, builder);
            }
        }
    }

    /// <summary>Distance from the drone to (<paramref name="x"/>, <paramref name="y"/>), in tiles.</summary>
    internal static float Distance(BuilderUnit builder, float x, float y)
    {
        var dx = x - builder.X;
        var dy = y - builder.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Moves by <c>min(<paramref name="step"/>, distance)</c> towards the target; the rotation follows the
    /// motion and stays as it was when the drone does not move.
    /// </summary>
    private static void MoveTowards(BuilderUnit builder, float x, float y, float step)
    {
        var dx = x - builder.X;
        var dy = y - builder.Y;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance <= 0f || step <= 0f)
        {
            return;
        }

        builder.Rotation = MathF.Atan2(dy, dx) * (180f / MathF.PI);
        if (step >= distance)
        {
            builder.X = x;
            builder.Y = y;
            return;
        }

        var scale = step / distance;
        builder.X += dx * scale;
        builder.Y += dy * scale;
    }

    /// <summary>Turns the first plan into a site, or drops it when it can no longer start.</summary>
    private static void BeginWork(GameState state, Factory factory, BuilderUnit builder)
    {
        var plan = builder.Plans[0];
        if (plan.Breaking)
        {
            if (factory.TryGetSpecAt(plan.Spec.Origin, out _, out _))
            {
                var site = Construction.BeginDeconstruct(state, plan.Spec.Origin, builder.OwnerPlayerId);
                builder.CurrentSiteId = site.Id;
            }
            else
            {
                // Already gone: nothing to take apart.
                builder.Plans.RemoveAt(0);
            }

            return;
        }

        string? reason = null;
        if (!factory.IsStructureUnlocked(plan.Spec.StructureId))
        {
            reason = CommandReasons.LockedByResearch;
        }
        else if (!factory.CanPlace(plan.Spec, out _))
        {
            reason = CommandReasons.Occupied;
        }

        if (reason is not null)
        {
            builder.Plans.RemoveAt(0);
            state.Events.Add(new PlanDropped(builder.OwnerPlayerId, plan, reason));
            return;
        }

        builder.CurrentSiteId = Construction.BeginBuild(state, plan.Spec, builder.OwnerPlayerId).Id;
    }
}
