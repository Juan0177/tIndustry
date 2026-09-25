using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Shared;

/// <summary>Godot factory-slice save (v4: active campaign level). v1–v3 load with null level.</summary>
public sealed class FactorySliceSaveData
{
    public const int CurrentVersion = 4;

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
    public string FilterItemId { get; set; } = ExtractorStub.DefaultFilterItemId;
    public float Progress { get; set; }
}

public sealed class PowerNodeSaveDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public string DefinitionId { get; set; } = PowerNodeStub.Tier1Id;
}

public sealed class MinerSaveDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Direction { get; set; } = "East";
    public float Progress { get; set; }
    public int EjectIndex { get; set; }
    public string OutputItemId { get; set; } = MinerProducer.DefaultOutputItemId;
    public string DefinitionId { get; set; } = MinerProducer.BasicId;
}

public sealed class CraftSaveDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Direction { get; set; } = "East";
    public string RecipeId { get; set; } = "smelt-iron";
    public string DefinitionId { get; set; } = SmelterStub.SmelterBuildingId;
    public float Progress { get; set; }
    public bool IsCrafting { get; set; }
    public Dictionary<string, int> InputBuffer { get; set; } = new(StringComparer.Ordinal);
    public List<string> OutputQueue { get; set; } = [];
    public int EjectIndex { get; set; }
    public long ItemsCrafted { get; set; }
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

/// <summary>JSON slots under LocalAppData/tIndustry/godot-saves/.</summary>
public static class FactorySliceSaveStore
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

    public static string SavesDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "tIndustry",
            "godot-saves");

    public static string SlotPath(string slotId)
    {
        var safe = string.IsNullOrWhiteSpace(slotId) ? ContinueSlotId : slotId.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(c, '_');
        }

        return Path.Combine(SavesDirectory, $"{safe}.json");
    }

    public static bool Exists(string slotId) => File.Exists(SlotPath(slotId));

    public static void Save(string slotId, FactorySliceSaveData data)
    {
        Directory.CreateDirectory(SavesDirectory);
        data.Version = FactorySliceSaveData.CurrentVersion;
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(SlotPath(slotId), json);
    }

    public static FactorySliceSaveData Load(string slotId)
    {
        var path = SlotPath(slotId);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Save non trovato: {path}");
        }

        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<FactorySliceSaveData>(json, JsonOptions)
            ?? throw new InvalidDataException($"Save JSON non valido: {path}");
        if (data.Version < 1 || data.Version > FactorySliceSaveData.CurrentVersion)
        {
            throw new InvalidDataException($"Versione save non supportata: {data.Version}");
        }

        return data;
    }

    public static bool TryLoad(string slotId, out FactorySliceSaveData? data)
    {
        try
        {
            data = Load(slotId);
            return true;
        }
        catch
        {
            data = null;
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
}
