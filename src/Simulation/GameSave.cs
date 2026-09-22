using System.Text.Json;
using System.Text.Json.Serialization;

namespace TIndustry.Logistics;

public sealed class GameSaveData
{
    public const int CurrentVersion = 8;

    public int Version { get; set; } = CurrentVersion;
    public int Seed { get; set; }
    public int MapWidth { get; set; }
    public int MapHeight { get; set; }
    public int Money { get; set; }
    public Dictionary<string, int> Materials { get; set; } = [];
    public int SoldItems { get; set; }
    public int SaleRevenue { get; set; }
    public long NextItemId { get; set; } = 1;
    public List<string> UnlockedStructures { get; set; } = [];
    public int CoreUpgradeLevel { get; set; }
    public int CoreSaleBonusPercent { get; set; }
    public float PowerBuffer { get; set; }
    public EconomySessionSaveData Session { get; set; } = new();
    public CameraSaveData Camera { get; set; } = new();
    public List<MinerSaveData> Miners { get; set; } = [];
    public List<SmelterSaveData> Smelters { get; set; } = [];
    public List<SmelterSaveData> Assemblers { get; set; } = [];
    public List<GeneratorSaveData> Generators { get; set; } = [];
    /// <summary>Deprecated v7 cable tiles — ignored on load (superseded by power nodes).</summary>
    public List<PowerCableSaveData> PowerCables { get; set; } = [];
    public List<PowerNodeSaveData> PowerNodes { get; set; } = [];
    public List<PowerLinkSaveData> PowerLinks { get; set; } = [];
    public List<ExtractorSaveData> Extractors { get; set; } = [];
    public List<ConveyorSaveData> Conveyors { get; set; } = [];
}

public sealed class EconomySessionSaveData
{
    public int StartingMoney { get; set; }
    public int BuildSpend { get; set; }
    public int UnlockSpend { get; set; }
    public int UpgradeSpend { get; set; }
    public int SaleIncome { get; set; }
    public int RefundIncome { get; set; }
    public Dictionary<string, int> SoldByItem { get; set; } = [];
}

public sealed class CameraSaveData
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Zoom { get; set; } = WorldCamera.DefaultZoom;
}

public sealed class MinerSaveData
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Direction { get; set; } = "East";
    public float Progress { get; set; }
    public string OutputItemId { get; set; } = "iron-ore";
    public string DefinitionId { get; set; } = "miner";
}

public sealed class SmelterSaveData
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Direction { get; set; } = "East";
    public string RecipeId { get; set; } = "smelt-iron";
    public float Progress { get; set; }
    public bool IsCrafting { get; set; }
    public Dictionary<string, int> InputBuffer { get; set; } = [];
    public List<string> OutputQueue { get; set; } = [];
    /// <summary>Forno coal fuel buffer (assemblers leave at 0).</summary>
    public int FuelBuffer { get; set; }
    public float BurnRemaining { get; set; }
}

public sealed class GeneratorSaveData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int FuelBuffer { get; set; }
    public float BurnRemaining { get; set; }
}

public sealed class ExtractorSaveData
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Direction { get; set; } = "East";
    public string FilterItemId { get; set; } = "iron-plate";
    public float Progress { get; set; }
}

public sealed class PowerCableSaveData
{
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class PowerNodeSaveData
{
    public int X { get; set; }
    public int Y { get; set; }
    public string DefinitionId { get; set; } = PowerNodeBuilding.Tier1Id;
}

public sealed class PowerLinkSaveData
{
    public string KindA { get; set; } = "Node";
    public int AX { get; set; }
    public int AY { get; set; }
    public string KindB { get; set; } = "Node";
    public int BX { get; set; }
    public int BY { get; set; }
}

public sealed class ConveyorSaveData
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Direction { get; set; } = "East";
    public string DefinitionId { get; set; } = "conveyor-basic";
    public string Kind { get; set; } = "belt";
    public int? BridgePartnerX { get; set; }
    public int? BridgePartnerY { get; set; }
    public int SplitterToggle { get; set; }
    public string? FilterItemId { get; set; }
    public List<ItemSaveData> Items { get; set; } = [];
}

public sealed class ItemSaveData
{
    public long Id { get; set; }
    public string ItemId { get; set; } = "iron-ore";
    public float Progress { get; set; }
    /// <summary>Junction travel axis; null on normal belts.</summary>
    public string? Travel { get; set; }
}

public sealed class SaveSlotInfo
{
    public required string Id { get; init; }
    public required string Path { get; init; }
    public required DateTime ModifiedUtc { get; init; }
    public int Seed { get; init; }
    public int Money { get; init; }
    public int MapWidth { get; init; }
    public int MapHeight { get; init; }
}

public static class GameSaveStore
{
    public const string ContinueSlotId = "continua";
    private const string SlotPrefix = "slot-";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string SavesDirectory
    {
        get
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "tIndustry",
                "saves");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string SlotPath(string slotId) =>
        Path.Combine(SavesDirectory, $"{slotId}.json");

    public static bool Exists(string slotId) => File.Exists(SlotPath(slotId));

    public static void Save(string slotId, GameSaveData data)
    {
        data.Version = GameSaveData.CurrentVersion;
        var path = SlotPath(slotId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static GameSaveData Load(string slotId)
    {
        var path = SlotPath(slotId);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Salvataggio non trovato: {slotId}", path);
        }

        var data = JsonSerializer.Deserialize<GameSaveData>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"Salvataggio non valido: {slotId}");
        if (data.Version is < 1 or > GameSaveData.CurrentVersion)
        {
            throw new InvalidDataException(
                $"Versione salvataggio non supportata: {data.Version} (attesa 1..{GameSaveData.CurrentVersion})");
        }

        return data;
    }

    public static bool TryLoad(string slotId, out GameSaveData data)
    {
        try
        {
            data = Load(slotId);
            return true;
        }
        catch
        {
            data = null!;
            return false;
        }
    }

    public static void Delete(string slotId)
    {
        var path = SlotPath(slotId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static string CreateSlotId() => $"{SlotPrefix}{DateTime.UtcNow:yyyyMMdd-HHmmss}";

    public static IReadOnlyList<SaveSlotInfo> ListSlots()
    {
        var results = new List<SaveSlotInfo>();
        foreach (var path in Directory.EnumerateFiles(SavesDirectory, "*.json"))
        {
            var id = Path.GetFileNameWithoutExtension(path);
            try
            {
                var data = JsonSerializer.Deserialize<GameSaveData>(File.ReadAllText(path), JsonOptions);
                if (data is null)
                {
                    continue;
                }

                results.Add(new SaveSlotInfo
                {
                    Id = id,
                    Path = path,
                    ModifiedUtc = File.GetLastWriteTimeUtc(path),
                    Seed = data.Seed,
                    Money = data.Money,
                    MapWidth = data.MapWidth,
                    MapHeight = data.MapHeight
                });
            }
            catch
            {
                // Skip corrupt slots in the manager UI.
            }
        }

        return results
            .OrderByDescending(slot => slot.Id == ContinueSlotId)
            .ThenByDescending(slot => slot.ModifiedUtc)
            .ToList();
    }

    public static GameSaveData Capture(
        FactoryWorld world,
        ConveyorGrid conveyors,
        EconomyWallet wallet,
        WorldCamera camera,
        ResearchState research,
        EconomySession session,
        long nextItemId)
    {
        return new GameSaveData
        {
            Version = GameSaveData.CurrentVersion,
            Seed = world.Seed,
            MapWidth = world.Terrain.Width,
            MapHeight = world.Terrain.Height,
            Money = wallet.Money,
            Materials = wallet.MaterialsSnapshot(),
            SoldItems = world.SoldItems,
            SaleRevenue = world.SaleRevenue,
            NextItemId = nextItemId,
            UnlockedStructures = research.UnlockedIds.OrderBy(id => id, StringComparer.Ordinal).ToList(),
            CoreUpgradeLevel = world.CoreUpgradeLevel,
            CoreSaleBonusPercent = world.CoreSaleBonusPercent,
            PowerBuffer = world.PowerBuffer,
            Session = new EconomySessionSaveData
            {
                StartingMoney = session.StartingMoney,
                BuildSpend = session.BuildSpend,
                UnlockSpend = session.UnlockSpend,
                UpgradeSpend = session.UpgradeSpend,
                SaleIncome = session.SaleIncome,
                RefundIncome = session.RefundIncome,
                SoldByItem = session.SoldByItem.ToDictionary(pair => pair.Key, pair => pair.Value)
            },
            Camera = new CameraSaveData
            {
                X = camera.X,
                Y = camera.Y,
                Zoom = camera.Zoom
            },
            Miners = world.Miners.Values
                .Select(miner => new MinerSaveData
                {
                    X = miner.Position.X,
                    Y = miner.Position.Y,
                    Direction = miner.Direction.ToString(),
                    Progress = miner.Progress,
                    OutputItemId = miner.OutputItemId,
                    DefinitionId = miner.DefinitionId
                })
                .ToList(),
            Smelters = world.Smelters.Values
                .Select(smelter => new SmelterSaveData
                {
                    X = smelter.Position.X,
                    Y = smelter.Position.Y,
                    Direction = smelter.Direction.ToString(),
                    RecipeId = smelter.Recipe.Id,
                    Progress = smelter.Progress,
                    IsCrafting = smelter.IsCrafting,
                    InputBuffer = smelter.InputBuffer.ToDictionary(pair => pair.Key, pair => pair.Value),
                    OutputQueue = smelter.OutputQueue.ToList(),
                    FuelBuffer = smelter.FuelBuffer,
                    BurnRemaining = smelter.BurnRemaining
                })
                .ToList(),
            Assemblers = world.Assemblers.Values
                .Select(assembler => new SmelterSaveData
                {
                    X = assembler.Position.X,
                    Y = assembler.Position.Y,
                    Direction = assembler.Direction.ToString(),
                    RecipeId = assembler.Recipe.Id,
                    Progress = assembler.Progress,
                    IsCrafting = assembler.IsCrafting,
                    InputBuffer = assembler.InputBuffer.ToDictionary(pair => pair.Key, pair => pair.Value),
                    OutputQueue = assembler.OutputQueue.ToList()
                })
                .ToList(),
            Generators = world.Generators.Values
                .Select(generator => new GeneratorSaveData
                {
                    X = generator.Position.X,
                    Y = generator.Position.Y,
                    FuelBuffer = generator.FuelBuffer,
                    BurnRemaining = generator.BurnRemaining
                })
                .ToList(),
            PowerCables = [],
            PowerNodes = world.PowerNodes.Values
                .Select(node => new PowerNodeSaveData
                {
                    X = node.Position.X,
                    Y = node.Position.Y,
                    DefinitionId = node.DefinitionId
                })
                .OrderBy(n => n.Y)
                .ThenBy(n => n.X)
                .ToList(),
            PowerLinks = world.PowerLinks
                .Select(link => new PowerLinkSaveData
                {
                    KindA = link.A.Kind.ToString(),
                    AX = link.A.Origin.X,
                    AY = link.A.Origin.Y,
                    KindB = link.B.Kind.ToString(),
                    BX = link.B.Origin.X,
                    BY = link.B.Origin.Y
                })
                .ToList(),
            Extractors = world.Extractors.Values
                .Select(extractor => new ExtractorSaveData
                {
                    X = extractor.Position.X,
                    Y = extractor.Position.Y,
                    Direction = extractor.Direction.ToString(),
                    FilterItemId = extractor.FilterItemId,
                    Progress = extractor.Progress
                })
                .ToList(),
            Conveyors = conveyors.Cells.Values
                .Select(cell => new ConveyorSaveData
                {
                    X = cell.Position.X,
                    Y = cell.Position.Y,
                    Direction = cell.Direction.ToString(),
                    DefinitionId = cell.Definition.Id,
                    Kind = cell.Kind.ToString().ToLowerInvariant(),
                    BridgePartnerX = cell.BridgePartner?.X,
                    BridgePartnerY = cell.BridgePartner?.Y,
                    SplitterToggle = cell.SplitterToggle,
                    FilterItemId = cell.Kind == LogisticsKind.Sorter ? cell.FilterItemId : null,
                    Items = cell.Items
                        .Select(item => new ItemSaveData
                        {
                            Id = item.Id,
                            ItemId = item.ItemId,
                            Progress = item.Progress,
                            Travel = item.Travel?.ToString()
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    public static (FactoryWorld World, ConveyorGrid Conveyors, EconomyWallet Wallet, WorldCamera Camera, ResearchState Research, EconomySession Session, long NextItemId)
        Restore(GameSaveData data, GameContent content)
    {
        var world = new FactoryWorld(data.MapWidth, data.MapHeight, data.Seed);
        world.SetSoldItems(data.SoldItems, data.SaleRevenue);
        world.SetCoreUpgrade(data.CoreUpgradeLevel, data.CoreSaleBonusPercent);
        if (data.Version >= 6)
        {
            world.SetPowerBuffer(data.PowerBuffer);
        }

        var wallet = new EconomyWallet(data.Money, data.Materials);
        var conveyors = new ConveyorGrid();
        var definitions = content.Conveyors.ToDictionary(definition => definition.Id, StringComparer.Ordinal);
        var recipes = content.Recipes.ToDictionary(recipe => recipe.Id, StringComparer.Ordinal);
        var smelterRecipes = content.Recipes
            .Where(r => r.Id.StartsWith("smelt-", StringComparison.Ordinal))
            .ToList();
        var assemblerRecipes = content.Recipes
            .Where(r => r.Id.StartsWith("craft-", StringComparison.Ordinal))
            .ToList();
        var research = RestoreResearch(data, content);
        var session = new EconomySession(data.Session.StartingMoney > 0 ? data.Session.StartingMoney : data.Money);
        session.Restore(
            data.Session.StartingMoney > 0 ? data.Session.StartingMoney : data.Money,
            data.Session.BuildSpend,
            data.Session.UnlockSpend,
            data.Session.UpgradeSpend,
            data.Session.SaleIncome > 0 ? data.Session.SaleIncome : data.SaleRevenue,
            data.Session.RefundIncome,
            data.Session.SoldByItem);

        foreach (var minerData in data.Miners)
        {
            var position = new GridPosition(minerData.X, minerData.Y);
            if (!Enum.TryParse<Direction>(minerData.Direction, ignoreCase: true, out var direction))
            {
                direction = Direction.East;
            }

            if (!world.TryRestoreMiner(
                    position,
                    direction,
                    minerData.Progress,
                    minerData.OutputItemId,
                    string.IsNullOrWhiteSpace(minerData.DefinitionId) ? MinerBuilding.BasicId : minerData.DefinitionId))
            {
                throw new InvalidDataException($"Impossibile ripristinare il minatore a {position}.");
            }
        }

        foreach (var smelterData in data.Smelters)
        {
            if (!recipes.TryGetValue(smelterData.RecipeId, out var recipe))
            {
                throw new InvalidDataException($"Ricetta sconosciuta: {smelterData.RecipeId}");
            }

            if (!Enum.TryParse<Direction>(smelterData.Direction, ignoreCase: true, out var direction))
            {
                direction = Direction.East;
            }

            var position = new GridPosition(smelterData.X, smelterData.Y);
            if (!world.TryRestoreSmelter(
                    position,
                    direction,
                    recipe,
                    smelterData.Progress,
                    smelterData.IsCrafting,
                    smelterData.InputBuffer,
                    smelterData.OutputQueue,
                    smelterData.FuelBuffer,
                    smelterData.BurnRemaining,
                    smelterRecipes))
            {
                throw new InvalidDataException($"Impossibile ripristinare il forno a {position}.");
            }
        }

        foreach (var assemblerData in data.Assemblers)
        {
            if (!recipes.TryGetValue(assemblerData.RecipeId, out var recipe))
            {
                throw new InvalidDataException($"Ricetta assemblatore sconosciuta: {assemblerData.RecipeId}");
            }

            if (!Enum.TryParse<Direction>(assemblerData.Direction, ignoreCase: true, out var direction))
            {
                direction = Direction.East;
            }

            var position = new GridPosition(assemblerData.X, assemblerData.Y);
            if (!world.TryRestoreAssembler(
                    position,
                    direction,
                    recipe,
                    assemblerData.Progress,
                    assemblerData.IsCrafting,
                    assemblerData.InputBuffer,
                    assemblerData.OutputQueue,
                    assemblerRecipes))
            {
                throw new InvalidDataException($"Impossibile ripristinare l'assemblatore a {position}.");
            }
        }

        foreach (var generatorData in data.Generators)
        {
            var position = new GridPosition(generatorData.X, generatorData.Y);
            if (!world.TryRestoreGenerator(position, generatorData.FuelBuffer, generatorData.BurnRemaining))
            {
                throw new InvalidDataException($"Impossibile ripristinare il generatore a {position}.");
            }
        }

        // v7 cables are deprecated — drop on load. v8+ restores power nodes + geometric links.
        if (data.Version >= 8)
        {
            foreach (var nodeData in data.PowerNodes)
            {
                var position = new GridPosition(nodeData.X, nodeData.Y);
                var definitionId = string.IsNullOrWhiteSpace(nodeData.DefinitionId)
                    ? PowerNodeBuilding.Tier1Id
                    : nodeData.DefinitionId;
                if (!world.TryRestorePowerNode(position, definitionId))
                {
                    throw new InvalidDataException($"Impossibile ripristinare il nodo a {position}.");
                }
            }

            foreach (var linkData in data.PowerLinks)
            {
                if (!Enum.TryParse<PowerEndpointKind>(linkData.KindA, ignoreCase: true, out var kindA)
                    || !Enum.TryParse<PowerEndpointKind>(linkData.KindB, ignoreCase: true, out var kindB))
                {
                    continue;
                }

                // CORE is not on the power graph — drop legacy core links on load.
                if (kindA == PowerEndpointKind.Core || kindB == PowerEndpointKind.Core)
                {
                    continue;
                }

                world.TryRestorePowerLink(
                    new PowerEndpointId(kindA, new GridPosition(linkData.AX, linkData.AY)),
                    new PowerEndpointId(kindB, new GridPosition(linkData.BX, linkData.BY)));
            }

            world.RefreshPowerNetworks();
        }

        foreach (var extractorData in data.Extractors)
        {
            if (!Enum.TryParse<Direction>(extractorData.Direction, ignoreCase: true, out var direction))
            {
                throw new InvalidDataException($"Direzione estrattore non valida: {extractorData.Direction}");
            }

            var position = new GridPosition(extractorData.X, extractorData.Y);
            if (!world.TryRestoreExtractor(
                    position,
                    direction,
                    extractorData.FilterItemId,
                    extractorData.Progress))
            {
                throw new InvalidDataException($"Impossibile ripristinare l'estrattore a {position}.");
            }
        }

        foreach (var conveyorData in data.Conveyors)
        {
            if (!definitions.TryGetValue(conveyorData.DefinitionId, out var definition))
            {
                throw new InvalidDataException($"Definizione nastro sconosciuta: {conveyorData.DefinitionId}");
            }

            if (!Enum.TryParse<Direction>(conveyorData.Direction, ignoreCase: true, out var direction))
            {
                throw new InvalidDataException($"Direzione nastro non valida: {conveyorData.Direction}");
            }

            var position = new GridPosition(conveyorData.X, conveyorData.Y);
            var items = conveyorData.Items
                .Select(item =>
                {
                    Direction? travel = null;
                    if (!string.IsNullOrEmpty(item.Travel)
                        && Enum.TryParse<Direction>(item.Travel, ignoreCase: true, out var parsed))
                    {
                        travel = parsed;
                    }

                    return new TransportedItem(item.Id, item.ItemId, item.Progress, travel);
                })
                .ToList();
            GridPosition? bridgePartner = null;
            if (conveyorData.BridgePartnerX is { } bx && conveyorData.BridgePartnerY is { } by)
            {
                bridgePartner = new GridPosition(bx, by);
            }

            if (!conveyors.TryRestore(
                    position,
                    direction,
                    definition,
                    items,
                    bridgePartner,
                    conveyorData.SplitterToggle,
                    conveyorData.FilterItemId))
            {
                throw new InvalidDataException($"Impossibile ripristinare il nastro a {position}.");
            }
        }

        var camera = new WorldCamera(data.Camera.X, data.Camera.Y, data.Camera.Zoom);
        return (world, conveyors, wallet, camera, research, session, data.NextItemId);
    }

    private static ResearchState RestoreResearch(GameSaveData data, GameContent content)
    {
        if (data.Version >= 3 && data.UnlockedStructures.Count > 0)
        {
            // Migrate deprecated Cavo T1 unlock → Nodo T1.
            var unlocked = data.UnlockedStructures.ToList();
            if (unlocked.Contains("power-cable") && !unlocked.Contains(PowerNodeBuilding.Tier1Id))
            {
                unlocked.Add(PowerNodeBuilding.Tier1Id);
            }

            return ResearchState.FromSaved(unlocked, content);
        }

        // Migrate Phase 1–2 saves: keep defaults, unlock tech implied by placed buildings.
        var research = ResearchState.CreateNew(content);
        if (data.Smelters.Count > 0)
        {
            research.ForceUnlock("smelter");
        }

        if (data.Assemblers.Count > 0)
        {
            research.ForceUnlock("assembler");
        }

        if (data.Extractors.Count > 0)
        {
            research.ForceUnlock("extractor");
        }

        if (data.Generators.Count > 0)
        {
            research.ForceUnlock("generator");
        }

        if (data.Conveyors.Any(cell => cell.DefinitionId == "conveyor-fast"))
        {
            research.ForceUnlock("conveyor-fast");
        }

        if (data.Conveyors.Any(cell => cell.DefinitionId == "conveyor-express"))
        {
            research.ForceUnlock("conveyor-express");
        }

        if (data.Miners.Any(miner => miner.DefinitionId == MinerBuilding.AdvancedId))
        {
            research.ForceUnlock(MinerBuilding.AdvancedId);
        }

        foreach (var id in new[] { "junction", "splitter", "conveyor-bridge", "sorter" })
        {
            if (data.Conveyors.Any(cell => cell.DefinitionId == id))
            {
                research.ForceUnlock(id);
            }
        }

        return research;
    }
}
