namespace TIndustry.Shared;

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

    /// <summary>Lamps, in id order (lit = powered in the last tick).</summary>
    IReadOnlyList<Lamp> Lamps { get; }

    /// <summary>Rules of this game (the client reads <see cref="GameRules.Night"/>).</summary>
    GameRules Rules { get; }

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

    MarketCatalog Market { get; }

    ISessionView Session { get; }

    IReadOnlySet<GridPosition> CoreTiles { get; }

    TerrainMap? Terrain { get; }

    float PowerBuffer { get; }

    float PowerCapacity { get; }

    float PowerGenerationPerSecond { get; }

    bool AutoSellAtCore { get; }

    int CoreUpgradeLevel { get; }

    int CoreSaleBonusPercent { get; }

    string? ActiveCampaignLevelId { get; }

    long CoreDeliveredItems { get; }

    bool CanAffordStructure(string structureId, int multiplicity = 1);

    Refusal? Shortfall(string structureId, int multiplicity = 1);

    int PreviewSellPrice(string itemId);

    Refusal? CoreUpgradeRefusal();

    bool IsStructureUnlocked(string structureId);

    bool CanOccupy(GridPosition position);

    bool CanOccupyFootprint(GridPosition origin, int size);

    /// <summary>
    /// True when <paramref name="spec"/> can be placed on its cells (see <see cref="Factory.CanPlace"/>);
    /// <paramref name="reason"/> is the refusal otherwise. The client's placement ghost asks this, never a copy of it.
    /// </summary>
    bool CanPlace(BuildSpec spec, out RefusalCode? reason);

    bool IsBuildingTile(GridPosition tile);
}
