namespace TIndustry.Shared;

/// <summary>
/// Playable factory simulation: miners → belts → forni/assemblatori → core stock.
/// Phase F: generators feed a shared power buffer; connected craft spends for +20%.
/// Soft brownout: empty buffer → forno falls back to coal; assembler loses bonus.
/// </summary>
public sealed class Factory : IGameView
{
    /// <summary>Raylib <c>FactoryWorld.CorePowerCapacity</c>.</summary>
    public const float CorePowerCapacity = GameState.CorePowerCapacity;
    /// <summary>Raylib <c>FactoryWorld.CorePowerGeneration</c> (always ticks into buffer).</summary>
    public const float CorePowerGeneration = GameState.CorePowerGeneration;
    /// <summary>Raylib <c>FactoryWorld.SmelterPowerDraw</c> (per second while powered craft).</summary>
    public const float SmelterPowerDraw = CraftingSystem.SmelterPowerDraw;
    /// <summary>Raylib <c>FactoryWorld.AssemblerPowerDraw</c>.</summary>
    public const float AssemblerPowerDraw = CraftingSystem.AssemblerPowerDraw;

    /// <summary>The whole simulation state; <see cref="Simulation.Step"/> advances it.</summary>
    public GameState State { get; }

    private long nextItemId
    {
        get => State.NextItemId;
        set => State.NextItemId = value;
    }

    private long nextEntityId
    {
        get => State.NextEntityId;
        set => State.NextEntityId = value;
    }

    private List<Miner> miners => State.Miners;
    private List<Crafter> smelters => State.Smelters;
    private List<Crafter> assemblers => State.Assemblers;
    private List<Generator> generators => State.Generators;
    private List<Extractor> extractors => State.Extractors;
    private List<PowerNode> powerNodes => State.PowerNodes;
    private List<Lamp> lamps => State.Lamps;
    private List<StorageBlock> storages => State.Storages;

    public Factory(
        FactoryContent content,
        BeltGrid belts,
        IReadOnlySet<GridPosition> coreTiles,
        EconomyWallet? wallet = null,
        ResearchState? research = null,
        EconomySession? session = null)
    {
        var resolvedWallet = wallet ?? new EconomyWallet();
        State = new GameState(
            content,
            belts,
            coreTiles,
            resolvedWallet,
            research ?? ResearchState.CreateNew(content),
            session ?? new EconomySession(resolvedWallet.Money));
        BeltDefinition = belts.DefaultDefinition
            ?? content.RequireConveyor("conveyor-basic");
        JunctionDefinition = content.RequireConveyor("junction");
        SplitterDefinition = content.RequireConveyor("splitter");
        SorterDefinition = content.RequireConveyor("sorter");
        BridgeDefinition = content.RequireConveyor("conveyor-bridge");
    }

    /// <summary>Wraps an existing state (used by snapshots); definitions come from its content.</summary>
    internal Factory(GameState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        BeltDefinition = state.Belts.DefaultDefinition
            ?? state.Content.RequireConveyor("conveyor-basic");
        JunctionDefinition = state.Content.RequireConveyor("junction");
        SplitterDefinition = state.Content.RequireConveyor("splitter");
        SorterDefinition = state.Content.RequireConveyor("sorter");
        BridgeDefinition = state.Content.RequireConveyor("conveyor-bridge");
    }

    public FactoryContent Content => State.Content;
    public BeltGrid Belts => State.Belts;
    public ConveyorDefinition BeltDefinition { get; }
    public ConveyorDefinition JunctionDefinition { get; }
    public ConveyorDefinition SplitterDefinition { get; }
    public ConveyorDefinition SorterDefinition { get; }
    public ConveyorDefinition BridgeDefinition { get; }
    public IReadOnlySet<GridPosition> CoreTiles => State.CoreTiles;
    public int CoreTier => State.CoreTier;
    public int CoreCapacity(string itemId) => State.Storage.Capacity(itemId);
    public EconomyWallet Wallet => State.Wallet;
    public ResearchState Research => State.Research;
    public EconomySession Session => State.Session;
    public string? ActiveCampaignLevelId
    {
        get => State.ActiveCampaignLevelId;
        set => State.ActiveCampaignLevelId = value;
    }

    /// <summary>Shared power buffer (Raylib parity). Recharges from core + live gens.</summary>
    public float PowerBuffer
    {
        get => State.PowerBuffer;
        private set => State.PowerBuffer = value;
    }

    /// <summary>Max buffer: core capacity + live generators × CapacityBonus.</summary>
    public float PowerCapacity
    {
        get => State.PowerCapacity;
        private set => State.PowerCapacity = value;
    }

    /// <summary>Last tick generation rate (core + live gens).</summary>
    public float PowerGenerationPerSecond => State.PowerGenerationPerSecond;

    public TerrainMap? Terrain
    {
        get => State.Terrain;
        set => State.Terrain = value;
    }

    public long CoreDeliveredItems
    {
        get => State.CoreDeliveredItems;
        private set => State.CoreDeliveredItems = value;
    }

    public IReadOnlyList<Miner> Miners => miners;
    public IReadOnlyList<Crafter> Smelters => smelters;
    public IReadOnlyList<Crafter> Assemblers => assemblers;
    public IReadOnlyList<Generator> Generators => generators;
    public IReadOnlyList<Extractor> Extractors => extractors;
    public IReadOnlyList<PowerNode> PowerNodes => powerNodes;
    public IReadOnlyList<Lamp> Lamps => lamps;
    public IReadOnlyList<StorageBlock> Storages => storagesView ??= storages.AsReadOnly();
    private IReadOnlyList<StorageBlock>? storagesView;
    public GameRules Rules => State.Rules;

    /// <inheritdoc cref="GameRules.DayNight"/>
    public DayNightMode DayNight => State.Rules.DayNight;

    public DayNightPhase DayNightPhase => DayNightClock.PhaseAt(State.Rules.DayNight, State.FirstBuildTick, State.CurrentTick);

    public int Light => DayNightClock.LightAt(State.Rules.DayNight, State.FirstBuildTick, State.CurrentTick);

    public long? TicksToNextChange =>
        DayNightClock.TicksToNextChange(State.Rules.DayNight, State.FirstBuildTick, State.CurrentTick);

    public long? FirstBuildTick => State.FirstBuildTick;

    /// <summary>
    /// A block was placed (a site opened or an instant placement accepted from a player's command): the world's time
    /// starts now, the first time only (<see cref="GameState.FirstBuildTick"/>).
    /// </summary>
    internal void NoteFirstBuild() => State.FirstBuildTick ??= State.CurrentTick;
    public long NextItemId => nextItemId;
    public long NextEntityId => nextEntityId;

    /// <summary>Fixed ticks simulated so far (<see cref="SimulationClock.TickSeconds"/> each).</summary>
    public long CurrentTick
    {
        get => State.CurrentTick;
        private set => State.CurrentTick = value;
    }

    IReadOnlyDictionary<GridPosition, BeltGridCell> IGameView.Belts => Belts.Cells;
    IWalletView IGameView.Wallet => Wallet;
    IResearchView IGameView.Research => Research;
    ISessionView IGameView.Session => Session;

    public bool IsBeltCorner(GridPosition position) => GetBeltShape(position) == BeltShape.Corner;

    public BeltShape GetBeltShape(GridPosition position) =>
        BeltIncoming().TryGetValue(position, out var entry) ? entry.Shape : BeltShape.Straight;

    public bool IsJunctionSideConnected(GridPosition position, Direction side) =>
        BeltIncoming().TryGetValue(position, out var entry) && (entry.JunctionSides & (1 << (int)side)) != 0;

    /// <summary>
    /// Mindustry's auto-junction: a dragged belt line facing <paramref name="direction"/> that crosses a plain belt
    /// perpendicular to it puts a junction there, so both lines keep flowing. Only where the junction behaves like the
    /// belt it replaces: the crossed belt is straight (not a corner or T; the dragged line joining its side is
    /// fine), fed from behind by logistics only (no building feeds it) and hands over to a logistics cell ahead (not the Core or a building);
    /// the cell has no plan (not marked for removal). Whether the cell lies inside the dragged line is the line's call
    /// (<see cref="PlacementLine"/>, <see cref="BeltLine.IsInterior"/>): two crossings in a row both become junctions.
    /// Splitters, sorters, bridges, junctions and parallel belts are never crossings.
    /// </summary>
    public BeltCrossing BeltCrossingAt(GridPosition position, Direction direction)
    {
        if (!Belts.TryGet(position, out var cell)
            || cell.Kind != LogisticsKind.Belt
            || cell.Direction == direction
            || cell.Direction == DirectionMath.Opposite(direction)
            || GetBeltShape(position) is BeltShape.Corner or BeltShape.T
            || IsPlannedTile(position)
            || !Belts.Contains(position.Step(cell.Direction)))
        {
            return BeltCrossing.None;
        }

        BeltIncoming(); // builds the building-feed lookup used below
        var logistics = Belts.Feeders(position, cell.Direction, null);
        var all = Belts.Feeders(position, cell.Direction, buildingFeeds);
        if (!logistics.Back || all != logistics)
        {
            return BeltCrossing.None;
        }

        return IsStructureUnlocked(BuildSpec.StructureIdFor(BuildingKind.Junction)!)
            ? BeltCrossing.Junction
            : BeltCrossing.None;
    }

    /// <summary>
    /// Mindustry's belt over belt: <see cref="BeltOverBelt.None"/> unless <paramref name="cell"/> holds a plain belt
    /// that no plan, mark for removal or site touches and <paramref name="conveyorId"/> is a plain belt; then
    /// <see cref="BeltOverBelt.Same"/> (same id and direction) or <see cref="BeltOverBelt.Rotate"/> (same id).
    /// </summary>
    public BeltOverBelt BeltOverBeltAt(GridPosition cell, Direction direction, string conveyorId) =>
        BeltOverBeltAt(cell, direction, conveyorId, ignoring: null);

    /// <summary>True while the build plan's cell still answers <see cref="BeltOverBelt.Replace"/>, the plan itself aside.</summary>
    internal bool IsReplacePlan(BuildPlan plan) =>
        !plan.Breaking
        && plan.Spec.Kind is null
        && BeltOverBeltAt(plan.Spec.Origin, plan.Spec.Direction, plan.Spec.StructureId, plan) == BeltOverBelt.Replace;

    private BeltOverBelt BeltOverBeltAt(GridPosition cell, Direction direction, string conveyorId, BuildPlan? ignoring)
    {
        if (!Belts.TryGet(cell, out var belt)
            || belt.Kind != LogisticsKind.Belt
            || Content.FindConveyor(conveyorId)?.Kind != LogisticsKind.Belt
            || State.Builders.Any(b => b.Plans.Any(p =>
                !ReferenceEquals(p, ignoring) && !p.Breaking && Footprint(p.Spec).Contains(cell)))
            || TryGetSiteAt(cell, out _)
            || State.Builders.Any(b => b.Plans.Any(p => p.Breaking && p.Spec.Origin == cell)))
        {
            return BeltOverBelt.None;
        }

        return !string.Equals(belt.Definition.Id, conveyorId, StringComparison.Ordinal) ? BeltOverBelt.Replace
            : belt.Direction == direction ? BeltOverBelt.Same
            : BeltOverBelt.Rotate;
    }

    /// <summary>
    /// Takes the plain belt on <paramref name="cell"/> away without refund for a replacement, returning its spec and
    /// items (front first, positions kept). False, changing nothing, when no plain belt is there.
    /// </summary>
    internal bool TryTakeBeltForReplacement(GridPosition cell, out ReplacedBelt old)
    {
        old = null!;
        if (!Belts.TryGet(cell, out var belt) || belt.Kind != LogisticsKind.Belt)
        {
            return false;
        }

        old = new ReplacedBelt(
            new BuildSpec(belt.Definition.Id, null, cell, belt.Direction),
            belt.Items.Select(i => new TransportedItem(i.Id, i.ItemId, i.Progress, i.Travel)).ToList());
        return RemoveInstant(cell, refund: false);
    }

    /// <summary>
    /// Ends a replacement on the new belt of <paramref name="spec"/>: it takes the first <c>Capacity</c> items of
    /// <paramref name="old"/> with their positions, and the old cost beyond the credit comes back at the old belt's
    /// refund share (<see cref="RefundBasisPointsFor"/>, rounded up once per line).
    /// </summary>
    internal void SettleReplacement(BuildSpec spec, ReplacedBelt old)
    {
        var cell = Belts.Cells[spec.Origin];
        cell.RestoreState(cell.SplitterToggle, old.Items.Take(cell.Definition.Capacity));
        var oldCost = CostOf(old.Spec);
        var credit = Construction.Credit(oldCost, CostOf(spec));
        var basisPoints = RefundBasisPointsFor(old.Spec);
        for (var i = 0; i < oldCost.Count; i++)
        {
            var back = Construction.RefundShare(oldCost[i].Amount - credit[i], basisPoints);
            if (back <= 0)
            {
                continue;
            }

            if (oldCost[i].ItemId is { } itemId)
            {
                State.Storage.AddClamped(itemId, back);
            }
            else
            {
                Wallet.AddMoney(back);
            }
        }
    }

    /// <summary>
    /// Puts the belt of a cancelled replacement back, free, with all its items. The site kept the cell and a loaded
    /// <see cref="ReplacedBelt.Spec"/> is checked (<see cref="IsReplaceableBelt"/>), so this always succeeds; should it
    /// not, the items go to the Core rather than vanish.
    /// </summary>
    internal void RestoreReplacedBelt(ReplacedBelt old)
    {
        if (PlaceInstant(old.Spec, charge: false))
        {
            Belts.Cells[old.Spec.Origin].RestoreState(0, old.Items);
        }
        else
        {
            ItemsToCore(old.Items);
        }
    }

    /// <summary>A replaced belt's spec a site on <paramref name="origin"/> can put back: a known plain belt on that cell.</summary>
    private bool IsReplaceableBelt(BuildSpec old, GridPosition origin) =>
        old.Kind is null && old.Origin == origin && Content.FindConveyor(old.StructureId)?.Kind == LogisticsKind.Belt;

    /// <summary>Puts each item back in the Core's stock, even over its maximum (no sale, not counted as delivered).</summary>
    private void ItemsToCore(IEnumerable<TransportedItem> items)
    {
        foreach (var item in items)
        {
            State.Storage.Return(item.ItemId, 1);
        }
    }

    /// <summary>
    /// Instant replacement of the plain belt under <paramref name="spec"/>, all or nothing: pays the new cost less the
    /// credit (else <see cref="RefusalCode.InsufficientResources"/> with only that difference missing, nothing changed),
    /// gives the rest of the old cost back as <see cref="SettleReplacement"/> does, and keeps the items.
    /// </summary>
    internal Refusal? TryReplaceBeltInstant(BuildSpec spec)
    {
        if (!TryGetSpecAt(spec.Origin, out var oldSpec, out _) || oldSpec.Kind is not null)
        {
            return RefusalCode.Occupied;
        }

        var cost = CostOf(spec);
        var credit = Construction.Credit(cost, CostOf(oldSpec));
        var money = 0;
        var materials = new List<ResourceAmount>();
        for (var i = 0; i < cost.Count; i++)
        {
            var due = cost[i].Amount - credit[i];
            if (cost[i].ItemId is { } itemId)
            {
                if (due > 0)
                {
                    materials.Add(new ResourceAmount(itemId, due));
                }
            }
            else
            {
                money = due;
            }
        }

        // Paid first: a failed charge changes nothing, so an upgrade is never free.
        if (ShortfallOf(money, materials, spec.StructureId) is { } shortfall)
        {
            return shortfall;
        }

        if (!TryCharge(money, materials))
        {
            return new Refusal(RefusalCode.InsufficientResources, spec.StructureId);
        }

        if (!TryTakeBeltForReplacement(spec.Origin, out var old))
        {
            Refund(money, materials, putBack: true);
            return RefusalCode.Occupied;
        }

        if (!PlaceInstant(spec, charge: false))
        {
            // Cannot happen: the cell was just freed. Put the old belt back and the payment too.
            RestoreReplacedBelt(old);
            Refund(money, materials, putBack: true);
            return RefusalCode.Occupied;
        }

        SettleReplacement(spec, old);
        return null;
    }

    /// <summary>Turns the belt on <paramref name="cell"/> in place, keeping its items (the shape cache follows the grid's version).</summary>
    internal bool TryRotateBelt(GridPosition cell, Direction direction) => Belts.TryOrient(cell, direction);

    /// <summary>
    /// Instant auto-junction: takes the belt on <paramref name="tile"/> away and places <paramref name="junction"/>,
    /// paid; the belt is refunded as a removal would. All or nothing: when the junction cannot stand, the belt comes
    /// back and nothing is paid or refunded.
    /// </summary>
    internal bool TryReplaceBeltWithJunction(GridPosition tile, BuildSpec junction)
    {
        if (!TryGetSpecAt(tile, out var belt, out _) || belt.Kind is not null || !RemoveInstant(tile, refund: false))
        {
            return false;
        }

        if (!PlaceInstant(junction, charge: true))
        {
            PlaceInstant(belt with { Origin = tile }, charge: false);
            return false;
        }

        Refund(belt);
        return true;
    }

    public bool TryGetBeltIncomingDirection(GridPosition position, out Direction incoming)
    {
        BeltIncoming().TryGetValue(position, out var entry);
        incoming = entry.Incoming ?? default;
        return entry.Incoming is not null;
    }

    // Shape cache: the client asks per belt item per frame, so the answers are computed once per layout change.
    // JunctionSides: bit (int)Direction set for each connected side of a junction.
    private readonly Dictionary<GridPosition, (Direction? Incoming, BeltShape Shape, int JunctionSides)> beltIncoming = new();
    private readonly Dictionary<GridPosition, (GridPosition Origin, int Size, Direction? Out)> ejectorTiles = new();
    private Func<GridPosition, GridPosition, bool>? buildingFeeds;
    // Tiles of buildings that exchange items with belts (miners, crafters, extractors, generators, storage, the Core): belts
    // blend with them (visual only). Lamps and power nodes have no item I/O.
    private readonly HashSet<GridPosition> blendTiles = new();
    private Func<GridPosition, GridPosition, bool>? buildingBlends;
    private int buildingsVersion;
    private (int Belts, int Buildings, int CoreTier) beltIncomingVersion = (-1, -1, -1);

    private Dictionary<GridPosition, (Direction? Incoming, BeltShape Shape, int JunctionSides)> BeltIncoming()
    {
        // The Core tier too: a finished upgrade (placed through another Factory over the same state) grows the Core.
        var version = (Belts.Version, buildingsVersion, State.CoreTier);
        if (version == beltIncomingVersion)
        {
            return beltIncoming;
        }

        ejectorTiles.Clear();
        foreach (var miner in miners)
        {
            AddEjector(miner.Position, Miner.Size, null);
        }

        foreach (var crafter in smelters)
        {
            AddEjector(crafter.Position, Crafter.Size, null);
        }

        foreach (var crafter in assemblers)
        {
            AddEjector(crafter.Position, Crafter.Size, null);
        }

        foreach (var extractor in extractors)
        {
            AddEjector(extractor.Position, Extractor.Size, extractor.Direction);
        }

        blendTiles.Clear();
        blendTiles.UnionWith(ejectorTiles.Keys);
        blendTiles.UnionWith(CoreTiles);
        foreach (var storage in storages)
        {
            blendTiles.UnionWith(storage.OccupiedTiles());
        }

        foreach (var generator in generators)
        {
            for (var y = 0; y < Generator.Size; y++)
            {
                for (var x = 0; x < Generator.Size; x++)
                {
                    blendTiles.Add(new GridPosition(generator.Position.X + x, generator.Position.Y + y));
                }
            }
        }

        beltIncoming.Clear();
        buildingFeeds ??= BuildingEjectsInto;
        buildingBlends ??= (source, _) => blendTiles.Contains(source);
        foreach (var (position, cell) in Belts.Cells)
        {
            // One feeder lookup per cell gives both the incoming flow and the shape (visual: buildings blend by
            // capability, as in Mindustry, not by whether they eject into this belt right now).
            var (back, left, right) = Belts.Feeders(position, cell.Direction, buildingBlends);
            Direction? incoming = BeltGrid.IncomingFrom(cell.Direction, back, left, right, out var dir) ? dir : null;
            var shape = cell.Kind == LogisticsKind.Belt
                ? BeltGrid.ShapeFrom(back, left, right, Belts.BuildingFeeders(position, cell.Direction, buildingBlends))
                : BeltShape.Straight;
            var sides = 0;
            if (cell.Kind == LogisticsKind.Junction)
            {
                foreach (var side in JunctionSideOrder)
                {
                    sides |= Belts.IsJunctionSideConnected(position, side) ? 1 << (int)side : 0;
                }
            }

            if (incoming is not null || shape != BeltShape.Straight || sides != 0)
            {
                beltIncoming[position] = (incoming, shape, sides);
            }
        }

        beltIncomingVersion = version;
        return beltIncoming;
    }

    private static readonly Direction[] JunctionSideOrder =
        [Direction.North, Direction.East, Direction.South, Direction.West];

    private void AddEjector(GridPosition origin, int size, Direction? output)
    {
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                ejectorTiles[new GridPosition(origin.X + x, origin.Y + y)] = (origin, size, output);
            }
        }
    }

    /// <summary>
    /// True when the building on <paramref name="source"/> would eject items into the logistics cell at
    /// <paramref name="target"/> (an adjacent tile), with the same outward rule the buildings use when ticking.
    /// Only miners, crafters and extractors output items (not the Core, generators, power nodes or lamps).
    /// </summary>
    private bool BuildingEjectsInto(GridPosition source, GridPosition target)
    {
        if (!ejectorTiles.TryGetValue(source, out var building) || !Belts.TryGet(target, out var cell))
        {
            return false;
        }

        var next = target.Step(cell.Direction);
        if (building.Out is { } output && !target.Equals(source.Step(output)))
        {
            return false;
        }

        return !BuildingIo.Occupies(building.Origin, building.Size, next);
    }

    // Read-only wrappers: the view must not be cast back to the live lists.
    public IReadOnlyList<BuilderUnit> Builders => buildersView ??= State.Builders.AsReadOnly();
    public IReadOnlyList<ConstructSite> Sites => sitesView ??= State.Sites.AsReadOnly();
    private IReadOnlyList<BuilderUnit>? buildersView;
    private IReadOnlyList<ConstructSite>? sitesView;

    public bool TryGetSiteAt(GridPosition tile, out ConstructSite site)
    {
        foreach (var candidate in State.Sites)
        {
            if (candidate.Occupies(tile))
            {
                site = candidate;
                return true;
            }
        }

        site = null!;
        return false;
    }

    /// <summary>Plans of <paramref name="playerId"/>'s drones, in drone id then queue order.</summary>
    public IReadOnlyList<BuildPlan> PlansOf(int playerId)
    {
        var plans = new List<BuildPlan>();
        foreach (var builder in State.Builders)
        {
            if (builder.OwnerPlayerId == playerId)
            {
                plans.AddRange(builder.Plans);
            }
        }

        return plans;
    }

    public IReadOnlyList<GridPosition> PlanTiles(BuildPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.Breaking)
        {
            return Footprint(plan.Spec);
        }

        return TryGetSpecAt(plan.Spec.Origin, out var target, out _)
            ? Footprint(target)
            : [plan.Spec.Origin];
    }

    /// <summary>
    /// The site standing for build plan <paramref name="plan"/>: the site under construction with the same spec
    /// (taken apart too, once cancelled). A plan sent to the back of the queue while stalled keeps its site on
    /// the map; null for a breaking plan or a plan not started yet.
    /// </summary>
    public ConstructSite? SiteOfPlan(BuildPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Breaking)
        {
            return null;
        }

        foreach (var site in State.Sites)
        {
            if (site.Spec == plan.Spec)
            {
                return site;
            }
        }

        return null;
    }

    /// <summary>First drone of <paramref name="playerId"/> (lowest id), or null.</summary>
    public BuilderUnit? BuilderOf(int playerId)
    {
        foreach (var builder in State.Builders)
        {
            if (builder.OwnerPlayerId == playerId)
            {
                return builder;
            }
        }

        return null;
    }

    /// <summary>Adds a drone for <paramref name="ownerPlayerId"/> at the centre of the Core (new games).</summary>
    public BuilderUnit AddBuilder(int ownerPlayerId)
    {
        var (x, y) = CoreCentre();
        var builder = new BuilderUnit(nextEntityId++, ownerPlayerId, x, y);
        State.Builders.Add(builder);
        return builder;
    }

    /// <summary>Centre of the Core's cells, in tiles (0, 0 without a Core).</summary>
    public (float X, float Y) CoreCentre()
    {
        if (CoreTiles.Count == 0)
        {
            return (0f, 0f);
        }

        var (minX, minY, maxX, maxY) = CoreBounds();
        return ((minX + maxX + 1) / 2f, (minY + maxY + 1) / 2f);
    }

    /// <summary>
    /// Where an idle drone waits: the centre of the cell one tile west of the Core, in the middle of its
    /// west side (the upper middle cell for an even-sized Core).
    /// </summary>
    public (float X, float Y) BuilderRestPoint()
    {
        if (CoreTiles.Count == 0)
        {
            return (0f, 0f);
        }

        var (minX, minY, _, maxY) = CoreBounds();
        var cellY = minY + (maxY - minY) / 2;
        return (minX - 1 + 0.5f, cellY + 0.5f);
    }

    private (int MinX, int MinY, int MaxX, int MaxY) CoreBounds()
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var t in CoreTiles)
        {
            minX = Math.Min(minX, t.X);
            minY = Math.Min(minY, t.Y);
            maxX = Math.Max(maxX, t.X);
            maxY = Math.Max(maxY, t.Y);
        }

        return (minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Centre of <paramref name="spec"/>'s block, in tiles (a bridge: its entry cell). For a breaking plan
    /// use the clicked cell instead (<see cref="PlanCentre"/>).
    /// </summary>
    public (float X, float Y) SpecCentre(BuildSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var half = FootprintSize(spec) / 2f;
        return (spec.Origin.X + half, spec.Origin.Y + half);
    }

    /// <summary>
    /// Centre of the cells <paramref name="site"/> really covers (a bridge: midway between its ends). The
    /// drone works within range of this point, whichever cell its plan was made on.
    /// </summary>
    public static (float X, float Y) SiteCentre(ConstructSite site)
    {
        ArgumentNullException.ThrowIfNull(site);
        if (site.Tiles.Count == 0)
        {
            return (site.Spec.Origin.X + 0.5f, site.Spec.Origin.Y + 0.5f);
        }

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var t in site.Tiles)
        {
            minX = Math.Min(minX, t.X);
            minY = Math.Min(minY, t.Y);
            maxX = Math.Max(maxX, t.X);
            maxY = Math.Max(maxY, t.Y);
        }

        return ((minX + maxX + 1) / 2f, (minY + maxY + 1) / 2f);
    }

    /// <summary>Where the drone heads for <paramref name="plan"/>: the block's centre, or the clicked cell's when breaking.</summary>
    public (float X, float Y) PlanCentre(BuildPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return plan.Breaking
            ? (plan.Spec.Origin.X + 0.5f, plan.Spec.Origin.Y + 0.5f)
            : SpecCentre(plan.Spec);
    }

    /// <summary>
    /// True when a pending (non-breaking) plan of any player covers <paramref name="tile"/>. Breaking
    /// plans stand on something that exists, so they never reserve cells.
    /// </summary>
    public bool IsPlannedTile(GridPosition tile)
    {
        foreach (var builder in State.Builders)
        {
            foreach (var plan in builder.Plans)
            {
                if (!plan.Breaking && Footprint(plan.Spec).Contains(tile))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Primary / first miner (compat for HUD).</summary>
    public Miner? Miner => miners.Count > 0 ? miners[0] : null;

    public FactorySaveData Capture()
    {
        // The Core's real edge (a hand-made Core of another size is saved as it is and refused on load).
        var coreSize = (int)Math.Round(Math.Sqrt(CoreTiles.Count));

        return new FactorySaveData
        {
            Version = FactorySaveData.CurrentVersion,
            NextItemId = nextItemId,
            NextEntityId = nextEntityId,
            CurrentTick = CurrentTick,
            CoreDeliveredItems = CoreDeliveredItems,
            CoreX = State.CoreOrigin.X,
            CoreY = State.CoreOrigin.Y,
            CoreSize = coreSize,
            CoreTier = CoreTier,
            CoreStartX = State.CoreStart.X,
            CoreStartY = State.CoreStart.Y,
            Money = Wallet.Money,
            Materials = SortedByKey(Wallet.MaterialsSnapshot()),
            UnlockedStructures = Research.UnlockedIds.OrderBy(id => id, StringComparer.Ordinal).ToList(),
            SaleIncome = Session.SaleIncome,
            SoldByItem = SortedByKey(Session.SoldByItem),
            StartingMoney = Session.StartingMoney,
            ActiveCampaignLevelId = ActiveCampaignLevelId,
            Market = State.Exchange.Capture(),
            PowerBuffer = PowerBuffer,
            PowerCapacity = PowerCapacity,
            Seed = Terrain?.Seed ?? 0,
            MapWidth = Terrain?.Width ?? 0,
            MapHeight = Terrain?.Height ?? 0,
            Generator = SavedGenerator(Terrain),
            MapSize = Terrain is { Generator: MapGenerator.Name } biomes ? biomes.Width : 0,
            TerrainHash = Terrain?.Hash,
            MapId = CampaignMapId(Terrain),
            // Canonical order (ids, then belt cells by Y, X) so captures compare byte for byte.
            Miners = miners.OrderBy(m => m.Id).Select(m => new MinerSaveDto
            {
                Id = m.Id,
                X = m.Position.X,
                Y = m.Position.Y,
                Direction = m.Direction.ToString(),
                Progress = m.Progress,
                EjectIndex = m.EjectIndex,
                OutputItemId = m.OutputItemId,
                DefinitionId = m.DefinitionId
            }).ToList(),
            Smelters = smelters.OrderBy(s => s.Id).Select(CaptureCraft).ToList(),
            Assemblers = assemblers.OrderBy(a => a.Id).Select(CaptureCraft).ToList(),
            Generators = generators.OrderBy(g => g.Id).Select(g => new GeneratorSaveDto
            {
                Id = g.Id,
                X = g.Position.X,
                Y = g.Position.Y,
                Direction = g.Direction.ToString(),
                FuelBuffer = g.FuelBuffer,
                BurnRemaining = g.BurnRemaining,
                FuelConsumed = g.FuelConsumed
            }).ToList(),
            Extractors = extractors.OrderBy(e => e.Id).Select(e => new ExtractorSaveDto
            {
                Id = e.Id,
                X = e.Position.X,
                Y = e.Position.Y,
                Direction = e.Direction.ToString(),
                FilterItemId = e.FilterItemId,
                Progress = e.Progress
            }).ToList(),
            PowerNodes = powerNodes.OrderBy(n => n.Id).Select(n => new PowerNodeSaveDto
            {
                Id = n.Id,
                X = n.Position.X,
                Y = n.Position.Y,
                DefinitionId = n.DefinitionId
            }).ToList(),
            Lamps = lamps.Count == 0
                ? null
                : lamps.OrderBy(l => l.Id).Select(l => new LampSaveDto { Id = l.Id, X = l.Position.X, Y = l.Position.Y }).ToList(),
            Storages = storages.Count == 0
                ? null
                : storages.OrderBy(s => s.Id).Select(s => new StorageSaveDto
                {
                    Id = s.Id,
                    Kind = s.Kind.ToString(),
                    X = s.Origin.X,
                    Y = s.Origin.Y,
                    Items = s.ContentsInOrder() is { Count: > 0 } contents
                        ? SortedByKey(contents.Select(c => new KeyValuePair<string, int>(c.ItemId, c.Count)))
                        : null
                }).ToList(),
            Rules = new GameRulesSaveDto
            {
                RefundPercent = State.Rules.RefundPercent,
                InstantBuild = State.Rules.InstantBuild,
                // Until the v16 save (DayNight and FirstBuildTick), only always-night is kept, as the old night flag.
                Night = State.Rules.DayNight == DayNightMode.AlwaysNight
            },
            Builders = State.Builders.OrderBy(b => b.Id).Select(b => new BuilderSaveDto
            {
                Id = b.Id,
                Owner = b.OwnerPlayerId,
                X = b.X,
                Y = b.Y,
                Rotation = b.Rotation,
                CurrentSiteId = b.CurrentSiteId,
                Plans = b.Plans.Select(p => new BuildPlanSaveDto { Spec = CaptureSpec(p.Spec), Breaking = p.Breaking }).ToList()
            }).ToList(),
            Sites = State.Sites.OrderBy(site => site.Id).Select(site => new SiteSaveDto
            {
                Id = site.Id,
                Spec = CaptureSpec(site.Spec),
                Tiles = site.Tiles.Select(t => new GridCellSaveDto { X = t.X, Y = t.Y }).ToList(),
                Progress = site.Progress,
                SpentByItem = CaptureCostLines(site.Cost, site.Spent),
                RefundedByItem = CaptureCostLines(site.Cost, site.Refunded),
                StalledTicks = site.StalledTicks,
                Deconstructing = site.Deconstructing,
                RefundBasisPoints = site.RefundBasisPoints,
                Owner = site.OwnerPlayerId,
                Replaced = site.Replaced is { } old
                    ? new ReplacedSaveDto { Spec = CaptureSpec(old.Spec), Items = old.Items.Select(CaptureItem).ToList() }
                    : null
            }).ToList(),
            // BeltGrid.Cells already enumerates in canonical (Y, X) order.
            Belts = Belts.Cells.Values.Select(c => new BeltSaveDto
            {
                X = c.Position.X,
                Y = c.Position.Y,
                Direction = c.Direction.ToString(),
                DefinitionId = c.Definition.Id,
                SplitterToggle = c.SplitterToggle,
                BridgePartnerX = c.BridgePartner?.X,
                BridgePartnerY = c.BridgePartner?.Y,
                FilterItemId = c.Kind == LogisticsKind.Sorter ? c.FilterItemId : null,
                Items = c.Items.Select(CaptureItem).ToList()
            }).ToList()
        };
    }

    private static ItemSaveDto CaptureItem(TransportedItem it) => new()
    {
        Id = it.Id,
        ItemId = it.ItemId,
        Progress = it.Progress,
        Travel = it.Travel?.ToString()
    };

    private static TransportedItem RestoreItem(ItemSaveDto it) => new(
        it.Id,
        it.ItemId,
        it.Progress,
        string.IsNullOrWhiteSpace(it.Travel) ? null : ParseDirection(it.Travel));

    private static BuildSpecSaveDto CaptureSpec(BuildSpec spec) => new()
    {
        StructureId = spec.StructureId,
        Kind = spec.Kind?.ToString(),
        X = spec.Origin.X,
        Y = spec.Origin.Y,
        Direction = spec.Direction.ToString(),
        RecipeId = spec.RecipeId,
        FilterItemId = spec.FilterItemId
    };

    /// <summary>One line per cost item, money (no item id) first, then materials in ordinal id order.</summary>
    private static List<CostLineSaveDto> CaptureCostLines(IReadOnlyList<CostEntry> cost, IReadOnlyList<int> values) =>
        cost.Select((entry, i) => new CostLineSaveDto { ItemId = entry.ItemId, Amount = values[i] })
            .OrderBy(line => line.ItemId is not null)
            .ThenBy(line => line.ItemId, StringComparer.Ordinal)
            .ToList();

    /// <summary>Saved per-item lines of a site (none when absent).</summary>
    private static List<(string? ItemId, int Amount)> SavedCostLines(List<CostLineSaveDto>? byItem) =>
        (byItem ?? []).Select(line => (line.ItemId, line.Amount)).ToList();

    /// <summary>Amount of the saved line for <paramref name="itemId"/> (0 when the save has none).</summary>
    private static int SavedAmount(List<(string? ItemId, int Amount)> lines, string? itemId)
    {
        foreach (var line in lines)
        {
            if (string.Equals(line.ItemId, itemId, StringComparison.Ordinal))
            {
                return line.Amount;
            }
        }

        return 0;
    }

    private static BuildSpec RestoreSpec(BuildSpecSaveDto dto) => new(
        dto.StructureId,
        Enum.TryParse<BuildingKind>(dto.Kind, ignoreCase: true, out var kind) ? kind : null,
        new GridPosition(dto.X, dto.Y),
        ParseDirection(dto.Direction),
        dto.RecipeId,
        dto.FilterItemId);

    private static Dictionary<string, int> SortedByKey(IEnumerable<KeyValuePair<string, int>> source)
    {
        // Fresh dictionary filled in ordinal key order: enumerates (and serializes) in that order.
        var sorted = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var kv in source.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            sorted[kv.Key] = kv.Value;
        }

        return sorted;
    }

    private static CraftSaveDto CaptureCraft(Crafter craft) => new()
    {
        Id = craft.Id,
        X = craft.Position.X,
        Y = craft.Position.Y,
        Direction = craft.Direction.ToString(),
        RecipeId = craft.Recipe.Id,
        DefinitionId = craft.DefinitionId,
        Progress = craft.Progress,
        IsCrafting = craft.IsCrafting,
        InputBuffer = SortedByKey(craft.InputBuffer),
        OutputQueue = craft.OutputQueue.ToList(),
        EjectIndex = craft.EjectIndex,
        ItemsCrafted = craft.ItemsCrafted,
        FuelBuffer = craft.FuelBuffer,
        BurnRemaining = craft.BurnRemaining
    };

    /// <summary>
    /// Rebuilds a factory from a save; the terrain is regenerated with the saved generator, or read from the
    /// campaign map file in <paramref name="mapsDirectory"/>.
    /// </summary>
    /// <exception cref="SaveMapException">The terrain cannot be rebuilt (code, argument and Italian detail).</exception>
    public static Factory Restore(FactoryContent content, FactorySaveData data, string? mapsDirectory = null)
    {
        var belts = new BeltGrid();
        foreach (var cell in data.Belts)
        {
            var def = content.FindConveyor(cell.DefinitionId)
                ?? content.RequireConveyor("conveyor-basic");
            var dir = ParseDirection(cell.Direction);
            var items = cell.Items.Select(RestoreItem).ToList();
            GridPosition? partner = null;
            if (cell.BridgePartnerX is { } bx && cell.BridgePartnerY is { } by)
            {
                partner = new GridPosition(bx, by);
            }

            // A bridge the current rules would not allow (older saves reached 5 cells) is not placed, both ends.
            if (def.Kind == LogisticsKind.Bridge
                && (partner is not { } end || !BeltGrid.IsBridgeSpan(new GridPosition(cell.X, cell.Y), end)))
            {
                continue;
            }

            belts.TryRestore(
                new GridPosition(cell.X, cell.Y),
                dir,
                def,
                cell.SplitterToggle,
                items,
                partner,
                cell.FilterItemId);
        }

        if (data.CoreTier < 0 || data.CoreTier >= content.CoreTiers.Count)
        {
            throw new InvalidDataException($"Livello del CORE {data.CoreTier} inesistente.");
        }

        if (data.CoreSize != content.CoreTiers[data.CoreTier].Size)
        {
            throw new InvalidDataException(
                $"CORE di lato {data.CoreSize} al livello {data.CoreTier} (lato {content.CoreTiers[data.CoreTier].Size}).");
        }

        if (data.Market is null)
        {
            throw new InvalidDataException("Salvataggio senza mercato.");
        }

        var core = CoreStockSink.MakeCoreTiles(new GridPosition(data.CoreX, data.CoreY), data.CoreSize);
        var wallet = new EconomyWallet(data.Money, data.Materials);
        var research = ResearchState.FromSaved(data.UnlockedStructures, content);
        var session = new EconomySession(data.StartingMoney);
        session.Restore(data.StartingMoney, data.SaleIncome, data.SoldByItem);
        var factory = new Factory(content, belts, core, wallet, research, session)
        {
            nextItemId = Math.Max(1, data.NextItemId),
            CurrentTick = Math.Max(0, data.CurrentTick),
            CoreDeliveredItems = Math.Max(0, data.CoreDeliveredItems),
            ActiveCampaignLevelId = data.ActiveCampaignLevelId
        };
        // The cells are the saved ones (CoreSize, checked against the tier above); the tier and start are restored here.
        factory.State.CoreTier = data.CoreTier;
        factory.State.CoreStart = new GridPosition(data.CoreStartX, data.CoreStartY);
        factory.State.Rules = new GameRules(
            Math.Clamp(data.Rules.RefundPercent, 0, 100),
            data.Rules.InstantBuild,
            data.Rules.Night ? DayNightMode.AlwaysNight : DayNightMode.Cycle);
        // Capacity first, then the buffer clamped to it: SetPowerBuffer would clamp to the core-only
        // capacity and silently drop stored energy above it.
        if (data.PowerCapacity > 0f)
        {
            factory.PowerCapacity = Math.Max(CorePowerCapacity, data.PowerCapacity);
        }

        factory.PowerBuffer = Math.Clamp(data.PowerBuffer, 0f, factory.PowerCapacity);

        // Before the sites: restoring a site checks the terrain under it. The terrain was generated around the
        // starting shard, whatever the Core has grown into since.
        factory.Terrain = RestoreTerrain(content, data, StartingShard(content, data), mapsDirectory);
        CheckRegeneratedTerrain(data, factory.Terrain);

        // After the terrain: its setter reseeds the market.
        factory.State.Exchange.Restore(data.Market, content.MarketRules, factory.CurrentTick);

        // Defensive: a building without a stable id (0) gets a fresh one in list order.
        var maxSavedId = data.Miners.Select(d => d.Id)
            .Concat(data.Smelters.Select(d => d.Id))
            .Concat(data.Assemblers.Select(d => d.Id))
            .Concat(data.Generators.Select(d => d.Id))
            .Concat(data.Extractors.Select(d => d.Id))
            .Concat(data.PowerNodes.Select(d => d.Id))
            .Concat((data.Lamps ?? []).Select(d => d.Id))
            .Concat((data.Storages ?? []).Select(d => d.Id))
            .Concat(data.Builders.Select(d => d.Id))
            .Concat(data.Sites.Select(d => d.Id))
            .DefaultIfEmpty(0)
            .Max();
        factory.nextEntityId = Math.Max(Math.Max(1, data.NextEntityId), maxSavedId + 1);
        long RestoredId(long savedId) => savedId > 0 ? savedId : factory.nextEntityId++;

        foreach (var m in data.Miners)
        {
            var miner = new Miner(
                new GridPosition(m.X, m.Y),
                ParseDirection(m.Direction),
                outputItemId: m.OutputItemId,
                definitionId: m.DefinitionId,
                id: RestoredId(m.Id));
            miner.RestoreProgress(m.Progress, m.EjectIndex);
            factory.miners.Add(miner);
        }

        foreach (var s in data.Smelters)
        {
            factory.smelters.Add(RestoreCraft(content, s, Crafter.SmelterBuildingId, RestoredId(s.Id)));
        }

        foreach (var a in data.Assemblers)
        {
            factory.assemblers.Add(RestoreCraft(content, a, Crafter.AssemblerBuildingId, RestoredId(a.Id)));
        }

        foreach (var g in data.Generators)
        {
            var gen = new Generator(new GridPosition(g.X, g.Y), ParseDirection(g.Direction), RestoredId(g.Id));
            gen.RestoreFuel(g.FuelBuffer, g.BurnRemaining, g.FuelConsumed);
            factory.generators.Add(gen);
        }

        foreach (var e in data.Extractors)
        {
            var ex = new Extractor(
                new GridPosition(e.X, e.Y),
                ParseDirection(e.Direction),
                e.FilterItemId,
                RestoredId(e.Id));
            ex.RestoreProgress(e.Progress);
            factory.extractors.Add(ex);
        }

        foreach (var n in data.PowerNodes)
        {
            factory.powerNodes.Add(new PowerNode(new GridPosition(n.X, n.Y), n.DefinitionId, RestoredId(n.Id)));
        }

        foreach (var l in data.Lamps ?? [])
        {
            factory.lamps.Add(new Lamp(new GridPosition(l.X, l.Y), RestoredId(l.Id)));
        }

        foreach (var s in data.Storages ?? [])
        {
            if (!Enum.TryParse<BuildingKind>(s.Kind, out var kind) || !BuildSpec.IsStorage(kind))
            {
                throw new InvalidDataException($"Magazzino di tipo sconosciuto: '{s.Kind}'.");
            }

            var storage = factory.NewStorage(kind, new GridPosition(s.X, s.Y), RestoredId(s.Id));
            foreach (var (itemId, count) in (s.Items ?? []).OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                storage.Return(itemId, count);
            }

            factory.storages.Add(storage);
        }

        foreach (var b in data.Builders.OrderBy(b => b.Id))
        {
            var builder = new BuilderUnit(b.Id, b.Owner, b.X, b.Y)
            {
                Rotation = b.Rotation,
                CurrentSiteId = b.CurrentSiteId
            };
            builder.PlanQueue.AddRange(b.Plans.Select(p => new BuildPlan(RestoreSpec(p.Spec), p.Breaking)));
            factory.State.Builders.Add(builder);
        }

        foreach (var d in data.Sites.OrderBy(d => d.Id))
        {
            factory.RestoreSite(d);
        }

        // A drone whose current site is gone starts its first plan again.
        foreach (var builder in factory.State.Builders)
        {
            if (builder.CurrentSiteId is { } siteId && !factory.State.Sites.Any(site => site.Id == siteId))
            {
                builder.CurrentSiteId = null;
            }
        }

        SortById(factory.miners, m => m.Id);
        SortById(factory.smelters, s => s.Id);
        SortById(factory.assemblers, a => a.Id);
        SortById(factory.generators, g => g.Id);
        SortById(factory.extractors, e => e.Id);
        SortById(factory.powerNodes, n => n.Id);
        SortById(factory.lamps, l => l.Id);
        SortById(factory.storages, s => s.Id);
        // Which storage blocks touch the Core (the saved Core cells and tier are set above).
        factory.State.Storage.RecomputeLinks();
        factory.buildingsVersion++; // buildings were added straight to the lists: drop any cached corners
        return factory;
    }

    /// <summary>Generator name stored in a save for <paramref name="terrain"/> (see <see cref="FactorySaveData.Generator"/>).</summary>
    internal static string SavedGenerator(TerrainMap? terrain) =>
        terrain is null ? ClassicTerrainGenerator.Name
        : CampaignMapId(terrain) is not null ? FactorySaveData.CampaignGenerator
        : terrain.Generator;

    internal static string? CampaignMapId(TerrainMap? terrain) =>
        terrain is not null && terrain.Generator.StartsWith(CampaignMapFile.GeneratorPrefix, StringComparison.Ordinal)
            ? terrain.Generator[CampaignMapFile.GeneratorPrefix.Length..]
            : null;

    /// <summary>
    /// Terrain of a save (null when it has none): the classic or biome generator from seed and size, or the
    /// campaign map file, which must still have the saved size.
    /// </summary>
    private static TerrainMap? RestoreTerrain(
        FactoryContent content,
        FactorySaveData data,
        (GridPosition Origin, int Size) shard,
        string? mapsDirectory)
    {
        var (core, coreSize) = shard;
        var coreTiles = CoreStockSink.MakeCoreTiles(core, coreSize);
        switch (data.Generator)
        {
            case ClassicTerrainGenerator.Name:
            {
                if (data.MapWidth < 12 || data.MapHeight < 8)
                {
                    return null;
                }

                var w = Math.Min(data.MapWidth, MaxMapEdge);
                var h = Math.Min(data.MapHeight, MaxMapEdge);
                return TerrainMap.Generate(w, h, data.Seed, coreTiles, ResolveStarterDeposit(core, coreSize, w, h));
            }

            case MapGenerator.Name:
            case MapGenerator.UnversionedName:
            {
                var edge = data.MapSize;
                if (edge < 8 || edge > MaxMapEdge || data.MapWidth != edge || data.MapHeight != edge)
                {
                    throw new SaveMapException(SaveLoadError.MapInvalidSize, $"{data.MapWidth}×{data.MapHeight}",
                        $"Mappa del salvataggio non valida: misura {data.MapSize} per {data.MapWidth}×{data.MapHeight}.");
                }

                return MapGenerator.Generate(
                    MapGenSettings.Normal((MapSize)edge, data.Seed),
                    coreTiles,
                    ResolveStarterDeposit(core, coreSize, edge, edge));
            }

            case FactorySaveData.CampaignGenerator:
                return LoadCampaignTerrain(content, data, mapsDirectory);

            default:
                throw new SaveMapException(SaveLoadError.UnknownGenerator, data.Generator,
                    $"Generatore di mappa sconosciuto nel salvataggio: '{data.Generator}'.");
        }
    }

    /// <summary>
    /// The Core the game started with (origin and edge), from which the terrain generators made the map: the saved
    /// start (<see cref="FactorySaveData.CoreStartX"/>, <see cref="FactorySaveData.CoreStartY"/>) with the shard's edge.
    /// </summary>
    internal static (GridPosition Origin, int Size) StartingShard(FactoryContent content, FactorySaveData data) =>
        (new GridPosition(data.CoreStartX, data.CoreStartY), content.CoreTiers[0].Size);

    /// <summary>
    /// A terrain regenerated from a generator (classic or biomes) must be the saved one: a generator changed
    /// after the save would otherwise put the factory on other walls, water and deposits. Campaign maps are
    /// files and keep their own rule (same size; an edited map loads). Saves without a hash are not checked.
    /// </summary>
    private static void CheckRegeneratedTerrain(FactorySaveData data, TerrainMap? terrain)
    {
        if (data.TerrainHash is not { } saved || terrain is null
            || data.Generator == FactorySaveData.CampaignGenerator || terrain.Hash == saved)
        {
            return;
        }

        throw new SaveMapException(SaveLoadError.TerrainChanged, data.Generator,
            $"Il terreno rigenerato non corrisponde al salvataggio (generatore '{data.Generator}', seme {data.Seed}): "
            + "la partita è di una versione con un generatore di mappe diverso.");
    }

    /// <summary>The campaign map file of a save; it must still have the saved size.</summary>
    private static TerrainMap LoadCampaignTerrain(FactoryContent content, FactorySaveData data, string? mapsDirectory)
    {
        if (string.IsNullOrWhiteSpace(data.MapId))
        {
            throw new SaveMapException(SaveLoadError.CampaignMapUnnamed, null, "Salvataggio della campagna senza il nome della mappa.");
        }

        // A plain file name inside the maps folder, never a path (hand-edited saves): the map files' own rule.
        try
        {
            CampaignMapFile.CheckId(data.MapId);
        }
        catch (ArgumentException ex)
        {
            throw new SaveMapException(SaveLoadError.CampaignMapBadName, data.MapId,
                $"Nome della mappa della campagna non valido: '{data.MapId}'.", ex);
        }

        if (mapsDirectory is null)
        {
            throw new SaveMapException(SaveLoadError.MapsFolderMissing, data.MapId,
                $"Mappa della campagna '{data.MapId}' non disponibile: cartella delle mappe non trovata.");
        }

        TerrainMap map;
        try
        {
            map = CampaignMapFile.Load(mapsDirectory, data.MapId, content, out _);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            throw new SaveMapException(SaveLoadError.CampaignMapMissing, data.MapId,
                $"Mappa della campagna '{data.MapId}' mancante o non valida. {ex.Message}", ex);
        }

        if (map.Width != data.MapWidth || map.Height != data.MapHeight)
        {
            throw new SaveMapException(SaveLoadError.CampaignMapChanged, data.MapId,
                $"La mappa della campagna '{data.MapId}' è cambiata: {map.Width}×{map.Height} invece di "
                + $"{data.MapWidth}×{data.MapHeight}.");
        }

        return map;
    }

    /// <summary>
    /// Restores one saved site, validating what a hand-edited or content-drifted save could carry: progress
    /// within [0, 1], refund share within [0, 10000] basis points, spent/refunded within [0, cost] and mapped
    /// by item id, cells recomputed when none were saved. A site whose spec is no longer buildable, or whose
    /// cells are outside the map, on a wall or on deep water, or a miner site on ore its tier cannot dig, is dropped
    /// with its plan, and what it took from
    /// the Core goes back (its refund share, less what was refunded).
    /// </summary>
    private void RestoreSite(SiteSaveDto d)
    {
        var spec = RestoreSpec(d.Spec);
        var refundBasisPoints = Math.Clamp(d.RefundBasisPoints, 0, Construction.FullRefundBasisPoints);
        var savedCost = CostOf(spec);
        var spent = SavedCostLines(d.SpentByItem);
        var refunded = SavedCostLines(d.RefundedByItem);
        // Cost is recomputed; cells are restored as saved (a bridge keeps its chosen exit).
        var tiles = !IsValidSpec(spec) ? null
            : d.Tiles.Count > 0 ? d.Tiles.Select(t => new GridPosition(t.X, t.Y)).ToList()
            : Footprint(spec);
        // A T1 site on titanium only (saved before the ore-hardness rule) could never finish: dropped the same way.
        var tooWeak = tiles is not null
            && !d.Deconstructing
            && spec.Kind is BuildingKind.Miner or BuildingKind.MinerAdvanced
            && MinerTooWeak(spec.Origin, spec.StructureId);
        // A replacement site must be able to put its belt back (content may have dropped or changed that conveyor).
        var replaced = d.Replaced is { } r
            ? new ReplacedBelt(RestoreSpec(r.Spec), r.Items.Select(RestoreItem).ToList())
            : null;
        var badReplaced = replaced is not null && !IsReplaceableBelt(replaced.Spec, spec.Origin);
        if (tiles is null || tiles.Any(TerrainBlocks) || tooWeak || badReplaced)
        {
            // The belt a dropped replacement site held is gone: its items go to the Core.
            if (replaced is not null)
            {
                ItemsToCore(replaced.Items);
            }

            foreach (var (itemId, amount) in spent)
            {
                var back = Construction.RefundShare(amount, refundBasisPoints)
                    - Math.Max(0, SavedAmount(refunded, itemId));
                Credit(itemId, back);
            }

            DropPlansOfSite(d.Id, spec);
            return;
        }

        // Spent lines the recomputed cost no longer has (plates of a site saved before raw-ore costs) are not
        // lost: what was spent and not yet refunded goes back to the Core.
        foreach (var (itemId, amount) in spent)
        {
            if (!savedCost.Any(c => string.Equals(c.ItemId, itemId, StringComparison.Ordinal)))
            {
                Credit(itemId, Math.Max(0, amount) - Math.Max(0, SavedAmount(refunded, itemId)));
            }
        }

        var site = new ConstructSite(d.Id, spec, tiles, savedCost, d.Owner)
        {
            Progress = float.IsNaN(d.Progress) ? 0f : Math.Clamp(d.Progress, 0f, 1f),
            Deconstructing = d.Deconstructing,
            RefundBasisPoints = refundBasisPoints,
            StalledTicks = Math.Clamp(d.StalledTicks, 0, Construction.StuckTicks),
            Replaced = replaced
        };
        for (var i = 0; i < site.Cost.Count; i++)
        {
            var amount = site.Cost[i].Amount;
            site.SpentLines[i] = Math.Clamp(SavedAmount(spent, site.Cost[i].ItemId), 0, amount);
            site.RefundedLines[i] = Math.Clamp(SavedAmount(refunded, site.Cost[i].ItemId), 0, amount);
        }

        State.Sites.Add(site);

        void Credit(string? itemId, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            // Pieces taken from the Core and never delivered: they come back whole, even above the maximum.
            if (itemId is null)
            {
                Wallet.AddMoney(amount);
            }
            else
            {
                State.Storage.Return(itemId, amount);
            }
        }
    }

    /// <summary>Removes the plan of a dropped site: the current plan of the drone working on it, else a waiting build plan for it.</summary>
    private void DropPlansOfSite(long siteId, BuildSpec spec)
    {
        foreach (var builder in State.Builders)
        {
            if (builder.CurrentSiteId == siteId)
            {
                builder.CurrentSiteId = null;
                if (builder.PlanQueue.Count > 0)
                {
                    builder.PlanQueue.RemoveAt(0);
                }

                return;
            }
        }

        foreach (var builder in State.Builders)
        {
            var index = builder.PlanQueue.FindIndex(plan => !plan.Breaking && plan.Spec == spec);
            if (index >= 0)
            {
                builder.PlanQueue.RemoveAt(index);
                return;
            }
        }
    }

    /// <summary>Systems iterate buildings in ascending id order (stable sort keeps list order on ties).</summary>
    private static void SortById<T>(List<T> list, Func<T, long> id)
    {
        var sorted = list.OrderBy(id).ToList();
        list.Clear();
        list.AddRange(sorted);
    }

    private static Crafter RestoreCraft(FactoryContent content, CraftSaveDto dto, string fallbackId, long id)
    {
        var recipe = content.FindRecipe(dto.RecipeId)
            ?? content.FindRecipe(fallbackId == Crafter.AssemblerBuildingId
                ? "craft-copper-wire"
                : "smelt-iron")
            ?? throw new InvalidDataException($"Ricetta '{dto.RecipeId}' mancante nel save.");
        var buildingId = string.IsNullOrWhiteSpace(dto.DefinitionId) ? fallbackId : dto.DefinitionId;
        var available = buildingId == Crafter.AssemblerBuildingId
            ? content.AssemblerRecipes()
            : content.SmelterRecipes();
        var craft = new Crafter(
            new GridPosition(dto.X, dto.Y),
            ParseDirection(dto.Direction),
            recipe,
            buildingId,
            available,
            id);
        craft.RestoreCraftState(
            dto.Progress,
            dto.IsCrafting,
            dto.InputBuffer,
            dto.OutputQueue,
            dto.EjectIndex,
            dto.ItemsCrafted,
            dto.FuelBuffer,
            dto.BurnRemaining);
        return craft;
    }

    private static Direction ParseDirection(string? value) =>
        Enum.TryParse<Direction>(value, ignoreCase: true, out var dir) ? dir : Direction.East;

    /// <summary>Phase C seed: miner → belts → forno → belts → core (plates stock).</summary>
    public static Factory CreatePhaseCDemo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var beltDef = content.RequireConveyor(beltId);
        var recipe = content.FindRecipe("smelt-iron")
            ?? throw new InvalidDataException("Ricetta smelt-iron mancante.");
        var grid = new BeltGrid();
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), content.CoreTiers[0].Size);
        var factory = new Factory(content, grid, core);
        factory.UnlockAllForDemo();

        // Miner 2×2 at (2,7); forno 2×2 at (6,7).
        factory.TryPlaceMiner(new GridPosition(2, 7), Direction.East);
        factory.TryPlaceSmelter(new GridPosition(6, 7), Direction.East, recipe);
        SeedSmelterFuel(factory);

        // Feed: (4,8)(5,8) → into forno west edge (6,8).
        grid.PlacePath(
        [
            new GridPosition(4, 8),
            new GridPosition(5, 8)
        ], beltDef);
        // Orient last feed cell into smelter.
        grid.TryOrient(new GridPosition(5, 8), Direction.East);

        // Output: east of forno then L south into core.
        grid.PlacePath(
        [
            new GridPosition(8, 8),
            new GridPosition(9, 8),
            new GridPosition(10, 8),
            new GridPosition(10, 9),
            new GridPosition(10, 10),
            new GridPosition(10, 11),
            new GridPosition(10, 12),
            new GridPosition(10, 13)
        ], beltDef);

        return factory;
    }

    /// <summary>
    /// Phase D seed: iron miner→forno→plates + copper miner → assemblatore
    /// (<c>craft-copper-wire</c>) → wire → core.
    /// </summary>
    public static Factory CreatePhaseDDemo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var beltDef = content.RequireConveyor(beltId);
        var smelt = content.FindRecipe("smelt-iron")
            ?? throw new InvalidDataException("Ricetta smelt-iron mancante.");
        var wire = content.FindRecipe("craft-copper-wire")
            ?? throw new InvalidDataException("Ricetta craft-copper-wire mancante.");
        var grid = new BeltGrid();
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), content.CoreTiers[0].Size);
        var factory = new Factory(content, grid, core);
        factory.UnlockAllForDemo();

        // Iron miner (2,7) → forno (6,7) → plates east toward assembler (12,7).
        factory.TryPlaceMiner(new GridPosition(2, 7), Direction.East, "iron-ore");
        factory.TryPlaceSmelter(new GridPosition(6, 7), Direction.East, smelt);
        factory.TryPlaceAssembler(new GridPosition(12, 7), Direction.East, wire);
        // Copper miner south of iron row.
        factory.TryPlaceMiner(new GridPosition(2, 10), Direction.East, "copper-ore");
        SeedSmelterFuel(factory);

        // Iron ore feed into forno.
        grid.PlacePath(
        [
            new GridPosition(4, 8),
            new GridPosition(5, 8)
        ], beltDef);
        grid.TryOrient(new GridPosition(5, 8), Direction.East);

        // Plates: forno east → into assembler west edge (12,8).
        grid.PlacePath(
        [
            new GridPosition(8, 8),
            new GridPosition(9, 8),
            new GridPosition(10, 8),
            new GridPosition(11, 8)
        ], beltDef);
        grid.TryOrient(new GridPosition(11, 8), Direction.East);

        // Copper ore: miner → east then north into assembler south edge (12,9).
        grid.PlacePath(
        [
            new GridPosition(4, 11),
            new GridPosition(5, 11),
            new GridPosition(6, 11),
            new GridPosition(7, 11),
            new GridPosition(8, 11),
            new GridPosition(9, 11),
            new GridPosition(10, 11),
            new GridPosition(11, 11),
            new GridPosition(12, 11),
            new GridPosition(12, 10),
            new GridPosition(12, 9)
        ], beltDef);
        grid.TryOrient(new GridPosition(12, 9), Direction.North);

        // Wire out: assembler east (14,8) → south → west into core north (10,13).
        grid.PlacePath(
        [
            new GridPosition(14, 8),
            new GridPosition(14, 9),
            new GridPosition(14, 10),
            new GridPosition(14, 11),
            new GridPosition(14, 12),
            new GridPosition(14, 13),
            new GridPosition(13, 13),
            new GridPosition(12, 13),
            new GridPosition(11, 13),
            new GridPosition(10, 13)
        ], beltDef);
        grid.TryOrient(new GridPosition(10, 13), Direction.South);

        return factory;
    }

    /// <summary>
    /// Phase E seed: Phase D craft loop + junction pass-through on plates + splitter fork on wire.
    /// </summary>
    public static Factory CreatePhaseEDemo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var beltDef = content.RequireConveyor(beltId);
        var junctionDef = content.RequireConveyor("junction");
        var splitterDef = content.RequireConveyor("splitter");
        var smelt = content.FindRecipe("smelt-iron")
            ?? throw new InvalidDataException("Ricetta smelt-iron mancante.");
        var wire = content.FindRecipe("craft-copper-wire")
            ?? throw new InvalidDataException("Ricetta craft-copper-wire mancante.");
        var grid = new BeltGrid();
        var core = CoreStockSink.MakeCoreTiles(new GridPosition(9, 14), content.CoreTiers[0].Size);
        var factory = new Factory(content, grid, core);
        factory.UnlockAllForDemo();

        factory.TryPlaceMiner(new GridPosition(2, 7), Direction.East, "iron-ore");
        factory.TryPlaceSmelter(new GridPosition(6, 7), Direction.East, smelt);
        factory.TryPlaceAssembler(new GridPosition(12, 7), Direction.East, wire);
        factory.TryPlaceMiner(new GridPosition(2, 10), Direction.East, "copper-ore");
        SeedSmelterFuel(factory);

        // Iron ore → forno.
        grid.PlacePath([new GridPosition(4, 8), new GridPosition(5, 8)], beltDef);
        grid.TryOrient(new GridPosition(5, 8), Direction.East);

        // Plates → junction (10,8) pass-through → (11,8) into assembler.
        grid.PlacePath([new GridPosition(8, 8), new GridPosition(9, 8)], beltDef);
        grid.TryOrient(new GridPosition(9, 8), Direction.East);
        Require(grid.TryPlaceFree(new GridPosition(10, 8), Direction.East, junctionDef), "junction seed");
        grid.PlacePath([new GridPosition(11, 8)], beltDef);
        grid.TryOrient(new GridPosition(11, 8), Direction.East);

        // Copper → assembler south (same corridor as Phase D).
        grid.PlacePath(
        [
            new GridPosition(4, 11),
            new GridPosition(5, 11),
            new GridPosition(6, 11),
            new GridPosition(7, 11),
            new GridPosition(8, 11),
            new GridPosition(9, 11),
            new GridPosition(10, 11),
            new GridPosition(11, 11),
            new GridPosition(12, 11),
            new GridPosition(12, 10),
            new GridPosition(12, 9)
        ], beltDef);
        grid.TryOrient(new GridPosition(12, 9), Direction.North);

        // Wire → splitter T-fork facing South.
        grid.PlacePath([new GridPosition(14, 8), new GridPosition(14, 9)], beltDef);
        grid.TryOrient(new GridPosition(14, 9), Direction.South);
        Require(grid.TryPlaceFree(new GridPosition(14, 10), Direction.South, splitterDef), "splitter seed");

        // West arm → core (route east of copper column).
        grid.PlacePath(
        [
            new GridPosition(13, 10),
            new GridPosition(13, 11),
            new GridPosition(13, 12),
            new GridPosition(13, 13),
            new GridPosition(12, 13),
            new GridPosition(11, 13),
            new GridPosition(10, 13)
        ], beltDef);
        grid.TryOrient(new GridPosition(10, 13), Direction.South);

        // East arm dead-end (proves fork).
        grid.PlacePath([new GridPosition(15, 10), new GridPosition(16, 10)], beltDef);
        grid.TryOrient(new GridPosition(16, 10), Direction.East);

        return factory;
    }

    /// <summary>
    /// Phase F seed: Phase E loop + coal miner → generator adjacent to forno (powered craft).
    /// </summary>
    public static Factory CreatePhaseFDemo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var factory = CreatePhaseEDemo(content, beltId);
        var beltDef = factory.BeltDefinition;

        // Generator north of forno (6,7) → adjacent on south edge.
        Require(factory.TryPlaceGenerator(new GridPosition(6, 5), Direction.East), "generator seed");
        // Coal miner west of generator.
        Require(factory.TryPlaceMiner(new GridPosition(2, 4), Direction.East, "coal"), "coal miner");
        factory.Belts.PlacePath(
        [
            new GridPosition(4, 5),
            new GridPosition(5, 5)
        ], beltDef);
        factory.Belts.TryOrient(new GridPosition(5, 5), Direction.East);

        return factory;
    }

    /// <summary>
    /// Sorter + bridge demo: Phase F loop + Mindustry sorter fork + thin bridge span (decision 14).
    /// </summary>
    public static Factory CreateSorterBridgeDemo(FactoryContent content, string beltId = "conveyor-basic")
    {
        var factory = CreatePhaseFDemo(content, beltId);
        var beltDef = factory.BeltDefinition;

        // Sorter corridor (north of craft loop): feed → sorter → match east / overflow N+S.
        factory.Belts.PlacePath(
        [
            new GridPosition(16, 2),
            new GridPosition(17, 2)
        ], beltDef);
        factory.Belts.TryOrient(new GridPosition(17, 2), Direction.East);
        Require(factory.TryPlaceSorter(new GridPosition(18, 2), Direction.East), "sorter seed");
        factory.Belts.PlacePath([new GridPosition(19, 2), new GridPosition(20, 2)], beltDef);
        factory.Belts.TryOrient(new GridPosition(20, 2), Direction.East);
        factory.Belts.PlacePath([new GridPosition(18, 1)], beltDef); // overflow left of East = North
        factory.Belts.TryOrient(new GridPosition(18, 1), Direction.North);
        factory.Belts.PlacePath([new GridPosition(18, 3)], beltDef); // overflow right of East = South
        factory.Belts.TryOrient(new GridPosition(18, 3), Direction.South);

        // Bridge span 2 over mid (16,12)→(18,12); mid stays empty for thin-span visual.
        // Underpass: belt corridor south of mid (proves mid freeness without covering the span).
        Require(factory.TryPlaceBridge(new GridPosition(16, 12), Direction.East), "bridge seed");
        factory.Belts.PlacePath(
        [
            new GridPosition(15, 12)
        ], beltDef);
        factory.Belts.TryOrient(new GridPosition(15, 12), Direction.East);
        factory.Belts.PlacePath(
        [
            new GridPosition(19, 12),
            new GridPosition(20, 12)
        ], beltDef);
        factory.Belts.TryOrient(new GridPosition(20, 12), Direction.East);
        // Parallel underpass corridor (y=13) — mid (17,12) remains empty for decision-14 thin span.
        factory.Belts.PlacePath(
        [
            new GridPosition(16, 13),
            new GridPosition(17, 13),
            new GridPosition(18, 13)
        ], beltDef);
        factory.Belts.TryOrient(new GridPosition(18, 13), Direction.East);

        return factory;
    }

    public bool CanOccupy(GridPosition position) => CanOccupyFootprint(position, 1);

    public bool CanOccupyFootprint(GridPosition origin, int size)
    {
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var tile = new GridPosition(origin.X + x, origin.Y + y);
                if (tile.X < 0 || tile.Y < 0 || TerrainBlocks(tile))
                {
                    return false;
                }

                if (CoreTiles.Contains(tile) || Belts.Contains(tile) || IsBuildingTile(tile))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// True when the terrain forbids building on <paramref name="tile"/>: outside the map, a wall or deep
    /// water. Without a terrain (demo factory) nothing blocks.
    /// </summary>
    private bool TerrainBlocks(GridPosition tile) =>
        Terrain is { } terrain && (!terrain.InBounds(tile) || terrain[tile].BlocksBuilding);

    /// <summary>
    /// Refusal for building on <paramref name="cells"/>, in this order: Core, wall, deep water,
    /// otherwise <see cref="RefusalCode.Occupied"/>.
    /// </summary>
    public RefusalCode RefusalReason(IEnumerable<GridPosition> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        var list = cells as IReadOnlyCollection<GridPosition> ?? cells.ToList();
        if (list.Any(CoreTiles.Contains))
        {
            return RefusalCode.OnCore;
        }

        if (Terrain is { } terrain)
        {
            if (list.Any(c => terrain.InBounds(c) && terrain[c].Wall != WallKind.None))
            {
                return RefusalCode.Wall;
            }

            if (list.Any(c => terrain.InBounds(c) && terrain[c].Floor == FloorKind.DeepWater))
            {
                return RefusalCode.DeepWater;
            }
        }

        return RefusalCode.Occupied;
    }

    /// <summary>True when a building or a construction site covers <paramref name="tile"/>.</summary>
    public bool IsBuildingTile(GridPosition tile)
    {
        foreach (var site in State.Sites)
        {
            if (site.Occupies(tile))
            {
                return true;
            }
        }

        foreach (var miner in miners)
        {
            foreach (var t in miner.OccupiedTiles())
            {
                if (t.Equals(tile))
                {
                    return true;
                }
            }
        }

        foreach (var craft in CraftMachines())
        {
            if (craft.Occupies(tile))
            {
                return true;
            }
        }

        foreach (var gen in generators)
        {
            if (gen.Occupies(tile))
            {
                return true;
            }
        }

        foreach (var ex in extractors)
        {
            if (ex.Occupies(tile))
            {
                return true;
            }
        }

        foreach (var node in powerNodes)
        {
            if (node.Occupies(tile))
            {
                return true;
            }
        }

        foreach (var lamp in lamps)
        {
            if (lamp.Occupies(tile))
            {
                return true;
            }
        }

        foreach (var storage in storages)
        {
            if (storage.Occupies(tile))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Unlock all placeables so seeded demos keep working under gates.</summary>
    public void UnlockAllForDemo()
    {
        foreach (var id in ResearchState.DemoStructureIds)
        {
            Research.ForceUnlock(id);
        }

        // Seed layouts call TryPlace* (now charged) — stock enough for demo footprints.
        EnsureDemoBuildStock();
    }

    /// <summary>Demos without a live generator: top up forno coal so craft can run.</summary>
    public static void SeedSmelterFuel(Factory factory, int units = Crafter.FuelBufferCapacity)
    {
        foreach (var sm in factory.Smelters)
        {
            sm.SeedFuel(units);
        }
    }

    public bool IsStructureUnlocked(string structureId) => Research.IsUnlocked(structureId);

    public bool TryUnlockStructure(string structureId)
    {
        var structure = Content.FindStructure(structureId);
        return structure is not null && Research.TryUnlock(structure, Wallet);
    }

    /// <summary>Sells stocked materials at the price of the moment (see <see cref="MarketSales.Sell"/>).</summary>
    public bool TrySellFromWallet(string itemId, int amount, int playerId) =>
        amount > 0 && State.Exchange.Find(itemId) is { } item
        && MarketSales.Sell(State, item, amount, sellAll: false, playerId) is not null;

    /// <summary>Sells the whole stock of one item (the "Vendi tutto" button).</summary>
    public bool TrySellAll(string itemId, int playerId) =>
        SellAllWaitTicks() == 0 && State.Exchange.Find(itemId) is { } item && Wallet.MaterialCount(itemId) is var stock and > 0
        && MarketSales.Sell(State, item, stock, sellAll: true, playerId) is not null;

    /// <summary>Ticks left before "sell all" works again after a missed jackpot attempt (0 when it is free).</summary>
    public long SellAllWaitTicks() => Math.Max(0, State.Exchange.SellAllBlockedUntilTick - State.CurrentTick);

    public IReadOnlyList<MarketItemView> MarketItems => State.Exchange.Items.Select(ViewOf).ToArray();

    public MarketItemView? MarketItem(string itemId) => State.Exchange.Find(itemId) is { } item ? ViewOf(item) : null;

    public int SellAllCooldownSeconds => (int)((SellAllWaitTicks() + SimulationClock.TickRate - 1) / SimulationClock.TickRate);

    private MarketItemView ViewOf(MarketItemState item) => new(
        item.ItemId, item.BaseValue, item.PriceMilli, MarketMath.UnitIncome(item.BaseValue, item.PriceMilli), item.Phase,
        item.Phase == MarketPhase.Crash
            ? (int)Math.Max(0, (item.CrashEndTick - CurrentTick + SimulationClock.TickRate - 1) / SimulationClock.TickRate)
            : 0,
        item.AutoSell, item.AutoSellWhenFull, item.HistoryMilli.ToArray());

    // ---- Core upgrade (the next tier's block, placed over the Core) ----

    /// <summary>
    /// The block that upgrades the Core to the next tier, where the growth rule puts it: the foundation at the shard's
    /// origin (one more column right, one more row down), the nucleus one cell up and left of the foundation's
    /// origin (one more row up, one more column left: centred on the starting shard). Null at the last tier.
    /// </summary>
    public BuildSpec? CoreUpgradeSpec()
    {
        var next = CoreTier + 1;
        if (next >= Content.CoreTiers.Count || next > 2)
        {
            return null;
        }

        var kind = next == 1 ? BuildingKind.CoreFoundation : BuildingKind.CoreNucleus;
        var origin = State.CoreOrigin;
        if (next == 2)
        {
            origin = new GridPosition(origin.X - 1, origin.Y - 1);
        }

        return new BuildSpec(BuildSpec.StructureIdFor(kind)!, kind, origin, Direction.East);
    }

    /// <summary>The cells the next upgrade adds to the Core, row by row (empty at the last tier).</summary>
    public IReadOnlyList<GridPosition> CoreUpgradeNewTiles() =>
        CoreUpgradeSpec() is { } spec ? Footprint(spec) : [];

    /// <summary>
    /// Why the Core cannot be upgraded now, in this order: <see cref="RefusalCode.CoreMaxTier"/>,
    /// <see cref="RefusalCode.CoreUpgradeInProgress"/> (a plan or a site under construction),
    /// <see cref="RefusalCode.CoreUpgradeLocked"/> (with the structure id), <see cref="RefusalCode.CoreUpgradeOutOfMap"/>,
    /// <see cref="RefusalCode.CoreUpgradeBlocked"/> (a new cell taken, planned or not buildable); null when it can be
    /// placed. Resources are not checked: the site waits for them (instant build checks them on placing).
    /// </summary>
    public Refusal? CoreUpgradeRefusal()
    {
        if (CoreUpgradeSpec() is not { } spec)
        {
            return RefusalCode.CoreMaxTier;
        }

        if (IsCoreUpgradeUnderWay())
        {
            return RefusalCode.CoreUpgradeInProgress;
        }

        if (!IsStructureUnlocked(spec.StructureId))
        {
            return new Refusal(RefusalCode.CoreUpgradeLocked, spec.StructureId);
        }

        if (CorePlacementRefusal(spec) is { } placement)
        {
            return placement;
        }

        return Footprint(spec).Any(IsPlannedTile) ? RefusalCode.CoreUpgradeBlocked : null;
    }

    /// <summary>True when a Core upgrade is planned (not breaking) or its site is being built.</summary>
    private bool IsCoreUpgradeUnderWay()
    {
        foreach (var site in State.Sites)
        {
            if (!site.Deconstructing && BuildSpec.IsCoreUpgrade(site.Spec.Kind))
            {
                return true;
            }
        }

        foreach (var builder in State.Builders)
        {
            foreach (var plan in builder.Plans)
            {
                if (!plan.Breaking && BuildSpec.IsCoreUpgrade(plan.Spec.Kind))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Where a Core block may stand: null when <paramref name="spec"/> is the next upgrade (<see cref="CoreUpgradeSpec"/>)
    /// and its new cells are inside the map and free (no Core, building, belt, site, wall or deep water); otherwise
    /// <see cref="RefusalCode.CoreMaxTier"/>, <see cref="RefusalCode.CoreUpgradeBlocked"/> (wrong block or origin, a
    /// cell taken) or <see cref="RefusalCode.CoreUpgradeOutOfMap"/>. Plans are not looked at (the drone's own plan
    /// covers the cells when it starts the site).
    /// </summary>
    private RefusalCode? CorePlacementRefusal(BuildSpec spec)
    {
        if (CoreUpgradeSpec() is not { } next)
        {
            return RefusalCode.CoreMaxTier;
        }

        if (spec.Kind != next.Kind || spec.Origin != next.Origin)
        {
            return RefusalCode.CoreUpgradeBlocked;
        }

        var tiles = Footprint(next);
        if (tiles.Any(t => t.X < 0 || t.Y < 0 || (Terrain is { } terrain && !terrain.InBounds(t))))
        {
            return RefusalCode.CoreUpgradeOutOfMap;
        }

        return tiles.All(CanOccupy) ? null : RefusalCode.CoreUpgradeBlocked;
    }

    /// <summary>The Core tier whose block or structure id is <paramref name="structureId"/> (1 or 2), or -1.</summary>
    private int CoreTierFor(string structureId)
    {
        for (var tier = 1; tier < Content.CoreTiers.Count; tier++)
        {
            if (string.Equals(Content.CoreTiers[tier].Id, structureId, StringComparison.Ordinal))
            {
                return tier;
            }
        }

        return -1;
    }

    /// <summary>
    /// Finishes a Core upgrade: the Core takes the new tier's size at <paramref name="spec"/>'s origin (its stock stays,
    /// maximum, auto-sell rate and drone follow the tier) and linked storage is recomputed. With
    /// <paramref name="charge"/> the tier's build cost is paid first (fails, unchanged, when the Core is short).
    /// </summary>
    private bool PlaceCoreUpgrade(BuildSpec spec, bool charge)
    {
        if (CorePlacementRefusal(spec) is not null)
        {
            return false;
        }

        var tier = BuildSpec.CoreTierOf(spec.Kind);
        if (charge && !TryCharge(0, Content.CoreTiers[tier].BuildCost))
        {
            return false;
        }

        // SetCore recomputes the Core's cells and the linked storage blocks.
        State.SetCore(spec.Origin, tier);
        buildingsVersion++;
        return true;
    }

    /// <summary>Longest map edge (the "enorme" normal map); campaign and restored maps are clamped to it.</summary>
    public const int MaxMapEdge = 2000;

    public static GridPosition CenteredCoreOrigin(int mapWidth, int mapHeight, int coreSize = 3) =>
        new(
            Math.Max(0, (Math.Max(coreSize, mapWidth) - coreSize) / 2),
            Math.Max(0, (Math.Max(coreSize, mapHeight) - coreSize) / 2));

    public static (int Width, int Height) ResolveCampaignMapSize(CampaignLevelDefinition level)
    {
        var w = Math.Max(12, level.MapWidth);
        var h = Math.Max(8, level.MapHeight);
        w = Math.Min(w, MaxMapEdge);
        h = Math.Min(h, MaxMapEdge);
        return (w, h);
    }

    internal static GridPosition ResolveStarterDeposit(
        GridPosition coreOrigin, int coreSize, int mapWidth, int mapHeight)
    {
        // Prefer west of core (Raylib-style), clamped into map.
        var x = Math.Clamp(coreOrigin.X - Miner.Size - 2, 1, Math.Max(1, mapWidth - Miner.Size - 1));
        var y = Math.Clamp(coreOrigin.Y, 1, Math.Max(1, mapHeight - Miner.Size - 1));
        // Avoid overlapping the core footprint.
        if (x + Miner.Size > coreOrigin.X && x < coreOrigin.X + coreSize
            && y + Miner.Size > coreOrigin.Y && y < coreOrigin.Y + coreSize)
        {
            x = Math.Max(1, coreOrigin.X - Miner.Size - 1);
        }

        return new GridPosition(x, y);
    }

    public static string? StructureIdForTool(string toolKey) => toolKey switch
    {
        "belt" => "conveyor-basic",
        "belt-fast" => "conveyor-fast",
        "belt-express" => "conveyor-express",
        "belt-armored" => "conveyor-armored",
        "miner" => "miner",
        "miner-advanced" => "miner-advanced",
        "smelter" => "smelter",
        "assembler" => "assembler",
        "junction" => "junction",
        "splitter" => "splitter",
        "generator" => "generator",
        "sorter" => "sorter",
        "bridge" => "conveyor-bridge",
        "extractor" => "extractor",
        "power-node" => "power-node",
        "power-node-t2" => "power-node-t2",
        "lamp" => Lamp.BuildingId,
        "container" => "container",
        "vault" => "vault",
        _ => null
    };

    private bool RequireUnlocked(string structureId) => Research.IsUnlocked(structureId);

    /// <summary>True if wallet covers money + materials for a structure (bridge = ×2 heads).</summary>
    public bool CanAffordStructure(string structureId, int multiplicity = 1)
    {
        multiplicity = Math.Max(1, multiplicity);
        if (Content.FindBuilding(structureId) is { } building)
        {
            return Wallet.CanAfford(
                building.MoneyCost * multiplicity,
                ScaleAmounts(building.BuildCost, multiplicity));
        }

        if (Content.FindConveyor(structureId) is { } conveyor)
        {
            return Wallet.CanAfford(
                conveyor.MoneyCost * multiplicity,
                ScaleAmounts(conveyor.EffectiveBuildCost, multiplicity));
        }

        if (CoreTierFor(structureId) is var tier and > 0)
        {
            return Wallet.CanAfford(0, Content.CoreTiers[tier].BuildCost);
        }

        return true;
    }

    /// <summary>
    /// What is missing to build <paramref name="structureId"/> <paramref name="multiplicity"/> times: a
    /// <see cref="RefusalCode.InsufficientResources"/> refusal with the missing money and materials, or null when
    /// affordable (or unknown).
    /// </summary>
    public Refusal? Shortfall(string structureId, int multiplicity = 1)
    {
        multiplicity = Math.Max(1, multiplicity);
        int money;
        IReadOnlyList<ResourceAmount> mats;
        if (Content.FindBuilding(structureId) is { } building)
        {
            money = building.MoneyCost * multiplicity;
            mats = ScaleAmounts(building.BuildCost, multiplicity);
        }
        else if (Content.FindConveyor(structureId) is { } conveyor)
        {
            money = conveyor.MoneyCost * multiplicity;
            mats = ScaleAmounts(conveyor.EffectiveBuildCost, multiplicity);
        }
        else if (CoreTierFor(structureId) is var tier and > 0)
        {
            money = 0;
            mats = Content.CoreTiers[tier].BuildCost;
        }
        else
        {
            return null;
        }

        return ShortfallOf(money, mats, structureId);
    }

    /// <summary>Missing money and materials to pay <paramref name="money"/> and <paramref name="mats"/>; null when affordable.</summary>
    private Refusal? ShortfallOf(int money, IReadOnlyList<ResourceAmount> mats, string? structureId)
    {
        if (Wallet.CanAfford(money, mats))
        {
            return null;
        }

        var missing = new List<ResourceAmount>();
        foreach (var entry in mats)
        {
            var have = Wallet.MaterialCount(entry.ItemId);
            if (have < entry.Amount)
            {
                missing.Add(new ResourceAmount(entry.ItemId, entry.Amount - have));
            }
        }

        return new Refusal(RefusalCode.InsufficientResources, structureId, Math.Max(0, money - Wallet.Money), missing);
    }

    private bool TryCharge(int money, IReadOnlyList<ResourceAmount> materials) =>
        Wallet.TrySpend(money, materials);

    /// <summary>
    /// Gives back the <paramref name="refundPercent"/> share of a cost. A refund (removal) enters the Core up to the
    /// maximum (<see cref="CoreStorage.AddClamped"/>); with <paramref name="putBack"/> it is a payment taken back by a
    /// failed operation and enters whole (<see cref="CoreStorage.Return"/>).
    /// </summary>
    private void Refund(int money, IReadOnlyList<ResourceAmount> materials, int refundPercent = 100, bool putBack = false)
    {
        var pct = Math.Clamp(refundPercent, 0, 100);
        if (pct <= 0)
        {
            return;
        }

        Wallet.AddMoney(Construction.RefundShare(money, pct * 100));
        foreach (var entry in materials)
        {
            var amount = Construction.RefundShare(entry.Amount, pct * 100);
            if (amount <= 0)
            {
                continue;
            }

            if (putBack)
            {
                State.Storage.Return(entry.ItemId, amount);
            }
            else
            {
                State.Storage.AddClamped(entry.ItemId, amount);
            }
        }
    }

    private bool TryChargeConveyor(ConveyorDefinition def, int multiplicity = 1)
    {
        multiplicity = Math.Max(1, multiplicity);
        return TryCharge(
            def.MoneyCost * multiplicity,
            ScaleAmounts(def.EffectiveBuildCost, multiplicity));
    }

    private void RefundConveyor(ConveyorDefinition def, int multiplicity = 1, bool putBack = false) =>
        Refund(
            def.MoneyCost * Math.Max(1, multiplicity),
            ScaleAmounts(def.EffectiveBuildCost, Math.Max(1, multiplicity)),
            refundPercent: 100,
            putBack);

    private bool TryChargeBuilding(string buildingId)
    {
        var cost = Content.FindBuilding(buildingId);
        return cost is null || TryCharge(cost.MoneyCost, cost.BuildCost);
    }

    private void RefundBuilding(string buildingId)
    {
        var cost = Content.FindBuilding(buildingId);
        if (cost is null)
        {
            return;
        }

        Refund(cost.MoneyCost, cost.BuildCost, cost.RefundPercent);
    }

    private static IReadOnlyList<ResourceAmount> ScaleAmounts(
        IReadOnlyList<ResourceAmount> amounts, int multiplicity)
    {
        if (multiplicity == 1 || amounts.Count == 0)
        {
            return amounts;
        }

        var scaled = new ResourceAmount[amounts.Count];
        for (var i = 0; i < amounts.Count; i++)
        {
            scaled[i] = new ResourceAmount(amounts[i].ItemId, amounts[i].Amount * multiplicity);
        }

        return scaled;
    }

    /// <summary>Demo / self-test stock so seed layouts can place without a prior Mercato loop.</summary>
    public void EnsureDemoBuildStock(
        int ironPlates = 200,
        int copperWire = 80,
        int copperOre = 40,
        int money = 0,
        int logistics = 200)
    {
        void TopUp(string itemId, int target)
        {
            var need = Math.Max(0, target - Wallet.MaterialCount(itemId));
            if (need > 0)
            {
                Wallet.AddMaterial(itemId, need);
            }
        }

        TopUp("iron-plate", ironPlates);
        TopUp("copper-wire", copperWire);
        TopUp("copper-ore", copperOre);
        // Logistics, miners and smelters are paid in raw ore (Mindustry's copper/lead/titanium), the top belts also
        // in graphite and silicon.
        foreach (var itemId in new[] { "iron-ore", "lead-ore", "titanium-ore", "graphite", "silicon" })
        {
            TopUp(itemId, logistics);
        }

        if (money > Wallet.Money)
        {
            Wallet.AddMoney(money - Wallet.Money);
        }
    }

    public bool TryPlaceBelt(GridPosition position, Direction direction, string? conveyorId = null)
    {
        var id = string.IsNullOrWhiteSpace(conveyorId) ? BuildSpec.DefaultConveyorId : conveyorId;
        if (!RequireUnlocked(id))
        {
            return false;
        }

        var def = Content.FindConveyor(id) ?? Content.RequireConveyor(BuildSpec.DefaultConveyorId);
        return PlaceBelt(position, direction, def, charge: true);
    }

    public bool TryPlaceJunction(GridPosition position, Direction direction) =>
        RequireUnlocked("junction")
        && PlaceLogistics(position, direction, JunctionDefinition, charge: true);

    public bool TryPlaceSplitter(GridPosition position, Direction direction) =>
        RequireUnlocked("splitter")
        && PlaceLogistics(position, direction, SplitterDefinition, charge: true);

    public bool TryPlaceSorter(GridPosition position, Direction direction, string? filterItemId = null) =>
        RequireUnlocked("sorter")
        && PlaceLogistics(position, direction, SorterDefinition, charge: true, filterItemId);

    public bool TryPlaceBridge(GridPosition entry, Direction direction) =>
        RequireUnlocked("conveyor-bridge")
        && PlaceBridge(entry, direction, charge: true, exit: null);

    public bool TryRemoveBelt(GridPosition position)
    {
        if (!Belts.TryGet(position, out var cell))
        {
            return false;
        }

        var def = cell.Definition;
        var multiplicity = cell.Kind == LogisticsKind.Bridge ? 2 : 1;
        if (!Belts.TryRemove(position))
        {
            return false;
        }

        RefundConveyor(def, multiplicity);
        return true;
    }

    public bool TryPlaceMiner(
        GridPosition origin,
        Direction direction,
        string outputItemId = Miner.DefaultOutputItemId,
        string definitionId = Miner.BasicId)
    {
        var id = string.IsNullOrWhiteSpace(definitionId) ? Miner.BasicId : definitionId;
        return RequireUnlocked(id) && PlaceMiner(origin, direction, outputItemId, id, charge: true);
    }

    public bool TryPlaceSmelter(GridPosition origin, Direction direction, RecipeDefinition? recipe = null) =>
        RequireUnlocked(Crafter.SmelterBuildingId)
        && PlaceCrafter(origin, direction, recipe, Crafter.SmelterBuildingId, charge: true);

    public bool TryPlaceAssembler(GridPosition origin, Direction direction, RecipeDefinition? recipe = null) =>
        RequireUnlocked(Crafter.AssemblerBuildingId)
        && PlaceCrafter(origin, direction, recipe, Crafter.AssemblerBuildingId, charge: true);

    public bool TryPlaceGenerator(GridPosition origin, Direction direction = Direction.East) =>
        RequireUnlocked(Generator.BuildingId) && PlaceGenerator(origin, direction, charge: true);

    public bool TryPlaceExtractor(
        GridPosition origin,
        Direction direction,
        string filterItemId = Extractor.DefaultFilterItemId) =>
        RequireUnlocked(Extractor.BuildingId) && PlaceExtractor(origin, direction, filterItemId, charge: true);

    public bool TryPlacePowerNode(GridPosition origin, string definitionId = PowerNode.Tier1Id)
    {
        var id = definitionId == PowerNode.Tier2Id ? PowerNode.Tier2Id : PowerNode.Tier1Id;
        return RequireUnlocked(id) && PlacePowerNode(origin, id, charge: true);
    }

    public bool TryPlaceLamp(GridPosition origin) =>
        RequireUnlocked(Lamp.BuildingId) && PlaceLamp(origin, charge: true);

    public bool TryRemoveBuildingAt(GridPosition tile)
    {
        if (!TryRemoveBuildingEntity(tile, out var buildingId))
        {
            return false;
        }

        RefundBuilding(buildingId);
        return true;
    }

    // ---- Spec primitives (construction sites, commands) ----

    /// <summary>Cells <paramref name="spec"/> covers: its square, or both ends of a bridge.</summary>
    public IReadOnlyList<GridPosition> Footprint(BuildSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.Kind == BuildingKind.Bridge)
        {
            // An existing bridge covers its own ends; a new one ends on the first free cell in range.
            if (Belts.TryGet(spec.Origin, out var cell)
                && cell.Kind == LogisticsKind.Bridge
                && cell.BridgePartner is { } partner)
            {
                return [spec.Origin, partner];
            }

            return FindBridgeExit(spec.Origin, spec.Direction) is { } exit
                ? [spec.Origin, exit]
                : [spec.Origin];
        }

        var size = FootprintSize(spec);
        var tiles = new List<GridPosition>(size * size);
        // A Core block covers only the cells it adds: its square less the previous tier's Core, which the growth
        // rule puts at the same origin (foundation over the shard) or one cell down and right (nucleus over the
        // foundation). Fixed by the spec alone, so a plan, its site and a saved site agree.
        var previous = (Origin: default(GridPosition), Size: 0);
        if (BuildSpec.CoreTierOf(spec.Kind) is var tier and > 0)
        {
            var shift = tier >= 2 ? 1 : 0;
            previous = (new GridPosition(spec.Origin.X + shift, spec.Origin.Y + shift), Content.CoreTiers[tier - 1].Size);
        }

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var tile = new GridPosition(spec.Origin.X + x, spec.Origin.Y + y);
                if (previous.Size > 0
                    && tile.X >= previous.Origin.X && tile.X < previous.Origin.X + previous.Size
                    && tile.Y >= previous.Origin.Y && tile.Y < previous.Origin.Y + previous.Size)
                {
                    continue;
                }

                tiles.Add(tile);
            }
        }

        return tiles;
    }

    /// <summary>
    /// Whole cost of <paramref name="spec"/> (bridge = both ends): money first when &gt; 0, then
    /// materials in ordinal id order.
    /// </summary>
    public IReadOnlyList<CostEntry> CostOf(BuildSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return Construction.CostOf(Content, spec);
    }

    /// <summary>
    /// Share of the cost given back when the finished <paramref name="spec"/> is taken apart, in basis
    /// points (0–10000): the game's <see cref="GameRules.RefundPercent"/> × the building's own refund
    /// percent. Each cost line then gives back <c>ceil(amount × basisPoints / 10000)</c> (rounded up once, <see cref="Construction.RefundShare"/>).
    /// </summary>
    public int RefundBasisPointsFor(BuildSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var own = spec.Kind is not null && Content.FindBuilding(spec.StructureId) is { } building
            ? building.RefundPercent
            : 100;
        return Math.Clamp(State.Rules.RefundPercent, 0, 100) * Math.Clamp(own, 0, 100);
    }

    /// <summary>
    /// True when <paramref name="spec"/> names something buildable: a known conveyor (belt specs), the
    /// structure id of its kind, and for forno / assembler a recipe of that machine (or none).
    /// </summary>
    public bool IsValidSpec(BuildSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.Kind is not { } kind)
        {
            return Content.FindConveyor(spec.StructureId) is not null;
        }

        if (!string.Equals(BuildSpec.StructureIdFor(kind), spec.StructureId, StringComparison.Ordinal))
        {
            return false;
        }

        if (spec.RecipeId is { } recipeId && kind is BuildingKind.Smelter or BuildingKind.Assembler)
        {
            return FindRecipeFor(kind, recipeId) is not null;
        }

        return true;
    }

    private RecipeDefinition? FindRecipeFor(BuildingKind kind, string recipeId)
    {
        var available = kind == BuildingKind.Smelter ? Content.SmelterRecipes() : Content.AssemblerRecipes();
        return available.FirstOrDefault(r => string.Equals(r.Id, recipeId, StringComparison.Ordinal));
    }

    /// <summary>
    /// True when <paramref name="spec"/> is valid (<see cref="IsValidSpec"/>) and its cells are free
    /// (no Core, building, belt, site, wall or deep water, inside the map; a miner needs an ore it can dig or
    /// no ore at all, see <see cref="MinerTooWeak"/>). Does not check research or resources.
    /// <paramref name="reason"/> is the refusal (null when placeable).
    /// </summary>
    public bool CanPlace(BuildSpec spec, out RefusalCode? reason)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (!IsValidSpec(spec))
        {
            reason = RefusalCode.InvalidCommand;
            return false;
        }

        if (BuildSpec.IsCoreUpgrade(spec.Kind))
        {
            reason = CorePlacementRefusal(spec);
            return reason is null;
        }

        var ok = spec.Kind switch
        {
            null or BuildingKind.Junction or BuildingKind.Splitter or BuildingKind.Sorter =>
                !Belts.Contains(spec.Origin) && CanOccupy(spec.Origin),
            BuildingKind.Bridge =>
                !Belts.Contains(spec.Origin)
                && CanOccupy(spec.Origin)
                && FindBridgeExit(spec.Origin, spec.Direction) is not null,
            _ => CanOccupyFootprint(spec.Origin, FootprintSize(spec))
        };
        if (ok && spec.Kind is BuildingKind.Miner or BuildingKind.MinerAdvanced
            && MinerTooWeak(spec.Origin, spec.StructureId))
        {
            reason = RefusalCode.NeedsAdvancedMiner;
            return false;
        }

        reason = ok ? null : RefusalReason(spec);
        return ok;
    }

    /// <summary>
    /// Refusal for <paramref name="spec"/> (see <see cref="RefusalReason(IEnumerable{GridPosition})"/>).
    /// A bridge whose entry is free but has no exit says why: a wall or deep water on the exit cells, when that
    /// is what blocks them; otherwise "occupied".
    /// </summary>
    public RefusalCode RefusalReason(BuildSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (BuildSpec.IsCoreUpgrade(spec.Kind))
        {
            return CorePlacementRefusal(spec) ?? RefusalCode.CoreUpgradeBlocked;
        }

        var reason = RefusalReason(Footprint(spec));
        if (reason == RefusalCode.Occupied
            && spec.Kind is BuildingKind.Miner or BuildingKind.MinerAdvanced
            && CanOccupyFootprint(spec.Origin, Miner.Size)
            && MinerTooWeak(spec.Origin, spec.StructureId))
        {
            return RefusalCode.NeedsAdvancedMiner;
        }

        if (reason == RefusalCode.Occupied
            && spec.Kind == BuildingKind.Bridge
            && !Belts.Contains(spec.Origin)
            && CanOccupy(spec.Origin)
            && FindBridgeExit(spec.Origin, spec.Direction) is null)
        {
            var exits = new List<GridPosition>();
            for (var span = BeltGridCell.MinBridgeSpan; span <= BeltGridCell.MaxBridgeSpan; span++)
            {
                exits.Add(spec.Origin.Step(spec.Direction, span));
            }

            var exitReason = RefusalReason(exits);
            if (exitReason is RefusalCode.Wall or RefusalCode.DeepWater)
            {
                return exitReason;
            }
        }

        return reason;
    }

    /// <summary>
    /// Places <paramref name="spec"/> now. With <paramref name="charge"/> it pays like the matching
    /// <c>TryPlace…</c> (fails, unchanged, when the wallet is short); without it nothing is paid.
    /// Returns false, changing nothing, when the spec is invalid or its cells are not. Research is not checked.
    /// </summary>
    public bool PlaceInstant(BuildSpec spec, bool charge) => PlaceInstant(spec, charge, bridgeExit: null);

    /// <summary><see cref="PlaceInstant(BuildSpec, bool)"/> with a bridge forced to end on <paramref name="bridgeExit"/>.</summary>
    internal bool PlaceInstant(BuildSpec spec, bool charge, GridPosition? bridgeExit)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (!IsValidSpec(spec))
        {
            return false;
        }

        var origin = spec.Origin;
        var dir = spec.Direction;
        switch (spec.Kind)
        {
            case null:
                return Content.FindConveyor(spec.StructureId) is { } conveyor
                    && PlaceBelt(origin, dir, conveyor, charge);
            case BuildingKind.Junction:
                return PlaceLogistics(origin, dir, JunctionDefinition, charge);
            case BuildingKind.Splitter:
                return PlaceLogistics(origin, dir, SplitterDefinition, charge);
            case BuildingKind.Sorter:
                return PlaceLogistics(origin, dir, SorterDefinition, charge, spec.FilterItemId);
            case BuildingKind.Bridge:
                return PlaceBridge(origin, dir, charge, bridgeExit);
            case BuildingKind.Miner:
                return PlaceMiner(origin, dir, Miner.DefaultOutputItemId, Miner.BasicId, charge);
            case BuildingKind.MinerAdvanced:
                return PlaceMiner(origin, dir, Miner.DefaultOutputItemId, Miner.AdvancedId, charge);
            case BuildingKind.Smelter:
            case BuildingKind.Assembler:
            {
                var buildingId = spec.Kind == BuildingKind.Smelter
                    ? Crafter.SmelterBuildingId
                    : Crafter.AssemblerBuildingId;
                // IsValidSpec guarantees a non-null RecipeId names a recipe of this machine.
                var recipe = spec.RecipeId is { } recipeId ? FindRecipeFor(spec.Kind.Value, recipeId) : null;

                return PlaceCrafter(origin, dir, recipe, buildingId, charge);
            }
            case BuildingKind.Generator:
                return PlaceGenerator(origin, dir, charge);
            case BuildingKind.Extractor:
                return PlaceExtractor(origin, dir, spec.FilterItemId ?? Extractor.DefaultFilterItemId, charge);
            case BuildingKind.PowerNode:
                return PlacePowerNode(origin, PowerNode.Tier1Id, charge);
            case BuildingKind.PowerNodeT2:
                return PlacePowerNode(origin, PowerNode.Tier2Id, charge);
            case BuildingKind.Lamp:
                return PlaceLamp(origin, charge);
            case BuildingKind.CoreFoundation:
            case BuildingKind.CoreNucleus:
                return PlaceCoreUpgrade(spec, charge);
            case BuildingKind.Container:
            case BuildingKind.Vault:
                return PlaceStorage(spec, charge);
            default:
                return false;
        }
    }

    /// <summary>
    /// The spec of the building or belt piece on <paramref name="tile"/> (a bridge reads from its entry)
    /// and its entity id (0 for belt pieces, which have none).
    /// </summary>
    public bool TryGetSpecAt(GridPosition tile, out BuildSpec spec, out long entityId)
    {
        foreach (var m in miners)
        {
            if (m.OccupiedTiles().Contains(tile))
            {
                var kind = m.DefinitionId == Miner.AdvancedId ? BuildingKind.MinerAdvanced : BuildingKind.Miner;
                spec = new BuildSpec(m.DefinitionId, kind, m.Position, m.Direction);
                entityId = m.Id;
                return true;
            }
        }

        foreach (var c in CraftMachines())
        {
            if (c.Occupies(tile))
            {
                var kind = c.IsAssembler ? BuildingKind.Assembler : BuildingKind.Smelter;
                spec = new BuildSpec(c.DefinitionId, kind, c.Position, c.Direction, c.Recipe.Id);
                entityId = c.Id;
                return true;
            }
        }

        foreach (var g in generators)
        {
            if (g.Occupies(tile))
            {
                spec = new BuildSpec(Generator.BuildingId, BuildingKind.Generator, g.Position, g.Direction);
                entityId = g.Id;
                return true;
            }
        }

        foreach (var e in extractors)
        {
            if (e.Occupies(tile))
            {
                spec = new BuildSpec(
                    Extractor.BuildingId, BuildingKind.Extractor, e.Position, e.Direction, FilterItemId: e.FilterItemId);
                entityId = e.Id;
                return true;
            }
        }

        foreach (var n in powerNodes)
        {
            if (n.Occupies(tile))
            {
                var kind = n.DefinitionId == PowerNode.Tier2Id ? BuildingKind.PowerNodeT2 : BuildingKind.PowerNode;
                spec = new BuildSpec(n.DefinitionId, kind, n.Position, Direction.East);
                entityId = n.Id;
                return true;
            }
        }

        foreach (var l in lamps)
        {
            if (l.Occupies(tile))
            {
                spec = new BuildSpec(Lamp.BuildingId, BuildingKind.Lamp, l.Position, Direction.East);
                entityId = l.Id;
                return true;
            }
        }

        foreach (var s in storages)
        {
            if (s.Occupies(tile))
            {
                spec = new BuildSpec(s.StructureId, s.Kind, s.Origin, Direction.East);
                entityId = s.Id;
                return true;
            }
        }

        if (Belts.TryGet(tile, out var cell))
        {
            entityId = 0;
            var id = cell.Definition.Id;
            switch (cell.Kind)
            {
                case LogisticsKind.Junction:
                    spec = new BuildSpec(id, BuildingKind.Junction, tile, cell.Direction);
                    return true;
                case LogisticsKind.Splitter:
                    spec = new BuildSpec(id, BuildingKind.Splitter, tile, cell.Direction);
                    return true;
                case LogisticsKind.Sorter:
                    spec = new BuildSpec(id, BuildingKind.Sorter, tile, cell.Direction, FilterItemId: cell.FilterItemId);
                    return true;
                case LogisticsKind.Bridge when cell.BridgePartner is { } partner:
                    var entry = IsAhead(tile, cell.Direction, partner) ? tile : partner;
                    spec = new BuildSpec(id, BuildingKind.Bridge, entry, cell.Direction);
                    return true;
                default:
                    spec = new BuildSpec(id, null, tile, cell.Direction);
                    return true;
            }
        }

        spec = null!;
        entityId = 0;
        return false;
    }

    /// <summary>
    /// Removes the building or belt piece on <paramref name="tile"/> now. With <paramref name="refund"/>
    /// it gives back <c>ceil(amount × </c><see cref="RefundBasisPointsFor"/><c> / 10000)</c> per cost line.
    /// </summary>
    public bool RemoveInstant(GridPosition tile, bool refund)
    {
        if (!TryGetSpecAt(tile, out var spec, out _))
        {
            return false;
        }

        var removed = spec.Kind is null or BuildingKind.Junction or BuildingKind.Splitter
            or BuildingKind.Sorter or BuildingKind.Bridge
            ? Belts.TryRemove(tile)
            : TryRemoveBuildingEntity(tile, out _);
        if (!removed)
        {
            return false;
        }

        if (refund)
        {
            Refund(spec);
        }

        return true;
    }

    /// <summary>Gives back <c>ceil(amount × </c><see cref="RefundBasisPointsFor"/><c> / 10000)</c> per cost line of <paramref name="spec"/>.</summary>
    private void Refund(BuildSpec spec)
    {
        var basisPoints = RefundBasisPointsFor(spec);
        foreach (var entry in CostOf(spec))
        {
            var amount = Construction.RefundShare(entry.Amount, basisPoints);
            if (entry.ItemId is { } itemId)
            {
                State.Storage.AddClamped(itemId, amount);
            }
            else if (amount > 0)
            {
                Wallet.AddMoney(amount);
            }
        }
    }

    private int FootprintSize(BuildSpec spec) => spec.Kind switch
    {
        BuildingKind.Miner or BuildingKind.MinerAdvanced => Miner.Size,
        BuildingKind.Smelter or BuildingKind.Assembler => Crafter.Size,
        BuildingKind.Generator => Generator.Size,
        BuildingKind.Extractor => Extractor.Size,
        BuildingKind.PowerNodeT2 => 2,
        BuildingKind.CoreFoundation or BuildingKind.CoreNucleus => Content.CoreTiers[BuildSpec.CoreTierOf(spec.Kind)].Size,
        BuildingKind.Container or BuildingKind.Vault => StorageBlock.SizeOf(spec.Kind),
        _ => 1
    };

    /// <summary>First free bridge end 2–4 cells ahead of <paramref name="entry"/> (same rule as <see cref="BeltGrid.TryPlaceBridge"/>).</summary>
    private GridPosition? FindBridgeExit(GridPosition entry, Direction direction)
    {
        for (var span = BeltGridCell.MinBridgeSpan; span <= BeltGridCell.MaxBridgeSpan; span++)
        {
            var candidate = entry.Step(direction, span);
            if (!Belts.Contains(candidate) && CanOccupy(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsAhead(GridPosition from, Direction direction, GridPosition target)
    {
        for (var span = 1; span <= BeltGridCell.MaxBridgeSpan; span++)
        {
            if (from.Step(direction, span).Equals(target))
            {
                return true;
            }
        }

        return false;
    }

    private bool PlaceBelt(GridPosition position, Direction direction, ConveyorDefinition def, bool charge)
    {
        if (Belts.Cells.ContainsKey(position) || !CanOccupy(position))
        {
            return false;
        }

        if (charge && !TryChargeConveyor(def))
        {
            return false;
        }

        if (!Belts.TryPlaceFree(position, direction, def, CanOccupy))
        {
            if (charge)
            {
                RefundConveyor(def, putBack: true);
            }

            return false;
        }

        return true;
    }

    private bool PlaceLogistics(
        GridPosition position,
        Direction direction,
        ConveyorDefinition def,
        bool charge,
        string? sorterFilter = null)
    {
        if (Belts.Cells.ContainsKey(position)
            || !CanOccupy(position)
            || (charge && !TryChargeConveyor(def)))
        {
            return false;
        }

        if (!Belts.TryPlaceFree(position, direction, def, CanOccupy))
        {
            if (charge)
            {
                RefundConveyor(def, putBack: true);
            }

            return false;
        }

        if (def.Kind == LogisticsKind.Sorter
            && Belts.TryGet(position, out var cell)
            && cell.Kind == LogisticsKind.Sorter)
        {
            cell.SetFilterItem(sorterFilter ?? BeltGridCell.DefaultSorterFilter);
        }

        return true;
    }

    /// <summary>Bridge from <paramref name="entry"/>; <paramref name="exit"/> forces the far end (finished sites).</summary>
    private bool PlaceBridge(GridPosition entry, Direction direction, bool charge, GridPosition? exit)
    {
        if (charge && !TryChargeConveyor(BridgeDefinition, multiplicity: 2))
        {
            return false;
        }

        Func<GridPosition, bool> canOccupy = exit is { } forced
            ? p => CanOccupy(p) && (p.Equals(entry) || p.Equals(forced))
            : CanOccupy;
        if (!Belts.TryPlaceBridge(entry, direction, BridgeDefinition, canOccupy))
        {
            if (charge)
            {
                RefundConveyor(BridgeDefinition, multiplicity: 2, putBack: true);
            }

            return false;
        }

        return true;
    }

    private bool PlaceMiner(
        GridPosition origin,
        Direction direction,
        string outputItemId,
        string definitionId,
        bool charge)
    {
        var covered = Miner.FootprintArea;
        var itemId = outputItemId;
        if (Terrain is { } terrain)
        {
            if (MinerTooWeak(origin, definitionId))
            {
                return false;
            }

            // Ores harder than the miner's tier do not count: a T1 on iron + titanium digs the iron cells only.
            var majority = terrain.MajorityDeposit(origin, Miner.Size, d => Content.MinerCanDig(definitionId, d));
            covered = majority == DepositKind.None
                ? 0
                : terrain.CountDepositTiles(origin, Miner.Size, majority);
            if (majority != DepositKind.None)
            {
                itemId = TerrainMap.ItemIdForDeposit(majority);
            }
        }

        if (!CanOccupyFootprint(origin, Miner.Size) || (charge && !TryChargeBuilding(definitionId)))
        {
            return false;
        }

        buildingsVersion++;
        miners.Add(new Miner(origin, direction, covered, itemId, definitionId, nextEntityId++));
        return true;
    }

    /// <summary>
    /// True when the footprint at <paramref name="origin"/> has ore but none that <paramref name="minerId"/> can
    /// dig (a T1 on titanium only). A footprint without ore is not refused, as before.
    /// </summary>
    private bool MinerTooWeak(GridPosition origin, string minerId)
    {
        if (Terrain is not { } terrain)
        {
            return false;
        }

        var anyOre = false;
        for (var y = 0; y < Miner.Size; y++)
        {
            for (var x = 0; x < Miner.Size; x++)
            {
                var p = new GridPosition(origin.X + x, origin.Y + y);
                if (!terrain.InBounds(p) || terrain[p].Deposit == DepositKind.None)
                {
                    continue;
                }

                if (Content.MinerCanDig(minerId, terrain[p].Deposit))
                {
                    return false;
                }

                anyOre = true;
            }
        }

        return anyOre;
    }

    private bool PlaceCrafter(
        GridPosition origin,
        Direction direction,
        RecipeDefinition? recipe,
        string buildingId,
        bool charge)
    {
        var isAssembler = buildingId == Crafter.AssemblerBuildingId;
        var recipes = isAssembler ? Content.AssemblerRecipes() : Content.SmelterRecipes();
        var defaultRecipeId = isAssembler ? "craft-copper-wire" : "smelt-iron";
        recipe ??= recipes.FirstOrDefault(r => r.Id == defaultRecipeId)
            ?? Content.FindRecipe(defaultRecipeId)
            ?? throw new InvalidDataException($"Ricetta {defaultRecipeId} mancante.");

        if (!CanOccupyFootprint(origin, Crafter.Size) || (charge && !TryChargeBuilding(buildingId)))
        {
            return false;
        }

        buildingsVersion++;
        (isAssembler ? assemblers : smelters).Add(new Crafter(
            origin, direction, recipe, buildingId, recipes, nextEntityId++));
        return true;
    }

    private bool PlaceGenerator(GridPosition origin, Direction direction, bool charge)
    {
        if (!CanOccupyFootprint(origin, Generator.Size) || (charge && !TryChargeBuilding(Generator.BuildingId)))
        {
            return false;
        }

        buildingsVersion++; // belts blend with generators
        generators.Add(new Generator(origin, direction, nextEntityId++));
        return true;
    }

    private bool PlaceExtractor(GridPosition origin, Direction direction, string filterItemId, bool charge)
    {
        if (!CanOccupyFootprint(origin, Extractor.Size) || (charge && !TryChargeBuilding(Extractor.BuildingId)))
        {
            return false;
        }

        buildingsVersion++;
        extractors.Add(new Extractor(origin, direction, filterItemId, nextEntityId++));
        return true;
    }

    private bool PlacePowerNode(GridPosition origin, string id, bool charge)
    {
        var size = id == PowerNode.Tier2Id ? 2 : 1;
        if (!CanOccupyFootprint(origin, size) || (charge && !TryChargeBuilding(id)))
        {
            return false;
        }

        powerNodes.Add(new PowerNode(origin, id, nextEntityId++));
        return true;
    }

    private bool PlaceLamp(GridPosition origin, bool charge)
    {
        if (!CanOccupyFootprint(origin, Lamp.Size) || (charge && !TryChargeBuilding(Lamp.BuildingId)))
        {
            return false;
        }

        lamps.Add(new Lamp(origin, nextEntityId++));
        return true;
    }

    /// <summary>
    /// Places a container or vault and recomputes the links: touching the Core it adds its space to every item's
    /// maximum, elsewhere it keeps its own contents.
    /// </summary>
    private bool PlaceStorage(BuildSpec spec, bool charge)
    {
        var size = StorageBlock.SizeOf(spec.Kind);
        if (!CanOccupyFootprint(spec.Origin, size) || (charge && !TryChargeBuilding(spec.StructureId)))
        {
            return false;
        }

        buildingsVersion++; // belts blend with storage blocks
        storages.Add(NewStorage(spec.Kind!.Value, spec.Origin, nextEntityId++));
        State.Storage.RecomputeLinks();
        return true;
    }

    /// <summary>A storage block of <paramref name="kind"/> with the capacity of its structure in the content.</summary>
    private StorageBlock NewStorage(BuildingKind kind, GridPosition origin, long id)
    {
        var structureId = BuildSpec.StructureIdFor(kind)!;
        var capacity = Content.FindStructure(structureId)?.ItemCapacity
            ?? throw new InvalidDataException($"content.json: struttura {structureId} mancante.");
        return new StorageBlock(id, kind, origin, StorageBlock.SizeOf(kind), capacity);
    }

    /// <summary>Removes the building on <paramref name="tile"/> without refund; returns its definition id.</summary>
    private bool TryRemoveBuildingEntity(GridPosition tile, out string buildingId)
    {
        buildingsVersion++; // over-invalidating on a miss is harmless
        for (var i = miners.Count - 1; i >= 0; i--)
        {
            if (miners[i].OccupiedTiles().Contains(tile))
            {
                buildingId = miners[i].DefinitionId;
                miners.RemoveAt(i);
                return true;
            }
        }

        for (var i = smelters.Count - 1; i >= 0; i--)
        {
            if (smelters[i].Occupies(tile))
            {
                buildingId = Crafter.SmelterBuildingId;
                smelters.RemoveAt(i);
                return true;
            }
        }

        for (var i = assemblers.Count - 1; i >= 0; i--)
        {
            if (assemblers[i].Occupies(tile))
            {
                buildingId = Crafter.AssemblerBuildingId;
                assemblers.RemoveAt(i);
                return true;
            }
        }

        for (var i = generators.Count - 1; i >= 0; i--)
        {
            if (generators[i].Occupies(tile))
            {
                buildingId = Generator.BuildingId;
                generators.RemoveAt(i);
                return true;
            }
        }

        for (var i = extractors.Count - 1; i >= 0; i--)
        {
            if (extractors[i].Occupies(tile))
            {
                buildingId = Extractor.BuildingId;
                extractors.RemoveAt(i);
                return true;
            }
        }

        for (var i = powerNodes.Count - 1; i >= 0; i--)
        {
            if (powerNodes[i].Occupies(tile))
            {
                buildingId = powerNodes[i].DefinitionId;
                powerNodes.RemoveAt(i);
                return true;
            }
        }

        for (var i = lamps.Count - 1; i >= 0; i--)
        {
            if (lamps[i].Occupies(tile))
            {
                buildingId = Lamp.BuildingId;
                lamps.RemoveAt(i);
                return true;
            }
        }

        for (var i = storages.Count - 1; i >= 0; i--)
        {
            if (storages[i].Occupies(tile))
            {
                // Its contents are lost; a linked block takes its space away from the Core (the stock stays).
                buildingId = storages[i].StructureId;
                storages.RemoveAt(i);
                State.Storage.RecomputeLinks();
                return true;
            }
        }

        buildingId = "";
        return false;
    }

    private IEnumerable<Crafter> CraftMachines() => State.CraftMachines();

    /// <summary>Advances the simulation by exactly one fixed tick (<see cref="SimulationClock.TickSeconds"/>).</summary>
    public void Step() => Simulation.Step(State);

    public void SetPowerBuffer(float buffer) => State.SetPowerBuffer(buffer);

    public void RecalculatePowerCapacity(int liveGeneratorCount) =>
        State.RecalculatePowerCapacity(liveGeneratorCount);

    /// <summary>Drain shared buffer; false on brownout (Raylib parity).</summary>
    public bool TrySpendPower(float amount) => State.TrySpendPower(amount);

    /// <summary>
    /// Spend only when the building is adjacency/node-connected to a live generator.
    /// </summary>
    public bool TrySpendPowerForBuilding(
        GridPosition origin,
        int size,
        float amount,
        IReadOnlyList<Generator>? liveGens = null) =>
        State.TrySpendPowerForBuilding(origin, size, amount, liveGens);

    /// <summary>
    /// Direct building→building (or building→core) when footprints touch (Raylib parity).
    /// Prefer over belts for compact layouts.
    /// </summary>
    public bool TryDeliverAdjacent(
        GridPosition origin,
        int size,
        string itemId,
        Crafter? excludeCrafter = null) =>
        State.TryDeliverAdjacent(origin, size, itemId, excludeCrafter);

    /// <summary>Guard for demo/seed layouts: throws if a scripted placement fails.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Demo seed fallito: {message}");
        }
    }
}
