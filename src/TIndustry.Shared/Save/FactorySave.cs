using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Shared;

/// <summary>
/// Full game state as stored in the canonical snapshot (see <see cref="Snapshot"/>). Version 16: the world's time
/// (<see cref="GameRulesSaveDto.DayNight"/>, <see cref="FirstBuildTick"/>, <see cref="FirstAttackRaised"/>); since 15 the
/// Core grows by tiers (<see cref="CoreTier"/>, <see cref="CoreStartX"/>/<see cref="CoreStartY"/>), containers and vaults
/// (<see cref="Storages"/>) and the market with the auto-sell rate (<see cref="Market"/>). Older versions are not read
/// (the game is not out yet): they get <see cref="SaveLoadError.OlderVersion"/>.
/// </summary>
public sealed class FactorySaveData
{
    public const int CurrentVersion = 16;

    /// <summary>Oldest version still read: only the current one.</summary>
    public const int MinReadableVersion = 16;

    /// <summary><see cref="Generator"/> of a campaign map file (<see cref="MapId"/> names it).</summary>
    public const string CampaignGenerator = "campagna";

    public int Version { get; set; } = CurrentVersion;
    public long NextItemId { get; set; } = 1;
    /// <summary>Next stable building id.</summary>
    public long NextEntityId { get; set; }
    /// <summary>Fixed simulation ticks elapsed.</summary>
    public long CurrentTick { get; set; }

    /// <summary>Tick of the first block placed, when the world's time starts (<see cref="GameState.FirstBuildTick"/>); null before.</summary>
    public long? FirstBuildTick { get; set; }

    /// <summary>The first attack was raised (<see cref="GameState.FirstAttackRaised"/>): written only when true.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool FirstAttackRaised { get; set; }
    public long CoreDeliveredItems { get; set; }
    /// <summary>Top-left cell of the Core.</summary>
    public int CoreX { get; set; }
    public int CoreY { get; set; }
    /// <summary>Edge of the Core: must be the size of <see cref="CoreTier"/>, or the save is unreadable.</summary>
    public int CoreSize { get; set; }
    /// <summary>Core tier (index in <see cref="FactoryContent.CoreTiers"/>: 0 shard, 1 foundation, 2 nucleus).</summary>
    public int CoreTier { get; set; }
    /// <summary>
    /// Top-left cell of the Core the game started with (<see cref="GameState.CoreStart"/>): the terrain generators made
    /// the map around it, so the terrain is regenerated from it whatever the Core has grown into.
    /// </summary>
    public int CoreStartX { get; set; }
    public int CoreStartY { get; set; }
    public int Money { get; set; }
    public Dictionary<string, int> Materials { get; set; } = new(StringComparer.Ordinal);
    public List<string> UnlockedStructures { get; set; } = [];
    public int StartingMoney { get; set; }
    public int SaleIncome { get; set; }
    public Dictionary<string, int> SoldByItem { get; set; } = new(StringComparer.Ordinal);
    public string? ActiveCampaignLevelId { get; set; }
    /// <summary>The market (prices, phases, random stream, history, auto-sell); a save without it is unreadable.</summary>
    public MarketSaveDto? Market { get; set; }
    /// <summary>Terrain seed (0 = no terrain).</summary>
    public int Seed { get; set; }
    public int MapWidth { get; set; }
    public int MapHeight { get; set; }

    /// <summary>
    /// Terrain generator: <c>"classico"</c>, <c>"biomi-1"</c> (also called <c>"biomi"</c>)
    /// (regenerated with <see cref="MapGenSettings.Normal"/> of <see cref="MapSize"/> and <see cref="Seed"/>) or
    /// <c>"campagna"</c> (the map file <see cref="MapId"/>).
    /// </summary>
    public string Generator { get; set; } = ClassicTerrainGenerator.Name;

    /// <summary>Edge of a <c>"biomi-1"</c> map; 0 for the other generators.</summary>
    public int MapSize { get; set; }

    /// <summary>
    /// <see cref="TerrainMap.TerrainHash"/> of the terrain when saved. A terrain regenerated from a generator
    /// must match it, or the load fails; null when there is no terrain.
    /// </summary>
    public ulong? TerrainHash { get; set; }

    /// <summary>Campaign map id (<c>data/maps/&lt;id&gt;.png</c>) of a <c>"campagna"</c> map; null otherwise.</summary>
    public string? MapId { get; set; }

    public List<MinerSaveDto> Miners { get; set; } = [];
    public List<CraftSaveDto> Smelters { get; set; } = [];
    public List<CraftSaveDto> Assemblers { get; set; } = [];
    public List<GeneratorSaveDto> Generators { get; set; } = [];
    public List<ExtractorSaveDto> Extractors { get; set; } = [];
    public List<PowerNodeSaveDto> PowerNodes { get; set; } = [];
    /// <summary>Lamps: null when there are none.</summary>
    public List<LampSaveDto>? Lamps { get; set; }
    /// <summary>Batteries: null when there are none.</summary>
    public List<BatterySaveDto>? Batteries { get; set; }
    /// <summary>Containers and vaults, in id order: null when there are none.</summary>
    public List<StorageSaveDto>? Storages { get; set; }
    public List<BeltSaveDto> Belts { get; set; } = [];
    public GameRulesSaveDto Rules { get; set; } = new();
    /// <summary>Construction drones, in id order.</summary>
    public List<BuilderSaveDto> Builders { get; set; } = [];
    /// <summary>Construction and deconstruction sites, in id order.</summary>
    public List<SiteSaveDto> Sites { get; set; } = [];
}

public sealed class GameRulesSaveDto
{
    public int RefundPercent { get; set; } = 100;
    public bool InstantBuild { get; set; }
    /// <summary>World's time: the <see cref="DayNightMode"/> name; an absent key reads as <c>Cycle</c>; an explicit null or an unknown name makes the save unreadable.</summary>
    public string? DayNight { get; set; } = nameof(DayNightMode.Cycle);
}

public sealed class LampSaveDto
{
    public long Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class BatterySaveDto
{
    public long Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    /// <summary>Stored energy, sixty-thousandths of a unit (<see cref="Battery.Charge"/>).</summary>
    public long Charge { get; set; }
}

/// <summary>A container or vault: kind name, origin and, when standalone, its contents in ordinal item order.</summary>
public sealed class StorageSaveDto
{
    public long Id { get; set; }
    /// <summary><see cref="BuildingKind"/> name (<c>Container</c> or <c>Vault</c>).</summary>
    public string Kind { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    /// <summary>Pieces per item of a standalone block, written in ordinal key order; null when empty (linked blocks are).</summary>
    public Dictionary<string, int>? Items { get; set; }
}

public sealed class BuildSpecSaveDto
{
    public string StructureId { get; set; } = string.Empty;
    /// <summary>Building kind name; null for a plain belt.</summary>
    public string? Kind { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string Direction { get; set; } = "East";
    public string? RecipeId { get; set; }
    public string? FilterItemId { get; set; }
}

public sealed class BuildPlanSaveDto
{
    public BuildSpecSaveDto Spec { get; set; } = new();
    public bool Breaking { get; set; }
}

public sealed class BuilderSaveDto
{
    public long Id { get; set; }
    public int Owner { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Rotation { get; set; }
    public long? CurrentSiteId { get; set; }
    /// <summary>Build queue in queue order (first = current).</summary>
    public List<BuildPlanSaveDto> Plans { get; set; } = [];
}

/// <summary>The market of a save: random stream, sell-all cooldown and one entry per item.</summary>
public sealed class MarketSaveDto
{
    public ulong RandomState { get; set; }
    public ulong RandomInc { get; set; }
    public long SellAllBlockedUntilTick { get; set; }
    public List<MarketItemSaveDto> Items { get; set; } = [];
}

/// <summary>Price state of one market item (see <see cref="MarketItemState"/>), history included.</summary>
public sealed class MarketItemSaveDto
{
    public string ItemId { get; set; } = "";
    public int TrendMilli { get; set; }
    public int PriceMilli { get; set; }
    public MarketPhase Phase { get; set; }
    public long PhaseStartTick { get; set; }
    public int SpikeTopMilli { get; set; }
    public int SpikeFromMilli { get; set; }
    public long NextSpikeTick { get; set; }
    public int StepFromMilli { get; set; }
    public int StepTargetMilli { get; set; }
    public long CrashEndTick { get; set; }
    public int CrashJitterMilli { get; set; }
    public long CrashDipEndTick { get; set; }
    public int CrashDipMilli { get; set; }
    public long BoostEndTick { get; set; }
    public int SaleDropMicro { get; set; }
    public long SaleRecoveryEndTick { get; set; }
    public int[] WindowPieces { get; set; } = [];
    public long WindowSecond { get; set; }
    public int PityMisses { get; set; }
    public bool AutoSell { get; set; }
    public bool AutoSellWhenFull { get; set; }
    /// <summary>Fraction of a piece owed by the auto-sell rate, in sixtieths of a piece (0..59).</summary>
    public int AutoSellCarry { get; set; }
    public List<int> History { get; set; } = [];
}

/// <summary>
/// A construction site. The cost is not saved (recomputed from the spec); the cells are, because a
/// bridge keeps the exit chosen when the site started. <c>Missing</c> is transient and not saved.
/// </summary>
public sealed class SiteSaveDto
{
    public long Id { get; set; }
    public BuildSpecSaveDto Spec { get; set; } = new();
    public List<GridCellSaveDto> Tiles { get; set; } = [];
    public float Progress { get; set; }

    /// <summary>
    /// Taken from the Core so far, one line per cost item in ordinal order (money: no item id). Mapped onto
    /// the recomputed cost by item id, so a content update that changes the cost lines cannot move them.
    /// </summary>
    public List<CostLineSaveDto>? SpentByItem { get; set; }

    /// <summary>Given back so far, same lines as <see cref="SpentByItem"/>.</summary>
    public List<CostLineSaveDto>? RefundedByItem { get; set; }

    /// <summary>Ticks in a row the site has been stalled while worked on.</summary>
    public int StalledTicks { get; set; }

    public bool Deconstructing { get; set; }
    /// <summary>Refund share in basis points (0–10000).</summary>
    public int RefundBasisPoints { get; set; }
    public int Owner { get; set; }

    /// <summary>The belt a replacement site took away (written only for replacement sites).</summary>
    public ReplacedSaveDto? Replaced { get; set; }
}

/// <summary>A belt taken away by a replacement site: its spec and items (front first).</summary>
public sealed class ReplacedSaveDto
{
    public BuildSpecSaveDto Spec { get; set; } = new();
    public List<ItemSaveDto> Items { get; set; } = [];
}

/// <summary>One cost line of a site: <see cref="ItemId"/> null means money.</summary>
public sealed class CostLineSaveDto
{
    public string? ItemId { get; set; }
    public int Amount { get; set; }
}

public sealed class GridCellSaveDto
{
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class ExtractorSaveDto
{
    /// <summary>Stable building id.</summary>
    public long Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string Direction { get; set; } = "East";
    public string FilterItemId { get; set; } = Extractor.DefaultFilterItemId;
    public float Progress { get; set; }
}

public sealed class PowerNodeSaveDto
{
    /// <summary>Stable building id.</summary>
    public long Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string DefinitionId { get; set; } = PowerNode.Tier1Id;
}

public sealed class MinerSaveDto
{
    /// <summary>Stable building id.</summary>
    public long Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string Direction { get; set; } = "East";
    public float Progress { get; set; }
    public int EjectIndex { get; set; }
    public string OutputItemId { get; set; } = Miner.DefaultOutputItemId;
    public string DefinitionId { get; set; } = Miner.BasicId;
}

public sealed class CraftSaveDto
{
    /// <summary>Stable building id.</summary>
    public long Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string Direction { get; set; } = "East";
    public string RecipeId { get; set; } = "smelt-iron";
    public string DefinitionId { get; set; } = Crafter.SmelterBuildingId;
    public float Progress { get; set; }
    public bool IsCrafting { get; set; }
    public Dictionary<string, int> InputBuffer { get; set; } = new(StringComparer.Ordinal);
    public List<string> OutputQueue { get; set; } = [];
    public int EjectIndex { get; set; }
    public long ItemsCrafted { get; set; }
    public int FuelBuffer { get; set; }
    public float BurnRemaining { get; set; }
}

public sealed class GeneratorSaveDto
{
    /// <summary>Stable building id.</summary>
    public long Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string Direction { get; set; } = "East";
    public int FuelBuffer { get; set; }
    public float BurnRemaining { get; set; }
    public long FuelConsumed { get; set; }
}

public sealed class BeltSaveDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Direction { get; set; } = "East";
    public string DefinitionId { get; set; } = "conveyor-basic";
    public int SplitterToggle { get; set; }
    public int? BridgePartnerX { get; set; }
    public int? BridgePartnerY { get; set; }
    public string? FilterItemId { get; set; }
    public List<ItemSaveDto> Items { get; set; } = [];
}

public sealed class ItemSaveDto
{
    public long Id { get; set; }
    public string ItemId { get; set; } = "";
    public float Progress { get; set; }
    public string? Travel { get; set; }
}

/// <summary>
/// Save file (one JSON file per slot): the fields the slot list needs, plus the canonical snapshot
/// (<see cref="Snapshot"/>, base64) holding the whole game state. The pending command queue and the
/// simulation clock are not saved: a save is the applied state at a tick boundary.
/// </summary>
public sealed class SaveFileData
{
    public int Version { get; set; } = FactorySaveData.CurrentVersion;
    public int Money { get; set; }
    /// <summary>Terrain seed (0 = no terrain).</summary>
    public int Seed { get; set; }
    public int MapWidth { get; set; }
    public int MapHeight { get; set; }
    public string? ActiveCampaignLevelId { get; set; }
    /// <summary>Terrain generator for the slot list (<see cref="FactorySaveData.Generator"/>).</summary>
    public string? Generator { get; set; }
    /// <summary>Campaign map file of a <c>"campagna"</c> save (<see cref="FactorySaveData.MapId"/>).</summary>
    public string? MapId { get; set; }
    /// <summary>Canonical snapshot bytes, base64.</summary>
    public string? Snapshot { get; set; }
}

/// <summary>Why a save did not load (<see cref="FactorySaveStore.TryLoadSlot"/>); the client turns it into text.</summary>
public enum SaveLoadError
{
    /// <summary>Damaged file, unknown version or bad snapshot.</summary>
    Unreadable,

    /// <summary>Saved by an older, unsupported version.</summary>
    OlderVersion,

    /// <summary>The saved map size does not fit its generator; <see cref="SaveLoadFailure.Argument"/> = "W×H".</summary>
    MapInvalidSize,

    /// <summary>The save names a generator this version does not have; argument = its name.</summary>
    UnknownGenerator,

    /// <summary>The regenerated terrain differs from the saved one; argument = the generator name.</summary>
    TerrainChanged,

    /// <summary>A campaign save without the name of its map.</summary>
    CampaignMapUnnamed,

    /// <summary>The campaign map name is not a plain file name; argument = the name.</summary>
    CampaignMapBadName,

    /// <summary>The folder of the campaign maps (data/maps) was not found; argument = the map id.</summary>
    MapsFolderMissing,

    /// <summary>The campaign map file is missing or invalid; argument = the map id.</summary>
    CampaignMapMissing,

    /// <summary>The campaign map file changed size since the save; argument = the map id.</summary>
    CampaignMapChanged
}

/// <summary>
/// A failed load: its <paramref name="Code"/>, the id or name it is about (<paramref name="Argument"/>) and an Italian
/// <paramref name="Detail"/> for the log and the tests.
/// </summary>
public sealed record SaveLoadFailure(SaveLoadError Code, string? Argument, string Detail);

/// <summary>
/// The terrain of a save cannot be rebuilt (campaign map file missing or changed, unknown generator):
/// <see cref="Code"/> and <see cref="Argument"/> say why; the message is Italian detail for the log.
/// </summary>
public sealed class SaveMapException(SaveLoadError code, string? argument, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public SaveLoadError Code { get; } = code;

    public string? Argument { get; } = argument;
}

/// <summary>One save slot row for Gestione salvataggi.</summary>
public sealed class SaveSlotInfo
{
    public required string Id { get; init; }
    public required string Path { get; init; }
    public DateTime ModifiedUtc { get; init; }
    public int Money { get; init; }
    public int Seed { get; init; }
    public int MapWidth { get; init; }
    public int MapHeight { get; init; }
    public string? ActiveCampaignLevelId { get; init; }
    /// <summary>Terrain generator (<c>"classico"</c>, <c>"biomi"</c>, <c>"campagna"</c>), null when the file does not say.</summary>
    public string? Generator { get; init; }
    /// <summary>Campaign map file of a campaign save.</summary>
    public string? MapId { get; init; }
}

/// <summary>JSON slots under LocalAppData/tIndustry/godot-saves/.</summary>
public static class FactorySaveStore
{
    public const string ContinueSlotId = "continua";
    public const string QuickSlotId = "slot-1";

    /// <summary>Continua as it was before this session's first autosave replaced it (see <see cref="ContinueAutoSave"/>).</summary>
    public const string ContinueBackupSlotId = "continua.prev";

    /// <summary><see cref="TryLoad"/> error for a save of an older, unsupported version (the client shows its own text).</summary>
    public const string OlderVersionMessage = "Salvataggio di una versione precedente: non compatibile.";

    /// <summary><see cref="TryLoad"/> error for a damaged or unknown file (the client shows its own text).</summary>
    public const string UnreadableMessage = "Salvataggio non leggibile.";

    /// <summary>Save JSON format (also used by tests to compare captures byte for byte).</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>Tests point this at a temp dir so they never touch the player's real slots.</summary>
    public static string? SavesDirectoryOverride { get; set; }

    public static string SavesDirectory =>
        SavesDirectoryOverride ?? Path.Combine(GameDataPaths.Root, "godot-saves");

    public static string SlotPath(string slotId)
    {
        var safe = string.IsNullOrWhiteSpace(slotId) ? ContinueSlotId : slotId.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(c, '_');
        }

        return Path.Combine(SavesDirectory, $"{safe}.json");
    }

    public static string ContinueBackupPath => SlotPath(ContinueBackupSlotId);

    /// <summary>Copies the current Continua to <see cref="ContinueBackupPath"/>; false when there is none.</summary>
    public static bool BackupContinue()
    {
        var path = SlotPath(ContinueSlotId);
        if (!File.Exists(path))
        {
            return false;
        }

        AtomicFile.Copy(path, ContinueBackupPath);
        return true;
    }

    public static string CreateSlotId() =>
        $"slot-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

    public static bool Exists(string slotId) => File.Exists(SlotPath(slotId));

    public static IReadOnlyList<SaveSlotInfo> ListSlots()
    {
        var list = new List<SaveSlotInfo>();
        if (!Directory.Exists(SavesDirectory))
        {
            return list;
        }

        foreach (var path in Directory.EnumerateFiles(SavesDirectory, "*.json"))
        {
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue; // never list leftover "<slot>.json.tmp" files from an interrupted write
            }

            if (ReadSlotInfoAt(path) is { } info)
            {
                list.Add(info);
            }
        }

        return list
            .OrderBy(s => s.Id == ContinueSlotId ? 0 : 1)
            .ThenByDescending(s => s.ModifiedUtc)
            .ToList();
    }

    /// <summary>The list row of one slot, reading only its file; null when it is missing or unreadable.</summary>
    public static SaveSlotInfo? ReadSlotInfo(string slotId) =>
        Exists(slotId) ? ReadSlotInfoAt(SlotPath(slotId)) : null;

    private static SaveSlotInfo? ReadSlotInfoAt(string path)
    {
        try
        {
            var data = JsonSerializer.Deserialize<SaveFileData>(File.ReadAllText(path), JsonOptions);
            if (data is null)
            {
                return null;
            }

            return new SaveSlotInfo
            {
                Id = Path.GetFileNameWithoutExtension(path),
                Path = path,
                ModifiedUtc = File.GetLastWriteTimeUtc(path),
                Money = data.Money,
                Seed = data.Seed,
                MapWidth = data.MapWidth,
                MapHeight = data.MapHeight,
                ActiveCampaignLevelId = data.ActiveCampaignLevelId,
                Generator = data.Generator,
                MapId = data.MapId
            };
        }
        catch
        {
            return null; // corrupt / unsupported files are skipped (Raylib parity)
        }
    }

    /// <summary>Writes the applied state of <paramref name="host"/> (not its queued commands) to the slot.</summary>
    public static void Save(string slotId, SimHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var view = host.State;
        var file = new SaveFileData
        {
            Version = FactorySaveData.CurrentVersion,
            Money = view.Wallet.Money,
            Seed = view.Terrain?.Seed ?? 0,
            MapWidth = view.Terrain?.Width ?? 0,
            MapHeight = view.Terrain?.Height ?? 0,
            ActiveCampaignLevelId = view.ActiveCampaignLevelId,
            Generator = view.Terrain is null ? null : Factory.SavedGenerator(view.Terrain),
            MapId = Factory.CampaignMapId(view.Terrain),
            Snapshot = Convert.ToBase64String(host.CreateSnapshot())
        };
        GameDataPaths.CreateDirectory(SavesDirectory);
        AtomicFile.WriteAllText(SlotPath(slotId), JsonSerializer.Serialize(file, JsonOptions));
    }

    /// <summary>
    /// <see cref="TryLoadSlot"/> with the reason as its Italian detail (<see cref="SaveLoadFailure.Detail"/>), or null
    /// for a missing slot.
    /// </summary>
    public static bool TryLoad(
        FactoryContent content, string slotId, out SimHost? host, out string? error, string? mapsDirectory = null)
    {
        var ok = TryLoadSlot(content, slotId, out host, out var failure, mapsDirectory);
        error = failure?.Detail;
        return ok;
    }

    /// <summary>
    /// Loads a slot into a new host (empty command queue, fresh clock); never throws.
    /// <paramref name="failure"/> says why it did not load, or is null for a missing slot.
    /// <paramref name="mapsDirectory"/> is the folder of the campaign map files (needed by campaign saves).
    /// </summary>
    public static bool TryLoadSlot(
        FactoryContent content, string slotId, out SimHost? host, out SaveLoadFailure? failure, string? mapsDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            var file = ReadFile(slotId);
            host = SimHost.FromSnapshot(content, Convert.FromBase64String(file.Snapshot!), mapsDirectory);
            failure = null;
            return true;
        }
        catch (Exception ex)
        {
            failure = FailureOf(ex);
        }

        host = null;
        return false;
    }

    /// <summary>
    /// Reads and validates a save file anywhere on disk (envelope only: version, snapshot present); never throws.
    /// <paramref name="failure"/> says why it is not usable, or is null for a missing file.
    /// </summary>
    public static bool TryReadSaveFile(string path, out SaveFileData? data, out SaveLoadFailure? failure)
    {
        try
        {
            data = ReadSaveFile(path);
            failure = null;
            return true;
        }
        catch (Exception ex)
        {
            data = null;
            failure = FailureOf(ex);
            return false;
        }
    }

    private static SaveLoadFailure? FailureOf(Exception ex) => ex switch
    {
        SaveMapException map => new SaveLoadFailure(map.Code, map.Argument, map.Message),
        // Only "older version" has its own reason; technical detail (future version, bad snapshot)
        // collapses into "unreadable".
        InvalidDataException { Message: OlderVersionMessage } =>
            new SaveLoadFailure(SaveLoadError.OlderVersion, null, OlderVersionMessage),
        FileNotFoundException => null, // missing file: callers show their own "no save" text
        _ => new SaveLoadFailure(SaveLoadError.Unreadable, null, UnreadableMessage)
    };

    private static SaveFileData ReadFile(string slotId) => ReadSaveFile(SlotPath(slotId));

    /// <summary>Reads and validates the file envelope (version, snapshot present).</summary>
    private static SaveFileData ReadSaveFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Save non trovato: {path}");
        }

        var data = JsonSerializer.Deserialize<SaveFileData>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException(UnreadableMessage);
        if (data.Version >= 1 && data.Version < FactorySaveData.MinReadableVersion)
        {
            throw new InvalidDataException(OlderVersionMessage);
        }

        if (data.Version < 1 || data.Version > FactorySaveData.CurrentVersion)
        {
            throw new InvalidDataException($"Versione save non supportata: {data.Version}");
        }

        if (string.IsNullOrEmpty(data.Snapshot))
        {
            throw new InvalidDataException(UnreadableMessage);
        }

        return data;
    }

    public static void Delete(string slotId)
    {
        var path = SlotPath(slotId);
        if (File.Exists(path))
        {
            GameDataPaths.EnsureWritable(path);
            File.Delete(path);
        }
    }

    /// <summary>Copy continua (or any source) into a new timestamped slot; null if the source is not loadable.</summary>
    public static string? DuplicateSlot(string sourceSlotId)
    {
        try
        {
            ReadFile(sourceSlotId);
        }
        catch (Exception)
        {
            return null;
        }

        var id = CreateSlotId();
        AtomicFile.Copy(SlotPath(sourceSlotId), SlotPath(id));
        return id;
    }
}
