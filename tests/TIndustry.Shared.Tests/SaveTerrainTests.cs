using System.Text;
using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// The terrain generator is saved (classic, biomes, campaign map file) and the terrain regenerated on load must be
/// the saved one; sites and drone plans on terrain that blocks building are dropped on load.
/// </summary>
[Collection(SaveStoreCollection.Name)]
public sealed class SaveTerrainTests
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly string MapsDir = Path.Combine(DataDir, "maps");
    private static readonly FactoryContent Content = FactoryContent.Load(Path.Combine(DataDir, "content.json"));
    private static readonly CampaignCatalog Catalog = CampaignCatalog.Load(Path.Combine(DataDir, "campaign.json"));

    private static string SnapshotJson(string slotId)
    {
        var file = JsonDocument.Parse(File.ReadAllText(FactorySaveStore.SlotPath(slotId)));
        var snapshot = file.RootElement.GetProperty("snapshot").GetString()!;
        return Encoding.UTF8.GetString(Convert.FromBase64String(snapshot));
    }

    private static FactorySaveData ReadData(byte[] snapshot) =>
        JsonSerializer.Deserialize<FactorySaveData>(snapshot, FactorySaveStore.JsonOptions)!;

    [Fact]
    public void RoundTripBiomes()
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(100, 100), map: MapGenSettings.Normal((MapSize)200, 31),
            rules: new GameRules(InstantBuild: true));
        var host = new SimHost(factory);
        host.Submit(new PlaceBelt(0, new GridPosition(98, 100), Direction.East, "conveyor-basic"));
        host.AdvanceTicks(30);

        FactorySaveStore.Save("terreno-biomi", host);
        var data = ReadData(Encoding.UTF8.GetBytes(SnapshotJson("terreno-biomi")));
        Assert.Equal(("biomi-1", 200, (string?)null), (data.Generator, data.MapSize, data.MapId));
        Assert.Equal(TerrainMap.TerrainHash(factory.Terrain!), data.TerrainHash);

        Assert.True(FactorySaveStore.TryLoad(Content, "terreno-biomi", out var loaded, out var error), error);
        Assert.Equal(MapGenerator.Name, loaded!.State.Terrain!.Generator);
        Assert.Equal(TerrainMap.TerrainHash(factory.Terrain!), TerrainMap.TerrainHash(loaded.State.Terrain));
        Assert.Equal(host.Checksum(), loaded.Checksum());
        Assert.True(loaded.State.Belts.ContainsKey(new GridPosition(98, 100)));
    }

    private static FactorySaveData BiomeSave() =>
        NewGame.Sandbox(Content, new GridPosition(100, 100), map: MapGenSettings.Normal((MapSize)200, 31)).Capture();

    [Fact]
    public void OldBiomeNameLoadsAsTheFirstBiomeGenerator()
    {
        // "biomi" (the name before the generator had a version) still means the same frozen generator.
        var data = BiomeSave();
        var hash = data.TerrainHash;
        data.Generator = "biomi";
        data.TerrainHash = null;

        var restored = Factory.Restore(Content, data);

        Assert.Equal(MapGenerator.Name, restored.Terrain!.Generator);
        Assert.Equal(hash, TerrainMap.TerrainHash(restored.Terrain));
    }

    [Fact]
    public void TerrainHashMustMatchTheRegeneratedTerrain()
    {
        var data = BiomeSave();
        Assert.NotNull(data.TerrainHash);
        Assert.NotNull(Factory.Restore(Content, data).Terrain);

        // A generator that no longer makes the saved terrain: refused, never loaded onto another map.
        data.TerrainHash += 1;
        var ex = Assert.Throws<SaveMapException>(() => Factory.Restore(Content, data));
        Assert.StartsWith("Il terreno rigenerato non corrisponde al salvataggio", ex.Message);
        Assert.Equal((SaveLoadError.TerrainChanged, MapGenerator.Name), (ex.Code, ex.Argument));

        var classic = NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 6).Capture();
        classic.TerrainHash ^= 0xFF;
        Assert.Throws<SaveMapException>(() => Factory.Restore(Content, classic));
    }

    [Fact]
    public void RoundTripCampaignMap()
    {
        var level = Catalog.Levels[0];
        var host = new SimHost(NewGame.Campaign(Content, Catalog, level, mapsDirectory: MapsDir));
        host.AdvanceTicks(10);

        FactorySaveStore.Save("terreno-campagna", host);
        var data = ReadData(Encoding.UTF8.GetBytes(SnapshotJson("terreno-campagna")));
        Assert.Equal(("campagna", 0, level.Map), (data.Generator, data.MapSize, data.MapId));

        Assert.True(FactorySaveStore.TryLoad(Content, "terreno-campagna", out var loaded, out var error, MapsDir), error);
        Assert.Equal("campagna:" + level.Map, loaded!.State.Terrain!.Generator);
        Assert.Equal(TerrainMap.TerrainHash(host.State.Terrain!), TerrainMap.TerrainHash(loaded.State.Terrain));
        Assert.Equal(host.Checksum(), loaded.Checksum());
    }

    [Fact]
    public void SlotListNamesTheGeneratorAndMap()
    {
        var biomes = new SimHost(NewGame.Sandbox(Content, new GridPosition(100, 100), map: MapGenSettings.Normal((MapSize)200, 31)));
        var classic = new SimHost(NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 6));
        var level = Catalog.Levels[0];
        var campaign = new SimHost(NewGame.Campaign(Content, Catalog, level, mapsDirectory: MapsDir));
        FactorySaveStore.Save("elenco-biomi", biomes);
        FactorySaveStore.Save("elenco-classico", classic);
        FactorySaveStore.Save("elenco-campagna", campaign);

        var slots = FactorySaveStore.ListSlots();

        var b = Assert.Single(slots, s => s.Id == "elenco-biomi");
        Assert.Equal(("biomi-1", (string?)null, 31, 200), (b.Generator, b.MapId, b.Seed, b.MapWidth));
        var c = Assert.Single(slots, s => s.Id == "elenco-classico");
        Assert.Equal(("classico", (string?)null), (c.Generator, c.MapId));
        var m = Assert.Single(slots, s => s.Id == "elenco-campagna");
        Assert.Equal(("campagna", level.Map), (m.Generator, m.MapId));

        // One slot without listing them all: the same row.
        var one = FactorySaveStore.ReadSlotInfo("elenco-biomi");
        Assert.Equal((b.Generator, b.Seed, b.MapWidth, b.MapHeight), (one!.Generator, one.Seed, one.MapWidth, one.MapHeight));
        Assert.Null(FactorySaveStore.ReadSlotInfo("elenco-inesistente"));
    }

    [Fact]
    public void CampaignSaveWithoutItsMapFailsWithClearMessage()
    {
        var level = Catalog.Levels[0];
        var host = new SimHost(NewGame.Campaign(Content, Catalog, level, mapsDirectory: MapsDir));
        FactorySaveStore.Save("terreno-senza-mappa", host);

        Assert.False(FactorySaveStore.TryLoad(Content, "terreno-senza-mappa", out var noDir, out var error));
        Assert.Null(noDir);
        Assert.Equal($"Mappa della campagna '{level.Map}' non disponibile: cartella delle mappe non trovata.", error);
        // The client gets the reason as a code with the map id (it shows its own text).
        Assert.False(FactorySaveStore.TryLoadSlot(Content, "terreno-senza-mappa", out _, out var failure));
        Assert.Equal((SaveLoadError.MapsFolderMissing, level.Map), (failure!.Code, failure.Argument));

        var empty = Path.Combine(Path.GetTempPath(), $"tindustry-mappe-vuote-{Guid.NewGuid():N}");
        Directory.CreateDirectory(empty);
        try
        {
            Assert.False(FactorySaveStore.TryLoad(Content, "terreno-senza-mappa", out var missing, out error, empty));
            Assert.Null(missing);
            Assert.StartsWith($"Mappa della campagna '{level.Map}' mancante o non valida.", error);
            Assert.False(FactorySaveStore.TryLoadSlot(Content, "terreno-senza-mappa", out _, out var missingFailure, empty));
            Assert.Equal((SaveLoadError.CampaignMapMissing, level.Map), (missingFailure!.Code, missingFailure.Argument));
        }
        finally
        {
            Directory.Delete(empty, recursive: true);
        }

        // Another level's map in its place has another size: refused, never silently used.
        var data = host.State is Factory f ? f.Capture() : throw new InvalidOperationException();
        data.MapId = Catalog.Levels[1].Map;
        var ex = Assert.Throws<SaveMapException>(() => Factory.Restore(Content, data, MapsDir));
        Assert.StartsWith($"La mappa della campagna '{data.MapId}' è cambiata", ex.Message);

        // Only ids a map file can have (the same rule as CampaignMapFile): never a path, never upper case.
        foreach (var id in new[] { "../maps/" + level.Map, "Foo", "c:mappa", "liv 01" })
        {
            data.MapId = id;
            ex = Assert.Throws<SaveMapException>(() => Factory.Restore(Content, data, MapsDir));
            Assert.Equal($"Nome della mappa della campagna non valido: '{id}'.", ex.Message);
            Assert.Equal((SaveLoadError.CampaignMapBadName, id), (ex.Code, ex.Argument));
        }
    }

    [Fact]
    public void UnknownGeneratorIsRefused()
    {
        var data = NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120).Capture();
        data.Generator = "vulcani";

        var ex = Assert.Throws<SaveMapException>(() => Factory.Restore(Content, data));
        Assert.Equal("Generatore di mappa sconosciuto nel salvataggio: 'vulcani'.", ex.Message);
        Assert.Equal((SaveLoadError.UnknownGenerator, "vulcani"), (ex.Code, ex.Argument));
    }

    /// <summary>Level 1's map with its wall cell nearest to the Core.</summary>
    private static (Factory Factory, GridPosition Wall) CampaignWithWall()
    {
        var factory = NewGame.Campaign(Content, Catalog, Catalog.Levels[0], mapsDirectory: MapsDir);
        var map = factory.Terrain!;
        var core = factory.CoreTiles.Min(t => (t.X, t.Y));
        GridPosition? best = null;
        var bestDistance = int.MaxValue;
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var distance = Math.Abs(x - core.X) + Math.Abs(y - core.Y);
                if (map[x, y].Wall != WallKind.None && distance < bestDistance)
                {
                    (best, bestDistance) = (new GridPosition(x, y), distance);
                }
            }
        }

        Assert.NotNull(best);
        return (factory, best!.Value);
    }

    [Fact]
    public void SiteOnWallIsDroppedOnLoadWithRefund()
    {
        var (factory, wall) = CampaignWithWall();
        var spec = new BuildSpec("conveyor-basic", null, wall, Direction.East);
        var cost = Assert.Single(factory.CostOf(spec), c => c.Amount > 0);
        var data = factory.Capture();
        var before = cost.ItemId is null ? data.Money : data.Materials.GetValueOrDefault(cost.ItemId);
        var siteId = data.NextEntityId + 5;
        data.Sites.Add(new SiteSaveDto
        {
            Id = siteId,
            Spec = new BuildSpecSaveDto { StructureId = "conveyor-basic", X = wall.X, Y = wall.Y },
            Progress = 0.5f,
            SpentByItem = [new CostLineSaveDto { ItemId = cost.ItemId, Amount = cost.Amount }],
            RefundBasisPoints = Construction.FullRefundBasisPoints
        });
        data.Builders[0].CurrentSiteId = siteId;
        data.Builders[0].Plans.Add(new BuildPlanSaveDto { Spec = data.Sites[0].Spec });
        // Outside the map: dropped the same way, nothing spent.
        data.Sites.Add(new SiteSaveDto
        {
            Id = siteId + 1,
            Spec = new BuildSpecSaveDto { StructureId = "conveyor-basic", X = 5000, Y = 3 }
        });

        var restored = Factory.Restore(Content, data, MapsDir);

        Assert.Empty(restored.Sites);
        Assert.Empty(restored.PlansOf(0));
        Assert.Null(restored.Builders[0].CurrentSiteId);
        var after = cost.ItemId is null ? restored.Wallet.Money : restored.Wallet.MaterialCount(cost.ItemId);
        Assert.Equal(before + cost.Amount, after);
    }

    [Fact]
    public void PlanOnWallIsDroppedWithWallReason()
    {
        var (factory, wall) = CampaignWithWall();
        var data = factory.Capture();
        data.Builders[0].Plans.Add(new BuildPlanSaveDto
        {
            Spec = new BuildSpecSaveDto { StructureId = "conveyor-basic", X = wall.X, Y = wall.Y }
        });
        var host = new SimHost(Factory.Restore(Content, data, MapsDir));
        var dropped = new List<PlanDropped>();
        host.EventRaised += e =>
        {
            if (e is PlanDropped d)
            {
                dropped.Add(d);
            }
        };

        for (var i = 0; i < 6000 && dropped.Count == 0; i++)
        {
            host.AdvanceTicks(1);
        }

        var drop = Assert.Single(dropped);
        Assert.Equal(wall, drop.Plan.Spec.Origin);
        Assert.Equal(RefusalCode.Wall, drop.Reason);
    }
}
