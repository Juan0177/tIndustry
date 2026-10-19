using System.Numerics;

namespace TIndustry.Shared;

/// <summary>
/// Drawing-only motion of one belt item: where it was one tick before the latest tick and at the latest tick, and the
/// cell it was on. The client draws it at <see cref="At"/>(<see cref="SimHost.TickAlpha"/>), at most one tick behind
/// the simulation. Never part of the simulation state.
/// </summary>
public readonly record struct ItemMotion(Vector2 Before, Vector2 After, GridPosition Cell, Direction Direction)
{
    /// <summary>Standing at <paramref name="now"/>: a new item, or one that moved without continuity.</summary>
    public static ItemMotion Start(Vector2 now, GridPosition cell, Direction direction) => new(now, now, cell, direction);

    /// <summary>
    /// The motion after <paramref name="ticksRun"/> ticks, with the item now at <paramref name="now"/> on
    /// <paramref name="cell"/>. It starts over when <paramref name="restart"/> is set (a load, ticks dropped by the
    /// clock's cap), when there is no <paramref name="previous"/> motion, or when the item is neither on the same cell
    /// nor on the next cell along its old belt. With k ticks in one frame "before" is one tick behind the newest, on
    /// the path between the two positions seen: exact for the belts' linear motion, whatever k.
    /// </summary>
    public static ItemMotion Next(
        ItemMotion? previous, Vector2 now, GridPosition cell, Direction direction, long ticksRun, bool restart)
    {
        if (restart || ticksRun < 0 || previous is not { } old
            || (old.Cell != cell && old.Cell.Step(old.Direction) != cell))
        {
            return Start(now, cell, direction);
        }

        return ticksRun == 0
            ? old
            : new ItemMotion(Vector2.Lerp(old.After, now, (ticksRun - 1f) / ticksRun), now, cell, direction);
    }

    /// <summary>Where to draw the item at sub-tick fraction <paramref name="alpha"/> (0 = the tick before, 1 = the latest).</summary>
    public Vector2 At(double alpha) => Vector2.Lerp(Before, After, (float)alpha);
}
