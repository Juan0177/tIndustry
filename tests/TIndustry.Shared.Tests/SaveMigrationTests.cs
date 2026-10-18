using System.Text;
using System.Text.Json;
using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>
/// Save v12: the terrain generator is saved (classic, biomes, campaign map file); v11 saves load on the
/// classic generator; sites and drone plans on terrain that blocks building are dropped on load.
/// </summary>
[Collection(SaveStoreCollection.Name)]
public sealed class SaveMigrationTests
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly string MapsDir = Path.Combine(DataDir, "maps");
    private static readonly FactoryContent Content = FactoryContent.Load(Path.Combine(DataDir, "content.json"));
    private static readonly CampaignCatalog Catalog = CampaignCatalog.Load(Path.Combine(DataDir, "campaign.json"));

    /// <summary>
    /// Written by the v11 code of main (243a870): sandbox 120×120, seed 6, Core (60, 60), a miner, a smelter,
    /// four belts (two of them on classic water at (11, 18)) and a miner site at (66, 56) at about 55%.
    /// </summary>
    private static readonly string V11Fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "save-v11.json");

    /// <summary>Terrain fingerprint of the fixture's map, measured with the v11 code.</summary>
    private const ulong V11TerrainHash = 2630313119214773145UL;

    private static string SnapshotJson(string slotId)
    {
        var file = JsonDocument.Parse(File.ReadAllText(FactorySaveStore.SlotPath(slotId)));
        var snapshot = file.RootElement.GetProperty("snapshot").GetString()!;
        return Encoding.UTF8.GetString(Convert.FromBase64String(snapshot));
    }

    private static FactorySaveData ReadData(byte[] snapshot) =>
        JsonSerializer.Deserialize<FactorySaveData>(snapshot, FactorySaveStore.JsonOptions)!;

    [Fact]
    public void V11FixtureLoadsWithClassicTerrain()
    {
        Directory.CreateDirectory(FactorySaveStore.SavesDirectory);
        File.Copy(V11Fixture, FactorySaveStore.SlotPath("fixture-v11"), overwrite: true);
        Assert.Contains("\"version\":11", SnapshotJson("fixture-v11"));

        Assert.True(FactorySaveStore.TryLoad(Content, "fixture-v11", out var host, out var error), error);
        var state = host!.State;

        var terrain = state.Terrain!;
        Assert.Equal(ClassicTerrainGenerator.Name, terrain.Generator);
        Assert.Equal((120, 120, 6), (terrain.Width, terrain.Height, terrain.Seed));
        Assert.Equal(V11TerrainHash, TerrainMap.TerrainHash(terrain));

        Assert.Equal(25, state.CurrentTick);
        Assert.Single(state.Miners);
        Assert.Single(state.Smelters);
        Assert.Equal(4, state.Belts.Count);
        var onWater = new GridPosition(11, 18);
        Assert.True(state.Belts.ContainsKey(onWater));
        Assert.Equal(FloorKind.ShallowWater, terrain[onWater].Floor);

        // Classic water stays buildable after the migration: the first free water cell of the lake under the belts.
        var freeWater = Enumerable.Range(0, terrain.Height)
            .SelectMany(y => Enumerable.Range(0, terrain.Width).Select(x => new GridPosition(x, y)))
            .First(c => terrain[c].Floor == FloorKind.ShallowWater && !state.Belts.ContainsKey(c));
        Assert.True(((Factory)state).CanPlace(
            new BuildSpec("conveyor-basic", null, freeWater, Direction.East), out var waterReason), waterReason?.ToString());
        var site = Assert.Single(state.Sites);
        Assert.Equal(new GridPosition(66, 56), site.Spec.Origin);
        Assert.InRange(site.Progress, 0.5f, 0.6f);
        Assert.Equal(new GridPosition(66, 56), Assert.Single(state.PlansOf(0)).Spec.Origin);

        // Saved again it is a v12 save on the classic generator, and the site still gets built.
        FactorySaveStore.Save("fixture-v12", host);
        var data = ReadData(Encoding.UTF8.GetBytes(SnapshotJson("fixture-v12")));
        Assert.Equal((12, "classico", 0, (string?)null), (data.Version, data.Generator, data.MapSize, data.MapId));
        Assert.True(FactorySaveStore.TryLoad(Content, "fixture-v12", out var resaved, out error), error);
        Assert.Equal(host.Checksum(), resaved!.Checksum());
        host.AdvanceTicks(120);
        Assert.Empty(host.State.Sites);
        Assert.Equal(2, host.State.Miners.Count);
    }

    [Fact]
    public void V11SnapshotWithoutGeneratorReadsAsClassic()
    {
        var host = new SimHost(NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 6));
        var json = Encoding.UTF8.GetString(host.CreateSnapshot());
        Assert.Contains("\"generator\":\"classico\"", json);
        var v11 = json.Replace("\"version\":12", "\"version\":11").Replace("\"generator\":\"classico\",", "");

        var state = Snapshot.Read(Content, Encoding.UTF8.GetBytes(v11));

        Assert.Equal(ClassicTerrainGenerator.Name, state.Terrain!.Generator);
        Assert.Equal(TerrainMap.TerrainHash(host.State.Terrain!), TerrainMap.TerrainHash(state.Terrain));
    }

    [Fact]
    public void V12RoundTripBiomes()
    {
        var factory = NewGame.Sandbox(
            Content, new GridPosition(100, 100), map: MapGenSettings.Normal((MapSize)200, 31),
            rules: new GameRules(InstantBuild: true));
        var host = new SimHost(factory);
        host.Submit(new PlaceBelt(0, new GridPosition(98, 100), Direction.East, "conveyor-basic"));
        host.AdvanceTicks(30);

        FactorySaveStore.Save("v12-biomi", host);
        var data = ReadData(Encoding.UTF8.GetBytes(SnapshotJson("v12-biomi")));
        Assert.Equal(("biomi-1", 200, (string?)null), (data.Generator, data.MapSize, data.MapId));
        Assert.Equal(TerrainMap.TerrainHash(factory.Terrain!), data.TerrainHash);

        Assert.True(FactorySaveStore.TryLoad(Content, "v12-biomi", out var loaded, out var error), error);
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
        // v12 saves written before the generator was versioned say "biomi": the same frozen generator.
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

        var classic = NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 6).Capture();
        classic.TerrainHash ^= 0xFF;
        Assert.Throws<SaveMapException>(() => Factory.Restore(Content, classic));

        // Without a hash (v12 saves written before it) the terrain loads as before.
        data.TerrainHash = null;
        Assert.NotNull(Factory.Restore(Content, data).Terrain);
    }

    [Fact]
    public void V12RoundTripCampaignMap()
    {
        var level = Catalog.Levels[0];
        var host = new SimHost(NewGame.Campaign(Content, Catalog, level, mapsDirectory: MapsDir));
        host.AdvanceTicks(10);

        FactorySaveStore.Save("v12-campagna", host);
        var data = ReadData(Encoding.UTF8.GetBytes(SnapshotJson("v12-campagna")));
        Assert.Equal(("campagna", 0, level.Map), (data.Generator, data.MapSize, data.MapId));

        Assert.True(FactorySaveStore.TryLoad(Content, "v12-campagna", out var loaded, out var error, MapsDir), error);
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

        // A v11 file has neither field: the list leaves them empty (the game loads it on the classic generator).
        Directory.CreateDirectory(FactorySaveStore.SavesDirectory);
        File.Copy(V11Fixture, FactorySaveStore.SlotPath("elenco-v11"), overwrite: true);
        var old = Assert.Single(FactorySaveStore.ListSlots(), s => s.Id == "elenco-v11");
        Assert.Equal(((string?)null, (string?)null), (old.Generator, old.MapId));
    }

    [Fact]
    public void CampaignSaveWithoutItsMapFailsWithClearMessage()
    {
        var level = Catalog.Levels[0];
        var host = new SimHost(NewGame.Campaign(Content, Catalog, level, mapsDirectory: MapsDir));
        FactorySaveStore.Save("v12-senza-mappa", host);

        Assert.False(FactorySaveStore.TryLoad(Content, "v12-senza-mappa", out var noDir, out var error));
        Assert.Null(noDir);
        Assert.Equal($"Mappa della campagna '{level.Map}' non disponibile: cartella delle mappe non trovata.", error);

        var empty = Path.Combine(Path.GetTempPath(), $"tindustry-mappe-vuote-{Guid.NewGuid():N}");
        Directory.CreateDirectory(empty);
        try
        {
            Assert.False(FactorySaveStore.TryLoad(Content, "v12-senza-mappa", out var missing, out error, empty));
            Assert.Null(missing);
            Assert.StartsWith($"Mappa della campagna '{level.Map}' mancante o non valida.", error);
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
        }
    }

    [Fact]
    public void UnknownGeneratorIsRefused()
    {
        var data = NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120).Capture();
        data.Generator = "vulcani";

        var ex = Assert.Throws<SaveMapException>(() => Factory.Restore(Content, data));
        Assert.Equal("Generatore di mappa sconosciuto nel salvataggio: 'vulcani'.", ex.Message);
    }

    [Fact]
    public void V10StillRejected()
    {
        Directory.CreateDirectory(FactorySaveStore.SavesDirectory);
        var path = FactorySaveStore.SlotPath("fixture-v10");
        File.WriteAllText(path, File.ReadAllText(V11Fixture).Replace("\"version\": 11", "\"version\": 10"));

        Assert.False(FactorySaveStore.TryLoad(Content, "fixture-v10", out var loaded, out var error));
        Assert.Null(loaded);
        Assert.Equal("Salvataggio di una versione precedente: non compatibile.", error);

        var snapshot = Encoding.UTF8.GetString(host().CreateSnapshot()).Replace("\"version\":12", "\"version\":10");
        Assert.Throws<InvalidDataException>(() => Snapshot.Read(Content, Encoding.UTF8.GetBytes(snapshot)));

        static SimHost host() => new(NewGame.Demo(Content));
    }

    [Fact]
    public void SaveWritesVersion12()
    {
        var host = new SimHost(NewGame.Sandbox(Content, new GridPosition(60, 60), mapWidth: 120, mapHeight: 120, seed: 3));
        FactorySaveStore.Save("v12-versione", host);

        Assert.Contains("\"version\": 12", File.ReadAllText(FactorySaveStore.SlotPath("v12-versione")));
        var snapshot = SnapshotJson("v12-versione");
        Assert.Contains("\"version\":12", snapshot);
        Assert.Contains("\"generator\":\"classico\",\"mapSize\":0", snapshot);
        Assert.DoesNotContain("mapId", snapshot);
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
