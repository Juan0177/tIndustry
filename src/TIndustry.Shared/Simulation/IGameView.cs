namespace TIndustry.Shared;

/// <summary>
/// A power network as the client sees it (last balance): rates in thousandths of a unit per second, charge and
/// capacity in sixty-thousandths of a unit, satisfaction in basis points (0–10 000).
/// </summary>
public readonly record struct PowerNetworkView(
    int Index,
    int MemberCount,
    long ProducedRate,
    long RequestedRate,
    int SatisfactionBp,
    long Stored,
    long Capacity)
{
    public static PowerNetworkView Of(PowerNetwork network)
    {
        ArgumentNullException.ThrowIfNull(network);
        return new PowerNetworkView(
            network.Index,
            network.Members.Count,
            network.ProducedRate,
            network.RequestedRate,
            network.SatisfactionBp,
            network.Stored,
            network.Capacity);
    }
}

/// <summary>A power node's lasers as the client sees them: used, maximum, and range in cells.</summary>
public readonly record struct PowerNodeLinks(long NodeId, int Used, int Max, int Range);

/// <summary>Read-only wallet (implemented by <see cref="EconomyWallet"/>).</summary>
public interface IWalletView
{
    int Money { get; }

    int MaterialCount(string itemId);

    IReadOnlyDictionary<string, int> MaterialsSnapshot();

    bool CanAfford(int money, IReadOnlyList<ResourceAmount> cost);
}

/// <summary>Read-only sales record of the session (implemented by <see cref="EconomySession"/>).</summary>
public interface ISessionView
{
    int StartingMoney { get; }

    int SaleIncome { get; }

    IReadOnlyDictionary<string, int> SoldByItem { get; }
}

/// <summary>Read-only research state (implemented by <see cref="ResearchState"/>).</summary>
public interface IResearchView
{
    IReadOnlyCollection<string> UnlockedIds { get; }

    bool IsUnlocked(string structureId);

    ResearchNodeState GetNodeState(StructureDefinition structure);

    bool CanUnlock(StructureDefinition structure, IWalletView wallet);

    bool MeetsPrerequisites(StructureDefinition structure);
}

/// <summary>
/// What the client reads of the game, without mutators: the client changes the game only by
/// submitting commands to <see cref="SimHost"/>. Implemented by <see cref="Factory"/>.
/// </summary>
public interface IGameView
{
    long CurrentTick { get; }

    FactoryContent Content { get; }

    IReadOnlyDictionary<GridPosition, BeltGridCell> Belts { get; }

    /// <summary>Belt cell turning a corner (visual only; <see cref="BeltGrid.IsCorner"/>).</summary>
    bool IsBeltCorner(GridPosition position);

    /// <summary>How the belt cell is drawn: straight, corner, T merge or side join (visual only; <see cref="BeltGrid.GetShape"/>).</summary>
    BeltShape GetBeltShape(GridPosition position);

    /// <summary>Junction side with an input or an output (visual only; <see cref="BeltGrid.IsJunctionSideConnected"/>).</summary>
    bool IsJunctionSideConnected(GridPosition position, Direction side);

    /// <summary>Auto-junction rule for a dragged belt facing <paramref name="direction"/> on <paramref name="position"/>.</summary>
    BeltCrossing BeltCrossingAt(GridPosition position, Direction direction);

    /// <summary>What the plain belt <paramref name="conveyorId"/> facing <paramref name="direction"/> does over the belt on <paramref name="cell"/>.</summary>
    BeltOverBelt BeltOverBeltAt(GridPosition cell, Direction direction, string conveyorId);

    /// <summary>Direction of the belt feeding the position (visual only).</summary>
    bool TryGetBeltIncomingDirection(GridPosition position, out Direction incoming);

    /// <summary>Default belt definition (HUD shows its rate).</summary>
    ConveyorDefinition BeltDefinition { get; }

    IReadOnlyList<Miner> Miners { get; }

    IReadOnlyList<Crafter> Smelters { get; }

    IReadOnlyList<Crafter> Assemblers { get; }

    IReadOnlyList<Generator> Generators { get; }

    IReadOnlyList<Extractor> Extractors { get; }

    IReadOnlyList<PowerNode> PowerNodes { get; }

    /// <summary>Lamps, in id order (light = satisfaction of their network in the last tick).</summary>
    IReadOnlyList<Lamp> Lamps { get; }

    /// <summary>Batteries, in id order.</summary>
    IReadOnlyList<Battery> Batteries { get; }

    /// <summary>Containers and vaults, in id order (linked to the Core or standalone with their contents).</summary>
    IReadOnlyList<StorageBlock> Storages { get; }

    /// <summary>Rules of this game.</summary>
    GameRules Rules { get; }

    /// <summary>The world's time chosen for this game (<see cref="GameRules.DayNight"/>).</summary>
    DayNightMode DayNight { get; }

    /// <summary>Phase of the world's time now (<see cref="DayNightClock.PhaseAt"/>).</summary>
    DayNightPhase DayNightPhase { get; }

    /// <summary>Light now, 0 (night) to <see cref="DayNightClock.FullLight"/> (day) (<see cref="DayNightClock.LightAt"/>).</summary>
    int Light { get; }

    /// <summary>Ticks to the next night, day or first attack; null when nothing is counted (<see cref="DayNightClock.TicksToNextChange"/>).</summary>
    long? TicksToNextChange { get; }

    /// <summary>Tick of the first block placed, when the world's time started; null before it.</summary>
    long? FirstBuildTick { get; }

    /// <summary>Construction drones, in id order (position, rotation, queue, current site).</summary>
    IReadOnlyList<BuilderUnit> Builders { get; }

    /// <summary>Construction and deconstruction sites, in id order.</summary>
    IReadOnlyList<ConstructSite> Sites { get; }

    /// <summary>The site covering <paramref name="tile"/>, if any.</summary>
    bool TryGetSiteAt(GridPosition tile, out ConstructSite site);

    /// <summary>Build queue of <paramref name="playerId"/> (first = being reached or worked on).</summary>
    IReadOnlyList<BuildPlan> PlansOf(int playerId);

    /// <summary>
    /// Cells <paramref name="plan"/> covers (graphics): the planned block's (a new bridge: up to the exit
    /// it would get now), or, when breaking, the whole block standing on the clicked cell.
    /// </summary>
    IReadOnlyList<GridPosition> PlanTiles(BuildPlan plan);

    /// <summary>True when a pending (non-breaking) plan of any player covers <paramref name="tile"/>.</summary>
    bool IsPlannedTile(GridPosition tile);

    /// <summary>
    /// The site already standing for build plan <paramref name="plan"/> (the current one, or one left where it
    /// stopped when its stalled plan went to the back of the queue); null when the plan has none.
    /// </summary>
    ConstructSite? SiteOfPlan(BuildPlan plan);

    IWalletView Wallet { get; }

    IResearchView Research { get; }

    ISessionView Session { get; }

    IReadOnlySet<GridPosition> CoreTiles { get; }

    /// <summary>Core tier (0 shard, 1 foundation, 2 nucleus), index in <see cref="FactoryContent.CoreTiers"/>.</summary>
    int CoreTier { get; }

    /// <summary>Maximum Core stock of <paramref name="itemId"/> (<see cref="CoreStorage.Capacity"/>).</summary>
    int CoreCapacity(string itemId);

    TerrainMap? Terrain { get; }

    /// <summary>Lasers between power blocks as entity id pairs (<c>A &lt; B</c>), ordered.</summary>
    IReadOnlyList<(long A, long B)> PowerLinks { get; }

    /// <summary>Every power network with its last balance, ordered by lowest member id.</summary>
    IReadOnlyList<PowerNetworkView> PowerNetworks { get; }

    /// <summary>The network of the power block covering <paramref name="tile"/>; null when there is none.</summary>
    PowerNetworkView? PowerNetworkOf(GridPosition tile);

    /// <summary>Every power block (generators, nodes, batteries, consumers), in id order (<see cref="PowerGrid.Blocks"/>).</summary>
    IReadOnlyList<PowerBlock> PowerBlocks { get; }

    /// <summary>The power block covering <paramref name="tile"/>; null when there is none (or a site stands there).</summary>
    PowerBlock? PowerBlockAt(GridPosition tile);

    /// <summary>Lasers used, lasers it can hold and range of the power node <paramref name="nodeId"/>; null for anything else.</summary>
    PowerNodeLinks? PowerNodeLinksOf(long nodeId);

    /// <summary>
    /// True when a laser from the node <paramref name="fromNodeId"/> reaches the power block <paramref name="toEntityId"/>
    /// (the range rule of <see cref="TogglePowerLink"/>, never a copy of it in the client).
    /// </summary>
    bool PowerLinkInRange(long fromNodeId, long toEntityId);

    string? ActiveCampaignLevelId { get; }

    long CoreDeliveredItems { get; }

    bool CanAffordStructure(string structureId, int multiplicity = 1);

    Refusal? Shortfall(string structureId, int multiplicity = 1);

    /// <summary>Every market item in content order.</summary>
    IReadOnlyList<MarketItemView> MarketItems { get; }

    MarketItemView? MarketItem(string itemId);

    /// <summary>Seconds (rounded up) before "sell all" works again; 0 when it is free.</summary>
    int SellAllCooldownSeconds { get; }

    /// <summary>Why the Core cannot be upgraded now (see <see cref="Factory.CoreUpgradeRefusal"/>); null when it can.</summary>
    Refusal? CoreUpgradeRefusal();

    /// <summary>The block of the next Core tier where the growth rule puts it (<see cref="Factory.CoreUpgradeSpec"/>); null at the last tier.</summary>
    BuildSpec? CoreUpgradeSpec();

    /// <summary>The cells the next Core upgrade adds, row by row (empty at the last tier).</summary>
    IReadOnlyList<GridPosition> CoreUpgradeNewTiles();

    bool IsStructureUnlocked(string structureId);

    bool CanOccupy(GridPosition position);

    bool CanOccupyFootprint(GridPosition origin, int size);

    /// <summary>
    /// True when <paramref name="spec"/> can be placed on its cells (see <see cref="Factory.CanPlace"/>);
    /// <paramref name="reason"/> is the refusal otherwise. The client's placement ghost asks this, never a copy of it.
    /// </summary>
    bool CanPlace(BuildSpec spec, out RefusalCode? reason);

    /// <summary>Cells <paramref name="spec"/> covers (see <see cref="Factory.Footprint"/>): a bridge's both ends.</summary>
    IReadOnlyList<GridPosition> Footprint(BuildSpec spec);

    bool IsBuildingTile(GridPosition tile);

    /// <summary>The spec and entity id of what stands on <paramref name="tile"/> (see <see cref="Factory.TryGetSpecAt"/>).</summary>
    bool TryGetSpecAt(GridPosition tile, out BuildSpec spec, out long entityId);
}
