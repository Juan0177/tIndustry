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

    /// <summary>Copy seed campaign.json into AppData on first launch (like content.json).</summary>
    public static string EnsureUserCampaign()
    {
        Directory.CreateDirectory(GameContentStore.ContentDirectory);
        if (!File.Exists(UserCampaignPath))
        {
            var seed = SeedCampaignPath;
            if (!File.Exists(seed))
            {
                throw new FileNotFoundException(
                    "Seed campagna mancante: campaign.json non trovato nel package.",
                    seed);
            }

            File.Copy(seed, UserCampaignPath, overwrite: false);
        }

        return UserCampaignPath;
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
