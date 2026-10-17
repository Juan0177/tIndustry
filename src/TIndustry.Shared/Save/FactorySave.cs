using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Shared;

/// <summary>
/// Full game state as stored in the canonical snapshot (see <see cref="Snapshot"/>). Version 10 is the
/// first format where a save file is metadata plus this snapshot; older files are not compatible.
/// </summary>
public sealed class FactorySaveData
{
    public const int CurrentVersion = 10;

    public int Version { get; set; } = CurrentVersion;
    public long NextItemId { get; set; } = 1;
    /// <summary>Next stable building id.</summary>
    public long NextEntityId { get; set; }
    /// <summary>Fixed simulation ticks elapsed.</summary>
    public long CurrentTick { get; set; }
    public long CoreDeliveredItems { get; set; }
    public int CoreX { get; set; } = 9;
    public int CoreY { get; set; } = 14;
    public int CoreSize { get; set; } = 2;
    public int Money { get; set; }
    public Dictionary<string, int> Materials { get; set; } = new(StringComparer.Ordinal);
    public List<string> UnlockedStructures { get; set; } = [];
    public int StartingMoney { get; set; }
    public int SaleIncome { get; set; }
    public Dictionary<string, int> SoldByItem { get; set; } = new(StringComparer.Ordinal);
    public string? ActiveCampaignLevelId { get; set; }
    public bool AutoSellAtCore { get; set; }
    public int CoreUpgradeLevel { get; set; }
    public int CoreSaleBonusPercent { get; set; }
    public float PowerBuffer { get; set; }
    public float PowerCapacity { get; set; }
    /// <summary>Terrain seed (0 = no terrain).</summary>
    public int Seed { get; set; }
    public int MapWidth { get; set; }
    public int MapHeight { get; set; }
    public List<MinerSaveDto> Miners { get; set; } = [];
    public List<CraftSaveDto> Smelters { get; set; } = [];
    public List<CraftSaveDto> Assemblers { get; set; } = [];
    public List<GeneratorSaveDto> Generators { get; set; } = [];
    public List<ExtractorSaveDto> Extractors { get; set; } = [];
    public List<PowerNodeSaveDto> PowerNodes { get; set; } = [];
    public List<BeltSaveDto> Belts { get; set; } = [];
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
/// Save file v10 (one JSON file per slot): the fields the slot list needs, plus the canonical snapshot
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
    /// <summary>Canonical snapshot bytes, base64.</summary>
    public string? Snapshot { get; set; }
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
}

/// <summary>JSON slots under LocalAppData/tIndustry/godot-saves/.</summary>
public static class FactorySaveStore
{
    public const string ContinueSlotId = "continua";
    public const string QuickSlotId = "slot-1";

    /// <summary>Continua as it was before this session's first autosave replaced it (see <see cref="ContinueAutoSave"/>).</summary>
    public const string ContinueBackupSlotId = "continua.prev";

    private const string OlderVersionMessage = "Salvataggio di una versione precedente: non compatibile.";
    private const string UnreadableMessage = "Salvataggio non leggibile.";

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

            try
            {
                var id = Path.GetFileNameWithoutExtension(path);
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<SaveFileData>(json, JsonOptions);
                if (data is null)
                {
                    continue;
                }

                list.Add(new SaveSlotInfo
                {
                    Id = id,
                    Path = path,
                    ModifiedUtc = File.GetLastWriteTimeUtc(path),
                    Money = data.Money,
                    Seed = data.Seed,
                    MapWidth = data.MapWidth,
                    MapHeight = data.MapHeight,
                    ActiveCampaignLevelId = data.ActiveCampaignLevelId
                });
            }
            catch
            {
                // Skip corrupt / unsupported files (Raylib parity).
            }
        }

        return list
            .OrderBy(s => s.Id == ContinueSlotId ? 0 : 1)
            .ThenByDescending(s => s.ModifiedUtc)
            .ToList();
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
            Snapshot = Convert.ToBase64String(host.CreateSnapshot())
        };
        GameDataPaths.CreateDirectory(SavesDirectory);
        AtomicFile.WriteAllText(SlotPath(slotId), JsonSerializer.Serialize(file, JsonOptions));
    }

    /// <summary>
    /// Loads a slot into a new host (empty command queue, fresh clock); never throws.
    /// <paramref name="error"/> carries the player-facing reason, or null for a missing slot.
    /// </summary>
    public static bool TryLoad(FactoryContent content, string slotId, out SimHost? host, out string? error)
    {
        ArgumentNullException.ThrowIfNull(content);
        try
        {
            var file = ReadFile(slotId);
            host = SimHost.FromSnapshot(content, Convert.FromBase64String(file.Snapshot!));
            error = null;
            return true;
        }
        catch (InvalidDataException ex)
        {
            // Only the "older version" text is meant for the player; technical detail
            // (future version, bad snapshot) collapses into the generic unreadable message.
            error = ex.Message == OlderVersionMessage ? OlderVersionMessage : UnreadableMessage;
        }
        catch (FileNotFoundException)
        {
            error = null; // missing slot: callers show their own "no save" text
        }
        catch (Exception)
        {
            error = UnreadableMessage;
        }

        host = null;
        return false;
    }

    /// <summary>Reads and validates the file envelope (version, snapshot present).</summary>
    private static SaveFileData ReadFile(string slotId)
    {
        var path = SlotPath(slotId);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Save non trovato: {path}");
        }

        var data = JsonSerializer.Deserialize<SaveFileData>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException(UnreadableMessage);
        if (data.Version >= 1 && data.Version < FactorySaveData.CurrentVersion)
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
