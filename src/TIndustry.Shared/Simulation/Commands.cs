namespace TIndustry.Shared;

/// <summary>
/// A player's request to change the game. Queued by <see cref="SimHost.Submit"/>, validated and
/// applied at the start of tick <see cref="Tick"/>; the outcome comes back as an event.
/// </summary>
public abstract record SimCommand(int PlayerId)
{
    /// <summary>Tick at whose start the command is applied (0 = "next tick", set on submit).</summary>
    public long Tick { get; init; }
}

/// <summary>Buildings and logistics pieces placed by <see cref="PlaceBuilding"/> (belts use <see cref="PlaceBelt"/>).</summary>
public enum BuildingKind
{
    Junction,
    Splitter,
    Sorter,
    Bridge,
    Miner,
    MinerAdvanced,
    Smelter,
    Assembler,
    Generator,
    Extractor,
    PowerNode,
    PowerNodeT2,
    Lamp
}

/// <summary>Replaces <see cref="Factory.TryPlaceBelt"/>.</summary>
public sealed record PlaceBelt(int PlayerId, GridPosition Position, Direction Direction, string ConveyorId)
    : SimCommand(PlayerId);

/// <summary>
/// Replaces the <c>TryPlace…</c> methods for junction, splitter, sorter, bridge, miners, forno,
/// assembler, generator, extractor and power nodes. <paramref name="RecipeId"/> applies to forno /
/// assembler (null = today's default recipe); <paramref name="FilterItemId"/> to sorter / extractor.
/// </summary>
public sealed record PlaceBuilding(
    int PlayerId,
    BuildingKind Kind,
    GridPosition Origin,
    Direction Direction,
    string? RecipeId = null,
    string? FilterItemId = null) : SimCommand(PlayerId);

/// <summary>
/// Takes away what stands on <paramref name="Tile"/> (see <see cref="SimHost"/> for the order). A single
/// right-click toggles: a second Remove on a block marked for dismantling takes the mark off. With
/// <paramref name="OnlyMark"/> (every cell of a right-drag) it only marks or cancels build plans and sites and
/// never takes a dismantling mark off, so overlapping drags keep all their marks.
/// </summary>
public sealed record Remove(int PlayerId, GridPosition Tile, bool OnlyMark = false) : SimCommand(PlayerId);

/// <summary>Replaces <see cref="Factory.TryUnlockStructure"/>.</summary>
public sealed record UnlockStructure(int PlayerId, string StructureId) : SimCommand(PlayerId);

/// <summary>Replaces <see cref="Factory.TrySellFromWallet"/>.</summary>
public sealed record Sell(int PlayerId, string ItemId, int Amount) : SimCommand(PlayerId);

/// <summary>Replaces assigning <see cref="Factory.AutoSellAtCore"/>.</summary>
public sealed record SetAutoSell(int PlayerId, bool Enabled) : SimCommand(PlayerId);

/// <summary>Replaces <see cref="Factory.TryUpgradeCore"/>.</summary>
public sealed record UpgradeCore(int PlayerId) : SimCommand(PlayerId);

/// <summary>
/// Sets the filter of the sorter on <paramref name="Tile"/> (replaces the client's direct
/// <c>BeltGridCell.CycleFilterItem</c>). <paramref name="ItemId"/> must be one of <see cref="SorterFilters.Ids"/>.
/// </summary>
public sealed record SetSorterFilter(int PlayerId, GridPosition Tile, string ItemId) : SimCommand(PlayerId);

/// <summary>
/// Rejection reasons for <see cref="SimHost.CommandRejected"/>: the Italian texts the Godot client
/// shows in its toasts / status labels today, gathered in one place.
/// </summary>
public static class CommandReasons
{
    /// <summary>Fallback when <see cref="Factory.FormatNeedMessage"/> is empty (World.Placement, World.Session).</summary>
    public const string InsufficientResources = "Risorse insufficienti";

    /// <summary>Placement on a CORE tile (World.Placement).</summary>
    public const string OnCore = "Non puoi costruire sul Core";

    /// <summary>
    /// Placement on an occupied / invalid tile (World.Placement). Today's client text wins over the
    /// spec's paraphrase "Spazio occupato.".
    /// </summary>
    public const string Occupied = "Cella occupata o non valida";

    /// <summary>Placement on a cell with a wall (<see cref="TerrainTile.Wall"/>).</summary>
    public const string Wall = "C'è una parete";

    /// <summary>Placement on deep water (<see cref="FloorKind.DeepWater"/>); bridges span it.</summary>
    public const string DeepWater = "Acqua profonda";

    /// <summary>
    /// A T1 miner on cells whose only ore is harder than its tier (titanium): Mindustry refuses a drill that
    /// cannot dig what lies under it.
    /// </summary>
    public const string NeedsAdvancedMiner = "Serve il Minatore T2 per il titanio";

    /// <summary><see cref="PlanDropped"/>: the structure was locked when the drone reached the plan.</summary>
    public const string LockedByResearch = "Bloccato dalla ricerca";

    /// <summary>Research: already unlocked (ResearchPanel).</summary>
    public const string AlreadyUnlocked = "Già sbloccato.";

    /// <summary>Research: prerequisites missing (ResearchPanel).</summary>
    public const string MissingPrerequisites = "Prerequisiti mancanti.";

    /// <summary>Research: cannot pay the unlock (ResearchPanel).</summary>
    public const string UnlockUnaffordable = "Risorse insufficienti per sbloccare.";

    /// <summary>Mercato: sale failed (MercatoPanel).</summary>
    public const string SellFailed = "Vendita fallita (stock insufficiente).";

    /// <summary>Remove on an empty tile (the client shows nothing today).</summary>
    public const string NothingToRemove = "Niente da rimuovere.";

    /// <summary>
    /// Malformed command (unknown id, non-positive amount, sorter filter on a non-sorter tile or with
    /// an item outside <see cref="SorterFilters.Ids"/>): the client never sends one today.
    /// </summary>
    public const string InvalidCommand = "Comando non valido.";

    /// <summary>Locked structure: "<c>{tool} bloccato: sbloccalo in Ricerca (T).</c>" (World.Placement).</summary>
    public static string Locked(string toolName) => $"{toolName} bloccato: sbloccalo in Ricerca (T).";

    /// <summary>Tool name the client uses in the locked toast (World.Input <c>ToolIt</c>).</summary>
    public static string ToolName(BuildingKind kind) => kind switch
    {
        BuildingKind.Junction => "Giunzione",
        BuildingKind.Splitter => "Splitter",
        BuildingKind.Sorter => "Selezionatore",
        BuildingKind.Bridge => "Ponte",
        BuildingKind.Miner => "Minatore",
        BuildingKind.MinerAdvanced => "Minatore T2",
        BuildingKind.Smelter => "Forno",
        BuildingKind.Assembler => "Assemblatore",
        BuildingKind.Generator => "Generatore",
        BuildingKind.Extractor => "Estrattore",
        BuildingKind.PowerNode => "Nodo potenza",
        BuildingKind.PowerNodeT2 => "Nodo T2",
        BuildingKind.Lamp => "Lampada",
        _ => "?"
    };

    /// <summary>Belt tool name by conveyor id (World.Input <c>ToolIt</c>).</summary>
    public static string BeltToolName(string conveyorId) => conveyorId switch
    {
        "conveyor-fast" => "Nastro T2",
        "conveyor-express" => "Nastro T3",
        _ => "Nastro"
    };
}
