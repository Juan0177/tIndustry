using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Shared;

/// <summary>Godot factory-slice save (v4: active campaign level). v1–v3 load with null level.</summary>
public sealed class FactorySaveData
{
    public const int CurrentVersion = 9;

    public int Version { get; set; } = CurrentVersion;
    public long NextItemId { get; set; } = 1;
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
    /// <summary>Terrain seed (0 = none / legacy save).</summary>
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
    public int X { get; set; }
    public int Y { get; set; }
    public string Direction { get; set; } = "East";
    public string FilterItemId { get; set; } = Extractor.DefaultFilterItemId;
    public float Progress { get; set; }
}

public sealed class PowerNodeSaveDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public string DefinitionId { get; set; } = PowerNode.Tier1Id;
}

public sealed class MinerSaveDto
{
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

    private static readonly JsonSerializerOptions JsonOptions = new()
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

    public static string CreateSlotId() =>
        $"slot-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

    public static bool Exists(string slotId) => File.Exists(SlotPath(slotId));

    public static IReadOnlyList<SaveSlotInfo> ListSlots()
    {
        Directory.CreateDirectory(SavesDirectory);
        var list = new List<SaveSlotInfo>();
        foreach (var path in Directory.EnumerateFiles(SavesDirectory, "*.json"))
        {
            try
            {
                var id = Path.GetFileNameWithoutExtension(path);
                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<FactorySaveData>(json, JsonOptions);
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

    public static void Save(string slotId, FactorySaveData data)
    {
        Directory.CreateDirectory(SavesDirectory);
        data.Version = FactorySaveData.CurrentVersion;
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(SlotPath(slotId), json);
    }

    public static FactorySaveData Load(string slotId)
    {
        var path = SlotPath(slotId);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Save non trovato: {path}");
        }

        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<FactorySaveData>(json, JsonOptions)
            ?? throw new InvalidDataException($"Save JSON non valido: {path}");
        if (data.Version >= 1 && data.Version < FactorySaveData.CurrentVersion)
        {
            throw new InvalidDataException("Salvataggio di una versione precedente: non compatibile.");
        }

        if (data.Version < 1 || data.Version > FactorySaveData.CurrentVersion)
        {
            throw new InvalidDataException($"Versione save non supportata: {data.Version}");
        }

        return data;
    }

    public static bool TryLoad(string slotId, out FactorySaveData? data) =>
        TryLoad(slotId, out data, out _);

    /// <summary>Like <see cref="Load"/> but never throws; <paramref name="error"/> carries the player-facing reason.</summary>
    public static bool TryLoad(string slotId, out FactorySaveData? data, out string? error)
    {
        try
        {
            data = Load(slotId);
            error = null;
            return true;
        }
        catch (InvalidDataException ex)
        {
            data = null;
            error = ex.Message;
            return false;
        }
        catch (Exception)
        {
            data = null;
            error = null;
            return false;
        }
    }

    public static void Delete(string slotId)
    {
        var path = SlotPath(slotId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>Copy continua (or any source) into a new timestamped slot.</summary>
    public static string? DuplicateSlot(string sourceSlotId)
    {
        if (!TryLoad(sourceSlotId, out var data) || data is null)
        {
            return null;
        }

        var id = CreateSlotId();
        Save(id, data);
        return id;
    }
}
