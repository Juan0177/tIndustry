using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Logistics;

public sealed record ResourceAmount(string ItemId, int Amount);

public sealed record UnlockRequirement(
    int Money,
    IReadOnlyList<ResourceAmount> Materials);

public sealed record ConveyorDefinition(
    string Id,
    int Tier,
    float RateItemsPerSecond,
    int Capacity,
    float ItemSpacing,
    int MoneyCost,
    IReadOnlyList<ResourceAmount> BuildCost,
    UnlockRequirement? Unlock);

public sealed record RecipeDefinition(
    string Id,
    float DurationSeconds,
    IReadOnlyList<ResourceAmount> Inputs,
    IReadOnlyList<ResourceAmount> Outputs);

public sealed class GameContent
{
    public required IReadOnlyList<ConveyorDefinition> Conveyors { get; init; }
    public required IReadOnlyList<RecipeDefinition> Recipes { get; init; }
    public IReadOnlyList<StructureDefinition> Structures { get; set; } = [];
    public IReadOnlyList<MarketItemDefinition> Market { get; set; } = [];
    public IReadOnlyList<BuildingDefinition> Buildings { get; set; } = [];
    public EconomyConfig? Economy { get; set; }

    public StructureDefinition? FindStructure(string id) =>
        Structures.FirstOrDefault(structure => structure.Id == id);

    public BuildingDefinition? FindBuilding(string id) =>
        Buildings.FirstOrDefault(building => building.Id == id);

    public MarketCatalog CreateMarket() =>
        Market.Count > 0 ? new MarketCatalog(Market) : MarketCatalog.CreateDefault();

    public EconomyConfig GetEconomy() =>
        Economy ?? new EconomyConfig(
            new CoreUpgradeDefinition(150, [new ResourceAmount("iron-plate", 20)], 25),
            "Rimozione edifici/nastri: rimborso completo (100%).");

    public BuildingDefinition GetBuildingOrDefault(string id) =>
        FindBuilding(id) ?? id switch
        {
            "miner" => new BuildingDefinition("miner", 25, [new ResourceAmount("iron-plate", 4)], 100),
            "smelter" => new BuildingDefinition("smelter", 40, [new ResourceAmount("iron-plate", 6)], 100),
            _ => new BuildingDefinition(id, 0, [], 100)
        };

    public static GameContent Load(string path)
    {
        if (Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return ExcelContentStore.Load(path);
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        var content = JsonSerializer.Deserialize<GameContent>(File.ReadAllText(path), options)
            ?? throw new InvalidDataException($"Contenuto non valido: {path}");
        if (content.Structures.Count == 0)
        {
            content.Structures = BuildLegacyStructures(content);
        }

        if (content.Market.Count == 0)
        {
            content.Market = MarketCatalog.CreateDefault().Items.ToList();
        }

        if (content.Buildings.Count == 0)
        {
            content.Buildings =
            [
                new BuildingDefinition("miner", 25, [new ResourceAmount("iron-plate", 4)], 100),
                new BuildingDefinition("smelter", 40, [new ResourceAmount("iron-plate", 6)], 100)
            ];
        }

        content.Economy ??= new EconomyConfig(
            new CoreUpgradeDefinition(150, [new ResourceAmount("iron-plate", 20)], 25),
            "Rimozione edifici/nastri: rimborso completo (100%).");

        return content;
    }

    public static IReadOnlyList<StructureDefinition> BuildLegacyStructures(GameContent content)
    {
        var structures = new List<StructureDefinition>
        {
            new("conveyor-basic", "Nastro base", StructureKind.Conveyor, true, null),
            new("miner", "Minatore", StructureKind.Building, true, null)
        };

        foreach (var conveyor in content.Conveyors.Where(entry => entry.Unlock is not null))
        {
            structures.Add(new StructureDefinition(
                conveyor.Id,
                conveyor.Id,
                StructureKind.Conveyor,
                false,
                conveyor.Unlock));
        }

        structures.Add(new StructureDefinition(
            "smelter",
            "Forno",
            StructureKind.Building,
            false,
            new UnlockRequirement(100, [new ResourceAmount("iron-plate", 15)])));

        return structures;
    }
}
