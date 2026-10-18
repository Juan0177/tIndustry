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
    public CoreUpgradeDefinition CoreUpgrade { get; init; } =
        new(150, [new ResourceAmount("iron-plate", 20)], 25);

    /// <summary>Mindustry's <c>item.cost</c> per item id (build-time weight); missing items weigh 1.</summary>
    public IReadOnlyDictionary<string, float> BuildWeights { get; init; } =
        new Dictionary<string, float>(StringComparer.Ordinal);

    /// <summary>The player's builder drone (Mindustry's alpha).</summary>
    public UnitDefinition Drone { get; init; } = DefaultDrone;

    /// <summary>Floors and walls of the map (<c>terrain</c> section): biome, colour, buildable.</summary>
    public TerrainTable Terrain { get; init; } = TerrainTable.Empty;

    internal static readonly UnitDefinition DefaultDrone = new(0.5f, 27.5f, 22.5f);

    /// <summary>Build-time weight of one unit of <paramref name="itemId"/> (default 1).</summary>
    public float BuildWeight(string itemId) =>
        BuildWeights.TryGetValue(itemId, out var weight) ? weight : 1f;

    /// <summary>Build-time multiplier of a building or conveyor (default 1).</summary>
    public float BuildCostMultiplier(string structureId) =>
        FindBuilding(structureId)?.BuildCostMultiplier
        ?? FindConveyor(structureId)?.BuildCostMultiplier
        ?? 1f;

    public ConveyorDefinition? FindConveyor(string id) =>
        Conveyors.FirstOrDefault(c => c.Id == id);

    public ConveyorDefinition RequireConveyor(string id = "conveyor-basic") =>
        FindConveyor(id)
        ?? throw new InvalidDataException($"Conveyor '{id}' non trovato nel content caricato.");

    public RecipeDefinition? FindRecipe(string id) =>
        Recipes.FirstOrDefault(r => r.Id == id);

    public IReadOnlyList<RecipeDefinition> SmelterRecipes() =>
        Recipes.Where(r => r.Id.StartsWith("smelt-", StringComparison.Ordinal)).ToList();

    public IReadOnlyList<RecipeDefinition> AssemblerRecipes() =>
        Recipes.Where(r => r.Id.StartsWith("craft-", StringComparison.Ordinal)).ToList();

    public BuildingDefinition? FindBuilding(string id) =>
        Buildings.FirstOrDefault(b => b.Id == id);

    public MarketItemDefinition? FindMarketItem(string itemId) =>
        Market.FirstOrDefault(m => m.ItemId == itemId);

    public string DisplayName(string itemId) =>
        FindMarketItem(itemId)?.DisplayName ?? itemId;

    public StructureDefinition? FindStructure(string id) =>
        Structures.FirstOrDefault(s => s.Id == id);

    public StructureDefinition RequireStructure(string id) =>
        FindStructure(id)
        ?? throw new InvalidDataException($"Structure '{id}' non trovato nel content caricato.");

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
        var coreUpgrade = MapCoreUpgrade(root.Economy?.CoreUpgrade);
        var buildWeights = MapBuildWeights(root.BuildWeights);
        var drone = MapUnit("drone", root.Units?.Drone);
        var terrain = MapTerrain(root.Terrain);
        ValidateLamp(buildings, structures);

        return new FactoryContent
        {
            Conveyors = conveyors,
            Recipes = recipes,
            Buildings = buildings,
            Market = market,
            Structures = structures,
            CoreUpgrade = coreUpgrade,
            BuildWeights = buildWeights,
            Drone = drone,
            Terrain = terrain
        };
    }

    // A lamp without a positive power use would light for free (TrySpendPower(0) always succeeds).
    private static void ValidateLamp(List<BuildingDefinition> buildings, List<StructureDefinition> structures)
    {
        var lamp = buildings.FirstOrDefault(b => b.Id == Lamp.BuildingId);
        var declared = lamp is not null || structures.Any(s => s.Id == Lamp.BuildingId);
        if (declared && !(lamp?.PowerUse > 0f))
        {
            throw new InvalidDataException(
                "content.json: la Lampada deve avere un consumo di corrente (powerUse) maggiore di zero.");
        }
    }

    private static IReadOnlyDictionary<string, float> MapBuildWeights(Dictionary<string, float>? weights)
    {
        var map = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var (itemId, weight) in weights ?? [])
        {
            if (string.IsNullOrWhiteSpace(itemId) || !(weight > 0f) || !float.IsFinite(weight))
            {
                throw new InvalidDataException($"content.json: buildWeights['{itemId}'] deve essere > 0.");
            }

            map[itemId] = weight;
        }

        return map;
    }

    private static TerrainTable MapTerrain(TerrainDto? dto)
    {
        if (dto is null)
        {
            throw new InvalidDataException("content.json: manca la sezione terrain.");
        }

        var floors = (dto.Floors ?? []).Select(f =>
        {
            var kind = ParseTerrainKind<FloorKind>("floors", f.Id);
            CheckTerrainRow("floors", f.Id, f.Biome, f.Color);
            var buildable = f.Buildable ?? true;
            if (buildable != (kind != FloorKind.DeepWater))
            {
                throw new InvalidDataException(
                    $"content.json: terrain.floors '{f.Id}': buildable deve essere {(kind != FloorKind.DeepWater ? "true" : "false")}.");
            }

            if (f.EdgePriority is not { } edgePriority)
            {
                throw new InvalidDataException($"content.json: terrain.floors '{f.Id}': manca edgePriority.");
            }

            if (edgePriority < 0)
            {
                throw new InvalidDataException($"content.json: terrain.floors '{f.Id}': edgePriority deve essere >= 0.");
            }

            return new FloorDef(f.Id!, kind, f.Biome!, f.Color!.ToUpperInvariant(), buildable, edgePriority);
        }).ToList();
        var walls = (dto.Walls ?? []).Select(w =>
        {
            var kind = ParseTerrainKind<WallKind>("walls", w.Id);
            if (kind == WallKind.None)
            {
                throw new InvalidDataException("content.json: terrain.walls non deve contenere 'none'.");
            }

            CheckTerrainRow("walls", w.Id, w.Biome, w.Color);
            return new WallDef(w.Id!, kind, w.Biome!, w.Color!.ToUpperInvariant());
        }).ToList();

        CheckCoverage("floors", floors.Select(f => f.Kind).ToList(), Enum.GetValues<FloorKind>());
        CheckCoverage("walls", walls.Select(w => w.Kind).ToList(),
            Enum.GetValues<WallKind>().Where(k => k != WallKind.None).ToArray());
        if (floors.Select(f => f.EdgePriority).Distinct().Count() != floors.Count)
        {
            throw new InvalidDataException("content.json: terrain richiede una edgePriority diversa per ogni suolo.");
        }

        var water = floors.Where(f => f.Kind is FloorKind.ShallowWater or FloorKind.DeepWater).ToList();
        var land = floors.Except(water).ToList();
        if (water.Count > 0 && land.Count > 0 && water.Max(f => f.EdgePriority) >= land.Min(f => f.EdgePriority))
        {
            throw new InvalidDataException("content.json: terrain: l'acqua deve avere edgePriority più bassa di ogni altro suolo.");
        }

        var colours = floors.Select(f => f.Color).Concat(walls.Select(w => w.Color)).ToList();
        if (colours.Distinct(StringComparer.Ordinal).Count() != colours.Count)
        {
            throw new InvalidDataException("content.json: terrain richiede un colore diverso per ogni suolo e parete.");
        }

        return new TerrainTable(floors, walls);
    }

    // "deep-water" -> FloorKind.DeepWater: the kind comes from the kebab-case id.
    private static T ParseTerrainKind<T>(string section, string? id)
        where T : struct, Enum
    {
        var name = string.Concat((id ?? "").Split('-').Select(part =>
            part.Length == 0 ? "" : char.ToUpperInvariant(part[0]) + part[1..]));
        if (string.IsNullOrEmpty(id)
            || id != id.ToLowerInvariant()
            || !Enum.TryParse<T>(name, ignoreCase: false, out var kind)
            || !Enum.IsDefined(kind)
            || !string.Equals(kind.ToString(), name, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"content.json: terrain.{section} ha un id sconosciuto '{id}'.");
        }

        return kind;
    }

    private static void CheckTerrainRow(string section, string? id, string? biome, string? color)
    {
        if (biome is null || !TerrainTable.Biomes.Contains(biome, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"content.json: terrain.{section} '{id}': bioma '{biome}' sconosciuto ({string.Join(", ", TerrainTable.Biomes)}).");
        }

        if (color is not { Length: 7 } || color[0] != '#' || !color[1..].All(Uri.IsHexDigit))
        {
            throw new InvalidDataException(
                $"content.json: terrain.{section} '{id}': colore '{color}' non nel formato #RRGGBB.");
        }
    }

    private static void CheckCoverage<T>(string section, IReadOnlyList<T> kinds, IReadOnlyList<T> expected)
        where T : struct, Enum
    {
        foreach (var kind in expected)
        {
            var count = kinds.Count(k => k.Equals(kind));
            if (count != 1)
            {
                throw new InvalidDataException(
                    $"content.json: terrain.{section} deve avere una riga per '{kind}' (trovate {count}).");
            }
        }
    }

    private static UnitDefinition MapUnit(string unitId, UnitDto? dto)
    {
        if (dto is null)
        {
            throw new InvalidDataException($"content.json: manca units.{unitId}.");
        }

        if (!(dto.BuildSpeed > 0f) || !(dto.BuildRange > 1f) || !(dto.Speed > 0f))
        {
            throw new InvalidDataException(
                $"content.json: units.{unitId} richiede buildSpeed > 0, buildRange > 1 e speed > 0.");
        }

        return new UnitDefinition(dto.BuildSpeed, dto.BuildRange, dto.Speed);
    }

    private static float MapMultiplier(float? multiplier) =>
        multiplier is { } m && m > 0f && float.IsFinite(m) ? m : 1f;

    private static CoreUpgradeDefinition MapCoreUpgrade(CoreUpgradeDto? dto)
    {
        if (dto is null)
        {
            return new(150, [new ResourceAmount("iron-plate", 20)], 25);
        }

        var mats = MapAmounts(dto.BuildCost);
        if (mats.Count == 0)
        {
            mats = [new ResourceAmount("iron-plate", 20)];
        }

        return new(
            Math.Max(0, dto.MoneyCost),
            mats,
            Math.Max(0, dto.SaleBonusPercent <= 0 ? 25 : dto.SaleBonusPercent));
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
            ParseKind(c.Kind),
            MapMultiplier(c.BuildCostMultiplier));

    private static RecipeDefinition MapRecipe(RecipeDto r) =>
        new(r.Id, r.DurationSeconds, MapAmounts(r.Inputs), MapAmounts(r.Outputs));

    private static BuildingDefinition MapBuilding(BuildingDto b) =>
        new(
            b.Id,
            b.MoneyCost,
            MapAmounts(b.BuildCost),
            b.RefundPercent <= 0 ? 100 : b.RefundPercent,
            MapMultiplier(b.BuildCostMultiplier),
            Math.Max(0f, b.PowerUse));

    private static MarketItemDefinition MapMarket(MarketDto m) =>
        new(
            m.ItemId,
            string.IsNullOrWhiteSpace(m.DisplayName) ? m.ItemId : m.DisplayName,
            m.SellPrice,
            m.MinSellPrice,
            m.MaxSellPrice,
            m.SoftStock);

    private static StructureDefinition MapStructure(StructureDto s) =>
        new(
            s.Id,
            string.IsNullOrWhiteSpace(s.DisplayName) ? s.Id : s.DisplayName,
            string.IsNullOrWhiteSpace(s.Kind) ? "building" : s.Kind,
            s.UnlockedByDefault,
            MapUnlock(s.Unlock),
            s.IsStub,
            (s.Prerequisites ?? [])
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList());

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
        public EconomyDto? Economy { get; set; }
        public Dictionary<string, float>? BuildWeights { get; set; }
        public UnitsDto? Units { get; set; }
        public TerrainDto? Terrain { get; set; }
    }

    private sealed class TerrainDto
    {
        public List<FloorDto>? Floors { get; set; }
        public List<WallDto>? Walls { get; set; }
    }

    private sealed class FloorDto
    {
        public string? Id { get; set; }
        public string? Biome { get; set; }
        public string? Color { get; set; }
        public bool? Buildable { get; set; }

        public int? EdgePriority { get; set; }
    }

    private sealed class WallDto
    {
        public string? Id { get; set; }
        public string? Biome { get; set; }
        public string? Color { get; set; }
    }

    private sealed class UnitsDto
    {
        public UnitDto? Drone { get; set; }
    }

    private sealed class UnitDto
    {
        public float BuildSpeed { get; set; }
        public float BuildRange { get; set; }
        public float Speed { get; set; }
    }

    private sealed class EconomyDto
    {
        public CoreUpgradeDto? CoreUpgrade { get; set; }
    }

    private sealed class CoreUpgradeDto
    {
        public int MoneyCost { get; set; }
        public List<AmountDto>? BuildCost { get; set; }
        public int SaleBonusPercent { get; set; }
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
        public float? BuildCostMultiplier { get; set; }
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
        public float? BuildCostMultiplier { get; set; }
        /// <summary>Power drawn per second while working (lamp); 0 = none or set in code (craft machines).</summary>
        public float PowerUse { get; set; }
    }

    private sealed class MarketDto
    {
        public string ItemId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int SellPrice { get; set; }
        public int? MinSellPrice { get; set; }
        public int? MaxSellPrice { get; set; }
        public int? SoftStock { get; set; }
    }

    private sealed class StructureDto
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Kind { get; set; } = "";
        public bool UnlockedByDefault { get; set; }
        public UnlockDto? Unlock { get; set; }
        public bool IsStub { get; set; }
        public List<string>? Prerequisites { get; set; }
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
