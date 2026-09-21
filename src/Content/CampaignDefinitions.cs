using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Logistics;

/// <summary>Objective kinds checked each sim tick during a campaign level.</summary>
public enum CampaignObjectiveType
{
    EarnMoney,
    StockItem,
    UnlockResearch,
    SellItem
}

public sealed record CampaignObjectiveDefinition(
    CampaignObjectiveType Type,
    int Amount = 1,
    string? ItemId = null,
    string? StructureId = null,
    string? Label = null);

public sealed record CampaignLevelDefinition(
    string Id,
    string Name,
    string Description,
    int Seed,
    int MapWidth = 1000,
    int MapHeight = 1000,
    int StartingMoney = 180,
    IReadOnlyList<ResourceAmount>? StartingMaterials = null,
    IReadOnlyList<CampaignObjectiveDefinition>? Objectives = null,
    string? UnlocksNext = null);

public sealed class CampaignCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public required IReadOnlyList<CampaignLevelDefinition> Levels { get; init; }

    public CampaignLevelDefinition? Find(string id) =>
        Levels.FirstOrDefault(level => level.Id == id);

    public CampaignLevelDefinition? FirstLevel => Levels.Count > 0 ? Levels[0] : null;

    public CampaignLevelDefinition? NextAfter(CampaignLevelDefinition level)
    {
        if (string.IsNullOrWhiteSpace(level.UnlocksNext))
        {
            return null;
        }

        return Find(level.UnlocksNext);
    }

    public static string SeedCampaignPath =>
        Path.Combine(AppContext.BaseDirectory, "data", "campaign.json");

    public static string UserCampaignPath =>
        Path.Combine(GameContentStore.ContentDirectory, "campaign.json");

    /// <summary>
    /// Copy seed campaign.json into AppData on first launch; thereafter merge missing
    /// level ids and sync authored fields (name/objectives/…) from seed so updates ship.
    /// </summary>
    public static string EnsureUserCampaign()
    {
        Directory.CreateDirectory(GameContentStore.ContentDirectory);
        var seed = SeedCampaignPath;
        if (!File.Exists(seed))
        {
            throw new FileNotFoundException(
                "Seed campagna mancante: campaign.json non trovato nel package.",
                seed);
        }

        if (!File.Exists(UserCampaignPath))
        {
            File.Copy(seed, UserCampaignPath, overwrite: false);
        }
        else
        {
            var changed = false;
            changed |= MergeMissingSeedLevels(UserCampaignPath, seed);
            changed |= SyncSeedLevelFields(UserCampaignPath, seed);
            _ = changed;
        }

        return UserCampaignPath;
    }

    /// <summary>
    /// Appends seed levels whose ids are absent from the user catalog (additive only).
    /// </summary>
    public static bool MergeMissingSeedLevels(string userCampaignPath, string seedCampaignPath)
    {
        var user = Load(userCampaignPath);
        var seed = Load(seedCampaignPath);
        var levels = user.Levels.ToList();
        var known = new HashSet<string>(levels.Select(level => level.Id), StringComparer.Ordinal);
        var added = false;
        foreach (var level in seed.Levels)
        {
            if (known.Add(level.Id))
            {
                levels.Add(level);
                added = true;
            }
        }

        if (!added)
        {
            return false;
        }

        WriteLevels(userCampaignPath, levels);
        return true;
    }

    /// <summary>
    /// Overwrites authored fields from seed for matching level ids (balancing + chain).
    /// Preserves user-only custom levels. Returns true when the file was updated.
    /// </summary>
    public static bool SyncSeedLevelFields(string userCampaignPath, string seedCampaignPath)
    {
        var user = Load(userCampaignPath);
        var seed = Load(seedCampaignPath);
        var seedById = seed.Levels.ToDictionary(level => level.Id, StringComparer.Ordinal);
        var levels = user.Levels.ToList();
        var changed = false;

        for (var i = 0; i < levels.Count; i++)
        {
            if (!seedById.TryGetValue(levels[i].Id, out var fromSeed))
            {
                continue;
            }

            if (LevelFieldsEqual(levels[i], fromSeed))
            {
                continue;
            }

            levels[i] = fromSeed;
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        WriteLevels(userCampaignPath, levels);
        return true;
    }

    private static bool LevelFieldsEqual(CampaignLevelDefinition left, CampaignLevelDefinition right)
    {
        if (!string.Equals(left.Name, right.Name, StringComparison.Ordinal)
            || !string.Equals(left.Description, right.Description, StringComparison.Ordinal)
            || left.Seed != right.Seed
            || left.MapWidth != right.MapWidth
            || left.MapHeight != right.MapHeight
            || left.StartingMoney != right.StartingMoney
            || !string.Equals(left.UnlocksNext, right.UnlocksNext, StringComparison.Ordinal))
        {
            return false;
        }

        return MaterialsEqual(left.StartingMaterials, right.StartingMaterials)
            && ObjectivesEqual(left.Objectives, right.Objectives);
    }

    private static bool MaterialsEqual(
        IReadOnlyList<ResourceAmount>? left,
        IReadOnlyList<ResourceAmount>? right)
    {
        var a = left ?? Array.Empty<ResourceAmount>();
        var b = right ?? Array.Empty<ResourceAmount>();
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i].ItemId, b[i].ItemId, StringComparison.Ordinal)
                || a[i].Amount != b[i].Amount)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ObjectivesEqual(
        IReadOnlyList<CampaignObjectiveDefinition>? left,
        IReadOnlyList<CampaignObjectiveDefinition>? right)
    {
        var a = left ?? Array.Empty<CampaignObjectiveDefinition>();
        var b = right ?? Array.Empty<CampaignObjectiveDefinition>();
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i].Type != b[i].Type
                || a[i].Amount != b[i].Amount
                || !string.Equals(a[i].ItemId, b[i].ItemId, StringComparison.Ordinal)
                || !string.Equals(a[i].StructureId, b[i].StructureId, StringComparison.Ordinal)
                || !string.Equals(a[i].Label, b[i].Label, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static void WriteLevels(string path, IReadOnlyList<CampaignLevelDefinition> levels)
    {
        var catalog = new CampaignCatalog { Levels = levels.ToList() };
        File.WriteAllText(path, JsonSerializer.Serialize(catalog, WriteOptions));
    }

    public static CampaignCatalog Load(string? path = null)
    {
        var resolved = path ?? EnsureUserCampaign();
        var catalog = JsonSerializer.Deserialize<CampaignCatalog>(File.ReadAllText(resolved), JsonOptions)
            ?? throw new InvalidDataException($"Campagna non valida: {resolved}");
        if (catalog.Levels.Count == 0)
        {
            throw new InvalidDataException("La campagna non contiene livelli.");
        }

        return catalog;
    }

    public EconomyWallet CreateWallet(CampaignLevelDefinition level)
    {
        var materials = new Dictionary<string, int>(StringComparer.Ordinal);
        if (level.StartingMaterials is not null)
        {
            foreach (var entry in level.StartingMaterials)
            {
                materials[entry.ItemId] = materials.GetValueOrDefault(entry.ItemId) + entry.Amount;
            }
        }

        return new EconomyWallet(level.StartingMoney, materials);
    }

    public string ObjectiveSummary(CampaignLevelDefinition level)
    {
        var objectives = level.Objectives ?? [];
        if (objectives.Count == 0)
        {
            return "Nessun obiettivo";
        }

        return string.Join(" · ", objectives.Select(FormatObjectiveShort));
    }

    public static string FormatObjectiveShort(CampaignObjectiveDefinition objective)
    {
        if (!string.IsNullOrWhiteSpace(objective.Label))
        {
            return objective.Label!;
        }

        return objective.Type switch
        {
            CampaignObjectiveType.EarnMoney => $"Guadagna ${objective.Amount}",
            CampaignObjectiveType.StockItem => $"Stock {objective.Amount} {objective.ItemId}",
            CampaignObjectiveType.UnlockResearch => $"Sblocca {objective.StructureId}",
            CampaignObjectiveType.SellItem => $"Vendi {objective.Amount} {objective.ItemId}",
            _ => "Obiettivo"
        };
    }

    public static string FormatObjectiveProgress(
        CampaignObjectiveDefinition objective,
        int current)
    {
        var label = FormatObjectiveShort(objective);
        var target = Math.Max(1, objective.Amount);
        if (objective.Type == CampaignObjectiveType.UnlockResearch)
        {
            return current >= 1 ? $"{label} ✓" : label;
        }

        return $"{label} ({Math.Min(current, target)}/{target})";
    }
}
