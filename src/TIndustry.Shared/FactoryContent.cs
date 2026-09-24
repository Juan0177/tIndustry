using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Shared;

/// <summary>
/// Loads conveyors, recipes, buildings, market, and structures from content.json
/// without Raylib / AppData / Excel dependencies.
/// </summary>
public sealed class FactoryContent
{
    public required IReadOnlyList<ConveyorDefinition> Conveyors { get; init; }
    public required IReadOnlyList<RecipeDefinition> Recipes { get; init; }
    public IReadOnlyList<BuildingDefinition> Buildings { get; init; } = [];
    public IReadOnlyList<MarketItemDefinition> Market { get; init; } = [];
    public IReadOnlyList<StructureDefinition> Structures { get; init; } = [];

    public ConveyorDefinition? FindConveyor(string id) =>
        Conveyors.FirstOrDefault(c => c.Id == id);

    public ConveyorDefinition RequireConveyor(string id = "conveyor-basic") =>
        FindConveyor(id)
        ?? throw new InvalidDataException($"Conveyor '{id}' non trovato nel content caricato.");

    public RecipeDefinition? FindRecipe(string id) =>
        Recipes.FirstOrDefault(r => r.Id == id);

    public BuildingDefinition? FindBuilding(string id) =>
        Buildings.FirstOrDefault(b => b.Id == id);

    public MarketItemDefinition? FindMarketItem(string itemId) =>
        Market.FirstOrDefault(m => m.ItemId == itemId);

    public string DisplayName(string itemId) =>
        FindMarketItem(itemId)?.DisplayName ?? itemId;

    public static FactoryContent Load(string contentJsonPath)
    {
        var json = File.ReadAllText(contentJsonPath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        var root = JsonSerializer.Deserialize<RootDto>(json, options)
            ?? throw new InvalidDataException($"content.json non valido: {contentJsonPath}");

        var conveyors = root.Conveyors.Select(MapConveyor).ToList();
        var recipes = root.Recipes.Select(MapRecipe).ToList();
        var buildings = root.Buildings.Select(MapBuilding).ToList();
        var market = root.Market.Select(MapMarket).ToList();
        var structures = root.Structures.Select(MapStructure).ToList();

        return new FactoryContent
        {
            Conveyors = conveyors,
            Recipes = recipes,
            Buildings = buildings,
            Market = market,
            Structures = structures
        };
    }

    private static ConveyorDefinition MapConveyor(ConveyorDto c) =>
        new(
            c.Id,
            c.Tier,
            c.RateItemsPerSecond,
            Math.Max(1, c.Capacity),
            c.ItemSpacing <= 0 ? 1f : c.ItemSpacing,
            c.MoneyCost,
            MapAmounts(c.BuildCost),
            MapUnlock(c.Unlock),
            ParseKind(c.Kind));

    private static RecipeDefinition MapRecipe(RecipeDto r) =>
        new(r.Id, r.DurationSeconds, MapAmounts(r.Inputs), MapAmounts(r.Outputs));

    private static BuildingDefinition MapBuilding(BuildingDto b) =>
        new(b.Id, b.MoneyCost, MapAmounts(b.BuildCost), b.RefundPercent <= 0 ? 100 : b.RefundPercent);

    private static MarketItemDefinition MapMarket(MarketDto m) =>
        new(m.ItemId, string.IsNullOrWhiteSpace(m.DisplayName) ? m.ItemId : m.DisplayName, m.SellPrice);

    private static StructureDefinition MapStructure(StructureDto s) =>
        new(
            s.Id,
            string.IsNullOrWhiteSpace(s.DisplayName) ? s.Id : s.DisplayName,
            string.IsNullOrWhiteSpace(s.Kind) ? "building" : s.Kind,
            s.UnlockedByDefault,
            MapUnlock(s.Unlock),
            s.IsStub);

    private static IReadOnlyList<ResourceAmount> MapAmounts(List<AmountDto>? list) =>
        (list ?? [])
            .Where(a => !string.IsNullOrWhiteSpace(a.ItemId) && a.Amount > 0)
            .Select(a => new ResourceAmount(a.ItemId, a.Amount))
            .ToList();

    private static UnlockRequirement? MapUnlock(UnlockDto? unlock)
    {
        if (unlock is null)
        {
            return null;
        }

        return new UnlockRequirement(unlock.Money, MapAmounts(unlock.Materials));
    }

    private static LogisticsKind ParseKind(string? kind) =>
        kind?.Trim().ToLowerInvariant() switch
        {
            "junction" => LogisticsKind.Junction,
            "splitter" => LogisticsKind.Splitter,
            "bridge" => LogisticsKind.Bridge,
            "sorter" => LogisticsKind.Sorter,
            _ => LogisticsKind.Belt
        };

    private sealed class RootDto
    {
        public List<ConveyorDto> Conveyors { get; set; } = [];
        public List<RecipeDto> Recipes { get; set; } = [];
        public List<BuildingDto> Buildings { get; set; } = [];
        public List<MarketDto> Market { get; set; } = [];
        public List<StructureDto> Structures { get; set; } = [];
    }

    private sealed class ConveyorDto
    {
        public string Id { get; set; } = "";
        public int Tier { get; set; }
        public float RateItemsPerSecond { get; set; }
        public int Capacity { get; set; } = 1;
        public float ItemSpacing { get; set; } = 1f;
        public int MoneyCost { get; set; }
        public List<AmountDto>? BuildCost { get; set; }
        public UnlockDto? Unlock { get; set; }
        public string? Kind { get; set; }
    }

    private sealed class RecipeDto
    {
        public string Id { get; set; } = "";
        public float DurationSeconds { get; set; }
        public List<AmountDto>? Inputs { get; set; }
        public List<AmountDto>? Outputs { get; set; }
    }

    private sealed class BuildingDto
    {
        public string Id { get; set; } = "";
        public int MoneyCost { get; set; }
        public List<AmountDto>? BuildCost { get; set; }
        public int RefundPercent { get; set; } = 100;
    }

    private sealed class MarketDto
    {
        public string ItemId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int SellPrice { get; set; }
    }

    private sealed class StructureDto
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Kind { get; set; } = "";
        public bool UnlockedByDefault { get; set; }
        public UnlockDto? Unlock { get; set; }
        public bool IsStub { get; set; }
    }

    private sealed class AmountDto
    {
        public string ItemId { get; set; } = "";
        public int Amount { get; set; }
    }

    private sealed class UnlockDto
    {
        public int Money { get; set; }
        public List<AmountDto>? Materials { get; set; }
    }
}
