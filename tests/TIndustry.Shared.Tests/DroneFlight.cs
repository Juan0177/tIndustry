using TIndustry.Shared;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Flies a player's drone the way a player does, with <see cref="SetDroneInput"/>: the drone no longer flies to its
/// plans on its own, so tests that build far from the Core bring it within range first.
/// </summary>
internal static class DroneFlight
{
    /// <summary>
    /// Holds the input towards (<paramref name="x"/>, <paramref name="y"/>) one tick at a time until the drone is within
    /// half a tile on both axes, then releases it; returns the ticks run.
    /// </summary>
    public static int FlyTo(SimHost host, Factory factory, float x, float y, int playerId = 0, int maxTicks = 20_000)
    {
        var drone = factory.BuilderOf(playerId) ?? throw new InvalidOperationException("nessun drone");
        var (heldX, heldY) = (drone.InputX, drone.InputY);
        for (var tick = 0; tick <= maxTicks; tick++)
        {
            var dx = Axis(x - drone.X);
            var dy = Axis(y - drone.Y);
            if ((dx, dy) != (heldX, heldY))
            {
                host.Submit(new SetDroneInput(playerId, dx, dy));
                (heldX, heldY) = (dx, dy);
            }

            if (dx == 0 && dy == 0)
            {
                host.AdvanceTicks(1); // applies the release
                return tick + 1;
            }

            host.AdvanceTicks(1);
        }

        throw new InvalidOperationException("drone mai arrivato");
    }

    /// <summary>
    /// The <paramref name="kind"/> deposit cell nearest to the Core's centre that is at least <paramref name="minDistance"/>
    /// tiles from it (a real map: no hand-made terrain), ties broken by (y, x).
    /// </summary>
    public static GridPosition NearestDeposit(Factory factory, DepositKind kind, float minDistance)
    {
        var terrain = factory.Terrain ?? throw new InvalidOperationException("nessuna mappa");
        var (cx, cy) = factory.CoreCentre();
        GridPosition? best = null;
        var bestDistance = float.MaxValue;
        for (var y = 0; y < terrain.Height; y++)
        {
            for (var x = 0; x < terrain.Width; x++)
            {
                if (terrain[x, y].Deposit != kind)
                {
                    continue;
                }

                var d = MathF.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                if (d >= minDistance && d < bestDistance)
                {
                    (best, bestDistance) = (new GridPosition(x, y), d);
                }
            }
        }

        return best ?? throw new InvalidOperationException($"nessun giacimento {kind}");
    }

    private static int Axis(float delta) => delta > 0.5f ? 1 : delta < -0.5f ? -1 : 0;
}
