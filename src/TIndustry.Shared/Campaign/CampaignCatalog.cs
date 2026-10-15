using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Shared;

/// <summary>Objective kinds checked each tick during a campaign level.</summary>
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

/// <summary>Thin campaign catalog loaded from campaign.json (Godot / Shared).</summary>
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

    public static CampaignCatalog Load(string path)
    {
        var catalog = JsonSerializer.Deserialize<CampaignCatalog>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Campagna non valida: {path}");
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

        return string.Join(" · ", objectives.Select(o => FormatObjectiveShort(o)));
    }

    public static string FormatObjectiveShort(
        CampaignObjectiveDefinition objective,
        Func<string, string>? itemName = null)
    {
        if (!string.IsNullOrWhiteSpace(objective.Label))
        {
            return objective.Label!;
        }

        string Name(string? id) =>
            string.IsNullOrWhiteSpace(id) ? "?" : (itemName?.Invoke(id!) ?? id!);

        return objective.Type switch
        {
            CampaignObjectiveType.EarnMoney => $"Guadagna ${objective.Amount}",
            CampaignObjectiveType.StockItem =>
                $"Magazzino {objective.Amount} {Name(objective.ItemId)}",
            CampaignObjectiveType.UnlockResearch => $"Sblocca {objective.StructureId}",
            CampaignObjectiveType.SellItem =>
                $"Vendi {objective.Amount} {Name(objective.ItemId)}",
            _ => "Obiettivo"
        };
    }

    public static string FormatObjectiveProgress(
        CampaignObjectiveDefinition objective,
        int current,
        Func<string, string>? itemName = null)
    {
        var label = FormatObjectiveShort(objective, itemName);
        var target = Math.Max(1, objective.Amount);
        if (objective.Type == CampaignObjectiveType.UnlockResearch)
        {
            return current >= 1 ? $"{label} ✓" : label;
        }

        return $"{label} ({Math.Min(current, target)}/{target})";
    }
}
