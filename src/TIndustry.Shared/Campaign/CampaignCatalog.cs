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

/// <summary>
/// A campaign level. With <paramref name="Map"/> (id of a map in <c>data/maps/</c>, see <see cref="CampaignMapFile"/>)
/// the size, terrain and Core come from that file and <paramref name="MapWidth"/>, <paramref name="MapHeight"/> and
/// <paramref name="Seed"/> only serve the classic generator (tests with explicit sizes, older clients).
/// <paramref name="Night"/> plays the level at night (<see cref="DayNightMode.AlwaysNight"/>).
/// </summary>
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
    string? UnlocksNext = null,
    int RefundPercent = 100,
    string? Map = null,
    bool Night = false);

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

        foreach (var level in catalog.Levels)
        {
            if (level.RefundPercent is < 0 or > 100)
            {
                throw new InvalidDataException($"Campagna: refundPercent di '{level.Id}' fuori da 0–100.");
            }
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
}
