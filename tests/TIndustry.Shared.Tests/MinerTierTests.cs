using System.Text.Json;
using System.Text.Json.Nodes;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Ore hardness vs miner tier (Mindustry's drill tier): the T1 miner does not dig titanium, the T2 digs everything.
/// </summary>
public sealed class MinerTierTests
{
    private const int Size = 40;

    private static readonly string ContentPath = Path.Combine(AppContext.BaseDirectory, "data", "content.json");

    private static readonly FactoryContent Content = FactoryContent.Load(ContentPath);

    private static TerrainTile Ore(DepositKind deposit) => new(FloorKind.Stone, WallKind.None, deposit);

    /// <summary>40×40 stone with the Core at (30,30), everything unlocked and the given deposits.</summary>
    private static Factory Sandbox(bool instant, params (int X, int Y, DepositKind Deposit)[] cells)
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(30, 30), mapWidth: Size, mapHeight: Size,
            rules: instant ? new GameRules(InstantBuild: true) : null);
        factory.UnlockAllForDemo();
        var tiles = new TerrainTile[Size, Size];
        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                tiles[x, y] = Ore(DepositKind.None);
            }
        }

        foreach (var (x, y, deposit) in cells)
        {
            tiles[x, y] = Ore(deposit);
        }

        factory.Terrain = TerrainMap.FromTiles(tiles);
        return factory;
    }

    private static (int, int, DepositKind)[] Patch(int x, int y, DepositKind deposit) =>
        [(x, y, deposit), (x + 1, y, deposit), (x, y + 1, deposit), (x + 1, y + 1, deposit)];

    private static RefusalCode? Submit(Factory factory, SimCommand command)
    {
        var host = new SimHost(factory);
        RefusalCode? reason = null;
        host.CommandRejected += (_, r) => reason = r.Code;
        host.Submit(command);
        host.AdvanceTicks(1);
        return reason;
    }

    private static BuildSpec MinerSpec(string id, int x, int y) =>
        new(id, id == Miner.AdvancedId ? BuildingKind.MinerAdvanced : BuildingKind.Miner, new GridPosition(x, y), Direction.East);

    [Fact]
    public void ContentGivesTiersAndHardness()
    {
        Assert.Equal(1, Content.FindBuilding(Miner.BasicId)!.Tier);
        Assert.Equal(2, Content.FindBuilding(Miner.AdvancedId)!.Tier);
        Assert.Equal(2, Content.Terrain.Hardness(DepositKind.Titanium));
        foreach (var ore in new[] { DepositKind.Iron, DepositKind.Copper, DepositKind.Coal, DepositKind.Lead })
        {
            Assert.Equal(1, Content.Terrain.Hardness(ore));
        }
    }

    [Fact]
    public void BasicMinerOnTitaniumOnlyIsRefused()
    {
        var factory = Sandbox(instant: true, Patch(10, 10, DepositKind.Titanium));

        Assert.False(factory.CanPlace(MinerSpec(Miner.BasicId, 10, 10), out var reason));
        Assert.Equal(RefusalCode.NeedsAdvancedMiner, reason);
        Assert.False(factory.TryPlaceMiner(new GridPosition(10, 10), Direction.East));

        // Instant build: the command is refused with the same text.
        Assert.Equal(RefusalCode.NeedsAdvancedMiner,
            Submit(factory, new PlaceBuilding(0, BuildingKind.Miner, new GridPosition(10, 10), Direction.East)));
        Assert.Empty(factory.Miners);

        // One titanium cell and nothing else under the footprint: still refused.
        var single = Sandbox(instant: true, (11, 11, DepositKind.Titanium));
        Assert.False(single.CanPlace(MinerSpec(Miner.BasicId, 10, 10), out reason));
        Assert.Equal(RefusalCode.NeedsAdvancedMiner, reason);
    }

    [Fact]
    public void BasicMinerPlanOnTitaniumIsRefusedAndDropped()
    {
        // Drone plans: the command is refused up front.
        var planned = Sandbox(instant: false, Patch(10, 10, DepositKind.Titanium));
        Assert.Equal(RefusalCode.NeedsAdvancedMiner,
            Submit(planned, new PlaceBuilding(0, BuildingKind.Miner, new GridPosition(10, 10), Direction.East)));
        Assert.Empty(planned.BuilderOf(0)!.Plans);

        // A plan queued before the rule (old save) is dropped with the reason when the drone gets to it.
        var host = new SimHost(planned);
        var dropped = new List<RefusalCode>();
        host.EventRaised += e =>
        {
            if (e is PlanDropped d)
            {
                dropped.Add(d.Reason);
            }
        };
        planned.BuilderOf(0)!.PlanQueue.Add(new BuildPlan(MinerSpec(Miner.BasicId, 10, 10), Breaking: false));
        for (var i = 0; i < 60 * 60 && dropped.Count == 0; i++)
        {
            host.AdvanceTicks(1);
        }

        Assert.Equal([RefusalCode.NeedsAdvancedMiner], dropped);
        Assert.Empty(planned.Miners);
    }

    [Fact]
    public void BasicMinerOnIronAndTitaniumDigsIronWithTheIronCellsOnly()
    {
        // Three titanium cells and one iron cell: the T1 ignores the titanium.
        var factory = Sandbox(instant: true,
            (10, 10, DepositKind.Titanium), (11, 10, DepositKind.Titanium),
            (10, 11, DepositKind.Titanium), (11, 11, DepositKind.Iron));

        Assert.True(factory.CanPlace(MinerSpec(Miner.BasicId, 10, 10), out _));
        Assert.True(factory.TryPlaceMiner(new GridPosition(10, 10), Direction.East));
        var miner = Assert.Single(factory.Miners);
        Assert.Equal("iron-ore", miner.OutputItemId);
        Assert.Equal(1, miner.CoveredDepositTiles);
    }

    [Fact]
    public void AdvancedMinerDigsTitanium()
    {
        var factory = Sandbox(instant: true,
            (10, 10, DepositKind.Titanium), (11, 10, DepositKind.Titanium),
            (10, 11, DepositKind.Titanium), (11, 11, DepositKind.Iron));

        Assert.True(factory.CanPlace(MinerSpec(Miner.AdvancedId, 10, 10), out _));
        Assert.Null(Submit(factory,
            new PlaceBuilding(0, BuildingKind.MinerAdvanced, new GridPosition(10, 10), Direction.East)));
        var miner = Assert.Single(factory.Miners);
        Assert.Equal(Miner.AdvancedId, miner.DefinitionId);
        Assert.Equal("titanium-ore", miner.OutputItemId);
        Assert.Equal(3, miner.CoveredDepositTiles);
    }

    [Fact]
    public void BasicMinerWithoutOreIsStillAllowed()
    {
        var factory = Sandbox(instant: true);

        Assert.True(factory.CanPlace(MinerSpec(Miner.BasicId, 10, 10), out _));
        Assert.Null(Submit(factory, new PlaceBuilding(0, BuildingKind.Miner, new GridPosition(10, 10), Direction.East)));
        var miner = Assert.Single(factory.Miners);
        Assert.Equal(0, miner.CoveredDepositTiles);
        Assert.False(miner.IsWorking);
    }

    [Theory]
    [InlineData("miner", "tier")]
    [InlineData("miner-advanced", "tier")]
    public void MinerWithoutTierIsAnItalianError(string minerId, string field)
    {
        var error = LoadMutated(root =>
        {
            var building = root["buildings"]!.AsArray().Single(b => (string?)b!["id"] == minerId)!.AsObject();
            building.Remove(field);
        });
        Assert.Contains($"buildings '{minerId}'", error);
        Assert.Contains("manca tier", error);

        var zero = LoadMutated(root =>
            root["buildings"]!.AsArray().Single(b => (string?)b!["id"] == minerId)!["tier"] = 0);
        Assert.Contains("tier deve essere >= 1", zero);
    }

    [Fact]
    public void DepositWithoutHardnessIsAnItalianError()
    {
        var missing = LoadMutated(root =>
            root["terrain"]!["deposits"]!.AsArray().Single(d => (string?)d!["id"] == "titanium")!.AsObject().Remove("hardness"));
        Assert.Contains("terrain.deposits 'titanium': manca hardness", missing);

        var zero = LoadMutated(root =>
            root["terrain"]!["deposits"]!.AsArray().Single(d => (string?)d!["id"] == "lead")!["hardness"] = 0);
        Assert.Contains("terrain.deposits 'lead': hardness deve essere >= 1", zero);

        var gone = LoadMutated(root =>
        {
            var deposits = root["terrain"]!["deposits"]!.AsArray();
            deposits.Remove(deposits.Single(d => (string?)d!["id"] == "coal"));
        });
        Assert.Contains("terrain.deposits deve avere una riga per 'Coal'", gone);

        var noSection = LoadMutated(root => root["terrain"]!.AsObject().Remove("deposits"));
        Assert.Contains("terrain.deposits deve avere una riga per 'Iron'", noSection);
    }

    [Fact]
    public void SavedBasicMinerOnTitaniumKeepsDiggingTitanium()
    {
        // A game saved by hand (or by older content): a T1 standing on titanium. The classic 1000×1000 map has titanium.
        var (factory, origin) = ClassicWithTitaniumSpot();
        Assert.False(factory.CanPlace(MinerSpec(Miner.BasicId, origin.X, origin.Y), out _));

        var data = factory.Capture();
        data.Miners.Add(new MinerSaveDto
        {
            Id = data.NextEntityId + 100,
            X = origin.X,
            Y = origin.Y,
            Direction = "East",
            OutputItemId = "titanium-ore",
            DefinitionId = Miner.BasicId
        });
        var json = JsonSerializer.Serialize(data, FactorySaveStore.JsonOptions);
        var reloaded = Factory.Restore(Content, JsonSerializer.Deserialize<FactorySaveData>(json, FactorySaveStore.JsonOptions)!);

        var miner = Assert.Single(reloaded.Miners);
        Assert.Equal("titanium-ore", miner.OutputItemId);
        Assert.Equal(Miner.BasicId, miner.DefinitionId);
        Assert.True(miner.IsWorking);

        // Capturing again writes the same miner back: the save bytes of the miner do not change.
        var again = reloaded.Capture();
        Assert.Equal(
            JsonSerializer.Serialize(data.Miners, FactorySaveStore.JsonOptions),
            JsonSerializer.Serialize(again.Miners, FactorySaveStore.JsonOptions));

        Assert.True(reloaded.TryPlaceBelt(new GridPosition(origin.X + 2, origin.Y), Direction.East));
        reloaded.RunSeconds(Miner.MiningDurationSeconds + 0.5f);
        Assert.True(miner.ItemsProduced >= 1);
        Assert.True(reloaded.Belts.TryGet(new GridPosition(origin.X + 2, origin.Y), out var cell));
        Assert.Contains(cell.Items, i => i.ItemId == "titanium-ore");
    }

    [Fact]
    public void TitaniumLevelStartPaysTheAdvancedMiner()
    {
        // Level 09 asks for titanium: what it starts with covers at least the T2 unlock and one T2 (the rest of
        // the level, smelter and assembler, still comes from the market, as before the rule).
        var catalog = CampaignCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "campaign.json"));
        var level = catalog.Find("level-09-titanio")!;
        var wallet = catalog.CreateWallet(level);
        var unlock = Content.RequireStructure(Miner.AdvancedId).Unlock!;
        var build = Content.FindBuilding(Miner.AdvancedId)!.BuildCost;

        Assert.True(level.StartingMoney >= unlock.Money, $"soldi {level.StartingMoney} < {unlock.Money}");
        foreach (var itemId in unlock.Materials.Select(m => m.ItemId).Concat(build.Select(b => b.ItemId)).Distinct())
        {
            var need = unlock.Materials.Where(m => m.ItemId == itemId).Sum(m => m.Amount)
                + build.Where(b => b.ItemId == itemId).Sum(b => b.Amount);
            Assert.True(wallet.MaterialCount(itemId) >= need, $"{itemId}: {wallet.MaterialCount(itemId)} < {need}");
        }

        Assert.Contains("Minatore T2", level.Description);
    }

    [Fact]
    public void SavedBasicMinerSiteOnTitaniumIsDroppedAndRefunded()
    {
        // A v12 save with a T1 site on titanium (started before the rule) would never finish: it is dropped
        // on load and what it took goes back, like a site on a wall.
        var (factory, origin) = ClassicWithTitaniumSpot();
        var data = factory.Capture();
        var plates = factory.Wallet.MaterialCount("iron-plate");
        data.Sites.Add(new SiteSaveDto
        {
            Id = data.NextEntityId + 100,
            Spec = new BuildSpecSaveDto { StructureId = Miner.BasicId, Kind = "Miner", X = origin.X, Y = origin.Y },
            Progress = 0.5f,
            SpentByItem = [new CostLineSaveDto { ItemId = "iron-plate", Amount = 2 }],
            RefundBasisPoints = 10000
        });

        var reloaded = Factory.Restore(Content, data);

        Assert.Empty(reloaded.State.Sites);
        Assert.Empty(reloaded.Miners);
        Assert.Equal(plates + 2, reloaded.Wallet.MaterialCount("iron-plate"));
    }

    /// <summary>A classic 1000×1000 sandbox and a free 2×2 of titanium with a free cell east of it.</summary>
    private static (Factory Factory, GridPosition Origin) ClassicWithTitaniumSpot()
    {
        var factory = NewGame.Sandbox(Content, new GridPosition(500, 500), rules: new GameRules(InstantBuild: true));
        var terrain = factory.Terrain!;
        for (var y = 0; y + 1 < terrain.Height; y++)
        {
            for (var x = 0; x + 2 < terrain.Width; x++)
            {
                var at = new GridPosition(x, y);
                if (terrain.CountDepositTiles(at, Miner.Size, DepositKind.Titanium) == Miner.FootprintArea
                    && factory.CanOccupyFootprint(at, Miner.Size)
                    && factory.CanOccupy(new GridPosition(x + 2, y)))
                {
                    return (factory, at);
                }
            }
        }

        throw new InvalidOperationException("Nessun 2×2 di titanio libero nella mappa classica.");
    }

    /// <summary>Loads content.json changed by <paramref name="mutate"/> and returns the error message.</summary>
    private static string LoadMutated(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(File.ReadAllText(ContentPath))!.AsObject();
        mutate(root);
        var path = Path.Combine(Path.GetTempPath(), $"tindustry-content-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, root.ToJsonString());
        try
        {
            return Assert.Throws<InvalidDataException>(() => FactoryContent.Load(path)).Message;
        }
        finally
        {
            File.Delete(path);
        }
    }
}
