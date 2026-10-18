using TIndustry.Shared;
using Xunit;

namespace TIndustry.Shared.Tests;

/// <summary>Campaign maps as files (<see cref="CampaignMapFile"/>) and the shipped maps of every level.</summary>
public sealed class CampaignMapTests : IDisposable
{
    private static readonly string DataDir = Path.Combine(AppContext.BaseDirectory, "data");
    private static readonly string MapsDir = Path.Combine(DataDir, "maps");
    private static readonly FactoryContent Content = FactoryContent.Load(Path.Combine(DataDir, "content.json"));

    private readonly string dir = Path.Combine(Path.GetTempPath(), $"tindustry-mappe-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>A 40×30 meadow with the Core at (19, 14), a wall row at y = 2 and ore patches near the Core.</summary>
    private static TerrainMap Sample(Func<int, int, TerrainTile?>? change = null)
    {
        var tiles = new TerrainTile[40, 30];
        for (var x = 0; x < 40; x++)
        {
            for (var y = 0; y < 30; y++)
            {
                var tile = new TerrainTile(x < 5 ? FloorKind.Sand : FloorKind.Grass, WallKind.None, DepositKind.None);
                if (y == 2)
                {
                    tile = new TerrainTile(FloorKind.Stone, WallKind.StoneWall, DepositKind.None);
                }
                else if (x is >= 10 and < 13 && y is >= 10 and < 13)
                {
                    tile = tile with { Deposit = DepositKind.Iron };
                }
                else if (x is >= 26 and < 29 && y is >= 10 and < 13)
                {
                    tile = tile with { Deposit = DepositKind.Copper };
                }

                tiles[x, y] = change?.Invoke(x, y) ?? tile;
            }
        }

        return TerrainMap.FromTiles(tiles);
    }

    private void SaveSample(string id, TerrainMap map, GridPosition core) =>
        CampaignMapFile.Save(dir, id, map, core, ["prati", "sabbia"], "prova", Content);

    [Fact]
    public void CampaignMapLoadsAndMatchesSize()
    {
        var original = Sample();
        SaveSample("prova", original, new GridPosition(19, 14));

        var map = CampaignMapFile.Load(dir, "prova", Content, out var core);

        Assert.Equal((40, 30), (map.Width, map.Height));
        Assert.Equal(new GridPosition(19, 14), core);
        Assert.Equal("campagna:prova", map.Generator);
        Assert.Equal(TerrainMap.TerrainHash(original), TerrainMap.TerrainHash(map));
        Assert.Equal(new TerrainTile(FloorKind.Stone, WallKind.StoneWall, DepositKind.None), map[7, 2]);
        Assert.Equal(DepositKind.Iron, map[11, 11].Deposit);
        Assert.Equal(FloorKind.Sand, map[0, 0].Floor);
        Assert.Empty(CampaignMapFile.Validate(dir, "prova", Content));
    }

    [Fact]
    public void WallStandsOnTheFirstFloorOfItsBiome()
    {
        SaveSample("muro", Sample((x, y) => x == 30 && y == 25
            ? new TerrainTile(FloorKind.Ice, WallKind.IceWall, DepositKind.None)
            : null), new GridPosition(19, 14));

        var map = CampaignMapFile.Load(dir, "muro", Content, out _);

        Assert.Equal(new TerrainTile(FloorKind.Snow, WallKind.IceWall, DepositKind.None), map[30, 25]);
    }

    [Fact]
    public void UnknownColourIsReportedInItalian()
    {
        SaveSample("colore", Sample(), new GridPosition(19, 14));
        var path = CampaignMapFile.TerrainPath(dir, "colore");
        using (var stream = File.OpenRead(path))
        {
            var (w, h, rgba) = PngReader.Read(stream);
            var o = (4 * w + 3) * 4;
            (rgba[o], rgba[o + 1], rgba[o + 2]) = (255, 0, 255);
            stream.Close();
            PngWriter.WriteRgba(path, w, h, rgba);
        }

        var problems = CampaignMapFile.Validate(dir, "colore", Content);

        var problem = Assert.Single(problems);
        Assert.Contains("colori sconosciuti", problem);
        Assert.Contains("#FF00FF in (3, 4)", problem);
        var error = Assert.Throws<InvalidDataException>(() => CampaignMapFile.Load(dir, "colore", Content, out _));
        Assert.Contains("#FF00FF in (3, 4)", error.Message);
    }

    [Fact]
    public void CoreOnWallIsReported()
    {
        SaveSample("core", Sample(), new GridPosition(19, 1));

        var problems = CampaignMapFile.Validate(dir, "core", Content);

        Assert.Contains(problems, p => p.Contains("Il Core in (19, 1) è su una parete"));
    }

    [Fact]
    public void MissingFilesAndOresAreReported()
    {
        Assert.Contains(CampaignMapFile.Validate(dir, "assente", Content), p => p.Contains("Manca il file assente.json"));

        // A wall ring around the copper: enclosed, and the level needing copper sees none.
        SaveSample("chiuso", Sample((x, y) => (x is 25 or 29 && y is >= 9 and <= 13) || (y is 9 or 13 && x is >= 25 and <= 29)
            ? new TerrainTile(FloorKind.Stone, WallKind.StoneWall, DepositKind.None)
            : null), new GridPosition(19, 14));
        var level = new CampaignLevelDefinition("prova", "Prova", "", 1, Map: "chiuso");

        var problems = CampaignMapFile.Validate(dir, "chiuso", Content, level);

        Assert.Contains(problems, p => p.Contains("chiuse da pareti"));
        Assert.Contains(problems, p => p.Contains("celle di rame") && p.Contains("ce ne sono 0"));
    }

    [Fact]
    public void RequiredOresFollowRecipesAndResearch()
    {
        var catalog = CampaignCatalog.Load(Path.Combine(DataDir, "campaign.json"));
        DepositKind[] basic = [DepositKind.Iron, DepositKind.Copper];
        DepositKind[] smelting = [DepositKind.Iron, DepositKind.Copper, DepositKind.Coal];

        Assert.Equal(basic, CampaignMapFile.RequiredOres(Content, catalog.Find("level-01-primi-passi")!));
        Assert.Equal(smelting, CampaignMapFile.RequiredOres(Content, catalog.Find("level-03-lastre")!));
        Assert.Equal(
            [DepositKind.Iron, DepositKind.Copper, DepositKind.Coal, DepositKind.Lead],
            CampaignMapFile.RequiredOres(Content, catalog.Find("level-07-piombo")!));
        Assert.Equal(
            [DepositKind.Iron, DepositKind.Copper, DepositKind.Coal, DepositKind.Lead, DepositKind.Titanium],
            CampaignMapFile.RequiredOres(Content, catalog.Find("level-09-titanio")!));
    }

    [Fact]
    public void EveryCampaignLevelHasAValidMap()
    {
        var catalog = CampaignCatalog.Load(Path.Combine(DataDir, "campaign.json"));
        int[] sizes = [200, 250, 300, 350, 400, 500, 600, 700, 850, 1000];

        Assert.Equal(sizes.Length, catalog.Levels.Count);
        for (var i = 0; i < catalog.Levels.Count; i++)
        {
            var level = catalog.Levels[i];
            Assert.False(string.IsNullOrEmpty(level.Map), $"{level.Id}: manca la mappa");
            Assert.Equal([], CampaignMapFile.Validate(MapsDir, level.Map!, Content, level));

            var factory = NewGame.Campaign(Content, catalog, level, mapsDirectory: MapsDir);
            var map = CampaignMapFile.Load(MapsDir, level.Map!, Content, out var core);
            Assert.Equal((sizes[i], sizes[i]), (factory.Terrain!.Width, factory.Terrain.Height));
            Assert.Equal("campagna:" + level.Map, factory.Terrain.Generator);
            Assert.Contains(core, factory.CoreTiles);
            Assert.Equal(TerrainMap.TerrainHash(map), TerrainMap.TerrainHash(factory.Terrain));
        }
    }

    [Fact]
    public void UnreadableFileIsAProblemNotACrash()
    {
        SaveSample("bloccata", Sample(), new GridPosition(19, 14));
        using (File.Open(CampaignMapFile.TerrainPath(dir, "bloccata"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            if (OperatingSystem.IsWindows())
            {
                // Only Windows enforces the share lock: elsewhere the file stays readable.
                Assert.Contains(CampaignMapFile.Validate(dir, "bloccata", Content),
                    p => p.StartsWith("bloccata.png: impossibile leggere il file", StringComparison.Ordinal));
                var error = Assert.Throws<InvalidDataException>(() => CampaignMapFile.Load(dir, "bloccata", Content, out _));
                Assert.Contains("bloccata.png", error.Message);
            }
        }
    }

    [Fact]
    public void CoreOriginOnAWallIsRefused()
    {
        var catalog = CampaignCatalog.Load(Path.Combine(DataDir, "campaign.json"));
        var level = catalog.Levels[0];
        var map = CampaignMapFile.Load(MapsDir, level.Map!, Content, out _);
        var wall = Enumerable.Range(0, map.Width * map.Height)
            .Select(i => new GridPosition(i % map.Width, i / map.Width))
            .First(p => map[p].Wall != WallKind.None);

        var error = Assert.Throws<InvalidDataException>(() =>
            NewGame.Campaign(Content, catalog, level, coreOrigin: wall, mapsDirectory: MapsDir));

        Assert.Contains("una parete", error.Message);
    }

    [Fact]
    public void MapColoursAreDistinct()
    {
        Assert.Null(CampaignMapFile.ColourClash(Content));

        var floors = Content.Terrain.Floors;
        var clashing = new FactoryContent
        {
            Conveyors = Content.Conveyors,
            Recipes = Content.Recipes,
            Terrain = new TerrainTable(
                floors,
                Content.Terrain.Walls.Select(w => w.Kind == WallKind.StoneWall ? w with { Color = floors[0].Color } : w).ToList())
        };
        var clash = CampaignMapFile.ColourClash(clashing);

        Assert.Contains("Colori duplicati", clash);
        Assert.Contains("'stone' e 'stone-wall'", clash);
        SaveSample("doppio", Sample(), new GridPosition(19, 14));
        Assert.Equal([clash!], CampaignMapFile.Validate(dir, "doppio", clashing));
    }

    [Fact]
    public void ForcedSizeKeepsTheClassicGenerator()
    {
        var catalog = CampaignCatalog.Load(Path.Combine(DataDir, "campaign.json"));

        var factory = NewGame.Campaign(Content, catalog, catalog.Levels[0], mapWidth: 60, mapHeight: 60, mapsDirectory: MapsDir);

        Assert.Equal((60, 60), (factory.Terrain!.Width, factory.Terrain.Height));
        Assert.Equal(ClassicTerrainGenerator.Name, factory.Terrain.Generator);
    }
}
