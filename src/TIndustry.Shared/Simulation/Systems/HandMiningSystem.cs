namespace TIndustry.Shared;

/// <summary>
/// Hand mining and unloading (spec «Scavo a mano», Mindustry's <c>MinerComp</c>). Per drone, in id order:
/// <list type="number">
/// <item>Mining: a <see cref="BuilderUnit.MiningTile"/> that is no longer minable (out of build range, no free deposit
/// the tier digs: covered by the Core, a building, a belt or a site) is cancelled, target and progress cleared
/// (Mindustry's <c>validMine</c>). While the drone is building (<see cref="BuilderUnit.CurrentSiteId"/>) mining
/// pauses. With room in the hold for the ore (empty, or the same item below <see cref="UnitDefinition.HoldCapacity"/>)
/// the progress gains the drone's <see cref="UnitDefinition.MineRateMilli"/> (the tier's unit with the research upgrades,
/// <see cref="GameState.EffectiveUnit"/>; its hold and range likewise) each tick and makes a piece at
/// <see cref="UnitDefinition.MineProgressPerPiece"/> (integers: 0.7 pieces/s is exactly 7 pieces in 600 ticks); a full
/// hold (the Core full of that item too, when near it) waits, target and progress kept. The deposit is never consumed.</item>
/// <item>Unloading: within <see cref="UnloadRange"/> cells of the Core (Euclidean distance from the drone's centre to the
/// nearest edge of the Core's cells; 0 over it) the hold goes into the Core up to its maximum per item
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

        var unit = state.EffectiveUnit;
        Factory? factory = null;
        foreach (var builder in state.Builders)
        {
            if (builder.MiningTile is not null)
            {
                Mine(factory ??= new Factory(state), builder, unit);
            }

            Unload(state, builder);
        }
    }

    /// <summary>
    /// Why <paramref name="builder"/> cannot start mining <paramref name="tile"/> now, or null when it can
    /// (<see cref="SetMining"/>, <see cref="Factory.MiningRefusal"/>).
    /// </summary>
    internal static RefusalCode? Refusal(Factory factory, BuilderUnit builder, GridPosition tile)
    {
        var state = factory.State;
        var unit = state.EffectiveUnit;
        if (Invalid(factory, builder, tile, unit) is { } invalid)
        {
            return invalid;
        }

        var itemId = OreAt(state, tile)!;
        if (builder.HoldCount > 0 && !string.Equals(builder.HoldItemId, itemId, StringComparison.Ordinal))
        {
            return RefusalCode.MiningHoldOtherItem;
        }

        // A full hold only makes sense next to a Core that still takes the item: it unloads and mining goes on.
        if (builder.HoldCount >= unit.HoldCapacity
            && !(CanUnload(state, builder) && state.Storage.CanAccept(itemId)))
        {
            return RefusalCode.MiningHoldFull;
        }

        return null;
    }

    /// <summary>Why <paramref name="tile"/> is not minable by <paramref name="builder"/> at all, or null.</summary>
    private static RefusalCode? Invalid(Factory factory, BuilderUnit builder, GridPosition tile, UnitDefinition unit)
    {
        if (OreAt(factory.State, tile) is not { } itemId)
        {
            return RefusalCode.MiningNoDeposit;
        }

        if (!unit.CanMine(itemId))
        {
            return RefusalCode.MiningNotMineable;
        }

        if (!InRange(builder, tile, unit.BuildRange))
        {
            return RefusalCode.MiningOutOfRange;
        }

        if (IsCovered(factory, tile))
        {
            return RefusalCode.MiningCovered;
        }

        return null;
    }

    private static void Mine(Factory factory, BuilderUnit builder, UnitDefinition unit)
    {
        var tile = builder.MiningTile!.Value;
        if (Invalid(factory, builder, tile, unit) is not null)
        {
            builder.MiningTile = null;
            builder.MiningProgress = 0;
            return;
        }

        var itemId = OreAt(factory.State, tile)!;
        if (builder.CurrentSiteId is not null
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
        if (builder.HoldCount <= 0 || builder.HoldItemId is not { } itemId || !CanUnload(state, builder))
        {
            return;
        }

        builder.HoldCount -= state.Storage.AddClamped(itemId, builder.HoldCount);
        if (builder.HoldCount == 0)
        {
            builder.HoldItemId = null;
        }
    }

    private static bool CanUnload(GameState state, BuilderUnit builder) =>
        state.CoreTiles.Count > 0 && DistanceToCore(state, builder.X, builder.Y) <= UnloadRange;

    /// <summary>The Core, a building, a belt (any logistics block) or a construction site stands on <paramref name="tile"/>.</summary>
    private static bool IsCovered(Factory factory, GridPosition tile) =>
        factory.CoreTiles.Contains(tile) || factory.Belts.Contains(tile) || factory.IsBuildingTile(tile);

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
    internal static bool InRange(BuilderUnit builder, GridPosition tile, float range) =>
        BuilderSystem.Distance(builder, tile.X + 0.5f, tile.Y + 0.5f) <= range;

    /// <summary>
    /// Euclidean distance from (<paramref name="x"/>, <paramref name="y"/>) to the nearest edge of the Core's cells
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
