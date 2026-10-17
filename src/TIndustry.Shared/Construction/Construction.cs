namespace TIndustry.Shared;

/// <summary>Outcome of one <see cref="Construction.Advance"/> tick.</summary>
public enum ConstructStep
{
    Progressed,
    Stalled,
    Finished
}

/// <summary>
/// Construction sites as in Mindustry: build time from
/// <c>buildCost = (20 + Σ amount × buildWeight) × buildCostMultiplier</c>, progress
/// <c>buildSpeed / buildCost</c> per tick, cost taken from the Core gradually and refunds given back
/// gradually while deconstructing.
/// </summary>
public static class Construction
{
    /// <summary>Mindustry's base build cost of any block.</summary>
    public const float BaseBuildCost = 20f;

    /// <summary>Build cost (time units) of <paramref name="spec"/>; money does not count.</summary>
    public static float BuildCost(FactoryContent c, BuildSpec spec)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(spec);
        var weighted = 0f;
        foreach (var entry in CostOf(c, spec))
        {
            if (entry.ItemId is { } itemId)
            {
                weighted += entry.Amount * c.BuildWeight(itemId);
            }
        }

        return (BaseBuildCost + weighted) * c.BuildCostMultiplier(spec.StructureId);
    }

    /// <summary>
    /// Whole cost of <paramref name="spec"/>: money first when &gt; 0, then materials in ordinal id order
    /// (duplicates merged). A bridge pays for both its ends.
    /// </summary>
    internal static IReadOnlyList<CostEntry> CostOf(FactoryContent c, BuildSpec spec)
    {
        int money;
        IReadOnlyList<ResourceAmount> materials;
        if (spec.Kind is not null && c.FindBuilding(spec.StructureId) is { } building)
        {
            money = building.MoneyCost;
            materials = building.BuildCost;
        }
        else if (c.FindConveyor(spec.StructureId) is { } conveyor)
        {
            money = conveyor.MoneyCost;
            materials = conveyor.EffectiveBuildCost;
        }
        else
        {
            return [];
        }

        var multiplicity = spec.Kind == BuildingKind.Bridge ? 2 : 1;
        var cost = new List<CostEntry>();
        if (money > 0)
        {
            cost.Add(new CostEntry(null, money * multiplicity));
        }

        foreach (var group in materials
                     .Where(m => m.Amount > 0)
                     .GroupBy(m => m.ItemId, StringComparer.Ordinal)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            cost.Add(new CostEntry(group.Key, group.Sum(m => m.Amount) * multiplicity));
        }

        return cost;
    }

    /// <summary>
    /// Starts building <paramref name="spec"/>: reserves its cells with a site at progress 0 that has
    /// taken nothing yet. Throws when the cells are not free (callers check <see cref="Factory.CanPlace"/>).
    /// </summary>
    public static ConstructSite BeginBuild(GameState s, BuildSpec spec, int ownerPlayerId)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(spec);
        var factory = new Factory(s);
        if (!factory.CanPlace(spec, out var reason))
        {
            throw new InvalidOperationException($"Cantiere non avviabile su {spec.Origin}: {reason}");
        }

        var site = new ConstructSite(
            s.NextEntityId++,
            spec,
            factory.Footprint(spec),
            factory.CostOf(spec),
            ownerPlayerId);
        s.Sites.Add(site);
        return site;
    }

    /// <summary>
    /// Turns the building or belt on <paramref name="tile"/> into a deconstruction site: the entity is
    /// removed without refund (its items are lost, as in Mindustry) and the site starts complete
    /// (progress 1, whole cost spent). Throws when nothing is there.
    /// </summary>
    public static ConstructSite BeginDeconstruct(GameState s, GridPosition tile, int ownerPlayerId)
    {
        ArgumentNullException.ThrowIfNull(s);
        var factory = new Factory(s);
        if (!factory.TryGetSpecAt(tile, out var spec, out _))
        {
            throw new InvalidOperationException($"Niente da smontare su {tile}.");
        }

        var tiles = factory.Footprint(spec);
        var cost = factory.CostOf(spec);
        var refundPercent = factory.RefundPercentFor(spec);
        factory.RemoveInstant(tile, refund: false);

        var site = new ConstructSite(s.NextEntityId++, spec, tiles, cost, ownerPlayerId)
        {
            Progress = 1f,
            Deconstructing = true,
            RefundPercent = refundPercent
        };
        for (var i = 0; i < cost.Count; i++)
        {
            site.Spent[i] = cost[i].Amount;
        }

        s.Sites.Add(site);
        return site;
    }

    /// <summary>
    /// Cancels a site under construction: it is taken apart from where it is and gives back all it
    /// has spent. No-op on a site that is already deconstructing.
    /// </summary>
    public static void Cancel(ConstructSite site)
    {
        ArgumentNullException.ThrowIfNull(site);
        if (site.Deconstructing)
        {
            return;
        }

        site.Deconstructing = true;
        site.RefundPercent = 100;
        site.Missing = null;
    }

    /// <summary>
    /// One tick of work on <paramref name="site"/> at <paramref name="buildSpeed"/>. Building: progress is
    /// capped by what the Core can still pay (Mindustry's <c>checkRequired</c>), so a short site stops
    /// exactly where resources end and resumes by itself; at 1 the real block replaces the site.
    /// Deconstructing: progress goes down at the same speed and refunds come back gradually; at 0 the
    /// site disappears.
    /// </summary>
    public static ConstructStep Advance(GameState s, ConstructSite site, float buildSpeed)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(site);
        var buildCost = BuildCost(s.Content, site.Spec);
        return site.Deconstructing
            ? AdvanceDeconstruct(s, site, buildSpeed / buildCost)
            : AdvanceBuild(s, site, buildSpeed / buildCost);
    }

    private static ConstructStep AdvanceBuild(GameState s, ConstructSite site, float delta)
    {
        var target = Math.Min(1f, site.Progress + delta);
        CostEntry? limiting = null;
        for (var i = 0; i < site.Cost.Count; i++)
        {
            var amount = site.Cost[i].Amount;
            if (amount <= 0)
            {
                continue;
            }

            var available = Available(s.Wallet, site.Cost[i].ItemId);
            var cap = (site.Spent[i] + available) / (float)amount;
            if (cap < target)
            {
                target = cap;
                limiting = new CostEntry(
                    site.Cost[i].ItemId,
                    Math.Max(0, amount - site.Spent[i] - available));
            }
        }

        if (target <= site.Progress)
        {
            site.Missing = limiting;
            return ConstructStep.Stalled;
        }

        for (var i = 0; i < site.Cost.Count; i++)
        {
            var amount = site.Cost[i].Amount;
            var due = target >= 1f ? amount : (int)Math.Ceiling(target * (double)amount);
            // The clamp absorbs float overshoot of ceil: never more than the Core holds or than the cost.
            var take = Math.Clamp(
                Math.Min(due, amount) - site.Spent[i],
                0,
                Available(s.Wallet, site.Cost[i].ItemId));
            if (take > 0 && Take(s.Wallet, site.Cost[i].ItemId, take))
            {
                site.Spent[i] += take;
            }
        }

        site.Progress = target;
        site.Missing = null;
        if (site.Progress < 1f)
        {
            return ConstructStep.Progressed;
        }

        s.Sites.Remove(site);
        var factory = new Factory(s);
        var bridgeExit = site.Spec.Kind == BuildingKind.Bridge && site.Tiles.Count > 1
            ? site.Tiles[1]
            : (GridPosition?)null;
        if (!factory.PlaceInstant(site.Spec, charge: false, bridgeExit))
        {
            // Cannot happen: the site kept its cells reserved.
            throw new InvalidOperationException($"Cantiere {site.Id} completato su celle non valide.");
        }

        return ConstructStep.Finished;
    }

    private static ConstructStep AdvanceDeconstruct(GameState s, ConstructSite site, float delta)
    {
        site.Progress = Math.Max(0f, site.Progress - delta);
        site.Missing = null;
        for (var i = 0; i < site.Cost.Count; i++)
        {
            var total = site.Spent[i] * site.RefundPercent / 100;
            var due = site.Progress <= 0f
                ? total
                : (int)Math.Floor((1f - site.Progress) * (double)total);
            var give = Math.Min(due, total) - site.Refunded[i];
            if (give <= 0)
            {
                continue;
            }

            if (site.Cost[i].ItemId is { } itemId)
            {
                s.Wallet.AddMaterial(itemId, give);
            }
            else
            {
                s.Wallet.AddMoney(give);
            }

            site.Refunded[i] += give;
        }

        if (site.Progress > 0f)
        {
            return ConstructStep.Progressed;
        }

        s.Sites.Remove(site);
        return ConstructStep.Finished;
    }

    private static int Available(EconomyWallet wallet, string? itemId) =>
        Math.Max(0, itemId is null ? wallet.Money : wallet.MaterialCount(itemId));

    private static bool Take(EconomyWallet wallet, string? itemId, int amount) =>
        itemId is null ? wallet.TrySpend(amount, []) : wallet.TryRemoveMaterial(itemId, amount);
}
