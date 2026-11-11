namespace TIndustry.Shared;

/// <summary>
/// Moves the drones and starts their sites (Mindustry's player-controlled builder and <c>beginPlace</c>). Per
/// drone, in id order: it moves along its held input (<see cref="BuilderUnit.InputX"/>, <see cref="BuilderUnit.InputY"/>)
/// at its tier's speed, the diagonal normalized, kept inside the map and flying over everything; nothing moves it
/// on its own. A drone whose site falls out of build range lets it be (the site stays where it stopped, its plan stays
/// queued). A drone without a site picks the first plan of its queue within build range, brings it to the front,
/// revalidates it and turns it into a site (or resumes the site the plan left standing); plans out of range wait.
/// </summary>
public sealed class BuilderSystem : ISimulationSystem
{
    /// <summary>Closest the drone's centre gets to a map edge, in tiles (half a cell).</summary>
    private const float EdgeMargin = 0.5f;

    public string Name => "Builder";

    public void Run(GameState state, SystemContext ctx)
    {
        if (state.Builders.Count == 0)
        {
            return;
        }

        Factory? factory = null;
        var unit = state.EffectiveUnit;
        foreach (var builder in state.Builders)
        {
            Move(state, builder, unit.Speed * ctx.Dt);

            if (builder.CurrentSiteId is { } siteId)
            {
                KeepOrRelease(state, builder, siteId, unit.BuildRange);
                if (builder.CurrentSiteId is not null)
                {
                    continue;
                }
            }

            if (builder.Plans.Count == 0)
            {
                continue;
            }

            factory ??= new Factory(state);
            var first = FirstInRange(factory, builder);
            if (first < 0)
            {
                continue;
            }

            if (first > 0)
            {
                var plan = builder.PlanQueue[first];
                builder.PlanQueue.RemoveAt(first);
                builder.PlanQueue.Insert(0, plan);
            }

            BeginWork(state, factory, builder);
        }
    }

    /// <summary>
    /// Moves <paramref name="builder"/> by <paramref name="step"/> tiles along its held input, the diagonal
    /// normalized; the centre stays within [0.5, size − 0.5] on a map with terrain. The rotation follows the input
    /// and stays as it was without one.
    /// </summary>
    private static void Move(GameState state, BuilderUnit builder, float step)
    {
        if ((builder.InputX == 0 && builder.InputY == 0) || step <= 0f)
        {
            return;
        }

        float dx = builder.InputX;
        float dy = builder.InputY;
        if (dx != 0f && dy != 0f)
        {
            // 1/√2: the diagonal moves as fast as a straight line.
            const float diagonal = 0.70710677f;
            dx *= diagonal;
            dy *= diagonal;
        }

        builder.Rotation = MathF.Atan2(builder.InputY, builder.InputX) * (180f / MathF.PI);
        var x = builder.X + dx * step;
        var y = builder.Y + dy * step;
        if (state.Terrain is { } terrain)
        {
            x = Math.Clamp(x, EdgeMargin, terrain.Width - EdgeMargin);
            y = Math.Clamp(y, EdgeMargin, terrain.Height - EdgeMargin);
        }

        builder.X = x;
        builder.Y = y;
    }

    /// <summary>
    /// Keeps the drone on its site while the site is within <paramref name="buildRange"/> (the same point
    /// <see cref="ConstructSystem"/> measures to); out of range the drone lets the site be, leaving it standing and
    /// its plan queued. A site that no longer exists releases the drone and its plan.
    /// </summary>
    private static void KeepOrRelease(GameState state, BuilderUnit builder, long siteId, float buildRange)
    {
        ConstructSite? site = null;
        foreach (var candidate in state.Sites)
        {
            if (candidate.Id == siteId)
            {
                site = candidate;
                break;
            }
        }

        if (site is null)
        {
            builder.CurrentSiteId = null;
            if (builder.PlanQueue.Count > 0)
            {
                builder.PlanQueue.RemoveAt(0);
            }

            return;
        }

        var (x, y) = Factory.SiteCentre(site);
        if (Distance(builder, x, y) > buildRange)
        {
            builder.CurrentSiteId = null;
            site.StalledTicks = 0;
        }
    }

    /// <summary>Index of the first plan of the queue within build range, or −1.</summary>
    private static int FirstInRange(Factory factory, BuilderUnit builder)
    {
        for (var i = 0; i < builder.PlanQueue.Count; i++)
        {
            if (factory.IsPlanInRange(builder, builder.PlanQueue[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Distance from the drone to (<paramref name="x"/>, <paramref name="y"/>), in tiles.</summary>
    internal static float Distance(BuilderUnit builder, float x, float y)
    {
        var dx = x - builder.X;
        var dy = y - builder.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Turns the first plan into a site, or drops it when it can no longer start. A plan whose site already
    /// stands (it went to the back of the queue while stalled) picks that site up where it stopped.
    /// </summary>
    private static void BeginWork(GameState state, Factory factory, BuilderUnit builder)
    {
        var plan = builder.Plans[0];
        if (factory.SiteOfPlan(plan) is { } waiting)
        {
            builder.CurrentSiteId = waiting.Id;
            return;
        }

        if (plan.Breaking)
        {
            if (factory.TryGetSiteAt(plan.Spec.Origin, out _))
            {
                // A cancelled tier change still giving the old belt back: its own plan goes first.
                if (builder.PlanQueue.Count > 1)
                {
                    builder.PlanQueue.RemoveAt(0);
                    builder.PlanQueue.Add(plan);
                }
            }
            else if (factory.TryGetSpecAt(plan.Spec.Origin, out _, out _))
            {
                var site = Construction.BeginDeconstruct(state, plan.Spec.Origin, builder.OwnerPlayerId);
                builder.CurrentSiteId = site.Id;
            }
            else
            {
                // Already gone: nothing to take apart.
                builder.PlanQueue.RemoveAt(0);
            }

            return;
        }

        RefusalCode? reason = null;
        if (!factory.IsStructureUnlocked(plan.Spec.StructureId))
        {
            reason = RefusalCode.LockedByResearch;
        }
        else if (factory.IsReplacePlan(plan))
        {
            builder.CurrentSiteId = Construction.BeginReplace(state, plan.Spec, builder.OwnerPlayerId).Id;
            return;
        }
        else if (!factory.CanPlace(plan.Spec, out var refusal))
        {
            reason = refusal ?? RefusalCode.Occupied;
        }

        if (reason is not null)
        {
            builder.PlanQueue.RemoveAt(0);
            state.Events.Add(new PlanDropped(builder.OwnerPlayerId, plan, reason.Value));
            return;
        }

        builder.CurrentSiteId = Construction.BeginBuild(state, plan.Spec, builder.OwnerPlayerId).Id;
    }
}
