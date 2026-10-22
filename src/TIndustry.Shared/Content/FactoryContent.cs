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
    public MarketRules MarketRules { get; init; } = null!;
    public IReadOnlyList<StructureDefinition> Structures { get; init; } = [];
    public CoreUpgradeDefinition CoreUpgrade { get; init; } =
        new(150, [new ResourceAmount("iron-plate", 20)]);

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

    /// <summary>
    /// True when the miner <paramref name="minerId"/> digs <paramref name="deposit"/>: its tier is at least the
    /// ore's hardness. Content built in code without tiers or hardness puts no limit.
    /// </summary>
    public bool MinerCanDig(string minerId, DepositKind deposit)
    {
        if (deposit == DepositKind.None)
        {
            return false;
        }

        var tier = FindBuilding(minerId)?.Tier ?? 0;
        return tier <= 0 || tier >= Terrain.Hardness(deposit);
    }

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
        CheckMinerTiers(root.Buildings);
        var market = root.Market.Select(MapMarket).ToList();
        var marketRules = MapMarketRules(root.MarketRules);
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
            MarketRules = marketRules,
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

        var deposits = (dto.Deposits ?? []).Select(d =>
        {
            var kind = ParseTerrainKind<DepositKind>("deposits", d.Id);
            if (kind == DepositKind.None)
            {
                throw new InvalidDataException("content.json: terrain.deposits non deve contenere 'none'.");
            }

            if (d.Hardness is not { } hardness)
            {
                throw new InvalidDataException($"content.json: terrain.deposits '{d.Id}': manca hardness.");
            }

            if (hardness < 1)
            {
                throw new InvalidDataException($"content.json: terrain.deposits '{d.Id}': hardness deve essere >= 1.");
            }

            return new DepositDef(d.Id!, kind, hardness);
        }).ToList();
        CheckCoverage("deposits", deposits.Select(d => d.Kind).ToList(),
            Enum.GetValues<DepositKind>().Where(k => k != DepositKind.None).ToArray());

        return new TerrainTable(floors, walls) { Deposits = deposits };
    }

    // Every miner says which ores it digs: tier >= the ore's hardness (terrain.deposits).
    private static void CheckMinerTiers(IEnumerable<BuildingDto> buildings)
    {
        foreach (var id in new[] { Miner.BasicId, Miner.AdvancedId })
        {
            var miner = buildings.FirstOrDefault(b => b.Id == id);
            if (miner is null)
            {
                continue;
            }

            if (miner.Tier is not { } tier)
            {
                throw new InvalidDataException($"content.json: buildings '{id}': manca tier (1 = Minatore T1, 2 = T2).");
            }

            if (tier < 1)
            {
                throw new InvalidDataException($"content.json: buildings '{id}': tier deve essere >= 1.");
            }
        }
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
            return new(150, [new ResourceAmount("iron-plate", 20)]);
        }

        var mats = MapAmounts(dto.BuildCost);
        if (mats.Count == 0)
        {
            mats = [new ResourceAmount("iron-plate", 20)];
        }

        return new(Math.Max(0, dto.MoneyCost), mats);
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
            MapMultiplier(c.BuildCostMultiplier),
            c.Armored);

    private static RecipeDefinition MapRecipe(RecipeDto r) =>
        new(r.Id, r.DurationSeconds, MapAmounts(r.Inputs), MapAmounts(r.Outputs));

    private static BuildingDefinition MapBuilding(BuildingDto b) =>
        new(
            b.Id,
            b.MoneyCost,
            MapAmounts(b.BuildCost),
            b.RefundPercent <= 0 ? 100 : b.RefundPercent,
            MapMultiplier(b.BuildCostMultiplier),
            Math.Max(0f, b.PowerUse),
            b.Tier ?? 0);

    private static MarketItemDefinition MapMarket(MarketDto m) =>
        new(
            m.ItemId,
            string.IsNullOrWhiteSpace(m.DisplayName) ? m.ItemId : m.DisplayName,
            m.BaseValue is > 0
                ? m.BaseValue.Value
                : throw new InvalidDataException(
                    $"content.json: il mercato di '{m.ItemId}' deve avere un valore base (baseValue) maggiore di zero."));

    private static MarketRules MapMarketRules(MarketRulesDto? r)
    {
        if (r is null)
        {
            throw new InvalidDataException("content.json: manca il blocco delle regole del mercato (marketRules).");
        }

        // Every field is required; scale converts the JSON unit (fraction, multiplier) to the integer unit.
        static int F(double? value, string name, int scale = 1)
        {
            if (value is not { } v || v < 0)
            {
                throw new InvalidDataException(
                    $"content.json: marketRules.{name} manca o è negativo (deve essere un numero maggiore o uguale a zero).");
            }

            return (int)Math.Round(v * scale, MidpointRounding.AwayFromZero);
        }

        static int Positive(int value, string name) =>
            value > 0
                ? value
                : throw new InvalidDataException($"content.json: marketRules.{name} deve essere maggiore di zero.");

        static void Order(int low, int high, string lowName, string highName)
        {
            if (low > high)
            {
                throw new InvalidDataException(
                    $"content.json: marketRules.{lowName} non può superare marketRules.{highName}.");
            }
        }

        var rules = new MarketRules(
            F(r.NormalMin, "normalMin", 1000), F(r.NormalMax, "normalMax", 1000),
            F(r.NormalAverage, "normalAverage", 1000), Positive(F(r.NormalStepSeconds, "normalStepSeconds"), "normalStepSeconds"),
            F(r.NormalPull, "normalPull", 1000), F(r.NormalNoise, "normalNoise", 1000),
            F(r.SpikeThreshold, "spikeThreshold", 1000), F(r.SpikeMax, "spikeMax", 1000),
            F(r.SpikeRiseSeconds, "spikeRiseSeconds"), F(r.SpikeHoldSeconds, "spikeHoldSeconds"),
            F(r.SpikeFallSeconds, "spikeFallSeconds"),
            F(r.SpikeIntervalMinMinutes, "spikeIntervalMinMinutes"), F(r.SpikeIntervalMaxMinutes, "spikeIntervalMaxMinutes"),
            F(r.SaleImpact, "saleImpact", 1_000_000), Positive(F(r.SaleRecoverySeconds, "saleRecoverySeconds"), "saleRecoverySeconds"),
            F(r.CrashPieces, "crashPieces"), F(r.CrashWindowSeconds, "crashWindowSeconds"),
            F(r.CrashMinutes, "crashMinutes"), F(r.CrashDipMinutes, "crashDipMinutes"),
            F(r.CrashDipMin, "crashDipMin", 1000), F(r.CrashDipMax, "crashDipMax", 1000),
            Positive(F(r.CrashDipEverySeconds, "crashDipEverySeconds"), "crashDipEverySeconds"),
            F(r.CrashDipChance, "crashDipChance", 10_000),
            F(r.JackpotMinPieces, "jackpotMinPieces"), F(r.JackpotBase, "jackpotBase", 10_000),
            Positive(F(r.JackpotStepPieces, "jackpotStepPieces"), "jackpotStepPieces"),
            F(r.JackpotStep, "jackpotStep", 10_000), F(r.JackpotCap, "jackpotCap", 10_000),
            F(r.JackpotBonus, "jackpotBonus", 1000),
            F(r.PityPieces, "pityPieces"), F(r.PityStep, "pityStep", 10_000),
            F(r.PityGuaranteeMisses, "pityGuaranteeMisses"),
            F(r.MissCooldownSeconds, "missCooldownSeconds"), F(r.MissSpikeDelaySeconds, "missSpikeDelaySeconds"),
            F(r.LostBoost, "lostBoost", 10_000), F(r.LostBoostMinutes, "lostBoostMinutes"),
            F(r.LostPieces, "lostPieces"), Positive(F(r.HistorySeconds, "historySeconds"), "historySeconds"));

        Order(rules.NormalMinMilli, rules.NormalMaxMilli, "normalMin", "normalMax");
        Order(rules.NormalMaxMilli, rules.SpikeThresholdMilli, "normalMax", "spikeThreshold");
        Order(rules.SpikeThresholdMilli, rules.SpikeMaxMilli, "spikeThreshold", "spikeMax");
        Order(rules.SpikeIntervalMinMinutes, rules.SpikeIntervalMaxMinutes, "spikeIntervalMinMinutes", "spikeIntervalMaxMinutes");
        Order(rules.CrashDipMinMilli, rules.CrashDipMaxMilli, "crashDipMin", "crashDipMax");
        Order(rules.JackpotCapBp, 10_000, "jackpotCap", "1 (100%)");
        Order(rules.CrashDipChanceBp, 10_000, "crashDipChance", "1 (100%)");
        return rules;
    }

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
        public MarketRulesDto? MarketRules { get; set; }
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
        public List<DepositDto>? Deposits { get; set; }
    }

    private sealed class DepositDto
    {
        public string? Id { get; set; }
        public int? Hardness { get; set; }
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
        public bool Armored { get; set; }
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
        /// <summary>Miners only: digs the ores whose hardness is at most this (terrain.deposits).</summary>
        public int? Tier { get; set; }
    }

    private sealed class MarketDto
    {
        public string ItemId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int? BaseValue { get; set; }
    }

    private sealed class MarketRulesDto
    {
        public double? NormalMin { get; set; }
        public double? NormalMax { get; set; }
        public double? NormalAverage { get; set; }
        public double? NormalStepSeconds { get; set; }
        public double? NormalPull { get; set; }
        public double? NormalNoise { get; set; }
        public double? SpikeThreshold { get; set; }
        public double? SpikeMax { get; set; }
        public double? SpikeRiseSeconds { get; set; }
        public double? SpikeHoldSeconds { get; set; }
        public double? SpikeFallSeconds { get; set; }
        public double? SpikeIntervalMinMinutes { get; set; }
        public double? SpikeIntervalMaxMinutes { get; set; }
        public double? SaleImpact { get; set; }
        public double? SaleRecoverySeconds { get; set; }
        public double? CrashPieces { get; set; }
        public double? CrashWindowSeconds { get; set; }
        public double? CrashMinutes { get; set; }
        public double? CrashDipMinutes { get; set; }
        public double? CrashDipMin { get; set; }
        public double? CrashDipMax { get; set; }
        public double? CrashDipEverySeconds { get; set; }
        public double? CrashDipChance { get; set; }
        public double? JackpotMinPieces { get; set; }
        public double? JackpotBase { get; set; }
        public double? JackpotStepPieces { get; set; }
        public double? JackpotStep { get; set; }
        public double? JackpotCap { get; set; }
        public double? JackpotBonus { get; set; }
        public double? PityPieces { get; set; }
        public double? PityStep { get; set; }
        public double? PityGuaranteeMisses { get; set; }
        public double? MissCooldownSeconds { get; set; }
        public double? MissSpikeDelaySeconds { get; set; }
        public double? LostBoost { get; set; }
        public double? LostBoostMinutes { get; set; }
        public double? LostPieces { get; set; }
        public double? HistorySeconds { get; set; }
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
