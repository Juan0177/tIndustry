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

    private static int Axis(float delta) => delta > 0.5f ? 1 : delta < -0.5f ? -1 : 0;
}
