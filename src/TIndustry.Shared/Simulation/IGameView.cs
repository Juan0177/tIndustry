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

    /// <summary>Default belt definition (HUD shows its rate).</summary>
    ConveyorDefinition BeltDefinition { get; }

    IReadOnlyList<Miner> Miners { get; }

    IReadOnlyList<Crafter> Smelters { get; }

    IReadOnlyList<Crafter> Assemblers { get; }

    IReadOnlyList<Generator> Generators { get; }

    IReadOnlyList<Extractor> Extractors { get; }

    IReadOnlyList<PowerNode> PowerNodes { get; }

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

    string FormatNeedMessage(string structureId, int multiplicity = 1);

    int PreviewSellPrice(string itemId);

    string FormatCoreUpgradeNeedMessage();

    bool IsStructureUnlocked(string structureId);

    bool CanOccupy(GridPosition position);

    bool CanOccupyFootprint(GridPosition origin, int size);

    bool IsBuildingTile(GridPosition tile);

    /// <summary>Canonical capture of the whole state (saving).</summary>
    FactorySaveData Capture();
}
