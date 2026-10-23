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
/// <paramref name="DayNight"/> is the level's time (campaign.json <c>dayNight</c>: <c>ciclo</c>, <c>giorno</c> or
/// <c>notte</c>, see <see cref="CampaignCatalog.DayNightNames"/>); every level in the file must say it.
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
    [property: JsonConverter(typeof(CampaignDayNightConverter))] DayNightMode DayNight = DayNightMode.Cycle);

/// <summary>A level's <c>dayNight</c> by its campaign name (<see cref="CampaignCatalog.DayNightNames"/>).</summary>
public sealed class CampaignDayNightConverter : JsonConverter<DayNightMode>
{
    public override DayNightMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String
        && CampaignCatalog.DayNightNames.TryGetValue(reader.GetString()!, out var mode)
            ? mode
            : throw new JsonException("dayNight: ciclo, giorno o notte.");

    public override void Write(Utf8JsonWriter writer, DayNightMode value, JsonSerializerOptions options) =>
        writer.WriteStringValue(CampaignCatalog.DayNightNames.First(pair => pair.Value == value).Key);
}

/// <summary>Thin campaign catalog loaded from campaign.json (Godot / Shared).</summary>
public sealed class CampaignCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>Names of the world's time in campaign.json (and in the agents' journals): one per <see cref="DayNightMode"/>.</summary>
    public static readonly IReadOnlyDictionary<string, DayNightMode> DayNightNames =
        new Dictionary<string, DayNightMode>(StringComparer.Ordinal)
        {
            ["ciclo"] = DayNightMode.Cycle,
            ["giorno"] = DayNightMode.AlwaysDay,
            ["notte"] = DayNightMode.AlwaysNight
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
        var json = File.ReadAllText(path);
        CheckDayNight(json);
        var catalog = JsonSerializer.Deserialize<CampaignCatalog>(json, JsonOptions)
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

    /// <summary>
    /// Every level says its time (<c>dayNight</c>, one of <see cref="DayNightNames"/>): a missing or unknown one is a
    /// load error naming the level, never a silent default.
    /// </summary>
    private static void CheckDayNight(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var levels = document.RootElement.EnumerateObject()
            .FirstOrDefault(p => string.Equals(p.Name, "levels", StringComparison.OrdinalIgnoreCase)).Value;
        if (levels.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var level in levels.EnumerateArray())
        {
            index++;
            if (level.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var properties = level.EnumerateObject().ToList();
            var id = properties.FirstOrDefault(p => string.Equals(p.Name, "id", StringComparison.OrdinalIgnoreCase)).Value;
            var name = id.ValueKind == JsonValueKind.String ? id.GetString() : $"#{index}";
            var dayNight = properties
                .FirstOrDefault(p => string.Equals(p.Name, "dayNight", StringComparison.OrdinalIgnoreCase)).Value;
            if (dayNight.ValueKind != JsonValueKind.String || !DayNightNames.ContainsKey(dayNight.GetString()!))
            {
                throw new InvalidDataException(
                    $"Campagna: il livello '{name}' deve dire il suo tempo con dayNight (ciclo, giorno o notte).");
            }
        }
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
