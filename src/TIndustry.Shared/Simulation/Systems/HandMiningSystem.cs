namespace TIndustry.Shared;

/// <summary>
/// Hand mining and unloading (spec «Scavo a mano», Mindustry's <c>MinerComp</c>). Per drone, in id order:
/// <list type="number">
/// <item>Mining: with a <see cref="BuilderUnit.MiningTile"/> within build range whose ore its tier digs, and room in the
/// hold for that ore (empty, or the same item below <see cref="UnitDefinition.HoldCapacity"/>), the progress gains the
/// tier's <see cref="UnitDefinition.MineRateMilli"/> each tick and makes a piece at
/// <see cref="UnitDefinition.MineProgressPerPiece"/> (integers: 0.7 pieces/s is exactly 7 pieces in 600 ticks).
/// Otherwise it waits, target and progress kept. The deposit is never consumed.</item>
/// <item>Unloading: within <see cref="UnloadRange"/> cells of the Core (Euclidean distance from the drone's centre to the
/// nearest point of the Core's cells; 0 over it) the hold goes into the Core up to its maximum per item
/// (<see cref="CoreStorage.AddClamped"/>); what does not fit stays in the hold. Hand-unloaded pieces do not count in
/// <see cref="GameState.CoreDeliveredItems"/> (that counter is the belts').</item>
/// </list>
/// </summary>
public sealed class HandMiningSystem : ISimulationSystem
{
    /// <summary>Farthest the drone unloads its hold from the Core, in cells (spec: 6).</summary>
    public const float UnloadRange = 6f;

    public string Name => "HandMining";

    public void Run(GameState state, SystemContext ctx)
    {
        if (state.Builders.Count == 0)
        {
            return;
        }

        var unit = state.Content.UnitForTier(state.CoreTier);
        foreach (var builder in state.Builders)
        {
            Mine(state, builder, unit);
            Unload(state, builder);
        }
    }

    /// <summary>
    /// Why <paramref name="builder"/> cannot start mining <paramref name="tile"/> now, or null when it can
    /// (<see cref="SetMining"/>, <see cref="Factory.MiningRefusal"/>).
    /// </summary>
    internal static RefusalCode? Refusal(GameState state, BuilderUnit builder, GridPosition tile)
    {
        if (OreAt(state, tile) is not { } itemId)
        {
            return RefusalCode.MiningNoDeposit;
        }

        var unit = state.Content.UnitForTier(state.CoreTier);
        if (!unit.CanMine(itemId))
        {
            return RefusalCode.MiningNotMineable;
        }

        if (!InRange(builder, tile, unit.BuildRange))
        {
            return RefusalCode.MiningOutOfRange;
        }

        if (builder.HoldCount > 0 && !string.Equals(builder.HoldItemId, itemId, StringComparison.Ordinal))
        {
            return RefusalCode.MiningHoldOtherItem;
        }

        return null;
    }

    private static void Mine(GameState state, BuilderUnit builder, UnitDefinition unit)
    {
        if (builder.MiningTile is not { } tile
            || OreAt(state, tile) is not { } itemId
            || !unit.CanMine(itemId)
            || !InRange(builder, tile, unit.BuildRange)
            || builder.HoldCount >= unit.HoldCapacity
            || (builder.HoldCount > 0 && !string.Equals(builder.HoldItemId, itemId, StringComparison.Ordinal)))
        {
            return;
        }

        builder.MiningProgress += unit.MineRateMilli;
        if (builder.MiningProgress < UnitDefinition.MineProgressPerPiece)
        {
            return;
        }

        // Rates stay well below a piece per tick, so at most one piece lands per tick.
        builder.MiningProgress -= UnitDefinition.MineProgressPerPiece;
        builder.HoldItemId = itemId;
        builder.HoldCount++;
    }

    private static void Unload(GameState state, BuilderUnit builder)
    {
        if (builder.HoldCount <= 0 || builder.HoldItemId is not { } itemId || state.CoreTiles.Count == 0
            || DistanceToCore(state, builder.X, builder.Y) > UnloadRange)
        {
            return;
        }

        builder.HoldCount -= state.Storage.AddClamped(itemId, builder.HoldCount);
        if (builder.HoldCount == 0)
        {
            builder.HoldItemId = null;
        }
    }

    /// <summary>The ore item of the deposit on <paramref name="tile"/>, or null (no terrain, outside the map, no deposit).</summary>
    internal static string? OreAt(GameState state, GridPosition tile)
    {
        if (state.Terrain is not { } terrain || !terrain.InBounds(tile))
        {
            return null;
        }

        var deposit = terrain[tile].Deposit;
        return deposit == DepositKind.None ? null : TerrainMap.ItemIdForDeposit(deposit);
    }

    /// <summary>Distance from the drone's centre to the centre of <paramref name="tile"/> within <paramref name="range"/>.</summary>
    private static bool InRange(BuilderUnit builder, GridPosition tile, float range) =>
        BuilderSystem.Distance(builder, tile.X + 0.5f, tile.Y + 0.5f) <= range;

    /// <summary>
    /// Euclidean distance from (<paramref name="x"/>, <paramref name="y"/>) to the nearest point of the Core's cells
    /// (their bounding square), 0 over the Core. Min and max do not depend on the set's order.
    /// </summary>
    internal static float DistanceToCore(GameState state, float x, float y)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var t in state.CoreTiles)
        {
            minX = Math.Min(minX, t.X);
            minY = Math.Min(minY, t.Y);
            maxX = Math.Max(maxX, t.X);
            maxY = Math.Max(maxY, t.Y);
        }

        var dx = Math.Max(0f, Math.Max(minX - x, x - (maxX + 1)));
        var dy = Math.Max(0f, Math.Max(minY - y, y - (maxY + 1)));
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
