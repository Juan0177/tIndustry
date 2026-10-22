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

    /// <summary>100% expressed in basis points (refund shares).</summary>
    public const int FullRefundBasisPoints = 10_000;

    /// <summary>
    /// What comes back of <paramref name="amount"/> at <paramref name="basisPoints"/>, rounded up once: at 50% a
    /// cost of 1 gives 1 back. Never more than <paramref name="amount"/> (the share is at most 100%).
    /// </summary>
    public static int RefundShare(long amount, int basisPoints)
    {
        if (amount <= 0 || basisPoints <= 0)
        {
            return 0;
        }

        var share = Math.Min(basisPoints, FullRefundBasisPoints);
        return (int)((amount * share + FullRefundBasisPoints - 1) / FullRefundBasisPoints);
    }

    /// <summary>
    /// Ticks in a row (one second) a worked site may stay stalled for materials before its plan goes to the
    /// back of the drone's queue, when other plans wait (Mindustry's <c>plan.stuck</c>).
    /// </summary>
    public const int StuckTicks = 60;

    /// <summary>Build cost (time units) of <paramref name="spec"/>; money does not count.</summary>
    public static float BuildCost(FactoryContent c, BuildSpec spec)
    {
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(spec);
        return BuildCost(c, spec.StructureId, CostOf(c, spec));
    }

    /// <summary>Build cost from an already computed cost list (a site's <see cref="ConstructSite.Cost"/>).</summary>
    internal static float BuildCost(FactoryContent c, string structureId, IReadOnlyList<CostEntry> cost)
    {
        var weighted = 0f;
        foreach (var entry in cost)
        {
            if (entry.ItemId is { } itemId)
            {
                weighted += entry.Amount * c.BuildWeight(itemId);
            }
        }

        return (BaseBuildCost + weighted) * c.BuildCostMultiplier(structureId);
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
    /// Starts replacing the plain belt under <paramref name="spec"/> with another tier: the old belt goes away without
    /// refund, the site remembers its spec and items (<see cref="ConstructSite.Replaced"/>) and counts the common part of
    /// the two costs as already spent (<see cref="ReplacementCredit"/>). The build time is the new belt's whole time.
    /// Throws when there is no plain belt to replace.
    /// </summary>
    public static ConstructSite BeginReplace(GameState s, BuildSpec spec, int ownerPlayerId)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(spec);
        if (!new Factory(s).TryTakeBeltForReplacement(spec.Origin, out var old))
        {
            throw new InvalidOperationException($"Nessun nastro da sostituire su {spec.Origin}.");
        }

        var site = BeginBuild(s, spec, ownerPlayerId);
        site.Replaced = old;
        var credit = ReplacementCredit(s.Content, site);
        for (var i = 0; i < credit.Length; i++)
        {
            site.SpentLines[i] = credit[i];
        }

        return site;
    }

    /// <summary>
    /// Credit of a replacement site per <see cref="ConstructSite.Cost"/> line: <c>min(new, old)</c> for the same item
    /// (money is a line of its own); all 0 for an ordinary site. Recomputed from the content, never saved.
    /// </summary>
    internal static int[] ReplacementCredit(FactoryContent c, ConstructSite site) =>
        site.Replaced is { } old ? Credit(site.Cost, CostOf(c, old.Spec)) : new int[site.Cost.Count];

    /// <summary><c>min(new, old)</c> per line of <paramref name="newCost"/>, matched by item id (null = money).</summary>
    internal static int[] Credit(IReadOnlyList<CostEntry> newCost, IReadOnlyList<CostEntry> oldCost)
    {
        var credit = new int[newCost.Count];
        for (var i = 0; i < newCost.Count; i++)
        {
            foreach (var entry in oldCost)
            {
                if (string.Equals(entry.ItemId, newCost[i].ItemId, StringComparison.Ordinal))
                {
                    credit[i] = Math.Min(newCost[i].Amount, entry.Amount);
                }
            }
        }

        return credit;
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
        var refundBasisPoints = factory.RefundBasisPointsFor(spec);
        factory.RemoveInstant(tile, refund: false);

        var site = new ConstructSite(s.NextEntityId++, spec, tiles, cost, ownerPlayerId)
        {
            Progress = 1f,
            Deconstructing = true,
            RefundBasisPoints = refundBasisPoints
        };
        for (var i = 0; i < cost.Count; i++)
        {
            site.SpentLines[i] = cost[i].Amount;
        }

        s.Sites.Add(site);
        return site;
    }

    /// <summary>
    /// Cancels a site under construction: it is taken apart from where it is and gives back all it
    /// has spent. A replacement site first takes its credit out of what it spent (it was never paid) and, at 0,
    /// puts the old belt back with its items. No-op on a site that is already deconstructing.
    /// </summary>
    public static void Cancel(GameState s, ConstructSite site)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(site);
        if (site.Deconstructing)
        {
            return;
        }

        var credit = ReplacementCredit(s.Content, site);
        for (var i = 0; i < credit.Length; i++)
        {
            site.SpentLines[i] = Math.Max(0, site.SpentLines[i] - credit[i]);
        }

        site.Deconstructing = true;
        site.RefundBasisPoints = FullRefundBasisPoints;
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
        var buildCost = BuildCost(s.Content, site.Spec.StructureId, site.Cost);
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
                site.SpentLines[i] += take;
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

        if (site.Replaced is { } old)
        {
            factory.SettleReplacement(site.Spec, old);
        }

        return ConstructStep.Finished;
    }

    private static ConstructStep AdvanceDeconstruct(GameState s, ConstructSite site, float delta)
    {
        site.Progress = Math.Max(0f, site.Progress - delta);
        site.Missing = null;
        for (var i = 0; i < site.Cost.Count; i++)
        {
            // Total back: spent × share, rounded up once. What is still "standing" is the share of the
            // remaining progress, rounded up, so refunds flow gradually from wherever the site starts
            // (1 for a finished building, its current progress for a cancelled site).
            var total = RefundShare(site.Spent[i], site.RefundBasisPoints);
            var standing = site.Progress <= 0f
                ? 0
                : (int)Math.Ceiling(
                    site.Progress * (double)site.Cost[i].Amount * site.RefundBasisPoints / FullRefundBasisPoints);
            var due = Math.Clamp(total - standing, 0, total);
            var give = due - site.Refunded[i];
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

            site.RefundedLines[i] += give;
        }

        if (site.Progress > 0f)
        {
            return ConstructStep.Progressed;
        }

        s.Sites.Remove(site);
        if (site.Replaced is { } old)
        {
            new Factory(s).RestoreReplacedBelt(old);
        }

        return ConstructStep.Finished;
    }

    private static int Available(EconomyWallet wallet, string? itemId) =>
        Math.Max(0, itemId is null ? wallet.Money : wallet.MaterialCount(itemId));

    private static bool Take(EconomyWallet wallet, string? itemId, int amount) =>
        itemId is null ? wallet.TrySpend(amount, []) : wallet.TryRemoveMaterial(itemId, amount);
}
