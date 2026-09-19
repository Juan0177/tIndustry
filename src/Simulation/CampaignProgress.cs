using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Logistics;

/// <summary>
/// Persisted campaign unlock/completion state under AppData (<c>campaignProgress.json</c>).
/// </summary>
public sealed class CampaignProgress
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public List<string> CompletedLevelIds { get; set; } = [];

    public static string ProgressPath =>
        Path.Combine(GameSettings.SettingsDirectory, "campaignProgress.json");

    public static CampaignProgress Load()
    {
        try
        {
            if (!File.Exists(ProgressPath))
            {
                return new CampaignProgress();
            }

            return JsonSerializer.Deserialize<CampaignProgress>(File.ReadAllText(ProgressPath), JsonOptions)
                ?? new CampaignProgress();
        }
        catch
        {
            return new CampaignProgress();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(GameSettings.SettingsDirectory);
        // Deduplicate while preserving order.
        CompletedLevelIds = CompletedLevelIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        File.WriteAllText(ProgressPath, JsonSerializer.Serialize(this, JsonOptions));
    }

    public bool IsCompleted(string levelId) =>
        CompletedLevelIds.Contains(levelId, StringComparer.Ordinal);

    public bool IsUnlocked(CampaignLevelDefinition level, CampaignCatalog catalog)
    {
        if (catalog.Levels.Count == 0)
        {
            return false;
        }

        // First level is always playable.
        if (string.Equals(level.Id, catalog.Levels[0].Id, StringComparison.Ordinal))
        {
            return true;
        }

        // Unlocked when the previous level that points here is completed, or already finished.
        if (IsCompleted(level.Id))
        {
            return true;
        }

        foreach (var candidate in catalog.Levels)
        {
            if (string.Equals(candidate.UnlocksNext, level.Id, StringComparison.Ordinal)
                && IsCompleted(candidate.Id))
            {
                return true;
            }
        }

        return false;
    }

    public void MarkComplete(string levelId)
    {
        if (!CompletedLevelIds.Contains(levelId, StringComparer.Ordinal))
        {
            CompletedLevelIds.Add(levelId);
        }

        Save();
    }

    /// <summary>Current progress toward an objective (0…target).</summary>
    public static int GetObjectiveCurrent(
        CampaignObjectiveDefinition objective,
        EconomyWallet wallet,
        EconomySession session,
        ResearchState research)
    {
        var target = Math.Max(1, objective.Amount);
        return objective.Type switch
        {
            CampaignObjectiveType.EarnMoney =>
                Math.Clamp(session.SaleIncome, 0, target),
            CampaignObjectiveType.StockItem when !string.IsNullOrWhiteSpace(objective.ItemId) =>
                Math.Clamp(wallet.MaterialCount(objective.ItemId!), 0, target),
            CampaignObjectiveType.UnlockResearch when !string.IsNullOrWhiteSpace(objective.StructureId) =>
                research.IsUnlocked(objective.StructureId!) ? 1 : 0,
            CampaignObjectiveType.SellItem when !string.IsNullOrWhiteSpace(objective.ItemId) =>
                Math.Clamp(session.SoldByItem.GetValueOrDefault(objective.ItemId!), 0, target),
            _ => 0
        };
    }

    public static int GetObjectiveTarget(CampaignObjectiveDefinition objective) =>
        objective.Type == CampaignObjectiveType.UnlockResearch
            ? 1
            : Math.Max(1, objective.Amount);

    public static bool IsObjectiveComplete(
        CampaignObjectiveDefinition objective,
        EconomyWallet wallet,
        EconomySession session,
        ResearchState research) =>
        GetObjectiveCurrent(objective, wallet, session, research)
        >= GetObjectiveTarget(objective);

    public static bool AreAllObjectivesComplete(
        CampaignLevelDefinition level,
        EconomyWallet wallet,
        EconomySession session,
        ResearchState research)
    {
        var objectives = level.Objectives;
        if (objectives is null || objectives.Count == 0)
        {
            return false;
        }

        return objectives.All(objective =>
            IsObjectiveComplete(objective, wallet, session, research));
    }
}
